using System.Text.RegularExpressions;

namespace TraceMap.Core;

public static partial class PostgresArchiveLinkExtractor
{
    private const string SurfaceLimitation = "Static PostgreSQL archive-link statement evidence only; connectivity, applied state, permissions, replication health, scheduling, execution, and archive correctness are not proven.";
    private const string PrerequisiteLimitation = "Checked-in prerequisite evidence only; absence means missing evidence, not missing live database state or capability.";
    private const string GapLimitation = "Archive-link analysis is partial; the gap does not establish runtime failure or absence of a live link.";

    internal static IReadOnlyList<CodeFact> ExtractFileSurfaces(
        ScanManifest manifest,
        string relativePath,
        IReadOnlyList<SqlExecutionContextExtractor.SqlStatement> statements,
        IReadOnlyList<CodeFact> fileFacts)
    {
        var contexts = fileFacts
            .Where(fact => fact.FactType is FactTypes.SqlExecutionContextDeclared or FactTypes.SqlExecutionContextCandidate)
            .GroupBy(fact => ParseOrdinal(fact.Properties.GetValueOrDefault("statementOrdinal")))
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(fact => fact.FactType == FactTypes.SqlExecutionContextDeclared ? 0 : 1).ThenBy(fact => fact.FactId, StringComparer.Ordinal).First());
        var safetyGapOrdinals = fileFacts
            .Where(fact => fact.RuleId == RuleIds.DatabaseSqlSecretSafetyGap)
            .Select(fact => ParseOrdinal(fact.Properties.GetValueOrDefault("statementOrdinal")))
            .ToHashSet();
        var facts = new List<CodeFact>();
        foreach (var statement in statements)
        {
            var classification = Classify(statement.StructuralText);
            if (classification is null)
            {
                continue;
            }

            contexts.TryGetValue(statement.Ordinal, out var contextFact);
            var contextRole = ContextRole(contextFact);
            var contextEvidence = contextFact?.FactType == FactTypes.SqlExecutionContextDeclared
                && contextFact.Properties.GetValueOrDefault("contextClassification") == "declared"
                ? "declared"
                : "inferred-or-unknown";
            var coverage = statement.LexicallyComplete
                && contextRole != "unknown"
                && contextEvidence == "declared"
                ? "complete"
                : "reduced";
            var identity = FactFactory.Hash($"{relativePath}|{statement.Ordinal}|{classification.Mechanism}|{classification.SurfaceKind}", 24);
            var surfaceProperties = new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["contextEvidence"] = contextEvidence,
                ["contextRole"] = contextRole,
                ["coverage"] = coverage,
                ["identityPrecision"] = "span-only",
                ["limitation"] = SurfaceLimitation,
                ["mechanism"] = classification.Mechanism,
                ["objectIdentity"] = identity,
                ["statementOrdinal"] = statement.Ordinal.ToString(),
                ["surfaceKind"] = classification.SurfaceKind
            };
            if (classification.LinkIdentity is not null)
            {
                surfaceProperties["linkIdentity"] = classification.LinkIdentity;
            }
            facts.Add(FactFactory.Create(
                manifest,
                FactTypes.DatabaseLinkSurfaceDeclared,
                RuleIds.DatabasePostgresArchiveLink,
                EvidenceTiers.Tier2Structural,
                new EvidenceSpan(
                    relativePath,
                    statement.StartLine,
                    statement.EndLine,
                    null,
                    nameof(PostgresArchiveLinkExtractor),
                    ScannerVersions.PostgresArchiveLinkExtractor),
                targetSymbol: $"archive-link-{identity}",
                contractElement: classification.SurfaceKind,
                properties: surfaceProperties));

