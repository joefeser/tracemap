Public Class NamespaceProbe
    Public Shared Function Inspect(ByVal value As String) As Boolean
        Dim control As New Control()
        Dim expression As New RegularExpressions.Regex("^x$")
        Return expression.IsMatch(value) AndAlso control IsNot Nothing
    End Function
End Class
