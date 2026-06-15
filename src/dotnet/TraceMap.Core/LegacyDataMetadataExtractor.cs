using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace TraceMap.Core;

public static partial class LegacyDataMetadataExtractor
{
    private const string ExtractorId = "LegacyDataMetadataExtractor";
    private const string StaticCoverage = "StaticDesignTimeMetadata";
    private const string StaticLimitation = "Static checked-in metadata evidence only; does not prove runtime data access, SQL execution, database existence, provider compatibility, deployment, or production usage.";

    public static IReadOnlyList<CodeFact> Extract(
        string repoPath,
        ScanManifest manifest,
        IReadOnlyList<FileInventoryItem> inventory,
        IReadOnlyList<CodeFact>? existingFacts = null)
    {
        var facts = new List<CodeFact>();
        var descriptors = new List<GeneratedLinkDescriptor>();
        var files = inventory.OrderBy(item => item.RelativePath, StringComparer.Ordinal).ToArray();

        foreach (var file in files)
        {
            if (file.Kind == "Dbml")
            {
                ExtractXmlMetadata(repoPath, manifest, file, facts, descriptors, "Dbml", RuleIds.LegacyDataDbml, ExtractDbml);
            }
            else if (file.Kind == "Edmx")
            {
                ExtractXmlMetadata(repoPath, manifest, file, facts, descriptors, "Edmx", RuleIds.LegacyDataEdmx, ExtractEdmx);
            }
            else if (IsXsd(file))
            {
                ExtractTypedDataSetIfGated(repoPath, manifest, file, facts, descriptors);
            }
            else if (file.Kind == "Config")
            {
                ExtractConfig(repoPath, manifest, file, facts);
            }
            else if (IsGeneratedDesignerCandidate(repoPath, file))
            {
                AddInventoryFact(manifest, facts, file.RelativePath, "GeneratedDesigner", RuleIds.LegacyDataMetadataInventory, 1, null, null);
            }
        }

        AddGeneratedCodeLinks(repoPath, manifest, files, existingFacts ?? [], descriptors, facts);

        return facts
            .GroupBy(fact => fact.FactId, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(fact => fact.FactType, StringComparer.Ordinal)
            .ThenBy(fact => fact.Evidence.FilePath, StringComparer.Ordinal)
            .ThenBy(fact => fact.Evidence.StartLine)
            .ThenBy(fact => fact.TargetSymbol, StringComparer.Ordinal)
            .ThenBy(fact => fact.FactId, StringComparer.Ordinal)
            .ToArray();
    }

    private static void ExtractXmlMetadata(
        string repoPath,
        ScanManifest manifest,
        FileInventoryItem file,
        List<CodeFact> facts,
        List<GeneratedLinkDescriptor> descriptors,
        string metadataKind,
        string ruleId,
        Action<ScanManifest, FileInventoryItem, LegacyDataXmlDocument, List<CodeFact>, List<GeneratedLinkDescriptor>> extract)
    {
        var fullPath = Path.Combine(repoPath, file.RelativePath);
        try
        {
            var document = LegacyDataXml.Load(fullPath);
            AddInventoryFact(manifest, facts, file.RelativePath, metadataKind, RuleIds.LegacyDataMetadataInventory, 1, document.DocumentHash, null);
            extract(manifest, file, document, facts, descriptors);
        }
        catch (LegacyDataXmlException ex)
        {
            AddGap(manifest, facts, file.RelativePath, ruleId, ex.Classification, ex.Message, metadataKind);
        }
        catch (IOException)
        {
            AddGap(manifest, facts, file.RelativePath, ruleId, "MalformedLegacyDataMetadata", "metadata document could not be read safely", metadataKind);
        }
        catch (UnauthorizedAccessException)
        {
            AddGap(manifest, facts, file.RelativePath, ruleId, "MalformedLegacyDataMetadata", "metadata document could not be read safely", metadataKind);
        }
    }

    private static void ExtractDbml(
        ScanManifest manifest,
        FileInventoryItem file,
        LegacyDataXmlDocument xml,
        List<CodeFact> facts,
        List<GeneratedLinkDescriptor> descriptors)
    {
        var databases = xml.Document.Descendants().Where(element => element.Name.LocalName == "Database").ToArray();
        if (databases.Length != 1)
        {
            AddGap(manifest, facts, file.RelativePath, RuleIds.LegacyDataDbml, "UnsupportedLegacyDataMetadataVersion", "DBML metadata must contain exactly one Database descriptor.", "Dbml");
            return;
        }

        var database = databases[0];
        var databaseName = Attr(database, "Name");
        var contextType = Attr(database, "Class");
        if (!string.IsNullOrWhiteSpace(contextType))
        {
            AddEntityFact(manifest, facts, file.RelativePath, database, "Dbml", "context", contextType, null, xml.DocumentHash);
            descriptors.Add(new GeneratedLinkDescriptor(file.RelativePath, "Dbml", contextType!, contextType, ExpectedDesignerBasename(file.RelativePath), "context"));
        }

        foreach (var table in database.Descendants().Where(element => element.Name.LocalName == "Table").OrderBy(GetLine).ThenBy(element => Attr(element, "Name"), StringComparer.Ordinal))
        {
            var tableName = Attr(table, "Name");
            var member = Attr(table, "Member");
            var type = table.Elements().FirstOrDefault(element => element.Name.LocalName == "Type");
            var entityName = Attr(type, "Name") ?? member;
            AddStorageFact(manifest, facts, file.RelativePath, table, "Dbml", "table", tableName, xml.DocumentHash);
            if (!string.IsNullOrWhiteSpace(entityName))
            {
                AddEntityFact(manifest, facts, file.RelativePath, type ?? table, "Dbml", "entity", entityName, null, xml.DocumentHash);
                AddMappingFact(manifest, facts, file.RelativePath, table, "Dbml", "entity-table", entityName, tableName, xml.DocumentHash);
                descriptors.Add(new GeneratedLinkDescriptor(file.RelativePath, "Dbml", entityName!, entityName, ExpectedDesignerBasename(file.RelativePath), "entity"));
            }

            foreach (var column in table.Descendants().Where(element => element.Name.LocalName == "Column").OrderBy(GetLine).ThenBy(element => Attr(element, "Name"), StringComparer.Ordinal))
            {
                var columnName = Attr(column, "Name");
                var propertyName = Attr(column, "Member") ?? columnName;
                AddColumnFact(manifest, facts, file.RelativePath, column, "Dbml", "column", entityName, propertyName, columnName, xml.DocumentHash);
                AddMappingFact(manifest, facts, file.RelativePath, column, "Dbml", "property-column", propertyName, columnName, xml.DocumentHash, entityName, tableName);
            }

            foreach (var association in table.Descendants().Where(element => element.Name.LocalName == "Association").OrderBy(GetLine).ThenBy(element => Attr(element, "Name"), StringComparer.Ordinal))
            {
                AddMappingFact(manifest, facts, file.RelativePath, association, "Dbml", "association", Attr(association, "Member") ?? Attr(association, "Name"), Attr(association, "ThisKey") ?? Attr(association, "OtherKey"), xml.DocumentHash, entityName, tableName);
            }
        }

        foreach (var function in database.Descendants().Where(element => element.Name.LocalName is "Function" or "Method").OrderBy(GetLine).ThenBy(element => Attr(element, "Name"), StringComparer.Ordinal))
        {
            var routine = Attr(function, "Name") ?? Attr(function, "Method");
            AddStorageFact(manifest, facts, file.RelativePath, function, "Dbml", "routine", routine, xml.DocumentHash);
            AddMappingFact(manifest, facts, file.RelativePath, function, "Dbml", "routine", Attr(function, "Method") ?? routine, routine, xml.DocumentHash);
        }

        if (!string.IsNullOrWhiteSpace(databaseName) && !LegacyDataSafeValues.IsSafeIdentifier(databaseName))
        {
            AddGap(manifest, facts, file.RelativePath, RuleIds.LegacyDataDbml, "UnsupportedLegacyDataMetadataVersion", "DBML database/provider identity contained unsafe metadata and was hashed or omitted.", "Dbml", GetLine(database));
        }
    }

    private static void ExtractEdmx(
        ScanManifest manifest,
        FileInventoryItem file,
        LegacyDataXmlDocument xml,
        List<CodeFact> facts,
        List<GeneratedLinkDescriptor> descriptors)
    {
        var runtime = xml.Document.Descendants().FirstOrDefault(element => element.Name.LocalName == "Runtime");
        if (runtime is null)
        {
            AddGap(manifest, facts, file.RelativePath, RuleIds.LegacyDataEdmx, "UnsupportedLegacyDataMetadataVersion", "EDMX metadata is missing Runtime CSDL/SSDL/MSL sections.", "Edmx");
            return;
        }

        var conceptualSchemas = runtime.Descendants().Where(element => element.Name.LocalName == "ConceptualModels").SelectMany(element => element.Descendants().Where(IsSchema)).ToArray();
        var storageSchemas = runtime.Descendants().Where(element => element.Name.LocalName == "StorageModels").SelectMany(element => element.Descendants().Where(IsSchema)).ToArray();
        var mappingElements = runtime.Descendants().Where(element => element.Name.LocalName is "EntitySetMapping" or "ScalarProperty" or "MappingFragment" or "FunctionImportMapping").ToArray();
        if (conceptualSchemas.Length == 0 && storageSchemas.Length == 0 && mappingElements.Length == 0)
        {
            AddGap(manifest, facts, file.RelativePath, RuleIds.LegacyDataEdmx, "UnsupportedLegacyDataMetadataVersion", "EDMX runtime section did not contain supported CSDL, SSDL, or MSL metadata.", "Edmx");
            return;
        }

        if (conceptualSchemas.SelectMany(schema => schema.Elements().Where(element => element.Name.LocalName == "EntityContainer")).Count() > 1
            || storageSchemas.SelectMany(schema => schema.Elements().Where(element => element.Name.LocalName == "EntityContainer")).Count() > 1)
        {
            AddGap(manifest, facts, file.RelativePath, RuleIds.LegacyDataEdmx, "AmbiguousEdmxMapping", "EDMX metadata contains multiple conceptual or storage containers.", "Edmx");
        }

        foreach (var schema in conceptualSchemas.OrderBy(GetLine))
        {
            var namespaceName = Attr(schema, "Namespace");
            foreach (var entity in schema.Elements().Where(element => element.Name.LocalName == "EntityType").OrderBy(GetLine).ThenBy(element => Attr(element, "Name"), StringComparer.Ordinal))
            {
                var entityName = Attr(entity, "Name");
                AddEntityFact(manifest, facts, file.RelativePath, entity, "Edmx", "entity", entityName, namespaceName, xml.DocumentHash);
                if (!string.IsNullOrWhiteSpace(entityName))
                {
                    descriptors.Add(new GeneratedLinkDescriptor(file.RelativePath, "Edmx", entityName!, entityName, ExpectedDesignerBasename(file.RelativePath), "entity"));
                }

                foreach (var property in entity.Elements().Where(element => element.Name.LocalName is "Property" or "NavigationProperty").OrderBy(GetLine).ThenBy(element => Attr(element, "Name"), StringComparer.Ordinal))
                {
                    AddColumnFact(manifest, facts, file.RelativePath, property, "Edmx", "conceptual-property", entityName, Attr(property, "Name"), null, xml.DocumentHash);
                }
            }
        }

        foreach (var schema in storageSchemas.OrderBy(GetLine))
        {
            foreach (var entitySet in schema.Descendants().Where(element => element.Name.LocalName is "EntitySet" or "Function").OrderBy(GetLine).ThenBy(element => Attr(element, "Name"), StringComparer.Ordinal))
            {
                AddStorageFact(manifest, facts, file.RelativePath, entitySet, "Edmx", entitySet.Name.LocalName == "Function" ? "routine" : "storage-entity-set", Attr(entitySet, "Table") ?? Attr(entitySet, "Name"), xml.DocumentHash);
            }

            foreach (var entity in schema.Elements().Where(element => element.Name.LocalName == "EntityType").OrderBy(GetLine).ThenBy(element => Attr(element, "Name"), StringComparer.Ordinal))
            {
                foreach (var property in entity.Elements().Where(element => element.Name.LocalName == "Property").OrderBy(GetLine).ThenBy(element => Attr(element, "Name"), StringComparer.Ordinal))
                {
                    AddColumnFact(manifest, facts, file.RelativePath, property, "Edmx", "storage-column", Attr(entity, "Name"), null, Attr(property, "Name"), xml.DocumentHash);
                }
            }
        }

        AddEdmxMappings(manifest, file, xml, facts, runtime.Descendants().ToArray(), mappingElements);
    }

    private static void AddEdmxMappings(ScanManifest manifest, FileInventoryItem file, LegacyDataXmlDocument xml, List<CodeFact> facts, XElement[] allMappingDescendants, XElement[] mappingElements)
    {
        foreach (var unsupported in allMappingDescendants.Where(element => element.Name.LocalName is "Condition" or "ComplexProperty" or "AssociationSetMapping").OrderBy(GetLine))
        {
            AddGap(manifest, facts, file.RelativePath, RuleIds.LegacyDataEdmx, "UnsupportedEdmxMappingShape", $"Unsupported EDMX mapping element {unsupported.Name.LocalName}.", "Edmx", GetLine(unsupported));
        }

        foreach (var mapping in mappingElements.Where(element => element.Name.LocalName == "EntitySetMapping").OrderBy(GetLine).ThenBy(element => Attr(element, "Name"), StringComparer.Ordinal))
        {
            var fragments = mapping.Descendants().Where(element => element.Name.LocalName == "MappingFragment").ToArray();
            if (fragments.Length != 1)
            {
                AddGap(manifest, facts, file.RelativePath, RuleIds.LegacyDataEdmx, "AmbiguousEdmxMapping", "EDMX entity-set mapping does not contain exactly one storage fragment.", "Edmx", GetLine(mapping));
                continue;
            }

            var conceptualSet = Attr(mapping, "Name");
            var storageSet = Attr(fragments[0], "StoreEntitySet");
            AddMappingFact(manifest, facts, file.RelativePath, mapping, "Edmx", "entity-table", conceptualSet, storageSet, xml.DocumentHash);
            foreach (var scalar in fragments[0].Descendants().Where(element => element.Name.LocalName == "ScalarProperty").OrderBy(GetLine).ThenBy(element => Attr(element, "Name"), StringComparer.Ordinal))
            {
                AddMappingFact(manifest, facts, file.RelativePath, scalar, "Edmx", "property-column", Attr(scalar, "Name"), Attr(scalar, "ColumnName"), xml.DocumentHash, conceptualSet, storageSet);
            }
        }
    }

    private static void ExtractTypedDataSetIfGated(
        string repoPath,
        ScanManifest manifest,
        FileInventoryItem file,
        List<CodeFact> facts,
        List<GeneratedLinkDescriptor> descriptors)
    {
        var fullPath = Path.Combine(repoPath, file.RelativePath);
        try
        {
            var xml = LegacyDataXml.Load(fullPath);
            if (!HasTypedDataSetIndicator(xml.Document))
            {
                return;
            }

            AddInventoryFact(manifest, facts, file.RelativePath, "TypedDataSet", RuleIds.LegacyDataMetadataInventory, 1, xml.DocumentHash, null);
            ExtractTypedDataSet(manifest, file, xml, facts, descriptors);
        }
        catch (LegacyDataXmlException ex)
        {
            AddGap(manifest, facts, file.RelativePath, RuleIds.LegacyDataTypedDataSet, ex.Classification, ex.Message, "TypedDataSet");
        }
        catch (IOException)
        {
            AddGap(manifest, facts, file.RelativePath, RuleIds.LegacyDataTypedDataSet, "MalformedLegacyDataMetadata", "typed DataSet metadata could not be read safely", "TypedDataSet");
        }
    }

    private static void ExtractTypedDataSet(
        ScanManifest manifest,
        FileInventoryItem file,
        LegacyDataXmlDocument xml,
        List<CodeFact> facts,
        List<GeneratedLinkDescriptor> descriptors)
    {
        var dataset = xml.Document.Descendants()
            .FirstOrDefault(element => Attr(element, "IsDataSet")?.Equals("true", StringComparison.OrdinalIgnoreCase) == true)
            ?? xml.Document.Root;
        var datasetName = Attr(dataset, "name") ?? Attr(dataset, "Name") ?? Path.GetFileNameWithoutExtension(file.RelativePath);
        AddEntityFact(manifest, facts, file.RelativePath, dataset!, "TypedDataSet", "dataset", datasetName, null, xml.DocumentHash);
        descriptors.Add(new GeneratedLinkDescriptor(file.RelativePath, "TypedDataSet", datasetName, datasetName, ExpectedDesignerBasename(file.RelativePath), "dataset"));

        var tableElements = xml.Document.Descendants()
            .Where(element => element.Name.LocalName == "element" && IsTypedDataTableElement(element))
            .OrderBy(GetLine)
            .ThenBy(element => Attr(element, "Generator_UserTableName") ?? Attr(element, "name"), StringComparer.Ordinal)
            .ToArray();
        if (tableElements.Length == 0)
        {
            AddGap(manifest, facts, file.RelativePath, RuleIds.LegacyDataTypedDataSet, "UnrelatedXsdSchemaGated", "XSD has typed DataSet indicators but no supported DataTable or TableAdapter content.", "TypedDataSet");
        }

        foreach (var table in tableElements)
        {
            var tableName = Attr(table, "Generator_UserTableName") ?? Attr(table, "name");
            var rowType = Attr(table, "Generator_RowClassName");
            AddStorageFact(manifest, facts, file.RelativePath, table, "TypedDataSet", "datatable", tableName, xml.DocumentHash);
            AddEntityFact(manifest, facts, file.RelativePath, table, "TypedDataSet", "datarow", rowType ?? tableName, null, xml.DocumentHash);
            AddMappingFact(manifest, facts, file.RelativePath, table, "TypedDataSet", "dataset-table", datasetName, tableName, xml.DocumentHash);
            if (!string.IsNullOrWhiteSpace(rowType))
            {
                descriptors.Add(new GeneratedLinkDescriptor(file.RelativePath, "TypedDataSet", rowType!, rowType, ExpectedDesignerBasename(file.RelativePath), "row"));
            }

            foreach (var column in table.Descendants().Where(element => element.Name.LocalName == "element").Where(element => !ReferenceEquals(element, table)).OrderBy(GetLine).ThenBy(element => Attr(element, "name"), StringComparer.Ordinal))
            {
                var columnName = Attr(column, "Generator_UserColumnName") ?? Attr(column, "name");
                AddColumnFact(manifest, facts, file.RelativePath, column, "TypedDataSet", "datacolumn", tableName, columnName, columnName, xml.DocumentHash);
            }
        }

        foreach (var relation in xml.Document.Descendants().Where(element => element.Attributes().Any(attribute => attribute.Name.LocalName == "Relationship")).OrderBy(GetLine))
        {
            AddMappingFact(manifest, facts, file.RelativePath, relation, "TypedDataSet", "relation", Attr(relation, "name") ?? Attr(relation, "Relationship"), Attr(relation, "Relationship"), xml.DocumentHash);
        }

        ExtractTableAdapterCommands(manifest, file, xml, facts, descriptors);
    }

    private static void ExtractTableAdapterCommands(
        ScanManifest manifest,
        FileInventoryItem file,
        LegacyDataXmlDocument xml,
        List<CodeFact> facts,
        List<GeneratedLinkDescriptor> descriptors)
    {
        foreach (var node in xml.Document.Descendants().Where(IsCommandElement).OrderBy(GetLine).ThenBy(element => Attr(element, "Name") ?? element.Name.LocalName, StringComparer.Ordinal))
        {
            var adapterName = Attr(node, "Generator_TableAdapterName") ?? Attr(node, "TableAdapterName") ?? Attr(node, "Name") ?? Attr(node, "name");
            var commandName = Attr(node, "Generator_SourceName") ?? Attr(node, "MethodName") ?? Attr(node, "Name") ?? Attr(node, "name");
            var commandText = CommandText(node);
            AddEntityFact(manifest, facts, file.RelativePath, node, "TypedDataSet", "tableadapter", adapterName, null, xml.DocumentHash);
            AddMappingFact(manifest, facts, file.RelativePath, node, "TypedDataSet", "adapter-command", adapterName, commandName, xml.DocumentHash);
            if (!string.IsNullOrWhiteSpace(adapterName))
            {
                descriptors.Add(new GeneratedLinkDescriptor(file.RelativePath, "TypedDataSet", adapterName!, adapterName, ExpectedDesignerBasename(file.RelativePath), "adapter"));
            }

            if (string.IsNullOrWhiteSpace(commandText))
            {
                AddGap(manifest, facts, file.RelativePath, RuleIds.LegacyDataTypedDataSet, "DynamicTableAdapterCommand", "TableAdapter command text is not complete static text.", "TypedDataSet", GetLine(node));
                continue;
            }

            AddSqlFacts(manifest, facts, file.RelativePath, node, commandText!, "typed-dataset-tableadapter");
        }
    }

    private static void ExtractConfig(string repoPath, ScanManifest manifest, FileInventoryItem file, List<CodeFact> facts)
    {
        var fullPath = Path.Combine(repoPath, file.RelativePath);
        try
        {
            var xml = LegacyDataXml.Load(fullPath);
            AddConfigTransformGapIfPresent(manifest, file, xml.Document, facts);
            foreach (var element in xml.Document.Descendants().OrderBy(GetLine))
            {
                if ((element.Name.LocalName.Equals("connectionStrings", StringComparison.OrdinalIgnoreCase)
                    || element.Parent?.Name.LocalName.Equals("connectionStrings", StringComparison.OrdinalIgnoreCase) == true)
                    && element.Attributes().Any(attribute => attribute.Name.LocalName is "configSource" or "file"))
                {
                    AddGap(manifest, facts, file.RelativePath, RuleIds.LegacyDataConfig, "ExternalConfigInclude", "connectionStrings uses external config include/source metadata that TraceMap does not load.", "Config", GetLine(element));
                }

                if (element.Name.LocalName.Equals("EncryptedData", StringComparison.OrdinalIgnoreCase)
                    || element.Attributes().Any(attribute => attribute.Name.LocalName.Equals("configProtectionProvider", StringComparison.OrdinalIgnoreCase)))
                {
                    AddGap(manifest, facts, file.RelativePath, RuleIds.LegacyDataConfig, "EncryptedConfigSection", "Config section is encrypted or opaque.", "Config", GetLine(element));
                }
            }

            foreach (var add in xml.Document.Descendants().Where(element => element.Name.LocalName is "add" or "provider").OrderBy(GetLine).ThenBy(element => Attr(element, "name") ?? Attr(element, "invariant") ?? Attr(element, "invariantName"), StringComparer.Ordinal))
            {
                var parentName = add.Parent?.Name.LocalName ?? string.Empty;
                if (parentName.Equals("connectionStrings", StringComparison.OrdinalIgnoreCase))
                {
                    AddProviderConfigFact(manifest, facts, file.RelativePath, add, "connection-string", Attr(add, "name"), Attr(add, "providerName"), Attr(add, "connectionString"));
                }
                else if (parentName.Equals("DbProviderFactories", StringComparison.OrdinalIgnoreCase))
                {
                    AddProviderConfigFact(manifest, facts, file.RelativePath, add, "provider-factory", Attr(add, "name") ?? Attr(add, "invariant"), Attr(add, "invariant"), null);
                }
                else if (add.Ancestors().Any(ancestor => ancestor.Name.LocalName.Equals("providers", StringComparison.OrdinalIgnoreCase)
                    && (ancestor.Parent?.Name.LocalName.Equals("entityFramework", StringComparison.OrdinalIgnoreCase) ?? false)))
                {
                    AddProviderConfigFact(manifest, facts, file.RelativePath, add, "entity-framework-provider", Attr(add, "invariantName") ?? Attr(add, "name"), Attr(add, "type"), null);
                }
            }
        }
        catch (LegacyDataXmlException ex)
        {
            AddGap(manifest, facts, file.RelativePath, RuleIds.LegacyDataConfig, ex.Classification, ex.Message, "Config");
        }
        catch (IOException)
        {
            AddGap(manifest, facts, file.RelativePath, RuleIds.LegacyDataConfig, "MalformedLegacyDataMetadata", "legacy data config metadata could not be read safely", "Config");
        }
    }

    private static void AddGeneratedCodeLinks(
        string repoPath,
        ScanManifest manifest,
        IReadOnlyList<FileInventoryItem> inventory,
        IReadOnlyList<CodeFact> existingFacts,
        IReadOnlyList<GeneratedLinkDescriptor> descriptors,
        List<CodeFact> facts)
    {
        var generatedFiles = inventory
            .Where(item => FileInventory.IsCSharpKind(item.Kind) && Path.GetFileName(item.RelativePath).EndsWith(".designer.cs", StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => item.RelativePath, StringComparer.Ordinal)
            .ToArray();
        var semanticTypeFacts = existingFacts
            .Where(fact => fact.FactType == FactTypes.TypeDeclared && fact.EvidenceTier == EvidenceTiers.Tier1Semantic)
            .ToArray();

        foreach (var descriptor in descriptors
            .Where(descriptor => !string.IsNullOrWhiteSpace(descriptor.TypeName))
            .GroupBy(descriptor => $"{descriptor.MetadataPath}|{descriptor.TypeName}|{descriptor.Role}", StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(descriptor => descriptor.MetadataPath, StringComparer.Ordinal)
            .ThenBy(descriptor => descriptor.TypeName, StringComparer.Ordinal))
        {
            var descriptorTypeName = descriptor.TypeName!;
            var scopedFiles = generatedFiles
                .Where(file => descriptor.ExpectedGeneratedFileName is null
                    || Path.GetFileName(file.RelativePath).Equals(descriptor.ExpectedGeneratedFileName, StringComparison.OrdinalIgnoreCase)
                    || Path.GetDirectoryName(file.RelativePath)?.Equals(Path.GetDirectoryName(descriptor.MetadataPath), StringComparison.OrdinalIgnoreCase) == true)
                .ToArray();
            var semanticMatches = semanticTypeFacts
                .Where(fact => TypeNameMatches(fact.TargetSymbol ?? fact.ContractElement, descriptorTypeName)
                    && scopedFiles.Any(file => file.RelativePath.Equals(fact.Evidence.FilePath, StringComparison.Ordinal)))
                .ToArray();
            if (semanticMatches.Length == 1)
            {
                AddGeneratedLinkFact(manifest, facts, semanticMatches[0].Evidence.FilePath, semanticMatches[0].Evidence.StartLine, descriptor, EvidenceTiers.Tier1Semantic, "semantic-symbol", semanticMatches[0].FactId);
                continue;
            }

            var syntaxMatches = scopedFiles
                .Where(file => FileContainsType(repoPath, file.RelativePath, descriptorTypeName))
                .ToArray();
            if (syntaxMatches.Length == 1)
            {
                AddGeneratedLinkFact(manifest, facts, syntaxMatches[0].RelativePath, 1, descriptor, EvidenceTiers.Tier2Structural, "generated-file", null);
            }
            else if (syntaxMatches.Length > 1 || semanticMatches.Length > 1)
            {
                AddGap(manifest, facts, descriptor.MetadataPath, RuleIds.LegacyDataGeneratedLink, "AmbiguousGeneratedCodeLink", $"Multiple generated-code candidates matched {descriptorTypeName}.", descriptor.MetadataKind);
            }
            else if (descriptor.ExpectedGeneratedFileName is not null)
            {
                AddGap(manifest, facts, descriptor.MetadataPath, RuleIds.LegacyDataGeneratedLink, "MissingGeneratedCode", $"Expected generated code file {descriptor.ExpectedGeneratedFileName} was not found for metadata descriptor.", descriptor.MetadataKind);
            }
        }
    }

    private static void AddInventoryFact(ScanManifest manifest, List<CodeFact> facts, string relativePath, string metadataKind, string ruleId, int line, string? metadataHash, string? extraKind)
    {
        var properties = BaseProperties(metadataKind, metadataHash);
        properties["inventoryKind"] = extraKind ?? metadataKind;
        properties["path"] = relativePath;
        facts.Add(FactFactory.Create(
            manifest,
            FactTypes.LegacyDataMetadataDeclared,
            ruleId,
            EvidenceTiers.Tier2Structural,
            Evidence(relativePath, line),
            targetSymbol: $"{metadataKind}:{Path.GetFileName(relativePath)}",
            properties: properties));
    }

    private static void AddEntityFact(ScanManifest manifest, List<CodeFact> facts, string relativePath, XObject evidenceNode, string metadataKind, string entityKind, string? entityName, string? namespaceName, string metadataHash)
    {
        var properties = BaseProperties(metadataKind, metadataHash);
        properties["entityKind"] = entityKind;
        LegacyDataSafeValues.AddSafeOrHash(properties, "entityName", "entityNameHash", entityName);
        LegacyDataSafeValues.AddSafeOrHash(properties, "typeName", "typeNameHash", entityName);
        if (!string.IsNullOrWhiteSpace(namespaceName))
        {
            properties["namespaceHash"] = FactFactory.Hash(namespaceName.Trim(), 32);
        }

        facts.Add(FactFactory.Create(
            manifest,
            FactTypes.LegacyDataEntityDeclared,
            RuleForMetadata(metadataKind),
            EvidenceTiers.Tier2Structural,
            Evidence(relativePath, GetLine(evidenceNode)),
            targetSymbol: LegacyDataSafeValues.Identity(entityKind, entityName),
            contractElement: LegacyDataSafeValues.SafeIdentifier(entityName),
            properties: properties));
    }

    private static void AddStorageFact(ScanManifest manifest, List<CodeFact> facts, string relativePath, XObject evidenceNode, string metadataKind, string storageKind, string? storageName, string metadataHash)
    {
        var properties = BaseProperties(metadataKind, metadataHash);
        properties["storageObjectKind"] = storageKind;
        LegacyDataSafeValues.AddSafeOrHash(properties, "storageObjectName", "storageObjectHash", storageName);
        if (storageKind.Contains("table", StringComparison.OrdinalIgnoreCase) || storageKind.Contains("entity-set", StringComparison.OrdinalIgnoreCase))
        {
            LegacyDataSafeValues.AddSafeOrHash(properties, "tableName", "tableNameHash", storageName);
        }

        facts.Add(FactFactory.Create(
            manifest,
            FactTypes.LegacyDataStorageObjectDeclared,
            RuleForMetadata(metadataKind),
            EvidenceTiers.Tier2Structural,
            Evidence(relativePath, GetLine(evidenceNode)),
            targetSymbol: LegacyDataSafeValues.Identity(storageKind, storageName),
            contractElement: LegacyDataSafeValues.SafeIdentifier(storageName),
            properties: properties));
    }

    private static void AddColumnFact(ScanManifest manifest, List<CodeFact> facts, string relativePath, XObject evidenceNode, string metadataKind, string columnKind, string? ownerName, string? propertyName, string? columnName, string metadataHash)
    {
        var properties = BaseProperties(metadataKind, metadataHash);
        properties["columnKind"] = columnKind;
        LegacyDataSafeValues.AddSafeOrHash(properties, "ownerName", "ownerNameHash", ownerName);
        LegacyDataSafeValues.AddSafeOrHash(properties, "propertyName", "propertyNameHash", propertyName);
        LegacyDataSafeValues.AddSafeOrHash(properties, "fieldName", "fieldNameHash", propertyName);
        LegacyDataSafeValues.AddSafeOrHash(properties, "columnName", "columnHash", columnName ?? propertyName);

        facts.Add(FactFactory.Create(
            manifest,
            FactTypes.LegacyDataColumnDeclared,
            RuleForMetadata(metadataKind),
            EvidenceTiers.Tier2Structural,
            Evidence(relativePath, GetLine(evidenceNode)),
            targetSymbol: LegacyDataSafeValues.Identity(columnKind, $"{ownerName}.{propertyName ?? columnName}"),
            contractElement: LegacyDataSafeValues.SafeIdentifier(propertyName ?? columnName),
            properties: properties));
    }

    private static void AddMappingFact(ScanManifest manifest, List<CodeFact> facts, string relativePath, XObject evidenceNode, string metadataKind, string mappingKind, string? sourceName, string? targetName, string metadataHash, string? entityName = null, string? tableName = null)
    {
        var properties = BaseProperties(metadataKind, metadataHash);
        properties["mappingKind"] = mappingKind;
        LegacyDataSafeValues.AddSafeOrHash(properties, "sourceName", "sourceNameHash", sourceName);
        LegacyDataSafeValues.AddSafeOrHash(properties, "targetName", "targetNameHash", targetName);
        LegacyDataSafeValues.AddSafeOrHash(properties, "entityName", "entityNameHash", entityName ?? (mappingKind == "entity-table" ? sourceName : null));
        LegacyDataSafeValues.AddSafeOrHash(properties, "tableName", "tableNameHash", tableName ?? (mappingKind == "entity-table" ? targetName : null));
        if (mappingKind == "property-column")
        {
            LegacyDataSafeValues.AddSafeOrHash(properties, "propertyName", "propertyNameHash", sourceName);
            LegacyDataSafeValues.AddSafeOrHash(properties, "columnName", "columnHash", targetName);
        }

        facts.Add(FactFactory.Create(
            manifest,
            FactTypes.LegacyDataMappingDeclared,
            RuleForMetadata(metadataKind),
            EvidenceTiers.Tier2Structural,
            Evidence(relativePath, GetLine(evidenceNode)),
            sourceSymbol: LegacyDataSafeValues.SafeIdentifier(sourceName),
            targetSymbol: LegacyDataSafeValues.Identity(mappingKind, targetName ?? sourceName),
            contractElement: LegacyDataSafeValues.SafeIdentifier(targetName ?? sourceName),
            properties: properties));
    }

    private static void AddProviderConfigFact(ScanManifest manifest, List<CodeFact> facts, string relativePath, XElement element, string configKind, string? name, string? providerName, string? rawValue)
    {
        var properties = BaseProperties("Config", null);
        properties["configKind"] = configKind;
        properties["hasRawValue"] = string.IsNullOrWhiteSpace(rawValue) ? "false" : "true";
        LegacyDataSafeValues.AddSafeOrHash(properties, "connectionName", "connectionNameHash", name);
        LegacyDataSafeValues.AddSafeOrHash(properties, "providerName", "providerNameHash", providerName);
        if (!string.IsNullOrWhiteSpace(rawValue))
        {
            properties["valueHash"] = FactFactory.Hash(rawValue.Trim(), 32);
        }

        facts.Add(FactFactory.Create(
            manifest,
            FactTypes.LegacyDataProviderConfigDeclared,
            RuleIds.LegacyDataConfig,
            EvidenceTiers.Tier2Structural,
            Evidence(relativePath, GetLine(element)),
            targetSymbol: LegacyDataSafeValues.Identity(configKind, name ?? providerName),
            contractElement: LegacyDataSafeValues.SafeIdentifier(name),
            properties: properties));
    }

    private static void AddSqlFacts(ScanManifest manifest, List<CodeFact> facts, string relativePath, XElement element, string commandText, string sourceKind)
    {
        var operationName = SqlShapeExtractor.OperationName(commandText);
        var textProperties = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["metadataKind"] = "TypedDataSet",
            ["sqlSourceKind"] = sourceKind,
            ["textHash"] = FactFactory.Hash(commandText, 32),
            ["textLength"] = commandText.Length.ToString(),
            ["coverage"] = StaticCoverage,
            ["limitation"] = "Static TableAdapter command-text hash only; does not prove command execution or stored-procedure existence."
        };
        if (!string.IsNullOrWhiteSpace(operationName))
        {
            textProperties["operationName"] = operationName;
        }

        facts.Add(FactFactory.Create(
            manifest,
            FactTypes.SqlTextUsed,
            RuleIds.LegacyDataTypedDataSet,
            EvidenceTiers.Tier2Structural,
            Evidence(relativePath, GetLine(element)),
            properties: textProperties));

        var shapeProperties = SqlShapeExtractor.QueryShapeProperties(commandText, sourceKind);
        if (!shapeProperties.ContainsKey("queryShapeHash"))
        {
            return;
        }

        shapeProperties["metadataKind"] = "TypedDataSet";
        shapeProperties["coverage"] = StaticCoverage;
        shapeProperties["limitation"] = "Static TableAdapter command shape only; does not prove SQL execution or database schema existence.";
        var target = shapeProperties.GetValueOrDefault("tableName") ?? shapeProperties.GetValueOrDefault("operationName") ?? "sql-shape";
        facts.Add(FactFactory.Create(
            manifest,
            FactTypes.QueryPatternDetected,
            RuleIds.LegacyDataTypedDataSet,
            EvidenceTiers.Tier2Structural,
            Evidence(relativePath, GetLine(element)),
            targetSymbol: target,
            contractElement: target,
            properties: shapeProperties));
    }

    private static void AddGeneratedLinkFact(ScanManifest manifest, List<CodeFact> facts, string filePath, int line, GeneratedLinkDescriptor descriptor, string tier, string linkKind, string? supportingFactId)
    {
        var properties = BaseProperties(descriptor.MetadataKind, null);
        properties["linkKind"] = linkKind;
        properties["symbolRole"] = descriptor.Role;
        properties["metadataPath"] = descriptor.MetadataPath;
        LegacyDataSafeValues.AddSafeOrHash(properties, "typeName", "typeNameHash", descriptor.TypeName);
        LegacyDataSafeValues.AddSafeOrHash(properties, "generatedCodeFileName", "generatedCodeFileNameHash", Path.GetFileName(filePath));
        if (!string.IsNullOrWhiteSpace(supportingFactId))
        {
            properties["supportingFactIds"] = supportingFactId;
        }

        facts.Add(FactFactory.Create(
            manifest,
            FactTypes.LegacyDataGeneratedCodeLinked,
            RuleIds.LegacyDataGeneratedLink,
            tier,
            Evidence(filePath, line),
            targetSymbol: LegacyDataSafeValues.Identity(descriptor.Role, descriptor.TypeName),
            contractElement: LegacyDataSafeValues.SafeIdentifier(descriptor.TypeName),
            properties: properties));
    }

    private static void AddGap(ScanManifest manifest, List<CodeFact> facts, string relativePath, string ruleId, string classification, string message, string metadataKind, int line = 1)
    {
        var properties = BaseProperties(metadataKind, null);
        properties["classification"] = classification;
        properties["gapKind"] = classification;
        properties["message"] = SafeMessage(message);
        facts.Add(FactFactory.Create(
            manifest,
            FactTypes.AnalysisGap,
            ruleId,
            EvidenceTiers.Tier4Unknown,
            Evidence(relativePath, line),
            targetSymbol: classification,
            properties: properties));
    }

    private static SortedDictionary<string, string> BaseProperties(string metadataKind, string? metadataHash)
    {
        var properties = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["metadataKind"] = metadataKind,
            ["coverage"] = StaticCoverage,
            ["limitation"] = StaticLimitation
        };
        if (!string.IsNullOrWhiteSpace(metadataHash))
        {
            properties["metadataHash"] = metadataHash;
        }

        return properties;
    }

    private static EvidenceSpan Evidence(string relativePath, int line)
    {
        return new EvidenceSpan(relativePath, Math.Max(1, line), Math.Max(1, line), null, ExtractorId, ScannerVersions.LegacyDataExtractor);
    }

    private static string RuleForMetadata(string metadataKind)
    {
        return metadataKind switch
        {
            "Dbml" => RuleIds.LegacyDataDbml,
            "Edmx" => RuleIds.LegacyDataEdmx,
            "TypedDataSet" => RuleIds.LegacyDataTypedDataSet,
            "Config" => RuleIds.LegacyDataConfig,
            _ => RuleIds.LegacyDataMetadataInventory
        };
    }

    private static bool IsXsd(FileInventoryItem file)
    {
        return file.RelativePath.EndsWith(".xsd", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasTypedDataSetIndicator(XDocument document)
    {
        if (document.Root is null)
        {
            return false;
        }

        const string msdataNamespace = "urn:schemas-microsoft-com:xml-msdata";
        const string mspropNamespace = "urn:schemas-microsoft-com:xml-msprop";
        return new[] { document.Root }
            .DescendantsAndSelf()
            .Any(element =>
                element.Name.NamespaceName.Equals(msdataNamespace, StringComparison.Ordinal)
                || element.Name.NamespaceName.Equals(mspropNamespace, StringComparison.Ordinal)
                || element.Attributes().Any(attribute =>
                    attribute.IsNamespaceDeclaration
                        ? attribute.Value.Equals(msdataNamespace, StringComparison.Ordinal)
                            || attribute.Value.Equals(mspropNamespace, StringComparison.Ordinal)
                        : attribute.Name.NamespaceName.Equals(msdataNamespace, StringComparison.Ordinal)
                            || attribute.Name.NamespaceName.Equals(mspropNamespace, StringComparison.Ordinal)
                            || (attribute.Name.LocalName.StartsWith("Generator_", StringComparison.Ordinal)
                                && attribute.Name.NamespaceName.Equals(mspropNamespace, StringComparison.Ordinal))));
    }

    private static bool IsTypedDataTableElement(XElement element)
    {
        return !string.IsNullOrWhiteSpace(Attr(element, "Generator_UserTableName"))
            || !string.IsNullOrWhiteSpace(Attr(element, "Generator_RowClassName"))
            || (element.Parent?.Name.LocalName == "sequence"
                && element.Descendants().Any(descendant => descendant.Name.LocalName == "element"));
    }

    private static bool IsCommandElement(XElement element)
    {
        if (element.Name.LocalName.Contains("Command", StringComparison.OrdinalIgnoreCase)
            || element.Name.LocalName.Contains("DbSource", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return element.Attributes().Any(attribute =>
            attribute.Name.LocalName.Contains("Command", StringComparison.OrdinalIgnoreCase)
            || attribute.Name.LocalName.Contains("DbSource", StringComparison.OrdinalIgnoreCase)
            || attribute.Name.LocalName.Equals("Generator_TableAdapterName", StringComparison.Ordinal));
    }

    private static string? CommandText(XElement element)
    {
        var attribute = element.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName.Contains("CommandText", StringComparison.OrdinalIgnoreCase)
            || attribute.Name.LocalName.Equals("CommandText", StringComparison.OrdinalIgnoreCase)
            || attribute.Name.LocalName.Equals("Sql", StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(attribute?.Value))
        {
            return attribute.Value.Trim();
        }

        var child = element.Descendants().FirstOrDefault(descendant => descendant.Name.LocalName.Contains("CommandText", StringComparison.OrdinalIgnoreCase)
            || descendant.Name.LocalName.Equals("Sql", StringComparison.OrdinalIgnoreCase));
        var value = child?.Value.Trim();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static bool IsGeneratedDesignerCandidate(string repoPath, FileInventoryItem file)
    {
        if (!FileInventory.IsCSharpKind(file.Kind)
            || !Path.GetFileName(file.RelativePath).EndsWith(".designer.cs", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            var text = File.ReadAllText(Path.Combine(repoPath, file.RelativePath));
            return text.Contains("System.Data.DataSet", StringComparison.Ordinal)
                || text.Contains("TableAdapter", StringComparison.Ordinal)
                || text.Contains("System.Data.Linq", StringComparison.Ordinal)
                || text.Contains("DesignerCategoryAttribute(\"code\")", StringComparison.Ordinal)
                || text.Contains("GeneratedCodeAttribute", StringComparison.Ordinal);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool FileContainsType(string repoPath, string relativePath, string typeName)
    {
        try
        {
            var text = File.ReadAllText(Path.Combine(repoPath, relativePath));
            var escaped = Regex.Escape(typeName);
            return Regex.IsMatch(text, $@"\b(?:class|partial\s+class|struct|interface)\s+{escaped}\b", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or RegexMatchTimeoutException)
        {
            return false;
        }
    }

    private static bool TypeNameMatches(string? candidate, string typeName)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        return candidate.Equals(typeName, StringComparison.Ordinal)
            || candidate.EndsWith("." + typeName, StringComparison.Ordinal);
    }

    private static string ExpectedDesignerBasename(string relativePath)
    {
        return $"{Path.GetFileNameWithoutExtension(relativePath)}.designer.cs";
    }

    private static void AddConfigTransformGapIfPresent(ScanManifest manifest, FileInventoryItem file, XDocument document, List<CodeFact> facts)
    {
        var fileName = Path.GetFileName(file.RelativePath);
        if (fileName.Contains(".Debug.config", StringComparison.OrdinalIgnoreCase)
            || fileName.Contains(".Release.config", StringComparison.OrdinalIgnoreCase)
            || document.Descendants().Any(element => element.Attributes().Any(attribute => attribute.Name.LocalName is "Transform" or "Locator"
                || attribute.Name.NamespaceName.Contains("XML-Document-Transform", StringComparison.OrdinalIgnoreCase))))
        {
            AddGap(manifest, facts, file.RelativePath, RuleIds.LegacyDataConfig, "ConfigTransformPresent", "Config transform metadata is present and was not executed.", "Config");
        }
    }

    private static bool IsSchema(XElement element)
    {
        return element.Name.LocalName == "Schema";
    }

    private static string? Attr(XElement? element, string name)
    {
        return element?.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase))?.Value.Trim();
    }

    private static int GetLine(XObject? node)
    {
        return node is IXmlLineInfo lineInfo && lineInfo.HasLineInfo()
            ? Math.Max(1, lineInfo.LineNumber)
            : 1;
    }

    private static string SafeMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "Legacy data metadata analysis gap.";
        }

        var safe = message.Replace('\r', ' ').Replace('\n', ' ').Trim();
        if (safe.Length > 180)
        {
            safe = safe[..180];
        }

        return safe;
    }

    private sealed record GeneratedLinkDescriptor(
        string MetadataPath,
        string MetadataKind,
        string? TypeName,
        string? Identity,
        string? ExpectedGeneratedFileName,
        string Role);
}
