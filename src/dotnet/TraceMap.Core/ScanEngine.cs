namespace TraceMap.Core;

public static class ScanEngine
{
    public static ScanResult Scan(ScanOptions options)
    {
        var repoPath = Path.GetFullPath(options.RepoPath);
        var outputPath = Path.GetFullPath(options.OutputPath);
        if (!Directory.Exists(repoPath))
        {
            throw new DirectoryNotFoundException($"Repository path does not exist: {repoPath}");
        }

        var git = GitMetadataProvider.Detect(repoPath);
        var inventory = ApplyScope(FileInventory.Collect(repoPath, outputPath), repoPath, options);
        var solutions = inventory
            .Where(item => item.Kind == "Solution")
            .Select(item => item.RelativePath)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        var projects = inventory
            .Where(item => item.Kind == "Project")
            .Select(item => item.RelativePath)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        var targetFrameworkInfos = ProjectFileReader.ReadTargetFrameworks(repoPath, inventory);
        var targetFrameworks = targetFrameworkInfos
            .Select(item => item.TargetFramework)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var semanticResult = CSharpSemanticExtractor.Extract(repoPath, inventory, options);

        var knownGaps = git.KnownGaps
            .Concat(semanticResult.GapFacts.Select(GetGapMessage))
            .OrderBy(gap => gap, StringComparer.Ordinal)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var buildStatus = semanticResult.Attempted
            ? semanticResult.ReducedCoverage ? "FailedOrPartial" : "Succeeded"
            : "NotRun";
        var analysisLevel = semanticResult.Attempted
            ? semanticResult.ReducedCoverage ? "Level1SemanticAnalysisReduced" : "Level1SemanticAnalysis"
            : "Level3SyntaxAnalysis";

        var manifest = new ScanManifest(
            CreateScanId(git, inventory),
            git.RepoName,
            git.RemoteUrl,
            git.Branch,
            git.CommitSha,
            ScannerVersions.TraceMap,
            DateTimeOffset.UtcNow,
            analysisLevel,
            buildStatus,
            solutions,
            projects,
            targetFrameworks,
            knownGaps,
            GetScanRootRelativePath(repoPath, git),
            FactFactory.Hash(repoPath, 32),
            string.IsNullOrWhiteSpace(git.GitRootPath) ? null : FactFactory.Hash(Path.GetFullPath(git.GitRootPath), 32));

        var facts = CreateFacts(manifest, inventory, targetFrameworkInfos, ProjectFileReader.ReadPackageReferences(repoPath, inventory), knownGaps, repoPath, semanticResult, options);
        return new ScanResult(manifest, facts, inventory);
    }

    private static string CreateScanId(GitMetadata git, IReadOnlyList<FileInventoryItem> inventory)
    {
        var signature = string.Join('\n', inventory.Select(item => $"{item.RelativePath}|{item.Kind}|{item.SizeBytes}"));
        var repoIdentity = string.IsNullOrWhiteSpace(git.RemoteUrl) ? git.RepoName : git.RemoteUrl;
        return "scan-" + FactFactory.Hash($"{repoIdentity}|{git.CommitSha}|{signature}", 20);
    }

    private static string GetScanRootRelativePath(string repoPath, GitMetadata git)
    {
        if (string.IsNullOrWhiteSpace(git.GitRootPath))
        {
            return ".";
        }

        var relative = Path.GetRelativePath(git.GitRootPath, repoPath);
        return relative is "." or ""
            ? "."
            : FileInventory.NormalizeRelativePath(relative);
    }

