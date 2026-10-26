using System.Data;
using Microsoft.Data.SqlClient;
namespace LinkedCSharp;
public static class Cases {
    public static object ReaderName(SqlConnection connection) {
        using var command = new SqlCommand("dbo.ReadItems");
        command.CommandType = CommandType.StoredProcedure;
        command.Connection = connection;
        using var reader = command.ExecuteReader();
        if (reader.Read()) return reader["Name"];
        return null;
    }
    public static object TableName(SqlConnection connection) {
        using var command = new SqlCommand("dbo.ReadItems");
        command.CommandType = CommandType.StoredProcedure;
        command.Connection = connection;
        using var adapter = new SqlDataAdapter(command);
        var table = new DataTable();
        adapter.Fill(table);
        return table.Rows[0]["Name"];
    }
    public static object SetName(SqlConnection connection) {
        using var command = new SqlCommand("dbo.ReadItems");
        command.CommandType = CommandType.StoredProcedure;
        command.Connection = connection;
        using var adapter = new SqlDataAdapter(command);
        var set = new DataSet();
        adapter.Fill(set);
        return set.Tables[0].Rows[0]["Name"];
    }
    public static object MissingName(SqlConnection connection) {
        using var command = new SqlCommand("dbo.ReadItems");
        command.CommandType = CommandType.StoredProcedure;
        command.Connection = connection;
        using var adapter = new SqlDataAdapter(command);
        var table = new DataTable();
        adapter.Fill(table);
        return table.Rows[0]["Missing"];
    }
    public static object CaseOnly(SqlConnection connection) {
        using var command = new SqlCommand("dbo.ReadItems");
        command.CommandType = CommandType.StoredProcedure;
        command.Connection = connection;
        using var adapter = new SqlDataAdapter(command);
        var table = new DataTable();
        adapter.Fill(table);
        return table.Rows[0]["name"];
    }
    public static object LaterTable(SqlConnection connection) {
        using var command = new SqlCommand("dbo.ReadItems");
        command.CommandType = CommandType.StoredProcedure;
        command.Connection = connection;
        using var adapter = new SqlDataAdapter(command);
        var set = new DataSet();
        adapter.Fill(set);
        return set.Tables[1].Rows[0]["Value"];
    }
    // Phase 4: one private static helper level; each call site is bound separately.
    private static DataTable ReadTable(SqlConnection connection, string procedure) {
        using var command = new SqlCommand(procedure);
        command.CommandType = CommandType.StoredProcedure;
        command.Connection = connection;
        using var adapter = new SqlDataAdapter(command);
        var table = new DataTable();
        adapter.Fill(table);
        return table;
    }
    private static DataSet ReadSet(SqlConnection connection, string procedure) {
        using var command = new SqlCommand(procedure);
        command.CommandType = CommandType.StoredProcedure;
        command.Connection = connection;
        using var adapter = new SqlDataAdapter(command);
        var set = new DataSet();
        adapter.Fill(set);
        return set;
    }
    public static object HelperTableName(SqlConnection connection) {
        var table = ReadTable(connection, "dbo.ReadItems");
        return table.Rows[0]["Name"];
    }
    public static object HelperOtherProcedure(SqlConnection connection) {
        var table = ReadTable(connection, "dbo.ReadOther");
        return table.Rows[0]["Title"];
    }
    public static object HelperSetName(SqlConnection connection) {
        var set = ReadSet(connection, "dbo.ReadItems");
        return set.Tables[0].Rows[0]["Name"];
    }
    public static object HelperMissingName(SqlConnection connection) {
        var table = ReadTable(connection, "dbo.ReadItems");
        return table.Rows[0]["Missing"];
    }
    public static object HelperLaterTable(SqlConnection connection) {
        var set = ReadSet(connection, "dbo.ReadItems");
        return set.Tables[1].Rows[0]["Value"];
    }
}
