using System.Xml;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TraceMap.Core;

public static class LegacyRemotingExtractor
{
    private const string ExtractorId = "LegacyRemotingExtractor";

    private static readonly HashSet<string> RemotingNamespaces = new(StringComparer.Ordinal)
    {
        "System.Runtime.Remoting",
        "System.Runtime.Remoting.Channels",
        "System.Runtime.Remoting.Channels.Tcp",
        "System.Runtime.Remoting.Channels.Http",
        "System.Runtime.Remoting.Channels.Ipc"
    };

    private static readonly HashSet<string> KnownApiTypes = new(StringComparer.Ordinal)
    {
        "RemotingConfiguration",
        "ChannelServices",
        "WellKnownObjectMode",
        "ObjRef",
        "RemotingServices",
        "IChannel",
        "IChannelReceiver",
        "IChannelSender"
    };

    private static readonly Dictionary<string, (string Kind, string Direction)> ChannelTypes = new(StringComparer.Ordinal)
    {
        ["TcpChannel"] = ("tcp", "unknown"),
        ["HttpChannel"] = ("http", "unknown"),
        ["IpcChannel"] = ("ipc", "unknown"),
        ["TcpServerChannel"] = ("tcp", "server"),
        ["TcpClientChannel"] = ("tcp", "client"),
        ["HttpServerChannel"] = ("http", "server"),
        ["HttpClientChannel"] = ("http", "client"),
        ["IpcServerChannel"] = ("ipc", "server"),
        ["IpcClientChannel"] = ("ipc", "client")
    };

