using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using TraceMap.Core;

namespace TraceMap.Reporting;

public sealed record LegacyEvidencePackCreateOptions(
    string InputPath,
    string InputKind,
    string Label,
    string Purpose,
    string ClaimLevel,
    string OutputPath,
    string? Date = null,
    bool DryRun = false);

public sealed record LegacyEvidencePackValidateOptions(
    string PackPath,
    string? ExpectedClaimLevel = null);

public sealed record LegacyEvidencePackPromoteOptions(
    string PackPath,
    string MarkdownPath,
    string OutputPath,
    bool Force = false,
    bool DryRun = false);

public sealed record LegacyEvidencePackCreateResult(
    LegacyEvidencePackDocument Pack,
    LegacyEvidencePackValidationResult Validation,
    string? JsonPath,
    string? MarkdownPath,
    string? ValidationPath);

public sealed record LegacyEvidencePackPromoteResult(
    LegacyEvidencePackValidationResult Validation,
    string? JsonPath,
    string? MarkdownPath);

public sealed record LegacyEvidencePackDocument(
    string SchemaVersion,
    string PackId,
    string GeneratedFor,
    string ClaimLevel,
    string Date,
    LegacyEvidencePackCommandProvenance CommandProvenance,
    IReadOnlyList<LegacyEvidencePackSource> Sources,
    LegacyEvidencePackSummary Summary,
    IReadOnlyList<LegacyEvidencePackSection> EvidenceSections,
    IReadOnlyList<LegacyEvidencePackGap> Gaps,
    IReadOnlyList<string> Limitations,
    LegacyEvidencePackSafety Safety);

public sealed record LegacyEvidencePackCommandProvenance(
    string PackGeneratorVersion,
    string NormalizedCommand,
    string InputKind,
    string InputFingerprint,
    IReadOnlyList<string> ValidationCommands);

public sealed record LegacyEvidencePackSource(
    string Label,
    string SourceClassification,
    string CoverageLabel,
    bool CommitShaPresent,
    string? CommitSha,
    string? CommitShaHash,
    string? RepoIdentityHash,
    IReadOnlyDictionary<string, string> ExtractorVersions);

public sealed record LegacyEvidencePackSummary(
    string RuleId,
    string EvidenceTier,
    string SourceLabel,
    string CoverageLabel,
    int FactCount,
    int GapCount,
    IReadOnlyDictionary<string, int> ByRuleId,
    IReadOnlyDictionary<string, int> ByEvidenceTier,
    IReadOnlyDictionary<string, int> ByFactCategory,
    IReadOnlyList<string> Limitations);

public sealed record LegacyEvidencePackSection(
    string SectionId,
    string Title,
    string Status,
    string ClaimBoundary,
    IReadOnlyList<LegacyEvidencePackRow> Rows,
    IReadOnlyList<LegacyEvidencePackGap> Gaps,
    IReadOnlyList<string> Limitations);

public sealed record LegacyEvidencePackRow(
    string RuleId,
    string EvidenceTier,
    string SourceLabel,
    string CoverageLabel,
    string Category,
    int Count,
    string SafeProvenance,
    IReadOnlyList<string> Limitations);

public sealed record LegacyEvidencePackGap(
    string RuleId,
    string EvidenceTier,
    string SourceLabel,
    string CoverageLabel,
    string Category,
    string Message);

public sealed record LegacyEvidencePackSafety(
    string ValidatorVersion,
    string Classification,
    IReadOnlyList<string> RejectedCategories,
    IReadOnlyList<string> Limitations);

public sealed record LegacyEvidencePackValidationResult(
    string SchemaVersion,
    string ValidatorVersion,
    string Classification,
    bool IsValid,
    IReadOnlyList<LegacyEvidencePackValidationDiagnostic> Diagnostics);

public sealed record LegacyEvidencePackValidationDiagnostic(
    string Category,
    string Path,
    string RuleId,
    string Message);

public static class LegacyEvidencePackClaimLevels
{
    public const string LocalOnly = "local-only";
    public const string DemoSafe = "demo-safe";
    public const string PublicSafe = "public-safe";
}

public static class LegacyEvidencePackSchemas
{
    public const string Pack = "legacy-evidence-pack.v1";
    public const string Validation = "legacy-evidence-pack-validation.v1";
    public const string GeneratorVersion = "legacy-evidence-pack.v1";
    public const string ValidatorVersion = "legacy-evidence-pack-safety.v1";
}

public static class LegacyEvidencePackRuleIds
{
    public const string Summary = "legacy.evidence-pack.summary.v1";
    public const string Section = "legacy.evidence-pack.section.v1";
    public const string ClaimBoundary = "legacy.evidence-pack.claim-boundary.v1";
    public const string SafetyValidation = "legacy.evidence-pack.safety-validation.v1";
    public const string CommandProvenance = "legacy.evidence-pack.command-provenance.v1";
    public const string InputAvailability = "legacy.evidence-pack.input-availability.v1";
}

public enum GitIgnoreStatus
{
    Ignored,
    NotIgnored,
    Unknown
}

