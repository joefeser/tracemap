using System.Text.Json;
using Microsoft.Data.Sqlite;
using TraceMap.Core;

namespace TraceMap.Reduction;

public sealed record ReduceOptions(string IndexPath, string ContractDeltaPath, string OutputPath);

public sealed record ContractDelta(
    string? Contract,
    string? Source,
    IReadOnlyList<ContractDeltaChange> Changes);

public sealed record ContractDeltaChange(
    string? Element,
    string? ChangeType,
    string? OldType,
    string? NewType,
    string? Value);

public sealed record ImpactReport(
    ScanManifest Manifest,
    ContractDelta Delta,
    IReadOnlyList<ImpactFinding> Findings);

public sealed record ImpactFinding(
    string Element,
    string? ChangeType,
    string Classification,
    string RuleId,
    string Reason,
    IReadOnlyList<ImpactEvidence> Evidence);

public sealed record ImpactEvidence(
    string FactId,
    string FactType,
    string RuleId,
    string EvidenceTier,
    string FilePath,
    int StartLine,
    int EndLine,
    string? TargetSymbol,
    string? ContractElement,
    string CommitSha);

public static class ImpactClassifications
{
    public const string DefiniteImpact = nameof(DefiniteImpact);
    public const string ProbableImpact = nameof(ProbableImpact);
    public const string NeedsReview = nameof(NeedsReview);
    public const string NoEvidenceFullCoverage = nameof(NoEvidenceFullCoverage);
    public const string NoEvidenceReducedCoverage = nameof(NoEvidenceReducedCoverage);
    public const string UnknownAnalysisGap = nameof(UnknownAnalysisGap);
}

