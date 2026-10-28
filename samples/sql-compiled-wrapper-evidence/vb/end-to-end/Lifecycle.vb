Imports System
Imports System.Collections.Generic
Imports System.Data
Namespace PublicSql.VisualBasic
    ' Compile-only integration fixture. Never invoke provider-backed methods.
    Public NotInheritable Class Lifecycle
        Private stored As DataSet
        Public Property State As DataSet
            Get
                Return stored
            End Get
            Set(value As DataSet)
                stored = value
            End Set
        End Property
        Private auxiliary As DataTable
        Public ReadOnly Items As New List(Of Projection)()
        Public Failed As Boolean
        Public NotInheritable Class Projection
            Public Name As Object
        End Class
        Private Sub BuildAuxiliary()
            auxiliary = New DataTable("Overlay")
            auxiliary.Columns.Add("Score", GetType(Integer))
            auxiliary.Rows.Add(7)
        End Sub
        Private Sub Overlay(enabled As Boolean)
            If enabled Then
                For Each row As DataRow In State.Tables(0).Rows
                    row("Score") = auxiliary.Rows(0)("Score")
                Next
            End If
        End Sub
        Public Sub Load(enabled As Boolean)
            Try
                Items.Clear()
                State = New DataSet()
                State.Tables.Add("First") : State.Tables.Add("Second")
                State.Tables(0).Merge(Wrapper.Direct("public.first").Tables(0))
                State.Tables(1).Merge(Wrapper.Direct("public.second").Tables(0))
                BuildAuxiliary() : Overlay(enabled)
                For Each row As DataRow In State.Tables(0).Rows
                    Items.Add(New Projection With {.Name = row("Name")})
                Next
            Catch ex As Exception
                Failed = True
            End Try
        End Sub
        Public Function First(enabled As Boolean) As Object
            Load(enabled) : Return State.Tables(0).Rows(0)("Name")
        End Function
        Public Function Second(enabled As Boolean) As Object
            Load(enabled) : Return State.Tables(1).Rows(0)("Count")
        End Function
        Public Function Score(enabled As Boolean) As Object
            Load(enabled) : Return State.Tables(0).Rows(0)("Score")
        End Function
        Public Function Typed(enabled As Boolean) As Object
            Load(enabled) : Return Items(0).Name
        End Function
        Public Function Missing() As Object
            Load(False) : Return State.Tables(0).Rows(0)("Absent")
        End Function
        Public Function Refresh() As Object
            Load(True)
            State.Tables(0).Clear()
            State.Tables(0).Merge(Wrapper.Direct("public.first").Tables(0))
            Return State.Tables(0).Rows(0)("Score")
        End Function
        Public Function Reload() As Object
            Load(True) : Load(False) : Return State.Tables(0).Rows(0)("Score")
        End Function
        Public Function DifferentRefresh() As Object
            Load(False) : State.Tables(0).Clear()
            State.Tables(0).Merge(Wrapper.Direct("public.second").Tables(0))
            Return State.Tables(0).Rows(0)("Score")
        End Function
        Public Function ClearedRead() As Object
            Load(False) : State.Tables(0).Clear() : Return State.Tables(0).Rows(0)("Name")
        End Function
        Public Function Escaped() As Object
            Load(False) : GC.KeepAlive(State) : Return State.Tables(0).Rows(0)("Name")
        End Function
        Public Function CrossRow() As Object
            Load(False)
            For Each row As DataRow In State.Tables(0).Rows
                row("Score") = State.Tables(0).Rows(0)("Score")
            Next
            Return State.Tables(0).Rows(0)("Score")
        End Function
        Public Function LoopCarried() As Object
            Load(False)
            For Each row As DataRow In State.Tables(0).Rows
                Failed = True
            Next
            Return State.Tables(0).Rows(0)("Name")
        End Function
    End Class
End Namespace
