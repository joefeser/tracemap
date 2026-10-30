using System;
using System.Collections;
using System.Data;
using System.Data.SqlClient;
using System.Xml;
namespace PublicSql.CSharp
{
    // Independently authored public fixture; no connection is opened by the tests.
    public sealed class EventNamingRoot
    {
        public DataSet EventNamingProduce(string connection)
        {
            var wrapper = new EventNamingWrapper(connection); var alias = wrapper;
            int first = 1, second = 1;
            bool missing = (object)wrapper == null;
            bool aliases = (object)alias == (object)wrapper;
            bool integers = first == second;
            if (missing) return null;
            if (!aliases) return null;
            if (!integers) return null;
            wrapper.Open(); return wrapper.Execute();
        }
        public DataSet EventNamingRuntimeStateGuard(string value)
        {
            var connection = new SqlConnection(value);
            bool open = connection.State == ConnectionState.Open;
            if (open) return EventNamingProduce(value);
            return null;
        }
        public DataSet EventNamingRuntimeDbStateGuard(string value)
        {
            System.Data.Common.DbConnection connection = new SqlConnection(value);
            bool open = connection.State == ConnectionState.Open;
            if (open) return EventNamingProduce(value);
            return null;
        }
        public DataSet EventNamingRuntimeStateRead(string value)
        {
            var connection = new SqlConnection(value);
            var ignored = connection.State;
            return EventNamingProduce(value);
        }
        public DataSet EventNamingUnknownEquality(string connection)
        { bool missing = (object)connection == null; if (missing) return null; return EventNamingProduce(connection); }
        public DataSet EventNamingViewEquality(string connection)
        {
            var data = new DataSet(); var left = data.Tables; var right = data.Tables;
            bool equal = (object)left == (object)right;
            if (equal) return EventNamingProduce(connection);
            return null;
        }
        public DataSet EventNamingDistinctEquality(string connection)
        {
            var wrapper = new EventNamingWrapper(connection); var other = new EventNamingWrapper(connection);
            bool equal = (object)wrapper == (object)other;
            if (equal) return null;
            wrapper.Open(); return wrapper.Execute();
        }
        public DataSet EventNamingReused(string connection)
        { var wrapper = new EventNamingWrapper(connection); wrapper.Open(); wrapper.Execute(); wrapper.Open(); return wrapper.Execute(); }
        public DataSet EventNameListMutation(string connection)
        { var wrapper = new EventNamingWrapper(connection); wrapper.Open(); wrapper.ReplaceNames(); return wrapper.Execute(); }
        public DataSet EventNameListEscape(string connection)
        { var wrapper = new EventNamingWrapper(connection); return wrapper.EscapeNames(); }
        public DataSet EventNamingMismatch(string connection)
        { var wrapper = new EventNamingWrapper(connection); wrapper.Open(); return wrapper.ExecuteOtherConnection(); }
        public DataSet EventNamingSeparate(string connection)
        { EventNamingProduce(connection); return EventNamingProduce(connection); }
    }
    public sealed class EventNamingWrapper
    {
        private ArrayList names = new ArrayList();
        private readonly SqlConnection connection;
        private readonly SqlInfoMessageEventHandler handler;
        public EventNamingWrapper(string value)
        { connection = new SqlConnection(value); handler = new SqlInfoMessageEventHandler(OnMessage); }
        private void OnMessage(object sender, SqlInfoMessageEventArgs args)
        {
            try
            {
                if (args.Message.Substring(0, 12) == "public-meta:")
                {
                    var document = new XmlDocument();
                    document.LoadXml(args.Message.Substring(12));
                    var primary = document.SelectSingleNode("/public/name");
                    if (primary != null)
                    {
                        names.Add(primary.Attributes["label"].Value);
                        XmlNode ordinalAttribute = primary.Attributes[0];
                        names.Add(ordinalAttribute.InnerXml);
                    }
                    else
                    {
                        foreach (XmlNode node in document.SelectNodes("/public/names/name"))
                            names.Add(node.Attributes["label"].Value);
                    }
                }
                else names.Add("public-default");
            }
            catch (Exception) { }

        }
        public void Open() { connection.InfoMessage += handler; connection.Open(); }
        public DataSet Execute()
        {
            var command = new SqlCommand("public.event_result", connection); command.CommandType = CommandType.StoredProcedure;
            var parameters = new ArrayList(); parameters.Add(new FieldParameter("@public", SqlDbType.Int, 4));
            foreach (FieldParameter value in parameters)
                command.Parameters.Add(new SqlParameter(value.Name, value.Type, value.Size));
            var result = new DataSet(); var adapter = new SqlDataAdapter(command); adapter.Fill(result);
            Rename(result); connection.InfoMessage -= handler; connection.Close(); return result;
        }
        public void ReplaceNames() { names = new ArrayList(); }
        public DataSet EscapeNames()
        { var parameter = new SqlParameter("@public", SqlDbType.Variant, 4); parameter.Value = names; return new DataSet(); }
        public DataSet ExecuteOtherConnection()
        {
            var command = new SqlCommand("public.other", new SqlConnection("public-other")); command.CommandType = CommandType.StoredProcedure;
            var result = new DataSet(); new SqlDataAdapter(command).Fill(result); Rename(result); return result;
        }
        private void Rename(DataSet result)
        {
            for (var index = 0; index < names.Count; index++)
                if (index == 0) result.DataSetName = names[index].ToString();
                else result.Tables[index - 1].TableName = names[index].ToString();
        }
    }
    public sealed class EventUnsafeRoot
    {
        private readonly ArrayList names = new ArrayList();
        private readonly SqlCommand captured = new SqlCommand("public.unsafe");
        private void Unsafe(object sender, SqlInfoMessageEventArgs args)
        { captured.CommandText = args.Message; names.Add(args.Message); }
        public DataSet EventUnsafeProduce()
        { var connection = new SqlConnection("public"); connection.InfoMessage += new SqlInfoMessageEventHandler(Unsafe); return EntryProtocol.ProduceInputs(1, 2, "public", true); }
        private void SavedReceiver(object sender, SqlInfoMessageEventArgs args)
        {
            var saved = this;
            try { names.Add(args.Message); }
            catch (Exception) { names.Add(saved.ToString()); }
        }
        public DataSet EventReceiverCatchRefusal()
        { var connection = new SqlConnection("public"); connection.InfoMessage += new SqlInfoMessageEventHandler(SavedReceiver); return EntryProtocol.ProduceInputs(1, 2, "public", true); }
        private void Sender(object sender, SqlInfoMessageEventArgs args) { names.Add(sender.ToString()); }
        public DataSet EventSenderRefusal()
        { var connection = new SqlConnection("public"); connection.InfoMessage += new SqlInfoMessageEventHandler(Sender); return EntryProtocol.ProduceInputs(1, 2, "public", true); }
    }
}
