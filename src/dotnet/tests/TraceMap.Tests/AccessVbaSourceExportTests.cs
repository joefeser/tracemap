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
        Assert.Contains("AccessVbaOriginalSourceChanged", script, StringComparison.Ordinal);
        Assert.Contains("AccessVbaSuppliedCopyChanged", script, StringComparison.Ordinal);
        Assert.Contains("AccessVbaWorkingCopyChanged", script, StringComparison.Ordinal);
        Assert.Contains("Properties.Delete(\"StartupForm\")", script, StringComparison.Ordinal);
        Assert.Contains("OpenCurrentDatabase($workingCopy, $true)", script, StringComparison.Ordinal);
        Assert.DoesNotContain("OpenCurrentDatabase($copy, $true)", script, StringComparison.Ordinal);
        Assert.Contains("AccessVbaGenerationCanaryFired", script, StringComparison.Ordinal);
        Assert.Contains("AccessVbaExtractionCanaryFired", script, StringComparison.Ordinal);
        Assert.Contains("workingCopyPreExportSha256", script, StringComparison.Ordinal);
        Assert.Contains("workingCopyPostExportSha256", script, StringComparison.Ordinal);
        Assert.DoesNotContain("AccessVbaSourceChanged", script, StringComparison.Ordinal);
        Assert.Contains("AccessVbaCanaryFired", script, StringComparison.Ordinal);
        Assert.Contains("Wait-Job -Job $job -Timeout $TimeoutSeconds", script, StringComparison.Ordinal);
        Assert.Contains("AccessVbaProcessCleanupFailed", script, StringComparison.Ordinal);
        Assert.Contains("FormReportMetadataDirectory", script, StringComparison.Ordinal);
        Assert.Contains("AccessVbaMetadataBundleBindingMismatch", script, StringComparison.Ordinal);
        Assert.Contains("Windows PowerShell 5.1 lacks the hashtable conversion switch", script, StringComparison.Ordinal);
        Assert.DoesNotContain("-AsHashtable", script, StringComparison.Ordinal);

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
        Assert.Contains("AccessVbaOriginalSourceChanged", smoke, StringComparison.Ordinal);
        Assert.Contains("AccessVbaWorkingCopyOutcomeInvalid", smoke, StringComparison.Ordinal);
        Assert.Contains("suppliedCopyOutcome=$expectedSuppliedCopyOutcome", smoke, StringComparison.Ordinal);
        Assert.Contains("workingCopyOutcome=$expectedWorkingCopyOutcome", smoke, StringComparison.Ordinal);
        Assert.DoesNotContain("OpenForm", smoke, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("OpenRecordset", smoke, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Windows_validation_scripts_avoid_PowerShell_7_only_hashtable_deserialization()
    {
        var root = FindRepoRoot();
        var scripts = Directory.GetFiles(Path.Combine(root, "scripts", "access-validation"), "*.ps1");

        foreach (var path in scripts)
        {
            var script = await File.ReadAllTextAsync(path);
            Assert.DoesNotContain("-AsHashtable", script, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Windows_validation_scripts_parenthesize_commands_used_with_boolean_operators()
    {
        var root = FindRepoRoot();
        var scripts = Directory.GetFiles(Path.Combine(root, "scripts", "access-validation"), "*.ps1");
        var unparenthesizedTestPath = new System.Text.RegularExpressions.Regex(
            @"(?m)(?<!\()Test-Path\s+-LiteralPath\s+[^\r\n()]+\s+-(?:or|and)\s+Test-Path\b",
            System.Text.RegularExpressions.RegexOptions.CultureInvariant);

        foreach (var path in scripts)
        {
            var script = await File.ReadAllTextAsync(path);
            Assert.DoesNotMatch(unparenthesizedTestPath, script);
        }
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
