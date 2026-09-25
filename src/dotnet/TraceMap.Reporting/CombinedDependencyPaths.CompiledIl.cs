using TraceMap.Core;

namespace TraceMap.Reporting;

public static partial class CombinedDependencyPathReporter
{
    private const string CompiledIlBridgeRuleId = "combined.paths.compiled-il-bridge.v1";
    private const string ProjectlessPdbIdentityRuleId = "combined.paths.projectless-pdb-identity.v1";
    private const string ProjectlessPublishCandidateRuleId = "combined.paths.projectless-publish-candidate.v1";

    private static void AddBoundCompiledIlEdges(EvidenceGraph graph, IReadOnlyList<CombinedFactRow> facts)
    {
        AddProjectlessPdbIdentityEdges(graph, facts);
        AddProjectlessPublishCandidateEdges(graph, facts);
        AddProjectlessPublishMemberCandidates(graph, facts);
        var ilCalls = facts.Where(fact => fact.FactType == FactTypes.ManagedIlCallObserved).ToArray();
        if (ilCalls.Length == 0)
            return;

        var factsByOriginalId = facts
            .GroupBy(fact => (fact.SourceIndexId, fact.OriginalFactId))
            .ToDictionary(group => group.Key, group => group.ToArray());
        var methods = facts
            .Where(fact => fact.FactType == FactTypes.ManagedMethodDeclared && !string.IsNullOrWhiteSpace(fact.TargetSymbol))
            .ToArray();
        var methodsByIdentity = methods
            .GroupBy(fact => (fact.SourceIndexId, fact.TargetSymbol!))
            .ToDictionary(group => group.Key, group => group.ToArray());
        var methodsByMemberReference = methods
            .Where(fact => fact.Properties.GetValueOrDefault("provenanceState") == "bound")
            .Select(fact => (Fact: fact, Reference: ExpectedMemberReference(fact)))
            .Where(item => item.Reference is not null)
            .GroupBy(item => item.Reference!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Select(item => item.Fact).ToArray(), StringComparer.Ordinal);
        var admittedReferencePrefixes = methods
            .Where(fact => fact.Properties.GetValueOrDefault("provenanceState") == "bound")
            .Select(fact => fact.Properties.GetValueOrDefault("assemblyReferenceIdentity"))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => "memberref|type:scope(" + value + ")type(")
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var bodiesByOriginalId = facts
            .Where(fact => fact.FactType == FactTypes.ManagedIlBodyDeclared)
            .GroupBy(fact => (fact.SourceIndexId, fact.OriginalFactId))
            .ToDictionary(group => group.Key, group => group.ToArray());
        var sourceDisplaysBySymbolId = facts
            .Where(fact => fact.FactType == FactTypes.CallEdge
                && fact.EvidenceTier == EvidenceTiers.Tier1Semantic
                && !string.IsNullOrWhiteSpace(fact.SourceSymbol)
                && !string.IsNullOrWhiteSpace(fact.Properties.GetValueOrDefault("sourceSymbolId")))
            .GroupBy(fact => (fact.SourceIndexId, fact.Properties["sourceSymbolId"]))
            .ToDictionary(group => group.Key, group => group.Select(fact => fact.SourceSymbol!)
                .Distinct(StringComparer.Ordinal).ToArray());

        foreach (var join in facts.Where(fact => fact.FactType == FactTypes.SourceMetadataIdentityReconciled
                     && fact.EvidenceTier == EvidenceTiers.Tier1Semantic
                     && fact.Properties.GetValueOrDefault("compiledProvenanceState") == "bound")
                 .OrderBy(fact => fact.CombinedFactId, StringComparer.Ordinal))
        {
            var declarationIdentity = join.Properties.GetValueOrDefault("sourceDeclarationIdentity");
            if (string.IsNullOrWhiteSpace(declarationIdentity) || string.IsNullOrWhiteSpace(join.TargetSymbol)
                || string.IsNullOrWhiteSpace(join.Properties.GetValueOrDefault("provenanceBindingInputSha256"))
                || !TryUniqueFact(factsByOriginalId, join.SourceIndexId, join.Properties.GetValueOrDefault("compiledFactId"), out var method)
                || method.FactType != FactTypes.ManagedMethodDeclared
                || !string.Equals(join.TargetSymbol, method.TargetSymbol, StringComparison.Ordinal)
                || !TryUniqueFact(factsByOriginalId, join.SourceIndexId, join.Properties.GetValueOrDefault("sourceFactId"), out _))
                continue;
            if (!sourceDisplaysBySymbolId.TryGetValue((join.SourceIndexId, declarationIdentity), out var sourceDisplays)
                || sourceDisplays.Length != 1)
            {
                AddCompiledIlGap(graph, join,
                    "CompiledIlSourceDisplayUnavailable", "source-symbol-id-to-display-ambiguous-or-unavailable",
                    sourceDisplays?.Length);
                continue;
            }

            var sourceNode = graph.GetOrAddSymbolNode(join.SourceIndexId, join.SourceLabel, sourceDisplays[0],
                join.FilePath, join.StartLine, join.EndLine, join.RuleId, join.EvidenceTier);
            var compiledNode = graph.GetOrAddSymbolNode(method.SourceIndexId, method.SourceLabel, method.TargetSymbol!,
                method.FilePath, method.StartLine, method.EndLine, method.RuleId, method.EvidenceTier);
            foreach (var (from, to, direction) in new[]
                     {
                         (sourceNode, compiledNode, "source-to-compiled"),
                         (compiledNode, sourceNode, "compiled-to-source")
                     })
            {
                graph.AddEdge(new GraphEdge(
                    $"compiled-source-identity:{join.CombinedFactId}:{direction}",
                    "compiled-source-identity", from.NodeId, to.NodeId,
                    "EvidenceEdge", CompiledIlBridgeRuleId, EvidenceTiers.Tier1Semantic,
                    [join.CombinedFactId, method.CombinedFactId], [], SafePath(join.FilePath),
                    join.StartLine, join.EndLine));
            }
        }

