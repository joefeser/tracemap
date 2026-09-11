Option Strict On
Option Explicit On

' Synthetic custom event source used by the Web Forms fixture to demonstrate
' RaiseEvent together with a WithEvents consumer in the code-behind.

Public Class StatusChangedEventArgs
    Inherits EventArgs

    Private ReadOnly _message As String

    Public Sub New(ByVal message As String)
        _message = message
    End Sub

    Public ReadOnly Property Message() As String
        Get
            Return _message
        End Get
    End Property

End Class

Public Class StatusNotifier

    Public Event StatusChanged As EventHandler(Of StatusChangedEventArgs)

    Public Sub Announce(ByVal message As String)
        RaiseEvent StatusChanged(Me, New StatusChangedEventArgs(message))
    End Sub

End Class