public static class LegacyEvidencePacks
{
    public const string PackJsonFileName = "evidence-pack.json";
    public const string PackMarkdownFileName = "evidence-pack.md";
    public const string ValidationJsonFileName = "validation-result.json";
    public const string ApprovedPromotionRoot = "docs/evidence-packs/legacy";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly Regex NeutralSlug = new("^[a-z0-9][a-z0-9-]{2,63}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex DatePeriod = new("^\\d{4}-\\d{2}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex CommitSha = new("^[a-f0-9]{40}$", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex UriScheme = new("[a-z][a-z0-9+.-]*://", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex WindowsDrive = new(@"(^|[^a-zA-Z])[a-zA-Z]:[\\/]", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex AbsoluteUnixPath = new(@"(^|[\s""'`=:(])/(Users|home|var|tmp|private|opt)/[^\s""'`<>]+", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex SecretLike = new("(password|passwd|pwd|secret|token|credential|connectionstring|api[_-]?key|private key|bearer\\s+)", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex RawSql = new("\\b(select|insert|update|delete|merge)\\b[\\s\\S]{0,80}\\b(from|into|set|where)\\b", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex SourceLike = new(@"\b(public|private|protected|internal)\s+(sealed\s+|static\s+|partial\s+|async\s+)*(class|struct|interface|enum|record|void|string|int|bool|decimal|double|var)\b|\b(namespace|using)\s+[A-Za-z_][A-Za-z0-9_.]*", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex RawHostname = new(@"(^|[^a-z0-9-])([a-z0-9-]+\.)+(com|net|org|io|dev|local|internal)([^a-z0-9-]|$)", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly string[] ProhibitedPublicClaimTokens =
    [
        "ai impact analysis",
        "llm analysis",
        "embedding",
        "vector database",
        "prompt-based classification",
        "proves runtime",
        "runtime proof",
        "production usage",
        "vulnerability scan",
        "release approval"
    ];

    public static async Task<LegacyEvidencePackCreateResult> CreateAsync(LegacyEvidencePackCreateOptions options, CancellationToken cancellationToken = default)
    {
        ValidateCreateOptions(options);
        var repoRoot = FindRepoRoot(Directory.GetCurrentDirectory());
        var inputPath = ResolvePath(options.InputPath, repoRoot);
        var outputPath = ResolvePath(options.OutputPath, repoRoot);
        var date = NormalizeDate(options.Date, options.ClaimLevel);
        var input = await ReadInputAsync(inputPath, options.InputKind, options.Label, cancellationToken);
        var inputFingerprint = FingerprintInput(options.InputKind, inputPath);
        var packId = BuildPackId(options.Label, options.Purpose, options.ClaimLevel, date, inputFingerprint);

        var gaps = input.Gaps.ToList();
        var storageDiagnostics = new List<LegacyEvidencePackValidationDiagnostic>();
        if (!IsAllowedInput(inputPath, repoRoot, options.InputKind))
        {
            gaps.Add(Gap(options.Label, "input-storage", "Input is outside approved sample or ignored local storage."));
            storageDiagnostics.Add(Diagnostic("input-storage", options.InputPath, "input is outside approved sample or ignored local storage"));
        }

        if (!IsAllowedCreateOutput(outputPath, repoRoot, options.ClaimLevel))
        {
            gaps.Add(Gap(options.Label, "output-storage", "Output path is outside approved evidence-pack storage."));
            storageDiagnostics.Add(Diagnostic("output-storage", options.OutputPath, "output path is outside approved evidence-pack storage"));
        }

        var limitations = new SortedSet<string>(input.Limitations, StringComparer.Ordinal)
        {
            "Evidence packs summarize deterministic static evidence only.",
            "Evidence packs do not copy raw scan artifacts, facts, snippets, SQL, config values, remotes, or local paths.",
            "No public conclusion should be drawn without the rule IDs, evidence tiers, coverage labels, gaps, and limitations preserved in this pack."
        };

        var pack = new LegacyEvidencePackDocument(
            LegacyEvidencePackSchemas.Pack,
            packId,
            options.Purpose,
            options.ClaimLevel,
            date,
            new LegacyEvidencePackCommandProvenance(
                LegacyEvidencePackSchemas.GeneratorVersion,
                "tracemap evidence-pack create",
                options.InputKind,
                inputFingerprint,
                [
                    "tracemap evidence-pack validate",
                    "scripts/check-private-paths.sh"
                ]),
            input.Sources,
            input.Summary,
            input.Sections,
            gaps.OrderBy(item => item.Category, StringComparer.Ordinal).ThenBy(item => item.Message, StringComparer.Ordinal).ToArray(),
            limitations.ToArray(),
            new LegacyEvidencePackSafety(
                LegacyEvidencePackSchemas.ValidatorVersion,
                options.ClaimLevel,
                [],
                [
                    "Safety validation is category-based and does not authorize publishing raw local artifacts.",
                    "Rejected diagnostics intentionally avoid echoing unsafe values."
                ]));

        var validation = MergeValidation(ValidateDocument(pack, options.ClaimLevel, "generated-pack"), storageDiagnostics);
        if (!validation.IsValid)
        {
            pack = pack with
            {
                Safety = pack.Safety with
                {
                    Classification = "rejected",
                    RejectedCategories = validation.Diagnostics.Select(item => item.Category).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray()
                }
            };
            validation = MergeValidation(ValidateDocument(pack, options.ClaimLevel, "generated-pack"), storageDiagnostics);
        }

        if (options.DryRun)
        {
            return new LegacyEvidencePackCreateResult(pack, validation, null, null, null);
        }

        if (!validation.IsValid)
        {
            return new LegacyEvidencePackCreateResult(pack, validation, null, null, null);
        }

        Directory.CreateDirectory(outputPath);
        var jsonPath = Path.Combine(outputPath, PackJsonFileName);
        var markdownPath = Path.Combine(outputPath, PackMarkdownFileName);
        var validationPath = Path.Combine(outputPath, ValidationJsonFileName);
        await WriteJsonAsync(jsonPath, pack, cancellationToken);
        await File.WriteAllTextAsync(markdownPath, RenderMarkdown(pack), cancellationToken);
        var writtenValidation = ValidateGeneratedFiles(jsonPath, markdownPath, options.ClaimLevel);
        if (!writtenValidation.IsValid)
        {
            File.Delete(jsonPath);
            File.Delete(markdownPath);
            return new LegacyEvidencePackCreateResult(pack, writtenValidation, null, null, null);
        }

        await WriteJsonAsync(validationPath, writtenValidation, cancellationToken);
        return new LegacyEvidencePackCreateResult(pack, writtenValidation, jsonPath, markdownPath, validationPath);
    }

    public static async Task<LegacyEvidencePackValidationResult> ValidateAsync(LegacyEvidencePackValidateOptions options, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.PackPath))
        {
            throw new ArgumentException("evidence-pack validate requires --pack <evidence-pack.json>.", nameof(options));
        }

        var repoRoot = FindRepoRoot(Directory.GetCurrentDirectory());
        var packPath = ResolvePath(options.PackPath, repoRoot);
        var pack = await ReadJsonAsync<LegacyEvidencePackDocument>(packPath, cancellationToken);
        var markdownPath = Path.Combine(Path.GetDirectoryName(packPath)!, PackMarkdownFileName);
        return File.Exists(markdownPath)
            ? ValidateGeneratedFiles(packPath, markdownPath, options.ExpectedClaimLevel)
            : ValidateDocument(pack, options.ExpectedClaimLevel, options.PackPath);
    }

    public static async Task<LegacyEvidencePackPromoteResult> PromoteAsync(LegacyEvidencePackPromoteOptions options, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.PackPath) || string.IsNullOrWhiteSpace(options.MarkdownPath) || string.IsNullOrWhiteSpace(options.OutputPath))
        {
            throw new ArgumentException("evidence-pack promote requires --pack, --markdown, and --out.");
        }

        var repoRoot = FindRepoRoot(Directory.GetCurrentDirectory());
        var packPath = ResolvePath(options.PackPath, repoRoot);
        var markdownPath = ResolvePath(options.MarkdownPath, repoRoot);
        var outputPath = ResolvePath(options.OutputPath, repoRoot);
        if (!IsUnderRepoRelative(outputPath, repoRoot, ApprovedPromotionRoot, includeRoot: false))
        {
            throw new InvalidOperationException($"evidence-pack promote --out must be a child directory under {ApprovedPromotionRoot}/.");
        }

        var ignoreStatus = GitCheckIgnore(outputPath, repoRoot);
        if (ignoreStatus == GitIgnoreStatus.Ignored)
        {
            throw new InvalidOperationException("evidence-pack promote destination is ignored by git and cannot be a tracked promotion root.");
        }

        if (ignoreStatus == GitIgnoreStatus.Unknown)
        {
            throw new InvalidOperationException("evidence-pack promote could not verify whether the destination is ignored by git.");
        }

        if (Directory.Exists(outputPath) && !options.Force)
        {
            throw new IOException("evidence-pack promote destination already exists; use --force to replace it.");
        }

        var validation = ValidateGeneratedFiles(packPath, markdownPath, LegacyEvidencePackClaimLevels.PublicSafe);
        if (!validation.IsValid)
        {
            return new LegacyEvidencePackPromoteResult(validation, null, null);
        }

        if (options.DryRun)
        {
            return new LegacyEvidencePackPromoteResult(validation, null, null);
        }

        if (Directory.Exists(outputPath))
        {
            Directory.Delete(outputPath, recursive: true);
        }

        Directory.CreateDirectory(outputPath);
        var promotedJson = Path.Combine(outputPath, PackJsonFileName);
        var promotedMarkdown = Path.Combine(outputPath, PackMarkdownFileName);
        File.Copy(packPath, promotedJson);
        File.Copy(markdownPath, promotedMarkdown);
        return new LegacyEvidencePackPromoteResult(validation, promotedJson, promotedMarkdown);
    }

    public static string BuildPackId(string label, string purpose, string claimLevel, string date, string inputFingerprint)
    {
        ValidateNeutralToken(label, "label");
        ValidateNeutralToken(purpose, "purpose");
        ThrowIfUnknownClaimLevel(claimLevel);
        return $"{label}__{purpose}__{claimLevel}__{date}__{inputFingerprint[..Math.Min(12, inputFingerprint.Length)]}";
    }

    public static string RenderMarkdown(LegacyEvidencePackDocument pack)
    {
        var lines = new List<string>
        {
            "# Legacy Evidence Pack",
            "",
            $"Pack ID: `{Cell(pack.PackId)}`",
            $"Schema: `{Cell(pack.SchemaVersion)}`",
            $"Claim level: `{Cell(pack.ClaimLevel)}`",
            $"Date: `{Cell(pack.Date)}`",
            $"Purpose: `{Cell(pack.GeneratedFor)}`",
            "",
            "This pack summarizes deterministic static evidence only. It does not prove runtime behavior.",
            "",
            "## Summary",
            "",
            "| Metric | Value | Rule | Tier | Coverage |",
            "| --- | ---: | --- | --- | --- |",
            $"| Facts | `{pack.Summary.FactCount}` | `{Cell(pack.Summary.RuleId)}` | `{Cell(pack.Summary.EvidenceTier)}` | `{Cell(pack.Summary.CoverageLabel)}` |",
            $"| Gaps | `{pack.Summary.GapCount}` | `{Cell(pack.Summary.RuleId)}` | `{Cell(pack.Summary.EvidenceTier)}` | `{Cell(pack.Summary.CoverageLabel)}` |",
            "",
            "## Sources",
            "",
            "| Label | Classification | Coverage | Commit proof |",
            "| --- | --- | --- | --- |"
        };

        foreach (var source in pack.Sources.OrderBy(item => item.Label, StringComparer.Ordinal))
        {
            lines.Add($"| `{Cell(source.Label)}` | `{Cell(source.SourceClassification)}` | `{Cell(source.CoverageLabel)}` | `{(source.CommitShaPresent ? "present-hashed" : "missing")}` |");
        }

        foreach (var section in pack.EvidenceSections.OrderBy(item => item.SectionId, StringComparer.Ordinal))
        {
            lines.Add("");
            lines.Add($"## {Cell(section.Title)}");
            lines.Add("");
            lines.Add($"Status: `{Cell(section.Status)}`");
            lines.Add($"Claim boundary: `{Cell(section.ClaimBoundary)}`");
            lines.Add("");
            lines.Add("| Category | Count | Rule | Tier | Source | Coverage | Provenance |");
            lines.Add("| --- | ---: | --- | --- | --- | --- | --- |");
            foreach (var row in section.Rows.OrderBy(item => item.Category, StringComparer.Ordinal).ThenBy(item => item.RuleId, StringComparer.Ordinal))
            {
                lines.Add($"| `{Cell(row.Category)}` | `{row.Count}` | `{Cell(row.RuleId)}` | `{Cell(row.EvidenceTier)}` | `{Cell(row.SourceLabel)}` | `{Cell(row.CoverageLabel)}` | `{Cell(row.SafeProvenance)}` |");
            }

            if (section.Gaps.Count > 0)
            {
                lines.Add("");
                lines.Add("Gaps:");
                foreach (var gap in section.Gaps.OrderBy(item => item.Category, StringComparer.Ordinal))
                {
                    lines.Add($"- `{Cell(gap.Category)}` ({Cell(gap.RuleId)}, {Cell(gap.EvidenceTier)}): {Cell(gap.Message)}");
                }
            }

            lines.Add("");
            lines.Add("Limitations:");
            foreach (var limitation in section.Limitations.Order(StringComparer.Ordinal))
            {
                lines.Add($"- {Cell(limitation)}");
            }
        }

        if (pack.Gaps.Count > 0)
        {
            lines.Add("");
            lines.Add("## Pack Gaps");
            lines.Add("");
            foreach (var gap in pack.Gaps.OrderBy(item => item.Category, StringComparer.Ordinal))
            {
                lines.Add($"- `{Cell(gap.Category)}` ({Cell(gap.RuleId)}, {Cell(gap.EvidenceTier)}): {Cell(gap.Message)}");
            }
        }

        lines.Add("");
        lines.Add("## Limitations");
        lines.Add("");
        foreach (var limitation in pack.Limitations.Order(StringComparer.Ordinal))
        {
            lines.Add($"- {Cell(limitation)}");
        }

        lines.Add("");
        return string.Join('\n', lines);
    }

    private static async Task<LegacyEvidencePackInput> ReadInputAsync(string inputPath, string inputKind, string label, CancellationToken cancellationToken)
    {
        return inputKind switch
        {
            "legacy-validation-summary" or "public-demo-summary" => await ReadSummaryInputAsync(inputPath, label, cancellationToken),
            "legacy-baseline" => await ReadBaselineInputAsync(inputPath, label, cancellationToken),
            "scan-output" => await ReadScanOutputInputAsync(inputPath, label, cancellationToken),
            _ => new LegacyEvidencePackInput(
                [DefaultSource(label, "unknown", "unsupported-input")],
                EmptySummary(label, "unsupported-input"),
                [],
                [Gap(label, "unsupported-input-kind", $"Input kind `{inputKind}` is not supported by this evidence-pack generator.")],
                ["Unsupported input kind is preserved as an input availability gap, not absence evidence."])
        };
    }

    private static async Task<LegacyEvidencePackInput> ReadSummaryInputAsync(string inputPath, string label, CancellationToken cancellationToken)
    {
        var summaryPath = Directory.Exists(inputPath) ? Path.Combine(inputPath, "legacy-validation-summary.json") : inputPath;
        if (!File.Exists(summaryPath))
        {
            return MissingInput(label, "legacy-validation-summary");
        }

        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(summaryPath, cancellationToken));
        var root = document.RootElement;
        var source = ReadSource(root, label);
        var factCount = ReadInt(root, "factCount", 0);
        var gapCount = ReadInt(root, "gapCount", 0);
        var byRule = ReadIntMap(root, "byRuleId");
        var byTier = ReadIntMap(root, "byEvidenceTier");
        var byCategory = ReadIntMap(root, "byFactCategory");
        var rows = ReadRows(root, source.Label, source.CoverageLabel, byRule, byTier, byCategory);
        var gaps = ReadGaps(root, source.Label, source.CoverageLabel);
        var limitations = ReadStringArray(root, "limitations");
        if (limitations.Count == 0)
        {
            limitations = ["Summary input is already redacted and does not include raw evidence rows."];
        }

        return new LegacyEvidencePackInput(
            [source],
            new LegacyEvidencePackSummary(
                LegacyEvidencePackRuleIds.Summary,
                SummaryTier(byTier),
                source.Label,
                source.CoverageLabel,
                factCount,
                gapCount,
                byRule,
                byTier,
                byCategory,
                limitations),
            BuildSections(source.Label, source.CoverageLabel, rows, gaps, limitations),
            gaps,
            limitations);
    }

    private static async Task<LegacyEvidencePackInput> ReadBaselineInputAsync(string inputPath, string label, CancellationToken cancellationToken)
    {
        var manifestPath = Directory.Exists(inputPath) ? Path.Combine(inputPath, LegacyBaselineArtifacts.ManifestFileName) : inputPath;
        if (!File.Exists(manifestPath))
        {
            return MissingInput(label, "legacy-baseline");
        }

        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(manifestPath, cancellationToken));
        var root = document.RootElement;
        var sample = TryGetProperty(root, "sample", out var sampleElement) ? sampleElement : default;
        var scan = TryGetProperty(root, "scan", out var scanElement) ? scanElement : default;
        var counts = TryGetProperty(root, "counts", out var countsElement) ? countsElement : default;
        var sourceLabel = ReadString(sample, "label", label);
        ValidateNeutralToken(sourceLabel, "source label");
        var coverage = ReadString(scan, "coverageLabel", "unknown");
        var source = new LegacyEvidencePackSource(
            sourceLabel,
            "baseline-summary",
            coverage,
            CommitPresent(sample),
            null,
            CommitHash(sample),
            ReadStringOrNull(sample, "repoIdentityHash"),
            ReadExtractorVersions(root));
        var byRule = ReadIntMap(counts, "byRuleId");
        var byTier = ReadIntMap(counts, "byEvidenceTier");
        var byCategory = ReadIntMap(counts, "byFactType");
        var limitations = ReadStringArray(root, "limitations");
        var gapCount = ReadInt(counts, "gapsTotal", 0);
        var rows = RowsFromCounts(source.Label, coverage, byRule, byTier, byCategory);
        var gaps = ReadStringArray(root, "knownGaps")
            .Select(item => Gap(source.Label, item, "Known baseline gap preserved from redacted manifest.", coverage))
            .ToArray();

        return new LegacyEvidencePackInput(
            [source],
            new LegacyEvidencePackSummary(
                LegacyEvidencePackRuleIds.Summary,
                SummaryTier(byTier),
                source.Label,
                coverage,
                ReadInt(counts, "factsTotal", 0),
                gapCount,
                byRule,
                byTier,
                byCategory,
                limitations),
            BuildSections(source.Label, coverage, rows, gaps, limitations),
            gaps,
            limitations);
    }

    private static async Task<LegacyEvidencePackInput> ReadScanOutputInputAsync(string inputPath, string label, CancellationToken cancellationToken)
    {
        var manifestPath = Path.Combine(inputPath, "scan-manifest.json");
        if (!File.Exists(manifestPath))
        {
            return MissingInput(label, "scan-output");
        }

        using var manifestDocument = JsonDocument.Parse(await File.ReadAllTextAsync(manifestPath, cancellationToken));
        var manifest = manifestDocument.RootElement;
        var facts = await ReadFactsAsync(Path.Combine(inputPath, "facts.ndjson"), cancellationToken);
        var byRule = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var byTier = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var byCategory = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var fact in facts)
        {
            Increment(byRule, ReadString(fact, "ruleId", "unknown"));
            Increment(byTier, ReadString(fact, "evidenceTier", EvidenceTiers.Tier4Unknown));
            Increment(byCategory, ReadString(fact, "factType", "unknown"));
        }

        var source = new LegacyEvidencePackSource(
            label,
            "scan-output-summary",
            ReadString(manifest, "analysisLevel", "unknown"),
            !string.IsNullOrWhiteSpace(ReadString(manifest, "commitSha", "")),
            RawPublicCommitSha("scan-output-summary", ReadString(manifest, "commitSha", "")),
            HashOrNull("commit", label, ReadString(manifest, "commitSha", "")),
            null,
            new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["tracemap"] = ReadString(manifest, "scannerVersion", "unknown")
            });
        var gaps = ReadStringArray(manifest, "knownGaps")
            .Select(item => Gap(label, item, "Known scan gap preserved from scan manifest.", source.CoverageLabel))
            .ToArray();
        var limitations = new List<string>
        {
            "Raw scan-output inputs are summarized by counts only.",
            "Raw facts, local paths, remotes, snippets, SQL, and config values are not copied."
        };
        if (!File.Exists(Path.Combine(inputPath, "facts.ndjson")))
        {
            limitations.Add("facts.ndjson was missing; count evidence is partial.");
        }

        var rows = RowsFromCounts(label, source.CoverageLabel, byRule, byTier, byCategory);
        return new LegacyEvidencePackInput(
            [source],
            new LegacyEvidencePackSummary(
                LegacyEvidencePackRuleIds.Summary,
                SummaryTier(byTier),
                label,
                source.CoverageLabel,
                facts.Count,
                gaps.Length + byTier.GetValueOrDefault(EvidenceTiers.Tier4Unknown),
                byRule,
                byTier,
                byCategory,
                limitations),
            BuildSections(label, source.CoverageLabel, rows, gaps, limitations),
            gaps,
            limitations);
    }

    private static IReadOnlyList<LegacyEvidencePackSection> BuildSections(
        string sourceLabel,
        string coverageLabel,
        IReadOnlyList<LegacyEvidencePackRow> rows,
        IReadOnlyList<LegacyEvidencePackGap> gaps,
        IReadOnlyList<string> limitations)
    {
        var sections = new List<LegacyEvidencePackSection>();
        foreach (var group in rows.GroupBy(row => SectionFor(row.Category)).OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            sections.Add(new LegacyEvidencePackSection(
                group.Key,
                TitleFor(group.Key),
                group.Any(row => row.Count > 0) ? "available" : "not-observed",
                "static-evidence-only",
                group.OrderBy(row => row.Category, StringComparer.Ordinal).ThenBy(row => row.RuleId, StringComparer.Ordinal).ToArray(),
                gaps.Where(gap => SectionFor(gap.Category) == group.Key).OrderBy(gap => gap.Category, StringComparer.Ordinal).ToArray(),
                limitations.Count == 0
                    ? ["Section rows summarize static evidence counts only."]
                    : limitations));
        }

        if (sections.Count == 0)
        {
            sections.Add(new LegacyEvidencePackSection(
                "input-availability",
                "Input Availability",
                "gap",
                "static-evidence-only",
                [
                    new LegacyEvidencePackRow(
                        LegacyEvidencePackRuleIds.InputAvailability,
                        EvidenceTiers.Tier4Unknown,
                        sourceLabel,
                        coverageLabel,
                        "no-rows",
                        0,
                        "input-summary",
                        ["No count rows were available from the input."])
                ],
                gaps,
                ["Missing rows are input availability gaps, not proof of absence."]));
        }

        return sections;
    }

    private static IReadOnlyList<LegacyEvidencePackRow> ReadRows(
        JsonElement root,
        string sourceLabel,
        string coverageLabel,
        IReadOnlyDictionary<string, int> byRule,
        IReadOnlyDictionary<string, int> byTier,
        IReadOnlyDictionary<string, int> byCategory)
    {
        if (TryGetProperty(root, "evidenceRows", out var rowsElement) && rowsElement.ValueKind == JsonValueKind.Array)
        {
            var rows = new List<LegacyEvidencePackRow>();
            foreach (var item in rowsElement.EnumerateArray())
            {
                rows.Add(new LegacyEvidencePackRow(
                    ReadString(item, "ruleId", LegacyEvidencePackRuleIds.Section),
                    ReadString(item, "evidenceTier", EvidenceTiers.Tier4Unknown),
                    ReadString(item, "sourceLabel", sourceLabel),
                    ReadString(item, "coverageLabel", coverageLabel),
                    ReadString(item, "category", "unknown"),
                    ReadInt(item, "count", 0),
                    ReadString(item, "safeProvenance", "redacted-summary"),
                    ReadStringArray(item, "limitations")));
            }

            return rows.OrderBy(item => item.Category, StringComparer.Ordinal).ThenBy(item => item.RuleId, StringComparer.Ordinal).ToArray();
        }

        return RowsFromCounts(sourceLabel, coverageLabel, byRule, byTier, byCategory);
    }

    private static IReadOnlyList<LegacyEvidencePackRow> RowsFromCounts(
        string sourceLabel,
        string coverageLabel,
        IReadOnlyDictionary<string, int> byRule,
        IReadOnlyDictionary<string, int> byTier,
        IReadOnlyDictionary<string, int> byCategory)
    {
        var rows = new List<LegacyEvidencePackRow>();
        foreach (var (ruleId, count) in byRule.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            rows.Add(new LegacyEvidencePackRow(
                ruleId == "unknown" ? LegacyEvidencePackRuleIds.Section : ruleId,
                EvidenceTiers.Tier4Unknown,
                sourceLabel,
                coverageLabel,
                $"rule:{ruleId}",
                count,
                "count-by-rule-id",
                RuleLimitations(ruleId)));
        }

        foreach (var (tier, count) in byTier.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            rows.Add(new LegacyEvidencePackRow(
                LegacyEvidencePackRuleIds.Section,
                tier,
                sourceLabel,
                coverageLabel,
                $"tier:{tier}",
                count,
                "count-by-evidence-tier",
                ["Tier count is aggregate evidence and does not identify individual source rows."]));
        }

        foreach (var (category, count) in byCategory.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            rows.Add(new LegacyEvidencePackRow(
                LegacyEvidencePackRuleIds.Section,
                EvidenceTiers.Tier4Unknown,
                sourceLabel,
                coverageLabel,
                $"category:{category}",
                count,
                "count-by-fact-category",
                ["Fact category count is aggregate evidence and does not prove runtime behavior."]));
        }

        return rows;
    }

    private static IReadOnlyList<LegacyEvidencePackGap> ReadGaps(JsonElement root, string sourceLabel, string coverageLabel)
    {
        if (!TryGetProperty(root, "gaps", out var gapsElement) || gapsElement.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var gaps = new List<LegacyEvidencePackGap>();
        foreach (var item in gapsElement.EnumerateArray())
        {
            gaps.Add(new LegacyEvidencePackGap(
                ReadString(item, "ruleId", LegacyEvidencePackRuleIds.InputAvailability),
                ReadString(item, "evidenceTier", EvidenceTiers.Tier4Unknown),
                ReadString(item, "sourceLabel", sourceLabel),
                ReadString(item, "coverageLabel", coverageLabel),
                ReadString(item, "category", "input-gap"),
                ReadString(item, "message", "Input gap preserved without unsafe detail.")));
        }

        return gaps.OrderBy(item => item.Category, StringComparer.Ordinal).ToArray();
    }

    private static LegacyEvidencePackValidationResult MergeValidation(
        LegacyEvidencePackValidationResult validation,
        IReadOnlyList<LegacyEvidencePackValidationDiagnostic> diagnostics)
    {
        if (diagnostics.Count == 0)
        {
            return validation;
        }

        var merged = validation.Diagnostics
            .Concat(diagnostics)
            .GroupBy(item => (item.Category, item.Path, item.Message))
            .Select(group => group.First())
            .OrderBy(item => item.Category, StringComparer.Ordinal)
            .ThenBy(item => item.Path, StringComparer.Ordinal)
            .ToArray();
        return validation with
        {
            Classification = "rejected",
            IsValid = false,
            Diagnostics = merged
        };
    }

    private static LegacyEvidencePackSource ReadSource(JsonElement root, string label)
    {
        var sourceElement = TryGetProperty(root, "source", out var source) ? source : default;
        var sourceLabel = ReadString(sourceElement, "label", label);
        ValidateNeutralToken(sourceLabel, "source label");
        var commit = ReadString(sourceElement, "commitSha", "");
        var repoIdentity = ReadString(sourceElement, "repoIdentity", "");
        return new LegacyEvidencePackSource(
            sourceLabel,
            ReadString(sourceElement, "sourceClassification", "redacted-summary"),
            ReadString(sourceElement, "coverageLabel", ReadString(root, "coverageLabel", "unknown")),
            !string.IsNullOrWhiteSpace(commit),
            RawPublicCommitSha(ReadString(sourceElement, "sourceClassification", "redacted-summary"), commit),
            HashOrNull("commit", sourceLabel, commit),
            HashOrNull("repo", sourceLabel, repoIdentity),
            ReadStringMap(sourceElement, "extractorVersions"));
    }

    private static string? RawPublicCommitSha(string sourceClassification, string commit)
    {
        return IsPublicSourceClassification(sourceClassification) && CommitSha.IsMatch(commit)
            ? commit.ToLowerInvariant()
            : null;
    }

    private static bool IsPublicSourceClassification(string sourceClassification)
    {
        return sourceClassification is "public-repo" or "public-archive";
    }

    private static LegacyEvidencePackValidationResult ValidateGeneratedFiles(string jsonPath, string markdownPath, string? expectedClaimLevel)
    {
        var diagnostics = new List<LegacyEvidencePackValidationDiagnostic>();
        LegacyEvidencePackDocument? pack = null;
        try
        {
            pack = JsonSerializer.Deserialize<LegacyEvidencePackDocument>(File.ReadAllText(jsonPath), JsonOptions);
        }
        catch (JsonException)
        {
            diagnostics.Add(Diagnostic("invalid-json", jsonPath, "evidence-pack JSON could not be parsed"));
        }

        var markdownText = File.ReadAllText(markdownPath);
        if (pack is not null)
        {
            diagnostics.AddRange(ValidateDocument(pack, expectedClaimLevel, jsonPath).Diagnostics);
            if (!string.Equals(markdownText, RenderMarkdown(pack), StringComparison.Ordinal))
            {
                diagnostics.Add(Diagnostic("markdown-mismatch", markdownPath, "evidence-pack Markdown does not match the pack JSON"));
            }
        }

        diagnostics.AddRange(ScanUnsafeText(markdownText, markdownPath, expectedClaimLevel ?? pack?.ClaimLevel));
        diagnostics = diagnostics
            .GroupBy(item => (item.Category, item.Path, item.Message))
            .Select(group => group.First())
            .OrderBy(item => item.Category, StringComparer.Ordinal)
            .ThenBy(item => item.Path, StringComparer.Ordinal)
            .ToList();
        return new LegacyEvidencePackValidationResult(
            LegacyEvidencePackSchemas.Validation,
            LegacyEvidencePackSchemas.ValidatorVersion,
            diagnostics.Count == 0 ? pack?.ClaimLevel ?? "unknown" : "rejected",
            diagnostics.Count == 0,
            diagnostics);
    }

    private static LegacyEvidencePackValidationResult ValidateDocument(LegacyEvidencePackDocument pack, string? expectedClaimLevel, string path)
    {
        var diagnostics = new List<LegacyEvidencePackValidationDiagnostic>();
        if (pack.SchemaVersion != LegacyEvidencePackSchemas.Pack)
        {
            diagnostics.Add(Diagnostic("schema-version", path, "evidence-pack schema version is not supported"));
        }

        if (expectedClaimLevel is not null && !pack.ClaimLevel.Equals(expectedClaimLevel, StringComparison.Ordinal))
        {
            diagnostics.Add(Diagnostic("claim-level-mismatch", path, "evidence-pack claim level does not match expected claim level"));
        }

        if (!IsKnownClaimLevel(pack.ClaimLevel))
        {
            diagnostics.Add(Diagnostic("claim-level-invalid", path, "evidence-pack claim level must be local-only, demo-safe, or public-safe"));
        }
        else if (pack.ClaimLevel is LegacyEvidencePackClaimLevels.PublicSafe or LegacyEvidencePackClaimLevels.DemoSafe)
        {
            if (!DatePeriod.IsMatch(pack.Date))
            {
                diagnostics.Add(Diagnostic("date-period", path, "demo-safe and public-safe packs require fixture-pinned yyyy-MM dates"));
            }
        }

        if (pack.Summary.RuleId != LegacyEvidencePackRuleIds.Summary)
        {
            diagnostics.Add(Diagnostic("summary-rule", path, "summary must cite legacy.evidence-pack.summary.v1"));
        }

        foreach (var section in pack.EvidenceSections)
        {
            foreach (var row in section.Rows)
            {
                if (string.IsNullOrWhiteSpace(row.RuleId) || string.IsNullOrWhiteSpace(row.EvidenceTier) || string.IsNullOrWhiteSpace(row.SourceLabel) || string.IsNullOrWhiteSpace(row.CoverageLabel) || string.IsNullOrWhiteSpace(row.SafeProvenance) || row.Limitations.Count == 0)
                {
                    diagnostics.Add(Diagnostic("row-evidence-metadata", path, "every evidence row must include rule ID, evidence tier, source label, coverage label, provenance, and limitations"));
                    break;
                }
            }
        }

        diagnostics.AddRange(ValidateRuleCatalog(AllRuleIds(pack)));
        diagnostics.AddRange(ScanUnsafeText(JsonSerializer.Serialize(pack, JsonOptions), path, pack.ClaimLevel));
        diagnostics = diagnostics
            .GroupBy(item => (item.Category, item.Message))
            .Select(group => group.First())
            .OrderBy(item => item.Category, StringComparer.Ordinal)
            .ToList();
        return new LegacyEvidencePackValidationResult(
            LegacyEvidencePackSchemas.Validation,
            LegacyEvidencePackSchemas.ValidatorVersion,
            diagnostics.Count == 0 ? pack.ClaimLevel : "rejected",
            diagnostics.Count == 0,
            diagnostics);
    }

    private static IReadOnlyList<string> AllRuleIds(LegacyEvidencePackDocument pack)
    {
        return new[]
            {
                pack.Summary.RuleId,
                LegacyEvidencePackRuleIds.CommandProvenance,
                LegacyEvidencePackRuleIds.SafetyValidation
            }
            .Concat(pack.EvidenceSections.SelectMany(section => section.Rows.Select(row => row.RuleId)))
            .Concat(pack.EvidenceSections.SelectMany(section => section.Gaps.Select(gap => gap.RuleId)))
            .Concat(pack.Gaps.Select(gap => gap.RuleId))
            .Where(ruleId => !string.IsNullOrWhiteSpace(ruleId))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<LegacyEvidencePackValidationDiagnostic> ValidateRuleCatalog(IEnumerable<string> ruleIds)
    {
        var repoRoot = FindRepoRoot(Directory.GetCurrentDirectory());
        var catalogPath = Path.Combine(repoRoot, "rules", "rule-catalog.yml");
        if (!File.Exists(catalogPath))
        {
            return [Diagnostic("rule-catalog-missing", "rules/rule-catalog.yml", "rule catalog is unavailable")];
        }

        var text = File.ReadAllText(catalogPath);
        return ruleIds
            .Where(ruleId => ruleId != "unknown" && !RuleCatalogContains(text, ruleId))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .Select(ruleId => Diagnostic("rule-catalog-entry-missing", "rules/rule-catalog.yml", $"observed rule ID lacks catalog entry: {ruleId}"))
            .ToArray();
    }

    private static IReadOnlyList<LegacyEvidencePackValidationDiagnostic> ScanUnsafeText(string text, string path, string? claimLevel)
    {
        var diagnostics = new List<LegacyEvidencePackValidationDiagnostic>();
        AddIf(AbsoluteUnixPath.IsMatch(text), "absolute-path");
        AddIf(WindowsDrive.IsMatch(text), "absolute-path");
        AddIf(UriScheme.IsMatch(text), "raw-remote-or-url");
        AddIf(RawHostname.IsMatch(text), "raw-remote-or-url");
        AddIf(text.Contains(".git", StringComparison.OrdinalIgnoreCase), "raw-remote-or-url");
        AddIf(SecretLike.IsMatch(text), "secret-like-value");
        AddIf(RawSql.IsMatch(text), "raw-sql");
        AddIf(SourceLike.IsMatch(text), "source-like-snippet");
        AddIf(ContainsPrivatePathGuardToken(text), "private-path-guard");

        if (claimLevel is LegacyEvidencePackClaimLevels.PublicSafe or LegacyEvidencePackClaimLevels.DemoSafe)
        {
            foreach (var token in ProhibitedPublicClaimTokens)
            {
                AddIf(text.Contains(token, StringComparison.OrdinalIgnoreCase), "prohibited-public-claim");
            }
        }

        return diagnostics
            .GroupBy(item => item.Category)
            .Select(group => group.First())
            .OrderBy(item => item.Category, StringComparer.Ordinal)
            .ToArray();

        void AddIf(bool condition, string category)
        {
            if (condition)
            {
                diagnostics.Add(Diagnostic(category, path, "unsafe evidence-pack content category detected"));
            }
        }
    }

    private static bool ContainsPrivatePathGuardToken(string text)
    {
        foreach (var token in PrivatePathGuardTokens())
        {
            if (!string.IsNullOrWhiteSpace(token) && text.Contains(token, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static IReadOnlyList<string> PrivatePathGuardTokens()
    {
        var repoRoot = FindRepoRoot(Directory.GetCurrentDirectory());
        var guardPath = Path.Combine(repoRoot, "scripts", "check-private-paths.sh");
        if (!File.Exists(guardPath))
        {
            return [];
        }

        return Regex.Matches(File.ReadAllText(guardPath), @"\$'(?<bytes>(?:\\x[0-9a-fA-F]{2})+)'")
            .Select(match => DecodeShellHex(match.Groups["bytes"].Value))
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static string DecodeShellHex(string value)
    {
        var bytes = Regex.Matches(value, @"\\x(?<hex>[0-9a-fA-F]{2})")
            .Select(match => Convert.ToByte(match.Groups["hex"].Value, 16))
            .ToArray();
        return Encoding.UTF8.GetString(bytes);
    }

    private static void ValidateCreateOptions(LegacyEvidencePackCreateOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.InputPath))
        {
            throw new ArgumentException("evidence-pack create requires --input <path>.", nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.InputKind))
        {
            throw new ArgumentException("evidence-pack create requires --input-kind <kind>.", nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.OutputPath))
        {
            throw new ArgumentException("evidence-pack create requires --out <path>.", nameof(options));
        }

        ValidateNeutralToken(options.Label, "label");
        ValidateNeutralToken(options.Purpose, "purpose");
        ThrowIfUnknownClaimLevel(options.ClaimLevel);
    }

    private static void ThrowIfUnknownClaimLevel(string value)
    {
        if (!IsKnownClaimLevel(value))
        {
            throw new ArgumentException("evidence-pack claim level must be local-only, demo-safe, or public-safe.");
        }
    }

    private static bool IsKnownClaimLevel(string value)
    {
        return value is LegacyEvidencePackClaimLevels.LocalOnly or LegacyEvidencePackClaimLevels.DemoSafe or LegacyEvidencePackClaimLevels.PublicSafe;
    }

    private static void ValidateNeutralToken(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value) || !NeutralSlug.IsMatch(value)
            || value.Contains('/', StringComparison.Ordinal)
            || value.Contains('\\', StringComparison.Ordinal)
            || value.Contains('@', StringComparison.Ordinal)
            || value.EndsWith(".git", StringComparison.OrdinalIgnoreCase)
            || value.Contains('~', StringComparison.Ordinal)
            || UriScheme.IsMatch(value)
            || WindowsDrive.IsMatch(value)
            || RawHostname.IsMatch(value)
            || value.Count(ch => ch == '-') > 7)
        {
            throw new ArgumentException($"evidence-pack {name} must be a short neutral slug.", name);
        }
    }

    private static string NormalizeDate(string? value, string claimLevel)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            if (claimLevel is LegacyEvidencePackClaimLevels.PublicSafe or LegacyEvidencePackClaimLevels.DemoSafe)
            {
                throw new ArgumentException("demo-safe and public-safe evidence packs require --date <yyyy-MM>.");
            }

            return DateTimeOffset.UtcNow.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        }

        if (DatePeriod.IsMatch(value))
        {
            return value;
        }

        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var timestamp))
        {
            return timestamp.ToString("yyyy-MM", CultureInfo.InvariantCulture);
        }

        throw new ArgumentException("evidence-pack --date must be yyyy-MM or ISO timestamp.");
    }

