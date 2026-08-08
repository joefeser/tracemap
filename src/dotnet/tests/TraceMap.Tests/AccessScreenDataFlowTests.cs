using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
using TraceMap.Access.Cli;
using TraceMap.Core;
using TraceMap.Reporting;
using TraceMap.Storage;

namespace TraceMap.Tests;

public sealed class AccessScreenDataFlowTests
{
    private const string Commit = "2222222222222222222222222222222222222222";
    private const string ProtectedMarker = "Customer_Secret_Form_91827";

    [Fact]
    public void Builder_composes_bounded_branching_cycle_and_gap_evidence_deterministically()
    {
        var facts = FlowFacts();

        var first = AccessScreenDataFlowReporter.Build("synthetic", Commit, facts, 12, 100, 100);
        var second = AccessScreenDataFlowReporter.Build("synthetic", Commit, facts.Reverse().ToArray(), 12, 100, 100);

        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
        Assert.StartsWith("repo-", first.RepositoryId, StringComparison.Ordinal);
        Assert.Equal(Commit, first.CommitSha);
        Assert.Contains(first.Roots, root => root.RootKind == "ui-root-candidate");
        Assert.Contains(first.Gaps, gap => gap.Classification == "AccessStartupIdentityUnavailable");
        Assert.Contains(first.Gaps, gap => gap.Classification == "AccessFlowCycleDetected");
        Assert.Contains(first.Gaps, gap => gap.Classification == "AccessFlowDynamicOrUnresolvedTarget");
        Assert.Contains(first.Gaps, gap => gap.Classification == "AccessFlowTargetDeclarationMissing");
        Assert.Contains(first.Paths, path => path.TerminalKind == "report");
        Assert.Contains(first.Paths, path => path.TerminalKind == "external-boundary");
        Assert.Contains(first.Paths, path => path.TerminalKind == "cycle");
        Assert.All(first.Paths.SelectMany(path => path.Edges), edge =>
        {
            Assert.StartsWith("fact-", edge.Evidence.FactId, StringComparison.Ordinal);
            Assert.StartsWith("legacy.access.", edge.Evidence.RuleId, StringComparison.Ordinal);
            Assert.Equal(Commit, edge.Evidence.CommitSha);
            Assert.Equal("fixture.accdb", edge.Evidence.FilePath);
            Assert.NotEmpty(edge.Evidence.ExtractorVersion);
        });
        Assert.All(first.Paths, path =>
        {
            Assert.NotEmpty(path.SupportingFactIds);
            Assert.NotEmpty(path.RuleIds);
            Assert.Contains(path.EvidenceTier, new[]
            {
                EvidenceTiers.Tier2Structural,
                EvidenceTiers.Tier3SyntaxOrTextual,
                EvidenceTiers.Tier4Unknown
            });
        });
        Assert.All(first.Gaps, gap =>
        {
            Assert.Equal(Commit, gap.CommitSha);
            Assert.NotEmpty(gap.FilePath);
            Assert.True(gap.StartLine > 0);
            Assert.True(gap.EndLine >= gap.StartLine);
            Assert.NotEmpty(gap.ExtractorVersion);
        });
        var depthBounded = AccessScreenDataFlowReporter.Build("synthetic", Commit, facts, 1, 100, 100);
        Assert.True(depthBounded.Summary.Truncated);
        Assert.Contains(depthBounded.Gaps, gap => gap.Classification == "AccessFlowDepthLimitReached");
        var pathBounded = AccessScreenDataFlowReporter.Build("synthetic", Commit, facts, 12, 1, 100);
        Assert.True(pathBounded.Summary.Truncated);
        Assert.Contains(pathBounded.Gaps, gap => gap.Classification == "AccessFlowPathLimitReached");
    }

