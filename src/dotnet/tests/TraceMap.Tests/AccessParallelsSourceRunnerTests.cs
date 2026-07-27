namespace TraceMap.Tests;

public sealed class AccessParallelsSourceRunnerTests
{
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

        Assert.Contains("ValidateSet(\"doctor\", \"build\", \"synthetic\")", host, StringComparison.Ordinal);
        Assert.Contains("net0\\s+\\(-\\)", host, StringComparison.Ordinal);
        Assert.Contains("mode='ro'", host, StringComparison.Ordinal);
        Assert.Contains("mode='rw'", host, StringComparison.Ordinal);
        Assert.Contains("AccessParallelsNetworkEnabled", host, StringComparison.Ordinal);
        Assert.Contains(
            "sourceClean=true;remoteAbsent=true;accessAvailable=true$",
            host,
            StringComparison.Ordinal);
        Assert.Contains(
            "buildPassed=true;accessTestsPassed=true;sourceClean=true$",
            host,
            StringComparison.Ordinal);
        Assert.Contains(
            "consumerContracts=completed;reviewBundleRetained=true;processCleanup=true;sourceClean=true$",
            host,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "[A-Za-z][A-Za-z0-9]*=",
            host,
            StringComparison.Ordinal);
        Assert.DoesNotContain("prlctl set", host, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("remote", guest, StringComparison.Ordinal);
        Assert.Contains("status --porcelain", guest, StringComparison.Ordinal);
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
        Assert.Contains("$statusAfterExit = $LASTEXITCODE", guest, StringComparison.Ordinal);
        Assert.Contains("finally {", guest, StringComparison.Ordinal);
        Assert.Contains(
            "Remove-Item $smokeRoot -Recurse -Force -ErrorAction Stop",
            guest,
            StringComparison.Ordinal);
        Assert.Contains("AccessGuestSyntheticCleanupFailed", guest, StringComparison.Ordinal);
        Assert.DoesNotContain("--filter \"Access\"", guest, StringComparison.Ordinal);
        Assert.DoesNotContain("Invoke-AccessRepresentativeSmoke.ps1", guest, StringComparison.Ordinal);

        foreach (var source in new[] { host, guest })
        {
            Assert.DoesNotContain("Application.VBE", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("VBComponents", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("RunMacro", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("OpenRecordset", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("OpenQuery", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("SaveAsText", source, StringComparison.OrdinalIgnoreCase);
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
