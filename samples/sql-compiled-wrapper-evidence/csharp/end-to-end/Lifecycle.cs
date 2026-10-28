using System;
using System.Collections.Generic;
using System.Data;
namespace PublicSql.CSharp
{
    // Compile-only integration fixture. Never invoke these provider-backed methods.
    public sealed class ArrayLifecycle
    {
        public object ArrayParameterRead()
        {
            var parameters = new System.Collections.ArrayList();
            parameters.Add(new FieldParameter("@Name", SqlDbType.NVarChar, 40));
            parameters.Add(new FieldParameter("@Code", SqlDbType.VarChar, 10));
            return Wrapper.ArrayParameters("public.first", parameters).Tables[0].Rows[0]["Name"];
        }
        public object ArrayIncompleteRead()
        {
            var parameters = new System.Collections.ArrayList();
            parameters.Add(new FieldParameter("@Name", SqlDbType.NVarChar, 40));
            parameters.Add(new FieldParameter("@Code", SqlDbType.VarChar, 10));
            return Wrapper.ArrayIncomplete("public.first", parameters).Tables[0].Rows[0]["Name"];
        }
        public object ArrayPropertyRead()
        {
            var parameters = new System.Collections.ArrayList();
            parameters.Add(new PropertyParameter("@Name", SqlDbType.NVarChar, 40));
            return Wrapper.ArrayProperties("public.first", parameters).Tables[0].Rows[0]["Name"];
        }
        public object ArraySeparateExecutions()
        {
            var first = new System.Collections.ArrayList();
            first.Add(new FieldParameter("@Name", SqlDbType.NVarChar, 40));
            Wrapper.ArrayParameters("public.first", first);
            var second = new System.Collections.ArrayList();
            second.Add(new FieldParameter("@Code", SqlDbType.VarChar, 10));
            return Wrapper.ArrayParameters("public.second", second).Tables[0].Rows[0]["Name"];
        }
        public object ArrayUnsupportedCollection()
        {
            var parameters = new System.Collections.Queue();
            parameters.Enqueue(new FieldParameter("@Name", SqlDbType.NVarChar, 40));
            return Wrapper.Direct("public.first").Tables[0].Rows[0]["Name"];
        }
        public object ArrayUnsupportedMutation()
        {
            var parameters = new System.Collections.ArrayList();
            parameters.Add(new FieldParameter("@Name", SqlDbType.NVarChar, 40));
            parameters.RemoveAt(0);
            return Wrapper.ArrayParameters("public.first", parameters).Tables[0].Rows[0]["Name"];
        }
        public object ArrayMutatedElement()
        {
            var parameters = new System.Collections.ArrayList();
            var value = new MutableParameter(40);
            parameters.Add(value); value.Size = 80;
            return Wrapper.ArrayProperties("public.first", parameters).Tables[0].Rows[0]["Name"];
        }
        public object ArrayEscaped()
        {
            var parameters = new System.Collections.ArrayList();
            parameters.Add(new FieldParameter("@Name", SqlDbType.NVarChar, 40));
            System.GC.KeepAlive(parameters);
            return Wrapper.ArrayParameters("public.first", parameters).Tables[0].Rows[0]["Name"];
        }
        public object ArrayTooLarge()
        {
            var parameters = new System.Collections.ArrayList();
            parameters.Add(new FieldParameter("@Name", SqlDbType.NVarChar, 40));
            parameters.Add(new FieldParameter("@Name", SqlDbType.NVarChar, 40));
            parameters.Add(new FieldParameter("@Name", SqlDbType.NVarChar, 40));
            parameters.Add(new FieldParameter("@Name", SqlDbType.NVarChar, 40));
            parameters.Add(new FieldParameter("@Name", SqlDbType.NVarChar, 40));
            parameters.Add(new FieldParameter("@Name", SqlDbType.NVarChar, 40));
            parameters.Add(new FieldParameter("@Name", SqlDbType.NVarChar, 40));
            parameters.Add(new FieldParameter("@Name", SqlDbType.NVarChar, 40));
            parameters.Add(new FieldParameter("@Name", SqlDbType.NVarChar, 40));
            parameters.Add(new FieldParameter("@Name", SqlDbType.NVarChar, 40));
            parameters.Add(new FieldParameter("@Name", SqlDbType.NVarChar, 40));
            parameters.Add(new FieldParameter("@Name", SqlDbType.NVarChar, 40));
            parameters.Add(new FieldParameter("@Name", SqlDbType.NVarChar, 40));
            parameters.Add(new FieldParameter("@Name", SqlDbType.NVarChar, 40));
            parameters.Add(new FieldParameter("@Name", SqlDbType.NVarChar, 40));
            parameters.Add(new FieldParameter("@Name", SqlDbType.NVarChar, 40));
            parameters.Add(new FieldParameter("@Name", SqlDbType.NVarChar, 40));
            return Wrapper.ArrayParameters("public.first", parameters).Tables[0].Rows[0]["Name"];
        }
        public object ArrayInvalidAttachment()
        {
            var value = new System.Data.SqlClient.SqlParameter("@Name", SqlDbType.NVarChar, 40);
            Wrapper.Forwarded("public.first", value);
            return Wrapper.Forwarded("public.second", value).Tables[0].Rows[0]["Name"];
        }
        public object ArrayWrongElement()
        {
            var parameters = new System.Collections.ArrayList();
            parameters.Add("not a parameter");
            return Wrapper.ArrayParameters("public.first", parameters).Tables[0].Rows[0]["Name"];
        }
        public object ArrayWrongCustomType()
        {
            var parameters = new System.Collections.ArrayList();
            parameters.Add(new PropertyParameter("@Name", SqlDbType.NVarChar, 40));
            return Wrapper.ArrayParameters("public.first", parameters).Tables[0].Rows[0]["Name"];
        }
        public object ArrayRepeatedEnumeration()
        {
            var parameters = new System.Collections.ArrayList();
            parameters.Add(new FieldParameter("@Name", SqlDbType.NVarChar, 40));
            Wrapper.ArrayParameters("public.first", parameters);
            return Wrapper.ArrayParameters("public.second", parameters).Tables[0].Rows[0]["Name"];
        }
    }
    public sealed class Lifecycle
    {
        private DataSet stored;
        public DataSet State { get { return stored; } set { stored = value; } }
        private DataTable auxiliary;
        public readonly List<Projection> Items = new List<Projection>();
        public bool Failed;
        public sealed class Projection { public object Name; }

