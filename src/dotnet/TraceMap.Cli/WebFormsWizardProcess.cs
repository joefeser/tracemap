using System.Diagnostics;
using System.Text;
using System.Security.Cryptography;
using TraceMap.Core;

namespace TraceMap.Cli;

public static class WebFormsWizardProcess
{
    public static Task<WebFormsWizardProcessResult> RunAsync(string executable, string directory,
        IReadOnlyList<string> arguments, CancellationToken cancellationToken) =>
        RunAsync(executable, directory, arguments, cancellationToken, TimeSpan.FromMinutes(30), TimeSpan.FromSeconds(30));

    internal static async Task<WebFormsWizardProcessResult> RunAsync(string executable, string directory,
        IReadOnlyList<string> arguments, CancellationToken cancellationToken, TimeSpan buildTimeout, TimeSpan drainTimeout)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(buildTimeout);
        using var process = new Process { StartInfo = new(executable)
        { WorkingDirectory = directory, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true } };
        foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);
        // Reused MSBuild nodes and the shared compiler server outlive the build and inherit
        // the redirected pipes, so end-of-stream would never arrive after a successful exit.
        process.StartInfo.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        process.StartInfo.Environment["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0";
        process.StartInfo.Environment["UseSharedCompilation"] = "false";
        using var reads = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token);
        if (!process.Start()) throw new InvalidOperationException("WEBFORMS_WIZARD_BUILD_START_FAILED");
        var stdout = Read(process.StandardOutput);
        var stderr = Read(process.StandardError);
        var output = Task.WhenAll(stdout, stderr);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
            // WaitAsync bounds the wait independently of whether the OS pipe honors
            // cancellation of an outstanding read (Windows synchronous pipes do not).
            try { await output.WaitAsync(drainTimeout, timeout.Token); }
            catch (TimeoutException exception)
            { throw new InvalidOperationException("WEBFORMS_WIZARD_BUILD_OUTPUT_UNTERMINATED", exception); }
            var outResult = await stdout;
            var errResult = await stderr;
            return new(process.ExitCode, outResult.Tail, errResult.Tail, outResult.Hash, errResult.Hash);
        }
        catch (Exception exception)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));
            if (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested)
                throw new InvalidOperationException("WEBFORMS_WIZARD_BUILD_TIMEOUT", exception);
            throw;
        }
        finally
        {
            reads.Cancel();
            process.StandardOutput.Dispose();
            process.StandardError.Dispose();
            // A disposed pipe may fault an outstanding read after the bounded caller
            // has returned. Observe it without waiting indefinitely for the writer.
            _ = output.ContinueWith(task => { _ = task.Exception; }, CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        }

        async Task<(string Tail, string Hash)> Read(StreamReader reader)
        {
            var result = new StringBuilder();
            var buffer = new char[4096];
            var bytes = new byte[8192];
            using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            int count;
            while ((count = await reader.ReadAsync(buffer.AsMemory(), reads.Token)) != 0)
            {
                // Hash every decoded UTF-16 code unit in little-endian order, including split surrogates.
                for (var i = 0; i < count; i++)
                {
                    bytes[2 * i] = (byte)buffer[i];
                    bytes[2 * i + 1] = (byte)(buffer[i] >> 8);
                }
                digest.AppendData(bytes, 0, count * 2);
                result.Append(buffer, 0, count);
                if (result.Length > 65_536) result.Remove(0, result.Length - 65_536);
            }
            return (result.ToString(), Convert.ToHexStringLower(digest.GetHashAndReset()));
        }
    }
}