    [Fact]
    public void Builder_labels_count_only_input_and_bounds_as_partial()
    {
        var countOnly = new[]
        {
            Fact(
                "fact-count-only",
                FactTypes.AnalyzerCapabilityDiagnostic,
                RuleIds.LegacyAccessCoverageGap,
                EvidenceTiers.Tier4Unknown,
                null,
                "access-database-count-only",
                ("coverageLabel", "count-observed-source-unavailable"))
        };

        var report = AccessScreenDataFlowReporter.Build("synthetic", Commit, countOnly, 1, 1, 2);

        Assert.Equal("partial", report.Coverage);
        Assert.Empty(report.Paths);
        Assert.Contains(report.Gaps, gap => gap.Classification == "AccessDesignFlowEvidenceUnavailable");
        Assert.Contains(report.Gaps, gap => gap.Classification == "AccessFlowGapLimitReached");
        Assert.True(report.Summary.Truncated);
    }

    [Fact]
    public void Builder_does_not_treat_host_context_bindings_as_missing_flow_targets()
    {
        const string reportKey = "access-report-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        var facts = new[]
        {
            Fact("fact-report-context", FactTypes.AccessReportDeclared, RuleIds.LegacyAccessUiSurface,
                EvidenceTiers.Tier2Structural, null, reportKey, ("coverageLabel", "structured-design-observed")),
            Fact("fact-page-context", FactTypes.AccessBindingDeclared, RuleIds.LegacyAccessBinding,
                EvidenceTiers.Tier3SyntaxOrTextual, reportKey, null,
                ("targetKind", "context"), ("coverageLabel", "complete"), ("runtimeValueCoverage", "partial"))
        };

        var report = AccessScreenDataFlowReporter.Build("synthetic", Commit, facts, 12, 100, 100);

        Assert.DoesNotContain(report.Gaps, gap => gap.Classification == "AccessFlowTargetUnavailable"
            && gap.SupportingFactIds.Contains("fact-page-context", StringComparer.Ordinal));
    }

    [Fact]
    public void Builder_marks_partial_edges_normalizes_tiers_and_drops_unsafe_limitations()
    {
        var facts = FlowFacts();
        var partial = facts.Single(fact => fact.FactId == "fact-event") with
        {
            EvidenceTier = "PrivateTierValue",
            Properties = new SortedDictionary<string, string>(
                facts.Single(fact => fact.FactId == "fact-event").Properties
                    .ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal),
                StringComparer.Ordinal)
            {
                ["coverageLabel"] = "partial",
                ["limitations"] = "static-evidence-only;/private/customer/secret.sql"
            }
        };
        facts = facts.Where(fact => fact.FactId != partial.FactId).Append(partial).ToArray();

        var report = AccessScreenDataFlowReporter.Build("synthetic", Commit, facts, 12, 100, 100);

