using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TraceMap.Core;

public static partial class LegacyWcfExtractor
{
    public static IReadOnlyList<CodeFact> Extract(string repoPath, ScanManifest manifest, IEnumerable<FileInventoryItem> inventory)
    {
        var facts = new List<CodeFact>();
        var files = inventory.OrderBy(item => item.RelativePath, StringComparer.Ordinal).ToArray();

        foreach (var file in files.Where(item => item.Kind == "Config"))
        {
            ExtractConfig(repoPath, manifest, file, facts);
        }

        foreach (var file in files.Where(item => FileInventory.IsCSharpKind(item.Kind)))
        {
            ExtractCSharp(repoPath, manifest, file, facts);
        }

        foreach (var file in files.Where(item => item.Kind == "ServiceHost"))
        {
            ExtractServiceHost(repoPath, manifest, file, facts);
        }

        foreach (var file in files.Where(item => item.Kind == "ServiceReferenceMetadata"))
        {
            ExtractServiceReferenceMetadata(repoPath, manifest, file, facts);
        }

        AddMappings(manifest, facts);
        return facts;
    }

    private static void ExtractConfig(string repoPath, ScanManifest manifest, FileInventoryItem file, List<CodeFact> facts)
    {
        var fullPath = Path.Combine(repoPath, file.RelativePath);
        try
        {
            var document = XDocument.Load(fullPath, LoadOptions.SetLineInfo);
            foreach (var endpoint in document.Descendants()
                .Where(element => element.Name.LocalName == "endpoint")
                .Where(IsServiceModelEndpoint)
                .OrderBy(GetLine)
                .ThenBy(element => AttributeValue(element, "name") ?? string.Empty, StringComparer.Ordinal))
            {
                var clientAncestor = endpoint.Ancestors().FirstOrDefault(element => element.Name.LocalName == "client");
                var serviceAncestor = endpoint.Ancestors().FirstOrDefault(element => element.Name.LocalName == "service");
                if (clientAncestor is null && serviceAncestor is null)
                {
                    continue;
                }

                var contract = AttributeValue(endpoint, "contract") ?? string.Empty;
                var address = AttributeValue(endpoint, "address") ?? string.Empty;
                var endpointName = AttributeValue(endpoint, "name") ?? string.Empty;
                var binding = AttributeValue(endpoint, "binding") ?? string.Empty;
                var serviceName = serviceAncestor is null ? string.Empty : AttributeValue(serviceAncestor, "name") ?? string.Empty;
                var factType = clientAncestor is not null
                    ? FactTypes.WcfClientEndpointDeclared
                    : FactTypes.WcfServiceEndpointDeclared;
                var target = string.IsNullOrWhiteSpace(contract) ? endpointName : contract;
                var properties = new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["addressHash"] = string.IsNullOrWhiteSpace(address) ? string.Empty : FactFactory.Hash(address, 32),
                    ["addressScheme"] = TryGetAddressScheme(address),
                    ["binding"] = binding,
                    ["contractName"] = contract,
                    ["endpointName"] = endpointName,
                    ["serviceName"] = serviceName,
                    ["sourceFormat"] = "system.serviceModel"
                };
                AddMissing(properties, "missingContract", contract);
                AddMissing(properties, "missingBinding", binding);
                AddMissing(properties, "missingAddress", address);

                facts.Add(FactFactory.Create(
                    manifest,
                    factType,
                    RuleIds.LegacyWcfConfig,
                    EvidenceTiers.Tier2Structural,
                    new EvidenceSpan(file.RelativePath, GetLine(endpoint), GetLine(endpoint), null, "LegacyWcfExtractor", ScannerVersions.LegacyWcfExtractor),
                    targetSymbol: string.IsNullOrWhiteSpace(target) ? null : target,
                    contractElement: string.IsNullOrWhiteSpace(contract) ? null : contract,
                    properties: properties));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or XmlException)
        {
            facts.Add(FactFactory.Create(
                manifest,
                FactTypes.AnalysisGap,
                RuleIds.LegacyWcfConfig,
                EvidenceTiers.Tier4Unknown,
                new EvidenceSpan(file.RelativePath, 1, 1, null, "LegacyWcfExtractor", ScannerVersions.LegacyWcfExtractor),
                properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["message"] = "Unable to parse WCF config evidence."
                }));
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
            var generatedFile = IsGeneratedServiceReferenceFile(file.RelativePath, text);

            foreach (var type in root.DescendantNodes().OfType<TypeDeclarationSyntax>().OrderBy(type => GetLine(tree, type)).ThenBy(GetTypeName, StringComparer.Ordinal))
            {
                var typeName = GetTypeName(type);
                if (HasAttribute(type.AttributeLists, "ServiceContract"))
                {
                    facts.Add(FactFactory.Create(
                        manifest,
                        FactTypes.WcfServiceContractDeclared,
                            RuleIds.LegacyWcfContract,
                        EvidenceTiers.Tier3SyntaxOrTextual,
                        Span(tree, file.RelativePath, type),
                        sourceSymbol: typeName,
                        targetSymbol: typeName,
                        contractElement: type.Identifier.ValueText,
                        properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["contractName"] = typeName,
                            ["typeName"] = type.Identifier.ValueText
                        }));
                }

                var clientContractName = GetClientBaseContractName(type);
                var isGeneratedClient = !string.IsNullOrWhiteSpace(clientContractName);
                if (isGeneratedClient)
                {
                    facts.Add(FactFactory.Create(
                                manifest,
                                FactTypes.WcfGeneratedClientDeclared,
                                RuleIds.LegacyWcfContract,
                                EvidenceTiers.Tier2Structural,
                        Span(tree, file.RelativePath, type),
                        sourceSymbol: typeName,
                        targetSymbol: typeName,
                        properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["clientContractName"] = clientContractName,
                            ["clientName"] = type.Identifier.ValueText,
                            ["matchedBy"] = generatedFile ? "GeneratedServiceReferenceFile" : "ClientBaseOrClientSuffix",
                            ["typeName"] = typeName
                        }));
                }

