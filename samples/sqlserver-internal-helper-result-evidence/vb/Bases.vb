' Independently authored. Compile and scan only; never invoke methods.
#Disable Warning BC40000 ' Signed legacy provider static compatibility control.
Imports System
Imports System.Data
Imports System.Data.SqlClient
Friend Class Bases
Protected Friend Shared Function ProtectedInternalHelper(procedure As String) As DataTable
Dim command = New SqlCommand(procedure)
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Return table
End Function
Public Shared Function UseProtectedInternalHelper() As Object
Dim table = ProtectedInternalHelper("dbo.ReadItems")
Return table.Rows(0)("Name")
End Function
End Class
