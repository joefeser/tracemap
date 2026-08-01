using System.Text;
using System.Text.RegularExpressions;
using TraceMap.Core;

namespace TraceMap.Access;

internal sealed record AccessRawVbaModule(string Name, string ModuleKind, string Source);

internal sealed record AccessRawEventProcedureReference(
    string OwnerStableKey,
    string EventRole,
    string ModuleName,
    string ProcedureName,
    string OwnerKind = "unknown",
    string BindingKind = "event-procedure",
    string? EventExpressionHash = null,
    int EventExpressionLength = 0);

internal sealed record AccessVbaProjectionResult(
    IReadOnlyList<AccessVbaModuleProjection> Modules,
    IReadOnlyList<AccessEventBindingProjection> EventBindings,
    IReadOnlyList<AccessGapProjection> Gaps);

internal static partial class AccessVbaProjector
{
    private sealed record ProcedureWork(
        string RawName,
        AccessVbaProcedureProjection Projection,
        int BodyStartIndex,
        int BodyEndIndex);

    private sealed record ModuleWork(
        string RawName,
        AccessVbaModuleProjection Projection,
        IReadOnlyList<ProcedureWork> Procedures);

    public static AccessVbaProjectionResult Project(
        string databaseIdentitySeed,
        IReadOnlyList<AccessRawVbaModule> rawModules,
        IReadOnlyList<AccessRawEventProcedureReference>? eventReferences = null,
        IReadOnlyDictionary<string, IReadOnlyList<(string StableKey, string Kind)>>? knownObjects = null,
        AccessLimits? limits = null,
        AccessIdentityDisclosurePolicy disclosurePolicy = AccessIdentityDisclosurePolicy.SafeIdentifier)
    {
        limits ??= AccessLimits.Default;
        var gaps = new List<AccessGapProjection>();
        var modules = new List<ModuleWork>();
        foreach (var raw in rawModules
                     .OrderBy(item => item.ModuleKind, StringComparer.Ordinal)
                     .ThenBy(item => AccessSafeValues.RoleHash("access-vba-module-sort", item.Name), StringComparer.Ordinal)
                     .Take(limits.MaxObjectsPerCollection))
        {
            if (raw.Source.Length > limits.MaxVbaModuleTextLength)
            {
                gaps.Add(new("AccessVbaModuleTextLimitReached", "vba-module", null, RuleIds.LegacyAccessVba));
                continue;
            }

            var lines = NormalizeLines(raw.Source);
            if (lines.Length > limits.MaxVbaModuleLines)
            {
                gaps.Add(new("AccessVbaModuleLineLimitReached", "vba-module", null, RuleIds.LegacyAccessVba));
                continue;
            }

            var moduleIdentity = AccessSafeValues.Identity(databaseIdentitySeed, "vba-module", raw.Name, disclosurePolicy: disclosurePolicy);
            var moduleGapStart = gaps.Count;
            var procedureWork = ParseProcedureDeclarations(databaseIdentitySeed, moduleIdentity, lines, limits, gaps, disclosurePolicy);
            var procedures = procedureWork
                .Select(work => work.Projection with
                {
                    Calls = ProjectCalls(databaseIdentitySeed, moduleIdentity, work, procedureWork, lines, knownObjects, limits, gaps, disclosurePolicy),
                    Effects = ProjectEffects(databaseIdentitySeed, work, lines, disclosurePolicy)
                })
                .ToArray();
            var updatedWork = procedureWork.Zip(procedures, (work, projection) => work with { Projection = projection }).ToArray();
            modules.Add(new(
                raw.Name,
                new(
                    moduleIdentity,
                    NormalizeModuleKind(raw.ModuleKind),
                    AccessSafeValues.RoleHash("access-vba-module-source", raw.Source),
                    lines.Length,
                    procedures,
                    procedures.Any(procedure => procedure.Calls.Any(call => call.Coverage != "complete"))
                        || gaps.Count > moduleGapStart
                            ? "partial"
                            : "complete"),
                updatedWork));
        }

        if (rawModules.Count > limits.MaxObjectsPerCollection)
            gaps.Add(new("AccessVbaModuleCollectionLimitReached", "vba-project", null, RuleIds.LegacyAccessVba));

        var bindings = MapEventProcedures(databaseIdentitySeed, modules, eventReferences ?? [], gaps, disclosurePolicy);
        return new(
            modules.Select(item => item.Projection).OrderBy(item => item.Identity.StableKey, StringComparer.Ordinal).ToArray(),
            bindings,
            gaps.OrderBy(item => item.Classification, StringComparer.Ordinal)
                .ThenBy(item => item.StableScopeKey, StringComparer.Ordinal)
                .ToArray());
    }

