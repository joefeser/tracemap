using System.Globalization;
using System.Reflection.Metadata;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Mono.Cecil;
using Mono.Cecil.Cil;
using CecilAssemblyDefinition = Mono.Cecil.AssemblyDefinition;
using CecilMethodDefinition = Mono.Cecil.MethodDefinition;
using CecilTypeDefinition = Mono.Cecil.TypeDefinition;
using TraceMap.Cli;
using TraceMap.Core;
using TraceMap.Storage;

namespace TraceMap.Tests;

public sealed class IlRewritePdbEvidenceExtractorTests
{
    [Fact]
    public void Operand_only_rewrite_keeps_every_sequence_point_offset_unchanged()
    {
        using var temp = new TempDirectory();
        var (beforeDll, beforePdb, afterDll, afterPdb) = PreparePair(temp, OperandOnlyMutation);

        var result = Scan(PairOptions(beforeDll, afterDll, beforePdb, afterPdb));

        var provenance = result.Manifest.IlRewritePdbProvenance!;
        Assert.Equal("rewrite-pdb-complete", provenance.CoverageState);
        var outcome = Assert.Single(provenance.Outcomes);
        Assert.Equal("admitted", outcome.Outcome);
        Assert.True(outcome.JoinedMethodCount > 0);
        Assert.True(outcome.PdbRelationshipCount > 0);
        Assert.Equal(outcome.PdbRelationshipCount, outcome.OffsetsUnchangedCount);
        Assert.Equal(0, outcome.OffsetsChangedCount);
        Assert.Empty(outcome.GapKinds);

        var relationship = Relationship(result, "StringAlpha");
        Assert.Equal("sequence-point-offsets-unchanged", relationship["sequencePointOffsets"]);
        Assert.Equal("operand-only-change", relationship["rewriteRelationshipKind"]);
        Assert.NotEqual(relationship["beforePdbMethodIdentity"], relationship["afterPdbMethodIdentity"]);
        Assert.StartsWith("pdb:format:portable|id:", relationship["beforePdbMethodIdentity"], StringComparison.Ordinal);
        Assert.NotEqual(outcome.BeforePdbContentId, outcome.AfterPdbContentId);
        Assert.False(string.IsNullOrWhiteSpace(relationship["rewriteFactId"]));
        var fact = PdbRelationshipFacts(result).Single(item => item.Properties["methodIdentity"] == relationship["methodIdentity"]);
        Assert.Equal(EvidenceTiers.Tier2Structural, fact.EvidenceTier);
        Assert.Equal("managed-il-rewrite-pdb-v1", fact.Properties["evidenceLocationKind"]);
        Assert.Equal(ScannerVersions.IlRewritePdbEvidenceExtractor, fact.Evidence.ExtractorVersion);
    }

    [Fact]
    public void Instruction_insertion_shifts_later_sequence_point_offsets()
    {
        using var temp = new TempDirectory();
        var (beforeDll, beforePdb, afterDll, afterPdb) = PreparePair(temp, InsertNopMutation);

        var result = Scan(PairOptions(beforeDll, afterDll, beforePdb, afterPdb));

        var provenance = result.Manifest.IlRewritePdbProvenance!;
        Assert.Equal("rewrite-pdb-complete", provenance.CoverageState);
        var outcome = Assert.Single(provenance.Outcomes);
        Assert.Equal(1, outcome.OffsetsChangedCount);
        Assert.True(outcome.OffsetsUnchangedCount > 0);
        Assert.Empty(outcome.GapKinds);

        var relationship = Relationship(result, "LoopTripAlpha");
        Assert.Equal("sequence-point-offsets-changed", relationship["sequencePointOffsets"]);
        Assert.Equal("instruction-stream-change", relationship["rewriteRelationshipKind"]);
        Assert.Equal(relationship["beforeSequencePointCount"], relationship["afterSequencePointCount"]);
        Assert.NotEqual(relationship["beforeSequencePointsSha256"], relationship["afterSequencePointsSha256"]);
        Assert.NotEqual(relationship["beforePdbMethodIdentity"], relationship["afterPdbMethodIdentity"]);
        // Unaffected methods in the same pair keep their exact offset vectors.
        Assert.Equal("sequence-point-offsets-unchanged", Relationship(result, "StringBeta")["sequencePointOffsets"]);
    }

    [Fact]
    public void Identical_before_and_after_pairs_classify_every_relationship_as_unchanged()
    {
        using var temp = new TempDirectory();
        var (beforeDll, beforePdb, afterDll, afterPdb) = PreparePair(temp, null);

        var result = Scan(PairOptions(beforeDll, afterDll, beforePdb, afterPdb));

        var provenance = result.Manifest.IlRewritePdbProvenance!;
        Assert.Equal("rewrite-pdb-complete", provenance.CoverageState);
        var outcome = Assert.Single(provenance.Outcomes);
        Assert.True(outcome.PdbRelationshipCount > 0);
        Assert.Equal(outcome.PdbRelationshipCount, outcome.OffsetsUnchangedCount);
        Assert.Equal(0, outcome.OffsetsChangedCount);
        Assert.Empty(outcome.GapKinds);
        Assert.Empty(PdbGapFacts(result));
    }

    [Fact]
    public void Missing_after_pdb_emits_side_scoped_gap_without_relationships()
    {
        using var temp = new TempDirectory();
        var (beforeDll, beforePdb, afterDll, _) = PreparePair(temp, OperandOnlyMutation);
        var missing = Path.Combine(temp.Path, "does-not-exist.pdb");

        var result = Scan(PairOptions(beforeDll, afterDll, beforePdb, missing));

        var provenance = result.Manifest.IlRewritePdbProvenance!;
        Assert.Equal("rewrite-pdb-partial", provenance.CoverageState);
        var outcome = Assert.Single(provenance.Outcomes);
        Assert.Equal("unavailable", outcome.Outcome);
        Assert.Equal("after", outcome.Side);
        Assert.Equal("after:IlRewritePdbSideMissing", outcome.Cause);
        Assert.Contains("IlRewritePdbSideUnavailable", outcome.GapKinds);
        Assert.Empty(PdbRelationshipFacts(result));
        var gap = Assert.Single(PdbGapFacts(result));
        Assert.Equal(EvidenceTiers.Tier4Unknown, gap.EvidenceTier);
        Assert.Equal("after", gap.Properties["side"]);
        Assert.Equal("IlRewritePdbSideMissing", gap.Properties["cause"]);
    }

