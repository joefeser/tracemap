using Microsoft.Data.Sqlite;
using System.Text;
using TraceMap.Core;
using TraceMap.Reporting;
using TraceMap.Storage;

namespace TraceMap.Tests;

public sealed class LegacyDataMetadataExtractorTests
{
    [Fact]
    public void Scan_extracts_dbml_descriptors_and_generated_link_without_unsafe_names()
    {
        using var temp = new TempDirectory();
        var repo = CreateRepo(temp);
        File.WriteAllText(Path.Combine(repo, "Model.dbml"), """
            <Database Name="Data Source=prod;Password=secret" Class="SampleDataContext" xmlns="http://schemas.microsoft.com/linqtosql/dbml/2007">
              <Table Name="dbo.Customers" Member="Customers">
                <Type Name="Customer">
                  <Column Name="CustomerId" Member="CustomerId" Type="System.Int32" IsPrimaryKey="true" />
                  <Column Name="password_token_column" Member="Secret" Type="System.String" />
                  <Association Name="FK_Order_Customer" Member="Orders" ThisKey="CustomerId" OtherKey="CustomerId" />
                </Type>
              </Table>
              <Function Name="dbo.GetCustomers" Method="GetCustomers" />
            </Database>
            """);
        File.WriteAllText(Path.Combine(repo, "Model.designer.cs"), """
            namespace Sample;
            public partial class SampleDataContext {}
            public partial class Customer {}
            """);

        var result = Scan(repo, temp);

        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.LegacyDataMetadataDeclared && fact.Properties.GetValueOrDefault("metadataKind") == "Dbml");
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.LegacyDataStorageObjectDeclared && fact.Properties.GetValueOrDefault("tableName") == "dbo.Customers");
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.LegacyDataColumnDeclared && fact.Properties.GetValueOrDefault("columnName") == "CustomerId");
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.LegacyDataColumnDeclared && fact.Properties.ContainsKey("columnHash"));
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.LegacyDataMappingDeclared && fact.Properties.GetValueOrDefault("mappingKind") == "association");
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.LegacyDataGeneratedCodeLinked && fact.EvidenceTier == EvidenceTiers.Tier2Structural);
        Assert.All(result.Facts.Where(IsLegacyDataFact), fact => Assert.NotEqual(EvidenceTiers.Tier1Semantic, fact.EvidenceTier));
        Assert.DoesNotContain("prod-db", SerializedProperties(result));
        Assert.DoesNotContain("secret", SerializedProperties(result), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Scan_extracts_edmx_simple_mapping_and_gaps_unsupported_shapes()
    {
        using var temp = new TempDirectory();
        var repo = CreateRepo(temp);
        File.WriteAllText(Path.Combine(repo, "Model.edmx"), """
            <edmx:Edmx Version="3.0" xmlns:edmx="http://schemas.microsoft.com/ado/2009/11/edmx">
              <edmx:Runtime>
                <edmx:ConceptualModels>
                  <Schema Namespace="Sample.Model" xmlns="http://schemas.microsoft.com/ado/2009/11/edm">
                    <EntityType Name="Customer"><Property Name="Name" Type="String" /></EntityType>
                    <EntityContainer Name="Entities"><EntitySet Name="Customers" EntityType="Sample.Model.Customer" /></EntityContainer>
                  </Schema>
                </edmx:ConceptualModels>
                <edmx:StorageModels>
                  <Schema Namespace="Store" Provider="System.Data.SqlClient" ProviderManifestToken="2012" xmlns="http://schemas.microsoft.com/ado/2009/11/edm/ssdl">
                    <EntityType Name="CustomerStore"><Property Name="Name" Type="nvarchar" /></EntityType>
                    <EntityContainer Name="StoreContainer"><EntitySet Name="CustomerStore" Table="Customers" /></EntityContainer>
                  </Schema>
                </edmx:StorageModels>
                <edmx:Mappings>
                  <Mapping xmlns="http://schemas.microsoft.com/ado/2009/11/mapping/cs">
                    <EntityContainerMapping CdmEntityContainer="Entities" StorageEntityContainer="StoreContainer">
                      <EntitySetMapping Name="Customers">
                        <EntityTypeMapping TypeName="IsTypeOf(Sample.Model.Customer)">
                          <MappingFragment StoreEntitySet="CustomerStore">
                            <ScalarProperty Name="Name" ColumnName="Name" />
                            <Condition ColumnName="Discriminator" Value="Customer" />
                          </MappingFragment>
                        </EntityTypeMapping>
                      </EntitySetMapping>
                    </EntityContainerMapping>
                  </Mapping>
                </edmx:Mappings>
              </edmx:Runtime>
            </edmx:Edmx>
            """);

        var result = Scan(repo, temp);

        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.LegacyDataEntityDeclared && fact.Properties.GetValueOrDefault("entityName") == "Customer");
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.LegacyDataMappingDeclared && fact.Properties.GetValueOrDefault("mappingKind") == "property-column");
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyDataEdmx
            && fact.Properties.GetValueOrDefault("classification") == "UnsupportedEdmxMappingShape");
        Assert.DoesNotContain("http://schemas.microsoft.com", SerializedProperties(result));
    }

    [Fact]
    public void Scan_extracts_typed_dataset_tableadapter_sql_hash_and_missing_generated_gap()
    {
        using var temp = new TempDirectory();
        var repo = CreateRepo(temp);
        File.WriteAllText(Path.Combine(repo, "Orders.xsd"), """
            <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema"
                       xmlns:msdata="urn:schemas-microsoft-com:xml-msdata"
                       xmlns:msprop="urn:schemas-microsoft-com:xml-msprop">
              <xs:element name="OrdersDataSet" msdata:IsDataSet="true">
                <xs:complexType>
                  <xs:choice maxOccurs="unbounded">
                    <xs:element name="Orders" msprop:Generator_UserTableName="Orders" msprop:Generator_RowClassName="OrdersRow">
                      <xs:complexType><xs:sequence><xs:element name="OrderId" type="xs:int" /></xs:sequence></xs:complexType>
                    </xs:element>
                  </xs:choice>
                </xs:complexType>
              </xs:element>
              <DbSource Name="OrdersTableAdapter" Generator_TableAdapterName="OrdersTableAdapter" CommandText="SELECT OrderId FROM Orders WHERE Status = 'Ready'" />
            </xs:schema>
            """);

        var result = Scan(repo, temp);
        var properties = SerializedProperties(result);

        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.LegacyDataEntityDeclared && fact.Properties.GetValueOrDefault("entityKind") == "dataset");
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.SqlTextUsed && fact.RuleId == RuleIds.LegacyDataTypedDataSet && fact.Properties.ContainsKey("textHash"));
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.QueryPatternDetected && fact.Properties.GetValueOrDefault("tableName") == "Orders");
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyDataGeneratedLink
            && fact.Properties.GetValueOrDefault("classification") == "MissingGeneratedCode");
        Assert.DoesNotContain("SELECT OrderId", properties);
        Assert.DoesNotContain("Ready", properties);
    }

    [Fact]
    public void Scan_suppresses_unrelated_xsd_and_hashes_non_ascii_long_identifiers()
    {
        using var temp = new TempDirectory();
        var repo = CreateRepo(temp);
        File.WriteAllText(Path.Combine(repo, "Vendor.xsd"), """<xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema"><xs:element name="Vendor" /></xs:schema>""");
        var longName = new string('å', 140);
        File.WriteAllText(Path.Combine(repo, "DataSet.xsd"), $$"""
            <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema" xmlns:msdata="urn:schemas-microsoft-com:xml-msdata" xmlns:msprop="urn:schemas-microsoft-com:xml-msprop">
              <xs:element name="DataSet" msdata:IsDataSet="true">
                <xs:complexType><xs:choice><xs:element name="{{longName}}" msprop:Generator_UserTableName="{{longName}}"><xs:complexType><xs:sequence><xs:element name="Id" type="xs:int" /></xs:sequence></xs:complexType></xs:element></xs:choice></xs:complexType>
              </xs:element>
            </xs:schema>
            """);

        var result = Scan(repo, temp);

        Assert.DoesNotContain(result.Facts, fact => fact.Evidence.FilePath == "Vendor.xsd" && IsLegacyDataFact(fact));
        Assert.Contains(result.Inventory, item => item.RelativePath == "Vendor.xsd" && item.Kind == "XsdSchema");
        Assert.Contains(result.Facts, fact => fact.Evidence.FilePath == "DataSet.xsd" && fact.Properties.ContainsKey("storageObjectHash"));
        Assert.DoesNotContain(longName, SerializedProperties(result));
    }

    [Fact]
    public void Scan_extracts_config_provider_metadata_without_connection_values_and_external_include_gap()
    {
        using var temp = new TempDirectory();
        var repo = CreateRepo(temp);
        File.WriteAllText(Path.Combine(repo, "Web.config"), """
            <configuration>
              <connectionStrings>
                <add name="MainDb" providerName="System.Data.SqlClient" connectionString="Server=prod-db;Initial Catalog=SecretCatalog;User ID=admin;Password=topsecret" />
                <add name="ExternalDb" configSource="external.config" />
              </connectionStrings>
              <system.data>
                <DbProviderFactories>
                  <add name="SqlClient" invariant="System.Data.SqlClient" />
                </DbProviderFactories>
              </system.data>
              <entityFramework>
                <providers><provider invariantName="System.Data.SqlClient" type="Safe.Provider.Type" /></providers>
              </entityFramework>
            </configuration>
            """);

        var result = Scan(repo, temp);
        var properties = SerializedProperties(result);

        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.LegacyDataProviderConfigDeclared && fact.Properties.GetValueOrDefault("connectionName") == "MainDb");
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.LegacyDataProviderConfigDeclared && fact.Properties.ContainsKey("valueHash"));
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.LegacyDataProviderConfigDeclared && fact.Properties.GetValueOrDefault("configKind") == "entity-framework-provider");
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyDataConfig
            && fact.Properties.GetValueOrDefault("classification") == "ExternalConfigInclude");
        Assert.DoesNotContain("prod-db", properties);
        Assert.DoesNotContain("SecretCatalog", properties);
        Assert.DoesNotContain("topsecret", properties, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Scan_emits_parser_security_gap_for_dtd_without_external_fetch()
    {
        using var temp = new TempDirectory();
        var repo = CreateRepo(temp);
        var externalPath = Path.Combine(temp.Path, "should-not-be-read.txt");
        File.WriteAllText(Path.Combine(repo, "Danger.dbml"), $$"""
            <!DOCTYPE foo [ <!ENTITY xxe SYSTEM "file://{{externalPath}}"> ]>
            <Database Name="Danger">&xxe;</Database>
            """);

        var result = Scan(repo, temp);

        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyDataDbml
            && fact.Properties.GetValueOrDefault("classification") == "LegacyDataParserSecurityRejected");
        Assert.DoesNotContain(externalPath, SerializedProperties(result));
    }

    [Fact]
    public void Scan_outputs_stable_legacy_data_fact_ids_for_same_commit_and_inventory()
    {
        using var temp = new TempDirectory();
        var repo = CreateRepo(temp);
        File.WriteAllText(Path.Combine(repo, "Model.dbml"), """
            <Database Class="SampleDataContext"><Table Name="Orders" Member="Orders"><Type Name="Order"><Column Name="OrderId" Member="OrderId" /></Type></Table></Database>
            """);

        var first = Scan(repo, temp);
        var second = Scan(repo, temp);
        var firstIds = first.Facts.Where(IsLegacyDataFact).Select(fact => fact.FactId).OrderBy(id => id, StringComparer.Ordinal).ToArray();
        var secondIds = second.Facts.Where(IsLegacyDataFact).Select(fact => fact.FactId).OrderBy(id => id, StringComparer.Ordinal).ToArray();

        Assert.Equal(firstIds, secondIds);
    }

    [Fact]
    public void Scan_report_and_sqlite_include_legacy_data_without_private_values()
    {
        using var temp = new TempDirectory();
        var repo = CreateRepo(temp);
        File.WriteAllText(Path.Combine(repo, "App.config"), """
            <configuration>
              <connectionStrings>
                <add name="MainDb" providerName="System.Data.SqlClient" connectionString="Server=prod-db;Password=topsecret" />
              </connectionStrings>
            </configuration>
            """);
        var result = Scan(repo, temp);
        var report = MarkdownReportWriter.Build(result);
        var indexPath = Path.Combine(temp.Path, "index.sqlite");
        SqliteIndexWriter.Write(indexPath, result.Manifest, result.Facts);
        using var connection = new SqliteConnection($"Data Source={indexPath}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "select group_concat(properties_json, char(10)) from facts where fact_type like 'LegacyData%';";
        var sqliteProperties = command.ExecuteScalar()?.ToString() ?? string.Empty;

        Assert.Contains("Legacy Data Metadata", report);
        Assert.Contains(FactTypes.LegacyDataProviderConfigDeclared, report);
        Assert.DoesNotContain("prod-db", report);
        Assert.DoesNotContain("topsecret", report, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("prod-db", sqliteProperties);
        Assert.DoesNotContain("topsecret", sqliteProperties, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Scan_does_not_store_raw_config_xml_in_parse_gap_messages()
    {
        using var temp = new TempDirectory();
        var repo = CreateRepo(temp);
        File.WriteAllText(Path.Combine(repo, "App.config"), """
            <configuration>
              <connectionStrings>
                <add name="MainDb" connectionString="Server=prod-db;Password=topsecret">
              </connectionStrings>
            </configuration>
            """);

        var result = Scan(repo, temp);
        var properties = SerializedProperties(result);

        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.AnalysisGap && fact.Properties.GetValueOrDefault("classification") == "MalformedLegacyDataMetadata");
        Assert.DoesNotContain("prod-db", properties);
        Assert.DoesNotContain("topsecret", properties, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<add", properties);
    }

    [Fact]
    public void Scan_preserves_xml_declared_encoding_when_parsing_metadata()
    {
        using var temp = new TempDirectory();
        var repo = CreateRepo(temp);
        File.WriteAllBytes(Path.Combine(repo, "Latin1.dbml"), Encoding.Latin1.GetBytes("""
            <?xml version="1.0" encoding="iso-8859-1"?>
            <Database Class="CafeDataContext">
              <Table Name="Café" Member="Cafes">
                <Type Name="Cafe"><Column Name="Id" Member="Id" /></Type>
              </Table>
            </Database>
            """));

        var result = Scan(repo, temp);

        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.LegacyDataStorageObjectDeclared && fact.Evidence.FilePath == "Latin1.dbml");
        Assert.DoesNotContain(result.Facts, fact => fact.Evidence.FilePath == "Latin1.dbml" && fact.Properties.GetValueOrDefault("classification") == "MalformedLegacyDataMetadata");
    }

    [Fact]
    public void Scan_does_not_gate_xsd_on_generic_datatype_or_relationship_tokens()
    {
        using var temp = new TempDirectory();
        var repo = CreateRepo(temp);
        File.WriteAllText(Path.Combine(repo, "Generic.xsd"), """
            <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema">
              <xs:element name="DataType" type="xs:string" />
              <xs:element name="Relationship" type="xs:string" />
            </xs:schema>
            """);

        var result = Scan(repo, temp);

        Assert.DoesNotContain(result.Facts, fact => fact.Evidence.FilePath == "Generic.xsd" && IsLegacyDataFact(fact));
    }

    [Fact]
    public void Report_preserves_existing_legacy_data_hash_labels()
    {
        using var temp = new TempDirectory();
        var repo = CreateRepo(temp);
        File.WriteAllText(Path.Combine(repo, "Model.dbml"), """
            <Database Class="SampleDataContext">
              <Table Name="Orders" Member="Orders">
                <Type Name="Order">
                  <Column Name="password_token_column" Member="Secret" />
                </Type>
              </Table>
            </Database>
            """);

        var result = Scan(repo, temp);
        var columnHash = result.Facts
            .Where(fact => fact.FactType == FactTypes.LegacyDataColumnDeclared)
            .Select(fact => fact.Properties.GetValueOrDefault("columnHash"))
            .Single(value => !string.IsNullOrWhiteSpace(value));
        var report = MarkdownReportWriter.Build(result);

        Assert.Contains($"column-hash:{columnHash}", report);
    }

    private static bool IsLegacyDataFact(CodeFact fact)
    {
        return fact.FactType.StartsWith("LegacyData", StringComparison.Ordinal)
            || fact.RuleId.StartsWith("legacy.data.", StringComparison.Ordinal);
    }

    private static string SerializedProperties(ScanResult result)
    {
        return string.Join("\n", result.Facts.SelectMany(fact => fact.Properties.Select(pair => $"{pair.Key}={pair.Value}")));
    }

    private static ScanResult Scan(string repo, TempDirectory temp)
    {
        return ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));
    }

    private static string CreateRepo(TempDirectory temp)
    {
        var repo = Path.Combine(temp.Path, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(repo);
        return repo;
    }
}
