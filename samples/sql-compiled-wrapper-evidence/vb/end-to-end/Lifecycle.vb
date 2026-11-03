Imports System
Imports System.Collections.Generic
Imports System.Data
Namespace PublicSql.VisualBasic
    ' Compile-only integration fixture. Never invoke provider-backed methods.
    Public NotInheritable Class EntryProtocol
        Private stored As DataSet
        Public Shared Function ProduceInputs(first As Integer, second As Integer, third As String, fourth As Boolean) As DataSet
            Dim parameters = New System.Collections.ArrayList()
            parameters.Add(New InputParameter("@First", SqlDbType.NVarChar, 20, first))
            parameters.Add(New InputParameter("@Second", SqlDbType.NVarChar, 20, second))
            parameters.Add(New InputParameter("@Third", SqlDbType.NVarChar, 20, third))
            parameters.Add(New InputParameter("@Fourth", SqlDbType.NVarChar, 20, fourth))
            Return Wrapper.ArrayInputs("public.first", parameters)
        End Function
        Public Sub StoreInputs()
            stored = ProduceInputs(1, 2, "three", True)
        End Sub
        Public Function StoredInputRead() As Object
            StoreInputs()
            Return stored.Tables(0).Rows(0)("Name")
        End Function
        Public Shared Function EmptyProducer() As DataSet
            Return New DataSet()
        End Function
        Public Shared Function UnknownCommand(command As String) As DataSet
            Return Wrapper.Direct(command)
        End Function
        Public Shared Function UnknownGuard(first As Integer, second As Integer, third As String, fourth As Boolean) As DataSet
            If Not fourth Then Return New DataSet()
            Return ProduceInputs(first, second, third, fourth)
        End Function
        Public Shared Sub DiscardInputs()
            ProduceInputs(1, 2, "three", True)
        End Sub
    End Class
    Public NotInheritable Class ArrayLifecycle
        Public Function ArrayParameterRead() As Object
            Dim parameters = New System.Collections.ArrayList()
            parameters.Add(New FieldParameter("@Name", SqlDbType.NVarChar, 40))
            parameters.Add(New FieldParameter("@Code", SqlDbType.VarChar, 10))
            Return Wrapper.ArrayParameters("public.first", parameters).Tables(0).Rows(0)("Name")
        End Function
        Public Function ArrayIncompleteRead() As Object
            Dim parameters = New System.Collections.ArrayList()
            parameters.Add(New FieldParameter("@Name", SqlDbType.NVarChar, 40))
            parameters.Add(New FieldParameter("@Code", SqlDbType.VarChar, 10))
            Return Wrapper.ArrayIncomplete("public.first", parameters).Tables(0).Rows(0)("Name")
        End Function
        Public Function ArrayPropertyRead() As Object
            Dim parameters = New System.Collections.ArrayList()
            parameters.Add(New PropertyParameter("@Name", SqlDbType.NVarChar, 40))
            Return Wrapper.ArrayProperties("public.first", parameters).Tables(0).Rows(0)("Name")
        End Function
        Public Function ArraySeparateExecutions() As Object
            Dim first = New System.Collections.ArrayList()
            first.Add(New FieldParameter("@Name", SqlDbType.NVarChar, 40))
            Wrapper.ArrayParameters("public.first", first)
            Dim second = New System.Collections.ArrayList()
            second.Add(New FieldParameter("@Code", SqlDbType.VarChar, 10))
            Return Wrapper.ArrayParameters("public.second", second).Tables(0).Rows(0)("Name")
        End Function
        Public Function ArrayUnsupportedCollection() As Object
            Dim parameters = New System.Collections.Queue()
            parameters.Enqueue(New FieldParameter("@Name", SqlDbType.NVarChar, 40))
            Return Wrapper.Direct("public.first").Tables(0).Rows(0)("Name")
        End Function
        Public Function ArrayUnsupportedMutation() As Object
            Dim parameters = New System.Collections.ArrayList()
            parameters.Add(New FieldParameter("@Name", SqlDbType.NVarChar, 40))
            parameters.RemoveAt(0)
            Return Wrapper.ArrayParameters("public.first", parameters).Tables(0).Rows(0)("Name")
        End Function
        Public Function ArrayMutatedElement() As Object
            Dim parameters = New System.Collections.ArrayList()
            Dim value = New MutableParameter(40)
            parameters.Add(value) : value.Size = 80
            Return Wrapper.ArrayProperties("public.first", parameters).Tables(0).Rows(0)("Name")
        End Function
        Public Function ArrayEscaped() As Object
            Dim parameters = New System.Collections.ArrayList()
            parameters.Add(New FieldParameter("@Name", SqlDbType.NVarChar, 40))
            System.GC.KeepAlive(parameters)
            Return Wrapper.ArrayParameters("public.first", parameters).Tables(0).Rows(0)("Name")
        End Function
        Public Function ArrayTooLarge() As Object
            Dim parameters = New System.Collections.ArrayList()
            parameters.Add(New FieldParameter("@Name", SqlDbType.NVarChar, 40))
            parameters.Add(New FieldParameter("@Name", SqlDbType.NVarChar, 40))
            parameters.Add(New FieldParameter("@Name", SqlDbType.NVarChar, 40))
            parameters.Add(New FieldParameter("@Name", SqlDbType.NVarChar, 40))
            parameters.Add(New FieldParameter("@Name", SqlDbType.NVarChar, 40))
            parameters.Add(New FieldParameter("@Name", SqlDbType.NVarChar, 40))
            parameters.Add(New FieldParameter("@Name", SqlDbType.NVarChar, 40))
            parameters.Add(New FieldParameter("@Name", SqlDbType.NVarChar, 40))
            parameters.Add(New FieldParameter("@Name", SqlDbType.NVarChar, 40))
            parameters.Add(New FieldParameter("@Name", SqlDbType.NVarChar, 40))
            parameters.Add(New FieldParameter("@Name", SqlDbType.NVarChar, 40))
            parameters.Add(New FieldParameter("@Name", SqlDbType.NVarChar, 40))
            parameters.Add(New FieldParameter("@Name", SqlDbType.NVarChar, 40))
            parameters.Add(New FieldParameter("@Name", SqlDbType.NVarChar, 40))
            parameters.Add(New FieldParameter("@Name", SqlDbType.NVarChar, 40))
            parameters.Add(New FieldParameter("@Name", SqlDbType.NVarChar, 40))
            parameters.Add(New FieldParameter("@Name", SqlDbType.NVarChar, 40))
            Return Wrapper.ArrayParameters("public.first", parameters).Tables(0).Rows(0)("Name")
        End Function
        Public Function ArrayInvalidAttachment() As Object
            Dim value = New System.Data.SqlClient.SqlParameter("@Name", SqlDbType.NVarChar, 40)
            Wrapper.Forwarded("public.first", value)
            Return Wrapper.Forwarded("public.second", value).Tables(0).Rows(0)("Name")
        End Function
        Public Function ArrayWrongElement() As Object
            Dim parameters = New System.Collections.ArrayList()
            parameters.Add("not a parameter")
            Return Wrapper.ArrayParameters("public.first", parameters).Tables(0).Rows(0)("Name")
        End Function
        Public Function ArrayWrongCustomType() As Object
            Dim parameters = New System.Collections.ArrayList()
            parameters.Add(New PropertyParameter("@Name", SqlDbType.NVarChar, 40))
            Return Wrapper.ArrayParameters("public.first", parameters).Tables(0).Rows(0)("Name")
        End Function
        Public Function ArrayRepeatedEnumeration() As Object
            Dim parameters = New System.Collections.ArrayList()
            parameters.Add(New FieldParameter("@Name", SqlDbType.NVarChar, 40))
            Wrapper.ArrayParameters("public.first", parameters)
            Return Wrapper.ArrayParameters("public.second", parameters).Tables(0).Rows(0)("Name")
        End Function
    End Class
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
        Public Function DuplicateTable() As Object
            Load(False) : State.Tables.Add("First") : Return State.Tables(0).Rows(0)("Name")
        End Function
        Public Function EmptyLoop() As Object
            Load(False) : State.Tables(0).Clear() : Overlay(True) : Return State.Tables(1).Rows(0)("Count")
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
