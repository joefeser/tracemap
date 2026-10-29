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
        Assert.Equal(3, facts.Count);
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
            .Distinct(StringComparer.Ordinal).Select(path => MetadataReference.CreateFromFile(path)).ToArray();
        var compilation = CSharpCompilation.Create("PublicImpostor", [tree], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        Assert.Empty(compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        var facts = new List<SemanticFactCandidate>(); var gaps = new List<SemanticFactCandidate>();
        LinqPipelineEvidenceExtractor.Extract(null, "PublicImpostor.cs", tree.GetRoot(), compilation.GetSemanticModel(tree), facts, gaps);
        Assert.Empty(facts);
        Assert.Empty(gaps);
    }

    private static (List<SemanticFactCandidate> Facts, List<SemanticFactCandidate> Gaps) Extract(string language)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))) directory = directory.Parent;
        Assert.NotNull(directory);
        var extension = language == "csharp" ? "cs" : "vb";
        var path = Path.Combine(directory.FullName, "samples", "sql-compiled-wrapper-evidence", language, "end-to-end", "LinqPipelines." + extension);
        var source = File.ReadAllText(path);
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Distinct(StringComparer.Ordinal).Select(path => MetadataReference.CreateFromFile(path)).ToArray();
        SyntaxTree tree; Compilation compilation;
        if (language == "csharp")
        {
            tree = CSharpSyntaxTree.ParseText(source, path: "LinqPipelines.cs");
            compilation = CSharpCompilation.Create("PublicLinq", [tree], references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        }
        else
        {
            tree = VisualBasicSyntaxTree.ParseText(source, path: "LinqPipelines.vb");
            compilation = VisualBasicCompilation.Create("PublicLinq", [tree], references, new VisualBasicCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optionInfer: true));
        }
        Assert.Empty(compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error));
        var facts = new List<SemanticFactCandidate>(); var gaps = new List<SemanticFactCandidate>();
        LinqPipelineEvidenceExtractor.Extract(null, tree.FilePath, tree.GetRoot(), compilation.GetSemanticModel(tree), facts, gaps);
        return (facts, gaps);
    }
}
