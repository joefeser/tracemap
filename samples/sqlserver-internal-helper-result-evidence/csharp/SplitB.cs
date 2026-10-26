// Independently authored. Compile and scan only; never invoke methods.
#pragma warning disable CS0618 // Signed legacy provider static compatibility control.
using System;
using System.Data;
using System.Data.SqlClient;
internal static partial class Split {
public static object UseSplitPartialHelper() {
var table = SplitPartialHelper("dbo.ReadItems");
return table.Rows[0]["Name"];
}
}
