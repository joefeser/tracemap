using System.Xml;
using System.Xml.Linq;

namespace TraceMap.Core;

internal enum SafeXmlFailureKind
{
    Malformed,
    SecurityRejected,
    TooLarge
}

internal sealed class SafeXmlException : Exception
{
    public SafeXmlException(SafeXmlFailureKind failureKind, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        FailureKind = failureKind;
    }

    public SafeXmlFailureKind FailureKind { get; }
}

internal static class SafeXml
{
    public const long MaxXmlBytes = 2 * 1024 * 1024;
    private const long MaxCharactersInDocument = 4 * 1024 * 1024;
    private const int MaxNodes = 100_000;
    private const int MaxDepth = 128;

    public static XDocument LoadDocument(string fullPath)
    {
        var info = new FileInfo(fullPath);
        if (info.Exists && info.Length > MaxXmlBytes)
        {
            throw new SafeXmlException(SafeXmlFailureKind.TooLarge, "XML metadata exceeds configured size bounds.");
        }

        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersFromEntities = 0,
            MaxCharactersInDocument = MaxCharactersInDocument
        };

        try
        {
            using var stream = File.OpenRead(fullPath);
            using var reader = XmlReader.Create(stream, settings);
            var document = XDocument.Load(reader, LoadOptions.SetLineInfo);
            ValidateBounds(document);
            return document;
        }
        catch (XmlException ex) when (IsSecurityException(ex))
        {
            throw new SafeXmlException(SafeXmlFailureKind.SecurityRejected, "XML metadata was rejected by safe parser settings.", ex);
        }
        catch (XmlException ex)
        {
            throw new SafeXmlException(SafeXmlFailureKind.Malformed, "XML metadata is malformed.", ex);
        }
    }

    private static void ValidateBounds(XDocument document)
    {
        var nodes = 0;
        foreach (var node in document.DescendantNodes())
        {
            nodes++;
            if (nodes > MaxNodes)
            {
                throw new SafeXmlException(SafeXmlFailureKind.TooLarge, "XML metadata exceeds configured node-count bounds.");
            }

            if (node is XElement element && Depth(element) > MaxDepth)
            {
                throw new SafeXmlException(SafeXmlFailureKind.TooLarge, "XML metadata exceeds configured depth bounds.");
            }
        }
    }

    private static int Depth(XElement element)
    {
        var depth = 0;
        for (var current = element; current is not null; current = current.Parent)
        {
            depth++;
        }

        return depth;
    }

    private static bool IsSecurityException(XmlException ex)
    {
        return ex.Message.Contains("DTD", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("entity", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("entities", StringComparison.OrdinalIgnoreCase);
    }
}
