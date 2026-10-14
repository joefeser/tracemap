' Public synthetic source-only fixture. No application methods are executed.
Public Class Widget
    Private Sub Clicked(sender As Object, e As System.EventArgs) Handles button.Click
        Call (New Worker()).Run(1)
    End Sub
End Class

Public Class Worker
    Public Sub Run(first As Integer, Optional second As Integer = 0)
        Dim client As New System.Net.WebClient()
        client.UploadString("https://example.invalid", "synthetic")
        Dim command As New System.Data.SqlClient.SqlCommand()
        command.ExecuteNonQuery()
        Dim copy As New System.Data.SqlClient.SqlBulkCopy("synthetic")
        copy.WriteToServer(Nothing)
    End Sub
End Class
