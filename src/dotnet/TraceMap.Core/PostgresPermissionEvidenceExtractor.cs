using System.Text.RegularExpressions;

namespace TraceMap.Core;

public static partial class PostgresPermissionEvidenceExtractor
{
    public const string RegistryVersion = "postgres-permission-prerequisites/v1";

    private const string StatementLimitation = "Static permission statement evidence only; effective privileges, inheritance, ordering, object existence, and runtime authorization are not proven.";
    private const string PrerequisiteLimitation = "Deterministic static review guidance only; the candidate is not an authoritative PostgreSQL authorization rule and requires DBA/operator validation.";
    private const string GapLimitation = "Permission analysis is partial; missing or conflicting script evidence does not establish live privilege state.";

    private static readonly HashSet<string> OwnerReviewCapabilities = new(StringComparer.Ordinal)
    {
        "create-extension", "create-foreign-server", "create-user-mapping",
        "create-publication", "create-subscription", "schedule-job", "ownership"
    };

    internal static IReadOnlyList<CodeFact> ExtractFilePermissions(
        ScanManifest manifest,
        string relativePath,
        IReadOnlyList<SqlExecutionContextExtractor.SqlStatement> statements,
        IReadOnlyList<CodeFact> fileFacts)
    {
        var contexts = EffectiveContexts(fileFacts);
        var protectedOrdinals = fileFacts
            .Where(fact => fact.FactType == FactTypes.SecretBearingSqlStep || fact.RuleId == RuleIds.DatabaseSqlSecretSafetyGap)
            .Select(fact => ParseOrdinal(fact.Properties.GetValueOrDefault("statementOrdinal")))
            .ToHashSet();
        var facts = new List<CodeFact>();
        foreach (var statement in statements)
        {
            var permission = Classify(statement.StructuralText);
            if (permission is null)
            {
                if (PermissionPrefixPattern().IsMatch(statement.StructuralText))
                {
                    facts.Add(CreateGap(manifest, relativePath, statement.StartLine, statement.EndLine, statement.Ordinal, "unsupported-or-dynamic-permission-statement"));
                }
                continue;
            }

            contexts.TryGetValue(statement.Ordinal, out var contextFact);
            var coverage = statement.LexicallyComplete && permission.IdentityEstablished ? "complete" : "reduced";
            var properties = new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["actionKind"] = permission.ActionKind,
                ["capabilityCode"] = permission.CapabilityCode,
                ["contextRole"] = ContextRole(contextFact),
                ["coverage"] = coverage,
                ["identityPrecision"] = permission.IdentityEstablished ? "opaque-object-and-role" : "unknown",
                ["limitation"] = StatementLimitation,
                ["objectKind"] = permission.ObjectKind,
                ["registryVersion"] = RegistryVersion,
                ["statementOrdinal"] = statement.Ordinal.ToString()
            };
            if (permission.ObjectIdentity is not null) properties["objectIdentity"] = permission.ObjectIdentity;
            if (permission.RoleIdentity is not null) properties["roleIdentity"] = permission.RoleIdentity;
            if (permission.LinkIdentity is not null) properties["linkIdentity"] = permission.LinkIdentity;

            facts.Add(FactFactory.Create(
                manifest,
                FactTypes.DatabasePermissionDeclared,
                RuleIds.DatabasePostgresPermissionStatement,
                EvidenceTiers.Tier2Structural,
                new EvidenceSpan(
                    relativePath,
                    statement.StartLine,
                    statement.EndLine,
                    protectedOrdinals.Contains(statement.Ordinal) ? null : FactFactory.Hash(statement.StructuralText, 32),
                    nameof(PostgresPermissionEvidenceExtractor),
                    ScannerVersions.PostgresPermissionEvidenceExtractor),
                targetSymbol: permission.ObjectIdentity is null ? $"permission-step-{statement.Ordinal:D4}" : $"permission-{permission.ObjectIdentity}",
                contractElement: permission.CapabilityCode,
                properties: properties));

            if (!statement.LexicallyComplete || !permission.IdentityEstablished)
            {
                facts.Add(CreateGap(manifest, relativePath, statement.StartLine, statement.EndLine, statement.Ordinal,
                    !statement.LexicallyComplete ? "malformed-permission-statement" : "unknown-permission-identity"));
            }
        }

