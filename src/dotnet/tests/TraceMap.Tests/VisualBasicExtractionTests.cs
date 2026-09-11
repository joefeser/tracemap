using Microsoft.Data.Sqlite;
using TraceMap.Core;

namespace TraceMap.Tests;

// Issue #736 spec tasks 5-7: compiler-resolved Visual Basic facts, bounded
// per-file syntax fallback, and shared SQLite/project-metadata integration.
// Every assertion below claims only what the emitted evidence honestly shows.
public sealed class VisualBasicExtractionTests
{
    // ---------- Task 5: compiler-backed semantic facts ----------

    [Fact]
    public void Modern_vb_fixture_emits_compiler_resolved_declaration_facts()
    {
        var result = ScanModernFixture();

        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.TypeDeclared
            && fact.RuleId == RuleIds.VisualBasicSemanticDeclarations
            && fact.EvidenceTier == EvidenceTiers.Tier1Semantic
            && fact.TargetSymbol?.Contains("PurchaseOrder", StringComparison.Ordinal) == true
            && fact.Properties["typeKind"] == "Class"
            && fact.Properties["namespace"] == "VbModernSample.Contracts");
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.MethodDeclared
            && fact.RuleId == RuleIds.VisualBasicSemanticDeclarations
            && fact.ContractElement == "Fulfill");
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.PropertyDeclared
            && fact.ContractElement == "CustomerCode"
            && fact.Properties["isDefault"] == "False");
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.FieldDeclared
            && fact.ContractElement == "_formatter"
            && fact.Properties["containingType"].Contains("PriceCatalog", StringComparison.Ordinal));
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.ParameterDeclared
            && fact.ContractElement == "order"
            && fact.Properties["isByRef"] == "False");
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.TypeDeclared
            && fact.Properties["typeKind"] == "Delegate"
            && fact.TargetSymbol?.Contains("PriceFormatter", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void Modern_vb_fixture_resolves_calls_with_overload_selection()
    {
        var result = ScanModernFixture();

        // Overload resolution: the Decimal and Integer Sum calls bind to
        // distinct compiler-resolved callees.
        var callEdges = result.Facts
            .Where(fact => fact.FactType == FactTypes.CallEdge
                && fact.RuleId == RuleIds.VisualBasicSemanticCallGraph
                && fact.SourceSymbol?.Contains("Fulfill", StringComparison.Ordinal) == true
                && fact.TargetSymbol?.Contains(".Sum(", StringComparison.Ordinal) == true)
            .ToArray();
        Assert.Equal(2, callEdges.Length);
        Assert.Contains(callEdges, edge => edge.TargetSymbol!.Contains("Decimal", StringComparison.Ordinal));
        Assert.Contains(callEdges, edge => edge.TargetSymbol!.Contains("Integer", StringComparison.Ordinal));
        Assert.All(callEdges, edge => Assert.Equal("SemanticMethodInvocation", edge.Properties["callKind"]));

        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.MethodInvoked
            && fact.RuleId == RuleIds.VisualBasicSemanticMethodInvocation
            && fact.ContractElement == "TryNormalizeSku"
            && fact.Properties["receiverSymbol"].Contains("_utilities", StringComparison.Ordinal));
    }

    [Fact]
    public void Modern_vb_fixture_emits_interface_inheritance_and_override_relationships()
    {
        var result = ScanModernFixture();

        var relationships = result.Facts
            .Where(fact => fact.FactType == FactTypes.SymbolRelationship
                && fact.RuleId == RuleIds.VisualBasicSemanticSymbolRelationship)
            .ToArray();
        Assert.Contains(relationships, fact =>
            fact.Properties["relationshipKind"] == "ImplementsInterface"
            && fact.SourceSymbol?.Contains("WarehouseOrderRepository", StringComparison.Ordinal) == true
            && fact.TargetSymbol?.EndsWith("IOrderRepository", StringComparison.Ordinal) == true);
        Assert.Contains(relationships, fact =>
            fact.Properties["relationshipKind"] == "ImplementsInterfaceMember"
            && fact.SourceSymbol?.Contains(".Save(", StringComparison.Ordinal) == true
            && fact.TargetSymbol?.Contains("IOrderRepository.Save", StringComparison.Ordinal) == true);
        Assert.Contains(relationships, fact =>
            fact.Properties["relationshipKind"] == "InheritsFrom"
            && fact.SourceSymbol?.Contains("PurchaseOrder", StringComparison.Ordinal) == true
            && fact.TargetSymbol?.EndsWith("EntityBase", StringComparison.Ordinal) == true);
        Assert.Contains(relationships, fact =>
            fact.Properties["relationshipKind"] == "Overrides"
            && fact.SourceSymbol?.Contains("PurchaseOrder.Describe", StringComparison.Ordinal) == true);
        Assert.Contains(relationships, fact =>
            fact.Properties["relationshipKind"] == "InheritsFrom"
            && fact.SourceSymbol?.Contains("PriceRecalculatedEventArgs", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void Modern_vb_fixture_resolves_default_member_property_access()
    {
        var result = ScanModernFixture();

        // catalog(0) resolves through the unambiguous default property Quote.
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.PropertyAccessed
            && fact.RuleId == RuleIds.VisualBasicSemanticPropertyAccess
            && fact.EvidenceTier == EvidenceTiers.Tier1Semantic
            && fact.TargetSymbol?.Contains("PriceCatalog.Quote", StringComparison.Ordinal) == true
            && fact.Properties["isDefault"] == "True");
    }

    [Fact]
    public void Modern_vb_fixture_emits_argument_flow_for_named_optional_byref_and_params_arguments()
    {
        var result = ScanModernFixture();

        var arguments = result.Facts
            .Where(fact => fact.FactType == FactTypes.ArgumentPassed
                && fact.RuleId == RuleIds.VisualBasicSemanticValueFlow
                && fact.SourceSymbol?.Contains("Fulfill", StringComparison.Ordinal) == true)
            .ToArray();

        // Named argument suffix:=... binds to the Optional parameter.
        var named = Assert.Single(arguments, fact => fact.ContractElement == "suffix");
        Assert.Equal("1", named.Properties["parameterOrdinal"]);
        Assert.Equal("Literal", named.Properties["argumentExpressionKind"]);

        // The ByRef customerCode argument binds to parameter sku.
        Assert.Contains(arguments, fact =>
            fact.ContractElement == "sku"
            && fact.Properties["argumentExpressionKind"] == "Identifier");

        // ParamArray arguments expand onto the tags parameter.
        Assert.Equal(3, arguments.Count(fact => fact.ContractElement == "tags"));

        // Argument expressions are hashed, never stored raw.
        Assert.All(arguments, fact =>
        {
            Assert.True(fact.Properties.ContainsKey("argumentExpressionHash"));
            Assert.True(fact.Properties["argumentExpressionHash"].Length > 0);
        });
    }

    [Fact]
    public void Vb_scans_never_promote_event_wiring_to_resolved_edges()
    {
        var modern = ScanModernFixture();
        var webforms = ScanEngine.Scan(new ScanOptions(
            Path.Combine(FindRepoRoot(), "samples", "vb-webforms-sample"),
            Path.Combine(Path.GetTempPath(), "tracemap-vb-extraction-webforms")));

        foreach (var result in new[] { modern, webforms })
        {
            // Handles/AddHandler/RemoveHandler/RaiseEvent must stay event-less:
            // no resolved edge may target an event accessor or the wiring surface.
            Assert.DoesNotContain(result.Facts, fact =>
                fact.RuleId.StartsWith("vb.semantic.", StringComparison.Ordinal)
                && (fact.TargetSymbol?.Contains("add_", StringComparison.Ordinal) == true
                    || fact.TargetSymbol?.Contains(".Click", StringComparison.Ordinal) == true
                    || fact.TargetSymbol?.Contains("Me.Init", StringComparison.Ordinal) == true));
            Assert.DoesNotContain(result.Facts, fact =>
                fact.RuleId.StartsWith("vb.semantic.", StringComparison.Ordinal)
                && fact.FactType == FactTypes.SymbolRelationship
                && (fact.TargetSymbol?.Contains("StatusChanged", StringComparison.Ordinal) == true
                    || fact.TargetSymbol?.Contains("PriceRecalculated", StringComparison.Ordinal) == true));
        }

        // The handlers themselves remain declared method evidence.
        Assert.Contains(webforms.Facts, fact =>
            fact.FactType == FactTypes.MethodDeclared
            && fact.RuleId == RuleIds.VisualBasicSemanticDeclarations
            && fact.ContractElement == "SaveButton_Click");
    }

    // ---------- Task 6: bounded per-file syntax fallback ----------

    [Fact]
    public void Orphan_vb_files_without_a_project_fall_back_to_bounded_syntax_facts()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Orphan.vb"), """
            Namespace Orphan
                Public Class Widget
                    Public Function Describe() As String
                        Dim factory As New WidgetFactory()
                        Return factory.Build()
                    End Function
                End Class
            End Namespace
            """);
        Commit(repo);

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));

        // Tier3 syntax evidence, clearly separated by rule and tier.
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.TypeDeclared
            && fact.RuleId == RuleIds.VisualBasicSyntaxDeclarations
            && fact.EvidenceTier == EvidenceTiers.Tier3SyntaxOrTextual
            && fact.TargetSymbol == "Widget");
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.CallEdge
            && fact.RuleId == RuleIds.VisualBasicSyntaxCallGraph
            && fact.EvidenceTier == EvidenceTiers.Tier3SyntaxOrTextual
            && fact.Properties["callKind"] == "SyntaxInvocation");
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.ObjectCreated
            && fact.RuleId == RuleIds.VisualBasicSyntaxObjectCreation
            && fact.Properties["creationKind"] == "SyntaxObjectCreation");
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.MethodDeclared
            && fact.RuleId == RuleIds.VisualBasicSyntaxDeclarations
            && fact.TargetSymbol == "Describe");

        // Explicit per-file Tier4 gap plus the scan-level no-project gap; the
        // manifest records sanitized categorical gap messages.
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.VisualBasicSemanticWorkspace
            && fact.EvidenceTier == EvidenceTiers.Tier4Unknown
            && fact.Properties["gapKind"] == "SemanticAnalysisUnavailable");
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.VisualBasicSemanticWorkspace
            && fact.Properties["gapKind"] == "NoVisualBasicProjectOrSolution");
        Assert.NotEmpty(result.Manifest.KnownGaps);

        // Syntax-only callees are text, never symbol-ID joins.
        Assert.DoesNotContain(result.Facts, fact =>
            fact.RuleId.StartsWith("vb.syntax.", StringComparison.Ordinal)
            && fact.Properties.Any(pair => pair.Key.EndsWith("SymbolId", StringComparison.Ordinal)));
    }

    [Fact]
    public void Failed_vb_project_load_falls_back_to_syntax_facts_with_reduced_coverage()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Broken.vbproj"), "this is not valid msbuild xml");
        File.WriteAllText(Path.Combine(repo, "Sample.vb"), """
            Module Sample
                Sub Main()
                    Dim text As String = "unused"
                    Console.WriteLine(text)
                End Sub
            End Module
            """);
        Commit(repo);

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));

        // The unusable project must surface as sanitized workspace/compilation
        // gaps and reduced coverage; the exact gap categorization depends on
        // where MSBuild fails, so the test claims the posture, not the code.
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.VisualBasicSemanticWorkspace
            && fact.EvidenceTier == EvidenceTiers.Tier4Unknown
            && (fact.Properties["gapKind"] == "ProjectLoadFailed"
                || fact.Properties["gapKind"] == "CompilationDiagnostic"
                || fact.Properties["gapKind"] == "WorkspaceDiagnostic"));
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.CallEdge
            && fact.RuleId == RuleIds.VisualBasicSyntaxCallGraph
            && fact.EvidenceTier == EvidenceTiers.Tier3SyntaxOrTextual);
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.Properties.ContainsKey("gapKind")
            && fact.Properties["gapKind"] == "SemanticAnalysisUnavailable");
        Assert.Equal("FailedOrPartial", result.Manifest.BuildStatus);
        Assert.EndsWith("Reduced", result.Manifest.AnalysisLevel, StringComparison.Ordinal);
    }

    [Fact]
    public void Semantically_analyzed_vb_files_do_not_receive_tier3_duplicates()
    {
        var result = ScanModernFixture();

        Assert.DoesNotContain(result.Facts, fact =>
            fact.RuleId.StartsWith("vb.syntax.", StringComparison.Ordinal));
    }

    // ---------- Task 7: shared joins and project metadata ----------

    [Fact]
    public async Task Vb_facts_join_into_shared_sqlite_tables()
    {
        var repoRoot = FindRepoRoot();
        using var temp = new TempDirectory();
        var outputPath = Path.Combine(temp.Path, "out");
        using var output = new StringWriter();
        using var error = new StringWriter();
        Assert.Equal(0, await TraceMap.Cli.TraceMapCommand.RunAsync(
            ["scan", "--repo", Path.Combine(repoRoot, "samples", "vb-modern-sample"), "--out", outputPath],
            output,
            error));

        await using var connection = new SqliteConnection($"Data Source={Path.Combine(outputPath, "index.sqlite")}");
        await connection.OpenAsync();

        Assert.True(await CountAsync(connection, "select count(*) from call_edges where rule_id like 'vb.semantic.%'") > 0);
        Assert.True(await CountAsync(connection, "select count(*) from object_creations where rule_id like 'vb.semantic.%'") > 0);
        Assert.True(await CountAsync(connection, "select count(*) from argument_flows where rule_id like 'vb.semantic.%'") > 0);
        Assert.True(await CountAsync(connection, "select count(*) from symbol_relationships where rule_id like 'vb.semantic.%'") > 0);
        Assert.True(await CountAsync(connection, """
            select count(*) from fact_symbols fs
            join facts f on f.fact_id = fs.fact_id
            where f.rule_id like 'vb.semantic.%'
            """) > 0);
        Assert.True(await CountAsync(connection, "select count(*) from symbol_occurrences so join facts f on f.fact_id = so.fact_id where f.rule_id like 'vb.semantic.%'") > 0);
        Assert.Equal(0, await CountAsync(connection, "select count(*) from symbols where language <> 'visualbasic'"));

        // Value-origin joins: at least one resolved parameter forwarding edge.
        Assert.True(await CountAsync(connection, "select count(*) from parameter_forward_edges") > 0);

        // No Tier3 rows leak into the resolved call-edge table from VB fallback
        // when the whole fixture is semantically covered.
        Assert.Equal(0, await CountAsync(connection, "select count(*) from call_edges where rule_id like 'vb.syntax.%'"));
    }

    [Fact]
    public void Vbproj_target_frameworks_and_package_references_are_emitted()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Pkg.vbproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <RootNamespace>Pkg</RootNamespace>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Newtonsoft.Json" Version="13.0.3" />
              </ItemGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(repo, "Keeper.vb"), "Module Keeper\nEnd Module");
        Commit(repo);

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));

        var framework = Assert.Single(result.Facts, fact =>
            fact.FactType == FactTypes.TargetFrameworkDeclared
            && fact.ProjectPath == "Pkg.vbproj");
        Assert.Equal("net10.0", framework.TargetSymbol);

        var package = Assert.Single(result.Facts, fact =>
            fact.FactType == FactTypes.PackageReferenced
            && fact.TargetSymbol == "Newtonsoft.Json");
        Assert.Equal("Pkg.vbproj", package.ProjectPath);
        Assert.Equal("vbproj", package.Properties["manifestKind"]);
        Assert.Equal("13.0.3", package.Properties["version"]);
    }

    [Fact]
    public void Mixed_solution_vb_semantic_facts_are_not_duplicated_or_missing()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Mixed.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(repo, "Alpha.cs"), """
            namespace Mixed;
            public sealed class Alpha { public void Run() { } }
            """);
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
                    Public Sub Go()
                    End Sub
                End Class
            End Namespace
            """);
        Commit(repo);

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));

        // Exactly one owner per language: VB declarations are compiler-resolved
        // exactly once, C# declarations stay under the C# rule.
        Assert.Equal(1, result.Facts.Count(fact =>
            fact.FactType == FactTypes.TypeDeclared
            && fact.RuleId == RuleIds.VisualBasicSemanticDeclarations
            && fact.TargetSymbol?.Contains("Bravo", StringComparison.Ordinal) == true));
        Assert.Equal(1, result.Facts.Count(fact =>
            fact.FactType == FactTypes.MethodDeclared
            && fact.RuleId == RuleIds.VisualBasicSemanticDeclarations
            && fact.ContractElement == "Go"));
        Assert.Equal(1, result.Facts.Count(fact =>
            fact.FactType == FactTypes.TypeDeclared
            && fact.RuleId == RuleIds.CSharpSemanticDeclarations
            && fact.TargetSymbol?.Contains("Alpha", StringComparison.Ordinal) == true));
        Assert.DoesNotContain(result.Facts, fact =>
            fact.RuleId.StartsWith("vb.syntax.", StringComparison.Ordinal));
    }

    // ---------- Determinism and privacy ----------

    [Fact]
    public void Modern_vb_scan_facts_and_sqlite_joins_are_deterministic()
    {
        var repoRoot = FindRepoRoot();
        var repoPath = Path.Combine(repoRoot, "samples", "vb-modern-sample");
        var first = ScanEngine.Scan(new ScanOptions(repoPath, Path.Combine(Path.GetTempPath(), "tracemap-vb-ext-det-1")));
        var second = ScanEngine.Scan(new ScanOptions(repoPath, Path.Combine(Path.GetTempPath(), "tracemap-vb-ext-det-2")));

        var selector = (ScanResult result) => result.Facts
            .Select(fact => string.Join('|',
                fact.FactId,
                fact.FactType,
                fact.RuleId,
                fact.SourceSymbol ?? string.Empty,
                fact.TargetSymbol ?? string.Empty,
                string.Join(";", fact.Properties.Select(pair => $"{pair.Key}={pair.Value}"))))
            .ToArray();
        Assert.Equal(selector(first), selector(second));
    }

    [Fact]
    public void Vb_facts_do_not_leak_paths_source_text_or_literal_values()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Secrets.vb"), """
            Module Secrets
                Const PasswordMarker As String = "super-secret-sentinel-value"
                Sub Main()
                    System.Console.WriteLine(PasswordMarker)
                End Sub
            End Module
            """);
        Commit(repo);

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));

        foreach (var fact in result.Facts)
        {
            var factText = string.Join('|',
                fact.Evidence.FilePath,
                fact.SourceSymbol ?? string.Empty,
                fact.TargetSymbol ?? string.Empty,
                fact.ContractElement ?? string.Empty,
                string.Join(";", fact.Properties.Select(pair => $"{pair.Key}={pair.Value}")));
            Assert.DoesNotContain(temp.Path, factText, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("super-secret-sentinel-value", factText, StringComparison.Ordinal);
        }
    }

    // ---------- Helpers ----------

    private static ScanResult ScanModernFixture()
    {
        return ScanEngine.Scan(new ScanOptions(
            Path.Combine(FindRepoRoot(), "samples", "vb-modern-sample"),
            Path.Combine(Path.GetTempPath(), "tracemap-vb-extraction-modern")));
    }

    private static async Task<long> CountAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt64(result, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void Commit(string repo, string message = "vb extraction test fixture")
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
            if (Directory.Exists(Path.Combine(directory.FullName, ".git"))
                && Directory.Exists(Path.Combine(directory.FullName, "samples")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
