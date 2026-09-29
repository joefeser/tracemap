namespace TraceMap.Cli;

/// <summary>Compose the existing explicit admission gates in one new owned review folder.</summary>
public static class WebFormsReviewStartCommand
{
    public static async Task<int> RunAsync(string[] args, TextWriter output, TextWriter error,
        LocalReviewScanRunner scanRunner, CancellationToken cancellationToken = default)
    {
        string? run = null;
        try
        {
            if (args.Length is not (5 or 7) || args[0] != "start" || args[1] != "--config" || args[3] != "--out"
                || (args.Length == 7 && args[5] != "--attest-exact-source-commit"))
                throw Invalid("ARGUMENT_INVALID");
            cancellationToken.ThrowIfCancellationRequested();
            var root = WebFormsReviewPreflightCommand.PhysicalPath(args[4]);
            // Validate the whole prospective root, not only its children, before
            // creating any output. No discovery, default attestation or overwrite.
            var plan = await WebFormsReviewPreflightCommand.BuildAsync(args[2], root, cancellationToken);
            var config = plan.Configuration;
            var originalConfiguration = plan.Inputs.Single(input => input.Role == "configuration");
            var prepare = args.Length == 7;
            if (prepare)
            {
                if (args[6] != config.SourceCommitSha) throw Invalid("ATTESTATION_COMMIT_MISMATCH");
                if (config.BindingReceipts.Length != 0 || config.PublishReceiptRelativePath is not null
                    || config.ReceiptRoot is not null || config.PreparationProvenance is not null)
                    throw Invalid("CONFIG_ALREADY_HAS_RECEIPTS");
                if (config.PublishSourceRelativePaths is not { Length: > 0 }) throw Invalid("EXPLICIT_SOURCE_MEMBERSHIP_REQUIRED");
            }
            else if (config.BindingReceipts.Length == 0 || config.PublishReceiptRelativePath is null)
                throw Invalid("RECEIPTS_OR_EXPLICIT_ATTESTATION_REQUIRED");
            var runtime = WebFormsReviewPreflightCommand.PhysicalPath(Path.GetDirectoryName(typeof(WebFormsReviewStartCommand).Assembly.Location)!);
            if (Within(root, runtime) || Within(runtime, root)) throw Invalid("OUTPUT_OVERLAPS_RUNTIME");
            await RecheckConfigurationAsync();
            cancellationToken.ThrowIfCancellationRequested();
            var parent = Path.GetDirectoryName(root)!;
            Directory.CreateDirectory(parent);
            var reservation = Path.Combine(parent, ".webforms-start-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(reservation);
            Directory.Move(reservation, root); // Atomic refusal if another starter owns this root.
            var configuration = args[2];
            if (prepare)
            {
                var evidence = Path.Combine(root, "evidence");
                if (await WebFormsReviewPreparationCommand.RunAsync(["prepare", "--config", configuration, "--out", evidence,
                        "--attest-exact-source-commit", args[6]], output, error, cancellationToken) != 0) return 1;
                configuration = Path.Combine(evidence, "review-config.local.json");
            }
            await RecheckConfigurationAsync();
            run = Path.Combine(root, "run");
            if (await WebFormsReviewPreflightCommand.RunAsync(["preflight", "--config", configuration, "--out", run],
                    output, error, cancellationToken) != 0) return 1;
            await RecheckConfigurationAsync();
            await output.WriteLineAsync($"webFormsReviewRoot={root}");
            return await WebFormsReviewExecutionCommand.RunAsync(["run", "--run", run], output, error, scanRunner, cancellationToken);

            async Task RecheckConfigurationAsync()
            {
                if (WebFormsReviewPreflightCommand.PhysicalPath(args[2]) != originalConfiguration.Path)
                    throw Invalid("CONFIGURATION_CHANGED");
                var current = await WebFormsReviewPreflightCommand.HashAsync("configuration", originalConfiguration.Path,
                    originalConfiguration.Bytes, cancellationToken);
                if (current.Bytes != originalConfiguration.Bytes || current.Sha256 != originalConfiguration.Sha256)
                    throw Invalid("CONFIGURATION_CHANGED");
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (InvalidDataException exception) when (exception.Message.StartsWith("WEBFORMS_NATIVE_START_", StringComparison.Ordinal))
        { await error.WriteLineAsync("error: " + exception.Message); return 1; }
        catch (WebFormsReviewPreflightCommand.PreflightException exception)
        { await error.WriteLineAsync("error: " + exception.Code); return 1; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        { await error.WriteLineAsync("error: WEBFORMS_NATIVE_START_INPUT_OR_OUTPUT_UNAVAILABLE"); return 1; }
        finally
        {
            // Failures preserve their exact owned attempts. Recovery always names
            // the pinned run rather than searching TEMP or repeating preparation.
            if (run is not null && File.Exists(Path.Combine(run, "run-manifest.json")))
                await output.WriteLineAsync($"webFormsPinnedRun={run};resumeCommand=webforms-review resume --run;cleanup=false");
        }
    }

    private static InvalidDataException Invalid(string code) => new("WEBFORMS_NATIVE_START_" + code);
    private static bool Within(string parent, string child) => string.Equals(parent, child,
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)
        || child.StartsWith(Path.TrimEndingDirectorySeparator(parent) + Path.DirectorySeparatorChar,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
}
