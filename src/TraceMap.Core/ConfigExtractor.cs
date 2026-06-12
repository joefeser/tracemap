using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

namespace TraceMap.Core;

public static class ConfigExtractor
{
    public static IReadOnlyList<CodeFact> Extract(string repoPath, ScanManifest manifest, IEnumerable<FileInventoryItem> inventory)
    {
        var facts = new List<CodeFact>();
        foreach (var file in inventory
            .Where(IsConfigCandidate)
            .OrderBy(item => item.RelativePath, StringComparer.Ordinal))
        {
            var fullPath = Path.Combine(repoPath, file.RelativePath);
            try
            {
                if (IsAppSettingsJson(file.RelativePath))
                {
                    AddJsonConfigFacts(manifest, facts, file.RelativePath, File.ReadAllText(fullPath));
                }
                else if (IsXmlConfig(file.RelativePath))
                {
                    AddXmlConfigFacts(manifest, facts, file.RelativePath, fullPath);
                }
                else if (Path.GetFileName(file.RelativePath).Equals("packages.config", StringComparison.OrdinalIgnoreCase))
                {
                    AddPackagesConfigFacts(manifest, facts, file.RelativePath, fullPath);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or XmlException)
            {
                facts.Add(FactFactory.Create(
                    manifest,
                    FactTypes.AnalysisGap,
                    RuleIds.ConfigKey,
                    EvidenceTiers.Tier4Unknown,
                    new EvidenceSpan(file.RelativePath, 1, 1, null, "ConfigExtractor", ScannerVersions.ConfigExtractor),
                    properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["message"] = $"Unable to parse config file: {ex.Message}"
                    }));
            }
        }

        return facts;
    }

    private static void AddJsonConfigFacts(ScanManifest manifest, List<CodeFact> facts, string relativePath, string text)
    {
        using var document = JsonDocument.Parse(text, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        AddJsonElementFacts(manifest, facts, relativePath, text, document.RootElement, []);
    }

    private static void AddJsonElementFacts(
        ScanManifest manifest,
        List<CodeFact> facts,
        string relativePath,
        string text,
        JsonElement element,
        IReadOnlyList<string> pathParts)
    {
        foreach (var property in element.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal))
        {
            var nextPath = pathParts.Concat([property.Name]).ToArray();
            var keyPath = string.Join(":", nextPath);
            var line = FindLineForJsonProperty(text, property.Name);
            facts.Add(FactFactory.Create(
                manifest,
                FactTypes.ConfigKeyDeclared,
                RuleIds.ConfigKey,
                EvidenceTiers.Tier2Structural,
                new EvidenceSpan(relativePath, line, line, null, "ConfigExtractor", ScannerVersions.ConfigExtractor),
                targetSymbol: keyPath,
                contractElement: keyPath,
                properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["keyPath"] = keyPath,
                    ["sourceFormat"] = "Json",
                    ["valueKind"] = property.Value.ValueKind.ToString()
                }));

            if (IsJsonConnectionString(nextPath, property.Value, out var connectionName, out var connectionValue))
            {
                facts.Add(FactFactory.Create(
                    manifest,
                    FactTypes.ConnectionStringDeclared,
                    RuleIds.ConfigKey,
                    EvidenceTiers.Tier2Structural,
                    new EvidenceSpan(relativePath, line, line, null, "ConfigExtractor", ScannerVersions.ConfigExtractor),
                    targetSymbol: connectionName,
                    contractElement: connectionName,
                    properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["connectionName"] = connectionName,
                        ["sourceFormat"] = "Json",
                        ["valueHash"] = FactFactory.Hash(connectionValue, 32)
                    }));
            }

