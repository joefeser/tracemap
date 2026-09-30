using System.Collections;
using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;

namespace TraceMap.Reporting;

public static partial class CombinedDependencyPathReporter
{
    // This private scratch database is never published or discovered for reuse.
    // SQLite owns its temporary file and removes it on connection disposal. The
    // byte commitments identify the exact generator and input, not authenticity.
    private sealed partial class IndexedGraphStore : IDisposable
    {
        private readonly SqliteConnection connection;
        private bool sorted;
        private long ordinal;
        private readonly string inputSha256;
        private readonly string generatorSha256;
        private readonly CancellationToken token;
        private long payloadDecodedBytes;
        private long payloadFrameBytes;
        private long globalEdgePayloadRowsRead;
        private long incomingCountQueries;
        private long incomingReferenceRowsObserved;
        private string storagePhase = "schema";
        private long successfulWrites;
        private long? snapshotLogicalBytes;
        private long? snapshotWrites;
        private IReadOnlyDictionary<string, long>? snapshotObjectBytes;
        private readonly Dictionary<string, long> observationTicks = new(StringComparer.Ordinal);
        private string observationStage = "schema";
        private long observationStarted = Stopwatch.GetTimestamp();
        private readonly Action<string>? stageObserver;
        public int MaximumOutgoingRowsLoaded { get; private set; }
        public long MaximumOutgoingDecodedBytesLoaded { get; private set; }
        public int NodeCount { get; private set; }
        public int EdgeCount { get; private set; }

        public void MarkObservationStage(string stage)
        {
            var now = Stopwatch.GetTimestamp();
            observationTicks[observationStage] = checked(observationTicks.GetValueOrDefault(observationStage)
                + now - observationStarted);
            observationStage = stage;
            observationStarted = now;
            stageObserver?.Invoke(stage);
        }

        private IReadOnlyDictionary<string, long> ObserveStageMilliseconds()
        {
            var ticks = new Dictionary<string, long>(observationTicks, StringComparer.Ordinal);
            ticks[observationStage] = checked(ticks.GetValueOrDefault(observationStage)
                + Stopwatch.GetTimestamp() - observationStarted);
            return ticks.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToDictionary(pair => pair.Key,
                pair => (long)(pair.Value * 1000d / Stopwatch.Frequency), StringComparer.Ordinal);
        }

