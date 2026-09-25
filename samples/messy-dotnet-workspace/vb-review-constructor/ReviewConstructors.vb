Imports System.Data.SqlClient

Namespace Review
    Public Class Outer(Of T)
        Public Class Inner
            Public Sub New()
                Dim command As New SqlCommand()
                command.ExecuteReader()
            End Sub

            Public Function Choose() As String
                Return "synthetic"
            End Function
        End Class
    End Class
End Namespace

' An unqualified creation outside both namespaces cannot select either Foo.
Public Class RootCaller
    Public Sub Run()
        Dim unresolved = New Foo()
    End Sub
End Class

Namespace OtherNamespace
    Public Class Foo
        Public Sub New()
        End Sub
    End Class
End Namespace

Namespace CallerNamespace
    Public Class Foo
        Public Sub New()
            Dim marker As New Object()
        End Sub
    End Class

    Public Class ReviewCaller
        Public Sub Run()
            Dim nested = New Review.Outer(Of Integer).Inner()
            Dim selected = (New Review.Outer(Of Integer).Inner()).Choose()
            Dim unresolved = New Foo()
        End Sub
    End Class
End Namespace
