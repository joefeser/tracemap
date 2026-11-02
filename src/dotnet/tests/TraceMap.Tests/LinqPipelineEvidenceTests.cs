using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.VisualBasic;
using TraceMap.Core;
using System.Text.Json;
namespace TraceMap.Tests;

public sealed class LinqPipelineEvidenceTests
{
    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Object_grouping_and_datarow_projection_are_distinct_bounded_semantic_dependencies(string language)
    {
        var (facts, gaps) = Extract(language);
        Assert.True(facts.Count == 3, string.Join("\n", facts.Select(fact => fact.ContractElement).Concat(gaps.Select(gap => gap.Properties!["gapKind"] + " at " + gap.Evidence.StartLine))));
        Assert.Equal(4, gaps.Count);
        Assert.All(facts, fact => {
            Assert.Equal(RuleIds.DotNetSemanticLinqPipeline, fact.RuleId);
            Assert.Equal(EvidenceTiers.Tier1Semantic, fact.EvidenceTier);
            Assert.DoesNotContain("public_identifier", fact.Properties!["pipeline"], StringComparison.Ordinal);
            Assert.Contains("execution-unproven", fact.Properties["coverage"], StringComparison.Ordinal);
        });
        var grouping = Assert.Single(facts, fact => fact.ContractElement == "object-group-count-filter");
        using var group = JsonDocument.Parse(grouping.Properties!["pipeline"]);
        Assert.Single(group.RootElement.GetProperty("captures").EnumerateArray());
        Assert.Equal(2, group.RootElement.GetProperty("mappings").GetArrayLength());
        var projections = facts.Where(fact => fact.ContractElement == "datatable-row-projection").ToArray();
        Assert.Equal(2, projections.Length);
        foreach (var projection in projections)
        {
            using var json = JsonDocument.Parse(projection.Properties!["pipeline"]);
            Assert.Equal(2, json.RootElement.GetProperty("mappings").GetArrayLength());
            Assert.All(json.RootElement.GetProperty("mappings").EnumerateArray(), map => Assert.Equal(64, map.GetProperty("columnSha256").GetString()!.Length));
        }
        Assert.Contains(gaps, fact => fact.Properties!["gapKind"] == "linq-pipeline-constant-row-column-required");
        Assert.Contains(gaps, fact => fact.Properties!["gapKind"] == "linq-pipeline-framework-overload-or-identity");
        Assert.Contains(gaps, fact => fact.Properties!["gapKind"] == "linq-pipeline-indexed-or-nonfunc-callback");
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Replay_is_deterministic_and_does_not_replace_SQL_lifecycle_facts(string language)
    {
        var first = Extract(language); var second = Extract(language);
        Assert.Equal(first.Facts.Select(fact => fact.Properties!["pipeline"]), second.Facts.Select(fact => fact.Properties!["pipeline"]));
        Assert.All(first.Facts, fact => Assert.Equal(FactTypes.LinqPipelineCandidate, fact.FactType));
        Assert.DoesNotContain(first.Facts, fact => fact.FactType is FactTypes.ManagedIlSqlLifecycleEntryCandidate or FactTypes.ManagedIlSqlLifecycleCandidate);
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Jtoken_filter_projection_and_join_bind_keys_without_claiming_JSON_origin_or_values(string language)
    {
        var (facts, gaps) = Extract(language, "JsonLinqPipelines");
        Assert.True(facts.Count == 3, string.Join("\n", facts.Select(fact => fact.ContractElement).Concat(gaps.Select(gap => gap.Properties!["gapKind"] + " at " + gap.Evidence.StartLine))));
        Assert.Equal(3, gaps.Count);
        foreach (var fact in facts)
        {
            Assert.Equal("jtoken-filter-project-join", fact.ContractElement);
            var serialized = fact.Properties!["pipeline"];
            Assert.DoesNotContain("public-alpha", serialized, StringComparison.Ordinal);
            Assert.DoesNotContain("caption", serialized, StringComparison.Ordinal);
            using var json = JsonDocument.Parse(serialized);
            Assert.Contains("json-origin-unmodeled", json.RootElement.GetProperty("sourceKind").GetString()!, StringComparison.Ordinal);
            Assert.Equal(FactFactory.Hash("kind", 64), json.RootElement.GetProperty("jsonFilter").GetProperty("keySha256").GetString());
            Assert.Equal(FactFactory.Hash("public-alpha", 64), json.RootElement.GetProperty("jsonFilter").GetProperty("comparedLiteralSha256").GetString());
            Assert.Equal(FactFactory.Hash("caption", 64), json.RootElement.GetProperty("mappings")[0].GetProperty("columnSha256").GetString());
            Assert.Equal(3, json.RootElement.GetProperty("operators").GetArrayLength());
        }
        Assert.Contains(gaps, fact => fact.Properties!["gapKind"] == "linq-pipeline-json-constant-token-key-required");
        Assert.Contains(gaps, fact => fact.Properties!["gapKind"] == "linq-pipeline-json-value-string-identity");
        Assert.Contains(gaps, fact => fact.Properties!["gapKind"] == "linq-pipeline-json-pipeline-alias-mutated-or-ref-escape");
        Assert.Equal(facts.Select(fact => fact.Properties!["pipeline"]), Extract(language, "JsonLinqPipelines").Facts.Select(fact => fact.Properties!["pipeline"]));
    }

    [Fact]
    public void Unsigned_framework_name_impostor_does_not_emit_pipeline_evidence()
    {
        const string source = """
            using System; using System.Collections.Generic;
            namespace System.Linq {
                public static class Enumerable {
                    public static IEnumerable<R> Select<T,R>(IEnumerable<T> source, Func<T,R> selector) => null;
                    public static List<T> ToList<T>(IEnumerable<T> source) => null;
                }
            }
            public class Example {
                public object Run(IEnumerable<string> values) => System.Linq.Enumerable.ToList(
                    System.Linq.Enumerable.Select(values, value => value));
            }
            """;
        var tree = CSharpSyntaxTree.ParseText(source);
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Append(typeof(Newtonsoft.Json.Linq.JToken).Assembly.Location).Distinct(StringComparer.Ordinal).Select(path => MetadataReference.CreateFromFile(path)).ToArray();
        var compilation = CSharpCompilation.Create("PublicImpostor", [tree], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        Assert.Empty(compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        var facts = new List<SemanticFactCandidate>(); var gaps = new List<SemanticFactCandidate>();
        LinqPipelineEvidenceExtractor.Extract(null, "PublicImpostor.cs", tree.GetRoot(), compilation.GetSemanticModel(tree), facts, gaps);
        Assert.Empty(facts);
        Assert.Empty(gaps);
    }

    private static (List<SemanticFactCandidate> Facts, List<SemanticFactCandidate> Gaps) Extract(string language, string sampleName = "LinqPipelines")
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))) directory = directory.Parent;
        Assert.NotNull(directory);
        var extension = language == "csharp" ? "cs" : "vb";
        var path = Path.Combine(directory.FullName, "samples", "sql-compiled-wrapper-evidence", language, "end-to-end", sampleName + "." + extension);
        var source = File.ReadAllText(path);
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Append(typeof(Newtonsoft.Json.Linq.JToken).Assembly.Location).Distinct(StringComparer.Ordinal).Select(path => MetadataReference.CreateFromFile(path)).ToArray();
        SyntaxTree tree; Compilation compilation;
        if (language == "csharp")
        {
            tree = CSharpSyntaxTree.ParseText(source, path: sampleName + ".cs");
            compilation = CSharpCompilation.Create("PublicLinq", [tree], references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        }
        else
        {
            tree = VisualBasicSyntaxTree.ParseText(source, path: sampleName + ".vb");
            compilation = VisualBasicCompilation.Create("PublicLinq", [tree], references, new VisualBasicCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optionInfer: true));
        }
        Assert.Empty(compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        var facts = new List<SemanticFactCandidate>(); var gaps = new List<SemanticFactCandidate>();
        LinqPipelineEvidenceExtractor.Extract(null, tree.FilePath, tree.GetRoot(), compilation.GetSemanticModel(tree), facts, gaps);
        return (facts, gaps);
    }
}
