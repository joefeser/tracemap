using System.Data;
using System.Collections;
using System.Web;
namespace PublicSql.CSharp
{
    public class ReferenceOwnerBase
    {
        private string marker;
        public ReferenceOwnerBase() { marker = "public-base"; }
        public string BaseMarker { get { return marker; } }
    }
    public sealed class ReferenceOwnerDerived : ReferenceOwnerBase
    {
        private string marker;
        public ReferenceOwnerDerived() { marker = "public-derived"; }
        public string DerivedMarker { get { return marker; } }
    }
    public sealed class ReferenceOwnerRoot
    {
        private ReferenceOwnerDerived holder = new ReferenceOwnerDerived();
        private string seed = System.Environment.GetEnvironmentVariable("PUBLIC_SYNTHETIC_OWNER").Trim();
        public DataSet ReferenceCtorProduce() { return Forward(seed, holder.BaseMarker, holder.DerivedMarker); }
        private static DataSet Forward(string value, string first, string second) { return EntryProtocol.ProduceInputs(1, 2, value, true); }
        public DataSet ReferenceCtorTraceProduce()
        {
            HttpContext.Current.Trace.Write(seed);
            return Forward(seed, holder.BaseMarker, holder.DerivedMarker);
        }
        public DataSet ReferenceOwnerExecutions()
        {
            Wrapper.ArrayInputs(holder.BaseMarker, new ArrayList());
            return Wrapper.ArrayInputs(holder.DerivedMarker, new ArrayList());
        }
    }
}
