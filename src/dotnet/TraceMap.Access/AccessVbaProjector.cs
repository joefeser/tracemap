using System.Text;
using System.Text.RegularExpressions;
using TraceMap.Core;

namespace TraceMap.Access;

internal sealed record AccessRawVbaModule(string Name, string ModuleKind, string Source);

internal sealed record AccessRawEventProcedureReference(
    string OwnerStableKey,
    string EventRole,
    string ModuleName,
    string ProcedureName);

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
        AccessLimits? limits = null)
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

            var moduleIdentity = AccessSafeValues.Identity(databaseIdentitySeed, "vba-module", raw.Name);
            var moduleGapStart = gaps.Count;
            var procedureWork = ParseProcedureDeclarations(databaseIdentitySeed, moduleIdentity, lines, limits, gaps);
            var procedures = procedureWork
                .Select(work => work.Projection with
                {
                    Calls = ProjectCalls(databaseIdentitySeed, moduleIdentity, work, procedureWork, lines, knownObjects, limits, gaps)
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

        var bindings = MapEventProcedures(databaseIdentitySeed, modules, eventReferences ?? [], gaps);
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
        List<AccessGapProjection> gaps)
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
                gaps.Add(new("AccessVbaProcedureEndUnavailable", "vba-procedure", moduleIdentity.StableKey, RuleIds.LegacyAccessVba));
            }

            var identity = AccessSafeValues.Identity(databaseIdentitySeed, $"vba-procedure-{moduleIdentity.StableKey}", declaration.Name, ordinal);
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
        List<AccessGapProjection> gaps)
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
                AddDynamicCall(databaseIdentitySeed, moduleIdentity, procedure, lineNumber, sourceLine, calls, gaps);
                if (calls.Count > limits.MaxVbaCallsPerProcedure) break;
            }
            if (calls.Count > limits.MaxVbaCallsPerProcedure) break;

            foreach (Match match in DoCmdNavigationPattern().Matches(masked))
            {
                var callKind = "open-" + match.Groups["kind"].Value.ToLowerInvariant();
                AddLiteralTargetCall(databaseIdentitySeed, procedure, lineNumber, callKind, sourceLine, match.Index + match.Length,
                    0, knownObjects, calls, gaps);
                if (calls.Count > limits.MaxVbaCallsPerProcedure) break;
            }
            if (calls.Count > limits.MaxVbaCallsPerProcedure) break;

            foreach (Match match in DaoCollectionPattern().Matches(masked))
            {
                var collection = match.Groups["kind"].Value.ToLowerInvariant();
                AddLiteralTargetCall(databaseIdentitySeed, procedure, lineNumber, collection == "querydefs" ? "dao-query-reference" : "dao-table-reference",
                    sourceLine, match.Index + match.Length, 0, knownObjects, calls, gaps);
                if (calls.Count > limits.MaxVbaCallsPerProcedure) break;
            }
            if (calls.Count > limits.MaxVbaCallsPerProcedure) break;

            foreach (Match match in OpenRecordsetPattern().Matches(masked))
            {
                AddLiteralTargetCall(databaseIdentitySeed, procedure, lineNumber, "open-recordset-reference", sourceLine,
                    match.Index + match.Length, 0, knownObjects, calls, gaps);
                if (calls.Count > limits.MaxVbaCallsPerProcedure) break;
            }
            if (calls.Count > limits.MaxVbaCallsPerProcedure) break;

            foreach (Match match in DomainFunctionPattern().Matches(masked))
            {
                AddLiteralTargetCall(databaseIdentitySeed, procedure, lineNumber, "domain-function-reference", sourceLine,
                    match.Index + match.Length, 1, knownObjects, calls, gaps);
                if (calls.Count > limits.MaxVbaCallsPerProcedure) break;
            }
            if (calls.Count > limits.MaxVbaCallsPerProcedure) break;

            foreach (Match match in ExplicitLocalCallPattern().Matches(masked))
            {
                var targetName = match.Groups["name"].Value;
                var candidates = moduleProcedures.Where(item => string.Equals(item.RawName, targetName, StringComparison.OrdinalIgnoreCase)).ToArray();
                var identity = AccessSafeValues.Identity(databaseIdentitySeed, $"vba-call-{procedure.Projection.Identity.StableKey}", $"local-call-{lineNumber}", calls.Count);
                var literalTarget = AccessSafeValues.Identity(databaseIdentitySeed, "vba-procedure-target", targetName);
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
        List<AccessGapProjection> gaps)
    {
        var identity = AccessSafeValues.Identity(databaseIdentitySeed, $"vba-call-{procedure.Projection.Identity.StableKey}", $"{callKind}-{lineNumber}", calls.Count);
        var argument = ArgumentAt(sourceLine, argumentsStart, argumentIndex);
        if (!TryExactStringLiteral(argument, out var literal))
        {
            var expression = argument ?? string.Empty;
            calls.Add(new(identity, procedure.Projection.Identity.StableKey, callKind, lineNumber, lineNumber, null, null,
                "dynamic", AccessSafeValues.RoleHash($"access-vba-{callKind}-expression", expression), expression.Length, "partial"));
            gaps.Add(new("AccessVbaDynamicDispatch", "vba-call", identity.StableKey, RuleIds.LegacyAccessVba));
            return;
        }

        var literalIdentity = AccessSafeValues.Identity(databaseIdentitySeed, $"vba-{callKind}-target", literal);
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
        List<AccessGapProjection> gaps)
    {
        var identity = AccessSafeValues.Identity(databaseIdentitySeed, $"vba-call-{procedure.Projection.Identity.StableKey}", $"dynamic-{lineNumber}", calls.Count);
        calls.Add(new(identity, procedure.Projection.Identity.StableKey, "dynamic-dispatch", lineNumber, lineNumber, null, null,
            "unknown", AccessSafeValues.RoleHash("access-vba-dynamic-expression", sourceLine), sourceLine.Length, "partial"));
        gaps.Add(new("AccessVbaDynamicDispatch", "vba-call", identity.StableKey ?? moduleIdentity.StableKey, RuleIds.LegacyAccessVba));
    }

    private static IReadOnlyList<AccessEventBindingProjection> MapEventProcedures(
        string databaseIdentitySeed,
        IReadOnlyList<ModuleWork> modules,
        IReadOnlyList<AccessRawEventProcedureReference> references,
        List<AccessGapProjection> gaps)
    {
        var result = new List<AccessEventBindingProjection>();
        foreach (var reference in references.OrderBy(item => item.OwnerStableKey, StringComparer.Ordinal).ThenBy(item => item.EventRole, StringComparer.Ordinal))
        {
            if (!AllowedEventRoles.Contains(reference.EventRole))
            {
                gaps.Add(new("AccessEventRoleUnsupported", "event-binding", reference.OwnerStableKey, RuleIds.LegacyAccessEventBinding));
                continue;
            }
            var moduleCandidates = modules.Where(item => string.Equals(item.RawName, reference.ModuleName, StringComparison.OrdinalIgnoreCase)).ToArray();
            var procedureCandidates = moduleCandidates
                .SelectMany(item => item.Procedures.Select(procedure => (Module: item, Procedure: procedure)))
                .Where(item => string.Equals(item.Procedure.RawName, reference.ProcedureName, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            var moduleStableKey = moduleCandidates.Length == 1
                ? moduleCandidates[0].Projection.Identity.StableKey
                : AccessSafeValues.Identity(databaseIdentitySeed, "vba-event-module-target", reference.ModuleName).StableKey;
            var procedureStableKey = procedureCandidates.Length == 1 ? procedureCandidates[0].Procedure.Projection.Identity.StableKey : null;
            result.Add(new(reference.OwnerStableKey, reference.EventRole, moduleStableKey, procedureStableKey,
                procedureCandidates.Length == 1 ? "complete" : "partial"));
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
        "after-update", "before-update", "on-click", "on-current", "on-dbl-click", "on-load", "on-no-data", "on-open"
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

    [GeneratedRegex("^\\s*\"(?<value>(?:\"\"|[^\"])*)\"\\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex ExactStringLiteralPattern();
}