        public IndexedGraphStore(string generatorSha256, string inputSha256, long maxStorageBytes, CancellationToken token,
            Action<string>? stageObserver = null)
        {
            if (maxStorageBytes < 4096) throw new ArgumentOutOfRangeException(nameof(maxStorageBytes));
            if (maxStorageBytes < 64 * 1024) throw new ReportInputLimitException("graph-storage-bytes");
            this.inputSha256 = inputSha256;
            this.generatorSha256 = generatorSha256;
            this.token = token;
            this.stageObserver = stageObserver;
            Facts = new IndexedFactRows(this);
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
                    create table graph_facts(ordinal integer primary key, id text not null unique,
                        source_id text not null, original_id text not null, source_key text not null,
                        fact_type text not null, payload blob not null, il_call_reference text);
                    create unique index graph_facts_original on graph_facts(source_id,original_id);
                    create index graph_facts_identity_order on graph_facts(id collate graph_ordinal);
                    create index graph_facts_type_order on graph_facts(fact_type,ordinal);
                    create index graph_facts_source_key on graph_facts(source_key,id collate graph_ordinal);
                    create index graph_facts_il_call_reference on graph_facts(fact_type,source_id,il_call_reference,ordinal)
                        where il_call_reference is not null;
                    create table graph_nodes(id text primary key, display_name text not null, payload blob not null, ordinal integer not null);
                    create table graph_edges(id text primary key, from_id text not null, to_id text not null,
                        rank integer not null, file_path text, line integer not null,
                        payload blob not null, ordinal integer not null, payload_bytes integer not null);
                    create index graph_edges_from on graph_edges(from_id, ordinal);
                    create index graph_edges_to on graph_edges(to_id,ordinal);
                    create table graph_edge_order(global_order integer primary key, from_id text not null,
                        local_order integer not null, edge_id text not null unique);
                    create unique index graph_edge_order_from on graph_edge_order(from_id, local_order);
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
                    insert into graph_metadata values ('schema', 'private.transient-path-graph.v3'),
                        ('payloadEncoding', 'length-framed-json-v1'),
                        ('generatorSha256', $generator), ('boundedInputSha256', $input),
                        ('maxStorageBytes', $maximum);
                    """;
                command.Parameters.AddWithValue("$generator", generatorSha256);
                command.Parameters.AddWithValue("$input", inputSha256);
                command.Parameters.AddWithValue("$maximum", maxStorageBytes.ToString(System.Globalization.CultureInfo.InvariantCulture));
                command.ExecuteNonQuery();
                using var verify = connection.CreateCommand();
                verify.CommandText = "select count(*) from sqlite_master where type='table' and name in ('graph_metadata','graph_facts','graph_nodes','graph_edges','graph_aliases','graph_edge_order');";
                if (Convert.ToInt64(verify.ExecuteScalar()) != 6)
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
            var payload = command.ExecuteScalar() as byte[];
            node = payload is null ? null! : IndexedGraphPayload.Decode<GraphNode>(payload);
            return payload is not null;
        }

        public bool AddNode(GraphNode node)
        {
            storagePhase = "nodes";
            if (sorted) throw new InvalidOperationException("COMBINED_GRAPH_STORAGE_FROZEN");
            using var command = Command("insert or ignore into graph_nodes values($id,$name,$payload,$ordinal);", node.NodeId);
            command.Parameters.AddWithValue("$name", node.DisplayName);
            var payload = IndexedGraphPayload.Encode(node);
            command.Parameters.AddWithValue("$payload", payload);
            command.Parameters.AddWithValue("$ordinal", ordinal++);
            var added = Write(command) != 0;
            if (added) { NodeCount++; RecordPayload(payload); }
            return added;
        }

        public IEnumerable<GraphNode> Nodes(bool identityOrder = false)
        {
            using var command = Command("select payload from graph_nodes order by "
                + (identityOrder ? "id collate graph_ordinal" : "ordinal") + ";");
            using var reader = command.ExecuteReader();
            while (reader.Read())
            { token.ThrowIfCancellationRequested(); yield return IndexedGraphPayload.Decode<GraphNode>((byte[])reader.GetValue(0)); }
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

        public bool HasAtLeastIncomingEdges(string id, int minimum)
        {
            if (minimum <= 0) throw new ArgumentOutOfRangeException(nameof(minimum));
            // Classification only asks whether the global count reaches a
            // threshold. Read at most that many index keys, never edge payloads.
            using var command = Command("select count(*) from (select 1 from graph_edges where to_id=$id limit $minimum);", id);
            command.Parameters.AddWithValue("$minimum", minimum);
            var observed = Convert.ToInt64(command.ExecuteScalar());
            incomingCountQueries++;
            incomingReferenceRowsObserved = checked(incomingReferenceRowsObserved + observed);
            return observed >= minimum;
        }

        public GraphEdge Edge(string id)
        {
            using var command = Command("select payload from graph_edges where id=$id;", id);
            var payload = command.ExecuteScalar() as byte[] ?? throw new KeyNotFoundException();
            return IndexedGraphPayload.Decode<GraphEdge>(payload);
        }

        public void AddEdge(GraphEdge edge)
        {
            storagePhase = "edges";
            if (sorted) throw new InvalidOperationException("COMBINED_GRAPH_STORAGE_FROZEN");
            using var command = Command("insert into graph_edges values($id,$from,$to,$rank,$file,$line,$payload,$ordinal,$bytes);", edge.EdgeId);
            command.Parameters.AddWithValue("$from", edge.FromNodeId);
            command.Parameters.AddWithValue("$to", edge.ToNodeId);
            command.Parameters.AddWithValue("$rank", EdgeRank(edge.EdgeKind));
            command.Parameters.AddWithValue("$file", (object?)edge.FilePath ?? DBNull.Value);
            command.Parameters.AddWithValue("$line", edge.StartLine ?? 0);
            var payload = IndexedGraphPayload.Encode(edge);
            command.Parameters.AddWithValue("$payload", payload);
            command.Parameters.AddWithValue("$bytes", IndexedGraphPayload.DecodedLength(payload));
            command.Parameters.AddWithValue("$ordinal", ordinal++);
            Write(command);
            RecordPayload(payload);
            EdgeCount++;
        }

        public IEnumerable<GraphEdge> Edges(string? from = null)
        {
            var sql = sorted
                ? "select e.payload from graph_edge_order o join graph_edges e on e.id=o.edge_id "
                    + (from is null ? "order by o.global_order;" : "where o.from_id=$id order by o.local_order;")
                : "select payload from graph_edges " + (from is null ? "" : "where from_id=$id ") + "order by ordinal;";
            using var command = Command(sql, from);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                token.ThrowIfCancellationRequested();
                if (from is null) globalEdgePayloadRowsRead++;
                if (from is not null) MaximumOutgoingRowsLoaded = Math.Max(MaximumOutgoingRowsLoaded, 1);
                yield return IndexedGraphPayload.Decode<GraphEdge>((byte[])reader.GetValue(0));
            }
        }

        public IReadOnlyList<GraphEdge> Outgoing(string id)
        {
            using var count = Command("select count(*) from graph_edges where from_id=$id;", id);
            return new IndexedOutgoingEdges(this, id, checked((int)Convert.ToInt64(count.ExecuteScalar())));
        }

        public IEnumerable<string> Predecessors(string id)
        {
            using var command = Command("select from_id from graph_edges where to_id=$id order by ordinal;", id);
            using var reader = command.ExecuteReader();
            incomingCountQueries++;
            while (reader.Read())
            {
                token.ThrowIfCancellationRequested();
                incomingReferenceRowsObserved++;
                yield return reader.GetString(0);
            }
        }

        private IReadOnlyList<GraphEdge> ReadOutgoingPage(string id, int startIndex)
        {
            using var command = Command(sorted ? """
                select e.payload,e.payload_bytes from graph_edge_order o
                join graph_edges e on e.id=o.edge_id
                where o.from_id=$id and o.local_order >= $position order by o.local_order limit 64;
                """ : """
                select payload,payload_bytes from graph_edges
                where from_id=$id order by ordinal limit 64 offset $offset;
                """, id);
            command.Parameters.AddWithValue(sorted ? "$position" : "$offset", sorted ? startIndex + 1 : startIndex);
            using var reader = command.ExecuteReader();
            var rows = new List<GraphEdge>();
            long bytes = 0;
            while (reader.Read())
            {
                token.ThrowIfCancellationRequested();
                var rowBytes = reader.GetInt64(1);
                // One already-admitted large row can exceed the cache target;
                // never retain a second row in that case or silently skip it.
                if (rows.Count != 0 && rowBytes > 512 * 1024 - bytes) break;
                var payload = (byte[])reader.GetValue(0);
                if (IndexedGraphPayload.DecodedLength(payload) != rowBytes)
                    throw new InvalidDataException("COMBINED_GRAPH_PAYLOAD_INVALID");
                rows.Add(IndexedGraphPayload.Decode<GraphEdge>(payload));
                bytes += rowBytes;
            }
            MaximumOutgoingRowsLoaded = Math.Max(MaximumOutgoingRowsLoaded, rows.Count);
            MaximumOutgoingDecodedBytesLoaded = Math.Max(MaximumOutgoingDecodedBytesLoaded, bytes);
            return rows;
        }

        private sealed class IndexedOutgoingEdges(IndexedGraphStore store, string id, int count) : IReadOnlyList<GraphEdge>
        {
            private int pageStart = -1;
            private IReadOnlyList<GraphEdge> page = [];
            public int Count => count;
            public GraphEdge this[int index]
            {
                get
                {
                    if ((uint)index >= (uint)Count) throw new ArgumentOutOfRangeException(nameof(index));
                    if (index < pageStart || index >= pageStart + page.Count)
                    {
                        page = [];
                        pageStart = index / 64 * 64;
                        page = store.ReadOutgoingPage(id, pageStart);
                        if (index >= pageStart + page.Count)
                        {
                            page = [];
                            pageStart = index;
                            page = store.ReadOutgoingPage(id, index);
                        }
                        if (page.Count == 0) throw new InvalidDataException("COMBINED_GRAPH_ORDER_INCOMPLETE");
                    }
                    return page[index - pageStart];
                }
            }
            public IEnumerator<GraphEdge> GetEnumerator() => store.Edges(id).GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        private int Write(SqliteCommand command)
        {
            // journal_mode=off means SQLITE_FULL can leave the disposable store
            // unreadable. Capture bounded allocation observations while it is
            // still valid; never inspect a damaged store to recover a graph.
            if (successfulWrites % 8192 == 0)
            {
                using var size = Command("select (select page_count from pragma_page_count) * (select page_size from pragma_page_size);");
                snapshotLogicalBytes = Convert.ToInt64(size.ExecuteScalar());
                snapshotObjectBytes = ReadObjectStorageBytes();
                snapshotWrites = successfulWrites;
            }
            try { var rows = command.ExecuteNonQuery(); successfulWrites++; return rows; }
            catch (SqliteException exception) when (exception.SqliteErrorCode == 13)
            { throw new ReportInputLimitException("graph-storage-bytes"); }
        }

        private void RecordPayload(byte[] payload)
        {
            payloadDecodedBytes = checked(payloadDecodedBytes + IndexedGraphPayload.DecodedLength(payload));
            payloadFrameBytes = checked(payloadFrameBytes + payload.Length);
        }

        public async Task AssertInputUnchangedAsync(string inputPath, CancellationToken cancellationToken)
        {
            await using var input = File.OpenRead(inputPath);
            var current = Convert.ToHexString(await SHA256.HashDataAsync(input, cancellationToken)).ToLowerInvariant();
            if (!string.Equals(current, inputSha256, StringComparison.Ordinal))
                throw new InvalidDataException("COMBINED_GRAPH_INPUT_CHANGED");
        }

        public void Sort()
        {
            if (sorted) return;
            storagePhase = "edge-order";
            const string order = "e.rank,n.display_name collate graph_ordinal,e.file_path collate graph_ordinal,e.line,e.id collate graph_ordinal";
            using var command = Command("insert into graph_edge_order select row_number() over (order by " + order
                + "),e.from_id,row_number() over (partition by e.from_id order by " + order
                + "),e.id from graph_edges e join graph_nodes n on n.id=e.to_id;");
            if (Write(command) != EdgeCount) throw new InvalidDataException("COMBINED_GRAPH_ORDER_INCOMPLETE");
            sorted = true;
        }

        public IndexedGraphUsage Observe(GraphOutgoing? outgoing = null)
        {
            using var size = Command("select (select page_count from pragma_page_count) * (select page_size from pragma_page_size);");
            using var generator = Command("select value from graph_metadata where key='generatorSha256';");
            return new IndexedGraphUsage("sqlite-temporary", (string)generator.ExecuteScalar()!, inputSha256,
                NodeCount, EdgeCount, Convert.ToInt64(size.ExecuteScalar()), outgoing?.MaximumRowsLoaded ?? MaximumOutgoingRowsLoaded)
                { StoredFacts = storedFactCount, PayloadEncoding = "length-framed-json-v1",
                    PayloadDecodedBytes = payloadDecodedBytes, PayloadFrameBytes = payloadFrameBytes,
                    MaximumOutgoingDecodedBytesLoaded = MaximumOutgoingDecodedBytesLoaded,
                    StoragePhase = storagePhase, ObjectStorageBytes = ReadObjectStorageBytes(),
                    GlobalEdgePayloadRowsRead = globalEdgePayloadRowsRead,
                    IncomingCountQueries = incomingCountQueries, IncomingReferenceRowsObserved = incomingReferenceRowsObserved,
                    StageElapsedMilliseconds = ObserveStageMilliseconds(), FactPayloadRowsRead = factPayloadRowsRead,
                    FactPayloadBytesRead = factPayloadBytesRead, FactPayloadRowsByStage = new Dictionary<string, long>(factRowsByStage) };
        }

        public IndexedGraphUsage ObserveRefused() => new("sqlite-temporary", generatorSha256, inputSha256,
            NodeCount, EdgeCount, snapshotLogicalBytes, MaximumOutgoingRowsLoaded)
            { StoredFacts = storedFactCount, PayloadEncoding = "length-framed-json-v1",
                PayloadDecodedBytes = payloadDecodedBytes, PayloadFrameBytes = payloadFrameBytes,
                MaximumOutgoingDecodedBytesLoaded = MaximumOutgoingDecodedBytesLoaded,
                StoragePhase = storagePhase, ObjectStorageBytes = snapshotObjectBytes,
                ObservationState = "last-successful-write-snapshot-not-admitted",
                AllocationSnapshotSuccessfulWrites = snapshotWrites, SuccessfulWritesBeforeRefusal = successfulWrites,
                GlobalEdgePayloadRowsRead = globalEdgePayloadRowsRead,
                IncomingCountQueries = incomingCountQueries, IncomingReferenceRowsObserved = incomingReferenceRowsObserved,
                StageElapsedMilliseconds = ObserveStageMilliseconds(), FactPayloadRowsRead = factPayloadRowsRead,
                FactPayloadBytesRead = factPayloadBytesRead, FactPayloadRowsByStage = new Dictionary<string, long>(factRowsByStage) };

        private IReadOnlyDictionary<string, long>? ReadObjectStorageBytes()
        {
            try
            {
                using var command = Command("select name,sum(pgsize) from dbstat group by name order by name collate graph_ordinal limit 64;");
                using var reader = command.ExecuteReader();
                var rows = new SortedDictionary<string, long>(StringComparer.Ordinal);
                while (reader.Read()) rows.Add(reader.GetString(0), reader.GetInt64(1));
                return rows;
            }
            catch (SqliteException exception) when (exception.SqliteErrorCode == 1)
            {
                // The optional dbstat capability can be absent in another SQLite
                // build. Unknown physical allocation is never reported as zero.
                return null;
            }
        }

        public IEnumerable<IReadOnlyList<(GraphNode Node, SymbolAlias Alias)>> SymbolReconciliationGroups()
        {
            foreach (var node in Nodes())
            {
                if (node.NodeKind is not ("Symbol" or "Method" or "Type")) continue;
                var alias = TryCreateSymbolAlias(node.DisplayName);
                if (alias is null) continue;
                storagePhase = "aliases";
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
                    var node = IndexedGraphPayload.Decode<GraphNode>((byte[])reader.GetValue(0));
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
        public bool HasAtLeastIncomingEdges(string id, int minimum) => store?.HasAtLeastIncomingEdges(id, minimum)
            ?? memory.Where(edge => edge.ToNodeId == id).Take(minimum).Count() >= minimum;
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
        public int MaximumRowsLoaded => store?.MaximumOutgoingRowsLoaded ?? 0;
        public bool TryGetValue(string id, out IReadOnlyList<GraphEdge> edges)
        {
            if (store is null)
            {
                var found = memory.TryGetValue(id, out var retained);
                edges = retained!;
                return found;
            }
            edges = store.Outgoing(id);
            return edges.Count != 0;
        }
        public void Add(GraphEdge edge)
        {
            if (!memory.TryGetValue(edge.FromNodeId, out var rows)) memory[edge.FromNodeId] = rows = [];
            rows.Add(edge);
        }
        public IEnumerable<KeyValuePair<string, List<GraphEdge>>> MemoryEntries => memory;
    }

    private sealed class GraphIncoming(IndexedGraphStore? store)
    {
        private readonly Dictionary<string, List<string>> memory = new(StringComparer.Ordinal);
        public IEnumerable<string> Predecessors(string id)
            => store?.Predecessors(id) ?? (memory.TryGetValue(id, out var rows) ? rows : []);
        public void Add(GraphEdge edge)
        {
            if (store is not null) return;
            if (!memory.TryGetValue(edge.ToNodeId, out var rows)) memory[edge.ToNodeId] = rows = [];
            rows.Add(edge.FromNodeId);
        }
    }

    private sealed class ReversedGraphEdges(IReadOnlyList<GraphEdge> edges) : IReadOnlyList<GraphEdge>
    {
        public int Count => edges.Count;
        public GraphEdge this[int index] => (uint)index < (uint)Count
            ? edges[Count - index - 1] : throw new ArgumentOutOfRangeException(nameof(index));
        public IEnumerator<GraphEdge> GetEnumerator()
        { for (var index = 0; index < Count; index++) yield return this[index]; }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
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
        int StoredNodes, int StoredEdges, long? LogicalStorageBytes, int MaximumOutgoingRowsLoaded)
    {
        public int StoredFacts { get; init; }
        public string? PayloadEncoding { get; init; }
        public long? PayloadDecodedBytes { get; init; }
        public long? PayloadFrameBytes { get; init; }
        public long? MaximumOutgoingDecodedBytesLoaded { get; init; }
        public string? StoragePhase { get; init; }
        public IReadOnlyDictionary<string, long>? ObjectStorageBytes { get; init; }
        public string ObservationState { get; init; } = "admitted";
        public long? AllocationSnapshotSuccessfulWrites { get; init; }
        public long? SuccessfulWritesBeforeRefusal { get; init; }
        public IReadOnlyDictionary<string, long>? StageElapsedMilliseconds { get; init; }
        public long? GlobalEdgePayloadRowsRead { get; init; }
        public long? IncomingCountQueries { get; init; }
        public long? IncomingReferenceRowsObserved { get; init; }
        public long FactPayloadRowsRead { get; init; }
        public long FactPayloadBytesRead { get; init; }
        public IReadOnlyDictionary<string, long>? FactPayloadRowsByStage { get; init; }
    }

    private static async Task<IndexedGraphStore> CreateIndexedGraphStoreAsync(string inputPath, long maxStorageBytes, CancellationToken token,
        Action<string>? stageObserver = null)
    {
        await using var input = File.OpenRead(inputPath);
        var inputHash = Convert.ToHexString(await SHA256.HashDataAsync(input, token)).ToLowerInvariant();
        await using var generator = File.OpenRead(typeof(CombinedDependencyPathReporter).Assembly.Location);
        var generatorHash = Convert.ToHexString(await SHA256.HashDataAsync(generator, token)).ToLowerInvariant();
        return new IndexedGraphStore(generatorHash, inputHash, maxStorageBytes, token, stageObserver);
    }
}