        Assert.Equal("partial", report.Coverage);
        Assert.Contains(report.Paths, path => path.Classification == "PartialStaticCandidateTrail");
        Assert.DoesNotContain(report.Paths.SelectMany(path => path.EvidenceTiers), tier => tier == "PrivateTierValue");
        Assert.DoesNotContain(report.Paths.SelectMany(path => path.Limitations), value => value.Contains("private", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Builder_bounds_queued_branches_before_terminal_paths_are_emitted()
    {
        const string form = "access-form-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        var facts = new List<CodeFact>
        {
            Fact("fact-form-root", FactTypes.AccessFormDeclared, RuleIds.LegacyAccessUiSurface, EvidenceTiers.Tier2Structural, null, form,
                ("coverageLabel", "complete"))
        };
        var source = form;
        for (var level = 1; level <= 64; level++)
        {
            var target = level == 64
                ? "access-report-bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"
                : $"access-vba-procedure-{level:x32}";
            facts.Add(Fact(
                $"fact-declaration-{level}",
                level == 64 ? FactTypes.AccessReportDeclared : FactTypes.AccessVbaProcedureDeclared,
                level == 64 ? RuleIds.LegacyAccessUiSurface : RuleIds.LegacyAccessVba,
                EvidenceTiers.Tier3SyntaxOrTextual,
                null,
                target,
                ("coverageLabel", "complete")));
            facts.Add(Fact($"fact-branch-{level}-a", FactTypes.AccessNavigationCandidate, RuleIds.LegacyAccessVba, EvidenceTiers.Tier3SyntaxOrTextual, source, target,
                ("targetKind", level == 64 ? "report" : "procedure"), ("coverageLabel", "complete")));
            facts.Add(Fact($"fact-branch-{level}-b", FactTypes.AccessNavigationCandidate, RuleIds.LegacyAccessVba, EvidenceTiers.Tier3SyntaxOrTextual, source, target,
                ("targetKind", level == 64 ? "report" : "procedure"), ("coverageLabel", "complete")));
            source = target;
        }

        var report = AccessScreenDataFlowReporter.Build("synthetic", Commit, facts, 64, 1, 100);

        Assert.Single(report.Paths);
        Assert.Equal(64, report.Paths[0].Depth);
        Assert.True(report.Summary.Truncated);
        Assert.Contains(report.Gaps, gap => gap.Classification == "AccessFlowPathLimitReached");
    }

    [Fact]
    public void Builder_marks_paths_through_missing_declarations_partial()
    {
        const string form = "access-form-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        const string missing = "access-vba-procedure-bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        var facts = new[]
        {
            Fact("fact-form-root", FactTypes.AccessFormDeclared, RuleIds.LegacyAccessUiSurface, EvidenceTiers.Tier2Structural, null, form,
                ("coverageLabel", "complete")),
            Fact("fact-missing-target", FactTypes.AccessEventBindingCandidate, RuleIds.LegacyAccessEventBinding, EvidenceTiers.Tier3SyntaxOrTextual, form, missing,
                ("targetKind", "procedure"), ("coverageLabel", "complete"))
        };

        var report = AccessScreenDataFlowReporter.Build("synthetic", Commit, facts, 12, 100, 100);

        var path = Assert.Single(report.Paths);
        Assert.Equal("PartialStaticCandidateTrail", path.Classification);
        Assert.Contains(report.Gaps, gap => gap.Classification == "AccessFlowTargetDeclarationMissing");
    }

    [Fact]
    public void Builder_grounds_query_output_gaps_with_the_declared_output_chain()
    {
        const string query = "access-query-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        const string output = "access-query-field-bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        var facts = new[]
        {
            Fact(
                "fact-query-declaration",
                FactTypes.AccessQueryDeclared,
                RuleIds.LegacyAccessQuery,
                EvidenceTiers.Tier2Structural,
                null,
                query,
                ("coverageLabel", "partial")),
            Fact(
                "fact-output-declaration",
                FactTypes.AccessQueryOutputDeclared,
                RuleIds.LegacyAccessQuery,
                EvidenceTiers.Tier2Structural,
                query,
                output,
                ("coverageLabel", "partial")),
            Fact(
                "fact-output-gap",
                FactTypes.AnalysisGap,
                RuleIds.LegacyAccessQuery,
                EvidenceTiers.Tier4Unknown,
                null,
                output,
                ("classification", "AccessQueryOutputSourceUnavailable"),
                ("scopeKind", "query-output-field"))
        };

        var report = AccessScreenDataFlowReporter.Build("synthetic", Commit, facts, 12, 100, 100);

        var gap = Assert.Single(
            report.Gaps,
            item => item.Classification == "AccessQueryOutputSourceUnavailable");
        Assert.Equal(
            ["fact-output-declaration", "fact-output-gap", "fact-query-declaration"],
            gap.SupportingFactIds);
    }

    [Fact]
    public void Builder_does_not_reconstruct_query_owners_for_ambiguous_output_gaps()
    {
        const string firstQuery = "access-query-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        const string secondQuery = "access-query-bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        const string output = "access-query-field-cccccccccccccccccccccccccccccccc";
        var facts = new[]
        {
            Fact("fact-query-one", FactTypes.AccessQueryDeclared, RuleIds.LegacyAccessQuery,
                EvidenceTiers.Tier2Structural, null, firstQuery, ("coverageLabel", "partial")),
            Fact("fact-query-two", FactTypes.AccessQueryDeclared, RuleIds.LegacyAccessQuery,
                EvidenceTiers.Tier2Structural, null, secondQuery, ("coverageLabel", "partial")),
            Fact("fact-output-one", FactTypes.AccessQueryOutputDeclared, RuleIds.LegacyAccessQuery,
                EvidenceTiers.Tier2Structural, firstQuery, output, ("coverageLabel", "partial")),
            Fact("fact-output-two", FactTypes.AccessQueryOutputDeclared, RuleIds.LegacyAccessQuery,
                EvidenceTiers.Tier2Structural, secondQuery, output, ("coverageLabel", "partial")),
            Fact("fact-output-gap", FactTypes.AnalysisGap, RuleIds.LegacyAccessQuery,
                EvidenceTiers.Tier4Unknown, null, output,
                ("classification", "AccessQueryOutputSourceUnavailable"),
                ("scopeKind", "query-output-field-owner-unknown"))
        };

        var report = AccessScreenDataFlowReporter.Build("synthetic", Commit, facts, 12, 100, 100);

        var gap = Assert.Single(report.Gaps, item =>
            item.Classification == "AccessQueryOutputSourceUnavailable");
        Assert.Equal(["fact-output-gap"], gap.SupportingFactIds);
    }

    [Fact]
    public void Builder_prefers_valid_persisted_gap_supporting_fact_ids()
    {
        const string binding = "access-binding-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        var facts = new[]
        {
            Fact("fact-binding-declaration", FactTypes.AccessBindingDeclared, RuleIds.LegacyAccessBinding,
                EvidenceTiers.Tier3SyntaxOrTextual, null, null,
                ("stableBindingKey", binding),
                ("coverageLabel", "partial")),
            Fact("fact-binding-gap", FactTypes.AnalysisGap, RuleIds.LegacyAccessBinding,
                EvidenceTiers.Tier4Unknown, null, binding,
                ("classification", "AccessBindingInlineSqlProjectionPartial"),
                ("scopeKind", "binding"),
                ("supportingFactIds", "fact-binding-declaration"))
        };

        var report = AccessScreenDataFlowReporter.Build("synthetic", Commit, facts, 12, 100, 100);

        var gap = Assert.Single(report.Gaps, item =>
            item.Classification == "AccessBindingInlineSqlProjectionPartial");
        Assert.Equal(["fact-binding-declaration", "fact-binding-gap"], gap.SupportingFactIds);
    }

    [Fact]
    public void Builder_preserves_supplemental_binding_provenance_declared_by_the_binding()
    {
        const string binding = "access-binding-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        var facts = new[]
        {
            Fact("fact-binding-support", FactTypes.AccessQueryCrosstabLineageCandidate, RuleIds.LegacyAccessQuery,
                EvidenceTiers.Tier3SyntaxOrTextual, null, null, ("coverageLabel", "partial")),
            Fact("fact-binding-declaration", FactTypes.AccessBindingDeclared, RuleIds.LegacyAccessBinding,
                EvidenceTiers.Tier3SyntaxOrTextual, null, null,
                ("stableBindingKey", binding),
                ("coverageLabel", "partial"),
                ("supportingFactIds", "fact-binding-support")),
            Fact("fact-binding-gap", FactTypes.AnalysisGap, RuleIds.LegacyAccessBinding,
                EvidenceTiers.Tier4Unknown, null, binding,
                ("classification", "AccessBindingInlineSqlProjectionPartial"),
                ("scopeKind", "binding"),
                ("supportingFactIds", "fact-binding-declaration;fact-binding-support"))
        };

        var report = AccessScreenDataFlowReporter.Build("synthetic", Commit, facts, 12, 100, 100);

        var gap = Assert.Single(report.Gaps, item =>
            item.Classification == "AccessBindingInlineSqlProjectionPartial");
        Assert.Equal(
            ["fact-binding-declaration", "fact-binding-gap", "fact-binding-support"],
            gap.SupportingFactIds);
    }

    [Fact]
    public void Builder_rejects_existing_but_unrelated_persisted_gap_support()
    {
        const string binding = "access-binding-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        const string unrelatedQuery = "access-query-bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        var facts = new[]
        {
            Fact("fact-unrelated-query", FactTypes.AccessQueryDeclared, RuleIds.LegacyAccessQuery,
                EvidenceTiers.Tier2Structural, null, unrelatedQuery, ("coverageLabel", "complete")),
            Fact("fact-binding-gap", FactTypes.AnalysisGap, RuleIds.LegacyAccessBinding,
                EvidenceTiers.Tier4Unknown, null, binding,
                ("classification", "AccessBindingInlineSqlProjectionPartial"),
                ("scopeKind", "binding"),
                ("supportingFactIds", "fact-unrelated-query"))
        };

        var report = AccessScreenDataFlowReporter.Build("synthetic", Commit, facts, 12, 100, 100);

        var gap = Assert.Single(report.Gaps, item =>
            item.Classification == "AccessBindingInlineSqlProjectionPartial");
        Assert.Equal(["fact-binding-gap"], gap.SupportingFactIds);
    }

    [Fact]
    public void Builder_reconstructs_gap_support_when_persisted_ids_are_unsafe()
    {
        const string query = "access-query-aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        var facts = new[]
        {
            Fact("fact-query-declaration", FactTypes.AccessQueryDeclared, RuleIds.LegacyAccessQuery,
                EvidenceTiers.Tier2Structural, null, query, ("coverageLabel", "partial")),
            Fact("fact-query-gap", FactTypes.AnalysisGap, RuleIds.LegacyAccessQuery,
                EvidenceTiers.Tier4Unknown, null, query,
                ("classification", "AccessQueryOutputProjectionPartial"),
                ("scopeKind", "query"),
                ("supportingFactIds", "../../private-source"))
        };

        var report = AccessScreenDataFlowReporter.Build("synthetic", Commit, facts, 12, 100, 100);

        var gap = Assert.Single(report.Gaps, item => item.Classification == "AccessQueryOutputProjectionPartial");
        Assert.Equal(["fact-query-declaration", "fact-query-gap"], gap.SupportingFactIds);
    }

    [Theory]
    [InlineData(@"C:\SecretDriveMarker91827\db.accdb", "SecretDriveMarker91827")]
    [InlineData(@"\\SecretServerMarker91827\share\db.accdb", "SecretServerMarker91827")]
    public void Builder_redacts_windows_absolute_evidence_paths_cross_platform(string unsafePath, string protectedMarker)
    {
        var facts = FlowFacts()
            .Select(fact => fact.FactId == "fact-event"
                ? fact with { Evidence = fact.Evidence with { FilePath = unsafePath } }
                : fact)
            .ToArray();

        var report = AccessScreenDataFlowReporter.Build("synthetic", Commit, facts, 12, 100, 100);

        Assert.Contains(
            report.Paths.SelectMany(path => path.Edges),
            edge => edge.Evidence.FactId == "fact-event" && edge.Evidence.FilePath == "unavailable");
        Assert.DoesNotContain(protectedMarker, JsonSerializer.Serialize(report), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-commit")]
    public void Builder_rejects_missing_scan_identity(string commit)
    {
        Assert.Throws<InvalidDataException>(() =>
            AccessScreenDataFlowReporter.Build("synthetic", commit, FlowFacts(), 12, 100, 100));
        Assert.Throws<InvalidDataException>(() =>
            AccessScreenDataFlowReporter.Build("", Commit, FlowFacts(), 12, 100, 100));
    }

    [Fact]
    public async Task Cli_writes_safe_deterministic_markdown_and_json_from_standard_index()
    {
        using var temp = new TempDirectory();
        var index = Path.Combine(temp.Path, "index.sqlite");
        var manifest = new ScanManifest(
            "scan-access-flow",
            "synthetic",
            null,
            "dev",
            Commit,
            "tracemap-access/0.1.0",
            DateTimeOffset.UnixEpoch,
            "Level1SemanticAnalysisReduced",
            "FailedOrPartial",
            [],
            [],
            [],
            []);
        SqliteIndexWriter.Write(index, manifest, FlowFacts());
        var indexHash = Sha256(File.ReadAllBytes(index));
        var output = Path.Combine(temp.Path, "flow");
        var secondOutput = Path.Combine(temp.Path, "flow-second");
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exit = await AccessCommand.RunAsync(
            ["flow", "--index", index, "--out", output, "--max-depth", "12", "--max-paths", "100"],
            stdout,
            stderr);

        Assert.Equal(0, exit);
        var secondExit = await AccessCommand.RunAsync(
            ["flow", "--index", index, "--out", secondOutput, "--max-depth", "12", "--max-paths", "100"],
            TextWriter.Null,
            stderr);
        Assert.Equal(0, secondExit);
        Assert.Equal(indexHash, Sha256(File.ReadAllBytes(index)));
        Assert.Equal(
            Directory.EnumerateFiles(output).OrderBy(Path.GetFileName).Select(file => Sha256(File.ReadAllBytes(file))),
            Directory.EnumerateFiles(secondOutput).OrderBy(Path.GetFileName).Select(file => Sha256(File.ReadAllBytes(file))));
        Assert.True(File.Exists(Path.Combine(output, "access-flow.md")));
        Assert.True(File.Exists(Path.Combine(output, "access-flow.json")));
        Assert.Contains("Candidate paths:", stdout.ToString(), StringComparison.Ordinal);
        Assert.Empty(stderr.ToString());
        foreach (var file in Directory.EnumerateFiles(output))
        {
            var text = Encoding.UTF8.GetString(File.ReadAllBytes(file));
            Assert.DoesNotContain(ProtectedMarker, text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(temp.Path, text, StringComparison.Ordinal);
            Assert.DoesNotContain("SELECT * FROM Customers", text, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("no", text, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static CodeFact[] FlowFacts()
    {
        const string form = "access-form-11111111111111111111111111111111";
        const string control = "access-control-22222222222222222222222222222222";
        const string procedure = "access-vba-procedure-33333333333333333333333333333333";
        const string missingProcedure = "access-vba-procedure-99999999999999999999999999999999";
        const string query = "access-query-44444444444444444444444444444444";
        const string table = "access-table-55555555555555555555555555555555";
        const string tableField = "access-field-12121212121212121212121212121212";
        const string queryOutput = "access-query-field-13131313131313131313131313131313";
        const string report = "access-report-66666666666666666666666666666666";
        return
        [
            Fact("fact-form", FactTypes.AccessFormDeclared, RuleIds.LegacyAccessUiSurface, EvidenceTiers.Tier2Structural, null, form,
                ("coverageLabel", "structured-design-observed"), ("objectName", ProtectedMarker)),
            Fact("fact-control", FactTypes.AccessControlDeclared, RuleIds.LegacyAccessUiSurface, EvidenceTiers.Tier2Structural, form, control,
                ("coverageLabel", "structured-design-observed")),
            Fact("fact-procedure", FactTypes.AccessVbaProcedureDeclared, RuleIds.LegacyAccessVba, EvidenceTiers.Tier3SyntaxOrTextual, "access-vba-module-77777777777777777777777777777777", procedure,
                ("coverageLabel", "bounded-textual-design")),
            Fact("fact-query", FactTypes.AccessQueryDeclared, RuleIds.LegacyAccessQuery, EvidenceTiers.Tier2Structural, null, query,
                ("coverageLabel", "complete"), ("rawSql", "SELECT * FROM Customers")),
            Fact("fact-table", FactTypes.LegacyDataEntityDeclared, RuleIds.LegacyAccessSchema, EvidenceTiers.Tier2Structural, null, table,
                ("coverageLabel", "complete")),
            Fact("fact-table-field", FactTypes.LegacyDataColumnDeclared, RuleIds.LegacyAccessSchema, EvidenceTiers.Tier2Structural, table, tableField,
                ("coverageLabel", "complete")),
            Fact("fact-query-output", FactTypes.AccessQueryOutputDeclared, RuleIds.LegacyAccessQuery, EvidenceTiers.Tier2Structural, query, queryOutput,
                ("coverageLabel", "complete")),
            Fact("fact-query-output-source", FactTypes.AccessQueryOutputSourceCandidate, RuleIds.LegacyAccessQuery, EvidenceTiers.Tier3SyntaxOrTextual, queryOutput, tableField,
                ("targetKind", "field"), ("coverageLabel", "complete")),
            Fact("fact-control-output", FactTypes.AccessBindingDeclared, RuleIds.LegacyAccessBinding, EvidenceTiers.Tier2Structural, control, queryOutput,
                ("targetKind", "field"), ("coverageLabel", "complete")),
            Fact("fact-report", FactTypes.AccessReportDeclared, RuleIds.LegacyAccessUiSurface, EvidenceTiers.Tier2Structural, null, report,
                ("coverageLabel", "structured-design-observed")),
            Fact("fact-event", FactTypes.AccessEventBindingCandidate, RuleIds.LegacyAccessEventBinding, EvidenceTiers.Tier3SyntaxOrTextual, control, procedure,
                ("coverageLabel", "exact-same-module")),
            Fact("fact-missing-event", FactTypes.AccessEventBindingCandidate, RuleIds.LegacyAccessEventBinding, EvidenceTiers.Tier3SyntaxOrTextual, form, missingProcedure,
                ("coverageLabel", "partial")),
            Fact("fact-navigation-query", FactTypes.AccessNavigationCandidate, RuleIds.LegacyAccessVba, EvidenceTiers.Tier3SyntaxOrTextual, procedure, query,
                ("targetKind", "query"), ("coverageLabel", "bounded-static-candidate")),
            Fact("fact-navigation-report", FactTypes.AccessNavigationCandidate, RuleIds.LegacyAccessVba, EvidenceTiers.Tier3SyntaxOrTextual, procedure, report,
                ("targetKind", "report"), ("coverageLabel", "bounded-static-candidate")),
            Fact("fact-navigation-cycle", FactTypes.AccessNavigationCandidate, RuleIds.LegacyAccessVba, EvidenceTiers.Tier3SyntaxOrTextual, procedure, procedure,
                ("targetKind", "procedure"), ("coverageLabel", "bounded-static-candidate")),
            Fact("fact-navigation-dynamic", FactTypes.AccessNavigationCandidate, RuleIds.LegacyAccessVba, EvidenceTiers.Tier3SyntaxOrTextual, procedure, null,
                ("targetKind", "unknown"), ("coverageLabel", "partial")),
            Fact("fact-query-table", FactTypes.AccessQueryDependencyCandidate, RuleIds.LegacyAccessQuery, EvidenceTiers.Tier3SyntaxOrTextual, query, table,
                ("targetKind", "table"), ("coverageLabel", "complete")),
            Fact("fact-external", FactTypes.AccessExternalLinkDeclared, RuleIds.LegacyAccessExternalLink, EvidenceTiers.Tier2Structural, table, null,
                ("boundaryKind", "odbc"), ("coverageLabel", "hash-only-boundary"))
        ];
    }

    private static CodeFact Fact(
        string id,
        string factType,
        string ruleId,
        string tier,
        string? source,
        string? target,
        params (string Key, string Value)[] values)
    {
        var properties = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["limitations"] = "static-evidence-only;no-execution"
        };
        foreach (var (key, value) in values) properties[key] = value;
        return new(
            id,
            "scan-access-flow",
            "synthetic",
            Commit,
            null,
            factType,
            ruleId,
            tier,
            source,
            target,
            null,
            new("fixture.accdb", 1, 1, null, "AccessSourceNeutralDesignEvidence", "access-design-evidence/0.1.0"),
            properties);
    }

    private static string Sha256(byte[] value) =>
        Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();
}
