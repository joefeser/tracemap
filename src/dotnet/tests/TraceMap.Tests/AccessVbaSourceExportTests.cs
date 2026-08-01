namespace TraceMap.Tests;

public sealed class AccessVbaSourceExportTests
{
    [Fact]
    public async Task Source_exporter_uses_save_as_text_and_preserves_the_separate_private_source_boundary()
    {
        var root = FindRepoRoot();
        var script = await File.ReadAllTextAsync(Path.Combine(
            root,
            "scripts",
            "access-validation",
            "Export-AccessVbaSource.ps1"));

        Assert.Contains("$access.AutomationSecurity = 3", script, StringComparison.Ordinal);
        Assert.Contains("$access.Visible = $false", script, StringComparison.Ordinal);
        Assert.Contains("$access.SaveAsText($AcModule, $name, $rawPath)", script, StringComparison.Ordinal);
        Assert.Contains("private-access-source", script, StringComparison.Ordinal);
        Assert.Contains("normalized-design-evidence", script, StringComparison.Ordinal);
        Assert.Contains("sourceText = $source", script, StringComparison.Ordinal);
        Assert.Contains("ui-design-document", script, StringComparison.Ordinal);
        Assert.Contains("formReportDesignFileCount", script, StringComparison.Ordinal);
        Assert.Contains("Get-LoadedModuleCount", script, StringComparison.Ordinal);
        Assert.Contains("AccessVbaLoadedStateChanged", script, StringComparison.Ordinal);
        Assert.Contains("AccessVbaSourceChanged", script, StringComparison.Ordinal);
        Assert.Contains("AccessVbaCanaryFired", script, StringComparison.Ordinal);
        Assert.Contains("Wait-Job -Job $job -Timeout $TimeoutSeconds", script, StringComparison.Ordinal);
        Assert.Contains("AccessVbaProcessCleanupFailed", script, StringComparison.Ordinal);
        Assert.Contains("FormReportMetadataDirectory", script, StringComparison.Ordinal);
        Assert.Contains("AccessVbaMetadataBundleBindingMismatch", script, StringComparison.Ordinal);

        foreach (var forbidden in new[]
        {
            "Application.VBE",
            "ActiveVBProject",
            "VBComponents",
            "CodeModule",
            "OpenForm",
            "OpenReport",
            "OpenQuery",
            "OpenRecordset",
            "RunMacro"
        })
        {
            Assert.DoesNotContain(forbidden, script, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Synthetic_fixture_and_smoke_cover_an_expression_event_without_execution()
    {
        var root = FindRepoRoot();
        var generator = await File.ReadAllTextAsync(Path.Combine(
            root, "scripts", "access-validation", "New-SyntheticAccessFixture.ps1"));
        var smoke = await File.ReadAllTextAsync(Path.Combine(
            root, "scripts", "access-validation", "Invoke-AccessVbaSourceProducerSmoke.ps1"));

        Assert.Contains("=RunSyntheticScenario()", generator, StringComparison.Ordinal);
        Assert.Contains("Public Function RunSyntheticScenario()", generator, StringComparison.Ordinal);
        Assert.Contains("$lifecycleList.AfterUpdate = \"[Event Procedure]\"", generator, StringComparison.Ordinal);
        Assert.DoesNotContain("$lifecycleList.OnAfterUpdate", generator, StringComparison.Ordinal);
        Assert.Contains("SyntheticOpenArgsMarker_92817", generator, StringComparison.Ordinal);
        Assert.Contains("VbaProducer", smoke, StringComparison.Ordinal);
        Assert.Contains("GenerationCanaryPath", smoke, StringComparison.Ordinal);
        Assert.Contains("ExtractionCanaryPath", smoke, StringComparison.Ordinal);
        Assert.Contains("normalized-design-evidence", smoke, StringComparison.Ordinal);
        Assert.Contains("private-access-source", smoke, StringComparison.Ordinal);
        Assert.DoesNotContain("OpenForm", smoke, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("OpenRecordset", smoke, StringComparison.OrdinalIgnoreCase);
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "AGENTS.md")))
                return current.FullName;
            current = current.Parent;
        }

        throw new InvalidOperationException("Repository root was not found.");
    }
}
