using System.Collections;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace TraceMap.Reporting;

public static partial class CombinedDependencyPathReporter
{
    // This private scratch database is never published or discovered for reuse.
    // SQLite owns its temporary file and removes it on connection disposal. The
    // byte commitments identify the exact generator and input, not authenticity.
    private sealed class IndexedGraphStore : IDisposable
    {
        private readonly SqliteConnection connection;
        private bool sorted;
        private long ordinal;
        private readonly string inputSha256;
        private readonly CancellationToken token;
        public int NodeCount { get; private set; }
        public int EdgeCount { get; private set; }

        public IndexedGraphStore(string generatorSha256, string inputSha256, long maxStorageBytes, CancellationToken token)
        {
            if (maxStorageBytes < 4096) throw new ArgumentOutOfRangeException(nameof(maxStorageBytes));
            if (maxStorageBytes < 64 * 1024) throw new ReportInputLimitException("graph-storage-bytes");
            this.inputSha256 = inputSha256;
            this.token = token;
            connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = "", Pooling = false, Cache = SqliteCacheMode.Private
            }.ToString());
            try
            {
                connection.Open();
                connection.CreateCollation("graph_ordinal", StringComparer.Ordinal.Compare);
                var schema = $$"""
                    pragma page_size=4096;
                    pragma max_page_count={{maxStorageBytes / 4096}};
                    pragma journal_mode=off;
                    pragma synchronous=off;
                    pragma temp_store=file;
                    pragma cache_size=-8192;
                    create table graph_metadata(key text primary key, value text not null);
                    create table graph_nodes(id text primary key, display_name text not null, payload text not null, ordinal integer not null);
                    create table graph_edges(id text primary key, from_id text not null, to_id text not null,
                        rank integer not null, file_path text, line integer not null,
                        payload text not null, ordinal integer not null);
                    create index graph_edges_from on graph_edges(from_id, ordinal);
                    create table graph_aliases(node_id text primary key, member_key text not null,
                        type_key text, signature_key text, ordinal integer not null);
                    create index graph_aliases_member on graph_aliases(member_key, ordinal);
                    """;
                foreach (var statement in schema.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                {
                    using var setup = connection.CreateCommand();
                    setup.CommandText = statement;
                    setup.ExecuteNonQuery();
                }
                using var command = connection.CreateCommand();
                command.CommandText = """
                    insert into graph_metadata values ('schema', 'private.transient-path-graph.v1'),
                        ('generatorSha256', $generator), ('boundedInputSha256', $input),
                        ('maxStorageBytes', $maximum);
                    """;
                command.Parameters.AddWithValue("$generator", generatorSha256);
                command.Parameters.AddWithValue("$input", inputSha256);
                command.Parameters.AddWithValue("$maximum", maxStorageBytes.ToString(System.Globalization.CultureInfo.InvariantCulture));
                command.ExecuteNonQuery();
                using var verify = connection.CreateCommand();
                verify.CommandText = "select count(*) from sqlite_master where type='table' and name in ('graph_metadata','graph_nodes','graph_edges','graph_aliases');";
                if (Convert.ToInt64(verify.ExecuteScalar()) != 4)
                    throw new InvalidDataException("COMBINED_GRAPH_STORAGE_SCHEMA_INVALID");
            }
            catch (SqliteException exception) when (exception.SqliteErrorCode == 13)
            { connection.Dispose(); throw new ReportInputLimitException("graph-storage-bytes"); }
            catch { connection.Dispose(); throw; }
        }

        private SqliteCommand Command(string sql, string? id = null)
        {
            token.ThrowIfCancellationRequested();
            var command = connection.CreateCommand();
            command.CommandText = sql;
            if (id is not null) command.Parameters.AddWithValue("$id", id);
            return command;
        }

        public bool ContainsNode(string id)
        {
            using var command = Command("select exists(select 1 from graph_nodes where id=$id);", id);
            return Convert.ToInt64(command.ExecuteScalar()) != 0;
        }

