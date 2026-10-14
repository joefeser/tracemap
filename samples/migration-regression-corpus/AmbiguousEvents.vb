' The report's syntax join does not resolve overloads from argument values.
' Both one-argument methods must remain unconnected, rather than guessing one.
Public Class AmbiguousEvents
    Inherits UnavailableWidgetBase
    Private Sub Trigger(sender As Object, e As System.EventArgs) Handles actionButton.Click
        Dispatch(1)
    End Sub
    Private Sub Dispatch(value As Integer)
        Dim client As New System.Net.WebClient()
        client.DownloadString("https://example.invalid/integer")
    End Sub
    Private Sub Dispatch(value As String)
        Dim client As New System.Net.WebClient()
        client.DownloadString("https://example.invalid/string")
    End Sub
End Class
