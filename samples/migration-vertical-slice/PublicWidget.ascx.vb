Public Class PublicWidget
    Inherits System.Web.UI.UserControl
    Protected Sub Submit_Click(sender As Object, e As System.EventArgs)
        Dim backend As New PublicBackend()
        backend.Save()
    End Sub
End Class
