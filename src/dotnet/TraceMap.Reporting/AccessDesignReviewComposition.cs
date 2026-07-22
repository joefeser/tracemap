using System.Text.Json;
using Microsoft.Data.Sqlite;
using TraceMap.Core;

namespace TraceMap.Reporting;

internal sealed record AccessDesignFactRow(
    string SourceLabel,
    ScanManifest Manifest,
    CodeFact Fact,
    bool ProvenanceCompatible);

internal static class AccessDesignReviewComposer
{
    internal const string SectionName = "accessEvidence";
    private const string SectionRuleId = "release.review.section.v1";
    private static readonly JsonSerializerOptions ManifestJsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static readonly IReadOnlyList<string> Limitations =
    [
        "Access design evidence is an after-snapshot static inventory, not a before/after change conclusion.",
        "Count-only form/report, VBA, and macro coverage does not establish item identities, bindings, procedures, calls, command bodies, or runtime reachability.",
        "The section does not read rows, execute queries, open or render UI, inspect VBA source or macro bodies, prove effective permissions or external connectivity, approve a release, or certify migration safety.",
        "Raw SQL, query and external-source hashes, connection material, credentials, private object names, captions, expressions, local paths, and infrastructure identities are not rendered."
    ];

    private static readonly HashSet<string> ItemLevelFactTypes = new(StringComparer.Ordinal)
    {
        FactTypes.AccessFormDeclared,
        FactTypes.AccessReportDeclared,
        FactTypes.AccessControlDeclared,
        FactTypes.AccessBindingDeclared,
        FactTypes.AccessVbaModuleDeclared,
        FactTypes.AccessVbaProcedureDeclared,
        FactTypes.AccessNavigationCandidate,
        FactTypes.AccessEventBindingCandidate,
        FactTypes.AccessMacroDeclared
    };

