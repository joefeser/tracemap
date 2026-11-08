Public Class Queries
    Public Sub ChangeStock(gateway As IStockGateway, bindings As System.Collections.ArrayList)
        Dim sql As String = "UPDATE sample_stock "
        sql = sql & "SET quantity = @quantity "
        sql = sql & "WHERE item = @item"
        gateway.ExecSql_Write(sql, bindings)
    End Sub

    Public Function FindStock(gateway As IStockGateway, key As String) As String
        Return gateway.ExecSql_Read("SELECT quantity FROM sample_stock WHERE item = '" & key & "'")
    End Function

    Public Sub LogOnly(logger As IReviewLog)
        Dim sql As String = "SELECT quantity FROM sample_stock"
        logger.Write(sql)
    End Sub

    Public Sub Replaced(gateway As IStockGateway)
        Dim sql As String = "UPDATE sample_stock SET quantity = 0"
        sql = "diagnostic text"
        gateway.ExecSql_Write(sql)
    End Sub

    Public Sub Conditional(gateway As IStockGateway, enabled As Boolean)
        Dim sql As String = "UPDATE sample_stock SET quantity = 0"
        If enabled Then sql = "diagnostic text"
        gateway.ExecSql_Write(sql)
    End Sub

    Public Sub OtherArgument(gateway As IStockGateway, message As String)
        Dim sql As String = "UPDATE sample_stock SET quantity = 0"
        gateway.ExecSql_Read(message, sql)
    End Sub
End Class

Public Interface IStockGateway
    Sub ExecSql_Write(text As String, Optional bindings As System.Collections.ArrayList = Nothing)
    Function ExecSql_Read(text As String, Optional detail As String = Nothing) As String
End Interface

Public Interface IReviewLog
    Sub Write(message As String)
End Interface
