Imports System.Data
Imports System.Data.SqlClient

Namespace PublicLazy.Framework
    ' Synthetic analysis-only provider, compiled into a separate assembly.
    Public Class PublicLog
        Public Sub InsertLog(message As String)
            Dim text As String = BuildText(message)
            ExecuteText(text)
        End Sub

        Public Sub InsertLiteral()
            ExecuteText("SELECT 1 /* public literal control */")
        End Sub

        Public Sub InsertReturnedLiteral()
            ExecuteText(LiteralText())
        End Sub

        Private Shared Function LiteralText() As String
            Return "SELECT 1 /* public literal control */"
        End Function

        Private Function BuildText(message As String) As String
            ' A returned string is deliberately distinct from caller-slot forwarding.
            Return String.Concat("SELECT LEN('", message.Replace("'", "''"), "')")
        End Function

        Public Overridable Function ExecuteText(text As String) As Object
            Dim command As New SqlCommand()
            command.CommandText = text
            command.CommandType = CommandType.Text
            Return command.ExecuteScalar()
        End Function
    End Class

    Public Class PublicQueries
        Public Shared Function GetRegion(user As String) As String
            Return user
        End Function

        Public Shared Sub Lookup(region As String)
            Dim command As New SqlCommand("public.synthetic_lookup")
            command.CommandType = CommandType.StoredProcedure
            command.Parameters.AddWithValue("@region", region)
            Dim adapter As New SqlDataAdapter(command)
            adapter.Fill(New DataSet())
        End Sub
    End Class
End Namespace
