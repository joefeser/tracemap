using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace TraceMap.Core;

public static class MessageSurfaceIdentity
{
    private static readonly Regex SafeRenderablePattern = new("^[A-Za-z0-9_.:/+-]{1,96}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static IReadOnlyList<string> SurfaceKinds { get; } =
    [
        "message-queue",
        "message-topic",
        "message-subscription",
        "message-exchange",
        "message-stream",
        "message-event",
        "message-channel",
        "message-unknown"
    ];

    public static MessageDestinationIdentity FromRaw(string rawValue)
    {
        var trimmed = rawValue.Trim();
        if (trimmed.Length == 0)
        {
            return new MessageDestinationIdentity("unknown", null, null, "missing-destination-identity");
        }

        if (IsWildcardPattern(trimmed))
        {
            return new MessageDestinationIdentity("dynamic", null, Sha256(trimmed), "wildcard-pattern-destination");
        }

        if (IsDynamic(trimmed))
        {
            return new MessageDestinationIdentity("dynamic", null, Sha256(trimmed), "dynamic-destination");
        }

        if (ContainsUnsafeValue(trimmed))
        {
            return new MessageDestinationIdentity("hashed", null, Sha256(trimmed), "unsafe-omitted");
        }

        if (!SafeRenderablePattern.IsMatch(trimmed))
        {
            return new MessageDestinationIdentity("hashed", null, Sha256(trimmed), "unsafe-omitted");
        }

        return new MessageDestinationIdentity("static", NormalizeRenderable(trimmed), null, null);
    }

    public static string SafeMetadataHash(IReadOnlyDictionary<string, string> metadata)
    {
        var material = string.Join(
            "\n",
            metadata
                .Where(pair => !string.IsNullOrWhiteSpace(pair.Value))
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => $"{pair.Key.ToLowerInvariant()}={pair.Value}"));
        return Sha256(material);
    }

    public static string StableKey(
        string sourceLabel,
        string language,
        string surfaceKind,
        string frameworkFamily,
        string operationDirection,
        string operationKind,
        string destinationIdentityStatus,
        string? normalizedDestinationKey,
        string? destinationHash,
        string? eventTypeIdentity,
        string? occurrenceDiscriminator,
        string safeMetadataHash)
    {
        return string.Join(
            "|",
            [
                "message-surface/v1",
                sourceLabel,
                language,
                surfaceKind,
                frameworkFamily,
                operationDirection,
                operationKind,
                destinationIdentityStatus,
                normalizedDestinationKey ?? destinationHash ?? eventTypeIdentity ?? string.Empty,
                occurrenceDiscriminator ?? string.Empty,
                safeMetadataHash
            ]);
    }

    public static string OccurrenceDiscriminator(string filePath, int startLine, int endLine, string ruleId, string safeMetadataHash)
    {
        return Sha256($"{filePath}|{startLine}|{endLine}|{ruleId}|{safeMetadataHash}")[..16];
    }

    public static string Sha256(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string NormalizeRenderable(string value)
    {
        return value.Trim().Replace('\\', '/').ToLowerInvariant();
    }

    private static bool IsDynamic(string value)
    {
        return value.Contains("${", StringComparison.Ordinal)
            || value.Contains("$(", StringComparison.Ordinal)
            || value.Contains("%", StringComparison.Ordinal)
            || value.Contains("{", StringComparison.Ordinal)
            || value.Contains("}", StringComparison.Ordinal)
            || value.Contains("?", StringComparison.Ordinal);
    }

    private static bool IsWildcardPattern(string value)
    {
        return value.Contains("*", StringComparison.Ordinal)
            || value.Contains("#", StringComparison.Ordinal);
    }

    private static bool ContainsUnsafeValue(string value)
    {
        return value.Contains("://", StringComparison.Ordinal)
            || value.Contains('@', StringComparison.Ordinal)
            || value.Contains('\\', StringComparison.Ordinal)
            || value.StartsWith("/", StringComparison.Ordinal)
            || value.StartsWith("C:", StringComparison.OrdinalIgnoreCase)
            || value.Contains("password", StringComparison.OrdinalIgnoreCase)
            || value.Contains("secret", StringComparison.OrdinalIgnoreCase)
            || value.Contains("token", StringComparison.OrdinalIgnoreCase)
            || value.Contains("key=", StringComparison.OrdinalIgnoreCase);
    }
}

public sealed record MessageDestinationIdentity(
    string Status,
    string? NormalizedDestinationKey,
    string? DestinationHash,
    string? GapReason);