        return facts;
    }

    internal static IReadOnlyList<CodeFact> Reduce(ScanManifest manifest, IEnumerable<CodeFact> allFacts)
    {
        var facts = allFacts.ToArray();
        var permissions = facts
            .Where(fact => fact.FactType == FactTypes.DatabasePermissionDeclared)
            .OrderBy(fact => fact.Evidence.FilePath, StringComparer.Ordinal)
            .ThenBy(fact => fact.Evidence.StartLine)
            .ThenBy(fact => fact.FactId, StringComparer.Ordinal)
            .ToArray();
        var archiveByLocation = facts
            .Where(fact => fact.FactType == FactTypes.DatabaseLinkSurfaceDeclared)
            .GroupBy(fact => (fact.Evidence.FilePath, Ordinal: ParseOrdinal(fact.Properties.GetValueOrDefault("statementOrdinal"))))
            .ToDictionary(group => group.Key, group => group.First());
        var contextOperations = facts
            .Where(fact => fact.FactType is FactTypes.SqlExecutionContextDeclared or FactTypes.SqlExecutionContextCandidate)
            .GroupBy(fact => (fact.Evidence.FilePath, Ordinal: ParseOrdinal(fact.Properties.GetValueOrDefault("statementOrdinal"))))
            .Select(group => group.OrderBy(fact => fact.FactType == FactTypes.SqlExecutionContextDeclared ? 0 : 1).ThenBy(fact => fact.FactId, StringComparer.Ordinal).First())
            .Where(fact => fact.Properties.GetValueOrDefault("stepKind") != "grant-permission")
            .OrderBy(fact => fact.Evidence.FilePath, StringComparer.Ordinal)
            .ThenBy(fact => ParseOrdinal(fact.Properties.GetValueOrDefault("statementOrdinal")))
            .ToArray();
        var results = new List<CodeFact>();

        foreach (var operation in contextOperations)
        {
            var ordinal = ParseOrdinal(operation.Properties.GetValueOrDefault("statementOrdinal"));
            archiveByLocation.TryGetValue((operation.Evidence.FilePath, ordinal), out var archiveSurface);
            foreach (var capability in CandidateCapabilities(operation.Properties.GetValueOrDefault("stepKind")))
            {
                var linkIdentity = archiveSurface?.Properties.GetValueOrDefault("linkIdentity");
                var compatible = permissions
                    .Where(permission => PermissionMatches(permission, capability, linkIdentity))
                    .ToArray();
                var grants = compatible.Where(permission => permission.Properties.GetValueOrDefault("actionKind") is "grant" or "owner-change" or "role-membership" or "default-privilege").ToArray();
                var revokes = compatible.Where(permission => permission.Properties.GetValueOrDefault("actionKind") == "revoke").ToArray();
                var crossFile = compatible.Any(permission => permission.Evidence.FilePath != operation.Evidence.FilePath);
                var permissionAfterOperation = grants.Any(permission => permission.Evidence.FilePath == operation.Evidence.FilePath
                    && permission.Evidence.StartLine > operation.Evidence.EndLine);
                var operationContext = ContextRole(operation);
                var permissionContextUnknown = grants.Any(permission => permission.Properties.GetValueOrDefault("contextRole") == "unknown");
                var incompatibleContext = grants.Any(permission => operationContext != "unknown"
                    && permission.Properties.GetValueOrDefault("contextRole") is { } permissionContext
                    && permissionContext != "unknown"
                    && permissionContext != operationContext);
                var operationReduced = operation.Properties.GetValueOrDefault("coverage") == "reduced";

                var (status, reason) = OwnerReviewCapabilities.Contains(capability)
                    ? ("needs-owner-review", "capability-requires-owner-validation")
                    : revokes.Length > 0
                        ? ("conflicting-evidence", grants.Length > 0 ? "grant-and-revoke-in-scripts" : "revoke-in-scripts")
                        : grants.Length == 0
                            ? ("missing-evidence", "no-compatible-permission-statement")
                            : incompatibleContext
                                ? ("conflicting-evidence", "incompatible-permission-context")
                            : crossFile
                                ? ("needs-owner-review", "cross-file-order-unknown")
                                : permissionAfterOperation
                                    ? ("needs-owner-review", "permission-after-operation")
                                    : permissionContextUnknown || operationContext == "unknown"
                                        ? ("needs-owner-review", "permission-context-unknown")
                                : operationReduced || grants.Any(grant => grant.Properties.GetValueOrDefault("coverage") != "complete")
                                    ? ("unknown", "reduced-input-evidence")
                                    : ("present-in-scripts", "compatible-grant-in-scripts");
                var coverage = status == "present-in-scripts" ? "complete" : "reduced";
                var properties = new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["candidateCapability"] = capability,
                    ["contextRole"] = ContextRole(operation),
                    ["coverage"] = coverage,
                    ["evidenceStatus"] = status,
                    ["limitation"] = PrerequisiteLimitation,
                    ["operationKind"] = operation.Properties.GetValueOrDefault("stepKind") ?? "unknown-sql-step",
                    ["ownerQuestion"] = OwnerQuestion(capability, status),
                    ["reasonCode"] = reason,
                    ["registryVersion"] = RegistryVersion,
                    ["statementOrdinal"] = ordinal.ToString()
                };
                if (archiveSurface is not null) properties["operationFactId"] = archiveSurface.FactId;
                if (linkIdentity is not null) properties["linkIdentity"] = linkIdentity;
                if (grants.Length > 0) properties["supportingFactIds"] = JoinFactIds(grants);
                if (revokes.Length > 0) properties["contradictingFactIds"] = JoinFactIds(revokes);

                results.Add(FactFactory.Create(
                    manifest,
                    FactTypes.DatabasePrerequisiteCandidate,
                    RuleIds.DatabasePostgresPermissionPrerequisite,
                    EvidenceTiers.Tier2Structural,
                    new EvidenceSpan(
                        operation.Evidence.FilePath,
                        operation.Evidence.StartLine,
                        operation.Evidence.EndLine,
                        operation.Evidence.SnippetHash,
                        nameof(PostgresPermissionEvidenceExtractor),
                        ScannerVersions.PostgresPermissionEvidenceExtractor),
                    targetSymbol: operation.TargetSymbol,
                    contractElement: capability,
                    properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["candidateCapability"] = capability,
                        ["contextRole"] = ContextRole(operation),
                        ["coverage"] = operationReduced ? "reduced" : "complete",
                        ["limitation"] = PrerequisiteLimitation,
                        ["operationKind"] = operation.Properties.GetValueOrDefault("stepKind") ?? "unknown-sql-step",
                        ["registryVersion"] = RegistryVersion,
                        ["statementOrdinal"] = ordinal.ToString()
                    }));

                results.Add(FactFactory.Create(
                    manifest,
                    FactTypes.DatabasePrerequisiteEvidence,
                    RuleIds.DatabasePostgresPermissionCoverage,
                    EvidenceTiers.Tier2Structural,
                    new EvidenceSpan(
                        operation.Evidence.FilePath,
                        operation.Evidence.StartLine,
                        operation.Evidence.EndLine,
                        operation.Evidence.SnippetHash,
                        nameof(PostgresPermissionEvidenceExtractor),
                        ScannerVersions.PostgresPermissionEvidenceExtractor),
                    targetSymbol: operation.TargetSymbol,
                    contractElement: capability,
                    properties: properties));

                if (status is not "present-in-scripts")
                {
                    results.Add(CreateGap(manifest, operation.Evidence.FilePath, operation.Evidence.StartLine, operation.Evidence.EndLine, ordinal, $"{status}:{capability}"));
                }
            }
        }

        return results;
    }

    private static PermissionStatement? Classify(string structural)
    {
        var match = GrantObjectPattern().Match(structural);
        if (match.Success)
        {
            var action = match.Groups["action"].Value.Equals("GRANT", StringComparison.OrdinalIgnoreCase) ? "grant" : "revoke";
            var kind = ObjectKind(match.Groups["kind"].Value);
            var capability = Capability(match.Groups["privileges"].Value, kind);
            var objectName = match.Groups["object"].Value;
            var roleName = match.Groups["role"].Value;
            return Statement(action, kind, capability, objectName, roleName);
        }

        match = OwnerPattern().Match(structural);
        if (match.Success) return Statement("owner-change", ObjectKind(match.Groups["kind"].Value), "ownership", match.Groups["object"].Value, match.Groups["role"].Value);
        match = DefaultPrivilegesPattern().Match(structural);
        if (match.Success) return Statement("default-privilege", ObjectKind(match.Groups["kind"].Value), "default-privilege", "default-scope", match.Groups["role"].Value);
        match = RoleMembershipPattern().Match(structural);
        if (match.Success) return Statement(match.Groups["action"].Value.Equals("GRANT", StringComparison.OrdinalIgnoreCase) ? "role-membership" : "revoke", "role", "role-membership", match.Groups["member"].Value, match.Groups["role"].Value);
        return null;
    }

    private static PermissionStatement Statement(string action, string kind, string capability, string objectName, string roleName)
    {
        var objectIdentity = Identity($"permission-object:{kind}", objectName);
        var roleIdentity = Identity("permission-role", roleName);
        var linkIdentity = kind == "foreign-server" ? Identity("postgres-fdw-server", objectName) : null;
        return new PermissionStatement(action, kind, capability, objectIdentity, roleIdentity, linkIdentity, objectIdentity is not null && roleIdentity is not null);
    }

    private static IEnumerable<string> CandidateCapabilities(string? stepKind) => stepKind switch
    {
        "extension-setup" => ["create-extension"],
        "fdw-server-setup" => ["create-foreign-server"],
        "user-mapping" => ["create-user-mapping", "usage-foreign-server"],
        "schema-import" => ["usage-foreign-server", "create-schema-object"],
        "foreign-table-setup" => ["usage-foreign-server", "create-schema-object"],
        "publication-setup" => ["create-publication"],
        "subscription-setup" => ["create-subscription"],
        "scheduled-job" => ["schedule-job"],
        _ => []
    };

    private static bool PermissionMatches(CodeFact permission, string capability, string? linkIdentity)
    {
        if (permission.Properties.GetValueOrDefault("capabilityCode") != capability) return false;
        if (capability == "usage-foreign-server")
            return !string.IsNullOrWhiteSpace(linkIdentity) && permission.Properties.GetValueOrDefault("linkIdentity") == linkIdentity;
        return true;
    }

    private static string Capability(string privileges, string objectKind)
    {
        var value = privileges.ToUpperInvariant();
        if (value.Contains("ALL", StringComparison.Ordinal)) return "all-privileges-review";
        if (objectKind == "foreign-server" && value.Contains("USAGE", StringComparison.Ordinal)) return "usage-foreign-server";
        if (objectKind == "foreign-wrapper" && value.Contains("USAGE", StringComparison.Ordinal)) return "usage-foreign-wrapper";
        if (objectKind == "schema" && value.Contains("CREATE", StringComparison.Ordinal)) return "create-schema-object";
        if (objectKind == "schema" && value.Contains("USAGE", StringComparison.Ordinal)) return "usage-schema";
        if (objectKind == "database" && value.Contains("CONNECT", StringComparison.Ordinal)) return "connect-database";
        if (objectKind == "sequence" && value.Contains("USAGE", StringComparison.Ordinal)) return "sequence-usage";
        if (objectKind == "routine" && value.Contains("EXECUTE", StringComparison.Ordinal)) return "execute-routine";
        if (objectKind == "table" && Regex.IsMatch(value, @"\b(?:SELECT|INSERT|UPDATE|DELETE|TRUNCATE|REFERENCES|TRIGGER)\b", RegexOptions.CultureInvariant)) return "table-access";
        return "unknown-permission";
    }

    private static string ObjectKind(string value) => Regex.Replace(value.Trim().ToLowerInvariant(), @"\s+", "-") switch
    {
        "function" or "procedure" or "functions" => "routine",
        "tables" => "table",
        "sequences" => "sequence",
        "foreign-data-wrapper" => "foreign-wrapper",
        var kind => kind
    };

    private static IReadOnlyDictionary<int, CodeFact> EffectiveContexts(IEnumerable<CodeFact> facts) => facts
        .Where(fact => fact.FactType is FactTypes.SqlExecutionContextDeclared or FactTypes.SqlExecutionContextCandidate)
        .GroupBy(fact => ParseOrdinal(fact.Properties.GetValueOrDefault("statementOrdinal")))
        .ToDictionary(group => group.Key, group => group.OrderBy(fact => fact.FactType == FactTypes.SqlExecutionContextDeclared ? 0 : 1).ThenBy(fact => fact.FactId, StringComparer.Ordinal).First());

    private static string ContextRole(CodeFact? fact)
    {
        if (fact is null) return "unknown";
        if (fact.Properties.GetValueOrDefault("executionMode") == "scheduled") return "scheduled";
        if (fact.Properties.GetValueOrDefault("databaseRole") == "validation-only") return "validation-only";
        return fact.Properties.GetValueOrDefault("serverRole") switch { "source" => "source", "archive-target" => "archive-target", "admin" => "admin", _ => "unknown" };
    }

    private static string? Identity(string family, string value) => string.IsNullOrWhiteSpace(value) ? null : FactFactory.Hash($"{family}|{value.ToUpperInvariant()}", 24);
    private static int ParseOrdinal(string? value) => int.TryParse(value, out var ordinal) ? ordinal : 0;
    private static string JoinFactIds(IEnumerable<CodeFact> facts) => string.Join(",", facts.Select(fact => fact.FactId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
    private static string OwnerQuestion(string capability, string status) => status == "present-in-scripts"
        ? $"Confirm `{capability}` is effective for the runtime role and target context."
        : $"Who owns validation of `{capability}` for the intended execution context?";

    private static CodeFact CreateGap(ScanManifest manifest, string path, int start, int end, int ordinal, string kind) => FactFactory.Create(
        manifest, FactTypes.AnalysisGap, RuleIds.DatabasePostgresPermissionGap, EvidenceTiers.Tier4Unknown,
        new EvidenceSpan(path, Math.Max(1, start), Math.Max(Math.Max(1, start), end), null, nameof(PostgresPermissionEvidenceExtractor), ScannerVersions.PostgresPermissionEvidenceExtractor),
        targetSymbol: ordinal > 0 ? $"sql-step-{ordinal:D4}" : null,
        properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["coverage"] = "reduced", ["gapKind"] = kind, ["limitation"] = GapLimitation, ["statementOrdinal"] = ordinal.ToString()
        });

    [GeneratedRegex(@"\b(?<action>GRANT|REVOKE)\s+(?<privileges>[A-Za-z_,\s]+?)\s+ON\s+(?<kind>DATABASE|SCHEMA|TABLE|SEQUENCE|FUNCTION|PROCEDURE|FOREIGN\s+SERVER|FOREIGN\s+DATA\s+WRAPPER)\s+(?<object>[A-Za-z_][A-Za-z0-9_$.]*)\s+(?:TO|FROM)\s+(?<role>[A-Za-z_][A-Za-z0-9_$]*)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex GrantObjectPattern();
    [GeneratedRegex(@"\bALTER\s+(?<kind>DATABASE|SCHEMA|TABLE|SEQUENCE|FUNCTION|PROCEDURE|FOREIGN\s+SERVER)\s+(?<object>[A-Za-z_][A-Za-z0-9_$.]*)[\s\S]*?\bOWNER\s+TO\s+(?<role>[A-Za-z_][A-Za-z0-9_$]*)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex OwnerPattern();
    [GeneratedRegex(@"\bALTER\s+DEFAULT\s+PRIVILEGES\b[\s\S]*?\bGRANT\s+[A-Za-z_,\s]+?\s+ON\s+(?<kind>TABLES|SEQUENCES|FUNCTIONS)\s+TO\s+(?<role>[A-Za-z_][A-Za-z0-9_$]*)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DefaultPrivilegesPattern();
    [GeneratedRegex(@"\b(?<action>GRANT|REVOKE)\s+(?<member>[A-Za-z_][A-Za-z0-9_$]*)\s+(?:TO|FROM)\s+(?<role>[A-Za-z_][A-Za-z0-9_$]*)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RoleMembershipPattern();
    [GeneratedRegex(@"^\s*(?:GRANT|REVOKE|ALTER\s+DEFAULT\s+PRIVILEGES|ALTER\s+(?:DATABASE|SCHEMA|TABLE|SEQUENCE|FUNCTION|PROCEDURE|FOREIGN\s+SERVER)\b)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PermissionPrefixPattern();

    private sealed record PermissionStatement(string ActionKind, string ObjectKind, string CapabilityCode, string? ObjectIdentity, string? RoleIdentity, string? LinkIdentity, bool IdentityEstablished);
}
