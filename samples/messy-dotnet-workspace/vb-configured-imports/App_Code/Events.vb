' Public synthetic event/control signature; never execute.
Public Class Events
    Public Shared Sub Handle(sender As Object, e As EventArgs, control As Control)
        Dim result = Provider.Query("synthetic command", Nothing)
    End Sub
End Class
