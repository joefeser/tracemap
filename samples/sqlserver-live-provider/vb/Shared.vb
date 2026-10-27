Imports System.Data
Imports Microsoft.Data.SqlClient
Namespace LinkedVisualBasic
' Phase 6: one Friend helper in its own document; its call site in Linked.vb is enumerated across the compilation.
Friend Module SharedData
    Friend Function ReadShared(connection As SqlConnection, procedure As String) As DataTable
        Using command As New SqlCommand(procedure)
            command.CommandType = CommandType.StoredProcedure
            command.Connection = connection
            Using adapter As New SqlDataAdapter(command)
                Dim table As New DataTable()
                adapter.Fill(table)
                Return table
            End Using
        End Using
    End Function
End Module
End Namespace
