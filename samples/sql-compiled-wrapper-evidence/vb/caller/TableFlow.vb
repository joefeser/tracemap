Imports System.Data
Namespace PublicSql.VisualBasic
    ' Compile-only; never invoke provider-backed fixture methods.
    Public NotInheritable Class TableFlow
        Private stored As DataSet
        Public Property State As DataSet
            Get
                Return stored
            End Get
            Set(value As DataSet)
                stored = value
            End Set
        End Property
        Private Shared Function FetchFirst() As DataSet
            Return Wrapper.Direct("public.first")
        End Function
        Private Shared Function FetchSecond() As DataSet
            Return Wrapper.Direct("public.second")
        End Function
        Public Function First() As Object
            State = New DataSet()
            State.Tables.Add("First")
            State.Tables.Add("Second")
            State.Tables(0).Merge(FetchFirst().Tables(0))
            State.Tables(1).Merge(FetchSecond().Tables(0))
            Return State.Tables(0).Rows(0)("Name")
        End Function
        Public Function Second() As Object
            State = New DataSet()
            State.Tables.Add("First")
            State.Tables.Add("Second")
            State.Tables(0).Merge(FetchFirst().Tables(0))
            State.Tables(1).Merge(FetchSecond().Tables(0))
            Return State.Tables(1).Rows(0)("Name")
        End Function
        Public Function Conditional(change As Boolean) As Object
            State = New DataSet()
            State.Tables.Add("First")
            State.Tables.Add("Second")
            State.Tables(0).Merge(FetchFirst().Tables(0))
            If change Then State.Tables(0).Rows(0)("Name") = "changed"
            Return State.Tables(0).Rows(0)("Name")
        End Function
        Public Function Mutated() As Object
            State = New DataSet()
            State.Tables.Add("First")
            State.Tables.Add("Second")
            State.Tables(0).Merge(FetchFirst().Tables(0))
            State.Tables(0).Rows(0)("Name") = "changed"
            Return State.Tables(0).Rows(0)("Name")
        End Function
        Public Function UnknownHelper() As Object
            State = New DataSet()
            State.Tables.Add("First")
            State.Tables.Add("Second")
            State.Tables(0).Merge(FetchFirst().Tables(0))
            Observe(State)
            Return State.Tables(0).Rows(0)("Name")
        End Function
        Public Function RepeatedMerge() As Object
            State = New DataSet()
            State.Tables.Add("First")
            State.Tables.Add("Second")
            State.Tables(0).Merge(FetchFirst().Tables(0))
            State.Tables(0).Merge(FetchSecond().Tables(0))
            Return State.Tables(0).Rows(0)("Name")
        End Function
        Public Function ClearMerge() As Object
            State = New DataSet()
            State.Tables.Add("First")
            State.Tables.Add("Second")
            State.Tables(0).Merge(FetchFirst().Tables(0))
            State.Tables(0).Clear()
            State.Tables(0).Merge(FetchSecond().Tables(0))
            Return State.Tables(0).Rows(0)("Name")
        End Function
        Private Shared Sub Observe(value As DataSet)
            System.GC.KeepAlive(value)
        End Sub
        Public Function DuplicateName() As Object
            State = New DataSet()
            State.Tables.Add("First") : State.Tables.Add("First")
            State.Tables(0).Merge(FetchFirst().Tables(0))
            Return State.Tables(0).Rows(0)("Name")
        End Function
        Public Function OutOfRange() As Object
            State = New DataSet() : State.Tables.Add("First")
            State.Tables(0).Merge(FetchFirst().Tables(0))
            Return State.Tables(1).Rows(0)("Name")
        End Function
        Public Function DynamicSlot(slot As Integer) As Object
            State = New DataSet() : State.Tables.Add("First")
            State.Tables(0).Merge(FetchFirst().Tables(0))
            Return State.Tables(slot).Rows(0)("Name")
        End Function
        Public Function ThirdTable() As Object
            State = New DataSet()
            State.Tables.Add("First") : State.Tables.Add("Second") : State.Tables.Add("Third")
            State.Tables(0).Merge(FetchFirst().Tables(0))
            Return State.Tables(0).Rows(0)("Name")
        End Function
        Public Function Existing(value As DataSet) As Object
            State = value
            State.Tables(0).Merge(FetchFirst().Tables(0))
            Return State.Tables(0).Rows(0)("Name")
        End Function
        Public Function Exceptional() As Object
            Try
                State = New DataSet()
                State.Tables.Add("First")
                State.Tables(0).Merge(FetchFirst().Tables(0))
            Catch ex As System.Exception
            End Try
            Return State.Tables(0).Rows(0)("Name")
        End Function
    End Class
End Namespace
