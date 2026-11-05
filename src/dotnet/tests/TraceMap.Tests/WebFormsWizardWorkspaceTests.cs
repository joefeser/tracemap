using System.Text.Json;
using System.Text.Json.Nodes;
using TraceMap.Cli;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class WebFormsWizardWorkspaceTests
{
    [Fact]
    public async Task Existing_wizard_continuation_offers_workspace_without_a_switch_list_or_report_resume()
    {
        using var fixture = new WorkspaceFromWizardTests.Fixture();
        await fixture.Prepare();
        using var input = new StringReader($"workspace\n{fixture.Framework}\nlater\n");
        var calls = 0;
        Task<int> NoNative(string[] args, TextWriter output, TextWriter error, CancellationToken token)
        { calls++; throw new InvalidOperationException("workspace setup must not resume a scan or native reports"); }
        Assert.Equal(2, await WebFormsWizardCommand.RunAsync(["wizard", "--root", fixture.WizardRoot, "--continue"],
            input, fixture.Output, fixture.Error, nativeRunner: NoNative));
        Assert.Equal(0, calls);
        Assert.True(File.Exists(Path.Combine(fixture.WizardRoot, "site", "workspace-answers.local.json")), fixture.Output.ToString());
    }

    [Fact]
    public async Task Pause_remembers_answers_and_resume_creates_workspace_in_the_same_root_without_reasking()
    {
        using var fixture = new WorkspaceFromWizardTests.Fixture();
        await fixture.Prepare();
        using (var store = WebFormsWizardStore.Open(fixture.WizardRoot, true))
        using (var input = new StringReader($"{fixture.Framework}\nFramework.csproj\nlater\n"))
            Assert.Equal(2, await WebFormsWizardWorkspace.RunAsync(store, "site", input, fixture.Output, fixture.Error, default));
        var path = Path.Combine(fixture.WizardRoot, "site", "workspace-answers.local.json");
        using (var document = JsonDocument.Parse(File.ReadAllText(path)))
        {
            Assert.Equal("local-only", document.RootElement.GetProperty("visibility").GetString());
            Assert.Matches("^[0-9a-f]{64}$", document.RootElement.GetProperty("generatorSha256").GetString()!);
            Assert.Matches("^[0-9a-f]{64}$", document.RootElement.GetProperty("boundedInputSha256").GetString()!);
        }
        var projectBefore = File.ReadAllBytes(Path.Combine(fixture.WizardRoot, "site", "project.config.json"));
        fixture.Output.GetStringBuilder().Clear();
        using (var store = WebFormsWizardStore.Open(fixture.WizardRoot, true))
        using (var input = new StringReader($"{fixture.Manifest}\n{fixture.SqlRoot}\ncreate\n"))
            Assert.True(await WebFormsWizardWorkspace.RunAsync(store, "site", input, fixture.Output, fixture.Error, default) == 0, fixture.Error.ToString());
        Assert.DoesNotContain("Framework SOURCE repository folder", fixture.Output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("Framework project/solution: choose", fixture.Output.ToString(), StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(fixture.WizardRoot, "workspace.local.json")));
        Assert.False(Directory.Exists(Path.Combine(fixture.WizardRoot, "sources")));
        Assert.Equal(projectBefore, File.ReadAllBytes(Path.Combine(fixture.WizardRoot, "site", "project.config.json")));
    }

    [Theory]
    [InlineData("1")]
    [InlineData("2")]
    [InlineData("3")]
    [InlineData("4")]
    public async Task Change_one_saved_choice_keeps_other_inputs_and_does_not_create_early(string field)
    {
        using var fixture = new WorkspaceFromWizardTests.Fixture();
        await fixture.Prepare();
        using (var store = WebFormsWizardStore.Open(fixture.WizardRoot, true))
        using (var input = new StringReader($"{fixture.Framework}\nFramework.csproj\n{fixture.Manifest}\n{fixture.SqlRoot}\nlater\n"))
            Assert.Equal(2, await WebFormsWizardWorkspace.RunAsync(store, "site", input, fixture.Output, fixture.Error, default));
        using (var store = WebFormsWizardStore.Open(fixture.WizardRoot, true))
        using (var input = new StringReader($"change\n{field}\nlater\n"))
            Assert.Equal(2, await WebFormsWizardWorkspace.RunAsync(store, "site", input, fixture.Output, fixture.Error, default));
        using var saved = JsonDocument.Parse(File.ReadAllText(Path.Combine(fixture.WizardRoot, "site", "workspace-answers.local.json")));
        var answers = saved.RootElement.GetProperty("answers");
        Assert.Equal(field == "1" ? JsonValueKind.Null : JsonValueKind.String, answers.GetProperty("frameworkRoot").ValueKind);
        Assert.Equal(field is "1" or "2" ? JsonValueKind.Null : JsonValueKind.String, answers.GetProperty("frameworkTarget").ValueKind);
        Assert.Equal(field == "3" ? JsonValueKind.Null : JsonValueKind.String, answers.GetProperty("sqlManifest").ValueKind);
        Assert.Equal(field == "4" ? JsonValueKind.Null : JsonValueKind.String, answers.GetProperty("sqlRoot").ValueKind);
        Assert.False(File.Exists(Path.Combine(fixture.WizardRoot, "workspace.local.json")));
    }

    [Fact]
    public async Task Tampered_answers_are_refused_and_preserved()
    {
        using var fixture = new WorkspaceFromWizardTests.Fixture();
        await fixture.Prepare();
        using (var store = WebFormsWizardStore.Open(fixture.WizardRoot, true))
        using (var input = new StringReader($"{fixture.Framework}\nlater\n"))
            Assert.Equal(2, await WebFormsWizardWorkspace.RunAsync(store, "site", input, fixture.Output, fixture.Error, default));
        var path = Path.Combine(fixture.WizardRoot, "site", "workspace-answers.local.json");
        var changed = JsonNode.Parse(File.ReadAllText(path))!;
        changed["answers"]!["frameworkRoot"] = "different-folder";
        File.WriteAllText(path, changed.ToJsonString());
        var bytes = File.ReadAllBytes(path);
        using var resumed = WebFormsWizardStore.Open(fixture.WizardRoot, true);
        using var empty = new StringReader("");
        Assert.Contains("ANSWERS_INVALID", (await Assert.ThrowsAsync<InvalidOperationException>(() =>
            WebFormsWizardWorkspace.RunAsync(resumed, "site", empty, fixture.Output, fixture.Error, default))).Message, StringComparison.Ordinal);
        Assert.Equal(bytes, File.ReadAllBytes(path));
    }

    [Fact]
    public async Task Existing_workspace_can_be_verified_without_asking_for_all_inputs_again()
    {
        using var fixture = new WorkspaceFromWizardTests.Fixture();
        await fixture.Prepare();
        using (var store = WebFormsWizardStore.Open(fixture.WizardRoot, true))
        using (var input = new StringReader($"{fixture.Framework}\nFramework.csproj\n{fixture.Manifest}\n{fixture.SqlRoot}\ncreate\n"))
            Assert.True(await WebFormsWizardWorkspace.RunAsync(store, "site", input, fixture.Output, fixture.Error, default) == 0, fixture.Error.ToString());
        var path = Path.Combine(fixture.WizardRoot, "workspace.local.json");
        var original = File.ReadAllBytes(path);
        fixture.Output.GetStringBuilder().Clear();
        using (var store = WebFormsWizardStore.Open(fixture.WizardRoot, true))
        using (var input = new StringReader("verify\n"))
            Assert.Equal(0, await WebFormsWizardWorkspace.RunAsync(store, "site", input, fixture.Output, fixture.Error, default));
        Assert.Equal(original, File.ReadAllBytes(path));
        Assert.DoesNotContain("Framework SOURCE repository folder", fixture.Output.ToString(), StringComparison.Ordinal);
    }
}
