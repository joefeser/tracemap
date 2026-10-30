Imports System
Imports System.Collections
Imports System.Configuration
Imports System.Data
Imports System.Data.SqlClient
Imports System.Runtime.CompilerServices

Namespace PublicSql.VisualBasic
    ' Independently authored adjacent-catalog fixtures for the whole-protocol batch:
    ' command/adapter configuration, typed-array parameter flow, VB scalar helpers,
    ' string concatenation, the configuration opaque chain and event-log tracing.
    Public NotInheritable Class AdjacentRoot
        Public Function AdjacentProduce(connection As String) As DataSet
            Dim wrapper = New AdjacentWrapper(connection)
            Return wrapper.Execute()
        End Function
        Public Function AdjacentTypedArray(connection As String) As DataSet
            Dim wrapper = New AdjacentWrapper(connection)
            Return wrapper.ExecuteTypedArray()
        End Function
        Public Function AdjacentScalarHelpers(connection As String) As DataSet
            Dim wrapper = New AdjacentWrapper(connection)
            Return wrapper.ExecuteScalarHelpers()
        End Function
        ' AdjacentConfigProduce lives on AdjacentConfigRoot itself: the opaque boundary covers the root ctor
        Public Function AdjacentEventLog(connection As String) As DataSet
            Dim wrapper = New AdjacentWrapper(connection)
            Return wrapper.ExecuteEventLog()
        End Function
    End Class
    Public NotInheritable Class AdjacentWrapper
        Private ReadOnly connection As SqlConnection
        Public Sub New(value As String)
            connection = New SqlConnection(value)
        End Sub
        Private Function Run(command As SqlCommand) As DataSet
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
            If connection.State <> ConnectionState.Open Then
                Throw New InvalidOperationException("public-closed")
            End If
            Dim command As New SqlCommand("public." + "adjacent", connection)
            command.CommandType = CommandType.StoredProcedure
            command.CommandTimeout = 30
            command.Transaction = Nothing
            Dim sourced = New SqlParameter("@public", SqlDbType.Int, 4, "public_source")
            sourced.Direction = ParameterDirection.Input
            command.Parameters.Add(sourced)
            Dim names As New ArrayList()
            names.Add(New AdjacentParameter("@public", SqlDbType.VarChar, 64))
            names.Clear()
            names.Add(New AdjacentParameter("@public", SqlDbType.VarChar, 4))
            For Each item As AdjacentParameter In names
                command.Parameters.Add(New SqlParameter(item.Name, item.Type, item.Size))
            Next
            Return Run(command)
        End Function
        Public Function ExecuteTypedArray() As DataSet
            If connection.State <> ConnectionState.Open Then
                Throw New InvalidOperationException("public-closed")
            End If
            Dim command As New SqlCommand("public.typed_array", connection)
            command.CommandType = CommandType.StoredProcedure
            Dim parameters As New ArrayList()
            parameters.Add(New AdjacentParameter("@public", SqlDbType.Int, 4))
            parameters.Add(New AdjacentParameter("@public", SqlDbType.VarChar, 64))
            Dim copied = DirectCast(parameters.ToArray(GetType(AdjacentParameter)), AdjacentParameter())
            For Each item As AdjacentParameter In copied
                command.Parameters.Add(New SqlParameter(item.Name, item.Type, item.Size))
            Next
            Dim adapter = New SqlDataAdapter(command)
            adapter.TableMappings.Add("Table", "ResultSet")
            Return Run(command)
        End Function
        Public Function ExecuteScalarHelpers() As DataSet
            If connection.State <> ConnectionState.Open Then
                Throw New InvalidOperationException("public-closed")
            End If
            ' VB scalar helpers: a concrete Boolean selects one known size through IIf,
            ' a boxed Integer passes through ToInteger, and GetObjectValue wraps an
            ' object value before parameter attachment.
            Dim chosen As Integer = CInt(Microsoft.VisualBasic.Interaction.IIf(True, 4, 64))
            Dim boxed As Object = 4
            Dim size As Integer = Microsoft.VisualBasic.CompilerServices.Conversions.ToInteger(boxed)
            Dim value As Object = "public-value"
            Dim command As New SqlCommand("public.scalar_helpers", connection)
            command.CommandType = CommandType.StoredProcedure
            Dim first = New SqlParameter("@public", SqlDbType.VarChar, chosen)
            first.Value = RuntimeHelpers.GetObjectValue(value)
            command.Parameters.Add(first)
            command.Parameters.Add(New SqlParameter("@public", SqlDbType.Int, size))
            Return Run(command)
        End Function
        Public Function ExecuteEventLog() As DataSet
            ' Static event-log terminal beside the web-trace chain, root body only.
            System.Diagnostics.EventLog.WriteEntry("PublicSource", "public-adjacent-started")
            If connection.State <> ConnectionState.Open Then
                Throw New InvalidOperationException("public-closed")
            End If
            Dim command As New SqlCommand("public.event_log", connection)
            command.CommandType = CommandType.StoredProcedure
            Dim parameters As New ArrayList()
            parameters.Add(New AdjacentParameter("@public", SqlDbType.Int, 4))
            For Each item As AdjacentParameter In parameters
                command.Parameters.Add(New SqlParameter(item.Name, item.Type, item.Size))
            Next
            Dim result = Run(command)
            System.Diagnostics.EventLog.WriteEntry("PublicSource", "public-adjacent-finished")
            Return result
        End Function
    End Class
    Public NotInheritable Class AdjacentConfigRoot
        Private ReadOnly configured As String
        Public Sub New()
            configured = ConfigurationManager.ConnectionStrings("public-sample").ConnectionString
        End Sub
        Public Function AdjacentConfigProduce() As DataSet
            Dim connection = New SqlConnection(configured)
            Dim command As New SqlCommand("public.configured", connection)
            command.CommandType = CommandType.StoredProcedure
            Dim parameters As New ArrayList()
            parameters.Add(New AdjacentParameter("@public", SqlDbType.Int, 4))
            For Each item As AdjacentParameter In parameters
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
    Public NotInheritable Class AdjacentParameter
        Public ReadOnly Name As String
        Public ReadOnly Type As SqlDbType
        Public ReadOnly Size As Integer
        Public Sub New(name As String, type As SqlDbType, size As Integer)
            Me.Name = name : Me.Type = type : Me.Size = size
        End Sub
    End Class
End Namespace
