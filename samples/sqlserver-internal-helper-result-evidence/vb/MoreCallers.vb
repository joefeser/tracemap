' Independently authored. Compile and scan only; never invoke methods.
#Disable Warning BC40000 ' Signed legacy provider static compatibility control.
Imports System
Imports System.Data
Imports System.Data.SqlClient
Friend Module MoreCallers
Public Function OtherTypeCaller(columnName As String, index As Integer, flag As Boolean) As Object
Dim table = InternalData.ReadTable("dbo.ReadItems")
Return table.Rows(0)("Id")
End Function
End Module
