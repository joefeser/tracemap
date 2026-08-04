using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using TraceMap.Access;
using TraceMap.Access.Cli;
using TraceMap.Core;

namespace TraceMap.Tests;

[Collection("AccessGitEnvironment")]
public sealed class AccessFoundationTests
{
    [Fact]
    public void Design_composer_reconciles_only_unique_hash_only_query_outputs_named_by_direct_controls()
    {
        const string queryStableKey = "access-query-1";
        const string outputStableKey = "access-query-output-1";
        const string outputName = "Total Of NSAPoints";
        var surface = new AccessRawUiSurface(
            "ReportOne",
            "report",
            false,
            "[Pivot Query]",
            [
                new AccessRawControl("direct", 0, 109, $"[{outputName}]", null, []),
                new AccessRawControl("expression", 1, 109, $"=Sum([{outputName}])", null, [])
            ],
            []);
        var knownObjects = new Dictionary<string, List<(string StableKey, string Kind)>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Pivot Query"] = [(queryStableKey, "query")]
        };
        var outputHash = AccessSafeValues.RoleHash($"access-query-field-{queryStableKey}-name", outputName);
        var fieldsByHash = new Dictionary<string, Dictionary<string, List<string>>>(StringComparer.Ordinal)
        {
            [queryStableKey] = new(StringComparer.Ordinal) { [outputHash] = [outputStableKey] }
        };
        var fieldsByName = new Dictionary<string, Dictionary<string, List<string>>>(StringComparer.Ordinal);

        AccessDesignEvidenceComposer.ReconcileSurfaceQueryOutputNames(
            [surface], knownObjects, fieldsByHash, fieldsByName);

        Assert.Equal(outputStableKey, Assert.Single(fieldsByName[queryStableKey][outputName]));

        fieldsByName.Clear();
        AccessDesignEvidenceComposer.ReconcileSurfaceQueryOutputNames(
            [surface with { SurfaceKind = "form" }], knownObjects, fieldsByHash, fieldsByName);
        Assert.Empty(fieldsByName);

