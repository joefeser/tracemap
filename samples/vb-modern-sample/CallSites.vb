Option Strict On
Option Explicit On

Imports VbModernSample.Contracts
Imports VbModernSample.Domain
Imports VbModernSample.Services

Namespace CallSites

    Public Class OrderFulfillmentService

        Private ReadOnly _repository As IOrderRepository
        Private ReadOnly _utilities As New CatalogUtilities()

        Public Sub New(repository As IOrderRepository)
            _repository = repository
        End Sub

        Public Function Fulfill(order As PurchaseOrder) As String

            ' Direct interface call: argument "order" flows to parameter "order" of Save.
            _repository.Save(order)

            ' ByRef argument: the raw code variable is normalized by the callee.
            Dim customerCode As String = order.CustomerCode
            If _utilities.TryNormalizeSku(customerCode) Then
                order.CustomerCode = customerCode
            End If

            ' Positional argument plus named argument into an Optional parameter.
            Dim label As String = _utilities.BuildLabel(order.Describe(), suffix:=" [fulfilled]")

            ' ParamArray argument list expands into the tags() parameter.
            Dim tags As String = _utilities.JoinTags("priority", order.CustomerCode, "domestic")

            ' Overload resolution: Decimal total selects the Decimal Sum overload.
            Dim total As Decimal = _utilities.Sum(order.LineTotal(), 0D)

            ' Overload resolution: Integer count selects the Integer Sum overload.
            Dim lineCount As Integer = _utilities.Sum(order.Lines.Count, 0)

            ' Default property invocation: Quote(index) through the default member.
            Dim catalog As New PriceCatalog()
            catalog.RecordQuote(total)
            Dim lastQuote As Decimal = catalog(0)

            Return label & " tags=" & tags & " total=" &
                catalog.FormatPrice(lastQuote) & " lines=" &
                lineCount.ToString(System.Globalization.CultureInfo.InvariantCulture)

        End Function

        Public Function FindOrder(orderId As Integer) As PurchaseOrder
            ' Compiler-resolved call into the concrete repository when one is wired.
            Return _repository.FindById(orderId)
        End Function

    End Class

    Public Class WarehouseOrderRepository
        Implements IOrderRepository

        ' Backing field used by both interface members.
        Private ReadOnly _orders As New Dictionary(Of Integer, PurchaseOrder)()

        Public Function FindById(orderId As Integer) As PurchaseOrder Implements IOrderRepository.FindById
            Dim found As PurchaseOrder = Nothing
            If _orders.TryGetValue(orderId, found) Then
                Return found
            End If
            Return Nothing
        End Function

        Public Sub Save(order As PurchaseOrder) Implements IOrderRepository.Save
            _orders(order.Id) = order
        End Sub

    End Class

End Namespace
