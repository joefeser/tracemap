using System.Data;
using Microsoft.Data.SqlClient;

// Opt-in runtime oracle, never invoked by ordinary unit tests. Fixed loopback and
// synthetic database only. The harness injects a transient secret without argv.
var password = Environment.GetEnvironmentVariable("SQLCMDPASSWORD")
    ?? throw new InvalidOperationException("Disposable credential required");
var cs = new SqlConnectionStringBuilder {
    DataSource = "tcp:127.0.0.1,1433", InitialCatalog = "TraceMapSynthetic908",
    UserID = "sa", Password = password, Encrypt = true,
    TrustServerCertificate = true, ConnectTimeout = 5, Pooling = false
};
using var connection = new SqlConnection(cs.ConnectionString);
connection.Open();
var count = 0;
void Check(bool condition, string name) {
    if (!condition) throw new InvalidOperationException("Failed: " + name);
    Console.WriteLine("PASS " + name); count++;
}
SqlCommand Command(string name) => new(name, connection) {
    CommandType = CommandType.StoredProcedure, CommandTimeout = 20
};
SqlCommand Read(string name = "dbo.ReadItem") {
    var command = Command(name);
    command.Parameters.Add("@Name", SqlDbType.NVarChar, 20).Value = "sample";
    return command;
}
// Fixed public data; no user SQL or schema admission.
using (var seed = new SqlCommand("DELETE FROM dbo.Items; INSERT dbo.Items VALUES (1,N'sample');", connection))
    seed.ExecuteNonQuery();
using (var command = Read())
using (var reader = command.ExecuteReader()) {
    Check(reader.Read(), "reader-row");
    Check(reader.GetInt32(0) == 1 && (string)reader["Name"] == "sample", "reader-name-and-ordinal");
    Check(reader.GetOrdinal("name") == 1, "reader-case-fallback-runtime-only");
    var missing = false;
    try { reader.GetOrdinal("Missing"); } catch (IndexOutOfRangeException) { missing = true; }
    Check(missing, "reader-missing-column-refuses");
    var ordinal = false;
    try { reader.GetValue(2); } catch (IndexOutOfRangeException) { ordinal = true; }
    Check(ordinal, "reader-invalid-ordinal-refuses");
    Check(!reader.NextResult(), "reader-single-result");
}
using (var command = Read("dbo.RenamedItem"))
using (var adapter = new SqlDataAdapter(command)) {
    var table = new DataTable(); adapter.Fill(table);
    Check(table.Rows.Count == 1 && table.Columns.Contains("DisplayName") && !table.Columns.Contains("Name"), "renamed-projection");
    Check((string)table.Rows[0]["displayname"] == "sample", "datatable-case-fallback-runtime-only");
    var missing = false;
    try { _ = table.Rows[0]["Name"]; } catch (ArgumentException) { missing = true; }
    Check(missing, "datatable-renamed-read-refuses");
}
using (var command = Read())
using (var adapter = new SqlDataAdapter(command)) {
    var table = new DataTable(); adapter.Fill(table);
    Check((int)table.Rows[0][0] == 1 && (string)table.Rows[0]["Name"] == "sample", "datatable-fill");
    var set = new DataSet(); adapter.Fill(set);
    Check(set.Tables.Count == 1 && set.Tables[0].TableName == "Table", "dataset-default-table");
    Check((string)set.Tables["Table"]!.Rows[0]["Name"] == "sample", "dataset-name-read");
    var named = new DataSet(); adapter.Fill(named, "Src");
    Check(named.Tables.Count == 1 && named.Tables[0].TableName == "Src", "dataset-source-table-name");
}
using (var command = Command("dbo.Channels")) {
    var output = command.Parameters.Add("@Count", SqlDbType.Int); output.Direction = ParameterDirection.Output;
    var returned = command.Parameters.Add("@return", SqlDbType.Int); returned.Direction = ParameterDirection.ReturnValue;
    using (var reader = command.ExecuteReader()) {
        Check(reader.Read() && reader.GetName(1) == "Name", "channels-first-result");
        Check(reader.NextResult() && reader.Read() && reader.GetInt32(0) == 1, "channels-second-result");
        Check(!reader.NextResult(), "channels-result-count");
    }
    Check((int)output.Value == 1 && (int)returned.Value == 7, "output-and-return-distinct");
    using var adapter = new SqlDataAdapter(command);
    var set = new DataSet(); adapter.Fill(set, "Src");
    Check(set.Tables.Count == 2 && set.Tables[0].TableName == "Src" && set.Tables[1].TableName == "Src1", "dataset-multiple-results-runtime-only");
}
using (var command = new SqlCommand("SELECT 1 AS Name, 2 AS Name;", connection))
using (var adapter = new SqlDataAdapter(command)) {
    var table = new DataTable(); adapter.Fill(table);
    Check(table.Columns[0].ColumnName == "Name" && table.Columns[1].ColumnName == "Name1", "duplicate-column-renaming-runtime-only");
}
Check(count == 19, "case-denominator");
using (var setup = new SqlCommand("CREATE PROCEDURE dbo.ReadItems AS SELECT 1 AS Id, N'sample' AS Name, 2 AS Total; SELECT 99 AS Value;", connection))
    setup.ExecuteNonQuery();
foreach (var (language, type) in new[] { ("csharp", typeof(LinkedCSharp.Cases)), ("vb", typeof(LinkedVisualBasic.Cases)) }) {
    foreach (var method in new[] { "ReaderName", "TableName", "SetName", "MissingName", "CaseOnly", "LaterTable" }) {
        object? value = null;
        var missing = false;
        try { value = type.GetMethod(method)!.Invoke(null, [connection]); }
        catch (System.Reflection.TargetInvocationException error) when (method == "MissingName" && error.InnerException is ArgumentException) { missing = true; }
        Check(method == "MissingName" ? missing : method == "LaterTable" ? Equals(value, 99) : Equals(value, "sample"), "linked-" + language + "-" + method);
    }
}
if (count != 32) throw new InvalidOperationException("Linked denominator mismatch");
Console.WriteLine("PROVIDER_CHECKS_PASSED 32");
