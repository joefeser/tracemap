// Independently authored. Compile and scan only; never invoke methods.
#pragma warning disable CS0618 // Signed legacy provider static compatibility control.
using System;
using System.Data;
using System.Data.SqlClient;
// A typed DataSet: its tables are not the fresh-adapter defaults, so it stays outside the subset.
public sealed class SyntheticTypedSet : DataSet { }
public static class DataSetCases {
private static readonly DataSet SharedSet = new DataSet();
private static void Consume(DataSet results) { }
private static void ConsumeTable(DataTable table) { }
private static void Touch(ref DataSet results) { }
// ---- Core cells: compared against JSON, constant DDL and table-backed DDL shapes.
public static object IndexZero(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results);
return results.Tables[0].Rows[0]["Name"];
}
public static object DefaultName(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results);
return results.Tables["Table"].Rows[0]["Id"];
}
public static object SourceTableName(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results, "Items");
return results.Tables["Items"].Rows[0]["Total"];
}
public static object SourceTableIndexZero(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results, "Items");
return results.Tables[0].Rows[0][1];
}
public static object SelectCommandProperty(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter();
adapter.SelectCommand = command;
var results = new DataSet();
adapter.Fill(results);
return results.Tables[0].Rows[0]["Name"];
}
public static object DiscardFill(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
_ = adapter.Fill(results);
return results.Tables[0].Rows[0]["Total"];
}
public static object TableLocal(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results);
var table = results.Tables[0];
return table.Rows[0]["Name"];
}
public static object RowLocalOrdinal(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results);
var row = results.Tables[0].Rows[0];
return row[2];
}
public static object ForeachRows(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results);
object last = null;
foreach (DataRow row in results.Tables[0].Rows) {
var id = row["Id"];
last = row["Total"];
}
return last;
}
public static object TableLocalForeach(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results);
var table = results.Tables[0];
object last = null;
foreach (DataRow row in table.Rows) {
last = row["Name"];
}
return last;
}
public static object TablesCountGuard(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results);
if (results.Tables.Count > 0) {
return results.Tables[0].Rows[0]["Name"];
}
return null;
}
public static object RowsCountGuard(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results);
if (results.Tables[0].Rows.Count > 0) {
return results.Tables[0].Rows[0]["Name"];
}
return null;
}
public static object NestedGuards(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results);
object last = null;
if (results.Tables.Count > 0) {
if (results.Tables[0].Rows.Count > 0) {
foreach (DataRow row in results.Tables[0].Rows) {
last = row[1];
}
}
}
return last;
}
public static object IsNullName(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results);
return results.Tables[0].Rows[0].IsNull("Total");
}
public static object IsNullOrdinal(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results);
var row = results.Tables[0].Rows[0];
return row.IsNull(0);
}
public static object BangName(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results);
var row = results.Tables["Table"].Rows[0];
return row["Name"];
}
public static object UsingLifetime(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
using (var adapter = new SqlDataAdapter(command))
using (var results = new DataSet()) {
adapter.Fill(results);
return results.Tables[0].Rows[0]["Name"];
}
}
public static object WithParameter(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
command.Parameters.AddWithValue("@Id", index);
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results);
return results.Tables[0].Rows[0]["Name"];
}
public static object TwoFills(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results);
var otherCommand = new SqlCommand("dbo.ReadOther");
otherCommand.CommandType = CommandType.StoredProcedure;
var otherAdapter = new SqlDataAdapter(otherCommand);
var otherResults = new DataSet();
otherAdapter.Fill(otherResults);
var name = results.Tables[0].Rows[0]["Name"];
return otherResults.Tables[0].Rows[0]["Title"];
}
public static object MixedSelectors(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results);
var id = results.Tables[0].Rows[0]["Id"];
return results.Tables["Table"].Rows[0]["Name"];
}
public static object ConvertedOrdinal(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results);
return results.Tables[0].Rows[0][(int)1.9];
}
public static object Reordered(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results);
var name = results.Tables[0].Rows[0]["Name"];
return results.Tables[0].Rows[0][2];
}
public static object MissingName(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results);
return results.Tables[0].Rows[0]["Title"];
}
public static object OrdinalOutOfRange(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results);
return results.Tables[0].Rows[0][3];
}
public static object IsNullMissing(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results);
return results.Tables[0].Rows[0].IsNull("Title");
}
public static object Renamed(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results);
return results.Tables[0].Rows[0]["Name"];
}
public static object CaseOnly(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results);
return results.Tables[0].Rows[0]["name"];
}
public static object DuplicateDeclared(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results);
return results.Tables[0].Rows[0]["Name"];
}
public static object DuplicateRenamed(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results);
return results.Tables[0].Rows[0]["Name1"];
}
public static object DynamicName(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results);
return results.Tables[0].Rows[0][columnName];
}
public static object DynamicOrdinal(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results);
return results.Tables[0].Rows[0][index];
}
public static object NonIdentifierName(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results);
return results.Tables[0].Rows[0]["Unit Price"];
}
public static object Incomplete(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results);
return results.Tables[0].Rows[0]["Name"];
}
public static object AbsentBinding(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results);
return results.Tables[0].Rows[0]["Name"];
}
// ---- State cells: unknown with an exact state gap (table-backed DDL only).
public static object FillDataTable(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
return table.Rows[0]["Name"];
}
public static object FillRange(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results, 0, 10, "Table");
return results.Tables[0].Rows[0]["Name"];
}
public static object FillResultUsed(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
var count = adapter.Fill(results);
return results.Tables[0].Rows[0]["Name"];
}
public static object DynamicSourceTable(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results, columnName);
return results.Tables[0].Rows[0]["Name"];
}
public static object NonIdentifierSourceTable(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results, "Item Rows");
return results.Tables[0].Rows[0]["Name"];
}
public static object EmptySourceTable(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results, "");
return results.Tables[0].Rows[0]["Name"];
}
public static object SecondTableIndex(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results);
return results.Tables[1].Rows[0]["Name"];
}
public static object NegativeTableIndex(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results);
return results.Tables[-1].Rows[0]["Name"];
}
public static object DynamicTableIndex(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results);
return results.Tables[index].Rows[0]["Name"];
}
public static object DynamicTableName(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results);
return results.Tables[columnName].Rows[0]["Name"];
}
public static object LaterTableName(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results);
return results.Tables["Table1"].Rows[0]["Name"];
}
public static object CaseOnlyTableName(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results);
return results.Tables["table"].Rows[0]["Name"];
}
public static object SourceNameMismatch(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results, "Items");
return results.Tables["Table"].Rows[0]["Name"];
}
public static object TableNamespace(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results);
return results.Tables["Table", "urn:synthetic"].Rows[0]["Name"];
}
public static object TablesCountUse(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results);
var count = results.Tables.Count;
return results.Tables[0].Rows[0]["Name"];
}
public static object TablesCountWrongGuard(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results);
if (results.Tables.Count > 1) {
return results.Tables[0].Rows[0]["Name"];
}
return null;
}
public static object TablesAdd(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results);
results.Tables.Add("Extra");
return results.Tables[0].Rows[0]["Name"];
}
public static object TablesAlias(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results);
var tables = results.Tables;
return tables[0].Rows[0]["Name"];
}
public static object TablesMember(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results);
var has = results.Tables.Contains("Table");
return results.Tables[0].Rows[0]["Name"];
}
public static object TablesForeach(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results);
object last = null;
foreach (DataTable item in results.Tables) {
last = item.TableName;
}
return last;
}
public static object MergeDataSet(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results);
results.Merge(new DataSet());
return results.Tables[0].Rows[0]["Name"];
}
public static object ReadXml(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results);
results.ReadXml("synthetic.xml");
return results.Tables[0].Rows[0]["Name"];
}
public static object Relations(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results);
var relationSet = results.Relations;
return results.Tables[0].Rows[0]["Name"];
}
public static object EnforceConstraints(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
results.EnforceConstraints = false;
adapter.Fill(results);
return results.Tables[0].Rows[0]["Name"];
}
public static object CaseSensitiveDataSet(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
results.CaseSensitive = true;
adapter.Fill(results);
return results.Tables[0].Rows[0]["Name"];
}
public static object CopyDataSet(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results);
var copy = results.Copy();
return copy.Tables[0].Rows[0]["Name"];
}
public static object DataSetMember(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results);
var xml = results.GetXml();
return results.Tables[0].Rows[0]["Name"];
}
public static object NamedDataSet(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet("Items");
adapter.Fill(results);
return results.Tables[0].Rows[0]["Name"];
}
public static object DataSetInitializer(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet { DataSetName = "Items" };
adapter.Fill(results);
return results.Tables[0].Rows[0]["Name"];
}
public static object DataSetReassigned(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results);
results = new DataSet();
return results.Tables[0].Rows[0]["Name"];
}
public static object DataSetAlias(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results);
var aliased = results;
return aliased.Tables[0].Rows[0]["Name"];
}
public static object DataSetEscape(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results);
Consume(results);
return results.Tables[0].Rows[0]["Name"];
}
public static object RefDataSet(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results);
Touch(ref results);
return results.Tables[0].Rows[0]["Name"];
}
public static object UseBeforeFill(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
var early = results.Tables[0];
adapter.Fill(results);
return early.Rows[0]["Name"];
}
public static object SharedDataSet(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = SharedSet;
adapter.Fill(results);
return results.Tables[0].Rows[0]["Name"];
}
public static object TypedDataSet(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new SyntheticTypedSet();
adapter.Fill(results);
return results.Tables[0].Rows[0]["Name"];
}
public static void HelperFill(SqlDataAdapter adapter, DataSet results) {
adapter.Fill(results);
}
public static object RepeatedFill(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results);
adapter.Fill(results);
return results.Tables[0].Rows[0]["Name"];
}
public static object AdapterReused(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
var otherResults = new DataSet();
adapter.Fill(results);
adapter.Fill(otherResults);
return results.Tables[0].Rows[0]["Name"];
}
public static object FillSchemaFirst(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.FillSchema(results, SchemaType.Source);
adapter.Fill(results);
return results.Tables[0].Rows[0]["Name"];
}
public static object TableMappings(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.TableMappings.Add("Table", "Items");
adapter.Fill(results);
return results.Tables[0].Rows[0]["Name"];
}
public static object MissingSchemaAction(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.MissingSchemaAction = System.Data.MissingSchemaAction.AddWithKey;
adapter.Fill(results);
return results.Tables[0].Rows[0]["Name"];
}
public static object MissingMappingAction(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.MissingMappingAction = System.Data.MissingMappingAction.Error;
adapter.Fill(results);
return results.Tables[0].Rows[0]["Name"];
}
public static object CommandTypeAfterAdapter(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
var adapter = new SqlDataAdapter(command);
command.CommandType = CommandType.StoredProcedure;
var results = new DataSet();
adapter.Fill(results);
return results.Tables[0].Rows[0]["Name"];
}
public static object TextCommand(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.Text;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results);
return results.Tables[0].Rows[0]["Name"];
}
public static object TableLocalReassigned(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results);
var table = results.Tables[0];
table = new DataTable();
return table.Rows[0]["Name"];
}
public static object TableLocalEscape(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results);
var table = results.Tables[0];
ConsumeTable(table);
return table.Rows[0]["Name"];
}
public static object TableColumns(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results);
var columns = results.Tables[0].Columns;
return results.Tables[0].Rows[0]["Name"];
}
public static object TableSelect(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results);
var selected = results.Tables[0].Select();
return selected;
}
public static object LinqRows(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results);
var rows = results.Tables[0].AsEnumerable();
return rows;
}
public static object TableClear(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results);
results.Tables[0].Clear();
return null;
}
public static object RowsAdd(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results);
results.Tables[0].Rows.Add(1, "Synthetic", 2m);
return null;
}
public static object RowWrite(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results);
results.Tables[0].Rows[0]["Name"] = "Synthetic";
return null;
}
public static object FieldExtension(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results);
return results.Tables[0].Rows[0].Field<string>("Name");
}
public static object DynamicRowIndex(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results);
return results.Tables[0].Rows[index]["Name"];
}
public static object RowAlias(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results);
object row = results.Tables[0].Rows[0];
return row;
}
public static object ForeachObjectRow(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results);
object last = null;
foreach (object item in results.Tables[0].Rows) {
last = item;
}
return last;
}
public static object GuardElse(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results);
if (results.Tables.Count > 0) {
return results.Tables[0].Rows[0]["Name"];
} else {
return null;
}
}
public static object TablesAndRowsGuard(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results);
if (results.Tables.Count > 0 && results.Tables[0].Rows.Count > 0) {
return results.Tables[0].Rows[0]["Name"];
}
return null;
}
public static object OtherDataSetGuard(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results);
var other = new DataSet();
if (other.Tables.Count > 0) {
return results.Tables[0].Rows[0]["Name"];
}
return null;
}
public static object TryCatch(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results);
try {
return results.Tables[0].Rows[0]["Name"];
} catch (InvalidOperationException) {
return null;
}
}
public static object NoReads(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var results = new DataSet();
adapter.Fill(results);
return null;
}
}
