using System.Xml;
using System.Xml.Linq;

namespace TraceMap.Core;

public static class BuildEnvironmentDiagnosticExtractor
{
    public const string DiagnosticKindTargetFramework = "target-framework";
    public const string DiagnosticKindToolset = "toolset";
    public const string DiagnosticKindProjectFormat = "project-format";
    public const string DiagnosticKindRestore = "restore";
    public const string DiagnosticKindGeneratedFile = "generated-file";
    public const string DiagnosticKindWorkspace = "workspace";

    private static readonly HashSet<string> WebApplicationProjectGuids = new(StringComparer.OrdinalIgnoreCase)
    {
        "{349c5851-65df-11da-9384-00065b846f21}"
    };

    private static readonly Dictionary<string, string> UnsupportedProjectGuids = new(StringComparer.OrdinalIgnoreCase)
    {
        ["{54435603-dbb4-11d2-8724-00a0c9a8b90c}"] = "setup-deployment",
        ["{c8d11400-126e-41cd-887f-60bd40844f9e}"] = "database-project"
    };

    public static IReadOnlyList<CodeFact> Extract(
        string repoPath,
        ScanManifest manifest,
        IReadOnlyList<FileInventoryItem> inventory,
        SemanticExtractionResult semanticResult)
    {
        var diagnostics = new List<BuildEnvironmentDiagnosticCandidate>();
        foreach (var project in inventory.Where(item => item.Kind is "Project" or "NonCSharpProject"))
        {
            diagnostics.AddRange(ReadProjectDiagnostics(repoPath, project));
        }

        diagnostics.AddRange(ReadRestoreDiagnostics(inventory));
        diagnostics.AddRange(ReadGeneratedFileDiagnostics(repoPath, inventory));
        diagnostics.AddRange(ReadWorkspaceDiagnostics(semanticResult.GapFacts));

        return diagnostics
            .OrderBy(item => item.DiagnosticKind, StringComparer.Ordinal)
            .ThenBy(item => item.DiagnosticCode, StringComparer.Ordinal)
            .ThenBy(item => item.FilePath, StringComparer.Ordinal)
            .ThenBy(item => item.StartLine)
            .Select(item => CreateFact(manifest, item))
            .OrderBy(fact => fact.Properties.GetValueOrDefault("diagnosticKind"), StringComparer.Ordinal)
            .ThenBy(fact => fact.Properties.GetValueOrDefault("diagnosticCode"), StringComparer.Ordinal)
            .ThenBy(fact => fact.Evidence.FilePath, StringComparer.Ordinal)
            .ThenBy(fact => fact.Evidence.StartLine)
            .ThenBy(fact => fact.FactId, StringComparer.Ordinal)
            .ToArray();
    }

    public static SanitizedDiagnostic SanitizeWorkspaceGap(string gapKind, string rawMessage, string? diagnosticId = null)
    {
        var raw = rawMessage ?? string.Empty;
        var code = gapKind switch
        {
            "MSBuildRegistrationFailed" => "MSBuildRegistrationFailed",
            "RestoreFailed" => CategorizeRestoreFailure(raw),
            "CompilationCreateFailed" or "CompilationMissing" => "CompilationCreationFailed",
            "CompilationDiagnostic" when IsReferenceAssemblyDiagnostic(raw, diagnosticId) => "MissingReferenceAssemblies",
            "WorkspaceDiagnostic" or "ProjectLoadFailed" or "SolutionLoadFailed" when IsSdkResolutionFailure(raw) => "SdkResolutionFailed",
            "WorkspaceDiagnostic" or "ProjectLoadFailed" or "SolutionLoadFailed" when IsReferenceAssemblyDiagnostic(raw, diagnosticId) => "MissingReferenceAssemblies",
            "WorkspaceDiagnostic" or "ProjectLoadFailed" or "SolutionLoadFailed" => "UncategorizedWorkspaceFailure",
            _ => "UncategorizedWorkspaceFailure"
        };
        var kind = gapKind == "RestoreFailed" ? DiagnosticKindRestore : DiagnosticKindWorkspace;
        var guidance = GuidanceFor(code);
        return new SanitizedDiagnostic(
            code,
            kind,
            RuleFor(kind),
            TierFor(code, kind),
            guidance,
            CoverageEffectFor(code, kind),
            "category-only",
            MessageFor(code, kind),
            LimitationFor(code, kind));
    }

    public static string HashObservedValue(string propertyKey, string diagnosticCode, string rawValue)
    {
        return FactFactory.Hash($"build-environment|{propertyKey}|{diagnosticCode}|{rawValue}", 32);
    }

