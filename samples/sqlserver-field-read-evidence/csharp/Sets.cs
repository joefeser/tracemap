// Independently authored. Compile and scan only; never invoke methods.
#pragma warning disable CS0618 // Signed legacy provider static compatibility control.
using System;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
public static class Sets {
private static void Consume(object value) { }
// ---- Core cells: the first DataSet table only.
public static object SetFieldName(string columnName, DataColumn column, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results);
return results.Tables[0].Rows[0].Field<string>("Name");
}
public static object SetFieldTableLocal(string columnName, DataColumn column, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results);
var items = results.Tables["Table"];
foreach (DataRow row in items.Rows) {
Consume(row.Field<int>(0));
}
return null;
}
public static object SetFieldMissing(string columnName, DataColumn column, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results);
return results.Tables[0].Rows[0].Field<string>("Missing");
}
// ---- State cell (table-backed DDL only).
public static object SetFieldLaterTable(string columnName, DataColumn column, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results);
return results.Tables[1].Rows[0].Field<int>("Value");
}
}
