using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;

namespace TraceMap.Cli;

internal sealed record WebFormsEvidenceQuery(string Document, string Pointer, int Offset = 0, int Limit = 16, int Depth = 1);
public sealed record WebFormsEvidenceItem(string Pointer, string Kind, JsonElement? Value,
    int ChildCount, int Offset, int ReturnedChildren, int OmittedChildren, IReadOnlyList<WebFormsEvidenceItem> Children)
{
    public int? NextOffset { get; init; }
}
public sealed record WebFormsEvidenceResponse(string SchemaVersion, string RuleId, string EvidenceTier,
    string Visibility, string ClaimLevel, string GeneratorSha256, string BoundedInputSha256,
    string RunId, string PreflightSha256, string CheckpointSha256, string IndexSha256,
    string IndexGeneratorSha256, string IndexBoundedInputSha256, string Document, string Pointer,
    int Offset, int Limit, int Depth, int MaxResponseNodes, int MaxResponseBytes,
    bool Truncated, WebFormsEvidenceItem Result, IReadOnlyList<string> Limitations);

public static partial class WebFormsReviewExecutionCommand
{
    internal const int QueryMaxNodes = 2048;
    internal const int QueryMaxBytes = 131_072;
    private static readonly JsonSerializerOptions QueryJson = new()
    { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true, MaxDepth = 64,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };

