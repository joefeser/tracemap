' Synthetic static-analysis input. Never execute these methods or contact endpoints.
' UnavailableWidgetBase deliberately has no source or admitted assembly in this fixture.
Public Class ExternalBaseWidget
    Inherits UnavailableWidgetBase

    Private Sub Send_Click(sender As Object, e As System.EventArgs) Handles sendButton.Click
        SendCore()
    End Sub

    Private Sub SendCore()
        Dim client As New System.Net.WebClient()
        client.UploadString("https://example.invalid/synthetic", "POST", "public-payload")
    End Sub

    Private Sub Save_Click(sender As Object, e As System.EventArgs) Handles saveButton.Click
        Dim backend As New SyntheticRepository()
        backend.Save()
    End Sub
End Class

' Same event syntax in a non-WebForms class must not become a WebForms boundary.
Public Class DesktopEvents
    Inherits System.Windows.Forms.Form
    Private Sub Refresh_Click(sender As Object, e As System.EventArgs) Handles refreshButton.Click
        Dim client As New System.Net.WebClient()
        client.DownloadString("https://example.invalid/desktop")
    End Sub
End Class

' Ordinary direct UserControl inheritance remains a syntax-only framework candidate.
Public Class DirectWidget
    Inherits System.Web.UI.UserControl
    Private Sub Save_Click(sender As Object, e As System.EventArgs) Handles saveButton.Click
        Dim backend As New SyntheticRepository()
        backend.Save()
    End Sub
End Class
