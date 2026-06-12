using System.Security.Cryptography;
using System.Text;

namespace TraceMap.Core;

public static class FactFactory
{
    public static CodeFact Create(
        ScanManifest manifest,
        string factType,
        string ruleId,
        string evidenceTier,
        EvidenceSpan evidence,
        string? projectPath = null,
        string? sourceSymbol = null,
        string? targetSymbol = null,
        string? contractElement = null,
        IReadOnlyDictionary<string, string>? properties = null)
    {
        var stableProperties = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in properties ?? new Dictionary<string, string>())
        {
            stableProperties[key] = value;
        }
        var factId = CreateFactId(
            manifest.ScanId,
            factType,
            ruleId,
            evidence.FilePath,
            evidence.StartLine,
            evidence.EndLine,
            projectPath,
            sourceSymbol,
            targetSymbol,
            contractElement,
            stableProperties);

        return new CodeFact(
            factId,
            manifest.ScanId,
            manifest.RepoName,
            manifest.CommitSha,
            projectPath,
            factType,
            ruleId,
            evidenceTier,
            sourceSymbol,
            targetSymbol,
            contractElement,
            evidence,
            stableProperties);
    }

    private static string CreateFactId(
        string scanId,
        string factType,
        string ruleId,
        string filePath,
        int startLine,
        int endLine,
        string? projectPath,
        string? sourceSymbol,
        string? targetSymbol,
        string? contractElement,
        IReadOnlyDictionary<string, string> properties)
    {
        var builder = new StringBuilder();
        builder.Append(scanId).Append('|')
            .Append(factType).Append('|')
            .Append(ruleId).Append('|')
            .Append(filePath).Append('|')
            .Append(startLine).Append('|')
            .Append(endLine).Append('|')
            .Append(projectPath).Append('|')
            .Append(sourceSymbol).Append('|')
            .Append(targetSymbol).Append('|')
            .Append(contractElement);

        foreach (var (key, value) in properties)
        {
            builder.Append('|').Append(key).Append('=').Append(value);
        }

        return "fact-" + Hash(builder.ToString(), 20);
    }

    public static string Hash(string value, int length)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        var hex = Convert.ToHexString(bytes).ToLowerInvariant();
        return hex[..Math.Min(length, hex.Length)];
    }
}