    [Fact]
    public void Mismatched_after_pdb_fails_closed_without_cross_side_binding()
    {
        using var temp = new TempDirectory();
        var (beforeDll, beforePdb, afterDll, _) = PreparePair(temp, InsertNopMutation);

        // The before PDB's portable content identity matches the before
        // assembly only; declaring it for the after side must never re-bind.
        var result = Scan(PairOptions(beforeDll, afterDll, beforePdb, beforePdb));

        var provenance = result.Manifest.IlRewritePdbProvenance!;
        var outcome = Assert.Single(provenance.Outcomes);
        Assert.Equal("mismatched", outcome.Outcome);
        Assert.Equal("after", outcome.Side);
        Assert.Contains("IlRewritePdbAssemblyBindingMismatch", outcome.GapKinds);
        Assert.Empty(PdbRelationshipFacts(result));
    }

    [Fact]
    public void Malformed_pdb_side_emits_malformed_gap_without_crashing()
    {
        using var temp = new TempDirectory();
        var (beforeDll, beforePdb, afterDll, afterPdb) = PreparePair(temp, OperandOnlyMutation);
        File.WriteAllBytes(afterPdb, "BSJB"u8.ToArray());

        var result = Scan(PairOptions(beforeDll, afterDll, beforePdb, afterPdb));

        var outcome = Assert.Single(result.Manifest.IlRewritePdbProvenance!.Outcomes);
        Assert.Equal("unavailable", outcome.Outcome);
        Assert.Equal("after", outcome.Side);
        Assert.Equal("after:MalformedPortablePdb", outcome.Cause);
        Assert.Contains("IlRewritePdbSideUnavailable", outcome.GapKinds);
        Assert.Empty(PdbRelationshipFacts(result));
    }

    [Fact]
    public void Windows_native_pdb_side_is_an_unsupported_shape()
    {
        using var temp = new TempDirectory();
        var (beforeDll, beforePdb, afterDll, afterPdb) = PreparePair(temp, OperandOnlyMutation);
        File.WriteAllBytes(afterPdb, "Microsoft C/C++ MSF 7.00\r\n\u001aDS\0\0\0"u8.ToArray());

        var result = Scan(PairOptions(beforeDll, afterDll, beforePdb, afterPdb));

        var outcome = Assert.Single(result.Manifest.IlRewritePdbProvenance!.Outcomes);
        Assert.Equal("unsupported", outcome.Outcome);
        Assert.Equal("after", outcome.Side);
        Assert.Contains("IlRewritePdbUnsupportedShape", outcome.GapKinds);
        var expectedCause = OperatingSystem.IsWindows() ? "WindowsPdbIndependentReaderUnavailable" : "WindowsPdbRequiresWindows";
        Assert.Equal($"after:{expectedCause}", outcome.Cause);
        Assert.Empty(PdbRelationshipFacts(result));
    }

    [Fact]
    public void Stripped_after_debug_information_emits_bounded_one_side_gap()
    {
        using var temp = new TempDirectory();
        var (beforeDll, beforePdb, afterDll, afterPdb) = PreparePair(temp, StripStringAlphaDebugInformation);

        var result = Scan(PairOptions(beforeDll, afterDll, beforePdb, afterPdb));

        var outcome = Assert.Single(result.Manifest.IlRewritePdbProvenance!.Outcomes);
        Assert.Contains("IlRewritePdbMethodDebugInformationAbsent", outcome.GapKinds);
        Assert.Equal(1, outcome.MethodDebugInformationAbsentCount);
        Assert.True(outcome.PdbRelationshipCount > 0);
        Assert.DoesNotContain(PdbRelationshipFacts(result), fact => fact.Properties["methodIdentity"].Contains("StringAlpha", StringComparison.Ordinal));
        var delta = Assert.Single(PdbGapFacts(result, "IlRewritePdbMethodDebugInformationAbsent"));
        Assert.Equal("before", delta.Properties["side"]);
        Assert.Equal("1", delta.Properties["identityCount"]);
        Assert.Contains("StringAlpha", delta.Properties["identity[0]"], StringComparison.Ordinal);
        // The one-side-only gap is evidenced on the PDB input that carries it.
        Assert.Equal(outcome.BeforePdbSafeLocator, delta.Evidence.FilePath);
    }

    [Fact]
    public void Join_budget_exhaustion_is_atomic_after_successful_side_reads()
    {
        using var temp = new TempDirectory();
        var (beforeDll, beforePdb, afterDll, afterPdb) = PreparePair(temp, OperandOnlyMutation);

        // A large budget proves the full cost C of the pair; C - 1 then lets
        // both PDB sides complete and expires exactly inside the joined-edge
        // enumeration, which must fail closed atomically.
        var complete = Scan(PairOptions(beforeDll, afterDll, beforePdb, afterPdb));
        var completeOutcome = Assert.Single(complete.Manifest.IlRewritePdbProvenance!.Outcomes);
        Assert.Equal("admitted", completeOutcome.Outcome);
        var consumed = completeOutcome.ConsumedWorkUnits;
        Assert.True(consumed > completeOutcome.PdbRelationshipCount, "Expected side reads to consume budget before the join.");

        var exhausted = Scan(PairOptions(beforeDll, afterDll, beforePdb, afterPdb, new IlRewritePdbLimits(MaxTotalWorkUnits: consumed - 1)));
        var outcome = Assert.Single(exhausted.Manifest.IlRewritePdbProvenance!.Outcomes);
        Assert.Equal("limit-exhausted", outcome.Outcome);
        Assert.Contains("IlRewritePdbTotalWorkLimitExceeded", outcome.GapKinds);
        // The sides were bound before exhaustion: their content identities
        // survive on the outcome, but no relationship or debug delta does.
        Assert.NotNull(outcome.BeforePdbContentId);
        Assert.NotNull(outcome.AfterPdbContentId);
        Assert.Equal(0, outcome.PdbRelationshipCount);
        Assert.Equal(0, outcome.OffsetsUnchangedCount + outcome.OffsetsChangedCount + outcome.MethodDebugInformationAbsentCount);
        Assert.Empty(PdbRelationshipFacts(exhausted));
        Assert.Empty(PdbGapFacts(exhausted, "IlRewritePdbMethodDebugInformationAbsent"));
    }

