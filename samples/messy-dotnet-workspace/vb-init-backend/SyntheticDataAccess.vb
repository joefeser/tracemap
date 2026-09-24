Imports System.Collections.Generic
Imports System.Data
Imports System.Data.SqlClient

' The getter is populated by a constructor side effect, not by a call in the
' page handler. Names and data are entirely synthetic.
Public Class SyntheticDataAccess
    Public ReadOnly Property MyList As List(Of String)

    Public Sub New()
        MyList = New List(Of String)()
        Dim rows As DataSet = New SyntheticService().SelectChoices("east", "autumn", 2026)
        For Each row As DataRow In rows.Tables(0).Rows
            MyList.Add(row.Item("DisplayName").ToString())
        Next
    End Sub
End Class

Public Class SyntheticService
    Private ReadOnly Repository As New SyntheticRepository()

    Public Function SelectChoices(region As String, season As String, year As Integer) As DataSet
        Return Repository.SelectChoices(region, season, year)
    End Function
End Class

Public Class SyntheticRepository
    Private ReadOnly Gateway As New SyntheticSqlGateway()

    Public Function SelectChoices(region As String, season As String, year As Integer) As DataSet
        Return Gateway.ExecuteDataSet("synthetic.lookup", region, season, year)
    End Function
End Class

Public Class SyntheticSqlGateway
    Public Function ExecuteDataSet(procedureName As String, region As String, season As String, year As Integer) As DataSet
        Dim command As New SqlCommand()
        command.ExecuteReader()
        Return New DataSet()
    End Function
End Class
