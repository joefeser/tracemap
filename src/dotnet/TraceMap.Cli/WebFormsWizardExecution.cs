using System.Text.Json;
using System.Text.RegularExpressions;
using TraceMap.Core;

namespace TraceMap.Cli;

public delegate Task<int> WebFormsWizardNativeRunner(string[] args, TextWriter output, TextWriter error, CancellationToken token);

/// <summary>Explicit attestation/start and pinned resume adapter. Native services retain execution authority.</summary>
public static class WebFormsWizardExecution
{
    public static async Task<int> RunAsync(WebFormsWizardStore store, string id, string? attestedCommit,
        TextWriter output, TextWriter error, CancellationToken token = default, WebFormsWizardNativeRunner? runner = null)
    {
        runner ??= TraceMapCommand.RunAsync;
        var configPath = await WebFormsWizardNative.ValidateAsync(store, id, token);
        var project = store.ReadProject(id);
        var native = project.Native!;
        var fresh = project.Run is null;
        if (fresh)
        {
            if (project.Step != "ready" || attestedCommit != native.SourceCommitSha) throw Fail("EXPLICIT_SOURCE_ATTESTATION_REQUIRED");
            var reference = new WebFormsWizardRunReference("runs/" + id + "-" + Guid.NewGuid().ToString("N"),
                attestedCommit, native.Sha256);
            project = project with { Run = reference, Step = "running" };
            store.SaveProject(project);
        }
        var run = project.Run!;
        if (run.NativeConfigSha256 != native.Sha256 || run.AttestedCommitSha != native.SourceCommitSha ||
            !Regex.IsMatch(run.RelativeRoot, "\\Aruns/" + Regex.Escape(id) + "-[0-9a-f]{32}\\z", RegexOptions.CultureInvariant)) throw Fail("RUN_REFERENCE_INVALID");
        var root = WebFormsReviewPreflightCommand.Child(store.DirectoryPath, run.RelativeRoot);
        var nativeRun = Path.Combine(root, "run");
        if (!fresh && !File.Exists(Path.Combine(nativeRun, "run-manifest.json"))) throw Fail("RUN_NOT_STARTED_USE_REPAIR");
        await output.WriteLineAsync("Pinned review attempt: " + root);
        try
        {
            if (project.Step != "completed")
            {
                var arguments = fresh
                    ? new[] { "webforms-review", "start", "--config", configPath, "--out", root, "--attest-exact-source-commit", run.AttestedCommitSha }
                    : ["webforms-review", "resume", "--run", nativeRun];
                var code = await runner(arguments, output, error, token);
                if (code != 0)
                {
                    store.SaveProject(project with { Step = "failed" });
                    await output.WriteLineAsync("Attempt retained. No completion claim; continue can resume a native manifest, or explicit repair can create a new attempt.");
                    return code;
                }
            }
            using var statusOutput = new StringWriter();
            var statusCode = await runner(["webforms-review", "status", "--run", nativeRun, "--json"], statusOutput, error, token);
            if (statusCode != 0) throw Fail("COMPLETION_STATUS_UNAVAILABLE");
            var status = JsonSerializer.Deserialize<WebFormsReviewStatus>(statusOutput.ToString(), new JsonSerializerOptions(JsonSerializerDefaults.Web) { MaxDepth = 32 });
            if (status is null || status.State != "reports-completed-review-only" || !status.RetainedArtifactsVerified
                || status.SourceCommitSha != run.AttestedCommitSha || string.IsNullOrWhiteSpace(status.WorkbenchPath)) throw Fail("REPORTS_NOT_VERIFIED_COMPLETE");
            _ = await WebFormsWizardNative.ValidateAsync(store, id, token);
            if (project.Step != "completed") store.SaveProject(project with { Step = "completed" });
            await output.WriteLineAsync("Verified retained reports: " + status.WorkbenchPath);
            await output.WriteLineAsync("Coverage: " + status.Coverage + "; static review only, not customer runtime or complete coverage proof.");
            return 0;
        }
        catch
        {
            // The cursor records an interrupted/failed attempt, never a claim of a live process.
            if (project.Step != "completed") store.SaveProject(project with { Step = "failed" });
            throw;
        }
    }

    private static InvalidOperationException Fail(string code) => new("WEBFORMS_WIZARD_" + code);
}
