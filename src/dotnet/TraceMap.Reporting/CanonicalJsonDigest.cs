using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TraceMap.Reporting;

/// <summary>Stable SHA-256 over recursively key-sorted compact JSON.</summary>
public static class CanonicalJsonDigest
{
    public const string Algorithm = "sha256-canonical-json-v1";

    public static string Compute(string json, params string[] blankPropertyPaths)
    {
        var node = JsonNode.Parse(json) ?? throw new JsonException("JSON root is required.");
        return Compute(node, blankPropertyPaths);
    }

    public static string Compute(JsonNode node, params string[] blankPropertyPaths)
    {
        var blank = blankPropertyPaths.ToHashSet(StringComparer.Ordinal);
        var canonical = Canonicalize(node, string.Empty, blank).ToJsonString(new JsonSerializerOptions { WriteIndented = false });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    public static JsonNode Canonicalize(JsonNode node) => Canonicalize(node, string.Empty, new HashSet<string>(StringComparer.Ordinal));

    private static JsonNode Canonicalize(JsonNode node, string path, IReadOnlySet<string> blank)
    {
        if (node is JsonObject obj)
        {
            var sorted = new JsonObject();
            foreach (var property in obj.OrderBy(property => property.Key, StringComparer.Ordinal))
            {
                var propertyPath = string.IsNullOrEmpty(path) ? property.Key : $"{path}.{property.Key}";
                sorted[property.Key] = blank.Contains(propertyPath) || blank.Contains(property.Key)
                    ? string.Empty
                    : property.Value is null ? null : Canonicalize(property.Value, propertyPath, blank);
            }

            return sorted;
        }

        if (node is JsonArray array)
        {
            return new JsonArray(array.Select(item => item is null ? null : Canonicalize(item, path, blank)).ToArray());
        }

        return node.DeepClone();
    }
}
