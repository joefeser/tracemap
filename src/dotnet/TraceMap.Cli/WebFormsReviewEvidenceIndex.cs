using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace TraceMap.Cli;

/// <summary>Lossless JSON token tree: indexed lookups never deserialize either complete handoff.</summary>
internal static class WebFormsReviewEvidenceIndex
{
    internal const string Name = "review-evidence.sqlite";
    internal const string Schema = "webforms-review-evidence-index.v1";
    internal const string Rule = "workflow.webforms.bounded-evidence-query.v1";
    internal const int MaxNodes = 2_000_000;
    internal const int NewPlanMaxNodes = 20_000_000;
    internal const int MaxSupportedNodes = 50_000_000;
    internal const int MaxTokenBytes = 1_048_576;
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    internal sealed record Context(string SchemaVersion, string RuleId, string Visibility, string ClaimLevel,
        string GeneratorSha256, string BoundedInputSha256, string RunId,
        string ApplicationHandoffSha256, string CompiledHandoffSha256, int NodeCount,
        int MaxNodes, int MaxTokenBytes, long MaxInputBytes, long MaxIndexBytes);

    internal static async Task<WebFormsReviewArtifact> WriteAsync(string directory, string runId,
        string applicationSha, string compiledSha, long maxInputBytes, long maxIndexBytes, CancellationToken token,
        int maxNodes = MaxNodes, string? applicationPath = null, string? compiledPath = null)
    {
        if (maxNodes is < 2 or > MaxSupportedNodes) throw Invalid("NODE_BUDGET_INVALID");
        var path = Path.Combine(directory, Name);
        if (File.Exists(path) || Directory.Exists(path)) throw Invalid("OUTPUT_EXISTS");
        var application = applicationPath ?? Path.Combine(directory, WebFormsReviewReportExecution.HandoffName);
        var compiled = compiledPath ?? Path.Combine(directory, "compiled", "compiled-paths.handoff.local.json");
        var appHash = await WebFormsReviewPreflightCommand.HashAsync("application", application, maxInputBytes, token);
        var compiledHash = await WebFormsReviewPreflightCommand.HashAsync("compiled", compiled, maxInputBytes, token);
        if (appHash.Sha256 != applicationSha || compiledHash.Sha256 != compiledSha ||
            appHash.Bytes > maxInputBytes - compiledHash.Bytes) throw Invalid("INPUT_CHANGED_OR_LIMIT");
        var generator = await WebFormsReviewPreflightCommand.HashAsync("generator", typeof(WebFormsReviewEvidenceIndex).Assembly.Location, 67_108_864, token);
        var context = new Context(Schema, Rule, "local-only", "review-only-static-not-runtime", generator.Sha256, "", runId,
            applicationSha, compiledSha, 0, maxNodes, MaxTokenBytes, maxInputBytes, maxIndexBytes);
        using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false }.ToString()))
        {
            await connection.OpenAsync(token);
            using var setup = connection.CreateCommand();
            var pages = Math.Clamp(maxIndexBytes / 4096, 1, int.MaxValue);
            setup.CommandText = $"PRAGMA page_size=4096; PRAGMA journal_mode=DELETE; PRAGMA synchronous=FULL; PRAGMA temp_store=FILE; PRAGMA cache_size=-2048; PRAGMA max_page_count={pages.ToString(CultureInfo.InvariantCulture)}; " +
                "CREATE TABLE metadata(id INTEGER PRIMARY KEY CHECK(id=1), context_json TEXT NOT NULL); " +
                "CREATE TABLE json_nodes(id INTEGER PRIMARY KEY, document TEXT NOT NULL, parent_id INTEGER, ordinal INTEGER NOT NULL, name TEXT, kind TEXT NOT NULL, scalar_json BLOB, child_count INTEGER NOT NULL DEFAULT 0);";
            setup.ExecuteNonQuery();
            using var transaction = connection.BeginTransaction();
            using var writer = new TokenWriter(connection, transaction, token, maxNodes);
            writer.Read("application", application);
            writer.Read("compiled", compiled);
            context = context with { NodeCount = writer.Count };
            context = context with { BoundedInputSha256 = ContextHash(context) };
            using var meta = connection.CreateCommand(); meta.Transaction = transaction;
            meta.CommandText = "INSERT INTO metadata(id,context_json) VALUES(1,$json)";
            meta.Parameters.AddWithValue("$json", JsonSerializer.Serialize(context, Json));
            meta.ExecuteNonQuery();
            // Bulk-load the token tree before constructing secondary indexes.
            // Unique index creation still rejects duplicate object properties.
            using var indexes = connection.CreateCommand(); indexes.Transaction = transaction;
            indexes.CommandText = "CREATE UNIQUE INDEX json_child_order ON json_nodes(document,parent_id,ordinal); " +
                "CREATE UNIQUE INDEX json_child_name ON json_nodes(document,parent_id,name) WHERE name IS NOT NULL; " +
                "CREATE UNIQUE INDEX json_document_root ON json_nodes(document) WHERE parent_id IS NULL;";
            indexes.ExecuteNonQuery(); transaction.Commit();
        }
        var afterApp = await WebFormsReviewPreflightCommand.HashAsync("application", application, maxInputBytes, token);
        var afterCompiled = await WebFormsReviewPreflightCommand.HashAsync("compiled", compiled, maxInputBytes, token);
        if (appHash != afterApp || compiledHash != afterCompiled) throw Invalid("INPUT_CHANGED");
        var artifact = await WebFormsReviewPreflightCommand.HashAsync("evidence-index", path, maxIndexBytes, token);
        return new(Name, artifact.Bytes, artifact.Sha256);
    }

    internal static Context ReadContext(SqliteConnection connection, string runId, string applicationSha, string compiledSha)
    {
        using var command = connection.CreateCommand(); command.CommandText = "SELECT context_json FROM metadata WHERE id=1";
        var text = command.ExecuteScalar() as string ?? throw Invalid("CONTEXT_UNAVAILABLE");
        if (Encoding.UTF8.GetByteCount(text) > 16_384) throw Invalid("CONTEXT_LIMIT");
        var bytes = Encoding.UTF8.GetBytes(text);
        WebFormsReviewPreflightCommand.RejectDuplicateProperties(bytes);
        var context = JsonSerializer.Deserialize<Context>(bytes, new JsonSerializerOptions(Json) { UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow }) ?? throw Invalid("CONTEXT_INVALID");
        if (context.SchemaVersion != Schema || context.RuleId != Rule || context.Visibility != "local-only" ||
            context.ClaimLevel != "review-only-static-not-runtime" || context.RunId != runId ||
            context.ApplicationHandoffSha256 != applicationSha || context.CompiledHandoffSha256 != compiledSha ||
            !HashShape(context.GeneratorSha256) || context.MaxNodes is < 2 or > MaxSupportedNodes ||
            context.NodeCount < 2 || context.NodeCount > context.MaxNodes ||
            context.MaxTokenBytes != MaxTokenBytes || context.MaxInputBytes <= 0 || context.MaxIndexBytes <= 0 ||
            context.BoundedInputSha256 != ContextHash(context)) throw Invalid("CONTEXT_INVALID");
        return context;
    }

    private static string ContextHash(Context context) => WebFormsReviewReportExecution.CanonicalHash(context with { BoundedInputSha256 = "" }, 16_384, CancellationToken.None);
    private static bool HashShape(string? value) => value?.Length == 64 && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
    internal static InvalidDataException Invalid(string category) => new("WEBFORMS_EVIDENCE_" + category);

    private sealed class Frame(long id, bool isObject)
    {
        public long Id { get; } = id;
        public bool IsObject { get; } = isObject;
        public int Children { get; set; }
        public string? Name { get; set; }
    }
    private sealed class TokenWriter : IDisposable
    {
        private readonly SqliteCommand insert;
        private readonly SqliteCommand finish;
        private readonly CancellationToken token;
        private readonly int maxNodes;
        private readonly Stack<Frame> stack = new();
        public int Count { get; private set; }
        public TokenWriter(SqliteConnection connection, SqliteTransaction transaction, CancellationToken token, int maxNodes)
        {
            this.token = token;
            this.maxNodes = maxNodes;
            insert = connection.CreateCommand(); insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO json_nodes(id,document,parent_id,ordinal,name,kind,scalar_json) VALUES($id,$document,$parent,$ordinal,$name,$kind,$value)";
            foreach (var parameter in new[] { "$id", "$document", "$parent", "$ordinal", "$name", "$kind", "$value" }) insert.Parameters.Add(new SqliteParameter(parameter, DBNull.Value));
            finish = connection.CreateCommand(); finish.Transaction = transaction;
            finish.CommandText = "UPDATE json_nodes SET child_count=$count WHERE id=$id";
            finish.Parameters.Add(new("$count", 0)); finish.Parameters.Add(new("$id", 0));
        }
        public void Read(string document, string path)
        {
            using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65_536, FileOptions.SequentialScan);
            var buffer = new byte[65_536]; var length = 0; var eof = false; var roots = 0;
            var state = new JsonReaderState(new JsonReaderOptions { MaxDepth = 64 });
            while (true)
            {
                token.ThrowIfCancellationRequested();
                if (length == buffer.Length)
                {
                    if (buffer.Length >= MaxTokenBytes) throw Invalid("TOKEN_LIMIT");
                    Array.Resize(ref buffer, Math.Min(MaxTokenBytes, buffer.Length * 2));
                }
                var read = input.Read(buffer, length, buffer.Length - length); length += read; eof = read == 0;
                var reader = new Utf8JsonReader(buffer.AsSpan(0, length), eof, state);
                while (reader.Read())
                {
                    token.ThrowIfCancellationRequested();
                    if (reader.TokenType == JsonTokenType.PropertyName)
                    {
                        if (stack.Count == 0 || !stack.Peek().IsObject || stack.Peek().Name is not null) throw Invalid("JSON_SHAPE");
                        var name = reader.GetString()!;
                        if (name.Length > 4096) throw Invalid("PROPERTY_LIMIT");
                        stack.Peek().Name = name; continue;
                    }
                    if (reader.TokenType is JsonTokenType.EndObject or JsonTokenType.EndArray)
                    {
                        if (!stack.TryPop(out var frame) || frame.Name is not null || frame.IsObject != (reader.TokenType == JsonTokenType.EndObject)) throw Invalid("JSON_SHAPE");
                        finish.Parameters["$count"].Value = frame.Children; finish.Parameters["$id"].Value = frame.Id;
                        finish.ExecuteNonQuery(); continue;
                    }
                    if (++Count > maxNodes) throw Invalid("NODE_LIMIT");
                    Frame? parent = stack.TryPeek(out var current) ? current : null;
                    if (parent is null && ++roots != 1 || parent is { IsObject: true, Name: null }) throw Invalid("JSON_SHAPE");
                    var container = reader.TokenType is JsonTokenType.StartObject or JsonTokenType.StartArray;
                    var raw = container ? null : buffer.AsSpan(checked((int)reader.TokenStartIndex), checked((int)(reader.BytesConsumed - reader.TokenStartIndex))).ToArray();
                    if (!container && reader.TokenType is not (JsonTokenType.String or JsonTokenType.Number or JsonTokenType.True or JsonTokenType.False or JsonTokenType.Null)) throw Invalid("JSON_SHAPE");
                    insert.Parameters["$id"].Value = Count; insert.Parameters["$document"].Value = document;
                    insert.Parameters["$parent"].Value = (object?)parent?.Id ?? DBNull.Value;
                    insert.Parameters["$ordinal"].Value = parent?.Children ?? 0;
                    insert.Parameters["$name"].Value = (object?)parent?.Name ?? DBNull.Value;
                    insert.Parameters["$kind"].Value = reader.TokenType switch { JsonTokenType.StartObject => "object", JsonTokenType.StartArray => "array", JsonTokenType.String => "string", JsonTokenType.Number => "number", JsonTokenType.Null => "null", _ => "boolean" };
                    insert.Parameters["$value"].Value = (object?)raw ?? DBNull.Value; insert.ExecuteNonQuery();
                    if (parent is not null) { parent.Children++; parent.Name = null; }
                    if (container) stack.Push(new(Count, reader.TokenType == JsonTokenType.StartObject));
                }
                var consumed = checked((int)reader.BytesConsumed); state = reader.CurrentState;
                buffer.AsSpan(consumed, length - consumed).CopyTo(buffer); length -= consumed;
                if (eof)
                {
                    if (length != 0 || stack.Count != 0 || roots != 1) throw Invalid("JSON_INCOMPLETE");
                    break;
                }
            }
        }
        public void Dispose() { insert.Dispose(); finish.Dispose(); }
    }
}
