Option Strict Off
Option Explicit On

' Synthetic legacy model file in the older project-level default namespace style
' (no explicit Namespace block; RootNamespace supplies the qualifier).

Public Class CatalogItem

    Private _sku As String
    Private _displayName As String
    Private _listPrice As Decimal

    Public Sub New()
        _sku = ""
        _displayName = ""
        _listPrice = 0D
    End Sub

    Public Property Sku() As String
        Get
            Return _sku
        End Get
        Set(ByVal value As String)
            _sku = value
        End Set
    End Property

    Public Property DisplayName() As String
        Get
            Return _displayName
        End Get
        Set(ByVal value As String)
            _displayName = value
        End Set
    End Property

    Public Property ListPrice() As Decimal
        Get
            Return _listPrice
        End Get
        Set(ByVal value As Decimal)
            _listPrice = value
        End Set
    End Property

End Class

Public Class CatalogSnapshot

    Private _items As New List(Of CatalogItem)

    Public ReadOnly Property Items() As List(Of CatalogItem)
        Get
            Return _items
        End Get
    End Property

    Public Function Count() As Integer
        Return _items.Count
    End Function

End Class
