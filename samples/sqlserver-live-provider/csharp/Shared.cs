using System.Data;
using Microsoft.Data.SqlClient;
namespace LinkedCSharp;
// Phase 6: one internal static helper in its own document; its call site in Linked.cs is enumerated across the compilation.
// The SQLi suppression below covers an internal StoredProcedure helper called only with the fixed dbo.ReadItems name in
// this disposable synthetic harness, not SQL text.
internal static class SharedData {
    internal static DataTable ReadShared(SqlConnection connection, string procedure) {
        using var command = new SqlCommand(procedure); // nosemgrep: csharp.lang.security.sqli.csharp-sqli
        command.CommandType = CommandType.StoredProcedure;
        command.Connection = connection;
        using var adapter = new SqlDataAdapter(command);
        var table = new DataTable();
        adapter.Fill(table);
        return table;
    }
}
