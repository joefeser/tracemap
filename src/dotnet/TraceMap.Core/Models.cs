namespace TraceMap.Core;

public sealed record ScanManifest(
    string ScanId,
    string RepoName,
    string? RemoteUrl,
    string? Branch,
    string CommitSha,
    string ScannerVersion,
    DateTimeOffset ScannedAt,
    string AnalysisLevel,
    string BuildStatus,
    IReadOnlyList<string> Solutions,
    IReadOnlyList<string> Projects,
    IReadOnlyList<string> TargetFrameworks,
    IReadOnlyList<string> KnownGaps,
    string? ScanRootRelativePath = null,
    string? ScanRootPathHash = null,
    string? GitRootHash = null);

public sealed record EvidenceSpan(
    string FilePath,
    int StartLine,
    int EndLine,
    string? SnippetHash,
    string ExtractorId,
    string ExtractorVersion);

public sealed record CodeFact(
    string FactId,
    string ScanId,
    string Repo,
    string CommitSha,
    string? ProjectPath,
    string FactType,
    string RuleId,
    string EvidenceTier,
    string? SourceSymbol,
    string? TargetSymbol,
    string? ContractElement,
    EvidenceSpan Evidence,
    IReadOnlyDictionary<string, string> Properties);

public sealed record SymbolIdentity(
    string SymbolId,
    string Language,
    string SymbolKind,
    string DisplayName,
    string? AssemblyName,
    string? AssemblyVersion,
    string? ContainingSymbolId);

public sealed record SymbolOccurrence(
    string SymbolId,
    string FactId,
    string Role,
    string OccurrenceKind,
    EvidenceSpan Evidence);

public sealed record SymbolRelationship(
    string SourceSymbolId,
    string TargetSymbolId,
    string RelationshipKind,
    string RuleId,
    EvidenceSpan Evidence);

public sealed record ScanOptions(
    string RepoPath,
    string OutputPath,
    IReadOnlyList<string>? SolutionPaths = null,
    IReadOnlyList<string>? ProjectPaths = null,
    IReadOnlyList<string>? IncludeGlobs = null,
    IReadOnlyList<string>? ExcludeGlobs = null,
    string? TargetFramework = null,
    bool Restore = false);

public sealed record FileInventoryItem(
    string RelativePath,
    string Kind,
    long SizeBytes);

public sealed record GitMetadata(
    string RepoName,
    string? RemoteUrl,
    string? Branch,
    string CommitSha,
    IReadOnlyList<string> KnownGaps,
    string? GitRootPath = null);

public sealed record ScanResult(
    ScanManifest Manifest,
    IReadOnlyList<CodeFact> Facts,
    IReadOnlyList<FileInventoryItem> Inventory);

public static class EvidenceTiers
{
    public const string Tier1Semantic = nameof(Tier1Semantic);
    public const string Tier2Structural = nameof(Tier2Structural);
    public const string Tier3SyntaxOrTextual = nameof(Tier3SyntaxOrTextual);
    public const string Tier4Unknown = nameof(Tier4Unknown);
}

