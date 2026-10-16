' Public synthetic projectless website: no .vbproj and no executable entry point.
Public Class CatalogPage
    Public Sub Save(store As SyntheticStore)
        store.ExecuteProcedure("PublicCatalog_Save")
    End Sub

    Public Sub Search(store As SyntheticStore, fragment As String)
        store.ExecuteText("SELECT ItemId FROM PublicCatalog WHERE " & fragment)
    End Sub
End Class
