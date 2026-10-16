#pragma warning disable CS0618 // Intentional obsolete-provider compatibility fixture; never executed.
using System.Data;
using System.Data.Common;
using CommandAlias = System.Data.SqlClient.SqlCommand;
public class SqlControls {
    private DbCommand field = null;
    private const string Statement = "SELECT PublicId FROM PublicRows";
    public void ControlLiteral() { new CommandAlias("SELECT PublicId FROM PublicRows").ExecuteScalar(); }
    public void ControlConstant() { new CommandAlias(Statement).ExecuteNonQuery(); }
    public void ControlDynamic(string text) { new CommandAlias(text).ExecuteReader(); }
    public void ControlStoredProcedure(CommandAlias command) { command.CommandText = "PublicProcedure"; command.CommandType = CommandType.StoredProcedure; command.ExecuteNonQuery(); }
    public void ControlText(CommandAlias command) { command.CommandText = Statement; command.CommandType = CommandType.Text; command.ExecuteReader(); }
    public void ControlParameters(CommandAlias command) { var p = command.Parameters.Add("@PublicId", SqlDbType.Int); p.Direction = ParameterDirection.InputOutput; p.Value = 1; command.ExecuteScalar(); }
    public void ControlTransaction(CommandAlias command, System.Data.SqlClient.SqlTransaction transaction) { command.Transaction = transaction; command.ExecuteNonQuery(); }
    public void ControlReuse(CommandAlias command) { command.CommandText = Statement; command.ExecuteScalar(); command.CommandText = "PublicProcedure"; command.CommandType = CommandType.StoredProcedure; command.ExecuteNonQuery(); }
    public void ControlReassignment(CommandAlias command, CommandAlias other) { command = other; command.ExecuteScalar(); }
    public void ControlAlias(CommandAlias command) { command.ExecuteScalar(); }
    public void ControlField() { field.ExecuteScalar(); }
    public void ControlFactory(DbProviderFactory factory) { DbCommand command = factory.CreateCommand(); command.ExecuteScalar(); }
    public void ControlInterface(IDbCommand command) { command.ExecuteReader(); }
    public void ControlGeneric<T>(T command) where T:DbCommand { command.ExecuteScalar(); }
    public void ControlShared(DbCommand command) { command.ExecuteScalar(); }
    public void ControlEntry(DbCommand command) { ControlWrapper(command); }
    public void ControlWrapper(DbCommand command) { ControlShared(command); }
    public void ControlUnconnected(DbCommand command) { command.ExecuteNonQuery(); }
    public void ControlOpenClose(System.Data.SqlClient.SqlConnection connection) { connection.Open(); connection.Close(); }
    public void ControlFake(FakeCommand command) { command.ExecuteScalar(); }
    public void ControlContent() { string text = "SqlCommand ExecuteNonQuery SELECT fake"; System.GC.KeepAlive(text); }
    public void ControlBranch(CommandAlias command, bool condition) { command.CommandText = Statement; if (condition) command.CommandText = "DifferentPublicStatement"; command.ExecuteScalar(); }
    public void ControlUnknownCall(CommandAlias command) { command.CommandText = Statement; System.GC.KeepAlive(command); command.ExecuteScalar(); }
    public void ControlNestedAssignment(CommandAlias command, bool condition) { System.GC.KeepAlive(condition ? (command.CommandText = Statement) : "different"); command.ExecuteScalar(); }
    public void ControlAliasEscape(CommandAlias command) { command.CommandText = Statement; var alias = command; alias.CommandText = "DifferentPublicStatement"; command.ExecuteScalar(); }
    public void ControlConnectionAssignment(CommandAlias command, System.Data.SqlClient.SqlConnection connection) { command.Connection = connection; command.ExecuteScalar(); }
    public void ControlConnectionConstructor(System.Data.SqlClient.SqlConnection connection) { new CommandAlias(Statement, connection).ExecuteScalar(); }
    public class FakeCommand { public object ExecuteScalar() { return null; } }
}
