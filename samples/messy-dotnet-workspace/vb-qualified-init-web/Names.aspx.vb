Imports System
Imports Synthetic.Data

Public Partial Class NamesPage
    Inherits System.Web.UI.Page

    Protected WithEvents Names As System.Web.UI.WebControls.DropDownList

    Protected Sub Names_Init(sender As Object, e As EventArgs) Handles Names.Init
        With Names
            For Each item In New ChoiceNames().MyList
                .Items.Add(New System.Web.UI.WebControls.ListItem(item))
            Next
        End With
    End Sub
End Class
