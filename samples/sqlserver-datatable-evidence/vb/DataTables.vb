' Independently authored. Compile and scan only; never invoke methods.
#Disable Warning BC40000 ' Signed legacy provider static compatibility control.
Imports System
Imports System.Data
Imports System.Data.SqlClient
Public Module DataTableCases
Private Sub Consume(table As DataTable)
End Sub
Private Sub Touch(ByRef table As DataTable)
End Sub
Public Function [CtorName](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Return table.Rows(0)("Name")
End Function
Public Function [SelectCommandProperty](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter()
adapter.SelectCommand = command
Dim table = New DataTable()
adapter.Fill(table)
Return table.Rows(0)("Id")
End Function
Public Function [DiscardFill](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
Call adapter.Fill(table)
Return table.Rows(0)("Total")
End Function
Public Function [RowLocalOrdinal](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Dim row = table.Rows(0)
Return row(2)
End Function
Public Function [ForeachRows](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Dim last As Object = Nothing
For Each row As DataRow In table.Rows
Dim id = row("Id")
last = row("Total")
Next
Return last
End Function
Public Function [CountGuard](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
If table.Rows.Count > 0 Then
Return table.Rows(0)("Name")
End If
Return Nothing
End Function
Public Function [CountGuardForeach](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Dim last As Object = Nothing
If table.Rows.Count > 0 Then
For Each row As DataRow In table.Rows
last = row(1)
Next
End If
Return last
End Function
Public Function [IsNullName](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Return table.Rows(0).IsNull("Total")
End Function
Public Function [IsNullOrdinal](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Dim row = table.Rows(0)
Return row.IsNull(0)
End Function
Public Function [BangName](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Dim row = table.Rows(0)
Return row!Name
End Function
Public Function [UsingLifetime](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Using adapter = New SqlDataAdapter(command)
Using table = New DataTable()
adapter.Fill(table)
Return table.Rows(0)("Name")
End Using
End Using
End Function
Public Function [WithParameter](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
command.Parameters.AddWithValue("@Id", index)
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Return table.Rows(0)("Name")
End Function
Public Function [TwoFills](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Dim otherCommand = New SqlCommand("dbo.ReadOther")
otherCommand.CommandType = CommandType.StoredProcedure
Dim otherAdapter = New SqlDataAdapter(otherCommand)
Dim otherTable = New DataTable()
otherAdapter.Fill(otherTable)
Dim name = table.Rows(0)("Name")
Return otherTable.Rows(0)("Title")
End Function
Public Function [ConvertedOrdinal](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Return table.Rows(0)(CInt(1.6))
End Function
Public Function [MissingName](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Return table.Rows(0)("Title")
End Function
Public Function [OrdinalOutOfRange](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Return table.Rows(0)(3)
End Function
Public Function [IsNullMissing](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Return table.Rows(0).IsNull("Price")
End Function
Public Function [Renamed](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Return table.Rows(0)("Name")
End Function
Public Function [Reordered](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Dim id = table.Rows(0)("Id")
Return table.Rows(0)("Total")
End Function
Public Function [CaseOnly](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Return table.Rows(0)("name")
End Function
Public Function [DuplicateDeclared](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Return table.Rows(0)("Name")
End Function
Public Function [DuplicateRenamed](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Return table.Rows(0)("Name1")
End Function
Public Function [DynamicName](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Return table.Rows(0)(columnName)
End Function
Public Function [DynamicOrdinal](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Return table.Rows(0)(index)
End Function
Public Function [NonIdentifierName](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Return table.Rows(0)("Unit Price")
End Function
Public Function [Incomplete](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Return table.Rows(0)("Name")
End Function
Public Function [AbsentBinding](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Return table.Rows(0)("Name")
End Function
Public Function [TextCommand](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.Text
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Return table.Rows(0)("Name")
End Function
Public Function [NoCommandType](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Return table.Rows(0)("Name")
End Function
Public Function [CommandTypeAfterAdapter](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
Dim adapter = New SqlDataAdapter(command)
command.CommandType = CommandType.StoredProcedure
Dim table = New DataTable()
adapter.Fill(table)
Return table.Rows(0)("Name")
End Function
Public Function [ParameterAfterAdapter](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
command.Parameters.AddWithValue("@Id", index)
Dim table = New DataTable()
adapter.Fill(table)
Return table.Rows(0)("Name")
End Function
Public Function [CommandAlias](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim [alias] = command
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Return table.Rows(0)("Name")
End Function
Public Function [CommandExecuted](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
command.ExecuteNonQuery()
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Return table.Rows(0)("Name")
End Function
Public Function [ConditionalCommandText](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
If flag Then
command.CommandText = "dbo.ReadOther"
End If
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Return table.Rows(0)("Name")
End Function
Public Function [TextAdapterConstructor](columnName As String, index As Integer, flag As Boolean) As Object
Dim connection = New SqlConnection()
Dim adapter = New SqlDataAdapter("dbo.ReadItems", connection)
Dim table = New DataTable()
adapter.Fill(table)
Return table.Rows(0)("Name")
End Function
Public Function [AdapterInitializer](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter With {.SelectCommand = command}
Dim table = New DataTable()
adapter.Fill(table)
Return table.Rows(0)("Name")
End Function
Public Function [SelectCommandTwice](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
adapter.SelectCommand = command
Dim table = New DataTable()
adapter.Fill(table)
Return table.Rows(0)("Name")
End Function
Public Function [SelectCommandUnbound](columnName As String, index As Integer, flag As Boolean) As Object
Dim adapter = New SqlDataAdapter()
Dim table = New DataTable()
adapter.Fill(table)
Return table.Rows(0)("Name")
End Function
Public Function [SelectCommandAfterFill](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter()
Dim table = New DataTable()
adapter.Fill(table)
adapter.SelectCommand = command
Return table.Rows(0)("Name")
End Function
Public Function [SelectCommandViaAdapter](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
adapter.SelectCommand.CommandText = "dbo.ReadOther"
Dim table = New DataTable()
adapter.Fill(table)
Return table.Rows(0)("Name")
End Function
Public Function [FillDataSet](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim [set] = New DataSet()
adapter.Fill([set])
Return [set].Tables(0).Rows(0)("Name")
End Function
Public Function [FillDataSetTable](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim [set] = New DataSet()
adapter.Fill([set], "Items")
Return [set].Tables(0).Rows(0)("Name")
End Function
Public Function [FillRange](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(0, 10, table)
Return table.Rows(0)("Name")
End Function
Public Function [FillResultUsed](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
Dim count = adapter.Fill(table)
Return table.Rows(0)("Name")
End Function
Public Function [RepeatedFill](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
adapter.Fill(table)
Return table.Rows(0)("Name")
End Function
Public Function [AdapterReused](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
Dim other = New DataTable()
adapter.Fill(table)
adapter.Fill(other)
Return table.Rows(0)("Name")
End Function
Public Function [FillSchemaFirst](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.FillSchema(table, SchemaType.Source)
adapter.Fill(table)
Return table.Rows(0)("Name")
End Function
Public Function [TableMappings](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
adapter.TableMappings.Add("Table", "Items")
Dim table = New DataTable()
adapter.Fill(table)
Return table.Rows(0)("Name")
End Function
Public Function [MissingSchemaAction](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
adapter.MissingSchemaAction = System.Data.MissingSchemaAction.AddWithKey
Dim table = New DataTable()
adapter.Fill(table)
Return table.Rows(0)("Name")
End Function
Public Function [MissingMappingAction](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
adapter.MissingMappingAction = System.Data.MissingMappingAction.Ignore
Dim table = New DataTable()
adapter.Fill(table)
Return table.Rows(0)("Name")
End Function
Public Function [AcceptChangesDuringFill](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
adapter.AcceptChangesDuringFill = False
Dim table = New DataTable()
adapter.Fill(table)
Return table.Rows(0)("Name")
End Function
Public Function [ContinueUpdateOnError](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
adapter.ContinueUpdateOnError = True
Dim table = New DataTable()
adapter.Fill(table)
Return table.Rows(0)("Name")
End Function
Public Function [AdapterAlias](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim [alias] = adapter
Dim table = New DataTable()
adapter.Fill(table)
Return table.Rows(0)("Name")
End Function
Public Function [AdapterReassignment](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Return table.Rows(0)("Name")
End Function
Public Function [NamedTable](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable("Items")
adapter.Fill(table)
Return table.Rows(0)("Name")
End Function
Public Function [TableReassigned](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
table = New DataTable()
adapter.Fill(table)
Return table.Rows(0)("Name")
End Function
Public Function [TableAlias](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Dim [alias] = table
Return table.Rows(0)("Name")
End Function
Public Function [TableEscape](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Consume(table)
Return table.Rows(0)("Name")
End Function
Public Function [RefEscape](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Touch(table)
Return table.Rows(0)("Name")
End Function
Public Function [ColumnsAdd](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
table.Columns.Add("Extra")
adapter.Fill(table)
Return table.Rows(0)("Name")
End Function
Public Function [CaseSensitive](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
table.CaseSensitive = True
adapter.Fill(table)
Return table.Rows(0)("Name")
End Function
Public Function [TableNameWrite](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
table.TableName = "Items"
adapter.Fill(table)
Return table.Rows(0)("Name")
End Function
Public Function [MergeTable](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
Dim other = New DataTable()
adapter.Fill(table)
table.Merge(other)
Return table.Rows(0)("Name")
End Function
Public Function [ClearTable](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
table.Clear()
Return table.Rows(0)("Name")
End Function
Public Function [RowsAdd](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
table.Rows.Add(1, "synthetic", 2.5D)
Return table.Rows(0)("Name")
End Function
Public Function [RowWrite](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
table.Rows(0)("Name") = "synthetic"
Return table.Rows(0)("Name")
End Function
Public Function [OtherTableRows](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
Dim other = New DataTable()
adapter.Fill(table)
Dim row = other.Rows(0)
Return row("Name")
End Function
Public Function [SelectRows](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Dim rows = table.Select()
Return rows(0)("Name")
End Function
Public Function [DefaultView](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Return table.DefaultView(0)("Name")
End Function
Public Function [CopyTable](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Return table.Copy().Rows(0)("Name")
End Function
Public Function [LinqRows](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Dim rows = table.AsEnumerable()
Return rows
End Function
Public Function [FieldExtension](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Return table.Rows(0).Field(Of String)("Name")
End Function
Public Function [DataColumnIndexer](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
Dim column = New DataColumn("Name")
adapter.Fill(table)
Return table.Rows(0)(column)
End Function
Public Function [RowVersion](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Return table.Rows(0)("Name", DataRowVersion.Current)
End Function
Public Function [DynamicRowIndex](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Return table.Rows(index)("Name")
End Function
Public Function [RowReassignment](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Dim row = table.Rows(0)
row = table.Rows(1)
Return row("Name")
End Function
Public Function [RowAlias](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Dim row = table.Rows(0)
Dim [alias] = row
Return row("Name")
End Function
Public Function [RowMember](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Dim row = table.Rows(0)
Return row.ItemArray
End Function
Public Function [UnrelatedTableGuard](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
Dim other = New DataTable()
adapter.Fill(table)
If other.Rows.Count > 0 Then
Return table.Rows(0)("Name")
End If
Return Nothing
End Function
Public Function [ReaderGuard](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Dim gateCommand = New SqlCommand("dbo.ReadOther")
Dim gate = gateCommand.ExecuteReader()
If gate.Read() Then
Return table.Rows(0)("Name")
End If
Return Nothing
End Function
Public Function [AdapterGuard](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
If adapter.Fill(table) > 0 Then
Return table.Rows(0)("Name")
End If
Return Nothing
End Function
Public Function [GuardBeforeFill](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
If table.Rows.Count > 0 Then
Return Nothing
End If
adapter.Fill(table)
Return table.Rows(0)("Name")
End Function
Public Function [GuardWrongComparison](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
If table.Rows.Count >= 1 Then
Return table.Rows(0)("Name")
End If
Return Nothing
End Function
Public Function [GuardElse](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
If table.Rows.Count > 0 Then
Return table.Rows(0)("Name")
Else
Return Nothing
End If
End Function
Public Function [SwitchFlow](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Select Case index
Case 0
Return table.Rows(0)("Name")
End Select
Return Nothing
End Function
Public Function [TryCatch](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
Try
adapter.Fill(table)
Catch ex As SqlException
End Try
Return table.Rows(0)("Name")
End Function
Public Function [LambdaFlow](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Dim read As Func(Of Object) = Function() table.Rows(0)("Name")
Return read()
End Function
Public Sub [HelperFill](adapter As SqlDataAdapter, table As DataTable)
adapter.Fill(table)
End Sub
Public Function [ForeachObjectRow](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Dim last As Object = Nothing
For Each item As Object In table.Rows
last = item
Next
Return last
End Function
End Module
