' Independently authored. Compile and scan only; never invoke methods.
#Disable Warning BC40000 ' Signed legacy provider static compatibility control.
Imports System
Imports System.Data
Imports System.Data.SqlClient
Imports System.Linq
Public Module Tables
Private Sub Consume(value As Object)
End Sub
' ---- Core cells: compared against JSON, constant DDL and table-backed DDL shapes.
Public Function TableFieldName(columnName As String, column As DataColumn, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Return table.Rows(0).Field(Of String)("Name")
End Function
Public Function TableFieldOrdinal(columnName As String, column As DataColumn, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Return table.Rows(0).Field(Of Integer)(0)
End Function
Public Function TableFieldRowLocal(columnName As String, column As DataColumn, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Dim row = table.Rows(0)
Return row.Field(Of Integer?)("Id")
End Function
Public Function TableFieldForeach(columnName As String, column As DataColumn, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
If table.Rows.Count > 0 Then
For Each row As DataRow In table.Rows
Consume(row.Field(Of Decimal)("Total"))
Next
End If
Return Nothing
End Function
Public Function TableFieldMixed(columnName As String, column As DataColumn, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Dim row = table.Rows(0)
Consume(row("Id"))
Return row.Field(Of String)("Name")
End Function
Public Function TableFieldMissing(columnName As String, column As DataColumn, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Return table.Rows(0).Field(Of String)("Missing")
End Function
Public Function TableFieldOrdinalOutOfRange(columnName As String, column As DataColumn, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Return table.Rows(0).Field(Of String)(3)
End Function
Public Function TableFieldCaseOnly(columnName As String, column As DataColumn, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Return table.Rows(0).Field(Of String)("name")
End Function
Public Function TableFieldDynamic(columnName As String, column As DataColumn, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Return table.Rows(0).Field(Of String)(columnName)
End Function
Public Function TableFieldRenamed(columnName As String, column As DataColumn, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Return table.Rows(0).Field(Of String)("Name")
End Function
Public Function TableFieldIncomplete(columnName As String, column As DataColumn, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Return table.Rows(0).Field(Of String)("Name")
End Function
' ---- State cells (table-backed DDL only): typed unknowns and field-read-absent sites.
Public Function TableFieldColumn(columnName As String, column As DataColumn, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Return table.Rows(0).Field(Of String)(column)
End Function
Public Function TableFieldVersion(columnName As String, column As DataColumn, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Return table.Rows(0).Field(Of String)("Name", DataRowVersion.Original)
End Function
Public Function TableSetField(columnName As String, column As DataColumn, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
table.Rows(0).SetField("Name", "renamed")
Return Nothing
End Function
Public Function TableFieldAsEnumerable(columnName As String, column As DataColumn, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Return table.AsEnumerable().First().Field(Of String)("Name")
End Function
Public Function TableFieldStatic(columnName As String, column As DataColumn, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Return DataRowExtensions.Field(Of String)(table.Rows(0), "Name")
End Function
Public Function TableFieldUnbound(columnName As String, column As DataColumn, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Dim other = New DataTable()
Return other.Rows(0).Field(Of String)("Name")
End Function
Public Function TableNoField(columnName As String, column As DataColumn, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Return table.Rows(0)("Name")
End Function
End Module
