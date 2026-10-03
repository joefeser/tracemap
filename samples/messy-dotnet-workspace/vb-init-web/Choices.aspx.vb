Imports System

Public Partial Class ChoicesPage
    Inherits System.Web.UI.Page

    Protected WithEvents Name As System.Web.UI.WebControls.DropDownList

    Protected Sub Name_Init(sender As Object, e As EventArgs) Handles Name.Init
        With Name
            .Items.Clear()
            For Each choice As String In New SyntheticDataAccess().MyList
                .Items.Add(choice)
            Next
        End With
    End Sub
End Class
