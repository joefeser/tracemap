namespace TraceMap.Core;

/// <summary>
/// Closed vocabulary for the EF6 CLR-to-EDMX symbol-composition rule
/// (legacy.data.edmx.symbol-composition.v1). Values are contract surface:
/// they are persisted on facts, catalogued in rules/rule-catalog.yml, and
/// asserted by tests.
/// </summary>
internal static class EdmxSymbolCompositionVocabulary
{
    public const string MapsToConceptualEntity = nameof(MapsToConceptualEntity);
    public const string MapsToConceptualProperty = nameof(MapsToConceptualProperty);
    public const string MapsToStorageTable = nameof(MapsToStorageTable);
    public const string MapsToStorageColumn = nameof(MapsToStorageColumn);

    public static readonly IReadOnlyList<string> RelationshipKinds =
    [
        MapsToConceptualEntity,
        MapsToConceptualProperty,
        MapsToStorageTable,
        MapsToStorageColumn
    ];

    public const string RelationshipSource = "edmx-symbol-composition";

    public const string BridgeMechanismSemanticAttribute = "semantic-attribute";
    public const string BridgeMechanismGenerationMetadata = "generation-metadata";
    public const string BridgeMechanismGeneratedFileScope = "generated-file-scope";

    public const string ScopeRuleSameDirectoryDesignerFile = "same-directory-designer-file";

    public const string TargetSymbolLanguage = "edmx";

    public const string TargetKindConceptualEntityType = "EdmxConceptualEntityType";
    public const string TargetKindConceptualProperty = "EdmxConceptualProperty";
    public const string TargetKindStorageEntitySet = "EdmxStorageEntitySet";
    public const string TargetKindStorageColumn = "EdmxStorageColumn";

    public const string ClassificationAmbiguousClrSymbolReconciliation = "AmbiguousClrSymbolReconciliation";
    public const string ClassificationClrSymbolEvidenceUnavailable = "ClrSymbolEvidenceUnavailable";
    public const string ClassificationUnresolvedGeneratedNamespace = "UnresolvedGeneratedNamespace";
    public const string ClassificationMissingSemanticPropertyEvidence = "MissingSemanticPropertyEvidence";

    public const string LimitationStaticDesignTime = "edmx-static-design-time";
    public const string LimitationGeneratedCodeFreshnessUnverified = "generated-code-freshness-unverified";
    public const string LimitationConceptualDescriptorStructural = "conceptual-descriptor-structural";
    public const string LimitationNamespaceBridgeStructural = "namespace-bridge-structural";
    public const string LimitationStorageJoinStructural = "storage-join-structural";
}

/// <summary>
/// Post-extraction composition stage for legacy.data.edmx.symbol-composition.v1.
/// Consumes shipped EDMX descriptor facts and Tier1 semantic declaration facts
/// (never re-parsing the EDMX), resolves the namespace reconciliation ladder,
/// emits the generated-file scope bridge fact, composed SymbolRelationship
/// edges, and fail-closed AnalysisGap facts.
/// </summary>
internal static class LegacyDataEdmxSymbolComposition
{
    private const string ExtractorIdName = "LegacyDataEdmxSymbolComposition";

    public static IReadOnlyList<CodeFact> Extract(
        ScanManifest manifest,
        IReadOnlyList<FileInventoryItem> inventory,
        IReadOnlyList<CodeFact> facts,
        IReadOnlySet<string> semanticallyAnalyzedFiles)
    {
        var output = new List<CodeFact>();
        var tier1Declarations = facts
            .Where(fact => fact.FactType == FactTypes.TypeDeclared
                && fact.RuleId == RuleIds.CSharpSemanticDeclarations
                && fact.EvidenceTier == EvidenceTiers.Tier1Semantic
                && fact.Properties.ContainsKey("targetSymbolId"))
            .OrderBy(fact => fact.FactId, StringComparer.Ordinal)
            .ToArray();
        var semanticProperties = facts
            .Where(fact => fact.FactType == FactTypes.PropertyDeclared
                && fact.RuleId == RuleIds.CSharpSemanticDeclarations
                && fact.EvidenceTier == EvidenceTiers.Tier1Semantic
                && fact.Properties.ContainsKey("targetSymbolId")
                && fact.Properties.ContainsKey("containingTypeSymbolId"))
            .OrderBy(fact => fact.FactId, StringComparer.Ordinal)
            .ToArray();

        foreach (var metadataFact in facts
                     .Where(IsEdmxMetadataFact)
                     .OrderBy(fact => fact.Evidence.FilePath, StringComparer.Ordinal)
                     .ThenBy(fact => fact.FactId, StringComparer.Ordinal))
        {
            ComposeEdmx(manifest, inventory, facts, semanticallyAnalyzedFiles, tier1Declarations, semanticProperties, metadataFact, output);
        }

        return output;
    }

    private static bool IsEdmxMetadataFact(CodeFact fact) =>
        fact.FactType == FactTypes.LegacyDataMetadataDeclared
        && fact.Properties.GetValueOrDefault("metadataFormat") == "edmx";