    [Fact]
    public void Assembly_reread_uses_the_compiled_input_limit_not_the_pdb_limit()
    {
        using var temp = new TempDirectory();
        // Padding the after side with debug-info-free methods grows the
        // assembly far beyond its PDB, so a PDB-side file bound between the
        // two sizes would falsely reject the assembly if it were applied to
        // the assembly re-read.
        var (beforeDll, beforePdb, afterDll, afterPdb) = PreparePair(temp, PadWithBodylessMethods);
        var assemblySize = new FileInfo(afterDll).Length;
        var pdbSize = new FileInfo(afterPdb).Length;
        Assert.True(assemblySize > pdbSize + 1_024, $"Expected the padded assembly ({assemblySize} bytes) to exceed its PDB ({pdbSize} bytes) by more than 1 KiB.");
        var pdbBound = pdbSize + 1_024;
        Assert.True(pdbBound < assemblySize, "The PDB bound must stay below the paired assembly size for this regression.");

        var result = Scan(PairOptions(beforeDll, afterDll, beforePdb, afterPdb, new IlRewritePdbLimits(MaxFileSizeBytes: pdbBound)));

        var outcome = Assert.Single(result.Manifest.IlRewritePdbProvenance!.Outcomes);
        Assert.DoesNotContain("IlRewritePdbAssemblyArtifactChangedOrUnreadable", outcome.GapKinds);
        Assert.True(outcome.PdbRelationshipCount > 0);
        Assert.Contains(PdbRelationshipFacts(result), fact => fact.Properties["methodIdentity"].Contains("method:11:StringAlpha|", StringComparison.Ordinal));
    }

    [Fact]
    public void Admission_limit_and_declaration_causes_keep_specific_gap_kinds()
    {
        using var temp = new TempDirectory();
        var (beforeDll, beforePdb, afterDll, afterPdb) = PreparePair(temp, OperandOnlyMutation);

        var oversized = Scan(PairOptions(beforeDll, afterDll, beforePdb, afterPdb, new IlRewritePdbLimits(MaxFileSizeBytes: 16)));
        var oversizedOutcome = Assert.Single(oversized.Manifest.IlRewritePdbProvenance!.Outcomes);
        Assert.Equal("limit-exhausted", oversizedOutcome.Outcome);
        Assert.Contains("IlRewritePdbSideFileSizeLimitExceeded", oversizedOutcome.GapKinds);
        Assert.DoesNotContain("IlRewritePdbSideUnavailable", oversizedOutcome.GapKinds);

        var invalidDeclaration = Scan(PairOptions(beforeDll, afterDll, beforePdb, "after\0invalid"));
        var invalidOutcome = Assert.Single(invalidDeclaration.Manifest.IlRewritePdbProvenance!.Outcomes);
        Assert.Equal("invalid", invalidOutcome.Outcome);
        Assert.Contains("IlRewritePdbSideDeclarationInvalid", invalidOutcome.GapKinds);
        var invalidGap = Assert.Single(PdbGapFacts(invalidDeclaration, "IlRewritePdbSideDeclarationInvalid"));
        Assert.Equal("after", invalidGap.Properties["side"]);
    }

    [Fact]
    public void Rejected_pdb_bytes_participate_in_the_bounded_input_digest()
    {
        using var temp = new TempDirectory();
        var (beforeDll, beforePdb, afterDll, afterPdb) = PreparePair(temp, OperandOnlyMutation);
        // An in-repo locator carries no raw digest by itself, so two
        // different rejected Windows-PDB byte sequences at the same declared
        // path must still produce distinct provenance digests and scan ids.
        var probe = Path.Combine(RepoRoot(), "bin", "Debug", "net10.0", "rewrite-pdb-digest-probe.pdb");
        try
        {
            ScanResult ScanProbe(byte[] bytes)
            {
                File.WriteAllBytes(probe, bytes);
                return Scan(PairOptions(beforeDll, afterDll, beforePdb, probe));
            }

            var first = ScanProbe("Microsoft C/C++ MSF 7.00\r\n\u001aDS\0\0\0first"u8.ToArray());
            var second = ScanProbe("Microsoft C/C++ MSF 7.00\r\n\u001aDS\0\0\0second"u8.ToArray());
            foreach (var result in new[] { first, second })
            {
                var outcome = Assert.Single(result.Manifest.IlRewritePdbProvenance!.Outcomes);
                Assert.Contains("IlRewritePdbUnsupportedShape", outcome.GapKinds);
                Assert.NotNull(outcome.AfterRawFileSha256);
            }

            Assert.NotEqual(
                first.Manifest.IlRewritePdbProvenance!.BoundedInputSha256,
                second.Manifest.IlRewritePdbProvenance!.BoundedInputSha256);
            Assert.NotEqual(first.Manifest.ScanId, second.Manifest.ScanId);
        }
        finally
        {
            File.Delete(probe);
        }
    }


    [Fact]
    public void Disabled_parent_declarations_participate_in_the_bounded_input_digest()
    {
        using var temp = new TempDirectory();
        var (beforeDll, beforePdb, afterDll, afterPdb) = PreparePair(temp, OperandOnlyMutation);
        var first = Scan(PairOptions(beforeDll, afterDll, beforePdb, afterPdb) with { IlRewriteEvidence = false });
        var second = Scan(PairOptions(beforeDll, afterDll, beforePdb, Path.Combine(temp.Path, "other.pdb")) with { IlRewriteEvidence = false });
        Assert.All(new[] { first, second }, result =>
        {
            var outcome = Assert.Single(result.Manifest.IlRewritePdbProvenance!.Outcomes);
            Assert.Contains("IlRewritePdbRewritePairUnavailable", outcome.GapKinds);
            Assert.Equal("IlRewriteEvidenceDisabled", outcome.Cause);
        });
        Assert.NotEqual(
            first.Manifest.IlRewritePdbProvenance!.BoundedInputSha256,
            second.Manifest.IlRewritePdbProvenance!.BoundedInputSha256);
        Assert.NotEqual(first.Manifest.ScanId, second.Manifest.ScanId);
    }

