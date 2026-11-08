Public Class Queries
    Public Sub ChangeStock(gateway As Object, bindings As Object)
        Dim sql As String = "UPDATE sample_stock "
        sql = sql & "SET quantity = @quantity "
        sql = sql & "WHERE item = @item"
        gateway.ExecSql_Write(sql, bindings)
    End Sub

    Public Function FindStock(gateway As Object, key As String) As Object
        Return gateway.ExecSql_Read("SELECT quantity FROM sample_stock WHERE item = '" & key & "'")
    End Function

    Public Sub LogOnly(logger As Object)
        Dim sql As String = "SELECT quantity FROM sample_stock"
        logger.Write(sql)
    End Sub

    Public Sub Replaced(gateway As Object)
        Dim sql As String = "UPDATE sample_stock SET quantity = 0"
        sql = "diagnostic text"
        gateway.ExecSql_Write(sql)
    End Sub

    Public Sub Conditional(gateway As Object, enabled As Boolean)
        Dim sql As String = "UPDATE sample_stock SET quantity = 0"
        If enabled Then sql = "diagnostic text"
        gateway.ExecSql_Write(sql)
    End Sub

    Public Sub OtherArgument(gateway As Object, message As String)
        Dim sql As String = "UPDATE sample_stock SET quantity = 0"
        gateway.ExecSql_Write(message, sql)
    End Sub
End Class
