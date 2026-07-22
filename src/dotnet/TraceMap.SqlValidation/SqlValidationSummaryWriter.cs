using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TraceMap.SqlValidation;

public static class SqlValidationSummaryWriter
{
    public static async Task<(string Digest, string Json)> WriteAsync(
        string outputPath,
        SqlValidationPlan plan,
        IReadOnlyList<SqlValidationAssertion> assertions,
        CancellationToken cancellationToken)
    {
        var root = BuildRoot(plan, assertions);
        var digest = ComputeDigest(root);
        root["artifact"]!["digest"] = digest;
        var json = root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n";

        try
        {
            await using var stream = new FileStream(outputPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous);
            await using var writer = new StreamWriter(stream, new UTF8Encoding(false));
            await writer.WriteAsync(json.AsMemory(), cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            throw new SqlValidationHarnessException("SqlValidationOutputWriteFailed");
        }
        return (digest, json);
    }

    internal static JsonObject BuildRoot(SqlValidationPlan plan, IReadOnlyList<SqlValidationAssertion> assertions) => new()
    {
        ["schemaVersion"] = SqlValidationContract.SummarySchemaVersion,
        ["artifactId"] = plan.ArtifactId,
        ["repository"] = plan.Repository,
        ["commitSha"] = plan.CommitSha,
        ["observedAt"] = FormatTimestamp(plan.ObservedAt),
        ["expiresAt"] = FormatTimestamp(plan.ExpiresAt),
        ["targetContext"] = new JsonObject
        {
            ["engine"] = plan.TargetContext.Engine,
            ["serverRole"] = plan.TargetContext.ServerRole,
            ["databaseRole"] = plan.TargetContext.DatabaseRole,
            ["schemaRole"] = plan.TargetContext.SchemaRole,
            ["executionMode"] = plan.TargetContext.ExecutionMode
        },
        ["validator"] = new JsonObject
        {
            ["id"] = SqlValidationContract.ValidatorId,
            ["version"] = SqlValidationContract.ValidatorVersion
        },
        ["artifact"] = new JsonObject
        {
            ["algorithm"] = SqlValidationContract.DigestAlgorithm,
            ["digest"] = string.Empty
        },
        ["publicClaimLevel"] = "public-safe",
        ["assertions"] = new JsonArray(assertions.OrderBy(assertion => assertion.Code, StringComparer.Ordinal)
            .Select(assertion => (JsonNode)new JsonObject { ["code"] = assertion.Code, ["status"] = assertion.Status }).ToArray()),
        ["limitations"] = new JsonArray(SqlValidationContract.Limitations.Select(value => (JsonNode)JsonValue.Create(value)!).ToArray())
    };

    internal static string ComputeDigest(JsonNode root)
    {
        var copy = root.DeepClone();
        copy["artifact"]!["digest"] = string.Empty;
        var canonical = Canonicalize(copy).ToJsonString(new JsonSerializerOptions { WriteIndented = false });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    private static JsonNode Canonicalize(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            var sorted = new JsonObject();
            foreach (var property in obj.OrderBy(property => property.Key, StringComparer.Ordinal))
                sorted[property.Key] = property.Value is null ? null : Canonicalize(property.Value);
            return sorted;
        }
        if (node is JsonArray array)
            return new JsonArray(array.Select(item => item is null ? null : Canonicalize(item)).ToArray());
        return node.DeepClone();
    }

    private static string FormatTimestamp(DateTimeOffset value) => value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", System.Globalization.CultureInfo.InvariantCulture);
}
