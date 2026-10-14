Imports System.Data

' Compile and scan only. No real connection strings or database execution.
Public Class SyntheticStore
    Public Sub ExecuteProcedure(procedureName As String)
        ExecuteProcedure(procedureName, New Object() {})
    End Sub

    Public Sub ExecuteProcedure(procedureName As String, arguments As Object())
        Dim command As New SqlClient.SqlCommand(procedureName)
        command.CommandType = CommandType.StoredProcedure
        command.ExecuteNonQuery()
    End Sub

    Public Function ExecuteText(sql As String) As Object
        Dim command As New SqlClient.SqlCommand(sql)
        Return command.ExecuteScalar()
    End Function
End Class
