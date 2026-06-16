using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TraceMap.Core;

public static class LegacyDataMetadataExtractor
{
    private const string ExtractorId = "LegacyDataExtractor";
    private static readonly XNamespace MsData = "urn:schemas-microsoft-com:xml-msdata";
    private static readonly XNamespace MsProp = "urn:schemas-microsoft-com:xml-msprop";

    public static IReadOnlyList<CodeFact> Extract(
        string repoPath,
        ScanManifest manifest,
        IReadOnlyList<FileInventoryItem> inventory,
        IReadOnlyList<CodeFact> existingFacts)
    {
        var facts = new List<CodeFact>();
        var generatedCandidates = LoadGeneratedCandidates(repoPath, inventory);

        foreach (var item in inventory.OrderBy(item => item.RelativePath, StringComparer.Ordinal))
        {
            switch (item.Kind)
            {
                case "Dbml":
                    ExtractDbml(repoPath, manifest, item, facts);
                    break;
                case "Edmx":
                    ExtractEdmx(repoPath, manifest, item, facts);
                    break;
                case "Xsd":
                    ExtractTypedDataSet(repoPath, manifest, item, facts);
                    break;
                case "Config":
                    ExtractDataConfig(repoPath, manifest, item, facts);
                    break;
                case "CSharp" when IsDesigner(item.RelativePath):
                    AddGeneratedDesignerInventory(manifest, item, facts);
                    break;
            }
        }

        AddGeneratedCodeLinks(manifest, facts, generatedCandidates, existingFacts);

        return facts
            .OrderBy(fact => fact.Evidence.FilePath, StringComparer.Ordinal)
            .ThenBy(fact => fact.Evidence.StartLine)
            .ThenBy(fact => fact.RuleId, StringComparer.Ordinal)
            .ThenBy(fact => fact.FactType, StringComparer.Ordinal)
            .ThenBy(fact => fact.TargetSymbol, StringComparer.Ordinal)
            .ThenBy(fact => fact.FactId, StringComparer.Ordinal)
            .ToArray();
    }

    private static void ExtractDbml(string repoPath, ScanManifest manifest, FileInventoryItem item, List<CodeFact> facts)
    {
        var fullPath = Path.Combine(repoPath, item.RelativePath);
        if (!TryLoadMetadataDocument(manifest, item, fullPath, "Dbml", facts, out var document, out var metadataHash))
        {
            return;
        }

        AddMetadataInventoryFact(manifest, facts, item.RelativePath, "Dbml", metadataHash, RuleIds.LegacyDataMetadataInventory, document.Root);

        var databases = document.Descendants().Where(element => element.Name.LocalName == "Database").ToArray();
        if (databases.Length > 1)
        {
            AddGap(manifest, facts, item.RelativePath, RuleIds.LegacyDataDbml, "UnsupportedLegacyDataMetadataVersion", "Multiple DBML Database descriptors were present and require a future deterministic mapping rule.", databases[1]);
        }

        var database = databases.FirstOrDefault() ?? document.Root;
        if (database is null)
        {
            return;
        }

        var provider = AttributeValue(database, "Provider");
        foreach (var table in database.Descendants().Where(element => element.Name.LocalName == "Table").OrderBy(GetLine).ThenBy(element => AttributeValue(element, "Name"), StringComparer.Ordinal))
        {
            var tableName = AttributeValue(table, "Name") ?? AttributeValue(table, "Member") ?? "table";
            var typeElement = table.Elements().FirstOrDefault(element => element.Name.LocalName == "Type");
            var typeName = AttributeValue(typeElement, "Name") ?? AttributeValue(table, "Type") ?? AttributeValue(table, "Member");
            var storageProps = MetadataProperties("Dbml", metadataHash, "table");
            AddSafeName(storageProps, "storageObjectName", "storageObjectHash", tableName);
            storageProps["storageObjectKind"] = "Table";
            AddHashOnly(storageProps, "providerNameHash", provider);
            facts.Add(CreateLegacyFact(manifest, FactTypes.LegacyDataStorageObjectDeclared, RuleIds.LegacyDataDbml, item.RelativePath, table, TargetFrom(storageProps, "storageObjectName", "storageObjectHash"), storageProps));

            if (!string.IsNullOrWhiteSpace(typeName))
            {
                var entityProps = MetadataProperties("Dbml", metadataHash, "entity");
                AddSafeName(entityProps, "entityName", "entityHash", typeName);
                AddSafeName(entityProps, "typeName", "typeHash", typeName);
                AddGeneratedHints(entityProps, database, item.RelativePath);
                facts.Add(CreateLegacyFact(manifest, FactTypes.LegacyDataEntityDeclared, RuleIds.LegacyDataDbml, item.RelativePath, typeElement ?? table, TargetFrom(entityProps, "typeName", "typeHash"), entityProps));

                var mappingProps = MetadataProperties("Dbml", metadataHash, "entity-table");
                mappingProps["mappingKind"] = "entity-table";
                AddSafeName(mappingProps, "entityName", "entityHash", typeName);
                AddSafeName(mappingProps, "tableName", "tableHash", tableName);
                facts.Add(CreateLegacyFact(manifest, FactTypes.LegacyDataMappingDeclared, RuleIds.LegacyDataDbml, item.RelativePath, table, TargetFrom(mappingProps, "entityName", "entityHash"), mappingProps));
            }

            foreach (var column in table.Descendants().Where(element => element.Name.LocalName == "Column").OrderBy(GetLine).ThenBy(element => AttributeValue(element, "Member") ?? AttributeValue(element, "Name"), StringComparer.Ordinal))
            {
                var memberName = AttributeValue(column, "Member") ?? AttributeValue(column, "Name") ?? "column";
                var columnName = AttributeValue(column, "Name") ?? memberName;
                var columnProps = MetadataProperties("Dbml", metadataHash, "column");
                AddSafeName(columnProps, "propertyName", "propertyHash", memberName);
                AddSafeName(columnProps, "columnName", "columnHash", columnName);
                AddSafeName(columnProps, "tableName", "tableHash", tableName);
                AddOptional(columnProps, "isPrimaryKey", AttributeValue(column, "IsPrimaryKey"));
                AddOptional(columnProps, "isNullable", AttributeValue(column, "CanBeNull"));
                AddOptional(columnProps, "isGenerated", AttributeValue(column, "IsDbGenerated"));
                facts.Add(CreateLegacyFact(manifest, FactTypes.LegacyDataColumnDeclared, RuleIds.LegacyDataDbml, item.RelativePath, column, TargetFrom(columnProps, "propertyName", "propertyHash"), columnProps));

                var mappingProps = MetadataProperties("Dbml", metadataHash, "property-column");
                mappingProps["mappingKind"] = "property-column";
                AddSafeName(mappingProps, "entityName", "entityHash", typeName);
                AddSafeName(mappingProps, "propertyName", "propertyHash", memberName);
                AddSafeName(mappingProps, "tableName", "tableHash", tableName);
                AddSafeName(mappingProps, "columnName", "columnHash", columnName);
                facts.Add(CreateLegacyFact(manifest, FactTypes.LegacyDataMappingDeclared, RuleIds.LegacyDataDbml, item.RelativePath, column, TargetFrom(mappingProps, "propertyName", "propertyHash"), mappingProps));
            }
        }

        foreach (var association in database.Descendants().Where(element => element.Name.LocalName == "Association").OrderBy(GetLine))
        {
            var associationName = AttributeValue(association, "Name") ?? AttributeValue(association, "Member") ?? "association";
            var properties = MetadataProperties("Dbml", metadataHash, "association");
            properties["mappingKind"] = "association";
            AddSafeName(properties, "associationName", "associationHash", associationName);
            AddSafeName(properties, "sourceMemberName", "sourceMemberHash", AttributeValue(association, "ThisKey"));
            AddSafeName(properties, "targetMemberName", "targetMemberHash", AttributeValue(association, "OtherKey"));
            facts.Add(CreateLegacyFact(manifest, FactTypes.LegacyDataMappingDeclared, RuleIds.LegacyDataDbml, item.RelativePath, association, TargetFrom(properties, "associationName", "associationHash"), properties));
        }

        foreach (var routine in database.Descendants().Where(element => element.Name.LocalName is "Function" or "Method").OrderBy(GetLine))
        {
            var routineName = AttributeValue(routine, "Name") ?? AttributeValue(routine, "Method") ?? "routine";
            var properties = MetadataProperties("Dbml", metadataHash, "routine");
            properties["storageObjectKind"] = "Routine";
            properties["mappingKind"] = "routine";
            AddSafeName(properties, "storageObjectName", "storageObjectHash", routineName);
            AddSafeName(properties, "methodName", "methodHash", AttributeValue(routine, "Method"));
            facts.Add(CreateLegacyFact(manifest, FactTypes.LegacyDataStorageObjectDeclared, RuleIds.LegacyDataDbml, item.RelativePath, routine, TargetFrom(properties, "storageObjectName", "storageObjectHash"), properties));
        }
    }

