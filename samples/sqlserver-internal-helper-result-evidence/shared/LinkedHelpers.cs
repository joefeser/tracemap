// Independently authored. Compile and scan only; never invoke methods.
#pragma warning disable CS0618 // Signed legacy provider static compatibility control.
using System;
using System.Data;
using System.Data.SqlClient;
// Compiled into csharp/InternalHelpers.csproj through MSBuild Link from outside the project directory.
internal static class LinkedHelpers {
internal static DataTable LinkedFileHelper(string procedure) {
var command = new SqlCommand(procedure); // nosemgrep: csharp.lang.security.sqli.csharp-sqli
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
return table;
}
}
