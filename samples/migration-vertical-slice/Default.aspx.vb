Imports System.Data
Public Class PublicPage
    Inherits System.Web.UI.Page
    Protected Sub Save_Click(sender As Object, e As System.EventArgs)
        Dim backend As New PublicBackend()
        backend.Save()
    End Sub
End Class
