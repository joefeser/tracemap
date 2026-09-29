using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TraceMap.Core;

namespace TraceMap.Cli;

public sealed record SourceSnapshotRetentionManifest(
    string SchemaVersion, string RuleId, string EvidenceTier, string Visibility, string ClaimLevel,
    string GeneratorSha256, string CoreGeneratorSha256, string BoundedInputSha256,
    string ScanId, string CommitSha, string? ScanRootPathHash, string ScanManifestSha256,
    string SourceSnapshotDigest, string RosterSha256, long RosterBytes,
    long FileCount, long SourceBytes, long MaxFiles, long MaxSourceBytes, long MaxRosterBytes);

internal sealed class SourceSnapshotRetentionException(string code)
    : InvalidOperationException("SOURCE_SNAPSHOT_RETENTION_" + code);

internal sealed record SourceSnapshotRosterHeader(string SchemaVersion, string RuleId, string Visibility,
    string GeneratorSha256, string BoundedInputSha256, string InputFormat);

/// <summary>Explicit local retention of the scanner's complete byte-snapshot roster.</summary>
internal static class SourceSnapshotRetention
{
    internal const string ManifestName = "source-snapshot-manifest.local.json";
    internal const string RosterName = "source-snapshot.local.ndjson";
    internal const string Schema = "source-snapshot-retention.v1";
    internal const string RuleId = "workflow.source-snapshot-retention.v1";
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 8
    };

    internal static async Task WriteAsync(string root, string sourceRoot, ScanResult result,
        long maxFiles, long maxSourceBytes, long maxRosterBytes, CancellationToken token)
    {
        ValidateLimits(maxFiles, maxSourceBytes, maxRosterBytes);
        var roster = result.SourceSnapshotInventory ?? throw Fail("AUTHORITATIVE_ROSTER_UNAVAILABLE");
        var inspection = Inspect();
        if (inspection.Digest != result.Manifest.SourceSnapshotDigest) throw Fail("SOURCE_CHANGED");
        var generator = await HashAsync(typeof(SourceSnapshotRetention).Assembly.Location, token);
        var coreGenerator = await HashAsync(typeof(ScanEngine).Assembly.Location, token);
        var scanManifestSha = await HashAsync(Path.Combine(root, "scan-manifest.json"), token);
        var rosterPath = Path.Combine(root, RosterName);
        long rosterBytes = 0;
        await using (var stream = new FileStream(rosterPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            65536, FileOptions.Asynchronous | FileOptions.SequentialScan))
        {
            var header = JsonSerializer.SerializeToUtf8Bytes(new SourceSnapshotRosterHeader(
                "source-snapshot-roster.v1", RuleId, "local-only", generator, inspection.Digest,
                "tracemap-framed-source-snapshot.v1"), Json);
            if (header.Length + 1 > maxRosterBytes) throw Fail("ROSTER_BYTES_LIMIT");
            await stream.WriteAsync(header, token);
            await stream.WriteAsync("\n"u8.ToArray(), token);
            rosterBytes += header.Length + 1;
            foreach (var item in roster)
            {
                token.ThrowIfCancellationRequested();
                var line = JsonSerializer.SerializeToUtf8Bytes(item, Json);
                if (line.Length + 1 > maxRosterBytes - rosterBytes) throw Fail("ROSTER_BYTES_LIMIT");
                await stream.WriteAsync(line, token);
                await stream.WriteAsync("\n"u8.ToArray(), token);
                rosterBytes += line.Length + 1;
            }
        }
        var rosterSha = await HashAsync(rosterPath, token);
        var manifest = new SourceSnapshotRetentionManifest(Schema, RuleId, EvidenceTiers.Tier2Structural,
            "local-only", "retained-source-snapshot-not-runtime", generator, coreGenerator, "",
            result.Manifest.ScanId, result.Manifest.CommitSha, result.Manifest.ScanRootPathHash, scanManifestSha,
            inspection.Digest, rosterSha, rosterBytes, inspection.FileCount, inspection.Bytes,
            maxFiles, maxSourceBytes, maxRosterBytes);
        manifest = manifest with { BoundedInputSha256 = InputDigest(manifest) };
        var after = Inspect();
        if (after != inspection || scanManifestSha != await HashAsync(Path.Combine(root, "scan-manifest.json"), token)
            || generator != await HashAsync(typeof(SourceSnapshotRetention).Assembly.Location, token)
            || coreGenerator != await HashAsync(typeof(ScanEngine).Assembly.Location, token)) throw Fail("INPUT_CHANGED");
        await using var output = new FileStream(Path.Combine(root, ManifestName), FileMode.CreateNew,
            FileAccess.Write, FileShare.None, 65536, FileOptions.Asynchronous);
        await JsonSerializer.SerializeAsync(output, manifest, Json, token);
        await output.WriteAsync("\n"u8.ToArray(), token);

        SourceSnapshotInspection Inspect()
        {
            try { return SourceSnapshotInspector.InspectOrderedInventory(sourceRoot, roster, maxFiles, maxSourceBytes, token); }
            catch (SourceSnapshotException) { throw Fail("SOURCE_CHANGED"); }
            catch (SourceInventoryException) { throw Fail("SOURCE_UNAVAILABLE"); }
            catch (InvalidOperationException exception) when (exception.Message.StartsWith("SourceSnapshot", StringComparison.Ordinal))
            { throw Fail(exception.Message == "SourceSnapshotInputLimit" ? "SOURCE_INPUT_LIMIT" : "SOURCE_ROSTER_INVALID"); }
        }
    }

    internal static void ValidateLimits(long maxFiles, long maxSourceBytes, long maxRosterBytes)
    {
        if (maxFiles is < 1 or > 100_000_000 || maxSourceBytes is < 1 or > 1_099_511_627_776
            || maxRosterBytes is < 1 or > 1_099_511_627_776) throw Fail("LIMIT_INVALID");
    }

    internal static async Task<SourceSnapshotRetentionManifest> ReadManifestAsync(
        WebFormsReviewInput manifestInput, WebFormsReviewInput rosterInput, WebFormsReviewInput scanManifestInput,
        ScanManifest parent, CancellationToken token)
    {
        var bytes = await WebFormsReviewPreflightCommand.ReadSmallAsync(manifestInput.Path, 1_048_576, token);
        if (Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant() != manifestInput.Sha256) throw Fail("MANIFEST_CHANGED");
        WebFormsReviewPreflightCommand.RejectDuplicateProperties(bytes);
        var retained = JsonSerializer.Deserialize<SourceSnapshotRetentionManifest>(bytes, Json) ?? throw Fail("MANIFEST_INVALID");
        if (retained.SchemaVersion != Schema || retained.RuleId != RuleId
            || retained.EvidenceTier != EvidenceTiers.Tier2Structural || retained.Visibility != "local-only"
            || retained.ClaimLevel != "retained-source-snapshot-not-runtime"
            || retained.ScanId != parent.ScanId || retained.CommitSha != parent.CommitSha
            || retained.ScanRootPathHash != parent.ScanRootPathHash
            || retained.ScanManifestSha256 != scanManifestInput.Sha256
            || retained.SourceSnapshotDigest != parent.SourceSnapshotDigest
            || retained.RosterSha256 != rosterInput.Sha256 || retained.RosterBytes != rosterInput.Bytes
            || retained.FileCount is < 1 || retained.FileCount > retained.MaxFiles
            || retained.SourceBytes < 0 || retained.SourceBytes > retained.MaxSourceBytes
            || retained.RosterBytes is < 1 || retained.RosterBytes > retained.MaxRosterBytes
            || retained.MaxFiles is < 1 or > 100_000_000
            || retained.MaxSourceBytes is < 1 or > 1_099_511_627_776
            || retained.MaxRosterBytes is < 1 or > 1_099_511_627_776
            || !Digest(retained.GeneratorSha256) || !Digest(retained.CoreGeneratorSha256)
            || !Digest(retained.BoundedInputSha256) || retained.BoundedInputSha256 != InputDigest(retained))
            throw Fail("MANIFEST_MISMATCH");
        return retained;
    }

    internal static IEnumerable<FileInventoryItem> ReadRoster(WebFormsReviewInput input, CancellationToken token,
        SourceSnapshotRetentionManifest? expected = null)
    {
        using var stream = new FileStream(input.Path, FileMode.Open, FileAccess.Read, FileShare.Read,
            65536, FileOptions.SequentialScan);
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true), false, 65536);
        var buffer = new char[16384];
        var line = new StringBuilder();
        var headerRead = false;
        int count;
        while ((count = reader.Read(buffer, 0, buffer.Length)) > 0)
        {
            token.ThrowIfCancellationRequested();
            if (stream.Length != input.Bytes) throw Fail("ROSTER_CHANGED");
            for (var offset = 0; offset < count; offset++)
            {
                if (buffer[offset] == '\n')
                {
                    if (line.Length == 0) throw Fail("ROSTER_EMPTY_LINE");
                    if (!headerRead)
                    {
                        var raw = Encoding.UTF8.GetBytes(line.ToString());
                        WebFormsReviewPreflightCommand.RejectDuplicateProperties(raw);
                        var header = JsonSerializer.Deserialize<SourceSnapshotRosterHeader>(raw, Json) ?? throw Fail("ROSTER_HEADER_INVALID");
                        if (header.SchemaVersion != "source-snapshot-roster.v1" || header.RuleId != RuleId
                            || header.Visibility != "local-only" || header.InputFormat != "tracemap-framed-source-snapshot.v1"
                            || !Digest(header.GeneratorSha256) || !Digest(header.BoundedInputSha256)
                            || expected is not null && (header.GeneratorSha256 != expected.GeneratorSha256
                                || header.BoundedInputSha256 != expected.SourceSnapshotDigest)) throw Fail("ROSTER_HEADER_INVALID");
                        headerRead = true;
                    }
                    else yield return Parse(line.ToString());
                    line.Clear();
                }
                else
                {
                    if (line.Length >= 32768) throw Fail("ROSTER_LINE_LIMIT");
                    line.Append(buffer[offset]);
                }
            }
        }
        if (line.Length != 0) throw Fail("ROSTER_UNTERMINATED_LINE");
        if (!headerRead) throw Fail("ROSTER_HEADER_UNAVAILABLE");
        if (stream.Length != input.Bytes) throw Fail("ROSTER_CHANGED");

        static FileInventoryItem Parse(string text)
        {
            WebFormsReviewPreflightCommand.RejectDuplicateProperties(Encoding.UTF8.GetBytes(text));
            return JsonSerializer.Deserialize<FileInventoryItem>(text, Json) ?? throw Fail("ROSTER_INVALID");
        }
    }

    internal static string InputDigest(SourceSnapshotRetentionManifest value) => Convert.ToHexString(SHA256.HashData(
        JsonSerializer.SerializeToUtf8Bytes(new
        {
            value.GeneratorSha256, value.ScanId, value.CommitSha, value.ScanRootPathHash, value.ScanManifestSha256,
            value.SourceSnapshotDigest, value.RosterSha256, value.RosterBytes, value.FileCount, value.SourceBytes,
            value.MaxFiles, value.MaxSourceBytes, value.MaxRosterBytes, value.CoreGeneratorSha256
        }, Json))).ToLowerInvariant();

    private static bool Digest(string? value) => value?.Length == 64 && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static async Task<string> HashAsync(string path, CancellationToken token)
    {
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, token)).ToLowerInvariant();
    }
    private static SourceSnapshotRetentionException Fail(string code) => new(code);
}
