using System.Globalization;

namespace TraceMap.Core;

/// <summary>
/// Bounded before/after IL rewrite identity evidence for explicitly declared
/// managed assembly pairs. The scanner never performs or attributes the
/// rewrite: both sides are operator-declared inputs that must independently
/// satisfy the dual-reader IL body contract before any join is attempted, and
/// an edge is emitted only when the complete exact assembly-scoped method
/// identity occurs exactly once on each side. Ambiguity, one-side-only
/// membership, assembly identity mismatch, reader disagreement, unsupported
/// shapes, and budget exhaustion fail closed to Tier4 gaps.
/// </summary>
internal static class IlRewriteEvidenceExtractor
{
    internal const string SchemaVersion = "il-rewrite-provenance.v1";
    internal const string PolicyVersion = "explicit-il-rewrite-evidence.v1";
    internal const string RewriteLocationKind = "managed-il-rewrite-v1";
    internal const string EdgeLimitation = "A rewrite edge proves only that the same complete method identity exists exactly once on each admitted side and that its canonical operand-aware body identity and module-local token are exactly as recorded; it does not prove semantic equivalence, behavior preservation, compilation provenance, source ownership, safe applicability, or that any other member of the pair survived rewriting.";
    internal const string RetargetLimitation = "A call-site retarget records the module-local reference tokens and complete member-reference identities of one ordinal-aligned call-family instruction on both sides; tokens are locations within their own module, never stable cross-build identities, and the retarget proves no dispatch, execution, or equivalence claim.";
    internal const string GapLimitation = "This categorical gap reduces only the explicitly requested before/after rewrite lane; it does not prove rewrite or membership absence and never alters source, compiled-metadata, PDB, or IL body/call evidence.";
    internal const int MembershipRetainedIdentityCount = 8;