    private static void ComposeEdmx(
        ScanManifest manifest,
        IReadOnlyList<FileInventoryItem> inventory,
        IReadOnlyList<CodeFact> facts,
        IReadOnlySet<string> semanticallyAnalyzedFiles,
        IReadOnlyList<CodeFact> tier1Declarations,
        IReadOnlyList<CodeFact> semanticProperties,
        CodeFact metadataFact,
        List<CodeFact> output)
    {
        var gapKeys = new HashSet<string>(StringComparer.Ordinal);
        var path = metadataFact.Evidence.FilePath;
        var scopedFiles = ScopeDesignerFiles(inventory, path);
        CodeFact? scopeFact = null;
        if (scopedFiles.Count > 0)
        {
            scopeFact = FactFactory.Create(
                manifest,
                FactTypes.LegacyDataGeneratedFileScope,
                RuleIds.LegacyDataEdmxSymbolComposition,
                EvidenceTiers.Tier2Structural,
                new EvidenceSpan(path, 1, 1, FactFactory.Hash($"{path}:generated-file-scope", 32), ExtractorIdName, ScannerVersions.LegacyDataSymbolComposition),
                targetSymbol: Path.GetFileName(path),
                properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["scopeRule"] = EdmxSymbolCompositionVocabulary.ScopeRuleSameDirectoryDesignerFile,
                    ["scopedFilePaths"] = string.Join(";", scopedFiles),
                    ["sourceMetadataFactId"] = metadataFact.FactId,
                    ["coverageLabel"] = "full"
                });
            output.Add(scopeFact);
        }

        EmitOutOfScopeGaps(manifest, facts, path, output, gapKeys);

        var entitiesAvailable = true;
        if (tier1Declarations.Count == 0)
        {
            AddCompositionGap(manifest, output, gapKeys, path, metadataFact.Evidence.StartLine,
                EdmxSymbolCompositionVocabulary.ClassificationClrSymbolEvidenceUnavailable,
                "No compiler-resolved symbol evidence exists in this scan; EF6 EDMX symbol composition is unavailable.");
            entitiesAvailable = false;
        }
        else if (scopedFiles.Count > 0 && !scopedFiles.Any(semanticallyAnalyzedFiles.Contains))
        {
            AddCompositionGap(manifest, output, gapKeys, path, metadataFact.Evidence.StartLine,
                EdmxSymbolCompositionVocabulary.ClassificationClrSymbolEvidenceUnavailable,
                "The EDMX generated-file scope was not semantically analyzed; failed analysis is not reported as missing generated code.");
            entitiesAvailable = false;
        }

        if (!entitiesAvailable)
        {
            return;
        }

        var allScopedFilesCovered = scopedFiles.Count == 0 || scopedFiles.All(semanticallyAnalyzedFiles.Contains);
        var scopedDeclarations = tier1Declarations
            .Where(fact => scopedFiles.Contains(fact.Evidence.FilePath))
            .ToArray();
        var ssdlEntitySets = facts
            .Where(fact => fact.FactType == FactTypes.LegacyDataStorageObjectDeclared
                && fact.Properties.GetValueOrDefault("descriptorKind") == "ssdl-entity-set"
                && fact.Properties.ContainsKey("stableModelKey")
                && fact.Evidence.FilePath == path)
            .OrderBy(fact => fact.Evidence.StartLine)
            .ThenBy(fact => fact.FactId, StringComparer.Ordinal)
            .ToArray();
        var ssdlColumns = facts
            .Where(fact => fact.FactType == FactTypes.LegacyDataColumnDeclared
                && fact.Properties.GetValueOrDefault("sourceSection") == "SSDL"
                && fact.Properties.ContainsKey("stableModelKey")
                && fact.Evidence.FilePath == path)
            .OrderBy(fact => fact.Evidence.StartLine)
            .ThenBy(fact => fact.FactId, StringComparer.Ordinal)
            .ToArray();
        var entitySetMappings = facts
            .Where(fact => fact.FactType == FactTypes.LegacyDataMappingDeclared
                && fact.Properties.GetValueOrDefault("mappingKind") == "entity-table"
                && fact.Evidence.FilePath == path)
            .OrderBy(fact => fact.Evidence.StartLine)
            .ThenBy(fact => fact.FactId, StringComparer.Ordinal)
            .ToArray();
        var propertyMappings = facts
            .Where(fact => fact.FactType == FactTypes.LegacyDataMappingDeclared
                && fact.Properties.GetValueOrDefault("mappingKind") == "property-column"
                && fact.Evidence.FilePath == path)
            .OrderBy(fact => fact.Evidence.StartLine)
            .ThenBy(fact => fact.FactId, StringComparer.Ordinal)
            .ToArray();
        var entitySets = facts
            .Where(fact => fact.FactType == FactTypes.LegacyDataEntityDeclared
                && fact.Properties.GetValueOrDefault("descriptorKind") == "csdl-entity-set"
                && fact.Evidence.FilePath == path)
            .OrderBy(fact => fact.Evidence.StartLine)
            .ThenBy(fact => fact.FactId, StringComparer.Ordinal)
            .ToArray();
        var csdlProperties = facts
            .Where(fact => fact.FactType == FactTypes.LegacyDataColumnDeclared
                && fact.Properties.GetValueOrDefault("sourceSection") == "CSDL"
                && fact.Evidence.FilePath == path)
            .OrderBy(fact => fact.Evidence.StartLine)
            .ThenBy(fact => fact.FactId, StringComparer.Ordinal)
            .ToArray();

