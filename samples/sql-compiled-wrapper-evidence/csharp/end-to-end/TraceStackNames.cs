using System.Data;
using System.Diagnostics;
using System.Web;
namespace PublicSql.CSharp
{
    public sealed class TraceStackNameRoot
    {
        public DataSet TraceStackNamesProduce(string message)
        {
            string[] parts;
            HttpContext.Current.Trace.Write("public-category", string.Concat(parts = new string[] {
                new StackTrace().GetFrame(0).GetMethod().Name,
                "/",
                new StackTrace().GetFrame(1).GetMethod().Name }));
            return EntryProtocol.ProduceInputs(1, 2, message, true);
        }
        public DataSet TraceLongStackNamesProduce(string message)
        {
            HttpContext.Current.Trace.Write("public-long", string.Concat(new string[] {
                new StackTrace().GetFrame(0).GetMethod().Name,
                new StackTrace().GetFrame(1).GetMethod().Name,
                new StackTrace().GetFrame(2).GetMethod().Name,
                new StackTrace().GetFrame(3).GetMethod().Name,
                new StackTrace().GetFrame(4).GetMethod().Name,
                new StackTrace().GetFrame(5).GetMethod().Name,
                new StackTrace().GetFrame(6).GetMethod().Name,
                new StackTrace().GetFrame(7).GetMethod().Name }));
            return EntryProtocol.ProduceInputs(1, 2, message, true);
        }
        public DataSet TraceDynamicFrameRefusal(int frame)
        {
            HttpContext.Current.Trace.Write(new StackTrace().GetFrame(frame).GetMethod().Name);
            return EntryProtocol.ProduceInputs(1, 2, "public", true);
        }
        public DataSet TraceStackEscapeRefusal()
        {
            StackTrace capture;
            HttpContext.Current.Trace.Write((capture = new StackTrace()).GetFrame(0).GetMethod().Name);
            return EntryProtocol.ProduceInputs(1, 2, "public", true);
        }
        public DataSet TraceGeneralReflectionRefusal()
        {
            HttpContext.Current.Trace.Write(typeof(string).GetMethods()[0].Name);
            return EntryProtocol.ProduceInputs(1, 2, "public", true);
        }
    }
}
