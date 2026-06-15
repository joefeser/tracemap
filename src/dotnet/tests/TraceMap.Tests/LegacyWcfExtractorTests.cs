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

    [Fact]
    public void Scan_extracts_svcmap_metadata_without_raw_url_or_absolute_path()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        var output = Path.Combine(temp.Path, "out");
        var serviceRef = Path.Combine(repo, "Service References", "Rating");
        Directory.CreateDirectory(serviceRef);
        File.WriteAllText(Path.Combine(serviceRef, "Reference.svcmap"), """
            <ReferenceGroup>
              <MetadataSource Address="https://services.example.test/Rating.svc?wsdl" SourceId="C:\Users\operator\Rating.wsdl" />
              <MetadataFile FileName="Rating.wsdl" />
              <MetadataFile FileName="schema.xsd" />
              <GeneratedFile FileName="C:\Users\operator\Reference.cs" />
            </ReferenceGroup>
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, output));
        var metadata = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.WcfServiceReferenceMetadataDeclared);

        Assert.Equal("SvcMap", metadata.Properties.GetValueOrDefault("metadataKind"));
        Assert.Equal("Reference.cs", metadata.Properties.GetValueOrDefault("generatedCodeFileName"));
        Assert.Equal("Rating.wsdl;schema.xsd", metadata.Properties.GetValueOrDefault("localMetadataFileNames"));
        var serialized = SerializeProperties(result.Facts);
        Assert.DoesNotContain("services.example.test", serialized);
        Assert.DoesNotContain("/private", serialized);
        Assert.DoesNotContain("Users", serialized);
        Assert.Contains("metadataSourceHash=", serialized);
    }

    [Fact]
    public void Scan_extracts_wsdl_operations_without_raw_namespace_or_soap_action()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        var output = Path.Combine(temp.Path, "out");
        var serviceRef = Path.Combine(repo, "Service References", "Rating");
        Directory.CreateDirectory(serviceRef);
        WriteWsdl(serviceRef, "IRatingService", "Rate", targetNamespace: "https://secret.example.test/contracts");

        var result = ScanEngine.Scan(new ScanOptions(repo, output));
        var operation = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.WcfMetadataOperationDeclared);

        Assert.Equal("Rate", operation.Properties.GetValueOrDefault("operationName"));
        Assert.Equal("IRatingService", operation.Properties.GetValueOrDefault("portTypeName"));
        Assert.Equal("wsdl", operation.Properties.GetValueOrDefault("sourceFormat"));
        var metadata = Assert.Single(result.Facts, fact =>
            fact.FactType == FactTypes.WcfServiceReferenceMetadataDeclared
            && fact.Properties.GetValueOrDefault("metadataKind") == "Wsdl");
        Assert.False(metadata.Properties.ContainsKey("metadataSourceHash"));
        var serialized = SerializeProperties(result.Facts);
        Assert.DoesNotContain("secret.example.test", serialized);
        Assert.DoesNotContain("soapAction", serialized);
    }

    [Fact]
    public void Scan_rejects_dtd_metadata_and_emits_malformed_wcf_metadata_gap()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        var output = Path.Combine(temp.Path, "out");
        var serviceRef = Path.Combine(repo, "Service References", "Rating");
        Directory.CreateDirectory(serviceRef);
        File.WriteAllText(Path.Combine(serviceRef, "Rating.wsdl"), """
            <!DOCTYPE definitions [ <!ENTITY xxe SYSTEM "file:///etc/passwd"> ]>
            <definitions>&xxe;</definitions>
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, output));

        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyWcfMetadata
            && fact.Properties.GetValueOrDefault("classification") == "MalformedWcfMetadata");
    }

    [Fact]
    public void Extract_emits_malformed_metadata_gap_when_metadata_file_disappears_after_inventory()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(Path.Combine(repo, "Service References", "Rating"));

        var facts = LegacyWcfExtractor.Extract(
            repo,
            Manifest(),
            [new FileInventoryItem("Service References/Rating/Missing.svcmap", "ServiceReferenceMetadata", 0)]);

        Assert.Contains(facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyWcfMetadata
            && fact.Properties.GetValueOrDefault("classification") == "MalformedWcfMetadata");
    }

    [Fact]
    public void Scan_gates_wsdl_disco_and_xsd_to_service_reference_folders()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        var output = Path.Combine(temp.Path, "out");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Loose.wsdl"), "<definitions />");
        File.WriteAllText(Path.Combine(repo, "Loose.disco"), "<discovery />");
        File.WriteAllText(Path.Combine(repo, "Loose.xsd"), "<schema />");
        var serviceRef = Path.Combine(repo, "Client", "Rating");
        Directory.CreateDirectory(serviceRef);
        File.WriteAllText(Path.Combine(serviceRef, "Reference.svcmap"), "<ReferenceGroup />");
        File.WriteAllText(Path.Combine(serviceRef, "Rating.wsdl"), "<definitions />");
        File.WriteAllText(Path.Combine(serviceRef, "Rating.disco"), "<discovery />");
        File.WriteAllText(Path.Combine(serviceRef, "Rating.xsd"), "<schema />");

        var result = ScanEngine.Scan(new ScanOptions(repo, output));

        Assert.DoesNotContain(result.Inventory, item => item.RelativePath is "Loose.wsdl" or "Loose.disco");
        Assert.Contains(result.Inventory, item => item.RelativePath == "Loose.xsd" && item.Kind == "XsdSchema");
        Assert.Contains(result.Inventory, item => item.RelativePath == "Client/Rating/Rating.wsdl" && item.Kind == "ServiceReferenceMetadata");
        Assert.Contains(result.Inventory, item => item.RelativePath == "Client/Rating/Rating.disco" && item.Kind == "ServiceReferenceMetadata");
        Assert.Contains(result.Inventory, item => item.RelativePath == "Client/Rating/Rating.xsd" && item.Kind == "ServiceReferenceMetadata");
    }

    [Fact]
    public void Scan_maps_async_suffix_when_corrobated_by_connected_wsdl_metadata()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        var output = Path.Combine(temp.Path, "out");
        Directory.CreateDirectory(repo);
        WriteContract(repo, "Sample.Contracts", "Rate");
        WriteGeneratedClient(repo, "Sample.Contracts", "ClientBase<Sample.Contracts.IRatingService>", "RateAsync");
        WriteClientEndpoint(repo);
        WriteWsdl(Path.Combine(repo, "Service References", "Rating"), "IRatingService", "Rate");

        var result = ScanEngine.Scan(new ScanOptions(repo, output));
        var mapping = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.WcfServiceReferenceMapping);

        Assert.Equal("Rate", mapping.ContractElement);
        Assert.Equal("AsyncSuffix", mapping.Properties.GetValueOrDefault("normalizationKind"));
        Assert.Equal("RateAsync", mapping.Properties.GetValueOrDefault("originalOperationName"));
    }

    [Fact]
    public void Scan_maps_begin_end_pair_but_not_lone_begin_or_lifecycle_pairs()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        var output = Path.Combine(temp.Path, "out");
        Directory.CreateDirectory(repo);
        WriteContract(repo, "Sample.Contracts", "BeginRate", "EndRate", "BeginLookup", "BeginOpen", "EndOpen", "Close");
        WriteGeneratedClient(repo, "Sample.Contracts", "ClientBase<Sample.Contracts.IRatingService>", "BeginRate", "EndRate", "BeginLookup", "BeginOpen", "EndOpen", "CloseAsync");
        WriteClientEndpoint(repo);

        var result = ScanEngine.Scan(new ScanOptions(repo, output));
        var mappings = result.Facts.Where(fact => fact.FactType == FactTypes.WcfServiceReferenceMapping).ToArray();

        Assert.Contains(mappings, fact =>
            fact.ContractElement == "Rate"
            && fact.Properties.GetValueOrDefault("normalizationKind") == "ApmBeginEndPair");
        Assert.DoesNotContain(mappings, fact => fact.ContractElement == "Lookup");
        Assert.DoesNotContain(mappings, fact => fact.ContractElement == "Open");
        Assert.DoesNotContain(mappings, fact => fact.ContractElement == "Close");
    }

    [Fact]
    public void Scan_collapses_convergent_sync_and_async_aliases_to_one_mapping()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        var output = Path.Combine(temp.Path, "out");
        Directory.CreateDirectory(repo);
        WriteContract(repo, "Sample.Contracts", "Rate");
        WriteGeneratedClient(repo, "Sample.Contracts", "ClientBase<Sample.Contracts.IRatingService>", "Rate", "RateAsync");
        WriteClientEndpoint(repo);
        WriteWsdl(Path.Combine(repo, "Service References", "Rating"), "IRatingService", "Rate");

        var result = ScanEngine.Scan(new ScanOptions(repo, output));
        var mappings = result.Facts.Where(fact => fact.FactType == FactTypes.WcfServiceReferenceMapping && fact.ContractElement == "Rate").ToArray();

        Assert.Single(mappings);
    }

    [Fact]
    public void Scan_emits_unlinked_metadata_gap_for_unrelated_wsdl_operation()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        var output = Path.Combine(temp.Path, "out");
        Directory.CreateDirectory(repo);
        WriteGeneratedClient(repo, "Sample.Contracts", "ClientBase<Sample.Contracts.IRatingService>", "RateAsync");
        WriteWsdl(Path.Combine(repo, "Service References", "Other"), "IOtherService", "Rate");

        var result = ScanEngine.Scan(new ScanOptions(repo, output));

        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyWcfOperationNormalization
            && fact.Properties.GetValueOrDefault("classification") == "UnlinkedWcfMetadata");
        Assert.DoesNotContain(result.Facts, fact => fact.FactType == FactTypes.WcfServiceReferenceMapping);
    }

    [Fact]
    public void Scan_emits_normalized_ambiguity_gap_without_mapping_when_endpoints_are_ambiguous()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        var output = Path.Combine(temp.Path, "out");
        Directory.CreateDirectory(repo);
        WriteContract(repo, "Sample.Contracts", "Rate");
        WriteGeneratedClient(repo, "Sample.Contracts", "ClientBase<Sample.Contracts.IRatingService>", "RateAsync");
        WriteWsdl(Path.Combine(repo, "Service References", "Rating"), "IRatingService", "Rate");
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
            && fact.Properties.GetValueOrDefault("classification") == "AmbiguousWcfNormalizedMapping");
        Assert.DoesNotContain(result.Facts, fact => fact.FactType == FactTypes.WcfServiceReferenceMapping);
    }

    [Fact]
    public void Scan_emits_metadata_ambiguity_when_duplicate_connected_wsdl_identities_match_normalized_operation()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        var output = Path.Combine(temp.Path, "out");
        var serviceRef = Path.Combine(repo, "Service References", "Rating");
        Directory.CreateDirectory(repo);
        WriteContract(repo, "Sample.Contracts", "Rate");
        WriteGeneratedClient(repo, "Sample.Contracts", "ClientBase<Sample.Contracts.IRatingService>", "RateAsync");
        WriteClientEndpoint(repo);
        WriteWsdl(serviceRef, "IRatingService", "Rate");
        WriteWsdl(serviceRef, "IRatingService", "Rate", fileName: "Rating.Copy.wsdl", targetNamespace: "urn:copy");

        var result = ScanEngine.Scan(new ScanOptions(repo, output));

        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyWcfMapping
            && fact.Properties.GetValueOrDefault("classification") == "AmbiguousWcfMetadataContractMapping"
            && fact.Properties.GetValueOrDefault("metadataHashCount") == "2");
        Assert.DoesNotContain(result.Facts, fact => fact.FactType == FactTypes.WcfServiceReferenceMapping);
    }

    private static void WriteContract(string repo, string contractNamespace)
    {
        WriteContract(repo, contractNamespace, "Rate");
    }

    private static void WriteContract(string repo, string contractNamespace, params string[] operations)
    {
        var operationDeclarations = string.Join(
            Environment.NewLine,
            operations.Select(operation => $"    [OperationContract]{Environment.NewLine}    string {operation}(RatingRequest request);"));
        File.WriteAllText(Path.Combine(repo, "Contracts.cs"), $$"""
            using System.ServiceModel;

            namespace {{contractNamespace}};

            [ServiceContract]
            public interface IRatingService
            {
            {{operationDeclarations}}
            }

            public sealed class RatingRequest { }
            """);
    }

    private static void WriteGeneratedClient(string repo, string contractNamespace, string baseType)
    {
        WriteGeneratedClient(repo, contractNamespace, baseType, "Rate");
    }

    private static void WriteGeneratedClient(string repo, string contractNamespace, string baseType, params string[] operations)
    {
        var serviceRef = Path.Combine(repo, "Service References", "Rating");
        Directory.CreateDirectory(serviceRef);
        var methodDeclarations = string.Join(
            Environment.NewLine,
            operations.Select(operation => $$"""
                public string {{operation}}({{contractNamespace}}.RatingRequest request)
                {
                    return string.Empty;
                }
            """));
        File.WriteAllText(Path.Combine(serviceRef, "Reference.cs"), $$"""
            using System.CodeDom.Compiler;
            using System.ServiceModel;

            namespace Sample.Clients;

            [GeneratedCode("svcutil", "4.0")]
            public partial class RatingServiceClient : {{baseType}}
            {
            {{methodDeclarations}}
            }
            """);
    }

    private static void WriteClientEndpoint(string repo)
    {
        File.WriteAllText(Path.Combine(repo, "App.config"), """
            <configuration>
              <system.serviceModel>
                <client>
                  <endpoint address="https://services.example.test/Rating.svc" binding="basicHttpBinding" contract="Sample.Contracts.IRatingService" />
                </client>
              </system.serviceModel>
            </configuration>
            """);
    }

    private static void WriteWsdl(string serviceRef, string portTypeName, string operationName, string targetNamespace = "urn:safe")
    {
        WriteWsdl(serviceRef, portTypeName, operationName, "Rating.wsdl", targetNamespace);
    }

    private static void WriteWsdl(string serviceRef, string portTypeName, string operationName, string fileName, string targetNamespace = "urn:safe")
    {
        Directory.CreateDirectory(serviceRef);
        File.WriteAllText(Path.Combine(serviceRef, fileName), $$"""
            <definitions xmlns="http://schemas.xmlsoap.org/wsdl/"
                         xmlns:soap="http://schemas.xmlsoap.org/wsdl/soap/"
                         targetNamespace="{{targetNamespace}}">
              <portType name="{{portTypeName}}">
                <operation name="{{operationName}}">
                  <input message="tns:{{operationName}}" />
                </operation>
              </portType>
              <binding name="RatingBinding" type="tns:{{portTypeName}}">
                <operation name="{{operationName}}">
                  <soap:operation soapAction="https://secret.example.test/actions/{{operationName}}" />
                </operation>
              </binding>
              <service name="RatingService">
                <port name="RatingPort" binding="tns:RatingBinding">
                  <soap:address location="https://secret.example.test/Rating.svc" />
                </port>
              </service>
            </definitions>
            """);
    }

    private static ScanManifest Manifest()
    {
        return new ScanManifest(
            "scan-test",
            "repo",
            null,
            null,
            "abc123",
            ScannerVersions.TraceMap,
            DateTimeOffset.UnixEpoch,
            "Level3SyntaxAnalysis",
            "NotRun",
            [],
            [],
            [],
            []);
    }

    private static string SerializeProperties(IEnumerable<CodeFact> facts)
    {
        return string.Join(
            "\n",
            facts.SelectMany(fact => fact.Properties.Select(pair => $"{pair.Key}={pair.Value}")));
    }
}
