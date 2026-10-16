Imports System.Collections.Generic
Imports System.Threading.Tasks

Namespace TraceMap.CompiledFixtures.Equivalence
    Public Class StateMachineMatrix
        Public Shared Async Function AwaitOne(value As Integer) As Task(Of Integer)
            Await Task.Yield()
            Return value
        End Function

        Public Shared Iterator Function Enumerate(value As Integer) As IEnumerable(Of Integer)
            Yield value
        End Function

        ' Same name does not identify a generated state-machine endpoint.
        Public Function MoveNext() As Boolean
            Return False
        End Function
    End Class
End Namespace
