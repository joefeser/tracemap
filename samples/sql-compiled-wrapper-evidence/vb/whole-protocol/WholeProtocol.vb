Imports System
Imports System.Collections
Imports System.Data
Imports System.Data.SqlClient
Namespace PublicSql.VisualBasic
    ' Independently authored whole-protocol fixture: configuration boundary, runtime
    ' state guard, fill/close with finally, inspected custom parameter objects, real
    ' SqlParameter attachment, adapter Fill and returned allocation. No connection is
    ' opened by tests; the state guard keeps its runtime value unbound.
    Public NotInheritable Class WholeProtocolRoot
        Public Function WholeProtocolProduce(connection As String) As DataSet
            Dim wrapper = New WholeProtocolWrapper(connection)
            Return wrapper.Execute()
        End Function
        Public Function WholeProtocolGuardedFill(connection As String) As DataSet
            Dim wrapper = New WholeProtocolWrapper(connection)
            Return wrapper.ExecuteGuarded()
        End Function
        Public Function WholeProtocolChoiceAttachment(connection As String) As DataSet
            Dim wrapper = New WholeProtocolWrapper(connection)
            Return wrapper.ExecuteChoice()
        End Function
        Public Function WholeProtocolRecheck(connection As String) As DataSet
            Dim wrapper = New WholeProtocolWrapper(connection)
            Return wrapper.ExecuteRecheck()
        End Function
        Public Function WholeProtocolGuardInLoop(connection As String) As DataSet
            Dim wrapper = New WholeProtocolWrapper(connection)
            Return wrapper.ExecuteGuardInLoop()
        End Function
        Public Function WholeProtocolStoredException(connection As String) As DataSet
            Dim wrapper = New WholeProtocolWrapper(connection)
            Return wrapper.ExecuteStoredException()
        End Function
        Public Function WholeProtocolStateComparedTwice(connection As String) As DataSet
            Dim wrapper = New WholeProtocolWrapper(connection)
            Return wrapper.ExecuteStateComparedTwice()
        End Function
        Public Function WholeProtocolManyGuards(connection As String) As DataSet
            Dim wrapper = New WholeProtocolWrapper(connection)
            Return wrapper.ExecuteManyGuards()
        End Function
    End Class
    Public NotInheritable Class WholeProtocolWrapper
        Private ReadOnly connection As SqlConnection
        Public Sub New(value As String)
            connection = New SqlConnection(value)
        End Sub
        Private Function Fill(name As String) As DataSet
            Dim command = New SqlCommand(name, connection)
            command.CommandType = CommandType.StoredProcedure
            Dim parameters As New ArrayList()
            parameters.Add(New WholeProtocolParameter("@public", SqlDbType.Int, 4))
            For Each item As WholeProtocolParameter In parameters
                command.Parameters.Add(New SqlParameter(item.Name, item.Type, item.Size))
            Next
            Dim result As New DataSet()
            Try
                Dim adapter = New SqlDataAdapter(command)
                adapter.Fill(result)
            Finally
                connection.Close()
            End Try
            Return result
        End Function
        Public Function Execute() As DataSet
            ' Stored Boolean local retains the equality shape; the throw path terminates
            ' exceptionally and never merges with the fill path.
            If connection.State <> ConnectionState.Open Then
                Throw New InvalidOperationException("public-closed")
            End If
            Return Fill("public.whole_protocol")
        End Function
        Public Function ExecuteGuarded() As DataSet
            ' Natural guard shape without a Boolean local.
            If connection.State = ConnectionState.Open Then
                Return Fill("public.guarded")
            End If
            Return Nothing
        End Function
        Public Function ExecuteChoice() As DataSet
            ' Incompatible attachments on the two state alternatives; neither may be
            ' silently selected or merged into unconditional entry evidence.
            Dim command = New SqlCommand("public.choice", connection)
            command.CommandType = CommandType.StoredProcedure
            If connection.State = ConnectionState.Open Then
                command.Parameters.Add(New SqlParameter("@public", SqlDbType.Int, 4))
            Else
                command.Parameters.Add(New SqlParameter("@public", SqlDbType.VarChar, 64))
            End If
            Dim result As New DataSet()
            Try
                Dim adapter = New SqlDataAdapter(command)
                adapter.Fill(result)
            Finally
                connection.Close()
            End Try
            Return result
        End Function
        Public Function ExecuteRecheck() As DataSet
            ' Separate reads stay separate decisions; both guards must hold on the fill
            ' path, and early-return alternatives keep their own identities.
            If connection.State <> ConnectionState.Open Then Return Nothing
            If connection.State <> ConnectionState.Open Then Return Nothing
            Return Fill("public.recheck")
        End Function
        Public Function ExecuteGuardInLoop() As DataSet
            ' A state read inside the per-row loop composes with the symbolic pass.
            Dim result = Fill("public.guard_loop")
            For Each row As DataRow In result.Tables(0).Rows
                If connection.State <> ConnectionState.Open Then Return Nothing
            Next
            Return result
        End Function
        Public Function ExecuteStoredException() As DataSet
            ' Negative control: a caught throw keeps its catch continuation unexecuted.
            Try
                If connection.State <> ConnectionState.Open Then
                    Throw New InvalidOperationException("public-closed")
                End If
                Return Fill("public.stored_exception")
            Catch generated_Publisher As InvalidOperationException
                Return Nothing
            End Try
        End Function
        Public Function ExecuteStateComparedTwice() As DataSet
            ' Negative control: two unbound reads compared to each other refuse.
            If connection.State = connection.State Then Return Nothing
            Return Execute()
        End Function
        Public Function ExecuteManyGuards() As DataSet
            ' Negative control: five two-sided state decisions exceed the bounded path
            ' alternatives (2^5 > 8) and must refuse explicitly instead of guessing.
            Dim size As Integer = 4
            If connection.State = ConnectionState.Open Then size = 4 Else size = 8
            If connection.State = ConnectionState.Open Then size = 5 Else size = 10
            If connection.State = ConnectionState.Open Then size = 6 Else size = 12
            If connection.State = ConnectionState.Open Then size = 7 Else size = 14
            If connection.State = ConnectionState.Open Then size = 9 Else size = 16
            Dim command = New SqlCommand("public.many_guards", connection)
            command.CommandType = CommandType.StoredProcedure
            Dim parameters As New ArrayList()
            parameters.Add(New WholeProtocolParameter("@public", SqlDbType.Int, size))
            For Each item As WholeProtocolParameter In parameters
                command.Parameters.Add(New SqlParameter(item.Name, item.Type, item.Size))
            Next
            Dim result As New DataSet()
            Try
                Dim adapter = New SqlDataAdapter(command)
                adapter.Fill(result)
            Finally
                connection.Close()
            End Try
            Return result
        End Function
    End Class
    Public NotInheritable Class WholeProtocolParameter
        Public ReadOnly Name As String
        Public ReadOnly Type As SqlDbType
        Public ReadOnly Size As Integer
        Public Sub New(name As String, type As SqlDbType, size As Integer)
            Me.Name = name : Me.Type = type : Me.Size = size
        End Sub
    End Class
End Namespace
