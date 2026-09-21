Namespace TraceMap.CompiledFixtures.VisualBasic
    Public Interface IFormatter
        Function Format(value As Integer) As String
    End Interface

    Public Class BaseWidget
        Public Overridable Function Describe(value As Integer) As String
            Return value.ToString()
        End Function
    End Class

    Public Class Widget
        Inherits BaseWidget
        Implements IFormatter

        Private ReadOnly items(9) As String

        Public Sub New()
        End Sub

        Public Sub New(seed As Integer)
            items(0) = seed.ToString()
        End Sub

        Default Public Property Item(index As Integer) As String
            Get
                Return items(index)
            End Get
            Set(value As String)
                items(index) = value
            End Set
        End Property

        Public ReadOnly Property OptionalItem(Optional index As Integer = 0) As String
            Get
                Return items(index)
            End Get
        End Property

        Public Overloads Function SelectValue(value As Integer) As Integer
            Return value
        End Function

        Public Overloads Function SelectValue(value As String) As String
            Return value
        End Function

        Public Overloads Function GenericArity(Of TOne)() As Integer
            Return 1
        End Function

        Public Overloads Function GenericArity(Of TOne, TTwo)() As Integer
            Return 2
        End Function

        Public Function OptionalValue(Optional value As Integer = 7) As Integer
            Return value
        End Function

        Public Overrides Function Describe(value As Integer) As String
            Return "vb:" & value.ToString()
        End Function

        Public Sub Shapes(ByRef value As Integer, values As Integer())
            value += values.Length
        End Sub

        Private Function Format(value As Integer) As String Implements IFormatter.Format
            Return value.ToString()
        End Function
    End Class

    Public Module ModuleShapes
        Public Function GenericValue(Of T)(value As T) As T
            Return value
        End Function
    End Module
End Namespace

Namespace TraceMap.CompiledFixtures.VisualBasic.Other
    Public Class Widget
        Public Function SelectValue(value As Integer) As Integer
            Return value + 1
        End Function
    End Class
End Namespace
