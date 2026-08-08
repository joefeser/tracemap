using System.Diagnostics;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class CSharpFullSnapshotStabilityTests
{
    [Fact]
    public void Full_snapshot_sequence_preserves_unchanged_relationships_and_attributes_while_graph_shrinks()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        WriteFixture(repo);
        var baseline = Scan(repo, Path.Combine(temp.Path, "baseline"));
        var baselineCall = SemanticCall(baseline, "src/TransitionSample/Caller.cs");
        var baselineTarget = TargetType(baseline);

        File.AppendAllText(Path.Combine(repo, "src", "TransitionSample", "Caller.cs"), "\n// unrelated caller edit\n");
        Commit(repo, "edit caller");
        var edited = Scan(repo, Path.Combine(temp.Path, "edited"));
        var editedCall = SemanticCall(edited, "src/TransitionSample/Caller.cs");
        var editedTarget = TargetType(edited);

        AssertStableRelationship(baselineCall, editedCall);
        AssertStableUnchangedDeclaration(baselineTarget, editedTarget);

        var oldCaller = Path.Combine(repo, "src", "TransitionSample", "Caller.cs");
        var movedCaller = Path.Combine(repo, "src", "TransitionSample", "MovedCaller.cs");
        File.Move(oldCaller, movedCaller);
        Commit(repo, "move caller");
        var moved = Scan(repo, Path.Combine(temp.Path, "moved"));
        var movedCall = SemanticCall(moved, "src/TransitionSample/MovedCaller.cs");

        AssertStableRelationship(editedCall, movedCall);
        Assert.DoesNotContain(moved.Facts, fact => fact.Evidence.FilePath == "src/TransitionSample/Caller.cs");

        File.Delete(Path.Combine(repo, "src", "TransitionSample", "Target.cs"));
        Commit(repo, "delete target");
        var deleted = Scan(repo, Path.Combine(temp.Path, "deleted"));

        Assert.Equal("FailedOrPartial", deleted.Manifest.BuildStatus);
        Assert.DoesNotContain(deleted.Facts, fact =>
            fact.FactType == FactTypes.CallEdge
            && fact.RuleId == RuleIds.CSharpSemanticCallGraph
            && fact.Properties.GetValueOrDefault("targetSymbolId") == baselineCall.Properties["targetSymbolId"]);
        Assert.Contains(deleted.Facts, fact =>
            fact.FactType == FactTypes.CallEdge
            && fact.RuleId == RuleIds.CSharpSyntaxCallGraph
            && fact.TargetSymbol == "Execute"
            && fact.Properties.GetValueOrDefault("calleeName") == "Execute"
            && fact.Evidence.FilePath == "src/TransitionSample/MovedCaller.cs");
        AssertCompilationDiagnosticGap(
            deleted,
            "src/TransitionSample/MovedCaller.cs",
            7,
            "CS0246");
    }

    [Fact]
    public void Excluded_cross_project_target_cannot_influence_dependent_compilation()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        WriteCrossProjectFixture(repo);
        var baseline = Scan(repo, Path.Combine(temp.Path, "baseline"));
        var baselineCall = Assert.Single(baseline.Facts, fact =>
            fact.FactType == FactTypes.CallEdge
            && fact.RuleId == RuleIds.CSharpSemanticCallGraph
            && fact.Evidence.FilePath == "src/Caller/Caller.cs"
            && fact.ContractElement == "Execute");

        var scoped = Scan(
            repo,
            Path.Combine(temp.Path, "scoped"),
            ["src/Target/Target.cs"]);

        Assert.Equal("FailedOrPartial", scoped.Manifest.BuildStatus);
        Assert.DoesNotContain(scoped.Inventory, item => item.RelativePath == "src/Target/Target.cs");
        Assert.DoesNotContain(scoped.Facts, fact =>
            fact.RuleId == RuleIds.CSharpSemanticCallGraph
            && fact.Properties.GetValueOrDefault("targetSymbolId")
                == baselineCall.Properties["targetSymbolId"]);
        Assert.Contains(scoped.Facts, fact =>
            fact.RuleId == RuleIds.CSharpSyntaxCallGraph
            && fact.TargetSymbol == "Execute"
            && fact.Evidence.FilePath == "src/Caller/Caller.cs");
        AssertCompilationDiagnosticGap(scoped, "src/Caller/Caller.cs", 7, "CS0246");
    }

    [Fact]
    public void Inventoried_path_matching_uses_platform_filesystem_case_semantics()
    {
        var comparer = CSharpSemanticExtractor.SourcePathComparer;

        Assert.Equal(
            OperatingSystem.IsWindows(),
            comparer.Equals("src/CaseSample/Caller.cs", "src/casesample/caller.cs"));
    }

    private static void AssertCompilationDiagnosticGap(
        ScanResult result,
        string filePath,
        int line,
        string diagnosticId)
    {
        var gap = Assert.Single(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.CSharpSemanticWorkspace
            && fact.EvidenceTier == EvidenceTiers.Tier4Unknown
            && fact.CommitSha == result.Manifest.CommitSha
            && fact.Evidence.FilePath == filePath
            && fact.Evidence.StartLine == line
            && fact.Evidence.EndLine == line
            && fact.Evidence.ExtractorId == "CSharpSemanticExtractor"
            && fact.Evidence.ExtractorVersion == ScannerVersions.CSharpSemanticExtractor
            && fact.Properties.GetValueOrDefault("gapKind") == "CompilationDiagnostic"
            && fact.Properties.GetValueOrDefault("diagnosticId") == diagnosticId);
        Assert.Equal("workspace", gap.Properties["diagnosticKind"]);
        Assert.Equal("reduces-semantic-coverage", gap.Properties["coverageEffect"]);
    }

    [Fact]
    public void Scoped_rescan_records_exclusion_and_preserves_unrelated_sql_evidence()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        WriteFixture(repo);
        var baseline = Scan(repo, Path.Combine(temp.Path, "baseline"));
        var baselineSql = SqlEvidenceSignature(baseline);

        Commit(repo, "scope checkpoint", allowEmpty: true);
        var scoped = Scan(
            repo,
            Path.Combine(temp.Path, "scoped"),
            ["src/TransitionSample/Caller.cs"]);

        Assert.Equal("Level1SemanticAnalysisReduced", scoped.Manifest.AnalysisLevel);
        Assert.Equal("FailedOrPartial", scoped.Manifest.BuildStatus);
        Assert.DoesNotContain(scoped.Inventory, item => item.RelativePath == "src/TransitionSample/Caller.cs");
        Assert.DoesNotContain(scoped.Facts, fact => fact.Evidence.FilePath == "src/TransitionSample/Caller.cs");
        Assert.Equal(baselineSql, SqlEvidenceSignature(scoped));
        Assert.Contains(scoped.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.CSharpSemanticWorkspace
            && fact.Properties.GetValueOrDefault("gapKind") == "ScanScopeExcludedSources"
            && fact.Evidence.FilePath == ".");
        var scanFact = Assert.Single(scoped.Facts, fact => fact.FactType == FactTypes.RepoScanned);
        Assert.Equal("src/TransitionSample/Caller.cs", scanFact.Properties["scanScopeExcludes"]);
        Assert.Equal(RuleIds.RepoManifest, scanFact.RuleId);
        Assert.Equal(EvidenceTiers.Tier2Structural, scanFact.EvidenceTier);
        Assert.Equal(scoped.Manifest.CommitSha, scanFact.CommitSha);
    }

    [Fact]
    public void Excluded_target_cannot_remain_as_a_semantic_edge_through_workspace_compilation()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        WriteFixture(repo);
        var baseline = Scan(repo, Path.Combine(temp.Path, "baseline"));
        var targetSymbolId = SemanticCall(baseline, "src/TransitionSample/Caller.cs").Properties["targetSymbolId"];

        var scoped = Scan(
            repo,
            Path.Combine(temp.Path, "scoped"),
            ["src/TransitionSample/Target.cs"]);

        Assert.Equal("FailedOrPartial", scoped.Manifest.BuildStatus);
        Assert.DoesNotContain(scoped.Inventory, item => item.RelativePath == "src/TransitionSample/Target.cs");
        Assert.DoesNotContain(scoped.Facts, fact => fact.Evidence.FilePath == "src/TransitionSample/Target.cs");
        Assert.DoesNotContain(scoped.Facts, fact =>
            fact.RuleId == RuleIds.CSharpSemanticCallGraph
            && fact.Properties.GetValueOrDefault("targetSymbolId") == targetSymbolId);
        Assert.Contains(scoped.Facts, fact =>
            fact.RuleId == RuleIds.CSharpSyntaxCallGraph
            && fact.TargetSymbol == "Execute"
            && fact.Evidence.FilePath == "src/TransitionSample/Caller.cs");
        AssertCompilationDiagnosticGap(scoped, "src/TransitionSample/Caller.cs", 7, "CS0246");
    }

    [Fact]
    public void Excluded_in_repo_generated_named_target_cannot_influence_semantic_edges()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        WriteFixture(repo, targetFileName: "Target.g.cs");
        var baseline = Scan(repo, Path.Combine(temp.Path, "baseline"));
        var targetSymbolId = SemanticCall(baseline, "src/TransitionSample/Caller.cs").Properties["targetSymbolId"];

        var scoped = Scan(
            repo,
            Path.Combine(temp.Path, "scoped"),
            ["src/TransitionSample/Target.g.cs"]);

        Assert.Equal("FailedOrPartial", scoped.Manifest.BuildStatus);
        Assert.DoesNotContain(scoped.Inventory, item => item.RelativePath == "src/TransitionSample/Target.g.cs");
        Assert.DoesNotContain(scoped.Facts, fact =>
            fact.RuleId == RuleIds.CSharpSemanticCallGraph
            && fact.Properties.GetValueOrDefault("targetSymbolId") == targetSymbolId);
        Assert.Contains(scoped.Facts, fact =>
            fact.RuleId == RuleIds.CSharpSyntaxCallGraph
            && fact.TargetSymbol == "Execute"
            && fact.Evidence.FilePath == "src/TransitionSample/Caller.cs");
    }

    private static void WriteFixture(string repo, string targetFileName = "Target.cs")
    {
        Directory.CreateDirectory(Path.Combine(repo, "src", "TransitionSample"));
        File.WriteAllText(Path.Combine(repo, "src", "TransitionSample", "TransitionSample.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <AssemblyName>TransitionSample</AssemblyName>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(repo, "src", "TransitionSample", targetFileName), """
            namespace TransitionSample;

            public sealed class Target
            {
                public void Execute() { }
            }
            """);
        File.WriteAllText(Path.Combine(repo, "src", "TransitionSample", "Caller.cs"), """
            namespace TransitionSample;

            public sealed class Caller
            {
                public void Run()
                {
                    new Target().Execute();
                }
            }
            """);
        File.WriteAllText(Path.Combine(repo, "schema.sql"), """
            create table transition_items (
                item_id integer not null
            );
            """);
        RunGit(repo, "init");
        RunGit(repo, "config", "user.email", "fixture@example.invalid");
        RunGit(repo, "config", "user.name", "TraceMap Fixture");
        Commit(repo, "baseline");
    }

    private static void WriteCrossProjectFixture(string repo)
    {
        Directory.CreateDirectory(Path.Combine(repo, "src", "Target"));
        Directory.CreateDirectory(Path.Combine(repo, "src", "Caller"));
        File.WriteAllText(Path.Combine(repo, "src", "Target", "Target.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <AssemblyName>CrossProject.Target</AssemblyName>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(repo, "src", "Target", "Target.cs"), """
            namespace CrossProject;

            public sealed class Target
            {
                public void Execute() { }
            }
            """);
        File.WriteAllText(Path.Combine(repo, "src", "Caller", "Caller.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <AssemblyName>CrossProject.Caller</AssemblyName>
              </PropertyGroup>
              <ItemGroup>
                <ProjectReference Include="../Target/Target.csproj" />
              </ItemGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(repo, "src", "Caller", "Caller.cs"), """
            namespace CrossProject;

            public sealed class Caller
            {
                public void Run()
                {
                    new Target().Execute();
                }
            }
            """);
        RunGit(repo, "init");
        RunGit(repo, "config", "user.email", "fixture@example.invalid");
        RunGit(repo, "config", "user.name", "TraceMap Fixture");
        Commit(repo, "baseline");
    }

    private static ScanResult Scan(string repo, string output, IReadOnlyList<string>? excludes = null)
    {
        var result = ScanEngine.Scan(new ScanOptions(repo, output, ExcludeGlobs: excludes));
        Assert.Matches("^[0-9a-f]{40}$", result.Manifest.CommitSha);
        Assert.All(result.Facts, fact => Assert.Equal(result.Manifest.CommitSha, fact.CommitSha));
        return result;
    }

    private static CodeFact SemanticCall(ScanResult result, string path) => Assert.Single(result.Facts, fact =>
        fact.FactType == FactTypes.CallEdge
        && fact.RuleId == RuleIds.CSharpSemanticCallGraph
        && fact.EvidenceTier == EvidenceTiers.Tier1Semantic
        && fact.ContractElement == "Execute"
        && fact.Evidence.FilePath == path);

    private static CodeFact TargetType(ScanResult result) => Assert.Single(result.Facts, fact =>
        fact.FactType == FactTypes.TypeDeclared
        && fact.RuleId == RuleIds.CSharpSemanticDeclarations
        && fact.TargetSymbol == "global::TransitionSample.Target");

    private static void AssertStableRelationship(CodeFact before, CodeFact after)
    {
        Assert.Equal(before.Properties["sourceSymbolId"], after.Properties["sourceSymbolId"]);
        Assert.Equal(before.Properties["targetSymbolId"], after.Properties["targetSymbolId"]);
        Assert.Equal(before.SourceSymbol, after.SourceSymbol);
        Assert.Equal(before.TargetSymbol, after.TargetSymbol);
        Assert.Equal(before.RuleId, after.RuleId);
        Assert.Equal(before.EvidenceTier, after.EvidenceTier);
        Assert.Equal(before.Evidence.ExtractorId, after.Evidence.ExtractorId);
        Assert.Equal(before.Evidence.ExtractorVersion, after.Evidence.ExtractorVersion);
        Assert.NotEqual(before.CommitSha, after.CommitSha);
    }

    private static void AssertStableUnchangedDeclaration(CodeFact before, CodeFact after)
    {
        Assert.Equal(before.Properties["targetSymbolId"], after.Properties["targetSymbolId"]);
        Assert.Equal(before.TargetSymbol, after.TargetSymbol);
        Assert.Equal(before.RuleId, after.RuleId);
        Assert.Equal(before.EvidenceTier, after.EvidenceTier);
        Assert.Equal(before.Evidence.FilePath, after.Evidence.FilePath);
        Assert.Equal(before.Evidence.StartLine, after.Evidence.StartLine);
        Assert.Equal(before.Evidence.EndLine, after.Evidence.EndLine);
        Assert.Equal(before.Evidence.ExtractorVersion, after.Evidence.ExtractorVersion);
    }

    private static string[] SqlEvidenceSignature(ScanResult result) => result.Facts
        .Where(fact => fact.Evidence.FilePath == "schema.sql")
        .Select(fact => string.Join('|',
            fact.FactType,
            fact.RuleId,
            fact.EvidenceTier,
            fact.ContractElement,
            fact.Evidence.StartLine,
            fact.Evidence.EndLine,
            fact.Evidence.ExtractorId,
            fact.Evidence.ExtractorVersion))
        .Order(StringComparer.Ordinal)
        .ToArray();

    private static void Commit(string repo, string message, bool allowEmpty = false)
    {
        RunGit(repo, "add", "-A");
        var arguments = new List<string> { "commit", "-m", message };
        if (allowEmpty)
        {
            arguments.Add("--allow-empty");
        }

        RunGit(repo, [.. arguments]);
    }

    private static void RunGit(string repo, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = repo,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)!;
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, process.StandardError.ReadToEnd());
    }
}
