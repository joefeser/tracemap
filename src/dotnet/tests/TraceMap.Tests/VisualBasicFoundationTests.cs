using TraceMap.Core;

namespace TraceMap.Tests;

// Foundation-slice coverage for issue #736: deterministic VB inventory and
// classification, Roslyn Visual Basic project loading, honest reduced
// coverage when loading fails, and no regression to C# scanning. Per-file VB
// semantic facts are a later slice; these tests claim only what the
// foundation emits today.
public sealed class VisualBasicFoundationTests
{
    [Fact]
    public void Modern_vb_fixture_inventories_project_and_sources()
    {
        var repoRoot = FindRepoRoot();
        var inventory = FileInventory.Collect(Path.Combine(repoRoot, "samples", "vb-modern-sample"));

        var project = Assert.Single(inventory, item => item.RelativePath == "VbModernSample.vbproj");
        Assert.Equal("VisualBasicProject", project.Kind);

        var sources = inventory
            .Where(item => item.RelativePath.EndsWith(".vb", StringComparison.Ordinal))
            .OrderBy(item => item.RelativePath, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(
            ["CallSites.vb", "Contracts.vb", "Domain.vb", "Services.vb"],
            sources.Select(item => item.RelativePath).ToArray());
        Assert.All(sources, item => Assert.Equal("VisualBasic", item.Kind));
    }

    [Fact]
    public void Vb_fixture_files_classify_generated_designer_and_assembly_info_shapes()
    {
        var repoRoot = FindRepoRoot();
        var inventory = FileInventory.Collect(Path.Combine(repoRoot, "samples"));

        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // Legacy fixture: generated/designer pair, assembly info, ordinary sources.
            ["vb-legacy-sample/My Project/Application.Designer.vb"] = "VisualBasicDesigner",
            ["vb-legacy-sample/My Project/Resources.Designer.vb"] = "VisualBasicDesigner",
            ["vb-legacy-sample/My Project/AssemblyInfo.vb"] = "VisualBasicAssemblyInfo",
            ["vb-legacy-sample/CatalogModels.vb"] = "VisualBasic",
            ["vb-legacy-sample/CatalogService.vb"] = "VisualBasic",
            ["vb-legacy-sample/LegacyQueueBridge.vb"] = "VisualBasic",
            ["vb-legacy-sample/VbLegacyCatalog.vbproj"] = "VisualBasicProject",
            // Web Forms fixture: code-behind plus generated designer shapes.
            ["vb-webforms-sample/Default.aspx.vb"] = "VisualBasicCodeBehind",
            ["vb-webforms-sample/Default.aspx.designer.vb"] = "VisualBasicDesigner",
            ["vb-webforms-sample/StatusNotifier.vb"] = "VisualBasic",
            ["vb-webforms-sample/VbWebFormsSample.vbproj"] = "VisualBasicProject"
        };
        var itemsByPath = inventory.ToDictionary(item => item.RelativePath, StringComparer.Ordinal);
        foreach (var (relativePath, expectedKind) in expected)
        {
            Assert.True(itemsByPath.TryGetValue(relativePath, out var item), $"Missing inventory item: {relativePath}");
            Assert.Equal(expectedKind, item.Kind);
        }
    }

    [Fact]
    public void Modern_vb_fixture_loads_semantically_with_clean_manifest()
    {
        var repoRoot = FindRepoRoot();
        var result = ScanEngine.Scan(new ScanOptions(
            Path.Combine(repoRoot, "samples", "vb-modern-sample"),
            Path.Combine(Path.GetTempPath(), "tracemap-vb-modern-out")));

        Assert.Equal("Level1SemanticAnalysis", result.Manifest.AnalysisLevel);
        Assert.Equal("Succeeded", result.Manifest.BuildStatus);
        Assert.Contains("VbModernSample.vbproj", result.Manifest.Projects);

        var observation = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.VisualBasicProjectObserved);
        Assert.Equal(RuleIds.VisualBasicSemanticCompilation, observation.RuleId);
        Assert.Equal(EvidenceTiers.Tier2Structural, observation.EvidenceTier);
        Assert.Equal("VbModernSample.vbproj", observation.Evidence.FilePath);
        Assert.Equal("VbModernSample.vbproj", observation.ProjectPath);
        Assert.Equal("Visual Basic", observation.Properties["language"]);
        Assert.True(int.TryParse(observation.Properties["documentCount"], out var documentCount) && documentCount >= 4,
            $"Unexpected document count: {observation.Properties["documentCount"]}");

