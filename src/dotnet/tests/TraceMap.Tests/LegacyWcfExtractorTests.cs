using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class LegacyWcfExtractorTests
{
    [Fact]
    public void Scan_extracts_wcf_config_contract_host_client_and_mapping_without_raw_address()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        var output = Path.Combine(temp.Path, "out");
        Directory.CreateDirectory(repo);
        Directory.CreateDirectory(Path.Combine(repo, "Service References", "Rating"));

        File.WriteAllText(Path.Combine(repo, "App.config"), """
            <?xml version="1.0" encoding="utf-8" ?>
            <configuration>
              <system.serviceModel>
                <client>
                  <endpoint address="https://services.example.test/Rating.svc"
                            binding="basicHttpBinding"
                            contract="Sample.Contracts.IRatingService"
                            name="RatingEndpoint" />
                </client>
              </system.serviceModel>
            </configuration>
            """);
        WriteContract(repo, "Sample.Contracts");
        WriteGeneratedClient(repo, "Sample.Contracts", "ClientBase<Sample.Contracts.IRatingService>");
        File.WriteAllText(Path.Combine(repo, "Rating.svc"), """
            <%@ ServiceHost Language="C#" Service="Sample.Services.RatingService" Factory="Sample.Hosting.CustomFactory" %>
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, output));
        var facts = result.Facts;

        Assert.Contains(facts, fact => fact.FactType == FactTypes.WcfClientEndpointDeclared);
        Assert.Contains(facts, fact => fact.FactType == FactTypes.WcfServiceContractDeclared);
        Assert.Contains(facts, fact => fact.FactType == FactTypes.WcfOperationContractDeclared);
        Assert.Contains(facts, fact => fact.FactType == FactTypes.WcfGeneratedClientDeclared);
        Assert.Contains(facts, fact => fact.FactType == FactTypes.WcfServiceHostDeclared);
        Assert.Contains(facts, fact =>
            fact.FactType == FactTypes.WcfServiceReferenceMapping
            && fact.EvidenceTier == EvidenceTiers.Tier2Structural
            && fact.ContractElement == "Rate"
            && fact.Properties.GetValueOrDefault("clientContractName") == "Sample.Contracts.IRatingService"
            && fact.Properties.GetValueOrDefault("hostCount") == "1");

        var serializedProperties = string.Join(
            "\n",
            facts.SelectMany(fact => fact.Properties.Select(pair => $"{pair.Key}={pair.Value}")));
        Assert.DoesNotContain("services.example.test", serializedProperties);
        Assert.Contains("addressHash=", serializedProperties);
        Assert.Contains("addressScheme=https", serializedProperties);
    }

    [Fact]
    public void Scan_emits_gap_for_unparseable_service_host_file()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        var output = Path.Combine(temp.Path, "out");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Broken.svc"), "not a service host directive");

        var result = ScanEngine.Scan(new ScanOptions(repo, output));

        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyWcfHost);
    }

    [Fact]
    public void Scan_emits_ambiguity_gap_without_normal_mapping_when_multiple_endpoints_match()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        var output = Path.Combine(temp.Path, "out");
        Directory.CreateDirectory(repo);
        Directory.CreateDirectory(Path.Combine(repo, "Service References", "Rating"));
        WriteContract(repo, "Sample.Contracts");
        WriteGeneratedClient(repo, "Sample.Contracts", "ClientBase<Sample.Contracts.IRatingService>");
        File.WriteAllText(Path.Combine(repo, "App.config"), """
            <configuration>
              <system.serviceModel>
                <client>
                  <endpoint address="https://one.example.test/Rating.svc" binding="basicHttpBinding" contract="Sample.Contracts.IRatingService" />
                  <endpoint address="https://two.example.test/Rating.svc" binding="basicHttpBinding" contract="Sample.Contracts.IRatingService" />
                </client>
              </system.serviceModel>
            </configuration>
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, output));

        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyWcfMapping
            && fact.Properties.GetValueOrDefault("classification") == "AmbiguousWcfServiceReferenceMapping");
        Assert.DoesNotContain(result.Facts, fact =>
            fact.FactType == FactTypes.WcfServiceReferenceMapping
            && fact.ContractElement == "Rate");
    }

    [Fact]
    public void Scan_does_not_map_contracts_by_short_name_only()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        var output = Path.Combine(temp.Path, "out");
        Directory.CreateDirectory(repo);
        Directory.CreateDirectory(Path.Combine(repo, "Service References", "Rating"));
        WriteContract(repo, "Other.Contracts");
        WriteGeneratedClient(repo, "Other.Contracts", "ClientBase<Other.Contracts.IRatingService>");
        File.WriteAllText(Path.Combine(repo, "App.config"), """
            <configuration>
              <system.serviceModel>
                <client>
                  <endpoint address="https://services.example.test/Rating.svc" binding="basicHttpBinding" contract="Sample.Contracts.IRatingService" />
                </client>
              </system.serviceModel>
            </configuration>
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, output));

        Assert.DoesNotContain(result.Facts, fact => fact.FactType == FactTypes.WcfServiceReferenceMapping);
    }

    [Fact]
    public void Scan_requires_generated_client_contract_to_match_operation_contract()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        var output = Path.Combine(temp.Path, "out");
        Directory.CreateDirectory(repo);
        Directory.CreateDirectory(Path.Combine(repo, "Service References", "Rating"));
        WriteContract(repo, "Sample.Contracts");
        WriteGeneratedClient(repo, "Other.Contracts", "ClientBase<Other.Contracts.IRatingService>");
        File.WriteAllText(Path.Combine(repo, "App.config"), """
            <configuration>
              <system.serviceModel>
                <client>
                  <endpoint address="https://services.example.test/Rating.svc" binding="basicHttpBinding" contract="Sample.Contracts.IRatingService" />
                </client>
              </system.serviceModel>
            </configuration>
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, output));

        Assert.DoesNotContain(result.Facts, fact => fact.FactType == FactTypes.WcfServiceReferenceMapping);
    }

    [Fact]
    public void Scan_disambiguates_repeated_operation_names_with_client_contract()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        var output = Path.Combine(temp.Path, "out");
        Directory.CreateDirectory(repo);
        Directory.CreateDirectory(Path.Combine(repo, "Service References", "Rating"));
        WriteContract(repo, "Sample.Contracts");
        File.WriteAllText(Path.Combine(repo, "OtherContracts.cs"), """
            using System.ServiceModel;

            namespace Other.Contracts;

            [ServiceContract]
            public interface ILookupService
            {
                [OperationContract]
                string Rate(string request);
            }
            """);
        WriteGeneratedClient(repo, "Sample.Contracts", "ClientBase<Sample.Contracts.IRatingService>");
        File.WriteAllText(Path.Combine(repo, "App.config"), """
            <configuration>
              <system.serviceModel>
                <client>
                  <endpoint address="https://services.example.test/Rating.svc" binding="basicHttpBinding" contract="Sample.Contracts.IRatingService" />
                </client>
              </system.serviceModel>
            </configuration>
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, output));
        var mappings = result.Facts
            .Where(fact => fact.FactType == FactTypes.WcfServiceReferenceMapping)
            .ToArray();

        var mapping = Assert.Single(mappings);
        Assert.Equal("Sample.Contracts.IRatingService", mapping.Properties.GetValueOrDefault("contractName"));
        Assert.DoesNotContain(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyWcfMapping
            && fact.Properties.GetValueOrDefault("classification") == "AmbiguousWcfServiceReferenceMapping");
    }

    [Fact]
    public void Scan_ignores_non_service_model_endpoint_elements()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        var output = Path.Combine(temp.Path, "out");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "App.config"), """
            <configuration>
              <client>
                <endpoint address="https://not-wcf.example.test" binding="custom" contract="Not.Wcf.IContract" />
              </client>
            </configuration>
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, output));

        Assert.DoesNotContain(result.Facts, fact => fact.FactType == FactTypes.WcfClientEndpointDeclared);
    }

    [Fact]
    public void Scan_extracts_asmx_class_attribute_as_host_service_name()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        var output = Path.Combine(temp.Path, "out");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Legacy.asmx"), """
            <%@ WebService Language="C#" Class="Sample.Services.LegacyWebService" %>
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, output));

        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.WcfServiceHostDeclared
            && fact.Properties.GetValueOrDefault("serviceName") == "Sample.Services.LegacyWebService");
    }

    [Fact]
    public void Scan_does_not_treat_generated_non_wcf_client_as_wcf_client()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        var output = Path.Combine(temp.Path, "out");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Reference.cs"), """
            using System.CodeDom.Compiler;
            using System.ServiceModel;

            [GeneratedCode("tool", "1.0")]
            public sealed class SearchClient
            {
                public void Query() { }
            }
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, output));

        Assert.DoesNotContain(result.Facts, fact => fact.FactType == FactTypes.WcfGeneratedClientDeclared);
    }

    [Fact]
    public void Scan_does_not_treat_custom_clientbase_substring_as_wcf_client()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        var output = Path.Combine(temp.Path, "out");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Reference.cs"), """
            public sealed class SearchClient : MyCustomClientBase
            {
                public void Query() { }
            }
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, output));

        Assert.DoesNotContain(result.Facts, fact => fact.FactType == FactTypes.WcfGeneratedClientDeclared);
    }

    [Fact]
    public void Scan_includes_containing_type_for_nested_generated_clients()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        var output = Path.Combine(temp.Path, "out");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Reference.cs"), """
            using System.ServiceModel;

            namespace Sample.Clients;

            public sealed class Container
            {
                public sealed class RatingServiceClient : ClientBase<Sample.Contracts.IRatingService>
                {
                    public void Rate() { }
                }
            }
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, output));

        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.WcfGeneratedClientDeclared
            && fact.TargetSymbol == "Sample.Clients.Container.RatingServiceClient");
    }

    private static void WriteContract(string repo, string contractNamespace)
    {
        File.WriteAllText(Path.Combine(repo, "Contracts.cs"), $$"""
            using System.ServiceModel;

            namespace {{contractNamespace}};

            [ServiceContract]
            public interface IRatingService
            {
                [OperationContract]
                string Rate(RatingRequest request);
            }

            public sealed class RatingRequest { }
            """);
    }

    private static void WriteGeneratedClient(string repo, string contractNamespace, string baseType)
    {
        File.WriteAllText(Path.Combine(repo, "Service References", "Rating", "Reference.cs"), $$"""
            using System.CodeDom.Compiler;
            using System.ServiceModel;

            namespace Sample.Clients;

            [GeneratedCode("svcutil", "4.0")]
            public partial class RatingServiceClient : {{baseType}}
            {
                public string Rate({{contractNamespace}}.RatingRequest request)
                {
                    return Channel.Rate(request);
                }
            }
            """);
    }
}
