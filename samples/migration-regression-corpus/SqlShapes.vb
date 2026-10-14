Imports System.Data
Imports P = System.Data.SqlClient.SqlParameter

Public Class SyntheticRepository
    Public Sub Save()
        SaveCore()
    End Sub

    Private Sub SaveCore()
        Try
            Dim transaction As SqlClient.SqlTransaction = Nothing
            Dim query As String = String.Empty
            query += "DELETE FROM SyntheticRows"
            query &= " WHERE RowId = @RowId"
            Dim command As SqlClient.SqlCommand = New SqlClient.SqlCommand(query)
            command.Parameters.Add("@RowId", SqlDbType.Int)
            command.Parameters.Add("@Year", SqlDbType.Int)
            command.Parameters.Add("@Month", SqlDbType.Int)
            command.Parameters.Add("@Label", SqlDbType.VarChar)
            command.Parameters.Add("@Group", SqlDbType.Int)
            command.Parameters("@RowId").Value = System.Environment.TickCount
            command.Parameters("@Year").Value = System.DateTime.UtcNow.Year
            command.Parameters("@Month").Value = System.DateTime.UtcNow.Month
            command.Parameters("@Label").Value = System.Environment.MachineName.Trim()
            command.Parameters("@Group").Value = CInt(System.DateTime.UtcNow.Day)
            command.Transaction = transaction
            command.ExecuteNonQuery()
        Catch
        End Try
    End Sub

    Public Sub BulkOnly(connection As SqlClient.SqlConnection, rows As DataTable)
        Using copy As New SqlClient.SqlBulkCopy(connection)
            copy.DestinationTableName = "SyntheticRows"
            copy.WriteToServer(rows)
        End Using
    End Sub

    Public Sub AliasConstructor()
        Dim ordinary = New Object()
        Dim parameter = New P("@Standalone", SqlDbType.Int)
    End Sub

    Public Sub ReassignedCommand()
        Dim command As New SqlClient.SqlCommand()
        command.Parameters.Add("@BeforeReset", SqlDbType.Int)
        command = New SqlClient.SqlCommand()
        command.ExecuteNonQuery()
    End Sub

    Public Sub UnknownText(context As Object)
        Dim query As String = context.Empty
        query &= "SELECT SyntheticValue"
        Dim command As New SqlClient.SqlCommand(query)
        command.Parameters.Add("@UnknownText", SqlDbType.Int)
        command.ExecuteNonQuery()
    End Sub
End Class
