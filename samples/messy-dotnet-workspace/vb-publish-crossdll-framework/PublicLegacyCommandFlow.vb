Imports System.Collections
Imports System.Data
Imports System.Data.SqlClient

Namespace PublicProof.Framework
    Public Class PublicLegacyCommandEntry
        Public Shared Function Run() As DataSet
            Dim parameters As ArrayList = Nothing
            Return New PublicLegacyCommandFlow().Run("public.synthetic_procedure", parameters)
        End Function
    End Class
    ' Synthetic legacy-compiler patterns only. This fixture is never executed.
    Public Structure PublicParameter
        Public Value As SqlParameter
        Public Function DebugName() As String
            Return "parameter"
        End Function
        Public Function DebugValue() As String
            Return "value"
        End Function
    End Structure

    Public Class PublicLegacyCommandFlow
        Private connection As SqlConnection
        Private transaction As SqlTransaction
        Private timeout As Integer
        Private trace As String

        Public Function Run(commandText As String, ByRef parameters As ArrayList) As DataSet
            Dim typed As PublicParameter() = Nothing
            Try
                typed = DirectCast(parameters.ToArray(GetType(PublicParameter)), PublicParameter())
            Catch
                Throw
            End Try
            Dim mappings As String() = Nothing
            Return Run(commandText, typed, mappings)
        End Function

        Public Function Run(commandText As String, ByRef parameters As PublicParameter(), ByRef mappings As String()) As DataSet
            trace = commandText & " "
            If connection Is Nothing OrElse connection.State <> ConnectionState.Open Then Return Nothing
            Try
                Dim result As New DataSet()
                Dim command As New SqlCommand(commandText, connection)
                command.CommandType = CommandType.StoredProcedure
                command.CommandTimeout = timeout
                If parameters IsNot Nothing Then
                    For Each parameter As PublicParameter In parameters
                        command.Parameters.Add(parameter.Value)
                        trace = String.Concat(New String() {trace, " ", parameter.DebugName(), "=", parameter.DebugValue()})
                    Next
                End If
                If transaction IsNot Nothing Then command.Transaction = transaction
                Dim adapter As New SqlDataAdapter(command)
                If mappings IsNot Nothing Then
                    For i As Integer = 0 To mappings.Length - 1
                        Dim tableName As String = "Table" & If(i = 0, "", CObj(i)).ToString()
                        adapter.TableMappings.Add(tableName, mappings(i).ToString())
                    Next
                End If
                adapter.Fill(result)
                Return result
            Catch
                Throw
            End Try
        End Function
    End Class
End Namespace
