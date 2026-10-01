using System.Security.Cryptography;
using System.Text.Json;
using TraceMap.Cli;

namespace TraceMap.Tests;

public sealed class WebFormsConfigMigrationTests
{
    private const string Legacy = """
        {
          // Private local configuration; comment removal must not alter URL-like paths.
          "schemaVersion": "focused-webforms-review-config.v1",
          "sourceRoot": "C:/source/private",
          "webFormsFolder": "--legacy,literal",
          "backendFolder": "Backend",
          "controlsFolder": "Controls",
          "projectSelection": { "mode": "projectless", "solutionRelativePath": "", "projectRelativePaths": [] },
          "outputRoot": "C:/old/review",
          "pageSelection": { "mode": "selected", "forms": ["--legacy,literal/Lookup.aspx"] }
        }
        """;

    [Theory]
    [InlineData("projectless")]
    [InlineData("projects")]
    [InlineData("solution")]
    [InlineData("discover")]
    public async Task Converts_local_jsonc_without_modifying_inputs_or_inventing_compiled_authority(string mode)
    {
        using var fixture = new Fixture();
        var json = Legacy.Replace("\"mode\": \"projectless\"", $"\"mode\": \"{mode}\"");
        if (mode == "projects") json = json.Replace("\"projectRelativePaths\": []", "\"projectRelativePaths\": [\"Backend/Data.vbproj\"]");
        if (mode == "solution") json = json.Replace("\"solutionRelativePath\": \"\"", "\"solutionRelativePath\": \"Application.sln\"");
        File.WriteAllText(fixture.Input, json);
        var original = File.ReadAllBytes(fixture.Input);
        Assert.Equal(0, await fixture.Run());
        Assert.Empty(fixture.Error.ToString());
        Assert.DoesNotContain("C:/", fixture.Output.ToString(), StringComparison.Ordinal);
        Assert.Equal(original, File.ReadAllBytes(fixture.Input));
        var draftBytes = File.ReadAllBytes(Path.Combine(fixture.Out, "review.draft.json"));
        using var draft = JsonDocument.Parse(draftBytes);
        Assert.Equal("C:/source/private", draft.RootElement.GetProperty("sourceRoot").GetString());
        Assert.Equal("--legacy,literal", draft.RootElement.GetProperty("sourceFolders")[0].GetString());
        Assert.Equal("--legacy,literal/Lookup.aspx", draft.RootElement.GetProperty("pageRelativePaths")[0].GetString());
        Assert.Equal(mode == "discover" ? "" : mode, draft.RootElement.GetProperty("projectMode").GetString());
        Assert.Equal("", draft.RootElement.GetProperty("sourceCommitSha").GetString());
        Assert.Equal("", draft.RootElement.GetProperty("publishedRoot").GetString());
        Assert.Equal(0, draft.RootElement.GetProperty("primaryAssemblies").GetArrayLength());
        using var receipt = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(fixture.Out, "migration.local.json")));
        var root = receipt.RootElement;
        Assert.False(root.GetProperty("readyToRun").GetBoolean());
        Assert.Equal(Hash(original), root.GetProperty("boundedInputSha256").GetString());
        Assert.Equal(Hash(draftBytes), root.GetProperty("outputConfigSha256").GetString());
        Assert.Equal(Hash(File.ReadAllBytes(typeof(WebFormsConfigMigrationCommand).Assembly.Location)), root.GetProperty("generatorSha256").GetString());
        Assert.Equal("C:/old/review", root.GetProperty("legacyOutputRoot").GetString());
        var files = Directory.GetFiles(fixture.Out).ToDictionary(path => Path.GetFileName(path)!, File.ReadAllBytes);
        Assert.Equal(1, await fixture.Run());
        Assert.Contains("OUTPUT_EXISTS", fixture.Error.ToString(), StringComparison.Ordinal);
        foreach (var file in files) Assert.Equal(file.Value, File.ReadAllBytes(Path.Combine(fixture.Out, file.Key!)));
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("unknown")]
    [InlineData("trailing-comma")]
    [InlineData("invalid-mode")]
    [InlineData("control-path")]
    [InlineData("oversize")]
    public async Task Invalid_or_ambiguous_inputs_never_publish_a_draft(string scenario)
    {
        using var fixture = new Fixture();
        var input = scenario switch
        {
            "duplicate" => Legacy.Replace("\"sourceRoot\":", "\"sourceRoot\": \"C:/other\", \"sourceRoot\":"),
            "unknown" => Legacy.Replace("\"outputRoot\":", "\"extra\": true, \"outputRoot\":"),
            "trailing-comma" => Legacy.Replace("\"forms\": [\"--legacy,literal/Lookup.aspx\"]", "\"forms\": [\"--legacy,literal/Lookup.aspx\"],"),
            "invalid-mode" => Legacy.Replace("\"projectless\"", "\"automatic\""),
            "control-path" => Legacy.Replace("C:/source/private", "C:/source/\\tprivate"),
            _ => new string(' ', 1_048_577)
        };
        File.WriteAllText(fixture.Input, input);
        Assert.Equal(1, await fixture.Run());
        Assert.False(Directory.Exists(fixture.Out));
        Assert.Empty(fixture.Output.ToString());
        Assert.DoesNotContain("C:/", fixture.Error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Review_root_selection_is_explicit_and_rejects_both_legacy_config_files()
    {
        using var fixture = new Fixture();
        var config = Path.Combine(fixture.Root, "config"); Directory.CreateDirectory(config);
        File.WriteAllText(Path.Combine(config, "webforms-review.jsonc"), Legacy);
        Assert.Equal(0, await TraceMapCommand.RunAsync(["webforms-review", "migrate-config", "--review-root", fixture.Root, "--out", fixture.Out], fixture.Output, fixture.Error));
        File.WriteAllText(Path.Combine(config, "webforms-review.json"), Legacy);
        Assert.Equal(1, await TraceMapCommand.RunAsync(["webforms-review", "migrate-config", "--review-root", fixture.Root, "--out", fixture.Out + "-other"], fixture.Output, fixture.Error));
        Assert.False(Directory.Exists(fixture.Out + "-other"));
    }

    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), $"tracemap-config-migration-{Guid.NewGuid():N}");
        public string Input => Path.Combine(Root, "legacy.jsonc");
        public string Out => Path.Combine(Root, "migrated");
        public StringWriter Output { get; } = new();
        public StringWriter Error { get; } = new();
        public Fixture() { Directory.CreateDirectory(Root); }
        public Task<int> Run() => TraceMapCommand.RunAsync(["webforms-review", "migrate-config", "--config", Input, "--out", Out], Output, Error);
        public void Dispose() { Directory.Delete(Root, true); Output.Dispose(); Error.Dispose(); }
    }
}
