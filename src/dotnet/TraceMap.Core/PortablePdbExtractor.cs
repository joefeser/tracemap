using System.Collections.Immutable;
using System.Globalization;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Mono.Cecil;
using Mono.Cecil.Cil;
using CecilTypeDefinition = Mono.Cecil.TypeDefinition;
using CecilModuleDefinition = Mono.Cecil.ModuleDefinition;

namespace TraceMap.Core;

internal static class PortablePdbExtractor
{
    internal const int MinimumProjectedTextLength = 71;
    internal const string SchemaVersion = "pdb-input-provenance.v1";
    internal const string SummarySchemaVersion = "pdb-evidence-summary.v1";
    internal const string PolicyVersion = "explicit-pdb-input.v1";
    internal const string PdbLocationKind = "portable-pdb-v1";
    internal const string Limitation = "PDB evidence proves only bounded compiler-produced debug metadata bound to one admitted assembly; it does not prove execution, reachability, behavior, source semantics, IL calls, or rewrite preservation.";
    internal const string GapLimitation = "This categorical gap reduces only the explicitly bounded PDB lane; it does not prove absence and never alters source-derived evidence.";
    internal const int MaximumArtifactCount = 32;

    private static readonly Guid Sha1DocumentHashAlgorithm = new("ff1816ec-aa5e-4d10-87f7-6f4963833460");
    private static readonly Guid Sha256DocumentHashAlgorithm = new("8829d00f-11b8-4213-878b-770e8597ac16");
    private static readonly Guid FSharpLanguage = new("ab4f38c9-b6e6-43ba-be3b-58080b2ccce3");
    private static readonly byte[] WindowsPdbSignature = "Microsoft C/C++ MSF 7.00\r\n\u001aDS\0\0\0"u8.ToArray();

    internal static PdbInputEvaluation Evaluate(
        string repoPath,
        ScanOptions options,
        CompiledInputEvaluation compiledEvaluation,
        CancellationToken cancellationToken = default)
    {
        var paths = CleanPaths(options.PdbInputPaths);
        if (paths.Count == 0)
            return PdbInputEvaluation.Disabled;

        var limits = options.PdbInputLimits ?? new PdbInputLimits();
        ValidateLimits(limits);
        var generatorSha256 = GeneratorSha256();
        var expected = paths.Take(limits.MaxArtifactCount)
            .Select(path => new PdbExpectedInput(BoundedSafeLocator(repoPath, ResolvePath(repoPath, path), limits.MaxTextLength)))
            .ToArray();
        var omitted = paths.Skip(limits.MaxArtifactCount)
            .Select(path => new PdbExpectedInput(BoundedSafeLocator(repoPath, ResolvePath(repoPath, path), limits.MaxTextLength)))
            .ToArray();
        var omittedDigest = omitted.Length == 0 ? null : CanonicalDigest(omitted);
        var compiledBindings = ReadCompiledBindings(
            repoPath,
            options,
            compiledEvaluation,
            options.CompiledInputLimits?.MaxFileSizeBytes ?? new CompiledInputLimits().MaxFileSizeBytes);
        var evaluated = new List<EvaluatedPdbInput>();
        var workBudget = new PdbWorkBudget(limits.MaxTotalWorkUnits);

        foreach (var path in paths.Take(limits.MaxArtifactCount))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fullPath = ResolvePath(repoPath, path);
            var rawSafeLocator = SafeLocator(repoPath, fullPath);
            var safeLocator = ProjectBoundedText(rawSafeLocator, limits.MaxTextLength);
            if (!string.Equals(rawSafeLocator, safeLocator, StringComparison.Ordinal))
            {
                evaluated.Add(Gap(safeLocator, "limit-exhausted", "unknown", "PdbInputTextLimitExceeded"));
                continue;
            }
            if (!File.Exists(fullPath))
            {
                evaluated.Add(Gap(safeLocator, "missing", "unknown", "MissingPdbInput"));
                continue;
            }

            byte[] bytes;
            try
            {
                bytes = ReadBoundedFile(fullPath, limits.MaxFileSizeBytes);
            }
            catch (PdbInputException exception)
            {
                evaluated.Add(Gap(safeLocator, "limit-exhausted", "unknown", exception.Message));
                continue;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                evaluated.Add(Gap(safeLocator, "unreadable", "unknown", "UnreadablePdbInput"));
                continue;
            }

            var rawSha256 = Sha256(bytes);
            if (!IsPortablePdb(bytes))
            {
                evaluated.Add(IsWindowsPdb(bytes)
                    ? Gap(
                        safeLocator,
                        OperatingSystem.IsWindows() ? "unsupported" : "unsupported-platform",
                        "windows-pdb",
                        OperatingSystem.IsWindows() ? "WindowsPdbIndependentReaderUnavailable" : "WindowsPdbRequiresWindows",
                        rawSha256)
                    : Gap(safeLocator, "malformed", "unknown", "MalformedPdbInput", rawSha256));
                continue;
            }

            try
            {
                using var provider = MetadataReaderProvider.FromPortablePdbStream(new MemoryStream(bytes, writable: false), MetadataStreamOptions.LeaveOpen);
                var reader = provider.GetMetadataReader();
                if (reader.DebugMetadataHeader is null)
                {
                    evaluated.Add(Gap(safeLocator, "malformed", "portable", "PortablePdbContentIdUnavailable", rawSha256));
                    continue;
                }
                var contentId = new BlobContentId(reader.DebugMetadataHeader.Id);
                var contentIdentity = ContentIdentity(contentId.Guid, contentId.Stamp);
                var matches = compiledBindings.Where(binding => binding.CodeViewIdentities.Contains(contentIdentity, StringComparer.Ordinal)).ToArray();
                if (matches.Length == 0)
                {
                    evaluated.Add(Gap(safeLocator, "mismatched", "portable", "PdbAssemblyIdentityMismatch", rawSha256, contentIdentity));
                    continue;
                }
                if (matches.Length > 1)
                {
                    evaluated.Add(Gap(safeLocator, "ambiguous", "portable", "AmbiguousPdbAssemblyMatch", rawSha256, contentIdentity));
                    continue;
                }
                var match = matches[0];
                if (!string.Equals(match.ProvenanceState, "bound", StringComparison.Ordinal)
                    || !string.Equals(match.Outcome, "admitted", StringComparison.Ordinal))
                {
                    evaluated.Add(Gap(safeLocator, "unbound", "portable", "PdbCompiledEvidenceUnacceptable", rawSha256, contentIdentity, match));
                    continue;
                }

                var observations = ReadPortablePdb(reader, contentIdentity, limits, workBudget);
                var cecilShapes = ReadCecilShapes(match.Bytes, bytes, workBudget);
                var srmShapes = CanonicalShapes(observations.Documents, observations.Methods);
                if (!cecilShapes.SequenceEqual(srmShapes, StringComparer.Ordinal))
                {
                    evaluated.Add(Gap(safeLocator, "disputed", "portable", "PdbReaderDisagreement", rawSha256, contentIdentity, match));
                    continue;
                }

                var outcome = new PdbInputOutcome(
                    safeLocator,
                    "admitted",
                    "portable",
                    rawSha256,
                    PrivacyProjectedDigest(safeLocator, rawSha256, contentIdentity, match.SafeLocator, "bound", []),
                    contentIdentity,
                    match.SafeLocator,
                    match.AssemblyIdentity,
                    match.ProvenanceBindingInputSha256,
                    "bound",
                    []);
                evaluated.Add(new EvaluatedPdbInput(outcome, observations.Documents, observations.Methods));
            }
            catch (PdbInputException exception)
            {
                evaluated.Add(Gap(safeLocator, "limit-exhausted", "portable", exception.Message, rawSha256));
            }
            catch (Exception exception) when (IsRecoverable(exception))
            {
                evaluated.Add(Gap(safeLocator, "malformed", "portable", "MalformedPortablePdb", rawSha256));
            }
        }

