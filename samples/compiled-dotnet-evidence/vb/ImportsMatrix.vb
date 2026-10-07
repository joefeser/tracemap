Imports FileToken = TraceMap.CompiledFixtures.FileImported.ImportToken

Namespace TraceMap.CompiledFixtures.ProjectImported
    Public Class ImportToken
    End Class
End Namespace

Namespace TraceMap.CompiledFixtures.FileImported
    Public Class ImportToken
    End Class
End Namespace

Namespace TraceMap.CompiledFixtures.Equivalence
    Public Class ImportsMatrix
        Public Shared Function ViaProject(value As ImportToken) As ImportToken
            Return value
        End Function
        Public Shared Function ViaAlias(value As FileToken) As FileToken
            Return value
        End Function
        Public Shared Function ViaDefault(value As List(Of Integer)) As List(Of Integer)
            Return value
        End Function
    End Class
End Namespace
