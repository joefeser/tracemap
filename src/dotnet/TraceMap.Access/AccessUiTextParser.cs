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
        var reportGroups = new List<AccessRawReportGroup>();
        var blocks = new Stack<string>();
        ControlBuilder? control = null;
        var controlDepth = -1;
        ReportGroupBuilder? reportGroup = null;
        var reportGroupDepth = -1;
        var reportGroupContainerDepth = -1;
        string? recordSource = null;
        string? filter = null;
        string? orderBy = null;
        bool? hasModule = null;
        long lineCount = 0;
        long characterCount = 0;
        var sawSurfaceBlock = false;
        string? pendingProperty = null;

        string? line;
        string? bufferedLine = null;
        while ((line = bufferedLine ?? reader.ReadLine()) is not null)
        {
            bufferedLine = null;
            lineCount++;
            characterCount += line.Length + 1L;
            if (lineCount > limits.MaxUiDesignLines || characterCount > limits.MaxUiDesignTextLength)
                throw new AccessScanException("AccessUiDesignTextLimitReached");
            var quotedProperty = PropertyPattern().Match(line);
            if (quotedProperty.Success && quotedProperty.Groups["value"].Value.TrimStart().StartsWith('"'))
            {
                while (reader.ReadLine() is { } continuation)
                {
                    if (!continuation.TrimStart().StartsWith('"'))
                    {
                        bufferedLine = continuation;
                        break;
                    }
                    lineCount++;
                    characterCount += continuation.Length + 1L;
                    if (lineCount > limits.MaxUiDesignLines || characterCount > limits.MaxUiDesignTextLength)
                        throw new AccessScanException("AccessUiDesignTextLimitReached");
                    line += "\n" + continuation;
                }
            }
            if (pendingProperty is not null)
            {
                pendingProperty += "\n" + line;
                var pendingValue = pendingProperty[(pendingProperty.IndexOf('=') + 1)..];
                if (!IsQuotedScalarComplete(pendingValue))
                    continue;
                line = pendingProperty;
                pendingProperty = null;
            }
            var trimmed = line.Trim();
            if (blocks.Count == 0 && (trimmed is "CodeBehindForm" or "CodeBehindReport")) break;

            var begin = BeginPattern().Match(trimmed);
            if (begin.Success)
            {
                var block = begin.Groups["block"].Success ? begin.Groups["block"].Value : "anonymous";
                blocks.Push(block);
                if (blocks.Skip(1).Any(item => item == "property-opaque"))
                    continue;
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
                if (reportGroup is not null && blocks.Count == reportGroupDepth)
                {
                    reportGroups.Add(reportGroup.Build());
                    reportGroup = null;
                    reportGroupDepth = -1;
                }
                if (blocks.Count == reportGroupContainerDepth)
                    reportGroupContainerDepth = -1;
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

            var indexedGroup = IndexedGroupPattern().Match(line);
            if (surfaceKind == "report"
                && control is null
                && reportGroup is null
                && reportGroupContainerDepth == blocks.Count
                && indexedGroup.Success)
            {
                blocks.Push("report-group");
                var ordinal = ReadBoundedNonNegativeInt(indexedGroup.Groups["ordinal"].Value);
                if (ordinal is null)
                {
                    gaps.Add(new("AccessUiDesignTextMalformed", "ui-surface", null, RuleIds.LegacyAccessUiSurface));
                }
                else
                {
                    reportGroup = new(ordinal.Value);
                    reportGroupDepth = blocks.Count;
                }
                continue;
            }

            var property = PropertyPattern().Match(line);
            if (!property.Success) continue;
            if (blocks.Contains("property-opaque")) continue;
            var name = property.Groups["name"].Value;
            var sourceValue = property.Groups["value"].Value;
            if (sourceValue.TrimStart().StartsWith('"') && !IsQuotedScalarComplete(sourceValue))
            {
                pendingProperty = line;
                continue;
            }
            if (sourceValue.TrimStart().Contains('{')
                && sourceValue.TrimEnd().EndsWith("Begin", StringComparison.OrdinalIgnoreCase))
            {
                blocks.Push("property-opaque");
                gaps.Add(new(ProtectedPropertyNames.Contains(name)
                    ? "AccessUiProtectedPropertyShapeUnsupported"
                    : "AccessUiCompoundPropertyShapeUnsupported",
                    control is null ? "ui-surface" : "control", null, RuleIds.LegacyAccessUiSurface));
                continue;
            }
            if (sourceValue.Trim().Equals("Begin", StringComparison.Ordinal))
            {
                blocks.Push("property-value");
                if (surfaceKind == "report" && control is null && name == "GroupLevel")
                {
                    reportGroupContainerDepth = blocks.Count;
                }
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

            if (surfaceKind == "report"
                && control is null
                && reportGroup is null
                && reportGroupContainerDepth == blocks.Count
                && ReportGroupPropertyNames.Contains(name))
            {
                reportGroup = new(reportGroups.Count);
                reportGroupDepth = blocks.Count;
            }

            if (control is not null)
            {
                if (blocks.Count != controlDepth) continue;
                switch (name)
                {
                    case "Name": control.Name = value; break;
                    case "ControlSource": control.ControlSource = value; break;
                    case "RowSource": control.RowSource = value; break;
                    case "RowSourceType": control.RowSourceType = value; break;
                    case "ValidationRule": control.ValidationRule = value; break;
                    case "BoundColumn": control.BoundColumn = ReadBoundedNonNegativeInt(value); break;
                    case "ColumnCount": control.ColumnCount = ReadBoundedNonNegativeInt(value); break;
                    case "SourceObject": control.SourceObject = value; break;
                    case "LinkMasterFields": control.LinkMasterFields = value; break;
                    case "LinkChildFields": control.LinkChildFields = value; break;
                    default:
                        if (EventRoles.TryGetValue(name, out var controlEventRole))
                            control.Events.Add(new(controlEventRole, value));
                        break;
                }
                continue;
            }

            if (reportGroup is not null)
            {
                if (blocks.Count != reportGroupDepth) continue;
                switch (name)
                {
                    case "ControlSource":
                    case "Expression":
                        reportGroup.Expression = value;
                        break;
                    case "SortOrder": reportGroup.SortOrder = value; break;
                    case "GroupOn": reportGroup.GroupOn = value; break;
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

        if (pendingProperty is not null)
            gaps.Add(new("AccessUiPropertyValueMalformed", control is null ? "ui-surface" : "control", null, RuleIds.LegacyAccessUiSurface));

        var malformed = !sawSurfaceBlock || blocks.Count != 0
            || gaps.Any(item => item.Classification is "AccessUiDesignTextMalformed"
                or "AccessUiProtectedPropertyShapeUnsupported"
                or "AccessUiCompoundPropertyShapeUnsupported"
                or "AccessUiPropertyValueMalformed");
        if (!sawSurfaceBlock || blocks.Count != 0)
            gaps.Add(new("AccessUiDesignTextMalformed", "ui-surface", null, RuleIds.LegacyAccessUiSurface));
        return new(
            new(surfaceName, surfaceKind, hasModule, recordSource,
                controls.OrderBy(item => item.Ordinal).ToArray(),
                surfaceEvents.OrderBy(item => item.Role, StringComparer.Ordinal).ToArray(),
                Coverage: malformed ? "partial" : "complete",
                Filter: filter,
                OrderBy: orderBy,
                ReportGroups: reportGroups.OrderBy(item => item.Ordinal).ToArray()),
            gaps.OrderBy(item => item.Classification, StringComparer.Ordinal).ToArray());
    }

    private static string ReadScalar(string source, out bool supported)
    {
        var parsed = ParseScalar(source);
        supported = parsed.Supported;
        return parsed.Value;
    }

    private static bool IsQuotedScalarComplete(string source) => ParseScalar(source).Complete;

    private static ScalarParseResult ParseScalar(string source)
    {
        var value = source.Trim();
        if (value.Length == 0) return new(string.Empty, true, true);
        if (value[0] != '"') return new(value, true, true);
        var builder = new StringBuilder(value.Length);
        var index = 1;
        while (index < value.Length)
        {
            var current = value[index];
            if (current == '\\' && IsSaveAsTextLineBreakPair(value, index))
            {
                builder.Append("\r\n");
                index += 8;
                continue;
            }
            if (current == '\\' && index + 1 < value.Length && value[index + 1] == '"')
            {
                builder.Append('"');
                index += 2;
                continue;
            }
            if (current != '"') { builder.Append(current); index++; continue; }
            if (index + 1 < value.Length && value[index + 1] == '"')
            {
                builder.Append('"');
                index += 2;
                continue;
            }
            index++;
            while (index < value.Length && char.IsWhiteSpace(value[index])) index++;
            if (index == value.Length) return new(builder.ToString(), true, true);
            if (value[index] != '"') return new(string.Empty, false, false);
            index++;
        }
        return new(string.Empty, false, false);
    }

    private static bool IsSaveAsTextLineBreakPair(string value, int index) =>
        index + 8 <= value.Length
        && value.AsSpan(index, 8).SequenceEqual("\\015\\012");

    private readonly record struct ScalarParseResult(string Value, bool Supported, bool Complete);

    private static bool? ReadBoolean(string value) => value.Trim() switch
    {
        "-1" or "True" or "true" or "NotDefault" => true,
        "0" or "False" or "false" => false,
        _ => null
    };

    private static int? ReadBoundedNonNegativeInt(string value) =>
        int.TryParse(value.Trim(), out var parsed) && parsed is >= 0 and <= 10_000 ? parsed : null;

    private static bool TryControlType(string block, out int value)
        => ControlTypes.TryGetValue(block, out value);

    internal static bool IsSupportedControlType(int value) => SupportedControlTypes.Contains(value);

    private sealed class ControlBuilder(int controlType, int ordinal)
    {
        public string? Name { get; set; }
        public string? ControlSource { get; set; }
        public string? RowSource { get; set; }
        public string? ValidationRule { get; set; }
        public string? RowSourceType { get; set; }
        public int? BoundColumn { get; set; }
        public int? ColumnCount { get; set; }
        public string? SourceObject { get; set; }
        public string? LinkMasterFields { get; set; }
        public string? LinkChildFields { get; set; }
        public List<AccessRawUiEvent> Events { get; } = [];

        public AccessRawControl Build() => new(
            Name!, ordinal, controlType, ControlSource, RowSource,
            Events.OrderBy(item => item.Role, StringComparer.Ordinal).ToArray(), ValidationRule,
            RowSourceType, BoundColumn, ColumnCount, SourceObject, LinkMasterFields, LinkChildFields);
    }

    private sealed class ReportGroupBuilder(int ordinal)
    {
        public string? Expression { get; set; }
        public string? SortOrder { get; set; }
        public string? GroupOn { get; set; }
        public AccessRawReportGroup Build() => new(ordinal, Expression, SortOrder, GroupOn);
    }

    private static readonly Dictionary<string, string> EventRoles = new(StringComparer.Ordinal)
    {
        ["AfterUpdate"] = "after-update",
        ["OnActivate"] = "on-activate",
        ["BeforeUpdate"] = "before-update",
        ["OnClick"] = "on-click",
        ["OnClose"] = "on-close",
        ["OnCurrent"] = "on-current",
        ["OnDeactivate"] = "on-deactivate",
        ["OnDblClick"] = "on-dbl-click",
        ["OnError"] = "on-error",
        ["OnLoad"] = "on-load",
        ["OnNoData"] = "on-no-data",
        ["OnOpen"] = "on-open",
        ["OnResize"] = "on-resize",
        ["OnTimer"] = "on-timer",
        ["OnUnload"] = "on-unload"
    };

    private static readonly IReadOnlyDictionary<string, int> ControlTypes =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["label"] = 100,
            ["rectangle"] = 101,
            ["line"] = 102,
            ["image"] = 103,
            ["commandbutton"] = 104,
            ["optionbutton"] = 105,
            ["checkbox"] = 106,
            ["optiongroup"] = 107,
            ["boundobjectframe"] = 108,
            ["textbox"] = 109,
            ["listbox"] = 110,
            ["combobox"] = 111,
            ["subform"] = 112,
            ["subreport"] = 112,
            ["objectframe"] = 114,
            ["pagebreak"] = 118,
            ["togglebutton"] = 122,
            ["tabcontrol"] = 123,
            ["page"] = 124,
            ["attachment"] = 126,
            ["webbrowsercontrol"] = 128,
            ["navigationcontrol"] = 129,
            ["navigationbutton"] = 130
        };

    private static readonly IReadOnlySet<int> SupportedControlTypes =
        ControlTypes.Values.ToHashSet();

    private static readonly HashSet<string> ProtectedPropertyNames = new(StringComparer.Ordinal)
    {
        "ControlSource", "Filter", "OrderBy", "RecordSource", "RowSource", "ValidationRule",
        "SourceObject", "LinkMasterFields", "LinkChildFields", "Expression"
    };

    private static readonly HashSet<string> ReportGroupPropertyNames = new(StringComparer.Ordinal)
    {
        "ControlSource", "Expression", "SortOrder", "GroupOn"
    };

    [GeneratedRegex(@"^Begin(?:\s+(?<block>[A-Za-z][A-Za-z0-9]*))?$", RegexOptions.CultureInvariant)]
    private static partial Regex BeginPattern();

    [GeneratedRegex(@"^\s*(?<ordinal>[0-9]+)\s*=\s*Begin\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex IndexedGroupPattern();

    [GeneratedRegex(@"^\s*(?<name>[A-Za-z][A-Za-z0-9]*)\s*=\s*(?<value>.*)$", RegexOptions.CultureInvariant | RegexOptions.Singleline)]
    private static partial Regex PropertyPattern();
}
