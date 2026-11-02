using System;
using System.Data;
using System.Collections;

namespace PublicSql.CSharp
{
    // Compile-only public examples. No environment/provider methods are invoked by builds.
    public sealed class OpaqueInitializerRoot
    {
        private string seed = Environment.GetEnvironmentVariable("PUBLIC_SYNTHETIC_SEED").Trim().ToUpperInvariant();
        private string unused = Environment.GetEnvironmentVariable("PUBLIC_SYNTHETIC_UNUSED").Trim();
        public DataSet OpaqueProduce() { return OpaqueForward(seed, unused); }
        public object OpaqueRead() { return OpaqueProduce().Tables[0].Rows[0]["Name"]; }
        private static DataSet OpaqueForward(string value, string ignored) { return EntryProtocol.ProduceInputs(1, 2, value, true); }
        public DataSet OpaqueCommand() { return Wrapper.ArrayInputs(seed, new ArrayList()); }
        public DataSet OpaqueControl() { if (seed == null) return new DataSet(); return EntryProtocol.ProduceInputs(1, 2, "three", true); }
        public DataSet OpaqueMutation() { seed = "changed"; return EntryProtocol.ProduceInputs(1, 2, seed, true); }
    }
    public sealed class OpaqueInitializerEscape
    {
        private string seed;
        public OpaqueInitializerEscape() { seed = Convert.ToString(this); }
        public DataSet OpaqueEscapeProduce() { return EntryProtocol.ProduceInputs(1, 2, seed, true); }
    }
    public sealed class OpaqueInitializerReference
    {
        private object seed = Environment.GetEnvironmentVariables();
        public DataSet OpaqueReferenceProduce() { return EntryProtocol.ProduceInputs(1, 2, Convert.ToString(seed), true); }
    }
    public sealed class OpaqueInitializerCatch
    {
        private string seed;
        public OpaqueInitializerCatch() { try { seed = Environment.GetEnvironmentVariable("PUBLIC_SYNTHETIC_CATCH"); } catch { seed = "fallback"; } }
        public DataSet OpaqueCatchProduce() { return EntryProtocol.ProduceInputs(1, 2, seed, true); }
    }
    public sealed class OpaqueInitializerLongChain
    {
        private string seed = Environment.GetEnvironmentVariable("PUBLIC_SYNTHETIC_LONG").Trim().Trim().Trim().Trim().Trim().Trim().Trim().Trim();
        public DataSet OpaqueLongProduce() { return EntryProtocol.ProduceInputs(1, 2, seed, true); }
    }
}
