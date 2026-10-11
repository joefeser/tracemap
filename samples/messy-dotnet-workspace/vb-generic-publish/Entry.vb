' Public synthetic signature fixture; never execute the application or SQL.
Imports System.Collections.Generic
Public Class Entry
    Public Shared Sub Start(items As Dictionary(Of String, Integer))
        Dim result = Provider.Query("synthetic command", Nothing)
    End Sub
End Class
