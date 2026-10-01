using TraceMap.Cli;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class WebFormsWizardExecutionTests
{
    [Fact]
    public async Task Incorrect_attestation_does_not_start_or_create_run_reference()
    {
        using var temp = new TempDirectory();
        using var store = await Ready(temp.Path);
        using var output = new StringWriter();
        using var error = new StringWriter();
        Assert.Equal("WEBFORMS_WIZARD_EXPLICIT_SOURCE_ATTESTATION_REQUIRED", (await Assert.ThrowsAsync<InvalidOperationException>(() =>
            WebFormsWizardExecution.RunAsync(store, "site", new string('0', 40), output, error,
                runner: (_, _, _, _) => throw new Exception("must not run")))).Message);
        Assert.Null(store.ReadProject("site").Run);
        Assert.Equal("ready", store.ReadProject("site").Step);
    }

    [Fact]
    public async Task Failed_start_is_pinned_and_not_silently_restarted()
    {
        using var temp = new TempDirectory();
        using var store = await Ready(temp.Path);
        using var output = new StringWriter();
        using var error = new StringWriter();
        var commit = store.ReadProject("site").Native!.SourceCommitSha;
        Assert.Equal(1, await WebFormsWizardExecution.RunAsync(store, "site", commit, output, error,
            runner: (args, _, _, _) => { Assert.Equal("start", args[1]); return Task.FromResult(1); }));
        var failed = store.ReadProject("site");
        Assert.Equal("failed", failed.Step);
        Assert.NotNull(failed.Run);
        Assert.Equal("WEBFORMS_WIZARD_RUN_NOT_STARTED_USE_REPAIR", (await Assert.ThrowsAsync<InvalidOperationException>(() =>
            WebFormsWizardExecution.RunAsync(store, "site", null, output, error,
                runner: (_, _, _, _) => throw new Exception("must not run")))).Message);
        Assert.Equal(failed.Run, store.ReadProject("site").Run);
    }

    [Fact]
    public async Task Zero_exit_without_verified_report_status_is_not_completion()
    {
        using var temp = new TempDirectory();
        using var store = await Ready(temp.Path);
        using var output = new StringWriter();
        using var error = new StringWriter();
        var commit = store.ReadProject("site").Native!.SourceCommitSha;
        Assert.Equal("WEBFORMS_WIZARD_REPORTS_NOT_VERIFIED_COMPLETE", (await Assert.ThrowsAsync<InvalidOperationException>(() =>
            WebFormsWizardExecution.RunAsync(store, "site", commit, output, error, runner: async (args, writer, _, _) =>
            {
                if (args[1] == "status") await writer.WriteLineAsync("{\"state\":\"scan-completed-reports-pending\",\"retainedArtifactsVerified\":true}");
                return 0;
            }))).Message);
        Assert.Equal("failed", store.ReadProject("site").Step);
        Assert.DoesNotContain("Verified retained reports:", output.ToString());
    }

    [Fact]
    public Task Real_native_pipeline_completes_and_completed_continue_only_verifies_retained_reports() => VerifyCompletedRun(false);

    [Fact]
    public Task Output_failure_after_verified_completion_preserves_completed_cursor() => VerifyCompletedRun(true);

    private static async Task VerifyCompletedRun(bool outputFailsAfterCompletion)
    {
        using var temp = new TempDirectory();
        try { await VerifyCompletedRun(temp.Path, outputFailsAfterCompletion); }
        catch
        {
            // Keep the original temp-path conditions during execution, then copy
            // only this public synthetic fixture for postmortem inspection.
            var retained = Environment.GetEnvironmentVariable("TRACEMAP_DEEP_CORPUS_ROOT");
            if (retained is not null)
            {
                try
                {
                    var target = Path.Combine(retained, "wizard", Guid.NewGuid().ToString("N"));
                    foreach (var source in Directory.EnumerateFiles(temp.Path, "*", SearchOption.AllDirectories))
                    {
                        var destination = Path.Combine(target, Path.GetRelativePath(temp.Path, source));
                        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                        File.Copy(source, destination, overwrite: false);
                    }
                }
                catch (IOException) { /* Preserve the original test failure. */ }
                catch (UnauthorizedAccessException) { /* Preserve the original test failure. */ }
            }
            throw;
        }
    }

    private static async Task VerifyCompletedRun(string root, bool outputFailsAfterCompletion)
    {
        using var store = await Ready(root);
        using var output = new StringWriter();
        using var error = new StringWriter();
        var commit = store.ReadProject("site").Native!.SourceCommitSha;
        if (outputFailsAfterCompletion)
        {
            using var failing = new CompletionFailingWriter();
            await Assert.ThrowsAsync<IOException>(() => WebFormsWizardExecution.RunAsync(store, "site", commit, failing, error));
        }
        else
        {
            var code = await WebFormsWizardExecution.RunAsync(store, "site", commit, output, error);
            Assert.True(code == 0, error.ToString() + output);
            Assert.Contains("Verified retained reports:", output.ToString());
        }
        var completed = store.ReadProject("site");
        Assert.Equal("completed", completed.Step);
        var before = File.ReadAllBytes(store.ProjectPath("site"));
        Assert.Equal(0, await WebFormsWizardExecution.RunAsync(store, "site", null, output, error));
        Assert.Equal(before, File.ReadAllBytes(store.ProjectPath("site")));
        Assert.Single(Directory.GetDirectories(Path.Combine(store.DirectoryPath, "runs")));
        var nativeRun = Path.Combine(store.DirectoryPath, completed.Run!.RelativeRoot, "run");
        var manifestPath = Assert.Single(Directory.GetFiles(Path.Combine(nativeRun, "attempts"), "scan-manifest.json", SearchOption.AllDirectories));
        using (var scan = System.Text.Json.JsonDocument.Parse(File.ReadAllText(manifestPath)))
        {
            var publish = scan.RootElement.GetProperty("webFormsPublishProvenance");
            Assert.Equal("bound", publish.GetProperty("status").GetString());
            Assert.Equal(1, publish.GetProperty("pageCount").GetInt32());
        }
        using (var manifest = System.Text.Json.JsonDocument.Parse(File.ReadAllText(Path.Combine(nativeRun, "run-manifest.json"))))
        {
            Assert.Contains(manifest.RootElement.GetProperty("inputs").EnumerateArray(), input =>
                input.GetProperty("role").GetString() == "publish-receipt");
        }
        store.RepairProject(completed, store.PreviewRepair("site"), true);
        using var status = new StringWriter();
        Assert.Equal(0, await TraceMapCommand.RunAsync(["webforms-review", "status", "--run", nativeRun, "--json"], status, error));
        using var document = System.Text.Json.JsonDocument.Parse(status.ToString());
        Assert.Equal("reports-completed-review-only", document.RootElement.GetProperty("state").GetString());
        Assert.True(document.RootElement.GetProperty("retainedArtifactsVerified").GetBoolean());
    }

    private static async Task<WebFormsWizardStore> Ready(string root)
    {
        var (site, published) = Inputs(root);
        var store = WebFormsWizardStore.Open(Path.Combine(root, "config"), false);
        site = WebFormsWizardStore.Physical(site);
        store.SaveProject(new("site", site, site, "projectless", "all", ["Default.aspx"], null, [], [], "publication"));
        WebFormsWizardPublication.Configure(store, "site", published, ["bin/CompiledEvidence.CSharp.dll"], []);
        store.SaveProject(store.ReadProject("site") with { Step = "configuration" });
        await WebFormsWizardNative.ConfigureAsync(store, "site");
        return store;
    }

    private sealed class CompletionFailingWriter : StringWriter
    {
        public override Task WriteLineAsync(string? value) => value?.StartsWith("Verified retained reports:", StringComparison.Ordinal) == true
            ? Task.FromException(new IOException("output unavailable")) : base.WriteLineAsync(value);
    }

    internal static (string Site, string Published) Inputs(string root)
    {
        var (site, published) = WebFormsWizardPublicationTests.Fixture(root);
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "samples", "compiled-dotnet-evidence"))) directory = directory.Parent;
        Assert.NotNull(directory);
        File.Delete(Path.Combine(published, "bin", "Site.dll"));
        File.Copy(Path.Combine(directory.FullName, "samples", "compiled-dotnet-evidence", "csharp", "bin", "Debug", "net10.0", "CompiledEvidence.CSharp.dll"),
            Path.Combine(published, "bin", "CompiledEvidence.CSharp.dll"));
        File.WriteAllText(Path.Combine(published, "bin", "Default.aspx.compiled"),
            "<preserve virtualPath=\"/Default.aspx\" assembly=\"CompiledEvidence.CSharp\" type=\"Public.Page\" />");
        File.WriteAllText(Path.Combine(site, "Default.aspx"), "<%@ Page Language=\"VB\" CodeFile=\"Default.aspx.vb\" Inherits=\"Public.Page\" %>");
        File.WriteAllText(Path.Combine(site, "Default.aspx.vb"), "Public Class Page\n Public Sub Load()\n End Sub\nEnd Class\n");
        WebFormsWizardNativeTests.InitGit(site);
        return (site, published);
    }
}
