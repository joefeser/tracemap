using TraceMap.Core;
using TraceMap.Storage;

namespace TraceMap.Tests;

public sealed class JsonlFactWriterTests
{
    [Fact]
    public async Task WriteAsync_writes_one_json_object_per_line()
    {
        using var temp = new TempDirectory();
        var manifest = new ScanManifest(
            "scan-test",
            "sample",
            null,
            null,
            "unknown",
            ScannerVersions.TraceMap,
            DateTimeOffset.UnixEpoch,
            "Level1FileInventoryOnly",
            "NotRun",
            [],
            [],
            [],
            []);
        var facts = new[]
        {
            FactFactory.Create(
                manifest,
                FactTypes.RepoScanned,
                RuleIds.RepoManifest,
                EvidenceTiers.Tier2Structural,
                new EvidenceSpan(".", 1, 1, null, "RepoManifestExtractor", ScannerVersions.RepoManifestExtractor)),
            FactFactory.Create(
                manifest,
                FactTypes.BuildStatus,
                RuleIds.RepoManifest,
                EvidenceTiers.Tier4Unknown,
                new EvidenceSpan(".", 1, 1, null, "RepoManifestExtractor", ScannerVersions.RepoManifestExtractor))
        };
        var path = Path.Combine(temp.Path, "facts.ndjson");

        await JsonlFactWriter.WriteAsync(path, facts);

        var lines = await File.ReadAllLinesAsync(path);
        Assert.Equal(2, lines.Length);
        Assert.All(lines, line => Assert.StartsWith("{\"factId\":\"fact-", line));
        Assert.Contains("\"factType\":\"RepoScanned\"", lines[0]);
    }
}
