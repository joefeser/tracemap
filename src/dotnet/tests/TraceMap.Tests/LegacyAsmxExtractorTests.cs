using TraceMap.Core;
using TraceMap.Reporting;

namespace TraceMap.Tests;

public sealed class LegacyAsmxExtractorTests
{
    [Fact]
    public void Scan_extracts_asmx_directive_attributes_and_inventory_kind()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        var output = Path.Combine(temp.Path, "out");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Legacy.asmx"), """


            <%@ WebService Language="C#" CodeBehind="Services/Legacy.asmx.cs" Class="Sample.Services.LegacyService" Unknown="https://example.invalid/service?credential=placeholder" %>
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, output));

        Assert.Contains(result.Inventory, item => item.RelativePath == "Legacy.asmx" && item.Kind == "AsmxServiceHost");
        var host = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.AsmxHostDeclared);
        Assert.Equal(RuleIds.LegacyAsmxHost, host.RuleId);
        Assert.Equal(EvidenceTiers.Tier2Structural, host.EvidenceTier);
        Assert.Equal(3, host.Evidence.StartLine);
        Assert.Equal("Sample.Services.LegacyService", host.Properties.GetValueOrDefault("serviceClassName"));
        Assert.Equal("Legacy.asmx.cs", host.Properties.GetValueOrDefault("codeBehindFile"));
        Assert.Equal("1", host.Properties.GetValueOrDefault("unsupportedAttributeCount"));
        Assert.DoesNotContain(result.Facts, fact => fact.FactType == FactTypes.WcfServiceHostDeclared);
        AssertNoUnsafeValues(result.Facts);
    }

    [Fact]
    public void Scan_emits_gap_for_duplicate_asmx_directive_attributes()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        var output = Path.Combine(temp.Path, "out");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Duplicate.asmx"), """
            <%@ WebService Language="C#" Class="Sample.Services.LegacyService" class="Other.Services.LegacyService" %>
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, output));

        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyAsmxHost
            && fact.Properties.GetValueOrDefault("classification") == "DuplicateAsmxDirectiveAttribute");
        Assert.DoesNotContain(result.Facts, fact => fact.FactType == FactTypes.AsmxHostDeclared);
    }

    [Fact]
    public void Scan_emits_gap_for_asmx_without_parseable_directive()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        var output = Path.Combine(temp.Path, "out");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Broken.asmx"), "not a directive");

        var result = ScanEngine.Scan(new ScanOptions(repo, output));

        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyAsmxHost
            && fact.Properties.GetValueOrDefault("classification") == "MalformedAsmxDirective");
    }

    [Fact]
    public void Scan_extracts_webservice_webmethod_and_distinct_soap_operation_attribute()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        var output = Path.Combine(temp.Path, "out");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "LegacyService.cs"), """
            using System.Web.Services;
            using System.Web.Services.Protocols;

            namespace Sample.Services;

            [WebService]
            [WebServiceBinding]
            public sealed class LegacyService
            {
                [WebMethod]
                [SoapDocumentMethod("https://example.invalid/actions/Rate")]
                public string Rate(string value) => value;
            }
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, output));

        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.AsmxServiceClassDeclared
            && fact.TargetSymbol == "Sample.Services.LegacyService");
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.AsmxOperationDeclared
            && fact.ContractElement == "Rate");
        var soap = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.AsmxSoapOperationDeclared);
        Assert.Equal("Rate", soap.Properties.GetValueOrDefault("operationName"));
        Assert.Contains("actionHash", soap.Properties.Keys);
        AssertNoUnsafeValues(result.Facts);
    }

    [Fact]
    public void Scan_extracts_generated_client_metadata_config_and_probable_mapping_without_raw_values()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        var output = Path.Combine(temp.Path, "out");
        var webReference = Path.Combine(repo, "Web References", "Rating");
        Directory.CreateDirectory(webReference);
        File.WriteAllText(Path.Combine(webReference, "Reference.cs"), """
            using System.CodeDom.Compiler;
            using System.Web.Services.Protocols;

            namespace Sample.WebReferences.Rating;

            [GeneratedCode("wsdl", "1.0")]
            public sealed class RatingSoapClient : SoapHttpClientProtocol
            {
                [SoapDocumentMethod("https://example.invalid/actions/Rate")]
                public string Rate(string value) => (string)Invoke("Rate", new object[] { value })[0];
            }
            """);
        File.WriteAllText(Path.Combine(webReference, "Rating.wsdl"), """
            <definitions xmlns="http://schemas.xmlsoap.org/wsdl/" targetNamespace="https://example.invalid/contracts">
              <portType name="RatingSoap">
                <operation name="Rate" />
              </portType>
              <binding name="RatingSoapBinding" type="tns:RatingSoap" />
              <service name="RatingService" />
            </definitions>
            """);
        File.WriteAllText(Path.Combine(webReference, "Rating.discomap"), """
            <DiscoveryClientResultsFile xmlns="urn:schemas-microsoft-com:xml-discovery" />
            """);
        File.WriteAllText(Path.Combine(repo, "App.config"), """
            <configuration>
              <appSettings>
                <add key="RatingServiceUrl" value="https://example.invalid/Rating.asmx?credential=placeholder" />
              </appSettings>
              <system.web>
                <webServices />
              </system.web>
            </configuration>
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, output));

        Assert.Contains(result.Inventory, item => item.RelativePath == "Web References/Rating/Rating.wsdl" && item.Kind == "AsmxServiceReferenceMetadata");
        Assert.Contains(result.Inventory, item => item.RelativePath == "Web References/Rating/Rating.discomap" && item.Kind == "AsmxServiceReferenceMetadata");
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.AsmxGeneratedClientDeclared);
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.AsmxClientOperationDeclared && fact.ContractElement == "Rate");
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.AsmxProxyMetadataDeclared
            && fact.Properties.GetValueOrDefault("metadataElement") == "operation"
            && fact.Properties.GetValueOrDefault("operationName") == "Rate");
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.AsmxConfigDeclared
            && fact.Properties.GetValueOrDefault("configKind") == "appSettings"
            && fact.Properties.GetValueOrDefault("valueOmitted") == "secret-like");
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.AsmxServiceReferenceMapping
            && fact.ContractElement == "Rate"
            && fact.EvidenceTier == EvidenceTiers.Tier3SyntaxOrTextual
            && fact.Properties.GetValueOrDefault("mappingKind") == "metadata-and-operation-name");
        AssertNoUnsafeValues(result.Facts);
    }

    [Fact]
    public void Scan_normalizes_generated_async_proxy_operations_to_base_operation_name()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        var output = Path.Combine(temp.Path, "out");
        var webReference = Path.Combine(repo, "Web References", "Rating");
        Directory.CreateDirectory(webReference);
        File.WriteAllText(Path.Combine(webReference, "Reference.cs"), """
            using System;
            using System.CodeDom.Compiler;
            using System.Web.Services.Protocols;

            namespace Sample.WebReferences.Rating;

            [GeneratedCode("wsdl", "1.0")]
            public sealed class RatingSoapClient : SoapHttpClientProtocol
            {
                public IAsyncResult BeginRate(string value, AsyncCallback callback, object state) => BeginInvoke("Rate", new object[] { value }, callback, state);
                public string EndRate(IAsyncResult asyncResult) => (string)EndInvoke(asyncResult)[0];
            }
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, output));
        var operations = result.Facts
            .Where(fact => fact.FactType == FactTypes.AsmxClientOperationDeclared)
            .ToArray();

        Assert.Equal(2, operations.Length);
        Assert.All(operations, fact => Assert.Equal("Rate", fact.ContractElement));
        Assert.All(operations, fact => Assert.Equal("Rate", fact.Properties.GetValueOrDefault("operationName")));
        Assert.DoesNotContain(operations, fact => fact.ContractElement is "BeginRate" or "EndRate");
    }

    [Fact]
    public void Scan_emits_gap_for_wsdl_external_imports_without_fetching()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        var output = Path.Combine(temp.Path, "out");
        var webReference = Path.Combine(repo, "Web References", "Rating");
        Directory.CreateDirectory(webReference);
        File.WriteAllText(Path.Combine(webReference, "Rating.wsdl"), """
            <definitions xmlns="http://schemas.xmlsoap.org/wsdl/">
              <import namespace="urn:rating" location="https://example.invalid/rating.wsdl" />
              <portType name="RatingSoap">
                <operation name="Rate" />
              </portType>
            </definitions>
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, output));

        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyAsmxMetadata
            && fact.Properties.GetValueOrDefault("classification") == "ExternalAsmxMetadataImport"
            && fact.Properties.GetValueOrDefault("externalImportCount") == "1");
        AssertNoUnsafeValues(result.Facts);
    }

    [Fact]
    public void Scan_leaves_svcmap_metadata_wcf_owned_when_asmx_evidence_is_nearby()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        var output = Path.Combine(temp.Path, "out");
        var serviceReference = Path.Combine(repo, "Service References", "Rating");
        Directory.CreateDirectory(serviceReference);
        File.WriteAllText(Path.Combine(serviceReference, "Reference.svcmap"), """
            <ReferenceGroup>
              <MetadataFile FileName="Rating.wsdl" />
            </ReferenceGroup>
            """);
        File.WriteAllText(Path.Combine(serviceReference, "Rating.wsdl"), """
            <definitions xmlns="http://schemas.xmlsoap.org/wsdl/">
              <portType name="RatingContract"><operation name="Rate" /></portType>
            </definitions>
            """);
        File.WriteAllText(Path.Combine(repo, "Legacy.asmx"), """
            <%@ WebService Language="C#" Class="Sample.Services.LegacyService" %>
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, output));

        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.WcfServiceReferenceMetadataDeclared);
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.WcfMetadataOperationDeclared);
        Assert.DoesNotContain(result.Facts, fact => fact.FactType == FactTypes.AsmxProxyMetadataDeclared);
    }

    [Fact]
    public void Scan_does_not_map_client_to_multiple_metadata_operations_by_name_only()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        var output = Path.Combine(temp.Path, "out");
        var webReference = Path.Combine(repo, "Web References", "Rating");
        Directory.CreateDirectory(webReference);
        File.WriteAllText(Path.Combine(webReference, "Reference.cs"), """
            using System.Web.Services.Protocols;

            namespace Sample.WebReferences.Rating;

            public sealed class RatingSoapClient : SoapHttpClientProtocol
            {
                public string GetStatus() => (string)Invoke("GetStatus", System.Array.Empty<object>())[0];
            }
            """);
        File.WriteAllText(Path.Combine(webReference, "First.wsdl"), """
            <definitions xmlns="http://schemas.xmlsoap.org/wsdl/">
              <portType name="FirstSoap"><operation name="GetStatus" /></portType>
            </definitions>
            """);
        File.WriteAllText(Path.Combine(webReference, "Second.wsdl"), """
            <definitions xmlns="http://schemas.xmlsoap.org/wsdl/">
              <portType name="SecondSoap"><operation name="GetStatus" /></portType>
            </definitions>
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, output));

        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyAsmxMapping
            && fact.Properties.GetValueOrDefault("classification") == "AmbiguousAsmxMetadataOperationMapping");
        Assert.DoesNotContain(result.Facts, fact => fact.FactType == FactTypes.AsmxServiceReferenceMapping);
    }

    [Fact]
    public void Scan_does_not_store_url_shaped_config_keys()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        var output = Path.Combine(temp.Path, "out");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "App.config"), """
            <configuration>
              <appSettings>
                <add key="https://example.invalid/RatingServiceUrl" value="placeholder" />
              </appSettings>
            </configuration>
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, output));

        Assert.DoesNotContain(result.Facts, fact =>
            fact.FactType == FactTypes.AsmxConfigDeclared
            && fact.Properties.ContainsKey("configKey"));
    }

    [Fact]
    public void Scan_ignores_generic_url_and_servicebus_app_settings_as_asmx_config()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        var output = Path.Combine(temp.Path, "out");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "App.config"), """
            <configuration>
              <appSettings>
                <add key="ApiBaseUrl" value="https://example.invalid/api" />
                <add key="ServiceBusConnection" value="Endpoint=sb://example.invalid/;SharedAccessKey=secret" />
                <add key="NotificationEndpointUrl" value="https://example.invalid/api/notify" />
                <add key="RatingServiceUrl" value="https://example.invalid/Rating.asmx" />
                <add key="RatingServiceEndpointUrl" value="https://example.invalid/Rating.asmx" />
                <add key="RatingEndpointUrl" value="https://example.invalid/Rating.asmx" />
                <add key="RatingSoapEndpoint" value="https://example.invalid/Rating.asmx" />
              </appSettings>
            </configuration>
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, output));
        var configKeys = result.Facts
            .Where(fact => fact.FactType == FactTypes.AsmxConfigDeclared)
            .Select(fact => fact.Properties.GetValueOrDefault("configKey"))
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .ToArray();

        Assert.Contains("RatingServiceUrl", configKeys);
        Assert.Contains("RatingServiceEndpointUrl", configKeys);
        Assert.Contains("RatingEndpointUrl", configKeys);
        Assert.Contains("RatingSoapEndpoint", configKeys);
        Assert.DoesNotContain("ApiBaseUrl", configKeys);
        Assert.DoesNotContain("ServiceBusConnection", configKeys);
        Assert.DoesNotContain("NotificationEndpointUrl", configKeys);
    }

    [Fact]
    public void Scan_does_not_claim_generic_source_maps_as_asmx_metadata()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        var output = Path.Combine(temp.Path, "out");
        Directory.CreateDirectory(Path.Combine(repo, "assets"));
        File.WriteAllText(Path.Combine(repo, "assets", "app.js.map"), "{}");

        var result = ScanEngine.Scan(new ScanOptions(repo, output));

        Assert.DoesNotContain(result.Inventory, item => item.RelativePath == "assets/app.js.map");
        Assert.DoesNotContain(result.Facts, fact => fact.FactType == FactTypes.AsmxProxyMetadataDeclared);
    }

    [Fact]
    public void Report_includes_asmx_static_evidence_and_limitations()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        var output = Path.Combine(temp.Path, "out");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Legacy.asmx"), """
            <%@ WebService Language="C#" Class="Sample.Services.LegacyService" %>
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, output));
        var markdown = MarkdownReportWriter.Build(result);

        Assert.Contains("Legacy ASMX/SOAP Static Evidence", markdown);
        Assert.Contains("AsmxHostDeclared", markdown);
        Assert.Contains("do not prove runtime service activation", markdown);
    }

    private static void AssertNoUnsafeValues(IEnumerable<CodeFact> facts)
    {
        var serialized = string.Join(
            "\n",
            facts.SelectMany(fact => fact.Properties.Select(pair => $"{pair.Key}={pair.Value}"))
                .Concat(facts.Select(fact => fact.TargetSymbol ?? string.Empty))
                .Concat(facts.Select(fact => fact.SourceSymbol ?? string.Empty)));
        Assert.DoesNotContain("example.invalid", serialized);
        Assert.DoesNotContain("credential=placeholder", serialized);
    }
}
