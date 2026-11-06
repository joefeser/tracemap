Imports System.Collections
Imports System.Data

' Independently authored public evidence fixtures. Never invoke these methods.
Public Class WritePage
    Public Sub UpdateWindowTextControl(store As SyntheticStore)
        Dim sql As String = "UPDATE dbo.PublicWindow "
        sql = sql & "SET Enabled = @enabled "
        sql = sql & "WHERE Code = @code"
        store.ExecuteUpdateText(sql)
    End Sub

    Public Sub UpdateWindow(store As SyntheticStore, code As String)
        Dim parameters As New ArrayList()
        parameters.Add(New SyntheticParameter("@code", SqlDbType.VarChar, code))
        Dim sql As String = "UPDATE dbo.PublicWindow "
        sql = sql & "SET Enabled = @enabled "
        sql = sql & "WHERE Code = @code"
        parameters.Add(New SyntheticParameter("@enabled", SqlDbType.Int, 1))
        store.ExecuteUpdate(sql, parameters)
    End Sub

    Public Sub ReplaceRows(store As SyntheticStore, connection As SqlClient.SqlConnection, rows As DataTable)
        store.ReplaceRows(connection, rows)
    End Sub
End Class
