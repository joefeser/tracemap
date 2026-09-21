using System.Globalization;
using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Mono.Cecil;
using CecilArrayType = Mono.Cecil.ArrayType;
using CecilCustomModifier = Mono.Cecil.IModifierType;
using CecilFunctionPointerType = Mono.Cecil.FunctionPointerType;
using CecilGenericInstanceType = Mono.Cecil.GenericInstanceType;
using CecilGenericParameter = Mono.Cecil.GenericParameter;
using CecilPointerType = Mono.Cecil.PointerType;
using CecilSentinelType = Mono.Cecil.SentinelType;
using CecilTypeDefinition = Mono.Cecil.TypeDefinition;
using CecilTypeReference = Mono.Cecil.TypeReference;

namespace TraceMap.Core;

public static class ManagedMetadataExtractor
{
    internal const int MinimumProjectedTextLength = 71;
    internal const int MaximumSignatureTypeNesting = 256;
    public const string SchemaVersion = "compiled-input-provenance.v1";
    public const string PolicyVersion = "explicit-managed-input.v1";
    public const string MetadataLocationKind = "managed-metadata-v1";
    public const string InputLocationKind = "managed-input-v1";
    public const string StructuralLimitation = "Compiled metadata declaration evidence does not prove source ownership, build freshness, authenticity, runtime load, execution, dispatch, or reachability.";
    public const string InputLimitation = "Admission proves only that the explicitly supplied bounded bytes were inspected; it does not prove freshness, source ownership, authenticity, runtime load, or completeness.";
    public const string GapLimitation = "This categorical gap reduces only the explicitly bounded compiled-input coverage; it does not prove absence and does not alter source-derived evidence.";

    internal static CompiledInputEvaluation Evaluate(
        string repoPath,
        string scanCommitSha,
        ScanOptions options,
        CancellationToken cancellationToken = default)
    {
        var primaryPaths = CleanPaths(options.CompiledInputPaths);
        var dependencyPaths = CleanPaths(options.CompiledDependencyPaths);
        var receiptPaths = CleanPaths(options.CompiledBindingReceiptPaths);
        if (primaryPaths.Count == 0 && dependencyPaths.Count == 0 && receiptPaths.Count == 0)
            return CompiledInputEvaluation.Disabled;

        var limits = options.CompiledInputLimits ?? new CompiledInputLimits();
        ValidateLimits(limits);
        var generatorSha256 = GeneratorSha256();
        var workBudget = new WorkBudget(limits.MaxTotalWorkUnits);
        var receipts = ReadReceipts(repoPath, receiptPaths, limits, workBudget);
        var descriptors = CreateDescriptors(repoPath, primaryPaths, dependencyPaths, limits);
        var evaluated = new List<EvaluatedInput>();
        var globalCandidates = new List<CompiledEvidenceCandidate>();
        var globalGapKinds = new List<string>(receipts.Gaps);
        var omittedDescriptors = descriptors.Skip(limits.MaxArtifactCount).ToArray();
        var omittedInputSha256 = omittedDescriptors.Length == 0
            ? null
            : CanonicalDigest(omittedDescriptors.Select(descriptor => new { descriptor.SafeLocator, descriptor.Role }).ToArray());
        if (omittedDescriptors.Length > 0)
            globalGapKinds.Add("LimitArtifactCountExceeded");
        if (descriptors.Count == 0)
            globalGapKinds.Add("NoManagedInputDeclared");

        foreach (var descriptor in descriptors.Take(limits.MaxArtifactCount))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (descriptor.SafeLocatorTextLimitExceeded)
            {
                evaluated.Add(EvaluatedInput.Gap(descriptor, "limit-exhausted", "ManagedInputTextLimitExceeded"));
                continue;
            }
            if (!File.Exists(descriptor.FullPath))
            {
                evaluated.Add(EvaluatedInput.Gap(descriptor, "missing", "MissingManagedInput"));
                continue;
            }

            byte[] bytes;
            try
            {
                var length = new FileInfo(descriptor.FullPath).Length;
                if (length > limits.MaxFileSizeBytes)
                {
                    evaluated.Add(EvaluatedInput.Gap(descriptor, "limit-exhausted", "ManagedInputFileSizeLimitExceeded"));
                    continue;
                }
                bytes = ReadBoundedFile(descriptor.FullPath, limits.MaxFileSizeBytes, "ManagedInputFileSizeLimitExceeded");
            }
            catch (ManagedInputException exception)
            {
                evaluated.Add(EvaluatedInput.Gap(descriptor, exception.Outcome, exception.GapKind));
                continue;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                evaluated.Add(EvaluatedInput.Gap(descriptor, "unreadable", "UnreadableManagedInput"));
                continue;
            }

            var rawSha256 = Sha256(bytes);
            var admittedDescriptor = FinalizeSafeLocator(descriptor, rawSha256, limits);
            if (admittedDescriptor.SafeLocatorTextLimitExceeded)
            {
                evaluated.Add(EvaluatedInput.Gap(admittedDescriptor, "limit-exhausted", "ManagedInputTextLimitExceeded", rawSha256));
                continue;
            }
            try
            {
                var workUnits = PreflightManagedInput(bytes, limits);
                if (!workBudget.TryConsume(workUnits))
                {
                    evaluated.Add(EvaluatedInput.Gap(admittedDescriptor, "limit-exhausted", "ManagedInputTotalWorkLimitExceeded", rawSha256));
                    continue;
                }
            }
            catch (ManagedInputException exception)
            {
                evaluated.Add(EvaluatedInput.Gap(admittedDescriptor, exception.Outcome, exception.GapKind, rawSha256));
                continue;
            }
            catch (BadImageFormatException)
            {
                evaluated.Add(EvaluatedInput.Gap(admittedDescriptor, "unreadable", "MalformedManagedInput", rawSha256));
                continue;
            }
            catch (Exception exception) when (IsRecoverableMetadataException(exception))
            {
                evaluated.Add(EvaluatedInput.Gap(admittedDescriptor, "unreadable", "MalformedManagedInput", rawSha256));
                continue;
            }
            MetadataReadResult cecil;
            MetadataReadResult srm;
            try
            {
                cecil = ReadWithCecil(bytes, admittedDescriptor.SafeLocator, limits);
            }
            catch (ManagedInputException exception)
            {
                evaluated.Add(EvaluatedInput.Gap(admittedDescriptor, exception.Outcome, exception.GapKind, rawSha256));
                continue;
            }
            catch (Exception exception) when (IsRecoverableMetadataException(exception))
            {
                evaluated.Add(EvaluatedInput.Gap(admittedDescriptor, "unreadable", "CecilManagedMetadataReaderFailure", rawSha256));
                continue;
            }
            try
            {
                srm = ReadWithSystemReflectionMetadata(bytes, admittedDescriptor.SafeLocator, limits);
            }
            catch (ManagedInputException exception)
            {
                evaluated.Add(EvaluatedInput.Gap(admittedDescriptor, exception.Outcome, exception.GapKind, rawSha256));
                continue;
            }
            catch (Exception exception) when (IsRecoverableMetadataException(exception))
            {
                evaluated.Add(EvaluatedInput.Gap(admittedDescriptor, "unreadable", "SystemReflectionMetadataReaderFailure", rawSha256));
                continue;
            }

            var disagreement = CrossCheck(cecil.Observations, srm.Observations);
            var receipt = receipts.Receipts.FirstOrDefault(candidate =>
                string.Equals(candidate.SafeLocator, admittedDescriptor.SafeLocator, StringComparison.Ordinal));
            var binding = ClassifyBinding(receipt, rawSha256, cecil.AssemblyIdentity, scanCommitSha);
            var gaps = new List<string>();
            if (binding.State != "bound")
                gaps.Add(binding.GapKind);
            gaps.AddRange(ReaderDisagreementGapKinds(disagreement));

            var candidates = BuildCandidates(admittedDescriptor, cecil, disagreement, rawSha256, binding)
                .ToList();
            evaluated.Add(new EvaluatedInput(
                admittedDescriptor,
                "admitted",
                binding.State,
                rawSha256,
                binding.Digest,
                cecil.AssemblyIdentity,
                cecil.AssemblyReferenceIdentity,
                cecil.ModuleName,
                cecil.ModuleMvid,
                gaps,
                cecil.AssemblyReferences,
                candidates));
        }

