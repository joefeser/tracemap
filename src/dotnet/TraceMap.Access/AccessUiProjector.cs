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
    IReadOnlyList<AccessRawUiEvent> Events,
    string? ValidationRule = null,
    string? RowSourceType = null,
    int? BoundColumn = null,
    int? ColumnCount = null,
    string? SourceObject = null,
    string? LinkMasterFields = null,
    string? LinkChildFields = null);

internal sealed record AccessRawReportGroup(
    int Ordinal,
    string? Expression,
    string? SortOrder,
    string? GroupOn);

internal sealed record AccessRawUiSurface(
    string Name,
    string SurfaceKind,
    bool? HasModule,
    string? RecordSource,
    IReadOnlyList<AccessRawControl> Controls,
    IReadOnlyList<AccessRawUiEvent> Events,
    string Coverage = "complete",
    string? Filter = null,
    string? OrderBy = null,
    string? DeclaredBoundState = null,
    IReadOnlyList<AccessRawReportGroup>? ReportGroups = null);

internal sealed record AccessUiProjectionResult(
    IReadOnlyList<AccessUiSurfaceProjection> Surfaces,
    IReadOnlyList<AccessGapProjection> Gaps);

internal static partial class AccessUiProjector
{
    public static AccessUiProjectionResult Project(
        string databaseIdentitySeed,
        IReadOnlyList<AccessRawUiSurface> rawSurfaces,
        IReadOnlyDictionary<string, IReadOnlyList<(string StableKey, string Kind)>> knownObjects,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>> fieldsByTable,
        AccessIdentityDisclosurePolicy disclosurePolicy = AccessIdentityDisclosurePolicy.SafeIdentifier)
    {
        var surfaces = new List<AccessUiSurfaceProjection>();
        var gaps = new List<AccessGapProjection>();
        foreach (var raw in rawSurfaces.OrderBy(item => item.SurfaceKind, StringComparer.Ordinal)
                     .ThenBy(item => AccessSafeValues.RoleHash("access-ui-sort-name", item.Name), StringComparer.Ordinal))
        {
            var kind = raw.SurfaceKind is "form" or "report" ? raw.SurfaceKind : "unknown";
            var identity = AccessSafeValues.Identity(databaseIdentitySeed, kind, raw.Name, disclosurePolicy: disclosurePolicy);
            var bindings = new List<AccessBindingProjection>();
            var recordBinding = ProjectBinding(databaseIdentitySeed, identity.StableKey, "record-source", raw.RecordSource, 0, knownObjects, null, gaps, disclosurePolicy);
            if (recordBinding is not null) bindings.Add(recordBinding);

            IReadOnlyDictionary<string, IReadOnlyList<string>>? scopedFields = null;
            if (recordBinding is { SourceKind: "direct-object", TargetStableKeys.Count: 1 }
                && fieldsByTable.TryGetValue(recordBinding.TargetStableKeys[0], out var fieldLookup))
                scopedFields = fieldLookup;
            var filterBinding = ProjectBinding(databaseIdentitySeed, identity.StableKey, "filter", raw.Filter, 0, null, scopedFields, gaps, disclosurePolicy);
            if (filterBinding is not null) bindings.Add(filterBinding);
            var orderByBinding = ProjectBinding(databaseIdentitySeed, identity.StableKey, "order-by", raw.OrderBy, 0, null, scopedFields, gaps, disclosurePolicy);
            if (orderByBinding is not null) bindings.Add(orderByBinding);

            var controls = new List<AccessControlProjection>();
            foreach (var rawControl in raw.Controls.OrderBy(item => item.Ordinal)
                         .ThenBy(item => AccessSafeValues.RoleHash("access-control-sort-name", item.Name), StringComparer.Ordinal))
            {
                var controlIdentity = AccessSafeValues.Identity(databaseIdentitySeed, $"control-{identity.StableKey}", rawControl.Name, rawControl.Ordinal, disclosurePolicy);
                var controlBindings = new List<AccessBindingProjection>();
                var controlSource = ProjectBinding(databaseIdentitySeed, controlIdentity.StableKey, "control-source", rawControl.ControlSource,
                    rawControl.Ordinal, null, scopedFields, gaps, disclosurePolicy);
                if (controlSource is not null) controlBindings.Add(controlSource);
                var rowSource = ProjectBinding(databaseIdentitySeed, controlIdentity.StableKey, "row-source", rawControl.RowSource,
                    rawControl.Ordinal, knownObjects, null, gaps, disclosurePolicy);
                if (rowSource is not null) controlBindings.Add(rowSource);
                var validation = ProjectBinding(databaseIdentitySeed, controlIdentity.StableKey, "validation-rule", rawControl.ValidationRule,
                    rawControl.Ordinal, null, scopedFields, gaps, disclosurePolicy);
                if (validation is not null) controlBindings.Add(validation);
                var sourceObject = ProjectBinding(databaseIdentitySeed, controlIdentity.StableKey, "source-object", NormalizeSourceObject(rawControl.SourceObject),
                    rawControl.Ordinal, knownObjects, null, gaps, disclosurePolicy);
                if (sourceObject is not null) controlBindings.Add(sourceObject);
                var masterCount = CountLinkFields(rawControl.LinkMasterFields);
                var childCount = CountLinkFields(rawControl.LinkChildFields);
                if (masterCount != childCount && (masterCount > 0 || childCount > 0))
                    gaps.Add(new(
                        "AccessSubsurfaceLinkFieldCountMismatch",
                        "binding",
                        controlIdentity.StableKey,
                        RuleIds.LegacyAccessBinding));
                controlBindings.AddRange(ProjectFieldListBindings(
                    databaseIdentitySeed, controlIdentity.StableKey, "link-master-field",
                    rawControl.LinkMasterFields, rawControl.Ordinal, scopedFields, gaps, disclosurePolicy));
                IReadOnlyDictionary<string, IReadOnlyList<string>>? childFields = null;
                if (sourceObject is { SourceKind: "direct-object", TargetStableKeys.Count: 1 }
                    && rawSurfaces.FirstOrDefault(candidate =>
                        AccessSafeValues.Identity(
                            databaseIdentitySeed,
                            candidate.SurfaceKind,
                            candidate.Name,
                            disclosurePolicy: disclosurePolicy).StableKey == sourceObject.TargetStableKeys[0]) is { } childSurface)
                {
                    var childRecord = ProjectBinding(databaseIdentitySeed, sourceObject.TargetStableKeys[0], "record-source",
                        childSurface.RecordSource, 0, knownObjects, null, [], disclosurePolicy);
                    if (childRecord is { SourceKind: "direct-object", TargetStableKeys.Count: 1 }
                        && fieldsByTable.TryGetValue(childRecord.TargetStableKeys[0], out var resolvedChildFields))
                        childFields = resolvedChildFields;
                }
                controlBindings.AddRange(ProjectFieldListBindings(
                    databaseIdentitySeed, controlIdentity.StableKey, "link-child-field",
                    rawControl.LinkChildFields, rawControl.Ordinal, childFields, gaps, disclosurePolicy));
                controls.Add(new(
                    controlIdentity,
                    identity.StableKey,
                    rawControl.Ordinal,
                    ControlType(rawControl.ControlType),
                    controlBindings.OrderBy(item => item.BindingKind, StringComparer.Ordinal).ToArray(),
                    ProjectEvents(rawControl.Events),
                    NormalizeRowSourceType(rawControl.RowSourceType),
                    NormalizeNonNegative(rawControl.BoundColumn),
                    NormalizeNonNegative(rawControl.ColumnCount)));
            }

            var surfaceEvents = ProjectEvents(raw.Events);
            var groupBindings = (raw.ReportGroups ?? [])
                .OrderBy(group => group.Ordinal)
                .Select(group =>
                {
                    var binding = ProjectBinding(
                        databaseIdentitySeed,
                        identity.StableKey,
                        "report-group-sort",
                        group.Expression,
                        group.Ordinal,
                        null,
                        scopedFields,
                        gaps,
                        disclosurePolicy);
                    return binding is null
                        ? null
                        : new AccessReportGroupProjection(
                            group.Ordinal,
                            binding,
                            NormalizeSortOrder(group.SortOrder),
                            NormalizeGroupOn(group.GroupOn));
                })
                .Where(group => group is not null)
                .Cast<AccessReportGroupProjection>()
                .ToArray();
            bindings.AddRange(groupBindings.Select(group => group.Binding));
            var designHash = DesignHash(identity, kind, raw.HasModule, raw.RecordSource, raw.Filter, raw.OrderBy, raw.Controls, raw.Events, raw.ReportGroups ?? []);
            surfaces.Add(new(
                identity,
                kind,
                raw.HasModule switch { true => "present", false => "absent", null => "unknown" },
                raw.DeclaredBoundState switch
                {
                    "bound" => "bound-declared",
                    "unbound" => "unbound",
                    "unknown" => "unknown",
                    _ => string.IsNullOrWhiteSpace(raw.RecordSource)
                        ? raw.Coverage == "complete" ? "unbound" : "unknown"
                        : "bound-declared"
                },
                designHash,
                bindings,
                controls,
                surfaceEvents,
                raw.Coverage,
                groupBindings));
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
        List<AccessGapProjection> gaps,
        AccessIdentityDisclosurePolicy disclosurePolicy)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length == 0) return null;
        var identity = AccessSafeValues.Identity(databaseIdentitySeed, $"binding-{ownerStableKey}-{bindingKind}", bindingKind, ordinal, disclosurePolicy);

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

