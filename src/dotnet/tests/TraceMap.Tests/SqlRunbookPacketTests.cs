using System.Text.Json;
using TraceMap.Cli;
using TraceMap.Core;
using TraceMap.Reporting;

namespace TraceMap.Tests;

public sealed class SqlRunbookPacketTests
{
    [Fact]
    public void Build_projects_ordered_safe_allowlisted_packet_deterministically()
    {
        var fixture = Path.Combine(FindRepoRoot(), "samples", "sql-operator-runbook");
        var result = Result(fixture);

        var first = SqlRunbookPacketBuilder.Build(result);
        var second = SqlRunbookPacketBuilder.Build(result);
        var json = JsonSerializer.Serialize(first, SqlRunbookPacketWriter.JsonOptions);
        var markdown = SqlRunbookPacketWriter.RenderMarkdown(first);

        Assert.Equal(SqlRunbookPacketBuilder.SchemaVersion, first.SchemaVersion);
        Assert.Equal(json, JsonSerializer.Serialize(second, SqlRunbookPacketWriter.JsonOptions));
        Assert.NotEmpty(first.StepGroups);
        Assert.Contains(first.StepGroups, group => group.ExecutionMode == "scheduled");
        Assert.Contains(first.StepGroups, group => group.ExecutionMode == "validation-only");
        Assert.Contains(first.StepGroups, group => group.ContextTransition);
        Assert.Contains(first.Milestones, milestone => milestone.Kind == "foreign-server" && milestone.State == "intended-by-script");
        Assert.Contains(first.Milestones, milestone => milestone.Kind == "scheduled-job");
        Assert.Contains(first.Milestones, milestone => milestone.State == "validation-step-present");
        Assert.Contains(first.ProtectedSteps, step => step.OwnerHandling == "route-protected-material-through-owner-approved-process");
        Assert.Contains(first.CleanupEvidence, cleanup => cleanup.State == "intended-by-script");
        Assert.All(first.Prerequisites, prerequisite => Assert.Contains(prerequisite.Status, new[] { "present-in-scripts", "missing-evidence", "conflicting-evidence", "unknown", "needs-owner-review" }));
        Assert.Contains("independently-verify-active-client-tab-connection-and-database", markdown);
        Assert.DoesNotContain("```sql", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cron.schedule", markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CREATE SERVER", markdown, StringComparison.OrdinalIgnoreCase);
        AssertSafe(json + markdown);
    }