            if (property.Value.ValueKind == JsonValueKind.Object)
            {
                AddJsonElementFacts(manifest, facts, relativePath, text, property.Value, nextPath);
            }
        }
    }

    private static void AddXmlConfigFacts(ScanManifest manifest, List<CodeFact> facts, string relativePath, string fullPath)
    {
        var document = XDocument.Load(fullPath, LoadOptions.SetLineInfo);
        foreach (var add in document.Descendants()
            .Where(element => element.Name.LocalName == "add")
            .OrderBy(GetLine)
            .ThenBy(element => AttributeValue(element, "key") ?? AttributeValue(element, "name"), StringComparer.Ordinal))
        {
            var parentName = add.Parent?.Name.LocalName ?? string.Empty;
            if (parentName.Equals("appSettings", StringComparison.OrdinalIgnoreCase))
            {
                var key = AttributeValue(add, "key");
                if (string.IsNullOrWhiteSpace(key))
                {
                    continue;
                }

                var keyPath = $"appSettings:{key}";
                facts.Add(FactFactory.Create(
                    manifest,
                    FactTypes.ConfigKeyDeclared,
                    RuleIds.ConfigKey,
                    EvidenceTiers.Tier2Structural,
                    new EvidenceSpan(relativePath, GetLine(add), GetLine(add), null, "ConfigExtractor", ScannerVersions.ConfigExtractor),
                    targetSymbol: keyPath,
                    contractElement: keyPath,
                    properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["keyPath"] = keyPath,
                        ["sourceFormat"] = "Xml"
                    }));
            }
            else if (parentName.Equals("connectionStrings", StringComparison.OrdinalIgnoreCase))
            {
                var name = AttributeValue(add, "name");
                var connectionString = AttributeValue(add, "connectionString");
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                facts.Add(FactFactory.Create(
                    manifest,
                    FactTypes.ConnectionStringDeclared,
                    RuleIds.ConfigKey,
                    EvidenceTiers.Tier2Structural,
                    new EvidenceSpan(relativePath, GetLine(add), GetLine(add), null, "ConfigExtractor", ScannerVersions.ConfigExtractor),
                    targetSymbol: name,
                    contractElement: name,
                    properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["connectionName"] = name,
                        ["providerName"] = AttributeValue(add, "providerName") ?? string.Empty,
                        ["sourceFormat"] = "Xml",
                        ["valueHash"] = string.IsNullOrWhiteSpace(connectionString) ? string.Empty : FactFactory.Hash(connectionString, 32)
                    }));
            }
        }
    }

    private static void AddPackagesConfigFacts(ScanManifest manifest, List<CodeFact> facts, string relativePath, string fullPath)
    {
        var document = XDocument.Load(fullPath, LoadOptions.SetLineInfo);
        foreach (var package in document.Descendants()
            .Where(element => element.Name.LocalName == "package")
            .OrderBy(GetLine)
            .ThenBy(element => AttributeValue(element, "id"), StringComparer.Ordinal))
        {
            var id = AttributeValue(package, "id");
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            var keyPath = $"packages:{id}";
            facts.Add(FactFactory.Create(
                manifest,
                FactTypes.ConfigKeyDeclared,
                RuleIds.ConfigKey,
                EvidenceTiers.Tier2Structural,
                new EvidenceSpan(relativePath, GetLine(package), GetLine(package), null, "ConfigExtractor", ScannerVersions.ConfigExtractor),
                targetSymbol: keyPath,
                contractElement: keyPath,
                properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["keyPath"] = keyPath,
                    ["package"] = id,
                    ["sourceFormat"] = "PackagesConfig",
                    ["version"] = AttributeValue(package, "version") ?? string.Empty
                }));
        }
    }

    private static bool IsConfigCandidate(FileInventoryItem item)
    {
        return item.Kind == "Json" || item.Kind == "Config" || item.Kind == "PackagesConfig";
    }

    private static bool IsAppSettingsJson(string relativePath)
    {
        var fileName = Path.GetFileName(relativePath);
        return fileName.StartsWith("appsettings", StringComparison.OrdinalIgnoreCase)
            && fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsXmlConfig(string relativePath)
    {
        var fileName = Path.GetFileName(relativePath);
        return fileName.Equals("Web.config", StringComparison.OrdinalIgnoreCase)
            || fileName.Equals("App.config", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsJsonConnectionString(IReadOnlyList<string> pathParts, JsonElement value, out string connectionName, out string connectionValue)
    {
        connectionName = string.Empty;
        connectionValue = string.Empty;
        if (pathParts.Count != 2
            || !pathParts[0].Equals("ConnectionStrings", StringComparison.OrdinalIgnoreCase)
            || value.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        connectionName = pathParts[1];
        connectionValue = value.GetString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(connectionName);
    }

    private static int FindLineForJsonProperty(string text, string propertyName)
    {
        var escapedName = JsonEncodedText.Encode(propertyName).ToString();
        var index = text.IndexOf($"\"{escapedName}\"", StringComparison.Ordinal);
        if (index < 0)
        {
            return 1;
        }

        var line = 1;
        for (var i = 0; i < index; i++)
        {
            if (text[i] == '\n')
            {
                line++;
            }
        }

        return line;
    }

    private static string? AttributeValue(XElement element, string name)
    {
        return element.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == name)?.Value.Trim();
    }

    private static int GetLine(XObject node)
    {
        return node is IXmlLineInfo lineInfo && lineInfo.HasLineInfo()
            ? Math.Max(1, lineInfo.LineNumber)
            : 1;
    }
}
