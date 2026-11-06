using System.Text.Json;
using System.Text.Json.Nodes;
using TraceMap.Cli;

namespace TraceMap.Tests;

public sealed class WorkspaceAddSourceTests
{
    [Fact]
    public async Task New_nested_selection_normalizes_separators_and_allows_dots_inside_a_filename()
    {
        using var fixture = new SourceFixture();
        Directory.CreateDirectory(Path.Combine(fixture.Framework, "src"));
        File.WriteAllText(Path.Combine(fixture.Framework, "src", "Library..v2.csproj"), "<Project />");
        var args = fixture.Args; args[Array.IndexOf(args, "--target") + 1] = @"src\Library..v2.csproj";
        Assert.True(await WorkspaceCommand.RunAsync(args, fixture.Output, fixture.Error) == 0, fixture.Error.ToString());
        var config = JsonNode.Parse(File.ReadAllText(fixture.Config))!;
        Assert.Equal("src/Library..v2.csproj", config["repositories"]![1]!["projects"]![0]!.GetValue<string>());
    }

    [Theory]
    [InlineData("absolute-inside", true)]
    [InlineData("parent-inside", true)]
    [InlineData("outside", false)]
    public async Task Legacy_saved_selection_keeps_containment_admission_without_rewriting(string kind, bool accepted)
    {
        using var fixture = new SourceFixture();
        var project = Path.Combine(fixture.Web, "Web.csproj"); File.WriteAllText(project, "<Project />");
        var config = JsonNode.Parse(File.ReadAllText(fixture.Config))!;
        config["repositories"]![0]!["projects"] = new JsonArray(kind switch
        {
            "absolute-inside" => project,
            "parent-inside" => "child/../Web.csproj",
            _ => Path.Combine(fixture.Framework, "Library.csproj")
        });
        File.WriteAllText(fixture.Config, config.ToJsonString()); var before = File.ReadAllBytes(fixture.Config);
        Assert.Equal(accepted ? 0 : 1, await WorkspaceCommand.RunAsync(["verify", "--root", fixture.Root], fixture.Output, fixture.Error));
        Assert.Equal(before, File.ReadAllBytes(fixture.Config));
        if (!accepted) Assert.Contains("WORKSPACE_PROJECT_MISSING_OR_OUTSIDE_REPOSITORY", fixture.Error.ToString());
    }

    [Fact]
    public async Task Add_source_preserves_v3_channels_and_completed_run_without_revalidating_or_scanning_them()
    {
        using var fixture = new WorkspaceFromWizardTests.Fixture(); await fixture.Prepare();
        var args = fixture.Arguments("Framework.csproj").ToList();
        foreach (var option in new[] { "--framework-repo", "--framework-target" })
        { var index = args.IndexOf(option); args.RemoveRange(index, 2); }
        Assert.True(await WorkspaceCommand.RunAsync(args.ToArray(), fixture.Output, fixture.Error) == 0, fixture.Error.ToString());
        var before = File.ReadAllBytes(fixture.Destination);
        var original = JsonNode.Parse(before)!;
        var oldRun = Path.Combine(fixture.Workspace, "runs", "workspace-original"); Directory.CreateDirectory(oldRun);
        var oldEvidence = Path.Combine(oldRun, "report.md"); File.WriteAllText(oldEvidence, "preserve old scope evidence");
        // Configuration extension must not parse/re-attest existing channel inputs.
        File.AppendAllText(fixture.Manifest, " invalid-current-input-for-this-test");
        var sqlBefore = File.ReadAllBytes(fixture.Manifest); var nativeBefore = File.ReadAllBytes(fixture.Prepared);
        var calls = 0;
        Task<int> Never(string[] command, TextWriter stdout, TextWriter stderr, CancellationToken token)
        { calls++; throw new InvalidOperationException("add-source must not scan"); }
        Assert.True(await WorkspaceCommand.RunAsync(["add-source", "--root", fixture.Workspace, "--id", "framework", "--repo", fixture.Framework, "--target", "Framework.csproj"], fixture.Output, fixture.Error, Never) == 0, fixture.Error.ToString());
        Assert.Equal(0, calls);
        var changed = JsonNode.Parse(File.ReadAllBytes(fixture.Destination))!;
        Assert.Equal(2, changed["repositories"]!.AsArray().Count);
        Assert.Equal(original["repositories"]![0]!.ToJsonString(), changed["repositories"]![0]!.ToJsonString());
        foreach (var key in new[] { "schemaVersion", "compiledReview", "sqlEvidence", "webFormsReport" })
            Assert.Equal(original[key]!.ToJsonString(), changed[key]!.ToJsonString());
        Assert.Equal(before, File.ReadAllBytes(Assert.Single(Directory.GetFiles(fixture.Workspace, "workspace-before-add-*.local.json"))));
        Assert.Equal(sqlBefore, File.ReadAllBytes(fixture.Manifest)); Assert.Equal(nativeBefore, File.ReadAllBytes(fixture.Prepared));
        Assert.Equal("preserve old scope evidence", File.ReadAllText(oldEvidence));
        Assert.Matches("^[0-9a-f]{64}$", changed["templateProvenance"]!["boundedInputSha256"]!.GetValue<string>());
    }

