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
        var inventory = FileInventory.Collect(repoPath, outputPath);
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

        var knownGaps = git.KnownGaps
            .Append("Build was not run in Milestone 1; analysis is limited to deterministic file, project, and config inventory.")
            .OrderBy(gap => gap, StringComparer.Ordinal)
            .ToArray();

        var manifest = new ScanManifest(
            CreateScanId(repoPath, git.CommitSha, inventory),
            git.RepoName,
            git.RemoteUrl,
            git.Branch,
            git.CommitSha,
            ScannerVersions.TraceMap,
            DateTimeOffset.UtcNow,
            "Level1FileInventoryOnly",
            "NotRun",
            solutions,
            projects,
            targetFrameworks,
            knownGaps);

        var facts = CreateFacts(manifest, inventory, targetFrameworkInfos, ProjectFileReader.ReadPackageReferences(repoPath, inventory), knownGaps);
        return new ScanResult(manifest, facts, inventory);
    }

    private static string CreateScanId(string repoPath, string commitSha, IReadOnlyList<FileInventoryItem> inventory)
    {
        var signature = string.Join('\n', inventory.Select(item => $"{item.RelativePath}|{item.Kind}|{item.SizeBytes}"));
        return "scan-" + FactFactory.Hash($"{repoPath}|{commitSha}|{signature}", 20);
    }

    private static IReadOnlyList<CodeFact> CreateFacts(
        ScanManifest manifest,
        IReadOnlyList<FileInventoryItem> inventory,
        IReadOnlyList<TargetFrameworkInfo> targetFrameworks,
        IReadOnlyList<PackageReferenceInfo> packageReferences,
        IReadOnlyList<string> knownGaps)
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
                    ["solutionCount"] = manifest.Solutions.Count.ToString()
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
                    ["reason"] = "Build is outside Milestone 1 scope."
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
            facts.Add(FactFactory.Create(
                manifest,
                FactTypes.PackageReferenced,
                RuleIds.ProjectFile,
                EvidenceTiers.Tier2Structural,
                new EvidenceSpan(item.ProjectPath, item.Line, item.Line, null, "ProjectFileExtractor", ScannerVersions.ProjectFileExtractor),
                projectPath: item.ProjectPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ? item.ProjectPath : null,
                targetSymbol: item.PackageName,
                properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["package"] = item.PackageName,
                    ["version"] = item.Version ?? string.Empty
                }));
        }

        return facts
            .OrderBy(fact => fact.FactType, StringComparer.Ordinal)
            .ThenBy(fact => fact.Evidence.FilePath, StringComparer.Ordinal)
            .ThenBy(fact => fact.Evidence.StartLine)
            .ThenBy(fact => fact.TargetSymbol, StringComparer.Ordinal)
            .ThenBy(fact => fact.FactId, StringComparer.Ordinal)
            .ToArray();
    }
}
