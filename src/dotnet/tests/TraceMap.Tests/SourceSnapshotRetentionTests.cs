using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using TraceMap.Cli;
using TraceMap.Core;
using TraceMap.Storage;

namespace TraceMap.Tests;

public sealed class SourceSnapshotRetentionTests
{
    [Fact]
    public async Task Complete_roster_roundtrips_exact_snapshot_and_generator_input_identity_without_source_snippets()
    {
        using var fixture = new Fixture();
        await fixture.Write();
        var header = await fixture.Header();
        Assert.Equal(SourceSnapshotRetention.Schema, header.SchemaVersion);
        Assert.Equal(SourceSnapshotRetention.RuleId, header.RuleId);
        Assert.Equal("local-only", header.Visibility);
        Assert.Equal(fixture.Result.Manifest.SourceSnapshotDigest, header.SourceSnapshotDigest);
        Assert.Equal(Hash(typeof(TraceMapCommand).Assembly.Location), header.GeneratorSha256);
        Assert.Equal(Hash(typeof(ScanEngine).Assembly.Location), header.CoreGeneratorSha256);
        Assert.Equal(SourceSnapshotRetention.InputDigest(header), header.BoundedInputSha256);
        using (var rosterHeader = JsonDocument.Parse(File.ReadLines(Path.Combine(fixture.Output, SourceSnapshotRetention.RosterName)).First()))
        {
            Assert.Equal(header.GeneratorSha256, rosterHeader.RootElement.GetProperty("generatorSha256").GetString());
            Assert.Equal(header.SourceSnapshotDigest, rosterHeader.RootElement.GetProperty("boundedInputSha256").GetString());
        }
        var observed = SourceSnapshotInspector.InspectOrderedInventory(fixture.Source,
            SourceSnapshotRetention.ReadRoster(fixture.Input(SourceSnapshotRetention.RosterName), default, header), 10, 4096);
        Assert.Equal(header.SourceSnapshotDigest, observed.Digest);
        Assert.Equal(header.FileCount, observed.FileCount);
        Assert.Equal(header.SourceBytes, observed.Bytes);
        Assert.DoesNotContain("public sealed class", File.ReadAllText(Path.Combine(fixture.Output, SourceSnapshotRetention.RosterName)), StringComparison.Ordinal);
        Assert.DoesNotContain("sourceSnapshotInventory", JsonSerializer.Serialize(fixture.Result, JsonOptions.Stable), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("files", "SOURCE_SNAPSHOT_RETENTION_SOURCE_INPUT_LIMIT")]
    [InlineData("source-bytes", "SOURCE_SNAPSHOT_RETENTION_SOURCE_INPUT_LIMIT")]
    [InlineData("roster-bytes", "SOURCE_SNAPSHOT_RETENTION_ROSTER_BYTES_LIMIT")]
    [InlineData("missing-roster", "SOURCE_SNAPSHOT_RETENTION_AUTHORITATIVE_ROSTER_UNAVAILABLE")]
    [InlineData("changed-source", "SOURCE_SNAPSHOT_RETENTION_SOURCE_CHANGED")]
    public async Task Writer_requires_authoritative_membership_and_admits_explicit_file_source_and_roster_budgets(string kind, string code)
    {
        using var fixture = new Fixture();
        if (kind == "changed-source")
            File.WriteAllText(Path.Combine(fixture.Source, "One.cs"), "public sealed class Two { }");
        var result = kind == "missing-roster" ? fixture.Result with { SourceSnapshotInventory = null } : fixture.Result;
        var error = await Assert.ThrowsAnyAsync<InvalidOperationException>(() => SourceSnapshotRetention.WriteAsync(
            fixture.Output, fixture.Source, result, kind == "files" ? 1 : 10,
            kind == "source-bytes" ? 1 : 4096, kind == "roster-bytes" ? 1 : 4096, default));
        Assert.Equal(code, error.Message);
        Assert.False(File.Exists(Path.Combine(fixture.Output, SourceSnapshotRetention.ManifestName)));
    }

    [Theory]
    [InlineData("scan", "SOURCE_SNAPSHOT_RETENTION_MANIFEST_MISMATCH")]
    [InlineData("roster", "SOURCE_SNAPSHOT_RETENTION_MANIFEST_MISMATCH")]
    [InlineData("generator", "SOURCE_SNAPSHOT_RETENTION_MANIFEST_MISMATCH")]
    [InlineData("input", "SOURCE_SNAPSHOT_RETENTION_MANIFEST_MISMATCH")]
    [InlineData("header-bytes", "SOURCE_SNAPSHOT_RETENTION_MANIFEST_CHANGED")]
    public async Task Retained_header_requires_exact_parent_roster_generator_and_bounded_input_identity(string mutation, string code)
    {
        using var fixture = new Fixture();
        await fixture.Write();
        var originalInput = fixture.Input(SourceSnapshotRetention.ManifestName);
        var header = await fixture.Header();
        header = mutation switch
        {
            "scan" => header with { ScanManifestSha256 = new string('a', 64) },
            "roster" => header with { RosterSha256 = new string('a', 64) },
            "generator" => header with { GeneratorSha256 = "not-a-digest" },
            _ => header with { BoundedInputSha256 = new string('a', 64) }
        };
        File.WriteAllText(Path.Combine(fixture.Output, SourceSnapshotRetention.ManifestName), JsonSerializer.Serialize(header, JsonOptions.Stable));
        var error = await Assert.ThrowsAnyAsync<InvalidOperationException>(() => SourceSnapshotRetention.ReadManifestAsync(
            mutation == "header-bytes" ? originalInput : fixture.Input(SourceSnapshotRetention.ManifestName),
            fixture.Input(SourceSnapshotRetention.RosterName), fixture.Input("scan-manifest.json"), fixture.Result.Manifest, default));
        Assert.Equal(code, error.Message);
    }

    [Theory]
    [InlineData("empty-line", "SOURCE_SNAPSHOT_RETENTION_ROSTER_EMPTY_LINE")]
    [InlineData("long-line", "SOURCE_SNAPSHOT_RETENTION_ROSTER_LINE_LIMIT")]
    [InlineData("unterminated", "SOURCE_SNAPSHOT_RETENTION_ROSTER_UNTERMINATED_LINE")]
    public void Roster_reader_has_a_hard_line_bound_and_never_loads_the_whole_artifact(string kind, string code)
    {
        using var fixture = new Fixture();
        File.WriteAllText(Path.Combine(fixture.Output, SourceSnapshotRetention.RosterName), kind switch
        { "empty-line" => "\n", "long-line" => new string('x', 32769) + "\n", _ => "{}" });
        var error = Assert.ThrowsAny<InvalidOperationException>(() =>
            SourceSnapshotRetention.ReadRoster(fixture.Input(SourceSnapshotRetention.RosterName), default).ToArray());
        Assert.Equal(code, error.Message);
    }

    [Fact]
    public async Task Ordinary_scan_keeps_default_outputs_and_opt_in_retention_preserves_fact_and_scan_identity()
    {
        using var fixture = new Fixture();
        var ordinary = Path.Combine(fixture.Root, "ordinary");
        var retained = Path.Combine(fixture.Root, "retained");
        using var output = new StringWriter();
        using var error = new StringWriter();
        Assert.Equal(0, await TraceMapCommand.RunAsync(["scan", "--repo", fixture.Source, "--out", ordinary], output, error));
        Assert.False(File.Exists(Path.Combine(ordinary, SourceSnapshotRetention.ManifestName)));
        Assert.False(File.Exists(Path.Combine(ordinary, SourceSnapshotRetention.RosterName)));
        Assert.Equal(0, await TraceMapCommand.RunAsync(["scan", "--repo", fixture.Source, "--out", retained,
            "--retain-source-snapshot"], output, error));
        Assert.Equal(File.ReadAllText(Path.Combine(ordinary, "facts.ndjson")), File.ReadAllText(Path.Combine(retained, "facts.ndjson")));
        var first = JsonSerializer.Deserialize<ScanManifest>(File.ReadAllText(Path.Combine(ordinary, "scan-manifest.json")), JsonOptions.Stable)!;
        var second = JsonSerializer.Deserialize<ScanManifest>(File.ReadAllText(Path.Combine(retained, "scan-manifest.json")), JsonOptions.Stable)!;
        Assert.Equal(first.ScanId, second.ScanId);
        Assert.Equal(first.SourceSnapshotDigest, second.SourceSnapshotDigest);
        Assert.True(File.Exists(Path.Combine(retained, SourceSnapshotRetention.ManifestName)));
        Assert.True(File.Exists(Path.Combine(retained, SourceSnapshotRetention.RosterName)));
        ScanOutputTransaction.ValidateTarget(retained, fixture.Source);
    }

    [Fact]
    public async Task Cancellation_and_no_overwrite_keep_prior_roster_bytes()
    {
        using var fixture = new Fixture();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            SourceSnapshotRetention.WriteAsync(fixture.Output, fixture.Source, fixture.Result, 10, 4096, 4096, new CancellationToken(true)));
        await fixture.Write();
        var before = fixture.Input(SourceSnapshotRetention.RosterName).Sha256;
        await Assert.ThrowsAsync<IOException>(() => fixture.Write());
        Assert.Equal(before, fixture.Input(SourceSnapshotRetention.RosterName).Sha256);
    }

