// Independently authored. Compile and scan only; never invoke methods.
#pragma warning disable CS0618 // Signed legacy provider static compatibility control.
using System.Data;
using System.Data.SqlClient;
public static class ParameterCases {
private static void Helper(SqlCommand command) { }
public static object Good(string dynamicName, bool condition) {
var command = new SqlCommand("dbo.ReadItem");
command.CommandText = "dbo.ReadItem";
command.CommandType = CommandType.StoredProcedure;
var parameter = command.Parameters.Add("@Name", SqlDbType.NVarChar, 20);
parameter.Direction = ParameterDirection.Input;
return command.ExecuteScalar();
}
public static object SizeMismatch(string dynamicName, bool condition) {
var command = new SqlCommand("dbo.ReadItem");
command.CommandText = "dbo.ReadItem";
command.CommandType = CommandType.StoredProcedure;
var parameter = command.Parameters.Add("@Name", SqlDbType.NVarChar, 40);
parameter.Direction = ParameterDirection.Input;
return command.ExecuteScalar();
}
public static object TypeMismatch(string dynamicName, bool condition) {
var command = new SqlCommand("dbo.ReadItem");
command.CommandText = "dbo.ReadItem";
command.CommandType = CommandType.StoredProcedure;
var parameter = command.Parameters.Add("@Name", SqlDbType.Int, 20);
parameter.Direction = ParameterDirection.Input;
return command.ExecuteScalar();
}
public static object DirectionMismatch(string dynamicName, bool condition) {
var command = new SqlCommand("dbo.ReadItem");
command.CommandText = "dbo.ReadItem";
command.CommandType = CommandType.StoredProcedure;
var parameter = command.Parameters.Add("@Name", SqlDbType.NVarChar, 20);
parameter.Direction = ParameterDirection.Output;
return command.ExecuteScalar();
}
public static object Max(string dynamicName, bool condition) {
var command = new SqlCommand("dbo.ReadItem");
command.CommandText = "dbo.ReadItem";
command.CommandType = CommandType.StoredProcedure;
var parameter = command.Parameters.Add("@Name", SqlDbType.NVarChar, -1);
parameter.Direction = ParameterDirection.Input;
return command.ExecuteScalar();
}
public static object MissingSize(string dynamicName, bool condition) {
var command = new SqlCommand("dbo.ReadItem");
command.CommandText = "dbo.ReadItem";
command.CommandType = CommandType.StoredProcedure;
var parameter = command.Parameters.Add("@Name", SqlDbType.NVarChar);
parameter.Direction = ParameterDirection.Input;
return command.ExecuteScalar();
}
public static object Ansi(string dynamicName, bool condition) {
var command = new SqlCommand("dbo.ReadItem");
command.CommandText = "dbo.ReadItem";
command.CommandType = CommandType.StoredProcedure;
var parameter = command.Parameters.Add("@Name", SqlDbType.VarChar, 20);
parameter.Direction = ParameterDirection.Input;
return command.ExecuteScalar();
}
public static object Decimal(string dynamicName, bool condition) {
var command = new SqlCommand("dbo.ReadItem");
command.CommandText = "dbo.ReadItem";
command.CommandType = CommandType.StoredProcedure;
var parameter = command.Parameters.Add("@Name", SqlDbType.Decimal);
parameter.Direction = ParameterDirection.Input;
return command.ExecuteScalar();
}
public static object CaseName(string dynamicName, bool condition) {
var command = new SqlCommand("dbo.ReadItem");
command.CommandText = "dbo.ReadItem";
command.CommandType = CommandType.StoredProcedure;
var parameter = command.Parameters.Add("@name", SqlDbType.NVarChar, 20);
parameter.Direction = ParameterDirection.Input;
return command.ExecuteScalar();
}
public static object InputOutput(string dynamicName, bool condition) {
var command = new SqlCommand("dbo.ReadItem");
command.CommandText = "dbo.ReadItem";
command.CommandType = CommandType.StoredProcedure;
var parameter = command.Parameters.Add("@Name", SqlDbType.NVarChar, 20);
parameter.Direction = ParameterDirection.InputOutput;
return command.ExecuteScalar();
}
public static object Channels(string dynamicName, bool condition) {
var command = new SqlCommand("dbo.ReadItem");
command.CommandText = "dbo.ReadItem";
command.CommandType = CommandType.StoredProcedure;
var parameter = command.Parameters.Add("@Name", SqlDbType.NVarChar, 20);
parameter.Direction = ParameterDirection.Output;
var status = command.Parameters.Add("@Status", SqlDbType.Int); status.Direction = ParameterDirection.ReturnValue;
return command.ExecuteScalar();
}
public static object DynamicName(string dynamicName, bool condition) {
var command = new SqlCommand("dbo.ReadItem");
command.CommandText = "dbo.ReadItem";
command.CommandType = CommandType.StoredProcedure;
var parameter = command.Parameters.Add(dynamicName, SqlDbType.NVarChar, 20);
parameter.Direction = ParameterDirection.Input;
return command.ExecuteScalar();
}
public static object Conditional(string dynamicName, bool condition) {
var command = new SqlCommand("dbo.ReadItem");
command.CommandText = "dbo.ReadItem";
command.CommandType = CommandType.StoredProcedure;
var parameter = command.Parameters.Add("@Name", SqlDbType.NVarChar, 20);
parameter.Direction = ParameterDirection.Input;
if (condition) parameter.Direction = ParameterDirection.Output;
return command.ExecuteScalar();
}
public static object Alias(string dynamicName, bool condition) {
var command = new SqlCommand("dbo.ReadItem");
command.CommandText = "dbo.ReadItem";
command.CommandType = CommandType.StoredProcedure;
var parameter = command.Parameters.Add("@Name", SqlDbType.NVarChar, 20);
parameter.Direction = ParameterDirection.Input;
var alias = command; alias.Parameters.Clear();
return command.ExecuteScalar();
}
public static object ParameterAlias(string dynamicName, bool condition) {
var command = new SqlCommand("dbo.ReadItem");
command.CommandText = "dbo.ReadItem";
command.CommandType = CommandType.StoredProcedure;
var parameter = command.Parameters.Add("@Name", SqlDbType.NVarChar, 20);
parameter.Direction = ParameterDirection.Input;
var alias = parameter; alias.Direction = ParameterDirection.Output;
return command.ExecuteScalar();
}
public static object Mutation(string dynamicName, bool condition) {
var command = new SqlCommand("dbo.ReadItem");
command.CommandText = "dbo.ReadItem";
command.CommandType = CommandType.StoredProcedure;
var parameter = command.Parameters.Add("@Name", SqlDbType.NVarChar, 20);
parameter.Direction = ParameterDirection.Input;
parameter.Size = 40;
return command.ExecuteScalar();
}
public static object ParameterReassignment(string dynamicName, bool condition) {
var command = new SqlCommand("dbo.ReadItem");
command.CommandText = "dbo.ReadItem";
command.CommandType = CommandType.StoredProcedure;
var parameter = command.Parameters.Add("@Name", SqlDbType.NVarChar, 20);
parameter.Direction = ParameterDirection.Input;
parameter = new SqlParameter();
return command.ExecuteScalar();
}
public static object HelperBoundary(string dynamicName, bool condition) {
var command = new SqlCommand("dbo.ReadItem");
command.CommandText = "dbo.ReadItem";
command.CommandType = CommandType.StoredProcedure;
var parameter = command.Parameters.Add("@Name", SqlDbType.NVarChar, 20);
parameter.Direction = ParameterDirection.Input;
Helper(command);
return command.ExecuteScalar();
}
public static object Text(string dynamicName, bool condition) {
var command = new SqlCommand("dbo.ReadItem");
command.CommandText = "dbo.ReadItem";
command.CommandType = CommandType.Text;
var parameter = command.Parameters.Add("@Name", SqlDbType.NVarChar, 20);
parameter.Direction = ParameterDirection.Input;
return command.ExecuteScalar();
}
public static object DynamicCommand(string dynamicName, bool condition) {
var command = new SqlCommand("dbo.ReadItem");
command.CommandText = dynamicName;
command.CommandType = CommandType.StoredProcedure;
var parameter = command.Parameters.Add("@Name", SqlDbType.NVarChar, 20);
parameter.Direction = ParameterDirection.Input;
return command.ExecuteScalar();
}
public static object Duplicate(string dynamicName, bool condition) {
var command = new SqlCommand("dbo.ReadItem");
command.CommandText = "dbo.ReadItem";
command.CommandType = CommandType.StoredProcedure;
var parameter = command.Parameters.Add("@Name", SqlDbType.NVarChar, 20);
parameter.Direction = ParameterDirection.Input;
command.Parameters.Add("@Name", SqlDbType.NVarChar, 20);
return command.ExecuteScalar();
}
public static object Extra(string dynamicName, bool condition) {
var command = new SqlCommand("dbo.ReadItem");
command.CommandText = "dbo.ReadItem";
command.CommandType = CommandType.StoredProcedure;
var parameter = command.Parameters.Add("@Name", SqlDbType.NVarChar, 20);
parameter.Direction = ParameterDirection.Input;
command.Parameters.Add("@Extra", SqlDbType.Int);
return command.ExecuteScalar();
}
public static object Missing(string dynamicName, bool condition) {
var command = new SqlCommand("dbo.ReadItem");
command.CommandText = "dbo.ReadItem";
command.CommandType = CommandType.StoredProcedure;
return command.ExecuteScalar();
}
public static object DefaultOmitted(string dynamicName, bool condition) {
var command = new SqlCommand("dbo.ReadItem");
command.CommandText = "dbo.ReadItem";
command.CommandType = CommandType.StoredProcedure;
return command.ExecuteScalar();
}
public static object TwoCommands(string dynamicName, bool condition) {
var command = new SqlCommand("dbo.ReadItem");
command.CommandText = "dbo.ReadItem";
command.CommandType = CommandType.StoredProcedure;
var parameter = command.Parameters.Add("@Name", SqlDbType.NVarChar, 20);
parameter.Direction = ParameterDirection.Input;
var second = new SqlCommand("dbo.ReadItem"); second.CommandType = CommandType.StoredProcedure; second.Parameters.Add("@Name", SqlDbType.NVarChar, 40); second.ExecuteScalar();
return command.ExecuteScalar();
}
}
