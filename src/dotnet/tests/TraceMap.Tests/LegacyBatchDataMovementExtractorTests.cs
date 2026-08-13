using System.Text.Json;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class LegacyBatchDataMovementExtractorTests
{
    [Fact]
    public void Extractor_inventories_and_composes_bounded_batch_data_movement_evidence()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Default.aspx"), "<%@ Page Language=\"C#\" %>");
        File.WriteAllText(Path.Combine(repo, "Archive.dtsx"), "<DTS:Executable xmlns:DTS=\"www.microsoft.com/SqlServer/Dts\" />");
        const string batchSource = """
            namespace Sample;

            public sealed class ArchiveService : System.ServiceProcess.ServiceBase { }
            public sealed class ArchiveJob : Quartz.IJob { }

            public sealed class BatchRunner
            {
                public static void Main() { }

                [TimerTrigger("%ArchiveSchedule%")]
                public void Run()
                {
                    try
                    {
                        Retry();
                        _ = System.IO.File.ReadAllText("private-file-path");
                        var watcher = new System.IO.FileSystemWatcher("private-watch-path");
                        var command = new System.Data.SqlClient.SqlCommand();
                        command.CommandType = System.Data.CommandType.StoredProcedure;
                        foreach (var item in Items()) command.ExecuteNonQuery();
                        System.Data.SqlClient.SqlBulkCopy bulk = new();
                        bulk.WriteToServer(Items());
                        BeginTransaction();
                        SaveCheckpoint();
                        LogInformation();
                    }
                    catch { }
                }

                public void Schedule() => global::Hangfire.RecurringJob.AddOrUpdate("private-job", () => Run(), "private-cron");
                private void Retry() { }
                private void BeginTransaction() { }
                private void SaveCheckpoint() { }
                private void LogInformation() { }
                private object[] Items() => [];
            }
            """;
        File.WriteAllText(Path.Combine(repo, "Batch.cs"), batchSource);

        var manifest = Manifest();
        var inventory = FileInventory.Collect(repo);
        var config = Fact(manifest, FactTypes.ConfigKeyDeclared, RuleIds.ConfigKey, "Web.config", 2, "ArchiveSchedule", "ArchiveSchedule",
            new() { ["keyPath"] = "ArchiveSchedule" });
        var integrationConfig = Fact(manifest, FactTypes.ConfigKeyDeclared, RuleIds.ConfigKey, "Web.config", 3, "QueueEndpoint", "QueueEndpoint",
            new() { ["keyPath"] = "QueueEndpoint" });
        var message = Fact(manifest, FactTypes.MessagePublisherSurface, RuleIds.MessageSurfacePublish, "Batch.cs", 10, "Run", "queue-hash",
            new() { ["containingMethod"] = "Run", ["containingType"] = "Sample.BatchRunner", ["frameworkFamily"] = "queue-client", ["operationDirection"] = "publish", ["surfaceKind"] = "message-queue" });
        var database = Fact(manifest, FactTypes.DatabaseOperationCandidate, RuleIds.DatabaseOperationCallPattern, "Batch.cs", 10, "global::Sample.BatchRunner.Run()", "ExecuteNonQuery",
            new() { ["operationKind"] = "execute-candidate" });
        var external = Fact(manifest, FactTypes.HttpCallDetected, RuleIds.HttpClientInvocation, "Batch.cs", 10, "global::Sample.BatchRunner.Run()", "SendAsync", new());
        var fileReadLine = Array.FindIndex(batchSource.Split('\n'), line => line.Contains("System.IO.File.ReadAllText", StringComparison.Ordinal)) + 1;
        Assert.True(fileReadLine > 0);
        var semanticFile = Fact(manifest, FactTypes.MethodInvoked, RuleIds.CSharpSemanticMethodInvocation, "Batch.cs", fileReadLine,
            "global::Sample.BatchRunner.Run()", "global::System.IO.File.ReadAllText(string)",
            new() { ["containingMethod"] = "Run", ["containingType"] = "Sample.BatchRunner" });
        var facts = new[] { config, integrationConfig, message, database, external, semanticFile };

        var first = LegacyBatchDataMovementExtractor.Extract(repo, manifest, inventory, facts);
        var second = LegacyBatchDataMovementExtractor.Extract(repo, manifest, inventory, facts);
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));

        var rows = first.Where(fact => fact.FactType == FactTypes.LegacyBatchDataMovementDeclared).ToArray();
        foreach (var expected in new[]
                 {
                     "scheduled-task", "windows-service", "console-job", "file-data-movement", "stored-procedure-batch",
                     "bulk-copy", "message-data-movement", "configuration-integration", "etl-package"
                 })
            Assert.True(
                rows.Any(row => row.Properties.GetValueOrDefault("surfaceKind") == expected),
                $"Missing {expected}; observed: {string.Join(",", rows.Select(row => row.Properties.GetValueOrDefault("surfaceKind")).Distinct())}");

        var scheduled = Assert.Single(rows, row => row.Properties.GetValueOrDefault("mechanism") == "timer-trigger-attribute");
        Assert.Equal("config-reference-matched", scheduled.Properties.GetValueOrDefault("scheduleSource"));
        Assert.Equal("catch-clause", scheduled.Properties.GetValueOrDefault("errorHandlingDeclaration"));
        Assert.Equal("named-call", scheduled.Properties.GetValueOrDefault("retryDeclaration"));
        Assert.Equal("named-call", scheduled.Properties.GetValueOrDefault("checkpointDeclaration"));
        Assert.Equal("named-call", scheduled.Properties.GetValueOrDefault("telemetryDeclaration"));
        Assert.Equal("named-call", scheduled.Properties.GetValueOrDefault("transactionDeclaration"));
        Assert.Contains(database.FactId, scheduled.Properties.GetValueOrDefault("databaseOperationFactIds"), StringComparison.Ordinal);
        Assert.Contains(message.FactId, scheduled.Properties.GetValueOrDefault("messageBoundaryFactIds"), StringComparison.Ordinal);
        Assert.Contains(external.FactId, scheduled.Properties.GetValueOrDefault("externalBoundaryFactIds"), StringComparison.Ordinal);
        Assert.Equal("resolved", scheduled.Properties.GetValueOrDefault("projectResolution"));
        Assert.Equal("src/Legacy.csproj", scheduled.ProjectPath);

        var storedProcedure = Assert.Single(rows, row => row.Properties.GetValueOrDefault("surfaceKind") == "stored-procedure-batch");
        Assert.Equal("present", storedProcedure.Properties.GetValueOrDefault("loopDeclaration"));
        var semanticFileRow = Assert.Single(rows, row => row.Properties.GetValueOrDefault("mechanism") == "compiler-resolved-system-io-call");
        Assert.Equal("named-call", semanticFileRow.Properties.GetValueOrDefault("retryDeclaration"));
        Assert.Equal("catch-clause", semanticFileRow.Properties.GetValueOrDefault("errorHandlingDeclaration"));
        Assert.Equal("named-call", semanticFileRow.Properties.GetValueOrDefault("transactionDeclaration"));
        Assert.Equal("named-call", semanticFileRow.Properties.GetValueOrDefault("checkpointDeclaration"));
        Assert.Equal("named-call", semanticFileRow.Properties.GetValueOrDefault("telemetryDeclaration"));
        Assert.All(rows, row =>
        {
            Assert.Equal(RuleIds.LegacyWebFormsBatchDataMovement, row.RuleId);
            Assert.False(string.IsNullOrWhiteSpace(row.Evidence.ExtractorVersion));
            Assert.True(row.Evidence.StartLine > 0);
        });

        var serialized = JsonSerializer.Serialize(first);
        foreach (var forbidden in new[] { "private-file-path", "private-watch-path", "private-job", "private-cron", "QueueEndpoint" })
            Assert.DoesNotContain(forbidden, serialized, StringComparison.OrdinalIgnoreCase);

        var scan = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "scan")));
        Assert.Contains(scan.Facts, fact => fact.FactType == FactTypes.LegacyBatchDataMovementDeclared);
        var report = TraceMap.Reporting.MarkdownReportWriter.Build(scan);
        Assert.Contains("## Web Forms Batch And Data-Movement Evidence", report, StringComparison.Ordinal);
        Assert.Contains("## Web Forms Batch And Data-Movement Limitations", report, StringComparison.Ordinal);
        foreach (var forbidden in new[] { "private-file-path", "private-watch-path", "private-job", "private-cron" })
            Assert.DoesNotContain(forbidden, report, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Extractor_fails_closed_for_missing_schedule_config_and_ambiguous_project_owners()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Default.aspx"), "<%@ Page Language=\"C#\" %>");
        File.WriteAllText(Path.Combine(repo, "Batch.cs"), """
            public sealed class BatchRunner
            {
                [TimerTrigger("%MissingSchedule%")]
                public void Run() { }
            }
            """);
        var manifest = Manifest() with { BuildStatus = "FailedOrPartial" };
        var inventory = FileInventory.Collect(repo);
        var left = Fact(manifest, FactTypes.MethodDeclared, RuleIds.CSharpSemanticDeclarations, "Batch.cs", 4, "global::BatchRunner.Run()", "Run", new(), "src/Left.csproj");
        var right = Fact(manifest, FactTypes.MethodDeclared, RuleIds.CSharpSemanticDeclarations, "Batch.cs", 4, "global::BatchRunner.Run()", "Run", new(), "src/Right.csproj");

        var facts = LegacyBatchDataMovementExtractor.Extract(repo, manifest, inventory, [left, right]);
        Assert.Contains(facts, fact => fact.FactType == FactTypes.AnalysisGap
            && fact.Properties.GetValueOrDefault("gapKind") == "BatchConfigurationReferenceUnavailable");
        Assert.Contains(facts, fact => fact.FactType == FactTypes.AnalysisGap
            && fact.Properties.GetValueOrDefault("gapKind") == "AmbiguousBatchOwnerProject");
        Assert.Contains(facts, fact => fact.FactType == FactTypes.AnalysisGap
            && fact.Properties.GetValueOrDefault("gapKind") == "ReducedBatchSemanticCoverage");
        var row = Assert.Single(facts, fact => fact.FactType == FactTypes.LegacyBatchDataMovementDeclared);
        Assert.Null(row.ProjectPath);
        Assert.Equal("ambiguous", row.Properties.GetValueOrDefault("projectResolution"));
        Assert.DoesNotContain("MissingSchedule", JsonSerializer.Serialize(facts), StringComparison.Ordinal);
    }

    [Fact]
    public void Extractor_does_not_project_batch_evidence_without_webforms_scope()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Program.cs"), "public static class Program { public static void Main() { } }");

        var facts = LegacyBatchDataMovementExtractor.Extract(repo, Manifest(), FileInventory.Collect(repo), []);
        Assert.Empty(facts);
    }

    [Fact]
    public void Extractor_rejects_unqualified_framework_lookalikes_inside_webforms_scope()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Default.aspx"), "<%@ Page Language=\"C#\" %>");
        File.WriteAllText(Path.Combine(repo, "Lookalikes.cs"), """
            public interface IJob { }
            public class ServiceBase { }
            public class SqlBulkCopy { public void WriteToServer(object value) { } }
            public static class File { public static string ReadAllText(string path) => ""; }

            public sealed class LocalJob : IJob { }
            public sealed class LocalService : ServiceBase { }
            public sealed class Runner
            {
                public void Run()
                {
                    _ = File.ReadAllText("not-a-framework-call");
                    var bulk = new SqlBulkCopy();
                    bulk.WriteToServer(new object());
                }
            }
            """);

        var facts = LegacyBatchDataMovementExtractor.Extract(repo, Manifest(), FileInventory.Collect(repo), []);

        Assert.DoesNotContain(facts, fact => fact.FactType == FactTypes.LegacyBatchDataMovementDeclared);
    }

    [Fact]
    public void Extractor_requires_explicit_stored_procedure_command_type()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Default.aspx"), "<%@ Page Language=\"C#\" %>");
        File.WriteAllText(Path.Combine(repo, "Batch.cs"), """
            public sealed class Batch
            {
                public void Run()
                {
                    var textCommand = new System.Data.SqlClient.SqlCommand();
                    textCommand.ExecuteNonQuery();
                }
            }
            """);

        var facts = LegacyBatchDataMovementExtractor.Extract(repo, Manifest(), FileInventory.Collect(repo), []);

        Assert.DoesNotContain(facts, fact => fact.FactType == FactTypes.LegacyBatchDataMovementDeclared
            && fact.Properties.GetValueOrDefault("surfaceKind") == "stored-procedure-batch");
    }

    [Fact]
    public void Rule_catalog_documents_batch_data_movement_non_claims()
    {
        var catalog = File.ReadAllText(Path.Combine(FindRepoRoot(), "rules", "rule-catalog.yml"));
        var marker = $"  - id: {RuleIds.LegacyWebFormsBatchDataMovement}";
        var start = catalog.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0);
        var next = catalog.IndexOf("\n  - id: ", start + marker.Length, StringComparison.Ordinal);
        var block = next < 0 ? catalog[start..] : catalog[start..next];
        Assert.Contains(FactTypes.LegacyBatchDataMovementDeclared, block, StringComparison.Ordinal);
        Assert.Contains("do not prove that a job is scheduled", block, StringComparison.Ordinal);
        Assert.Contains("architecture selection remains an owner decision", block, StringComparison.Ordinal);
    }

    private static CodeFact Fact(
        ScanManifest manifest,
        string factType,
        string ruleId,
        string file,
        int line,
        string source,
        string target,
        SortedDictionary<string, string> properties,
        string projectPath = "src/Legacy.csproj") =>
        FactFactory.Create(
            manifest,
            factType,
            ruleId,
            factType is FactTypes.MethodDeclared or FactTypes.MethodInvoked or FactTypes.DatabaseOperationCandidate or FactTypes.HttpCallDetected
                ? EvidenceTiers.Tier1Semantic
                : EvidenceTiers.Tier2Structural,
            new(file, line, line, null, "fixture", "fixture/1.0"),
            projectPath,
            source,
            target,
            properties: properties);

    private static ScanManifest Manifest() => new(
        "scan-batch",
        "synthetic",
        null,
        "main",
        "abc123",
        "test/1.0",
        DateTimeOffset.UnixEpoch,
        "Level1SemanticAnalysis",
        "Succeeded",
        [],
        ["src/Legacy.csproj"],
        [],
        []);

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "rules", "rule-catalog.yml"))) return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
