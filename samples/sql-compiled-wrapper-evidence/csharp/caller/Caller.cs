using System.Data;
using System.Data.SqlClient;
namespace PublicSql.CSharp
{
    // Compile-only: never invoke these methods or connect to a provider.
    public sealed class Caller
    {
        private DataSet destination;
        public object Direct() { var result = Wrapper.Direct("public.lookup"); return result.Tables[0].Rows[0]["Name"]; }
        public object Forwarded() { var result = Wrapper.Forwarded("public.lookup", new SqlParameter("@Name", SqlDbType.NVarChar, 40)); return result.Tables[0].Rows[0]["Name"]; }
        public object FieldObject() { var result = Wrapper.FieldObject("public.lookup", new FieldParameter("@Name", SqlDbType.NVarChar, 40)); return result.Tables[0].Rows[0]["Name"]; }
        public object PropertyObject() { var result = Wrapper.PropertyObject("public.lookup", new PropertyParameter("@Name", SqlDbType.NVarChar, 40)); return result.Tables[0].Rows[0]["Name"]; }
        public object Mutated() { var result = Wrapper.Mutated("public.lookup"); return result.Tables[0].Rows[0]["Name"]; }
        public object Escaped() { var result = Wrapper.Escaped("public.lookup"); return result.Tables[0].Rows[0]["Name"]; }
        public object Branch(bool condition) { var result = Wrapper.Branch("public.lookup", condition); return result.Tables[0].Rows[0]["Name"]; }
        public object MergeFresh() { destination = new DataSet(); destination.Merge(Wrapper.Direct("public.lookup")); return destination.Tables[0].Rows[0]["Name"]; }
        public object MergeExisting(DataSet existing) { destination = existing; destination.Merge(Wrapper.Direct("public.lookup")); return destination.Tables[0].Rows[0]["Name"]; }
        public object MergeMutated() { destination = new DataSet(); destination.Merge(Wrapper.Direct("public.lookup")); destination.Tables[0].Columns.Remove("Name"); return destination.Tables[0].Rows[0]["Name"]; }
    }
}
