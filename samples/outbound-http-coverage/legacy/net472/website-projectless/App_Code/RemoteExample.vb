Imports System.Net

' Synthetic static-analysis input only. Never invoke this method.
Public Class RemoteExample
    Public Function ReadRemote(client As WebClient) As String
        Return client.DownloadString("https://example.invalid/synthetic")
    End Function
End Class