    internal static IlRewriteEvaluation Evaluate(
        ScanOptions options,
        CancellationToken cancellationToken = default)
    {
        if (!options.IlRewriteEvidence)
            return IlRewriteEvaluation.Disabled;

        var limits = options.IlRewriteLimits ?? IlRewriteLimits.Default;
        if (limits.MaxPairCount <= 0)
            throw new ArgumentException("IL rewrite limits must be positive.");
        var bodyLimits = options.IlBodyLimits ?? new IlBodyLimits();
        var compiledLimits = options.CompiledInputLimits ?? new CompiledInputLimits();
        // The lane reuses both foreign limit sets, so it applies the identical
        // validation the owning extractors enforce before any provenance or
        // facts exist.
        IlBodyEvidenceExtractor.ValidateLimits(bodyLimits);
        ManagedMetadataExtractor.ValidateLimits(compiledLimits);
        var generatorSha256 = GeneratorSha256();
        var declaredBefore = options.IlRewriteBeforePaths ?? [];
        var declaredAfter = options.IlRewriteAfterPaths ?? [];
        // Blank slots are preserved, never dropped: filtering them would
        // silently re-pair later ordinals, so any blank slot in a declared
        // list invalidates the whole declaration.
        var hasBlankSlot = declaredBefore.Concat(declaredAfter).Any(value => string.IsNullOrWhiteSpace(value));
        var beforePaths = CleanOrderedPaths(declaredBefore);
        var afterPaths = CleanOrderedPaths(declaredAfter);
        var evaluated = new List<EvaluatedIlRewritePair>();
        if (declaredBefore.Count == 0 && declaredAfter.Count == 0)
        {
            evaluated.Add(SyntheticPairGap("rewrite-input-set", "IlRewritePairUnavailable"));
        }
        else if (hasBlankSlot || beforePaths.Count != afterPaths.Count)
        {
            evaluated.Add(SyntheticPairGap(
                "rewrite-input-set",
                "IlRewritePairDeclarationInvalid",
                detail: $"before={declaredBefore.Count.ToString(CultureInfo.InvariantCulture)},after={declaredAfter.Count.ToString(CultureInfo.InvariantCulture)},blankSlots={(hasBlankSlot ? "present" : "none")}",
                declarationSha256: ManagedMetadataExtractor.CanonicalDigest(new
                {
                    before = ProjectDeclaredSlots(options.RepoPath, declaredBefore, "rewrite-before", compiledLimits),
                    after = ProjectDeclaredSlots(options.RepoPath, declaredAfter, "rewrite-after", compiledLimits)
                })));
        }
        else
        {
            // Every declared ordinal is its own pair: identical (before,
            // after) tuples are not deduplicated, so repeated declarations
            // keep their own outcomes and the bounded-input digest stays
            // distinct from a single-declaration scan.
            var declared = new List<(string Before, string After)>();
            for (var index = 0; index < beforePaths.Count; index++)
                declared.Add((beforePaths[index], afterPaths[index]));

            var workBudget = new IlBodyEvidenceExtractor.IlWorkBudget(bodyLimits.MaxTotalWorkUnits);
            var metadataBudget = new IlBodyEvidenceExtractor.IlWorkBudget(compiledLimits.MaxTotalWorkUnits);
            var ordinal = 0;
            foreach (var (beforePath, afterPath) in declared.Take(limits.MaxPairCount))
            {
                cancellationToken.ThrowIfCancellationRequested();
                ordinal++;
                var pairId = $"rewrite-pair-{ordinal.ToString("D3", CultureInfo.InvariantCulture)}";
                evaluated.Add(EvaluatePair(
                    options.RepoPath, pairId, beforePath, afterPath, compiledLimits, bodyLimits, workBudget, metadataBudget, cancellationToken));
            }

            for (var index = limits.MaxPairCount; index < declared.Count; index++)
            {
                var overflow = declared[index];
                // The omitted pair's declared locators participate in the
                // outcome and therefore the bounded-input digest, so two
                // scans that omit different declared pairs never share a
                // rewrite provenance digest or scan identity.
                evaluated.Add(SyntheticPairGap(
                    $"rewrite-pair-{(index + 1).ToString("D3", CultureInfo.InvariantCulture)}",
                    "IlRewritePairCountLimitExceeded",
                    beforeSafeLocator: DeclaredLocator(options.RepoPath, overflow.Before, "rewrite-before", compiledLimits),
                    afterSafeLocator: DeclaredLocator(options.RepoPath, overflow.After, "rewrite-after", compiledLimits)));
            }
        }

        var outcomes = evaluated
            .Select(pair => pair.Outcome)
            .OrderBy(item => item.PairId, StringComparer.Ordinal)
            .ToArray();
        var expected = outcomes
            .Select(item => new IlRewriteExpectedPair(item.PairId, item.BeforeSafeLocator, item.AfterSafeLocator))
            .ToArray();
        var boundedInputSha256 = ManagedMetadataExtractor.CanonicalDigest(new
        {
            schemaVersion = SchemaVersion,
            policyVersion = PolicyVersion,
            generatorSha256,
            extractorIdentities = new[] { ScannerVersions.IlRewriteEvidenceExtractor, "system-reflection-metadata/10.0.0", "mono-cecil/0.11.6" },
            effectiveLimits = limits,
            effectiveBodyLimits = bodyLimits,
            effectiveCompiledLimits = compiledLimits,
            expectedPairs = expected,
            outcomes = outcomes.Select(item => new
            {
                item.PairId,
                item.BeforeSafeLocator,
                item.AfterSafeLocator,
                item.Outcome,
                item.BeforeRawFileSha256,
                item.AfterRawFileSha256,
                item.BeforeAssemblyIdentity,
                item.AfterAssemblyIdentity,
                item.PrivacyProjectedPairSha256,
                item.Side,
                item.Cause,
                item.GapKinds
            })
        });
        var coverage = outcomes.Length > 0 && outcomes.All(item => item.GapKinds.Count == 0)
            ? "rewrite-complete"
            : "rewrite-partial";
        var provenance = new IlRewriteProvenance(
            SchemaVersion,
            PolicyVersion,
            generatorSha256,
            [ScannerVersions.IlRewriteEvidenceExtractor, "system-reflection-metadata/10.0.0", "mono-cecil/0.11.6"],
            expected,
            limits,
            outcomes,
            boundedInputSha256,
            "local-only",
            coverage);
        var knownGaps = outcomes
            .SelectMany(item => item.GapKinds)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .Select(value => $"IL rewrite evidence coverage reduced: {value}.")
            .ToArray();
        return new IlRewriteEvaluation(provenance, evaluated, knownGaps);
    }

