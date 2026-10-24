#pragma warning disable CS0618 // Intentional legacy-provider static fixture; never executed.
// Independently authored static-only fixture. Never execute application calls.
using System.Data;
using System.Data.SqlClient;
public static class Contracts {
    public static string ReadGood() {
        var command = new SqlCommand("dbo.ReadItem");
        command.CommandType = CommandType.StoredProcedure;
        var parameter = command.Parameters.Add("@Name", SqlDbType.NVarChar, 20);
        parameter.Direction = ParameterDirection.Input;
        var reader = command.ExecuteReader();
        return (string)reader["Name"];
    }
    public static string SizeMismatch() {
        var command = new SqlCommand("dbo.ReadItem");
        command.CommandType = CommandType.StoredProcedure;
        var parameter = command.Parameters.Add("@Name", SqlDbType.NVarChar, 40);
        parameter.Direction = ParameterDirection.Input;
        var reader = command.ExecuteReader();
        return (string)reader["Name"];
    }
    public static string TypeMismatch() {
        var command = new SqlCommand("dbo.ReadItem");
        command.CommandType = CommandType.StoredProcedure;
        var parameter = command.Parameters.Add("@Name", SqlDbType.Int, 20);
        parameter.Direction = ParameterDirection.Input;
        var reader = command.ExecuteReader();
        return (string)reader["Name"];
    }
    public static string DirectionMismatch() {
        var command = new SqlCommand("dbo.ReadItem");
        command.CommandType = CommandType.StoredProcedure;
        var parameter = command.Parameters.Add("@Name", SqlDbType.NVarChar, 20);
        parameter.Direction = ParameterDirection.Output;
        var reader = command.ExecuteReader();
        return (string)reader["Name"];
    }
    public static string MissingColumn() {
        var command = new SqlCommand("dbo.ReadItem");
        command.CommandType = CommandType.StoredProcedure;
        var parameter = command.Parameters.Add("@Name", SqlDbType.NVarChar, 20);
        parameter.Direction = ParameterDirection.Input;
        var reader = command.ExecuteReader();
        return (string)reader["Missing"];
    }
    public static string RenamedColumn() {
        var command = new SqlCommand("dbo.RenamedItem");
        command.CommandType = CommandType.StoredProcedure;
        var parameter = command.Parameters.Add("@Name", SqlDbType.NVarChar, 20);
        parameter.Direction = ParameterDirection.Input;
        var reader = command.ExecuteReader();
        return (string)reader["Name"];
    }
    public static string ConditionalShape() {
        var command = new SqlCommand("dbo.ConditionalItem");
        command.CommandType = CommandType.StoredProcedure;
        var parameter = command.Parameters.Add("@Name", SqlDbType.NVarChar, 20);
        parameter.Direction = ParameterDirection.Input;
        var reader = command.ExecuteReader();
        return (string)reader["Name"];
    }
    public static string DynamicShape() {
        var command = new SqlCommand("dbo.DynamicItem");
        command.CommandType = CommandType.StoredProcedure;
        var parameter = command.Parameters.Add("@Name", SqlDbType.NVarChar, 20);
        parameter.Direction = ParameterDirection.Input;
        var reader = command.ExecuteReader();
        return (string)reader["Name"];
    }
    public static string TextControl() {
        var command = new SqlCommand("SELECT Name FROM dbo.Items");
        command.CommandType = CommandType.Text;
        var parameter = command.Parameters.Add("@Name", SqlDbType.NVarChar, 20);
        parameter.Direction = ParameterDirection.Input;
        var reader = command.ExecuteReader();
        return (string)reader["Name"];
    }
    public static string UnknownSchema() {
        var command = new SqlCommand("other.ReadItem");
        command.CommandType = CommandType.StoredProcedure;
        var parameter = command.Parameters.Add("@Name", SqlDbType.NVarChar, 20);
        parameter.Direction = ParameterDirection.Input;
        var reader = command.ExecuteReader();
        return (string)reader["Name"];
    }
    public static int Channels() {
        var command = new SqlCommand("dbo.Channels");
        command.CommandType = CommandType.StoredProcedure;
        var output = command.Parameters.Add("@Count", SqlDbType.Int);
        output.Direction = ParameterDirection.Output;
        var status = command.Parameters.Add("@Status", SqlDbType.Int);
        status.Direction = ParameterDirection.ReturnValue;
        return command.ExecuteNonQuery(); // affected rows is neither RETURN nor OUTPUT
    }
    public static object Wrapper(string procedure) => Execute(procedure);
    private static object Execute(string procedure) {
        var command = new SqlCommand(procedure);
        command.CommandType = CommandType.StoredProcedure;
        return command.ExecuteScalar();
    }
    public static object UnknownWrapper(System.Func<object> provider) => provider();
}
