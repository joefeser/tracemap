' The inline New SyntheticService() in the other source cannot select this
' declaration over the same-name service in the primary backend root.
Public Class SyntheticService
    Public Function SelectChoices(region As String, season As String, year As Integer) As System.Data.DataSet
        Return New System.Data.DataSet()
    End Function
End Class