    [Theory]
    [InlineData("IlRewriteTotalWorkLimitExceeded")]
    [InlineData("IlRewriteAssemblyIdentityMismatch")]
    public void Zero_edge_failed_parent_withholds_the_pdb_pair_instead_of_claiming_completeness(string parentGapKind)
    {
        using var temp = new TempDirectory();
        var (beforeDll, beforePdb, afterDll, afterPdb) = PreparePair(temp, OperandOnlyMutation);
        // A parent pair whose join was atomically exhausted keeps both side
        // artifacts but zero edges; the PDB lane must not relabel that
        // partial parent analysis as an admitted, complete PDB pair.
        var outcome = new IlRewritePairOutcome(
            "rewrite-pair-001",
            beforeDll,
            afterDll,
            "limit-exhausted",
            ManagedMetadataExtractor.Sha256(File.ReadAllBytes(beforeDll)),
            ManagedMetadataExtractor.Sha256(File.ReadAllBytes(afterDll)),
            "assembly",
            "assembly",
            "digest",
            [parentGapKind]);
        var pair = new EvaluatedIlRewritePair(
            outcome,
            [],
            [],
            [],
            new IlRewriteSideArtifact(beforeDll, outcome.BeforeRawFileSha256, null),
            new IlRewriteSideArtifact(afterDll, outcome.AfterRawFileSha256, null));
        var provenance = new IlRewriteProvenance(
            "il-rewrite-provenance.v1",
            "explicit-il-rewrite-evidence.v1",
            "generator",
            [],
            [],
            new IlRewriteLimits(),
            [outcome],
            "bounded",
            "local-only",
            "rewrite-partial");
        var evaluation = IlRewritePdbEvidenceExtractor.Evaluate(
            PairOptions(beforeDll, afterDll, beforePdb, afterPdb),
            new IlRewriteEvaluation(provenance, [pair], []));

        var pdbOutcome = Assert.Single(evaluation.Pairs).Outcome;
        Assert.Equal("rewrite-unavailable", pdbOutcome.Outcome);
        Assert.Contains("IlRewritePdbRewritePairUnavailable", pdbOutcome.GapKinds);
        Assert.Equal(parentGapKind, pdbOutcome.Cause);
        Assert.Equal("rewrite-pdb-partial", evaluation.Provenance!.CoverageState);
    }

    [Fact]
    public void Exhausted_pdb_work_budget_emits_limit_gap_instead_of_guessed_relationships()
    {
        using var temp = new TempDirectory();
        var (beforeDll, beforePdb, afterDll, afterPdb) = PreparePair(temp, OperandOnlyMutation);

        var result = Scan(PairOptions(beforeDll, afterDll, beforePdb, afterPdb, new IlRewritePdbLimits(MaxTotalWorkUnits: 1)));

        var outcome = Assert.Single(result.Manifest.IlRewritePdbProvenance!.Outcomes);
        Assert.Equal("limit-exhausted", outcome.Outcome);
        Assert.Contains("IlRewritePdbTotalWorkLimitExceeded", outcome.GapKinds);
        Assert.Empty(PdbRelationshipFacts(result));
    }

    [Fact]
    public void Unavailable_rewrite_pair_withholds_the_pdb_lane_with_parent_cause()
    {
        using var temp = new TempDirectory();
        var (beforeDll, beforePdb, afterDll, afterPdb) = PreparePair(temp, OperandOnlyMutation);
        CorruptMethodBody(afterDll, "StringAlpha");

        var result = Scan(PairOptions(beforeDll, afterDll, beforePdb, afterPdb));

        var outcome = Assert.Single(result.Manifest.IlRewritePdbProvenance!.Outcomes);
        Assert.Equal("rewrite-unavailable", outcome.Outcome);
        Assert.Contains("IlRewritePdbRewritePairUnavailable", outcome.GapKinds);
        var cause = outcome.Cause ?? string.Empty;
        Assert.True(
            cause.Contains("IlRewriteMalformedInput", StringComparison.Ordinal)
            || cause.Contains("IlRewriteReaderDisagreement", StringComparison.Ordinal),
            $"Expected the parent failure cause in the PDB outcome, found: {cause}");
        Assert.Empty(PdbRelationshipFacts(result));
    }

    [Fact]
    public void Flag_without_the_rewrite_lane_emits_parent_unavailable_gap()
    {
        using var temp = new TempDirectory();
        var (beforeDll, beforePdb, afterDll, afterPdb) = PreparePair(temp, OperandOnlyMutation);
        var options = PairOptions(beforeDll, afterDll, beforePdb, afterPdb) with { IlRewriteEvidence = false };

        var result = Scan(options);

        var outcome = Assert.Single(result.Manifest.IlRewritePdbProvenance!.Outcomes);
        Assert.Equal("rewrite-unavailable", outcome.Outcome);
        Assert.Contains("IlRewritePdbRewritePairUnavailable", outcome.GapKinds);
        Assert.Equal("IlRewriteEvidenceDisabled", outcome.Cause);
    }

    [Fact]
    public void Requested_pdb_lane_without_declarations_emits_pair_unavailable()
    {
        using var temp = new TempDirectory();
        var (beforeDll, _, afterDll, _) = PreparePair(temp, OperandOnlyMutation);
        var options = PairOptions(beforeDll, afterDll) with
        {
            IlRewritePdbEvidence = true,
            IlRewriteBeforePdbPaths = [],
            IlRewriteAfterPdbPaths = []
        };

        var result = Scan(options);

        var outcome = Assert.Single(result.Manifest.IlRewritePdbProvenance!.Outcomes);
        Assert.Equal("missing", outcome.Outcome);
        Assert.Contains("IlRewritePdbPairUnavailable", outcome.GapKinds);
    }

