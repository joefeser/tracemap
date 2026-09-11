Option Strict On
Option Explicit On

Namespace Services

    Public Class CatalogUtilities

        ' ByRef parameter: the callee assigns the parameter before returning.
        Public Function TryNormalizeSku(ByRef sku As String) As Boolean
            If sku Is Nothing Then
                Return False
            End If
            sku = sku.Trim().ToUpperInvariant()
            Return True
        End Function

        ' Optional parameter with a literal default value.
        Public Function BuildLabel(name As String, Optional suffix As String = "") As String
            Return name & suffix
        End Function

        ' ParamArray parameter.
        Public Function JoinTags(ParamArray tags() As String) As String
            Return String.Join("|", tags)
        End Function

        ' Overload pair differing only by parameter type.
        Public Overloads Function Sum(left As Integer, right As Integer) As Integer
            Return left + right
        End Function

        Public Overloads Function Sum(left As Decimal, right As Decimal) As Decimal
            Return left + right
        End Function

    End Class

    Public Class InventoryBatch

        Private ReadOnly _names As String()

        Public Sub New(names As String())
            _names = names
        End Sub

        ' Default (parameterized, read-only) property enabling batch(0) syntax.
        Default Public ReadOnly Property Item(index As Integer) As String
            Get
                Return _names(index)
            End Get
        End Property

        Public ReadOnly Property Count As Integer
            Get
                Return _names.Length
            End Get
        End Property

    End Class

End Namespace
