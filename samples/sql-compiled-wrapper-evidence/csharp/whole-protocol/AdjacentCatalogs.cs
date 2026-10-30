using System;
using System.Collections;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
namespace PublicSql.CSharp
{
    // Independently authored adjacent-catalog fixtures for the whole-protocol batch:
    // command/adapter configuration, typed-array parameter flow, VB scalar helpers,
    // string concatenation, the configuration opaque chain and event-log tracing.
    public sealed class AdjacentRoot
    {
        public DataSet AdjacentProduce(string connection)
        {
            var wrapper = new AdjacentWrapper(connection);
            return wrapper.Execute();
        }
        public DataSet AdjacentTypedArray(string connection)
        {
            var wrapper = new AdjacentWrapper(connection);
            return wrapper.ExecuteTypedArray();
        }
        // AdjacentConfigProduce lives on AdjacentConfigRoot itself: the opaque
        public DataSet AdjacentEventLog(string connection)
        {
            var wrapper = new AdjacentWrapper(connection);
            return wrapper.ExecuteEventLog();
        }
    }
    public sealed class AdjacentWrapper
    {
        private readonly SqlConnection connection;
        public AdjacentWrapper(string value) { connection = new SqlConnection(value); }
        private DataSet Run(SqlCommand command)
        {
            var result = new DataSet();
            try { var adapter = new SqlDataAdapter(command); adapter.Fill(result); }
            finally { connection.Close(); }
            return result;
        }
        public DataSet Execute()
        {
            if (connection.State != ConnectionState.Open)
                throw new InvalidOperationException("public-closed");
            var command = new SqlCommand("public." + "adjacent", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.CommandTimeout = 30;
            command.Transaction = null;
            var sourced = new SqlParameter("@public", SqlDbType.Int, 4, "public_source"); sourced.Direction = ParameterDirection.Input;
            command.Parameters.Add(sourced);
            var count = 4;
            var names = new ArrayList();
            names.Add(new AdjacentParameter("@public", SqlDbType.VarChar, 64));
            names.Clear();
            names.Add(new AdjacentParameter("@public", SqlDbType.VarChar, count));
            foreach (AdjacentParameter item in names)
                command.Parameters.Add(new SqlParameter(item.Name, item.Type, item.Size));
            return Run(command);
        }
        public DataSet ExecuteTypedArray()
        {
            if (connection.State != ConnectionState.Open)
                throw new InvalidOperationException("public-closed");
            var command = new SqlCommand("public.typed_array", connection);
            command.CommandType = CommandType.StoredProcedure;
            var parameters = new ArrayList();
            parameters.Add(new AdjacentParameter("@public", SqlDbType.Int, 4));
            parameters.Add(new AdjacentParameter("@public", SqlDbType.VarChar, 64));
            AdjacentParameter[] copied = (AdjacentParameter[])parameters.ToArray(typeof(AdjacentParameter));
            foreach (AdjacentParameter item in copied)
                command.Parameters.Add(new SqlParameter(item.Name, item.Type, item.Size));
            var adapter = new SqlDataAdapter(command);
            adapter.TableMappings.Add("Table", "ResultSet");
            return Run(command);
        }
        public DataSet ExecuteEventLog()
        {
            // Static event-log terminal beside the web-trace chain, root body only.
            WritePublicEntry("public-adjacent-started");
            if (connection.State != ConnectionState.Open)
                throw new InvalidOperationException("public-closed");
            var command = new SqlCommand("public.event_log", connection);
            command.CommandType = CommandType.StoredProcedure;
            var parameters = new ArrayList();
            parameters.Add(new AdjacentParameter("@public", SqlDbType.Int, 4));
            foreach (AdjacentParameter item in parameters)
                command.Parameters.Add(new SqlParameter(item.Name, item.Type, item.Size));
            var result = Run(command);
            WritePublicEntry("public-adjacent-finished");
            return result;
        }
        private static void WritePublicEntry(string message)
        { System.Diagnostics.EventLog.WriteEntry("PublicSource", message); }
    }
    // The configuration chain belongs to the ROOT type's own constructor, the exact
    // shape --il-opaque-root-initializers covers; the root method lives here too.
    public sealed class AdjacentConfigRoot
    {
        private readonly string configured;
        public AdjacentConfigRoot()
        { configured = ConfigurationManager.ConnectionStrings["public-sample"].ConnectionString; }
        public DataSet AdjacentConfigProduce()
        {
            var connection = new SqlConnection(configured);
            var command = new SqlCommand("public.configured", connection);
            command.CommandType = CommandType.StoredProcedure;
            var parameters = new ArrayList();
            parameters.Add(new AdjacentParameter("@public", SqlDbType.Int, 4));
            foreach (AdjacentParameter item in parameters)
                command.Parameters.Add(new SqlParameter(item.Name, item.Type, item.Size));
            var result = new DataSet();
            try { var adapter = new SqlDataAdapter(command); adapter.Fill(result); }
            finally { connection.Close(); }
            return result;
        }
    }
    public sealed class AdjacentParameter
    {
        public readonly string Name;
        public readonly SqlDbType Type;
        public readonly int Size;
        public AdjacentParameter(string name, SqlDbType type, int size)
        { Name = name; Type = type; Size = size; }
    }
}
