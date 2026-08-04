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
    private static readonly IReadOnlySet<string> ReportContextIdentifiers =
        new HashSet<string>(["Page", "Pages"], StringComparer.OrdinalIgnoreCase);

    public static AccessUiProjectionResult Project(
        string databaseIdentitySeed,
        IReadOnlyList<AccessRawUiSurface> rawSurfaces,
        IReadOnlyDictionary<string, IReadOnlyList<(string StableKey, string Kind)>> knownObjects,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>> fieldsByTable,
        AccessIdentityDisclosurePolicy disclosurePolicy = AccessIdentityDisclosurePolicy.SafeIdentifier,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? queryOutputStableKeys = null,
        IReadOnlyDictionary<string, string>? queryKindsByStableKey = null,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? vbaProcedureStableKeys = null,
        IReadOnlyDictionary<string, string>? fieldCoverageByStableKey = null,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>>? domainCriteriaFieldSetsByObject = null,
        IReadOnlySet<string>? completeTableFieldCatalogStableKeys = null)
    {
        var surfaces = new List<AccessUiSurfaceProjection>();
        var gaps = new List<AccessGapProjection>();
        var childFieldScopes = new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>?>(StringComparer.Ordinal);
        foreach (var raw in rawSurfaces.OrderBy(item => item.SurfaceKind, StringComparer.Ordinal)
                     .ThenBy(item => AccessSafeValues.RoleHash("access-ui-sort-name", item.Name), StringComparer.Ordinal))
        {
            var kind = raw.SurfaceKind is "form" or "report" ? raw.SurfaceKind : "unknown";
            var contextIdentifierNames = kind == "report" ? ReportContextIdentifiers : null;
            var identity = AccessSafeValues.Identity(databaseIdentitySeed, kind, raw.Name, disclosurePolicy: disclosurePolicy);
            var controlNames = raw.Controls.Select(control => control.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var controlStableKeys = raw.Controls
                .GroupBy(control => control.Name, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyList<string>)group
                        .Select(control => AccessSafeValues.Identity(
                            databaseIdentitySeed,
                            $"control-{identity.StableKey}",
                            control.Name,
                            control.Ordinal,
                            disclosurePolicy).StableKey)
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(value => value, StringComparer.Ordinal)
                        .ToArray(),
                    StringComparer.OrdinalIgnoreCase);
            var bindings = new List<AccessBindingProjection>();
            var recordBinding = ConstrainBindingEvidence(ProjectBinding(databaseIdentitySeed, identity.StableKey, "record-source", raw.RecordSource, 0, knownObjects, null, gaps, disclosurePolicy, controlNames, fieldsByTable, controlStableKeys,
                vbaProcedureStableKeys: vbaProcedureStableKeys,
                contextIdentifierNames: contextIdentifierNames,
                fieldCoverageByStableKey: fieldCoverageByStableKey,
                domainCriteriaFieldSetsByObject: domainCriteriaFieldSetsByObject,
                completeTableFieldCatalogStableKeys: completeTableFieldCatalogStableKeys), raw.Coverage, gaps);
            if (recordBinding is not null) bindings.Add(recordBinding);

            IReadOnlyDictionary<string, IReadOnlyList<string>>? scopedFields = null;
            if (recordBinding is { SourceKind: "direct-object", TargetStableKeys.Count: 1 }
                && fieldsByTable.TryGetValue(recordBinding.TargetStableKeys[0], out var fieldLookup))
                scopedFields = fieldLookup;
            else if (recordBinding is { SourceKind: "inline-sql" })
                scopedFields = BuildInlineFieldScope(raw.RecordSource, knownObjects, fieldsByTable, recordBinding.TargetStableKeys);
            var scopedObjectKeys = recordBinding?.TargetStableKeys.ToHashSet(StringComparer.Ordinal) ?? [];
            var scopedObjects = scopedFields is null
                ? null
                : knownObjects.ToDictionary(
                    item => item.Key,
                    item => (IReadOnlyList<(string StableKey, string Kind)>)item.Value
                        .Where(candidate => scopedObjectKeys.Contains(candidate.StableKey))
                        .ToArray(),
                    StringComparer.OrdinalIgnoreCase);
            var filterBinding = ConstrainBindingEvidence(ProjectBinding(databaseIdentitySeed, identity.StableKey, "filter", raw.Filter, 0, scopedObjects, scopedFields, gaps, disclosurePolicy,
                fieldSetsByObject: fieldsByTable, vbaProcedureStableKeys: vbaProcedureStableKeys,
                contextIdentifierNames: contextIdentifierNames,
                fieldCoverageByStableKey: fieldCoverageByStableKey,
                domainCriteriaFieldSetsByObject: domainCriteriaFieldSetsByObject), raw.Coverage, gaps);
            if (filterBinding is not null) bindings.Add(filterBinding);
            var orderByBinding = ConstrainBindingEvidence(ProjectBinding(databaseIdentitySeed, identity.StableKey, "order-by", raw.OrderBy, 0, scopedObjects, scopedFields, gaps, disclosurePolicy, controlNames, fieldsByTable, controlStableKeys,
                vbaProcedureStableKeys: vbaProcedureStableKeys,
                contextIdentifierNames: contextIdentifierNames,
                fieldCoverageByStableKey: fieldCoverageByStableKey,
                domainCriteriaFieldSetsByObject: domainCriteriaFieldSetsByObject), raw.Coverage, gaps);
            if (orderByBinding is not null) bindings.Add(orderByBinding);

            var controls = new List<AccessControlProjection>();
            foreach (var rawControl in raw.Controls.OrderBy(item => item.Ordinal)
                         .ThenBy(item => AccessSafeValues.RoleHash("access-control-sort-name", item.Name), StringComparer.Ordinal))
            {
                var controlIdentity = AccessSafeValues.Identity(databaseIdentitySeed, $"control-{identity.StableKey}", rawControl.Name, rawControl.Ordinal, disclosurePolicy);
                var controlBindings = new List<AccessBindingProjection>();
                var controlSource = ProjectBinding(databaseIdentitySeed, controlIdentity.StableKey, "control-source", rawControl.ControlSource,
                    rawControl.Ordinal, scopedObjects, scopedFields, gaps, disclosurePolicy, controlNames, fieldsByTable, controlStableKeys,
                    recordBinding, raw.RecordSource, queryKindsByStableKey, knownObjects, vbaProcedureStableKeys,
                    contextIdentifierNames,
                    fieldCoverageByStableKey,
                    domainCriteriaFieldSetsByObject);
                if (controlSource is not null) controlBindings.Add(controlSource);
                var rowSource = ProjectBinding(databaseIdentitySeed, controlIdentity.StableKey, "row-source", rawControl.RowSource,
                    rawControl.Ordinal, knownObjects, null, gaps, disclosurePolicy, controlNames, fieldsByTable, controlStableKeys,
                    vbaProcedureStableKeys: vbaProcedureStableKeys,
                    contextIdentifierNames: contextIdentifierNames,
                    fieldCoverageByStableKey: fieldCoverageByStableKey,
                    domainCriteriaFieldSetsByObject: domainCriteriaFieldSetsByObject);
                if (rowSource is not null) controlBindings.Add(rowSource);
                var validation = ProjectBinding(databaseIdentitySeed, controlIdentity.StableKey, "validation-rule", rawControl.ValidationRule,
                    rawControl.Ordinal, scopedObjects, scopedFields, gaps, disclosurePolicy, controlNames, fieldsByTable, controlStableKeys,
                    domainObjects: knownObjects, vbaProcedureStableKeys: vbaProcedureStableKeys,
                    contextIdentifierNames: contextIdentifierNames,
                    fieldCoverageByStableKey: fieldCoverageByStableKey,
                    domainCriteriaFieldSetsByObject: domainCriteriaFieldSetsByObject);
                if (validation is not null) controlBindings.Add(validation);
                var sourceObject = ProjectBinding(databaseIdentitySeed, controlIdentity.StableKey, "source-object", NormalizeSourceObject(rawControl.SourceObject),
                    rawControl.Ordinal, knownObjects, null, gaps, disclosurePolicy, controlNames, fieldsByTable, controlStableKeys,
                    vbaProcedureStableKeys: vbaProcedureStableKeys,
                    contextIdentifierNames: contextIdentifierNames,
                    fieldCoverageByStableKey: fieldCoverageByStableKey,
                    domainCriteriaFieldSetsByObject: domainCriteriaFieldSetsByObject);
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
                if (sourceObject is { SourceKind: "direct-object", TargetStableKeys.Count: 1 })
                {
                    var childStableKey = sourceObject.TargetStableKeys[0];
                    if (!childFieldScopes.TryGetValue(childStableKey, out childFields))
                    {
                        if (rawSurfaces.FirstOrDefault(candidate =>
                                AccessSafeValues.Identity(
                                    databaseIdentitySeed,
                                    candidate.SurfaceKind,
                                    candidate.Name,
                                    disclosurePolicy: disclosurePolicy).StableKey == childStableKey) is { } childSurface)
                        {
                            var childControlNames = childSurface.Controls
                                .Select(control => control.Name)
                                .ToHashSet(StringComparer.OrdinalIgnoreCase);
                            var childRecord = ConstrainBindingEvidence(ProjectBinding(databaseIdentitySeed, childStableKey, "record-source",
                                childSurface.RecordSource, 0, knownObjects, null, gaps, disclosurePolicy, childControlNames, fieldsByTable,
                                contextIdentifierNames: childSurface.SurfaceKind.Equals("report", StringComparison.OrdinalIgnoreCase)
                                    ? ReportContextIdentifiers
                                    : null,
                                fieldCoverageByStableKey: fieldCoverageByStableKey,
                                domainCriteriaFieldSetsByObject: domainCriteriaFieldSetsByObject,
                                completeTableFieldCatalogStableKeys: completeTableFieldCatalogStableKeys), childSurface.Coverage, gaps);
                            if (childRecord is { SourceKind: "direct-object", TargetStableKeys.Count: 1 }
                                && fieldsByTable.TryGetValue(childRecord.TargetStableKeys[0], out var resolvedChildFields))
                                childFields = resolvedChildFields;
                            else if (childRecord is { SourceKind: "inline-sql", Coverage: "complete" })
                                childFields = BuildInlineFieldScope(
                                    childSurface.RecordSource,
                                    knownObjects,
                                    fieldsByTable,
                                    childRecord.TargetStableKeys);
                        }
                        childFieldScopes[childStableKey] = childFields;
                    }
                }
                controlBindings.AddRange(ProjectFieldListBindings(
                    databaseIdentitySeed, controlIdentity.StableKey, "link-child-field",
                    rawControl.LinkChildFields, rawControl.Ordinal, childFields, gaps, disclosurePolicy));
                for (var index = 0; index < controlBindings.Count; index++)
                    controlBindings[index] = ConstrainBindingEvidence(controlBindings[index], raw.Coverage, gaps)!;
                var controlSourceBinding = controlBindings.FirstOrDefault(binding => binding.BindingKind == "control-source");
                var rowSourceBinding = controlBindings.FirstOrDefault(binding => binding.BindingKind == "row-source");
                var valueClassification = controlSourceBinding is null
                    ? "unbound"
                    : controlSourceBinding.SourceKind == "direct-field" ? "bound-field"
                    : controlSourceBinding.Expression?.Classification is "calculated-expression" or "domain-lookup" ? "calculated-expression"
                    : "unresolved-or-dynamic";
                var populationType = rowSourceBinding switch
                {
                    null => "none",
                    _ when NormalizeRowSourceType(rawControl.RowSourceType) == "value-list" => "value-list",
                    { SourceKind: "direct-object", TargetKind: "query" } => "saved-query",
                    { SourceKind: "direct-object" } => "table",
                    _ when rawControl.RowSource?.TrimStart().StartsWith("select", StringComparison.OrdinalIgnoreCase) == true => "inline-sql",
                    _ => "dynamic"
                };
                var selectedOrdinals = new List<int>();
                var selectedValues = new List<string>();
                if (rawControl.BoundColumn is > 0
                    && rowSourceBinding?.TargetStableKeys.Count == 1
                    && queryOutputStableKeys?.TryGetValue(rowSourceBinding.TargetStableKeys[0], out var outputs) == true)
                {
                    var boundIndex = rawControl.BoundColumn.Value - 1;
                    var valid = boundIndex < outputs.Count
                        && (rawControl.ColumnCount is null || rawControl.BoundColumn.Value <= rawControl.ColumnCount.Value)
                        && !string.IsNullOrWhiteSpace(outputs[boundIndex]);
                    if (valid)
                    {
                        selectedOrdinals.Add(boundIndex);
                        selectedValues.Add(outputs[boundIndex]);
                    }
                    else
                    {
                        gaps.Add(new("AccessBindingBoundColumnOutOfRange", "binding", controlIdentity.StableKey, RuleIds.LegacyAccessBinding));
                    }
                }
                var persistenceTargets = controlSourceBinding?.SourceKind == "direct-field"
                    ? controlSourceBinding.TargetStableKeys
                    : Array.Empty<string>();
                controls.Add(new(
                    controlIdentity,
                    identity.StableKey,
                    rawControl.Ordinal,
                    ControlType(rawControl.ControlType),
                    controlBindings.OrderBy(item => item.BindingKind, StringComparer.Ordinal).ToArray(),
                    ProjectEvents(rawControl.Events),
                    NormalizeRowSourceType(rawControl.RowSourceType),
                    NormalizeNonNegative(rawControl.BoundColumn),
                    NormalizeNonNegative(rawControl.ColumnCount),
                    valueClassification,
                    populationType,
                    rowSourceBinding?.TargetStableKeys ?? [],
                    rowSourceBinding?.Coverage ?? "none",
                    selectedOrdinals.ToArray(),
                    selectedValues.ToArray(),
                    persistenceTargets,
                    valueClassification == "unbound" && populationType is "saved-query" or "table" ? "query-backed-selector" : "none"));
            }

            var surfaceEvents = ProjectEvents(raw.Events);
            var groupBindings = (raw.ReportGroups ?? [])
                .OrderBy(group => group.Ordinal)
                .Select(group =>
                {
                    var binding = string.IsNullOrWhiteSpace(group.Expression)
                        ? MissingReportGroupBinding(
                            databaseIdentitySeed,
                            identity.StableKey,
                            group.Ordinal,
                            gaps,
                            disclosurePolicy)
                        : ConstrainBindingEvidence(ProjectBinding(
                            databaseIdentitySeed,
                            identity.StableKey,
                            "report-group-sort",
                            group.Expression,
                            group.Ordinal,
                            knownObjects,
                            scopedFields,
                            gaps,
                            disclosurePolicy,
                            controlNames,
                            fieldsByTable,
                            controlStableKeys,
                            contextIdentifierNames: contextIdentifierNames,
                            fieldCoverageByStableKey: fieldCoverageByStableKey,
                            domainCriteriaFieldSetsByObject: domainCriteriaFieldSetsByObject), raw.Coverage, gaps)!;
                    return new AccessReportGroupProjection(
                        group.Ordinal,
                        binding,
                        NormalizeSortOrder(group.SortOrder),
                        NormalizeGroupOn(group.GroupOn));
                })
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
        AccessIdentityDisclosurePolicy disclosurePolicy,
        IReadOnlySet<string>? controlNames = null,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>>? fieldSetsByObject = null,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? controlStableKeys = null,
        AccessBindingProjection? owningRecordBinding = null,
        string? owningRecordSource = null,
        IReadOnlyDictionary<string, string>? queryKindsByStableKey = null,
        IReadOnlyDictionary<string, IReadOnlyList<(string StableKey, string Kind)>>? domainObjects = null,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? vbaProcedureStableKeys = null,
        IReadOnlySet<string>? contextIdentifierNames = null,
        IReadOnlyDictionary<string, string>? fieldCoverageByStableKey = null,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>>? domainCriteriaFieldSetsByObject = null,
        IReadOnlySet<string>? completeTableFieldCatalogStableKeys = null)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length == 0) return null;
        var identity = AccessSafeValues.Identity(databaseIdentitySeed, $"binding-{ownerStableKey}-{bindingKind}", bindingKind, ordinal, disclosurePolicy);

        if (trimmed.StartsWith("select", StringComparison.OrdinalIgnoreCase)
            && objects is not null
            && fieldSetsByObject is not null)
        {
            var projection = AccessQueryProjector.ProjectStaticSelect(trimmed, objects, fieldSetsByObject);
            var targets = projection.Dependencies.Select(dependency => dependency.TargetStableKey)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            var dependencyKinds = projection.Dependencies.Select(dependency => dependency.TargetKind)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            var dependencyTargetKind = dependencyKinds.Length == 1 ? dependencyKinds[0]
                : dependencyKinds.Length == 0 ? "unknown"
                : "mixed";
            var completeTableWildcard = projection.DependencyCoverage == "complete"
                && AccessQueryProjector.HasOnlyWildcardProjection(trimmed)
                && targets.Length == 1
                && dependencyKinds.Length == 1
                && dependencyKinds[0] == "table"
                && completeTableFieldCatalogStableKeys?.Contains(targets[0]) == true;
            var staticLineageCoverage = projection.DependencyCoverage == "complete"
                && (projection.OutputCoverage == "complete" || completeTableWildcard)
                ? "complete"
                : "partial";
            if (staticLineageCoverage != "complete")
                gaps.Add(new("AccessBindingInlineSqlProjectionPartial", "binding", identity.StableKey, RuleIds.LegacyAccessBinding));
            return new(identity, ownerStableKey, bindingKind, "inline-sql",
                projection.SqlHash, projection.SqlLength, targets, dependencyTargetKind, staticLineageCoverage,
                RuntimeValueCoverage: projection.RuntimeValueCoverage);
        }

        if (TryDirectName(trimmed, out var directName))
        {
            var fieldOnlyDirectName = bindingKind is "control-source" or "validation-rule";
            if (fieldOnlyDirectName)
            {
                if (fields is not null && fields.TryGetValue(directName, out var fieldCandidates))
                {
                    if (fieldCandidates.Count == 1)
                        return DirectField(
                            identity, ownerStableKey, bindingKind, fieldCandidates[0], fieldCoverageByStableKey);
                    return Ambiguous(identity, ownerStableKey, bindingKind, trimmed, "field", gaps);
                }
            }
            else if (objects is not null && objects.TryGetValue(directName, out var objectCandidates))
            {
                if (objectCandidates.Count == 1)
                    return new(identity, ownerStableKey, bindingKind, "direct-object", null, 0,
                        [objectCandidates[0].StableKey], objectCandidates[0].Kind, "complete");
                return Ambiguous(identity, ownerStableKey, bindingKind, trimmed, "object", gaps);
            }
            else if (fields is not null && fields.TryGetValue(directName, out var fieldCandidates))
            {
                if (fieldCandidates.Count == 1)
                    return DirectField(
                        identity, ownerStableKey, bindingKind, fieldCandidates[0], fieldCoverageByStableKey);
                return Ambiguous(identity, ownerStableKey, bindingKind, trimmed, "field", gaps);
            }

            if (bindingKind == "control-source"
                && owningRecordBinding is { SourceKind: "direct-object", TargetStableKeys.Count: 1 }
                && queryKindsByStableKey?.GetValueOrDefault(owningRecordBinding.TargetStableKeys[0]) == "crosstab")
            {
                var candidate = AccessSafeValues.HashOnlyCandidate(
                    databaseIdentitySeed,
                    $"crosstab-output-candidate-{owningRecordBinding.TargetStableKeys[0]}",
                    directName).StableKey;
                gaps.Add(new("AccessBindingCrosstabOutputCandidate", "binding", identity.StableKey, RuleIds.LegacyAccessBinding));
                return new(identity, ownerStableKey, bindingKind, "surface-declared-crosstab-output-candidate",
                    AccessSafeValues.RoleHash($"access-{bindingKind}-expression", trimmed), trimmed.Length,
                    [candidate], "query-output-candidate", "partial");
            }

            if (bindingKind == "control-source"
                && owningRecordBinding is { SourceKind: "expression" or "inline-sql" }
                && owningRecordSource?.TrimStart().StartsWith("select", StringComparison.OrdinalIgnoreCase) == true
                && AccessQueryProjector.HasStaticOutputName(owningRecordSource, directName))
            {
                var sourceHash = owningRecordBinding.ExpressionHash
                    ?? AccessSafeValues.RoleHash("access-inline-record-source", owningRecordSource);
                var candidate = AccessSafeValues.HashOnlyCandidate(
                    databaseIdentitySeed,
                    $"inline-output-candidate-{sourceHash}",
                    directName).StableKey;
                gaps.Add(new("AccessBindingInlineSqlOutputCandidate", "binding", identity.StableKey, RuleIds.LegacyAccessBinding));
                return new(identity, ownerStableKey, bindingKind, "inline-source-output-candidate",
                    AccessSafeValues.RoleHash($"access-{bindingKind}-expression", trimmed), trimmed.Length,
                    [candidate], "query-output-candidate", "partial");
            }

            if (bindingKind == "control-source" && owningRecordBinding is { SourceKind: "inline-sql" })
            {
                gaps.Add(new("AccessBindingInlineSqlOutputUnmatched", "binding", identity.StableKey, RuleIds.LegacyAccessBinding));
                return new(identity, ownerStableKey, bindingKind, "inline-source-output-unmatched",
                    AccessSafeValues.RoleHash($"access-{bindingKind}-expression", trimmed), trimmed.Length,
                    [], "query-output", "partial");
            }

            if (bindingKind == "control-source" && owningRecordBinding is { SourceKind: "ambiguous-identifier" })
            {
                gaps.Add(new("AccessBindingRecordSourceAmbiguous", "binding", identity.StableKey, RuleIds.LegacyAccessBinding));
                return new(identity, ownerStableKey, bindingKind, "record-source-ambiguous",
                    AccessSafeValues.RoleHash($"access-{bindingKind}-expression", trimmed), trimmed.Length,
                    [], "field", "partial");
            }

            if (bindingKind == "control-source"
                && owningRecordBinding is { SourceKind: "direct-object", TargetKind: "query" })
            {
                gaps.Add(new("AccessBindingQueryOutputCatalogIncomplete", "binding", identity.StableKey, RuleIds.LegacyAccessBinding));
                return new(identity, ownerStableKey, bindingKind, "query-output-catalog-incomplete",
                    AccessSafeValues.RoleHash($"access-{bindingKind}-expression", trimmed), trimmed.Length,
                    [], "query-output", "partial");
            }

            if (bindingKind == "control-source" && owningRecordBinding is null)
            {
                gaps.Add(new("AccessBindingOwningRecordSourceUnavailable", "binding", identity.StableKey, RuleIds.LegacyAccessBinding));
                return new(identity, ownerStableKey, bindingKind, "owning-record-source-unavailable",
                    AccessSafeValues.RoleHash($"access-{bindingKind}-expression", trimmed), trimmed.Length,
                    [], "field", "partial");
            }

            gaps.Add(new("AccessBindingTargetUnresolved", "binding", identity.StableKey, RuleIds.LegacyAccessBinding));
            return new(identity, ownerStableKey, bindingKind, "unresolved-identifier",
                AccessSafeValues.RoleHash($"access-{bindingKind}-expression", trimmed), trimmed.Length, [], "unknown", "partial");
        }
        var candidates = ResolveExpressionCandidates(trimmed, objects, fields, out var ambiguous);
        var expression = AccessExpressionProjector.ProjectWithDomainCriteriaFields(
            trimmed,
            objects,
            fields,
            controlNames,
            fieldSetsByObject,
            controlStableKeys,
            domainObjects,
            vbaProcedureStableKeys,
            contextIdentifierNames,
            domainCriteriaFieldSetsByObject);
        var expressionTargets = candidates
            .Concat(expression.QueryStableKeys)
            .Concat(expression.FieldStableKeys)
            .Concat(expression.ControlStableKeys)
            .Concat(expression.VbaProcedureStableKeys)
            .Concat(expression.SelectedFieldStableKeys)
            .Concat(expression.CriteriaFieldStableKeys)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (expression.GapClassification is not null)
            gaps.Add(new(expression.GapClassification, "binding", identity.StableKey, RuleIds.LegacyAccessBinding));
        else if (ambiguous)
            gaps.Add(new("AccessBindingTargetAmbiguous", "binding", identity.StableKey, RuleIds.LegacyAccessBinding));
        var targetKind = expression.VbaProcedureStableKeys.Count > 0
            ? expressionTargets.Except(expression.VbaProcedureStableKeys, StringComparer.Ordinal).Any() ? "mixed" : "vba-procedure"
            : expression.ControlStableKeys.Count > 0
            ? expressionTargets.Except(expression.ControlStableKeys, StringComparer.Ordinal).Any() ? "mixed" : "control"
            : expression.FieldStableKeys.Count > 0 || expression.SelectedFieldStableKeys.Count > 0 || expression.CriteriaFieldStableKeys.Count > 0
            ? "field"
            : expression.QueryStableKeys.Count > 0 ? "object"
            : expressionTargets.Length == 0
                && expression.ExternalReferenceHashes.Count > 0
                && expression.GapClassification is null
                ? "context"
                : "unknown";
        return new(identity, ownerStableKey, bindingKind, "expression",
            AccessSafeValues.RoleHash($"access-{bindingKind}-expression", trimmed), trimmed.Length,
            expressionTargets, targetKind, expression.Coverage, expression, expression.RuntimeValueCoverage);
    }

    private static AccessBindingProjection? ConstrainBindingEvidence(
        AccessBindingProjection? binding,
        string evidenceCoverage,
        List<AccessGapProjection> gaps)
    {
        if (binding is null || evidenceCoverage == "complete" || binding.Coverage != "complete") return binding;
        if (binding.SourceKind == "inline-sql"
            && !gaps.Any(gap => gap.Classification == "AccessBindingInlineSqlProjectionPartial"
                && gap.StableScopeKey == binding.Identity.StableKey))
            gaps.Add(new(
                "AccessBindingInlineSqlProjectionPartial",
                "binding",
                binding.Identity.StableKey,
                RuleIds.LegacyAccessBinding));
        return binding with { Coverage = "partial" };
    }

    private static AccessBindingProjection DirectField(
        AccessSafeIdentity identity,
        string ownerStableKey,
        string bindingKind,
        string fieldStableKey,
        IReadOnlyDictionary<string, string>? fieldCoverageByStableKey) =>
        new(
            identity,
            ownerStableKey,
            bindingKind,
            "direct-field",
            null,
            0,
            [fieldStableKey],
            "field",
            fieldCoverageByStableKey?.GetValueOrDefault(fieldStableKey) == "partial"
                ? "partial"
                : "complete");

    private static AccessBindingProjection MissingReportGroupBinding(
        string databaseIdentitySeed,
        string ownerStableKey,
        int ordinal,
        List<AccessGapProjection> gaps,
        AccessIdentityDisclosurePolicy disclosurePolicy)
    {
        const string bindingKind = "report-group-sort";
        var identity = AccessSafeValues.Identity(
            databaseIdentitySeed,
            $"binding-{ownerStableKey}-{bindingKind}",
            bindingKind,
            ordinal,
            disclosurePolicy);
        gaps.Add(new(
            "AccessReportGroupExpressionUnavailable",
            "binding",
            identity.StableKey,
            RuleIds.LegacyAccessBinding));
        return new(
            identity,
            ownerStableKey,
            bindingKind,
            "unresolved",
            null,
            0,
            [],
            "field",
            "partial");
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

    private static IReadOnlyDictionary<string, IReadOnlyList<string>>? MergeFieldScopes(
        IReadOnlyList<string> sourceStableKeys,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>> fieldsByObject)
    {
        var merged = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var sourceStableKey in sourceStableKeys.Distinct(StringComparer.Ordinal))
        {
            if (!fieldsByObject.TryGetValue(sourceStableKey, out var fields)) continue;
            foreach (var field in fields)
            {
                if (!merged.TryGetValue(field.Key, out var candidates))
                    merged[field.Key] = candidates = [];
                candidates.AddRange(field.Value);
            }
        }
        return merged.Count == 0
            ? null
            : merged.ToDictionary(
                item => item.Key,
                item => (IReadOnlyList<string>)item.Value.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                StringComparer.OrdinalIgnoreCase);
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<string>>? BuildInlineFieldScope(
        string? sql,
        IReadOnlyDictionary<string, IReadOnlyList<(string StableKey, string Kind)>> knownObjects,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>> fieldsByObject,
        IReadOnlyList<string> dependencyStableKeys)
    {
        if (string.IsNullOrWhiteSpace(sql)) return null;
        if (AccessQueryProjector.HasWildcardProjection(sql))
            return MergeFieldScopes(dependencyStableKeys, fieldsByObject);

        var selectedFieldKeys = AccessQueryProjector.ProjectStaticSelect(sql, knownObjects, fieldsByObject)
            .Outputs.SelectMany(output => output.SourceFieldStableKeys)
            .ToHashSet(StringComparer.Ordinal);
        if (selectedFieldKeys.Count == 0) return null;

        var scoped = MergeFieldScopes(dependencyStableKeys, fieldsByObject);
        return scoped?.Select(entry => new
        {
            entry.Key,
            Candidates = entry.Value.Where(selectedFieldKeys.Contains).ToArray()
        })
            .Where(entry => entry.Candidates.Length > 0)
            .ToDictionary(
                entry => entry.Key,
                entry => (IReadOnlyList<string>)entry.Candidates,
                StringComparer.OrdinalIgnoreCase);
    }

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
        "after-update", "on-activate", "before-update", "on-click", "on-close", "on-current", "on-deactivate", "on-dbl-click", "on-error", "on-load", "on-no-data", "on-open", "on-resize", "on-timer", "on-unload"
    };

    [GeneratedRegex(@"^(?:\[(?<bracketed>[^\]]+)\]|(?<plain>[A-Za-z_][A-Za-z0-9_ -]*))$", RegexOptions.CultureInvariant)]
    private static partial Regex DirectIdentifierPattern();

    [GeneratedRegex(@"\[(?<name>[^\]]+)\]", RegexOptions.CultureInvariant)]
    private static partial Regex BracketedIdentifierPattern();

    [GeneratedRegex(@"[&+]|\b(?:eval|run|call)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DynamicEventPattern();
}