public static class ContractDeltaReducer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static readonly HashSet<string> DefiniteUsageFactTypes = new(StringComparer.Ordinal)
    {
        FactTypes.PropertyAccessed,
        FactTypes.MethodInvoked
    };

    private static readonly HashSet<string> ProbableSemanticFactTypes = new(StringComparer.Ordinal)
    {
        FactTypes.TypeDeclared,
        FactTypes.PropertyDeclared,
        FactTypes.DbContextDeclared,
        FactTypes.DbSetDeclared,
        FactTypes.HttpCallDetected,
        FactTypes.HttpClientCreated,
        FactTypes.DbChangeSaved,
        FactTypes.DapperCallDetected,
        FactTypes.SqlCommandDetected,
        FactTypes.SqlTextUsed,
        FactTypes.ConfigKeyDeclared,
        FactTypes.ConnectionStringDeclared
    };

    public static async Task<ImpactReport> ReduceAsync(ReduceOptions options, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(options.IndexPath))
        {
            throw new FileNotFoundException("TraceMap index does not exist.", options.IndexPath);
        }

        if (!File.Exists(options.ContractDeltaPath))
        {
            throw new FileNotFoundException("Contract delta does not exist.", options.ContractDeltaPath);
        }

        var delta = await ReadDeltaAsync(options.ContractDeltaPath, cancellationToken);
        await using var connection = new SqliteConnection($"Data Source={options.IndexPath};Mode=ReadOnly");
        await connection.OpenAsync(cancellationToken);

        var manifest = await ReadManifestAsync(connection, cancellationToken);
        var facts = await ReadFactsAsync(connection, cancellationToken);
        var findings = delta.Changes
            .Select(change => ReduceChange(manifest, facts, change))
            .ToArray();

        var report = new ImpactReport(manifest, delta, findings);
        var reportPath = ResolveReportPath(options.OutputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(reportPath)!);
        await File.WriteAllTextAsync(reportPath, ImpactMarkdownWriter.Build(report), cancellationToken);
        return report;
    }

    private static async Task<ContractDelta> ReadDeltaAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var delta = await JsonSerializer.DeserializeAsync<ContractDelta>(stream, JsonOptions, cancellationToken);
        if (delta is null)
        {
            throw new InvalidDataException("Contract delta JSON was empty.");
        }

        return delta with { Changes = delta.Changes ?? [] };
    }

    private static string ResolveReportPath(string outputPath)
    {
        var fullPath = Path.GetFullPath(outputPath);
        return string.Equals(Path.GetExtension(fullPath), ".md", StringComparison.OrdinalIgnoreCase)
            ? fullPath
            : Path.Combine(fullPath, "impact-report.md");
    }

    private static async Task<ScanManifest> ReadManifestAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "select manifest_json from scan_manifest order by scanned_at desc limit 1;";
        var value = await command.ExecuteScalarAsync(cancellationToken);
        if (value is not string json)
        {
            throw new InvalidDataException("TraceMap index does not contain a scan manifest.");
        }

        return JsonSerializer.Deserialize<ScanManifest>(json, JsonOptions)
            ?? throw new InvalidDataException("TraceMap scan manifest could not be parsed.");
    }

    private static async Task<IReadOnlyList<IndexedFact>> ReadFactsAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var facts = new List<IndexedFact>();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select fact_id,
                   commit_sha,
                   fact_type,
                   rule_id,
                   evidence_tier,
                   source_symbol,
                   target_symbol,
                   contract_element,
                   file_path,
                   start_line,
                   end_line,
                   properties_json
            from facts
            order by file_path, start_line, fact_type, fact_id;
            """;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            facts.Add(new IndexedFact(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.GetString(8),
                reader.GetInt32(9),
                reader.GetInt32(10),
                ReadProperties(reader.GetString(11))));
        }

        return facts;
    }

    private static IReadOnlyDictionary<string, string> ReadProperties(string json)
    {
        return JsonSerializer.Deserialize<SortedDictionary<string, string>>(json, JsonOptions)
            ?? new SortedDictionary<string, string>(StringComparer.Ordinal);
    }

    private static ImpactFinding ReduceChange(ScanManifest manifest, IReadOnlyList<IndexedFact> facts, ContractDeltaChange change)
    {
        var element = string.IsNullOrWhiteSpace(change.Element) ? "unknown" : change.Element.Trim();
        var parsed = ContractElementName.Parse(element);
        if (parsed.IsUnknown)
        {
            return new ImpactFinding(
                element,
                change.ChangeType,
                ImpactClassifications.UnknownAnalysisGap,
                RuleIds.ContractDeltaReduction,
                "The contract delta element could not be parsed into a type, property, or field name.",
                []);
        }

        var matches = facts
            .Select(fact => (Fact: fact, Match: MatchFact(parsed, fact)))
            .Where(item => item.Match != MatchStrength.None)
            .OrderByDescending(item => item.Match)
            .ThenBy(item => item.Fact.EvidenceTier, StringComparer.Ordinal)
            .ThenBy(item => item.Fact.FilePath, StringComparer.Ordinal)
            .ThenBy(item => item.Fact.StartLine)
            .Select(item => item.Fact)
            .Take(25)
            .ToArray();

        if (matches.Length == 0)
        {
            var fullCoverage = HasFullSemanticCoverage(manifest);
            return new ImpactFinding(
                element,
                change.ChangeType,
                fullCoverage ? ImpactClassifications.NoEvidenceFullCoverage : ImpactClassifications.NoEvidenceReducedCoverage,
                RuleIds.ContractDeltaReduction,
                fullCoverage
                    ? "No matching facts were found and the index reports full semantic coverage."
                    : "No matching facts were found, but the index reports reduced or syntax-only coverage.",
                []);
        }

        return new ImpactFinding(
            element,
            change.ChangeType,
            Classify(matches),
            RuleIds.ContractDeltaReduction,
            BuildReason(matches),
            matches.Select(ToEvidence).ToArray());
    }

    private static bool HasFullSemanticCoverage(ScanManifest manifest)
    {
        return string.Equals(manifest.AnalysisLevel, "Level1SemanticAnalysis", StringComparison.Ordinal)
            && string.Equals(manifest.BuildStatus, "Succeeded", StringComparison.Ordinal)
            && !string.Equals(manifest.CommitSha, "unknown", StringComparison.OrdinalIgnoreCase)
            && manifest.KnownGaps.Count == 0;
    }

    private static string Classify(IReadOnlyList<IndexedFact> matches)
    {
        if (matches.Any(fact => fact.EvidenceTier == EvidenceTiers.Tier1Semantic && DefiniteUsageFactTypes.Contains(fact.FactType)))
        {
            return ImpactClassifications.DefiniteImpact;
        }

        if (matches.Any(fact => fact.EvidenceTier == EvidenceTiers.Tier1Semantic && ProbableSemanticFactTypes.Contains(fact.FactType)))
        {
            return ImpactClassifications.ProbableImpact;
        }

        if (matches.Any(fact => fact.EvidenceTier == EvidenceTiers.Tier2Structural))
        {
            return ImpactClassifications.ProbableImpact;
        }

        if (matches.Any(fact => fact.EvidenceTier == EvidenceTiers.Tier3SyntaxOrTextual))
        {
            return ImpactClassifications.NeedsReview;
        }

        return ImpactClassifications.UnknownAnalysisGap;
    }

    private static string BuildReason(IReadOnlyList<IndexedFact> matches)
    {
        var classification = Classify(matches);
        return classification switch
        {
            ImpactClassifications.DefiniteImpact => "A changed contract element matched compiler-resolved usage evidence.",
            ImpactClassifications.ProbableImpact => "A changed contract element matched semantic or structural evidence, but not a compiler-resolved member usage.",
            ImpactClassifications.NeedsReview => "A changed contract element matched syntax-only or textual evidence; symbol identity is not proven.",
            _ => "A changed contract element matched analysis-gap evidence only."
        };
    }

    private static ImpactEvidence ToEvidence(IndexedFact fact)
    {
        return new ImpactEvidence(
            fact.FactId,
            fact.FactType,
            fact.RuleId,
            fact.EvidenceTier,
            fact.FilePath,
            fact.StartLine,
            fact.EndLine,
            fact.TargetSymbol,
            fact.ContractElement,
            fact.CommitSha);
    }

    private static MatchStrength MatchFact(ContractElementName element, IndexedFact fact)
    {
        var memberMatches = element.MemberName is not null && fact.MemberCandidates.Any(candidate => NamesMatch(element.MemberName, candidate));
        var typeMatches = element.TypeName is not null && fact.TypeCandidates.Any(candidate => NamesMatch(element.TypeName, candidate));

        if (element.MemberName is not null)
        {
            if (memberMatches && typeMatches)
            {
                return MatchStrength.TypeAndMember;
            }

            return memberMatches ? MatchStrength.Member : MatchStrength.None;
        }

        return typeMatches ? MatchStrength.Type : MatchStrength.None;
    }

    private static bool NamesMatch(string expected, string actual)
    {
        return string.Equals(NormalizeName(expected), NormalizeName(actual), StringComparison.Ordinal);
    }

    private static string NormalizeName(string value)
    {
        return new string(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    }

    private enum MatchStrength
    {
        None = 0,
        Type = 1,
        Member = 2,
        TypeAndMember = 3
    }

    private sealed record ContractElementName(string? TypeName, string? MemberName)
    {
        public bool IsUnknown => TypeName is null && MemberName is null;

        public static ContractElementName Parse(string element)
        {
            var parts = element
                .Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToArray();
            return parts.Length switch
            {
                0 => new ContractElementName(null, null),
                1 => new ContractElementName(parts[0], null),
                _ => new ContractElementName(parts[^2], parts[^1])
            };
        }
    }

    private sealed record IndexedFact(
        string FactId,
        string CommitSha,
        string FactType,
        string RuleId,
        string EvidenceTier,
        string? SourceSymbol,
        string? TargetSymbol,
        string? ContractElement,
        string FilePath,
        int StartLine,
        int EndLine,
        IReadOnlyDictionary<string, string> Properties)
    {
        public IEnumerable<string> MemberCandidates
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(ContractElement))
                {
                    yield return ContractElement;
                }

                foreach (var key in new[] { "propertyName", "memberName", "fieldName", "methodName", "keyPath", "name" })
                {
                    if (Properties.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                    {
                        yield return LastSymbolPart(value);
                    }
                }

                if (!string.IsNullOrWhiteSpace(TargetSymbol))
                {
                    yield return LastSymbolPart(TargetSymbol);
                }
            }
        }

        public IEnumerable<string> TypeCandidates
        {
            get
            {
                foreach (var key in new[] { "containingType", "className", "typeName", "namespace", "name" })
                {
                    if (Properties.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                    {
                        yield return LastSymbolPart(value);
                    }
                }

                if (!string.IsNullOrWhiteSpace(TargetSymbol)
                    && FactType is FactTypes.TypeDeclared or FactTypes.DbContextDeclared)
                {
                    yield return LastSymbolPart(TargetSymbol);
                }
            }
        }

        private static string LastSymbolPart(string value)
        {
            var normalized = value
                .Replace("global::", string.Empty, StringComparison.Ordinal)
                .Split('(', StringSplitOptions.TrimEntries)[0]
                .Trim();
            var separator = Math.Max(normalized.LastIndexOf('.'), normalized.LastIndexOf(':'));
            return separator >= 0 && separator + 1 < normalized.Length
                ? normalized[(separator + 1)..]
                : normalized;
        }
    }
}

public static class ImpactMarkdownWriter
{
    public static string Build(ImpactReport report)
    {
        var lines = new List<string>
        {
            "# TraceMap Impact Report",
            "",
            "## Repository",
            "",
            $"- Repo: `{report.Manifest.RepoName}`",
            $"- Commit SHA: `{report.Manifest.CommitSha}`",
            $"- Analysis level: `{report.Manifest.AnalysisLevel}`",
            $"- Build status: `{report.Manifest.BuildStatus}`",
            "",
            "## Contract Delta",
            "",
            $"- Contract: `{report.Delta.Contract ?? "unknown"}`",
            $"- Source: `{report.Delta.Source ?? "unknown"}`",
            $"- Changes: `{report.Delta.Changes.Count}`",
            "",
            "## Findings",
            ""
        };

        if (report.Findings.Count == 0)
        {
            lines.Add("- No changes were present in the contract delta.");
            lines.Add("");
            return string.Join(Environment.NewLine, lines);
        }

        foreach (var finding in report.Findings)
        {
            lines.Add($"### `{finding.Element}`");
            lines.Add("");
            lines.Add($"- Change type: `{finding.ChangeType ?? "unknown"}`");
            lines.Add($"- Classification: `{finding.Classification}`");
            lines.Add($"- Reducer rule: `{finding.RuleId}`");
            lines.Add($"- Reason: {finding.Reason}");
            lines.Add("");
            lines.Add("Evidence:");
            lines.Add("");

            if (finding.Evidence.Count == 0)
            {
                lines.Add($"- Manifest coverage evidence: analysis `{report.Manifest.AnalysisLevel}`, build `{report.Manifest.BuildStatus}`, commit `{report.Manifest.CommitSha}`.");
            }
            else
            {
                foreach (var evidence in finding.Evidence)
                {
                    lines.Add(
                        $"- `{evidence.FactType}` via `{evidence.RuleId}` ({evidence.EvidenceTier}) at `{evidence.FilePath}:{evidence.StartLine}-{evidence.EndLine}`, target `{evidence.TargetSymbol ?? evidence.ContractElement ?? "unknown"}`, commit `{evidence.CommitSha}`.");
                }
            }

            lines.Add("");
        }

        return string.Join(Environment.NewLine, lines);
    }
}
