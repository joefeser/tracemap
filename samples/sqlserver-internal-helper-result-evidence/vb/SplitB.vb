' Independently authored. Compile and scan only; never invoke methods.
#Disable Warning BC40000 ' Signed legacy provider static compatibility control.
Imports System
Imports System.Data
Imports System.Data.SqlClient
Partial Friend NotInheritable Class Split
Public Shared Function UseSplitPartialHelper() As Object
Dim table = SplitPartialHelper("dbo.ReadItems")
Return table.Rows(0)("Name")
End Function
End Class
