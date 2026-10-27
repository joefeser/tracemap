' Independently authored. Compile and scan only; never invoke methods.
#Disable Warning BC40000 ' Signed legacy provider static compatibility control.
Imports System
Imports System.Data
Imports System.Data.SqlClient
Imports System.Linq
Friend Module PrivateData
' ---- Bounded private helper (Phase 4 origin) and its same-document callers.
Private Function ReadPrivate(procedure As String) As DataTable
Dim command = New SqlCommand(procedure)
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Return table
End Function
Public Function HelperFieldName(columnName As String, column As DataColumn, flag As Boolean) As Object
Dim table = ReadPrivate("dbo.ReadItems")
Return table.Rows(0).Field(Of String)("Name")
End Function
Public Function HelperFieldOtherProcedure(columnName As String, column As DataColumn, flag As Boolean) As Object
Dim table = ReadPrivate("dbo.ReadOther")
Return table.Rows(0).Field(Of String)("Title")
End Function
Public Function HelperFieldMissing(columnName As String, column As DataColumn, flag As Boolean) As Object
Dim table = ReadPrivate("dbo.ReadItems")
Return table.Rows(0).Field(Of String)("Missing")
End Function
Public Function HelperFieldColumn(columnName As String, column As DataColumn, flag As Boolean) As Object
Dim table = ReadPrivate("dbo.ReadItems")
Return table.Rows(0).Field(Of String)(column)
End Function
' ---- State cell: a Field read inside the helper body does not widen the body.
Private Function ReadBody(procedure As String) As DataTable
Dim command = New SqlCommand(procedure)
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Dim name = table.Rows(0).Field(Of String)("Name")
Return table
End Function
Public Function UseReadBody(columnName As String, column As DataColumn, flag As Boolean) As Object
Dim table = ReadBody("dbo.ReadItems")
Return table.Rows(0)("Name")
End Function
End Module
Friend Module InternalData
' ---- Bounded internal helpers (Phase 5 origin); every caller is in Callers.
Friend Function ReadShared(procedure As String) As DataTable
Dim command = New SqlCommand(procedure)
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Return table
End Function
Friend Function ReadSharedSet(procedure As String) As DataSet
Dim command = New SqlCommand(procedure)
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results)
Return results
End Function
End Module