    private static IReadOnlyList<ProcedureWork> ParseProcedureDeclarations(
        string databaseIdentitySeed,
        AccessSafeIdentity moduleIdentity,
        string[] lines,
        AccessLimits limits,
        List<AccessGapProjection> gaps,
        AccessIdentityDisclosurePolicy disclosurePolicy)
    {
        var declarations = new List<(string Name, string Kind, int StartIndex)>();
        for (var index = 0; index < lines.Length; index++)
        {
            var match = ProcedureDeclarationPattern().Match(MaskCommentsAndStrings(lines[index]));
            if (!match.Success) continue;
            if (declarations.Count >= limits.MaxVbaProceduresPerModule)
            {
                gaps.Add(new("AccessVbaProcedureLimitReached", "vba-module", moduleIdentity.StableKey, RuleIds.LegacyAccessVba));
                break;
            }
            declarations.Add((match.Groups["name"].Value, ProcedureKind(match.Groups["kind"].Value), index));
        }

        var result = new List<ProcedureWork>();
        for (var ordinal = 0; ordinal < declarations.Count; ordinal++)
        {
            var declaration = declarations[ordinal];
            var identity = AccessSafeValues.Identity(databaseIdentitySeed, $"vba-procedure-{moduleIdentity.StableKey}", declaration.Name, ordinal, disclosurePolicy);
            var searchEnd = ordinal + 1 < declarations.Count ? declarations[ordinal + 1].StartIndex - 1 : lines.Length - 1;
            var endIndex = -1;
            for (var index = declaration.StartIndex + 1; index <= searchEnd; index++)
            {
                var endMatch = ProcedureEndPattern().Match(MaskCommentsAndStrings(lines[index]));
                if (!endMatch.Success || !string.Equals(endMatch.Groups["kind"].Value, EndKind(declaration.Kind), StringComparison.OrdinalIgnoreCase)) continue;
                endIndex = index;
                break;
            }
            if (endIndex < 0)
            {
                endIndex = Math.Max(declaration.StartIndex, searchEnd);
                gaps.Add(new("AccessVbaProcedureEndUnavailable", "vba-procedure", identity.StableKey, RuleIds.LegacyAccessVba));
            }

            result.Add(new(
                declaration.Name,
                new(identity, moduleIdentity.StableKey, declaration.Kind, declaration.StartIndex + 1, endIndex + 1, []),
                declaration.StartIndex + 1,
                endIndex - 1));
        }
        return result;
    }