public static class FactTypes
{
    public const string RepoScanned = nameof(RepoScanned);
    public const string BuildStatus = nameof(BuildStatus);
    public const string AnalysisGap = nameof(AnalysisGap);
    public const string BuildEnvironmentDiagnostic = nameof(BuildEnvironmentDiagnostic);
    public const string AnalyzerCapabilityDiagnostic = nameof(AnalyzerCapabilityDiagnostic);
    public const string FileInventoried = nameof(FileInventoried);
    public const string SolutionDeclared = nameof(SolutionDeclared);
    public const string ProjectDeclared = nameof(ProjectDeclared);
    public const string PackageReferenced = nameof(PackageReferenced);
    public const string TargetFrameworkDeclared = nameof(TargetFrameworkDeclared);
    public const string ConfigFileDeclared = nameof(ConfigFileDeclared);
    public const string SqlFileDeclared = nameof(SqlFileDeclared);
    public const string TypeDeclared = nameof(TypeDeclared);
    public const string MethodDeclared = nameof(MethodDeclared);
    public const string PropertyDeclared = nameof(PropertyDeclared);
    public const string FieldDeclared = nameof(FieldDeclared);
    public const string ParameterDeclared = nameof(ParameterDeclared);
    public const string LocalAlias = nameof(LocalAlias);
    public const string FieldAlias = nameof(FieldAlias);
    public const string DependencyResolved = nameof(DependencyResolved);
    public const string DeserializedObject = nameof(DeserializedObject);
    public const string ReflectionUsage = nameof(ReflectionUsage);
    public const string DynamicInvocation = nameof(DynamicInvocation);
    public const string CollectionMutation = nameof(CollectionMutation);
    public const string ObjectMutation = nameof(ObjectMutation);
    public const string BranchCondition = nameof(BranchCondition);
    public const string CallbackBoundary = nameof(CallbackBoundary);
    public const string AsyncBoundary = nameof(AsyncBoundary);
    public const string DependencyRegistered = nameof(DependencyRegistered);
    public const string SerializerContractMember = nameof(SerializerContractMember);
    public const string ReflectionTarget = nameof(ReflectionTarget);
    public const string DynamicDispatchCandidate = nameof(DynamicDispatchCandidate);
    public const string CollectionElementFlow = nameof(CollectionElementFlow);
    public const string MutationSemantics = nameof(MutationSemantics);
    public const string BranchFeasibility = nameof(BranchFeasibility);
    public const string SymbolRelationship = nameof(SymbolRelationship);
    public const string HttpRouteBinding = nameof(HttpRouteBinding);
    public const string DatabaseColumnMapping = nameof(DatabaseColumnMapping);
    public const string ConfigBinding = nameof(ConfigBinding);
    public const string EnumDeclared = nameof(EnumDeclared);
    public const string AttributeUsed = nameof(AttributeUsed);
    public const string MemberAccessName = nameof(MemberAccessName);
    public const string InvocationName = nameof(InvocationName);
    public const string CallEdge = nameof(CallEdge);
    public const string ObjectCreated = nameof(ObjectCreated);
    public const string ArgumentPassed = nameof(ArgumentPassed);
    public const string CalculationExpression = nameof(CalculationExpression);
    public const string BranchingLogic = nameof(BranchingLogic);
    public const string RetryPolicyLogic = nameof(RetryPolicyLogic);
    public const string SerializationLogic = nameof(SerializationLogic);
    public const string InfrastructureBoilerplate = nameof(InfrastructureBoilerplate);
    public const string QueryPatternDetected = nameof(QueryPatternDetected);
    public const string ObjectShapeInferred = nameof(ObjectShapeInferred);
    public const string PropertyAccessed = nameof(PropertyAccessed);
    public const string MethodInvoked = nameof(MethodInvoked);
    public const string HttpCallDetected = nameof(HttpCallDetected);
    public const string HttpClientCreated = nameof(HttpClientCreated);
    public const string DbContextDeclared = nameof(DbContextDeclared);
    public const string DbSetDeclared = nameof(DbSetDeclared);
    public const string DbChangeSaved = nameof(DbChangeSaved);
    public const string DapperCallDetected = nameof(DapperCallDetected);
    public const string SqlCommandDetected = nameof(SqlCommandDetected);
    public const string SqlTextUsed = nameof(SqlTextUsed);
    public const string ConfigKeyDeclared = nameof(ConfigKeyDeclared);
    public const string ConnectionStringDeclared = nameof(ConnectionStringDeclared);
    public const string WcfClientEndpointDeclared = nameof(WcfClientEndpointDeclared);
    public const string WcfServiceEndpointDeclared = nameof(WcfServiceEndpointDeclared);
    public const string WcfServiceContractDeclared = nameof(WcfServiceContractDeclared);
    public const string WcfOperationContractDeclared = nameof(WcfOperationContractDeclared);
    public const string WcfGeneratedClientDeclared = nameof(WcfGeneratedClientDeclared);
    public const string WcfServiceHostDeclared = nameof(WcfServiceHostDeclared);
    public const string WcfServiceReferenceMetadataDeclared = nameof(WcfServiceReferenceMetadataDeclared);
    public const string WcfMetadataOperationDeclared = nameof(WcfMetadataOperationDeclared);
    public const string WcfServiceReferenceMapping = nameof(WcfServiceReferenceMapping);
    public const string AsmxHostDeclared = nameof(AsmxHostDeclared);
    public const string AsmxServiceClassDeclared = nameof(AsmxServiceClassDeclared);
    public const string AsmxOperationDeclared = nameof(AsmxOperationDeclared);
    public const string AsmxSoapOperationDeclared = nameof(AsmxSoapOperationDeclared);
    public const string AsmxGeneratedClientDeclared = nameof(AsmxGeneratedClientDeclared);
    public const string AsmxClientOperationDeclared = nameof(AsmxClientOperationDeclared);
    public const string AsmxProxyMetadataDeclared = nameof(AsmxProxyMetadataDeclared);
    public const string AsmxConfigDeclared = nameof(AsmxConfigDeclared);
    public const string AsmxServiceReferenceMapping = nameof(AsmxServiceReferenceMapping);
    public const string RemotingApiUsageDeclared = nameof(RemotingApiUsageDeclared);
    public const string RemotingMarshalByRefObjectDeclared = nameof(RemotingMarshalByRefObjectDeclared);
    public const string RemotingChannelDeclared = nameof(RemotingChannelDeclared);
    public const string RemotingChannelRegistered = nameof(RemotingChannelRegistered);
    public const string RemotingServiceTypeRegistered = nameof(RemotingServiceTypeRegistered);
    public const string RemotingClientTypeRegistered = nameof(RemotingClientTypeRegistered);
    public const string RemotingClientActivationDeclared = nameof(RemotingClientActivationDeclared);
    public const string RemotingConfigSectionDeclared = nameof(RemotingConfigSectionDeclared);
    public const string RemotingConfigChannelDeclared = nameof(RemotingConfigChannelDeclared);
    public const string RemotingConfigServiceDeclared = nameof(RemotingConfigServiceDeclared);
    public const string RemotingConfigClientDeclared = nameof(RemotingConfigClientDeclared);
    public const string RemotingConfigProviderDeclared = nameof(RemotingConfigProviderDeclared);
    public const string WebFormsPageDeclared = nameof(WebFormsPageDeclared);
    public const string WebFormsControlDeclared = nameof(WebFormsControlDeclared);
    public const string WebFormsEventBindingDeclared = nameof(WebFormsEventBindingDeclared);
    public const string WebFormsDesignerControlDeclared = nameof(WebFormsDesignerControlDeclared);
    public const string WebFormsHandlerResolved = nameof(WebFormsHandlerResolved);
    public const string WebFormsEventFlowProjected = nameof(WebFormsEventFlowProjected);
    public const string WebFormsLogicSignalDetected = nameof(WebFormsLogicSignalDetected);
    public const string WinFormsSurfaceDeclared = nameof(WinFormsSurfaceDeclared);
    public const string WinFormsControlDeclared = nameof(WinFormsControlDeclared);
    public const string WinFormsEventBindingDeclared = nameof(WinFormsEventBindingDeclared);
    public const string WinFormsHandlerResolved = nameof(WinFormsHandlerResolved);
    public const string WinFormsNavigationEdgeDeclared = nameof(WinFormsNavigationEdgeDeclared);
    public const string WinFormsCallbackBoundaryDeclared = nameof(WinFormsCallbackBoundaryDeclared);
    public const string WinFormsHandlerFlowProjected = nameof(WinFormsHandlerFlowProjected);
    public const string WinFormsResourceMetadataDeclared = nameof(WinFormsResourceMetadataDeclared);
    public const string AspNetSurfaceDeclared = nameof(AspNetSurfaceDeclared);
    public const string AspNetRouteDeclared = nameof(AspNetRouteDeclared);
    public const string AspNetConfigSurfaceDeclared = nameof(AspNetConfigSurfaceDeclared);
    public const string AspNetHandlerDeclared = nameof(AspNetHandlerDeclared);
    public const string AspNetPageMethodDeclared = nameof(AspNetPageMethodDeclared);
    public const string AspNetNavigationReferenceDeclared = nameof(AspNetNavigationReferenceDeclared);
    public const string AspNetNavigationEdgeDeclared = nameof(AspNetNavigationEdgeDeclared);
    public const string LegacyDataMetadataDeclared = nameof(LegacyDataMetadataDeclared);
    public const string LegacyDataEntityDeclared = nameof(LegacyDataEntityDeclared);
    public const string LegacyDataStorageObjectDeclared = nameof(LegacyDataStorageObjectDeclared);
    public const string LegacyDataColumnDeclared = nameof(LegacyDataColumnDeclared);
    public const string LegacyDataMappingDeclared = nameof(LegacyDataMappingDeclared);
    public const string LegacyDataProviderConfigDeclared = nameof(LegacyDataProviderConfigDeclared);
    public const string LegacyDataGeneratedCodeLinked = nameof(LegacyDataGeneratedCodeLinked);
    public const string UiTemplateBinding = nameof(UiTemplateBinding);
    public const string UiFormControlBinding = nameof(UiFormControlBinding);
    public const string UiEventBinding = nameof(UiEventBinding);
    public const string UiTemplateVariable = nameof(UiTemplateVariable);
    public const string UiBindingGap = nameof(UiBindingGap);
    public const string RazorBinding = nameof(RazorBinding);
    public const string RazorFormTarget = nameof(RazorFormTarget);
    public const string RazorModelBindingTarget = nameof(RazorModelBindingTarget);
    public const string RazorBindingGap = nameof(RazorBindingGap);
    public const string MessagePublisherSurface = nameof(MessagePublisherSurface);
    public const string MessageConsumerSurface = nameof(MessageConsumerSurface);
    public const string MessageBindingDeclared = nameof(MessageBindingDeclared);
}

