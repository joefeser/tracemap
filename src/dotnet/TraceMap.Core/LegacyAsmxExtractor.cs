using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TraceMap.Core;

public static partial class LegacyAsmxExtractor
{
    private static readonly HashSet<string> ServiceAttributeNames = new(StringComparer.Ordinal)
    {
        "WebService",
        "WebServiceBinding",
        "ScriptService",
        "SoapDocumentService",
        "SoapRpcService"
    };

    private static readonly HashSet<string> SoapMethodAttributeNames = new(StringComparer.Ordinal)
    {
        "SoapDocumentMethod",
        "SoapRpcMethod"
    };

    public static IReadOnlyList<CodeFact> Extract(string repoPath, ScanManifest manifest, IEnumerable<FileInventoryItem> inventory)
    {
        var facts = new List<CodeFact>();
        var files = inventory.OrderBy(item => item.RelativePath, StringComparer.Ordinal).ToArray();

        foreach (var file in files.Where(item => item.Kind == "AsmxServiceHost"))
        {
            ExtractHost(repoPath, manifest, file, facts);
        }

        foreach (var file in files.Where(item => FileInventory.IsCSharpKind(item.Kind)))
        {
            ExtractCSharp(repoPath, manifest, file, facts);
        }

        foreach (var file in files.Where(item => item.Kind == "AsmxServiceReferenceMetadata"))
        {
            ExtractMetadata(repoPath, manifest, file, facts);
        }

        foreach (var file in files.Where(item => item.Kind == "Config"))
        {
            ExtractConfig(repoPath, manifest, file, facts);
        }

        AddMappings(manifest, facts);
        return facts;
    }

    private static void ExtractHost(string repoPath, ScanManifest manifest, FileInventoryItem file, List<CodeFact> facts)
    {
        var fullPath = Path.Combine(repoPath, file.RelativePath);
        try
        {
            var text = File.ReadAllText(fullPath);
            var directive = WebServiceDirective().Match(text);
            if (!directive.Success)
            {
                facts.Add(Gap(manifest, RuleIds.LegacyAsmxHost, file.RelativePath, 1, "MalformedAsmxDirective", "ASMX host file did not contain a parseable WebService directive."));
                return;
            }

            var directiveLine = LineNumberForOffset(text, directive.Index);
            var attributeMatches = DirectiveAttribute().Matches(directive.Value).ToArray();
            var duplicateAttribute = attributeMatches
                .GroupBy(match => match.Groups["name"].Value, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() > 1)
                .Select(group => group.Key)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(duplicateAttribute))
            {
                facts.Add(Gap(manifest, RuleIds.LegacyAsmxHost, file.RelativePath, directiveLine, "DuplicateAsmxDirectiveAttribute", "ASMX WebService directive contains duplicate attributes; TraceMap did not choose an arbitrary value."));
                return;
            }

            var attributes = attributeMatches
                .ToDictionary(match => match.Groups["name"].Value, match => match.Groups["value"].Value, StringComparer.OrdinalIgnoreCase);
            var serviceClass = SafeCodeName(attributes.GetValueOrDefault("Class", string.Empty));
            var language = SafeIdentifier(attributes.GetValueOrDefault("Language", string.Empty));
            var codeBehind = SafeFileName(attributes.GetValueOrDefault("CodeBehind", string.Empty));
            var codeFile = SafeFileName(attributes.GetValueOrDefault("CodeFile", string.Empty));
            var unsupportedCount = attributes.Keys
                .Count(name => !name.Equals("Class", StringComparison.OrdinalIgnoreCase)
                    && !name.Equals("Language", StringComparison.OrdinalIgnoreCase)
                    && !name.Equals("CodeBehind", StringComparison.OrdinalIgnoreCase)
                    && !name.Equals("CodeFile", StringComparison.OrdinalIgnoreCase));

            var properties = new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["coverageLabel"] = "static-asmx-host",
                ["hostKind"] = "asmx",
                ["sourceKind"] = "webservice-directive",
                ["surfaceKind"] = "asmx-service",
                ["unsupportedAttributeCount"] = unsupportedCount.ToString()
            };
            AddIfPresent(properties, "language", language);
            AddIfPresent(properties, "codeBehindFile", codeBehind);
            AddIfPresent(properties, "codeFile", codeFile);
            AddIfPresent(properties, "serviceClassName", serviceClass);

