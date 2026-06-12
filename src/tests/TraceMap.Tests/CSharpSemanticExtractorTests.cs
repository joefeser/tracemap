using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class CSharpSemanticExtractorTests
{
    [Fact]
    public void Scan_extracts_tier1_semantic_facts_from_compiling_project()
    {
        using var temp = new TempDirectory();
        Directory.CreateDirectory(Path.Combine(temp.Path, "src", "ModernSample"));
        File.WriteAllText(Path.Combine(temp.Path, "src", "ModernSample", "ModernSample.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(temp.Path, "src", "ModernSample", "CustomerProfile.cs"), """
            namespace ModernSample;

            public sealed class CustomerProfile
            {
                public string PrimaryEmail { get; init; } = "";
            }

            public sealed class ProfileReporter
            {
                public int Measure(CustomerProfile profile)
                {
                    return profile.PrimaryEmail.Trim().Length;
                }
            }
            """);

        var result = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, ".tracemap")));

        Assert.Equal("Level1SemanticAnalysis", result.Manifest.AnalysisLevel);
        Assert.Equal("Succeeded", result.Manifest.BuildStatus);
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.TypeDeclared
            && fact.RuleId == RuleIds.CSharpSemanticDeclarations
            && fact.EvidenceTier == EvidenceTiers.Tier1Semantic
            && fact.TargetSymbol == "global::ModernSample.CustomerProfile");
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.PropertyAccessed
            && fact.RuleId == RuleIds.CSharpSemanticPropertyAccess
            && fact.EvidenceTier == EvidenceTiers.Tier1Semantic
            && fact.TargetSymbol == "global::ModernSample.CustomerProfile.PrimaryEmail"
            && fact.ContractElement == "PrimaryEmail");
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.MethodInvoked
            && fact.RuleId == RuleIds.CSharpSemanticMethodInvocation
            && fact.EvidenceTier == EvidenceTiers.Tier1Semantic
            && fact.ContractElement == "Trim"
            && fact.TargetSymbol is not null
            && fact.TargetSymbol.Contains("string.Trim", StringComparison.Ordinal));
    }

    [Fact]
    public void Scan_records_compilation_gaps_and_keeps_syntax_fallback_for_broken_project()
    {
        using var temp = new TempDirectory();
        Directory.CreateDirectory(Path.Combine(temp.Path, "src", "BrokenSample"));
        File.WriteAllText(Path.Combine(temp.Path, "src", "BrokenSample", "BrokenSample.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(temp.Path, "src", "BrokenSample", "BrokenProfile.cs"), """
            namespace BrokenSample;

            public sealed class BrokenProfile
            {
                public string PrimaryEmail { get; init; } = "";

                public void Send(MissingContract contract)
                {
                    contract.Deliver(PrimaryEmail);
                }
            }
            """);

        var result = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, ".tracemap")));

        Assert.Equal("Level1SemanticAnalysisReduced", result.Manifest.AnalysisLevel);
        Assert.Equal("FailedOrPartial", result.Manifest.BuildStatus);
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.CSharpSemanticWorkspace
            && fact.Properties.TryGetValue("diagnosticId", out var diagnosticId)
            && diagnosticId == "CS0246");
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.InvocationName
            && fact.RuleId == RuleIds.CSharpSyntaxInvocation
            && fact.EvidenceTier == EvidenceTiers.Tier3SyntaxOrTextual
            && fact.TargetSymbol == "Deliver");
    }
}
