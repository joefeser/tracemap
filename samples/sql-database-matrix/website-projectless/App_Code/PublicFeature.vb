Imports System.Data.Common
Public Class PublicFeature
    Public Sub Entry(command As DbCommand)
        Wrapper(command)
    End Sub
    Public Sub Wrapper(command As DbCommand)
        command.ExecuteScalar()
    End Sub
End Class
