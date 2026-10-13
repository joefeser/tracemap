Public Class ParameterCandidates
    Public Sub ConstructOnly(name As String, value As Object)
        Dim candidate = New Global.System.Data.SqlClient.SqlParameter("@PublicId", Global.System.Data.SqlDbType.Int)
        Dim dynamicCandidate = New Global.System.Data.SqlClient.SqlParameter(name, value)
        ' Construction does not prove attachment to a command or procedure execution.
    End Sub
End Class
