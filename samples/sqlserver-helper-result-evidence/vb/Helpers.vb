' Independently authored. Compile and scan only; never invoke methods.
#Disable Warning BC40000 ' Signed legacy provider static compatibility control.
Imports System
Imports System.Data
Imports System.Data.SqlClient
Imports System.Runtime.CompilerServices
Public Module HelperCases
Private Sub Consume(value As Object)
End Sub
Private Sub ConsumeTable(table As DataTable)
End Sub
Private Sub Touch()
End Sub
' ---- Bounded helpers: one Phase 2/3 Fill shape, command text from the procedure parameter, filled local returned.
Private Function ReadTable(procedure As String) As DataTable
Dim command = New SqlCommand(procedure)
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Return table
End Function
Private Function ReadSet(procedure As String) As DataSet
Dim command = New SqlCommand(procedure)
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results)
Return results
End Function
Private Function ReadNamedSet(procedure As String) As DataSet
Dim command = New SqlCommand(procedure)
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim results = New DataSet()
adapter.Fill(results, "Items")
Return results
End Function
Private Function ReadTableText(procedure As String) As DataTable
Dim command = New SqlCommand()
command.CommandText = procedure
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter()
adapter.SelectCommand = command
Dim table = New DataTable()
adapter.Fill(table)
Return table
End Function
Private Function ReadTableUsing(connection As SqlConnection, procedure As String) As DataTable
Using command As New SqlCommand(procedure, connection)
command.CommandType = CommandType.StoredProcedure
Using adapter As New SqlDataAdapter(command)
Dim table As New DataTable()
adapter.Fill(table)
Return table
End Using
End Using
End Function
' ---- Core caller cells: compared against JSON, constant DDL and table-backed DDL shapes.
Public Function TableName(columnName As String, index As Integer, flag As Boolean) As Object
Dim table = ReadTable("dbo.ReadItems")
Return table.Rows(0)("Name")
End Function
Public Function TableOrdinal(columnName As String, index As Integer, flag As Boolean) As Object
Dim table = ReadTable("dbo.ReadItems")
Dim row = table.Rows(0)
Return row(2)
End Function
Public Function TableForeach(columnName As String, index As Integer, flag As Boolean) As Object
Dim table = ReadTable("dbo.ReadItems")
For Each row As DataRow In table.Rows
Consume(row("Id"))
Next
Return Nothing
End Function
Public Function TableRowsGuard(columnName As String, index As Integer, flag As Boolean) As Object
Dim table = ReadTable("dbo.ReadItems")
If table.Rows.Count > 0 Then
Return table.Rows(0).IsNull("Total")
End If
Return Nothing
End Function
Public Function OtherProcedure(columnName As String, index As Integer, flag As Boolean) As Object
Dim table = ReadTable("dbo.ReadOther")
Return table.Rows(0)("Title")
End Function
Public Function TwoCallers(columnName As String, index As Integer, flag As Boolean) As Object
Dim items = ReadTable("dbo.ReadItems")
Dim other = ReadTable("dbo.ReadOther")
Return CStr(items.Rows(0)("Name")) & CStr(other.Rows(0)("Title"))
End Function
Public Function SetIndex(columnName As String, index As Integer, flag As Boolean) As Object
Dim results = ReadSet("dbo.ReadItems")
Return results.Tables(0).Rows(0)("Name")
End Function
Public Function SetDefaultName(columnName As String, index As Integer, flag As Boolean) As Object
Dim results = ReadSet("dbo.ReadItems")
Return results.Tables("Table").Rows(0)("Id")
End Function
Public Function SetSourceName(columnName As String, index As Integer, flag As Boolean) As Object
Dim results = ReadNamedSet("dbo.ReadItems")
Return results.Tables!Items.Rows(0)("Total")
End Function
Public Function SetTableLocalGuards(columnName As String, index As Integer, flag As Boolean) As Object
Dim results = ReadSet("dbo.ReadItems")
If results.Tables.Count > 0 Then
Dim table = results.Tables(0)
For Each row As DataRow In table.Rows
Consume(row("Name"))
Next
End If
Return Nothing
End Function
Public Function TextHelper(columnName As String, index As Integer, flag As Boolean) As Object
Dim table = ReadTableText("dbo.ReadItems")
Return table.Rows(0)("Name")
End Function
Public Function ConnectionHelper(connection As SqlConnection) As Object
Dim table = ReadTableUsing(connection, "dbo.ReadItems")
Return table.Rows(0)("Name")
End Function
Public Function ConstProcedure(columnName As String, index As Integer, flag As Boolean) As Object
Const procedure As String = "dbo.ReadItems"
Dim table = ReadTable(procedure)
Return table.Rows(0)("Name")
End Function
Public Function MissingName(columnName As String, index As Integer, flag As Boolean) As Object
Dim table = ReadTable("dbo.ReadItems")
Return table.Rows(0)("Title")
End Function
Public Function OrdinalOutOfRange(columnName As String, index As Integer, flag As Boolean) As Object
Dim table = ReadTable("dbo.ReadItems")
Return table.Rows(0)(3)
End Function
Public Function OtherProcedureMissing(columnName As String, index As Integer, flag As Boolean) As Object
Dim table = ReadTable("dbo.ReadOther")
Return table.Rows(0)("Name")
End Function
Public Function SetMissing(columnName As String, index As Integer, flag As Boolean) As Object
Dim results = ReadSet("dbo.ReadItems")
Return results.Tables(0).Rows(0)("Title")
End Function
Public Function Renamed(columnName As String, index As Integer, flag As Boolean) As Object
Dim table = ReadTable("dbo.ReadItems")
Return table.Rows(0)!Name
End Function
Public Function CaseOnly(columnName As String, index As Integer, flag As Boolean) As Object
Dim table = ReadTable("dbo.ReadItems")
Return table.Rows(0)("name")
End Function
Public Function DynamicName(columnName As String, index As Integer, flag As Boolean) As Object
Dim table = ReadTable("dbo.ReadItems")
Return table.Rows(0)(columnName)
End Function
Public Function AbsentBinding(columnName As String, index As Integer, flag As Boolean) As Object
Dim table = ReadTable("dbo.ReadItems")
Return table.Rows(0)("Name")
End Function
Public Function Incomplete(columnName As String, index As Integer, flag As Boolean) As Object
Dim table = ReadTable("dbo.ReadItems")
Return table.Rows(0)("Name")
End Function
' ---- Helper state cells: the whole helper state is unknown with the authored gap (each helper keeps one caller).
Public Function PublicHelper(procedure As String) As DataTable
Dim command = New SqlCommand(procedure)
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Return table
End Function
Public Function UsePublicHelper() As Object
Dim table = PublicHelper("dbo.ReadItems")
Return table.Rows(0)("Name")
End Function
Friend Function InternalHelper(procedure As String) As DataTable
Dim command = New SqlCommand(procedure)
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Return table
End Function
Public Function UseInternalHelper() As Object
Dim table = InternalHelper("dbo.ReadItems")
Return table.Rows(0)("Name")
End Function
Private Function GenericHelper(Of T)(procedure As String) As DataTable
Dim command = New SqlCommand(procedure)
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Return table
End Function
Public Function UseGenericHelper() As Object
Dim table = GenericHelper(Of Integer)("dbo.ReadItems")
Return table.Rows(0)("Name")
End Function
Private Function OverloadedHelper(procedure As String) As DataTable
Dim command = New SqlCommand(procedure)
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Return table
End Function
Private Function OverloadedHelper(procedure As String, timeout As Integer) As DataTable
Dim command = New SqlCommand(procedure)
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Return table
End Function
Public Function UseOverloadedHelper() As Object
Dim table = OverloadedHelper("dbo.ReadItems")
Dim other = OverloadedHelper("dbo.ReadItems", 30)
Return table.Rows(0)("Name")
End Function
<Extension()>
Private Function ExtensionHelper(procedure As String) As DataTable
Dim command = New SqlCommand(procedure)
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Return table
End Function
Public Function UseExtensionHelper() As Object
Dim procedure = "dbo.ReadItems"
Dim table = procedure.ExtensionHelper()
Return table.Rows(0)("Name")
End Function
Private Function RefHelper(ByRef procedure As String) As DataTable
Dim command = New SqlCommand(procedure)
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Return table
End Function
Public Function UseRefHelper() As Object
Dim procedure = "dbo.ReadItems"
Dim table = RefHelper(procedure)
Return table.Rows(0)("Name")
End Function
Private Function OptionalHelper(Optional procedure As String = "dbo.ReadItems") As DataTable
Dim command = New SqlCommand(procedure)
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Return table
End Function
Public Function UseOptionalHelper() As Object
Dim table = OptionalHelper()
Return table.Rows(0)("Name")
End Function
Private Function TwoStringHelper(procedure As String, other As String) As DataTable
Dim command = New SqlCommand(procedure)
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Return table
End Function
Public Function UseTwoStringHelper() As Object
Dim table = TwoStringHelper("dbo.ReadItems", "unused")
Return table.Rows(0)("Name")
End Function
Private Function ObjectHelper(procedure As String) As Object
Dim command = New SqlCommand(procedure)
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Return table
End Function
Public Function UseObjectHelper() As Object
Dim table = ObjectHelper("dbo.ReadItems")
Return table
End Function
Private Function RecursiveHelper(procedure As String) As DataTable
If procedure.Length = 0 Then
Return RecursiveHelper("dbo.ReadItems")
End If
Dim command = New SqlCommand(procedure)
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Return table
End Function
Public Function UseRecursiveHelper() As Object
Dim table = RecursiveHelper("dbo.ReadItems")
Return table.Rows(0)("Name")
End Function
Private ReadOnly Reader As Func(Of String, DataTable) = AddressOf DelegateHelper
Private Function DelegateHelper(procedure As String) As DataTable
Dim command = New SqlCommand(procedure)
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Return table
End Function
Public Function UseDelegateHelper() As Object
Dim table = DelegateHelper("dbo.ReadItems")
Return table.Rows(0)("Name")
End Function
Public Function DelegateValue() As Object
Return Reader
End Function
Private Function MutatingHelper(procedure As String) As DataTable
Dim command = New SqlCommand(procedure)
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
table.Rows.Clear()
Return table
End Function
Public Function UseMutatingHelper() As Object
Dim table = MutatingHelper("dbo.ReadItems")
Return table.Rows(0)("Name")
End Function
Private Function GuardedHelper(procedure As String) As DataTable
Dim command = New SqlCommand(procedure)
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
If procedure.Length > 0 Then
adapter.Fill(table)
End If
Return table
End Function
Public Function UseGuardedHelper() As Object
Dim table = GuardedHelper("dbo.ReadItems")
Return table.Rows(0)("Name")
End Function
Private Function ExecutingHelper(procedure As String) As DataTable
Dim command = New SqlCommand(procedure)
command.CommandType = CommandType.StoredProcedure
command.ExecuteNonQuery()
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Return table
End Function
Public Function UseExecutingHelper() As Object
Dim table = ExecutingHelper("dbo.ReadItems")
Return table.Rows(0)("Name")
End Function
Private Function MultipleReturnHelper(procedure As String) As DataTable
Dim command = New SqlCommand(procedure)
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
If procedure.Length = 0 Then
Return table
End If
Return table
End Function
Public Function UseMultipleReturnHelper() As Object
Dim table = MultipleReturnHelper("dbo.ReadItems")
Return table.Rows(0)("Name")
End Function
Private Function ConstantTextHelper(procedure As String) As DataTable
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Return table
End Function
Public Function UseConstantTextHelper() As Object
Dim table = ConstantTextHelper("dbo.ReadOther")
Return table.Rows(0)("Name")
End Function
Private Function ParameterWriteHelper(procedure As String) As DataTable
procedure = "dbo." & procedure
Dim command = New SqlCommand(procedure)
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Return table
End Function
Public Function UseParameterWriteHelper() As Object
Dim table = ParameterWriteHelper("ReadItems")
Return table.Rows(0)("Name")
End Function
Private Function CallingHelper(procedure As String) As DataTable
Touch()
Dim command = New SqlCommand(procedure)
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Return table
End Function
Public Function UseCallingHelper() As Object
Dim table = CallingHelper("dbo.ReadItems")
Return table.Rows(0)("Name")
End Function
Private Function DisposedHelper(procedure As String) As DataTable
Dim command = New SqlCommand(procedure)
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Using table As New DataTable()
adapter.Fill(table)
Return table
End Using
End Function
Public Function UseDisposedHelper() As Object
Dim table = DisposedHelper("dbo.ReadItems")
Return table.Rows(0)("Name")
End Function
Private Function UncalledHelper(procedure As String) As DataTable
Dim command = New SqlCommand(procedure)
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Return table
End Function
Private Function TextCommandHelper(procedure As String) As DataTable
Dim command = New SqlCommand(procedure)
command.CommandType = CommandType.Text
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Return table
End Function
Public Function UseTextCommandHelper() As Object
Dim table = TextCommandHelper("SELECT 1 AS Name")
Return table.Rows(0)("Name")
End Function
Public Function InlineFill(columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Return table.Rows(0)("Name")
End Function
' ---- Caller state cells: each call site is its own entry; the helper stays bounded.
Public Function CallerNamedArgument(columnName As String, index As Integer, flag As Boolean) As Object
Dim table = ReadTable(procedure:="dbo.ReadItems")
Return table.Rows(0)("Name")
End Function
Public Function CallerDynamicProcedure(columnName As String, index As Integer, flag As Boolean) As Object
Dim table = ReadTable(columnName)
Return table.Rows(0)("Name")
End Function
Public Function CallerNullProcedure(columnName As String, index As Integer, flag As Boolean) As Object
Dim table = ReadTable(Nothing)
Return table.Rows(0)("Name")
End Function
Public Function CallerDiscarded(columnName As String, index As Integer, flag As Boolean) As Object
ReadTable("dbo.ReadItems")
Return Nothing
End Function
Public Function CallerChained(columnName As String, index As Integer, flag As Boolean) As Object
Return ReadTable("dbo.ReadItems").Rows(0)("Name")
End Function
Public Function CallerPassed(columnName As String, index As Integer, flag As Boolean) As Object
ConsumeTable(ReadTable("dbo.ReadItems"))
Return Nothing
End Function
Public Function CallerObjectLocal(columnName As String, index As Integer, flag As Boolean) As Object
Dim table As Object = ReadTable("dbo.ReadItems")
Return table
End Function
Public Function CallerRowWrite(columnName As String, index As Integer, flag As Boolean) As Object
Dim table = ReadTable("dbo.ReadItems")
table.Rows(0)("Name") = "changed"
Return Nothing
End Function
Public Function CallerRefill(columnName As String, index As Integer, flag As Boolean) As Object
Dim table = ReadTable("dbo.ReadItems")
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
adapter.Fill(table)
Return table.Rows(0)("Name")
End Function
Public Function CallerReassigned(columnName As String, index As Integer, flag As Boolean) As Object
Dim table = ReadTable("dbo.ReadItems")
table = ReadTable("dbo.ReadOther")
Return table.Rows(0)("Name")
End Function
Public Function CallerAlias(columnName As String, index As Integer, flag As Boolean) As Object
Dim table = ReadTable("dbo.ReadItems")
Dim copy = table
Return copy.Rows(0)("Name")
End Function
Public Function CallerTryCatch(columnName As String, index As Integer, flag As Boolean) As Object
Try
Dim table = ReadTable("dbo.ReadItems")
Return table.Rows(0)("Name")
Catch ex As Exception
Return Nothing
End Try
End Function
Public Function CallerLambda(columnName As String, index As Integer, flag As Boolean) As Object
Dim read As Func(Of DataTable) = Function()
Dim table = ReadTable("dbo.ReadItems")
Return table
End Function
Return read()
End Function
Public Function CallerLaterTable(columnName As String, index As Integer, flag As Boolean) As Object
Dim results = ReadSet("dbo.ReadItems")
Return results.Tables(1).Rows(0)("Value")
End Function
Public Function CallerSetMutation(columnName As String, index As Integer, flag As Boolean) As Object
Dim results = ReadSet("dbo.ReadItems")
results.AcceptChanges()
Return results.Tables(0).Rows(0)("Name")
End Function
Public Function CallerSourceNameMismatch(columnName As String, index As Integer, flag As Boolean) As Object
Dim results = ReadNamedSet("dbo.ReadItems")
Return results.Tables("Table").Rows(0)("Name")
End Function
Public Function CallerConnectionArgument(columnName As String, index As Integer, flag As Boolean) As Object
Dim table = ReadTableUsing(New SqlConnection(), "dbo.ReadItems")
Return table.Rows(0)("Name")
End Function
Public Function CallerNoReads(columnName As String, index As Integer, flag As Boolean) As Object
Dim table = ReadTable("dbo.ReadItems")
Return Nothing
End Function
End Module
' Instance and virtual helpers need a class.
Public NotInheritable Class InstanceHelpers
Private Function InstanceHelper(procedure As String) As DataTable
Dim command = New SqlCommand(procedure)
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Return table
End Function
Public Function UseInstanceHelper() As Object
Dim table = InstanceHelper("dbo.ReadItems")
Return table.Rows(0)("Name")
End Function
End Class
Public Class VirtualHelpers
Protected Overridable Function VirtualHelper(procedure As String) As DataTable
Dim command = New SqlCommand(procedure)
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Return table
End Function
Public Function UseVirtualHelper() As Object
Dim table = VirtualHelper("dbo.ReadItems")
Return table.Rows(0)("Name")
End Function
End Class
Partial Public Class PartialHelpers
Private Shared Function PartialHelper(procedure As String) As DataTable
Dim command = New SqlCommand(procedure)
command.CommandType = CommandType.StoredProcedure
Dim adapter = New SqlDataAdapter(command)
Dim table = New DataTable()
adapter.Fill(table)
Return table
End Function
End Class
Partial Public Class PartialHelpers
Public Shared Function UsePartialHelper() As Object
Dim table = PartialHelper("dbo.ReadItems")
Return table.Rows(0)("Name")
End Function
End Class