        // Whatever compiler errors remain on this buildable fixture are the
        // SDK's injected My-template noise, never source-attributed errors.
        Assert.Equal(
            observation.Properties["errorDiagnosticCount"],
            observation.Properties["injectedTemplateErrorCount"]);
        Assert.DoesNotContain(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.VisualBasicSemanticWorkspace);
        Assert.Equal([], result.Manifest.KnownGaps);
    }

    [Fact]
    public void Legacy_vb_fixture_reports_reduced_coverage_with_explicit_gaps()
    {
        var repoRoot = FindRepoRoot();
        var result = ScanEngine.Scan(new ScanOptions(
            Path.Combine(repoRoot, "samples", "vb-legacy-sample"),
            Path.Combine(Path.GetTempPath(), "tracemap-vb-legacy-out")));

        // The legacy fixture references a deliberately missing vendor assembly,
        // so the scan must never be reported as clean.
        Assert.Equal("FailedOrPartial", result.Manifest.BuildStatus);
        Assert.EndsWith("Reduced", result.Manifest.AnalysisLevel, StringComparison.Ordinal);
        Assert.Contains("VbLegacyCatalog.vbproj", result.Manifest.Projects);
        Assert.NotEmpty(result.Manifest.KnownGaps);
        Assert.Contains(result.Manifest.KnownGaps, gap =>
            gap.Contains("Compiler diagnostic", StringComparison.Ordinal)
            || gap.Contains("Workspace diagnostic", StringComparison.Ordinal)
            || gap.Contains("Visual Basic semantic coverage reduced", StringComparison.Ordinal));

        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.VisualBasicSemanticWorkspace
            && fact.EvidenceTier == EvidenceTiers.Tier4Unknown);
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.VisualBasicProjectObserved
            && fact.RuleId == RuleIds.VisualBasicSemanticCompilation);
    }

    [Fact]
    public void Modern_vb_scan_is_deterministic_across_repeated_runs()
    {
        var repoRoot = FindRepoRoot();
        var repoPath = Path.Combine(repoRoot, "samples", "vb-modern-sample");
        var first = ScanEngine.Scan(new ScanOptions(repoPath, Path.Combine(Path.GetTempPath(), "tracemap-vb-det-1")));
        var second = ScanEngine.Scan(new ScanOptions(repoPath, Path.Combine(Path.GetTempPath(), "tracemap-vb-det-2")));

        var firstFacts = first.Facts.Select(SerializeFact).ToArray();
        var secondFacts = second.Facts.Select(SerializeFact).ToArray();
        Assert.Equal(firstFacts, secondFacts);
        Assert.Equal(first.Manifest.SourceSnapshotDigest, second.Manifest.SourceSnapshotDigest);
    }

    [Fact]
    public void Csharp_scanning_is_unchanged_for_a_csharp_only_repository()
    {
        var repoRoot = FindRepoRoot();
        var result = ScanEngine.Scan(new ScanOptions(
            Path.Combine(repoRoot, "samples", "modern-sample"),
            Path.Combine(Path.GetTempPath(), "tracemap-vb-cs-regression")));

        Assert.Equal("Level1SemanticAnalysis", result.Manifest.AnalysisLevel);
        Assert.Equal("Succeeded", result.Manifest.BuildStatus);
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.TypeDeclared
            && fact.RuleId == RuleIds.CSharpSemanticDeclarations);
        Assert.DoesNotContain(result.Facts, fact => fact.RuleId.StartsWith("vb.semantic.", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Facts, fact => fact.FactType == FactTypes.VisualBasicProjectObserved);
    }

    [Fact]
    public void Mixed_csharp_and_visual_basic_repository_scans_both_languages()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Mixed.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <Nullable>enable</Nullable>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(repo, "Alpha.cs"), "namespace Mixed; public sealed class Alpha { }");
        File.WriteAllText(Path.Combine(repo, "Mixed.vbproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <RootNamespace>MixedVb</RootNamespace>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(repo, "Bravo.vb"), """
            Namespace MixedVb
                Public Class Bravo
                End Class
            End Namespace
            """);
        Commit(repo, "mixed languages");

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));

        Assert.Equal("Succeeded", result.Manifest.BuildStatus);
        Assert.Equal("Level1SemanticAnalysis", result.Manifest.AnalysisLevel);
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.TypeDeclared
            && fact.RuleId == RuleIds.CSharpSemanticDeclarations
            && fact.TargetSymbol?.Contains("Alpha", StringComparison.Ordinal) == true);
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.VisualBasicProjectObserved
            && fact.RuleId == RuleIds.VisualBasicSemanticCompilation
            && fact.ProjectPath == "Mixed.vbproj");
        Assert.DoesNotContain(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.VisualBasicSemanticWorkspace);
    }

    [Fact]
    public void Semantic_input_guard_protects_vb_sources_and_projects()
    {
        using var temp = new TempDirectory();
        const string sourceRelativePath = "Sample.vb";
        const string projectRelativePath = "Sample.vbproj";
        var sourcePath = Path.Combine(temp.Path, sourceRelativePath);
        var projectPath = Path.Combine(temp.Path, projectRelativePath);
        File.WriteAllText(sourcePath, "Module M\nEnd Module");
        File.WriteAllText(projectPath, "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        var inventory = FileInventory.Collect(temp.Path);
        var baseline = ScanEngine.CaptureSemanticInputSnapshot(temp.Path, inventory);

        // Both the Visual Basic source and the project file are protected
        // semantic inputs.
        Assert.Contains(sourceRelativePath, baseline.Keys);
        Assert.Contains(projectRelativePath, baseline.Keys);

        // Same-size source mutation must fail verification loudly.
        var semanticResult = new SemanticExtractionResult(
            [],
            [],
            true,
            false,
            new HashSet<string>(StringComparer.Ordinal) { sourceRelativePath },
            CompilationInputFiles: new HashSet<string>(StringComparer.Ordinal) { sourceRelativePath });
        File.WriteAllText(sourcePath, "Module N\nEnd Module");

        Assert.Throws<SourceSnapshotException>(() =>
            ScanEngine.VerifySemanticInputSnapshot(temp.Path, inventory, semanticResult, baseline));
    }

    [Fact]
    public void Vb_sources_without_a_project_emit_a_reduced_coverage_gap()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "Orphan.vb"), "Module M\nEnd Module");
        var inventory = FileInventory.Collect(temp.Path);

        var result = VisualBasicSemanticExtractor.Extract(
            temp.Path,
            inventory,
            new ScanOptions(temp.Path, Path.Combine(temp.Path, "out")));

        Assert.False(result.Attempted);
        Assert.True(result.ReducedCoverage);
        var gap = Assert.Single(result.GapFacts);
        Assert.Equal(FactTypes.AnalysisGap, gap.FactType);
        Assert.Equal(RuleIds.VisualBasicSemanticWorkspace, gap.RuleId);
        Assert.Equal(EvidenceTiers.Tier4Unknown, gap.EvidenceTier);
        Assert.Equal("NoVisualBasicProjectOrSolution", gap.Properties!["gapKind"]);
    }

    [Fact]
    public async Task Cli_scan_of_vb_modern_fixture_writes_required_artifacts()
    {
        var repoRoot = FindRepoRoot();
        using var temp = new TempDirectory();
        var outputPath = Path.Combine(temp.Path, "out");
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await TraceMap.Cli.TraceMapCommand.RunAsync(
            ["scan", "--repo", Path.Combine(repoRoot, "samples", "vb-modern-sample"), "--out", outputPath],
            output,
            error);

        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(Path.Combine(outputPath, "scan-manifest.json")));
        Assert.True(File.Exists(Path.Combine(outputPath, "facts.ndjson")));
        Assert.True(File.Exists(Path.Combine(outputPath, "index.sqlite")));
        Assert.True(File.Exists(Path.Combine(outputPath, "report.md")));
        Assert.True(File.Exists(Path.Combine(outputPath, "logs", "analyzer.log")));

        // Manifest coverage honesty: the VB-only fixture is fully semantic and clean.
        var manifest = await File.ReadAllTextAsync(Path.Combine(outputPath, "scan-manifest.json"));
        Assert.Contains("\"analysisLevel\": \"Level1SemanticAnalysis\"", manifest);
        Assert.Contains("\"buildStatus\": \"Succeeded\"", manifest);
        Assert.Contains("VbModernSample.vbproj", manifest);
    }

    private static string SerializeFact(CodeFact fact) =>
        string.Join('|',
            fact.FactId,
            fact.FactType,
            fact.RuleId,
            fact.EvidenceTier,
            fact.ProjectPath ?? string.Empty,
            fact.SourceSymbol ?? string.Empty,
            fact.TargetSymbol ?? string.Empty,
            fact.ContractElement ?? string.Empty,
            fact.Evidence.FilePath,
            fact.Evidence.StartLine,
            fact.Evidence.EndLine,
            fact.Evidence.ExtractorId,
            fact.Evidence.ExtractorVersion,
            string.Join(";", fact.Properties.Select(pair => $"{pair.Key}={pair.Value}")));

    private static void Commit(string repo, string message)
    {
        RunGit(repo, "init");
        RunGit(repo, "add", "-A");
        RunGit(repo, "-c", "user.name=TraceMap", "-c", "user.email=tests@example.invalid", "commit", "-m", message, "--allow-empty");
    }

    private static void RunGit(string repo, params string[] arguments)
    {
        using var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "git",
                WorkingDirectory = repo,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };
        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.Start();
        process.WaitForExit(30_000);
        Assert.Equal(0, process.ExitCode);
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if ((Directory.Exists(Path.Combine(directory.FullName, ".git")) || File.Exists(Path.Combine(directory.FullName, ".git")))
                && Directory.Exists(Path.Combine(directory.FullName, "samples")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
