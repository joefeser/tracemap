' Public synthetic declaration shape; no application or SQL execution.
Namespace PublicDomain
    Public Class Container
        Public Class Entry
            Public Shared Sub Start()
                Dim result = Provider.Query("synthetic command", Nothing)
            End Sub
        End Class
    End Class
End Namespace
