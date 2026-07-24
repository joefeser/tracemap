using System.Text.Json;
using System.Text.Json.Nodes;
using TraceMap.Cli;
using TraceMap.Core;
using TraceMap.Reporting;
using TraceMap.Storage;

namespace TraceMap.Tests;

public sealed class SqlValidationSummaryTests
{
    private const string Repository = "synthetic-sql-validation";
    private const string Commit = "0123456789abcdef0123456789abcdef01234567";
    private static readonly DateTimeOffset EvaluatedAt = DateTimeOffset.Parse("2026-07-22T12:00:00.0000000+00:00");
    private static readonly SqlValidationTargetContext Context = new("postgresql", "archive-target", "archive-data", "archive", "manual");

    [Fact]
    public async Task Valid_summary_is_deterministic_and_stays_separate_from_static_evidence()
    {
        using var temp = new TempDirectory();
        var path = await WriteSummaryAsync(temp.Path, "valid.json");
        var first = await ReadAsync([path]);
        var second = await ReadAsync([path]);

        var observation = Assert.Single(first.Observations);
        Assert.Empty(first.Gaps);
        Assert.Equal("postgres.required-extension-available", observation.AssertionCode);
        Assert.Equal("observed-pass", observation.Status);
        Assert.Equal(RuleIds.DatabaseSqlValidationSummaryObservation, observation.RuleId);
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));

        var result = ScanResultWithContext();
        var staticPacket = SqlRunbookPacketBuilder.Build(result);
        var packet = SqlRunbookPacketBuilder.Build(result, first);
        var markdown = SqlRunbookPacketWriter.RenderMarkdown(packet);
        Assert.Equal("sql-operator-runbook-packet/v3", packet.SchemaVersion);
        Assert.Equal(staticPacket.StepGroups[0].Steps[0].Evidence.EvidenceTier, packet.StepGroups[0].Steps[0].Evidence.EvidenceTier);
        Assert.Single(packet.ObservedValidation);
        Assert.Contains("evidence kind `observed-validation`", markdown);
        Assert.DoesNotContain(path, markdown, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Duplicate_and_conflicting_summaries_have_deterministic_gap_behavior()
    {
        using var temp = new TempDirectory();
        var first = await WriteSummaryAsync(temp.Path, "first.json");
        var duplicate = await WriteSummaryAsync(temp.Path, "duplicate.json");
        var exact = await ReadAsync([duplicate, first]);
        Assert.Single(exact.Observations);
        Assert.Contains(exact.Gaps, gap => gap.Code == "DuplicateSummary");

        var conflict = await WriteSummaryAsync(temp.Path, "conflict.json", root =>
            root["assertions"]![0]!["status"] = "observed-fail");
        var conflicting = await ReadAsync([first, conflict]);
        Assert.Empty(conflicting.Observations);
        Assert.Contains(conflicting.Gaps, gap => gap.Code == "ConflictingSummary");

        var assertionConflict = await WriteSummaryAsync(temp.Path, "assertion-conflict.json", root =>
        {
            root["artifactId"] = "validation-fixture-002";
        });
        var assertionConflicting = await ReadAsync([first, assertionConflict]);
        Assert.Empty(assertionConflicting.Observations);
        Assert.Contains(assertionConflicting.Gaps, gap => gap.Code == "ConflictingAssertion");

        var twoAssertions = await WriteSummaryAsync(temp.Path, "two-assertions.json", root =>
            ((JsonArray)root["assertions"]!).Add(new JsonObject { ["code"] = "postgres.target-schema-compatible", ["status"] = "observed-pass" }));
        var twoAssertionConflicts = await WriteSummaryAsync(temp.Path, "two-assertion-conflicts.json", root =>
        {
            root["artifactId"] = "validation-fixture-003";
            root["assertions"]![0]!["status"] = "observed-fail";
            ((JsonArray)root["assertions"]!).Add(new JsonObject { ["code"] = "postgres.target-schema-compatible", ["status"] = "observed-fail" });
        });
        var multipleConflicts = await ReadAsync([twoAssertions, twoAssertionConflicts]);
        Assert.Empty(multipleConflicts.Observations);
        Assert.Equal(2, multipleConflicts.Gaps.Count(gap => gap.Code == "ConflictingAssertion"));
    }

    [Fact]
    public async Task Multiple_unreadable_inputs_preserve_distinct_non_leaking_gaps()
    {
        using var temp = new TempDirectory();
        var result = await ReadAsync([
            Path.Combine(temp.Path, "missing-one.json"),
            Path.Combine(temp.Path, "missing-two.json")
        ]);

        Assert.Equal(2, result.Gaps.Count);
        Assert.Equal(2, result.Gaps.Select(gap => gap.GapId).Distinct(StringComparer.Ordinal).Count());
        Assert.All(result.Gaps, gap => Assert.Equal("MalformedSummary", gap.Code));
        var safe = JsonSerializer.Serialize(result);
        Assert.DoesNotContain(temp.Path, safe, StringComparison.Ordinal);
        Assert.DoesNotContain("missing-one", safe, StringComparison.Ordinal);
        Assert.DoesNotContain("missing-two", safe, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("expired", "ExpiredSummary")]
    [InlineData("zero-window", "InvalidObservationWindow")]
    [InlineData("commit", "SourceMismatch")]
    [InlineData("repository", "SourceMismatch")]
    [InlineData("context", "ContextMismatch")]
    [InlineData("validator", "UnsupportedValidator")]
    [InlineData("assertion", "UnsupportedAssertion")]
    [InlineData("tamper", "DigestMismatch")]
    [InlineData("secret", "MalformedSummary")]
    public async Task Rejected_summaries_emit_safe_rule_backed_gaps(string variant, string expectedCode)
    {
        using var temp = new TempDirectory();
        var path = await WriteSummaryAsync(temp.Path, "summary.json", root =>
        {
            switch (variant)
            {
                case "expired":
                    root["expiresAt"] = "2026-07-22T11:00:00.0000000+00:00";
                    break;
                case "zero-window":
                    root["expiresAt"] = "2026-07-22T10:00:00.0000000+00:00";
                    break;
                case "commit":
                    root["commitSha"] = "1123456789abcdef0123456789abcdef01234567";
                    break;
                case "repository":
                    root["repository"] = "different-repository";
                    break;
                case "context":
                    root["targetContext"]!["databaseRole"] = "source-data";
                    break;
                case "validator":
                    root["validator"]!["version"] = "2.0.0";
                    break;
                case "assertion":
                    root["assertions"]![0]!["code"] = "postgres.unsupported-check";
                    break;
                case "secret":
                    root["operatorNotes"] = "private-password-leak-sentinel";
                    break;
            }
        }, recomputeDigest: variant != "tamper");
        if (variant == "tamper")
        {
            var text = await File.ReadAllTextAsync(path);
            await File.WriteAllTextAsync(path, text.Replace("observed-pass", "observed-fail", StringComparison.Ordinal));
        }

        var result = await ReadAsync([path]);
        var gap = Assert.Single(result.Gaps);
        Assert.Empty(result.Observations);
        Assert.Equal(expectedCode, gap.Code);
        Assert.Equal(RuleIds.DatabaseSqlValidationSummaryGap, gap.RuleId);
        var safe = JsonSerializer.Serialize(result);
        Assert.DoesNotContain("private-password-leak-sentinel", safe, StringComparison.Ordinal);
        Assert.DoesNotContain(path, safe, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Release_review_composes_observations_in_a_separate_section_and_promotes_gaps()
    {
        using var temp = new TempDirectory();
        var before = Path.Combine(temp.Path, "before.sqlite");
        var after = Path.Combine(temp.Path, "after.sqlite");
        var result = ScanResultWithContext();
        SqliteIndexWriter.Write(before, result.Manifest, result.Facts);
        SqliteIndexWriter.Write(after, result.Manifest, result.Facts);
        var valid = await WriteSummaryAsync(temp.Path, "valid.json");

        var review = await ReleaseReviewReporter.BuildReportAsync(new ReleaseReviewOptions(
            before, after, Path.Combine(temp.Path, "review"), Scope: "sql-evidence", SqlValidationSummaryPaths: [valid], SqlValidationAsOf: EvaluatedAt));

        var finding = Assert.Single(review.SqlValidationObservations.Findings);
        Assert.Equal("sqlValidationObservations", finding.Section);
        Assert.Equal("observed-validation", finding.CoverageLabel);
        Assert.Equal(EvidenceTiers.Tier4Unknown, finding.EvidenceTier);
        Assert.Equal("validation-summary/validation-fixture-001.json", finding.FilePath);
        Assert.Equal(0, finding.StartLine);
        Assert.Equal(0, finding.EndLine);
        Assert.NotEmpty(review.SqlEvidence.Findings);
        Assert.True(review.Query.SqlValidationSummaryProvided);

        var expired = await WriteSummaryAsync(temp.Path, "expired.json", root => root["expiresAt"] = "2026-07-22T11:00:00.0000000+00:00");
        var rejected = await ReleaseReviewReporter.BuildReportAsync(new ReleaseReviewOptions(
            before, after, Path.Combine(temp.Path, "rejected"), Scope: "sql-evidence", SqlValidationSummaryPaths: [expired], SqlValidationAsOf: EvaluatedAt));
        Assert.Equal(ReleaseReviewStatuses.Unavailable, rejected.SqlValidationObservations.Status);
        Assert.Contains(rejected.Gaps, gap => gap.GapKind == "ExpiredSummary" && gap.RuleId == RuleIds.DatabaseSqlValidationSummaryGap);
    }

    [Fact]
    public async Task Scan_cli_accepts_explicit_summary_and_emits_only_safe_composed_fields()
    {
        using var temp = new TempDirectory();
        var fixture = Path.Combine(FindRepoRoot(), "samples", "sql-operator-runbook");
        var git = GitMetadataProvider.Detect(fixture);
        var summary = await WriteSummaryAsync(temp.Path, "cli-summary.json", root =>
        {
            root["repository"] = git.RepoName;
            root["commitSha"] = git.CommitSha;
            root["observedAt"] = "2026-01-01T00:00:00.0000000+00:00";
            root["expiresAt"] = "2030-01-01T00:00:00.0000000+00:00";
        });
        var outputPath = Path.Combine(temp.Path, "scan-output");
        var output = new StringWriter();
        var error = new StringWriter();

        var missingAsOfError = new StringWriter();
        var missingAsOf = await TraceMapCommand.RunAsync([
            "scan", "--repo", fixture, "--out", Path.Combine(temp.Path, "missing-as-of"), "--sql-validation-summary", summary
        ], new StringWriter(), missingAsOfError);
        Assert.Equal(1, missingAsOf);
        Assert.Contains("requires --sql-validation-as-of", missingAsOfError.ToString());

        var exitCode = await TraceMapCommand.RunAsync([
            "scan", "--repo", fixture, "--out", outputPath, "--sql-validation-summary", summary,
            "--sql-validation-as-of", "2026-07-22T12:00:00.0000000+00:00"
        ], output, error);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, error.ToString());
        var json = await File.ReadAllTextAsync(Path.Combine(outputPath, "sql-runbook.json"));
        Assert.Contains("\"observedValidation\"", json);
        Assert.Contains("postgres.required-extension-available", json);
        Assert.DoesNotContain(summary, json, StringComparison.Ordinal);
    }

    private static Task<SqlValidationComposition> ReadAsync(IReadOnlyList<string> paths) =>
        SqlValidationSummaryReader.ReadAsync(paths, [new SqlValidationExpectedSource("database", Repository, Commit, EvaluatedAt, [Context])]);

    private static async Task<string> WriteSummaryAsync(string directory, string name, Action<JsonObject>? mutate = null, bool recomputeDigest = true)
    {
        var root = new JsonObject
        {
            ["schemaVersion"] = SqlValidationSummaryReader.SchemaVersion,
            ["artifactId"] = "validation-fixture-001",
            ["repository"] = Repository,
            ["commitSha"] = Commit,
            ["observedAt"] = "2026-07-22T10:00:00.0000000+00:00",
            ["expiresAt"] = "2026-07-22T18:00:00.0000000+00:00",
            ["targetContext"] = new JsonObject
            {
                ["engine"] = Context.Engine,
                ["serverRole"] = Context.ServerRole,
                ["databaseRole"] = Context.DatabaseRole,
                ["schemaRole"] = Context.SchemaRole,
                ["executionMode"] = Context.ExecutionMode
            },
            ["validator"] = new JsonObject { ["id"] = SqlValidationSummaryReader.ValidatorId, ["version"] = SqlValidationSummaryReader.ValidatorVersion },
            ["artifact"] = new JsonObject { ["algorithm"] = SqlValidationSummaryReader.DigestAlgorithm, ["digest"] = new string('0', 64) },
            ["publicClaimLevel"] = SqlValidationSummaryReader.PublicClaimLevel,
            ["assertions"] = new JsonArray(new JsonObject { ["code"] = "postgres.required-extension-available", ["status"] = "observed-pass" }),
            ["limitations"] = new JsonArray("point-in-time-observation", "does-not-establish-continuing-state", "does-not-approve-release", "does-not-establish-safe-to-run")
        };
        mutate?.Invoke(root);
        if (recomputeDigest)
        {
            var unsigned = root.ToJsonString();
            root["artifact"]!["digest"] = SqlValidationSummaryReader.ComputeDigest(unsigned);
        }
        var path = Path.Combine(directory, name);
        await File.WriteAllTextAsync(path, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        return path;
    }

    private static ScanResult ScanResultWithContext()
    {
        var manifest = new ScanManifest("scan-validation", Repository, null, "dev", Commit, "test", EvaluatedAt,
            "Level3SyntaxAnalysis", "Succeeded", [], [], [], []);
        var fact = FactFactory.Create(manifest, FactTypes.SqlExecutionContextDeclared, RuleIds.DatabaseSqlContextDeclaration,
            EvidenceTiers.Tier2Structural, new EvidenceSpan("sql/setup.sql", 1, 1, null, "sql-execution-context", "1.0.0"),
            properties: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["statementOrdinal"] = "1",
                ["engineFamily"] = Context.Engine,
                ["serverRole"] = Context.ServerRole,
                ["databaseRole"] = Context.DatabaseRole,
                ["schemaRole"] = Context.SchemaRole,
                ["executionMode"] = Context.ExecutionMode,
                ["stepKind"] = "extension-setup",
                ["contextClassification"] = "declared",
                ["stopConditions"] = "verify-active-connection",
                ["coverage"] = "complete"
            });
        return new ScanResult(manifest, [fact], []);
    }

    private static string FindRepoRoot()
    {
        var current = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(current))
        {
            if (File.Exists(Path.Combine(current, "rules", "rule-catalog.yml"))) return current;
            current = Directory.GetParent(current)?.FullName;
        }
        throw new DirectoryNotFoundException("Unable to find TraceMap repo root.");
    }
}
