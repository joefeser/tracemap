using System.Configuration;
using System.Data;
using System.Web;
namespace PublicSql.CSharp
{
    // Public compile-only metadata. Builds never invoke configuration or helpers.
    public sealed class CompiledScalarRoot
    {
        private ReferenceOwnerDerived holder = new ReferenceOwnerDerived();
        private string seed = Wrapper.PublicScalarTransform(ConfigurationManager.ConnectionStrings["PublicSyntheticScalar"].ConnectionString);
        public DataSet CompiledScalarTraceProduce()
        {
            HttpContext.Current.Trace.Write(seed);
            return Forward(seed, holder.BaseMarker);
        }
        private static DataSet Forward(string value, string ignored) { return EntryProtocol.ProduceInputs(1, 2, value, true); }
        public DataSet CompiledScalarCommand() { return Wrapper.ArrayInputs(seed, new System.Collections.ArrayList()); }
        public DataSet CompiledScalarControl()
        { if (seed == null) return new DataSet(); return EntryProtocol.ProduceInputs(1, 2, seed, true); }
    }
    public sealed class CompiledScalarWriteRoot
    {
        private string seed = Wrapper.PublicScalarWrites(ConfigurationManager.ConnectionStrings["PublicSyntheticScalar"].ConnectionString);
        public DataSet CompiledScalarWriteProduce() { return EntryProtocol.ProduceInputs(1, 2, seed, true); }
    }
    public sealed class CompiledScalarThrowRoot
    {
        private string seed = Wrapper.PublicScalarThrows(ConfigurationManager.ConnectionStrings["PublicSyntheticScalar"].ConnectionString);
        public DataSet CompiledScalarThrowProduce() { return EntryProtocol.ProduceInputs(1, 2, seed, true); }
    }
}
