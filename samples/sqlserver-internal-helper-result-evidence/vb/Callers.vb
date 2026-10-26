' Independently authored. Compile and scan only; never invoke methods.
#Disable Warning BC40000 ' Signed legacy provider static compatibility control.
Imports System
Imports System.Data
Imports System.Data.SqlClient
Friend Module Callers
Private Sub Consume(value As Object)
End Sub
' ---- Core caller cells and caller state cells in another document.
Public Function CrossTableName(columnName As String, index As Integer, flag As Boolean) As Object
Dim table = InternalData.ReadTable("dbo.ReadItems")
Return table.Rows(0)("Name")
End Function
Public Function CrossTableOrdinal(columnName As String, index As Integer, flag As Boolean) As Object
Dim table = InternalData.ReadTable("dbo.ReadItems")
Dim row = table.Rows(0)
Return row(2)
End Function
Public Function CrossTableForeach(columnName As String, index As Integer, flag As Boolean) As Object
Dim table = InternalData.ReadTable("dbo.ReadItems")
For Each row As DataRow In table.Rows
Consume(row("Id"))
Next
Return Nothing
End Function
Public Function CrossOtherProcedure(columnName As String, index As Integer, flag As Boolean) As Object
Dim table = InternalData.ReadTable("dbo.ReadOther")
Return table.Rows(0)("Title")
End Function
Public Function CrossTwoCallers(columnName As String, index As Integer, flag As Boolean) As Object
Dim items = InternalData.ReadTable("dbo.ReadItems")
Dim other = InternalData.ReadTable("dbo.ReadOther")
Return CStr(items.Rows(0)("Name")) & CStr(other.Rows(0)("Title"))
End Function
Public Function CrossSetIndex(columnName As String, index As Integer, flag As Boolean) As Object
Dim results = InternalData.ReadSet("dbo.ReadItems")
Return results.Tables(0).Rows(0)("Name")
End Function
Public Function CrossSetGuards(columnName As String, index As Integer, flag As Boolean) As Object
Dim results = InternalData.ReadSet("dbo.ReadItems")
If results.Tables.Count > 0 Then
Dim table = results.Tables(0)
For Each row As DataRow In table.Rows
Consume(row("Name"))
Next
End If
Return Nothing
End Function
Public Function CrossTextHelper(columnName As String, index As Integer, flag As Boolean) As Object
Dim table = InternalData.ReadTableText("dbo.ReadItems")
Return table.Rows(0)("Name")
End Function
Public Function CrossConnectionHelper(connection As SqlConnection) As Object
Dim table = InternalData.ReadTableUsing(connection, "dbo.ReadItems")
Return table.Rows(0)("Name")
End Function
Public Function CrossMissingName(columnName As String, index As Integer, flag As Boolean) As Object
Dim table = InternalData.ReadTable("dbo.ReadItems")
Return table.Rows(0)("Title")
End Function
Public Function CrossOrdinalOutOfRange(columnName As String, index As Integer, flag As Boolean) As Object
Dim table = InternalData.ReadTable("dbo.ReadItems")
Return table.Rows(0)(3)
End Function
Public Function CrossOtherProcedureMissing(columnName As String, index As Integer, flag As Boolean) As Object
Dim table = InternalData.ReadTable("dbo.ReadOther")
Return table.Rows(0)("Name")
End Function
Public Function CrossRenamed(columnName As String, index As Integer, flag As Boolean) As Object
Dim table = InternalData.ReadTable("dbo.ReadItems")
Return table.Rows(0)("Name")
End Function
Public Function CrossCaseOnly(columnName As String, index As Integer, flag As Boolean) As Object
Dim table = InternalData.ReadTable("dbo.ReadItems")
Return table.Rows(0)("name")
End Function
Public Function CrossDynamicName(columnName As String, index As Integer, flag As Boolean) As Object
Dim table = InternalData.ReadTable("dbo.ReadItems")
Return table.Rows(0)(columnName)
End Function
Public Function CrossAbsentBinding(columnName As String, index As Integer, flag As Boolean) As Object
Dim table = InternalData.ReadTable("dbo.ReadItems")
Return table.Rows(0)("Name")
End Function
Public Function CrossIncomplete(columnName As String, index As Integer, flag As Boolean) As Object
Dim table = InternalData.ReadTable("dbo.ReadItems")
Return table.Rows(0)("Name")
End Function
Public Function CrossNamedArgument(columnName As String, index As Integer, flag As Boolean) As Object
Dim table = InternalData.ReadTable(procedure:="dbo.ReadItems")
Return table.Rows(0)("Name")
End Function
Public Function CrossDynamicProcedure(columnName As String, index As Integer, flag As Boolean) As Object
Dim table = InternalData.ReadTable(columnName)
Return table.Rows(0)("Name")
End Function
Public Function CrossDiscarded(columnName As String, index As Integer, flag As Boolean) As Object
InternalData.ReadTable("dbo.ReadItems")
Return Nothing
End Function
Public Function CrossRowWrite(columnName As String, index As Integer, flag As Boolean) As Object
Dim table = InternalData.ReadTable("dbo.ReadItems")
table.Rows(0)("Name") = "changed"
Return Nothing
End Function
Public Function CrossAlias(columnName As String, index As Integer, flag As Boolean) As Object
Dim table = InternalData.ReadTable("dbo.ReadItems")
Dim copy = table
Return copy.Rows(0)("Name")
End Function
Public Function CrossTryCatch(columnName As String, index As Integer, flag As Boolean) As Object
Try
Dim table = InternalData.ReadTable("dbo.ReadItems")
Return table.Rows(0)("Name")
Catch ex As Exception
Return Nothing
End Try
End Function
Public Function CrossLaterTable(columnName As String, index As Integer, flag As Boolean) As Object
Dim results = InternalData.ReadSet("dbo.ReadItems")
Return results.Tables(1).Rows(0)("Value")
End Function
Public Function CrossNoReads(columnName As String, index As Integer, flag As Boolean) As Object
Dim table = InternalData.ReadTable("dbo.ReadItems")
Return Nothing
End Function
' ---- Callers of refused helpers (no caller entries).
Public Function UsePublicHelper() As Object
Dim table = InternalData.PublicHelper("dbo.ReadItems")
Return table.Rows(0)("Name")
End Function
Public Function UseCrossDelegateHelper() As Object
Dim table = InternalData.CrossDelegateHelper("dbo.ReadItems")
Return table.Rows(0)("Name")
End Function
Public Function UseCrossNameofHelper() As Object
Dim table = InternalData.CrossNameofHelper("dbo.ReadItems")
Return table.Rows(0)("Name")
End Function
Public Function UseLinkedFileHelper() As Object
Dim table = LinkedHelpers.LinkedFileHelper("dbo.ReadItems")
Return table.Rows(0)("Name")
End Function
End Module
