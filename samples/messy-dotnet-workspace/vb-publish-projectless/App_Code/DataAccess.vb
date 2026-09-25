Imports System.Data
Imports System.Data.SqlClient

Public MustInherit Class SqlBaseDA
    Private ReadOnly connection As New SqlConnection("Data Source=(local);Initial Catalog=PublicFixture;Integrated Security=True")
    Protected Property SqlDA As PublicSqlDataAccess

    Protected Sub Open()
        SqlDA = New PublicSqlDataAccess(connection)
    End Sub
End Class

Public Class DataAccess
    Inherits SqlBaseDA

    Public Function SelectGroups(employeeId As String) As DataSet
        Open()
        Return SqlDA.ExecProc_DataSet("PublicFixture.GetGroupNames", employeeId)
    End Function
End Class

Public Class PublicSqlDataAccess
    Private ReadOnly connection As SqlConnection

    Public Sub New(value As SqlConnection)
        connection = value
    End Sub

    Public Function ExecProc_DataSet(commandText As String, employeeId As String) As DataSet
        Return ExecProc_DataSet(commandText, New SqlParameter("@employeeId", employeeId))
    End Function

    Public Function ExecProc_DataSet(commandText As String, ParamArray parameters As SqlParameter()) As DataSet
        Dim result As New DataSet()
        Using command As New SqlCommand(commandText, connection)
            command.CommandType = CommandType.StoredProcedure
            command.Parameters.AddRange(parameters)
            Using adapter As New SqlDataAdapter(command)
                adapter.Fill(result)
            End Using
        End Using
        Return result
    End Function
End Class
