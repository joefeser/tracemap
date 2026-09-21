using System.Globalization;
using System.Reflection.Metadata;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Mono.Cecil;
using Mono.Cecil.Cil;
using CecilAssemblyDefinition = Mono.Cecil.AssemblyDefinition;
using CecilMethodDefinition = Mono.Cecil.MethodDefinition;
using CecilModuleDefinition = Mono.Cecil.ModuleDefinition;
using CecilTypeDefinition = Mono.Cecil.TypeDefinition;
using TraceMap.Cli;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class IlRewriteEvidenceExtractorTests
{
    [Fact]
    public void Changed_constant_operand_with_same_opcodes_emits_operand_only_change_edge()
    {
        using var temp = new TempDirectory();
        var before = Path.Combine(temp.Path, "RewriteShapes.dll");
        var after = Path.Combine(temp.Path, "RewriteShapes.after.dll");
        WriteBeforeAssembly(before);
        MutateConstantOperand(before, after);

        var result = Scan(PairOptions(before, after));

        Assert.Equal("rewrite-complete", result.Manifest.IlRewriteProvenance!.CoverageState);
        var edge = EdgeFact(result, "Constant");
        Assert.Equal(EvidenceTiers.Tier2Structural, edge.EvidenceTier);
        Assert.Equal(RuleIds.DotNetIlRewrite, edge.RuleId);
        Assert.Equal("operand-only-change", edge.Properties["relationshipKind"]);
        Assert.Equal("true", edge.Properties["opcodeSequencePreserved"]);
        Assert.Equal("false", edge.Properties["tokenRetargeted"]);
        Assert.Equal("0", edge.Properties["callRetargetCount"]);
        Assert.NotEqual(edge.Properties["beforeIlBodySha256"], edge.Properties["afterIlBodySha256"]);
        Assert.Contains("|il-rewrite:before:sha256:", edge.TargetSymbol, StringComparison.Ordinal);
        Assert.Contains(":after:sha256:", edge.TargetSymbol, StringComparison.Ordinal);
        Assert.DoesNotContain(result.Facts, fact => fact.RuleId == RuleIds.DotNetIlRewriteGap);
    }

    [Fact]
    public void Inserted_member_retargets_tokens_while_preserving_target_identity()
    {
        using var temp = new TempDirectory();
        var before = Path.Combine(temp.Path, "RewriteShapes.dll");
        var after = Path.Combine(temp.Path, "RewriteShapes.after.dll");
        WriteBeforeAssembly(before);
        MutateInsertMethodFirst(before, after);

        var result = Scan(PairOptions(before, after));

        var provenance = result.Manifest.IlRewriteProvenance!;
        Assert.Equal("rewrite-partial", provenance.CoverageState);
        var caller = EdgeFact(result, "Caller");
        // The call's raw module-local token operand shifted, so the body digest
        // changed even though the target member identity is byte-identical.
        Assert.Equal("operand-only-change", caller.Properties["relationshipKind"]);
        Assert.Equal("true", caller.Properties["tokenRetargeted"]);
        var retarget = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.ManagedIlCallRetargetObserved);
        Assert.Equal(RuleIds.DotNetIlRewrite, retarget.RuleId);
        Assert.Equal(EvidenceTiers.Tier2Structural, retarget.EvidenceTier);
        Assert.Equal("call", retarget.Properties["opcode"]);
        Assert.Equal("0", retarget.Properties["callOrdinal"]);
        Assert.StartsWith("0x06", caller.Properties["beforeMetadataToken"], StringComparison.Ordinal);
        Assert.StartsWith("0x06", caller.Properties["afterMetadataToken"], StringComparison.Ordinal);
        Assert.NotEqual(caller.Properties["beforeMetadataToken"], caller.Properties["afterMetadataToken"]);
        Assert.NotEqual(retarget.Properties["beforeToken"], retarget.Properties["afterToken"]);
        Assert.Equal(retarget.Properties["beforeTargetIdentity"], retarget.Properties["afterTargetIdentity"]);
        Assert.Contains("method:6:Target|", retarget.Properties["beforeTargetIdentity"], StringComparison.Ordinal);
        var membership = Assert.Single(result.Facts, fact => fact.RuleId == RuleIds.DotNetIlRewriteGap
            && fact.Properties.GetValueOrDefault("gapKind") == "IlRewriteMethodAfterOnly");
        Assert.Equal("after", membership.Properties["side"]);
        // The after-only identity is evidenced on the after input.
        Assert.Equal(result.Manifest.IlRewriteProvenance!.Outcomes[0].AfterSafeLocator, membership.Evidence.FilePath);
        Assert.Equal("1", membership.Properties["identityCount"]);
        Assert.Contains("method:8:Inserted|", membership.Properties["identity[0]"], StringComparison.Ordinal);
        // Untouched memberless-of-calls body stays provably identical.
        Assert.Equal("unchanged", EdgeFact(result, "Stable").Properties["relationshipKind"]);
    }

    [Fact]
    public void Rewired_call_target_records_identity_retarget_without_membership_change()
    {
        using var temp = new TempDirectory();
        var before = Path.Combine(temp.Path, "RewriteShapes.dll");
        var after = Path.Combine(temp.Path, "RewriteShapes.after.dll");
        WriteBeforeAssembly(before);
        MutateCallTarget(before, after);

        var result = Scan(PairOptions(before, after));

        Assert.Equal("rewrite-complete", result.Manifest.IlRewriteProvenance!.CoverageState);
        var caller = EdgeFact(result, "Caller");
        Assert.Equal("operand-only-change", caller.Properties["relationshipKind"]);
        Assert.Equal("true", caller.Properties["opcodeSequencePreserved"]);
        var retarget = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.ManagedIlCallRetargetObserved);
        Assert.Contains("method:6:Target|", retarget.Properties["beforeTargetIdentity"], StringComparison.Ordinal);
        Assert.Contains("method:9:Alternate|", retarget.Properties["afterTargetIdentity"], StringComparison.Ordinal);
        Assert.NotEqual(retarget.Properties["beforeTargetIdentity"], retarget.Properties["afterTargetIdentity"]);
        Assert.NotEqual(retarget.Properties["beforeToken"], retarget.Properties["afterToken"]);
        Assert.Equal(
            EdgeFact(result, "Caller").Properties["methodIdentity"],
            caller.Properties["methodIdentity"]);
        Assert.DoesNotContain(result.Facts, fact => fact.RuleId == RuleIds.DotNetIlRewriteGap);
    }

    [Fact]
    public void Unchanged_method_emits_unchanged_edge()
    {
        using var temp = new TempDirectory();
        var before = Path.Combine(temp.Path, "RewriteShapes.dll");
        var after = Path.Combine(temp.Path, "RewriteShapes.after.dll");
        WriteBeforeAssembly(before);
        MutateConstantOperand(before, after);

        var result = Scan(PairOptions(before, after));

        var stable = EdgeFact(result, "Stable");
        Assert.Equal("unchanged", stable.Properties["relationshipKind"]);
        Assert.Equal("false", stable.Properties["tokenRetargeted"]);
        Assert.Equal(stable.Properties["beforeIlBodySha256"], stable.Properties["afterIlBodySha256"]);
        Assert.Equal(stable.Properties["beforeBodyIdentity"], stable.Properties["afterBodyIdentity"]);
        Assert.Equal("0", stable.Properties["callRetargetCount"]);
    }

    [Fact]
    public void Identical_before_and_after_proves_every_body_unchanged()
    {
        using var temp = new TempDirectory();
        var before = Path.Combine(temp.Path, "RewriteShapes.dll");
        WriteBeforeAssembly(before);

        var result = Scan(PairOptions(before, before));

        var provenance = result.Manifest.IlRewriteProvenance!;
        Assert.Equal("rewrite-complete", provenance.CoverageState);
        var edges = result.Facts.Where(fact => fact.FactType == FactTypes.ManagedIlRewriteObserved).ToArray();
        Assert.Equal(6, edges.Length);
        Assert.All(edges, edge => Assert.Equal("unchanged", edge.Properties["relationshipKind"]));
        Assert.All(edges, edge => Assert.Equal("false", edge.Properties["tokenRetargeted"]));
        Assert.DoesNotContain(result.Facts, fact => fact.RuleId == RuleIds.DotNetIlRewriteGap);
    }

    [Fact]
    public void Non_instruction_body_change_is_classified_as_body_structure_change()
    {
        using var temp = new TempDirectory();
        var before = Path.Combine(temp.Path, "RewriteShapes.dll");
        var after = Path.Combine(temp.Path, "RewriteShapes.after.dll");
        WriteBeforeAssembly(before);
        MutateAddLocal(before, after, "Stable");

        var result = Scan(PairOptions(before, after));

        var stable = EdgeFact(result, "Stable");
        Assert.Equal("body-structure-change", stable.Properties["relationshipKind"]);
        Assert.Equal("true", stable.Properties["opcodeSequencePreserved"]);
        Assert.Equal("0", stable.Properties["callRetargetCount"]);
        Assert.NotEqual(stable.Properties["beforeIlBodySha256"], stable.Properties["afterIlBodySha256"]);
        // Unmodified members are never swept into the structural change.
        Assert.Equal("unchanged", EdgeFact(result, "Constant").Properties["relationshipKind"]);
    }

    [Fact]
    public void Mixed_operand_and_structure_change_is_never_labeled_operand_only()
    {
        using var temp = new TempDirectory();
        var before = Path.Combine(temp.Path, "RewriteShapes.dll");
        var after = Path.Combine(temp.Path, "RewriteShapes.after.dll");
        WriteBeforeAssembly(before);
        MutateConstantOperand(before, after);
        MutateAddLocal(after, after, "Constant");

        var result = Scan(PairOptions(before, after));

        Assert.Equal("operand-and-body-structure-change", EdgeFact(result, "Constant").Properties["relationshipKind"]);
    }

    [Fact]
    public void Omitted_overflow_pairs_commit_their_declared_locators_to_the_digest()
    {
        using var temp = new TempDirectory();
        var first = Path.Combine(temp.Path, "RewriteShapes.dll");
        var second = Path.Combine(temp.Path, "RewriteShapesSecond.dll");
        WriteBeforeAssembly(first);
        File.Copy(first, second);
        var options = new ScanOptions(
            RepoRoot(),
            TempOutput(),
            IlRewriteEvidence: true,
            IlRewriteBeforePaths: [first, second],
            IlRewriteAfterPaths: [first, second],
            IlRewriteLimits: new IlRewriteLimits(MaxPairCount: 1));
        var firstOmitted = ScanEngine.Scan(options);
        var other = ScanEngine.Scan(options with { IlRewriteBeforePaths = [first, first], IlRewriteAfterPaths = [first, first] });

        var overflow = firstOmitted.Manifest.IlRewriteProvenance!.Outcomes[1];
        Assert.Equal("IlRewritePairCountLimitExceeded", Assert.Single(overflow.GapKinds));
        Assert.Contains("RewriteShapesSecond", overflow.BeforeSafeLocator, StringComparison.Ordinal);
        var otherProvenance = other.Manifest.IlRewriteProvenance!;
        Assert.NotEqual(firstOmitted.Manifest.IlRewriteProvenance!.BoundedInputSha256, otherProvenance.BoundedInputSha256);
    }

    [Fact]
    public void Duplicate_identity_on_after_side_fails_closed()
    {
        using var temp = new TempDirectory();
        var before = Path.Combine(temp.Path, "RewriteShapes.dll");
        var after = Path.Combine(temp.Path, "RewriteShapes.after.dll");
        WriteBeforeAssembly(before);
        MutateDuplicateStableIdentity(before, after);

        var result = Scan(PairOptions(before, after));

        Assert.Equal("rewrite-partial", result.Manifest.IlRewriteProvenance!.CoverageState);
        Assert.Contains(result.Facts, fact => fact.RuleId == RuleIds.DotNetIlRewriteGap
            && fact.Properties.GetValueOrDefault("gapKind") == "IlRewriteIdentityAmbiguous"
            && fact.EvidenceTier == EvidenceTiers.Tier4Unknown);
        Assert.DoesNotContain(result.Facts, fact => fact.FactType == FactTypes.ManagedIlRewriteObserved
            && fact.Properties.GetValueOrDefault("methodIdentity")!.Contains("method:6:Stable|", StringComparison.Ordinal));
        // Unambiguous identities keep their independently proven edges.
        Assert.NotNull(EdgeFact(result, "Constant"));
        Assert.NotNull(EdgeFact(result, "Caller"));
    }

    [Fact]
    public void Different_assembly_identity_fails_closed_without_joins()
    {
        using var temp = new TempDirectory();
        var before = Path.Combine(temp.Path, "RewriteShapes.dll");
        var after = Path.Combine(temp.Path, "RewriteShapes.after.dll");
        WriteBeforeAssembly(before);
        MutateAssemblyName(before, after);

        var result = Scan(PairOptions(before, after));

        Assert.Equal("rewrite-partial", result.Manifest.IlRewriteProvenance!.CoverageState);
        var gap = Assert.Single(result.Facts, fact => fact.RuleId == RuleIds.DotNetIlRewriteGap);
        Assert.Equal("IlRewriteAssemblyIdentityMismatch", gap.Properties["gapKind"]);
        Assert.DoesNotContain(result.Facts, fact => fact.FactType is FactTypes.ManagedIlRewriteObserved or FactTypes.ManagedIlCallRetargetObserved);
    }

    [Fact]
    public void Malformed_after_side_withholds_the_whole_pair()
    {
        using var temp = new TempDirectory();
        var before = Path.Combine(temp.Path, "RewriteShapes.dll");
        var after = Path.Combine(temp.Path, "RewriteShapes.after.dll");
        WriteBeforeAssembly(before);
        MutateConstantOperand(before, after);
        CorruptMethodBody(after, "Constant");

        var result = Scan(PairOptions(before, after));

        Assert.Equal("rewrite-partial", result.Manifest.IlRewriteProvenance!.CoverageState);
        Assert.Contains(result.Facts, fact => fact.RuleId == RuleIds.DotNetIlRewriteGap
            && fact.Properties.GetValueOrDefault("gapKind") is "IlRewriteMalformedInput" or "IlRewriteReaderDisagreement");
        Assert.DoesNotContain(result.Facts, fact => fact.FactType is FactTypes.ManagedIlRewriteObserved or FactTypes.ManagedIlCallRetargetObserved);
    }

    [Fact]
    public void Missing_after_side_emits_side_gap_with_cause()
    {
        using var temp = new TempDirectory();
        var before = Path.Combine(temp.Path, "RewriteShapes.dll");
        WriteBeforeAssembly(before);

        var result = Scan(PairOptions(before, Path.Combine(temp.Path, "absent.dll")));

        var provenance = result.Manifest.IlRewriteProvenance!;
        Assert.Equal("rewrite-partial", provenance.CoverageState);
        var outcome = Assert.Single(provenance.Outcomes);
        Assert.Equal("IlRewriteSideUnavailable", Assert.Single(outcome.GapKinds));
        Assert.Equal("unavailable", outcome.Outcome);
        Assert.Equal("after", outcome.Side);
        Assert.Equal("after:IlRewriteSideMissing", outcome.Cause);
        var gap = Assert.Single(result.Facts, fact => fact.RuleId == RuleIds.DotNetIlRewriteGap);
        Assert.Equal("after", gap.Properties["side"]);
        Assert.Equal("IlRewriteSideMissing", gap.Properties["cause"]);
        // The gap is evidenced on the failing input, not the healthy side.
        Assert.Equal(outcome.AfterSafeLocator, gap.Evidence.FilePath);
        Assert.DoesNotContain(result.Facts, fact => fact.FactType is FactTypes.ManagedIlRewriteObserved or FactTypes.ManagedIlCallRetargetObserved);
    }

    [Fact]
    public void Distinct_side_failures_keep_their_own_side_cause_and_locator()
    {
        using var temp = new TempDirectory();
        var before = Path.Combine(temp.Path, "RewriteShapes.dll");
        var after = Path.Combine(temp.Path, "RewriteShapes.after.dll");
        WriteBeforeAssembly(before);
        MutateConstantOperand(before, after);
        CorruptMethodBody(after, "Constant");

        var result = Scan(new ScanOptions(
            RepoRoot(),
            TempOutput(),
            IlRewriteEvidence: true,
            IlRewriteBeforePaths: [Path.Combine(temp.Path, "absent.dll")],
            IlRewriteAfterPaths: [after]));

        var provenance = result.Manifest.IlRewriteProvenance!;
        var outcome = Assert.Single(provenance.Outcomes);
        Assert.Equal(["IlRewriteMalformedInput", "IlRewriteSideUnavailable"], outcome.GapKinds);
        Assert.Equal("after+before", outcome.Side);
        Assert.Equal("after:MalformedIlBody+before:IlRewriteSideMissing", outcome.Cause);
        var sideGaps = result.Facts.Where(fact => fact.RuleId == RuleIds.DotNetIlRewriteGap
            && fact.Properties.ContainsKey("side")).OrderBy(fact => fact.Properties["side"], StringComparer.Ordinal).ToArray();
        Assert.Equal(2, sideGaps.Length);
        var afterGap = sideGaps[0];
        Assert.Equal("after", afterGap.Properties["side"]);
        Assert.Equal("IlRewriteMalformedInput", afterGap.Properties["gapKind"]);
        Assert.Equal("MalformedIlBody", afterGap.Properties["cause"]);
        Assert.Equal(outcome.AfterSafeLocator, afterGap.Evidence.FilePath);
        var beforeGap = sideGaps[1];
        Assert.Equal("before", beforeGap.Properties["side"]);
        Assert.Equal("IlRewriteSideUnavailable", beforeGap.Properties["gapKind"]);
        Assert.Equal("IlRewriteSideMissing", beforeGap.Properties["cause"]);
        Assert.Equal(outcome.BeforeSafeLocator, beforeGap.Evidence.FilePath);
    }

    [Fact]
    public void Positive_edges_keep_the_rewrite_location_kind_alongside_pair_gaps()
    {
        using var temp = new TempDirectory();
        var before = Path.Combine(temp.Path, "RewriteShapes.dll");
        var after = Path.Combine(temp.Path, "RewriteShapes.after.dll");
        WriteBeforeAssembly(before);
        MutateInsertMethodFirst(before, after);

        var result = Scan(PairOptions(before, after));

        Assert.Contains(result.Facts, fact => fact.FactType is FactTypes.ManagedIlRewriteObserved or FactTypes.ManagedIlCallRetargetObserved
            && fact.Properties.GetValueOrDefault("evidenceLocationKind") == "managed-il-rewrite-v1");
        Assert.DoesNotContain(result.Facts, fact => fact.FactType is FactTypes.ManagedIlRewriteObserved or FactTypes.ManagedIlCallRetargetObserved
            && fact.Properties.GetValueOrDefault("evidenceLocationKind") != "managed-il-rewrite-v1");
        Assert.Contains(result.Facts, fact => fact.RuleId == RuleIds.DotNetIlRewriteGap
            && fact.Properties.GetValueOrDefault("evidenceLocationKind") == "managed-input-v1");
    }

    [Fact]
    public void Membership_only_gaps_label_the_outcome_as_a_membership_delta()
    {
        using var temp = new TempDirectory();
        var before = Path.Combine(temp.Path, "RewriteShapes.dll");
        var after = Path.Combine(temp.Path, "RewriteShapes.after.dll");
        WriteBeforeAssembly(before);
        MutateInsertMethodFirst(before, after);

        var result = Scan(PairOptions(before, after));

        var outcome = Assert.Single(result.Manifest.IlRewriteProvenance!.Outcomes);
        Assert.Equal(["IlRewriteMethodAfterOnly"], outcome.GapKinds);
        Assert.Equal("membership-delta", outcome.Outcome);
    }

    [Fact]
    public void Omitted_membership_digest_covers_only_the_omitted_suffix()
    {
        using var temp = new TempDirectory();
        var before = Path.Combine(temp.Path, "RewriteShapesMany.dll");
        var after = Path.Combine(temp.Path, "RewriteShapesMany.after.dll");
        WriteManyMethodsAssembly(before, 10);
        MutateRemoveAllMethods(after, before);

        // The IL body lane independently reports every before-side method
        // identity, so the commitment is recomputed from reader-proven rows.
        var ilScan = Scan(new ScanOptions(
            RepoRoot(),
            TempOutput(),
            CompiledInputPaths: [before],
            IlBodyEvidence: true));
        var identities = ilScan.Facts
            .Where(fact => fact.FactType == FactTypes.ManagedIlBodyDeclared)
            .Select(fact => fact.TargetSymbol!.Split("|il-body:", 2)[0])
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(10, identities.Length);

        var result = Scan(PairOptions(before, after));
        var gap = Assert.Single(result.Facts, fact => fact.RuleId == RuleIds.DotNetIlRewriteGap
            && fact.Properties.GetValueOrDefault("gapKind") == "IlRewriteMethodBeforeOnly");
        Assert.Equal("10", gap.Properties["identityCount"]);
        Assert.Equal("2", gap.Properties["omittedIdentityCount"]);
        for (var index = 0; index < 8; index++)
            Assert.Equal(identities[index], gap.Properties[$"identity[{index}]"]);
        Assert.Equal(
            ManagedMetadataExtractor.CanonicalDigest(identities.Skip(8).ToArray()),
            gap.Properties["omittedIdentitySha256"]);
        Assert.Equal(result.Manifest.IlRewriteProvenance!.Outcomes[0].BeforeSafeLocator, gap.Evidence.FilePath);
    }

    [Fact]
    public void Invalid_reused_limits_are_rejected()
    {
        using var temp = new TempDirectory();
        var before = Path.Combine(temp.Path, "RewriteShapes.dll");
        WriteBeforeAssembly(before);
        Assert.Throws<ArgumentException>(() => Scan(new ScanOptions(
            RepoRoot(),
            TempOutput(),
            IlRewriteEvidence: true,
            IlRewriteBeforePaths: [before],
            IlRewriteAfterPaths: [before],
            IlBodyLimits: new IlBodyLimits(MaxTextLength: 0))));
        Assert.Throws<ArgumentException>(() => Scan(new ScanOptions(
            RepoRoot(),
            TempOutput(),
            IlRewriteEvidence: true,
            IlRewriteBeforePaths: [before],
            IlRewriteAfterPaths: [before],
            CompiledInputLimits: new CompiledInputLimits(MaxFileSizeBytes: 0))));
    }

    [Fact]
    public void Budget_exhaustion_emits_limit_gap_instead_of_guessed_edges()
    {
        using var temp = new TempDirectory();
        var before = Path.Combine(temp.Path, "RewriteShapes.dll");
        var after = Path.Combine(temp.Path, "RewriteShapes.after.dll");
        WriteBeforeAssembly(before);
        MutateConstantOperand(before, after);

        var result = Scan(PairOptions(before, after, new IlBodyLimits(MaxTotalWorkUnits: 4)));

        Assert.Equal("rewrite-partial", result.Manifest.IlRewriteProvenance!.CoverageState);
        Assert.Contains(result.Facts, fact => fact.RuleId == RuleIds.DotNetIlRewriteGap
            && fact.Properties.GetValueOrDefault("gapKind") == "IlRewriteTotalWorkLimitExceeded");
        Assert.DoesNotContain(result.Facts, fact => fact.FactType is FactTypes.ManagedIlRewriteObserved or FactTypes.ManagedIlCallRetargetObserved);
    }

    [Fact]
    public void Pair_count_limit_emits_gap_for_overflow_pairs()
    {
        using var temp = new TempDirectory();
        var first = Path.Combine(temp.Path, "RewriteShapes.dll");
        var second = Path.Combine(temp.Path, "RewriteShapesSecond.dll");
        WriteBeforeAssembly(first);
        File.Copy(first, second);
        var options = new ScanOptions(
            RepoRoot(),
            TempOutput(),
            IlRewriteEvidence: true,
            IlRewriteBeforePaths: [first, second],
            IlRewriteAfterPaths: [first, second],
            IlRewriteLimits: new IlRewriteLimits(MaxPairCount: 1));

        var result = ScanEngine.Scan(options);

        var provenance = result.Manifest.IlRewriteProvenance!;
        Assert.Equal(2, provenance.Outcomes.Count);
        Assert.Equal("admitted", provenance.Outcomes[0].Outcome);
        Assert.Equal("IlRewritePairCountLimitExceeded", Assert.Single(provenance.Outcomes[1].GapKinds));
        Assert.Equal("rewrite-partial", provenance.CoverageState);
    }

    [Fact]
    public void Requested_lane_without_pairs_emits_rule_backed_gap()
    {
        var result = Scan(new ScanOptions(RepoRoot(), TempOutput(), IlRewriteEvidence: true));

        var provenance = result.Manifest.IlRewriteProvenance!;
        Assert.Equal("rewrite-partial", provenance.CoverageState);
        Assert.Contains(result.Facts, fact => fact.RuleId == RuleIds.DotNetIlRewriteGap
            && fact.Properties.GetValueOrDefault("gapKind") == "IlRewritePairUnavailable"
            && fact.EvidenceTier == EvidenceTiers.Tier4Unknown);
        Assert.DoesNotContain(result.Facts, fact => fact.FactType is FactTypes.ManagedIlRewriteObserved or FactTypes.ManagedIlCallRetargetObserved);
    }

    [Fact]
    public void Declaration_count_mismatch_fails_closed()
    {
        using var temp = new TempDirectory();
        var before = Path.Combine(temp.Path, "RewriteShapes.dll");
        WriteBeforeAssembly(before);
        var result = Scan(new ScanOptions(
            RepoRoot(),
            TempOutput(),
            IlRewriteEvidence: true,
            IlRewriteBeforePaths: [before, before],
            IlRewriteAfterPaths: [before]));

        var provenance = result.Manifest.IlRewriteProvenance!;
        Assert.Equal("rewrite-partial", provenance.CoverageState);
        Assert.Contains(result.Facts, fact => fact.RuleId == RuleIds.DotNetIlRewriteGap
            && fact.Properties.GetValueOrDefault("gapKind") == "IlRewritePairDeclarationInvalid");
        Assert.DoesNotContain(result.Facts, fact => fact.FactType is FactTypes.ManagedIlRewriteObserved or FactTypes.ManagedIlCallRetargetObserved);
    }

    [Fact]
    public void Disabled_lane_is_inert_even_with_declared_pairs()
    {
        using var temp = new TempDirectory();
        var before = Path.Combine(temp.Path, "RewriteShapes.dll");
        var after = Path.Combine(temp.Path, "RewriteShapes.after.dll");
        WriteBeforeAssembly(before);
        MutateConstantOperand(before, after);
        var options = new ScanOptions(
            RepoRoot(),
            TempOutput(),
            IlRewriteBeforePaths: [before],
            IlRewriteAfterPaths: [after]);

        var result = Scan(options);

        Assert.Null(result.Manifest.IlRewriteProvenance);
        Assert.DoesNotContain(result.Facts, fact => fact.RuleId is RuleIds.DotNetIlRewrite or RuleIds.DotNetIlRewriteGap);
        Assert.DoesNotContain(result.Manifest.KnownGaps, gap => gap.StartsWith("IL rewrite evidence coverage reduced:", StringComparison.Ordinal));
    }

    [Fact]
    public void Invalid_limits_are_rejected()
    {
        using var temp = new TempDirectory();
        var before = Path.Combine(temp.Path, "RewriteShapes.dll");
        WriteBeforeAssembly(before);
        Assert.Throws<ArgumentException>(() => Scan(new ScanOptions(
            RepoRoot(),
            TempOutput(),
            IlRewriteEvidence: true,
            IlRewriteBeforePaths: [before],
            IlRewriteAfterPaths: [before],
            IlRewriteLimits: new IlRewriteLimits(MaxPairCount: 0))));
    }

    [Fact]
    public void Existing_source_metadata_and_il_evidence_is_unchanged_by_the_lane()
    {
        var fixture = Fixture("csharp", "CompiledEvidence.CSharp");
        using var temp = new TempDirectory();
        var after = Path.Combine(temp.Path, "CompiledEvidence.CSharp.after.dll");
        MutateUserString(fixture.Assembly, after, "il-alpha", "il-omega");
        var baseline = Scan(new ScanOptions(
            fixture.Source,
            TempOutput(),
            CompiledInputPaths: [fixture.Assembly],
            IlBodyEvidence: true));
        var withRewrite = Scan(new ScanOptions(
            fixture.Source,
            TempOutput(),
            CompiledInputPaths: [fixture.Assembly],
            IlBodyEvidence: true,
            IlRewriteEvidence: true,
            IlRewriteBeforePaths: [fixture.Assembly],
            IlRewriteAfterPaths: [after]));

        // Scan identity differs by design, so derived fact IDs (including
        // cross-referenced supporting IDs inside properties) are normalized
        // before comparing every non-rewrite fact byte for byte.
        var projection = (IReadOnlyList<CodeFact> facts) => facts
            .Where(fact => fact.RuleId is not (RuleIds.DotNetIlRewrite or RuleIds.DotNetIlRewriteGap))
            .Select(fact => JsonSerializer.Serialize(fact with { FactId = string.Empty, ScanId = string.Empty }))
            .Select(value => System.Text.RegularExpressions.Regex.Replace(value, "fact-[0-9a-f]+", "fact-x"))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(projection(baseline.Facts), projection(withRewrite.Facts));
        Assert.Equal(
            baseline.Facts.Count(fact => fact.FactType == FactTypes.ManagedIlBodyDeclared),
            withRewrite.Facts.Count(fact => fact.FactType == FactTypes.ManagedIlBodyDeclared));
        var stringAlpha = EdgeFact(withRewrite, "StringAlpha");
        Assert.Equal("operand-only-change", stringAlpha.Properties["relationshipKind"]);
        Assert.Equal("unchanged", EdgeFact(withRewrite, "StringBeta").Properties["relationshipKind"]);
    }

    [Fact]
    public void Three_language_matrix_admits_rewrite_pairs()
    {
        using var temp = new TempDirectory();
        var csharp = Fixture("csharp", "CompiledEvidence.CSharp");
        var vb = Fixture("vb", "CompiledEvidence.VisualBasic");
        var fsharp = Fixture("fsharp", "CompiledEvidence.FSharp");
        var csharpAfter = Path.Combine(temp.Path, "CompiledEvidence.CSharp.after.dll");
        MutateUserString(csharp.Assembly, csharpAfter, "il-alpha", "il-omega");
        var vbAfter = Path.Combine(temp.Path, "CompiledEvidence.VisualBasic.after.dll");
        File.Copy(vb.Assembly, vbAfter);
        var fsharpAfter = Path.Combine(temp.Path, "CompiledEvidence.FSharp.after.dll");
        File.Copy(fsharp.Assembly, fsharpAfter);

        var result = Scan(new ScanOptions(
            csharp.Source,
            TempOutput(),
            IlRewriteEvidence: true,
            IlRewriteBeforePaths: [csharp.Assembly, vb.Assembly, fsharp.Assembly],
            IlRewriteAfterPaths: [csharpAfter, vbAfter, fsharpAfter]));

        var provenance = result.Manifest.IlRewriteProvenance!;
        Assert.Equal(3, provenance.Outcomes.Count);
        Assert.All(provenance.Outcomes, outcome => Assert.Equal("admitted", outcome.Outcome));
        Assert.Equal("rewrite-complete", provenance.CoverageState);
        Assert.Contains(provenance.Outcomes, outcome => outcome.BeforeAssemblyIdentity!.Contains("name:23:CompiledEvidence.CSharp", StringComparison.Ordinal));
        Assert.Contains(provenance.Outcomes, outcome => outcome.AfterAssemblyIdentity!.Contains("name:28:CompiledEvidence.VisualBasic", StringComparison.Ordinal));
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.ManagedIlRewriteObserved
            && fact.Properties.GetValueOrDefault("beforeAssemblyIdentity")!.Contains("CompiledEvidence.FSharp", StringComparison.Ordinal)
            && fact.Properties["relationshipKind"] == "unchanged");
    }

    [Fact]
    public void Repeat_scans_are_deterministic_and_privacy_projected()
    {
        using var temp = new TempDirectory();
        var before = Path.Combine(temp.Path, "RewriteShapes.dll");
        var after = Path.Combine(temp.Path, "RewriteShapes.after.dll");
        WriteBeforeAssembly(before);
        MutateConstantOperand(before, after);
        var first = Scan(PairOptions(before, after));
        var second = ScanEngine.Scan(PairOptions(before, after));

        Assert.Equal(
            JsonSerializer.Serialize(first.Facts),
            JsonSerializer.Serialize(second.Facts));
        Assert.Equal(
            JsonSerializer.Serialize(first.Manifest.IlRewriteProvenance),
            JsonSerializer.Serialize(second.Manifest.IlRewriteProvenance));
        var serialized = JsonSerializer.Serialize(first);
        Assert.DoesNotContain(temp.Path, serialized, StringComparison.Ordinal);
        Assert.DoesNotContain(Path.GetFullPath(RepoRoot()), serialized, StringComparison.Ordinal);
        Assert.All(first.Facts.Where(fact => fact.RuleId is RuleIds.DotNetIlRewrite or RuleIds.DotNetIlRewriteGap), fact =>
        {
            Assert.False(string.IsNullOrWhiteSpace(fact.Properties.GetValueOrDefault("ilRewriteBoundedInputSha256")));
            Assert.False(string.IsNullOrWhiteSpace(fact.Properties.GetValueOrDefault("ilRewriteGeneratorSha256")));
            Assert.False(string.IsNullOrWhiteSpace(fact.Properties.GetValueOrDefault("limitation")));
            Assert.Equal(ScannerVersions.IlRewriteEvidenceExtractor, fact.Evidence.ExtractorVersion);
        });
    }

    [Fact]
    public async Task Rewrite_evidence_persists_through_every_required_artifact()
    {
        using var temp = new TempDirectory();
        var before = Path.Combine(temp.Path, "RewriteShapes.dll");
        var after = Path.Combine(temp.Path, "RewriteShapes.after.dll");
        WriteBeforeAssembly(before);
        MutateInsertMethodFirst(before, after);
        var output = Path.Combine(Directory.CreateTempSubdirectory("tracemap-ilrewrite-artifacts-").FullName, "out");
        var cliError = new StringWriter();
        var cliExit = await TraceMapCommand.RunAsync(
        [
            "scan",
            "--repo", RepoRoot(),
            "--out", output,
            "--il-rewrite-evidence",
            "--il-rewrite-before", before,
            "--il-rewrite-after", after
        ], TextWriter.Null, cliError);
        Assert.True(cliExit == 0, $"CLI scan failed with exit {cliExit}: {cliError}");

        Assert.True(File.Exists(Path.Combine(output, "facts.ndjson")));
        Assert.True(File.Exists(Path.Combine(output, "index.sqlite")));
        Assert.True(File.Exists(Path.Combine(output, "report.md")));
        Assert.True(File.Exists(Path.Combine(output, "scan-manifest.json")));
        var ndjson = File.ReadAllText(Path.Combine(output, "facts.ndjson"));
        Assert.Contains("dotnet.compiled.il-rewrite.v1", ndjson, StringComparison.Ordinal);
        Assert.Contains("dotnet.compiled.il-rewrite-gap.v1", ndjson, StringComparison.Ordinal);
        Assert.Contains("ManagedIlRewriteObserved", ndjson, StringComparison.Ordinal);
        Assert.Contains("ManagedIlCallRetargetObserved", ndjson, StringComparison.Ordinal);
        using var connection = new SqliteConnection($"Data Source={Path.Combine(output, "index.sqlite")}");
        connection.Open();
        var command = connection.CreateCommand();
        command.CommandText = "select count(*) from facts where rule_id like 'dotnet.compiled.il-rewrite%'";
        var rows = Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
        Assert.True(rows > 0);
        var manifestText = File.ReadAllText(Path.Combine(output, "scan-manifest.json"));
        Assert.Contains("ilRewriteProvenance", manifestText, StringComparison.Ordinal);
        Assert.Contains("## Compiled .NET IL Rewrite Evidence", File.ReadAllText(Path.Combine(output, "report.md")), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cli_rejects_unpaired_or_unflagged_rewrite_declarations()
    {
        using var temp = new TempDirectory();
        var before = Path.Combine(temp.Path, "RewriteShapes.dll");
        WriteBeforeAssembly(before);
        var unflagged = await TraceMapCommand.RunAsync(
        [
            "scan",
            "--repo", RepoRoot(),
            "--out", Path.Combine(temp.Path, "out1"),
            "--il-rewrite-before", before
        ], TextWriter.Null, new StringWriter());
        Assert.Equal(1, unflagged);
        var mismatched = await TraceMapCommand.RunAsync(
        [
            "scan",
            "--repo", RepoRoot(),
            "--out", Path.Combine(temp.Path, "out2"),
            "--il-rewrite-evidence",
            "--il-rewrite-before", before,
            "--il-rewrite-after", before,
            "--il-rewrite-after", before
        ], TextWriter.Null, new StringWriter());
        Assert.Equal(1, mismatched);
    }

    [Fact]
    public void Join_layer_withholds_edges_when_one_side_identity_is_ambiguous()
    {
        var body = new IlBodyObservation(
            "0x06000001",
            "identity",
            1,
            "aaaa",
            "eeee",
            0,
            "bbbb",
            0,
            "cccc",
            "8",
            false,
            "identity|il-body:instructions:1:sha256:dddd",
            "dddd",
            []);
        var before = new IlBodyEvidenceExtractor.IlReaderResult("assembly", "module", "mvid", [body]);
        var duplicated = new IlBodyEvidenceExtractor.IlReaderResult(
            "assembly",
            "module",
            "mvid",
            [body, body with { MetadataToken = "0x06000002" }]);
        var budget = new IlBodyEvidenceExtractor.IlWorkBudget(64);

        var join = IlRewriteEvidenceExtractor.JoinPair("rewrite-pair-001", before, duplicated, budget);

        Assert.Empty(join.Edges);
        Assert.Equal(["IlRewriteIdentityAmbiguous"], join.GapKinds);
        Assert.False(join.WorkExhausted);

        var joined = IlRewriteEvidenceExtractor.JoinPair(
            "rewrite-pair-001",
            before,
            new IlBodyEvidenceExtractor.IlReaderResult("assembly", "module", "mvid", [body with { MetadataToken = "0x06000009" }]),
            new IlBodyEvidenceExtractor.IlWorkBudget(64));
        var edge = Assert.Single(joined.Edges);
        Assert.Equal("unchanged", edge.RelationshipKind);
        Assert.True(edge.TokenRetargeted);
        Assert.Empty(join.Deltas);
    }

    [Fact]
    public void Join_phase_budget_exhaustion_is_atomic_with_no_partial_edges()
    {
        var first = new IlBodyObservation(
            "0x06000001",
            "identity-a",
            1,
            "aaaa",
            "eeee",
            0,
            "bbbb",
            0,
            "cccc",
            "8",
            false,
            "identity-a|il-body:instructions:1:sha256:dddd",
            "dddd",
            []);
        var second = new IlBodyObservation(
            "0x06000002",
            "identity-b",
            1,
            "aaaa",
            "eeee",
            0,
            "bbbb",
            0,
            "cccc",
            "8",
            false,
            "identity-b|il-body:instructions:1:sha256:dddd",
            "dddd",
            []);
        var before = new IlBodyEvidenceExtractor.IlReaderResult("assembly", "module", "mvid", [first, second]);
        var after = new IlBodyEvidenceExtractor.IlReaderResult(
            "assembly",
            "module",
            "mvid",
            [first with { MetadataToken = "0x06000009" }, second with { MetadataToken = "0x0600000a" }]);

        // Budget covers exactly one join: the second identity exhausts the
        // budget after one edge was accumulated, and the whole pair must
        // retain no partial evidence.
        var join = IlRewriteEvidenceExtractor.JoinPair("rewrite-pair-001", before, after, new IlBodyEvidenceExtractor.IlWorkBudget(1));

        Assert.True(join.WorkExhausted);
        Assert.Empty(join.Edges);
        Assert.Empty(join.Deltas);
        Assert.Equal(["IlRewriteTotalWorkLimitExceeded"], join.GapKinds);
    }

    [Fact]
    public void Repeated_pair_declarations_keep_distinct_outcomes_and_digests()
    {
        using var temp = new TempDirectory();
        var before = Path.Combine(temp.Path, "RewriteShapes.dll");
        var after = Path.Combine(temp.Path, "RewriteShapes.after.dll");
        WriteBeforeAssembly(before);
        MutateConstantOperand(before, after);
        var repeated = ScanEngine.Scan(new ScanOptions(
            RepoRoot(),
            TempOutput(),
            IlRewriteEvidence: true,
            IlRewriteBeforePaths: [before, before],
            IlRewriteAfterPaths: [after, after]));
        var single = Scan(PairOptions(before, after));

        var provenance = repeated.Manifest.IlRewriteProvenance!;
        Assert.Equal(2, provenance.Outcomes.Count);
        Assert.All(provenance.Outcomes, outcome => Assert.Equal("admitted", outcome.Outcome));
        Assert.Equal("rewrite-pair-002", provenance.Outcomes[1].PairId);
        Assert.Equal(
            2 * repeated.Facts.Count(fact => fact.FactType == FactTypes.ManagedIlRewriteObserved
                && fact.Properties.GetValueOrDefault("pairId") == "rewrite-pair-001"),
            repeated.Facts.Count(fact => fact.FactType == FactTypes.ManagedIlRewriteObserved));
        Assert.NotEqual(single.Manifest.IlRewriteProvenance!.BoundedInputSha256, provenance.BoundedInputSha256);
    }

    [Fact]
    public void Malformed_declared_path_fails_closed_to_a_side_gap_without_aborting()
    {
        var result = Scan(new ScanOptions(
            RepoRoot(),
            TempOutput(),
            IlRewriteEvidence: true,
            IlRewriteBeforePaths: ["before invalid"],
            IlRewriteAfterPaths: ["after invalid"]));

        var provenance = result.Manifest.IlRewriteProvenance!;
        Assert.Equal("rewrite-partial", provenance.CoverageState);
        var outcome = Assert.Single(provenance.Outcomes);
        Assert.Equal("IlRewriteSideUnavailable", Assert.Single(outcome.GapKinds));
        Assert.Equal(
            "after:IlRewriteSideDeclarationInvalid+before:IlRewriteSideDeclarationInvalid",
            outcome.Cause);
        Assert.StartsWith("__external__/rewrite-before/invalid-", outcome.BeforeSafeLocator, StringComparison.Ordinal);
        Assert.StartsWith("__external__/rewrite-after/invalid-", outcome.AfterSafeLocator, StringComparison.Ordinal);
        Assert.DoesNotContain(result.Facts, fact => fact.FactType is FactTypes.ManagedIlRewriteObserved or FactTypes.ManagedIlCallRetargetObserved);
    }

    [Fact]
    public void Bounded_input_digest_commits_the_compiled_admission_policy()
    {
        using var temp = new TempDirectory();
        var before = Path.Combine(temp.Path, "RewriteShapes.dll");
        var after = Path.Combine(temp.Path, "RewriteShapes.after.dll");
        WriteBeforeAssembly(before);
        MutateConstantOperand(before, after);
        var baseline = IlRewriteEvidenceExtractor.Evaluate(PairOptions(before, after));
        var tighter = IlRewriteEvidenceExtractor.Evaluate(new ScanOptions(
            RepoRoot(),
            TempOutput(),
            IlRewriteEvidence: true,
            IlRewriteBeforePaths: [before],
            IlRewriteAfterPaths: [after],
            CompiledInputLimits: new CompiledInputLimits(MaxFileSizeBytes: 1_024)));

        Assert.NotNull(baseline.Provenance);
        Assert.NotNull(tighter.Provenance);
        Assert.NotEqual(baseline.Provenance.BoundedInputSha256, tighter.Provenance.BoundedInputSha256);
    }

    [Fact]
    public void Rule_catalog_documents_the_rewrite_rules_with_limitations()
    {
        var catalog = File.ReadAllText(Path.Combine(FindRepoRoot(), "rules", "rule-catalog.yml"));
        Assert.Contains("- id: dotnet.compiled.il-rewrite.v1", catalog, StringComparison.Ordinal);
        Assert.Contains("- id: dotnet.compiled.il-rewrite-gap.v1", catalog, StringComparison.Ordinal);
        var rewriteRule = catalog[catalog.IndexOf("- id: dotnet.compiled.il-rewrite.v1", StringComparison.Ordinal)..];
        Assert.Contains("The scanner never performs or attributes the rewrite", rewriteRule, StringComparison.Ordinal);
        Assert.Contains("never guessed joins", rewriteRule, StringComparison.Ordinal);
        Assert.Contains("Rewritten PDB offsets", rewriteRule, StringComparison.Ordinal);
    }

    [Fact]
    public void Fixture_catalog_records_stable_rewrite_cases_gaps_and_non_claims()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            FindRepoRoot(), "samples", "compiled-dotnet-evidence", "fixture-cases.json")));
        Assert.Equal("compiled-dotnet-fixture-cases.v5", document.RootElement.GetProperty("schemaVersion").GetString());
        var cases = document.RootElement.GetProperty("ilRewriteCases").EnumerateArray().ToArray();
        Assert.True(cases.Length >= 8);
        Assert.Equal(cases.Length, cases.Select(item => item.GetProperty("id").GetString()).Distinct(StringComparer.Ordinal).Count());
        Assert.All(cases, item =>
        {
            Assert.False(string.IsNullOrWhiteSpace(item.GetProperty("shape").GetString()));
            Assert.NotEmpty(item.GetProperty("expectedRuleIds").EnumerateArray());
            Assert.Contains(item.GetProperty("expectedTier").GetString(), new[] { EvidenceTiers.Tier2Structural, EvidenceTiers.Tier4Unknown });
            Assert.NotEmpty(item.GetProperty("nonClaims").EnumerateArray());
        });
        Assert.Contains(cases, item => item.GetProperty("id").GetString() == "CS-ILRW-OPERAND-001");
        Assert.Contains(cases, item => item.GetProperty("id").GetString() == "CS-ILRW-TOKEN-002");
        Assert.Contains(cases, item => item.GetProperty("id").GetString() == "ILRW-DISAGREE-007");
        Assert.Contains(cases, item => item.GetProperty("id").GetString() == "ILRW-BUDGET-008");
    }

    private static ScanOptions PairOptions(string before, string after, IlBodyLimits? bodyLimits = null) => new(
        RepoRoot(),
        TempOutput(),
        IlRewriteEvidence: true,
        IlRewriteBeforePaths: [before],
        IlRewriteAfterPaths: [after],
        IlBodyLimits: bodyLimits);

    private static CodeFact EdgeFact(ScanResult result, string methodName)
    {
        var matches = result.Facts.Where(fact => fact.FactType == FactTypes.ManagedIlRewriteObserved
            && fact.Properties.GetValueOrDefault("methodIdentity")!.Contains(
                $"method:{methodName.Length.ToString(CultureInfo.InvariantCulture)}:{methodName}|",
                StringComparison.Ordinal)).ToArray();
        return Assert.Single(matches);
    }

    private static ScanResult Scan(ScanOptions options) => ScanEngine.Scan(options);

    private static string TempOutput() => Path.Combine(Directory.CreateTempSubdirectory("tracemap-ilrewrite-output-").FullName, "out");

    private static string RepoRoot() => Path.Combine(FindRepoRoot(), "samples", "compiled-dotnet-evidence", "csharp");

    private static (string Source, string Assembly) Fixture(string language, string assemblyName)
    {
        var source = Path.Combine(FindRepoRoot(), "samples", "compiled-dotnet-evidence", language);
        return (source, Path.Combine(source, "bin", "Debug", "net10.0", assemblyName + ".dll"));
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

    private static void WriteBeforeAssembly(string path)
    {
        using var assembly = CecilAssemblyDefinition.CreateAssembly(
            new AssemblyNameDefinition("RewriteShapes", new Version(1, 0)),
            "RewriteShapes",
            ModuleKind.Dll);
        var module = assembly.MainModule;
        var type = new CecilTypeDefinition("Fixture", "RewriteShapes", Mono.Cecil.TypeAttributes.Public, module.TypeSystem.Object);
        module.Types.Add(type);
        var target = AddStaticMethod(type, module, "Target", module.TypeSystem.Int32, [module.TypeSystem.Int32]);
        target.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
        target.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
        var alternate = AddStaticMethod(type, module, "Alternate", module.TypeSystem.Int32, [module.TypeSystem.Int32]);
        alternate.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
        alternate.Body.Instructions.Add(Instruction.Create(OpCodes.Ldc_I4, 1));
        alternate.Body.Instructions.Add(Instruction.Create(OpCodes.Add));
        alternate.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
        var caller = AddStaticMethod(type, module, "Caller", module.TypeSystem.Int32, [module.TypeSystem.Int32]);
        var constant = AddStaticMethod(type, module, "Constant", module.TypeSystem.Int32, []);
        var stable = AddStaticMethod(type, module, "Stable", module.TypeSystem.Void, []);
        var loader = AddStaticMethod(type, module, "Loader", module.TypeSystem.String, []);
        caller.Body.Instructions.Add(Instruction.Create(OpCodes.Ldarg_0));
        caller.Body.Instructions.Add(Instruction.Create(OpCodes.Call, FindMethod(type, "Target")));
        caller.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
        constant.Body.Instructions.Add(Instruction.Create(OpCodes.Ldc_I4, 41));
        constant.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
        stable.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
        loader.Body.Instructions.Add(Instruction.Create(OpCodes.Ldstr, "before"));
        loader.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
        assembly.Write(path);
    }

    private static CecilMethodDefinition AddStaticMethod(
        CecilTypeDefinition type,
        CecilModuleDefinition module,
        string name,
        Mono.Cecil.TypeReference returnType,
        IReadOnlyList<Mono.Cecil.TypeReference> parameters)
    {
        var method = new CecilMethodDefinition(
            name,
            Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.Static,
            returnType);
        foreach (var parameter in parameters)
            method.Parameters.Add(new ParameterDefinition(parameter));
        type.Methods.Add(method);
        return method;
    }

    private static CecilMethodDefinition FindMethod(CecilTypeDefinition type, string name) =>
        type.Methods.Single(method => method.Name == name);

    private static void WriteManyMethodsAssembly(string path, int count)
    {
        using var assembly = CecilAssemblyDefinition.CreateAssembly(
            new AssemblyNameDefinition("RewriteShapesMany", new Version(1, 0)),
            "RewriteShapesMany",
            ModuleKind.Dll);
        var module = assembly.MainModule;
        var type = new CecilTypeDefinition("Fixture", "RewriteShapes", Mono.Cecil.TypeAttributes.Public, module.TypeSystem.Object);
        module.Types.Add(type);
        for (var index = 0; index < count; index++)
        {
            var method = new CecilMethodDefinition(
                $"Named{index.ToString("D2", CultureInfo.InvariantCulture)}",
                Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.Static,
                module.TypeSystem.Void);
            method.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
            type.Methods.Add(method);
        }

        assembly.Write(path);
    }

    private static void MutateRemoveAllMethods(string after, string before)
    {
        using var assembly = CecilAssemblyDefinition.ReadAssembly(before);
        var type = (CecilTypeDefinition)assembly.MainModule.Types.Single(item => item.Name == "RewriteShapes");
        type.Methods.Clear();
        assembly.Write(after);
    }

    private static void Mutate(string before, string after, Action<CecilAssemblyDefinition, CecilTypeDefinition> mutate)
    {
        using var assembly = CecilAssemblyDefinition.ReadAssembly(before);
        var type = (CecilTypeDefinition)assembly.MainModule.Types.Single(item => item.Name == "RewriteShapes");
        mutate(assembly, type);
        assembly.Write(after);
    }

    private static void MutateConstantOperand(string before, string after) => Mutate(before, after, (_, type) =>
    {
        var constant = FindMethod(type, "Constant");
        var instruction = Assert.Single(constant.Body.Instructions, item => item.OpCode == OpCodes.Ldc_I4);
        instruction.Operand = 42;
    });

    private static void MutateInsertMethodFirst(string before, string after) => Mutate(before, after, (_, type) =>
    {
        var inserted = new CecilMethodDefinition(
            "Inserted",
            Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.Static,
            type.Module.TypeSystem.Void);
        inserted.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
        // Inserting at position zero renumbers every existing MethodDef row,
        // retargeting module-local tokens while member identities stay equal.
        type.Methods.Insert(0, inserted);
    });

    private static void MutateCallTarget(string before, string after) => Mutate(before, after, (_, type) =>
    {
        var caller = FindMethod(type, "Caller");
        var call = Assert.Single(caller.Body.Instructions, item => item.OpCode == OpCodes.Call);
        call.Operand = FindMethod(type, "Alternate");
    });

    private static void MutateDuplicateStableIdentity(string before, string after) => Mutate(before, after, (_, type) =>
    {
        var duplicate = new CecilMethodDefinition(
            "Stable",
            Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.Static,
            type.Module.TypeSystem.Void);
        duplicate.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
        type.Methods.Add(duplicate);
    });

    private static void MutateAssemblyName(string before, string after) => Mutate(before, after, (assembly, _) =>
    {
        assembly.Name.Name = "RewriteShapesDifferent";
    });

    private static void MutateAddLocal(string before, string after, string methodName)
    {
        using var assembly = CecilAssemblyDefinition.ReadAssembly(before);
        var type = (CecilTypeDefinition)assembly.MainModule.Types.Single(item => item.Name == "RewriteShapes");
        var method = FindMethod(type, methodName);
        // A new local changes only the non-instruction body structure: the
        // opcode stream, operands, and call sites stay byte-identical.
        method.Body.Variables.Add(new VariableDefinition(type.Module.TypeSystem.Int32));
        method.Body.InitLocals = true;
        assembly.Write(after);
    }

    private static void MutateUserString(string before, string after, string from, string to)
    {
        using var assembly = CecilAssemblyDefinition.ReadAssembly(before);
        var replaced = false;
        foreach (var method in AllMethods(assembly.MainModule.Types))
        {
            if (!method.HasBody)
                continue;
            foreach (var instruction in method.Body.Instructions)
            {
                if (instruction.OpCode == OpCodes.Ldstr && string.Equals(instruction.Operand as string, from, StringComparison.Ordinal))
                {
                    instruction.Operand = to;
                    replaced = true;
                }
            }
        }

        Assert.True(replaced, $"No ldstr {from} operand found to mutate.");
        assembly.Write(after);
    }

    private static IEnumerable<CecilMethodDefinition> AllMethods(IEnumerable<CecilTypeDefinition> types)
    {
        foreach (var type in types)
        foreach (var method in type.Methods)
            yield return method;
    }

    private static void CorruptMethodBody(string path, string methodName)
    {
        var bytes = File.ReadAllBytes(path);
        using var stream = new MemoryStream(bytes, writable: false);
        using var pe = new System.Reflection.PortableExecutable.PEReader(stream);
        var reader = pe.GetMetadataReader();
        foreach (var handle in reader.MethodDefinitions)
        {
            var method = reader.GetMethodDefinition(handle);
            if (reader.GetString(method.Name) != methodName || method.RelativeVirtualAddress == 0)
                continue;
            var bodyOffset = FileOffset(pe, method.RelativeVirtualAddress);
            var headerSize = (bytes[bodyOffset] & 0x03) == 0x02 ? 1 : 12;
            // 0xee is an unassigned single-byte opcode; any conforming reader
            // must fail closed instead of decoding past the corrupted stream.
            bytes[bodyOffset + headerSize] = 0xee;
            File.WriteAllBytes(path, bytes);
            return;
        }

        throw new InvalidOperationException($"{methodName} body not found.");
    }

    private static int FileOffset(System.Reflection.PortableExecutable.PEReader pe, int rva)
    {
        foreach (var section in pe.PEHeaders.SectionHeaders)
        {
            if (rva >= section.VirtualAddress && rva < section.VirtualAddress + Math.Max(section.VirtualSize, section.SizeOfRawData))
                return section.PointerToRawData + (rva - section.VirtualAddress);
        }

        throw new InvalidOperationException("RVA outside any section.");
    }
}
