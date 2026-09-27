Imports System.Data
Imports System.Data.SqlClient
Imports System.Collections
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
        Dim parameters As New ArrayList From {New SqlParameter("@employeeId", employeeId)}
        Return SqlDA.ExecProc_DataSet("PublicFixture.GetGroupNames", parameters)
    End Function
End Class
