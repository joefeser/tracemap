Imports System.Collections.Generic
Imports System.Data
Imports System.Data.SqlClient

' The getter is populated by a constructor side effect, not by a call in the
' page handler. Names and data are entirely synthetic.
Public Class SyntheticDataAccess
    Public ReadOnly Property MyList As List(Of String)

    Public Sub New()
        Dim source As New SyntheticListSource()
        MyList = source.LoadChoices()
    End Sub
End Class

Public Class SyntheticListSource
    Public Function LoadChoices() As List(Of String)
        Dim adapter As New SqlDataAdapter()
        adapter.Fill(New DataSet())
        Return New List(Of String)()
    End Function
End Class
