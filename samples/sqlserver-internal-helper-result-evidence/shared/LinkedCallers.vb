' Independently authored. Compile and scan only; never invoke methods.
#Disable Warning BC40000 ' Signed legacy provider static compatibility control.
Imports System
Imports System.Data
Imports System.Data.SqlClient
' Compiled into vb/InternalHelpers.vbproj through MSBuild Link from outside the project directory.
Friend Module LinkedCallers
Public Function UseLinkedCallerHelper() As Object
Dim table = InternalData.LinkedCallerHelper("dbo.ReadItems")
Return table.Rows(0)("Name")
End Function
End Module
