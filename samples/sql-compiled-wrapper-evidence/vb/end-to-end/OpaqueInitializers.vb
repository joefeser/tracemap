Imports System
Imports System.Data
Imports System.Collections

Namespace PublicSql.VisualBasic
    ' Compile-only public examples. Builds never invoke these methods.
    Public NotInheritable Class OpaqueInitializerRoot
        Private seed As String = Environment.GetEnvironmentVariable("PUBLIC_SYNTHETIC_SEED").Trim().ToUpperInvariant()
        Private unused As String = Environment.GetEnvironmentVariable("PUBLIC_SYNTHETIC_UNUSED").Trim()
        Public Function OpaqueProduce() As DataSet
            Return OpaqueForward(seed, unused)
        End Function
        Public Function OpaqueRead() As Object
            Return OpaqueProduce().Tables(0).Rows(0)("Name")
        End Function
        Private Shared Function OpaqueForward(value As String, ignored As String) As DataSet
            Return EntryProtocol.ProduceInputs(1, 2, value, True)
        End Function
        Public Function OpaqueCommand() As DataSet
            Return Wrapper.ArrayInputs(seed, New ArrayList())
        End Function
        Public Function OpaqueControl() As DataSet
            If seed Is Nothing Then Return New DataSet()
            Return EntryProtocol.ProduceInputs(1, 2, "three", True)
        End Function
        Public Function OpaqueMutation() As DataSet
            seed = "changed"
            Return EntryProtocol.ProduceInputs(1, 2, seed, True)
        End Function
    End Class
    Public NotInheritable Class OpaqueInitializerEscape
        Private seed As String
        Public Sub New()
            seed = Convert.ToString(Me)
        End Sub
        Public Function OpaqueEscapeProduce() As DataSet
            Return EntryProtocol.ProduceInputs(1, 2, seed, True)
        End Function
    End Class
    Public NotInheritable Class OpaqueInitializerReference
        Private seed As Object = Environment.GetEnvironmentVariables()
        Public Function OpaqueReferenceProduce() As DataSet
            Return EntryProtocol.ProduceInputs(1, 2, Convert.ToString(seed), True)
        End Function
    End Class
    Public NotInheritable Class OpaqueInitializerCatch
        Private seed As String
        Public Sub New()
            Try
                seed = Environment.GetEnvironmentVariable("PUBLIC_SYNTHETIC_CATCH")
            Catch
                seed = "fallback"
            End Try
        End Sub
        Public Function OpaqueCatchProduce() As DataSet
            Return EntryProtocol.ProduceInputs(1, 2, seed, True)
        End Function
    End Class
    Public NotInheritable Class OpaqueInitializerLongChain
        Private seed As String = Environment.GetEnvironmentVariable("PUBLIC_SYNTHETIC_LONG").Trim().Trim().Trim().Trim().Trim().Trim().Trim().Trim()
        Public Function OpaqueLongProduce() As DataSet
            Return EntryProtocol.ProduceInputs(1, 2, seed, True)
        End Function
    End Class
End Namespace
