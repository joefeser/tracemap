using System.Data;
namespace PublicSql.CSharp
{
    // Compile-only; never invoke provider-backed fixture methods.
    public sealed class TableFlow
    {
        private DataSet state;
        public DataSet State { get { return state; } set { state = value; } }
        private static DataSet FetchFirst() { return Wrapper.Direct("public.first"); }
        private static DataSet FetchSecond() { return Wrapper.Direct("public.second"); }
        public object First()
        {
            State = new DataSet();
            State.Tables.Add("First");
            State.Tables.Add("Second");
            State.Tables[0].Merge(FetchFirst().Tables[0]);
            State.Tables[1].Merge(FetchSecond().Tables[0]);
            return State.Tables[0].Rows[0]["Name"];
        }
        public object Second()
        {
            State = new DataSet();
            State.Tables.Add("First");
            State.Tables.Add("Second");
            State.Tables[0].Merge(FetchFirst().Tables[0]);
            State.Tables[1].Merge(FetchSecond().Tables[0]);
            return State.Tables[1].Rows[0]["Name"];
        }
        public object Conditional(bool change)
        {
            State = new DataSet();
            State.Tables.Add("First");
            State.Tables.Add("Second");
            State.Tables[0].Merge(FetchFirst().Tables[0]);
            if (change) State.Tables[0].Rows[0]["Name"] = "changed";
            return State.Tables[0].Rows[0]["Name"];
        }
        public object Mutated()
        {
            State = new DataSet();
            State.Tables.Add("First");
            State.Tables.Add("Second");
            State.Tables[0].Merge(FetchFirst().Tables[0]);
            State.Tables[0].Rows[0]["Name"] = "changed";
            return State.Tables[0].Rows[0]["Name"];
        }
        public object UnknownHelper()
        {
            State = new DataSet();
            State.Tables.Add("First");
            State.Tables.Add("Second");
            State.Tables[0].Merge(FetchFirst().Tables[0]);
            Observe(State);
            return State.Tables[0].Rows[0]["Name"];
        }
        public object RepeatedMerge()
        {
            State = new DataSet();
            State.Tables.Add("First");
            State.Tables.Add("Second");
            State.Tables[0].Merge(FetchFirst().Tables[0]);
            State.Tables[0].Merge(FetchSecond().Tables[0]);
            return State.Tables[0].Rows[0]["Name"];
        }
        public object ClearMerge()
        {
            State = new DataSet();
            State.Tables.Add("First");
            State.Tables.Add("Second");
            State.Tables[0].Merge(FetchFirst().Tables[0]);
            State.Tables[0].Clear(); State.Tables[0].Merge(FetchSecond().Tables[0]);
            return State.Tables[0].Rows[0]["Name"];
        }
        private static void Observe(DataSet value) { System.GC.KeepAlive(value); }
        public object DuplicateName()
        {
            State = new DataSet(); State.Tables.Add("First"); State.Tables.Add("First");
            State.Tables[0].Merge(FetchFirst().Tables[0]);
            return State.Tables[0].Rows[0]["Name"];
        }
        public object OutOfRange()
        {
            State = new DataSet(); State.Tables.Add("First");
            State.Tables[0].Merge(FetchFirst().Tables[0]);
            return State.Tables[1].Rows[0]["Name"];
        }
        public object DynamicSlot(int slot)
        {
            State = new DataSet(); State.Tables.Add("First");
            State.Tables[0].Merge(FetchFirst().Tables[0]);
            return State.Tables[slot].Rows[0]["Name"];
        }
        public object ThirdTable()
        {
            State = new DataSet(); State.Tables.Add("First"); State.Tables.Add("Second"); State.Tables.Add("Third");
            State.Tables[0].Merge(FetchFirst().Tables[0]);
            return State.Tables[0].Rows[0]["Name"];
        }
        public object Existing(DataSet value)
        {
            State = value;
            State.Tables[0].Merge(FetchFirst().Tables[0]);
            return State.Tables[0].Rows[0]["Name"];
        }
        public object Exceptional()
        {
            try { State = new DataSet(); State.Tables.Add("First"); State.Tables[0].Merge(FetchFirst().Tables[0]); }
            catch (System.Exception) { }
            return State.Tables[0].Rows[0]["Name"];
        }
    }
}
