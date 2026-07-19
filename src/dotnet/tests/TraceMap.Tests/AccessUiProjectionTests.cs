using System.Text.Json;
using Microsoft.Data.Sqlite;
using TraceMap.Access;
using TraceMap.Access.Cli;
using TraceMap.Combine;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class AccessUiProjectionTests
{
    private const string ProtectedExpression = "=[CustomerId] & \"Password_DesignMarker_92817\"";
    private const string ProtectedEvent = "=Run(\"PrivateEventTarget_92817\")";
    private const string ProtectedControlName = "Credential Label 92817";
    private const string ProtectedFilter = "[CustomerId] > 0 AND [SecretFilter_92817] = 1";
    private const string ProtectedOrder = "[CustomerId], [SecretOrder_92817]";
    private const string ProtectedValidation = "[CustomerId] <> \"SecretValidation_92817\"";

    [Fact]
    public void Text_design_parser_stops_before_code_hashes_protected_design_and_balances_unsupported_property_blocks()
    {
        const string design = """"
            Version =20
            Begin Form
                RecordSource ="Customers"
                Filter ="[CustomerId] > 0 AND [SecretFilter_92817] = 1"
                OrderBy ="[CustomerId], [SecretOrder_92817]"
                Caption ="Private Caption 92817"
                HasModule = NotDefault
                OnOpen ="[Event Procedure]"
                Begin
                    Begin TextBox
                        Name ="CustomerIdControl"
                        ControlSource ="CustomerId"
                        ValidationRule ="[CustomerId] <> ""SecretValidation_92817"""
                        ValidationRule = Begin
                            0x5072697661746556616c7565
                        End
                        OnClick ="=Run(""PrivateEventTarget_92817"")"
                    End
                    Begin Label
                        Name ="Credential Label 92817"
                        Caption ="Password_DesignMarker_92817"
                    End
                End
            End
            CodeBehindForm
            Private Sub Form_Open()
                Password_DesignMarker_92817
            End Sub
            """";
        var parsed = AccessUiTextParser.Parse(new StringReader(design), "frmCustomers", "form");
        var surface = Assert.IsType<AccessRawUiSurface>(parsed.Surface);
        Assert.True(surface.HasModule);
        Assert.Equal("Customers", surface.RecordSource);
        Assert.Equal(ProtectedFilter, surface.Filter);
        Assert.Equal(ProtectedOrder, surface.OrderBy);
        Assert.Equal(2, surface.Controls.Count);
        Assert.Contains(parsed.Gaps, gap => gap.Classification == "AccessUiProtectedPropertyShapeUnsupported");

        var seed = AccessSafeValues.DatabaseIdentitySeed("repo", new string('c', 40), "fixture.accdb", "hash");
        var table = AccessSafeValues.Identity(seed, "table", "Customers");
        var field = AccessSafeValues.Identity(seed, $"field-{table.StableKey}", "CustomerId");
        var projected = AccessUiProjector.Project(
            seed,
            [surface],
            new Dictionary<string, IReadOnlyList<(string StableKey, string Kind)>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Customers"] = [(table.StableKey, "table")]
            },
            new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>>(StringComparer.Ordinal)
            {
                [table.StableKey] = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["CustomerId"] = [field.StableKey]
                }
            });
        var wire = JsonSerializer.Serialize(projected);
        Assert.DoesNotContain("Private Caption 92817", wire, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Credential Label 92817", wire, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Password_DesignMarker_92817", wire, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PrivateEventTarget_92817", wire, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SecretFilter_92817", wire, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SecretOrder_92817", wire, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SecretValidation_92817", wire, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(projected.Surfaces.Single().Bindings, binding => binding.BindingKind == "filter" && binding.ExpressionHash is not null);
        Assert.Contains(projected.Surfaces.Single().Bindings, binding => binding.BindingKind == "order-by" && binding.ExpressionHash is not null);
        Assert.Contains(projected.Surfaces.Single().Controls.Single(item => item.Ordinal == 0).Bindings,
            binding => binding.BindingKind == "validation-rule" && binding.ExpressionHash is not null);
        Assert.Equal("dynamic", projected.Surfaces.Single().Controls.Single(item => item.Ordinal == 0).Events.Single().Category);
    }

    [Fact]
    public void Text_design_parser_enforces_character_and_line_limits()
    {
        var limits = AccessLimits.Default with { MaxUiDesignTextLength = 20, MaxUiDesignLines = 2 };
        var exception = Assert.Throws<AccessScanException>(() => AccessUiTextParser.Parse(
            new StringReader("Begin Form\nRecordSource =\"Customers\"\nEnd\n"), "frmCustomers", "form", limits));
        Assert.Equal("AccessUiDesignTextLimitReached", exception.Classification);
    }

    [Fact]
    public void Documented_surface_inventory_keeps_objects_unloaded_and_discards_metadata_if_property_access_loads_one()
    {
        var seed = AccessSafeValues.DatabaseIdentitySeed("repo", new string('d', 40), "fixture.accdb", "hash");
        var stable = new FakeAccessObject("frmStable", new Dictionary<string, object?>
        {
            ["HasModule"] = false,
            ["RecordSource"] = "Customers",
            ["OnOpen"] = "[Event Procedure]"
        });
        var loadOnProperties = new FakeAccessObject("frmLoads", new Dictionary<string, object?>
        {
            ["RecordSource"] = "PrivateSource_92817"
        }, loadWhenPropertiesRead: true);
        var raw = new List<AccessRawUiSurface>();
        var gaps = new List<AccessGapProjection>();

        new AccessComReader().ReadSurfaceCollection(
            new FakeAccessCollection([stable, loadOnProperties]), "form", seed, raw, gaps);

        Assert.False(stable.IsLoaded);
        Assert.Equal("Customers", raw.Single(item => item.Name == "frmStable").RecordSource);
        var loaded = raw.Single(item => item.Name == "frmLoads");
        Assert.Null(loaded.RecordSource);
        Assert.Equal("inventory-only", loaded.Coverage);
        Assert.Contains(gaps, gap => gap.Classification == "AccessSurfaceMetadataCausedLoad");
        Assert.DoesNotContain("PrivateSource_92817", JsonSerializer.Serialize(raw), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Ui_evidence_persists_through_standard_and_combined_indexes_without_protected_design_values()
    {
        using var temp = new TempDirectory();
        var databasePath = Path.Combine(temp.Path, "fixture.accdb");
        await File.WriteAllBytesAsync(databasePath, [1, 2, 3, 4]);
        var databaseHash = AccessInputValidator.HashFile(databasePath);
        var output = Path.Combine(temp.Path, "access-output");
        var input = new AccessValidatedInput(
            temp.Path,
            "fixture-repo",
            AccessSafeValues.RoleHash("access-repository-identity", "fixture-repo"),
            null,
            "test",
            new string('e', 40),
            databasePath,
            "fixture.accdb",
            databaseHash,
            ".accdb",
            output,
            false);
        var seed = AccessSafeValues.DatabaseIdentitySeed(input.RepositoryIdentityHash, input.CommitSha, input.DatabaseRelativePath, input.DatabaseHash);
        var table = AccessSafeValues.Identity(seed, "table", "Customers");
        var field = AccessSafeValues.Identity(seed, $"field-{table.StableKey}", "CustomerId");
        var raw = new AccessRawUiSurface(
            "frmCustomers",
            "form",
            false,
            "Customers",
            [new(ProtectedControlName, 0, 109, ProtectedExpression, null, [new("on-click", ProtectedEvent)], ProtectedValidation)],
            [],
            Filter: ProtectedFilter,
            OrderBy: ProtectedOrder);
        var ui = AccessUiProjector.Project(
            seed,
            [raw],
            new Dictionary<string, IReadOnlyList<(string StableKey, string Kind)>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Customers"] = [(table.StableKey, "table")]
            },
            new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>>(StringComparer.Ordinal)
            {
                [table.StableKey] = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["CustomerId"] = [field.StableKey]
                }
            });
        var projection = new AccessDatabaseProjection(
            "tracemap.access-projection.v1",
            databaseHash,
            ".accdb",
            "16.0",
            1234,
            false,
            false,
            0,
            [new(table, [new(field, 0, "long", 4, true)], [])],
            [],
            [],
            [],
            ui.Gaps,
            [new("formsReports", "observed")],
            ui.Surfaces);
        var scan = AccessFactBuilder.Build(input, projection, new(temp.Path, "fixture.accdb", output));

        await AccessArtifactWriter.WriteAsync(output, scan, AccessLimits.Default);
        Assert.Contains(scan.Facts, fact => fact.FactType == FactTypes.AccessFormDeclared);
        Assert.Contains(scan.Facts, fact => fact.FactType == FactTypes.AccessControlDeclared);
        Assert.Contains(scan.Facts, fact => fact.FactType == FactTypes.AccessBindingDeclared);
        foreach (var file in Directory.EnumerateFiles(output, "*", SearchOption.AllDirectories))
        {
            var artifactText = System.Text.Encoding.UTF8.GetString(await File.ReadAllBytesAsync(file));
            Assert.DoesNotContain(ProtectedExpression, artifactText, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(ProtectedEvent, artifactText, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(ProtectedControlName, artifactText, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Password_DesignMarker_92817", artifactText, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("SecretFilter_92817", artifactText, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("SecretOrder_92817", artifactText, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("SecretValidation_92817", artifactText, StringComparison.OrdinalIgnoreCase);
        }

        var combined = Path.Combine(temp.Path, "combined.sqlite");
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([Path.Combine(output, "index.sqlite")], combined, ["access"]));
        await using var connection = new SqliteConnection($"Data Source={combined}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select count(*),
                   sum(case when rule_id = $surfaceRule then 1 else 0 end),
                   sum(case when rule_id = $bindingRule then 1 else 0 end)
            from combined_facts
            where fact_type in ('AccessFormDeclared', 'AccessControlDeclared', 'AccessBindingDeclared');
            """;
        command.Parameters.AddWithValue("$surfaceRule", RuleIds.LegacyAccessUiSurface);
        command.Parameters.AddWithValue("$bindingRule", RuleIds.LegacyAccessBinding);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.True(reader.GetInt64(0) >= 3);
        Assert.True(reader.GetInt64(1) >= 2);
        Assert.True(reader.GetInt64(2) >= 1);
        await reader.CloseAsync();

        await using var payloadCommand = connection.CreateCommand();
        payloadCommand.CommandText = "select payload_json from combined_facts;";
        await using var payloadReader = await payloadCommand.ExecuteReaderAsync();
        while (await payloadReader.ReadAsync())
        {
            var payload = payloadReader.GetString(0);
            Assert.DoesNotContain(ProtectedExpression, payload, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(ProtectedEvent, payload, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(ProtectedControlName, payload, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Ui_projector_resolves_direct_bindings_hashes_expressions_and_classifies_events_without_raw_values()
    {
        var seed = AccessSafeValues.DatabaseIdentitySeed("repo", new string('a', 40), "fixture.accdb", "database-hash");
        var table = AccessSafeValues.Identity(seed, "table", "Customers");
        var query = AccessSafeValues.Identity(seed, "query", "qryOrders");
        var field = AccessSafeValues.Identity(seed, $"field-{table.StableKey}", "CustomerId");
        var known = new Dictionary<string, IReadOnlyList<(string StableKey, string Kind)>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Customers"] = [(table.StableKey, "table")],
            ["qryOrders"] = [(query.StableKey, "query")]
        };
        var fields = new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>>(StringComparer.Ordinal)
        {
            [table.StableKey] = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["CustomerId"] = [field.StableKey]
            }
        };
        var raw = new AccessRawUiSurface(
            "frmCustomers",
            "form",
            true,
            "[Customers]",
            [
                new("txtCustomer", 0, 109, "[CustomerId]", null,
                    [new("on-click", "[Event Procedure]")]),
                new("cboOrders", 1, 111, null, "qryOrders",
                    [new("on-dbl-click", "[Embedded Macro]")]),
                new(ProtectedControlName, 2, 109, ProtectedExpression, null,
                    [new("after-update", ProtectedEvent)], ProtectedValidation)
            ],
            [new("on-open", "[Event Procedure]"), new("on-load", ProtectedEvent)],
            Filter: ProtectedFilter,
            OrderBy: ProtectedOrder);

        var result = AccessUiProjector.Project(seed, [raw], known, fields);

        var surface = Assert.Single(result.Surfaces);
        Assert.Equal("form", surface.SurfaceKind);
        Assert.Equal("present", surface.ModulePresence);
        Assert.Equal("bound-declared", surface.BoundState);
        Assert.Equal(table.StableKey, surface.Bindings.Single(binding => binding.BindingKind == "record-source").TargetStableKeys.Single());
        Assert.Equal([field.StableKey], surface.Bindings.Single(binding => binding.BindingKind == "filter").TargetStableKeys);
        Assert.Equal([field.StableKey], surface.Bindings.Single(binding => binding.BindingKind == "order-by").TargetStableKeys);
        Assert.Equal(["combo-box", "text-box", "text-box"], surface.Controls.Select(item => item.ControlType).OrderBy(item => item, StringComparer.Ordinal));
        Assert.Equal(field.StableKey, surface.Controls.Single(item => item.Ordinal == 0).Bindings.Single().TargetStableKeys.Single());
        Assert.Equal(query.StableKey, surface.Controls.Single(item => item.Ordinal == 1).Bindings.Single().TargetStableKeys.Single());
        var expression = surface.Controls.Single(item => item.Ordinal == 2).Bindings.Single(binding => binding.BindingKind == "control-source");
        Assert.Equal("expression", expression.SourceKind);
        Assert.Equal("partial", expression.Coverage);
        Assert.Equal([field.StableKey], expression.TargetStableKeys);
        var validation = surface.Controls.Single(item => item.Ordinal == 2).Bindings.Single(binding => binding.BindingKind == "validation-rule");
        Assert.Equal("expression", validation.SourceKind);
        Assert.Equal([field.StableKey], validation.TargetStableKeys);
        Assert.Contains(result.Gaps, gap => gap.Classification == "AccessBindingExpressionPartial" && gap.RuleId == RuleIds.LegacyAccessBinding);
        Assert.Equal("event-procedure", surface.Events.Single(item => item.EventRole == "on-open").Category);
        Assert.Equal("dynamic", surface.Events.Single(item => item.EventRole == "on-load").Category);
        Assert.Equal("embedded-macro", surface.Controls.Single(item => item.Ordinal == 1).Events.Single().Category);

        var wire = JsonSerializer.Serialize(result.Surfaces);
        Assert.DoesNotContain(ProtectedExpression, wire, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(ProtectedEvent, wire, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(ProtectedControlName, wire, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Password_DesignMarker_92817", wire, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SecretFilter_92817", wire, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SecretOrder_92817", wire, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SecretValidation_92817", wire, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Ui_projection_and_fact_output_are_deterministic_rule_backed_and_preserve_ambiguity_as_a_gap()
    {
        using var temp = new TempDirectory();
        var databasePath = Path.Combine(temp.Path, "fixture.accdb");
        File.WriteAllBytes(databasePath, [1, 2, 3, 4]);
        var hash = AccessInputValidator.HashFile(databasePath);
        var input = new AccessValidatedInput(
            temp.Path,
            "fixture-repo",
            AccessSafeValues.RoleHash("access-repository-identity", "fixture-repo"),
            null,
            "test",
            new string('b', 40),
            databasePath,
            "fixture.accdb",
            hash,
            ".accdb",
            Path.Combine(temp.Path, "out"),
            false);
        var seed = AccessSafeValues.DatabaseIdentitySeed(input.RepositoryIdentityHash, input.CommitSha, input.DatabaseRelativePath, input.DatabaseHash);
        var first = AccessSafeValues.Identity(seed, "table", "Orders");
        var second = AccessSafeValues.Identity(seed, "query", "Orders");
        var known = new Dictionary<string, IReadOnlyList<(string StableKey, string Kind)>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Orders"] = [(first.StableKey, "table"), (second.StableKey, "query")]
        };
        var form = new AccessRawUiSurface("frmOrders", "form", false, "Orders", [], [new("on-open", string.Empty)]);
        var report = new AccessRawUiSurface("rptOrders", "report", null, null, [], []);

        var projectedA = AccessUiProjector.Project(seed, [form, report], known,
            new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>>(StringComparer.Ordinal));
        var projectedB = AccessUiProjector.Project(seed, [report, form], known,
            new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>>(StringComparer.Ordinal));
        Assert.Equal(
            JsonSerializer.Serialize(projectedA),
            JsonSerializer.Serialize(projectedB));
        Assert.Contains(projectedA.Gaps, gap => gap.Classification == "AccessBindingTargetAmbiguous");

        var databaseProjection = new AccessDatabaseProjection(
            "tracemap.access-projection.v1",
            hash,
            ".accdb",
            "16.0",
            1234,
            false,
            false,
            0,
            [],
            [],
            [],
            [],
            projectedA.Gaps,
            [new("formsReports", "observed")],
            projectedA.Surfaces);
        var scan = AccessFactBuilder.Build(input, databaseProjection, new(temp.Path, "fixture.accdb", input.OutputFullPath));

        Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.AccessFormDeclared && fact.RuleId == RuleIds.LegacyAccessUiSurface);
        Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.AccessReportDeclared && fact.RuleId == RuleIds.LegacyAccessUiSurface);
        Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.AccessBindingDeclared && fact.RuleId == RuleIds.LegacyAccessBinding);
        Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyAccessBinding
            && fact.Properties.GetValueOrDefault("classification") == "AccessBindingTargetAmbiguous");
        Assert.DoesNotContain("Orders", JsonSerializer.Serialize(scan.Facts.Where(fact => fact.FactType == FactTypes.AccessBindingDeclared)), StringComparison.Ordinal);

        var catalog = File.ReadAllText(Path.Combine(FindRepoRoot(), "rules", "rule-catalog.yml"));
        Assert.Contains($"id: {RuleIds.LegacyAccessUiSurface}", catalog, StringComparison.Ordinal);
        Assert.Contains($"id: {RuleIds.LegacyAccessBinding}", catalog, StringComparison.Ordinal);
        Assert.Contains("does not prove rendering", catalog, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Raw record sources", catalog, StringComparison.Ordinal);
    }

    private static string FindRepoRoot()
    {
        var current = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(current))
        {
            if (File.Exists(Path.Combine(current, "rules", "rule-catalog.yml"))) return current;
            current = Directory.GetParent(current)?.FullName ?? string.Empty;
        }
        throw new DirectoryNotFoundException("Repository root unavailable.");
    }

    public sealed class FakeAccessCollection(IReadOnlyList<FakeAccessObject> items)
    {
        public int Count => items.Count;
        public FakeAccessObject this[int index] => items[index];
    }

    public sealed class FakeAccessObject(string name, IReadOnlyDictionary<string, object?> values, bool loadWhenPropertiesRead = false)
    {
        public string Name { get; } = name;
        public bool IsLoaded { get; private set; }
        public FakeAccessProperties Properties
        {
            get
            {
                if (loadWhenPropertiesRead) IsLoaded = true;
                return new(values);
            }
        }
    }

    public sealed class FakeAccessProperties(IReadOnlyDictionary<string, object?> values)
    {
        public FakeAccessProperty this[string name] => values.TryGetValue(name, out var value)
            ? new(value)
            : throw new KeyNotFoundException();
    }

    public sealed class FakeAccessProperty(object? value)
    {
        public object? Value { get; } = value;
    }
}
