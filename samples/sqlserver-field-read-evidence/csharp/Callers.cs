// Independently authored. Compile and scan only; never invoke methods.
#pragma warning disable CS0618 // Signed legacy provider static compatibility control.
using System;
using System.Data;
using System.Data.SqlClient;
using System.Linq;
internal static class FieldCallers {
// ---- Internal-helper callers in another document: core cells, then caller state cells.
public static object InternalFieldName(string columnName, DataColumn column, bool flag) {
var table = InternalData.ReadShared("dbo.ReadItems");
return table.Rows[0].Field<string>("Name");
}
public static object InternalFieldOrdinal(string columnName, DataColumn column, bool flag) {
var table = InternalData.ReadShared("dbo.ReadItems");
var row = table.Rows[0];
return row.Field<decimal>(2);
}
public static object InternalFieldSet(string columnName, DataColumn column, bool flag) {
var results = InternalData.ReadSharedSet("dbo.ReadItems");
return results.Tables[0].Rows[0].Field<string>("Name");
}
public static object InternalFieldMissing(string columnName, DataColumn column, bool flag) {
var table = InternalData.ReadShared("dbo.ReadItems");
return table.Rows[0].Field<string>("Missing");
}
public static object InternalFieldVersion(string columnName, DataColumn column, bool flag) {
var table = InternalData.ReadShared("dbo.ReadItems");
return table.Rows[0].Field<string>("Name", DataRowVersion.Original);
}
public static object InternalNoField(string columnName, DataColumn column, bool flag) {
var table = InternalData.ReadShared("dbo.ReadItems");
return table.Rows[0]["Name"];
}
}
