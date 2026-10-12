' Public synthetic fixture. Do not execute application code or SQL.
Public Class Entry
    Public Shared Sub Start(items As Dictionary(Of String, Integer))
        Dim result = Provider.Query("synthetic command", Nothing)
    End Sub
End Class
