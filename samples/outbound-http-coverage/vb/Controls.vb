Imports System.Net
Imports System.Net.Http
Imports System.Net.Http.Json
Public Class Controls
    Public Sub DynamicDestination(client As HttpClient, destination As String)
        client.GetStringAsync(destination)
    End Sub
    Public Sub RequestVariable(client As HttpClient, message As HttpRequestMessage)
        client.SendAsync(message)
    End Sub
    Public Sub Wrapper(client As HttpClient, destination As String)
        DynamicDestination(client, destination)
    End Sub
    Public Sub Inherited(client As InheritedClient)
        client.GetStreamAsync("https://example.invalid/inherited")
    End Sub
    Public Sub FakeCalls(client As FakeClient)
        client.GetStringAsync("ignored")
        client.UploadString("ignored", "ignored")
        client.DownloadString("ignored")
        client.GetResponse()
    End Sub
    Public Sub ContentOnly(content As HttpContent)
        content.ReadFromJsonAsync(Of String)()
    End Sub
    Public Sub FileProtocol(client As WebClient)
        client.DownloadString("file:///public-fixture.txt")
    End Sub
    Public Sub DynamicVerb(client As WebClient, verb As String)
        client.UploadString("https://example.invalid/dynamic", verb, "synthetic")
    End Sub
End Class
Public Class InheritedClient
    Inherits HttpClient
End Class
Public Class FakeClient
    Public Sub GetStringAsync(value As String)
    End Sub
    Public Sub UploadString(value As String, data As String)
    End Sub
    Public Sub DownloadString(value As String)
    End Sub
    Public Sub GetResponse()
    End Sub
End Class
