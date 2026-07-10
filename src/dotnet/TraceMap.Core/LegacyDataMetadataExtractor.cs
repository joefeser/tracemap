using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TraceMap.Core;

public static class LegacyDataMetadataExtractor
{
    private const string ExtractorId = "LegacyDataExtractor";
    private const long MaxGeneratedDesignerBytes = SafeXml.MaxXmlBytes;
    private const int MaxNHibernatePropertiesPerClass = 500;
    private const int MaxNHibernateRelationshipsPerClass = 200;
    private static readonly XNamespace Xs = "http://www.w3.org/2001/XMLSchema";
    private static readonly XNamespace MsData = "urn:schemas-microsoft-com:xml-msdata";
    private static readonly XNamespace MsProp = "urn:schemas-microsoft-com:xml-msprop";

    public static IReadOnlyList<CodeFact> Extract(
        string repoPath,
        ScanManifest manifest,
        IReadOnlyList<FileInventoryItem> inventory,
        IReadOnlyList<CodeFact> existingFacts)
    {
        var facts = new List<CodeFact>();
        var generatedCandidates = LoadGeneratedCandidates(repoPath, manifest, inventory, facts);

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
                case "XsdSchema":
                    ExtractTypedDataSet(repoPath, manifest, item, facts);
                    break;
                case "Config":
                    ExtractDataConfig(repoPath, manifest, item, facts);
                    DetectUnsupportedOrmConfig(repoPath, manifest, item, facts);
                    break;
                case "CSharp" when IsDesigner(item.RelativePath):
                    AddGeneratedDesignerInventory(manifest, item, facts);
                    break;
                case "CSharp":
                case "WebFormsCodeBehind":
                case "WinFormsDesigner":
                    DetectUnsupportedOrmCSharp(repoPath, manifest, item, facts);
                    break;
                case "LegacyOrmMetadata":
                    if (IsNHibernateHbmPath(item.RelativePath))
                    {
                        ExtractNHibernateMapping(repoPath, manifest, item, facts);
                    }
                    else
                    {
                        DetectUnsupportedOrmMetadata(repoPath, manifest, item, facts);
                    }
                    break;
            }
        }

        AddGeneratedCodeLinks(manifest, facts, generatedCandidates);
        var mappedTypeNames = MappedTypeSyntaxLinkCandidateNames(facts);
        if (mappedTypeNames.Count > 0)
        {
            var csharpTypeDeclarations = LoadCSharpTypeDeclarations(repoPath, manifest, inventory, facts, mappedTypeNames);
            AddMappedTypeSyntaxLinks(manifest, facts, csharpTypeDeclarations);
        }

        return facts
            .OrderBy(fact => fact.Evidence.FilePath, StringComparer.Ordinal)
            .ThenBy(fact => fact.Evidence.StartLine)
            .ThenBy(fact => fact.RuleId, StringComparer.Ordinal)
            .ThenBy(fact => fact.FactType, StringComparer.Ordinal)
            .ThenBy(fact => fact.TargetSymbol, StringComparer.Ordinal)
            .ThenBy(fact => fact.FactId, StringComparer.Ordinal)
            .ToArray();
    }

    private static void DetectUnsupportedOrmMetadata(string repoPath, ScanManifest manifest, FileInventoryItem item, List<CodeFact> facts)
    {
        var family = UnsupportedOrmFamilyFromPath(item.RelativePath);
        var fullPath = Path.Combine(repoPath, item.RelativePath);
        try
        {
            var document = SafeXml.LoadDocument(fullPath);
            family ??= UnsupportedOrmFamilyFromDocument(document);
            if (family is null)
            {
                return;
            }

            AddUnsupportedOrmGap(manifest, facts, item.RelativePath, family, "metadata-file", "UnsupportedLegacyOrmDescriptor", "Recognized unsupported legacy ORM metadata descriptor; no entity, table, column, or relationship facts were inferred.", document.Root);
        }
        catch (SafeXmlException ex)
        {
            AddUnsupportedOrmGap(manifest, facts, item.RelativePath, family ?? "UnknownLegacyOrm", "metadata-file", Classification(ex), "Unsupported legacy ORM descriptor could not be parsed safely.", null);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AddUnsupportedOrmGap(manifest, facts, item.RelativePath, family ?? "UnknownLegacyOrm", "metadata-file", "MalformedLegacyDataMetadata", $"Unsupported legacy ORM descriptor could not be read: {ex.GetType().Name}.", null);
        }
    }

    private static void DetectUnsupportedOrmConfig(string repoPath, ScanManifest manifest, FileInventoryItem item, List<CodeFact> facts)
    {
        var fullPath = Path.Combine(repoPath, item.RelativePath);
        XDocument document;
        try
        {
            document = SafeXml.LoadDocument(fullPath);
        }
        catch (Exception ex) when (ex is SafeXmlException or IOException or UnauthorizedAccessException)
        {
            return;
        }

        foreach (var family in UnsupportedOrmFamiliesFromConfig(document).Order(StringComparer.Ordinal))
        {
            AddUnsupportedOrmGap(manifest, facts, item.RelativePath, family, "config", "UnsupportedLegacyOrmDescriptor", "Recognized unsupported legacy ORM config descriptor; no entity, table, column, or relationship facts were inferred.", document.Root);
        }
    }

    private static void DetectUnsupportedOrmCSharp(string repoPath, ScanManifest manifest, FileInventoryItem item, List<CodeFact> facts)
    {
        var fullPath = Path.Combine(repoPath, item.RelativePath);
        string source;
        try
        {
            source = File.ReadAllText(fullPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return;
        }

        var root = CSharpSyntaxTree.ParseText(source).GetRoot();
        var families = new SortedDictionary<string, int>(StringComparer.Ordinal);
        AddCSharpFamilyIfPresent(root, "Castle ActiveRecord", new[] { "ActiveRecord" }, families);
        AddCSharpFamilyIfPresent(root, "SubSonic", new[] { "SubSonic" }, families);
        AddCSharpFamilyIfPresent(root, "LLBLGen", new[] { "LLBLGen" }, families);
        AddCSharpFamilyIfPresent(root, "iBATIS.NET", new[] { "IBatisNet", "IBatis" }, families);
        AddCSharpFamilyIfPresent(root, "MyBatis.NET", new[] { "MyBatis" }, families);

        foreach (var (family, line) in families)
        {
            AddUnsupportedOrmGap(manifest, facts, item.RelativePath, family, "csharp", "UnsupportedLegacyOrmDescriptor", "Recognized unsupported legacy ORM code descriptor; no entity, table, column, or relationship facts were inferred.", null, line);
        }
    }

    private static void ExtractNHibernateMapping(string repoPath, ScanManifest manifest, FileInventoryItem item, List<CodeFact> facts)
    {
        var fullPath = Path.Combine(repoPath, item.RelativePath);
        XDocument document;
        string metadataHash;
        try
        {
            metadataHash = HashMetadataDocument(fullPath);
            document = SafeXml.LoadDocument(fullPath);
        }
        catch (SafeXmlException ex)
        {
            AddGap(manifest, facts, item.RelativePath, RuleIds.LegacyDataOrmNHibernate, Classification(ex), "NHibernate mapping XML could not be parsed safely.", null);
            return;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            AddGap(manifest, facts, item.RelativePath, RuleIds.LegacyDataOrmNHibernate, "MalformedLegacyDataMetadata", $"NHibernate mapping XML could not be read: {ex.GetType().Name}.", null);
            return;
        }

        if (!LooksLikeNHibernateMapping(document))
        {
            AddUnsupportedOrmGap(manifest, facts, item.RelativePath, "UnknownLegacyOrm", "hbm-xml", "UnsupportedLegacyOrmDescriptor", "XML file used an .hbm.xml name but did not contain a supported NHibernate mapping root.", document.Root);
            return;
        }

        var metadataFact = AddMetadataInventoryFact(manifest, facts, item.RelativePath, "NHibernateHbm", metadataHash, RuleIds.LegacyDataOrmNHibernate, document.Root);
        AddNHibernateUnsupportedShapeGaps(manifest, facts, item.RelativePath, document);

        foreach (var classElement in document.Descendants().Where(element => element.Name.LocalName == "class").OrderBy(GetLine).ThenBy(element => AttributeValue(element, "name"), StringComparer.Ordinal))
        {
            AddNHibernateClassFacts(manifest, facts, item.RelativePath, metadataHash, metadataFact.FactId, classElement);
        }
    }

    private static void AddNHibernateClassFacts(
        ScanManifest manifest,
        List<CodeFact> facts,
        string relativePath,
        string metadataHash,
        string sourceMetadataFactId,
        XElement classElement)
    {
        var className = AttributeValue(classElement, "name") ?? AttributeValue(classElement, "entity-name") ?? "mapped-class";
        var mappedTypeName = NHibernateMappedTypeName(classElement);
        var tableName = AttributeValue(classElement, "table");
        var schemaName = AttributeValue(classElement, "schema");
        var catalogName = AttributeValue(classElement, "catalog");

        var entityProps = MetadataProperties("NHibernateHbm", metadataHash, "orm-class");
        AddSafeName(entityProps, "entityName", "entityHash", className);
        AddSafeName(entityProps, "typeName", "typeHash", className);
        AddSafeName(entityProps, "mappedTypeName", "mappedTypeHash", mappedTypeName);
        AddSafeName(entityProps, "tableName", "tableHash", tableName);
        AddHashOnly(entityProps, "schemaHash", schemaName);
        AddHashOnly(entityProps, "catalogHash", catalogName);
        AddModelIdentity(entityProps, "NHibernateHbm", "entity", "orm-mapped", relativePath, "nhibernate-class", className, tableName, sourceMetadataFactId, Parts(("class", className), ("table", tableName)));
        facts.Add(CreateLegacyFact(manifest, FactTypes.LegacyDataEntityDeclared, RuleIds.LegacyDataOrmNHibernate, relativePath, classElement, TargetFrom(entityProps, "typeName", "typeHash"), entityProps));

        if (!string.IsNullOrWhiteSpace(tableName))
        {
            var storageProps = MetadataProperties("NHibernateHbm", metadataHash, "orm-table");
            storageProps["storageObjectKind"] = "Table";
            AddSafeName(storageProps, "storageObjectName", "storageObjectHash", tableName);
            AddSafeName(storageProps, "entityName", "entityHash", className);
            AddHashOnly(storageProps, "schemaHash", schemaName);
            AddHashOnly(storageProps, "catalogHash", catalogName);
            AddModelIdentity(storageProps, "NHibernateHbm", "storage-object", "storage", relativePath, "nhibernate-table", tableName, className, sourceMetadataFactId, Parts(("class", className), ("table", tableName)));
            facts.Add(CreateLegacyFact(manifest, FactTypes.LegacyDataStorageObjectDeclared, RuleIds.LegacyDataOrmNHibernate, relativePath, classElement, TargetFrom(storageProps, "storageObjectName", "storageObjectHash"), storageProps));

            var mappingProps = MetadataProperties("NHibernateHbm", metadataHash, "orm-class-table");
            mappingProps["mappingKind"] = "orm-class";
            AddSafeName(mappingProps, "entityName", "entityHash", className);
            AddSafeName(mappingProps, "tableName", "tableHash", tableName);
            AddModelIdentity(mappingProps, "NHibernateHbm", "entity", "mapping", relativePath, "nhibernate-class-table", className, tableName, sourceMetadataFactId, Parts(("class", className), ("table", tableName)));
            facts.Add(CreateLegacyFact(manifest, FactTypes.LegacyDataMappingDeclared, RuleIds.LegacyDataOrmNHibernate, relativePath, classElement, TargetFrom(mappingProps, "entityName", "entityHash"), mappingProps));
        }

        var propertyLikeElements = classElement.Elements()
            .Where(IsNHibernateColumnLikeElement)
            .OrderBy(GetLine)
            .ThenBy(element => AttributeValue(element, "name"), StringComparer.Ordinal)
            .ToArray();
        foreach (var property in propertyLikeElements.Take(MaxNHibernatePropertiesPerClass))
        {
            AddNHibernateColumnFacts(manifest, facts, relativePath, metadataHash, sourceMetadataFactId, className, tableName, property);
        }

        if (propertyLikeElements.Length > MaxNHibernatePropertiesPerClass)
        {
            AddGap(manifest, facts, relativePath, RuleIds.LegacyDataOrmNHibernate, "LegacyDataMetadataTooLarge", "NHibernate class exceeded the per-class property descriptor cap; skipped descriptors were not inferred.", propertyLikeElements[MaxNHibernatePropertiesPerClass]);
        }

        var relationshipElements = classElement.Elements()
            .Where(IsNHibernateRelationshipElement)
            .OrderBy(GetLine)
            .ThenBy(element => AttributeValue(element, "name"), StringComparer.Ordinal)
            .ToArray();
        foreach (var relationship in relationshipElements.Take(MaxNHibernateRelationshipsPerClass))
        {
            AddNHibernateRelationshipFact(manifest, facts, relativePath, metadataHash, sourceMetadataFactId, className, relationship);
        }

        if (relationshipElements.Length > MaxNHibernateRelationshipsPerClass)
        {
            AddGap(manifest, facts, relativePath, RuleIds.LegacyDataOrmNHibernate, "LegacyDataMetadataTooLarge", "NHibernate class exceeded the per-class relationship descriptor cap; skipped descriptors were not inferred.", relationshipElements[MaxNHibernateRelationshipsPerClass]);
        }
    }

    private static void AddNHibernateColumnFacts(
        ScanManifest manifest,
        List<CodeFact> facts,
        string relativePath,
        string metadataHash,
        string sourceMetadataFactId,
        string className,
        string? tableName,
        XElement property)
    {
        var propertyName = AttributeValue(property, "name") ?? property.Name.LocalName;
        if (IsNHibernateFormulaOnlyProperty(property))
        {
            AddGap(manifest, facts, relativePath, RuleIds.LegacyDataOrmNHibernate, "UnsupportedLegacyOrmMappingShape", "NHibernate formula-only property did not provide static column evidence; no column descriptor was inferred.", property);
            return;
        }

        var columnName = AttributeValue(property, "column")
            ?? AttributeValue(property.Elements().FirstOrDefault(element => element.Name.LocalName == "column"), "name")
            ?? propertyName;
        var descriptorKind = $"orm-{NormalizeToken(property.Name.LocalName, "property")}";
        var columnProps = MetadataProperties("NHibernateHbm", metadataHash, descriptorKind);
        AddSafeName(columnProps, "entityName", "entityHash", className);
        AddSafeName(columnProps, "propertyName", "propertyHash", propertyName);
        AddSafeName(columnProps, "columnName", "columnHash", columnName);
        AddSafeName(columnProps, "tableName", "tableHash", tableName);
        AddOptional(columnProps, "isNullable", NHibernateNullable(property));
        AddOptional(columnProps, "descriptorSource", property.Name.LocalName);
        AddModelIdentity(columnProps, "NHibernateHbm", "column", "orm-mapped", relativePath, "nhibernate-property-column", propertyName, tableName ?? className, sourceMetadataFactId, Parts(("class", className), ("property", propertyName), ("column", columnName), ("descriptor", property.Name.LocalName)));
        facts.Add(CreateLegacyFact(manifest, FactTypes.LegacyDataColumnDeclared, RuleIds.LegacyDataOrmNHibernate, relativePath, property, TargetFrom(columnProps, "propertyName", "propertyHash"), columnProps));

        var mappingProps = MetadataProperties("NHibernateHbm", metadataHash, "orm-property-column");
        mappingProps["mappingKind"] = "property-column";
        AddSafeName(mappingProps, "entityName", "entityHash", className);
        AddSafeName(mappingProps, "propertyName", "propertyHash", propertyName);
        AddSafeName(mappingProps, "columnName", "columnHash", columnName);
        AddSafeName(mappingProps, "tableName", "tableHash", tableName);
        AddOptional(mappingProps, "descriptorSource", property.Name.LocalName);
        AddModelIdentity(mappingProps, "NHibernateHbm", "column", "mapping", relativePath, "nhibernate-property-column-mapping", propertyName, tableName ?? className, sourceMetadataFactId, Parts(("class", className), ("property", propertyName), ("column", columnName), ("descriptor", property.Name.LocalName)));
        facts.Add(CreateLegacyFact(manifest, FactTypes.LegacyDataMappingDeclared, RuleIds.LegacyDataOrmNHibernate, relativePath, property, TargetFrom(mappingProps, "propertyName", "propertyHash"), mappingProps));
    }

    private static void AddNHibernateRelationshipFact(
        ScanManifest manifest,
        List<CodeFact> facts,
        string relativePath,
        string metadataHash,
        string sourceMetadataFactId,
        string className,
        XElement relationship)
    {
        var relationshipName = AttributeValue(relationship, "name") ?? relationship.Name.LocalName;
        var targetClass = AttributeValue(relationship, "class")
            ?? AttributeValue(relationship.Elements().FirstOrDefault(element => element.Name.LocalName is "one-to-many" or "many-to-many"), "class");
        var coverageLabel = string.IsNullOrWhiteSpace(targetClass) ? "reduced" : "full";
        var limitations = string.IsNullOrWhiteSpace(targetClass)
            ? new[] { "missing-target-endpoint" }
            : Array.Empty<string>();

        var properties = MetadataProperties("NHibernateHbm", metadataHash, $"orm-{NormalizeToken(relationship.Name.LocalName, "relationship")}");
        properties["mappingKind"] = NormalizeToken(relationship.Name.LocalName, "relationship");
        AddSafeName(properties, "associationName", "associationHash", relationshipName);
        AddSafeName(properties, "sourceEndpointName", "sourceEndpointHash", className);
        AddSafeName(properties, "targetEndpointName", "targetEndpointHash", targetClass);
        AddSafeName(properties, "columnName", "columnHash", AttributeValue(relationship, "column") ?? NHibernateKeyColumn(relationship));
        AddRelationshipSemantics(properties, sourceMetadataFactId, coverageLabel == "full" ? "full" : "unidirectional", limitations);
        AddModelIdentity(properties, "NHibernateHbm", "relationship", "mapping", relativePath, "nhibernate-relationship", relationshipName, className, sourceMetadataFactId, Parts(("class", className), ("relationship", relationshipName), ("target-class", targetClass), ("descriptor", relationship.Name.LocalName)), coverageLabel);
        facts.Add(CreateLegacyFact(manifest, FactTypes.LegacyDataMappingDeclared, RuleIds.LegacyDataOrmNHibernate, relativePath, relationship, TargetFrom(properties, "associationName", "associationHash"), properties));
    }

    private static void AddNHibernateUnsupportedShapeGaps(ScanManifest manifest, List<CodeFact> facts, string relativePath, XDocument document)
    {
        foreach (var unsupported in document.Descendants().Where(IsUnsupportedNHibernateShape).OrderBy(GetLine))
        {
            AddGap(manifest, facts, relativePath, RuleIds.LegacyDataOrmNHibernate, "UnsupportedLegacyOrmMappingShape", $"Unsupported NHibernate mapping shape: {unsupported.Name.LocalName}.", unsupported);
        }
    }

    private static void ExtractDbml(string repoPath, ScanManifest manifest, FileInventoryItem item, List<CodeFact> facts)
    {
        var fullPath = Path.Combine(repoPath, item.RelativePath);
        if (!TryLoadMetadataDocument(manifest, item, fullPath, "Dbml", facts, out var document, out var metadataHash))
        {
            return;
        }

        var metadataFact = AddMetadataInventoryFact(manifest, facts, item.RelativePath, "Dbml", metadataHash, RuleIds.LegacyDataMetadataInventory, document.Root);

        var databases = document.Descendants().Where(element => element.Name.LocalName == "Database").ToArray();
        if (databases.Length > 1)
        {
            AddGap(manifest, facts, item.RelativePath, RuleIds.LegacyDataDbml, "UnsupportedLegacyDataMetadataVersion", "Multiple DBML Database descriptors were present and require a future deterministic mapping rule.", databases[1]);
        }
        var coverageLabel = databases.Length > 1 ? "reduced" : "full";

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
            AddModelIdentity(storageProps, "Dbml", "storage-object", "storage", item.RelativePath, "dbml-table", tableName, null, metadataFact.FactId, Parts(("table", tableName)), coverageLabel);
            facts.Add(CreateLegacyFact(manifest, FactTypes.LegacyDataStorageObjectDeclared, RuleIds.LegacyDataDbml, item.RelativePath, table, TargetFrom(storageProps, "storageObjectName", "storageObjectHash"), storageProps));

            if (!string.IsNullOrWhiteSpace(typeName))
            {
                var entityProps = MetadataProperties("Dbml", metadataHash, "entity");
                AddSafeName(entityProps, "entityName", "entityHash", typeName);
                AddSafeName(entityProps, "typeName", "typeHash", typeName);
                AddGeneratedHints(entityProps, database, item.RelativePath);
                AddModelIdentity(entityProps, "Dbml", "entity", "conceptual", item.RelativePath, "dbml-entity", typeName, AttributeValue(database, "Class"), metadataFact.FactId, Parts(("type", typeName), ("table", tableName)), coverageLabel);
                facts.Add(CreateLegacyFact(manifest, FactTypes.LegacyDataEntityDeclared, RuleIds.LegacyDataDbml, item.RelativePath, typeElement ?? table, TargetFrom(entityProps, "typeName", "typeHash"), entityProps));

                var mappingProps = MetadataProperties("Dbml", metadataHash, "entity-table");
                mappingProps["mappingKind"] = "entity-table";
                AddSafeName(mappingProps, "entityName", "entityHash", typeName);
                AddSafeName(mappingProps, "tableName", "tableHash", tableName);
                AddModelIdentity(mappingProps, "Dbml", "entity", "mapping", item.RelativePath, "dbml-entity-table", typeName, tableName, metadataFact.FactId, Parts(("type", typeName), ("table", tableName)), coverageLabel);
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
                AddModelIdentity(columnProps, "Dbml", "column", "storage", item.RelativePath, "dbml-column", columnName, tableName, metadataFact.FactId, Parts(("table", tableName), ("column", columnName), ("property", memberName)), coverageLabel);
                facts.Add(CreateLegacyFact(manifest, FactTypes.LegacyDataColumnDeclared, RuleIds.LegacyDataDbml, item.RelativePath, column, TargetFrom(columnProps, "propertyName", "propertyHash"), columnProps));

                var mappingProps = MetadataProperties("Dbml", metadataHash, "property-column");
                mappingProps["mappingKind"] = "property-column";
                AddSafeName(mappingProps, "entityName", "entityHash", typeName);
                AddSafeName(mappingProps, "propertyName", "propertyHash", memberName);
                AddSafeName(mappingProps, "tableName", "tableHash", tableName);
                AddSafeName(mappingProps, "columnName", "columnHash", columnName);
                AddModelIdentity(mappingProps, "Dbml", "column", "mapping", item.RelativePath, "dbml-property-column", memberName, tableName, metadataFact.FactId, Parts(("type", typeName), ("property", memberName), ("table", tableName), ("column", columnName)), coverageLabel);
                facts.Add(CreateLegacyFact(manifest, FactTypes.LegacyDataMappingDeclared, RuleIds.LegacyDataDbml, item.RelativePath, column, TargetFrom(mappingProps, "propertyName", "propertyHash"), mappingProps));
            }
        }

        var associations = database.Descendants().Where(element => element.Name.LocalName == "Association").ToArray();
        var duplicateAssociationKeys = associations
            .GroupBy(association => $"{AttributeValue(association.Parent, "Name") ?? "unknown"}|{AttributeValue(association, "Name") ?? AttributeValue(association, "Member") ?? "association"}", StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        foreach (var duplicate in duplicateAssociationKeys.Values.OrderBy(GetLine))
        {
            AddGap(manifest, facts, item.RelativePath, RuleIds.LegacyDataDbml, "AmbiguousLegacyDataModelIdentity", "Duplicate DBML association names in the same source type require selector review.", duplicate);
        }

        foreach (var association in associations.OrderBy(GetLine))
        {
            var associationName = AttributeValue(association, "Name") ?? AttributeValue(association, "Member") ?? "association";
            var sourceTypeName = AttributeValue(association.Parent, "Name");
            var targetTypeName = AttributeValue(association, "Type");
            var associationKey = $"{sourceTypeName ?? "unknown"}|{associationName}";
            var relationshipCoverageLabel = coverageLabel;
            var relationshipLimitations = new List<string>();
            if (duplicateAssociationKeys.ContainsKey(associationKey))
            {
                relationshipCoverageLabel = "reduced";
                relationshipLimitations.Add("duplicate-relationship-name");
            }

            if (string.IsNullOrWhiteSpace(targetTypeName))
            {
                relationshipCoverageLabel = "reduced";
                relationshipLimitations.Add("missing-target-endpoint");
            }

            var properties = MetadataProperties("Dbml", metadataHash, "association");
            properties["mappingKind"] = "association";
            AddSafeName(properties, "associationName", "associationHash", associationName);
            AddSafeName(properties, "sourceEndpointName", "sourceEndpointHash", sourceTypeName);
            AddSafeName(properties, "targetEndpointName", "targetEndpointHash", targetTypeName);
            AddSafeName(properties, "sourceMemberName", "sourceMemberHash", AttributeValue(association, "ThisKey"));
            AddSafeName(properties, "targetMemberName", "targetMemberHash", AttributeValue(association, "OtherKey"));
            AddOptional(properties, "isForeignKey", AttributeValue(association, "IsForeignKey"));
            AddRelationshipSemantics(properties, metadataFact.FactId, string.IsNullOrWhiteSpace(targetTypeName) ? "unidirectional" : "full", relationshipLimitations);
            AddModelIdentity(properties, "Dbml", "relationship", "mapping", item.RelativePath, "dbml-association", associationName, sourceTypeName, metadataFact.FactId, Parts(("association", associationName), ("source-type", sourceTypeName), ("target-type", targetTypeName), ("this-key", AttributeValue(association, "ThisKey")), ("other-key", AttributeValue(association, "OtherKey"))), relationshipCoverageLabel);
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
            AddModelIdentity(properties, "Dbml", "routine", "storage", item.RelativePath, "dbml-routine", routineName, null, metadataFact.FactId, Parts(("routine", routineName), ("method", AttributeValue(routine, "Method"))), coverageLabel);
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

        var metadataFact = AddMetadataInventoryFact(manifest, facts, item.RelativePath, "Edmx", metadataHash, RuleIds.LegacyDataMetadataInventory, document.Root);

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
            AddGap(manifest, facts, item.RelativePath, RuleIds.LegacyDataEdmx, "AmbiguousLegacyDataModelIdentity", "Multiple conceptual or storage containers require review.", document.Root);
        }
        var coverageLabel = csdlSchemas.Length == 0 || ssdlSchemas.Length == 0 || mappingElements.Length == 0 || conceptualContainers.Length > 1 || storageContainers.Length > 1
            ? "reduced"
            : "full";
        var inheritedEdmxTypeNames = csdlSchemas
            .SelectMany(schema => schema.Elements().Where(element => element.Name.LocalName == "EntityType" && !string.IsNullOrWhiteSpace(AttributeValue(element, "BaseType"))))
            .Select(element => AttributeValue(element, "Name"))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var entity in csdlSchemas.SelectMany(schema => schema.Elements().Where(element => element.Name.LocalName == "EntityType")).OrderBy(GetLine).ThenBy(element => AttributeValue(element, "Name"), StringComparer.Ordinal))
        {
            var name = AttributeValue(entity, "Name") ?? "entity";
            var inheritedEntity = inheritedEdmxTypeNames.Contains(name);
            var entityCoverageLabel = inheritedEntity ? "reduced" : coverageLabel;
            if (inheritedEntity)
            {
                AddGap(manifest, facts, item.RelativePath, RuleIds.LegacyDataEdmx, "UnsupportedLegacyOrmMappingShape", "EDMX inherited model relationship shape requires future deterministic handling.", entity);
            }

            var properties = MetadataProperties("Edmx", metadataHash, "csdl-entity");
            properties["sourceSection"] = "CSDL";
            AddSafeName(properties, "entityName", "entityHash", name);
            AddSafeName(properties, "typeName", "typeHash", name);
            if (inheritedEntity)
            {
                properties["limitations"] = "unsupported-inherited-model-shape";
            }

            AddModelIdentity(properties, "Edmx", "entity", "conceptual", item.RelativePath, "edmx-csdl-entity", name, AttributeValue(entity.Parent, "Namespace"), metadataFact.FactId, Parts(("entity", name), ("namespace", AttributeValue(entity.Parent, "Namespace"))), entityCoverageLabel);
            facts.Add(CreateLegacyFact(manifest, FactTypes.LegacyDataEntityDeclared, RuleIds.LegacyDataEdmx, item.RelativePath, entity, TargetFrom(properties, "typeName", "typeHash"), properties));

            foreach (var property in entity.Elements().Where(element => element.Name.LocalName is "Property" or "NavigationProperty").OrderBy(GetLine))
            {
                var propertyName = AttributeValue(property, "Name") ?? "property";
                var columnProps = MetadataProperties("Edmx", metadataHash, "csdl-property");
                columnProps["sourceSection"] = "CSDL";
                AddSafeName(columnProps, "entityName", "entityHash", name);
                AddSafeName(columnProps, "propertyName", "propertyHash", propertyName);
                AddOptional(columnProps, "descriptorKind", property.Name.LocalName);
                if (inheritedEntity)
                {
                    columnProps["limitations"] = "unsupported-inherited-model-shape";
                }

                AddModelIdentity(columnProps, "Edmx", "column", "conceptual", item.RelativePath, "edmx-csdl-property", propertyName, name, metadataFact.FactId, Parts(("entity", name), ("property", propertyName), ("descriptor-kind", property.Name.LocalName)), entityCoverageLabel);
                facts.Add(CreateLegacyFact(manifest, FactTypes.LegacyDataColumnDeclared, RuleIds.LegacyDataEdmx, item.RelativePath, property, TargetFrom(columnProps, "propertyName", "propertyHash"), columnProps));
            }
        }

        foreach (var association in csdlSchemas.SelectMany(schema => schema.Elements().Where(element => element.Name.LocalName == "Association")).OrderBy(GetLine))
        {
            AddEdmxAssociationFact(manifest, facts, item.RelativePath, metadataHash, metadataFact.FactId, coverageLabel, inheritedEdmxTypeNames, association);
        }

        foreach (var set in conceptualContainers.SelectMany(container => container.Elements().Where(element => element.Name.LocalName == "EntitySet")).OrderBy(GetLine))
        {
            var name = AttributeValue(set, "Name") ?? "entity-set";
            var properties = MetadataProperties("Edmx", metadataHash, "csdl-entity-set");
            properties["sourceSection"] = "CSDL";
            AddSafeName(properties, "entityName", "entityHash", name);
            AddSafeName(properties, "entityTypeName", "entityTypeHash", LocalName(AttributeValue(set, "EntityType")));
            AddModelIdentity(properties, "Edmx", "entity", "conceptual", item.RelativePath, "edmx-csdl-entity-set", name, AttributeValue(set.Parent, "Name"), metadataFact.FactId, Parts(("entity-set", name), ("entity-type", LocalName(AttributeValue(set, "EntityType"))), ("container", AttributeValue(set.Parent, "Name"))), coverageLabel);
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
            AddModelIdentity(properties, "Edmx", "storage-object", "storage", item.RelativePath, "edmx-ssdl-entity-set", tableName, AttributeValue(set.Parent, "Name"), metadataFact.FactId, Parts(("table", tableName), ("entity-set", AttributeValue(set, "Name")), ("container", AttributeValue(set.Parent, "Name"))), coverageLabel);
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
                AddModelIdentity(properties, "Edmx", "column", "storage", item.RelativePath, "edmx-ssdl-column", columnName, storageType, metadataFact.FactId, Parts(("storage-type", storageType), ("column", columnName)), coverageLabel);
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
            AddModelIdentity(properties, "Edmx", "routine", "storage", item.RelativePath, "edmx-routine", routineName, AttributeValue(function.Parent, "Namespace"), metadataFact.FactId, Parts(("routine", routineName), ("section", properties["sourceSection"]), ("namespace", AttributeValue(function.Parent, "Namespace"))), coverageLabel);
            facts.Add(CreateLegacyFact(manifest, FactTypes.LegacyDataStorageObjectDeclared, RuleIds.LegacyDataEdmx, item.RelativePath, function, TargetFrom(properties, "storageObjectName", "storageObjectHash"), properties));
        }

        AddEdmxMappings(manifest, facts, item.RelativePath, metadataHash, metadataFact.FactId, coverageLabel, mappingElements);
    }

    private static void AddEdmxMappings(ScanManifest manifest, List<CodeFact> facts, string relativePath, string metadataHash, string sourceMetadataFactId, string coverageLabel, IReadOnlyList<XElement> mappingElements)
    {
        var unsupportedMappings = mappingElements.SelectMany(mapping => mapping.Descendants())
            .Where(element => element.Name.LocalName is "Condition" or "ComplexProperty" or "FunctionImportMapping")
            .OrderBy(GetLine)
            .ToArray();
        foreach (var unsupported in unsupportedMappings)
        {
            AddGap(manifest, facts, relativePath, RuleIds.LegacyDataEdmx, "UnsupportedLegacyOrmMappingShape", $"Unsupported EDMX mapping shape: {unsupported.Name.LocalName}.", unsupported);
        }
        var mappingCoverageLabel = unsupportedMappings.Length > 0 ? "reduced" : coverageLabel;

        foreach (var entitySetMapping in mappingElements.SelectMany(mapping => mapping.Descendants().Where(element => element.Name.LocalName == "EntitySetMapping")).OrderBy(GetLine))
        {
            var entitySetName = AttributeValue(entitySetMapping, "Name") ?? "entity-set";
            var fragments = entitySetMapping.Descendants().Where(element => element.Name.LocalName == "MappingFragment").ToArray();
            if (fragments.Length != 1)
            {
                AddGap(manifest, facts, relativePath, RuleIds.LegacyDataEdmx, "AmbiguousLegacyDataModelIdentity", "EntitySetMapping did not contain exactly one MappingFragment.", entitySetMapping);
                continue;
            }

            var fragment = fragments[0];
            var storeSet = AttributeValue(fragment, "StoreEntitySet") ?? "storage";
            var mappingProps = MetadataProperties("Edmx", metadataHash, "entity-table");
            mappingProps["sourceSection"] = "MSL";
            mappingProps["mappingKind"] = "entity-table";
            AddSafeName(mappingProps, "entityName", "entityHash", entitySetName);
            AddSafeName(mappingProps, "tableName", "tableHash", storeSet);
            AddModelIdentity(mappingProps, "Edmx", "entity", "mapping", relativePath, "edmx-msl-entity-table", entitySetName, storeSet, sourceMetadataFactId, Parts(("entity-set", entitySetName), ("store-set", storeSet)), mappingCoverageLabel);
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
                AddModelIdentity(properties, "Edmx", "column", "mapping", relativePath, "edmx-msl-property-column", propertyName, storeSet, sourceMetadataFactId, Parts(("entity-set", entitySetName), ("property", propertyName), ("store-set", storeSet), ("column", columnName)), mappingCoverageLabel);
                facts.Add(CreateLegacyFact(manifest, FactTypes.LegacyDataMappingDeclared, RuleIds.LegacyDataEdmx, relativePath, scalar, TargetFrom(properties, "propertyName", "propertyHash"), properties));
            }
        }

        foreach (var associationSetMapping in mappingElements.SelectMany(mapping => mapping.Descendants().Where(element => element.Name.LocalName == "AssociationSetMapping")).OrderBy(GetLine))
        {
            AddEdmxAssociationSetMappingFact(manifest, facts, relativePath, metadataHash, sourceMetadataFactId, mappingCoverageLabel, associationSetMapping);
        }
    }

    private static void AddEdmxAssociationFact(
        ScanManifest manifest,
        List<CodeFact> facts,
        string relativePath,
        string metadataHash,
        string sourceMetadataFactId,
        string coverageLabel,
        IReadOnlySet<string> inheritedEdmxTypeNames,
        XElement association)
    {
        var associationName = AttributeValue(association, "Name") ?? "association";
        var ends = association.Elements().Where(element => element.Name.LocalName == "End").ToArray();
        if (ends.Length != 2)
        {
            AddGap(manifest, facts, relativePath, RuleIds.LegacyDataEdmx, "AmbiguousLegacyDataModelIdentity", "EDMX association did not contain exactly two deterministic endpoints.", association);
            return;
        }

        var firstRole = AttributeValue(ends[0], "Role");
        var secondRole = AttributeValue(ends[1], "Role");
        if (string.IsNullOrWhiteSpace(firstRole)
            || string.IsNullOrWhiteSpace(secondRole)
            || string.Equals(firstRole, secondRole, StringComparison.Ordinal))
        {
            AddGap(manifest, facts, relativePath, RuleIds.LegacyDataEdmx, "AmbiguousLegacyDataModelIdentity", "EDMX association endpoints were missing or duplicated.", association);
            return;
        }

        var firstType = LocalName(AttributeValue(ends[0], "Type"));
        var secondType = LocalName(AttributeValue(ends[1], "Type"));
        var relationshipCoverageLabel = coverageLabel;
        var relationshipEndpointCoverage = "full";
        var relationshipLimitations = new List<string>();
        if (string.IsNullOrWhiteSpace(firstType) || string.IsNullOrWhiteSpace(secondType))
        {
            relationshipCoverageLabel = "reduced";
            relationshipEndpointCoverage = "unidirectional";
            relationshipLimitations.Add("missing-endpoint-type");
        }
        else if (inheritedEdmxTypeNames.Contains(firstType) || inheritedEdmxTypeNames.Contains(secondType))
        {
            relationshipCoverageLabel = "reduced";
            relationshipLimitations.Add("inherited-endpoint-needs-review");
        }

        var properties = MetadataProperties("Edmx", metadataHash, "csdl-association");
        properties["sourceSection"] = "CSDL";
        properties["mappingKind"] = "association";
        AddSafeName(properties, "associationName", "associationHash", associationName);
        AddSafeName(properties, "sourceEndpointName", "sourceEndpointHash", firstType ?? firstRole);
        AddSafeName(properties, "targetEndpointName", "targetEndpointHash", secondType ?? secondRole);
        AddSafeName(properties, "sourceEndpointRole", "sourceEndpointRoleHash", firstRole);
        AddSafeName(properties, "targetEndpointRole", "targetEndpointRoleHash", secondRole);
        AddSafeName(properties, "sourceMultiplicity", "sourceMultiplicityHash", AttributeValue(ends[0], "Multiplicity"));
        AddSafeName(properties, "targetMultiplicity", "targetMultiplicityHash", AttributeValue(ends[1], "Multiplicity"));
        AddRelationshipSemantics(properties, sourceMetadataFactId, relationshipEndpointCoverage, relationshipLimitations);
        AddModelIdentity(
            properties,
            "Edmx",
            "relationship",
            "mapping",
            relativePath,
            "edmx-csdl-association",
            associationName,
            AttributeValue(association.Parent, "Namespace"),
            sourceMetadataFactId,
            Parts(("association", associationName), ("source-role", firstRole), ("target-role", secondRole), ("source-type", firstType), ("target-type", secondType)),
            relationshipCoverageLabel);
        facts.Add(CreateLegacyFact(manifest, FactTypes.LegacyDataMappingDeclared, RuleIds.LegacyDataEdmx, relativePath, association, TargetFrom(properties, "associationName", "associationHash"), properties));
    }

    private static void AddEdmxAssociationSetMappingFact(
        ScanManifest manifest,
        List<CodeFact> facts,
        string relativePath,
        string metadataHash,
        string sourceMetadataFactId,
        string coverageLabel,
        XElement associationSetMapping)
    {
        var associationName = AttributeValue(associationSetMapping, "Name") ?? LocalName(AttributeValue(associationSetMapping, "TypeName")) ?? "association";
        var endProperties = associationSetMapping.Elements().Where(element => element.Name.LocalName == "EndProperty").ToArray();
        if (endProperties.Length != 2)
        {
            AddGap(manifest, facts, relativePath, RuleIds.LegacyDataEdmx, "AmbiguousLegacyDataModelIdentity", "AssociationSetMapping did not contain exactly two deterministic endpoints.", associationSetMapping);
            return;
        }

        var firstRole = AttributeValue(endProperties[0], "Name");
        var secondRole = AttributeValue(endProperties[1], "Name");
        if (string.IsNullOrWhiteSpace(firstRole)
            || string.IsNullOrWhiteSpace(secondRole)
            || string.Equals(firstRole, secondRole, StringComparison.Ordinal))
        {
            AddGap(manifest, facts, relativePath, RuleIds.LegacyDataEdmx, "AmbiguousLegacyDataModelIdentity", "AssociationSetMapping endpoints were missing or duplicated.", associationSetMapping);
            return;
        }

        var properties = MetadataProperties("Edmx", metadataHash, "msl-association");
        properties["sourceSection"] = "MSL";
        properties["mappingKind"] = "association";
        AddSafeName(properties, "associationName", "associationHash", associationName);
        AddSafeName(properties, "storeEntitySetName", "storeEntitySetHash", AttributeValue(associationSetMapping, "StoreEntitySet"));
        AddSafeName(properties, "sourceEndpointName", "sourceEndpointHash", firstRole);
        AddSafeName(properties, "targetEndpointName", "targetEndpointHash", secondRole);
        AddRelationshipSemantics(properties, sourceMetadataFactId, "full", Array.Empty<string>());
        AddModelIdentity(
            properties,
            "Edmx",
            "relationship",
            "mapping",
            relativePath,
            "edmx-msl-association",
            associationName,
            AttributeValue(associationSetMapping, "StoreEntitySet"),
            sourceMetadataFactId,
            Parts(("association", associationName), ("store-set", AttributeValue(associationSetMapping, "StoreEntitySet")), ("source-role", firstRole), ("target-role", secondRole)),
            coverageLabel);
        facts.Add(CreateLegacyFact(manifest, FactTypes.LegacyDataMappingDeclared, RuleIds.LegacyDataEdmx, relativePath, associationSetMapping, TargetFrom(properties, "associationName", "associationHash"), properties));
    }

    private static void AddTypedDataSetConstraintRelationshipFacts(
        ScanManifest manifest,
        List<CodeFact> facts,
        string relativePath,
        string metadataHash,
        string sourceMetadataFactId,
        XDocument document)
    {
        var constraintDefinitions = document.Descendants()
            .Where(IsXsdKeyOrUnique)
            .Select(element => new
            {
                Element = element,
                Name = LocalQualifiedName(AttributeValue(element, "name")),
                Table = LastXPathIdentifier(element.Elements().FirstOrDefault(IsXsdSelector) is { } selector ? AttributeValue(selector, "xpath") : null)
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Name) && !string.IsNullOrWhiteSpace(item.Table))
            .Select(item => new ConstraintDefinition(item.Element, item.Name, item.Table!))
            .ToArray();
        var ambiguousConstraintNames = constraintDefinitions
            .GroupBy(item => item.Name, StringComparer.Ordinal)
            .Where(group => group.Select(item => item.Table).Distinct(StringComparer.Ordinal).Count() > 1)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        foreach (var ambiguousGroup in ambiguousConstraintNames.Values.SelectMany(group => group).OrderBy(item => GetLine(item.Element)))
        {
            AddGap(manifest, facts, relativePath, RuleIds.LegacyDataTypedDataSet, "AmbiguousLegacyDataModelIdentity", "Typed DataSet constraint name resolved to multiple selector tables.", ambiguousGroup.Element);
        }

        var keyedTables = constraintDefinitions
            .Where(item => !ambiguousConstraintNames.ContainsKey(item.Name))
            .GroupBy(item => item.Name, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Table, StringComparer.Ordinal);

        foreach (var keyref in document.Descendants().Where(IsXsdKeyRef).OrderBy(GetLine))
        {
            var relationName = AttributeValue(keyref, "name") ?? "keyref";
            var referencedKey = LocalQualifiedName(AttributeValue(keyref, "refer"));
            var referencesAmbiguousConstraint = ambiguousConstraintNames.ContainsKey(referencedKey);
            var parentTableName = keyedTables.GetValueOrDefault(referencedKey);
            var selector = keyref.Elements().FirstOrDefault(IsXsdSelector);
            var childTableName = LastXPathIdentifier(AttributeValue(selector, "xpath"));
            var coverageLabel = string.IsNullOrWhiteSpace(parentTableName) || string.IsNullOrWhiteSpace(childTableName) ? "reduced" : "full";
            var limitations = new List<string>();
            if (coverageLabel == "reduced")
            {
                limitations.Add("constraint-endpoint-needs-review");
            }

            if (referencesAmbiguousConstraint)
            {
                limitations.Add("ambiguous-constraint-name");
                AddGap(manifest, facts, relativePath, RuleIds.LegacyDataTypedDataSet, "AmbiguousLegacyDataModelIdentity", "Typed DataSet keyref referenced an ambiguous constraint name.", keyref);
            }

            var properties = MetadataProperties("TypedDataSet", metadataHash, "constraint-relation");
            properties["mappingKind"] = "relation";
            AddSafeName(properties, "relationName", "relationHash", relationName);
            AddSafeName(properties, "parentTableName", "parentTableHash", parentTableName);
            AddSafeName(properties, "childTableName", "childTableHash", childTableName);
            AddSafeName(properties, "sourceEndpointName", "sourceEndpointHash", parentTableName);
            AddSafeName(properties, "targetEndpointName", "targetEndpointHash", childTableName);
            AddSafeName(properties, "referencedConstraintName", "referencedConstraintHash", referencedKey);
            AddRelationshipSemantics(properties, sourceMetadataFactId, coverageLabel == "reduced" ? "unidirectional" : "full", limitations);
            AddModelIdentity(
                properties,
                "TypedDataSet",
                "relationship",
                "generated",
                relativePath,
                "typed-dataset-constraint-relation",
                relationName,
                parentTableName,
                sourceMetadataFactId,
                Parts(("relation", relationName), ("parent", parentTableName), ("child", childTableName), ("refer", referencedKey)),
                coverageLabel);
            facts.Add(CreateLegacyFact(manifest, FactTypes.LegacyDataMappingDeclared, RuleIds.LegacyDataTypedDataSet, relativePath, keyref, TargetFrom(properties, "relationName", "relationHash"), properties));
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

        var metadataFact = AddMetadataInventoryFact(manifest, facts, item.RelativePath, "TypedDataSet", metadataHash, RuleIds.LegacyDataMetadataInventory, document.Root);
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
            AddModelIdentity(properties, "TypedDataSet", "mapped-type", "generated", item.RelativePath, "typed-dataset", dataSetName, null, metadataFact.FactId, Parts(("dataset", dataSetName)));
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
            AddModelIdentity(properties, "TypedDataSet", "entity", "generated", item.RelativePath, "typed-dataset-table", rowClass ?? tableName, tableName, metadataFact.FactId, Parts(("table", tableName), ("row-class", rowClass ?? tableName)));
            facts.Add(CreateLegacyFact(manifest, FactTypes.LegacyDataEntityDeclared, RuleIds.LegacyDataTypedDataSet, item.RelativePath, table, TargetFrom(properties, "entityName", "entityHash"), properties));
            var storageProperties = With(properties, ("storageObjectKind", "DataTable"));
            AddModelIdentity(storageProperties, "TypedDataSet", "storage-object", "generated", item.RelativePath, "typed-dataset-storage-table", tableName, null, metadataFact.FactId, Parts(("table", tableName), ("row-class", rowClass ?? tableName)));
            facts.Add(CreateLegacyFact(manifest, FactTypes.LegacyDataStorageObjectDeclared, RuleIds.LegacyDataTypedDataSet, item.RelativePath, table, TargetFrom(storageProperties, "tableName", "tableHash"), storageProperties));

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
                AddModelIdentity(columnProps, "TypedDataSet", "column", "generated", item.RelativePath, "typed-dataset-column", columnName, tableName, metadataFact.FactId, Parts(("table", tableName), ("column", columnName)));
                facts.Add(CreateLegacyFact(manifest, FactTypes.LegacyDataColumnDeclared, RuleIds.LegacyDataTypedDataSet, item.RelativePath, column, TargetFrom(columnProps, "columnName", "columnHash"), columnProps));
            }
        }

        foreach (var relation in document.Descendants().Where(element => element.Name == MsData + "Relationship").OrderBy(GetLine))
        {
            var parentTableName = AttributeValue(relation, "parent");
            var childTableName = AttributeValue(relation, "child");
            var relationshipCoverageLabel = string.IsNullOrWhiteSpace(parentTableName) || string.IsNullOrWhiteSpace(childTableName) ? "reduced" : "full";
            var limitations = new List<string>();
            if (relationshipCoverageLabel == "reduced")
            {
                limitations.Add("missing-relationship-endpoint");
            }

            var properties = MetadataProperties("TypedDataSet", metadataHash, "relation");
            properties["mappingKind"] = "relation";
            AddSafeName(properties, "relationName", "relationHash", AttributeValue(relation, "name"));
            AddSafeName(properties, "parentTableName", "parentTableHash", parentTableName);
            AddSafeName(properties, "childTableName", "childTableHash", childTableName);
            AddSafeName(properties, "sourceEndpointName", "sourceEndpointHash", parentTableName);
            AddSafeName(properties, "targetEndpointName", "targetEndpointHash", childTableName);
            AddRelationshipSemantics(properties, metadataFact.FactId, relationshipCoverageLabel == "reduced" ? "unidirectional" : "full", limitations);
            AddModelIdentity(properties, "TypedDataSet", "relationship", "generated", item.RelativePath, "typed-dataset-relation", AttributeValue(relation, "name"), parentTableName, metadataFact.FactId, Parts(("relation", AttributeValue(relation, "name")), ("parent", parentTableName), ("child", childTableName)), relationshipCoverageLabel);
            facts.Add(CreateLegacyFact(manifest, FactTypes.LegacyDataMappingDeclared, RuleIds.LegacyDataTypedDataSet, item.RelativePath, relation, TargetFrom(properties, "relationName", "relationHash"), properties));
        }

        AddTypedDataSetConstraintRelationshipFacts(manifest, facts, item.RelativePath, metadataHash, metadataFact.FactId, document);

        foreach (var command in document.Descendants().Where(IsTableAdapterCommand).OrderBy(GetLine))
        {
            AddTableAdapterCommandFacts(manifest, facts, item.RelativePath, metadataHash, metadataFact.FactId, command);
        }
    }

    private static void AddTableAdapterCommandFacts(ScanManifest manifest, List<CodeFact> facts, string relativePath, string metadataHash, string sourceMetadataFactId, XElement command)
    {
        var commandText = AttributeValue(command, "CommandText")
            ?? AttributeValue(command, "commandText")
            ?? command.Elements().FirstOrDefault(element => element.Name.LocalName.Contains("CommandText", StringComparison.OrdinalIgnoreCase))?.Value.Trim();
        var commandName = AttributeValue(command, "Name") ?? AttributeValue(command, "name") ?? AttributeValue(command, "MethodName") ?? command.Name.LocalName;
        var properties = MetadataProperties("TypedDataSet", metadataHash, "table-adapter-command");
        AddSafeName(properties, "commandName", "commandHash", commandName);
        AddSafeName(properties, "methodName", "methodHash", AttributeValue(command, "MethodName"));
        properties["mappingKind"] = "adapter-command";
        var coverageLabel = string.IsNullOrWhiteSpace(commandText) ? "reduced" : "full";
        AddModelIdentity(properties, "TableAdapter", "adapter", "generated", relativePath, "typed-dataset-table-adapter-command", commandName, null, sourceMetadataFactId, Parts(("command", commandName), ("method", AttributeValue(command, "MethodName"))), coverageLabel);

        if (string.IsNullOrWhiteSpace(commandText))
        {
            facts.Add(CreateLegacyFact(manifest, FactTypes.LegacyDataMappingDeclared, RuleIds.LegacyDataTypedDataSet, relativePath, command, TargetFrom(properties, "commandName", "commandHash"), properties));
            AddGap(manifest, facts, relativePath, RuleIds.LegacyDataTypedDataSet, "DynamicTableAdapterCommand", "TableAdapter command text was not complete static text.", command);
            return;
        }

        var protectedFact = SqlSecretSafetyExtractor.CreateEmbeddedFact(
            manifest,
            relativePath,
            GetLine(command),
            GetLine(command),
            commandText);
        if (protectedFact is not null)
        {
            properties["coverageLabel"] = "reduced";
            properties["redactionReason"] = "protected-sql-material";
            properties.Remove("metadataHash");
            properties["evidenceScope"] = "static-design-time-metadata";
            properties["runtimeProof"] = "False";
            var target = TargetFrom(properties, "commandName", "commandHash");
            facts.Add(FactFactory.Create(
                manifest,
                FactTypes.LegacyDataMappingDeclared,
                RuleIds.LegacyDataTypedDataSet,
                EvidenceTiers.Tier2Structural,
                new EvidenceSpan(relativePath, GetLine(command), GetLine(command), null, ExtractorId, ScannerVersions.LegacyDataExtractor),
                targetSymbol: target,
                contractElement: target,
                properties: properties));
            facts.Add(protectedFact);
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
            Evidence(item.RelativePath, 1, $"{item.RelativePath}:{item.SizeBytes}:generated-designer"),
            targetSymbol: Path.GetFileName(item.RelativePath),
            properties: properties));
    }

    private static void AddGeneratedCodeLinks(ScanManifest manifest, List<CodeFact> facts, IReadOnlyList<GeneratedCandidate> generatedCandidates)
    {
        var metadataFacts = facts
            .Where(fact => fact.RuleId is RuleIds.LegacyDataDbml or RuleIds.LegacyDataEdmx or RuleIds.LegacyDataTypedDataSet)
            .Where(fact => fact.FactType is FactTypes.LegacyDataEntityDeclared or FactTypes.LegacyDataStorageObjectDeclared)
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
            var scopedMatches = generatedCandidates
                .Where(candidate => string.IsNullOrWhiteSpace(explicitGeneratedName)
                    ? candidate.FileNameWithoutExtension.StartsWith(metadataBaseName, StringComparison.OrdinalIgnoreCase)
                    : candidate.FileName.Equals(explicitGeneratedName, StringComparison.OrdinalIgnoreCase))
                .SelectMany(candidate => candidate.LinesFor(expectedType).Select(line => new GeneratedTypeMatch(candidate.FilePath, line)))
                .ToArray();

            if (scopedMatches.Length == 1)
            {
                var candidate = scopedMatches[0];
                var isExplicitGeneratedFile = !string.IsNullOrWhiteSpace(explicitGeneratedName);
                var properties = MetadataProperties(fact.Properties.GetValueOrDefault("metadataKind") ?? "LegacyData", fact.Properties.GetValueOrDefault("metadataHash") ?? string.Empty, "generated-code-link");
                properties["coverageLabel"] = isExplicitGeneratedFile ? "full" : "reduced";
                properties["limitations"] = isExplicitGeneratedFile
                    ? "generated-code-freshness-unverified"
                    : "generated-code-freshness-unverified;syntax-only-generated-code-link";
                properties["sourceMetadataFactId"] = fact.FactId;
                properties["supportingFactIds"] = fact.FactId;
                properties["symbolRole"] = GeneratedSymbolRole(fact);
                properties["linkKind"] = isExplicitGeneratedFile ? "explicit-generated-file" : "type-name-syntax-fallback";
                properties["generatedCodeFileName"] = Path.GetFileName(candidate.FilePath);
                if (fact.Properties.TryGetValue("stableModelKey", out var stableModelKey))
                {
                    properties["stableModelKey"] = stableModelKey;
                }

                AddSafeName(properties, "typeName", "typeHash", expectedType);
                facts.Add(FactFactory.Create(
                    manifest,
                    FactTypes.LegacyDataGeneratedCodeLinked,
                    RuleIds.LegacyDataGeneratedLink,
                    isExplicitGeneratedFile ? EvidenceTiers.Tier2Structural : EvidenceTiers.Tier3SyntaxOrTextual,
                    Evidence(candidate.FilePath, candidate.Line, $"{candidate.FilePath}:{expectedType}:{fact.FactId}"),
                    targetSymbol: expectedType,
                    properties: properties));
            }
            else if (scopedMatches.Length > 1)
            {
                AddGeneratedLinkGap(manifest, facts, fact, "AmbiguousGeneratedCodeLink", "Multiple generated-code candidates matched a legacy data descriptor.", expectedType);
            }
            else if (!string.IsNullOrWhiteSpace(explicitGeneratedName))
            {
                AddGeneratedLinkGap(manifest, facts, fact, "MissingGeneratedCode", "Metadata names generated output that was not checked in.", expectedType);
            }
        }
    }

    private static void AddGeneratedLinkGap(
        ScanManifest manifest,
        List<CodeFact> facts,
        CodeFact sourceFact,
        string classification,
        string message,
        string expectedType)
    {
        var properties = MetadataProperties(
            sourceFact.Properties.GetValueOrDefault("metadataKind") ?? "LegacyData",
            sourceFact.Properties.GetValueOrDefault("metadataHash") ?? string.Empty,
            "generated-code-link-gap");
        properties["classification"] = classification;
        properties["coverage"] = "reduced";
        properties["message"] = message;
        properties["sourceMetadataFactId"] = sourceFact.FactId;
        properties["supportingFactIds"] = sourceFact.FactId;
        properties["symbolRole"] = GeneratedSymbolRole(sourceFact);
        if (sourceFact.Properties.TryGetValue("stableModelKey", out var stableModelKey))
        {
            properties["stableModelKey"] = stableModelKey;
        }

        AddSafeName(properties, "typeName", "typeHash", expectedType);
        facts.Add(FactFactory.Create(
            manifest,
            FactTypes.AnalysisGap,
            RuleIds.LegacyDataGeneratedLink,
            EvidenceTiers.Tier4Unknown,
            Evidence(sourceFact.Evidence.FilePath, sourceFact.Evidence.StartLine, $"{sourceFact.Evidence.FilePath}:{sourceFact.Evidence.StartLine}:{classification}:{sourceFact.FactId}:{expectedType}"),
            targetSymbol: TargetFrom(properties, "typeName", "typeHash"),
            properties: properties));
    }

    private static string GeneratedSymbolRole(CodeFact fact)
    {
        return fact.Properties.GetValueOrDefault("modelKind") switch
        {
            "mapped-type" => "generated-context",
            "entity" => "generated-entity",
            "storage-object" => "generated-storage-object",
            _ => "generated-type"
        };
    }

    private static void AddMappedTypeSyntaxLinks(ScanManifest manifest, List<CodeFact> facts, IReadOnlyDictionary<string, IReadOnlyList<CSharpTypeDeclaration>> csharpTypeDeclarations)
    {
        var mappedClassFacts = facts
            .Where(fact => fact.RuleId == RuleIds.LegacyDataOrmNHibernate
                && fact.FactType == FactTypes.LegacyDataEntityDeclared
                && string.Equals(fact.Properties.GetValueOrDefault("metadataFormat"), "nhibernate-hbm", StringComparison.Ordinal)
                && fact.Properties.TryGetValue("mappedTypeName", out var mappedTypeName)
                && IsQualifiedTypeName(mappedTypeName))
            .OrderBy(fact => fact.Evidence.FilePath, StringComparer.Ordinal)
            .ThenBy(fact => fact.Evidence.StartLine)
            .ThenBy(fact => fact.FactId, StringComparer.Ordinal)
            .ToArray();

        foreach (var fact in mappedClassFacts)
        {
            var mappedTypeName = fact.Properties.GetValueOrDefault("mappedTypeName");
            var lookupTypeName = CSharpTypeLookupName(mappedTypeName);
            if (string.IsNullOrWhiteSpace(mappedTypeName)
                || string.IsNullOrWhiteSpace(lookupTypeName)
                || !csharpTypeDeclarations.TryGetValue(lookupTypeName, out var declarations)
                || declarations.Count == 0)
            {
                AddGap(manifest, facts, fact.Evidence.FilePath, RuleIds.LegacyDataModelGeneratedLink, "MissingGeneratedCode", "NHibernate mapped class did not match a checked-in C# type declaration.", null, fact.Evidence.StartLine);
                continue;
            }

            if (declarations.Count > 1)
            {
                AddGap(manifest, facts, fact.Evidence.FilePath, RuleIds.LegacyDataModelGeneratedLink, "AmbiguousGeneratedCodeLink", "Multiple C# type declarations matched an NHibernate mapped class; no mapped-symbol link was inferred.", null, fact.Evidence.StartLine);
                continue;
            }

            var declaration = declarations[0];
            var properties = MetadataProperties(fact.Properties.GetValueOrDefault("metadataKind") ?? "NHibernateHbm", fact.Properties.GetValueOrDefault("metadataHash") ?? string.Empty, "mapped-symbol-link");
            properties["coverageLabel"] = "reduced";
            properties["linkKind"] = "mapped-type-syntax";
            properties["limitations"] = "syntax-only-mapped-type-link";
            properties["sourceMetadataFactId"] = fact.FactId;
            properties["supportingFactIds"] = fact.FactId;
            properties["symbolRole"] = "mapped-class";
            if (fact.Properties.TryGetValue("stableModelKey", out var stableModelKey))
            {
                properties["stableModelKey"] = stableModelKey;
            }

            properties["generatedCodeFileName"] = Path.GetFileName(declaration.FilePath);
            AddSafeName(properties, "mappedTypeName", "mappedTypeHash", mappedTypeName);
            AddSafeName(properties, "typeName", "typeHash", declaration.FullName);

            facts.Add(FactFactory.Create(
                manifest,
                FactTypes.LegacyDataGeneratedCodeLinked,
                RuleIds.LegacyDataModelGeneratedLink,
                EvidenceTiers.Tier3SyntaxOrTextual,
                Evidence(declaration.FilePath, declaration.Line, $"{declaration.FilePath}:{declaration.FullName}:{fact.FactId}:mapped-type-syntax"),
                targetSymbol: TargetFrom(properties, "typeName", "typeHash"),
                contractElement: TargetFrom(properties, "typeName", "typeHash"),
                properties: properties));
        }
    }

    private static IReadOnlySet<string> MappedTypeSyntaxLinkCandidateNames(IEnumerable<CodeFact> facts)
    {
        return new SortedSet<string>(facts
            .Where(fact => fact.RuleId == RuleIds.LegacyDataOrmNHibernate
            && fact.FactType == FactTypes.LegacyDataEntityDeclared
            && string.Equals(fact.Properties.GetValueOrDefault("metadataFormat"), "nhibernate-hbm", StringComparison.Ordinal)
            && fact.Properties.TryGetValue("mappedTypeName", out var mappedTypeName)
            && IsQualifiedTypeName(mappedTypeName))
            .Select(fact => fact.Properties["mappedTypeName"]),
            StringComparer.Ordinal);
    }

    private static IReadOnlyList<GeneratedCandidate> LoadGeneratedCandidates(
        string repoPath,
        ScanManifest manifest,
        IReadOnlyList<FileInventoryItem> inventory,
        List<CodeFact> facts)
    {
        var candidates = new List<GeneratedCandidate>();
        foreach (var item in inventory.Where(item => item.Kind == "CSharp" && IsDesigner(item.RelativePath)).OrderBy(item => item.RelativePath, StringComparer.Ordinal))
        {
            if (item.SizeBytes > MaxGeneratedDesignerBytes)
            {
                AddGap(manifest, facts, item.RelativePath, RuleIds.LegacyDataGeneratedLink, "GeneratedDesignerTooLarge", "Generated designer file exceeded the safe parsing size bound.", null);
                continue;
            }

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
                    candidates.Add(new GeneratedCandidate(
                        item.RelativePath,
                        types
                            .GroupBy(type => type.Name, StringComparer.Ordinal)
                            .ToDictionary(
                                group => group.Key,
                                group => (IReadOnlyList<int>)group.Select(type => type.Line).Order().ToArray(),
                                StringComparer.Ordinal)));
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }

        return candidates;
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<CSharpTypeDeclaration>> LoadCSharpTypeDeclarations(
        string repoPath,
        ScanManifest manifest,
        IReadOnlyList<FileInventoryItem> inventory,
        List<CodeFact> facts,
        IReadOnlySet<string> mappedTypeNames)
    {
        var declarations = new SortedDictionary<string, List<CSharpTypeDeclaration>>(StringComparer.Ordinal);
        foreach (var item in inventory
            .Where(item => item.Kind is "CSharp" or "WebFormsCodeBehind" or "WinFormsDesigner")
            .Where(item => IsLikelyMappedTypeDeclarationFile(item.RelativePath, mappedTypeNames))
            .OrderBy(item => item.RelativePath, StringComparer.Ordinal))
        {
            if (item.SizeBytes > MaxGeneratedDesignerBytes)
            {
                AddGap(
                    manifest,
                    facts,
                    item.RelativePath,
                    RuleIds.LegacyDataModelGeneratedLink,
                    "MappedTypeDeclarationFileTooLarge",
                    "C# file exceeded the safe parsing size bound while resolving NHibernate mapped-type links; declarations in this file were not inspected.",
                    null);
                continue;
            }

            var fullPath = Path.Combine(repoPath, item.RelativePath);
            try
            {
                var source = File.ReadAllText(fullPath);
                var tree = CSharpSyntaxTree.ParseText(source, path: item.RelativePath);
                var root = tree.GetCompilationUnitRoot();
                foreach (var type in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
                {
                    var fullName = FullTypeName(type);
                    if (string.IsNullOrWhiteSpace(fullName))
                    {
                        continue;
                    }

                    if (!declarations.TryGetValue(fullName, out var matches))
                    {
                        matches = new List<CSharpTypeDeclaration>();
                        declarations[fullName] = matches;
                    }

                    matches.Add(new CSharpTypeDeclaration(item.RelativePath, fullName, Line(tree, type)));
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
            }
        }

        return declarations.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<CSharpTypeDeclaration>)pair.Value
                .OrderBy(declaration => declaration.FilePath, StringComparer.Ordinal)
                .ThenBy(declaration => declaration.Line)
                .ToArray(),
            StringComparer.Ordinal);
    }

    private static bool IsLikelyMappedTypeDeclarationFile(string relativePath, IReadOnlySet<string> mappedTypeNames)
    {
        var fileName = Path.GetFileNameWithoutExtension(relativePath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        foreach (var mappedTypeName in mappedTypeNames)
        {
            foreach (var candidate in MappedTypeFileNameCandidates(mappedTypeName))
            {
                if (string.Equals(fileName, candidate, StringComparison.Ordinal)
                    || fileName.StartsWith(candidate + ".", StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static IEnumerable<string> MappedTypeFileNameCandidates(string mappedTypeName)
    {
        var typeName = mappedTypeName.Split(',', 2, StringSplitOptions.TrimEntries)[0];
        var parts = typeName
            .Split(new[] { '.', '+' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(part => part.Length > 0)
            .ToArray();
        if (parts.Length == 0)
        {
            yield break;
        }

        yield return parts[^1];
        if (typeName.Contains('+', StringComparison.Ordinal) && parts.Length > 1)
        {
            yield return parts[^2];
        }
    }

    private static string? CSharpTypeLookupName(string? mappedTypeName)
    {
        if (string.IsNullOrWhiteSpace(mappedTypeName))
        {
            return null;
        }

        return mappedTypeName.Trim().Replace('+', '.');
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
            metadataHash = HashMetadataDocument(fullPath);
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

    private static CodeFact AddMetadataInventoryFact(ScanManifest manifest, List<CodeFact> facts, string relativePath, string metadataKind, string metadataHash, string ruleId, XElement? element)
    {
        var fact = FactFactory.Create(
            manifest,
            FactTypes.LegacyDataMetadataDeclared,
            ruleId,
            EvidenceTiers.Tier2Structural,
            element is null ? Evidence(relativePath, 1, $"{relativePath}:{metadataKind}:{metadataHash}") : Evidence(relativePath, element),
            targetSymbol: metadataKind,
            properties: MetadataProperties(metadataKind, metadataHash, "document"));
        facts.Add(fact);
        return fact;
    }

    private static void AddGap(ScanManifest manifest, List<CodeFact> facts, string relativePath, string ruleId, string classification, string message, XObject? node, int? explicitLine = null)
    {
        var line = explicitLine ?? (node is null ? 1 : GetLine(node));
        facts.Add(FactFactory.Create(
            manifest,
            FactTypes.AnalysisGap,
            ruleId,
            EvidenceTiers.Tier4Unknown,
            Evidence(relativePath, line, $"{relativePath}:{line}:{classification}:{message}"),
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["classification"] = classification,
                ["coverage"] = "reduced",
                ["message"] = message
            }));
    }

    private static void AddUnsupportedOrmGap(
        ScanManifest manifest,
        List<CodeFact> facts,
        string relativePath,
        string descriptorFamily,
        string descriptorSignal,
        string classification,
        string message,
        XObject? node,
        int? explicitLine = null)
    {
        var family = NormalizeUnsupportedOrmFamily(descriptorFamily);
        var signal = NormalizeToken(descriptorSignal, "descriptor");
        var line = explicitLine ?? (node is null ? 1 : GetLine(node));
        facts.Add(FactFactory.Create(
            manifest,
            FactTypes.AnalysisGap,
            RuleIds.LegacyDataOrmUnsupported,
            EvidenceTiers.Tier4Unknown,
            Evidence(relativePath, line, $"{relativePath}:{line}:{family}:{signal}:{classification}"),
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["classification"] = classification,
                ["coverage"] = "reduced",
                ["descriptorFamily"] = family,
                ["descriptorSignal"] = signal,
                ["message"] = message,
                ["runtimeProof"] = "False",
                ["unsupportedLegacyOrmDescriptor"] = "True"
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
        return Evidence(relativePath, line, element.ToString(SaveOptions.DisableFormatting));
    }

    private static EvidenceSpan Evidence(string relativePath, int line, string hashSeed)
    {
        return new EvidenceSpan(relativePath, line, line, FactFactory.Hash(hashSeed, 32), ExtractorId, ScannerVersions.LegacyDataExtractor);
    }

    private static string HashMetadataDocument(string fullPath)
    {
        var info = new FileInfo(fullPath);
        if (info.Exists && info.Length > SafeXml.MaxXmlBytes)
        {
            throw new SafeXmlException(SafeXmlFailureKind.TooLarge, "XML metadata exceeds configured size bounds.");
        }

        return FactFactory.Hash(File.ReadAllText(fullPath), 32);
    }

    private static SortedDictionary<string, string> MetadataProperties(string metadataKind, string metadataHash, string descriptorKind)
    {
        return new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["descriptorKind"] = descriptorKind,
            ["metadataHash"] = metadataHash,
            ["metadataFormat"] = LegacyDataModelIdentity.MetadataFormat(metadataKind),
            ["metadataKind"] = metadataKind
        };
    }

    private static void AddModelIdentity(
        SortedDictionary<string, string> properties,
        string metadataKind,
        string modelKind,
        string descriptorRole,
        string relativePath,
        string scope,
        string? displayName,
        string? containerName,
        string sourceMetadataFactId,
        IReadOnlyDictionary<string, string> identityParts,
        string coverageLabel = "full")
    {
        LegacyDataModelIdentity.Apply(
            properties,
            new LegacyDataModelIdentityDescriptor(
                LegacyDataModelIdentity.MetadataFormat(metadataKind),
                modelKind,
                descriptorRole,
                relativePath,
                scope,
                displayName,
                containerName,
                identityParts,
                sourceMetadataFactId,
                coverageLabel));
    }

    private static void AddRelationshipSemantics(
        SortedDictionary<string, string> properties,
        string sourceMetadataFactId,
        string endpointCoverage,
        IEnumerable<string> limitations)
    {
        properties["modelRelationshipKind"] = "relationship";
        properties["modelRelationshipRuleId"] = RuleIds.LegacyDataModelRelationship;
        properties["modelRelationshipEvidenceTier"] = EvidenceTiers.Tier2Structural;
        properties["relationshipEndpointCoverage"] = string.Equals(endpointCoverage, "full", StringComparison.OrdinalIgnoreCase) ? "full" : "unidirectional";
        if (!string.IsNullOrWhiteSpace(sourceMetadataFactId))
        {
            properties["supportingFactIds"] = sourceMetadataFactId.Trim();
        }

        var limitationList = limitations
            .Where(limitation => !string.IsNullOrWhiteSpace(limitation))
            .Select(limitation => limitation.Trim())
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (limitationList.Length > 0)
        {
            properties["limitations"] = string.Join(";", limitationList);
        }
    }

    private static IReadOnlyDictionary<string, string> Parts(params (string Key, string? Value)[] values)
    {
        var parts = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, value) in values)
        {
            if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value))
            {
                parts[key] = value.Trim();
            }
        }

        return parts;
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

        LegacyDataSafeValues.AddSafeOrHash(properties, clearKey, hashKey, value, "hashed-unsafe-value");
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
        return LegacyDataSafeValues.IsSafeIdentifier(value);
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

    private static string? UnsupportedOrmFamilyFromPath(string relativePath)
    {
        var lower = relativePath.ToLowerInvariant();
        if (IsNHibernateHbmPath(relativePath))
        {
            return "NHibernate";
        }

        if (lower.Contains("llblgen", StringComparison.Ordinal)
            || lower.EndsWith(".llblgenproj", StringComparison.Ordinal)
            || lower.EndsWith(".lgp", StringComparison.Ordinal)
            || lower.EndsWith(".lgpx", StringComparison.Ordinal))
        {
            return "LLBLGen";
        }

        if (lower.Contains("subsonic", StringComparison.Ordinal))
        {
            return "SubSonic";
        }

        if (lower.Contains("activerecord", StringComparison.Ordinal))
        {
            return "Castle ActiveRecord";
        }

        if (lower.Contains("mybatis", StringComparison.Ordinal))
        {
            return "MyBatis.NET";
        }

        if (lower.Contains("ibatis", StringComparison.Ordinal)
            || lower.Contains("sqlmap", StringComparison.Ordinal)
            || lower.EndsWith(".sqlmap", StringComparison.Ordinal))
        {
            return "iBATIS.NET";
        }

        return null;
    }

    private static bool IsNHibernateHbmPath(string relativePath)
    {
        return relativePath.EndsWith(".hbm.xml", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeNHibernateMapping(XDocument document)
    {
        return document.Root?.Name.LocalName.Equals("hibernate-mapping", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool IsNHibernateColumnLikeElement(XElement element)
    {
        return element.Name.LocalName is "id" or "version" or "timestamp" or "property";
    }

    private static bool IsNHibernateRelationshipElement(XElement element)
    {
        return element.Name.LocalName is "many-to-one" or "one-to-one" or "set" or "list" or "bag" or "map";
    }

    private static bool IsUnsupportedNHibernateShape(XElement element)
    {
        return element.Name.LocalName is "subclass"
            or "joined-subclass"
            or "union-subclass"
            or "composite-id"
            or "component"
            or "dynamic-component"
            or "filter"
            or "filter-def"
            or "query"
            or "sql-query"
            or "loader"
            or "sql-insert"
            or "sql-update"
            or "sql-delete"
            or "formula";
    }

    private static bool IsNHibernateFormulaOnlyProperty(XElement property)
    {
        return property.Name.LocalName == "property"
            && (property.Attribute("formula") is not null || property.Elements().Any(element => element.Name.LocalName == "formula"))
            && AttributeValue(property, "column") is null
            && property.Elements().All(element => element.Name.LocalName != "column" || AttributeValue(element, "name") is null);
    }

    private static string? NHibernateNullable(XElement property)
    {
        var notNull = AttributeValue(property, "not-null");
        if (!bool.TryParse(notNull, out var parsed))
        {
            return null;
        }

        return parsed ? "False" : "True";
    }

    private static string? NHibernateKeyColumn(XElement relationship)
    {
        var key = relationship.Elements().FirstOrDefault(element => element.Name.LocalName == "key");
        return AttributeValue(key, "column")
            ?? AttributeValue(key?.Elements().FirstOrDefault(element => element.Name.LocalName == "column"), "name");
    }

    private static string? NHibernateMappedTypeName(XElement classElement)
    {
        var className = AttributeValue(classElement, "name");
        if (string.IsNullOrWhiteSpace(className))
        {
            return null;
        }

        className = className.Split(',', 2, StringSplitOptions.TrimEntries)[0];
        if (IsQualifiedTypeName(className))
        {
            return className;
        }

        var namespaceName = AttributeValue(classElement, "namespace")
            ?? classElement.Ancestors().Select(ancestor => AttributeValue(ancestor, "namespace")).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        return !string.IsNullOrWhiteSpace(namespaceName)
            ? $"{namespaceName.Trim()}.{className.Trim()}"
            : null;
    }

    private static string? UnsupportedOrmFamilyFromDocument(XDocument document)
    {
        var rootName = document.Root?.Name.LocalName ?? string.Empty;
        var rootNamespace = document.Root?.Name.NamespaceName ?? string.Empty;
        var text = $"{rootName} {rootNamespace}".ToLowerInvariant();

        if (text.Contains("llblgen", StringComparison.Ordinal))
        {
            return "LLBLGen";
        }

        if (text.Contains("subsonic", StringComparison.Ordinal))
        {
            return "SubSonic";
        }

        if (text.Contains("activerecord", StringComparison.Ordinal))
        {
            return "Castle ActiveRecord";
        }

        if (text.Contains("mybatis", StringComparison.Ordinal))
        {
            return "MyBatis.NET";
        }

        if (text.Contains("ibatis", StringComparison.Ordinal) || text.Contains("sqlmap", StringComparison.Ordinal))
        {
            return "iBATIS.NET";
        }

        return null;
    }

    private static IReadOnlySet<string> UnsupportedOrmFamiliesFromConfig(XDocument document)
    {
        var families = new HashSet<string>(StringComparer.Ordinal);
        var elements = document.Root is null
            ? Enumerable.Empty<XElement>()
            : document.Root.DescendantsAndSelf();
        foreach (var value in elements
            .SelectMany(element => element.Attributes().Select(attribute => attribute.Name.LocalName + " " + attribute.Value).Append(element.Name.LocalName)))
        {
            var normalized = value.ToLowerInvariant();
            if (normalized.Contains("llblgen", StringComparison.Ordinal))
            {
                families.Add("LLBLGen");
            }

            if (normalized.Contains("subsonic", StringComparison.Ordinal))
            {
                families.Add("SubSonic");
            }

            if (normalized.Contains("activerecord", StringComparison.Ordinal))
            {
                families.Add("Castle ActiveRecord");
            }

            if (normalized.Contains("mybatis", StringComparison.Ordinal))
            {
                families.Add("MyBatis.NET");
            }

            if (normalized.Contains("ibatis", StringComparison.Ordinal) || normalized.Contains("sqlmap", StringComparison.Ordinal))
            {
                families.Add("iBATIS.NET");
            }
        }

        return families;
    }

    private static void AddCSharpFamilyIfPresent(SyntaxNode root, string family, IReadOnlyCollection<string> tokens, IDictionary<string, int> families)
    {
        if (families.ContainsKey(family))
        {
            return;
        }

        foreach (var node in root.DescendantNodes())
        {
            if (!IsUnsupportedOrmSyntaxEvidence(node, tokens))
            {
                continue;
            }

            families[family] = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            return;
        }
    }

    private static bool IsUnsupportedOrmSyntaxEvidence(SyntaxNode node, IReadOnlyCollection<string> tokens)
    {
        return node switch
        {
            UsingDirectiveSyntax usingDirective => MentionsUnsupportedOrmToken(usingDirective.Name?.ToString(), tokens),
            AttributeSyntax attribute => MentionsUnsupportedOrmToken(attribute.Name.ToString(), tokens),
            ObjectCreationExpressionSyntax objectCreation => MentionsUnsupportedOrmToken(objectCreation.Type.ToString(), tokens),
            IdentifierNameSyntax identifier => MentionsUnsupportedOrmToken(identifier.Identifier.ValueText, tokens),
            GenericNameSyntax generic => MentionsUnsupportedOrmToken(generic.Identifier.ValueText, tokens),
            QualifiedNameSyntax qualified => MentionsUnsupportedOrmToken(qualified.ToString(), tokens),
            MemberAccessExpressionSyntax memberAccess => MentionsUnsupportedOrmToken(memberAccess.ToString(), tokens),
            _ => false
        };
    }

    private static bool MentionsUnsupportedOrmToken(string? value, IReadOnlyCollection<string> tokens)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        foreach (var segment in Regex.Split(value, @"[^A-Za-z0-9_]+").Where(segment => segment.Length > 0))
        {
            var normalized = segment.EndsWith("Attribute", StringComparison.OrdinalIgnoreCase)
                ? segment[..^"Attribute".Length]
                : segment;
            if (tokens.Any(token => string.Equals(normalized, token, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizeUnsupportedOrmFamily(string family)
    {
        return family switch
        {
            "LLBLGen" => "LLBLGen",
            "SubSonic" => "SubSonic",
            "Castle ActiveRecord" => "Castle ActiveRecord",
            "MyBatis.NET" => "MyBatis.NET",
            "iBATIS.NET" => "iBATIS.NET",
            _ => "UnknownLegacyOrm"
        };
    }

    private static string NormalizeToken(string value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var builder = new StringBuilder(value.Length);
        foreach (var ch in value.Trim())
        {
            builder.Append(char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '-');
        }

        var normalized = builder.ToString().Trim('-');
        while (normalized.Contains("--", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("--", "-", StringComparison.Ordinal);
        }

        return normalized.Length == 0 ? fallback : normalized;
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

    private static string LocalQualifiedName(string? qualifiedName)
    {
        if (string.IsNullOrWhiteSpace(qualifiedName))
        {
            return string.Empty;
        }

        var trimmed = qualifiedName.Trim();
        var colonIndex = trimmed.LastIndexOf(':');
        if (colonIndex >= 0 && colonIndex + 1 < trimmed.Length)
        {
            return trimmed[(colonIndex + 1)..];
        }

        return trimmed;
    }

    private static bool IsQualifiedTypeName(string? typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
        {
            return false;
        }

        var trimmed = typeName.Trim();
        return trimmed.Contains('.', StringComparison.Ordinal)
            && trimmed.All(ch => char.IsLetterOrDigit(ch) || ch is '_' or '.' or '+');
    }

    private static string FullTypeName(TypeDeclarationSyntax type)
    {
        var parts = new List<string>();
        for (SyntaxNode? current = type; current is not null; current = current.Parent)
        {
            switch (current)
            {
                case TypeDeclarationSyntax typeDeclaration:
                    parts.Add(typeDeclaration.Identifier.ValueText);
                    break;
                case BaseNamespaceDeclarationSyntax namespaceDeclaration:
                    // Name.ToString() keeps the full dotted namespace segment for block-scoped and file-scoped namespaces.
                    parts.Add(namespaceDeclaration.Name.ToString());
                    break;
            }
        }

        parts.Reverse();
        return string.Join(".", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
    }

    private static string? LastXPathIdentifier(string? xpath)
    {
        if (string.IsNullOrWhiteSpace(xpath))
        {
            return null;
        }

        var segment = xpath.Split(new[] { '/', ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .LastOrDefault(part => !string.Equals(part, ".", StringComparison.Ordinal));
        if (string.IsNullOrWhiteSpace(segment))
        {
            return null;
        }

        segment = segment.Trim().Trim('.', '@', '[', ']', '\'', '"');
        var colonIndex = segment.LastIndexOf(':');
        if (colonIndex >= 0 && colonIndex + 1 < segment.Length)
        {
            segment = segment[(colonIndex + 1)..];
        }

        var builder = new StringBuilder(segment.Length);
        foreach (var ch in segment)
        {
            if (char.IsLetterOrDigit(ch) || ch is '_' or '.' or '$')
            {
                builder.Append(ch);
            }
        }

        return builder.Length == 0 ? null : builder.ToString();
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

    private static bool IsXsdKeyOrUnique(XElement element)
    {
        return element.Name == Xs + "key" || element.Name == Xs + "unique";
    }

    private static bool IsXsdKeyRef(XElement element)
    {
        return element.Name == Xs + "keyref";
    }

    private static bool IsXsdSelector(XElement element)
    {
        return element.Name == Xs + "selector";
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

    private sealed record ConstraintDefinition(XElement Element, string Name, string Table);

    private sealed record GeneratedCandidate(string FilePath, IReadOnlyDictionary<string, IReadOnlyList<int>> TypeLines)
    {
        public IReadOnlySet<string> TypeNames { get; } = TypeLines.Keys.ToHashSet(StringComparer.Ordinal);
        public string FileName { get; } = Path.GetFileName(FilePath);
        public string FileNameWithoutExtension { get; } = Path.GetFileNameWithoutExtension(FilePath);

        public IReadOnlyList<int> LinesFor(string typeName)
        {
            return TypeLines.GetValueOrDefault(typeName) ?? Array.Empty<int>();
        }
    }

    private sealed record GeneratedTypeMatch(string FilePath, int Line);

    private sealed record CSharpTypeDeclaration(string FilePath, string FullName, int Line);
}
