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

Namespace TraceMap.CompiledFixtures.VisualBasic.Il
    Public Module IlBodyShapes
        ' VB-IL-ASSEMBLY-007: identical trivial body shape to the C# fixture
        ' method of the same name; assembly and signature scoping must keep the
        ' body identities distinct.
        Public Function IlIdentity(value As Integer) As Integer
            Return value
        End Function

        ' VB-IL-BODY-013: direct call, typed catch, and finally region.
        Public Function IlCallShape(value As Integer) As Integer
            Try
                Return IlIdentity(value)
            Catch ex As InvalidOperationException
                Return -1
            Finally
                Dim ignored = value.ToString()
            End Try
        End Function
    End Module
End Namespace

Namespace TraceMap.CompiledFixtures.Equivalence
    Public NotInheritable Class SharedShape
        Public Shared Function [Select](value As Integer) As Integer
            Return value
        End Function
        Public Shared Function [Select](value As String) As String
            Return value
        End Function
        Public Shared Function Reference(ByRef value As Integer) As Integer
            Return value
        End Function
        Public Shared Function Echo(Of T)(value As T) As T
            Return value
        End Function
        Public Shared Function Echo(Of TLeft, TRight)(value As TLeft) As TLeft
            Return value
        End Function
        Public Shared Function Rank(value As Integer()) As Integer()
            Return value
        End Function
        Public Shared Function Rank(value As Integer(,)) As Integer(,)
            Return value
        End Function
    End Class
End Namespace

Namespace TraceMap.CompiledFixtures.Equivalence
    Public NotInheritable Class AccessorShape
        Public Property Value As Integer
        Public ReadOnly Property Snapshot As Integer
            Get
                Return Value
            End Get
        End Property
        Public Custom Event Changed As System.EventHandler
            AddHandler(value As System.EventHandler)
            End AddHandler
            RemoveHandler(value As System.EventHandler)
            End RemoveHandler
            RaiseEvent(sender As Object, e As System.EventArgs)
            End RaiseEvent
        End Event
        Public Function get_Unbound() As Integer
            Return 0
        End Function
    End Class

    Public NotInheritable Class OptionShape
        Public Shared Function NullableRoundtrip(value As Integer?) As Integer?
            Return value
        End Function
    End Class

    Public NotInheritable Class OptionalShape
        Public Shared Function Required(value As Integer) As Integer
            Return value
        End Function
        Public Shared Function OptionalSeven(Optional value As Integer = 7) As Integer
            Return value
        End Function
        Public Shared Function OptionalNine(Optional value As Integer = 9) As Integer
            Return value
        End Function
        Public Shared Function Wide(Optional p0 As Integer = 0, Optional p1 As Integer = 0,
            Optional p2 As Integer = 0, Optional p3 As Integer = 0, Optional p4 As Integer = 0,
            Optional p5 As Integer = 0, Optional p6 As Integer = 0, Optional p7 As Integer = 0,
            Optional p8 As Integer = 0, Optional p9 As Integer = 0, Optional p10 As Integer = 0) As Integer
            Return p10
        End Function
    End Class
End Namespace

Namespace TraceMap.CompiledFixtures.Equivalence
    Public NotInheritable Class ConstraintShape
        Public Shared Function Free(Of T)(value As T) As T
            Return value
        End Function
        Public Shared Function Reference(Of T As Class)(value As T) As T
            Return value
        End Function
        Public Shared Function Value(Of T As Structure)(input As T) As T
            Return input
        End Function
        Public Shared Function Construct(Of T As New)(value As T) As T
            Return value
        End Function
        Public Shared Function Disposable(Of T As System.IDisposable)(value As T) As T
            Return value
        End Function
    End Class
End Namespace

Namespace TraceMap.CompiledFixtures.Equivalence
    Public NotInheritable Class DefaultShape
        Public Shared Function Required(value As Integer) As Integer
            Return value
        End Function
        Public Shared Function IntSeven(Optional value As Integer = 7) As Integer
            Return value
        End Function
        Public Shared Function IntNine(Optional value As Integer = 9) As Integer
            Return value
        End Function
        Public Shared Function Text(Optional value As String = "seven") As String
            Return value
        End Function
        Public Shared Function NullText(Optional value As String = Nothing) As String
            Return value
        End Function
        Public Shared Function DecimalSeven(Optional value As Decimal = 7D) As Decimal
            Return value
        End Function
    End Class
End Namespace

Namespace TraceMap.CompiledFixtures.Equivalence
    Public Interface ISharedFormatter
        Function Format(value As String) As String
    End Interface
    Public NotInheritable Class ExplicitShape
        Implements ISharedFormatter
        Public Function Format(value As String) As String
            Return "ordinary:" & value
        End Function
        Private Function FormatContract(value As String) As String Implements ISharedFormatter.Format
            Return "explicit:" & value
        End Function
    End Class
End Namespace
