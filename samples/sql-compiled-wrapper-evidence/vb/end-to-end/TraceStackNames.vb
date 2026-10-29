Imports System.Data
Imports System.Diagnostics
Imports System.Web
Namespace PublicSql.VisualBasic
    Public NotInheritable Class TraceStackNameRoot
        Public Function TraceStackNamesProduce(message As String) As DataSet
            HttpContext.Current.Trace.Write("public-category", String.Concat(New String() {
                New StackTrace().GetFrame(0).GetMethod().Name,
                "/",
                New StackTrace().GetFrame(1).GetMethod().Name }))
            Return EntryProtocol.ProduceInputs(1, 2, message, True)
        End Function
        Public Function TraceLongStackNamesProduce(message As String) As DataSet
            HttpContext.Current.Trace.Write("public-long", String.Concat(New String() {
                New StackTrace().GetFrame(0).GetMethod().Name,
                New StackTrace().GetFrame(1).GetMethod().Name,
                New StackTrace().GetFrame(2).GetMethod().Name,
                New StackTrace().GetFrame(3).GetMethod().Name,
                New StackTrace().GetFrame(4).GetMethod().Name,
                New StackTrace().GetFrame(5).GetMethod().Name,
                New StackTrace().GetFrame(6).GetMethod().Name,
                New StackTrace().GetFrame(7).GetMethod().Name }))
            Return EntryProtocol.ProduceInputs(1, 2, message, True)
        End Function
        Public Function TraceDynamicFrameRefusal(frame As Integer) As DataSet
            HttpContext.Current.Trace.Write(New StackTrace().GetFrame(frame).GetMethod().Name)
            Return EntryProtocol.ProduceInputs(1, 2, "public", True)
        End Function
        Public Function TraceGeneralReflectionRefusal() As DataSet
            HttpContext.Current.Trace.Write(GetType(String).GetMethods()(0).Name)
            Return EntryProtocol.ProduceInputs(1, 2, "public", True)
        End Function
    End Class
End Namespace
