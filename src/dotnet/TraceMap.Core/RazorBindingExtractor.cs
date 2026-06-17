using System.Text.RegularExpressions;

namespace TraceMap.Core;

public static class RazorBindingExtractor
{
    private static readonly Regex AspForRegex = new("""<(?<element>[a-zA-Z][\w:-]*)\b[^>]*\basp-for\s*=\s*["'](?<expr>[^"']+)["']""", RegexOptions.Compiled);
    private static readonly Regex HtmlForRegex = new("""Html\.(?<helper>TextBoxFor|EditorFor|DisplayFor|LabelFor|ValidationMessageFor|HiddenFor|CheckBoxFor|DropDownListFor)\s*\(\s*(?<expr>[^,\)]+)""", RegexOptions.Compiled);
    private static readonly Regex FormRegex = new("""<form\b(?<attrs>[^>]*)>""", RegexOptions.Compiled);
    private static readonly Regex AttributeRegex = new("""(?<name>asp-action|asp-controller|asp-page|asp-page-handler|method)\s*=\s*["'](?<value>[^"']+)["']""", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static IReadOnlyList<CodeFact> Extract(string repoPath, ScanManifest manifest, IReadOnlyList<FileInventoryItem> inventory)
    {
        var facts = new List<CodeFact>();
        foreach (var item in inventory.Where(item => item.Kind == "Razor").OrderBy(item => item.RelativePath, StringComparer.Ordinal))
        {
            var fullPath = Path.Combine(repoPath, item.RelativePath);
            if (!File.Exists(fullPath))
            {
                continue;
            }

            var text = File.ReadAllText(fullPath);
            var lineStarts = LineStarts(text);
            var modelType = ExtractModelType(text);
            foreach (Match match in AspForRegex.Matches(text))
            {
                var expression = match.Groups["expr"].Value.Trim();
                if (!IsStaticModelExpression(expression))
                {
                    facts.Add(Gap(manifest, item.RelativePath, LineFor(lineStarts, match.Index), "dynamic-asp-for", "asp-for expression was not a static model property path."));
                    continue;
                }

                var propertyPath = NormalizeModelExpression(expression);
                var properties = new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["uiFramework"] = "razor",
                    ["bindingKind"] = "asp-for",
                    ["controlKind"] = SafeElementKind(match.Groups["element"].Value),
                    ["propertyPath"] = propertyPath,
                    ["propertyName"] = LastSegment(propertyPath),
                    ["valueStored"] = "safe-metadata-only"
                };
                AddModelType(properties, modelType);
                facts.Add(FactFactory.Create(
                    manifest,
                    FactTypes.RazorBinding,
                    RuleIds.RazorBinding,
                    EvidenceTiers.Tier2Structural,
                    Span(item.RelativePath, lineStarts, match.Index, match.Length),
                    targetSymbol: propertyPath,
                    contractElement: LastSegment(propertyPath),
                    properties: properties));
            }

            foreach (Match match in HtmlForRegex.Matches(text))
            {
                var expression = match.Groups["expr"].Value.Trim();
                var propertyPath = ExtractLambdaProperty(expression);
                if (propertyPath is null)
                {
                    facts.Add(Gap(manifest, item.RelativePath, LineFor(lineStarts, match.Index), "dynamic-html-for", "Html.*For expression was not a static model property path."));
                    continue;
                }

                var properties = new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["uiFramework"] = "razor",
                    ["bindingKind"] = "html-for",
                    ["controlKind"] = match.Groups["helper"].Value,
                    ["propertyPath"] = propertyPath,
                    ["propertyName"] = LastSegment(propertyPath),
                    ["valueStored"] = "safe-metadata-only"
                };
                AddModelType(properties, modelType);
                facts.Add(FactFactory.Create(
                    manifest,
                    FactTypes.RazorBinding,
                    RuleIds.RazorBinding,
                    EvidenceTiers.Tier2Structural,
                    Span(item.RelativePath, lineStarts, match.Index, match.Length),
                    targetSymbol: propertyPath,
                    contractElement: LastSegment(propertyPath),
                    properties: properties));
            }

            foreach (Match match in FormRegex.Matches(text))
            {
                var attrs = ParseAttributes(match.Groups["attrs"].Value);
                if (attrs.Count == 0)
                {
                    continue;
                }

                var method = attrs.TryGetValue("method", out var methodValue) ? methodValue.ToUpperInvariant() : "GET";
                var properties = new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["uiFramework"] = "razor",
                    ["bindingKind"] = "form-action",
                    ["controlKind"] = "form",
                    ["httpMethod"] = method,
                    ["valueStored"] = "safe-metadata-only"
                };
                Copy(attrs, "asp-action", properties, "actionName");
                Copy(attrs, "asp-controller", properties, "controllerName");
                Copy(attrs, "asp-page", properties, "pagePathHash", hash: true);
                Copy(attrs, "asp-page-handler", properties, "handlerName");
                facts.Add(FactFactory.Create(
                    manifest,
                    FactTypes.RazorFormTarget,
                    RuleIds.RazorFormTarget,
                    EvidenceTiers.Tier2Structural,
                    Span(item.RelativePath, lineStarts, match.Index, match.Length),
                    targetSymbol: properties.TryGetValue("actionName", out var action) ? action : properties.GetValueOrDefault("handlerName"),
                    contractElement: method,
                    properties: properties));
            }

