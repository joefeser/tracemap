' Independently authored. Compile and scan only; never invoke methods.
#Disable Warning BC40000 ' Signed legacy provider static compatibility control.
Imports System
Imports System.Data
Imports System.Data.SqlClient
Friend Module Escapes
' AddressOf and NameOf references in another document make each helper unknown.
Friend ReadOnly Reader As Func(Of String, DataTable) = AddressOf InternalData.CrossDelegateHelper
Friend ReadOnly HelperName As String = NameOf(InternalData.CrossNameofHelper)
End Module
