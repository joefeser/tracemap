using Microsoft.Data.Sqlite;
using System.Reflection;
using TraceMap.Core;
using TraceMap.Reporting;
using TraceMap.Storage;

namespace TraceMap.Tests;

public sealed class LegacyDataMetadataExtractorTests
{
    [Fact]
    public void Legacy_data_model_identity_preserves_unknown_coverage()
    {
        var properties = ApplyLegacyDataModelIdentity("Customer", "unknown");

        Assert.Equal("unknown", properties["coverageLabel"]);
    }

    [Fact]
    public void Legacy_data_model_identity_treats_null_coverage_as_unknown()
    {
        var properties = ApplyLegacyDataModelIdentity("Customer", null);

        Assert.Equal("unknown", properties["coverageLabel"]);
    }

    [Fact]
    public void Legacy_data_model_identity_uses_shared_safe_identifier_rules()
    {
        var properties = ApplyLegacyDataModelIdentity("my-entity", "full");

        Assert.False(properties.ContainsKey("displayName"));
        Assert.True(properties.ContainsKey("displayNameHash"));
        Assert.Equal("hashed-unsafe-identifier", properties["displayNameRedaction"]);
    }

    [Fact]
    public void Scan_hashes_hyphenated_legacy_data_names_with_shared_identifier_rules()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "Hyphenated.dbml"), """
            <Database Name="LegacyDb" Class="LegacyContext" xmlns="http://schemas.microsoft.com/linqtosql/dbml/2007">
              <Table Name="dbo.Customers" Member="Customers">
                <Type Name="my-entity">
                  <Column Name="CustomerId" Member="CustomerId" />
                </Type>
              </Table>
            </Database>
            """);

        var result = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, "out")));

        var entity = Assert.Single(result.Facts, fact =>
            fact.FactType == FactTypes.LegacyDataEntityDeclared
            && fact.Properties.GetValueOrDefault("modelKind") == "entity"
            && fact.Properties.GetValueOrDefault("descriptorRole") == "conceptual");
        Assert.False(entity.Properties.ContainsKey("entityName"));
        Assert.True(entity.Properties.ContainsKey("entityHash"));
        Assert.False(entity.Properties.ContainsKey("displayName"));
        Assert.True(entity.Properties.ContainsKey("displayNameHash"));
    }

    [Fact]
    public void Scan_extracts_dbml_metadata_and_generated_designer_link()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "Northwind.dbml"), """
            <Database Name="Northwind" Class="NorthwindDataContext" xmlns="http://schemas.microsoft.com/linqtosql/dbml/2007">
              <Table Name="dbo.Customers" Member="Customers">
                <Type Name="Customer">
                  <Column Name="CustomerId" Member="CustomerId" IsPrimaryKey="true" CanBeNull="false" />
                  <Association Name="FK_Customer_Order" Member="Orders" ThisKey="CustomerId" OtherKey="CustomerId" />
                </Type>
              </Table>
              <Function Name="dbo.GetCustomers" Method="GetCustomers" />
            </Database>
            """);
        File.WriteAllText(Path.Combine(temp.Path, "Northwind.designer.cs"), """
            namespace Samples;
            public partial class Customer { }
            public partial class NorthwindDataContext { }
            """);

        var result = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, "out")));

        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.LegacyDataMetadataDeclared && fact.Properties.GetValueOrDefault("metadataKind") == "Dbml");
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.LegacyDataEntityDeclared && fact.Properties.GetValueOrDefault("entityName") == "Customer");
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.LegacyDataColumnDeclared && fact.Properties.GetValueOrDefault("columnName") == "CustomerId");
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.LegacyDataMappingDeclared && fact.Properties.GetValueOrDefault("mappingKind") == "association");
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.LegacyDataStorageObjectDeclared && fact.Properties.GetValueOrDefault("mappingKind") == "routine");
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.LegacyDataGeneratedCodeLinked && fact.TargetSymbol == "Customer");
        Assert.All(
            result.Facts.Where(fact => fact.RuleId.StartsWith("legacy.data.", StringComparison.Ordinal)),
            fact => Assert.False(string.IsNullOrWhiteSpace(fact.Evidence.SnippetHash)));
        Assert.DoesNotContain(result.Facts, fact => fact.Evidence.FilePath.Contains(temp.Path, StringComparison.Ordinal));
    }

    [Fact]
    public void Scan_rejects_dtd_without_fetching_external_entities()
    {
        using var temp = new TempDirectory();
        var externalPath = Path.Combine(temp.Path, "secret.txt");
        File.WriteAllText(externalPath, "do-not-read-this");
        File.WriteAllText(Path.Combine(temp.Path, "Unsafe.dbml"), $$"""
            <!DOCTYPE foo [
              <!ENTITY ext SYSTEM "file://{{externalPath}}">
            ]>
            <Database Name="&ext;" />
            """);

        var result = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, "out")));
        var report = MarkdownReportWriter.Build(result);

        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyDataMetadataInventory
            && fact.Properties.GetValueOrDefault("classification") == "LegacyDataParserSecurityRejected");
        Assert.DoesNotContain("do-not-read-this", report);
        Assert.DoesNotContain(externalPath, report);
    }

    [Fact]
    public void Scan_reports_malformed_xml_and_invalid_utf8_as_gaps()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "Broken.dbml"), "<Database><Table></Database>");
        File.WriteAllBytes(Path.Combine(temp.Path, "Broken.edmx"), [0x3c, 0x45, 0x64, 0x6d, 0x78, 0xc3, 0x28, 0x3e]);

        var result = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, "out")));

        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.AnalysisGap
            && fact.Evidence.FilePath == "Broken.dbml"
            && fact.Properties.GetValueOrDefault("classification") == "MalformedLegacyDataMetadata");
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.AnalysisGap
            && fact.Evidence.FilePath == "Broken.edmx"
            && fact.Properties.GetValueOrDefault("classification") == "MalformedLegacyDataMetadata");
    }

    [Fact]
    public void Scan_rejects_oversized_legacy_metadata_before_hashing_document()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "Huge.dbml"), $"<Database>{new string('x', 2 * 1024 * 1024 + 1)}</Database>");

        var result = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, "out")));

        var gap = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyDataMetadataInventory
            && fact.Evidence.FilePath == "Huge.dbml");
        Assert.Equal("LegacyDataMetadataTooLarge", gap.Properties.GetValueOrDefault("classification"));
        Assert.False(string.IsNullOrWhiteSpace(gap.Evidence.SnippetHash));
    }

    [Fact]
    public void Scan_rejects_excessive_legacy_metadata_node_count()
    {
        using var temp = new TempDirectory();
        var nodes = string.Concat(Enumerable.Repeat("<Column />", 100_001));
        File.WriteAllText(Path.Combine(temp.Path, "TooManyNodes.dbml"), $"""
            <Database xmlns="http://schemas.microsoft.com/linqtosql/dbml/2007">
              <Table Name="Customers"><Type Name="Customer">{nodes}</Type></Table>
            </Database>
            """);

        var result = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, "out")));

        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyDataMetadataInventory
            && fact.Evidence.FilePath == "TooManyNodes.dbml"
            && fact.Properties.GetValueOrDefault("classification") == "LegacyDataMetadataTooLarge");
    }

    [Fact]
    public void Scan_rejects_excessive_legacy_metadata_depth()
    {
        using var temp = new TempDirectory();
        var opening = string.Concat(Enumerable.Repeat("<Level>", 140));
        var closing = string.Concat(Enumerable.Repeat("</Level>", 140));
        File.WriteAllText(Path.Combine(temp.Path, "TooDeep.dbml"), $"""
            <Database xmlns="http://schemas.microsoft.com/linqtosql/dbml/2007">
              {opening}{closing}
            </Database>
            """);

        var result = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, "out")));

        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyDataMetadataInventory
            && fact.Evidence.FilePath == "TooDeep.dbml"
            && fact.Properties.GetValueOrDefault("classification") == "LegacyDataMetadataTooLarge");
    }

    [Fact]
    public void Scan_skips_oversized_generated_designer_candidates_with_gap()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "Northwind.dbml"), """
            <Database Name="Northwind" Class="NorthwindDataContext" xmlns="http://schemas.microsoft.com/linqtosql/dbml/2007">
              <Table Name="dbo.Customers" Member="Customers"><Type Name="Customer" /></Table>
            </Database>
            """);
        File.WriteAllText(
            Path.Combine(temp.Path, "Northwind.designer.cs"),
            "namespace Samples;\npublic partial class Customer { }\n" + new string(' ', 2 * 1024 * 1024 + 1));

        var result = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, "out")));

        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyDataGeneratedLink
            && fact.Evidence.FilePath == "Northwind.designer.cs"
            && fact.Properties.GetValueOrDefault("classification") == "GeneratedDesignerTooLarge");
        Assert.DoesNotContain(result.Facts, fact => fact.FactType == FactTypes.LegacyDataGeneratedCodeLinked);
    }

    [Fact]
    public void Scan_gates_unrelated_xsd_without_legacy_data_facts()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "vendor.xsd"), """
            <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema">
              <xs:element name="Message" type="xs:string" />
            </xs:schema>
            """);

        var result = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, "out")));

        Assert.DoesNotContain(result.Facts, fact => fact.FactType.StartsWith("LegacyData", StringComparison.Ordinal));
        Assert.Contains(result.Inventory, item => item.Kind == "XsdSchema" && item.RelativePath == "vendor.xsd");
    }

    [Fact]
    public void Scan_extracts_typed_dataset_commands_as_hash_and_shape_without_raw_sql()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "Orders.xsd"), """
            <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema"
                       xmlns:msdata="urn:schemas-microsoft-com:xml-msdata"
                       xmlns:msprop="urn:schemas-microsoft-com:xml-msprop">
              <xs:element name="OrdersDataSet" msdata:IsDataSet="true" msprop:Generator_DataSetName="OrdersDataSet">
                <xs:complexType>
                  <xs:choice maxOccurs="unbounded">
                    <xs:element name="Orders" msprop:Generator_UserTableName="Orders" msprop:Generator_RowClassName="OrdersRow">
                      <xs:complexType>
                        <xs:sequence>
                          <xs:element name="OrderId" type="xs:int" />
                          <xs:element name="Status" type="xs:string" />
                        </xs:sequence>
                      </xs:complexType>
                    </xs:element>
                  </xs:choice>
                </xs:complexType>
              </xs:element>
              <xs:annotation>
                <xs:appinfo>
                  <TableAdapterCommand Name="Fill" CommandText="SELECT OrderId, Status FROM Orders WHERE ApiSecret = 'hidden'" />
                </xs:appinfo>
              </xs:annotation>
            </xs:schema>
            """);

        var result = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, "out")));
        var report = MarkdownReportWriter.Build(result);

        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.LegacyDataEntityDeclared && fact.Properties.GetValueOrDefault("dataSetName") == "OrdersDataSet");
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.LegacyDataColumnDeclared && fact.Properties.GetValueOrDefault("columnName") == "OrderId");
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.SqlTextUsed && fact.RuleId == RuleIds.LegacyDataTypedDataSet && fact.Properties.ContainsKey("textHash"));
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.QueryPatternDetected && fact.Properties.GetValueOrDefault("tableName") == "Orders");
        Assert.DoesNotContain(result.Facts, fact => fact.RuleId == RuleIds.LegacyDataTypedDataSet
            && fact.Properties.GetValueOrDefault("metadataFormat") == "tableadapter"
            && fact.Properties.GetValueOrDefault("modelRelationshipKind") == "relationship");
        Assert.DoesNotContain("ApiSecret", report);
        Assert.DoesNotContain("SELECT OrderId", report);
    }

    [Fact]
    public void Scan_extracts_simple_edmx_mapping_and_gaps_missing_sections()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "Model.edmx"), """
            <edmx:Edmx xmlns:edmx="http://schemas.microsoft.com/ado/2009/11/edmx" Version="3.0">
              <edmx:Runtime>
                <edmx:ConceptualModels>
                  <Schema xmlns="http://schemas.microsoft.com/ado/2009/11/edm" Namespace="Model">
                    <EntityContainer Name="ModelContainer"><EntitySet Name="Customers" EntityType="Model.Customer" /></EntityContainer>
                    <EntityType Name="Customer"><Property Name="CustomerId" Type="Int32" /></EntityType>
                  </Schema>
                </edmx:ConceptualModels>
                <edmx:StorageModels>
                  <Schema xmlns="http://schemas.microsoft.com/ado/2009/11/edm/ssdl" Namespace="Store">
                    <EntityContainer Name="StoreContainer"><EntitySet Name="Customers" EntityType="Store.Customers" Table="Customers" /></EntityContainer>
                    <EntityType Name="Customers"><Property Name="CustomerId" Type="int" /></EntityType>
                  </Schema>
                </edmx:StorageModels>
                <edmx:Mappings>
                  <Mapping xmlns="http://schemas.microsoft.com/ado/2009/11/mapping/cs">
                    <EntityContainerMapping StorageEntityContainer="StoreContainer" CdmEntityContainer="ModelContainer">
                      <EntitySetMapping Name="Customers">
                        <EntityTypeMapping TypeName="Model.Customer">
                          <MappingFragment StoreEntitySet="Customers">
                            <ScalarProperty Name="CustomerId" ColumnName="CustomerId" />
                          </MappingFragment>
                        </EntityTypeMapping>
                      </EntitySetMapping>
                    </EntityContainerMapping>
                  </Mapping>
                </edmx:Mappings>
              </edmx:Runtime>
            </edmx:Edmx>
            """);
        File.WriteAllText(Path.Combine(temp.Path, "MissingSections.edmx"), "<edmx:Edmx xmlns:edmx=\"http://schemas.microsoft.com/ado/2009/11/edmx\" />");

        var result = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, "out")));

        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.LegacyDataMappingDeclared
            && fact.RuleId == RuleIds.LegacyDataEdmx
            && fact.Properties.GetValueOrDefault("mappingKind") == "property-column"
            && fact.Properties.GetValueOrDefault("columnName") == "CustomerId");
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyDataEdmx
            && fact.Properties.GetValueOrDefault("classification") == "UnsupportedLegacyDataMetadataVersion");
    }

    [Fact]
    public async Task Scan_extracts_config_provider_metadata_without_storing_secrets_in_sqlite_or_report()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "Web.config"), """
            <configuration>
              <connectionStrings configSource="external.config">
                <add name="OrdersDb" providerName="System.Data.SqlClient" connectionString="Server=prod-db;Database=Orders;User ID=admin;Password=super-secret" />
              </connectionStrings>
              <system.data>
                <DbProviderFactories>
                  <add name="SqlClient" invariant="System.Data.SqlClient" type="System.Data.SqlClient.SqlClientFactory, System.Data" />
                </DbProviderFactories>
              </system.data>
            </configuration>
            """);

        var result = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, "out")));
        var report = MarkdownReportWriter.Build(result);
        var indexPath = Path.Combine(temp.Path, "out", "index.sqlite");
        SqliteIndexWriter.Write(indexPath, result.Manifest, result.Facts);
        var allProperties = await ReadAllPropertiesAsync(indexPath);

        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.LegacyDataProviderConfigDeclared && fact.Properties.GetValueOrDefault("connectionName") == "OrdersDb");
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.AnalysisGap && fact.Properties.GetValueOrDefault("classification") == "ExternalConfigInclude");
        Assert.DoesNotContain("super-secret", report);
        Assert.DoesNotContain("prod-db", report);
        Assert.DoesNotContain("super-secret", allProperties);
        Assert.DoesNotContain("prod-db", allProperties);
    }

    [Fact]
    public void Scan_hashes_unsafe_and_long_non_ascii_identifiers()
    {
        using var temp = new TempDirectory();
        var longName = new string('界', 140);
        File.WriteAllText(Path.Combine(temp.Path, "Unsafe.dbml"), $$"""
            <Database Name="Northwind" xmlns="http://schemas.microsoft.com/linqtosql/dbml/2007">
              <Table Name="{{longName}}" Member="Customers">
                <Type Name="Customer">
                  <Column Name="PasswordToken" Member="PasswordToken" />
                </Type>
              </Table>
            </Database>
            """);

        var result = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, "out")));

        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.LegacyDataStorageObjectDeclared && fact.Properties.ContainsKey("storageObjectHash"));
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.LegacyDataColumnDeclared && fact.Properties.ContainsKey("columnHash"));
        Assert.DoesNotContain(result.Facts, fact => fact.Properties.Values.Contains(longName, StringComparer.Ordinal));
    }

    [Fact]
    public void Scan_produces_stable_legacy_fact_ids_for_same_commit_and_inventory()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "Stable.dbml"), """
            <Database Name="Northwind" xmlns="http://schemas.microsoft.com/linqtosql/dbml/2007">
              <Table Name="Customers" Member="Customers"><Type Name="Customer"><Column Name="Id" Member="Id" /></Type></Table>
            </Database>
            """);

        var first = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, "out1")));
        var second = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, "out2")));

        var firstIds = first.Facts.Where(fact => fact.RuleId.StartsWith("legacy.data.", StringComparison.Ordinal)).Select(fact => fact.FactId).Order().ToArray();
        var secondIds = second.Facts.Where(fact => fact.RuleId.StartsWith("legacy.data.", StringComparison.Ordinal)).Select(fact => fact.FactId).Order().ToArray();
        Assert.Equal(firstIds, secondIds);
    }

    [Fact]
    public void Scan_adds_normalized_legacy_data_model_identity_to_dbml_descriptors()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "Model.dbml"), """
            <Database Name="Store" Class="StoreContext" xmlns="http://schemas.microsoft.com/linqtosql/dbml/2007">
              <Table Name="Customers" Member="Customers">
                <Type Name="Customer">
                  <Column Name="CustomerId" Member="CustomerId" IsPrimaryKey="true" />
                  <Association Name="CustomerOrders" ThisKey="CustomerId" OtherKey="CustomerId" />
                </Type>
              </Table>
              <Function Name="GetCustomers" Method="GetCustomers" />
            </Database>
            """);

        var result = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, "out")));

        var entity = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.LegacyDataEntityDeclared
            && fact.RuleId == RuleIds.LegacyDataDbml
            && fact.Properties.GetValueOrDefault("entityName") == "Customer");
        Assert.Equal("dbml", entity.Properties.GetValueOrDefault("metadataFormat"));
        Assert.Equal("entity", entity.Properties.GetValueOrDefault("modelKind"));
        Assert.Equal("conceptual", entity.Properties.GetValueOrDefault("descriptorRole"));
        Assert.Equal(RuleIds.LegacyDataModelIdentity, entity.Properties.GetValueOrDefault("modelIdentityRuleId"));
        Assert.Equal(EvidenceTiers.Tier2Structural, entity.Properties.GetValueOrDefault("modelIdentityEvidenceTier"));
        Assert.Equal("Customer", entity.Properties.GetValueOrDefault("displayName"));
        Assert.StartsWith("ldm:", entity.Properties.GetValueOrDefault("stableModelKey"));
        Assert.False(string.IsNullOrWhiteSpace(entity.Properties.GetValueOrDefault("sourceMetadataFactId")));

        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.LegacyDataColumnDeclared
            && fact.RuleId == RuleIds.LegacyDataDbml
            && fact.Properties.GetValueOrDefault("modelKind") == "column"
            && fact.Properties.GetValueOrDefault("descriptorRole") == "storage"
            && fact.Properties.GetValueOrDefault("containerName") == "Customers");
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.LegacyDataMappingDeclared
            && fact.RuleId == RuleIds.LegacyDataDbml
            && fact.Properties.GetValueOrDefault("mappingKind") == "association"
            && fact.Properties.GetValueOrDefault("modelKind") == "relationship");
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.LegacyDataStorageObjectDeclared
            && fact.RuleId == RuleIds.LegacyDataDbml
            && fact.Properties.GetValueOrDefault("modelKind") == "routine"
            && fact.Properties.GetValueOrDefault("displayName") == "GetCustomers");
    }

    [Fact]
    public void Scan_adds_normalized_identity_to_edmx_and_typed_dataset_descriptors()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "Model.edmx"), """
            <edmx:Edmx xmlns:edmx="http://schemas.microsoft.com/ado/2009/11/edmx" Version="3.0">
              <edmx:Runtime>
                <edmx:ConceptualModels>
                  <Schema xmlns="http://schemas.microsoft.com/ado/2009/11/edm" Namespace="Model">
                    <EntityContainer Name="ModelContainer"><EntitySet Name="Customers" EntityType="Model.Customer" /></EntityContainer>
                    <EntityType Name="Customer"><Property Name="CustomerId" Type="Int32" /></EntityType>
                  </Schema>
                </edmx:ConceptualModels>
                <edmx:StorageModels>
                  <Schema xmlns="http://schemas.microsoft.com/ado/2009/11/edm/ssdl" Namespace="Store">
                    <EntityContainer Name="StoreContainer"><EntitySet Name="Customers" EntityType="Store.Customers" Table="Customers" /></EntityContainer>
                    <EntityType Name="Customers"><Property Name="CustomerId" Type="int" /></EntityType>
                  </Schema>
                </edmx:StorageModels>
                <edmx:Mappings>
                  <Mapping xmlns="http://schemas.microsoft.com/ado/2009/11/mapping/cs">
                    <EntityContainerMapping StorageEntityContainer="StoreContainer" CdmEntityContainer="ModelContainer">
                      <EntitySetMapping Name="Customers">
                        <EntityTypeMapping TypeName="Model.Customer">
                          <MappingFragment StoreEntitySet="Customers">
                            <ScalarProperty Name="CustomerId" ColumnName="CustomerId" />
                          </MappingFragment>
                        </EntityTypeMapping>
                      </EntitySetMapping>
                    </EntityContainerMapping>
                  </Mapping>
                </edmx:Mappings>
              </edmx:Runtime>
            </edmx:Edmx>
            """);
        File.WriteAllText(Path.Combine(temp.Path, "Orders.xsd"), """
            <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema"
                       xmlns:msdata="urn:schemas-microsoft-com:xml-msdata"
                       xmlns:msprop="urn:schemas-microsoft-com:xml-msprop">
              <xs:element name="OrdersDataSet" msdata:IsDataSet="true" msprop:Generator_DataSetName="OrdersDataSet">
                <xs:complexType>
                  <xs:choice maxOccurs="unbounded">
                    <xs:element name="Customers" msprop:Generator_UserTableName="Customers" msprop:Generator_RowClassName="CustomersRow">
                      <xs:complexType>
                        <xs:sequence>
                          <xs:element name="CustomerId" type="xs:int" />
                        </xs:sequence>
                      </xs:complexType>
                    </xs:element>
                  </xs:choice>
                </xs:complexType>
              </xs:element>
              <xs:annotation>
                <xs:appinfo>
                  <TableAdapterCommand Name="FillCustomers" CommandText="SELECT CustomerId FROM Customers" />
                </xs:appinfo>
              </xs:annotation>
            </xs:schema>
            """);

        var result = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, "out")));

        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.LegacyDataEntityDeclared
            && fact.RuleId == RuleIds.LegacyDataEdmx
            && fact.Properties.GetValueOrDefault("metadataFormat") == "edmx"
            && fact.Properties.GetValueOrDefault("modelKind") == "entity"
            && fact.Properties.GetValueOrDefault("stableModelKey")?.StartsWith("ldm:", StringComparison.Ordinal) == true);
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.LegacyDataMappingDeclared
            && fact.RuleId == RuleIds.LegacyDataEdmx
            && fact.Properties.GetValueOrDefault("descriptorRole") == "mapping"
            && fact.Properties.GetValueOrDefault("modelIdentityRuleId") == RuleIds.LegacyDataModelIdentity);
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.LegacyDataEntityDeclared
            && fact.RuleId == RuleIds.LegacyDataTypedDataSet
            && fact.Properties.GetValueOrDefault("metadataFormat") == "typed-dataset"
            && fact.Properties.GetValueOrDefault("modelKind") == "mapped-type"
            && fact.Properties.GetValueOrDefault("displayName") == "OrdersDataSet");
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.LegacyDataMappingDeclared
            && fact.RuleId == RuleIds.LegacyDataTypedDataSet
            && fact.Properties.GetValueOrDefault("metadataFormat") == "tableadapter"
            && fact.Properties.GetValueOrDefault("modelKind") == "adapter"
            && fact.Properties.GetValueOrDefault("displayName") == "FillCustomers");
    }

    [Fact]
    public void Scan_adds_dbml_relationship_semantics_and_duplicate_gaps()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "Model.dbml"), """
            <Database Name="Store" xmlns="http://schemas.microsoft.com/linqtosql/dbml/2007">
              <Table Name="Customers" Member="Customers">
                <Type Name="Customer">
                  <Column Name="CustomerId" Member="CustomerId" />
                  <Association Name="CustomerOrders" Type="Order" ThisKey="CustomerId" OtherKey="CustomerId" IsForeignKey="true" />
                  <Association Name="DuplicateRelation" Type="Order" ThisKey="CustomerId" OtherKey="CustomerId" />
                  <Association Name="DuplicateRelation" ThisKey="CustomerId" OtherKey="CustomerId" />
                </Type>
              </Table>
              <Table Name="Anonymous" Member="Anonymous">
                <Type>
                  <Association Name="MissingSource" Type="Order" ThisKey="CustomerId" OtherKey="CustomerId" />
                  <Association Name="MissingBoth" ThisKey="CustomerId" OtherKey="CustomerId" />
                </Type>
              </Table>
            </Database>
            """);

        var result = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, "out")));
        var repeated = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, "out-repeat")));

        var association = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.LegacyDataMappingDeclared
            && fact.RuleId == RuleIds.LegacyDataDbml
            && fact.Properties.GetValueOrDefault("associationName") == "CustomerOrders");
        Assert.Equal("association", association.Properties.GetValueOrDefault("mappingKind"));
        Assert.Equal("relationship", association.Properties.GetValueOrDefault("modelRelationshipKind"));
        Assert.Equal(RuleIds.LegacyDataModelRelationship, association.Properties.GetValueOrDefault("modelRelationshipRuleId"));
        Assert.Equal(EvidenceTiers.Tier2Structural, association.Properties.GetValueOrDefault("modelRelationshipEvidenceTier"));
        Assert.Equal("Customer", association.Properties.GetValueOrDefault("sourceEndpointName"));
        Assert.Equal("Order", association.Properties.GetValueOrDefault("targetEndpointName"));
        Assert.Equal("full", association.Properties.GetValueOrDefault("relationshipEndpointCoverage"));
        Assert.False(string.IsNullOrWhiteSpace(association.Properties.GetValueOrDefault("supportingFactIds")));

        var duplicateWithoutTarget = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.LegacyDataMappingDeclared
            && fact.RuleId == RuleIds.LegacyDataDbml
            && fact.Properties.GetValueOrDefault("associationName") == "DuplicateRelation"
            && fact.Properties.GetValueOrDefault("relationshipEndpointCoverage") == "unidirectional");
        Assert.Equal("reduced", duplicateWithoutTarget.Properties.GetValueOrDefault("coverageLabel"));
        Assert.Contains("missing-target-endpoint", duplicateWithoutTarget.Properties.GetValueOrDefault("limitations"));
        Assert.Contains("duplicate-relationship-name", duplicateWithoutTarget.Properties.GetValueOrDefault("limitations"));
        Assert.False(duplicateWithoutTarget.Properties.ContainsKey("targetEndpointName"));
        Assert.False(duplicateWithoutTarget.Properties.ContainsKey("targetEndpointHash"));
        Assert.Equal(2, result.Facts.Count(fact => fact.FactType == FactTypes.LegacyDataMappingDeclared
            && fact.RuleId == RuleIds.LegacyDataDbml
            && fact.Properties.GetValueOrDefault("associationName") == "DuplicateRelation"));
        Assert.DoesNotContain(result.Facts, fact => fact.Properties.ContainsKey("referentialIntegrity")
            || fact.Properties.ContainsKey("runtimeRelationshipLoaded")
            || fact.Properties.ContainsKey("tableExists"));

        var missingSource = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.LegacyDataMappingDeclared
            && fact.RuleId == RuleIds.LegacyDataDbml
            && fact.Properties.GetValueOrDefault("associationName") == "MissingSource");
        Assert.Equal("reduced", missingSource.Properties.GetValueOrDefault("coverageLabel"));
        Assert.Equal("unidirectional", missingSource.Properties.GetValueOrDefault("relationshipEndpointCoverage"));
        Assert.Equal("missing-source-endpoint", missingSource.Properties.GetValueOrDefault("limitations"));
        Assert.False(missingSource.Properties.ContainsKey("sourceEndpointName"));
        Assert.Equal("Order", missingSource.Properties.GetValueOrDefault("targetEndpointName"));
        Assert.DoesNotContain(result.Facts, fact => fact.FactType == FactTypes.LegacyDataMappingDeclared
            && fact.Properties.GetValueOrDefault("associationName") == "MissingBoth");
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyDataModelRelationship
            && fact.Properties.GetValueOrDefault("classification") == "IncompleteLegacyDataModelRelationship"
            && fact.Properties.GetValueOrDefault("safeReasonCode") == "missing-endpoint");

        var duplicateGap = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyDataDbml
            && fact.Properties.GetValueOrDefault("classification") == "AmbiguousLegacyDataModelIdentity");
        Assert.Equal("duplicate-relationship-identity", duplicateGap.Properties.GetValueOrDefault("safeReasonCode"));
        Assert.Equal("dbml", duplicateGap.Properties.GetValueOrDefault("relationshipFamily"));
        Assert.Equal("association", duplicateGap.Properties.GetValueOrDefault("descriptorKind"));
        Assert.Equal("False", duplicateGap.Properties.GetValueOrDefault("runtimeProof"));
        Assert.Equal(RuleIds.LegacyDataModelRelationship, duplicateGap.Properties.GetValueOrDefault("modelRelationshipRuleId"));

        var firstRelationshipFacts = result.Facts
            .Where(fact => fact.RuleId == RuleIds.LegacyDataDbml
                && (fact.FactType == FactTypes.AnalysisGap
                    || fact.Properties.GetValueOrDefault("modelRelationshipKind") == "relationship"))
            .Select(fact => fact.FactId)
            .ToArray();
        var repeatedRelationshipFacts = repeated.Facts
            .Where(fact => fact.RuleId == RuleIds.LegacyDataDbml
                && (fact.FactType == FactTypes.AnalysisGap
                    || fact.Properties.GetValueOrDefault("modelRelationshipKind") == "relationship"))
            .Select(fact => fact.FactId)
            .ToArray();
        Assert.Equal(firstRelationshipFacts, repeatedRelationshipFacts);
    }

    [Fact]
    public async Task Dbml_relationship_key_scope_and_extension_gaps_are_deterministic_and_do_not_invent_endpoints()
    {
        using var temp = new TempDirectory();
        const string protectedExtension = "Server=private-db;Password=super-secret";
        File.WriteAllText(Path.Combine(temp.Path, "RelationshipPrecision.dbml"), $$"""
            <Database Name="Store"
                      xmlns="http://schemas.microsoft.com/linqtosql/dbml/2007"
                      xmlns:vendor="urn:public-safe:dbml-extension">
              <Table Name="Customers" Member="Customers">
                <Type Name="Customer">
                  <Column Name="CustomerId" Member="CustomerId" />
                  <Column Name="TenantId" Member="TenantId" />
                  <Association Name="DeterministicComposite" Type="Order" ThisKey="CustomerId,TenantId" OtherKey="CustomerId,TenantId" />
                  <Association Name="MissingKey" Type="Order" OtherKey="CustomerId" />
                  <Association Name="MismatchedComposite" Type="Order" ThisKey="CustomerId,TenantId" OtherKey="CustomerId" />
                  <Association Name="DuplicateKeyMember" Type="Order" ThisKey="CustomerId,CustomerId" OtherKey="CustomerId,TenantId" />
                  <Association Name="UnsafeKey" Type="Order" ThisKey="Password=super-secret" OtherKey="CustomerId" />
                  <Association Name="ProviderExtended" Type="Order" ThisKey="CustomerId" OtherKey="CustomerId" vendor:Join="{{protectedExtension}}" />
                  <Association Name="DuplicateTargetScope" Type="DuplicateTarget" ThisKey="CustomerId" OtherKey="CustomerId" />
                </Type>
              </Table>
              <Table Name="Orders" Member="Orders"><Type Name="Order" /></Table>
              <Table Name="DuplicateTargetsOne" Member="DuplicateTargetsOne"><Type Name="DuplicateTarget" /></Table>
              <Table Name="DuplicateTargetsTwo" Member="DuplicateTargetsTwo"><Type Name="DuplicateTarget" /></Table>
              <Table Name="SharedScope" Member="SharedScopeOne">
                <Type Name="UniqueSourceOne">
                  <Association Name="DuplicateTableScope" Type="Order" ThisKey="CustomerId" OtherKey="CustomerId" />
                </Type>
              </Table>
              <Table Name="SharedScope" Member="SharedScopeTwo"><Type Name="UniqueSourceTwo" /></Table>
            </Database>
            """);

        var output = Path.Combine(temp.Path, "out");
        var repeatedOutput = Path.Combine(temp.Path, "out-repeat");
        Directory.CreateDirectory(output);
        var result = ScanEngine.Scan(new ScanOptions(temp.Path, output));
        var repeated = ScanEngine.Scan(new ScanOptions(temp.Path, repeatedOutput));

        var deterministic = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.LegacyDataMappingDeclared
            && fact.RuleId == RuleIds.LegacyDataDbml
            && fact.Properties.GetValueOrDefault("associationName") == "DeterministicComposite");
        Assert.Equal("full", deterministic.Properties.GetValueOrDefault("coverageLabel"));
        Assert.Equal("full", deterministic.Properties.GetValueOrDefault("relationshipEndpointCoverage"));

        var unsafeKey = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.LegacyDataMappingDeclared
            && fact.RuleId == RuleIds.LegacyDataDbml
            && fact.Properties.GetValueOrDefault("associationName") == "UnsafeKey");
        Assert.Equal("reduced", unsafeKey.Properties.GetValueOrDefault("coverageLabel"));
        Assert.Equal("full", unsafeKey.Properties.GetValueOrDefault("relationshipEndpointCoverage"));
        Assert.Equal("unsafe-redacted-endpoint-identity", unsafeKey.Properties.GetValueOrDefault("limitations"));
        Assert.True(unsafeKey.Properties.ContainsKey("sourceMemberHash"));
        Assert.False(unsafeKey.Properties.ContainsKey("sourceMemberName"));

        var rejectedNames = new[]
        {
            "MissingKey",
            "MismatchedComposite",
            "DuplicateKeyMember",
            "ProviderExtended",
            "DuplicateTargetScope",
            "DuplicateTableScope"
        };
        Assert.DoesNotContain(result.Facts, fact => fact.FactType == FactTypes.LegacyDataMappingDeclared
            && rejectedNames.Contains(fact.Properties.GetValueOrDefault("associationName"), StringComparer.Ordinal));

        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyDataModelRelationship
            && fact.Properties.GetValueOrDefault("classification") == "IncompleteLegacyDataModelRelationship"
            && fact.Properties.GetValueOrDefault("safeReasonCode") == "missing-endpoint");
        Assert.Equal(4, result.Facts.Count(fact => fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyDataDbml
            && fact.Properties.GetValueOrDefault("classification") == "AmbiguousLegacyDataModelIdentity"
            && fact.Properties.GetValueOrDefault("safeReasonCode") == "ambiguous-endpoint-candidates"));
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyDataDbml
            && fact.Properties.GetValueOrDefault("classification") == "UnsupportedLegacyOrmMappingShape"
            && fact.Properties.GetValueOrDefault("safeReasonCode") == "unsupported-relationship-shape");

        var firstRelationshipFactIds = result.Facts
            .Where(fact => fact.Properties.GetValueOrDefault("relationshipFamily") == "dbml"
                || (fact.RuleId == RuleIds.LegacyDataDbml
                    && fact.Properties.GetValueOrDefault("modelRelationshipKind") == "relationship"))
            .Select(fact => fact.FactId)
            .OrderBy(factId => factId, StringComparer.Ordinal)
            .ToArray();
        var repeatedRelationshipFactIds = repeated.Facts
            .Where(fact => fact.Properties.GetValueOrDefault("relationshipFamily") == "dbml"
                || (fact.RuleId == RuleIds.LegacyDataDbml
                    && fact.Properties.GetValueOrDefault("modelRelationshipKind") == "relationship"))
            .Select(fact => fact.FactId)
            .OrderBy(factId => factId, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(firstRelationshipFactIds, repeatedRelationshipFactIds);

        var factsPath = Path.Combine(output, "facts.ndjson");
        var indexPath = Path.Combine(output, "index.sqlite");
        await JsonlFactWriter.WriteAsync(factsPath, result.Facts);
        SqliteIndexWriter.Write(indexPath, result.Manifest, result.Facts);
        var defaultArtifacts = string.Join(
            "\n",
            await File.ReadAllTextAsync(factsPath),
            MarkdownReportWriter.Build(result),
            await ReadAllPropertiesAsync(indexPath));
        Assert.DoesNotContain(protectedExtension, defaultArtifacts, StringComparison.Ordinal);
        Assert.DoesNotContain("private-db", defaultArtifacts, StringComparison.Ordinal);
        Assert.DoesNotContain("super-secret", defaultArtifacts, StringComparison.Ordinal);
        Assert.DoesNotContain(temp.Path, defaultArtifacts, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Dbml_relationship_outputs_hash_unsafe_endpoint_and_key_values_across_default_artifacts()
    {
        using var temp = new TempDirectory();
        const string unsafeAssociation = "https://private.example/token";
        const string unsafeEndpoint = "prod-db.example.com;Password=super-secret";
        const string unsafeKey = "SELECT * FROM CredentialTable";
        const string unsafeOtherKey = "C:\\private\\secret.key;Provider=PrivateDialect";
        const string unsafeStorage = "private-server/private-catalog";
        File.WriteAllText(Path.Combine(temp.Path, "UnsafeRelationship.dbml"), $$"""
            <Database Name="Store" xmlns="http://schemas.microsoft.com/linqtosql/dbml/2007">
              <Table Name="{{unsafeStorage}}" Member="Customers">
                <Type Name="Customer">
                  <Association Name="{{unsafeAssociation}}" Type="{{unsafeEndpoint}}" ThisKey="{{unsafeKey}}" OtherKey="{{unsafeOtherKey}}" />
                </Type>
              </Table>
            </Database>
            """);

        var output = Path.Combine(temp.Path, "out");
        Directory.CreateDirectory(output);
        var result = ScanEngine.Scan(new ScanOptions(temp.Path, output));
        var factsPath = Path.Combine(output, "facts.ndjson");
        var indexPath = Path.Combine(output, "index.sqlite");
        await JsonlFactWriter.WriteAsync(factsPath, result.Facts);
        SqliteIndexWriter.Write(indexPath, result.Manifest, result.Facts);

        var relationship = Assert.Single(result.Facts, fact =>
            fact.FactType == FactTypes.LegacyDataMappingDeclared
            && fact.RuleId == RuleIds.LegacyDataDbml
            && fact.Properties.GetValueOrDefault("descriptorKind") == "association");
        Assert.True(relationship.Properties.ContainsKey("associationHash"));
        Assert.True(relationship.Properties.ContainsKey("targetEndpointHash"));
        Assert.True(relationship.Properties.ContainsKey("sourceMemberHash"));
        Assert.Equal("reduced", relationship.Properties.GetValueOrDefault("coverageLabel"));
        Assert.Equal("unsafe-redacted-endpoint-identity", relationship.Properties.GetValueOrDefault("limitations"));

        var defaultArtifacts = string.Join(
            "\n",
            await File.ReadAllTextAsync(factsPath),
            MarkdownReportWriter.Build(result),
            await ReadAllPropertiesAsync(indexPath));
        foreach (var protectedValue in new[] { unsafeAssociation, unsafeEndpoint, unsafeKey, unsafeOtherKey, unsafeStorage, "super-secret", "private.example", "CredentialTable", "PrivateDialect", temp.Path })
        {
            Assert.DoesNotContain(protectedValue, defaultArtifacts, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Scan_adds_edmx_relationship_semantics_and_ambiguous_gaps()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "Model.edmx"), """
            <edmx:Edmx xmlns:edmx="http://schemas.microsoft.com/ado/2009/11/edmx" Version="3.0">
              <edmx:Runtime>
                <edmx:ConceptualModels>
                  <Schema xmlns="http://schemas.microsoft.com/ado/2009/11/edm" Namespace="Model">
                    <EntityContainer Name="ModelContainer">
                      <EntitySet Name="Customers" EntityType="Model.Customer" />
                      <EntitySet Name="Orders" EntityType="Model.Order" />
                    </EntityContainer>
                    <EntityType Name="Customer"><Property Name="CustomerId" Type="Int32" /></EntityType>
                    <EntityType Name="Order"><Property Name="OrderId" Type="Int32" /></EntityType>
                    <EntityType Name="PreferredCustomer" BaseType="Model.Customer"><Property Name="LoyaltyId" Type="Int32" /></EntityType>
                    <Association Name="CustomerOrders">
                      <End Role="Customer" Type="Model.Customer" Multiplicity="1" />
                      <End Role="Orders" Type="Model.Order" Multiplicity="*" />
                    </Association>
                    <Association Name="PreferredCustomerOrders">
                      <End Role="PreferredCustomer" Type="Model.PreferredCustomer" Multiplicity="1" />
                      <End Role="Orders" Type="Model.Order" Multiplicity="*" />
                    </Association>
                    <Association Name="MissingTypeAssociation">
                      <End Role="Customer" Type="Model.Customer" />
                      <End Role="UnknownOrder" />
                    </Association>
                    <Association Name="MissingBothTypes">
                      <End Role="FirstRole" />
                      <End Role="SecondRole" />
                    </Association>
                    <Association Name="AmbiguousAssociation">
                      <End Role="OnlyOne" Type="Model.Customer" />
                    </Association>
                  </Schema>
                </edmx:ConceptualModels>
                <edmx:StorageModels>
                  <Schema xmlns="http://schemas.microsoft.com/ado/2009/11/edm/ssdl" Namespace="Store">
                    <EntityContainer Name="StoreContainer">
                      <EntitySet Name="Customers" EntityType="Store.Customers" Table="Customers" />
                      <EntitySet Name="Orders" EntityType="Store.Orders" Table="Orders" />
                      <EntitySet Name="FK_CustomerOrders" EntityType="Store.FK_CustomerOrders" Table="FK_CustomerOrders" />
                    </EntityContainer>
                    <EntityType Name="Customers"><Property Name="CustomerId" Type="int" /></EntityType>
                    <EntityType Name="Orders"><Property Name="OrderId" Type="int" /></EntityType>
                  </Schema>
                </edmx:StorageModels>
                <edmx:Mappings>
                  <Mapping xmlns="http://schemas.microsoft.com/ado/2009/11/mapping/cs">
                    <EntityContainerMapping StorageEntityContainer="StoreContainer" CdmEntityContainer="ModelContainer">
                      <AssociationSetMapping Name="CustomerOrders" TypeName="Model.CustomerOrders" StoreEntitySet="FK_CustomerOrders">
                        <EndProperty Name="Customer"><ScalarProperty Name="CustomerId" ColumnName="CustomerId" /></EndProperty>
                        <EndProperty Name="Orders"><ScalarProperty Name="OrderId" ColumnName="OrderId" /></EndProperty>
                      </AssociationSetMapping>
                      <AssociationSetMapping Name="BrokenAssociation" TypeName="Model.Broken" StoreEntitySet="FK_Broken">
                        <EndProperty Name="OnlyOne" />
                      </AssociationSetMapping>
                      <AssociationSetMapping Name="DuplicateRoleAssociation" TypeName="Model.Duplicate" StoreEntitySet="FK_Duplicate">
                        <EndProperty Name="Customer" />
                        <EndProperty Name="Customer" />
                      </AssociationSetMapping>
                      <AssociationSetMapping Name="MissingRoleAssociation" TypeName="Model.MissingRole" StoreEntitySet="FK_MissingRole">
                        <EndProperty Name="Customer" />
                        <EndProperty />
                      </AssociationSetMapping>
                    </EntityContainerMapping>
                  </Mapping>
                </edmx:Mappings>
              </edmx:Runtime>
            </edmx:Edmx>
            """);

        var result = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, "out")));
        var repeated = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, "out-repeat")));

        var csdlAssociation = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.LegacyDataMappingDeclared
            && fact.RuleId == RuleIds.LegacyDataEdmx
            && fact.Properties.GetValueOrDefault("descriptorKind") == "csdl-association"
            && fact.Properties.GetValueOrDefault("associationName") == "CustomerOrders");
        Assert.Equal("association", csdlAssociation.Properties.GetValueOrDefault("mappingKind"));
        Assert.Equal("relationship", csdlAssociation.Properties.GetValueOrDefault("modelRelationshipKind"));
        Assert.Equal("Customer", csdlAssociation.Properties.GetValueOrDefault("sourceEndpointName"));
        Assert.Equal("Order", csdlAssociation.Properties.GetValueOrDefault("targetEndpointName"));
        Assert.Equal("full", csdlAssociation.Properties.GetValueOrDefault("relationshipEndpointCoverage"));

        var inheritedEntity = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.LegacyDataEntityDeclared
            && fact.RuleId == RuleIds.LegacyDataEdmx
            && fact.Properties.GetValueOrDefault("entityName") == "PreferredCustomer");
        Assert.Equal("reduced", inheritedEntity.Properties.GetValueOrDefault("coverageLabel"));
        Assert.Contains("unsupported-inherited-model-shape", inheritedEntity.Properties.GetValueOrDefault("limitations"));

        var inheritedAssociation = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.LegacyDataMappingDeclared
            && fact.RuleId == RuleIds.LegacyDataEdmx
            && fact.Properties.GetValueOrDefault("descriptorKind") == "csdl-association"
            && fact.Properties.GetValueOrDefault("associationName") == "PreferredCustomerOrders");
        Assert.Equal("reduced", inheritedAssociation.Properties.GetValueOrDefault("coverageLabel"));
        Assert.Equal("full", inheritedAssociation.Properties.GetValueOrDefault("relationshipEndpointCoverage"));
        Assert.Contains("inherited-endpoint-needs-review", inheritedAssociation.Properties.GetValueOrDefault("limitations"));

        var missingTypeAssociation = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.LegacyDataMappingDeclared
            && fact.RuleId == RuleIds.LegacyDataEdmx
            && fact.Properties.GetValueOrDefault("descriptorKind") == "csdl-association"
            && fact.Properties.GetValueOrDefault("associationName") == "MissingTypeAssociation");
        Assert.Equal("unidirectional", missingTypeAssociation.Properties.GetValueOrDefault("relationshipEndpointCoverage"));
        Assert.Equal("reduced", missingTypeAssociation.Properties.GetValueOrDefault("coverageLabel"));
        Assert.Contains("missing-endpoint-type", missingTypeAssociation.Properties.GetValueOrDefault("limitations"));

        Assert.DoesNotContain(result.Facts, fact => fact.FactType == FactTypes.LegacyDataMappingDeclared
            && fact.Properties.GetValueOrDefault("associationName") is "MissingBothTypes" or "MissingRoleAssociation");
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyDataModelRelationship
            && fact.Properties.GetValueOrDefault("classification") == "IncompleteLegacyDataModelRelationship"
            && fact.Properties.GetValueOrDefault("relationshipFamily") == "edmx"
            && fact.Properties.GetValueOrDefault("descriptorKind") == "csdl-association"
            && fact.Properties.GetValueOrDefault("safeReasonCode") == "missing-endpoint");
        Assert.Equal(2, result.Facts.Count(fact => fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyDataModelRelationship
            && fact.Properties.GetValueOrDefault("classification") == "IncompleteLegacyDataModelRelationship"
            && fact.Properties.GetValueOrDefault("relationshipFamily") == "edmx"
            && fact.Properties.GetValueOrDefault("descriptorKind") == "msl-association"
            && fact.Properties.GetValueOrDefault("safeReasonCode") == "missing-endpoint"));

        var mslAssociation = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.LegacyDataMappingDeclared
            && fact.RuleId == RuleIds.LegacyDataEdmx
            && fact.Properties.GetValueOrDefault("descriptorKind") == "msl-association"
            && fact.Properties.GetValueOrDefault("associationName") == "CustomerOrders");
        Assert.Equal("MSL", mslAssociation.Properties.GetValueOrDefault("sourceSection"));
        Assert.Equal("relationship", mslAssociation.Properties.GetValueOrDefault("modelRelationshipKind"));
        Assert.Equal("FK_CustomerOrders", mslAssociation.Properties.GetValueOrDefault("containerName"));

        Assert.Equal(2, result.Facts.Count(fact => fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyDataEdmx
            && fact.Properties.GetValueOrDefault("classification") == "AmbiguousLegacyDataModelIdentity"));
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyDataEdmx
            && fact.Properties.GetValueOrDefault("classification") == "UnsupportedLegacyOrmMappingShape");

        Assert.Equal(
            result.Facts.Where(IsEdmxRelationshipEvidence).Select(fact => fact.FactId).Order(StringComparer.Ordinal),
            repeated.Facts.Where(IsEdmxRelationshipEvidence).Select(fact => fact.FactId).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Edmx_relationship_outputs_hash_unsafe_endpoint_values_across_default_artifacts()
    {
        using var temp = new TempDirectory();
        const string unsafeAssociation = "https://private.example/relation?token=secret";
        const string unsafeType = "Server=private;Password=secret";
        const string unsafeRole = "C:\\private\\endpoint";
        const string unsafeStoreSet = "private-server;Initial Catalog=Secret";
        File.WriteAllText(Path.Combine(temp.Path, "Unsafe.edmx"), $$"""
            <edmx:Edmx xmlns:edmx="http://schemas.microsoft.com/ado/2009/11/edmx" Version="3.0">
              <edmx:Runtime>
                <edmx:ConceptualModels>
                  <Schema xmlns="http://schemas.microsoft.com/ado/2009/11/edm" Namespace="Model">
                    <EntityContainer Name="ModelContainer" />
                    <Association Name="{{unsafeAssociation}}">
                      <End Role="{{unsafeRole}}" Type="Model.{{unsafeType}}" />
                      <End Role="SafeRole" Type="Model.SafeType" />
                    </Association>
                  </Schema>
                </edmx:ConceptualModels>
                <edmx:StorageModels>
                  <Schema xmlns="http://schemas.microsoft.com/ado/2009/11/edm/ssdl" Namespace="Store">
                    <EntityContainer Name="StoreContainer" />
                  </Schema>
                </edmx:StorageModels>
                <edmx:Mappings>
                  <Mapping xmlns="http://schemas.microsoft.com/ado/2009/11/mapping/cs">
                    <EntityContainerMapping StorageEntityContainer="StoreContainer" CdmEntityContainer="ModelContainer">
                      <AssociationSetMapping Name="{{unsafeAssociation}}" StoreEntitySet="{{unsafeStoreSet}}">
                        <EndProperty Name="{{unsafeRole}}" />
                        <EndProperty Name="SafeRole" />
                      </AssociationSetMapping>
                    </EntityContainerMapping>
                  </Mapping>
                </edmx:Mappings>
              </edmx:Runtime>
            </edmx:Edmx>
            """);

        var output = Path.Combine(temp.Path, "out");
        Directory.CreateDirectory(output);
        var result = ScanEngine.Scan(new ScanOptions(temp.Path, output));
        var factsPath = Path.Combine(output, "facts.ndjson");
        var indexPath = Path.Combine(output, "index.sqlite");
        await JsonlFactWriter.WriteAsync(factsPath, result.Facts);
        SqliteIndexWriter.Write(indexPath, result.Manifest, result.Facts);

        var relationships = result.Facts
            .Where(fact => fact.FactType == FactTypes.LegacyDataMappingDeclared
                && fact.RuleId == RuleIds.LegacyDataEdmx
                && fact.Properties.GetValueOrDefault("modelRelationshipKind") == "relationship")
            .ToArray();
        Assert.Equal(2, relationships.Length);
        Assert.All(relationships, relationship =>
        {
            Assert.Equal("reduced", relationship.Properties.GetValueOrDefault("coverageLabel"));
            Assert.Equal("unsafe-redacted-endpoint-identity", relationship.Properties.GetValueOrDefault("limitations"));
            Assert.True(relationship.Properties.ContainsKey("associationHash"));
            Assert.True(relationship.Properties.ContainsKey("sourceEndpointHash"));
        });

        var defaultArtifacts = string.Join(
            "\n",
            await File.ReadAllTextAsync(factsPath),
            MarkdownReportWriter.Build(result),
            await ReadAllPropertiesAsync(indexPath));
        foreach (var protectedValue in new[] { unsafeAssociation, unsafeType, unsafeRole, unsafeStoreSet, "private.example", "Password=secret", "Initial Catalog=Secret", temp.Path })
        {
            Assert.DoesNotContain(protectedValue, defaultArtifacts, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Scan_keeps_edmx_endpoint_coverage_separate_from_file_coverage()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "ReducedModel.edmx"), """
            <edmx:Edmx xmlns:edmx="http://schemas.microsoft.com/ado/2009/11/edmx" Version="3.0">
              <edmx:Runtime>
                <edmx:ConceptualModels>
                  <Schema xmlns="http://schemas.microsoft.com/ado/2009/11/edm" Namespace="Model">
                    <EntityContainer Name="FirstContainer" />
                    <EntityContainer Name="SecondContainer" />
                    <Association Name="CustomerOrders">
                      <End Role="Customer" Type="Model.Customer" />
                      <End Role="Orders" Type="Model.Order" />
                    </Association>
                  </Schema>
                </edmx:ConceptualModels>
                <edmx:StorageModels>
                  <Schema xmlns="http://schemas.microsoft.com/ado/2009/11/edm/ssdl" Namespace="Store">
                    <EntityContainer Name="StoreContainer" />
                  </Schema>
                </edmx:StorageModels>
                <edmx:Mappings>
                  <Mapping xmlns="http://schemas.microsoft.com/ado/2009/11/mapping/cs" />
                </edmx:Mappings>
              </edmx:Runtime>
            </edmx:Edmx>
            """);

        var result = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, "out")));

        var association = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.LegacyDataMappingDeclared
            && fact.RuleId == RuleIds.LegacyDataEdmx
            && fact.Properties.GetValueOrDefault("descriptorKind") == "csdl-association"
            && fact.Properties.GetValueOrDefault("associationName") == "CustomerOrders");
        Assert.Equal("reduced", association.Properties.GetValueOrDefault("coverageLabel"));
        Assert.Equal("full", association.Properties.GetValueOrDefault("relationshipEndpointCoverage"));
        Assert.False(association.Properties.ContainsKey("limitations"));
    }

    [Fact]
    public void Scan_adds_typed_dataset_relation_and_constraint_relationship_semantics()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "Orders.xsd"), """
            <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema"
                       xmlns:msdata="urn:schemas-microsoft-com:xml-msdata"
                       xmlns:msprop="urn:schemas-microsoft-com:xml-msprop"
                       xmlns:mstns="urn:store">
              <xs:element name="OrdersDataSet" msdata:IsDataSet="true" msprop:Generator_DataSetName="OrdersDataSet">
                <xs:complexType>
                  <xs:choice maxOccurs="unbounded">
                    <xs:element name="Customers" msprop:Generator_UserTableName="Customers" msprop:Generator_RowClassName="CustomersRow">
                      <xs:complexType><xs:sequence><xs:element name="CustomerId" type="xs:int" /></xs:sequence></xs:complexType>
                    </xs:element>
                    <xs:element name="Orders" msprop:Generator_UserTableName="Orders" msprop:Generator_RowClassName="OrdersRow">
                      <xs:complexType><xs:sequence><xs:element name="CustomerId" type="xs:int" /></xs:sequence></xs:complexType>
                    </xs:element>
                  </xs:choice>
                </xs:complexType>
              </xs:element>
              <xs:key name="Customers.IdKey"><xs:selector xpath=".//mstns:Customers" /><xs:field xpath="mstns:CustomerId" /></xs:key>
              <xs:keyref name="CustomerOrdersConstraint" refer="mstns:Customers.IdKey">
                <xs:selector xpath=".//mstns:Orders" />
                <xs:field xpath="mstns:CustomerId" />
              </xs:keyref>
              <xs:annotation><xs:appinfo>
                <msdata:Relationship name="CustomerOrders" parent="Customers" child="Orders" />
                <msdata:Relationship name="MissingParent" child="Orders" />
                <msdata:Relationship name="MissingBoth" />
              </xs:appinfo></xs:annotation>
            </xs:schema>
            """);

        var result = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, "out")));
        var repeated = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, "out-repeat")));

        var relation = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.LegacyDataMappingDeclared
            && fact.RuleId == RuleIds.LegacyDataTypedDataSet
            && fact.Properties.GetValueOrDefault("descriptorKind") == "relation"
            && fact.Properties.GetValueOrDefault("relationName") == "CustomerOrders");
        Assert.Equal("relation", relation.Properties.GetValueOrDefault("mappingKind"));
        Assert.Equal("relationship", relation.Properties.GetValueOrDefault("modelRelationshipKind"));
        Assert.Equal("Customers", relation.Properties.GetValueOrDefault("sourceEndpointName"));
        Assert.Equal("Orders", relation.Properties.GetValueOrDefault("targetEndpointName"));
        Assert.Equal("full", relation.Properties.GetValueOrDefault("relationshipEndpointCoverage"));

        var constraint = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.LegacyDataMappingDeclared
            && fact.RuleId == RuleIds.LegacyDataTypedDataSet
            && fact.Properties.GetValueOrDefault("descriptorKind") == "constraint-relation"
            && fact.Properties.GetValueOrDefault("relationName") == "CustomerOrdersConstraint");
        Assert.Equal("relation", constraint.Properties.GetValueOrDefault("mappingKind"));
        Assert.Equal("relationship", constraint.Properties.GetValueOrDefault("modelRelationshipKind"));
        Assert.Equal("Customers", constraint.Properties.GetValueOrDefault("sourceEndpointName"));
        Assert.Equal("Orders", constraint.Properties.GetValueOrDefault("targetEndpointName"));
        Assert.Equal("Customers.IdKey", constraint.Properties.GetValueOrDefault("referencedConstraintName"));
        Assert.Equal("full", constraint.Properties.GetValueOrDefault("relationshipEndpointCoverage"));

        var missingParent = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.LegacyDataMappingDeclared
            && fact.RuleId == RuleIds.LegacyDataTypedDataSet
            && fact.Properties.GetValueOrDefault("descriptorKind") == "relation"
            && fact.Properties.GetValueOrDefault("relationName") == "MissingParent");
        Assert.Equal("reduced", missingParent.Properties.GetValueOrDefault("coverageLabel"));
        Assert.Equal("unidirectional", missingParent.Properties.GetValueOrDefault("relationshipEndpointCoverage"));
        Assert.Equal("missing-relationship-endpoint", missingParent.Properties.GetValueOrDefault("limitations"));
        Assert.False(missingParent.Properties.ContainsKey("sourceEndpointName"));
        Assert.Equal("Orders", missingParent.Properties.GetValueOrDefault("targetEndpointName"));

        Assert.DoesNotContain(result.Facts, fact => fact.FactType == FactTypes.LegacyDataMappingDeclared
            && fact.Properties.GetValueOrDefault("relationName") == "MissingBoth");
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyDataModelRelationship
            && fact.Properties.GetValueOrDefault("classification") == "IncompleteLegacyDataModelRelationship"
            && fact.Properties.GetValueOrDefault("relationshipFamily") == "typed-dataset"
            && fact.Properties.GetValueOrDefault("safeReasonCode") == "missing-endpoint");

        Assert.Equal(
            result.Facts.Where(IsTypedDataSetRelationshipEvidence).Select(fact => fact.FactId),
            repeated.Facts.Where(IsTypedDataSetRelationshipEvidence).Select(fact => fact.FactId));
    }

    [Fact]
    public void Scan_keeps_same_line_typed_dataset_relationship_gaps_distinct_in_sqlite()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "Minified.xsd"), """
            <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema" xmlns:msdata="urn:schemas-microsoft-com:xml-msdata" xmlns:msprop="urn:schemas-microsoft-com:xml-msprop"><xs:element name="SafeDataSet" msdata:IsDataSet="true" msprop:Generator_DataSetName="SafeDataSet"/><xs:keyref name="MissingKeyrefA"/><xs:keyref name="MissingKeyrefB"/><xs:annotation><xs:appinfo><msdata:Relationship name="MissingRelationA"/><msdata:Relationship name="MissingRelationB"/></xs:appinfo></xs:annotation></xs:schema>
            """);

        var output = Path.Combine(temp.Path, "out");
        Directory.CreateDirectory(output);
        var result = ScanEngine.Scan(new ScanOptions(temp.Path, output));
        var relationshipGaps = result.Facts
            .Where(fact => fact.FactType == FactTypes.AnalysisGap
                && fact.RuleId == RuleIds.LegacyDataModelRelationship
                && fact.Properties.GetValueOrDefault("relationshipFamily") == "typed-dataset")
            .ToArray();

        Assert.Equal(4, relationshipGaps.Length);
        Assert.Single(relationshipGaps.Select(fact => fact.Evidence.StartLine).Distinct());
        Assert.Equal(4, relationshipGaps.Select(fact => fact.FactId).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(4, relationshipGaps.Select(fact => fact.Properties.GetValueOrDefault("descriptorOrdinal")).Distinct(StringComparer.Ordinal).Count());

        SqliteIndexWriter.Write(Path.Combine(output, "index.sqlite"), result.Manifest, result.Facts);
    }

    [Fact]
    public void Scan_keeps_relationship_gap_descriptor_ordinals_stable_across_xml_formatting()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "Minified.xsd"), """
            <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema" xmlns:msdata="urn:schemas-microsoft-com:xml-msdata" xmlns:msprop="urn:schemas-microsoft-com:xml-msprop"><xs:element name="SafeDataSet" msdata:IsDataSet="true" msprop:Generator_DataSetName="SafeDataSet"/><xs:annotation><xs:appinfo><msdata:Relationship name="Missing"/></xs:appinfo></xs:annotation></xs:schema>
            """);
        File.WriteAllText(Path.Combine(temp.Path, "Formatted.xsd"), """
            <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema"
                       xmlns:msdata="urn:schemas-microsoft-com:xml-msdata"
                       xmlns:msprop="urn:schemas-microsoft-com:xml-msprop">
              <xs:element name="SafeDataSet"
                          msdata:IsDataSet="true"
                          msprop:Generator_DataSetName="SafeDataSet" />
              <xs:annotation>
                <xs:appinfo>
                  <msdata:Relationship name="Missing" />
                </xs:appinfo>
              </xs:annotation>
            </xs:schema>
            """);

        var result = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, "out")));
        var ordinals = result.Facts
            .Where(fact => fact.FactType == FactTypes.AnalysisGap
                && fact.RuleId == RuleIds.LegacyDataModelRelationship
                && fact.Properties.GetValueOrDefault("relationshipFamily") == "typed-dataset")
            .OrderBy(fact => fact.Evidence.FilePath, StringComparer.Ordinal)
            .Select(fact => fact.Properties.GetValueOrDefault("descriptorOrdinal"))
            .ToArray();

        Assert.Equal(2, ordinals.Length);
        Assert.Equal(ordinals[0], ordinals[1]);
    }

    [Fact]
    public void Scan_marks_ambiguous_typed_dataset_constraints_and_ignores_non_xsd_lookalikes()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "AmbiguousConstraints.xsd"), """
            <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema"
                       xmlns:msdata="urn:schemas-microsoft-com:xml-msdata"
                       xmlns:msprop="urn:schemas-microsoft-com:xml-msprop"
                       xmlns:mstns="urn:store"
                       xmlns:custom="urn:custom">
              <xs:element name="OrdersDataSet" msdata:IsDataSet="true" msprop:Generator_DataSetName="OrdersDataSet">
                <xs:complexType>
                  <xs:choice maxOccurs="unbounded">
                    <xs:element name="Customers" msprop:Generator_UserTableName="Customers" msprop:Generator_RowClassName="CustomersRow" />
                    <xs:element name="Orders" msprop:Generator_UserTableName="Orders" msprop:Generator_RowClassName="OrdersRow" />
                    <xs:element name="OrderLines" msprop:Generator_UserTableName="OrderLines" msprop:Generator_RowClassName="OrderLinesRow" />
                  </xs:choice>
                </xs:complexType>
              </xs:element>
              <xs:key name="SharedKey"><xs:selector xpath=".//mstns:Customers" /><xs:field xpath="mstns:CustomerId" /></xs:key>
              <xs:key name="SharedKey"><xs:selector xpath=".//mstns:Orders" /><xs:field xpath="mstns:OrderId" /></xs:key>
              <xs:keyref name="AmbiguousCustomerOrders" refer="mstns:SharedKey">
                <xs:selector xpath=".//mstns:OrderLines" />
                <xs:field xpath="mstns:OrderId" />
              </xs:keyref>
              <xs:keyref name="AmbiguousMissingChild" refer="mstns:SharedKey" />
              <xs:keyref name="MissingBoth" />
              <custom:keyref name="FakeRelationship" refer="mstns:SharedKey">
                <custom:selector xpath=".//mstns:Orders" />
              </custom:keyref>
            </xs:schema>
            """);

        var result = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, "out")));

        Assert.Equal(4, result.Facts.Count(fact => fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyDataTypedDataSet
            && fact.Properties.GetValueOrDefault("classification") == "AmbiguousLegacyDataModelIdentity"));

        var ambiguousConstraint = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.LegacyDataMappingDeclared
            && fact.RuleId == RuleIds.LegacyDataTypedDataSet
            && fact.Properties.GetValueOrDefault("descriptorKind") == "constraint-relation"
            && fact.Properties.GetValueOrDefault("relationName") == "AmbiguousCustomerOrders");
        Assert.Equal("unidirectional", ambiguousConstraint.Properties.GetValueOrDefault("relationshipEndpointCoverage"));
        Assert.Contains("ambiguous-constraint-name", ambiguousConstraint.Properties.GetValueOrDefault("limitations"));
        Assert.Contains("constraint-endpoint-needs-review", ambiguousConstraint.Properties.GetValueOrDefault("limitations"));

        Assert.DoesNotContain(result.Facts, fact => fact.FactType == FactTypes.LegacyDataMappingDeclared
            && fact.Properties.GetValueOrDefault("relationName") == "FakeRelationship");
        Assert.DoesNotContain(result.Facts, fact => fact.FactType == FactTypes.LegacyDataMappingDeclared
            && fact.Properties.GetValueOrDefault("relationName") == "MissingBoth");
        Assert.DoesNotContain(result.Facts, fact => fact.FactType == FactTypes.LegacyDataMappingDeclared
            && fact.Properties.GetValueOrDefault("relationName") == "AmbiguousMissingChild");
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyDataModelRelationship
            && fact.Properties.GetValueOrDefault("descriptorKind") == "constraint-relation"
            && fact.Properties.GetValueOrDefault("safeReasonCode") == "missing-endpoint");
    }

    [Fact]
    public async Task Typed_dataset_relationship_outputs_hash_unsafe_names_across_default_artifacts()
    {
        using var temp = new TempDirectory();
        const string unsafeRelation = "https://private.example/relation?token=secret";
        const string unsafeParent = "private-server;Database=Catalog";
        const string unsafeChild = "C:\\private\\child.table";
        File.WriteAllText(Path.Combine(temp.Path, "UnsafeRelationships.xsd"), $$"""
            <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema"
                       xmlns:msdata="urn:schemas-microsoft-com:xml-msdata"
                       xmlns:msprop="urn:schemas-microsoft-com:xml-msprop">
              <xs:element name="SafeDataSet" msdata:IsDataSet="true" msprop:Generator_DataSetName="SafeDataSet" />
              <xs:annotation><xs:appinfo>
                <msdata:Relationship name="{{unsafeRelation}}" parent="{{unsafeParent}}" child="{{unsafeChild}}" />
              </xs:appinfo></xs:annotation>
            </xs:schema>
            """);

        var output = Path.Combine(temp.Path, "out");
        Directory.CreateDirectory(output);
        var result = ScanEngine.Scan(new ScanOptions(temp.Path, output));
        var factsPath = Path.Combine(output, "facts.ndjson");
        var indexPath = Path.Combine(output, "index.sqlite");
        await JsonlFactWriter.WriteAsync(factsPath, result.Facts);
        SqliteIndexWriter.Write(indexPath, result.Manifest, result.Facts);

        var relationship = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.LegacyDataMappingDeclared
            && fact.RuleId == RuleIds.LegacyDataTypedDataSet
            && fact.Properties.GetValueOrDefault("descriptorKind") == "relation");
        Assert.True(relationship.Properties.ContainsKey("relationHash"));
        Assert.True(relationship.Properties.ContainsKey("sourceEndpointHash"));
        Assert.True(relationship.Properties.ContainsKey("targetEndpointHash"));

        var defaultArtifacts = string.Join(
            "\n",
            await File.ReadAllTextAsync(factsPath),
            MarkdownReportWriter.Build(result),
            await ReadAllPropertiesAsync(indexPath));
        foreach (var protectedValue in new[] { unsafeRelation, unsafeParent, unsafeChild, "private.example", "private-server", "Catalog", temp.Path })
        {
            Assert.DoesNotContain(protectedValue, defaultArtifacts, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Scan_keeps_duplicate_display_names_distinct_by_format_and_source()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "A.dbml"), """
            <Database Name="A" xmlns="http://schemas.microsoft.com/linqtosql/dbml/2007">
              <Table Name="Customers" Member="Customers"><Type Name="Customer" /></Table>
            </Database>
            """);
        File.WriteAllText(Path.Combine(temp.Path, "B.dbml"), """
            <Database Name="B" xmlns="http://schemas.microsoft.com/linqtosql/dbml/2007">
              <Table Name="Customers" Member="Customers"><Type Name="Customer" /></Table>
            </Database>
            """);
        File.WriteAllText(Path.Combine(temp.Path, "C.xsd"), """
            <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema"
                       xmlns:msdata="urn:schemas-microsoft-com:xml-msdata"
                       xmlns:msprop="urn:schemas-microsoft-com:xml-msprop">
              <xs:element name="CustomerDataSet" msdata:IsDataSet="true" msprop:Generator_DataSetName="CustomerDataSet">
                <xs:complexType>
                  <xs:choice maxOccurs="unbounded">
                    <xs:element name="Customers" msprop:Generator_UserTableName="Customers" msprop:Generator_RowClassName="Customer" />
                  </xs:choice>
                </xs:complexType>
              </xs:element>
            </xs:schema>
            """);

        var result = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, "out1")));
        var second = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, "out2")));

        var customerEntityKeys = result.Facts
            .Where(fact => fact.FactType == FactTypes.LegacyDataEntityDeclared
                && fact.Properties.GetValueOrDefault("displayName") == "Customer")
            .Select(fact => fact.Properties.GetValueOrDefault("stableModelKey"))
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .ToArray();

        Assert.Equal(3, customerEntityKeys.Length);
        Assert.Equal(customerEntityKeys.Length, customerEntityKeys.Distinct(StringComparer.Ordinal).Count());

        var secondCustomerEntityKeys = second.Facts
            .Where(fact => fact.FactType == FactTypes.LegacyDataEntityDeclared
                && fact.Properties.GetValueOrDefault("displayName") == "Customer")
            .Select(fact => fact.Properties.GetValueOrDefault("stableModelKey"))
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Order(StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(customerEntityKeys.Order(StringComparer.Ordinal), secondCustomerEntityKeys);
    }

    [Fact]
    public void Scan_keeps_distinct_stable_keys_for_same_name_across_metadata_formats()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "Store.dbml"), """
            <Database Name="Store" xmlns="http://schemas.microsoft.com/linqtosql/dbml/2007">
              <Table Name="Customers" Member="Customers">
                <Type Name="Customer"><Column Name="CustomerId" Member="CustomerId" /></Type>
              </Table>
            </Database>
            """);
        File.WriteAllText(Path.Combine(temp.Path, "Store.edmx"), """
            <edmx:Edmx xmlns:edmx="http://schemas.microsoft.com/ado/2009/11/edmx" Version="3.0">
              <edmx:Runtime>
                <edmx:ConceptualModels>
                  <Schema xmlns="http://schemas.microsoft.com/ado/2009/11/edm" Namespace="Model">
                    <EntityType Name="Customer"><Property Name="CustomerId" Type="Int32" /></EntityType>
                  </Schema>
                </edmx:ConceptualModels>
              </edmx:Runtime>
            </edmx:Edmx>
            """);

        var result = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, "out")));

        var dbmlCustomer = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.LegacyDataEntityDeclared
            && fact.RuleId == RuleIds.LegacyDataDbml
            && fact.Properties.GetValueOrDefault("entityName") == "Customer");
        var edmxCustomer = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.LegacyDataEntityDeclared
            && fact.RuleId == RuleIds.LegacyDataEdmx
            && fact.Properties.GetValueOrDefault("entityName") == "Customer");

        Assert.Equal("dbml", dbmlCustomer.Properties.GetValueOrDefault("metadataFormat"));
        Assert.Equal("edmx", edmxCustomer.Properties.GetValueOrDefault("metadataFormat"));
        Assert.NotEqual(dbmlCustomer.Properties.GetValueOrDefault("stableModelKey"), edmxCustomer.Properties.GetValueOrDefault("stableModelKey"));
    }

    [Fact]
    public async Task Scan_hashes_unsafe_model_display_names_in_facts_and_sqlite()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "Unsafe.dbml"), """
            <Database Name="Store" xmlns="http://schemas.microsoft.com/linqtosql/dbml/2007">
              <Table Name="Server=prod;Database=Secret" Member="Customers">
                <Type Name="Customer"><Column Name="CustomerId" Member="CustomerId" /></Type>
              </Table>
            </Database>
            """);

        var result = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, "out")));
        var storage = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.LegacyDataStorageObjectDeclared
            && fact.RuleId == RuleIds.LegacyDataDbml);
        var indexPath = Path.Combine(temp.Path, "out", "index.sqlite");
        SqliteIndexWriter.Write(indexPath, result.Manifest, result.Facts);
        var allProperties = await ReadAllPropertiesAsync(indexPath);

        Assert.False(storage.Properties.ContainsKey("displayName"));
        Assert.True(storage.Properties.ContainsKey("displayNameHash"));
        Assert.Equal("hashed-unsafe-identifier", storage.Properties.GetValueOrDefault("displayNameRedaction"));
        Assert.StartsWith("ldm:", storage.Properties.GetValueOrDefault("stableModelKey"));
        Assert.DoesNotContain("Server=prod", allProperties);
        Assert.DoesNotContain("Database=Secret", allProperties);
    }

    [Fact]
    public void Scan_clears_stale_safe_display_when_reused_properties_become_unsafe()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "UnsafeTable.xsd"), """
            <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema"
                       xmlns:msdata="urn:schemas-microsoft-com:xml-msdata"
                       xmlns:msprop="urn:schemas-microsoft-com:xml-msprop">
              <xs:element name="OrdersDataSet" msdata:IsDataSet="true" msprop:Generator_DataSetName="OrdersDataSet">
                <xs:complexType>
                  <xs:choice maxOccurs="unbounded">
                    <xs:element name="Orders" msprop:Generator_UserTableName="Server=prod;Database=Secret" msprop:Generator_RowClassName="OrdersRow" />
                  </xs:choice>
                </xs:complexType>
              </xs:element>
            </xs:schema>
            """);

        var result = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, "out")));

        var storage = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.LegacyDataStorageObjectDeclared
            && fact.RuleId == RuleIds.LegacyDataTypedDataSet);
        Assert.False(storage.Properties.ContainsKey("displayName"));
        Assert.True(storage.Properties.ContainsKey("displayNameHash"));
        Assert.Equal("hashed-unsafe-identifier", storage.Properties.GetValueOrDefault("displayNameRedaction"));
        Assert.DoesNotContain("OrdersRow", storage.Properties.GetValueOrDefault("displayName") ?? string.Empty);
    }

    [Fact]
    public void Scan_marks_model_identity_coverage_reduced_when_metadata_gaps_exist()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "Reduced.dbml"), """
            <Root xmlns="http://schemas.microsoft.com/linqtosql/dbml/2007">
              <Database Name="First">
                <Table Name="Customers" Member="Customers"><Type Name="Customer" /></Table>
              </Database>
              <Database Name="Second" />
            </Root>
            """);

        var result = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, "out")));

        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.AnalysisGap
            && fact.Properties.GetValueOrDefault("classification") == "UnsupportedLegacyDataMetadataVersion");
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.LegacyDataEntityDeclared
            && fact.RuleId == RuleIds.LegacyDataDbml
            && fact.Properties.GetValueOrDefault("coverageLabel") == "reduced");
    }

    [Fact]
    public void Scan_keeps_stable_model_key_across_line_only_metadata_changes()
    {
        using var temp = new TempDirectory();
        var modelPath = Path.Combine(temp.Path, "Model.dbml");
        File.WriteAllText(modelPath, """
            <Database Name="Store" xmlns="http://schemas.microsoft.com/linqtosql/dbml/2007">
              <Table Name="Customers" Member="Customers"><Type Name="Customer" /></Table>
            </Database>
            """);
        var first = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, "out1")));

        File.WriteAllText(modelPath, """
            <Database Name="Store" xmlns="http://schemas.microsoft.com/linqtosql/dbml/2007">


              <Table Name="Customers" Member="Customers">
                <Type Name="Customer" />
              </Table>
            </Database>
            """);
        var second = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, "out2")));

        var firstKey = Assert.Single(first.Facts, fact => fact.FactType == FactTypes.LegacyDataEntityDeclared
            && fact.RuleId == RuleIds.LegacyDataDbml
            && fact.Properties.GetValueOrDefault("displayName") == "Customer").Properties.GetValueOrDefault("stableModelKey");
        var secondKey = Assert.Single(second.Facts, fact => fact.FactType == FactTypes.LegacyDataEntityDeclared
            && fact.RuleId == RuleIds.LegacyDataDbml
            && fact.Properties.GetValueOrDefault("displayName") == "Customer").Properties.GetValueOrDefault("stableModelKey");

        Assert.Equal(firstKey, secondKey);
    }

    [Fact]
    public async Task Scan_does_not_upgrade_descriptor_tier_above_tier2_for_generated_code_link()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "Model.dbml"), """
            <Database Name="Store" Class="StoreContext" xmlns="http://schemas.microsoft.com/linqtosql/dbml/2007">
              <Table Name="Customers" Member="Customers"><Type Name="Customer" /></Table>
            </Database>
            """);
        File.WriteAllText(Path.Combine(temp.Path, "Model.designer.cs"), """
            namespace Store;
            public partial class Customer { }
            public partial class StoreContext { }
            """);

        var result = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, "out")));
        var indexPath = Path.Combine(temp.Path, "out", "index.sqlite");
        SqliteIndexWriter.Write(indexPath, result.Manifest, result.Facts);
        var allProperties = await ReadAllPropertiesAsync(indexPath);

        var entity = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.LegacyDataEntityDeclared
            && fact.RuleId == RuleIds.LegacyDataDbml
            && fact.Properties.GetValueOrDefault("displayName") == "Customer");
        var link = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.LegacyDataGeneratedCodeLinked
            && fact.TargetSymbol == "Customer");

        Assert.Equal(EvidenceTiers.Tier2Structural, entity.EvidenceTier);
        Assert.Equal(EvidenceTiers.Tier2Structural, entity.Properties.GetValueOrDefault("modelIdentityEvidenceTier"));
        Assert.Equal(EvidenceTiers.Tier2Structural, link.EvidenceTier);
        Assert.Equal("explicit-generated-file", link.Properties.GetValueOrDefault("linkKind"));
        Assert.Equal("generated-entity", link.Properties.GetValueOrDefault("symbolRole"));
        Assert.Equal("dbml", link.Properties.GetValueOrDefault("metadataFormat"));
        Assert.Equal("full", link.Properties.GetValueOrDefault("coverageLabel"));
        Assert.Equal("generated-code-freshness-unverified", link.Properties.GetValueOrDefault("limitations"));
        Assert.Equal(entity.FactId, link.Properties.GetValueOrDefault("sourceMetadataFactId"));
        Assert.Equal(entity.FactId, link.Properties.GetValueOrDefault("supportingFactIds"));
        Assert.Equal(entity.Properties.GetValueOrDefault("stableModelKey"), link.Properties.GetValueOrDefault("stableModelKey"));
        Assert.False(link.Properties.ContainsKey("supportingFactId"));
        Assert.False(link.Properties.ContainsKey("generatedCodeFilePath"));
        Assert.DoesNotContain(temp.Path, allProperties);
    }

    [Fact]
    public void Scan_gaps_duplicate_type_declarations_in_generated_designer_without_choosing_a_winner()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "Model.dbml"), """
            <Database Name="Store" Class="StoreContext" xmlns="http://schemas.microsoft.com/linqtosql/dbml/2007">
              <Table Name="Customers" Member="Customers">
                <Type Name="Customer" />
              </Table>
            </Database>
            """);
        File.WriteAllText(Path.Combine(temp.Path, "Model.designer.cs"), """
            namespace Store.One
            {
                public partial class Customer { }
            }
            namespace Store.Two
            {
                public partial class Customer { }
            }
            """);

        var result = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, "out")));

        Assert.DoesNotContain(result.Facts, fact => fact.FactType == FactTypes.LegacyDataGeneratedCodeLinked
            && fact.TargetSymbol == "Customer");
        var gap = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyDataGeneratedLink
            && fact.Evidence.FilePath == "Model.dbml"
            && fact.Properties.GetValueOrDefault("classification") == "AmbiguousGeneratedCodeLink");
        Assert.Equal(3, gap.Evidence.StartLine);
    }

    [Fact]
    public void Scan_gaps_missing_explicit_dbml_generated_designer()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "Model.dbml"), """
            <Database Name="Store" Class="StoreContext" xmlns="http://schemas.microsoft.com/linqtosql/dbml/2007">
              <Table Name="Customers" Member="Customers">
                <Type Name="Customer" />
              </Table>
            </Database>
            """);

        var result = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, "out")));

        Assert.DoesNotContain(result.Facts, fact => fact.FactType == FactTypes.LegacyDataGeneratedCodeLinked
            && fact.RuleId == RuleIds.LegacyDataGeneratedLink);
        var gap = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyDataGeneratedLink
            && fact.Evidence.FilePath == "Model.dbml"
            && fact.Properties.GetValueOrDefault("classification") == "MissingGeneratedCode");
        Assert.Equal(3, gap.Evidence.StartLine);
        Assert.False(string.IsNullOrWhiteSpace(gap.Properties.GetValueOrDefault("sourceMetadataFactId")));
        Assert.False(string.IsNullOrWhiteSpace(gap.Properties.GetValueOrDefault("supportingFactIds")));
        Assert.Equal("Customer", gap.Properties.GetValueOrDefault("typeName"));
    }

    [Fact]
    public void Scan_emits_distinct_generated_link_gap_ids_for_same_line_descriptors()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "Model.dbml"), """
            <Database Name="Store" Class="StoreContext" xmlns="http://schemas.microsoft.com/linqtosql/dbml/2007"><Table Name="Customers" Member="Customers"><Type Name="Customer" /></Table><Table Name="Orders" Member="Orders"><Type Name="Order" /></Table></Database>
            """);

        var result = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, "out")));
        var indexPath = Path.Combine(temp.Path, "out", "index.sqlite");
        SqliteIndexWriter.Write(indexPath, result.Manifest, result.Facts);

        var gaps = result.Facts
            .Where(fact => fact.FactType == FactTypes.AnalysisGap
                && fact.RuleId == RuleIds.LegacyDataGeneratedLink
                && fact.Properties.GetValueOrDefault("classification") == "MissingGeneratedCode")
            .OrderBy(fact => fact.TargetSymbol, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(2, gaps.Length);
        Assert.Equal(2, gaps.Select(fact => fact.FactId).Distinct(StringComparer.Ordinal).Count());
        Assert.All(gaps, gap =>
        {
            Assert.Equal(1, gap.Evidence.StartLine);
            Assert.False(string.IsNullOrWhiteSpace(gap.Properties.GetValueOrDefault("sourceMetadataFactId")));
            Assert.Equal(gap.Properties.GetValueOrDefault("sourceMetadataFactId"), gap.Properties.GetValueOrDefault("supportingFactIds"));
        });
    }

    [Fact]
    public void Scan_marks_edmx_generated_designer_syntax_fallback_as_reduced_coverage()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "Model.edmx"), """
            <edmx:Edmx xmlns:edmx="http://schemas.microsoft.com/ado/2009/11/edmx" Version="3.0">
              <edmx:Runtime>
                <edmx:ConceptualModels>
                  <Schema xmlns="http://schemas.microsoft.com/ado/2009/11/edm" Namespace="Model">
                    <EntityContainer Name="ModelContainer"><EntitySet Name="Customers" EntityType="Model.Customer" /></EntityContainer>
                    <EntityType Name="Customer"><Property Name="CustomerId" Type="Int32" /></EntityType>
                  </Schema>
                </edmx:ConceptualModels>
                <edmx:StorageModels>
                  <Schema xmlns="http://schemas.microsoft.com/ado/2009/11/edm/ssdl" Namespace="Store">
                    <EntityContainer Name="StoreContainer"><EntitySet Name="Customers" EntityType="Store.Customers" Table="Customers" /></EntityContainer>
                    <EntityType Name="Customers"><Property Name="CustomerId" Type="int" /></EntityType>
                  </Schema>
                </edmx:StorageModels>
                <edmx:Mappings>
                  <Mapping xmlns="http://schemas.microsoft.com/ado/2009/11/mapping/cs">
                    <EntityContainerMapping StorageEntityContainer="StoreContainer" CdmEntityContainer="ModelContainer">
                      <EntitySetMapping Name="Customers">
                        <EntityTypeMapping TypeName="Model.Customer">
                          <MappingFragment StoreEntitySet="Customers">
                            <ScalarProperty Name="CustomerId" ColumnName="CustomerId" />
                          </MappingFragment>
                        </EntityTypeMapping>
                      </EntitySetMapping>
                    </EntityContainerMapping>
                  </Mapping>
                </edmx:Mappings>
              </edmx:Runtime>
            </edmx:Edmx>
            """);
        File.WriteAllText(Path.Combine(temp.Path, "Model.Generated.designer.cs"), """
            namespace Model;
            public partial class Customer { }
            """);

        var result = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, "out")));

        var entity = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.LegacyDataEntityDeclared
            && fact.RuleId == RuleIds.LegacyDataEdmx
            && fact.Properties.GetValueOrDefault("typeName") == "Customer");
        var link = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.LegacyDataGeneratedCodeLinked
            && fact.RuleId == RuleIds.LegacyDataGeneratedLink
            && fact.TargetSymbol == "Customer");

        Assert.Equal(EvidenceTiers.Tier3SyntaxOrTextual, link.EvidenceTier);
        Assert.Equal("type-name-syntax-fallback", link.Properties.GetValueOrDefault("linkKind"));
        Assert.Equal("generated-entity", link.Properties.GetValueOrDefault("symbolRole"));
        Assert.Equal("edmx", link.Properties.GetValueOrDefault("metadataFormat"));
        Assert.Equal("reduced", link.Properties.GetValueOrDefault("coverageLabel"));
        Assert.Equal("generated-code-freshness-unverified;syntax-only-generated-code-link", link.Properties.GetValueOrDefault("limitations"));
        Assert.Equal(entity.FactId, link.Properties.GetValueOrDefault("sourceMetadataFactId"));
        Assert.Equal(entity.FactId, link.Properties.GetValueOrDefault("supportingFactIds"));
        Assert.Equal(entity.Properties.GetValueOrDefault("stableModelKey"), link.Properties.GetValueOrDefault("stableModelKey"));
    }

    [Fact]
    public async Task Scan_emits_unsupported_old_orm_gaps_without_inventing_model_facts_or_leaking_content()
    {
        using var temp = new TempDirectory();
        Directory.CreateDirectory(Path.Combine(temp.Path, "Mappings"));
        Directory.CreateDirectory(Path.Combine(temp.Path, "sqlmap"));
        Directory.CreateDirectory(Path.Combine(temp.Path, "Orm"));
        Directory.CreateDirectory(Path.Combine(temp.Path, "Domain"));
        File.WriteAllText(Path.Combine(temp.Path, "Mappings", "Customer.sqlmap.xml"), """
            <sqlMap namespace="CustomerSecret" xmlns="http://ibatis.apache.org/mapping">
              <select id="GetCustomers">SELECT * FROM Customers WHERE ApiSecret = 'hidden'</select>
            </sqlMap>
            """);
        File.WriteAllText(Path.Combine(temp.Path, "sqlmap", "order.xml"), """
            <sqlMap namespace="OrderSecret" xmlns="http://ibatis.apache.org/mapping">
              <select id="GetOrders">SELECT * FROM Orders WHERE ApiSecret = 'hidden'</select>
            </sqlMap>
            """);
        File.WriteAllText(Path.Combine(temp.Path, "Orm", "Model.llblgenproj"), """
            <LLBLGenProject>
              <ConnectionString>Server=prod-db;Password=super-secret</ConnectionString>
            </LLBLGenProject>
            """);
        File.WriteAllText(Path.Combine(temp.Path, "SubSonic.config"), """
            <configuration>
              <configSections>
                <section name="SubSonicService" type="SubSonic.SubSonicSection, SubSonic" />
              </configSections>
              <SubSonicService defaultProvider="Server=prod-db;Password=super-secret" />
            </configuration>
            """);
        File.WriteAllText(Path.Combine(temp.Path, "RootSubSonic.config"), """
            <SubSonicService defaultProvider="Server=prod-db;Password=super-secret" />
            """);
        File.WriteAllText(Path.Combine(temp.Path, "Domain", "Customer.cs"), """
            using Castle.ActiveRecord;

            [ActiveRecord("Server=prod-db;Password=super-secret")]
            public sealed class CustomerRecord
            {
                [PrimaryKey]
                public int Id { get; set; }
            }
            """);
        File.WriteAllText(Path.Combine(temp.Path, "Domain", "Noise.cs"), """
            public static class Noise
            {
                // This project is not using SubSonic or LLBLGen.
                public const string Message = "MyBatis and iBATIS are not configured here";
            }
            """);

        var result = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, "out")));
        var report = MarkdownReportWriter.Build(result);
        var indexPath = Path.Combine(temp.Path, "out", "index.sqlite");
        SqliteIndexWriter.Write(indexPath, result.Manifest, result.Facts);
        var allProperties = await ReadAllPropertiesAsync(indexPath);

        Assert.Contains(result.Inventory, item => item.Kind == "LegacyOrmMetadata" && item.RelativePath == "Mappings/Customer.sqlmap.xml");
        Assert.Contains(result.Inventory, item => item.Kind == "LegacyOrmMetadata" && item.RelativePath == "sqlmap/order.xml");
        Assert.Contains(result.Inventory, item => item.Kind == "LegacyOrmMetadata" && item.RelativePath == "Orm/Model.llblgenproj");

        var gaps = result.Facts
            .Where(fact => fact.FactType == FactTypes.AnalysisGap && fact.RuleId == RuleIds.LegacyDataOrmUnsupported)
            .ToArray();
        Assert.Contains(gaps, fact => fact.Properties.GetValueOrDefault("descriptorFamily") == "iBATIS.NET");
        Assert.Contains(gaps, fact => fact.Properties.GetValueOrDefault("descriptorFamily") == "LLBLGen");
        Assert.Contains(gaps, fact => fact.Properties.GetValueOrDefault("descriptorFamily") == "SubSonic");
        Assert.Contains(gaps, fact => fact.Evidence.FilePath == "RootSubSonic.config"
            && fact.Properties.GetValueOrDefault("descriptorFamily") == "SubSonic");
        Assert.Contains(gaps, fact => fact.Properties.GetValueOrDefault("descriptorFamily") == "Castle ActiveRecord");
        Assert.DoesNotContain(gaps, fact => fact.Evidence.FilePath == "Domain/Noise.cs");
        Assert.All(gaps, fact =>
        {
            Assert.Equal("UnsupportedLegacyOrmDescriptor", fact.Properties.GetValueOrDefault("classification"));
            Assert.Equal(EvidenceTiers.Tier4Unknown, fact.EvidenceTier);
            Assert.Equal("False", fact.Properties.GetValueOrDefault("runtimeProof"));
        });

        Assert.DoesNotContain(result.Facts, fact => fact.RuleId == RuleIds.LegacyDataOrmUnsupported
            && fact.FactType is FactTypes.LegacyDataEntityDeclared
                or FactTypes.LegacyDataStorageObjectDeclared
                or FactTypes.LegacyDataColumnDeclared
                or FactTypes.LegacyDataMappingDeclared);
        Assert.DoesNotContain("super-secret", report);
        Assert.DoesNotContain("prod-db", report);
        Assert.DoesNotContain("ApiSecret", report);
        Assert.DoesNotContain("super-secret", allProperties);
        Assert.DoesNotContain("prod-db", allProperties);
        Assert.DoesNotContain("ApiSecret", allProperties);
    }

    [Fact]
    public void Scan_extracts_nhibernate_hbm_metadata_with_model_identity_and_relationships()
    {
        using var temp = new TempDirectory();
        Directory.CreateDirectory(Path.Combine(temp.Path, "Mappings"));
        File.WriteAllText(Path.Combine(temp.Path, "Mappings", "Customer.hbm.xml"), """
            <hibernate-mapping xmlns="urn:nhibernate-mapping-2.2" namespace="Samples.Domain">
              <class name="Customer" table="Customers" schema="dbo">
                <id name="Id" column="CustomerId" />
                <version name="RowVersion" column="RowVersion" />
                <property name="Status" column="Status" not-null="true" />
                <property name="Nickname" column="Nickname" not-null="FALSE" />
                <many-to-one name="Account" class="Account" column="AccountId" />
                <one-to-one name="Profile" class="CustomerProfile" />
                <set name="Orders">
                  <key>
                    <column name="CustomerId" />
                  </key>
                  <one-to-many class="Order" />
                </set>
                <bag name="Tags">
                  <key column="CustomerId" />
                  <many-to-many class="Tag" />
                </bag>
              </class>
            </hibernate-mapping>
            """);

        var result = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, "out")));
        var second = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, "out2")));

        Assert.Contains(result.Inventory, item => item.Kind == "LegacyOrmMetadata" && item.RelativePath == "Mappings/Customer.hbm.xml");
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.LegacyDataMetadataDeclared
            && fact.RuleId == RuleIds.LegacyDataOrmNHibernate
            && fact.Properties.GetValueOrDefault("metadataFormat") == "nhibernate-hbm");

        var entity = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.LegacyDataEntityDeclared
            && fact.RuleId == RuleIds.LegacyDataOrmNHibernate
            && fact.Properties.GetValueOrDefault("typeName") == "Customer");
        Assert.Equal("nhibernate-hbm", entity.Properties.GetValueOrDefault("metadataFormat"));
        Assert.Equal("entity", entity.Properties.GetValueOrDefault("modelKind"));
        Assert.Equal("orm-mapped", entity.Properties.GetValueOrDefault("descriptorRole"));
        Assert.Equal(RuleIds.LegacyDataModelIdentity, entity.Properties.GetValueOrDefault("modelIdentityRuleId"));
        Assert.StartsWith("ldm:", entity.Properties.GetValueOrDefault("stableModelKey"));
        Assert.True(entity.Properties.ContainsKey("schemaHash"));
        Assert.False(entity.Properties.ContainsKey("schemaName"));

        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.LegacyDataStorageObjectDeclared
            && fact.RuleId == RuleIds.LegacyDataOrmNHibernate
            && fact.Properties.GetValueOrDefault("storageObjectName") == "Customers");
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.LegacyDataColumnDeclared
            && fact.RuleId == RuleIds.LegacyDataOrmNHibernate
            && fact.Properties.GetValueOrDefault("propertyName") == "Status"
            && fact.Properties.GetValueOrDefault("columnName") == "Status"
            && fact.Properties.GetValueOrDefault("isNullable") == "False");
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.LegacyDataColumnDeclared
            && fact.RuleId == RuleIds.LegacyDataOrmNHibernate
            && fact.Properties.GetValueOrDefault("propertyName") == "Nickname"
            && fact.Properties.GetValueOrDefault("columnName") == "Nickname"
            && fact.Properties.GetValueOrDefault("isNullable") == "True");

        var account = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.LegacyDataMappingDeclared
            && fact.RuleId == RuleIds.LegacyDataOrmNHibernate
            && fact.Properties.GetValueOrDefault("associationName") == "Account");
        Assert.Equal("many-to-one", account.Properties.GetValueOrDefault("mappingKind"));
        Assert.Equal("relationship", account.Properties.GetValueOrDefault("modelRelationshipKind"));
        Assert.Equal("Customer", account.Properties.GetValueOrDefault("sourceEndpointName"));
        Assert.Equal("Account", account.Properties.GetValueOrDefault("targetEndpointName"));
        Assert.Equal("full", account.Properties.GetValueOrDefault("relationshipEndpointCoverage"));

        var orders = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.LegacyDataMappingDeclared
            && fact.RuleId == RuleIds.LegacyDataOrmNHibernate
            && fact.Properties.GetValueOrDefault("associationName") == "Orders");
        Assert.Equal("set", orders.Properties.GetValueOrDefault("mappingKind"));
        Assert.Equal("Order", orders.Properties.GetValueOrDefault("targetEndpointName"));
        Assert.Equal("CustomerId", orders.Properties.GetValueOrDefault("columnName"));

        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.LegacyDataMappingDeclared
            && fact.RuleId == RuleIds.LegacyDataOrmNHibernate
            && fact.Properties.GetValueOrDefault("mappingKind") == "one-to-one"
            && fact.Properties.GetValueOrDefault("targetEndpointName") == "CustomerProfile"
            && fact.Properties.GetValueOrDefault("relationshipEndpointCoverage") == "full");
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.LegacyDataMappingDeclared
            && fact.RuleId == RuleIds.LegacyDataOrmNHibernate
            && fact.Properties.GetValueOrDefault("mappingKind") == "bag"
            && fact.Properties.GetValueOrDefault("targetEndpointName") == "Tag"
            && fact.Properties.GetValueOrDefault("columnName") == "CustomerId"
            && fact.Properties.GetValueOrDefault("relationshipEndpointCoverage") == "full");

        Assert.Equal(
            result.Facts.Where(fact => fact.RuleId == RuleIds.LegacyDataOrmNHibernate).Select(fact => fact.FactId).Order(StringComparer.Ordinal),
            second.Facts.Where(fact => fact.RuleId == RuleIds.LegacyDataOrmNHibernate).Select(fact => fact.FactId).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Scan_classifies_nhibernate_missing_and_ambiguous_relationship_endpoints_without_invention()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "Relationships.hbm.xml"), """
            <hibernate-mapping xmlns="urn:nhibernate-mapping-2.2">
              <class name="Customer" table="Customers">
                <many-to-one name="MissingTarget" />
                <set name="AmbiguousCollection">
                  <one-to-many class="Order" />
                  <many-to-many class="Tag" />
                </set>
              </class>
              <class table="Unidentified">
                <many-to-one name="KnownTarget" class="Account" />
                <one-to-one name="MissingBoth" />
              </class>
            </hibernate-mapping>
            """);

        var result = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, "out")));
        var repeated = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, "out-repeat")));

        var missingTarget = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.LegacyDataMappingDeclared
            && fact.RuleId == RuleIds.LegacyDataOrmNHibernate
            && fact.Properties.GetValueOrDefault("associationName") == "MissingTarget");
        Assert.Equal("reduced", missingTarget.Properties.GetValueOrDefault("coverageLabel"));
        Assert.Equal("unidirectional", missingTarget.Properties.GetValueOrDefault("relationshipEndpointCoverage"));
        Assert.Equal("missing-target-endpoint", missingTarget.Properties.GetValueOrDefault("limitations"));
        Assert.Equal("Customer", missingTarget.Properties.GetValueOrDefault("sourceEndpointName"));
        Assert.False(missingTarget.Properties.ContainsKey("targetEndpointName"));

        var missingSource = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.LegacyDataMappingDeclared
            && fact.RuleId == RuleIds.LegacyDataOrmNHibernate
            && fact.Properties.GetValueOrDefault("associationName") == "KnownTarget");
        Assert.Equal("reduced", missingSource.Properties.GetValueOrDefault("coverageLabel"));
        Assert.Equal("unidirectional", missingSource.Properties.GetValueOrDefault("relationshipEndpointCoverage"));
        Assert.Equal("missing-source-endpoint", missingSource.Properties.GetValueOrDefault("limitations"));
        Assert.False(missingSource.Properties.ContainsKey("sourceEndpointName"));
        Assert.Equal("Account", missingSource.Properties.GetValueOrDefault("targetEndpointName"));

        Assert.DoesNotContain(result.Facts, fact => fact.FactType == FactTypes.LegacyDataMappingDeclared
            && fact.Properties.GetValueOrDefault("associationName") is "AmbiguousCollection" or "MissingBoth");
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyDataOrmNHibernate
            && fact.Properties.GetValueOrDefault("classification") == "AmbiguousLegacyDataModelIdentity"
            && fact.Properties.GetValueOrDefault("relationshipFamily") == "nhibernate-hbm"
            && fact.Properties.GetValueOrDefault("descriptorKind") == "set"
            && fact.Properties.GetValueOrDefault("safeReasonCode") == "ambiguous-endpoint-candidates");
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyDataModelRelationship
            && fact.Properties.GetValueOrDefault("classification") == "IncompleteLegacyDataModelRelationship"
            && fact.Properties.GetValueOrDefault("relationshipFamily") == "nhibernate-hbm"
            && fact.Properties.GetValueOrDefault("descriptorKind") == "one-to-one"
            && fact.Properties.GetValueOrDefault("safeReasonCode") == "missing-endpoint");

        Assert.Equal(
            result.Facts.Where(IsNHibernateRelationshipEvidence).Select(fact => fact.FactId).Order(StringComparer.Ordinal),
            repeated.Facts.Where(IsNHibernateRelationshipEvidence).Select(fact => fact.FactId).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Scan_links_nhibernate_mapped_class_to_scoped_csharp_type_syntax()
    {
        using var temp = new TempDirectory();
        Directory.CreateDirectory(Path.Combine(temp.Path, "Mappings"));
        Directory.CreateDirectory(Path.Combine(temp.Path, "Domain"));
        File.WriteAllText(Path.Combine(temp.Path, "Mappings", "Customer.hbm.xml"), """
            <hibernate-mapping xmlns="urn:nhibernate-mapping-2.2" namespace="Samples.Domain">
              <class name="Customer" table="Customers">
                <id name="Id" column="CustomerId" />
              </class>
            </hibernate-mapping>
            """);
        File.WriteAllText(Path.Combine(temp.Path, "Domain", "Customer.cs"), """
            namespace Samples.Domain;

            public partial class Customer
            {
                public int Id { get; set; }
            }
            """);

        var result = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, "out")));
        var report = MarkdownReportWriter.Build(result);
        var indexPath = Path.Combine(temp.Path, "out", "index.sqlite");
        SqliteIndexWriter.Write(indexPath, result.Manifest, result.Facts);
        var allProperties = await ReadAllPropertiesAsync(indexPath);

        var entity = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.LegacyDataEntityDeclared
            && fact.RuleId == RuleIds.LegacyDataOrmNHibernate
            && fact.Properties.GetValueOrDefault("typeName") == "Customer");
        var link = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.LegacyDataGeneratedCodeLinked
            && fact.RuleId == RuleIds.LegacyDataModelGeneratedLink);

        Assert.Equal("Samples.Domain.Customer", entity.Properties.GetValueOrDefault("mappedTypeName"));
        Assert.Equal(EvidenceTiers.Tier2Structural, entity.EvidenceTier);
        Assert.Equal(EvidenceTiers.Tier3SyntaxOrTextual, link.EvidenceTier);
        var entityAfterLink = Assert.Single(result.Facts, fact => fact.FactId == entity.FactId);
        Assert.Equal(EvidenceTiers.Tier2Structural, entityAfterLink.EvidenceTier);
        Assert.Equal("mapped-type-syntax", link.Properties.GetValueOrDefault("linkKind"));
        Assert.Equal("mapped-class", link.Properties.GetValueOrDefault("symbolRole"));
        Assert.Equal("nhibernate-hbm", link.Properties.GetValueOrDefault("metadataFormat"));
        Assert.Equal("Samples.Domain.Customer", link.Properties.GetValueOrDefault("mappedTypeName"));
        Assert.Equal("Samples.Domain.Customer", link.Properties.GetValueOrDefault("typeName"));
        Assert.Equal("Customer.cs", link.Properties.GetValueOrDefault("generatedCodeFileName"));
        Assert.Equal("reduced", link.Properties.GetValueOrDefault("coverageLabel"));
        Assert.Equal("syntax-only-mapped-type-link", link.Properties.GetValueOrDefault("limitations"));
        Assert.Equal(entity.FactId, link.Properties.GetValueOrDefault("sourceMetadataFactId"));
        Assert.Equal(entity.FactId, link.Properties.GetValueOrDefault("supportingFactIds"));
        Assert.Equal(entity.Properties.GetValueOrDefault("stableModelKey"), link.Properties.GetValueOrDefault("stableModelKey"));
        Assert.Equal("Domain/Customer.cs", link.Evidence.FilePath);
        Assert.DoesNotContain(result.Facts, fact => fact.RuleId == RuleIds.LegacyDataModelGeneratedLink
            && fact.Evidence.FilePath.Contains(temp.Path, StringComparison.Ordinal));
        Assert.DoesNotContain(temp.Path, report);
        Assert.DoesNotContain(temp.Path, allProperties);
        Assert.All(result.Facts.Where(fact => fact.RuleId == RuleIds.LegacyDataModelGeneratedLink), fact =>
        {
            Assert.DoesNotContain(temp.Path, fact.Evidence.FilePath);
            Assert.DoesNotContain(temp.Path, string.Join("\n", fact.Properties.Values));
        });
    }

    [Fact]
    public void Scan_links_fully_qualified_nhibernate_class_name_to_csharp_type_syntax()
    {
        using var temp = new TempDirectory();
        Directory.CreateDirectory(Path.Combine(temp.Path, "Mappings"));
        Directory.CreateDirectory(Path.Combine(temp.Path, "Domain"));
        File.WriteAllText(Path.Combine(temp.Path, "Mappings", "Order.hbm.xml"), """
            <hibernate-mapping xmlns="urn:nhibernate-mapping-2.2">
              <class name="Samples.Domain.Order, Samples.Domain" table="Orders">
                <id name="Id" column="OrderId" />
              </class>
            </hibernate-mapping>
            """);
        File.WriteAllText(Path.Combine(temp.Path, "Domain", "Order.cs"), """
            namespace Samples.Domain;
            public sealed class Order { }
            """);

        var result = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, "out")));

        var link = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.LegacyDataGeneratedCodeLinked
            && fact.RuleId == RuleIds.LegacyDataModelGeneratedLink);
        Assert.Equal("Samples.Domain.Order", link.Properties.GetValueOrDefault("mappedTypeName"));
        Assert.Equal("Samples.Domain.Order", link.Properties.GetValueOrDefault("typeName"));
        Assert.Equal("Domain/Order.cs", link.Evidence.FilePath);
    }

    [Fact]
    public void Scan_links_nhibernate_nested_class_name_to_csharp_nested_type_syntax()
    {
        using var temp = new TempDirectory();
        Directory.CreateDirectory(Path.Combine(temp.Path, "Mappings"));
        Directory.CreateDirectory(Path.Combine(temp.Path, "Domain"));
        File.WriteAllText(Path.Combine(temp.Path, "Mappings", "Address.hbm.xml"), """
            <hibernate-mapping xmlns="urn:nhibernate-mapping-2.2">
              <class name="Samples.Domain.Customer+Address" table="Addresses">
                <id name="Id" column="AddressId" />
              </class>
            </hibernate-mapping>
            """);
        File.WriteAllText(Path.Combine(temp.Path, "Domain", "Customer.cs"), """
            namespace Samples.Domain;
            public sealed class Customer
            {
                public sealed class Address { }
            }
            """);

        var result = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, "out")));

        var link = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.LegacyDataGeneratedCodeLinked
            && fact.RuleId == RuleIds.LegacyDataModelGeneratedLink);
        Assert.Equal("Samples.Domain.Customer+Address", link.Properties.GetValueOrDefault("mappedTypeName"));
        Assert.Equal("Samples.Domain.Customer.Address", link.Properties.GetValueOrDefault("typeName"));
        Assert.Equal("Domain/Customer.cs", link.Evidence.FilePath);
        Assert.DoesNotContain(result.Facts, fact => fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyDataModelGeneratedLink
            && fact.Properties.GetValueOrDefault("classification") == "MissingGeneratedCode");
    }

    [Fact]
    public void Scan_gaps_missing_nhibernate_mapped_class_syntax_declaration()
    {
        using var temp = new TempDirectory();
        Directory.CreateDirectory(Path.Combine(temp.Path, "Mappings"));
        File.WriteAllText(Path.Combine(temp.Path, "Mappings", "Customer.hbm.xml"), """
            <hibernate-mapping xmlns="urn:nhibernate-mapping-2.2">
              <class name="Samples.Domain.Customer" table="Customers">
                <id name="Id" column="CustomerId" />
              </class>
            </hibernate-mapping>
            """);

        var result = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, "out")));

        Assert.DoesNotContain(result.Facts, fact => fact.FactType == FactTypes.LegacyDataGeneratedCodeLinked
            && fact.RuleId == RuleIds.LegacyDataModelGeneratedLink);
        var gap = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyDataModelGeneratedLink
            && fact.Evidence.FilePath == "Mappings/Customer.hbm.xml"
            && fact.Properties.GetValueOrDefault("classification") == "MissingGeneratedCode");
        Assert.Equal(2, gap.Evidence.StartLine);
    }

    [Fact]
    public void Scan_gaps_oversized_csharp_files_during_nhibernate_mapped_type_resolution()
    {
        using var temp = new TempDirectory();
        Directory.CreateDirectory(Path.Combine(temp.Path, "Mappings"));
        Directory.CreateDirectory(Path.Combine(temp.Path, "Domain"));
        File.WriteAllText(Path.Combine(temp.Path, "Mappings", "Customer.hbm.xml"), """
            <hibernate-mapping xmlns="urn:nhibernate-mapping-2.2">
              <class name="Samples.Domain.Customer" table="Customers">
                <id name="Id" column="CustomerId" />
              </class>
            </hibernate-mapping>
            """);
        File.WriteAllText(
            Path.Combine(temp.Path, "Domain", "Customer.cs"),
            """
            namespace Samples.Domain;
            public sealed class Customer { }
            """
            + new string(' ', 2 * 1024 * 1024));

        var result = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, "out")));

        Assert.DoesNotContain(result.Facts, fact => fact.FactType == FactTypes.LegacyDataGeneratedCodeLinked
            && fact.RuleId == RuleIds.LegacyDataModelGeneratedLink);
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyDataModelGeneratedLink
            && fact.Evidence.FilePath == "Domain/Customer.cs"
            && fact.Properties.GetValueOrDefault("classification") == "MappedTypeDeclarationFileTooLarge");
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyDataModelGeneratedLink
            && fact.Evidence.FilePath == "Mappings/Customer.hbm.xml"
            && fact.Properties.GetValueOrDefault("classification") == "MissingGeneratedCode");
    }

    [Fact]
    public void Scan_does_not_link_nhibernate_entity_name_as_clr_type_identity()
    {
        using var temp = new TempDirectory();
        Directory.CreateDirectory(Path.Combine(temp.Path, "Mappings"));
        Directory.CreateDirectory(Path.Combine(temp.Path, "Domain"));
        File.WriteAllText(Path.Combine(temp.Path, "Mappings", "Customer.hbm.xml"), """
            <hibernate-mapping xmlns="urn:nhibernate-mapping-2.2">
              <class entity-name="Samples.Domain.Customer" table="Customers">
                <id name="Id" column="CustomerId" />
              </class>
            </hibernate-mapping>
            """);
        File.WriteAllText(Path.Combine(temp.Path, "Domain", "Customer.cs"), """
            namespace Samples.Domain;
            public sealed class Customer { }
            """);

        var result = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, "out")));

        var entity = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.LegacyDataEntityDeclared
            && fact.RuleId == RuleIds.LegacyDataOrmNHibernate);
        Assert.Equal("Samples.Domain.Customer", entity.Properties.GetValueOrDefault("typeName"));
        Assert.False(entity.Properties.ContainsKey("mappedTypeName"));
        Assert.DoesNotContain(result.Facts, fact => fact.FactType == FactTypes.LegacyDataGeneratedCodeLinked
            && fact.RuleId == RuleIds.LegacyDataModelGeneratedLink);
        Assert.DoesNotContain(result.Facts, fact => fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyDataModelGeneratedLink);
    }

    [Fact]
    public void Scan_gaps_ambiguous_nhibernate_mapped_class_syntax_candidates()
    {
        using var temp = new TempDirectory();
        Directory.CreateDirectory(Path.Combine(temp.Path, "Mappings"));
        Directory.CreateDirectory(Path.Combine(temp.Path, "Domain"));
        File.WriteAllText(Path.Combine(temp.Path, "Mappings", "Customer.hbm.xml"), """
            <hibernate-mapping xmlns="urn:nhibernate-mapping-2.2">
              <class name="Samples.Domain.Customer" table="Customers">
                <id name="Id" column="CustomerId" />
              </class>
            </hibernate-mapping>
            """);
        File.WriteAllText(Path.Combine(temp.Path, "Domain", "Customer.Part1.cs"), """
            namespace Samples.Domain;
            public partial class Customer { }
            """);
        File.WriteAllText(Path.Combine(temp.Path, "Domain", "Customer.Part2.cs"), """
            namespace Samples.Domain;
            public partial class Customer { }
            """);

        var result = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, "out")));

        Assert.DoesNotContain(result.Facts, fact => fact.FactType == FactTypes.LegacyDataGeneratedCodeLinked
            && fact.RuleId == RuleIds.LegacyDataModelGeneratedLink);
        var gap = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyDataModelGeneratedLink
            && fact.Evidence.FilePath == "Mappings/Customer.hbm.xml"
            && fact.Properties.GetValueOrDefault("classification") == "AmbiguousGeneratedCodeLink");
        Assert.Equal(2, gap.Evidence.StartLine);
    }

    [Fact]
    public void Scan_does_not_use_global_short_name_matching_for_nhibernate_mapped_classes()
    {
        using var temp = new TempDirectory();
        Directory.CreateDirectory(Path.Combine(temp.Path, "Mappings"));
        Directory.CreateDirectory(Path.Combine(temp.Path, "Domain"));
        File.WriteAllText(Path.Combine(temp.Path, "Mappings", "Customer.hbm.xml"), """
            <hibernate-mapping xmlns="urn:nhibernate-mapping-2.2">
              <class name="Customer" table="Customers">
                <id name="Id" column="CustomerId" />
              </class>
            </hibernate-mapping>
            """);
        File.WriteAllText(Path.Combine(temp.Path, "Domain", "Customer.cs"), """
            namespace Samples.Domain;
            public partial class Customer { }
            """);

        var result = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, "out")));

        Assert.DoesNotContain(result.Facts, fact => fact.FactType == FactTypes.LegacyDataGeneratedCodeLinked
            && fact.RuleId == RuleIds.LegacyDataModelGeneratedLink);
        Assert.DoesNotContain(result.Facts, fact => fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyDataModelGeneratedLink);
    }

    [Fact]
    public void Scan_treats_non_mapping_hbm_xml_as_unsupported_orm_gap()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "Noise.hbm.xml"), """
            <not-hibernate>
              <class name="Customer" table="Customers" />
            </not-hibernate>
            """);

        var result = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, "out")));

        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyDataOrmUnsupported
            && fact.Evidence.FilePath == "Noise.hbm.xml"
            && fact.Properties.GetValueOrDefault("classification") == "UnsupportedLegacyOrmDescriptor");
        Assert.DoesNotContain(result.Facts, fact => fact.RuleId == RuleIds.LegacyDataOrmNHibernate);
    }

    [Fact]
    public async Task Scan_hashes_nhibernate_unsafe_values_and_gaps_unsupported_shapes()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "Unsafe.hbm.xml"), """
            <hibernate-mapping xmlns="urn:nhibernate-mapping-2.2">
              <class name="Server=prod;Password=secret" table="Customers;DROP" catalog="SensitiveCatalog">
                <id name="Id" column="CustomerId" />
                <many-to-one name="https://private.example/relation?token=secret" class="Server=remote;Password=secret" column="C:\private\CustomerId" />
                <property name="TokenSecret" formula="SELECT ApiSecret FROM Customers" />
                <component name="Address" />
                <filter name="tenant">TenantSecret = :tenant</filter>
                <sql-query name="UnsafeQuery">SELECT ApiSecret FROM Customers</sql-query>
              </class>
            </hibernate-mapping>
            """);

        var result = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, "out")));
        var report = MarkdownReportWriter.Build(result);
        var indexPath = Path.Combine(temp.Path, "out", "index.sqlite");
        SqliteIndexWriter.Write(indexPath, result.Manifest, result.Facts);
        var allProperties = await ReadAllPropertiesAsync(indexPath);

        var entity = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.LegacyDataEntityDeclared
            && fact.RuleId == RuleIds.LegacyDataOrmNHibernate);
        Assert.True(entity.Properties.ContainsKey("typeHash"));
        Assert.True(entity.Properties.ContainsKey("tableHash"));
        Assert.True(entity.Properties.ContainsKey("catalogHash"));
        Assert.DoesNotContain("Server=prod", entity.Properties.Values);
        Assert.DoesNotContain("Customers;DROP", entity.Properties.Values);
        Assert.DoesNotContain(result.Facts, fact => fact.FactType == FactTypes.LegacyDataColumnDeclared
            && fact.RuleId == RuleIds.LegacyDataOrmNHibernate
            && fact.Properties.GetValueOrDefault("descriptorSource") == "property");

        var relationship = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.LegacyDataMappingDeclared
            && fact.RuleId == RuleIds.LegacyDataOrmNHibernate
            && fact.Properties.GetValueOrDefault("mappingKind") == "many-to-one");
        Assert.Equal("reduced", relationship.Properties.GetValueOrDefault("coverageLabel"));
        Assert.Equal("full", relationship.Properties.GetValueOrDefault("relationshipEndpointCoverage"));
        Assert.Equal("unsafe-redacted-endpoint-identity", relationship.Properties.GetValueOrDefault("limitations"));
        Assert.True(relationship.Properties.ContainsKey("associationHash"));
        Assert.True(relationship.Properties.ContainsKey("sourceEndpointHash"));
        Assert.True(relationship.Properties.ContainsKey("targetEndpointHash"));
        Assert.True(relationship.Properties.ContainsKey("columnHash"));

        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyDataOrmNHibernate
            && fact.Properties.GetValueOrDefault("classification") == "UnsupportedLegacyOrmMappingShape");
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyDataOrmNHibernate
            && fact.Properties.GetValueOrDefault("classification") == "UnsupportedLegacyOrmMappingShape");

        Assert.DoesNotContain("ApiSecret", report);
        Assert.DoesNotContain("TenantSecret", report);
        Assert.DoesNotContain("Password=secret", report);
        Assert.DoesNotContain("ApiSecret", allProperties);
        Assert.DoesNotContain("TenantSecret", allProperties);
        Assert.DoesNotContain("Password=secret", allProperties);
        Assert.DoesNotContain("private.example", report);
        Assert.DoesNotContain("private.example", allProperties);
        Assert.DoesNotContain("C:\\private", report);
        Assert.DoesNotContain("C:\\private", allProperties);
    }

    [Fact]
    public void Scan_rejects_nhibernate_hbm_dtd_with_legacy_data_gap_classification()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "Unsafe.hbm.xml"), """
            <!DOCTYPE hibernate-mapping [
              <!ENTITY ext SYSTEM "file:///private/secret.txt">
            ]>
            <hibernate-mapping xmlns="urn:nhibernate-mapping-2.2">
              <class name="&ext;" table="Customers" />
            </hibernate-mapping>
            """);

        var result = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, "out")));

        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyDataOrmNHibernate
            && fact.Evidence.FilePath == "Unsafe.hbm.xml"
            && fact.Properties.GetValueOrDefault("classification") == "LegacyDataParserSecurityRejected");
        Assert.DoesNotContain(result.Facts, fact => fact.FactType.StartsWith("LegacyData", StringComparison.Ordinal)
            && fact.RuleId == RuleIds.LegacyDataOrmNHibernate);
    }

    private static bool IsTypedDataSetRelationshipEvidence(CodeFact fact)
    {
        return (fact.RuleId == RuleIds.LegacyDataTypedDataSet
                || fact.RuleId == RuleIds.LegacyDataModelRelationship)
            && (fact.Properties.GetValueOrDefault("modelRelationshipKind") == "relationship"
                || fact.Properties.GetValueOrDefault("relationshipFamily") == "typed-dataset");
    }

    private static bool IsNHibernateRelationshipEvidence(CodeFact fact)
    {
        return (fact.RuleId == RuleIds.LegacyDataOrmNHibernate
                || fact.RuleId == RuleIds.LegacyDataModelRelationship)
            && (fact.Properties.GetValueOrDefault("modelRelationshipKind") == "relationship"
                || fact.Properties.GetValueOrDefault("relationshipFamily") == "nhibernate-hbm");
    }

    private static bool IsEdmxRelationshipEvidence(CodeFact fact)
    {
        return (fact.RuleId == RuleIds.LegacyDataEdmx
                || fact.RuleId == RuleIds.LegacyDataModelRelationship)
            && (fact.Properties.GetValueOrDefault("modelRelationshipKind") == "relationship"
                || fact.Properties.GetValueOrDefault("relationshipFamily") == "edmx");
    }

    private static async Task<string> ReadAllPropertiesAsync(string sqlitePath)
    {
        await using var connection = new SqliteConnection($"Data Source={sqlitePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "select group_concat(properties_json, char(10)) from facts;";
        return (string?)await command.ExecuteScalarAsync() ?? string.Empty;
    }

    private static SortedDictionary<string, string> ApplyLegacyDataModelIdentity(string displayName, string? coverageLabel)
    {
        var properties = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var coreAssembly = typeof(ScanEngine).Assembly;
        var descriptorType = coreAssembly.GetType("TraceMap.Core.LegacyDataModelIdentityDescriptor", throwOnError: true)!;
        var identityType = coreAssembly.GetType("TraceMap.Core.LegacyDataModelIdentity", throwOnError: true)!;
        var descriptor = Activator.CreateInstance(
            descriptorType,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null,
            args:
            [
                "dbml",
                "entity",
                "conceptual",
                "Models/Store.dbml",
                "Customer",
                displayName,
                null,
                null,
                null,
                coverageLabel
            ],
            culture: null)!;
        var apply = identityType.GetMethod("Apply", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)!;
        apply.Invoke(null, [properties, descriptor]);
        return properties;
    }
}
