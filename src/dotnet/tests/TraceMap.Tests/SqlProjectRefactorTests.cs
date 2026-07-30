using System.Text.Json;
using TraceMap.Cli;
using TraceMap.Core;
using TraceMap.Reporting;
using TraceMap.Storage;

namespace TraceMap.Tests;

public sealed class SqlProjectRefactorTests
{
    [Fact]
    public void Extractor_emits_bounded_rename_and_schema_move_intent_deterministically()
    {
        using var temp = new TempDirectory();
        Directory.CreateDirectory(Path.Combine(temp.Path, "Database", "Refactors"));
        File.WriteAllText(Path.Combine(temp.Path, "Database", "App.sqlproj"), """
            <Project Sdk="Microsoft.Build.Sql/2.0.0">
              <ItemGroup>
                <RefactorLog Include="  Refactors\App.refactorlog  " />
              </ItemGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(temp.Path, "Database", "Refactors", "App.refactorlog"), """
            <Operations>
              <Operation Name="Rename Refactor" Key="private-operation-key-sentinel">
                <Property Name="ElementName" Value="[dbo].[Orders]" />
                <Property Name="ElementType" Value="SqlTable" />
                <Property Name="NewName" Value="[ArchivedOrders]" />
              </Operation>
              <Operation Name="Rename Refactor">
                <Property Name="ElementName" Value="[Status]" />
                <Property Name="ElementType" Value="SqlSimpleColumn" />
                <Property Name="ParentElementName" Value="[dbo].[Orders]" />
                <Property Name="NewName" Value="[ArchiveStatus]" />
              </Operation>
              <Operation Name="Move Schema">
                <Property Name="ElementName" Value="[dbo].[Orders]" />
                <Property Name="ElementType" Value="SqlTable" />
                <Property Name="NewSchema" Value="[archive]" />
              </Operation>
            </Operations>
            """);

        var inventory = FileInventory.Collect(temp.Path);
        var first = SqlProjectRefactorExtractor.Extract(temp.Path, Manifest(), inventory);
        var second = SqlProjectRefactorExtractor.Extract(temp.Path, Manifest(), inventory);

        Assert.Contains(inventory, item => item.Kind == "SqlProject" && item.RelativePath == "Database/App.sqlproj");
        Assert.Contains(inventory, item => item.Kind == "SqlProjectRefactorLog" && item.RelativePath == "Database/Refactors/App.refactorlog");
        Assert.Single(first, fact => fact.FactType == FactTypes.SqlProjectRefactorLogDeclared);
        var operations = first.Where(fact => fact.FactType == FactTypes.SqlProjectRefactorOperation)
            .OrderBy(fact => fact.Evidence.StartLine).ToArray();
        Assert.Equal(["rename-table", "rename-column", "move-schema"],
            operations.Select(fact => fact.Properties["operationKind"]).ToArray());
        Assert.Equal("dbo", operations[0].Properties["schemaName"]);
        Assert.Equal("ArchivedOrders", operations[0].Properties["newTableName"]);
        Assert.Equal("ArchiveStatus", operations[1].Properties["newColumnName"]);
        Assert.Equal("archive", operations[2].Properties["newSchemaName"]);
        Assert.Equal(32, operations[0].Properties["operationKeyHash"].Length);
        Assert.All(first, fact =>
        {
            Assert.Contains(fact.RuleId, new[]
            {
                RuleIds.DatabaseSqlProjectRefactorIntent,
                RuleIds.DatabaseSqlProjectRefactorIntentGap
            });
            Assert.Equal(ScannerVersions.SqlProjectRefactorExtractor, fact.Evidence.ExtractorVersion);
            Assert.Equal("abcdef1234567890", fact.CommitSha);
        });
        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
        Assert.DoesNotContain("private-operation-key-sentinel", JsonSerializer.Serialize(first), StringComparison.Ordinal);
    }

    [Fact]
    public void Extractor_emits_safe_gaps_for_unsafe_missing_and_unreferenced_inputs()
    {
        using var temp = new TempDirectory();
        Directory.CreateDirectory(Path.Combine(temp.Path, "Database"));
        File.WriteAllText(Path.Combine(temp.Path, "Database", "App.sqlproj"), """
            <Project>
              <ItemGroup>
                <RefactorLog Include="$(PrivatePath)\secret.refactorlog" />
                <RefactorLog Include="@(RefactorLogs)" />
                <RefactorLog Include="%(Identity)" />
                <RefactorLog Include="first.refactorlog;second.refactorlog" />
                <RefactorLog Include="../../outside.refactorlog" />
                <RefactorLog Include="missing.refactorlog" />
                <RefactorLog Include="dangerous.refactorlog" />
                <RefactorLog Include="conditional.refactorlog" Condition="'$(Configuration)' == 'Release'" />
              </ItemGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(temp.Path, "Database", "dangerous.refactorlog"), """
            <!DOCTYPE private [<!ENTITY leak SYSTEM "file:///private/sentinel">]>
            <Operations>&leak;</Operations>
            """);
        File.WriteAllText(Path.Combine(temp.Path, "Database", "orphan.refactorlog"), "<Operations />");

        var facts = SqlProjectRefactorExtractor.Extract(temp.Path, Manifest(), FileInventory.Collect(temp.Path));
        var serialized = JsonSerializer.Serialize(facts);

        Assert.Equal(4, facts.Count(fact => fact.Properties.GetValueOrDefault("classification") == "RefactorLogReferenceUnsupported"));
        Assert.Contains(facts, fact => fact.Properties.GetValueOrDefault("classification") == "RefactorLogReferenceEscapesScanRoot");
        Assert.Contains(facts, fact => fact.Properties.GetValueOrDefault("classification") == "RefactorLogReferenceMissing");
        Assert.Contains(facts, fact => fact.Properties.GetValueOrDefault("classification") == "RefactorLogXmlSecurityRejected");
        Assert.Contains(facts, fact => fact.Properties.GetValueOrDefault("classification") == "RefactorLogConditionUnsupported");
        Assert.Contains(facts, fact => fact.Properties.GetValueOrDefault("classification") == "RefactorLogProjectReferenceUnavailable");
        Assert.DoesNotContain(facts, fact => fact.FactType == FactTypes.SqlProjectRefactorOperation);
        Assert.DoesNotContain("PrivatePath", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("file:///private/sentinel", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("&leak;", serialized, StringComparison.Ordinal);
    }

    [Fact]
    public void Extractor_rejects_refactor_logs_inherited_from_conditional_ancestors()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "App.sqlproj"), """
            <Project>
              <ItemGroup Condition="'$(Configuration)' == 'Release'">
                <RefactorLog Include="App.refactorlog" />
              </ItemGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(temp.Path, "App.refactorlog"), """
            <Operations>
              <Operation Name="Rename Refactor" Key="conditional-key">
                <Property Name="ElementName" Value="[dbo].[Before]" />
                <Property Name="ElementType" Value="SqlTable" />
                <Property Name="NewName" Value="[After]" />
              </Operation>
            </Operations>
            """);

        var facts = SqlProjectRefactorExtractor.Extract(
            temp.Path,
            Manifest(),
            FileInventory.Collect(temp.Path));

        Assert.Contains(facts, fact =>
            fact.Properties.GetValueOrDefault("classification") == "RefactorLogConditionUnsupported");
        Assert.DoesNotContain(facts, fact => fact.FactType == FactTypes.SqlProjectRefactorLogDeclared);
        Assert.DoesNotContain(facts, fact => fact.FactType == FactTypes.SqlProjectRefactorOperation);
    }

    [Fact]
    public void Extractor_caps_operations_and_retains_only_sanitized_omission_count()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "App.sqlproj"), """
            <Project><ItemGroup><RefactorLog Include="App.refactorlog" /></ItemGroup></Project>
            """);
        var operations = string.Join('\n', Enumerable.Range(0, 1002).Select(index => $"""
            <Operation Name="Rename Refactor" Key="private-key-{index}">
              <Property Name="ElementName" Value="[dbo].[Table{index}]" />
              <Property Name="ElementType" Value="SqlTable" />
              <Property Name="NewName" Value="[Renamed{index}]" />
            </Operation>
            """));
        File.WriteAllText(Path.Combine(temp.Path, "App.refactorlog"), $"<Operations>{operations}</Operations>");

        var facts = SqlProjectRefactorExtractor.Extract(temp.Path, Manifest(), FileInventory.Collect(temp.Path));

        Assert.Equal(1000, facts.Count(fact => fact.FactType == FactTypes.SqlProjectRefactorOperation));
        var gap = Assert.Single(facts,
            fact => fact.Properties.GetValueOrDefault("classification") == "RefactorOperationLimitExceeded");
        Assert.Equal("2", gap.Properties["omittedOperationCount"]);
        Assert.DoesNotContain("private-key-1001", JsonSerializer.Serialize(facts), StringComparison.Ordinal);
    }

    [Fact]
    public void Extractor_rejects_refactor_log_symlinks_before_reading_the_target()
    {
        if (OperatingSystem.IsWindows())
            return;
        using var temp = new TempDirectory();
        using var outside = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "App.sqlproj"),
            "<Project><ItemGroup><RefactorLog Include=\"linked.refactorlog\" /></ItemGroup></Project>");
        var outsideLog = Path.Combine(outside.Path, "outside.refactorlog");
        File.WriteAllText(outsideLog, """
            <Operations><Operation Name="Rename Refactor" Key="private-link-key">
              <Property Name="ElementName" Value="[private].[Outside]" />
              <Property Name="ElementType" Value="SqlTable" />
              <Property Name="NewName" Value="[Exposed]" />
            </Operation></Operations>
            """);
        File.CreateSymbolicLink(Path.Combine(temp.Path, "linked.refactorlog"), outsideLog);

        var facts = SqlProjectRefactorExtractor.Extract(temp.Path, Manifest(), FileInventory.Collect(temp.Path));
        var serialized = JsonSerializer.Serialize(facts);

        Assert.Contains(facts,
            fact => fact.Properties.GetValueOrDefault("classification") == "RefactorLogReferenceSymlinkRejected");
        Assert.DoesNotContain(facts, fact => fact.FactType == FactTypes.SqlProjectRefactorOperation);
        Assert.DoesNotContain("Outside", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("private-link-key", serialized, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Project_scope_excludes_other_sql_projects_and_their_refactor_logs()
    {
        using var temp = new TempDirectory();
        var output = Path.Combine(temp.Path, "out");
        foreach (var name in new[] { "Keep", "Skip" })
        {
            File.WriteAllText(Path.Combine(temp.Path, $"{name}.sqlproj"),
                $"<Project><ItemGroup><RefactorLog Include=\"{name}.refactorlog\" /></ItemGroup></Project>");
            File.WriteAllText(Path.Combine(temp.Path, $"{name}.refactorlog"), $"""
                <Operations><Operation Name="Rename Refactor" Key="{name}-key">
                  <Property Name="ElementName" Value="[dbo].[{name}Old]" />
                  <Property Name="ElementType" Value="SqlTable" />
                  <Property Name="NewName" Value="[{name}New]" />
                </Operation></Operations>
                """);
        }
        using var standardOutput = new StringWriter();
        using var errorOutput = new StringWriter();

        var exitCode = await TraceMapCommand.RunAsync(
            ["scan", "--repo", temp.Path, "--out", output, "--project", "Keep.sqlproj"],
            standardOutput,
            errorOutput);
        var facts = await File.ReadAllTextAsync(Path.Combine(output, "facts.ndjson"));
        var refactorFacts = facts.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Where(line => line.Contains("database.sql-project.refactor-intent", StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(0, exitCode);
        Assert.Contains(refactorFacts, line => line.Contains("KeepOld", StringComparison.Ordinal));
        Assert.DoesNotContain(refactorFacts, line => line.Contains("SkipOld", StringComparison.Ordinal));
        Assert.DoesNotContain(refactorFacts, line => line.Contains("Skip.refactorlog", StringComparison.Ordinal));
    }

    [Fact]
    public void Runbook_packet_projects_refactor_milestone_and_gap_without_raw_operation_key()
    {
        var manifest = Manifest();
        var operation = FactFactory.Create(
            manifest,
            FactTypes.SqlProjectRefactorOperation,
            RuleIds.DatabaseSqlProjectRefactorIntent,
            EvidenceTiers.Tier2Structural,
            new EvidenceSpan("App.refactorlog", 2, 2, null, nameof(SqlProjectRefactorExtractor), ScannerVersions.SqlProjectRefactorExtractor),
            properties: Properties(
                ("operationKind", "rename-table"),
                ("operationKeyHash", FactFactory.Hash("private-runbook-key", 32)),
                ("coverageLabel", "bounded-static-evidence"),
                ("limitations", "Checked-in intent only.")));
        var gap = FactFactory.Create(
            manifest,
            FactTypes.AnalysisGap,
            RuleIds.DatabaseSqlProjectRefactorIntentGap,
            EvidenceTiers.Tier4Unknown,
            new EvidenceSpan("App.refactorlog", 3, 3, null, nameof(SqlProjectRefactorExtractor), ScannerVersions.SqlProjectRefactorExtractor),
            properties: Properties(
                ("classification", "RefactorOperationUnsupported"),
                ("coverageLabel", "reduced"),
                ("limitations", "Unsupported shape.")));

        var packet = SqlRunbookPacketBuilder.Build(new ScanResult(manifest, [operation, gap], []));
        var serialized = JsonSerializer.Serialize(packet);

        var milestone = Assert.Single(packet.Milestones);
        Assert.Equal("rename-table", milestone.Kind);
        Assert.Equal("intended-by-project-refactor-log", milestone.State);
        Assert.Contains(packet.Gaps, item => item.Evidence.RuleId == RuleIds.DatabaseSqlProjectRefactorIntentGap);
        Assert.DoesNotContain("private-runbook-key", serialized, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reviewers_compose_refactor_intent_and_gaps_with_provenance_and_non_claims()
    {
        using var temp = new TempDirectory();
        var beforeIndex = Path.Combine(temp.Path, "before.sqlite");
        var afterIndex = Path.Combine(temp.Path, "after.sqlite");
        var designOutput = Path.Combine(temp.Path, "design");
        var releaseOutput = Path.Combine(temp.Path, "release");
        var before = Manifest() with { CommitSha = "1111111111111111" };
        var after = Manifest() with { CommitSha = "2222222222222222" };
        var operation = FactFactory.Create(
            after,
            FactTypes.SqlProjectRefactorOperation,
            RuleIds.DatabaseSqlProjectRefactorIntent,
            EvidenceTiers.Tier2Structural,
            new EvidenceSpan("Database/App.refactorlog", 4, 4, null, nameof(SqlProjectRefactorExtractor), ScannerVersions.SqlProjectRefactorExtractor),
            projectPath: "Database/App.sqlproj",
            sourceSymbol: "dbo.Orders",
            targetSymbol: "dbo.ArchivedOrders",
            contractElement: "rename-table",
            properties: Properties(
                ("objectKind", "table"),
                ("operationKind", "rename-table"),
                ("schemaName", "dbo"),
                ("tableName", "Orders"),
                ("newSchemaName", "dbo"),
                ("newTableName", "ArchivedOrders"),
                ("operationKeyHash", FactFactory.Hash("sentinel-private-key", 32)),
                ("coverageLabel", "bounded-static-evidence"),
                ("limitations", "Checked-in intent only; deployment and applied state are not proven.")));
        var gap = FactFactory.Create(
            after,
            FactTypes.AnalysisGap,
            RuleIds.DatabaseSqlProjectRefactorIntentGap,
            EvidenceTiers.Tier4Unknown,
            new EvidenceSpan("Database/App.sqlproj", 8, 8, null, nameof(SqlProjectRefactorExtractor), ScannerVersions.SqlProjectRefactorExtractor),
            projectPath: "Database/App.sqlproj",
            properties: Properties(
                ("classification", "RefactorOperationUnsupported"),
                ("coverageLabel", "reduced"),
                ("limitations", "Unsupported static shape.")));
        SqliteIndexWriter.Write(beforeIndex, before, []);
        SqliteIndexWriter.Write(afterIndex, after, [operation, gap]);

        var design = await DatabaseDesignReviewReporter.WriteAsync(
            new DatabaseDesignReviewOptions(afterIndex, designOutput));
        var release = await ReleaseReviewReporter.WriteAsync(
            new ReleaseReviewOptions(beforeIndex, afterIndex, releaseOutput));

        var designItem = Assert.Single(design.Report.GlobalObjects,
            item => item.EvidenceKind == "sql-project-refactor");
        Assert.Equal("ReviewRecommended", designItem.Classification);
        Assert.Equal(RuleIds.DatabaseSqlProjectRefactorIntent, designItem.Evidence.RuleId);
        Assert.Equal(after.CommitSha, designItem.Evidence.CommitSha);
        Assert.Contains(design.Report.Gaps,
            item => item.RuleId == RuleIds.DatabaseSqlProjectRefactorIntentGap);

        var finding = Assert.Single(release.Report.SqlEvidence.Findings,
            item => item.RuleId == RuleIds.DatabaseSqlProjectRefactorIntent);
        Assert.Equal(ReleaseReviewClassifications.ReviewRecommended, finding.Classification);
        Assert.Equal(ReleaseReviewStatuses.Available, release.Report.SqlEvidence.Status);
        Assert.Contains(release.Report.SqlEvidence.Gaps,
            item => item.RuleId == RuleIds.DatabaseSqlProjectRefactorIntentGap);
        Assert.NotEmpty(finding.SupportingFactIds);
        Assert.Equal(ScannerVersions.SqlProjectRefactorExtractor, finding.ExtractorVersion);

        var rendered = string.Join('\n',
            await File.ReadAllTextAsync(Path.Combine(designOutput, "database-design-review.md")),
            await File.ReadAllTextAsync(Path.Combine(designOutput, "database-design-review.json")),
            await File.ReadAllTextAsync(Path.Combine(releaseOutput, "release-review.md")),
            await File.ReadAllTextAsync(Path.Combine(releaseOutput, "release-review.json")));
        Assert.Contains("deployment and applied state are not proven", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("sentinel-private-key", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain(temp.Path, rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("safe to run", rendered, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Rules_are_cataloged_with_limitations()
    {
        var catalog = File.ReadAllText(Path.Combine(FindRepoRoot(), "rules", "rule-catalog.yml"));
        foreach (var rule in new[]
                 {
                     RuleIds.DatabaseSqlProjectRefactorIntent,
                     RuleIds.DatabaseSqlProjectRefactorIntentGap
                 })
        {
            var start = catalog.IndexOf($"  - id: {rule}", StringComparison.Ordinal);
            Assert.True(start >= 0);
            var next = catalog.IndexOf("\n  - id:", start + 1, StringComparison.Ordinal);
            var block = next < 0 ? catalog[start..] : catalog[start..next];
            Assert.Contains("limitations:", block, StringComparison.Ordinal);
        }
    }

    private static ScanManifest Manifest() =>
        new(
            "scan-sql-project",
            "sql-project",
            null,
            "main",
            "abcdef1234567890",
            "tracemap-test/1.0",
            DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
            "Level3SyntaxAnalysis",
            "NotRun",
            [],
            [],
            [],
            [],
            ".",
            FactFactory.Hash("sql-project", 32),
            FactFactory.Hash("sql-project-root", 32));

    private static SortedDictionary<string, string> Properties(params (string Key, string Value)[] values) =>
        new(values.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal), StringComparer.Ordinal);

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "rules", "rule-catalog.yml")))
                return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
