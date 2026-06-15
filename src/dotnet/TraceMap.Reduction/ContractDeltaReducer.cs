using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;
using TraceMap.Core;

namespace TraceMap.Reduction;

public sealed record ReduceOptions(
    string IndexPath,
    string? ContractDeltaPath,
    string OutputPath,
    string Format = "markdown",
    string? Scope = null,
    string? Source = null,
    string? ChangeId = null,
    string? Kind = null,
    string? Surface = null,
    string? Endpoint = null,
    bool IncludePaths = false,
    bool IncludeReverse = false,
    bool ExitCode = false,
    int MaxFindings = 100,
    int MaxEvidenceRows = 500,
    int MaxPathsPerChange = 5,
    int MaxContextQueries = 50,
    int MaxGaps = 1000,
    string? SqlSchemaDeltaPath = null,
    string? Table = null,
    string? Column = null,
    string? QueryShape = null);

public sealed record ReduceResult(
    ImpactReport Report,
    string? MarkdownPath,
    string? JsonPath,
    bool HasActionableFindings);

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
    [property: JsonIgnore]
    ScanManifest Manifest,
    [property: JsonIgnore]
    ContractDelta Delta,
    IReadOnlyList<ImpactFinding> Findings)
{
    public string ReportType { get; init; } = "contract-delta-impact-single";
    public string Version { get; init; } = "2.0";
    public string InputCompatibility { get; init; } = "LegacyContractDeltaV1";
    public string ReportCoverage { get; init; } = "Full";
    public IReadOnlyList<string> CoverageWarnings { get; init; } = [];
    public ContractDeltaInputSummary Input { get; init; } = ContractDeltaInputSummary.Empty;
    public ContractDeltaImpactQuery Query { get; init; } = ContractDeltaImpactQuery.Empty;
    public ContractDeltaIndexSummary Index { get; init; } = ContractDeltaIndexSummary.Empty;
    public ContractDeltaImpactSummary Summary { get; init; } = ContractDeltaImpactSummary.Empty;
    public IReadOnlyList<ContractDeltaImpactGap> Gaps { get; init; } = [];
    public IReadOnlyList<string> Limitations { get; init; } = ContractDeltaReducer.DefaultLimitations;
}

public sealed record ContractDeltaInputSummary(
    string Compatibility,
    string? Contract,
    IReadOnlyDictionary<string, string> Source,
    int ChangeCount)
{
    public static ContractDeltaInputSummary Empty { get; } = new(
        "unknown",
        null,
        new SortedDictionary<string, string>(StringComparer.Ordinal),
        0);
}

public sealed record ImpactFinding(
    string Element,
    string? ChangeType,
    string Classification,
    string RuleId,
    string Reason,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<ImpactEvidence> Evidence)
{
    public string FindingId { get; init; } = string.Empty;
    public string ChangeId { get; init; } = string.Empty;
    public string ChangeKind { get; init; } = string.Empty;
    public string Confidence { get; init; } = "unknown";
    public string EvidenceTier { get; init; } = EvidenceTiers.Tier4Unknown;
    public string? SourceLabel { get; init; }
    public IReadOnlyDictionary<string, string> Reference { get; init; } = new SortedDictionary<string, string>(StringComparer.Ordinal);
    public IReadOnlyList<ContractDeltaImpactContext> PathContext { get; init; } = [];
    public IReadOnlyList<ContractDeltaImpactContext> ReverseContext { get; init; } = [];
    public IReadOnlyList<string> Limitations { get; init; } = [];
}

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
    string CommitSha)
{
    public string? SourceLabel { get; init; }
    public string? SourceIndexId { get; init; }
    public string? ScanId { get; init; }
    public string? SourceSymbol { get; init; }
    public IReadOnlyDictionary<string, string> Metadata { get; init; } = new SortedDictionary<string, string>(StringComparer.Ordinal);
}

public sealed record ContractDeltaImpactQuery(
    IReadOnlyList<string> Scopes,
    bool IncludePaths,
    bool IncludeReverse,
    string? Source,
    string? ChangeId,
    string? Kind,
    string? Surface,
    string? Endpoint,
    string? Table,
    string? Column,
    string? QueryShape,
    int MaxFindings,
    int MaxEvidenceRows,
    int MaxPathsPerChange,
    int MaxContextQueries,
    int MaxGaps,
    bool ExitCode,
    IReadOnlyList<string> IgnoredSelectors,
    string Algorithm,
    string AlgorithmVersion)
{
    public static ContractDeltaImpactQuery Empty { get; } = new(
        ["all"],
        false,
        false,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        null,
        100,
        500,
        5,
        50,
        1000,
        false,
        [],
        "contract-delta-impact",
        "2.0");
}

public sealed record ContractDeltaIndexSummary(
    string IndexKind,
    int SourceCount,
    string? RepoIdentityHash,
    string? CommitSha,
    string? AnalysisLevel,
    string? BuildStatus,
    IReadOnlyList<ContractDeltaSourceSummary> Sources)
{
    public static ContractDeltaIndexSummary Empty { get; } = new("single", 0, null, null, null, null, []);
}

public sealed record ContractDeltaSourceSummary(
    string Label,
    string? SourceIndexId,
    string? ScanId,
    string? Language,
    string? CommitSha,
    string? ScannerVersion,
    string? AnalysisLevel,
    string? BuildStatus,
    string? RepositoryIdentityHash);

public sealed record ContractDeltaImpactSummary(
    int ChangeCount,
    int FindingCount,
    int EvidenceRowCount,
    int GapCount,
    bool Truncated)
{
    public static ContractDeltaImpactSummary Empty { get; } = new(0, 0, 0, 0, false);
}

public sealed record ContractDeltaImpactGap(
    string GapId,
    string GapKind,
    string? ChangeId,
    string? SourceLabel,
    string RuleId,
    string EvidenceTier,
    string Classification,
    string Message,
    IReadOnlyList<string> SupportingFactIds);

public sealed record ContractDeltaImpactContext(
    string Classification,
    string RuleId,
    string EvidenceTier,
    string Message,
    IReadOnlyList<string> SupportingFactIds);

public static class ImpactClassifications
{
    public const string DefiniteImpact = nameof(DefiniteImpact);
    public const string ProbableImpact = nameof(ProbableImpact);
    public const string NeedsReview = nameof(NeedsReview);
    public const string NoEvidenceFullCoverage = nameof(NoEvidenceFullCoverage);
    public const string NoEvidenceReducedCoverage = nameof(NoEvidenceReducedCoverage);
    public const string UnknownAnalysisGap = nameof(UnknownAnalysisGap);
    public const string StaticImpactEvidence = nameof(StaticImpactEvidence);
    public const string ProbableStaticImpact = nameof(ProbableStaticImpact);
    public const string NeedsReviewImpact = nameof(NeedsReviewImpact);
    public const string NoImpactEvidence = nameof(NoImpactEvidence);
    public const string SelectorNoMatch = nameof(SelectorNoMatch);
    public const string TruncatedByLimit = nameof(TruncatedByLimit);
    public const string PathContextUnavailable = nameof(PathContextUnavailable);
    public const string ReverseContextUnavailable = nameof(ReverseContextUnavailable);
}

public static class ContractDeltaReducer
{
    private const int HighFanOutMatchThreshold = 10;
    private const string SingleReportType = "contract-delta-impact-single";
    private const string CombinedReportType = "contract-delta-impact-combined";
    private const string SqlSingleReportType = "SqlSchemaChangeImpactSingleV1";
    private const string SqlCombinedReportType = "SqlSchemaChangeImpactCombinedV1";
    private const string Algorithm = "contract-delta-fact-match";
    private const string AlgorithmVersion = "2.0";

    internal static readonly IReadOnlyList<string> DefaultLimitations =
    [
        "Contract delta impact reports describe deterministic static evidence, not runtime reachability, traffic, or business impact.",
        "Name matching is deterministic and approximate; generic or high fan-out names are marked review-sensitive.",
        "NoEvidence classifications are coverage-relative and depend on scan coverage, source identity, and known gaps.",
        "Endpoint, package, SQL, and dependency-surface evidence does not prove runtime execution or deployment behavior.",
        "Path and reverse context are optional, bounded, static context and are never complete dependency coverage."
    ];

    private static readonly HashSet<string> GenericMemberNames = new(StringComparer.Ordinal)
    {
        "id",
        "name",
        "type",
        "status",
        "state",
        "value",
        "code",
        "key"
    };

    private static readonly HashSet<string> ValidKinds = new(StringComparer.Ordinal)
    {
        "type",
        "property",
        "method",
        "endpoint",
        "package",
        "schema",
        "sql-table",
        "sql-column",
        "sql-query",
        "dependency-surface"
    };

    private static readonly HashSet<string> ValidScopes = new(StringComparer.Ordinal)
    {
        "all",
        "type",
        "property",
        "method",
        "endpoint",
        "package",
        "schema",
        "sql-table",
        "sql-column",
        "sql-query",
        "dependency-surface"
    };

    private static readonly HashSet<string> ValidChangeTypes = new(StringComparer.Ordinal)
    {
        "added",
        "removed",
        "changed",
        "renamed",
        "type_changed",
        "behavior_changed",
        "signature_changed",
        "nullable_changed",
        "required_changed",
        "deprecated",
        "enum_value_added",
        "unknown"
    };

    private static readonly HashSet<string> ValidSqlSchemaKinds = new(StringComparer.Ordinal)
    {
        "schema",
        "table",
        "column",
        "query-shape",
        "sql-file",
        "mapping",
        "persistence-surface"
    };

    private static readonly HashSet<string> ValidSqlSchemaChangeTypes = new(StringComparer.Ordinal)
    {
        "added",
        "removed",
        "renamed",
        "type_changed",
        "nullable_changed",
        "constraint_changed",
        "index_changed",
        "behavior_changed",
        "shape_changed",
        "unknown_changed"
    };

    private static readonly JsonSerializerOptions InputJsonOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static readonly JsonSerializerOptions LegacyJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static readonly JsonSerializerOptions OutputJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly HashSet<string> DefiniteUsageFactTypes = new(StringComparer.Ordinal)
    {
        FactTypes.PropertyAccessed,
        FactTypes.MethodInvoked,
        FactTypes.TypeDeclared
    };

    private static readonly HashSet<string> ProbableSemanticFactTypes = new(StringComparer.Ordinal)
    {
        FactTypes.TypeDeclared,
        FactTypes.MethodDeclared,
        FactTypes.PropertyDeclared,
        FactTypes.FieldDeclared,
        FactTypes.ParameterDeclared,
        FactTypes.DbContextDeclared,
        FactTypes.DbSetDeclared,
        FactTypes.HttpCallDetected,
        FactTypes.HttpClientCreated,
        FactTypes.DbChangeSaved,
        FactTypes.DapperCallDetected,
        FactTypes.SqlCommandDetected,
        FactTypes.SqlTextUsed,
        FactTypes.QueryPatternDetected,
        FactTypes.PackageReferenced,
        FactTypes.ConfigKeyDeclared,
        FactTypes.ConnectionStringDeclared,
        FactTypes.SymbolRelationship,
        FactTypes.SerializerContractMember,
        FactTypes.DependencyRegistered,
        FactTypes.ReflectionTarget,
        FactTypes.HttpRouteBinding,
        FactTypes.DatabaseColumnMapping,
        FactTypes.ConfigBinding
    };

