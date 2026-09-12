Option Strict On
Option Explicit On

Namespace Domain

    ' Delegate declaration with a custom signature.
    Public Delegate Function PriceFormatter(amount As Decimal) As String

    ' Event-args payload used with the EventHandler(Of T) pattern.
    Public Class PriceRecalculatedEventArgs
        Inherits EventArgs

        Public Property OldValue As Decimal

        Public Property NewValue As Decimal

        Public Sub New(oldValue As Decimal, newValue As Decimal)
            Me.OldValue = oldValue
            Me.NewValue = newValue
        End Sub

    End Class

    Public Class PriceCatalog

        Public Const MaxLineCount As Integer = 200

        ' Plain field holding a delegate value.
        Private ReadOnly _formatter As PriceFormatter

        ' Instance field with an initializer (object creation evidence).
        Private ReadOnly _recentQuotes As New List(Of Decimal)()

        ' Event declared with the shared EventHandler(Of T) delegate type.
        Public Event PriceRecalculated As EventHandler(Of PriceRecalculatedEventArgs)

        ' Default (parameterized) property on a collection-shaped catalog.
        Default Public ReadOnly Property Quote(index As Integer) As Decimal
            Get
                Return _recentQuotes(index)
            End Get
        End Property

        Public ReadOnly Property RecentQuoteCount As Integer
            Get
                Return _recentQuotes.Count
            End Get
        End Property

        Public Property CurrencyCode As String = "USD"

        Public Sub New()
            Me.New(AddressOf FormatWithTwoDecimals)
        End Sub

        Public Sub New(formatter As PriceFormatter)
            _formatter = formatter
        End Sub

        ' Shared method used as a delegate target.
        Public Shared Function FormatWithTwoDecimals(amount As Decimal) As String
            Return amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
        End Function

        Public Function FormatPrice(amount As Decimal) As String
            Return _formatter.Invoke(amount)
        End Function

        Public Function RecordQuote(amount As Decimal) As Decimal
            Dim previous As Decimal = 0D
            If _recentQuotes.Count > 0 Then
                previous = _recentQuotes(_recentQuotes.Count - 1)
            End If
            _recentQuotes.Add(amount)
            If previous <> amount Then
                OnPriceRecalculated(previous, amount)
            End If
            Return amount
        End Function

        ' Delegate invocation through the event pattern.
        Protected Sub OnPriceRecalculated(previousValue As Decimal, newValue As Decimal)
            RaiseEvent PriceRecalculated(Me, New PriceRecalculatedEventArgs(previousValue, newValue))
        End Sub

    End Class

End Namespace
