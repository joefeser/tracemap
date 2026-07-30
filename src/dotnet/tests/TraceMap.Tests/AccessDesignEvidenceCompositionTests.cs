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
            && fact.Properties.GetValueOrDefault("coverageLabel") == "structured-design-observed"
            && fact.Properties.GetValueOrDefault("boundState") == "bound-declared"
            && fact.Properties["sourceCanonicalRecordIds"].Split(';').Length == 2);
        Assert.Contains(firstResult.Facts, fact => fact.FactType == FactTypes.AccessBindingDeclared
            && fact.Properties.GetValueOrDefault("bindingKind") == "record-source"
            && fact.TargetSymbol == firstResult.Facts.Single(item =>
                item.FactType == FactTypes.AccessQueryDeclared
                && item.Properties.GetValueOrDefault("objectName") == "SharedQuery").TargetSymbol);
        Assert.Contains(firstResult.Facts, fact => fact.FactType == FactTypes.AccessMacroDeclared
            && fact.Properties.GetValueOrDefault("startupRole") == "autoexec"
            && fact.Properties.GetValueOrDefault("bodyStatus") == "unavailable");
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

        if (!OperatingSystem.IsLinux())
        {
            stderr.GetStringBuilder().Clear();
            var differentlyCasedDesign = string.Concat(
                design[..^Path.GetFileName(design).Length],
                Path.GetFileName(design).ToUpperInvariant());
            var caseAliasExit = await AccessCommand.RunAsync(
                ["enrich-design", "--base-scan", baseScan, "--design-evidence", design, "--out", Path.Combine(differentlyCasedDesign, "output")],
                stdout,
                stderr);
            Assert.Equal(1, caseAliasExit);
            Assert.Contains("AccessUnsafeOutputPath", stderr.ToString(), StringComparison.Ordinal);
        }

        var alias = Path.Combine(temp.Path, "design-alias");
        try
        {
            Directory.CreateSymbolicLink(alias, design);
            stderr.GetStringBuilder().Clear();
            var symlinkExit = await AccessCommand.RunAsync(
                ["enrich-design", "--base-scan", baseScan, "--design-evidence", design, "--out", Path.Combine(alias, "output")],
                stdout,
                stderr);
            Assert.Equal(1, symlinkExit);
            Assert.Contains("AccessUnsafeOutputPath", stderr.ToString(), StringComparison.Ordinal);
        }
        catch (UnauthorizedAccessException) when (OperatingSystem.IsWindows())
        {
            // Windows developer mode controls whether test processes may create symlinks.
        }
    }

    [Fact]
    public async Task Bounded_reader_rejects_lengths_that_cannot_be_safely_buffered()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "sparse.bin");
        await using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            stream.SetLength((long)int.MaxValue + 1);

        var error = await Assert.ThrowsAsync<AccessScanException>(() =>
            AccessDesignEvidenceComposer.ReadBoundedAsync(
                path,
                (long)int.MaxValue + 2,
                CancellationToken.None));

        Assert.Equal("AccessBaseScanArtifactLimitReached", error.Classification);
    }

    [Fact]
    public async Task Enrichment_accumulates_support_when_distinct_records_share_a_projection_identity()
    {
        using var temp = new TempDirectory();
        var baseScan = await WriteBaseScanAsync(temp.Path);
        var design = WriteDesignBundle(temp.Path, baseScan, includeProjectionIdentityDuplicates: true);

        var result = await AccessDesignEvidenceComposer.ComposeAsync(baseScan, design);

        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.AccessVbaModuleDeclared
            && fact.Properties["sourceCanonicalRecordIds"].Split(';').Length == 2);
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.AccessMacroDeclared
            && fact.Properties["sourceCanonicalRecordIds"].Split(';').Length == 2);
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
        var databaseSeed = AccessSafeValues.DatabaseIdentitySeed(
            RepositoryHash, CommitSha, "fixtures/synthetic.accdb", DatabaseHash);
        var sharedQuery = AccessSafeValues.Identity(databaseSeed, "query", "SharedQuery");
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
            [new(
                sharedQuery,
                "select",
                new string('4', 64),
                16,
                "complete",
                [],
                [],
                false,
                null,
                null)],
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
        string? databaseIdentityOverride = null,
        bool includeProjectionIdentityDuplicates = false)
    {
        var directory = Path.Combine(root, "protected-design-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var vba = $"' {ProtectedForm}\nPublic Sub HandleClick()\nDoCmd.OpenForm \"{ProtectedForm}\"\nEnd Sub";
        var vbaHash = Sha256(Encoding.UTF8.GetBytes(vba));
        var designText = "Begin Form\n    HasModule = -1\nEnd";
        var designTextHash = Sha256(Encoding.UTF8.GetBytes(designText));
        var records = new List<string>
        {
            Record("catalog-object", "catalog-query", null, "catalog-export", "container-only", null, null, null, "complete",
                Object(("objectRole", "saved-query"), ("identity", "SharedQuery"), ("ordinal", 0))),
            Record("catalog-object", "catalog-form", null, "catalog-export", "container-only", null, null, null, "complete",
                Object(("objectRole", "form"), ("identity", ProtectedForm), ("ordinal", 0))),
            Record("ui-design-document", "ui-design-document", "catalog-form", "form-design-export", "exact-lines", designTextHash, 1, 3, "complete",
                Object(("documentRole", "form"), ("designText", designText), ("designSha256", designTextHash), ("lineCount", 3))),
            Record("ui-surface", "surface", "catalog-form", "form-design-export", "container-only", null, null, null, "complete",
                Object(("surfaceRole", "form"), ("identity", ProtectedForm), ("ordinal", 0),
                    ("modulePresence", "present"), ("boundState", "bound"),
                    ("recordSource", "SharedQuery"),
                    ("events", Object(("on-load", "[Event Procedure]"))))),
            Record("ui-control", "control", "surface", "form-design-export", "container-only", null, null, null, "complete",
                Object(("identity", ProtectedControl), ("ordinal", 0), ("controlType", 104),
                    ("events", Object(("on-click", "[Event Procedure]"))))),
            Record("vba-module", "module", null, "vba-module-export", "exact-lines", vbaHash, 1, 4, "complete",
                Object(("moduleRole", "standard"), ("identity", ProtectedModule), ("moduleKind", "standard"),
                    ("sourceText", vba), ("sourceSha256", vbaHash), ("lineCount", 4), ("coordinateBasis", "module-relative"))),
            Record("macro-inventory", "macro", null, "macro-inventory-export", "unavailable", null, null, null, "partial",
                Object(("macroCategory", "named"), ("identity", ProtectedMacro), ("ordinal", 0),
                    ("startupRole", "autoexec"), ("bodyStatus", "unavailable"))),
            Record("source-gap", "gap", null, "producer-gap", "unavailable", null, null, null, "partial",
                Object(("classification", "source-unavailable"), ("affectedScope", "macro"), ("coverageCategory", "source-unavailable")))
        };
        if (includeProjectionIdentityDuplicates)
        {
            records.Add(Record(
                "vba-module",
                "module-alternate-role",
                null,
                "vba-module-export",
                "exact-lines",
                vbaHash,
                1,
                4,
                "complete",
                Object(
                    ("moduleRole", "document"),
                    ("identity", ProtectedModule),
                    ("moduleKind", "standard"),
                    ("sourceText", vba),
                    ("sourceSha256", vbaHash),
                    ("lineCount", 4),
                    ("coordinateBasis", "module-relative"))));
            records.Add(Record(
                "macro-inventory",
                "macro-owner",
                null,
                "macro-inventory-export",
                "unavailable",
                null,
                null,
                null,
                "partial",
                Object(
                    ("macroCategory", "named"),
                    ("identity", ProtectedMacro),
                    ("ownerRole", "database"),
                    ("ordinal", 0),
                    ("startupRole", "autoexec"),
                    ("bodyStatus", "unavailable"))));
        }
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
            ("records", Object(("sha256", Sha256(recordsBytes)), ("count", records.Count), ("countsByKind", counts))),
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
