Imports System.Configuration
Imports System.Data
Namespace PublicSql.VisualBasic
    ' Compile-only metadata: builds never execute configuration access.
    Public NotInheritable Class ExternalConfigurationRoot
        Private seed As String = ConfigurationManager.ConnectionStrings("PublicSyntheticEntry").ConnectionString.Trim()
        Public Function ExternalConfigurationProduce() As DataSet
            Return EntryProtocol.ProduceInputs(1, 2, seed, True)
        End Function
    End Class
    Public NotInheritable Class CompiledOnlyConfigurationRoot
        Private seed As String = Wrapper.PublicScalarEcho("public-known")
        Public Function CompiledOnlyConfigurationProduce() As DataSet
            Return EntryProtocol.ProduceInputs(1, 2, seed, True)
        End Function
    End Class
    Public NotInheritable Class CompiledConfigurationRoot
        Private seed As String = Wrapper.PublicScalarEcho(ConfigurationManager.ConnectionStrings("PublicSyntheticEntry").ConnectionString.Trim())
        Public Function CompiledConfigurationProduce() As DataSet
            Return EntryProtocol.ProduceInputs(1, 2, seed, True)
        End Function
    End Class
End Namespace
