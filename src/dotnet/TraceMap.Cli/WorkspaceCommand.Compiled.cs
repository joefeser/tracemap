using System.Text.Json;
using TraceMap.Core;

namespace TraceMap.Cli;

public sealed record WorkspaceCompiledReview(string RepositoryId, string NativeConfigPath);
internal sealed record WorkspaceCompiledPlan(string ConfigPath, WebFormsReviewPreflightManifest NativePlan);

public static partial class WorkspaceCommand
{
    public const string CompiledSchemaVersion = "tracemap-workspace.v2";
    public const string CompiledRuleId = "workflow.workspace.compiled-collection.v1";

    private static async Task<WorkspaceCompiledPlan?> VerifyCompiledAsync(WorkspaceConfiguration configuration,
        string root, string prospectiveRun, IReadOnlyList<(WorkspaceRepository Config, string Root, string Head)> repositories,
        SortedDictionary<string, string> inputs, CancellationToken token)
    {
        if (configuration.CompiledReview is not { } review) return null;
        var source = repositories.SingleOrDefault(item => item.Config.Id == review.RepositoryId);
        if (source.Config is null) throw new ArgumentException("WORKSPACE_COMPILED_WEB_SCOPE_EXCLUDED");
        if (source.Config.Projects.Length != 0 || source.Config.Solution is not null
            || source.Config.Include is { Length: > 0 } || source.Config.Exclude is { Length: > 0 }
            || source.Config.TargetFramework is not null)
            throw new ArgumentException("WORKSPACE_COMPILED_SCOPE_BELONGS_IN_NATIVE_CONFIG");
        var path = Full(review.NativeConfigPath, root);
        var plan = await WebFormsReviewPreflightCommand.BuildAsync(path,
            System.IO.Path.Combine(prospectiveRun, "native-review"), token);
        var native = plan.Configuration;
        if (native.Operation != "fresh") throw new ArgumentException("WORKSPACE_COMPILED_FRESH_CONFIG_REQUIRED");
        if (!Full(native.SourceRoot).Equals(source.Root, PathComparison) || native.SourceCommitSha != source.Head)
            throw new ArgumentException("WORKSPACE_COMPILED_SOURCE_IDENTITY_MISMATCH");
        if (native.BindingReceipts.Length == 0 || native.PublishReceiptRelativePath is null)
            throw new ArgumentException("WORKSPACE_COMPILED_PREPARED_CONFIG_REQUIRED: prepare/import explicit native receipts first.");
        var validated = await WebFormsReviewInputValidation.ValidateAsync(plan, token);
        if (validated.CompiledProvenance.OmittedInputCount != 0
            || validated.CompiledProvenance.Outcomes.Any(item => item.Outcome != "admitted"))
            throw new ArgumentException("WORKSPACE_COMPILED_METADATA_NOT_ADMITTED");
        var primary = validated.CompiledProvenance.Outcomes.Where(item => item.Role == "primary").ToArray();
        if (primary.Length != native.PrimaryAssemblies.Length || primary.Any(item => item.Outcome != "admitted" || item.ProvenanceState != "bound"))
            throw new ArgumentException("WORKSPACE_COMPILED_PRIMARY_BINDING_REQUIRED");
        var options = WebFormsReviewAttachmentExecution.Options(plan, "unused-workspace-verification");
        if (WebFormsPublishInputInspector.Inspect(options, native.SourceCommitSha, token)?.Status != "bound")
            throw new ArgumentException("WORKSPACE_COMPILED_PUBLISH_BINDING_REQUIRED");
        await WebFormsReviewInputValidation.RecheckAsync(plan, token);
        // These are exact native admitted input bytes, not a projection or an agent attestation.
        foreach (var (input, index) in plan.Inputs.Select((input, index) => (input, index)))
            inputs.Add($"native-input:{index:D5}:{input.Role}", input.Sha256);
        return new(path, plan);
    }

    private static async Task<WebFormsWorkspaceSnapshot> VerifySnapshotAsync(string runRoot,
        WorkspaceCompiledPlan plan, WebFormsWorkspaceSnapshot? expected, CancellationToken token)
    {
        await WebFormsReviewInputValidation.RecheckAsync(plan.NativePlan, token);
        var snapshot = await WebFormsReviewExecutionCommand.ReadWorkspaceSnapshotAsync(runRoot, token);
        if (snapshot.ConfigurationSha256 != plan.NativePlan.Inputs.Single(input => input.Role == "configuration").Sha256)
            throw new ArgumentException("WORKSPACE_COMPILED_CONFIGURATION_CHANGED");
        if (expected is not null && (snapshot.CheckpointSha256 != expected.CheckpointSha256
            || snapshot.PreflightSha256 != expected.PreflightSha256 || snapshot.IndexSha256 != expected.IndexSha256
            || snapshot.ManifestSha256 != expected.ManifestSha256 || snapshot.ScanRoot != expected.ScanRoot))
            throw new ArgumentException("WORKSPACE_COMPILED_SNAPSHOT_CHANGED");
        return snapshot;
    }

    private static byte[] RunRecordBytes<T>(T record, object? compiledReview, object? sqlEvidence = null)
    {
        var node = JsonSerializer.SerializeToNode(record, Json)!.AsObject();
        if (compiledReview is not null) node.Add("compiledReview", JsonSerializer.SerializeToNode(compiledReview, Json));
        if (sqlEvidence is not null) node.Add("sqlEvidence", JsonSerializer.SerializeToNode(sqlEvidence, Json));
        return JsonSerializer.SerializeToUtf8Bytes(node, Json);
    }
}