        public bool TryGetNode(string id, out GraphNode node)
        {
            using var command = Command("select payload from graph_nodes where id=$id;", id);
            var json = command.ExecuteScalar() as string;
            node = json is null ? null! : JsonSerializer.Deserialize<GraphNode>(json)!;
            return json is not null;
        }

        public bool AddNode(GraphNode node)
        {
            using var command = Command("insert or ignore into graph_nodes values($id,$name,$payload,$ordinal);", node.NodeId);
            command.Parameters.AddWithValue("$name", node.DisplayName);
            command.Parameters.AddWithValue("$payload", JsonSerializer.Serialize(node));
            command.Parameters.AddWithValue("$ordinal", ordinal++);
            var added = Write(command) != 0;
            if (added) NodeCount++;
            return added;
        }

        public IEnumerable<GraphNode> Nodes(bool identityOrder = false)
        {
            using var command = Command("select payload from graph_nodes order by "
                + (identityOrder ? "id collate graph_ordinal" : "ordinal") + ";");
            using var reader = command.ExecuteReader();
            while (reader.Read())
            { token.ThrowIfCancellationRequested(); yield return JsonSerializer.Deserialize<GraphNode>(reader.GetString(0))!; }
        }

        public IEnumerable<string> NodeIds()
        {
            using var command = Command("select id from graph_nodes order by ordinal;");
            using var reader = command.ExecuteReader();
            while (reader.Read())
            { token.ThrowIfCancellationRequested(); yield return reader.GetString(0); }
        }

        public bool ContainsEdge(string id)
        {
            using var command = Command("select exists(select 1 from graph_edges where id=$id);", id);
            return Convert.ToInt64(command.ExecuteScalar()) != 0;
        }

        public GraphEdge Edge(string id)
        {
            using var command = Command("select payload from graph_edges where id=$id;", id);
            var json = command.ExecuteScalar() as string ?? throw new KeyNotFoundException();
            return JsonSerializer.Deserialize<GraphEdge>(json)!;
        }

        public void AddEdge(GraphEdge edge)
        {
            using var command = Command("insert into graph_edges values($id,$from,$to,$rank,$file,$line,$payload,$ordinal);", edge.EdgeId);
            command.Parameters.AddWithValue("$from", edge.FromNodeId);
            command.Parameters.AddWithValue("$to", edge.ToNodeId);
            command.Parameters.AddWithValue("$rank", EdgeRank(edge.EdgeKind));
            command.Parameters.AddWithValue("$file", (object?)edge.FilePath ?? DBNull.Value);
            command.Parameters.AddWithValue("$line", edge.StartLine ?? 0);
            command.Parameters.AddWithValue("$payload", JsonSerializer.Serialize(edge));
            command.Parameters.AddWithValue("$ordinal", ordinal++);
            Write(command);
            EdgeCount++;
        }

        public IEnumerable<GraphEdge> Edges(string? from = null)
        {
            var order = sorted
                ? "e.rank, n.display_name collate graph_ordinal, e.file_path collate graph_ordinal, e.line, e.id collate graph_ordinal"
                : "e.ordinal";
            using var command = Command("select e.payload from graph_edges e join graph_nodes n on n.id=e.to_id "
                + (from is null ? "" : "where e.from_id=$id ") + "order by " + order + ";", from);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            { token.ThrowIfCancellationRequested(); yield return JsonSerializer.Deserialize<GraphEdge>(reader.GetString(0))!; }
        }

        private static int Write(SqliteCommand command)
        {
            try { return command.ExecuteNonQuery(); }
            catch (SqliteException exception) when (exception.SqliteErrorCode == 13)
            { throw new ReportInputLimitException("graph-storage-bytes"); }
        }

