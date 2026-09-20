using System.Security.Cryptography;
using System.Text;

namespace TraceMap.Core;

internal static class SourceMetadataReconciler
{
    public const string SchemaVersion = "source-metadata-reconciliation.v1";
    public const string Limitation = "An exact source-to-metadata identity edge proves only that one compiler-resolved source declaration and one bound metadata declaration share the complete documented identity for this scan; it does not prove runtime loading, execution, dispatch, reachability, behavior, PDB correspondence, IL ownership, or rewrite equivalence.";
    public const string GapLimitation = "A reconciliation gap is bounded to the compiler-resolved source candidates and explicitly admitted compiled inputs in this scan; it does not prove source or metadata absence and never changes source analysis level.";

    public static IReadOnlyList<CodeFact> Reconcile(
        ScanManifest manifest,
        IReadOnlyList<SourceMetadataIdentityCandidate>? sourceCandidates,
        IReadOnlyList<CodeFact> compiledFacts,
        IReadOnlyList<FileInventoryItem> inventory)
    {
        if (manifest.CompiledInputProvenance is null)
            return [];

        var results = new List<CodeFact>();
        var metadataFacts = compiledFacts
            .Where(IsMetadataDeclaration)
            .OrderBy(fact => fact.TargetSymbol, StringComparer.Ordinal)
            .ThenBy(fact => fact.FactId, StringComparer.Ordinal)
            .ToArray();

        foreach (var group in (sourceCandidates ?? [])
            .GroupBy(candidate => candidate.SourceIdentity, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            var candidates = group
                .OrderBy(candidate => candidate.MetadataIdentity, StringComparer.Ordinal)
                .ThenBy(candidate => candidate.Evidence.FilePath, StringComparer.Ordinal)
                .ThenBy(candidate => candidate.Evidence.StartLine)
                .ToArray();
            var first = candidates[0];
            var metadataIdentities = candidates
                .Select(candidate => candidate.MetadataIdentity)
                .Where(identity => identity is not null)
                .Select(identity => identity!)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(identity => identity, StringComparer.Ordinal)
                .ToArray();
            var sourceObservation = CreateSourceObservation(manifest, first, candidates, metadataIdentities);
            results.Add(sourceObservation);

            if (metadataIdentities.Length == 0)
            {
                results.Add(CreateGap(manifest, first, sourceObservation, "SourceMetadataIdentityIncomplete", 0,
                    candidates.Select(candidate => candidate.IncompleteReason ?? "SourceMetadataIdentityIncomplete")));
                continue;
            }
            if (metadataIdentities.Length != 1)
            {
                results.Add(CreateGap(manifest, first, sourceObservation, "SourceMetadataIdentityAmbiguous", metadataIdentities.Length, metadataIdentities));
                continue;
            }

            var metadataIdentity = metadataIdentities[0];
            var exactMatches = metadataFacts
                .Where(fact => string.Equals(fact.TargetSymbol, metadataIdentity, StringComparison.Ordinal))
                .ToArray();
            var relationshipProofs = candidates.Select(candidate => candidate.RelationshipProof).Distinct(StringComparer.Ordinal).ToArray();
            var compilerGeneratedRelationshipProven = relationshipProofs.Any(proof =>
                proof is "roslyn-associated-property-accessor" or "roslyn-associated-event-accessor");
            exactMatches = exactMatches.Where(fact =>
                fact.Properties.GetValueOrDefault("compilerGenerated") != "true" || compilerGeneratedRelationshipProven).ToArray();

            if (exactMatches.Length == 0)
            {
                results.Add(CreateGap(manifest, first, sourceObservation, "SourceMetadataReconciliationZeroCandidate", 0, []));
                continue;
            }
            if (exactMatches.Length != 1)
            {
                results.Add(CreateGap(manifest, first, sourceObservation, "SourceMetadataReconciliationMultipleCandidates", exactMatches.Length,
                    exactMatches.Select(fact => fact.FactId)));
                continue;
            }

            var metadataFact = exactMatches[0];
            var sourceOptionalParameters = string.Join(",", candidates.SelectMany(candidate => candidate.OptionalParameterOrdinals).Distinct().OrderBy(value => value));
            var compiledOptionalParameters = metadataFact.Properties.GetValueOrDefault("optionalParameterOrdinals") ?? string.Empty;
            if (!string.Equals(sourceOptionalParameters, compiledOptionalParameters, StringComparison.Ordinal))
            {
                results.Add(CreateGap(manifest, first, sourceObservation, "SourceMetadataOptionalParameterMismatch", 1,
                    [metadataFact.FactId, $"source:{sourceOptionalParameters}", $"metadata:{compiledOptionalParameters}"]));
                continue;
            }
            if (metadataFact.Properties.GetValueOrDefault("sourceReconciliationEligibility") != "eligible")
            {
                results.Add(CreateGap(manifest, first, sourceObservation, "SourceMetadataReconciliationCompiledEvidenceUnacceptable", 1,
                    [metadataFact.FactId, metadataFact.Properties.GetValueOrDefault("sourceReconciliationBlocker") ?? "unknown"]));
                continue;
            }

            results.Add(FactFactory.Create(
                manifest,
                FactTypes.SourceMetadataIdentityReconciled,
                RuleIds.DotNetCompiledSourceIdentity,
                EvidenceTiers.Tier1Semantic,
                first.Evidence with
                {
                    ExtractorId = nameof(SourceMetadataReconciler),
                    ExtractorVersion = ScannerVersions.SourceMetadataReconciliationExtractor
                },
                projectPath: first.ProjectPath,
                sourceSymbol: first.SourceIdentity,
                targetSymbol: metadataIdentity,
                contractElement: first.MemberKind,
                properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["boundedInputSha256"] = metadataFact.Properties.GetValueOrDefault("boundedInputSha256") ?? string.Empty,
                    ["compiledExtractorVersion"] = metadataFact.Evidence.ExtractorVersion,
                    ["compiledFactId"] = metadataFact.FactId,
                    ["compiledGeneratorSha256"] = metadataFact.Properties.GetValueOrDefault("generatorSha256") ?? string.Empty,
                    ["compiledOptionalParameterOrdinals"] = compiledOptionalParameters,
                    ["compiledProvenanceState"] = metadataFact.Properties.GetValueOrDefault("provenanceState") ?? "unknown",
                    ["limitation"] = Limitation,
                    ["metadataIdentity"] = metadataIdentity,
                    ["optionalParameterOrdinals"] = sourceOptionalParameters,
                    ["provenanceBindingInputSha256"] = metadataFact.Properties.GetValueOrDefault("provenanceBindingInputSha256") ?? string.Empty,
                    ["reconciliationState"] = "exact-one-candidate",
                    ["relationshipProof"] = string.Join(",", relationshipProofs.OrderBy(value => value, StringComparer.Ordinal)),
                    ["sourceExtractorVersion"] = first.Evidence.ExtractorVersion,
                    ["sourceFactId"] = sourceObservation.FactId,
                    ["sourceIdentity"] = first.SourceIdentity,
                    ["sourceLanguage"] = first.Language,
                    ["supportingFactIds"] = string.Join(",", new[] { sourceObservation.FactId, metadataFact.FactId }.OrderBy(value => value, StringComparer.Ordinal))
                }));
        }

        foreach (var project in inventory
            .Where(item => item.Kind == "NonCSharpProject" && item.RelativePath.EndsWith(".fsproj", StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => item.RelativePath, StringComparer.Ordinal))
        {
            results.Add(FactFactory.Create(
                manifest,
                FactTypes.AnalysisGap,
                RuleIds.DotNetCompiledSourceIdentity,
                EvidenceTiers.Tier4Unknown,
                new EvidenceSpan(project.RelativePath, 1, 1, null, nameof(SourceMetadataReconciler), ScannerVersions.SourceMetadataReconciliationExtractor),
                projectPath: project.RelativePath,
                contractElement: "SourceMetadataReconciliationUnsupportedLanguage",
                properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["candidateCount"] = "0",
                    ["gapKind"] = "SourceMetadataReconciliationUnsupportedLanguage",
                    ["language"] = "fsharp",
                    ["limitation"] = GapLimitation,
                    ["reconciliationState"] = "unsupported-source-adapter"
                }));
        }

        return results;
    }

