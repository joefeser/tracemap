Imports System.Data
Imports System.Data.SqlClient

Public Class PublicExample
    Public Sub Nested(connection As SqlConnection, transaction As SqlTransaction, value As Integer)
        Try
            Dim command As New SqlCommand("PublicProcedure", connection)
            command.Parameters.Add("@PublicId", SqlDbType.Int)
            command.Parameters("@PublicId").Value = value
            command.Transaction = transaction
            command.ExecuteNonQuery()
        Catch
        End Try
    End Sub

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
