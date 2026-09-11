using System.Text.Json;
using Microsoft.Data.Sqlite;
using TraceMap.Reporting;

namespace TraceMap.Tests;

public sealed class WebFormsAgentEvidenceHandoffTests
{
    [Fact]
    public void SetHandoffValidatesIndexAndSelectsMatchingCorpusChunks()
    {
        WithFixture((root, inspection, handoff) =>
        {
            var set = Path.Combine(root, "set");
            Directory.CreateDirectory(set);
            File.Copy(inspection, Path.Combine(set, "inspection.snapshot.json"));
            WebFormsAgentEvidenceHandoff.WriteCase(Path.Combine(set, "case-001.handoff.json"), handoff);
            File.WriteAllText(Path.Combine(set, "case-001.private.html"), "private");

            var index = Path.Combine(root, "index.sqlite");
            CreateIndex(index, "scan-one", "commit-one");
            var corpus = Path.Combine(root, "docs");
            Directory.CreateDirectory(corpus);
            File.WriteAllText(Path.Combine(corpus, "manifest.json"), JsonSerializer.Serialize(new
            {
                schemaVersion = EvidenceDocsExporter.SchemaVersion,
                commitShas = new[] { "commit-one" },
                inputs = new[] { new { sourceRefs = new[] { new { scanId = "scan-one" } } } }
            }));
            File.WriteAllText(Path.Combine(corpus, "query-recipes.json"),
                EvidenceDocsQueryRecipes.RenderJson(EvidenceDocsQueryRecipes.Build()));
            File.WriteAllText(Path.Combine(corpus, "chunks.jsonl"), JsonSerializer.Serialize(new
            {
                chunkId = "chunk:matching-one",
                chunkFamily = "webforms-modernization",
                chunkType = "event-chain",
                supportingIds = new[] { "fact-handler" },
                retrievalHints = Array.Empty<object>()
            }) + "\n" + JsonSerializer.Serialize(new
            {
                chunkId = "chunk:unrelated",
                chunkFamily = "gap",
                chunkType = "gap",
                supportingIds = new[] { "unrelated" },
                retrievalHints = Array.Empty<object>()
            }) + "\n");

            var output = Path.Combine(set, "agent-evidence-handoff.json");
            var lines = WebFormsAgentEvidenceHandoff.WriteSet(
                Path.Combine(set, "inspection.snapshot.json"), set, output, index, corpus);

            Assert.Contains("agentEvidenceHandoff=created", lines);
            using var document = JsonDocument.Parse(File.ReadAllText(output));
            var result = document.RootElement;
            Assert.Equal(WebFormsAgentEvidenceHandoff.SchemaVersion, result.GetProperty("schemaVersion").GetString());
            Assert.Equal("validated", result.GetProperty("evidenceStore").GetProperty("availability").GetString());
            Assert.Equal("validated", result.GetProperty("evidenceCorpus").GetProperty("availability").GetString());
            var recommended = result.GetProperty("cases")[0].GetProperty("recommendedChunks");
            Assert.Single(recommended.EnumerateArray());
            Assert.Equal("chunk:matching-one", recommended[0].GetProperty("chunkId").GetString());
            Assert.Equal("chunks/webforms-modernization/chunk-matching-one.md", recommended[0].GetProperty("locator").GetString());
            Assert.Contains("index.sqlite", result.GetProperty("evidenceStore").GetProperty("relativeLocator").GetString());
        });
    }

    [Fact]
    public void CaseHandoffSerializationIsDeterministicAndCarriesClosedRecipes()
    {
        WithFixture((root, _, handoff) =>
        {
            var first = Path.Combine(root, "first.json");
            var second = Path.Combine(root, "second.json");
            WebFormsAgentEvidenceHandoff.WriteCase(first, handoff);
            WebFormsAgentEvidenceHandoff.WriteCase(second, handoff);
            Assert.Equal(File.ReadAllBytes(first), File.ReadAllBytes(second));
            Assert.All(handoff.RetrievalHints, hint =>
                Assert.Contains(EvidenceDocsQueryRecipes.Build().Recipes, recipe => recipe.RecipeId == hint.RecipeId));
        });
    }

