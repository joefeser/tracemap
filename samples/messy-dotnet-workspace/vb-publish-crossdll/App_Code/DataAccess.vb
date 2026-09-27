Imports System.Data
Imports System.Data.SqlClient
Imports PublicProof.Framework

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
