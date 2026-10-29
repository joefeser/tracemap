Imports System
Imports System.Collections.Generic
Imports System.Data
Imports System.Linq
Namespace PublicSql.VisualBasic
    Public NotInheritable Class GroupItem
        Public Property Label As String
    End Class
    Public NotInheritable Class RowProjection
        Public Property Identifier As Integer
        Public Property Caption As String
    End Class
    Public Module LinqPipelines
        Public Function GroupObjects(values As IEnumerable(Of GroupItem), threshold As Integer) As Object
            Return values.GroupBy(Function(value) value).
                Select(Function(group) New With {Key .Item = group.Key, Key .Total = group.Count()}).
                Where(Function(item) item.Total = threshold).ToList()
        End Function
        Public Function ProjectRows(table As DataTable) As List(Of RowProjection)
            Return table.AsEnumerable().Select(Function(row) New RowProjection With {
                .Identifier = CType(row("public_identifier").ToString().Trim(), Integer),
                .Caption = row("public_caption").ToString()}).ToList()
        End Function
        Public Function ProjectDataSet(data As DataSet) As List(Of RowProjection)
            Return data.Tables(0).AsEnumerable().Select(Function(row) New RowProjection With {
                .Identifier = CType(row("public_identifier"), Integer), .Caption = row("public_caption").ToString()}).ToList()
        End Function
        Public Function DynamicColumnRefusal(table As DataTable, column As String) As List(Of RowProjection)
            Return table.AsEnumerable().Select(Function(row) New RowProjection With {.Caption = row(column).ToString()}).ToList()
        End Function
        Public Function HelperRefusal(table As DataTable) As List(Of RowProjection)
            Return table.AsEnumerable().Select(Function(row) New RowProjection With {.Caption = Build(row("public_caption"))}).ToList()
        End Function
        Private Function Build(value As Object) As String
            Return value.ToString()
        End Function
        Public Function ComparerRefusal(values As IEnumerable(Of GroupItem), threshold As Integer) As Object
            Return values.GroupBy(Function(value) value, EqualityComparer(Of GroupItem).Default).
                Select(Function(group) New With {Key .Item = group.Key, Key .Total = group.Count()}).
                Where(Function(item) item.Total = threshold).ToList()
        End Function
        Public Function IndexedSelectorRefusal(table As DataTable) As List(Of RowProjection)
            Return table.AsEnumerable().Select(Function(row, index) New RowProjection With {.Identifier = index}).ToList()
        End Function
    End Module
End Namespace
