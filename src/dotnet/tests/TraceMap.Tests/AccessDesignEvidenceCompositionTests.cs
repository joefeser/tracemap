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
    private const string ProtectedField = "Private Field 51729";
    private const string UnusedProtectedQuery = "Unused_Private_Query_41922";
    private const string UnusedProtectedTable = "Unused_Private_Table_31922";
    private const string OrphanProtectedField = "Orphan_Private_Field_21922";

    [Fact]
    public async Task Hidden_identity_projection_is_explicit_hash_inventoried_and_independently_deletable()
    {
        using var temp = new TempDirectory();
        var baseScan = await WriteBaseScanAsync(temp.Path);
        var design = WriteDesignBundle(temp.Path, baseScan, includeHiddenIdentityRegressions: true);
        var output = Path.Combine(temp.Path, "hidden-identities");

        var result = await AccessHiddenIdentityProjection.WriteAsync(baseScan, design, output);

        Assert.True(result.IdentityCount >= 2);
        var payload = await File.ReadAllTextAsync(Path.Combine(output, "access-identities.json"));
        var manifestText = await File.ReadAllTextAsync(Path.Combine(output, "access-identity-manifest.json"));
        Assert.Contains(ProtectedForm, payload, StringComparison.Ordinal);
        Assert.Contains(ProtectedControl, payload, StringComparison.Ordinal);
        Assert.DoesNotContain(ProtectedModule, payload, StringComparison.Ordinal);
        Assert.DoesNotContain(ProtectedMacro, payload, StringComparison.Ordinal);
        Assert.DoesNotContain("sourceText", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("designText", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("SELECT Password FROM Users", payload, StringComparison.OrdinalIgnoreCase);
        using var payloadDocument = JsonDocument.Parse(payload);
        var identities = payloadDocument.RootElement.GetProperty("identities").EnumerateArray().ToArray();
        Assert.DoesNotContain(identities, item =>
            item.GetProperty("role").GetString() == "saved-query"
            && item.GetProperty("identity").GetString() == UnusedProtectedQuery);
        Assert.DoesNotContain(identities, item =>
            item.GetProperty("role").GetString() == "table"
            && item.GetProperty("identity").GetString() == UnusedProtectedTable);
        Assert.DoesNotContain(identities, item =>
            item.GetProperty("role").GetString() == "table-field"
            && item.GetProperty("identity").GetString() == OrphanProtectedField);
        var queryIdentity = identities
            .Single(item => item.GetProperty("role").GetString() == "saved-query");
        var baseQuery = File.ReadLines(Path.Combine(baseScan, "facts.ndjson"))
            .Select(line => JsonSerializer.Deserialize<CodeFact>(
                line,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!)
            .Single(fact => fact.FactType == FactTypes.AccessQueryDeclared);
        Assert.Equal(baseQuery.TargetSymbol, queryIdentity.GetProperty("stableKey").GetString());
        Assert.Contains("\"claimLevel\": \"hidden\"", manifestText, StringComparison.Ordinal);
        using var manifest = JsonDocument.Parse(manifestText);
        var file = Assert.Single(manifest.RootElement.GetProperty("files").EnumerateArray());
        Assert.Equal("access-identities.json", file.GetProperty("path").GetString());
        Assert.Equal(
            Sha256(await File.ReadAllBytesAsync(Path.Combine(output, "access-identities.json"))),
            file.GetProperty("sha256").GetString());
        Directory.Delete(output, true);
        Assert.True(Directory.Exists(baseScan));
        Assert.True(Directory.Exists(design));
    }

    [Fact]
    public async Task Hidden_identity_projection_deduplicates_controls_observed_in_structured_and_text_evidence()
    {
        using var temp = new TempDirectory();
        var baseScan = await WriteBaseScanAsync(temp.Path);
        var design = WriteDesignBundle(temp.Path, baseScan, includeReviewRegressions: true);
        var output = Path.Combine(temp.Path, "hidden-identities");

        var result = await AccessHiddenIdentityProjection.WriteAsync(baseScan, design, output);

        using var payload = JsonDocument.Parse(
            await File.ReadAllTextAsync(Path.Combine(output, "access-identities.json")));
        var identities = payload.RootElement.GetProperty("identities").EnumerateArray().ToArray();
        Assert.Contains("BaseTable", payload.RootElement.GetRawText(), StringComparison.Ordinal);
        Assert.Contains(ProtectedField, payload.RootElement.GetRawText(), StringComparison.Ordinal);
        var controls = identities.Where(item =>
            item.GetProperty("role").GetString() == "control"
            && item.GetProperty("identity").GetString() == ProtectedControl).ToArray();
        var control = Assert.Single(controls);
        Assert.Equal("ui-control", control.GetProperty("recordKind").GetString());
        Assert.Equal(identities.Length, result.IdentityCount);
    }

    [Fact]
    public async Task Hidden_identity_projection_rejects_a_base_scan_with_an_inconsistent_database_stable_key()
    {
        using var temp = new TempDirectory();
        var baseScan = await WriteBaseScanAsync(temp.Path);
        var design = WriteDesignBundle(temp.Path, baseScan);
        var factsPath = Path.Combine(baseScan, "facts.ndjson");
        var jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var facts = File.ReadLines(factsPath)
            .Select(line => JsonSerializer.Deserialize<CodeFact>(line, jsonOptions)!)
            .Select(fact => fact.FactType == FactTypes.LegacyDataMetadataDeclared
                && fact.RuleId == RuleIds.LegacyAccessDatabaseInventory
                    ? fact with { TargetSymbol = "access-database-inconsistent" }
                    : fact)
            .Select(fact => JsonSerializer.Serialize(fact, jsonOptions));
        await File.WriteAllTextAsync(factsPath, string.Join('\n', facts) + "\n", new UTF8Encoding(false));
        var output = Path.Combine(temp.Path, "hidden-identities");

        var error = await Assert.ThrowsAsync<AccessScanException>(() =>
            AccessHiddenIdentityProjection.WriteAsync(baseScan, design, output));

        Assert.Equal("AccessDesignInputDatabaseUnbound", error.Classification);
        Assert.False(Directory.Exists(output));
    }

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

    [Fact]
    public async Task Enrichment_resolves_hash_only_fields_flags_control_conflicts_and_preserves_event_and_macro_owners()
    {
        using var temp = new TempDirectory();
        var baseScan = await WriteBaseScanAsync(temp.Path);
        var design = WriteDesignBundle(temp.Path, baseScan, includeReviewRegressions: true);

        var result = await AccessDesignEvidenceComposer.ComposeAsync(baseScan, design);

        var field = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.LegacyDataColumnDeclared);
        Assert.DoesNotContain("objectName", field.Properties.Keys);
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.AccessBindingDeclared
            && fact.Properties.GetValueOrDefault("bindingKind") == "control-source"
            && fact.TargetSymbol == field.TargetSymbol);
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.AnalysisGap
            && fact.Properties.GetValueOrDefault("classification") == "AccessDesignInputSurfaceConflict");
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.AccessFormDeclared
            && fact.Properties.GetValueOrDefault("projectorCoverage") == "partial");
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.AccessEventBindingCandidate
            && fact.Properties.GetValueOrDefault("projectorCoverage") == "complete"
            && fact.Properties.ContainsKey("procedureStableKey"));
        var macros = result.Facts.Where(fact => fact.FactType == FactTypes.AccessMacroDeclared
            && fact.Properties.GetValueOrDefault("macroKind") == "embedded").ToArray();
        Assert.Equal(2, macros.Length);
        Assert.Equal(2, macros.Select(fact => fact.TargetSymbol).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(2, macros.Select(fact => fact.Properties["ownerStableKey"]).Distinct(StringComparer.Ordinal).Count());
        Assert.DoesNotContain(result.Facts, fact => fact.FactType == FactTypes.AnalysisGap
            && fact.Properties.GetValueOrDefault("classification") == "AccessMacroOwnerUnavailable");
        AssertNoProtectedMaterial(await WriteResultAsync(temp.Path, result));
    }

    [Fact]
    public async Task Enrichment_maps_an_exact_zero_argument_event_expression_to_its_owning_form_module_without_persisting_expression_text()
    {
        using var temp = new TempDirectory();
        var baseScan = await WriteBaseScanAsync(temp.Path);
        var design = WriteDesignBundle(temp.Path, baseScan, includeExpressionEvent: true);

        var result = await AccessDesignEvidenceComposer.ComposeAsync(baseScan, design);

        var binding = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.AccessEventBindingCandidate
            && fact.Properties.GetValueOrDefault("eventRole") == "on-click"
            && fact.Properties.GetValueOrDefault("projectorCoverage") == "complete");
        Assert.NotNull(binding.SourceSymbol);
        Assert.NotNull(binding.TargetSymbol);
        Assert.Equal("control", binding.Properties.GetValueOrDefault("ownerKind"));
        Assert.Equal("expression-function", binding.Properties.GetValueOrDefault("bindingKind"));
        Assert.NotNull(binding.Properties.GetValueOrDefault("eventExpressionHash"));
        Assert.Equal(1, binding.Evidence.StartLine);
        Assert.Equal(7, binding.Evidence.EndLine);
        var control = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.AccessControlDeclared
            && fact.Properties.GetValueOrDefault("eventDescriptors")?.Contains("on-click:expression:", StringComparison.Ordinal) == true);
        Assert.Equal(control.TargetSymbol, binding.SourceSymbol);
        var effect = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.AccessUiStateEffectCandidate);
        Assert.Equal(binding.TargetSymbol, effect.SourceSymbol);
        Assert.Equal("control-state-assignment", effect.Properties.GetValueOrDefault("effectKind"));
        Assert.NotNull(effect.Properties.GetValueOrDefault("conditionHash"));

        var output = await WriteResultAsync(temp.Path, result);
        var text = await File.ReadAllTextAsync(Path.Combine(output, "facts.ndjson"));
        Assert.DoesNotContain("RunSelectedScenario", text, StringComparison.Ordinal);
        Assert.DoesNotContain("OpenArgsMarker", text, StringComparison.Ordinal);
        Assert.DoesNotContain("StatusFilterMarker", text, StringComparison.Ordinal);
        Assert.DoesNotContain("txtChoice", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Enrichment_marks_a_report_group_only_conflict_as_partial()
    {
        using var temp = new TempDirectory();
        var baseScan = await WriteBaseScanAsync(temp.Path);
        var design = WriteDesignBundle(temp.Path, baseScan, includeReportGroupConflict: true);

        var result = await AccessDesignEvidenceComposer.ComposeAsync(baseScan, design);

        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.AnalysisGap
            && fact.Properties.GetValueOrDefault("classification") == "AccessDesignInputSurfaceConflict");
        var report = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.AccessReportDeclared);
        Assert.Equal("partial", report.Properties.GetValueOrDefault("projectorCoverage"));
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
        var table = AccessSafeValues.Identity(databaseSeed, "table", "BaseTable");
        var field = AccessSafeValues.Identity(databaseSeed, $"field-{table.StableKey}", ProtectedField);
        var projection = new AccessDatabaseProjection(
            "tracemap.access-projection.v1",
            DatabaseHash,
            ".accdb",
            "synthetic",
            0,
            false,
            false,
            0,
            [new(table, [new(field, 0, "text", 255, false)], [])],
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
        bool includeProjectionIdentityDuplicates = false,
        bool includeReviewRegressions = false,
        bool includeHiddenIdentityRegressions = false,
        bool includeReportGroupConflict = false,
        bool includeExpressionEvent = false)
    {
        var directory = Path.Combine(root, "protected-design-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var vba = $"' {ProtectedForm}\nPublic Sub HandleClick()\nDoCmd.OpenForm \"{ProtectedForm}\"\nEnd Sub";
        var vbaHash = Sha256(Encoding.UTF8.GetBytes(vba));
        var designText = includeReviewRegressions
            ? $"Begin Form\n    HasModule = -1\n    Begin TextBox\n        Name =\"{ProtectedControl}\"\n        ControlSource =\"Different Field 51729\"\n    End\nEnd"
            : "Begin Form\n    HasModule = -1\nEnd";
        var designTextHash = Sha256(Encoding.UTF8.GetBytes(designText));
        var records = new List<string>
        {
            Record("catalog-object", "catalog-query", null, "catalog-export", "container-only", null, null, null, "complete",
                Object(("objectRole", "saved-query"), ("identity", "SharedQuery"), ("ordinal", 0))),
            Record("catalog-object", "catalog-form", null, "catalog-export", "container-only", null, null, null, "complete",
                Object(("objectRole", "form"), ("identity", ProtectedForm), ("ordinal", 0))),
            Record("ui-design-document", "ui-design-document", "catalog-form", "form-design-export", "exact-lines", designTextHash, 1, 3, "complete",
                Object(("documentRole", "form"), ("designText", designText),
                    (includeReviewRegressions ? "documentSha256" : "designSha256", designTextHash),
                    ("lineCount", designText.Count(character => character == '\n') + 1))),
            Record("ui-surface", "surface", "catalog-form", "form-design-export", "container-only", null, null, null, "complete",
                Object(("surfaceRole", "form"), ("identity", ProtectedForm), ("ordinal", 0),
                    ("modulePresence", "present"), ("boundState", "bound"),
                    ("recordSource", includeReviewRegressions ? "BaseTable" : "SharedQuery"),
                    ("events", Object(("on-load", "[Event Procedure]"))))),
            Record("ui-control", "control", "surface", "form-design-export", "container-only", null, null, null, "complete",
                Object(("identity", ProtectedControl), ("ordinal", 0), ("controlType", 104),
                    ("controlSource", includeReviewRegressions
                        ? ProtectedField
                        : includeHiddenIdentityRegressions ? UnusedProtectedQuery : null),
                    ("rowSource", "SELECT Password FROM Users"),
                    ("linkMasterFields", includeHiddenIdentityRegressions
                        ? $"{UnusedProtectedTable}, {OrphanProtectedField}"
                        : null),
                    ("events", Object(("on-click", includeExpressionEvent ? "=RunSelectedScenario()" : "[Event Procedure]"))))),
            Record("vba-module", "module", null, "vba-module-export", "exact-lines", vbaHash, 1, 4, "complete",
                Object(("moduleRole", "standard"), ("identity", ProtectedModule), ("moduleKind", "standard"),
                    ("sourceText", vba), ("sourceSha256", vbaHash), ("lineCount", 4), ("coordinateBasis", "module-relative"))),
            Record("macro-inventory", "macro", null, "macro-inventory-export", "unavailable", null, null, null, "partial",
                Object(("macroCategory", "named"), ("identity", ProtectedMacro), ("ordinal", 0),
                    ("startupRole", "autoexec"), ("bodyStatus", "unavailable"))),
            Record("source-gap", "gap", null, "producer-gap", "unavailable", null, null, null, "partial",
                Object(("classification", "source-unavailable"), ("affectedScope", "macro"), ("coverageCategory", "source-unavailable")))
        };
        if (includeHiddenIdentityRegressions)
        {
            records.Add(Record(
                "catalog-object", "catalog-module", null, "catalog-export", "container-only", null, null, null, "complete",
                Object(("objectRole", "module"), ("identity", ProtectedModule), ("ordinal", 0))));
            records.Add(Record(
                "catalog-object", "catalog-macro", null, "catalog-export", "container-only", null, null, null, "complete",
                Object(("objectRole", "macro"), ("identity", ProtectedMacro), ("ordinal", 0))));
            records.Add(Record(
                "catalog-object", "catalog-unused-query", null, "catalog-export", "container-only", null, null, null, "complete",
                Object(("objectRole", "saved-query"), ("identity", UnusedProtectedQuery), ("ordinal", 0))));
            records.Add(Record(
                "catalog-object", "catalog-unused-table", null, "catalog-export", "container-only", null, null, null, "complete",
                Object(("objectRole", "table"), ("identity", UnusedProtectedTable), ("ordinal", 0))));
            records.Add(Record(
                "catalog-object", "catalog-orphan-field", "catalog-unused-table", "catalog-export", "container-only",
                null, null, null, "complete",
                Object(("objectRole", "table-field"), ("identity", OrphanProtectedField), ("ordinal", 0))));
        }
        if (includeReportGroupConflict)
        {
            const string reportName = "Private_Group_Report_21922";
            const string reportDesign = """
                Begin Report
                    GroupLevel = Begin
                        0 = Begin
                            Expression ="TextGroup"
                            SortOrder =0
                            GroupOn =0
                        End
                    End
                End
                """;
            var reportDesignHash = Sha256(Encoding.UTF8.GetBytes(reportDesign));
            records.Add(Record(
                "catalog-object", "catalog-report", null, "catalog-export", "container-only", null, null, null, "complete",
                Object(("objectRole", "report"), ("identity", reportName), ("ordinal", 0))));
            records.Add(Record(
                "ui-design-document", "report-design-document", "catalog-report", "report-design-export", "exact-lines",
                reportDesignHash, 1, reportDesign.Count(character => character == '\n') + 1, "complete",
                Object(
                    ("documentRole", "report"),
                    ("designText", reportDesign),
                    ("documentSha256", reportDesignHash),
                    ("lineCount", reportDesign.Count(character => character == '\n') + 1))));
            records.Add(Record(
                "ui-surface", "report-surface", "catalog-report", "report-design-export", "container-only",
                null, null, null, "complete",
                Object(
                    ("surfaceRole", "report"),
                    ("identity", reportName),
                    ("ordinal", 0),
                    ("modulePresence", "absent"),
                    ("boundState", "unbound"))));
            records.Add(Record(
                "report-group", "report-group", "report-surface", "report-design-export", "container-only",
                null, null, null, "complete",
                Object(
                    ("ordinal", 0),
                    ("expression", "StructuredGroup"),
                    ("sortOrder", "ascending"),
                    ("groupOn", "declared"))));
        }
        if (includeReviewRegressions)
        {
            var eventVba = $"Private Sub {ProtectedControl}_Click()\nEnd Sub";
            var eventVbaHash = Sha256(Encoding.UTF8.GetBytes(eventVba));
            records.Add(Record(
                "catalog-object", "catalog-table", null, "catalog-export", "container-only", null, null, null, "complete",
                Object(("objectRole", "table"), ("identity", "BaseTable"), ("ordinal", 0))));
            records.Add(Record(
                "catalog-object", "catalog-field", "catalog-table", "catalog-export", "container-only", null, null, null, "complete",
                Object(("objectRole", "table-field"), ("identity", ProtectedField), ("ordinal", 0))));
            records.Add(Record(
                "event-reference", "control-event", "control", "form-design-export", "container-only", null, null, null, "complete",
                Object(("eventRole", "on-click"), ("value", "[Event Procedure]"), ("ordinal", 0))));
            records.Add(Record(
                "vba-module", "form-module", null, "vba-module-export", "exact-lines", eventVbaHash, 1, 2, "complete",
                Object(("moduleRole", "form"), ("identity", $"Form_{ProtectedForm}"), ("moduleKind", "form"),
                    ("sourceText", eventVba), ("sourceSha256", eventVbaHash), ("lineCount", 2), ("coordinateBasis", "module-relative"))));
            records.Add(Record(
                "macro-inventory", "surface-macro", "surface", "macro-inventory-export", "unavailable", null, null, null, "partial",
                Object(("macroCategory", "embedded"), ("identity", ProtectedMacro), ("ownerRole", "form"), ("ordinal", 1),
                    ("startupRole", "not-autoexec"), ("bodyStatus", "protected-omitted"))));
            records.Add(Record(
                "macro-inventory", "control-macro", "control", "macro-inventory-export", "unavailable", null, null, null, "partial",
                Object(("macroCategory", "embedded"), ("identity", ProtectedMacro), ("ownerRole", "control"), ("ordinal", 1),
                    ("startupRole", "not-autoexec"), ("bodyStatus", "protected-omitted"))));
        }
        if (includeExpressionEvent)
        {
            const string expressionSource = "Private Function RunSelectedScenario() As Boolean\nIf Me.IsDirty Then\nMe.txtChoice.Visible = False\nEnd If\nDoCmd.OpenForm \"TargetForm\", , , \"StatusFilterMarker\", , \"OpenArgsMarker\"\nRunSelectedScenario = True\nEnd Function";
            var expressionHash = Sha256(Encoding.UTF8.GetBytes(expressionSource));
            records.Add(Record(
                "vba-module", "expression-form-module", null, "vba-module-export", "exact-lines", expressionHash, 1, 7, "complete",
                Object(("moduleRole", "form"), ("identity", $"Form_{ProtectedForm}"), ("moduleKind", "form"),
                    ("sourceText", expressionSource), ("sourceSha256", expressionHash), ("lineCount", 7), ("coordinateBasis", "module-relative"))));
        }
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

    private static async Task<string> WriteResultAsync(string root, ScanResult result)
    {
        var output = Path.Combine(root, "review-regression-output");
        await AccessArtifactWriter.WriteAsync(output, result, AccessLimits.Default);
        return output;
    }

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
            foreach (var marker in new[]
                     {
                         ProtectedForm, ProtectedControl, ProtectedModule, ProtectedMacro, ProtectedField,
                         "Password_Reset", "Different Field 51729"
                     })
                Assert.DoesNotContain(marker, text, StringComparison.OrdinalIgnoreCase);
        }
    }
}
