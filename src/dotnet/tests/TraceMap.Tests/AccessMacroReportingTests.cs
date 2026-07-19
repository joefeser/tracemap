using System.Text.Json;
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
        Assert.Contains("Macro protected-body gaps: 3", report, StringComparison.Ordinal);
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
        Assert.DoesNotContain(ProtectedMacroName, JsonSerializer.Serialize(docs), StringComparison.OrdinalIgnoreCase);
        AssertArtifactsDoNotContain(docsOutput, ProtectedMacroName);
    }

    [Fact]
    public async Task Vault_preserves_Access_macro_rule_and_release_review_emits_structured_unsupported_consumer_gap()
    {
        using var temp = new TempDirectory();
        var (scan, output) = await BuildScanAsync(temp.Path);
        await AccessArtifactWriter.WriteAsync(output, scan, AccessLimits.Default);
        var index = Path.Combine(output, "index.sqlite");
        var combined = Path.Combine(temp.Path, "combined.sqlite");
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([index], combined, ["access"]));

        var vaultOutput = Path.Combine(temp.Path, "vault-output");
        var vault = await VaultExporter.ExportAsync(new VaultExportOptions(
            combined,
            vaultOutput,
            MinimumClaimLevel: "hidden",
            Date: "2026-07",
            Format: "markdown,json"));
        Assert.Contains(vault.Graph.Nodes, node => node.RuleIds.Contains(RuleIds.LegacyAccessMacroGap, StringComparer.Ordinal));
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
            macro.Gaps,
            [new("macros", "inventory-observed-body-omitted")],
            Macros: macro.Macros);
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
}