    private static IReadOnlyList<CodeFact> CreateFacts(
        ScanManifest manifest,
        IReadOnlyList<FileInventoryItem> inventory,
        IReadOnlyList<TargetFrameworkInfo> targetFrameworks,
        IReadOnlyList<PackageReferenceInfo> packageReferences,
        IReadOnlyList<string> knownGaps,
        string repoPath,
        SemanticExtractionResult semanticResult,
        ScanOptions options)
    {
        var facts = new List<CodeFact>
        {
            FactFactory.Create(
                manifest,
                FactTypes.RepoScanned,
                RuleIds.RepoManifest,
                EvidenceTiers.Tier2Structural,
                new EvidenceSpan(".", 1, 1, null, "RepoManifestExtractor", ScannerVersions.RepoManifestExtractor),
                properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["fileCount"] = inventory.Count.ToString(),
                    ["projectCount"] = manifest.Projects.Count.ToString(),
                    ["solutionCount"] = manifest.Solutions.Count.ToString(),
                    ["scanScopeSolutions"] = string.Join(",", options.SolutionPaths ?? []),
                    ["scanScopeProjects"] = string.Join(",", options.ProjectPaths ?? []),
                    ["scanScopeIncludes"] = string.Join(",", options.IncludeGlobs ?? []),
                    ["scanScopeExcludes"] = string.Join(",", options.ExcludeGlobs ?? []),
                    ["targetFramework"] = options.TargetFramework ?? string.Empty,
                    ["restoreRequested"] = options.Restore.ToString()
                }),
            FactFactory.Create(
                manifest,
                FactTypes.BuildStatus,
                RuleIds.RepoManifest,
                EvidenceTiers.Tier4Unknown,
                new EvidenceSpan(".", 1, 1, null, "RepoManifestExtractor", ScannerVersions.RepoManifestExtractor),
                properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["status"] = manifest.BuildStatus,
                    ["reason"] = manifest.BuildStatus switch
                    {
                        "Succeeded" => "MSBuildWorkspace loaded projects and Roslyn compilation reported no errors.",
                        "FailedOrPartial" => "MSBuildWorkspace project load or Roslyn compilation reported gaps; syntax fallback still ran.",
                        _ => "No C# project was available for MSBuildWorkspace semantic analysis."
                    }
                })
        };

        foreach (var gap in knownGaps)
        {
            facts.Add(FactFactory.Create(
                manifest,
                FactTypes.AnalysisGap,
                RuleIds.RepoManifest,
                EvidenceTiers.Tier4Unknown,
                new EvidenceSpan(".", 1, 1, null, "RepoManifestExtractor", ScannerVersions.RepoManifestExtractor),
                properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["message"] = gap
                }));
        }

        foreach (var item in inventory)
        {
            facts.Add(FactFactory.Create(
                manifest,
                FactTypes.FileInventoried,
                RuleIds.FileInventory,
                EvidenceTiers.Tier2Structural,
                new EvidenceSpan(item.RelativePath, 1, 1, null, "FileInventoryExtractor", ScannerVersions.FileInventoryExtractor),
                properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["kind"] = item.Kind,
                    ["sizeBytes"] = item.SizeBytes.ToString()
                }));

            if (item.Kind == "Solution")
            {
                facts.Add(FactFactory.Create(
                    manifest,
                    FactTypes.SolutionDeclared,
                    RuleIds.ProjectFile,
                    EvidenceTiers.Tier2Structural,
                    new EvidenceSpan(item.RelativePath, 1, 1, null, "ProjectFileExtractor", ScannerVersions.ProjectFileExtractor),
                    properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["path"] = item.RelativePath
                    }));
            }
            else if (item.Kind == "Project")
            {
                facts.Add(FactFactory.Create(
                    manifest,
                    FactTypes.ProjectDeclared,
                    RuleIds.ProjectFile,
                    EvidenceTiers.Tier2Structural,
                    new EvidenceSpan(item.RelativePath, 1, 1, null, "ProjectFileExtractor", ScannerVersions.ProjectFileExtractor),
                    projectPath: item.RelativePath,
                    properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["path"] = item.RelativePath
                    }));
            }
            else if (item.Kind == "Config" || item.Kind == "Json")
            {
                facts.Add(FactFactory.Create(
                    manifest,
                    FactTypes.ConfigFileDeclared,
                    RuleIds.FileInventory,
                    EvidenceTiers.Tier2Structural,
                    new EvidenceSpan(item.RelativePath, 1, 1, null, "FileInventoryExtractor", ScannerVersions.FileInventoryExtractor),
                    properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["path"] = item.RelativePath,
                        ["kind"] = item.Kind
                    }));
            }
            else if (item.Kind == "Sql")
            {
                facts.Add(FactFactory.Create(
                    manifest,
                    FactTypes.SqlFileDeclared,
                    RuleIds.FileInventory,
                    EvidenceTiers.Tier3SyntaxOrTextual,
                    new EvidenceSpan(item.RelativePath, 1, 1, null, "FileInventoryExtractor", ScannerVersions.FileInventoryExtractor),
                    properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["path"] = item.RelativePath
                    }));
            }
        }

        foreach (var item in targetFrameworks)
        {
            facts.Add(FactFactory.Create(
                manifest,
                FactTypes.TargetFrameworkDeclared,
                RuleIds.ProjectFile,
                EvidenceTiers.Tier2Structural,
                new EvidenceSpan(item.ProjectPath, item.Line, item.Line, null, "ProjectFileExtractor", ScannerVersions.ProjectFileExtractor),
                projectPath: item.ProjectPath,
                targetSymbol: item.TargetFramework,
                properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["targetFramework"] = item.TargetFramework
                }));
        }

        foreach (var item in packageReferences)
        {
            var packageProperties = new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["dependencyGroup"] = item.DependencyGroup,
                ["dependencyScope"] = item.DependencyScope,
                ["ecosystem"] = "nuget",
                ["manifestKind"] = item.ManifestKind,
                ["package"] = item.PackageName,
                ["packageManager"] = "nuget",
                ["packageName"] = item.PackageName,
                ["sourceKind"] = item.ManifestKind == "packages.config" ? "manifest" : "build-file",
                ["surfaceKind"] = "package-config",
                ["targetFramework"] = item.TargetFramework ?? string.Empty
            };
            AddSafeVersionProperties(packageProperties, item.Version);
            facts.Add(FactFactory.Create(
                manifest,
                FactTypes.PackageReferenced,
                RuleIds.ProjectFile,
                EvidenceTiers.Tier2Structural,
                new EvidenceSpan(item.ProjectPath, item.Line, item.Line, null, "ProjectFileExtractor", ScannerVersions.ProjectFileExtractor),
                projectPath: item.ProjectPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ? item.ProjectPath : null,
                targetSymbol: item.PackageName,
                properties: packageProperties));
        }

        facts.AddRange(CSharpSyntaxExtractor.Extract(repoPath, manifest, inventory));
        facts.AddRange(CSharpIntegrationSyntaxExtractor.Extract(repoPath, manifest, inventory));
        facts.AddRange(SqlFileExtractor.Extract(repoPath, manifest, inventory));
        facts.AddRange(ConfigExtractor.Extract(repoPath, manifest, inventory));
        facts.AddRange(CSharpSemanticExtractor.MaterializeFacts(manifest, semanticResult.GapFacts));
        facts.AddRange(CSharpSemanticExtractor.MaterializeFacts(manifest, semanticResult.Facts));

        return facts
            .GroupBy(fact => fact.FactId, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(fact => fact.FactType, StringComparer.Ordinal)
            .ThenBy(fact => fact.Evidence.FilePath, StringComparer.Ordinal)
            .ThenBy(fact => fact.Evidence.StartLine)
            .ThenBy(fact => fact.TargetSymbol, StringComparer.Ordinal)
            .ThenBy(fact => fact.FactId, StringComparer.Ordinal)
            .ToArray();
    }

    private static string GetGapMessage(SemanticFactCandidate gap)
    {
        return gap.Properties is not null && gap.Properties.TryGetValue("message", out var message)
            ? message
            : "Roslyn semantic analysis reported a gap.";
    }

    private static void AddSafeVersionProperties(SortedDictionary<string, string> properties, string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            properties["version"] = string.Empty;
            return;
        }

        var trimmed = version.Trim();
        if (IsUnsafePackageVersion(trimmed))
        {
            properties["versionHash"] = FactFactory.Hash(trimmed, 32);
            properties["redactionReason"] = "unsafe-package-version";
            return;
        }

        properties["version"] = trimmed;
    }

    private static bool IsUnsafePackageVersion(string value)
    {
        return value.Contains("://", StringComparison.Ordinal)
            || value.Contains("\\", StringComparison.Ordinal)
            || value.StartsWith("/", StringComparison.Ordinal)
            || value.StartsWith("./", StringComparison.Ordinal)
            || value.StartsWith("../", StringComparison.Ordinal)
            || value.StartsWith("file:", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("git+", StringComparison.OrdinalIgnoreCase)
            || value.Contains("${", StringComparison.Ordinal)
            || value.Contains("$(", StringComparison.Ordinal)
            || value.Contains("%", StringComparison.Ordinal);
    }

    private static IReadOnlyList<FileInventoryItem> ApplyScope(
        IReadOnlyList<FileInventoryItem> inventory,
        string repoPath,
        ScanOptions options)
    {
        var solutionPaths = NormalizeOptionPaths(repoPath, options.SolutionPaths);
        var projectPaths = NormalizeOptionPaths(repoPath, options.ProjectPaths);
        var includeGlobs = (options.IncludeGlobs ?? []).Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
        var excludeGlobs = (options.ExcludeGlobs ?? []).Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
        var projectDirectories = projectPaths
            .Select(path => FileInventory.NormalizeRelativePath(Path.GetDirectoryName(path) ?? "."))
            .ToArray();

        return inventory
            .Where(item => includeGlobs.Length == 0 || includeGlobs.Any(glob => GlobMatches(item.RelativePath, glob)))
            .Where(item => excludeGlobs.Length == 0 || !excludeGlobs.Any(glob => GlobMatches(item.RelativePath, glob)))
            .Where(item => solutionPaths.Count == 0 || item.Kind != "Solution" || solutionPaths.Contains(item.RelativePath))
            .Where(item => projectPaths.Count == 0 || item.Kind != "Project" || projectPaths.Contains(item.RelativePath))
            .Where(item => projectDirectories.Length == 0
                || item.Kind is "Solution"
                || projectPaths.Contains(item.RelativePath)
                || projectDirectories.Any(directory => IsUnderScopedDirectory(item.RelativePath, directory)))
            .OrderBy(item => item.RelativePath, StringComparer.Ordinal)
            .ToArray();
    }

    private static HashSet<string> NormalizeOptionPaths(string repoPath, IReadOnlyList<string>? paths)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var path in paths ?? [])
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            var normalized = Path.IsPathRooted(path)
                ? Path.GetRelativePath(repoPath, Path.GetFullPath(path))
                : path;
            result.Add(FileInventory.NormalizeRelativePath(normalized));
        }

        return result;
    }

    private static bool IsUnderScopedDirectory(string relativePath, string directory)
    {
        if (directory is "." or "")
        {
            return true;
        }

        var normalizedDirectory = directory.TrimEnd('/') + "/";
        return relativePath.StartsWith(normalizedDirectory, StringComparison.Ordinal);
    }

    private static bool GlobMatches(string relativePath, string glob)
    {
        var normalizedGlob = FileInventory.NormalizeRelativePath(glob.Trim());
        if (string.IsNullOrWhiteSpace(normalizedGlob))
        {
            return false;
        }

        if (!normalizedGlob.Contains('*', StringComparison.Ordinal))
        {
            return relativePath.Equals(normalizedGlob, StringComparison.Ordinal)
                || relativePath.StartsWith(normalizedGlob.TrimEnd('/') + "/", StringComparison.Ordinal);
        }

        var regex = "^" + System.Text.RegularExpressions.Regex.Escape(normalizedGlob)
            .Replace("\\*\\*", ".*", StringComparison.Ordinal)
            .Replace("\\*", "[^/]*", StringComparison.Ordinal) + "$";
        return System.Text.RegularExpressions.Regex.IsMatch(relativePath, regex);
    }
}
