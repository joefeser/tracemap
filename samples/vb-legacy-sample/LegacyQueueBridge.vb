Option Strict Off
Option Explicit On

' Late-binding bridge typical of legacy VB projects. CreateObject returns
' System.Object and every member call on it is late bound, so compiler-resolved
' call evidence is impossible here by design; the adapter must record the
' limitation instead of guessing a target.

Public Class LegacyQueueBridge

    Private ReadOnly _queueProgramId As String = "LegacyVendor.QueueManager"

    Public Function EnqueueMessage(ByVal message As String) As Boolean
        Dim queue As Object = CreateObject(_queueProgramId)
        queue.Enqueue(message)
        Return True
    End Function

    Public Function QueueDepth() As Integer
        Dim queue As Object = CreateObject(_queueProgramId)
        Dim depth As Integer = queue.Depth()
        Return depth
    End Function

End Class
