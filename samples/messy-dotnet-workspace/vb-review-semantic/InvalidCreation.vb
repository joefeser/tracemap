Public Class SemanticCaller
    Public Sub Run()
        Dim invalid = New NeedsArg()
    End Sub
End Class

Public Class NeedsArg
    Public Sub New(value As Integer)
    End Sub
End Class
