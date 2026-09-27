Imports System.Data
Imports System.Data.SqlClient
Imports System.Collections

' Public synthetic assembly compiled independently of the Web Site publish.
Namespace PublicProof.Framework
    Public Class PublicSqlDataAccess
        Private ReadOnly connection As SqlConnection

        Public Sub New(value As SqlConnection)
            connection = value
        End Sub

        Public Function ExecProc_DataSet(commandText As String, employeeId As String) As DataSet
            Return ExecProc_DataSet(commandText, New SqlParameter("@employeeId", employeeId))
        End Function

        Public Function ExecProc_DataSet(commandText As String, parameters As ArrayList) As DataSet
            Dim typedParameters(parameters.Count - 1) As SqlParameter
            parameters.CopyTo(typedParameters)
            Return ExecProc_DataSet(commandText, typedParameters)
        End Function

        Public Function ExecProc_DataSet(commandText As String, ParamArray parameters As SqlParameter()) As DataSet
            Dim result As New DataSet()
            Using command As New SqlCommand(commandText, connection)
                command.CommandType = CommandType.StoredProcedure
                command.Parameters.AddRange(parameters)
                If commandText.EndsWith(".NonQuery", StringComparison.Ordinal) Then
                    command.ExecuteNonQuery()
                Else
                    Using adapter As New SqlDataAdapter(command)
                        adapter.Fill(result)
                    End Using
                End If
            End Using
            Return result
        End Function
    End Class

    ' Same-named public decoys force the IL join to use declaring type and
    ' signature, rather than choosing a provider by method name.
    Public Class PublicOdbcDataAccess
        Public Function ExecProc_DataSet(commandText As String, employeeId As String) As DataSet
            Return New DataSet()
        End Function
    End Class

    Public Class PublicSqliteDataAccess
        Public Function ExecProc_DataSet(commandText As String, employeeId As String) As DataSet
            Return New DataSet()
        End Function
    End Class

    Public Class PublicLegacyDataAccess
        Public Function ExecProc_DataSet(commandText As String, employeeId As String) As DataSet
            Return New DataSet()
        End Function
    End Class

    Public Class PublicCloudDataAccess
        Public Function ExecProc_DataSet(commandText As String, employeeId As String) As DataSet
            Return New DataSet()
        End Function
    End Class
End Namespace