public static class RuleIds
{
    public const string RepoManifest = "repo.manifest.v1";
    public const string FileInventory = "file.inventory.v1";
    public const string ProjectFile = "project.file.v1";
    public const string CSharpSyntaxDeclarations = "csharp.syntax.declarations.v1";
    public const string CSharpSyntaxMemberAccess = "csharp.syntax.memberaccess.v1";
    public const string CSharpSyntaxInvocation = "csharp.syntax.invocation.v1";
    public const string CSharpSyntaxCallGraph = "csharp.syntax.callgraph.v1";
    public const string CSharpSyntaxObjectCreation = "csharp.syntax.objectcreation.v1";
    public const string CSharpSyntaxLogicShape = "csharp.syntax.logicshape.v1";
    public const string CSharpSyntaxQueryPattern = "csharp.syntax.querypattern.v1";
    public const string CSharpSyntaxObjectShape = "csharp.syntax.objectshape.v1";
    public const string CSharpSyntaxAspNetRoute = "csharp.syntax.aspnetroute.v1";
    public const string CSharpSemanticDeclarations = "csharp.semantic.declarations.v1";
    public const string CSharpSemanticPropertyAccess = "csharp.semantic.propertyaccess.v1";
    public const string CSharpSemanticMethodInvocation = "csharp.semantic.methodinvocation.v1";
    public const string CSharpSemanticCallGraph = "csharp.semantic.callgraph.v1";
    public const string CSharpSemanticObjectCreation = "csharp.semantic.objectcreation.v1";
    public const string CSharpSemanticValueFlow = "csharp.semantic.valueflow.v1";
    public const string CSharpSemanticLocalAlias = "csharp.semantic.localalias.v1";
    public const string CSharpSemanticFieldAlias = "csharp.semantic.fieldalias.v1";
    public const string CSharpSemanticParameterForwarding = "csharp.semantic.parameterforwarding.v1";
    public const string CSharpSemanticSymbolIdentity = "csharp.semantic.symbolidentity.v1";
    public const string CSharpSemanticSymbolRelationship = "csharp.semantic.symbolrelationship.v1";
    public const string CSharpSemanticContractMapping = "csharp.semantic.contractmapping.v1";
    public const string CSharpSemanticFlowBoundary = "csharp.semantic.flowboundary.v1";
    public const string CSharpSemanticRuntimeEvidence = "csharp.semantic.runtimeevidence.v1";
    public const string CSharpSemanticWorkspace = "csharp.semantic.workspace.v1";
    public const string BuildEnvironmentTargetFramework = "build.environment.target-framework.v1";
    public const string BuildEnvironmentToolset = "build.environment.toolset.v1";
    public const string BuildEnvironmentProjectFormat = "build.environment.project-format.v1";
    public const string BuildEnvironmentRestore = "build.environment.restore.v1";
    public const string BuildEnvironmentGeneratedFiles = "build.environment.generated-files.v1";
    public const string BuildEnvironmentWorkspaceDiagnostic = "build.environment.workspace-diagnostic.v1";
    public const string AnalyzerCapabilitySemantic = "analyzer.capability.semantic.v1";
    public const string AnalyzerCapabilitySyntaxFallback = "analyzer.capability.syntax-fallback.v1";
    public const string AnalyzerCapabilityProjectConfig = "analyzer.capability.project-config.v1";
    public const string AnalyzerCapabilityPackageRestore = "analyzer.capability.package-restore.v1";
    public const string AnalyzerCapabilityGeneratedDesignTime = "analyzer.capability.generated-design-time.v1";
    public const string AnalyzerCapabilityLegacyToolchain = "analyzer.capability.legacy-toolchain.v1";
    public const string AnalyzerCapabilityDownstreamCoverage = "analyzer.capability.downstream-coverage.v1";
    public const string HttpClientInvocation = "http.client.invocation.v1";
    public const string DatabaseEntityFramework = "database.ef.v1";
    public const string DatabaseDapperInvocation = "database.dapper.invocation.v1";
    public const string DatabaseSqlText = "database.sql.text.v1";
    public const string DatabaseSqlShape = "database.sql.shape.v1";
    public const string ConfigKey = "config.key.v1";
    public const string LegacyWcfConfig = "legacy.wcf.config.v1";
    public const string LegacyWcfContract = "legacy.wcf.contract.v1";
    public const string LegacyWcfHost = "legacy.wcf.host.v1";
    public const string LegacyWcfMetadata = "legacy.wcf.metadata.v1";
    public const string LegacyWcfOperationNormalization = "legacy.wcf.operation-normalization.v1";
    public const string LegacyWcfMapping = "legacy.wcf.mapping.v1";
    public const string LegacyAsmxHost = "legacy.asmx.host.v1";
    public const string LegacyAsmxService = "legacy.asmx.service.v1";
    public const string LegacyAsmxOperation = "legacy.asmx.operation.v1";
    public const string LegacyAsmxClient = "legacy.asmx.client.v1";
    public const string LegacyAsmxMetadata = "legacy.asmx.metadata.v1";
    public const string LegacyAsmxConfig = "legacy.asmx.config.v1";
    public const string LegacyAsmxMapping = "legacy.asmx.mapping.v1";
    public const string LegacyRemotingApi = "legacy.remoting.api.v1";
    public const string LegacyRemotingMarshalByRef = "legacy.remoting.marshal-by-ref.v1";
    public const string LegacyRemotingChannel = "legacy.remoting.channel.v1";
    public const string LegacyRemotingRegistration = "legacy.remoting.registration.v1";
    public const string LegacyRemotingConfig = "legacy.remoting.config.v1";
    public const string LegacyWebFormsInventory = "legacy.webforms.inventory.v1";
    public const string LegacyWebFormsEventBinding = "legacy.webforms.event-binding.v1";
    public const string LegacyWebFormsHandlerResolution = "legacy.webforms.handler-resolution.v1";
    public const string LegacyWebFormsDesignerControl = "legacy.webforms.designer-control.v1";
    public const string LegacyWebFormsEventFlow = "legacy.webforms.event-flow.v1";
    public const string LegacyWebFormsLogicSignal = "legacy.webforms.logic-signal.v1";
    public const string LegacyWinFormsInventory = "legacy.winforms.inventory.v1";
    public const string LegacyWinFormsControl = "legacy.winforms.control.v1";
    public const string LegacyWinFormsEventBinding = "legacy.winforms.event-binding.v1";
    public const string LegacyWinFormsHandlerResolution = "legacy.winforms.handler-resolution.v1";
    public const string LegacyWinFormsNavigation = "legacy.winforms.navigation.v1";
    public const string LegacyWinFormsCallbackBoundary = "legacy.winforms.callback-boundary.v1";
    public const string LegacyWinFormsHandlerFlow = "legacy.winforms.handler-flow.v1";
    public const string LegacyWinFormsResourceMetadata = "legacy.winforms.resource-metadata.v1";
    public const string LegacyAspNetSurface = "legacy.aspnet.surface.v1";
    public const string LegacyAspNetRoute = "legacy.aspnet.route.v1";
    public const string LegacyAspNetConfig = "legacy.aspnet.config.v1";
    public const string LegacyAspNetHandler = "legacy.aspnet.handler.v1";
    public const string LegacyAspNetPageMethod = "legacy.aspnet.page-method.v1";
    public const string LegacyAspNetNavigation = "legacy.aspnet.navigation.v1";
    public const string LegacyDataMetadataInventory = "legacy.data.metadata.inventory.v1";
    public const string LegacyDataDbml = "legacy.data.dbml.v1";
    public const string LegacyDataEdmx = "legacy.data.edmx.v1";
    public const string LegacyDataTypedDataSet = "legacy.data.typed-dataset.v1";
    public const string LegacyDataConfig = "legacy.data.config.v1";
    public const string LegacyDataGeneratedLink = "legacy.data.generated-link.v1";
    public const string LegacyDataModelIdentity = "legacy.data.model.identity.v1";
    public const string LegacyDataModelRelationship = "legacy.data.model.relationship.v1";
    public const string LegacyDataOrmNHibernate = "legacy.data.orm.nhibernate.v1";
    public const string LegacyDataOrmUnsupported = "legacy.data.orm.unsupported.v1";
    public const string LegacyDataModelGeneratedLink = "legacy.data.model.generated-link.v1";
    public const string LegacyDataModelSurface = "legacy.data.model.surface.v1";
    public const string LegacyFlowInputAvailability = "legacy.flow.input-availability.v1";
    public const string LegacyFlowRootSelection = "legacy.flow.root-selection.v1";
    public const string LegacyFlowStaticTraversal = "legacy.flow.static-traversal.v1";
    public const string LegacyFlowParameterForwardUnavailable = "legacy.flow.parameter-forward-unavailable.v1";
    public const string LegacyFlowClassification = "legacy.flow.classification.v1";
    public const string LegacyFlowGapPropagation = "legacy.flow.gap-propagation.v1";
    public const string LegacyFlowRedaction = "legacy.flow.redaction.v1";
    public const string LegacyFlowReport = "legacy.flow.report.v1";
    public const string ContractDeltaReduction = "contract.delta.reduce.v1";
    public const string ContractDeltaInput = "contract.delta.input.v2";
    public const string ContractDeltaImpact = "contract.delta.impact.v2";
    public const string ContractDeltaContext = "contract.delta.context.v2";
    public const string EndpointAlignment = "endpoint.alignment.v1";
    public const string RazorBinding = "csharp.razor.binding.v1";
    public const string RazorFormTarget = "csharp.razor.form-target.v1";
    public const string RazorModelBinding = "csharp.razor.model-binding.v1";
    public const string RazorBindingGap = "csharp.razor.binding-gap.v1";
    public const string MessageSurfacePublish = "message.surface.publish.v1";
    public const string MessageSurfaceConsume = "message.surface.consume.v1";
    public const string MessageSurfaceBinding = "message.surface.binding.v1";
    public const string MessageSurfaceIdentity = "message.surface.identity.v1";
    public const string MessageSurfaceCombine = "message.surface.combine.v1";
    public const string MessageSurfaceCandidateEdge = "message.surface.candidate-edge.v1";
    public const string MessageSurfacePaths = "message.surface.paths.v1";
    public const string MessageSurfaceReducer = "message.surface.reducer.v1";
    public const string MessageSurfaceGap = "message.surface.gap.v1";
}