    private static bool IsAllowedInput(string path, string repoRoot, string inputKind)
    {
        if (inputKind == "scan-output")
        {
            return IsUnderRepoRelative(path, repoRoot, ".tmp")
                || IsUnderRepoRelative(path, repoRoot, "samples")
                || GitCheckIgnore(path, repoRoot) == GitIgnoreStatus.Ignored;
        }

        return IsUnderRepoRelative(path, repoRoot, "samples")
            || IsUnderRepoRelative(path, repoRoot, ".tmp")
            || IsUnderRepoRelative(path, repoRoot, "docs/evidence-packs")
            || GitCheckIgnore(path, repoRoot) == GitIgnoreStatus.Ignored;
    }

    private static bool IsAllowedCreateOutput(string path, string repoRoot, string claimLevel)
    {
        if (claimLevel == LegacyEvidencePackClaimLevels.LocalOnly)
        {
            return IsUnderRepoRelative(path, repoRoot, ".tmp/legacy-evidence-packs") && GitCheckIgnore(path, repoRoot) == GitIgnoreStatus.Ignored;
        }

        return IsUnderRepoRelative(path, repoRoot, ".tmp/legacy-evidence-packs")
            || IsUnderRepoRelative(path, repoRoot, "samples/synthetic-legacy-evidence-pack/output");
    }

