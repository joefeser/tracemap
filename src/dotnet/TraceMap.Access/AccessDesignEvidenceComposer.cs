using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TraceMap.Core;

namespace TraceMap.Access;

public static class AccessDesignEvidenceComposer
{
    public const string ComposerVersion = "access-design-evidence/0.1.0";
    private const string ExtractorId = "AccessSourceNeutralDesignEvidence";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static async Task<ScanResult> ComposeAsync(
        string baseScanDirectory,
        string designEvidenceDirectory,
        AccessLimits? limits = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await ComposeCoreAsync(
                baseScanDirectory,
                designEvidenceDirectory,
                limits,
                cancellationToken);
        }
        catch (AccessScanException) { throw; }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch { throw new AccessScanException("AccessDesignCompositionFailed"); }
    }

    private static async Task<ScanResult> ComposeCoreAsync(
        string baseScanDirectory,
        string designEvidenceDirectory,
        AccessLimits? limits,
        CancellationToken cancellationToken)
    {
        limits ??= AccessLimits.Default;
        var baseScan = await ReadBaseScanAsync(baseScanDirectory, limits, cancellationToken);
        var declared = AccessDesignEvidenceReader.InspectManifest(designEvidenceDirectory, limits);
        var databaseSeed = AccessSafeValues.DatabaseIdentitySeed(
            declared.RepositoryIdentityHash,
            baseScan.Manifest.CommitSha,
            baseScan.DatabasePath,
            baseScan.DatabaseHash);
        if (!AccessSafeValues.FixedHashEquals(databaseSeed, declared.DatabaseIdentityHash)
            || !string.Equals(AccessSafeValues.DatabaseStableKey(databaseSeed), baseScan.DatabaseStableKey, StringComparison.Ordinal))
            throw new AccessScanException("AccessDesignInputDatabaseUnbound");

        var binding = new AccessDesignEvidenceBinding(
            declared.RepositoryIdentityHash,
            baseScan.Manifest.CommitSha,
            baseScan.ManifestSha256,
            databaseSeed,
            baseScan.DatabaseHash);
        using var bundle = AccessDesignEvidenceReader.Read(designEvidenceDirectory, binding, limits);
        if (!bundle.AcceptedForProjection)
            throw new AccessScanException(bundle.Gaps.FirstOrDefault()?.Classification ?? "AccessDesignInputDatabaseUnbound");

        var projected = Project(bundle, databaseSeed, baseScan.Facts, limits);
        var projection = new AccessDatabaseProjection(
            "tracemap.access-projection.v1",
            baseScan.DatabaseHash,
            Path.GetExtension(baseScan.DatabasePath).ToLowerInvariant(),
            "source-neutral",
            0,
            false,
            false,
            0,
            [],
            [],
            [],
            [],
            projected.Gaps,
            [],
            projected.Ui.Surfaces,
            projected.Vba.Modules,
            projected.Vba.EventBindings,
            projected.Macros.Macros);
        var syntheticInput = new AccessValidatedInput(
            string.Empty,
            baseScan.Manifest.RepoName,
            declared.RepositoryIdentityHash,
            null,
            baseScan.Manifest.Branch,
            baseScan.Manifest.CommitSha,
            Path.Combine(baseScanDirectory, "scan-manifest.json"),
            baseScan.DatabasePath,
            baseScan.DatabaseHash,
            Path.GetExtension(baseScan.DatabasePath).ToLowerInvariant(),
            string.Empty,
            false,
            baseScan.DatabaseSize);
        var generated = AccessFactBuilder.Build(
            syntheticInput,
            projection,
            new AccessScanOptions(string.Empty, baseScan.DatabasePath, string.Empty),
            limits);

        var designContractHash = AccessSafeValues.RoleHash("access-design-contract", string.Join('|',
            bundle.Manifest.ProducerId,
            bundle.Manifest.ProducerVersion,
            bundle.Manifest.Mechanism,
            bundle.Manifest.BundleSha256,
            bundle.Manifest.CopyBinding,
            bundle.Manifest.RepositoryIdentityHash,
            bundle.Manifest.CommitSha,
            bundle.Manifest.BaseScanManifestSha256,
            bundle.Manifest.DatabaseIdentityHash,
            bundle.Manifest.SourceCopySha256,
            bundle.Manifest.CatalogCompleteness,
            bundle.Manifest.CoordinateCapability,
            string.Join(';', bundle.Manifest.CountsByKind.OrderBy(item => item.Key, StringComparer.Ordinal)
                .Select(item => $"{item.Key}:{item.Value}"))));
        var scanId = "access-enriched-" + FactFactory.Hash(string.Join('|',
            "access-design-enrichment/v1",
            baseScan.ManifestSha256,
            designContractHash,
            AccessDesignEvidenceReader.ManifestSchema,
            ComposerVersion), 32);
        var knownGaps = baseScan.Manifest.KnownGaps
            .Concat(projected.Gaps.Select(gap => gap.Classification))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var manifest = baseScan.Manifest with
        {
            ScanId = scanId,
            ScannerVersion = $"{AccessFactBuilder.ScannerVersion}+design-v1",
            ScannedAt = baseScan.Manifest.ScannedAt,
            AnalysisLevel = "Level1SemanticAnalysisReduced",
            BuildStatus = "FailedOrPartial",
            KnownGaps = knownGaps
        };

        var designTypes = new HashSet<string>(StringComparer.Ordinal)
        {
            FactTypes.AccessFormDeclared,
            FactTypes.AccessReportDeclared,
            FactTypes.AccessControlDeclared,
            FactTypes.AccessBindingDeclared,
            FactTypes.AccessVbaModuleDeclared,
            FactTypes.AccessVbaProcedureDeclared,
            FactTypes.AccessEventBindingCandidate,
            FactTypes.AccessNavigationCandidate,
            FactTypes.AccessMacroDeclared,
            FactTypes.AnalysisGap
        };
        var designFacts = generated.Facts
            .Where(fact => designTypes.Contains(fact.FactType))
            .Select(fact => AddProvenance(
                fact,
                projected.Support,
                bundle.Manifest,
                designContractHash,
                baseScan.DatabasePath))
            .ToList();
        designFacts.Add(BundleFact(manifest, bundle.Manifest, designContractHash, baseScan.DatabasePath));

        var facts = baseScan.Facts
            .Concat(designFacts)
            .Select(fact => Recreate(manifest, fact))
            .GroupBy(fact => fact.FactId, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(fact => fact.FactType, StringComparer.Ordinal)
            .ThenBy(fact => fact.FactId, StringComparer.Ordinal)
            .ToArray();
        if (facts.Length > limits.MaxFacts)
            throw new AccessScanException("AccessFactLimitReached");
        return new ScanResult(
            manifest,
            facts,
            [new FileInventoryItem(baseScan.DatabasePath, "MicrosoftAccessDatabase", baseScan.DatabaseSize)]);
    }

    private static ProjectionResult Project(
        AccessDesignEvidenceBundle bundle,
        string databaseSeed,
        IReadOnlyList<CodeFact> baseFacts,
        AccessLimits limits)
    {
        var support = new Dictionary<string, IReadOnlyList<AccessDesignEvidenceRecord>>(StringComparer.Ordinal);
        var normalizationGaps = new List<AccessGapProjection>();
        var knownObjects = new Dictionary<string, List<(string StableKey, string Kind)>>(StringComparer.OrdinalIgnoreCase);
        var knownObjectsByHash = new Dictionary<string, List<(string StableKey, string Kind)>>(StringComparer.Ordinal);
        var fieldsByTable = new Dictionary<string, Dictionary<string, List<string>>>(StringComparer.Ordinal);
        var fieldsByTableAndHash = new Dictionary<string, Dictionary<string, List<string>>>(StringComparer.Ordinal);
        var stableKeyByCanonicalRecordId = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var fact in baseFacts)
        {
            if (fact.TargetSymbol is null) continue;
            if (fact.Properties.TryGetValue("objectName", out var name))
            {
                var kind = fact.FactType switch
                {
                    FactTypes.LegacyDataEntityDeclared => "table",
                    FactTypes.AccessQueryDeclared => "query",
                    _ => ""
                };
                if (kind.Length > 0) AddKnown(knownObjects, name, fact.TargetSymbol, kind);
            }
            if (fact.Properties.TryGetValue("objectNameHash", out var nameHash))
            {
                var kind = fact.FactType switch
                {
                    FactTypes.LegacyDataEntityDeclared => "table",
                    FactTypes.AccessQueryDeclared => "query",
                    _ => ""
                };
                if (kind.Length > 0) AddKnown(knownObjectsByHash, nameHash, fact.TargetSymbol, kind);
            }
            if (fact.FactType == FactTypes.LegacyDataColumnDeclared
                && fact.SourceSymbol is not null)
            {
                if (fact.Properties.TryGetValue("objectName", out var fieldName))
                    AddField(fieldsByTable, fact.SourceSymbol, fieldName, fact.TargetSymbol);
                if (fact.Properties.TryGetValue("objectNameHash", out var fieldHash))
                    AddField(fieldsByTableAndHash, fact.SourceSymbol, fieldHash, fact.TargetSymbol);
            }
            if (fact.FactType == FactTypes.AccessQueryOutputDeclared
                && fact.SourceSymbol is not null)
            {
                if (fact.Properties.TryGetValue("objectName", out var outputName))
                    AddField(fieldsByTable, fact.SourceSymbol, outputName, fact.TargetSymbol);
                if (fact.Properties.TryGetValue("objectNameHash", out var outputHash))
                    AddField(fieldsByTableAndHash, fact.SourceSymbol, outputHash, fact.TargetSymbol);
            }
        }

        foreach (var record in bundle.Records.Where(record =>
                     record.Kind == "catalog-object"
                     && String(record.Payload, "objectRole") is not ("table-field" or "query-field")))
        {
            var role = String(record.Payload, "objectRole");
            var identity = OptionalString(record.Payload, "identity");
            var declaredStableKey = OptionalString(record.Payload, "stableKey");
            var normalizedKind = NormalizeCatalogKind(role);
            var baseStableKey = identity is null
                ? null
                : ResolveBaseStableKey(identity, normalizedKind, knownObjects, knownObjectsByHash);
            var projectedStableKey = identity is null
                ? null
                : baseStableKey ?? AccessSafeValues.Identity(
                    databaseSeed,
                    normalizedKind,
                    identity,
                    disclosurePolicy: AccessIdentityDisclosurePolicy.HashOnly).StableKey;
            if (declaredStableKey is not null
                && projectedStableKey is not null
                && !string.Equals(declaredStableKey, projectedStableKey, StringComparison.Ordinal))
            {
                normalizationGaps.Add(new(
                    "AccessDesignInputCatalogIdentityMismatch",
                    role,
                    record.CanonicalRecordId,
                    RuleIds.LegacyAccessDesignInput));
                continue;
            }
            if (declaredStableKey is not null
                && projectedStableKey is null
                && !baseFacts.Any(fact => fact.SourceSymbol == declaredStableKey || fact.TargetSymbol == declaredStableKey))
            {
                normalizationGaps.Add(new(
                    "AccessDesignInputCatalogStableKeyUnmatched",
                    role,
                    record.CanonicalRecordId,
                    RuleIds.LegacyAccessDesignInput));
                continue;
            }
            var stableKey = projectedStableKey ?? declaredStableKey;
            if (identity is not null && stableKey is not null)
                AddKnown(knownObjects, identity, stableKey, normalizedKind);
            if (stableKey is not null)
            {
                stableKeyByCanonicalRecordId[record.CanonicalRecordId] = stableKey;
                SetSupport(support, stableKey, [record]);
            }
        }

        foreach (var record in bundle.Records.Where(record =>
                     record.Kind == "catalog-object"
                     && String(record.Payload, "objectRole") is "table-field" or "query-field"))
        {
            var fieldRole = String(record.Payload, "objectRole");
            var identity = OptionalString(record.Payload, "identity");
            var declaredStableKey = OptionalString(record.Payload, "stableKey");
            var tableStableKey = record.ParentCanonicalRecordId is not null
                && stableKeyByCanonicalRecordId.TryGetValue(record.ParentCanonicalRecordId, out var parentStableKey)
                    ? parentStableKey
                    : null;
            string? projectedStableKey = null;
            if (identity is not null && tableStableKey is not null)
            {
                var identityRole = fieldRole == "query-field" ? "query-field" : "field";
                var fieldHash = AccessSafeValues.RoleHash($"access-{identityRole}-{tableStableKey}-name", identity);
                if (fieldsByTableAndHash.TryGetValue(tableStableKey, out var fieldsByHash)
                    && fieldsByHash.TryGetValue(fieldHash, out var matches)
                    && matches.Distinct(StringComparer.Ordinal).ToArray() is { Length: 1 } distinct)
                {
                    projectedStableKey = distinct[0];
                    AddField(fieldsByTable, tableStableKey, identity, projectedStableKey);
                }
            }
            if (declaredStableKey is not null
                && projectedStableKey is not null
                && !string.Equals(declaredStableKey, projectedStableKey, StringComparison.Ordinal))
            {
                normalizationGaps.Add(new(
                    "AccessDesignInputCatalogIdentityMismatch",
                    fieldRole,
                    record.CanonicalRecordId,
                    RuleIds.LegacyAccessDesignInput));
                continue;
            }
            var stableKey = projectedStableKey ?? declaredStableKey;
            if (stableKey is null
                || !baseFacts.Any(fact => fact.TargetSymbol == stableKey
                    && fact.FactType == (fieldRole == "query-field"
                        ? FactTypes.AccessQueryOutputDeclared
                        : FactTypes.LegacyDataColumnDeclared)))
            {
                normalizationGaps.Add(new(
                    "AccessDesignInputCatalogStableKeyUnmatched",
                    fieldRole,
                    record.CanonicalRecordId,
                    RuleIds.LegacyAccessDesignInput));
                continue;
            }
            stableKeyByCanonicalRecordId[record.CanonicalRecordId] = stableKey;
            SetSupport(support, stableKey, [record]);
        }

        var surfaceInputs = new List<SurfaceInput>();
        var uiParserGaps = new List<AccessGapProjection>();
        foreach (var document in bundle.Records.Where(record => record.Kind == "ui-design-document"))
        {
            var kind = String(document.Payload, "documentRole");
            var catalogParent = bundle.Records.FirstOrDefault(record =>
                record.Kind == "catalog-object"
                && record.CanonicalRecordId == document.ParentCanonicalRecordId);
            var surfaceName = catalogParent is null
                ? document.CanonicalRecordId
                : OptionalString(catalogParent.Payload, "identity") ?? document.CanonicalRecordId;
            var parsed = AccessUiTextParser.Parse(
                new StringReader(String(document.Payload, "designText")),
                surfaceName,
                kind,
                limits);
            uiParserGaps.AddRange(parsed.Gaps.Select(gap =>
                gap with { StableScopeKey = gap.StableScopeKey ?? document.CanonicalRecordId }));
            if (parsed.Surface is not null)
            {
                surfaceInputs.Add(new(parsed.Surface with { Coverage = document.Completeness }, document));
            }
        }
        var structured = bundle.Records.Where(record => record.Kind == "ui-surface").ToArray();
        foreach (var surface in structured)
        {
            var additionalSurfaceEvents = bundle.Records
                .Where(record => record.Kind == "event-reference"
                    && record.ParentCanonicalRecordId == surface.CanonicalRecordId)
                .Select(record => new AccessRawUiEvent(
                    String(record.Payload, "eventRole"),
                    String(record.Payload, "value")));
            var controls = bundle.Records
                .Where(record => record.Kind == "ui-control"
                    && record.ParentCanonicalRecordId == surface.CanonicalRecordId)
                .Select(record => new AccessRawControl(
                    String(record.Payload, "identity"),
                    Int(record.Payload, "ordinal"),
                    Int(record.Payload, "controlType"),
                    OptionalString(record.Payload, "controlSource"),
                    OptionalString(record.Payload, "rowSource"),
                    Events(record.Payload).Concat(bundle.Records
                        .Where(item => item.Kind == "event-reference"
                            && item.ParentCanonicalRecordId == record.CanonicalRecordId)
                        .Select(item => new AccessRawUiEvent(
                            String(item.Payload, "eventRole"),
                            String(item.Payload, "value"))))
                        .OrderBy(item => item.Role, StringComparer.Ordinal).ToArray(),
                    OptionalString(record.Payload, "validationRule"),
                    OptionalString(record.Payload, "rowSourceType"),
                    OptionalInt(record.Payload, "boundColumn"),
                    OptionalInt(record.Payload, "columnCount"),
                    OptionalString(record.Payload, "sourceObject"),
                    OptionalString(record.Payload, "linkMasterFields"),
                    OptionalString(record.Payload, "linkChildFields")))
                .ToArray();
            var reportGroupRecords = bundle.Records
                .Where(record => record.Kind == "report-group"
                    && record.ParentCanonicalRecordId == surface.CanonicalRecordId)
                .ToArray();
            var reportGroups = reportGroupRecords
                .Select(record => new AccessRawReportGroup(
                    Int(record.Payload, "ordinal"),
                    OptionalString(record.Payload, "expression"),
                    OptionalString(record.Payload, "sortOrder"),
                    OptionalString(record.Payload, "groupOn")))
                .OrderBy(group => group.Ordinal)
                .ToArray();
            var raw = new AccessRawUiSurface(
                String(surface.Payload, "identity"),
                String(surface.Payload, "surfaceRole"),
                OptionalString(surface.Payload, "modulePresence") switch
                {
                    "present" => true,
                    "absent" => false,
                    _ => null
                },
                OptionalString(surface.Payload, "recordSource"),
                controls,
                Events(surface.Payload).Concat(additionalSurfaceEvents)
                    .OrderBy(item => item.Role, StringComparer.Ordinal).ToArray(),
                surface.Completeness,
                OptionalString(surface.Payload, "filter"),
                OptionalString(surface.Payload, "orderBy"),
                OptionalString(surface.Payload, "boundState"),
                reportGroups);
            surfaceInputs.Add(new(raw, new[] { surface }.Concat(reportGroupRecords).ToArray()));
        }
        var mergedSurfaceInputs = surfaceInputs
            .GroupBy(input => SurfaceStableKey(databaseSeed, input.Raw), StringComparer.Ordinal)
            .Select(group => MergeSurfaceInputs(group.Key, group.ToArray(), normalizationGaps))
            .OrderBy(input => SurfaceStableKey(databaseSeed, input.Raw), StringComparer.Ordinal)
            .ToArray();
        var rawSurfaces = mergedSurfaceInputs.Select(input => input.Raw).ToArray();
        var known = knownObjects.ToDictionary(
            item => item.Key,
            item => (IReadOnlyList<(string StableKey, string Kind)>)item.Value
                .Distinct().OrderBy(value => value.StableKey, StringComparer.Ordinal).ToArray(),
            StringComparer.OrdinalIgnoreCase);
        var fields = fieldsByTable.ToDictionary(
            table => table.Key,
            table => (IReadOnlyDictionary<string, IReadOnlyList<string>>)table.Value.ToDictionary(
                field => field.Key,
                field => (IReadOnlyList<string>)field.Value.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                StringComparer.OrdinalIgnoreCase),
            StringComparer.Ordinal);
        var ui = AccessUiProjector.Project(databaseSeed, rawSurfaces, known, fields, AccessIdentityDisclosurePolicy.HashOnly);
        var eventProcedureReferences = new List<AccessRawEventProcedureReference>();
        foreach (var input in mergedSurfaceInputs)
        {
            var raw = input.Raw;
            var identity = AccessSafeValues.Identity(databaseSeed,
                raw.SurfaceKind is "form" or "report" ? raw.SurfaceKind : "unknown",
                raw.Name,
                disclosurePolicy: AccessIdentityDisclosurePolicy.HashOnly);
            var sources = input.Records;
            var source = sources.First();
            SetSupport(support, identity.StableKey, sources);
            var projectedSurface = ui.Surfaces.Single(item => item.Identity.StableKey == identity.StableKey);
            foreach (var surfaceRecord in sources.Where(item => item.Kind == "ui-surface"))
                stableKeyByCanonicalRecordId[surfaceRecord.CanonicalRecordId] = projectedSurface.Identity.StableKey;
            foreach (var control in projectedSurface.Controls)
            {
                var controlRecords = bundle.Records.Where(record =>
                {
                    if (record.Kind != "ui-control"
                        || !sources.Any(item => item.CanonicalRecordId == record.ParentCanonicalRecordId)
                        || Int(record.Payload, "ordinal") != control.Ordinal)
                        return false;
                    return AccessSafeValues.Identity(
                        databaseSeed,
                        $"control-{projectedSurface.Identity.StableKey}",
                        String(record.Payload, "identity"),
                        control.Ordinal,
                        AccessIdentityDisclosurePolicy.HashOnly).StableKey == control.Identity.StableKey;
                }).ToArray();
                foreach (var controlRecord in controlRecords)
                    stableKeyByCanonicalRecordId[controlRecord.CanonicalRecordId] = control.Identity.StableKey;
                SetSupport(support, control.Identity.StableKey, controlRecords.DefaultIfEmpty(source));
            }
            AddUiChildSupport(projectedSurface, support, sources);
        }

        foreach (var eventRecord in bundle.Records.Where(record =>
                     record.Kind == "event-reference"
                     && string.Equals(String(record.Payload, "value").Trim(), "[Event Procedure]", StringComparison.OrdinalIgnoreCase)))
        {
            if (eventRecord.ParentCanonicalRecordId is null
                || !stableKeyByCanonicalRecordId.TryGetValue(eventRecord.ParentCanonicalRecordId, out var ownerStableKey))
                continue;
            var parent = bundle.Records.Single(record =>
                record.CanonicalRecordId == eventRecord.ParentCanonicalRecordId);
            var surface = parent.Kind == "ui-surface"
                ? parent
                : bundle.Records.Single(record =>
                    record.CanonicalRecordId == parent.ParentCanonicalRecordId);
            var surfaceKind = String(surface.Payload, "surfaceRole");
            var surfaceName = String(surface.Payload, "identity");
            var eventSuffix = EventProcedureSuffix(String(eventRecord.Payload, "eventRole"));
            var procedureOwner = parent.Kind == "ui-control"
                ? String(parent.Payload, "identity")
                : surfaceKind == "report" ? "Report" : "Form";
            eventProcedureReferences.Add(new(
                ownerStableKey,
                String(eventRecord.Payload, "eventRole"),
                $"{(surfaceKind == "report" ? "Report" : "Form")}_{surfaceName}",
                $"{procedureOwner}_{eventSuffix}"));
            SetSupport(support, ownerStableKey, [eventRecord]);
        }

        var rawModules = bundle.Records.Where(record => record.Kind == "vba-module")
            .Select(record => new AccessRawVbaModule(
                String(record.Payload, "identity"),
                String(record.Payload, "moduleKind"),
                String(record.Payload, "sourceText")))
            .ToArray();
        var vba = AccessVbaProjector.Project(
            databaseSeed,
            rawModules,
            eventReferences: eventProcedureReferences,
            knownObjects: known,
            limits: limits,
            disclosurePolicy: AccessIdentityDisclosurePolicy.HashOnly);
        foreach (var module in vba.Modules)
        {
            var records = bundle.Records
                .Where(item => item.Kind == "vba-module"
                    && AccessSafeValues.Identity(
                        databaseSeed,
                        "vba-module",
                        String(item.Payload, "identity"),
                        disclosurePolicy: AccessIdentityDisclosurePolicy.HashOnly).StableKey == module.Identity.StableKey)
                .OrderBy(item => item.CanonicalRecordId, StringComparer.Ordinal)
                .ToArray();
            SetSupport(support, module.Identity.StableKey, records);
            foreach (var procedure in module.Procedures)
            {
                SetSupport(support, procedure.Identity.StableKey, records);
                foreach (var call in procedure.Calls) SetSupport(support, call.Identity.StableKey, records);
            }
        }

        var rawMacros = bundle.Records.Where(record => record.Kind == "macro-inventory")
            .Select(record => new AccessRawMacro(
                String(record.Payload, "identity"),
                String(record.Payload, "macroCategory"),
                record.ParentCanonicalRecordId is null
                    ? null
                    : stableKeyByCanonicalRecordId.GetValueOrDefault(record.ParentCanonicalRecordId) ?? string.Empty,
                Int(record.Payload, "ordinal"),
                OptionalString(record.Payload, "startupRole"),
                OptionalString(record.Payload, "bodyStatus")))
            .ToArray();
        var macros = AccessMacroProjector.Project(databaseSeed, rawMacros, limits, AccessIdentityDisclosurePolicy.HashOnly);
        foreach (var macro in macros.Macros)
        {
            var records = bundle.Records
                .Where(item => item.Kind == "macro-inventory"
                    && Int(item.Payload, "ordinal") == macro.Ordinal
                    && String(item.Payload, "macroCategory") == macro.MacroKind
                    && (item.ParentCanonicalRecordId is null
                            ? null
                            : stableKeyByCanonicalRecordId.GetValueOrDefault(item.ParentCanonicalRecordId))
                        == macro.OwnerStableKey
                    && AccessSafeValues.Identity(
                        databaseSeed,
                        $"macro-{macro.MacroKind}-{macro.OwnerStableKey ?? "database"}",
                        String(item.Payload, "identity"),
                        macro.Ordinal,
                        AccessIdentityDisclosurePolicy.HashOnly).StableKey == macro.Identity.StableKey)
                .OrderBy(item => item.CanonicalRecordId, StringComparer.Ordinal)
                .ToArray();
            SetSupport(support, macro.Identity.StableKey, records);
        }

        var gaps = bundle.Gaps.Select(gap => new AccessGapProjection(
                gap.Classification, gap.ScopeKind, gap.CanonicalRecordId, RuleIds.LegacyAccessDesignInput))
            .Concat(bundle.Records.Where(record => record.Kind == "source-gap").Select(record =>
                new AccessGapProjection(
                    "AccessDesignInputSourceDeclaredPartial",
                    String(record.Payload, "affectedScope"),
                    record.CanonicalRecordId,
                    RuleIds.LegacyAccessDesignInput)))
            .Concat(normalizationGaps)
            .Concat(bundle.Manifest.CatalogCompleteness == "complete"
                ? []
                : new[] { new AccessGapProjection(
                    "AccessDesignInputCatalogPartial",
                    "catalog",
                    null,
                    RuleIds.LegacyAccessDesignInput) })
            .Concat(bundle.Records.Where(record => record.Source.CoordinateStatus == "unavailable")
                .Select(record => new AccessGapProjection(
                    "AccessDesignInputCoordinateUnavailable",
                    record.Kind,
                    record.CanonicalRecordId,
                    RuleIds.LegacyAccessDesignInput)))
            .Concat(uiParserGaps)
            .Concat(ui.Gaps)
            .Concat(vba.Gaps)
            .Concat(macros.Gaps)
            .OrderBy(gap => gap.Classification, StringComparer.Ordinal)
            .ThenBy(gap => gap.StableScopeKey, StringComparer.Ordinal)
            .ToArray();
        foreach (var record in bundle.Records) support.TryAdd(record.CanonicalRecordId, [record]);
        return new(ui, vba, macros, gaps, support);
    }

    private static void AddUiChildSupport(
        AccessUiSurfaceProjection surface,
        Dictionary<string, IReadOnlyList<AccessDesignEvidenceRecord>> support,
        IReadOnlyList<AccessDesignEvidenceRecord> fallback)
    {
        foreach (var binding in surface.Bindings) SetSupport(support, binding.Identity.StableKey, fallback);
        foreach (var control in surface.Controls)
        {
            var source = support.GetValueOrDefault(control.Identity.StableKey) ?? fallback;
            foreach (var binding in control.Bindings) SetSupport(support, binding.Identity.StableKey, source);
        }
    }

    private static CodeFact AddProvenance(
        CodeFact fact,
        IReadOnlyDictionary<string, IReadOnlyList<AccessDesignEvidenceRecord>> support,
        AccessDesignEvidenceManifestProjection manifest,
        string designContractHash,
        string databasePath)
    {
        var records = FindSupport(fact, support);
        var record = records.FirstOrDefault(item => item.Source.CoordinateStatus == "exact-lines")
            ?? records.FirstOrDefault();
        var properties = new SortedDictionary<string, string>(
            fact.Properties.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal),
            StringComparer.Ordinal)
        {
            ["designInputSchema"] = AccessDesignEvidenceReader.ManifestSchema,
            ["designInputHash"] = manifest.BundleSha256,
            ["designInputContractHash"] = designContractHash,
            ["designProducerId"] = manifest.ProducerId,
            ["designProducerVersion"] = manifest.ProducerVersion,
            ["designMechanism"] = manifest.Mechanism,
            ["copyBinding"] = manifest.CopyBinding,
            ["sourceCanonicalRecordIds"] = records.Count > 0
                ? string.Join(';', records.Select(item => item.CanonicalRecordId).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal))
                : BundleSupportId(designContractHash),
            ["coordinateStatus"] = record?.Source.CoordinateStatus ?? "unavailable",
            ["completenessStatus"] = record?.Completeness ?? "partial",
            ["nonClaims"] = "no-runtime-reachability;no-execution;no-row-access;no-business-intent;no-completeness;no-release-approval"
        };
        if (properties.TryGetValue("coverageLabel", out var projectorCoverage))
            properties["projectorCoverage"] = projectorCoverage;
        properties["coverageLabel"] = manifest.CopyBinding == "owner-attested-derived-copy"
            ? "copy-lineage-owner-attested"
            : record?.Completeness is "partial" or "unavailable"
                ? "source-declared-partial"
                : record?.Kind is "ui-design-document" or "vba-module"
                    ? "bounded-textual-design"
                    : "structured-design-observed";
        var evidence = SpanFor(fact, record, databasePath);
        var tier = fact.FactType == FactTypes.AnalysisGap
            ? EvidenceTiers.Tier4Unknown
            : record?.Kind is "ui-design-document" or "vba-module"
                ? EvidenceTiers.Tier3SyntaxOrTextual
                : fact.EvidenceTier;
        return fact with { Properties = properties, Evidence = evidence, EvidenceTier = tier };
    }

    private static IReadOnlyList<AccessDesignEvidenceRecord> FindSupport(
        CodeFact fact,
        IReadOnlyDictionary<string, IReadOnlyList<AccessDesignEvidenceRecord>> support)
    {
        foreach (var key in new[] { fact.TargetSymbol, fact.SourceSymbol,
                     fact.Properties.GetValueOrDefault("scopeStableKey"),
                     fact.Properties.GetValueOrDefault("ownerStableKey"),
                     fact.Properties.GetValueOrDefault("moduleStableKey"),
                     fact.Properties.GetValueOrDefault("procedureStableKey") })
            if (key is not null && support.TryGetValue(key, out var record))
                return record;
        return [];
    }

    private static EvidenceSpan SpanFor(CodeFact fact, AccessDesignEvidenceRecord? record, string databasePath)
    {
        if (record?.Source.CoordinateStatus != "exact-lines")
            return new(databasePath, 1, 1, null, ExtractorId, ComposerVersion);
        var start = record.Source.StartLine!.Value;
        var end = record.Source.EndLine!.Value;
        if (record.Kind == "vba-module"
            && fact.FactType is FactTypes.AccessVbaProcedureDeclared or FactTypes.AccessNavigationCandidate)
        {
            start += Math.Max(0, fact.Evidence.StartLine - 1);
            end = start + Math.Max(0, fact.Evidence.EndLine - fact.Evidence.StartLine);
            end = Math.Min(end, record.Source.EndLine.Value);
        }
        return new(databasePath, start, end, record.Source.DocumentSha256, ExtractorId, ComposerVersion);
    }

    private static CodeFact BundleFact(
        ScanManifest manifest,
        AccessDesignEvidenceManifestProjection bundle,
        string designContractHash,
        string databasePath) =>
        FactFactory.Create(
            manifest,
            FactTypes.AnalyzerCapabilityDiagnostic,
            RuleIds.LegacyAccessDesignInput,
            EvidenceTiers.Tier2Structural,
            new(databasePath, 1, 1, null, ExtractorId, ComposerVersion),
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["capability"] = "source-neutral-design-evidence",
                ["status"] = "accepted",
                ["coverageLabel"] = bundle.CatalogCompleteness == "complete"
                    ? "structured-design-observed"
                    : "source-declared-partial",
                ["designInputSchema"] = AccessDesignEvidenceReader.ManifestSchema,
                ["designInputHash"] = bundle.BundleSha256,
                ["designInputContractHash"] = designContractHash,
                ["designProducerId"] = bundle.ProducerId,
                ["designProducerVersion"] = bundle.ProducerVersion,
                ["designMechanism"] = bundle.Mechanism,
                ["copyBinding"] = bundle.CopyBinding,
                ["coordinateStatus"] = bundle.CoordinateCapability,
                ["sourceCanonicalRecordIds"] = BundleSupportId(designContractHash),
                ["limitations"] = "protected-input-not-persisted;static-design-only;no-runtime-or-completeness-proof",
                ["nonClaims"] = "no-runtime-reachability;no-execution;no-row-access;no-business-intent;no-release-approval"
            });

    private static string BundleSupportId(string designContractHash) =>
        $"access-design-bundle-{designContractHash[..32]}";

    private static CodeFact Recreate(ScanManifest manifest, CodeFact fact) =>
        FactFactory.Create(
            manifest,
            fact.FactType,
            fact.RuleId,
            fact.EvidenceTier,
            fact.Evidence,
            fact.ProjectPath,
            fact.SourceSymbol,
            fact.TargetSymbol,
            fact.ContractElement,
            fact.Properties);

    internal static async Task<BaseScan> ReadBaseScanAsync(
        string directory,
        AccessLimits limits,
        CancellationToken cancellationToken)
    {
        var root = Path.GetFullPath(directory);
        if (!Directory.Exists(root) || IsReparse(root))
            throw new AccessScanException("AccessBaseScanUnavailable");
        var manifestPath = Path.Combine(root, "scan-manifest.json");
        var factsPath = Path.Combine(root, "facts.ndjson");
        foreach (var required in new[] { manifestPath, factsPath, Path.Combine(root, "index.sqlite"), Path.Combine(root, "report.md"), Path.Combine(root, "logs", "analyzer.log") })
            if (!File.Exists(required) || IsReparse(required))
                throw new AccessScanException("AccessBaseScanIncomplete");
        var manifestBytes = await ReadBoundedAsync(manifestPath, limits.MaxArtifactBytes, cancellationToken);
        ScanManifest manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<ScanManifest>(manifestBytes, JsonOptions)
                ?? throw new AccessScanException("AccessBaseScanManifestInvalid");
        }
        catch (AccessScanException) { throw; }
        catch { throw new AccessScanException("AccessBaseScanManifestInvalid"); }
        var facts = new List<CodeFact>();
        var factsInfo = new FileInfo(factsPath);
        if (factsInfo.Length <= 0 || factsInfo.Length > limits.MaxArtifactBytes)
            throw new AccessScanException("AccessBaseScanArtifactLimitReached");
        await using (var stream = new FileStream(factsPath, FileMode.Open, FileAccess.Read, FileShare.Read))
        using (var reader = new StreamReader(stream, new UTF8Encoding(false, true), false))
        {
            while (await reader.ReadLineAsync(cancellationToken) is { } line)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (facts.Count >= limits.MaxFacts) throw new AccessScanException("AccessBaseScanFactLimitReached");
                try
                {
                    var fact = JsonSerializer.Deserialize<CodeFact>(line, JsonOptions)
                        ?? throw new AccessScanException("AccessBaseScanFactsInvalid");
                    if (fact.ScanId != manifest.ScanId || fact.CommitSha != manifest.CommitSha || fact.Repo != manifest.RepoName)
                        throw new AccessScanException("AccessBaseScanFactsMismatch");
                    facts.Add(fact);
                }
                catch (AccessScanException) { throw; }
                catch { throw new AccessScanException("AccessBaseScanFactsInvalid"); }
            }
        }
        var databases = facts.Where(fact =>
                fact.FactType == FactTypes.LegacyDataMetadataDeclared
                && fact.RuleId == RuleIds.LegacyAccessDatabaseInventory
                && fact.Properties.GetValueOrDefault("modelKind") == "access-database")
            .ToArray();
        var inventory = facts.Where(fact =>
                fact.FactType == FactTypes.FileInventoried
                && fact.RuleId == RuleIds.LegacyAccessDatabaseInventory
                && fact.Properties.GetValueOrDefault("fileKind") == "MicrosoftAccessDatabase")
            .ToArray();
        if (databases.Length != 1 || inventory.Length != 1
            || databases[0].TargetSymbol is null
            || inventory[0].Evidence.FilePath != databases[0].Evidence.FilePath
            || !inventory[0].Properties.TryGetValue("databaseHash", out var hash)
            || hash.Length != 64 || !hash.All(Uri.IsHexDigit)
            || !inventory[0].Properties.TryGetValue("sizeBytes", out var sizeText)
            || !long.TryParse(sizeText, out var size) || size <= 0)
            throw new AccessScanException("AccessBaseScanDatabaseUnavailable");
        var databasePath = inventory[0].Evidence.FilePath.Replace('\\', '/');
        if (Path.IsPathFullyQualified(databasePath)
            || databasePath.Split('/', StringSplitOptions.RemoveEmptyEntries).Any(segment => segment is "." or ".."))
            throw new AccessScanException("AccessBaseScanDatabaseUnavailable");
        return new(
            manifest,
            facts,
            Convert.ToHexString(SHA256.HashData(manifestBytes)).ToLowerInvariant(),
            databasePath,
            hash,
            databases[0].TargetSymbol!,
            size);
    }

    internal static async Task<byte[]> ReadBoundedAsync(string path, long maximum, CancellationToken cancellationToken)
    {
        var info = new FileInfo(path);
        if (info.Length <= 0 || info.Length > maximum || info.Length > int.MaxValue)
            throw new AccessScanException("AccessBaseScanArtifactLimitReached");
        var bytes = new byte[checked((int)info.Length)];
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var offset = 0;
        while (offset < bytes.Length)
        {
            var read = await stream.ReadAsync(bytes.AsMemory(offset), cancellationToken);
            if (read == 0) throw new AccessScanException("AccessBaseScanReadFailed");
            offset += read;
        }
        return bytes;
    }

    private static bool IsReparse(string path) =>
        (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;

    private static void AddKnown(
        Dictionary<string, List<(string StableKey, string Kind)>> values,
        string name,
        string stableKey,
        string kind)
    {
        if (!values.TryGetValue(name, out var entries)) values[name] = entries = [];
        entries.Add((stableKey, kind));
    }

    private static string? ResolveBaseStableKey(
        string identity,
        string kind,
        IReadOnlyDictionary<string, List<(string StableKey, string Kind)>> knownByName,
        IReadOnlyDictionary<string, List<(string StableKey, string Kind)>> knownByHash)
    {
        var matches = new List<string>();
        if (knownByName.TryGetValue(identity, out var named))
            matches.AddRange(named.Where(item => item.Kind == kind).Select(item => item.StableKey));
        var hash = AccessSafeValues.RoleHash($"access-{kind}-name", identity);
        if (knownByHash.TryGetValue(hash, out var hashed))
            matches.AddRange(hashed.Where(item => item.Kind == kind).Select(item => item.StableKey));
        var distinct = matches.Distinct(StringComparer.Ordinal).ToArray();
        return distinct.Length == 1 ? distinct[0] : null;
    }

    private static string SurfaceStableKey(string databaseSeed, AccessRawUiSurface surface) =>
        AccessSafeValues.Identity(
            databaseSeed,
            surface.SurfaceKind is "form" or "report" ? surface.SurfaceKind : "unknown",
            surface.Name,
            disclosurePolicy: AccessIdentityDisclosurePolicy.HashOnly).StableKey;

    private static SurfaceInput MergeSurfaceInputs(
        string stableKey,
        IReadOnlyList<SurfaceInput> inputs,
        List<AccessGapProjection> gaps)
    {
        if (inputs.Count == 1) return inputs[0];
        var ordered = inputs
            .OrderByDescending(item => item.Records[0].Kind == "ui-surface")
            .ThenBy(item => item.Records[0].CanonicalRecordId, StringComparer.Ordinal)
            .ToArray();
        var preferred = ordered[0].Raw;
        var conflict = false;
        T? MergeValue<T>(Func<AccessRawUiSurface, T?> selector) where T : struct
        {
            var values = ordered.Select(item => selector(item.Raw)).Where(value => value.HasValue)
                .Select(value => value!.Value).Distinct().ToArray();
            if (values.Length > 1) conflict = true;
            return values.Length == 1 ? values[0] : null;
        }
        string? MergeText(Func<AccessRawUiSurface, string?> selector)
        {
            var values = ordered.Select(item => selector(item.Raw))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!.Trim()).Distinct(StringComparer.Ordinal).ToArray();
            if (values.Length > 1) conflict = true;
            return values.Length == 1 ? values[0] : null;
        }
        T[] MergeGrouped<T, TKey>(
            IEnumerable<T> values,
            Func<T, TKey> keySelector,
            Func<T, T, bool> equivalent,
            Func<IEnumerable<T>, IOrderedEnumerable<T>> order)
            where TKey : notnull
        {
            var selected = values.GroupBy(keySelector)
            .Select(group =>
            {
                var observations = group.ToArray();
                if (observations.Skip(1).Any(item => !equivalent(observations[0], item)))
                    conflict = true;
                return observations[0];
            })
            .ToArray();
            return order(selected).ToArray();
        }
        var controls = MergeGrouped(
            ordered.SelectMany(item => item.Raw.Controls),
            item => (item.Ordinal, NameHash: AccessSafeValues.RoleHash("access-control-merge-name", item.Name)),
            EquivalentControl,
            values => values
                .OrderBy(item => item.Ordinal)
                .ThenBy(item => AccessSafeValues.RoleHash("access-control-sort-name", item.Name), StringComparer.Ordinal));
        var events = ordered.SelectMany(item => item.Raw.Events).Distinct()
            .OrderBy(item => item.Role, StringComparer.Ordinal)
            .ThenBy(item => item.Value, StringComparer.Ordinal)
            .ToArray();
        var reportGroups = MergeGrouped(
            ordered.SelectMany(item => item.Raw.ReportGroups ?? []),
            item => item.Ordinal,
            (left, right) => left == right,
            values => values.OrderBy(item => item.Ordinal));
        var merged = preferred with
        {
            HasModule = MergeValue(surface => surface.HasModule),
            RecordSource = MergeText(surface => surface.RecordSource),
            Controls = controls,
            Events = events,
            Coverage = conflict || ordered.Any(item => item.Raw.Coverage != "complete") ? "partial" : "complete",
            Filter = MergeText(surface => surface.Filter),
            OrderBy = MergeText(surface => surface.OrderBy),
            DeclaredBoundState = MergeText(surface => surface.DeclaredBoundState),
            ReportGroups = reportGroups
        };
        if (conflict)
            gaps.Add(new(
                "AccessDesignInputSurfaceConflict",
                "ui-surface",
                stableKey,
                RuleIds.LegacyAccessDesignInput));
        return new(merged, ordered.SelectMany(item => item.Records).ToArray());
    }

    private static bool EquivalentControl(AccessRawControl left, AccessRawControl right) =>
        left.Ordinal == right.Ordinal
        && left.ControlType == right.ControlType
        && string.Equals(left.Name.Trim(), right.Name.Trim(), StringComparison.Ordinal)
        && string.Equals(left.ControlSource?.Trim(), right.ControlSource?.Trim(), StringComparison.Ordinal)
        && string.Equals(left.RowSource?.Trim(), right.RowSource?.Trim(), StringComparison.Ordinal)
        && string.Equals(left.ValidationRule?.Trim(), right.ValidationRule?.Trim(), StringComparison.Ordinal)
        && string.Equals(left.RowSourceType?.Trim(), right.RowSourceType?.Trim(), StringComparison.Ordinal)
        && left.BoundColumn == right.BoundColumn
        && left.ColumnCount == right.ColumnCount
        && string.Equals(left.SourceObject?.Trim(), right.SourceObject?.Trim(), StringComparison.Ordinal)
        && string.Equals(left.LinkMasterFields?.Trim(), right.LinkMasterFields?.Trim(), StringComparison.Ordinal)
        && string.Equals(left.LinkChildFields?.Trim(), right.LinkChildFields?.Trim(), StringComparison.Ordinal)
        && left.Events
            .OrderBy(item => item.Role, StringComparer.Ordinal)
            .ThenBy(item => item.Value, StringComparer.Ordinal)
            .SequenceEqual(right.Events
                .OrderBy(item => item.Role, StringComparer.Ordinal)
                .ThenBy(item => item.Value, StringComparer.Ordinal));

    private static string EventProcedureSuffix(string eventRole) => eventRole switch
    {
        "after-update" => "AfterUpdate",
        "before-update" => "BeforeUpdate",
        "on-click" => "Click",
        "on-current" => "Current",
        "on-dbl-click" => "DblClick",
        "on-load" => "Load",
        "on-no-data" => "NoData",
        "on-open" => "Open",
        _ => throw new AccessScanException("AccessDesignInputRecordMalformed")
    };

    private static void SetSupport(
        IDictionary<string, IReadOnlyList<AccessDesignEvidenceRecord>> support,
        string key,
        IEnumerable<AccessDesignEvidenceRecord> records)
    {
        var existing = support.TryGetValue(key, out var value)
            ? value
            : Array.Empty<AccessDesignEvidenceRecord>();
        var combined = existing
            .Concat(records)
            .DistinctBy(item => item.CanonicalRecordId, StringComparer.Ordinal)
            .OrderBy(item => item.CanonicalRecordId, StringComparer.Ordinal)
            .ToArray();
        support[key] = combined;
    }

    private static void AddField(
        Dictionary<string, Dictionary<string, List<string>>> values,
        string table,
        string name,
        string stableKey)
    {
        if (!values.TryGetValue(table, out var fields))
            values[table] = fields = new(StringComparer.OrdinalIgnoreCase);
        if (!fields.TryGetValue(name, out var entries)) fields[name] = entries = [];
        entries.Add(stableKey);
    }

    private static string NormalizeCatalogKind(string value) => value switch
    {
        "saved-query" => "query",
        "table-field" => "field",
        "query-field" => "field",
        _ => value
    };

    private static IReadOnlyList<AccessRawUiEvent> Events(JsonElement payload)
    {
        if (!payload.TryGetProperty("events", out var events)) return [];
        return events.EnumerateObject()
            .OrderBy(item => item.Name, StringComparer.Ordinal)
            .Select(item => new AccessRawUiEvent(item.Name, item.Value.GetString()))
            .ToArray();
    }

    private static string String(JsonElement payload, string name) =>
        payload.GetProperty(name).GetString() ?? string.Empty;

    private static string? OptionalString(JsonElement payload, string name) =>
        payload.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static int Int(JsonElement payload, string name) =>
        payload.GetProperty(name).GetInt32();

    private static int? OptionalInt(JsonElement payload, string name) =>
        payload.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : null;

    internal sealed record BaseScan(
        ScanManifest Manifest,
        IReadOnlyList<CodeFact> Facts,
        string ManifestSha256,
        string DatabasePath,
        string DatabaseHash,
        string DatabaseStableKey,
        long DatabaseSize);

    private sealed record SurfaceInput(
        AccessRawUiSurface Raw,
        IReadOnlyList<AccessDesignEvidenceRecord> Records)
    {
        public SurfaceInput(AccessRawUiSurface raw, AccessDesignEvidenceRecord record) : this(raw, [record]) { }
    }

    private sealed record ProjectionResult(
        AccessUiProjectionResult Ui,
        AccessVbaProjectionResult Vba,
        AccessMacroProjectionResult Macros,
        IReadOnlyList<AccessGapProjection> Gaps,
        IReadOnlyDictionary<string, IReadOnlyList<AccessDesignEvidenceRecord>> Support);
}