    public static IReadOnlyList<CodeFact> Extract(
        string repoPath,
        ScanManifest manifest,
        IEnumerable<FileInventoryItem> inventory,
        IEnumerable<SemanticFactCandidate> semanticFacts,
        bool semanticAttempted)
    {
        var facts = new List<CodeFact>();
        var files = inventory.OrderBy(item => item.RelativePath, StringComparer.Ordinal).ToArray();
        var semanticFactsArray = semanticFacts.ToArray();
        AddSemanticFacts(manifest, semanticFactsArray, facts);
        var semanticMarshalKeys = facts
            .Where(fact => fact.FactType == FactTypes.RemotingMarshalByRefObjectDeclared && fact.EvidenceTier == EvidenceTiers.Tier1Semantic)
            .Select(fact => MarshalKey(fact.Evidence.FilePath, fact.Evidence.StartLine, fact.Properties.GetValueOrDefault("typeName") ?? fact.SourceSymbol ?? string.Empty))
            .ToHashSet(StringComparer.Ordinal);

        var repositoryHasRemotingContext = false;
        foreach (var file in files.Where(item => FileInventory.IsCSharpKind(item.Kind)))
        {
            var before = facts.Count;
            ExtractCSharp(repoPath, manifest, file, semanticAttempted, semanticMarshalKeys, facts);
            repositoryHasRemotingContext |= facts.Skip(before).Any(IsRemotingContextFact);
        }

        foreach (var file in files.Where(item => item.Kind == "Config"))
        {
            var before = facts.Count;
            ExtractConfig(repoPath, manifest, file, facts);
            repositoryHasRemotingContext |= facts.Skip(before).Any(IsRemotingContextFact);
        }

        AddIndirectMarshalByRefGaps(manifest, semanticFactsArray, repositoryHasRemotingContext, facts);

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

    private static void AddSemanticFacts(ScanManifest manifest, IReadOnlyList<SemanticFactCandidate> semanticFacts, List<CodeFact> facts)
    {
        foreach (var candidate in semanticFacts.OrderBy(fact => fact.Evidence.FilePath, StringComparer.Ordinal).ThenBy(fact => fact.Evidence.StartLine))
        {
            if (candidate.FactType == FactTypes.SymbolRelationship
                && candidate.Properties?.GetValueOrDefault("relationshipKind") == "InheritsFrom"
                && IsMarshalByRefSymbol(candidate.TargetSymbol))
            {
                var typeName = SafeTypeName(candidate.SourceSymbol) ?? candidate.Properties.GetValueOrDefault("sourceSymbolDisplayName") ?? "unknown";
                facts.Add(FactFactory.Create(
                    manifest,
                    FactTypes.RemotingMarshalByRefObjectDeclared,
                    RuleIds.LegacyRemotingMarshalByRef,
                    EvidenceTiers.Tier1Semantic,
                    WithExtractor(candidate.Evidence),
                    projectPath: candidate.ProjectPath,
                    sourceSymbol: candidate.SourceSymbol,
                    targetSymbol: candidate.TargetSymbol,
                    contractElement: typeName,
                    properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["baseTypeName"] = "System.MarshalByRefObject",
                        ["coverage"] = "static-semantic-evidence",
                        ["limitation"] = "Inheritance is Remoting-capable object shape only; it does not prove hosting, activation, reachability, deployment, or production usage.",
                        ["sourceKind"] = "semantic",
                        ["typeName"] = typeName
                    }));
            }

            if (candidate.EvidenceTier == EvidenceTiers.Tier1Semantic && TrySemanticRemotingApiName(candidate, out var apiName, out var apiKind))
            {
                facts.Add(FactFactory.Create(
                    manifest,
                    FactTypes.RemotingApiUsageDeclared,
                    RuleIds.LegacyRemotingApi,
                    EvidenceTiers.Tier1Semantic,
                    WithExtractor(candidate.Evidence),
                    projectPath: candidate.ProjectPath,
                    sourceSymbol: candidate.SourceSymbol,
                    targetSymbol: candidate.TargetSymbol,
                    contractElement: apiName,
                    properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["apiKind"] = apiKind,
                        ["apiName"] = apiName,
                        ["coverage"] = "static-semantic-evidence",
                        ["limitation"] = "Compiler-resolved API reference only; this does not prove runtime Remoting configuration, reachability, deployment, or production usage.",
                        ["sourceKind"] = "semantic"
                    }));
            }
        }
    }

    private static void ExtractCSharp(
        string repoPath,
        ScanManifest manifest,
        FileInventoryItem file,
        bool semanticAttempted,
        IReadOnlySet<string> semanticMarshalKeys,
        List<CodeFact> facts)
    {
        var fullPath = Path.Combine(repoPath, file.RelativePath);
        try
        {
            var text = File.ReadAllText(fullPath);
            var tree = CSharpSyntaxTree.ParseText(text, path: file.RelativePath);
            var root = tree.GetCompilationUnitRoot();
            var fileHadContext = HasFileRemotingContext(root);

            ExtractApiUsage(manifest, file.RelativePath, tree, root, semanticAttempted, facts);
            ExtractMarshalByRef(manifest, file.RelativePath, tree, root, semanticMarshalKeys, facts);
            ExtractChannels(manifest, file.RelativePath, tree, root, facts);
            ExtractRegistrations(manifest, file.RelativePath, tree, root, fileHadContext, facts);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            facts.Add(Gap(
                manifest,
                RuleIds.LegacyRemotingApi,
                file.RelativePath,
                1,
                "UnreadableRemotingCSharp",
                "Unable to parse C# Remoting evidence."));
        }
    }

    private static void ExtractApiUsage(ScanManifest manifest, string relativePath, SyntaxTree tree, CompilationUnitSyntax root, bool semanticAttempted, List<CodeFact> facts)
    {
        foreach (var usingDirective in root.Usings.OrderBy(item => GetLine(tree, item)))
        {
            var name = usingDirective.Name?.ToString() ?? string.Empty;
            if (!IsRemotingNamespace(name))
            {
                continue;
            }

            facts.Add(CreateApiFact(manifest, relativePath, tree, usingDirective, name, "namespace-import", semanticAttempted));
        }

        foreach (var node in root.DescendantNodes().OrderBy(node => GetLine(tree, node)).ThenBy(node => node.SpanStart))
        {
            if (node.Ancestors().OfType<UsingDirectiveSyntax>().Any())
            {
                continue;
            }

            switch (node)
            {
                case QualifiedNameSyntax qualifiedName when IsRemotingQualifiedName(qualifiedName.ToString()):
                    facts.Add(CreateApiFact(manifest, relativePath, tree, qualifiedName, SafeQualifiedName(qualifiedName.ToString()), "fully-qualified-reference", semanticAttempted));
                    break;
                case MemberAccessExpressionSyntax memberAccess when IsRemotingQualifiedName(memberAccess.ToString()):
                    facts.Add(CreateApiFact(manifest, relativePath, tree, memberAccess, SafeQualifiedName(memberAccess.ToString()), "fully-qualified-reference", semanticAttempted));
                    break;
                case IdentifierNameSyntax identifier when KnownApiTypes.Contains(identifier.Identifier.ValueText):
                    if (identifier.Parent is QualifiedNameSyntax)
                    {
                        break;
                    }
                    facts.Add(CreateApiFact(manifest, relativePath, tree, identifier, identifier.Identifier.ValueText, "known-api-type-reference", semanticAttempted));
                    break;
            }
        }
    }

    private static CodeFact CreateApiFact(ScanManifest manifest, string relativePath, SyntaxTree tree, SyntaxNode node, string apiName, string apiKind, bool semanticAttempted)
    {
        var properties = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["apiKind"] = apiKind,
            ["apiName"] = apiName,
            ["coverage"] = semanticAttempted ? "syntax-fallback-semantic-unresolved" : "syntax-fallback",
            ["limitation"] = "Syntax-only Remoting API reference; ambiguous aliases or project-defined lookalikes remain review-tier evidence.",
            ["sourceKind"] = "syntax"
        };
        return FactFactory.Create(
            manifest,
            FactTypes.RemotingApiUsageDeclared,
            RuleIds.LegacyRemotingApi,
            EvidenceTiers.Tier3SyntaxOrTextual,
            Span(tree, relativePath, node),
            targetSymbol: apiName,
            contractElement: apiName,
            properties: properties);
    }

    private static void ExtractMarshalByRef(ScanManifest manifest, string relativePath, SyntaxTree tree, CompilationUnitSyntax root, IReadOnlySet<string> semanticMarshalKeys, List<CodeFact> facts)
    {
        foreach (var declaration in root.DescendantNodes().OfType<ClassDeclarationSyntax>().OrderBy(item => GetLine(tree, item)).ThenBy(item => item.Identifier.ValueText, StringComparer.Ordinal))
        {
            if (declaration.BaseList is null)
            {
                continue;
            }

            foreach (var baseType in declaration.BaseList.Types)
            {
                var baseTypeName = baseType.Type.ToString();
                if (!IsMarshalByRefSyntax(baseTypeName))
                {
                    continue;
                }

                var typeName = SafeTypeName(GetContainingTypeName(declaration));
                if (semanticMarshalKeys.Contains(MarshalKey(relativePath, GetLine(tree, baseType), typeName ?? declaration.Identifier.ValueText)))
                {
                    continue;
                }

                var properties = new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["baseTypeName"] = SafeQualifiedName(baseTypeName),
                    ["coverage"] = "syntax-fallback",
                    ["isAbstract"] = declaration.Modifiers.Any(SyntaxKind.AbstractKeyword).ToString(),
                    ["isPartial"] = declaration.Modifiers.Any(SyntaxKind.PartialKeyword).ToString(),
                    ["limitation"] = "Syntax inheritance is Remoting-capable object-shape evidence only; it does not prove hosting, activation, reachability, deployment, or production usage.",
                    ["sourceKind"] = "syntax",
                    ["typeName"] = typeName ?? declaration.Identifier.ValueText
                };
                if (IsGeneratedFile(relativePath))
                {
                    properties["isGenerated"] = "True";
                }

                facts.Add(FactFactory.Create(
                    manifest,
                    FactTypes.RemotingMarshalByRefObjectDeclared,
                    RuleIds.LegacyRemotingMarshalByRef,
                    EvidenceTiers.Tier3SyntaxOrTextual,
                    Span(tree, relativePath, baseType),
                    sourceSymbol: typeName,
                    targetSymbol: "System.MarshalByRefObject",
                    contractElement: typeName,
                    properties: properties));
            }
        }
    }

    private static void ExtractChannels(ScanManifest manifest, string relativePath, SyntaxTree tree, CompilationUnitSyntax root, List<CodeFact> facts)
    {
        var channelLocals = CollectSingleAssignmentChannelLocals(root);
        foreach (var creation in root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>().OrderBy(item => GetLine(tree, item)).ThenBy(item => item.SpanStart))
        {
            if (!TryChannelType(creation.Type.ToString(), out var channelName, out var channelKind, out var direction))
            {
                continue;
            }

            var properties = ChannelProperties(channelName, channelKind, direction, registrationCall: false, sourceKind: "syntax");
            AddArgumentHash(properties, creation.ArgumentList?.Arguments.Select(argument => argument.Expression));
            facts.Add(FactFactory.Create(
                manifest,
                FactTypes.RemotingChannelDeclared,
                RuleIds.LegacyRemotingChannel,
                EvidenceTiers.Tier3SyntaxOrTextual,
                Span(tree, relativePath, creation),
                targetSymbol: channelName,
                contractElement: channelName,
                properties: properties));
        }

        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>().OrderBy(item => GetLine(tree, item)).ThenBy(item => item.SpanStart))
        {
            if (!IsMemberCall(invocation, "ChannelServices", "RegisterChannel"))
            {
                continue;
            }

            var properties = new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["coverage"] = "syntax-fallback",
                ["limitation"] = "Static channel registration evidence only; dynamic variables, factories, dependency injection, reflection, and branch feasibility are not resolved in v1.",
                ["registrationCall"] = "True",
                ["sourceKind"] = "syntax"
            };
            var firstArg = invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression;
            if (firstArg is ObjectCreationExpressionSyntax inlineCreation
                && TryChannelType(inlineCreation.Type.ToString(), out var inlineName, out var inlineKind, out var inlineDirection))
            {
                properties["channelKind"] = inlineKind;
                properties["channelDirection"] = inlineDirection;
                properties["channelTypeName"] = inlineName;
                properties["linkKind"] = "inline-construction";
                AddArgumentHash(properties, inlineCreation.ArgumentList?.Arguments.Select(argument => argument.Expression));
            }
            else if (firstArg is IdentifierNameSyntax identifier && channelLocals.TryGetValue(identifier.Identifier.ValueText, out var localChannel))
            {
                properties["channelKind"] = localChannel.Kind;
                properties["channelDirection"] = localChannel.Direction;
                properties["channelTypeName"] = localChannel.TypeName;
                properties["linkKind"] = "same-method-single-local";
            }
            else if (firstArg is not null)
            {
                properties["linkKind"] = "unsupported-dynamic-or-nonlocal";
                properties["valueHash"] = HashSyntax(firstArg);
                facts.Add(Gap(
                    manifest,
                    RuleIds.LegacyRemotingChannel,
                    relativePath,
                    GetLine(tree, invocation),
                    "UnsupportedRemotingChannelRegistrationLink",
                    "Channel registration argument could not be deterministically linked to a local channel construction."));
            }
            else
            {
                properties["linkKind"] = "missing-channel-argument";
            }

            facts.Add(FactFactory.Create(
                manifest,
                FactTypes.RemotingChannelRegistered,
                RuleIds.LegacyRemotingChannel,
                EvidenceTiers.Tier3SyntaxOrTextual,
                Span(tree, relativePath, invocation),
                targetSymbol: "ChannelServices.RegisterChannel",
                contractElement: "RegisterChannel",
                properties: properties));
        }
    }

    private static void ExtractRegistrations(ScanManifest manifest, string relativePath, SyntaxTree tree, CompilationUnitSyntax root, bool fileHasContext, List<CodeFact> facts)
    {
        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>().OrderBy(item => GetLine(tree, item)).ThenBy(item => item.SpanStart))
        {
            if (TryRemotingConfigurationCall(invocation, out var methodName))
            {
                ExtractRemotingConfigurationCall(manifest, relativePath, tree, invocation, methodName, facts);
                continue;
            }

            if (IsMemberCall(invocation, "Activator", "GetObject"))
            {
                ExtractActivatorGetObject(manifest, relativePath, tree, invocation, fileHasContext, facts);
            }
        }
    }

    private static void ExtractRemotingConfigurationCall(ScanManifest manifest, string relativePath, SyntaxTree tree, InvocationExpressionSyntax invocation, string methodName, List<CodeFact> facts)
    {
        var registrationKind = methodName switch
        {
            "RegisterWellKnownServiceType" => "well-known-service",
            "RegisterWellKnownClientType" => "well-known-client",
            "RegisterActivatedServiceType" => "activated-service",
            "RegisterActivatedClientType" => "activated-client",
            "Configure" => "configure",
            _ => "unknown"
        };

        if (registrationKind is "activated-service" or "activated-client")
        {
            facts.Add(Gap(
                manifest,
                RuleIds.LegacyRemotingRegistration,
                relativePath,
                GetLine(tree, invocation),
                "RemotingActivatedTypeRegistrationDeferred",
                "Visible activated type registration call found, but full activated-registration argument extraction is deferred.",
                new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["limitation"] = "activated-type-registration-v1-deferred",
                    ["registrationKind"] = registrationKind
                }));
            return;
        }

        var properties = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["coverage"] = "syntax-fallback",
            ["limitation"] = "Static registration call evidence only; dynamic arguments, runtime configuration, deployment, reachability, and production usage are not proven.",
            ["registrationKind"] = registrationKind,
            ["sourceKind"] = "syntax"
        };
        var args = invocation.ArgumentList.Arguments.Select(argument => argument.Expression).ToArray();
        if (methodName == "Configure")
        {
            properties["configFileName"] = args.Length > 0 ? SafeConfigFileName(args[0]) : string.Empty;
            if (args.Length > 0)
            {
                properties["valueHash"] = HashSyntax(args[0]);
            }
            facts.Add(FactFactory.Create(
                manifest,
                FactTypes.RemotingApiUsageDeclared,
                RuleIds.LegacyRemotingRegistration,
                EvidenceTiers.Tier3SyntaxOrTextual,
                Span(tree, relativePath, invocation),
                targetSymbol: "RemotingConfiguration.Configure",
                contractElement: "Configure",
                properties: properties));
            return;
        }

        var targetType = args.Length > 0 ? SafeTypeArgument(args[0]) : null;
        if (!string.IsNullOrWhiteSpace(targetType))
        {
            properties["targetTypeName"] = targetType!;
        }
        else
        {
            properties["targetTypeStatus"] = "unresolved";
        }
        if (args.Length > 1)
        {
            properties[registrationKind == "well-known-client" ? "urlHash" : "objectUriHash"] = HashSyntax(args[1]);
        }
        if (args.Length > 2)
        {
            properties["objectMode"] = SafeMode(args[2]);
        }

        facts.Add(FactFactory.Create(
            manifest,
            registrationKind == "well-known-client" ? FactTypes.RemotingClientTypeRegistered : FactTypes.RemotingServiceTypeRegistered,
            RuleIds.LegacyRemotingRegistration,
            EvidenceTiers.Tier3SyntaxOrTextual,
            Span(tree, relativePath, invocation),
            targetSymbol: targetType,
            contractElement: registrationKind,
            properties: properties));
    }

    private static void ExtractActivatorGetObject(ScanManifest manifest, string relativePath, SyntaxTree tree, InvocationExpressionSyntax invocation, bool fileHasContext, List<CodeFact> facts)
    {
        if (!fileHasContext)
        {
            facts.Add(Gap(
                manifest,
                RuleIds.LegacyRemotingRegistration,
                relativePath,
                GetLine(tree, invocation),
                "ActivatorGetObjectNeedsRemotingContext",
                "Activator.GetObject call found without same-file Remoting channel, registration, or semantic MarshalByRefObject evidence."));
            return;
        }

        var args = invocation.ArgumentList.Arguments.Select(argument => argument.Expression).ToArray();
        var properties = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["coverage"] = "syntax-fallback-remoting-context",
            ["limitation"] = "Static client activation evidence gated by same-file Remoting context; URL/object URI values are hashed and runtime reachability is not proven.",
            ["registrationKind"] = "client-activation",
            ["sourceKind"] = "syntax"
        };
        var targetType = args.Length > 0 ? SafeTypeArgument(args[0]) : null;
        if (!string.IsNullOrWhiteSpace(targetType))
        {
            properties["targetTypeName"] = targetType!;
        }
        if (args.Length > 1)
        {
            properties["urlHash"] = HashSyntax(args[1]);
        }

        facts.Add(FactFactory.Create(
            manifest,
            FactTypes.RemotingClientActivationDeclared,
            RuleIds.LegacyRemotingRegistration,
            EvidenceTiers.Tier3SyntaxOrTextual,
            Span(tree, relativePath, invocation),
            targetSymbol: targetType,
            contractElement: "Activator.GetObject",
            properties: properties));
    }

    private static void ExtractConfig(string repoPath, ScanManifest manifest, FileInventoryItem file, List<CodeFact> facts)
    {
        var fullPath = Path.Combine(repoPath, file.RelativePath);
        try
        {
            var document = LoadSafeXml(fullPath);
            foreach (var section in document.Descendants().Where(element => element.Name.LocalName == "system.runtime.remoting").OrderBy(GetLine))
            {
                var sectionProperties = new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["configKind"] = "section",
                    ["coverage"] = "static-xml-config",
                    ["limitation"] = "Checked-in XML config evidence only; transforms, external includes, encrypted sections, runtime mutation, deployment, and production usage are not resolved.",
                    ["sourceFormat"] = "xml-config"
                };
                var applicationName = section.Descendants().FirstOrDefault(element => element.Name.LocalName == "application") is { } application
                    ? AttributeValue(application, "name")
                    : null;
                AddHashProperty(sectionProperties, "applicationNameHash", applicationName);

                facts.Add(FactFactory.Create(
                    manifest,
                    FactTypes.RemotingConfigSectionDeclared,
                    RuleIds.LegacyRemotingConfig,
                    EvidenceTiers.Tier2Structural,
                    ConfigSpan(file.RelativePath, section),
                    targetSymbol: "system.runtime.remoting",
                    contractElement: "system.runtime.remoting",
                    properties: sectionProperties));

                ExtractConfigChildren(manifest, file.RelativePath, section, facts);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or XmlException)
        {
            facts.Add(Gap(
                manifest,
                RuleIds.LegacyRemotingConfig,
                file.RelativePath,
                1,
                "MalformedRemotingConfig",
                "Unable to parse Remoting config evidence with safe XML settings."));
        }
    }

    private static void ExtractConfigChildren(ScanManifest manifest, string relativePath, XElement section, List<CodeFact> facts)
    {
        var recognized = 0;
        foreach (var element in section.Descendants().OrderBy(GetLine).ThenBy(element => element.Name.LocalName, StringComparer.Ordinal))
        {
            var local = element.Name.LocalName;
            if (local == "channel")
            {
                recognized++;
                var properties = ConfigProperties("channel");
                var type = AttributeValue(element, "type") ?? string.Empty;
                var id = AttributeValue(element, "id") ?? string.Empty;
                var channelRef = AttributeValue(element, "ref") ?? string.Empty;
                properties["channelKind"] = SafeChannelKind(type, id, channelRef);
                AddUnsafeAttributeHashes(properties, element, "port", "name", "ref", "type");
                facts.Add(FactFactory.Create(
                    manifest,
                    FactTypes.RemotingConfigChannelDeclared,
                    RuleIds.LegacyRemotingConfig,
                    EvidenceTiers.Tier2Structural,
                    ConfigSpan(relativePath, element),
                    targetSymbol: properties["channelKind"],
                    contractElement: "channel",
                    properties: properties));
            }
            else if (local is "wellknown" or "activated")
            {
                recognized++;
                var parent = element.Parent?.Name.LocalName ?? string.Empty;
                var isClient = parent.Equals("client", StringComparison.OrdinalIgnoreCase);
                var properties = ConfigProperties(isClient ? "client" : "service");
                properties["registrationKind"] = $"{(local == "wellknown" ? "well-known" : "activated")}-{(isClient ? "client" : "service")}";
                var (typeName, assemblyName) = SplitConfigType(AttributeValue(element, "type"), AttributeValue(element, "assembly"));
                AddSafeIdentifierProperty(properties, "typeName", typeName);
                AddSafeIdentifierProperty(properties, "assemblyName", assemblyName);
                AddModeProperty(properties, AttributeValue(element, "mode"));
                AddHashProperty(properties, "objectUriHash", AttributeValue(element, "objectUri"));
                AddHashProperty(properties, "urlHash", AttributeValue(element, "url"));
                facts.Add(FactFactory.Create(
                    manifest,
                    isClient ? FactTypes.RemotingConfigClientDeclared : FactTypes.RemotingConfigServiceDeclared,
                    RuleIds.LegacyRemotingConfig,
                    EvidenceTiers.Tier2Structural,
                    ConfigSpan(relativePath, element),
                    targetSymbol: properties.GetValueOrDefault("typeName"),
                    contractElement: properties["registrationKind"],
                    properties: properties));
            }
            else if (local is "provider" or "serverProviders" or "clientProviders")
            {
                recognized++;
                var properties = ConfigProperties("provider");
                properties["providerKind"] = local;
                AddUnsafeAttributeHashes(properties, element, element.Attributes().Select(attribute => attribute.Name.LocalName).ToArray());
                facts.Add(FactFactory.Create(
                    manifest,
                    FactTypes.RemotingConfigProviderDeclared,
                    RuleIds.LegacyRemotingConfig,
                    EvidenceTiers.Tier2Structural,
                    ConfigSpan(relativePath, element),
                    contractElement: local,
                    properties: properties));
            }
            else if (local is "application")
            {
                recognized++;
            }
        }

        if (recognized == 0 && section.Elements().Any())
        {
            facts.Add(Gap(
                manifest,
                RuleIds.LegacyRemotingConfig,
                relativePath,
                GetLine(section),
                "UnsupportedRemotingConfigChildren",
                "Remoting config section contains only unrecognized child elements.",
                new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["coverage"] = "static-xml-config-reduced",
                    ["limitation"] = "unsupported-remoting-config-shape"
                }));
        }
    }

    private static void AddIndirectMarshalByRefGaps(ScanManifest manifest, IReadOnlyList<SemanticFactCandidate> semanticFacts, bool repositoryHasRemotingContext, List<CodeFact> facts)
    {
        if (repositoryHasRemotingContext)
        {
            return;
        }

        foreach (var relationship in semanticFacts
            .Where(fact => fact.FactType == FactTypes.SymbolRelationship)
            .Where(fact => fact.Properties?.GetValueOrDefault("relationshipKind") == "InheritsFrom")
            .Where(fact => !IsMarshalByRefSymbol(fact.TargetSymbol))
            .Where(fact => fact.TargetSymbol?.Contains("MarshalByRef", StringComparison.Ordinal) == true)
            .OrderBy(fact => fact.Evidence.FilePath, StringComparer.Ordinal)
            .ThenBy(fact => fact.Evidence.StartLine))
        {
            facts.Add(Gap(
                manifest,
                RuleIds.LegacyRemotingMarshalByRef,
                relationship.Evidence.FilePath,
                relationship.Evidence.StartLine,
                "IndirectMarshalByRefWithoutRemotingContext",
                "Indirect MarshalByRefObject-style inheritance was not promoted without Remoting-specific repository, file, registration, or config context."));
        }
    }

    private static Dictionary<string, (string TypeName, string Kind, string Direction)> CollectSingleAssignmentChannelLocals(CompilationUnitSyntax root)
    {
        var result = new Dictionary<string, (string TypeName, string Kind, string Direction)>(StringComparer.Ordinal);
        foreach (var method in root.DescendantNodes().OfType<BaseMethodDeclarationSyntax>())
        {
            var candidates = new Dictionary<string, (string TypeName, string Kind, string Direction)>(StringComparer.Ordinal);
            var blocked = new HashSet<string>(StringComparer.Ordinal);
            foreach (var declarator in method.DescendantNodes().OfType<VariableDeclaratorSyntax>())
            {
                if (declarator.Initializer?.Value is ObjectCreationExpressionSyntax creation
                    && TryChannelType(creation.Type.ToString(), out var typeName, out var kind, out var direction))
                {
                    if (candidates.ContainsKey(declarator.Identifier.ValueText))
                    {
                        blocked.Add(declarator.Identifier.ValueText);
                    }
                    candidates[declarator.Identifier.ValueText] = (typeName, kind, direction);
                }
            }

            foreach (var assignment in method.DescendantNodes().OfType<AssignmentExpressionSyntax>())
            {
                if (assignment.Left is IdentifierNameSyntax identifier)
                {
                    blocked.Add(identifier.Identifier.ValueText);
                }
            }

            foreach (var pair in candidates.Where(pair => !blocked.Contains(pair.Key)))
            {
                result[pair.Key] = pair.Value;
            }
        }

        return result;
    }

    private static bool HasFileRemotingContext(CompilationUnitSyntax root)
    {
        return root.DescendantNodes().Any(node =>
            node is ObjectCreationExpressionSyntax creation && TryChannelType(creation.Type.ToString(), out _, out _, out _)
            || node is InvocationExpressionSyntax invocation && (IsMemberCall(invocation, "ChannelServices", "RegisterChannel") || TryRemotingConfigurationCall(invocation, out _))
            || node is ClassDeclarationSyntax declaration && declaration.BaseList?.Types.Any(baseType => IsMarshalByRefSyntax(baseType.Type.ToString())) == true);
    }

    private static bool IsRemotingContextFact(CodeFact fact)
    {
        return fact.FactType.StartsWith("Remoting", StringComparison.Ordinal)
            && fact.FactType != FactTypes.RemotingApiUsageDeclared;
    }

    private static bool TrySemanticRemotingApiName(SemanticFactCandidate candidate, out string apiName, out string apiKind)
    {
        apiName = string.Empty;
        apiKind = "semantic-symbol";
        var target = candidate.TargetSymbol ?? candidate.Properties?.GetValueOrDefault("targetSymbol") ?? string.Empty;
        if (target.Contains("System.Runtime.Remoting", StringComparison.Ordinal)
            || KnownApiTypes.Any(type => target.Contains($".{type}", StringComparison.Ordinal) || target.EndsWith(type, StringComparison.Ordinal)))
        {
            apiName = SafeQualifiedName(target);
            return true;
        }

        return false;
    }

    private static bool IsRemotingNamespace(string value)
    {
        return RemotingNamespaces.Contains(value) || value.StartsWith("System.Runtime.Remoting.", StringComparison.Ordinal);
    }

    private static bool IsRemotingQualifiedName(string value)
    {
        return value.StartsWith("System.Runtime.Remoting", StringComparison.Ordinal)
            || value.StartsWith("global::System.Runtime.Remoting", StringComparison.Ordinal);
    }

    private static bool IsMarshalByRefSyntax(string value)
    {
        return value is "MarshalByRefObject" or "System.MarshalByRefObject" or "global::System.MarshalByRefObject";
    }

    private static bool IsMarshalByRefSymbol(string? value)
    {
        return value is "System.MarshalByRefObject" or "global::System.MarshalByRefObject"
            || value?.EndsWith(".MarshalByRefObject", StringComparison.Ordinal) == true;
    }

    private static bool TryChannelType(string value, out string typeName, out string kind, out string direction)
    {
        typeName = ShortName(value);
        if (ChannelTypes.TryGetValue(typeName, out var channel))
        {
            kind = channel.Kind;
            direction = channel.Direction;
            return true;
        }

        kind = string.Empty;
        direction = string.Empty;
        return false;
    }

    private static bool TryRemotingConfigurationCall(InvocationExpressionSyntax invocation, out string methodName)
    {
        methodName = string.Empty;
        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess
            && memberAccess.Expression.ToString().EndsWith("RemotingConfiguration", StringComparison.Ordinal))
        {
            methodName = memberAccess.Name.Identifier.ValueText;
            return methodName is "RegisterWellKnownServiceType"
                or "RegisterWellKnownClientType"
                or "RegisterActivatedServiceType"
                or "RegisterActivatedClientType"
                or "Configure";
        }

        return false;
    }

    private static bool IsMemberCall(InvocationExpressionSyntax invocation, string receiverName, string methodName)
    {
        return invocation.Expression is MemberAccessExpressionSyntax memberAccess
            && memberAccess.Name.Identifier.ValueText == methodName
            && memberAccess.Expression.ToString().EndsWith(receiverName, StringComparison.Ordinal);
    }

    private static SortedDictionary<string, string> ChannelProperties(string typeName, string kind, string direction, bool registrationCall, string sourceKind)
    {
        return new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["channelDirection"] = direction,
            ["channelKind"] = kind,
            ["channelTypeName"] = typeName,
            ["coverage"] = "syntax-fallback",
            ["limitation"] = "Static channel evidence only; ports, URLs, names, provider values, runtime reachability, deployment, and production usage are not proven.",
            ["registrationCall"] = registrationCall.ToString(),
            ["sourceKind"] = sourceKind
        };
    }

    private static SortedDictionary<string, string> ConfigProperties(string configKind)
    {
        return new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["configKind"] = configKind,
            ["coverage"] = "static-xml-config",
            ["limitation"] = "Checked-in XML config evidence only; values are hashed or omitted and runtime config selection, reachability, deployment, and production usage are not proven.",
            ["sourceFormat"] = "xml-config"
        };
    }

    private static void AddArgumentHash(SortedDictionary<string, string> properties, IEnumerable<ExpressionSyntax>? expressions)
    {
        var expressionArray = expressions?.ToArray() ?? [];
        if (expressionArray.Length == 0)
        {
            return;
        }

        properties["valueHash"] = FactFactory.Hash(string.Join("|", expressionArray.Select(expression => expression.NormalizeWhitespace().ToFullString())), 32);
        properties["redaction"] = "constructor-or-config-arguments-hashed";
    }

    private static void AddUnsafeAttributeHashes(SortedDictionary<string, string> properties, XElement element, params string[] names)
    {
        var values = names
            .Select(name => AttributeValue(element, name))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (values.Length > 0)
        {
            properties["valueHash"] = FactFactory.Hash(string.Join("|", values), 32);
            properties["redaction"] = "config-values-hashed";
        }
    }

    private static void AddHashProperty(SortedDictionary<string, string> properties, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            properties[key] = FactFactory.Hash(value, 32);
        }
    }

    private static void AddSafeIdentifierProperty(SortedDictionary<string, string> properties, string key, string? value)
    {
        var safe = SafeIdentifier(value);
        if (!string.IsNullOrWhiteSpace(safe))
        {
            properties[key] = safe!;
        }
        else if (!string.IsNullOrWhiteSpace(value))
        {
            properties[$"{key}Hash"] = FactFactory.Hash(value!, 32);
        }
    }

    private static void AddModeProperty(SortedDictionary<string, string> properties, string? value)
    {
        if (value is "Singleton" or "SingleCall")
        {
            properties["objectMode"] = value;
        }
        else if (!string.IsNullOrWhiteSpace(value))
        {
            properties["objectMode"] = "unknown";
        }
    }

    private static string SafeMode(ExpressionSyntax expression)
    {
        var text = expression.ToString();
        if (text.EndsWith(".Singleton", StringComparison.Ordinal) || text == "Singleton")
        {
            return "Singleton";
        }
        if (text.EndsWith(".SingleCall", StringComparison.Ordinal) || text == "SingleCall")
        {
            return "SingleCall";
        }
        return "unknown";
    }

    private static string? SafeTypeArgument(ExpressionSyntax expression)
    {
        return expression switch
        {
            TypeOfExpressionSyntax typeOf => SafeTypeName(typeOf.Type.ToString()),
            _ => null
        };
    }

    private static string SafeConfigFileName(ExpressionSyntax expression)
    {
        return expression is LiteralExpressionSyntax literal && literal.Token.ValueText is { Length: > 0 } value
            ? Path.GetFileName(value)
            : string.Empty;
    }

    private static string SafeChannelKind(string type, string id, string channelRef)
    {
        var value = $"{type} {id} {channelRef}";
        if (value.Contains("tcp", StringComparison.OrdinalIgnoreCase))
        {
            return "tcp";
        }
        if (value.Contains("http", StringComparison.OrdinalIgnoreCase))
        {
            return "http";
        }
        if (value.Contains("ipc", StringComparison.OrdinalIgnoreCase))
        {
            return "ipc";
        }
        return "unknown";
    }

    private static (string? TypeName, string? AssemblyName) SplitConfigType(string? typeValue, string? assemblyValue)
    {
        var typeName = typeValue;
        var assemblyName = assemblyValue;
        if (!string.IsNullOrWhiteSpace(typeValue))
        {
            var parts = typeValue.Split(',', 2, StringSplitOptions.TrimEntries);
            typeName = parts[0];
            if (parts.Length > 1 && string.IsNullOrWhiteSpace(assemblyName))
            {
                assemblyName = parts[1];
            }
        }

        return (typeName, assemblyName);
    }

    private static string? SafeIdentifier(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 160)
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.All(character => char.IsLetterOrDigit(character) || character is '_' or '.' or '+' or '`' or ',')
            && trimmed.Any(char.IsLetter)
            ? trimmed
            : null;
    }

    private static string? SafeTypeName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Replace("global::", string.Empty, StringComparison.Ordinal).Trim();
        var safe = SafeIdentifier(trimmed);
        return string.IsNullOrWhiteSpace(safe) ? ShortName(trimmed) : safe;
    }

    private static string SafeQualifiedName(string value)
    {
        return value.Replace("global::", string.Empty, StringComparison.Ordinal).Trim();
    }

    private static string ShortName(string value)
    {
        var clean = value.Replace("global::", string.Empty, StringComparison.Ordinal).Trim();
        var parts = clean.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 0 ? clean : parts[^1];
    }

    private static string MarshalKey(string relativePath, int line, string typeName)
    {
        return $"{relativePath}|{line}|{ShortName(typeName)}";
    }

    private static string? GetContainingTypeName(ClassDeclarationSyntax declaration)
    {
        var names = new Stack<string>();
        for (SyntaxNode? node = declaration; node is not null; node = node.Parent)
        {
            switch (node)
            {
                case ClassDeclarationSyntax type:
                    names.Push(type.Identifier.ValueText);
                    break;
                case NamespaceDeclarationSyntax namespaceDeclaration:
                    names.Push(namespaceDeclaration.Name.ToString());
                    break;
                case FileScopedNamespaceDeclarationSyntax fileScopedNamespace:
                    names.Push(fileScopedNamespace.Name.ToString());
                    break;
            }
        }

        return SafeTypeName(string.Join(".", names));
    }

    private static string HashSyntax(ExpressionSyntax expression)
    {
        return FactFactory.Hash(expression.NormalizeWhitespace().ToFullString(), 32);
    }

    private static CodeFact Gap(
        ScanManifest manifest,
        string ruleId,
        string relativePath,
        int line,
        string classification,
        string message,
        SortedDictionary<string, string>? extraProperties = null)
    {
        var properties = extraProperties ?? new SortedDictionary<string, string>(StringComparer.Ordinal);
        properties["classification"] = classification;
        properties["coverage"] = "reduced";
        properties["message"] = message;
        return FactFactory.Create(
            manifest,
            FactTypes.AnalysisGap,
            ruleId,
            EvidenceTiers.Tier4Unknown,
            new EvidenceSpan(relativePath, Math.Max(1, line), Math.Max(1, line), null, ExtractorId, ScannerVersions.LegacyRemotingExtractor),
            properties: properties);
    }

    private static EvidenceSpan Span(SyntaxTree tree, string relativePath, SyntaxNode node)
    {
        var span = tree.GetLineSpan(node.Span);
        var start = span.StartLinePosition.Line + 1;
        var end = span.EndLinePosition.Line + 1;
        return new EvidenceSpan(relativePath, Math.Max(1, start), Math.Max(1, end), null, ExtractorId, ScannerVersions.LegacyRemotingExtractor);
    }

    private static EvidenceSpan ConfigSpan(string relativePath, XElement element)
    {
        var line = GetLine(element);
        return new EvidenceSpan(relativePath, line, line, null, ExtractorId, ScannerVersions.LegacyRemotingExtractor);
    }

    private static EvidenceSpan WithExtractor(EvidenceSpan evidence)
    {
        return evidence with { ExtractorId = ExtractorId, ExtractorVersion = ScannerVersions.LegacyRemotingExtractor };
    }

    private static int GetLine(SyntaxTree tree, SyntaxNode node)
    {
        return tree.GetLineSpan(node.Span).StartLinePosition.Line + 1;
    }

    private static int GetLine(XElement element)
    {
        return element is IXmlLineInfo info && info.HasLineInfo()
            ? Math.Max(1, info.LineNumber)
            : 1;
    }

    private static string? AttributeValue(XElement element, string name)
    {
        return element.Attributes().FirstOrDefault(attribute => attribute.Name.LocalName.Equals(name, StringComparison.OrdinalIgnoreCase))?.Value;
    }

    private static XDocument LoadSafeXml(string fullPath)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = 5_000_000
        };
        using var stream = File.OpenRead(fullPath);
        using var reader = XmlReader.Create(stream, settings);
        return XDocument.Load(reader, LoadOptions.SetLineInfo);
    }

    private static bool IsGeneratedFile(string relativePath)
    {
        var fileName = Path.GetFileName(relativePath);
        return fileName.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".g.i.cs", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".generated.cs", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".designer.cs", StringComparison.OrdinalIgnoreCase);
    }
}
