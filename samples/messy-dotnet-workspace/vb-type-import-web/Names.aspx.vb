Imports System
Imports Synthetic.Data.ChoiceNames

Public Partial Class NamesPage
    Inherits System.Web.UI.Page

    Protected WithEvents Names As System.Web.UI.WebControls.DropDownList

    Protected Sub Names_Init(sender As Object, e As EventArgs) Handles Names.Init
        For Each item In New ChoiceNames().MyList
            Names.Items.Add(New System.Web.UI.WebControls.ListItem(item))
        Next
    End Sub
End Class