    private static EvaluatedIlRewritePair EvaluatePair(
        string repoPath,
        string pairId,
        string beforePath,
        string afterPath,
        CompiledInputLimits compiledLimits,
        IlBodyLimits bodyLimits,
        IlBodyEvidenceExtractor.IlWorkBudget workBudget,
        IlBodyEvidenceExtractor.IlWorkBudget metadataBudget,
        CancellationToken cancellationToken)
    {
        var before = AdmitSide(repoPath, beforePath, "rewrite-before", compiledLimits, cancellationToken);
        var after = AdmitSide(repoPath, afterPath, "rewrite-after", compiledLimits, cancellationToken);
        var gapKinds = new List<string>();
        // Each entry keeps the failing side, its gap kind, its cause, and that
        // side's own locator so every side-specific gap is evidenced on the
        // input that failed, never on the healthy opposite side.
        var sideFailures = new List<IlRewriteSideFailure>();
        if (before.Error is { } beforeError)
            sideFailures.Add(new IlRewriteSideFailure("before", "IlRewriteSideUnavailable", beforeError, before.Descriptor?.SafeLocator ?? before.FallbackSafeLocator!));
        if (after.Error is { } afterError)
            sideFailures.Add(new IlRewriteSideFailure("after", "IlRewriteSideUnavailable", afterError, after.Descriptor?.SafeLocator ?? after.FallbackSafeLocator!));
        gapKinds.AddRange(sideFailures.Select(failure => failure.GapKind));

        IlBodyEvidenceExtractor.IlReaderResult? beforeResult = ReadAdmittedSide(before.Bytes, compiledLimits, metadataBudget, bodyLimits, workBudget, cancellationToken, "before", before.Descriptor?.SafeLocator ?? before.FallbackSafeLocator!, sideFailures, gapKinds);
        IlBodyEvidenceExtractor.IlReaderResult? afterResult = ReadAdmittedSide(after.Bytes, compiledLimits, metadataBudget, bodyLimits, workBudget, cancellationToken, "after", after.Descriptor?.SafeLocator ?? after.FallbackSafeLocator!, sideFailures, gapKinds);

        var edges = new List<IlRewriteEdge>();
        var deltas = new List<IlRewriteMembershipDelta>();
        if (beforeResult is not null && afterResult is not null)
        {
            if (!string.Equals(beforeResult.AssemblyIdentity, afterResult.AssemblyIdentity, StringComparison.Ordinal))
            {
                gapKinds.Add("IlRewriteAssemblyIdentityMismatch");
            }
            else
            {
                var join = JoinPair(pairId, beforeResult, afterResult, workBudget);
                edges.AddRange(join.Edges);
                deltas.AddRange(join.Deltas);
                gapKinds.AddRange(join.GapKinds);
            }
        }

        var distinctGapKinds = gapKinds.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        // Outcomes never contain a raw path: valid declarations carry the
        // bounded safe locator and invalid ones a privacy-projected digest.
        var outcome = new IlRewritePairOutcome(
            pairId,
            before.Descriptor?.SafeLocator ?? before.FallbackSafeLocator!,
            after.Descriptor?.SafeLocator ?? after.FallbackSafeLocator!,
            OutcomeLabel(distinctGapKinds),
            before.RawFileSha256,
            after.RawFileSha256,
            beforeResult?.AssemblyIdentity,
            afterResult?.AssemblyIdentity,
            ManagedMetadataExtractor.CanonicalDigest(new
            {
                pairId,
                beforeSafeLocator = before.Descriptor?.SafeLocator,
                afterSafeLocator = after.Descriptor?.SafeLocator,
                outcome = distinctGapKinds.Length == 0 ? "admitted" : "gap",
                gapKinds = distinctGapKinds
            }),
            distinctGapKinds,
            sideFailures.Count == 0
                ? null
                : string.Join("+", sideFailures.Select(failure => failure.Side).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal)),
            sideFailures.Count == 0
                ? null
                : string.Join("+", sideFailures
                    .Select(failure => $"{failure.Side}:{failure.Cause}")
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)));
        // Side artifacts stay in memory only: they let the PDB sub-lane
        // re-verify and reuse the admitted bytes and dual-reader results
        // without re-running admission, and never enter provenance or facts.
        return new EvaluatedIlRewritePair(
            outcome,
            edges,
            deltas,
            sideFailures,
            before.Bytes is null || beforeResult is null ? null : new IlRewriteSideArtifact(before.Descriptor!.FullPath, before.RawFileSha256, beforeResult),
            after.Bytes is null || afterResult is null ? null : new IlRewriteSideArtifact(after.Descriptor!.FullPath, after.RawFileSha256, afterResult));
    }

    private static IlBodyEvidenceExtractor.IlReaderResult? ReadAdmittedSide(
        byte[]? bytes,
        CompiledInputLimits compiledLimits,
        IlBodyEvidenceExtractor.IlWorkBudget metadataBudget,
        IlBodyLimits bodyLimits,
        IlBodyEvidenceExtractor.IlWorkBudget budget,
        CancellationToken cancellationToken,
        string side,
        string sideLocator,
        List<IlRewriteSideFailure> sideFailures,
        List<string> gapKinds)
    {
        if (bytes is null)
            return null;
        try
        {
            // Unlike the standalone body lane, these inputs have not passed
            // compiled-metadata admission. Reject unsupported assembly shapes
            // and enforce metadata row/work bounds before either body reader.
            var metadataWork = ManagedMetadataExtractor.PreflightManagedInput(bytes, compiledLimits);
            if (!metadataBudget.TryConsume(metadataWork))
                throw new ManagedMetadataExtractor.ManagedInputException("limit-exhausted", "ManagedInputTotalWorkLimitExceeded");
            // The raw System.Reflection.Metadata reader runs first so every
            // bound (opcode table, operand extent, switch table, text limit)
            // is validated before Mono.Cecil materializes the same operands,
            // exactly as the standalone IL body lane requires.
            var srm = IlBodyEvidenceExtractor.ReadSystemReflectionMetadataBodies(bytes, bodyLimits, budget, cancellationToken);
            var cecil = IlBodyEvidenceExtractor.ReadCecilBodies(bytes, bodyLimits, budget, cancellationToken);
            if (IlBodyEvidenceExtractor.CompareBodies(cecil, srm).Count > 0)
            {
                gapKinds.Add("IlRewriteReaderDisagreement");
                sideFailures.Add(new IlRewriteSideFailure(side, "IlRewriteReaderDisagreement", "IlReaderDisagreement", sideLocator));
                return null;
            }

            return cecil;
        }
        catch (IlBodyEvidenceExtractor.IlEvidenceException exception)
        {
            gapKinds.Add(RewriteGapKind(exception.GapKind));
            sideFailures.Add(new IlRewriteSideFailure(side, RewriteGapKind(exception.GapKind), exception.GapKind, sideLocator));
        }
        catch (ManagedMetadataExtractor.ManagedInputException exception)
        {
            gapKinds.Add(RewriteGapKind(exception.GapKind));
            sideFailures.Add(new IlRewriteSideFailure(side, RewriteGapKind(exception.GapKind), exception.GapKind, sideLocator));
        }
        catch (BadImageFormatException)
        {
            gapKinds.Add("IlRewriteMalformedInput");
            sideFailures.Add(new IlRewriteSideFailure(side, "IlRewriteMalformedInput", "MalformedIlBody", sideLocator));
        }
        catch (Exception exception) when (exception is IOException or InvalidOperationException or ArgumentException or NotSupportedException or FormatException or OverflowException or IndexOutOfRangeException)
        {
            gapKinds.Add("IlRewriteMalformedInput");
            sideFailures.Add(new IlRewriteSideFailure(side, "IlRewriteMalformedInput", "MalformedIlBody", sideLocator));
        }

        return null;
    }

    private static string RewriteGapKind(string readingGapKind) => readingGapKind switch
    {
        "IlTotalWorkLimitExceeded" or "ManagedInputTotalWorkLimitExceeded" => "IlRewriteTotalWorkLimitExceeded",
        "ManagedInputTypeCountLimitExceeded" => "IlRewriteTypeCountLimitExceeded",
        "ManagedInputMemberCountLimitExceeded" => "IlRewriteMemberCountLimitExceeded",
        "ManagedInputTextLimitExceeded" => "IlRewriteTextLimitExceeded",
        "ManagedInputSignatureNestingLimitExceeded" => "IlRewriteSignatureNestingLimitExceeded",
        "IlBodyCountLimitExceeded" => "IlRewriteBodyCountLimitExceeded",
        "IlInstructionLimitExceeded" => "IlRewriteInstructionLimitExceeded",
        "IlLocalLimitExceeded" => "IlRewriteLocalLimitExceeded",
        "IlExceptionRegionLimitExceeded" => "IlRewriteExceptionRegionLimitExceeded",
        "IlTextLimitExceeded" => "IlRewriteTextLimitExceeded",
        "IlSignatureNestingLimitExceeded" => "IlRewriteSignatureNestingLimitExceeded",
        "MalformedIlBody" => "IlRewriteMalformedInput",
        "ManagedNetmoduleInputUnsupported" => "IlRewriteUnsupportedShape",
        "IlCallTargetIdentityUnavailable" or "IlOperandEncodingUnsupported" or "IlExceptionRegionKindUnsupported" => "IlRewriteUnsupportedShape",
        _ => "IlRewriteUnsupportedShape"
    };

    private sealed record SideAdmission(
        ManagedMetadataExtractor.InputDescriptor? Descriptor,
        byte[]? Bytes,
        string? RawFileSha256,
        string? Error,
        string? FallbackSafeLocator = null);

    private static string ProjectInvalidDeclarationLocator(string role, string path)
    {
        var projected = $"__external__/{role}/invalid-{ManagedMetadataExtractor.CanonicalDigest(new { role, path })[..12]}";
        return projected.Length > 256 ? projected[..256] : projected;
    }

    private static string DeclaredLocator(string repoPath, string path, string role, CompiledInputLimits limits)
    {
        try
        {
            return ManagedMetadataExtractor.CreateDescriptor(repoPath, path, role, limits).SafeLocator;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return ProjectInvalidDeclarationLocator(role, path);
        }
    }

    private static SideAdmission AdmitSide(
        string repoPath,
        string path,
        string role,
        CompiledInputLimits limits,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ManagedMetadataExtractor.InputDescriptor? descriptor;
        try
        {
            descriptor = ManagedMetadataExtractor.CreateDescriptor(repoPath, path, role, limits);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            // A malformed declared path must fail closed to a bounded side
            // gap with a privacy-projected locator, never abort the scan.
            return new SideAdmission(
                null,
                null,
                null,
                "IlRewriteSideDeclarationInvalid",
                ProjectInvalidDeclarationLocator(role, path));
        }

        if (descriptor.SafeLocatorTextLimitExceeded)
            return new SideAdmission(descriptor, null, null, "IlRewriteSideTextLimitExceeded");
        if (!File.Exists(descriptor.FullPath))
            return new SideAdmission(descriptor, null, null, "IlRewriteSideMissing");
        try
        {
            var length = new FileInfo(descriptor.FullPath).Length;
            if (length > limits.MaxFileSizeBytes)
                return new SideAdmission(descriptor, null, null, "IlRewriteSideFileSizeLimitExceeded");
            var bytes = ManagedMetadataExtractor.ReadBoundedFile(descriptor.FullPath, limits.MaxFileSizeBytes, "IlRewriteSideFileSizeLimitExceeded");
            var rawSha256 = ManagedMetadataExtractor.Sha256(bytes);
            var admitted = ManagedMetadataExtractor.FinalizeSafeLocator(descriptor, rawSha256, limits);
            return admitted.SafeLocatorTextLimitExceeded
                ? new SideAdmission(admitted, null, null, "IlRewriteSideTextLimitExceeded")
                : new SideAdmission(admitted, bytes, rawSha256, null);
        }
        catch (ManagedMetadataExtractor.ManagedInputException exception)
        {
            // The file may grow between the length precheck and bounded read.
            return new SideAdmission(descriptor, null, null, exception.GapKind);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return new SideAdmission(descriptor, null, null, "IlRewriteSideUnreadable");
        }
    }

    internal sealed record JoinResult(
        IReadOnlyList<IlRewriteEdge> Edges,
        IReadOnlyList<IlRewriteMembershipDelta> Deltas,
        IReadOnlyList<string> GapKinds,
        bool WorkExhausted);

    internal static JoinResult JoinPair(
        string pairId,
        IlBodyEvidenceExtractor.IlReaderResult before,
        IlBodyEvidenceExtractor.IlReaderResult after,
        IlBodyEvidenceExtractor.IlWorkBudget budget)
    {
        var edges = new List<IlRewriteEdge>();
        var beforeOnly = new List<string>();
        var afterOnly = new List<string>();
        var gapKinds = new List<string>();
        var workExhausted = false;
        var beforeByIdentity = GroupByIdentity(before.Bodies);
        var afterByIdentity = GroupByIdentity(after.Bodies);
        foreach (var identity in beforeByIdentity.Keys.Concat(afterByIdentity.Keys).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal))
        {
            if (!budget.TryConsume(1))
            {
                // Joining is atomic: a pair whose join work is exhausted
                // retains no partial edges, membership deltas, or gap kinds,
                // only the limit gap itself.
                return new JoinResult([], [], ["IlRewriteTotalWorkLimitExceeded"], WorkExhausted: true);
            }

            beforeByIdentity.TryGetValue(identity, out var beforeBodies);
            afterByIdentity.TryGetValue(identity, out var afterBodies);
            var beforeCount = beforeBodies?.Count ?? 0;
            var afterCount = afterBodies?.Count ?? 0;
            if (beforeCount > 1 || afterCount > 1)
            {
                gapKinds.Add("IlRewriteIdentityAmbiguous");
            }
            else if (beforeCount == 1 && afterCount == 1)
            {
                edges.Add(BuildEdge(pairId, identity, beforeBodies![0], afterBodies![0]));
            }
            else if (beforeCount == 1)
            {
                beforeOnly.Add(identity);
                gapKinds.Add("IlRewriteMethodBeforeOnly");
            }
            else if (afterCount == 1)
            {
                afterOnly.Add(identity);
                gapKinds.Add("IlRewriteMethodAfterOnly");
            }
        }

        var deltas = new List<IlRewriteMembershipDelta>();
        if (beforeOnly.Count > 0)
            deltas.Add(MembershipDelta(pairId, "before", beforeOnly));
        if (afterOnly.Count > 0)
            deltas.Add(MembershipDelta(pairId, "after", afterOnly));
        return new JoinResult(
            edges.OrderBy(edge => edge.MethodIdentity, StringComparer.Ordinal).ToArray(),
            deltas,
            gapKinds.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            workExhausted);
    }

    private static Dictionary<string, List<IlBodyObservation>> GroupByIdentity(IReadOnlyList<IlBodyObservation> bodies)
    {
        var groups = new Dictionary<string, List<IlBodyObservation>>(StringComparer.Ordinal);
        foreach (var body in bodies)
        {
            if (!groups.TryGetValue(body.MethodIdentity, out var list))
                groups[body.MethodIdentity] = list = [];
            list.Add(body);
        }

        return groups;
    }

    private static IlRewriteEdge BuildEdge(string pairId, string identity, IlBodyObservation before, IlBodyObservation after)
    {
        var unchanged = string.Equals(before.BodyIdentity, after.BodyIdentity, StringComparison.Ordinal);
        var opcodeSequencePreserved = before.InstructionCount == after.InstructionCount
            && string.Equals(before.OpcodesSha256, after.OpcodesSha256, StringComparison.Ordinal);
        // Non-instruction body components (locals, exception regions, max
        // stack, init-locals) can change while every opcode stays equal. A
        // changed body must never be labeled operand-only unless the complete
        // non-instruction structure also matches.
        var nonInstructionStructureEqual = before.LocalCount == after.LocalCount
            && string.Equals(before.LocalsSha256, after.LocalsSha256, StringComparison.Ordinal)
            && before.ExceptionRegionCount == after.ExceptionRegionCount
            && string.Equals(before.ExceptionRegionsSha256, after.ExceptionRegionsSha256, StringComparison.Ordinal)
            && string.Equals(before.MaxStack, after.MaxStack, StringComparison.Ordinal)
            && before.InitLocals == after.InitLocals;
        var instructionsEqual = string.Equals(before.InstructionsSha256, after.InstructionsSha256, StringComparison.Ordinal);
        string relationshipKind;
        if (unchanged)
            relationshipKind = "unchanged";
        else if (!opcodeSequencePreserved)
            relationshipKind = "instruction-stream-change";
        else if (!instructionsEqual && nonInstructionStructureEqual)
            relationshipKind = "operand-only-change";
        else if (instructionsEqual)
            relationshipKind = "body-structure-change";
        else
            relationshipKind = "operand-and-body-structure-change";
        // Equal opcode-name streams produce exactly one call observation per
        // call-family instruction in both readers, so the ordinal alignment
        // below is exact; any count mismatch means the streams were not equal
        // and no per-instruction claim is made.
        var callRetargets = !opcodeSequencePreserved || before.Calls.Count != after.Calls.Count
            ? []
            : before.Calls.Zip(after.Calls, (first, second) => (Before: first, After: second))
                .Select((pair, ordinal) => new IlRewriteCallRetarget(
                    ordinal,
                    pair.Before.Opcode,
                    pair.Before.Offset,
                    pair.After.Offset,
                    pair.Before.ReferenceToken,
                    pair.After.ReferenceToken,
                    pair.Before.TargetIdentity,
                    pair.After.TargetIdentity))
                .Where(retarget =>
                    !string.Equals(retarget.BeforeToken, retarget.AfterToken, StringComparison.Ordinal)
                    || !string.Equals(retarget.BeforeTargetIdentity, retarget.AfterTargetIdentity, StringComparison.Ordinal))
                .ToArray();
        return new IlRewriteEdge(
            pairId,
            identity,
            before.BodyIdentity,
            after.BodyIdentity,
            before.BodySha256,
            after.BodySha256,
            before.MetadataToken,
            after.MetadataToken,
            !string.Equals(before.MetadataToken, after.MetadataToken, StringComparison.Ordinal),
            relationshipKind,
            opcodeSequencePreserved,
            callRetargets);
    }

    internal static string EdgeIdentity(IlRewriteEdge edge) =>
        $"{edge.MethodIdentity}|il-rewrite:before:sha256:{edge.BeforeBodySha256}:after:sha256:{edge.AfterBodySha256}";

    private static IlRewriteMembershipDelta MembershipDelta(string pairId, string side, IReadOnlyList<string> identities) => new(
        pairId,
        side,
        identities.Count,
        identities.Take(MembershipRetainedIdentityCount).ToArray(),
        Math.Max(0, identities.Count - MembershipRetainedIdentityCount),
        // The commitment covers exactly the omitted suffix so consumers can
        // recompute it from identities beyond the retained prefix.
        identities.Count > MembershipRetainedIdentityCount
            ? ManagedMetadataExtractor.CanonicalDigest(identities.Skip(MembershipRetainedIdentityCount).ToArray())
            : null);

    internal static IReadOnlyList<CodeFact> MaterializeFacts(
        ScanManifest manifest,
        IlRewriteEvaluation evaluation,
        CancellationToken cancellationToken = default)
    {
        if (evaluation.Provenance is null)
            return [];
        var provenance = evaluation.Provenance;
        var facts = new List<CodeFact>();
        foreach (var pair in evaluation.Pairs.OrderBy(item => item.PairId, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var outcome = pair.Outcome;
            var common = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["evidenceLocationKind"] = RewriteLocationKind,
                ["ilRewriteBoundedInputSha256"] = provenance.BoundedInputSha256,
                ["ilRewriteGeneratorSha256"] = provenance.GeneratorSha256,
                ["ilRewriteCoverage"] = provenance.CoverageState,
                ["artifactVisibility"] = provenance.ArtifactVisibility,
                ["pairId"] = outcome.PairId,
                ["beforeSafeLocator"] = outcome.BeforeSafeLocator,
                ["afterSafeLocator"] = outcome.AfterSafeLocator
            };
            if (outcome.BeforeAssemblyIdentity is not null)
                common["beforeAssemblyIdentity"] = outcome.BeforeAssemblyIdentity;
            if (outcome.AfterAssemblyIdentity is not null)
                common["afterAssemblyIdentity"] = outcome.AfterAssemblyIdentity;
            if (outcome.BeforeRawFileSha256 is not null)
                common["beforeRawFileSha256"] = outcome.BeforeRawFileSha256;
            if (outcome.AfterRawFileSha256 is not null)
                common["afterRawFileSha256"] = outcome.AfterRawFileSha256;

            // Side-scoped failures emit one gap per failing side with that
            // side's own cause and locator; one-side-only membership kinds emit
            // dedicated bounded membership facts below. The pair-level loop
            // covers only pair-scoped kinds so nothing is duplicated.
            var sideFailureKinds = pair.SideFailures.Select(failure => failure.GapKind).ToHashSet(StringComparer.Ordinal);
            foreach (var failure in pair.SideFailures.OrderBy(item => item.Side, StringComparer.Ordinal).ThenBy(item => item.GapKind, StringComparer.Ordinal))
            {
                var properties = new Dictionary<string, string>(common)
                {
                    ["evidenceLocationKind"] = "managed-input-v1",
                    ["gapKind"] = failure.GapKind,
                    ["side"] = failure.Side,
                    ["cause"] = failure.Cause,
                    ["limitation"] = GapLimitation
                };
                properties.Remove("beforeAssemblyIdentity");
                properties.Remove("afterAssemblyIdentity");
                facts.Add(GapFact(manifest, failure.SideLocator, failure.GapKind, properties));
            }

            foreach (var gapKind in outcome.GapKinds.Where(kind =>
                         kind is not ("IlRewriteMethodBeforeOnly" or "IlRewriteMethodAfterOnly")
                         && !sideFailureKinds.Contains(kind)))
            {
                var properties = new Dictionary<string, string>(common)
                {
                    ["evidenceLocationKind"] = "managed-input-v1",
                    ["gapKind"] = gapKind,
                    ["limitation"] = GapLimitation
                };
                properties.Remove("beforeAssemblyIdentity");
                properties.Remove("afterAssemblyIdentity");
                facts.Add(GapFact(manifest, outcome.BeforeSafeLocator, gapKind, properties));
            }

            foreach (var delta in pair.MembershipDeltas.OrderBy(item => item.Side, StringComparer.Ordinal))
            {
                var properties = new Dictionary<string, string>(common)
                {
                    ["evidenceLocationKind"] = "managed-input-v1",
                    ["gapKind"] = delta.Side == "before" ? "IlRewriteMethodBeforeOnly" : "IlRewriteMethodAfterOnly",
                    ["side"] = delta.Side,
                    ["identityCount"] = delta.IdentityCount.ToString(CultureInfo.InvariantCulture),
                    ["omittedIdentityCount"] = delta.OmittedIdentityCount.ToString(CultureInfo.InvariantCulture),
                    ["limitation"] = GapLimitation
                };
                if (delta.OmittedIdentitySha256 is not null)
                    properties["omittedIdentitySha256"] = delta.OmittedIdentitySha256;
                var index = 0;
                foreach (var identity in delta.RetainedIdentities)
                    properties[$"identity[{index++}]"] = identity;
                properties.Remove("beforeAssemblyIdentity");
                properties.Remove("afterAssemblyIdentity");
                // A one-side-only membership gap is evidenced on the input
                // that carries the identity, not on the opposite side.
                var deltaLocator = delta.Side == "after" ? outcome.AfterSafeLocator : outcome.BeforeSafeLocator;
                facts.Add(GapFact(manifest, deltaLocator, $"membership:{delta.Side}", properties));
            }

            foreach (var edge in pair.Edges.OrderBy(item => item.MethodIdentity, StringComparer.Ordinal))
            {
                var edgeIdentity = EdgeIdentity(edge);
                var edgeFact = FactFactory.Create(
                    manifest,
                    FactTypes.ManagedIlRewriteObserved,
                    RuleIds.DotNetIlRewrite,
                    EvidenceTiers.Tier2Structural,
                    RewriteEvidence(outcome.BeforeSafeLocator),
                    targetSymbol: edgeIdentity,
                    contractElement: $"il-rewrite:{edge.RelationshipKind}",
                    properties: new Dictionary<string, string>(common)
                    {
                        ["methodIdentity"] = edge.MethodIdentity,
                        ["beforeBodyIdentity"] = edge.BeforeBodyIdentity,
                        ["afterBodyIdentity"] = edge.AfterBodyIdentity,
                        ["beforeIlBodySha256"] = edge.BeforeBodySha256,
                        ["afterIlBodySha256"] = edge.AfterBodySha256,
                        ["beforeMetadataToken"] = edge.BeforeMetadataToken,
                        ["afterMetadataToken"] = edge.AfterMetadataToken,
                        ["tokenRetargeted"] = edge.TokenRetargeted ? "true" : "false",
                        ["relationshipKind"] = edge.RelationshipKind,
                        ["opcodeSequencePreserved"] = edge.OpcodeSequencePreserved ? "true" : "false",
                        ["callRetargetCount"] = edge.CallRetargets.Count.ToString(CultureInfo.InvariantCulture),
                        ["limitation"] = EdgeLimitation
                    });
                facts.Add(edgeFact);

                foreach (var retarget in edge.CallRetargets.OrderBy(item => item.Ordinal, Comparer<int>.Default))
                {
                    facts.Add(FactFactory.Create(
                        manifest,
                        FactTypes.ManagedIlCallRetargetObserved,
                        RuleIds.DotNetIlRewrite,
                        EvidenceTiers.Tier2Structural,
                        RewriteEvidence(outcome.BeforeSafeLocator),
                        targetSymbol: $"{edgeIdentity}|call-retarget:{retarget.Ordinal.ToString(CultureInfo.InvariantCulture)}:{retarget.Opcode}",
                        contractElement: $"il-call-retarget:{retarget.Opcode}",
                        properties: new Dictionary<string, string>(common)
                        {
                            ["rewriteFactId"] = edgeFact.FactId,
                            ["callOrdinal"] = retarget.Ordinal.ToString(CultureInfo.InvariantCulture),
                            ["opcode"] = retarget.Opcode,
                            ["beforeIlOffset"] = retarget.BeforeOffset.ToString(CultureInfo.InvariantCulture),
                            ["afterIlOffset"] = retarget.AfterOffset.ToString(CultureInfo.InvariantCulture),
                            ["beforeToken"] = retarget.BeforeToken,
                            ["afterToken"] = retarget.AfterToken,
                            ["beforeTargetIdentity"] = retarget.BeforeTargetIdentity,
                            ["afterTargetIdentity"] = retarget.AfterTargetIdentity,
                            ["limitation"] = RetargetLimitation
                        }));
                }
            }
        }

        return facts;
    }

    private static CodeFact GapFact(ScanManifest manifest, string safeLocator, string contractElement, IReadOnlyDictionary<string, string> properties) =>
        FactFactory.Create(
            manifest,
            FactTypes.AnalysisGap,
            RuleIds.DotNetIlRewriteGap,
            EvidenceTiers.Tier4Unknown,
            new EvidenceSpan(safeLocator, 1, 1, null, nameof(IlRewriteEvidenceExtractor), ScannerVersions.IlRewriteEvidenceExtractor),
            contractElement: contractElement,
            properties: properties);

    private static EvidenceSpan RewriteEvidence(string safeLocator) => new(
        safeLocator,
        1,
        1,
        null,
        nameof(IlRewriteEvidenceExtractor),
        ScannerVersions.IlRewriteEvidenceExtractor);

    private static EvaluatedIlRewritePair SyntheticPairGap(
        string pairId,
        string gapKind,
        string? detail = null,
        string? beforeSafeLocator = null,
        string? afterSafeLocator = null,
        string? declarationSha256 = null) => new(
        new IlRewritePairOutcome(
            pairId,
            beforeSafeLocator ?? "none",
            afterSafeLocator ?? "none",
            GapOutcome(gapKind),
            null,
            null,
            null,
            null,
            ManagedMetadataExtractor.CanonicalDigest(new { pairId, outcome = "gap", gapKind, detail, declarationSha256 }),
            [gapKind],
            null,
            detail),
        [],
        [],
        []);

    private static string GapOutcome(string gapKind) => gapKind switch
    {
        "IlRewritePairUnavailable" => "missing",
        "IlRewritePairDeclarationInvalid" or "IlRewriteSideDeclarationInvalid" => "invalid",
        "IlRewriteSideUnavailable" => "unavailable",
        "IlRewriteMalformedInput" => "malformed",
        "IlRewriteReaderDisagreement" => "disputed",
        "IlRewriteAssemblyIdentityMismatch" => "mismatched",
        "IlRewriteIdentityAmbiguous" => "ambiguous",
        "IlRewriteUnsupportedShape" => "unsupported",
        "IlRewriteMethodBeforeOnly" or "IlRewriteMethodAfterOnly" => "membership-delta",
        _ => "limit-exhausted"
    };

    private static string OutcomeLabel(IReadOnlyList<string> gapKinds)
    {
        if (gapKinds.Count == 0)
            return "admitted";
        // A membership delta never mislabels the outcome when it is the only
        // gap; otherwise the strongest non-membership kind names it.
        var nonMembership = gapKinds.Where(kind => kind is not ("IlRewriteMethodBeforeOnly" or "IlRewriteMethodAfterOnly")).ToArray();
        return GapOutcome(nonMembership.Length > 0 ? nonMembership[0] : gapKinds[0]);
    }

    private static string?[] ProjectDeclaredSlots(string repoPath, IReadOnlyList<string> paths, string role, CompiledInputLimits limits) =>
        paths.Select(path => string.IsNullOrWhiteSpace(path) ? null : DeclaredLocator(repoPath, path.Trim(), role, limits)).ToArray();

    private static IReadOnlyList<string> CleanOrderedPaths(IReadOnlyList<string>? values) =>
        (values ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .ToArray();

    private static string GeneratorSha256()
    {
        var path = typeof(IlRewriteEvidenceExtractor).Assembly.Location;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            throw new InvalidOperationException("The exact IL rewrite evidence generator bytes are unavailable.");
        return ManagedMetadataExtractor.Sha256(File.ReadAllBytes(path));
    }
}
