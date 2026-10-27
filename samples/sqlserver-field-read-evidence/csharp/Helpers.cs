// Independently authored. Compile and scan only; never invoke methods.
#pragma warning disable CS0618 // Signed legacy provider static compatibility control.
using System;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
internal static class PrivateData {
// ---- Bounded private helper (Phase 4 origin) and its same-document callers.
private static DataTable ReadPrivate(string procedure) {
var command = new SqlCommand(procedure); // nosemgrep: csharp.lang.security.sqli.csharp-sqli
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
return table;
}
public static object HelperFieldName(string columnName, DataColumn column, bool flag) {
var table = ReadPrivate("dbo.ReadItems");
return table.Rows[0].Field<string>("Name");
}
public static object HelperFieldOtherProcedure(string columnName, DataColumn column, bool flag) {
var table = ReadPrivate("dbo.ReadOther");
return table.Rows[0].Field<string>("Title");
}
public static object HelperFieldMissing(string columnName, DataColumn column, bool flag) {
var table = ReadPrivate("dbo.ReadItems");
return table.Rows[0].Field<string>("Missing");
}
public static object HelperFieldColumn(string columnName, DataColumn column, bool flag) {
var table = ReadPrivate("dbo.ReadItems");
return table.Rows[0].Field<string>(column);
}
// ---- State cell: a Field read inside the helper body does not widen the body.
private static DataTable ReadBody(string procedure) {
var command = new SqlCommand(procedure); // nosemgrep: csharp.lang.security.sqli.csharp-sqli
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
var name = table.Rows[0].Field<string>("Name");
return table;
}
public static object UseReadBody(string columnName, DataColumn column, bool flag) {
var table = ReadBody("dbo.ReadItems");
return table.Rows[0]["Name"];
}
}
internal static class InternalData {
// ---- Bounded internal helpers (Phase 5 origin); every caller is in Callers.
internal static DataTable ReadShared(string procedure) {
var command = new SqlCommand(procedure); // nosemgrep: csharp.lang.security.sqli.csharp-sqli
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
return table;
}
internal static DataSet ReadSharedSet(string procedure) {
var command = new SqlCommand(procedure); // nosemgrep: csharp.lang.security.sqli.csharp-sqli
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results);
return results;
}
}
