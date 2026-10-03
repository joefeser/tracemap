Imports System.Collections

Partial Public Class ReviewBoard
    Protected Sub SearchButton_Click(sender As Object, e As EventArgs)
        ResultsGrid.DataSource = ReviewWorkflow.LoadQueue(FilterText.Text)
        ResultsGrid.DataBind()
    End Sub

    Protected Sub ToggleButton_Click(sender As Object, e As EventArgs)
        SearchButton.Enabled = FilterText.Text.Length > 0
        ToggleButton.Text = "Updated"
    End Sub

    Protected Sub ApproveButton_Click(sender As Object, e As EventArgs)
        Dim values As New Hashtable()
        ResultsGrid.ExtractValuesFromItem(values, sender)
        ReviewWorkflow.Save(values)
    End Sub
End Class
