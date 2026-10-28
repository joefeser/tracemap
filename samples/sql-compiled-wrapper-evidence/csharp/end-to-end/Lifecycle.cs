using System;
using System.Collections.Generic;
using System.Data;
namespace PublicSql.CSharp
{
    // Compile-only integration fixture. Never invoke these provider-backed methods.
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
