using System.Globalization;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

namespace TraceMap.Core;

/// <summary>
/// Bounded before/after Portable PDB method and sequence-point identity
/// evidence for pairs already admitted by the IL rewrite lane. The scanner
/// never performs or attributes the rewrite: both PDB sides are
/// operator-declared inputs that must bind their own paired assembly through
/// the exact portable content GUID/stamp and PE CodeView entry, survive the
/// dual System.Reflection.Metadata/Mono.Cecil shape comparison, and carry
/// sequence-point offsets inside the dual-reader-proven body extents. Any
/// missing, malformed, unsupported, ambiguous, mismatched, row-inconsistent,
/// reader-disputed, or over-budget side withholds the whole pair behind a
/// Tier4 gap; only fully proven pairs emit per-method identity relationships
/// that classify the sequence-point IL offset vectors and never claim
/// behavioral equivalence, source ownership, or preserved debugging behavior.
/// </summary>
internal static class IlRewritePdbEvidenceExtractor
{
    internal const string SchemaVersion = "il-rewrite-pdb-provenance.v1";
    internal const string PolicyVersion = "explicit-il-rewrite-pdb-evidence.v1";
    internal const string PdbRewriteLocationKind = "managed-il-rewrite-pdb-v1";
    internal const string EdgeLimitation = "A rewrite PDB edge proves only that the two declared PDB artifacts contain these exact method and sequence-point identities for one method already joined by the IL rewrite contract, with each PDB bound to its own paired assembly by exact portable content identity; equal IL offset vectors do not prove behavioral equivalence, source ownership, compilation provenance, or preserved or correct debugging behavior.";
    internal const string GapLimitation = "This categorical gap reduces only the explicitly requested before/after rewrite PDB lane; it does not prove PDB, debug-information, or relationship absence and never alters source, compiled-metadata, PDB, IL body/call, or IL rewrite evidence.";
    internal const int MethodDebugRetainedIdentityCount = 8;