    private static IReadOnlyList<AccessBindingProjection> ProjectFieldListBindings(
        string databaseIdentitySeed,
        string ownerStableKey,
        string bindingKind,
        string? value,
        int controlOrdinal,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? fields,
        List<AccessGapProjection> gaps,
        AccessIdentityDisclosurePolicy disclosurePolicy)
    {
        if (string.IsNullOrWhiteSpace(value)) return [];
        var items = value.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (items.Length == 0 || items.Length > 64)
        {
            gaps.Add(new("AccessSubsurfaceLinkShapeUnsupported", "binding", ownerStableKey, RuleIds.LegacyAccessBinding));
            return [];
        }
        return items.Select((item, index) => ProjectBinding(
                databaseIdentitySeed,
                ownerStableKey,
                $"{bindingKind}-{index}",
                item,
                checked(controlOrdinal * 64 + index),
                null,
                fields,
                gaps,
                disclosurePolicy))
            .Where(binding => binding is not null)
            .Cast<AccessBindingProjection>()
            .ToArray();
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
        string? filter,
        string? orderBy,
        IReadOnlyList<AccessRawControl> controls,
        IReadOnlyList<AccessRawUiEvent> events,
        IReadOnlyList<AccessRawReportGroup> reportGroups)
    {
        var builder = new StringBuilder();
        builder.Append("access-ui-design/v1|").Append(identity.StableKey).Append('|').Append(kind).Append('|').Append(hasModule?.ToString() ?? "unknown");
        AppendProtected(builder, "record-source", recordSource);
        AppendProtected(builder, "filter", filter);
        AppendProtected(builder, "order-by", orderBy);
        foreach (var control in controls.OrderBy(item => item.Ordinal).ThenBy(item => AccessSafeValues.RoleHash("access-control-sort-name", item.Name), StringComparer.Ordinal))
        {
            builder.Append('|').Append(control.Ordinal).Append(':').Append(control.ControlType).Append(':')
                .Append(AccessSafeValues.RoleHash("access-control-design-name", control.Name));
            AppendProtected(builder, "control-source", control.ControlSource);
            AppendProtected(builder, "row-source", control.RowSource);
            AppendProtected(builder, "validation-rule", control.ValidationRule);
            AppendProtected(builder, "row-source-type", control.RowSourceType);
            AppendProtected(builder, "source-object", control.SourceObject);
            AppendProtected(builder, "link-master-fields", control.LinkMasterFields);
            AppendProtected(builder, "link-child-fields", control.LinkChildFields);
            builder.Append(':').Append(control.BoundColumn?.ToString() ?? "unknown")
                .Append(':').Append(control.ColumnCount?.ToString() ?? "unknown");
            AppendEvents(builder, control.Events);
        }
        foreach (var group in reportGroups.OrderBy(item => item.Ordinal))
        {
            builder.Append("|group:").Append(group.Ordinal);
            AppendProtected(builder, "group-expression", group.Expression);
            AppendProtected(builder, "group-sort-order", group.SortOrder);
            AppendProtected(builder, "group-on", group.GroupOn);
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

    private static int? NormalizeNonNegative(int? value) => value is >= 0 and <= 10_000 ? value : null;

    private static string? NormalizeSourceObject(string? value)
    {
        var normalized = value?.Trim();
        if (normalized is null) return null;
        foreach (var prefix in new[] { "Form.", "Report." })
            if (normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return normalized[prefix.Length..];
        return normalized;
    }

    private static int CountLinkFields(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? 0
            : value.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length;

    private static string NormalizeRowSourceType(string? value)
    {
        var normalized = value?.Trim();
        return normalized switch
        {
            "Table/Query" => "table-query",
            "Value List" => "value-list",
            "Field List" => "field-list",
            null or "" => "unspecified",
            _ => "other"
        };
    }

    private static string NormalizeSortOrder(string? value) => value?.Trim() switch
    {
        "1" or "Descending" or "descending" => "descending",
        "0" or "Ascending" or "ascending" => "ascending",
        _ => "unspecified"
    };

    private static string NormalizeGroupOn(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "unspecified" : "declared";

    private static readonly HashSet<string> AllowedEventRoles = new(StringComparer.Ordinal)
    {
        "after-update", "before-update", "on-click", "on-current", "on-dbl-click", "on-load", "on-no-data", "on-open"
    };

    [GeneratedRegex(@"^(?:\[(?<bracketed>[^\]]+)\]|(?<plain>[A-Za-z_][A-Za-z0-9_ -]*))$", RegexOptions.CultureInvariant)]
    private static partial Regex DirectIdentifierPattern();

    [GeneratedRegex(@"\[(?<name>[^\]]+)\]", RegexOptions.CultureInvariant)]
    private static partial Regex BracketedIdentifierPattern();

    [GeneratedRegex(@"[&+]|\b(?:eval|run|call)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DynamicEventPattern();
}
