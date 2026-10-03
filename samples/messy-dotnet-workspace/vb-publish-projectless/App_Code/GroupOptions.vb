Imports System
Imports System.Collections.Generic
Imports System.Data

Public Class GroupItem
    Public Property Identifier As String
    Public Property Name As String
End Class

Public Class GroupOptions
    Public ReadOnly MyList As List(Of GroupItem)

    Public Sub New()
        MyList = New List(Of GroupItem)()
        Dim result As DataSet = New BusinessLogic().SelectGroups("fixture")
        If result Is Nothing OrElse result.Tables.Count = 0 Then Return
        For Each row As DataRow In result.Tables(0).Rows
            MyList.Add(New GroupItem With {
                .Identifier = Convert.ToString(row("id")),
                .Name = Convert.ToString(row("name"))
            })
        Next
    End Sub
End Class
