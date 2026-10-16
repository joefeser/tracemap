Imports System.Data

' Public synthetic source-only controls. Never execute these methods.
Public Class QualifiedCommands
    Public Sub Direct(command As SqlClient.SqlCommand)
        command.ExecuteNonQuery()
    End Sub

    Public Sub Lifecycle(connection As SqlClient.SqlConnection)
        connection.Open()
        connection.Close()
    End Sub

    Public Sub Unrelated(command As Unrelated.SqlCommand)
        command.ExecuteNonQuery()
    End Sub
End Class

Namespace Unrelated
    Public Class SqlCommand
        Public Sub ExecuteNonQuery()
        End Sub
    End Class
End Namespace
