// Independently authored. Compile and scan only; never invoke methods.
#pragma warning disable CS0618 // Signed legacy provider static compatibility control.
using System;
using System.Data;
using System.Data.SqlClient;
internal static class Callers {
private static void Consume(object value) { }
// ---- Core caller cells and caller state cells in another document.
public static object CrossTableName(string columnName, int index, bool flag) {
var table = InternalData.ReadTable("dbo.ReadItems");
return table.Rows[0]["Name"];
}
public static object CrossTableOrdinal(string columnName, int index, bool flag) {
var table = InternalData.ReadTable("dbo.ReadItems");
var row = table.Rows[0];
return row[2];
}
public static object CrossTableForeach(string columnName, int index, bool flag) {
var table = InternalData.ReadTable("dbo.ReadItems");
foreach (DataRow row in table.Rows) {
Consume(row["Id"]);
}
return null;
}
public static object CrossOtherProcedure(string columnName, int index, bool flag) {
var table = InternalData.ReadTable("dbo.ReadOther");
return table.Rows[0]["Title"];
}
public static object CrossTwoCallers(string columnName, int index, bool flag) {
var items = InternalData.ReadTable("dbo.ReadItems");
var other = InternalData.ReadTable("dbo.ReadOther");
return (string)items.Rows[0]["Name"] + (string)other.Rows[0]["Title"];
}
public static object CrossSetIndex(string columnName, int index, bool flag) {
var results = InternalData.ReadSet("dbo.ReadItems");
return results.Tables[0].Rows[0]["Name"];
}
public static object CrossSetGuards(string columnName, int index, bool flag) {
var results = InternalData.ReadSet("dbo.ReadItems");
if (results.Tables.Count > 0) {
var table = results.Tables[0];
foreach (DataRow row in table.Rows) {
Consume(row["Name"]);
}
}
return null;
}
public static object CrossTextHelper(string columnName, int index, bool flag) {
var table = InternalData.ReadTableText("dbo.ReadItems");
return table.Rows[0]["Name"];
}
public static object CrossConnectionHelper(SqlConnection connection) {
var table = InternalData.ReadTableUsing(connection, "dbo.ReadItems");
return table.Rows[0]["Name"];
}
public static object CrossMissingName(string columnName, int index, bool flag) {
var table = InternalData.ReadTable("dbo.ReadItems");
return table.Rows[0]["Title"];
}
public static object CrossOrdinalOutOfRange(string columnName, int index, bool flag) {
var table = InternalData.ReadTable("dbo.ReadItems");
return table.Rows[0][3];
}
public static object CrossOtherProcedureMissing(string columnName, int index, bool flag) {
var table = InternalData.ReadTable("dbo.ReadOther");
return table.Rows[0]["Name"];
}
public static object CrossRenamed(string columnName, int index, bool flag) {
var table = InternalData.ReadTable("dbo.ReadItems");
return table.Rows[0]["Name"];
}
public static object CrossCaseOnly(string columnName, int index, bool flag) {
var table = InternalData.ReadTable("dbo.ReadItems");
return table.Rows[0]["name"];
}
public static object CrossDynamicName(string columnName, int index, bool flag) {
var table = InternalData.ReadTable("dbo.ReadItems");
return table.Rows[0][columnName];
}
public static object CrossAbsentBinding(string columnName, int index, bool flag) {
var table = InternalData.ReadTable("dbo.ReadItems");
return table.Rows[0]["Name"];
}
public static object CrossIncomplete(string columnName, int index, bool flag) {
var table = InternalData.ReadTable("dbo.ReadItems");
return table.Rows[0]["Name"];
}
public static object CrossNamedArgument(string columnName, int index, bool flag) {
var table = InternalData.ReadTable(procedure: "dbo.ReadItems");
return table.Rows[0]["Name"];
}
public static object CrossDynamicProcedure(string columnName, int index, bool flag) {
var table = InternalData.ReadTable(columnName);
return table.Rows[0]["Name"];
}
public static object CrossDiscarded(string columnName, int index, bool flag) {
InternalData.ReadTable("dbo.ReadItems");
return null;
}
public static object CrossRowWrite(string columnName, int index, bool flag) {
var table = InternalData.ReadTable("dbo.ReadItems");
table.Rows[0]["Name"] = "changed";
return null;
}
public static object CrossAlias(string columnName, int index, bool flag) {
var table = InternalData.ReadTable("dbo.ReadItems");
var copy = table;
return copy.Rows[0]["Name"];
}
public static object CrossTryCatch(string columnName, int index, bool flag) {
try {
var table = InternalData.ReadTable("dbo.ReadItems");
return table.Rows[0]["Name"];
} catch (Exception) {
return null;
}
}
public static object CrossLaterTable(string columnName, int index, bool flag) {
var results = InternalData.ReadSet("dbo.ReadItems");
return results.Tables[1].Rows[0]["Value"];
}
public static object CrossNoReads(string columnName, int index, bool flag) {
var table = InternalData.ReadTable("dbo.ReadItems");
return null;
}
// ---- Callers of refused helpers (no caller entries).
public static object UsePublicHelper() {
var table = InternalData.PublicHelper("dbo.ReadItems");
return table.Rows[0]["Name"];
}
public static object UseCrossDelegateHelper() {
var table = InternalData.CrossDelegateHelper("dbo.ReadItems");
return table.Rows[0]["Name"];
}
public static object UseCrossNameofHelper() {
var table = InternalData.CrossNameofHelper("dbo.ReadItems");
return table.Rows[0]["Name"];
}
public static object UseLinkedFileHelper() {
var table = LinkedHelpers.LinkedFileHelper("dbo.ReadItems");
return table.Rows[0]["Name"];
}
}
