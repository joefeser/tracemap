using System.Text;
using System.Text.RegularExpressions;
using TraceMap.Core;

namespace TraceMap.Access;

internal sealed record AccessRawUiEvent(string Role, string? Value);

internal sealed record AccessRawControl(
    string Name,
    int Ordinal,
    int ControlType,
    string? ControlSource,
    string? RowSource,
    IReadOnlyList<AccessRawUiEvent> Events);

internal sealed record AccessRawUiSurface(
    string Name,
    string SurfaceKind,
    bool? HasModule,
    string? RecordSource,
    IReadOnlyList<AccessRawControl> Controls,
    IReadOnlyList<AccessRawUiEvent> Events);

internal sealed record AccessUiProjectionResult(
    IReadOnlyList<AccessUiSurfaceProjection> Surfaces,
    IReadOnlyList<AccessGapProjection> Gaps);

internal static partial class AccessUiProjector
{
    public static AccessUiProjectionResult Project(
        string databaseIdentitySeed,
        IReadOnlyList<AccessRawUiSurface> rawSurfaces,
        IReadOnlyDictionary<string, IReadOnlyList<(string StableKey, string Kind)>> knownObjects,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>> fieldsByTable)
    {
        var surfaces = new List<AccessUiSurfaceProjection>();
        var gaps = new List<AccessGapProjection>();
        foreach (var raw in rawSurfaces.OrderBy(item => item.SurfaceKind, StringComparer.Ordinal)
                     .ThenBy(item => AccessSafeValues.RoleHash("access-ui-sort-name", item.Name), StringComparer.Ordinal))
        {
            var kind = raw.SurfaceKind is "form" or "report" ? raw.SurfaceKind : "unknown";
            var identity = AccessSafeValues.Identity(databaseIdentitySeed, kind, raw.Name);
            var bindings = new List<AccessBindingProjection>();
            var recordBinding = ProjectBinding(databaseIdentitySeed, identity.StableKey, "record-source", raw.RecordSource, 0, knownObjects, null, gaps);
            if (recordBinding is not null) bindings.Add(recordBinding);

            IReadOnlyDictionary<string, IReadOnlyList<string>>? scopedFields = null;
            if (recordBinding is { SourceKind: "direct-object", TargetStableKeys.Count: 1 }
                && fieldsByTable.TryGetValue(recordBinding.TargetStableKeys[0], out var fieldLookup))
                scopedFields = fieldLookup;

            var controls = new List<AccessControlProjection>();
            foreach (var rawControl in raw.Controls.OrderBy(item => item.Ordinal)
                         .ThenBy(item => AccessSafeValues.RoleHash("access-control-sort-name", item.Name), StringComparer.Ordinal))
            {
                var controlIdentity = AccessSafeValues.Identity(databaseIdentitySeed, $"control-{identity.StableKey}", rawControl.Name, rawControl.Ordinal);
                var controlBindings = new List<AccessBindingProjection>();
                var controlSource = ProjectBinding(databaseIdentitySeed, controlIdentity.StableKey, "control-source", rawControl.ControlSource,
                    rawControl.Ordinal, null, scopedFields, gaps);
                if (controlSource is not null) controlBindings.Add(controlSource);
                var rowSource = ProjectBinding(databaseIdentitySeed, controlIdentity.StableKey, "row-source", rawControl.RowSource,
                    rawControl.Ordinal, knownObjects, null, gaps);
                if (rowSource is not null) controlBindings.Add(rowSource);
                controls.Add(new(
                    controlIdentity,
                    identity.StableKey,
                    rawControl.Ordinal,
                    ControlType(rawControl.ControlType),
                    controlBindings.OrderBy(item => item.BindingKind, StringComparer.Ordinal).ToArray(),
                    ProjectEvents(rawControl.Events)));
            }

            var surfaceEvents = ProjectEvents(raw.Events);
            var designHash = DesignHash(identity, kind, raw.HasModule, raw.RecordSource, raw.Controls, raw.Events);
            surfaces.Add(new(
                identity,
                kind,
                raw.HasModule switch { true => "present", false => "absent", null => "unknown" },
                string.IsNullOrWhiteSpace(raw.RecordSource) ? "unbound" : "bound-declared",
                designHash,
                bindings,
                controls,
                surfaceEvents));
        }

        return new(
            surfaces.OrderBy(item => item.Identity.StableKey, StringComparer.Ordinal).ToArray(),
            gaps.OrderBy(item => item.Classification, StringComparer.Ordinal).ThenBy(item => item.StableScopeKey, StringComparer.Ordinal).ToArray());
    }

