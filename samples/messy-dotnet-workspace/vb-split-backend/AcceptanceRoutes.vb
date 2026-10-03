Imports System.Data
Imports System.Data.SqlClient

' Public-safe split-root counterpart to vb-compound-pages. The page and this
' backend are scanned independently before their indexes are combined.
Public Class SplitAcceptanceRoutes
    Public Sub DispatchTwo()
        Dim first As New SplitTwoLane01()
        Dim second As New SplitTwoLane02()
        Dim third As New SplitTwoLane03()
        Dim fourth As New SplitTwoLane04()
        Dim fifth As New SplitTwoLane05()
        first.Process()
        second.Process()
        third.Process()
        fourth.Process()
        fifth.Process()
    End Sub

    Public Sub DispatchThree()
        Dim route As New SplitThreeLane()
        route.Process()
    End Sub

    Public Sub DispatchEleven()
        Dim route As New SplitElevenLane()
        route.Process()
    End Sub
End Class

Public Class SplitTwoLane01
    Public Sub Process()
        Dim middle As New SplitTwoLane01Middle()
        middle.Forward()
    End Sub
End Class

Public Class SplitTwoLane02
    Public Sub Process()
        Dim middle As New SplitTwoLane02Middle()
        middle.Forward()
    End Sub
End Class

Public Class SplitTwoLane03
    Public Sub Process()
        Dim middle As New SplitNoTerminalMiddle()
        middle.Forward()
    End Sub
End Class

Public Class SplitTwoLane04
    Public Sub Process()
        Dim middle As New SplitNoTerminalMiddle()
        middle.Forward()
    End Sub
End Class

Public Class SplitTwoLane05
    Public Sub Process()
        Dim middle As New SplitNoTerminalMiddle()
        middle.Forward()
    End Sub
End Class

Public Class SplitTwoLane01Middle
    Public Sub Forward()
        ContinueRoute()
    End Sub

    Private Sub ContinueRoute()
        Dim tail As New SplitTwoLane01Tail()
        tail.Execute()
    End Sub
End Class

Public Class SplitTwoLane02Middle
    Public Sub Forward()
        Me.ContinueRoute()
    End Sub

    Private Sub ContinueRoute()
        Dim tail As New SplitTwoLane02Tail()
        tail.Execute()
    End Sub
End Class

Public Class SplitTwoLane01Tail
    Public Sub Execute()
        Dim adapter As New SqlDataAdapter()
        adapter.Fill(New DataSet())
    End Sub
End Class

Public Class SplitTwoLane02Tail
    Public Sub Execute()
        Dim command As New SqlCommand()
        command.ExecuteScalar()
    End Sub
End Class

Public Class SplitNoTerminalMiddle
    Public Sub Forward()
        Dim tail As New SplitNoTerminalTail()
        tail.Finish()
    End Sub
End Class

Public Class SplitNoTerminalTail
    Public Sub Finish()
    End Sub
End Class

Public Class SplitThreeLane
    Public Sub Process()
        Dim middle As New SplitNoTerminalMiddle()
        middle.Forward()
    End Sub
End Class

Public Class SplitElevenLane
    Public Sub Process()
        Dim middle As New SplitElevenMiddle()
        middle.Forward()
    End Sub
End Class

Public Class SplitElevenMiddle
    Public Sub Forward()
        Dim tail As New SplitElevenTail()
        tail.Execute()
    End Sub
End Class

Public Class SplitElevenTail
    Public Sub Execute()
        Dim command As New SqlCommand()
        command.ExecuteNonQuery()
    End Sub
End Class
