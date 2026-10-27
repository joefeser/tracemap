Imports System.Data
Imports System.Data.SqlClient
Namespace PublicSql.VisualBasic
    ' Compile-only: never invoke these methods or connect to a provider.
    Public NotInheritable Class Caller
        Private destination As DataSet
        Public Function Direct() As Object
            Dim result = Wrapper.Direct("public.lookup")
            Return result.Tables(0).Rows(0)("Name")
        End Function
        Public Function Forwarded() As Object
            Dim result = Wrapper.Forwarded("public.lookup", New SqlParameter("@Name", SqlDbType.NVarChar, 40))
            Return result.Tables(0).Rows(0)("Name")
        End Function
        Public Function FieldObject() As Object
            Dim result = Wrapper.FieldObject("public.lookup", New FieldParameter("@Name", SqlDbType.NVarChar, 40))
            Return result.Tables(0).Rows(0)("Name")
        End Function
        Public Function PropertyObject() As Object
            Dim result = Wrapper.PropertyObject("public.lookup", New PropertyParameter("@Name", SqlDbType.NVarChar, 40))
            Return result.Tables(0).Rows(0)("Name")
        End Function
        Public Function Mutated() As Object
            Dim result = Wrapper.Mutated("public.lookup")
            Return result.Tables(0).Rows(0)("Name")
        End Function
        Public Function Escaped() As Object
            Dim result = Wrapper.Escaped("public.lookup")
            Return result.Tables(0).Rows(0)("Name")
        End Function
        Public Function Branch(condition As Boolean) As Object
            Dim result = Wrapper.Branch("public.lookup", condition)
            Return result.Tables(0).Rows(0)("Name")
        End Function
        Public Function MergeFresh() As Object
            destination = New DataSet()
            destination.Merge(Wrapper.Direct("public.lookup"))
            Return destination.Tables(0).Rows(0)("Name")
        End Function
        Public Function MergeExisting(existing As DataSet) As Object
            destination = existing
            destination.Merge(Wrapper.Direct("public.lookup"))
            Return destination.Tables(0).Rows(0)("Name")
        End Function
        Public Function MergeMutated() As Object
            destination = New DataSet()
            destination.Merge(Wrapper.Direct("public.lookup"))
            destination.Tables(0).Columns.Remove("Name")
            Return destination.Tables(0).Rows(0)("Name")
        End Function
    End Class
End Namespace
