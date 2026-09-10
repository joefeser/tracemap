using System.Text.Json;
using TraceMap.Reporting;

namespace TraceMap.Tests;

public sealed class WebFormsCodePathReviewTests
{
    [Fact]
    public void WorkingTreeReviewRendersBoundedPathAndUniqueDefinitionCandidateWithoutConsoleDisclosure()
    {
        WithFixture((directory, sourceRoot, inspection) =>
        {
            var output = Path.Combine(directory, "review.private.html");
            var lines = WebFormsCodePathReview.Run(inspection, sourceRoot, "case-001", output);
            Assert.Contains("codePathReview=created", lines);
            Assert.Contains(lines, line => line == "case=case-001|sourceMode=working-tree|excerpts=4|definitionCandidates=1|anonymousNodes=2|anonymousEdges=1|review=unreviewed");
            Assert.DoesNotContain("Private", string.Join('\n', lines));
            var report = File.ReadAllText(output);
            Assert.Contains("Source mode: <code>working-tree</code>", report);
            Assert.Contains("Private.Page.Handler()", report);
            Assert.Contains("UiReset();", report);
            Assert.Contains("unique-name-definition-candidate-not-evidence", report);
            Assert.Contains("Expected UI/control-only behavior", report);
            Assert.Contains("id=\"call-path\"", report);
            Assert.Contains("href=\"#evidence-", report);
            Assert.Contains("aria-label=\"Anonymous retained call graph\"", report);
            Assert.Contains("class=\"graph-node\"", report);
            Assert.Contains("handler-001", report);
            Assert.Contains("calls x 1", report);
            Assert.DoesNotContain("<iframe", report);
            Assert.DoesNotContain("mermaid.esm", report);
            Assert.Contains("Private alias legend", report);
            Assert.Contains("id=\"trigger\" open", report);
            Assert.Contains("id=\"call-path\" open", report);
            Assert.Contains("id=\"verdict\" open", report);
            Assert.Contains("class=\"panel\" id=\"graph\"><summary>", report);
            Assert.Contains("class=\"panel\" id=\"evidence\"><summary>", report);
            Assert.Contains("function revealTarget()", report);
            var shareableHtml = File.ReadAllText(Path.Combine(directory, "review.shareable.html"));
            var shareableJson = File.ReadAllText(Path.Combine(directory, "review.shareable.json"));
            Assert.Contains("flowchart TD", shareableHtml);
            Assert.Contains("handler_001 -->|calls x 1| node_001", shareableHtml);
            Assert.DoesNotContain("  click ", shareableHtml);
            Assert.Contains("const graphNavigation", shareableHtml);
            Assert.Contains("targetId", shareableHtml);
            Assert.Contains("href=\"#node-handler-001\"", shareableHtml);
            Assert.Contains("mermaid@11.17.2", shareableHtml);
            Assert.DoesNotContain("-->|\"", shareableHtml);
            Assert.Contains("handler-001", shareableHtml);
            Assert.Contains("diagnostic.webforms.anonymous-code-path-review.v1", shareableJson);
            Assert.DoesNotContain("Private", shareableHtml);
            Assert.DoesNotContain("UiReset", shareableHtml);
            Assert.DoesNotContain("source/Page", shareableHtml);
            Assert.DoesNotContain("Private", shareableJson);
            Assert.DoesNotContain("UiReset", shareableJson);
            Assert.DoesNotContain("source/Page", shareableJson);
            Assert.Throws<IOException>(() => WebFormsCodePathReview.Run(inspection, sourceRoot, "case-001", output));
        });
    }

    [Fact]
    public void WorkingTreeReviewRejectsSourceTraversal()
    {
        WithFixture((directory, sourceRoot, inspection) =>
        {
            using var document = JsonDocument.Parse(File.ReadAllText(inspection));
            var json = document.RootElement.GetRawText().Replace("source/Page.aspx.cs", "../outside.cs", StringComparison.Ordinal);
            File.WriteAllText(inspection, json);
            Assert.Throws<InvalidDataException>(() => WebFormsCodePathReview.Run(
                inspection, sourceRoot, "case-001", Path.Combine(directory, "review.md")));
        });
    }

    private static void WithFixture(Action<string, string, string> test)
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var sourceRoot = Path.Combine(directory, "repo");
        Directory.CreateDirectory(Path.Combine(sourceRoot, "source"));
        var source = """
            class Page
            {
                void Handler()
                {
                    UiReset();
                }

                void UiReset()
                {
                    Enabled = false;
                }

                bool Enabled { get; set; }
            }
            """;
        File.WriteAllText(Path.Combine(sourceRoot, "source", "Page.aspx.cs"), source);
        var inspection = Path.Combine(directory, "inspection.json");
        File.WriteAllText(inspection, JsonSerializer.Serialize(new
        {
            schemaVersion = "webforms-batch-inspection.v1",
            commitSha = "commit-one",
            cases = new[]
            {
                new
                {
                    caseId = "case-001",
                    bounded = false,
                    evidenceConclusion = "ui-control-operations-observed-with-unresolved-leaves",
                    handlerLocation = Witness("handler", "Private.Page.Handler()", 3, 6),
                    bindings = new[] { new { bindingLocation = Witness("Private.Page.Control", "Private.Page.Handler()", 3, 3) } },
                    unresolvedOtherLeaves = new[] { "Private.Page.UiReset()" },
                    methods = new object[]
                    {
                        new
                        {
                            symbol = "Private.Page.Handler()",
                            exactDeclarationLocations = Array.Empty<object>(),
                            outgoingCallSites = new[] { Witness("Private.Page.Handler()", "Private.Page.UiReset()", 5, 5) }
                        },
                        new
                        {
                            symbol = "Private.Page.UiReset()",
                            exactDeclarationLocations = Array.Empty<object>(),
                            outgoingCallSites = Array.Empty<object>()
                        }
                    }
                }
            }
        }));
        try { test(directory, sourceRoot, inspection); }
        finally { Directory.Delete(directory, true); }

        static object Witness(string caller, string callee, int startLine, int endLine) => new
        {
            factId = Guid.NewGuid().ToString("N"),
            kind = "MethodInvoked",
            caller,
            callee,
            filePath = "source/Page.aspx.cs",
            startLine,
            endLine,
            ruleId = "csharp.semantic.methodinvocation.v1",
            tier = "Tier1Semantic"
        };
    }
}