    public static async Task<ReduceResult> ReduceAsync(ReduceOptions options, CancellationToken cancellationToken = default)
    {
        ValidateOptions(options);
        if (!File.Exists(options.IndexPath))
        {
            throw new FileNotFoundException("TraceMap index does not exist.", options.IndexPath);
        }

        var inputPath = options.SqlSchemaDeltaPath ?? options.ContractDeltaPath;
        if (string.IsNullOrWhiteSpace(inputPath))
        {
            throw new ArgumentException("reduce requires --contract-delta <path> or --sql-schema-delta <path>.");
        }

        if (!File.Exists(inputPath))
        {
            throw new FileNotFoundException(
                options.SqlSchemaDeltaPath is null ? "Contract delta does not exist." : "SQL schema delta does not exist.",
                inputPath);
        }

        var input = options.SqlSchemaDeltaPath is null
            ? await ReadDeltaInputAsync(inputPath, cancellationToken)
            : await ReadSqlSchemaDeltaInputAsync(inputPath, cancellationToken);
        await using var connection = new SqliteConnection($"Data Source={options.IndexPath};Mode=ReadOnly");
        await connection.OpenAsync(cancellationToken);

        var isCombined = await TableExistsAsync(connection, "index_sources", cancellationToken)
            && await TableExistsAsync(connection, "combined_facts", cancellationToken);
        if (!isCombined && !string.IsNullOrWhiteSpace(options.Source))
        {
            throw new ArgumentException("reduce --source requires a combined TraceMap index.");
        }

        if ((options.IncludePaths || options.IncludeReverse) && !isCombined)
        {
            throw new ArgumentException("reduce --include-paths and --include-reverse require a combined TraceMap index.");
        }

        var index = isCombined
            ? await ReadCombinedIndexAsync(connection, options.Source, cancellationToken)
            : await ReadSingleIndexAsync(connection, cancellationToken);
        var selectedChanges = SelectChanges(input.Changes, options, out var ignoredSelectors).ToArray();
        var gaps = new List<ContractDeltaImpactGap>();
        gaps.AddRange(BuildCoverageGaps(index, input.Changes.Count));
        gaps.AddRange(ignoredSelectors.Select((message, indexValue) => new ContractDeltaImpactGap(
            $"gap:selector:{Hash($"{indexValue}:{message}", 16)}",
            "IgnoredSelector",
            null,
            options.Source,
            RuleIds.ContractDeltaInput,
            EvidenceTiers.Tier4Unknown,
            isCombined ? ImpactClassifications.SelectorNoMatch : ImpactClassifications.NeedsReview,
            message,
            [])));

        if (selectedChanges.Length == 0)
        {
            gaps.Add(new ContractDeltaImpactGap(
                "gap:selector:no-match",
                "SelectorNoMatch",
                options.ChangeId,
                options.Source,
                RuleIds.ContractDeltaInput,
                EvidenceTiers.Tier4Unknown,
                ImpactClassifications.SelectorNoMatch,
                "No contract delta changes matched the selected reduce filters.",
                []));
        }

        var findings = new List<ImpactFinding>();
        var evidenceBudget = options.MaxEvidenceRows;
        foreach (var change in selectedChanges)
        {
            var finding = ReduceChange(index, input, change, options, isCombined, ref evidenceBudget, gaps);
            findings.Add(finding);
            if (findings.Count >= options.MaxFindings && selectedChanges.Length > findings.Count)
            {
                gaps.Add(new ContractDeltaImpactGap(
                    $"gap:truncated:findings:{Hash(change.Id, 16)}",
                    "TruncatedByLimit",
                    change.Id,
                    options.Source,
                    RuleIds.ContractDeltaImpact,
                    EvidenceTiers.Tier4Unknown,
                    ImpactClassifications.TruncatedByLimit,
                    $"Findings were truncated by --max-findings {options.MaxFindings}.",
                    []));
                break;
            }
        }

        var limitedGaps = SortAndCapGaps(gaps, options.MaxGaps, out var gapsTruncated);
        var findingsArray = findings
            .OrderBy(finding => finding.ChangeId, StringComparer.Ordinal)
            .ThenBy(finding => finding.Element, StringComparer.Ordinal)
            .ToArray();
        var reportCoverage = DetermineReportCoverage(index, findingsArray, limitedGaps);
        var manifest = index.Manifest ?? PlaceholderManifest(index);
        var legacyDelta = input.LegacyDelta ?? new ContractDelta(
            input.Contract,
            input.Source.TryGetValue("label", out var sourceLabel) ? sourceLabel : null,
            input.Changes.Select(change => new ContractDeltaChange(change.DisplayName, change.ChangeType, null, null, null)).ToArray());
        var report = new ImpactReport(manifest, legacyDelta, findingsArray)
        {
            ReportType = input.IsSqlSchemaDelta
                ? isCombined ? SqlCombinedReportType : SqlSingleReportType
                : isCombined ? CombinedReportType : SingleReportType,
            Version = input.IsSqlSchemaDelta ? "1" : "2.0",
            InputCompatibility = input.Compatibility,
            ReportCoverage = reportCoverage,
            CoverageWarnings = BuildCoverageWarnings(index),
            Input = new ContractDeltaInputSummary(
                input.Compatibility,
                SanitizeScalar(input.Contract ?? "unknown"),
                input.Source
                    .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
                input.Changes.Count),
            Query = new ContractDeltaImpactQuery(
                NormalizeScopes(options.Scope),
                options.IncludePaths,
                options.IncludeReverse,
                options.Source,
                options.ChangeId,
                options.Kind,
                options.Surface,
                options.Endpoint,
                options.Table,
                options.Column,
                options.QueryShape,
                options.MaxFindings,
                options.MaxEvidenceRows,
                options.MaxPathsPerChange,
                options.MaxContextQueries,
                options.MaxGaps,
                options.ExitCode,
                ignoredSelectors,
                Algorithm,
                AlgorithmVersion),
            Index = index.Summary,
            Summary = new ContractDeltaImpactSummary(
                selectedChanges.Length,
                findingsArray.Length,
                findingsArray.Sum(finding => finding.Evidence.Count),
                limitedGaps.Count,
                gapsTruncated || findingsArray.Any(finding => finding.Classification == ImpactClassifications.TruncatedByLimit)),
            Gaps = limitedGaps,
            Limitations = DefaultLimitations
        };

        var format = NormalizeFormat(options.Format);
        var (markdownPath, jsonPath) = await WriteOutputsAsync(options.OutputPath, format, report, cancellationToken);
        return new ReduceResult(report, markdownPath, jsonPath, HasActionableFindings(report));
    }

