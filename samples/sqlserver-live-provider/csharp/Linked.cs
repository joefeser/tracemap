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
}