    /// <summary>Only consumes completed owned run artifacts. No source, publish or runtime execution.</summary>
    public static async Task<int> QueryAsync(string[] args, TextWriter output, TextWriter error, CancellationToken token = default)
    {
        try
        {
            if (args.Length < 3 || args[0] != "query" || args[1] != "--run") throw WebFormsReviewEvidenceIndex.Invalid("QUERY_ARGUMENT_INVALID");
            var root = WebFormsReviewPreflightCommand.PhysicalPath(args[2]);
            var query = ParseQuery(args[3..]);
            using var runLock = AcquireQueryRunLock(root);
            var planBytes = await WebFormsReviewPreflightCommand.ReadSmallAsync(OwnedPath(root, "run-manifest.json"), 4_194_304, token);
            WebFormsReviewPreflightCommand.RejectDuplicateProperties(planBytes);
            var plan = JsonSerializer.Deserialize<WebFormsReviewPreflightManifest>(planBytes, JsonOptions) ?? throw Fail("PREFLIGHT_INVALID");
            if (!Guid.TryParseExact(plan.RunId, "N", out _)) throw Fail("PREFLIGHT_INVALID");
            var firstBytes = await WebFormsReviewPreflightCommand.ReadSmallAsync(OwnedPath(root, "checkpoints/0001.json"), 4_194_304, token);
            WebFormsReviewPreflightCommand.RejectDuplicateProperties(firstBytes);
            var first = JsonSerializer.Deserialize<WebFormsReviewCheckpoint>(firstBytes, JsonOptions) ?? throw Fail("CHECKPOINT_INVALID");
            var preflight = Digest(planBytes);
            var history = await ReadHistoryAsync(root, plan, preflight, first.RuntimeInputsSha256, token);
            var complete = history.Checkpoint;
            if (complete?.State != WebFormsReviewReportExecution.Completed || complete.Reports is null) throw WebFormsReviewEvidenceIndex.Invalid("QUERY_COMPLETED_REPORT_REQUIRED");
            var prefix = complete.Reports.ReportAttempt + "/";
            var admitted = complete.Artifacts.SingleOrDefault(item => item.RelativePath == prefix + WebFormsReviewEvidenceIndex.Name)
                ?? throw WebFormsReviewEvidenceIndex.Invalid("QUERY_INDEX_UNAVAILABLE");
            var application = complete.Artifacts.Single(item => item.RelativePath == prefix + WebFormsReviewReportExecution.HandoffName);
            var compiled = complete.Artifacts.Single(item => item.RelativePath == prefix + "compiled/compiled-paths.handoff.local.json");
            var indexPath = OwnedPath(root, admitted.RelativePath);
            RejectQuerySidecars(indexPath);
            if (!File.Exists(indexPath) || new FileInfo(indexPath).Length != admitted.Bytes) throw WebFormsReviewEvidenceIndex.Invalid("QUERY_INDEX_CHANGED");
            var actual = await WebFormsReviewPreflightCommand.HashAsync("query-index", indexPath, admitted.Bytes, token);
            if (actual.Bytes != admitted.Bytes || actual.Sha256 != admitted.Sha256) throw WebFormsReviewEvidenceIndex.Invalid("QUERY_INDEX_CHANGED");
            WebFormsReviewEvidenceIndex.Context context;
            WebFormsEvidenceItem result;
            bool truncated;
            using (var connection = new SqliteConnection($"Data Source={new Uri(indexPath).AbsoluteUri}?immutable=1;Mode=ReadOnly;Pooling=False"))
            {
                await connection.OpenAsync(token);
                context = WebFormsReviewEvidenceIndex.ReadContext(connection, plan.RunId, application.Sha256, compiled.Sha256);
                if (context.MaxIndexBytes < admitted.Bytes) throw WebFormsReviewEvidenceIndex.Invalid("QUERY_INDEX_LIMIT");
                (result, truncated) = ReadQuery(connection, query, token);
            }
            var generator = await WebFormsReviewPreflightCommand.HashAsync("query-generator", typeof(WebFormsReviewExecutionCommand).Assembly.Location, 67_108_864, token);
            var response = new WebFormsEvidenceResponse("webforms-review-evidence-slice.v1", WebFormsReviewEvidenceIndex.Rule,
                "Tier2Structural", "local-only", "review-only-static-not-runtime", generator.Sha256, "", plan.RunId,
                preflight, history.Sha256!, admitted.Sha256, context.GeneratorSha256, context.BoundedInputSha256,
                query.Document, query.Pointer, query.Offset, query.Limit, query.Depth, QueryMaxNodes, QueryMaxBytes, truncated, result,
                ["Indexed values are exact retained JSON tokens; containers expose child counts and omitted children, not invented empty evidence.",
                 "Truncation describes this retrieval slice, independently of packet coverage, graph traversal and path-enumeration limits.",
                 "Only the selected checkpointed evidence index is consumed. External source, DLLs and other artifacts were not revalidated; this is not fresh-source or runtime proof.",
                 "Generator/index/run hashes are private local integrity commitments, not authenticated build provenance. No LLM calls, source access or writes occurred.",
                 "No privacy projection is provided. Use ordinal page/chain aliases in outward answers; do not publish private identities, values or input fingerprints."]);
            var boundedInput = WebFormsReviewReportExecution.CanonicalHash(new
            {
                schema = response.SchemaVersion, rule = response.RuleId, generatorSha256 = generator.Sha256,
                preflightSha256 = preflight, checkpointSha256 = history.Sha256, indexSha256 = admitted.Sha256,
                indexContextSha256 = context.BoundedInputSha256, query, maxNodes = QueryMaxNodes, maxBytes = QueryMaxBytes,
                resultSha256 = WebFormsReviewReportExecution.CanonicalHash(result, QueryMaxBytes, token)
            }, QueryMaxBytes, token);
            response = response with { BoundedInputSha256 = boundedInput };
            using var bytes = new MemoryStream();
            await using (var bounded = new QueryOutputStream(bytes, token)) await JsonSerializer.SerializeAsync(bounded, response, QueryJson, token);
            if (actual != await WebFormsReviewPreflightCommand.HashAsync("query-index", indexPath, admitted.Bytes, token)) throw WebFormsReviewEvidenceIndex.Invalid("QUERY_INDEX_CHANGED");
            RejectQuerySidecars(indexPath);
            var rechecked = await ReadHistoryAsync(root, plan, preflight, first.RuntimeInputsSha256, token);
            if (history.Sha256 != rechecked.Sha256 || preflight != Digest(await WebFormsReviewPreflightCommand.ReadSmallAsync(OwnedPath(root, "run-manifest.json"), 4_194_304, token)))
                throw WebFormsReviewEvidenceIndex.Invalid("QUERY_RUN_CHANGED");
            token.ThrowIfCancellationRequested();
            await output.WriteAsync(Encoding.UTF8.GetString(bytes.GetBuffer(), 0, checked((int)bytes.Length)));
            return 0;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (ExecutionException exception) { await error.WriteLineAsync("error: " + exception.Message); return 1; }
        catch (InvalidDataException exception) when (exception.Message.StartsWith("WEBFORMS_EVIDENCE_", StringComparison.Ordinal))
        { await error.WriteLineAsync("error: " + exception.Message); return 1; }
        catch (InvalidDataException exception) when (exception.Message == "WEBFORMS_NATIVE_REPORT_OUTPUT_LIMIT")
        { await error.WriteLineAsync("error: WEBFORMS_EVIDENCE_QUERY_RESPONSE_LIMIT"); return 1; }
        catch (Exception) { await error.WriteLineAsync("error: WEBFORMS_EVIDENCE_QUERY_INPUT_OR_INDEX_INVALID"); return 1; }
    }

    private static FileStream AcquireQueryRunLock(string root)
    {
        var path = OwnedPath(root, ".native-run.lock");
        try { return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read); }
        catch (IOException exception) when (exception is not FileNotFoundException and not DirectoryNotFoundException)
        { throw WebFormsReviewEvidenceIndex.Invalid("QUERY_RUN_BUSY_OR_LOCK_UNAVAILABLE"); }
    }

