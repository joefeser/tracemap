using System.Text;
using System.Text.RegularExpressions;
using TraceMap.Core;

namespace TraceMap.Access;

internal sealed record AccessUiTextParseResult(
    AccessRawUiSurface? Surface,
    IReadOnlyList<AccessGapProjection> Gaps);

internal static partial class AccessUiTextParser
{
    public static AccessUiTextParseResult Parse(
        TextReader reader,
        string surfaceName,
        string surfaceKind,
        AccessLimits? limits = null)
    {
        limits ??= AccessLimits.Default;
        if (surfaceKind is not ("form" or "report"))
            return new(null, [new("AccessUiSurfaceKindUnsupported", "ui-surface", null, RuleIds.LegacyAccessUiSurface)]);

        var gaps = new List<AccessGapProjection>();
        var controls = new List<AccessRawControl>();
        var surfaceEvents = new List<AccessRawUiEvent>();
        var blocks = new Stack<string>();
        ControlBuilder? control = null;
        var controlDepth = -1;
        string? recordSource = null;
        string? filter = null;
        string? orderBy = null;
        bool? hasModule = null;
        long lineCount = 0;
        long characterCount = 0;
        var sawSurfaceBlock = false;

        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            lineCount++;
            characterCount += line.Length + 1L;
            if (lineCount > limits.MaxUiDesignLines || characterCount > limits.MaxUiDesignTextLength)
                throw new AccessScanException("AccessUiDesignTextLimitReached");
            var trimmed = line.Trim();
            if (trimmed is "CodeBehindForm" or "CodeBehindReport") break;

            var begin = BeginPattern().Match(trimmed);
            if (begin.Success)
            {
                var block = begin.Groups["block"].Success ? begin.Groups["block"].Value : "anonymous";
                blocks.Push(block);
                if (block.Equals(surfaceKind, StringComparison.OrdinalIgnoreCase)) sawSurfaceBlock = true;
                if (control is null && TryControlType(block, out var controlType))
                {
                    control = new(controlType, controls.Count);
                    controlDepth = blocks.Count;
                }
                continue;
            }
            if (trimmed == "End")
            {
                if (control is not null && blocks.Count == controlDepth)
                {
                    if (string.IsNullOrWhiteSpace(control.Name))
                        gaps.Add(new("AccessControlIdentityUnavailable", "control", null, RuleIds.LegacyAccessUiSurface));
                    else
                        controls.Add(control.Build());
                    control = null;
                    controlDepth = -1;
                }
                if (blocks.Count > 0) blocks.Pop();
                else gaps.Add(new("AccessUiDesignTextMalformed", "ui-surface", null, RuleIds.LegacyAccessUiSurface));
                continue;
            }

            var property = PropertyPattern().Match(line);
            if (!property.Success) continue;
            var name = property.Groups["name"].Value;
            var sourceValue = property.Groups["value"].Value;
            if (sourceValue.Trim().Equals("Begin", StringComparison.Ordinal))
            {
                blocks.Push("property-value");
                if (ProtectedPropertyNames.Contains(name))
                    gaps.Add(new("AccessUiProtectedPropertyShapeUnsupported", control is null ? "ui-surface" : "control", null, RuleIds.LegacyAccessUiSurface));
                continue;
            }
            var value = ReadScalar(sourceValue, out var supported);
            if (!supported)
            {
                if (ProtectedPropertyNames.Contains(name))
                    gaps.Add(new("AccessUiProtectedPropertyShapeUnsupported", control is null ? "ui-surface" : "control", null, RuleIds.LegacyAccessUiSurface));
                continue;
            }

            if (control is not null)
            {
                switch (name)
                {
                    case "Name": control.Name = value; break;
                    case "ControlSource": control.ControlSource = value; break;
                    case "RowSource": control.RowSource = value; break;
                    case "ValidationRule": control.ValidationRule = value; break;
                    default:
                        if (EventRoles.TryGetValue(name, out var controlEventRole))
                            control.Events.Add(new(controlEventRole, value));
                        break;
                }
                continue;
            }

            switch (name)
            {
                case "RecordSource": recordSource = value; break;
                case "Filter": filter = value; break;
                case "OrderBy": orderBy = value; break;
                case "HasModule": hasModule = ReadBoolean(value); break;
                default:
                    if (EventRoles.TryGetValue(name, out var surfaceEventRole))
                        surfaceEvents.Add(new(surfaceEventRole, value));
                    break;
            }
        }

