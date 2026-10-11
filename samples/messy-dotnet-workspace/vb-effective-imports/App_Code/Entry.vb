' Public synthetic inherited-import fixture. No file Imports, no execution.
Public Class Entry
    Public Shared Sub Start(items As Dictionary(Of String, Integer))
        Dim result = Provider.Query("synthetic command", Nothing)
    End Sub
End Class
