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
    public async Task Vault_preserves_Access_macro_rule_and_release_review_composes_safe_Access_design_evidence()
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
        Assert.Equal("1.1", review.Version);
        Assert.Equal(ReleaseReviewStatuses.Available, review.AccessEvidence.Status);
        Assert.Contains(review.AccessEvidence.Findings, item => item.Metadata.Any(pair => pair.Key == "evidenceKind" && pair.Value == "database-inventory"));
        Assert.Contains(review.AccessEvidence.Findings, item => item.Metadata.Any(pair => pair.Key == "evidenceKind" && pair.Value == "table"));
        Assert.Contains(review.AccessEvidence.Findings, item => item.Metadata.Any(pair => pair.Key == "evidenceKind" && pair.Value == "field"));
        Assert.Contains(review.AccessEvidence.Findings, item => item.Metadata.Any(pair => pair.Key == "evidenceKind" && pair.Value == "mapping"));
        Assert.Contains(review.AccessEvidence.Findings, item => item.Metadata.Any(pair => pair.Key == "evidenceKind" && pair.Value == "saved-query"));
        Assert.Contains(review.AccessEvidence.Findings, item => item.Metadata.Any(pair => pair.Key == "evidenceKind" && pair.Value == "query-dependency"));
        Assert.Contains(review.AccessEvidence.Findings, item => item.Metadata.Any(pair => pair.Key == "evidenceKind" && pair.Value == "external-boundary"));
        Assert.Contains(review.AccessEvidence.Findings, item => item.Metadata.Any(pair => pair.Key == "sourceDesignKey")
            && item.Metadata.Any(pair => pair.Key == "targetDesignKey"));
        Assert.All(review.AccessEvidence.Findings.SelectMany(item => item.Metadata)
            .Where(pair => pair.Key.EndsWith("DesignKey", StringComparison.Ordinal)),
            pair => Assert.StartsWith("access-", pair.Value, StringComparison.Ordinal));
        Assert.DoesNotContain(review.AccessEvidence.Findings.SelectMany(item => item.Metadata), pair => pair.Key == "objectName");
        Assert.Contains(review.AccessEvidence.Gaps, item => item.GapKind == "AccessMacroIdentityUnavailable");
        Assert.DoesNotContain(review.Gaps, item => item.GapKind == "AccessEvidenceConsumerUnsupported");
        Assert.All(review.AccessEvidence.Findings, finding =>
        {
            Assert.Equal("accessEvidence", finding.Section);
            Assert.Equal(scan.Manifest.CommitSha, finding.CommitSha);
            Assert.Equal("AccessCatalogExtractor", finding.ExtractorId);
            Assert.False(string.IsNullOrWhiteSpace(finding.ExtractorVersion));
            Assert.NotEmpty(finding.SupportingFactIds);
            Assert.Equal("fixture.accdb", finding.FilePath);
            Assert.Equal(1, finding.StartLine);
            Assert.Equal(1, finding.EndLine);
        });
        Assert.DoesNotContain(ProtectedMacroName, JsonSerializer.Serialize(review), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("OrdersPrivate", JsonSerializer.Serialize(review.AccessEvidence), StringComparison.Ordinal);
        Assert.DoesNotContain("SELECT * FROM OrdersPrivate", JsonSerializer.Serialize(review.AccessEvidence), StringComparison.Ordinal);
        Assert.DoesNotContain("PrivateServer", JsonSerializer.Serialize(review.AccessEvidence), StringComparison.Ordinal);

        var repeatedReview = await ReleaseReviewReporter.BuildReportAsync(new ReleaseReviewOptions(
            index,
            index,
            Path.Combine(temp.Path, "repeated-release-review.md")));
        Assert.Equal(JsonSerializer.Serialize(review.AccessEvidence), JsonSerializer.Serialize(repeatedReview.AccessEvidence));

        var written = await ReleaseReviewReporter.WriteAsync(new ReleaseReviewOptions(
            index,
            index,
            Path.Combine(temp.Path, "written-release-review"),
            Format: "markdown"));
        var writtenText = await File.ReadAllTextAsync(written.MarkdownPath!) + await File.ReadAllTextAsync(written.JsonPath!);
        Assert.Contains("Access Design Evidence", writtenText, StringComparison.Ordinal);
        Assert.DoesNotContain(ProtectedMacroName, writtenText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("OrdersPrivate", writtenText, StringComparison.Ordinal);
        Assert.DoesNotContain("PrivateServer", writtenText, StringComparison.Ordinal);

        var itemOutput = Path.Combine(temp.Path, "access-item-output");
        await AccessArtifactWriter.WriteAsync(itemOutput, scan, AccessLimits.Default);
        var itemReview = await ReleaseReviewReporter.BuildReportAsync(new ReleaseReviewOptions(
            Path.Combine(itemOutput, "index.sqlite"),
            Path.Combine(itemOutput, "index.sqlite"),
            Path.Combine(temp.Path, "item-release-review.md"),
            Scope: "access-evidence"));
        Assert.Contains(itemReview.AccessEvidence.Gaps, item => item.GapKind == "AccessItemEvidenceOutsideCountOnlyBoundary");
        Assert.DoesNotContain(itemReview.AccessEvidence.Findings, item => item.Metadata.Any(pair => pair.Value == "macro"));

        var combinedReview = await ReleaseReviewReporter.BuildReportAsync(new ReleaseReviewOptions(
            combined,
            combined,
            Path.Combine(temp.Path, "combined-release-review.md")));
        Assert.Equal(ReleaseReviewStatuses.Available, combinedReview.AccessEvidence.Status);
        Assert.NotEmpty(combinedReview.AccessEvidence.Findings);
        Assert.All(combinedReview.AccessEvidence.Findings.SelectMany(item => item.SupportingFactIds), factId => Assert.Contains(':', factId));
        Assert.All(combinedReview.AccessEvidence.Findings, finding => Assert.Equal("access", finding.SourceLabel));
        Assert.DoesNotContain(ProtectedMacroName, JsonSerializer.Serialize(combinedReview), StringComparison.OrdinalIgnoreCase);

        var selectedCombinedReview = await ReleaseReviewReporter.BuildReportAsync(new ReleaseReviewOptions(
            combined,
            combined,
            Path.Combine(temp.Path, "selected-combined-release-review.md"),
            Scope: "access-evidence",
            Source: "access"));
        Assert.Equal(ReleaseReviewStatuses.Available, selectedCombinedReview.AccessEvidence.Status);
        Assert.All(selectedCombinedReview.AccessEvidence.Findings, finding => Assert.Equal("access", finding.SourceLabel));

        var excludedCombinedReview = await ReleaseReviewReporter.BuildReportAsync(new ReleaseReviewOptions(
            combined,
            combined,
            Path.Combine(temp.Path, "excluded-combined-release-review.md"),
            Scope: "access-evidence",
            Source: "other"));
        Assert.Equal(ReleaseReviewStatuses.Deferred, excludedCombinedReview.AccessEvidence.Status);
        var excludedGap = Assert.Single(excludedCombinedReview.AccessEvidence.Gaps);
        Assert.Contains(excludedGap.Metadata, pair => pair.Key == "sourceSelector" && pair.Value == "other");

        var scopedReview = await ReleaseReviewReporter.BuildReportAsync(new ReleaseReviewOptions(
            index,
            index,
            Path.Combine(temp.Path, "scoped-release-review.md"),
            Scope: "access-evidence"));
        Assert.Equal(ReleaseReviewStatuses.Available, scopedReview.AccessEvidence.Status);
        Assert.Equal(ReleaseReviewStatuses.NotRequested, scopedReview.SqlEvidence.Status);

        SqliteConnection.ClearAllPools();
        var unsafePathIndex = Path.Combine(temp.Path, "unsafe-path.sqlite");
        File.Copy(index, unsafePathIndex);
        await using (var connection = new SqliteConnection($"Data Source={unsafePathIndex}"))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "update facts set file_path = 'C:\\private\\customer\\fixture.accdb';";
            await command.ExecuteNonQueryAsync();
        }
        SqliteConnection.ClearAllPools();

        var unsafePathReview = await ReleaseReviewReporter.BuildReportAsync(new ReleaseReviewOptions(
            unsafePathIndex,
            unsafePathIndex,
            Path.Combine(temp.Path, "unsafe-path-release-review.md"),
            Scope: "access-evidence"));
        var unsafePathSerialized = JsonSerializer.Serialize(unsafePathReview.AccessEvidence);
        Assert.DoesNotContain("C:\\private", unsafePathSerialized, StringComparison.OrdinalIgnoreCase);
        Assert.All(unsafePathReview.AccessEvidence.Findings, finding => Assert.StartsWith("absolute-path-hash:", finding.FilePath, StringComparison.Ordinal));

        var collisionIndex = Path.Combine(temp.Path, "gap-collision.sqlite");
        File.Copy(index, collisionIndex);
        await using (var connection = new SqliteConnection($"Data Source={collisionIndex}"))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                insert into facts (
                    fact_id, scan_id, repo, commit_sha, project_path, fact_type, rule_id, evidence_tier,
                    source_symbol, target_symbol, contract_element, file_path, start_line, end_line,
                    snippet_hash, extractor_id, extractor_version, properties_json)
                select fact_id || '-second-file', scan_id, repo, commit_sha, project_path, fact_type, rule_id, evidence_tier,
                    source_symbol, target_symbol, contract_element, 'second.accdb', start_line, end_line,
                    snippet_hash, extractor_id, extractor_version, properties_json
                from facts
                where fact_type = 'AnalysisGap'
                  and json_extract(properties_json, '$.classification') = 'AccessMacroIdentityUnavailable'
                limit 1;
                """;
            Assert.Equal(1, await command.ExecuteNonQueryAsync());
        }
        SqliteConnection.ClearAllPools();

        var collisionReview = await ReleaseReviewReporter.BuildReportAsync(new ReleaseReviewOptions(
            collisionIndex,
            collisionIndex,
            Path.Combine(temp.Path, "gap-collision-release-review.md"),
            Scope: "access-evidence"));
        var identityGaps = collisionReview.AccessEvidence.Gaps
            .Where(gap => gap.GapKind == "AccessMacroIdentityUnavailable")
            .ToArray();
        Assert.Equal(2, identityGaps.Length);
        Assert.Equal(2, identityGaps.Select(gap => gap.GapId).Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(identityGaps, gap => gap.Metadata.Any(pair => pair.Key == "filePath" && pair.Value == "fixture.accdb"));
        Assert.Contains(identityGaps, gap => gap.Metadata.Any(pair => pair.Key == "filePath" && pair.Value == "second.accdb"));

        var truncatedIndex = Path.Combine(temp.Path, "truncated.sqlite");
        File.Copy(index, truncatedIndex);
        await using (var connection = new SqliteConnection($"Data Source={truncatedIndex}"))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                insert into facts (
                    fact_id, scan_id, repo, commit_sha, project_path, fact_type, rule_id, evidence_tier,
                    source_symbol, target_symbol, contract_element, file_path, start_line, end_line,
                    snippet_hash, extractor_id, extractor_version, properties_json)
                select fact_id || '-fact-limit', scan_id, repo, commit_sha, project_path, fact_type, rule_id, evidence_tier,
                    source_symbol, target_symbol, contract_element, file_path, start_line, end_line,
                    snippet_hash, extractor_id, extractor_version,
                    json_set(properties_json, '$.classification', 'AccessFactLimitReached')
                from facts
                where fact_type = 'AnalysisGap'
                limit 1;
                """;
            Assert.Equal(1, await command.ExecuteNonQueryAsync());
        }
        SqliteConnection.ClearAllPools();

        var truncatedReview = await ReleaseReviewReporter.BuildReportAsync(new ReleaseReviewOptions(
            truncatedIndex,
            truncatedIndex,
            Path.Combine(temp.Path, "truncated-release-review.md"),
            Scope: "access-evidence"));
        Assert.Equal(ReleaseReviewStatuses.Truncated, truncatedReview.AccessEvidence.Status);
        Assert.Contains(truncatedReview.AccessEvidence.Gaps, item => item.GapKind == "AccessFactLimitReached"
            && item.Classification == ReleaseReviewClassifications.TruncatedByLimit);

        await using (var connection = new SqliteConnection($"Data Source={index}"))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "update facts set extractor_version = '' where rule_id like 'legacy.access.%';";
            await command.ExecuteNonQueryAsync();
        }
        SqliteConnection.ClearAllPools();

        var incompatible = await ReleaseReviewReporter.BuildReportAsync(new ReleaseReviewOptions(
            index,
            index,
            Path.Combine(temp.Path, "incompatible-release-review.md"),
            Scope: "access-evidence"));
        Assert.Equal(ReleaseReviewStatuses.Unavailable, incompatible.AccessEvidence.Status);
        Assert.Contains(incompatible.AccessEvidence.Gaps, item => item.GapKind == "AccessEvidenceProvenanceUnavailable");
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
        Assert.Contains("$releaseReview.accessEvidence.status -ne \"available\"", script, StringComparison.Ordinal);
        Assert.Contains("Access release review omitted composed design evidence", script, StringComparison.Ordinal);
        Assert.Contains("vault-export.gap.access-evidence-consumer-unsupported.v1", script, StringComparison.Ordinal);
        Assert.Contains("Phase 9 checkpoint must be outside the disposable smoke root", script, StringComparison.Ordinal);
        Assert.Contains("[string]::Equals($Phase9CheckpointPath, $SmokeRoot", script, StringComparison.Ordinal);
        Assert.Contains("Assert-DisposableSmokeRoot -Path $SmokeRoot", script, StringComparison.Ordinal);
        Assert.Contains("Existing smoke root is missing the TraceMap disposable marker", script, StringComparison.Ordinal);
        Assert.Contains(".tracemap-smoke-root", script, StringComparison.Ordinal);
        Assert.Contains("Remove-Item $SmokeRoot -Recurse -Force -ErrorAction Stop", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Remove-Item $SmokeRoot -Recurse -Force -ErrorAction SilentlyContinue", script, StringComparison.Ordinal);
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
        Assert.Contains("$releaseReview.accessEvidence.status -ne \"available\"", script, StringComparison.Ordinal);
        Assert.Contains("representative release-review contract failed", script, StringComparison.Ordinal);
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
        var table = AccessSafeValues.Identity(seed, "table", "OrdersPrivate");
        var field = AccessSafeValues.Identity(seed, $"field-{table.StableKey}", "CustomerSecretField");
        var indexIdentity = AccessSafeValues.Identity(seed, $"index-{table.StableKey}", "PrivateIndex");
        var relationshipIdentity = AccessSafeValues.Identity(seed, "relationship", "PrivateRelationship");
        var queryIdentity = AccessSafeValues.Identity(seed, "query", "PrivateQuery");
        var externalIdentity = AccessSafeValues.Identity(seed, "table", "PrivateServer");
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
            [new(table, [new(field, 0, "long", 4, true)], [new(indexIdentity, true, true, [field.StableKey])])],
            [new(relationshipIdentity, table.StableKey, table.StableKey, 0, [new(field.StableKey, field.StableKey, 0)])],
            [new(queryIdentity, "select", AccessSafeValues.RoleHash("access-query-sql", "SELECT * FROM OrdersPrivate"), 27, "complete", [], [new(table.StableKey, "table", "direct-static-reference")], false, null, null)],
            [new(externalIdentity, "odbc", AccessSafeValues.RoleHash("access-linked-source", "PrivateServer"), "linked-table")],
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
