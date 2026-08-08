using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class CSharpGraphCorrectnessFixtureTests
{
    private const string SemanticExtractor = "CSharpSemanticExtractor";

    [Fact]
    public void Same_name_types_keep_assembly_aware_ids_and_local_framework_collisions_separate()
    {
        using var temp = new TempDirectory();
        WriteProject(temp.Path, "Alpha");
        WriteProject(temp.Path, "Beta");
        WriteProject(temp.Path, "CollisionApp");
        WriteSource(temp.Path, "Alpha", "Collision.cs", """
            namespace Shared;

            public sealed class Collision
            {
                public void Ping() { }

                public void Invoke()
                {
                    Ping();
                }
            }
            """);
        WriteSource(temp.Path, "Beta", "Collision.cs", """
            namespace Shared;

            public sealed class Collision
            {
                public void Ping() { }

                public void Invoke()
                {
                    Ping();
                }
            }
            """);
        WriteSource(temp.Path, "CollisionApp", "TaskCollision.cs", """
            namespace CollisionApp;

            public sealed class Task
            {
                public void Run() { }
            }

            public sealed class Caller
            {
                public void Execute()
                {
                    var local = new Task();
                    var external = System.Threading.Tasks.Task.CompletedTask;
                    local.Run();
                    external.GetAwaiter();
                }
            }
            """);

        var result = Scan(temp.Path);

        Assert.Equal("Succeeded", result.Manifest.BuildStatus);
        Assert.DoesNotContain(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.CSharpSemanticWorkspace);

        var alphaDeclaration = AssertSemanticFact(result, FactTypes.TypeDeclared, "src/Alpha/Collision.cs", 3, 11, "global::Shared.Collision");
        var betaDeclaration = AssertSemanticFact(result, FactTypes.TypeDeclared, "src/Beta/Collision.cs", 3, 11, "global::Shared.Collision");
        Assert.Equal("csharp type Alpha%401.0.0.0 Shared.Collision", alphaDeclaration.Properties["targetSymbolId"]);
        Assert.Equal("csharp type Beta%401.0.0.0 Shared.Collision", betaDeclaration.Properties["targetSymbolId"]);
        Assert.NotEqual(alphaDeclaration.Properties["targetSymbolId"], betaDeclaration.Properties["targetSymbolId"]);

        var alphaCall = AssertSemanticCall(result, "src/Alpha/Collision.cs", 9, "Ping");
        var betaCall = AssertSemanticCall(result, "src/Beta/Collision.cs", 9, "Ping");
        Assert.Equal("Alpha", alphaCall.Properties["callerAssemblyName"]);
        Assert.Equal("Alpha", alphaCall.Properties["calleeAssemblyName"]);
        Assert.Equal("Beta", betaCall.Properties["callerAssemblyName"]);
        Assert.Equal("Beta", betaCall.Properties["calleeAssemblyName"]);
        Assert.Contains("Alpha%25401.0.0.0", alphaCall.Properties["sourceSymbolId"], StringComparison.Ordinal);
        Assert.Contains("Alpha%25401.0.0.0", alphaCall.Properties["targetSymbolId"], StringComparison.Ordinal);
        Assert.Contains("Beta%25401.0.0.0", betaCall.Properties["sourceSymbolId"], StringComparison.Ordinal);
        Assert.Contains("Beta%25401.0.0.0", betaCall.Properties["targetSymbolId"], StringComparison.Ordinal);
        Assert.NotEqual(alphaCall.Properties["targetSymbolId"], betaCall.Properties["targetSymbolId"]);

        var localTask = AssertSemanticFact(result, FactTypes.TypeDeclared, "src/CollisionApp/TaskCollision.cs", 3, 6, "global::CollisionApp.Task");
        Assert.Equal("csharp type CollisionApp%401.0.0.0 CollisionApp.Task", localTask.Properties["targetSymbolId"]);
        var localRun = AssertSemanticCall(result, "src/CollisionApp/TaskCollision.cs", 14, "Run");
        var externalAwaiter = AssertSemanticCall(result, "src/CollisionApp/TaskCollision.cs", 15, "GetAwaiter");
        Assert.Equal("CollisionApp", localRun.Properties["calleeAssemblyName"]);
        Assert.Equal("System.Runtime", externalAwaiter.Properties["calleeAssemblyName"]);
        Assert.Contains("CollisionApp.Task", localRun.Properties["targetSymbolDisplayName"], StringComparison.Ordinal);
        Assert.Contains("System.Threading.Tasks.Task", externalAwaiter.Properties["targetSymbolDisplayName"], StringComparison.Ordinal);
        Assert.NotEqual(localRun.Properties["targetSymbolId"], externalAwaiter.Properties["targetSymbolId"]);
    }

    [Fact]
    public void Partial_type_declarations_merge_and_cross_file_call_uses_canonical_member_endpoints()
    {
        using var temp = new TempDirectory();
        WriteProject(temp.Path, "PartialSample");
        WriteSource(temp.Path, "PartialSample", "Worker.State.cs", """
            namespace PartialSample;

            public sealed partial class Worker
            {
                private readonly Helper helper = new();

                public void Run()
                {
                    Execute();
                    helper.Touch();
                }
            }

            public sealed class Helper
            {
                public void Touch() { }
            }
            """);
        WriteSource(temp.Path, "PartialSample", "Worker.Behavior.cs", """
            namespace PartialSample;

            public sealed partial class Worker
            {
                private void Execute()
                {
                    helper.Touch();
                }
            }
            """);

        var result = Scan(temp.Path);

        Assert.Equal("Succeeded", result.Manifest.BuildStatus);
        Assert.DoesNotContain(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.CSharpSemanticWorkspace);
        var stateDeclaration = AssertSemanticFact(result, FactTypes.TypeDeclared, "src/PartialSample/Worker.State.cs", 3, 12, "global::PartialSample.Worker");
        var behaviorDeclaration = AssertSemanticFact(result, FactTypes.TypeDeclared, "src/PartialSample/Worker.Behavior.cs", 3, 9, "global::PartialSample.Worker");
        Assert.Equal("csharp type PartialSample%401.0.0.0 PartialSample.Worker", stateDeclaration.Properties["targetSymbolId"]);
        Assert.Equal(stateDeclaration.Properties["targetSymbolId"], behaviorDeclaration.Properties["targetSymbolId"]);

        var crossFileCall = AssertSemanticCall(result, "src/PartialSample/Worker.State.cs", 9, "Execute");
        Assert.Contains("PartialSample.Worker.Run", crossFileCall.Properties["sourceSymbolDisplayName"], StringComparison.Ordinal);
        Assert.Contains("PartialSample.Worker.Execute", crossFileCall.Properties["targetSymbolDisplayName"], StringComparison.Ordinal);
        Assert.Contains("PartialSample%25401.0.0.0", crossFileCall.Properties["sourceSymbolId"], StringComparison.Ordinal);
        Assert.Contains("PartialSample%25401.0.0.0", crossFileCall.Properties["targetSymbolId"], StringComparison.Ordinal);

        var helperCalls = result.Facts
            .Where(fact => IsSemanticCall(fact, "Touch"))
            .OrderBy(fact => fact.Evidence.FilePath, StringComparer.Ordinal)
            .ThenBy(fact => fact.Evidence.StartLine)
            .ToArray();
        Assert.Collection(
            helperCalls,
            call =>
            {
                AssertSpanAndProvenance(call, "src/PartialSample/Worker.Behavior.cs", 7, 7);
                Assert.Contains("PartialSample.Worker.Execute", call.Properties["sourceSymbolDisplayName"], StringComparison.Ordinal);
                Assert.Contains("PartialSample.Helper.Touch", call.Properties["targetSymbolDisplayName"], StringComparison.Ordinal);
            },
            call =>
            {
                AssertSpanAndProvenance(call, "src/PartialSample/Worker.State.cs", 10, 10);
                Assert.Contains("PartialSample.Worker.Run", call.Properties["sourceSymbolDisplayName"], StringComparison.Ordinal);
                Assert.Contains("PartialSample.Helper.Touch", call.Properties["targetSymbolDisplayName"], StringComparison.Ordinal);
            });
        Assert.Equal(helperCalls[0].Properties["targetSymbolId"], helperCalls[1].Properties["targetSymbolId"]);
    }

    [Fact]
    public void Receiver_resolution_honors_fields_parameters_inline_declarations_and_shadowing_and_reports_unresolved_calls()
    {
        using var temp = new TempDirectory();
        WriteProject(temp.Path, "ReceiverSample");
        WriteSource(temp.Path, "ReceiverSample", "Receivers.cs", """
            namespace Alpha
            {
                public sealed class Receiver { public void Touch() { } }
            }

            namespace Beta
            {
                public sealed class Receiver { public void Touch() { } }
            }

            namespace ReceiverSample
            {
                public sealed class Consumer
                {
                    private readonly Alpha.Receiver receiver = new();
                    public Beta.Receiver Property { get; } = new();

                    public void Exercise(Beta.Receiver parameter, object candidate)
                    {
                        receiver.Touch();
                        parameter.Touch();
                        Property.Touch();
                        if (TryResolve(out var inline)) inline.Touch();
                        if (candidate is Alpha.Receiver patterned) patterned.Touch();
                        {
                            var receiver = new Beta.Receiver();
                            receiver.Touch();
                            this.receiver.Touch();
                        }
                    }

                    private static bool TryResolve(out Alpha.Receiver value)
                    {
                        value = new Alpha.Receiver();
                        return true;
                    }

                    public void Broken(MissingReceiver missing)
                    {
                        missing.Touch();
                    }
                }
            }
            """);

        var result = Scan(temp.Path);

        Assert.Equal("FailedOrPartial", result.Manifest.BuildStatus);
        Assert.Equal("Level1SemanticAnalysisReduced", result.Manifest.AnalysisLevel);
        var expectedCalls = new[]
        {
            (Line: 20, Receiver: "Alpha.Receiver"),
            (Line: 21, Receiver: "Beta.Receiver"),
            (Line: 22, Receiver: "Beta.Receiver"),
            (Line: 23, Receiver: "Alpha.Receiver"),
            (Line: 24, Receiver: "Alpha.Receiver"),
            (Line: 27, Receiver: "Beta.Receiver"),
            (Line: 28, Receiver: "Alpha.Receiver")
        };
        foreach (var expected in expectedCalls)
        {
            var call = AssertSemanticCall(result, "src/ReceiverSample/Receivers.cs", expected.Line, "Touch");
            Assert.Contains(expected.Receiver + ".Touch", call.Properties["targetSymbolDisplayName"], StringComparison.Ordinal);
            Assert.Contains(expected.Receiver, call.Properties["targetSymbolId"], StringComparison.Ordinal);
            Assert.Contains("ReceiverSample.Consumer.Exercise", call.Properties["sourceSymbolDisplayName"], StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(call.Properties["sourceSymbolId"]));
            Assert.False(string.IsNullOrWhiteSpace(call.Properties["targetSymbolId"]));
        }

        var gap = Assert.Single(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.CSharpSemanticWorkspace
            && fact.Properties.GetValueOrDefault("diagnosticId") == "CS0246"
            && fact.Properties.GetValueOrDefault("diagnosticTokens")?.Split(';').Contains("MissingReceiver") == true);
        Assert.Equal(EvidenceTiers.Tier4Unknown, gap.EvidenceTier);
        AssertSpanAndProvenance(gap, "src/ReceiverSample/Receivers.cs", 38, 38);
        Assert.DoesNotContain(result.Facts, fact =>
            fact.FactType == FactTypes.CallEdge
            && fact.EvidenceTier == EvidenceTiers.Tier1Semantic
            && fact.Evidence.FilePath == "src/ReceiverSample/Receivers.cs"
            && fact.Evidence.StartLine == 40);
        var syntaxFallback = Assert.Single(result.Facts, fact =>
            fact.FactType == FactTypes.CallEdge
            && fact.RuleId == RuleIds.CSharpSyntaxCallGraph
            && fact.EvidenceTier == EvidenceTiers.Tier3SyntaxOrTextual
            && fact.TargetSymbol == "Touch"
            && fact.Evidence.FilePath == "src/ReceiverSample/Receivers.cs"
            && fact.Evidence.StartLine == 40);
        Assert.Equal("CSharpSyntaxExtractor", syntaxFallback.Evidence.ExtractorId);
        Assert.Equal(ScannerVersions.CSharpSyntaxExtractor, syntaxFallback.Evidence.ExtractorVersion);
    }

    private static ScanResult Scan(string repoPath) =>
        ScanEngine.Scan(new ScanOptions(repoPath, Path.Combine(repoPath, ".tracemap")));

    private static CodeFact AssertSemanticCall(ScanResult result, string filePath, int line, string contractElement)
    {
        var fact = Assert.Single(result.Facts, fact =>
            IsSemanticCall(fact, contractElement)
            && fact.Evidence.FilePath == filePath
            && fact.Evidence.StartLine == line);
        AssertSpanAndProvenance(fact, filePath, line, line);
        Assert.False(string.IsNullOrWhiteSpace(fact.Properties["sourceSymbolId"]));
        Assert.False(string.IsNullOrWhiteSpace(fact.Properties["targetSymbolId"]));
        return fact;
    }

    private static bool IsSemanticCall(CodeFact fact, string contractElement) =>
        fact.FactType == FactTypes.CallEdge
        && fact.RuleId == RuleIds.CSharpSemanticCallGraph
        && fact.EvidenceTier == EvidenceTiers.Tier1Semantic
        && fact.ContractElement == contractElement;

    private static CodeFact AssertSemanticFact(
        ScanResult result,
        string factType,
        string filePath,
        int startLine,
        int endLine,
        string targetSymbol)
    {
        var fact = Assert.Single(result.Facts, fact =>
            fact.FactType == factType
            && fact.RuleId == RuleIds.CSharpSemanticDeclarations
            && fact.EvidenceTier == EvidenceTiers.Tier1Semantic
            && fact.TargetSymbol == targetSymbol
            && fact.Evidence.FilePath == filePath);
        AssertSpanAndProvenance(fact, filePath, startLine, endLine);
        return fact;
    }

    private static void AssertSpanAndProvenance(CodeFact fact, string filePath, int startLine, int endLine)
    {
        Assert.Equal(filePath, fact.Evidence.FilePath);
        Assert.Equal(startLine, fact.Evidence.StartLine);
        Assert.Equal(endLine, fact.Evidence.EndLine);
        Assert.Equal(SemanticExtractor, fact.Evidence.ExtractorId);
        Assert.Equal(ScannerVersions.CSharpSemanticExtractor, fact.Evidence.ExtractorVersion);
    }

    private static void WriteProject(string repoPath, string projectName)
    {
        var directory = Path.Combine(repoPath, "src", projectName);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, projectName + ".csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
              </PropertyGroup>
            </Project>
            """);
    }

    private static void WriteSource(string repoPath, string projectName, string fileName, string source)
    {
        File.WriteAllText(Path.Combine(repoPath, "src", projectName, fileName), source);
    }
}
