using Microsoft.CodeAnalysis;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed partial class SourceMetadataReconciliationTests
{
    // Fixture-authored expectations: never derive the expected declaration from
    // the reconciler's candidate, observation, edge or production identity provider.
    private static void AssertMatrixSourceEndpoint(CodeFact edge, CodeFact observation, string directory, string owner, string name)
    {
        var vb = directory == "vb";
        var tag = vb ? "visualbasic" : "csharp";
        var language = vb ? LanguageNames.VisualBasic : LanguageNames.CSharp;
        var assembly = vb ? "CompiledEvidence.VisualBasic" : "CompiledEvidence.CSharp";
        const string ns = "TraceMap.CompiledFixtures.Equivalence";
        var integer = "System.Runtime@10.0.0.0:" + (vb ? "Integer" : "int");
        var self = assembly + "@1.0.0.0:" + ns + ".OperatorShape";
        string[] parameters;
        string result;
        if (owner == "OperatorShape")
        {
            parameters = name switch { "op_Implicit" => [integer], "op_Explicit" => [self], _ => [self, self] };
            result = name == "op_Explicit" ? integer : self;
        }
        else
        {
            var count = owner == "ModuleShape" ? (name == "Renamed" ? 1 : 2) : (name == "Wide" ? 11 : 1);
            parameters = Enumerable.Repeat(integer, count).ToArray();
            result = integer;
        }
        var sourceType = tag + " type " + Uri.EscapeDataString(assembly + "@1.0.0.0") + " " + ns + "." + owner;
        var declaration = tag + " method " + Uri.EscapeDataString(sourceType) + " " + name
            + "(" + Uri.EscapeDataString(string.Join(",", parameters)) + ")->" + Uri.EscapeDataString(result);
        var signature = owner switch
        {
            "ModuleShape" => ManagedMetadataExtractorTests.ModuleSignature(name),
            "OperatorShape" => ManagedMetadataExtractorTests.OperatorSignature(name, directory),
            _ => "arity:0|call:default|hasThis:false|explicitThis:false|(" + string.Join(",", Enumerable.Repeat("type(namespace:6:System|names:5:Int32)", parameters.Length)) + ")->type(namespace:6:System|names:5:Int32)"
        };
        var metadata = "assembly:name:" + assembly.Length + ":" + assembly
            + "|version:7:1.0.0.0|culture:7:neutral|publicKeyToken:4:null|module:" + (assembly.Length + 4)
            + ":" + assembly + ".dll|targetFramework:25:.NETCoreApp,Version=v10.0|type:namespace:37:" + ns
            + "|names:" + owner.Length + ":" + owner + "|arity:0|method:" + name.Length + ":" + name + "|" + signature;
        var span = (owner, name) switch
        {
            ("ModuleShape", "Curried") => (195, 195, 259, 261),
            ("ModuleShape", "Tupled") => (196, 196, 262, 264),
            ("ModuleShape", "Renamed") => (197, 197, 265, 267),
            ("OperatorShape", "op_Addition") => (184, 184, 242, 244),
            ("OperatorShape", "op_Implicit") => (185, 185, 245, 247),
            ("OperatorShape", "op_Explicit") => (186, 186, 248, 250),
            ("OperatorShape", "op_LooksLikeOperator") => (187, 187, 251, 253),
            ("OptionalShape", "Required") => (136, 136, 164, 166),
            ("OptionalShape", "OptionalSeven") => (137, 137, 167, 169),
            ("OptionalShape", "OptionalNine") => (138, 138, 170, 172),
            ("OptionalShape", "Wide") => (139, 141, 173, 178),
            _ => throw new InvalidOperationException("Unlisted source fixture declaration")
        };
        Assert.Equal(metadata, edge.TargetSymbol);
        foreach (var fact in new[] { edge, observation })
        {
            Assert.Equal("source:" + language + "|" + metadata, fact.SourceSymbol);
            Assert.Equal(declaration, fact.Properties["sourceDeclarationIdentity"]);
            Assert.Equal("FixtureShapes." + (vb ? "vb" : "cs"), fact.Evidence.FilePath);
            Assert.Equal(vb ? span.Item3 : span.Item1, fact.Evidence.StartLine);
            Assert.Equal(vb ? span.Item4 : span.Item2, fact.Evidence.EndLine);
        }
    }

    [Theory]
    [InlineData("csharp", "CompiledEvidence.CSharp.dll")]
    [InlineData("vb", "CompiledEvidence.VisualBasic.dll")]
    public void Source_matrix_oracles_reject_self_consistent_wrong_source_endpoints(string directory, string fileName)
    {
        var source = Path.Combine(FindRepoRoot(), "samples", "compiled-dotnet-evidence", directory);
        var scan = ScanBound(source, [FixtureAssemblyPath(source, fileName)]);
        foreach (var (owner, name, otherName) in new[]
        {
            ("ModuleShape", "Curried", "Tupled"),
            ("OperatorShape", "op_Addition", "op_LooksLikeOperator"),
            ("OptionalShape", "Required", "OptionalSeven")
        })
        {
            CodeFact Edge(string selected) => Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.SourceMetadataIdentityReconciled
                && fact.TargetSymbol!.Contains("|names:" + owner.Length + ":" + owner + "|", StringComparison.Ordinal)
                && fact.TargetSymbol.Contains("|method:" + selected.Length + ":" + selected + "|", StringComparison.Ordinal));
            var edge = Edge(name);
            var other = Edge(otherName);
            var observation = Assert.Single(scan.Facts, fact => fact.FactId == edge.Properties["sourceFactId"]);
            AssertMatrixSourceEndpoint(edge, observation, directory, owner, name);
            foreach (var mutation in new[] { "symbol", "declaration", "span" })
            {
                CodeFact Corrupt(CodeFact fact) => mutation switch
                {
                    "symbol" => fact with { SourceSymbol = other.SourceSymbol },
                    "span" => fact with { Evidence = fact.Evidence with { StartLine = other.Evidence.StartLine, EndLine = other.Evidence.EndLine } },
                    _ => fact with { Properties = new SortedDictionary<string, string>(fact.Properties.ToDictionary(pair => pair.Key, pair => pair.Value), StringComparer.Ordinal)
                    { ["sourceDeclarationIdentity"] = other.Properties["sourceDeclarationIdentity"] } }
                };
                Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => AssertMatrixSourceEndpoint(Corrupt(edge), Corrupt(observation), directory, owner, name));
            }
        }
    }
}
