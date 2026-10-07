Imports System.Collections
Imports System.Collections.Generic

Namespace TraceMap.CompiledFixtures.Equivalence
    Public Class CollectionMatrix
        Public Shared Function ReadLegacy(values As ArrayList, index As Integer) As Object
            Return values(index)
        End Function
        Public Shared Function ReadGeneric(values As List(Of Object), index As Integer) As Object
            Return values(index)
        End Function
        Public Shared Function ReadInteger(values As ArrayList, index As Integer) As Integer
            Return CInt(values(index))
        End Function
    End Class
End Namespace
