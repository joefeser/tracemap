// Independently authored. Compile and scan only; never invoke methods.
#pragma warning disable CS0618 // Signed legacy provider static compatibility control.
using System;
using System.Data;
using System.Data.SqlClient;
internal static class Escapes {
// Method-group and nameof references in another document make each helper unknown.
internal static readonly Func<string, DataTable> Reader = InternalData.CrossDelegateHelper;
internal static readonly string HelperName = nameof(InternalData.CrossNameofHelper);
}
