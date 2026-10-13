Imports System.Data
Imports System.Data.SqlClient

Public Class PublicExample
    ' Static extraction fixture only. Do not execute or connect to a database.
    Public Sub StraightLine()
        Dim command As New SqlCommand()
        command.Parameters.Add("@PublicId", SqlDbType.Int)
        command.Parameters.Add("@PublicLabel", SqlDbType.VarChar)
        command.ExecuteNonQuery()
    End Sub

    Public Sub Reassigned()
        Dim command As New SqlCommand()
        command.Parameters.Add("@PublicId", SqlDbType.Int)
        command = New SqlCommand()
        command.ExecuteNonQuery()
    End Sub
End Class
