using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TraceMap.Access;
using TraceMap.Access.Cli;
using TraceMap.Combine;
using TraceMap.Core;
using TraceMap.Reporting;

namespace TraceMap.Tests;

public sealed class AccessDesignEvidenceCompositionTests
{
    private const string RepositoryHash = "1111111111111111111111111111111111111111111111111111111111111111";
    private const string CommitSha = "2222222222222222222222222222222222222222";
    private const string DatabaseHash = "3333333333333333333333333333333333333333333333333333333333333333";
    private const string ProtectedForm = "Customer_Claims_Private_Form_91827";
    private const string ProtectedControl = "Password_Reset_Private_Button_81274";
    private const string ProtectedModule = "Private_Server_Module_71923";
    private const string ProtectedMacro = "Credential_Rotation_Macro_61922";

    [Fact]
    public async Task Enrichment_is_deterministic_hash_only_immutable_and_preserved_by_downstream_consumers()
    {
        using var temp = new TempDirectory();
        var baseScan = await WriteBaseScanAsync(temp.Path);
        var baseBefore = Snapshot(baseScan);
        var design = WriteDesignBundle(temp.Path, baseScan);
        var designBefore = Snapshot(design);

        var firstResult = await AccessDesignEvidenceComposer.ComposeAsync(baseScan, design);
        var secondResult = await AccessDesignEvidenceComposer.ComposeAsync(baseScan, design);
        var first = Path.Combine(temp.Path, "enriched-first");
        var second = Path.Combine(temp.Path, "enriched-second");
        await AccessArtifactWriter.WriteAsync(first, firstResult, AccessLimits.Default);
        await AccessArtifactWriter.WriteAsync(second, secondResult, AccessLimits.Default);

        AssertDirectoriesEqual(first, second);
        Assert.Equal(baseBefore, Snapshot(baseScan));
        Assert.Equal(designBefore, Snapshot(design));
        Assert.Equal(firstResult.Manifest.ScannedAt, secondResult.Manifest.ScannedAt);
        Assert.StartsWith("access-enriched-", firstResult.Manifest.ScanId, StringComparison.Ordinal);
        Assert.Contains(firstResult.Facts, fact => fact.RuleId == RuleIds.LegacyAccessDesignInput);
        Assert.Contains(firstResult.Facts, fact => fact.FactType == FactTypes.AccessFormDeclared);
        Assert.Contains(firstResult.Facts, fact => fact.FactType == FactTypes.AccessControlDeclared);
        Assert.Contains(firstResult.Facts, fact => fact.FactType == FactTypes.AccessVbaModuleDeclared);
        Assert.Contains(firstResult.Facts, fact => fact.FactType == FactTypes.AccessVbaProcedureDeclared);
        Assert.Contains(firstResult.Facts, fact => fact.FactType == FactTypes.AccessMacroDeclared);
        Assert.Contains(firstResult.Facts, fact => fact.FactType == FactTypes.AccessFormDeclared
            && fact.EvidenceTier == EvidenceTiers.Tier2Structural
            && fact.Properties.GetValueOrDefault("coverageLabel") == "structured-design-observed");
        Assert.Contains(firstResult.Facts, fact => fact.FactType == FactTypes.AccessVbaModuleDeclared
            && fact.EvidenceTier == EvidenceTiers.Tier3SyntaxOrTextual
            && fact.Evidence.ExtractorId == "AccessSourceNeutralDesignEvidence"
            && fact.Properties.GetValueOrDefault("coverageLabel") == "bounded-textual-design");
        Assert.Contains(firstResult.Facts, fact => fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyAccessDesignInput
            && fact.EvidenceTier == EvidenceTiers.Tier4Unknown);
        Assert.All(firstResult.Facts.Where(IsDesignFact), fact =>
        {
            Assert.True(fact.Properties.ContainsKey("designInputHash")
                || fact.RuleId == RuleIds.LegacyAccessDesignInput);
            Assert.DoesNotContain("objectName", fact.Properties.Keys);
            Assert.DoesNotContain("literalTargetName", fact.Properties.Keys);
            Assert.StartsWith("access-design-", fact.Properties["sourceCanonicalRecordIds"], StringComparison.Ordinal);
            Assert.Equal("synthetic-hand-authored", fact.Properties["designMechanism"]);
            Assert.Equal("hash-identical", fact.Properties["copyBinding"]);
            Assert.Contains("no-execution", fact.Properties["nonClaims"], StringComparison.Ordinal);
        });
        AssertNoProtectedMaterial(first);

        var index = Path.Combine(first, "index.sqlite");
        var combined = Path.Combine(temp.Path, "combined.sqlite");
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([index], combined, ["access-enriched"]));
        var docsOutput = Path.Combine(temp.Path, "evidence-docs");
        var docs = await EvidenceDocsExporter.ExportAsync(new EvidenceDocsExportOptions(
            index, docsOutput, Families: "legacy,gap,limitation", Format: "markdown,jsonl", Date: "2026-07"));
        Assert.Contains(docs.Chunks, chunk => chunk.RuleIds.Contains(RuleIds.LegacyAccessUiSurface, StringComparer.Ordinal));
        var vaultOutput = Path.Combine(temp.Path, "vault");
        await VaultExporter.ExportAsync(new VaultExportOptions(
            combined, vaultOutput, MinimumClaimLevel: "hidden", Date: "2026-07", Format: "markdown,json"));
        var release = await ReleaseReviewReporter.BuildReportAsync(new ReleaseReviewOptions(
            index, index, Path.Combine(temp.Path, "release-review.md")));
        Assert.Equal(ReleaseReviewStatuses.Available, release.AccessEvidence.Status);
        Assert.Contains(release.AccessEvidence.Findings,
            finding => finding.Metadata.Any(pair => pair.Key == "evidenceKind" && pair.Value == "form"));
        Assert.Contains(release.AccessEvidence.Findings,
            finding => finding.Metadata.Any(pair => pair.Key == "designInputHash"));
        var localReviewOutput = Path.Combine(temp.Path, "local-review");
        var localReview = await AccessLocalReviewBundle.CreateAsync(new(first, localReviewOutput));
        Assert.Equal(ReleaseReviewStatuses.Available, localReview.Manifest.AccessEvidenceStatus);
        AssertNoProtectedMaterial(docsOutput);
        AssertNoProtectedMaterial(vaultOutput);
        AssertNoProtectedMaterial(localReviewOutput);
        AssertNoProtectedMaterial(combined);
    }

    [Fact]
    public async Task Cli_requires_explicit_inputs_and_rejects_database_identity_mismatch_without_output()
    {
        using var temp = new TempDirectory();
        var baseScan = await WriteBaseScanAsync(temp.Path);
        var design = WriteDesignBundle(temp.Path, baseScan, databaseIdentityOverride: new string('9', 64));
        var output = Path.Combine(temp.Path, "rejected-output");
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exit = await AccessCommand.RunAsync(
            ["enrich-design", "--base-scan", baseScan, "--design-evidence", design, "--out", output],
            stdout,
            stderr);

        Assert.Equal(1, exit);
        Assert.Contains("AccessDesignInputDatabaseUnbound", stderr.ToString(), StringComparison.Ordinal);
        Assert.False(Directory.Exists(output));
        Assert.DoesNotContain(temp.Path, stderr.ToString(), StringComparison.Ordinal);

        stderr.GetStringBuilder().Clear();
        var nestedExit = await AccessCommand.RunAsync(
            ["enrich-design", "--base-scan", baseScan, "--design-evidence", design, "--out", Path.Combine(design, "output")],
            stdout,
            stderr);
        Assert.Equal(1, nestedExit);
        Assert.Contains("AccessUnsafeOutputPath", stderr.ToString(), StringComparison.Ordinal);
        Assert.False(Directory.Exists(Path.Combine(design, "output")));
    }

    private static async Task<string> WriteBaseScanAsync(string root)
    {
        var database = Path.Combine(root, "fixture.accdb");
        await File.WriteAllBytesAsync(database, [1, 2, 3, 4]);
        var output = Path.Combine(root, "base-scan");
        var input = new AccessValidatedInput(
            root,
            "synthetic-repo",
            RepositoryHash,
            null,
            "dev",
            CommitSha,
            database,
            "fixtures/synthetic.accdb",
            DatabaseHash,
            ".accdb",
            output,
            false,
            4);
        var projection = new AccessDatabaseProjection(
            "tracemap.access-projection.v1",
            DatabaseHash,
            ".accdb",
            "synthetic",
            0,
            false,
            false,
            0,
            [],
            [],
            [],
            [],
            [new("AccessUiCatalogUnavailable", "ui-catalog", null, RuleIds.LegacyAccessCoverageGap)],
            []);
        var scan = AccessFactBuilder.Build(input, projection, new(root, "fixtures/synthetic.accdb", output));
        await AccessArtifactWriter.WriteAsync(output, scan, AccessLimits.Default);
        return output;
    }

    private static string WriteDesignBundle(
        string root,
        string baseScan,
        string? databaseIdentityOverride = null)
    {
        var directory = Path.Combine(root, "protected-design-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var vba = $"' {ProtectedForm}\nPublic Sub HandleClick()\nDoCmd.OpenForm \"{ProtectedForm}\"\nEnd Sub";
        var vbaHash = Sha256(Encoding.UTF8.GetBytes(vba));
        var records = new[]
        {
            Record("catalog-object", "catalog-form", null, "catalog-export", "container-only", null, null, null, "complete",
                Object(("objectRole", "form"), ("identity", ProtectedForm), ("ordinal", 0))),
            Record("ui-surface", "surface", "catalog-form", "form-design-export", "container-only", null, null, null, "complete",
                Object(("surfaceRole", "form"), ("identity", ProtectedForm), ("ordinal", 0),
                    ("modulePresence", "present"), ("boundState", "bound"),
                    ("events", Object(("on-load", "[Event Procedure]"))))),
            Record("ui-control", "control", "surface", "form-design-export", "container-only", null, null, null, "complete",
                Object(("identity", ProtectedControl), ("ordinal", 0), ("controlType", 104),
                    ("events", Object(("on-click", "[Event Procedure]"))))),
            Record("vba-module", "module", null, "vba-module-export", "exact-lines", vbaHash, 1, 4, "complete",
                Object(("moduleRole", "standard"), ("identity", ProtectedModule), ("moduleKind", "standard"),
                    ("sourceText", vba), ("sourceSha256", vbaHash), ("lineCount", 4), ("coordinateBasis", "module-relative"))),
            Record("macro-inventory", "macro", null, "macro-inventory-export", "unavailable", null, null, null, "partial",
                Object(("macroCategory", "named"), ("identity", ProtectedMacro), ("ordinal", 0),
                    ("startupRole", "not-autoexec"), ("bodyStatus", "protected-omitted"))),
            Record("source-gap", "gap", null, "producer-gap", "unavailable", null, null, null, "partial",
                Object(("classification", "source-unavailable"), ("affectedScope", "macro"), ("coverageCategory", "source-unavailable")))
        };
        var recordsBytes = Encoding.UTF8.GetBytes(string.Join('\n', records) + "\n");
        File.WriteAllBytes(Path.Combine(directory, AccessDesignEvidenceReader.RecordsFileName), recordsBytes);
        var baseManifestBytes = File.ReadAllBytes(Path.Combine(baseScan, "scan-manifest.json"));
        var databaseSeed = AccessSafeValues.DatabaseIdentitySeed(
            RepositoryHash, CommitSha, "fixtures/synthetic.accdb", DatabaseHash);
        var counts = records.Select(record => JsonDocument.Parse(record))
            .GroupBy(document => document.RootElement.GetProperty("kind").GetString()!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var manifest = Object(
            ("schema", AccessDesignEvidenceReader.ManifestSchema),
            ("producer", Object(("id", "tracemap-synthetic-fixture"), ("version", "1.0.0"), ("mechanism", "synthetic-hand-authored"))),
            ("repository", Object(("identityHash", RepositoryHash), ("commitSha", CommitSha))),
            ("baseScan", Object(
                ("manifestSha256", Sha256(baseManifestBytes)),
                ("databaseIdentityHash", databaseIdentityOverride ?? databaseSeed))),
            ("sourceCopy", Object(("sha256", DatabaseHash), ("binding", "hash-identical"))),
            ("records", Object(("sha256", Sha256(recordsBytes)), ("count", records.Length), ("countsByKind", counts))),
            ("capabilities", Object(("coordinates", "mixed"), ("catalogCompleteness", "declared-partial"), ("identityDisclosure", "hash-only"))),
            ("exportedAtUtc", "2026-07-29T10:00:00Z"));
        File.WriteAllText(
            Path.Combine(directory, AccessDesignEvidenceReader.ManifestFileName),
            JsonSerializer.Serialize(manifest),
            new UTF8Encoding(false));
        return directory;
    }

    private static string Record(
        string kind,
        string id,
        string? parent,
        string role,
        string coordinateStatus,
        string? hash,
        int? start,
        int? end,
        string completeness,
        IReadOnlyDictionary<string, object?> payload)
    {
        var source = Object(("documentRole", role), ("coordinateStatus", coordinateStatus));
        if (hash is not null) source["documentSha256"] = hash;
        if (start is not null) source["startLine"] = start;
        if (end is not null) source["endLine"] = end;
        return JsonSerializer.Serialize(Object(
            ("schema", AccessDesignEvidenceReader.RecordSchema),
            ("kind", kind),
            ("recordId", id),
            ("parentRecordId", parent),
            ("source", source),
            ("completeness", completeness),
            ("payload", payload)));
    }

    private static Dictionary<string, object?> Object(params (string Key, object? Value)[] values) =>
        values.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal);

    private static string Sha256(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static IReadOnlyDictionary<string, string> Snapshot(string root) =>
        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .ToDictionary(
                path => Path.GetRelativePath(root, path).Replace('\\', '/'),
                path => Sha256(File.ReadAllBytes(path)),
                StringComparer.Ordinal);

    private static void AssertDirectoriesEqual(string first, string second)
    {
        var left = Snapshot(first);
        var right = Snapshot(second);
        Assert.Equal(left.OrderBy(item => item.Key), right.OrderBy(item => item.Key));
    }

    private static bool IsDesignFact(CodeFact fact) =>
        fact.Evidence.ExtractorId == "AccessSourceNeutralDesignEvidence";

    private static void AssertNoProtectedMaterial(string path)
    {
        var files = File.Exists(path) ? [path] : Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories);
        foreach (var file in files)
        {
            var text = Encoding.UTF8.GetString(File.ReadAllBytes(file));
            foreach (var marker in new[] { ProtectedForm, ProtectedControl, ProtectedModule, ProtectedMacro, "Password_Reset" })
                Assert.DoesNotContain(marker, text, StringComparison.OrdinalIgnoreCase);
        }
    }
}
