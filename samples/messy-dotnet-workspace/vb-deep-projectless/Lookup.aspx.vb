Imports System.Data
Imports System.Collections
Imports PublicProof.Framework

' Public synthetic static-analysis corpus. Never execute database operations.
Partial Public Class DeepLookup
    Inherits System.Web.UI.Page

    Public Sub Lookup_Click(sender As Object, args As EventArgs)
        Dim result As DataSet = DeepLayers.Layer01("public.deep_lookup")
    End Sub

    Public Sub Branch_Click(sender As Object, args As EventArgs)
        Dim result As DataSet
        If sender Is Nothing Then
            result = DeepLayers.Layer01("public.branch_left")
        Else
            result = DeepLayers.Alternate("public.branch_right")
        End If
    End Sub

    Public Sub Cycle_Click(sender As Object, args As EventArgs)
        Dim result As DataSet = DeepLayers.CycleA("public.cycle_exit", True)
    End Sub

    Public Sub Comparison_Click(sender As Object, args As EventArgs)
        Dim result As DataSet = DeepLayers.CycleA("public.cycle_exit", sender Is Nothing)
    End Sub

    Public Sub Unknown_Click(sender As Object, args As EventArgs)
        Dim result As DataSet = DeepLayers.Layer01(Request.QueryString("procedure"))
    End Sub
End Class

Public Class DeepLayers
    Public Shared Function Layer01(text As String) As DataSet
        Return Layer02(text)
    End Function
    Public Shared Function Layer02(text As String) As DataSet
        Return Layer03(text)
    End Function
    Public Shared Function Layer03(text As String) As DataSet
        Return Layer04(text)
    End Function
    Public Shared Function Layer04(text As String) As DataSet
        Return Layer05(text)
    End Function
    Public Shared Function Layer05(text As String) As DataSet
        Return Layer06(text)
    End Function
    Public Shared Function Layer06(text As String) As DataSet
        Return Layer07(text)
    End Function
    Public Shared Function Layer07(text As String) As DataSet
        Return Layer08(text)
    End Function
    Public Shared Function Layer08(text As String) As DataSet
        Return Layer09(text)
    End Function
    Public Shared Function Layer09(text As String) As DataSet
        Return Layer10(text)
    End Function
    Public Shared Function Layer10(text As String) As DataSet
        Return Layer11(text)
    End Function
    Public Shared Function Layer11(text As String) As DataSet
        Return Layer12(text)
    End Function
    Public Shared Function Layer12(text As String) As DataSet
        Dim parameters As ArrayList = Nothing
        Return New PublicLegacyCommandFlow().Run(text, parameters)
    End Function

    Public Shared Function Alternate(text As String) As DataSet
        Return Layer07(text)
    End Function

    Public Shared Function CycleA(text As String, recurse As Boolean) As DataSet
        If recurse Then Return CycleB(text, recurse)
        Return Layer01(text)
    End Function

    Public Shared Function CycleB(text As String, recurse As Boolean) As DataSet
        Return CycleA(text, recurse)
    End Function
End Class

' These declarations must never be joined merely by the shared method name.
Public Class DeepDecoy
    Public Shared Function Layer12(text As String) As DataSet
        Return Nothing
    End Function
    Public Shared Function Run(text As String, ByRef parameters As ArrayList) As DataSet
        Return Nothing
    End Function
End Class
