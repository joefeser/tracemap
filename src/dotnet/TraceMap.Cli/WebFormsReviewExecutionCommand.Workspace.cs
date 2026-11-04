namespace TraceMap.Cli;

internal sealed record WebFormsWorkspaceSnapshot(string ScanRoot, string ReportRoot,
    string ConfigurationSha256, string PreflightSha256, string CheckpointSha256,
    string ManifestSha256, string IndexSha256, string? Coverage, IReadOnlyList<string> Gaps);

public static partial class WebFormsReviewExecutionCommand
{
    /// <summary>Reuse the native completed-run verifier; never discover a latest attempt or rewrite its artifacts.</summary>
    internal static async Task<WebFormsWorkspaceSnapshot> ReadWorkspaceSnapshotAsync(string root, CancellationToken token)
    {
        root = WebFormsReviewPreflightCommand.PhysicalPath(root);
        using var runLock = new FileStream(OwnedPath(root, ".native-run.lock"), FileMode.Open, FileAccess.Read, FileShare.None);
        var retained = await ReadCompletedRunAsync(root, token);
        var checkpoint = retained.History.Checkpoint!;
        if (retained.Location is not null || retained.Plan.Configuration.Operation != "fresh")
            throw Fail("WORKSPACE_FRESH_RUN_REQUIRED");
        var runtime = WebFormsReviewPreflightCommand.PhysicalPath(Path.GetDirectoryName(typeof(WebFormsReviewExecutionCommand).Assembly.Location)!);
        if (await RuntimeDigestAsync(runtime, token) != checkpoint.RuntimeInputsSha256)
            throw Fail("WORKSPACE_RUNTIME_CHANGED");
        var prefix = checkpoint.Attempt + "/scan/";
        var manifest = checkpoint.Artifacts.Single(item => item.RelativePath == prefix + "scan-manifest.json");
        var index = checkpoint.Artifacts.Single(item => item.RelativePath == prefix + "index.sqlite");
        return new(OwnedPath(root, checkpoint.Attempt + "/scan"), OwnedPath(root, checkpoint.Reports!.ReportAttempt),
            retained.Plan.Inputs.Single(input => input.Role == "configuration").Sha256,
            retained.PreflightSha256, retained.History.Sha256!, manifest.Sha256, index.Sha256,
            checkpoint.Reports.Coverage, checkpoint.Gaps);
    }
}
