using System.Text.Json;
using Microsoft.Data.Sqlite;
using TraceMap.Access;
using TraceMap.Access.Cli;
using TraceMap.Combine;
using TraceMap.Core;
using TraceMap.Reporting;

namespace TraceMap.Tests;

public sealed class AccessMacroReportingTests
{
    private const string ProtectedMacroName = "PasswordMacro_92817";

    [Fact]
    public void Product_macro_inventory_reads_only_counts_and_never_catalog_items()
    {
        var catalog = new FakeMacroCountCollection(3);
        var application = new FakeMacroApplication(new FakeMacroProject(catalog));
        var gaps = new List<AccessGapProjection>();

        var inventory = new AccessComReader().ReadMacroInventoryCounts(application, gaps);

        Assert.Equal(3, inventory.NamedMacroCount);
        Assert.Null(inventory.LoadedMacroCountUnchanged);
        Assert.Equal("named-count-observed-loaded-state-unavailable-other-categories-identities-bodies-unavailable", inventory.Coverage);
        Assert.Equal(0, catalog.IndexerReadCount);
        Assert.Equal(0, application.UnsupportedLoadedMacrosReadCount);
        Assert.Single(gaps, gap => gap.Classification == "AccessMacroInventoryUnavailable"
            && gap.RuleId == RuleIds.LegacyAccessMacroGap);
        Assert.Contains(gaps, gap => gap.Classification == "AccessMacroLoadedStateUnavailable"
            && gap.RuleId == RuleIds.LegacyAccessMacroGap);
        Assert.Contains(gaps, gap => gap.Classification == "AccessMacroIdentityUnavailable");
        Assert.Contains(gaps, gap => gap.Classification == "AccessMacroEmbeddedInventoryUnavailable");
        Assert.Contains(gaps, gap => gap.Classification == "AccessMacroDataInventoryUnavailable");
        Assert.Contains(gaps, gap => gap.Classification == "AccessMacroStartupInventoryUnavailable");
        Assert.Contains(gaps, gap => gap.Classification == "AccessMacroBodyOmitted");
    }

    [Fact]
    public void Product_macro_inventory_records_unavailable_catalog_without_accessing_unsupported_loaded_collection()
    {
        var application = new FakeMacroApplication(new FakeMacroProject(new FakeUnavailableMacroCountCollection()));
        var gaps = new List<AccessGapProjection>();

        var inventory = new AccessComReader().ReadMacroInventoryCounts(application, gaps);

        Assert.Null(inventory.NamedMacroCount);
        Assert.Null(inventory.LoadedMacroCountUnchanged);
        Assert.Equal("named-count-unavailable-loaded-state-unavailable-other-categories-identities-bodies-unavailable", inventory.Coverage);
        Assert.Equal(0, application.UnsupportedLoadedMacrosReadCount);
        Assert.Contains(gaps, gap => gap.Classification == "AccessMacroCollectionLimitReached"
            && gap.RuleId == RuleIds.LegacyAccessMacroGap);
        Assert.Contains(gaps, gap => gap.Classification == "AccessMacroLoadedStateUnavailable"
            && gap.RuleId == RuleIds.LegacyAccessMacroGap);
    }

