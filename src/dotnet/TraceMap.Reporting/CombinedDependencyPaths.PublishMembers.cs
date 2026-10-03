namespace TraceMap.Reporting;

public static partial class CombinedDependencyPathReporter
{
    // Full source/name counts remain global within the admitted source. Only a
    // safely framed declaring type narrows candidate work; opaque identities
    // stay in the fallback roster and retain the historical predicate.
    internal sealed class PublishMemberCandidateIndex
    {
        private readonly Dictionary<(string Source, string? Name), Bucket> buckets = [];

        internal PublishMemberCandidateIndex(IEnumerable<CombinedFactRow> methods)
        {
            foreach (var method in methods)
            {
                var key = (method.SourceIndexId, method.Properties.GetValueOrDefault("metadataName")?.ToUpperInvariant());
                if (!buckets.TryGetValue(key, out var bucket)) buckets.Add(key, bucket = new());
                bucket.Total++;
                var hash = method.Properties.GetValueOrDefault("rawFileSha256");
                if (hash is not null) bucket.ByHash[hash] = bucket.ByHash.GetValueOrDefault(hash) + 1;
                if (!TryIndexedPublishTypePath(method, out var type)) { bucket.Fallback.Add(method); continue; }
                if (!bucket.ByType.TryGetValue(type, out var rows)) bucket.ByType.Add(type, rows = []);
                rows.Add(method);
            }
        }

        internal int CandidateWork(string source, string? name, string typePath)
        {
            var bucket = Find(source, name);
            return bucket is null ? 0 : checked((bucket.ByType.GetValueOrDefault(typePath)?.Count ?? 0) + bucket.Fallback.Count);
        }

        internal PublishMemberSelection Select(string source, string name, string typePath, IReadOnlySet<string?> hashes)
        {
            var bucket = Find(source, name);
            if (bucket is null) return new(0, 0, []);
            var boundCount = hashes.Where(hash => hash is not null).Sum(hash => bucket.ByHash.GetValueOrDefault(hash!));
            var structured = bucket.ByType.GetValueOrDefault(typePath) ?? [];
            var marker = "|type:" + typePath + "|arity:0|method:";
            var qualified = structured.Concat(bucket.Fallback.Where(row =>
                    row.TargetSymbol?.Contains(marker, StringComparison.OrdinalIgnoreCase) == true))
                .Where(row => hashes.Contains(row.Properties.GetValueOrDefault("rawFileSha256"))).ToArray();
            return new(bucket.Total, boundCount, qualified);
        }

        private Bucket? Find(string source, string? name) => buckets.GetValueOrDefault((source, name?.ToUpperInvariant()));
        private sealed class Bucket
        {
            internal int Total;
            internal Dictionary<string, int> ByHash { get; } = new(StringComparer.Ordinal);
            internal Dictionary<string, List<CombinedFactRow>> ByType { get; } = new(StringComparer.OrdinalIgnoreCase);
            internal List<CombinedFactRow> Fallback { get; } = [];
        }
    }

    internal sealed record PublishMemberSelection(int NamedCount, int BoundAssemblyCount,
        IReadOnlyList<CombinedFactRow> QualifiedCandidates);

    private static bool TryIndexedPublishTypePath(CombinedFactRow method, out string typePath)
    {
        typePath = string.Empty;
        var assembly = method.Properties.GetValueOrDefault("assemblyIdentity");
        var identity = method.TargetSymbol;
        if (string.IsNullOrEmpty(assembly) || string.IsNullOrEmpty(identity)
            || assembly.Contains("|type:", StringComparison.OrdinalIgnoreCase)
            || !identity.StartsWith(assembly + "|type:", StringComparison.Ordinal)) return false;
        var position = assembly.Length + "|type:".Length;
        var start = position;
        if (!ConsumeLiteral(identity, ref position, "namespace:")
            || !ReadComponent(identity, ref position, out var ns)
            || !ConsumeLiteral(identity, ref position, "|names:")
            || !ReadComponent(identity, ref position, out var name)
            || ns.Contains('|', StringComparison.Ordinal) || name.Length == 0 || name.Contains('|', StringComparison.Ordinal)) return false;
        var end = position;
        // Generic/nested/malformed and delimiter-bearing identities are not
        // discarded: they retain the name-wide fallback/work accounting.
        if (!ConsumeLiteral(identity, ref position, "|arity:0|method:")
            || !ReadComponent(identity, ref position, out var methodName)
            || methodName.Contains("|type:", StringComparison.OrdinalIgnoreCase)
            || !ConsumeLiteral(identity, ref position, "|")
            || identity.AsSpan(position).Contains("|type:", StringComparison.OrdinalIgnoreCase)) return false;
        typePath = identity[start..end];
        return true;
    }
}
