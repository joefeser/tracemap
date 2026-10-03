Option Strict On
Option Explicit On

Namespace Contracts

    Public Interface IOrderRepository

        Function FindById(orderId As Integer) As PurchaseOrder

        Sub Save(order As PurchaseOrder)

    End Interface

    Public Interface IPriceFormatter

        Function Format(amount As Decimal) As String

    End Interface

    Public MustInherit Class EntityBase

        Public Property Id As Integer

        Public Overridable Function Describe() As String
            Return Me.GetType().Name & "#" & Id.ToString(System.Globalization.CultureInfo.InvariantCulture)
        End Function

    End Class

    Public Class PurchaseOrder
        Inherits EntityBase

        Public Property CustomerCode As String = ""

        Public Property Lines As New List(Of OrderLine)()

        Public Function LineTotal() As Decimal
            Dim total As Decimal = 0D
            For Each line As OrderLine In Lines
                total += line.UnitPrice * line.Quantity
            Next line
            Return total
        End Function

        Public Overrides Function Describe() As String
            Return "Order " & Id.ToString(System.Globalization.CultureInfo.InvariantCulture) &
                " for " & CustomerCode & " with " & Lines.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) & " line(s)"
        End Function

    End Class

    Public Class OrderLine

        Public Property Sku As String = ""

        Public Property Quantity As Integer

        Public Property UnitPrice As Decimal

    End Class

End Namespace