    [Fact]
    public async Task Existing_scope_filter_is_extended_in_a_new_provenance_bound_copy()
    {
        using var fixture = new SourceFixture();
        var scope = Path.Combine(fixture.Root, "selected.txt"); File.WriteAllText(scope, "# prior scope\nweb\n");
        var config = JsonNode.Parse(File.ReadAllText(fixture.Config))!; config["scopeFile"] = "selected.txt";
        File.WriteAllText(fixture.Config, config.ToJsonString()); var oldScope = File.ReadAllBytes(scope);
        Assert.True(await WorkspaceCommand.RunAsync(fixture.Args, fixture.Output, fixture.Error) == 0, fixture.Error.ToString());
        Assert.Equal(oldScope, File.ReadAllBytes(scope));
        var changed = JsonNode.Parse(File.ReadAllText(fixture.Config))!;
        var newScope = Path.Combine(fixture.Root, changed["scopeFile"]!.GetValue<string>());
        Assert.NotEqual(scope, newScope);
        Assert.Contains("generatorSha256=", File.ReadAllText(newScope));
        Assert.Contains("boundedInputSha256=", File.ReadAllText(newScope));
        Assert.True(await WorkspaceCommand.RunAsync(["verify", "--root", fixture.Root], fixture.Output, fixture.Error) == 0, fixture.Error.ToString());
        Assert.Contains("Selected 2 source repositories", fixture.Output.ToString());
    }

