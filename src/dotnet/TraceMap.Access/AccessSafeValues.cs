using System.Security.Cryptography;
using TraceMap.Core;

namespace TraceMap.Access;

public enum AccessIdentityDisclosurePolicy
{
    SafeIdentifier,
    HashOnly
}

public static class AccessSafeValues
{
    private const string CrosstabPivotColumnCandidatePrefix = "access-query-pivot-column-candidate-";
    private const string CrosstabPivotPrefixMismatchCandidatePrefix = "access-query-pivot-prefix-mismatch-candidate-";
    private const string QueryOutputAliasMismatchCandidatePrefix = "access-query-output-alias-mismatch-candidate-";

    public static string DatabaseIdentitySeed(string repositoryIdentityHash, string commitSha, string relativePath, string databaseHash) =>
        RoleHash("access-database-identity", string.Join('|', repositoryIdentityHash, commitSha, relativePath.Replace('\\', '/'), databaseHash));

    public static string DatabaseStableKey(string identitySeed) => $"access-database-{FactFactory.Hash($"access-database/v1|{identitySeed}", 32)}";

    public static AccessSafeIdentity Identity(
        string databaseIdentitySeed,
        string objectKind,
        string? value,
        int occurrence = 0,
        AccessIdentityDisclosurePolicy disclosurePolicy = AccessIdentityDisclosurePolicy.SafeIdentifier)
    {
        var raw = string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim();
        var nameHash = RoleHash($"access-{objectKind}-name", raw);
        var display = disclosurePolicy == AccessIdentityDisclosurePolicy.SafeIdentifier
            && LegacyDataSafeValues.IsSafeIdentifier(raw)
                ? raw
                : null;
        var keyMaterial = string.Join('|', "access-object/v1", databaseIdentitySeed, objectKind, display ?? $"hash:{nameHash}", occurrence);
        return new AccessSafeIdentity(display, nameHash, $"access-{objectKind}-{FactFactory.Hash(keyMaterial, 32)}");
    }

    internal static AccessSafeIdentity CrosstabPivotColumnCandidate(
        string databaseIdentitySeed,
        string queryStableKey,
        string columnName) =>
        HashOnlyCandidate(
            databaseIdentitySeed,
            $"query-pivot-column-candidate-{queryStableKey}",
            columnName);

    internal static bool IsCrosstabPivotColumnCandidate(string stableKey) =>
        stableKey.StartsWith(CrosstabPivotColumnCandidatePrefix, StringComparison.Ordinal);

    internal static AccessSafeIdentity CrosstabPivotPrefixMismatchCandidate(
        string databaseIdentitySeed,
        string queryStableKey,
        string requestedColumnName) =>
        HashOnlyCandidate(
            databaseIdentitySeed,
            $"query-pivot-prefix-mismatch-candidate-{queryStableKey}",
            requestedColumnName);

    internal static bool IsCrosstabPivotPrefixMismatchCandidate(string stableKey) =>
        stableKey.StartsWith(CrosstabPivotPrefixMismatchCandidatePrefix, StringComparison.Ordinal);

    internal static AccessSafeIdentity QueryOutputAliasMismatchCandidate(
        string databaseIdentitySeed,
        string queryStableKey,
        string requestedFieldName) =>
        HashOnlyCandidate(
            databaseIdentitySeed,
            $"query-output-alias-mismatch-candidate-{queryStableKey}",
            requestedFieldName);

    internal static bool IsQueryOutputAliasMismatchCandidate(string stableKey) =>
        stableKey.StartsWith(QueryOutputAliasMismatchCandidatePrefix, StringComparison.Ordinal);

    internal static AccessSafeIdentity HashOnlyCandidate(
        string databaseIdentitySeed,
        string objectKind,
        string value) =>
        Identity(
            databaseIdentitySeed,
            objectKind,
            value,
            disclosurePolicy: AccessIdentityDisclosurePolicy.HashOnly);

    public static string RoleHash(string role, string value) => FactFactory.Hash($"{role}\0{value}", 64);

    internal static bool FixedHashEquals(string left, string right)
    {
        if (left.Length != 64 || right.Length != 64) return false;
        try
        {
            return CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(left),
                Convert.FromHexString(right));
        }
        catch
        {
            return false;
        }
    }

    public static string ProviderFamily(string? connection)
    {
        if (string.IsNullOrWhiteSpace(connection)) return "unknown";
        if (connection.Contains("ODBC", StringComparison.OrdinalIgnoreCase)) return "odbc";
        if (connection.Contains("SharePoint", StringComparison.OrdinalIgnoreCase) || connection.Contains("WSS", StringComparison.OrdinalIgnoreCase)) return "sharepoint";
        if (connection.Contains("Excel", StringComparison.OrdinalIgnoreCase)) return "excel";
        if (connection.Contains("Text", StringComparison.OrdinalIgnoreCase)) return "text";
        if (connection.Contains(".accdb", StringComparison.OrdinalIgnoreCase) || connection.Contains(".mdb", StringComparison.OrdinalIgnoreCase)) return "access-file";
        return "external-other";
    }

    public static string DaoTypeFamily(int value) => value switch
    {
        1 => "boolean",
        2 => "byte",
        3 => "integer",
        4 => "long",
        5 => "currency",
        6 => "single",
        7 => "double",
        8 => "date-time",
        9 => "binary",
        10 => "text",
        11 => "long-binary",
        12 => "memo",
        15 => "guid",
        16 => "bigint",
        17 => "varbinary",
        18 => "char",
        19 => "numeric",
        20 => "decimal",
        21 => "float",
        22 => "time",
        23 => "timestamp",
        _ => $"dao-type-{value}"
    };

    public static string QueryKind(int value) => value switch
    {
        0 => "select",
        16 => "crosstab",
        32 => "delete",
        48 => "update",
        64 => "append",
        80 => "make-table",
        96 => "data-definition",
        112 => "pass-through",
        128 => "union",
        160 => "bulk",
        224 => "compound",
        _ => $"query-type-{value}"
    };
}
