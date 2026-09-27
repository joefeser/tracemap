Imports System.Data

Public Class BusinessLogic
    Private ReadOnly DA As New DataAccess()

    Public Function SelectGroups(employeeId As String) As DataSet
        Return DA.SelectGroups(employeeId)
    End Function
End Class
