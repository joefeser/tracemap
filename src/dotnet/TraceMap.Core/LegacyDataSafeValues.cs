using System.Text.RegularExpressions;

namespace TraceMap.Core;

internal static partial class LegacyDataSafeValues
{
    private static readonly Regex SafeIdentifierRegex = SafeIdentifierPattern();
    private static readonly string[] UnsafeSubstrings =
    [
        "password",
        "passwd",
        "pwd",
        "token",
        "secret",
        "apikey",
        "api_key",
        "connectionstring",
        "data source",
        "initial catalog",
        "user id",
        "userid",
        "uid="
    ];

    public static bool IsSafeIdentifier(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= 128
            && SafeIdentifierRegex.IsMatch(trimmed)
            && !IsUnsafeValue(trimmed);
    }

    public static void AddSafeOrHash(
        SortedDictionary<string, string> properties,
        string clearKey,
        string hashKey,
        string? value,
        string unsafeReason = "unsafe-identifier")
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var trimmed = value.Trim();
        properties.Remove(clearKey);
        properties.Remove(hashKey);
        properties.Remove($"{clearKey}Redaction");

        if (IsSafeIdentifier(trimmed))
        {
            properties[clearKey] = trimmed;
            return;
        }

        properties[hashKey] = FactFactory.Hash(trimmed, 32);
        properties[$"{clearKey}Redaction"] = unsafeReason;
    }

    public static string Identity(string kind, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return $"{kind}:unknown";
        }

        var trimmed = value.Trim();
        return IsSafeIdentifier(trimmed)
            ? $"{kind}:{trimmed}"
            : $"{kind}-hash:{FactFactory.Hash(trimmed, 32)}";
    }

    private static bool IsUnsafeValue(string value)
    {
        if (value.Contains("://", StringComparison.Ordinal)
            || value.Contains('\\', StringComparison.Ordinal)
            || value.StartsWith("/", StringComparison.Ordinal)
            || value.StartsWith("./", StringComparison.Ordinal)
            || value.StartsWith("../", StringComparison.Ordinal)
            || value.Contains("${", StringComparison.Ordinal)
            || value.Contains("$(", StringComparison.Ordinal)
            || value.Contains('%', StringComparison.Ordinal))
        {
            return true;
        }

        return UnsafeSubstrings.Any(fragment => value.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    }

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_.$+]*$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeIdentifierPattern();
}
