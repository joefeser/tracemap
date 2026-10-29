using System.Data;
using System.Web;
namespace PublicSql.CSharp
{
    public sealed class TraceArgumentRoot
    {
        public DataSet TraceComputedProduce(string message, int count)
        {
            HttpContext.Current.Trace.Write(string.Concat("public-", message, count.ToString()));
            return EntryProtocol.ProduceInputs(1, 2, message, true);
        }
        public DataSet TraceBuiltLocalProduce(string message, int count)
        {
            var local = string.Format("public-{0}", count);
            HttpContext.Current.Trace.Write(local);
            return EntryProtocol.ProduceInputs(1, 2, local, true);
        }
        public DataSet TraceArrayProduce(string message, int count)
        {
            HttpContext.Current.Trace.Write(string.Concat(new object[] { "public-", message, count }));
            return EntryProtocol.ProduceInputs(1, 2, message, true);
        }
        public DataSet TraceObjectConcatRefusal(object value)
        {
            HttpContext.Current.Trace.Write(string.Concat("public-", value));
            return EntryProtocol.ProduceInputs(1, 2, "value", true);
        }
        public DataSet TraceUnknownBuilderRefusal(string message)
        {
            HttpContext.Current.Trace.Write(UnknownBuilder(message));
            return EntryProtocol.ProduceInputs(1, 2, message, true);
        }
        private static string UnknownBuilder(string message) { return message; }
        public DataSet TraceComputedCommandRefusal(string message)
        {
            var command = string.Concat("public-", message);
            return Wrapper.ArrayInputs(command, new System.Collections.ArrayList());
        }
    }
}
