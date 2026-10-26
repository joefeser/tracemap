// Independently authored. Compile and scan only; never invoke methods.
#pragma warning disable CS0618 // Signed legacy provider static compatibility control.
using System;
using System.Data;
using System.Data.SqlClient;
public static class DataTableCases {
private static void Consume(DataTable table) { }
private static void Touch(ref DataTable table) { }
// ---- Core cells: compared against JSON, constant DDL and table-backed DDL shapes.
public static object CtorName(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
return table.Rows[0]["Name"];
}
public static object SelectCommandProperty(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter();
adapter.SelectCommand = command;
var table = new DataTable();
adapter.Fill(table);
return table.Rows[0]["Id"];
}
public static object DiscardFill(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
_ = adapter.Fill(table);
return table.Rows[0]["Total"];
}
public static object RowLocalOrdinal(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
var row = table.Rows[0];
return row[2];
}
public static object ForeachRows(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
object last = null;
foreach (DataRow row in table.Rows) {
var id = row["Id"];
last = row["Total"];
}
return last;
}
public static object CountGuard(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
if (table.Rows.Count > 0) {
return table.Rows[0]["Name"];
}
return null;
}
public static object CountGuardForeach(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
object last = null;
if (table.Rows.Count > 0) {
foreach (DataRow row in table.Rows) {
last = row[1];
}
}
return last;
}
public static object IsNullName(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
return table.Rows[0].IsNull("Total");
}
public static object IsNullOrdinal(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
var row = table.Rows[0];
return row.IsNull(0);
}
public static object BangName(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
var row = table.Rows[0];
return row["Name"];
}
public static object UsingLifetime(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
using (var adapter = new SqlDataAdapter(command))
using (var table = new DataTable()) {
adapter.Fill(table);
return table.Rows[0]["Name"];
}
}
public static object WithParameter(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
command.Parameters.AddWithValue("@Id", index);
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
return table.Rows[0]["Name"];
}
public static object TwoFills(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
var otherCommand = new SqlCommand("dbo.ReadOther");
otherCommand.CommandType = CommandType.StoredProcedure;
var otherAdapter = new SqlDataAdapter(otherCommand);
var otherTable = new DataTable();
otherAdapter.Fill(otherTable);
var name = table.Rows[0]["Name"];
return otherTable.Rows[0]["Title"];
}
public static object ConvertedOrdinal(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
return table.Rows[0][(int)1.9];
}
public static object MissingName(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
return table.Rows[0]["Title"];
}
public static object OrdinalOutOfRange(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
return table.Rows[0][3];
}
public static object IsNullMissing(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
return table.Rows[0].IsNull("Price");
}
public static object Renamed(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
return table.Rows[0]["Name"];
}
public static object Reordered(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
var id = table.Rows[0]["Id"];
return table.Rows[0]["Total"];
}
public static object CaseOnly(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
return table.Rows[0]["name"];
}
public static object DuplicateDeclared(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
return table.Rows[0]["Name"];
}
public static object DuplicateRenamed(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
return table.Rows[0]["Name1"];
}
public static object DynamicName(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
return table.Rows[0][columnName];
}
public static object DynamicOrdinal(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
return table.Rows[0][index];
}
public static object NonIdentifierName(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
return table.Rows[0]["Unit Price"];
}
public static object Incomplete(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
return table.Rows[0]["Name"];
}
public static object AbsentBinding(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
return table.Rows[0]["Name"];
}
// ---- State cells: every one is unknown with an exact state gap.
public static object TextCommand(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.Text;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
return table.Rows[0]["Name"];
}
public static object NoCommandType(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
return table.Rows[0]["Name"];
}
public static object CommandTypeAfterAdapter(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
var adapter = new SqlDataAdapter(command);
command.CommandType = CommandType.StoredProcedure;
var table = new DataTable();
adapter.Fill(table);
return table.Rows[0]["Name"];
}
public static object ParameterAfterAdapter(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
command.Parameters.AddWithValue("@Id", index);
var table = new DataTable();
adapter.Fill(table);
return table.Rows[0]["Name"];
}
public static object CommandAlias(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var alias = command;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
return table.Rows[0]["Name"];
}
public static object CommandExecuted(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
command.ExecuteNonQuery();
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
return table.Rows[0]["Name"];
}
public static object ConditionalCommandText(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
if (flag) {
command.CommandText = "dbo.ReadOther";
}
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
return table.Rows[0]["Name"];
}
public static object TextAdapterConstructor(string columnName, int index, bool flag) {
var connection = new SqlConnection();
var adapter = new SqlDataAdapter("dbo.ReadItems", connection);
var table = new DataTable();
adapter.Fill(table);
return table.Rows[0]["Name"];
}
public static object AdapterInitializer(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter { SelectCommand = command };
var table = new DataTable();
adapter.Fill(table);
return table.Rows[0]["Name"];
}
public static object SelectCommandTwice(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
adapter.SelectCommand = command;
var table = new DataTable();
adapter.Fill(table);
return table.Rows[0]["Name"];
}
public static object SelectCommandUnbound(string columnName, int index, bool flag) {
var adapter = new SqlDataAdapter();
var table = new DataTable();
adapter.Fill(table);
return table.Rows[0]["Name"];
}
public static object SelectCommandAfterFill(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter();
var table = new DataTable();
adapter.Fill(table);
adapter.SelectCommand = command;
return table.Rows[0]["Name"];
}
public static object SelectCommandViaAdapter(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
adapter.SelectCommand.CommandText = "dbo.ReadOther";
var table = new DataTable();
adapter.Fill(table);
return table.Rows[0]["Name"];
}
public static object FillDataSet(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var set = new DataSet();
adapter.Fill(set);
return set.Tables[0].Rows[0]["Name"];
}
public static object FillDataSetTable(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var set = new DataSet();
adapter.Fill(set, "Items");
return set.Tables[0].Rows[0]["Name"];
}
public static object FillRange(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(0, 10, table);
return table.Rows[0]["Name"];
}
public static object FillResultUsed(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
var count = adapter.Fill(table);
return table.Rows[0]["Name"];
}
public static object RepeatedFill(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
adapter.Fill(table);
return table.Rows[0]["Name"];
}
public static object AdapterReused(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
var other = new DataTable();
adapter.Fill(table);
adapter.Fill(other);
return table.Rows[0]["Name"];
}
public static object FillSchemaFirst(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.FillSchema(table, SchemaType.Source);
adapter.Fill(table);
return table.Rows[0]["Name"];
}
public static object TableMappings(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
adapter.TableMappings.Add("Table", "Items");
var table = new DataTable();
adapter.Fill(table);
return table.Rows[0]["Name"];
}
public static object MissingSchemaAction(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
adapter.MissingSchemaAction = System.Data.MissingSchemaAction.AddWithKey;
var table = new DataTable();
adapter.Fill(table);
return table.Rows[0]["Name"];
}
public static object MissingMappingAction(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
adapter.MissingMappingAction = System.Data.MissingMappingAction.Ignore;
var table = new DataTable();
adapter.Fill(table);
return table.Rows[0]["Name"];
}
public static object AcceptChangesDuringFill(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
adapter.AcceptChangesDuringFill = false;
var table = new DataTable();
adapter.Fill(table);
return table.Rows[0]["Name"];
}
public static object ContinueUpdateOnError(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
adapter.ContinueUpdateOnError = true;
var table = new DataTable();
adapter.Fill(table);
return table.Rows[0]["Name"];
}
public static object AdapterAlias(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var alias = adapter;
var table = new DataTable();
adapter.Fill(table);
return table.Rows[0]["Name"];
}
public static object AdapterReassignment(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
return table.Rows[0]["Name"];
}
public static object NamedTable(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable("Items");
adapter.Fill(table);
return table.Rows[0]["Name"];
}
public static object TableReassigned(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
table = new DataTable();
adapter.Fill(table);
return table.Rows[0]["Name"];
}
public static object TableAlias(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
var alias = table;
return table.Rows[0]["Name"];
}
public static object TableEscape(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
Consume(table);
return table.Rows[0]["Name"];
}
public static object RefEscape(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
Touch(ref table);
return table.Rows[0]["Name"];
}
public static object ColumnsAdd(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
table.Columns.Add("Extra");
adapter.Fill(table);
return table.Rows[0]["Name"];
}
public static object CaseSensitive(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
table.CaseSensitive = true;
adapter.Fill(table);
return table.Rows[0]["Name"];
}
public static object TableNameWrite(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
table.TableName = "Items";
adapter.Fill(table);
return table.Rows[0]["Name"];
}
public static object MergeTable(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
var other = new DataTable();
adapter.Fill(table);
table.Merge(other);
return table.Rows[0]["Name"];
}
public static object ClearTable(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
table.Clear();
return table.Rows[0]["Name"];
}
public static object RowsAdd(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
table.Rows.Add(1, "synthetic", 2.5m);
return table.Rows[0]["Name"];
}
public static object RowWrite(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
table.Rows[0]["Name"] = "synthetic";
return table.Rows[0]["Name"];
}
public static object OtherTableRows(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
var other = new DataTable();
adapter.Fill(table);
var row = other.Rows[0];
return row["Name"];
}
public static object SelectRows(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
var rows = table.Select();
return rows[0]["Name"];
}
public static object DefaultView(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
return table.DefaultView[0]["Name"];
}
public static object CopyTable(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
return table.Copy().Rows[0]["Name"];
}
public static object LinqRows(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
var rows = table.AsEnumerable();
return rows;
}
public static object FieldExtension(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
return table.Rows[0].Field<string>("Name");
}
public static object DataColumnIndexer(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
var column = new DataColumn("Name");
adapter.Fill(table);
return table.Rows[0][column];
}
public static object RowVersion(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
return table.Rows[0]["Name", DataRowVersion.Current];
}
public static object DynamicRowIndex(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
return table.Rows[index]["Name"];
}
public static object RowReassignment(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
var row = table.Rows[0];
row = table.Rows[1];
return row["Name"];
}
public static object RowAlias(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
var row = table.Rows[0];
var alias = row;
return row["Name"];
}
public static object RowMember(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
var row = table.Rows[0];
return row.ItemArray;
}
public static object UnrelatedTableGuard(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
var other = new DataTable();
adapter.Fill(table);
if (other.Rows.Count > 0) {
return table.Rows[0]["Name"];
}
return null;
}
public static object ReaderGuard(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
var gateCommand = new SqlCommand("dbo.ReadOther");
var gate = gateCommand.ExecuteReader();
if (gate.Read()) {
return table.Rows[0]["Name"];
}
return null;
}
public static object AdapterGuard(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
if (adapter.Fill(table) > 0) {
return table.Rows[0]["Name"];
}
return null;
}
public static object GuardBeforeFill(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
if (table.Rows.Count > 0) {
return null;
}
adapter.Fill(table);
return table.Rows[0]["Name"];
}
public static object GuardWrongComparison(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
if (table.Rows.Count >= 1) {
return table.Rows[0]["Name"];
}
return null;
}
public static object GuardElse(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
if (table.Rows.Count > 0) {
return table.Rows[0]["Name"];
} else {
return null;
}
}
public static object SwitchFlow(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
switch (index) {
case 0:
return table.Rows[0]["Name"];
}
return null;
}
public static object TryCatch(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
try {
adapter.Fill(table);
} catch (SqlException) {
}
return table.Rows[0]["Name"];
}
public static object LambdaFlow(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
Func<object> read = () => table.Rows[0]["Name"];
return read();
}
public static void HelperFill(SqlDataAdapter adapter, DataTable table) {
adapter.Fill(table);
}
public static object ForeachObjectRow(string columnName, int index, bool flag) {
var command = new SqlCommand("dbo.ReadItems");
command.CommandType = CommandType.StoredProcedure;
var adapter = new SqlDataAdapter(command);
var table = new DataTable();
adapter.Fill(table);
object last = null;
foreach (object item in table.Rows) {
last = item;
}
return last;
}
}