    [Fact]
    public async Task Count_only_macro_inventory_emits_metadata_and_gap_without_macro_identity_facts()
    {
        using var temp = new TempDirectory();
        var databasePath = Path.Combine(temp.Path, "fixture.accdb");
        await File.WriteAllBytesAsync(databasePath, [1, 2, 3, 4]);
        var databaseHash = AccessInputValidator.HashFile(databasePath);
        var input = new AccessValidatedInput(
            temp.Path, "repo", AccessSafeValues.RoleHash("access-repository-identity", "repo"), null, "test",
            new string('e', 40), databasePath, "fixture.accdb", databaseHash, ".accdb", Path.Combine(temp.Path, "out"), false);
        var inventory = new AccessMacroInventoryProjection(2, null, "named-count-observed-loaded-state-unavailable-other-categories-identities-bodies-unavailable");
        var projection = new AccessDatabaseProjection(
            "tracemap.access-projection.v1", databaseHash, ".accdb", "16.0", 1234, false, false, 0,
            [], [], [], [],
            [new("AccessMacroInventoryUnavailable", "macro-catalog", null, RuleIds.LegacyAccessMacroGap)],
            [new("macros", inventory.Coverage)],
            Macros: [], MacroInventory: inventory);

        var scan = AccessFactBuilder.Build(input, projection, new(temp.Path, "fixture.accdb", input.OutputFullPath));

        var databaseFact = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.LegacyDataMetadataDeclared);
        Assert.Equal("2", databaseFact.Properties.GetValueOrDefault("namedMacroCount"));
        Assert.DoesNotContain("macroLoadedCountUnchanged", databaseFact.Properties);
        Assert.Equal("named-count-observed-loaded-state-unavailable-other-categories-identities-bodies-unavailable", databaseFact.Properties.GetValueOrDefault("macroCoverage"));
        Assert.DoesNotContain(scan.Facts, fact => fact.FactType == FactTypes.AccessMacroDeclared);
        Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyAccessMacroGap
            && fact.Properties.GetValueOrDefault("classification") == "AccessMacroInventoryUnavailable");
    }

    [Fact]
    public void Macro_projector_is_deterministic_hashes_unsafe_names_and_records_body_omission_by_category()
    {
        var seed = AccessSafeValues.DatabaseIdentitySeed("repo", new string('d', 40), "fixture.accdb", "hash");
        var firstOwner = AccessSafeValues.Identity(seed, "control", "FirstButton").StableKey;
        var secondOwner = AccessSafeValues.Identity(seed, "control", "SecondButton").StableKey;
        AccessRawMacro[] forward =
        [
            new("AutoExec", "named"),
            new(ProtectedMacroName, "data"),
            new("AutoExec", "embedded", firstOwner),
            new("AutoExec", "embedded", secondOwner)
        ];
        var projected = AccessMacroProjector.Project(seed, forward);
        var reversed = AccessMacroProjector.Project(seed, forward.Reverse().ToArray());

        Assert.Equal(JsonSerializer.Serialize(projected), JsonSerializer.Serialize(reversed));
        Assert.Equal(4, projected.Macros.Count);
        Assert.Equal("autoexec", projected.Macros.Single(item => item.MacroKind == "named").StartupRole);
        Assert.All(projected.Macros.Where(item => item.MacroKind == "embedded"), item => Assert.Equal("not-autoexec", item.StartupRole));
        Assert.Equal(2, projected.Macros.Where(item => item.MacroKind == "embedded").Select(item => item.Identity.StableKey).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(new[] { firstOwner, secondOwner }.OrderBy(value => value, StringComparer.Ordinal),
            projected.Macros.Where(item => item.MacroKind == "embedded")
                .Select(item => item.OwnerStableKey).OrderBy(value => value, StringComparer.Ordinal));
        Assert.Null(projected.Macros.Single(item => item.MacroKind == "data").Identity.DisplayName);
        Assert.All(projected.Macros, macro => Assert.Equal("protected-omitted", macro.BodyStatus));
        Assert.Equal(3, projected.Gaps.Count(gap => gap.Classification == "AccessMacroBodyOmitted"
            && gap.RuleId == RuleIds.LegacyAccessMacroGap));
        Assert.DoesNotContain(ProtectedMacroName, JsonSerializer.Serialize(projected), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Macro_projector_drops_an_unsafe_owner_channel_and_records_a_rule_backed_gap()
    {
        var seed = AccessSafeValues.DatabaseIdentitySeed("repo", new string('f', 40), "fixture.accdb", "hash");
        const string protectedOwner = "PrivateControlOwner_92817";

        var projected = AccessMacroProjector.Project(seed, [new("ButtonMacro", "embedded", protectedOwner)]);

        Assert.Null(Assert.Single(projected.Macros).OwnerStableKey);
        Assert.Contains(projected.Gaps, gap => gap.Classification == "AccessMacroOwnerUnavailable"
            && gap.RuleId == RuleIds.LegacyAccessMacroGap);
        Assert.DoesNotContain(protectedOwner, JsonSerializer.Serialize(projected), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Macro_evidence_reports_hidden_static_counts_and_routes_to_hidden_legacy_docs_without_protected_names()
    {
        using var temp = new TempDirectory();
        var (scan, output) = await BuildScanAsync(temp.Path);
        await AccessArtifactWriter.WriteAsync(output, scan, AccessLimits.Default);
        Assert.Contains(scan.Facts, fact => fact.FactType == FactTypes.AccessMacroDeclared
            && fact.SourceSymbol?.StartsWith("access-control-", StringComparison.Ordinal) == true);

        var report = await File.ReadAllTextAsync(Path.Combine(output, "report.md"));
        Assert.Contains("## Access Design Evidence Summary", report, StringComparison.Ordinal);
        Assert.Contains("Public claim level: `hidden`", report, StringComparison.Ordinal);
        Assert.Contains("Macro inventory facts: 3", report, StringComparison.Ordinal);
        Assert.Contains("Named macro catalog count: `3`", report, StringComparison.Ordinal);
        Assert.Contains("Macro coverage: `named-count-observed-loaded-state-unavailable-other-categories-identities-bodies-unavailable`", report, StringComparison.Ordinal);
        Assert.Contains("Macro coverage gaps: 9", report, StringComparison.Ordinal);
        Assert.Contains("Form/report/control/binding facts: 0", report, StringComparison.Ordinal);
        Assert.Contains("VBA/event/navigation facts: 0", report, StringComparison.Ordinal);
        Assert.Contains("`AccessMacroLoadedStateUnavailable`: 1", report, StringComparison.Ordinal);
        Assert.DoesNotContain(ProtectedMacroName, report, StringComparison.OrdinalIgnoreCase);

        var docsOutput = Path.Combine(temp.Path, "docs-output");
        var docs = await EvidenceDocsExporter.ExportAsync(new EvidenceDocsExportOptions(
            Path.Combine(output, "index.sqlite"),
            docsOutput,
            Families: "legacy,gap,limitation",
            Format: "markdown,jsonl",
            Date: "2026-07"));
        var macroChunks = docs.Chunks.Where(chunk => chunk.RuleIds.Contains(RuleIds.LegacyAccessMacroGap, StringComparer.Ordinal)).ToArray();
        Assert.NotEmpty(macroChunks);
        Assert.All(macroChunks, chunk => Assert.Equal("hidden", chunk.ClaimLevel));
        Assert.Contains(macroChunks, chunk => chunk.ChunkFamily == "legacy");
        var serializedDocs = JsonSerializer.Serialize(docs);
        Assert.Contains("namedMacroCount", serializedDocs, StringComparison.Ordinal);
        Assert.Contains("named-count-observed-loaded-state-unavailable-other-categories-identities-bodies-unavailable", serializedDocs, StringComparison.Ordinal);
        Assert.DoesNotContain(ProtectedMacroName, JsonSerializer.Serialize(docs), StringComparison.OrdinalIgnoreCase);
        AssertArtifactsDoNotContain(docsOutput, ProtectedMacroName);
    }

    [Fact]
    public async Task Vault_preserves_Access_macro_rule_and_release_review_emits_structured_unsupported_consumer_gap()
    {
        using var temp = new TempDirectory();
        var (scan, output) = await BuildScanAsync(temp.Path);
        var countOnlyScan = scan with
        {
            Facts = scan.Facts.Where(fact => fact.FactType != FactTypes.AccessMacroDeclared).ToArray()
        };
        await AccessArtifactWriter.WriteAsync(output, countOnlyScan, AccessLimits.Default);
        var index = Path.Combine(output, "index.sqlite");
        var combined = Path.Combine(temp.Path, "combined.sqlite");
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([index], combined, ["access"]));

        await using (var connection = new SqliteConnection($"Data Source={combined};Mode=ReadOnly"))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "select properties_json from combined_facts where fact_type = 'LegacyDataMetadataDeclared' limit 1;";
            var propertiesJson = Assert.IsType<string>(await command.ExecuteScalarAsync());
            Assert.Contains("namedMacroCount", propertiesJson, StringComparison.Ordinal);
            Assert.Contains("named-count-observed-loaded-state-unavailable-other-categories-identities-bodies-unavailable", propertiesJson, StringComparison.Ordinal);
        }

        var vaultOutput = Path.Combine(temp.Path, "vault-output");
        var vault = await VaultExporter.ExportAsync(new VaultExportOptions(
            combined,
            vaultOutput,
            MinimumClaimLevel: "hidden",
            Date: "2026-07",
            Format: "markdown,json"));
        var vaultGap = Assert.Single(vault.Graph.Gaps, gap => gap.Classification == "AccessEvidenceConsumerUnsupported");
        Assert.Equal("vault-export.gap.access-evidence-consumer-unsupported.v1", vaultGap.RuleId);
        Assert.NotEmpty(vaultGap.SupportingFactIds ?? []);
        Assert.Contains(vault.Graph.Nodes, node =>
            node.RuleIds.Contains("vault-export.gap.access-evidence-consumer-unsupported.v1", StringComparer.Ordinal)
            && node.SupportingFactIds.Count > 0);
        Assert.DoesNotContain(ProtectedMacroName, JsonSerializer.Serialize(vault), StringComparison.OrdinalIgnoreCase);
        AssertArtifactsDoNotContain(vaultOutput, ProtectedMacroName);

        var review = await ReleaseReviewReporter.BuildReportAsync(new ReleaseReviewOptions(
            index,
            index,
            Path.Combine(temp.Path, "release-review.md")));
        var gap = Assert.Single(review.Gaps, item => item.GapKind == "AccessEvidenceConsumerUnsupported");
        Assert.Equal("accessEvidence", gap.Section);
        Assert.Equal("release.review.section.v1", gap.RuleId);
        Assert.Equal(ReleaseReviewClassifications.PartialAnalysis, gap.Classification);
        Assert.NotEmpty(gap.SupportingFactIds);
        Assert.DoesNotContain(ProtectedMacroName, JsonSerializer.Serialize(review), StringComparison.OrdinalIgnoreCase);

        var combinedReview = await ReleaseReviewReporter.BuildReportAsync(new ReleaseReviewOptions(
            combined,
            combined,
            Path.Combine(temp.Path, "combined-release-review.md")));
        var combinedGap = Assert.Single(combinedReview.Gaps, item => item.GapKind == "AccessEvidenceConsumerUnsupported");
        Assert.NotEmpty(combinedGap.SupportingFactIds);
        Assert.All(combinedGap.SupportingFactIds, factId => Assert.Contains(':', factId));
        Assert.DoesNotContain(ProtectedMacroName, JsonSerializer.Serialize(combinedReview), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Windows_smoke_checkpoints_the_closed_Phase_9_contract_before_disposable_cleanup()
    {
        var script = File.ReadAllText(Path.Combine(
            FindRepoRoot(), "scripts", "access-validation", "Invoke-AccessSmoke.ps1"));

        Assert.Contains("[string]$Phase9CheckpointPath", script, StringComparison.Ordinal);
        Assert.Contains("tracemap.access-phase9-checkpoint.v1", script, StringComparison.Ordinal);
        Assert.Contains("phase9ConsumerContracts = \"boundary-stop\"", script, StringComparison.Ordinal);
        Assert.Contains("phase9ConsumerContracts = \"completed\"", script, StringComparison.Ordinal);
        Assert.Contains("failureClassification = \"none\"", script, StringComparison.Ordinal);
        Assert.Contains("checkpointSequence = 0", script, StringComparison.Ordinal);
        Assert.Contains("$sequencePath = \"$Phase9CheckpointPath.$($phase9Checkpoint.checkpointSequence)\"", script, StringComparison.Ordinal);
        Assert.Contains("[IO.File]::Move($sequenceTemporary, $sequencePath)", script, StringComparison.Ordinal);
        Assert.Contains("tool-missing", script, StringComparison.Ordinal);
        Assert.Contains("tool-inside-disposable-root", script, StringComparison.Ordinal);
        Assert.Contains("powerShellHost", script, StringComparison.Ordinal);
        Assert.Contains("@generatorArguments *> $null", script, StringComparison.Ordinal);
        Assert.Contains("$env:TRACEMAP_ACCESS_GENERATOR", script, StringComparison.Ordinal);
        Assert.DoesNotContain("\"-File\", $Generator", script, StringComparison.Ordinal);
        Assert.Contains("generator-process-failed", script, StringComparison.Ordinal);
        Assert.Contains("fixture-database-missing", script, StringComparison.Ordinal);
        Assert.Contains("generation-canary-fired", script, StringComparison.Ordinal);
        Assert.Contains("fixture-provenance", script, StringComparison.Ordinal);
        Assert.Contains("fixture-git-init-failed", script, StringComparison.Ordinal);
        Assert.Contains("fixture-git-config-failed", script, StringComparison.Ordinal);
        Assert.Contains("fixture-git-stage-failed", script, StringComparison.Ordinal);
        Assert.Contains("fixture-git-commit-failed", script, StringComparison.Ordinal);
        Assert.Contains("fixture-incompatible-input-failed", script, StringComparison.Ordinal);
        Assert.Contains("fixture-hash-failed", script, StringComparison.Ordinal);
        Assert.Contains("fixture-boundary-cleanup-failed", script, StringComparison.Ordinal);
        Assert.Contains("switch ($phase9Checkpoint.stopStage)", script, StringComparison.Ordinal);
        Assert.Contains("product-scan-failed", script, StringComparison.Ordinal);
        Assert.Contains("report-validation-failed", script, StringComparison.Ordinal);
        Assert.Contains("combine-validation-failed", script, StringComparison.Ordinal);
        Assert.Contains("docs-validation-failed", script, StringComparison.Ordinal);
        Assert.Contains("vault-validation-failed", script, StringComparison.Ordinal);
        Assert.Contains("release-review-validation-failed", script, StringComparison.Ordinal);
        Assert.Contains("safety-check-failed", script, StringComparison.Ordinal);
        Assert.Contains("docs-export --index $combined", script, StringComparison.Ordinal);
        Assert.Contains("vault export --combined-index $combined", script, StringComparison.Ordinal);
        Assert.Contains("release-review --before $combined --after $combined", script, StringComparison.Ordinal);
        Assert.Contains("AccessEvidenceConsumerUnsupported", script, StringComparison.Ordinal);
        Assert.Contains("vault-export.gap.access-evidence-consumer-unsupported.v1", script, StringComparison.Ordinal);
        Assert.Contains("Phase 9 checkpoint must be outside the disposable smoke root", script, StringComparison.Ordinal);
        Assert.Contains("[string]::Equals($Phase9CheckpointPath, $SmokeRoot", script, StringComparison.Ordinal);
        Assert.DoesNotContain("DatabaseHash = $originalHash", script, StringComparison.Ordinal);
        Assert.Contains("if ($foundMarker) { throw \"protected marker leaked into downstream output\" }", script, StringComparison.Ordinal);
        Assert.Contains("OriginalUnchanged = $true", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Representative_smoke_is_local_only_count_only_and_durably_checkpointed()
    {
        var script = File.ReadAllText(Path.Combine(
            FindRepoRoot(), "scripts", "access-validation", "Invoke-AccessRepresentativeSmoke.ps1"));

        Assert.Contains("[switch]$InputExplicitlyAuthorized", script, StringComparison.Ordinal);
        Assert.DoesNotContain("[Parameter(Mandatory = $true)]\n    [switch]$InputExplicitlyAuthorized", script, StringComparison.Ordinal);
        Assert.Contains("tracemap.access-phase9-representative-checkpoint.v1", script, StringComparison.Ordinal);
        Assert.Contains("$sequencePath = \"$CheckpointBasePath.$($checkpoint.checkpointSequence)\"", script, StringComparison.Ordinal);
        Assert.Contains("Wait-AccessScanJobs", script, StringComparison.Ordinal);
        Assert.Contains("Test-AccessSurfaceVisible", script, StringComparison.Ordinal);
        Assert.Contains("$job.State -ne \"Completed\"", script, StringComparison.Ordinal);
        Assert.Contains("$concurrentResultA.Count -ne 1", script, StringComparison.Ordinal);
        Assert.Contains("$manifest.commitSha -ne $disposableCommit", script, StringComparison.Ordinal);
        Assert.Contains("$_.evidence.extractorVersion", script, StringComparison.Ordinal);
        Assert.Contains("git init -b access-representative", script, StringComparison.Ordinal);
        Assert.Contains("if (@(& git remote).Count -ne 0)", script, StringComparison.Ordinal);
        Assert.Contains("rowDataReadFalse", script, StringComparison.Ordinal);
        Assert.Contains("executionPerformedFalse", script, StringComparison.Ordinal);
        Assert.Contains("uiIdentityFactsZero", script, StringComparison.Ordinal);
        Assert.Contains("vbaIdentityFlowFactsZero", script, StringComparison.Ordinal);
        Assert.Contains("AccessNavigationCandidate", script, StringComparison.Ordinal);
        Assert.Contains("AccessEventBindingCandidate", script, StringComparison.Ordinal);
        Assert.Contains("macroIdentityFactsZero", script, StringComparison.Ordinal);
        Assert.Contains("AccessEvidenceConsumerUnsupported", script, StringComparison.Ordinal);
        Assert.Contains("protectedOutputMatchCount", script, StringComparison.Ordinal);
        Assert.Contains("originalUnchanged", script, StringComparison.Ordinal);
        Assert.Contains("phase95Representative = \"completed\"", script, StringComparison.Ordinal);
        Assert.Contains("representative scratch must be a new non-root path", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Remove-Item $ScratchRoot", script, StringComparison.Ordinal);
        Assert.Contains("if ($null -ne $destination) { $destination.Dispose() }", script, StringComparison.Ordinal);
        Assert.DoesNotContain("RunMacro", script, StringComparison.Ordinal);
        Assert.DoesNotContain("OpenRecordset", script, StringComparison.Ordinal);
        Assert.DoesNotContain("OpenQuery", script, StringComparison.Ordinal);
        Assert.DoesNotContain("SaveAsText", script, StringComparison.Ordinal);
    }

    private static async Task<(ScanResult Scan, string Output)> BuildScanAsync(string root)
    {
        var databasePath = Path.Combine(root, "fixture.accdb");
        await File.WriteAllBytesAsync(databasePath, [1, 2, 3, 4]);
        var databaseHash = AccessInputValidator.HashFile(databasePath);
        var output = Path.Combine(root, "access-output");
        var input = new AccessValidatedInput(
            root,
            "repo",
            AccessSafeValues.RoleHash("access-repository-identity", "repo"),
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
        var embeddedOwner = AccessSafeValues.Identity(seed, "control", "MacroButton").StableKey;
        var macro = AccessMacroProjector.Project(seed,
            [new("AutoExec", "named"), new(ProtectedMacroName, "data"), new("ButtonMacro", "embedded", embeddedOwner)]);
        const string macroCoverage = "named-count-observed-loaded-state-unavailable-other-categories-identities-bodies-unavailable";
        AccessGapProjection[] productMacroGaps =
        [
            new("AccessMacroInventoryUnavailable", "macro-catalog", null, RuleIds.LegacyAccessMacroGap),
            new("AccessMacroLoadedStateUnavailable", "macro-loaded-state", null, RuleIds.LegacyAccessMacroGap),
            new("AccessMacroIdentityUnavailable", "macro-named", null, RuleIds.LegacyAccessMacroGap),
            new("AccessMacroEmbeddedInventoryUnavailable", "macro-embedded", null, RuleIds.LegacyAccessMacroGap),
            new("AccessMacroDataInventoryUnavailable", "macro-data", null, RuleIds.LegacyAccessMacroGap),
            new("AccessMacroStartupInventoryUnavailable", "macro-startup", null, RuleIds.LegacyAccessMacroGap)
        ];
        var projection = new AccessDatabaseProjection(
            "tracemap.access-projection.v1",
            databaseHash,
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
            [
                .. macro.Gaps,
                .. productMacroGaps,
                new("AccessUiSurfaceUnavailable", "ui-surface", null, RuleIds.LegacyAccessUiSurface),
                new("AccessVbaProjectUnavailable", "vba-project", null, RuleIds.LegacyAccessVba)
            ],
            [new("macros", macroCoverage)],
            Macros: macro.Macros,
            MacroInventory: new(3, null, macroCoverage));
        return (AccessFactBuilder.Build(input, projection, new(root, "fixture.accdb", output)), output);
    }

    private static void AssertArtifactsDoNotContain(string root, string marker)
    {
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var text = System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(file));
            Assert.DoesNotContain(marker, text, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "src", "dotnet", "TraceMap.sln")))
                return current.FullName;
            current = current.Parent;
        }

        throw new DirectoryNotFoundException("TraceMap repository root was not found.");
    }

    public sealed class FakeMacroApplication
    {
        public FakeMacroApplication(FakeMacroProject project) => CurrentProject = project;

        public FakeMacroProject CurrentProject { get; }
        public int UnsupportedLoadedMacrosReadCount { get; private set; }
        public object Macros
        {
            get
            {
                UnsupportedLoadedMacrosReadCount++;
                throw new InvalidOperationException("Application.Macros is not an approved Access API.");
            }
        }
    }

    public sealed class FakeMacroProject(object macros)
    {
        public object AllMacros { get; } = macros;
    }

    public sealed class FakeMacroCountCollection(int count)
    {
        public int Count { get; } = count;
        public int IndexerReadCount { get; private set; }
        public object this[int index]
        {
            get
            {
                IndexerReadCount++;
                throw new InvalidOperationException($"Macro catalog item {index} must not be read.");
            }
        }
    }

    public sealed class FakeUnavailableMacroCountCollection
    {
        public int Count => throw new InvalidOperationException("Count unavailable.");
    }
}