    private static IReadOnlyList<BuildEnvironmentDiagnosticCandidate> ReadProjectDiagnostics(string repoPath, FileInventoryItem project)
    {
        var diagnostics = new List<BuildEnvironmentDiagnosticCandidate>();
        var fullPath = Path.Combine(repoPath, project.RelativePath);
        if (!TryLoadXml(fullPath, out var document))
        {
            diagnostics.Add(Candidate(
                "UnknownLegacyProjectFormat",
                DiagnosticKindProjectFormat,
                RuleIds.BuildEnvironmentProjectFormat,
                EvidenceTiers.Tier4Unknown,
                project.RelativePath,
                1,
                project.RelativePath,
                ProjectStyle(project, isSdkStyle: false),
                guidanceCode: GuidanceFor("UnknownLegacyProjectFormat"),
                coverageEffect: "caps-to-structural",
                sanitization: "category-only"));
            return diagnostics;
        }

        var root = document.Root;
        var isSdkStyle = IsSdkStyleProject(root);
        var projectStyle = ProjectStyle(project, isSdkStyle);
        if (!isSdkStyle || project.Kind == "NonCSharpProject")
        {
            diagnostics.Add(Candidate(
                project.Kind == "NonCSharpProject" ? "UnknownLegacyProjectFormat" : "NonSdkStyleProject",
                DiagnosticKindProjectFormat,
                RuleIds.BuildEnvironmentProjectFormat,
                project.Kind == "NonCSharpProject" ? EvidenceTiers.Tier4Unknown : EvidenceTiers.Tier2Structural,
                project.RelativePath,
                1,
                project.RelativePath,
                projectStyle,
                guidanceCode: GuidanceFor(project.Kind == "NonCSharpProject" ? "UnknownLegacyProjectFormat" : "NonSdkStyleProject"),
                coverageEffect: "caps-to-structural",
                sanitization: "none"));
        }

        var toolsVersion = root?.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == "ToolsVersion")?.Value.Trim();
        if (!string.IsNullOrWhiteSpace(toolsVersion))
        {
            diagnostics.Add(Candidate(
                "OldMsBuildToolsVersion",
                DiagnosticKindToolset,
                RuleIds.BuildEnvironmentToolset,
                EvidenceTiers.Tier2Structural,
                project.RelativePath,
                GetLine(root!),
                project.RelativePath,
                projectStyle,
                safeObservedValue: SafeShortValue(toolsVersion),
                toolFamily: "MSBuild",
                guidanceCode: GuidanceFor("OldMsBuildToolsVersion"),
                coverageEffect: "informational",
                sanitization: SafeShortValue(toolsVersion) == toolsVersion ? "none" : "hashed"));
        }

        foreach (var element in Elements(document, "TargetFramework", "TargetFrameworks", "TargetFrameworkVersion"))
        {
            foreach (var target in SplitTargetFrameworks(element.Value))
            {
                var normalized = NormalizeTargetFramework(target, element.Name.LocalName);
                var code = IsLegacyTargetFramework(normalized) || element.Name.LocalName == "TargetFrameworkVersion"
                    ? "LegacyTargetFramework"
                    : "SdkStyleTargetFramework";
                diagnostics.Add(Candidate(
                    code,
                    DiagnosticKindTargetFramework,
                    RuleIds.BuildEnvironmentTargetFramework,
                    EvidenceTiers.Tier2Structural,
                    project.RelativePath,
                    GetLine(element),
                    project.RelativePath,
                    projectStyle,
                    targetFramework: normalized,
                    safeObservedValue: normalized,
                    toolFamily: ToolFamilyForTargetFramework(normalized),
                    guidanceCode: GuidanceFor(code),
                    coverageEffect: "informational",
                    sanitization: "none"));
            }
        }

        foreach (var element in Elements(document, "VisualStudioVersion"))
        {
            var value = element.Value.Trim();
            diagnostics.Add(Candidate(
                "VisualStudioVersionDeclared",
                DiagnosticKindToolset,
                RuleIds.BuildEnvironmentToolset,
                EvidenceTiers.Tier2Structural,
                project.RelativePath,
                GetLine(element),
                project.RelativePath,
                projectStyle,
                safeObservedValue: SafeShortValue(value),
                toolFamily: "Visual Studio",
                guidanceCode: GuidanceFor("VisualStudioVersionDeclared"),
                coverageEffect: "informational",
                sanitization: SafeShortValue(value) == value ? "none" : "hashed"));
        }

