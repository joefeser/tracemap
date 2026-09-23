Namespace CrossLanguage.VisualBasic
    Public Module VbBridge
        Public Function Run(value As Integer) As Integer
            Return CrossLanguage.FSharp.Functions.Terminal(value)
        End Function
    End Module
End Namespace
