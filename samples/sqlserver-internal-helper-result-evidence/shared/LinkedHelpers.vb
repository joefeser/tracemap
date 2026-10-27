' Independently authored. Compile and scan only; never invoke methods.
#Disable Warning BC40000 ' Signed legacy provider static compatibility control.
Imports System
Imports System.Data
Imports System.Data.SqlClient
' Compiled into vb/InternalHelpers.vbproj through MSBuild Link from outside the project directory.
Friend Module LinkedHelpers
Friend Function LinkedFileHelper(procedure As String) As DataTable
Dim command = New SqlCommand(procedure)
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Return table
End Function
End Module