            if (!statement.LexicallyComplete)
            {
                facts.Add(CreateGap(
                    manifest,
                    relativePath,
                    statement.StartLine,
                    statement.EndLine,
                    statement.Ordinal,
                    classification.Mechanism,
                    "malformed-statement"));
            }
            else if (safetyGapOrdinals.Contains(statement.Ordinal))
            {
                facts.Add(CreateGap(
                    manifest,
                    relativePath,
                    statement.StartLine,
                    statement.EndLine,
                    statement.Ordinal,
                    classification.Mechanism,
                    "dynamic-or-unsupported-boundary"));
            }
        }

        return facts;
    }

    internal static IReadOnlyList<CodeFact> Reduce(ScanManifest manifest, IEnumerable<CodeFact> allFacts)
    {
        var surfaces = allFacts
            .Where(fact => fact.FactType == FactTypes.DatabaseLinkSurfaceDeclared)
            .OrderBy(fact => fact.Evidence.FilePath, StringComparer.Ordinal)
            .ThenBy(fact => fact.Evidence.StartLine)
            .ThenBy(fact => fact.FactId, StringComparer.Ordinal)
            .ToArray();
        var facts = new List<CodeFact>();

        foreach (var surface in surfaces)
        {
            var mechanism = surface.Properties["mechanism"];
            var surfaceKind = surface.Properties["surfaceKind"];
            if (surface.Properties.GetValueOrDefault("contextRole") == "unknown")
            {
                facts.Add(CreateGap(
                    manifest,
                    surface.Evidence.FilePath,
                    surface.Evidence.StartLine,
                    surface.Evidence.EndLine,
                    ParseOrdinal(surface.Properties.GetValueOrDefault("statementOrdinal")),
                    mechanism,
                    "unknown-context"));
            }

            foreach (var prerequisiteCode in Prerequisites(mechanism, surfaceKind))
            {
                var supporting = surfaces.FirstOrDefault(candidate =>
                    MechanismMatchesPrerequisite(prerequisiteCode, mechanism, candidate.Properties.GetValueOrDefault("mechanism"))
                    && Satisfies(prerequisiteCode, candidate.Properties.GetValueOrDefault("surfaceKind"))
                    && LinkIdentityMatches(prerequisiteCode, candidate, surface));
                var satisfaction = supporting is null ? "missing-evidence" : "established-static-evidence";
                var properties = new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["coverage"] = supporting is null ? "reduced" : "complete",
                    ["limitation"] = PrerequisiteLimitation,
                    ["mechanism"] = mechanism,
                    ["prerequisiteCode"] = prerequisiteCode,
                    ["satisfaction"] = satisfaction,
                    ["surfaceFactId"] = surface.FactId
                };
                if (supporting is not null)
                {
                    properties["supportingFactIds"] = supporting.FactId;
                }

                facts.Add(FactFactory.Create(
                    manifest,
                    FactTypes.DatabasePrerequisiteCandidate,
                    RuleIds.DatabasePostgresArchiveLinkPrerequisite,
                    EvidenceTiers.Tier2Structural,
                    new EvidenceSpan(
                        surface.Evidence.FilePath,
                        surface.Evidence.StartLine,
                        surface.Evidence.EndLine,
                        null,
                        nameof(PostgresArchiveLinkExtractor),
                        ScannerVersions.PostgresArchiveLinkExtractor),
                    targetSymbol: surface.TargetSymbol,
                    contractElement: prerequisiteCode,
                    properties: properties));

                if (supporting is null)
                {
                    facts.Add(CreateGap(
                        manifest,
                        surface.Evidence.FilePath,
                        surface.Evidence.StartLine,
                        surface.Evidence.EndLine,
                        ParseOrdinal(surface.Properties.GetValueOrDefault("statementOrdinal")),
                        mechanism,
                        $"missing-evidence:{prerequisiteCode}"));
                    continue;
                }

                var direction = Direction(supporting, surface);
                facts.Add(FactFactory.Create(
                    manifest,
                    FactTypes.DatabaseLinkEdgeCandidate,
                    RuleIds.DatabasePostgresArchiveLink,
                    EvidenceTiers.Tier2Structural,
                    new EvidenceSpan(
                        surface.Evidence.FilePath,
                        surface.Evidence.StartLine,
                        surface.Evidence.EndLine,
                        null,
                        nameof(PostgresArchiveLinkExtractor),
                        ScannerVersions.PostgresArchiveLinkExtractor),
                    sourceSymbol: supporting.TargetSymbol,
                    targetSymbol: surface.TargetSymbol,
                    contractElement: "prerequisite-evidence",
                    properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["coverage"] = direction == "unknown" ? "reduced" : "complete",
                        ["direction"] = direction,
                        ["limitation"] = SurfaceLimitation,
                        ["linkKind"] = "prerequisite-evidence",
                        ["mechanism"] = mechanism,
                        ["supportingFactIds"] = string.Join(",", new[] { supporting.FactId, surface.FactId }.Order(StringComparer.Ordinal))
                    }));

                if (direction == "unknown")
                {
                    facts.Add(CreateGap(
                        manifest,
                        surface.Evidence.FilePath,
                        surface.Evidence.StartLine,
                        surface.Evidence.EndLine,
                        ParseOrdinal(surface.Properties.GetValueOrDefault("statementOrdinal")),
                        mechanism,
                        "unknown-direction"));
                }
            }
        }

        return facts;
    }

    private static ArchiveSurface? Classify(string structural)
    {
        if (PostgresFdwExtensionPattern().IsMatch(structural)) return new("postgres-fdw", "extension", null);
        if (ForeignServerPattern().IsMatch(structural)) return new("postgres-fdw", "foreign-server", LinkIdentity(ForeignServerIdentityPattern(), structural, "postgres-fdw-server"));
        if (UserMappingPattern().IsMatch(structural)) return new("postgres-fdw", "user-mapping", LinkIdentity(UserMappingIdentityPattern(), structural, "postgres-fdw-server"));
        if (ImportForeignSchemaPattern().IsMatch(structural)) return new("postgres-fdw", "schema-import", LinkIdentity(ImportForeignSchemaIdentityPattern(), structural, "postgres-fdw-server"));
        if (ForeignTablePattern().IsMatch(structural)) return new("postgres-fdw", "foreign-table", LinkIdentity(ForeignTableIdentityPattern(), structural, "postgres-fdw-server"));
        if (ForeignServerGrantPattern().IsMatch(structural)) return new("postgres-fdw", "server-grant", LinkIdentity(ForeignServerGrantIdentityPattern(), structural, "postgres-fdw-server"));
        if (DblinkExtensionPattern().IsMatch(structural)) return new("dblink", "extension", null);
        if (DblinkPattern().IsMatch(structural)) return new("dblink", "call", null);
        if (PublicationPattern().IsMatch(structural)) return new("logical-publication", "publication", LinkIdentity(PublicationIdentityPattern(), structural, "logical-publication"));
        if (SubscriptionPattern().IsMatch(structural)) return new("logical-subscription", "subscription", LinkIdentity(SubscriptionPublicationIdentityPattern(), structural, "logical-publication"));
        if (PgCronExtensionPattern().IsMatch(structural)) return new("pg-cron-scheduled-operation", "extension", null);
        if (CronPattern().IsMatch(structural)) return new("pg-cron-scheduled-operation", "scheduled-operation", null);
        return null;
    }

    private static IEnumerable<string> Prerequisites(string mechanism, string surfaceKind)
    {
        if (surfaceKind != "extension" && mechanism is "postgres-fdw" or "dblink" or "pg-cron-scheduled-operation")
            yield return "extension-declaration";
        if (mechanism == "postgres-fdw" && surfaceKind is "user-mapping" or "schema-import" or "foreign-table" or "server-grant")
            yield return "foreign-server-declaration";
        if (mechanism == "postgres-fdw" && surfaceKind is "schema-import" or "foreign-table")
            yield return "user-mapping-declaration";
        if (mechanism == "logical-subscription" && surfaceKind == "subscription")
            yield return "publication-declaration";
    }

    private static bool Satisfies(string prerequisiteCode, string? surfaceKind) => prerequisiteCode switch
    {
        "extension-declaration" => surfaceKind == "extension",
        "foreign-server-declaration" => surfaceKind == "foreign-server",
        "user-mapping-declaration" => surfaceKind == "user-mapping",
        "publication-declaration" => surfaceKind == "publication",
        _ => false
    };

    private static bool MechanismMatchesPrerequisite(string prerequisiteCode, string targetMechanism, string? candidateMechanism) =>
        prerequisiteCode == "publication-declaration"
            ? candidateMechanism == "logical-publication"
            : candidateMechanism == targetMechanism;

    private static bool LinkIdentityMatches(string prerequisiteCode, CodeFact candidate, CodeFact target)
    {
        if (prerequisiteCode == "extension-declaration")
        {
            return true;
        }

        var candidateIdentity = candidate.Properties.GetValueOrDefault("linkIdentity");
        var targetIdentity = target.Properties.GetValueOrDefault("linkIdentity");
        return !string.IsNullOrWhiteSpace(candidateIdentity)
            && candidateIdentity == targetIdentity;
    }

    private static string? LinkIdentity(Regex pattern, string structural, string family)
    {
        var match = pattern.Match(structural);
        var identifier = match.Success ? match.Groups["id"].Value : string.Empty;
        return string.IsNullOrWhiteSpace(identifier)
            ? null
            : FactFactory.Hash($"{family}|{identifier.ToUpperInvariant()}", 24);
    }

    private static string Direction(CodeFact source, CodeFact target)
    {
        if (source.Properties.GetValueOrDefault("contextEvidence") != "declared"
            || target.Properties.GetValueOrDefault("contextEvidence") != "declared") return "unknown";
        var left = source.Properties.GetValueOrDefault("contextRole");
        var right = target.Properties.GetValueOrDefault("contextRole");
        if (left == "source" && right == "archive-target") return "source-to-archive";
        if (left == "archive-target" && right == "source") return "archive-to-source";
        return "unknown";
    }

    private static string ContextRole(CodeFact? fact)
    {
        if (fact is null) return "unknown";
        if (fact.Properties.GetValueOrDefault("executionMode") == "scheduled") return "scheduled";
        if (fact.Properties.GetValueOrDefault("databaseRole") == "validation-only") return "validation-only";
        return fact.Properties.GetValueOrDefault("serverRole") switch
        {
            "source" => "source",
            "archive-target" => "archive-target",
            "admin" => "admin",
            _ => "unknown"
        };
    }

    private static CodeFact CreateGap(
        ScanManifest manifest,
        string relativePath,
        int startLine,
        int endLine,
        int ordinal,
        string mechanism,
        string gapKind) => FactFactory.Create(
            manifest,
            FactTypes.AnalysisGap,
            RuleIds.DatabasePostgresArchiveLinkGap,
            EvidenceTiers.Tier4Unknown,
            new EvidenceSpan(relativePath, Math.Max(1, startLine), Math.Max(Math.Max(1, startLine), endLine), null, nameof(PostgresArchiveLinkExtractor), ScannerVersions.PostgresArchiveLinkExtractor),
            targetSymbol: ordinal > 0 ? $"sql-step-{ordinal:D4}" : null,
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["coverage"] = "reduced",
                ["gapKind"] = gapKind,
                ["limitation"] = GapLimitation,
                ["mechanism"] = mechanism,
                ["statementOrdinal"] = ordinal.ToString()
            });

    private static int ParseOrdinal(string? value) => int.TryParse(value, out var ordinal) ? ordinal : 0;

    [GeneratedRegex(@"\b(?:CREATE|ALTER)\s+SERVER\b[\s\S]*?\bFOREIGN\s+DATA\s+WRAPPER\s+postgres_fdw\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ForeignServerPattern();
    [GeneratedRegex(@"\b(?:CREATE|ALTER)\s+SERVER\s+(?<id>[A-Za-z_][A-Za-z0-9_$]*)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ForeignServerIdentityPattern();
    [GeneratedRegex(@"\bCREATE\s+EXTENSION\b[\s\S]*?\bpostgres_fdw\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PostgresFdwExtensionPattern();
    [GeneratedRegex(@"\b(?:CREATE|ALTER)\s+USER\s+MAPPING\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UserMappingPattern();
    [GeneratedRegex(@"\bSERVER\s+(?<id>[A-Za-z_][A-Za-z0-9_$]*)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UserMappingIdentityPattern();
    [GeneratedRegex(@"\bIMPORT\s+FOREIGN\s+SCHEMA\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ImportForeignSchemaPattern();
    [GeneratedRegex(@"\bFROM\s+SERVER\s+(?<id>[A-Za-z_][A-Za-z0-9_$]*)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ImportForeignSchemaIdentityPattern();
    [GeneratedRegex(@"\bCREATE\s+FOREIGN\s+TABLE\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ForeignTablePattern();
    [GeneratedRegex(@"\bSERVER\s+(?<id>[A-Za-z_][A-Za-z0-9_$]*)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ForeignTableIdentityPattern();
    [GeneratedRegex(@"\bGRANT\b[\s\S]*?\bON\s+FOREIGN\s+SERVER\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ForeignServerGrantPattern();
    [GeneratedRegex(@"\bON\s+FOREIGN\s+SERVER\s+(?<id>[A-Za-z_][A-Za-z0-9_$]*)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ForeignServerGrantIdentityPattern();
    [GeneratedRegex(@"\b(?:dblink|dblink_exec|dblink_connect|dblink_connect_u)\s*\(", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DblinkPattern();
    [GeneratedRegex(@"\bCREATE\s+EXTENSION\b[\s\S]*?\bdblink\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DblinkExtensionPattern();
    [GeneratedRegex(@"\b(?:CREATE|ALTER)\s+PUBLICATION\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PublicationPattern();
    [GeneratedRegex(@"\b(?:CREATE|ALTER)\s+PUBLICATION\s+(?<id>[A-Za-z_][A-Za-z0-9_$]*)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PublicationIdentityPattern();
    [GeneratedRegex(@"\b(?:CREATE|ALTER)\s+SUBSCRIPTION\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SubscriptionPattern();
    [GeneratedRegex(@"\bPUBLICATION\s+(?<id>[A-Za-z_][A-Za-z0-9_$]*)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SubscriptionPublicationIdentityPattern();
    [GeneratedRegex(@"\bcron\s*\.\s*(?:schedule|schedule_in_database|unschedule)\s*\(", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CronPattern();
    [GeneratedRegex(@"\bCREATE\s+EXTENSION\b[\s\S]*?\bpg_cron\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PgCronExtensionPattern();

    private sealed record ArchiveSurface(string Mechanism, string SurfaceKind, string? LinkIdentity);
}
