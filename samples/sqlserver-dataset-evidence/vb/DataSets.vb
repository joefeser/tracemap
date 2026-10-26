' Independently authored. Compile and scan only; never invoke methods.
#Disable Warning BC40000 ' Signed legacy provider static compatibility control.
Imports System
Imports System.Data
Imports System.Data.SqlClient
' A typed DataSet: its tables are not the fresh-adapter defaults, so it stays outside the subset.
Public NotInheritable Class SyntheticTypedSet
Inherits DataSet
End Class
Public Module DataSetCases
Private ReadOnly SharedSet As New DataSet()
Private Sub Consume(results As DataSet)
End Sub
Private Sub ConsumeTable(table As DataTable)
End Sub
Private Sub Touch(ByRef results As DataSet)
End Sub
' ---- Core cells: compared against JSON, constant DDL and table-backed DDL shapes.
Public Function [IndexZero](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results)
Return results.Tables(0).Rows(0)("Name")
End Function
Public Function [DefaultName](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results)
Return results.Tables("Table").Rows(0)("Id")
End Function
Public Function [SourceTableName](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results, "Items")
Return results.Tables("Items").Rows(0)("Total")
End Function
Public Function [SourceTableIndexZero](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results, "Items")
Return results.Tables(0).Rows(0)(1)
End Function
Public Function [SelectCommandProperty](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter()
adapter.SelectCommand = command
Dim results = New DataSet()
adapter.Fill(results)
Return results.Tables(0).Rows(0)("Name")
End Function
Public Function [DiscardFill](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
Call adapter.Fill(results)
Return results.Tables(0).Rows(0)("Total")
End Function
Public Function [TableLocal](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results)
Dim table = results.Tables(0)
Return table.Rows(0)("Name")
End Function
Public Function [RowLocalOrdinal](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results)
Dim row = results.Tables(0).Rows(0)
Return row(2)
End Function
Public Function [ForeachRows](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results)
Dim last As Object = Nothing
For Each row As DataRow In results.Tables(0).Rows
Dim id = row("Id")
last = row("Total")
Next
Return last
End Function
Public Function [TableLocalForeach](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results)
Dim table = results.Tables(0)
Dim last As Object = Nothing
For Each row As DataRow In table.Rows
last = row("Name")
Next
Return last
End Function
Public Function [TablesCountGuard](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results)
If results.Tables.Count > 0 Then
Return results.Tables(0).Rows(0)("Name")
End If
Return Nothing
End Function
Public Function [RowsCountGuard](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results)
If results.Tables(0).Rows.Count > 0 Then
Return results.Tables(0).Rows(0)("Name")
End If
Return Nothing
End Function
Public Function [NestedGuards](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results)
Dim last As Object = Nothing
If results.Tables.Count > 0 Then
If results.Tables(0).Rows.Count > 0 Then
For Each row As DataRow In results.Tables(0).Rows
last = row(1)
Next
End If
End If
Return last
End Function
Public Function [IsNullName](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results)
Return results.Tables(0).Rows(0).IsNull("Total")
End Function
Public Function [IsNullOrdinal](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results)
Dim row = results.Tables(0).Rows(0)
Return row.IsNull(0)
End Function
Public Function [BangName](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results)
Dim row = results.Tables!Table.Rows(0)
Return row!Name
End Function
Public Function [UsingLifetime](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Using adapter = New SqlDataAdapter(command)
Using results = New DataSet()
adapter.Fill(results)
Return results.Tables(0).Rows(0)("Name")
End Using
End Using
End Function
Public Function [WithParameter](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
command.Parameters.AddWithValue("@Id", index)
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results)
Return results.Tables(0).Rows(0)("Name")
End Function
Public Function [TwoFills](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results)
Dim otherCommand = New SqlCommand("dbo.ReadOther")
otherCommand.CommandType = CommandType.StoredProcedure
Dim otherAdapter = New SqlDataAdapter(otherCommand)
Dim otherResults = New DataSet()
otherAdapter.Fill(otherResults)
Dim name = results.Tables(0).Rows(0)("Name")
Return otherResults.Tables(0).Rows(0)("Title")
End Function
Public Function [MixedSelectors](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results)
Dim id = results.Tables(0).Rows(0)("Id")
Return results.Tables("Table").Rows(0)("Name")
End Function
Public Function [ConvertedOrdinal](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results)
Return results.Tables(0).Rows(0)(CInt(1.6))
End Function
Public Function [Reordered](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results)
Dim name = results.Tables(0).Rows(0)("Name")
Return results.Tables(0).Rows(0)(2)
End Function
Public Function [MissingName](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results)
Return results.Tables(0).Rows(0)("Title")
End Function
Public Function [OrdinalOutOfRange](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results)
Return results.Tables(0).Rows(0)(3)
End Function
Public Function [IsNullMissing](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results)
Return results.Tables(0).Rows(0).IsNull("Title")
End Function
Public Function [Renamed](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results)
Return results.Tables(0).Rows(0)("Name")
End Function
Public Function [CaseOnly](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results)
Return results.Tables(0).Rows(0)("name")
End Function
Public Function [DuplicateDeclared](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results)
Return results.Tables(0).Rows(0)("Name")
End Function
Public Function [DuplicateRenamed](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results)
Return results.Tables(0).Rows(0)("Name1")
End Function
Public Function [DynamicName](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results)
Return results.Tables(0).Rows(0)(columnName)
End Function
Public Function [DynamicOrdinal](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results)
Return results.Tables(0).Rows(0)(index)
End Function
Public Function [NonIdentifierName](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results)
Return results.Tables(0).Rows(0)("Unit Price")
End Function
Public Function [Incomplete](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results)
Return results.Tables(0).Rows(0)("Name")
End Function
Public Function [AbsentBinding](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results)
Return results.Tables(0).Rows(0)("Name")
End Function
' ---- State cells: unknown with an exact state gap (table-backed DDL only).
Public Function [FillDataTable](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Return table.Rows(0)("Name")
End Function
Public Function [FillRange](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results, 0, 10, "Table")
Return results.Tables(0).Rows(0)("Name")
End Function
Public Function [FillResultUsed](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
Dim count = adapter.Fill(results)
Return results.Tables(0).Rows(0)("Name")
End Function
Public Function [DynamicSourceTable](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results, columnName)
Return results.Tables(0).Rows(0)("Name")
End Function
Public Function [NonIdentifierSourceTable](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results, "Item Rows")
Return results.Tables(0).Rows(0)("Name")
End Function
Public Function [EmptySourceTable](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results, "")
Return results.Tables(0).Rows(0)("Name")
End Function
Public Function [SecondTableIndex](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results)
Return results.Tables(1).Rows(0)("Name")
End Function
Public Function [NegativeTableIndex](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results)
Return results.Tables(-1).Rows(0)("Name")
End Function
Public Function [DynamicTableIndex](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results)
Return results.Tables(index).Rows(0)("Name")
End Function
Public Function [DynamicTableName](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results)
Return results.Tables(columnName).Rows(0)("Name")
End Function
Public Function [LaterTableName](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results)
Return results.Tables("Table1").Rows(0)("Name")
End Function
Public Function [CaseOnlyTableName](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results)
Return results.Tables("table").Rows(0)("Name")
End Function
Public Function [SourceNameMismatch](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results, "Items")
Return results.Tables("Table").Rows(0)("Name")
End Function
Public Function [TableNamespace](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results)
Return results.Tables("Table", "urn:synthetic").Rows(0)("Name")
End Function
Public Function [TablesCountUse](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results)
Dim count = results.Tables.Count
Return results.Tables(0).Rows(0)("Name")
End Function
Public Function [TablesCountWrongGuard](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results)
If results.Tables.Count > 1 Then
Return results.Tables(0).Rows(0)("Name")
End If
Return Nothing
End Function
Public Function [TablesAdd](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results)
results.Tables.Add("Extra")
Return results.Tables(0).Rows(0)("Name")
End Function
Public Function [TablesAlias](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results)
Dim tables = results.Tables
Return tables(0).Rows(0)("Name")
End Function
Public Function [TablesMember](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results)
Dim has = results.Tables.Contains("Table")
Return results.Tables(0).Rows(0)("Name")
End Function
Public Function [TablesForeach](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results)
Dim last As Object = Nothing
For Each item As DataTable In results.Tables
last = item.TableName
Next
Return last
End Function
Public Function [MergeDataSet](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results)
results.Merge(New DataSet())
Return results.Tables(0).Rows(0)("Name")
End Function
Public Function [ReadXml](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results)
results.ReadXml("synthetic.xml")
Return results.Tables(0).Rows(0)("Name")
End Function
Public Function [Relations](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results)
Dim relationSet = results.Relations
Return results.Tables(0).Rows(0)("Name")
End Function
Public Function [EnforceConstraints](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
results.EnforceConstraints = False
adapter.Fill(results)
Return results.Tables(0).Rows(0)("Name")
End Function
Public Function [CaseSensitiveDataSet](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
results.CaseSensitive = True
adapter.Fill(results)
Return results.Tables(0).Rows(0)("Name")
End Function
Public Function [CopyDataSet](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results)
Dim copy = results.Copy()
Return copy.Tables(0).Rows(0)("Name")
End Function
Public Function [DataSetMember](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results)
Dim xml = results.GetXml()
Return results.Tables(0).Rows(0)("Name")
End Function
Public Function [NamedDataSet](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet("Items")
adapter.Fill(results)
Return results.Tables(0).Rows(0)("Name")
End Function
Public Function [DataSetInitializer](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet() With {.DataSetName = "Items"}
adapter.Fill(results)
Return results.Tables(0).Rows(0)("Name")
End Function
Public Function [DataSetReassigned](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results)
results = New DataSet()
Return results.Tables(0).Rows(0)("Name")
End Function
Public Function [DataSetAlias](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results)
Dim aliased = results
Return aliased.Tables(0).Rows(0)("Name")
End Function
Public Function [DataSetEscape](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results)
Consume(results)
Return results.Tables(0).Rows(0)("Name")
End Function
Public Function [RefDataSet](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results)
Touch(results)
Return results.Tables(0).Rows(0)("Name")
End Function
Public Function [UseBeforeFill](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
Dim early = results.Tables(0)
adapter.Fill(results)
Return early.Rows(0)("Name")
End Function
Public Function [SharedDataSet](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = SharedSet
adapter.Fill(results)
Return results.Tables(0).Rows(0)("Name")
End Function
Public Function [TypedDataSet](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New SyntheticTypedSet()
adapter.Fill(results)
Return results.Tables(0).Rows(0)("Name")
End Function
Public Sub [HelperFill](adapter As SqlDataAdapter, results As DataSet)
adapter.Fill(results)
End Sub
Public Function [RepeatedFill](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results)
adapter.Fill(results)
Return results.Tables(0).Rows(0)("Name")
End Function
Public Function [AdapterReused](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
Dim otherResults = New DataSet()
adapter.Fill(results)
adapter.Fill(otherResults)
Return results.Tables(0).Rows(0)("Name")
End Function
Public Function [FillSchemaFirst](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.FillSchema(results, SchemaType.Source)
adapter.Fill(results)
Return results.Tables(0).Rows(0)("Name")
End Function
Public Function [TableMappings](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.TableMappings.Add("Table", "Items")
adapter.Fill(results)
Return results.Tables(0).Rows(0)("Name")
End Function
Public Function [MissingSchemaAction](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.MissingSchemaAction = System.Data.MissingSchemaAction.AddWithKey
adapter.Fill(results)
Return results.Tables(0).Rows(0)("Name")
End Function
Public Function [MissingMappingAction](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.MissingMappingAction = System.Data.MissingMappingAction.Error
adapter.Fill(results)
Return results.Tables(0).Rows(0)("Name")
End Function
Public Function [CommandTypeAfterAdapter](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
Dim adapter = New SqlDataAdapter(command)
command.CommandType = CommandType.StoredProcedure
Dim results = New DataSet()
adapter.Fill(results)
Return results.Tables(0).Rows(0)("Name")
End Function
Public Function [TextCommand](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.Text
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results)
Return results.Tables(0).Rows(0)("Name")
End Function
Public Function [TableLocalReassigned](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results)
Dim table = results.Tables(0)
table = New DataTable()
Return table.Rows(0)("Name")
End Function
Public Function [TableLocalEscape](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results)
Dim table = results.Tables(0)
ConsumeTable(table)
Return table.Rows(0)("Name")
End Function
Public Function [TableColumns](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results)
Dim columns = results.Tables(0).Columns
Return results.Tables(0).Rows(0)("Name")
End Function
Public Function [TableSelect](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results)
Dim selected = results.Tables(0).Select()
Return selected
End Function
Public Function [LinqRows](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results)
Dim rows = results.Tables(0).AsEnumerable()
Return rows
End Function
Public Function [TableClear](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results)
results.Tables(0).Clear()
Return Nothing
End Function
Public Function [RowsAdd](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results)
results.Tables(0).Rows.Add(1, "Synthetic", 2D)
Return Nothing
End Function
Public Function [RowWrite](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results)
results.Tables(0).Rows(0)("Name") = "Synthetic"
Return Nothing
End Function
Public Function [FieldExtension](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results)
Return results.Tables(0).Rows(0).Field(Of String)("Name")
End Function
Public Function [DynamicRowIndex](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results)
Return results.Tables(0).Rows(index)("Name")
End Function
Public Function [RowAlias](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results)
Dim row As Object = results.Tables(0).Rows(0)
Return row
End Function
Public Function [ForeachObjectRow](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results)
Dim last As Object = Nothing
For Each item As Object In results.Tables(0).Rows
last = item
Next
Return last
End Function
Public Function [GuardElse](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results)
If results.Tables.Count > 0 Then
Return results.Tables(0).Rows(0)("Name")
Else
Return Nothing
End If
End Function
Public Function [TablesAndRowsGuard](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results)
If results.Tables.Count > 0 AndAlso results.Tables(0).Rows.Count > 0 Then
Return results.Tables(0).Rows(0)("Name")
End If
Return Nothing
End Function
Public Function [OtherDataSetGuard](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results)
Dim other = New DataSet()
If other.Tables.Count > 0 Then
Return results.Tables(0).Rows(0)("Name")
End If
Return Nothing
End Function
Public Function [TryCatch](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results)
Try
Return results.Tables(0).Rows(0)("Name")
Catch ex As InvalidOperationException
Return Nothing
End Try
End Function
Public Function [NoReads](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results)
Return Nothing
End Function
End Module
