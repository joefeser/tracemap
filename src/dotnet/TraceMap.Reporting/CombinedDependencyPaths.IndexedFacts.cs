using System.Collections;

namespace TraceMap.Reporting;

public static partial class CombinedDependencyPathReporter
{
    // All admitted rows remain available. This is not a selected-root filter:
    // overload, dispatch and cross-assembly competitors must survive admission.
    internal interface IIndexedCombinedFacts : IReadOnlyList<CombinedFactRow>
    {
        void Add(CombinedFactRow fact);
        IEnumerable<CombinedFactRow> OfTypes(IReadOnlyList<string> types);
        IEnumerable<CombinedFactRow> IdentityOrdered { get; }
        IReadOnlyDictionary<string, CombinedFactRow> ByCombinedId { get; }
        IReadOnlyDictionary<string, CombinedFactRow> BySourceKey { get; }
        IReadOnlyDictionary<(string SourceIndexId, string OriginalFactId), CombinedFactRow[]> ByOriginalId { get; }
    }

    private sealed partial class IndexedGraphStore
    {
        private int storedFactCount;
        private long factPayloadRowsRead;
        private long factPayloadBytesRead;
        private readonly Dictionary<string, long> factRowsByStage = new(StringComparer.Ordinal);
        public IIndexedCombinedFacts Facts { get; }

        private void AddFact(CombinedFactRow fact)
        {
            storagePhase = "facts";
            if (sorted) throw new InvalidOperationException("COMBINED_GRAPH_STORAGE_FROZEN");
            using var command = Command("insert into graph_facts values($ordinal,$id,$source,$original,$key,$type,$payload);", fact.CombinedFactId);
            command.Parameters.AddWithValue("$ordinal", storedFactCount + 1);
            command.Parameters.AddWithValue("$source", fact.SourceIndexId);
            command.Parameters.AddWithValue("$original", fact.OriginalFactId);
            command.Parameters.AddWithValue("$key", SourceFactKey(fact.SourceIndexId, fact.OriginalFactId));
            command.Parameters.AddWithValue("$type", fact.FactType);
            var payload = IndexedGraphPayload.Encode(fact);
            command.Parameters.AddWithValue("$payload", payload);
            Write(command);
            RecordPayload(payload);
            storedFactCount++;
        }

        private IEnumerable<CombinedFactRow> ReadFacts(bool identityOrder = false, IReadOnlyList<string>? types = null,
            bool sourceKeys = false)
        {
            if (types is { Count: 0 }) yield break;
            using var command = Command("select payload from graph_facts f "
                + (sourceKeys ? "where f.id=(select candidate.id from graph_facts candidate where candidate.source_key=f.source_key order by candidate.id collate graph_ordinal limit 1) "
                    : types is null ? "" : "where fact_type in (" + string.Join(",", Enumerable.Range(0, types.Count).Select(index => "$type" + index)) + ") ")
                + "order by "
                + (identityOrder ? "id collate graph_ordinal" : "ordinal") + ";");
            if (types is not null)
                for (var index = 0; index < types.Count; index++) command.Parameters.AddWithValue("$type" + index, types[index]);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                token.ThrowIfCancellationRequested();
                yield return DecodeFact((byte[])reader.GetValue(0));
            }
        }

        private CombinedFactRow FactAt(int index)
        {
            if ((uint)index >= (uint)storedFactCount) throw new ArgumentOutOfRangeException(nameof(index));
            using var command = Command("select payload from graph_facts where ordinal=$ordinal;");
            command.Parameters.AddWithValue("$ordinal", index + 1);
            return DecodeFact((byte[])command.ExecuteScalar()!);
        }

        private int SourceKeyCount()
        {
            using var command = Command("select count(distinct source_key) from graph_facts;");
            return checked((int)Convert.ToInt64(command.ExecuteScalar()));
        }

        private bool HasFact(string id, bool sourceKey = false)
        {
            using var command = Command("select exists(select 1 from graph_facts where " + (sourceKey ? "source_key" : "id") + "=$id);", id);
            return Convert.ToInt64(command.ExecuteScalar()) != 0;
        }

        private bool TryGetFact(string id, out CombinedFactRow fact, bool sourceKey = false)
        {
            using var command = Command("select payload from graph_facts where " + (sourceKey ? "source_key" : "id")
                + "=$id order by id collate graph_ordinal limit 1;", id);
            var payload = command.ExecuteScalar() as byte[];
            fact = payload is null ? null! : DecodeFact(payload);
            return payload is not null;
        }

        private bool TryGetOriginalFact((string SourceIndexId, string OriginalFactId) key, out CombinedFactRow[] facts)
        {
            using var command = Command("select payload from graph_facts where source_id=$source and original_id=$original;");
            command.Parameters.AddWithValue("$source", key.SourceIndexId);
            command.Parameters.AddWithValue("$original", key.OriginalFactId);
            var payload = command.ExecuteScalar() as byte[];
            facts = payload is null ? [] : [DecodeFact(payload)];
            return payload is not null;
        }

        private CombinedFactRow DecodeFact(byte[] payload)
        {
            factPayloadRowsRead = checked(factPayloadRowsRead + 1);
            factPayloadBytesRead = checked(factPayloadBytesRead + payload.LongLength);
            factRowsByStage[observationStage] = checked(factRowsByStage.GetValueOrDefault(observationStage) + 1);
            return IndexedGraphPayload.Decode<CombinedFactRow>(payload);
        }