    private static void ExtractEdmx(string repoPath, ScanManifest manifest, FileInventoryItem item, List<CodeFact> facts)
    {
        var fullPath = Path.Combine(repoPath, item.RelativePath);
        if (!TryLoadMetadataDocument(manifest, item, fullPath, "Edmx", facts, out var document, out var metadataHash))
        {
            return;
        }

        AddMetadataInventoryFact(manifest, facts, item.RelativePath, "Edmx", metadataHash, RuleIds.LegacyDataMetadataInventory, document.Root);

        var csdlSchemas = document.Descendants().Where(IsCsdlSchema).ToArray();
        var ssdlSchemas = document.Descendants().Where(IsSsdlSchema).ToArray();
        var mappingElements = document.Descendants().Where(IsMslMapping).ToArray();
        if (csdlSchemas.Length == 0 || ssdlSchemas.Length == 0 || mappingElements.Length == 0)
        {
            AddGap(manifest, facts, item.RelativePath, RuleIds.LegacyDataEdmx, "UnsupportedLegacyDataMetadataVersion", "EDMX runtime CSDL, SSDL, or MSL sections were missing.", document.Root);
        }

        var conceptualContainers = csdlSchemas.SelectMany(schema => schema.Elements().Where(element => element.Name.LocalName == "EntityContainer")).ToArray();
        var storageContainers = ssdlSchemas.SelectMany(schema => schema.Elements().Where(element => element.Name.LocalName == "EntityContainer")).ToArray();
        if (conceptualContainers.Length > 1 || storageContainers.Length > 1)
        {
            AddGap(manifest, facts, item.RelativePath, RuleIds.LegacyDataEdmx, "AmbiguousEdmxMapping", "Multiple conceptual or storage containers require review.", document.Root);
        }

        foreach (var entity in csdlSchemas.SelectMany(schema => schema.Elements().Where(element => element.Name.LocalName == "EntityType")).OrderBy(GetLine).ThenBy(element => AttributeValue(element, "Name"), StringComparer.Ordinal))
        {
            var name = AttributeValue(entity, "Name") ?? "entity";
            var properties = MetadataProperties("Edmx", metadataHash, "csdl-entity");
            properties["sourceSection"] = "CSDL";
            AddSafeName(properties, "entityName", "entityHash", name);
            AddSafeName(properties, "typeName", "typeHash", name);
            facts.Add(CreateLegacyFact(manifest, FactTypes.LegacyDataEntityDeclared, RuleIds.LegacyDataEdmx, item.RelativePath, entity, TargetFrom(properties, "typeName", "typeHash"), properties));

            foreach (var property in entity.Elements().Where(element => element.Name.LocalName is "Property" or "NavigationProperty").OrderBy(GetLine))
            {
                var propertyName = AttributeValue(property, "Name") ?? "property";
                var columnProps = MetadataProperties("Edmx", metadataHash, "csdl-property");
                columnProps["sourceSection"] = "CSDL";
                AddSafeName(columnProps, "entityName", "entityHash", name);
                AddSafeName(columnProps, "propertyName", "propertyHash", propertyName);
                AddOptional(columnProps, "descriptorKind", property.Name.LocalName);
                facts.Add(CreateLegacyFact(manifest, FactTypes.LegacyDataColumnDeclared, RuleIds.LegacyDataEdmx, item.RelativePath, property, TargetFrom(columnProps, "propertyName", "propertyHash"), columnProps));
            }
        }

        foreach (var set in conceptualContainers.SelectMany(container => container.Elements().Where(element => element.Name.LocalName == "EntitySet")).OrderBy(GetLine))
        {
            var name = AttributeValue(set, "Name") ?? "entity-set";
            var properties = MetadataProperties("Edmx", metadataHash, "csdl-entity-set");
            properties["sourceSection"] = "CSDL";
            AddSafeName(properties, "entityName", "entityHash", name);
            AddSafeName(properties, "entityTypeName", "entityTypeHash", LocalName(AttributeValue(set, "EntityType")));
            facts.Add(CreateLegacyFact(manifest, FactTypes.LegacyDataEntityDeclared, RuleIds.LegacyDataEdmx, item.RelativePath, set, TargetFrom(properties, "entityName", "entityHash"), properties));
        }

        foreach (var set in storageContainers.SelectMany(container => container.Elements().Where(element => element.Name.LocalName == "EntitySet")).OrderBy(GetLine))
        {
            var tableName = AttributeValue(set, "Table") ?? AttributeValue(set, "Name") ?? "storage";
            var properties = MetadataProperties("Edmx", metadataHash, "ssdl-entity-set");
            properties["sourceSection"] = "SSDL";
            properties["storageObjectKind"] = "TableOrView";
            AddSafeName(properties, "storageObjectName", "storageObjectHash", tableName);
            AddSafeName(properties, "entitySetName", "entitySetHash", AttributeValue(set, "Name"));
            AddHashOnly(properties, "providerNameHash", AttributeValue(set, "Schema"));
            facts.Add(CreateLegacyFact(manifest, FactTypes.LegacyDataStorageObjectDeclared, RuleIds.LegacyDataEdmx, item.RelativePath, set, TargetFrom(properties, "storageObjectName", "storageObjectHash"), properties));
        }

        foreach (var entity in ssdlSchemas.SelectMany(schema => schema.Elements().Where(element => element.Name.LocalName == "EntityType")).OrderBy(GetLine).ThenBy(element => AttributeValue(element, "Name"), StringComparer.Ordinal))
        {
            var storageType = AttributeValue(entity, "Name") ?? "storage-type";
            foreach (var property in entity.Elements().Where(element => element.Name.LocalName == "Property").OrderBy(GetLine))
            {
                var columnName = AttributeValue(property, "Name") ?? "column";
                var properties = MetadataProperties("Edmx", metadataHash, "ssdl-column");
                properties["sourceSection"] = "SSDL";
                AddSafeName(properties, "storageObjectName", "storageObjectHash", storageType);
                AddSafeName(properties, "columnName", "columnHash", columnName);
                facts.Add(CreateLegacyFact(manifest, FactTypes.LegacyDataColumnDeclared, RuleIds.LegacyDataEdmx, item.RelativePath, property, TargetFrom(properties, "columnName", "columnHash"), properties));
            }
        }

        foreach (var function in ssdlSchemas.SelectMany(schema => schema.Elements().Where(element => element.Name.LocalName is "Function" or "FunctionImport")).OrderBy(GetLine))
        {
            var routineName = AttributeValue(function, "Name") ?? "routine";
            var properties = MetadataProperties("Edmx", metadataHash, "routine");
            properties["sourceSection"] = IsSsdl(function) ? "SSDL" : "CSDL";
            properties["storageObjectKind"] = "Routine";
            AddSafeName(properties, "storageObjectName", "storageObjectHash", routineName);
            facts.Add(CreateLegacyFact(manifest, FactTypes.LegacyDataStorageObjectDeclared, RuleIds.LegacyDataEdmx, item.RelativePath, function, TargetFrom(properties, "storageObjectName", "storageObjectHash"), properties));
        }

        AddEdmxMappings(manifest, facts, item.RelativePath, metadataHash, mappingElements);
    }