    [Fact]
    public void Declaration_count_mismatch_and_blank_slots_invalidate_the_declaration()
    {
        using var temp = new TempDirectory();
        var (beforeDll, beforePdb, afterDll, afterPdb) = PreparePair(temp, OperandOnlyMutation);

        var mismatched = Scan(PairOptions(beforeDll, afterDll, beforePdb, afterPdb) with { IlRewriteAfterPdbPaths = [] });
        var mismatchedOutcome = Assert.Single(mismatched.Manifest.IlRewritePdbProvenance!.Outcomes);
        Assert.Equal("invalid", mismatchedOutcome.Outcome);
        Assert.Contains("IlRewritePdbDeclarationInvalid", mismatchedOutcome.GapKinds);
        Assert.Empty(PdbRelationshipFacts(mismatched));

        var blank = Scan(PairOptions(beforeDll, afterDll, beforePdb, afterPdb) with { IlRewriteBeforePdbPaths = [" "] });
        var blankOutcome = Assert.Single(blank.Manifest.IlRewritePdbProvenance!.Outcomes);
        Assert.Contains("IlRewritePdbDeclarationInvalid", blankOutcome.GapKinds);
        Assert.Empty(PdbRelationshipFacts(blank));
    }

    [Fact]
    public void Different_declared_pdb_sets_never_share_a_provenance_digest()
    {
        using var temp = new TempDirectory();
        var (beforeDll, beforePdb, afterDll, afterPdb) = PreparePair(temp, OperandOnlyMutation);

        var first = Scan(PairOptions(beforeDll, afterDll, beforePdb, afterPdb));
        var second = Scan(PairOptions(beforeDll, afterDll, beforePdb, Path.Combine(temp.Path, "other.pdb")));

        Assert.NotEqual(
            first.Manifest.IlRewritePdbProvenance!.BoundedInputSha256,
            second.Manifest.IlRewritePdbProvenance!.BoundedInputSha256);
        Assert.NotEqual(first.Manifest.ScanId, second.Manifest.ScanId);
    }

    [Fact]
    public void Pdb_declarations_without_the_flag_leave_existing_evidence_unchanged()
    {
        using var temp = new TempDirectory();
        var (beforeDll, beforePdb, afterDll, afterPdb) = PreparePair(temp, OperandOnlyMutation);

        var baseline = Scan(PairOptions(beforeDll, afterDll));
        var declared = Scan(PairOptions(beforeDll, afterDll) with
        {
            IlRewriteBeforePdbPaths = [beforePdb],
            IlRewriteAfterPdbPaths = [afterPdb]
        });

        Assert.Null(declared.Manifest.IlRewritePdbProvenance);
        Assert.DoesNotContain(declared.Facts, fact => fact.RuleId is RuleIds.DotNetIlRewritePdb or RuleIds.DotNetIlRewritePdbGap);
        Assert.Equal(baseline.Manifest.ScanId, declared.Manifest.ScanId);
        Assert.Equal(baseline.Manifest.IlRewriteProvenance!.BoundedInputSha256, declared.Manifest.IlRewriteProvenance!.BoundedInputSha256);
        Assert.Equal(
            JsonSerializer.Serialize(baseline.Facts.Select(fact => JsonSerializer.Serialize(fact, JsonOptions.Stable)).ToArray()),
            JsonSerializer.Serialize(declared.Facts.Select(fact => JsonSerializer.Serialize(fact, JsonOptions.Stable)).ToArray()));
    }

