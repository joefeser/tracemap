' Independently authored. Compile and scan only; never invoke methods.
#Disable Warning BC40000 ' Signed legacy provider static compatibility control.
Imports System
Imports System.Data
Imports System.Data.SqlClient
Imports System.Linq
Public Module Sets
Private Sub Consume(value As Object)
End Sub
' ---- Core cells: the first DataSet table only.
Public Function SetFieldName(columnName As String, column As DataColumn, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results)
Return results.Tables(0).Rows(0).Field(Of String)("Name")
End Function
Public Function SetFieldTableLocal(columnName As String, column As DataColumn, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results)
Dim items = results.Tables("Table")
For Each row As DataRow In items.Rows
Consume(row.Field(Of Integer)(0))
Next
Return Nothing
End Function
Public Function SetFieldMissing(columnName As String, column As DataColumn, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results)
Return results.Tables(0).Rows(0).Field(Of String)("Missing")
End Function
' ---- State cell (table-backed DDL only).
Public Function SetFieldLaterTable(columnName As String, column As DataColumn, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results)
Return results.Tables(1).Rows(0).Field(Of Integer)("Value")
End Function
End Module