        var malformed = !sawSurfaceBlock || blocks.Count != 0
            || gaps.Any(item => item.Classification == "AccessUiDesignTextMalformed");
        if (!sawSurfaceBlock || blocks.Count != 0)
            gaps.Add(new("AccessUiDesignTextMalformed", "ui-surface", null, RuleIds.LegacyAccessUiSurface));
        return new(
            new(surfaceName, surfaceKind, hasModule, recordSource,
                controls.OrderBy(item => item.Ordinal).ToArray(),
                surfaceEvents.OrderBy(item => item.Role, StringComparer.Ordinal).ToArray(),
                Coverage: malformed ? "partial" : "complete",
                Filter: filter,
                OrderBy: orderBy),
            gaps.OrderBy(item => item.Classification, StringComparer.Ordinal).ToArray());
    }

    private static string ReadScalar(string source, out bool supported)
    {
        var value = source.Trim();
        if (value.Length == 0) { supported = true; return string.Empty; }
        if (value[0] != '"') { supported = true; return value; }
        var builder = new StringBuilder(value.Length);
        for (var index = 1; index < value.Length; index++)
        {
            var current = value[index];
            if (current != '"') { builder.Append(current); continue; }
            if (index + 1 < value.Length && value[index + 1] == '"') { builder.Append('"'); index++; continue; }
            supported = string.IsNullOrWhiteSpace(value[(index + 1)..]);
            return supported ? builder.ToString() : string.Empty;
        }
        supported = false;
        return string.Empty;
    }

    private static bool? ReadBoolean(string value) => value.Trim() switch
    {
        "-1" or "True" or "true" or "NotDefault" => true,
        "0" or "False" or "false" => false,
        _ => null
    };

    private static bool TryControlType(string block, out int value)
    {
        value = block.ToLowerInvariant() switch
        {
            "label" => 100,
            "rectangle" => 101,
            "line" => 102,
            "image" => 103,
            "commandbutton" => 104,
            "optionbutton" => 105,
            "checkbox" => 106,
            "optiongroup" => 107,
            "boundobjectframe" => 108,
            "textbox" => 109,
            "listbox" => 110,
            "combobox" => 111,
            "subform" or "subreport" => 112,
            "objectframe" => 114,
            "pagebreak" => 118,
            "togglebutton" => 122,
            "tabcontrol" => 123,
            "page" => 124,
            "attachment" => 126,
            "webbrowsercontrol" => 128,
            "navigationcontrol" => 129,
            "navigationbutton" => 130,
            _ => -1
        };
        return value >= 0;
    }

    private sealed class ControlBuilder(int controlType, int ordinal)
    {
        public string? Name { get; set; }
        public string? ControlSource { get; set; }
        public string? RowSource { get; set; }
        public string? ValidationRule { get; set; }
        public List<AccessRawUiEvent> Events { get; } = [];

        public AccessRawControl Build() => new(
            Name!, ordinal, controlType, ControlSource, RowSource,
            Events.OrderBy(item => item.Role, StringComparer.Ordinal).ToArray(), ValidationRule);
    }

    private static readonly Dictionary<string, string> EventRoles = new(StringComparer.Ordinal)
    {
        ["AfterUpdate"] = "after-update",
        ["BeforeUpdate"] = "before-update",
        ["OnClick"] = "on-click",
        ["OnCurrent"] = "on-current",
        ["OnDblClick"] = "on-dbl-click",
        ["OnLoad"] = "on-load",
        ["OnNoData"] = "on-no-data",
        ["OnOpen"] = "on-open"
    };

    private static readonly HashSet<string> ProtectedPropertyNames = new(StringComparer.Ordinal)
    {
        "ControlSource", "Filter", "OrderBy", "RecordSource", "RowSource", "ValidationRule"
    };

    [GeneratedRegex(@"^Begin(?:\s+(?<block>[A-Za-z][A-Za-z0-9]*))?$", RegexOptions.CultureInvariant)]
    private static partial Regex BeginPattern();

    [GeneratedRegex(@"^\s*(?<name>[A-Za-z][A-Za-z0-9]*)\s*=\s*(?<value>.*)$", RegexOptions.CultureInvariant)]
    private static partial Regex PropertyPattern();
}
