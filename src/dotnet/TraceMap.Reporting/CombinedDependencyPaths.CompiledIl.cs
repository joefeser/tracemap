using TraceMap.Core;

namespace TraceMap.Reporting;

public static partial class CombinedDependencyPathReporter
{
    private const string CompiledIlBridgeRuleId = "combined.paths.compiled-il-bridge.v1";
    private const string ProjectlessPdbIdentityRuleId = "combined.paths.projectless-pdb-identity.v1";

    private static void AddBoundCompiledIlEdges(EvidenceGraph graph, IReadOnlyList<CombinedFactRow> facts)
    {
        AddProjectlessPdbIdentityEdges(graph, facts);
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
