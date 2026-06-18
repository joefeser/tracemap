using Microsoft.Data.Sqlite;
using TraceMap.Core;
using TraceMap.Reporting;
using TraceMap.Storage;

namespace TraceMap.Tests;

public sealed class LegacyDataMetadataExtractorTests
{
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
    public void Scan_does_not_upgrade_descriptor_tier_above_tier2_for_generated_code_link()
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

        var entity = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.LegacyDataEntityDeclared
            && fact.RuleId == RuleIds.LegacyDataDbml
            && fact.Properties.GetValueOrDefault("displayName") == "Customer");
        var link = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.LegacyDataGeneratedCodeLinked
            && fact.TargetSymbol == "Customer");

        Assert.Equal(EvidenceTiers.Tier2Structural, entity.EvidenceTier);
        Assert.Equal(EvidenceTiers.Tier2Structural, entity.Properties.GetValueOrDefault("modelIdentityEvidenceTier"));
        Assert.Equal(EvidenceTiers.Tier2Structural, link.EvidenceTier);
    }

    private static async Task<string> ReadAllPropertiesAsync(string sqlitePath)
    {
        await using var connection = new SqliteConnection($"Data Source={sqlitePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "select group_concat(properties_json, char(10)) from facts;";
        return (string?)await command.ExecuteScalarAsync() ?? string.Empty;
    }
}
