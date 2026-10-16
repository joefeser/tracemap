' Public synthetic projectless VB source; never execute the database operation.
Imports System.Data
Public Class Entry
    Public Shared Sub Start()
        Dim id As Integer = 0
        Dim result = Helper(id)
    End Sub
    Private Shared Function Helper(ByRef id As Integer) As DataSet
        Return Provider.Query("synthetic command", Nothing)
    End Function
End Class