    [Fact]
    public void CaseHandoffRejectsUnknownRecipeAndInvalidParameters()
    {
        WithFixture((root, _, handoff) =>
        {
            var original = handoff.RetrievalHints[0];
            var unknown = handoff with { RetrievalHints = [original with { RecipeId = "unknown-recipe" }] };
            Assert.Throws<InvalidDataException>(() => WebFormsAgentEvidenceHandoff.WriteCase(Path.Combine(root, "unknown.json"), unknown));

            var invalidLimit = handoff with
            {
                RetrievalHints =
                [
                    original with
                    {
                        Parameters = original.Parameters.ToDictionary(
                            pair => pair.Key,
                            pair => pair.Key == "limit" ? "0" : pair.Value,
                            StringComparer.Ordinal)
                    }
                ]
            };
            Assert.Throws<InvalidDataException>(() => WebFormsAgentEvidenceHandoff.WriteCase(Path.Combine(root, "invalid.json"), invalidLimit));
        });
    }

    [Fact]
    public void SetHandoffRejectsMismatchedIndexWithoutPublishing()
    {
        WithFixture((root, inspection, handoff) =>
        {
            var set = Path.Combine(root, "set");
            Directory.CreateDirectory(set);
            File.Copy(inspection, Path.Combine(set, "inspection.snapshot.json"));
            WebFormsAgentEvidenceHandoff.WriteCase(Path.Combine(set, "case-001.handoff.json"), handoff);
            var index = Path.Combine(root, "index.sqlite");
            CreateIndex(index, "scan-other", "commit-other");
            var output = Path.Combine(set, "agent-evidence-handoff.json");

            var error = Assert.Throws<InvalidDataException>(() => WebFormsAgentEvidenceHandoff.WriteSet(
                Path.Combine(set, "inspection.snapshot.json"), set, output, index));

            Assert.Equal("AgentHandoffIndexProvenanceMismatch", error.Message);
            Assert.False(File.Exists(output));
        });
    }

    private static void WithFixture(Action<string, string, WebFormsAgentCaseHandoff> test)
    {
        var root = Path.Combine(Path.GetTempPath(), "tracemap-handoff-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var inspection = Path.Combine(root, "inspection.json");
        File.WriteAllText(inspection, JsonSerializer.Serialize(new
        {
            schemaVersion = "webforms-batch-inspection.v1",
            scanId = "scan-one",
            commitSha = "commit-one",
            cases = new[]
            {
                new
                {
                    caseId = "case-001",
                    handler = "Private.Page.Handler()",
                    bounded = false,
                    evidenceConclusion = "ui-control-operations-observed-with-unresolved-leaves",
                    handlerLocation = Witness("fact-handler", "handler", "Private.Page.Handler()", 10),
                    bindings = new[] { new { surfaceId = "surface-one", bindingLocation = Witness("fact-binding", "control", "Private.Page.Handler()", 3, "source/Page.aspx") } },
                    stoppingSymbols = new[] { "Private.Page.LoadData()" },
                    methods = new[]
                    {
                        new
                        {
                            symbol = "Private.Page.Handler()",
                            exactDeclarationLocations = Array.Empty<object>(),
                            outgoingCallSites = new[] { Witness("fact-call", "Private.Page.Handler()", "Private.Page.LoadData()", 12) }
                        },
                        new
                        {
                            symbol = "Private.Page.LoadData()",
                            exactDeclarationLocations = Array.Empty<object>(),
                            outgoingCallSites = Array.Empty<object>()
                        }
                    }
                }
            }
        }));
        using var document = JsonDocument.Parse(File.ReadAllText(inspection));
        var handoff = WebFormsAgentEvidenceHandoff.BuildCase(document.RootElement, document.RootElement.GetProperty("cases")[0],
            "case-001", "case-001.private.html", "inspection.snapshot.json", WebFormsAgentEvidenceHandoff.HashFile(inspection));
        try { test(root, inspection, handoff); }
        finally { Directory.Delete(root, recursive: true); }

        static object Witness(string factId, string caller, string callee, int line, string path = "source/Page.aspx.cs") => new
        {
            factId,
            kind = "MethodInvoked",
            caller,
            callee,
            filePath = path,
            startLine = line,
            endLine = line,
            ruleId = "csharp.semantic.methodinvocation.v1",
            tier = "Tier1Semantic"
        };
    }

    private static void CreateIndex(string path, string scanId, string commitSha)
    {
        using var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path }.ToString());
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            create table scan_manifest (
              scan_id text primary key,
              repo text not null,
              commit_sha text not null,
              scanner_version text not null,
              scanned_at text not null,
              analysis_level text not null,
              build_status text not null,
              manifest_json text not null
            );
            insert into scan_manifest values ($scan, 'private-repository', $commit, 'test-version', '2026-09-11T00:00:00Z', 'semantic', 'Succeeded', '{}');
            """;
        command.Parameters.AddWithValue("$scan", scanId);
        command.Parameters.AddWithValue("$commit", commitSha);
        command.ExecuteNonQuery();
    }
}
