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

        foreach (var file in files.Where(item => item.Kind == "CSharp"))
        {
            ExtractCSharp(repoPath, manifest, file, facts);
        }

        foreach (var file in files.Where(item => item.Kind == "ServiceHost"))
        {
            ExtractServiceHost(repoPath, manifest, file, facts);
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

        foreach (var client in generatedMethods.OrderBy(fact => fact.TargetSymbol, StringComparer.Ordinal))
        {
            var operationName = client.Properties.GetValueOrDefault("operationName", string.Empty);
            var clientContractName = client.Properties.GetValueOrDefault("clientContractName", string.Empty);
            if (string.IsNullOrWhiteSpace(operationName))
            {
                continue;
            }
            if (string.IsNullOrWhiteSpace(clientContractName))
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
                continue;
            }

            var matchingOperations = operations
                .Where(operation => operation.Properties.GetValueOrDefault("operationName", string.Empty).Equals(operationName, StringComparison.Ordinal))
                .Where(operation => NamesAlign(clientContractName, operation.Properties.GetValueOrDefault("contractName", string.Empty)))
                .OrderBy(operation => operation.TargetSymbol, StringComparer.Ordinal)
                .ToArray();
            foreach (var operation in matchingOperations)
            {
                var contractName = operation.Properties.GetValueOrDefault("contractName", string.Empty);
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
                    ? "config-contract-and-operation-name"
                    : "operation-name-only";

                var properties = new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["clientName"] = client.Properties.GetValueOrDefault("clientName", string.Empty),
                    ["clientContractName"] = clientContractName,
                    ["contractName"] = contractName,
                    ["endpointCount"] = matchingEndpoints.Length.ToString(),
                    ["hostCount"] = matchingHosts.Length.ToString(),
                    ["mappingKind"] = mappingKind,
                    ["operationName"] = operationName
                };
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
                        targetSymbol: operation.TargetSymbol,
                        contractElement: operationName,
                        properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["classification"] = "AmbiguousWcfServiceReferenceMapping",
                            ["endpointCount"] = matchingEndpoints.Length.ToString(),
                            ["hostCount"] = matchingHosts.Length.ToString(),
                            ["message"] = "Multiple WCF mapping candidates matched; TraceMap did not choose an arbitrary winner.",
                            ["operationCandidateCount"] = matchingOperations.Length.ToString(),
                            ["operationName"] = operationName
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
                    targetSymbol: operation.TargetSymbol,
                    contractElement: operationName,
                    properties: properties));
            }
        }
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

    [GeneratedRegex(@"<%@\s*(?:ServiceHost|WebService)\b[^%]*%>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ServiceHostDirective();

    [GeneratedRegex(@"(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*""(?<value>[^""]*)""", RegexOptions.IgnoreCase)]
    private static partial Regex DirectiveAttribute();
}
