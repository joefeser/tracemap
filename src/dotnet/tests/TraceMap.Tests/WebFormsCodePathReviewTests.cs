using System.Text.Json;
using System.Text.Json.Nodes;
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
            var lines = WebFormsCodePathReview.Run(inspection, sourceRoot, "case-001", output, includeRawSource: true);
            Assert.Contains("codePathReview=created", lines);
            Assert.Contains("artifacts=private-html;shareable-html;shareable-json;annotated-source-html:2", lines);
            Assert.Contains(lines, line => line == "case=case-001|sourceMode=working-tree|triggerContextLines=12|excerpts=4|definitionCandidates=1|anonymousNodes=2|anonymousEdges=1|review=unreviewed");
            Assert.DoesNotContain("Private", string.Join('\n', lines));
            var report = File.ReadAllText(output);
            Assert.Contains("Source mode: <code>working-tree</code>", report);
            Assert.Contains("Trigger context: <code>12 lines before and after the retained binding span</code>", report);
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
            Assert.Contains("diagnostic.webforms.local-code-path-review.v3", report);
            Assert.Contains("review.source-001.html#L3", report);
            Assert.Contains("review.source-002.html#L3", report);
            var annotatedSourcePath = Path.Combine(directory, "review.source-002.html");
            Assert.True(File.Exists(annotatedSourcePath));
            var annotatedSource = File.ReadAllText(annotatedSourcePath);
            Assert.Contains("Private annotated source source/Page.aspx.cs", annotatedSource);
            Assert.Contains("id=\"L5\"", annotatedSource);
            Assert.Contains("class=\"source-line handler retained-call\" id=\"L5\"", annotatedSource);
            Assert.Contains("review.private.html#evidence-", annotatedSource);
            Assert.Contains("UiReset();", annotatedSource);
            Assert.Contains(new string('x', 600), annotatedSource);
            Assert.DoesNotContain("[line truncated]", annotatedSource);
            var annotatedMarkup = File.ReadAllText(Path.Combine(directory, "review.source-001.html"));
            Assert.Contains("Private annotated source source/Page.aspx", annotatedMarkup);
            Assert.Contains("class=\"source-line event-binding\" id=\"L3\"", annotatedMarkup);
            Assert.Contains("&lt;asp:Button", annotatedMarkup);
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
            Assert.DoesNotContain("source-001.html", shareableHtml);
            Assert.DoesNotContain("source-001.html", shareableJson);
            Assert.Throws<IOException>(() => WebFormsCodePathReview.Run(inspection, sourceRoot, "case-001", output, includeRawSource: true));

            var indexedOutput = Path.Combine(directory, "indexed.private.html");
            WebFormsCodePathReview.Run(inspection, sourceRoot, "case-001", indexedOutput, returnHref: "index.html", includeRawSource: true);
            var indexedReport = File.ReadAllText(indexedOutput);
            Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(indexedReport, "← Return to review index").Count);
            Assert.Contains("href=\"index.html\"", indexedReport);
            Assert.Throws<InvalidDataException>(() => WebFormsCodePathReview.Run(inspection, sourceRoot, "case-001",
                Path.Combine(directory, "unsafe.private.html"), returnHref: "../private.html"));
        });
    }

    [Fact]
    public void WorkingTreeReviewOmitsRawSourceByDefault()
    {
        WithFixture((directory, sourceRoot, inspection) =>
        {
            var output = Path.Combine(directory, "review.private.html");
            WebFormsCodePathReview.Run(inspection, sourceRoot, "case-001", output);
            var report = File.ReadAllText(output);
            Assert.DoesNotContain("UiReset();", report, StringComparison.Ordinal);
            Assert.Contains("Raw source excerpts were not included", report, StringComparison.Ordinal);
            Assert.Empty(Directory.GetFiles(directory, "review.source-*.html"));
        });
    }

    [Fact]
    public void WorkingTreeReviewRejectsSymlinkEscape()
    {
        if (OperatingSystem.IsWindows()) return;
        WithFixture((directory, sourceRoot, inspection) =>
        {
            var outside = Path.Combine(directory, "outside");
            Directory.CreateDirectory(outside);
            File.Copy(Path.Combine(sourceRoot, "source", "Page.aspx.cs"), Path.Combine(outside, "Page.aspx.cs"));
            Directory.Delete(Path.Combine(sourceRoot, "source"), recursive: true);
            Directory.CreateSymbolicLink(Path.Combine(sourceRoot, "source"), outside);
            Assert.Throws<InvalidDataException>(() => WebFormsCodePathReview.Run(
                inspection, sourceRoot, "case-001", Path.Combine(directory, "review.private.html"), includeRawSource: true));
        });
    }

    [Fact]
    public void WorkingTreeReviewRejectsAnnotatedSourceCollisionWithoutPublishingOtherArtifacts()
    {
        WithFixture((directory, sourceRoot, inspection) =>
        {
            var collision = Path.Combine(directory, "review.source-001.html");
            File.WriteAllText(collision, "keep");
            Assert.Throws<IOException>(() => WebFormsCodePathReview.Run(
                inspection, sourceRoot, "case-001", Path.Combine(directory, "review.private.html"), includeRawSource: true));
            Assert.Equal("keep", File.ReadAllText(collision));
            Assert.False(File.Exists(Path.Combine(directory, "review.private.html")));
            Assert.False(File.Exists(Path.Combine(directory, "review.shareable.html")));
            Assert.False(File.Exists(Path.Combine(directory, "review.shareable.json")));
        });
    }

    [Fact]
    public void WorkingTreeReviewRejectsPathologicalAnnotatedSourceLineCountTransactionally()
    {
        WithFixture((directory, sourceRoot, inspection) =>
        {
            File.WriteAllLines(Path.Combine(sourceRoot, "source", "Page.aspx"), Enumerable.Repeat("x", 100_001));
            Assert.Throws<InvalidDataException>(() => WebFormsCodePathReview.Run(
                inspection, sourceRoot, "case-001", Path.Combine(directory, "review.private.html"), includeRawSource: true));
            Assert.Empty(Directory.GetFiles(directory, "review.*.html"));
            Assert.False(File.Exists(Path.Combine(directory, "review.shareable.json")));
        });
    }

    [Fact]
    public void WorkingTreeReviewRejectsAggregateAnnotatedSourceLineCountTransactionally()
    {
        WithFixture((directory, sourceRoot, inspection) =>
        {
            File.WriteAllLines(Path.Combine(sourceRoot, "source", "Page.aspx"), Enumerable.Repeat("x", 80_000));
            File.WriteAllLines(Path.Combine(sourceRoot, "source", "Page.aspx.cs"), Enumerable.Repeat("x", 80_000));
            var exception = Assert.Throws<InvalidDataException>(() => WebFormsCodePathReview.Run(
                inspection, sourceRoot, "case-001", Path.Combine(directory, "review.private.html"), includeRawSource: true));
            Assert.Equal("CodePathReviewSourceAggregateLineLimit", exception.Message);
            Assert.Empty(Directory.GetFiles(directory, "review.*.html"));
            Assert.False(File.Exists(Path.Combine(directory, "review.shareable.json")));
        });
    }

    [Fact]
    public void WorkingTreeReviewPreservesCaseDistinctSourcePaths()
    {
        if (OperatingSystem.IsWindows()) return;
        WithFixture((directory, sourceRoot, inspection) =>
        {
            var upperPath = Path.Combine(sourceRoot, "source", "Page.aspx.cs");
            var lowerPath = Path.Combine(sourceRoot, "source", "page.aspx.cs");
            var upperSource = File.ReadAllText(upperPath);
            var lowerSource = upperSource.Replace("UiReset();", "LowerCaseFile();", StringComparison.Ordinal);
            File.WriteAllText(lowerPath, lowerSource);
            if (File.ReadAllText(upperPath).Contains("LowerCaseFile();", StringComparison.Ordinal))
            {
                File.WriteAllText(upperPath, upperSource);
                return;
            }

            var root = JsonNode.Parse(File.ReadAllText(inspection))!;
            root["cases"]![0]!["methods"]![0]!["outgoingCallSites"]![0]!["filePath"] = "source/page.aspx.cs";
            File.WriteAllText(inspection, root.ToJsonString());

            var output = Path.Combine(directory, "review.private.html");
            WebFormsCodePathReview.Run(inspection, sourceRoot, "case-001", output, includeRawSource: true);

            var annotated = Directory.GetFiles(directory, "review.source-*.html")
                .Select(File.ReadAllText).ToArray();
            Assert.Equal(3, annotated.Length);
            Assert.Contains(annotated, value => value.Contains("Private annotated source source/Page.aspx.cs", StringComparison.Ordinal) &&
                value.Contains("UiReset();", StringComparison.Ordinal) && !value.Contains("LowerCaseFile();", StringComparison.Ordinal));
            Assert.Contains(annotated, value => value.Contains("Private annotated source source/page.aspx.cs", StringComparison.Ordinal) &&
                value.Contains("LowerCaseFile();", StringComparison.Ordinal) && !value.Contains("UiReset();", StringComparison.Ordinal));
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
        File.AppendAllText(Path.Combine(sourceRoot, "source", "Page.aspx.cs"), Environment.NewLine + "// " + new string('x', 600));
        File.WriteAllText(Path.Combine(sourceRoot, "source", "Page.aspx"), """
            <asp:Page>
              <asp:Button
                OnClick="Handler" />
            </asp:Page>
            """);
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
                    bindings = new[] { new { bindingLocation = Witness("Private.Page.Control", "Private.Page.Handler()", 3, 3, "source/Page.aspx") } },
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

        static object Witness(string caller, string callee, int startLine, int endLine, string filePath = "source/Page.aspx.cs") => new
        {
            factId = Guid.NewGuid().ToString("N"),
            kind = "MethodInvoked",
            caller,
            callee,
            filePath,
            startLine,
            endLine,
            ruleId = "csharp.semantic.methodinvocation.v1",
            tier = "Tier1Semantic"
        };
    }
}
