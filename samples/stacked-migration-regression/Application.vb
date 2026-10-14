Imports System.Data

' Independent public fixture. Static scanning only; no method is executed.
Public Class Widget
    Inherits UnavailableWidgetBase
    Private Sub Save_Click(sender As Object, e As System.EventArgs) Handles saveButton.Click
        Controller.Current.Apply(Nothing)
    End Sub
    Private Sub Send_Click(sender As Object, e As System.EventArgs) Handles sendButton.Click
        SendCore()
    End Sub
    Private Sub SendCore()
        Dim client As New System.Net.WebClient()
        client.UploadString("https://example.invalid/send", "synthetic")
    End Sub
    Private Sub Load_Click(sender As Object, e As System.EventArgs) Handles loadButton.Click
        Call (New RemoteService()).Load(1)
    End Sub
End Class

Public Class Controller
    Public Shared Property Current As Selection
End Class
Public Class Selection
    Public Sub Apply(rows As DataTable, Optional mode As Integer = 0)
        Call (New Repository()).Save(rows)
    End Sub
End Class
Public Class RemoteService
    Public Sub Load(key As Integer, Optional mode As Integer = 0)
        Dim client As New System.Net.WebClient()
        client.DownloadString("https://example.invalid/load")
    End Sub
End Class

Public Class Repository
    Private store As New DataStore()
    Public Sub Save(rows As DataTable)
        store.WriteRows(rows)
    End Sub
    Protected Class DataStore
        Public Sub WriteRows(rows As DataTable)
            Try
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
                command.ExecuteNonQuery()
                Using copy As New SqlClient.SqlBulkCopy("synthetic")
                    copy.WriteToServer(rows)
                End Using
            Catch
            End Try
        End Sub
    End Class
End Class

' A global short-name decoy must not replace the nested receiver declaration.
Public Class DataStore
    Public Sub WriteRows(rows As DataTable)
    End Sub
End Class
