using System.Text.Json;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class SqlShapeExtractorTests
{
    [Fact]
    public void Query_shape_matches_python_v1_golden_fixture()
    {
        foreach (var fixture in ReadFixtures())
        {
            var shape = SqlShapeExtractor.QueryShape(fixture.Sql);

            Assert.Equal(fixture.QueryShapeHash, shape.QueryShapeHash);
            Assert.Equal(fixture.OperationName ?? string.Empty, shape.OperationName);
            Assert.Equal(fixture.TableNames ?? string.Empty, string.Join(';', shape.TableNames));
            Assert.Equal(fixture.ColumnNames ?? string.Empty, string.Join(';', shape.ColumnNames));
            Assert.Equal(fixture.TextHash, FactFactory.Hash(fixture.Sql, 32));
        }
    }

    [Fact]
    public void With_cte_emits_shape_hash_without_operation_or_table_metadata()
    {
        var fixture = ReadFixtures().Single(item => item.Name == "with-cte");
        var props = SqlShapeExtractor.QueryShapeProperties(fixture.Sql, fixture.SqlSourceKind);

        Assert.Equal(fixture.QueryShapeHash, props["queryShapeHash"]);
        Assert.Equal("sql-file", props["sqlSourceKind"]);
        Assert.DoesNotContain("operationName", props.Keys);
        Assert.DoesNotContain("tableName", props.Keys);
        Assert.DoesNotContain("columnNames", props.Keys);
    }

    [Fact]
    public void Is_sql_like_skips_leading_comments_before_checking_first_token()
    {
        Assert.True(SqlTextDetector.IsSqlLike("-- header\nSELECT id FROM orders;"));
        Assert.True(SqlTextDetector.IsSqlLike("/* header */ SELECT id FROM orders;"));
        Assert.False(SqlTextDetector.IsSqlLike("/* unterminated"));
    }

    [Fact]
    public void Unsupported_subquery_table_position_does_not_overclaim_table_metadata()
    {
        var shape = SqlShapeExtractor.QueryShape(ReadUnsupportedSql("subquery-table-position"));

        Assert.Equal("SELECT", shape.OperationName);
        Assert.Empty(shape.TableNames);
        Assert.Equal(new[] { "id" }, shape.ColumnNames);
    }

    private static IReadOnlyList<SqlShapeFixture> ReadFixtures()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(FindRepoRoot(), "samples/sql-shape-fixtures/sql-shape-v1.json")));
        return document.RootElement.GetProperty("cases")
            .EnumerateArray()
            .Select(item => new SqlShapeFixture(
                item.GetProperty("name").GetString()!,
                item.GetProperty("sql").GetString()!,
                item.GetProperty("sqlSourceKind").GetString()!,
                item.GetProperty("textHash").GetString()!,
                item.GetProperty("queryShapeHash").GetString()!,
                item.TryGetProperty("operationName", out var operation) ? operation.GetString() : null,
                item.TryGetProperty("tableNames", out var tables) ? tables.GetString() : null,
                item.TryGetProperty("columnNames", out var columns) ? columns.GetString() : null))
            .ToArray();
    }

    private static string ReadUnsupportedSql(string name)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(FindRepoRoot(), "samples/sql-shape-fixtures/sql-shape-v1.json")));
        return document.RootElement.GetProperty("unsupportedCases")
            .EnumerateArray()
            .Where(item => item.GetProperty("name").GetString() == name)
            .Select(item => item.GetProperty("sql").GetString()!)
            .Single();
    }

    private static string FindRepoRoot()
    {
        var current = AppContext.BaseDirectory;
        while (!string.IsNullOrWhiteSpace(current))
        {
            if (File.Exists(Path.Combine(current, "samples/sql-shape-fixtures/sql-shape-v1.json")))
            {
                return current;
            }

            current = Directory.GetParent(current)?.FullName;
        }

        throw new DirectoryNotFoundException("Unable to find TraceMap repo root.");
    }

    private sealed record SqlShapeFixture(
        string Name,
        string Sql,
        string SqlSourceKind,
        string TextHash,
        string QueryShapeHash,
        string? OperationName,
        string? TableNames,
        string? ColumnNames);
}
