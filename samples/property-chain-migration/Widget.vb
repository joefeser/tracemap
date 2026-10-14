' Public synthetic source-only fixture; no application methods execute.
Public Class Widget
    Private Sub Clicked(sender As Object, e As System.EventArgs) Handles button.Click
        Controller.Current.Store.Save()
    End Sub
End Class
