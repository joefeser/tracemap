' Deliberately duplicates the other root's type and explicit constructor.
' A projectless caller cannot prove which one New refers to.
Public Class SyntheticDataAccess
    Public Sub New()
        Dim alternate As New AlternateLoader()
        alternate.LoadChoices()
    End Sub
End Class

Public Class AlternateLoader
    Public Sub LoadChoices()
    End Sub
End Class
