using TraceMap.Core;

namespace TraceMap.Cli;

public sealed record WorkspaceSqlEvidence(string RepositoryId, string ManifestPath, string SourceRoot);

public static partial class WorkspaceCommand
{
    public const string SqlSchemaVersion = "tracemap-workspace.v3";
    public const string SqlRuleId = "workflow.workspace.sql-collection.v1";

    private static SqlLocalCollectionInput? VerifySql(WorkspaceConfiguration configuration, string root, string prospectiveRun,
        IReadOnlyList<(WorkspaceRepository Config, string Root, string Head)> repositories,
        SortedDictionary<string, string> inputs, CancellationToken token)
    {
        if (configuration.SqlEvidence is not { } sql) return null;
        var context = repositories.SingleOrDefault(item => item.Config.Id == sql.RepositoryId);
        if (context.Config is null) throw new ArgumentException("WORKSPACE_SQL_CONTEXT_SCOPE_EXCLUDED");
        var manifest = SqlServerLocalDefinitionsCommand.PhysicalPath(Full(sql.ManifestPath, root));
        var source = SqlServerLocalDefinitionsCommand.PhysicalPath(Full(sql.SourceRoot, root));
        prospectiveRun = SqlServerLocalDefinitionsCommand.PhysicalPath(prospectiveRun);
        Unlinked(manifest); Unlinked(source);
        if (Within(prospectiveRun, source) || Within(source, prospectiveRun) || Within(manifest, prospectiveRun))
            throw new ArgumentException("WORKSPACE_SQL_OUTPUT_OVERLAPS_INPUT");
        var plan = SqlServerLocalDefinitionsCommand.Inspect(manifest, source, token);
        if (plan.Export.Input.ContextRepository != context.Config.Id || plan.Export.Input.ContextCommitSha != context.Head)
            throw new ArgumentException("WORKSPACE_SQL_APPLICATION_CONTEXT_MISMATCH");
        inputs.Add("sql:manifest", plan.ManifestSha256);
        inputs.Add("sql:bounded-input", plan.Export.BoundedInputSha256);
        foreach (var file in plan.SourceFiles) inputs.Add("sql-file:" + file.Path, file.Sha256);
        return plan;
    }
}
