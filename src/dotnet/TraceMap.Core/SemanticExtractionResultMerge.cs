namespace TraceMap.Core;

// Language-neutral merge of per-language semantic extraction results (C# and
// Visual Basic) into one shared result for downstream snapshot verification,
// storage, and reporting. Ordering is stable so persistence stays
// deterministic; each input extractor already produces deterministic output.
internal static class SemanticExtractionResultMerge
{
    public static SemanticExtractionResult Merge(
        SemanticExtractionResult first,
        SemanticExtractionResult second)
    {
        if (first is null)
        {
            return second;
        }

        if (second is null)
        {
            return first;
        }

        return new SemanticExtractionResult(
            MergeCandidates(first.Facts, second.Facts),
            MergeCandidates(first.GapFacts, second.GapFacts),
            Attempted: first.Attempted || second.Attempted,
            ReducedCoverage: first.ReducedCoverage || second.ReducedCoverage,
            AnalyzedFiles: MergeSets(first.AnalyzedFiles, second.AnalyzedFiles),
            ScopeReduced: first.ScopeReduced || second.ScopeReduced,
            CompilationInputFiles: MergeSets(first.CompilationInputFiles, second.CompilationInputFiles),
            ProtectedSourceSpans: (first.ProtectedSourceSpans ?? [])
                .Concat(second.ProtectedSourceSpans ?? [])
                .OrderBy(span => span.FilePath, StringComparer.Ordinal)
                .ThenBy(span => span.Start)
                .ThenBy(span => span.Length)
                .ToArray());
    }

    private static IReadOnlyList<SemanticFactCandidate> MergeCandidates(
        IReadOnlyList<SemanticFactCandidate> first,
        IReadOnlyList<SemanticFactCandidate> second)
    {
        return first
            .Concat(second)
            .OrderBy(CandidateKey, StringComparer.Ordinal)
            .ToArray();
    }

    private static string CandidateKey(SemanticFactCandidate candidate)
    {
        var properties = candidate.Properties;
        var propertySignature = properties is null
            ? string.Empty
            : string.Join(";", properties.Select(pair => $"{pair.Key}={pair.Value}"));
        return string.Join('|',
            candidate.Evidence.FilePath,
            candidate.Evidence.StartLine.ToString(System.Globalization.CultureInfo.InvariantCulture),
            candidate.Evidence.EndLine.ToString(System.Globalization.CultureInfo.InvariantCulture),
            candidate.RuleId,
            candidate.FactType,
            candidate.SourceSymbol ?? string.Empty,
            candidate.TargetSymbol ?? string.Empty,
            candidate.ContractElement ?? string.Empty,
            FactFactory.Hash(propertySignature, 16));
    }

    private static IReadOnlySet<string> MergeSets(IReadOnlySet<string>? first, IReadOnlySet<string>? second)
    {
        if (first is null || first.Count == 0)
        {
            return second ?? new HashSet<string>(StringComparer.Ordinal);
        }

        if (second is null || second.Count == 0)
        {
            return first;
        }

        return new HashSet<string>(first.Concat(second), StringComparer.Ordinal);
    }
}