            facts.Add(FactFactory.Create(
                manifest,
                FactTypes.AsmxHostDeclared,
                RuleIds.LegacyAsmxHost,
                EvidenceTiers.Tier2Structural,
                new EvidenceSpan(file.RelativePath, directiveLine, directiveLine, null, "LegacyAsmxExtractor", ScannerVersions.LegacyAsmxExtractor),
                targetSymbol: string.IsNullOrWhiteSpace(serviceClass) ? null : serviceClass,
                contractElement: string.IsNullOrWhiteSpace(serviceClass) ? null : serviceClass,
                properties: properties));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            facts.Add(Gap(manifest, RuleIds.LegacyAsmxHost, file.RelativePath, 1, "UnavailableAsmxDirective", "Unable to parse ASMX host evidence."));
        }
    }

    private static void ExtractCSharp(string repoPath, ScanManifest manifest, FileInventoryItem file, List<CodeFact> facts)
    {
        var fullPath = Path.Combine(repoPath, file.RelativePath);
        try
        {
            var text = File.ReadAllText(fullPath);
            var tree = CSharpSyntaxTree.ParseText(text, path: file.RelativePath);
            var root = tree.GetCompilationUnitRoot();
            var generatedFile = IsGeneratedSoapReferenceFile(file.RelativePath, text);
            var asmxHostTypes = facts
                .Where(fact => fact.FactType == FactTypes.AsmxHostDeclared && !string.IsNullOrWhiteSpace(fact.TargetSymbol))
                .Select(fact => fact.TargetSymbol!)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var type in root.DescendantNodes().OfType<TypeDeclarationSyntax>().OrderBy(type => GetLine(tree, type)).ThenBy(GetTypeName, StringComparer.Ordinal))
            {
                var typeName = GetTypeName(type);
                var serviceAttributes = MatchedAttributeNames(type.AttributeLists, ServiceAttributeNames).ToArray();
                var hasAsmxHost = asmxHostTypes.Contains(typeName);
                var isAsmxServiceContext = serviceAttributes.Length > 0 || hasAsmxHost;
                if (serviceAttributes.Length > 0)
                {
                    facts.Add(FactFactory.Create(
                        manifest,
                        FactTypes.AsmxServiceClassDeclared,
                        RuleIds.LegacyAsmxService,
                        EvidenceTiers.Tier3SyntaxOrTextual,
                        Span(tree, file.RelativePath, type),
                        sourceSymbol: typeName,
                        targetSymbol: typeName,
                        contractElement: type.Identifier.ValueText,
                        properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["attributeNames"] = string.Join(";", serviceAttributes),
                            ["coverageLabel"] = "syntax-asmx-service",
                            ["reviewReason"] = "Syntax-only ASMX attribute evidence may include aliases or project-defined lookalikes without semantic resolution.",
                            ["serviceClassName"] = typeName,
                            ["surfaceKind"] = "asmx-service",
                            ["typeName"] = type.Identifier.ValueText
                        }));
                }

                var isGeneratedClient = IsSoapHttpClientProtocolType(type);
                if (isGeneratedClient)
                {
                    facts.Add(FactFactory.Create(
                        manifest,
                        FactTypes.AsmxGeneratedClientDeclared,
                        RuleIds.LegacyAsmxClient,
                        EvidenceTiers.Tier3SyntaxOrTextual,
                        Span(tree, file.RelativePath, type),
                        sourceSymbol: typeName,
                        targetSymbol: typeName,
                        properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["clientName"] = type.Identifier.ValueText,
                            ["coverageLabel"] = "syntax-asmx-client",
                            ["matchedBy"] = generatedFile ? "GeneratedWebReferenceFileAndSoapHttpClientProtocol" : "SoapHttpClientProtocol",
                            ["reviewReason"] = "Generated SOAP client evidence is static and does not prove runtime endpoint use.",
                            ["surfaceKind"] = "asmx-client",
                            ["typeName"] = typeName
                        }));
                }

                foreach (var method in type.Members.OfType<MethodDeclarationSyntax>().OrderBy(method => GetLine(tree, method)).ThenBy(method => method.Identifier.ValueText, StringComparer.Ordinal))
                {
                    var methodName = method.Identifier.ValueText;
                    var methodSymbol = $"{typeName}.{methodName}";
                    if (isAsmxServiceContext && HasAttribute(method.AttributeLists, "WebMethod"))
                    {
                        facts.Add(FactFactory.Create(
                            manifest,
                            FactTypes.AsmxOperationDeclared,
                            RuleIds.LegacyAsmxOperation,
                            EvidenceTiers.Tier3SyntaxOrTextual,
                            Span(tree, file.RelativePath, method),
                            sourceSymbol: typeName,
                            targetSymbol: methodSymbol,
                            contractElement: methodName,
                            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                            {
                                ["coverageLabel"] = "syntax-asmx-operation",
                                ["operationName"] = methodName,
                                ["reviewReason"] = "Syntax-only WebMethod evidence may include aliases or project-defined lookalikes without semantic resolution.",
                                ["serviceClassName"] = typeName,
                                ["surfaceKind"] = "asmx-operation"
                            }));
                    }

                    var soapAttributes = method.AttributeLists.SelectMany(list => list.Attributes)
                        .Select(attribute => (Attribute: attribute, Name: MatchedAttributeName(attribute, SoapMethodAttributeNames)))
                        .Where(item => item.Name is not null)
                        .OrderBy(item => item.Name, StringComparer.Ordinal)
                        .ToArray();
                    foreach (var item in soapAttributes)
                    {
                        var properties = new SortedDictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["attributeName"] = item.Name!,
                            ["coverageLabel"] = "syntax-asmx-soap-operation",
                            ["operationName"] = methodName,
                            ["reviewReason"] = "SOAP operation-shape evidence is static and action/binding values are hashed or omitted.",
                            ["serviceClassName"] = typeName,
                            ["surfaceKind"] = "asmx-operation"
                        };
                        AddSafeAttributeArguments(properties, item.Attribute);
                        facts.Add(FactFactory.Create(
                            manifest,
                            FactTypes.AsmxSoapOperationDeclared,
                            RuleIds.LegacyAsmxOperation,
                            EvidenceTiers.Tier3SyntaxOrTextual,
                            Span(tree, file.RelativePath, method),
                            sourceSymbol: typeName,
                            targetSymbol: methodSymbol,
                            contractElement: methodName,
                            properties: properties));
                    }

                    if (isGeneratedClient && IsGeneratedClientOperation(method, soapAttributes.Length > 0))
                    {
                        var clientOperationName = GeneratedClientOperationName(method);
                        facts.Add(FactFactory.Create(
                            manifest,
                            FactTypes.AsmxClientOperationDeclared,
                            RuleIds.LegacyAsmxClient,
                            EvidenceTiers.Tier3SyntaxOrTextual,
                            Span(tree, file.RelativePath, method),
                            sourceSymbol: typeName,
                            targetSymbol: methodSymbol,
                            contractElement: clientOperationName,
                            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                            {
                                ["clientName"] = type.Identifier.ValueText,
                                ["coverageLabel"] = "syntax-asmx-client-operation",
                                ["matchedBy"] = soapAttributes.Length > 0 ? "SoapMethodAttribute" : "SoapHttpClientProtocolInvokeWrapper",
                                ["operationName"] = clientOperationName,
                                ["reviewReason"] = "Generated SOAP client operation evidence is static and does not prove runtime endpoint use.",
                                ["surfaceKind"] = "asmx-client",
                                ["typeName"] = typeName
                            }));
                    }
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            facts.Add(Gap(manifest, RuleIds.LegacyAsmxService, file.RelativePath, 1, "UnavailableAsmxCSharp", "Unable to parse ASMX C# evidence."));
        }
    }

    private static void ExtractMetadata(string repoPath, ScanManifest manifest, FileInventoryItem file, List<CodeFact> facts)
    {
        var extension = Path.GetExtension(file.RelativePath).ToLowerInvariant();
        var metadataKind = extension switch
        {
            ".wsdl" => "Wsdl",
            ".disco" => "Disco",
            ".discomap" => "DiscoMap",
            ".map" => "ProxyMap",
            _ => "Metadata"
        };

        var fullPath = Path.Combine(repoPath, file.RelativePath);
        try
        {
            var metadataHash = SafeFileHash(fullPath);
            var document = LoadSafeXml(fullPath);
            var metadataLine = document.Root is null ? 1 : GetLine(document.Root);
            var properties = new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["coverageLabel"] = "static-asmx-metadata",
                ["metadataFileName"] = Path.GetFileName(file.RelativePath),
                ["metadataHash"] = metadataHash,
                ["metadataKind"] = metadataKind,
                ["serviceReferenceFolder"] = FolderLabel(file.RelativePath),
                ["sourceFormat"] = extension.TrimStart('.'),
                ["surfaceKind"] = "asmx-metadata"
            };
            AddTargetNamespaceHash(properties, document);
            AddExternalImportGap(manifest, file, document, facts);

            facts.Add(FactFactory.Create(
                manifest,
                FactTypes.AsmxProxyMetadataDeclared,
                RuleIds.LegacyAsmxMetadata,
                EvidenceTiers.Tier2Structural,
                new EvidenceSpan(file.RelativePath, metadataLine, metadataLine, null, "LegacyAsmxExtractor", ScannerVersions.LegacyAsmxExtractor),
                targetSymbol: $"{metadataKind}:{Path.GetFileName(file.RelativePath)}",
                properties: properties));

            if (extension.Equals(".wsdl", StringComparison.OrdinalIgnoreCase))
            {
                ExtractWsdlMetadataOperations(manifest, file, document, metadataHash, facts);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or XmlException)
        {
            facts.Add(FactFactory.Create(
                manifest,
                FactTypes.AnalysisGap,
                RuleIds.LegacyAsmxMetadata,
                EvidenceTiers.Tier4Unknown,
                new EvidenceSpan(file.RelativePath, 1, 1, null, "LegacyAsmxExtractor", ScannerVersions.LegacyAsmxExtractor),
                properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["classification"] = "MalformedAsmxMetadata",
                    ["metadataFileName"] = Path.GetFileName(file.RelativePath),
                    ["metadataKind"] = metadataKind,
                    ["message"] = "Unable to parse checked-in ASMX/SOAP metadata with safe XML settings.",
                    ["sourceFormat"] = extension.TrimStart('.')
                }));
        }
    }

    private static void AddExternalImportGap(ScanManifest manifest, FileInventoryItem file, XDocument document, List<CodeFact> facts)
    {
        var externalImportCount = document.Descendants()
            .Where(element => element.Name.LocalName is "import" or "include")
            .Select(element => AttributeValue(element, "location") ?? AttributeValue(element, "schemaLocation") ?? string.Empty)
            .Count(value => !string.IsNullOrWhiteSpace(value));
        if (externalImportCount == 0)
        {
            return;
        }

        facts.Add(FactFactory.Create(
            manifest,
            FactTypes.AnalysisGap,
            RuleIds.LegacyAsmxMetadata,
            EvidenceTiers.Tier4Unknown,
            new EvidenceSpan(file.RelativePath, 1, 1, null, "LegacyAsmxExtractor", ScannerVersions.LegacyAsmxExtractor),
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["classification"] = "ExternalAsmxMetadataImport",
                ["externalImportCount"] = externalImportCount.ToString(),
                ["message"] = "Checked-in ASMX/SOAP metadata references external imports or includes; TraceMap did not fetch or resolve them.",
                ["metadataFileName"] = Path.GetFileName(file.RelativePath)
            }));
    }

    private static void ExtractWsdlMetadataOperations(ScanManifest manifest, FileInventoryItem file, XDocument document, string metadataHash, List<CodeFact> facts)
    {
        var serviceName = document.Descendants()
            .Where(element => element.Name.LocalName == "service")
            .Select(element => SafeIdentifier(AttributeValue(element, "name")))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .OrderBy(value => value, StringComparer.Ordinal)
            .FirstOrDefault() ?? string.Empty;
        var bindingName = document.Descendants()
            .Where(element => element.Name.LocalName == "binding")
            .Select(element => SafeIdentifier(AttributeValue(element, "name")))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .OrderBy(value => value, StringComparer.Ordinal)
            .FirstOrDefault() ?? string.Empty;

        foreach (var portType in document.Descendants()
            .Where(element => element.Name.LocalName == "portType")
            .OrderBy(GetLine)
            .ThenBy(element => AttributeValue(element, "name") ?? string.Empty, StringComparer.Ordinal))
        {
            var portTypeName = SafeIdentifier(AttributeValue(portType, "name"));
            if (string.IsNullOrWhiteSpace(portTypeName))
            {
                continue;
            }

            foreach (var operation in portType.Elements()
                .Where(element => element.Name.LocalName == "operation")
                .OrderBy(GetLine)
                .ThenBy(element => AttributeValue(element, "name") ?? string.Empty, StringComparer.Ordinal))
            {
                var operationName = SafeIdentifier(AttributeValue(operation, "name"));
                if (string.IsNullOrWhiteSpace(operationName))
                {
                    continue;
                }

                var properties = new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["coverageLabel"] = "static-asmx-metadata-operation",
                    ["metadataElement"] = "operation",
                    ["metadataFileName"] = Path.GetFileName(file.RelativePath),
                    ["metadataHash"] = metadataHash,
                    ["metadataKind"] = "Wsdl",
                    ["metadataSourceKind"] = "checked-in-wsdl",
                    ["operationName"] = operationName,
                    ["portTypeName"] = portTypeName,
                    ["serviceReferenceFolder"] = FolderLabel(file.RelativePath),
                    ["sourceFormat"] = "wsdl",
                    ["surfaceKind"] = "asmx-metadata"
                };
                AddIfPresent(properties, "bindingName", bindingName);
                AddIfPresent(properties, "serviceName", serviceName);

                facts.Add(FactFactory.Create(
                    manifest,
                    FactTypes.AsmxProxyMetadataDeclared,
                    RuleIds.LegacyAsmxMetadata,
                    EvidenceTiers.Tier2Structural,
                    new EvidenceSpan(file.RelativePath, GetLine(operation), GetLine(operation), null, "LegacyAsmxExtractor", ScannerVersions.LegacyAsmxExtractor),
                    sourceSymbol: portTypeName,
                    targetSymbol: $"{portTypeName}.{operationName}",
                    contractElement: operationName,
                    properties: properties));
            }
        }
    }

    private static void ExtractConfig(string repoPath, ScanManifest manifest, FileInventoryItem file, List<CodeFact> facts)
    {
        var fullPath = Path.Combine(repoPath, file.RelativePath);
        try
        {
            var document = LoadSafeXml(fullPath);
            foreach (var webServices in document.Descendants().Where(element => element.Name.LocalName == "webServices").OrderBy(GetLine))
            {
                facts.Add(FactFactory.Create(
                    manifest,
                    FactTypes.AsmxConfigDeclared,
                    RuleIds.LegacyAsmxConfig,
                    EvidenceTiers.Tier2Structural,
                    new EvidenceSpan(file.RelativePath, GetLine(webServices), GetLine(webServices), null, "LegacyAsmxExtractor", ScannerVersions.LegacyAsmxExtractor),
                    properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["configKind"] = "system.web/webServices",
                        ["coverageLabel"] = "static-asmx-config",
                        ["sourceFormat"] = "config",
                        ["surfaceKind"] = "asmx-config"
                    }));
            }

            foreach (var add in document.Descendants()
                .Where(element => element.Name.LocalName == "add")
                .Where(IsAppSettingAdd)
                .OrderBy(GetLine)
                .ThenBy(element => AttributeValue(element, "key") ?? string.Empty, StringComparer.Ordinal))
            {
                var key = AttributeValue(add, "key") ?? string.Empty;
                var value = AttributeValue(add, "value") ?? string.Empty;
                if (!LooksLikeAsmxConfigKey(key, value))
                {
                    continue;
                }

                var safeKey = SafeConfigKey(key);
                if (string.IsNullOrWhiteSpace(safeKey))
                {
                    continue;
                }

                var properties = new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["configKind"] = "appSettings",
                    ["configKey"] = safeKey,
                    ["coverageLabel"] = "static-asmx-config",
                    ["sourceFormat"] = "config",
                    ["surfaceKind"] = "asmx-config"
                };
                AddSanitizedValue(properties, value);
                facts.Add(FactFactory.Create(
                    manifest,
                    FactTypes.AsmxConfigDeclared,
                    RuleIds.LegacyAsmxConfig,
                    EvidenceTiers.Tier2Structural,
                    new EvidenceSpan(file.RelativePath, GetLine(add), GetLine(add), null, "LegacyAsmxExtractor", ScannerVersions.LegacyAsmxExtractor),
                    targetSymbol: safeKey,
                    contractElement: safeKey,
                    properties: properties));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or XmlException)
        {
            facts.Add(FactFactory.Create(
                manifest,
                FactTypes.AnalysisGap,
                RuleIds.LegacyAsmxConfig,
                EvidenceTiers.Tier4Unknown,
                new EvidenceSpan(file.RelativePath, 1, 1, null, "LegacyAsmxExtractor", ScannerVersions.LegacyAsmxExtractor),
                properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["classification"] = "MalformedAsmxConfig",
                    ["message"] = "Unable to parse ASMX/SOAP config evidence with safe XML settings."
                }));
        }
    }

    private static void AddMappings(ScanManifest manifest, List<CodeFact> facts)
    {
        var serviceOperations = facts
            .Where(fact => fact.FactType == FactTypes.AsmxOperationDeclared)
            .ToArray();
        var clientOperations = facts
            .Where(fact => fact.FactType == FactTypes.AsmxClientOperationDeclared)
            .ToArray();
        var metadataOperations = facts
            .Where(fact => fact.FactType == FactTypes.AsmxProxyMetadataDeclared
                && fact.Properties.GetValueOrDefault("metadataElement", string.Empty).Equals("operation", StringComparison.Ordinal))
            .ToArray();

        foreach (var client in clientOperations.OrderBy(fact => fact.TargetSymbol, StringComparer.Ordinal))
        {
            var operationName = client.Properties.GetValueOrDefault("operationName", string.Empty);
            if (string.IsNullOrWhiteSpace(operationName))
            {
                continue;
            }

            var operationMatches = serviceOperations
                .Where(fact => fact.Properties.GetValueOrDefault("operationName", string.Empty).Equals(operationName, StringComparison.Ordinal))
                .OrderBy(fact => fact.TargetSymbol, StringComparer.Ordinal)
                .ToArray();
            if (operationMatches.Length > 1)
            {
                facts.Add(FactFactory.Create(
                    manifest,
                    FactTypes.AnalysisGap,
                    RuleIds.LegacyAsmxMapping,
                    EvidenceTiers.Tier4Unknown,
                    client.Evidence,
                    sourceSymbol: client.TargetSymbol,
                    contractElement: operationName,
                    properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["classification"] = "AmbiguousAsmxServiceReferenceMapping",
                        ["message"] = "Multiple ASMX operation candidates matched a generated SOAP client operation; TraceMap did not choose an arbitrary winner.",
                        ["operationCandidateCount"] = operationMatches.Length.ToString(),
                        ["operationName"] = operationName
                    }));
                continue;
            }

            var metadataMatches = metadataOperations
                .Where(fact => fact.Properties.GetValueOrDefault("operationName", string.Empty).Equals(operationName, StringComparison.Ordinal))
                .Where(fact => SameReferenceFolder(client, fact))
                .OrderBy(fact => fact.TargetSymbol, StringComparer.Ordinal)
                .ToArray();
            if (operationMatches.Length == 0 && metadataMatches.Length == 0)
            {
                continue;
            }

            if (operationMatches.Length == 0 && metadataMatches.Length > 1)
            {
                facts.Add(FactFactory.Create(
                    manifest,
                    FactTypes.AnalysisGap,
                    RuleIds.LegacyAsmxMapping,
                    EvidenceTiers.Tier4Unknown,
                    client.Evidence,
                    sourceSymbol: client.TargetSymbol,
                    contractElement: operationName,
                    properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["classification"] = "AmbiguousAsmxMetadataOperationMapping",
                        ["message"] = "Multiple ASMX metadata operation candidates matched a generated SOAP client operation; TraceMap did not choose an arbitrary metadata file.",
                        ["metadataOperationCandidateCount"] = metadataMatches.Length.ToString(),
                        ["operationName"] = operationName
                    }));
                continue;
            }

            var target = operationMatches.FirstOrDefault() ?? metadataMatches.First();
            var supportingFactIds = new[] { client.FactId, target.FactId }
                .Concat(metadataMatches.Select(fact => fact.FactId))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();

            facts.Add(FactFactory.Create(
                manifest,
                FactTypes.AsmxServiceReferenceMapping,
                RuleIds.LegacyAsmxMapping,
                EvidenceTiers.Tier3SyntaxOrTextual,
                client.Evidence,
                sourceSymbol: client.TargetSymbol,
                targetSymbol: target.TargetSymbol,
                contractElement: operationName,
                properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["clientName"] = client.Properties.GetValueOrDefault("clientName", string.Empty),
                    ["coverageLabel"] = "syntax-asmx-mapping",
                    ["mappingKind"] = metadataMatches.Length > 0 ? "metadata-and-operation-name" : "operation-name-only",
                    ["operationName"] = operationName,
                    ["reviewReason"] = "ASMX client-to-service mapping is probable static evidence only and does not prove runtime calls or endpoint reachability.",
                    ["surfaceKind"] = "asmx-client",
                    ["supportingFactIds"] = string.Join(";", supportingFactIds)
                }));
        }
    }

    private static bool SameReferenceFolder(CodeFact client, CodeFact metadata)
    {
        var clientFolder = FolderLabel(client.Evidence.FilePath);
        var metadataFolder = metadata.Properties.GetValueOrDefault("serviceReferenceFolder", string.Empty);
        if (string.IsNullOrWhiteSpace(clientFolder) || string.IsNullOrWhiteSpace(metadataFolder))
        {
            return false;
        }

        return clientFolder.Equals(metadataFolder, StringComparison.Ordinal);
    }

    private static bool IsGeneratedClientOperation(MethodDeclarationSyntax method, bool hasSoapAttribute)
    {
        if (!method.Modifiers.Any(SyntaxKind.PublicKeyword))
        {
            return false;
        }

        return hasSoapAttribute
            || method.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Any(IsSoapHttpClientProtocolInvocation);
    }

    private static string GeneratedClientOperationName(MethodDeclarationSyntax method)
    {
        var methodName = method.Identifier.ValueText;
        var prefixLength = methodName.StartsWith("Begin", StringComparison.Ordinal)
            ? 5
            : methodName.StartsWith("End", StringComparison.Ordinal) ? 3 : 0;
        if (prefixLength == 0 || methodName.Length <= prefixLength)
        {
            return methodName;
        }

        var usesAsyncInvokeWrapper = method.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Any(IsAsyncSoapHttpClientProtocolInvocation);
        return usesAsyncInvokeWrapper ? methodName[prefixLength..] : methodName;
    }

    private static bool IsSoapHttpClientProtocolInvocation(InvocationExpressionSyntax invocation)
    {
        return InvocationName(invocation) is "Invoke" or "BeginInvoke" or "EndInvoke";
    }

    private static bool IsAsyncSoapHttpClientProtocolInvocation(InvocationExpressionSyntax invocation)
    {
        return InvocationName(invocation) is "BeginInvoke" or "EndInvoke";
    }

    private static string InvocationName(InvocationExpressionSyntax invocation)
    {
        return invocation.Expression switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
            _ => string.Empty
        };
    }

    private static bool IsSoapHttpClientProtocolType(TypeDeclarationSyntax type)
    {
        if (type.BaseList is null)
        {
            return false;
        }

        foreach (var baseType in type.BaseList.Types)
        {
            var name = baseType.Type.ToString().Trim();
            if (name.Equals("SoapHttpClientProtocol", StringComparison.Ordinal)
                || name.Equals("System.Web.Services.Protocols.SoapHttpClientProtocol", StringComparison.Ordinal)
                || name.EndsWith(".SoapHttpClientProtocol", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsGeneratedSoapReferenceFile(string relativePath, string text)
    {
        return relativePath.Contains("Web Reference", StringComparison.OrdinalIgnoreCase)
            || relativePath.Contains("WebReference", StringComparison.OrdinalIgnoreCase)
            || Path.GetFileName(relativePath).Equals("Reference.cs", StringComparison.OrdinalIgnoreCase)
            || text.Contains("System.CodeDom.Compiler.GeneratedCodeAttribute", StringComparison.Ordinal)
            || text.Contains("[GeneratedCode", StringComparison.Ordinal);
    }

    private static IEnumerable<string> MatchedAttributeNames(SyntaxList<AttributeListSyntax> attributes, ISet<string> expectedNames)
    {
        return attributes
            .SelectMany(list => list.Attributes)
            .Select(attribute => MatchedAttributeName(attribute, expectedNames))
            .Where(name => name is not null)
            .Select(name => name!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal);
    }

    private static bool HasAttribute(SyntaxList<AttributeListSyntax> attributes, string expectedName)
    {
        return attributes
            .SelectMany(list => list.Attributes)
            .Any(attribute => AttributeNameMatches(attribute, expectedName));
    }

    private static string? MatchedAttributeName(AttributeSyntax attribute, ISet<string> expectedNames)
    {
        return expectedNames.FirstOrDefault(expectedName => AttributeNameMatches(attribute, expectedName));
    }

    private static bool AttributeNameMatches(AttributeSyntax attribute, string expectedName)
    {
        var name = attribute.Name.ToString();
        return name.Equals(expectedName, StringComparison.Ordinal)
            || name.Equals(expectedName + "Attribute", StringComparison.Ordinal)
            || name.EndsWith("." + expectedName, StringComparison.Ordinal)
            || name.EndsWith("." + expectedName + "Attribute", StringComparison.Ordinal);
    }

    private static void AddSafeAttributeArguments(SortedDictionary<string, string> properties, AttributeSyntax attribute)
    {
        if (attribute.ArgumentList is null)
        {
            return;
        }

        foreach (var argument in attribute.ArgumentList.Arguments)
        {
            var key = argument.NameEquals?.Name.Identifier.ValueText
                ?? argument.NameColon?.Name.Identifier.ValueText
                ?? "Action";
            if (key is not ("Action" or "RequestNamespace" or "ResponseNamespace" or "Binding"))
            {
                continue;
            }

            var literal = argument.Expression switch
            {
                LiteralExpressionSyntax literalExpression when literalExpression.IsKind(SyntaxKind.StringLiteralExpression) => literalExpression.Token.ValueText,
                _ => string.Empty
            };
            if (string.IsNullOrWhiteSpace(literal))
            {
                continue;
            }

            if (LooksSecretLike(key) || LooksSecretLike(literal))
            {
                properties[$"{LowerFirst(key)}Omitted"] = "secret-like";
                continue;
            }

            var safeName = SafeIdentifier(literal);
            if (!string.IsNullOrWhiteSpace(safeName) && !literal.Contains('.', StringComparison.Ordinal))
            {
                properties[LowerFirst(key)] = safeName;
            }
            else
            {
                properties[$"{LowerFirst(key)}Hash"] = ContextHash("asmx-soap-attribute", literal);
            }
        }
    }

    private static void AddTargetNamespaceHash(SortedDictionary<string, string> properties, XDocument document)
    {
        var targetNamespace = document.Root?.Attributes()
            .FirstOrDefault(attribute => attribute.Name.LocalName == "targetNamespace")
            ?.Value
            .Trim();
        if (string.IsNullOrWhiteSpace(targetNamespace))
        {
            return;
        }

        if (LooksSecretLike(targetNamespace))
        {
            properties["targetNamespaceOmitted"] = "secret-like";
            return;
        }

        var safe = SafeIdentifier(targetNamespace);
        if (!string.IsNullOrWhiteSpace(safe) && !targetNamespace.Contains('.', StringComparison.Ordinal))
        {
            properties["targetNamespaceToken"] = safe;
            return;
        }

        properties["targetNamespaceHash"] = ContextHash("asmx-target-namespace", targetNamespace);
    }

    private static void AddSanitizedValue(SortedDictionary<string, string> properties, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            properties["valueState"] = "empty";
            return;
        }

        if (LooksSecretLike(value) || LooksConnectionStringLike(value))
        {
            properties["valueOmitted"] = "secret-like";
            return;
        }

        properties["valueHash"] = ContextHash("asmx-config-value", value);
        properties["valueKind"] = Uri.TryCreate(value, UriKind.Absolute, out var uri) ? uri.Scheme : "non-url";
    }

    private static bool IsAppSettingAdd(XElement element)
    {
        return element.Parent?.Name.LocalName == "appSettings";
    }

    private static bool LooksLikeAsmxConfigKey(string key, string value)
    {
        var normalized = ConfigKeySeparator().Replace(key, string.Empty);
        return normalized.Contains("Asmx", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("Soap", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("WebReference", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith("ServiceUrl", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith("ServiceUri", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith("ServiceEndpoint", StringComparison.OrdinalIgnoreCase)
            || (normalized.EndsWith("Url", StringComparison.OrdinalIgnoreCase) && LooksLikeAsmxEndpointValue(value))
            || normalized.EndsWith("WsdlUrl", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeAsmxEndpointValue(string value)
    {
        return value.Contains(".asmx", StringComparison.OrdinalIgnoreCase)
            || value.Contains("?wsdl", StringComparison.OrdinalIgnoreCase)
            || value.EndsWith(".wsdl", StringComparison.OrdinalIgnoreCase);
    }

    private static string SafeCodeName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        return SafeCodeNameRegex().IsMatch(trimmed) ? trimmed : string.Empty;
    }

    private static string SafeIdentifier(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        return SafeIdentifierRegex().IsMatch(trimmed)
            && !trimmed.Contains("://", StringComparison.Ordinal)
            ? trimmed
            : string.Empty;
    }

    private static string SafeConfigKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || LooksSecretLike(value)
            || value.Contains("://", StringComparison.Ordinal)
            || value.Contains('/', StringComparison.Ordinal)
            || value.Contains('\\', StringComparison.Ordinal)
            || value.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        return SafeConfigKeyRegex().IsMatch(trimmed) ? trimmed : string.Empty;
    }

    private static string SafeFileName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || LooksSecretLike(value))
        {
            return string.Empty;
        }

        if (value.Contains("://", StringComparison.Ordinal) || value.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        var normalized = value.Trim().Replace('\\', '/');
        var lastSlash = normalized.LastIndexOf('/');
        var fileName = lastSlash >= 0 ? normalized[(lastSlash + 1)..] : normalized;
        return SafeFileNameRegex().IsMatch(fileName) ? fileName : string.Empty;
    }

    private static bool LooksSecretLike(string value)
    {
        return SecretLikeRegex().IsMatch(value);
    }

    private static bool LooksConnectionStringLike(string value)
    {
        return value.Contains("Password=", StringComparison.OrdinalIgnoreCase)
            || value.Contains("Pwd=", StringComparison.OrdinalIgnoreCase)
            || value.Contains("User ID=", StringComparison.OrdinalIgnoreCase)
            || value.Contains("Initial Catalog=", StringComparison.OrdinalIgnoreCase)
            || value.Contains("Data Source=", StringComparison.OrdinalIgnoreCase)
            || value.Contains("Server=", StringComparison.OrdinalIgnoreCase);
    }

    private static string ContextHash(string context, string value)
    {
        return FactFactory.Hash($"{context}|{value}", 32);
    }

    private static string SafeFileHash(string fullPath)
    {
        using var stream = File.OpenRead(fullPath);
        var bytes = SHA256.HashData(stream);
        var hex = Convert.ToHexString(bytes).ToLowerInvariant();
        return hex[..32];
    }

    private static XDocument LoadSafeXml(string fullPath)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersFromEntities = 1024,
            MaxCharactersInDocument = 10 * 1024 * 1024
        };
        using var stream = File.OpenRead(fullPath);
        using var reader = XmlReader.Create(stream, settings);
        return XDocument.Load(reader, LoadOptions.SetLineInfo);
    }

    private static string GetTypeName(TypeDeclarationSyntax type)
    {
        var parts = new List<string> { type.Identifier.ValueText };
        SyntaxNode? parent = type.Parent;
        while (parent is TypeDeclarationSyntax parentType)
        {
            parts.Add(parentType.Identifier.ValueText);
            parent = parent.Parent;
        }

        parts.Reverse();
        var typeName = string.Join(".", parts);
        var namespaces = type.Ancestors()
            .OfType<BaseNamespaceDeclarationSyntax>()
            .Select(ns => ns.Name.ToString())
            .Reverse()
            .ToArray();
        return namespaces.Length == 0
            ? typeName
            : string.Join(".", namespaces) + "." + typeName;
    }

    private static EvidenceSpan Span(SyntaxTree tree, string relativePath, SyntaxNode node)
    {
        var lineSpan = tree.GetLineSpan(node.Span);
        var start = lineSpan.StartLinePosition.Line + 1;
        var end = lineSpan.EndLinePosition.Line + 1;
        return new EvidenceSpan(relativePath, Math.Max(1, start), Math.Max(1, end), null, "LegacyAsmxExtractor", ScannerVersions.LegacyAsmxExtractor);
    }

    private static int GetLine(SyntaxTree tree, SyntaxNode node)
    {
        return tree.GetLineSpan(node.Span).StartLinePosition.Line + 1;
    }

    private static int GetLine(XObject node)
    {
        return node is IXmlLineInfo lineInfo && lineInfo.HasLineInfo()
            ? Math.Max(1, lineInfo.LineNumber)
            : 1;
    }

    private static int LineNumberForOffset(string text, int offset)
    {
        var line = 1;
        for (var index = 0; index < Math.Min(offset, text.Length); index++)
        {
            if (text[index] == '\n')
            {
                line++;
            }
        }

        return line;
    }

    private static string FolderLabel(string relativePath)
    {
        var directory = FileInventory.NormalizeRelativePath(Path.GetDirectoryName(relativePath) ?? string.Empty);
        return directory is "." ? string.Empty : directory;
    }

    private static string? AttributeValue(XElement element, string name)
    {
        return element.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == name)?.Value.Trim();
    }

    private static void AddIfPresent(SortedDictionary<string, string> properties, string key, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            properties[key] = value;
        }
    }

    private static CodeFact Gap(ScanManifest manifest, string ruleId, string filePath, int line, string classification, string message)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.AnalysisGap,
            ruleId,
            EvidenceTiers.Tier4Unknown,
            new EvidenceSpan(filePath, line, line, null, "LegacyAsmxExtractor", ScannerVersions.LegacyAsmxExtractor),
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["classification"] = classification,
                ["message"] = message
            });
    }

    private static string LowerFirst(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? value
            : char.ToLowerInvariant(value[0]) + value[1..];
    }

    [GeneratedRegex(@"<%@\s*WebService\b[^%]*%>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex WebServiceDirective();

    [GeneratedRegex(@"(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*""(?<value>[^""]*)""", RegexOptions.IgnoreCase)]
    private static partial Regex DirectiveAttribute();

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_.]*$")]
    private static partial Regex SafeCodeNameRegex();

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_.-]*$")]
    private static partial Regex SafeIdentifierRegex();

    [GeneratedRegex(@"^[A-Za-z0-9_.-]{1,160}$")]
    private static partial Regex SafeConfigKeyRegex();

    [GeneratedRegex(@"[\s_.:-]+")]
    private static partial Regex ConfigKeySeparator();

    [GeneratedRegex(@"^[A-Za-z0-9_. -]{1,160}$")]
    private static partial Regex SafeFileNameRegex();

    [GeneratedRegex(@"(?i)(password|passwd|pwd|secret|token|apikey|api_key|clientsecret|client_secret|credential|auth|bearer|sas|sig=|privatekey)")]
    private static partial Regex SecretLikeRegex();
}