    [Theory]
    [InlineData("--source-snapshot-max-files", "100000001", "SOURCE_SNAPSHOT_RETENTION_LIMIT_INVALID")]
    [InlineData("--source-snapshot-max-roster-bytes", "1", "SOURCE_SNAPSHOT_RETENTION_ROSTER_BYTES_LIMIT")]
    public async Task CLI_reports_categorical_retention_limits_without_publishing_a_completed_scan(string option, string value, string code)
    {
        using var fixture = new Fixture();
        var outputRoot = Path.Combine(fixture.Root, "bounded");
        using var output = new StringWriter(); using var error = new StringWriter();
        Assert.Equal(1, await TraceMapCommand.RunAsync(["scan", "--repo", fixture.Source, "--out", outputRoot,
            "--retain-source-snapshot", option, value], output, error));
        Assert.Contains(code, error.ToString(), StringComparison.Ordinal);
        Assert.False(ScanOutputTransaction.HasCompleteOutput(outputRoot));
        Assert.False(File.Exists(Path.Combine(outputRoot, SourceSnapshotRetention.ManifestName)));
        if (option == "--source-snapshot-max-files") Assert.False(Directory.Exists(outputRoot));
    }

    private static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private sealed class Fixture : IDisposable
    {
        private readonly TempDirectory temp = new();
        public string Root => temp.Path;
        public string Source => Path.Combine(Root, "source");
        public string Output => Path.Combine(Root, "output");
        public ScanResult Result { get; }
        public Fixture()
        {
            Directory.CreateDirectory(Source);
            Directory.CreateDirectory(Output);
            File.WriteAllText(Path.Combine(Source, "One.cs"), "public sealed class One { }");
            File.WriteAllText(Path.Combine(Source, "Two.vb"), "Public Class Two\nEnd Class\n");
            Git("init", "-q"); Git("config", "user.name", "Public test");
            Git("config", "user.email", "public-test@example.invalid");
            Git("add", "."); Git("commit", "-qm", "public source roster");
            Result = ScanEngine.Scan(new(Source, Output));
            ManifestWriter.WriteAsync(Path.Combine(Output, "scan-manifest.json"), Result.Manifest).GetAwaiter().GetResult();
        }
        public Task Write() => SourceSnapshotRetention.WriteAsync(Output, Source, Result, 10, 4096, 4096, default);
        public Task<SourceSnapshotRetentionManifest> Header() => SourceSnapshotRetention.ReadManifestAsync(
            Input(SourceSnapshotRetention.ManifestName), Input(SourceSnapshotRetention.RosterName), Input("scan-manifest.json"), Result.Manifest, default);
        public WebFormsReviewInput Input(string name)
        {
            var path = Path.Combine(Output, name);
            return new(name, path, new FileInfo(path).Length, Hash(path));
        }
        private void Git(params string[] args)
        {
            using var process = new Process { StartInfo = new("git") { WorkingDirectory = Source,
                RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false } };
            foreach (var arg in args) process.StartInfo.ArgumentList.Add(arg);
            Assert.True(process.Start());
            var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
            Assert.True(process.WaitForExit(10000)); Task.WaitAll(stdout, stderr); Assert.Equal(0, process.ExitCode);
        }
        public void Dispose() => temp.Dispose();
    }
}
