#Disable Warning BC40000 ' Intentional obsolete-provider compatibility fixture; never executed.
Imports System.Data
Imports System.Data.Common
Imports CommandAlias = System.Data.SqlClient.SqlCommand
Public Class SqlControls
    Private field As DbCommand
    Private Const Statement As String = "SELECT PublicId FROM PublicRows"
    Public Sub ControlLiteral()
        Call (New CommandAlias("SELECT PublicId FROM PublicRows")).ExecuteScalar()
    End Sub
    Public Sub ControlConstant()
        Call (New CommandAlias(Statement)).ExecuteNonQuery()
    End Sub
    Public Sub ControlDynamic(text As String)
        Call (New CommandAlias(text)).ExecuteReader()
    End Sub
    Public Sub ControlStoredProcedure(command As CommandAlias)
        command.CommandText = "PublicProcedure"
        command.CommandType = CommandType.StoredProcedure
        command.ExecuteNonQuery()
    End Sub
    Public Sub ControlText(command As CommandAlias)
        command.CommandText = Statement
        command.CommandType = CommandType.Text
        command.ExecuteReader()
    End Sub
    Public Sub ControlParameters(command As CommandAlias)
        Dim p = command.Parameters.Add("@PublicId", SqlDbType.Int)
        p.Direction = ParameterDirection.InputOutput
        p.Value = 1
        command.ExecuteScalar()
    End Sub
    Public Sub ControlTransaction(command As CommandAlias, transaction As System.Data.SqlClient.SqlTransaction)
        command.Transaction = transaction
        command.ExecuteNonQuery()
    End Sub
    Public Sub ControlReuse(command As CommandAlias)
        command.CommandText = Statement
        command.ExecuteScalar()
        command.CommandText = "PublicProcedure"
        command.CommandType = CommandType.StoredProcedure
        command.ExecuteNonQuery()
    End Sub
    Public Sub ControlReassignment(command As CommandAlias, other As CommandAlias)
        command = other
        command.ExecuteScalar()
    End Sub
    Public Sub ControlAlias(command As CommandAlias)
        command.ExecuteScalar()
    End Sub
    Public Sub ControlField()
        field.ExecuteScalar()
    End Sub
    Public Sub ControlFactory(factory As DbProviderFactory)
        Dim command As DbCommand = factory.CreateCommand()
        command.ExecuteScalar()
    End Sub
    Public Sub ControlInterface(command As IDbCommand)
        command.ExecuteReader()
    End Sub
    Public Sub ControlGeneric(Of T As DbCommand)(command As T)
        command.ExecuteScalar()
    End Sub
    Public Sub ControlShared(command As DbCommand)
        command.ExecuteScalar()
    End Sub
    Public Sub ControlEntry(command As DbCommand)
        ControlWrapper(command)
    End Sub
    Public Sub ControlWrapper(command As DbCommand)
        ControlShared(command)
    End Sub
    Public Sub ControlUnconnected(command As DbCommand)
        command.ExecuteNonQuery()
    End Sub
    Public Sub ControlOpenClose(connection As System.Data.SqlClient.SqlConnection)
        connection.Open()
        connection.Close()
    End Sub
    Public Sub ControlFake(command As FakeCommand)
        command.ExecuteScalar()
    End Sub
    Public Sub ControlContent()
        Dim text = "SqlCommand ExecuteNonQuery SELECT fake"
        System.GC.KeepAlive(text)
    End Sub
    Public Sub ControlBranch(command As CommandAlias, condition As Boolean)
        command.CommandText = Statement
        If condition Then command.CommandText = "DifferentPublicStatement"
        command.ExecuteScalar()
    End Sub
    Public Sub ControlUnknownCall(command As CommandAlias)
        command.CommandText = Statement
        System.GC.KeepAlive(command)
        command.ExecuteScalar()
    End Sub
    Public Sub ControlNestedAssignment(command As CommandAlias, condition As Boolean)
        command.CommandText = Statement
        System.GC.KeepAlive(If(condition, command, Nothing))
        command.ExecuteScalar()
    End Sub
    Public Sub ControlAliasEscape(command As CommandAlias)
        command.CommandText = Statement
        Dim aliasCommand = command
        aliasCommand.CommandText = "DifferentPublicStatement"
        command.ExecuteScalar()
    End Sub
    Public Sub ControlConnectionAssignment(command As CommandAlias, connection As System.Data.SqlClient.SqlConnection)
        command.Connection = connection
        command.ExecuteScalar()
    End Sub
    Public Sub ControlConnectionConstructor(connection As System.Data.SqlClient.SqlConnection)
        Call (New CommandAlias(Statement, connection)).ExecuteScalar()
    End Sub
    Public Class FakeCommand
        Public Function ExecuteScalar() As Object
            Return Nothing
        End Function
    End Class
End Class