public static class ScannerVersions
{
    public const string TraceMap = "tracemap-milestone16";
    public const string RepoManifestExtractor = "repo-manifest/0.1.0";
    public const string FileInventoryExtractor = "file-inventory/0.1.0";
    public const string ProjectFileExtractor = "project-file/0.1.0";
    public const string BuildEnvironmentExtractor = "build-environment/0.1.0";
    public const string AnalyzerCapabilityExtractor = "analyzer-capability/0.1.0";
    public const string CSharpSyntaxExtractor = "csharp-syntax/0.3.0";
    public const string CSharpAspNetSyntaxRouteExtractor = "csharp-aspnet-syntax-route/0.1.0";
    public const string CSharpIntegrationSyntaxExtractor = "csharp-integration-syntax/0.1.0";
    public const string CSharpSemanticExtractor = "csharp-semantic/0.12.0";
    public const string ConfigExtractor = "config/0.1.0";
    public const string SqlTextExtractor = "sql-text/0.1.0";
    public const string SqlShapeExtractor = "sql-shape/0.1.0";
    public const string LegacyWcfExtractor = "legacy-wcf/0.2.0";
    public const string LegacyAsmxExtractor = "legacy-asmx/0.1.0";
    public const string LegacyRemotingExtractor = "legacy-remoting/0.1.0";
    public const string LegacyWebFormsExtractor = "legacy-webforms/0.1.0";
    public const string LegacyWinFormsExtractor = "legacy-winforms/0.1.0";
    public const string LegacyAspNetExtractor = "legacy-aspnet/0.1.0";
    public const string LegacyDataExtractor = "legacy-data/0.1.0";
    public const string EndpointAlignment = "endpoint-alignment/0.1.0";
    public const string RazorBindingExtractor = "csharp-razor-binding/0.1.0";
}