        AddDuplicateAndDependencyGaps(evaluated, limits);
        foreach (var globalGapKind in globalGapKinds.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal))
        {
            var inputSetGap = globalGapKind is "NoManagedInputDeclared" or "LimitArtifactCountExceeded";
            var safeLocator = inputSetGap ? "compiled-input-set" : "compiled-binding-receipt";
            var candidate = GapCandidate(safeLocator, globalGapKind, "unknown", null, null);
            if (globalGapKind == "LimitArtifactCountExceeded")
            {
                var properties = CopyProperties(candidate.Properties);
                properties["omittedInputCount"] = omittedDescriptors.Length.ToString(CultureInfo.InvariantCulture);
                properties["omittedInputSha256"] = omittedInputSha256!;
                candidate = candidate with { Properties = properties };
            }
            globalCandidates.Add(candidate);
        }

        var expectedInputs = evaluated
            .Select(item => new CompiledExpectedInput(item.Descriptor.SafeLocator, item.Descriptor.Role))
            .OrderBy(item => item.SafeLocator, StringComparer.Ordinal)
            .ThenBy(item => item.Role, StringComparer.Ordinal)
            .ToArray();
        var preDigestOutcomes = evaluated.Select(ToPreDigestOutcome).ToArray();
        var boundedInputSha256 = CanonicalDigest(new
        {
            schemaVersion = SchemaVersion,
            policyVersion = PolicyVersion,
            generatorSha256,
            extractorIdentities = new[] { ScannerVersions.ManagedMetadataExtractor, "system-reflection-metadata/10.0.0" },
            expectedInputs,
            declaredDependencyRoots = expectedInputs.Where(item => item.Role == "dependency").Select(item => item.SafeLocator).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            effectiveLimits = limits,
            outcomes = preDigestOutcomes,
            declaredBindingDigests = receipts.BindingDigests,
            bindingReceiptGaps = receipts.Gaps.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            globalGapKinds = globalGapKinds.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            omittedInputCount = omittedDescriptors.Length,
            omittedInputSha256 = omittedInputSha256 ?? string.Empty
        });

        var outcomes = evaluated.Select(item => new CompiledInputOutcome(
                item.Descriptor.SafeLocator,
                item.Descriptor.Role,
                item.Outcome,
                item.ProvenanceState,
                item.RawSha256,
                PrivacyProjectedInputDigest(item),
                item.BindingDigest,
                item.AssemblyIdentity,
                item.ModuleName,
                item.ModuleMvid,
                item.GapKinds.OrderBy(value => value, StringComparer.Ordinal).Distinct(StringComparer.Ordinal).ToArray(),
                item.DependencyResolutionOutcomes.OrderBy(value => value, StringComparer.Ordinal).ToArray()))
            .OrderBy(item => item.SafeLocator, StringComparer.Ordinal)
            .ThenBy(item => item.Role, StringComparer.Ordinal)
            .ToArray();
        var coverage = globalGapKinds.Count == 0
            && outcomes.Length > 0
            && outcomes.All(item => item.Outcome == "admitted" && item.ProvenanceState == "bound" && item.GapKinds.Count == 0)
                ? "compiled-metadata-complete"
                : "compiled-metadata-partial";
        var provenance = new CompiledInputProvenance(
            SchemaVersion,
            PolicyVersion,
            generatorSha256,
            [ScannerVersions.ManagedMetadataExtractor, "system-reflection-metadata/10.0.0"],
            expectedInputs,
            limits,
            outcomes,
            outcomes.Select(item => item.ProvenanceBindingInputSha256)
                .Concat(receipts.BindingDigests)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray(),
            boundedInputSha256,
            "local-only",
            coverage,
            omittedDescriptors.Length,
            omittedInputSha256);

        var candidatesWithProvenance = evaluated
            .SelectMany(item => MaterializeInputCandidates(item, boundedInputSha256, generatorSha256, coverage))
            .Concat(globalCandidates.Select(candidate => WithCommonProvenance(candidate, boundedInputSha256, generatorSha256, coverage, null, null)))
            .OrderBy(candidate => candidate.SafeLocator, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.FactType, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.MetadataToken, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.TargetSymbol, StringComparer.Ordinal)
            .ToArray();
        var knownGaps = outcomes
            .SelectMany(item => item.GapKinds)
            .Concat(globalGapKinds)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .Select(value => $"Compiled metadata coverage reduced: {value}.")
            .ToArray();
        var bindingArtifacts = evaluated
            .Where(item => item.Outcome == "admitted")
            .Select(item => new CompiledInputBindingArtifact(
                item.Descriptor.FullPath,
                item.Descriptor.SafeLocator,
                item.Descriptor.Role,
                item.Outcome,
                item.ProvenanceState,
                item.RawSha256,
                item.AssemblyIdentity,
                item.BindingDigest))
            .OrderBy(item => item.SafeLocator, StringComparer.Ordinal)
            .ThenBy(item => item.FullPath, StringComparer.Ordinal)
            .ToArray();
        return new CompiledInputEvaluation(provenance, candidatesWithProvenance, knownGaps, bindingArtifacts);
    }

    internal static IReadOnlyList<CodeFact> MaterializeFacts(ScanManifest manifest, CompiledInputEvaluation evaluation) =>
        evaluation.Candidates.Select(candidate => FactFactory.Create(
                manifest,
                candidate.FactType,
                candidate.RuleId,
                candidate.EvidenceTier,
                new EvidenceSpan(
                    candidate.SafeLocator,
                    1,
                    1,
                    null,
                    nameof(ManagedMetadataExtractor),
                    ScannerVersions.ManagedMetadataExtractor),
                sourceSymbol: candidate.SourceSymbol,
                targetSymbol: candidate.TargetSymbol,
                contractElement: candidate.ContractElement,
                properties: candidate.Properties))
            .ToArray();

    internal static IReadOnlyList<ReaderDisagreement> CrossCheck(
        IReadOnlyList<MetadataObservation> cecil,
        IReadOnlyList<MetadataObservation> srm)
    {
        var left = cecil.ToDictionary(item => item.Key, StringComparer.Ordinal);
        var right = srm.ToDictionary(item => item.Key, StringComparer.Ordinal);
        return left.Keys.Concat(right.Keys).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal)
            .Where(key => !left.TryGetValue(key, out var leftValue)
                || !right.TryGetValue(key, out var rightValue)
                || !string.Equals(leftValue.Identity, rightValue.Identity, StringComparison.Ordinal))
            .Select(key => new ReaderDisagreement(
                key,
                left.GetValueOrDefault(key)?.Identity,
                right.GetValueOrDefault(key)?.Identity))
            .ToArray();
    }

    internal static IReadOnlyList<string> ReaderDisagreementGapKinds(IReadOnlyList<ReaderDisagreement> disagreements) =>
        disagreements.Count == 0 ? [] : ["MetadataReaderDisagreement"];

    private static IReadOnlyList<CompiledEvidenceCandidate> MaterializeInputCandidates(
        EvaluatedInput item,
        string boundedInputSha256,
        string generatorSha256,
        string coverage)
    {
        var reconciliationBlocker = item.ProvenanceState != "bound"
            ? $"CompiledProvenance{CultureInfo.InvariantCulture.TextInfo.ToTitleCase(item.ProvenanceState)}"
            : item.GapKinds.Contains("AmbiguousDuplicateManagedAssembly", StringComparer.Ordinal)
                ? "AmbiguousDuplicateManagedAssembly"
                : null;
        var result = item.Candidates
            .Select(candidate => WithReconciliationEligibility(candidate, reconciliationBlocker))
            .Select(candidate => WithCommonProvenance(candidate, boundedInputSha256, generatorSha256, coverage, item.RawSha256, item.BindingDigest))
            .ToList();
        if (item.Outcome == "admitted")
        {
            result.Add(WithCommonProvenance(new CompiledEvidenceCandidate(
                item.Descriptor.SafeLocator,
                FactTypes.ManagedInputAdmitted,
                RuleIds.DotNetCompiledInput,
                EvidenceTiers.Tier2Structural,
                null,
                item.AssemblyIdentity,
                item.Descriptor.Role,
                new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["evidenceLocationKind"] = InputLocationKind,
                    ["inputRole"] = item.Descriptor.Role,
                    ["provenanceState"] = item.ProvenanceState,
                    ["limitation"] = InputLimitation
                },
                null), boundedInputSha256, generatorSha256, coverage, item.RawSha256, item.BindingDigest));
        }

        foreach (var gapKind in item.GapKinds.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal))
        {
            result.Add(WithCommonProvenance(
                GapCandidate(item.Descriptor.SafeLocator, gapKind, item.ProvenanceState, item.RawSha256, item.BindingDigest),
                boundedInputSha256,
                generatorSha256,
                coverage,
                item.RawSha256,
                item.BindingDigest));
        }
        return result;
    }

    private static CompiledEvidenceCandidate WithReconciliationEligibility(
        CompiledEvidenceCandidate candidate,
        string? blocker)
    {
        var properties = CopyProperties(candidate.Properties);
        properties["sourceReconciliationEligibility"] = blocker is null ? "eligible" : "ineligible";
        properties["sourceReconciliationBlocker"] = blocker ?? string.Empty;
        return candidate with { Properties = properties };
    }

    private static CompiledEvidenceCandidate WithCommonProvenance(
        CompiledEvidenceCandidate candidate,
        string boundedInputSha256,
        string generatorSha256,
        string coverage,
        string? rawSha256,
        string? bindingDigest)
    {
        var properties = CopyProperties(candidate.Properties);
        properties["boundedInputSha256"] = boundedInputSha256;
        properties["compiledCoverage"] = coverage;
        properties["generatorSha256"] = generatorSha256;
        if (rawSha256 is not null)
            properties["rawFileSha256"] = rawSha256;
        if (bindingDigest is not null)
            properties["provenanceBindingInputSha256"] = bindingDigest;
        return candidate with { Properties = properties };
    }

    private static CompiledEvidenceCandidate GapCandidate(
        string safeLocator,
        string gapKind,
        string provenanceState,
        string? rawSha256,
        string? bindingDigest) => new(
            safeLocator,
            FactTypes.AnalysisGap,
            RuleIds.DotNetCompiledGap,
            EvidenceTiers.Tier4Unknown,
            null,
            null,
            gapKind,
            new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["evidenceLocationKind"] = InputLocationKind,
                ["gapKind"] = gapKind,
                ["provenanceState"] = provenanceState,
                ["limitation"] = GapLimitation
            },
            null);

    private static IEnumerable<CompiledEvidenceCandidate> BuildCandidates(
        InputDescriptor descriptor,
        MetadataReadResult result,
        IReadOnlyList<ReaderDisagreement> disagreements,
        string rawSha256,
        BindingClassification binding)
    {
        var disputed = disagreements.Select(item => item.Key).ToHashSet(StringComparer.Ordinal);
        foreach (var observation in result.Observations.Where(item => !disputed.Contains(item.Key)))
        {
            var properties = CopyProperties(observation.Properties);
            properties["assemblyIdentity"] = result.AssemblyIdentity;
            properties["assemblyReferenceIdentity"] = result.AssemblyReferenceIdentity;
            properties["evidenceLocationKind"] = MetadataLocationKind;
            properties["inputRole"] = descriptor.Role;
            properties["limitation"] = StructuralLimitation;
            properties["moduleMvid"] = result.ModuleMvid;
            properties["moduleName"] = result.ModuleName;
            properties["provenanceState"] = binding.State;
            properties["metadataToken"] = observation.MetadataToken;
            if (!string.IsNullOrWhiteSpace(result.TargetFramework))
                properties["targetFramework"] = result.TargetFramework;
            if (binding.Receipt?.BinarySourceRepository is not null)
                properties["binarySourceRepositorySha256"] = Sha256(Encoding.UTF8.GetBytes(binding.Receipt.BinarySourceRepository));
            if (binding.Receipt?.BinarySourceCommitSha is not null)
                properties["binarySourceCommitSha"] = binding.Receipt.BinarySourceCommitSha;
            if (binding.Receipt?.BinaryBuildIdentity is not null)
                properties["binaryBuildIdentitySha256"] = Sha256(Encoding.UTF8.GetBytes(binding.Receipt.BinaryBuildIdentity));

            yield return new CompiledEvidenceCandidate(
                descriptor.SafeLocator,
                observation.FactType,
                observation.RuleId,
                EvidenceTiers.Tier2Structural,
                null,
                observation.Identity,
                observation.MemberKind,
                properties,
                observation.MetadataToken);
        }
    }

    private static void AddDuplicateAndDependencyGaps(List<EvaluatedInput> inputs, CompiledInputLimits limits)
    {
        foreach (var group in inputs.Where(item => item.Outcome == "admitted" && item.Descriptor.Role == "primary" && item.AssemblyIdentity is not null)
            .GroupBy(item => item.AssemblyIdentity!, StringComparer.Ordinal)
            .Where(group => group.Count() > 1))
        {
            foreach (var item in group)
                item.GapKinds.Add("AmbiguousDuplicateManagedAssembly");
        }

        var dependencies = inputs.Where(item => item.Outcome == "admitted" && item.Descriptor.Role == "dependency").ToArray();
        foreach (var input in inputs.Where(item => item.Outcome == "admitted"))
        {
            foreach (var reference in input.AssemblyReferences.OrderBy(value => value, StringComparer.Ordinal))
            {
                var matches = dependencies.Where(candidate => string.Equals(candidate.AssemblyReferenceIdentity, reference, StringComparison.Ordinal))
                    .OrderBy(candidate => candidate.Descriptor.SafeLocator, StringComparer.Ordinal)
                    .ToArray();
                if (matches.Length == 0)
                {
                    input.GapKinds.Add("UnresolvedManagedAssemblyReference");
                    AddDependencyResolutionOutcome(input, $"{reference}=>unresolved", limits);
                }
                else if (matches.Length > 1)
                {
                    input.GapKinds.Add("AmbiguousManagedAssemblyReference");
                    AddDependencyResolutionOutcome(input, $"{reference}=>ambiguous:{string.Join(",", matches.Select(item => item.Descriptor.SafeLocator))}", limits);
                }
                else
                {
                    AddDependencyResolutionOutcome(input, $"{reference}=>resolved:{matches[0].Descriptor.SafeLocator}", limits);
                }
            }
        }
    }

    private static void AddDependencyResolutionOutcome(EvaluatedInput input, string outcome, CompiledInputLimits limits)
    {
        if (outcome.Length > limits.MaxTextLength)
        {
            input.GapKinds.Add("ManagedInputTextLimitExceeded");
            return;
        }
        input.DependencyResolutionOutcomes.Add(outcome);
    }

    private static BindingClassification ClassifyBinding(
        CompiledBindingReceipt? receipt,
        string rawSha256,
        string assemblyIdentity,
        string scanCommitSha)
    {
        var projection = new
        {
            schemaVersion = receipt?.SchemaVersion ?? "none",
            safeLocator = receipt?.SafeLocator ?? string.Empty,
            artifactDigestMatch = receipt?.ArtifactSha256 is null ? "not-supplied" : string.Equals(receipt.ArtifactSha256, rawSha256, StringComparison.Ordinal) ? "match" : "mismatch",
            assemblyIdentityMatch = receipt?.AssemblyIdentity is null ? "not-supplied" : string.Equals(receipt.AssemblyIdentity, assemblyIdentity, StringComparison.Ordinal) ? "match" : "mismatch",
            sourceRepositorySha256 = receipt?.BinarySourceRepository is null ? string.Empty : Sha256(Encoding.UTF8.GetBytes(receipt.BinarySourceRepository)),
            sourceCommitSha256 = receipt?.BinarySourceCommitSha is null ? string.Empty : Sha256(Encoding.UTF8.GetBytes(receipt.BinarySourceCommitSha)),
            sourceCommitRelation = receipt?.BinarySourceCommitRelation ?? string.Empty,
            buildIdentitySha256 = receipt?.BinaryBuildIdentity is null ? string.Empty : Sha256(Encoding.UTF8.GetBytes(receipt.BinaryBuildIdentity))
        };
        var digest = CanonicalDigest(projection);
        if (receipt is null)
            return new BindingClassification("unbound", "UnboundManagedInput", digest, null);
        if (!string.Equals(receipt.SchemaVersion, "compiled-input-binding.v1", StringComparison.Ordinal)
            || !IsSha256(receipt.ArtifactSha256)
            || !string.Equals(receipt.ArtifactSha256, rawSha256, StringComparison.Ordinal)
            || receipt.AssemblyIdentity is not null && !string.Equals(receipt.AssemblyIdentity, assemblyIdentity, StringComparison.Ordinal))
            return new BindingClassification("mismatch", "ManagedInputProvenanceMismatch", digest, null);
        if (string.IsNullOrWhiteSpace(receipt.BinarySourceRepository)
            || !IsCommitSha(receipt.BinarySourceCommitSha)
            || string.IsNullOrWhiteSpace(receipt.BinaryBuildIdentity))
            return new BindingClassification("unknown", "ManagedInputBindingIncomplete", digest, null);
        if (!string.IsNullOrEmpty(receipt.BinarySourceCommitRelation)
            && !string.Equals(receipt.BinarySourceCommitRelation, "ancestor-of-scan", StringComparison.Ordinal))
            return new BindingClassification("mismatch", "ManagedInputProvenanceMismatch", digest, null);
        if (!string.Equals(receipt.BinarySourceCommitSha, scanCommitSha, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(receipt.BinarySourceCommitRelation, "ancestor-of-scan", StringComparison.Ordinal))
            return new BindingClassification("mismatch", "ManagedInputProvenanceMismatch", digest, null);
        if (!string.Equals(receipt.BinarySourceCommitSha, scanCommitSha, StringComparison.OrdinalIgnoreCase))
            return new BindingClassification("stale", "StaleManagedInput", digest, receipt);
        return new BindingClassification("bound", string.Empty, digest, receipt);
    }

    private static ReceiptReadResult ReadReceipts(
        string repoPath,
        IReadOnlyList<string> receiptPaths,
        CompiledInputLimits limits,
        WorkBudget workBudget)
    {
        var receipts = new List<CompiledBindingReceipt>();
        var gaps = new List<string>();
        var pathComparer = CSharpSemanticExtractor.CreateSourcePathComparer(repoPath);
        var resolvedReceiptPaths = receiptPaths
            .Select(path => ResolvePath(repoPath, path))
            .GroupBy(path => path, pathComparer)
            .Select(group => group.OrderBy(path => path, StringComparer.Ordinal).First())
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        foreach (var (fullPath, index) in resolvedReceiptPaths.Select((value, index) => (value, index)))
        {
            if (index >= limits.MaxArtifactCount)
            {
                gaps.Add("ManagedBindingReceiptCountLimitExceeded");
                continue;
            }
            try
            {
                var receiptByteLimit = Math.Min(
                    limits.MaxFileSizeBytes,
                    checked((long)limits.MaxTextLength * (limits.MaxArtifactCount + 1L) * 8L));
                var bytes = ReadBoundedFile(fullPath, receiptByteLimit, "ManagedBindingReceiptFileSizeLimitExceeded");
                var bindingCount = CountReceiptBindings(bytes, limits.MaxArtifactCount);
                if (bindingCount < 0)
                {
                    gaps.Add("ManagedBindingReceiptBindingCountLimitExceeded");
                    continue;
                }
                if (!workBudget.TryConsume(1L + bindingCount))
                {
                    gaps.Add("ManagedBindingReceiptWorkLimitExceeded");
                    continue;
                }
                var document = JsonSerializer.Deserialize<CompiledBindingReceiptDocument>(bytes, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNameCaseInsensitive = false,
                    MaxDepth = 16
                });
                if (document?.SchemaVersion != "compiled-input-binding-set.v1" || document.Bindings is null)
                {
                    gaps.Add("UnsupportedManagedBindingReceipt");
                    continue;
                }
                if ((document.SchemaVersion?.Length ?? 0) > limits.MaxTextLength
                    || document.Bindings.Any(item => item is null || ReceiptTextValues(item).Any(value => value.Length > limits.MaxTextLength)))
                {
                    gaps.Add("ManagedBindingReceiptTextLimitExceeded");
                    continue;
                }
                receipts.AddRange(document.Bindings.Where(item => !string.IsNullOrWhiteSpace(item.SafeLocator)));
            }
            catch (ManagedInputException exception)
            {
                gaps.Add(exception.GapKind);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or OverflowException)
            {
                gaps.Add("UnreadableManagedBindingReceipt");
            }
        }
        var duplicate = receipts.GroupBy(item => item.SafeLocator, StringComparer.Ordinal).Any(group => group.Count() > 1);
        if (duplicate)
            gaps.Add("AmbiguousManagedBindingReceipt");
        var uniqueReceipts = receipts.GroupBy(item => item.SafeLocator, StringComparer.Ordinal)
            .Where(group => group.Count() == 1)
            .Select(group => group.Single())
            .OrderBy(item => item.SafeLocator, StringComparer.Ordinal)
            .ToArray();
        return new ReceiptReadResult(
            uniqueReceipts,
            gaps.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            uniqueReceipts.Select(ReceiptDigest).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray());
    }

    private static IEnumerable<string> ReceiptTextValues(CompiledBindingReceipt receipt)
    {
        yield return receipt.SchemaVersion ?? string.Empty;
        yield return receipt.SafeLocator ?? string.Empty;
        yield return receipt.ArtifactSha256 ?? string.Empty;
        yield return receipt.AssemblyIdentity ?? string.Empty;
        yield return receipt.BinarySourceRepository ?? string.Empty;
        yield return receipt.BinarySourceCommitSha ?? string.Empty;
        yield return receipt.BinarySourceCommitRelation ?? string.Empty;
        yield return receipt.BinaryBuildIdentity ?? string.Empty;
    }

    private static int CountReceiptBindings(byte[] bytes, int maximumBindings)
    {
        var reader = new Utf8JsonReader(bytes, new JsonReaderOptions { MaxDepth = 16 });
        var bindingsArrayDepth = -1;
        var count = 0;
        while (reader.Read())
        {
            if (bindingsArrayDepth < 0
                && reader.TokenType == JsonTokenType.PropertyName
                && reader.CurrentDepth == 1
                && reader.ValueTextEquals("bindings")
                && reader.Read()
                && reader.TokenType == JsonTokenType.StartArray)
            {
                bindingsArrayDepth = reader.CurrentDepth;
                continue;
            }
            if (bindingsArrayDepth >= 0
                && reader.TokenType == JsonTokenType.StartObject
                && reader.CurrentDepth == bindingsArrayDepth + 1
                && ++count > maximumBindings)
            {
                return -1;
            }
            if (bindingsArrayDepth >= 0
                && reader.TokenType == JsonTokenType.EndArray
                && reader.CurrentDepth == bindingsArrayDepth)
            {
                bindingsArrayDepth = -1;
            }
        }
        return count;
    }

    private static string ReceiptDigest(CompiledBindingReceipt receipt) => CanonicalDigest(new
    {
        receipt.SchemaVersion,
        receipt.SafeLocator,
        artifactSha256State = IsSha256(receipt.ArtifactSha256) ? "supplied-valid-shape" : "supplied-invalid-shape",
        artifactSha256Commitment = IsSha256(receipt.ArtifactSha256) ? Sha256(Encoding.UTF8.GetBytes(receipt.ArtifactSha256)) : string.Empty,
        assemblyIdentityCommitment = receipt.AssemblyIdentity is null ? string.Empty : Sha256(Encoding.UTF8.GetBytes(receipt.AssemblyIdentity)),
        sourceRepositoryCommitment = receipt.BinarySourceRepository is null ? string.Empty : Sha256(Encoding.UTF8.GetBytes(receipt.BinarySourceRepository)),
        sourceCommitCommitment = receipt.BinarySourceCommitSha is null ? string.Empty : Sha256(Encoding.UTF8.GetBytes(receipt.BinarySourceCommitSha)),
        sourceCommitRelation = receipt.BinarySourceCommitRelation ?? string.Empty,
        buildIdentityCommitment = receipt.BinaryBuildIdentity is null ? string.Empty : Sha256(Encoding.UTF8.GetBytes(receipt.BinaryBuildIdentity))
    });

    private static IReadOnlyList<InputDescriptor> CreateDescriptors(
        string repoPath,
        IReadOnlyList<string> primaryPaths,
        IReadOnlyList<string> dependencyPaths,
        CompiledInputLimits limits)
    {
        var pathComparer = CSharpSemanticExtractor.CreateSourcePathComparer(repoPath);
        var descriptors = primaryPaths.Select(path => CreateDescriptor(repoPath, path, "primary", limits))
            .Concat(dependencyPaths.Select(path => CreateDescriptor(repoPath, path, "dependency", limits)))
            .GroupBy(item => item.Role, StringComparer.Ordinal)
            .SelectMany(roleGroup => roleGroup
                .GroupBy(item => item.FullPath, pathComparer)
                .Select(pathGroup => pathGroup.OrderBy(item => item.FullPath, StringComparer.Ordinal).First()))
            .OrderBy(item => item.SafeLocator, StringComparer.Ordinal)
            .ThenBy(item => item.Role, StringComparer.Ordinal)
            .ThenBy(item => item.FullPath, StringComparer.Ordinal)
            .ToArray();
        var collisions = descriptors.GroupBy(item => item.SafeLocator, StringComparer.Ordinal).Where(group => group.Count() > 1).ToArray();
        if (collisions.Length == 0)
            return descriptors;
        var collisionOrdinals = collisions
            .SelectMany(group => group.OrderBy(item => item.FullPath, StringComparer.Ordinal).ThenBy(item => item.Role, StringComparer.Ordinal)
                .Select((item, index) => (Key: (item.FullPath, item.Role), Suffix: $"-candidate-{index + 1:D3}")))
            .ToDictionary(item => item.Key, item => item.Suffix);
        return descriptors.Select(item => collisionOrdinals.TryGetValue((item.FullPath, item.Role), out var suffix)
                ? item with { SafeLocator = AppendBoundedSuffix(item.SafeLocator, suffix, limits.MaxTextLength) }
                : item)
            .OrderBy(item => item.SafeLocator, StringComparer.Ordinal)
            .ThenBy(item => item.Role, StringComparer.Ordinal)
            .ToArray();
    }

    private static InputDescriptor CreateDescriptor(string repoPath, string path, string role, CompiledInputLimits limits)
    {
        var fullPath = ResolvePath(repoPath, path);
        var relative = Path.GetRelativePath(repoPath, fullPath);
        var withinRepo = relative != ".." && !relative.StartsWith("../", StringComparison.Ordinal) && !Path.IsPathRooted(relative);
        string safeLocator;
        if (withinRepo)
        {
            safeLocator = FileInventory.NormalizeRelativePath(relative);
        }
        else
        {
            var name = SafeFileName(Path.GetFileName(fullPath));
            safeLocator = $"__external__/{role}/{(File.Exists(fullPath) ? "pending" : "missing")}-{name}";
        }
        var textLimitExceeded = safeLocator.Length > limits.MaxTextLength;
        return new InputDescriptor(
            fullPath,
            textLimitExceeded ? ProjectBoundedText(safeLocator, limits.MaxTextLength) : safeLocator,
            role,
            !withinRepo,
            textLimitExceeded);
    }

    private static InputDescriptor FinalizeSafeLocator(InputDescriptor descriptor, string rawSha256, CompiledInputLimits limits)
    {
        if (!descriptor.IsExternal)
            return descriptor;
        var pendingPrefix = $"__external__/{descriptor.Role}/pending-";
        var retainedNameAndSuffix = descriptor.SafeLocator.StartsWith(pendingPrefix, StringComparison.Ordinal)
            ? descriptor.SafeLocator[pendingPrefix.Length..]
            : SafeFileName(Path.GetFileName(descriptor.FullPath));
        var safeLocator = $"__external__/{descriptor.Role}/{rawSha256[..12]}-{retainedNameAndSuffix}";
        var textLimitExceeded = safeLocator.Length > limits.MaxTextLength;
        return descriptor with
        {
            SafeLocator = textLimitExceeded ? ProjectBoundedText(safeLocator, limits.MaxTextLength) : safeLocator,
            SafeLocatorTextLimitExceeded = descriptor.SafeLocatorTextLimitExceeded || textLimitExceeded
        };
    }

    private static string AppendBoundedSuffix(string value, string suffix, int maxTextLength)
    {
        if (value.Length + suffix.Length <= maxTextLength)
            return value + suffix;
        if (suffix.Length < maxTextLength)
            return value[..Math.Min(value.Length, maxTextLength - suffix.Length)] + suffix;
        return ProjectBoundedText(value + suffix, maxTextLength);
    }

    private static string ProjectBoundedText(string value, int maxTextLength)
    {
        var projection = "sha256:" + Sha256(Encoding.UTF8.GetBytes(value));
        return projection[..Math.Min(maxTextLength, projection.Length)];
    }

    private static MetadataReadResult ReadWithCecil(byte[] bytes, string safeLocator, CompiledInputLimits limits)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        using var resolver = new RejectingAssemblyResolver();
        using var module = Mono.Cecil.ModuleDefinition.ReadModule(stream, new ReaderParameters
        {
            AssemblyResolver = resolver,
            InMemory = true,
            ReadSymbols = false,
            ReadingMode = ReadingMode.Deferred
        });
        if ((module.Attributes & ModuleAttributes.ILOnly) == 0)
            throw new ManagedInputException("unsupported", "NativeOrMixedModeManagedInput");
        if (module.Assembly is null)
            throw new ManagedInputException("unsupported", "ManagedNetmoduleInputUnsupported");

        var allTypes = FlattenTypes(module.Types).Where(type => type.Name != "<Module>").ToArray();
        var memberCount = allTypes.Sum(type => (long)type.Methods.Count + type.Fields.Count + type.Properties.Count + type.Events.Count);
        EnforceMetadataLimits(allTypes.Length, memberCount, limits);
        var assemblyReferenceIdentity = AssemblyReferenceIdentity(
            module.Assembly?.Name?.Name ?? "<netmodule>",
            module.Assembly?.Name?.Version?.ToString() ?? "0.0.0.0",
            module.Assembly?.Name?.Culture,
            module.Assembly?.Name?.PublicKeyToken);
        var targetFramework = TargetFramework(module.Assembly);
        if (targetFramework?.Length > limits.MaxTextLength)
            throw new ManagedInputException("limit-exhausted", "ManagedInputTextLimitExceeded");
        var assemblyIdentity = AssemblyArtifactIdentity(assemblyReferenceIdentity, module.Name, targetFramework);
        var observations = new List<MetadataObservation>();
        observations.Add(Observation("assembly", 0x20000001, FactTypes.ManagedAssemblyDeclared, RuleIds.DotNetCompiledAssembly, assemblyIdentity, "assembly",
            new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["assemblyName"] = module.Assembly?.Name?.Name ?? "<netmodule>",
                ["assemblyVersion"] = module.Assembly?.Name?.Version?.ToString() ?? "0.0.0.0",
                ["culture"] = NormalizeCulture(module.Assembly?.Name?.Culture),
                ["publicKeyToken"] = PublicKeyToken(module.Assembly?.Name?.PublicKeyToken),
                ["publicKeyTokenState"] = module.Assembly?.Name?.PublicKeyToken is { Length: > 0 } ? "present" : "none"
            }));
        observations.Add(Observation("module", 0x00000001, FactTypes.ManagedModuleDeclared, RuleIds.DotNetCompiledAssembly,
            $"{assemblyIdentity}|mvid:{module.Mvid:D}", "module", EmptyProperties()));
        foreach (var type in allTypes.OrderBy(item => item.MetadataToken.ToUInt32()))
        {
            var typeIdentity = TypeIdentity(assemblyIdentity, type);
            observations.Add(Observation("type", unchecked((int)type.MetadataToken.ToUInt32()), FactTypes.ManagedTypeDeclared, RuleIds.DotNetCompiledMember, typeIdentity, "type",
                MemberProperties(type, type.GenericParameters.Count, IsCompilerGenerated(type))));
            foreach (var method in type.Methods)
            {
                var memberKind = method.IsConstructor ? "constructor" : "method";
                var signature = MethodSignature(FormatType(method.ReturnType), method.Parameters.Select(parameter => FormatType(parameter.ParameterType)), method.GenericParameters.Count,
                    method.CallingConvention == MethodCallingConvention.VarArg ? "vararg" : "default", method.HasThis, method.ExplicitThis);
                observations.Add(Observation(memberKind, unchecked((int)method.MetadataToken.ToUInt32()), FactTypes.ManagedMethodDeclared, RuleIds.DotNetCompiledMember,
                    $"{typeIdentity}|{memberKind}:{EncodeIdentityComponent(method.Name)}|{signature}", memberKind,
                    MethodProperties(method, signature)));
            }
            foreach (var field in type.Fields)
            {
                var signature = FormatType(field.FieldType);
                observations.Add(Observation("field", unchecked((int)field.MetadataToken.ToUInt32()), FactTypes.ManagedFieldDeclared, RuleIds.DotNetCompiledMember,
                    $"{typeIdentity}|field:{EncodeIdentityComponent(field.Name)}|type:{signature}", "field", MemberProperties(field, 0, IsCompilerGenerated(field), signature)));
            }
            foreach (var property in type.Properties)
            {
                var accessor = property.GetMethod ?? property.SetMethod;
                var signature = PropertySignature(
                    FormatType(property.PropertyType),
                    property.Parameters.Select(parameter => FormatType(parameter.ParameterType)),
                    accessor?.CallingConvention == MethodCallingConvention.VarArg ? "vararg" : "default",
                    accessor?.HasThis == true);
                observations.Add(Observation("property", unchecked((int)property.MetadataToken.ToUInt32()), FactTypes.ManagedPropertyDeclared, RuleIds.DotNetCompiledMember,
                    $"{typeIdentity}|property:{EncodeIdentityComponent(property.Name)}|{signature}", "property", PropertyProperties(property, signature)));
            }
            foreach (var @event in type.Events)
            {
                var signature = FormatType(@event.EventType);
                observations.Add(Observation("event", unchecked((int)@event.MetadataToken.ToUInt32()), FactTypes.ManagedEventDeclared, RuleIds.DotNetCompiledMember,
                    $"{typeIdentity}|event:{EncodeIdentityComponent(@event.Name)}|type:{signature}", "event", MemberProperties(@event, 0, IsCompilerGenerated(@event), signature)));
            }
        }
        var references = module.AssemblyReferences
            .Select(reference => AssemblyReferenceIdentity(reference.Name, reference.Version?.ToString() ?? "0.0.0.0", reference.Culture, reference.PublicKeyToken))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        EnforceTextLimits(observations, references, limits);
        return new MetadataReadResult(
            assemblyIdentity,
            module.Name,
            module.Mvid.ToString("D", CultureInfo.InvariantCulture),
            targetFramework,
            observations.OrderBy(item => item.Key, StringComparer.Ordinal).ToArray(),
            references,
            assemblyReferenceIdentity);
    }

    private static long PreflightManagedInput(byte[] bytes, CompiledInputLimits limits)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        using var pe = new PEReader(stream, PEStreamOptions.LeaveOpen);
        if (!pe.HasMetadata || pe.PEHeaders.CorHeader is null)
            throw new ManagedInputException("unsupported", "NonManagedBinaryInput");
        if ((pe.PEHeaders.CorHeader.Flags & CorFlags.ILOnly) == 0)
            throw new ManagedInputException("unsupported", "NativeOrMixedModeManagedInput");
        var reader = pe.GetMetadataReader();
        if (!reader.IsAssembly)
            throw new ManagedInputException("unsupported", "ManagedNetmoduleInputUnsupported");
        if (reader.AssemblyFiles.Any(handle => reader.GetAssemblyFile(handle).ContainsMetadata))
            throw new ManagedInputException("unsupported", "MultiModuleManagedAssemblyUnsupported");
        var typeCount = Math.Max(0, reader.TypeDefinitions.Count - 1);
        var memberCount = (long)reader.GetTableRowCount(TableIndex.MethodDef)
            + reader.GetTableRowCount(TableIndex.Field)
            + reader.GetTableRowCount(TableIndex.Property)
            + reader.GetTableRowCount(TableIndex.Event);
        EnforceMetadataLimits(typeCount, memberCount, limits);
        try
        {
            return checked(2L * (1L + typeCount + memberCount + reader.AssemblyReferences.Count));
        }
        catch (OverflowException)
        {
            throw new ManagedInputException("limit-exhausted", "ManagedInputTotalWorkLimitExceeded");
        }
    }

    private static MetadataReadResult ReadWithSystemReflectionMetadata(byte[] bytes, string safeLocator, CompiledInputLimits limits)
    {
        using var stream = new MemoryStream(bytes, writable: false);
        using var pe = new PEReader(stream, PEStreamOptions.LeaveOpen);
        if (!pe.HasMetadata || pe.PEHeaders.CorHeader is null)
            throw new ManagedInputException("unsupported", "NonManagedBinaryInput");
        if ((pe.PEHeaders.CorHeader.Flags & CorFlags.ILOnly) == 0)
            throw new ManagedInputException("unsupported", "NativeOrMixedModeManagedInput");
        var reader = pe.GetMetadataReader();
        if (!reader.IsAssembly)
            throw new ManagedInputException("unsupported", "ManagedNetmoduleInputUnsupported");
        var typeCount = reader.TypeDefinitions.Count - 1;
        var memberCount = (long)reader.GetTableRowCount(TableIndex.MethodDef)
            + reader.GetTableRowCount(TableIndex.Field)
            + reader.GetTableRowCount(TableIndex.Property)
            + reader.GetTableRowCount(TableIndex.Event);
        EnforceMetadataLimits(typeCount, memberCount, limits);
        var module = reader.GetModuleDefinition();
        var moduleName = reader.GetString(module.Name);
        var moduleMvid = reader.GetGuid(module.Mvid).ToString("D", CultureInfo.InvariantCulture);
        var targetFramework = TargetFramework(reader);
        if (targetFramework?.Length > limits.MaxTextLength)
            throw new ManagedInputException("limit-exhausted", "ManagedInputTextLimitExceeded");
        string assemblyIdentity;
        string assemblyName;
        string assemblyVersion;
        string culture;
        byte[] publicKeyToken;
        var assembly = reader.GetAssemblyDefinition();
        assemblyName = reader.GetString(assembly.Name);
        assemblyVersion = assembly.Version.ToString();
        culture = assembly.Culture.IsNil ? string.Empty : reader.GetString(assembly.Culture);
        var publicKey = assembly.PublicKey.IsNil ? [] : reader.GetBlobBytes(assembly.PublicKey);
        publicKeyToken = PublicKeyTokenFromPublicKey(publicKey);
        var assemblyReferenceIdentity = AssemblyReferenceIdentity(assemblyName, assemblyVersion, culture, publicKeyToken);
        assemblyIdentity = AssemblyArtifactIdentity(assemblyReferenceIdentity, moduleName, targetFramework);

        var provider = new MetadataTypeProvider(reader);
        var observations = new List<MetadataObservation>
        {
            Observation("assembly", 0x20000001, FactTypes.ManagedAssemblyDeclared, RuleIds.DotNetCompiledAssembly, assemblyIdentity, "assembly",
                new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["assemblyName"] = assemblyName,
                    ["assemblyVersion"] = assemblyVersion,
                    ["culture"] = NormalizeCulture(culture),
                    ["publicKeyToken"] = PublicKeyToken(publicKeyToken),
                    ["publicKeyTokenState"] = publicKeyToken.Length > 0 ? "present" : "none"
                }),
            Observation("module", 0x00000001, FactTypes.ManagedModuleDeclared, RuleIds.DotNetCompiledAssembly,
                $"{assemblyIdentity}|mvid:{moduleMvid}", "module", EmptyProperties())
        };
        foreach (var handle in reader.TypeDefinitions)
        {
            var type = reader.GetTypeDefinition(handle);
            if (reader.GetString(type.Name) == "<Module>")
                continue;
            var typeIdentity = TypeIdentity(assemblyIdentity, reader, handle);
            observations.Add(Observation("type", MetadataTokens.GetToken(handle), FactTypes.ManagedTypeDeclared, RuleIds.DotNetCompiledMember, typeIdentity, "type",
                MemberProperties(reader.GetString(type.Name), type.GetGenericParameters().Count, false, null)));
            foreach (var methodHandle in type.GetMethods())
            {
                var method = reader.GetMethodDefinition(methodHandle);
                var name = reader.GetString(method.Name);
                var decoded = method.DecodeSignature(provider, genericContext: null);
                var memberKind = name is ".ctor" or ".cctor" ? "constructor" : "method";
                var callingConvention = decoded.Header.CallingConvention == SignatureCallingConvention.VarArgs ? "vararg" : "default";
                var signature = MethodSignature(decoded.ReturnType, decoded.ParameterTypes, method.GetGenericParameters().Count, callingConvention,
                    decoded.Header.IsInstance, (decoded.Header.RawValue & 0x40) != 0);
                observations.Add(Observation(memberKind, MetadataTokens.GetToken(methodHandle), FactTypes.ManagedMethodDeclared, RuleIds.DotNetCompiledMember,
                    $"{typeIdentity}|{memberKind}:{EncodeIdentityComponent(name)}|{signature}", memberKind,
                    MethodProperties(reader, method, name, signature)));
            }
            foreach (var fieldHandle in type.GetFields())
            {
                var field = reader.GetFieldDefinition(fieldHandle);
                var name = reader.GetString(field.Name);
                var signature = field.DecodeSignature(provider, genericContext: null);
                observations.Add(Observation("field", MetadataTokens.GetToken(fieldHandle), FactTypes.ManagedFieldDeclared, RuleIds.DotNetCompiledMember,
                    $"{typeIdentity}|field:{EncodeIdentityComponent(name)}|type:{signature}", "field", MemberProperties(name, 0, false, signature)));
            }
            foreach (var propertyHandle in type.GetProperties())
            {
                var property = reader.GetPropertyDefinition(propertyHandle);
                var name = reader.GetString(property.Name);
                var decoded = property.DecodeSignature(provider, genericContext: null);
                var signature = PropertySignature(
                    decoded.ReturnType,
                    decoded.ParameterTypes,
                    decoded.Header.CallingConvention == SignatureCallingConvention.VarArgs ? "vararg" : "default",
                    decoded.Header.IsInstance);
                observations.Add(Observation("property", MetadataTokens.GetToken(propertyHandle), FactTypes.ManagedPropertyDeclared, RuleIds.DotNetCompiledMember,
                    $"{typeIdentity}|property:{EncodeIdentityComponent(name)}|{signature}", "property", PropertyProperties(reader, property, name, signature)));
            }
            foreach (var eventHandle in type.GetEvents())
            {
                var @event = reader.GetEventDefinition(eventHandle);
                var name = reader.GetString(@event.Name);
                var signature = provider.GetTypeFromEntityHandle(@event.Type);
                observations.Add(Observation("event", MetadataTokens.GetToken(eventHandle), FactTypes.ManagedEventDeclared, RuleIds.DotNetCompiledMember,
                    $"{typeIdentity}|event:{EncodeIdentityComponent(name)}|type:{signature}", "event", MemberProperties(name, 0, false, signature)));
            }
        }
        var references = reader.AssemblyReferences.Select(handle =>
        {
            var reference = reader.GetAssemblyReference(handle);
            var token = reference.PublicKeyOrToken.IsNil ? [] : reader.GetBlobBytes(reference.PublicKeyOrToken);
            if ((reference.Flags & AssemblyFlags.PublicKey) != 0)
                token = PublicKeyTokenFromPublicKey(token);
            return AssemblyReferenceIdentity(reader.GetString(reference.Name), reference.Version.ToString(), reference.Culture.IsNil ? null : reader.GetString(reference.Culture), token);
        }).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        EnforceTextLimits(observations, references, limits);
        return new MetadataReadResult(assemblyIdentity, moduleName, moduleMvid, targetFramework, observations.OrderBy(item => item.Key, StringComparer.Ordinal).ToArray(), references, assemblyReferenceIdentity);
    }

    private static MetadataObservation Observation(string kind, int token, string factType, string ruleId, string identity, string memberKind, IReadOnlyDictionary<string, string> properties) =>
        new($"{kind}:{Token(token)}", identity, factType, ruleId, memberKind, Token(token), properties);

    internal static string TypeIdentity(string assemblyIdentity, CecilTypeDefinition type)
    {
        var (ns, names) = CecilTypeName(type);
        return $"{assemblyIdentity}|type:{MetadataTypePath(ns, names)}|arity:{type.GenericParameters.Count}";
    }

    private static string TypeIdentity(string assemblyIdentity, MetadataReader reader, TypeDefinitionHandle handle)
    {
        var (ns, names, arity) = MetadataTypeName(reader, handle);
        return $"{assemblyIdentity}|type:{MetadataTypePath(ns, names)}|arity:{arity}";
    }

    internal static (string Namespace, IReadOnlyList<string> Names) CecilTypeName(CecilTypeReference type)
    {
        var names = new Stack<string>();
        CecilTypeReference? current = type;
        while (current is not null)
        {
            names.Push(current.Name);
            current = current.DeclaringType;
        }
        var outer = type;
        while (outer.DeclaringType is not null)
            outer = outer.DeclaringType;
        return (outer.Namespace ?? string.Empty, names.ToArray());
    }

    internal static (string Namespace, IReadOnlyList<string> Names, int Arity) MetadataTypeName(MetadataReader reader, TypeDefinitionHandle handle)
    {
        var names = new Stack<string>();
        var current = handle;
        string ns = string.Empty;
        var arity = reader.GetTypeDefinition(handle).GetGenericParameters().Count;
        while (!current.IsNil)
        {
            var definition = reader.GetTypeDefinition(current);
            names.Push(reader.GetString(definition.Name));
            if (definition.GetDeclaringType().IsNil)
                ns = definition.Namespace.IsNil ? string.Empty : reader.GetString(definition.Namespace);
            current = definition.GetDeclaringType();
        }
        return (ns, names.ToArray(), arity);
    }

    internal static string MethodSignature<T>(T returnType, IEnumerable<T> parameters, int genericArity, string callingConvention, bool hasThis, bool explicitThis) =>
        $"arity:{genericArity}|call:{callingConvention}|hasThis:{hasThis.ToString().ToLowerInvariant()}|explicitThis:{explicitThis.ToString().ToLowerInvariant()}|({string.Join(",", parameters)})->{returnType}";

    private static string PropertySignature<T>(T propertyType, IEnumerable<T> parameters, string callingConvention, bool hasThis) =>
        $"call:{callingConvention}|hasThis:{hasThis.ToString().ToLowerInvariant()}|({string.Join(",", parameters)})->{propertyType}";

    private static string FunctionPointerSignature<T>(T returnType, IEnumerable<T> parameters, int genericArity, string callingConvention, bool hasThis, bool explicitThis, int requiredParameterCount) =>
        MethodSignature(returnType, parameters, genericArity, callingConvention, hasThis, explicitThis)
        + $"|requiredParameters:{requiredParameterCount.ToString(CultureInfo.InvariantCulture)}";

    private static string FunctionPointerCallingConvention(MethodCallingConvention callingConvention) => ((int)callingConvention & 0x0f) switch
    {
        0 => "default",
        1 => "cdecl",
        2 => "stdcall",
        3 => "thiscall",
        4 => "fastcall",
        5 => "vararg",
        var value => $"unknown-{value.ToString(CultureInfo.InvariantCulture)}"
    };

    private static string FunctionPointerCallingConvention(SignatureCallingConvention callingConvention) => callingConvention switch
    {
        SignatureCallingConvention.Default => "default",
        SignatureCallingConvention.CDecl => "cdecl",
        SignatureCallingConvention.StdCall => "stdcall",
        SignatureCallingConvention.ThisCall => "thiscall",
        SignatureCallingConvention.FastCall => "fastcall",
        SignatureCallingConvention.VarArgs => "vararg",
        SignatureCallingConvention.Unmanaged => "unmanaged",
        _ => $"unknown-{((int)callingConvention).ToString(CultureInfo.InvariantCulture)}"
    };

    internal static string FormatArrayShape(int rank, IEnumerable<int> sizes, IEnumerable<int> lowerBounds) =>
        $"[rank={rank};sizes={FormatShapeValues(sizes)};lowerBounds={FormatShapeValues(lowerBounds)}]";

    private static string FormatShapeValues(IEnumerable<int> values)
    {
        var materialized = values.ToArray();
        return materialized.Length == 0 ? "-" : string.Join(",", materialized.Select(value => value.ToString(CultureInfo.InvariantCulture)));
    }

    internal static string FormatType(CecilTypeReference type) => FormatType(type, 0);

    private static string FormatType(CecilTypeReference type, int nesting)
    {
        if (nesting > MaximumSignatureTypeNesting)
            throw new ManagedInputException("limit-exhausted", "ManagedInputSignatureNestingLimitExceeded");
        if (type is CecilCustomModifier modifier)
            return $"{(modifier is RequiredModifierType ? "modreq" : "modopt")}({FormatType(modifier.ModifierType, nesting + 1)}) {FormatType(modifier.ElementType, nesting + 1)}";
        if (type is ByReferenceType byReference)
            return FormatType(byReference.ElementType, nesting + 1) + "&";
        if (type is CecilPointerType pointer)
            return FormatType(pointer.ElementType, nesting + 1) + "*";
        if (type is CecilArrayType array)
            return FormatType(array.ElementType, nesting + 1) + (array.IsVector
                ? "[]"
                : FormatArrayShape(
                    array.Rank,
                    array.Dimensions.TakeWhile(dimension => dimension.LowerBound.HasValue && dimension.UpperBound.HasValue)
                        .Select(dimension => checked(dimension.UpperBound!.Value - dimension.LowerBound!.Value + 1)),
                    array.Dimensions.TakeWhile(dimension => dimension.LowerBound.HasValue)
                        .Select(dimension => dimension.LowerBound!.Value)));
        if (type is CecilGenericInstanceType generic)
            return FormatType(generic.ElementType, nesting + 1) + "<" + string.Join(",", generic.GenericArguments.Select(argument => FormatType(argument, nesting + 1))) + ">";
        if (type is CecilGenericParameter parameter)
            return parameter.Type == GenericParameterType.Method ? $"!!{parameter.Position}" : $"!{parameter.Position}";
        if (type is CecilFunctionPointerType functionPointer)
        {
            var requiredParameterCount = functionPointer.Parameters.TakeWhile(item => item.ParameterType is not CecilSentinelType).Count();
            return "fnptr:" + FunctionPointerSignature(
                FormatType(functionPointer.ReturnType, nesting + 1),
                functionPointer.Parameters.Select(item => FormatType(item.ParameterType, nesting + 1)),
                functionPointer.GenericParameters.Count,
                FunctionPointerCallingConvention(functionPointer.CallingConvention),
                functionPointer.HasThis,
                functionPointer.ExplicitThis,
                requiredParameterCount);
        }
        if (type is CecilSentinelType sentinel)
            return FormatType(sentinel.ElementType, nesting + 1);
        var (ns, names) = CecilTypeName(type);
        var typePath = "type(" + MetadataTypePath(ns, names) + ")";
        return IsPrimitiveSignatureType(type.MetadataType)
            ? typePath
            : "scope(" + CecilAssemblyScope(type) + ")" + typePath;
    }

    private static bool IsPrimitiveSignatureType(MetadataType type) => type is
        MetadataType.Void or MetadataType.Boolean or MetadataType.Char or MetadataType.SByte or MetadataType.Byte
        or MetadataType.Int16 or MetadataType.UInt16 or MetadataType.Int32 or MetadataType.UInt32
        or MetadataType.Int64 or MetadataType.UInt64 or MetadataType.Single or MetadataType.Double
        or MetadataType.String or MetadataType.TypedByReference or MetadataType.IntPtr or MetadataType.UIntPtr
        or MetadataType.Object;

    internal static string CecilAssemblyScope(CecilTypeReference type)
    {
        var assemblyName = type.Scope switch
        {
            AssemblyNameReference reference => reference,
            Mono.Cecil.ModuleDefinition module => module.Assembly?.Name,
            _ => null
        };
        if (assemblyName is null)
            throw new ManagedInputException("unsupported", "ManagedSignatureAssemblyScopeUnavailable");
        return AssemblyReferenceIdentity(
            assemblyName.Name,
            assemblyName.Version?.ToString() ?? "0.0.0.0",
            assemblyName.Culture,
            assemblyName.PublicKeyToken);
    }

    private static IReadOnlyDictionary<string, string> MemberProperties(IMemberDefinition member, int genericArity, bool generated, string? signature = null) =>
        MemberProperties(member.Name, genericArity, generated, signature);

    private static IReadOnlyDictionary<string, string> MethodProperties(Mono.Cecil.MethodDefinition method, string signature)
    {
        var result = CopyProperties(MemberProperties(method, method.GenericParameters.Count, IsCompilerGenerated(method), signature));
        result["optionalParameterOrdinals"] = string.Join(",", method.Parameters
            .Select((parameter, ordinal) => (parameter, ordinal))
            .Where(item => item.parameter.IsOptional)
            .Select(item => item.ordinal.ToString(CultureInfo.InvariantCulture)));
        return result;
    }

    private static IReadOnlyDictionary<string, string> PropertyProperties(Mono.Cecil.PropertyDefinition property, string signature)
    {
        var result = CopyProperties(MemberProperties(property, 0, IsCompilerGenerated(property), signature));
        result["optionalParameterOrdinals"] = string.Join(",", property.Parameters
            .Select((parameter, ordinal) => (parameter, ordinal))
            .Where(item => item.parameter.IsOptional)
            .Select(item => item.ordinal.ToString(CultureInfo.InvariantCulture)));
        return result;
    }

    private static IReadOnlyDictionary<string, string> MethodProperties(
        MetadataReader reader,
        System.Reflection.Metadata.MethodDefinition method,
        string name,
        string signature)
    {
        var result = CopyProperties(MemberProperties(name, method.GetGenericParameters().Count, false, signature));
        result["optionalParameterOrdinals"] = string.Join(",", method.GetParameters()
            .Select(handle => reader.GetParameter(handle))
            .Where(parameter => parameter.SequenceNumber > 0 && (parameter.Attributes & System.Reflection.ParameterAttributes.Optional) != 0)
            .Select(parameter => (parameter.SequenceNumber - 1).ToString(CultureInfo.InvariantCulture))
            .OrderBy(value => value, StringComparer.Ordinal));
        return result;
    }

    private static IReadOnlyDictionary<string, string> PropertyProperties(
        MetadataReader reader,
        System.Reflection.Metadata.PropertyDefinition property,
        string name,
        string signature)
    {
        var result = CopyProperties(MemberProperties(name, 0, false, signature));
        var accessors = property.GetAccessors();
        var accessorHandle = !accessors.Getter.IsNil ? accessors.Getter : accessors.Setter;
        if (accessorHandle.IsNil)
        {
            result["optionalParameterOrdinals"] = string.Empty;
            return result;
        }
        var parameters = reader.GetMethodDefinition(accessorHandle).GetParameters()
            .Select(handle => reader.GetParameter(handle))
            .Where(parameter => parameter.SequenceNumber > 0)
            .OrderBy(parameter => parameter.SequenceNumber)
            .ToArray();
        var propertyParameterCount = property.DecodeSignature(new MetadataTypeProvider(reader), genericContext: null).ParameterTypes.Length;
        result["optionalParameterOrdinals"] = string.Join(",", parameters
            .Take(propertyParameterCount)
            .Where(parameter => (parameter.Attributes & System.Reflection.ParameterAttributes.Optional) != 0)
            .Select(parameter => (parameter.SequenceNumber - 1).ToString(CultureInfo.InvariantCulture)));
        return result;
    }

    private static IReadOnlyDictionary<string, string> MemberProperties(string name, int genericArity, bool generated, string? signature)
    {
        var result = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["compilerGenerated"] = generated.ToString().ToLowerInvariant(),
            ["genericArity"] = genericArity.ToString(CultureInfo.InvariantCulture),
            ["genericConstructionState"] = genericArity > 0 ? "open-definition" : "non-generic",
            ["metadataName"] = name
        };
        if (signature is not null)
            result["signature"] = signature;
        return result;
    }

    private static SortedDictionary<string, string> CopyProperties(IReadOnlyDictionary<string, string> source)
    {
        var result = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in source)
            result[key] = value;
        return result;
    }

    private static IReadOnlyDictionary<string, string> EmptyProperties() =>
        new SortedDictionary<string, string>(StringComparer.Ordinal);

    private static bool IsCompilerGenerated(Mono.Cecil.ICustomAttributeProvider provider) =>
        provider.HasCustomAttributes && provider.CustomAttributes.Any(attribute =>
            attribute.AttributeType.FullName == "System.Runtime.CompilerServices.CompilerGeneratedAttribute");

    internal static IEnumerable<CecilTypeDefinition> FlattenTypes(IEnumerable<CecilTypeDefinition> roots)
    {
        var stack = new Stack<CecilTypeDefinition>(roots.Reverse());
        while (stack.Count > 0)
        {
            var type = stack.Pop();
            yield return type;
            for (var index = type.NestedTypes.Count - 1; index >= 0; index--)
                stack.Push(type.NestedTypes[index]);
        }
    }

    internal static string? TargetFramework(Mono.Cecil.AssemblyDefinition? assembly)
    {
        if (assembly is null)
            return null;
        var attribute = assembly.CustomAttributes.FirstOrDefault(item => item.AttributeType.FullName == "System.Runtime.Versioning.TargetFrameworkAttribute");
        return attribute?.ConstructorArguments.Count == 1 && attribute.ConstructorArguments[0].Value is string value ? value : null;
    }

    internal static string? TargetFramework(MetadataReader reader)
    {
        foreach (var handle in reader.GetAssemblyDefinition().GetCustomAttributes())
        {
            var attribute = reader.GetCustomAttribute(handle);
            if (attribute.Constructor.Kind != HandleKind.MemberReference)
                continue;
            var constructor = reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor);
            if (constructor.Parent.Kind != HandleKind.TypeReference)
                continue;
            var type = reader.GetTypeReference((TypeReferenceHandle)constructor.Parent);
            if (reader.GetString(type.Namespace) != "System.Runtime.Versioning"
                || reader.GetString(type.Name) != "TargetFrameworkAttribute")
                continue;
            var value = reader.GetBlobReader(attribute.Value);
            return value.ReadUInt16() == 1 ? value.ReadSerializedString() : null;
        }
        return null;
    }

    private static void EnforceMetadataLimits(long typeCount, long memberCount, CompiledInputLimits limits)
    {
        if (typeCount > limits.MaxTypeCount)
            throw new ManagedInputException("limit-exhausted", "ManagedInputTypeCountLimitExceeded");
        if (memberCount > limits.MaxMemberCount)
            throw new ManagedInputException("limit-exhausted", "ManagedInputMemberCountLimitExceeded");
    }

    private static void EnforceTextLimits(
        IEnumerable<MetadataObservation> observations,
        IEnumerable<string> assemblyReferences,
        CompiledInputLimits limits)
    {
        if (observations.Any(item => item.Identity.Length > limits.MaxTextLength
            || item.Properties.Any(property => property.Key.Length > limits.MaxTextLength || property.Value.Length > limits.MaxTextLength))
            || assemblyReferences.Any(reference => reference.Length > limits.MaxTextLength))
        {
            throw new ManagedInputException("limit-exhausted", "ManagedInputTextLimitExceeded");
        }
    }

    private static object ToPreDigestOutcome(EvaluatedInput item) => new
    {
        safeLocator = item.Descriptor.SafeLocator,
        role = item.Descriptor.Role,
        item.Outcome,
        item.ProvenanceState,
        rawFileSha256 = item.RawSha256 ?? string.Empty,
        provenanceBindingInputSha256 = item.BindingDigest,
        assemblyIdentity = item.AssemblyIdentity ?? string.Empty,
        moduleName = item.ModuleName ?? string.Empty,
        moduleMvid = item.ModuleMvid ?? string.Empty,
        gapKinds = item.GapKinds.OrderBy(value => value, StringComparer.Ordinal).Distinct(StringComparer.Ordinal).ToArray(),
        assemblyReferences = item.AssemblyReferences.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
        dependencyResolutionOutcomes = item.DependencyResolutionOutcomes.OrderBy(value => value, StringComparer.Ordinal).ToArray()
    };

    private static string PrivacyProjectedInputDigest(EvaluatedInput item) => CanonicalDigest(new
    {
        item.Descriptor.SafeLocator,
        item.Descriptor.Role,
        item.Outcome,
        item.ProvenanceState,
        item.AssemblyIdentity,
        item.ModuleName,
        item.ModuleMvid,
        gapKinds = item.GapKinds.OrderBy(value => value, StringComparer.Ordinal).Distinct(StringComparer.Ordinal).ToArray()
    });

    private static string GeneratorSha256()
    {
        var path = typeof(ManagedMetadataExtractor).Assembly.Location;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            throw new InvalidOperationException("The exact managed metadata generator bytes are unavailable.");
        return Sha256(File.ReadAllBytes(path));
    }

    internal static string CanonicalDigest<T>(T value)
    {
        var json = JsonSerializer.Serialize(value, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        using var document = JsonDocument.Parse(json);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
            WriteCanonical(writer, document.RootElement);
        return Sha256(stream.ToArray());
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject().OrderBy(item => item.Name, StringComparer.Ordinal))
                {
                    writer.WritePropertyName(property.Name);
                    WriteCanonical(writer, property.Value);
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                    WriteCanonical(writer, item);
                writer.WriteEndArray();
                break;
            default:
                element.WriteTo(writer);
                break;
        }
    }

    internal static string AssemblyReferenceIdentity(string name, string version, string? culture, byte[]? publicKeyToken) =>
        $"assembly:name:{EncodeIdentityComponent(name)}|version:{EncodeIdentityComponent(version)}|culture:{EncodeIdentityComponent(NormalizeCulture(culture))}|publicKeyToken:{EncodeIdentityComponent(PublicKeyToken(publicKeyToken))}";

    internal static string AssemblyArtifactIdentity(string referenceIdentity, string moduleName, string? targetFramework) =>
        $"{referenceIdentity}|module:{EncodeIdentityComponent(moduleName)}|targetFramework:{EncodeIdentityComponent(string.IsNullOrWhiteSpace(targetFramework) ? "unknown" : targetFramework)}";

    internal static string EncodeIdentityComponent(string value) => $"{value.Length.ToString(CultureInfo.InvariantCulture)}:{value}";

    internal static string MetadataTypePath(string @namespace, IEnumerable<string> names) =>
        $"namespace:{EncodeIdentityComponent(@namespace)}|names:{string.Concat(names.Select(EncodeIdentityComponent))}";

    private static string NormalizeCulture(string? culture) => string.IsNullOrWhiteSpace(culture) ? "neutral" : culture;
    private static string PublicKeyToken(byte[]? token) => token is { Length: > 0 } ? Convert.ToHexString(token).ToLowerInvariant() : "null";
    private static byte[] PublicKeyTokenFromPublicKey(byte[] key)
    {
        if (key.Length == 0)
            return [];
        var hash = SHA1.HashData(key);
        return hash[^8..].Reverse().ToArray();
    }
    internal static string Token(int token) => $"0x{unchecked((uint)token):x8}";
    internal static string Token(uint token) => $"0x{token:x8}";
    internal static string Sha256(byte[] value) => Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();
    private static bool IsSha256(string? value) => value is { Length: 64 } && value.All(character => character is (>= '0' and <= '9') or (>= 'a' and <= 'f'));

    private static bool IsCommitSha(string? value) => value is { Length: 40 } && value.All(character => character is (>= '0' and <= '9') or (>= 'a' and <= 'f') or (>= 'A' and <= 'F'));
    private static bool IsRecoverableMetadataException(Exception exception) => exception is
        BadImageFormatException or IOException or InvalidOperationException or ArgumentException or NotSupportedException
        or TypeLoadException or FormatException or OverflowException or IndexOutOfRangeException;

    private static byte[] ReadBoundedFile(string path, long maximumBytes, string gapKind)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81_920, FileOptions.SequentialScan);
        using var output = new MemoryStream((int)Math.Min(maximumBytes, 1_048_576));
        var buffer = new byte[81_920];
        long total = 0;
        while (true)
        {
            var read = stream.Read(buffer, 0, buffer.Length);
            if (read == 0)
                return output.ToArray();
            total += read;
            if (total > maximumBytes)
                throw new ManagedInputException("limit-exhausted", gapKind);
            output.Write(buffer, 0, read);
        }
    }

    private static string ResolvePath(string repoPath, string path) => Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(repoPath, path));
    private static string SafeFileName(string value)
    {
        var safe = new string(value.Select(character => char.IsLetterOrDigit(character) || character is '.' or '-' or '_' ? character : '_').ToArray());
        return string.IsNullOrWhiteSpace(safe) ? "input.bin" : safe;
    }
    private static IReadOnlyList<string> CleanPaths(IReadOnlyList<string>? values) =>
        (values ?? []).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim()).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
    private static void ValidateLimits(CompiledInputLimits limits)
    {
        if (limits.MaxArtifactCount <= 0 || limits.MaxFileSizeBytes <= 0 || limits.MaxTypeCount <= 0 || limits.MaxMemberCount <= 0 || limits.MaxTextLength <= 0 || limits.MaxTotalWorkUnits <= 0)
            throw new ArgumentException("Compiled input limits must all be positive.");
        if (limits.MaxTextLength < MinimumProjectedTextLength)
            throw new ArgumentException($"Compiled input maximum text length must be at least {MinimumProjectedTextLength} characters.");
    }

    internal sealed record MetadataObservation(
        string Key,
        string Identity,
        string FactType,
        string RuleId,
        string MemberKind,
        string MetadataToken,
        IReadOnlyDictionary<string, string> Properties);

    internal sealed record ReaderDisagreement(string Key, string? CecilIdentity, string? SystemReflectionMetadataIdentity);

    private sealed record MetadataReadResult(
        string AssemblyIdentity,
        string ModuleName,
        string ModuleMvid,
        string? TargetFramework,
        IReadOnlyList<MetadataObservation> Observations,
        IReadOnlyList<string> AssemblyReferences,
        string AssemblyReferenceIdentity);

    private sealed record InputDescriptor(
        string FullPath,
        string SafeLocator,
        string Role,
        bool IsExternal,
        bool SafeLocatorTextLimitExceeded);

    private sealed class EvaluatedInput
    {
        public EvaluatedInput(InputDescriptor descriptor, string outcome, string provenanceState, string? rawSha256, string bindingDigest,
            string? assemblyIdentity, string? assemblyReferenceIdentity, string? moduleName, string? moduleMvid, IEnumerable<string> gapKinds,
            IReadOnlyList<string> assemblyReferences, IReadOnlyList<CompiledEvidenceCandidate> candidates)
        {
            Descriptor = descriptor;
            Outcome = outcome;
            ProvenanceState = provenanceState;
            RawSha256 = rawSha256;
            BindingDigest = bindingDigest;
            AssemblyIdentity = assemblyIdentity;
            AssemblyReferenceIdentity = assemblyReferenceIdentity;
            ModuleName = moduleName;
            ModuleMvid = moduleMvid;
            GapKinds = gapKinds.ToList();
            AssemblyReferences = assemblyReferences;
            DependencyResolutionOutcomes = [];
            Candidates = candidates;
        }
        public InputDescriptor Descriptor { get; }
        public string Outcome { get; }
        public string ProvenanceState { get; }
        public string? RawSha256 { get; }
        public string BindingDigest { get; }
        public string? AssemblyIdentity { get; }
        public string? AssemblyReferenceIdentity { get; }
        public string? ModuleName { get; }
        public string? ModuleMvid { get; }
        public List<string> GapKinds { get; }
        public IReadOnlyList<string> AssemblyReferences { get; }
        public List<string> DependencyResolutionOutcomes { get; }
        public IReadOnlyList<CompiledEvidenceCandidate> Candidates { get; }
        public static EvaluatedInput Gap(InputDescriptor descriptor, string outcome, string gapKind, string? rawSha256 = null)
        {
            var digest = CanonicalDigest(new { descriptor.SafeLocator, descriptor.Role, outcome, gapKind });
            return new EvaluatedInput(descriptor, outcome, outcome, rawSha256, digest, null, null, null, null, [gapKind], [], []);
        }
    }

    private sealed record BindingClassification(string State, string GapKind, string Digest, CompiledBindingReceipt? Receipt);
    private sealed record ReceiptReadResult(
        IReadOnlyList<CompiledBindingReceipt> Receipts,
        IReadOnlyList<string> Gaps,
        IReadOnlyList<string> BindingDigests);
    private sealed record CompiledBindingReceiptDocument(string SchemaVersion, IReadOnlyList<CompiledBindingReceipt> Bindings);
    private sealed record CompiledBindingReceipt(
        string SchemaVersion,
        string SafeLocator,
        string ArtifactSha256,
        string? AssemblyIdentity,
        string? BinarySourceRepository,
        string? BinarySourceCommitSha,
        string? BinarySourceCommitRelation,
        string? BinaryBuildIdentity);

    private sealed class ManagedInputException(string outcome, string gapKind) : Exception(gapKind)
    {
        public string Outcome { get; } = outcome;
        public string GapKind { get; } = gapKind;
    }

    private sealed class WorkBudget(long remaining)
    {
        private long _remaining = remaining;

        public bool TryConsume(long units)
        {
            if (units < 0 || units > _remaining)
                return false;
            _remaining -= units;
            return true;
        }
    }

    internal sealed class RejectingAssemblyResolver : IAssemblyResolver
    {
        public Mono.Cecil.AssemblyDefinition Resolve(AssemblyNameReference name) => throw new AssemblyResolutionException(name);
        public Mono.Cecil.AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters) => throw new AssemblyResolutionException(name);
        public void Dispose() { }
    }

    internal sealed class MetadataTypeProvider(MetadataReader reader) : ISignatureTypeProvider<string, object?>
    {
        private readonly string _definitionScope = DefinitionScope(reader);
        public string GetArrayType(string elementType, ArrayShape shape) => elementType + FormatArrayShape(shape.Rank, shape.Sizes, shape.LowerBounds);
        public string GetByReferenceType(string elementType) => elementType + "&";
        public string GetFunctionPointerType(MethodSignature<string> signature) => "fnptr:" + FunctionPointerSignature(
            signature.ReturnType,
            signature.ParameterTypes,
            signature.GenericParameterCount,
            FunctionPointerCallingConvention(signature.Header.CallingConvention),
            signature.Header.IsInstance,
            (signature.Header.RawValue & 0x40) != 0,
            signature.RequiredParameterCount);
        public string GetGenericInstantiation(string genericType, ImmutableArray<string> typeArguments) => genericType + "<" + string.Join(",", typeArguments) + ">";
        public string GetGenericMethodParameter(object? genericContext, int index) => $"!!{index}";
        public string GetGenericTypeParameter(object? genericContext, int index) => $"!{index}";
        public string GetModifiedType(string modifier, string unmodifiedType, bool isRequired) => $"{(isRequired ? "modreq" : "modopt")}({modifier}) {unmodifiedType}";
        public string GetPinnedType(string elementType) => elementType + " pinned";
        public string GetPointerType(string elementType) => elementType + "*";
        public string GetPrimitiveType(PrimitiveTypeCode typeCode) => typeCode switch
        {
            PrimitiveTypeCode.Void => SystemType("Void"),
            PrimitiveTypeCode.Boolean => SystemType("Boolean"),
            PrimitiveTypeCode.Char => SystemType("Char"),
            PrimitiveTypeCode.SByte => SystemType("SByte"),
            PrimitiveTypeCode.Byte => SystemType("Byte"),
            PrimitiveTypeCode.Int16 => SystemType("Int16"),
            PrimitiveTypeCode.UInt16 => SystemType("UInt16"),
            PrimitiveTypeCode.Int32 => SystemType("Int32"),
            PrimitiveTypeCode.UInt32 => SystemType("UInt32"),
            PrimitiveTypeCode.Int64 => SystemType("Int64"),
            PrimitiveTypeCode.UInt64 => SystemType("UInt64"),
            PrimitiveTypeCode.Single => SystemType("Single"),
            PrimitiveTypeCode.Double => SystemType("Double"),
            PrimitiveTypeCode.String => SystemType("String"),
            PrimitiveTypeCode.TypedReference => SystemType("TypedReference"),
            PrimitiveTypeCode.IntPtr => SystemType("IntPtr"),
            PrimitiveTypeCode.UIntPtr => SystemType("UIntPtr"),
            PrimitiveTypeCode.Object => SystemType("Object"),
            _ => "primitive:" + ((int)typeCode).ToString(CultureInfo.InvariantCulture)
        };
        public string GetSZArrayType(string elementType) => elementType + "[]";
        public string GetTypeFromDefinition(MetadataReader metadataReader, TypeDefinitionHandle handle, byte rawTypeKind) => TypeName(metadataReader, handle);
        public string GetTypeFromReference(MetadataReader metadataReader, TypeReferenceHandle handle, byte rawTypeKind) => TypeName(metadataReader, handle);
        public string GetTypeFromSpecification(MetadataReader metadataReader, object? genericContext, TypeSpecificationHandle handle, byte rawTypeKind) => metadataReader.GetTypeSpecification(handle).DecodeSignature(this, genericContext);
        public string GetTypeFromEntityHandle(EntityHandle handle) => handle.Kind switch
        {
            HandleKind.TypeDefinition => TypeName(reader, (TypeDefinitionHandle)handle),
            HandleKind.TypeReference => TypeName(reader, (TypeReferenceHandle)handle),
            HandleKind.TypeSpecification => reader.GetTypeSpecification((TypeSpecificationHandle)handle).DecodeSignature(this, null),
            _ => "<unsupported-type>"
        };
        private string TypeName(MetadataReader metadataReader, TypeDefinitionHandle handle)
        {
            var (ns, names, _) = MetadataTypeName(metadataReader, handle);
            return "scope(" + _definitionScope + ")type(" + MetadataTypePath(ns, names) + ")";
        }
        private static string SystemType(string name) => "type(" + MetadataTypePath("System", [name]) + ")";
        private static string TypeName(MetadataReader metadataReader, TypeReferenceHandle handle)
        {
            var names = new Stack<string>();
            var current = handle;
            string ns = string.Empty;
            EntityHandle resolutionScope = default;
            while (!current.IsNil)
            {
                var reference = metadataReader.GetTypeReference(current);
                names.Push(metadataReader.GetString(reference.Name));
                ns = reference.Namespace.IsNil ? ns : metadataReader.GetString(reference.Namespace);
                resolutionScope = reference.ResolutionScope;
                current = reference.ResolutionScope.Kind == HandleKind.TypeReference ? (TypeReferenceHandle)reference.ResolutionScope : default;
            }
            return "scope(" + ResolutionScope(metadataReader, resolutionScope) + ")type(" + MetadataTypePath(ns, names) + ")";
        }

        private static string DefinitionScope(MetadataReader metadataReader)
        {
            if (!metadataReader.IsAssembly)
                throw new ManagedInputException("unsupported", "ManagedSignatureAssemblyScopeUnavailable");
            var assembly = metadataReader.GetAssemblyDefinition();
            var token = assembly.PublicKey.IsNil ? [] : PublicKeyTokenFromPublicKey(metadataReader.GetBlobBytes(assembly.PublicKey));
            return AssemblyReferenceIdentity(
                metadataReader.GetString(assembly.Name),
                assembly.Version.ToString(),
                assembly.Culture.IsNil ? null : metadataReader.GetString(assembly.Culture),
                token);
        }

        private static string ResolutionScope(MetadataReader metadataReader, EntityHandle scope)
        {
            if (scope.Kind == HandleKind.AssemblyReference)
            {
                var reference = metadataReader.GetAssemblyReference((AssemblyReferenceHandle)scope);
                var token = reference.PublicKeyOrToken.IsNil ? [] : metadataReader.GetBlobBytes(reference.PublicKeyOrToken);
                if ((reference.Flags & AssemblyFlags.PublicKey) != 0)
                    token = PublicKeyTokenFromPublicKey(token);
                return AssemblyReferenceIdentity(
                    metadataReader.GetString(reference.Name),
                    reference.Version.ToString(),
                    reference.Culture.IsNil ? null : metadataReader.GetString(reference.Culture),
                    token);
            }
            if (scope.Kind == HandleKind.ModuleDefinition)
                return DefinitionScope(metadataReader);
            throw new ManagedInputException("unsupported", "ManagedSignatureAssemblyScopeUnavailable");
        }
    }
}