        private sealed class IndexedFactRows(IndexedGraphStore store) : IIndexedCombinedFacts
        {
            public int Count => store.storedFactCount;
            public CombinedFactRow this[int index] => store.FactAt(index);
            public void Add(CombinedFactRow fact) => store.AddFact(fact);
            public IEnumerable<CombinedFactRow> OfTypes(IReadOnlyList<string> types) => store.ReadFacts(types: types);
            public IEnumerable<CombinedFactRow> IdentityOrdered => store.ReadFacts(identityOrder: true);
            public IReadOnlyDictionary<string, CombinedFactRow> ByCombinedId { get; } = new IndexedFactsById(store);
            public IReadOnlyDictionary<string, CombinedFactRow> BySourceKey { get; } = new IndexedFactsById(store, sourceKey: true);
            public IReadOnlyDictionary<(string SourceIndexId, string OriginalFactId), CombinedFactRow[]> ByOriginalId { get; }
                = new IndexedFactsByOriginalId(store);
            public IEnumerator<CombinedFactRow> GetEnumerator() => store.ReadFacts().GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        private sealed class IndexedFactsById(IndexedGraphStore store, bool sourceKey = false) : IReadOnlyDictionary<string, CombinedFactRow>
        {
            private string Key(CombinedFactRow fact) => sourceKey ? SourceFactKey(fact.SourceIndexId, fact.OriginalFactId) : fact.CombinedFactId;
            public int Count => sourceKey ? store.SourceKeyCount() : store.storedFactCount;
            public IEnumerable<string> Keys => Values.Select(Key);
            public IEnumerable<CombinedFactRow> Values => store.ReadFacts(sourceKeys: sourceKey);
            public CombinedFactRow this[string key] => TryGetValue(key, out var fact) ? fact : throw new KeyNotFoundException();
            public bool ContainsKey(string key) => store.HasFact(key, sourceKey);
            public bool TryGetValue(string key, out CombinedFactRow value) => store.TryGetFact(key, out value, sourceKey);
            public IEnumerator<KeyValuePair<string, CombinedFactRow>> GetEnumerator() => Values
                .Select(fact => new KeyValuePair<string, CombinedFactRow>(Key(fact), fact)).GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        private sealed class IndexedFactsByOriginalId(IndexedGraphStore store)
            : IReadOnlyDictionary<(string SourceIndexId, string OriginalFactId), CombinedFactRow[]>
        {
            public int Count => store.storedFactCount;
            public IEnumerable<(string SourceIndexId, string OriginalFactId)> Keys
                => store.ReadFacts().Select(fact => (fact.SourceIndexId, fact.OriginalFactId));
            public IEnumerable<CombinedFactRow[]> Values => store.ReadFacts().Select(fact => new[] { fact });
            public CombinedFactRow[] this[(string SourceIndexId, string OriginalFactId) key]
                => TryGetValue(key, out var facts) ? facts : throw new KeyNotFoundException();
            public bool ContainsKey((string SourceIndexId, string OriginalFactId) key) => TryGetValue(key, out _);
            public bool TryGetValue((string SourceIndexId, string OriginalFactId) key, out CombinedFactRow[] value)
                => store.TryGetOriginalFact(key, out value);
            public IEnumerator<KeyValuePair<(string SourceIndexId, string OriginalFactId), CombinedFactRow[]>> GetEnumerator()
                => store.ReadFacts().Select(fact => new KeyValuePair<(string SourceIndexId, string OriginalFactId), CombinedFactRow[]>(
                    (fact.SourceIndexId, fact.OriginalFactId), [fact])).GetEnumerator();
            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }

    private static IReadOnlyDictionary<string, CombinedFactRow> CombinedFactsById(IReadOnlyList<CombinedFactRow> facts,
        bool uniqueOnly = false) => facts is IIndexedCombinedFacts indexed ? indexed.ByCombinedId
        : uniqueOnly ? facts.GroupBy(fact => fact.CombinedFactId, StringComparer.Ordinal).Where(group => group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single(), StringComparer.Ordinal)
        : facts.ToDictionary(fact => fact.CombinedFactId, StringComparer.Ordinal);

    private static IReadOnlyDictionary<(string SourceIndexId, string OriginalFactId), CombinedFactRow[]> CombinedFactsByOriginalId(
        IReadOnlyList<CombinedFactRow> facts) => facts is IIndexedCombinedFacts indexed ? indexed.ByOriginalId
        : facts.GroupBy(fact => (fact.SourceIndexId, fact.OriginalFactId)).ToDictionary(group => group.Key, group => group.ToArray());

    private static IEnumerable<CombinedFactRow> IdentityOrderedFacts(IReadOnlyList<CombinedFactRow> facts)
        => facts is IIndexedCombinedFacts indexed ? indexed.IdentityOrdered
            : facts.OrderBy(fact => fact.CombinedFactId, StringComparer.Ordinal);

    internal static IEnumerable<CombinedFactRow> FactsOfTypes(IReadOnlyList<CombinedFactRow> facts, params string[] types)
        => facts is IIndexedCombinedFacts indexed ? indexed.OfTypes(types)
            : facts.Where(fact => types.Contains(fact.FactType, StringComparer.Ordinal));

    private static IReadOnlyDictionary<string, CombinedFactRow> CombinedFactsBySourceKey(IReadOnlyList<CombinedFactRow> facts)
        => facts is IIndexedCombinedFacts indexed ? indexed.BySourceKey
            : facts.GroupBy(fact => SourceFactKey(fact.SourceIndexId, fact.OriginalFactId), StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.OrderBy(fact => fact.CombinedFactId, StringComparer.Ordinal).First(), StringComparer.Ordinal);
}
