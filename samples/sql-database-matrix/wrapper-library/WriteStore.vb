Imports System.Collections
Imports System.Data

Public Class SyntheticParameter
    Public ReadOnly Name As String
    Public ReadOnly Kind As SqlDbType
    Public ReadOnly Value As Object
    Public Sub New(name As String, kind As SqlDbType, value As Object)
        Me.Name = name
        Me.Kind = kind
        Me.Value = value
    End Sub
End Class

Partial Public Class SyntheticStore
    Public Sub ExecuteUpdateText(sql As String)
        Dim command As New SqlClient.SqlCommand(sql)
        command.ExecuteNonQuery()
    End Sub

    Public Sub ExecuteUpdate(sql As String, parameters As ArrayList)
        Using command As New SqlClient.SqlCommand(sql)
            For Each item As SyntheticParameter In parameters
                command.Parameters.Add(New SqlClient.SqlParameter(item.Name, item.Kind) With {.Value = item.Value})
            Next
            command.ExecuteNonQuery()
        End Using
    End Sub

    Public Sub ReplaceRows(connection As SqlClient.SqlConnection, rows As DataTable)
        Using transaction As SqlClient.SqlTransaction = connection.BeginTransaction()
            Try
                Dim sql As String = "DELETE FROM dbo.PublicStage "
                sql &= "WHERE BatchId = @batch"
                Using command As New SqlClient.SqlCommand(sql, connection, transaction)
                    command.Parameters.Add("@batch", SqlDbType.Int).Value = 7
                    command.ExecuteNonQuery()
                End Using
                Using copy As New SqlClient.SqlBulkCopy(connection, SqlClient.SqlBulkCopyOptions.FireTriggers, transaction)
                    copy.DestinationTableName = "dbo.PublicStage"
                    copy.WriteToServer(rows)
                End Using
                transaction.Commit()
            Catch
                transaction.Rollback()
                Throw
            End Try
        End Using
    End Sub
End Class