    internal static WebFormsEvidenceQuery ParseQuery(string[] args)
    {
        var options = new Dictionary<string, string>(StringComparer.Ordinal);
        if (args.Length % 2 != 0) throw WebFormsReviewEvidenceIndex.Invalid("QUERY_ARGUMENT_INVALID");
        for (var index = 0; index < args.Length; index += 2)
            if (args[index] is not ("--document" or "--pointer" or "--offset" or "--limit" or "--depth") || !options.TryAdd(args[index], args[index + 1]))
                throw WebFormsReviewEvidenceIndex.Invalid("QUERY_ARGUMENT_INVALID");
        int Number(string key, int defaultValue, int maximum) => !options.TryGetValue(key, out var value) ? defaultValue :
            int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number <= maximum ? number : throw WebFormsReviewEvidenceIndex.Invalid("QUERY_ARGUMENT_INVALID");
        var query = new WebFormsEvidenceQuery(options.GetValueOrDefault("--document", "application"), options.GetValueOrDefault("--pointer", ""),
            Number("--offset", 0, WebFormsReviewEvidenceIndex.MaxNodes), Number("--limit", 16, 50), Number("--depth", 1, 8));
        if (query.Document is not ("application" or "compiled") || query.Limit == 0) throw WebFormsReviewEvidenceIndex.Invalid("QUERY_ARGUMENT_INVALID");
        _ = Segments(query.Pointer);
        return query;
    }
    private static string[] Segments(string pointer)
    {
        if (pointer == "") return [];
        if (pointer.Length > 4096 || !pointer.StartsWith('/') || pointer.Any(character => character < ' ')) throw WebFormsReviewEvidenceIndex.Invalid("QUERY_POINTER_INVALID");
        var parts = pointer[1..].Split('/');
        if (parts.Length > 64) throw WebFormsReviewEvidenceIndex.Invalid("QUERY_POINTER_INVALID");
        foreach (var part in parts)
            for (var index = 0; index < part.Length; index++)
                if (part[index] == '~' && (++index >= part.Length || part[index] is not ('0' or '1'))) throw WebFormsReviewEvidenceIndex.Invalid("QUERY_POINTER_INVALID");
        return parts.Select(part => part.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal)).ToArray();
    }
    private sealed record QueryNode(long Id, int Ordinal, string? Name, string Kind, byte[]? Scalar, int Count);
    internal static (WebFormsEvidenceItem Result, bool Truncated) ReadQuery(SqliteConnection connection, WebFormsEvidenceQuery query, CancellationToken token)
    {
        using var root = connection.CreateCommand(); root.CommandText = "SELECT id,ordinal,name,kind,scalar_json,child_count,length(scalar_json) FROM json_nodes WHERE document=$doc AND parent_id IS NULL";
        root.Parameters.AddWithValue("$doc", query.Document);
        QueryNode selected;
        using (var reader = root.ExecuteReader()) { if (!reader.Read()) throw WebFormsReviewEvidenceIndex.Invalid("QUERY_DOCUMENT_UNAVAILABLE"); selected = Node(reader); }
        foreach (var segment in Segments(query.Pointer))
        {
            token.ThrowIfCancellationRequested();
            using var child = connection.CreateCommand();
            child.CommandText = "SELECT id,ordinal,name,kind,scalar_json,child_count,length(scalar_json) FROM json_nodes WHERE document=$doc AND parent_id=$parent AND " +
                (selected.Kind == "object" ? "name=$value" : selected.Kind == "array" ? "ordinal=$value" : throw WebFormsReviewEvidenceIndex.Invalid("QUERY_POINTER_NOT_CONTAINER"));
            child.Parameters.AddWithValue("$doc", query.Document); child.Parameters.AddWithValue("$parent", selected.Id);
            if (selected.Kind == "array")
            {
                if (segment.Length == 0 || segment.Length > 1 && segment[0] == '0' || !int.TryParse(segment, NumberStyles.None, CultureInfo.InvariantCulture, out var ordinal)) throw WebFormsReviewEvidenceIndex.Invalid("QUERY_ARRAY_INDEX_INVALID");
                child.Parameters.AddWithValue("$value", ordinal);
            }
            else child.Parameters.AddWithValue("$value", segment);
            using var reader = child.ExecuteReader();
            if (!reader.Read()) throw WebFormsReviewEvidenceIndex.Invalid("QUERY_POINTER_UNAVAILABLE");
            selected = Node(reader);
        }
        if (selected.Kind is not ("object" or "array") && query.Offset != 0) throw WebFormsReviewEvidenceIndex.Invalid("QUERY_OFFSET_NOT_CONTAINER");
        var count = 0; var retainedBytes = 0L; var truncated = false;
        WebFormsEvidenceItem Visit(QueryNode node, string pointer, int depth, int offset)
        {
            token.ThrowIfCancellationRequested();
            if (++count > QueryMaxNodes) throw WebFormsReviewEvidenceIndex.Invalid("QUERY_NODE_LIMIT");
            retainedBytes = checked(retainedBytes + Encoding.UTF8.GetByteCount(pointer) + (node.Scalar?.Length ?? 0));
            if (retainedBytes > QueryMaxBytes) throw WebFormsReviewEvidenceIndex.Invalid("QUERY_RESPONSE_LIMIT");
            JsonElement? value = node.Scalar is null ? null : JsonSerializer.Deserialize<JsonElement>(node.Scalar);
            var children = new List<WebFormsEvidenceItem>();
            if (depth > 0 && node.Count > 0)
            {
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT id,ordinal,name FROM json_nodes WHERE document=$doc AND parent_id=$parent AND ordinal >= $offset ORDER BY ordinal LIMIT $limit";
                command.Parameters.AddWithValue("$doc", query.Document); command.Parameters.AddWithValue("$parent", node.Id);
                command.Parameters.AddWithValue("$limit", query.Limit); command.Parameters.AddWithValue("$offset", offset);
                var rows = new List<(long Id, int Ordinal, string? Name)>();
                using (var reader = command.ExecuteReader()) while (reader.Read()) rows.Add((reader.GetInt64(0), reader.GetInt32(1), reader.IsDBNull(2) ? null : reader.GetString(2)));
                foreach (var row in rows)
                {
                    // Retain only a bounded locator page, not all sibling scalar blobs.
                    using var exact = connection.CreateCommand();
                    exact.CommandText = "SELECT id,ordinal,name,kind,scalar_json,child_count,length(scalar_json) FROM json_nodes WHERE document=$doc AND id=$id";
                    exact.Parameters.AddWithValue("$doc", query.Document); exact.Parameters.AddWithValue("$id", row.Id);
                    QueryNode child;
                    using (var reader = exact.ExecuteReader()) { if (!reader.Read()) throw WebFormsReviewEvidenceIndex.Invalid("QUERY_NODE_UNAVAILABLE"); child = Node(reader); }
                    var key = node.Kind == "array" ? row.Ordinal.ToString(CultureInfo.InvariantCulture) : row.Name!;
                    children.Add(Visit(child, pointer + "/" + key.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal), depth - 1, 0));
                }
            }
            var omitted = node.Count - children.Count;
            if (omitted > 0) truncated = true;
            return new(pointer, node.Kind, value, node.Count, offset, children.Count, omitted, children)
                { NextOffset = depth > 0 && offset + children.Count < node.Count ? offset + children.Count : null };
        }
        var result = Visit(selected, query.Pointer, query.Depth, query.Offset);
        return (result, truncated);
        static QueryNode Node(SqliteDataReader reader)
        {
            if (!reader.IsDBNull(6) && reader.GetInt64(6) > WebFormsReviewEvidenceIndex.MaxTokenBytes) throw WebFormsReviewEvidenceIndex.Invalid("QUERY_TOKEN_LIMIT");
            if (!reader.IsDBNull(6) && reader.GetInt64(6) > QueryMaxBytes) throw WebFormsReviewEvidenceIndex.Invalid("QUERY_RESPONSE_LIMIT");
            var kind = reader.GetString(3); var scalar = reader.IsDBNull(4) ? null : (byte[])reader.GetValue(4); var childCount = reader.GetInt32(5);
            if (kind is not ("object" or "array" or "string" or "number" or "null" or "boolean") || childCount < 0 ||
                ((kind is "object" or "array") != (scalar is null)) || scalar is not null && childCount != 0) throw WebFormsReviewEvidenceIndex.Invalid("QUERY_NODE_INVALID");
            return new(reader.GetInt64(0), reader.GetInt32(1), reader.IsDBNull(2) ? null : reader.GetString(2), kind, scalar, childCount);
        }
    }
    private static void RejectQuerySidecars(string path)
    {
        foreach (var suffix in new[] { "-wal", "-shm", "-journal" })
            if (File.Exists(path + suffix) || Directory.Exists(path + suffix)) throw WebFormsReviewEvidenceIndex.Invalid("QUERY_INDEX_SIDECAR");
    }
    private sealed class QueryOutputStream(Stream inner, CancellationToken token) : Stream
    {
        private int bytes;
        public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            token.ThrowIfCancellationRequested();
            if (buffer.Length > QueryMaxBytes - bytes) throw WebFormsReviewEvidenceIndex.Invalid("QUERY_RESPONSE_LIMIT");
            inner.Write(buffer); bytes += buffer.Length;
        }
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        { cancellationToken.ThrowIfCancellationRequested(); Write(buffer.Span); return ValueTask.CompletedTask; }
        public override bool CanRead => false; public override bool CanSeek => false; public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException(); public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => inner.Flush(); public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException(); public override void SetLength(long value) => throw new NotSupportedException();
    }
}
