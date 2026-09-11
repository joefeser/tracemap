using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;
using TraceMap.Core;
using TraceMap.Reporting;
using TraceMap.Storage;

namespace TraceMap.Tests;

public sealed class WebFormsAgentEvidenceHandoffTests
{
    private const string CommitSha = "1111111111111111111111111111111111111111";

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
            CreateIndex(index, "scan-one", CommitSha);
            var corpus = Path.Combine(root, "docs");
            EvidenceDocsExporter.ExportAsync(new EvidenceDocsExportOptions(index, corpus, Format: "jsonl")).GetAwaiter().GetResult();

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
            Assert.NotEmpty(recommended.EnumerateArray());
            Assert.All(recommended.EnumerateArray(), item => Assert.StartsWith("chunks.jsonl#line=", item.GetProperty("locator").GetString()));
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

            var invalidSpan = handoff.RetrievalHints.First(hint => hint.RecipeId == "facts-by-file-span");
            var invalidStartLine = handoff with
            {
                RetrievalHints =
                [
                    invalidSpan with
                    {
                        Parameters = invalidSpan.Parameters.ToDictionary(
                            pair => pair.Key,
                            pair => pair.Key == "start_line" ? "not-a-line" : pair.Value,
                            StringComparer.Ordinal)
                    }
                ]
            };
            Assert.Throws<InvalidDataException>(() => WebFormsAgentEvidenceHandoff.WriteCase(Path.Combine(root, "invalid-span.json"), invalidStartLine));
        });
    }

    [Fact]
    public void CaseHandoffRejectsEvidenceWithoutDocumentedRuleOrTier()
    {
        WithFixture((root, _, handoff) =>
        {
            var witness = handoff.Evidence[0];
            Assert.Throws<InvalidDataException>(() => WebFormsAgentEvidenceHandoff.WriteCase(
                Path.Combine(root, "missing-rule.json"),
                handoff with { Evidence = [witness with { RuleId = "rule-unavailable" }] }));
            Assert.Throws<InvalidDataException>(() => WebFormsAgentEvidenceHandoff.WriteCase(
                Path.Combine(root, "invalid-tier.json"),
                handoff with { Evidence = [witness with { EvidenceTier = "Tier5Maybe" }] }));
            foreach (var tier in new[]
                     {
                         EvidenceTiers.Tier1Semantic,
                         EvidenceTiers.Tier2Structural,
                         EvidenceTiers.Tier3SyntaxOrTextual,
                         EvidenceTiers.Tier4Unknown
                     })
            {
                WebFormsAgentEvidenceHandoff.WriteCase(
                    Path.Combine(root, $"valid-{tier}.json"),
                    handoff with { Evidence = [witness with { EvidenceTier = tier }] });
            }
        });
    }

    [Fact]
    public void SetHandoffRejectsTamperedCorpusWithoutPublishing()
    {
        WithFixture((root, inspection, handoff) =>
        {
            var set = PrepareSet(root, inspection, handoff);
            var index = Path.Combine(root, "index.sqlite");
            CreateIndex(index, "scan-one", CommitSha);
            var corpus = Path.Combine(root, "docs");
            EvidenceDocsExporter.ExportAsync(new EvidenceDocsExportOptions(index, corpus, Format: "jsonl")).GetAwaiter().GetResult();
            File.AppendAllText(Path.Combine(corpus, "chunks.jsonl"), "{}\n");
            var output = Path.Combine(set, "agent-evidence-handoff.json");

            var error = Assert.Throws<InvalidDataException>(() => WebFormsAgentEvidenceHandoff.WriteSet(
                Path.Combine(set, "inspection.snapshot.json"), set, output, index, corpus));

            Assert.Equal("AgentHandoffCorpusIntegrityMismatch", error.Message);
            Assert.False(File.Exists(output));
        });
    }

    [Fact]
    public void SetHandoffRejectsCrossProductCorpusProvenance()
    {
        WithFixture((root, inspection, handoff) =>
        {
            var set = PrepareSet(root, inspection, handoff);
            var index = Path.Combine(root, "index.sqlite");
            CreateIndex(index, "scan-one", CommitSha);
            var corpus = Path.Combine(root, "docs");
            EvidenceDocsExporter.ExportAsync(new EvidenceDocsExportOptions(index, corpus, Format: "jsonl")).GetAwaiter().GetResult();
            RewriteManifest(corpus, manifest =>
            {
                var refs = manifest["inputs"]![0]!["sourceRefs"]!.AsArray();
                var first = refs[0]!.DeepClone().AsObject();
                var second = refs[0]!.DeepClone().AsObject();
                first["commitSha"] = "2222222222222222222222222222222222222222";
                second["scanId"] = "scan-two";
                refs.Clear();
                refs.Add(first);
                refs.Add(second);
            });
            var output = Path.Combine(set, "agent-evidence-handoff.json");

            var error = Assert.Throws<InvalidDataException>(() => WebFormsAgentEvidenceHandoff.WriteSet(
                Path.Combine(set, "inspection.snapshot.json"), set, output, index, corpus));

            Assert.Equal("AgentHandoffCorpusProvenanceMismatch", error.Message);
            Assert.False(File.Exists(output));
        });
    }

    [Fact]
    public void SetHandoffRejectsAlteredRecipeCatalogEvenWithUpdatedManifestDigest()
    {
        WithFixture((root, inspection, handoff) =>
        {
            var set = PrepareSet(root, inspection, handoff);
            var index = Path.Combine(root, "index.sqlite");
            CreateIndex(index, "scan-one", CommitSha);
            var corpus = Path.Combine(root, "docs");
            EvidenceDocsExporter.ExportAsync(new EvidenceDocsExportOptions(index, corpus, Format: "jsonl")).GetAwaiter().GetResult();
            var recipesPath = Path.Combine(corpus, "query-recipes.json");
            var recipes = JsonNode.Parse(File.ReadAllText(recipesPath))!.AsObject();
            recipes["recipes"]![0]!["title"] = "altered title";
            File.WriteAllText(recipesPath, SerializeNode(recipes), new UTF8Encoding(false));
            RewriteManifest(corpus, manifest => UpdateOutputDigest(manifest, corpus, "query-recipes.json"));
            var output = Path.Combine(set, "agent-evidence-handoff.json");

            var error = Assert.Throws<InvalidDataException>(() => WebFormsAgentEvidenceHandoff.WriteSet(
                Path.Combine(set, "inspection.snapshot.json"), set, output, index, corpus));

            Assert.Equal("AgentHandoffRecipeCatalogMismatch", error.Message);
            Assert.False(File.Exists(output));
        });
    }

    [Fact]
    public void SetHandoffRejectsMalformedChunkEvenWithUpdatedManifestDigest()
    {
        WithFixture((root, inspection, handoff) =>
        {
            var set = PrepareSet(root, inspection, handoff);
            var index = Path.Combine(root, "index.sqlite");
            CreateIndex(index, "scan-one", CommitSha);
            var corpus = Path.Combine(root, "docs");
            EvidenceDocsExporter.ExportAsync(new EvidenceDocsExportOptions(index, corpus, Format: "jsonl")).GetAwaiter().GetResult();
            File.WriteAllText(Path.Combine(corpus, "chunks.jsonl"), "{\"schemaVersion\":\"tracemap-evidence-docs.v1\"}\n", new UTF8Encoding(false));
            RewriteManifest(corpus, manifest => UpdateOutputDigest(manifest, corpus, "chunks.jsonl"));
            var output = Path.Combine(set, "agent-evidence-handoff.json");

            var error = Assert.Throws<InvalidDataException>(() => WebFormsAgentEvidenceHandoff.WriteSet(
                Path.Combine(set, "inspection.snapshot.json"), set, output, index, corpus));

            Assert.Equal("AgentHandoffCorpusSchemaMismatch", error.Message);
            Assert.False(File.Exists(output));
        });
    }

    [Fact]
    public void SetHandoffRejectsNoncanonicalIndexWithStableCode()
    {
        WithFixture((root, inspection, handoff) =>
        {
            var set = PrepareSet(root, inspection, handoff);
            var index = Path.Combine(root, "index.sqlite");
            CreateIndex(index, "scan-one", CommitSha);
            using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = index }.ToString()))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "drop table facts;";
                command.ExecuteNonQuery();
            }
            var output = Path.Combine(set, "agent-evidence-handoff.json");

            var error = Assert.Throws<InvalidDataException>(() => WebFormsAgentEvidenceHandoff.WriteSet(
                Path.Combine(set, "inspection.snapshot.json"), set, output, index));

            Assert.Equal("AgentHandoffIndexInvalid", error.Message);
            Assert.False(File.Exists(output));
        });
    }

    [Fact]
    public void SetHandoffRejectsIndexWithoutRecipeCallEdgeSurface()
    {
        WithFixture((root, inspection, handoff) =>
        {
            var set = PrepareSet(root, inspection, handoff);
            var index = Path.Combine(root, "index.sqlite");
            CreateIndex(index, "scan-one", CommitSha);
            using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = index }.ToString()))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "drop table call_edges;";
                command.ExecuteNonQuery();
            }
            var output = Path.Combine(set, "agent-evidence-handoff.json");

            var error = Assert.Throws<InvalidDataException>(() => WebFormsAgentEvidenceHandoff.WriteSet(
                Path.Combine(set, "inspection.snapshot.json"), set, output, index));

            Assert.Equal("AgentHandoffIndexInvalid", error.Message);
            Assert.False(File.Exists(output));
        });
    }

    [Fact]
    public void SetHandoffRejectsCaseThatDoesNotMatchInspection()
    {
        WithFixture((root, inspection, handoff) =>
        {
            var set = PrepareSet(root, inspection, handoff);
            var altered = handoff with { Subject = handoff.Subject with { Handler = "Private.Other.Handler()" } };
            File.WriteAllText(
                Path.Combine(set, "case-001.handoff.json"),
                JsonSerializer.Serialize(altered, TestJsonOptions) + "\n",
                new UTF8Encoding(false));
            var output = Path.Combine(set, "agent-evidence-handoff.json");

            var error = Assert.Throws<InvalidDataException>(() => WebFormsAgentEvidenceHandoff.WriteSet(
                Path.Combine(set, "inspection.snapshot.json"), set, output));

            Assert.Equal("AgentHandoffCaseInspectionMismatch", error.Message);
            Assert.False(File.Exists(output));
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
                    commitSha = CommitSha,
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
            "case-001", "case-001.private.html", "inspection.snapshot.json", WebFormsAgentEvidenceHandoff.HashFile(inspection),
            "agent-evidence-handoff.json");
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

    private static string PrepareSet(string root, string inspection, WebFormsAgentCaseHandoff handoff)
    {
        var set = Path.Combine(root, "set");
        Directory.CreateDirectory(set);
        File.Copy(inspection, Path.Combine(set, "inspection.snapshot.json"));
        WebFormsAgentEvidenceHandoff.WriteCase(Path.Combine(set, "case-001.handoff.json"), handoff);
        File.WriteAllText(Path.Combine(set, "case-001.private.html"), "private");
        return set;
    }

    private static void RewriteManifest(string corpus, Action<JsonObject> mutate)
    {
        var path = Path.Combine(corpus, "manifest.json");
        var manifest = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        mutate(manifest);
        var typed = JsonSerializer.Deserialize<EvidenceDocsManifest>(manifest.ToJsonString(), TestJsonOptions)!;
        File.WriteAllText(path, EvidenceDocsExporter.RenderSelfConsistentManifest(typed), new UTF8Encoding(false));
    }

    private static void UpdateOutputDigest(JsonObject manifest, string corpus, string relativePath)
    {
        var output = manifest["outputs"]!.AsArray().Select(value => value!.AsObject())
            .Single(value => value["path"]!.GetValue<string>() == relativePath);
        var path = Path.Combine(corpus, relativePath);
        var bytes = File.ReadAllBytes(path);
        output["sizeBytes"] = bytes.LongLength;
        output["lineCount"] = bytes.Count(value => value == (byte)'\n');
        output["sha256"] = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    private static readonly JsonSerializerOptions TestJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    private static string SerializeNode(JsonNode node) => Normalize(node.ToJsonString(TestJsonOptions));

    private static string Normalize(string value) => value.Replace("\r\n", "\n", StringComparison.Ordinal)
        .Replace("\r", "\n", StringComparison.Ordinal).TrimEnd('\n') + "\n";

    private static void CreateIndex(string path, string scanId, string commitSha)
    {
        var manifest = new ScanManifest(
            scanId,
            "private-repository",
            null,
            "test",
            commitSha,
            "test-version",
            DateTimeOffset.Parse("2026-09-11T00:00:00Z"),
            "Level1SemanticAnalysis",
            "Succeeded",
            [],
            [],
            [],
            []);
        var fact = new CodeFact(
            "fact-handler",
            scanId,
            manifest.RepoName,
            commitSha,
            null,
            FactTypes.MethodInvoked,
            "csharp.semantic.methodinvocation.v1",
            EvidenceTiers.Tier1Semantic,
            "Private.Page.Handler()",
            "Private.Page.LoadData()",
            null,
            new EvidenceSpan("source/Page.aspx.cs", 10, 10, null, "test", "1"),
            new Dictionary<string, string>(StringComparer.Ordinal) { ["surfaceIdentity"] = "surface-one" });
        SqliteIndexWriter.Write(path, manifest, [fact]);
    }
}
