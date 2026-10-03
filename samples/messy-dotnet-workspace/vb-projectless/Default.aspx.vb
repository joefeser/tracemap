Imports System
Imports System.Data
Imports System.Data.SqlClient

Public Partial Class DefaultPage
    Inherits System.Web.UI.Page

    Protected Sub SubmitButton_Click(sender As Object, e As EventArgs)
        Queue.Enqueue("orders")
        Dim adapter As SqlDataAdapter = New SqlDataAdapter()
        adapter.Fill(New DataSet())
    End Sub
End Class