        foreach (var import in Elements(document, "Import"))
        {
            if (HasSdkAttribute(import))
            {
                continue;
            }

            var importValue = AttributeValue(import, "Project");
            if (string.IsNullOrWhiteSpace(importValue))
            {
                continue;
            }

            var basename = Path.GetFileName(importValue.Replace('\\', '/'));
            var isKnownLegacy = basename.Contains("WebApplication", StringComparison.OrdinalIgnoreCase)
                || basename.Equals("Microsoft.CSharp.targets", StringComparison.OrdinalIgnoreCase)
                || basename.Equals("Microsoft.Common.props", StringComparison.OrdinalIgnoreCase);
            diagnostics.Add(Candidate(
                isKnownLegacy ? "ImportedLegacyTargets" : "UnknownImportedTargets",
                DiagnosticKindToolset,
                RuleIds.BuildEnvironmentToolset,
                isKnownLegacy ? EvidenceTiers.Tier2Structural : EvidenceTiers.Tier4Unknown,
                project.RelativePath,
                GetLine(import),
                project.RelativePath,
                projectStyle,
                safeObservedValue: IsSafeBasename(basename) ? basename : null,
                observedValueHash: IsSafeBasename(basename) ? null : HashObservedValue("importBasename", "UnknownImportedTargets", basename),
                toolFamily: basename.Contains("WebApplication", StringComparison.OrdinalIgnoreCase) ? "Visual Studio Web targets" : "MSBuild",
                guidanceCode: GuidanceFor(isKnownLegacy ? "ImportedLegacyTargets" : "UnknownImportedTargets"),
                coverageEffect: isKnownLegacy ? "informational" : "caps-to-structural",
                sanitization: IsSafeBasename(basename) ? "none" : "hashed"));
            if (basename.Contains("WebApplication", StringComparison.OrdinalIgnoreCase))
            {
                diagnostics.Add(Candidate(
                    "WebApplicationProjectTargets",
                    DiagnosticKindProjectFormat,
                    RuleIds.BuildEnvironmentProjectFormat,
                    EvidenceTiers.Tier2Structural,
                    project.RelativePath,
                    GetLine(import),
                    project.RelativePath,
                    "legacy-web-application",
                    safeObservedValue: basename,
                    toolFamily: "Visual Studio Web targets",
                    guidanceCode: GuidanceFor("WebApplicationProjectTargets"),
                    coverageEffect: "caps-to-structural",
                    sanitization: "none"));
            }
        }

        foreach (var element in Elements(document, "ProjectTypeGuids"))
        {
            foreach (var guid in element.Value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var normalizedGuid = guid.Trim().ToLowerInvariant();
                if (WebApplicationProjectGuids.Contains(normalizedGuid))
                {
                    diagnostics.Add(Candidate(
                        "WebApplicationProjectTargets",
                        DiagnosticKindProjectFormat,
                        RuleIds.BuildEnvironmentProjectFormat,
                        EvidenceTiers.Tier2Structural,
                        project.RelativePath,
                        GetLine(element),
                        project.RelativePath,
                        "legacy-web-application",
                        safeObservedValue: "web-application-project",
                        toolFamily: "Visual Studio Web targets",
                        guidanceCode: GuidanceFor("WebApplicationProjectTargets"),
                        coverageEffect: "caps-to-structural",
                        sanitization: "none"));
                }
                else if (UnsupportedProjectGuids.TryGetValue(normalizedGuid, out var category))
                {
                    diagnostics.Add(Candidate(
                        "UnsupportedProjectTypeGuid",
                        DiagnosticKindProjectFormat,
                        RuleIds.BuildEnvironmentProjectFormat,
                        EvidenceTiers.Tier2Structural,
                        project.RelativePath,
                        GetLine(element),
                        project.RelativePath,
                        projectStyle,
                        safeObservedValue: category,
                        guidanceCode: GuidanceFor("UnsupportedProjectTypeGuid"),
                        coverageEffect: "caps-to-structural",
                        sanitization: "none"));
                }
                else
                {
                    diagnostics.Add(Candidate(
                        "UnknownLegacyProjectFormat",
                        DiagnosticKindProjectFormat,
                        RuleIds.BuildEnvironmentProjectFormat,
                        EvidenceTiers.Tier4Unknown,
                        project.RelativePath,
                        GetLine(element),
                        project.RelativePath,
                        projectStyle,
                        observedValueHash: HashObservedValue("projectTypeGuid", "UnknownLegacyProjectFormat", normalizedGuid),
                        guidanceCode: GuidanceFor("UnknownLegacyProjectFormat"),
                        coverageEffect: "caps-to-structural",
                        sanitization: "hashed"));
                }
            }
        }

