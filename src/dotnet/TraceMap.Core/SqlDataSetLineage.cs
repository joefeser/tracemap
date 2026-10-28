namespace TraceMap.Core;

/// <summary>
/// Internal abstract state for the compiled lifecycle interpreter. Not an admission
/// API or serialized evidence contract: callers must supply independently verified
/// instruction sites. Schema ancestry is deliberately separate from value ancestry.
/// </summary>
internal static class SqlDataSetLineage
{
    internal const int MaxColumns = 256, MaxAlternatives = 32, MaxEvents = 256;
    internal sealed record Site(string Body, string Binary, long Offset, int Frame);
    internal sealed record Origin(string Kind, string Allocation, string? Execution, string Column, Site Site);
    internal sealed record Choice(string Predicate, bool WhenTrue);
    internal sealed record Alternative(Origin Origin, IReadOnlyList<Choice> Conditions);
    internal sealed record Column(string Identity, IReadOnlyList<Origin> Schema,
        IReadOnlyList<Alternative> Values);
    internal sealed record Event(int Sequence, string Action, Site Site);
    internal sealed record Table(string Allocation, IReadOnlyList<Column> Columns,
        IReadOnlyList<Event> Events, bool RowsCleared, string? Gap);
    internal sealed record Failure(string Boundary, Table Before, Table? Completed,
        bool PartialOperationPossible);

    internal static Table Empty(string allocation) => new(allocation, [], [], true, null);

    internal static Table Source(string allocation, string? execution, IEnumerable<string> columns, Site site)
    {
        var names = columns.ToArray();
        if (names.Length > MaxColumns || names.Distinct(StringComparer.Ordinal).Count() != names.Length)
            throw new ArgumentException("lineage-source-columns-invalid");
        return new(allocation, names.Select(name =>
        {
            var origin = new Origin(execution is null ? "application" : "sql", allocation, execution, name, site);
            return new Column(name, new[] { origin }, new[] { new Alternative(origin, []) });
        }).ToArray(), [new(0, "source", site)], false, null);
    }

    // Only empty-row destinations are admitted. A populated Merge needs key and
    // preserveChanges semantics, which this operation does not guess.
    internal static Table Merge(Table target, Table source, Site site)
    {
        if (target.Gap is not null || source.Gap is not null) return Unknown(target, "merge-input-unknown", site);
        if (!target.RowsCleared) return Unknown(target, "merge-existing-rows", site);
        var columns = new List<Column>();
        foreach (var name in target.Columns.Select(c => c.Identity).Concat(source.Columns.Select(c => c.Identity)).Distinct(StringComparer.Ordinal))
        {
            var old = target.Columns.SingleOrDefault(c => c.Identity == name);
            var incoming = source.Columns.SingleOrDefault(c => c.Identity == name);
            // A retained column absent from the new result has provider/default
            // semantics, not values from its previous execution.
            var values = incoming?.Values ?? new[] { new Alternative(
                new Origin("unknown-default", target.Allocation, null, name, site), []) };
            columns.Add(new(name, (old?.Schema ?? []).Concat(incoming?.Schema ?? []).Distinct().ToArray(), values));
        }
        if (columns.Count > MaxColumns) return Unknown(target, "column-limit", site);
        return Record(target with { Columns = columns.ToArray(), RowsCleared = source.RowsCleared }, "merge", site);
    }

    internal static Table Clear(Table table, Site site)
        => Record(table with { Columns = table.Columns.Select(c => c with { Values = [] }).ToArray(), RowsCleared = true }, "clear", site);

    internal static Table Write(Table table, string column, IReadOnlyList<Alternative> replacement,
        bool? guard, string predicate, Site site)
    {
        if (table.Gap is not null) return table;
        var selected = table.Columns.SingleOrDefault(c => c.Identity == column);
        if (selected is null) return Unknown(table, "write-column-schema-unavailable", site);
        if (guard == false || table.RowsCleared) return Record(table, "write-skipped", site);
        IReadOnlyList<Alternative> values = replacement.ToArray();
        if (guard is null)
        {
            if (string.IsNullOrWhiteSpace(predicate)) throw new ArgumentException("lineage-predicate-required");
            values = Constrain(selected.Values, predicate, false).Concat(Constrain(values, predicate, true)).ToArray();
        }
        if (values.Count > MaxAlternatives) return Unknown(table, "alternative-limit", site);
        return Record(table with { Columns = table.Columns.Select(c => c.Identity == column ? c with { Values = values } : c).ToArray() }, "write", site);
    }

    private static IEnumerable<Alternative> Constrain(IEnumerable<Alternative> values, string predicate, bool truth)
    {
        foreach (var value in values)
        {
            if (value.Conditions.Any(c => c.Predicate == predicate && c.WhenTrue != truth)) continue;
            yield return value with { Conditions = value.Conditions.Append(new(predicate, truth)).Distinct().ToArray() };
        }
    }

    internal static Table Unknown(Table table, string reason, Site site)
        => Record(table with { Gap = reason, Columns = table.Columns.Select(c => c with { Values = [] }).ToArray() }, "unknown-effect", site);

    // Captures both states without inventing rollback or treating an operation
    // boundary as proof that an in-progress operation is atomic.
    internal static Failure AtFailure(string boundary, Table before, Table? completed, bool partialOperationPossible)
        => new(boundary, before, completed, partialOperationPossible);

    private static Table Record(Table table, string action, Site site)
    {
        if (table.Events.Count >= MaxEvents) return table with { Gap = "event-limit", Columns = table.Columns.Select(c => c with { Values = [] }).ToArray() };
        return table with { Events = table.Events.Append(new(table.Events.Count, action, site)).ToArray() };
    }
}
