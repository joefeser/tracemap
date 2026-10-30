using System;
using System.Collections;
using System.Data;
using System.Data.SqlClient;
using System.Reflection;
namespace PublicSql.CSharp
{
    // Independently authored synthetic closure mirroring the public v12-001 body/edge
    // table: two inner roots (a full producer and a traced producer) plus the
    // connection, adapter-build, parameter, exception, crypto-shaped, table-mapping
    // and debug-helper callee bodies. Public types, names and literals only.
    public sealed class ClosureException : Exception
    {
        public ClosureException(string message, Exception inner) : base(message, inner) { }
    }
    public sealed class ClosureParameter
    {
        private readonly string name; private readonly SqlDbType type; private readonly int size;
        public ClosureParameter(string name, SqlDbType type, int size)
        { this.name = name; this.type = type; this.size = size; }
        public string Name => name;
        public SqlDbType Type => type;
        public int Size => size;
    }
    public static class ClosureText
    {
        // Crypto-shaped compiled scalar transform: static, (String)->String, bounded
        // body with no direct writes; qualified only by the compiled-scalar boundary.
        public static string Shape(string value)
        {
            var chars = value.ToCharArray();
            var rotated = new char[chars.Length];
            for (var index = 0; index < chars.Length; index++)
                rotated[index] = chars[chars.Length - 1 - index];
            return new string(rotated);
        }
        public static string CurrentName()
        { return MethodBase.GetCurrentMethod().Name; }
        public static string FrameName()
        { return new System.Diagnostics.StackTrace().GetFrame(0).GetMethod().Name; }
        public static string Describe(Exception failure)
        { return (failure.Message + "|") + failure.InnerException.Message; }
    }
    public sealed class ClosureBase
    {
        private readonly ArrayList pool = new ArrayList();
        private readonly SqlConnection connection;
        public ClosureBase(string value) { connection = new SqlConnection(value); }
        public SqlConnection Connection => connection;
        public void Open()
        {
            if (connection.State != ConnectionState.Open)
                throw new InvalidOperationException("public-open-required");
            pool.Clear();
            pool.Add(new ClosureParameter("@public", SqlDbType.Int, 4));
            connection.Open();
        }
        public void Close()
        {
            if (connection.State != ConnectionState.Closed)
                connection.Close();
        }
    }
    public static class ClosureBuild
    {
        public static SqlCommand Prepare(SqlConnection connection, int size)
        {
            var command = new SqlCommand("public.closure", connection);
            command.CommandType = CommandType.StoredProcedure;
            command.CommandTimeout = 30;
            command.Transaction = null;
            var sourced = new SqlParameter("@public", SqlDbType.Int, 4, "public_source");
            sourced.Direction = ParameterDirection.Input;
            sourced.Value = "public-value";
            command.Parameters.Add(sourced);
            var pool = new ArrayList();
            pool.Add(new ClosureParameter("@public", SqlDbType.VarChar, 64));
            pool.Add(new ClosureParameter("@public", SqlDbType.VarChar, size));
            ClosureParameter[] copied = (ClosureParameter[])pool.ToArray(typeof(ClosureParameter));
            foreach (ClosureParameter item in copied)
                command.Parameters.Add(new SqlParameter(item.Name, item.Type, item.Size));
            return command;
        }
        public static DataSet Run(SqlCommand command)
        {
            var result = new DataSet();
            var adapter = new SqlDataAdapter(command);
            adapter.TableMappings.Add("Table", "public.result");
            adapter.Fill(result);
            return result;
        }
    }
    public sealed class ClosureRoot
    {
        private readonly string shaped;
        public ClosureRoot() { shaped = ClosureText.Shape("public-connection-literal"); }
        public DataSet ClosureProduce()
        {
            var opened = new ClosureBase(shaped);
            opened.Open();
            var command = ClosureBuild.Prepare(opened.Connection, 64);
            try { return ClosureBuild.Run(command); }
            finally { opened.Close(); }
        }
        public DataSet ClosureInputProduce(int size)
        {
            // Negative control: parameter sizes must be compiled constants; an
            // input-driven size keeps its value unresolved and refuses.
            var opened = new ClosureBase(shaped);
            opened.Open();
            var command = ClosureBuild.Prepare(opened.Connection, size);
            try { return ClosureBuild.Run(command); }
            finally { opened.Close(); }
        }
    }
    public sealed class ClosureTraceRoot
    {
        private readonly ArrayList names = new ArrayList();
        private void OnClosureMessage(object sender, SqlInfoMessageEventArgs args)
        { names.Add("public-closure-default"); }
        public DataSet ClosureTraceProduce(string connection)
        {
            System.Diagnostics.EventLog.WriteEntry("PublicClosure", ClosureText.CurrentName());
            var opened = new ClosureBase(connection);
            opened.Open();
            var command = new SqlCommand("public.traced", opened.Connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.Add(new SqlParameter("@public", SqlDbType.VarChar, 64));
            var handler = new SqlInfoMessageEventHandler(OnClosureMessage);
            var result = new DataSet();
            try
            {
                opened.Connection.InfoMessage += handler;
                var adapter = new SqlDataAdapter(command);
                adapter.TableMappings.Add("Table", "public.traced_result");
                adapter.Fill(result);
            }
            finally
            {
                opened.Connection.InfoMessage -= handler;
                opened.Close();
            }
            return result;
        }
        public DataSet ClosureNamesProduce(string connection)
        {
            var command = new SqlCommand("public.names", new SqlConnection(connection));
            command.CommandType = CommandType.StoredProcedure;
            var adapter = new SqlDataAdapter(command);
            var mappings = adapter.TableMappings;
            mappings.Add("Table", "public.names_result");
            var source = mappings[0].SourceTable;
            if (source.Length == 0) return null;
            var result = new DataSet();
            adapter.Fill(result);
            return result;
        }
        public DataSet ClosureFrameProduce(string connection)
        {
            System.Diagnostics.EventLog.WriteEntry("PublicClosure", ClosureText.FrameName());
            var command = new SqlCommand("public.frames", new SqlConnection(connection));
            command.CommandType = CommandType.StoredProcedure;
            var result = new DataSet();
            new SqlDataAdapter(command).Fill(result);
            return result;
        }
        public DataSet ClosureExceptionProduce(string connection)
        {
            var opened = new ClosureBase(connection);
            if (opened.Connection.State != ConnectionState.Open)
                throw new ClosureException("public-closure-failure", null);
            var command = new SqlCommand("public.exception", opened.Connection);
            command.CommandType = CommandType.StoredProcedure;
            var result = new DataSet();
            new SqlDataAdapter(command).Fill(result);
            return result;
        }
        public DataSet ClosureExceptionNestedProduce(string connection)
        {
            var opened = new ClosureBase(connection);
            if (opened.Connection.State != ConnectionState.Open)
                throw new ClosureException("public-closure-failure", new InvalidOperationException("public-inner"));
            var command = new SqlCommand("public.exception_nested", opened.Connection);
            command.CommandType = CommandType.StoredProcedure;
            var result = new DataSet();
            new SqlDataAdapter(command).Fill(result);
            return result;
        }
        public DataSet ClosureDescribeProduce(string connection, Exception failure)
        {
            System.Diagnostics.EventLog.WriteEntry("PublicClosure" + "-debug" + "-source", ClosureText.Describe(failure));
            var command = new SqlCommand("public.describe", new SqlConnection("public-describe"));
            command.CommandType = CommandType.StoredProcedure;
            var result = new DataSet();
            new SqlDataAdapter(command).Fill(result);
            return result;
        }
        public DataSet ClosureTypedInputProduce(string connection, Exception failure)
        {
            // Negative control: a non-primitive root argument leaves every input
            // untyped, so typed admissions downstream refuse.
            System.Diagnostics.EventLog.WriteEntry("PublicClosure", ClosureText.Describe(failure));
            var command = new SqlCommand("public.typed_input", new SqlConnection(connection));
            command.CommandType = CommandType.StoredProcedure;
            var result = new DataSet();
            new SqlDataAdapter(command).Fill(result);
            return result;
        }
    }
}
