using System.Diagnostics;
using System.Text;
using System.Security.Cryptography;
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
            var outResult = await stdout;
            var errResult = await stderr;
            return new(process.ExitCode, outResult.Tail, errResult.Tail, outResult.Hash, errResult.Hash);
        }
        catch (Exception exception)
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync(CancellationToken.None);
            if (exception is OperationCanceledException && !cancellationToken.IsCancellationRequested)
                throw new InvalidOperationException("WEBFORMS_WIZARD_BUILD_TIMEOUT", exception);
            throw;
        }

        async Task<(string Tail, string Hash)> Read(StreamReader reader)
        {
            var result = new StringBuilder();
            var buffer = new char[4096];
            var bytes = new byte[8192];
            using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            int count;
            while ((count = await reader.ReadAsync(buffer.AsMemory(), timeout.Token)) != 0)
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
