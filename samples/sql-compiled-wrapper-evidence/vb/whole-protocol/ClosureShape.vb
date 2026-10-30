Imports System
Imports System.Collections
Imports System.Data
Imports System.Data.SqlClient
Imports System.Reflection

Namespace PublicSql.VisualBasic
    ' Independently authored synthetic closure mirroring the public v12-001 body/edge
    ' table: two inner roots (a full producer and a traced producer) plus the
    ' connection, adapter-build, parameter, exception, crypto-shaped, table-mapping
    ' and debug-helper callee bodies. Public types, names and literals only.
    Public NotInheritable Class ClosureException
        Inherits Exception
        Public Sub New(message As String, inner As Exception)
            MyBase.New(message, inner)
        End Sub
    End Class
    Public NotInheritable Class ClosureParameter
        Public ReadOnly Name As String
        Public ReadOnly Type As SqlDbType
        Public ReadOnly Size As Integer
        Public Sub New(name As String, type As SqlDbType, size As Integer)
            Me.Name = name : Me.Type = type : Me.Size = size
        End Sub
    End Class
    Public NotInheritable Class ClosureText
        ' Crypto-shaped compiled scalar transform: static, (String)->String, bounded
        ' body with no direct writes; qualified only by the compiled-scalar boundary.
        Public Shared Function Shape(value As String) As String
            Dim chars = value.ToCharArray()
            Dim rotated(chars.Length - 1) As Char
            For index = 0 To chars.Length - 1
                rotated(index) = chars(chars.Length - 1 - index)
            Next
            Return New String(rotated)
        End Function
        Public Shared Function CurrentName() As String
            Return MethodBase.GetCurrentMethod().Name
        End Function
        Public Shared Function FrameName() As String
            Return New System.Diagnostics.StackTrace().GetFrame(0).GetMethod().Name
        End Function
        Public Shared Function Describe(failure As Exception) As String
            Return (failure.Message & "|") & failure.InnerException.Message
        End Function
    End Class
    Public NotInheritable Class ClosureBase
        Private ReadOnly pool As New ArrayList()
        Private ReadOnly connectionValue As SqlConnection
        Public Sub New(value As String)
            connectionValue = New SqlConnection(value)
        End Sub
        Public ReadOnly Property Connection As SqlConnection
            Get
                Return connectionValue
            End Get
        End Property
        Public Sub Open()
            If connectionValue.State <> ConnectionState.Open Then
                Throw New InvalidOperationException("public-open-required")
            End If
            pool.Clear()
            pool.Add(New ClosureParameter("@public", SqlDbType.Int, 4))
            connectionValue.Open()
        End Sub
        Public Sub Close()
            If connectionValue.State <> ConnectionState.Closed Then
                connectionValue.Close()
            End If
        End Sub
    End Class
    Public NotInheritable Class ClosureBuild
        Public Shared Function Prepare(connection As SqlConnection, size As Integer) As SqlCommand
            Dim command As New SqlCommand("public.closure", connection)
            command.CommandType = CommandType.StoredProcedure
            command.CommandTimeout = 30
            command.Transaction = Nothing
            Dim sourced = New SqlParameter("@public", SqlDbType.Int, 4, "public_source")
            sourced.Direction = ParameterDirection.Input
            sourced.Value = "public-value"
            command.Parameters.Add(sourced)
            Dim pool As New ArrayList()
            pool.Add(New ClosureParameter("@public", SqlDbType.VarChar, 64))
            pool.Add(New ClosureParameter("@public", SqlDbType.VarChar, size))
            Dim copied = DirectCast(pool.ToArray(GetType(ClosureParameter)), ClosureParameter())
            For Each item As ClosureParameter In copied
                command.Parameters.Add(New SqlParameter(item.Name, item.Type, item.Size))
            Next
            Return command
        End Function
        Public Shared Function Run(command As SqlCommand) As DataSet
            Dim result As New DataSet()
            Dim adapter = New SqlDataAdapter(command)
            adapter.TableMappings.Add("Table", "public.result")
            adapter.Fill(result)
            Return result
        End Function
    End Class
    Public NotInheritable Class ClosureRoot
        Private ReadOnly shaped As String
        Public Sub New()
            shaped = ClosureText.Shape("public-connection-literal")
        End Sub
        Public Function ClosureProduce() As DataSet
            Dim opened = New ClosureBase(shaped)
            opened.Open()
            Dim command = ClosureBuild.Prepare(opened.Connection, 64)
            Try
                Return ClosureBuild.Run(command)
            Finally
                opened.Close()
            End Try
        End Function
        Public Function ClosureInputProduce(size As Integer) As DataSet
            ' Negative control: parameter sizes must be compiled constants; an
            ' input-driven size keeps its value unresolved and refuses.
            Dim opened = New ClosureBase(shaped)
            opened.Open()
            Dim command = ClosureBuild.Prepare(opened.Connection, size)
            Try
                Return ClosureBuild.Run(command)
            Finally
                opened.Close()
            End Try
        End Function
    End Class
    Public NotInheritable Class ClosureTraceRoot
        Private ReadOnly names As New ArrayList()
        Private Sub OnClosureMessage(sender As Object, args As SqlInfoMessageEventArgs)
            names.Add("public-closure-default")
        End Sub
        Public Function ClosureTraceProduce(connection As String) As DataSet
            System.Diagnostics.EventLog.WriteEntry("PublicClosure", ClosureText.CurrentName())
            Dim opened = New ClosureBase(connection)
            opened.Open()
            Dim command As New SqlCommand("public.traced", opened.Connection)
            command.CommandType = CommandType.StoredProcedure
            command.Parameters.Add(New SqlParameter("@public", SqlDbType.VarChar, 64))
            Dim handler = New SqlInfoMessageEventHandler(AddressOf OnClosureMessage)
            Dim result As New DataSet()
            Try
                AddHandler opened.Connection.InfoMessage, handler
                Dim adapter = New SqlDataAdapter(command)
                adapter.TableMappings.Add("Table", "public.traced_result")
                adapter.Fill(result)
            Finally
                RemoveHandler opened.Connection.InfoMessage, handler
                opened.Close()
            End Try
            Return result
        End Function
        Public Function ClosureNamesProduce(connection As String) As DataSet
            Dim command As New SqlCommand("public.names", New SqlConnection(connection))
            command.CommandType = CommandType.StoredProcedure
            Dim adapter = New SqlDataAdapter(command)
            Dim mappings = adapter.TableMappings
            mappings.Add("Table", "public.names_result")
            Dim source = mappings(0).SourceTable
            If source.Length = 0 Then Return Nothing
            Dim result As New DataSet()
            adapter.Fill(result)
            Return result
        End Function
        Public Function ClosureFrameProduce(connection As String) As DataSet
            System.Diagnostics.EventLog.WriteEntry("PublicClosure", ClosureText.FrameName())
            Dim command As New SqlCommand("public.frames", New SqlConnection(connection))
            command.CommandType = CommandType.StoredProcedure
            Dim result As New DataSet()
            Dim adapter = New SqlDataAdapter(command)
            adapter.Fill(result)
            Return result
        End Function
        Public Function ClosureExceptionProduce(connection As String) As DataSet
            Dim opened = New ClosureBase(connection)
            If opened.Connection.State <> ConnectionState.Open Then
                Throw New ClosureException("public-closure-failure", Nothing)
            End If
            Dim command As New SqlCommand("public.exception", opened.Connection)
            command.CommandType = CommandType.StoredProcedure
            Dim result As New DataSet()
            Dim adapter = New SqlDataAdapter(command)
            adapter.Fill(result)
            Return result
        End Function
        Public Function ClosureExceptionNestedProduce(connection As String) As DataSet
            Dim opened = New ClosureBase(connection)
            If opened.Connection.State <> ConnectionState.Open Then
                Throw New ClosureException("public-closure-failure", New InvalidOperationException("public-inner"))
            End If
            Dim command As New SqlCommand("public.exception_nested", opened.Connection)
            command.CommandType = CommandType.StoredProcedure
            Dim result As New DataSet()
            Dim adapter = New SqlDataAdapter(command)
            adapter.Fill(result)
            Return result
        End Function
        Public Function ClosureDescribeProduce(connection As String, failure As Exception) As DataSet
            System.Diagnostics.EventLog.WriteEntry("PublicClosure" & "-debug" & "-source", ClosureText.Describe(failure))
            Dim command As New SqlCommand("public.describe", New SqlConnection("public-describe"))
            command.CommandType = CommandType.StoredProcedure
            Dim result As New DataSet()
            Dim adapter = New SqlDataAdapter(command)
            adapter.Fill(result)
            Return result
        End Function
        Public Function ClosureTypedInputProduce(connection As String, failure As Exception) As DataSet
            ' Negative control: a non-primitive root argument leaves every input
            ' untyped, so typed admissions downstream refuse.
            System.Diagnostics.EventLog.WriteEntry("PublicClosure", ClosureText.Describe(failure))
            Dim command As New SqlCommand("public.typed_input", New SqlConnection(connection))
            command.CommandType = CommandType.StoredProcedure
            Dim result As New DataSet()
            Dim adapter = New SqlDataAdapter(command)
            adapter.Fill(result)
            Return result
        End Function
    End Class
End Namespace
