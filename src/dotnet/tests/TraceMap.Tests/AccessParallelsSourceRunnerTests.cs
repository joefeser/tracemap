namespace TraceMap.Tests;

public sealed class AccessParallelsSourceRunnerTests
{
    [Theory]
    [InlineData("prefix\r\nprotected=true")]
    [InlineData("prefix\rprotected=true")]
    [InlineData("prefix\nprotected=true")]
    [InlineData("prefix\0protected=true")]
    [InlineData("protected=true\nprefix")]
    [InlineData("PREFIX")]
    public void Exact_host_output_contract_rejects_nonidentical_guest_output(string candidate)
    {
        const string expected = "prefix";

        Assert.False(string.Equals(candidate, expected, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Source_runners_preserve_isolation_and_existing_extraction_boundary()
    {
        var root = FindRepoRoot();
        var host = await File.ReadAllTextAsync(Path.Combine(
            root,
            "scripts",
            "access-validation",
            "Invoke-AccessParallelsSource.ps1"));
        var guest = await File.ReadAllTextAsync(Path.Combine(
            root,
            "scripts",
            "access-validation",
            "Invoke-AccessGuestSource.ps1"));

        Assert.Contains("ValidateSet(\"doctor\", \"build\", \"synthetic\", \"metadata\")", host, StringComparison.Ordinal);
        Assert.Contains("net0\\s+\\(-\\)", host, StringComparison.Ordinal);
        Assert.Contains("$ExpectedInputSharePath", host, StringComparison.Ordinal);
        Assert.Contains("$ExpectedOutputSharePath", host, StringComparison.Ordinal);
        Assert.Contains("Get-CanonicalHostPath", host, StringComparison.Ordinal);
        Assert.Contains("$_.Name -eq \"access_input\"", host, StringComparison.Ordinal);
        Assert.Contains("$_.Name -eq \"access_output\"", host, StringComparison.Ordinal);
        Assert.Contains("$_.Path, $expectedInputPath", host, StringComparison.Ordinal);
        Assert.Contains("$_.Path, $expectedOutputPath", host, StringComparison.Ordinal);
        Assert.Contains("$_.Mode -eq \"ro\"", host, StringComparison.Ordinal);
        Assert.Contains("$_.Mode -eq \"rw\"", host, StringComparison.Ordinal);
        Assert.Contains("AccessParallelsNetworkEnabled", host, StringComparison.Ordinal);
        Assert.Contains(
            "$vmInfo -match \"(?m)^\\s+net\\d+\\s+\\(\\+\\)\"",
            host,
            StringComparison.Ordinal);
        Assert.Contains("$shares.Count -ne 2", host, StringComparison.Ordinal);
        Assert.Contains(
            "sourceClean=true;remoteAbsent=true;accessAvailable=true\"",
            host,
            StringComparison.Ordinal);
        Assert.Contains(
            "buildPassed=true;accessTestsPassed=true;sourceClean=true\"",
            host,
            StringComparison.Ordinal);
        Assert.Contains(
            "consumerContracts=completed;reviewBundleRetained=true;processCleanup=true;sourceClean=true\"",
            host,
            StringComparison.Ordinal);
        Assert.Contains("[StringComparison]::Ordinal", host, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "[A-Za-z][A-Za-z0-9]*=",
            host,
            StringComparison.Ordinal);
        Assert.Equal(
            1,
            host.Split("Write-Output", StringSplitOptions.None).Length - 1);
        Assert.DoesNotContain("Write-Host", host, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("prlctl set", host, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("remote", guest, StringComparison.Ordinal);
        Assert.Contains("status --porcelain", guest, StringComparison.Ordinal);
        Assert.Contains("Get-SourceIdentity", guest, StringComparison.Ordinal);
        Assert.True(
            guest.Split(
                "Test-ExpectedIdentity",
                StringSplitOptions.None).Length - 1 >= 5);
        Assert.Contains("Get-FileHash -LiteralPath $git", guest, StringComparison.Ordinal);
        Assert.Contains("Get-FileHash -LiteralPath $dotnet", guest, StringComparison.Ordinal);
        Assert.Contains("[IO.FileAttributes]::ReparsePoint", guest, StringComparison.Ordinal);
        Assert.Contains("$packages = Join-Path $GuestRoot \"packages\"", guest, StringComparison.Ordinal);
        Assert.Contains("Test-TrustedPath $packages $GuestRoot", guest, StringComparison.Ordinal);
        Assert.Contains("$env:NUGET_PACKAGES = $packages", guest, StringComparison.Ordinal);
        Assert.Contains("$artifactParents = @(", guest, StringComparison.Ordinal);
        Assert.Contains("Join-Path $GuestRoot \"runs\"", guest, StringComparison.Ordinal);
        Assert.Contains("Join-Path $GuestRoot \"checkpoints\"", guest, StringComparison.Ordinal);
        Assert.Contains("Join-Path $GuestRoot \"review-bundles\"", guest, StringComparison.Ordinal);
        Assert.Contains("Test-TrustedPath $artifactParent $GuestRoot", guest, StringComparison.Ordinal);
        Assert.Contains("AccessGuestSyntheticOutputBoundaryInvalid", guest, StringComparison.Ordinal);
        Assert.Contains("Invoke-AccessSmoke.ps1", guest, StringComparison.Ordinal);
        Assert.Contains("phase9ConsumerContracts", guest, StringComparison.Ordinal);
        Assert.Contains("localReviewBundleContractCorrect", guest, StringComparison.Ordinal);
        Assert.Contains("TraceMap.Tests.AccessFoundationTests", guest, StringComparison.Ordinal);
        Assert.Contains("TraceMap.Tests.AccessLocalReviewBundleTests", guest, StringComparison.Ordinal);
        Assert.Contains("FullyQualifiedName~$testClass", guest, StringComparison.Ordinal);
        Assert.Equal(
            2,
            guest.Split(
                "& $dotnet build $solution --no-restore --verbosity quiet",
                StringSplitOptions.None).Length - 1);
        Assert.Contains("AccessGuestSyntheticBuildFailed", guest, StringComparison.Ordinal);
        Assert.Contains("$headExit = $LASTEXITCODE", guest, StringComparison.Ordinal);
        Assert.Contains("$statusExit = $LASTEXITCODE", guest, StringComparison.Ordinal);
        Assert.Contains("$remoteExit = $LASTEXITCODE", guest, StringComparison.Ordinal);
        Assert.Contains("$harnessExit = 0", guest, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "$harnessExit = $LASTEXITCODE",
            guest,
            StringComparison.Ordinal);
        Assert.Contains("finally {", guest, StringComparison.Ordinal);
        Assert.Contains(
            "Remove-Item $smokeRoot -Recurse -Force -ErrorAction Stop",
            guest,
            StringComparison.Ordinal);
        Assert.Contains(
            "Remove-Item $reviewBundle -Recurse -Force -ErrorAction Stop",
            guest,
            StringComparison.Ordinal);
        Assert.Contains(
            "Split-Path -Leaf $checkpoint",
            guest,
            StringComparison.Ordinal);
        Assert.Contains(
            "Remove-Item -LiteralPath $checkpoint -Force -ErrorAction Stop",
            guest,
            StringComparison.Ordinal);
        Assert.Contains("$syntheticSucceeded = $true", guest, StringComparison.Ordinal);
        Assert.Contains("AccessGuestSyntheticCleanupFailed", guest, StringComparison.Ordinal);
        Assert.DoesNotContain("--filter \"Access\"", guest, StringComparison.Ordinal);
        Assert.DoesNotContain("Invoke-AccessRepresentativeSmoke.ps1", guest, StringComparison.Ordinal);
        Assert.Contains("Invoke-AccessMetadataProducerSmoke.ps1", guest, StringComparison.Ordinal);
        Assert.Contains("access-parallels-metadata=completed", host, StringComparison.Ordinal);
        var producer = await File.ReadAllTextAsync(Path.Combine(
            root,
            "scripts",
            "access-validation",
            "Export-AccessFormReportMetadata.ps1"));
        Assert.Contains("$access.AutomationSecurity = 3", producer, StringComparison.Ordinal);
        Assert.Contains("$access.Visible = $false", producer, StringComparison.Ordinal);
        Assert.Contains("$access.SaveAsText(", producer, StringComparison.Ordinal);
        Assert.Contains("AccessMetadataCopyBindingMismatch", producer, StringComparison.Ordinal);
        Assert.Contains("[int]$TimeoutSeconds = 300", producer, StringComparison.Ordinal);
        Assert.Contains("Wait-Job -Job $workerJob -Timeout $TimeoutSeconds", producer, StringComparison.Ordinal);
        Assert.Contains("AccessMetadataTimeout", producer, StringComparison.Ordinal);
        Assert.Contains("$workerProcessMarker", producer, StringComparison.Ordinal);
        Assert.Contains("$workerHostMarker", producer, StringComparison.Ordinal);
        Assert.Contains("Get-CimInstance -ClassName Win32_Process", producer, StringComparison.Ordinal);
        Assert.Contains("Get-OwnedAccessProcesses", producer, StringComparison.Ordinal);
        Assert.Contains("ConvertTo-AccessProcessIdentities", producer, StringComparison.Ordinal);
        Assert.Contains("Remove-Job -Job $workerJob -Force", producer, StringComparison.Ordinal);
        Assert.Contains("try { $workerJob.Dispose() } catch { }", producer, StringComparison.Ordinal);
        Assert.Contains("$workerJob = $null", producer, StringComparison.Ordinal);
        Assert.Contains("$observedAccess = @(Get-Process -Name \"MSACCESS\"", producer, StringComparison.Ordinal);
        Assert.Contains("$remainingOwnedAccess = @(Get-OwnedAccessProcesses $ownedAccessProcessIdentities)", producer, StringComparison.Ordinal);
        Assert.Contains("$processCleanupFailed = $remainingOwnedAccess.Count -gt 0 -or $unattributedAccess.Count -gt 0", producer, StringComparison.Ordinal);
        Assert.Contains("GetWindowThreadProcessId", producer, StringComparison.Ordinal);
        Assert.Contains("$Application.hWndAccessApp()", producer, StringComparison.Ordinal);
        Assert.Contains("Close-ComObject $dbEngine", producer, StringComparison.Ordinal);
        Assert.Contains("Close-ComObject $currentProject", producer, StringComparison.Ordinal);
        Assert.Contains("Close-ComObject $surfaceProject", producer, StringComparison.Ordinal);
        Assert.Contains("[uint32]([int]::MaxValue)", producer, StringComparison.Ordinal);
        Assert.Contains("startTimeUtcTicks", producer, StringComparison.Ordinal);
        Assert.Contains("ProcessName, \"MSACCESS\"", producer, StringComparison.Ordinal);
        Assert.Contains("AccessMetadataProcessOwnershipAmbiguous", producer, StringComparison.Ordinal);
        Assert.Contains("Get-LoadedState $access", producer, StringComparison.Ordinal);
        Assert.Contains("AccessMetadataSourceChanged", producer, StringComparison.Ordinal);
        Assert.Contains("$guardDatabase.Properties.Delete(\"StartupForm\")", producer, StringComparison.Ordinal);
        Assert.Contains("$workerScratchDirectory", producer, StringComparison.Ordinal);
        Assert.Contains("$workerParameters[\"WorkerScratchDirectoryPath\"] = $workerScratchDirectory", producer, StringComparison.Ordinal);
        Assert.DoesNotContain("$scratchPattern", producer, StringComparison.Ordinal);
        Assert.Contains("Remove-Item -LiteralPath $workerScratchDirectory -Recurse -Force -ErrorAction Stop", producer, StringComparison.Ordinal);
        Assert.Contains("AccessMetadataProcessMarkerBindingMismatch", producer, StringComparison.Ordinal);
        Assert.Contains("$createdOutput = $true", producer, StringComparison.Ordinal);
        Assert.Contains("if ($createdOutput -and (-not $succeeded -or $cleanupFailure)", producer, StringComparison.Ordinal);
        Assert.Contains("AccessMetadataProcessCleanupFailed", producer, StringComparison.Ordinal);
        Assert.Contains("AccessMetadataOutputCleanupFailed", producer, StringComparison.Ordinal);
        Assert.Contains("Test-SourceHashesUnchanged", producer, StringComparison.Ordinal);
        Assert.Contains("$scratchCleanupFailed = $true", producer, StringComparison.Ordinal);
        Assert.DoesNotContain("OpenForm", producer, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("OpenReport", producer, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("OpenQuery", producer, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("OpenRecordset", producer, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(".Module", producer, StringComparison.OrdinalIgnoreCase);
        var metadataHarness = await File.ReadAllTextAsync(Path.Combine(
            root,
            "scripts",
            "access-validation",
            "Invoke-AccessMetadataProducerSmoke.ps1"));
        Assert.Contains("-TimeoutSeconds 240", metadataHarness, StringComparison.Ordinal);
        Assert.Contains("Wait-Job -Job $producerJob -Timeout 300", metadataHarness, StringComparison.Ordinal);
        Assert.Contains("Stop-Process -Force", metadataHarness, StringComparison.Ordinal);

        foreach (var source in new[] { host, guest })
        {
            Assert.DoesNotContain("Application.VBE", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("VBComponents", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("RunMacro", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("OpenRecordset", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("OpenQuery", source, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "AGENTS.md")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Repository root was not found.");
    }
}