                foreach (var method in type.Members.OfType<MethodDeclarationSyntax>().OrderBy(method => GetLine(tree, method)).ThenBy(method => method.Identifier.ValueText, StringComparer.Ordinal))
                {
                    var methodName = method.Identifier.ValueText;
                    var methodSymbol = $"{typeName}.{methodName}";
                    if (HasAttribute(method.AttributeLists, "OperationContract"))
                    {
                        facts.Add(FactFactory.Create(
                            manifest,
                            FactTypes.WcfOperationContractDeclared,
                            RuleIds.LegacyWcfContract,
                            EvidenceTiers.Tier3SyntaxOrTextual,
                            Span(tree, file.RelativePath, method),
                            sourceSymbol: typeName,
                            targetSymbol: methodSymbol,
                            contractElement: methodName,
                            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                            {
                                ["contractName"] = typeName,
                                ["operationName"] = methodName
                            }));
                    }

                    if (isGeneratedClient && method.Modifiers.Any(SyntaxKind.PublicKeyword))
                    {
                        facts.Add(FactFactory.Create(
                            manifest,
                            FactTypes.WcfGeneratedClientDeclared,
                        RuleIds.LegacyWcfContract,
                            EvidenceTiers.Tier2Structural,
                            Span(tree, file.RelativePath, method),
                            sourceSymbol: typeName,
                            targetSymbol: methodSymbol,
                            contractElement: methodName,
                            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                            {
                                ["clientContractName"] = clientContractName,
                                ["clientName"] = type.Identifier.ValueText,
                                ["operationName"] = methodName,
                                ["typeName"] = typeName
                            }));
                    }
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            facts.Add(FactFactory.Create(
                manifest,
                FactTypes.AnalysisGap,
                RuleIds.LegacyWcfContract,
                EvidenceTiers.Tier4Unknown,
                new EvidenceSpan(file.RelativePath, 1, 1, null, "LegacyWcfExtractor", ScannerVersions.LegacyWcfExtractor),
                properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["message"] = "Unable to parse WCF C# evidence."
                }));
        }
    }

    private static void ExtractServiceHost(string repoPath, ScanManifest manifest, FileInventoryItem file, List<CodeFact> facts)
    {
        var fullPath = Path.Combine(repoPath, file.RelativePath);
        try
        {
            var text = File.ReadAllText(fullPath);
            var directive = ServiceHostDirective().Match(text);
            if (!directive.Success)
            {
                facts.Add(FactFactory.Create(
                    manifest,
                    FactTypes.AnalysisGap,
                    RuleIds.LegacyWcfHost,
                    EvidenceTiers.Tier4Unknown,
                    new EvidenceSpan(file.RelativePath, 1, 1, null, "LegacyWcfExtractor", ScannerVersions.LegacyWcfExtractor),
                    properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["message"] = "Service host file did not contain a parseable directive."
                    }));
                return;
            }

            var attributes = DirectiveAttribute().Matches(directive.Value)
                .ToDictionary(match => match.Groups["name"].Value, match => match.Groups["value"].Value, StringComparer.OrdinalIgnoreCase);
            var service = attributes.GetValueOrDefault("Service", string.Empty);
            if (string.IsNullOrWhiteSpace(service))
            {
                service = attributes.GetValueOrDefault("Class", string.Empty);
            }
            var factory = attributes.GetValueOrDefault("Factory", string.Empty);
            facts.Add(FactFactory.Create(
                manifest,
                FactTypes.WcfServiceHostDeclared,
                RuleIds.LegacyWcfHost,
                EvidenceTiers.Tier2Structural,
                new EvidenceSpan(file.RelativePath, 1, 1, null, "LegacyWcfExtractor", ScannerVersions.LegacyWcfExtractor),
                targetSymbol: string.IsNullOrWhiteSpace(service) ? null : service,
                contractElement: string.IsNullOrWhiteSpace(service) ? null : service,
                properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["factoryName"] = factory,
                    ["hostKind"] = Path.GetExtension(file.RelativePath).TrimStart('.').ToLowerInvariant(),
                    ["serviceName"] = service
                }));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            facts.Add(FactFactory.Create(
                manifest,
                FactTypes.AnalysisGap,
                RuleIds.LegacyWcfHost,
                EvidenceTiers.Tier4Unknown,
                new EvidenceSpan(file.RelativePath, 1, 1, null, "LegacyWcfExtractor", ScannerVersions.LegacyWcfExtractor),
                properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["message"] = "Unable to parse WCF service host evidence."
                }));
        }
    }

    private static void ExtractServiceReferenceMetadata(string repoPath, ScanManifest manifest, FileInventoryItem file, List<CodeFact> facts)
    {
        var fullPath = Path.Combine(repoPath, file.RelativePath);
        var extension = Path.GetExtension(file.RelativePath).ToLowerInvariant();
        var metadataKind = extension switch
        {
            ".svcmap" => "SvcMap",
            ".wsdl" => "Wsdl",
            ".disco" => "Disco",
            ".xsd" => "Schema",
            _ => "Metadata"
        };
        var sourceFormat = extension.TrimStart('.');
        var folder = ServiceReferenceFolderLabel(file.RelativePath);
        try
        {
            var metadataHash = SafeFileHash(fullPath);
            var document = LoadSafeXml(fullPath);
            var isSvcMap = extension.Equals(".svcmap", StringComparison.OrdinalIgnoreCase);
            var localMetadataFileNames = isSvcMap
                ? SafeMetadataBasenames(document)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray()
                : Array.Empty<string>();
            var generatedCodeFileName = isSvcMap
                ? SafeGeneratedCodeBasenames(document)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .FirstOrDefault() ?? string.Empty
                : string.Empty;
            var remoteSourceHash = isSvcMap
                ? UrlLikeValues(document)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .Select(value => FactFactory.Hash(value, 32))
                    .FirstOrDefault() ?? string.Empty
                : string.Empty;

            var properties = new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["metadataFileName"] = Path.GetFileName(file.RelativePath),
                ["metadataHash"] = metadataHash,
                ["metadataKind"] = metadataKind,
                ["serviceReferenceFolder"] = folder,
                ["sourceFormat"] = sourceFormat
            };
            if (!string.IsNullOrWhiteSpace(generatedCodeFileName))
            {
                properties["generatedCodeFileName"] = generatedCodeFileName;
            }
            if (localMetadataFileNames.Length > 0)
            {
                properties["localMetadataFileNames"] = string.Join(";", localMetadataFileNames);
            }
            if (!string.IsNullOrWhiteSpace(remoteSourceHash))
            {
                properties["metadataSourceHash"] = remoteSourceHash;
            }

            var metadataLine = document.Root is null ? 1 : GetLine(document.Root);
            facts.Add(FactFactory.Create(
                manifest,
                FactTypes.WcfServiceReferenceMetadataDeclared,
                RuleIds.LegacyWcfMetadata,
                EvidenceTiers.Tier2Structural,
                new EvidenceSpan(file.RelativePath, metadataLine, metadataLine, null, "LegacyWcfExtractor", ScannerVersions.LegacyWcfExtractor),
                targetSymbol: $"{metadataKind}:{Path.GetFileName(file.RelativePath)}",
                properties: properties));

            if (extension.Equals(".wsdl", StringComparison.OrdinalIgnoreCase))
            {
                ExtractWsdlOperations(manifest, file, document, metadataHash, folder, facts);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or XmlException)
        {
            facts.Add(FactFactory.Create(
                manifest,
                FactTypes.AnalysisGap,
                RuleIds.LegacyWcfMetadata,
                EvidenceTiers.Tier4Unknown,
                new EvidenceSpan(file.RelativePath, 1, 1, null, "LegacyWcfExtractor", ScannerVersions.LegacyWcfExtractor),
                properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["classification"] = "MalformedWcfMetadata",
                    ["metadataFileName"] = Path.GetFileName(file.RelativePath),
                    ["metadataKind"] = metadataKind,
                    ["message"] = "Unable to parse checked-in WCF service-reference metadata with safe XML settings.",
                    ["sourceFormat"] = sourceFormat
                }));
        }
    }

    private static void ExtractWsdlOperations(
        ScanManifest manifest,
        FileInventoryItem file,
        XDocument document,
        string metadataHash,
        string folder,
        List<CodeFact> facts)
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
        var portName = document.Descendants()
            .Where(element => element.Name.LocalName == "port")
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
                    ["contractName"] = portTypeName,
                    ["metadataFileName"] = Path.GetFileName(file.RelativePath),
                    ["metadataHash"] = metadataHash,
                    ["metadataSourceKind"] = "checked-in-wsdl",
                    ["operationName"] = operationName,
                    ["portTypeName"] = portTypeName,
                    ["serviceReferenceFolder"] = folder,
                    ["sourceFormat"] = "wsdl"
                };
                if (!string.IsNullOrWhiteSpace(serviceName))
                {
                    properties["serviceName"] = serviceName;
                }
                if (!string.IsNullOrWhiteSpace(bindingName))
                {
                    properties["bindingName"] = bindingName;
                }
                if (!string.IsNullOrWhiteSpace(portName))
                {
                    properties["portName"] = portName;
                }

                facts.Add(FactFactory.Create(
                    manifest,
                    FactTypes.WcfMetadataOperationDeclared,
                    RuleIds.LegacyWcfMetadata,
                    EvidenceTiers.Tier2Structural,
                    new EvidenceSpan(file.RelativePath, GetLine(operation), GetLine(operation), null, "LegacyWcfExtractor", ScannerVersions.LegacyWcfExtractor),
                    sourceSymbol: portTypeName,
                    targetSymbol: $"{portTypeName}.{operationName}",
                    contractElement: operationName,
                    properties: properties));
            }
        }
    }

    private static void AddMappings(ScanManifest manifest, List<CodeFact> facts)
    {
        var endpoints = facts
            .Where(fact => fact.FactType is FactTypes.WcfClientEndpointDeclared or FactTypes.WcfServiceEndpointDeclared)
            .ToArray();
        var operations = facts
            .Where(fact => fact.FactType == FactTypes.WcfOperationContractDeclared)
            .ToArray();
        var generatedMethods = facts
            .Where(fact => fact.FactType == FactTypes.WcfGeneratedClientDeclared
                && fact.Properties.ContainsKey("operationName"))
            .ToArray();
        var hosts = facts
            .Where(fact => fact.FactType == FactTypes.WcfServiceHostDeclared)
            .ToArray();
        var metadataOperations = facts
            .Where(fact => fact.FactType == FactTypes.WcfMetadataOperationDeclared)
            .ToArray();
        var operationCandidates = BuildOperationCandidates(operations)
            .GroupBy(candidate => $"{candidate.ContractName}|{candidate.NormalizedOperationName}", StringComparer.Ordinal)
            .Select(group => group.OrderBy(candidate => candidate.Rank).ThenBy(candidate => candidate.Fact.TargetSymbol, StringComparer.Ordinal).First())
            .OrderBy(candidate => candidate.ContractName, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.NormalizedOperationName, StringComparer.Ordinal)
            .ToArray();
        var clientCandidates = BuildClientCandidates(generatedMethods, operationCandidates, metadataOperations, manifest, facts)
            .OrderBy(candidate => candidate.Rank)
            .ThenBy(candidate => candidate.Fact.TargetSymbol, StringComparer.Ordinal)
            .ThenBy(candidate => candidate.NormalizedOperationName, StringComparer.Ordinal)
            .ToArray();
        var emittedLogicalMappings = new HashSet<string>(StringComparer.Ordinal);

        foreach (var client in generatedMethods.Where(fact => string.IsNullOrWhiteSpace(fact.Properties.GetValueOrDefault("clientContractName", string.Empty))).OrderBy(fact => fact.TargetSymbol, StringComparer.Ordinal))
        {
            var operationName = client.Properties.GetValueOrDefault("operationName", string.Empty);
            if (!string.IsNullOrWhiteSpace(operationName))
            {
                facts.Add(FactFactory.Create(
                    manifest,
                    FactTypes.AnalysisGap,
                    RuleIds.LegacyWcfMapping,
                    EvidenceTiers.Tier4Unknown,
                    client.Evidence,
                    sourceSymbol: client.TargetSymbol,
                    contractElement: operationName,
                    properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["classification"] = "MissingWcfClientContract",
                        ["message"] = "Generated WCF client did not expose a parseable ClientBase<TContract> contract.",
                        ["operationName"] = operationName
                    }));
            }
        }

        foreach (var candidate in clientCandidates)
        {
            var client = candidate.Fact;
            var clientContractName = client.Properties.GetValueOrDefault("clientContractName", string.Empty);
            var matchingOperations = operationCandidates
                .Where(operation => operation.NormalizedOperationName.Equals(candidate.NormalizedOperationName, StringComparison.Ordinal))
                .Where(operation => NamesAlign(clientContractName, operation.ContractName))
                .OrderBy(operation => operation.Rank)
                .ThenBy(operation => operation.Fact.TargetSymbol, StringComparer.Ordinal)
                .ToArray();
            if (matchingOperations.Length == 0)
            {
                continue;
            }

            var connectedMetadata = ConnectedMetadataOperations(metadataOperations, client, candidate.NormalizedOperationName)
                .OrderBy(fact => fact.TargetSymbol, StringComparer.Ordinal)
                .ToArray();
            if (candidate.RequiresMetadata && connectedMetadata.Length == 0)
            {
                facts.Add(FactFactory.Create(
                    manifest,
                    FactTypes.AnalysisGap,
                    RuleIds.LegacyWcfOperationNormalization,
                    EvidenceTiers.Tier4Unknown,
                    client.Evidence,
                    sourceSymbol: client.TargetSymbol,
                    contractElement: candidate.OriginalOperationName,
                    properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["classification"] = "MissingLocalWcfMetadata",
                        ["clientContractName"] = clientContractName,
                        ["message"] = "A normalized WCF client operation required connected checked-in WSDL metadata, but no local metadata operation was linked to the generated client.",
                        ["normalizedOperationName"] = candidate.NormalizedOperationName,
                        ["originalOperationName"] = candidate.OriginalOperationName
                    }));
                continue;
            }

            var connectedMetadataHashes = connectedMetadata
                .Select(MetadataHash)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            var connectedMetadataContracts = connectedMetadata
                .Select(fact => fact.Properties.GetValueOrDefault("contractName", string.Empty))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            if (candidate.RequiresMetadata
                && (connectedMetadataContracts.Length > 1 || connectedMetadataHashes.Length > 1))
            {
                facts.Add(FactFactory.Create(
                    manifest,
                    FactTypes.AnalysisGap,
                    RuleIds.LegacyWcfMapping,
                    EvidenceTiers.Tier4Unknown,
                    client.Evidence,
                    sourceSymbol: client.TargetSymbol,
                    contractElement: candidate.OriginalOperationName,
                    properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["classification"] = "AmbiguousWcfMetadataContractMapping",
                        ["clientContractName"] = clientContractName,
                        ["metadataHashCount"] = connectedMetadataHashes.Length.ToString(),
                        ["message"] = "Multiple connected WCF metadata contracts or metadata identities matched the normalized operation; TraceMap did not choose an arbitrary winner.",
                        ["normalizedOperationName"] = candidate.NormalizedOperationName,
                        ["originalOperationName"] = candidate.OriginalOperationName,
                        ["metadataContractCount"] = connectedMetadataContracts.Length.ToString()
                    }));
                continue;
            }

            foreach (var operation in matchingOperations)
            {
                var contractName = operation.ContractName;
                var matchingEndpoints = endpoints
                    .Where(endpoint => NamesAlign(endpoint.Properties.GetValueOrDefault("contractName", string.Empty), contractName))
                    .OrderBy(endpoint => endpoint.TargetSymbol, StringComparer.Ordinal)
                    .ToArray();
                var matchingHosts = hosts
                    .Where(host => HostMatchesContract(host.Properties.GetValueOrDefault("serviceName", string.Empty), contractName))
                    .OrderBy(host => host.TargetSymbol, StringComparer.Ordinal)
                    .ToArray();
                var ambiguous = matchingEndpoints.Length > 1 || matchingHosts.Length > 1 || matchingOperations.Length > 1;
                if (endpoints.Length > 0 && matchingEndpoints.Length == 0)
                {
                    continue;
                }

                var tier = matchingEndpoints.Length > 0
                    ? EvidenceTiers.Tier2Structural
                    : EvidenceTiers.Tier3SyntaxOrTextual;
                var mappingKind = matchingEndpoints.Length > 0
                    ? candidate.ConfigMappingKind
                    : candidate.NoEndpointMappingKind;
                var metadataHash = candidate.MetadataHash ?? string.Empty;
                var logicalKey = string.Join("|", clientContractName, contractName, candidate.NormalizedOperationName);
                if (!emittedLogicalMappings.Add(logicalKey))
                {
                    continue;
                }

                var properties = new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["clientName"] = client.Properties.GetValueOrDefault("clientName", string.Empty),
                    ["clientContractName"] = clientContractName,
                    ["contractName"] = contractName,
                    ["endpointCount"] = matchingEndpoints.Length.ToString(),
                    ["hostCount"] = matchingHosts.Length.ToString(),
                    ["mappingKind"] = mappingKind,
                    ["operationName"] = candidate.NormalizedOperationName,
                    ["originalOperationName"] = candidate.OriginalOperationName
                };
                if (!candidate.NormalizationKind.Equals("ExactOriginal", StringComparison.Ordinal))
                {
                    properties["normalizationKind"] = candidate.NormalizationKind;
                    properties["normalizedOperationName"] = candidate.NormalizedOperationName;
                }
                if (!string.IsNullOrWhiteSpace(metadataHash))
                {
                    properties["metadataHash"] = metadataHash;
                }
                var metadataSupportFacts = string.IsNullOrWhiteSpace(metadataHash)
                    ? Array.Empty<CodeFact>()
                    : connectedMetadata;
                var supportingFactIds = new[] { client.FactId, operation.Fact.FactId }
                    .Concat(operation.SupportingFactIds)
                    .Concat(metadataSupportFacts.Select(fact => fact.FactId))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray();
                if (supportingFactIds.Length > 0)
                {
                    properties["supportingFactIds"] = string.Join(";", supportingFactIds);
                }
                if (matchingEndpoints.Length == 0)
                {
                    properties["reviewReason"] = "No WCF endpoint config evidence found; operation-name-only mapping.";
                }
                if (ambiguous)
                {
                    facts.Add(FactFactory.Create(
                        manifest,
                        FactTypes.AnalysisGap,
                        RuleIds.LegacyWcfMapping,
                        EvidenceTiers.Tier4Unknown,
                        client.Evidence,
                        sourceSymbol: client.TargetSymbol,
                        targetSymbol: operation.Fact.TargetSymbol,
                        contractElement: candidate.NormalizedOperationName,
                        properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["classification"] = candidate.NormalizationKind.Equals("ExactOriginal", StringComparison.Ordinal)
                                ? "AmbiguousWcfServiceReferenceMapping"
                                : "AmbiguousWcfNormalizedMapping",
                            ["endpointCount"] = matchingEndpoints.Length.ToString(),
                            ["hostCount"] = matchingHosts.Length.ToString(),
                            ["message"] = "Multiple WCF mapping candidates matched; TraceMap did not choose an arbitrary winner.",
                            ["operationCandidateCount"] = matchingOperations.Length.ToString(),
                            ["normalizedOperationName"] = candidate.NormalizedOperationName,
                            ["operationName"] = candidate.NormalizedOperationName,
                            ["originalOperationName"] = candidate.OriginalOperationName
                        }));
                }

                if (ambiguous)
                {
                    continue;
                }

                facts.Add(FactFactory.Create(
                    manifest,
                    FactTypes.WcfServiceReferenceMapping,
                    RuleIds.LegacyWcfMapping,
                    tier,
                    client.Evidence,
                    sourceSymbol: client.TargetSymbol,
                    targetSymbol: operation.Fact.TargetSymbol,
                    contractElement: candidate.NormalizedOperationName,
                    properties: properties));
            }
        }
    }

    private sealed record ClientOperationCandidate(
        CodeFact Fact,
        string OriginalOperationName,
        string NormalizedOperationName,
        string NormalizationKind,
        int Rank,
        bool RequiresMetadata,
        string? MetadataHash,
        string ConfigMappingKind,
        string NoEndpointMappingKind);

    private sealed record ContractOperationCandidate(
        CodeFact Fact,
        string OriginalOperationName,
        string NormalizedOperationName,
        string NormalizationKind,
        string ContractName,
        int Rank,
        IReadOnlyList<string> SupportingFactIds);

    private static IReadOnlyList<ClientOperationCandidate> BuildClientCandidates(
        IReadOnlyList<CodeFact> generatedMethods,
        IReadOnlyList<ContractOperationCandidate> operationCandidates,
        IReadOnlyList<CodeFact> metadataOperations,
        ScanManifest manifest,
        List<CodeFact> facts)
    {
        var candidates = new List<ClientOperationCandidate>();
        foreach (var client in generatedMethods.OrderBy(fact => fact.TargetSymbol, StringComparer.Ordinal))
        {
            var original = client.Properties.GetValueOrDefault("operationName", string.Empty);
            var clientContractName = client.Properties.GetValueOrDefault("clientContractName", string.Empty);
            if (string.IsNullOrWhiteSpace(original) || string.IsNullOrWhiteSpace(clientContractName))
            {
                continue;
            }

            var exactMetadata = ConnectedMetadataOperations(metadataOperations, client, original).ToArray();
            if (!IsLifecycleOperation(original) || exactMetadata.Length > 0)
            {
                candidates.Add(new ClientOperationCandidate(
                    client,
                    original,
                    original,
                    "ExactOriginal",
                    1,
                    false,
                    null,
                    "config-contract-and-operation-name",
                    "operation-name-only"));
            }

            if (TryAsyncBaseName(original, out var asyncBaseName) && !IsLifecycleOperation(original) && !IsLifecycleOperation(asyncBaseName))
            {
                var connectedMetadata = ConnectedMetadataOperations(metadataOperations, client, asyncBaseName).ToArray();
                if (connectedMetadata.Length > 0)
                {
                    candidates.Add(new ClientOperationCandidate(
                        client,
                        original,
                        asyncBaseName,
                        "AsyncSuffix",
                        2,
                        true,
                        connectedMetadata.Select(MetadataHash).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)),
                        "config-contract-metadata-normalized-async",
                        "metadata-normalized-async"));
                }
                else if (HasSameContractGeneratedSibling(generatedMethods, client, asyncBaseName)
                    || HasAlignedOperation(operationCandidates, clientContractName, asyncBaseName))
                {
                    candidates.Add(new ClientOperationCandidate(
                        client,
                        original,
                        asyncBaseName,
                        "GeneratedCodeOnlyAlias",
                        4,
                        false,
                        null,
                        "config-contract-generated-alias",
                        "generated-code-only-alias"));
                }
                else
                {
                    AddMetadataLinkGap(manifest, facts, metadataOperations, client, asyncBaseName);
                }
            }

            if (TryApmBaseName(original, out var apmBaseName)
                && !IsLifecycleOperation(original)
                && !IsLifecycleOperation(apmBaseName)
                && HasApmPair(generatedMethods, client, apmBaseName))
            {
                var connectedMetadata = ConnectedMetadataOperations(metadataOperations, client, apmBaseName).ToArray();
                candidates.Add(new ClientOperationCandidate(
                    client,
                    original,
                    apmBaseName,
                    "ApmBeginEndPair",
                    3,
                    false,
                    connectedMetadata.Select(MetadataHash).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)),
                    "config-contract-apm-normalized",
                    "apm-normalized-operation"));
            }
        }

        return candidates
            .GroupBy(candidate => $"{candidate.Fact.FactId}|{candidate.NormalizedOperationName}|{candidate.NormalizationKind}", StringComparer.Ordinal)
            .Select(group => group.OrderBy(candidate => candidate.Rank).First())
            .ToArray();
    }

    private static IReadOnlyList<ContractOperationCandidate> BuildOperationCandidates(IReadOnlyList<CodeFact> operations)
    {
        var candidates = new List<ContractOperationCandidate>();
        foreach (var operation in operations.OrderBy(fact => fact.TargetSymbol, StringComparer.Ordinal))
        {
            var operationName = operation.Properties.GetValueOrDefault("operationName", string.Empty);
            var contractName = operation.Properties.GetValueOrDefault("contractName", string.Empty);
            if (string.IsNullOrWhiteSpace(operationName) || string.IsNullOrWhiteSpace(contractName))
            {
                continue;
            }

            candidates.Add(new ContractOperationCandidate(
                operation,
                operationName,
                operationName,
                "ExactOriginal",
                contractName,
                1,
                Array.Empty<string>()));
        }

        foreach (var group in operations
            .GroupBy(operation => operation.Properties.GetValueOrDefault("contractName", string.Empty), StringComparer.Ordinal)
            .Where(group => !string.IsNullOrWhiteSpace(group.Key)))
        {
            var byName = group
                .GroupBy(operation => operation.Properties.GetValueOrDefault("operationName", string.Empty), StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.OrderBy(fact => fact.TargetSymbol, StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);
            foreach (var beginName in byName.Keys.Where(name => name.StartsWith("Begin", StringComparison.Ordinal) && name.Length > "Begin".Length).OrderBy(name => name, StringComparer.Ordinal))
            {
                var baseName = beginName["Begin".Length..];
                if (IsLifecycleOperation(baseName))
                {
                    continue;
                }

                var endName = "End" + baseName;
                if (!byName.TryGetValue(endName, out var endFacts))
                {
                    continue;
                }

                var beginFact = byName[beginName][0];
                candidates.Add(new ContractOperationCandidate(
                    beginFact,
                    beginName,
                    baseName,
                    "ApmBeginEndPair",
                    group.Key,
                    3,
                    byName[beginName].Concat(endFacts).Select(fact => fact.FactId).OrderBy(value => value, StringComparer.Ordinal).ToArray()));
            }
        }

        return candidates;
    }

    private static IEnumerable<CodeFact> ConnectedMetadataOperations(IReadOnlyList<CodeFact> metadataOperations, CodeFact client, string operationName)
    {
        var clientContractName = client.Properties.GetValueOrDefault("clientContractName", string.Empty);
        var clientFolder = ServiceReferenceFolderLabel(client.Evidence.FilePath);
        return metadataOperations
            .Where(fact => fact.Properties.GetValueOrDefault("operationName", string.Empty).Equals(operationName, StringComparison.Ordinal))
            .Where(fact =>
            {
                var metadataFolder = fact.Properties.GetValueOrDefault("serviceReferenceFolder", string.Empty);
                if (!string.IsNullOrWhiteSpace(clientFolder)
                    && !string.IsNullOrWhiteSpace(metadataFolder)
                    && metadataFolder.Equals(clientFolder, StringComparison.Ordinal))
                {
                    return true;
                }

                var portTypeName = fact.Properties.GetValueOrDefault("portTypeName", string.Empty);
                var contractName = fact.Properties.GetValueOrDefault("contractName", string.Empty);
                return NamesAlign(clientContractName, portTypeName)
                    || NamesAlign(clientContractName, contractName)
                    || StripInterfacePrefix(SimpleName(clientContractName)).Equals(StripInterfacePrefix(SimpleName(portTypeName)), StringComparison.Ordinal)
                    || StripInterfacePrefix(SimpleName(clientContractName)).Equals(StripInterfacePrefix(SimpleName(contractName)), StringComparison.Ordinal);
            });
    }

    private static void AddMetadataLinkGap(
        ScanManifest manifest,
        List<CodeFact> facts,
        IReadOnlyList<CodeFact> metadataOperations,
        CodeFact client,
        string normalizedOperationName)
    {
        var sameNameMetadata = metadataOperations
            .Where(fact => fact.Properties.GetValueOrDefault("operationName", string.Empty).Equals(normalizedOperationName, StringComparison.Ordinal))
            .ToArray();
        if (sameNameMetadata.Length == 0)
        {
            facts.Add(FactFactory.Create(
                manifest,
                FactTypes.AnalysisGap,
                RuleIds.LegacyWcfOperationNormalization,
                EvidenceTiers.Tier4Unknown,
                client.Evidence,
                sourceSymbol: client.TargetSymbol,
                contractElement: client.Properties.GetValueOrDefault("operationName", string.Empty),
                properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["classification"] = "MissingLocalWcfMetadata",
                    ["clientContractName"] = client.Properties.GetValueOrDefault("clientContractName", string.Empty),
                    ["message"] = "A WCF async client method had no connected checked-in metadata, sync sibling, or aligned service operation to corroborate normalization.",
                    ["normalizedOperationName"] = normalizedOperationName,
                    ["originalOperationName"] = client.Properties.GetValueOrDefault("operationName", string.Empty)
                }));
            return;
        }

        facts.Add(FactFactory.Create(
            manifest,
            FactTypes.AnalysisGap,
            RuleIds.LegacyWcfOperationNormalization,
            EvidenceTiers.Tier4Unknown,
            client.Evidence,
            sourceSymbol: client.TargetSymbol,
            contractElement: client.Properties.GetValueOrDefault("operationName", string.Empty),
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["classification"] = "UnlinkedWcfMetadata",
                ["clientContractName"] = client.Properties.GetValueOrDefault("clientContractName", string.Empty),
                ["message"] = "Checked-in WCF metadata contained a matching operation name, but it was not linked to the generated client by folder or contract identity.",
                ["normalizedOperationName"] = normalizedOperationName,
                ["originalOperationName"] = client.Properties.GetValueOrDefault("operationName", string.Empty)
            }));
    }

    private static bool HasSameContractGeneratedSibling(IReadOnlyList<CodeFact> generatedMethods, CodeFact client, string operationName)
    {
        var clientContractName = client.Properties.GetValueOrDefault("clientContractName", string.Empty);
        var typeName = client.Properties.GetValueOrDefault("typeName", string.Empty);
        return generatedMethods.Any(method =>
            !ReferenceEquals(method, client)
            && method.Properties.GetValueOrDefault("operationName", string.Empty).Equals(operationName, StringComparison.Ordinal)
            && method.Properties.GetValueOrDefault("clientContractName", string.Empty).Equals(clientContractName, StringComparison.Ordinal)
            && method.Properties.GetValueOrDefault("typeName", string.Empty).Equals(typeName, StringComparison.Ordinal));
    }

    private static bool HasAlignedOperation(IReadOnlyList<ContractOperationCandidate> operationCandidates, string clientContractName, string operationName)
    {
        return operationCandidates.Any(operation =>
            operation.NormalizedOperationName.Equals(operationName, StringComparison.Ordinal)
            && NamesAlign(clientContractName, operation.ContractName));
    }

    private static bool HasApmPair(IReadOnlyList<CodeFact> generatedMethods, CodeFact client, string baseName)
    {
        var clientContractName = client.Properties.GetValueOrDefault("clientContractName", string.Empty);
        var typeName = client.Properties.GetValueOrDefault("typeName", string.Empty);
        return generatedMethods.Any(method => SameGeneratedClient(method, clientContractName, typeName, "Begin" + baseName))
            && generatedMethods.Any(method => SameGeneratedClient(method, clientContractName, typeName, "End" + baseName));
    }

    private static bool SameGeneratedClient(CodeFact method, string clientContractName, string typeName, string operationName)
    {
        return method.Properties.GetValueOrDefault("operationName", string.Empty).Equals(operationName, StringComparison.Ordinal)
            && method.Properties.GetValueOrDefault("clientContractName", string.Empty).Equals(clientContractName, StringComparison.Ordinal)
            && method.Properties.GetValueOrDefault("typeName", string.Empty).Equals(typeName, StringComparison.Ordinal);
    }

    private static bool IsGeneratedServiceReferenceFile(string relativePath, string text)
    {
        return relativePath.Contains("Service Reference", StringComparison.OrdinalIgnoreCase)
            || relativePath.Contains("ServiceReference", StringComparison.OrdinalIgnoreCase)
            || Path.GetFileName(relativePath).Equals("Reference.cs", StringComparison.OrdinalIgnoreCase)
            || text.Contains("System.CodeDom.Compiler.GeneratedCodeAttribute", StringComparison.Ordinal)
            || text.Contains("[GeneratedCode", StringComparison.Ordinal);
    }

    private static string GetClientBaseContractName(TypeDeclarationSyntax type)
    {
        var typeName = type.Identifier.ValueText;
        if (!typeName.EndsWith("Client", StringComparison.Ordinal) || type.BaseList is null)
        {
            return string.Empty;
        }

        foreach (var baseType in type.BaseList.Types)
        {
            var baseName = baseType.Type;
            if (baseName is QualifiedNameSyntax qualifiedName)
            {
                baseName = qualifiedName.Right;
            }
            if (baseName is AliasQualifiedNameSyntax aliasQualifiedName)
            {
                baseName = aliasQualifiedName.Name;
            }
            if (baseName is not GenericNameSyntax genericName || genericName.TypeArgumentList.Arguments.Count != 1)
            {
                continue;
            }

            var identifier = genericName.Identifier.ValueText;
            if (identifier is "ClientBase"
                || baseType.Type.ToString().StartsWith("System.ServiceModel.ClientBase<", StringComparison.Ordinal))
            {
                return genericName.TypeArgumentList.Arguments[0].ToString().Trim();
            }
        }

        return string.Empty;
    }

    private static bool HasAttribute(SyntaxList<AttributeListSyntax> attributes, string expectedName)
    {
        return attributes
            .SelectMany(list => list.Attributes)
            .Any(attribute =>
            {
                var name = attribute.Name.ToString();
                return name.Equals(expectedName, StringComparison.Ordinal)
                    || name.Equals(expectedName + "Attribute", StringComparison.Ordinal)
                    || name.EndsWith("." + expectedName, StringComparison.Ordinal)
                    || name.EndsWith("." + expectedName + "Attribute", StringComparison.Ordinal);
            });
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
        return new EvidenceSpan(relativePath, Math.Max(1, start), Math.Max(1, end), null, "LegacyWcfExtractor", ScannerVersions.LegacyWcfExtractor);
    }

    private static int GetLine(SyntaxTree tree, SyntaxNode node)
    {
        return tree.GetLineSpan(node.Span).StartLinePosition.Line + 1;
    }

    private static bool NamesAlign(string left, string right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return false;
        }

        return left.Equals(right, StringComparison.Ordinal)
            || left.EndsWith("." + right, StringComparison.Ordinal)
            || right.EndsWith("." + left, StringComparison.Ordinal);
    }

    private static bool HostMatchesContract(string serviceName, string contractName)
    {
        if (NamesAlign(serviceName, contractName))
        {
            return true;
        }

        var serviceSimpleName = SimpleName(serviceName);
        var contractSimpleName = StripInterfacePrefix(SimpleName(contractName));
        return !string.IsNullOrWhiteSpace(serviceSimpleName)
            && serviceSimpleName.Equals(contractSimpleName, StringComparison.Ordinal);
    }

    private static bool IsServiceModelEndpoint(XElement endpoint)
    {
        var serviceModelAncestor = endpoint.Ancestors().FirstOrDefault(element => element.Name.LocalName == "system.serviceModel");
        if (serviceModelAncestor is null)
        {
            return false;
        }

        var clientAncestor = endpoint.Ancestors().FirstOrDefault(element => element.Name.LocalName == "client");
        if (clientAncestor is not null)
        {
            return clientAncestor.Parent == serviceModelAncestor;
        }

        var serviceAncestor = endpoint.Ancestors().FirstOrDefault(element => element.Name.LocalName == "service");
        return serviceAncestor?.Parent?.Name.LocalName == "services"
            && serviceAncestor.Parent.Parent == serviceModelAncestor;
    }

    private static string SimpleName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var lastDot = value.LastIndexOf('.');
        return lastDot >= 0 ? value[(lastDot + 1)..] : value;
    }

    private static string StripInterfacePrefix(string value)
    {
        return value.Length > 1 && value[0] == 'I' && char.IsUpper(value[1])
            ? value[1..]
            : value;
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

    private static string SafeFileHash(string fullPath)
    {
        using var stream = File.OpenRead(fullPath);
        var bytes = SHA256.HashData(stream);
        var hex = Convert.ToHexString(bytes).ToLowerInvariant();
        return hex[..32];
    }

    private static string ServiceReferenceFolderLabel(string relativePath)
    {
        var directory = FileInventory.NormalizeRelativePath(Path.GetDirectoryName(relativePath) ?? string.Empty);
        return directory is "." ? string.Empty : directory;
    }

    private static IEnumerable<string> SafeMetadataBasenames(XDocument document)
    {
        return SafeDocumentValues(document)
            .Select(SafeBasename)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Where(value =>
            {
                var extension = Path.GetExtension(value);
                return extension.Equals(".svcmap", StringComparison.OrdinalIgnoreCase)
                    || extension.Equals(".wsdl", StringComparison.OrdinalIgnoreCase)
                    || extension.Equals(".disco", StringComparison.OrdinalIgnoreCase)
                    || extension.Equals(".xsd", StringComparison.OrdinalIgnoreCase);
            })!;
    }

    private static IEnumerable<string> SafeGeneratedCodeBasenames(XDocument document)
    {
        return SafeDocumentValues(document)
            .Select(SafeBasename)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Where(value => Path.GetExtension(value).Equals(".cs", StringComparison.OrdinalIgnoreCase))!;
    }

    private static string SafeBasename(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || value.Contains("://", StringComparison.Ordinal)
            || value.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
        {
            return string.Empty;
        }

        var normalized = value.Trim().Replace('\\', '/');
        var lastSlash = normalized.LastIndexOf('/');
        var fileName = lastSlash >= 0 ? normalized[(lastSlash + 1)..] : normalized;
        return fileName.Contains(':', StringComparison.Ordinal)
            ? string.Empty
            : fileName;
    }

    private static IEnumerable<string> UrlLikeValues(XDocument document)
    {
        return SafeDocumentValues(document).Where(IsUrlLike);
    }

    private static IEnumerable<string> SafeDocumentValues(XDocument document)
    {
        foreach (var attribute in document.Descendants().SelectMany(element => element.Attributes()))
        {
            if (!string.IsNullOrWhiteSpace(attribute.Value))
            {
                yield return attribute.Value.Trim();
            }
        }

        foreach (var element in document.Descendants())
        {
            if (!element.HasElements && !string.IsNullOrWhiteSpace(element.Value))
            {
                yield return element.Value.Trim();
            }
        }
    }

    private static string SafeIdentifier(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        return SafeNcName().IsMatch(trimmed) && !IsUrlLike(trimmed)
            ? trimmed
            : string.Empty;
    }

    private static string MetadataHash(CodeFact fact)
    {
        return fact.Properties.GetValueOrDefault("metadataHash", string.Empty);
    }

    private static bool TryAsyncBaseName(string operationName, out string baseName)
    {
        const string suffix = "Async";
        if (operationName.EndsWith(suffix, StringComparison.Ordinal) && operationName.Length > suffix.Length)
        {
            baseName = operationName[..^suffix.Length];
            return true;
        }

        baseName = string.Empty;
        return false;
    }

    private static bool TryApmBaseName(string operationName, out string baseName)
    {
        if (operationName.StartsWith("Begin", StringComparison.Ordinal) && operationName.Length > "Begin".Length)
        {
            baseName = operationName["Begin".Length..];
            return true;
        }

        if (operationName.StartsWith("End", StringComparison.Ordinal) && operationName.Length > "End".Length)
        {
            baseName = operationName["End".Length..];
            return true;
        }

        baseName = string.Empty;
        return false;
    }

    private static bool IsLifecycleOperation(string operationName)
    {
        if (operationName is "Open" or "Close" or "Abort" or "Dispose" or "OpenAsync" or "CloseAsync")
        {
            return true;
        }

        return TryApmBaseName(operationName, out var baseName)
            && (baseName is "Open" or "Close" or "Abort" or "Dispose");
    }

    private static bool IsUrlLike(string value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && !string.IsNullOrWhiteSpace(uri.Scheme);
    }

    private static void AddMissing(SortedDictionary<string, string> properties, string key, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            properties[key] = "true";
        }
    }

    private static string TryGetAddressScheme(string address)
    {
        return Uri.TryCreate(address, UriKind.Absolute, out var uri)
            ? uri.Scheme
            : string.Empty;
    }

    private static string? AttributeValue(XElement element, string name)
    {
        return element.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName == name)?.Value.Trim();
    }

    private static int GetLine(XObject node)
    {
        return node is IXmlLineInfo lineInfo && lineInfo.HasLineInfo()
            ? Math.Max(1, lineInfo.LineNumber)
            : 1;
    }

    [GeneratedRegex(@"<%@\s*ServiceHost\b[^%]*%>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ServiceHostDirective();

    [GeneratedRegex(@"(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*""(?<value>[^""]*)""", RegexOptions.IgnoreCase)]
    private static partial Regex DirectiveAttribute();

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_.-]*$")]
    private static partial Regex SafeNcName();
}
