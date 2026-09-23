Imports System.Data
Imports System.Data.SqlClient

' Each page enters through one typed receiver. Five classes deliberately share
' the same Process name in this one file; every next hop must stay in its own
' containing type. These are synthetic public-safe identities.
Public Class AcceptanceRoutes
    Public Sub DispatchTwo()
        Dim first As New TwoLane01()
        Dim second As New TwoLane02()
        Dim third As New TwoLane03()
        Dim fourth As New TwoLane04()
        Dim fifth As New TwoLane05()
        first.Process()
        second.Process()
        third.Process()
        fourth.Process()
        fifth.Process()
    End Sub

    Public Sub DispatchThree()
        Dim route As New ThreeLane()
        route.Process()
    End Sub

    Public Sub DispatchEleven()
        Dim route As New ElevenLane()
        route.Process()
    End Sub
End Class

Public Class TwoLane01
    Public Sub Process()
        Dim middle As New TwoLane01Middle()
        middle.Forward()
    End Sub
End Class

Public Class TwoLane02
    Public Sub Process()
        Dim middle As New TwoLane02Middle()
        middle.Forward()
    End Sub
End Class

Public Class TwoLane03
    Public Sub Process()
        Dim middle As New NoTerminalMiddle()
        middle.Forward()
    End Sub
End Class

Public Class TwoLane04
    Public Sub Process()
        Dim middle As New NoTerminalMiddle()
        middle.Forward()
    End Sub
End Class

Public Class TwoLane05
    Public Sub Process()
        Dim middle As New NoTerminalMiddle()
        middle.Forward()
    End Sub
End Class

Public Class TwoLane01Middle
    Public Sub Forward()
        Dim tail As New TwoLane01Tail()
        tail.Execute()
    End Sub
End Class

Public Class TwoLane02Middle
    Public Sub Forward()
        Dim tail As New TwoLane02Tail()
        tail.Execute()
    End Sub
End Class

Public Class TwoLane01Tail
    Public Sub Execute()
        Dim adapter As New SqlDataAdapter()
        adapter.Fill(New DataSet())
    End Sub
End Class

Public Class TwoLane02Tail
    Public Sub Execute()
        Dim command As New SqlCommand()
        command.ExecuteScalar()
    End Sub
End Class

Public Class NoTerminalMiddle
    Public Sub Forward()
        Dim tail As New NoTerminalTail()
        tail.Finish()
    End Sub
End Class

Public Class NoTerminalTail
    Public Sub Finish()
    End Sub
End Class

Public Class ThreeLane
    Public Sub Process()
        Dim middle As New NoTerminalMiddle()
        middle.Forward()
    End Sub
End Class

Public Class ElevenLane
    Public Sub Process()
        Dim middle As New ElevenMiddle()
        middle.Forward()
    End Sub
End Class

Public Class ElevenMiddle
    Public Sub Forward()
        Dim tail As New ElevenTail()
        tail.Execute()
    End Sub
End Class

Public Class ElevenTail
    Public Sub Execute()
        Dim command As New SqlCommand()
        command.ExecuteNonQuery()
    End Sub
End Class
