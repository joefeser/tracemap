Public Class Controller
    Public Shared Property Current As Holder
End Class
Public Class Holder
    Public Property Store As Worker
End Class
Public Class Worker
    Public Sub Save()
        Dim command As New System.Data.SqlClient.SqlCommand()
        command.ExecuteNonQuery()
        Dim copy As New System.Data.SqlClient.SqlBulkCopy("synthetic")
        copy.WriteToServer(Nothing)
    End Sub
End Class
