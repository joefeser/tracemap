' Independently authored. Compile and scan only; never invoke methods.
#Disable Warning BC40000 ' Signed legacy provider static compatibility control.
Imports System.Data
Imports System.Data.SqlClient
Public Module ParameterCases
Private Sub Helper(command As SqlCommand)
End Sub
Public Function [Good](valueName As String, condition As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItem")
command.CommandText = "dbo.ReadItem"
command.CommandType = CommandType.StoredProcedure
Dim parameter = command.Parameters.Add("@Name", SqlDbType.NVarChar, 20)
parameter.Direction = ParameterDirection.Input
Return command.ExecuteScalar()
End Function
Public Function [SizeMismatch](valueName As String, condition As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItem")
command.CommandText = "dbo.ReadItem"
command.CommandType = CommandType.StoredProcedure
Dim parameter = command.Parameters.Add("@Name", SqlDbType.NVarChar, 40)
parameter.Direction = ParameterDirection.Input
Return command.ExecuteScalar()
End Function
Public Function [TypeMismatch](valueName As String, condition As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItem")
command.CommandText = "dbo.ReadItem"
command.CommandType = CommandType.StoredProcedure
Dim parameter = command.Parameters.Add("@Name", SqlDbType.Int, 20)
parameter.Direction = ParameterDirection.Input
Return command.ExecuteScalar()
End Function
Public Function [DirectionMismatch](valueName As String, condition As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItem")
command.CommandText = "dbo.ReadItem"
command.CommandType = CommandType.StoredProcedure
Dim parameter = command.Parameters.Add("@Name", SqlDbType.NVarChar, 20)
parameter.Direction = ParameterDirection.Output
Return command.ExecuteScalar()
End Function
Public Function [Max](valueName As String, condition As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItem")
command.CommandText = "dbo.ReadItem"
command.CommandType = CommandType.StoredProcedure
Dim parameter = command.Parameters.Add("@Name", SqlDbType.NVarChar, -1)
parameter.Direction = ParameterDirection.Input
Return command.ExecuteScalar()
End Function
Public Function [MissingSize](valueName As String, condition As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItem")
command.CommandText = "dbo.ReadItem"
command.CommandType = CommandType.StoredProcedure
Dim parameter = command.Parameters.Add("@Name", SqlDbType.NVarChar)
parameter.Direction = ParameterDirection.Input
Return command.ExecuteScalar()
End Function
Public Function [Ansi](valueName As String, condition As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItem")
command.CommandText = "dbo.ReadItem"
command.CommandType = CommandType.StoredProcedure
Dim parameter = command.Parameters.Add("@Name", SqlDbType.VarChar, 20)
parameter.Direction = ParameterDirection.Input
Return command.ExecuteScalar()
End Function
Public Function [Decimal](valueName As String, condition As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItem")
command.CommandText = "dbo.ReadItem"
command.CommandType = CommandType.StoredProcedure
Dim parameter = command.Parameters.Add("@Name", SqlDbType.Decimal)
parameter.Direction = ParameterDirection.Input
Return command.ExecuteScalar()
End Function
Public Function [CaseName](valueName As String, condition As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItem")
command.CommandText = "dbo.ReadItem"
command.CommandType = CommandType.StoredProcedure
Dim parameter = command.Parameters.Add("@name", SqlDbType.NVarChar, 20)
parameter.Direction = ParameterDirection.Input
Return command.ExecuteScalar()
End Function
Public Function [InputOutput](valueName As String, condition As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItem")
command.CommandText = "dbo.ReadItem"
command.CommandType = CommandType.StoredProcedure
Dim parameter = command.Parameters.Add("@Name", SqlDbType.NVarChar, 20)
parameter.Direction = ParameterDirection.InputOutput
Return command.ExecuteScalar()
End Function
Public Function [Channels](valueName As String, condition As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItem")
command.CommandText = "dbo.ReadItem"
command.CommandType = CommandType.StoredProcedure
Dim parameter = command.Parameters.Add("@Name", SqlDbType.NVarChar, 20)
parameter.Direction = ParameterDirection.Output
Dim status = command.Parameters.Add("@Status", SqlDbType.Int)
status.Direction = ParameterDirection.ReturnValue
Return command.ExecuteScalar()
End Function
Public Function [DynamicName](valueName As String, condition As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItem")
command.CommandText = "dbo.ReadItem"
command.CommandType = CommandType.StoredProcedure
Dim parameter = command.Parameters.Add(valueName, SqlDbType.NVarChar, 20)
parameter.Direction = ParameterDirection.Input
Return command.ExecuteScalar()
End Function
Public Function [Conditional](valueName As String, condition As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItem")
command.CommandText = "dbo.ReadItem"
command.CommandType = CommandType.StoredProcedure
Dim parameter = command.Parameters.Add("@Name", SqlDbType.NVarChar, 20)
parameter.Direction = ParameterDirection.Input
If condition Then parameter.Direction = ParameterDirection.Output
Return command.ExecuteScalar()
End Function
Public Function [Alias](valueName As String, condition As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItem")
command.CommandText = "dbo.ReadItem"
command.CommandType = CommandType.StoredProcedure
Dim parameter = command.Parameters.Add("@Name", SqlDbType.NVarChar, 20)
parameter.Direction = ParameterDirection.Input
Dim aliasCommand = command
aliasCommand.Parameters.Clear()
Return command.ExecuteScalar()
End Function
Public Function [ParameterAlias](valueName As String, condition As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItem")
command.CommandText = "dbo.ReadItem"
command.CommandType = CommandType.StoredProcedure
Dim parameter = command.Parameters.Add("@Name", SqlDbType.NVarChar, 20)
parameter.Direction = ParameterDirection.Input
Dim aliasParameter = parameter
aliasParameter.Direction = ParameterDirection.Output
Return command.ExecuteScalar()
End Function
Public Function [Mutation](valueName As String, condition As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItem")
command.CommandText = "dbo.ReadItem"
command.CommandType = CommandType.StoredProcedure
Dim parameter = command.Parameters.Add("@Name", SqlDbType.NVarChar, 20)
parameter.Direction = ParameterDirection.Input
parameter.Size = 40
Return command.ExecuteScalar()
End Function
Public Function [ParameterReassignment](valueName As String, condition As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItem")
command.CommandText = "dbo.ReadItem"
command.CommandType = CommandType.StoredProcedure
Dim parameter = command.Parameters.Add("@Name", SqlDbType.NVarChar, 20)
parameter.Direction = ParameterDirection.Input
parameter = New SqlParameter()
Return command.ExecuteScalar()
End Function
Public Function [HelperBoundary](valueName As String, condition As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItem")
command.CommandText = "dbo.ReadItem"
command.CommandType = CommandType.StoredProcedure
Dim parameter = command.Parameters.Add("@Name", SqlDbType.NVarChar, 20)
parameter.Direction = ParameterDirection.Input
Helper(command)
Return command.ExecuteScalar()
End Function
Public Function [Text](valueName As String, condition As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItem")
command.CommandText = "dbo.ReadItem"
command.CommandType = CommandType.Text
Dim parameter = command.Parameters.Add("@Name", SqlDbType.NVarChar, 20)
parameter.Direction = ParameterDirection.Input
Return command.ExecuteScalar()
End Function
Public Function [DynamicCommand](valueName As String, condition As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItem")
command.CommandText = valueName
command.CommandType = CommandType.StoredProcedure
Dim parameter = command.Parameters.Add("@Name", SqlDbType.NVarChar, 20)
parameter.Direction = ParameterDirection.Input
Return command.ExecuteScalar()
End Function
Public Function [Duplicate](valueName As String, condition As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItem")
command.CommandText = "dbo.ReadItem"
command.CommandType = CommandType.StoredProcedure
Dim parameter = command.Parameters.Add("@Name", SqlDbType.NVarChar, 20)
parameter.Direction = ParameterDirection.Input
command.Parameters.Add("@Name", SqlDbType.NVarChar, 20)
Return command.ExecuteScalar()
End Function
Public Function [Extra](valueName As String, condition As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItem")
command.CommandText = "dbo.ReadItem"
command.CommandType = CommandType.StoredProcedure
Dim parameter = command.Parameters.Add("@Name", SqlDbType.NVarChar, 20)
parameter.Direction = ParameterDirection.Input
command.Parameters.Add("@Extra", SqlDbType.Int)
Return command.ExecuteScalar()
End Function
Public Function [Missing](valueName As String, condition As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItem")
command.CommandText = "dbo.ReadItem"
command.CommandType = CommandType.StoredProcedure
Return command.ExecuteScalar()
End Function
Public Function [DefaultOmitted](valueName As String, condition As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItem")
command.CommandText = "dbo.ReadItem"
command.CommandType = CommandType.StoredProcedure
Return command.ExecuteScalar()
End Function
Public Function [TwoCommands](valueName As String, condition As Boolean) As Object
Dim command = New SqlCommand("dbo.ReadItem")
command.CommandText = "dbo.ReadItem"
command.CommandType = CommandType.StoredProcedure
Dim parameter = command.Parameters.Add("@Name", SqlDbType.NVarChar, 20)
parameter.Direction = ParameterDirection.Input
Dim second = New SqlCommand("dbo.ReadItem")
second.CommandType = CommandType.StoredProcedure
second.Parameters.Add("@Name", SqlDbType.NVarChar, 40)
second.ExecuteScalar()
Return command.ExecuteScalar()
End Function
End Module