        public async Task AssertInputUnchangedAsync(string inputPath, CancellationToken cancellationToken)
        {
            await using var input = File.OpenRead(inputPath);
            var current = Convert.ToHexString(await SHA256.HashDataAsync(input, cancellationToken)).ToLowerInvariant();
            if (!string.Equals(current, inputSha256, StringComparison.Ordinal))
                throw new InvalidDataException("COMBINED_GRAPH_INPUT_CHANGED");
        }

        public void Sort() => sorted = true;

        public IndexedGraphUsage Observe(GraphOutgoing outgoing)
        {
            using var size = Command("select (select page_count from pragma_page_count) * (select page_size from pragma_page_size);");
            using var generator = Command("select value from graph_metadata where key='generatorSha256';");
            return new IndexedGraphUsage("sqlite-temporary", (string)generator.ExecuteScalar()!, inputSha256,
                NodeCount, EdgeCount, Convert.ToInt64(size.ExecuteScalar()), outgoing.MaximumRowsLoaded);
        }

        public IEnumerable<IReadOnlyList<(GraphNode Node, SymbolAlias Alias)>> SymbolReconciliationGroups()
        {
            foreach (var node in Nodes())
            {
                if (node.NodeKind is not ("Symbol" or "Method" or "Type")) continue;
                var alias = TryCreateSymbolAlias(node.DisplayName);
                if (alias is null) continue;
                using var insert = Command("insert into graph_aliases values($id,$member,$type,$signature,$ordinal);", node.NodeId);
                insert.Parameters.AddWithValue("$member", node.SourceIndexId + "\0" + alias.MemberKey);
                insert.Parameters.AddWithValue("$type", (object?)alias.TypeKey ?? DBNull.Value);
                insert.Parameters.AddWithValue("$signature", (object?)alias.SignatureKey ?? DBNull.Value);
                insert.Parameters.AddWithValue("$ordinal", ordinal++);
                Write(insert);
            }
            using var keys = Command("select member_key from graph_aliases group by member_key order by min(ordinal);");
            using var keyReader = keys.ExecuteReader();
            while (keyReader.Read())
            {
                token.ThrowIfCancellationRequested();
                using var group = Command("""
                    select n.payload,a.type_key,a.signature_key from graph_aliases a
                    join graph_nodes n on n.id=a.node_id where a.member_key=$id order by a.ordinal;
                    """, keyReader.GetString(0));
                using var reader = group.ExecuteReader();
                var rows = new List<(GraphNode Node, SymbolAlias Alias)>();
                while (reader.Read())
                {
                    token.ThrowIfCancellationRequested();
                    var node = JsonSerializer.Deserialize<GraphNode>(reader.GetString(0))!;
                    var alias = TryCreateSymbolAlias(node.DisplayName)!;
                    rows.Add((node, alias));
                }
                yield return rows;
            }
        }
        public void Dispose() => connection.Dispose();
    }

    private sealed class GraphNodes(IndexedGraphStore? store)
    {
        private readonly Dictionary<string, GraphNode> memory = new(StringComparer.Ordinal);
        public int Count => store?.NodeCount ?? memory.Count;
        public IEnumerable<string> Keys => store?.NodeIds() ?? memory.Keys;
        public IEnumerable<GraphNode> Values => store?.Nodes() ?? memory.Values;
        public IEnumerable<GraphNode> IdentityOrderedValues => store?.Nodes(identityOrder: true)
            ?? memory.Values.OrderBy(node => node.NodeId, StringComparer.Ordinal);
        public bool ContainsKey(string id) => store?.ContainsNode(id) ?? memory.ContainsKey(id);
        public bool TryGetValue(string id, out GraphNode node)
            => store is null ? memory.TryGetValue(id, out node!) : store.TryGetNode(id, out node);
        public GraphNode this[string id]
        {
            get => TryGetValue(id, out var node) ? node : throw new KeyNotFoundException();
            set { if (store is null) memory[id] = value; else store.AddNode(value); }
        }
        public void TryAdd(string id, GraphNode node)
        { if (store is null) memory.TryAdd(id, node); else store.AddNode(node); }
    }

    private sealed class GraphEdges(IndexedGraphStore? store) : IEnumerable<GraphEdge>
    {
        private readonly List<GraphEdge> memory = [];
        public int Count => store?.EdgeCount ?? memory.Count;
        public void Add(GraphEdge edge) { if (store is null) memory.Add(edge); else store.AddEdge(edge); }
        public void Sort(Comparison<GraphEdge> comparison)
        { if (store is null) memory.Sort(comparison); else store.Sort(); }
        public IEnumerator<GraphEdge> GetEnumerator() => (store?.Edges() ?? memory).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class GraphEdgeIds(IndexedGraphStore? store)
    {
        private readonly Dictionary<string, GraphEdge> memory = new(StringComparer.Ordinal);
        public bool ContainsKey(string id) => store?.ContainsEdge(id) ?? memory.ContainsKey(id);
        public GraphEdge this[string id]
        {
            get => store is null ? memory[id] : store.Edge(id);
            set { if (store is null) memory[id] = value; }
        }
    }

    private sealed class GraphOutgoing(IndexedGraphStore? store)
    {
        private readonly Dictionary<string, List<GraphEdge>> memory = new(StringComparer.Ordinal);
        public int MaximumRowsLoaded { get; private set; }
        public bool TryGetValue(string id, out List<GraphEdge> edges)
        {
            if (store is null) return memory.TryGetValue(id, out edges!);
            edges = store.Edges(id).ToList();
            MaximumRowsLoaded = Math.Max(MaximumRowsLoaded, edges.Count);
            return edges.Count != 0;
        }
        public List<GraphEdge> this[string id] { set => memory[id] = value; }
        public IEnumerable<KeyValuePair<string, List<GraphEdge>>> MemoryEntries => memory;
    }

    private sealed class DispatchGraphNodes(GraphNodes nodes) : IReadOnlyDictionary<string, StaticDispatchCandidateNode>
    {
        private static StaticDispatchCandidateNode Project(GraphNode node) => new(
            node.NodeId, node.NodeKind, node.DisplayName, node.SourceIndexId, node.SourceLabel,
            node.CommitSha, node.FilePath, node.StartLine, node.EndLine);
        public int Count => nodes.Count;
        public IEnumerable<string> Keys => nodes.IdentityOrderedValues.Select(node => node.NodeId);
        public IEnumerable<StaticDispatchCandidateNode> Values => nodes.IdentityOrderedValues.Select(Project);
        public StaticDispatchCandidateNode this[string key] => Project(nodes[key]);
        public bool ContainsKey(string key) => nodes.ContainsKey(key);
        public bool TryGetValue(string key, out StaticDispatchCandidateNode value)
        {
            if (nodes.TryGetValue(key, out var node)) { value = Project(node); return true; }
            value = null!; return false;
        }
        public IEnumerator<KeyValuePair<string, StaticDispatchCandidateNode>> GetEnumerator()
            => nodes.IdentityOrderedValues.Select(node => new KeyValuePair<string, StaticDispatchCandidateNode>(node.NodeId, Project(node))).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    internal sealed record IndexedGraphUsage(string Engine, string GeneratorSha256, string InputSha256,
        int StoredNodes, int StoredEdges, long LogicalStorageBytes, int MaximumOutgoingRowsLoaded);

    private static async Task<IndexedGraphStore> CreateIndexedGraphStoreAsync(string inputPath, long maxStorageBytes, CancellationToken token)
    {
        await using var input = File.OpenRead(inputPath);
        var inputHash = Convert.ToHexString(await SHA256.HashDataAsync(input, token)).ToLowerInvariant();
        await using var generator = File.OpenRead(typeof(CombinedDependencyPathReporter).Assembly.Location);
        var generatorHash = Convert.ToHexString(await SHA256.HashDataAsync(generator, token)).ToLowerInvariant();
        return new IndexedGraphStore(generatorHash, inputHash, maxStorageBytes, token);
    }
}
