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

            public interface IProfileReporter
            {
                int Measure(CustomerProfile profile);
            }

            public abstract class ReporterBase
            {
                public virtual int Score(CustomerProfile profile)
                {
                    return 0;
                }
            }

            public sealed class ProfileReporter : ReporterBase, IProfileReporter
            {
                private readonly CustomerProfile seed = new CustomerProfile();
                private CustomerProfile cached = new CustomerProfile();

                public int Measure(CustomerProfile profile)
                {
                    var observed = profile;
                    cached = observed;
                    var copy = new CustomerProfile();
                    var label = Count(profile.PrimaryEmail);
                    return Count(cached, copy) + label;
                }

                private int Count(CustomerProfile source, CustomerProfile other)
                {
                    return source.PrimaryEmail.Trim().Length + other.PrimaryEmail.Length + seed.PrimaryEmail.Length;
                }

                private int Count(string source)
                {
                    return source.Length;
                }

                public override int Score(CustomerProfile profile)
                {
                    return profile.PrimaryEmail.Length;
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
            fact.FactType == FactTypes.TypeDeclared
            && fact.TargetSymbol == "global::ModernSample.CustomerProfile"
            && fact.Properties.TryGetValue("targetSymbolId", out var symbolId)
            && symbolId.StartsWith("csharp type ", StringComparison.Ordinal)
            && fact.Properties.TryGetValue("targetSymbolKind", out var symbolKind)
            && symbolKind == "NamedType");
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.PropertyAccessed
            && fact.RuleId == RuleIds.CSharpSemanticPropertyAccess
            && fact.EvidenceTier == EvidenceTiers.Tier1Semantic
            && fact.TargetSymbol == "global::ModernSample.CustomerProfile.PrimaryEmail"
            && fact.ContractElement == "PrimaryEmail"
            && fact.Properties.ContainsKey("sourceSymbolId")
            && fact.Properties.ContainsKey("targetSymbolId"));
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.FieldDeclared
            && fact.RuleId == RuleIds.CSharpSemanticDeclarations
            && fact.EvidenceTier == EvidenceTiers.Tier1Semantic
            && fact.ContractElement == "seed"
            && fact.TargetSymbol is not null
            && fact.TargetSymbol.Contains("ProfileReporter.seed", StringComparison.Ordinal));
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.ParameterDeclared
            && fact.RuleId == RuleIds.CSharpSemanticDeclarations
            && fact.EvidenceTier == EvidenceTiers.Tier1Semantic
            && fact.ContractElement == "profile"
            && fact.SourceSymbol is not null
            && fact.SourceSymbol.Contains("ProfileReporter.Measure", StringComparison.Ordinal));
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.LocalAlias
            && fact.RuleId == RuleIds.CSharpSemanticLocalAlias
            && fact.EvidenceTier == EvidenceTiers.Tier1Semantic
            && fact.ContractElement == "observed"
            && fact.Properties.TryGetValue("originSymbolKind", out var originSymbolKind)
            && originSymbolKind == "Parameter"
            && fact.Properties.TryGetValue("originSymbol", out var originSymbol)
            && originSymbol.Contains("CustomerProfile profile", StringComparison.Ordinal));
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.FieldAlias
            && fact.RuleId == RuleIds.CSharpSemanticFieldAlias
            && fact.EvidenceTier == EvidenceTiers.Tier1Semantic
            && fact.ContractElement == "cached"
            && fact.Properties.TryGetValue("originSymbolKind", out var fieldOriginSymbolKind)
            && fieldOriginSymbolKind == "Local"
            && fact.Properties.TryGetValue("originSymbol", out var fieldOriginSymbol)
            && fieldOriginSymbol == "observed");
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.MethodInvoked
            && fact.RuleId == RuleIds.CSharpSemanticMethodInvocation
            && fact.EvidenceTier == EvidenceTiers.Tier1Semantic
            && fact.ContractElement == "Trim"
            && fact.TargetSymbol is not null
            && fact.TargetSymbol.Contains("string.Trim", StringComparison.Ordinal));
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.CallEdge
            && fact.RuleId == RuleIds.CSharpSemanticCallGraph
            && fact.EvidenceTier == EvidenceTiers.Tier1Semantic
            && fact.SourceSymbol is not null
            && fact.SourceSymbol.Contains("ProfileReporter.Count", StringComparison.Ordinal)
            && fact.TargetSymbol is not null
            && fact.TargetSymbol.Contains("string.Trim", StringComparison.Ordinal));
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.ObjectCreated
            && fact.RuleId == RuleIds.CSharpSemanticObjectCreation
            && fact.EvidenceTier == EvidenceTiers.Tier1Semantic
            && fact.TargetSymbol == "global::ModernSample.CustomerProfile"
            && fact.Properties.TryGetValue("callerAssemblyName", out var callerAssembly)
            && callerAssembly == "ModernSample"
            && fact.Properties.TryGetValue("calleeAssemblyName", out var calleeAssembly)
            && calleeAssembly == "ModernSample"
            && fact.Properties.TryGetValue("assignedTo", out var assignedTo)
            && assignedTo == "copy");
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.ArgumentPassed
            && fact.RuleId == RuleIds.CSharpSemanticValueFlow
            && fact.EvidenceTier == EvidenceTiers.Tier1Semantic
            && fact.TargetSymbol is not null
            && fact.TargetSymbol.Contains("ProfileReporter.Count", StringComparison.Ordinal)
            && fact.Properties.TryGetValue("parameterName", out var parameterName)
            && parameterName == "source"
            && fact.Properties.TryGetValue("parameterType", out var parameterType)
            && parameterType == "global::ModernSample.CustomerProfile"
            && fact.Properties.TryGetValue("argumentSymbolKind", out var argumentSymbolKind)
            && argumentSymbolKind == "Field"
            && fact.Properties.TryGetValue("argumentSourceFile", out var argumentSourceFile)
            && argumentSourceFile == "src/ModernSample/CustomerProfile.cs");
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.ArgumentPassed
            && fact.Properties.ContainsKey("sourceSymbolId")
            && fact.Properties.ContainsKey("targetSymbolId")
            && fact.Properties.ContainsKey("parameterSymbolId")
            && fact.Properties.ContainsKey("argumentSymbolId"));

        var countCallTargetIds = result.Facts
            .Where(fact => fact.FactType == FactTypes.CallEdge && fact.ContractElement == "Count")
            .Select(fact => fact.Properties.TryGetValue("targetSymbolId", out var symbolId) ? symbolId : string.Empty)
            .Where(symbolId => !string.IsNullOrWhiteSpace(symbolId))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(2, countCallTargetIds.Length);
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.SymbolRelationship
            && fact.RuleId == RuleIds.CSharpSemanticSymbolRelationship
            && fact.ContractElement == "InheritsFrom"
            && fact.SourceSymbol == "global::ModernSample.ProfileReporter"
            && fact.TargetSymbol == "global::ModernSample.ReporterBase"
            && fact.Properties.ContainsKey("sourceSymbolId")
            && fact.Properties.ContainsKey("targetSymbolId"));
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.SymbolRelationship
            && fact.ContractElement == "ImplementsInterface"
            && fact.TargetSymbol == "global::ModernSample.IProfileReporter");
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.SymbolRelationship
            && fact.ContractElement == "Overrides"
            && fact.SourceSymbol is not null
            && fact.SourceSymbol.Contains("ProfileReporter.Score", StringComparison.Ordinal)
            && fact.TargetSymbol is not null
            && fact.TargetSymbol.Contains("ReporterBase.Score", StringComparison.Ordinal));
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.SymbolRelationship
            && fact.ContractElement == "ImplementsInterfaceMember"
            && fact.SourceSymbol is not null
            && fact.SourceSymbol.Contains("ProfileReporter.Measure", StringComparison.Ordinal)
            && fact.TargetSymbol is not null
            && fact.TargetSymbol.Contains("IProfileReporter.Measure", StringComparison.Ordinal));
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
            && diagnosticId == "CS0246"
            && fact.Properties.TryGetValue("diagnosticTokens", out var tokens)
            && tokens.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Contains("MissingContract"));
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.InvocationName
            && fact.RuleId == RuleIds.CSharpSyntaxInvocation
            && fact.EvidenceTier == EvidenceTiers.Tier3SyntaxOrTextual
            && fact.TargetSymbol == "Deliver");
    }

    [Fact]
    public void Scan_extracts_tier1_flow_boundary_facts()
    {
        using var temp = new TempDirectory();
        Directory.CreateDirectory(Path.Combine(temp.Path, "src", "BoundarySample"));
        File.WriteAllText(Path.Combine(temp.Path, "src", "BoundarySample", "BoundarySample.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(temp.Path, "src", "BoundarySample", "Boundary.cs"), """
            using System;
            using System.Collections.Generic;
            using System.Runtime.Serialization;
            using System.Text.Json;
            using System.Text.Json.Serialization;

            namespace BoundarySample;

            public interface IWorker
            {
            }

            public sealed class Worker : IWorker
            {
            }

            public sealed class ServiceCollection
            {
                public void AddSingleton<TService, TImplementation>()
                {
                }

                public void AddTransient(Type serviceType, Type implementationType)
                {
                }
            }

            public sealed class RequestDto
            {
                [JsonPropertyName("customer_name")]
                public string Name { get; set; } = "";

                [DataMember(Name = "customer_age")]
                public int Age { get; set; }
            }

            public sealed class FlowBoundaryDemo
            {
                private RequestDto? current;

                public void Handle(IServiceProvider services, string json, dynamic connection)
                {
                    var request = JsonSerializer.Deserialize<RequestDto>(json);
                    var demo = services.GetService(typeof(FlowBoundaryDemo));
                    var registrations = new ServiceCollection();
                    registrations.AddSingleton<IWorker, Worker>();
                    registrations.AddTransient(typeof(FlowBoundaryDemo), typeof(FlowBoundaryDemo));
                    var list = new List<RequestDto>();
                    if (true)
                    {
                        current = current;
                    }

                    if (request != null)
                    {
                        current = request;
                        request.Age += 1;
                        list.Add(request);
                    }

                    var method = typeof(FlowBoundaryDemo).GetMethod(nameof(Handle));
                    method?.Invoke(this, new object[] { services, json, connection });
                    connection.Query<RequestDto>("select * from Requests");
                }
            }
            """);

        var result = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, ".tracemap")));

        Assert.Equal("Level1SemanticAnalysis", result.Manifest.AnalysisLevel);
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.DeserializedObject && fact.RuleId == RuleIds.CSharpSemanticFlowBoundary);
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.DependencyResolved && fact.RuleId == RuleIds.CSharpSemanticFlowBoundary);
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.CollectionMutation && fact.RuleId == RuleIds.CSharpSemanticFlowBoundary);
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.ObjectMutation && fact.RuleId == RuleIds.CSharpSemanticFlowBoundary);
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.ReflectionUsage && fact.RuleId == RuleIds.CSharpSemanticFlowBoundary);
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.DynamicInvocation && fact.RuleId == RuleIds.CSharpSemanticFlowBoundary);
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.BranchCondition && fact.RuleId == RuleIds.CSharpSemanticFlowBoundary);
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.DependencyRegistered && fact.RuleId == RuleIds.CSharpSemanticRuntimeEvidence);
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.SerializerContractMember && fact.RuleId == RuleIds.CSharpSemanticRuntimeEvidence && fact.ContractElement == "customer_name");
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.ReflectionTarget && fact.RuleId == RuleIds.CSharpSemanticRuntimeEvidence && fact.ContractElement == "Handle");
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.DynamicDispatchCandidate && fact.RuleId == RuleIds.CSharpSemanticRuntimeEvidence && fact.ContractElement == "Query");
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.CollectionElementFlow && fact.RuleId == RuleIds.CSharpSemanticRuntimeEvidence);
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.MutationSemantics && fact.RuleId == RuleIds.CSharpSemanticRuntimeEvidence);
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.BranchFeasibility && fact.RuleId == RuleIds.CSharpSemanticRuntimeEvidence);
    }

    [Fact]
    public void Scan_extracts_tier1_contract_mapping_facts()
    {
        using var temp = new TempDirectory();
        Directory.CreateDirectory(Path.Combine(temp.Path, "src", "MappingSample"));
        File.WriteAllText(Path.Combine(temp.Path, "src", "MappingSample", "MappingSample.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(temp.Path, "src", "MappingSample", "Mappings.cs"), """
            using System;

            namespace MappingSample;

            [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
            public sealed class RouteAttribute(string template) : Attribute { }
            public sealed class HttpPostAttribute(string template) : Attribute { }
            public sealed class FromBodyAttribute : Attribute { }
            public sealed class TableAttribute(string name) : Attribute { }
            public sealed class ColumnAttribute(string name) : Attribute { }

            public sealed class Configuration
            {
                public Configuration GetSection(string name) => this;
            }

            public static class ConfigurationExtensions
            {
                public static T? Get<T>(this Configuration configuration) => default;
                public static void Bind(this Configuration configuration, object target) { }
            }

            public sealed class CustomerOptions { }

            [Table("customer_profiles")]
            public sealed class CustomerProfile
            {
                [Column("primary_email")]
                public string PrimaryEmail { get; set; } = "";
            }

            [Route("api/customers")]
            public sealed class CustomerController
            {
                [HttpPost("{id}")]
                public void Update([FromBody] CustomerProfile profile)
                {
                    var options = new CustomerOptions();
                    new Configuration().GetSection("Customers").Bind(options);
                    _ = new Configuration().GetSection("CustomerDefaults").Get<CustomerOptions>();
                }
            }
            """);

        var result = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, ".tracemap")));

        Assert.Equal("Level1SemanticAnalysis", result.Manifest.AnalysisLevel);
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.HttpRouteBinding
            && fact.RuleId == RuleIds.CSharpSemanticContractMapping
            && fact.Properties.TryGetValue("routeTemplates", out var routes)
            && routes.Contains("api/customers", StringComparison.Ordinal)
            && routes.Contains("{id}", StringComparison.Ordinal)
            && fact.Properties.TryGetValue("bodyParameterTypes", out var bodyTypes)
            && bodyTypes.Contains("CustomerProfile", StringComparison.Ordinal));
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.DatabaseColumnMapping
            && fact.ContractElement == "primary_email"
            && fact.RuleId == RuleIds.CSharpSemanticContractMapping);
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.DatabaseColumnMapping
            && fact.ContractElement == "customer_profiles"
            && fact.RuleId == RuleIds.CSharpSemanticContractMapping);
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.ConfigBinding
            && fact.ContractElement == "Customers"
            && fact.RuleId == RuleIds.CSharpSemanticContractMapping);
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.ConfigBinding
            && fact.ContractElement == "CustomerDefaults"
            && fact.RuleId == RuleIds.CSharpSemanticContractMapping);
    }
}