            foreach (var dynamicLine in DynamicRazorLines(text, lineStarts))
            {
                facts.Add(Gap(manifest, item.RelativePath, dynamicLine.Line, dynamicLine.Kind, dynamicLine.Message));
            }
        }

        return facts
            .OrderBy(fact => fact.Evidence.FilePath, StringComparer.Ordinal)
            .ThenBy(fact => fact.Evidence.StartLine)
            .ThenBy(fact => fact.FactType, StringComparer.Ordinal)
            .ThenBy(fact => fact.FactId, StringComparer.Ordinal)
            .ToArray();
    }

    private static CodeFact Gap(ScanManifest manifest, string filePath, int line, string kind, string message)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.RazorBindingGap,
            RuleIds.RazorBindingGap,
            EvidenceTiers.Tier4Unknown,
            new EvidenceSpan(filePath, line, line, null, "RazorBindingExtractor", ScannerVersions.RazorBindingExtractor),
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["uiFramework"] = "razor",
                ["gapKind"] = kind,
                ["message"] = message,
                ["valueStored"] = "safe-metadata-only"
            });
    }

    private static EvidenceSpan Span(string filePath, IReadOnlyList<int> lineStarts, int index, int length)
    {
        var start = LineFor(lineStarts, index);
        var end = LineFor(lineStarts, index + length);
        return new EvidenceSpan(filePath, start, end, null, "RazorBindingExtractor", ScannerVersions.RazorBindingExtractor);
    }

    private static IReadOnlyList<int> LineStarts(string text)
    {
        var starts = new List<int> { 0 };
        for (var index = 0; index < text.Length; index++)
        {
            if (text[index] == '\n')
            {
                starts.Add(index + 1);
            }
        }

        return starts;
    }

    private static int LineFor(IReadOnlyList<int> lineStarts, int index)
    {
        var line = 0;
        for (var candidate = 0; candidate < lineStarts.Count; candidate++)
        {
            if (lineStarts[candidate] > index)
            {
                break;
            }

            line = candidate;
        }

        return line + 1;
    }

    private static bool IsStaticModelExpression(string value)
    {
        return Regex.IsMatch(value, @"^[A-Za-z_][\w]*(\.[A-Za-z_][\w]*)*$");
    }

    private static string NormalizeModelExpression(string value)
    {
        return value.Trim().TrimStart('@');
    }

    private static string? ExtractLambdaProperty(string expression)
    {
        var match = Regex.Match(expression, @"(?:\(?\s*[A-Za-z_][\w]*\s*\)?\s*=>\s*)?(?:[A-Za-z_][\w]*|Model)\.(?<path>[A-Za-z_][\w]*(?:\.[A-Za-z_][\w]*)*)");
        return match.Success ? match.Groups["path"].Value : null;
    }

    private static string? ExtractModelType(string text)
    {
        var match = Regex.Match(text, @"(?m)^\s*@model\s+(?<type>[A-Za-z_][\w.<>?,\s]*)\s*$");
        if (!match.Success)
        {
            return null;
        }

        var value = match.Groups["type"].Value.Trim();
        return value.Contains("://", StringComparison.Ordinal) ? null : value;
    }

    private static void AddModelType(SortedDictionary<string, string> properties, string? modelType)
    {
        if (!string.IsNullOrWhiteSpace(modelType))
        {
            properties["modelType"] = modelType;
        }
    }

    private static string LastSegment(string value)
    {
        var parts = value.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.LastOrDefault() ?? value;
    }

    private static string SafeElementKind(string value)
    {
        return Regex.IsMatch(value, @"^[A-Za-z][\w:-]*$") ? value.ToLowerInvariant() : "element";
    }

    private static SortedDictionary<string, string> ParseAttributes(string value)
    {
        var result = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (Match match in AttributeRegex.Matches(value))
        {
            var attr = match.Groups["name"].Value.ToLowerInvariant();
            var attrValue = match.Groups["value"].Value.Trim();
            if (!string.IsNullOrWhiteSpace(attrValue) && !attrValue.Contains("://", StringComparison.Ordinal))
            {
                result[attr] = attrValue;
            }
        }

        return result;
    }

    private static void Copy(IReadOnlyDictionary<string, string> attrs, string attrName, SortedDictionary<string, string> properties, string propertyName, bool hash = false)
    {
        if (!attrs.TryGetValue(attrName, out var value))
        {
            return;
        }

        properties[propertyName] = hash ? "hash-" + FactFactory.Hash(value, 16) : value;
    }

    private static IEnumerable<(int Line, string Kind, string Message)> DynamicRazorLines(string text, IReadOnlyList<int> lineStarts)
    {
        foreach (Match match in Regex.Matches(text, @"ViewBag\.|ViewData\s*\[|@model\s+dynamic|Html\.Partial|Html\.RenderPartial"))
        {
            yield return (LineFor(lineStarts, match.Index), "dynamic-razor-model", "Dynamic Razor model/view data/partial evidence prevents precise model-property lineage.");
        }
    }
}