    [Fact]
    public void Repeat_scans_are_deterministic_and_privacy_projected()
    {
        using var temp = new TempDirectory();
        var (beforeDll, beforePdb, afterDll, afterPdb) = PreparePair(temp, InsertNopMutation);
        var options = PairOptions(beforeDll, afterDll, beforePdb, afterPdb);

        var first = Scan(options);
        var second = Scan(options with { OutputPath = TempOutput() });

        var serialize = (ScanResult result) => JsonSerializer.Serialize(result.Facts, JsonOptions.Stable);
        Assert.Equal(serialize(first), serialize(second));
        Assert.Equal(first.Manifest.IlRewritePdbProvenance!.BoundedInputSha256, second.Manifest.IlRewritePdbProvenance!.BoundedInputSha256);
        Assert.Matches("^[0-9a-f]{64}$", first.Manifest.IlRewritePdbProvenance!.BoundedInputSha256);
        Assert.Matches("^[0-9a-f]{64}$", first.Manifest.IlRewritePdbProvenance!.GeneratorSha256);
        Assert.Equal("local-only", first.Manifest.IlRewritePdbProvenance!.ArtifactVisibility);
        var serialized = serialize(first) + JsonSerializer.Serialize(first.Manifest.IlRewritePdbProvenance, JsonOptions.Stable);
        Assert.DoesNotContain(temp.Path, serialized, StringComparison.Ordinal);
        Assert.DoesNotContain(beforePdb, serialized, StringComparison.Ordinal);
        // String-literal operands and source paths never appear verbatim.
        Assert.DoesNotContain("il-alpha", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("il-omega", serialized, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cli_scan_persists_pdb_relationships_through_every_artifact()
    {
        using var temp = new TempDirectory();
        var (beforeDll, beforePdb, afterDll, afterPdb) = PreparePair(temp, InsertNopMutation);
        var output = Path.Combine(Directory.CreateTempSubdirectory("tracemap-ilrewritepdb-artifacts-").FullName, "out");
        var cliError = new StringWriter();
        var cliExit = await TraceMapCommand.RunAsync(
        [
            "scan",
            "--repo", RepoRoot(),
            "--out", output,
            "--il-rewrite-evidence",
            "--il-rewrite-before", beforeDll,
            "--il-rewrite-after", afterDll,
            "--il-rewrite-pdb-evidence",
            "--il-rewrite-pdb-before", beforePdb,
            "--il-rewrite-pdb-after", afterPdb
        ], TextWriter.Null, cliError);
        Assert.True(cliExit == 0, $"CLI scan failed with exit {cliExit}: {cliError}");

        var manifestText = File.ReadAllText(Path.Combine(output, "scan-manifest.json"));
        Assert.Contains("ilRewritePdbProvenance", manifestText, StringComparison.Ordinal);
        Assert.Contains("il-rewrite-pdb-provenance.v1", manifestText, StringComparison.Ordinal);
        var ndjson = File.ReadAllText(Path.Combine(output, "facts.ndjson"));
        Assert.Contains("dotnet.compiled.il-rewrite-pdb.v1", ndjson, StringComparison.Ordinal);
        Assert.Contains("ManagedIlRewritePdbObserved", ndjson, StringComparison.Ordinal);
        Assert.Contains("sequence-point-offsets-changed", ndjson, StringComparison.Ordinal);
        using var connection = new SqliteConnection($"Data Source={Path.Combine(output, "index.sqlite")}");
        connection.Open();
        var command = connection.CreateCommand();
        command.CommandText = "select count(*) from facts where rule_id like 'dotnet.compiled.il-rewrite-pdb%'";
        var rows = Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
        Assert.True(rows > 0);
        Assert.Contains("## Compiled .NET IL Rewrite PDB Evidence", File.ReadAllText(Path.Combine(output, "report.md")), StringComparison.Ordinal);
        var receiptPath = Path.Combine(output, "scan-receipt.json");
        if (File.Exists(receiptPath))
            Assert.Contains("ilRewritePdbProvenance", File.ReadAllText(receiptPath), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cli_rejects_invalid_pdb_declarations()
    {
        using var temp = new TempDirectory();
        var (beforeDll, beforePdb, afterDll, afterPdb) = PreparePair(temp, OperandOnlyMutation);
        var unflagged = await TraceMapCommand.RunAsync(
        [
            "scan", "--repo", RepoRoot(), "--out", Path.Combine(temp.Path, "out1"),
            "--il-rewrite-evidence", "--il-rewrite-before", beforeDll, "--il-rewrite-after", afterDll,
            "--il-rewrite-pdb-before", beforePdb
        ], TextWriter.Null, new StringWriter());
        Assert.Equal(1, unflagged);

        var withoutParent = await TraceMapCommand.RunAsync(
        [
            "scan", "--repo", RepoRoot(), "--out", Path.Combine(temp.Path, "out2"),
            "--il-rewrite-pdb-evidence"
        ], TextWriter.Null, new StringWriter());
        Assert.Equal(1, withoutParent);

        var mismatched = await TraceMapCommand.RunAsync(
        [
            "scan", "--repo", RepoRoot(), "--out", Path.Combine(temp.Path, "out3"),
            "--il-rewrite-evidence", "--il-rewrite-before", beforeDll, "--il-rewrite-after", afterDll,
            "--il-rewrite-pdb-evidence", "--il-rewrite-pdb-before", beforePdb
        ], TextWriter.Null, new StringWriter());
        Assert.Equal(1, mismatched);

        var misaligned = await TraceMapCommand.RunAsync(
        [
            "scan", "--repo", RepoRoot(), "--out", Path.Combine(temp.Path, "out4"),
            "--il-rewrite-evidence", "--il-rewrite-before", beforeDll, "--il-rewrite-after", afterDll,
            "--il-rewrite-before", beforeDll, "--il-rewrite-after", afterDll,
            "--il-rewrite-pdb-evidence", "--il-rewrite-pdb-before", beforePdb, "--il-rewrite-pdb-after", afterPdb
        ], TextWriter.Null, new StringWriter());
        Assert.Equal(1, misaligned);
    }

    [Fact]
    public void Sequence_point_digest_is_deterministic_and_offset_sensitive()
    {
        var first = MethodWithPoints([Point(0, 0, 1, false, 3, 1, 3, 10), Point(1, 11, 1, true, 4, 1, 4, 2)]);
        var same = MethodWithPoints([Point(0, 0, 1, false, 3, 1, 3, 10), Point(1, 11, 1, true, 4, 1, 4, 2)]);
        var shifted = MethodWithPoints([Point(0, 0, 1, false, 3, 1, 3, 10), Point(1, 12, 1, true, 4, 1, 4, 2)]);
        Assert.Equal(IlRewritePdbEvidenceExtractor.SequencePointsSha256(first), IlRewritePdbEvidenceExtractor.SequencePointsSha256(same));
        Assert.NotEqual(IlRewritePdbEvidenceExtractor.SequencePointsSha256(first), IlRewritePdbEvidenceExtractor.SequencePointsSha256(shifted));
    }

    [Fact]
    public void Gap_outcome_labels_are_exact()
    {
        Assert.Equal("ambiguous", IlRewritePdbEvidenceExtractor.GapOutcome("IlRewritePdbAssemblyBindingAmbiguous"));
        Assert.Equal("inconsistent", IlRewritePdbEvidenceExtractor.GapOutcome("IlRewritePdbMethodRowInconsistent"));
        Assert.Equal("method-debug-delta", IlRewritePdbEvidenceExtractor.GapOutcome("IlRewritePdbMethodDebugInformationAbsent"));
        Assert.Equal("rewrite-unavailable", IlRewritePdbEvidenceExtractor.GapOutcome("IlRewritePdbRewritePairUnavailable"));
    }

    [Fact]
    public void Invalid_limits_are_rejected()
    {
        // Limits are validated before any file is read, so dummy paths suffice.
        const string beforeDll = "before.dll";
        const string afterDll = "after.dll";
        const string beforePdb = "before.pdb";
        const string afterPdb = "after.pdb";
        Assert.Throws<ArgumentException>(() => Scan(PairOptions(beforeDll, afterDll, beforePdb, afterPdb, new IlRewritePdbLimits(MaxTextLength: 0))));
        Assert.Throws<ArgumentException>(() => Scan(PairOptions(beforeDll, afterDll, beforePdb, afterPdb, new IlRewritePdbLimits(MaxTotalWorkUnits: 0))));
        Assert.Throws<ArgumentException>(() => Scan(PairOptions(beforeDll, afterDll, beforePdb, afterPdb, new IlRewritePdbLimits(MaxTextLength: PortablePdbExtractor.MinimumProjectedTextLength - 1))));
    }

    [Fact]
    public void Rule_catalog_documents_the_rewrite_pdb_rules_with_limitations()
    {
        var catalog = File.ReadAllText(Path.Combine(FindRepoRoot(), "rules", "rule-catalog.yml"));
        Assert.Contains("id: dotnet.compiled.il-rewrite-pdb.v1", catalog, StringComparison.Ordinal);
        Assert.Contains("id: dotnet.compiled.il-rewrite-pdb-gap.v1", catalog, StringComparison.Ordinal);
        Assert.Contains("compares IL offset vectors only", catalog, StringComparison.Ordinal);
        Assert.Contains("It does not prove behavioral equivalence, source ownership, debugging-behavior preservation", catalog, StringComparison.Ordinal);
        Assert.Contains("duplicate matching entries, cross-side matches, changed or unreadable matched assemblies", catalog, StringComparison.Ordinal);
        Assert.Contains("ILAsm/ILDAsm parity", catalog, StringComparison.Ordinal);
    }

    [Fact]
    public void Fixture_catalog_records_stable_rewrite_pdb_cases_and_deferred_prerequisites()
    {
        var catalog = JsonSerializer.Deserialize<JsonElement>(
            File.ReadAllText(Path.Combine(FindRepoRoot(), "samples", "compiled-dotnet-evidence", "fixture-cases.json")));
        Assert.Equal("compiled-dotnet-fixture-cases.v8", catalog.GetProperty("schemaVersion").GetString());
        var cases = catalog.GetProperty("ilRewritePdbCases").EnumerateArray().ToArray();
        Assert.True(cases.Length >= 11);
        var ids = cases.Select(item => item.GetProperty("id").GetString()!).ToArray();
        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.All(ids, id => Assert.False(string.IsNullOrWhiteSpace(id)));
        foreach (var required in new[]
                 {
                     "CS-ILRWPDB-OFFSET-STABLE-001", "CS-ILRWPDB-OFFSET-SHIFT-002", "CS-ILRWPDB-AFTER-MISSING-004",
                     "CS-ILRWPDB-BINDING-MISMATCH-005", "CS-ILRWPDB-READER-DISAGREE-010", "CS-ILRWPDB-BUDGET-011"
                 })
            Assert.Contains(required, ids);
        var implemented = cases.Where(item => !item.TryGetProperty("status", out var status) || status.GetString() == "implemented").ToArray();
        Assert.All(implemented, item =>
        {
            Assert.True(item.GetProperty("expectedRuleIds").GetArrayLength() > 0);
            Assert.True(item.GetProperty("nonClaims").GetArrayLength() > 0);
            Assert.Contains(item.GetProperty("expectedTier").GetString()!, new[] { "Tier2Structural", "Tier4Unknown" });
        });
        var deferred = cases.Where(item => item.TryGetProperty("status", out var status) && status.GetString() == "deferred").ToArray();
        Assert.Equal(2, deferred.Length);
        Assert.All(deferred, item =>
        {
            Assert.True(item.GetProperty("expectedRuleIds").GetArrayLength() == 0);
            Assert.False(string.IsNullOrWhiteSpace(item.GetProperty("prerequisites").GetString()));
        });
        Assert.Contains(ids, id => id == "ILRWPDB-ILASM-PARITY-012");
        var ilasm = cases.Single(item => item.GetProperty("id").GetString() == "ILRWPDB-ILASM-PARITY-012");
        Assert.Contains("ILAsm", ilasm.GetProperty("prerequisites").GetString(), StringComparison.Ordinal);
    }



    private static string FindRepoRoot()
    {
        var current = AppContext.BaseDirectory;
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current, ".git")) || File.Exists(Path.Combine(current, ".git")))
                return current;
            current = Directory.GetParent(current)?.FullName;
        }
        throw new InvalidOperationException("Repository root not found.");
    }

    private static string RepoRoot() => Path.Combine(FindRepoRoot(), "samples", "compiled-dotnet-evidence", "csharp");

    private static (string Assembly, string Pdb) Fixture()
    {
        var assembly = Path.Combine(RepoRoot(), "bin", "Debug", "net10.0", "CompiledEvidence.CSharp.dll");
        return (assembly, Path.ChangeExtension(assembly, ".pdb"));
    }

    private static string TempOutput() => Path.Combine(Directory.CreateTempSubdirectory("tracemap-ilrewritepdb-output-").FullName, "out");

    private static ScanResult Scan(ScanOptions options) => ScanEngine.Scan(options);

    private static ScanOptions PairOptions(
        string beforeDll,
        string afterDll,
        string? beforePdb = null,
        string? afterPdb = null,
        IlRewritePdbLimits? limits = null) => new(
        RepoRoot(),
        TempOutput(),
        IlRewriteEvidence: true,
        IlRewriteBeforePaths: [beforeDll],
        IlRewriteAfterPaths: [afterDll],
        IlRewritePdbEvidence: beforePdb is not null,
        IlRewriteBeforePdbPaths: beforePdb is null ? [] : [beforePdb],
        IlRewriteAfterPdbPaths: afterPdb is null || beforePdb is null ? [] : [afterPdb],
        IlRewritePdbLimits: limits);

    /// <summary>
    /// Copies the deterministic compiler fixture pair to the temp directory as
    /// the before side and writes a Cecil-mutated after assembly plus its own
    /// portable PDB. The writer coordinates the after PDB content id with the
    /// after assembly's CodeView entry, so the pair is bound by construction.
    /// </summary>
    private static (string BeforeDll, string BeforePdb, string AfterDll, string AfterPdb) PreparePair(
        TempDirectory temp,
        Action<CecilAssemblyDefinition>? mutation)
    {
        var (fixtureDll, fixturePdb) = Fixture();
        var beforeDll = Path.Combine(temp.Path, "before.dll");
        var beforePdb = Path.Combine(temp.Path, "before.pdb");
        File.Copy(fixtureDll, beforeDll, true);
        File.Copy(fixturePdb, beforePdb, true);
        var afterDll = Path.Combine(temp.Path, "after.dll");
        WriteRewriteVariant(fixtureDll, afterDll, mutation);
        return (beforeDll, beforePdb, afterDll, Path.ChangeExtension(afterDll, ".pdb"));
    }

    private static void WriteRewriteVariant(string sourceDll, string targetDll, Action<CecilAssemblyDefinition>? mutation)
    {
        using var assembly = CecilAssemblyDefinition.ReadAssembly(sourceDll, new ReaderParameters
        {
            ReadSymbols = true,
            SymbolReaderProvider = new PortablePdbReaderProvider(),
            AssemblyResolver = new RejectingAssemblyResolver()
        });
        mutation?.Invoke(assembly);
        assembly.Write(targetDll, new WriterParameters
        {
            WriteSymbols = true,
            SymbolWriterProvider = new PortablePdbWriterProvider()
        });
    }

    private static void OperandOnlyMutation(CecilAssemblyDefinition assembly)
    {
        var ldstr = Method(assembly, "StringAlpha").Body.Instructions.Single(instruction => instruction.OpCode.Code == Code.Ldstr);
        ldstr.Operand = "il-omega";
    }

    private static void InsertNopMutation(CecilAssemblyDefinition assembly)
    {
        var method = Method(assembly, "LoopTripAlpha");
        var target = method.Body.Instructions.First(instruction => instruction.OpCode.Code == Code.Ldarg_0);
        var worker = method.Body.GetILProcessor();
        worker.InsertBefore(target, worker.Create(OpCodes.Nop));
    }

    private static void StripStringAlphaDebugInformation(CecilAssemblyDefinition assembly)
    {
        Method(assembly, "StringAlpha").DebugInformation.SequencePoints.Clear();
    }

    /// <summary>
    /// Appends debug-info-free filler methods so the after assembly grows far
    /// beyond its own PDB without touching any joined method identity.
    /// </summary>
    private static void PadWithBodylessMethods(CecilAssemblyDefinition assembly)
    {
        var module = assembly.MainModule;
        var type = (CecilTypeDefinition)Method(assembly, "StringAlpha").DeclaringType!;
        for (var index = 0; index < 300; index++)
        {
            var method = new CecilMethodDefinition(
                $"Pad{index.ToString("D3", CultureInfo.InvariantCulture)}",
                Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.Static,
                module.TypeSystem.Void);
            var worker = method.Body.GetILProcessor();
            worker.Append(worker.Create(OpCodes.Ret));
            type.Methods.Add(method);
        }
    }

    private static CecilMethodDefinition Method(CecilAssemblyDefinition assembly, string name)
    {
        CecilMethodDefinition? found = null;
        foreach (var type in Flatten(assembly.MainModule.Types))
        foreach (var method in type.Methods)
        {
            if (method.Name == name)
                found = method;
        }

        return found ?? throw new InvalidOperationException($"Method {name} not found.");
    }

    private static IEnumerable<CecilTypeDefinition> Flatten(IEnumerable<CecilTypeDefinition> types)
    {
        foreach (var type in types)
        {
            yield return type;
            foreach (var nested in Flatten(type.NestedTypes))
                yield return nested;
        }
    }

    private static void CorruptMethodBody(string path, string methodName)
    {
        var bytes = File.ReadAllBytes(path);
        using (var pe = new System.Reflection.PortableExecutable.PEReader(new MemoryStream(bytes, writable: false)))
        {
            var reader = pe.GetMetadataReader();
            for (var row = 1; row <= reader.MethodDefinitions.Count; row++)
            {
                var handle = System.Reflection.Metadata.Ecma335.MetadataTokens.MethodDefinitionHandle(row);
                var definition = reader.GetMethodDefinition(handle);
                var name = reader.GetString(definition.Name);
                if (name != methodName)
                    continue;
                var rva = definition.RelativeVirtualAddress;
                var offset = FileOffset(pe, (int)rva);
                var headerByte = bytes[offset];
                var ilOffset = (headerByte & 0x3) == 2 ? offset + 1 : offset + 12;
                bytes[ilOffset] = 0xee; // Unassigned opcode; both readers must reject it.
                break;
            }
        }

        File.WriteAllBytes(path, bytes);
    }

    private static int FileOffset(System.Reflection.PortableExecutable.PEReader pe, int rva)
    {
        foreach (var section in pe.PEHeaders.SectionHeaders!)
        {
            if (rva >= section.VirtualAddress && rva < section.VirtualAddress + Math.Max(section.SizeOfRawData, section.VirtualSize))
                return (int)(section.PointerToRawData + (uint)(rva - section.VirtualAddress));
        }

        throw new InvalidOperationException("RVA not mapped.");
    }

    private static IReadOnlyList<CodeFact> PdbRelationshipFacts(ScanResult result) =>
        result.Facts.Where(fact => fact.FactType == FactTypes.ManagedIlRewritePdbObserved).ToArray();

    private static IReadOnlyList<CodeFact> PdbGapFacts(ScanResult result, string? gapKind = null) =>
        result.Facts
            .Where(fact => fact.RuleId == RuleIds.DotNetIlRewritePdbGap && fact.FactType == FactTypes.AnalysisGap)
            .Where(fact => gapKind is null || fact.Properties.GetValueOrDefault("gapKind") == gapKind)
            .ToArray();

    private static IReadOnlyDictionary<string, string> Relationship(ScanResult result, string methodName) =>
        PdbRelationshipFacts(result)
            .Single(fact => fact.Properties["methodIdentity"].Contains($"method:{methodName.Length}:{methodName}|", StringComparison.Ordinal))
            .Properties;

    private static PdbMethodObservation MethodWithPoints(IReadOnlyList<PdbSequencePointObservation> points) =>
        new(18, "0x06000012", "pdb:format:portable|id:test|method:18", points);

    private static PdbSequencePointObservation Point(int ordinal, int offset, int documentRow, bool hidden, int startLine, int startColumn, int endLine, int endColumn) =>
        new(ordinal, offset, documentRow, hidden, startLine, startColumn, endLine, endColumn, $"test|sequence:{ordinal}|offset:{offset}");

    private sealed class RejectingAssemblyResolver : IAssemblyResolver
    {
        public CecilAssemblyDefinition Resolve(AssemblyNameReference name) => throw new AssemblyResolutionException(name);
        public CecilAssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters) => throw new AssemblyResolutionException(name);
        public void Dispose()
        {
        }
    }
}
