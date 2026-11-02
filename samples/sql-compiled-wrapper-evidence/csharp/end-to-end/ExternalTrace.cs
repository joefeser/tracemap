using System.Data;
using System.Web;

namespace PublicSql.CSharp
{
    // Compile-only synthetic ASP.NET trace shapes; these methods are never executed by builds.
    public sealed class ExternalTraceRoot
    {
        public DataSet TraceProducer(string message)
        {
            HttpContext.Current.Trace.Write("public-before");
            var result = EntryProtocol.ProduceInputs(1, 2, message, true);
            HttpContext.Current.Trace.Write("public-category", message);
            return result;
        }
        public DataSet TraceLocalString(string message)
        {
            var copy = message;
            HttpContext.Current.Trace.Write(copy);
            return EntryProtocol.ProduceInputs(1, 2, message, true);
        }
        public DataSet TraceSeparateExecutions()
        {
            HttpContext.Current.Trace.Write("first");
            EntryProtocol.ProduceInputs(1, 2, "first", true);
            HttpContext.Current.Trace.Write("second");
            return EntryProtocol.ProduceInputs(3, 4, "second", false);
        }
        public DataSet TraceConditional(bool enabled)
        {
            if (enabled) return EntryProtocol.ProduceInputs(1, 2, "value", true);
            HttpContext.Current.Trace.Write("conditional");
            return EntryProtocol.ProduceInputs(1, 2, "value", true);
        }
        public DataSet TraceGuarded()
        {
            if (HttpContext.Current != null) HttpContext.Current.Trace.Write("guarded");
            return EntryProtocol.ProduceInputs(1, 2, "value", true);
        }
        public DataSet TraceEscape()
        {
            var context = HttpContext.Current;
            System.GC.KeepAlive(context);
            return EntryProtocol.ProduceInputs(1, 2, "value", true);
        }
        public DataSet TraceWrongOverload()
        {
            HttpContext.Current.Trace.Write("category", "message", new System.Exception());
            return EntryProtocol.ProduceInputs(1, 2, "value", true);
        }
        public DataSet TraceTrackedArgument(object value)
        {
            HttpContext.Current.Trace.Write(value.ToString());
            return EntryProtocol.ProduceInputs(1, 2, "value", true);
        }
        public DataSet TraceTooMany()
        {
            HttpContext.Current.Trace.Write("public-limit");
            HttpContext.Current.Trace.Write("public-limit");
            HttpContext.Current.Trace.Write("public-limit");
            HttpContext.Current.Trace.Write("public-limit");
            HttpContext.Current.Trace.Write("public-limit");
            HttpContext.Current.Trace.Write("public-limit");
            HttpContext.Current.Trace.Write("public-limit");
            HttpContext.Current.Trace.Write("public-limit");
            HttpContext.Current.Trace.Write("public-limit");
            return EntryProtocol.ProduceInputs(1, 2, "value", true);
        }
        public object TraceRead() { return TraceProducer("value").Tables[0].Rows[0]["Name"]; }
    }
    public sealed class OpaqueExternalTraceRoot
    {
        private string seed = System.Environment.GetEnvironmentVariable("PUBLIC_SYNTHETIC_TRACE").Trim();
        public DataSet OpaqueTraceProducer()
        {
            HttpContext.Current.Trace.Write(seed);
            return EntryProtocol.ProduceInputs(1, 2, seed, true);
        }
    }
}
