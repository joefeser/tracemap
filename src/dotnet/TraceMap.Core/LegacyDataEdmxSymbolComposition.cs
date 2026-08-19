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
