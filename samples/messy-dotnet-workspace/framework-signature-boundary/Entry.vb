' Public synthetic source root. The test generates matching PE fixtures with
' independently chosen desktop framework reference versions; no SQL is executed.
Public Class Entry
    Public Shared Sub Start()
        Dim result = Provider.Query("synthetic command", Nothing)
    End Sub
End Class
