Imports System

Public Partial Class PageTwo
    Inherits System.Web.UI.Page

    Protected Sub RunButton_Click(sender As Object, e As EventArgs)
        Dim route As New AcceptanceRoutes()
        route.DispatchTwo()
    End Sub
End Class
