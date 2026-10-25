#Disable Warning BC40000 ' Intentional legacy-provider static fixture; never executed.
' Independently authored static-only fixture. Never execute application calls.
Imports System.Data
Imports System.Data.SqlClient
Public Module Contracts
    Public Function ReadGood() As String
        Dim command = New SqlCommand("dbo.ReadItem")
        command.CommandText = "dbo.ReadItem"
        command.CommandType = CommandType.StoredProcedure
        Dim parameter = command.Parameters.Add("@Name", SqlDbType.NVarChar, 20)
        parameter.Direction = ParameterDirection.Input
        Dim reader = command.ExecuteReader()
        Return CStr(reader("Name"))
    End Function
    Public Function SizeMismatch() As String
        Dim command = New SqlCommand("dbo.ReadItem")
        command.CommandText = "dbo.ReadItem"
        command.CommandType = CommandType.StoredProcedure
        Dim parameter = command.Parameters.Add("@Name", SqlDbType.NVarChar, 40)
        parameter.Direction = ParameterDirection.Input
        Dim reader = command.ExecuteReader()
        Return CStr(reader("Name"))
    End Function
    Public Function TypeMismatch() As String
        Dim command = New SqlCommand("dbo.ReadItem")
        command.CommandText = "dbo.ReadItem"
        command.CommandType = CommandType.StoredProcedure
        Dim parameter = command.Parameters.Add("@Name", SqlDbType.Int, 20)
        parameter.Direction = ParameterDirection.Input
        Dim reader = command.ExecuteReader()
        Return CStr(reader("Name"))
    End Function
    Public Function DirectionMismatch() As String
        Dim command = New SqlCommand("dbo.ReadItem")
        command.CommandText = "dbo.ReadItem"
        command.CommandType = CommandType.StoredProcedure
        Dim parameter = command.Parameters.Add("@Name", SqlDbType.NVarChar, 20)
        parameter.Direction = ParameterDirection.Output
        Dim reader = command.ExecuteReader()
        Return CStr(reader("Name"))
    End Function
    Public Function MissingColumn() As String
        Dim command = New SqlCommand("dbo.ReadItem")
        command.CommandText = "dbo.ReadItem"
        command.CommandType = CommandType.StoredProcedure
        Dim parameter = command.Parameters.Add("@Name", SqlDbType.NVarChar, 20)
        parameter.Direction = ParameterDirection.Input
        Dim reader = command.ExecuteReader()
        Return CStr(reader("Missing"))
    End Function
    Public Function RenamedColumn() As String
        Dim command = New SqlCommand("dbo.RenamedItem")
        command.CommandText = "dbo.RenamedItem"
        command.CommandType = CommandType.StoredProcedure
        Dim parameter = command.Parameters.Add("@Name", SqlDbType.NVarChar, 20)
        parameter.Direction = ParameterDirection.Input
        Dim reader = command.ExecuteReader()
        Return CStr(reader("Name"))
    End Function
    Public Function ConditionalShape() As String
        Dim command = New SqlCommand("dbo.ConditionalItem")
        command.CommandText = "dbo.ConditionalItem"
        command.CommandType = CommandType.StoredProcedure
        Dim parameter = command.Parameters.Add("@Name", SqlDbType.NVarChar, 20)
        parameter.Direction = ParameterDirection.Input
        Dim reader = command.ExecuteReader()
        Return CStr(reader("Name"))
    End Function
    Public Function DynamicShape() As String
        Dim command = New SqlCommand("dbo.DynamicItem")
        command.CommandText = "dbo.DynamicItem"
        command.CommandType = CommandType.StoredProcedure
        Dim parameter = command.Parameters.Add("@Name", SqlDbType.NVarChar, 20)
        parameter.Direction = ParameterDirection.Input
        Dim reader = command.ExecuteReader()
        Return CStr(reader("Name"))
    End Function
    Public Function TextControl() As String
        Dim command = New SqlCommand("SELECT Name FROM dbo.Items")
        command.CommandText = "SELECT Name FROM dbo.Items"
        command.CommandType = CommandType.Text
        Dim parameter = command.Parameters.Add("@Name", SqlDbType.NVarChar, 20)
        parameter.Direction = ParameterDirection.Input
        Dim reader = command.ExecuteReader()
        Return CStr(reader("Name"))
    End Function
    Public Function UnknownSchema() As String
        Dim command = New SqlCommand("other.ReadItem")
        command.CommandText = "other.ReadItem"
        command.CommandType = CommandType.StoredProcedure
        Dim parameter = command.Parameters.Add("@Name", SqlDbType.NVarChar, 20)
        parameter.Direction = ParameterDirection.Input
        Dim reader = command.ExecuteReader()
        Return CStr(reader("Name"))
    End Function
    Public Function Channels() As Integer
        Dim command = New SqlCommand("dbo.Channels")
        command.CommandText = "dbo.Channels"
        command.CommandType = CommandType.StoredProcedure
        Dim output = command.Parameters.Add("@Count", SqlDbType.Int)
        output.Direction = ParameterDirection.Output
        Dim status = command.Parameters.Add("@Status", SqlDbType.Int)
        status.Direction = ParameterDirection.ReturnValue
        Return command.ExecuteNonQuery() ' affected rows is neither RETURN nor OUTPUT
    End Function
    Public Function Wrapper(procedure As String) As Object
        Return Execute(procedure)
    End Function
    Private Function Execute(procedure As String) As Object
        Dim command = New SqlCommand(procedure)
        command.CommandText = procedure
        command.CommandType = CommandType.StoredProcedure
        Return command.ExecuteScalar()
    End Function
    Public Function UnknownWrapper(provider As System.Func(Of Object)) As Object
        Return provider()
    End Function
End Module
