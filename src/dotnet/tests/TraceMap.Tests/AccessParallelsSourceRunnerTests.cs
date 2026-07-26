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
        Assert.DoesNotContain("prlctl set", host, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("remote", guest, StringComparison.Ordinal);
        Assert.Contains("status --porcelain", guest, StringComparison.Ordinal);
        Assert.Contains("Invoke-AccessSmoke.ps1", guest, StringComparison.Ordinal);
        Assert.Contains("phase9ConsumerContracts", guest, StringComparison.Ordinal);
        Assert.Contains("localReviewBundleContractCorrect", guest, StringComparison.Ordinal);
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
