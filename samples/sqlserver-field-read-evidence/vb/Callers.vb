' Independently authored. Compile and scan only; never invoke methods.
#Disable Warning BC40000 ' Signed legacy provider static compatibility control.
Imports System
Imports System.Data
Imports System.Data.SqlClient
Imports System.Linq
Friend Module FieldCallers
' ---- Internal-helper callers in another document: core cells, then caller state cells.
Public Function InternalFieldName(columnName As String, column As DataColumn, flag As Boolean) As Object
Dim table = InternalData.ReadShared("dbo.ReadItems")
Return table.Rows(0).Field(Of String)("Name")
End Function
Public Function InternalFieldOrdinal(columnName As String, column As DataColumn, flag As Boolean) As Object
Dim table = InternalData.ReadShared("dbo.ReadItems")
Dim row = table.Rows(0)
Return row.Field(Of Decimal)(2)
End Function
Public Function InternalFieldSet(columnName As String, column As DataColumn, flag As Boolean) As Object
Dim results = InternalData.ReadSharedSet("dbo.ReadItems")
Return results.Tables(0).Rows(0).Field(Of String)("Name")
End Function
Public Function InternalFieldMissing(columnName As String, column As DataColumn, flag As Boolean) As Object
Dim table = InternalData.ReadShared("dbo.ReadItems")
Return table.Rows(0).Field(Of String)("Missing")
End Function
Public Function InternalFieldVersion(columnName As String, column As DataColumn, flag As Boolean) As Object
Dim table = InternalData.ReadShared("dbo.ReadItems")
Return table.Rows(0).Field(Of String)("Name", DataRowVersion.Original)
End Function
Public Function InternalNoField(columnName As String, column As DataColumn, flag As Boolean) As Object
Dim table = InternalData.ReadShared("dbo.ReadItems")
Return table.Rows(0)("Name")
End Function
End Module
