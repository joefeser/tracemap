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
    Public Structure ClosureStructPropertyParameter
        ' The v17 private shape: the struct wraps a SqlParameter PROPERTY and the
        ' struct itself rides the collection; consumers attach item.Param directly.
        Public ReadOnly Property Param As SqlParameter
        Public Sub New(name As String, type As SqlDbType, size As Integer, value As Object)
            Param = New SqlParameter(name, type, size)
            Param.Value = value
        End Sub
    End Structure
    Public Structure ClosureStructParameter
        Public ReadOnly Name As String
        Public ReadOnly Type As SqlDbType
        Public ReadOnly Size As Integer
        Public ReadOnly Value As Object
        Public Sub New(name As String, type As SqlDbType, size As Integer, value As Object)
            Me.Name = name : Me.Type = type : Me.Size = size : Me.Value = value
        End Sub
    End Structure
    Public Structure ClosureStructNullGuardParameter
        ' v18-001 stop shape: the struct ctor null-guards an INPUT-supplied value and
        ' substitutes DBNull.Value — the classic .NET 1 pattern.
        Public ReadOnly Property Param As SqlParameter
        Public Sub New(name As String, type As SqlDbType, size As Integer, value As Object)
            ' The confirmed private ctor shape: null-guard to DBNull, the .NET 1
            ' type-name dispatch on System.String, the nested empty-string-to-DBNull
            ' inline check, then value and direction assignment.
            If value Is Nothing Then value = System.DBNull.Value
            If value.GetType().ToString() = "System.String" Then
                If value.ToString() = "" Then value = System.DBNull.Value
            End If
            ' bne.un pre-admission: the inverted form of the same comparisons
            ' (ECMA-335 has no ceq.un; "not equal" lowers to bne.un).
            If value.GetType().ToString() <> "System.Byte()" Then size = size
            ' v23-001: CInt over a runtime string stays unproven (culture parse).
            Param = New SqlParameter(name, type, size)
            Param.Value = value
            Param.Direction = ParameterDirection.Input
        End Sub
        Public Sub New(name As String, type As SqlDbType, value As Object)
            ' The size-less overload: the two-argument SqlParameter constructor
            ' directly (mirrors the confirmed private shape; no chaining).
            If value Is Nothing Then value = System.DBNull.Value
            Param = New SqlParameter(name, type)
            Param.Value = value
            Param.Direction = ParameterDirection.Input
        End Sub
        Public Sub New(name As String, type As SqlDbType, direction As ParameterDirection, size As Integer, value As Object)
            ' The direction overload: the parameter direction passes through from
            ' the caller instead of defaulting to Input.
            If value Is Nothing Then value = System.DBNull.Value
            If value.GetType().ToString() = "System.String" Then
                If value.ToString() = "" Then value = System.DBNull.Value
            End If
            Param = New SqlParameter(name, type, size)
            Param.Value = value
            Param.Direction = direction
        End Sub
    End Structure
    Public NotInheritable Class ClosureStructNullGuardExec
        Public Shared Function Forward(name As String, ByRef parameters As ArrayList, ByRef outputNames As String()) As DataSet
            ' The 2-arg overload: converts the list, then forwards THREE ByRef values
            ' (its own locals) into the 3-arg overload — addresses crossing frames.
            Dim converted = DirectCast(parameters.ToArray(GetType(ClosureStructNullGuardParameter)), ClosureStructNullGuardParameter())
            Dim builtNames(1) As String
            Return Run(name, converted, builtNames, outputNames)
        End Function
        Public Shared Function Run(name As String, ByRef parameters As ClosureStructNullGuardParameter(), ByRef builtNames As String(), ByRef outputNames As String()) As DataSet
            ' The 3-arg overload: writes through BOTH array addresses (its own local's
            ' address from Forward's frame and the root's names address) and attaches.
            Dim stored = New SqlConnection("public-overload")
            If stored Is Nothing Then Return New DataSet()
            If parameters Is Nothing Then Return New DataSet()
            builtNames(0) = "public.built"
            outputNames(0) = "public.output"
            Dim command As New SqlCommand(name, stored)
            command.CommandType = CommandType.StoredProcedure
            For Each item As ClosureStructNullGuardParameter In parameters
                command.Parameters.Add(item.Param)
            Next
            ' v39-001: get_ParameterName / get_Value read-backs on attached parameters
            Dim firstName As String = command.Parameters(0).ParameterName
            Dim firstValue As Object = command.Parameters(0).Value
            If firstName Is Nothing Then Return New DataSet()
            If firstValue Is Nothing Then Return New DataSet()
            Dim result As New DataSet()
            Try
                Dim adapter = New SqlDataAdapter(command)
                adapter.Fill(result)
            Finally
                stored.Close()
            End Try
            Return result
        End Function

        Public Shared Function Execute(name As String, ByRef parameters As ArrayList) As DataSet
            Dim stored = New SqlConnection("public-struct-null-guard")
            Dim command As New SqlCommand(name, stored)
            command.CommandType = CommandType.StoredProcedure
            For Each item As ClosureStructNullGuardParameter In parameters
                Dim ignored = item.GetType().ToString() ' v20/v21: GetType then Type.ToString — untracked then unbound
                command.Parameters.Add(item.Param)
            Next
            Dim result As New DataSet()
            Try
                Dim adapter = New SqlDataAdapter(command)
                adapter.Fill(result)
            Finally
                stored.Close()
            End Try
            Return result
        End Function
        Public Shared Function Execute(name As String, ByRef parameters As ArrayList, ByRef outputNames As String()) As DataSet
            Dim probe As String = outputNames(0) ' the typed read from the ByRef output
            Return Execute(name, parameters)
        End Function
    End Class
    Public NotInheritable Class ClosureNullFoldExec
        ' v40-001: the caller keeps a NULL-INITIALIZED local array and forwards it
        ' ByRef; on the fork path that never assigned it, the callee's null guard
        ' is the reference non-null test (cgt.un vs ldnull) with BOTH operands
        ' proven null — folded deterministically, never forked.
        Public Shared Function Forward(name As String, convert As Boolean, ByRef list As ArrayList, ByRef outputNames As String()) As DataSet
            ' The fork source is the Boolean input: on the false-assumed path the
            ' local stays null-origin and is forwarded ByRef exactly as the private
            ' walk forwards its never-assigned typed array.
            Dim converted() As ClosureStructNullGuardParameter
            If convert Then
                converted = DirectCast(list.ToArray(GetType(ClosureStructNullGuardParameter)), ClosureStructNullGuardParameter())
            Else
                converted = Nothing
            End If
            Return Run(name, converted, outputNames)
        End Function
        Public Shared Function Run(name As String, ByRef converted As ClosureStructNullGuardParameter(), ByRef outputNames As String()) As DataSet
            Dim stored = New SqlConnection("public-null-fold")
            If stored Is Nothing Then Return New DataSet()
            If converted Is Nothing Then
                ' The null-path copy-back: the store writes through the forwarded address.
                converted = New ClosureStructNullGuardParameter() {}
            End If
            Dim command As New SqlCommand(name, stored)
            command.CommandType = CommandType.StoredProcedure
            For Each item As ClosureStructNullGuardParameter In converted
                command.Parameters.Add(item.Param)
            Next
            Dim result As New DataSet()
            Try
                Dim adapter = New SqlDataAdapter(command)
                adapter.Fill(result)
            Finally
                stored.Close()
            End Try
            Return result
        End Function
    End Class
    Public NotInheritable Class ClosureGuardExec
        Public Shared Function Execute(name As String, ByRef parameters As ArrayList, fail As Boolean) As DataSet
            Dim stored = New SqlConnection("public-guard-exec")
            If stored Is Nothing Then Return New DataSet()
            ' v33-001: the reference non-null test on the ByRef ARRAY parameter
            ' (If Not parameters Is Nothing) — a tracked snapshot is provably non-null.
            If parameters Is Nothing Then Return New DataSet()
            If fail Then
                Throw New ClosureGuardException(System.Reflection.MethodBase.GetCurrentMethod().Name,
                    name, name, New Exception(New Exception("public-inner").ToString()))
            End If
            Dim command As New SqlCommand(name, stored)
            command.CommandType = CommandType.StoredProcedure
            For Each item As ClosureStructNullGuardParameter In parameters
                command.Parameters.Add(item.Param)
            Next
            Dim result As New DataSet()
            Try
                Dim adapter = New SqlDataAdapter(command)
                adapter.Fill(result)
            Finally
                stored.Close()
            End Try
            Return result
        End Function
    End Class
    Public NotInheritable Class ClosureStructExec
        ' The confirmed private topology: the ExecProc helper receives the ArrayList
        ' BY REFERENCE and itself enumerates it, attaching each struct's wrapped
        ' SqlParameter to the command.
        Public Shared Function Execute(name As String, ByRef parameters As ArrayList) As DataSet
            Dim stored = New SqlConnection("public-struct-exec")
            ' v28-001: the reference non-null test (IsNot Nothing lowers to cgt.un
            ' vs ldnull); a tracked connection allocation is provably non-null.
            If stored Is Nothing Then Return New DataSet()
            Dim command As New SqlCommand(name, stored)
            command.CommandType = CommandType.StoredProcedure
            For Each item As ClosureStructPropertyParameter In parameters
                command.Parameters.Add(item.Param)
            Next
            Dim result As New DataSet()
            Try
                Dim adapter = New SqlDataAdapter(command)
                adapter.Fill(result)
            Finally
                stored.Close()
            End Try
            Return result
        End Function
    End Class
    Public NotInheritable Class ClosureByRef
        Public Shared Sub Execute(name As String, ByRef parameters As ArrayList, ByRef names As String())
            parameters.Add(New ClosureParameter("@public", SqlDbType.Int, 4))
            parameters.Add(New ClosureParameter("@public2", SqlDbType.VarChar, 64))
            names(0) = "public.byref_result"
            names(1) = "public.byref_tables"
        End Sub
    End Class
    Public NotInheritable Class ClosureRoot
        Private ReadOnly shaped As String
        Public Sub New()
            shaped = ClosureText.Shape("public-connection-literal")
        End Sub
        ' ByRef shape from the v14-001 outcome-B finding: locals passed by reference
        ' (ArrayList& and String()&) into the execute helper, which mutates the list
        ' and fills the names array through the aliases.
        Public Function ClosureByRefProduce(connection As String) As DataSet
            Dim stored = New SqlConnection(connection)
            Dim command As New SqlCommand("public.byref", stored)
            command.CommandType = CommandType.StoredProcedure
            Dim parameters As New ArrayList()
            Dim names(1) As String
            ClosureByRef.Execute("public.byref", parameters, names)
            For Each item As ClosureParameter In parameters
                command.Parameters.Add(New SqlParameter(item.Name, item.Type, item.Size))
            Next
            Dim result As New DataSet()
            Try
                Dim adapter = New SqlDataAdapter(command)
                adapter.TableMappings.Add("Table", names(0))
                adapter.Fill(result)
            Finally
                stored.Close()
            End Try
            Return result
        End Function
        Public Function ClosureByRefFrameworkProduce(connection As String) As DataSet
            ' Negative control: a framework ByRef API (out parameter) never receives a
            ' local address; only tracked inlined callees may take references.
            Dim stored = New SqlConnection(connection)
            Dim command As New SqlCommand("public.byref_framework", stored)
            command.CommandType = CommandType.StoredProcedure
            Dim size As Integer = 0
            Integer.TryParse("4", size)
            command.Parameters.Add(New SqlParameter("@public", SqlDbType.VarChar, size))
            Dim result As New DataSet()
            Dim adapter = New SqlDataAdapter(command)
            adapter.Fill(result)
            Return result
        End Function
        Public Function ClosureRawParameterProduce(connection As String) As DataSet
            ' v15-001 outcome B2 shape: constructor-proven SqlParameter allocations
            ' added directly to the lifecycle ArrayList (no custom wrapper objects).
            Dim stored = New SqlConnection(connection)
            Dim command As New SqlCommand("public.raw_parameters", stored)
            command.CommandType = CommandType.StoredProcedure
            Dim parameters As New ArrayList()
            parameters.Add(New SqlParameter("@public", SqlDbType.Int, 4))
            parameters.Add(New SqlParameter("@public2", SqlDbType.VarChar, 64))
            For Each item As SqlParameter In parameters
                command.Parameters.Add(item)
            Next
            Dim result As New DataSet()
            Try
                Dim adapter = New SqlDataAdapter(command)
                adapter.Fill(result)
            Finally
                stored.Close()
            End Try
            Return result
        End Function
        Public Function ClosureNameListProduce(connection As String) As DataSet
            ' Negative control: string items in the lifecycle collection are not the
            ' parameter protocol and stay refused.
            Dim stored = New SqlConnection(connection)
            Dim command As New SqlCommand("public.name_list", stored)
            command.CommandType = CommandType.StoredProcedure
            Dim names As New ArrayList()
            names.Add("public-name")
            For Each name As String In names
                command.Parameters.Add(New SqlParameter(name, SqlDbType.VarChar, 64))
            Next
            Dim result As New DataSet()
            Try
                Dim adapter = New SqlDataAdapter(command)
                adapter.Fill(result)
            Finally
                stored.Close()
            End Try
            Return result
        End Function
        Public Function ClosureCrossParameterProduce(connection As String) As DataSet
            ' v15 follow-up shape: the custom parameter type lives in a SEPARATE
            ' assembly (the wrapper role), so the constructor call is a cross-assembly
            ' memberref — the existing four-field wrapper type mirrors the reported
            ' (String, SqlDbType, Int32, Object) signature without growing the shared
            ' scan input budget.
            Dim stored = New SqlConnection(connection)
            Dim command As New SqlCommand("public.cross_parameter", stored)
            command.CommandType = CommandType.StoredProcedure
            Dim parameters As New ArrayList()
            parameters.Add(New InputParameter("@public", SqlDbType.Int, 4, "public-value"))
            parameters.Add(New InputParameter("@public2", SqlDbType.VarChar, 64, "public-second"))
            For Each item As InputParameter In parameters
                Dim parameter = New SqlParameter(item.Name, item.Type, item.Size)
                parameter.Value = item.Value
                command.Parameters.Add(parameter)
            Next
            Dim result As New DataSet()
            Try
                Dim adapter = New SqlDataAdapter(command)
                adapter.Fill(result)
            Finally
                stored.Close()
            End Try
            Return result
        End Function
        Public Function ClosureNullItemProduce(connection As String) As DataSet
            ' Declared boundary probe: a null item in the lifecycle collection; the
            ' refusal message stays stable and the found kind rides as a diagnostic.
            Dim stored = New SqlConnection(connection)
            Dim command As New SqlCommand("public.null_item", stored)
            command.CommandType = CommandType.StoredProcedure
            Dim parameters As New ArrayList()
            parameters.Add(Nothing)
            For Each item As ClosureParameter In parameters
                command.Parameters.Add(New SqlParameter(item.Name, item.Type, item.Size))
            Next
            Dim result As New DataSet()
            Dim adapter = New SqlDataAdapter(command)
            adapter.Fill(result)
            Return result
        End Function
        Public Function ClosureStructProduce(connection As String) As DataSet
            ' v17-001 diagnostic shape: a value-type parameter constructed in place —
            ' compilers lower struct construction to ldloca + CALL (not newobj), and
            ' the constructed value flows into the lifecycle collection.
            Dim stored = New SqlConnection(connection)
            Dim command As New SqlCommand("public.struct_parameter", stored)
            command.CommandType = CommandType.StoredProcedure
            Dim parameters As New ArrayList()
            Dim first As New ClosureStructParameter("@public", SqlDbType.Int, 4, "public-value")
            Dim second As New ClosureStructParameter("@public2", SqlDbType.VarChar, 64, "public-second")
            parameters.Add(first)
            parameters.Add(second)
            For Each item As ClosureStructParameter In parameters
                Dim parameter = New SqlParameter(item.Name, item.Type, item.Size)
                parameter.Value = item.Value
                command.Parameters.Add(parameter)
            Next
            Dim result As New DataSet()
            Try
                Dim adapter = New SqlDataAdapter(command)
                adapter.Fill(result)
            Finally
                stored.Close()
            End Try
            Return result
        End Function
        Public Function ClosureStructPropertyProduce(connection As String) As DataSet
            ' v17 consumption shape: the struct (not its SqlParameter) is added to
            ' the collection; the loop attaches item.Param directly.
            Dim stored = New SqlConnection(connection)
            Dim command As New SqlCommand("public.struct_property", stored)
            command.CommandType = CommandType.StoredProcedure
            Dim parameters As New ArrayList()
            Dim first As New ClosureStructPropertyParameter("@public", SqlDbType.Int, 4, "public-value")
            Dim second As New ClosureStructPropertyParameter("@public2", SqlDbType.VarChar, 64, "public-second")
            parameters.Add(first)
            parameters.Add(second)
            For Each item As ClosureStructPropertyParameter In parameters
                command.Parameters.Add(item.Param)
            Next
            Dim result As New DataSet()
            Try
                Dim adapter = New SqlDataAdapter(command)
                adapter.Fill(result)
            Finally
                stored.Close()
            End Try
            Return result
        End Function
        Public Function ClosureStructExecProduce(connection As String) As DataSet
            ' Root shape: build the struct parameters, hand the list by reference to
            ' the execute helper, and return its dataset.
            Dim parameters As New ArrayList()
            Dim first As New ClosureStructPropertyParameter("@public", SqlDbType.Int, 4, "public-value")
            Dim second As New ClosureStructPropertyParameter("@public2", SqlDbType.VarChar, 64, "public-second")
            parameters.Add(first)
            parameters.Add(second)
            Return ClosureStructExec.Execute("public.struct_exec", parameters)
        End Function
        Public Function ClosureStructNullGuardProduce(name As String, value As Object) As DataSet
            ' The root's own inputs feed the guarded ctor argument; the constant-built
            ' second parameter needs no fork.
            Dim parameters As New ArrayList()
            parameters.Add(New ClosureStructNullGuardParameter("@public", SqlDbType.Int, CInt(name), value))
            parameters.Add(New ClosureStructNullGuardParameter("@public2", SqlDbType.VarChar, ParameterDirection.Output, 64, "public-second"))
            Return ClosureStructNullGuardExec.Execute("public.struct_null_guard", parameters)
        End Function
        Public Function ClosurePathBudgetProduce(connection As String, first As Object, second As Object) As DataSet
            ' v19-001 stop shape: two DISTINCT guarded inputs (two forks) compound
            ' with the connection state guards — completing paths exceed the old
            ' 8-path bound; the raised bound keeps every path individually proven.
            Dim opened = New ClosureBase(connection)
            opened.Open()
            Dim parameters As New ArrayList()
            parameters.Add(New ClosureStructNullGuardParameter("@public", SqlDbType.Int, 4, first))
            parameters.Add(New ClosureStructNullGuardParameter("@public2", SqlDbType.VarChar, ParameterDirection.Output, 64, "public-second"))
            Dim result = ClosureStructNullGuardExec.Execute("public.path_budget", parameters)
            opened.Close()
            Return result
        End Function
        Public Function ClosureFullRootProduce(connection As String, month As Integer, year As Integer, employee As String, domicile As String) As DataSet
            ' The full confirmed root shape (sanitized): trace preamble with the
            ' four-frame stack-name chain, open, the parameter list mixing a string
            ' input, a boxed CInt(Integer), an unbound CInt(String) SIZE, and a
            ' VarChar(-1) constant, the two-argument ExecProc call, close, return.
            System.Web.HttpContext.Current.Trace.Write("Public Data Access",
                (New System.Diagnostics.StackTrace()).GetFrame(3).GetMethod().Name + " > " +
                (New System.Diagnostics.StackTrace()).GetFrame(2).GetMethod().Name + " > " +
                (New System.Diagnostics.StackTrace()).GetFrame(1).GetMethod().Name + " > " +
                (New System.Diagnostics.StackTrace()).GetFrame(0).GetMethod().Name)
            Dim returnValue As New DataSet()
            Dim opened = New ClosureBase(connection)
            Try
                opened.Open()
                Dim parametersList As New ArrayList()
                parametersList.Add(New ClosureStructNullGuardParameter("@public", SqlDbType.VarChar, 15, connection))
                parametersList.Add(New ClosureStructNullGuardParameter("@public_month", SqlDbType.Int, CInt(month)))
                parametersList.Add(New ClosureStructNullGuardParameter("@public_year", SqlDbType.Int, year))
                parametersList.Add(New ClosureStructNullGuardParameter("@public_lines", SqlDbType.VarChar, -1, "public-lines"))
                parametersList.Add(New ClosureStructNullGuardParameter("@public_employee", SqlDbType.VarChar, 12, employee))
                parametersList.Add(New ClosureStructNullGuardParameter("@public_domicile", SqlDbType.Char, 3, domicile))
                returnValue = ClosureStructNullGuardExec.Execute("public.full_root", parametersList)
            Finally
                opened.Close()
            End Try
            Return returnValue
        End Function
        Public Function ClosurePeriodProduce(bidPeriod As String) As DataSet
            ' The COBOL period shape: a 6-digit YYYYMM string sliced into year and
            ' month parts, each parsed with CInt into parameter values.
            Dim size As Integer
            Dim yearPart As String = bidPeriod.Substring(0, 4)
            Dim monthPart As String = Microsoft.VisualBasic.Strings.Mid(bidPeriod, 5, 2)
            ' v27-001: an unbound integer compared to a compiled constant (the range
            ' check) forks a bounded hypothetical keyed by the value's provenance.
            If CInt(monthPart) > 0 Then size = 4 Else size = 8
            Dim parameters As New ArrayList()
            parameters.Add(New ClosureStructNullGuardParameter("@public_year", SqlDbType.Int, 4, CInt(yearPart)))
            parameters.Add(New ClosureStructNullGuardParameter("@public_month", SqlDbType.Int, CInt(monthPart)))
            Return ClosureStructNullGuardExec.Execute("public.period", parameters)
        End Function
        Public Function ClosureGuardProduce(connection As String, fail As Boolean) As DataSet
            ' v31-001 shape: the guard throws the custom exception with the
            ' current-method name, two command texts and a constructed inner.
            Dim parameters As New ArrayList()
            parameters.Add(New ClosureStructNullGuardParameter("@public", SqlDbType.VarChar, 12, connection))
            Return ClosureGuardExec.Execute("public.guard", parameters, fail)
        End Function
        Public Function ClosurePolarityProduce(value As Object) As DataSet
            ' Dedicated polarity pin: one nullable input, one type-name string fork
            ' with an observable size per side. Debug lowers ceq; the Cecil-rewrite
            ' test proves the Release brtrue-direct shape keeps the same labels.
            Dim size As Integer = 4
            If value.GetType().ToString() = "System.Int32" Then size = 4 Else size = 9
            Dim parameters As New ArrayList()
            parameters.Add(New ClosureStructNullGuardParameter("@public", SqlDbType.Int, size, value))
            Return ClosureStructNullGuardExec.Execute("public.polarity", parameters)
        End Function
        Public Function ClosureOutputNamesProduce(connection As String) As DataSet
            ' v34-001: a typed element load from the ByRef String() output parameter
            ' (the names array the callee fills); untracked elements stay unknown.
            Dim names(1) As String
            Dim parameters As New ArrayList()
            parameters.Add(New ClosureStructNullGuardParameter("@public", SqlDbType.VarChar, 12, connection))
            Dim result = ClosureStructNullGuardExec.Execute("public.output_names", parameters, names)
            Dim first As String = names(0) ' typed read consumed; element stays unknown
            Dim copied(1) As ClosureStructNullGuardParameter ' typed struct array
            Dim element As ClosureStructNullGuardParameter = copied(0) ' the bare typed ldelem consumed; element stays unknown
            If result Is Nothing Then Return result
            Return result
        End Function
        Public Function ClosureIndexedProduce(connection As String) As DataSet
            ' The indexed For family: get_Count/get_Item over the ArrayList and the
            ' length-1 bound over a typed array, with the .s branch forms.
            Dim parameters As New ArrayList()
            parameters.Add(New ClosureStructNullGuardParameter("@public", SqlDbType.VarChar, 12, connection))
            parameters.Add(New ClosureStructNullGuardParameter("@public2", SqlDbType.VarChar, 64, "public-second"))
            Dim copied = DirectCast(parameters.ToArray(GetType(ClosureStructNullGuardParameter)), ClosureStructNullGuardParameter())
            Dim consumed As Integer = 0
            For index As Integer = 0 To copied.Length - 1
                consumed = consumed + 1
            Next
            For position As Integer = 0 To parameters.Count - 1
                consumed = consumed + 1
            Next
            Return ClosureStructNullGuardExec.Execute("public.indexed", parameters)
        End Function
        Public Function ClosureFieldAddressProduce(connection As String) As DataSet
            ' v36-001: ldflda — ByRef field arguments on tracked struct-holders
            ' (the copy-back temp shape) and direct field-address reads.
            Dim holder As New ClosureFieldHolder(4)
            ClosureFieldHelper.Mutate(holder)
            ClosureFieldHelper.AppendLast(holder.LastCommand)
            Dim parameters As New ArrayList()
            parameters.Add(New ClosureStructNullGuardParameter("@public", SqlDbType.VarChar, 12, connection))
            Return ClosureStructNullGuardExec.Execute("public.field_address", parameters)
        End Function
        Public Function ClosureOverloadProduce(connection As String) As DataSet
            ' v37-001: the overload forwarding chain — the 2-arg ExecProc builds the
            ' typed array and forwards ByRef locals (params array + output names) into
            ' the 3-arg overload two frames below the root's locals.
            Dim names(1) As String
            Dim parameters As New ArrayList()
            parameters.Add(New ClosureStructNullGuardParameter("@public", SqlDbType.VarChar, 12, connection))
            Return ClosureStructNullGuardExec.Forward("public.overload", parameters, names)
        End Function
        Public Function ClosureNullFoldProduce(connection As String, convert As Boolean) As DataSet
            ' v40-001: the null-init forward — one fork path forwards a never-assigned
            ' null-origin array into the 3-arg overload's null guard. The private walk
            ' forks the root's own ArrayList input nullness; the fixture forks a
            ' Boolean input to keep root arguments primitive.
            Dim names(1) As String
            Dim parameters As New ArrayList()
            parameters.Add(New ClosureStructNullGuardParameter("@public", SqlDbType.VarChar, 12, connection))
            Return ClosureNullFoldExec.Forward("public.null_fold", convert, parameters, names)
        End Function
        Public Function ClosureStrictCompareProduce(connection As String) As DataSet
            ' Codex review fix: cgt evaluated as !(clt) — equal operands produced
            ' true. Two count-derived ints compared through a STORED flag lower to
            ' the cgt value form; equal operands must fold to false.
            Dim parameters As New ArrayList()
            parameters.Add(New ClosureStructNullGuardParameter("@public", SqlDbType.VarChar, 12, connection))
            Dim first As Integer = parameters.Count
            Dim second As Integer = parameters.Count
            Dim larger As Boolean = first > second
            If larger Then Return New DataSet()
            Return ClosureStructNullGuardExec.Execute("public.strict_compare", parameters)
        End Function
        Public Function ClosureFillCountProduce(connection As String) As DataSet
            ' Codex review fix: Fill fabricated a zero count. The returned count
            ' is an unbound integer; a stored > 0 flag forks the rows/no-rows
            ' hypothetical instead of selecting a path.
            Dim stored = New SqlConnection("public-fill-count")
            Dim command As New SqlCommand("public.fill_count", stored)
            command.CommandType = CommandType.StoredProcedure
            command.Parameters.Add(New SqlParameter("@public", SqlDbType.VarChar, 64))
            Dim result As New DataSet()
            Dim filled As Integer
            Try
                Dim adapter = New SqlDataAdapter(command)
                filled = adapter.Fill(result)
            Finally
                stored.Close()
            End Try
            Dim anyRows As Boolean = filled > 0
            If anyRows Then Return result
            Return result
        End Function
        Public Function ClosureFillZeroProduce(connection As String) As DataSet
            ' Negative control: an EQUALITY test on the unbound Fill count
            ' refuses by name — no inferred row count, ever.
            Dim stored = New SqlConnection("public-fill-zero")
            Dim command As New SqlCommand("public.fill_zero", stored)
            command.CommandType = CommandType.StoredProcedure
            command.Parameters.Add(New SqlParameter("@public", SqlDbType.VarChar, 64))
            Dim result As New DataSet()
            Dim filled As Integer
            Try
                Dim adapter = New SqlDataAdapter(command)
                filled = adapter.Fill(result)
            Finally
                stored.Close()
            End Try
            Dim noRows As Boolean = filled = 0
            If noRows Then Return result
            Return result
        End Function
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
    ' The confirmed exception shape: a custom exception whose ctor takes the
    ' current-method name, two command texts and an inner exception; the inner may
    ' be the caught exception or a framework exception constructed as an argument.
    Public NotInheritable Class ClosureFieldHolder
        Public Stored As Integer
        Public LastCommand As String
        Public Sub New(value As Integer)
            Stored = value
            LastCommand = "public-initial"
        End Sub
    End Class
    Public NotInheritable Class ClosureFieldHelper
        Public Shared Sub Mutate(ByRef holder As ClosureFieldHolder)
            ' ByRef struct-argument: the caller emits ldflda of the field/holder
            ' address; the callee stores through it (stind.ref on the address).
            holder.Stored = holder.Stored + 1
        End Sub
        Public Shared Sub AppendLast(ByRef lastCommand As String)
            ' v38-001: a THIS-FIELD address (allocation + _lastcommand shape) passed
            ' ByRef into a tracked callee that reads and stores through it.
            lastCommand = lastCommand & " public-appended"
        End Sub
    End Class
    Public NotInheritable Class ClosureGuardException
        Inherits Exception
        Private ReadOnly CommandText1 As String
        Private ReadOnly CommandText2 As String
        Private ReadOnly StoredInner As Exception
        Public Sub New(methodName As String, commandText1 As String, commandText2 As String, inner As Exception)
            MyBase.New(methodName & ": " & commandText1)
            Me.CommandText1 = commandText1
            Me.CommandText2 = commandText2
            Me.StoredInner = inner
        End Sub
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
        Public Function ClosureExternalDetachProduce(connection As String) As DataSet
            ' v41-001: the walk's terminal stop — the matched RemoveHandler followed
            ' by a SECOND one: the handler stays tracked-shape but is no longer in
            ' the connection's subscription set. The external-detach admission
            ' records the second unsubscribe without claiming the external
            ' subscription (out-of-window subscribe / different connection).
            System.Diagnostics.EventLog.WriteEntry("PublicClosure", ClosureText.CurrentName())
            Dim opened = New ClosureBase(connection)
            opened.Open()
            Dim command As New SqlCommand("public.external_detach", opened.Connection)
            command.CommandType = CommandType.StoredProcedure
            command.Parameters.Add(New SqlParameter("@public", SqlDbType.VarChar, 64))
            Dim handler = New SqlInfoMessageEventHandler(AddressOf OnClosureMessage)
            Dim result As New DataSet()
            Try
                AddHandler opened.Connection.InfoMessage, handler
                Dim adapter = New SqlDataAdapter(command)
                adapter.TableMappings.Add("Table", "public.external_detach_result")
                adapter.Fill(result)
            Finally
                RemoveHandler opened.Connection.InfoMessage, handler
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
