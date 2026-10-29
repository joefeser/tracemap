Imports System.Data
Imports System.Web
Namespace PublicSql.VisualBasic
    Public NotInheritable Class TraceArgumentRoot
        Public Function ScalarCopyProduce(message As String) As DataSet
            System.Runtime.CompilerServices.RuntimeHelpers.GetObjectValue(message)
            Return EntryProtocol.ProduceInputs(1, 2, message, True)
        End Function
        Public Function UntypedCopyRefusal(value As Object) As DataSet
            System.Runtime.CompilerServices.RuntimeHelpers.GetObjectValue(value)
            Return EntryProtocol.ProduceInputs(1, 2, "public", True)
        End Function
        Public Function TrackedCopyRefusal() As DataSet
            System.Runtime.CompilerServices.RuntimeHelpers.GetObjectValue(New System.Data.SqlClient.SqlCommand("public"))
            Return EntryProtocol.ProduceInputs(1, 2, "public", True)
        End Function
        Public Function TraceComputedProduce(message As String, count As Integer) As DataSet
            HttpContext.Current.Trace.Write(String.Concat("public-", message, count.ToString()))
            Return EntryProtocol.ProduceInputs(1, 2, message, True)
        End Function
        Public Function TraceBuiltLocalProduce(message As String, count As Integer) As DataSet
            Dim local = String.Format("public-{0}", count)
            HttpContext.Current.Trace.Write(local)
            Return EntryProtocol.ProduceInputs(1, 2, local, True)
        End Function
        Public Function TraceArrayProduce(message As String, count As Integer) As DataSet
            HttpContext.Current.Trace.Write(String.Concat(New Object() { "public-", message, count }))
            Return EntryProtocol.ProduceInputs(1, 2, message, True)
        End Function
        Public Function TraceObjectConcatRefusal(value As Object) As DataSet
            HttpContext.Current.Trace.Write(String.Concat("public-", value))
            Return EntryProtocol.ProduceInputs(1, 2, "value", True)
        End Function
        Public Function TraceUnknownBuilderRefusal(message As String) As DataSet
            HttpContext.Current.Trace.Write(UnknownBuilder(message))
            Return EntryProtocol.ProduceInputs(1, 2, message, True)
        End Function
        Private Shared Function UnknownBuilder(message As String) As String
            Return message
        End Function
        Public Function TraceDelegateOpcodeRefusal(message As String) As DataSet
            Dim callback As New System.Func(Of String, String)(AddressOf PublicIdentity)
            System.GC.KeepAlive(callback)
            Return EntryProtocol.ProduceInputs(1, 2, message, True)
        End Function
        Private Shared Function PublicIdentity(value As String) As String
            Return value
        End Function
        Public Function TraceComputedCommandRefusal(message As String) As DataSet
            Dim command = String.Concat("public-", message)
            Return Wrapper.ArrayInputs(command, New System.Collections.ArrayList())
        End Function
    End Class
End Namespace
