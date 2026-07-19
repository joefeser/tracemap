using System.Text.Json.Serialization;

namespace TraceMap.Access;

public sealed record AccessScanOptions(
    string RepoPath,
    string DatabasePath,
    string OutputPath,
    int TimeoutSeconds = 600);

public sealed record AccessLimits(
    long MaxDatabaseBytes = 2L * 1024 * 1024 * 1024,
    int MaxObjectsPerCollection = 10_000,
    int MaxChildrenPerObject = 10_000,
    int MaxStringLength = 1024 * 1024,
    int MaxQueryTextLength = 4 * 1024 * 1024,
    int MaxUiDesignTextLength = 4 * 1024 * 1024,
    int MaxUiDesignLines = 100_000,
    int MaxFacts = 100_000,
    int MaxGaps = 10_000,
    long MaxProjectionBytes = 64L * 1024 * 1024,
    long MaxArtifactBytes = 512L * 1024 * 1024)
{
    public static AccessLimits Default { get; } = new();
}

public sealed record AccessValidatedInput(
    string GitRoot,
    string RepoName,
    string RepositoryIdentityHash,
    string? RemoteUrl,
    string? Branch,
    string CommitSha,
    string DatabaseFullPath,
    string DatabaseRelativePath,
    string DatabaseHash,
    string DatabaseExtension,
    string OutputFullPath,
    bool IsGitLfs);

public sealed record AccessSafeIdentity(string? DisplayName, string NameHash, string StableKey);

public sealed record AccessFieldProjection(
    AccessSafeIdentity Identity,
    int Ordinal,
    string TypeFamily,
    int DeclaredSize,
    bool Required);

public sealed record AccessIndexProjection(
    AccessSafeIdentity Identity,
    bool Primary,
    bool Unique,
    IReadOnlyList<string> FieldStableKeys);

public sealed record AccessTableProjection(
    AccessSafeIdentity Identity,
    IReadOnlyList<AccessFieldProjection> Fields,
    IReadOnlyList<AccessIndexProjection> Indexes);

public sealed record AccessRelationshipFieldProjection(
    string SourceFieldStableKey,
    string TargetFieldStableKey,
    int Ordinal);

public sealed record AccessRelationshipProjection(
    AccessSafeIdentity Identity,
    string SourceTableStableKey,
    string TargetTableStableKey,
    int Attributes,
    IReadOnlyList<AccessRelationshipFieldProjection> Fields);

public sealed record AccessQueryParameterProjection(
    AccessSafeIdentity Identity,
    int Ordinal,
    string TypeFamily);

public sealed record AccessQueryDependencyProjection(
    string TargetStableKey,
    string TargetKind,
    string Coverage);

public sealed record AccessQueryProjection(
    AccessSafeIdentity Identity,
    string QueryKind,
    string SqlHash,
    int SqlLength,
    string ReferenceCoverage,
    IReadOnlyList<AccessQueryParameterProjection> Parameters,
    IReadOnlyList<AccessQueryDependencyProjection> Dependencies,
    bool IsPassThrough,
    string? ConnectionHash,
    string? ProviderFamily);

public sealed record AccessExternalLinkProjection(
    AccessSafeIdentity Identity,
    string SourceKind,
    string SourceHash,
    string BoundaryKind);

public sealed record AccessUiEventProjection(
    string EventRole,
    string Category,
    string? ValueHash,
    int ValueLength);

public sealed record AccessBindingProjection(
    AccessSafeIdentity Identity,
    string OwnerStableKey,
    string BindingKind,
    string SourceKind,
    string? ExpressionHash,
    int ExpressionLength,
    IReadOnlyList<string> TargetStableKeys,
    string TargetKind,
    string Coverage);

public sealed record AccessControlProjection(
    AccessSafeIdentity Identity,
    string SurfaceStableKey,
    int Ordinal,
    string ControlType,
    IReadOnlyList<AccessBindingProjection> Bindings,
    IReadOnlyList<AccessUiEventProjection> Events);

public sealed record AccessUiSurfaceProjection(
    AccessSafeIdentity Identity,
    string SurfaceKind,
    string ModulePresence,
    string BoundState,
    string DesignHash,
    IReadOnlyList<AccessBindingProjection> Bindings,
    IReadOnlyList<AccessControlProjection> Controls,
    IReadOnlyList<AccessUiEventProjection> Events);

public sealed record AccessGapProjection(string Classification, string ScopeKind, string? StableScopeKey, string? RuleId = null);

public sealed record AccessCapabilityProjection(string Name, string Status);

public sealed record AccessDatabaseProjection(
    string Schema,
    string DatabaseHash,
    string DatabaseExtension,
    string AccessVersion,
    int AccessProcessId,
    bool RowDataRead,
    bool ExecutionPerformed,
    int OmittedSystemObjectCount,
    IReadOnlyList<AccessTableProjection> Tables,
    IReadOnlyList<AccessRelationshipProjection> Relationships,
    IReadOnlyList<AccessQueryProjection> Queries,
    IReadOnlyList<AccessExternalLinkProjection> ExternalLinks,
    IReadOnlyList<AccessGapProjection> Gaps,
    IReadOnlyList<AccessCapabilityProjection> Capabilities,
    IReadOnlyList<AccessUiSurfaceProjection>? UiSurfaces = null);

public sealed record AccessWorkerFrame(
    string Kind,
    string Token,
    int? AccessProcessId = null,
    AccessDatabaseProjection? Result = null,
    string? Classification = null)
{
    public static AccessWorkerFrame Hello(string token, int pid) => new("hello", token, pid);
    public static AccessWorkerFrame Heartbeat(string token) => new("heartbeat", token);
    public static AccessWorkerFrame Success(string token, AccessDatabaseProjection result) => new("result", token, result.AccessProcessId, result);
    public static AccessWorkerFrame Failure(string token, string classification) => new("failure", token, Classification: SafeClassification(classification));

    private static string SafeClassification(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 128 && value.All(character => char.IsLetterOrDigit(character) || character is '-' or '_')
            ? value
            : "AccessWorkerFailure";
}

[JsonSerializable(typeof(AccessWorkerFrame))]
[JsonSerializable(typeof(AccessDatabaseProjection))]
public partial class AccessJsonContext : JsonSerializerContext;
