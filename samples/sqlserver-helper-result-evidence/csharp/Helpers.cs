// Independently authored. Compile and scan only; never invoke methods.
#pragma warning disable CS0618 // Signed legacy provider static compatibility control.
using System;
using System.Data;
using System.Data.SqlClient;
public static class HelperCases {
private static void Consume(object value) { }
private static void ConsumeTable(DataTable table) { }
private static void Touch() { }
// ---- Bounded helpers: one Phase 2/3 Fill shape, command text from the procedure parameter, filled local returned.
private static DataTable ReadTable(string procedure) {
var command = new SqlCommand(procedure);
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
return table;
}
private static DataSet ReadSet(string procedure) {
var command = new SqlCommand(procedure);
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results);
return results;
}
private static DataSet ReadNamedSet(string procedure) {
var command = new SqlCommand(procedure);
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results, "Items");
return results;
}
private static DataTable ReadTableText(string procedure) {
var command = new SqlCommand();
command.CommandText = procedure;
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter();
adapter.SelectCommand = command;
var table = new DataTable();
adapter.Fill(table);
return table;
}
private static DataTable ReadTableUsing(SqlConnection connection, string procedure) {
using var command = new SqlCommand(procedure, connection);
command.CommandType = CommandType.StoredProcedure;
using var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
return table;
}
// ---- Core caller cells: compared against JSON, constant DDL and table-backed DDL shapes.
public static object TableName(string columnName, int index, bool flag) {
var table = ReadTable("dbo.ReadItems");
return table.Rows[0]["Name"];
}
public static object TableOrdinal(string columnName, int index, bool flag) {
var table = ReadTable("dbo.ReadItems");
var row = table.Rows[0];
return row[2];
}
public static object TableForeach(string columnName, int index, bool flag) {
var table = ReadTable("dbo.ReadItems");
foreach (DataRow row in table.Rows) {
Consume(row["Id"]);
}
return null;
}
public static object TableRowsGuard(string columnName, int index, bool flag) {
var table = ReadTable("dbo.ReadItems");
if (table.Rows.Count > 0) {
return table.Rows[0].IsNull("Total");
}
return null;
}
public static object OtherProcedure(string columnName, int index, bool flag) {
var table = ReadTable("dbo.ReadOther");
return table.Rows[0]["Title"];
}
public static object TwoCallers(string columnName, int index, bool flag) {
var items = ReadTable("dbo.ReadItems");
var other = ReadTable("dbo.ReadOther");
return (string)items.Rows[0]["Name"] + (string)other.Rows[0]["Title"];
}
public static object SetIndex(string columnName, int index, bool flag) {
var results = ReadSet("dbo.ReadItems");
return results.Tables[0].Rows[0]["Name"];
}
public static object SetDefaultName(string columnName, int index, bool flag) {
var results = ReadSet("dbo.ReadItems");
return results.Tables["Table"].Rows[0]["Id"];
}
public static object SetSourceName(string columnName, int index, bool flag) {
var results = ReadNamedSet("dbo.ReadItems");
return results.Tables["Items"].Rows[0]["Total"];
}
public static object SetTableLocalGuards(string columnName, int index, bool flag) {
var results = ReadSet("dbo.ReadItems");
if (results.Tables.Count > 0) {
var table = results.Tables[0];
foreach (DataRow row in table.Rows) {
Consume(row["Name"]);
}
}
return null;
}
public static object TextHelper(string columnName, int index, bool flag) {
var table = ReadTableText("dbo.ReadItems");
return table.Rows[0]["Name"];
}
public static object ConnectionHelper(SqlConnection connection) {
var table = ReadTableUsing(connection, "dbo.ReadItems");
return table.Rows[0]["Name"];
}
public static object ConstProcedure(string columnName, int index, bool flag) {
const string procedure = "dbo.ReadItems";
var table = ReadTable(procedure);
return table.Rows[0]["Name"];
}
public static object MissingName(string columnName, int index, bool flag) {
var table = ReadTable("dbo.ReadItems");
return table.Rows[0]["Title"];
}
public static object OrdinalOutOfRange(string columnName, int index, bool flag) {
var table = ReadTable("dbo.ReadItems");
return table.Rows[0][3];
}
public static object OtherProcedureMissing(string columnName, int index, bool flag) {
var table = ReadTable("dbo.ReadOther");
return table.Rows[0]["Name"];
}
public static object SetMissing(string columnName, int index, bool flag) {
var results = ReadSet("dbo.ReadItems");
return results.Tables[0].Rows[0]["Title"];
}
public static object Renamed(string columnName, int index, bool flag) {
var table = ReadTable("dbo.ReadItems");
return table.Rows[0]["Name"];
}
public static object CaseOnly(string columnName, int index, bool flag) {
var table = ReadTable("dbo.ReadItems");
return table.Rows[0]["name"];
}
public static object DynamicName(string columnName, int index, bool flag) {
var table = ReadTable("dbo.ReadItems");
return table.Rows[0][columnName];
}
public static object AbsentBinding(string columnName, int index, bool flag) {
var table = ReadTable("dbo.ReadItems");
return table.Rows[0]["Name"];
}
public static object Incomplete(string columnName, int index, bool flag) {
var table = ReadTable("dbo.ReadItems");
return table.Rows[0]["Name"];
}
// ---- Helper state cells: the whole helper state is unknown with the authored gap (each helper keeps one caller).
public static DataTable PublicHelper(string procedure) {
var command = new SqlCommand(procedure);
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
return table;
}
public static object UsePublicHelper() {
var table = PublicHelper("dbo.ReadItems");
return table.Rows[0]["Name"];
}
internal static DataTable InternalHelper(string procedure) {
var command = new SqlCommand(procedure);
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
return table;
}
public static object UseInternalHelper() {
var table = InternalHelper("dbo.ReadItems");
return table.Rows[0]["Name"];
}
private static DataTable GenericHelper<T>(string procedure) {
var command = new SqlCommand(procedure);
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
return table;
}
public static object UseGenericHelper() {
var table = GenericHelper<int>("dbo.ReadItems");
return table.Rows[0]["Name"];
}
private static DataTable OverloadedHelper(string procedure) {
var command = new SqlCommand(procedure);
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
return table;
}
private static DataTable OverloadedHelper(string procedure, int timeout) {
var command = new SqlCommand(procedure);
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
return table;
}
public static object UseOverloadedHelper() {
var table = OverloadedHelper("dbo.ReadItems");
var other = OverloadedHelper("dbo.ReadItems", 30);
return table.Rows[0]["Name"];
}
private static DataTable ExtensionHelper(this string procedure) {
var command = new SqlCommand(procedure);
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
return table;
}
public static object UseExtensionHelper() {
var procedure = "dbo.ReadItems";
var table = procedure.ExtensionHelper();
return table.Rows[0]["Name"];
}
private static DataTable RefHelper(ref string procedure) {
var command = new SqlCommand(procedure);
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
return table;
}
public static object UseRefHelper() {
var procedure = "dbo.ReadItems";
var table = RefHelper(ref procedure);
return table.Rows[0]["Name"];
}
private static DataTable OptionalHelper(string procedure = "dbo.ReadItems") {
var command = new SqlCommand(procedure);
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
return table;
}
public static object UseOptionalHelper() {
var table = OptionalHelper();
return table.Rows[0]["Name"];
}
private static DataTable TwoStringHelper(string procedure, string other) {
var command = new SqlCommand(procedure);
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
return table;
}
public static object UseTwoStringHelper() {
var table = TwoStringHelper("dbo.ReadItems", "unused");
return table.Rows[0]["Name"];
}
private static object ObjectHelper(string procedure) {
var command = new SqlCommand(procedure);
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
return table;
}
public static object UseObjectHelper() {
var value = ObjectHelper("dbo.ReadItems");
return value;
}
private static DataTable RecursiveHelper(string procedure) {
if (procedure.Length == 0) {
return RecursiveHelper("dbo.ReadItems");
}
var command = new SqlCommand(procedure);
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
return table;
}
public static object UseRecursiveHelper() {
var table = RecursiveHelper("dbo.ReadItems");
return table.Rows[0]["Name"];
}
private static readonly Func<string, DataTable> Reader = DelegateHelper;
private static DataTable DelegateHelper(string procedure) {
var command = new SqlCommand(procedure);
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
return table;
}
public static object UseDelegateHelper() {
var table = DelegateHelper("dbo.ReadItems");
return table.Rows[0]["Name"];
}
public static object DelegateValue() {
return Reader;
}
private static DataTable MutatingHelper(string procedure) {
var command = new SqlCommand(procedure);
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
table.Rows.Clear();
return table;
}
public static object UseMutatingHelper() {
var table = MutatingHelper("dbo.ReadItems");
return table.Rows[0]["Name"];
}
private static DataTable GuardedHelper(string procedure) {
var command = new SqlCommand(procedure);
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
if (procedure.Length > 0) {
adapter.Fill(table);
}
return table;
}
public static object UseGuardedHelper() {
var table = GuardedHelper("dbo.ReadItems");
return table.Rows[0]["Name"];
}
private static DataTable ExecutingHelper(string procedure) {
var command = new SqlCommand(procedure);
command.CommandType = CommandType.StoredProcedure;
command.ExecuteNonQuery();
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
return table;
}
public static object UseExecutingHelper() {
var table = ExecutingHelper("dbo.ReadItems");
return table.Rows[0]["Name"];
}
private static DataTable MultipleReturnHelper(string procedure) {
var command = new SqlCommand(procedure);
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
if (procedure.Length == 0) {
return table;
}
return table;
}
public static object UseMultipleReturnHelper() {
var table = MultipleReturnHelper("dbo.ReadItems");
return table.Rows[0]["Name"];
}
private static DataTable ConstantTextHelper(string procedure) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
return table;
}
public static object UseConstantTextHelper() {
var table = ConstantTextHelper("dbo.ReadOther");
return table.Rows[0]["Name"];
}
private static DataTable ParameterWriteHelper(string procedure) {
procedure = "dbo." + procedure;
var command = new SqlCommand(procedure);
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
return table;
}
public static object UseParameterWriteHelper() {
var table = ParameterWriteHelper("ReadItems");
return table.Rows[0]["Name"];
}
private static DataTable CallingHelper(string procedure) {
Touch();
var command = new SqlCommand(procedure);
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
return table;
}
public static object UseCallingHelper() {
var table = CallingHelper("dbo.ReadItems");
return table.Rows[0]["Name"];
}
private static DataTable DisposedHelper(string procedure) {
var command = new SqlCommand(procedure);
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
using var table = new DataTable();
adapter.Fill(table);
return table;
}
public static object UseDisposedHelper() {
var table = DisposedHelper("dbo.ReadItems");
return table.Rows[0]["Name"];
}
private static DataTable UncalledHelper(string procedure) {
var command = new SqlCommand(procedure);
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
return table;
}
private static DataTable TextCommandHelper(string procedure) {
var command = new SqlCommand(procedure);
command.CommandType = CommandType.Text;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
return table;
}
public static object UseTextCommandHelper() {
var table = TextCommandHelper("SELECT 1 AS Name");
return table.Rows[0]["Name"];
}
public static object InlineFill(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
return table.Rows[0]["Name"];
}
// ---- Caller state cells: each call site is its own entry; the helper stays bounded.
public static object CallerNamedArgument(string columnName, int index, bool flag) {
var table = ReadTable(procedure: "dbo.ReadItems");
return table.Rows[0]["Name"];
}
public static object CallerDynamicProcedure(string columnName, int index, bool flag) {
var table = ReadTable(columnName);
return table.Rows[0]["Name"];
}
public static object CallerNullProcedure(string columnName, int index, bool flag) {
var table = ReadTable(null);
return table.Rows[0]["Name"];
}
public static object CallerDiscarded(string columnName, int index, bool flag) {
ReadTable("dbo.ReadItems");
return null;
}
public static object CallerChained(string columnName, int index, bool flag) {
return ReadTable("dbo.ReadItems").Rows[0]["Name"];
}
public static object CallerPassed(string columnName, int index, bool flag) {
ConsumeTable(ReadTable("dbo.ReadItems"));
return null;
}
public static object CallerObjectLocal(string columnName, int index, bool flag) {
object table = ReadTable("dbo.ReadItems");
return table;
}
public static object CallerRowWrite(string columnName, int index, bool flag) {
var table = ReadTable("dbo.ReadItems");
table.Rows[0]["Name"] = "changed";
return null;
}
public static object CallerRefill(string columnName, int index, bool flag) {
var table = ReadTable("dbo.ReadItems");
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
adapter.Fill(table);
return table.Rows[0]["Name"];
}
public static object CallerReassigned(string columnName, int index, bool flag) {
var table = ReadTable("dbo.ReadItems");
table = ReadTable("dbo.ReadOther");
return table.Rows[0]["Name"];
}
public static object CallerAlias(string columnName, int index, bool flag) {
var table = ReadTable("dbo.ReadItems");
var copy = table;
return copy.Rows[0]["Name"];
}
public static object CallerTryCatch(string columnName, int index, bool flag) {
try {
var table = ReadTable("dbo.ReadItems");
return table.Rows[0]["Name"];
} catch (Exception) {
return null;
}
}
public static object CallerLambda(string columnName, int index, bool flag) {
Func<DataTable> read = () => {
var table = ReadTable("dbo.ReadItems");
return table;
};
return read();
}
public static object CallerLaterTable(string columnName, int index, bool flag) {
var results = ReadSet("dbo.ReadItems");
return results.Tables[1].Rows[0]["Value"];
}
public static object CallerSetMutation(string columnName, int index, bool flag) {
var results = ReadSet("dbo.ReadItems");
results.AcceptChanges();
return results.Tables[0].Rows[0]["Name"];
}
public static object CallerSourceNameMismatch(string columnName, int index, bool flag) {
var results = ReadNamedSet("dbo.ReadItems");
return results.Tables["Table"].Rows[0]["Name"];
}
public static object CallerConnectionArgument(string columnName, int index, bool flag) {
var table = ReadTableUsing(new SqlConnection(), "dbo.ReadItems");
return table.Rows[0]["Name"];
}
public static object CallerNoReads(string columnName, int index, bool flag) {
var table = ReadTable("dbo.ReadItems");
return null;
}
}
// Instance and virtual helpers need a non-static type.
public sealed class InstanceHelpers {
private DataTable InstanceHelper(string procedure) {
var command = new SqlCommand(procedure);
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
return table;
}
public object UseInstanceHelper() {
var table = InstanceHelper("dbo.ReadItems");
return table.Rows[0]["Name"];
}
}
public class VirtualHelpers {
protected virtual DataTable VirtualHelper(string procedure) {
var command = new SqlCommand(procedure);
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
return table;
}
public object UseVirtualHelper() {
var table = VirtualHelper("dbo.ReadItems");
return table.Rows[0]["Name"];
}
}
public static partial class PartialHelpers {
private static DataTable PartialHelper(string procedure) {
var command = new SqlCommand(procedure);
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
return table;
}
}
public static partial class PartialHelpers {
public static object UsePartialHelper() {
var table = PartialHelper("dbo.ReadItems");
return table.Rows[0]["Name"];
}
}
