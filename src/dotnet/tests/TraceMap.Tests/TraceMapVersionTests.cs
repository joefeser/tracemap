using System.Text.Json;
using TraceMap.Cli;

namespace TraceMap.Tests;

public sealed class TraceMapVersionTests
{
    [Fact]
    public void Version_result_is_deterministic_and_uses_closed_readiness_states()
    {
        static bool Available(string capability) => capability is "git" or "dotnet-msbuild";

        var first = TraceMapVersionInfo.Create(Available);
        var second = TraceMapVersionInfo.Create(Available);

        Assert.Equal("tracemap-version.v1", first.SchemaVersion);
        Assert.Contains(first.SourceState, new[] { "clean", "dirty", "unavailable" });
        Assert.True(first.SourceCommit == "unavailable" || first.SourceCommit.Length == 40);
        Assert.Equal("ready", first.Readiness.Outcome);
        Assert.Equal("none", first.Readiness.NextAction);
        Assert.Equal("available", first.Readiness.Git.Status);
        Assert.Equal("available", first.Readiness.MsBuild.Status);
        Assert.Equal(
            JsonSerializer.Serialize(first, TraceMapVersionInfo.JsonOptions),
            JsonSerializer.Serialize(second, TraceMapVersionInfo.JsonOptions));
    }

    [Theory]
    [InlineData(true, false, "reduced", "install-dotnet-sdk-or-use-reduced-analysis")]
    [InlineData(false, true, "unavailable", "install-git")]
    [InlineData(false, false, "unavailable", "install-git")]
    public void Version_result_reports_missing_capabilities_without_raw_diagnostics(
        bool git,
        bool msBuild,
        string outcome,
        string nextAction)
    {
        var result = TraceMapVersionInfo.Create(capability => capability switch
        {
            "git" => git,
            "dotnet-msbuild" => msBuild,
            _ => false
        });

        Assert.Equal(outcome, result.Readiness.Outcome);
        Assert.Equal(nextAction, result.Readiness.NextAction);
    }

    [Fact]
    public async Task Version_json_is_schema_versioned_safe_and_repeatable()
    {
        using var firstOutput = new StringWriter();
        using var secondOutput = new StringWriter();
        using var error = new StringWriter();

        Assert.Equal(0, await TraceMapCommand.RunAsync(["version", "--json"], firstOutput, error));
        Assert.Equal(0, await TraceMapCommand.RunAsync(["version", "--json"], secondOutput, error));

        Assert.Equal(firstOutput.ToString(), secondOutput.ToString());
        using var document = JsonDocument.Parse(firstOutput.ToString());
        Assert.Equal("tracemap-version.v1", document.RootElement.GetProperty("schemaVersion").GetString());
        Assert.Matches("^(unavailable|[0-9a-f]{40})$", document.RootElement.GetProperty("sourceCommit").GetString());
        Assert.Contains(
            document.RootElement.GetProperty("readiness").GetProperty("outcome").GetString(),
            new[] { "ready", "reduced", "unavailable" });
        Assert.DoesNotContain(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), firstOutput.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("PATH", firstOutput.ToString(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public async Task Version_human_output_and_help_are_concise_and_unknown_options_fail()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        Assert.Equal(0, await TraceMapCommand.RunAsync(["version"], output, error));
        Assert.Contains("TraceMap ", output.ToString(), StringComparison.Ordinal);
        Assert.Contains("Readiness: ", output.ToString(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, error.ToString());

        output.GetStringBuilder().Clear();
        Assert.Equal(0, await TraceMapCommand.RunAsync(["version", "--help"], output, error));
        Assert.Contains("tracemap version [--json]", output.ToString(), StringComparison.Ordinal);

        output.GetStringBuilder().Clear();
        Assert.Equal(1, await TraceMapCommand.RunAsync(["version", "--verbose"], output, error));
        Assert.Contains("unsupported version option", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Version_schema_declares_closed_safe_contract()
    {
        var root = FindRepositoryRoot();
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "docs", "contracts", "tracemap-version.v1.schema.json")));

        Assert.Equal("tracemap-version.v1", document.RootElement.GetProperty("properties").GetProperty("schemaVersion").GetProperty("const").GetString());
        Assert.False(document.RootElement.GetProperty("additionalProperties").GetBoolean());
        Assert.False(document.RootElement.GetProperty("properties").GetProperty("host").GetProperty("additionalProperties").GetBoolean());
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "TraceMap.slnx"))
                || File.Exists(Path.Combine(current.FullName, "src", "dotnet", "TraceMap.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Repository root not found.");
    }
}