    private static void ValidateOptions(ReduceOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.IndexPath))
        {
            throw new ArgumentException("reduce requires --index <path>.");
        }

        if (!string.IsNullOrWhiteSpace(options.ContractDeltaPath) && !string.IsNullOrWhiteSpace(options.SqlSchemaDeltaPath))
        {
            throw new ArgumentException("reduce accepts either --contract-delta <path> or --sql-schema-delta <path>, not both.");
        }

        if (string.IsNullOrWhiteSpace(options.ContractDeltaPath) && string.IsNullOrWhiteSpace(options.SqlSchemaDeltaPath))
        {
            throw new ArgumentException("reduce requires --contract-delta <path> or --sql-schema-delta <path>.");
        }

        if (string.IsNullOrWhiteSpace(options.OutputPath))
        {
            throw new ArgumentException("reduce requires --out <path>.");
        }

        if (options.MaxFindings <= 0 || options.MaxEvidenceRows <= 0 || options.MaxPathsPerChange <= 0 || options.MaxContextQueries <= 0 || options.MaxGaps <= 0)
        {
            throw new ArgumentException("reduce numeric limits must be positive integers.");
        }

        _ = NormalizeFormat(options.Format);
        _ = NormalizeScopes(options.Scope);
    }

    private static async Task<ContractDeltaInput> ReadDeltaInputAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        using var document = await JsonDocument.ParseAsync(stream, new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip
        }, cancellationToken);
        var root = document.RootElement.Clone();
        var isV2 = root.TryGetProperty("version", out var versionElement)
            && versionElement.ValueKind == JsonValueKind.String
            && string.Equals(versionElement.GetString(), "contract-delta-v2", StringComparison.Ordinal);
        if (!isV2)
        {
            return ParseLegacyDelta(root);
        }

        return ParseV2Delta(root);
    }

    private static ContractDeltaInput ParseLegacyDelta(JsonElement root)
    {
        var delta = root.Deserialize<ContractDelta>(LegacyJsonOptions)
            ?? throw new InvalidDataException("Contract delta JSON was empty.");
        var changes = (delta.Changes ?? [])
            .Select((change, index) => NormalizeLegacyChange(change, index))
            .ToArray();
        return new ContractDeltaInput(
            "legacy",
            "LegacyContractDeltaV1",
            delta.Contract,
            new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["label"] = SanitizeScalar(delta.Source ?? "unknown")
            },
            changes,
            delta with { Changes = delta.Changes ?? [] },
            false);
    }

    private static NormalizedChange NormalizeLegacyChange(ContractDeltaChange change, int index)
    {
        var element = string.IsNullOrWhiteSpace(change.Element) ? "unknown" : change.Element.Trim();
        var parsed = ContractElementName.Parse(element);
        var reference = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["element"] = SanitizeScalar(element)
        };
        var kind = "type";
        if (parsed.MemberName is not null)
        {
            kind = "property";
            reference["typeName"] = parsed.TypeName ?? string.Empty;
            reference["propertyName"] = parsed.MemberName;
        }
        else if (parsed.TypeName is not null)
        {
            reference["typeName"] = parsed.TypeName;
        }

        return new NormalizedChange(
            $"legacy-{index + 1:000}",
            kind,
            kind,
            string.IsNullOrWhiteSpace(change.ChangeType) ? "unknown" : change.ChangeType.Trim(),
            reference,
            element,
            true,
            parsed.MemberName is null ? "legacy-name" : "legacy-type-member");
    }

    private static ContractDeltaInput ParseV2Delta(JsonElement root)
    {
        var contract = GetOptionalString(root, "contract");
        var source = ParseSource(root);
        if (!root.TryGetProperty("changes", out var changesElement) || changesElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("Contract delta v2 requires a changes array.");
        }

        var changes = new List<NormalizedChange>();
        foreach (var changeElement in changesElement.EnumerateArray())
        {
            var id = GetRequiredString(changeElement, "id", "Contract delta v2 changes require id.");
            var kind = GetRequiredString(changeElement, "kind", "Contract delta v2 changes require kind.");
            if (!ValidKinds.Contains(kind))
            {
                throw new InvalidDataException("Contract delta v2 contains an unsupported change kind.");
            }

            var changeType = GetRequiredString(changeElement, "changeType", "Contract delta v2 changes require changeType.");
            if (!ValidChangeTypes.Contains(changeType))
            {
                throw new InvalidDataException("Contract delta v2 contains an unsupported change type.");
            }

            if (!changeElement.TryGetProperty("reference", out var referenceElement) || referenceElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException("Contract delta v2 changes require a reference object.");
            }

            var reference = ParseReferenceMap(referenceElement);
            ValidateReference(kind, reference);
            var displayName = BuildDisplayName(kind, reference);
            changes.Add(new NormalizedChange(id, kind, kind, changeType, reference, displayName, false, Specificity(kind, reference)));
        }

        return new ContractDeltaInput("contract-delta-v2", "ContractDeltaV2", contract, source, changes, null, false);
    }

    private static async Task<ContractDeltaInput> ReadSqlSchemaDeltaInputAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        using var document = await JsonDocument.ParseAsync(stream, new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip
        }, cancellationToken);

        return ParseSqlSchemaDelta(document.RootElement.Clone());
    }

    private static ContractDeltaInput ParseSqlSchemaDelta(JsonElement root)
    {
        var version = GetRequiredString(root, "version", "SQL schema delta requires version.");
        if (!string.Equals(version, "sql-schema-delta.v1", StringComparison.Ordinal))
        {
            throw new InvalidDataException("SQL schema delta contains an unsupported version.");
        }

        var source = ParseSource(root);
        if (!root.TryGetProperty("changes", out var changesElement) || changesElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("SQL schema delta requires a changes array.");
        }

        var changes = new List<NormalizedChange>();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var changeElement in changesElement.EnumerateArray())
        {
            var id = GetRequiredString(changeElement, "id", "SQL schema delta changes require id.");
            if (!ids.Add(id))
            {
                throw new InvalidDataException("SQL schema delta contains duplicate change ids.");
            }

            var inputKind = GetRequiredString(changeElement, "kind", "SQL schema delta changes require kind.");
            if (!ValidSqlSchemaKinds.Contains(inputKind))
            {
                throw new InvalidDataException("SQL schema delta contains an unsupported change kind.");
            }

            var changeType = GetRequiredString(changeElement, "changeType", "SQL schema delta changes require changeType.");
            if (!ValidSqlSchemaChangeTypes.Contains(changeType))
            {
                throw new InvalidDataException("SQL schema delta contains an unsupported change type.");
            }

            if (!changeElement.TryGetProperty("reference", out var referenceElement) || referenceElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException("SQL schema delta changes require a reference object.");
            }

            var reference = NormalizeSqlSchemaReference(inputKind, referenceElement);
            var contractKind = ToContractKind(inputKind, reference);
            ValidateReference(contractKind, reference);
            var displayName = BuildDisplayName(contractKind, reference);
            changes.Add(new NormalizedChange(
                id,
                contractKind,
                inputKind,
                changeType,
                reference,
                displayName,
                false,
                SqlSchemaSpecificity(inputKind, reference)));
        }

        return new ContractDeltaInput(
            "sql-schema-delta.v1",
            "SqlSchemaDeltaV1",
            "sql-schema",
            source,
            changes.OrderBy(change => change.Id, StringComparer.Ordinal).ToArray(),
            null,
            true);
    }

    private static IReadOnlyDictionary<string, string> NormalizeSqlSchemaReference(string inputKind, JsonElement referenceElement)
    {
        var raw = ParseStringMap(referenceElement, sanitizeValues: false);
        var allowed = inputKind switch
        {
            "schema" => new HashSet<string>(["schemaName", "databaseNameHash", "sourceKind", "surfaceKind"], StringComparer.Ordinal),
            "table" => new HashSet<string>(["schemaName", "tableName", "tableNames", "sourceKind", "surfaceKind"], StringComparer.Ordinal),
            "column" => new HashSet<string>(["schemaName", "tableName", "columnName", "columnNames", "mappedName", "containingType", "propertyName"], StringComparer.Ordinal),
            "query-shape" => new HashSet<string>(["queryShapeHash", "textHash", "operationName", "tableName", "tableNames", "columnNames", "sqlSourceKind", "sourceSymbol"], StringComparer.Ordinal),
            "sql-file" => new HashSet<string>(["sqlResourceName", "sqlSourceKind", "textHash", "queryShapeHash", "tableName", "tableNames"], StringComparer.Ordinal),
            "mapping" => new HashSet<string>(["surfaceKind", "tableName", "columnName", "mappedName", "containingType", "propertyName", "sourceSymbol", "targetSymbol"], StringComparer.Ordinal),
            "persistence-surface" => new HashSet<string>(["surfaceKind", "surfaceName", "stableKey", "tableName", "columnName", "mappedName", "containingType", "sourceLabel"], StringComparer.Ordinal),
            _ => new HashSet<string>(StringComparer.Ordinal)
        };
        var safe = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in raw)
        {
            if (!allowed.Contains(key))
            {
                continue;
            }

            var sanitized = SanitizeReferenceValue(key, value);
            if (!sanitized.StartsWith("value-hash:", StringComparison.Ordinal) || IsHashReferenceKey(key))
            {
                safe[key] = sanitized;
            }
        }

        if ((inputKind is "mapping" or "persistence-surface") && !safe.ContainsKey("surfaceKind"))
        {
            safe["surfaceKind"] = "sql-persistence";
        }

        if (inputKind == "sql-file" && safe.TryGetValue("sqlResourceName", out var resourceName) && !safe.ContainsKey("name"))
        {
            safe["name"] = resourceName;
        }

        if (safe.Count == 0)
        {
            throw new InvalidDataException("SQL schema delta reference did not contain a safe selector.");
        }

        return safe;
    }

    private static bool IsHashReferenceKey(string key)
    {
        return key is "queryShapeHash" or "textHash" or "databaseNameHash";
    }

    private static string ToContractKind(string inputKind, IReadOnlyDictionary<string, string> reference)
    {
        return inputKind switch
        {
            "schema" => "schema",
            "table" => "sql-table",
            "column" => "sql-column",
            "query-shape" or "sql-file" => "sql-query",
            "mapping" or "persistence-surface" => "dependency-surface",
            _ => inputKind
        };
    }

    private static string SqlSchemaSpecificity(string inputKind, IReadOnlyDictionary<string, string> reference)
    {
        return inputKind switch
        {
            "column" when Has(reference, "tableName") && Has(reference, "columnName", "columnNames") => "sql-table-column",
            "query-shape" when Has(reference, "queryShapeHash") => "sql-query-shape",
            "query-shape" when Has(reference, "textHash") => "sql-text-hash-only",
            "mapping" when Has(reference, "mappedName") && !Has(reference, "tableName") && !Has(reference, "columnName") => "sql-mapped-name-only",
            "mapping" or "persistence-surface" when Has(reference, "tableName", "columnName", "mappedName", "surfaceName", "stableKey") => "surface-kind-name",
            "schema" => "sql-schema-only",
            "table" => "sql-table-only",
            "column" => "sql-column-only",
            _ => Specificity(ToContractKind(inputKind, reference), reference)
        };
    }

    private static void ValidateReference(string kind, IReadOnlyDictionary<string, string> reference)
    {
        var hasAny = reference.Count > 0;
        var valid = kind switch
        {
            "type" => Has(reference, "typeName", "fullyQualifiedName", "symbolId", "name"),
            "property" => Has(reference, "propertyName", "memberName", "jsonName", "columnName", "symbolId", "name"),
            "method" => Has(reference, "methodName", "signature", "symbolId", "name"),
            "endpoint" => Has(reference, "path", "pathKey", "normalizedPathKey", "routeTemplate"),
            "package" => Has(reference, "packageName", "name"),
            "schema" => Has(reference, "schemaName", "databaseNameHash", "sourceKind", "surfaceKind", "tableName", "columnName", "name"),
            "sql-table" => Has(reference, "tableName", "tableNames", "schemaName", "name"),
            "sql-column" => Has(reference, "columnName", "columnNames", "mappedName", "propertyName", "name"),
            "sql-query" => Has(reference, "queryShapeHash", "textHash", "tableName", "tableNames", "operationName", "sqlSourceKind", "sqlResourceName", "name"),
            "dependency-surface" => Has(reference, "surfaceKind", "kind") && Has(reference, "surfaceName", "stableKey", "packageName", "name", "tableName", "columnName", "mappedName"),
            _ => hasAny
        };
        if (kind == "dependency-surface"
            && string.Equals(NormalizeSurfaceKind(Value(reference, "surfaceKind", "kind")), "package-config", StringComparison.OrdinalIgnoreCase)
            && !Has(reference, "surfaceName", "packageName", "stableKey", "name"))
        {
            valid = false;
        }

        if (!valid || !hasAny)
        {
            throw new InvalidDataException("Contract delta v2 reference is missing required identity fields.");
        }
    }

    private static bool Has(IReadOnlyDictionary<string, string> reference, params string[] keys)
    {
        return keys.Any(key => reference.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value));
    }

    private static string Specificity(string kind, IReadOnlyDictionary<string, string> reference)
    {
        return kind switch
        {
            "property" when Has(reference, "typeName", "fullyQualifiedName") && Has(reference, "propertyName", "memberName", "jsonName", "columnName") => "type-member",
            "method" when Has(reference, "typeName", "fullyQualifiedName") && Has(reference, "methodName", "signature") => "type-member",
            "endpoint" when Has(reference, "method") && Has(reference, "path", "pathKey", "normalizedPathKey") => "endpoint-method-path",
            "package" when Has(reference, "ecosystem") => "package-ecosystem",
            "sql-column" when Has(reference, "tableName") => "sql-table-column",
            "dependency-surface" when Has(reference, "surfaceKind") && Has(reference, "surfaceName", "stableKey", "packageName") => "surface-kind-name",
            _ => "name-only"
        };
    }

    private static string BuildDisplayName(string kind, IReadOnlyDictionary<string, string> reference)
    {
        return kind switch
        {
            "property" => JoinNonEmpty(".", Value(reference, "typeName", "fullyQualifiedName"), Value(reference, "propertyName", "memberName", "jsonName", "columnName", "name")),
            "method" => JoinNonEmpty(".", Value(reference, "typeName", "fullyQualifiedName"), Value(reference, "signature", "methodName", "name")),
            "endpoint" => JoinNonEmpty(" ", Value(reference, "method"), Value(reference, "path", "normalizedPathKey", "routeTemplate")),
            "package" => JoinNonEmpty(":", Value(reference, "ecosystem"), Value(reference, "packageName", "name")),
            "sql-table" => Value(reference, "tableName", "name") ?? "sql-table",
            "sql-column" => JoinNonEmpty(".", Value(reference, "tableName"), Value(reference, "columnName", "name")),
            "sql-query" => Value(reference, "queryShapeHash", "textHash", "operationName", "name", "tableName") ?? "sql-query",
            "dependency-surface" => JoinNonEmpty(":", Value(reference, "surfaceKind"), Value(reference, "surfaceName", "packageName", "stableKey", "name")),
            _ => Value(reference, "typeName", "fullyQualifiedName", "schemaName", "name") ?? kind
        } ?? kind;
    }

    private static string? JoinNonEmpty(string separator, params string?[] values)
    {
        var parts = values.Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
        return parts.Length == 0 ? null : string.Join(separator, parts);
    }

    private static string? Value(IReadOnlyDictionary<string, string> reference, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (reference.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static IEnumerable<string> ReferenceValues(IReadOnlyDictionary<string, string> reference, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (reference.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                foreach (var part in SplitValue(value))
                {
                    yield return part;
                }
            }
        }
    }

    private static IReadOnlyDictionary<string, string> ParseSource(JsonElement root)
    {
        if (!root.TryGetProperty("source", out var sourceElement))
        {
            return new SortedDictionary<string, string>(StringComparer.Ordinal);
        }

        if (sourceElement.ValueKind == JsonValueKind.String)
        {
            return new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["label"] = SanitizeScalar(sourceElement.GetString() ?? "unknown")
            };
        }

        if (sourceElement.ValueKind == JsonValueKind.Object)
        {
            return ParseStringMap(sourceElement, sanitizeValues: true);
        }

        return new SortedDictionary<string, string>(StringComparer.Ordinal);
    }

    private static IReadOnlyList<NormalizedChange> SelectChanges(IReadOnlyList<NormalizedChange> changes, ReduceOptions options, out IReadOnlyList<string> ignoredSelectors)
    {
        var ignored = new List<string>();
        var scopes = NormalizeScopes(options.Scope);
        var selected = changes.AsEnumerable();
        if (!scopes.Contains("all"))
        {
            selected = selected.Where(change => scopes.Contains(change.Kind));
        }

        if (!string.IsNullOrWhiteSpace(options.ChangeId))
        {
            selected = selected.Where(change => string.Equals(change.Id, options.ChangeId, StringComparison.Ordinal));
        }

        var requestedKind = options.Kind?.Trim();
        var kindFilter = NormalizeKindFilter(requestedKind);
        if (!string.IsNullOrWhiteSpace(kindFilter))
        {
            if (!ValidKinds.Contains(kindFilter))
            {
                throw new ArgumentException("reduce --kind must be a supported contract delta kind.");
            }

            selected = string.Equals(requestedKind, kindFilter, StringComparison.Ordinal)
                ? selected.Where(change => string.Equals(change.Kind, kindFilter, StringComparison.Ordinal))
                : selected.Where(change => string.Equals(change.InputKind, requestedKind, StringComparison.Ordinal));
        }

        if (!string.IsNullOrWhiteSpace(options.Endpoint))
        {
            selected = selected.Where(change => change.Kind == "endpoint" && EndpointMatchesSelector(change, options.Endpoint));
        }

        if (!string.IsNullOrWhiteSpace(options.Surface))
        {
            selected = selected.Where(change => change.Kind == "dependency-surface"
                && string.Equals(NormalizeSurfaceKind(Value(change.Reference, "surfaceKind")), NormalizeSurfaceKind(options.Surface), StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(options.Surface) && !changes.Any(change => change.Kind == "dependency-surface"))
        {
            ignored.Add("--surface had no dependency-surface contract changes to filter.");
        }

        if (!string.IsNullOrWhiteSpace(options.Table))
        {
            selected = selected.Where(change => ReferenceValues(change.Reference, "tableName", "tableNames", "name")
                .Any(tableName => NamesMatch(options.Table, tableName)));
        }

        if (!string.IsNullOrWhiteSpace(options.Column))
        {
            selected = selected.Where(change => ReferenceValues(change.Reference, "columnName", "columnNames", "mappedName", "propertyName", "name")
                .Any(columnName => NamesMatch(options.Column, columnName)));
        }

        if (!string.IsNullOrWhiteSpace(options.QueryShape))
        {
            selected = selected.Where(change => Value(change.Reference, "queryShapeHash")
                is { } queryShapeHash && string.Equals(options.QueryShape, queryShapeHash, StringComparison.Ordinal));
        }

        ignoredSelectors = ignored;
        return selected.ToArray();
    }

    private static string? NormalizeKindFilter(string? kind)
    {
        if (string.IsNullOrWhiteSpace(kind))
        {
            return null;
        }

        return kind.Trim() switch
        {
            "table" => "sql-table",
            "column" => "sql-column",
            "query-shape" or "sql-file" => "sql-query",
            "mapping" or "persistence-surface" => "dependency-surface",
            var value => value
        };
    }

    private static bool EndpointMatchesSelector(NormalizedChange change, string selector)
    {
        var parts = selector.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
        {
            throw new ArgumentException("reduce --endpoint must be in the form \"METHOD /path\".");
        }

        var method = Value(change.Reference, "method");
        var path = Value(change.Reference, "path", "pathKey", "normalizedPathKey", "routeTemplate");
        return string.Equals(method, parts[0], StringComparison.OrdinalIgnoreCase)
            && string.Equals(NormalizeEndpointPath(path), NormalizeEndpointPath(parts[1]), StringComparison.OrdinalIgnoreCase);
    }

    private static ImpactFinding ReduceChange(
        IndexData index,
        ContractDeltaInput input,
        NormalizedChange change,
        ReduceOptions options,
        bool isCombined,
        ref int evidenceBudget,
        List<ContractDeltaImpactGap> gaps)
    {
        var factsForMatching = isCombined && input.IsSqlSchemaDelta
            ? index.ProjectedCombinedSqlSurfaceFacts.Concat(index.Facts.Where(fact => fact.FactType == FactTypes.AnalysisGap))
            : index.Facts;
        var matchedItems = factsForMatching
            .Where(fact => string.IsNullOrWhiteSpace(options.Source) || string.Equals(fact.SourceLabel, options.Source, StringComparison.Ordinal))
            .Select(fact => (Fact: fact, Match: MatchFact(change, fact)))
            .Where(item => item.Match.Strength != MatchStrength.None)
            .OrderByDescending(item => item.Match.Strength)
            .ThenBy(item => item.Match.ReviewOnly)
            .ThenBy(item => item.Fact.SourceLabel ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(item => item.Fact.FilePath, StringComparer.Ordinal)
            .ThenBy(item => item.Fact.StartLine)
            .ThenBy(item => item.Fact.FactId, StringComparer.Ordinal)
            .ToArray();

        if (matchedItems.Length == 0)
        {
            var noEvidenceClassification = NoEvidenceClassification(index, isCombined);
            var coverageEvidence = BuildNoMatchEvidence(index, change, options, noEvidenceClassification);
            return BuildFinding(input, change, noEvidenceClassification, BuildReason(noEvidenceClassification, isCombined), [], [coverageEvidence], options, isCombined);
        }

        var matchesTruncated = matchedItems.Length > evidenceBudget;
        var takeCount = Math.Min(Math.Max(evidenceBudget, 0), matchedItems.Length);
        var matches = matchedItems.Take(takeCount).ToArray();
        evidenceBudget -= takeCount;
        if (matchesTruncated)
        {
            gaps.Add(new ContractDeltaImpactGap(
                $"gap:truncated:evidence:{Hash(change.Id, 16)}",
                "TruncatedByLimit",
                change.Id,
                options.Source,
                RuleIds.ContractDeltaImpact,
                EvidenceTiers.Tier4Unknown,
                ImpactClassifications.TruncatedByLimit,
                $"Evidence rows were truncated by --max-evidence-rows {options.MaxEvidenceRows}.",
                matchedItems.Take(10).Select(item => item.Fact.FactId).OrderBy(id => id, StringComparer.Ordinal).ToArray()));
        }

        var warnings = BuildWarnings(change, matchedItems);
        var classification = Classify(matches, change, input.IsSqlSchemaDelta, isCombined, warnings);
        if (options.IncludePaths)
        {
            gaps.Add(new ContractDeltaImpactGap(
                $"gap:path-context:{Hash(change.Id, 16)}",
                "PathContextUnavailable",
                change.Id,
                options.Source,
                RuleIds.ContractDeltaContext,
                EvidenceTiers.Tier4Unknown,
                ImpactClassifications.PathContextUnavailable,
                "No stable combined path selector could be derived without overclaiming path coverage for this contract delta match set.",
                matches.Select(item => item.Fact.FactId).Take(10).OrderBy(id => id, StringComparer.Ordinal).ToArray()));
        }

        if (options.IncludeReverse)
        {
            gaps.Add(new ContractDeltaImpactGap(
                $"gap:reverse-context:{Hash(change.Id, 16)}",
                "ReverseContextUnavailable",
                change.Id,
                options.Source,
                RuleIds.ContractDeltaContext,
                EvidenceTiers.Tier4Unknown,
                ImpactClassifications.ReverseContextUnavailable,
                "No stable combined reverse-dependency selector could be derived without overclaiming reverse impact coverage for this contract delta match set.",
                matches.Select(item => item.Fact.FactId).Take(10).OrderBy(id => id, StringComparer.Ordinal).ToArray()));
        }

        var evidence = matches.Select(item => ToEvidence(item.Fact, item.Match)).ToArray();
        return BuildFinding(input, change, classification, BuildReason(classification, isCombined), warnings, evidence, options, isCombined);
    }

    private static ImpactFinding BuildFinding(
        ContractDeltaInput input,
        NormalizedChange change,
        string classification,
        string reason,
        IReadOnlyList<string> warnings,
        IReadOnlyList<ImpactEvidence> evidence,
        ReduceOptions options,
        bool isCombined)
    {
        var evidenceTier = HighestEvidenceTier(evidence);
        var sourceLabel = evidence.Select(row => row.SourceLabel).FirstOrDefault(label => !string.IsNullOrWhiteSpace(label));
        var contexts = isCombined && options.IncludePaths
            ? [new ContractDeltaImpactContext(ImpactClassifications.PathContextUnavailable, RuleIds.ContractDeltaContext, EvidenceTiers.Tier4Unknown, "Path context unavailable for this contract delta selector.", evidence.Select(row => row.FactId).Take(10).ToArray())]
            : Array.Empty<ContractDeltaImpactContext>();
        var reverseContexts = isCombined && options.IncludeReverse
            ? [new ContractDeltaImpactContext(ImpactClassifications.ReverseContextUnavailable, RuleIds.ContractDeltaContext, EvidenceTiers.Tier4Unknown, "Reverse context unavailable for this contract delta selector.", evidence.Select(row => row.FactId).Take(10).ToArray())]
            : Array.Empty<ContractDeltaImpactContext>();
        var findingRuleId = input.Compatibility == "LegacyContractDeltaV1"
            ? RuleIds.ContractDeltaReduction
            : RuleIds.ContractDeltaImpact;
        var findingPrefix = input.IsSqlSchemaDelta ? "sql-schema-impact" : "contract-delta";
        return new ImpactFinding(
            change.DisplayName,
            change.ChangeType,
            classification,
            findingRuleId,
            reason,
            warnings,
            evidence)
        {
            FindingId = $"finding:{findingPrefix}:{Hash($"{change.Id}:{classification}:{change.DisplayName}", 24)}",
            ChangeId = change.Id,
            ChangeKind = change.InputKind,
            Confidence = ConfidenceFor(classification, input.IsSqlSchemaDelta),
            EvidenceTier = evidenceTier,
            SourceLabel = sourceLabel,
            Reference = change.Reference
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal),
            PathContext = contexts,
            ReverseContext = reverseContexts,
            Limitations = FindingLimitations(change, classification, input.Compatibility, input.IsSqlSchemaDelta)
        };
    }

    private static string NoEvidenceClassification(IndexData index, bool isCombined)
    {
        if (isCombined)
        {
            return HasFullCoverage(index) ? ImpactClassifications.NoImpactEvidence : ImpactClassifications.UnknownAnalysisGap;
        }

        return HasFullCoverage(index) ? ImpactClassifications.NoEvidenceFullCoverage : ImpactClassifications.NoEvidenceReducedCoverage;
    }

    private static string Classify(IReadOnlyList<(IndexedFact Fact, EvidenceMatch Match)> matches, NormalizedChange change, bool isSqlSchemaDelta, bool isCombined, IReadOnlyList<string> warnings)
    {
        if (matches.Any(item => item.Fact.FactType == FactTypes.AnalysisGap || item.Fact.EvidenceTier == EvidenceTiers.Tier4Unknown))
        {
            return ImpactClassifications.UnknownAnalysisGap;
        }

        if (matches.Any(item => item.Match.ReviewOnly)
            || warnings.Any(IsReviewSensitiveWarning)
            || isSqlSchemaDelta && IsSqlSchemaReviewTier(change, matches))
        {
            return isCombined ? ImpactClassifications.NeedsReviewImpact : ImpactClassifications.NeedsReview;
        }

        if (matches.Any(item => item.Fact.EvidenceTier == EvidenceTiers.Tier1Semantic && DefiniteUsageFactTypes.Contains(item.Fact.FactType)))
        {
            return isCombined ? ImpactClassifications.StaticImpactEvidence : ImpactClassifications.DefiniteImpact;
        }

        if (matches.Any(item => item.Fact.EvidenceTier == EvidenceTiers.Tier1Semantic && ProbableSemanticFactTypes.Contains(item.Fact.FactType)))
        {
            return isCombined ? ImpactClassifications.ProbableStaticImpact : ImpactClassifications.ProbableImpact;
        }

        if (matches.Any(item => item.Fact.EvidenceTier == EvidenceTiers.Tier2Structural))
        {
            return isCombined ? ImpactClassifications.ProbableStaticImpact : ImpactClassifications.ProbableImpact;
        }

        if (matches.Any(item => item.Fact.EvidenceTier == EvidenceTiers.Tier3SyntaxOrTextual))
        {
            return isCombined ? ImpactClassifications.NeedsReviewImpact : ImpactClassifications.NeedsReview;
        }

        return ImpactClassifications.UnknownAnalysisGap;
    }

    private static bool IsSqlSchemaReviewTier(NormalizedChange change, IReadOnlyList<(IndexedFact Fact, EvidenceMatch Match)> matches)
    {
        return change.Specificity is "sql-schema-only" or "sql-table-only" or "sql-column-only" or "sql-text-hash-only" or "sql-mapped-name-only"
            || matches.Any(item => item.Match.EvidenceKind is "sql-text-hash" or "sql-schema-metadata" or "sql-resource");
    }

    private static string BuildReason(string classification, bool isCombined)
    {
        return classification switch
        {
            ImpactClassifications.DefiniteImpact or ImpactClassifications.StaticImpactEvidence => "A changed contract reference matched compiler-resolved static usage evidence.",
            ImpactClassifications.ProbableImpact or ImpactClassifications.ProbableStaticImpact => "A changed contract reference matched semantic or strong structural evidence.",
            ImpactClassifications.NeedsReview or ImpactClassifications.NeedsReviewImpact => "A changed contract reference matched syntax-only, textual, generic, high fan-out, or review-sensitive evidence.",
            ImpactClassifications.NoEvidenceFullCoverage => "No matching facts were found and the index reports full semantic coverage.",
            ImpactClassifications.NoEvidenceReducedCoverage => "No matching facts were found, but the index reports reduced or syntax-only coverage.",
            ImpactClassifications.NoImpactEvidence => "No matching static impact evidence was found across the selected combined index sources under full coverage.",
            _ => isCombined
                ? "The selected combined index has analysis gaps or source coverage caveats that prevent a credible no-impact conclusion."
                : "A changed contract reference matched analysis-gap evidence or the scan has gaps that prevent a credible conclusion."
        };
    }

    private static bool IsReviewSensitiveWarning(string warning)
    {
        return warning.StartsWith("Generic ", StringComparison.Ordinal)
            || warning.StartsWith("Name-only ", StringComparison.Ordinal)
            || warning.StartsWith("High fan-out ", StringComparison.Ordinal);
    }

    private static IReadOnlyList<string> BuildWarnings(NormalizedChange change, IReadOnlyList<(IndexedFact Fact, EvidenceMatch Match)> matchedItems)
    {
        var warnings = new List<string>();
        var primaryName = PrimaryName(change);
        if (!string.IsNullOrWhiteSpace(primaryName)
            && GenericMemberNames.Contains(NormalizeName(primaryName))
            && matchedItems.Count > 1)
        {
            warnings.Add($"Generic member name `{primaryName}` matched {matchedItems.Count} facts; review target identity before treating this as contract-specific.");
        }

        if (change.Specificity == "name-only" && matchedItems.Count > 1)
        {
            warnings.Add("Name-only contract reference matched multiple facts; review exact type, endpoint, package, or surface identity.");
        }

        if (matchedItems.Count > HighFanOutMatchThreshold)
        {
            var fileCount = matchedItems
                .Select(item => item.Fact.FilePath)
                .Distinct(StringComparer.Ordinal)
                .Count();
            warnings.Add($"High fan-out match set: {matchedItems.Count} facts across {fileCount} files; prioritize Tier1 evidence and exact identity matches.");
        }

        if (matchedItems.Any(item => item.Fact.HasSymbolIdentity)
            && matchedItems.Any(item => !item.Fact.HasSymbolIdentity))
        {
            warnings.Add("Mixed symbol-backed and name-only matches were found; prioritize rows with symbol identity and Tier1 evidence.");
        }

        if (matchedItems.Any(item => item.Fact.FactType == FactTypes.SymbolRelationship))
        {
            warnings.Add("Symbol relationship evidence is compiler-resolved but direct; transitive inheritance/interface implications require graph traversal.");
        }

        return warnings;
    }

    private static IReadOnlyList<string> FindingLimitations(NormalizedChange change, string classification, string compatibility, bool isSqlSchemaDelta)
    {
        var limitations = new List<string>();
        if (compatibility == "LegacyContractDeltaV1")
        {
            limitations.Add("Legacy v1 contract delta input has type/property name matching only; v2 structured references provide stronger identity.");
        }

        if (change.Specificity is "name-only" or "legacy-name")
        {
            limitations.Add("This finding uses name-only matching and is review-sensitive.");
        }

        if (classification is ImpactClassifications.NoEvidenceReducedCoverage or ImpactClassifications.UnknownAnalysisGap)
        {
            limitations.Add("Reduced coverage or analysis gaps prevent a full absence-of-evidence conclusion.");
        }

        if (isSqlSchemaDelta)
        {
            limitations.Add("SQL/schema impact is deterministic static evidence, not runtime execution, schema existence, migration correctness, dialect validation, query-plan behavior, permissions, data contents, or tenant behavior proof.");
        }
        else if (change.Kind is "sql-query" or "sql-table" or "sql-column")
        {
            limitations.Add("SQL evidence is static shape or API usage evidence and does not prove runtime execution or schema state.");
        }

        if (change.Kind == "endpoint")
        {
            limitations.Add("Endpoint evidence is static route or client-call evidence and does not prove runtime traffic, auth, deployment base path, or reachability.");
        }

        return limitations.OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    private static EvidenceMatch MatchFact(NormalizedChange change, IndexedFact fact)
    {
        if (fact.FactType == FactTypes.AnalysisGap)
        {
            return MatchAnalysisGap(change, fact);
        }

        return change.Kind switch
        {
            "type" => MatchType(change, fact),
            "property" => MatchMember(change, fact, ["propertyName", "memberName", "fieldName", "jsonName", "columnName", "name"]),
            "method" => MatchMember(change, fact, ["methodName", "signature", "memberName", "name"]),
            "endpoint" => MatchEndpoint(change, fact),
            "package" => MatchPackage(change, fact),
            "schema" => MatchSchema(change, fact),
            "sql-table" => MatchSqlTable(change, fact),
            "sql-column" => MatchSqlColumn(change, fact),
            "sql-query" => MatchSqlQuery(change, fact),
            "dependency-surface" => MatchDependencySurface(change, fact),
            _ => EvidenceMatch.None
        };
    }

    private static EvidenceMatch MatchAnalysisGap(NormalizedChange change, IndexedFact fact)
    {
        var terms = SearchTerms(change).ToArray();
        if (terms.Length == 0)
        {
            return EvidenceMatch.None;
        }

        return terms.Any(term => fact.SearchTextCandidates.Any(candidate => TextMentionsName(candidate, term)))
            ? new EvidenceMatch(MatchStrength.Textual, true, "analysis-gap")
            : EvidenceMatch.None;
    }

    private static EvidenceMatch MatchType(NormalizedChange change, IndexedFact fact)
    {
        var typeName = Value(change.Reference, "typeName", "fullyQualifiedName", "name", "symbolId");
        return typeName is not null && fact.TypeCandidates.Any(candidate => NamesMatch(typeName, candidate))
            ? new EvidenceMatch(MatchStrength.Type, false, "type")
            : EvidenceMatch.None;
    }

    private static EvidenceMatch MatchMember(NormalizedChange change, IndexedFact fact, IReadOnlyList<string> memberKeys)
    {
        var typeName = Value(change.Reference, "typeName", "fullyQualifiedName", "declaringType");
        var typeIsConstrained = !string.IsNullOrWhiteSpace(typeName);
        var typeMatches = typeName is not null && fact.TypeCandidates.Any(candidate => NamesMatch(typeName, candidate));
        var signature = Value(change.Reference, "signature");
        if (!string.IsNullOrWhiteSpace(signature))
        {
            var signatureMatches = SignatureCandidates(fact).Any(candidate => SignaturesMatch(signature, candidate));
            if (signatureMatches && (!typeIsConstrained || typeMatches))
            {
                return new EvidenceMatch(MatchStrength.Exact, false, "signature");
            }

            if (signatureMatches)
            {
                return new EvidenceMatch(MatchStrength.Member, true, "signature-type-unverified");
            }
        }

        var memberName = Value(change.Reference, memberKeys.ToArray());
        var memberMatches = memberName is not null && fact.MemberCandidates.Any(candidate => NamesMatch(memberName, candidate));
        if (memberMatches && typeMatches)
        {
            return new EvidenceMatch(MatchStrength.TypeAndMember, false, "type-member");
        }

        if (memberMatches)
        {
            return new EvidenceMatch(MatchStrength.Member, typeIsConstrained, typeIsConstrained ? "member-type-unverified" : "member");
        }

        return EvidenceMatch.None;
    }

    private static EvidenceMatch MatchEndpoint(NormalizedChange change, IndexedFact fact)
    {
        if (fact.FactType is not (FactTypes.HttpRouteBinding or FactTypes.HttpCallDetected or FactTypes.HttpClientCreated))
        {
            return EvidenceMatch.None;
        }

        var expectedMethod = Value(change.Reference, "method", "httpMethod");
        var expectedPath = NormalizeEndpointPath(Value(change.Reference, "normalizedPathKey", "pathKey", "path", "routeTemplate"));
        var actualPathValue = Value(fact.Properties, "normalizedPathKey", "pathKey", "path", "routeTemplate", "routePath", "normalizedPathTemplate");
        var actualMethod = Value(fact.Properties, "httpMethod", "method", "verb") ?? HttpMethodFromEndpointKey(actualPathValue);
        var actualPath = NormalizeEndpointPath(actualPathValue);
        var methodMatches = expectedMethod is null || actualMethod is null || string.Equals(expectedMethod, actualMethod, StringComparison.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(expectedPath) && string.Equals(expectedPath, actualPath, StringComparison.OrdinalIgnoreCase) && methodMatches)
        {
            return new EvidenceMatch(expectedMethod is null ? MatchStrength.Textual : MatchStrength.Endpoint, expectedMethod is null || actualMethod is null, "endpoint");
        }

        return EvidenceMatch.None;
    }

    private static EvidenceMatch MatchPackage(NormalizedChange change, IndexedFact fact)
    {
        if (fact.FactType != FactTypes.PackageReferenced)
        {
            return EvidenceMatch.None;
        }

        var expectedName = Value(change.Reference, "packageName", "name");
        var expectedEcosystem = Value(change.Reference, "ecosystem");
        var actualName = Value(fact.Properties, "packageName", "name", "targetSymbol", "package");
        var actualEcosystem = Value(fact.Properties, "ecosystem", "packageManager");
        if (expectedName is not null && actualName is not null && NamesMatch(expectedName, actualName))
        {
            var ecosystemMatches = string.IsNullOrWhiteSpace(expectedEcosystem)
                || string.IsNullOrWhiteSpace(actualEcosystem)
                || string.Equals(expectedEcosystem, actualEcosystem, StringComparison.OrdinalIgnoreCase);
            return ecosystemMatches
                ? new EvidenceMatch(expectedEcosystem is null ? MatchStrength.Member : MatchStrength.TypeAndMember, expectedEcosystem is null || actualEcosystem is null, "package")
                : EvidenceMatch.None;
        }

        return EvidenceMatch.None;
    }

    private static EvidenceMatch MatchSchema(NormalizedChange change, IndexedFact fact)
    {
        return MatchSqlColumn(change, fact).Strength != MatchStrength.None
            ? MatchSqlColumn(change, fact)
            : MatchSqlTable(change, fact);
    }

    private static EvidenceMatch MatchSqlTable(NormalizedChange change, IndexedFact fact)
    {
        if (!IsSqlFact(fact))
        {
            return EvidenceMatch.None;
        }

        var expectedTables = ReferenceValues(change.Reference, "tableName", "tableNames", "schemaName", "name").ToArray();
        if (expectedTables.Length == 0)
        {
            return EvidenceMatch.None;
        }

        var tableMatches = PropertyValues(fact, "tableName", "tableNames", "schemaName", "entityName", "name")
            .Any(value => expectedTables.Any(expectedTable => NamesMatch(expectedTable, value)));
        return tableMatches ? new EvidenceMatch(MatchStrength.Member, false, SqlEvidenceKind(fact, "sql-schema-metadata")) : EvidenceMatch.None;
    }

    private static EvidenceMatch MatchSqlColumn(NormalizedChange change, IndexedFact fact)
    {
        if (!IsSqlFact(fact))
        {
            return EvidenceMatch.None;
        }

        var expectedColumns = ReferenceValues(change.Reference, "columnName", "columnNames", "mappedName", "propertyName", "name").ToArray();
        var expectedTables = ReferenceValues(change.Reference, "tableName", "tableNames").ToArray();
        if (expectedColumns.Length == 0)
        {
            return EvidenceMatch.None;
        }

        var columnMatches = PropertyValues(fact, "columnName", "columnNames", "fieldName", "fieldNames", "propertyName", "mappedName")
            .Any(value => expectedColumns.Any(expectedColumn => NamesMatch(expectedColumn, value)));
        var tableMatches = expectedTables.Length == 0
            || PropertyValues(fact, "tableName", "tableNames", "schemaName", "entityName")
                .Any(value => expectedTables.Any(expectedTable => NamesMatch(expectedTable, value)));
        return columnMatches && tableMatches
            ? new EvidenceMatch(expectedTables.Length == 0 ? MatchStrength.Member : MatchStrength.TypeAndMember, expectedTables.Length == 0, SqlEvidenceKind(fact, "sql-schema-metadata"))
            : EvidenceMatch.None;
    }

    private static EvidenceMatch MatchSqlQuery(NormalizedChange change, IndexedFact fact)
    {
        if (!IsSqlFact(fact))
        {
            return EvidenceMatch.None;
        }

        if (string.Equals(SurfaceKind(fact), "sql-persistence", StringComparison.OrdinalIgnoreCase))
        {
            return EvidenceMatch.None;
        }

        var expectedSourceKind = Value(change.Reference, "sqlSourceKind", "sourceKind");
        if (!SqlSourceKindMatchesOrAbsent(expectedSourceKind, fact))
        {
            return EvidenceMatch.None;
        }

        var expectedHash = Value(change.Reference, "queryShapeHash", "textHash");
        if (!string.IsNullOrWhiteSpace(expectedHash)
            && PropertyValues(fact, "queryShapeHash", "textHash", "sqlTextHash")
                .Any(value => string.Equals(value, expectedHash, StringComparison.Ordinal)))
        {
            var hashKind = change.Reference.ContainsKey("textHash") && !change.Reference.ContainsKey("queryShapeHash")
                ? "sql-text-hash"
                : SqlEvidenceKind(fact, "sql-query-shape");
            return new EvidenceMatch(MatchStrength.Exact, hashKind == "sql-text-hash", hashKind);
        }

        var expectedOperation = Value(change.Reference, "operationName", "name");
        if (!string.IsNullOrWhiteSpace(expectedOperation)
            && PropertyValues(fact, "operationName", "queryKind", "name")
                .Any(value => NamesMatch(expectedOperation, value)))
        {
            return new EvidenceMatch(MatchStrength.Member, true, SqlEvidenceKind(fact, "sql-query-shape"));
        }

        var expectedResource = Value(change.Reference, "sqlResourceName", "name");
        if (!string.IsNullOrWhiteSpace(expectedResource)
            && PropertyValues(fact, "sqlResourceName", "resourceName", "name", "fileName")
                .Any(value => NamesMatch(expectedResource, value)))
        {
            return new EvidenceMatch(MatchStrength.Member, true, "sql-resource");
        }

        if (Has(change.Reference, "columnName", "columnNames"))
        {
            return MatchSqlColumn(change, fact);
        }

        return MatchSqlTable(change, fact);
    }

    private static bool SqlSourceKindMatchesOrAbsent(string? expectedSourceKind, IndexedFact fact)
    {
        if (string.IsNullOrWhiteSpace(expectedSourceKind))
        {
            return true;
        }

        return PropertyValues(fact, "sqlSourceKind", "sourceKind")
            .Any(value => string.Equals(expectedSourceKind, value, StringComparison.OrdinalIgnoreCase));
    }

    private static string SqlEvidenceKind(IndexedFact fact, string fallback)
    {
        return fact.FactType switch
        {
            FactTypes.QueryPatternDetected => "sql-query-shape",
            FactTypes.SqlTextUsed => "sql-text-hash",
            FactTypes.SqlFileDeclared => "sql-resource",
            FactTypes.DatabaseColumnMapping => "sql-persistence-mapping",
            _ => fallback
        };
    }

    private static EvidenceMatch MatchDependencySurface(NormalizedChange change, IndexedFact fact)
    {
        var expectedKind = NormalizeSurfaceKind(Value(change.Reference, "surfaceKind", "kind"));
        var expectedEcosystem = Value(change.Reference, "ecosystem");
        var actualKind = SurfaceKind(fact);
        if (expectedKind is null)
        {
            return EvidenceMatch.None;
        }

        var kindMatches = string.Equals(expectedKind, actualKind, StringComparison.OrdinalIgnoreCase);
        if (!kindMatches)
        {
            return EvidenceMatch.None;
        }

        if (string.Equals(expectedKind, "sql-persistence", StringComparison.OrdinalIgnoreCase))
        {
            return MatchSqlPersistenceSurface(change, fact);
        }

        var expectedName = Value(change.Reference, "surfaceName", "packageName", "name", "stableKey", "tableName", "columnName", "mappedName");
        if (expectedName is null)
        {
            return EvidenceMatch.None;
        }

        var nameMatches = fact.SearchTextCandidates.Any(candidate => NamesMatch(expectedName, candidate))
            || PropertyValues(fact, "surfaceName", "stableKey", "name", "tableName", "packageName", "package", "path", "normalizedPathKey", "routeTemplate")
                .Any(value => NamesMatch(expectedName, value) || string.Equals(expectedName, value, StringComparison.OrdinalIgnoreCase));
        var ecosystemMatches = expectedEcosystem is null
            || !string.Equals(actualKind, "package-config", StringComparison.OrdinalIgnoreCase)
            || PropertyValues(fact, "ecosystem", "packageEcosystem", "packageManager")
                .Any(value => string.Equals(expectedEcosystem, value, StringComparison.OrdinalIgnoreCase));
        return nameMatches
            && ecosystemMatches
            ? new EvidenceMatch(MatchStrength.TypeAndMember, false, "dependency-surface")
            : EvidenceMatch.None;
    }

    private static EvidenceMatch MatchSqlPersistenceSurface(NormalizedChange change, IndexedFact fact)
    {
        var hasIdentity = Has(
            change.Reference,
            "surfaceName",
            "stableKey",
            "name",
            "tableName",
            "tableNames",
            "columnName",
            "columnNames",
            "mappedName",
            "propertyName");
        if (!hasIdentity)
        {
            return EvidenceMatch.None;
        }

        var surfaceMatches = ReferenceMatchesFactOrAbsent(
            change.Reference,
            fact,
            ["surfaceName", "stableKey", "name"],
            ["surfaceName", "stableKey", "name", "tableName", "columnName", "mappedName"]);
        var tableMatches = ReferenceMatchesFactOrAbsent(
            change.Reference,
            fact,
            ["tableName", "tableNames"],
            ["tableName", "tableNames", "schemaName", "entityName"]);
        var columnMatches = ReferenceMatchesFactOrAbsent(
            change.Reference,
            fact,
            ["columnName", "columnNames", "propertyName"],
            ["columnName", "columnNames", "fieldName", "fieldNames", "propertyName"]);
        var mappedMatches = ReferenceMatchesFactOrAbsent(
            change.Reference,
            fact,
            ["mappedName"],
            ["mappedName", "propertyName", "columnName"]);
        if (!surfaceMatches || !tableMatches || !columnMatches || !mappedMatches)
        {
            return EvidenceMatch.None;
        }

        var reviewOnly = !Has(change.Reference, "tableName", "tableNames")
            || !Has(change.Reference, "columnName", "columnNames", "mappedName", "propertyName");
        return new EvidenceMatch(MatchStrength.TypeAndMember, reviewOnly, "sql-persistence-mapping");
    }

    private static bool ReferenceMatchesFactOrAbsent(
        IReadOnlyDictionary<string, string> reference,
        IndexedFact fact,
        IReadOnlyList<string> referenceKeys,
        IReadOnlyList<string> factKeys)
    {
        var expectedValues = ReferenceValues(reference, referenceKeys.ToArray()).ToArray();
        if (expectedValues.Length == 0)
        {
            return true;
        }

        return PropertyValues(fact, factKeys.ToArray())
            .Any(actual => expectedValues.Any(expected => NamesMatch(expected, actual) || string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase)));
    }

    private static bool IsSqlFact(IndexedFact fact)
    {
        return fact.FactType is FactTypes.DatabaseColumnMapping
            or FactTypes.QueryPatternDetected
            or FactTypes.DapperCallDetected
            or FactTypes.SqlCommandDetected
            or FactTypes.SqlTextUsed
            or FactTypes.SqlFileDeclared
            or FactTypes.DbContextDeclared
            or FactTypes.DbSetDeclared
            or FactTypes.DbChangeSaved;
    }

    private static string SurfaceKind(IndexedFact fact)
    {
        if (fact.Properties.TryGetValue("surfaceKind", out var surfaceKind) && !string.IsNullOrWhiteSpace(surfaceKind))
        {
            return NormalizeSurfaceKind(surfaceKind) ?? surfaceKind;
        }

        if (fact.FactType == FactTypes.DatabaseColumnMapping)
        {
            return "sql-persistence";
        }

        if (fact.FactType is FactTypes.QueryPatternDetected or FactTypes.SqlTextUsed or FactTypes.SqlFileDeclared or FactTypes.DapperCallDetected or FactTypes.SqlCommandDetected)
        {
            return "sql-query";
        }

        if (IsSqlFact(fact))
        {
            return "sql";
        }

        if (fact.FactType is FactTypes.HttpRouteBinding)
        {
            return "http-route";
        }

        if (fact.FactType is FactTypes.HttpCallDetected or FactTypes.HttpClientCreated)
        {
            return "http-client";
        }

        if (fact.FactType is FactTypes.PackageReferenced)
        {
            return "package-config";
        }

        if (fact.FactType is FactTypes.ConfigKeyDeclared or FactTypes.ConnectionStringDeclared or FactTypes.ConfigBinding)
        {
            return "config";
        }

        return "symbol";
    }

    private static string? NormalizeSurfaceKind(string? surfaceKind)
    {
        if (string.IsNullOrWhiteSpace(surfaceKind))
        {
            return null;
        }

        return surfaceKind.Trim().Equals("package", StringComparison.OrdinalIgnoreCase)
            ? "package-config"
            : surfaceKind.Trim();
    }

    private static IEnumerable<string> PropertyValues(IndexedFact fact, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (fact.Properties.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                foreach (var part in SplitValue(value))
                {
                    yield return part;
                }
            }
        }
    }

    private static IEnumerable<string> SplitValue(string value)
    {
        foreach (var part in value.Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            yield return part;
        }
    }

    private static IEnumerable<string> SearchTerms(NormalizedChange change)
    {
        foreach (var value in change.Reference.Values)
        {
            if (!string.IsNullOrWhiteSpace(value) && !LooksLikeHash(value))
            {
                yield return value;
            }
        }
    }

    private static string? PrimaryName(NormalizedChange change)
    {
        return Value(change.Reference, "propertyName", "memberName", "methodName", "typeName", "packageName", "tableName", "columnName", "name");
    }

    private static ImpactEvidence ToEvidence(IndexedFact fact, EvidenceMatch match)
    {
        return new ImpactEvidence(
            fact.FactId,
            fact.FactType,
            fact.RuleId,
            fact.EvidenceTier,
            SafePath(fact.FilePath),
            fact.StartLine,
            fact.EndLine,
            SafeSymbol(fact.TargetSymbol),
            SafeSymbol(fact.ContractElement),
            fact.CommitSha)
        {
            SourceLabel = fact.SourceLabel,
            SourceIndexId = fact.SourceIndexId,
            ScanId = fact.ScanId,
            SourceSymbol = SafeSymbol(fact.SourceSymbol),
            Metadata = SafeMetadata(fact.Properties, match.EvidenceKind)
        };
    }

    private static ImpactEvidence BuildNoMatchEvidence(IndexData index, NormalizedChange change, ReduceOptions options, string classification)
    {
        var selectedSource = !string.IsNullOrWhiteSpace(options.Source)
            ? index.Summary.Sources.FirstOrDefault(source => string.Equals(source.Label, options.Source, StringComparison.Ordinal))
            : index.Summary.Sources.OrderBy(source => source.Label, StringComparer.Ordinal).FirstOrDefault();
        var commitSha = index.Manifest?.CommitSha
            ?? selectedSource?.CommitSha
            ?? index.Summary.CommitSha
            ?? "unknown";
        var analysisLevel = index.Manifest?.AnalysisLevel
            ?? selectedSource?.AnalysisLevel
            ?? index.Summary.AnalysisLevel
            ?? "unknown";
        var buildStatus = index.Manifest?.BuildStatus
            ?? selectedSource?.BuildStatus
            ?? index.Summary.BuildStatus
            ?? "unknown";
        var evidenceTier = classification is ImpactClassifications.NoEvidenceFullCoverage or ImpactClassifications.NoImpactEvidence
            ? EvidenceTiers.Tier2Structural
            : EvidenceTiers.Tier4Unknown;

        return new ImpactEvidence(
            $"evidence:no-match:{Hash($"{change.Id}:{options.Source}:{analysisLevel}:{buildStatus}:{commitSha}", 24)}",
            FactTypes.RepoScanned,
            RuleIds.RepoManifest,
            evidenceTier,
            "scan-manifest.json",
            1,
            1,
            "No matching facts",
            change.DisplayName,
            commitSha)
        {
            SourceLabel = options.Source ?? selectedSource?.Label,
            SourceIndexId = selectedSource?.SourceIndexId,
            ScanId = selectedSource?.ScanId,
            Metadata = new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["evidenceKind"] = "coverage-no-match",
                ["analysisLevel"] = analysisLevel,
                ["buildStatus"] = buildStatus,
                ["changeKind"] = change.InputKind,
                ["classificationBasis"] = classification,
                ["matchedFactCount"] = "0",
                ["sourceCount"] = index.Summary.SourceCount.ToString(CultureInfo.InvariantCulture)
            }
        };
    }

    private static async Task<IndexData> ReadSingleIndexAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var manifest = await ReadManifestAsync(connection, cancellationToken);
        var facts = await ReadFactsAsync(connection, null, cancellationToken);
        var summary = new ContractDeltaIndexSummary(
            "single",
            1,
            HashOrNull(manifest.RemoteUrl ?? manifest.RepoName),
            manifest.CommitSha,
            manifest.AnalysisLevel,
            manifest.BuildStatus,
            [
                new ContractDeltaSourceSummary(
                    "default",
                    null,
                    manifest.ScanId,
                    LanguageFromScanner(manifest.ScannerVersion),
                    manifest.CommitSha,
                    manifest.ScannerVersion,
                    manifest.AnalysisLevel,
                    manifest.BuildStatus,
                    HashOrNull(manifest.RemoteUrl ?? manifest.RepoName))
            ]);
        return new IndexData(false, manifest, summary, facts, []);
    }

    private static async Task<IndexData> ReadCombinedIndexAsync(SqliteConnection connection, string? sourceFilter, CancellationToken cancellationToken)
    {
        var sources = new List<ContractDeltaSourceSummary>();
        var manifests = new Dictionary<string, ScanManifest>(StringComparer.Ordinal);
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                select source_index_id, label, scan_id, repo_name, remote_url, commit_sha, scanner_version, language,
                       analysis_level, build_status, manifest_json
                from index_sources
                order by label;
                """;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var sourceIndexId = reader.GetString(0);
                var label = reader.GetString(1);
                if (!string.IsNullOrWhiteSpace(sourceFilter) && !string.Equals(sourceFilter, label, StringComparison.Ordinal))
                {
                    continue;
                }

                var manifestJson = reader.GetString(10);
                var manifest = JsonSerializer.Deserialize<ScanManifest>(manifestJson, LegacyJsonOptions)
                    ?? throw new InvalidDataException("Combined TraceMap source manifest could not be parsed.");
                manifests[sourceIndexId] = manifest;
                sources.Add(new ContractDeltaSourceSummary(
                    label,
                    sourceIndexId,
                    reader.GetString(2),
                    reader.IsDBNull(7) ? LanguageFromScanner(reader.GetString(6)) : reader.GetString(7),
                    reader.GetString(5),
                    reader.GetString(6),
                    reader.GetString(8),
                    reader.GetString(9),
                    HashOrNull((reader.IsDBNull(4) ? null : reader.GetString(4)) ?? reader.GetString(3))));
            }
        }

        if (!string.IsNullOrWhiteSpace(sourceFilter) && sources.Count == 0)
        {
            throw new InvalidDataException("Combined TraceMap index does not contain the requested --source label.");
        }

        var facts = await ReadFactsAsync(connection, sourceFilter, cancellationToken);
        var projectedCombinedSqlSurfaceFacts = ProjectCombinedSqlSurfaceFacts(facts);
        var summary = new ContractDeltaIndexSummary(
            "combined",
            sources.Count,
            null,
            null,
            null,
            null,
            sources);
        return new IndexData(true, sources.Count == 1 ? manifests.Values.FirstOrDefault() : null, summary, facts, projectedCombinedSqlSurfaceFacts);
    }

    private static IReadOnlyList<IndexedFact> ProjectCombinedSqlSurfaceFacts(IReadOnlyList<IndexedFact> facts)
    {
        var byCombinedFactId = facts.ToDictionary(fact => fact.FactId, StringComparer.Ordinal);
        return CombinedSurfaceProjection.BuildSurfaces(facts.Select(ToSurfaceProjectionInput).ToArray())
            .Where(surface => surface.SurfaceKind is "sql-query" or "sql-persistence")
            .Select(surface => ToProjectedSurfaceFact(surface, byCombinedFactId))
            .OrderBy(fact => fact.SourceLabel ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(fact => SurfaceKind(fact), StringComparer.Ordinal)
            .ThenBy(fact => fact.Properties.TryGetValue("surfaceName", out var surfaceName) ? surfaceName : string.Empty, StringComparer.Ordinal)
            .ThenBy(fact => fact.FilePath, StringComparer.Ordinal)
            .ThenBy(fact => fact.StartLine)
            .ThenBy(fact => fact.FactId, StringComparer.Ordinal)
            .ToArray();
    }

    private static CombinedSurfaceFactInput ToSurfaceProjectionInput(IndexedFact fact)
    {
        return new CombinedSurfaceFactInput(
            fact.FactId,
            fact.SourceIndexId ?? string.Empty,
            fact.SourceLabel ?? string.Empty,
            fact.OriginalFactId,
            fact.ScanId ?? string.Empty,
            fact.CommitSha,
            fact.FactType,
            fact.RuleId,
            fact.EvidenceTier,
            fact.FilePath,
            fact.StartLine,
            fact.EndLine,
            fact.Properties);
    }

    private static IndexedFact ToProjectedSurfaceFact(CombinedSurfaceProjectionRow surface, IReadOnlyDictionary<string, IndexedFact> factsById)
    {
        factsById.TryGetValue(surface.CombinedFactId, out var original);
        return new IndexedFact(
            surface.CombinedFactId,
            surface.OriginalFactId,
            surface.CommitSha,
            surface.FactType,
            surface.RuleId,
            surface.EvidenceTier,
            original?.SourceSymbol,
            original?.TargetSymbol,
            original?.ContractElement,
            surface.FilePath,
            surface.StartLine,
            surface.EndLine,
            BuildProjectedSurfaceProperties(surface, original?.Properties),
            surface.SourceLabel,
            surface.SourceIndexId,
            original?.SourceAnalysisLevel,
            original?.SourceBuildStatus,
            surface.ScanId);
    }

    private static IReadOnlyDictionary<string, string> BuildProjectedSurfaceProperties(CombinedSurfaceProjectionRow surface, IReadOnlyDictionary<string, string>? originalProperties)
    {
        var properties = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["surfaceKind"] = surface.SurfaceKind,
            ["surfaceName"] = surface.DisplayName,
            ["sourceIndexId"] = surface.SourceIndexId,
            ["sourceLabel"] = surface.SourceLabel,
            ["scanId"] = surface.ScanId
        };
        AddIfPresent(properties, "operationName", surface.OperationName);
        AddIfPresent(properties, "tableName", surface.TableName);
        AddIfPresent(properties, "columnNames", surface.ColumnNames);
        AddIfPresent(properties, "sourceKind", surface.SourceKind);
        AddIfPresent(properties, "sqlSourceKind", surface.SourceKind);
        AddIfPresent(properties, "queryShapeHash", surface.ShapeHash);
        AddIfPresent(properties, "textHash", surface.TextHash);
        AddIfPresent(properties, "textLength", surface.TextLength);
        AddIfPresent(properties, "packageName", surface.PackageName);
        AddIfPresent(properties, "configKey", surface.ConfigKey);
        AddIfPresent(properties, "ecosystem", surface.Ecosystem);
        AddIfPresent(properties, "manifestKind", surface.ManifestKind);
        AddIfPresent(properties, "dependencyScope", surface.DependencyScope);
        AddIfPresent(properties, "dependencyGroup", surface.DependencyGroup);
        AddIfPresent(properties, "packageManager", surface.PackageManager);
        AddIfPresent(properties, "versionHash", surface.VersionHash);
        AddIfPresent(properties, "redactionReason", surface.RedactionReason);

        if (originalProperties is not null)
        {
            foreach (var key in new[] { "mappedName", "propertyName", "mappingKind", "entityName", "schemaName", "stableKey", "name", "sqlResourceName", "resourceName", "fileName" })
            {
                if (originalProperties.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                {
                    var sanitized = SanitizeReferenceValue(key, value);
                    if (!sanitized.StartsWith("value-hash:", StringComparison.Ordinal))
                    {
                        properties[key] = sanitized;
                    }
                }
            }
        }

        return properties;
    }

    private static void AddIfPresent(SortedDictionary<string, string> properties, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            properties[key] = value;
        }
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

        return JsonSerializer.Deserialize<ScanManifest>(json, LegacyJsonOptions)
            ?? throw new InvalidDataException("TraceMap scan manifest could not be parsed.");
    }

    private static async Task<IReadOnlyList<IndexedFact>> ReadFactsAsync(SqliteConnection connection, string? sourceFilter, CancellationToken cancellationToken)
    {
        var isCombined = await TableExistsAsync(connection, "combined_facts", cancellationToken);
        var facts = new List<IndexedFact>();
        await using var command = connection.CreateCommand();
        if (isCombined)
        {
            command.CommandText = """
                select cf.combined_fact_id,
                       cf.original_fact_id,
                       cf.commit_sha,
                       cf.fact_type,
                       cf.rule_id,
                       cf.evidence_tier,
                       cf.source_symbol,
                       cf.target_symbol,
                       cf.contract_element,
                       cf.file_path,
                       cf.start_line,
                       cf.end_line,
                       cf.properties_json,
                       s.label,
                       s.source_index_id,
                       s.analysis_level,
                       s.build_status,
                       cf.scan_id
                from combined_facts cf
                join index_sources s on s.source_index_id = cf.source_index_id
                where $source is null or s.label = $source
                order by s.label, cf.file_path, cf.start_line, cf.fact_type, cf.combined_fact_id;
                """;
            command.Parameters.AddWithValue("$source", string.IsNullOrWhiteSpace(sourceFilter) ? DBNull.Value : sourceFilter);
        }
        else
        {
            command.CommandText = """
                select fact_id,
                       fact_id,
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
                       properties_json,
                       null,
                       null,
                       null,
                       null,
                       null
                from facts
                order by file_path, start_line, fact_type, fact_id;
                """;
        }

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            facts.Add(new IndexedFact(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetString(8),
                reader.GetString(9),
                reader.GetInt32(10),
                reader.GetInt32(11),
                ReadProperties(reader.GetString(12)),
                reader.IsDBNull(13) ? null : reader.GetString(13),
                reader.IsDBNull(14) ? null : reader.GetString(14),
                reader.IsDBNull(15) ? null : reader.GetString(15),
                reader.IsDBNull(16) ? null : reader.GetString(16),
                reader.IsDBNull(17) ? null : reader.GetString(17)));
        }

        return facts;
    }

    private static IReadOnlyDictionary<string, string> ReadProperties(string json)
    {
        return JsonSerializer.Deserialize<SortedDictionary<string, string>>(json, LegacyJsonOptions)
            ?? new SortedDictionary<string, string>(StringComparer.Ordinal);
    }

    private static async Task<bool> TableExistsAsync(SqliteConnection connection, string tableName, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "select 1 from sqlite_master where type = 'table' and name = $name limit 1;";
        command.Parameters.AddWithValue("$name", tableName);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is not null;
    }

    private static IReadOnlyList<ContractDeltaImpactGap> BuildCoverageGaps(IndexData index, int changeCount)
    {
        var gaps = new List<ContractDeltaImpactGap>();
        foreach (var source in index.Summary.Sources)
        {
            if (!IsFullCoverage(source.AnalysisLevel, source.BuildStatus, source.CommitSha))
            {
                gaps.Add(new ContractDeltaImpactGap(
                    $"gap:coverage:{Hash($"{source.Label}:{source.AnalysisLevel}:{source.BuildStatus}:{source.CommitSha}", 16)}",
                    "ReducedCoverage",
                    null,
                    source.Label,
                    RuleIds.ContractDeltaImpact,
                    EvidenceTiers.Tier4Unknown,
                    ImpactClassifications.UnknownAnalysisGap,
                    "Source scan coverage is reduced or commit identity is unknown; absence-of-evidence findings are coverage-relative.",
                    []));
            }
        }

        if (changeCount == 0)
        {
            gaps.Add(new ContractDeltaImpactGap(
                "gap:input:no-changes",
                "InputNoChanges",
                null,
                null,
                RuleIds.ContractDeltaInput,
                EvidenceTiers.Tier4Unknown,
                ImpactClassifications.SelectorNoMatch,
                "Contract delta input contains no changes.",
                []));
        }

        return gaps;
    }

    private static IReadOnlyList<string> BuildCoverageWarnings(IndexData index)
    {
        return index.Summary.Sources
            .Where(source => !IsFullCoverage(source.AnalysisLevel, source.BuildStatus, source.CommitSha))
            .Select(source => $"Source `{source.Label}` has coverage `{source.AnalysisLevel ?? "unknown"}` build `{source.BuildStatus ?? "unknown"}` commit `{source.CommitSha ?? "unknown"}`.")
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool HasFullCoverage(IndexData index)
    {
        return index.Summary.Sources.Count > 0
            && index.Summary.Sources.All(source => IsFullCoverage(source.AnalysisLevel, source.BuildStatus, source.CommitSha));
    }

    private static bool IsFullCoverage(string? analysisLevel, string? buildStatus, string? commitSha)
    {
        return string.Equals(analysisLevel, "Level1SemanticAnalysis", StringComparison.Ordinal)
            && string.Equals(buildStatus, "Succeeded", StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(commitSha)
            && !string.Equals(commitSha, "unknown", StringComparison.OrdinalIgnoreCase);
    }

    private static string DetermineReportCoverage(IndexData index, IReadOnlyList<ImpactFinding> findings, IReadOnlyList<ContractDeltaImpactGap> gaps)
    {
        if (gaps.Any(gap => gap.Classification == ImpactClassifications.TruncatedByLimit)
            || findings.Any(finding => finding.Classification == ImpactClassifications.TruncatedByLimit))
        {
            return "Partial";
        }

        if (gaps.Any(gap => gap.Classification == ImpactClassifications.UnknownAnalysisGap)
            || findings.Any(finding => finding.Classification == ImpactClassifications.UnknownAnalysisGap))
        {
            return "Reduced";
        }

        return HasFullCoverage(index) ? "Full" : "Reduced";
    }

    private static IReadOnlyList<ContractDeltaImpactGap> SortAndCapGaps(IReadOnlyList<ContractDeltaImpactGap> gaps, int maxGaps, out bool truncated)
    {
        truncated = gaps.Count > maxGaps;
        return gaps
            .OrderBy(gap => gap.GapKind, StringComparer.Ordinal)
            .ThenBy(gap => gap.ChangeId ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(gap => gap.SourceLabel ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(gap => gap.GapId, StringComparer.Ordinal)
            .Take(maxGaps)
            .ToArray();
    }

    private static bool HasActionableFindings(ImpactReport report)
    {
        if (report.ReportType is SqlSingleReportType or SqlCombinedReportType)
        {
            return report.Findings.Any(finding => finding.Classification is
                ImpactClassifications.DefiniteImpact
                or ImpactClassifications.ProbableImpact
                or ImpactClassifications.StaticImpactEvidence
                or ImpactClassifications.ProbableStaticImpact);
        }

        return report.Findings.Any(finding => finding.Classification is
            ImpactClassifications.DefiniteImpact
            or ImpactClassifications.ProbableImpact
            or ImpactClassifications.NeedsReview
            or ImpactClassifications.StaticImpactEvidence
            or ImpactClassifications.ProbableStaticImpact
            or ImpactClassifications.NeedsReviewImpact);
    }

    private static string ConfidenceFor(string classification, bool isSqlSchemaDelta)
    {
        if (!isSqlSchemaDelta)
        {
            return classification switch
            {
                ImpactClassifications.DefiniteImpact or ImpactClassifications.StaticImpactEvidence => "high",
                ImpactClassifications.ProbableImpact or ImpactClassifications.ProbableStaticImpact => "medium",
                ImpactClassifications.NeedsReview or ImpactClassifications.NeedsReviewImpact => "review",
                ImpactClassifications.NoEvidenceFullCoverage or ImpactClassifications.NoImpactEvidence => "coverage-relative-none",
                _ => "unknown"
            };
        }

        return classification switch
        {
            ImpactClassifications.DefiniteImpact or ImpactClassifications.StaticImpactEvidence => "High",
            ImpactClassifications.ProbableImpact or ImpactClassifications.ProbableStaticImpact => "Medium",
            _ => "Low"
        };
    }

    private static string HighestEvidenceTier(IReadOnlyList<ImpactEvidence> evidence)
    {
        if (evidence.Any(row => row.EvidenceTier == EvidenceTiers.Tier1Semantic))
        {
            return EvidenceTiers.Tier1Semantic;
        }

        if (evidence.Any(row => row.EvidenceTier == EvidenceTiers.Tier2Structural))
        {
            return EvidenceTiers.Tier2Structural;
        }

        if (evidence.Any(row => row.EvidenceTier == EvidenceTiers.Tier3SyntaxOrTextual))
        {
            return EvidenceTiers.Tier3SyntaxOrTextual;
        }

        return EvidenceTiers.Tier4Unknown;
    }

    private static ScanManifest PlaceholderManifest(IndexData index)
    {
        var first = index.Summary.Sources.FirstOrDefault();
        return new ScanManifest(
            "combined",
            "combined",
            null,
            null,
            first?.CommitSha ?? "unknown",
            ScannerVersions.TraceMap,
            DateTimeOffset.UnixEpoch,
            first?.AnalysisLevel ?? "unknown",
            first?.BuildStatus ?? "unknown",
            [],
            [],
            [],
            []);
    }

    private static async Task<(string? MarkdownPath, string? JsonPath)> WriteOutputsAsync(string outputPath, string format, ImpactReport report, CancellationToken cancellationToken)
    {
        var fullPath = Path.GetFullPath(outputPath);
        var extension = Path.GetExtension(fullPath);
        var isDirectoryTarget = string.IsNullOrWhiteSpace(extension);
        var isSqlReport = report.ReportType is SqlSingleReportType or SqlCombinedReportType;
        if (isDirectoryTarget)
        {
            Directory.CreateDirectory(fullPath);
            var markdownPath = Path.Combine(fullPath, isSqlReport ? "sql-impact-report.md" : "impact-report.md");
            var jsonPath = Path.Combine(fullPath, isSqlReport ? "sql-impact-report.json" : "impact-report.json");
            if (isSqlReport && format == "json")
            {
                await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(report, OutputJsonOptions), cancellationToken);
                return (null, jsonPath);
            }

            await File.WriteAllTextAsync(markdownPath, ImpactMarkdownWriter.Build(report), cancellationToken);
            await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(report, OutputJsonOptions), cancellationToken);
            return (markdownPath, jsonPath);
        }

        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        if (format == "json" || extension.Equals(".json", StringComparison.OrdinalIgnoreCase))
        {
            await File.WriteAllTextAsync(fullPath, JsonSerializer.Serialize(report, OutputJsonOptions), cancellationToken);
            return (null, fullPath);
        }

        await File.WriteAllTextAsync(fullPath, ImpactMarkdownWriter.Build(report), cancellationToken);
        return (fullPath, null);
    }

    private static string NormalizeFormat(string? format)
    {
        var normalized = string.IsNullOrWhiteSpace(format) ? "markdown" : format.Trim().ToLowerInvariant();
        return normalized switch
        {
            "md" => "markdown",
            "markdown" => "markdown",
            "json" => "json",
            _ => throw new ArgumentException("reduce --format must be markdown or json.")
        };
    }

    private static IReadOnlyList<string> NormalizeScopes(string? scope)
    {
        if (string.IsNullOrWhiteSpace(scope))
        {
            return ["all"];
        }

        var scopes = scope.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (scopes.Length == 0)
        {
            return ["all"];
        }

        foreach (var value in scopes)
        {
            if (!ValidScopes.Contains(value))
            {
                throw new ArgumentException("reduce --scope contains an unsupported contract delta kind.");
            }
        }

        return scopes.Contains("all", StringComparer.Ordinal) ? ["all"] : scopes.OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    private static IReadOnlyDictionary<string, string> ParseStringMap(JsonElement element, bool sanitizeValues)
    {
        var values = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal))
        {
            var value = property.Value.ValueKind switch
            {
                JsonValueKind.String => property.Value.GetString(),
                JsonValueKind.Number => property.Value.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => null
            };
            if (!string.IsNullOrWhiteSpace(value))
            {
                values[property.Name] = sanitizeValues ? SanitizeScalar(value) : value;
            }
        }

        return values;
    }

    private static IReadOnlyDictionary<string, string> ParseReferenceMap(JsonElement element)
    {
        var raw = ParseStringMap(element, sanitizeValues: false);
        return raw
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .ToDictionary(pair => pair.Key, pair => SanitizeReferenceValue(pair.Key, pair.Value), StringComparer.Ordinal);
    }

    private static string SanitizeReferenceValue(string key, string value)
    {
        var normalizedKey = key.ToLowerInvariant();
        if (normalizedKey.Contains("sql", StringComparison.Ordinal) && normalizedKey is not ("queryshapehash" or "texthash" or "sqlsourcekind" or "sqlresourcename"))
        {
            return $"value-hash:{Hash(value, 16)}";
        }

        if (normalizedKey.Contains("value", StringComparison.Ordinal)
            || normalizedKey.Contains("literal", StringComparison.Ordinal)
            || normalizedKey.Contains("connection", StringComparison.Ordinal)
            || normalizedKey.Contains("secret", StringComparison.Ordinal)
            || normalizedKey.Contains("token", StringComparison.Ordinal)
            || normalizedKey.Contains("password", StringComparison.Ordinal)
            || value.Contains("://", StringComparison.Ordinal)
            || value.StartsWith($"{Path.DirectorySeparatorChar}Users{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            || value.StartsWith("/home/", StringComparison.Ordinal)
            || value.Contains(":\\", StringComparison.Ordinal))
        {
            return $"value-hash:{Hash(value, 16)}";
        }

        return value.Trim();
    }

    private static string GetRequiredString(JsonElement element, string propertyName, string message)
    {
        var value = GetOptionalString(element, propertyName);
        return string.IsNullOrWhiteSpace(value) ? throw new InvalidDataException(message) : value;
    }

    private static string? GetOptionalString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()?.Trim()
            : null;
    }

    private static string SanitizeScalar(string value)
    {
        var trimmed = value.Trim();
        if (LooksUnsafe(trimmed))
        {
            return $"value-hash:{Hash(trimmed, 16)}";
        }

        return trimmed;
    }

    private static IReadOnlyDictionary<string, string> SafeMetadata(IReadOnlyDictionary<string, string> properties, string evidenceKind)
    {
        var safe = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["evidenceKind"] = evidenceKind
        };

        foreach (var (key, value) in properties.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (!IsRenderableMetadataKey(key))
            {
                continue;
            }

            safe[key] = LooksUnsafe(value) ? $"value-hash:{Hash(value, 16)}" : value;
        }

        return safe;
    }

    private static bool IsRenderableMetadataKey(string key)
    {
        var normalized = key.ToLowerInvariant();
        if (normalized.Contains("connection", StringComparison.Ordinal)
            || normalized.Contains("secret", StringComparison.Ordinal)
            || normalized.Contains("token", StringComparison.Ordinal)
            || normalized.Contains("password", StringComparison.Ordinal)
            || normalized is "sql" or "sqltext" or "rawsql" or "value" or "literal" or "rawvalue")
        {
            return false;
        }

        return normalized.Contains("name", StringComparison.Ordinal)
            || normalized.Contains("kind", StringComparison.Ordinal)
            || normalized.Contains("type", StringComparison.Ordinal)
            || normalized.Contains("method", StringComparison.Ordinal)
            || normalized.Contains("path", StringComparison.Ordinal)
            || normalized.Contains("route", StringComparison.Ordinal)
            || normalized.Contains("hash", StringComparison.Ordinal)
            || normalized.Contains("column", StringComparison.Ordinal)
            || normalized.Contains("table", StringComparison.Ordinal)
            || normalized.Contains("package", StringComparison.Ordinal)
            || normalized.Contains("ecosystem", StringComparison.Ordinal)
            || normalized.Contains("symbol", StringComparison.Ordinal)
            || normalized.Contains("operation", StringComparison.Ordinal)
            || normalized.Contains("surface", StringComparison.Ordinal);
    }

    private static string SafePath(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return "unknown";
        }

        if (Path.IsPathRooted(filePath) || filePath.Contains("://", StringComparison.Ordinal))
        {
            return $"path-hash:{Hash(filePath, 16)}";
        }

        return filePath.Replace('\\', '/');
    }

    private static string? SafeSymbol(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return LooksUnsafe(value) ? $"symbol-hash:{Hash(value, 16)}" : value;
    }

    private static bool LooksUnsafe(string value)
    {
        return Path.IsPathRooted(value)
            || value.Contains("://", StringComparison.Ordinal)
            || value.Contains("Server=", StringComparison.OrdinalIgnoreCase)
            || value.Contains("Password=", StringComparison.OrdinalIgnoreCase)
            || value.Contains("User Id=", StringComparison.OrdinalIgnoreCase)
            || value.Contains('\n')
            || value.Contains('\r');
    }

    private static bool LooksLikeHash(string value)
    {
        return value.Length >= 12 && value.All(character => Uri.IsHexDigit(character) || character is '-' or '_' or ':');
    }

    private static string NormalizeEndpointPath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        var withoutMethod = StripHttpMethodPrefix(trimmed);
        if (!string.IsNullOrWhiteSpace(withoutMethod))
        {
            trimmed = withoutMethod;
        }

        return trimmed.StartsWith("/", StringComparison.Ordinal) ? trimmed : "/" + trimmed;
    }

    private static string? HttpMethodFromEndpointKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var parts = value.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 2 && IsHttpMethod(parts[0]) ? parts[0] : null;
    }

    private static string? StripHttpMethodPrefix(string value)
    {
        var parts = value.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 2 && IsHttpMethod(parts[0]) ? parts[1] : null;
    }

    private static bool IsHttpMethod(string value)
    {
        return value is "GET" or "POST" or "PUT" or "PATCH" or "DELETE" or "HEAD" or "OPTIONS" or "TRACE"
            || value.Equals("get", StringComparison.OrdinalIgnoreCase)
            || value.Equals("post", StringComparison.OrdinalIgnoreCase)
            || value.Equals("put", StringComparison.OrdinalIgnoreCase)
            || value.Equals("patch", StringComparison.OrdinalIgnoreCase)
            || value.Equals("delete", StringComparison.OrdinalIgnoreCase)
            || value.Equals("head", StringComparison.OrdinalIgnoreCase)
            || value.Equals("options", StringComparison.OrdinalIgnoreCase)
            || value.Equals("trace", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> SignatureCandidates(IndexedFact fact)
    {
        foreach (var value in PropertyValues(fact, "signature", "methodSignature", "sourceSymbolDisplayName", "targetSymbolDisplayName", "sourceSymbol", "targetSymbol"))
        {
            yield return value;
        }

        if (!string.IsNullOrWhiteSpace(fact.SourceSymbol))
        {
            yield return fact.SourceSymbol;
        }

        if (!string.IsNullOrWhiteSpace(fact.TargetSymbol))
        {
            yield return fact.TargetSymbol;
        }
    }

    private static bool SignaturesMatch(string expected, string actual)
    {
        return string.Equals(NormalizeSignature(expected), NormalizeSignature(actual), StringComparison.Ordinal);
    }

    private static string NormalizeSignature(string value)
    {
        return new string(value
            .Replace("global::", string.Empty, StringComparison.Ordinal)
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
    }

    private static bool NamesMatch(string expected, string actual)
    {
        return string.Equals(NormalizeName(expected), NormalizeName(actual), StringComparison.Ordinal);
    }

    private static bool TextMentionsName(string text, string expected)
    {
        var normalizedText = NormalizeName(text);
        var normalizedExpected = NormalizeName(expected);
        return normalizedExpected.Length > 0
            && normalizedText.Contains(normalizedExpected, StringComparison.Ordinal);
    }

    private static string NormalizeName(string value)
    {
        return new string(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    }

    private static string HashOrNull(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? "unknown" : Hash(value, 24);
    }

    private static string Hash(string value, int length = 64)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        var hex = Convert.ToHexString(bytes).ToLowerInvariant();
        return hex[..Math.Min(length, hex.Length)];
    }

    private static string LanguageFromScanner(string? scannerVersion)
    {
        if (string.IsNullOrWhiteSpace(scannerVersion))
        {
            return "unknown";
        }

        if (scannerVersion.Contains("typescript", StringComparison.OrdinalIgnoreCase))
        {
            return "typescript";
        }

        if (scannerVersion.Contains("jvm", StringComparison.OrdinalIgnoreCase))
        {
            return "jvm";
        }

        if (scannerVersion.Contains("python", StringComparison.OrdinalIgnoreCase))
        {
            return "python";
        }

        return "dotnet";
    }

    private sealed record ContractDeltaInput(
        string Version,
        string Compatibility,
        string? Contract,
        IReadOnlyDictionary<string, string> Source,
        IReadOnlyList<NormalizedChange> Changes,
        ContractDelta? LegacyDelta,
        bool IsSqlSchemaDelta);

    private sealed record NormalizedChange(
        string Id,
        string Kind,
        string InputKind,
        string ChangeType,
        IReadOnlyDictionary<string, string> Reference,
        string DisplayName,
        bool IsLegacy,
        string Specificity);

    private sealed record IndexData(
        bool IsCombined,
        ScanManifest? Manifest,
        ContractDeltaIndexSummary Summary,
        IReadOnlyList<IndexedFact> Facts,
        IReadOnlyList<IndexedFact> ProjectedCombinedSqlSurfaceFacts);

    private sealed record EvidenceMatch(MatchStrength Strength, bool ReviewOnly, string EvidenceKind)
    {
        public static EvidenceMatch None { get; } = new(MatchStrength.None, false, "none");
    }

    private enum MatchStrength
    {
        None = 0,
        Textual = 1,
        Type = 2,
        Member = 3,
        TypeAndMember = 4,
        Endpoint = 5,
        Exact = 6
    }

    private sealed record ContractElementName(string? TypeName, string? MemberName)
    {
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
        string OriginalFactId,
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
        IReadOnlyDictionary<string, string> Properties,
        string? SourceLabel,
        string? SourceIndexId,
        string? SourceAnalysisLevel,
        string? SourceBuildStatus,
        string? ScanId)
    {
        public IEnumerable<string> MemberCandidates
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(ContractElement))
                {
                    yield return ContractElement;
                }

                foreach (var key in new[]
                {
                    "propertyName",
                    "memberName",
                    "fieldName",
                    "methodName",
                    "signature",
                    "keyPath",
                    "jsonName",
                    "columnName",
                    "name",
                    "sourceSymbol",
                    "targetSymbol",
                    "sourceSymbolDisplayName",
                    "targetSymbolDisplayName",
                    "contractName",
                    "routeTemplate",
                    "path",
                    "normalizedPathKey",
                    "packageName",
                    "tableName",
                    "queryShapeHash",
                    "surfaceName",
                    "stableKey"
                })
                {
                    if (Properties.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                    {
                        yield return LastSymbolPart(value);
                    }
                }

                if (!string.IsNullOrWhiteSpace(SourceSymbol))
                {
                    yield return LastSymbolPart(SourceSymbol);
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
                foreach (var key in new[]
                {
                    "containingType",
                    "className",
                    "typeName",
                    "fullyQualifiedName",
                    "namespace",
                    "name",
                    "serviceType",
                    "implementationType",
                    "declaringType",
                    "sourceSymbol",
                    "targetSymbol",
                    "sourceSymbolDisplayName",
                    "targetSymbolDisplayName"
                })
                {
                    if (Properties.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                    {
                        foreach (var candidate in TypeCandidatesFromSymbol(value))
                        {
                            yield return candidate;
                        }
                    }
                }

                if (!string.IsNullOrWhiteSpace(SourceSymbol))
                {
                    foreach (var candidate in TypeCandidatesFromSymbol(SourceSymbol))
                    {
                        yield return candidate;
                    }
                }

                if (!string.IsNullOrWhiteSpace(TargetSymbol))
                {
                    foreach (var candidate in TypeCandidatesFromSymbol(TargetSymbol))
                    {
                        yield return candidate;
                    }
                }
            }
        }

        public bool HasSymbolIdentity =>
            Properties.ContainsKey("sourceSymbolId")
            || Properties.ContainsKey("targetSymbolId")
            || Properties.ContainsKey("argumentSymbolId")
            || Properties.ContainsKey("parameterSymbolId")
            || Properties.ContainsKey("originSymbolId")
            || Properties.ContainsKey("constructorSymbolId")
            || Properties.ContainsKey("symbolId");

        public IEnumerable<string> SearchTextCandidates
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(SourceSymbol))
                {
                    yield return SourceSymbol;
                }

                if (!string.IsNullOrWhiteSpace(TargetSymbol))
                {
                    yield return TargetSymbol;
                }

                if (!string.IsNullOrWhiteSpace(ContractElement))
                {
                    yield return ContractElement;
                }

                foreach (var value in Properties.Values)
                {
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        yield return value;
                    }
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

        private static IEnumerable<string> TypeCandidatesFromSymbol(string value)
        {
            var normalized = value
                .Replace("global::", string.Empty, StringComparison.Ordinal)
                .Split('(', StringSplitOptions.TrimEntries)[0]
                .Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                yield break;
            }

            yield return LastSymbolPart(normalized);

            var lastDot = normalized.LastIndexOf('.');
            if (lastDot <= 0)
            {
                yield break;
            }

            var containing = normalized[..lastDot];
            var containingLastDot = containing.LastIndexOf('.');
            yield return containingLastDot >= 0 ? containing[(containingLastDot + 1)..] : containing;
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
            "## Report",
            "",
            $"- Report type: `{report.ReportType}`",
            $"- Version: `{report.Version}`",
            $"- Input compatibility: `{report.InputCompatibility}`",
            $"- Report coverage: `{report.ReportCoverage}`",
            "",
            "## Repository",
            "",
            $"- Index kind: `{report.Index.IndexKind}`",
            $"- Sources: `{report.Index.SourceCount}`",
            $"- Repo identity hash: `{report.Index.RepoIdentityHash ?? "combined"}`",
            $"- Commit SHA: `{report.Index.CommitSha ?? report.Manifest.CommitSha}`",
            $"- Analysis level: `{report.Index.AnalysisLevel ?? report.Manifest.AnalysisLevel}`",
            $"- Build status: `{report.Index.BuildStatus ?? report.Manifest.BuildStatus}`",
            $"- Scanner version: `{Cell(report.Index.Sources.Count == 1 ? report.Index.Sources[0].ScannerVersion ?? report.Manifest.ScannerVersion : report.Manifest.ScannerVersion)}`",
            "",
            "## Contract Delta",
            "",
            $"- Contract: `{Cell(report.Input.Contract ?? report.Delta.Contract ?? "unknown")}`",
            $"- Source: `{Cell(report.Input.Source.TryGetValue("label", out var inputSourceLabel) ? inputSourceLabel : report.Delta.Source ?? "unknown")}`",
            $"- Changes: `{report.Summary.ChangeCount}`",
            ""
        };

        if (report.Index.Sources.Count > 0)
        {
            lines.Add("## Sources");
            lines.Add("");
            lines.Add("| Label | Language | Scanner version | Commit | Analysis | Build |");
            lines.Add("| --- | --- | --- | --- | --- | --- |");
            foreach (var source in report.Index.Sources.OrderBy(source => source.Label, StringComparer.Ordinal))
            {
                lines.Add($"| `{Cell(source.Label)}` | `{Cell(source.Language ?? "unknown")}` | `{Cell(source.ScannerVersion ?? "unknown")}` | `{Cell(source.CommitSha ?? "unknown")}` | `{Cell(source.AnalysisLevel ?? "unknown")}` | `{Cell(source.BuildStatus ?? "unknown")}` |");
            }

            lines.Add("");
        }

        lines.Add("## Findings");
        lines.Add("");

        if (report.Findings.Count == 0)
        {
            lines.Add("- No changes were present in the contract delta or no changes matched the selected filters.");
            lines.Add("");
        }
        else
        {
            foreach (var finding in report.Findings)
            {
                lines.Add($"### `{Cell(finding.Element)}`");
                lines.Add("");
                lines.Add($"- Change id: `{Cell(finding.ChangeId)}`");
                lines.Add($"- Change kind: `{Cell(finding.ChangeKind)}`");
                lines.Add($"- Change type: `{Cell(finding.ChangeType ?? "unknown")}`");
                lines.Add($"- Classification: `{Cell(finding.Classification)}`");
                lines.Add($"- Confidence: `{Cell(finding.Confidence)}`");
                lines.Add($"- Evidence tier: `{Cell(finding.EvidenceTier)}`");
                lines.Add($"- Reducer rule: `{Cell(finding.RuleId)}`");
                lines.Add($"- Reason: {Cell(finding.Reason)}");
                if (finding.Warnings.Count > 0)
                {
                    lines.Add("- Warnings:");
                    foreach (var warning in finding.Warnings)
                    {
                        lines.Add($"  - {Cell(warning)}");
                    }
                }

                if (finding.Limitations.Count > 0)
                {
                    lines.Add("- Limitations:");
                    foreach (var limitation in finding.Limitations)
                    {
                        lines.Add($"  - {Cell(limitation)}");
                    }
                }

                lines.Add("");
                lines.Add("Evidence:");
                lines.Add("");

                if (finding.Evidence.Count == 0)
                {
                    lines.Add($"- Manifest coverage evidence: analysis `{Cell(report.Manifest.AnalysisLevel)}`, build `{Cell(report.Manifest.BuildStatus)}`, commit `{Cell(report.Manifest.CommitSha)}`.");
                }
                else
                {
                    if (finding.Evidence.Any(evidence => evidence.Metadata.TryGetValue("evidenceKind", out var kind) && kind == "coverage-no-match"))
                    {
                        lines.Add($"- Manifest coverage evidence: analysis `{Cell(report.Manifest.AnalysisLevel)}`, build `{Cell(report.Manifest.BuildStatus)}`, commit `{Cell(report.Manifest.CommitSha)}`.");
                        lines.Add("");
                    }

                    lines.Add("| Source | Fact type | Rule | Tier | Location | Target | Commit |");
                    lines.Add("| --- | --- | --- | --- | --- | --- | --- |");
                    foreach (var evidence in finding.Evidence)
                    {
                        lines.Add(
                            $"| `{Cell(evidence.SourceLabel ?? "default")}` | `{Cell(evidence.FactType)}` | `{Cell(evidence.RuleId)}` | `{Cell(evidence.EvidenceTier)}` | `{Cell(evidence.FilePath)}:{evidence.StartLine}-{evidence.EndLine}` | `{Cell(evidence.TargetSymbol ?? evidence.ContractElement ?? "unknown")}` | `{Cell(evidence.CommitSha)}` |");
                    }
                }

                if (finding.PathContext.Count > 0 || finding.ReverseContext.Count > 0)
                {
                    lines.Add("");
                    lines.Add("Context:");
                    foreach (var context in finding.PathContext.Concat(finding.ReverseContext))
                    {
                        lines.Add($"- `{Cell(context.Classification)}` ({Cell(context.RuleId)}): {Cell(context.Message)}");
                    }
                }

                lines.Add("");
            }
        }

        if (report.Gaps.Count > 0)
        {
            lines.Add("## Gaps");
            lines.Add("");
            lines.Add("| Gap | Rule | Tier | Classification | Message |");
            lines.Add("| --- | --- | --- | --- | --- |");
            foreach (var gap in report.Gaps)
            {
                lines.Add($"| `{Cell(gap.GapKind)}` | `{Cell(gap.RuleId)}` | `{Cell(gap.EvidenceTier)}` | `{Cell(gap.Classification)}` | {Cell(gap.Message)} |");
            }

            lines.Add("");
        }

        if (report.CoverageWarnings.Count > 0)
        {
            lines.Add("## Coverage Warnings");
            lines.Add("");
            foreach (var warning in report.CoverageWarnings)
            {
                lines.Add($"- {Cell(warning)}");
            }

            lines.Add("");
        }

        lines.Add("## Limitations");
        lines.Add("");
        foreach (var limitation in report.Limitations)
        {
            lines.Add($"- {Cell(limitation)}");
        }

        lines.Add("");
        return string.Join(Environment.NewLine, lines);
    }

    private static string Cell(string value)
    {
        return value
            .Replace("|", "\\|", StringComparison.Ordinal)
            .ReplaceLineEndings(" ")
            .Replace("[", "\\[", StringComparison.Ordinal)
            .Replace("]", "\\]", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal)
            .Replace("`", "\\`", StringComparison.Ordinal)
            .Replace("<", "\\<", StringComparison.Ordinal)
            .Replace(">", "\\>", StringComparison.Ordinal);
    }
}
