using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace TraceMap.Cli;

public static partial class WebFormsReviewExecutionCommand
{
    // A bounded projection of retained paths only, never a new traversal or a parity verdict.
    internal static WebFormsEvidenceItem ReadHandlerSummary(SqliteConnection connection, string handler, CancellationToken token)
    {
        if (handler.Length is < 1 or > 128 || handler.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '_'))
            throw WebFormsReviewEvidenceIndex.Invalid("HANDLER_INVALID");
        WebFormsEvidenceItem Read(string pointer, int limit = 1, int depth = 0) =>
            ReadQuery(connection, new("compiled", pointer, Limit: limit, Depth: depth), token).Result;
        string String(string pointer) => Read(pointer).Value?.GetString() ?? "";
        string Escape(string value) => value.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);
        var variants = Read("/variants").ChildCount;
        var chains = Read("/chains").ChildCount;
        if (variants > 4096 || chains > 4096) throw WebFormsReviewEvidenceIndex.Invalid("HANDLER_SUMMARY_LIMIT");
        var selected = new HashSet<int>();
        var identities = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < variants; i++)
        {
            token.ThrowIfCancellationRequested();
            var reference = String($"/variants/{i}/nodeReferences/0");
            var node = "/nodes/" + Escape(reference);
            var symbol = String(node + "/symbolId");
            var signature = symbol.Split('(', 2)[0];
            if (!signature.EndsWith("." + handler, StringComparison.Ordinal)) continue;
            selected.Add(i);
            identities.Add(String(node + "/sourceIndexId") + "\u001f" + String(node + "/scanId") + "\u001f" +
                String(node + "/commitSha") + "\u001f" + symbol);
        }
        var selectedChains = 0;
        var references = 0;
        for (var i = 0; i < chains; i++)
        {
            var pointer = $"/chains/{i}/variantIndexes";
            var count = Read(pointer).ChildCount;
            references = checked(references + count);
            if (references > 4096) throw WebFormsReviewEvidenceIndex.Invalid("HANDLER_SUMMARY_LIMIT");
            var matched = false;
            for (var j = 0; j < count; j++)
            {
                var index = Read(pointer + "/" + j).Value!.Value.GetInt32();
                if (index < 0 || index >= variants) throw WebFormsReviewEvidenceIndex.Invalid("HANDLER_SUMMARY_SHAPE");
                matched |= selected.Contains(index);
            }
            if (matched) selectedChains++;
        }
        var truncated = Read("/header/summary/truncated").Value!.Value.GetBoolean();
        WebFormsEvidenceItem Value(string name, object value) => new("/handler-summary/" + name, "scalar",
            JsonSerializer.SerializeToElement(value), 0, 0, 0, 0, []);
        WebFormsEvidenceItem[] children = [Value("exactChains", selectedChains), Value("evidenceVariants", selected.Count),
            Value("rootIdentities", identities.Count), Value("retainedVariantsInspected", variants),
            Value("compiledTruncated", truncated), Value("retainedOnly", true)];
        return new("/handler-summary", "object", null, children.Length, 0, children.Length, 0, children);
    }
}
