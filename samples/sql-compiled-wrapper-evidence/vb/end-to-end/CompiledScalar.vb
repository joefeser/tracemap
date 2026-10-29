Imports System.Configuration
Imports System.Data
Imports System.Web
Namespace PublicSql.VisualBasic
    ' Public compile-only metadata. Builds never invoke configuration or helpers.
    Public NotInheritable Class CompiledScalarRoot
        Private holder As New ReferenceOwnerDerived()
        Private seed As String = Wrapper.PublicScalarTransform(ConfigurationManager.ConnectionStrings("PublicSyntheticScalar").ConnectionString)
        Public Function CompiledScalarTraceProduce() As DataSet
            HttpContext.Current.Trace.Write(seed)
            Return Forward(seed, holder.BaseMarker)
        End Function
        Private Shared Function Forward(value As String, ignored As String) As DataSet
            Return EntryProtocol.ProduceInputs(1, 2, value, True)
        End Function
        Public Function CompiledScalarCommand() As DataSet
            Return Wrapper.ArrayInputs(seed, New System.Collections.ArrayList())
        End Function
        Public Function CompiledScalarControl() As DataSet
            If seed Is Nothing Then Return New DataSet()
            Return EntryProtocol.ProduceInputs(1, 2, seed, True)
        End Function
    End Class
    Public NotInheritable Class CompiledScalarWriteRoot
        Private seed As String = Wrapper.PublicScalarWrites(ConfigurationManager.ConnectionStrings("PublicSyntheticScalar").ConnectionString)
        Public Function CompiledScalarWriteProduce() As DataSet
            Return EntryProtocol.ProduceInputs(1, 2, seed, True)
        End Function
    End Class
    Public NotInheritable Class CompiledScalarThrowRoot
        Private seed As String = Wrapper.PublicScalarThrows(ConfigurationManager.ConnectionStrings("PublicSyntheticScalar").ConnectionString)
        Public Function CompiledScalarThrowProduce() As DataSet
            Return EntryProtocol.ProduceInputs(1, 2, seed, True)
        End Function
    End Class
End Namespace