        private void BuildAuxiliary()
        {
            auxiliary = new DataTable("Overlay");
            auxiliary.Columns.Add("Score", typeof(int));
            auxiliary.Rows.Add(7);
        }
        private void Overlay(bool enabled)
        {
            if (enabled)
                foreach (DataRow row in State.Tables[0].Rows)
                    row["Score"] = auxiliary.Rows[0]["Score"];
        }
        public void Load(bool enabled)
        {
            try
            {
                Items.Clear();
                State = new DataSet();
                State.Tables.Add("First"); State.Tables.Add("Second");
                State.Tables[0].Merge(Wrapper.Direct("public.first").Tables[0]);
                State.Tables[1].Merge(Wrapper.Direct("public.second").Tables[0]);
                BuildAuxiliary(); Overlay(enabled);
                foreach (DataRow row in State.Tables[0].Rows)
                    Items.Add(new Projection { Name = row["Name"] });
            }
            catch (Exception) { Failed = true; }
        }
        public object First(bool enabled) { Load(enabled); return State.Tables[0].Rows[0]["Name"]; }
        public object Second(bool enabled) { Load(enabled); return State.Tables[1].Rows[0]["Count"]; }
        public object Score(bool enabled) { Load(enabled); return State.Tables[0].Rows[0]["Score"]; }
        public object Typed(bool enabled) { Load(enabled); return Items[0].Name; }
        public object Missing() { Load(false); return State.Tables[0].Rows[0]["Absent"]; }
        public object Refresh()
        {
            Load(true);
            State.Tables[0].Clear();
            State.Tables[0].Merge(Wrapper.Direct("public.first").Tables[0]);
            return State.Tables[0].Rows[0]["Score"];
        }
        public object Reload() { Load(true); Load(false); return State.Tables[0].Rows[0]["Score"]; }
        public object DifferentRefresh()
        {
            Load(false); State.Tables[0].Clear();
            State.Tables[0].Merge(Wrapper.Direct("public.second").Tables[0]);
            return State.Tables[0].Rows[0]["Score"];
        }
        public object ClearedRead() { Load(false); State.Tables[0].Clear(); return State.Tables[0].Rows[0]["Name"]; }
        public object DuplicateTable() { Load(false); State.Tables.Add("First"); return State.Tables[0].Rows[0]["Name"]; }
        public object EmptyLoop() { Load(false); State.Tables[0].Clear(); Overlay(true); return State.Tables[1].Rows[0]["Count"]; }
        public object Escaped() { Load(false); GC.KeepAlive(State); return State.Tables[0].Rows[0]["Name"]; }
        public object CrossRow()
        {
            Load(false);
            foreach (DataRow row in State.Tables[0].Rows)
                row["Score"] = State.Tables[0].Rows[0]["Score"];
            return State.Tables[0].Rows[0]["Score"];
        }
        public object LoopCarried()
        {
            Load(false);
            foreach (DataRow row in State.Tables[0].Rows) Failed = true;
            return State.Tables[0].Rows[0]["Name"];
        }
    }
}
