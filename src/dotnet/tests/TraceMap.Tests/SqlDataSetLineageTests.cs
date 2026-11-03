using TraceMap.Core;
using Xunit;
using L = TraceMap.Core.SqlDataSetLineage;

namespace TraceMap.Tests;

public sealed class SqlDataSetLineageTests
{
    private static L.Site Site(int offset) => new("body", new string('a', 64), offset, 1);
    private static L.Table First() => L.Merge(L.Empty("destination"), L.Source("source", "execution-1", ["Name", "Score"], Site(1)), Site(2));

    [Theory]
    [InlineData(true, "application")]
    [InlineData(false, "sql")]
    public void Guarded_overlay_changes_values_not_schema(bool guard, string kind)
    {
        var original = First();
        var auxiliary = L.Source("auxiliary", null, ["Score"], Site(3));
        var updated = L.Write(original, "Score", auxiliary.Columns[0].Values, guard, "enabled", Site(4));
        var score = updated.Columns.Single(c => c.Identity == "Score");
        Assert.Equal("sql", Assert.Single(score.Schema).Kind);
        Assert.Equal(kind, Assert.Single(score.Values).Origin.Kind);
        Assert.Equal("sql", Assert.Single(original.Columns[1].Values).Origin.Kind);
        Assert.Equal("sql", Assert.Single(updated.Columns[0].Values).Origin.Kind);
    }

    [Fact]
    public void Unknown_guard_retains_two_correlated_origins()
    {
        var auxiliary = L.Source("auxiliary", null, ["Score"], Site(3));
        var updated = L.Write(First(), "Score", auxiliary.Columns[0].Values, null, "enabled", Site(4));
        var values = updated.Columns[1].Values;
        Assert.Equal(2, values.Count);
        Assert.Equal(new L.Choice("enabled", false), Assert.Single(values[0].Conditions));
        Assert.Equal("execution-1", values[0].Origin.Execution);
        Assert.Equal(new L.Choice("enabled", true), Assert.Single(values[1].Conditions));
        Assert.Null(values[1].Origin.Execution);
    }

    [Fact]
    public void Clear_remerge_retains_schema_but_never_old_values()
    {
        var old = First();
        var replacement = L.Source("new-source", "execution-2", ["Name"], Site(5));
        var refreshed = L.Merge(L.Clear(old, Site(4)), replacement, Site(6));
        Assert.Equal("destination", refreshed.Allocation);
        Assert.Equal("execution-2", Assert.Single(refreshed.Columns[0].Values).Origin.Execution);
        Assert.Equal("unknown-default", Assert.Single(refreshed.Columns[1].Values).Origin.Kind);
        Assert.Null(refreshed.Columns[1].Values[0].Origin.Execution);
        Assert.Equal("execution-1", Assert.Single(refreshed.Columns[1].Schema).Execution);
        Assert.Equal(new[] { "merge", "clear", "merge" }, refreshed.Events.Select(e => e.Action));
    }

    [Fact]
    public void Independent_destinations_do_not_exchange_executions()
    {
        var first = First();
        var second = L.Merge(L.Empty("destination-2"), L.Source("source-2", "execution-2", ["Count"], Site(3)), Site(4));
        Assert.Equal("execution-1", first.Columns[0].Values[0].Origin.Execution);
        Assert.Equal("execution-2", second.Columns[0].Values[0].Origin.Execution);
        Assert.NotEqual(first.Allocation, second.Allocation);
    }

    [Fact]
    public void Missing_write_does_not_create_a_schema_column()
    {
        var result = L.Write(First(), "Absent", [], true, "enabled", Site(3));
        Assert.Equal("write-column-schema-unavailable", result.Gap);
        Assert.DoesNotContain(result.Columns, c => c.Identity == "Absent");
        Assert.All(result.Columns, c => Assert.Empty(c.Values));
    }

    [Fact]
    public void Existing_rows_merge_and_helper_escape_do_not_claim_preserved_values()
    {
        Assert.Equal("merge-existing-rows", L.Merge(First(), First(), Site(3)).Gap);
        var escaped = L.Unknown(First(), "helper-escape", Site(4));
        Assert.All(escaped.Columns, c => Assert.Empty(c.Values));
        Assert.All(escaped.Columns, c => Assert.NotEmpty(c.Schema));
    }

    [Fact]
    public void Failure_snapshot_preserves_before_and_completed_states_without_rollback_claim()
    {
        var before = First();
        var after = L.Clear(before, Site(4));
        var failure = L.AtFailure("clear", before, after, true);
        Assert.False(failure.Before.RowsCleared);
        Assert.True(failure.Completed!.RowsCleared);
        Assert.True(failure.PartialOperationPossible);
        Assert.NotEmpty(failure.Before.Columns[0].Values);
    }
}
