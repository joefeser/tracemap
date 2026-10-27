// Independently authored. Compile and scan only; never invoke methods.
#pragma warning disable CS0618 // Signed legacy provider static compatibility control.
using System;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
public static class Tables {
private static void Consume(object value) { }
// ---- Core cells: compared against JSON, constant DDL and table-backed DDL shapes.
public static object TableFieldName(string columnName, DataColumn column, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
return table.Rows[0].Field<string>("Name");
}
public static object TableFieldOrdinal(string columnName, DataColumn column, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
return table.Rows[0].Field<int>(0);
}
public static object TableFieldRowLocal(string columnName, DataColumn column, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
var row = table.Rows[0];
return row.Field<int?>("Id");
}
public static object TableFieldForeach(string columnName, DataColumn column, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
if (table.Rows.Count > 0) {
foreach (DataRow row in table.Rows) {
Consume(row.Field<decimal>("Total"));
}
}
return null;
}
public static object TableFieldMixed(string columnName, DataColumn column, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
var row = table.Rows[0];
Consume(row["Id"]);
return row.Field<string>("Name");
}
public static object TableFieldMissing(string columnName, DataColumn column, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
return table.Rows[0].Field<string>("Missing");
}
public static object TableFieldOrdinalOutOfRange(string columnName, DataColumn column, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
return table.Rows[0].Field<string>(3);
}
public static object TableFieldCaseOnly(string columnName, DataColumn column, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
return table.Rows[0].Field<string>("name");
}
public static object TableFieldDynamic(string columnName, DataColumn column, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
return table.Rows[0].Field<string>(columnName);
}
public static object TableFieldRenamed(string columnName, DataColumn column, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
return table.Rows[0].Field<string>("Name");
}
public static object TableFieldIncomplete(string columnName, DataColumn column, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
return table.Rows[0].Field<string>("Name");
}
// ---- State cells (table-backed DDL only): typed unknowns and field-read-absent sites.
public static object TableFieldColumn(string columnName, DataColumn column, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
return table.Rows[0].Field<string>(column);
}
public static object TableFieldVersion(string columnName, DataColumn column, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
return table.Rows[0].Field<string>("Name", DataRowVersion.Original);
}
public static object TableSetField(string columnName, DataColumn column, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
table.Rows[0].SetField("Name", "renamed");
return null;
}
public static object TableFieldAsEnumerable(string columnName, DataColumn column, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
return table.AsEnumerable().First().Field<string>("Name");
}
public static object TableFieldStatic(string columnName, DataColumn column, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
return DataRowExtensions.Field<string>(table.Rows[0], "Name");
}
public static object TableFieldUnbound(string columnName, DataColumn column, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
var other = new DataTable();
return other.Rows[0].Field<string>("Name");
}
public static object TableNoField(string columnName, DataColumn column, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
return table.Rows[0]["Name"];
}
}