        foreach (var call in ilCalls.OrderBy(fact => fact.CombinedFactId, StringComparer.Ordinal))
        {
            var referenceKind = call.Properties.GetValueOrDefault("referenceKind");
            var opcode = call.Properties.GetValueOrDefault("opcode");
            if (referenceKind is not ("methoddef" or "memberref")
                || opcode is not ("call" or "callvirt" or "newobj")
                || !TryUniqueFact(bodiesByOriginalId, call.SourceIndexId,
                    call.Properties.GetValueOrDefault("ilBodyFactId"), out var body)
                || !TryUniqueFact(factsByOriginalId, call.SourceIndexId,
                    body.Properties.GetValueOrDefault("compiledFactId"), out var caller)
                || caller.FactType != FactTypes.ManagedMethodDeclared
                || caller.Properties.GetValueOrDefault("provenanceState") != "bound"
                || string.IsNullOrWhiteSpace(body.Properties.GetValueOrDefault("ilBoundedInputSha256"))
                || string.IsNullOrWhiteSpace(body.Properties.GetValueOrDefault("ilGeneratorSha256"))
                || string.IsNullOrWhiteSpace(body.Properties.GetValueOrDefault("rawFileSha256"))
                || body.Properties.GetValueOrDefault("rawFileSha256") != caller.Properties.GetValueOrDefault("rawFileSha256")
                || call.Properties.GetValueOrDefault("rawFileSha256") != body.Properties.GetValueOrDefault("rawFileSha256"))
                continue;

            var targetIdentity = call.Properties.GetValueOrDefault("targetIdentity") ?? string.Empty;
            var matched = referenceKind == "methoddef"
                ? methodsByIdentity.TryGetValue((call.SourceIndexId, targetIdentity), out var targets)
                : methodsByMemberReference.TryGetValue(targetIdentity, out targets);
            if (!matched
                || targets is not { Length: 1 })
            {
                if (referenceKind == "methoddef"
                    || admittedReferencePrefixes.Any(prefix => targetIdentity.StartsWith(prefix, StringComparison.Ordinal)))
                {
                    AddCompiledIlGap(graph, call,
                        targets is { Length: > 1 } ? "CompiledIlTargetAmbiguous" : "CompiledIlTargetUnavailable",
                        referenceKind == "methoddef" ? "same-assembly-methoddef-target-not-unique" : "admitted-memberref-target-not-unique",
                        targets?.Length);
                }
                continue;
            }

            var target = targets[0];
            if (target.Properties.GetValueOrDefault("provenanceState") != "bound")
                continue;
            var from = graph.GetOrAddSymbolNode(caller.SourceIndexId, caller.SourceLabel, caller.TargetSymbol!,
                caller.FilePath, caller.StartLine, caller.EndLine, caller.RuleId, caller.EvidenceTier);
            var to = graph.GetOrAddSymbolNode(target.SourceIndexId, target.SourceLabel, target.TargetSymbol!,
                target.FilePath, target.StartLine, target.EndLine, target.RuleId, target.EvidenceTier);
            var virtualCandidate = opcode == "callvirt";
            graph.AddEdge(new GraphEdge(
                $"compiled-il-call:{call.CombinedFactId}", virtualCandidate ? "compiled-il-callvirt-candidate" : "compiled-il-call",
                from.NodeId, to.NodeId, "EvidenceEdge", CompiledIlBridgeRuleId,
                virtualCandidate ? EvidenceTiers.Tier3SyntaxOrTextual : EvidenceTiers.Tier2Structural,
                [call.CombinedFactId, body.CombinedFactId, caller.CombinedFactId, target.CombinedFactId],
                [], SafePath(call.FilePath), call.StartLine, call.EndLine));
        }
    }

    private static void AddProjectlessPublishCandidateEdges(EvidenceGraph graph, IReadOnlyList<CombinedFactRow> facts)
    {
        var maps = facts.Where(fact => fact.FactType == FactTypes.WebFormsPublishPageMapped
                && fact.RuleId == RuleIds.LegacyWebFormsPublishMap
                && fact.EvidenceTier == EvidenceTiers.Tier2Structural)
            .OrderBy(fact => fact.CombinedFactId, StringComparer.Ordinal).ToArray();
        foreach (var map in maps)
        {
            var sourcePath = map.Properties.GetValueOrDefault("sourcePath");
            var rawSha = map.Properties.GetValueOrDefault("assemblyRawSha256");
            var generatedType = map.Properties.GetValueOrDefault("generatedType");
            if (string.IsNullOrWhiteSpace(sourcePath) || sourcePath != map.FilePath
                || string.IsNullOrWhiteSpace(rawSha) || string.IsNullOrWhiteSpace(generatedType)
                || string.IsNullOrWhiteSpace(map.Properties.GetValueOrDefault("boundedInputSha256"))
                || string.IsNullOrWhiteSpace(map.Properties.GetValueOrDefault("generatorSha256")))
                continue;

            var pages = facts.Where(fact => fact.SourceIndexId == map.SourceIndexId
                    && fact.FactType == FactTypes.WebFormsPageDeclared
                    && fact.FilePath == sourcePath)
                .ToArray();
            if (pages.Length != 1 || !TrySimpleTypePath(pages[0].Properties.GetValueOrDefault("pageTypeName"), out var sourceType))
            {
                AddCompiledIlGap(graph, map, "ProjectlessPublishPageUnavailable",
                    "one-exact-page-and-qualified-source-type-required", pages.Length, ProjectlessPublishCandidateRuleId);
                continue;
            }

            var generatedTypes = facts.Where(fact => fact.SourceIndexId == map.SourceIndexId
                    && fact.FactType == FactTypes.ManagedTypeDeclared
                    && fact.Properties.GetValueOrDefault("provenanceState") == "bound"
                    && fact.Properties.GetValueOrDefault("rawFileSha256") == rawSha
                    && MatchesTypePath(fact.TargetSymbol, generatedType))
                .ToArray();
            if (generatedTypes.Length != 1)
            {
                AddCompiledIlGap(graph, map, "ProjectlessPublishGeneratedTypeUnavailable",
                    "mapped-generated-type-not-unique-in-bound-assembly", generatedTypes.Length, ProjectlessPublishCandidateRuleId);
                continue;
            }

            foreach (var handler in facts.Where(fact => fact.SourceIndexId == map.SourceIndexId
                         && fact.FactType == FactTypes.WebFormsHandlerResolved
                         && fact.Properties.GetValueOrDefault("markupFile") == sourcePath
                         && fact.Properties.GetValueOrDefault("pageTypeName") == pages[0].Properties.GetValueOrDefault("pageTypeName"))
                     .OrderBy(fact => fact.CombinedFactId, StringComparer.Ordinal))
            {
                var name = handler.Properties.GetValueOrDefault("handlerName");
                var linkedCode = handler.Properties.GetValueOrDefault("linkedCodePath");
                var sourceSymbol = handler.Properties.GetValueOrDefault("handlerSymbol");
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(linkedCode)
                    || string.IsNullOrWhiteSpace(sourceSymbol) || linkedCode != handler.FilePath)
                    continue;
                var linkedBindings = facts.Where(fact => fact.SourceIndexId == map.SourceIndexId
                        && fact.FactType == FactTypes.WebFormsPublishSourceBound
                        && fact.RuleId == RuleIds.LegacyWebFormsPublishMap
                        && fact.EvidenceTier == EvidenceTiers.Tier2Structural
                        && fact.FilePath == linkedCode
                        && fact.Properties.GetValueOrDefault("sourcePath") == linkedCode
                        && fact.Properties.GetValueOrDefault("boundedInputSha256") == map.Properties.GetValueOrDefault("boundedInputSha256")
                        && fact.Properties.GetValueOrDefault("generatorSha256") == map.Properties.GetValueOrDefault("generatorSha256"))
                    .ToArray();
                if (linkedBindings.Length != 1)
                {
                    AddCompiledIlGap(graph, handler, "ProjectlessPublishSourceAmbiguous",
                        "one-receipt-bound-linked-code-file-required", linkedBindings.Length, ProjectlessPublishCandidateRuleId);
                    continue;
                }
                var declarations = facts.Where(fact => fact.SourceIndexId == map.SourceIndexId
                        && fact.FactType == FactTypes.MethodDeclared
                        && fact.RuleId == RuleIds.VisualBasicSyntaxDeclarations
                        && fact.FilePath == linkedCode
                        && fact.Properties.GetValueOrDefault("qualifiedContainingType") == pages[0].Properties.GetValueOrDefault("pageTypeName")
                        && fact.Properties.GetValueOrDefault("name") == name
                        && fact.StartLine == handler.StartLine)
                    .ToArray();
                if (declarations.Length != 1)
                {
                    AddCompiledIlGap(graph, handler, "ProjectlessPublishSourceAmbiguous",
                        "one-exact-qualified-handler-declaration-required", declarations.Length, ProjectlessPublishCandidateRuleId);
                    continue;
                }
                var methods = facts.Where(fact => fact.SourceIndexId == map.SourceIndexId
                        && fact.FactType == FactTypes.ManagedMethodDeclared
                        && fact.Properties.GetValueOrDefault("provenanceState") == "bound"
                        && fact.Properties.GetValueOrDefault("rawFileSha256") == rawSha
                        && fact.Properties.GetValueOrDefault("metadataName") == name
                        && fact.TargetSymbol?.Contains("|type:" + sourceType + "|arity:0|method:", StringComparison.Ordinal) == true)
                    .ToArray();
                if (methods.Length != 1)
                {
                    AddCompiledIlGap(graph, handler, "ProjectlessPublishMetadataAmbiguous",
                        "one-bound-qualified-method-required", methods.Length, ProjectlessPublishCandidateRuleId);
                    continue;
                }

                var sourceNode = graph.GetOrAddSymbolNode(handler.SourceIndexId, handler.SourceLabel,
                    sourceSymbol, handler.FilePath, handler.StartLine, handler.EndLine, handler.RuleId, handler.EvidenceTier);
                var compiled = methods[0];
                var compiledNode = graph.GetOrAddSymbolNode(compiled.SourceIndexId, compiled.SourceLabel,
                    compiled.TargetSymbol!, compiled.FilePath, compiled.StartLine, compiled.EndLine, compiled.RuleId, compiled.EvidenceTier);
                graph.AddEdge(new GraphEdge(
                    $"projectless-publish-candidate:{handler.CombinedFactId}:{compiled.CombinedFactId}",
                    "projectless-publish-method-candidate", sourceNode.NodeId, compiledNode.NodeId,
                    "EvidenceEdge", ProjectlessPublishCandidateRuleId, EvidenceTiers.Tier3SyntaxOrTextual,
                    [map.CombinedFactId, linkedBindings[0].CombinedFactId, pages[0].CombinedFactId, generatedTypes[0].CombinedFactId,
                        handler.CombinedFactId, declarations[0].CombinedFactId, compiled.CombinedFactId],
                    [], SafePath(handler.FilePath), handler.StartLine, handler.EndLine));
            }
        }
    }

    private static bool MatchesTypePath(string? identity, string typeName) =>
        TrySimpleTypePath(typeName, out var typePath)
        && identity?.Contains("|type:" + typePath + "|arity:0", StringComparison.Ordinal) == true;

    private static void AddProjectlessPublishMemberCandidates(EvidenceGraph graph, IReadOnlyList<CombinedFactRow> facts)
    {
        var pageSourceKeys = facts.Where(fact => fact.FactType == FactTypes.WebFormsPublishPageMapped)
            .Select(fact => (fact.SourceIndexId, fact.FilePath)).ToHashSet();
        foreach (var mapped in facts.Where(fact => fact.FactType == FactTypes.WebFormsPublishPageMapped))
        {
            foreach (var page in facts.Where(fact => fact.SourceIndexId == mapped.SourceIndexId
                         && fact.FactType == FactTypes.WebFormsPageDeclared
                         && fact.FilePath == mapped.FilePath))
            {
                var linkedCode = page.Properties.GetValueOrDefault("linkedCodePath");
                if (!string.IsNullOrWhiteSpace(linkedCode)) pageSourceKeys.Add((mapped.SourceIndexId, linkedCode));
            }
        }
        var assemblies = facts.Where(fact => fact.FactType == FactTypes.WebFormsPublishAssemblyBound
                && fact.RuleId == RuleIds.LegacyWebFormsPublishMap
                && fact.EvidenceTier == EvidenceTiers.Tier2Structural)
            .ToArray();
        var sourceInputs = facts.Where(fact => fact.FactType == FactTypes.WebFormsPublishSourceBound
                     && fact.RuleId == RuleIds.LegacyWebFormsPublishMap
                     && fact.EvidenceTier == EvidenceTiers.Tier2Structural
                     && !pageSourceKeys.Contains((fact.SourceIndexId, fact.FilePath)))
                 .OrderBy(fact => fact.CombinedFactId, StringComparer.Ordinal).ToArray();
        var declarationsByFile = facts.Where(fact => fact.FactType == FactTypes.MethodDeclared
                && fact.RuleId == RuleIds.VisualBasicSyntaxDeclarations)
            .GroupBy(fact => (fact.SourceIndexId, fact.FilePath))
            .ToDictionary(group => group.Key, group => group.ToArray());
        var methodsByName = facts.Where(fact => fact.FactType == FactTypes.ManagedMethodDeclared
                && fact.Properties.GetValueOrDefault("provenanceState") == "bound")
            .GroupBy(fact => (fact.SourceIndexId, fact.Properties.GetValueOrDefault("metadataName")))
            .ToDictionary(group => group.Key, group => group.ToArray());
        long candidateWork = 0;
        foreach (var source in sourceInputs)
        {
            if (!declarationsByFile.TryGetValue((source.SourceIndexId, source.FilePath), out var declarations)) continue;
            foreach (var declaration in declarations)
            {
                var key = (source.SourceIndexId, declaration.Properties.GetValueOrDefault("name"));
                candidateWork += methodsByName.GetValueOrDefault(key)?.Length ?? 0;
                if (candidateWork <= 100_000) continue;
                AddCompiledIlGap(graph, source, "ProjectlessPublishMemberWorkLimit",
                    "bounded-publish-member-join-work-exceeded", null, ProjectlessPublishCandidateRuleId);
                return;
            }
        }
        foreach (var source in sourceInputs)
        {
            var boundedInput = source.Properties.GetValueOrDefault("boundedInputSha256");
            if (string.IsNullOrWhiteSpace(boundedInput) || source.Properties.GetValueOrDefault("sourcePath") != source.FilePath)
                continue;
            var boundAssemblies = assemblies.Where(assembly => assembly.SourceIndexId == source.SourceIndexId
                    && assembly.Properties.GetValueOrDefault("boundedInputSha256") == boundedInput)
                .ToArray();
            var hashes = boundAssemblies.Select(assembly => assembly.Properties.GetValueOrDefault("assemblyRawSha256"))
                .Where(hash => !string.IsNullOrWhiteSpace(hash)).ToHashSet(StringComparer.Ordinal);
            if (hashes.Count == 0) continue;

            if (!declarationsByFile.TryGetValue((source.SourceIndexId, source.FilePath), out var sourceDeclarations))
                continue;
            foreach (var declaration in sourceDeclarations.OrderBy(fact => fact.CombinedFactId, StringComparer.Ordinal))
            {
                var typeName = declaration.Properties.GetValueOrDefault("qualifiedContainingType");
                var name = declaration.Properties.GetValueOrDefault("name");
                var memberIdentity = declaration.Properties.GetValueOrDefault("memberIdentity");
                if (!TrySimpleTypePath(typeName, out var typePath) || string.IsNullOrWhiteSpace(name)
                    || name == "New" || string.IsNullOrWhiteSpace(memberIdentity))
                    continue;
                var candidates = (methodsByName.GetValueOrDefault((source.SourceIndexId, name)) ?? [])
                    .Where(fact => hashes.Contains(fact.Properties.GetValueOrDefault("rawFileSha256"))
                        && fact.TargetSymbol?.Contains("|type:" + typePath + "|arity:0|method:", StringComparison.Ordinal) == true
                        && PublishCandidateSignatureMatches(declaration, fact))
                    .ToArray();
                if (candidates.Length != 1)
                {
                    AddCompiledIlGap(graph, declaration, "ProjectlessPublishMemberAmbiguous",
                        "one-qualified-published-member-with-compatible-parameter-shapes-required",
                        candidates.Length, ProjectlessPublishCandidateRuleId);
                    continue;
                }
                var compiled = candidates[0];
                var matchingAssembly = boundAssemblies.Where(assembly =>
                    assembly.Properties.GetValueOrDefault("assemblyRawSha256") == compiled.Properties.GetValueOrDefault("rawFileSha256"))
                    .ToArray();
                if (matchingAssembly.Length != 1)
                {
                    AddCompiledIlGap(graph, declaration, "ProjectlessPublishAssemblyAmbiguous",
                        "one-declared-published-assembly-required", matchingAssembly.Length, ProjectlessPublishCandidateRuleId);
                    continue;
                }
                var sourceNode = graph.GetOrAddSymbolNode(declaration.SourceIndexId, declaration.SourceLabel,
                    memberIdentity, declaration.FilePath, declaration.StartLine, declaration.EndLine,
                    declaration.RuleId, declaration.EvidenceTier);
                var compiledNode = graph.GetOrAddSymbolNode(compiled.SourceIndexId, compiled.SourceLabel,
                    compiled.TargetSymbol!, compiled.FilePath, compiled.StartLine, compiled.EndLine,
                    compiled.RuleId, compiled.EvidenceTier);
                foreach (var (from, to, direction) in new[]
                         {
                             (sourceNode, compiledNode, "source-to-compiled"),
                             (compiledNode, sourceNode, "compiled-to-source")
                         })
                    graph.AddEdge(new GraphEdge(
                        $"projectless-publish-member:{declaration.CombinedFactId}:{compiled.CombinedFactId}:{direction}",
                        "projectless-publish-member-candidate", from.NodeId, to.NodeId,
                        "EvidenceEdge", ProjectlessPublishCandidateRuleId, EvidenceTiers.Tier3SyntaxOrTextual,
                        [source.CombinedFactId, matchingAssembly[0].CombinedFactId,
                            declaration.CombinedFactId, compiled.CombinedFactId],
                        [], SafePath(declaration.FilePath), declaration.StartLine, declaration.EndLine));
            }
        }
    }

    private static bool PublishCandidateSignatureMatches(CombinedFactRow source, CombinedFactRow compiled)
    {
        var rawSyntaxTypes = source.Properties.GetValueOrDefault("parameterTypes");
        var syntaxTypes = string.IsNullOrEmpty(rawSyntaxTypes) ? [] : rawSyntaxTypes.Split(';');
        if (!int.TryParse(source.Properties.GetValueOrDefault("parameterCount"), out var sourceCount)
            || sourceCount != syntaxTypes.Length
            || !TrySignatureParameters(compiled.Properties.GetValueOrDefault("signature"), out var metadataTypes)
            || sourceCount != metadataTypes.Length)
            return false;
        var lexicalNamespace = source.Properties.GetValueOrDefault("lexicalNamespace") ?? string.Empty;
        var importedNamespaces = source.Properties.GetValueOrDefault("importedNamespaces") ?? string.Empty;
        return syntaxTypes.Zip(metadataTypes).All(pair =>
            PublishParameterMatches(pair.First, pair.Second, lexicalNamespace, importedNamespaces));
    }

    private static bool TrySignatureParameters(string? signature, out string[] parameters)
    {
        parameters = [];
        if (string.IsNullOrWhiteSpace(signature) || signature.Length > 16_384) return false;
        var open = signature.IndexOf("|(", StringComparison.Ordinal);
        if (open < 0) return false;
        var start = open + 2;
        var depth = 1;
        var parts = new List<string>();
        for (var index = start; index < signature.Length; index++)
        {
            if (signature[index] == '(') depth++;
            else if (signature[index] == ')' && --depth == 0)
            {
                if (!signature.AsSpan(index).StartsWith(")->", StringComparison.Ordinal)) return false;
                if (index > start) parts.Add(signature[start..index]);
                parameters = parts.ToArray();
                return true;
            }
            else if (signature[index] == ',' && depth == 1)
            {
                if (index == start) return false;
                parts.Add(signature[start..index]);
                start = index + 1;
            }
            if (depth < 1) return false;
        }
        return false;
    }

    internal static bool PublishParameterMatches(string syntaxType, string metadataType,
        string lexicalNamespace = "", string importedNamespaces = "")
    {
        var syntax = syntaxType.Trim();
        var array = syntax.EndsWith("()", StringComparison.Ordinal);
        if (array) syntax = syntax[..^2];
        if (syntax.Length == 0 || syntax.Equals("unavailable", StringComparison.OrdinalIgnoreCase)) return false;
        var expected = syntax.ToUpperInvariant() switch
        {
            "STRING" => "System.String",
            "OBJECT" => "System.Object",
            "INTEGER" => "System.Int32",
            "BOOLEAN" => "System.Boolean",
            "LONG" => "System.Int64",
            "SHORT" => "System.Int16",
            "BYTE" => "System.Byte",
            "DECIMAL" => "System.Decimal",
            "DOUBLE" => "System.Double",
            "SINGLE" => "System.Single",
            "CHAR" => "System.Char",
            "DATE" => "System.DateTime",
            _ => syntax.StartsWith("Global.", StringComparison.OrdinalIgnoreCase) ? syntax[7..] : syntax
        };
        var value = metadataType.Trim();
        if (array)
        {
            if (!value.EndsWith("[]", StringComparison.Ordinal)) return false;
            value = value[..^2];
        }
        else if (value.EndsWith("[]", StringComparison.Ordinal)) return false;
        if (!value.StartsWith("type(", StringComparison.Ordinal)
            && !value.StartsWith("scope(", StringComparison.Ordinal)) return false;
        var marker = value.LastIndexOf("type(namespace:", StringComparison.Ordinal);
        if (marker < 0 || !value.EndsWith(')') || !TryMetadataTypeName(value[(marker + 5)..^1], out var fullName))
            return false;
        if (expected.Contains('.', StringComparison.Ordinal))
            return string.Equals(expected, fullName, StringComparison.OrdinalIgnoreCase);

        // A bare user-defined type name is not enough to bind an arbitrary
        // metadata namespace. It must be reachable through lexical scope or
        // an explicit, non-aliased Imports clause retained with the source.
        var qualifiedScopes = importedNamespaces
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Append(lexicalNamespace.Trim())
            .Where(scope => scope.Length > 0)
            .SelectMany(scope =>
            {
                var namespaceCandidate = scope + "." + expected;
                return scope.EndsWith("." + expected, StringComparison.OrdinalIgnoreCase)
                    ? new[] { scope, namespaceCandidate }
                    : new[] { namespaceCandidate };
            })
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (qualifiedScopes.Length == 0)
            return string.Equals(expected, fullName, StringComparison.OrdinalIgnoreCase);
        return qualifiedScopes.Count(candidate =>
            string.Equals(candidate, fullName, StringComparison.OrdinalIgnoreCase)) == 1;
    }

    private static bool TryMetadataTypeName(string path, out string fullName)
    {
        fullName = string.Empty;
        var position = 0;
        if (!ConsumeLiteral(path, ref position, "namespace:")
            || !ReadComponent(path, ref position, out var ns)
            || !ConsumeLiteral(path, ref position, "|names:")
            || !ReadComponent(path, ref position, out var name)
            || position != path.Length || name.Length == 0)
            return false;
        fullName = ns.Length == 0 ? name : ns + "." + name;
        return true;
    }

    private static bool ReadComponent(string value, ref int position, out string component)
    {
        component = string.Empty;
        var start = position;
        while (position < value.Length && char.IsAsciiDigit(value[position])) position++;
        if (position == start || position >= value.Length || value[position] != ':'
            || !int.TryParse(value.AsSpan(start, position - start), out var length)
            || length < 0 || length > value.Length - position - 1) return false;
        component = value.Substring(position + 1, length);
        position += length + 1;
        return true;
    }

    private static bool TrySimpleTypePath(string? qualifiedName, out string typePath)
    {
        typePath = string.Empty;
        if (string.IsNullOrWhiteSpace(qualifiedName) || qualifiedName.StartsWith("global::", StringComparison.Ordinal)
            || qualifiedName.StartsWith("Global.", StringComparison.OrdinalIgnoreCase))
            return false;
        var lastDot = qualifiedName.LastIndexOf('.');
        var namespaceName = lastDot < 0 ? string.Empty : qualifiedName[..lastDot];
        var name = qualifiedName[(lastDot + 1)..];
        if (name.Length == 0 || (namespaceName.Length > 0 && namespaceName.Split('.').Any(part => part.Length == 0))
            || name.Contains('+', StringComparison.Ordinal) || name.Contains('`', StringComparison.Ordinal))
            return false;
        typePath = $"namespace:{namespaceName.Length}:{namespaceName}|names:{name.Length}:{name}";
        return true;
    }

    private static void AddProjectlessPdbIdentityEdges(EvidenceGraph graph, IReadOnlyList<CombinedFactRow> facts)
    {
        var declarations = facts.Where(fact => fact.FactType == FactTypes.MethodDeclared
                && fact.RuleId == RuleIds.VisualBasicSyntaxDeclarations
                && fact.EvidenceTier == EvidenceTiers.Tier3SyntaxOrTextual
                && !string.IsNullOrWhiteSpace(fact.Properties.GetValueOrDefault("memberIdentity"))
                && int.TryParse(fact.Properties.GetValueOrDefault("bodyStartLine"), out _)
                && int.TryParse(fact.Properties.GetValueOrDefault("bodyEndLine"), out _))
            .ToArray();
        if (declarations.Length == 0) return;
        var declarationsByFile = declarations
            .GroupBy(fact => (fact.SourceIndexId, fact.FilePath))
            .ToDictionary(group => group.Key, group => group.ToArray());

        var byOriginalId = facts.GroupBy(fact => (fact.SourceIndexId, fact.OriginalFactId))
            .ToDictionary(group => group.Key, group => group.ToArray());
        var methodIdentityCounts = facts.Where(fact => fact.FactType == FactTypes.ManagedMethodDeclared
                && !string.IsNullOrWhiteSpace(fact.TargetSymbol))
            .GroupBy(fact => (fact.SourceIndexId, fact.TargetSymbol!))
            .ToDictionary(group => group.Key, group => group.Count());
        var documentJoins = facts.Where(fact => fact.FactType == FactTypes.PdbSourceDocumentReconciled
                && fact.Properties.GetValueOrDefault("pdbProvenanceState") == "bound")
            .GroupBy(fact => (fact.SourceIndexId, fact.Properties.GetValueOrDefault("pdbDocumentFactId")))
            .ToDictionary(group => group.Key, group => group.ToArray());
        var pointsByMethodJoin = facts.Where(fact => fact.FactType == FactTypes.PdbSequencePointDeclared)
            .GroupBy(fact => (fact.SourceIndexId, fact.Properties.GetValueOrDefault("metadataPdbReconciliationFactId")))
            .ToDictionary(group => group.Key, group => group.ToArray());

        foreach (var join in facts.Where(fact => fact.FactType == FactTypes.MetadataPdbMethodReconciled
                     && fact.Properties.GetValueOrDefault("pdbProvenanceState") == "bound")
                 .OrderBy(fact => fact.CombinedFactId, StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(join.Properties.GetValueOrDefault("pdbBoundedInputSha256"))
                || string.IsNullOrWhiteSpace(join.Properties.GetValueOrDefault("pdbGeneratorSha256"))
                || string.IsNullOrWhiteSpace(join.Properties.GetValueOrDefault("provenanceBindingInputSha256"))
                || !TryUniqueFact(byOriginalId, join.SourceIndexId, join.Properties.GetValueOrDefault("compiledFactId"), out var method)
                || method.FactType != FactTypes.ManagedMethodDeclared
                || method.Properties.GetValueOrDefault("provenanceState") != "bound"
                || method.TargetSymbol != join.Properties.GetValueOrDefault("metadataIdentity")
                || !TryUniqueFact(byOriginalId, join.SourceIndexId, join.Properties.GetValueOrDefault("pdbMethodFactId"), out var pdbMethod)
                || pdbMethod.FactType != FactTypes.PdbMethodDeclared
                || !pointsByMethodJoin.TryGetValue((join.SourceIndexId, join.OriginalFactId), out var allPoints))
                continue;
            if (!methodIdentityCounts.TryGetValue((join.SourceIndexId, method.TargetSymbol!), out var identityCount)
                || identityCount != 1)
            {
                AddCompiledIlGap(graph, join, "ProjectlessPdbMetadataAmbiguous",
                    "bound-metadata-method-identity-not-unique", identityCount, ProjectlessPdbIdentityRuleId);
                continue;
            }

            var points = allPoints.Where(point => point.Properties.GetValueOrDefault("hidden") == "false").ToArray();
            if (points.Length == 0 || points.Any(point => point.Properties.GetValueOrDefault("pdbMethodFactId") != pdbMethod.OriginalFactId))
                continue;
            var docIds = points.Select(point => point.Properties.GetValueOrDefault("pdbDocumentFactId"))
                .Distinct(StringComparer.Ordinal).ToArray();
            if (docIds.Length != 1 || string.IsNullOrWhiteSpace(docIds[0])
                || !documentJoins.TryGetValue((join.SourceIndexId, docIds[0]), out var docs)
                || docs.Length != 1
                || !TryUniqueFact(byOriginalId, join.SourceIndexId, docIds[0], out var pdbDocument)
                || pdbDocument.FactType != FactTypes.PdbDocumentDeclared
                || docs[0].Properties.GetValueOrDefault("pdbInputFactId") != join.Properties.GetValueOrDefault("pdbInputFactId"))
                continue;
            var document = docs[0];
            var sourcePath = document.Properties.GetValueOrDefault("sourcePath");
            var metadataName = method.Properties.GetValueOrDefault("metadataName");
            if (string.IsNullOrWhiteSpace(sourcePath) || string.IsNullOrWhiteSpace(metadataName)
                || points.Any(point => point.FilePath != sourcePath
                    || !int.TryParse(point.Properties.GetValueOrDefault("startLine"), out _)
                    || !int.TryParse(point.Properties.GetValueOrDefault("endLine"), out _)))
                continue;

            var lookupName = metadataName == ".ctor" ? "New" : metadataName;
            var candidates = declarationsByFile.GetValueOrDefault((join.SourceIndexId, sourcePath), [])
                .Where(declaration =>
                    string.Equals(declaration.Properties.GetValueOrDefault("name"), lookupName, StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(declaration.Properties.GetValueOrDefault("bodyStartLine"), out var start)
                    && int.TryParse(declaration.Properties.GetValueOrDefault("bodyEndLine"), out var end)
                    && points.All(point => int.Parse(point.Properties["startLine"]) >= start
                        && int.Parse(point.Properties["endLine"]) <= end))
                .ToArray();
            if (candidates.Length != 1)
            {
                if (candidates.Length > 1)
                    AddCompiledIlGap(graph, join, "ProjectlessPdbMethodAmbiguous",
                        "exact-document-sequence-points-match-multiple-source-methods", candidates.Length,
                        ProjectlessPdbIdentityRuleId);
                continue;
            }
            var declaration = candidates[0];
            var sourceIdentity = declaration.Properties["memberIdentity"];
            var sourceNode = graph.GetOrAddSymbolNode(declaration.SourceIndexId, declaration.SourceLabel,
                sourceIdentity, declaration.FilePath, declaration.StartLine, declaration.EndLine,
                declaration.RuleId, declaration.EvidenceTier);
            var methodNode = graph.GetOrAddSymbolNode(method.SourceIndexId, method.SourceLabel,
                method.TargetSymbol!, method.FilePath, method.StartLine, method.EndLine,
                method.RuleId, method.EvidenceTier);
            graph.AddEdge(new GraphEdge(
                $"projectless-source-pdb-identity:{join.CombinedFactId}:{declaration.CombinedFactId}",
                "projectless-source-pdb-identity", sourceNode.NodeId, methodNode.NodeId,
                "EvidenceEdge", ProjectlessPdbIdentityRuleId, EvidenceTiers.Tier2Structural,
                points.Select(point => point.CombinedFactId)
                    .Append(document.CombinedFactId).Append(join.CombinedFactId)
                    .Append(pdbDocument.CombinedFactId).Append(pdbMethod.CombinedFactId)
                    .Append(declaration.CombinedFactId).Append(method.CombinedFactId)
                    .Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                [], SafePath(declaration.FilePath), declaration.StartLine, declaration.EndLine));
        }
    }

    private static bool TryUniqueFact(
        IReadOnlyDictionary<(string SourceIndexId, string OriginalFactId), CombinedFactRow[]> factsByOriginalId,
        string sourceIndexId,
        string? originalFactId,
        out CombinedFactRow fact)
    {
        fact = null!;
        if (string.IsNullOrWhiteSpace(originalFactId)
            || !factsByOriginalId.TryGetValue((sourceIndexId, originalFactId), out var candidates)
            || candidates.Length != 1)
            return false;
        fact = candidates[0];
        return true;
    }

    private static string? ExpectedMemberReference(CombinedFactRow method)
    {
        var assemblyIdentity = method.Properties.GetValueOrDefault("assemblyIdentity");
        var assemblyReference = method.Properties.GetValueOrDefault("assemblyReferenceIdentity");
        var name = method.Properties.GetValueOrDefault("metadataName");
        var signature = method.Properties.GetValueOrDefault("signature");
        var identity = method.TargetSymbol;
        if (string.IsNullOrWhiteSpace(assemblyIdentity) || string.IsNullOrWhiteSpace(assemblyReference)
            || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(signature)
            || string.IsNullOrWhiteSpace(identity)
            || !identity.StartsWith(assemblyIdentity + "|type:", StringComparison.Ordinal))
            return null;

        // Type paths are length-framed. Consume every component by its declared
        // UTF-16 length; a name containing '|arity:' must not redirect the join.
        var position = assemblyIdentity.Length + "|type:".Length;
        var typeStart = position;
        if (!ConsumeLiteral(identity, ref position, "namespace:")
            || !ConsumeComponent(identity, ref position)
            || !ConsumeLiteral(identity, ref position, "|names:"))
            return null;
        var nameCount = 0;
        while (position < identity.Length && char.IsAsciiDigit(identity[position]))
        {
            if (!ConsumeComponent(identity, ref position)) return null;
            nameCount++;
        }
        if (nameCount == 0) return null;
        var typePath = identity[typeStart..position];
        if (!ConsumeLiteral(identity, ref position, "|arity:")) return null;
        var arityStart = position;
        while (position < identity.Length && char.IsAsciiDigit(identity[position])) position++;
        if (position == arityStart || identity[arityStart..position] != "0") return null;
        var kind = name is ".ctor" or ".cctor" ? "|constructor:" : "|method:";
        if (!ConsumeLiteral(identity, ref position, kind)
            || !ConsumeComponent(identity, ref position)
            || !ConsumeLiteral(identity, ref position, "|")
            || identity[position..] != signature)
            return null;

        return $"memberref|type:scope({assemblyReference})type({typePath})|member:{name.Length}:{name}|{signature}";
    }

    private static bool ConsumeLiteral(string value, ref int position, string literal)
    {
        if (!value.AsSpan(position).StartsWith(literal, StringComparison.Ordinal)) return false;
        position += literal.Length;
        return true;
    }

    private static bool ConsumeComponent(string value, ref int position)
    {
        var lengthStart = position;
        while (position < value.Length && char.IsAsciiDigit(value[position])) position++;
        if (position == lengthStart || position >= value.Length || value[position] != ':'
            || !int.TryParse(value.AsSpan(lengthStart, position - lengthStart), out var length)
            || length < 0 || length > value.Length - position - 1)
            return false;
        position += length + 1;
        return true;
    }

    private static void AddCompiledIlGap(EvidenceGraph graph, CombinedFactRow fact,
        string gapKind, string reason, int? candidateCount, string ruleId = CompiledIlBridgeRuleId)
    {
        graph.Gaps.Add(new CombinedPathGap(
            $"gap:compiled-il:{fact.CombinedFactId}:{gapKind}", gapKind,
            CombinedDependencyPathClassifications.UnknownAnalysisGap,
            "Bound compiled evidence could not be joined to one path-graph target; no edge was inferred.",
            fact.SourceIndexId, fact.SourceLabel, null, fact.CombinedFactId,
            ruleId, EvidenceTiers.Tier4Unknown,
            SafePath(fact.FilePath), fact.StartLine, reason,
            fact.CommitSha, fact.ExtractorVersion, "bound-compiled-il",
            fact.EndLine, candidateCount, SupportingFactIds: [fact.CombinedFactId]));
    }
}