        fieldsByHash[queryStableKey][outputHash].Add("access-query-output-ambiguous");
        fieldsByName.Clear();
        AccessDesignEvidenceComposer.ReconcileSurfaceQueryOutputNames(
            [surface], knownObjects, fieldsByHash, fieldsByName);
        Assert.Empty(fieldsByName);
    }

    [Fact]
    public void Design_composer_reconciles_only_unique_hash_only_domain_outputs_named_by_expressions()
    {
        const string queryStableKey = "access-query-domain";
        const string selectedStableKey = "access-query-output-percent";
        const string criteriaStableKey = "access-query-output-weekly-plan";
        var surface = new AccessRawUiSurface(
            "FormOne",
            "form",
            true,
            null,
            [
                new AccessRawControl(
                    "calculated",
                    0,
                    109,
                    "=DLookUp(\"[Percent]\",\"qWeekly\",\"[WeeklyPlanID]=[txtWeeklyPlanID]\")",
                    null,
                    [])
            ],
            []);
        var knownObjects = new Dictionary<string, List<(string StableKey, string Kind)>>(StringComparer.OrdinalIgnoreCase)
        {
            ["qWeekly"] = [(queryStableKey, "query")]
        };
        var fieldsByHash = new Dictionary<string, Dictionary<string, List<string>>>(StringComparer.Ordinal)
        {
            [queryStableKey] = new(StringComparer.Ordinal)
            {
                [AccessSafeValues.RoleHash($"access-query-field-{queryStableKey}-name", "Percent")] = [selectedStableKey],
                [AccessSafeValues.RoleHash($"access-query-field-{queryStableKey}-name", "WeeklyPlanID")] = [criteriaStableKey]
            }
        };
        var fieldsByName = new Dictionary<string, Dictionary<string, List<string>>>(StringComparer.Ordinal);
        var criteriaFieldsByName = new Dictionary<string, Dictionary<string, List<string>>>(StringComparer.Ordinal);

        AccessDesignEvidenceComposer.ReconcileDomainExpressionQueryOutputNames(
            "database-seed", [surface], knownObjects, fieldsByHash, fieldsByName, criteriaFieldsByName);

        Assert.Equal(selectedStableKey, Assert.Single(fieldsByName[queryStableKey]["Percent"]));
        Assert.False(fieldsByName[queryStableKey].ContainsKey("WeeklyPlanID"));
        Assert.Equal(criteriaStableKey, Assert.Single(criteriaFieldsByName[queryStableKey]["WeeklyPlanID"]));
        Assert.False(fieldsByName[queryStableKey].ContainsKey("txtWeeklyPlanID"));

        fieldsByHash[queryStableKey][AccessSafeValues.RoleHash(
            $"access-query-field-{queryStableKey}-name", "WeeklyPlanID")].Add("ambiguous-output");
        fieldsByName.Clear();
        criteriaFieldsByName.Clear();
        AccessDesignEvidenceComposer.ReconcileDomainExpressionQueryOutputNames(
            "database-seed", [surface], knownObjects, fieldsByHash, fieldsByName, criteriaFieldsByName);
        Assert.False(criteriaFieldsByName.ContainsKey(queryStableKey));
    }

    [Fact]
    public void Design_composer_reconciles_only_exact_declared_crosstab_pivot_headings_as_candidates()
    {
        const string databaseSeed = "database-seed";
        const string queryStableKey = "access-query-pivot";
        var surface = new AccessRawUiSurface(
            "ReportOne",
            "report",
            false,
            null,
            [new AccessRawControl("week", 0, 109, "=DLookUp(\"[4]\",\"qPivot\")", null, [])],
            []);
        var knownObjects = new Dictionary<string, List<(string StableKey, string Kind)>>(StringComparer.OrdinalIgnoreCase)
        {
            ["qPivot"] = [(queryStableKey, "query")]
        };
        var fieldsByHash = new Dictionary<string, Dictionary<string, List<string>>>(StringComparer.Ordinal)
        {
            [queryStableKey] = new(StringComparer.Ordinal)
        };
        var fieldsByName = new Dictionary<string, Dictionary<string, List<string>>>(StringComparer.Ordinal);
        var criteriaFieldsByName = new Dictionary<string, Dictionary<string, List<string>>>(StringComparer.Ordinal);
        var pivotHashes = new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
        {
            [queryStableKey] = new HashSet<string>(StringComparer.Ordinal)
            {
                AccessSafeValues.RoleHash("access-query-pivot-column", "4")
            }
        };

        AccessDesignEvidenceComposer.ReconcileDomainExpressionQueryOutputNames(
            databaseSeed,
            [surface],
            knownObjects,
            fieldsByHash,
            fieldsByName,
            criteriaFieldsByName,
            pivotHashes);

        var candidate = Assert.Single(fieldsByName[queryStableKey]["4"]);
        Assert.True(AccessSafeValues.IsCrosstabPivotColumnCandidate(candidate));

        fieldsByName.Clear();
        fieldsByName[queryStableKey] = new(StringComparer.OrdinalIgnoreCase)
        {
            ["W5"] = ["declared-week-five"]
        };
        pivotHashes[queryStableKey] = new HashSet<string>(StringComparer.Ordinal)
        {
            AccessSafeValues.RoleHash("access-query-pivot-column", "W5")
        };
        AccessDesignEvidenceComposer.ReconcileDomainExpressionQueryOutputNames(
            databaseSeed,
            [surface with
            {
                Controls = [new AccessRawControl("week", 0, 109, "=DLookUp(\"[5]\",\"qPivot\")", null, [])]
            }],
            knownObjects,
            fieldsByHash,
            fieldsByName,
            criteriaFieldsByName,
            pivotHashes);
        Assert.True(AccessSafeValues.IsCrosstabPivotPrefixMismatchCandidate(
            Assert.Single(fieldsByName[queryStableKey]["5"])));

        fieldsByName.Clear();
        AccessDesignEvidenceComposer.ReconcileDomainExpressionQueryOutputNames(
            databaseSeed,
            [surface with
            {
                Controls = [new AccessRawControl("week", 0, 109, "=DLookUp(\"[6]\",\"qPivot\")", null, [])]
            }],
            knownObjects,
            fieldsByHash,
            fieldsByName,
            criteriaFieldsByName,
            pivotHashes);
        Assert.Empty(fieldsByName);
    }

    [Fact]
    public void Design_composer_criteria_scope_flows_to_ui_without_promoting_criteria_to_return_output()
    {
        const string queryStableKey = "query-domain";
        const string selectedStableKey = "output-percent";
        const string criteriaStableKey = "table-weekly-plan";
        var fields = new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>>(StringComparer.Ordinal)
        {
            [queryStableKey] = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Percent"] = [selectedStableKey]
            },
            ["table-source"] = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["WeeklyPlanID"] = [criteriaStableKey]
            }
        };
        var facts = new[]
        {
            QueryDeclarationFact(queryStableKey, "complete"),
            DependencyFact("dependency-table", queryStableKey, "table-source", "table")
        };
        var criteriaScopes = AccessDesignEvidenceComposer.BuildDomainCriteriaFieldSets(facts, fields);
        var surface = new AccessRawUiSurface(
            "frmWeekly",
            "form",
            true,
            null,
            [
                new AccessRawControl("txtPlan", 0, 109, null, null, []),
                new AccessRawControl(
                    "txtPercent",
                    1,
                    109,
                    "=DLookUp(\"[Percent]\",\"qWeekly\",\"[WeeklyPlanID]=[txtPlan]\")",
                    null,
                    [])
            ],
            []);
        var known = new Dictionary<string, IReadOnlyList<(string StableKey, string Kind)>>(StringComparer.OrdinalIgnoreCase)
        {
            ["qWeekly"] = [(queryStableKey, "query")]
        };

        var projected = AccessUiProjector.Project(
            "synthetic-database",
            [surface],
            known,
            fields,
            domainCriteriaFieldSetsByObject: criteriaScopes);
        var binding = Assert.Single(
            Assert.Single(projected.Surfaces).Controls.Single(control => control.Identity.DisplayName == "txtPercent").Bindings);

        Assert.Equal("complete", binding.Coverage);
        Assert.Equal("partial", binding.RuntimeValueCoverage);
        Assert.Equal([selectedStableKey], binding.Expression!.SelectedFieldStableKeys);
        Assert.Equal([criteriaStableKey], binding.Expression.CriteriaFieldStableKeys);
        Assert.DoesNotContain(criteriaStableKey, binding.Expression.SelectedFieldStableKeys);

        var returnFromCriteriaOnly = AccessUiProjector.Project(
            "synthetic-database",
            [surface with
            {
                Controls =
                [
                    new AccessRawControl(
                        "txtCriteriaAsReturn",
                        0,
                        109,
                        "=DLookUp(\"[WeeklyPlanID]\",\"qWeekly\")",
                        null,
                        [])
                ]
            }],
            known,
            fields,
            domainCriteriaFieldSetsByObject: criteriaScopes);
        var returnBinding = Assert.Single(Assert.Single(returnFromCriteriaOnly.Surfaces).Controls).Bindings.Single();
        Assert.Equal("partial", returnBinding.Coverage);
        Assert.Equal("AccessBindingDomainSelectedFieldDependencyOnly", returnBinding.Expression!.GapClassification);
        Assert.Empty(returnBinding.Expression.SelectedFieldStableKeys);
    }

    [Fact]
    public void Design_composer_builds_bounded_domain_criteria_scopes_from_direct_dependencies()
    {
        var fields = new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>>(StringComparer.Ordinal)
        {
            ["query-domain"] = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Percent"] = ["output-percent"]
            },
            ["query-source"] = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["WeeklyPlanID"] = ["query-weekly-plan"]
            },
            ["table-source"] = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["StartDate"] = ["table-start-date"],
                ["WeeklyPlanID"] = ["table-weekly-plan"]
            },
            ["unrelated"] = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Secret"] = ["unrelated-secret"]
            }
        };
        var dependencies = new[]
        {
            QueryDeclarationFact("query-domain", "complete"),
            DependencyFact("dependency-query", "query-domain", "query-source", "query"),
            DependencyFact("dependency-table", "query-domain", "table-source", "table")
        };

        var scopes = AccessDesignEvidenceComposer.BuildDomainCriteriaFieldSets(dependencies, fields);

        Assert.Equal(["output-percent"], scopes["query-domain"]["Percent"]);
        Assert.Equal(["table-start-date"], scopes["query-domain"]["StartDate"]);
        Assert.Equal(2, scopes["query-domain"]["WeeklyPlanID"].Count);
        Assert.False(scopes["query-domain"].ContainsKey("Secret"));

        var partialScopes = AccessDesignEvidenceComposer.BuildDomainCriteriaFieldSets(
            dependencies.Skip(1).Append(QueryDeclarationFact("query-domain", "partial")).ToArray(),
            fields);
        Assert.Equal(["output-percent"], partialScopes["query-domain"]["Percent"]);
        Assert.False(partialScopes["query-domain"].ContainsKey("StartDate"));

    }

    [Fact]
    public void Design_composer_marks_table_field_catalog_complete_only_without_acquisition_gaps()
    {
        var tables = new[]
        {
            EntityFact("table-complete"),
            EntityFact("table-field-gap"),
            EntityFact("table-no-fields")
        };
        var withScopedGap = tables.Append(GapFact(
            "AccessObjectMetadataUnavailable",
            "field",
            "table-field-gap")).ToArray();

        var complete = AccessDesignEvidenceComposer.BuildCompleteTableFieldCatalogStableKeys(
            withScopedGap,
            new HashSet<string>(["table-complete", "table-field-gap"], StringComparer.Ordinal));
        var disabledByGlobalGap = AccessDesignEvidenceComposer.BuildCompleteTableFieldCatalogStableKeys(
            withScopedGap.Append(GapFact("AccessFieldCollectionLimit", "table", null)).ToArray(),
            new HashSet<string>(["table-complete", "table-field-gap"], StringComparer.Ordinal));

        Assert.Equal(["table-complete"], complete);
        Assert.Empty(disabledByGlobalGap);
    }

    private static CodeFact QueryDeclarationFact(string target, string referenceCoverage) => new(
        "declaration-" + referenceCoverage,
        "scan-access",
        "synthetic",
        new string('a', 40),
        null,
        FactTypes.AccessQueryDeclared,
        RuleIds.LegacyAccessQuery,
        EvidenceTiers.Tier2Structural,
        null,
        target,
        null,
        new EvidenceSpan("database.accdb", 1, 1, null, "access-query", "1.0.0"),
            new Dictionary<string, string> { ["referenceCoverage"] = referenceCoverage });

    private static CodeFact EntityFact(string target) => new(
        "entity-" + target,
        "scan-access",
        "synthetic",
        new string('a', 40),
        null,
        FactTypes.LegacyDataEntityDeclared,
        RuleIds.LegacyAccessSchema,
        EvidenceTiers.Tier2Structural,
        null,
        target,
        null,
        new EvidenceSpan("database.accdb", 1, 1, null, "access-schema", "1.0.0"),
        new Dictionary<string, string>());

    private static CodeFact GapFact(string classification, string scopeKind, string? target) => new(
        "gap-" + classification + "-" + (target ?? "global"),
        "scan-access",
        "synthetic",
        new string('a', 40),
        null,
        FactTypes.AnalysisGap,
        RuleIds.LegacyAccessCoverageGap,
        EvidenceTiers.Tier4Unknown,
        null,
        target,
        null,
        new EvidenceSpan("database.accdb", 1, 1, null, "access-gap", "1.0.0"),
        new Dictionary<string, string>
        {
            ["classification"] = classification,
            ["scopeKind"] = scopeKind
        });

    private static CodeFact DependencyFact(
        string factId,
        string source,
        string target,
        string targetKind) => new(
            factId,
            "scan-access",
            "synthetic",
            new string('a', 40),
            null,
            FactTypes.AccessQueryDependencyCandidate,
            RuleIds.LegacyAccessQuery,
            EvidenceTiers.Tier3SyntaxOrTextual,
            source,
            target,
            null,
            new EvidenceSpan("database.accdb", 1, 1, null, "access-query", "1.0.0"),
            new Dictionary<string, string> { ["targetKind"] = targetKind });

    private static CodeFact CrosstabLineageFact(string source, string staticColumnHashes) => new(
        "crosstab-" + source,
        "scan-access",
        "synthetic",
        new string('a', 40),
        null,
        FactTypes.AccessQueryCrosstabLineageCandidate,
        RuleIds.LegacyAccessQuery,
        EvidenceTiers.Tier3SyntaxOrTextual,
        source,
        null,
        null,
        new EvidenceSpan("database.accdb", 1, 1, null, "access-query", "1.0.0"),
        new Dictionary<string, string> { ["staticColumnHashes"] = staticColumnHashes });

    [Fact]
    public void Conflicting_query_kinds_are_omitted_independently_of_input_order()
    {
        var first = AccessDesignEvidenceComposer.BuildConsistentQueryKinds(
            [("query-a", "select"), ("query-a", "crosstab"), ("query-b", "select")]);
        var second = AccessDesignEvidenceComposer.BuildConsistentQueryKinds(
            [("query-b", "select"), ("query-a", "crosstab"), ("query-a", "select")]);

        Assert.Equal(first, second);
        Assert.False(first.ContainsKey("query-a"));
        Assert.Equal("select", first["query-b"]);
    }

    [Fact]
    public void Static_crosstab_pivot_hashes_require_a_consistent_crosstab_declaration()
    {
        var pivotHash = AccessSafeValues.RoleHash("access-query-pivot-column", "W4");
        var projected = AccessDesignEvidenceComposer.BuildStaticCrosstabPivotHashes(
            [
                CrosstabLineageFact("query-pivot", pivotHash + ";invalid"),
                CrosstabLineageFact("query-select", pivotHash)
            ],
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["query-pivot"] = "crosstab",
                ["query-select"] = "select"
            });

        Assert.Equal([pivotHash], projected["query-pivot"]);
        Assert.False(projected.ContainsKey("query-select"));
    }

    [Fact]
    public void Wildcard_projection_detection_is_limited_to_projection_items()
    {
        Assert.True(AccessQueryProjector.HasWildcardProjection("SELECT * FROM Orders"));
        Assert.True(AccessQueryProjector.HasWildcardProjection("SELECT Orders.* FROM Orders"));
        Assert.False(AccessQueryProjector.HasWildcardProjection("SELECT OrderId FROM Orders WHERE Note='*'"));
    }

    private const string SecretMarker = "Password_ProdVault_92817";
    private const string SqlMarker = "SELECT * FROM PayrollSecrets_92817";
    private const string ConnectionMarker = "ODBC;DSN=PrivateLedger_92817;PWD=NeverPersistThis";

    [Fact]
    public void Access_rule_catalog_uses_standard_primary_tiers_and_documents_all_possible_tiers()
    {
        var catalog = File.ReadAllText(Path.Combine(FindRepoRoot(), "rules", "rule-catalog.yml"));
        foreach (var ruleId in new[]
        {
            "legacy.access.database.inventory.v1",
            "legacy.access.schema.v1",
            "legacy.access.query.v1",
            "legacy.access.external-link.v1",
            "legacy.access.ui-surface.v1",
            "legacy.access.binding.v1",
            "legacy.access.macro-gap.v1"
        })
        {
            var start = Regex.Match(catalog, $@"(?m)^\s*-\s*id:\s*{Regex.Escape(ruleId)}\s*$");
            Assert.True(start.Success, $"Missing rule catalog entry for {ruleId}.");
            var remainder = catalog[start.Index..];
            var next = Regex.Match(remainder[start.Length..], @"(?m)^\s*-\s*id:\s*\S+\s*$");
            var block = next.Success ? remainder[..(start.Length + next.Index)] : remainder;
            Assert.Matches(@"(?m)^\s*evidenceTier:\s*Tier(?:1Semantic|2Structural|3SyntaxOrTextual|4Unknown)\s*$", block);
            Assert.Contains("possibleEvidenceTiers:", block, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Access_worker_always_observes_the_bounded_stderr_drain()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepoRoot(), "src", "dotnet", "TraceMap.Access", "AccessWorkerSupervisor.cs"));

        Assert.Contains("DrainBoundedAsync(worker.StandardError, total.Token)", source, StringComparison.Ordinal);
        Assert.Contains("finally", source, StringComparison.Ordinal);
        Assert.Contains("await stderrTask.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Safe_identity_hashes_protected_names_and_scopes_keys_to_repository_commit_and_path()
    {
        var firstSeed = AccessSafeValues.DatabaseIdentitySeed("repo-a", new string('a', 40), "db/app.accdb", "db-hash");
        var secondSeed = AccessSafeValues.DatabaseIdentitySeed("repo-a", new string('b', 40), "db/app.accdb", "db-hash");
        var protectedIdentity = AccessSafeValues.Identity(firstSeed, "table", SecretMarker);

        Assert.Null(protectedIdentity.DisplayName);
        Assert.DoesNotContain(SecretMarker, JsonSerializer.Serialize(protectedIdentity), StringComparison.Ordinal);
        Assert.NotEqual(
            AccessSafeValues.Identity(firstSeed, "table", "Orders").StableKey,
            AccessSafeValues.Identity(secondSeed, "table", "Orders").StableKey);
    }

    [Fact]
    public void Query_projector_ignores_literals_and_comments_and_marks_external_in_clause_partial()
    {
        var known = new Dictionary<string, IReadOnlyList<(string StableKey, string Kind)>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Orders"] = [("table-orders", "table")],
            ["PayrollSecrets_92817"] = [("table-secret", "table")],
            ["CommentOnly"] = [("table-comment", "table")]
        };

        var projected = AccessQueryProjector.ProjectDependencies(
            "SELECT * FROM [Orders] WHERE note='FROM [PayrollSecrets_92817]' -- JOIN CommentOnly\n", known);
        var external = AccessQueryProjector.ProjectDependencies(
            "SELECT * FROM Orders IN 'C:\\\\PrivateLedger_92817.accdb'", known);

        Assert.Equal(["table-orders"], projected.Dependencies.Select(item => item.TargetStableKey));
        Assert.Equal("complete", projected.Coverage);
        Assert.True(external.UnsupportedShape);
        Assert.Equal("partial", external.Coverage);
    }

    [Fact]
    public void Query_projector_marks_unresolved_references_partial_and_recognizes_access_from_lists()
    {
        var known = new Dictionary<string, IReadOnlyList<(string StableKey, string Kind)>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Customers"] = [("table-customers", "table")],
            ["Orders"] = [("table-orders", "table")]
        };

        var commaList = AccessQueryProjector.ProjectDependencies(
            "SELECT * FROM [Customers], Orders WHERE Customers.Id = Orders.CustomerId", known);
        var parenthesizedJoin = AccessQueryProjector.ProjectDependencies(
            "SELECT * FROM ([Customers] INNER JOIN Orders ON Customers.Id = Orders.CustomerId)", known);
        var unresolved = AccessQueryProjector.ProjectDependencies(
            "SELECT * FROM LinkedOrders", known);

        Assert.Equal(["table-customers", "table-orders"], commaList.Dependencies.Select(item => item.TargetStableKey));
        Assert.Equal("complete", commaList.Coverage);
        Assert.False(commaList.UnsupportedShape);
        Assert.Equal(["table-customers", "table-orders"], parenthesizedJoin.Dependencies.Select(item => item.TargetStableKey));
        Assert.Equal("complete", parenthesizedJoin.Coverage);
        Assert.True(unresolved.UnsupportedShape);
        Assert.Equal("partial", unresolved.Coverage);
        Assert.Empty(unresolved.Dependencies);
    }

    [Fact]
    public void Query_projector_does_not_treat_bracketed_object_names_as_unsupported_clauses()
    {
        var known = new Dictionary<string, IReadOnlyList<(string StableKey, string Kind)>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Union"] = [("table-union", "table")],
            ["Transform"] = [("table-transform", "table")]
        };

        var projected = AccessQueryProjector.ProjectDependencies(
            "SELECT * FROM [Union] INNER JOIN [Transform] ON [Union].Id = [Transform].Id;",
            known);

        Assert.Equal(["table-transform", "table-union"], projected.Dependencies.Select(item => item.TargetStableKey));
        Assert.Equal("complete", projected.Coverage);
        Assert.False(projected.UnsupportedShape);
    }

    [Fact]
    public void Query_projector_marks_unterminated_bracketed_identifiers_partial()
    {
        var known = new Dictionary<string, IReadOnlyList<(string StableKey, string Kind)>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Orders"] = [("table-orders", "table")]
        };

        var projected = AccessQueryProjector.ProjectDependencies(
            "SELECT * FROM Orders WHERE [Malformed = 1 UNION SELECT * FROM Orders;",
            known);

        Assert.Equal(["table-orders"], projected.Dependencies.Select(item => item.TargetStableKey));
        Assert.Equal("partial", projected.Coverage);
        Assert.True(projected.UnsupportedShape);
    }

    [Fact]
    public void Query_output_shape_accepts_only_direct_select_fields()
    {
        Assert.True(AccessQueryProjector.IsDirectOutputField(
            "SELECT Orders.OrderId, [Orders].[Order Status] FROM Orders;",
            "OrderId"));
        Assert.True(AccessQueryProjector.IsDirectOutputField(
            "SELECT Orders.OrderId, [Orders].[Order Status] FROM Orders;",
            "Order Status"));
        Assert.False(AccessQueryProjector.IsDirectOutputField(
            "SELECT Orders.OrderId AS Identifier FROM Orders;",
            "Identifier"));
        Assert.False(AccessQueryProjector.IsDirectOutputField(
            "SELECT Orders.* FROM Orders;",
            "OrderId"));
        Assert.False(AccessQueryProjector.IsDirectOutputField(
            "SELECT Count(Orders.OrderId) AS OrderCount FROM Orders;",
            "OrderCount"));

        Assert.True(AccessQueryProjector.HasStaticOutputName(
            "SELECT Orders.OrderId AS Identifier, Total: Sum(Orders.Amount) FROM Orders;",
            "Identifier"));
        Assert.True(AccessQueryProjector.HasStaticOutputName(
            "SELECT Orders.OrderId AS Identifier, Total: Sum(Orders.Amount) FROM Orders;",
            "Total"));
        Assert.False(AccessQueryProjector.HasStaticOutputName(
            "SELECT Orders.OrderId AS Identifier FROM Orders;",
            "Missing"));
    }

    [Fact]
    public void Query_projector_projects_append_field_correspondence_without_retaining_sql()
    {
        var sourceField = new AccessSafeIdentity(null, "source-name", "field-source");
        var targetField = new AccessSafeIdentity(null, "target-name", "field-target");
        var known = new Dictionary<string, IReadOnlyList<(string StableKey, string Kind)>>(StringComparer.OrdinalIgnoreCase)
        {
            ["SourceTable"] = [("table-source", "table")],
            ["TargetTable"] = [("table-target", "table")]
        };
        var fields = new Dictionary<string, Dictionary<string, List<AccessFieldProjection>>>(StringComparer.Ordinal)
        {
            ["table-source"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["SourceId"] = [new(sourceField, 0, "long", 4, true)]
            },
            ["table-target"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["TargetId"] = [new(targetField, 0, "long", 4, true)]
            }
        };

        var projected = AccessQueryProjector.ProjectActionLineage(
            "INSERT INTO TargetTable (TargetId) SELECT SourceId FROM SourceTable WHERE SourceId = [pId];",
            "append", known, fields);

        Assert.Equal("table-target", projected.TargetStableKey);
        Assert.Equal(["field-target"], projected.TargetFieldStableKeys);
        var mapping = Assert.Single(projected.FieldMappings);
        Assert.Equal(["field-source"], mapping.SourceFieldStableKeys);
        Assert.Equal("field-target", mapping.TargetFieldStableKey);
        Assert.Equal("complete", mapping.Coverage);
        Assert.NotNull(projected.PredicateExpressionHash);
        Assert.DoesNotContain("INSERT", JsonSerializer.Serialize(projected), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Query_projector_preserves_unresolved_append_target_ordinals()
    {
        var known = new Dictionary<string, IReadOnlyList<(string StableKey, string Kind)>>(StringComparer.OrdinalIgnoreCase)
        {
            ["SourceTable"] = [("table-source", "table")],
            ["TargetTable"] = [("table-target", "table")]
        };
        var fields = new Dictionary<string, Dictionary<string, List<AccessFieldProjection>>>(StringComparer.Ordinal)
        {
            ["table-source"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["SourceA"] = [new(new(null, "source-a", "field-source-a"), 0, "long", 4, false)],
                ["SourceB"] = [new(new(null, "source-b", "field-source-b"), 1, "long", 4, false)]
            },
            ["table-target"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["KnownField"] = [new(new(null, "known", "field-known"), 0, "long", 4, false)]
            }
        };

        var projected = AccessQueryProjector.ProjectActionLineage(
            "INSERT INTO TargetTable (UnknownField, KnownField) SELECT SourceA, SourceB FROM SourceTable;",
            "append", known, fields);

        Assert.Equal(["", "field-known"], projected.TargetFieldStableKeys);
        Assert.Equal("partial", projected.FieldMappings[0].Coverage);
        Assert.Equal("field-known", projected.FieldMappings[1].TargetFieldStableKey);
        Assert.Equal(["field-source-b"], projected.FieldMappings[1].SourceFieldStableKeys);
    }

    [Fact]
    public void Query_projector_keeps_partially_resolved_action_expressions_partial_and_source_scoped()
    {
        var known = new Dictionary<string, IReadOnlyList<(string StableKey, string Kind)>>(StringComparer.OrdinalIgnoreCase)
        {
            ["SourceTable"] = [("table-source", "table")],
            ["UnrelatedTable"] = [("table-unrelated", "table")],
            ["TargetTable"] = [("table-target", "table")]
        };
        var fields = new Dictionary<string, Dictionary<string, List<AccessFieldProjection>>>(StringComparer.Ordinal)
        {
            ["table-source"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["SourceId"] = [new(new(null, "source", "field-source"), 0, "long", 4, false)]
            },
            ["table-unrelated"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["MissingId"] = [new(new(null, "unrelated", "field-unrelated"), 0, "long", 4, false)]
            },
            ["table-target"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["TargetId"] = [new(new(null, "target", "field-target"), 0, "long", 4, false)]
            }
        };

        var projected = AccessQueryProjector.ProjectActionLineage(
            "INSERT INTO TargetTable (TargetId) SELECT SourceId + MissingId FROM SourceTable;",
            "append",
            known,
            fields);

        var mapping = Assert.Single(projected.FieldMappings);
        Assert.Equal(["field-source"], mapping.SourceFieldStableKeys);
        Assert.DoesNotContain("field-unrelated", mapping.SourceFieldStableKeys);
        Assert.Equal("partial", mapping.Coverage);
        Assert.Equal("partial", projected.Coverage);
    }

    [Fact]
    public void Query_projector_keeps_action_coverage_partial_when_a_declared_dependency_is_unresolved()
    {
        var known = new Dictionary<string, IReadOnlyList<(string StableKey, string Kind)>>(StringComparer.OrdinalIgnoreCase)
        {
            ["SourceTable"] = [("table-source", "table")],
            ["TargetTable"] = [("table-target", "table")]
        };
        var fields = new Dictionary<string, Dictionary<string, List<AccessFieldProjection>>>(StringComparer.Ordinal)
        {
            ["table-source"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["SourceId"] = [new(new(null, "source", "field-source"), 0, "long", 4, false)]
            },
            ["table-target"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["TargetId"] = [new(new(null, "target", "field-target"), 0, "long", 4, false)]
            }
        };

        var projected = AccessQueryProjector.ProjectActionLineage(
            "INSERT INTO TargetTable (TargetId) SELECT SourceTable.SourceId FROM SourceTable LEFT JOIN MissingSource ON SourceTable.SourceId = MissingSource.Id;",
            "append",
            known,
            fields);

        var mapping = Assert.Single(projected.FieldMappings);
        Assert.Equal("complete", mapping.Coverage);
        Assert.Equal(["field-source"], mapping.SourceFieldStableKeys);
        Assert.Equal("partial", projected.Coverage);
    }

    [Fact]
    public void Query_projector_projects_bounded_crosstab_shape_and_keeps_dynamic_pivots_partial()
    {
        var field = new AccessSafeIdentity(null, "row-name", "field-row");
        var known = new Dictionary<string, IReadOnlyList<(string StableKey, string Kind)>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Events"] = [("table-events", "table")]
        };
        var fields = new Dictionary<string, Dictionary<string, List<AccessFieldProjection>>>(StringComparer.Ordinal)
        {
            ["table-events"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["Category"] = [new(field, 0, "text", 64, false)],
                ["Amount"] = [new(new(null, "amount", "field-amount"), 1, "decimal", 16, false)],
                ["Month"] = [new(new(null, "month", "field-month"), 2, "text", 16, false)],
                ["EventDate"] = [new(new(null, "event-date", "field-event-date"), 3, "date", 8, false)]
            }
        };

        var staticShape = AccessQueryProjector.ProjectCrosstabLineage(
            "TRANSFORM Sum(Events.Amount) SELECT Events.Category FROM Events GROUP BY Events.Category PIVOT Events.Month IN ('Jan','Feb');",
            known, fields);
        var dynamicShape = AccessQueryProjector.ProjectCrosstabLineage(
            "TRANSFORM Sum(Events.Amount) SELECT Events.Category FROM Events GROUP BY Events.Category PIVOT Events.Month;",
            known, fields);
        var alternateStaticShape = AccessQueryProjector.ProjectCrosstabLineage(
            "TRANSFORM Sum(Events.Amount) SELECT Events.Category FROM Events GROUP BY Events.Category PIVOT Events.Month IN (\"Q1\",2025);",
            known, fields);
        var partiallyResolvedRows = AccessQueryProjector.ProjectCrosstabLineage(
            "TRANSFORM Sum(Events.Amount) SELECT Events.Category, MissingCategory FROM Events GROUP BY Events.Category PIVOT Events.Month IN ('Jan');",
            known, fields);
        var unsupportedFunction = AccessQueryProjector.ProjectCrosstabLineage(
            "TRANSFORM Sum(CustomNormalize(Events.Amount)) SELECT Events.Category FROM Events GROUP BY Events.Category PIVOT Events.Month IN ('Jan');",
            known, fields);

        Assert.Equal(["field-row"], staticShape.RowHeadingFieldStableKeys);
        Assert.Equal(2, staticShape.StaticColumnHashes.Count);
        Assert.NotEqual(staticShape.AggregateExpressionHash, staticShape.ValueExpressionHash);
        Assert.Equal("complete", staticShape.Coverage);
        Assert.Equal(2, alternateStaticShape.StaticColumnHashes.Count);
        Assert.Equal("partial", partiallyResolvedRows.Coverage);
        Assert.Equal("partial", unsupportedFunction.Coverage);
        Assert.Empty(dynamicShape.StaticColumnHashes);
        Assert.Equal("partial", dynamicShape.Coverage);

        var outputs = AccessQueryProjector.ProjectCrosstabOutputCatalog(
            "TRANSFORM Sum(Events.Amount) AS TotalAmount SELECT Events.Category FROM Events GROUP BY Events.Category PIVOT Events.Month IN ('Jan','Feb');",
            known,
            fields);
        Assert.Equal(["Category", "TotalAmount", "Jan", "Feb"], outputs.Select(output => output.Name));
        Assert.Equal(["row-heading", "aggregate", "static-pivot", "static-pivot"], outputs.Select(output => output.OutputKind));
        Assert.All(outputs, output => Assert.Equal("complete", output.Coverage));
        Assert.Equal(["field-row"], outputs[0].SourceFieldStableKeys);
        Assert.Equal(["field-amount"], outputs[1].SourceFieldStableKeys);
        Assert.Equal(["field-amount"], outputs[2].SourceFieldStableKeys);

        var aliasedOutputs = AccessQueryProjector.ProjectCrosstabOutputCatalog(
            "TRANSFORM Sum(Events.Amount) AS TotalAmount SELECT Format(Events.EventDate, 'yyyy'), Events.Category AS CategoryLabel FROM Events GROUP BY Format(Events.EventDate, 'yyyy'), Events.Category PIVOT Events.Month IN ('Jan');",
            known,
            fields);
        Assert.Equal([1, 2, 3], aliasedOutputs.Select(output => output.Ordinal));
        Assert.Equal(["CategoryLabel", "TotalAmount", "Jan"], aliasedOutputs.Select(output => output.Name));
        Assert.All(aliasedOutputs, output => Assert.Equal("complete", output.Coverage));
        Assert.Equal(["field-row"], aliasedOutputs[0].SourceFieldStableKeys);

        var accessAliasedOutputs = AccessQueryProjector.ProjectCrosstabOutputCatalog(
            "TRANSFORM Sum(Events.Amount) AS TotalAmount SELECT CategoryLabel: Events.Category FROM Events GROUP BY Events.Category PIVOT Events.Month IN ('Jan');",
            known,
            fields);
        Assert.Equal("complete", accessAliasedOutputs[0].Coverage);
        Assert.Equal(["field-row"], accessAliasedOutputs[0].SourceFieldStableKeys);

        var malformedOutputs = AccessQueryProjector.ProjectCrosstabOutputCatalog(
            "TRANSFORM Sum(Events.Amount) AS TotalAmount SELECT Events.[Category FROM Events GROUP BY Events.Category PIVOT Events.Month IN ('Jan');",
            known,
            fields);
        Assert.NotEmpty(malformedOutputs);
        Assert.All(malformedOutputs, output => Assert.Equal("partial", output.Coverage));

        var unsupportedPivotOutputs = AccessQueryProjector.ProjectCrosstabOutputCatalog(
            "TRANSFORM Sum(Events.Amount) AS TotalAmount SELECT Events.Category FROM Events GROUP BY Events.Category PIVOT CustomNormalize(Events.Month) IN ('Jan');",
            known,
            fields);
        Assert.Equal("partial", unsupportedPivotOutputs.Single(output => output.Name == "Jan").Coverage);

        var dynamicOutputs = AccessQueryProjector.ProjectCrosstabOutputCatalog(
            "TRANSFORM Sum(Events.Amount) SELECT Events.Category FROM Events GROUP BY Events.Category PIVOT Events.Month;",
            known,
            fields);
        Assert.Single(dynamicOutputs);
        Assert.Equal("row-heading", dynamicOutputs[0].OutputKind);

        var duplicateOutputs = AccessQueryProjector.ProjectCrosstabOutputCatalog(
            "TRANSFORM Sum(Events.Amount) AS Category SELECT Events.Category FROM Events GROUP BY Events.Category PIVOT Events.Month IN ('Jan');",
            known,
            fields);
        Assert.Equal([0, 1, 2], duplicateOutputs.Select(output => output.Ordinal));
        var duplicateCategories = duplicateOutputs.Where(output => output.Name == "Category").ToArray();
        Assert.Equal(2, duplicateCategories.Length);
        Assert.All(duplicateCategories, output =>
        {
            Assert.Equal("partial", output.Coverage);
            Assert.EndsWith("-duplicate-name", output.OutputKind, StringComparison.Ordinal);
        });
        Assert.Equal("Jan", duplicateOutputs[2].Name);
    }

    [Fact]
    public void Com_reader_emits_static_crosstab_outputs_for_downstream_composition()
    {
        var seed = AccessSafeValues.DatabaseIdentitySeed("repo", new string('a', 40), "fixture.accdb", "hash");
        var queryIdentity = AccessSafeValues.Identity(seed, "query", "MonthlyTotals");
        var tableIdentity = AccessSafeValues.Identity(seed, "table", "Events");
        var category = new AccessFieldProjection(
            AccessSafeValues.Identity(seed, $"field-{tableIdentity.StableKey}", "Category"), 0, "text", 64, false);
        var amount = new AccessFieldProjection(
            AccessSafeValues.Identity(seed, $"field-{tableIdentity.StableKey}", "Amount"), 1, "decimal", 16, false);
        var month = new AccessFieldProjection(
            AccessSafeValues.Identity(seed, $"field-{tableIdentity.StableKey}", "Month"), 2, "text", 16, false);
        var database = new FakeDaoDatabase(new FakeDaoQuery(
            "MonthlyTotals",
            "TRANSFORM Sum(Events.Amount) AS TotalAmount SELECT Events.Category FROM Events GROUP BY Events.Category PIVOT Events.Month IN ('Jan');",
            16));
        var gaps = new List<AccessGapProjection>();

        var queries = new AccessComReader().ReadQueries(
            database,
            seed,
            new Dictionary<string, AccessSafeIdentity>(StringComparer.OrdinalIgnoreCase)
            {
                ["MonthlyTotals"] = queryIdentity,
                ["Events"] = tableIdentity
            },
            new Dictionary<string, IReadOnlyList<(string StableKey, string Kind)>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Events"] = [(tableIdentity.StableKey, "table")]
            },
            new Dictionary<string, List<AccessTableProjection>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Events"] = [new(tableIdentity, [category, amount, month], [])]
            },
            new Dictionary<string, Dictionary<string, List<AccessFieldProjection>>>(StringComparer.Ordinal)
            {
                [tableIdentity.StableKey] = new(StringComparer.OrdinalIgnoreCase)
                {
                    ["Category"] = [category],
                    ["Amount"] = [amount],
                    ["Month"] = [month]
                }
            },
            gaps,
            []);

        var query = Assert.Single(queries);
        Assert.Equal(["Category", "TotalAmount", "Jan"], query.OutputFields!.Select(output => output.Identity.DisplayName));
        Assert.All(query.OutputFields!, output => Assert.Equal("complete", output.Coverage));
        Assert.DoesNotContain(gaps, gap => gap.Classification == "AccessQueryCrosstabDownstreamCompositionUnavailable");
    }

    [Fact]
    public void Query_projector_preserves_update_and_delete_targets_with_predicate_hashes()
    {
        var known = new Dictionary<string, IReadOnlyList<(string StableKey, string Kind)>>(StringComparer.OrdinalIgnoreCase)
        {
            ["TargetTable"] = [("table-target", "table")]
        };
        var fields = new Dictionary<string, Dictionary<string, List<AccessFieldProjection>>>(StringComparer.Ordinal)
        {
            ["table-target"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["Status"] = [new(new(null, "status", "field-status"), 0, "text", 32, false)],
                ["Id"] = [new(new(null, "id", "field-id"), 1, "long", 4, true)]
            }
        };

        var update = AccessQueryProjector.ProjectActionLineage(
            "UPDATE TargetTable SET Status = 'Ready' WHERE Id = [pId];", "update", known, fields);
        var unresolvedUpdate = AccessQueryProjector.ProjectActionLineage(
            "UPDATE TargetTable SET Status = MissingField WHERE Id = [pId];", "update", known, fields);
        var delete = AccessQueryProjector.ProjectActionLineage(
            "DELETE FROM TargetTable WHERE Id = [pId];", "delete", known, fields);

        Assert.Equal("table-target", update.TargetStableKey);
        Assert.Equal("field-status", Assert.Single(update.FieldMappings).TargetFieldStableKey);
        Assert.NotNull(update.PredicateExpressionHash);
        Assert.Equal("partial", unresolvedUpdate.Coverage);
        Assert.Equal("partial", Assert.Single(unresolvedUpdate.FieldMappings).Coverage);
        Assert.Equal("table-target", delete.TargetStableKey);
        Assert.NotNull(delete.PredicateExpressionHash);
    }

    [Fact]
    public void Query_projector_marks_unsupported_action_predicates_partial_and_accepts_constant_static_predicates()
    {
        var known = new Dictionary<string, IReadOnlyList<(string StableKey, string Kind)>>(StringComparer.OrdinalIgnoreCase)
        {
            ["TargetTable"] = [("table-target", "table")]
        };
        var actionFields = new Dictionary<string, Dictionary<string, List<AccessFieldProjection>>>(StringComparer.Ordinal)
        {
            ["table-target"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["Id"] = [new(new(null, "id", "field-id"), 0, "long", 4, true)]
            }
        };

        var unsupported = AccessQueryProjector.ProjectActionLineage(
            "DELETE FROM TargetTable WHERE Eval(Id);", "delete", known, actionFields);

        Assert.Equal("partial", unsupported.Coverage);
        Assert.NotNull(unsupported.PredicateExpressionHash);

        var staticFields = new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>>(StringComparer.Ordinal)
        {
            ["table-target"] = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Id"] = ["field-id"]
            }
        };
        var constant = AccessQueryProjector.ProjectStaticSelect(
            "SELECT TargetTable.Id FROM TargetTable WHERE 1=1;", known, staticFields);

        Assert.Equal("complete", constant.Coverage);
        Assert.Equal(AccessSafeValues.RoleHash("access-query-predicate", "1=1"), constant.PredicateHash);
    }

    [Fact]
    public void Query_projector_keeps_unsupported_order_by_functions_partial()
    {
        var known = new Dictionary<string, IReadOnlyList<(string StableKey, string Kind)>>(StringComparer.OrdinalIgnoreCase)
        {
            ["SourceTable"] = [("table-source", "table")]
        };
        var fields = new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>>(StringComparer.Ordinal)
        {
            ["table-source"] = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Id"] = ["field-id"]
            }
        };

        var projected = AccessQueryProjector.ProjectStaticSelect(
            "SELECT SourceTable.Id FROM SourceTable ORDER BY CustomSort(SourceTable.Id);",
            known,
            fields);

        Assert.Equal("partial", projected.Coverage);
        Assert.Single(projected.FunctionNameHashes);
        Assert.Equal(
            AccessSafeValues.RoleHash("access-query-function-name", "CustomSort"),
            projected.FunctionNameHashes[0]);
    }

    [Fact]
    public void Query_projector_resolves_saved_query_output_fields_for_action_and_crosstab_lineage()
    {
        var known = new Dictionary<string, IReadOnlyList<(string StableKey, string Kind)>>(StringComparer.OrdinalIgnoreCase)
        {
            ["SourceQuery"] = [("query-source", "query")],
            ["TargetTable"] = [("table-target", "table")]
        };
        var fields = new Dictionary<string, Dictionary<string, List<AccessFieldProjection>>>(StringComparer.Ordinal)
        {
            ["query-source"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["SourceId"] = [new(new(null, "source-id", "query-field-source-id"), 0, "query-output", 0, false)],
                ["Amount"] = [new(new(null, "amount", "query-field-amount"), 1, "query-output", 0, false)],
                ["Month"] = [new(new(null, "month", "query-field-month"), 2, "query-output", 0, false)]
            },
            ["table-target"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["TargetId"] = [new(new(null, "target-id", "field-target-id"), 0, "long", 4, true)]
            }
        };

        var action = AccessQueryProjector.ProjectActionLineage(
            "INSERT INTO TargetTable (TargetId) SELECT SourceQuery.SourceId FROM SourceQuery;",
            "append", known, fields);
        var crosstab = AccessQueryProjector.ProjectCrosstabLineage(
            "TRANSFORM Sum(SourceQuery.Amount) SELECT SourceQuery.SourceId FROM SourceQuery GROUP BY SourceQuery.SourceId PIVOT SourceQuery.Month IN ('Jan');",
            known, fields);

        Assert.Equal("complete", action.Coverage);
        Assert.Equal(["query-field-source-id"], Assert.Single(action.FieldMappings).SourceFieldStableKeys);
        Assert.Equal("complete", crosstab.Coverage);
        Assert.Equal(["query-field-source-id"], crosstab.RowHeadingFieldStableKeys);
    }

    [Fact]
    public void Com_reader_preloads_saved_query_output_fields_before_action_lineage_projection()
    {
        var seed = AccessSafeValues.DatabaseIdentitySeed("repo", new string('a', 40), "fixture.accdb", "hash");
        var sourceQuery = AccessSafeValues.Identity(seed, "query", "SourceQuery");
        var appendQuery = AccessSafeValues.Identity(seed, "query", "AppendQuery");
        var targetTable = AccessSafeValues.Identity(seed, "table", "TargetTable");
        var targetField = new AccessFieldProjection(
            AccessSafeValues.Identity(seed, $"field-{targetTable.StableKey}", "TargetId"),
            0,
            "long",
            4,
            true);
        var appendDefinition = new FakeDaoQuery(
                "AppendQuery",
                "INSERT INTO TargetTable (TargetId) SELECT SourceQuery.SourceId FROM SourceQuery;",
                64);
        var sourceDefinition = new FakeDaoQuery(
                "SourceQuery",
                "SELECT SourceId FROM SourceTable;",
                new FakeDaoField("SourceId"));
        var database = new FakeDaoDatabase(appendDefinition, sourceDefinition);
        var identities = new Dictionary<string, AccessSafeIdentity>(StringComparer.OrdinalIgnoreCase)
        {
            ["AppendQuery"] = appendQuery,
            ["SourceQuery"] = sourceQuery
        };
        var known = new Dictionary<string, IReadOnlyList<(string StableKey, string Kind)>>(StringComparer.OrdinalIgnoreCase)
        {
            ["AppendQuery"] = [(appendQuery.StableKey, "query")],
            ["SourceQuery"] = [(sourceQuery.StableKey, "query")],
            ["TargetTable"] = [(targetTable.StableKey, "table")]
        };
        var targetProjection = new AccessTableProjection(targetTable, [targetField], []);
        var gaps = new List<AccessGapProjection>();

        var queries = new AccessComReader().ReadQueries(
            database,
            seed,
            identities,
            known,
            new Dictionary<string, List<AccessTableProjection>>(StringComparer.OrdinalIgnoreCase)
            {
                ["TargetTable"] = [targetProjection]
            },
            new Dictionary<string, Dictionary<string, List<AccessFieldProjection>>>(StringComparer.Ordinal)
            {
                [targetTable.StableKey] = new(StringComparer.OrdinalIgnoreCase)
                {
                    ["TargetId"] = [targetField]
                }
            },
            gaps,
            []);

        var action = queries.Single(query => query.Identity == appendQuery).ActionLineage;
        Assert.NotNull(action);
        Assert.Equal("complete", action!.Coverage);
        Assert.NotEmpty(Assert.Single(action.FieldMappings).SourceFieldStableKeys);
        Assert.DoesNotContain(gaps, gap =>
            gap.Classification == "AccessQueryActionLineagePartial"
            && gap.StableScopeKey == appendQuery.StableKey);
        Assert.Equal(1, sourceDefinition.FieldsReadCount);
        Assert.Equal(0, appendDefinition.FieldsReadCount);
    }

    [Fact]
    public void Query_projector_hashes_declared_output_names_instead_of_select_expressions()
    {
        var known = new Dictionary<string, IReadOnlyList<(string StableKey, string Kind)>>(StringComparer.OrdinalIgnoreCase)
        {
            ["TargetTable"] = [("table-target", "table")]
        };
        var fields = new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>>(StringComparer.Ordinal)
        {
            ["table-target"] = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Id"] = ["field-id"]
            }
        };

        var projection = AccessQueryProjector.ProjectStaticSelect(
            "SELECT TargetTable.Id AS Identifier, Abs(TargetTable.Id) FROM TargetTable;", known, fields);

        Assert.Equal(AccessSafeValues.RoleHash("access-query-output-name", "Identifier"), projection.Outputs[0].NameHash);
        Assert.Null(projection.Outputs[1].NameHash);
        Assert.True(AccessQueryProjector.HasStaticOutputName(
            "SELECT TargetTable.Id AS Identifier FROM TargetTable;", "Identifier"));
    }

    [Fact]
    public void Query_projector_separates_static_output_lineage_from_runtime_value_coverage()
    {
        var known = new Dictionary<string, IReadOnlyList<(string StableKey, string Kind)>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Food"] = [("table-food", "table")]
        };
        var fields = new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>>(StringComparer.Ordinal)
        {
            ["table-food"] = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["FoodId"] = ["field-food-id"],
                ["UserId"] = ["field-user-id"]
            }
        };

        var projection = AccessQueryProjector.ProjectStaticSelect(
            "SELECT Food.FoodId FROM Food WHERE Food.UserId = glngUserID();",
            known,
            fields);

        Assert.Equal("complete", projection.DependencyCoverage);
        Assert.Equal("complete", projection.OutputCoverage);
        Assert.Equal("partial", projection.RuntimeValueCoverage);
        Assert.Equal("partial", projection.Coverage);

        var selectFunction = AccessQueryProjector.ProjectStaticSelect(
            "SELECT glngUserID() AS UserId FROM Food;",
            known,
            fields);
        Assert.Equal("partial", selectFunction.RuntimeValueCoverage);

        var builtInPredicate = AccessQueryProjector.ProjectStaticSelect(
            "SELECT Food.FoodId FROM Food WHERE Food.FoodId < Date();",
            known,
            fields);
        Assert.Equal("partial", builtInPredicate.RuntimeValueCoverage);
    }

    [Fact]
    public void Com_reader_falls_back_to_unique_static_output_names_when_dao_catalog_is_empty()
    {
        var seed = AccessSafeValues.DatabaseIdentitySeed("repo", new string('a', 40), "fixture.accdb", "hash");
        var queryIdentity = AccessSafeValues.Identity(seed, "query", "qWeeklyPlans");
        var database = new FakeDaoDatabase(new FakeDaoQuery(
            "qWeeklyPlans",
            "SELECT WeeklyPlanID, StartDate, PercentComplete AS PlanPercent FROM WeeklyPlans;"));
        var gaps = new List<AccessGapProjection>();

        var query = Assert.Single(new AccessComReader().ReadQueries(
            database,
            seed,
            new Dictionary<string, AccessSafeIdentity>(StringComparer.OrdinalIgnoreCase)
            {
                ["qWeeklyPlans"] = queryIdentity
            },
            new Dictionary<string, IReadOnlyList<(string StableKey, string Kind)>>(StringComparer.OrdinalIgnoreCase)
            {
                ["WeeklyPlans"] = [("table-weekly-plans", "table")]
            },
            new Dictionary<string, List<AccessTableProjection>>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, Dictionary<string, List<AccessFieldProjection>>>(StringComparer.Ordinal),
            gaps,
            []));

        Assert.Equal(3, query.OutputFields!.Count);
        Assert.Equal([0, 1, 2], query.OutputFields.Select(output => output.Ordinal));
        Assert.All(query.OutputFields, output => Assert.Equal("partial", output.Coverage));
        Assert.Equal(3, gaps.Count(gap => gap.Classification == "AccessQueryOutputSourceUnavailable"));
    }

    [Fact]
    public void Com_reader_records_a_gap_when_zero_field_query_output_catalog_cannot_be_derived()
    {
        var seed = AccessSafeValues.DatabaseIdentitySeed("repo", new string('a', 40), "fixture.accdb", "hash");
        var queryIdentity = AccessSafeValues.Identity(seed, "query", "UnsupportedQuery");
        var database = new FakeDaoDatabase(new FakeDaoQuery("UnsupportedQuery", "PARAMETERS p Long;"));
        var gaps = new List<AccessGapProjection>();

        var query = Assert.Single(new AccessComReader().ReadQueries(
            database,
            seed,
            new Dictionary<string, AccessSafeIdentity>(StringComparer.OrdinalIgnoreCase)
            {
                ["UnsupportedQuery"] = queryIdentity
            },
            new Dictionary<string, IReadOnlyList<(string StableKey, string Kind)>>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, List<AccessTableProjection>>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, Dictionary<string, List<AccessFieldProjection>>>(StringComparer.Ordinal),
            gaps,
            []));

        Assert.Empty(query.OutputFields!);
        Assert.Contains(gaps, gap => gap.Classification == "AccessQueryOutputCatalogUnavailable"
            && gap.StableScopeKey == queryIdentity.StableKey);
    }

    [Fact]
    public void Com_reader_keeps_query_outputs_when_dependency_coverage_is_partial()
    {
        var seed = AccessSafeValues.DatabaseIdentitySeed("repo", new string('a', 40), "fixture.accdb", "hash");
        var queryIdentity = AccessSafeValues.Identity(seed, "query", "PartialQuery");
        var tableIdentity = AccessSafeValues.Identity(seed, "table", "Orders");
        var field = new AccessFieldProjection(
            AccessSafeValues.Identity(seed, $"field-{tableIdentity.StableKey}", "Id"),
            0,
            "long",
            4,
            true);
        var table = new AccessTableProjection(tableIdentity, [field], []);
        var database = new FakeDaoDatabase(
            new FakeDaoQuery(
                "PartialQuery",
                "SELECT [Id] FROM [Orders], [MissingSource]",
                new FakeDaoField("Id", "Orders", "Id")));
        var gaps = new List<AccessGapProjection>();

        var queries = new AccessComReader().ReadQueries(
            database,
            seed,
            new Dictionary<string, AccessSafeIdentity>(StringComparer.OrdinalIgnoreCase)
            {
                ["PartialQuery"] = queryIdentity
            },
            new Dictionary<string, IReadOnlyList<(string StableKey, string Kind)>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Orders"] = [(tableIdentity.StableKey, "table")]
            },
            new Dictionary<string, List<AccessTableProjection>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Orders"] = [table]
            },
            new Dictionary<string, Dictionary<string, List<AccessFieldProjection>>>(StringComparer.Ordinal)
            {
                [tableIdentity.StableKey] = new(StringComparer.OrdinalIgnoreCase)
                {
                    ["Id"] = [field]
                }
            },
            gaps,
            []);

        var output = Assert.Single(Assert.Single(queries).OutputFields!);
        Assert.Equal("partial", output.Coverage);
        Assert.Equal([field.Identity.StableKey], output.SourceFieldStableKeys);
        Assert.Contains(gaps, gap =>
            gap.Classification == "AccessQueryOutputDependencyPartial"
            && gap.ScopeKind == "query-output-field"
            && gap.StableScopeKey == output.Identity.StableKey);
        Assert.DoesNotContain(gaps, gap => gap.Classification == "AccessQueryOutputMetadataUnavailable");
    }

    [Fact]
    public void Com_reader_scopes_output_failures_to_the_most_specific_constructed_identity()
    {
        var seed = AccessSafeValues.DatabaseIdentitySeed("repo", new string('a', 40), "fixture.accdb", "hash");
        var sourceFailureIdentity = AccessSafeValues.Identity(seed, "query", "SourceFailure");
        var nameFailureIdentity = AccessSafeValues.Identity(seed, "query", "NameFailure");
        var database = new FakeDaoDatabase(
            new FakeDaoQuery(
                "SourceFailure",
                "SELECT [Id] FROM [Orders]",
                new FakeDaoField("Id", throwOnSource: true)),
            new FakeDaoQuery(
                "NameFailure",
                "SELECT [Id] FROM [Orders]",
                new FakeDaoField(throwOnName: true)));
        var gaps = new List<AccessGapProjection>();

        var queries = new AccessComReader().ReadQueries(
            database,
            seed,
            new Dictionary<string, AccessSafeIdentity>(StringComparer.OrdinalIgnoreCase)
            {
                ["SourceFailure"] = sourceFailureIdentity,
                ["NameFailure"] = nameFailureIdentity
            },
            new Dictionary<string, IReadOnlyList<(string StableKey, string Kind)>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Orders"] = [("access-table-11111111111111111111111111111111", "table")]
            },
            new Dictionary<string, List<AccessTableProjection>>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, Dictionary<string, List<AccessFieldProjection>>>(StringComparer.Ordinal),
            gaps,
            []);

        var sourceOutput = Assert.Single(queries.Single(query => query.Identity == sourceFailureIdentity).OutputFields!);
        Assert.Contains(gaps, gap =>
            gap.Classification == "AccessQueryOutputSourceUnavailable"
            && gap.ScopeKind == "query-output-field"
            && gap.StableScopeKey == sourceOutput.Identity.StableKey);
        Assert.Contains(gaps, gap =>
            gap.Classification == "AccessQueryOutputFieldNameUnavailable"
            && gap.ScopeKind == "query"
            && gap.StableScopeKey == nameFailureIdentity.StableKey);
    }

    [Fact]
    public void Input_validator_requires_exact_tracked_head_bytes_preserves_requested_output_and_rejects_destructive_ancestor()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "access-fixture");
        Directory.CreateDirectory(Path.Combine(repo, "data"));
        RunGit(repo, "init", "-b", "test");
        RunGit(repo, "config", "user.email", "test@example.invalid");
        RunGit(repo, "config", "user.name", "TraceMap Test");
        File.WriteAllBytes(Path.Combine(repo, "data", "fixture.accdb"), [1, 2, 3, 4]);
        RunGit(repo, "add", "data/fixture.accdb");
        RunGit(repo, "commit", "-m", "fixture");

        var valid = AccessInputValidator.Validate(new(repo, "data/fixture.accdb", Path.Combine(repo, "out")));
        Assert.Equal("data/fixture.accdb", valid.DatabaseRelativePath);
        Assert.Null(valid.RemoteUrl);

        var subdirectoryOutput = Path.Combine(repo, "data", "requested-output");
        var fromSubdirectory = AccessInputValidator.Validate(new(Path.Combine(repo, "data"), "data/fixture.accdb", subdirectoryOutput));
        Assert.Equal(Path.GetDirectoryName(fromSubdirectory.DatabaseFullPath), Path.GetDirectoryName(fromSubdirectory.OutputFullPath));
        Assert.Equal("requested-output", Path.GetFileName(fromSubdirectory.OutputFullPath));

        var gitMetadata = Assert.Throws<AccessScanException>(() =>
            AccessInputValidator.Validate(new(repo, "data/fixture.accdb", Path.Combine(repo, ".git", "hooks", "access-output"))));
        Assert.Equal("AccessUnsafeOutputPath", gitMetadata.Classification);

        var existingOutput = Path.Combine(repo, "existing-output");
        Directory.CreateDirectory(existingOutput);
        var sentinel = Path.Combine(existingOutput, "do-not-delete.txt");
        File.WriteAllText(sentinel, "owned by caller");
        var existing = Assert.Throws<AccessScanException>(() =>
            AccessInputValidator.Validate(new(repo, "data/fixture.accdb", existingOutput)));
        Assert.Equal("AccessOutputAlreadyExists", existing.Classification);
        Assert.Equal("owned by caller", File.ReadAllText(sentinel));

        var ancestor = Assert.Throws<AccessScanException>(() =>
            AccessInputValidator.Validate(new(repo, "data/fixture.accdb", Path.Combine(repo, "data"))));
        Assert.Equal("AccessUnsafeOutputPath", ancestor.Classification);

        File.WriteAllBytes(Path.Combine(repo, "data", "fixture.accdb"), [4, 3, 2, 1]);
        var dirty = Assert.Throws<AccessScanException>(() =>
            AccessInputValidator.Validate(new(repo, "data/fixture.accdb", Path.Combine(repo, "out"))));
        Assert.Equal("AccessInputNotAtCommit", dirty.Classification);

        File.WriteAllBytes(Path.Combine(repo, "data", "fixture.accdb"), [1, 2, 3, 4]);
        RunGit(repo, "update-index", "--assume-unchanged", "data/fixture.accdb");
        File.WriteAllBytes(Path.Combine(repo, "data", "fixture.accdb"), [4, 3, 2, 1]);
        var assumedUnchanged = Assert.Throws<AccessScanException>(() =>
            AccessInputValidator.Validate(new(repo, "data/fixture.accdb", Path.Combine(repo, "out"))));
        Assert.Equal("AccessInputNotAtCommit", assumedUnchanged.Classification);
    }

    [Fact]
    public async Task Cli_help_and_version_do_not_require_windows_or_com_and_scan_fails_cleanly_off_windows()
    {
        var output = new StringWriter();
        var error = new StringWriter();
        Assert.Equal(0, await AccessCommand.RunAsync(["--help"], output, error));
        Assert.Contains("static design metadata only", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("scan-file --database <local.accdb-or-mdb>", output.ToString(), StringComparison.Ordinal);

        output.GetStringBuilder().Clear();
        Assert.Equal(0, await AccessCommand.RunAsync(["scan-file", "--help"], output, error));
        Assert.Contains("local file snapshot", output.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("original file is never opened by Access", output.ToString(), StringComparison.Ordinal);

        output.GetStringBuilder().Clear();
        Assert.Equal(0, await AccessCommand.RunAsync(["--version"], output, error));
        Assert.Equal(AccessFactBuilder.ScannerVersion, output.ToString().Trim());

        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(1, await AccessCommand.RunAsync(
                ["scan", "--repo", ".", "--database", "fixture.accdb", "--out", "out"], output, error));
            Assert.Contains("AccessUnsupportedPlatform", error.ToString(), StringComparison.Ordinal);
            error.GetStringBuilder().Clear();
            Assert.Equal(1, await AccessCommand.RunAsync(
                ["scan-file", "--database", "fixture.accdb", "--out", "out"], output, error));
            Assert.Contains("AccessUnsupportedPlatform", error.ToString(), StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task File_first_snapshot_is_deterministic_private_no_remote_and_cleaned_after_success()
    {
        using var temp = new TempDirectory();
        var database = Path.Combine(temp.Path, "private-name.accdb");
        await File.WriteAllBytesAsync(database, [1, 2, 3, 4]);
        var commits = new List<string>();
        var scratchPaths = new List<string>();
        var runner = new AccessFileScanRunner();

        async Task<ScanResult> RunOnceAsync(string output)
        {
            return await runner.RunCoreAsync(
                new(database, output),
                (options, _) =>
                {
                    var input = AccessInputValidator.Validate(options);
                    commits.Add(input.CommitSha);
                    Assert.Equal(AccessProvenanceKinds.LocalFileSnapshot, input.ProvenanceKind);
                    Assert.Equal("database.accdb", input.DatabaseRelativePath);
                    Assert.Equal("localfilesnapshot", input.RepoName);
                    Assert.Null(input.RemoteUrl);
                    Assert.Empty(RunGitCapture(input.GitRoot, "remote"));
                    return Task.FromResult(AccessFactBuilder.Build(input, Projection(input), options));
                },
                () =>
                {
                    var path = Path.Combine(temp.Path, $"scratch-{scratchPaths.Count}");
                    Directory.CreateDirectory(path);
                    scratchPaths.Add(path);
                    return path;
                },
                path => Directory.Delete(path, recursive: true),
                CancellationToken.None);
        }

        var first = await RunOnceAsync(Path.Combine(temp.Path, "out-one"));
        var second = await RunOnceAsync(Path.Combine(temp.Path, "out-two"));

        Assert.Equal(commits[0], commits[1]);
        Assert.All(scratchPaths, path => Assert.False(Directory.Exists(path)));
        Assert.Equal([1, 2, 3, 4], await File.ReadAllBytesAsync(database));
        Assert.Contains(first.Facts, fact => fact.Properties.GetValueOrDefault("provenanceKind") == AccessProvenanceKinds.LocalFileSnapshot);
        Assert.Equal(
            first.Facts.Select(fact => fact.FactId),
            second.Facts.Select(fact => fact.FactId));
        var serialized = JsonSerializer.Serialize(first);
        Assert.DoesNotContain(database, serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private-name.accdb", serialized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task File_first_snapshot_ignores_host_git_configuration_and_pins_sha1()
    {
        if (OperatingSystem.IsWindows()) return;

        using var temp = new TempDirectory();
        var database = Path.Combine(temp.Path, "fixture.accdb");
        var marker = Path.Combine(temp.Path, "filter-ran");
        var script = Path.Combine(temp.Path, "leak-filter.sh");
        var attributes = Path.Combine(temp.Path, "global-attributes");
        var globalConfig = Path.Combine(temp.Path, "global-gitconfig");
        await File.WriteAllBytesAsync(database, [1, 2, 3, 4]);
        await File.WriteAllTextAsync(script, $"#!/bin/sh\nprintf ran > '{marker}'\ncat\n");
        File.SetUnixFileMode(script, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        await File.WriteAllTextAsync(attributes, "*.accdb filter=leak\n");
        await File.WriteAllTextAsync(
            globalConfig,
            $"[core]\n\tattributesFile = {attributes}\n[filter \"leak\"]\n\tclean = {script}\n\trequired = true\n[init]\n\tdefaultObjectFormat = sha256\n");

        var hostileGitVariables = new Dictionary<string, string?>
        {
            ["GIT_CONFIG_GLOBAL"] = globalConfig,
            ["GIT_CONFIG_COUNT"] = "0",
            ["GIT_DIR"] = Path.Combine(temp.Path, "hostile-git-dir"),
            ["GIT_WORK_TREE"] = Path.Combine(temp.Path, "hostile-work-tree"),
            ["GIT_INDEX_FILE"] = Path.Combine(temp.Path, "hostile-index"),
            ["GIT_OBJECT_DIRECTORY"] = Path.Combine(temp.Path, "hostile-objects"),
            ["GIT_COMMON_DIR"] = Path.Combine(temp.Path, "hostile-common-dir")
        };
        var previousGitVariables = hostileGitVariables.Keys.ToDictionary(
            key => key,
            Environment.GetEnvironmentVariable,
            StringComparer.Ordinal);
        try
        {
            foreach (var pair in hostileGitVariables)
                Environment.SetEnvironmentVariable(pair.Key, pair.Value);
            var runner = new AccessFileScanRunner();
            var result = await runner.RunCoreAsync(
                new(database, Path.Combine(temp.Path, "out")),
                (options, _) =>
                {
                    var input = AccessInputValidator.Validate(options);
                    Assert.Equal(40, input.CommitSha.Length);
                    return Task.FromResult(AccessFactBuilder.Build(input, Projection(input), options));
                },
                () =>
                {
                    var path = Path.Combine(temp.Path, "scratch");
                    Directory.CreateDirectory(path);
                    return path;
                },
                path => Directory.Delete(path, recursive: true),
                CancellationToken.None);

            Assert.NotEmpty(result.Facts);
            Assert.False(File.Exists(marker));
        }
        finally
        {
            foreach (var pair in previousGitVariables)
                Environment.SetEnvironmentVariable(pair.Key, pair.Value);
        }
    }

    [Fact]
    public async Task File_first_snapshot_rejects_network_paths_and_uses_the_configured_timeout_for_git()
    {
        using var temp = new TempDirectory();
        var runner = new AccessFileScanRunner();
        var scratchCreated = false;

        var exception = await Assert.ThrowsAsync<AccessScanException>(() => runner.RunCoreAsync(
            new(@"\\server\share\fixture.accdb", Path.Combine(temp.Path, "out"), 47),
            (_, _) => throw new InvalidOperationException("scan should not run"),
            () =>
            {
                scratchCreated = true;
                return Path.Combine(temp.Path, "unexpected-scratch");
            },
            _ => { },
            CancellationToken.None));

        Assert.Equal("AccessNetworkDatabasePathRejected", exception.Classification);
        Assert.False(scratchCreated);
        Assert.True(AccessFileScanRunner.IsNetworkHostedPath(
            Path.Combine(temp.Path, "mapped.accdb"),
            _ => DriveType.Network));
        Assert.False(AccessFileScanRunner.IsNetworkHostedPath(
            Path.Combine(temp.Path, "local.accdb"),
            _ => DriveType.Fixed));
        var scratchError = Assert.Throws<AccessScanException>(() =>
            AccessFileScanRunner.ValidateScratchDirectory(temp.Path, _ => DriveType.Network));
        Assert.Equal("AccessFileSnapshotNetworkScratchRejected", scratchError.Classification);
        Assert.Equal(47_000, AccessFileScanRunner.GitTimeoutMilliseconds(47));
    }

    [Fact]
    public async Task File_first_snapshot_rejects_network_scratch_before_copy_or_scan()
    {
        using var temp = new TempDirectory();
        var database = Path.Combine(temp.Path, "fixture.accdb");
        await File.WriteAllBytesAsync(database, [1, 2, 3, 4]);
        var scanCalled = false;

        var exception = await Assert.ThrowsAsync<AccessScanException>(() => new AccessFileScanRunner().RunCoreAsync(
            new(database, Path.Combine(temp.Path, "out")),
            (_, _) =>
            {
                scanCalled = true;
                throw new InvalidOperationException("scan should not run");
            },
            () => @"\\server\share\tracemap-access-scratch",
            _ => { },
            CancellationToken.None));

        Assert.Equal("AccessFileSnapshotNetworkScratchRejected", exception.Classification);
        Assert.False(scanCalled);
        Assert.Equal([1, 2, 3, 4], await File.ReadAllBytesAsync(database));
    }

    [Fact]
    public async Task File_first_snapshot_rechecks_original_and_cleans_scratch_on_failure_and_cancellation()
    {
        using var temp = new TempDirectory();
        var database = Path.Combine(temp.Path, "fixture.accdb");
        await File.WriteAllBytesAsync(database, [1, 2, 3, 4]);
        var runner = new AccessFileScanRunner();
        var scratchIndex = 0;
        var scratchPaths = new List<string>();
        string NewScratch()
        {
            var path = Path.Combine(temp.Path, $"snapshot-{scratchIndex++}");
            Directory.CreateDirectory(path);
            scratchPaths.Add(path);
            return path;
        }

        var failed = await Assert.ThrowsAsync<AccessScanException>(() => runner.RunCoreAsync(
            new(database, Path.Combine(temp.Path, "failed-out")),
            (_, _) => throw new AccessScanException("SyntheticScanFailure"),
            NewScratch,
            path => Directory.Delete(path, recursive: true),
            CancellationToken.None));
        Assert.Equal("SyntheticScanFailure", failed.Classification);

        var canceled = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runner.RunCoreAsync(
            new(database, Path.Combine(temp.Path, "canceled-out")),
            (_, _) => throw new OperationCanceledException(),
            NewScratch,
            path => Directory.Delete(path, recursive: true),
            CancellationToken.None));
        Assert.NotNull(canceled);

        var changed = await Assert.ThrowsAsync<AccessScanException>(() => runner.RunCoreAsync(
            new(database, Path.Combine(temp.Path, "changed-out")),
            (options, _) =>
            {
                var input = AccessInputValidator.Validate(options);
                File.WriteAllBytes(database, [4, 3, 2, 1]);
                return Task.FromResult(AccessFactBuilder.Build(input, Projection(input), options));
            },
            NewScratch,
            path => Directory.Delete(path, recursive: true),
            CancellationToken.None));
        Assert.Equal("AccessOriginalInputChangedDuringScan", changed.Classification);
        Assert.All(scratchPaths, path => Assert.False(Directory.Exists(path)));
    }

    [Fact]
    public async Task File_first_snapshot_classifies_malformed_input_paths_before_creating_scratch()
    {
        using var temp = new TempDirectory();
        var database = Path.Combine(temp.Path, "fixture.accdb");
        await File.WriteAllBytesAsync(database, [1, 2, 3, 4]);
        var scratchCreated = false;
        var runner = new AccessFileScanRunner();

        var databaseException = await Assert.ThrowsAsync<AccessScanException>(() => runner.RunCoreAsync(
            new("invalid\0database.accdb", Path.Combine(temp.Path, "out")),
            (_, _) => throw new InvalidOperationException("scan should not run"),
            () =>
            {
                scratchCreated = true;
                return Path.Combine(temp.Path, "unexpected-scratch");
            },
            _ => { },
            CancellationToken.None));
        Assert.Equal("AccessDatabasePathInvalid", databaseException.Classification);

        var outputException = await Assert.ThrowsAsync<AccessScanException>(() => runner.RunCoreAsync(
            new(database, "invalid\0output"),
            (_, _) => throw new InvalidOperationException("scan should not run"),
            () =>
            {
                scratchCreated = true;
                return Path.Combine(temp.Path, "unexpected-scratch");
            },
            _ => { },
            CancellationToken.None));
        Assert.Equal("AccessUnsafeOutputPath", outputException.Classification);
        Assert.False(scratchCreated);
    }

    [Theory]
    [InlineData(false, 1, 2, false)]
    [InlineData(true, 0, 2, false)]
    [InlineData(true, 1, 2, true)]
    public void File_first_snapshot_only_allows_a_missing_final_output_leaf(
        bool allowMissingLeaf,
        int segmentIndex,
        int segmentCount,
        bool expected)
    {
        Assert.Equal(
            expected,
            AccessFileScanRunner.CanAllowMissingLeaf(allowMissingLeaf, segmentIndex, segmentCount));
    }

    [Fact]
    public async Task File_first_snapshot_fails_closed_when_internal_repository_cleanup_fails()
    {
        using var temp = new TempDirectory();
        var database = Path.Combine(temp.Path, "fixture.accdb");
        await File.WriteAllBytesAsync(database, [1, 2, 3, 4]);
        var scratch = Path.Combine(temp.Path, "cleanup-failure");
        var runner = new AccessFileScanRunner();

        var exception = await Assert.ThrowsAsync<AccessScanException>(() => runner.RunCoreAsync(
            new(database, Path.Combine(temp.Path, "out")),
            (options, _) =>
            {
                var input = AccessInputValidator.Validate(options);
                return Task.FromResult(AccessFactBuilder.Build(input, Projection(input), options));
            },
            () =>
            {
                Directory.CreateDirectory(scratch);
                return scratch;
            },
            _ => throw new IOException("synthetic cleanup failure"),
            CancellationToken.None,
            () => Task.CompletedTask));

        Assert.Equal("AccessFileSnapshotCleanupFailed", exception.Classification);
        Directory.Delete(scratch, recursive: true);
    }

    [Fact]
    public async Task File_first_snapshot_retries_transient_internal_repository_cleanup_failure()
    {
        using var temp = new TempDirectory();
        var database = Path.Combine(temp.Path, "fixture.accdb");
        await File.WriteAllBytesAsync(database, [1, 2, 3, 4]);
        var scratch = Path.Combine(temp.Path, "transient-cleanup-failure");
        var cleanupAttempts = 0;
        var retryDelays = 0;

        var result = await new AccessFileScanRunner().RunCoreAsync(
            new(database, Path.Combine(temp.Path, "out")),
            (options, _) =>
            {
                var input = AccessInputValidator.Validate(options);
                return Task.FromResult(AccessFactBuilder.Build(input, Projection(input), options));
            },
            () =>
            {
                Directory.CreateDirectory(scratch);
                return scratch;
            },
            path =>
            {
                cleanupAttempts++;
                if (cleanupAttempts < 3) throw new IOException("synthetic transient cleanup failure");
                Directory.Delete(path, recursive: true);
            },
            CancellationToken.None,
            () =>
            {
                retryDelays++;
                return Task.CompletedTask;
            });

        Assert.NotEmpty(result.Facts);
        Assert.Equal(3, cleanupAttempts);
        Assert.Equal(2, retryDelays);
        Assert.False(Directory.Exists(scratch));
    }

    [Fact]
    public void File_first_snapshot_cleanup_clears_read_only_members_before_deletion()
    {
        using var temp = new TempDirectory();
        var scratch = Path.Combine(temp.Path, "read-only-cleanup");
        var objects = Path.Combine(scratch, ".git", "objects", "ab");
        Directory.CreateDirectory(objects);
        var gitObject = Path.Combine(objects, "synthetic-object");
        File.WriteAllBytes(gitObject, [1, 2, 3, 4]);
        File.SetAttributes(gitObject, File.GetAttributes(gitObject) | FileAttributes.ReadOnly);

        AccessFileScanRunner.ClearReadOnlyAttributes(scratch);

        Assert.Equal(FileAttributes.None, File.GetAttributes(gitObject) & FileAttributes.ReadOnly);
        Directory.Delete(scratch, recursive: true);
        Assert.False(Directory.Exists(scratch));
    }

    [Fact]
    public async Task File_first_snapshot_cleanup_is_idempotent_when_scratch_is_already_absent()
    {
        using var temp = new TempDirectory();
        var scratch = Path.Combine(temp.Path, "already-removed");
        var deleteCalled = false;

        AccessFileScanRunner.ClearReadOnlyAttributes(scratch);
        await AccessFileScanRunner.DeleteScratchDirectoryWithRetryAsync(
            scratch,
            _ => deleteCalled = true,
            () => Task.CompletedTask);

        Assert.False(deleteCalled);
    }

    [Fact]
    public async Task File_first_validation_rejects_unsupported_reparse_and_caller_owned_paths_before_snapshot_creation()
    {
        using var temp = new TempDirectory();
        var runner = new AccessFileScanRunner();
        var scratchCreated = false;
        Task<ScanResult> RunAsync(string database, string output) => runner.RunCoreAsync(
            new(database, output),
            (_, _) => throw new InvalidOperationException("scan should not run"),
            () =>
            {
                scratchCreated = true;
                return Path.Combine(temp.Path, "unexpected-scratch");
            },
            _ => { },
            CancellationToken.None);

        var unsupported = Path.Combine(temp.Path, "fixture.txt");
        await File.WriteAllBytesAsync(unsupported, [1]);
        var unsupportedError = await Assert.ThrowsAsync<AccessScanException>(
            () => RunAsync(unsupported, Path.Combine(temp.Path, "unsupported-out")));
        Assert.Equal("AccessUnsupportedDatabaseExtension", unsupportedError.Classification);

        var database = Path.Combine(temp.Path, "fixture.accdb");
        await File.WriteAllBytesAsync(database, [1]);
        var existingOutput = Path.Combine(temp.Path, "existing-output");
        Directory.CreateDirectory(existingOutput);
        await File.WriteAllTextAsync(Path.Combine(existingOutput, "sentinel.txt"), "preserve");
        var existingError = await Assert.ThrowsAsync<AccessScanException>(
            () => RunAsync(database, existingOutput));
        Assert.Equal("AccessOutputAlreadyExists", existingError.Classification);
        Assert.Equal("preserve", await File.ReadAllTextAsync(Path.Combine(existingOutput, "sentinel.txt")));

        if (!OperatingSystem.IsWindows())
        {
            var link = Path.Combine(temp.Path, "fixture-link.accdb");
            File.CreateSymbolicLink(link, database);
            var linkError = await Assert.ThrowsAsync<AccessScanException>(
                () => RunAsync(link, Path.Combine(temp.Path, "link-out")));
            Assert.Equal("AccessDatabaseReparsePointRejected", linkError.Classification);
        }

        Assert.False(scratchCreated);
    }

    [Fact]
    public void Environment_probe_classifies_unsupported_platform_and_missing_access_com()
    {
        var unsupported = Assert.Throws<AccessScanException>(() => AccessEnvironmentProbe.Probe(false, () => typeof(object)));
        Assert.Equal("AccessUnsupportedPlatform", unsupported.Classification);
        var missingCom = Assert.Throws<AccessScanException>(() => AccessEnvironmentProbe.Probe(true, () => null));
        Assert.Equal("AccessComUnavailable", missingCom.Classification);
        Assert.Equal(typeof(object), AccessEnvironmentProbe.Probe(true, () => typeof(object)));
    }

    [Fact]
    public async Task Facts_and_all_standard_text_artifacts_suppress_raw_sql_connections_paths_and_credentials()
    {
        using var temp = new TempDirectory();
        var databasePath = Path.Combine(temp.Path, "fixture.accdb");
        await File.WriteAllBytesAsync(databasePath, [1, 2, 3, 4]);
        var input = Input(databasePath, Path.Combine(temp.Path, "artifacts"));
        var projection = Projection(input);
        var result = AccessFactBuilder.Build(input, projection, new(temp.Path, "fixture.accdb", input.OutputFullPath));

        var serializedFacts = JsonSerializer.Serialize(result.Facts);
        var report = AccessArtifactWriter.Report(result);
        var log = AccessArtifactWriter.AnalyzerLog(result);
        foreach (var protectedValue in new[] { SecretMarker, SqlMarker, ConnectionMarker, temp.Path })
        {
            Assert.DoesNotContain(protectedValue, serializedFacts, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(protectedValue, report, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(protectedValue, log, StringComparison.OrdinalIgnoreCase);
        }

        await AccessArtifactWriter.WriteAsync(input.OutputFullPath, result, AccessLimits.Default);
        Assert.Equal(
            ["facts.ndjson", "index.sqlite", "logs/analyzer.log", "report.md", "scan-manifest.json"],
            Directory.EnumerateFiles(input.OutputFullPath, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(input.OutputFullPath, path).Replace('\\', '/'))
                .OrderBy(path => path, StringComparer.Ordinal));
        foreach (var file in Directory.EnumerateFiles(input.OutputFullPath, "*", SearchOption.AllDirectories))
        {
            if (Path.GetExtension(file).Equals(".sqlite", StringComparison.OrdinalIgnoreCase)) continue;
            var contents = await File.ReadAllTextAsync(file);
            Assert.DoesNotContain(SecretMarker, contents, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(ConnectionMarker, contents, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(temp.Path, contents, StringComparison.OrdinalIgnoreCase);
        }

        var existingArtifactHash = AccessInputValidator.HashFile(Path.Combine(input.OutputFullPath, "facts.ndjson"));
        var existingOutput = await Assert.ThrowsAsync<AccessScanException>(() =>
            AccessArtifactWriter.WriteAsync(input.OutputFullPath, result, AccessLimits.Default));
        Assert.Equal("AccessOutputAlreadyExists", existingOutput.Classification);
        Assert.Equal(existingArtifactHash, AccessInputValidator.HashFile(Path.Combine(input.OutputFullPath, "facts.ndjson")));
    }

    [Fact]
    public void Fact_ids_and_order_are_deterministic_and_case_only_names_remain_distinct()
    {
        using var temp = new TempDirectory();
        var databasePath = Path.Combine(temp.Path, "fixture.accdb");
        File.WriteAllBytes(databasePath, [1, 2, 3, 4]);
        var input = Input(databasePath, Path.Combine(temp.Path, "out"));
        var projection = Projection(input);

        var first = AccessFactBuilder.Build(input, projection, new(temp.Path, "fixture.accdb", input.OutputFullPath));
        var second = AccessFactBuilder.Build(input, projection, new(temp.Path, "fixture.accdb", input.OutputFullPath));

        Assert.Equal(first.Facts.Select(fact => fact.FactId), second.Facts.Select(fact => fact.FactId));
        Assert.Equal(first.Facts.Select(fact => fact.FactType), first.Facts.Select(fact => fact.FactType).OrderBy(value => value, StringComparer.Ordinal));
        var tableKeys = projection.Tables.Select(table => table.Identity.StableKey).ToArray();
        Assert.Equal(2, tableKeys.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Controlled_working_copies_are_distinct_hash_verified_private_and_deleted()
    {
        using var temp = new TempDirectory();
        var databasePath = Path.Combine(temp.Path, "fixture.accdb");
        File.WriteAllBytes(databasePath, [1, 2, 3, 4]);
        var input = Input(databasePath, Path.Combine(temp.Path, "out"));
        string firstDirectory;
        string secondDirectory;
        using (var first = AccessWorkingCopy.Create(input))
        using (var second = AccessWorkingCopy.Create(input))
        {
            firstDirectory = first.DirectoryPath;
            secondDirectory = second.DirectoryPath;
            Assert.NotEqual(firstDirectory, secondDirectory);
            Assert.Equal(input.DatabaseHash, AccessInputValidator.HashFile(first.DatabasePath));
            Assert.Equal(input.DatabaseHash, AccessInputValidator.HashFile(second.DatabasePath));
            if (!OperatingSystem.IsWindows())
            {
                var mode = File.GetUnixFileMode(firstDirectory);
                Assert.Equal(UnixFileMode.None, mode & (UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute));
            }
        }
        Assert.False(Directory.Exists(firstDirectory));
        Assert.False(Directory.Exists(secondDirectory));
        Assert.Equal(input.DatabaseHash, AccessInputValidator.HashFile(databasePath));
    }

    [Theory]
    [InlineData("../fixture.accdb", "AccessDatabasePathTraversal")]
    [InlineData("data/../fixture.accdb", "AccessDatabasePathTraversal")]
    [InlineData("", "AccessDatabasePathMissing")]
    public void Relative_path_normalization_rejects_traversal(string path, string classification)
    {
        var exception = Assert.Throws<AccessScanException>(() => AccessInputValidator.NormalizeRelativeSegments(path));
        Assert.Equal(classification, exception.Classification);
    }

    [Fact]
    public void Absolute_database_path_is_rejected_before_git_or_com()
    {
        var exception = Assert.Throws<AccessScanException>(() =>
            AccessInputValidator.Validate(new("missing-repo", Path.GetFullPath("fixture.accdb"), Path.GetFullPath("out"))));
        Assert.Equal("AccessDatabasePathMustBeRelative", exception.Classification);
    }

    [Fact]
    public void Untracked_database_and_missing_git_metadata_are_rejected()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        File.WriteAllBytes(Path.Combine(repo, "fixture.accdb"), [1, 2, 3, 4]);
        var missingGit = Assert.Throws<AccessScanException>(() =>
            AccessInputValidator.Validate(new(repo, "fixture.accdb", Path.Combine(repo, "out"))));
        Assert.Equal("AccessGitRootUnavailable", missingGit.Classification);

        RunGit(repo, "init", "-b", "test");
        RunGit(repo, "config", "user.email", "test@example.invalid");
        RunGit(repo, "config", "user.name", "TraceMap Test");
        File.WriteAllText(Path.Combine(repo, "README.md"), "fixture");
        RunGit(repo, "add", "README.md");
        RunGit(repo, "commit", "-m", "fixture root");
        var untracked = Assert.Throws<AccessScanException>(() =>
            AccessInputValidator.Validate(new(repo, "fixture.accdb", Path.Combine(repo, "out"))));
        Assert.Equal("AccessInputNotTracked", untracked.Classification);
    }

    [Fact]
    public void Git_lfs_pointer_bytes_are_not_accepted_as_a_materialized_database()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        RunGit(repo, "init", "-b", "test");
        RunGit(repo, "config", "user.email", "test@example.invalid");
        RunGit(repo, "config", "user.name", "TraceMap Test");
        File.WriteAllText(Path.Combine(repo, ".gitattributes"), "*.accdb filter=lfs\n");
        File.WriteAllText(Path.Combine(repo, "fixture.accdb"), $"version https://git-lfs.github.com/spec/v1\noid sha256:{new string('a', 64)}\nsize 1234\n");
        RunGit(repo, "add", ".gitattributes", "fixture.accdb");
        RunGit(repo, "commit", "-m", "pointer fixture");

        var pointer = Assert.Throws<AccessScanException>(() =>
            AccessInputValidator.Validate(new(repo, "fixture.accdb", Path.Combine(repo, "out"))));
        Assert.Equal("AccessGitLfsContentMismatch", pointer.Classification);
    }

    [Fact]
    public void Fact_ceiling_keeps_foundational_evidence_and_emits_an_explicit_gap()
    {
        using var temp = new TempDirectory();
        var databasePath = Path.Combine(temp.Path, "fixture.accdb");
        File.WriteAllBytes(databasePath, [1, 2, 3, 4]);
        var input = Input(databasePath, Path.Combine(temp.Path, "out"));
        var limits = AccessLimits.Default with { MaxFacts = 4 };

        var result = AccessFactBuilder.Build(input, Projection(input), new(temp.Path, "fixture.accdb", input.OutputFullPath), limits);

        Assert.Equal(4, result.Facts.Count);
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.FileInventoried);
        Assert.Contains(result.Facts, fact => fact.Properties.GetValueOrDefault("classification") == "AccessFactLimitReached");
        Assert.Contains("AccessFactLimitReached", result.Manifest.KnownGaps);
    }

    [Fact]
    public void Identical_catalog_gaps_are_deduplicated_before_sqlite_persistence()
    {
        using var temp = new TempDirectory();
        var databasePath = Path.Combine(temp.Path, "fixture.mdb");
        File.WriteAllBytes(databasePath, [1, 2, 3, 4]);
        var input = Input(databasePath, Path.Combine(temp.Path, "out")) with { DatabaseExtension = ".mdb" };
        var projection = Projection(input) with
        {
            DatabaseExtension = ".mdb",
            Gaps = [new("AccessTableCatalogUnavailable", "database-tables", null), new("AccessTableCatalogUnavailable", "database-tables", null)]
        };

        var result = AccessFactBuilder.Build(input, projection, new(temp.Path, "fixture.mdb", input.OutputFullPath));
        Assert.Single(result.Facts, fact => fact.Properties.GetValueOrDefault("classification") == "AccessTableCatalogUnavailable");
        Assert.Equal(result.Facts.Count, result.Facts.Select(fact => fact.FactId).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Exact_gap_limit_preserves_all_gaps_and_over_limit_replaces_only_one_with_limit_evidence()
    {
        using var temp = new TempDirectory();
        var databasePath = Path.Combine(temp.Path, "fixture.accdb");
        File.WriteAllBytes(databasePath, [1, 2, 3, 4]);
        var input = Input(databasePath, Path.Combine(temp.Path, "out"));
        var limits = AccessLimits.Default with { MaxGaps = 2 };
        var two = Projection(input) with { Gaps = [new("GapA", "database", null), new("GapB", "database", null)] };
        var three = two with { Gaps = [.. two.Gaps, new("GapC", "database", null)] };

        var exact = AccessFactBuilder.Build(input, two, new(temp.Path, "fixture.accdb", input.OutputFullPath), limits);
        Assert.Contains(exact.Facts, fact => fact.Properties.GetValueOrDefault("classification") == "GapA");
        Assert.Contains(exact.Facts, fact => fact.Properties.GetValueOrDefault("classification") == "GapB");
        Assert.DoesNotContain(exact.Facts, fact => fact.Properties.GetValueOrDefault("classification") == "AccessGapLimitReached");

        var truncated = AccessFactBuilder.Build(input, three, new(temp.Path, "fixture.accdb", input.OutputFullPath), limits);
        Assert.Single(truncated.Facts, fact => fact.Properties.GetValueOrDefault("classification") == "AccessGapLimitReached");
        Assert.Equal(2, truncated.Facts.Count(fact => fact.FactType == FactTypes.AnalysisGap));
    }

    [Fact]
    public void Relationship_masks_are_normalized_without_losing_raw_or_unknown_bits()
    {
        using var temp = new TempDirectory();
        var databasePath = Path.Combine(temp.Path, "fixture.accdb");
        File.WriteAllBytes(databasePath, [1, 2, 3, 4]);
        var input = Input(databasePath, Path.Combine(temp.Path, "out"));
        var seed = AccessSafeValues.DatabaseIdentitySeed(input.RepositoryIdentityHash, input.CommitSha, input.DatabaseRelativePath, input.DatabaseHash);
        const int attributes = 1 | 2 | 4 | 8 | 256 | 4096 | 16_777_216 | 33_554_432;
        var relationship = new AccessRelationshipProjection(
            AccessSafeValues.Identity(seed, "relationship", "SyntheticRelationship"),
            "source-table",
            "target-table",
            attributes,
            []);
        var projection = Projection(input) with { Relationships = [relationship] };

        var result = AccessFactBuilder.Build(input, projection, new(temp.Path, "fixture.accdb", input.OutputFullPath));
        var fact = Assert.Single(result.Facts, candidate =>
            candidate.Properties.GetValueOrDefault("mappingKind") == "declared-relationship");

        Assert.Equal(attributes.ToString(), fact.Properties["relationshipAttributes"]);
        Assert.Equal(
            "unique-one-to-one;not-enforced;inherited;update-cascade;delete-cascade;left-default-join;right-default-join",
            fact.Properties["relationshipAttributeFlags"]);
        Assert.Equal("8", fact.Properties["relationshipUnknownAttributeBits"]);
        Assert.Contains("no-runtime-enforcement", fact.Properties["limitations"], StringComparison.Ordinal);
    }

    [Fact]
    public void Query_owned_gaps_preserve_stable_owner_and_supporting_declaration_fact()
    {
        using var temp = new TempDirectory();
        var databasePath = Path.Combine(temp.Path, "fixture.accdb");
        File.WriteAllBytes(databasePath, [1, 2, 3, 4]);
        var input = Input(databasePath, Path.Combine(temp.Path, "out"));
        var projection = Projection(input);

        var result = AccessFactBuilder.Build(input, projection, new(temp.Path, "fixture.accdb", input.OutputFullPath));
        var query = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.AccessQueryDeclared);
        var gap = Assert.Single(result.Facts, fact =>
            fact.Properties.GetValueOrDefault("classification") == "AccessQueryDependencyPartial");

        Assert.Equal(query.TargetSymbol, gap.TargetSymbol);
        Assert.Equal(query.TargetSymbol, gap.Properties["scopeStableKey"]);
        Assert.Equal(query.FactId, gap.Properties["supportingFactIds"]);
    }

    [Fact]
    public void Query_output_gaps_reference_the_owning_query_declaration_fact()
    {
        using var temp = new TempDirectory();
        var databasePath = Path.Combine(temp.Path, "fixture.accdb");
        File.WriteAllBytes(databasePath, [1, 2, 3, 4]);
        var input = Input(databasePath, Path.Combine(temp.Path, "out"));
        var projection = Projection(input);
        var query = Assert.Single(projection.Queries);
        var outputIdentity = AccessSafeValues.Identity(
            AccessSafeValues.DatabaseIdentitySeed(input.RepositoryIdentityHash, input.CommitSha, input.DatabaseRelativePath, input.DatabaseHash),
            $"query-field-{query.Identity.StableKey}",
            "OutputField",
            0);
        var output = new AccessQueryOutputFieldProjection(outputIdentity, 0, "unknown", [], "partial");
        projection = projection with
        {
            Queries = [query with { OutputFields = [output] }],
            Gaps = [new("AccessQueryOutputSourceUnavailable", "query-output-field", outputIdentity.StableKey, RuleIds.LegacyAccessQuery)]
        };

        var result = AccessFactBuilder.Build(input, projection, new(temp.Path, "fixture.accdb", input.OutputFullPath));
        var declaration = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.AccessQueryDeclared);
        var outputDeclaration = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.AccessQueryOutputDeclared);
        var gap = Assert.Single(result.Facts, fact =>
            fact.Properties.GetValueOrDefault("classification") == "AccessQueryOutputSourceUnavailable");

        Assert.Equal(outputIdentity.StableKey, gap.TargetSymbol);
        Assert.Equal(EvidenceTiers.Tier3SyntaxOrTextual, outputDeclaration.EvidenceTier);
        Assert.Equal(declaration.FactId, gap.Properties["supportingFactIds"]);
    }

    [Fact]
    public void Query_output_gap_owner_collisions_fail_closed_without_a_supporting_query_fact()
    {
        using var temp = new TempDirectory();
        var databasePath = Path.Combine(temp.Path, "fixture.accdb");
        File.WriteAllBytes(databasePath, [1, 2, 3, 4]);
        var input = Input(databasePath, Path.Combine(temp.Path, "out"));
        var projection = Projection(input);
        var firstQuery = Assert.Single(projection.Queries);
        var seed = AccessSafeValues.DatabaseIdentitySeed(
            input.RepositoryIdentityHash,
            input.CommitSha,
            input.DatabaseRelativePath,
            input.DatabaseHash);
        var secondQuery = firstQuery with { Identity = AccessSafeValues.Identity(seed, "query", "SecondQuery") };
        var sharedOutputIdentity = AccessSafeValues.Identity(seed, "query-field-shared", "OutputField", 0);
        var sharedOutput = new AccessQueryOutputFieldProjection(sharedOutputIdentity, 0, "long", [], "partial");
        projection = projection with
        {
            Queries =
            [
                firstQuery with { OutputFields = [sharedOutput] },
                secondQuery with { OutputFields = [sharedOutput] }
            ],
            Gaps = [new("AccessQueryOutputSourceUnavailable", "query-output-field", sharedOutputIdentity.StableKey, RuleIds.LegacyAccessQuery)]
        };

        var result = AccessFactBuilder.Build(input, projection, new(temp.Path, "fixture.accdb", input.OutputFullPath));
        var gap = Assert.Single(result.Facts, fact =>
            fact.Properties.GetValueOrDefault("classification") == "AccessQueryOutputSourceUnavailable");

        Assert.Equal(sharedOutputIdentity.StableKey, gap.TargetSymbol);
        Assert.False(gap.Properties.ContainsKey("supportingFactIds"));
        Assert.Equal("query-output-field-owner-unknown", gap.Properties["scopeKind"]);
    }

    [Fact]
    public void Query_parameter_limit_preserves_query_declaration_and_attributes_the_gap()
    {
        using var temp = new TempDirectory();
        var databasePath = Path.Combine(temp.Path, "fixture.accdb");
        File.WriteAllBytes(databasePath, [1, 2, 3, 4]);
        var input = Input(databasePath, Path.Combine(temp.Path, "out"));
        var seed = AccessSafeValues.DatabaseIdentitySeed(input.RepositoryIdentityHash, input.CommitSha, input.DatabaseRelativePath, input.DatabaseHash);
        var identity = AccessSafeValues.Identity(seed, "query", "LimitedQuery");
        var identities = new Dictionary<string, AccessSafeIdentity>(StringComparer.OrdinalIgnoreCase)
        {
            ["LimitedQuery"] = identity
        };
        var gaps = new List<AccessGapProjection>();
        var external = new List<AccessExternalLinkProjection>();
        var reader = new AccessComReader(AccessLimits.Default with { MaxChildrenPerObject = 1 });

        var queries = reader.ReadQueries(
            new FakeQueryDatabase(),
            seed,
            identities,
            new Dictionary<string, IReadOnlyList<(string StableKey, string Kind)>>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, List<AccessTableProjection>>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, Dictionary<string, List<AccessFieldProjection>>>(StringComparer.Ordinal),
            gaps,
            external);

        var query = Assert.Single(queries);
        Assert.Equal(identity.StableKey, query.Identity.StableKey);
        var ownedGap = Assert.Single(gaps, gap => gap.Classification == "AccessQueryParameterCollectionLimit");
        Assert.Equal(identity.StableKey, ownedGap.StableScopeKey);

        var projection = Projection(input) with { Queries = queries, Gaps = gaps };
        var result = AccessFactBuilder.Build(input, projection, new(temp.Path, "fixture.accdb", input.OutputFullPath));
        var declaration = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.AccessQueryDeclared);
        var gapFact = Assert.Single(result.Facts, fact =>
            fact.Properties.GetValueOrDefault("classification") == "AccessQueryParameterCollectionLimit");
        Assert.Equal(declaration.FactId, gapFact.Properties["supportingFactIds"]);
    }

    [Fact]
    public void Internal_raw_field_lookup_supports_redacted_names_and_index_scopes_are_table_specific()
    {
        var databaseSeed = AccessSafeValues.DatabaseIdentitySeed("repo", new string('a', 40), "fixture.accdb", "hash");
        var firstTable = AccessSafeValues.Identity(databaseSeed, "table", "First");
        var secondTable = AccessSafeValues.Identity(databaseSeed, "table", "Second");
        var protectedField = new AccessFieldProjection(AccessSafeValues.Identity(databaseSeed, $"field-{firstTable.StableKey}", "Order ID"), 0, "long", 4, true);
        var lookup = new Dictionary<string, List<AccessFieldProjection>>(StringComparer.OrdinalIgnoreCase) { ["Order ID"] = [protectedField] };

        Assert.Null(protectedField.Identity.DisplayName);
        Assert.True(AccessComReader.UniqueField(lookup, "order id", out var resolved));
        Assert.Same(protectedField, resolved);
        Assert.NotEqual(
            AccessSafeValues.Identity(databaseSeed, $"index-{firstTable.StableKey}", "PrimaryKey").StableKey,
            AccessSafeValues.Identity(databaseSeed, $"index-{secondTable.StableKey}", "PrimaryKey").StableKey);
    }

    [Fact]
    public void Bounded_com_strings_preserve_classification_and_frame_limits_count_utf8_plus_lf()
    {
        var failure = Assert.Throws<AccessScanException>(() =>
            AccessComReader.BoundedString(() => throw new InvalidOperationException("raw COM failure"), 10, "AccessVersionUnavailable"));
        Assert.Equal("AccessVersionUnavailable", failure.Classification);
        Assert.Equal(3, AccessWorkerProtocol.EncodedFrameBytes("é"));
    }

    [Fact]
    public void Worker_failure_frames_allow_only_bounded_classification_tokens()
    {
        var frame = AccessWorkerFrame.Failure("safe-token", $"failure at {Path.GetTempPath()} {ConnectionMarker}");
        var json = JsonSerializer.Serialize(frame, AccessJsonContext.Default.AccessWorkerFrame);

        Assert.Equal("AccessWorkerFailure", frame.Classification);
        Assert.DoesNotContain(ConnectionMarker, json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Path.GetTempPath(), json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Owned_process_decision_rejects_preexisting_stale_wrong_name_and_foreign_session_candidates()
    {
        var workerStart = DateTimeOffset.UtcNow;
        var valid = new OwnedAccessProcess(100, "MSACCESS", 7, workerStart);

        Assert.Equal(valid, AccessProcessOwnership.Accept(valid, new HashSet<int>(), workerStart, 7));
        Assert.Null(AccessProcessOwnership.Accept(valid, new HashSet<int> { 100 }, workerStart, 7));
        Assert.Null(AccessProcessOwnership.Accept(valid with { StartedAtUtc = workerStart.AddMinutes(-1) }, new HashSet<int>(), workerStart, 7));
        Assert.Null(AccessProcessOwnership.Accept(valid with { ProcessName = "notepad" }, new HashSet<int>(), workerStart, 7));
        Assert.Null(AccessProcessOwnership.Accept(valid with { SessionId = 8 }, new HashSet<int>(), workerStart, 7));
    }

    [Fact]
    public void Owned_pid_fallback_after_job_rejection_revalidates_identity_before_termination()
    {
        var startedAt = DateTimeOffset.UtcNow;
        var owned = new OwnedAccessProcess(100, "MSACCESS", 7, startedAt);

        Assert.True(AccessProcessOwnership.CanTerminateFallback(owned, owned));
        Assert.False(AccessProcessOwnership.CanTerminateFallback(owned, owned with { ProcessId = 101 }));
        Assert.False(AccessProcessOwnership.CanTerminateFallback(owned, owned with { ProcessName = "notepad" }));
        Assert.False(AccessProcessOwnership.CanTerminateFallback(owned, owned with { SessionId = 8 }));
        Assert.False(AccessProcessOwnership.CanTerminateFallback(owned, owned with { StartedAtUtc = startedAt.AddMilliseconds(1) }));
    }

    [Fact]
    public async Task Worker_protocol_accepts_heartbeats_and_owned_result_and_rejects_crash_token_and_idle_failures()
    {
        using var temp = new TempDirectory();
        var databasePath = Path.Combine(temp.Path, "fixture.accdb");
        await File.WriteAllBytesAsync(databasePath, [1, 2, 3, 4]);
        var input = Input(databasePath, Path.Combine(temp.Path, "out"));
        var projection = Projection(input) with { AccessProcessId = 42 };
        var token = "protocol-token";
        var owned = new OwnedAccessProcess(42, "MSACCESS", 1, DateTimeOffset.UtcNow);
        var frames = string.Join('\n',
            JsonSerializer.Serialize(AccessWorkerFrame.Heartbeat(token), AccessJsonContext.Default.AccessWorkerFrame),
            JsonSerializer.Serialize(AccessWorkerFrame.Hello(token, 42), AccessJsonContext.Default.AccessWorkerFrame),
            JsonSerializer.Serialize(AccessWorkerFrame.Success(token, projection), AccessJsonContext.Default.AccessWorkerFrame)) + "\n";

        var accepted = await AccessWorkerProtocol.ReadResultAsync(
            new StringReader(frames), token, 1024 * 1024, TimeSpan.FromSeconds(1), _ => owned, _ => { }, CancellationToken.None);
        Assert.Equal(
            JsonSerializer.Serialize(projection, AccessJsonContext.Default.AccessDatabaseProjection),
            JsonSerializer.Serialize(accepted, AccessJsonContext.Default.AccessDatabaseProjection));

        var crashed = await Assert.ThrowsAsync<AccessScanException>(() => AccessWorkerProtocol.ReadResultAsync(
            new StringReader(string.Empty), token, 1024, TimeSpan.FromSeconds(1), _ => owned, _ => { }, CancellationToken.None));
        Assert.Equal("AccessWorkerResultMissing", crashed.Classification);

        var wrongTokenFrame = JsonSerializer.Serialize(AccessWorkerFrame.Heartbeat("wrong"), AccessJsonContext.Default.AccessWorkerFrame);
        var wrongToken = await Assert.ThrowsAsync<AccessScanException>(() => AccessWorkerProtocol.ReadResultAsync(
            new StringReader(wrongTokenFrame), token, 1024, TimeSpan.FromSeconds(1), _ => owned, _ => { }, CancellationToken.None));
        Assert.Equal("AccessWorkerTokenMismatch", wrongToken.Classification);

        // A fully stalled worker is classified by the idle watchdog.
        var idle = await Assert.ThrowsAsync<AccessScanException>(() => AccessWorkerProtocol.ReadResultAsync(
            new BlockingTextReader(), token, 1024, TimeSpan.FromMilliseconds(25), _ => owned, _ => { }, CancellationToken.None));
        Assert.Equal("AccessWorkerHeartbeatTimeout", idle.Classification);

        // A modal COM call can leave the independent heartbeat alive. The total
        // deadline must still win and invoke supervisor containment.
        using var totalDeadline = new CancellationTokenSource(TimeSpan.FromMilliseconds(30));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => AccessWorkerProtocol.ReadResultAsync(
            new EndlessHeartbeatTextReader(token), token, 1024 * 1024, TimeSpan.FromSeconds(1), _ => owned, _ => { }, totalDeadline.Token));
    }

    private static AccessValidatedInput Input(string databasePath, string outputPath)
    {
        var hash = AccessInputValidator.HashFile(databasePath);
        return new(
            Path.GetDirectoryName(databasePath)!,
            "fixture-repo",
            AccessSafeValues.RoleHash("access-repository-identity", "fixture-repo"),
            null,
            "test",
            new string('a', 40),
            databasePath,
            "fixture.accdb",
            hash,
            ".accdb",
            outputPath,
            false);
    }

    private static AccessDatabaseProjection Projection(AccessValidatedInput input)
    {
        var seed = AccessSafeValues.DatabaseIdentitySeed(input.RepositoryIdentityHash, input.CommitSha, input.DatabaseRelativePath, input.DatabaseHash);
        var orders = AccessSafeValues.Identity(seed, "table", "Orders");
        var ordersCase = AccessSafeValues.Identity(seed, "table", "orders");
        var protectedQuery = AccessSafeValues.Identity(seed, "query", SecretMarker);
        var protectedExternal = AccessSafeValues.Identity(seed, "table", "PrivateServer_Password");
        return new(
            "tracemap.access-projection.v1",
            input.DatabaseHash,
            ".accdb",
            "16.0",
            1234,
            false,
            false,
            2,
            [new(orders, [], []), new(ordersCase, [], [])],
            [],
            [new(protectedQuery, "select", AccessSafeValues.RoleHash("access-query-sql", SqlMarker), SqlMarker.Length, "partial", [], [], false, null, null)],
            [new(protectedExternal, "odbc", AccessSafeValues.RoleHash("access-linked-source", ConnectionMarker), "linked-table")],
            [new("AccessQueryDependencyPartial", "query", protectedQuery.StableKey)],
            [new("rowDataRead", "false"), new("executionPerformed", "false")]);
    }

    private static void RunGit(string workingDirectory, params string[] args)
    {
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var arg in args) start.ArgumentList.Add(arg);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("git unavailable");
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, $"git {string.Join(' ', args)} failed: {output} {error}");
    }

    private static string RunGitCapture(string workingDirectory, params string[] args)
    {
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var arg in args) start.ArgumentList.Add(arg);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("git unavailable");
        var output = process.StandardOutput.ReadToEnd();
        _ = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.Equal(0, process.ExitCode);
        return output.Trim();
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "rules", "rule-catalog.yml"))) return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate repository root.");
    }

    public sealed class FakeDaoDatabase(params FakeDaoQuery[] queries)
    {
        public FakeDaoCollection<FakeDaoQuery> QueryDefs { get; } = new(queries);
    }

    public sealed class FakeDaoQuery
    {
        private readonly FakeDaoCollection<FakeDaoField> _fields;

        public FakeDaoQuery(string name, string sql, params FakeDaoField[] fields)
            : this(name, sql, 0, fields) { }

        public FakeDaoQuery(string name, string sql, int type, params FakeDaoField[] fields)
        {
            Name = name;
            SQL = sql;
            Type = type;
            _fields = new(fields);
        }

        public string Name { get; }
        public int Type { get; }
        public string SQL { get; }
        public FakeDaoCollection<FakeDaoParameter> Parameters { get; } = new([]);
        public int FieldsReadCount { get; private set; }
        public FakeDaoCollection<FakeDaoField> Fields
        {
            get
            {
                FieldsReadCount++;
                return _fields;
            }
        }
    }

    public sealed class FakeDaoField(
        string name = "Id",
        string sourceTable = "",
        string sourceField = "",
        bool throwOnName = false,
        bool throwOnSource = false)
    {
        public string Name => throwOnName ? throw new InvalidOperationException() : name;
        public string SourceTable => throwOnSource ? throw new InvalidOperationException() : sourceTable;
        public string SourceField => throwOnSource ? throw new InvalidOperationException() : sourceField;
        public int Type => 4;
    }

    public sealed class FakeDaoParameter;

    public sealed class FakeDaoCollection<T>(params T[] values)
    {
        public int Count => values.Length;
        public T this[int index] => values[index];
    }

    private sealed class BlockingTextReader : TextReader
    {
        public override async ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return null;
        }
    }

    private sealed class EndlessHeartbeatTextReader(string token) : TextReader
    {
        private readonly string _frame = JsonSerializer.Serialize(AccessWorkerFrame.Heartbeat(token), AccessJsonContext.Default.AccessWorkerFrame);

        public override async ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken)
        {
            await Task.Delay(1, cancellationToken);
            return _frame;
        }
    }

    public sealed class FakeQueryDatabase
    {
        public FakeQueryCollection QueryDefs { get; } = new();
    }

    public sealed class FakeQueryCollection
    {
        public int Count => 1;
        public FakeQuery this[int index] => index == 0 ? new() : throw new IndexOutOfRangeException();
    }

    public sealed class FakeQuery
    {
        public string Name => "LimitedQuery";
        public int Type => 0;
        public string SQL => "SELECT 1";
        public FakeParameterCollection Parameters { get; } = new();
    }

    public sealed class FakeParameterCollection
    {
        public int Count => 2;
        public object this[int index] => throw new InvalidOperationException($"Unexpected parameter access {index}.");
    }
}

[CollectionDefinition("AccessGitEnvironment", DisableParallelization = true)]
public sealed class AccessGitEnvironmentCollection;
