// Independently authored. Compile and scan only; never invoke methods.
#pragma warning disable CS0618 // Signed legacy provider static compatibility control.
using System.Data;
using System.Data.SqlClient;
public static class ResultCases {
private static void Consume(SqlDataReader reader) { }
public static object NameMatch(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var reader = command.ExecuteReader();
return reader["Name"];
}
public static object MissingName(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var reader = command.ExecuteReader();
return reader["Title"];
}
public static object Renamed(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var reader = command.ExecuteReader();
return reader["Name"];
}
public static object OrdinalValid(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var reader = command.ExecuteReader();
return reader.GetInt32(0);
}
public static object OrdinalOutOfRange(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var reader = command.ExecuteReader();
return reader.GetString(3);
}
public static object Reordered(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var reader = command.ExecuteReader();
var id = reader["Id"];
return reader["Total"];
}
public static object GetOrdinalDirect(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var reader = command.ExecuteReader();
return reader.GetString(reader.GetOrdinal("Name"));
}
public static object GetOrdinalLocal(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var reader = command.ExecuteReader();
var ordinal = reader.GetOrdinal("Total");
return reader.GetDecimal(ordinal);
}
public static object GetOrdinalMissing(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var reader = command.ExecuteReader();
var ordinal = reader.GetOrdinal("Price");
return reader.GetDecimal(ordinal);
}
public static object CaseOnly(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var reader = command.ExecuteReader();
return reader["name"];
}
public static object DuplicateDeclared(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var reader = command.ExecuteReader();
return reader["Name"];
}
public static object DynamicName(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var reader = command.ExecuteReader();
return reader[columnName];
}
public static object DynamicOrdinal(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var reader = command.ExecuteReader();
return reader.GetInt32(index);
}
public static object AbsentBinding(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var reader = command.ExecuteReader();
return reader["Name"];
}
public static object ReaderAlias(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var reader = command.ExecuteReader();
var alias = reader;
return alias["Name"];
}
public static object ReaderReassignment(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var reader = command.ExecuteReader();
var value = reader["Name"];
reader = null;
return value;
}
public static object CommandAlias(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
var alias = command;
command.CommandType = CommandType.StoredProcedure;
var reader = command.ExecuteReader();
return reader["Name"];
}
public static object OrdinalAlias(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var reader = command.ExecuteReader();
var ordinal = reader.GetOrdinal("Name");
var copy = ordinal;
return reader.GetString(copy);
}
public static object OrdinalReassignment(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var reader = command.ExecuteReader();
var ordinal = reader.GetOrdinal("Name");
ordinal = 2;
return reader.GetString(ordinal);
}
public static object HelperEscape(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var reader = command.ExecuteReader();
Consume(reader);
return reader["Name"];
}
public static object TwoReaders(string columnName, int index, bool flag) {
var first = new SqlCommand("dbo.ReadItems");
first.CommandType = CommandType.StoredProcedure;
var second = new SqlCommand("dbo.ReadOther");
second.CommandType = CommandType.StoredProcedure;
var firstReader = first.ExecuteReader();
var secondReader = second.ExecuteReader();
var name = firstReader["Name"];
return secondReader["Title"];
}
public static object ReadLoop(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var reader = command.ExecuteReader();
object last = null;
while (reader.Read()) {
last = reader["Name"];
var id = reader.GetInt32(0);
}
return last;
}
public static object ReadGuard(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var reader = command.ExecuteReader();
if (reader.Read()) {
return reader["Total"];
}
return null;
}
public static object UsingLifetime(string columnName, int index, bool flag) {
using (var command = new SqlCommand("dbo.ReadItems")) {
command.CommandType = CommandType.StoredProcedure;
using (var reader = command.ExecuteReader()) {
return reader["Id"];
}
}
}
public static object NextResult(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var reader = command.ExecuteReader();
reader.NextResult();
return reader["Name"];
}
public static object ScalarChannel(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
return command.ExecuteScalar();
}
public static object NonQueryChannel(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
return command.ExecuteNonQuery();
}
public static object RecordsAffected(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var reader = command.ExecuteReader();
return reader.RecordsAffected;
}
public static object Incomplete(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var reader = command.ExecuteReader();
return reader["Name"];
}
public static object TextCommand(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.Text;
var reader = command.ExecuteReader();
return reader["Name"];
}
public static object Conditional(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
if (flag) command.CommandText = "dbo.ReadOther";
var reader = command.ExecuteReader();
return reader["Name"];
}
public static object WithParameter(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
command.Parameters.Add("@Id", SqlDbType.Int);
var reader = command.ExecuteReader();
return reader["Name"];
}
public static object NullCheck(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var reader = command.ExecuteReader();
return reader.IsDBNull(1);
}
}