    public static IReadOnlyList<AccessUiEventProjection> ProjectEvents(IReadOnlyList<AccessRawUiEvent> events) =>
        events
            .Where(item => AllowedEventRoles.Contains(item.Role))
            .Select(item =>
            {
                var value = item.Value?.Trim() ?? string.Empty;
                var category = value switch
                {
                    "" => "none",
                    "[Event Procedure]" => "event-procedure",
                    "[Embedded Macro]" => "embedded-macro",
                    _ when DynamicEventPattern().IsMatch(value) => "dynamic",
                    _ when value.StartsWith('=') => "expression",
                    _ => "unknown"
                };
                var protectedValue = category is "expression" or "dynamic" or "unknown";
                return new AccessUiEventProjection(
                    item.Role,
                    category,
                    protectedValue ? AccessSafeValues.RoleHash($"access-event-{item.Role}", value) : null,
                    protectedValue ? value.Length : 0);
            })
            .OrderBy(item => item.EventRole, StringComparer.Ordinal)
            .ToArray();

    private static AccessBindingProjection? ProjectBinding(
        string databaseIdentitySeed,
        string ownerStableKey,
        string bindingKind,
        string? value,
        int ordinal,
        IReadOnlyDictionary<string, IReadOnlyList<(string StableKey, string Kind)>>? objects,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? fields,
        List<AccessGapProjection> gaps)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length == 0) return null;
        var identity = AccessSafeValues.Identity(databaseIdentitySeed, $"binding-{ownerStableKey}-{bindingKind}", bindingKind, ordinal);

        if (TryDirectName(trimmed, out var directName))
        {
            if (objects is not null && objects.TryGetValue(directName, out var objectCandidates))
            {
                if (objectCandidates.Count == 1)
                    return new(identity, ownerStableKey, bindingKind, "direct-object", null, 0,
                        [objectCandidates[0].StableKey], objectCandidates[0].Kind, "complete");
                return Ambiguous(identity, ownerStableKey, bindingKind, trimmed, "object", gaps);
            }
            if (fields is not null && fields.TryGetValue(directName, out var fieldCandidates))
            {
                if (fieldCandidates.Count == 1)
                    return new(identity, ownerStableKey, bindingKind, "direct-field", null, 0,
                        [fieldCandidates[0]], "field", "complete");
                return Ambiguous(identity, ownerStableKey, bindingKind, trimmed, "field", gaps);
            }

            gaps.Add(new("AccessBindingTargetUnresolved", "binding", identity.StableKey, RuleIds.LegacyAccessBinding));
            return new(identity, ownerStableKey, bindingKind, "unresolved-identifier",
                AccessSafeValues.RoleHash($"access-{bindingKind}-expression", trimmed), trimmed.Length, [], "unknown", "partial");
        }

        var candidates = ResolveExpressionCandidates(trimmed, objects, fields, out var ambiguous);
        gaps.Add(new(ambiguous ? "AccessBindingTargetAmbiguous" : "AccessBindingExpressionPartial", "binding", identity.StableKey, RuleIds.LegacyAccessBinding));
        return new(identity, ownerStableKey, bindingKind, "expression",
            AccessSafeValues.RoleHash($"access-{bindingKind}-expression", trimmed), trimmed.Length,
            candidates, fields is not null ? "field" : "object", "partial");
    }

    private static AccessBindingProjection Ambiguous(
        AccessSafeIdentity identity,
        string ownerStableKey,
        string bindingKind,
        string value,
        string targetKind,
        List<AccessGapProjection> gaps)
    {
        gaps.Add(new("AccessBindingTargetAmbiguous", "binding", identity.StableKey, RuleIds.LegacyAccessBinding));
        return new(identity, ownerStableKey, bindingKind, "ambiguous-identifier",
            AccessSafeValues.RoleHash($"access-{bindingKind}-expression", value), value.Length,
            [], targetKind, "partial");
    }

    private static IReadOnlyList<string> ResolveExpressionCandidates(
        string expression,
        IReadOnlyDictionary<string, IReadOnlyList<(string StableKey, string Kind)>>? objects,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? fields,
        out bool ambiguous)
    {
        ambiguous = false;
        var result = new SortedSet<string>(StringComparer.Ordinal);
        foreach (Match match in BracketedIdentifierPattern().Matches(MaskQuotedLiterals(expression)))
        {
            var name = match.Groups["name"].Value.Trim();
            if (fields is not null && fields.TryGetValue(name, out var fieldCandidates))
            {
                if (fieldCandidates.Count == 1) result.Add(fieldCandidates[0]);
                else if (fieldCandidates.Count > 1) ambiguous = true;
            }
            if (objects is not null && objects.TryGetValue(name, out var objectCandidates))
            {
                if (objectCandidates.Count == 1) result.Add(objectCandidates[0].StableKey);
                else if (objectCandidates.Count > 1) ambiguous = true;
            }
        }
        return result.ToArray();
    }

    private static bool TryDirectName(string value, out string name)
    {
        var match = DirectIdentifierPattern().Match(value);
        name = match.Success
            ? (match.Groups["bracketed"].Success ? match.Groups["bracketed"].Value : match.Groups["plain"].Value).Trim()
            : string.Empty;
        return match.Success;
    }

    private static string DesignHash(
        AccessSafeIdentity identity,
        string kind,
        bool? hasModule,
        string? recordSource,
        IReadOnlyList<AccessRawControl> controls,
        IReadOnlyList<AccessRawUiEvent> events)
    {
        var builder = new StringBuilder();
        builder.Append("access-ui-design/v1|").Append(identity.StableKey).Append('|').Append(kind).Append('|').Append(hasModule?.ToString() ?? "unknown");
        AppendProtected(builder, "record-source", recordSource);
        foreach (var control in controls.OrderBy(item => item.Ordinal).ThenBy(item => AccessSafeValues.RoleHash("access-control-sort-name", item.Name), StringComparer.Ordinal))
        {
            builder.Append('|').Append(control.Ordinal).Append(':').Append(control.ControlType).Append(':')
                .Append(AccessSafeValues.RoleHash("access-control-design-name", control.Name));
            AppendProtected(builder, "control-source", control.ControlSource);
            AppendProtected(builder, "row-source", control.RowSource);
            AppendEvents(builder, control.Events);
        }
        AppendEvents(builder, events);
        return AccessSafeValues.RoleHash("access-ui-design", builder.ToString());
    }

    private static void AppendEvents(StringBuilder builder, IReadOnlyList<AccessRawUiEvent> events)
    {
        foreach (var item in events.Where(item => AllowedEventRoles.Contains(item.Role)).OrderBy(item => item.Role, StringComparer.Ordinal))
        {
            builder.Append('|').Append(item.Role).Append(':');
            AppendProtected(builder, $"event-{item.Role}", item.Value);
        }
    }

    private static void AppendProtected(StringBuilder builder, string role, string? value)
    {
        var normalized = value ?? string.Empty;
        builder.Append(normalized.Length).Append(':').Append(AccessSafeValues.RoleHash($"access-ui-{role}", normalized));
    }

    private static string MaskQuotedLiterals(string value)
    {
        var builder = new StringBuilder(value.Length);
        var quote = '\0';
        for (var index = 0; index < value.Length; index++)
        {
            var current = value[index];
            if (quote != '\0')
            {
                builder.Append(' ');
                if (current == quote)
                {
                    if (index + 1 < value.Length && value[index + 1] == quote) { builder.Append(' '); index++; }
                    else quote = '\0';
                }
                continue;
            }
            if (current is '\'' or '"') { quote = current; builder.Append(' '); }
            else builder.Append(current);
        }
        return builder.ToString();
    }

    private static string ControlType(int value) => value switch
    {
        100 => "label",
        101 => "rectangle",
        102 => "line",
        103 => "image",
        104 => "command-button",
        105 => "option-button",
        106 => "check-box",
        107 => "option-group",
        108 => "bound-object-frame",
        109 => "text-box",
        110 => "list-box",
        111 => "combo-box",
        112 => "subform-or-subreport",
        114 => "unbound-object-frame",
        118 => "page-break",
        122 => "toggle-button",
        123 => "tab-control",
        124 => "page",
        126 => "attachment",
        127 => "empty-cell",
        128 => "web-browser",
        129 => "navigation-control",
        130 => "navigation-button",
        _ => $"access-control-{value}"
    };

    private static readonly HashSet<string> AllowedEventRoles = new(StringComparer.Ordinal)
    {
        "after-update", "before-update", "on-click", "on-current", "on-dbl-click", "on-load", "on-no-data", "on-open"
    };

    [GeneratedRegex(@"^(?:\[(?<bracketed>[^\]]+)\]|(?<plain>[A-Za-z_][A-Za-z0-9_ .-]*))$", RegexOptions.CultureInvariant)]
    private static partial Regex DirectIdentifierPattern();

    [GeneratedRegex(@"\[(?<name>[^\]]+)\]", RegexOptions.CultureInvariant)]
    private static partial Regex BracketedIdentifierPattern();

    [GeneratedRegex(@"[&+]|\b(?:eval|run|call)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DynamicEventPattern();
}
