// Independently authored. Compile and scan only; never invoke methods.
#pragma warning disable CS0618 // Signed legacy provider static compatibility control.
using System;
using System.Data;
using System.Data.SqlClient;
internal static class MoreCallers {
public static object OtherTypeCaller(string columnName, int index, bool flag) {
var table = InternalData.ReadTable("dbo.ReadItems");
return table.Rows[0]["Id"];
}
}
