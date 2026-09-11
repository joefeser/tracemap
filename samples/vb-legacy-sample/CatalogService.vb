Option Strict On
Option Explicit On

Imports System.Collections.Generic

' Conditional-configuration constant exercised by the #If blocks below.
#Const LegacyGatewayEnabled = True

Public Class CatalogService

    Private ReadOnly _items As New List(Of CatalogItem)

    Public Sub AddItem(ByVal item As CatalogItem)
        _items.Add(item)
    End Sub

    Public Function FindBySku(ByVal sku As String) As CatalogItem
        For Each item As CatalogItem In _items
            If String.Compare(item.Sku, sku, System.StringComparison.OrdinalIgnoreCase) = 0 Then
                Return item
            End If
        Next item
        Return Nothing
    End Function

#If LegacyGatewayEnabled Then

    ' Intentionally unresolved reference: LegacyVendor.Connectors ships only as a
    ' private vendor library (see the HintPath in VbLegacyCatalog.vbproj) and is
    ' deliberately absent from this fixture. Compiler binding here must fail and
    ' force per-file syntax fallback with an explicit gap.
    Public Function FetchVendorPrice(ByVal sku As String) As Decimal
        Dim gateway As New LegacyVendor.Connectors.Gateway()
        Dim vendorPrice As Decimal = gateway.LookupPrice(sku)
        Return vendorPrice
    End Function

#Else

    ' Compatibility branch kept for older deployments that run without the
    ' vendor gateway. Both conditional branches stay syntactically valid.
    Public Function FetchVendorPrice(ByVal sku As String) As Decimal
        Dim fallbackItem As CatalogItem = FindBySku(sku)
        If fallbackItem Is Nothing Then
            Return 0D
        End If
        Return fallbackItem.ListPrice
    End Function

#End If

#If DEBUG Then

    Public Sub DumpDiagnostics(ByVal sink As IList(Of String))
        sink.Add("items=" & _items.Count.ToString(System.Globalization.CultureInfo.InvariantCulture))
    End Sub

#End If

End Class

' Minimal "form-like" host class so the generated application designer has a
' main-form target. Synthetic and never built in this fixture.
Partial Public Class CatalogHostForm

End Class
