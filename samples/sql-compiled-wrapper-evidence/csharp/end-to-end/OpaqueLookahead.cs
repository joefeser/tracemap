using System.Configuration;
using System.Data;
namespace PublicSql.CSharp
{
    // Compile-only metadata: no configuration lookup is executed by builds.
    public sealed class ExternalConfigurationRoot
    {
        private string seed = ConfigurationManager.ConnectionStrings["PublicSyntheticEntry"].ConnectionString.Trim();
        public DataSet ExternalConfigurationProduce() { return EntryProtocol.ProduceInputs(1, 2, seed, true); }
    }
    public sealed class CompiledOnlyConfigurationRoot
    {
        private string seed = Wrapper.PublicScalarEcho("public-known");
        public DataSet CompiledOnlyConfigurationProduce() { return EntryProtocol.ProduceInputs(1, 2, seed, true); }
    }
    public sealed class CompiledConfigurationRoot
    {
        private string seed = Wrapper.PublicScalarEcho(ConfigurationManager.ConnectionStrings["PublicSyntheticEntry"].ConnectionString.Trim());
        public DataSet CompiledConfigurationProduce() { return EntryProtocol.ProduceInputs(1, 2, seed, true); }
    }
}