    [Theory]
    [InlineData("duplicate-id")]
    [InlineData("duplicate-root")]
    [InlineData("outside-target")]
    [InlineData("absolute-inside")]
    [InlineData("windows-absolute")]
    [InlineData("windows-drive-relative")]
    [InlineData("unc-target")]
    [InlineData("parent-inside")]
    [InlineData("parent-backslash")]
    [InlineData("parent-escape")]
    [InlineData("missing-target")]
    [InlineData("wrong-kind")]
    [InlineData("non-git")]
    [InlineData("bin-folder")]
    public async Task Invalid_source_additions_preserve_the_original_configuration(string mutation)
    {
        using var fixture = new SourceFixture(); var args = fixture.Args;
        switch (mutation)
        {
            case "duplicate-id": args[Array.IndexOf(args, "--id") + 1] = "web"; break;
            case "duplicate-root": args[Array.IndexOf(args, "--repo") + 1] = fixture.Web; break;
            case "absolute-inside": args[Array.IndexOf(args, "--target") + 1] = Path.Combine(fixture.Framework, "Library.csproj"); break;
            case "windows-absolute": args[Array.IndexOf(args, "--target") + 1] = "C:/other/src/Library.csproj"; break;
            case "windows-drive-relative": args[Array.IndexOf(args, "--target") + 1] = "C:Library.csproj"; break;
            case "unc-target": args[Array.IndexOf(args, "--target") + 1] = @"\\server\share\Library.csproj"; break;
            case "parent-inside": args[Array.IndexOf(args, "--target") + 1] = "child/../Library.csproj"; break;
            case "parent-backslash": args[Array.IndexOf(args, "--target") + 1] = @"child\..\Library.csproj"; break;
            case "parent-escape": args[Array.IndexOf(args, "--target") + 1] = "../../Library.csproj"; break;
            case "outside-target": args[Array.IndexOf(args, "--target") + 1] = "../outside.csproj"; break;
            case "missing-target": args[Array.IndexOf(args, "--target") + 1] = "missing.csproj"; break;
            case "wrong-kind": args[Array.IndexOf(args, "--target") + 1] = "file.dll"; break;
            case "bin-folder":
                var bin = Path.Combine(fixture.Framework, "bin"); Directory.CreateDirectory(bin);
                args[Array.IndexOf(args, "--repo") + 1] = bin; args[Array.IndexOf(args, "--target") + 1] = "discovery"; break;
            case "non-git":
                var nongit = Path.Combine(fixture.Root, "nongit"); Directory.CreateDirectory(nongit);
                args[Array.IndexOf(args, "--repo") + 1] = nongit; args[Array.IndexOf(args, "--target") + 1] = "discovery"; break;
        }
        var before = File.ReadAllBytes(fixture.Config);
        Assert.Equal(1, await WorkspaceCommand.RunAsync(args, fixture.Output, fixture.Error));
        Assert.Equal(before, File.ReadAllBytes(fixture.Config));
        if (mutation is "absolute-inside" or "windows-absolute" or "windows-drive-relative" or "unc-target")
            Assert.Contains("WORKSPACE_SELECTION_RELATIVE_PATH_REQUIRED", fixture.Error.ToString());
        if (mutation is "parent-inside" or "parent-backslash" or "parent-escape")
            Assert.Contains("WORKSPACE_SELECTION_PARENT_SEGMENT_UNSUPPORTED", fixture.Error.ToString());
        Assert.Empty(Directory.GetFiles(fixture.Root, "workspace-before-add-*.local.json"));
    }

    [Fact]
    public async Task Concurrent_configuration_change_during_preview_is_preserved_and_refused()
    {
        using var fixture = new SourceFixture(); var changed = File.ReadAllText(fixture.Config) + "\n";
        using var writer = new MutatingWriter(() => File.WriteAllText(fixture.Config, changed));
        Assert.Equal(1, await WorkspaceCommand.RunAsync(fixture.Args, writer, fixture.Error));
        Assert.Contains("WORKSPACE_ADD_SOURCE_INPUT_CHANGED", fixture.Error.ToString());
        Assert.Equal(changed, File.ReadAllText(fixture.Config));
        Assert.Empty(Directory.GetFiles(fixture.Root, "workspace-before-add-*.local.json"));
    }

    private sealed class MutatingWriter(Action mutation) : StringWriter
    {
        public override Task WriteLineAsync(string? value)
        { if (value?.StartsWith("Source addition:", StringComparison.Ordinal) == true) mutation(); return base.WriteLineAsync(value); }
    }

    private sealed class SourceFixture : IDisposable
    {
        private readonly TempDirectory temp = new();
        public string Root { get; }
        public string Web { get; }
        public string Framework { get; }
        public string Config => Path.Combine(Root, "workspace.local.json");
        public StringWriter Output { get; } = new(); public StringWriter Error { get; } = new();
        public string[] Args => ["add-source", "--root", Root, "--id", "framework", "--repo", Framework, "--target", "Library.csproj"];
        public SourceFixture()
        {
            Root = Path.Combine(temp.Path, "workspace"); Directory.CreateDirectory(Root);
            Web = Path.Combine(temp.Path, "web"); Directory.CreateDirectory(Web); File.WriteAllText(Path.Combine(Web, "Web.cs"), "public class Web {} "); WebFormsWizardNativeTests.InitGit(Web);
            Framework = Path.Combine(temp.Path, "framework"); Directory.CreateDirectory(Framework);
            File.WriteAllText(Path.Combine(Framework, "Library.csproj"), "<Project />"); WebFormsWizardNativeTests.InitGit(Framework);
            File.WriteAllText(Config, JsonSerializer.Serialize(new WorkspaceConfiguration(WorkspaceCommand.SchemaVersion,
                [new("web", Web, [])]), new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        }
        public void Dispose() => temp.Dispose();
    }
}