    internal static IlRewritePdbEvaluation Evaluate(
        ScanOptions options,
        IlRewriteEvaluation rewriteEvaluation,
        CancellationToken cancellationToken = default)
    {
        if (!options.IlRewritePdbEvidence)
            return IlRewritePdbEvaluation.Disabled;

        var limits = options.IlRewritePdbLimits ?? new IlRewritePdbLimits();
        ValidateLimits(limits);
        var compiledLimits = options.CompiledInputLimits ?? new CompiledInputLimits();
        var generatorSha256 = GeneratorSha256();
        var declaredBefore = options.IlRewriteBeforePdbPaths ?? [];
        var declaredAfter = options.IlRewriteAfterPdbPaths ?? [];
        var declaredPairSlots = options.IlRewriteBeforePaths ?? [];
        // Blank PDB slots are preserved, never dropped: filtering them would
        // silently re-pair later ordinals exactly like blank assembly slots.
        var hasBlankSlot = declaredBefore.Concat(declaredAfter).Any(value => string.IsNullOrWhiteSpace(value));
        var beforePaths = CleanOrderedPaths(declaredBefore);
        var afterPaths = CleanOrderedPaths(declaredAfter);
        var evaluated = new List<EvaluatedIlRewritePdbPair>();

        if (rewriteEvaluation.Provenance is null)
        {
            // The disabled-parent outcome still commits both ordered
            // projected slot lists, so two scans declaring different PDB
            // sets never share a provenance digest or scan identity even
            // though both fail identically for the missing parent lane.
            evaluated.Add(SyntheticPairGap(
                "rewrite-pdb-input-set",
                "IlRewritePdbRewritePairUnavailable",
                cause: "IlRewriteEvidenceDisabled",
                declarationSha256: ManagedMetadataExtractor.CanonicalDigest(new
                {
                    before = ProjectDeclaredSlots(options.RepoPath, declaredBefore, "rewrite-pdb-before", compiledLimits),
                    after = ProjectDeclaredSlots(options.RepoPath, declaredAfter, "rewrite-pdb-after", compiledLimits)
                })));
        }
        else if (declaredBefore.Count == 0 && declaredAfter.Count == 0)
        {
            evaluated.Add(SyntheticPairGap("rewrite-pdb-input-set", "IlRewritePdbPairUnavailable"));
        }
        else if (hasBlankSlot
            || beforePaths.Count != afterPaths.Count
            || beforePaths.Count != declaredPairSlots.Count)
        {
            // The rejected declaration still commits both ordered projected
            // slot lists so two scans rejecting different declared PDB sets
            // never share a provenance digest or scan identity.
            evaluated.Add(SyntheticPairGap(
                "rewrite-pdb-input-set",
                "IlRewritePdbDeclarationInvalid",
                detail: $"before={declaredBefore.Count.ToString(CultureInfo.InvariantCulture)},after={declaredAfter.Count.ToString(CultureInfo.InvariantCulture)},declaredPairs={declaredPairSlots.Count.ToString(CultureInfo.InvariantCulture)},blankSlots={(hasBlankSlot ? "present" : "none")}",
                declarationSha256: ManagedMetadataExtractor.CanonicalDigest(new
                {
                    before = ProjectDeclaredSlots(options.RepoPath, declaredBefore, "rewrite-pdb-before", compiledLimits),
                    after = ProjectDeclaredSlots(options.RepoPath, declaredAfter, "rewrite-pdb-after", compiledLimits)
                })));
        }
        else
        {
            var pairsById = rewriteEvaluation.Pairs.ToDictionary(pair => pair.PairId, StringComparer.Ordinal);
            var budget = new PortablePdbExtractor.PdbWorkBudget(limits.MaxTotalWorkUnits);
            for (var index = 0; index < beforePaths.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var pairId = $"rewrite-pair-{(index + 1).ToString("D3", CultureInfo.InvariantCulture)}";
                if (pairsById.TryGetValue(pairId, out var pair) && pair.BeforeSide is not null && pair.AfterSide is not null)
                {
                    if (pair.Edges.Count == 0 && pair.Outcome.GapKinds.Count > 0)
                    {
                        // No parent join exists to build on — atomic join
                        // exhaustion, assembly identity mismatch, or a fully
                        // one-sided membership outcome. Both sides may have
                        // been read, but a zero-relationship "admitted"
                        // outcome here would relabel a failed parent join as
                        // complete PDB coverage.
                        evaluated.Add(SyntheticPairGap(
                            pairId,
                            "IlRewritePdbRewritePairUnavailable",
                            beforeSafeLocator: DeclaredLocator(options.RepoPath, beforePaths[index], "rewrite-pdb-before", compiledLimits),
                            afterSafeLocator: DeclaredLocator(options.RepoPath, afterPaths[index], "rewrite-pdb-after", compiledLimits),
                            cause: string.Join("+", pair.Outcome.GapKinds)));
                        continue;
                    }

                    evaluated.Add(EvaluatePdbPair(
                        options.RepoPath,
                        pair,
                        beforePaths[index],
                        afterPaths[index],
                        limits,
                        compiledLimits,
                        budget,
                        cancellationToken));
                    continue;
                }

                // The assembly join itself is unavailable for this ordinal —
                // a failed side, an overflow pair beyond the rewrite-pair
                // limit, or an invalid whole-pair declaration — so no PDB
                // relationship can be proven on top of it. The declared PDB
                // locators still participate in the outcome and digest.
                var cause = pairsById.TryGetValue(pairId, out var failed)
                    ? string.Join("+", failed.Outcome.GapKinds)
                    : pairsById.TryGetValue("rewrite-input-set", out var synthetic)
                        ? string.Join("+", synthetic.Outcome.GapKinds)
                        : "IlRewritePairEvaluationMissing";
                evaluated.Add(SyntheticPairGap(
                    pairId,
                    "IlRewritePdbRewritePairUnavailable",
                    beforeSafeLocator: DeclaredLocator(options.RepoPath, beforePaths[index], "rewrite-pdb-before", compiledLimits),
                    afterSafeLocator: DeclaredLocator(options.RepoPath, afterPaths[index], "rewrite-pdb-after", compiledLimits),
                    cause: cause));
            }
        }

        var outcomes = evaluated
            .Select(pair => pair.Outcome)
            .OrderBy(item => item.PairId, StringComparer.Ordinal)
            .ToArray();
        var expected = outcomes
            .Select(item => new IlRewritePdbExpectedPair(item.PairId, item.BeforePdbSafeLocator, item.AfterPdbSafeLocator))
            .ToArray();
        var boundedInputSha256 = ManagedMetadataExtractor.CanonicalDigest(new
        {
            schemaVersion = SchemaVersion,
            policyVersion = PolicyVersion,
            generatorSha256,
            extractorIdentities = new[] { ScannerVersions.IlRewritePdbEvidenceExtractor, "system-reflection-metadata/10.0.0", "mono-cecil/0.11.6" },
            // The derived artifact commits the parent rewrite provenance and
            // both paired assembly hashes: the same PDBs reused against
            // differently rewritten assemblies must never share this digest.
            parentRewriteBoundedInputSha256 = rewriteEvaluation.Provenance?.BoundedInputSha256,
            parentPairAssemblySha256 = rewriteEvaluation.Pairs
                .OrderBy(item => item.PairId, StringComparer.Ordinal)
                .Select(item => new { item.PairId, item.Outcome.BeforeRawFileSha256, item.Outcome.AfterRawFileSha256 })
                .ToArray(),
            effectiveLimits = limits,
            expectedPairs = expected,
            outcomes = outcomes.Select(item => new
            {
                item.PairId,
                item.BeforePdbSafeLocator,
                item.AfterPdbSafeLocator,
                item.Outcome,
                item.BeforeRawFileSha256,
                item.AfterRawFileSha256,
                item.BeforePdbContentId,
                item.AfterPdbContentId,
                item.BeforeBindingState,
                item.AfterBindingState,
                item.PrivacyProjectedPairSha256,
                item.Side,
                item.Cause,
                item.GapKinds,
                item.JoinedMethodCount,
                item.PdbRelationshipCount,
                item.OffsetsUnchangedCount,
                item.OffsetsChangedCount,
                item.MethodDebugInformationAbsentCount,
                item.ConsumedWorkUnits
            })
        });
        var coverage = outcomes.Length > 0 && outcomes.All(item => item.GapKinds.Count == 0)
            ? "rewrite-pdb-complete"
            : "rewrite-pdb-partial";
        var provenance = new IlRewritePdbProvenance(
            SchemaVersion,
            PolicyVersion,
            generatorSha256,
            [ScannerVersions.IlRewritePdbEvidenceExtractor, "system-reflection-metadata/10.0.0", "mono-cecil/0.11.6"],
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
            .Select(value => $"IL rewrite PDB evidence coverage reduced: {value}.")
            .ToArray();
        return new IlRewritePdbEvaluation(provenance, evaluated, knownGaps);
    }

    private static EvaluatedIlRewritePdbPair EvaluatePdbPair(
        string repoPath,
        EvaluatedIlRewritePair pair,
        string beforePdbPath,
        string afterPdbPath,
        IlRewritePdbLimits limits,
        CompiledInputLimits compiledLimits,
        PortablePdbExtractor.PdbWorkBudget budget,
        CancellationToken cancellationToken)
    {
        var consumedBeforePair = budget.Consumed;
        var before = AdmitPdbSide(repoPath, beforePdbPath, "rewrite-pdb-before", limits, compiledLimits, cancellationToken);
        var after = AdmitPdbSide(repoPath, afterPdbPath, "rewrite-pdb-after", limits, compiledLimits, cancellationToken);
        var gapKinds = new List<string>();
        var sideFailures = new List<IlRewritePdbSideFailure>();
        if (before.Error is { } beforeError)
            sideFailures.Add(new IlRewritePdbSideFailure("before", AdmissionGapKind(beforeError), beforeError, before.Descriptor?.SafeLocator ?? before.FallbackSafeLocator!));
        if (after.Error is { } afterError)
            sideFailures.Add(new IlRewritePdbSideFailure("after", AdmissionGapKind(afterError), afterError, after.Descriptor?.SafeLocator ?? after.FallbackSafeLocator!));
        gapKinds.AddRange(sideFailures.Select(failure => failure.GapKind));

        PdbSideEvidence? beforeEvidence = null;
        PdbSideEvidence? afterEvidence = null;
        if (before.Bytes is not null && after.Bytes is not null)
        {
            // The paired assemblies were admitted under the compiled-input
            // bounds, so their re-verification reads use that same limit; the
            // rewrite-PDB file-size bound applies only to declared PDB inputs.
            beforeEvidence = BindAndReadPdbSide("before", before, pair.BeforeSide!, pair.Outcome.BeforeSafeLocator, limits, compiledLimits.MaxFileSizeBytes, budget, gapKinds, sideFailures, cancellationToken);
            afterEvidence = BindAndReadPdbSide("after", after, pair.AfterSide!, pair.Outcome.AfterSafeLocator, limits, compiledLimits.MaxFileSizeBytes, budget, gapKinds, sideFailures, cancellationToken);
        }

        var relationships = new List<IlRewritePdbRelationship>();
        var beforeOnlyDebug = new List<string>();
        var afterOnlyDebug = new List<string>();
        if (beforeEvidence is not null && afterEvidence is not null)
        {
            try
            {
                foreach (var edge in pair.Edges.OrderBy(item => item.MethodIdentity, StringComparer.Ordinal))
                {
                    budget.Consume("PdbInputTotalWorkLimitExceeded");
                    var beforeBody = pair.BeforeSide!.Reader!.Bodies.Single(body => string.Equals(body.MethodIdentity, edge.MethodIdentity, StringComparison.Ordinal));
                    var afterBody = pair.AfterSide!.Reader!.Bodies.Single(body => string.Equals(body.MethodIdentity, edge.MethodIdentity, StringComparison.Ordinal));
                    var beforeMethod = beforeEvidence.MethodsByToken.GetValueOrDefault(beforeBody.MetadataToken);
                    var afterMethod = afterEvidence.MethodsByToken.GetValueOrDefault(afterBody.MetadataToken);
                    if (beforeMethod is null && afterMethod is null)
                        continue;
                    if (beforeMethod is null || afterMethod is null)
                    {
                        // The identity's debug information exists on exactly one
                        // side: record it on the side that carries it.
                        (afterMethod is null ? beforeOnlyDebug : afterOnlyDebug).Add(edge.MethodIdentity);
                        continue;
                    }

                    relationships.Add(BuildRelationship(pair.PairId, edge, beforeBody, afterBody, beforeMethod, afterMethod));
                }
            }
            catch (PortablePdbExtractor.PdbInputException exception)
            {
                // Join exhaustion is atomic exactly like the parent rewrite
                // lane: no partial relationships or debug deltas survive, and
                // the pair keeps its bound PDB identities behind the limit gap.
                relationships.Clear();
                beforeOnlyDebug.Clear();
                afterOnlyDebug.Clear();
                gapKinds.Add(PdbGapKind(exception.Message));
            }

            if (beforeOnlyDebug.Count > 0)
                gapKinds.Add("IlRewritePdbMethodDebugInformationAbsent");
            if (afterOnlyDebug.Count > 0)
                gapKinds.Add("IlRewritePdbMethodDebugInformationAbsent");
        }

        var deltas = new List<IlRewritePdbMethodDebugDelta>();
        if (beforeOnlyDebug.Count > 0)
            deltas.Add(MethodDebugDelta(pair.PairId, "before", beforeOnlyDebug));
        if (afterOnlyDebug.Count > 0)
            deltas.Add(MethodDebugDelta(pair.PairId, "after", afterOnlyDebug));

        var distinctGapKinds = gapKinds.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        var outcome = new IlRewritePdbPairOutcome(
            pair.PairId,
            before.Descriptor?.SafeLocator ?? before.FallbackSafeLocator!,
            after.Descriptor?.SafeLocator ?? after.FallbackSafeLocator!,
            OutcomeLabel(distinctGapKinds),
            before.RawFileSha256,
            after.RawFileSha256,
            beforeEvidence?.ContentIdentity,
            afterEvidence?.ContentIdentity,
            beforeEvidence is null ? null : "bound",
            afterEvidence is null ? null : "bound",
            ManagedMetadataExtractor.CanonicalDigest(new
            {
                pairId = pair.PairId,
                beforeSafeLocator = before.Descriptor?.SafeLocator,
                afterSafeLocator = after.Descriptor?.SafeLocator,
                outcome = distinctGapKinds.Length == 0 ? "admitted" : "gap",
                beforePdbContentId = beforeEvidence?.ContentIdentity,
                afterPdbContentId = afterEvidence?.ContentIdentity,
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
                    .OrderBy(value => value, StringComparer.Ordinal)),
            pair.Edges.Count,
            relationships.Count,
            relationships.Count(relationship => relationship.SequencePointOffsetsUnchanged),
            relationships.Count(relationship => !relationship.SequencePointOffsetsUnchanged),
            beforeOnlyDebug.Count + afterOnlyDebug.Count,
            budget.Consumed - consumedBeforePair);
        return new EvaluatedIlRewritePdbPair(outcome, relationships, deltas, sideFailures);
    }

    /// <summary>
    /// Binds one declared PDB to its own paired assembly and reads the complete
    /// dual-reader method/sequence-point shape. Every failure is side-scoped
    /// and fails closed: no partial side evidence survives.
    /// </summary>
    private static PdbSideEvidence? BindAndReadPdbSide(
        string side,
        PdbSideAdmission admission,
        IlRewriteSideArtifact assemblySide,
        string assemblySafeLocator,
        IlRewritePdbLimits limits,
        long assemblyMaximumBytes,
        PortablePdbExtractor.PdbWorkBudget budget,
        List<string> gapKinds,
        List<IlRewritePdbSideFailure> sideFailures,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var bytes = admission.Bytes!;
        var pdbLocator = admission.Descriptor!.SafeLocator;
        try
        {
            // The matched assembly is re-read and re-hashed immediately before
            // use; only path and admitted digest were retained after the
            // rewrite join, and a changed file can never back PDB evidence.
            budget.Consume("PdbInputTotalWorkLimitExceeded");
            var verifiedBytes = PortablePdbExtractor.ReadVerifiedCompiledBytes(assemblySide.FullPath, assemblySide.RawFileSha256!, assemblyMaximumBytes, cancellationToken);
            if (verifiedBytes is null)
            {
                Fail("IlRewritePdbAssemblyArtifactChangedOrUnreadable", "IlRewritePdbAssemblyArtifactChangedOrUnreadable", assemblySafeLocator);
                return null;
            }

            using var provider = MetadataReaderProvider.FromPortablePdbStream(new MemoryStream(bytes, writable: false), MetadataStreamOptions.LeaveOpen);
            var reader = provider.GetMetadataReader();
            if (reader.DebugMetadataHeader is null)
            {
                Fail("IlRewritePdbUnsupportedShape", "PortablePdbContentIdUnavailable", pdbLocator);
                return null;
            }

            var contentId = new BlobContentId(reader.DebugMetadataHeader.Id);
            var contentIdentity = ContentIdentity(contentId.Guid, contentId.Stamp);
            using var pe = new PEReader(new MemoryStream(verifiedBytes, writable: false));
            var codeViewIdentities = new List<string>();
            foreach (var entry in pe.ReadDebugDirectory().Where(entry => entry.Type == DebugDirectoryEntryType.CodeView))
            {
                cancellationToken.ThrowIfCancellationRequested();
                budget.Consume("PdbInputTotalWorkLimitExceeded");
                var data = pe.ReadCodeViewDebugDirectoryData(entry);
                codeViewIdentities.Add(ContentIdentity(data.Guid, entry.Stamp));
            }

            var matches = PortablePdbExtractor.CountMatchingCodeViewEntries(codeViewIdentities, contentIdentity);
            if (matches == 0)
            {
                Fail("IlRewritePdbAssemblyBindingMismatch", "IlRewritePdbAssemblyBindingMismatch", pdbLocator);
                return null;
            }

            if (matches > 1)
            {
                Fail("IlRewritePdbAssemblyBindingAmbiguous", "IlRewritePdbAssemblyBindingAmbiguous", pdbLocator);
                return null;
            }

            var view = new PdbInputLimits(
                MaxFileSizeBytes: limits.MaxFileSizeBytes,
                MaxDocumentCount: limits.MaxDocumentCount,
                MaxMethodCount: limits.MaxMethodCount,
                MaxSequencePointCount: limits.MaxSequencePointCount,
                MaxTextLength: limits.MaxTextLength,
                MaxTotalWorkUnits: limits.MaxTotalWorkUnits);
            var observations = PortablePdbExtractor.ReadPortablePdb(reader, contentIdentity, view, budget, cancellationToken);
            var cecilShapes = PortablePdbExtractor.ReadCecilShapeCounts(verifiedBytes, bytes, budget, cancellationToken);
            if (!PortablePdbExtractor.ShapeCountsAgree(cecilShapes, PortablePdbExtractor.CanonicalShapes(observations.Documents, observations.Methods), budget, cancellationToken))
            {
                Fail("IlRewritePdbReaderDisagreement", "PdbReaderDisagreement", pdbLocator);
                return null;
            }

            var metadataReader = pe.GetMetadataReader();
            foreach (var method in observations.Methods)
            {
                cancellationToken.ThrowIfCancellationRequested();
                budget.Consume("PdbInputTotalWorkLimitExceeded");
                // Every PDB method row must correspond to a dual-reader-proven
                // body on its own side, and every sequence-point offset must
                // fall inside that body's IL extent.
                var body = assemblySide.Reader!.Bodies.FirstOrDefault(candidate => string.Equals(candidate.MetadataToken, method.MetadataToken, StringComparison.Ordinal));
                if (body is null)
                {
                    Fail("IlRewritePdbMethodRowInconsistent", "PdbMethodWithoutProvenBody", pdbLocator);
                    return null;
                }

                var row = ParseMethodDefRow(method.MetadataToken);
                var extent = GetMethodBodyIlSize(pe, metadataReader, row);
                if (extent is null || method.SequencePoints.Any(point => point.Offset < 0 || point.Offset >= extent))
                {
                    Fail("IlRewritePdbMethodRowInconsistent", "SequencePointOffsetBeyondBodyExtent", pdbLocator);
                    return null;
                }
            }

            return new PdbSideEvidence(
                contentIdentity,
                observations.Documents,
                observations.Methods,
                observations.Methods.ToDictionary(method => method.MetadataToken, StringComparer.Ordinal));
        }
        catch (PortablePdbExtractor.PdbInputException exception)
        {
            Fail(PdbGapKind(exception.Message), exception.Message, pdbLocator);
        }
        catch (Exception exception) when (exception is BadImageFormatException or IOException or InvalidDataException
            or InvalidOperationException or ArgumentException or NotSupportedException or FormatException or IndexOutOfRangeException)
        {
            Fail("IlRewritePdbSideUnavailable", "MalformedPortablePdb", pdbLocator);
        }

        return null;

        void Fail(string gapKind, string cause, string locator)
        {
            gapKinds.Add(gapKind);
            sideFailures.Add(new IlRewritePdbSideFailure(side, gapKind, cause, locator));
        }
    }

    private static IlRewritePdbRelationship BuildRelationship(
        string pairId,
        IlRewriteEdge edge,
        IlBodyObservation beforeBody,
        IlBodyObservation afterBody,
        PdbMethodObservation beforeMethod,
        PdbMethodObservation afterMethod)
    {
        // The classification compares the ordered IL offset vectors only.
        // Lines, columns, documents, and hidden flags are committed by the
        // per-side digests and never participate in the classification.
        var offsetsUnchanged = beforeMethod.SequencePoints.Count == afterMethod.SequencePoints.Count
            && beforeMethod.SequencePoints.Zip(afterMethod.SequencePoints, (first, second) => first.Offset == second.Offset).All(equal => equal);
        return new IlRewritePdbRelationship(
            pairId,
            edge.MethodIdentity,
            edge.RelationshipKind,
            beforeBody.MetadataToken,
            afterBody.MetadataToken,
            edge.BeforeBodyIdentity,
            edge.AfterBodyIdentity,
            edge.BeforeBodySha256,
            edge.AfterBodySha256,
            beforeMethod.Identity,
            afterMethod.Identity,
            beforeMethod.SequencePoints.Count,
            afterMethod.SequencePoints.Count,
            SequencePointsSha256(beforeMethod),
            SequencePointsSha256(afterMethod),
            offsetsUnchanged);
    }

    internal static string SequencePointsSha256(PdbMethodObservation method) =>
        ManagedMetadataExtractor.CanonicalDigest(method.SequencePoints
            .Select(point => new[]
            {
                point.Ordinal,
                point.Offset,
                point.DocumentRowId,
                point.Hidden ? 1 : 0,
                point.StartLine,
                point.StartColumn,
                point.EndLine,
                point.EndColumn
            })
            .ToArray());

    internal static IReadOnlyList<CodeFact> MaterializeFacts(
        ScanManifest manifest,
        IlRewritePdbEvaluation evaluation,
        IReadOnlyList<CodeFact> rewriteFacts,
        CancellationToken cancellationToken = default)
    {
        if (evaluation.Provenance is null)
            return [];
        var provenance = evaluation.Provenance;
        var rewriteEdgeFactIds = rewriteFacts
            .Where(fact => fact.FactType == FactTypes.ManagedIlRewriteObserved)
            .GroupBy(fact => (PairId: fact.Properties.GetValueOrDefault("pairId"), MethodIdentity: fact.Properties.GetValueOrDefault("methodIdentity")))
            .ToDictionary(group => group.Key, group => group.First().FactId);
        var facts = new List<CodeFact>();
        foreach (var pair in evaluation.Pairs.OrderBy(item => item.PairId, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var outcome = pair.Outcome;
            var common = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["evidenceLocationKind"] = PdbRewriteLocationKind,
                ["ilRewritePdbBoundedInputSha256"] = provenance.BoundedInputSha256,
                ["ilRewritePdbGeneratorSha256"] = provenance.GeneratorSha256,
                ["ilRewritePdbCoverageState"] = provenance.CoverageState,
                ["artifactVisibility"] = provenance.ArtifactVisibility,
                ["pairId"] = outcome.PairId,
                ["beforePdbSafeLocator"] = outcome.BeforePdbSafeLocator,
                ["afterPdbSafeLocator"] = outcome.AfterPdbSafeLocator
            };
            if (outcome.BeforePdbContentId is not null)
                common["beforePdbContentId"] = outcome.BeforePdbContentId;
            if (outcome.AfterPdbContentId is not null)
                common["afterPdbContentId"] = outcome.AfterPdbContentId;
            if (outcome.BeforeRawFileSha256 is not null)
                common["beforePdbRawFileSha256"] = outcome.BeforeRawFileSha256;
            if (outcome.AfterRawFileSha256 is not null)
                common["afterPdbRawFileSha256"] = outcome.AfterRawFileSha256;

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
                properties.Remove("beforePdbContentId");
                properties.Remove("afterPdbContentId");
                facts.Add(GapFact(manifest, failure.SideLocator, failure.GapKind, properties));
            }

            foreach (var gapKind in outcome.GapKinds.Where(kind => kind != "IlRewritePdbMethodDebugInformationAbsent" && !sideFailureKinds.Contains(kind)))
            {
                var properties = new Dictionary<string, string>(common)
                {
                    ["evidenceLocationKind"] = "managed-input-v1",
                    ["gapKind"] = gapKind,
                    ["limitation"] = GapLimitation
                };
                properties.Remove("beforePdbContentId");
                properties.Remove("afterPdbContentId");
                facts.Add(GapFact(manifest, outcome.BeforePdbSafeLocator, gapKind, properties));
            }

            foreach (var delta in pair.MethodDebugDeltas.OrderBy(item => item.Side, StringComparer.Ordinal))
            {
                var properties = new Dictionary<string, string>(common)
                {
                    ["evidenceLocationKind"] = "managed-input-v1",
                    ["gapKind"] = "IlRewritePdbMethodDebugInformationAbsent",
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
                properties.Remove("beforePdbContentId");
                properties.Remove("afterPdbContentId");
                // A one-side-only debug-information gap is evidenced on the
                // PDB input that carries the identity, not on the other side.
                var deltaLocator = delta.Side == "after" ? outcome.AfterPdbSafeLocator : outcome.BeforePdbSafeLocator;
                facts.Add(GapFact(manifest, deltaLocator, $"method-debug:{delta.Side}", properties));
            }

            foreach (var relationship in pair.Relationships.OrderBy(item => item.MethodIdentity, StringComparer.Ordinal))
            {
                var classification = relationship.SequencePointOffsetsUnchanged ? "sequence-point-offsets-unchanged" : "sequence-point-offsets-changed";
                facts.Add(FactFactory.Create(
                    manifest,
                    FactTypes.ManagedIlRewritePdbObserved,
                    RuleIds.DotNetIlRewritePdb,
                    EvidenceTiers.Tier2Structural,
                    PdbRewriteEvidence(outcome.BeforePdbSafeLocator),
                    targetSymbol: $"{relationship.MethodIdentity}|il-rewrite-pdb:before-pdb:sha256:{ManagedMetadataExtractor.Sha256(System.Text.Encoding.UTF8.GetBytes(relationship.BeforePdbMethodIdentity))}:after-pdb:sha256:{ManagedMetadataExtractor.Sha256(System.Text.Encoding.UTF8.GetBytes(relationship.AfterPdbMethodIdentity))}",
                    contractElement: $"il-rewrite-pdb:{classification}",
                    properties: new Dictionary<string, string>(common)
                    {
                        ["methodIdentity"] = relationship.MethodIdentity,
                        ["rewriteRelationshipKind"] = relationship.RewriteRelationshipKind,
                        ["rewriteFactId"] = rewriteEdgeFactIds.GetValueOrDefault((outcome.PairId, relationship.MethodIdentity)) ?? string.Empty,
                        ["beforeMetadataToken"] = relationship.BeforeMetadataToken,
                        ["afterMetadataToken"] = relationship.AfterMetadataToken,
                        ["beforeIlBodyIdentity"] = relationship.BeforeBodyIdentity,
                        ["afterIlBodyIdentity"] = relationship.AfterBodyIdentity,
                        ["beforeIlBodySha256"] = relationship.BeforeIlBodySha256,
                        ["afterIlBodySha256"] = relationship.AfterIlBodySha256,
                        ["beforePdbMethodIdentity"] = relationship.BeforePdbMethodIdentity,
                        ["afterPdbMethodIdentity"] = relationship.AfterPdbMethodIdentity,
                        ["beforeSequencePointCount"] = relationship.BeforeSequencePointCount.ToString(CultureInfo.InvariantCulture),
                        ["afterSequencePointCount"] = relationship.AfterSequencePointCount.ToString(CultureInfo.InvariantCulture),
                        ["beforeSequencePointsSha256"] = relationship.BeforeSequencePointsSha256,
                        ["afterSequencePointsSha256"] = relationship.AfterSequencePointsSha256,
                        ["sequencePointOffsets"] = classification,
                        ["limitation"] = EdgeLimitation
                    }));
            }
        }

        return facts;
    }

    private static CodeFact GapFact(ScanManifest manifest, string safeLocator, string contractElement, IReadOnlyDictionary<string, string> properties) =>
        FactFactory.Create(
            manifest,
            FactTypes.AnalysisGap,
            RuleIds.DotNetIlRewritePdbGap,
            EvidenceTiers.Tier4Unknown,
            new EvidenceSpan(safeLocator, 1, 1, null, nameof(IlRewritePdbEvidenceExtractor), ScannerVersions.IlRewritePdbEvidenceExtractor),
            contractElement: contractElement,
            properties: properties);

    private static EvidenceSpan PdbRewriteEvidence(string safeLocator) => new(
        safeLocator,
        1,
        1,
        null,
        nameof(IlRewritePdbEvidenceExtractor),
        ScannerVersions.IlRewritePdbEvidenceExtractor);

    private static IlRewritePdbMethodDebugDelta MethodDebugDelta(string pairId, string side, IReadOnlyList<string> identities) => new(
        pairId,
        side,
        identities.Count,
        identities.Take(MethodDebugRetainedIdentityCount).ToArray(),
        Math.Max(0, identities.Count - MethodDebugRetainedIdentityCount),
        identities.Count > MethodDebugRetainedIdentityCount
            ? ManagedMetadataExtractor.CanonicalDigest(identities.Skip(MethodDebugRetainedIdentityCount).ToArray())
            : null);

    private sealed record PdbSideAdmission(
        ManagedMetadataExtractor.InputDescriptor? Descriptor,
        byte[]? Bytes,
        string? RawFileSha256,
        string? Error,
        string? FallbackSafeLocator = null);

    private sealed record PdbSideEvidence(
        string ContentIdentity,
        IReadOnlyList<PdbDocumentObservation> Documents,
        IReadOnlyList<PdbMethodObservation> Methods,
        IReadOnlyDictionary<string, PdbMethodObservation> MethodsByToken);

    private static PdbSideAdmission AdmitPdbSide(
        string repoPath,
        string path,
        string role,
        IlRewritePdbLimits limits,
        CompiledInputLimits compiledLimits,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ManagedMetadataExtractor.InputDescriptor? descriptor;
        try
        {
            descriptor = ManagedMetadataExtractor.CreateDescriptor(repoPath, path, role, compiledLimits);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            // A malformed declared PDB path must fail closed to a bounded side
            // gap with a privacy-projected locator, never abort the scan.
            return new PdbSideAdmission(null, null, null, "IlRewritePdbSideDeclarationInvalid", ProjectInvalidDeclarationLocator(role, path));
        }

        if (descriptor.SafeLocatorTextLimitExceeded)
            return new PdbSideAdmission(descriptor, null, null, "IlRewritePdbSideTextLimitExceeded");
        if (!File.Exists(descriptor.FullPath))
            return new PdbSideAdmission(descriptor, null, null, "IlRewritePdbSideMissing");
        try
        {
            var length = new FileInfo(descriptor.FullPath).Length;
            if (length > limits.MaxFileSizeBytes)
                return new PdbSideAdmission(descriptor, null, null, "IlRewritePdbSideFileSizeLimitExceeded");
            var bytes = ManagedMetadataExtractor.ReadBoundedFile(descriptor.FullPath, limits.MaxFileSizeBytes, "IlRewritePdbSideFileSizeLimitExceeded");
            var rawSha256 = ManagedMetadataExtractor.Sha256(bytes);
            var admitted = ManagedMetadataExtractor.FinalizeSafeLocator(descriptor, rawSha256, compiledLimits);
            // Every branch that read the bytes retains the raw digest: a
            // rejected PDB replaced by different malformed or Windows PDB
            // bytes must still change the provenance digest and scan
            // identity, exactly like an admitted-input change would.
            if (admitted.SafeLocatorTextLimitExceeded)
                return new PdbSideAdmission(admitted, null, rawSha256, "IlRewritePdbSideTextLimitExceeded");
            if (!PortablePdbExtractor.IsPortablePdb(bytes))
            {
                return PortablePdbExtractor.IsWindowsPdb(bytes)
                    ? new PdbSideAdmission(admitted, null, rawSha256, OperatingSystem.IsWindows() ? "WindowsPdbIndependentReaderUnavailable" : "WindowsPdbRequiresWindows")
                    : new PdbSideAdmission(admitted, null, rawSha256, "IlRewritePdbSideMalformed");
            }

            return new PdbSideAdmission(admitted, bytes, rawSha256, null);
        }
        catch (ManagedMetadataExtractor.ManagedInputException)
        {
            // The file may grow between the length precheck and bounded read.
            return new PdbSideAdmission(descriptor, null, null, "IlRewritePdbSideFileSizeLimitExceeded");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return new PdbSideAdmission(descriptor, null, null, "IlRewritePdbSideUnreadable");
        }
    }

    private static string AdmissionGapKind(string cause) => cause switch
    {
        // A native Windows PDB is a categorical unsupported shape on every
        // host, not a generic unavailable side; limit and declaration causes
        // keep their specific kinds so consumers can distinguish an
        // adjustable limit or a bad declaration from an I/O failure.
        "WindowsPdbIndependentReaderUnavailable" or "WindowsPdbRequiresWindows" => "IlRewritePdbUnsupportedShape",
        "IlRewritePdbSideDeclarationInvalid" => "IlRewritePdbSideDeclarationInvalid",
        "IlRewritePdbSideTextLimitExceeded" => "IlRewritePdbTextLimitExceeded",
        "IlRewritePdbSideFileSizeLimitExceeded" => "IlRewritePdbSideFileSizeLimitExceeded",
        _ => "IlRewritePdbSideUnavailable"
    };

    private static string PdbGapKind(string cause) => cause switch
    {
        "PdbInputTotalWorkLimitExceeded" => "IlRewritePdbTotalWorkLimitExceeded",
        "PdbDocumentCountExceeded" => "IlRewritePdbDocumentCountExceeded",
        "PdbMethodCountExceeded" => "IlRewritePdbMethodCountExceeded",
        "PdbSequencePointCountExceeded" => "IlRewritePdbSequencePointCountExceeded",
        "PdbInputTextLimitExceeded" => "IlRewritePdbTextLimitExceeded",
        "PdbInputFileSizeExceeded" => "IlRewritePdbSideFileSizeLimitExceeded",
        _ => "IlRewritePdbSideUnavailable"
    };

    private static int ParseMethodDefRow(string metadataToken) =>
        int.TryParse(metadataToken.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var row) && row > 0
            ? row
            : throw new InvalidDataException("PDB method token is not a valid MethodDef token.");

    private static int? GetMethodBodyIlSize(PEReader pe, MetadataReader metadataReader, int row)
    {
        try
        {
            var definition = metadataReader.GetMethodDefinition(MetadataTokens.MethodDefinitionHandle(row));
            if (definition.RelativeVirtualAddress == 0)
                return null;
            return pe.GetMethodBody(definition.RelativeVirtualAddress).GetILReader().Length;
        }
        catch (Exception exception) when (exception is BadImageFormatException or IOException or InvalidOperationException or ArgumentException)
        {
            return null;
        }
    }

    private static string ContentIdentity(Guid guid, uint stamp) => $"{guid:D}:{stamp:x8}";

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

    private static EvaluatedIlRewritePdbPair SyntheticPairGap(
        string pairId,
        string gapKind,
        string? detail = null,
        string? beforeSafeLocator = null,
        string? afterSafeLocator = null,
        string? cause = null,
        string? declarationSha256 = null) => new(
        new IlRewritePdbPairOutcome(
            pairId,
            beforeSafeLocator ?? "none",
            afterSafeLocator ?? "none",
            GapOutcome(gapKind),
            null,
            null,
            null,
            null,
            null,
            null,
            ManagedMetadataExtractor.CanonicalDigest(new { pairId, outcome = "gap", gapKind, detail, cause, declarationSha256 }),
            [gapKind],
            null,
            detail is null ? cause : $"{detail}{(cause is null ? string.Empty : $";cause={cause}")}"),
        [],
        [],
        []);

    internal static string GapOutcome(string gapKind) => gapKind switch
    {
        "IlRewritePdbPairUnavailable" => "missing",
        "IlRewritePdbDeclarationInvalid" or "IlRewritePdbSideDeclarationInvalid" => "invalid",
        "IlRewritePdbSideUnavailable" => "unavailable",
        "IlRewritePdbReaderDisagreement" => "disputed",
        "IlRewritePdbAssemblyBindingMismatch" => "mismatched",
        "IlRewritePdbAssemblyBindingAmbiguous" => "ambiguous",
        "IlRewritePdbAssemblyArtifactChangedOrUnreadable" => "unbound",
        "IlRewritePdbUnsupportedShape" => "unsupported",
        "IlRewritePdbMethodRowInconsistent" => "inconsistent",
        "IlRewritePdbMethodDebugInformationAbsent" => "method-debug-delta",
        "IlRewritePdbRewritePairUnavailable" => "rewrite-unavailable",
        _ => "limit-exhausted"
    };

    private static string OutcomeLabel(IReadOnlyList<string> gapKinds)
    {
        if (gapKinds.Count == 0)
            return "admitted";
        // A one-side-only debug-information delta never mislabels the outcome
        // when it is the only gap; otherwise the strongest kind names it.
        var nonMembership = gapKinds.Where(kind => kind != "IlRewritePdbMethodDebugInformationAbsent").ToArray();
        return GapOutcome(nonMembership.Length > 0 ? nonMembership[0] : gapKinds[0]);
    }

    private static string?[] ProjectDeclaredSlots(string repoPath, IReadOnlyList<string> paths, string role, CompiledInputLimits limits) =>
        paths.Select(path => string.IsNullOrWhiteSpace(path) ? null : DeclaredLocator(repoPath, path.Trim(), role, limits)).ToArray();

    private static IReadOnlyList<string> CleanOrderedPaths(IReadOnlyList<string>? values) =>
        (values ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .ToArray();

    private static void ValidateLimits(IlRewritePdbLimits limits)
    {
        if (limits.MaxFileSizeBytes <= 0
            || limits.MaxDocumentCount <= 0
            || limits.MaxMethodCount <= 0
            || limits.MaxSequencePointCount <= 0
            || limits.MaxTextLength <= 0
            || limits.MaxTotalWorkUnits <= 0)
            throw new ArgumentException("IL rewrite PDB limits must all be positive.");
        if (limits.MaxTextLength < PortablePdbExtractor.MinimumProjectedTextLength)
            throw new ArgumentException($"IL rewrite PDB maximum text length must be at least {PortablePdbExtractor.MinimumProjectedTextLength} characters.");
    }

    private static string GeneratorSha256()
    {
        var path = typeof(IlRewritePdbEvidenceExtractor).Assembly.Location;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            throw new InvalidOperationException("The exact IL rewrite PDB evidence generator bytes are unavailable.");
        return ManagedMetadataExtractor.Sha256(File.ReadAllBytes(path));
    }
}
