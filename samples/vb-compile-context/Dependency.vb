Imports System
Imports System.Collections
Imports System.Data
Imports System.Data.SqlClient

Namespace PublicFixture
    Public Class ParameterSpec
        Public ReadOnly Name As String
        Public ReadOnly Kind As SqlDbType
        Public ReadOnly Value As Object
        Public Sub New(ByVal name As String, ByVal kind As SqlDbType, ByVal value As Object)
            Me.Name = name
            Me.Kind = kind
            Me.Value = value
        End Sub
    End Class

    Public Class DataAccess
        Public Shared Function Execute(ByVal connectionString As String, ByVal procedure As String, ByVal parameters As ArrayList) As DataSet
            Using connection As New SqlConnection(connectionString)
                Using command As New SqlCommand(procedure, connection)
                    command.CommandType = CommandType.StoredProcedure
                    For Each item As ParameterSpec In parameters
                        Dim parameter As New SqlParameter(item.Name, item.Kind)
                        parameter.Value = item.Value
                        command.Parameters.Add(parameter)
                    Next
                    Using adapter As New SqlDataAdapter(command)
                        Dim result As New DataSet()
                        adapter.Fill(result)
                        Return result
                    End Using
                End Using
            End Using
        End Function
    End Class
End Namespace

' Deliberately conflicting public namespaces for the compiler negative control.
Namespace PublicFixture.Left.Collision
    Public Class Marker
    End Class
End Namespace
Namespace PublicFixture.Right.Collision
    Public Class Marker
    End Class
End Namespace
