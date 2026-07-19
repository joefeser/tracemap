using System.Text.Json;
using TraceMap.Access;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class AccessUiProjectionTests
{
    private const string ProtectedExpression = "=[CustomerId] & \"Password_DesignMarker_92817\"";
    private const string ProtectedEvent = "=Run(\"PrivateEventTarget_92817\")";
    private const string ProtectedControlName = "Credential Label 92817";

    [Fact]
    public void Text_design_parser_stops_before_code_hashes_protected_design_and_balances_unsupported_property_blocks()
    {
        const string design = """
            Version =20
            Begin Form
                RecordSource ="Customers"
                Caption ="Private Caption 92817"
                HasModule = NotDefault
                OnOpen ="[Event Procedure]"
                Begin
                    Begin TextBox
                        Name ="CustomerIdControl"
                        ControlSource ="CustomerId"
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
            """;
        var parsed = AccessUiTextParser.Parse(new StringReader(design), "frmCustomers", "form");
        var surface = Assert.IsType<AccessRawUiSurface>(parsed.Surface);
        Assert.True(surface.HasModule);
        Assert.Equal("Customers", surface.RecordSource);
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
                    [new("after-update", ProtectedEvent)])
            ],
            [new("on-open", "[Event Procedure]"), new("on-load", ProtectedEvent)]);

        var result = AccessUiProjector.Project(seed, [raw], known, fields);

        var surface = Assert.Single(result.Surfaces);
        Assert.Equal("form", surface.SurfaceKind);
        Assert.Equal("present", surface.ModulePresence);
        Assert.Equal("bound-declared", surface.BoundState);
        Assert.Equal(table.StableKey, Assert.Single(surface.Bindings).TargetStableKeys.Single());
        Assert.Equal(["combo-box", "text-box", "text-box"], surface.Controls.Select(item => item.ControlType).OrderBy(item => item, StringComparer.Ordinal));
        Assert.Equal(field.StableKey, surface.Controls.Single(item => item.Ordinal == 0).Bindings.Single().TargetStableKeys.Single());
        Assert.Equal(query.StableKey, surface.Controls.Single(item => item.Ordinal == 1).Bindings.Single().TargetStableKeys.Single());
        var expression = surface.Controls.Single(item => item.Ordinal == 2).Bindings.Single();
        Assert.Equal("expression", expression.SourceKind);
        Assert.Equal("partial", expression.Coverage);
        Assert.Equal([field.StableKey], expression.TargetStableKeys);
        Assert.Contains(result.Gaps, gap => gap.Classification == "AccessBindingExpressionPartial" && gap.RuleId == RuleIds.LegacyAccessBinding);
        Assert.Equal("event-procedure", surface.Events.Single(item => item.EventRole == "on-open").Category);
        Assert.Equal("dynamic", surface.Events.Single(item => item.EventRole == "on-load").Category);
        Assert.Equal("embedded-macro", surface.Controls.Single(item => item.Ordinal == 1).Events.Single().Category);

        var wire = JsonSerializer.Serialize(result.Surfaces);
        Assert.DoesNotContain(ProtectedExpression, wire, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(ProtectedEvent, wire, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(ProtectedControlName, wire, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Password_DesignMarker_92817", wire, StringComparison.OrdinalIgnoreCase);
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
}
