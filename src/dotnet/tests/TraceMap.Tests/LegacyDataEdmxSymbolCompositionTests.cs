using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class LegacyDataEdmxSymbolCompositionTests
{
    [Fact]
    public void F01_composes_entity_to_table_chain_with_bridge_provenance()
    {
        using var fixture = new Ef6Fixture();
        var result = fixture.Scan();

        Assert.Equal("Level1SemanticAnalysis", result.Manifest.AnalysisLevel);
        var scope = result.Facts.Single(fact => fact.FactType == FactTypes.LegacyDataGeneratedFileScope);
        Assert.Equal(RuleIds.LegacyDataEdmxSymbolComposition, scope.RuleId);
        Assert.Equal(EvidenceTiers.Tier2Structural, scope.EvidenceTier);
        Assert.Equal("same-directory-designer-file", scope.Properties.GetValueOrDefault("scopeRule"));
        Assert.Equal(fixture.DesignerPath, scope.Properties.GetValueOrDefault("scopedFilePaths"));
        Assert.Equal(result.Facts.Single(fact => fact.FactType == FactTypes.LegacyDataMetadataDeclared
                && fact.Properties.GetValueOrDefault("metadataFormat") == "edmx").FactId,
            scope.Properties.GetValueOrDefault("sourceMetadataFactId"));

        var declaration = result.Facts.Single(fact =>
            fact.FactType == FactTypes.TypeDeclared
            && fact.TargetSymbol == "global::Model.Customer");
        var csdlEntity = result.Facts.Single(fact =>
            fact.FactType == FactTypes.LegacyDataEntityDeclared
            && fact.Properties.GetValueOrDefault("descriptorKind") == "csdl-entity"
            && fact.Properties.GetValueOrDefault("entityName") == "Customer");
        var conceptual = result.Facts.Single(fact =>
            fact.FactType == FactTypes.SymbolRelationship
            && fact.Properties.GetValueOrDefault("relationshipKind") == "MapsToConceptualEntity");
        Assert.Equal(RuleIds.LegacyDataEdmxSymbolComposition, conceptual.RuleId);
        Assert.Equal(EvidenceTiers.Tier2Structural, conceptual.EvidenceTier);
        Assert.Equal(declaration.Properties["targetSymbolId"], conceptual.Properties["sourceSymbolId"]);
        Assert.Equal(csdlEntity.Properties["stableModelKey"], conceptual.Properties["targetSymbolId"]);
        Assert.Equal("edmx", conceptual.Properties["targetSymbolLanguage"]);
        Assert.Equal("EdmxConceptualEntityType", conceptual.Properties["targetSymbolKind"]);
        Assert.Equal("generated-file-scope", conceptual.Properties["namespaceBridgeMechanism"]);
        Assert.Equal(scope.FactId, conceptual.Properties["namespaceBridgeFactId"]);
        Assert.Equal(
            $"{declaration.FactId};{scope.FactId};{csdlEntity.FactId}",
            conceptual.Properties["supportingFactIds"]);
        Assert.Equal("full", conceptual.Properties.GetValueOrDefault("coverageLabel"));
        Assert.Contains("edmx-static-design-time", conceptual.Properties.GetValueOrDefault("limitations"), StringComparison.Ordinal);
        Assert.Contains("namespace-bridge-structural", conceptual.Properties.GetValueOrDefault("limitations"), StringComparison.Ordinal);

        var ssdlSet = result.Facts.Single(fact =>
            fact.FactType == FactTypes.LegacyDataStorageObjectDeclared
            && fact.Properties.GetValueOrDefault("descriptorKind") == "ssdl-entity-set");
        var entityTable = result.Facts.Single(fact =>
            fact.FactType == FactTypes.LegacyDataMappingDeclared
            && fact.Properties.GetValueOrDefault("mappingKind") == "entity-table");
        var entitySet = result.Facts.Single(fact =>
            fact.FactType == FactTypes.LegacyDataEntityDeclared
            && fact.Properties.GetValueOrDefault("descriptorKind") == "csdl-entity-set");
        var storage = result.Facts.Single(fact =>
            fact.FactType == FactTypes.SymbolRelationship
            && fact.Properties.GetValueOrDefault("relationshipKind") == "MapsToStorageTable");
        Assert.Equal(ssdlSet.Properties["stableModelKey"], storage.Properties["targetSymbolId"]);
        Assert.Equal("dbo.CustomerTable", storage.TargetSymbol);
        Assert.Equal(entityTable.Evidence.StartLine, storage.Evidence.StartLine);
        Assert.Equal(
            $"{declaration.FactId};{scope.FactId};{csdlEntity.FactId};{entitySet.FactId};{entityTable.FactId};{ssdlSet.FactId}",
            storage.Properties["supportingFactIds"]);
        Assert.Contains("storage-join-structural", storage.Properties.GetValueOrDefault("limitations"), StringComparison.Ordinal);
        Assert.EndsWith("Ef6Sample.csproj", storage.ProjectPath ?? string.Empty, StringComparison.Ordinal);
    }

    [Fact]
    public void F02_composes_property_to_column_and_ignores_same_named_column_on_other_storage_type()
    {
        using var fixture = new Ef6Fixture();
        fixture.Ssdl = """
                <edmx:StorageModels>
                  <Schema xmlns="http://schemas.microsoft.com/ado/2009/11/edm/ssdl" Namespace="Store">
                    <EntityContainer Name="StoreContainer">
                      <EntitySet Name="Customers" EntityType="Store.CustomerTable" Table="dbo.CustomerTable" />
                      <EntitySet Name="AuditLogs" EntityType="Store.AuditLog" Table="dbo.AuditLog" />
                    </EntityContainer>
                    <EntityType Name="CustomerTable"><Property Name="CustomerId" Type="int" /><Property Name="Name" Type="nvarchar" /></EntityType>
                    <EntityType Name="AuditLog"><Property Name="CustomerId" Type="int" /></EntityType>
                  </Schema>
                </edmx:StorageModels>
            """;
        var result = fixture.Scan();

        var customerColumn = result.Facts.Single(fact =>
            fact.FactType == FactTypes.LegacyDataColumnDeclared
            && fact.Properties.GetValueOrDefault("sourceSection") == "SSDL"
            && fact.Properties.GetValueOrDefault("storageObjectName") == "CustomerTable"
            && fact.Properties.GetValueOrDefault("columnName") == "CustomerId");
        var auditColumn = result.Facts.Single(fact =>
            fact.FactType == FactTypes.LegacyDataColumnDeclared
            && fact.Properties.GetValueOrDefault("sourceSection") == "SSDL"
            && fact.Properties.GetValueOrDefault("storageObjectName") == "AuditLog"
            && fact.Properties.GetValueOrDefault("columnName") == "CustomerId");

        var propertyEdge = result.Facts.Single(fact =>
            fact.FactType == FactTypes.SymbolRelationship
            && fact.Properties.GetValueOrDefault("relationshipKind") == "MapsToConceptualProperty"
            && fact.Properties.GetValueOrDefault("targetSymbolDisplayName") == "CustomerId");
        Assert.StartsWith("csharp property ", propertyEdge.Properties["sourceSymbolId"], StringComparison.Ordinal);

        var columnEdge = result.Facts.Single(fact =>
            fact.FactType == FactTypes.SymbolRelationship
            && fact.Properties.GetValueOrDefault("relationshipKind") == "MapsToStorageColumn"
            && fact.Properties.GetValueOrDefault("targetSymbolDisplayName") == "CustomerId");
        Assert.Equal(customerColumn.Properties["stableModelKey"], columnEdge.Properties["targetSymbolId"]);
        Assert.NotEqual(auditColumn.Properties["stableModelKey"], columnEdge.Properties["targetSymbolId"]);
        Assert.Equal("EdmxStorageColumn", columnEdge.Properties["targetSymbolKind"]);
    }

    [Fact]
    public void F03_honors_entity_type_mapping_type_name_over_entity_set_mapping_name()
    {
        using var fixture = new Ef6Fixture();
        fixture.DesignerCode = """
            namespace Model;

            public class Customers
            {
                public int CustomerId { get; set; }
            }

            public class Customer
            {
                public int CustomerId { get; set; }
                public string Name { get; set; } = "";
            }
            """;
        var result = fixture.Scan();

        var conceptual = result.Facts.Single(fact =>
            fact.FactType == FactTypes.SymbolRelationship
            && fact.Properties.GetValueOrDefault("relationshipKind") == "MapsToConceptualEntity");
        Assert.EndsWith("Model.Customer", conceptual.Properties["sourceSymbol"], StringComparison.Ordinal);
        Assert.DoesNotContain("Model.Customers", conceptual.Properties["sourceSymbol"], StringComparison.Ordinal);
    }

    [Fact]
    public void F05_same_simple_names_across_namespaces_compose_without_cross_wiring()
    {
        using var fixture = new Ef6Fixture();
        fixture.Csdl = """
                <edmx:ConceptualModels>
                  <Schema xmlns="http://schemas.microsoft.com/ado/2009/11/edm" Namespace="ModelA">
                    <EntityContainer Name="ModelContainer">
                      <EntitySet Name="CustomersA" EntityType="ModelA.Customer" />
                      <EntitySet Name="CustomersB" EntityType="ModelB.Customer" />
                    </EntityContainer>
                    <EntityType Name="Customer"><Property Name="CustomerId" Type="Int32" /></EntityType>
                  </Schema>
                  <Schema xmlns="http://schemas.microsoft.com/ado/2009/11/edm" Namespace="ModelB">
                    <EntityType Name="Customer"><Property Name="CustomerId" Type="Int32" /></EntityType>
                  </Schema>
                </edmx:ConceptualModels>
            """;
        fixture.Msl = """
                <edmx:Mappings>
                  <Mapping xmlns="http://schemas.microsoft.com/ado/2009/11/mapping/cs">
                    <EntityContainerMapping StorageEntityContainer="StoreContainer" CdmEntityContainer="ModelContainer">
                      <EntitySetMapping Name="CustomersA">
                        <EntityTypeMapping TypeName="ModelA.Customer">
                          <MappingFragment StoreEntitySet="Customers">
                            <ScalarProperty Name="CustomerId" ColumnName="CustomerId" />
                          </MappingFragment>
                        </EntityTypeMapping>
                      </EntitySetMapping>
                      <EntitySetMapping Name="CustomersB">
                        <EntityTypeMapping TypeName="ModelB.Customer">
                          <MappingFragment StoreEntitySet="Customers">
                            <ScalarProperty Name="CustomerId" ColumnName="CustomerId" />
                          </MappingFragment>
                        </EntityTypeMapping>
                      </EntitySetMapping>
                    </EntityContainerMapping>
                  </Mapping>
                </edmx:Mappings>
            """;
        fixture.DesignerCode = """
            namespace ModelA
            {
                public class Customer
                {
                    public int CustomerId { get; set; }
                }
            }

            namespace ModelB
            {
                public class Customer
                {
                    public int CustomerId { get; set; }
                }
            }
            """;
        var result = fixture.Scan();

        var conceptualEdges = result.Facts
            .Where(fact => fact.FactType == FactTypes.SymbolRelationship
                && fact.Properties.GetValueOrDefault("relationshipKind") == "MapsToConceptualEntity")
            .OrderBy(fact => fact.Properties["sourceSymbol"], StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(2, conceptualEdges.Length);
        Assert.Contains("ModelA.Customer", conceptualEdges[0].Properties["sourceSymbol"], StringComparison.Ordinal);
        Assert.Contains("ModelB.Customer", conceptualEdges[1].Properties["sourceSymbol"], StringComparison.Ordinal);
        var targetKeys = conceptualEdges.Select(edge => edge.Properties["targetSymbolId"]).ToHashSet(StringComparer.Ordinal);
        Assert.Equal(2, targetKeys.Count);
        Assert.DoesNotContain(result.Facts, fact => fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyDataEdmxSymbolComposition
            && fact.Properties.GetValueOrDefault("classification") == "AmbiguousClrSymbolReconciliation");
    }

    [Fact]
    public void F06_same_canonical_id_across_compilation_scopes_fails_closed()
    {
        using var fixture = new Ef6Fixture(withProject: false);
        fixture.WriteSharedProject("ProjectA");
        fixture.WriteSharedProject("ProjectB");
        var result = fixture.Scan();

        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyDataEdmxSymbolComposition
            && fact.Properties.GetValueOrDefault("classification") == "AmbiguousClrSymbolReconciliation"
            && fact.Properties.GetValueOrDefault("message")!.Contains("Customer", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Facts, fact =>
            fact.FactType == FactTypes.SymbolRelationship
            && fact.Properties.GetValueOrDefault("relationshipKind") == "MapsToConceptualEntity");
    }

    [Fact]
    public void F07_ambiguous_joins_produce_explicit_gaps()
    {
        using var fixture = new Ef6Fixture();
        fixture.Csdl = """
                <edmx:ConceptualModels>
                  <Schema xmlns="http://schemas.microsoft.com/ado/2009/11/edm" Namespace="Model">
                    <EntityContainer Name="ModelContainer">
                      <EntitySet Name="Customers" EntityType="Model.Missing" />
                      <EntitySet Name="Orders" EntityType="Model.Order" />
                      <EntitySet Name="Widgets" EntityType="Model.Widget" />
                      <EntitySet Name="Gadgets" EntityType="Model.Gadget" />
                    </EntityContainer>
                    <EntityType Name="Customer"><Property Name="CustomerId" Type="Int32" /></EntityType>
                    <EntityType Name="Order"><Property Name="OrderId" Type="Int32" /></EntityType>
                    <EntityType Name="Widget"><Property Name="WidgetId" Type="Int32" /></EntityType>
                    <EntityType Name="Gadget"><Property Name="GadgetId" Type="Int32" /></EntityType>
                  </Schema>
                </edmx:ConceptualModels>
            """;
        fixture.Msl = """
                <edmx:Mappings>
                  <Mapping xmlns="http://schemas.microsoft.com/ado/2009/11/mapping/cs">
                    <EntityContainerMapping StorageEntityContainer="StoreContainer" CdmEntityContainer="ModelContainer">
                      <EntitySetMapping Name="Orders">
                        <EntityTypeMapping TypeName="Model.Wrong">
                          <MappingFragment StoreEntitySet="Customers">
                            <ScalarProperty Name="OrderId" ColumnName="CustomerId" />
                          </MappingFragment>
                        </EntityTypeMapping>
                      </EntitySetMapping>
                      <EntitySetMapping Name="Widgets">
                        <EntityTypeMapping TypeName="Model.Widget">
                          <MappingFragment StoreEntitySet="Nope">
                            <ScalarProperty Name="WidgetId" ColumnName="CustomerId" />
                          </MappingFragment>
                        </EntityTypeMapping>
                      </EntitySetMapping>
                      <EntitySetMapping Name="Gadgets">
                        <EntityTypeMapping TypeName="Model.Gadget">
                          <MappingFragment StoreEntitySet="Customers">
                            <ScalarProperty Name="GadgetId" ColumnName="MissingColumn" />
                          </MappingFragment>
                        </EntityTypeMapping>
                      </EntitySetMapping>
                    </EntityContainerMapping>
                  </Mapping>
                </edmx:Mappings>
            """;
        fixture.DesignerCode = """
            namespace Model;

            public class Customer
            {
                public int CustomerId { get; set; }
            }

            public class Order
            {
                public int OrderId { get; set; }
            }

            public class Widget
            {
                public int WidgetId { get; set; }
            }

            public class Gadget
            {
                public int GadgetId { get; set; }
            }
            """;
        var result = fixture.Scan();

        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyDataEdmxSymbolComposition
            && fact.Properties.GetValueOrDefault("classification") == "AmbiguousLegacyDataModelIdentity"
            && fact.Properties.GetValueOrDefault("message")!.Contains("entity set", StringComparison.Ordinal)
            && fact.Properties.GetValueOrDefault("message")!.Contains("Customer", StringComparison.Ordinal));
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyDataEdmxSymbolComposition
            && fact.Properties.GetValueOrDefault("classification") == "AmbiguousLegacyDataModelIdentity"
            && fact.Properties.GetValueOrDefault("message")!.Contains("did not resolve to conceptual entity", StringComparison.Ordinal));
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyDataEdmxSymbolComposition
            && fact.Properties.GetValueOrDefault("classification") == "AmbiguousLegacyDataModelIdentity"
            && fact.Properties.GetValueOrDefault("message")!.Contains("StoreEntitySet", StringComparison.Ordinal));
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyDataEdmxSymbolComposition
            && fact.Properties.GetValueOrDefault("classification") == "AmbiguousLegacyDataModelIdentity"
            && fact.Properties.GetValueOrDefault("message")!.Contains("SSDL column", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Facts, fact =>
            fact.FactType == FactTypes.SymbolRelationship
            && fact.RuleId == RuleIds.LegacyDataEdmxSymbolComposition
            && fact.Properties.GetValueOrDefault("relationshipKind") == "MapsToStorageColumn");
        var gadgetStorage = result.Facts.Count(fact =>
            fact.FactType == FactTypes.SymbolRelationship
            && fact.Properties.GetValueOrDefault("relationshipKind") == "MapsToStorageTable");
        Assert.Equal(1, gadgetStorage);
    }

    [Fact]
    public void F08_unsupported_shapes_fail_closed_with_composition_owned_gaps()
    {
        using var fixture = new Ef6Fixture();
        fixture.Csdl = """
                <edmx:ConceptualModels>
                  <Schema xmlns="http://schemas.microsoft.com/ado/2009/11/edm" Namespace="Model">
                    <EntityContainer Name="ModelContainer">
                      <EntitySet Name="Customers" EntityType="Model.Customer" />
                      <EntitySet Name="Specials" EntityType="Model.Special" />
                      <AssociationSet Name="FK_Order_Customer" Association="Model.FK_Order_Customer">
                        <End Role="Customer" EntitySet="Customers" />
                        <End Role="Orders" EntitySet="Specials" />
                      </AssociationSet>
                    </EntityContainer>
                    <EntityType Name="Customer"><Property Name="CustomerId" Type="Int32" /></EntityType>
                    <EntityType Name="Special" BaseType="Model.Customer"><Property Name="Extra" Type="Int32" /></EntityType>
                    <Association Name="FK_Order_Customer">
                      <End Role="Customer" Type="Model.Customer" Multiplicity="1" />
                      <End Role="Orders" Type="Model.Order" Multiplicity="*" />
                    </Association>
                    <EntityType Name="Order"><Property Name="OrderId" Type="Int32" /></EntityType>
                  </Schema>
                </edmx:ConceptualModels>
            """;
        fixture.Ssdl = """
                <edmx:StorageModels>
                  <Schema xmlns="http://schemas.microsoft.com/ado/2009/11/edm/ssdl" Namespace="Store">
                    <EntityContainer Name="StoreContainer"><EntitySet Name="Customers" EntityType="Store.CustomerTable" Table="dbo.CustomerTable" /></EntityContainer>
                    <EntityType Name="CustomerTable"><Property Name="CustomerId" Type="int" /></EntityType>
                    <Function Name="DeleteCustomer" ReturnType="int" />
                  </Schema>
                </edmx:StorageModels>
            """;
        fixture.Msl = """
                <edmx:Mappings>
                  <Mapping xmlns="http://schemas.microsoft.com/ado/2009/11/mapping/cs">
                    <EntityContainerMapping StorageEntityContainer="StoreContainer" CdmEntityContainer="ModelContainer">
                      <EntitySetMapping Name="Customers">
                        <EntityTypeMapping TypeName="IsTypeOf(Model.Customer)">
                          <MappingFragment StoreEntitySet="Customers">
                            <Condition ColumnName="Kind" IsNull="false" />
                            <ScalarProperty Name="CustomerId" ColumnName="CustomerId" />
                          </MappingFragment>
                        </EntityTypeMapping>
                      </EntitySetMapping>
                      <AssociationSetMapping Name="FK_Order_Customer" Association="Model.FK_Order_Customer">
                        <EndProperty Name="Customer">
                          <ScalarProperty Name="CustomerId" ColumnName="CustomerId" />
                        </EndProperty>
                        <EndProperty Name="Orders">
                          <ScalarProperty Name="OrderId" ColumnName="CustomerId" />
                        </EndProperty>
                      </AssociationSetMapping>
                    </EntityContainerMapping>
                  </Mapping>
                </edmx:Mappings>
            """;
        var result = fixture.Scan();

        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyDataEdmxSymbolComposition
            && fact.Properties.GetValueOrDefault("classification") == "UnsupportedLegacyOrmMappingShape"
            && fact.Properties.GetValueOrDefault("message")!.Contains("IsTypeOf", StringComparison.Ordinal));
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyDataEdmxSymbolComposition
            && fact.Properties.GetValueOrDefault("classification") == "UnsupportedLegacyOrmMappingShape"
            && fact.Properties.GetValueOrDefault("message")!.Contains("Association mapping", StringComparison.Ordinal));
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyDataEdmxSymbolComposition
            && fact.Properties.GetValueOrDefault("classification") == "UnsupportedLegacyOrmMappingShape"
            && fact.Properties.GetValueOrDefault("message")!.Contains("Provider-defined routine", StringComparison.Ordinal));
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyDataEdmx
            && fact.Properties.GetValueOrDefault("classification") == "UnsupportedLegacyOrmMappingShape"
            && fact.Properties.GetValueOrDefault("message")!.Contains("Condition", StringComparison.Ordinal));
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyDataEdmx
            && fact.Properties.GetValueOrDefault("classification") == "UnsupportedLegacyOrmMappingShape"
            && fact.Properties.GetValueOrDefault("message")!.Contains("inherited", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Facts, fact =>
            fact.FactType == FactTypes.SymbolRelationship
            && fact.RuleId == RuleIds.LegacyDataEdmxSymbolComposition
            && fact.Properties.GetValueOrDefault("relationshipKind") is "MapsToStorageTable" or "MapsToStorageColumn");
    }

    [Fact]
    public void F09_missing_generated_code_stays_partial_without_edges()
    {
        using var fixture = new Ef6Fixture();
        fixture.DesignerCode = null;
        var result = fixture.Scan();

        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyDataEdmxSymbolComposition
            && fact.Properties.GetValueOrDefault("classification") == "MissingGeneratedCode"
            && fact.Properties.GetValueOrDefault("message")!.Contains("Customer", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Facts, fact =>
            fact.FactType == FactTypes.SymbolRelationship
            && fact.RuleId == RuleIds.LegacyDataEdmxSymbolComposition);
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.LegacyDataEntityDeclared
            && fact.Properties.GetValueOrDefault("descriptorKind") == "csdl-entity"
            && fact.Properties.GetValueOrDefault("entityName") == "Customer");
    }

    [Fact]
    public void F10_no_compiler_evidence_fails_closed()
    {
        using var fixture = new Ef6Fixture(withProject: false);
        var result = fixture.Scan();

        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyDataEdmxSymbolComposition
            && fact.Properties.GetValueOrDefault("classification") == "ClrSymbolEvidenceUnavailable");
        Assert.DoesNotContain(result.Facts, fact =>
            fact.FactType == FactTypes.SymbolRelationship
            && fact.RuleId == RuleIds.LegacyDataEdmxSymbolComposition);
    }

    [Fact]
    public void F13_repeated_scans_are_deterministic()
    {
        using var fixture = new Ef6Fixture();
        var first = fixture.Scan(outputSuffix: "one");
        var second = fixture.Scan(outputSuffix: "two");

        string Fingerprint(ScanResult result) => string.Join("\n", result.Facts
            .Where(fact => fact.RuleId == RuleIds.LegacyDataEdmxSymbolComposition)
            .OrderBy(fact => fact.FactId, StringComparer.Ordinal)
            .Select(fact => $"{fact.FactId}|{fact.FactType}|{fact.EvidenceTier}|{fact.Evidence.StartLine}|{string.Join(";", fact.Properties.Select(pair => $"{pair.Key}={pair.Value}"))}"));
        Assert.Equal(Fingerprint(first), Fingerprint(second));
    }

    [Fact]
    public void F14_attribute_bridge_composes_divergent_namespace_via_mechanism_one()
    {
        using var fixture = new Ef6Fixture();
        fixture.EdmxFileName = "DataModel.edmx";
        fixture.DesignerCode = null;
        fixture.ExtraFiles["AttributeBearer.cs"] = """
            using System.Data.Entity.Core.Objects.DataClasses;

            namespace App.Data;

            [EdmEntityType(NamespaceName = "Model", Name = "Customer")]
            public class Customer
            {
                public int CustomerId { get; set; }
                public string Name { get; set; } = "";
            }
            """;
        var result = fixture.Scan();

        var declaration = result.Facts.Single(fact =>
            fact.FactType == FactTypes.TypeDeclared
            && fact.TargetSymbol == "global::App.Data.Customer");
        var conceptual = result.Facts.Single(fact =>
            fact.FactType == FactTypes.SymbolRelationship
            && fact.Properties.GetValueOrDefault("relationshipKind") == "MapsToConceptualEntity");
        Assert.Equal("semantic-attribute", conceptual.Properties["namespaceBridgeMechanism"]);
        Assert.Equal(declaration.FactId, conceptual.Properties["namespaceBridgeFactId"]);
        Assert.Equal(
            $"{declaration.FactId};{result.Facts.Single(fact => fact.FactType == FactTypes.LegacyDataEntityDeclared && fact.Properties.GetValueOrDefault("descriptorKind") == "csdl-entity").FactId}",
            conceptual.Properties["supportingFactIds"]);
        Assert.StartsWith("csharp type ", conceptual.Properties["sourceSymbolId"], StringComparison.Ordinal);
        var storage = result.Facts.Single(fact =>
            fact.FactType == FactTypes.SymbolRelationship
            && fact.Properties.GetValueOrDefault("relationshipKind") == "MapsToStorageTable");
        Assert.Equal("semantic-attribute", storage.Properties["namespaceBridgeMechanism"]);
    }

    [Fact]
    public void F15_divergent_namespace_without_bridge_gaps_closed()
    {
        using var fixture = new Ef6Fixture();
        fixture.DesignerCode = """
            namespace App.Data;

            public class Customer
            {
                public int CustomerId { get; set; }
            }
            """;
        var result = fixture.Scan();

        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyDataEdmxSymbolComposition
            && fact.Properties.GetValueOrDefault("classification") == "UnresolvedGeneratedNamespace"
            && fact.Properties.GetValueOrDefault("message")!.Contains("Customer", StringComparison.Ordinal));
        Assert.DoesNotContain(result.Facts, fact =>
            fact.FactType == FactTypes.SymbolRelationship
            && fact.RuleId == RuleIds.LegacyDataEdmxSymbolComposition);
    }

    [Fact]
    public void F16_scoped_candidate_collisions_fail_closed()
    {
        using var fixture = new Ef6Fixture(withProject: false);
        fixture.WriteSharedProject("ProjectA");
        fixture.WriteSharedProject("ProjectB");
        var result = fixture.Scan();

        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyDataEdmxSymbolComposition
            && fact.Properties.GetValueOrDefault("classification") == "AmbiguousClrSymbolReconciliation");
        Assert.DoesNotContain(result.Facts, fact =>
            fact.FactType == FactTypes.SymbolRelationship
            && fact.RuleId == RuleIds.LegacyDataEdmxSymbolComposition
            && fact.Properties.GetValueOrDefault("relationshipKind") == "MapsToConceptualEntity");

        using var duplicate = new Ef6Fixture();
        duplicate.WriteProject("ProjectA", "DuplicateA");
        duplicate.WriteProject("ProjectB", "DuplicateB");
        duplicate.ExtraFiles[Path.Combine("ProjectA", "AttributeA.cs")] = """
            using System.Data.Entity.Core.Objects.DataClasses;

            namespace Model;

            [EdmEntityType(NamespaceName = "Model", Name = "Customer")]
            public class Customer
            {
                public int CustomerId { get; set; }
            }
            """;
        duplicate.ExtraFiles[Path.Combine("ProjectB", "AttributeB.cs")] = """
            using System.Data.Entity.Core.Objects.DataClasses;

            namespace Model;

            [EdmEntityType(NamespaceName = "Model", Name = "Customer")]
            public class Customer
            {
                public int CustomerId { get; set; }
            }
            """;
        duplicate.DesignerCode = null;
        var duplicateResult = duplicate.Scan();
        Assert.Contains(duplicateResult.Facts, fact => fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyDataEdmxSymbolComposition
            && fact.Properties.GetValueOrDefault("classification") == "AmbiguousClrSymbolReconciliation");
        Assert.DoesNotContain(duplicateResult.Facts, fact =>
            fact.FactType == FactTypes.SymbolRelationship
            && fact.Properties.GetValueOrDefault("relationshipKind") == "MapsToConceptualEntity");
    }

    [Fact]
    public void F17_scope_decoys_never_become_candidates()
    {
        using var fixture = new Ef6Fixture();
        fixture.DesignerCode = """
            namespace App.Data;

            public class Customer
            {
                public int CustomerId { get; set; }
            }
            """;
        fixture.ExtraFiles[Path.Combine("Other", "Model.Designer.cs")] = """
            namespace Model;

            public class Customer
            {
                public int CustomerId { get; set; }
            }
            """;
        fixture.ExtraFiles["ModelArchive.Designer.cs"] = """
            namespace Model;

            public class Customer
            {
                public int CustomerId { get; set; }
            }
            """;
        fixture.ExtraFiles["Model2.Designer.cs"] = """
            namespace Model;

            public class Customer
            {
                public int CustomerId { get; set; }
            }
            """;
        var result = fixture.Scan();

        var scope = result.Facts.Single(fact => fact.FactType == FactTypes.LegacyDataGeneratedFileScope);
        Assert.Equal(fixture.DesignerPath, scope.Properties.GetValueOrDefault("scopedFilePaths"));
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyDataEdmxSymbolComposition
            && fact.Properties.GetValueOrDefault("classification") == "UnresolvedGeneratedNamespace");
        Assert.DoesNotContain(result.Facts, fact =>
            fact.FactType == FactTypes.SymbolRelationship
            && fact.RuleId == RuleIds.LegacyDataEdmxSymbolComposition
            && fact.Properties.GetValueOrDefault("relationshipKind") == "MapsToConceptualEntity");
    }

    [Fact]
    public void F18_per_edmx_compiler_availability_classifies_failed_analysis_correctly()
    {
        using var fixture = new Ef6Fixture(withProject: false);
        fixture.WriteSharedProject("Healthy");
        var brokenDirectory = Path.Combine(fixture.TempPath, "Broken");
        Directory.CreateDirectory(brokenDirectory);
        File.WriteAllText(Path.Combine(brokenDirectory, "Broken.csproj"), "<Project><Compile");
        File.WriteAllText(Path.Combine(brokenDirectory, "Model.edmx"), Ef6Fixture.DefaultEdmx);
        File.WriteAllText(Path.Combine(brokenDirectory, "Model.Designer.cs"), Ef6Fixture.DefaultDesignerCode);
        var result = fixture.Scan();

        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.SymbolRelationship
            && fact.Properties.GetValueOrDefault("relationshipKind") == "MapsToConceptualEntity"
            && fact.Evidence.FilePath == Path.Combine("src", "Model.edmx"));
        var unavailable = result.Facts.Where(fact => fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyDataEdmxSymbolComposition
            && fact.Properties.GetValueOrDefault("classification") == "ClrSymbolEvidenceUnavailable"
            && fact.Evidence.FilePath == Path.Combine("Broken", "Model.edmx"));
        Assert.NotEmpty(unavailable);
        Assert.DoesNotContain(result.Facts, fact => fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyDataEdmxSymbolComposition
            && fact.Properties.GetValueOrDefault("classification") is "MissingGeneratedCode" or "UnresolvedGeneratedNamespace"
            && fact.Evidence.FilePath == Path.Combine("Broken", "Model.edmx"));
    }

    private sealed class Ef6Fixture : IDisposable
    {
        public const string DefaultEdmxFileName = "Model.edmx";
        public const string DefaultDesignerFileName = "Model.Designer.cs";

        private readonly TempDirectory temp = new();

        public Ef6Fixture(bool withProject = true)
        {
            Root = "src";
            EdmxFileName = DefaultEdmxFileName;
            ExtraFiles = new Dictionary<string, string>(StringComparer.Ordinal);
            if (withProject)
            {
                WriteRootProject();
            }
        }

        public string Root { get; }

        public string TempPath => temp.Path;

        public string EdmxFileName { get; set; }

        public Dictionary<string, string> ExtraFiles { get; }

        public string Csdl { get; set; } = """
                <edmx:ConceptualModels>
                  <Schema xmlns="http://schemas.microsoft.com/ado/2009/11/edm" Namespace="Model">
                    <EntityContainer Name="ModelContainer"><EntitySet Name="Customers" EntityType="Model.Customer" /></EntityContainer>
                    <EntityType Name="Customer">
                      <Property Name="CustomerId" Type="Int32" />
                      <Property Name="Name" Type="String" />
                    </EntityType>
                  </Schema>
                </edmx:ConceptualModels>
            """;

        public string Ssdl { get; set; } = """
                <edmx:StorageModels>
                  <Schema xmlns="http://schemas.microsoft.com/ado/2009/11/edm/ssdl" Namespace="Store">
                    <EntityContainer Name="StoreContainer"><EntitySet Name="Customers" EntityType="Store.CustomerTable" Table="dbo.CustomerTable" /></EntityContainer>
                    <EntityType Name="CustomerTable">
                      <Property Name="CustomerId" Type="int" />
                      <Property Name="Name" Type="nvarchar" />
                    </EntityType>
                  </Schema>
                </edmx:StorageModels>
            """;

        public string Msl { get; set; } = """
                <edmx:Mappings>
                  <Mapping xmlns="http://schemas.microsoft.com/ado/2009/11/mapping/cs">
                    <EntityContainerMapping StorageEntityContainer="StoreContainer" CdmEntityContainer="ModelContainer">
                      <EntitySetMapping Name="Customers">
                        <EntityTypeMapping TypeName="Model.Customer">
                          <MappingFragment StoreEntitySet="Customers">
                            <ScalarProperty Name="CustomerId" ColumnName="CustomerId" />
                            <ScalarProperty Name="Name" ColumnName="Name" />
                          </MappingFragment>
                        </EntityTypeMapping>
                      </EntitySetMapping>
                    </EntityContainerMapping>
                  </Mapping>
                </edmx:Mappings>
            """;

        public string? DesignerCode { get; set; } = DefaultDesignerCode;

        public const string Stubs = """
                namespace System.Data.Entity
                {
                    public abstract class DbContext { }
                    public sealed class DbSet<TEntity> { }
                }

                namespace System.Data.Entity.Core.Objects.DataClasses
                {
                    [System.AttributeUsage(System.AttributeTargets.Class)]
                    public sealed class EdmEntityTypeAttribute : System.Attribute
                    {
                        public string NamespaceName { get; set; } = "";
                        public string Name { get; set; } = "";
                    }
                }
                """;

        public static string DefaultDesignerCode => """
            namespace Model;

            public class Customer
            {
                public int CustomerId { get; set; }
                public string Name { get; set; } = "";
            }
            """;

        public static string DefaultEdmx => BuildEdmx(null, null, null);

        public string DesignerPath => Path.Combine(Root, DefaultDesignerFileName);

        public static string BuildEdmx(string? csdl, string? ssdl, string? msl) => $"""
            <edmx:Edmx xmlns:edmx="http://schemas.microsoft.com/ado/2009/11/edmx" Version="3.0">
              <edmx:Runtime>
            {csdl ?? """
                <edmx:ConceptualModels>
                  <Schema xmlns="http://schemas.microsoft.com/ado/2009/11/edm" Namespace="Model">
                    <EntityContainer Name="ModelContainer"><EntitySet Name="Customers" EntityType="Model.Customer" /></EntityContainer>
                    <EntityType Name="Customer"><Property Name="CustomerId" Type="Int32" /></EntityType>
                  </Schema>
                </edmx:ConceptualModels>
            """}
            {ssdl ?? """
                <edmx:StorageModels>
                  <Schema xmlns="http://schemas.microsoft.com/ado/2009/11/edm/ssdl" Namespace="Store">
                    <EntityContainer Name="StoreContainer"><EntitySet Name="Customers" EntityType="Store.CustomerTable" Table="dbo.CustomerTable" /></EntityContainer>
                    <EntityType Name="CustomerTable"><Property Name="CustomerId" Type="int" /></EntityType>
                  </Schema>
                </edmx:StorageModels>
            """}
            {msl ?? """
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
            """}
              </edmx:Runtime>
            </edmx:Edmx>
            """;

        public void WriteRootProject()
        {
            Directory.CreateDirectory(Path.Combine(temp.Path, Root));
            File.WriteAllText(Path.Combine(temp.Path, Root, "Ef6Sample.csproj"), """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                    <TargetFramework>net10.0</TargetFramework>
                  </PropertyGroup>
                </Project>
                """);
            File.WriteAllText(Path.Combine(temp.Path, Root, "Ef6Stubs.cs"), Stubs);
        }

        public void WriteProject(string projectName, string assemblyName)
        {
            var projectDirectory = Path.Combine(temp.Path, Root, projectName);
            Directory.CreateDirectory(projectDirectory);
            File.WriteAllText(Path.Combine(projectDirectory, $"{projectName}.csproj"), $"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                    <TargetFramework>net10.0</TargetFramework>
                    <AssemblyName>{assemblyName}</AssemblyName>
                  </PropertyGroup>
                </Project>
                """);
            File.WriteAllText(Path.Combine(projectDirectory, "Ef6Stubs.cs"), Stubs);
        }

        public void WriteSharedProject(string projectName)
        {
            var projectDirectory = Path.Combine(temp.Path, Root, projectName);
            Directory.CreateDirectory(projectDirectory);
            File.WriteAllText(Path.Combine(projectDirectory, $"{projectName}.csproj"), $$"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                    <TargetFramework>net10.0</TargetFramework>
                    <AssemblyName>SharedEf6</AssemblyName>
                  </PropertyGroup>
                  <ItemGroup>
                    <Compile Include="../Model.Designer.cs" Link="Model.Designer.cs" />
                  </ItemGroup>
                </Project>
                """);
            File.WriteAllText(Path.Combine(projectDirectory, "Ef6Stubs.cs"), Stubs);
        }

        public ScanResult Scan(string? outputSuffix = null)
        {
            var dataDirectory = Path.Combine(temp.Path, Root);
            Directory.CreateDirectory(dataDirectory);
            File.WriteAllText(Path.Combine(dataDirectory, EdmxFileName), BuildEdmx(Csdl, Ssdl, Msl));
            if (DesignerCode is not null && EdmxFileName == DefaultEdmxFileName)
            {
                File.WriteAllText(Path.Combine(dataDirectory, DefaultDesignerFileName), DesignerCode);
            }

            foreach (var (relativePath, content) in ExtraFiles.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                var fullPath = Path.Combine(temp.Path, Root, relativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                File.WriteAllText(fullPath, content);
            }

            var output = Path.Combine(temp.Path, "out" + (outputSuffix ?? string.Empty));
            return ScanEngine.Scan(new ScanOptions(temp.Path, output));
        }

        public void Dispose() => temp.Dispose();
    }
}
