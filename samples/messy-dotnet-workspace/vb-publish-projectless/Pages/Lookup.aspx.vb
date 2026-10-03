Imports System

Partial Public Class LookupPage
    Inherits System.Web.UI.Page

    Protected Sub Names_Init(sender As Object, e As EventArgs) Handles Names.Init
        With Names
            For Each item As GroupItem In New GroupOptions().MyList
                .Items.Add(New System.Web.UI.WebControls.ListItem With {
                    .Text = item.Name,
                    .Value = item.Identifier
                })
            Next
        End With
    End Sub
End Class
