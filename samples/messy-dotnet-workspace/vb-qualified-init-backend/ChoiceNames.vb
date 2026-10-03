Imports System.Collections.Generic
Imports System.Data
Imports System.Data.SqlClient

Namespace Synthetic.Data
    Public Class ChoiceNames
        Public ReadOnly Property MyList As List(Of String)

        Public Sub New()
            MyList = New List(Of String)()
            Dim rows = New ChoiceLogic().SelectNames()
            For Each row As DataRow In rows.Tables(0).Rows
                MyList.Add(row.Item("Name").ToString())
            Next
        End Sub
    End Class

    Public Class ChoiceLogic
        Public Function SelectNames() As DataSet
            Return New ChoiceStore().SelectNames()
        End Function
    End Class

    Public Class ChoiceStore
        Public Function SelectNames() As DataSet
            Dim command As New SqlCommand()
            command.ExecuteReader()
            Return New DataSet()
        End Function
    End Class
End Namespace

' Same simple name and arity in a different namespace. It must not be selected.
Namespace Unrelated.Data
    Public Class ChoiceNames
        Public Sub New()
            Dim command As New SqlCommand()
            command.ExecuteReader()
        End Sub
    End Class
End Namespace
