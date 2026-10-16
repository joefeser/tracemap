Imports System.Data

Public Class NestedWidget
    Inherits UnavailableWidgetBase
    Private Sub Save_Click(sender As Object, e As System.EventArgs) Handles saveButton.Click
        Dim service As New NestedService()
        service.Save()
    End Sub
End Class

Public Class NestedService
    Private store As New DataStore()
    Public Sub Save()
        store.WriteRows(Nothing)
    End Sub
    Protected Class DataStore
        Public Sub WriteRows(rows As DataTable)
            Dim command As New SqlClient.SqlCommand()
            command.ExecuteNonQuery()
            Using copy As New SqlClient.SqlBulkCopy("synthetic-not-a-connection")
                copy.WriteToServer(rows)
            End Using
        End Sub
    End Class
End Class

' Same short names must not steal the lexically nested receiver target.
Public Class DataStore
    Public Sub WriteRows(rows As DataTable)
    End Sub
End Class
Public Class OtherService
    Protected Class DataStore
        Public Sub WriteRows(rows As DataTable)
        End Sub
    End Class
End Class