    private static void AddEdmxMappings(ScanManifest manifest, List<CodeFact> facts, string relativePath, string metadataHash, IReadOnlyList<XElement> mappingElements)
    {
        foreach (var unsupported in mappingElements.SelectMany(mapping => mapping.Descendants()).Where(element => element.Name.LocalName is "Condition" or "ComplexProperty" or "AssociationSetMapping" or "FunctionImportMapping").OrderBy(GetLine))
        {
            AddGap(manifest, facts, relativePath, RuleIds.LegacyDataEdmx, "UnsupportedEdmxMappingShape", $"Unsupported EDMX mapping shape: {unsupported.Name.LocalName}.", unsupported);
        }

        foreach (var entitySetMapping in mappingElements.SelectMany(mapping => mapping.Descendants().Where(element => element.Name.LocalName == "EntitySetMapping")).OrderBy(GetLine))
        {
            var entitySetName = AttributeValue(entitySetMapping, "Name") ?? "entity-set";
            var fragments = entitySetMapping.Descendants().Where(element => element.Name.LocalName == "MappingFragment").ToArray();
            if (fragments.Length != 1)
            {
                AddGap(manifest, facts, relativePath, RuleIds.LegacyDataEdmx, "AmbiguousEdmxMapping", "EntitySetMapping did not contain exactly one MappingFragment.", entitySetMapping);
                continue;
            }

            var fragment = fragments[0];
            var storeSet = AttributeValue(fragment, "StoreEntitySet") ?? "storage";
            var mappingProps = MetadataProperties("Edmx", metadataHash, "entity-table");
            mappingProps["sourceSection"] = "MSL";
            mappingProps["mappingKind"] = "entity-table";
            AddSafeName(mappingProps, "entityName", "entityHash", entitySetName);
            AddSafeName(mappingProps, "tableName", "tableHash", storeSet);
            facts.Add(CreateLegacyFact(manifest, FactTypes.LegacyDataMappingDeclared, RuleIds.LegacyDataEdmx, relativePath, fragment, TargetFrom(mappingProps, "entityName", "entityHash"), mappingProps));

            foreach (var scalar in fragment.Descendants().Where(element => element.Name.LocalName == "ScalarProperty").OrderBy(GetLine))
            {
                var propertyName = AttributeValue(scalar, "Name") ?? "property";
                var columnName = AttributeValue(scalar, "ColumnName") ?? "column";
                var properties = MetadataProperties("Edmx", metadataHash, "property-column");
                properties["sourceSection"] = "MSL";
                properties["mappingKind"] = "property-column";
                AddSafeName(properties, "entityName", "entityHash", entitySetName);
                AddSafeName(properties, "tableName", "tableHash", storeSet);
                AddSafeName(properties, "propertyName", "propertyHash", propertyName);
                AddSafeName(properties, "columnName", "columnHash", columnName);
                facts.Add(CreateLegacyFact(manifest, FactTypes.LegacyDataMappingDeclared, RuleIds.LegacyDataEdmx, relativePath, scalar, TargetFrom(properties, "propertyName", "propertyHash"), properties));
            }
        }
    }

