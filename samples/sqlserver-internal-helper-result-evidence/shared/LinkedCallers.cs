// Independently authored. Compile and scan only; never invoke methods.
#pragma warning disable CS0618 // Signed legacy provider static compatibility control.
using System;
using System.Data;
using System.Data.SqlClient;
// Compiled into csharp/InternalHelpers.csproj through MSBuild Link from outside the project directory.
internal static class LinkedCallers {
public static object UseLinkedCallerHelper() {
var table = InternalData.LinkedCallerHelper("dbo.ReadItems");
return table.Rows[0]["Name"];
}
}
