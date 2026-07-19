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
    public void Product_vba_inventory_reads_only_counts_and_never_accesses_vbe_or_catalog_items()
    {
        var catalog = new FakeCountCollection(4);
        var application = new FakeVbaApplication(new FakeVbaProject(catalog), [0, 0]);
        var gaps = new List<AccessGapProjection>();

        var inventory = new AccessComReader().ReadVbaInventoryCounts(application, gaps);

        Assert.Equal(4, inventory.ModuleCount);
        Assert.True(inventory.LoadedModuleCountUnchanged);
        Assert.Equal("count-observed-source-unavailable", inventory.Coverage);
        Assert.Equal(0, catalog.IndexerReadCount);
        Assert.Equal(0, application.VbeReadCount);
        Assert.Equal(2, application.LoadedModulesReadCount);
        Assert.Single(gaps, gap => gap.Classification == "AccessVbaProjectUnavailable"
            && gap.RuleId == RuleIds.LegacyAccessVba);
    }

    [Fact]
    public void Product_vba_inventory_records_gap_when_loaded_module_count_changes()
    {
        var application = new FakeVbaApplication(
            new FakeVbaProject(new FakeCountCollection(1)), [0, 1]);
        var gaps = new List<AccessGapProjection>();

        var inventory = new AccessComReader().ReadVbaInventoryCounts(application, gaps);

        Assert.False(inventory.LoadedModuleCountUnchanged);
        Assert.Equal("count-observed-source-unavailable-canary-changed", inventory.Coverage);
        Assert.Contains(gaps, gap => gap.Classification == "AccessVbaModuleStateChanged"
            && gap.ScopeKind == "vba-project"
            && gap.RuleId == RuleIds.LegacyAccessVba);
        Assert.Equal(0, application.VbeReadCount);
    }

    [Fact]
    public void Product_vba_inventory_records_gap_when_module_catalog_count_is_unavailable()
    {
        var application = new ThrowingVbaApplication([0, 0]);
        var gaps = new List<AccessGapProjection>();

        var inventory = new AccessComReader().ReadVbaInventoryCounts(application, gaps);

        Assert.Null(inventory.ModuleCount);
        Assert.True(inventory.LoadedModuleCountUnchanged);
        Assert.Equal("count-unavailable-source-unavailable", inventory.Coverage);
        Assert.Contains(gaps, gap => gap.Classification == "AccessVbaModuleCatalogUnavailable"
            && gap.ScopeKind == "vba-project"
            && gap.RuleId == RuleIds.LegacyAccessVba);
    }

    [Fact]
    public async Task Count_only_vba_inventory_emits_metadata_and_gap_without_identity_or_flow_facts()
    {
        using var temp = new TempDirectory();
        var databasePath = Path.Combine(temp.Path, "fixture.accdb");
        await File.WriteAllBytesAsync(databasePath, [1, 2, 3, 4]);
        var databaseHash = AccessInputValidator.HashFile(databasePath);
        var input = new AccessValidatedInput(
            temp.Path, "repo", AccessSafeValues.RoleHash("access-repository-identity", "repo"), null, "test",
            new string('e', 40), databasePath, "fixture.accdb", databaseHash, ".accdb", Path.Combine(temp.Path, "out"), false);
        var inventory = new AccessVbaInventoryProjection(3, true, "count-observed-source-unavailable");
        var projection = new AccessDatabaseProjection(
            "tracemap.access-projection.v1", databaseHash, ".accdb", "16.0", 1234, false, false, 0,
            [], [], [], [],
            [new("AccessVbaProjectUnavailable", "vba-project", null, RuleIds.LegacyAccessVba)],
            [new("vbaModules", inventory.Coverage)],
            VbaModules: [], EventBindings: [], VbaInventory: inventory);

        var scan = AccessFactBuilder.Build(input, projection, new(temp.Path, "fixture.accdb", input.OutputFullPath));

        var databaseFact = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.LegacyDataMetadataDeclared);
        Assert.Equal("3", databaseFact.Properties.GetValueOrDefault("vbaModuleCount"));
        Assert.Equal("true", databaseFact.Properties.GetValueOrDefault("vbaLoadedModuleCountUnchanged"));
        Assert.Equal("count-observed-source-unavailable", databaseFact.Properties.GetValueOrDefault("vbaCoverage"));
        Assert.DoesNotContain(scan.Facts, fact => fact.FactType is FactTypes.AccessVbaModuleDeclared
            or FactTypes.AccessVbaProcedureDeclared
            or FactTypes.AccessNavigationCandidate
            or FactTypes.AccessEventBindingCandidate);
        Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyAccessVba
            && fact.Properties.GetValueOrDefault("classification") == "AccessVbaProjectUnavailable");
    }

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
    public void Projector_ends_arguments_at_statement_separator_and_rejects_wrong_kind_catalog_targets()
    {
        const string source = """
            Private Sub EntryPoint()
                DoCmd.OpenForm "frmCustomers": Call Helper
                DoCmd.OpenForm "qryOrders"
            End Sub

            Private Sub Helper()
            End Sub
            """;
        var seed = AccessSafeValues.DatabaseIdentitySeed("repo", new string('f', 40), "fixture.accdb", "hash");
        var form = AccessSafeValues.Identity(seed, "form", "frmCustomers");
        var query = AccessSafeValues.Identity(seed, "query", "qryOrders");
        var known = new Dictionary<string, IReadOnlyList<(string StableKey, string Kind)>>(StringComparer.OrdinalIgnoreCase)
        {
            ["frmCustomers"] = [(form.StableKey, "form")],
            ["qryOrders"] = [(query.StableKey, "query")]
        };

        var result = AccessVbaProjector.Project(seed, [new("ModuleA", "standard", source)], knownObjects: known);
        var calls = result.Modules.Single().Procedures
            .Single(procedure => procedure.Identity.DisplayName == "EntryPoint").Calls;

        Assert.Contains(calls, call => call.CallKind == "open-form"
            && call.TargetStableKey == form.StableKey
            && call.Coverage == "complete");
        Assert.Contains(calls, call => call.CallKind == "local-procedure-call"
            && call.TargetStableKey is not null);
        var wrongKind = Assert.Single(calls, call => call.LiteralTargetIdentity?.DisplayName == "qryOrders");
        Assert.Equal("open-form", wrongKind.CallKind);
        Assert.Equal("form", wrongKind.TargetKind);
        Assert.Null(wrongKind.TargetStableKey);
        Assert.Equal("partial", wrongKind.Coverage);
        Assert.Contains(result.Gaps, gap => gap.Classification == "AccessVbaLiteralTargetUnresolved"
            && gap.StableScopeKey == wrongKind.Identity.StableKey);
    }

    [Fact]
    public void Projector_scopes_missing_procedure_terminator_gaps_to_distinct_procedures()
    {
        const string source = """
            Private Sub First()
                Call Helper
            Private Sub Second()
                Call Helper
            """;
        var seed = AccessSafeValues.DatabaseIdentitySeed("repo", new string('1', 40), "fixture.accdb", "hash");

        var result = AccessVbaProjector.Project(seed, [new("ModuleA", "standard", source)]);

        var procedureKeys = result.Modules.Single().Procedures
            .Select(procedure => procedure.Identity.StableKey)
            .OfType<string>()
            .ToHashSet();
        var gaps = result.Gaps.Where(gap => gap.Classification == "AccessVbaProcedureEndUnavailable").ToArray();
        Assert.Equal(2, gaps.Length);
        Assert.All(gaps, gap => Assert.Contains(Assert.IsType<string>(gap.StableScopeKey), procedureKeys));
        Assert.Equal(2, gaps.Select(gap => gap.StableScopeKey).Distinct().Count());
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
    public void Call_limit_gap_is_emitted_only_when_a_call_is_actually_omitted()
    {
        const string exactSource = """
            Public Sub EntryPoint()
                Call Helper
            End Sub
            Public Sub Helper()
            End Sub
            """;
        const string overflowSource = """
            Public Sub EntryPoint()
                Call Helper
                Call Helper
            End Sub
            Public Sub Helper()
            End Sub
            """;
        var seed = AccessSafeValues.DatabaseIdentitySeed("repo", new string('d', 40), "fixture.accdb", "hash");
        var limits = AccessLimits.Default with { MaxVbaCallsPerProcedure = 1 };

        var exact = AccessVbaProjector.Project(seed, [new("ModuleA", "standard", exactSource)], limits: limits);
        Assert.Single(exact.Modules.Single().Procedures.Single(procedure => procedure.Identity.DisplayName == "EntryPoint").Calls);
        Assert.DoesNotContain(exact.Gaps, gap => gap.Classification == "AccessVbaCallLimitReached");

        var overflow = AccessVbaProjector.Project(seed, [new("ModuleA", "standard", overflowSource)], limits: limits);
        Assert.Single(overflow.Modules.Single().Procedures.Single(procedure => procedure.Identity.DisplayName == "EntryPoint").Calls);
        Assert.Single(overflow.Gaps, gap => gap.Classification == "AccessVbaCallLimitReached");
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

        // Clear the SQLite connection pool before reading files as bytes.
        // On Windows, pooled connections hold a file lock on the .sqlite even
        // after the logical connection is closed, causing IOException when
        // File.ReadAllBytesAsync tries to open the same file.
        SqliteConnection.ClearAllPools();

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

        // Open a fresh connection after the pool is cleared for the fact-count query.
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

    public sealed class FakeVbaApplication(FakeVbaProject project, IReadOnlyList<int> loadedModuleCounts)
    {
        private int _loadedModuleReadIndex;
        public FakeVbaProject CurrentProject => project;
        public int LoadedModulesReadCount => _loadedModuleReadIndex;
        public int VbeReadCount { get; private set; }
        public FakeCountCollection Modules
        {
            get
            {
                var index = Math.Min(_loadedModuleReadIndex, loadedModuleCounts.Count - 1);
                _loadedModuleReadIndex++;
                return new(loadedModuleCounts[index]);
            }
        }
        public object VBE
        {
            get
            {
                VbeReadCount++;
                throw new InvalidOperationException("VBE access is forbidden for v0.");
            }
        }
    }

    public sealed class FakeVbaProject(FakeCountCollection modules)
    {
        public FakeCountCollection AllModules => modules;
    }

    public sealed class ThrowingVbaApplication(IReadOnlyList<int> loadedModuleCounts)
    {
        private int _loadedModuleReadIndex;
        public object CurrentProject => throw new InvalidOperationException("Module catalog unavailable.");
        public FakeCountCollection Modules
        {
            get
            {
                var index = Math.Min(_loadedModuleReadIndex, loadedModuleCounts.Count - 1);
                _loadedModuleReadIndex++;
                return new(loadedModuleCounts[index]);
            }
        }
    }

    public sealed class FakeCountCollection(int count)
    {
        public int Count => count;
        public int IndexerReadCount { get; private set; }
        public object this[int index]
        {
            get
            {
                IndexerReadCount++;
                throw new InvalidOperationException("Collection item access is forbidden for v0.");
            }
        }
    }
}