    private static void ExtractTypedDataSet(string repoPath, ScanManifest manifest, FileInventoryItem item, List<CodeFact> facts)
    {
        var fullPath = Path.Combine(repoPath, item.RelativePath);
        if (!TryLoadMetadataDocument(manifest, item, fullPath, "TypedDataSet", facts, out var document, out var metadataHash))
        {
            return;
        }

        var indicators = TypedDataSetIndicators(document);
        if (!indicators.HasIntrinsicIndicator)
        {
            return;
        }

        AddMetadataInventoryFact(manifest, facts, item.RelativePath, "TypedDataSet", metadataHash, RuleIds.LegacyDataMetadataInventory, document.Root);
        if (!indicators.HasDescriptorContent)
        {
            AddGap(manifest, facts, item.RelativePath, RuleIds.LegacyDataTypedDataSet, "UnrelatedXsdSchemaGated", "XSD had typed DataSet namespace indicators but no DataSet or TableAdapter content.", document.Root);
            return;
        }

        foreach (var dataSet in document.Descendants().Where(IsDataSetElement).OrderBy(GetLine))
        {
            var dataSetName = AttributeValue(dataSet, "name") ?? AttributeValue(dataSet, MsProp + "Generator_DataSetName") ?? "DataSet";
            var properties = MetadataProperties("TypedDataSet", metadataHash, "dataset");
            AddSafeName(properties, "dataSetName", "dataSetHash", dataSetName);
            AddSafeName(properties, "typeName", "typeHash", dataSetName);
            facts.Add(CreateLegacyFact(manifest, FactTypes.LegacyDataEntityDeclared, RuleIds.LegacyDataTypedDataSet, item.RelativePath, dataSet, TargetFrom(properties, "dataSetName", "dataSetHash"), properties));
        }

        var tables = document.Descendants()
            .Where(element => AttributeValue(element, MsProp + "Generator_UserTableName") is not null
                || AttributeValue(element, MsProp + "Generator_RowClassName") is not null
                || AttributeValue(element, MsProp + "Generator_TableClassName") is not null)
            .OrderBy(GetLine)
            .ThenBy(element => AttributeValue(element, "name"), StringComparer.Ordinal)
            .ToArray();

        foreach (var table in tables)
        {
            var tableName = AttributeValue(table, MsProp + "Generator_UserTableName") ?? AttributeValue(table, "name") ?? "table";
            var rowClass = AttributeValue(table, MsProp + "Generator_RowClassName");
            var properties = MetadataProperties("TypedDataSet", metadataHash, "table");
            AddSafeName(properties, "tableName", "tableHash", tableName);
            AddSafeName(properties, "entityName", "entityHash", rowClass ?? tableName);
            AddSafeName(properties, "typeName", "typeHash", rowClass ?? tableName);
            facts.Add(CreateLegacyFact(manifest, FactTypes.LegacyDataEntityDeclared, RuleIds.LegacyDataTypedDataSet, item.RelativePath, table, TargetFrom(properties, "entityName", "entityHash"), properties));
            facts.Add(CreateLegacyFact(manifest, FactTypes.LegacyDataStorageObjectDeclared, RuleIds.LegacyDataTypedDataSet, item.RelativePath, table, TargetFrom(properties, "tableName", "tableHash"), With(properties, ("storageObjectKind", "DataTable"))));

            foreach (var column in table.Descendants().Where(element => element.Name.LocalName == "element" && AttributeValue(element, "name") is not null).OrderBy(GetLine))
            {
                if (ReferenceEquals(column, table))
                {
                    continue;
                }

                var columnName = AttributeValue(column, "name") ?? "column";
                var columnProps = MetadataProperties("TypedDataSet", metadataHash, "column");
                AddSafeName(columnProps, "tableName", "tableHash", tableName);
                AddSafeName(columnProps, "columnName", "columnHash", columnName);
                AddSafeName(columnProps, "propertyName", "propertyHash", columnName);
                facts.Add(CreateLegacyFact(manifest, FactTypes.LegacyDataColumnDeclared, RuleIds.LegacyDataTypedDataSet, item.RelativePath, column, TargetFrom(columnProps, "columnName", "columnHash"), columnProps));
            }
        }

        foreach (var relation in document.Descendants().Where(element => element.Name == MsData + "Relationship").OrderBy(GetLine))
        {
            var properties = MetadataProperties("TypedDataSet", metadataHash, "relation");
            properties["mappingKind"] = "relation";
            AddSafeName(properties, "relationName", "relationHash", AttributeValue(relation, "name"));
            AddSafeName(properties, "parentTableName", "parentTableHash", AttributeValue(relation, "parent"));
            AddSafeName(properties, "childTableName", "childTableHash", AttributeValue(relation, "child"));
            facts.Add(CreateLegacyFact(manifest, FactTypes.LegacyDataMappingDeclared, RuleIds.LegacyDataTypedDataSet, item.RelativePath, relation, TargetFrom(properties, "relationName", "relationHash"), properties));
        }

        foreach (var command in document.Descendants().Where(IsTableAdapterCommand).OrderBy(GetLine))
        {
            AddTableAdapterCommandFacts(manifest, facts, item.RelativePath, metadataHash, command);
        }
    }

