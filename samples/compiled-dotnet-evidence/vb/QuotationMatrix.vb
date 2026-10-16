Imports System
Imports System.Linq.Expressions

Namespace TraceMap.CompiledFixtures.Equivalence
    Public NotInheritable Class QuotationMatrix
        Public Shared Function Tree() As Expression(Of Func(Of Integer))
            Return Function() 42
        End Function
        Public Shared Function [Delegate]() As Func(Of Integer)
            Return Function() 42
        End Function
        Public Shared Function Echo(value As Expression(Of Func(Of Integer))) As Expression(Of Func(Of Integer))
            Return value
        End Function
    End Class
End Namespace
