using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using TraceMap.Cli;

namespace TraceMap.Tests;

public sealed class WebFormsHandlerSummaryTests
{
    [Fact]
    public async Task Handler_counts_preserve_variants_and_report_ambiguous_roots_and_truncation()
    {
        var directory = Path.Combine(Path.GetTempPath(), "tracemap-handler-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(directory, "compiled"));
        try
        {
            var app = Path.Combine(directory, "handoff.local.json");
            var compiled = Path.Combine(directory, "compiled/compiled-paths.handoff.local.json");
            File.WriteAllText(app, "{}");
            File.WriteAllText(compiled, """
                {"header":{"summary":{"truncated":true}},"nodes":{
                  "node:a":{"symbolId":"_Page.Target(System.Object)","sourceIndexId":"a","scanId":"s","commitSha":"c"},
                  "node:b":{"symbolId":"_Page.Target(System.Object)","sourceIndexId":"b","scanId":"s","commitSha":"c"},
                  "node:c":{"symbolId":"_Page.OtherTarget(System.Object)","sourceIndexId":"a","scanId":"s","commitSha":"c"}},
                 "variants":[{"nodeReferences":["node:a"]},{"nodeReferences":["node:a"]},{"nodeReferences":["node:b"]},{"nodeReferences":["node:c"]}],
                 "chains":[{"variantIndexes":[0,1]},{"variantIndexes":[2]},{"variantIndexes":[3]}]}
                """);
            string Hash(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
            await WebFormsReviewEvidenceIndex.WriteAsync(directory, "run", Hash(app), Hash(compiled), 1_000_000, 10_000_000, CancellationToken.None);
            using var connection = new SqliteConnection($"Data Source={Path.Combine(directory, WebFormsReviewEvidenceIndex.Name)};Mode=ReadOnly;Pooling=False");
            connection.Open();
            var result = WebFormsReviewExecutionCommand.ReadHandlerSummary(connection, "Target", CancellationToken.None);
            var values = result.Children.ToDictionary(item => item.Pointer.Split('/')[^1], item => item.Value!.Value);
            Assert.Equal(2, values["exactChains"].GetInt32());
            Assert.Equal(3, values["evidenceVariants"].GetInt32());
            Assert.Equal(2, values["rootIdentities"].GetInt32());
            Assert.True(values["compiledTruncated"].GetBoolean());
            Assert.Equal(0, WebFormsReviewExecutionCommand.ReadHandlerSummary(connection, "Missing", CancellationToken.None).Children[0].Value!.Value.GetInt32());
            Assert.Throws<InvalidDataException>(() => WebFormsReviewExecutionCommand.ReadHandlerSummary(connection, "private/path", CancellationToken.None));
        }
        finally { Directory.Delete(directory, true); }
    }
}
