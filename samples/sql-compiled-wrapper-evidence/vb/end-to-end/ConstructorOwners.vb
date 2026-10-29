Imports System.Data
Imports System.Collections
Imports System.Web
Namespace PublicSql.VisualBasic
    Public Class ReferenceOwnerBase
        Private marker As String
        Public Sub New()
            marker = "public-base"
        End Sub
        Public ReadOnly Property BaseMarker As String
            Get
                Return marker
            End Get
        End Property
    End Class
    Public NotInheritable Class ReferenceOwnerDerived
        Inherits ReferenceOwnerBase
        Private marker As String
        Public Sub New()
            marker = "public-derived"
        End Sub
        Public ReadOnly Property DerivedMarker As String
            Get
                Return marker
            End Get
        End Property
    End Class
    Public NotInheritable Class ReferenceOwnerRoot
        Private holder As New ReferenceOwnerDerived()
        Private seed As String = System.Environment.GetEnvironmentVariable("PUBLIC_SYNTHETIC_OWNER").Trim()
        Public Function ReferenceCtorProduce() As DataSet
            Return Forward(seed, holder.BaseMarker, holder.DerivedMarker)
        End Function
        Private Shared Function Forward(value As String, first As String, second As String) As DataSet
            Return EntryProtocol.ProduceInputs(1, 2, value, True)
        End Function
        Public Function ReferenceCtorTraceProduce() As DataSet
            HttpContext.Current.Trace.Write(seed)
            Return Forward(seed, holder.BaseMarker, holder.DerivedMarker)
        End Function
        Public Function ReferenceOwnerExecutions() As DataSet
            Wrapper.ArrayInputs(holder.BaseMarker, New ArrayList())
            Return Wrapper.ArrayInputs(holder.DerivedMarker, New ArrayList())
        End Function
    End Class
End Namespace