        foreach (var entity in facts
                     .Where(fact => fact.FactType == FactTypes.LegacyDataEntityDeclared
                         && fact.Properties.GetValueOrDefault("descriptorKind") == "csdl-entity"
                         && (fact.Properties.GetValueOrDefault("limitations") ?? string.Empty).Contains("unsupported-inherited-model-shape") is false
                         && fact.Evidence.FilePath == path)
                     .OrderBy(fact => fact.Evidence.StartLine)
                     .ThenBy(fact => fact.FactId, StringComparer.Ordinal))
        {
            ComposeEntity(
                manifest,
                entity,
                path,
                tier1Declarations,
                scopedFiles,
                scopedDeclarations,
                allScopedFilesCovered,
                semanticProperties,
                entitySets,
                csdlProperties,
                entitySetMappings,
                propertyMappings,
                ssdlEntitySets,
                ssdlColumns,
                scopeFact,
                output,
                gapKeys);
        }
    }

    private static void ComposeEntity(
        ScanManifest manifest,
        CodeFact entity,
        string path,
        IReadOnlyList<CodeFact> tier1Declarations,
        IReadOnlySet<string> scopedFiles,
        IReadOnlyList<CodeFact> scopedDeclarations,
        bool allScopedFilesCovered,
        IReadOnlyList<CodeFact> semanticProperties,
        IReadOnlyList<CodeFact> entitySets,
        IReadOnlyList<CodeFact> csdlProperties,
        IReadOnlyList<CodeFact> entitySetMappings,
        IReadOnlyList<CodeFact> propertyMappings,
        IReadOnlyList<CodeFact> ssdlEntitySets,
        IReadOnlyList<CodeFact> ssdlColumns,
        CodeFact? scopeFact,
        List<CodeFact> output,
        HashSet<string> gapKeys)
    {
        var namespaceClear = entity.Properties.GetValueOrDefault("containerName");
        var nameClear = entity.Properties.GetValueOrDefault("entityName");

        var mechanism1 = tier1Declarations
            .Where(declaration => declaration.Properties.ContainsKey("generatedConceptualName") || declaration.Properties.ContainsKey("generatedConceptualNameHash"))
            .Where(declaration => ValuesMatch(
                declaration.Properties, "generatedConceptualNamespace", "generatedConceptualNamespaceHash",
                entity.Properties, "containerName", "containerHash"))
            .ToArray();
        var mechanism1Qualified = mechanism1
            .Where(declaration => ValuesMatch(
                declaration.Properties, "generatedConceptualName", "generatedConceptualNameHash",
                entity.Properties, "entityName", "entityHash"))
            .ToArray();

        var mechanism3 = namespaceClear is null
            ? Array.Empty<CodeFact>()
            : scopedDeclarations
                .Where(declaration => string.Equals(
                    QualifiedName(declaration.Properties.GetValueOrDefault("namespace") ?? string.Empty, declaration.Properties.GetValueOrDefault("name") ?? string.Empty),
                    QualifiedName(namespaceClear, nameClear ?? string.Empty),
                    StringComparison.Ordinal))
                .ToArray();

        CodeFact? bridge;
        string bridgeMechanism;
        List<CodeFact> candidates;
        if (mechanism1Qualified.Length > 0)
        {
            candidates = mechanism1Qualified.ToList();
            bridge = null;
            bridgeMechanism = EdmxSymbolCompositionVocabulary.BridgeMechanismSemanticAttribute;
        }
        else if (mechanism3.Length > 0)
        {
            candidates = mechanism3.ToList();
            bridge = scopeFact;
            bridgeMechanism = EdmxSymbolCompositionVocabulary.BridgeMechanismGeneratedFileScope;
        }
        else
        {
            AddNoCandidateGap(manifest, entity, path, scopedFiles, scopedDeclarations, allScopedFilesCovered, output, gapKeys);
            return;
        }

        var distinctSymbols = candidates
            .GroupBy(declaration => declaration.Properties["targetSymbolId"], StringComparer.Ordinal)
            .ToArray();
        var distinctScopes = candidates
            .Select(declaration => (declaration.Properties["targetSymbolId"], declaration.ProjectPath ?? string.Empty))
            .Distinct()
            .ToArray();
        if (distinctSymbols.Length != 1 || distinctScopes.Length != 1)
        {
            AddCompositionGap(manifest, output, gapKeys, path, entity.Evidence.StartLine,
                EdmxSymbolCompositionVocabulary.ClassificationAmbiguousClrSymbolReconciliation,
                $"The CLR symbol for conceptual entity {Display(entity.Properties, "entityName", "entityHash")} did not resolve to exactly one canonical symbol in one compilation scope.");
            return;
        }

        var declaration = candidates
            .OrderBy(fact => fact.Evidence.StartLine)
            .ThenBy(fact => fact.FactId, StringComparer.Ordinal)
            .First();
        bridge ??= declaration;

        var coverage = WeakestCoverage(entity.Properties.GetValueOrDefault("coverageLabel"), declaration.Properties.GetValueOrDefault("coverageLabel"), scopeFact?.Properties.GetValueOrDefault("coverageLabel"));
        output.Add(CreateComposedRelationship(
            manifest,
            EdmxSymbolCompositionVocabulary.MapsToConceptualEntity,
            declaration,
            Display(entity.Properties, "entityName", "entityHash"),
            EdmxSymbolCompositionVocabulary.TargetKindConceptualEntityType,
            entity.Properties.GetValueOrDefault("stableModelKey") ?? string.Empty,
            entity.Evidence.FilePath,
            entity.Evidence.StartLine,
            entity.Evidence.EndLine,
            [declaration.FactId, bridge.FactId, entity.FactId],
            bridge.FactId,
            bridgeMechanism,
            coverage,
            withConceptualLimitation: true,
            withBridgeStructuralLimitation: bridgeMechanism != EdmxSymbolCompositionVocabulary.BridgeMechanismSemanticAttribute,
            withStorageLimitation: false));

        var canonicalName = namespaceClear is null ? null : QualifiedName(namespaceClear, nameClear ?? string.Empty);
        var resolvedSet = entitySets
            .Where(set =>
            {
                var reference = set.Properties.GetValueOrDefault("entityTypeReference");
                return reference is not null
                    ? string.Equals(reference, canonicalName, StringComparison.Ordinal)
                    : ValuesMatch(set.Properties, "entityTypeName", "entityTypeHash", entity.Properties, "entityName", "entityHash");
            })
            .ToArray();
        if (resolvedSet.Length != 1)
        {
            AddCompositionGap(manifest, output, gapKeys, path, entity.Evidence.StartLine,
                "AmbiguousLegacyDataModelIdentity",
                $"The CSDL entity set for conceptual entity {Display(entity.Properties, "entityName", "entityHash")} did not resolve to exactly one candidate.");
            return;
        }

        var entitySet = resolvedSet[0];
        var mapping = entitySetMappings
            .Where(candidate => ValuesMatch(candidate.Properties, "entityName", "entityHash", entitySet.Properties, "entityName", "entityHash"))
            .ToArray();
        if (mapping.Length == 0)
        {
            return;
        }

        if (mapping.Length > 1)
        {
            AddCompositionGap(manifest, output, gapKeys, path, entity.Evidence.StartLine,
                "AmbiguousLegacyDataModelIdentity",
                $"Multiple MSL entity-table mappings matched entity set {Display(entitySet.Properties, "entityName", "entityHash")}.");
            return;
        }

        var entityTable = mapping[0];
        if (entityTable.Properties.GetValueOrDefault("typeNamePresent") != "True")
        {
            AddCompositionGap(manifest, output, gapKeys, path, entityTable.Evidence.StartLine,
                "AmbiguousLegacyDataModelIdentity",
                $"EntityTypeMapping TypeName was missing for entity set {Display(entitySet.Properties, "entityName", "entityHash")}.");
            return;
        }

        if (entityTable.Properties.GetValueOrDefault("typeNameIsTypeOf") == "True")
        {
            AddCompositionGap(manifest, output, gapKeys, path, entityTable.Evidence.StartLine,
                "UnsupportedLegacyOrmMappingShape",
                "IsTypeOf hierarchy mappings are outside the composed EF6 entity chains.");
            return;
        }

        if (namespaceClear is not null
            && !ValuesMatch(entityTable.Properties, "resolvedConceptualTypeName", "resolvedConceptualTypeHash",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["resolvedConceptualTypeName"] = QualifiedName(namespaceClear, nameClear ?? string.Empty)
                },
                "resolvedConceptualTypeName",
                "resolvedConceptualTypeHash"))
        {
            AddCompositionGap(manifest, output, gapKeys, path, entityTable.Evidence.StartLine,
                "AmbiguousLegacyDataModelIdentity",
                $"EntityTypeMapping TypeName did not resolve to conceptual entity {Display(entity.Properties, "entityName", "entityHash")}.");
            return;
        }

        if (entityTable.Properties.GetValueOrDefault("storeEntitySetResolved") != "True"
            || !entityTable.Properties.TryGetValue("storageEntityTypeIdentity", out var storageIdentity))
        {
            AddCompositionGap(manifest, output, gapKeys, path, entityTable.Evidence.StartLine,
                "AmbiguousLegacyDataModelIdentity",
                $"MappingFragment StoreEntitySet did not resolve through the SSDL storage container for entity set {Display(entitySet.Properties, "entityName", "entityHash")}.");
            return;
        }

        var storeSets = ssdlEntitySets
            .Where(candidate => string.Equals(candidate.Properties.GetValueOrDefault("storageEntityTypeIdentity"), storageIdentity, StringComparison.Ordinal))
            .ToArray();
        if (storeSets.Length != 1)
        {
            AddCompositionGap(manifest, output, gapKeys, path, entityTable.Evidence.StartLine,
                "AmbiguousLegacyDataModelIdentity",
                $"The SSDL entity set for entity set {Display(entitySet.Properties, "entityName", "entityHash")} did not resolve to exactly one candidate.");
            return;
        }

        var storeSet = storeSets[0];
        output.Add(CreateComposedRelationship(
            manifest,
            EdmxSymbolCompositionVocabulary.MapsToStorageTable,
            declaration,
            Display(storeSet.Properties, "storageObjectName", "storageObjectHash"),
            EdmxSymbolCompositionVocabulary.TargetKindStorageEntitySet,
            storeSet.Properties.GetValueOrDefault("stableModelKey") ?? string.Empty,
            entityTable.Evidence.FilePath,
            entityTable.Evidence.StartLine,
            entityTable.Evidence.EndLine,
            [declaration.FactId, bridge.FactId, entity.FactId, entitySet.FactId, entityTable.FactId, storeSet.FactId],
            bridge.FactId,
            bridgeMechanism,
            WeakestCoverage(coverage, entityTable.Properties.GetValueOrDefault("coverageLabel"), storeSet.Properties.GetValueOrDefault("coverageLabel")),
            withConceptualLimitation: false,
            withBridgeStructuralLimitation: bridgeMechanism != EdmxSymbolCompositionVocabulary.BridgeMechanismSemanticAttribute,
            withStorageLimitation: true));

        ComposeProperties(
            manifest,
            entity,
            entitySet,
            storeSet,
            entityTable,
            storageIdentity,
            declaration,
            bridge,
            bridgeMechanism,
            path,
            semanticProperties,
            csdlProperties,
            propertyMappings,
            ssdlColumns,
            output,
            gapKeys);
    }

    private static void ComposeProperties(
        ScanManifest manifest,
        CodeFact entity,
        CodeFact entitySet,
        CodeFact storeSet,
        CodeFact entityTable,
        string storageIdentity,
        CodeFact declaration,
        CodeFact bridge,
        string bridgeMechanism,
        string path,
        IReadOnlyList<CodeFact> semanticProperties,
        IReadOnlyList<CodeFact> csdlProperties,
        IReadOnlyList<CodeFact> propertyMappings,
        IReadOnlyList<CodeFact> ssdlColumns,
        List<CodeFact> output,
        HashSet<string> gapKeys)
    {
        var symbolId = declaration.Properties["targetSymbolId"];
        var conceptualProperties = propertyMappings
            .Where(mapping => ValuesMatch(mapping.Properties, "entityName", "entityHash", entitySet.Properties, "entityName", "entityHash"))
            .ToArray();
        foreach (var propertyMapping in conceptualProperties)
        {
            var clrProperties = semanticProperties
                .Where(property => string.Equals(property.Properties.GetValueOrDefault("containingTypeSymbolId"), symbolId, StringComparison.Ordinal)
                    && ValuesMatch(property.Properties, "name", "propertyNameHash", propertyMapping.Properties, "propertyName", "propertyHash"))
                .ToArray();
            if (clrProperties.Length == 0)
            {
                AddCompositionGap(manifest, output, gapKeys, path, propertyMapping.Evidence.StartLine,
                    EdmxSymbolCompositionVocabulary.ClassificationMissingSemanticPropertyEvidence,
                    $"No compiler-resolved property symbol matched member {Display(propertyMapping.Properties, "propertyName", "propertyHash")}; name attachment is not used.");
                continue;
            }

            if (clrProperties.Select(property => property.Properties["targetSymbolId"]).Distinct(StringComparer.Ordinal).Count() > 1)
            {
                AddCompositionGap(manifest, output, gapKeys, path, propertyMapping.Evidence.StartLine,
                    EdmxSymbolCompositionVocabulary.ClassificationAmbiguousClrSymbolReconciliation,
                    $"Multiple canonical property symbols matched member {Display(propertyMapping.Properties, "propertyName", "propertyHash")}.");
                continue;
            }

            var clrProperty = clrProperties[0];
            var conceptualCandidates = csdlProperties
                .Where(property => property.Evidence.FilePath == entity.Evidence.FilePath
                    && ValuesMatch(property.Properties, "entityName", "entityHash", entity.Properties, "entityName", "entityHash")
                    && SchemaNamespaceMatches(property.Properties, entity.Properties)
                    && property.Properties.GetValueOrDefault("descriptorKind") != "NavigationProperty"
                    && ValuesMatch(property.Properties, "propertyName", "propertyHash", propertyMapping.Properties, "propertyName", "propertyHash"))
                .ToArray();
            if (conceptualCandidates.Length != 1)
            {
                AddCompositionGap(manifest, output, gapKeys, path, propertyMapping.Evidence.StartLine,
                    "AmbiguousLegacyDataModelIdentity",
                    $"The CSDL property for member {Display(propertyMapping.Properties, "propertyName", "propertyHash")} did not resolve to exactly one candidate on the reconciled entity.");
                continue;
            }

            var conceptualProperty = conceptualCandidates[0];
            output.Add(CreateComposedRelationship(
                manifest,
                EdmxSymbolCompositionVocabulary.MapsToConceptualProperty,
                clrProperty,
                Display(propertyMapping.Properties, "propertyName", "propertyHash"),
                EdmxSymbolCompositionVocabulary.TargetKindConceptualProperty,
                conceptualProperty.Properties.GetValueOrDefault("stableModelKey") ?? string.Empty,
                clrProperty.Evidence.FilePath,
                clrProperty.Evidence.StartLine,
                clrProperty.Evidence.EndLine,
                [clrProperty.FactId, bridge.FactId, entity.FactId, conceptualProperty.FactId],
                bridge.FactId,
                bridgeMechanism,
                WeakestCoverage(entity.Properties.GetValueOrDefault("coverageLabel"), clrProperty.Properties.GetValueOrDefault("coverageLabel"), conceptualProperty.Properties.GetValueOrDefault("coverageLabel")),
                withConceptualLimitation: true,
                withBridgeStructuralLimitation: bridgeMechanism != EdmxSymbolCompositionVocabulary.BridgeMechanismSemanticAttribute,
                withStorageLimitation: false));

            var storageColumns = ssdlColumns
                .Where(column => string.Equals(column.Properties.GetValueOrDefault("storageEntityTypeIdentity"), storageIdentity, StringComparison.Ordinal)
                    && ValuesMatch(column.Properties, "columnName", "columnHash", propertyMapping.Properties, "columnName", "columnHash"))
                .ToArray();
            if (storageColumns.Length != 1)
            {
                AddCompositionGap(manifest, output, gapKeys, path, propertyMapping.Evidence.StartLine,
                    "AmbiguousLegacyDataModelIdentity",
                    $"The SSDL column for member {Display(propertyMapping.Properties, "propertyName", "propertyHash")} did not resolve to exactly one candidate within the mapped storage type.");
                continue;
            }

            var storageColumn = storageColumns[0];
            output.Add(CreateComposedRelationship(
                manifest,
                EdmxSymbolCompositionVocabulary.MapsToStorageColumn,
                clrProperty,
                Display(storageColumn.Properties, "columnName", "columnHash"),
                EdmxSymbolCompositionVocabulary.TargetKindStorageColumn,
                storageColumn.Properties.GetValueOrDefault("stableModelKey") ?? string.Empty,
                propertyMapping.Evidence.FilePath,
                propertyMapping.Evidence.StartLine,
                propertyMapping.Evidence.EndLine,
                [clrProperty.FactId, bridge.FactId, entity.FactId, entitySet.FactId, conceptualProperty.FactId, entityTable.FactId, propertyMapping.FactId, storeSet.FactId, storageColumn.FactId],
                bridge.FactId,
                bridgeMechanism,
                WeakestCoverage(entity.Properties.GetValueOrDefault("coverageLabel"), entitySet.Properties.GetValueOrDefault("coverageLabel"), conceptualProperty.Properties.GetValueOrDefault("coverageLabel"), entityTable.Properties.GetValueOrDefault("coverageLabel"), propertyMapping.Properties.GetValueOrDefault("coverageLabel"), storeSet.Properties.GetValueOrDefault("coverageLabel"), storageColumn.Properties.GetValueOrDefault("coverageLabel")),
                withConceptualLimitation: false,
                withBridgeStructuralLimitation: bridgeMechanism != EdmxSymbolCompositionVocabulary.BridgeMechanismSemanticAttribute,
                withStorageLimitation: true));
        }
    }

    private static void AddNoCandidateGap(
        ScanManifest manifest,
        CodeFact entity,
        string path,
        IReadOnlySet<string> scopedFiles,
        IReadOnlyList<CodeFact> scopedDeclarations,
        bool allScopedFilesCovered,
        List<CodeFact> output,
        HashSet<string> gapKeys)
    {
        var displayName = Display(entity.Properties, "entityName", "entityHash");
        string classification;
        string message;
        if (scopedFiles.Count == 0 || (scopedDeclarations.Count == 0 && allScopedFilesCovered))
        {
            classification = "MissingGeneratedCode";
            message = $"No generated CLR type was found for conceptual entity {displayName}.";
        }
        else if (scopedDeclarations.Count == 0)
        {
            classification = EdmxSymbolCompositionVocabulary.ClassificationClrSymbolEvidenceUnavailable;
            message = $"The generated-file scope for conceptual entity {displayName} has no compiler-resolved declarations and lacks confirmed semantic coverage.";
        }
        else if (scopedDeclarations.Any(declaration => ValuesMatch(
                     declaration.Properties, "name", null as string,
                     entity.Properties, "entityName", "entityHash")))
        {
            classification = EdmxSymbolCompositionVocabulary.ClassificationUnresolvedGeneratedNamespace;
            message = $"Generated CLR declarations exist for {displayName} under a divergent namespace with no deterministic bridge.";
        }
        else
        {
            classification = "MissingGeneratedCode";
            message = $"No generated CLR type was found for conceptual entity {displayName}.";
        }

        AddCompositionGap(manifest, output, gapKeys, path, entity.Evidence.StartLine, classification, message);
    }

    private static void EmitOutOfScopeGaps(ScanManifest manifest, IReadOnlyList<CodeFact> facts, string path, List<CodeFact> output, HashSet<string> gapKeys)
    {
        foreach (var association in facts
                     .Where(fact => fact.FactType == FactTypes.LegacyDataMappingDeclared
                         && fact.Properties.GetValueOrDefault("descriptorKind") == "msl-association"
                         && fact.Evidence.FilePath == path)
                     .OrderBy(fact => fact.Evidence.StartLine)
                     .ThenBy(fact => fact.FactId, StringComparer.Ordinal))
        {
            AddCompositionGap(manifest, output, gapKeys, path, association.Evidence.StartLine,
                "UnsupportedLegacyOrmMappingShape",
                $"Association mapping {Display(association.Properties, "associationName", "associationHash")} is outside the composed EF6 entity and property chains.");
        }

        foreach (var routine in facts
                     .Where(fact => fact.FactType == FactTypes.LegacyDataStorageObjectDeclared
                         && fact.Properties.GetValueOrDefault("storageObjectKind") == "Routine"
                         && fact.Evidence.FilePath == path)
                     .OrderBy(fact => fact.Evidence.StartLine)
                     .ThenBy(fact => fact.FactId, StringComparer.Ordinal))
        {
            AddCompositionGap(manifest, output, gapKeys, path, routine.Evidence.StartLine,
                "UnsupportedLegacyOrmMappingShape",
                $"Provider-defined routine {Display(routine.Properties, "storageObjectName", "storageObjectHash")} is outside the composed EF6 entity and property chains.");
        }
    }

    private static IReadOnlySet<string> ScopeDesignerFiles(IReadOnlyList<FileInventoryItem> inventory, string edmxRelativePath)
    {
        var directory = Path.GetDirectoryName(edmxRelativePath) ?? string.Empty;
        var baseName = Path.GetFileNameWithoutExtension(edmxRelativePath);
        var expectedFileName = baseName + ".Designer.cs";
        return inventory
            .Where(item => item.Kind == "CSharp"
                && string.Equals(Path.GetDirectoryName(item.RelativePath) ?? string.Empty, directory, StringComparison.Ordinal)
                && string.Equals(Path.GetFileName(item.RelativePath), expectedFileName, StringComparison.Ordinal))
            .Select(item => item.RelativePath)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static void AddCompositionGap(ScanManifest manifest, List<CodeFact> output, HashSet<string> gapKeys, string path, int line, string classification, string message)
    {
        var key = $"{path}\u001f{line}\u001f{classification}\u001f{message}";
        if (!gapKeys.Add(key))
        {
            return;
        }

        output.Add(FactFactory.Create(
            manifest,
            FactTypes.AnalysisGap,
            RuleIds.LegacyDataEdmxSymbolComposition,
            EvidenceTiers.Tier4Unknown,
            new EvidenceSpan(path, line, line, FactFactory.Hash($"{path}:{line}:{classification}:{message}", 32), ExtractorIdName, ScannerVersions.LegacyDataSymbolComposition),
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["classification"] = classification,
                ["coverage"] = "reduced",
                ["message"] = message
            }));
    }

    private static CodeFact CreateComposedRelationship(
        ScanManifest manifest,
        string relationshipKind,
        CodeFact declaration,
        string targetDisplayName,
        string targetSymbolKind,
        string targetKey,
        string path,
        int startLine,
        int endLine,
        IReadOnlyList<string> supportingFactIds,
        string bridgeFactId,
        string bridgeMechanism,
        string coverageLabel,
        bool withConceptualLimitation,
        bool withBridgeStructuralLimitation,
        bool withStorageLimitation)
    {
        var limitations = new List<string>
        {
            EdmxSymbolCompositionVocabulary.LimitationStaticDesignTime,
            EdmxSymbolCompositionVocabulary.LimitationGeneratedCodeFreshnessUnverified
        };
        if (withConceptualLimitation)
        {
            limitations.Add(EdmxSymbolCompositionVocabulary.LimitationConceptualDescriptorStructural);
        }

        if (withBridgeStructuralLimitation)
        {
            limitations.Add(EdmxSymbolCompositionVocabulary.LimitationNamespaceBridgeStructural);
        }

        if (withStorageLimitation)
        {
            limitations.Add(EdmxSymbolCompositionVocabulary.LimitationStorageJoinStructural);
        }

        var properties = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["relationshipKind"] = relationshipKind,
            ["relationshipSource"] = EdmxSymbolCompositionVocabulary.RelationshipSource,
            ["sourceSymbol"] = declaration.TargetSymbol ?? string.Empty,
            ["sourceSymbolId"] = declaration.Properties.GetValueOrDefault("targetSymbolId") ?? string.Empty,
            ["sourceSymbolLanguage"] = "csharp",
            ["sourceSymbolKind"] = declaration.Properties.GetValueOrDefault("targetSymbolKind") ?? "NamedType",
            ["sourceSymbolDisplayName"] = declaration.Properties.GetValueOrDefault("targetSymbolDisplayName") ?? declaration.TargetSymbol ?? string.Empty,
            ["targetSymbol"] = targetDisplayName,
            ["targetSymbolId"] = targetKey,
            ["targetSymbolLanguage"] = EdmxSymbolCompositionVocabulary.TargetSymbolLanguage,
            ["targetSymbolKind"] = targetSymbolKind,
            ["targetSymbolDisplayName"] = targetDisplayName,
            ["namespaceBridgeFactId"] = bridgeFactId,
            ["namespaceBridgeMechanism"] = bridgeMechanism,
            ["supportingFactIds"] = string.Join(";", supportingFactIds.Distinct(StringComparer.Ordinal)),
            ["coverageLabel"] = coverageLabel,
            ["limitations"] = string.Join(";", limitations)
        };
        var assemblyName = declaration.Properties.GetValueOrDefault("targetSymbolAssemblyName");
        if (assemblyName is not null)
        {
            properties["sourceSymbolAssemblyName"] = assemblyName;
        }

        var assemblyVersion = declaration.Properties.GetValueOrDefault("targetSymbolAssemblyVersion");
        if (assemblyVersion is not null)
        {
            properties["sourceSymbolAssemblyVersion"] = assemblyVersion;
        }

        var containingSymbolId = declaration.Properties.GetValueOrDefault("targetSymbolContainingSymbolId");
        if (containingSymbolId is not null)
        {
            properties["sourceSymbolContainingSymbolId"] = containingSymbolId;
        }

        return FactFactory.Create(
            manifest,
            FactTypes.SymbolRelationship,
            RuleIds.LegacyDataEdmxSymbolComposition,
            EvidenceTiers.Tier2Structural,
            new EvidenceSpan(path, startLine, endLine, null, ExtractorIdName, ScannerVersions.LegacyDataSymbolComposition),
            projectPath: declaration.ProjectPath,
            sourceSymbol: declaration.TargetSymbol,
            targetSymbol: targetDisplayName,
            contractElement: relationshipKind,
            properties: properties);
    }

    private static string QualifiedName(string namespaceName, string name) =>
        string.IsNullOrWhiteSpace(namespaceName) ? name : $"{namespaceName}.{name}";

    private static string Display(IReadOnlyDictionary<string, string> properties, string clearKey, string hashKey) =>
        properties.GetValueOrDefault(clearKey)
            ?? (properties.TryGetValue(hashKey, out var hash) ? $"hash:{hash}" : "unknown");

    private static string WeakestCoverage(params string?[] labels)
    {
        var values = labels.Where(label => !string.IsNullOrWhiteSpace(label)).ToArray();
        if (values.Length == 0)
        {
            return "full";
        }

        if (values.Any(label => string.Equals(label, "reduced", StringComparison.Ordinal)))
        {
            return "reduced";
        }

        return values.All(label => string.Equals(label, "full", StringComparison.Ordinal)) ? "full" : "unknown";
    }

    private static bool SchemaNamespaceMatches(IReadOnlyDictionary<string, string> property, IReadOnlyDictionary<string, string> entity)
    {
        var propertyNamespace = property.GetValueOrDefault("schemaNamespace");
        var entityNamespace = entity.GetValueOrDefault("containerName");
        if (propertyNamespace is null && entityNamespace is null)
        {
            return true;
        }

        return ValuesMatch(property, "schemaNamespace", "schemaNamespaceHash", entity, "containerName", "containerHash");
    }

    private static bool ValuesMatch(
        IReadOnlyDictionary<string, string> left,
        string leftClear,
        string? leftHash,
        IReadOnlyDictionary<string, string> right,
        string rightClear,
        string rightHash)
    {
        var leftValue = left.GetValueOrDefault(leftClear);
        var rightValue = right.GetValueOrDefault(rightClear);
        if (leftValue is not null && rightValue is not null)
        {
            return string.Equals(leftValue, rightValue, StringComparison.Ordinal);
        }

        var leftHashValue = leftHash is null ? null : left.GetValueOrDefault(leftHash);
        var rightHashValue = right.GetValueOrDefault(rightHash);
        if (leftValue is not null && rightHashValue is not null)
        {
            return string.Equals(FactFactory.Hash(leftValue, 32), rightHashValue, StringComparison.Ordinal);
        }

        if (rightValue is not null && leftHashValue is not null)
        {
            return string.Equals(FactFactory.Hash(rightValue, 32), leftHashValue, StringComparison.Ordinal);
        }

        return false;
    }
}
