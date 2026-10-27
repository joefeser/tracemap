' Independently authored. Compile and scan only; never invoke methods.
#Disable Warning BC40000 ' Signed legacy provider static compatibility control.
Imports System
Imports System.Data
Imports System.Data.SqlClient
Friend Module InternalData
' ---- Bounded internal helpers: one Phase 2/3 Fill shape, command text from the procedure parameter, filled local returned.
Friend Function ReadTable(procedure As String) As DataTable
Dim command = New SqlCommand(procedure)
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Return table
End Function
Friend Function ReadSet(procedure As String) As DataSet
Dim command = New SqlCommand(procedure)
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results)
Return results
End Function
Friend Function ReadTableText(procedure As String) As DataTable
Dim command = New SqlCommand()
command.CommandText = procedure
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter()
adapter.SelectCommand = command
Dim table = New DataTable()
adapter.Fill(table)
Return table
End Function
Friend Function ReadTableUsing(connection As SqlConnection, procedure As String) As DataTable
Using command As New SqlCommand(procedure, connection)
command.CommandType = CommandType.StoredProcedure
Using adapter As New SqlDataAdapter(command)
Dim table As New DataTable()
adapter.Fill(table)
Return table
End Using
End Using
End Function
' ---- Same-document caller of an internal helper (core cell).
Public Function SameDocumentName(columnName As String, index As Integer, flag As Boolean) As Object
Dim table = ReadTable("dbo.ReadItems")
Return table.Rows(0)("Name")
End Function
' ---- Helper state cells: each refused helper keeps its caller(s) elsewhere.
Private Function PrivateHelper(procedure As String) As DataTable
Dim command = New SqlCommand(procedure)
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Return table
End Function
Public Function UsePrivateHelper(columnName As String, index As Integer, flag As Boolean) As Object
Dim table = PrivateHelper("dbo.ReadItems")
Return table.Rows(0)("Name")
End Function
Public Function PublicHelper(procedure As String) As DataTable
Dim command = New SqlCommand(procedure)
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Return table
End Function
Friend Function CrossDelegateHelper(procedure As String) As DataTable
Dim command = New SqlCommand(procedure)
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Return table
End Function
Friend Function CrossNameofHelper(procedure As String) As DataTable
Dim command = New SqlCommand(procedure)
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Return table
End Function
Friend Function GeneratedCallerHelper(procedure As String) As DataTable
Dim command = New SqlCommand(procedure)
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Return table
End Function
Friend Function LinkedCallerHelper(procedure As String) As DataTable
Dim command = New SqlCommand(procedure)
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Return table
End Function
Friend Function UncalledInternalHelper(procedure As String) As DataTable
Dim command = New SqlCommand(procedure)
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Return table
End Function
End Module
