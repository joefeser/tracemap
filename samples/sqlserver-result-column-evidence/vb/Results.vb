' Independently authored. Compile and scan only; never invoke methods.
#Disable Warning BC40000 ' Signed legacy provider static compatibility control.
Imports System.Data
Imports System.Data.SqlClient
Public Module ResultCases
Private Sub Consume(reader As SqlDataReader)
End Sub
Public Function [NameMatch](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim reader = command.ExecuteReader()
Return reader("Name")
End Function
Public Function [MissingName](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim reader = command.ExecuteReader()
Return reader("Title")
End Function
Public Function [Renamed](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim reader = command.ExecuteReader()
Return reader("Name")
End Function
Public Function [OrdinalValid](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim reader = command.ExecuteReader()
Return reader.GetInt32(0)
End Function
Public Function [OrdinalOutOfRange](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim reader = command.ExecuteReader()
Return reader.GetString(3)
End Function
Public Function [Reordered](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim reader = command.ExecuteReader()
Dim id = reader("Id")
Return reader("Total")
End Function
Public Function [GetOrdinalDirect](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim reader = command.ExecuteReader()
Return reader.GetString(reader.GetOrdinal("Name"))
End Function
Public Function [GetOrdinalLocal](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim reader = command.ExecuteReader()
Dim ordinal = reader.GetOrdinal("Total")
Return reader.GetDecimal(ordinal)
End Function
Public Function [GetOrdinalMissing](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim reader = command.ExecuteReader()
Dim ordinal = reader.GetOrdinal("Price")
Return reader.GetDecimal(ordinal)
End Function
Public Function [CaseOnly](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim reader = command.ExecuteReader()
Return reader("name")
End Function
Public Function [DuplicateDeclared](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim reader = command.ExecuteReader()
Return reader("Name")
End Function
Public Function [DynamicName](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim reader = command.ExecuteReader()
Return reader(columnName)
End Function
Public Function [DynamicOrdinal](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim reader = command.ExecuteReader()
Return reader.GetInt32(index)
End Function
Public Function [AbsentBinding](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim reader = command.ExecuteReader()
Return reader("Name")
End Function
Public Function [ReaderAlias](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim reader = command.ExecuteReader()
Dim [alias] = reader
Return [alias]("Name")
End Function
Public Function [ReaderReassignment](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim reader = command.ExecuteReader()
Dim value = reader("Name")
reader = Nothing
Return value
End Function
Public Function [CommandAlias](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
Dim [alias] = command
command.CommandType = CommandType.StoredProcedure
Dim reader = command.ExecuteReader()
Return reader("Name")
End Function
Public Function [OrdinalAlias](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim reader = command.ExecuteReader()
Dim ordinal = reader.GetOrdinal("Name")
Dim copy = ordinal
Return reader.GetString(copy)
End Function
Public Function [OrdinalReassignment](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim reader = command.ExecuteReader()
Dim ordinal = reader.GetOrdinal("Name")
ordinal = 2
Return reader.GetString(ordinal)
End Function
Public Function [HelperEscape](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim reader = command.ExecuteReader()
Consume(reader)
Return reader("Name")
End Function
Public Function [TwoReaders](columnName As String, index As Integer, flag As Boolean) As Object
Dim first = New SqlCommand("dbo.ReadItems")
first.CommandType = CommandType.StoredProcedure
Dim second = New SqlCommand("dbo.ReadOther")
second.CommandType = CommandType.StoredProcedure
Dim firstReader = first.ExecuteReader()
Dim secondReader = second.ExecuteReader()
Dim name = firstReader("Name")
Return secondReader("Title")
End Function
Public Function [ReadLoop](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim reader = command.ExecuteReader()
Dim last As Object = Nothing
While reader.Read()
last = reader("Name")
Dim id = reader.GetInt32(0)
End While
Return last
End Function
Public Function [ReadGuard](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim reader = command.ExecuteReader()
If reader.Read() Then
Return reader("Total")
End If
Return Nothing
End Function
Public Function [UsingLifetime](columnName As String, index As Integer, flag As Boolean) As Object
Using command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Using reader = command.ExecuteReader()
Return reader("Id")
End Using
End Using
Return Nothing
End Function
Public Function [NextResult](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim reader = command.ExecuteReader()
reader.NextResult()
Return reader("Name")
End Function
Public Function [ScalarChannel](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Return command.ExecuteScalar()
End Function
Public Function [NonQueryChannel](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Return command.ExecuteNonQuery()
End Function
Public Function [RecordsAffected](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim reader = command.ExecuteReader()
Return reader.RecordsAffected
End Function
Public Function [Incomplete](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim reader = command.ExecuteReader()
Return reader("Name")
End Function
Public Function [TextCommand](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.Text
Dim reader = command.ExecuteReader()
Return reader("Name")
End Function
Public Function [Conditional](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
If flag Then command.CommandText = "dbo.ReadOther"
Dim reader = command.ExecuteReader()
Return reader("Name")
End Function
Public Function [WithParameter](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
command.Parameters.Add("@Id", SqlDbType.Int)
Dim reader = command.ExecuteReader()
Return reader("Name")
End Function
Public Function [NullCheck](columnName As String, index As Integer, flag As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItems")
command.CommandType = CommandType.StoredProcedure
Dim reader = command.ExecuteReader()
Return reader.IsDBNull(1)
End Function
End Module