        if (omitted.Length > 0)
            evaluated.Add(Gap("pdb-input-set", "limit-exhausted", "unknown", "PdbArtifactCountExceeded"));

        var outcomes = evaluated.Select(item => item.Outcome)
            .OrderBy(item => item.SafeLocator, StringComparer.Ordinal)
            .ThenBy(item => item.Outcome, StringComparer.Ordinal)
            .ToArray();
        var boundedInputSha256 = CanonicalDigest(new
        {
            schemaVersion = SchemaVersion,
            policyVersion = PolicyVersion,
            generatorSha256,
            expectedInputs = expected,
            effectiveLimits = limits,
            outcomes = outcomes.Select(item => new
            {
                item.SafeLocator,
                item.Outcome,
                item.Format,
                item.RawFileSha256,
                item.PrivacyProjectedInputSha256,
                item.PdbContentId,
                item.MatchedAssemblySafeLocator,
                item.MatchedAssemblyIdentity,
                item.ProvenanceBindingInputSha256,
                item.BindingState,
                item.GapKinds
            }),
            omittedInputCount = omitted.Length,
            omittedInputSha256 = omittedDigest ?? string.Empty
        });
        var coverage = outcomes.Length > 0 && outcomes.All(item => item.Outcome == "admitted" && item.GapKinds.Count == 0)
            ? "pdb-complete"
            : "pdb-partial";
        var provenance = new PdbInputProvenance(
            SchemaVersion,
            PolicyVersion,
            generatorSha256,
            [ScannerVersions.PortablePdbExtractor, "system-reflection-metadata/10.0.0", "mono-cecil/0.11.6"],
            expected,
            limits,
            outcomes,
            boundedInputSha256,
            "local-only",
            coverage,
            omitted.Length,
            omittedDigest);
        var gaps = outcomes.SelectMany(item => item.GapKinds)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .Select(value => $"PDB coverage reduced: {value}.")
            .ToArray();
        return new PdbInputEvaluation(provenance, evaluated, gaps, workBudget.Consumed);
    }

    internal static IReadOnlyList<CodeFact> MaterializeFacts(
        string repoPath,
        ScanManifest manifest,
        PdbInputEvaluation evaluation,
        IReadOnlyList<CodeFact> compiledFacts,
        IReadOnlyList<FileInventoryItem> inventory)
    {
        if (evaluation.Provenance is null)
            return [];
        var sourceIndex = BuildSourceChecksumIndex(repoPath, inventory, evaluation.Inputs, evaluation.Provenance.EffectiveLimits, evaluation.ConsumedWorkUnits);
        var plans = evaluation.Inputs.Select(input =>
        {
            var sourceMatches = MatchSourceDocuments(sourceIndex, input.Documents);
            var metadataCandidates = input.Methods.ToDictionary(
                method => method.MethodRowId,
                method => compiledFacts.Where(fact =>
                        fact.FactType == FactTypes.ManagedMethodDeclared
                        && string.Equals(fact.Evidence.FilePath, input.Outcome.MatchedAssemblySafeLocator, StringComparison.Ordinal)
                        && string.Equals(fact.Properties.GetValueOrDefault("metadataToken"), method.MetadataToken, StringComparison.Ordinal)
                        && string.Equals(fact.Properties.GetValueOrDefault("sourceReconciliationEligibility"), "eligible", StringComparison.Ordinal))
                    .ToArray());
            return new PdbMaterializationPlan(input, sourceMatches, metadataCandidates);
        }).ToArray();
        var reconciliationIncomplete = plans.Any(plan => plan.Input.Outcome.Outcome == "admitted"
            && (plan.SourceMatches.Values.Any(match => match.Candidates.Count != 1)
                || plan.MetadataCandidates.Values.Any(candidates => candidates.Length != 1)));
        var provenance = evaluation.Provenance with
        {
            CoverageState = evaluation.Provenance.CoverageState == "pdb-complete" && !reconciliationIncomplete
                ? "pdb-complete"
                : "pdb-partial"
        };
        var facts = new List<CodeFact>();
        foreach (var plan in plans.OrderBy(item => item.Input.Outcome.SafeLocator, StringComparer.Ordinal))
        {
            var input = plan.Input;
            var outcome = input.Outcome;
            var common = CommonProperties(provenance, outcome);
            if (outcome.Outcome != "admitted")
            {
                foreach (var gapKind in outcome.GapKinds.OrderBy(value => value, StringComparer.Ordinal))
                    facts.Add(GapFact(manifest, outcome.SafeLocator, gapKind, common));
                continue;
            }

            var inputFact = FactFactory.Create(
                manifest,
                FactTypes.PdbInputAdmitted,
                RuleIds.DotNetPdbInput,
                EvidenceTiers.Tier2Structural,
                PdbEvidence(outcome.SafeLocator),
                targetSymbol: outcome.PdbContentId,
                contractElement: outcome.Format,
                properties: Merge(common, new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["limitation"] = Limitation
                }));
            facts.Add(inputFact);

            var sourceMatches = plan.SourceMatches;
            var documentFacts = new Dictionary<int, CodeFact>();
            foreach (var document in input.Documents.OrderBy(item => item.RowId))
            {
                var documentFact = FactFactory.Create(
                    manifest,
                    FactTypes.PdbDocumentDeclared,
                    RuleIds.DotNetPdbIdentity,
                    EvidenceTiers.Tier2Structural,
                    PdbEvidence(outcome.SafeLocator),
                    targetSymbol: document.Identity,
                    contractElement: "document",
                    properties: Merge(common, new SortedDictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["documentRowId"] = document.RowId.ToString(CultureInfo.InvariantCulture),
                        ["documentNameSha256"] = document.NameHash,
                        ["checksumAlgorithm"] = document.HashAlgorithm,
                        ["checksum"] = document.Checksum,
                        ["language"] = document.Language,
                        ["pdbInputFactId"] = inputFact.FactId,
                        ["limitation"] = Limitation
                    }));
                facts.Add(documentFact);
                documentFacts[document.RowId] = documentFact;

                var match = sourceMatches.GetValueOrDefault(document.RowId) ?? SourceDocumentMatch.Zero;
                if (match.Candidates.Count == 1)
                {
                    var sourcePath = match.Candidates[0];
                    facts.Add(FactFactory.Create(
                        manifest,
                        FactTypes.PdbSourceDocumentReconciled,
                        RuleIds.DotNetPdbIdentity,
                        EvidenceTiers.Tier2Structural,
                        new EvidenceSpan(sourcePath, 1, 1, null, nameof(PortablePdbExtractor), ScannerVersions.PortablePdbExtractor),
                        sourceSymbol: document.Identity,
                        targetSymbol: $"source-file:{ManagedMetadataExtractor.EncodeIdentityComponent(sourcePath)}",
                        contractElement: "exact-document-checksum",
                        properties: Merge(common, new SortedDictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["candidateCount"] = "1",
                            ["sourcePath"] = sourcePath,
                            ["pdbInputFactId"] = inputFact.FactId,
                            ["pdbDocumentFactId"] = documentFact.FactId,
                            ["limitation"] = "An exact document checksum edge proves byte correspondence for this scan only; it does not prove statement execution or semantic ownership."
                        })));
                }
                else
                {
                    facts.Add(GapFact(manifest, outcome.SafeLocator,
                        match.GapKind ?? (match.Candidates.Count == 0 ? "PdbSourceDocumentZeroCandidate" : "PdbSourceDocumentMultipleCandidates"),
                        Merge(common, new SortedDictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["pdbDocumentIdentity"] = document.Identity,
                            ["pdbInputFactId"] = inputFact.FactId,
                            ["pdbDocumentFactId"] = documentFact.FactId,
                            ["candidateCount"] = match.Candidates.Count.ToString(CultureInfo.InvariantCulture)
                        })));
                }
            }

            foreach (var method in input.Methods.OrderBy(item => item.MethodRowId))
            {
                var methodFact = FactFactory.Create(
                    manifest,
                    FactTypes.PdbMethodDeclared,
                    RuleIds.DotNetPdbIdentity,
                    EvidenceTiers.Tier2Structural,
                    PdbEvidence(outcome.SafeLocator),
                    targetSymbol: method.Identity,
                    contractElement: "method-debug-information",
                    properties: Merge(common, new SortedDictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["metadataToken"] = method.MetadataToken,
                        ["sequencePointCount"] = method.SequencePoints.Count.ToString(CultureInfo.InvariantCulture),
                        ["pdbInputFactId"] = inputFact.FactId,
                        ["limitation"] = Limitation
                    }));
                facts.Add(methodFact);

                var metadataCandidates = plan.MetadataCandidates[method.MethodRowId];
                if (metadataCandidates.Length == 1)
                {
                    var metadata = metadataCandidates[0];
                    var reconciliationFact = FactFactory.Create(
                        manifest,
                        FactTypes.MetadataPdbMethodReconciled,
                        RuleIds.DotNetPdbIdentity,
                        EvidenceTiers.Tier2Structural,
                        PdbEvidence(outcome.SafeLocator),
                        sourceSymbol: metadata.TargetSymbol,
                        targetSymbol: method.Identity,
                        contractElement: "exact-bound-method-row",
                        properties: Merge(common, new SortedDictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["compiledFactId"] = metadata.FactId,
                            ["metadataIdentity"] = metadata.TargetSymbol ?? string.Empty,
                            ["pdbInputFactId"] = inputFact.FactId,
                            ["pdbMethodFactId"] = methodFact.FactId,
                            ["pdbMethodIdentity"] = method.Identity,
                            ["limitation"] = Limitation
                        }));
                    facts.Add(reconciliationFact);

                    foreach (var point in method.SequencePoints.OrderBy(item => item.Ordinal))
                        AddSequencePointFact(facts, manifest, outcome, common, input, sourceMatches, documentFacts, methodFact, reconciliationFact, point);
                }
                else
                {
                    facts.Add(GapFact(manifest, outcome.SafeLocator,
                        metadataCandidates.Length == 0 ? "PdbMetadataMethodZeroCandidate" : "PdbMetadataMethodMultipleCandidates",
                        Merge(common, new SortedDictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["metadataToken"] = method.MetadataToken,
                            ["pdbInputFactId"] = inputFact.FactId,
                            ["pdbMethodFactId"] = methodFact.FactId,
                            ["pdbMethodIdentity"] = method.Identity,
                            ["candidateCount"] = metadataCandidates.Length.ToString(CultureInfo.InvariantCulture)
                        })));
                }
            }
        }
        return facts;
    }

    internal static PdbEvidenceSummary? BuildSummary(
        ScanManifest manifest,
        IReadOnlyList<CodeFact> facts,
        int maximumEntries = 256)
    {
        if (manifest.PdbInputProvenance is not { } provenance)
            return null;
        if (maximumEntries < 0)
            throw new ArgumentOutOfRangeException(nameof(maximumEntries));
        var pdbFacts = facts.Where(fact => fact.RuleId is
                RuleIds.DotNetPdbInput or RuleIds.DotNetPdbIdentity or RuleIds.DotNetPdbSequencePoint or RuleIds.DotNetPdbGap)
            .ToArray();
        var allEntries = pdbFacts
            .Where(fact => fact.FactType is FactTypes.MetadataPdbMethodReconciled
                or FactTypes.PdbSourceDocumentReconciled or FactTypes.PdbSequencePointDeclared)
            .OrderBy(fact => fact.FactType, StringComparer.Ordinal)
            .ThenBy(fact => fact.FactId, StringComparer.Ordinal)
            .Select(fact => new PdbEvidenceSummaryEntry(
                fact.FactType,
                fact.SourceSymbol ?? string.Empty,
                fact.TargetSymbol ?? string.Empty,
                fact.FactId,
                SupportingFactIds(fact),
                fact.RuleId,
                fact.EvidenceTier,
                fact.Evidence.ExtractorVersion,
                fact.Properties.GetValueOrDefault("pdbProvenanceState") ?? string.Empty,
                fact.Properties.GetValueOrDefault("provenanceBindingInputSha256") ?? string.Empty,
                fact.Evidence.FilePath,
                fact.Evidence.StartLine,
                fact.Evidence.EndLine,
                fact.CommitSha,
                fact.Properties.GetValueOrDefault("limitation") ?? Limitation))
            .ToArray();
        var retained = allEntries.Take(maximumEntries).ToArray();
        var omitted = allEntries.Skip(maximumEntries).ToArray();
        var coverage = pdbFacts.Any(fact => fact.FactType == FactTypes.AnalysisGap)
            ? "pdb-partial"
            : provenance.CoverageState;
        var summaryInputSha256 = CanonicalDigest(new
        {
            provenance.BoundedInputSha256,
            manifest.SourceSnapshotDigest,
            manifest.CommitSha,
            factIds = pdbFacts.Select(fact => fact.FactId).Order(StringComparer.Ordinal).ToArray()
        });
        return new PdbEvidenceSummary(
            SummarySchemaVersion,
            coverage,
            summaryInputSha256,
            provenance.GeneratorSha256,
            pdbFacts.Count(fact => fact.FactType == FactTypes.PdbDocumentDeclared),
            pdbFacts.Count(fact => fact.FactType == FactTypes.PdbMethodDeclared),
            pdbFacts.Count(fact => fact.FactType == FactTypes.PdbSequencePointDeclared),
            pdbFacts.Count(fact => fact.FactType == FactTypes.MetadataPdbMethodReconciled),
            pdbFacts.Count(fact => fact.FactType == FactTypes.PdbSourceDocumentReconciled),
            pdbFacts.Count(fact => fact.FactType == FactTypes.AnalysisGap),
            retained,
            omitted.Length,
            omitted.Length == 0 ? null : CanonicalDigest(omitted));
    }

    internal static ScanManifest FinalizeManifest(ScanManifest manifest, IReadOnlyList<CodeFact> facts)
    {
        if (manifest.PdbInputProvenance is not { } provenance)
            return manifest;
        var gapKinds = facts.Where(fact => fact.RuleId == RuleIds.DotNetPdbGap && fact.FactType == FactTypes.AnalysisGap)
            .Select(fact => fact.Properties.GetValueOrDefault("gapKind"))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var coverage = gapKinds.Length == 0 && provenance.CoverageState == "pdb-complete"
            ? "pdb-complete"
            : "pdb-partial";
        return manifest with
        {
            PdbInputProvenance = provenance with { CoverageState = coverage },
            KnownGaps = manifest.KnownGaps
                .Concat(gapKinds.Select(value => $"PDB coverage reduced: {value}."))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray()
        };
    }

    private static IReadOnlyList<string> SupportingFactIds(CodeFact fact)
    {
        var keys = new[]
        {
            "pdbInputFactId", "pdbDocumentFactId", "pdbMethodFactId",
            "compiledFactId", "metadataPdbReconciliationFactId"
        };
        return keys.Select(key => fact.Properties.GetValueOrDefault(key))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static void AddSequencePointFact(
        ICollection<CodeFact> facts,
        ScanManifest manifest,
        PdbInputOutcome outcome,
        IReadOnlyDictionary<string, string> common,
        EvaluatedPdbInput input,
        IReadOnlyDictionary<int, SourceDocumentMatch> sourceMatches,
        IReadOnlyDictionary<int, CodeFact> documentFacts,
        CodeFact methodFact,
        CodeFact? reconciliationFact,
        PdbSequencePointObservation point)
    {
        var document = input.Documents.Single(item => item.RowId == point.DocumentRowId);
        var match = sourceMatches.GetValueOrDefault(document.RowId) ?? SourceDocumentMatch.Zero;
        var evidence = match.Candidates.Count == 1 && !point.Hidden
            ? new EvidenceSpan(match.Candidates[0], Math.Max(1, point.StartLine), Math.Max(1, point.EndLine), null, nameof(PortablePdbExtractor), ScannerVersions.PortablePdbExtractor)
            : PdbEvidence(outcome.SafeLocator);
        facts.Add(FactFactory.Create(
            manifest,
            FactTypes.PdbSequencePointDeclared,
            RuleIds.DotNetPdbSequencePoint,
            EvidenceTiers.Tier2Structural,
            evidence,
            sourceSymbol: methodFact.TargetSymbol,
            targetSymbol: document.Identity,
            contractElement: point.Identity,
            properties: Merge(common, new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["pdbMethodFactId"] = methodFact.FactId,
                ["pdbDocumentFactId"] = documentFacts[document.RowId].FactId,
                ["metadataPdbReconciliationFactId"] = reconciliationFact?.FactId ?? string.Empty,
                ["ordinal"] = point.Ordinal.ToString(CultureInfo.InvariantCulture),
                ["ilOffset"] = point.Offset.ToString(CultureInfo.InvariantCulture),
                ["hidden"] = point.Hidden.ToString().ToLowerInvariant(),
                ["startLine"] = point.StartLine.ToString(CultureInfo.InvariantCulture),
                ["startColumn"] = point.StartColumn.ToString(CultureInfo.InvariantCulture),
                ["endLine"] = point.EndLine.ToString(CultureInfo.InvariantCulture),
                ["endColumn"] = point.EndColumn.ToString(CultureInfo.InvariantCulture),
                ["limitation"] = Limitation
            })));
    }

    private static (IReadOnlyList<PdbDocumentObservation> Documents, IReadOnlyList<PdbMethodObservation> Methods) ReadPortablePdb(
        MetadataReader reader,
        string contentIdentity,
        PdbInputLimits limits,
        PdbWorkBudget workBudget)
    {
        if (reader.Documents.Count > limits.MaxDocumentCount)
            throw new PdbInputException("PdbDocumentCountExceeded");
        var documents = new List<PdbDocumentObservation>();
        foreach (var handle in reader.Documents)
        {
            workBudget.Consume("PdbInputTotalWorkLimitExceeded");
            var document = reader.GetDocument(handle);
            var rowId = MetadataTokens.GetRowNumber(handle);
            var algorithm = document.HashAlgorithm.IsNil ? Guid.Empty : reader.GetGuid(document.HashAlgorithm);
            var checksum = document.Hash.IsNil ? string.Empty : Convert.ToHexString(reader.GetBlobBytes(document.Hash)).ToLowerInvariant();
            var language = document.Language.IsNil ? Guid.Empty : reader.GetGuid(document.Language);
            var name = reader.GetString(document.Name);
            var identity = $"pdb:format:portable|id:{contentIdentity}|document:{rowId.ToString(CultureInfo.InvariantCulture)}|hashAlgorithm:{algorithm:D}|checksum:{checksum}|language:{language:D}";
            if (identity.Length > limits.MaxTextLength || checksum.Length > limits.MaxTextLength)
                throw new PdbInputException("PdbInputTextLimitExceeded");
            documents.Add(new PdbDocumentObservation(rowId, identity, Sha256(Encoding.UTF8.GetBytes(name)), algorithm.ToString("D"), checksum, language.ToString("D")));
        }

        var methods = new List<PdbMethodObservation>();
        var sequencePointCount = 0;
        var documentRows = documents.Select(item => item.RowId).ToHashSet();
        for (var rowId = 1; rowId <= reader.MethodDebugInformation.Count; rowId++)
        {
            var handle = MetadataTokens.MethodDebugInformationHandle(rowId);
            var information = reader.GetMethodDebugInformation(handle);
            var methodIdentity = $"pdb:format:portable|id:{contentIdentity}|method:{rowId.ToString(CultureInfo.InvariantCulture)}";
            var observations = new List<PdbSequencePointObservation>();
            var currentDocument = information.Document;
            var ordinal = 0;
            foreach (var point in information.GetSequencePoints())
            {
                if (observations.Count == 0)
                {
                    if (methods.Count >= limits.MaxMethodCount)
                        throw new PdbInputException("PdbMethodCountExceeded");
                    workBudget.Consume("PdbInputTotalWorkLimitExceeded");
                }
                if (++sequencePointCount > limits.MaxSequencePointCount)
                    throw new PdbInputException("PdbSequencePointCountExceeded");
                workBudget.Consume("PdbInputTotalWorkLimitExceeded");
                if (!point.Document.IsNil)
                    currentDocument = point.Document;
                if (currentDocument.IsNil)
                    throw new InvalidDataException("Portable PDB sequence point has no document.");
                var documentRow = MetadataTokens.GetRowNumber(currentDocument);
                if (!documentRows.Contains(documentRow))
                    throw new InvalidDataException("Portable PDB sequence point references an undeclared document row.");
                var hidden = point.IsHidden;
                var identity = $"{methodIdentity}|sequence:{ordinal.ToString(CultureInfo.InvariantCulture)}|offset:{point.Offset.ToString(CultureInfo.InvariantCulture)}|document:{documentRow.ToString(CultureInfo.InvariantCulture)}|hidden:{hidden.ToString().ToLowerInvariant()}|range:{point.StartLine.ToString(CultureInfo.InvariantCulture)}:{point.StartColumn.ToString(CultureInfo.InvariantCulture)}-{point.EndLine.ToString(CultureInfo.InvariantCulture)}:{point.EndColumn.ToString(CultureInfo.InvariantCulture)}";
                if (identity.Length > limits.MaxTextLength)
                    throw new PdbInputException("PdbInputTextLimitExceeded");
                observations.Add(new PdbSequencePointObservation(ordinal, point.Offset, documentRow, hidden, point.StartLine, point.StartColumn, point.EndLine, point.EndColumn, identity));
                ordinal++;
            }
            if (observations.Count == 0)
                continue;
            methods.Add(new PdbMethodObservation(
                rowId,
                $"0x{(0x06000000u | (uint)rowId):x8}",
                methodIdentity,
                observations));
        }
        return (documents, methods);
    }

    private static IReadOnlyList<string> ReadCecilShapes(byte[] peBytes, byte[] pdbBytes, PdbWorkBudget workBudget)
    {
        using var peStream = new MemoryStream(peBytes, writable: false);
        using var pdbStream = new MemoryStream(pdbBytes, writable: false);
        using var resolver = new RejectingAssemblyResolver();
        using var module = CecilModuleDefinition.ReadModule(peStream, new ReaderParameters
        {
            InMemory = true,
            ReadingMode = ReadingMode.Deferred,
            ReadSymbols = true,
            SymbolReaderProvider = new PortablePdbReaderProvider(),
            SymbolStream = pdbStream,
            AssemblyResolver = resolver
        });
        var shapes = new List<string>();
        foreach (var method in AllTypes(module.Types).SelectMany(type => type.Methods).Where(method => method.DebugInformation.HasSequencePoints))
        {
            workBudget.Consume("PdbInputTotalWorkLimitExceeded");
            var ordinal = 0;
            foreach (var point in method.DebugInformation.SequencePoints)
            {
                workBudget.Consume("PdbInputTotalWorkLimitExceeded");
                var checksum = point.Document.Hash is { Length: > 0 } ? Convert.ToHexString(point.Document.Hash).ToLowerInvariant() : string.Empty;
                shapes.Add(Shape(
                    $"0x{method.MetadataToken.ToUInt32():x8}",
                    ordinal++,
                    point.Offset,
                    checksum,
                    point.IsHidden,
                    point.StartLine,
                    point.StartColumn,
                    point.EndLine,
                    point.EndColumn));
            }
        }
        return shapes.Order(StringComparer.Ordinal).ToArray();
    }

    internal static IReadOnlyList<string> CanonicalShapes(
        IReadOnlyList<PdbDocumentObservation> documents,
        IReadOnlyList<PdbMethodObservation> methods)
    {
        var byRow = documents.ToDictionary(item => item.RowId);
        var shapes = new List<string>();
        foreach (var method in methods)
        foreach (var point in method.SequencePoints)
        {
            if (!byRow.TryGetValue(point.DocumentRowId, out var document))
                throw new InvalidDataException("Portable PDB sequence point references an undeclared document row.");
            shapes.Add(Shape(
                method.MetadataToken,
                point.Ordinal,
                point.Offset,
                document.Checksum,
                point.Hidden,
                point.StartLine,
                point.StartColumn,
                point.EndLine,
                point.EndColumn));
        }
        return shapes.Order(StringComparer.Ordinal).ToArray();
    }

    private static string Shape(string token, int ordinal, int offset, string checksum, bool hidden, int startLine, int startColumn, int endLine, int endColumn) =>
        $"{token}|{ordinal.ToString(CultureInfo.InvariantCulture)}|{offset.ToString(CultureInfo.InvariantCulture)}|{checksum}|{hidden.ToString().ToLowerInvariant()}|{startLine.ToString(CultureInfo.InvariantCulture)}:{startColumn.ToString(CultureInfo.InvariantCulture)}-{endLine.ToString(CultureInfo.InvariantCulture)}:{endColumn.ToString(CultureInfo.InvariantCulture)}";

    private static IEnumerable<CecilTypeDefinition> AllTypes(IEnumerable<CecilTypeDefinition> roots)
    {
        foreach (var type in roots)
        {
            yield return type;
            foreach (var nested in AllTypes(type.NestedTypes))
                yield return nested;
        }
    }

    private static IReadOnlyDictionary<int, SourceDocumentMatch> MatchSourceDocuments(
        SourceChecksumIndex index,
        IReadOnlyList<PdbDocumentObservation> documents)
    {
        var result = new Dictionary<int, SourceDocumentMatch>();
        foreach (var document in documents)
        {
            if (Guid.Parse(document.Language) == FSharpLanguage)
            {
                result[document.RowId] = new SourceDocumentMatch([], "PdbSourceReconciliationUnsupportedLanguage");
                continue;
            }
            var algorithm = Guid.Parse(document.HashAlgorithm);
            if (algorithm != Sha1DocumentHashAlgorithm && algorithm != Sha256DocumentHashAlgorithm)
            {
                result[document.RowId] = new SourceDocumentMatch([], "PdbDocumentChecksumAlgorithmUnsupported");
                continue;
            }
            if (index.GapKind is not null)
            {
                result[document.RowId] = new SourceDocumentMatch([], index.GapKind);
                continue;
            }
            var ordered = index.Candidates.GetValueOrDefault((algorithm, document.Checksum)) ?? [];
            result[document.RowId] = new SourceDocumentMatch(
                ordered,
                ordered.Count == 0 ? "PdbSourceDocumentZeroCandidate"
                    : ordered.Count > 1 ? "PdbSourceDocumentMultipleCandidates" : null);
        }
        return result;
    }

    private static SourceChecksumIndex BuildSourceChecksumIndex(
        string repoPath,
        IReadOnlyList<FileInventoryItem> inventory,
        IReadOnlyList<EvaluatedPdbInput> inputs,
        PdbInputLimits limits,
        long consumedWorkUnits)
    {
        var algorithms = inputs.Where(input => input.Outcome.Outcome == "admitted")
            .SelectMany(input => input.Documents)
            .Where(document => Guid.Parse(document.Language) != FSharpLanguage)
            .Select(document => Guid.Parse(document.HashAlgorithm))
            .Where(algorithm => algorithm == Sha1DocumentHashAlgorithm || algorithm == Sha256DocumentHashAlgorithm)
            .Distinct()
            .OrderBy(value => value)
            .ToArray();
        if (algorithms.Length == 0)
            return SourceChecksumIndex.Empty;

        var sources = inventory.Where(item => item.Kind is "CSharp" or "VisualBasic" or "FSharp")
            .OrderBy(item => item.RelativePath, StringComparer.Ordinal)
            .ToArray();
        if (sources.Length > limits.MaxSourceFileCount)
            return SourceChecksumIndex.Gap("PdbSourceFileCountExceeded");
        long totalBytes = 0;
        foreach (var source in sources)
        {
            if (source.SizeBytes > limits.MaxSourceFileSizeBytes)
                return SourceChecksumIndex.Gap("PdbSourceFileSizeExceeded");
            try
            {
                totalBytes = checked(totalBytes + source.SizeBytes);
            }
            catch (OverflowException)
            {
                return SourceChecksumIndex.Gap("PdbSourceTotalBytesExceeded");
            }
            if (totalBytes > limits.MaxSourceTotalBytes)
                return SourceChecksumIndex.Gap("PdbSourceTotalBytesExceeded");
        }
        var requiredWork = checked((long)sources.Length * algorithms.Length);
        if (requiredWork > limits.MaxTotalWorkUnits - consumedWorkUnits)
            return SourceChecksumIndex.Gap("PdbSourceReconciliationWorkLimitExceeded");

        var candidates = new Dictionary<(Guid Algorithm, string Checksum), List<string>>();
        var buffer = new byte[81_920];
        long actualTotalBytes = 0;
        foreach (var source in sources)
        {
            var path = Path.Combine(repoPath, source.RelativePath.Replace('/', Path.DirectorySeparatorChar));
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, buffer.Length, FileOptions.SequentialScan);
                if (stream.Length > limits.MaxSourceFileSizeBytes)
                    return SourceChecksumIndex.Gap("PdbSourceFileSizeExceeded");
                using var sha1 = algorithms.Contains(Sha1DocumentHashAlgorithm) ? IncrementalHash.CreateHash(HashAlgorithmName.SHA1) : null;
                using var sha256 = algorithms.Contains(Sha256DocumentHashAlgorithm) ? IncrementalHash.CreateHash(HashAlgorithmName.SHA256) : null;
                long actualFileBytes = 0;
                int read;
                while ((read = stream.Read(buffer, 0, buffer.Length)) != 0)
                {
                    actualFileBytes = checked(actualFileBytes + read);
                    actualTotalBytes = checked(actualTotalBytes + read);
                    if (actualFileBytes > limits.MaxSourceFileSizeBytes)
                        return SourceChecksumIndex.Gap("PdbSourceFileSizeExceeded");
                    if (actualTotalBytes > limits.MaxSourceTotalBytes)
                        return SourceChecksumIndex.Gap("PdbSourceTotalBytesExceeded");
                    sha1?.AppendData(buffer, 0, read);
                    sha256?.AppendData(buffer, 0, read);
                }
                Add(Sha1DocumentHashAlgorithm, sha1);
                Add(Sha256DocumentHashAlgorithm, sha256);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return SourceChecksumIndex.Gap("PdbSourceInputUnreadable");
            }

            void Add(Guid algorithm, IncrementalHash? hash)
            {
                if (hash is null)
                    return;
                var checksum = Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
                if (!candidates.TryGetValue((algorithm, checksum), out var paths))
                    candidates[(algorithm, checksum)] = paths = [];
                paths.Add(source.RelativePath);
            }
        }
        return new SourceChecksumIndex(
            candidates.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<string>)pair.Value.Order(StringComparer.Ordinal).ToArray()),
            null);
    }

    private static IReadOnlyList<CompiledBinding> ReadCompiledBindings(
        string repoPath,
        ScanOptions options,
        CompiledInputEvaluation evaluation,
        long maximumBytes)
    {
        if (evaluation.Provenance is null)
            return [];
        var result = new List<CompiledBinding>();
        foreach (var path in CleanPaths((options.CompiledInputPaths ?? []).Concat(options.CompiledDependencyPaths ?? []).ToArray()))
        {
            var fullPath = ResolvePath(repoPath, path);
            if (!File.Exists(fullPath) || new FileInfo(fullPath).Length > maximumBytes)
                continue;
            byte[] bytes;
            try
            {
                bytes = ReadBoundedFile(fullPath, maximumBytes);
            }
            catch (Exception exception) when (exception is PdbInputException or IOException or UnauthorizedAccessException)
            {
                continue;
            }
            var digest = Sha256(bytes);
            var outcomes = evaluation.Provenance.Outcomes.Where(item => string.Equals(item.RawFileSha256, digest, StringComparison.Ordinal)).ToArray();
            if (outcomes.Length != 1)
                continue;
            var outcome = outcomes[0];
            var identities = new List<string>();
            try
            {
                using var pe = new PEReader(new MemoryStream(bytes, writable: false));
                foreach (var entry in pe.ReadDebugDirectory().Where(entry => entry.Type == DebugDirectoryEntryType.CodeView))
                {
                    var data = pe.ReadCodeViewDebugDirectoryData(entry);
                    identities.Add(ContentIdentity(data.Guid, entry.Stamp));
                }
            }
            catch (Exception exception) when (IsRecoverable(exception))
            {
                continue;
            }
            result.Add(new CompiledBinding(
                outcome.SafeLocator,
                outcome.Outcome,
                outcome.ProvenanceState,
                outcome.AssemblyIdentity,
                outcome.ProvenanceBindingInputSha256,
                bytes,
                identities.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray()));
        }
        return result;
    }

    private static EvaluatedPdbInput Gap(
        string safeLocator,
        string outcome,
        string format,
        string gapKind,
        string? rawSha256 = null,
        string? contentId = null,
        CompiledBinding? binding = null)
    {
        var gaps = new[] { gapKind };
        return new EvaluatedPdbInput(
            new PdbInputOutcome(
                safeLocator,
                outcome,
                format,
                rawSha256,
                PrivacyProjectedDigest(safeLocator, rawSha256, contentId, binding?.SafeLocator, outcome, gaps),
                contentId,
                binding?.SafeLocator,
                binding?.AssemblyIdentity,
                binding?.ProvenanceBindingInputSha256,
                binding is null ? "unmatched" : binding.ProvenanceState,
                gaps),
            [],
            []);
    }

    private static CodeFact GapFact(ScanManifest manifest, string safeLocator, string gapKind, IReadOnlyDictionary<string, string> properties) =>
        FactFactory.Create(
            manifest,
            FactTypes.AnalysisGap,
            RuleIds.DotNetPdbGap,
            EvidenceTiers.Tier4Unknown,
            PdbEvidence(safeLocator),
            contractElement: gapKind,
            properties: Merge(properties, new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["gapKind"] = gapKind,
                ["limitation"] = GapLimitation
            }));

    private static SortedDictionary<string, string> CommonProperties(PdbInputProvenance provenance, PdbInputOutcome outcome) => new(StringComparer.Ordinal)
    {
        ["pdbSchemaVersion"] = provenance.SchemaVersion,
        ["pdbPolicyVersion"] = provenance.PolicyVersion,
        ["pdbBoundedInputSha256"] = provenance.BoundedInputSha256,
        ["pdbGeneratorSha256"] = provenance.GeneratorSha256,
        ["pdbCoverageState"] = provenance.CoverageState,
        ["pdbFormat"] = outcome.Format,
        ["pdbContentId"] = outcome.PdbContentId ?? string.Empty,
        ["pdbProvenanceState"] = outcome.BindingState,
        ["pdbRawFileSha256"] = outcome.RawFileSha256 ?? string.Empty,
        ["matchedAssemblySafeLocator"] = outcome.MatchedAssemblySafeLocator ?? string.Empty,
        ["matchedAssemblyIdentity"] = outcome.MatchedAssemblyIdentity ?? string.Empty,
        ["provenanceBindingInputSha256"] = outcome.ProvenanceBindingInputSha256 ?? string.Empty,
        ["evidenceLocationKind"] = PdbLocationKind
    };

    private static SortedDictionary<string, string> Merge(IReadOnlyDictionary<string, string> first, IReadOnlyDictionary<string, string> second)
    {
        var result = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in first)
            result[key] = value;
        foreach (var (key, value) in second)
            result[key] = value;
        return result;
    }

    private static EvidenceSpan PdbEvidence(string safeLocator) =>
        new(safeLocator, 1, 1, null, nameof(PortablePdbExtractor), ScannerVersions.PortablePdbExtractor);

    private static string PrivacyProjectedDigest(string locator, string? rawSha256, string? contentId, string? assemblyLocator, string state, IReadOnlyList<string> gaps) =>
        CanonicalDigest(new
        {
            safeLocator = locator,
            rawDigestCommitment = rawSha256 is null ? string.Empty : Sha256(Encoding.UTF8.GetBytes(rawSha256)),
            pdbContentId = contentId ?? string.Empty,
            matchedAssemblySafeLocator = assemblyLocator ?? string.Empty,
            bindingState = state,
            gapKinds = gaps.Order(StringComparer.Ordinal).ToArray()
        });

    private static string ContentIdentity(Guid guid, uint stamp) => $"{guid:D}:{stamp:x8}";
    private static bool IsPortablePdb(byte[] bytes) => bytes.Length >= 4 && bytes[0] == (byte)'B' && bytes[1] == (byte)'S' && bytes[2] == (byte)'J' && bytes[3] == (byte)'B';
    internal static bool IsWindowsPdb(byte[] bytes) => bytes.AsSpan().StartsWith(WindowsPdbSignature);
    private static string ResolvePath(string repoPath, string path) => Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(repoPath, path));

    private static string SafeLocator(string repoPath, string fullPath)
    {
        var relative = Path.GetRelativePath(repoPath, fullPath).Replace('\\', '/');
        if (!relative.StartsWith("../", StringComparison.Ordinal) && relative != ".." && !Path.IsPathRooted(relative))
            return relative;
        var directoryHash = FactFactory.Hash(Path.GetDirectoryName(fullPath) ?? string.Empty, 16);
        var fileName = new string(Path.GetFileName(fullPath).Select(character => char.IsLetterOrDigit(character) || character is '.' or '-' or '_' ? character : '_').ToArray());
        return $"external/{directoryHash}/{(string.IsNullOrWhiteSpace(fileName) ? "input.pdb" : fileName)}";
    }

    private static string BoundedSafeLocator(string repoPath, string fullPath, int maximumLength) =>
        ProjectBoundedText(SafeLocator(repoPath, fullPath), maximumLength);

    private static string ProjectBoundedText(string value, int maximumLength) =>
        value.Length <= maximumLength ? value : "sha256:" + Sha256(Encoding.UTF8.GetBytes(value));

    private static IReadOnlyList<string> CleanPaths(IReadOnlyList<string>? paths) =>
        (paths ?? []).Where(path => !string.IsNullOrWhiteSpace(path)).Select(path => path.Trim()).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();

    private static void ValidateLimits(PdbInputLimits limits)
    {
        if (limits.MaxArtifactCount <= 0 || limits.MaxArtifactCount > MaximumArtifactCount
            || limits.MaxFileSizeBytes <= 0 || limits.MaxDocumentCount <= 0
            || limits.MaxMethodCount <= 0 || limits.MaxSequencePointCount <= 0
            || limits.MaxSourceFileCount <= 0 || limits.MaxSourceFileSizeBytes <= 0 || limits.MaxSourceTotalBytes <= 0
            || limits.MaxTextLength <= 0 || limits.MaxTotalWorkUnits <= 0)
            throw new ArgumentException("PDB input limits must all be positive.");
        if (limits.MaxTextLength < MinimumProjectedTextLength)
            throw new ArgumentException($"PDB input maximum text length must be at least {MinimumProjectedTextLength} characters.");
    }

    private static byte[] ReadBoundedFile(string path, long maximumBytes)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81_920, FileOptions.SequentialScan);
        if (stream.Length > maximumBytes)
            throw new PdbInputException("PdbInputFileSizeExceeded");
        using var output = new MemoryStream((int)Math.Min(stream.Length, 1_048_576));
        var buffer = new byte[81_920];
        long total = 0;
        int read;
        while ((read = stream.Read(buffer, 0, buffer.Length)) != 0)
        {
            total = checked(total + read);
            if (total > maximumBytes)
                throw new PdbInputException("PdbInputFileSizeExceeded");
            output.Write(buffer, 0, read);
        }
        return output.ToArray();
    }

    private static string GeneratorSha256()
    {
        var path = typeof(PortablePdbExtractor).Assembly.Location;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            throw new InvalidOperationException("PdbGeneratorIdentityUnavailable");
        return Sha256(File.ReadAllBytes(path));
    }

    private static string CanonicalDigest<T>(T value)
    {
        using var stream = new MemoryStream();
        JsonSerializer.Serialize(stream, value, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        return Sha256(stream.ToArray());
    }

    private static string Sha256(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    internal static bool IsRecoverable(Exception exception) => exception is BadImageFormatException or IOException or InvalidDataException
        or InvalidOperationException or ArgumentException or NotSupportedException or FormatException or IndexOutOfRangeException;

    private sealed record CompiledBinding(
        string SafeLocator,
        string Outcome,
        string ProvenanceState,
        string? AssemblyIdentity,
        string ProvenanceBindingInputSha256,
        byte[] Bytes,
        IReadOnlyList<string> CodeViewIdentities);

    private sealed record SourceDocumentMatch(IReadOnlyList<string> Candidates, string? GapKind)
    {
        public static readonly SourceDocumentMatch Zero = new([], "PdbSourceDocumentZeroCandidate");
    }

    private sealed record SourceChecksumIndex(
        IReadOnlyDictionary<(Guid Algorithm, string Checksum), IReadOnlyList<string>> Candidates,
        string? GapKind)
    {
        public static readonly SourceChecksumIndex Empty = new(
            new Dictionary<(Guid Algorithm, string Checksum), IReadOnlyList<string>>(), null);

        public static SourceChecksumIndex Gap(string gapKind) => new(
            new Dictionary<(Guid Algorithm, string Checksum), IReadOnlyList<string>>(), gapKind);
    }

    private sealed record PdbMaterializationPlan(
        EvaluatedPdbInput Input,
        IReadOnlyDictionary<int, SourceDocumentMatch> SourceMatches,
        IReadOnlyDictionary<int, CodeFact[]> MetadataCandidates);

    private sealed class PdbWorkBudget(long maximum)
    {
        public long Consumed { get; private set; }

        public void Consume(string gapKind)
        {
            if (Consumed >= maximum)
                throw new PdbInputException(gapKind);
            Consumed++;
        }
    }

    private sealed class RejectingAssemblyResolver : IAssemblyResolver
    {
        public Mono.Cecil.AssemblyDefinition Resolve(AssemblyNameReference name) => throw new AssemblyResolutionException(name);
        public Mono.Cecil.AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters) => throw new AssemblyResolutionException(name);
        public void Dispose()
        {
        }
    }

    private sealed class PdbInputException(string message) : Exception(message);
}