    private static CodeFact CreateSourceObservation(
        ScanManifest manifest,
        SourceMetadataIdentityCandidate first,
        IReadOnlyList<SourceMetadataIdentityCandidate> candidates,
        IReadOnlyList<string> metadataIdentities) => FactFactory.Create(
            manifest,
            FactTypes.SourceMetadataIdentityObserved,
            RuleIds.DotNetCompiledSourceIdentity,
            EvidenceTiers.Tier1Semantic,
            first.Evidence with
            {
                ExtractorId = nameof(SourceMetadataReconciler),
                ExtractorVersion = ScannerVersions.SourceMetadataReconciliationExtractor
            },
            projectPath: first.ProjectPath,
            sourceSymbol: first.SourceIdentity,
            targetSymbol: metadataIdentities.Count == 1 ? metadataIdentities[0] : null,
            contractElement: first.MemberKind,
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["declarationSiteCount"] = candidates.Select(candidate => (candidate.Evidence.FilePath, candidate.Evidence.StartLine, candidate.Evidence.EndLine)).Distinct().Count().ToString(),
                ["limitation"] = Limitation,
                ["metadataIdentity"] = metadataIdentities.Count == 1 ? metadataIdentities[0] : string.Empty,
                ["optionalParameterOrdinals"] = string.Join(",", candidates.SelectMany(candidate => candidate.OptionalParameterOrdinals).Distinct().OrderBy(value => value)),
                ["relationshipProof"] = string.Join(",", candidates.Select(candidate => candidate.RelationshipProof).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal)),
                ["sourceExtractorVersion"] = first.Evidence.ExtractorVersion,
                ["sourceIdentity"] = first.SourceIdentity,
                ["sourceLanguage"] = first.Language
            });

    private static CodeFact CreateGap(
        ScanManifest manifest,
        SourceMetadataIdentityCandidate source,
        CodeFact sourceObservation,
        string gapKind,
        int candidateCount,
        IEnumerable<string> details) => FactFactory.Create(
            manifest,
            FactTypes.AnalysisGap,
            RuleIds.DotNetCompiledSourceIdentity,
            EvidenceTiers.Tier4Unknown,
            source.Evidence with
            {
                ExtractorId = nameof(SourceMetadataReconciler),
                ExtractorVersion = ScannerVersions.SourceMetadataReconciliationExtractor
            },
            projectPath: source.ProjectPath,
            sourceSymbol: source.SourceIdentity,
            targetSymbol: source.MetadataIdentity,
            contractElement: gapKind,
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["candidateCount"] = candidateCount.ToString(),
                ["details"] = string.Join(",", details.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal)),
                ["expectedMetadataIdentity"] = source.MetadataIdentity ?? string.Empty,
                ["gapKind"] = gapKind,
                ["limitation"] = GapLimitation,
                ["reconciliationState"] = "unjoined",
                ["sourceFactId"] = sourceObservation.FactId,
                ["sourceIdentity"] = source.SourceIdentity,
                ["supportingFactIds"] = sourceObservation.FactId
            });

    private static bool IsMetadataDeclaration(CodeFact fact) => fact.FactType is
        FactTypes.ManagedTypeDeclared
        or FactTypes.ManagedMethodDeclared
        or FactTypes.ManagedFieldDeclared
        or FactTypes.ManagedPropertyDeclared
        or FactTypes.ManagedEventDeclared;

    public static SourceMetadataReconciliationSummary? BuildSummary(
        ScanManifest manifest,
        IReadOnlyList<CodeFact> facts,
        int maximumEntries = 4_096)
    {
        if (manifest.CompiledInputProvenance is null)
            return null;
        var allEntries = facts
            .Where(fact => fact.RuleId == RuleIds.DotNetCompiledSourceIdentity)
            .Where(fact => fact.FactType is FactTypes.SourceMetadataIdentityReconciled or FactTypes.AnalysisGap)
            .Select(ToEntry)
            .OrderBy(entry => entry.SourceIdentity, StringComparer.Ordinal)
            .ThenBy(entry => entry.MetadataIdentity, StringComparer.Ordinal)
            .ThenBy(entry => entry.ReconciliationState, StringComparer.Ordinal)
            .ThenBy(entry => entry.SourceFactId, StringComparer.Ordinal)
            .ToArray();
        var retained = allEntries.Take(maximumEntries).ToArray();
        var omitted = allEntries.Skip(maximumEntries).ToArray();
        var coverage = allEntries.Any(entry => entry.ReconciliationState != "exact-one-candidate")
            ? "source-metadata-partial"
            : "source-metadata-complete";
        return new SourceMetadataReconciliationSummary(
            SchemaVersion,
            RuleIds.DotNetCompiledSourceIdentity,
            ScannerVersions.SourceMetadataReconciliationExtractor,
            coverage,
            manifest.CompiledInputProvenance.BoundedInputSha256,
            manifest.CompiledInputProvenance.GeneratorSha256,
            retained,
            omitted.Length,
            omitted.Length == 0 ? null : Digest(omitted));
    }

    private static SourceMetadataReconciliationEntry ToEntry(CodeFact fact)
    {
        var compiledFactIds = new[]
            {
                fact.Properties.GetValueOrDefault("compiledFactId") ?? string.Empty,
                fact.Properties.GetValueOrDefault("details") ?? string.Empty
            }
            .SelectMany(value => value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(value => value.StartsWith("fact-", StringComparison.Ordinal))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        return new SourceMetadataReconciliationEntry(
            fact.Properties.GetValueOrDefault("reconciliationState") ?? "unjoined",
            fact.Properties.GetValueOrDefault("sourceIdentity") ?? fact.SourceSymbol ?? string.Empty,
            fact.Properties.GetValueOrDefault("metadataIdentity")
                ?? fact.Properties.GetValueOrDefault("expectedMetadataIdentity")
                ?? fact.TargetSymbol
                ?? string.Empty,
            fact.Properties.GetValueOrDefault("sourceFactId") ?? string.Empty,
            compiledFactIds,
            fact.RuleId,
            fact.EvidenceTier,
            fact.Evidence.ExtractorVersion,
            fact.Properties.GetValueOrDefault("compiledProvenanceState") ?? "unknown",
            fact.Properties.GetValueOrDefault("provenanceBindingInputSha256") ?? string.Empty,
            fact.Properties.GetValueOrDefault("gapKind") ?? string.Empty,
            fact.Properties.GetValueOrDefault("limitation") ?? GapLimitation);
    }

    private static string Digest(IEnumerable<SourceMetadataReconciliationEntry> entries)
    {
        var value = string.Join('\n', entries.Select(entry => string.Join('|',
            entry.ReconciliationState,
            entry.SourceIdentity,
            entry.MetadataIdentity,
            entry.SourceFactId,
            string.Join(',', entry.CompiledFactIds),
            entry.GapKind)));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
    }
}