    [Fact]
    public void Build_preserves_partial_context_permission_validation_and_gap_states()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "partial.sql"), """
            CREATE SERVER fixture_link FOREIGN DATA WRAPPER postgres_fdw;
            CREATE USER MAPPING FOR fixture_role SERVER fixture_link OPTIONS (password '${FIXTURE_SECRET}');
            REVOKE USAGE ON FOREIGN SERVER fixture_link FROM fixture_role;
            """);
        var packet = SqlRunbookPacketBuilder.Build(Result(temp.Path));

        Assert.Equal("reduced", packet.Coverage.Status);
        Assert.Contains(packet.StopConditions, stop => stop.Code == "validation-step-not-established"
            && stop.Evidence.RuleId == RuleIds.DatabaseSqlOperatorRunbookPacket);
        Assert.Contains(packet.Gaps, gap => gap.Category is "context" or "permission" or "archive-link");
        Assert.Contains(packet.OwnerQuestions, question => question.Question.Contains("validation", StringComparison.OrdinalIgnoreCase));
        Assert.All(packet.OwnerQuestions, question =>
        {
            Assert.False(string.IsNullOrWhiteSpace(question.Evidence.RuleId));
            Assert.False(string.IsNullOrWhiteSpace(question.Evidence.FilePath));
            Assert.True(question.Evidence.LineSpan.StartLine > 0);
        });
        Assert.DoesNotContain(packet.Milestones, milestone => milestone.State is "applied" or "healthy" or "succeeded");
    }

    [Fact]
    public void Build_exposes_wrong_context_missing_permission_unknown_order_and_contradictory_evidence()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "a-context-conflict.sql"), """
            -- tracemap-sql-context: engine=postgresql server=source database=source-data schema=application mode=manual step=grant-permission capabilities=grant-permission stops=verify-active-connection
            GRANT USAGE ON FOREIGN SERVER fixture_conflict TO fixture_role;
            -- tracemap-sql-context: engine=postgresql server=archive-target database=archive-data schema=archive mode=manual step=user-mapping capabilities=create-user-mapping stops=secret-owner-review,verify-active-connection
            CREATE USER MAPPING FOR fixture_role SERVER fixture_conflict OPTIONS (password '${FIXTURE_SECRET}');
            """);
        File.WriteAllText(Path.Combine(temp.Path, "b-missing.sql"), """
            -- tracemap-sql-context: engine=postgresql server=archive-target database=archive-data schema=archive mode=manual step=user-mapping capabilities=create-user-mapping stops=secret-owner-review,verify-active-connection
            CREATE USER MAPPING FOR missing_role SERVER missing_server OPTIONS (password '${FIXTURE_SECRET}');
            """);
        File.WriteAllText(Path.Combine(temp.Path, "c-operation.sql"), """
            -- tracemap-sql-context: engine=postgresql server=archive-target database=archive-data schema=archive mode=manual step=user-mapping capabilities=create-user-mapping stops=secret-owner-review,verify-active-connection
            CREATE USER MAPPING FOR ordered_role SERVER ordered_server OPTIONS (password '${FIXTURE_SECRET}');
            """);
        File.WriteAllText(Path.Combine(temp.Path, "z-grant.sql"), """
            -- tracemap-sql-context: engine=postgresql server=archive-target database=archive-data schema=archive mode=manual step=grant-permission capabilities=grant-permission stops=verify-active-connection
            GRANT USAGE ON FOREIGN SERVER ordered_server TO ordered_role;
            """);
        File.WriteAllText(Path.Combine(temp.Path, "contradictory.sql"), """
            GRANT USAGE ON FOREIGN SERVER repeated_server TO repeated_role;
            REVOKE USAGE ON FOREIGN SERVER repeated_server FROM repeated_role;
            CREATE USER MAPPING FOR repeated_role SERVER repeated_server OPTIONS (password '${FIXTURE_SECRET}');
            GRANT USAGE ON FOREIGN SERVER ${DYNAMIC_SERVER} TO dynamic_role;
            """);

        var first = SqlRunbookPacketBuilder.Build(Result(temp.Path));
        var second = SqlRunbookPacketBuilder.Build(Result(temp.Path));

        Assert.Contains(first.Prerequisites, row => row.Status == "conflicting-evidence");
        Assert.Contains(first.Prerequisites, row => row.Status == "missing-evidence");
        Assert.Contains(first.Prerequisites, row => row.Status == "needs-owner-review");
        Assert.Contains(first.Gaps, gap => gap.Category == "permission");
        Assert.Contains(first.StopConditions, stop => stop.Code == "resolve-permission-gap");
        Assert.Equal(JsonSerializer.Serialize(first, SqlRunbookPacketWriter.JsonOptions), JsonSerializer.Serialize(second, SqlRunbookPacketWriter.JsonOptions));
    }

    [Fact]
    public async Task Cli_emits_standard_artifacts_and_safe_packet_outputs()
    {
        using var temp = new TempDirectory();
        var fixture = Path.Combine(FindRepoRoot(), "samples", "sql-operator-runbook");
        var outputPath = Path.Combine(temp.Path, "out");
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await TraceMapCommand.RunAsync(["scan", "--repo", fixture, "--out", outputPath], output, error);

        Assert.Equal(0, exitCode);
        foreach (var name in new[] { "scan-manifest.json", "facts.ndjson", "index.sqlite", "report.md", "logs/analyzer.log", "sql-runbook.md", "sql-runbook.json" })
            Assert.True(File.Exists(Path.Combine(outputPath, name)), name);
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(outputPath, "sql-runbook.json")));
        foreach (var field in new[] { "purpose", "source", "coverage", "stepGroups", "milestones", "prerequisites", "protectedSteps", "validationExpectations", "cleanupEvidence", "stopConditions", "gaps", "ownerQuestions", "limitations" })
            Assert.True(document.RootElement.TryGetProperty(field, out _), field);
        var evidence = document.RootElement.GetProperty("stepGroups")[0].GetProperty("steps")[0].GetProperty("evidence");
        Assert.Equal(document.RootElement.GetProperty("source").GetProperty("commitSha").GetString(), evidence.GetProperty("commitSha").GetString());
        Assert.True(evidence.GetProperty("lineSpan").TryGetProperty("startLine", out _));
        var allText = string.Join('\n', Directory.EnumerateFiles(outputPath, "*", SearchOption.AllDirectories)
            .Where(path => !path.EndsWith(".sqlite", StringComparison.Ordinal)).Select(File.ReadAllText));
        Assert.Contains("## SQL Operator Runbook Packet", allText);
        AssertNoLeaks(allText);
        AssertSafe(await File.ReadAllTextAsync(Path.Combine(outputPath, "sql-runbook.md"))
            + await File.ReadAllTextAsync(Path.Combine(outputPath, "sql-runbook.json")));
    }

    [Fact]
    public void Build_is_null_safe_and_reduces_unknown_commit_identity()
    {
        var fixture = Path.Combine(FindRepoRoot(), "samples", "sql-operator-runbook");
        var original = Result(fixture);
        var facts = original.Facts.Select((fact, index) => index == 0 && fact.Evidence is not null
            ? fact with { Evidence = fact.Evidence with { FilePath = null! } }
            : fact).ToArray();
        var manifest = original.Manifest with { BuildStatus = null!, CommitSha = "unknown" };

        var packet = SqlRunbookPacketBuilder.Build(new ScanResult(manifest, facts, original.Inventory));

        Assert.Equal("reduced", packet.Coverage.Status);
        Assert.Contains("build", packet.Coverage.ReducedComponents);
        Assert.Contains("commit-identity", packet.Coverage.ReducedComponents);
        Assert.Contains(packet.StopConditions, stop => stop.Code == "commit-identity-not-established"
            && stop.Evidence.RuleId == RuleIds.DatabaseSqlOperatorRunbookPacket);
    }

    [Fact]
    public async Task Cli_does_not_emit_runbook_artifacts_without_supported_sql_evidence()
    {
        using var temp = new TempDirectory();
        var repoPath = Path.Combine(temp.Path, "repo");
        var outputPath = Path.Combine(temp.Path, "out");
        Directory.CreateDirectory(repoPath);
        File.WriteAllText(Path.Combine(repoPath, "README.md"), "public-safe fixture");
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await TraceMapCommand.RunAsync(["scan", "--repo", repoPath, "--out", outputPath], output, error);

        Assert.Equal(0, exitCode);
        Assert.False(File.Exists(Path.Combine(outputPath, "sql-runbook.md")));
        Assert.False(File.Exists(Path.Combine(outputPath, "sql-runbook.json")));
        Assert.DoesNotContain("## SQL Operator Runbook Packet", await File.ReadAllTextAsync(Path.Combine(outputPath, "report.md")));
    }

    private static void AssertSafe(string value)
    {
        AssertNoLeaks(value);
        Assert.DoesNotContain("safe to run", value, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("permissions satisfied", value, StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertNoLeaks(string value)
    {
        foreach (var sentinel in new[] { "private-host-leak-sentinel", "private-password-leak-sentinel", "raw-scheduled-command-leak-sentinel" })
            Assert.DoesNotContain(sentinel, value, StringComparison.OrdinalIgnoreCase);
    }

    private static ScanResult Result(string repoPath)
    {
        var manifest = new ScanManifest("scan-runbook-test", "synthetic-sql-runbook", null, "test", "0123456789abcdef", "test", DateTimeOffset.UnixEpoch, "Level3SyntaxAnalysis", "NotRun", [], [], [], []);
        return new ScanResult(manifest, SqlExecutionContextExtractor.Extract(repoPath, manifest, FileInventory.Collect(repoPath)), FileInventory.Collect(repoPath));
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
