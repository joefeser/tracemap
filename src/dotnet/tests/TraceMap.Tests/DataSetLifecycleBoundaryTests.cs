using System.Data;
using Xunit;

namespace TraceMap.Tests;

// In-memory behavioral controls, not scanner support or database acceptance.
public sealed class DataSetLifecycleBoundaryTests
{
    [Theory]
    [InlineData(false, 1, 10)]
    [InlineData(true, 9, 10)]
    [InlineData(true, 1, 90)]
    public void Overlay_requires_both_guard_and_matching_row(bool enabled, int key, int expected)
    {
        using var table = Source();
        if (enabled)
            foreach (var row in table.Select($"Id = {key}")) row["Amount"] = 90;
        Assert.Equal(expected, table.Rows[0]["Amount"]);
        Assert.Equal(20, table.Rows[1]["Amount"]);
        Assert.Equal(2, table.Columns.Count);
    }

    [Fact]
    public void Locally_created_destination_imports_columns_but_clear_does_not_preserve_values()
    {
        using var source = Source();
        using var destination = new DataTable("Destination");
        destination.Merge(source);
        Assert.Equal(typeof(int), destination.Columns["Amount"]!.DataType);
        destination.Rows[0]["Amount"] = 90;
        destination.Clear();
        Assert.Equal(2, destination.Columns.Count);
        Assert.Empty(destination.Rows.Cast<DataRow>());
        destination.Merge(source);
        Assert.Equal(10, destination.Rows[0]["Amount"]);
        Assert.Equal(10, source.Rows[0]["Amount"]);
    }

    [Theory]
    [InlineData(0, true, 1, 1, 0, 7)]
    [InlineData(1, false, 0, 0, 0, 0)]
    [InlineData(2, false, 2, 0, 0, 0)]
    [InlineData(3, false, 2, 2, 0, 10)]
    [InlineData(4, false, 2, 2, 0, 10)]
    [InlineData(5, false, 2, 2, 0, 90)]
    [InlineData(6, false, 2, 2, 1, 90)]
    public void Caught_failure_preserves_state_at_its_boundary(
        int stop, bool retainsPrior, int tables, int rows, int projected, int firstValue)
    {
        using var prior = new DataSet();
        prior.Tables.Add(Source());
        prior.Tables[0].Rows[0]["Amount"] = 7;
        // Prior state intentionally differs from freshly fetched values.
        prior.Tables[0].Rows.RemoveAt(1);
        var holder = new Holder { Data = prior };
        var list = new List<int> { 7 };
        using var first = Source();
        using var second = Source();
        var caught = false;
        try
        {
            list.Clear();
            FailAt(0);
            holder.Data = new DataSet();
            FailAt(1);
            holder.Data.Tables.Add("First");
            holder.Data.Tables.Add("Second");
            FailAt(2);
            holder.Data.Tables[0].Merge(first);
            FailAt(3);
            holder.Data.Tables[1].Merge(second);
            FailAt(4);
            holder.Data.Tables[0].Rows[0]["Amount"] = 90;
            FailAt(5);
            list.Add((int)holder.Data.Tables[0].Rows[0]["Amount"]);
            FailAt(6);
        }
        catch (InjectedFailure) { caught = true; }
        try
        {
            Assert.True(caught);
            Assert.Equal(retainsPrior, ReferenceEquals(prior, holder.Data));
            Assert.Equal(tables, holder.Data.Tables.Count);
            Assert.Equal(rows, tables == 0 ? 0 : holder.Data.Tables[0].Rows.Count);
            Assert.Equal(projected, list.Count);
            if (rows > 0) Assert.Equal(firstValue, holder.Data.Tables[0].Rows[0]["Amount"]);
            if (stop >= 3) Assert.Equal(stop >= 4 ? 2 : 0, holder.Data.Tables[1].Rows.Count);
            if (stop >= 5) Assert.Equal(20, holder.Data.Tables[0].Rows[1]["Amount"]);
        }
        finally { if (!ReferenceEquals(prior, holder.Data)) holder.Data.Dispose(); }

        void FailAt(int boundary) { if (boundary == stop) throw new InjectedFailure(); }
    }

    [Fact]
    public void Enclosing_catch_does_not_resume_initialization_after_failed_dependency()
    {
        var laterCallRan = false;
        try
        {
            Fail();
            laterCallRan = true;
        }
        catch (InjectedFailure) { }
        Assert.False(laterCallRan);
        static void Fail() => throw new InjectedFailure();
    }

    private static DataTable Source()
    {
        var table = new DataTable("Result");
        table.Columns.Add("Id", typeof(int));
        table.Columns.Add("Amount", typeof(int));
        table.Rows.Add(1, 10);
        table.Rows.Add(2, 20);
        return table;
    }

    private sealed class Holder { public required DataSet Data { get; set; } }
    private sealed class InjectedFailure : Exception;
}
