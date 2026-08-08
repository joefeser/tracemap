using System.Diagnostics;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using TraceMap.Core;
using TraceMap.Storage;

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
        AssertNoErrorTypeSemanticFacts(scoped, "src/Caller/Caller.cs", 7);
        AssertCompilationDiagnosticGap(scoped, "src/Caller/Caller.cs", 7, "CS0246");
    }

    [Fact]
    public void Project_scope_retains_reference_sources_for_compilation_support_without_emitting_their_facts()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        WriteCrossProjectFixture(repo);

        var scoped = Scan(
            repo,
            Path.Combine(temp.Path, "project-scoped"),
            projectPaths: ["src/Caller/Caller.csproj"]);

        Assert.Equal("Succeeded", scoped.Manifest.BuildStatus);
        Assert.DoesNotContain(scoped.Inventory, item => item.RelativePath.StartsWith("src/Target/", StringComparison.Ordinal));
        Assert.DoesNotContain(scoped.Facts, fact => fact.Evidence.FilePath.StartsWith("src/Target/", StringComparison.Ordinal));
        Assert.DoesNotContain(scoped.Facts, fact =>
            fact.Properties.GetValueOrDefault("gapKind") == "ScanScopeExcludedSources");
        Assert.Contains(scoped.Facts, fact =>
            fact.FactType == FactTypes.CallEdge
            && fact.RuleId == RuleIds.CSharpSemanticCallGraph
            && fact.EvidenceTier == EvidenceTiers.Tier1Semantic
            && fact.Evidence.FilePath == "src/Caller/Caller.cs"
            && fact.TargetSymbol == "global::CrossProject.Target.Execute()");
    }

    [Fact]
    public void Project_scope_includes_linked_compile_items_outside_the_project_directory()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        WriteLinkedCompileItemFixture(repo);

        var scoped = Scan(
            repo,
            Path.Combine(temp.Path, "project-scoped"),
            projectPaths: ["src/App/App.csproj"]);

        Assert.Equal("Succeeded", scoped.Manifest.BuildStatus);
        Assert.Contains(scoped.Inventory, item => item.RelativePath == "src/Shared/Helper.cs");
        Assert.Contains(scoped.Facts, fact =>
            fact.FactType == FactTypes.TypeDeclared
            && fact.RuleId == RuleIds.CSharpSemanticDeclarations
            && fact.EvidenceTier == EvidenceTiers.Tier1Semantic
            && fact.Evidence.FilePath == "src/Shared/Helper.cs"
            && fact.TargetSymbol == "global::App.Helper");
        Assert.Contains(scoped.Facts, fact =>
            fact.FactType == FactTypes.CallEdge
            && fact.RuleId == RuleIds.CSharpSemanticCallGraph
            && fact.EvidenceTier == EvidenceTiers.Tier1Semantic
            && fact.Evidence.FilePath == "src/App/Caller.cs"
            && fact.TargetSymbol == "global::App.Helper.Assist()");
        Assert.DoesNotContain(scoped.Facts, fact =>
            fact.Properties.GetValueOrDefault("gapKind") == "ScanScopeExcludedSources");
    }

    [Fact]
    public void Excluded_base_and_await_target_cannot_persist_error_type_semantic_evidence()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        WriteErrorTypeFixture(repo);

        var scoped = Scan(
            repo,
            Path.Combine(temp.Path, "scoped"),
            ["src/App/Target.cs"]);

        Assert.Equal("FailedOrPartial", scoped.Manifest.BuildStatus);
        AssertNoErrorTypeSemanticFacts(scoped, "src/App/Caller.cs", 7);
        Assert.DoesNotContain(scoped.Facts, fact =>
            fact.EvidenceTier == EvidenceTiers.Tier1Semantic
            && fact.FactType == FactTypes.SymbolRelationship
            && fact.Evidence.FilePath == "src/App/Caller.cs"
            && fact.Evidence.StartLine == 3);
        Assert.DoesNotContain(scoped.Facts, fact =>
            fact.EvidenceTier == EvidenceTiers.Tier1Semantic
            && fact.FactType == FactTypes.AsyncBoundary
            && fact.Evidence.FilePath == "src/App/Caller.cs"
            && fact.Evidence.StartLine == 7);

        using var connection = new SqliteConnection($"Data Source={Path.Combine(temp.Path, "scoped", "index.sqlite")}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM symbols WHERE symbol_kind = 'ErrorType' OR symbol_id LIKE '% '";
        Assert.Equal(0L, (long)(command.ExecuteScalar() ?? -1L));
    }

    [Fact]
    public void Inventoried_path_matching_uses_actual_filesystem_case_semantics()
    {
        using var temp = new TempDirectory();
        var caseProbe = Path.Combine(temp.Path, "CaseProbe");
        Directory.CreateDirectory(caseProbe);
        var alternateCase = Path.Combine(temp.Path, "caseProbe");
        var comparer = CSharpSemanticExtractor.CreateSourcePathComparer(caseProbe);

        Assert.Equal(
            Directory.Exists(alternateCase),
            comparer.Equals("src/CaseSample/Caller.cs", "src/casesample/caller.cs"));
    }

    [Fact]
    public void Case_insensitive_compile_item_drift_retains_canonical_inventory_paths_and_semantic_evidence()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "CaseDriftRepo");
        Directory.CreateDirectory(repo);
        var alternateCase = Path.Combine(temp.Path, "caseDriftRepo");
        if (!Directory.Exists(alternateCase))
        {
            return;
        }

        WriteCaseDriftFixture(repo);
        var result = Scan(repo, Path.Combine(temp.Path, "output"));

        Assert.Equal("Succeeded", result.Manifest.BuildStatus);
        Assert.DoesNotContain(result.Facts, fact =>
            fact.Properties.GetValueOrDefault("gapKind") == "ScanScopeExcludedSources");
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.TypeDeclared
            && fact.RuleId == RuleIds.CSharpSemanticDeclarations
            && fact.TargetSymbol == "global::CaseDrift.Target"
            && fact.Evidence.FilePath == "src/CaseDrift/Sub/Target.cs");
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.CallEdge
            && fact.RuleId == RuleIds.CSharpSemanticCallGraph
            && fact.TargetSymbol == "global::CaseDrift.Target.Execute()");
    }

    [Fact]
    public void Non_scope_inventory_omissions_do_not_strip_compilation_support_or_claim_scope_reduction()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        WriteCustomIntermediateOutputFixture(repo);

        var result = Scan(repo, Path.Combine(temp.Path, "output"));

        Assert.Equal("Level1SemanticAnalysis", result.Manifest.AnalysisLevel);
        Assert.Equal("Succeeded", result.Manifest.BuildStatus);
        Assert.DoesNotContain(result.Facts, fact =>
            fact.Properties.GetValueOrDefault("gapKind") == "ScanScopeExcludedSources");
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.TypeDeclared
            && fact.RuleId == RuleIds.CSharpSemanticDeclarations
            && fact.TargetSymbol == "global::IntermediateOutput.Widget");
        Assert.DoesNotContain(result.Facts, fact =>
            fact.RuleId == RuleIds.CSharpSemanticDeclarations
            && fact.Evidence.FilePath.Contains("build-int/", StringComparison.Ordinal));
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

    private static void AssertNoErrorTypeSemanticFacts(ScanResult result, string filePath, int line)
    {
        Assert.DoesNotContain(result.Facts, fact =>
            fact.EvidenceTier == EvidenceTiers.Tier1Semantic
            && fact.Evidence.FilePath == filePath
            && fact.Evidence.StartLine == line
            && (fact.RuleId == RuleIds.CSharpSemanticCallGraph
                || fact.RuleId == RuleIds.CSharpSemanticObjectCreation));
        Assert.DoesNotContain(result.Facts, fact =>
            fact.EvidenceTier == EvidenceTiers.Tier1Semantic
            && (fact.Properties.GetValueOrDefault("targetSymbolKind") == "ErrorType"
                || fact.Properties.GetValueOrDefault("constructorSymbolKind") == "ErrorType"));
    }

    [Fact]
    public void Scoped_rescan_records_exclusion_and_preserves_unrelated_sql_evidence()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        WriteFixture(repo);
        var baselineOutput = Path.Combine(temp.Path, "baseline");
        var baseline = Scan(repo, baselineOutput);
        var baselineSql = SqlEvidenceSignature(baseline);
        var baselinePersistedSql = PersistedSqlEvidenceSignatures(baselineOutput);

        Commit(repo, "scope checkpoint", allowEmpty: true);
        var scopedOutput = Path.Combine(temp.Path, "scoped");
        var scoped = Scan(
            repo,
            scopedOutput,
            ["src/TransitionSample/Caller.cs"]);

        Assert.Equal("Level1SemanticAnalysisReduced", scoped.Manifest.AnalysisLevel);
        Assert.Equal("FailedOrPartial", scoped.Manifest.BuildStatus);
        Assert.DoesNotContain(scoped.Inventory, item => item.RelativePath == "src/TransitionSample/Caller.cs");
        Assert.DoesNotContain(scoped.Facts, fact => fact.Evidence.FilePath == "src/TransitionSample/Caller.cs");
        Assert.Equal(baselineSql, SqlEvidenceSignature(scoped));
        var scopedPersistedSql = PersistedSqlEvidenceSignatures(scopedOutput);
        Assert.Equal(baselinePersistedSql.Ndjson, scopedPersistedSql.Ndjson);
        Assert.Equal(baselinePersistedSql.Sqlite, scopedPersistedSql.Sqlite);
        Assert.Equal(scopedPersistedSql.Ndjson, scopedPersistedSql.Sqlite);
        var scopeGap = Assert.Single(scoped.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.CSharpSemanticWorkspace
            && fact.Properties.GetValueOrDefault("gapKind") == "ScanScopeExcludedSources"
            && fact.Evidence.FilePath == ".");
        Assert.Equal("scan-scope", scopeGap.Properties["diagnosticKind"]);
        Assert.Equal("ScanScopeExcludedSources", scopeGap.Properties["diagnosticCode"]);
        Assert.Equal("1", scopeGap.Properties["excludedDocumentCount"]);
        Assert.Equal("ReviewScanScope", scopeGap.Properties["guidanceCode"]);
        Assert.DoesNotContain("workspace", scopeGap.Properties["message"], StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(scoped.Facts, fact =>
            fact.FactType == FactTypes.BuildEnvironmentDiagnostic
            && fact.Properties.GetValueOrDefault("diagnosticCode") == "ScanScopeExcludedSources");
        var projectLoad = Assert.Single(scoped.Facts, fact =>
            fact.FactType == FactTypes.AnalyzerCapabilityDiagnostic
            && fact.Properties.GetValueOrDefault("capabilityCode") == "MSBuildProjectLoad");
        Assert.Equal("available", projectLoad.Properties["capabilityState"]);
        var buildStatus = Assert.Single(scoped.Facts, fact => fact.FactType == FactTypes.BuildStatus);
        Assert.Contains("without claiming an MSBuildWorkspace load failure", buildStatus.Properties["reason"], StringComparison.Ordinal);
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
        AssertNoErrorTypeSemanticFacts(scoped, "src/TransitionSample/Caller.cs", 7);
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
        Assert.Contains(baseline.Facts, fact =>
            fact.FactType == FactTypes.TypeDeclared
            && fact.RuleId == RuleIds.CSharpSemanticDeclarations
            && fact.EvidenceTier == EvidenceTiers.Tier1Semantic
            && fact.Evidence.FilePath == "src/TransitionSample/Target.g.cs"
            && fact.TargetSymbol == "global::TransitionSample.Target");

        var scoped = Scan(
            repo,
            Path.Combine(temp.Path, "scoped"),
            ["src/TransitionSample/Target.g.cs"]);

        Assert.Equal("FailedOrPartial", scoped.Manifest.BuildStatus);
        Assert.DoesNotContain(scoped.Inventory, item => item.RelativePath == "src/TransitionSample/Target.g.cs");
        Assert.DoesNotContain(scoped.Facts, fact =>
            fact.RuleId == RuleIds.CSharpSemanticCallGraph
            && fact.Properties.GetValueOrDefault("targetSymbolId") == targetSymbolId);
        AssertNoErrorTypeSemanticFacts(scoped, "src/TransitionSample/Caller.cs", 7);
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

    private static void WriteCustomIntermediateOutputFixture(string repo)
    {
        var projectDirectory = Path.Combine(repo, "src", "IntermediateOutput");
        Directory.CreateDirectory(projectDirectory);
        File.WriteAllText(Path.Combine(projectDirectory, "IntermediateOutput.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <AssemblyName>IntermediateOutput</AssemblyName>
                <BaseIntermediateOutputPath>$(MSBuildThisFileDirectory)build-int/</BaseIntermediateOutputPath>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(projectDirectory, "Widget.cs"), """
            namespace IntermediateOutput;

            public sealed class Widget
            {
                public void Execute() { }
            }
            """);
        RunDotnet(repo, "build", "src/IntermediateOutput/IntermediateOutput.csproj", "--nologo", "--verbosity", "quiet");
        RunGit(repo, "init");
        RunGit(repo, "config", "user.email", "fixture@example.invalid");
        RunGit(repo, "config", "user.name", "TraceMap Fixture");
        Commit(repo, "baseline");
    }

    private static void WriteCaseDriftFixture(string repo)
    {
        var projectDirectory = Path.Combine(repo, "src", "CaseDrift");
        Directory.CreateDirectory(Path.Combine(projectDirectory, "Sub"));
        File.WriteAllText(Path.Combine(projectDirectory, "CaseDrift.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <AssemblyName>CaseDrift</AssemblyName>
                <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
              </PropertyGroup>
              <ItemGroup>
                <Compile Include="sub/target.cs" />
                <Compile Include="Caller.cs" />
              </ItemGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(projectDirectory, "Sub", "Target.cs"), """
            namespace CaseDrift;

            public sealed class Target
            {
                public void Execute() { }
            }
            """);
        File.WriteAllText(Path.Combine(projectDirectory, "Caller.cs"), """
            namespace CaseDrift;

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

    private static void WriteLinkedCompileItemFixture(string repo)
    {
        Directory.CreateDirectory(Path.Combine(repo, "src", "App"));
        Directory.CreateDirectory(Path.Combine(repo, "src", "Shared"));
        File.WriteAllText(
            Path.Combine(repo, "src", "App", "App.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <AssemblyName>App</AssemblyName>
              </PropertyGroup>
              <ItemGroup>
                <Compile Include="../Shared/Helper.cs" Link="Helper.cs" />
              </ItemGroup>
            </Project>
            """);
        File.WriteAllText(
            Path.Combine(repo, "src", "Shared", "Helper.cs"),
            """
            namespace App;

            public sealed class Helper
            {
                public void Assist() { }
            }
            """);
        File.WriteAllText(
            Path.Combine(repo, "src", "App", "Caller.cs"),
            """
            namespace App;

            public sealed class Caller
            {
                public void Run() => new Helper().Assist();
            }
            """);
        RunGit(repo, "init");
        RunGit(repo, "config", "user.email", "fixture@example.invalid");
        RunGit(repo, "config", "user.name", "TraceMap Fixture");
        Commit(repo, "baseline");
    }

    private static void WriteErrorTypeFixture(string repo)
    {
        Directory.CreateDirectory(Path.Combine(repo, "src", "App"));
        File.WriteAllText(
            Path.Combine(repo, "src", "App", "App.csproj"),
            """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
            </Project>
            """);
        File.WriteAllText(
            Path.Combine(repo, "src", "App", "Target.cs"),
            """
            namespace App;

            public interface ITargetContract { }

            public static class Target
            {
                public static Task ExecuteAsync() => Task.CompletedTask;
            }
            """);
        File.WriteAllText(
            Path.Combine(repo, "src", "App", "Caller.cs"),
            """
            namespace App;

            public sealed class Caller : ITargetContract
            {
                public async Task Run()
                {
                    await Target.ExecuteAsync();
                }
            }
            """);
        RunGit(repo, "init");
        RunGit(repo, "config", "user.email", "fixture@example.invalid");
        RunGit(repo, "config", "user.name", "TraceMap Fixture");
        Commit(repo, "baseline");
    }

    private static ScanResult Scan(
        string repo,
        string output,
        IReadOnlyList<string>? excludes = null,
        IReadOnlyList<string>? projectPaths = null)
    {
        var result = ScanEngine.Scan(new ScanOptions(
            repo,
            output,
            ProjectPaths: projectPaths,
            ExcludeGlobs: excludes));
        Assert.Matches("^[0-9a-f]{40}$", result.Manifest.CommitSha);
        Assert.All(result.Facts, fact => Assert.Equal(result.Manifest.CommitSha, fact.CommitSha));
        Directory.CreateDirectory(output);
        JsonlFactWriter.WriteAsync(Path.Combine(output, "facts.ndjson"), result.Facts).GetAwaiter().GetResult();
        SqliteIndexWriter.Write(Path.Combine(output, "index.sqlite"), result.Manifest, result.Facts);
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
        .Select(NormalizedFactSignature)
        .Order(StringComparer.Ordinal)
        .ToArray();

    private static (string[] Ndjson, string[] Sqlite) PersistedSqlEvidenceSignatures(string output)
    {
        var jsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var ndjson = File.ReadLines(Path.Combine(output, "facts.ndjson"))
            .Select(line => JsonSerializer.Deserialize<CodeFact>(line, jsonOptions))
            .Select(fact => Assert.IsType<CodeFact>(fact))
            .Where(fact => fact.Evidence.FilePath == "schema.sql")
            .Select(NormalizedFactSignature)
            .Order(StringComparer.Ordinal)
            .ToArray();

        using var connection = new SqliteConnection($"Data Source={Path.Combine(output, "index.sqlite")}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            select repo, project_path, fact_type, rule_id, evidence_tier,
                   source_symbol, target_symbol, contract_element,
                   file_path, start_line, end_line, snippet_hash,
                   extractor_id, extractor_version, properties_json
            from facts
            where file_path = 'schema.sql'
            order by fact_id
            """;
        using var reader = command.ExecuteReader();
        var sqliteFacts = new List<CodeFact>();
        while (reader.Read())
        {
            var properties = JsonSerializer.Deserialize<SortedDictionary<string, string>>(
                reader.GetString(14),
                jsonOptions) ?? [];
            sqliteFacts.Add(new CodeFact(
                "ignored",
                "ignored",
                reader.GetString(0),
                "ignored",
                reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                new EvidenceSpan(
                    reader.GetString(8),
                    reader.GetInt32(9),
                    reader.GetInt32(10),
                    reader.IsDBNull(11) ? null : reader.GetString(11),
                    reader.GetString(12),
                    reader.GetString(13)),
                properties));
        }

        return (
            ndjson,
            sqliteFacts.Select(NormalizedFactSignature).Order(StringComparer.Ordinal).ToArray());
    }

    private static string NormalizedFactSignature(CodeFact fact) => JsonSerializer.Serialize(new
    {
        fact.Repo,
        fact.ProjectPath,
        fact.FactType,
        fact.RuleId,
        fact.EvidenceTier,
        fact.SourceSymbol,
        fact.TargetSymbol,
        fact.ContractElement,
        fact.Evidence.FilePath,
        fact.Evidence.StartLine,
        fact.Evidence.EndLine,
        fact.Evidence.SnippetHash,
        fact.Evidence.ExtractorId,
        fact.Evidence.ExtractorVersion,
        Properties = fact.Properties.OrderBy(pair => pair.Key, StringComparer.Ordinal)
    });

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
        RunProcess(repo, "git", arguments);
    }

    private static void RunDotnet(string repo, params string[] arguments)
    {
        RunProcess(repo, "dotnet", arguments);
    }

    private static void RunProcess(string workingDirectory, string fileName, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
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
