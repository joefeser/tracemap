Namespace CrossLanguage.VisualBasic
    Public Module VbBridge
        Public Function Run(value As Integer) As Integer
            Return CrossLanguage.FSharp.Functions.Terminal(value)
        End Function
    End Module

    ' Public-safe analogue of a legacy inherited Open-initialized field.
    Public Class SqlBaseDA
        Protected SQLDA As ProcedureGateway

        Protected Sub Open()
            SQLDA = New ProcedureGateway()
        End Sub
    End Class

    Public Class DataAccess
        Inherits SqlBaseDA

        Public Function SelectNames() As Integer
            Open()
            Return SQLDA.ExecuteProcedure()
        End Function
    End Class

    Public Class ProcedureGateway
        Public Function ExecuteProcedure() As Integer
            Return 1
        End Function
    End Class
End Namespace