    public static async Task<ReleaseReviewSection> BuildSectionAsync(
        string path,
        string indexKind,
        bool requested,
        string? sourceSelector,
        CancellationToken cancellationToken)
    {
        if (!requested)
        {
            return new ReleaseReviewSection(
                ReleaseReviewStatuses.NotRequested,
                [],
                [],
                ["Access design evidence is outside the requested release-review scope."]);
        }

        var rows = await ReadRowsAsync(path, indexKind, cancellationToken);
        if (!string.IsNullOrWhiteSpace(sourceSelector))
        {
            rows = rows
                .Where(row => string.Equals(row.SourceLabel, sourceSelector, StringComparison.Ordinal))
                .ToArray();
        }
        if (rows.Count == 0)
        {
            var gap = Gap(
                "CompatibleEvidenceUnavailable",
                null,
                SectionRuleId,
                EvidenceTiers.Tier4Unknown,
                ReleaseReviewClassifications.PartialAnalysis,
                "No compatible Microsoft Access design evidence is present in the after snapshot.",
                [],
                [
                    Pair("evidenceScope", "selected-after-index-access-catalog"),
                    Pair("indexKind", indexKind),
                    Pair("sourceSelector", sourceSelector ?? "all-sources")
                ]);
            return new ReleaseReviewSection(ReleaseReviewStatuses.Deferred, [], [gap], Limitations);
        }

        var incompatibleRows = rows.Where(row => !row.ProvenanceCompatible).ToArray();
        if (incompatibleRows.Length > 0)
        {
            var ids = incompatibleRows
                .Select(row => row.Fact.FactId)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .Take(24)
                .ToArray();
            var gap = Gap(
                "AccessEvidenceProvenanceUnavailable",
                null,
                SectionRuleId,
                EvidenceTiers.Tier4Unknown,
                ReleaseReviewClassifications.UnknownAnalysisGap,
                "Microsoft Access facts are present, but extractor ID/version provenance is unavailable; the design section is not projected.",
                ids,
                [Pair("incompatibleFactCount", incompatibleRows.Length.ToString(System.Globalization.CultureInfo.InvariantCulture))]);
            return new ReleaseReviewSection(ReleaseReviewStatuses.Unavailable, [], [gap], Limitations);
        }

        var findings = new List<ReleaseReviewFinding>();
        var gaps = new List<ReleaseReviewGap>();
        foreach (var row in rows)
        {
            if (row.Fact.FactType == FactTypes.AnalysisGap)
            {
                continue;
            }

            if (ItemLevelFactTypes.Contains(row.Fact.FactType))
            {
                continue;
            }

            var finding = FromFact(row);
            if (finding is not null)
            {
                findings.Add(finding);
            }
        }

        foreach (var group in rows.Where(row => row.Fact.FactType == FactTypes.AnalysisGap)
                     .GroupBy(row => (
                         row.SourceLabel,
                         row.Fact.RuleId,
                         Classification: SafeToken(row.Fact.Properties.GetValueOrDefault("classification"), "AccessAnalysisGap"),
                         row.Fact.Evidence.FilePath,
                         row.Fact.Evidence.StartLine,
                         row.Fact.Evidence.EndLine,
                         ScopeKind: SafeToken(row.Fact.Properties.GetValueOrDefault("scopeKind"), "unknown"),
                         ScopeStableKey: row.Fact.Properties.GetValueOrDefault("scopeStableKey") ?? string.Empty))
                     .OrderBy(group => group.Key.SourceLabel, StringComparer.Ordinal)
                     .ThenBy(group => group.Key.RuleId, StringComparer.Ordinal)
                     .ThenBy(group => group.Key.Classification, StringComparer.Ordinal)
                     .ThenBy(group => group.Key.FilePath, StringComparer.Ordinal)
                     .ThenBy(group => group.Key.StartLine)
                     .ThenBy(group => group.Key.EndLine)
                     .ThenBy(group => group.Key.ScopeKind, StringComparer.Ordinal)
                     .ThenBy(group => group.Key.ScopeStableKey, StringComparer.Ordinal))
        {
            gaps.Add(FromAnalysisGap(group));
        }

        foreach (var group in rows.Where(row => ItemLevelFactTypes.Contains(row.Fact.FactType))
                     .GroupBy(row => (
                         row.SourceLabel,
                         row.Fact.FactType,
                         row.Fact.Evidence.FilePath,
                         row.Fact.Evidence.StartLine,
                         row.Fact.Evidence.EndLine))
                     .OrderBy(group => group.Key.SourceLabel, StringComparer.Ordinal)
                     .ThenBy(group => group.Key.FactType, StringComparer.Ordinal)
                     .ThenBy(group => group.Key.FilePath, StringComparer.Ordinal)
                     .ThenBy(group => group.Key.StartLine)
                     .ThenBy(group => group.Key.EndLine))
        {
            var first = group.OrderBy(row => row.Fact.FactId, StringComparer.Ordinal).First();
            gaps.Add(Gap(
                "AccessItemEvidenceOutsideCountOnlyBoundary",
                first.SourceLabel,
                SectionRuleId,
                EvidenceTiers.Tier4Unknown,
                ReleaseReviewClassifications.PartialAnalysis,
                $"`{first.Fact.FactType}` rows are not composed because the shipped Access product boundary is count-only for UI, VBA, and macro capabilities.",
                group.Select(row => row.Fact.FactId).OrderBy(value => value, StringComparer.Ordinal).Take(24).ToArray(),
                [
                    Pair("factType", first.Fact.FactType),
                    Pair("factCount", group.Count().ToString(System.Globalization.CultureInfo.InvariantCulture)),
                    Pair("upstreamRuleId", first.Fact.RuleId),
                    Pair("upstreamEvidenceTier", first.Fact.EvidenceTier)
                ]));
        }

        var orderedFindings = findings
            .OrderBy(finding => finding.Classification == ReleaseReviewClassifications.ReviewRecommended ? 0 : 1)
            .ThenBy(finding => finding.SourceLabel, StringComparer.Ordinal)
            .ThenBy(finding => finding.FindingId, StringComparer.Ordinal)
            .ToArray();
        var orderedGaps = gaps
            .GroupBy(gap => gap.GapId, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(gap => gap.SourceLabel ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(gap => gap.GapKind, StringComparer.Ordinal)
            .ThenBy(gap => gap.GapId, StringComparer.Ordinal)
            .ToArray();
        var status = orderedGaps.Any(gap => gap.Classification == ReleaseReviewClassifications.TruncatedByLimit)
            ? ReleaseReviewStatuses.Truncated
            : ReleaseReviewStatuses.Available;
        return new ReleaseReviewSection(status, orderedFindings, orderedGaps, Limitations);
    }

    private static ReleaseReviewFinding? FromFact(AccessDesignFactRow row)
    {
        var fact = row.Fact;
        var (kind, metadata, classification) = fact.FactType switch
        {
            FactTypes.LegacyDataMetadataDeclared => ("database-inventory", InventoryMetadata(fact.Properties), ReleaseReviewClassifications.NoActionableEvidence),
            FactTypes.AnalyzerCapabilityDiagnostic => ("capability", Select(fact.Properties, "capability", "status"), CapabilityClassification(fact.Properties)),
            FactTypes.LegacyDataEntityDeclared => ("table", Select(fact.Properties, "entityKind", "modelKind", "descriptorRole", "coverageLabel"), ReleaseReviewClassifications.NoActionableEvidence),
            FactTypes.LegacyDataColumnDeclared => ("field", Select(fact.Properties, "ordinal", "dataTypeFamily", "declaredSize", "required", "descriptorRole"), ReleaseReviewClassifications.NoActionableEvidence),
            FactTypes.LegacyDataMappingDeclared => ("mapping", MappingMetadata(fact.Properties), ReleaseReviewClassifications.NoActionableEvidence),
            FactTypes.AccessQueryDeclared => ("saved-query", Select(fact.Properties, "queryKind", "referenceCoverage", "parameterCount", "isPassThrough"), QueryClassification(fact.Properties)),
            FactTypes.AccessQueryDependencyCandidate => ("query-dependency", Select(fact.Properties, "targetKind", "coverageLabel"), ReleaseReviewClassifications.NoActionableEvidence),
            FactTypes.AccessExternalLinkDeclared => ("external-boundary", Select(fact.Properties, "boundaryKind", "sourceKind", "coverageLabel"), ReleaseReviewClassifications.ReviewRecommended),
            _ => (null, [], ReleaseReviewClassifications.NoActionableEvidence)
        };
        if (kind is null)
        {
            return null;
        }

        var safeMetadata = Sorted(metadata.Concat(IdentityMetadata(fact, kind)));
        var coverage = Coverage(fact, safeMetadata);
        var limitations = SplitLimitations(fact.Properties.GetValueOrDefault("limitations"))
            .Concat(Limitations)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return new ReleaseReviewFinding(
            StableId("finding", row.SourceLabel, kind, fact.FactId),
            SectionName,
            row.SourceLabel,
            classification,
            fact.RuleId,
            fact.EvidenceTier,
            fact.CommitSha,
            null,
            SafeRelativePath(fact.Evidence.FilePath),
            fact.Evidence.StartLine,
            fact.Evidence.EndLine,
            Sorted([Pair("evidenceKind", kind), Pair("coverageLabel", coverage), .. safeMetadata]),
            [fact.FactId],
            [],
            limitations,
            fact.Evidence.ExtractorId,
            fact.Evidence.ExtractorVersion,
            coverage);
    }

    private static ReleaseReviewGap FromAnalysisGap(IEnumerable<AccessDesignFactRow> groupedRows)
    {
        var rows = groupedRows.OrderBy(row => row.Fact.FactId, StringComparer.Ordinal).ToArray();
        var row = rows[0];
        var fact = row.Fact;
        var kind = SafeToken(fact.Properties.GetValueOrDefault("classification"), "AccessAnalysisGap");
        var classification = kind.Contains("Limit", StringComparison.Ordinal)
            ? ReleaseReviewClassifications.TruncatedByLimit
            : ReleaseReviewClassifications.PartialAnalysis;
        return new ReleaseReviewGap(
            StableId(
                "gap",
                row.SourceLabel,
                kind,
                fact.RuleId,
                fact.Evidence.FilePath,
                fact.Evidence.StartLine.ToString(System.Globalization.CultureInfo.InvariantCulture),
                fact.Evidence.EndLine.ToString(System.Globalization.CultureInfo.InvariantCulture),
                fact.Properties.GetValueOrDefault("scopeKind") ?? string.Empty,
                fact.Properties.GetValueOrDefault("scopeStableKey") ?? string.Empty,
                string.Join(';', rows.Select(item => item.Fact.FactId))),
            kind,
            SectionName,
            row.SourceLabel,
            fact.RuleId,
            fact.EvidenceTier,
            classification,
            $"Upstream Microsoft Access evidence recorded the bounded `{kind}` coverage gap.",
            [],
            rows.Select(item => item.Fact.FactId).Take(24).ToArray(),
            [],
            Sorted([
                Pair("coverageLabel", "reduced"),
                Pair("extractorId", fact.Evidence.ExtractorId),
                Pair("extractorVersion", fact.Evidence.ExtractorVersion),
                Pair("filePath", SafeRelativePath(fact.Evidence.FilePath) ?? "unknown"),
                Pair("startLine", fact.Evidence.StartLine.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                Pair("endLine", fact.Evidence.EndLine.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                Pair("scopeKind", SafeToken(fact.Properties.GetValueOrDefault("scopeKind"), "unknown")),
                Pair("scopeDesignKey", SafeOpaqueKey(fact.Properties.GetValueOrDefault("scopeStableKey")) ?? string.Empty),
                Pair("factCount", rows.Length.ToString(System.Globalization.CultureInfo.InvariantCulture))
            ]));
    }

    private static ReleaseReviewGap Gap(
        string kind,
        string? sourceLabel,
        string ruleId,
        string tier,
        string classification,
        string message,
        IReadOnlyList<string> supportingFactIds,
        IReadOnlyList<KeyValuePair<string, string>> metadata) =>
        new(
            StableId("gap", sourceLabel ?? "none", kind, string.Join(';', supportingFactIds)),
            kind,
            SectionName,
            sourceLabel,
            ruleId,
            tier,
            classification,
            message,
            [],
            supportingFactIds,
            [],
            Sorted(metadata));

    private static IReadOnlyList<KeyValuePair<string, string>> InventoryMetadata(IReadOnlyDictionary<string, string> properties) =>
        Select(properties,
            "modelKind", "descriptorRole", "omittedSystemObjectCount",
            "formCount", "reportCount", "formsReportsCoverage",
            "vbaModuleCount", "vbaLoadedModuleCountUnchanged", "vbaCoverage",
            "namedMacroCount", "macroLoadedCountUnchanged", "macroCoverage",
            "coverageLabel");

    private static IReadOnlyList<KeyValuePair<string, string>> MappingMetadata(IReadOnlyDictionary<string, string> properties)
    {
        var values = Select(properties, "mappingKind", "indexPrimary", "indexUnique").ToList();
        if (SafeOpaqueKey(properties.GetValueOrDefault("stableModelKey")) is { } mappingDesignKey)
            values.Add(Pair("mappingDesignKey", mappingDesignKey));
        if (properties.TryGetValue("fieldStableKeys", out var fields))
            values.Add(Pair("fieldCount", CountList(fields).ToString(System.Globalization.CultureInfo.InvariantCulture)));
        if (properties.TryGetValue("fieldPairs", out var pairs))
            values.Add(Pair("fieldPairCount", CountList(pairs).ToString(System.Globalization.CultureInfo.InvariantCulture)));
        return Sorted(values);
    }

    private static IReadOnlyList<KeyValuePair<string, string>> Select(IReadOnlyDictionary<string, string> properties, params string[] keys) =>
        Sorted(keys.Where(properties.ContainsKey).Select(key => Pair(key, properties[key])));

    private static IReadOnlyList<KeyValuePair<string, string>> IdentityMetadata(CodeFact fact, string kind)
    {
        var values = new List<KeyValuePair<string, string>>();
        if (SafeOpaqueKey(fact.TargetSymbol) is { } target)
            values.Add(Pair(kind is "mapping" or "query-dependency" ? "targetDesignKey" : "designKey", target));
        if (SafeOpaqueKey(fact.SourceSymbol) is { } source)
            values.Add(Pair(kind switch
            {
                "mapping" or "query-dependency" => "sourceDesignKey",
                "external-boundary" => "designKey",
                _ => "parentDesignKey"
            }, source));
        return Sorted(values);
    }

    private static string? SafeOpaqueKey(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= 128
        && value.StartsWith("access-", StringComparison.Ordinal)
        && value.All(character => char.IsLetterOrDigit(character) || character == '-')
            ? value
            : null;

    private static string CapabilityClassification(IReadOnlyDictionary<string, string> properties)
    {
        var capability = properties.GetValueOrDefault("capability");
        var status = properties.GetValueOrDefault("status");
        return status is "available" or "observed"
            || (capability is "rowDataRead" or "executionPerformed" && status == "false")
            || (capability == "startupSuppression" && status == "force-disable-requested")
                ? ReleaseReviewClassifications.NoActionableEvidence
                : ReleaseReviewClassifications.ReviewRecommended;
    }

    private static string QueryClassification(IReadOnlyDictionary<string, string> properties) =>
        string.Equals(properties.GetValueOrDefault("isPassThrough"), "true", StringComparison.Ordinal)
            || properties.GetValueOrDefault("referenceCoverage") is "partial" or "unknown"
            ? ReleaseReviewClassifications.ReviewRecommended
            : ReleaseReviewClassifications.NoActionableEvidence;

    private static string Coverage(CodeFact fact, IReadOnlyList<KeyValuePair<string, string>> metadata) =>
        metadata.FirstOrDefault(pair => pair.Key is "coverageLabel" or "referenceCoverage").Value
        ?? fact.Properties.GetValueOrDefault("formsReportsCoverage")
        ?? fact.Properties.GetValueOrDefault("vbaCoverage")
        ?? fact.Properties.GetValueOrDefault("macroCoverage")
        ?? "catalog-observed";

    private static int CountList(string value) =>
        string.IsNullOrWhiteSpace(value) ? 0 : value.Split(';', StringSplitOptions.RemoveEmptyEntries).Length;

    private static IReadOnlyList<string> SplitLimitations(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(item => item.All(character => char.IsLetterOrDigit(character) || character is '-' or '_'))
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray();

    private static string SafeToken(string? value, string fallback) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= 128
        && value.All(character => char.IsLetterOrDigit(character) || character is '-' or '_')
            ? value
            : fallback;

    private static string? SafeRelativePath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var safePath = CombinedReportHelpers.SafePath(value);
        return safePath == "n/a"
            || safePath.Split('/', StringSplitOptions.RemoveEmptyEntries).Any(segment => segment == "..")
            ? null
            : safePath;
    }

    private static KeyValuePair<string, string> Pair(string key, string value) => new(key, value);

    private static IReadOnlyList<KeyValuePair<string, string>> Sorted(IEnumerable<KeyValuePair<string, string>> values) =>
        values.Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .ThenBy(pair => pair.Value, StringComparer.Ordinal)
            .ToArray();

    private static string StableId(params string[] parts) =>
        "release-review-" + FactFactory.Hash(string.Join('|', parts), 32);

    private static async Task<IReadOnlyList<AccessDesignFactRow>> ReadRowsAsync(
        string path,
        string indexKind,
        CancellationToken cancellationToken)
    {
        var builder = new SqliteConnectionStringBuilder { DataSource = Path.GetFullPath(path), Mode = SqliteOpenMode.ReadOnly };
        await using var connection = new SqliteConnection(builder.ToString());
        await connection.OpenAsync(cancellationToken);
        var table = indexKind == "combined" ? "combined_facts" : "facts";
        var hasExtractorId = await ColumnExistsAsync(connection, table, "extractor_id", cancellationToken);
        var hasExtractorVersion = await ColumnExistsAsync(connection, table, "extractor_version", cancellationToken);
        var extractorId = hasExtractorId ? "facts.extractor_id" : "null";
        var extractorVersion = hasExtractorVersion ? "facts.extractor_version" : "null";
        await using var command = connection.CreateCommand();
        command.CommandText = indexKind == "combined"
            ? $"""
                select sources.label, sources.manifest_json,
                       facts.combined_fact_id, facts.scan_id, facts.repo, facts.commit_sha, facts.project_path,
                       facts.fact_type, facts.rule_id, facts.evidence_tier, facts.source_symbol, facts.target_symbol,
                       facts.contract_element, facts.file_path, facts.start_line, facts.end_line, facts.snippet_hash,
                       {extractorId}, {extractorVersion}, facts.properties_json
                from combined_facts facts
                join index_sources sources on sources.source_index_id = facts.source_index_id
                where facts.rule_id like 'legacy.access.%'
                order by sources.label, facts.file_path, facts.start_line, facts.combined_fact_id;
                """
            : $"""
                select manifest.repo, manifest.manifest_json,
                       facts.fact_id, facts.scan_id, facts.repo, facts.commit_sha, facts.project_path,
                       facts.fact_type, facts.rule_id, facts.evidence_tier, facts.source_symbol, facts.target_symbol,
                       facts.contract_element, facts.file_path, facts.start_line, facts.end_line, facts.snippet_hash,
                       {extractorId}, {extractorVersion}, facts.properties_json
                from facts
                cross join scan_manifest manifest
                where facts.rule_id like 'legacy.access.%'
                order by facts.file_path, facts.start_line, facts.fact_id;
                """;
        var rows = new List<AccessDesignFactRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var manifest = JsonSerializer.Deserialize<ScanManifest>(reader.GetString(1), ManifestJsonOptions)
                ?? throw new InvalidDataException("Access design evidence input has an invalid scan manifest.");
            var extractorIdValue = reader.IsDBNull(17) ? null : reader.GetString(17);
            var extractorVersionValue = reader.IsDBNull(18) ? null : reader.GetString(18);
            var properties = reader.IsDBNull(19)
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                : JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(19))
                    ?? new Dictionary<string, string>(StringComparer.Ordinal);
            var evidence = new EvidenceSpan(
                reader.IsDBNull(13) ? "unknown" : reader.GetString(13),
                reader.IsDBNull(14) ? 1 : reader.GetInt32(14),
                reader.IsDBNull(15) ? 1 : reader.GetInt32(15),
                reader.IsDBNull(16) ? null : reader.GetString(16),
                extractorIdValue ?? "unknown",
                extractorVersionValue ?? "unknown");
            var fact = new CodeFact(
                reader.GetString(2),
                reader.IsDBNull(3) ? manifest.ScanId : reader.GetString(3),
                reader.IsDBNull(4) ? manifest.RepoName : reader.GetString(4),
                reader.IsDBNull(5) ? manifest.CommitSha : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.GetString(7),
                reader.GetString(8),
                reader.GetString(9),
                reader.IsDBNull(10) ? null : reader.GetString(10),
                reader.IsDBNull(11) ? null : reader.GetString(11),
                reader.IsDBNull(12) ? null : reader.GetString(12),
                evidence,
                properties);
            rows.Add(new AccessDesignFactRow(
                indexKind == "single" ? "single" : reader.GetString(0),
                manifest,
                fact,
                !string.IsNullOrWhiteSpace(extractorIdValue) && !string.IsNullOrWhiteSpace(extractorVersionValue)));
        }

        return rows;
    }

    private static async Task<bool> ColumnExistsAsync(
        SqliteConnection connection,
        string table,
        string column,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "select count(*) from pragma_table_info($table) where name = $column;";
        command.Parameters.AddWithValue("$table", table);
        command.Parameters.AddWithValue("$column", column);
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken), System.Globalization.CultureInfo.InvariantCulture) > 0;
    }
}
