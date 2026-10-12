' Public synthetic method-parameter ordinal fixture; never execute.
Imports System.Collections.Generic
Public Class Entry
    Public Shared Sub Start(Of TLeft, TRight)(items As Dictionary(Of TRight, TLeft))
        Dim result = Provider.Query("synthetic command", Nothing)
    End Sub
End Class
