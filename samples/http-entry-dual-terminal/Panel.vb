Imports System
Imports System.Net
Imports System.Web.UI
Imports System.Web.UI.WebControls

' Synthetic compile-only evidence. Never host or execute this fixture.
Public Class QueryPanel
    Inherits UserControl
    Protected WithEvents RefreshToggle As New CheckBox()
    Public Sub RefreshChanged(sender As Object, e As EventArgs) Handles RefreshToggle.CheckedChanged
        BeginQuery()
        CycleOnly()
    End Sub
    Public Sub BeginQuery()
        Dim request As New ProfileQuery()
    End Sub
    Private Sub CycleOnly()
        CycleOnly()
    End Sub
End Class

Public Class ProfileQuery
    Public Sub New()
        RemoteCalls.FetchProfile()
    End Sub
End Class

Public Class RemoteCalls
    Public Shared Sub FetchProfile()
        FetchToken()
        Using client As New WebClient()
            client.UploadString("https://example.invalid/profile", "POST", "synthetic")
        End Using
    End Sub
    Public Shared Sub FetchToken()
        Dim request As HttpWebRequest = CType(WebRequest.Create("https://example.invalid/token"), HttpWebRequest)
        Using response As WebResponse = request.GetResponse()
        End Using
    End Sub
End Class
