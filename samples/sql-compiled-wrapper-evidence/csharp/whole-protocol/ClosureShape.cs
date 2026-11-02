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
    public struct ClosureStructNullGuardParameter
    {
        // v18-001 stop shape: the struct ctor null-guards an INPUT-supplied value and
        // substitutes DBNull.Value — the classic .NET 1 pattern.
        public SqlParameter Param { get; }
        public ClosureStructNullGuardParameter(string name, SqlDbType type, int size, object value)
        {
            if (value == null) value = System.DBNull.Value;
            Param = new SqlParameter(name, type, size);
            Param.Value = value;
        }
        public ClosureStructNullGuardParameter(string name, SqlDbType type, ParameterDirection direction, int size, object value)
        {
            // The direction overload: the parameter direction passes through from
            // the caller instead of defaulting to Input.
            if (value == null) value = System.DBNull.Value;
            if (value.GetType().ToString() == "System.String")
            {
                if (value.ToString() == "") value = System.DBNull.Value;
            }
            // bne.un pre-admission: the inverted comparisons (ECMA-335 has no
            // ceq.un; C# lowers != on value-type results to bne.un).
            if (size != 0) { var probe = size; }
            Param = new SqlParameter(name, type, size);
            Param.Value = value;
            Param.Direction = direction;
        }
    }
    public static class ClosureStructNullGuardExec
    {
        public static DataSet Execute(string name, ref ArrayList parameters)
        {
            var stored = new SqlConnection("public-struct-null-guard");
            var command = new SqlCommand(name, stored);
            command.CommandType = CommandType.StoredProcedure;
            foreach (ClosureStructNullGuardParameter item in parameters)
            {
                var ignored = item.GetType().ToString(); // v20/v21: GetType then Type.ToString — untracked then unbound
                command.Parameters.Add(item.Param);
            }
            var result = new DataSet();
            try { var adapter = new SqlDataAdapter(command); adapter.Fill(result); }
            finally { stored.Close(); }
            return result;
        }
    }
    public static class ClosureNullFoldExec
    {
        // v40-001: the null-init forward — C# lowers the guard as ceq (Equal folds
        // null against null already); the VB twin lowers cgt.un and pins the
        // non-null-test fold with both operands proven null.
        public static DataSet Forward(string name, bool convert, ref ArrayList list, ref string[] outputNames)
        {
            // The fork source is the Boolean input: on the false-assumed path the
            // local stays null-origin and is forwarded ByRef exactly as the private
            // walk forwards its never-assigned typed array.
            ClosureStructNullGuardParameter[] converted;
            if (convert)
                converted = (ClosureStructNullGuardParameter[])list.ToArray(typeof(ClosureStructNullGuardParameter));
            else
                converted = null;
            return Run(name, ref converted, ref outputNames);
        }
        public static DataSet Run(string name, ref ClosureStructNullGuardParameter[] converted, ref string[] outputNames)
        {
            var stored = new SqlConnection("public-null-fold");
            if (stored == null) return new DataSet();
            if (converted == null)
            {
                // The null-path copy-back: the store writes through the forwarded address.
                converted = new ClosureStructNullGuardParameter[] { };
            }
            var command = new SqlCommand(name, stored);
            command.CommandType = CommandType.StoredProcedure;
            foreach (var item in converted)
                command.Parameters.Add(item.Param);
            var result = new DataSet();
            try { var adapter = new SqlDataAdapter(command); adapter.Fill(result); }
            finally { stored.Close(); }
            return result;
        }
    }
    public static class ClosureStructExec
    {
        // The confirmed private topology: the ExecProc helper receives the ArrayList
        // BY REFERENCE and itself enumerates it, attaching each struct's wrapped
        // SqlParameter to the command.
        public static DataSet Execute(string name, ref ArrayList parameters)
        {
            var stored = new SqlConnection("public-struct-exec");
            var command = new SqlCommand(name, stored);
            command.CommandType = CommandType.StoredProcedure;
            foreach (ClosureStructPropertyParameter item in parameters)
                command.Parameters.Add(item.Param);
            var result = new DataSet();
            try { var adapter = new SqlDataAdapter(command); adapter.Fill(result); }
            finally { stored.Close(); }
            return result;
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
    public struct ClosureStructPropertyParameter
    {
        // The v17 private shape: the struct wraps a SqlParameter PROPERTY and the
        // struct itself rides the collection; consumers attach item.Param directly.
        public SqlParameter Param { get; }
        public ClosureStructPropertyParameter(string name, SqlDbType type, int size, object value)
        { Param = new SqlParameter(name, type, size); Param.Value = value; }
    }
    public struct ClosureStructParameter
    {
        public readonly string Name;
        public readonly SqlDbType Type;
        public readonly int Size;
        public readonly object Value;
        public ClosureStructParameter(string name, SqlDbType type, int size, object value)
        { Name = name; Type = type; Size = size; Value = value; }
    }
    public static class ClosureByRef
    {
        public static void Execute(string name, ref ArrayList parameters, ref string[] names)
        {
            parameters.Add(new ClosureParameter("@public", SqlDbType.Int, 4));
            parameters.Add(new ClosureParameter("@public2", SqlDbType.VarChar, 64));
            names[0] = "public.byref_result";
            names[1] = "public.byref_tables";
        }
    }
    public sealed class ClosureRoot
    {
        private readonly string shaped;
        public ClosureRoot() { shaped = ClosureText.Shape("public-connection-literal"); }
        // ByRef shape from the v14-001 outcome-B finding: locals passed by reference
        // (ArrayList& and String[]&) into the execute helper, which mutates the list
        // and fills the names array through the aliases.
        public DataSet ClosureByRefProduce(string connection)
        {
            var stored = new SqlConnection(connection);
            var command = new SqlCommand("public.byref", stored);
            command.CommandType = CommandType.StoredProcedure;
            var parameters = new ArrayList();
            string[] names = new string[2];
            ClosureByRef.Execute("public.byref", ref parameters, ref names);
            foreach (ClosureParameter item in parameters)
                command.Parameters.Add(new SqlParameter(item.Name, item.Type, item.Size));
            var result = new DataSet();
            try { var adapter = new SqlDataAdapter(command); adapter.TableMappings.Add("Table", names[0]); adapter.Fill(result); }
            finally { stored.Close(); }
            return result;
        }
        public DataSet ClosureByRefFrameworkProduce(string connection)
        {
            // Negative control: a framework ByRef API (out parameter) never receives a
            // local address; only tracked inlined callees may take references.
            var stored = new SqlConnection(connection);
            var command = new SqlCommand("public.byref_framework", stored);
            command.CommandType = CommandType.StoredProcedure;
            var size = 0;
            int.TryParse("4", out size);
            command.Parameters.Add(new SqlParameter("@public", SqlDbType.VarChar, size));
            var result = new DataSet();
            new SqlDataAdapter(command).Fill(result);
            return result;
        }
        public DataSet ClosureRawParameterProduce(string connection)
        {
            // v15-001 outcome B2 shape: constructor-proven SqlParameter allocations
            // added directly to the lifecycle ArrayList (no custom wrapper objects).
            var stored = new SqlConnection(connection);
            var command = new SqlCommand("public.raw_parameters", stored);
            command.CommandType = CommandType.StoredProcedure;
            var parameters = new ArrayList();
            parameters.Add(new SqlParameter("@public", SqlDbType.Int, 4));
            parameters.Add(new SqlParameter("@public2", SqlDbType.VarChar, 64));
            foreach (SqlParameter item in parameters)
                command.Parameters.Add(item);
            var result = new DataSet();
            try { var adapter = new SqlDataAdapter(command); adapter.Fill(result); }
            finally { stored.Close(); }
            return result;
        }
        public DataSet ClosureNameListProduce(string connection)
        {
            // Negative control: string items in the lifecycle collection are not the
            // parameter protocol and stay refused.
            var stored = new SqlConnection(connection);
            var command = new SqlCommand("public.name_list", stored);
            command.CommandType = CommandType.StoredProcedure;
            var names = new ArrayList();
            names.Add("public-name");
            foreach (string name in names)
                command.Parameters.Add(new SqlParameter(name, SqlDbType.VarChar, 64));
            var result = new DataSet();
            try { var adapter = new SqlDataAdapter(command); adapter.Fill(result); }
            finally { stored.Close(); }
            return result;
        }
        public DataSet ClosureCrossParameterProduce(string connection)
        {
            // v15 follow-up shape: the custom parameter type lives in a SEPARATE
            // assembly (the wrapper role), so the constructor call is a cross-assembly
            // memberref — the existing four-field wrapper type mirrors the reported
            // (String, SqlDbType, Int32, Object) signature without growing the shared
            // scan input budget.
            var stored = new SqlConnection(connection);
            var command = new SqlCommand("public.cross_parameter", stored);
            command.CommandType = CommandType.StoredProcedure;
            var parameters = new ArrayList();
            parameters.Add(new InputParameter("@public", SqlDbType.Int, 4, "public-value"));
            parameters.Add(new InputParameter("@public2", SqlDbType.VarChar, 64, "public-second"));
            foreach (InputParameter item in parameters)
            {
                var parameter = new SqlParameter(item.Name, item.Type, item.Size);
                parameter.Value = item.Value;
                command.Parameters.Add(parameter);
            }
            var result = new DataSet();
            try { var adapter = new SqlDataAdapter(command); adapter.Fill(result); }
            finally { stored.Close(); }
            return result;
        }
        public DataSet ClosureNullItemProduce(string connection)
        {
            // Declared boundary probe: a null item in the lifecycle collection; the
            // refusal message stays stable and the found kind rides as a diagnostic.
            var stored = new SqlConnection(connection);
            var command = new SqlCommand("public.null_item", stored);
            command.CommandType = CommandType.StoredProcedure;
            var parameters = new ArrayList();
            parameters.Add(null);
            foreach (ClosureParameter item in parameters)
                command.Parameters.Add(new SqlParameter(item.Name, item.Type, item.Size));
            var result = new DataSet();
            new SqlDataAdapter(command).Fill(result);
            return result;
        }
        public DataSet ClosureStructProduce(string connection)
        {
            // v17-001 diagnostic shape: a value-type parameter constructed in place —
            // compilers lower struct construction to ldloca + CALL (not newobj), and
            // the constructed value flows into the lifecycle collection.
            var stored = new SqlConnection(connection);
            var command = new SqlCommand("public.struct_parameter", stored);
            command.CommandType = CommandType.StoredProcedure;
            var parameters = new ArrayList();
            var first = new ClosureStructParameter("@public", SqlDbType.Int, 4, "public-value");
            var second = new ClosureStructParameter("@public2", SqlDbType.VarChar, 64, "public-second");
            parameters.Add(first);
            parameters.Add(second);
            foreach (ClosureStructParameter item in parameters)
            {
                var parameter = new SqlParameter(item.Name, item.Type, item.Size);
                parameter.Value = item.Value;
                command.Parameters.Add(parameter);
            }
            var result = new DataSet();
            try { var adapter = new SqlDataAdapter(command); adapter.Fill(result); }
            finally { stored.Close(); }
            return result;
        }
        public DataSet ClosureStructPropertyProduce(string connection)
        {
            // v17 consumption shape: the struct (not its SqlParameter) is added to
            // the collection; the loop attaches item.Param directly.
            var stored = new SqlConnection(connection);
            var command = new SqlCommand("public.struct_property", stored);
            command.CommandType = CommandType.StoredProcedure;
            var parameters = new ArrayList();
            var first = new ClosureStructPropertyParameter("@public", SqlDbType.Int, 4, "public-value");
            var second = new ClosureStructPropertyParameter("@public2", SqlDbType.VarChar, 64, "public-second");
            parameters.Add(first);
            parameters.Add(second);
            foreach (ClosureStructPropertyParameter item in parameters)
                command.Parameters.Add(item.Param);
            var result = new DataSet();
            try { var adapter = new SqlDataAdapter(command); adapter.Fill(result); }
            finally { stored.Close(); }
            return result;
        }
        public DataSet ClosureStructExecProduce(string connection)
        {
            // Root shape: build the struct parameters, hand the list by reference to
            // the execute helper, and return its dataset.
            var parameters = new ArrayList();
            var first = new ClosureStructPropertyParameter("@public", SqlDbType.Int, 4, "public-value");
            var second = new ClosureStructPropertyParameter("@public2", SqlDbType.VarChar, 64, "public-second");
            parameters.Add(first);
            parameters.Add(second);
            return ClosureStructExec.Execute("public.struct_exec", ref parameters);
        }
        public DataSet ClosureStructNullGuardProduce(string name, object value)
        {
            // The root's own inputs feed the guarded ctor argument; the constant-built
            // second parameter needs no fork.
            var parameters = new ArrayList();
            parameters.Add(new ClosureStructNullGuardParameter("@public", SqlDbType.Int, 4, value));
            parameters.Add(new ClosureStructNullGuardParameter("@public2", SqlDbType.VarChar, 64, "public-second"));
            return ClosureStructNullGuardExec.Execute("public.struct_null_guard", ref parameters);
        }
        public DataSet ClosurePathBudgetProduce(string connection, object first, object second)
        {
            // v19-001 stop shape: two DISTINCT guarded inputs (two forks) compound
            // with the connection state guards — completing paths exceed the old
            // 8-path bound; the raised bound keeps every path individually proven.
            var opened = new ClosureBase(connection);
            opened.Open();
            var parameters = new ArrayList();
            parameters.Add(new ClosureStructNullGuardParameter("@public", SqlDbType.Int, 4, first));
            parameters.Add(new ClosureStructNullGuardParameter("@public2", SqlDbType.VarChar, ParameterDirection.Output, 64, "public-second"));
            var result = ClosureStructNullGuardExec.Execute("public.path_budget", ref parameters);
            opened.Close();
            return result;
        }
        public DataSet ClosureGuardProduce(string connection, bool fail)
        {
            var parameters = new ArrayList();
            parameters.Add(new ClosureStructNullGuardParameter("@public", SqlDbType.VarChar, 12, connection));
            return ClosureGuardExec.Execute("public.guard", ref parameters, fail);
        }
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
    public sealed class ClosureGuardException : Exception
    {
        private readonly string CommandText1;
        private readonly string CommandText2;
        private readonly Exception StoredInner;
        public ClosureGuardException(string methodName, string commandText1, string commandText2, Exception inner)
            : base(methodName + ": " + commandText1)
        { CommandText1 = commandText1; CommandText2 = commandText2; StoredInner = inner; }
    }
    public static class ClosureGuardExec
    {
        public static DataSet Execute(string name, ref ArrayList parameters, bool fail)
        {
            var stored = new SqlConnection("public-guard-exec");
            if (stored == null) return new DataSet();
            if (parameters == null) return new DataSet(); // v33: the ByRef array guard
            if (fail)
                throw new ClosureGuardException(System.Reflection.MethodBase.GetCurrentMethod().Name,
                    name, name, new Exception(new Exception("public-inner").ToString()));
            var command = new SqlCommand(name, stored);
            command.CommandType = CommandType.StoredProcedure;
            foreach (ClosureStructNullGuardParameter item in parameters)
                command.Parameters.Add(item.Param);
            var result = new DataSet();
            try { var adapter = new SqlDataAdapter(command); adapter.Fill(result); }
            finally { stored.Close(); }
            return result;
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
        public DataSet ClosureExternalDetachProduce(string connection)
        {
            // v41-001: the walk's terminal stop — the matched unsubscribe followed
            // by a SECOND one: the handler stays tracked-shape but is no longer in
            // the connection's subscription set. The external-detach admission
            // records the second unsubscribe without claiming the external
            // subscription (out-of-window subscribe / different connection).
            System.Diagnostics.EventLog.WriteEntry("PublicClosure", ClosureText.CurrentName());
            var opened = new ClosureBase(connection);
            opened.Open();
            var command = new SqlCommand("public.external_detach", opened.Connection);
            command.CommandType = CommandType.StoredProcedure;
            command.Parameters.Add(new SqlParameter("@public", SqlDbType.VarChar, 64));
            var handler = new SqlInfoMessageEventHandler(OnClosureMessage);
            var result = new DataSet();
            try
            {
                opened.Connection.InfoMessage += handler;
                var adapter = new SqlDataAdapter(command);
                adapter.TableMappings.Add("Table", "public.external_detach_result");
                adapter.Fill(result);
            }
            finally
            {
                opened.Connection.InfoMessage -= handler;
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
        public DataSet ClosureNullFoldProduce(string connection, bool convert)
        {
            // v40-001: the null-init forward — one fork path forwards a never-assigned
            // null-origin array into the callee's null guard. The private walk forks
            // the root's own ArrayList input nullness; the fixture forks a Boolean
            // input to keep root arguments primitive.
            var names = new string[2];
            var parameters = new ArrayList();
            parameters.Add(new ClosureStructNullGuardParameter("@public", SqlDbType.VarChar, 12, connection));
            return ClosureNullFoldExec.Forward("public.null_fold", convert, ref parameters, ref names);
        }
    }
}
