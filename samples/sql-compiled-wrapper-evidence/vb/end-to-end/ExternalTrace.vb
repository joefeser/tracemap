Imports System.Data
Imports System.Web
Namespace PublicSql.VisualBasic
    ' Compile-only synthetic trace shapes. Builds never invoke these methods.
    Public NotInheritable Class ExternalTraceRoot
        Public Function TraceProducer(message As String) As DataSet
            HttpContext.Current.Trace.Write("public-before")
            Dim result = EntryProtocol.ProduceInputs(1, 2, message, True)
            HttpContext.Current.Trace.Write("public-category", message)
            Return result
        End Function
        Public Function TraceLocalString(message As String) As DataSet
            Dim copy = message
            HttpContext.Current.Trace.Write(copy)
            Return EntryProtocol.ProduceInputs(1, 2, message, True)
        End Function
        Public Function TraceSeparateExecutions() As DataSet
            HttpContext.Current.Trace.Write("first")
            EntryProtocol.ProduceInputs(1, 2, "first", True)
            HttpContext.Current.Trace.Write("second")
            Return EntryProtocol.ProduceInputs(3, 4, "second", False)
        End Function
        Public Function TraceConditional(enabled As Boolean) As DataSet
            If enabled Then Return EntryProtocol.ProduceInputs(1, 2, "value", True)
            HttpContext.Current.Trace.Write("conditional")
            Return EntryProtocol.ProduceInputs(1, 2, "value", True)
        End Function
        Public Function TraceGuarded() As DataSet
            If HttpContext.Current IsNot Nothing Then HttpContext.Current.Trace.Write("guarded")
            Return EntryProtocol.ProduceInputs(1, 2, "value", True)
        End Function
        Public Function TraceEscape() As DataSet
            Dim context = HttpContext.Current
            System.GC.KeepAlive(context)
            Return EntryProtocol.ProduceInputs(1, 2, "value", True)
        End Function
        Public Function TraceWrongOverload() As DataSet
            HttpContext.Current.Trace.Write("category", "message", New System.Exception())
            Return EntryProtocol.ProduceInputs(1, 2, "value", True)
        End Function
        Public Function TraceTrackedArgument(value As Object) As DataSet
            HttpContext.Current.Trace.Write(value.ToString())
            Return EntryProtocol.ProduceInputs(1, 2, "value", True)
        End Function
        Public Function TraceTooMany() As DataSet
            HttpContext.Current.Trace.Write("public-limit")
            HttpContext.Current.Trace.Write("public-limit")
            HttpContext.Current.Trace.Write("public-limit")
            HttpContext.Current.Trace.Write("public-limit")
            HttpContext.Current.Trace.Write("public-limit")
            HttpContext.Current.Trace.Write("public-limit")
            HttpContext.Current.Trace.Write("public-limit")
            HttpContext.Current.Trace.Write("public-limit")
            HttpContext.Current.Trace.Write("public-limit")
            Return EntryProtocol.ProduceInputs(1, 2, "value", True)
        End Function
        Public Function TraceRead() As Object
            Return TraceProducer("value").Tables(0).Rows(0)("Name")
        End Function
    End Class
    Public NotInheritable Class OpaqueExternalTraceRoot
        Private seed As String = System.Environment.GetEnvironmentVariable("PUBLIC_SYNTHETIC_TRACE").Trim()
        Public Function OpaqueTraceProducer() As DataSet
            HttpContext.Current.Trace.Write(seed)
            Return EntryProtocol.ProduceInputs(1, 2, seed, True)
        End Function
    End Class
End Namespace
