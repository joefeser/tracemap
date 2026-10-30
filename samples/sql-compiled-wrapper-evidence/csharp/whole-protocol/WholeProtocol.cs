using System;
using System.Collections;
using System.Data;
using System.Data.SqlClient;
namespace PublicSql.CSharp
{
    // Independently authored whole-protocol fixture: configuration boundary, runtime
    // state guard, open/fill/close with finally, inspected custom parameter objects,
    // real SqlParameter attachment, adapter Fill and returned allocation. No connection
    // is opened by tests; the state guard keeps its runtime value unbound.
    public sealed class WholeProtocolRoot
    {
        public DataSet WholeProtocolProduce(string connection)
        { var wrapper = new WholeProtocolWrapper(connection); return wrapper.Execute(); }
        public DataSet WholeProtocolGuardedFill(string connection)
        { var wrapper = new WholeProtocolWrapper(connection); return wrapper.ExecuteGuarded(); }
        public DataSet WholeProtocolChoiceAttachment(string connection)
        { var wrapper = new WholeProtocolWrapper(connection); return wrapper.ExecuteChoice(); }
        public DataSet WholeProtocolRecheck(string connection)
        { var wrapper = new WholeProtocolWrapper(connection); return wrapper.ExecuteRecheck(); }
        public DataSet WholeProtocolGuardInLoop(string connection)
        { var wrapper = new WholeProtocolWrapper(connection); return wrapper.ExecuteGuardInLoop(); }
        public DataSet WholeProtocolStoredException(string connection)
        { var wrapper = new WholeProtocolWrapper(connection); return wrapper.ExecuteStoredException(); }
        public DataSet WholeProtocolStateComparedTwice(string connection)
        { var wrapper = new WholeProtocolWrapper(connection); return wrapper.ExecuteStateComparedTwice(); }
        public DataSet WholeProtocolManyGuards(string connection)
        { var wrapper = new WholeProtocolWrapper(connection); return wrapper.ExecuteManyGuards(); }
    }
    public sealed class WholeProtocolWrapper
    {
        private readonly SqlConnection connection;
        public WholeProtocolWrapper(string value) { connection = new SqlConnection(value); }
        private DataSet Fill(string name)
        {
            var command = new SqlCommand(name, connection); command.CommandType = CommandType.StoredProcedure;
            var parameters = new ArrayList(); parameters.Add(new WholeProtocolParameter("@public", SqlDbType.Int, 4));
            foreach (WholeProtocolParameter item in parameters)
                command.Parameters.Add(new SqlParameter(item.Name, item.Type, item.Size));
            var result = new DataSet();
            try { var adapter = new SqlDataAdapter(command); adapter.Fill(result); }
            finally { connection.Close(); }
            return result;
        }
        public DataSet Execute()
        {
            // Stored Boolean local retains the ceq shape; the throw path terminates
            // exceptionally and never merges with the fill path.
            if (connection.State != ConnectionState.Open)
                throw new InvalidOperationException("public-closed");
            return Fill("public.whole_protocol");
        }
        public DataSet ExecuteGuarded()
        {
            // Natural guard shape without a Boolean local.
            if (connection.State == ConnectionState.Open)
                return Fill("public.guarded");
            return null;
        }
        public DataSet ExecuteChoice()
        {
            // Incompatible attachments on the two state alternatives; neither may be
            // silently selected or merged into unconditional entry evidence.
            var command = new SqlCommand("public.choice", connection); command.CommandType = CommandType.StoredProcedure;
            if (connection.State == ConnectionState.Open)
                command.Parameters.Add(new SqlParameter("@public", SqlDbType.Int, 4));
            else
                command.Parameters.Add(new SqlParameter("@public", SqlDbType.VarChar, 64));
            var result = new DataSet();
            try { var adapter = new SqlDataAdapter(command); adapter.Fill(result); }
            finally { connection.Close(); }
            return result;
        }
        public DataSet ExecuteRecheck()
        {
            // Separate reads stay separate decisions; both guards must hold on the
            // fill path, and early-return alternatives keep their own identities.
            if (connection.State != ConnectionState.Open) return null;
            if (connection.State != ConnectionState.Open) return null;
            return Fill("public.recheck");
        }
        public DataSet ExecuteGuardInLoop()
        {
            // A state read inside the per-row loop composes with the symbolic pass.
            var result = Fill("public.guard_loop");
            foreach (DataRow row in result.Tables[0].Rows)
                if (connection.State != ConnectionState.Open) return null;
            return result;
        }
        public DataSet ExecuteStoredException()
        {
            // Negative control: a caught throw keeps its catch continuation unexecuted.
            try
            {
                if (connection.State != ConnectionState.Open)
                    throw new InvalidOperationException("public-closed");
                return Fill("public.stored_exception");
            }
            catch (InvalidOperationException) { return null; }
        }
        public DataSet ExecuteStateComparedTwice()
        {
            // Negative control: two unbound reads compared to each other refuse.
            if (connection.State == connection.State) return null;
            return Execute();
        }
        public DataSet ExecuteManyGuards()
        {
            // Negative control: five two-sided state decisions exceed the bounded path
            // alternatives (2^5 > 8) and must refuse explicitly instead of guessing.
            int size = 4;
            if (connection.State == ConnectionState.Open) size = 4; else size = 8;
            if (connection.State == ConnectionState.Open) size = 5; else size = 10;
            if (connection.State == ConnectionState.Open) size = 6; else size = 12;
            if (connection.State == ConnectionState.Open) size = 7; else size = 14;
            if (connection.State == ConnectionState.Open) size = 9; else size = 16;
            var command = new SqlCommand("public.many_guards", connection); command.CommandType = CommandType.StoredProcedure;
            var parameters = new ArrayList(); parameters.Add(new WholeProtocolParameter("@public", SqlDbType.Int, size));
            foreach (WholeProtocolParameter item in parameters)
                command.Parameters.Add(new SqlParameter(item.Name, item.Type, item.Size));
            var result = new DataSet();
            try { var adapter = new SqlDataAdapter(command); adapter.Fill(result); }
            finally { connection.Close(); }
            return result;
        }
    }
    public sealed class WholeProtocolParameter
    {
        private readonly string name; private readonly SqlDbType type; private readonly int size;
        public WholeProtocolParameter(string name, SqlDbType type, int size)
        { this.name = name; this.type = type; this.size = size; }
        public string Name => name;
        public SqlDbType Type => type;
        public int Size => size;
    }
}
