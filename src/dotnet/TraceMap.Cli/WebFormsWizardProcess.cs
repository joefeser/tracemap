using System.Diagnostics;
using System.Text;
using TraceMap.Core;

namespace TraceMap.Cli;

public static class WebFormsWizardProcess
{
    public static async Task<WebFormsWizardProcessResult> RunAsync(string executable, string directory,
        IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(30));
        using var process = new Process { StartInfo = new(executable)
        { WorkingDirectory = directory, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true } };
        foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);
        if (!process.Start()) throw new InvalidOperationException("WEBFORMS_WIZARD_BUILD_START_FAILED");
        try
        {
            var stdout = Read(process.StandardOutput);
            var stderr = Read(process.StandardError);
            await Task.WhenAll(stdout, stderr, process.WaitForExitAsync(timeout.Token));
            return new(process.ExitCode, await stdout, await stderr);
        }
        catch (Exception exception)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None);
            if (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested)
                throw new InvalidOperationException("WEBFORMS_WIZARD_BUILD_TIMEOUT", exception);
            throw;
        }

        async Task<string> Read(StreamReader reader)
        {
            var result = new StringBuilder();
            var buffer = new char[4096];
            int count;
            while ((count = await reader.ReadAsync(buffer.AsMemory(), timeout.Token)) != 0)
            {
                if (result.Length + count > 65_536)
                {
                    timeout.Cancel();
                    throw new InvalidOperationException("WEBFORMS_WIZARD_BUILD_OUTPUT_LIMIT");
                }
                result.Append(buffer, 0, count);
            }
            return result.ToString();
        }
    }
}