        foreach (var packageReference in Elements(document, "PackageReference"))
        {
            diagnostics.Add(Candidate(
                "PackageReferencePresent",
                DiagnosticKindRestore,
                RuleIds.BuildEnvironmentRestore,
                EvidenceTiers.Tier2Structural,
                project.RelativePath,
                GetLine(packageReference),
                project.RelativePath,
                projectStyle,
                safeObservedValue: "PackageReference",
                toolFamily: "NuGet",
                guidanceCode: GuidanceFor("PackageReferencePresent"),
                coverageEffect: "informational",
                sanitization: "none"));
        }

        return diagnostics;
    }

    private static IReadOnlyList<BuildEnvironmentDiagnosticCandidate> ReadRestoreDiagnostics(IReadOnlyList<FileInventoryItem> inventory)
    {
        var diagnostics = new List<BuildEnvironmentDiagnosticCandidate>();
        foreach (var item in inventory.Where(item => item.Kind is "PackagesConfig" or "PackagesLock" or "NuGetConfig"))
        {
            var code = item.Kind switch
            {
                "PackagesConfig" => "PackagesConfigPresent",
                "PackagesLock" => "PackagesLockPresent",
                _ => "NuGetConfigPresent"
            };
            diagnostics.Add(Candidate(
                code,
                DiagnosticKindRestore,
                RuleIds.BuildEnvironmentRestore,
                EvidenceTiers.Tier2Structural,
                item.RelativePath,
                1,
                null,
                null,
                safeObservedValue: item.Kind switch
                {
                    "PackagesConfig" => "packages.config",
                    "PackagesLock" => "packages.lock.json",
                    _ => "nuget.config"
                },
                toolFamily: "NuGet",
                guidanceCode: GuidanceFor(code),
                coverageEffect: "informational",
                sanitization: "none"));
        }

        return diagnostics;
    }

    private static IReadOnlyList<BuildEnvironmentDiagnosticCandidate> ReadGeneratedFileDiagnostics(string repoPath, IReadOnlyList<FileInventoryItem> inventory)
    {
        var byPath = inventory.Select(item => item.RelativePath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var projectDirectories = inventory
            .Where(item => item.Kind == "Project")
            .Select(item => NormalizeDirectory(Path.GetDirectoryName(item.RelativePath)))
            .ToArray();
        var diagnostics = new List<BuildEnvironmentDiagnosticCandidate>();
        foreach (var item in inventory.Where(item => item.Kind is "WebFormsMarkup" or "ServiceReferenceMetadata" or "Resource" or "Settings"))
        {
            if (item.Kind is "Resource" or "Settings" && IsMalformedXml(Path.Combine(repoPath, item.RelativePath)))
            {
                diagnostics.Add(Candidate(
                    "GeneratedFileMalformed",
                    DiagnosticKindGeneratedFile,
                    RuleIds.BuildEnvironmentGeneratedFiles,
                    EvidenceTiers.Tier4Unknown,
                    item.RelativePath,
                    1,
                    null,
                    null,
                    safeObservedValue: item.Kind,
                    guidanceCode: GuidanceFor("GeneratedFileMalformed"),
                    coverageEffect: "caps-to-structural",
                    sanitization: "category-only"));
                continue;
            }

            var allExpected = ExpectedGeneratedFiles(item).ToArray();
            var expected = allExpected.Where(path => !byPath.Contains(path)).ToArray();
            foreach (var missing in expected)
            {
                diagnostics.Add(Candidate(
                    "GeneratedFileMissing",
                    DiagnosticKindGeneratedFile,
                    RuleIds.BuildEnvironmentGeneratedFiles,
                    EvidenceTiers.Tier4Unknown,
                    item.RelativePath,
                    1,
                    null,
                    null,
                    safeObservedValue: Path.GetFileName(missing),
                    guidanceCode: GuidanceFor("GeneratedFileMissing"),
                    coverageEffect: "caps-to-syntax",
                    sanitization: "none"));
            }

            if (expected.Length == 0 && allExpected.Length > 0 && !HasNearbyProject(item.RelativePath, projectDirectories))
            {
                diagnostics.Add(Candidate(
                    "GeneratedFileUnlinked",
                    DiagnosticKindGeneratedFile,
                    RuleIds.BuildEnvironmentGeneratedFiles,
                    EvidenceTiers.Tier3SyntaxOrTextual,
                    item.RelativePath,
                    1,
                    null,
                    null,
                    safeObservedValue: item.Kind,
                    guidanceCode: GuidanceFor("GeneratedFileUnlinked"),
                    coverageEffect: "caps-to-structural",
                    sanitization: "none"));
            }
        }

        return diagnostics;
    }

    private static bool HasNearbyProject(string relativePath, IReadOnlyList<string> projectDirectories)
    {
        var directory = NormalizeDirectory(Path.GetDirectoryName(relativePath));
        return projectDirectories.Any(projectDirectory =>
            projectDirectory == "."
            || IsSameOrChildDirectory(directory, projectDirectory)
            || (directory != "." && IsSameOrChildDirectory(projectDirectory, directory)));
    }

    private static bool IsSameOrChildDirectory(string child, string parent)
    {
        return child.Equals(parent, StringComparison.OrdinalIgnoreCase)
            || child.StartsWith(parent.TrimEnd('/') + "/", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeDirectory(string? directory)
    {
        return string.IsNullOrWhiteSpace(directory)
            ? "."
            : FileInventory.NormalizeRelativePath(directory);
    }

    private static IEnumerable<string> ExpectedGeneratedFiles(FileInventoryItem item)
    {
        var directory = Path.GetDirectoryName(item.RelativePath)?.Replace('\\', '/') ?? string.Empty;
        var fileName = Path.GetFileName(item.RelativePath);
        var extension = Path.GetExtension(fileName);
        var baseName = fileName[..^extension.Length];
        var prefix = string.IsNullOrWhiteSpace(directory) ? string.Empty : directory + "/";
        if (item.Kind == "WebFormsMarkup")
        {
            yield return $"{prefix}{fileName}.cs";
            yield return $"{prefix}{fileName}.designer.cs";
        }
        else if (item.Kind == "ServiceReferenceMetadata" && extension.Equals(".svcmap", StringComparison.OrdinalIgnoreCase))
        {
            yield return $"{prefix}Reference.cs";
        }
        else if (item.Kind == "Resource")
        {
            yield return $"{prefix}{baseName}.Designer.cs";
        }
        else if (item.Kind == "Settings")
        {
            yield return $"{prefix}{baseName}.Designer.cs";
        }
    }

    private static IReadOnlyList<BuildEnvironmentDiagnosticCandidate> ReadWorkspaceDiagnostics(IReadOnlyList<SemanticFactCandidate> gaps)
    {
        return gaps
            .Select(gap =>
            {
                var code = gap.Properties?.GetValueOrDefault("diagnosticCode") ?? "UncategorizedWorkspaceFailure";
                var kind = gap.Properties?.GetValueOrDefault("diagnosticKind") ?? DiagnosticKindWorkspace;
                return Candidate(
                    code,
                    kind,
                    RuleFor(kind),
                    TierFor(code, kind),
                    gap.Evidence.FilePath,
                    gap.Evidence.StartLine,
                    gap.ProjectPath,
                    null,
                    toolFamily: kind == DiagnosticKindRestore ? "NuGet" : "MSBuild/Roslyn",
                    guidanceCode: GuidanceFor(code),
                    coverageEffect: CoverageEffectFor(code, kind),
                    sanitization: gap.Properties?.GetValueOrDefault("sanitization") ?? "category-only");
            })
            .ToArray();
    }

    private static CodeFact CreateFact(ScanManifest manifest, BuildEnvironmentDiagnosticCandidate item)
    {
        var properties = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["coverageEffect"] = item.CoverageEffect,
            ["diagnosticCode"] = item.DiagnosticCode,
            ["diagnosticKind"] = item.DiagnosticKind,
            ["guidanceCode"] = item.GuidanceCode,
            ["guidance"] = GuidanceText(item.GuidanceCode),
            ["limitation"] = item.Limitation,
            ["sanitization"] = item.Sanitization
        };
        AddIfPresent(properties, "projectStyle", item.ProjectStyle);
        AddIfPresent(properties, "safeObservedValue", item.SafeObservedValue);
        AddIfPresent(properties, "observedValueHash", item.ObservedValueHash);
        AddIfPresent(properties, "targetFramework", item.TargetFramework);
        AddIfPresent(properties, "toolFamily", item.ToolFamily);

        return FactFactory.Create(
            manifest,
            FactTypes.BuildEnvironmentDiagnostic,
            item.RuleId,
            item.EvidenceTier,
            new EvidenceSpan(
                item.FilePath,
                item.StartLine,
                item.EndLine,
                null,
                "BuildEnvironmentDiagnosticExtractor",
                ScannerVersions.BuildEnvironmentExtractor),
            projectPath: item.ProjectPath,
            targetSymbol: item.TargetFramework,
            contractElement: item.DiagnosticCode,
            properties: properties);
    }

    private static BuildEnvironmentDiagnosticCandidate Candidate(
        string diagnosticCode,
        string diagnosticKind,
        string ruleId,
        string tier,
        string filePath,
        int line,
        string? projectPath,
        string? projectStyle,
        string? targetFramework = null,
        string? toolFamily = null,
        string? safeObservedValue = null,
        string? observedValueHash = null,
        string? guidanceCode = null,
        string coverageEffect = "informational",
        string sanitization = "none")
    {
        return new BuildEnvironmentDiagnosticCandidate(
            diagnosticCode,
            diagnosticKind,
            ruleId,
            tier,
            FileInventory.NormalizeRelativePath(filePath),
            Math.Max(1, line),
            Math.Max(1, line),
            projectPath,
            projectStyle,
            targetFramework,
            toolFamily,
            safeObservedValue,
            observedValueHash,
            guidanceCode ?? GuidanceFor(diagnosticCode),
            coverageEffect,
            sanitization,
            LimitationFor(diagnosticCode, diagnosticKind));
    }

    private static IEnumerable<XElement> Elements(XDocument document, params string[] names)
    {
        return document.Descendants().Where(element => names.Contains(element.Name.LocalName, StringComparer.Ordinal));
    }

    private static IEnumerable<string> SplitTargetFrameworks(string value)
    {
        return value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(item => item.Length > 0);
    }

    private static string NormalizeTargetFramework(string value, string elementName)
    {
        var trimmed = value.Trim();
        if (elementName == "TargetFrameworkVersion" && trimmed.StartsWith("v", StringComparison.OrdinalIgnoreCase))
        {
            return $".NETFramework,Version={trimmed}";
        }

        return trimmed;
    }

    private static bool IsLegacyTargetFramework(string value)
    {
        return value.StartsWith(".NETFramework", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("v", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("net4", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("net3", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("net2", StringComparison.OrdinalIgnoreCase);
    }

    private static string ToolFamilyForTargetFramework(string value)
    {
        return IsLegacyTargetFramework(value) ? ".NET Framework" : ".NET SDK";
    }

    private static string ProjectStyle(FileInventoryItem project, bool isSdkStyle)
    {
        if (project.Kind == "NonCSharpProject")
        {
            return "unknown";
        }

        return isSdkStyle ? "sdk-style" : "non-sdk-style";
    }

    private static bool IsSdkStyleProject(XElement? root)
    {
        return root?.Attributes().Any(attribute => attribute.Name.LocalName == "Sdk" && !string.IsNullOrWhiteSpace(attribute.Value)) == true
            || root?.Descendants().Any(element =>
                element.Name.LocalName == "Import"
                && HasSdkAttribute(element)) == true;
    }

    private static bool HasSdkAttribute(XElement element)
    {
        return element.Attributes().Any(attribute => attribute.Name.LocalName == "Sdk" && !string.IsNullOrWhiteSpace(attribute.Value));
    }

    private static string? SafeShortValue(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length is > 0 and <= 80 && trimmed.All(ch => char.IsLetterOrDigit(ch) || ch is '.' or '-' or '_' or ' '))
        {
            return trimmed;
        }

        return null;
    }

    private static bool IsSafeBasename(string value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && value.Length <= 100
            && value.All(ch => char.IsLetterOrDigit(ch) || ch is '.' or '-' or '_');
    }

    private static bool TryLoadXml(string fullPath, out XDocument document)
    {
        try
        {
            document = XDocument.Load(fullPath, LoadOptions.SetLineInfo);
            return true;
        }
        catch
        {
            document = new XDocument();
            return false;
        }
    }

    private static bool IsMalformedXml(string fullPath)
    {
        return !TryLoadXml(fullPath, out _);
    }

    private static string? AttributeValue(XElement element, string name)
    {
        return element.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == name)?.Value.Trim();
    }

    private static int GetLine(XObject node)
    {
        return node is IXmlLineInfo lineInfo && lineInfo.HasLineInfo()
            ? Math.Max(1, lineInfo.LineNumber)
            : 1;
    }

    private static string CategorizeRestoreFailure(string raw)
    {
        if (raw.Contains("credential", StringComparison.OrdinalIgnoreCase)
            || raw.Contains("unauthorized", StringComparison.OrdinalIgnoreCase)
            || raw.Contains("authentication", StringComparison.OrdinalIgnoreCase)
            || raw.Contains("401", StringComparison.Ordinal)
            || raw.Contains("403", StringComparison.Ordinal))
        {
            return "CredentialRequired";
        }

        if (raw.Contains("NU1301", StringComparison.OrdinalIgnoreCase)
            || raw.Contains("service index", StringComparison.OrdinalIgnoreCase)
            || raw.Contains("source", StringComparison.OrdinalIgnoreCase)
            || raw.Contains("timed out", StringComparison.OrdinalIgnoreCase)
            || raw.Contains("connection", StringComparison.OrdinalIgnoreCase))
        {
            return "PackageSourceUnavailable";
        }

        if (raw.Contains("NU1101", StringComparison.OrdinalIgnoreCase)
            || raw.Contains("Unable to find package", StringComparison.OrdinalIgnoreCase)
            || raw.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return "PackageVersionUnavailable";
        }

        if (raw.Contains("unsupported", StringComparison.OrdinalIgnoreCase)
            || raw.Contains("invalid package", StringComparison.OrdinalIgnoreCase)
            || raw.Contains("packages.config", StringComparison.OrdinalIgnoreCase))
        {
            return "UnsupportedPackageFormat";
        }

        return "NuGetRestoreFailed";
    }

    private static bool IsSdkResolutionFailure(string raw)
    {
        return raw.Contains("SDK", StringComparison.OrdinalIgnoreCase)
            || raw.Contains("NETSDK", StringComparison.OrdinalIgnoreCase)
            || raw.Contains("MSB4236", StringComparison.OrdinalIgnoreCase)
            || raw.Contains("resolver", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsReferenceAssemblyDiagnostic(string raw, string? diagnosticId)
    {
        return string.Equals(diagnosticId, "CS0012", StringComparison.OrdinalIgnoreCase)
            || raw.Contains("reference assemblies", StringComparison.OrdinalIgnoreCase)
            || raw.Contains("reference assembly", StringComparison.OrdinalIgnoreCase)
            || raw.Contains("framework assembly", StringComparison.OrdinalIgnoreCase);
    }

    private static string RuleFor(string diagnosticKind)
    {
        return diagnosticKind switch
        {
            DiagnosticKindTargetFramework => RuleIds.BuildEnvironmentTargetFramework,
            DiagnosticKindToolset => RuleIds.BuildEnvironmentToolset,
            DiagnosticKindProjectFormat => RuleIds.BuildEnvironmentProjectFormat,
            DiagnosticKindRestore => RuleIds.BuildEnvironmentRestore,
            DiagnosticKindGeneratedFile => RuleIds.BuildEnvironmentGeneratedFiles,
            _ => RuleIds.BuildEnvironmentWorkspaceDiagnostic
        };
    }

    private static string TierFor(string diagnosticCode, string diagnosticKind)
    {
        if (diagnosticKind == DiagnosticKindRestore && diagnosticCode is "NuGetRestoreFailed" or "PackageSourceUnavailable" or "CredentialRequired" or "PackageVersionUnavailable" or "UnsupportedPackageFormat")
        {
            return EvidenceTiers.Tier3SyntaxOrTextual;
        }

        if (diagnosticKind is DiagnosticKindWorkspace or DiagnosticKindGeneratedFile || diagnosticCode.StartsWith("Unknown", StringComparison.Ordinal))
        {
            return EvidenceTiers.Tier4Unknown;
        }

        return EvidenceTiers.Tier2Structural;
    }

    private static string GuidanceFor(string diagnosticCode)
    {
        return diagnosticCode switch
        {
            "LegacyTargetFramework" or "MissingReferenceAssemblies" => "UseCompatibleReferenceAssemblies",
            "OldMsBuildToolsVersion" or "VisualStudioVersionDeclared" => "UseCompatibleMSBuildToolset",
            "ImportedLegacyTargets" or "UnknownImportedTargets" => "ReviewImportedTargets",
            "WebApplicationProjectTargets" => "UseCompatibleWebApplicationTargets",
            "PackagesConfigPresent" or "PackageReferencePresent" or "PackagesLockPresent" or "NuGetConfigPresent" => "ReviewNuGetRestoreInputs",
            "NuGetRestoreFailed" or "PackageSourceUnavailable" or "CredentialRequired" or "PackageVersionUnavailable" or "UnsupportedPackageFormat" => "ReviewSanitizedRestoreFailure",
            "GeneratedFileMissing" or "GeneratedFileMalformed" or "GeneratedFileUnlinked" => "ReviewGeneratedFileCoverage",
            "SdkResolutionFailed" => "UseCompatibleDotNetSdk",
            "MSBuildRegistrationFailed" or "CompilationCreationFailed" => "UseCompatibleMSBuildToolset",
            _ => "ReviewEnvironmentGap"
        };
    }

    private static string CoverageEffectFor(string diagnosticCode, string diagnosticKind)
    {
        if (diagnosticKind == DiagnosticKindWorkspace)
        {
            return "reduces-semantic-coverage";
        }

        if (diagnosticKind == DiagnosticKindRestore && diagnosticCode is "NuGetRestoreFailed" or "PackageSourceUnavailable" or "CredentialRequired" or "PackageVersionUnavailable" or "UnsupportedPackageFormat")
        {
            return "reduces-semantic-coverage";
        }

        if (diagnosticKind == DiagnosticKindGeneratedFile)
        {
            return "caps-to-syntax";
        }

        return "informational";
    }

    private static string MessageFor(string diagnosticCode, string diagnosticKind)
    {
        return diagnosticKind == DiagnosticKindRestore
            ? $"Restore diagnostic category: {diagnosticCode}. Native output was redacted."
            : $"Workspace diagnostic category: {diagnosticCode}. Native output was redacted.";
    }

    private static string GuidanceText(string guidanceCode)
    {
        return guidanceCode switch
        {
            "UseCompatibleReferenceAssemblies" => "Compatible reference assemblies appear necessary for semantic analysis.",
            "UseCompatibleMSBuildToolset" => "A compatible MSBuild toolset appears necessary for this project style.",
            "UseCompatibleWebApplicationTargets" => "Visual Studio Web Application targets appear necessary for full project load.",
            "ReviewImportedTargets" => "Imported targets could affect build behavior; TraceMap did not evaluate them.",
            "ReviewNuGetRestoreInputs" => "NuGet restore inputs are present; package resolution is not assumed unless restore is explicitly requested.",
            "ReviewSanitizedRestoreFailure" => "Explicit restore failed with a sanitized category; review package resolution in a compatible environment.",
            "ReviewGeneratedFileCoverage" => "Generated or designer-file evidence may cap semantic coverage for related legacy patterns.",
            "UseCompatibleDotNetSdk" => "A compatible .NET SDK appears necessary for full project load.",
            _ => "Review the environment diagnostic category and supporting evidence."
        };
    }

    private static string LimitationFor(string diagnosticCode, string diagnosticKind)
    {
        return diagnosticKind switch
        {
            DiagnosticKindTargetFramework => "Static framework declarations do not prove runtime installation, support status, security posture, or sufficient tooling.",
            DiagnosticKindToolset => "MSBuild and import indicators are static clues; TraceMap does not evaluate arbitrary imported build logic.",
            DiagnosticKindProjectFormat => "Project-format diagnostics are static evidence and do not prove runtime deployment behavior.",
            DiagnosticKindRestore => "Restore diagnostics do not prove package absence, vulnerability, runtime loading, or deployment behavior; unsafe values are omitted or hashed.",
            DiagnosticKindGeneratedFile => "Generated-file diagnostics are analysis gaps and do not prove generated code absence at runtime.",
            _ => "Workspace diagnostics are sanitized categories; raw native output is not stored in shareable artifacts."
        };
    }

    private static void AddIfPresent(IDictionary<string, string> properties, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            properties[key] = value;
        }
    }

    public sealed record SanitizedDiagnostic(
        string DiagnosticCode,
        string DiagnosticKind,
        string RuleId,
        string EvidenceTier,
        string GuidanceCode,
        string CoverageEffect,
        string Sanitization,
        string Message,
        string Limitation);

    private sealed record BuildEnvironmentDiagnosticCandidate(
        string DiagnosticCode,
        string DiagnosticKind,
        string RuleId,
        string EvidenceTier,
        string FilePath,
        int StartLine,
        int EndLine,
        string? ProjectPath,
        string? ProjectStyle,
        string? TargetFramework,
        string? ToolFamily,
        string? SafeObservedValue,
        string? ObservedValueHash,
        string GuidanceCode,
        string CoverageEffect,
        string Sanitization,
        string Limitation);
}