    private static IReadOnlyList<AccessVbaCallProjection> ProjectCalls(
        string databaseIdentitySeed,
        AccessSafeIdentity moduleIdentity,
        ProcedureWork procedure,
        IReadOnlyList<ProcedureWork> moduleProcedures,
        string[] lines,
        IReadOnlyDictionary<string, IReadOnlyList<(string StableKey, string Kind)>>? knownObjects,
        AccessLimits limits,
        List<AccessGapProjection> gaps,
        AccessIdentityDisclosurePolicy disclosurePolicy)
    {
        var calls = new List<AccessVbaCallProjection>();
        for (var index = procedure.BodyStartIndex; index <= procedure.BodyEndIndex && index < lines.Length; index++)
        {
            var sourceLine = CodeWithoutComment(lines[index]);
            var masked = MaskCommentsAndStrings(lines[index]);
            var lineNumber = index + 1;
            if (masked.TrimEnd().EndsWith('_'))
                gaps.Add(new("AccessVbaLineContinuationPartial", "vba-procedure", procedure.Projection.Identity.StableKey, RuleIds.LegacyAccessVba));

            foreach (Match match in DynamicDispatchPattern().Matches(masked))
            {
                AddDynamicCall(databaseIdentitySeed, moduleIdentity, procedure, lineNumber, sourceLine, calls, gaps, disclosurePolicy);
                if (calls.Count > limits.MaxVbaCallsPerProcedure) break;
            }
            if (calls.Count > limits.MaxVbaCallsPerProcedure) break;

            foreach (Match match in DoCmdNavigationPattern().Matches(masked))
            {
                var callKind = "open-" + match.Groups["kind"].Value.ToLowerInvariant();
                AddLiteralTargetCall(databaseIdentitySeed, procedure, lineNumber, callKind, sourceLine, match.Index + match.Length,
                    0, knownObjects, calls, gaps, disclosurePolicy);
                if (calls.Count > limits.MaxVbaCallsPerProcedure) break;
            }
            if (calls.Count > limits.MaxVbaCallsPerProcedure) break;

            foreach (Match match in DaoCollectionPattern().Matches(masked))
            {
                var collection = match.Groups["kind"].Value.ToLowerInvariant();
                AddLiteralTargetCall(databaseIdentitySeed, procedure, lineNumber, collection == "querydefs" ? "dao-query-reference" : "dao-table-reference",
                    sourceLine, match.Index + match.Length, 0, knownObjects, calls, gaps, disclosurePolicy);
                if (calls.Count > limits.MaxVbaCallsPerProcedure) break;
            }
            if (calls.Count > limits.MaxVbaCallsPerProcedure) break;

            foreach (Match match in OpenRecordsetPattern().Matches(masked))
            {
                AddLiteralTargetCall(databaseIdentitySeed, procedure, lineNumber, "open-recordset-reference", sourceLine,
                    match.Index + match.Length, 0, knownObjects, calls, gaps, disclosurePolicy);
                if (calls.Count > limits.MaxVbaCallsPerProcedure) break;
            }
            if (calls.Count > limits.MaxVbaCallsPerProcedure) break;

            foreach (Match match in DomainFunctionPattern().Matches(masked))
            {
                AddLiteralTargetCall(databaseIdentitySeed, procedure, lineNumber, "domain-function-reference", sourceLine,
                    match.Index + match.Length, 1, knownObjects, calls, gaps, disclosurePolicy);
                if (calls.Count > limits.MaxVbaCallsPerProcedure) break;
            }
            if (calls.Count > limits.MaxVbaCallsPerProcedure) break;

            foreach (Match match in ExplicitLocalCallPattern().Matches(masked))
            {
                var targetName = match.Groups["name"].Value;
                var candidates = moduleProcedures.Where(item => string.Equals(item.RawName, targetName, StringComparison.OrdinalIgnoreCase)).ToArray();
                var identity = AccessSafeValues.Identity(databaseIdentitySeed, $"vba-call-{procedure.Projection.Identity.StableKey}", $"local-call-{lineNumber}", calls.Count, disclosurePolicy);
                var literalTarget = AccessSafeValues.Identity(databaseIdentitySeed, "vba-procedure-target", targetName, disclosurePolicy: disclosurePolicy);
                var target = candidates.Length == 1 ? candidates[0].Projection.Identity.StableKey : null;
                var coverage = candidates.Length == 1 ? "complete" : "partial";
                calls.Add(new(identity, procedure.Projection.Identity.StableKey, "local-procedure-call", lineNumber, lineNumber,
                    target, literalTarget, "vba-procedure", null, 0, coverage));
                if (candidates.Length != 1)
                    gaps.Add(new(candidates.Length == 0 ? "AccessVbaCallTargetUnresolved" : "AccessVbaCallTargetAmbiguous",
                        "vba-call", identity.StableKey, RuleIds.LegacyAccessVba));
                if (calls.Count > limits.MaxVbaCallsPerProcedure) break;
            }
        }

        if (calls.Count > limits.MaxVbaCallsPerProcedure)
        {
            var omittedCallKeys = calls.Skip(limits.MaxVbaCallsPerProcedure)
                .Select(call => call.Identity.StableKey)
                .ToHashSet(StringComparer.Ordinal);
            gaps.RemoveAll(gap => gap.StableScopeKey is not null && omittedCallKeys.Contains(gap.StableScopeKey));
            gaps.Add(new("AccessVbaCallLimitReached", "vba-procedure", procedure.Projection.Identity.StableKey, RuleIds.LegacyAccessVba));
        }
        return calls.Take(limits.MaxVbaCallsPerProcedure)
            .OrderBy(item => item.StartLine)
            .ThenBy(item => item.CallKind, StringComparer.Ordinal)
            .ThenBy(item => item.Identity.StableKey, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<AccessVbaEffectProjection> ProjectEffects(
        string databaseIdentitySeed,
        ProcedureWork procedure,
        string[] lines,
        AccessIdentityDisclosurePolicy disclosurePolicy)
    {
        var effects = new List<AccessVbaEffectProjection>();
        var conditions = new Stack<(string Hash, int Length, string Text)>();
        for (var index = procedure.BodyStartIndex; index <= procedure.BodyEndIndex && index < lines.Length; index++)
        {
            var source = CodeWithoutComment(lines[index]);
            var masked = MaskCommentsAndStrings(lines[index]);
            var trimmed = masked.Trim();
            if (trimmed.Length == 0) continue;
            if (EndIfPattern().IsMatch(trimmed))
            {
                if (conditions.Count > 0) conditions.Pop();
                continue;
            }
            if (ElsePattern().IsMatch(trimmed))
            {
                if (conditions.Count > 0)
                {
                    var prior = conditions.Pop();
                    var alternateText = $"Else({prior.Text})";
                    conditions.Push((AccessSafeValues.RoleHash("access-vba-condition", alternateText), alternateText.Length, alternateText));
                }
                continue;
            }
            var elseIf = ElseIfPattern().Match(trimmed);
            if (elseIf.Success)
            {
                if (conditions.Count > 0) conditions.Pop();
                var alternateText = source.Trim();
                conditions.Push((AccessSafeValues.RoleHash("access-vba-condition", alternateText), alternateText.Length, alternateText));
                continue;
            }
            var condition = IfConditionPattern().Match(trimmed);
            if (condition.Success)
            {
                var conditionText = source.Trim();
                if (trimmed.EndsWith("Then", StringComparison.OrdinalIgnoreCase))
                    conditions.Push((AccessSafeValues.RoleHash("access-vba-condition", conditionText), conditionText.Length, conditionText));
                continue;
            }

            var line = index + 1;
            var activeCondition = conditions.Count > 0 ? conditions.Peek() : ((string Hash, int Length, string Text)?)null;
            foreach (Match match in MeStateAssignmentPattern().Matches(masked))
            {
                var target = AccessSafeValues.Identity(databaseIdentitySeed, "vba-control-state-target", match.Groups["name"].Value, disclosurePolicy: disclosurePolicy);
                var expression = source[(source.IndexOf('=') + 1)..].Trim();
                effects.Add(NewEffect(databaseIdentitySeed, procedure, effects.Count, "control-state-assignment", line, target,
                    expression, activeCondition is null ? null : (activeCondition.Value.Hash, activeCondition.Value.Length), disclosurePolicy));
            }
            if (MeRequeryPattern().IsMatch(masked))
                effects.Add(NewEffect(databaseIdentitySeed, procedure, effects.Count, "surface-requery", line, null, string.Empty,
                    activeCondition is null ? null : (activeCondition.Value.Hash, activeCondition.Value.Length), disclosurePolicy));
            foreach (Match match in FormsReferencePattern().Matches(masked))
            {
                var argument = ArgumentAt(source, match.Index + match.Length, 0);
                var target = TryExactStringLiteral(argument, out var literal)
                    ? AccessSafeValues.Identity(databaseIdentitySeed, "vba-form-reference-target", literal, disclosurePolicy: disclosurePolicy)
                    : null;
                effects.Add(NewEffect(databaseIdentitySeed, procedure, effects.Count, "forms-reference", line, target,
                    argument ?? string.Empty,
                    activeCondition is null ? null : (activeCondition.Value.Hash, activeCondition.Value.Length),
                    disclosurePolicy, target is null ? "partial" : "complete"));
            }
        }
        return effects.OrderBy(item => item.StartLine).ThenBy(item => item.EffectKind, StringComparer.Ordinal).ToArray();
    }

    private static AccessVbaEffectProjection NewEffect(
        string seed,
        ProcedureWork procedure,
        int ordinal,
        string kind,
        int line,
        AccessSafeIdentity? target,
        string expression,
        (string Hash, int Length)? condition,
        AccessIdentityDisclosurePolicy disclosurePolicy,
        string coverage = "complete") =>
        new(
            AccessSafeValues.Identity(seed, $"vba-effect-{procedure.Projection.Identity.StableKey}", $"{kind}-{line}", ordinal, disclosurePolicy),
            procedure.Projection.Identity.StableKey,
            kind,
            line,
            line,
            target,
            expression.Length == 0 ? null : AccessSafeValues.RoleHash($"access-vba-{kind}-expression", expression),
            expression.Length,
            condition?.Hash,
            condition?.Length ?? 0,
            coverage);

    private static void AddLiteralTargetCall(
        string databaseIdentitySeed,
        ProcedureWork procedure,
        int lineNumber,
        string callKind,
        string sourceLine,
        int argumentsStart,
        int argumentIndex,
        IReadOnlyDictionary<string, IReadOnlyList<(string StableKey, string Kind)>>? knownObjects,
        List<AccessVbaCallProjection> calls,
        List<AccessGapProjection> gaps,
        AccessIdentityDisclosurePolicy disclosurePolicy)
    {
        var identity = AccessSafeValues.Identity(databaseIdentitySeed, $"vba-call-{procedure.Projection.Identity.StableKey}", $"{callKind}-{lineNumber}", calls.Count, disclosurePolicy);
        var argument = ArgumentAt(sourceLine, argumentsStart, argumentIndex);
        if (!TryExactStringLiteral(argument, out var literal))
        {
            var expression = argument ?? string.Empty;
            calls.Add(new(identity, procedure.Projection.Identity.StableKey, callKind, lineNumber, lineNumber, null, null,
                "dynamic", AccessSafeValues.RoleHash($"access-vba-{callKind}-expression", expression), expression.Length, "partial"));
            gaps.Add(new("AccessVbaDynamicDispatch", "vba-call", identity.StableKey, RuleIds.LegacyAccessVba));
            return;
        }

        var literalIdentity = AccessSafeValues.Identity(databaseIdentitySeed, $"vba-{callKind}-target", literal, disclosurePolicy: disclosurePolicy);
        var catalogCandidates = knownObjects is not null && knownObjects.TryGetValue(literal, out var values) ? values : [];
        var expectedKinds = ExpectedTargetKinds(callKind);
        var candidates = expectedKinds is null
            ? catalogCandidates
            : catalogCandidates.Where(candidate => expectedKinds.Contains(candidate.Kind, StringComparer.Ordinal)).ToArray();
        var target = candidates.Count == 1 ? candidates[0].StableKey : null;
        var targetKind = candidates.Count == 1
            ? candidates[0].Kind
            : expectedKinds is { Count: 1 } ? expectedKinds[0] : "access-object";
        var coverage = candidates.Count == 1 ? "complete" : "partial";
        calls.Add(new(identity, procedure.Projection.Identity.StableKey, callKind, lineNumber, lineNumber,
            target, literalIdentity, targetKind, null, 0, coverage));
        if (knownObjects is null)
            gaps.Add(new("AccessVbaTargetCatalogUnavailable", "vba-call", identity.StableKey, RuleIds.LegacyAccessVba));
        else if (candidates.Count != 1)
            gaps.Add(new(candidates.Count == 0 ? "AccessVbaLiteralTargetUnresolved" : "AccessVbaLiteralTargetAmbiguous",
                "vba-call", identity.StableKey, RuleIds.LegacyAccessVba));
    }

    private static IReadOnlyList<string>? ExpectedTargetKinds(string callKind) => callKind switch
    {
        "open-form" => ["form"],
        "open-report" => ["report"],
        "open-query" or "dao-query-reference" => ["query"],
        "dao-table-reference" => ["table"],
        "open-recordset-reference" or "domain-function-reference" => ["query", "table"],
        _ => null
    };

    private static void AddDynamicCall(
        string databaseIdentitySeed,
        AccessSafeIdentity moduleIdentity,
        ProcedureWork procedure,
        int lineNumber,
        string sourceLine,
        List<AccessVbaCallProjection> calls,
        List<AccessGapProjection> gaps,
        AccessIdentityDisclosurePolicy disclosurePolicy)
    {
        var identity = AccessSafeValues.Identity(databaseIdentitySeed, $"vba-call-{procedure.Projection.Identity.StableKey}", $"dynamic-{lineNumber}", calls.Count, disclosurePolicy);
        calls.Add(new(identity, procedure.Projection.Identity.StableKey, "dynamic-dispatch", lineNumber, lineNumber, null, null,
            "unknown", AccessSafeValues.RoleHash("access-vba-dynamic-expression", sourceLine), sourceLine.Length, "partial"));
        gaps.Add(new("AccessVbaDynamicDispatch", "vba-call", identity.StableKey ?? moduleIdentity.StableKey, RuleIds.LegacyAccessVba));
    }

    private static IReadOnlyList<AccessEventBindingProjection> MapEventProcedures(
        string databaseIdentitySeed,
        IReadOnlyList<ModuleWork> modules,
        IReadOnlyList<AccessRawEventProcedureReference> references,
        List<AccessGapProjection> gaps,
        AccessIdentityDisclosurePolicy disclosurePolicy)
    {
        var result = new List<AccessEventBindingProjection>();
        var modulesByRawName = modules
            .GroupBy(item => item.RawName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);
        foreach (var reference in references.OrderBy(item => item.OwnerStableKey, StringComparer.Ordinal).ThenBy(item => item.EventRole, StringComparer.Ordinal))
        {
            if (!AllowedEventRoles.Contains(reference.EventRole))
            {
                gaps.Add(new("AccessEventRoleUnsupported", "event-binding", reference.OwnerStableKey, RuleIds.LegacyAccessEventBinding));
                continue;
            }
            var moduleCandidates = modulesByRawName.TryGetValue(reference.ModuleName, out var candidates)
                ? candidates
                : [];
            var procedureCandidates = moduleCandidates
                .SelectMany(item => item.Procedures.Select(procedure => (Module: item, Procedure: procedure)))
                .Where(item => string.Equals(item.Procedure.RawName, reference.ProcedureName, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            var moduleStableKey = moduleCandidates.Length == 1
                ? moduleCandidates[0].Projection.Identity.StableKey
                : AccessSafeValues.Identity(databaseIdentitySeed, "vba-event-module-target", reference.ModuleName, disclosurePolicy: disclosurePolicy).StableKey;
            var procedure = procedureCandidates.Length == 1 ? procedureCandidates[0].Procedure.Projection : null;
            var procedureStableKey = procedure?.Identity.StableKey;
            result.Add(new(reference.OwnerStableKey, reference.EventRole, moduleStableKey, procedureStableKey,
                procedureCandidates.Length == 1 ? "complete" : "partial",
                reference.OwnerKind, reference.BindingKind, reference.EventExpressionHash, reference.EventExpressionLength,
                procedure?.StartLine ?? 0, procedure?.EndLine ?? 0));
            if (procedureCandidates.Length != 1)
                gaps.Add(new(procedureCandidates.Length == 0 ? "AccessEventProcedureUnresolved" : "AccessEventProcedureAmbiguous",
                    "event-binding", reference.OwnerStableKey, RuleIds.LegacyAccessEventBinding));
        }
        return result.OrderBy(item => item.OwnerStableKey, StringComparer.Ordinal).ThenBy(item => item.EventRole, StringComparer.Ordinal).ToArray();
    }

    private static string[] NormalizeLines(string source) =>
        source.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');

    private static string NormalizeModuleKind(string value) => value.Trim().ToLowerInvariant() switch
    {
        "standard" => "standard",
        "class" => "class",
        "form" => "form-class",
        "report" => "report-class",
        _ => "unknown"
    };

    private static string ProcedureKind(string value) => value.Trim().ToLowerInvariant().Replace(' ', '-');

    private static string EndKind(string procedureKind) => procedureKind switch
    {
        "function" => "Function",
        "property-get" or "property-let" or "property-set" => "Property",
        _ => "Sub"
    };

    private static string CodeWithoutComment(string line)
    {
        var builder = new StringBuilder(line.Length);
        var inString = false;
        for (var index = 0; index < line.Length; index++)
        {
            var current = line[index];
            if (current == '"')
            {
                builder.Append(current);
                if (inString && index + 1 < line.Length && line[index + 1] == '"')
                {
                    builder.Append(line[++index]);
                    continue;
                }
                inString = !inString;
                continue;
            }
            if (!inString && current == '\'') break;
            if (!inString && IsRemCommentStart(line, index)) break;
            builder.Append(current);
        }
        return builder.ToString();
    }

    private static bool IsRemCommentStart(string line, int index)
    {
        if (index + 3 > line.Length || !line.AsSpan(index, 3).Equals("Rem".AsSpan(), StringComparison.OrdinalIgnoreCase)) return false;
        var before = index == 0 ? '\0' : line[index - 1];
        var after = index + 3 >= line.Length ? '\0' : line[index + 3];
        return (index == 0 || char.IsWhiteSpace(before) || before == ':')
            && (after == '\0' || char.IsWhiteSpace(after));
    }

    private static string MaskCommentsAndStrings(string line)
    {
        var code = CodeWithoutComment(line);
        var chars = code.ToCharArray();
        var inString = false;
        for (var index = 0; index < chars.Length; index++)
        {
            if (chars[index] != '"')
            {
                if (inString) chars[index] = ' ';
                continue;
            }
            chars[index] = ' ';
            if (inString && index + 1 < chars.Length && chars[index + 1] == '"')
            {
                chars[++index] = ' ';
                continue;
            }
            inString = !inString;
        }
        return new string(chars);
    }

    private static string? ArgumentAt(string line, int start, int requestedIndex)
    {
        var cursor = start;
        while (cursor < line.Length && char.IsWhiteSpace(line[cursor])) cursor++;
        if (cursor < line.Length && line[cursor] == '(') cursor++;
        var argumentStart = cursor;
        var argumentIndex = 0;
        var nested = 0;
        var inString = false;
        for (; cursor <= line.Length; cursor++)
        {
            var current = cursor < line.Length ? line[cursor] : '\0';
            if (current == '"')
            {
                if (inString && cursor + 1 < line.Length && line[cursor + 1] == '"')
                {
                    cursor++;
                    continue;
                }
                inString = !inString;
                continue;
            }
            if (inString) continue;
            if (current == '(') { nested++; continue; }
            if (current == ')' && nested > 0) { nested--; continue; }
            if (current == ':' && nested == 0)
                return argumentIndex == requestedIndex ? line[argumentStart..cursor].Trim() : null;
            if ((current == ',' || current == ')' || current == '\0') && nested == 0)
            {
                if (argumentIndex == requestedIndex) return line[argumentStart..cursor].Trim();
                argumentIndex++;
                argumentStart = cursor + 1;
            }
        }
        return null;
    }

    private static bool TryExactStringLiteral(string? argument, out string value)
    {
        var match = ExactStringLiteralPattern().Match(argument ?? string.Empty);
        value = match.Success ? match.Groups["value"].Value.Replace("\"\"", "\"", StringComparison.Ordinal) : string.Empty;
        return match.Success;
    }

    private static readonly HashSet<string> AllowedEventRoles = new(StringComparer.Ordinal)
    {
        "after-update", "on-activate", "before-update", "on-click", "on-close", "on-current", "on-deactivate", "on-dbl-click", "on-error", "on-load", "on-no-data", "on-open", "on-resize", "on-timer", "on-unload"
    };

    [GeneratedRegex(@"^\s*(?:(?:Public|Private|Friend|Static)\s+)?(?<kind>Sub|Function|Property\s+(?:Get|Let|Set))\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ProcedureDeclarationPattern();

    [GeneratedRegex(@"^\s*End\s+(?<kind>Sub|Function|Property)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ProcedureEndPattern();

    [GeneratedRegex(@"\b(?:Eval|Run|CallByName|AddressOf|CreateObject|GetObject|Shell)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DynamicDispatchPattern();

    [GeneratedRegex(@"\bDoCmd\s*\.\s*Open(?<kind>Form|Report|Query)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DoCmdNavigationPattern();

    [GeneratedRegex(@"\b(?<kind>QueryDefs|TableDefs)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DaoCollectionPattern();

    [GeneratedRegex(@"\bOpenRecordset\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex OpenRecordsetPattern();

    [GeneratedRegex(@"\b(?:DLookup|DCount|DSum|DAvg|DMin|DMax|DFirst|DLast)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DomainFunctionPattern();

    [GeneratedRegex(@"\bCall\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ExplicitLocalCallPattern();

    [GeneratedRegex(@"^\s*If\s+.+?\s+Then\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex IfConditionPattern();

    [GeneratedRegex(@"^\s*End\s+If\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EndIfPattern();

    [GeneratedRegex(@"^\s*Else\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ElsePattern();

    [GeneratedRegex(@"^\s*ElseIf\s+.+?\s+Then\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ElseIfPattern();

    [GeneratedRegex(@"\bMe\s*\.\s*(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\.\s*(?:Visible|Enabled|Locked|Caption)\s*=", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MeStateAssignmentPattern();

    [GeneratedRegex(@"\bMe\s*\.\s*Requery\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex MeRequeryPattern();

    [GeneratedRegex(@"\bForms\s*\(", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FormsReferencePattern();

    [GeneratedRegex("^\\s*\"(?<value>(?:\"\"|[^\"])*)\"\\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex ExactStringLiteralPattern();
}
