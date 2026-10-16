Imports System.Data
Imports System.Net
Public Class PublicBackend
    Public Sub Save()
        SaveCore()
        Notify()
    End Sub
    Private Sub SaveCore()
        Try
            Dim transaction As SqlClient.SqlTransaction = Nothing
            Dim text As String = String.Empty
            text += "DELETE FROM PublicRows"
            text &= " WHERE PublicId = @PublicId"
            Dim command As SqlClient.SqlCommand = New SqlClient.SqlCommand(text)
            command.Parameters.Add("@PublicId", SqlDbType.Int)
            command.Parameters.Add("@PublicYear", SqlDbType.Int)
            command.Parameters.Add("@PublicMonth", SqlDbType.Int)
            command.Parameters.Add("@PublicLabel", SqlDbType.VarChar)
            command.Parameters.Add("@PublicGroup", SqlDbType.Int)
            command.Parameters("@PublicId").Value = System.Environment.TickCount
            command.Parameters("@PublicYear").Value = System.DateTime.UtcNow.Year
            command.Parameters("@PublicMonth").Value = System.DateTime.UtcNow.Month
            command.Parameters("@PublicLabel").Value = System.Environment.MachineName.Trim()
            command.Parameters("@PublicGroup").Value = CInt(System.DateTime.UtcNow.Day)
            command.Transaction = transaction
            command.ExecuteNonQuery()
        Catch
        End Try
    End Sub
    Private Sub Notify()
        Dim client As New WebClient()
        client.DownloadString("https://example.invalid/public")
    End Sub
    Public Sub Unconnected()
        Dim command As New SqlClient.SqlCommand()
        command.ExecuteScalar()
    End Sub
End Class