    private static void AddTableAdapterCommandFacts(ScanManifest manifest, List<CodeFact> facts, string relativePath, string metadataHash, XElement command)
    {
        var commandText = AttributeValue(command, "CommandText")
            ?? AttributeValue(command, "commandText")
            ?? command.Elements().FirstOrDefault(element => element.Name.LocalName.Contains("CommandText", StringComparison.OrdinalIgnoreCase))?.Value.Trim();
        var commandName = AttributeValue(command, "Name") ?? AttributeValue(command, "name") ?? AttributeValue(command, "MethodName") ?? command.Name.LocalName;
        var properties = MetadataProperties("TypedDataSet", metadataHash, "table-adapter-command");
        AddSafeName(properties, "commandName", "commandHash", commandName);
        AddSafeName(properties, "methodName", "methodHash", AttributeValue(command, "MethodName"));
        properties["mappingKind"] = "adapter-command";

        if (string.IsNullOrWhiteSpace(commandText))
        {
            facts.Add(CreateLegacyFact(manifest, FactTypes.LegacyDataMappingDeclared, RuleIds.LegacyDataTypedDataSet, relativePath, command, TargetFrom(properties, "commandName", "commandHash"), properties));
            AddGap(manifest, facts, relativePath, RuleIds.LegacyDataTypedDataSet, "DynamicTableAdapterCommand", "TableAdapter command text was not complete static text.", command);
            return;
        }

        properties["textHash"] = FactFactory.Hash(commandText, 32);
        properties["textLength"] = commandText.Length.ToString();
        facts.Add(CreateLegacyFact(manifest, FactTypes.LegacyDataMappingDeclared, RuleIds.LegacyDataTypedDataSet, relativePath, command, TargetFrom(properties, "commandName", "commandHash"), properties));

        if (SqlTextDetector.IsSqlLike(commandText))
        {
            facts.Add(FactFactory.Create(
                manifest,
                FactTypes.SqlTextUsed,
                RuleIds.LegacyDataTypedDataSet,
                EvidenceTiers.Tier2Structural,
                Evidence(relativePath, command),
                targetSymbol: TargetFrom(properties, "commandName", "commandHash"),
                properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["metadataKind"] = "TypedDataSet",
                    ["sqlSourceKind"] = "typed-dataset-tableadapter",
                    ["textHash"] = FactFactory.Hash(commandText, 32),
                    ["textLength"] = commandText.Length.ToString(),
                    ["operationName"] = SqlShapeExtractor.OperationName(commandText)
                }));

            var shape = SqlShapeExtractor.QueryShapeProperties(commandText, "typed-dataset-tableadapter");
            shape["metadataKind"] = "TypedDataSet";
            facts.Add(FactFactory.Create(
                manifest,
                FactTypes.QueryPatternDetected,
                RuleIds.LegacyDataTypedDataSet,
                EvidenceTiers.Tier2Structural,
                Evidence(relativePath, command),
                targetSymbol: TargetFrom(properties, "commandName", "commandHash"),
                properties: shape));
        }
    }

    private static void ExtractDataConfig(string repoPath, ScanManifest manifest, FileInventoryItem item, List<CodeFact> facts)
    {
        var fileName = Path.GetFileName(item.RelativePath);
        if (Regex.IsMatch(fileName, @"\.(Debug|Release|Staging|Production)\.config$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)))
        {
            AddGap(manifest, facts, item.RelativePath, RuleIds.LegacyDataConfig, "ConfigTransformPresent", "Config transform companion file is checked in.", null);
        }

        var fullPath = Path.Combine(repoPath, item.RelativePath);
        if (!TryLoadMetadataDocument(manifest, item, fullPath, "Config", facts, out var document, out var metadataHash))
        {
            return;
        }

        foreach (var transformAttr in document.Descendants().Attributes().Where(attribute => attribute.Name.LocalName is "Transform" or "Locator").OrderBy(attribute => GetLine(attribute.Parent)))
        {
            AddGap(manifest, facts, item.RelativePath, RuleIds.LegacyDataConfig, "ConfigTransformPresent", "XDT transform attributes were present.", transformAttr.Parent);
        }

        foreach (var section in document.Descendants().Where(element => AttributeValue(element, "configProtectionProvider") is not null).OrderBy(GetLine))
        {
            AddGap(manifest, facts, item.RelativePath, RuleIds.LegacyDataConfig, "EncryptedConfigSection", "Encrypted or protected config section is opaque to static extraction.", section);
        }

        foreach (var include in document.Descendants().Where(element => AttributeValue(element, "configSource") is not null || AttributeValue(element, "file") is not null).OrderBy(GetLine))
        {
            AddGap(manifest, facts, item.RelativePath, RuleIds.LegacyDataConfig, "ExternalConfigInclude", "Config uses external include/source behavior that TraceMap does not load.", include);
        }

        foreach (var add in document.Descendants().Where(element => element.Name.LocalName == "add").OrderBy(GetLine).ThenBy(element => AttributeValue(element, "name") ?? AttributeValue(element, "invariant"), StringComparer.Ordinal))
        {
            var parentName = add.Parent?.Name.LocalName ?? string.Empty;
            if (parentName.Equals("connectionStrings", StringComparison.OrdinalIgnoreCase))
            {
                var connectionName = AttributeValue(add, "name");
                if (string.IsNullOrWhiteSpace(connectionName))
                {
                    continue;
                }

                var properties = MetadataProperties("Config", metadataHash, "connection-string");
                properties["configSection"] = "connectionStrings";
                properties["connectionStringPresent"] = string.IsNullOrWhiteSpace(AttributeValue(add, "connectionString")) ? "False" : "True";
                AddSafeName(properties, "connectionName", "connectionNameHash", connectionName);
                AddSafeName(properties, "providerInvariantName", "providerInvariantHash", AttributeValue(add, "providerName"));
                AddHashOnly(properties, "connectionStringHash", AttributeValue(add, "connectionString"));
                facts.Add(CreateLegacyFact(manifest, FactTypes.LegacyDataProviderConfigDeclared, RuleIds.LegacyDataConfig, item.RelativePath, add, TargetFrom(properties, "connectionName", "connectionNameHash"), properties));
            }
            else if (parentName.Equals("DbProviderFactories", StringComparison.OrdinalIgnoreCase) || add.Attributes().Any(attribute => attribute.Name.LocalName.Equals("invariant", StringComparison.OrdinalIgnoreCase)))
            {
                var invariant = AttributeValue(add, "invariant") ?? AttributeValue(add, "invariantName") ?? AttributeValue(add, "name");
                var properties = MetadataProperties("Config", metadataHash, "provider-factory");
                properties["configSection"] = "DbProviderFactories";
                AddSafeName(properties, "providerInvariantName", "providerInvariantHash", invariant);
                AddHashOnly(properties, "providerTypeHash", AttributeValue(add, "type"));
                facts.Add(CreateLegacyFact(manifest, FactTypes.LegacyDataProviderConfigDeclared, RuleIds.LegacyDataConfig, item.RelativePath, add, TargetFrom(properties, "providerInvariantName", "providerInvariantHash"), properties));
            }
        }

        foreach (var provider in document.Descendants().Where(element => element.Name.LocalName is "provider" or "defaultConnectionFactory").OrderBy(GetLine))
        {
            var properties = MetadataProperties("Config", metadataHash, "ef-provider");
            properties["configSection"] = provider.Name.LocalName;
            AddSafeName(properties, "providerInvariantName", "providerInvariantHash", AttributeValue(provider, "invariantName") ?? AttributeValue(provider, "name"));
            AddHashOnly(properties, "providerTypeHash", AttributeValue(provider, "type"));
            facts.Add(CreateLegacyFact(manifest, FactTypes.LegacyDataProviderConfigDeclared, RuleIds.LegacyDataConfig, item.RelativePath, provider, TargetFrom(properties, "providerInvariantName", "providerInvariantHash"), properties));
        }
    }

    private static void AddGeneratedDesignerInventory(ScanManifest manifest, FileInventoryItem item, List<CodeFact> facts)
    {
        var properties = MetadataProperties("GeneratedDesigner", FactFactory.Hash(item.RelativePath, 32), "generated-designer");
        properties["generatedCodeFileName"] = Path.GetFileName(item.RelativePath);
        properties["linkageCandidate"] = "True";
        facts.Add(FactFactory.Create(
            manifest,
            FactTypes.LegacyDataMetadataDeclared,
            RuleIds.LegacyDataMetadataInventory,
            EvidenceTiers.Tier2Structural,
            new EvidenceSpan(item.RelativePath, 1, 1, null, ExtractorId, ScannerVersions.LegacyDataExtractor),
            targetSymbol: Path.GetFileName(item.RelativePath),
            properties: properties));
    }

    private static void AddGeneratedCodeLinks(ScanManifest manifest, List<CodeFact> facts, IReadOnlyList<GeneratedCandidate> generatedCandidates, IReadOnlyList<CodeFact> existingFacts)
    {
        var metadataFacts = facts
            .Where(fact => fact.RuleId is RuleIds.LegacyDataDbml or RuleIds.LegacyDataEdmx or RuleIds.LegacyDataTypedDataSet
                && fact.FactType is FactTypes.LegacyDataEntityDeclared or FactTypes.LegacyDataStorageObjectDeclared)
            .ToArray();

        foreach (var fact in metadataFacts)
        {
            var expectedType = FirstPresent(
                fact.Properties.GetValueOrDefault("typeName"),
                fact.Properties.GetValueOrDefault("entityName"),
                fact.Properties.GetValueOrDefault("dataSetName"));
            if (string.IsNullOrWhiteSpace(expectedType))
            {
                continue;
            }

            var explicitGeneratedName = fact.Properties.GetValueOrDefault("generatedCodeFileName");
            var metadataBaseName = Path.GetFileNameWithoutExtension(fact.Evidence.FilePath);
            var scopedCandidates = generatedCandidates
                .Where(candidate => string.IsNullOrWhiteSpace(explicitGeneratedName)
                    ? Path.GetFileNameWithoutExtension(candidate.FilePath).StartsWith(metadataBaseName, StringComparison.OrdinalIgnoreCase)
                    : Path.GetFileName(candidate.FilePath).Equals(explicitGeneratedName, StringComparison.OrdinalIgnoreCase))
                .Where(candidate => candidate.TypeNames.Contains(expectedType, StringComparer.Ordinal))
                .ToArray();

            if (scopedCandidates.Length == 1)
            {
                var candidate = scopedCandidates[0];
                var properties = MetadataProperties(fact.Properties.GetValueOrDefault("metadataKind") ?? "LegacyData", fact.Properties.GetValueOrDefault("metadataHash") ?? string.Empty, "generated-code-link");
                properties["supportingFactId"] = fact.FactId;
                properties["linkKind"] = string.IsNullOrWhiteSpace(explicitGeneratedName) ? "type-name-syntax-fallback" : "explicit-generated-file";
                properties["generatedCodeFileName"] = Path.GetFileName(candidate.FilePath);
                properties["generatedCodeFilePath"] = candidate.FilePath;
                AddSafeName(properties, "typeName", "typeHash", expectedType);
                facts.Add(FactFactory.Create(
                    manifest,
                    FactTypes.LegacyDataGeneratedCodeLinked,
                    RuleIds.LegacyDataGeneratedLink,
                    string.IsNullOrWhiteSpace(explicitGeneratedName) ? EvidenceTiers.Tier3SyntaxOrTextual : EvidenceTiers.Tier2Structural,
                    new EvidenceSpan(candidate.FilePath, candidate.LineFor(expectedType), candidate.LineFor(expectedType), null, ExtractorId, ScannerVersions.LegacyDataExtractor),
                    targetSymbol: expectedType,
                    properties: properties));
            }
            else if (scopedCandidates.Length > 1)
            {
                AddGap(manifest, facts, fact.Evidence.FilePath, RuleIds.LegacyDataGeneratedLink, "AmbiguousGeneratedCodeLink", "Multiple generated-code candidates matched a legacy data descriptor.", null);
            }
            else if (!string.IsNullOrWhiteSpace(explicitGeneratedName))
            {
                AddGap(manifest, facts, fact.Evidence.FilePath, RuleIds.LegacyDataGeneratedLink, "MissingGeneratedCode", "Metadata names generated output that was not checked in.", null);
            }
        }

        _ = existingFacts;
    }

    private static IReadOnlyList<GeneratedCandidate> LoadGeneratedCandidates(string repoPath, IReadOnlyList<FileInventoryItem> inventory)
    {
        var candidates = new List<GeneratedCandidate>();
        foreach (var item in inventory.Where(item => item.Kind == "CSharp" && IsDesigner(item.RelativePath)).OrderBy(item => item.RelativePath, StringComparer.Ordinal))
        {
            var fullPath = Path.Combine(repoPath, item.RelativePath);
            try
            {
                var source = File.ReadAllText(fullPath);
                var tree = CSharpSyntaxTree.ParseText(source, path: item.RelativePath);
                var root = tree.GetCompilationUnitRoot();
                var types = root.DescendantNodes()
                    .OfType<TypeDeclarationSyntax>()
                    .Select(type => (Name: type.Identifier.ValueText, Line: Line(tree, type)))
                    .Where(item => !string.IsNullOrWhiteSpace(item.Name))
                    .ToArray();
                if (types.Length > 0)
                {
                    candidates.Add(new GeneratedCandidate(item.RelativePath, types.ToDictionary(type => type.Name, type => type.Line, StringComparer.Ordinal)));
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }

        return candidates;
    }

    private static bool TryLoadMetadataDocument(
        ScanManifest manifest,
        FileInventoryItem item,
        string fullPath,
        string metadataKind,
        List<CodeFact> facts,
        out XDocument document,
        out string metadataHash)
    {
        document = new XDocument();
        metadataHash = string.Empty;
        try
        {
            metadataHash = FactFactory.Hash(File.ReadAllText(fullPath), 32);
            document = SafeXml.LoadDocument(fullPath);
            return true;
        }
        catch (SafeXmlException ex)
        {
            AddGap(manifest, facts, item.RelativePath, RuleIds.LegacyDataMetadataInventory, Classification(ex), $"{metadataKind} metadata could not be parsed safely.", null);
            return false;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AddGap(manifest, facts, item.RelativePath, RuleIds.LegacyDataMetadataInventory, "MalformedLegacyDataMetadata", $"{metadataKind} metadata could not be read: {ex.GetType().Name}.", null);
            return false;
        }
    }

    private static string Classification(SafeXmlException ex)
    {
        return ex.FailureKind switch
        {
            SafeXmlFailureKind.SecurityRejected => "LegacyDataParserSecurityRejected",
            SafeXmlFailureKind.TooLarge => "LegacyDataMetadataTooLarge",
            _ => "MalformedLegacyDataMetadata"
        };
    }

    private static void AddMetadataInventoryFact(ScanManifest manifest, List<CodeFact> facts, string relativePath, string metadataKind, string metadataHash, string ruleId, XElement? element)
    {
        facts.Add(FactFactory.Create(
            manifest,
            FactTypes.LegacyDataMetadataDeclared,
            ruleId,
            EvidenceTiers.Tier2Structural,
            element is null ? new EvidenceSpan(relativePath, 1, 1, null, ExtractorId, ScannerVersions.LegacyDataExtractor) : Evidence(relativePath, element),
            targetSymbol: metadataKind,
            properties: MetadataProperties(metadataKind, metadataHash, "document")));
    }

    private static void AddGap(ScanManifest manifest, List<CodeFact> facts, string relativePath, string ruleId, string classification, string message, XObject? node)
    {
        var line = node is null ? 1 : GetLine(node);
        facts.Add(FactFactory.Create(
            manifest,
            FactTypes.AnalysisGap,
            ruleId,
            EvidenceTiers.Tier4Unknown,
            new EvidenceSpan(relativePath, line, line, null, ExtractorId, ScannerVersions.LegacyDataExtractor),
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["classification"] = classification,
                ["coverage"] = "reduced",
                ["message"] = message
            }));
    }

    private static CodeFact CreateLegacyFact(
        ScanManifest manifest,
        string factType,
        string ruleId,
        string relativePath,
        XElement element,
        string? targetSymbol,
        SortedDictionary<string, string> properties)
    {
        properties["evidenceScope"] = "static-design-time-metadata";
        properties["runtimeProof"] = "False";
        return FactFactory.Create(
            manifest,
            factType,
            ruleId,
            EvidenceTiers.Tier2Structural,
            Evidence(relativePath, element),
            targetSymbol: string.IsNullOrWhiteSpace(targetSymbol) ? null : targetSymbol,
            contractElement: string.IsNullOrWhiteSpace(targetSymbol) ? null : targetSymbol,
            properties: properties);
    }

    private static EvidenceSpan Evidence(string relativePath, XElement element)
    {
        var line = GetLine(element);
        return new EvidenceSpan(relativePath, line, line, null, ExtractorId, ScannerVersions.LegacyDataExtractor);
    }

    private static SortedDictionary<string, string> MetadataProperties(string metadataKind, string metadataHash, string descriptorKind)
    {
        return new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["descriptorKind"] = descriptorKind,
            ["metadataHash"] = metadataHash,
            ["metadataKind"] = metadataKind
        };
    }

    private static SortedDictionary<string, string> With(SortedDictionary<string, string> source, params (string Key, string Value)[] values)
    {
        var copy = new SortedDictionary<string, string>(source, StringComparer.Ordinal);
        foreach (var (key, value) in values)
        {
            copy[key] = value;
        }

        return copy;
    }

    private static void AddGeneratedHints(SortedDictionary<string, string> properties, XElement database, string relativePath)
    {
        AddSafeName(properties, "contextTypeName", "contextTypeHash", AttributeValue(database, "Class"));
        var generatedFile = AttributeValue(database, "GeneratedFile") ?? Path.GetFileNameWithoutExtension(relativePath) + ".designer.cs";
        properties["generatedCodeFileName"] = Path.GetFileName(generatedFile);
    }

    private static void AddSafeName(SortedDictionary<string, string> properties, string clearKey, string hashKey, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var normalized = value.Trim();
        if (IsSafeIdentifier(normalized))
        {
            properties[clearKey] = normalized;
            return;
        }

        properties[hashKey] = FactFactory.Hash(normalized, 32);
        properties[$"{clearKey}Redaction"] = "hashed-unsafe-value";
    }

    private static void AddHashOnly(SortedDictionary<string, string> properties, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            properties[key] = FactFactory.Hash(value.Trim(), 32);
        }
    }

    private static void AddOptional(SortedDictionary<string, string> properties, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            properties[key] = value.Trim();
        }
    }

    private static bool IsSafeIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 128)
        {
            return false;
        }

        var lower = value.ToLowerInvariant();
        if (lower.Contains("password", StringComparison.Ordinal)
            || lower.Contains("secret", StringComparison.Ordinal)
            || lower.Contains("token", StringComparison.Ordinal)
            || lower.Contains("apikey", StringComparison.Ordinal)
            || lower.Contains("api_key", StringComparison.Ordinal)
            || lower.Contains("connectionstring", StringComparison.Ordinal)
            || value.Contains("://", StringComparison.Ordinal)
            || value.Contains("\\", StringComparison.Ordinal)
            || value.Contains("/", StringComparison.Ordinal)
            || value.Contains(";", StringComparison.Ordinal)
            || value.Contains("=", StringComparison.Ordinal)
            || value.Contains("@", StringComparison.Ordinal))
        {
            return false;
        }

        return value.All(ch => char.IsLetterOrDigit(ch) || ch is '_' or '.' or '-' or ' ');
    }

    private static string? TargetFrom(IReadOnlyDictionary<string, string> properties, string clearKey, string hashKey)
    {
        return properties.GetValueOrDefault(clearKey)
            ?? (properties.TryGetValue(hashKey, out var hash) ? $"hash:{hash}" : null);
    }

    private static string? FirstPresent(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
    }

    private static string? AttributeValue(XElement? element, string name)
    {
        return element?.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase))?.Value.Trim();
    }

    private static string? AttributeValue(XElement? element, XName name)
    {
        return element?.Attribute(name)?.Value.Trim();
    }

    private static int GetLine(XObject? node)
    {
        return node is IXmlLineInfo lineInfo && lineInfo.HasLineInfo()
            ? Math.Max(1, lineInfo.LineNumber)
            : 1;
    }

    private static bool IsCsdlSchema(XElement element)
    {
        return element.Name.LocalName == "Schema" && !element.Name.NamespaceName.Contains("/ssdl", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSsdlSchema(XElement element)
    {
        return element.Name.LocalName == "Schema" && element.Name.NamespaceName.Contains("/ssdl", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMslMapping(XElement element)
    {
        return element.Name.LocalName == "Mapping" && element.Name.NamespaceName.Contains("/mapping", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSsdl(XElement element)
    {
        return element.Name.NamespaceName.Contains("/ssdl", StringComparison.OrdinalIgnoreCase);
    }

    private static string? LocalName(string? qualifiedName)
    {
        if (string.IsNullOrWhiteSpace(qualifiedName))
        {
            return null;
        }

        var index = qualifiedName.LastIndexOf(".", StringComparison.Ordinal);
        return index >= 0 ? qualifiedName[(index + 1)..] : qualifiedName;
    }

    private static TypedDataSetIndicatorResult TypedDataSetIndicators(XDocument document)
    {
        var hasNamespace = document.Root?.Attributes().Any(attribute =>
            attribute.Name.LocalName.Equals("msdata", StringComparison.OrdinalIgnoreCase)
            || attribute.Value.Equals(MsData.NamespaceName, StringComparison.OrdinalIgnoreCase)) == true;
        var elements = document.Descendants().ToArray();
        var hasIntrinsic = hasNamespace
            || elements.Any(element => element.Attributes().Any(attribute =>
                attribute.Name.Namespace == MsData
                || attribute.Name.Namespace == MsProp
                || attribute.Name.LocalName.StartsWith("Generator_", StringComparison.OrdinalIgnoreCase)));
        var hasContent = elements.Any(IsDataSetElement)
            || elements.Any(element => AttributeValue(element, MsProp + "Generator_UserTableName") is not null
                || AttributeValue(element, MsProp + "Generator_RowClassName") is not null
                || AttributeValue(element, MsProp + "Generator_TableClassName") is not null)
            || elements.Any(element => element.Name == MsData + "Relationship")
            || elements.Any(IsTableAdapterCommand);
        return new TypedDataSetIndicatorResult(hasIntrinsic, hasContent);
    }

    private static bool IsDataSetElement(XElement element)
    {
        return AttributeValue(element, MsData + "IsDataSet")?.Equals("true", StringComparison.OrdinalIgnoreCase) == true
            || AttributeValue(element, MsProp + "Generator_DataSetName") is not null;
    }

    private static bool IsTableAdapterCommand(XElement element)
    {
        return element.Name.LocalName.Contains("Command", StringComparison.OrdinalIgnoreCase)
            || element.Name.LocalName.Contains("TableAdapter", StringComparison.OrdinalIgnoreCase)
            || AttributeValue(element, "CommandText") is not null
            || AttributeValue(element, "commandText") is not null;
    }

    private static bool IsDesigner(string relativePath)
    {
        return Path.GetFileName(relativePath).EndsWith(".designer.cs", StringComparison.OrdinalIgnoreCase);
    }

    private static int Line(Microsoft.CodeAnalysis.SyntaxTree tree, TypeDeclarationSyntax type)
    {
        return tree.GetLineSpan(type.Span).StartLinePosition.Line + 1;
    }

    private sealed record TypedDataSetIndicatorResult(bool HasIntrinsicIndicator, bool HasDescriptorContent);

    private sealed record GeneratedCandidate(string FilePath, IReadOnlyDictionary<string, int> TypeLines)
    {
        public IReadOnlySet<string> TypeNames { get; } = TypeLines.Keys.ToHashSet(StringComparer.Ordinal);

        public int LineFor(string typeName)
        {
            return TypeLines.GetValueOrDefault(typeName, 1);
        }
    }
}
