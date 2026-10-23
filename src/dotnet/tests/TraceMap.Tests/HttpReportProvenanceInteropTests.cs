using System.Diagnostics;
using System.Text.Json;
using TraceMap.Cli;
using TraceMap.Reporting;
using static TraceMap.Tests.WebFormsReviewExecutionShared;
using Fixture = TraceMap.Tests.WebFormsReviewExecutionShared.Fixture;

namespace TraceMap.Tests;

// The dedicated provenance lane supplies Python explicitly. Other .NET-only
// lanes report this integration as skipped, never as a passing Python check.
public sealed class ProvenanceInteropTheoryAttribute : TheoryAttribute
{
    public ProvenanceInteropTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TRACEMAP_PROVENANCE_PYTHON")))
            Skip = "Set TRACEMAP_PROVENANCE_PYTHON to run native receipt/Python interoperability.";
    }
}

public sealed class HttpReportProvenanceInteropTests
{
    [ProvenanceInteropTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Native_http_receipt_verifies_without_claiming_path_presence(bool hasHttpPath)
    {
        using var fixture = new Fixture();
        fixture.AddPublicEventFixture(http: hasHttpPath);
        await fixture.Preflight();
        Assert.True(await fixture.Execute("run", Scan) == 0, fixture.Error.ToString());
        Assert.True(await fixture.ExecuteReports("resume", WebFormsReviewReportExecution.WriteAsync) == 0,
            fixture.Error.ToString());
        var bundle = Path.Combine(fixture.Run, fixture.LastCheckpoint().Reports!.ReportAttempt);
        var destination = Path.Combine(fixture.Root, "native-http-output");
        using var error = new StringWriter();
        Assert.True(await TraceMapCommand.RunAsync(["webforms-review", "requery-handler", "--run", fixture.Run,
            "--bundle", bundle, "--handler", "Names_Init", "--out", destination, "--surface", "http-client"],
            TextWriter.Null, error) == 0, error.ToString());
        var receiptPath = Path.Combine(destination, "handler-requery.local.json");
        var receipt = JsonSerializer.Deserialize<WebFormsHandlerRequeryReceipt>(File.ReadAllText(receiptPath), JsonOptions)!;
        Assert.Equal(hasHttpPath, receipt.ExactChains > 0);
        Assert.Equal(WebFormsReviewExecutionCommand.HandlerRequeryHash(receipt), receipt.BoundedInputSha256);
        var before = Directory.GetFiles(fixture.Root, "*", SearchOption.AllDirectories).ToDictionary(path => path, Hash);
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "scripts", "verify-http-report-provenance.py")))
            root = root.Parent;
        Assert.NotNull(root);
        var script = Path.Combine(root!.FullName, "scripts", "verify-http-report-provenance.py");
        var cli = typeof(TraceMapCommand).Assembly.Location;
        var result = await Verify(script, receiptPath, cli);
        Assert.True(result.ExitCode == 0, result.Error);
        Assert.Contains("CLI_AND_REPORTING_BYTES_MATCH; HANDOFF_LINK_MATCH", result.Output);
        Assert.Contains("not authenticated build", result.Output);
        foreach (var (path, hash) in before) Assert.Equal(hash, Hash(path));
        Assert.Equal(before.Count, Directory.GetFiles(fixture.Root, "*", SearchOption.AllDirectories).Length);

        // Real native JSON plus an incorrect component identity must fail even
        // though the CLI identity, selected root, and all path facts still match.
        File.WriteAllText(receiptPath, JsonSerializer.Serialize(receipt with { ReportingGeneratorSha256 = new('f', 64) }, JsonOptions));
        result = await Verify(script, receiptPath, cli);
        Assert.Equal(1, result.ExitCode);
        Assert.Equal("PROVENANCE_NOT_VERIFIED", result.Error.Trim());
        Assert.DoesNotContain(fixture.Root, result.Error);
    }

    private static async Task<(int ExitCode, string Output, string Error)> Verify(string script, string receipt, string cli)
    {
        using var process = new Process { StartInfo = new(Environment.GetEnvironmentVariable("TRACEMAP_PROVENANCE_PYTHON")!)
            { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true } };
        foreach (var arg in new[] { script, "--receipt", receipt, "--cli-dll", cli }) process.StartInfo.ArgumentList.Add(arg);
        Assert.True(process.Start());
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) { process.Kill(entireProcessTree: true); throw; }
        return (process.ExitCode, await output, await error);
    }
}
