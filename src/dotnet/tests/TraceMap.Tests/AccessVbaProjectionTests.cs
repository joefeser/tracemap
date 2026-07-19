using System.Text.Json;
using Microsoft.Data.Sqlite;
using TraceMap.Access;
using TraceMap.Access.Cli;
using TraceMap.Combine;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class AccessVbaProjectionTests
{
    private const string ProtectedComment = "PrivateCommentMarker_92817";
    private const string ProtectedLiteral = "Password_VbaLiteral_92817";
    private const string ProtectedCommand = "PrivateCommandBody_92817";

    [Fact]
    public void Projector_masks_comments_and_ordinary_literals_projects_allowlisted_calls_and_maps_exact_event_procedure()
    {
        const string source = """
            Option Compare Database
            Private Sub Form_Load()
                ' Call CommentOnlyTarget PrivateCommentMarker_92817
                x = 1: Rem Call RemOnlyTarget
                Dim prose As String
                prose = "DoCmd.OpenForm ""LiteralOnlyTarget"" Password_VbaLiteral_92817"
                Call Helper
                DoCmd.OpenForm "frmCustomers"
                DoCmd.OpenReport targetVariable
                Set q = CurrentDb.QueryDefs("qryOrders")
                Set rs = CurrentDb.OpenRecordset("Customers")
                value = DLookup("CustomerId", "Customers")
                Application.Run callbackName
                Shell "PrivateCommandBody_92817"
            End Sub

            Private Sub Helper()
            End Sub
            """;
        var seed = AccessSafeValues.DatabaseIdentitySeed("repo", new string('a', 40), "fixture.accdb", "hash");
        var form = AccessSafeValues.Identity(seed, "form", "frmCustomers");
        var query = AccessSafeValues.Identity(seed, "query", "qryOrders");
        var table = AccessSafeValues.Identity(seed, "table", "Customers");
        var known = new Dictionary<string, IReadOnlyList<(string StableKey, string Kind)>>(StringComparer.OrdinalIgnoreCase)
        {
            ["frmCustomers"] = [(form.StableKey, "form")],
            ["qryOrders"] = [(query.StableKey, "query")],
            ["Customers"] = [(table.StableKey, "table")]
        };

        var result = AccessVbaProjector.Project(
            seed,
            [new("Form_frmCustomers", "form", source)],
            [
                new("surface-key", "on-load", "Form_frmCustomers", "Form_Load"),
                new("surface-key", "PasswordEventRole_92817", "Form_frmCustomers", "Form_Load")
            ],
            known);

        var module = Assert.Single(result.Modules);
        Assert.Equal("form-class", module.ModuleKind);
        Assert.Equal(2, module.Procedures.Count);
        var procedure = module.Procedures.Single(item => item.Identity.DisplayName == "Form_Load");
        Assert.Equal(8, procedure.Calls.Count);
        Assert.Contains(procedure.Calls, call => call.CallKind == "local-procedure-call" && call.TargetStableKey is not null);
        Assert.Contains(procedure.Calls, call => call.CallKind == "open-form" && call.TargetStableKey == form.StableKey);
        Assert.Contains(procedure.Calls, call => call.CallKind == "dao-query-reference" && call.TargetStableKey == query.StableKey);
        Assert.Contains(procedure.Calls, call => call.CallKind == "open-recordset-reference" && call.TargetStableKey == table.StableKey);
        Assert.Contains(procedure.Calls, call => call.CallKind == "domain-function-reference" && call.TargetStableKey == table.StableKey);
        Assert.Contains(procedure.Calls, call => call.CallKind == "open-report" && call.Coverage == "partial");
        Assert.DoesNotContain(procedure.Calls, call => call.LiteralTargetIdentity?.DisplayName == "LiteralOnlyTarget");
        Assert.DoesNotContain(procedure.Calls, call => call.LiteralTargetIdentity?.DisplayName == "CommentOnlyTarget");
        Assert.DoesNotContain(procedure.Calls, call => call.LiteralTargetIdentity?.DisplayName == "RemOnlyTarget");
        Assert.Contains(result.Gaps, gap => gap.Classification == "AccessVbaDynamicDispatch");

        var binding = Assert.Single(result.EventBindings);
        Assert.Equal("complete", binding.Coverage);
        Assert.Equal(procedure.Identity.StableKey, binding.ProcedureStableKey);

        var wire = JsonSerializer.Serialize(result);
        Assert.DoesNotContain(ProtectedComment, wire, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(ProtectedLiteral, wire, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(ProtectedCommand, wire, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PasswordEventRole_92817", wire, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(source, wire, StringComparison.Ordinal);
        Assert.Contains(result.Gaps, gap => gap.Classification == "AccessEventRoleUnsupported");
    }

    [Fact]
    public void Projector_is_order_independent_hashes_secret_bearing_literal_targets_and_emits_event_and_limit_gaps()
    {
        const string sourceA = """
            Public Sub Alpha()
                DoCmd.OpenForm "PasswordTarget_92817"
            End Sub
            """;
        const string sourceB = """
            Public Function Beta() As Boolean
                Beta = True
            End Function
            """;
        var seed = AccessSafeValues.DatabaseIdentitySeed("repo", new string('b', 40), "fixture.accdb", "hash");
        var forward = AccessVbaProjector.Project(seed,
            [new("ModuleA", "standard", sourceA), new("ModuleB", "class", sourceB)],
            [new("owner", "on-click", "ModuleA", "MissingHandler")]);
        var reverse = AccessVbaProjector.Project(seed,
            [new("ModuleB", "class", sourceB), new("ModuleA", "standard", sourceA)],
            [new("owner", "on-click", "ModuleA", "MissingHandler")]);

        Assert.Equal(JsonSerializer.Serialize(forward), JsonSerializer.Serialize(reverse));
        var literal = forward.Modules.SelectMany(module => module.Procedures).SelectMany(procedure => procedure.Calls)
            .Single().LiteralTargetIdentity;
        Assert.NotNull(literal);
        Assert.Null(literal.DisplayName);
        Assert.DoesNotContain("PasswordTarget_92817", JsonSerializer.Serialize(forward), StringComparison.OrdinalIgnoreCase);
        Assert.Contains(forward.Gaps, gap => gap.Classification == "AccessEventProcedureUnresolved"
            && gap.RuleId == RuleIds.LegacyAccessEventBinding);

        var limited = AccessVbaProjector.Project(seed, [new("ModuleA", "standard", sourceA)], limits: AccessLimits.Default with
        {
            MaxVbaModuleTextLength = 10
        });
        Assert.Empty(limited.Modules);
        Assert.Contains(limited.Gaps, gap => gap.Classification == "AccessVbaModuleTextLimitReached");
    }

    [Fact]
    public async Task Fact_builder_preserves_vba_line_spans_rules_and_safe_projections_without_source()
    {
        const string source = """
            Private Sub Form_Load()
                ' PrivateVbaComment_92817
                sqlText = "SELECT * FROM PrivateSqlTable_92817"
                localPath = "C:\Users\PrivatePerson_92817\secret.txt"
                DoCmd.OpenForm "frmCustomers"
            End Sub
            """;
        using var temp = new TempDirectory();
        var databasePath = Path.Combine(temp.Path, "fixture.accdb");
        await File.WriteAllBytesAsync(databasePath, [1, 2, 3, 4]);
        var databaseHash = AccessInputValidator.HashFile(databasePath);
        var input = new AccessValidatedInput(
            temp.Path, "repo", AccessSafeValues.RoleHash("access-repository-identity", "repo"), null, "test",
            new string('c', 40), databasePath, "fixture.accdb", databaseHash, ".accdb", Path.Combine(temp.Path, "out"), false);
        var seed = AccessSafeValues.DatabaseIdentitySeed(input.RepositoryIdentityHash, input.CommitSha, input.DatabaseRelativePath, input.DatabaseHash);
        var form = AccessSafeValues.Identity(seed, "form", "frmCustomers");
        var projected = AccessVbaProjector.Project(seed,
            [new("Form_frmCustomers", "form", source)],
            [new("surface-key", "on-load", "Form_frmCustomers", "Form_Load")],
            new Dictionary<string, IReadOnlyList<(string StableKey, string Kind)>>(StringComparer.OrdinalIgnoreCase)
            {
                ["frmCustomers"] = [(form.StableKey, "form")]
            });
        var projection = new AccessDatabaseProjection(
            "tracemap.access-projection.v1", databaseHash, ".accdb", "16.0", 1234, false, false, 0,
            [], [], [], [], projected.Gaps, [new("vbaModules", "observed")], [], projected.Modules, projected.EventBindings);

        var scan = AccessFactBuilder.Build(input, projection, new(temp.Path, "fixture.accdb", input.OutputFullPath));

        var moduleFact = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.AccessVbaModuleDeclared);
        Assert.Equal(RuleIds.LegacyAccessVba, moduleFact.RuleId);
        Assert.Equal(EvidenceTiers.Tier3SyntaxOrTextual, moduleFact.EvidenceTier);
        Assert.True(moduleFact.Evidence.EndLine >= 3);
        var procedureFact = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.AccessVbaProcedureDeclared);
        Assert.Equal(1, procedureFact.Evidence.StartLine);
        Assert.Equal(6, procedureFact.Evidence.EndLine);
        var callFact = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.AccessNavigationCandidate);
        Assert.Equal(5, callFact.Evidence.StartLine);
        Assert.Equal(form.StableKey, callFact.TargetSymbol);
        Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.AccessEventBindingCandidate
            && fact.RuleId == RuleIds.LegacyAccessEventBinding);

        var wire = JsonSerializer.Serialize(scan);
        Assert.DoesNotContain("DoCmd.OpenForm", wire, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(source, wire, StringComparison.Ordinal);

        await AccessArtifactWriter.WriteAsync(input.OutputFullPath, scan, AccessLimits.Default);
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([Path.Combine(input.OutputFullPath, "index.sqlite")], combinedPath, ["access"]));
        var protectedMarkers = new[]
        {
            "PrivateVbaComment_92817",
            "PrivateSqlTable_92817",
            "PrivatePerson_92817",
            "SELECT * FROM",
            "C:\\Users\\"
        };
        foreach (var file in Directory.EnumerateFiles(input.OutputFullPath, "*", SearchOption.AllDirectories).Append(combinedPath))
        {
            var artifactText = System.Text.Encoding.UTF8.GetString(await File.ReadAllBytesAsync(file));
            foreach (var marker in protectedMarkers)
                Assert.DoesNotContain(marker, artifactText, StringComparison.OrdinalIgnoreCase);
        }

        await using var connection = new SqliteConnection($"Data Source={combinedPath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "select count(*) from combined_facts where fact_type in ('AccessVbaModuleDeclared', 'AccessVbaProcedureDeclared', 'AccessNavigationCandidate', 'AccessEventBindingCandidate');";
        Assert.Equal(4L, Convert.ToInt64(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture));

        var catalog = await File.ReadAllTextAsync(Path.Combine(FindRepoRoot(), "rules", "rule-catalog.yml"));
        Assert.Contains($"id: {RuleIds.LegacyAccessVba}", catalog, StringComparison.Ordinal);
        Assert.Contains($"id: {RuleIds.LegacyAccessEventBinding}", catalog, StringComparison.Ordinal);
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, ".git")) && !File.Exists(Path.Combine(directory.FullName, ".git")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
