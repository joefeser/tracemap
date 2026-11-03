using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
namespace PublicSql.CSharp
{
    public static class JsonLinqPipelines
    {
        public static string Direct(IEnumerable<JToken> tokens)
            => string.Join(" / ", tokens.Where(token => token["kind"].Value<string>() == "public-alpha")
                .Select(token => token["caption"].Value<string>()));
        public static string LocalPipeline(JToken tokens)
        {
            var captions = tokens.Where(token => (string)token["kind"] == "public-alpha")
                .Select(token => token["caption"].Value<string>());
            return string.Join(" / ", captions);
        }
        public static string FromJson(string json)
        {
            var document = (JObject)JsonConvert.DeserializeObject(json);
            var tokens = document.SelectToken("publicEntries");
            if (tokens != null)
            {
                var captions = tokens.Where(token => token["kind"].Value<string>() == "public-alpha")
                    .Select(token => token["caption"].Value<string>());
                return string.Join(" / ", captions);
            }
            return "";
        }
        public static string DynamicKeyRefusal(IEnumerable<JToken> tokens, string key)
            => string.Join(" / ", tokens.Where(token => token["kind"].Value<string>() == "public-alpha")
                .Select(token => token[key].Value<string>()));
        public static string HelperRefusal(IEnumerable<JToken> tokens)
            => string.Join(" / ", tokens.Where(token => token["kind"].Value<string>() == "public-alpha")
                .Select(token => Build(token)));
        private static string Build(JToken token) => token["caption"].Value<string>();
        public static string ReassignmentRefusal(IEnumerable<JToken> tokens)
        {
            var captions = tokens.Where(token => token["kind"].Value<string>() == "public-alpha")
                .Select(token => token["caption"].Value<string>());
            captions = Enumerable.Empty<string>();
            return string.Join(" / ", captions);
        }
    }
}
