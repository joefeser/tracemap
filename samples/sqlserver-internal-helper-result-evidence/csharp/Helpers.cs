// Independently authored. Compile and scan only; never invoke methods.
#pragma warning disable CS0618 // Signed legacy provider static compatibility control.
using System;
using System.Data;
using System.Data.SqlClient;
internal static class InternalData {
// ---- Bounded internal helpers: one Phase 2/3 Fill shape, command text from the procedure parameter, filled local returned.
internal static DataTable ReadTable(string procedure) {
var command = new SqlCommand(procedure); // nosemgrep: csharp.lang.security.sqli.csharp-sqli
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
return table;
}
internal static DataSet ReadSet(string procedure) {
var command = new SqlCommand(procedure); // nosemgrep: csharp.lang.security.sqli.csharp-sqli
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results);
return results;
}
internal static DataTable ReadTableText(string procedure) {
var command = new SqlCommand();
command.CommandText = procedure; // nosemgrep: csharp.lang.security.sqli.csharp-sqli
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter();
adapter.SelectCommand = command;
var table = new DataTable();
adapter.Fill(table);
return table;
}
internal static DataTable ReadTableUsing(SqlConnection connection, string procedure) {
using var command = new SqlCommand(procedure, connection); // nosemgrep: csharp.lang.security.sqli.csharp-sqli
command.CommandType = CommandType.StoredProcedure;
using var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
return table;
}
// ---- Same-document caller of an internal helper (core cell).
public static object SameDocumentName(string columnName, int index, bool flag) {
var table = ReadTable("dbo.ReadItems");
return table.Rows[0]["Name"];
}
// ---- Helper state cells: each refused helper keeps its caller(s) elsewhere.
private static DataTable PrivateHelper(string procedure) {
var command = new SqlCommand(procedure); // nosemgrep: csharp.lang.security.sqli.csharp-sqli
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
return table;
}
public static object UsePrivateHelper(string columnName, int index, bool flag) {
var table = PrivateHelper("dbo.ReadItems");
return table.Rows[0]["Name"];
}
public static DataTable PublicHelper(string procedure) {
var command = new SqlCommand(procedure); // nosemgrep: csharp.lang.security.sqli.csharp-sqli
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
return table;
}
internal static DataTable CrossDelegateHelper(string procedure) {
var command = new SqlCommand(procedure); // nosemgrep: csharp.lang.security.sqli.csharp-sqli
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
return table;
}
internal static DataTable CrossNameofHelper(string procedure) {
var command = new SqlCommand(procedure); // nosemgrep: csharp.lang.security.sqli.csharp-sqli
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
return table;
}
internal static DataTable GeneratedCallerHelper(string procedure) {
var command = new SqlCommand(procedure); // nosemgrep: csharp.lang.security.sqli.csharp-sqli
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
return table;
}
internal static DataTable LinkedCallerHelper(string procedure) {
var command = new SqlCommand(procedure); // nosemgrep: csharp.lang.security.sqli.csharp-sqli
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
return table;
}
internal static DataTable UncalledInternalHelper(string procedure) {
var command = new SqlCommand(procedure); // nosemgrep: csharp.lang.security.sqli.csharp-sqli
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
return table;
}
}
