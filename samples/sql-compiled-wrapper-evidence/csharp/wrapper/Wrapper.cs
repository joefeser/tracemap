using System.Data;
using System.Data.SqlClient;

namespace PublicSql.CSharp
{
    public sealed class FieldParameter
    {
        public readonly string Name;
        public readonly SqlDbType Type;
        public readonly int Size;
        public FieldParameter(string name, SqlDbType type, int size) { Name = name; Type = type; Size = size; }
    }
    public sealed class PropertyParameter
    {
        public string Name { get; }
        public SqlDbType Type { get; }
        public int Size { get; }
        public PropertyParameter(string name, SqlDbType type, int size) { Name = name; Type = type; Size = size; }
    }
    public static class Wrapper
    {
        public static DataSet Direct(string procedure)
        {
            var command = new SqlCommand(procedure); command.CommandType = CommandType.StoredProcedure;
            var parameter = new SqlParameter("@Name", SqlDbType.NVarChar, 40);
            command.Parameters.Add(parameter);
            var adapter = new SqlDataAdapter(command); var result = new DataSet();
            adapter.Fill(result); return result;
        }
        public static DataSet Forwarded(string procedure, SqlParameter parameter)
        {
            var command = new SqlCommand(procedure); command.CommandType = CommandType.StoredProcedure;
            command.Parameters.Add(parameter);
            var adapter = new SqlDataAdapter(command); var result = new DataSet();
            adapter.Fill(result); return result;
        }
        public static DataSet FieldObject(string procedure, FieldParameter value)
        { return Forwarded(procedure, new SqlParameter(value.Name, value.Type, value.Size)); }
        public static DataSet PropertyObject(string procedure, PropertyParameter value)
        { return Forwarded(procedure, new SqlParameter(value.Name, value.Type, value.Size)); }
        public static DataSet Mutated(string procedure)
        {
            var parameter = new SqlParameter("@Name", SqlDbType.NVarChar, 40);
            parameter.Size = 80; return Forwarded(procedure, parameter);
        }
        public static DataSet Escaped(string procedure)
        {
            var parameter = new SqlParameter("@Name", SqlDbType.NVarChar, 40);
            UnknownEffect(parameter); return Forwarded(procedure, parameter);
        }
        public static DataSet Branch(string procedure, bool condition)
        {
            var parameter = new SqlParameter("@Name", SqlDbType.NVarChar, condition ? 40 : 80);
            return Forwarded(procedure, parameter);
        }
        private static void UnknownEffect(SqlParameter parameter) { parameter.Size = 120; }
    }
}
