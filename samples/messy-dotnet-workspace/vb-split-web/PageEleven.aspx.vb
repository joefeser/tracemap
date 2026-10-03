Imports System

Public Partial Class PageEleven
    Inherits System.Web.UI.Page

    Protected Sub RunButton_Click(sender As Object, e As EventArgs)
        Dim route As New SplitAcceptanceRoutes()
        route.DispatchEleven()
    End Sub
End Class
