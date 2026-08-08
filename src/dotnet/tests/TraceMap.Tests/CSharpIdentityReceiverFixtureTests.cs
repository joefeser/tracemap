using System.Diagnostics;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class CSharpIdentityReceiverFixtureTests
{
    [Fact]
    public void Namespaces_overloads_aliases_nested_generics_and_receiver_forms_keep_compiler_identity()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        WriteProject(repo, "IdentitySample", """
            using FirstWidget = One.Widget;

            namespace One
            {
                public sealed class Widget
                {
                    public void Ping(int value) { }
                    public void Ping(string value) { }
                }
            }

            namespace Two
            {
                public sealed class Widget
                {
                    public void Ping() { }
                }
            }

            namespace IdentitySample
            {
                public interface IService
                {
                    void Work();
                }

                public sealed class Service : IService
                {
                    public void Work() { }
                }

                public static class ServiceExtensions
                {
                    public static void Extend(this IService service) { }
                }

                public static class StaticWorker
                {
                    public static void Go() { }
                }

                public sealed class Outer
                {
                    public sealed class Nested<T>
                    {
                        public void Take(T value) { }
                    }
                }

                public sealed class Caller
                {
                    public void Run(
                        FirstWidget first,
                        Two.Widget second,
                        Outer.Nested<int> numbers,
                        Outer.Nested<string> text,
                        IService service)
                    {
                        first.Ping(1);
                        first.Ping("one");
                        second.Ping();
                        numbers.Take(1);
                        text.Take("one");
                        StaticWorker.Go();
                        service.Work();
                        service.Extend();
                    }
                }
            }
            """);
        var result = Scan(repo);

        var calls = result.Facts
            .Where(fact => fact.FactType == FactTypes.CallEdge)
            .Where(fact => fact.RuleId == RuleIds.CSharpSemanticCallGraph)
            .Where(fact => fact.EvidenceTier == EvidenceTiers.Tier1Semantic)
            .Where(fact => fact.Evidence.FilePath == "src/IdentitySample/Fixture.cs")
            .ToArray();

        var pingCalls = calls.Where(fact => fact.ContractElement == "Ping").OrderBy(fact => fact.Evidence.StartLine).ToArray();
        Assert.Equal(3, pingCalls.Length);
        Assert.Equal(3, pingCalls.Select(TargetId).Distinct(StringComparer.Ordinal).Count());
        Assert.Contains("One.Widget", TargetId(pingCalls[0]), StringComparison.Ordinal);
        Assert.Contains(":int)->void", Uri.UnescapeDataString(TargetId(pingCalls[0])), StringComparison.Ordinal);
        Assert.Contains("One.Widget", TargetId(pingCalls[1]), StringComparison.Ordinal);
        Assert.Contains(":string)->void", Uri.UnescapeDataString(TargetId(pingCalls[1])), StringComparison.Ordinal);
        Assert.Contains("Two.Widget", TargetId(pingCalls[2]), StringComparison.Ordinal);
        Assert.EndsWith("Ping()->void", TargetId(pingCalls[2]), StringComparison.Ordinal);

        var takeCalls = calls.Where(fact => fact.ContractElement == "Take").OrderBy(fact => fact.Evidence.StartLine).ToArray();
        Assert.Equal(2, takeCalls.Length);
        Assert.Equal(2, takeCalls.Select(TargetId).Distinct(StringComparer.Ordinal).Count());
        Assert.All(takeCalls, fact => Assert.Contains("IdentitySample.Outer.Nested", TargetId(fact), StringComparison.Ordinal));
        Assert.Contains(":int)->void", Uri.UnescapeDataString(TargetId(takeCalls[0])), StringComparison.Ordinal);
        Assert.Contains(":string)->void", Uri.UnescapeDataString(TargetId(takeCalls[1])), StringComparison.Ordinal);

        var staticCall = Assert.Single(calls, fact => fact.ContractElement == "Go");
        Assert.Contains("IdentitySample.StaticWorker", TargetId(staticCall), StringComparison.Ordinal);
        var interfaceCall = Assert.Single(calls, fact => fact.ContractElement == "Work");
        Assert.Contains("IdentitySample.IService", TargetId(interfaceCall), StringComparison.Ordinal);
        var extensionCall = Assert.Single(calls, fact => fact.ContractElement == "Extend");
        Assert.Contains("IdentitySample.ServiceExtensions", TargetId(extensionCall), StringComparison.Ordinal);
        Assert.Equal("IdentitySample", extensionCall.Properties["calleeAssemblyName"]);

        Assert.All(calls, fact =>
        {
            Assert.Equal("csharp-semantic/0.15.0", fact.Evidence.ExtractorVersion);
            Assert.Equal(result.Manifest.CommitSha, fact.CommitSha);
            Assert.True(fact.Evidence.StartLine > 0);
            Assert.Equal(fact.Evidence.StartLine, fact.Evidence.EndLine);
        });
    }

    [Fact]
    public void Unresolved_global_symbol_does_not_bind_to_same_named_source_type()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        WriteProject(repo, "CollisionSample", """
            namespace Local
            {
                public sealed class MissingContract
                {
                    public void Execute() { }
                }
            }

            namespace CollisionSample
            {
                public sealed class Caller
                {
                    public void Run(Local.MissingContract resolved, global::MissingContract unresolved)
                    {
                        resolved.Execute();
                        unresolved.Execute();
                    }
                }
            }
            """);
        var result = Scan(repo);

        Assert.Equal("FailedOrPartial", result.Manifest.BuildStatus);
        var semanticExecute = Assert.Single(result.Facts, fact =>
            fact.FactType == FactTypes.CallEdge
            && fact.RuleId == RuleIds.CSharpSemanticCallGraph
            && fact.ContractElement == "Execute");
        Assert.Contains("Local.MissingContract", TargetId(semanticExecute), StringComparison.Ordinal);
        Assert.Equal(15, semanticExecute.Evidence.StartLine);
        Assert.DoesNotContain(result.Facts, fact =>
            fact.RuleId == RuleIds.CSharpSemanticCallGraph
            && fact.Evidence.FilePath == "src/CollisionSample/Fixture.cs"
            && fact.Evidence.StartLine == 16);
        Assert.Contains(result.Facts, fact =>
            fact.RuleId == RuleIds.CSharpSyntaxCallGraph
            && fact.TargetSymbol == "Execute"
            && fact.Evidence.StartLine == 16);
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.CSharpSemanticWorkspace
            && fact.EvidenceTier == EvidenceTiers.Tier4Unknown
            && fact.Properties.GetValueOrDefault("diagnosticId") == "CS0400");
    }

    private static string TargetId(CodeFact fact)
    {
        Assert.True(fact.Properties.TryGetValue("targetSymbolId", out var targetId));
        Assert.False(string.IsNullOrWhiteSpace(targetId));
        return targetId;
    }

    private static void WriteProject(string repo, string assemblyName, string source)
    {
        var directory = Path.Combine(repo, "src", assemblyName);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, $"{assemblyName}.csproj"), $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <AssemblyName>{assemblyName}</AssemblyName>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(directory, "Fixture.cs"), source);
        RunGit(repo, "init");
        RunGit(repo, "config", "user.email", "fixture@example.invalid");
        RunGit(repo, "config", "user.name", "TraceMap Fixture");
        RunGit(repo, "add", "-A");
        RunGit(repo, "commit", "-m", "fixture");
    }

    private static ScanResult Scan(string repo)
    {
        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(Path.GetDirectoryName(repo)!, "out")));
        Assert.Matches("^[0-9a-f]{40}$", result.Manifest.CommitSha);
        Assert.All(result.Facts, fact => Assert.Equal(result.Manifest.CommitSha, fact.CommitSha));
        return result;
    }

    private static void RunGit(string repo, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = repo,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)!;
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, process.StandardError.ReadToEnd());
    }
}
