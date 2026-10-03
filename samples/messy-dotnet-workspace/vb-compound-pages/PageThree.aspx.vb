Imports System

Public Partial Class PageThree
    Inherits System.Web.UI.Page

    Protected Sub RunButton_Click(sender As Object, e As EventArgs)
        Dim route As New AcceptanceRoutes()
        route.DispatchThree()
    End Sub
End Class
