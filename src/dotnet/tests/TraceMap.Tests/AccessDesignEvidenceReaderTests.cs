using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TraceMap.Access;

namespace TraceMap.Tests;

public sealed class AccessDesignEvidenceReaderTests
{
    private const string RepositoryHash = "1111111111111111111111111111111111111111111111111111111111111111";
    private const string CommitSha = "2222222222222222222222222222222222222222";
    private const string BaseManifestHash = "3333333333333333333333333333333333333333333333333333333333333333";
    private const string DatabaseIdentityHash = "4444444444444444444444444444444444444444444444444444444444444444";
    private const string DatabaseHash = "5555555555555555555555555555555555555555555555555555555555555555";
    private const string ProtectedMarker = "Customer_Form_Secret_82731";

    [Fact]
    public void Valid_bundle_is_canonical_across_line_property_id_and_timestamp_order()
    {
        using var temp = new TempDirectory();
        var firstPath = Path.Combine(temp.Path, "first");
        var secondPath = Path.Combine(temp.Path, "second");
        var designText = $"Version =20\nBegin Form\n    Caption =\"{ProtectedMarker}\"\nEnd";
        var documentHash = Sha256(designText);
        var protectedIdentity = $"{ProtectedMarker}_Café";

        var firstRecords = new[]
        {
            Record("catalog-object", "producer-form", null, "catalog-export", "container-only", null, null, null, "complete",
                Ordered(("objectRole", "form"), ("identity", protectedIdentity), ("ordinal", 0))),
            Record("ui-design-document", "producer-document", "producer-form", "form-design-export", "exact-lines", documentHash, 1, 4, "complete",
                Ordered(("documentRole", "form"), ("designText", designText), ("documentSha256", documentHash), ("lineCount", 4)))
        };
        var secondRecords = new[]
        {
            Record("ui-design-document", "changed-document-id", "changed-form-id", "form-design-export", "exact-lines", documentHash, 1, 4, "complete",
                Ordered(("lineCount", 4), ("documentSha256", documentHash), ("designText", designText), ("documentRole", "form"))),
            Record("catalog-object", "changed-form-id", null, "catalog-export", "container-only", null, null, null, "complete",
                Ordered(("ordinal", 0), ("identity", $"  {protectedIdentity.ToLowerInvariant().Normalize(NormalizationForm.FormD)}  "), ("objectRole", "form")))
        };
        WriteBundle(firstPath, firstRecords, "2026-07-29T10:00:00Z");
        WriteBundle(secondPath, secondRecords, "2026-07-29T11:00:00Z");

        using var first = AccessDesignEvidenceReader.Read(firstPath, Binding());
        using var second = AccessDesignEvidenceReader.Read(secondPath, Binding());

        Assert.True(first.AcceptedForProjection);
        Assert.True(second.AcceptedForProjection);
        Assert.Empty(first.Gaps);
        Assert.Equal(
            first.Records.Select(item => (item.Kind, item.CanonicalRecordId, item.ParentCanonicalRecordId)),
            second.Records.Select(item => (item.Kind, item.CanonicalRecordId, item.ParentCanonicalRecordId)));
        Assert.All(first.Records, record =>
        {
            Assert.DoesNotContain(ProtectedMarker, record.CanonicalRecordId, StringComparison.Ordinal);
            Assert.DoesNotContain("producer-", record.CanonicalRecordId, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Complete_v1_vocabulary_is_strictly_accepted_as_protected_in_process_input()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "bundle");
        var designText = "Version =20";
        var designHash = Sha256(designText);
        var sourceText = "Public Sub HandleClick()\nEnd Sub";
        var sourceHash = Sha256(sourceText);
        WriteBundle(path,
        [
            Record("catalog-object", "form-catalog", null, "catalog-export", "container-only", null, null, null, "complete",
                Ordered(("objectRole", "form"), ("identity", "ProtectedForm"), ("ordinal", 0))),
            Record("ui-design-document", "form-document", "form-catalog", "form-design-export", "exact-lines", designHash, 1, 1, "complete",
                Ordered(("documentRole", "form"), ("designText", designText), ("documentSha256", designHash), ("lineCount", 1))),
            Record("ui-surface", "form-surface", "form-catalog", "form-design-export", "container-only", null, null, null, "complete",
                Ordered(("surfaceRole", "form"), ("identity", "ProtectedForm"), ("ordinal", 0),
                    ("modulePresence", "present"), ("boundState", "bound"),
                    ("events", Ordered(("on-load", "[Event Procedure]"))))),
            Record("ui-control", "button-control", "form-surface", "form-design-export", "container-only", null, null, null, "complete",
                Ordered(("identity", "ProtectedButton"), ("ordinal", 0), ("controlType", 104),
                    ("events", Ordered(("on-click", "[Event Procedure]"))))),
            Record("vba-module", "module-one", null, "vba-module-export", "exact-lines", sourceHash, 1, 2, "complete",
                Ordered(("moduleRole", "standard"), ("identity", "ProtectedModule"), ("moduleKind", "standard"),
                    ("sourceText", sourceText), ("sourceSha256", sourceHash), ("lineCount", 2), ("coordinateBasis", "module-relative"))),
            Record("event-reference", "event-one", "button-control", "form-design-export", "container-only", null, null, null, "complete",
                Ordered(("eventRole", "on-click"), ("value", "[Event Procedure]"), ("ordinal", 0))),
            Record("macro-inventory", "macro-one", null, "macro-inventory-export", "unavailable", null, null, null, "partial",
                Ordered(("macroCategory", "named"), ("identity", "ProtectedMacro"), ("ordinal", 0),
                    ("startupRole", "not-autoexec"), ("bodyStatus", "protected-omitted"))),
            Record("source-gap", "gap-one", null, "producer-gap", "unavailable", null, null, null, "partial",
                Ordered(("classification", "source-unavailable"), ("affectedScope", "macro"), ("coverageCategory", "source-unavailable")))
        ]);

        using var result = AccessDesignEvidenceReader.Read(path, Binding());

        Assert.True(result.AcceptedForProjection);
        Assert.Equal(8, result.Records.Count);
        Assert.Equal(
            new[] { "catalog-object", "event-reference", "macro-inventory", "source-gap", "ui-control", "ui-design-document", "ui-surface", "vba-module" },
            result.Records.Select(record => record.Kind));
        Assert.Empty(result.Gaps);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("catalog")]
    public void Ui_controls_require_a_ui_surface_parent(string parentShape)
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, parentShape);
        var records = new List<string>();
        if (parentShape == "catalog")
        {
            records.Add(Record("catalog-object", "owner", null, "catalog-export", "container-only", null, null, null, "complete",
                Ordered(("objectRole", "form"), ("identity", "NotASurface"), ("ordinal", 0))));
        }
        records.Add(Record("ui-control", "control", parentShape == "catalog" ? "owner" : null,
            "form-design-export", "container-only", null, null, null, "complete",
            Ordered(("identity", "Button"), ("ordinal", 0), ("controlType", 104))));
        WriteBundle(path, records);

        var exception = Assert.Throws<AccessScanException>(() => AccessDesignEvidenceReader.Read(path, Binding()));

        Assert.Equal("AccessDesignInputParentUnavailable", exception.Classification);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("catalog")]
    public void Event_references_require_a_ui_surface_or_control_parent(string parentShape)
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, parentShape);
        var records = new List<string>();
        if (parentShape == "catalog")
        {
            records.Add(Record("catalog-object", "owner", null, "catalog-export", "container-only", null, null, null, "complete",
                Ordered(("objectRole", "form"), ("identity", "NotAUiOwner"), ("ordinal", 0))));
        }
        records.Add(Record("event-reference", "event", parentShape == "catalog" ? "owner" : null,
            "form-design-export", "container-only", null, null, null, "complete",
            Ordered(("eventRole", "on-click"), ("value", "[Event Procedure]"), ("ordinal", 0))));
        WriteBundle(path, records);

        var exception = Assert.Throws<AccessScanException>(() => AccessDesignEvidenceReader.Read(path, Binding()));

        Assert.Equal("AccessDesignInputParentUnavailable", exception.Classification);
    }

    [Theory]
    [InlineData("ui-surface", null)]
    [InlineData("ui-surface", "  ")]
    [InlineData("ui-control", null)]
    [InlineData("ui-control", "")]
    [InlineData("vba-module", null)]
    [InlineData("vba-module", "\t")]
    [InlineData("macro-inventory", null)]
    [InlineData("macro-inventory", " ")]
    public void Required_protected_identities_must_be_nonblank(string kind, string? identity)
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, $"{kind}-{(identity is null ? "null" : "blank")}");
        var records = new List<string>();
        if (kind == "ui-control")
        {
            records.Add(Record("ui-surface", "surface", null, "form-design-export", "container-only", null, null, null, "complete",
                Ordered(("surfaceRole", "form"), ("identity", "ValidSurface"), ("ordinal", 0))));
        }
        var source = "Public Sub Example()\nEnd Sub";
        var sourceHash = Sha256(source);
        records.Add(kind switch
        {
            "ui-surface" => Record(kind, "subject", null, "form-design-export", "container-only", null, null, null, "complete",
                Ordered(("surfaceRole", "form"), ("identity", identity), ("ordinal", 0))),
            "ui-control" => Record(kind, "subject", "surface", "form-design-export", "container-only", null, null, null, "complete",
                Ordered(("identity", identity), ("ordinal", 0), ("controlType", 104))),
            "vba-module" => Record(kind, "subject", null, "vba-module-export", "exact-lines", sourceHash, 1, 2, "complete",
                Ordered(("moduleRole", "standard"), ("identity", identity), ("moduleKind", "standard"),
                    ("sourceText", source), ("sourceSha256", sourceHash), ("lineCount", 2), ("coordinateBasis", "module-relative"))),
            "macro-inventory" => Record(kind, "subject", null, "macro-inventory-export", "unavailable", null, null, null, "partial",
                Ordered(("macroCategory", "named"), ("identity", identity), ("ordinal", 0),
                    ("startupRole", "not-autoexec"), ("bodyStatus", "protected-omitted"))),
            _ => throw new InvalidOperationException()
        });
        WriteBundle(path, records);

        var exception = Assert.Throws<AccessScanException>(() => AccessDesignEvidenceReader.Read(path, Binding()));

        Assert.Equal("AccessDesignInputRecordMalformed", exception.Classification);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(113)]
    [InlineData(999)]
    public void Ui_controls_reject_control_types_outside_the_parser_vocabulary(int controlType)
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, controlType.ToString(System.Globalization.CultureInfo.InvariantCulture));
        WriteBundle(path,
        [
            Record("ui-surface", "surface", null, "form-design-export", "container-only", null, null, null, "complete",
                Ordered(("surfaceRole", "form"), ("identity", "ValidSurface"), ("ordinal", 0))),
            Record("ui-control", "control", "surface", "form-design-export", "container-only", null, null, null, "complete",
                Ordered(("identity", "ValidControl"), ("ordinal", 0), ("controlType", controlType)))
        ]);

        var exception = Assert.Throws<AccessScanException>(() => AccessDesignEvidenceReader.Read(path, Binding()));

        Assert.Equal("AccessDesignInputRecordMalformed", exception.Classification);
    }

    [Fact]
    public void Catalog_objects_require_an_identity_and_hash_an_existing_stable_key()
    {
        using var temp = new TempDirectory();
        var missingPath = Path.Combine(temp.Path, "missing");
        WriteBundle(missingPath,
        [
            Record("catalog-object", "catalog", null, "catalog-export", "container-only", null, null, null, "complete",
                Ordered(("objectRole", "table"), ("ordinal", 0)))
        ]);

        var missing = Assert.Throws<AccessScanException>(() => AccessDesignEvidenceReader.Read(missingPath, Binding()));

        Assert.Equal("AccessDesignInputRecordMalformed", missing.Classification);

        var stableKeyPath = Path.Combine(temp.Path, "stable-key");
        const string stableKey = "access-table-protected-key";
        WriteBundle(stableKeyPath,
        [
            Record("catalog-object", "catalog", null, "catalog-export", "container-only", null, null, null, "complete",
                Ordered(("objectRole", "table"), ("stableKey", stableKey), ("ordinal", 0)))
        ]);

        using var accepted = AccessDesignEvidenceReader.Read(stableKeyPath, Binding());

        var record = Assert.Single(accepted.Records);
        Assert.DoesNotContain(stableKey, record.CanonicalRecordId, StringComparison.Ordinal);
        Assert.StartsWith("access-design-record-", record.CanonicalRecordId, StringComparison.Ordinal);
    }

    [Fact]
    public void Owner_attested_copy_is_accepted_only_with_lineage_gap()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "bundle");
        WriteBundle(path,
        [
            Record("catalog-object", "table-1", null, "catalog-export", "unavailable", null, null, null, "partial",
                Ordered(("objectRole", "table"), ("identity", "ProtectedTable"), ("ordinal", 0)))
        ], copyBinding: "owner-attested-derived-copy", sourceCopyHash: new string('a', 64));

        using var result = AccessDesignEvidenceReader.Read(path, Binding());

        Assert.True(result.AcceptedForProjection);
        Assert.Single(result.Records);
        Assert.Collection(result.Gaps,
            gap => Assert.Equal("AccessDesignInputCopyOwnerAttested", gap.Classification));
    }

    [Fact]
    public void Binding_mismatch_emits_classification_only_gap_and_no_records()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "bundle");
        WriteBundle(path,
        [
            Record("catalog-object", "table-1", null, "catalog-export", "container-only", null, null, null, "complete",
                Ordered(("objectRole", "table"), ("identity", ProtectedMarker), ("ordinal", 0)))
        ]);

        using var result = AccessDesignEvidenceReader.Read(path, Binding() with { CommitSha = new string('9', 40) });

        Assert.False(result.AcceptedForProjection);
        Assert.Empty(result.Records);
        var gap = Assert.Single(result.Gaps);
        Assert.Equal("AccessDesignInputCommitMismatch", gap.Classification);
        Assert.Null(gap.CanonicalRecordId);
    }

    [Fact]
    public void Conflicting_canonical_duplicates_poison_only_the_conflicted_identity()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "bundle");
        WriteBundle(path,
        [
            Record("ui-surface", "surface-1", null, "form-design-export", "container-only", null, null, null, "complete",
                Ordered(("surfaceRole", "form"), ("identity", "SameForm"), ("ordinal", 0), ("modulePresence", "present"), ("boundState", "bound"))),
            Record("ui-surface", "surface-2", null, "form-design-export", "container-only", null, null, null, "complete",
                Ordered(("surfaceRole", "form"), ("identity", "SameForm"), ("ordinal", 0), ("modulePresence", "absent"), ("boundState", "bound"))),
            Record("catalog-object", "table-1", null, "catalog-export", "container-only", null, null, null, "complete",
                Ordered(("objectRole", "table"), ("identity", "UnrelatedTable"), ("ordinal", 0)))
        ]);

        using var result = AccessDesignEvidenceReader.Read(path, Binding());

        Assert.True(result.AcceptedForProjection);
        Assert.Single(result.Records);
        Assert.Equal("catalog-object", result.Records[0].Kind);
        var gap = Assert.Single(result.Gaps);
        Assert.Equal("AccessDesignInputDuplicateConflict", gap.Classification);
        Assert.StartsWith("access-design-record-", gap.CanonicalRecordId, StringComparison.Ordinal);
    }

    [Fact]
    public void Equivalent_canonical_duplicates_collapse_after_canonicalization()
    {
        using var temp = new TempDirectory();
        var forwardPath = Path.Combine(temp.Path, "forward");
        var reversePath = Path.Combine(temp.Path, "reverse");
        var firstPayload = Ordered(("surfaceRole", "form"), ("identity", "SameForm"), ("ordinal", 0),
            ("modulePresence", "present"), ("boundState", "bound"));
        var secondPayload = Ordered(("surfaceRole", "form"), ("identity", " sameform "), ("ordinal", 0),
            ("modulePresence", "present"), ("boundState", "bound"));
        string[] records =
        [
            Record("ui-surface", "surface-1", null, "form-design-export", "container-only", null, null, null, "complete", firstPayload),
            Record("ui-surface", "surface-2", null, "form-design-export", "container-only", null, null, null, "complete", secondPayload)
        ];
        WriteBundle(forwardPath, records);
        WriteBundle(reversePath, records.Reverse().ToArray());

        using var forward = AccessDesignEvidenceReader.Read(forwardPath, Binding());
        using var reverse = AccessDesignEvidenceReader.Read(reversePath, Binding());

        Assert.True(forward.AcceptedForProjection);
        Assert.True(reverse.AcceptedForProjection);
        Assert.Equal(
            Assert.Single(forward.Records).Payload.GetProperty("identity").GetString(),
            Assert.Single(reverse.Records).Payload.GetProperty("identity").GetString());
        Assert.Empty(forward.Gaps);
        Assert.Empty(reverse.Gaps);
    }

    [Fact]
    public void Rejected_duplicate_does_not_poison_an_equivalent_accepted_parent()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "bundle");
        var parent = Record("ui-surface", "surface-valid", null, "form-design-export", "container-only", null, null, null, "complete",
            Ordered(("surfaceRole", "form"), ("identity", "SameForm"), ("ordinal", 0),
                ("modulePresence", "present"), ("boundState", "bound")));
        var rejectedDuplicate = MutateRecord(
            Record("ui-surface", "surface-rejected", null, "form-design-export", "container-only", null, null, null, "complete",
                Ordered(("surfaceRole", "form"), ("identity", "SameForm"), ("ordinal", 0),
                    ("modulePresence", "present"), ("boundState", "bound"))),
            root => root["source"]!.AsObject()["unexpected"] = true);
        WriteBundle(path,
        [
            parent,
            rejectedDuplicate,
            Record("ui-control", "child", "surface-valid", "form-design-export", "container-only", null, null, null, "complete",
                Ordered(("identity", "Button"), ("ordinal", 0), ("controlType", 104)))
        ]);

        using var result = AccessDesignEvidenceReader.Read(path, Binding());

        Assert.True(result.AcceptedForProjection);
        Assert.Equal(2, result.Records.Count);
        Assert.Contains(result.Records, record => record.Kind == "ui-control");
        Assert.DoesNotContain(result.Gaps, gap => gap.Classification == "AccessDesignInputParentRejected");
        Assert.Contains(result.Gaps, gap => gap.Classification == "AccessDesignInputFieldRejected");
    }

    [Fact]
    public void Macro_body_fields_are_structurally_rejected()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "bundle");
        WriteBundle(path,
        [
            Record("macro-inventory", "macro-1", null, "macro-inventory-export", "unavailable", null, null, null, "partial",
                Ordered(("macroCategory", "named"), ("identity", "AutoExec"), ("ordinal", 0),
                    ("startupRole", "autoexec"), ("bodyStatus", "protected-omitted"), ("commandBody", ProtectedMarker)))
        ]);

        using var result = AccessDesignEvidenceReader.Read(path, Binding());

        Assert.True(result.AcceptedForProjection);
        Assert.Empty(result.Records);
        var gap = Assert.Single(result.Gaps);
        Assert.Equal("AccessDesignInputFieldRejected", gap.Classification);
        Assert.DoesNotContain(ProtectedMarker, gap.Classification, StringComparison.Ordinal);
        Assert.StartsWith("access-design-record-", gap.CanonicalRecordId, StringComparison.Ordinal);
    }

    [Fact]
    public void Record_cap_rejects_the_whole_bundle_independent_of_line_order()
    {
        using var temp = new TempDirectory();
        var forwardPath = Path.Combine(temp.Path, "forward");
        var reversePath = Path.Combine(temp.Path, "reverse");
        var records = new[]
        {
            Record("catalog-object", "one", null, "catalog-export", "container-only", null, null, null, "complete",
                Ordered(("objectRole", "table"), ("identity", "One"), ("ordinal", 0))),
            Record("catalog-object", "two", null, "catalog-export", "container-only", null, null, null, "complete",
                Ordered(("objectRole", "table"), ("identity", "Two"), ("ordinal", 1)))
        };
        WriteBundle(forwardPath, records);
        WriteBundle(reversePath, records.Reverse().ToArray());
        var limits = AccessLimits.Default with { MaxDesignRecords = 1 };

        var forward = Assert.Throws<AccessScanException>(() => AccessDesignEvidenceReader.Read(forwardPath, Binding(), limits));
        var reverse = Assert.Throws<AccessScanException>(() => AccessDesignEvidenceReader.Read(reversePath, Binding(), limits));

        Assert.Equal("AccessDesignInputRecordLimitReached", forward.Classification);
        Assert.Equal(forward.Classification, reverse.Classification);
    }

    [Fact]
    public void Protected_text_hash_and_coordinate_claims_are_validated()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "bundle");
        WriteBundle(path,
        [
            Record("ui-design-document", "document-1", null, "form-design-export", "exact-lines", new string('6', 64), 1, 2, "complete",
                Ordered(("documentRole", "form"), ("designText", "line one\nline two"),
                    ("documentSha256", new string('6', 64)), ("lineCount", 2)))
        ]);

        using var result = AccessDesignEvidenceReader.Read(path, Binding());

        Assert.True(result.AcceptedForProjection);
        Assert.Empty(result.Records);
        Assert.Equal("AccessDesignInputHashMismatch", Assert.Single(result.Gaps).Classification);
    }

    [Fact]
    public void Unknown_record_fields_and_duplicate_json_properties_fail_closed()
    {
        using var temp = new TempDirectory();
        var unknownPath = Path.Combine(temp.Path, "unknown");
        WriteBundle(unknownPath,
        [
            Record("catalog-object", "one", null, "catalog-export", "container-only", null, null, null, "complete",
                Ordered(("objectRole", "table"), ("identity", "One"), ("ordinal", 0)), ("unexpected", true))
        ]);
        using (var unknown = AccessDesignEvidenceReader.Read(unknownPath, Binding()))
        {
            Assert.True(unknown.AcceptedForProjection);
            Assert.Empty(unknown.Records);
            Assert.Equal("AccessDesignInputFieldRejected", Assert.Single(unknown.Gaps).Classification);
        }

        var duplicatePath = Path.Combine(temp.Path, "duplicate");
        Directory.CreateDirectory(duplicatePath);
        var record = "{\"schema\":\"tracemap.access-design-evidence.record.v1\",\"schema\":\"tracemap.access-design-evidence.record.v1\",\"kind\":\"catalog-object\",\"recordId\":\"one\",\"parentRecordId\":null,\"source\":{\"documentRole\":\"catalog-export\",\"coordinateStatus\":\"container-only\"},\"completeness\":\"complete\",\"payload\":{\"objectRole\":\"table\"}}";
        WriteBundleFiles(duplicatePath, [record]);
        var duplicate = Assert.Throws<AccessScanException>(() => AccessDesignEvidenceReader.Read(duplicatePath, Binding()));
        Assert.Equal("AccessDesignInputDuplicateProperty", duplicate.Classification);
    }

    [Fact]
    public void Duplicate_manifest_properties_use_the_classified_disposal_path()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "duplicate-manifest");
        WriteBundle(path, []);
        var manifestPath = Path.Combine(path, AccessDesignEvidenceReader.ManifestFileName);
        var manifest = File.ReadAllText(manifestPath);
        var schemaProperty = $"\"schema\":\"{AccessDesignEvidenceReader.ManifestSchema}\",";
        File.WriteAllText(
            manifestPath,
            manifest.Replace(schemaProperty, schemaProperty + schemaProperty, StringComparison.Ordinal),
            new UTF8Encoding(false));

        var exception = Assert.Throws<AccessScanException>(() => AccessDesignEvidenceReader.Read(path, Binding()));

        Assert.Equal("AccessDesignInputDuplicateProperty", exception.Classification);
    }

    [Theory]
    [InlineData("missing", "AccessDesignInputParentUnavailable")]
    [InlineData("cycle", "AccessDesignInputParentCycle")]
    public void Parent_references_fail_closed_without_producer_ids_in_the_classification(
        string shape,
        string expectedClassification)
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "bundle");
        string[] records = shape == "missing"
            ?
            [
                Record("catalog-object", "one", "absent-producer-id", "catalog-export", "container-only", null, null, null, "complete",
                    Ordered(("objectRole", "table"), ("identity", ProtectedMarker), ("ordinal", 0)))
            ]
            :
            [
                Record("catalog-object", "one", "two", "catalog-export", "container-only", null, null, null, "complete",
                    Ordered(("objectRole", "table"), ("identity", "One"), ("ordinal", 0))),
                Record("catalog-object", "two", "one", "catalog-export", "container-only", null, null, null, "complete",
                    Ordered(("objectRole", "table"), ("identity", "Two"), ("ordinal", 1)))
            ];
        WriteBundle(path, records);

        var exception = Assert.Throws<AccessScanException>(() => AccessDesignEvidenceReader.Read(path, Binding()));

        Assert.Equal(expectedClassification, exception.Classification);
        Assert.DoesNotContain("producer", exception.Classification, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Manifest_counts_are_exact_and_unknown_kinds_are_not_accepted()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "bundle");
        WriteBundle(path,
        [
            Record("catalog-object", "one", null, "catalog-export", "container-only", null, null, null, "complete",
                Ordered(("objectRole", "table"), ("identity", "One"), ("ordinal", 0)))
        ]);
        MutateManifest(path, root =>
        {
            root["records"]!["count"] = 2;
            root["records"]!["countsByKind"]!["catalog-object"] = 2;
        });

        var exception = Assert.Throws<AccessScanException>(() => AccessDesignEvidenceReader.Read(path, Binding()));

        Assert.Equal("AccessDesignInputRecordCountMismatch", exception.Classification);
    }

    [Fact]
    public void Source_copy_hash_mismatch_is_unbound_and_returns_no_design_records()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "bundle");
        WriteBundle(path,
        [
            Record("catalog-object", "one", null, "catalog-export", "container-only", null, null, null, "complete",
                Ordered(("objectRole", "table"), ("identity", "One"), ("ordinal", 0)))
        ], sourceCopyHash: new string('a', 64));

        using var result = AccessDesignEvidenceReader.Read(path, Binding());

        Assert.False(result.AcceptedForProjection);
        Assert.Empty(result.Records);
        Assert.Equal("AccessDesignInputDatabaseUnbound", Assert.Single(result.Gaps).Classification);
    }

    [Fact]
    public void Byte_limits_apply_to_the_snapshotted_manifest_and_record_file()
    {
        using var temp = new TempDirectory();
        var manifestPath = Path.Combine(temp.Path, "manifest-limit");
        var recordPath = Path.Combine(temp.Path, "record-limit");
        WriteBundle(manifestPath,
        [
            Record("catalog-object", "one", null, "catalog-export", "container-only", null, null, null, "complete",
                Ordered(("objectRole", "table"), ("identity", "One"), ("ordinal", 0)))
        ]);
        WriteBundle(recordPath,
        [
            Record("catalog-object", "one", null, "catalog-export", "container-only", null, null, null, "complete",
                Ordered(("objectRole", "table"), ("identity", "One"), ("ordinal", 0)))
        ]);

        var manifest = Assert.Throws<AccessScanException>(() => AccessDesignEvidenceReader.Read(
            manifestPath, Binding(), AccessLimits.Default with { MaxDesignManifestBytes = 8 }));
        var records = Assert.Throws<AccessScanException>(() => AccessDesignEvidenceReader.Read(
            recordPath, Binding(), AccessLimits.Default with { MaxDesignBundleBytes = 8 }));

        Assert.Equal("AccessDesignInputManifestLimitReached", manifest.Classification);
        Assert.Equal("AccessDesignInputBundleLimitReached", records.Classification);
    }

    [Fact]
    public void Initial_file_size_probe_failures_are_classification_only()
    {
        using var temp = new TempDirectory();
        var protectedPath = Path.Combine(temp.Path, ProtectedMarker, "missing.ndjson");

        var exception = Assert.Throws<AccessScanException>(() =>
            AccessDesignEvidenceReader.ReadBoundedFile(
                protectedPath,
                AccessLimits.Default.MaxDesignBundleBytes,
                "AccessDesignInputBundleLimitReached"));

        Assert.Equal("AccessDesignInputReadFailed", exception.Classification);
        Assert.Equal(exception.Classification, exception.Message);
        Assert.DoesNotContain(ProtectedMarker, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Member_enumeration_failures_are_classification_only()
    {
        using var temp = new TempDirectory();
        var protectedDirectory = Path.Combine(temp.Path, ProtectedMarker, "missing");

        var exception = Assert.Throws<AccessScanException>(() =>
            AccessDesignEvidenceReader.ValidateMembers(
                protectedDirectory,
                Path.Combine(protectedDirectory, AccessDesignEvidenceReader.ManifestFileName),
                Path.Combine(protectedDirectory, AccessDesignEvidenceReader.RecordsFileName)));

        Assert.Equal("AccessDesignInputReadFailed", exception.Classification);
        Assert.Equal(exception.Classification, exception.Message);
        Assert.DoesNotContain(ProtectedMarker, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Malformed_utf8_is_rejected_after_exact_file_hash_validation()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "bundle");
        WriteBundle(path,
        [
            Record("catalog-object", "one", null, "catalog-export", "container-only", null, null, null, "complete",
                Ordered(("objectRole", "table"), ("identity", "One"), ("ordinal", 0)))
        ]);
        var bytes = new byte[] { 0xc3, 0x28 };
        File.WriteAllBytes(Path.Combine(path, AccessDesignEvidenceReader.RecordsFileName), bytes);
        MutateManifest(path, root => root["records"]!["sha256"] = Sha256(bytes));

        var exception = Assert.Throws<AccessScanException>(() => AccessDesignEvidenceReader.Read(path, Binding()));

        Assert.Equal("AccessDesignInputEncodingInvalid", exception.Classification);
    }

    [Fact]
    public void Exact_coordinates_must_fit_the_validated_source_document()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "bundle");
        var source = "line one\nline two";
        var hash = Sha256(source);
        WriteBundle(path,
        [
            Record("ui-design-document", "document-1", null, "form-design-export", "exact-lines", hash, 1, 3, "complete",
                Ordered(("documentRole", "form"), ("designText", source), ("documentSha256", hash), ("lineCount", 2)))
        ]);

        using var result = AccessDesignEvidenceReader.Read(path, Binding());

        Assert.True(result.AcceptedForProjection);
        Assert.Empty(result.Records);
        Assert.Equal("AccessDesignInputCoordinateUnavailable", Assert.Single(result.Gaps).Classification);
    }

    [Fact]
    public void Source_validation_errors_reject_only_the_scoped_record()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "bundle");
        var invalidSource = MutateRecord(
            Record("catalog-object", "invalid", null, "catalog-export", "container-only", null, null, null, "complete",
                Ordered(("objectRole", "table"), ("identity", "Invalid"), ("ordinal", 0))),
            root => root["source"]!.AsObject()["unexpected"] = true);
        WriteBundle(path,
        [
            invalidSource,
            Record("catalog-object", "valid", null, "catalog-export", "container-only", null, null, null, "complete",
                Ordered(("objectRole", "table"), ("identity", "Valid"), ("ordinal", 1)))
        ]);

        using var result = AccessDesignEvidenceReader.Read(path, Binding());

        Assert.True(result.AcceptedForProjection);
        Assert.Single(result.Records);
        Assert.Equal("AccessDesignInputFieldRejected", Assert.Single(result.Gaps).Classification);
    }

    [Theory]
    [InlineData(1.5)]
    [InlineData(1e30)]
    public void Scoped_rejections_cannot_leak_numeric_identity_format_exceptions(double ordinal)
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, ordinal.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
        var record = MutateRecord(
            Record("catalog-object", "invalid", null, "catalog-export", "container-only", null, null, null, "complete",
                Ordered(("objectRole", "table"), ("identity", "ProtectedTable"), ("ordinal", ordinal))),
            root => root["source"]!.AsObject()["unexpected"] = true);
        WriteBundle(path, [record]);

        var exception = Assert.Throws<AccessScanException>(() => AccessDesignEvidenceReader.Read(path, Binding()));

        Assert.Equal("AccessDesignInputRecordMalformed", exception.Classification);
        Assert.Equal(exception.Classification, exception.Message);
    }

    [Fact]
    public void Exact_child_coordinates_require_a_validated_document_and_parent_bounds()
    {
        using var temp = new TempDirectory();
        var missingDocumentPath = Path.Combine(temp.Path, "missing-document");
        WriteBundle(missingDocumentPath,
        [
            Record("ui-surface", "surface", null, "form-design-export", "exact-lines", new string('a', 64), 1, 1, "complete",
                Ordered(("surfaceRole", "form"), ("identity", "Form"), ("ordinal", 0)))
        ]);

        using (var missingDocument = AccessDesignEvidenceReader.Read(missingDocumentPath, Binding()))
        {
            Assert.Empty(missingDocument.Records);
            Assert.Equal("AccessDesignInputCoordinateUnavailable", Assert.Single(missingDocument.Gaps).Classification);
        }

        var parentBoundsPath = Path.Combine(temp.Path, "parent-bounds");
        var text = "one\ntwo\nthree";
        var hash = Sha256(text);
        WriteBundle(parentBoundsPath,
        [
            Record("ui-design-document", "document", null, "form-design-export", "exact-lines", hash, 1, 3, "complete",
                Ordered(("documentRole", "form"), ("designText", text), ("documentSha256", hash), ("lineCount", 3))),
            Record("ui-surface", "surface", "document", "form-design-export", "exact-lines", hash, 1, 2, "complete",
                Ordered(("surfaceRole", "form"), ("identity", "Form"), ("ordinal", 0))),
            Record("ui-control", "control", "surface", "form-design-export", "exact-lines", hash, 1, 3, "complete",
                Ordered(("identity", "Button"), ("ordinal", 0), ("controlType", 104)))
        ]);

        using var parentBounds = AccessDesignEvidenceReader.Read(parentBoundsPath, Binding());

        Assert.Equal(2, parentBounds.Records.Count);
        Assert.DoesNotContain(parentBounds.Records, record => record.Kind == "ui-control");
        Assert.Equal("AccessDesignInputCoordinateUnavailable", Assert.Single(parentBounds.Gaps).Classification);
    }

    [Fact]
    public void Manifest_preserves_declared_catalog_completeness()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "bundle");
        WriteBundle(path, []);

        using var result = AccessDesignEvidenceReader.Read(path, Binding());

        Assert.Equal("declared-partial", result.Manifest.CatalogCompleteness);
    }

    [Fact]
    public void Manifest_preserves_declared_coordinate_capability()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "bundle");
        WriteBundle(path, []);
        MutateManifest(path, root => root["capabilities"]!["coordinates"] = "unavailable");

        using var result = AccessDesignEvidenceReader.Read(path, Binding());

        Assert.Equal("unavailable", result.Manifest.CoordinateCapability);
    }

    [Fact]
    public void Per_record_text_limit_rejects_the_scope_and_its_children_without_rejecting_the_bundle()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "bundle");
        var source = "bounded protected text";
        var hash = Sha256(source);
        WriteBundle(path,
        [
            Record("ui-design-document", "document-1", null, "form-design-export", "exact-lines", hash, 1, 1, "complete",
                Ordered(("documentRole", "form"), ("designText", source), ("documentSha256", hash), ("lineCount", 1))),
            Record("ui-surface", "surface-1", "document-1", "form-design-export", "container-only", null, null, null, "complete",
                Ordered(("surfaceRole", "form"), ("identity", "ProtectedForm"), ("ordinal", 0)))
        ]);

        using var result = AccessDesignEvidenceReader.Read(
            path,
            Binding(),
            AccessLimits.Default with { MaxUiDesignTextLength = 4 });

        Assert.True(result.AcceptedForProjection);
        Assert.Empty(result.Records);
        Assert.Equal(
            new[] { "AccessDesignInputParentRejected", "AccessDesignInputRecordLimitReached" },
            result.Gaps.Select(gap => gap.Classification));
        Assert.All(result.Gaps, gap => Assert.StartsWith("access-design-record-", gap.CanonicalRecordId, StringComparison.Ordinal));
    }

    [Fact]
    public void Extra_members_are_rejected_instead_of_becoming_implicit_inputs()
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "bundle");
        WriteBundle(path, []);
        File.WriteAllText(Path.Combine(path, "unexpected.txt"), ProtectedMarker);

        var exception = Assert.Throws<AccessScanException>(() => AccessDesignEvidenceReader.Read(path, Binding()));

        Assert.Equal("AccessDesignInputMembersInvalid", exception.Classification);
    }

    private static AccessDesignEvidenceBinding Binding() =>
        new(RepositoryHash, CommitSha, BaseManifestHash, DatabaseIdentityHash, DatabaseHash);

    private static string Record(
        string kind,
        string recordId,
        string? parentRecordId,
        string documentRole,
        string coordinateStatus,
        string? documentHash,
        int? startLine,
        int? endLine,
        string completeness,
        IReadOnlyDictionary<string, object?> payload,
        params (string Name, object? Value)[] additional)
    {
        var source = Ordered(
            ("documentRole", documentRole),
            ("coordinateStatus", coordinateStatus));
        if (documentHash is not null) source.Add("documentSha256", documentHash);
        if (startLine is not null) source.Add("startLine", startLine);
        if (endLine is not null) source.Add("endLine", endLine);
        var envelope = Ordered(
            ("schema", AccessDesignEvidenceReader.RecordSchema),
            ("kind", kind),
            ("recordId", recordId),
            ("parentRecordId", parentRecordId),
            ("source", source),
            ("completeness", completeness),
            ("payload", payload));
        foreach (var item in additional)
            envelope.Add(item.Name, item.Value);
        return JsonSerializer.Serialize(envelope);
    }

    private static Dictionary<string, object?> Ordered(params (string Name, object? Value)[] values)
    {
        var result = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var value in values)
            result.Add(value.Name, value.Value);
        return result;
    }

    private static void WriteBundle(
        string path,
        IReadOnlyList<string> records,
        string exportedAtUtc = "2026-07-29T10:00:00Z",
        string copyBinding = "hash-identical",
        string? sourceCopyHash = null)
    {
        Directory.CreateDirectory(path);
        WriteBundleFiles(path, records, exportedAtUtc, copyBinding, sourceCopyHash);
    }

    private static void WriteBundleFiles(
        string path,
        IReadOnlyList<string> records,
        string exportedAtUtc = "2026-07-29T10:00:00Z",
        string copyBinding = "hash-identical",
        string? sourceCopyHash = null)
    {
        var recordsBytes = Encoding.UTF8.GetBytes(string.Join('\n', records) + (records.Count == 0 ? string.Empty : "\n"));
        File.WriteAllBytes(Path.Combine(path, AccessDesignEvidenceReader.RecordsFileName), recordsBytes);
        var counts = records
            .Select(line => JsonDocument.Parse(line))
            .GroupBy(document => document.RootElement.GetProperty("kind").GetString()!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var manifest = Ordered(
            ("schema", AccessDesignEvidenceReader.ManifestSchema),
            ("producer", Ordered(("id", "tracemap-synthetic-fixture"), ("version", "1.0.0"), ("mechanism", "synthetic-hand-authored"))),
            ("repository", Ordered(("identityHash", RepositoryHash), ("commitSha", CommitSha))),
            ("baseScan", Ordered(("manifestSha256", BaseManifestHash), ("databaseIdentityHash", DatabaseIdentityHash))),
            ("sourceCopy", Ordered(("sha256", sourceCopyHash ?? DatabaseHash), ("binding", copyBinding))),
            ("records", Ordered(("sha256", Sha256(recordsBytes)), ("count", records.Count), ("countsByKind", counts))),
            ("capabilities", Ordered(("coordinates", "mixed"), ("catalogCompleteness", "declared-partial"), ("identityDisclosure", "hash-only"))),
            ("exportedAtUtc", exportedAtUtc));
        File.WriteAllText(Path.Combine(path, AccessDesignEvidenceReader.ManifestFileName), JsonSerializer.Serialize(manifest), new UTF8Encoding(false));
    }

    private static void MutateManifest(string path, Action<System.Text.Json.Nodes.JsonObject> mutate)
    {
        var manifestPath = Path.Combine(path, AccessDesignEvidenceReader.ManifestFileName);
        var root = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(manifestPath))!.AsObject();
        mutate(root);
        File.WriteAllText(manifestPath, root.ToJsonString(), new UTF8Encoding(false));
    }

    private static string MutateRecord(string record, Action<System.Text.Json.Nodes.JsonObject> mutate)
    {
        var root = System.Text.Json.Nodes.JsonNode.Parse(record)!.AsObject();
        mutate(root);
        return root.ToJsonString();
    }

    private static string Sha256(string value) => Sha256(Encoding.UTF8.GetBytes(value));

    private static string Sha256(byte[] value) =>
        Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();
}
