using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class CSharpSyntaxExtractorTests
{
    [Fact]
    public void Scan_extracts_syntax_facts_from_broken_project_without_build()
    {
        using var temp = new TempDirectory();
        Directory.CreateDirectory(Path.Combine(temp.Path, "src", "Broken"));
        File.WriteAllText(Path.Combine(temp.Path, "src", "Broken", "Broken.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(temp.Path, "src", "Broken", "CustomerProfile.cs"), """
            using System;
            using System.Net.Http;

            namespace BrokenSample;

            [Obsolete]
            public sealed class CustomerProfile
            {
                public string PrimaryEmail { get; set; } = "";

                public async Task LoadAsync(HttpClient client, MissingType missing)
                {
                    var profile = new CustomerProfile();
                    Console.WriteLine(profile.PrimaryEmail);
                    await client.GetAsync("/profiles");
                    missing.DoesNotCompile();
                }
            }

            public enum CustomerStatus
            {
                Active,
                Suspended
            }
            """);

        var result = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, ".tracemap")));

        Assert.Equal("Level1SemanticAnalysisReduced", result.Manifest.AnalysisLevel);
        Assert.Equal("FailedOrPartial", result.Manifest.BuildStatus);
        Assert.Contains(result.Manifest.KnownGaps, gap => gap.Contains("Compilation diagnostic", StringComparison.OrdinalIgnoreCase));
        AssertFact(result, FactTypes.TypeDeclared, "CustomerProfile", "src/Broken/CustomerProfile.cs", 6, 18);
        AssertFact(result, FactTypes.PropertyDeclared, "PrimaryEmail", "src/Broken/CustomerProfile.cs", 9, 9);
        AssertFact(result, FactTypes.MethodDeclared, "LoadAsync", "src/Broken/CustomerProfile.cs", 11, 17);
        AssertFact(result, FactTypes.EnumDeclared, "CustomerStatus", "src/Broken/CustomerProfile.cs", 20, 24);
        AssertFact(result, FactTypes.AttributeUsed, "Obsolete", "src/Broken/CustomerProfile.cs", 6, 6);
        AssertFact(result, FactTypes.ObjectCreated, "CustomerProfile", "src/Broken/CustomerProfile.cs", 13, 13);
        AssertFact(result, FactTypes.MemberAccessName, "PrimaryEmail", "src/Broken/CustomerProfile.cs", 14, 14);
        AssertFact(result, FactTypes.InvocationName, "GetAsync", "src/Broken/CustomerProfile.cs", 15, 15);
        AssertFact(result, FactTypes.CallEdge, "GetAsync", "src/Broken/CustomerProfile.cs", 15, 15);
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.CallEdge
            && fact.RuleId == RuleIds.CSharpSyntaxCallGraph
            && fact.TargetSymbol == "CustomerProfile"
            && fact.Properties.TryGetValue("callKind", out var callKind)
            && callKind == "SyntaxObjectCreation");

        Assert.All(
            result.Facts.Where(fact => fact.RuleId.StartsWith("csharp.syntax.", StringComparison.Ordinal)),
            fact => Assert.Equal(EvidenceTiers.Tier3SyntaxOrTextual, fact.EvidenceTier));
    }

    [Fact]
    public void Scan_records_parse_errors_as_analysis_gaps()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "BrokenSyntax.cs"), """
            public sealed class BrokenSyntax
            {
                public void M(
            }
            """);

        var result = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, ".tracemap")));

        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.CSharpSyntaxDeclarations
            && fact.Evidence.FilePath == "BrokenSyntax.cs"
            && fact.Evidence.StartLine >= 1);
    }

    [Fact]
    public void Scan_does_not_extract_syntax_facts_from_generated_source()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "Generated.g.cs"), """
            public sealed class GeneratedType
            {
                public string GeneratedProperty { get; set; } = "";
            }
            """);

        var result = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, ".tracemap")));

        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.FileInventoried && fact.Evidence.FilePath == "Generated.g.cs");
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.InfrastructureBoilerplate
            && fact.TargetSymbol == "GeneratedSource"
            && fact.Evidence.FilePath == "Generated.g.cs");
        Assert.DoesNotContain(result.Facts, fact => fact.TargetSymbol == "GeneratedType");
        Assert.DoesNotContain(result.Facts, fact => fact.TargetSymbol == "GeneratedProperty");
    }

    [Fact]
    public void Scan_extracts_logic_hotspots_from_syntax()
    {
        using var temp = new TempDirectory();
        Directory.CreateDirectory(Path.Combine(temp.Path, "src", "Logic"));
        File.WriteAllText(Path.Combine(temp.Path, "src", "Logic", "RetryMath.cs"), """
            using System;

            public sealed class RetryMath
            {
                public TimeSpan GetShouldRetry(int currentRetryCount)
                {
                    if (currentRetryCount < 3)
                    {
                        var delay = TimeSpan.FromMilliseconds(100 + (50 * currentRetryCount));
                        return delay;
                    }

                    return TimeSpan.Zero;
                }
            }
            """);

        var result = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, ".tracemap")));

        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.CalculationExpression
            && fact.Evidence.FilePath == "src/Logic/RetryMath.cs"
            && fact.Properties.ContainsKey("expressionHash"));
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.BranchingLogic
            && fact.TargetSymbol == "IfStatement");
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.RetryPolicyLogic
            && fact.TargetSymbol == "GetShouldRetry");
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.CallEdge
            && fact.SourceSymbol == "GetShouldRetry"
            && fact.TargetSymbol == "FromMilliseconds");
    }

    [Fact]
    public void Syntax_expression_facts_store_hashes_and_safe_names_not_raw_expressions()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "Sample.cs"), """
            public sealed class Sample
            {
                public void Run(dynamic logger)
                {
                    logger.Info(BuildMessage());
                }

                private string BuildMessage()
                {
                    return "ok";
                }
            }
            """);

        var result = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, ".tracemap")));

        var invocation = Assert.Single(result.Facts, fact =>
            fact.FactType == FactTypes.InvocationName
            && fact.TargetSymbol == "Info"
            && fact.RuleId == RuleIds.CSharpSyntaxInvocation);
        Assert.DoesNotContain("expression", invocation.Properties.Keys);
        Assert.Equal("SimpleMemberAccessExpression", invocation.Properties["expressionKind"]);
        Assert.Equal("logger", invocation.Properties["receiverName"]);
        Assert.True(invocation.Properties["expressionHash"].Length > 0);

        var memberAccess = Assert.Single(result.Facts, fact =>
            fact.FactType == FactTypes.MemberAccessName
            && fact.TargetSymbol == "Info"
            && fact.RuleId == RuleIds.CSharpSyntaxMemberAccess);
        Assert.DoesNotContain("expression", memberAccess.Properties.Keys);
        Assert.Equal("IdentifierName", memberAccess.Properties["expressionKind"]);
        Assert.Equal("logger", memberAccess.SourceSymbol);
    }

    private static void AssertFact(ScanResult result, string factType, string targetSymbol, string filePath, int startLine, int endLine)
    {
        var fact = Assert.Single(result.Facts, fact =>
            fact.FactType == factType
            && fact.TargetSymbol == targetSymbol
            && fact.Evidence.FilePath == filePath
            && fact.RuleId.StartsWith("csharp.syntax.", StringComparison.Ordinal));

        Assert.Equal(startLine, fact.Evidence.StartLine);
        Assert.Equal(endLine, fact.Evidence.EndLine);
    }
}
