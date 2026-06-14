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
    public const string HttpClientInvocation = "http.client.invocation.v1";
    public const string DatabaseEntityFramework = "database.ef.v1";
    public const string DatabaseDapperInvocation = "database.dapper.invocation.v1";
    public const string DatabaseSqlText = "database.sql.text.v1";
    public const string DatabaseSqlShape = "database.sql.shape.v1";
    public const string ConfigKey = "config.key.v1";
    public const string ContractDeltaReduction = "contract.delta.reduce.v1";
    public const string ContractDeltaInput = "contract.delta.input.v2";
    public const string ContractDeltaImpact = "contract.delta.impact.v2";
    public const string ContractDeltaContext = "contract.delta.context.v2";
    public const string EndpointAlignment = "endpoint.alignment.v1";
}

public static class ScannerVersions
{
    public const string TraceMap = "tracemap-milestone15";
    public const string RepoManifestExtractor = "repo-manifest/0.1.0";
    public const string FileInventoryExtractor = "file-inventory/0.1.0";
    public const string ProjectFileExtractor = "project-file/0.1.0";
    public const string CSharpSyntaxExtractor = "csharp-syntax/0.3.0";
    public const string CSharpAspNetSyntaxRouteExtractor = "csharp-aspnet-syntax-route/0.1.0";
    public const string CSharpIntegrationSyntaxExtractor = "csharp-integration-syntax/0.1.0";
    public const string CSharpSemanticExtractor = "csharp-semantic/0.11.0";
    public const string ConfigExtractor = "config/0.1.0";
    public const string SqlTextExtractor = "sql-text/0.1.0";
    public const string SqlShapeExtractor = "sql-shape/0.1.0";
    public const string EndpointAlignment = "endpoint-alignment/0.1.0";
}
