using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace TraceMap.Core;

internal sealed record LegacyDataXmlDocument(XDocument Document, string DocumentHash);

internal static class LegacyDataXml
{
    public const long MaxXmlBytes = 2 * 1024 * 1024;
    public const int MaxXmlNodes = 75_000;

    public static LegacyDataXmlDocument Load(string fullPath)
    {
        var info = new FileInfo(fullPath);
        if (info.Length > MaxXmlBytes)
        {
            throw new LegacyDataXmlException("LegacyDataMetadataTooLarge", "metadata document exceeds configured size bound");
        }

        string text;
        try
        {
            text = File.ReadAllText(fullPath);
        }
        catch (DecoderFallbackException ex)
        {
            throw new LegacyDataXmlException("MalformedLegacyDataMetadata", ex.Message, ex);
        }

        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = MaxXmlBytes,
            MaxCharactersFromEntities = 0
        };

        try
        {
            using var stringReader = new StringReader(text);
            using var reader = XmlReader.Create(stringReader, settings);
            var document = XDocument.Load(reader, LoadOptions.SetLineInfo);
            if (document.DescendantNodes().Take(MaxXmlNodes + 1).Count() > MaxXmlNodes)
            {
                throw new LegacyDataXmlException("LegacyDataMetadataTooLarge", "metadata document exceeds configured node-count bound");
            }

            return new LegacyDataXmlDocument(document, FactFactory.Hash(text, 32));
        }
        catch (LegacyDataXmlException)
        {
            throw;
        }
        catch (XmlException ex) when (ex.Message.Contains("DTD", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("entity", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("XmlResolver", StringComparison.OrdinalIgnoreCase))
        {
            throw new LegacyDataXmlException("LegacyDataParserSecurityRejected", ex.Message, ex);
        }
        catch (XmlException ex)
        {
            throw new LegacyDataXmlException("MalformedLegacyDataMetadata", ex.Message, ex);
        }
    }
}

internal sealed class LegacyDataXmlException(string classification, string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    public string Classification { get; } = classification;
}

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

    public static string? SafeIdentifier(string? value)
    {
        return IsSafeIdentifier(value) ? value!.Trim() : null;
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

    public static bool IsUnsafeValue(string value)
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

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_.$]*$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeIdentifierPattern();
}
