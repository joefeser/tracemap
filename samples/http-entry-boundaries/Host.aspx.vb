Imports System
Imports System.Web.UI

Public Class HostPage
    Inherits Page
    Private widget As New ProfileWidget()

    Protected Sub Page_Load(sender As Object, e As EventArgs) Handles Me.Load
        ' Markup containment and page lifecycle do not call widget event handlers.
    End Sub

    Public Sub ExplicitRead()
        Dim profile = widget.Profile
    End Sub
End Class