    private static bool IsUnderRepoRelative(string path, string repoRoot, string relativeRoot, bool includeRoot = true)
    {
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var target = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(repoRoot));
        var normalizedRelativeRoot = relativeRoot
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);
        var allowedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.Combine(root, normalizedRelativeRoot)));
        return target.Equals(allowedRoot, comparison)
            ? includeRoot
            : target.StartsWith(allowedRoot + Path.DirectorySeparatorChar, comparison);
    }

    private static GitIgnoreStatus GitCheckIgnore(string path, string repoRoot)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "git",
                ArgumentList = { "check-ignore", path },
                WorkingDirectory = repoRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });
            process?.WaitForExit(5000);
            return process?.ExitCode switch
            {
                0 => GitIgnoreStatus.Ignored,
                1 => GitIgnoreStatus.NotIgnored,
                _ => GitIgnoreStatus.Unknown
            };
        }
        catch
        {
            return GitIgnoreStatus.Unknown;
        }
    }

    private static string ResolvePath(string path, string repoRoot)
    {
        return Path.IsPathFullyQualified(path)
            ? Path.GetFullPath(path)
            : Path.GetFullPath(Path.Combine(repoRoot, path));
    }

    private static string FindRepoRoot(string start)
    {
        var directory = new DirectoryInfo(Path.GetFullPath(start));
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".git")) || File.Exists(Path.Combine(directory.FullName, ".git")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return Path.GetFullPath(start);
    }

    private static async Task<T> ReadJsonAsync<T>(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var value = await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken);
        return value ?? throw new InvalidOperationException($"Unable to read JSON file: {path}");
    }

    private static async Task WriteJsonAsync<T>(string path, T value, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, value, JsonOptions, cancellationToken);
        await stream.WriteAsync("\n"u8.ToArray(), cancellationToken);
    }

    private static async Task<IReadOnlyList<JsonElement>> ReadFactsAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return [];
        }

        var facts = new List<JsonElement>();
        await using var stream = File.OpenRead(path);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            using var document = JsonDocument.Parse(line);
            facts.Add(document.RootElement.Clone());
        }

        return facts;
    }

    private static string FingerprintInput(string inputKind, string inputPath)
    {
        var builder = new StringBuilder();
        AppendLengthPrefixed(builder, "input-kind", inputKind);
        if (File.Exists(inputPath))
        {
            AppendLengthPrefixed(builder, "file", Path.GetFileName(inputPath));
            AppendLengthPrefixed(builder, "sha", Sha256Hex(File.ReadAllBytes(inputPath)));
        }
        else if (Directory.Exists(inputPath))
        {
            foreach (var file in Directory.EnumerateFiles(inputPath, "*", SearchOption.TopDirectoryOnly).Order(StringComparer.Ordinal))
            {
                AppendLengthPrefixed(builder, "file", Path.GetFileName(file));
                AppendLengthPrefixed(builder, "sha", Sha256Hex(File.ReadAllBytes(file)));
            }
        }
        else
        {
            AppendLengthPrefixed(builder, "missing", "true");
        }

        return Sha256Hex(Encoding.UTF8.GetBytes(builder.ToString()));
    }

    private static string? HashOrNull(string field, string label, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var builder = new StringBuilder();
        AppendLengthPrefixed(builder, "legacy-evidence-pack", "");
        AppendLengthPrefixed(builder, "field", field);
        AppendLengthPrefixed(builder, "label", label);
        AppendLengthPrefixed(builder, "value", value);
        return "sha256:" + Sha256Hex(Encoding.UTF8.GetBytes(builder.ToString()));
    }

    private static string Sha256Hex(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static void AppendLengthPrefixed(StringBuilder builder, string key, string value)
    {
        builder.Append(key);
        builder.Append(':');
        builder.Append(value.Length.ToString(CultureInfo.InvariantCulture));
        builder.Append(':');
        builder.Append(value);
        builder.Append('\n');
    }

    private static string ReadString(JsonElement element, string propertyName, string defaultValue)
    {
        return TryGetProperty(element, propertyName, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!
            : defaultValue;
    }

    private static string? ReadStringOrNull(JsonElement element, string propertyName)
    {
        var value = ReadString(element, propertyName, "");
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static int ReadInt(JsonElement element, string propertyName, int defaultValue)
    {
        return TryGetProperty(element, propertyName, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var result)
            ? result
            : defaultValue;
    }

    private static IReadOnlyDictionary<string, int> ReadIntMap(JsonElement element, string propertyName)
    {
        var map = new SortedDictionary<string, int>(StringComparer.Ordinal);
        if (!TryGetProperty(element, propertyName, out var value) || value.ValueKind != JsonValueKind.Object)
        {
            return map;
        }

        foreach (var property in value.EnumerateObject().OrderBy(item => item.Name, StringComparer.Ordinal))
        {
            if (property.Value.ValueKind == JsonValueKind.Number && property.Value.TryGetInt32(out var count))
            {
                map[property.Name] = count;
            }
        }

        return map;
    }

    private static IReadOnlyDictionary<string, string> ReadStringMap(JsonElement element, string propertyName)
    {
        var map = new SortedDictionary<string, string>(StringComparer.Ordinal);
        if (!TryGetProperty(element, propertyName, out var value) || value.ValueKind != JsonValueKind.Object)
        {
            return map;
        }

        foreach (var property in value.EnumerateObject().OrderBy(item => item.Name, StringComparer.Ordinal))
        {
            if (property.Value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(property.Value.GetString()))
            {
                map[property.Name] = property.Value.GetString()!;
            }
        }

        return map;
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement element, string propertyName)
    {
        if (!TryGetProperty(element, propertyName, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return value.EnumerateArray()
            .Where(item => item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
            .Select(item => item.GetString()!)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(propertyName, out value))
        {
            return true;
        }

        value = default;
        return false;
    }

    private static IReadOnlyDictionary<string, string> ReadExtractorVersions(JsonElement root)
    {
        if (!TryGetProperty(root, "extractors", out var extractors) || extractors.ValueKind != JsonValueKind.Object)
        {
            return new SortedDictionary<string, string>(StringComparer.Ordinal);
        }

        var map = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in extractors.EnumerateObject().OrderBy(item => item.Name, StringComparer.Ordinal))
        {
            if (property.Value.ValueKind == JsonValueKind.Object)
            {
                map[property.Name] = ReadString(property.Value, "version", "unknown");
            }
        }

        return map;
    }

    private static bool CommitPresent(JsonElement sample)
    {
        return TryGetProperty(sample, "commitIdentity", out var commit)
            && commit.ValueKind == JsonValueKind.Object
            && !string.IsNullOrWhiteSpace(ReadString(commit, "value", ""));
    }

    private static string? CommitHash(JsonElement sample)
    {
        return TryGetProperty(sample, "commitIdentity", out var commit)
            && commit.ValueKind == JsonValueKind.Object
            ? HashOrNull("commit", ReadString(sample, "label", "sample"), ReadString(commit, "value", ""))
            : null;
    }

    private static string SummaryTier(IReadOnlyDictionary<string, int> tiers)
    {
        if (tiers.ContainsKey(EvidenceTiers.Tier4Unknown))
        {
            return EvidenceTiers.Tier4Unknown;
        }

        if (tiers.ContainsKey(EvidenceTiers.Tier3SyntaxOrTextual))
        {
            return EvidenceTiers.Tier3SyntaxOrTextual;
        }

        if (tiers.ContainsKey(EvidenceTiers.Tier2Structural))
        {
            return EvidenceTiers.Tier2Structural;
        }

        return tiers.ContainsKey(EvidenceTiers.Tier1Semantic) ? EvidenceTiers.Tier1Semantic : EvidenceTiers.Tier4Unknown;
    }

    private static LegacyEvidencePackSource DefaultSource(string label, string coverageLabel, string classification)
    {
        return new LegacyEvidencePackSource(label, classification, coverageLabel, false, null, null, null, new SortedDictionary<string, string>(StringComparer.Ordinal));
    }

    private static LegacyEvidencePackSummary EmptySummary(string label, string coverageLabel)
    {
        return new LegacyEvidencePackSummary(
            LegacyEvidencePackRuleIds.Summary,
            EvidenceTiers.Tier4Unknown,
            label,
            coverageLabel,
            0,
            1,
            new SortedDictionary<string, int>(StringComparer.Ordinal),
            new SortedDictionary<string, int>(StringComparer.Ordinal),
            new SortedDictionary<string, int>(StringComparer.Ordinal),
            ["No input evidence was available."]);
    }

    private static LegacyEvidencePackInput MissingInput(string label, string inputKind)
    {
        return new LegacyEvidencePackInput(
            [DefaultSource(label, "unknown", "missing-input")],
            EmptySummary(label, "unknown"),
            [],
            [Gap(label, "missing-input", $"No {inputKind} input was available.")],
            ["Missing input is preserved as an input availability gap, not absence evidence."]);
    }

    private static LegacyEvidencePackGap Gap(string sourceLabel, string category, string message, string coverageLabel = "unknown")
    {
        return new LegacyEvidencePackGap(
            LegacyEvidencePackRuleIds.InputAvailability,
            EvidenceTiers.Tier4Unknown,
            sourceLabel,
            coverageLabel,
            category,
            message);
    }

    private static string SectionFor(string category)
    {
        if (category.Contains("webforms", StringComparison.OrdinalIgnoreCase) || category.Contains("ui", StringComparison.OrdinalIgnoreCase))
        {
            return "legacy-ui";
        }

        if (category.Contains("wcf", StringComparison.OrdinalIgnoreCase) || category.Contains("service", StringComparison.OrdinalIgnoreCase) || category.Contains("http", StringComparison.OrdinalIgnoreCase))
        {
            return "legacy-service";
        }

        if (category.Contains("sql", StringComparison.OrdinalIgnoreCase) || category.Contains("database", StringComparison.OrdinalIgnoreCase) || category.Contains("data", StringComparison.OrdinalIgnoreCase))
        {
            return "legacy-data";
        }

        if (category.Contains("gap", StringComparison.OrdinalIgnoreCase) || category.Contains("unknown", StringComparison.OrdinalIgnoreCase) || category.Contains("missing", StringComparison.OrdinalIgnoreCase))
        {
            return "input-availability";
        }

        return "static-evidence";
    }

    private static string TitleFor(string section)
    {
        return section switch
        {
            "legacy-ui" => "Legacy UI Evidence",
            "legacy-service" => "Legacy Service Evidence",
            "legacy-data" => "Legacy Data Evidence",
            "input-availability" => "Input Availability",
            _ => "Static Evidence"
        };
    }

    private static IReadOnlyList<string> RuleLimitations(string ruleId)
    {
        if (ruleId.StartsWith("legacy.", StringComparison.Ordinal))
        {
            return ["Legacy static evidence does not prove runtime execution, deployment, reachability, or production use."];
        }

        if (ruleId.StartsWith("csharp.syntax.", StringComparison.Ordinal))
        {
            return ["Syntax evidence does not prove compiler-resolved symbols."];
        }

        if (ruleId.StartsWith("csharp.semantic.", StringComparison.Ordinal))
        {
            return ["Semantic evidence depends on successful compiler/project loading."];
        }

        return ["Aggregate evidence row does not prove runtime behavior."];
    }

    private static bool RuleCatalogContains(string catalogText, string ruleId)
    {
        var pattern = @"^\s*-\s*id:\s*" + Regex.Escape(ruleId) + @"\s*$";
        return Regex.IsMatch(catalogText, pattern, RegexOptions.Multiline | RegexOptions.CultureInvariant);
    }

    private static LegacyEvidencePackValidationDiagnostic Diagnostic(string category, string path, string message)
    {
        return new LegacyEvidencePackValidationDiagnostic(category, path, LegacyEvidencePackRuleIds.SafetyValidation, message);
    }

    private static string Cell(string value)
    {
        return value.Replace("|", "\\|", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
    }

    private static void Increment(SortedDictionary<string, int> counts, string key)
    {
        counts[key] = counts.GetValueOrDefault(key) + 1;
    }

    private sealed record LegacyEvidencePackInput(
        IReadOnlyList<LegacyEvidencePackSource> Sources,
        LegacyEvidencePackSummary Summary,
        IReadOnlyList<LegacyEvidencePackSection> Sections,
        IReadOnlyList<LegacyEvidencePackGap> Gaps,
        IReadOnlyList<string> Limitations);
}
