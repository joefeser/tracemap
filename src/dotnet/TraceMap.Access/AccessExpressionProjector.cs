using System.Text.RegularExpressions;

namespace TraceMap.Access;

/// <summary>Projects a deliberately bounded Access expression shape without evaluating it.</summary>
public static partial class AccessExpressionProjector
{
    private static readonly HashSet<string> Functions = new(StringComparer.OrdinalIgnoreCase)
    {
        "abs", "avg", "count", "date", "dateadd", "datediff", "dlookup", "dsum", "first", "format",
        "iif", "instr", "isnull", "len", "max", "min", "nz", "sum", "val",
        "dcount", "davg", "dmax", "dmin"
    };

    public static AccessExpressionProjection Project(
        string expression,
        IReadOnlyDictionary<string, IReadOnlyList<(string StableKey, string Kind)>>? objects,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? fields,
        IReadOnlySet<string>? controlNames = null,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>>? fieldSetsByObject = null,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? controlStableKeys = null,
        IReadOnlyDictionary<string, IReadOnlyList<(string StableKey, string Kind)>>? domainObjects = null,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? vbaProcedureStableKeys = null,
        IReadOnlySet<string>? contextIdentifierNames = null) =>
        ProjectWithDomainCriteriaFields(
            expression,
            objects,
            fields,
            controlNames,
            fieldSetsByObject,
            controlStableKeys,
            domainObjects,
            vbaProcedureStableKeys,
            contextIdentifierNames,
            null);

    internal static AccessExpressionProjection ProjectWithDomainCriteriaFields(
        string expression,
        IReadOnlyDictionary<string, IReadOnlyList<(string StableKey, string Kind)>>? objects,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? fields,
        IReadOnlySet<string>? controlNames = null,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>>? fieldSetsByObject = null,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? controlStableKeys = null,
        IReadOnlyDictionary<string, IReadOnlyList<(string StableKey, string Kind)>>? domainObjects = null,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? vbaProcedureStableKeys = null,
        IReadOnlySet<string>? contextIdentifierNames = null,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>>? domainCriteriaFieldSetsByObject = null)
    {
        var normalized = expression.Trim();
        var functionMatches = FunctionPattern().Matches(MaskLiteralsAndBracketedIdentifiers(normalized));
        var functionNames = functionMatches
            .Select(match => match.Groups["name"].Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var functions = functionNames
            .Select(name => AccessSafeValues.RoleHash("access-expression-function", name.ToLowerInvariant()))
            .OrderBy(value => value, StringComparer.Ordinal).ToArray();
        var operators = OperatorPattern().Matches(MaskLiteralsAndBracketedIdentifiers(normalized))
            .Select(match => match.Value.Any(char.IsLetter)
                ? match.Value.ToLowerInvariant()
                : match.Value)
            .Distinct(StringComparer.Ordinal)
            .Select(value => AccessSafeValues.RoleHash("access-expression-operator", value))
            .OrderBy(value => value, StringComparer.Ordinal).ToArray();
        var fieldKeys = new SortedSet<string>(StringComparer.Ordinal);
        var queryKeys = new SortedSet<string>(StringComparer.Ordinal);
        var selectedFields = new SortedSet<string>(StringComparer.Ordinal);
        var selectedFieldRefs = new SortedSet<string>(StringComparer.Ordinal);
        var criteriaFields = new SortedSet<string>(StringComparer.Ordinal);
        var criteriaFieldRefs = new SortedSet<string>(StringComparer.Ordinal);
        var controlKeys = new SortedSet<string>(StringComparer.Ordinal);
        var controlRefs = new SortedSet<string>(StringComparer.Ordinal);
        var externalRefs = new SortedSet<string>(StringComparer.Ordinal);
        var vbaProcedureKeys = new SortedSet<string>(StringComparer.Ordinal);
        var literals = new SortedSet<string>(StringComparer.Ordinal);
        var unresolvedFunction = false;
        foreach (var functionName in functionNames.Where(name => !Functions.Contains(name)))
        {
            if (vbaProcedureStableKeys?.TryGetValue(functionName, out var procedureCandidates) == true
                && procedureCandidates.Count == 1)
                vbaProcedureKeys.Add(procedureCandidates[0]);
            else
                unresolvedFunction = true;
        }
        var unresolved = false;
        var ambiguous = false;
        var domainSelectedFieldUnresolved = false;
        var domainCriteriaFieldUnresolved = false;
        var domainCriteriaFieldAmbiguous = false;
        var domainFieldCatalogIncomplete = false;
        var malformedBracketedIdentifier = HasUnbalancedBracketedIdentifier(normalized);
        var domainSyntaxMatches = DomainNamePattern().Matches(MaskLiteralsAndBracketedIdentifiers(normalized));
        var domainMatches = FindDomainCalls(normalized, domainSyntaxMatches);
        unresolved |= malformedBracketedIdentifier || domainSyntaxMatches.Count != domainMatches.Count;

        foreach (Match literal in LiteralPattern().Matches(MaskBracketedIdentifiers(normalized)))
            literals.Add(literal.Groups["kind"].Value.StartsWith('"') ? "string" : "number");

        // Domain-call arguments are resolved below against the domain object's
        // field set. Mask the complete calls here so their identifiers are not
        // incorrectly resolved against the owning surface (or marked missing)
        // by the general expression pass.
        var nonDomainExpression = MaskDomainCalls(normalized, domainMatches);
        var generalExternalMatches = ExternalReferencePattern().Matches(nonDomainExpression);
        AddExternalReferences(generalExternalMatches, externalRefs);
        foreach (Match match in IdentifierPattern().Matches(MaskMatches(nonDomainExpression, generalExternalMatches)))
        {
            var name = NormalizeIdentifier(match.Groups["name"].Value);
            if (IsKeyword(name) || functionNames.Contains(name) || Functions.Contains(name)) continue;
            if (contextIdentifierNames?.Contains(name) == true)
            {
                externalRefs.Add(AccessSafeValues.RoleHash(
                    "access-expression-context-identifier",
                    name.ToLowerInvariant()));
                continue;
            }
            var resolved = false;
            if (fields is not null && fields.TryGetValue(name, out var candidates))
            {
                if (candidates.Count == 1)
                {
                    fieldKeys.Add(candidates[0]);
                    resolved = true;
                }
                else ambiguous = true;
            }
            if (controlStableKeys is not null && controlStableKeys.TryGetValue(name, out var stableControlCandidates))
            {
                if (stableControlCandidates.Count == 1)
                {
                    controlKeys.Add(stableControlCandidates[0]);
                    if (resolved) ambiguous = true;
                    resolved = true;
                }
                else ambiguous = true;
            }
            if (resolved) continue;
            if (objects is not null && objects.TryGetValue(name, out var objectCandidates))
            {
                if (objectCandidates.Count == 1 && objectCandidates[0].Kind is "query" or "table") queryKeys.Add(objectCandidates[0].StableKey);
                else ambiguous = true;
                continue;
            }
            if (controlNames is not null && controlNames.Contains(name))
                controlRefs.Add(AccessSafeValues.RoleHash("access-expression-control", name));
            else
                unresolved = true;
        }

        foreach (var dlookup in domainMatches)
        {
            var args = SplitArguments(dlookup.Args);
            if (args.Count >= 2)
            {
                var queryCandidate = ResolveObject(args[1], domainObjects ?? objects, queryKeys);
                var domainFields = queryCandidate is not null && fieldSetsByObject?.TryGetValue(queryCandidate, out var queryFields) == true
                    ? queryFields
                    : null;
                if (queryCandidate is not null && domainFields is null)
                    domainFieldCatalogIncomplete = true;
                var wildcardSelection = dlookup.Name.Equals("DCount", StringComparison.OrdinalIgnoreCase)
                    && NormalizeArgumentIdentifier(args[0]) == "*";
                var selectedResolution = wildcardSelection
                    ? FieldResolution.Unique
                    : queryCandidate is not null
                        ? ResolveField(args[0], domainFields, selectedFields)
                        : FieldResolution.Missing;
                if (wildcardSelection) literals.Add("wildcard");
                if (selectedResolution == FieldResolution.Missing)
                {
                    AddIdentifierReferenceHash(args[0], "access-expression-selected-field", selectedFieldRefs);
                    domainSelectedFieldUnresolved |= queryCandidate is not null;
                    unresolved = true;
                }
                else if (selectedResolution == FieldResolution.Ambiguous)
                    ambiguous = true;
                if (queryCandidate is null) unresolved = true;
                if (args.Count >= 3)
                {
                    var domainCriteriaFields = queryCandidate is not null
                        && domainCriteriaFieldSetsByObject?.TryGetValue(queryCandidate, out var criteriaFieldsForQuery) == true
                            ? criteriaFieldsForQuery
                            : domainFields;
                    var criteriaExpression = args[2].Trim().Trim('"');
                    var externalMatches = ExternalReferencePattern().Matches(criteriaExpression);
                    AddExternalReferences(externalMatches, externalRefs);
                    foreach (var candidate in ExtractIdentifiers(MaskMatches(criteriaExpression, externalMatches)))
                    {
                        var criteriaResolution = queryCandidate is not null
                            ? ResolveField(candidate, domainCriteriaFields, criteriaFields)
                            : FieldResolution.Missing;
                        if (criteriaResolution == FieldResolution.Ambiguous)
                        {
                            domainCriteriaFieldAmbiguous = true;
                            ambiguous = true;
                        }
                        var controlMatched = false;
                        if (controlStableKeys?.TryGetValue(candidate, out var stableControlCandidates) == true)
                        {
                            controlMatched = true;
                            if (stableControlCandidates.Count == 1)
                                controlKeys.Add(stableControlCandidates[0]);
                            else ambiguous = true;
                        }
                        else if (controlNames?.Contains(candidate) == true)
                        {
                            controlMatched = true;
                            controlRefs.Add(AccessSafeValues.RoleHash("access-expression-control", candidate));
                        }
                        if (criteriaResolution == FieldResolution.Unique && controlMatched)
                            ambiguous = true;
                        else if (criteriaResolution == FieldResolution.Missing && !controlMatched)
                        {
                            AddIdentifierReferenceHash(candidate, "access-expression-criteria-field", criteriaFieldRefs);
                            domainCriteriaFieldUnresolved = true;
                            unresolved = true;
                        }
                    }
                }
            }
            else unresolved = true;
        }

        var dynamic = Regex.IsMatch(normalized, @"\b(?:Eval|Run|Call)\s*\(", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var runtimeValueDependent = dynamic
            || domainMatches.Count > 0
            || functionNames.Count > 0
            || vbaProcedureKeys.Count > 0
            || controlKeys.Count > 0
            || controlRefs.Count > 0
            || externalRefs.Count > 0
            || normalized.Contains("&", StringComparison.Ordinal);
        var classification = domainMatches.Count > 0 ? "domain-lookup"
            : functions.Length > 0 || normalized.StartsWith('=') || operators.Length > 0 ? "calculated-expression"
            : "expression";
        var coverage = dynamic || ambiguous || unresolved || unresolvedFunction || domainFieldCatalogIncomplete ? "partial" : "complete";
        var gap = dynamic ? "AccessBindingExpressionDynamic"
            : unresolvedFunction ? "AccessBindingExpressionFunctionUnresolved"
            : domainFieldCatalogIncomplete ? "AccessBindingDomainFieldCatalogIncomplete"
            : domainSelectedFieldUnresolved ? "AccessBindingDomainSelectedFieldUnmatched"
            : domainCriteriaFieldAmbiguous ? "AccessBindingDomainCriteriaFieldAmbiguous"
            : ambiguous ? "AccessBindingExpressionTargetAmbiguous"
            : domainCriteriaFieldUnresolved ? "AccessBindingDomainCriteriaFieldUnmatched"
            : unresolved ? "AccessBindingExpressionPartial"
            : null;
        return new(
            classification,
            AccessSafeValues.RoleHash("access-expression-structure", normalized),
            functions,
            operators,
            fieldKeys.ToArray(),
            controlKeys.ToArray(),
            controlRefs.ToArray(),
            externalRefs.ToArray(),
            vbaProcedureKeys.ToArray(),
            queryKeys.ToArray(),
            selectedFields.ToArray(),
            selectedFieldRefs.ToArray(),
            criteriaFields.ToArray(),
            criteriaFieldRefs.ToArray(),
            literals.ToArray(),
            coverage,
            runtimeValueDependent ? "partial" : "complete",
            gap);
    }

    internal static IReadOnlyList<AccessStaticDomainFieldCandidate> FindStaticDomainFieldCandidates(
        string expression)
    {
        var normalized = expression.Trim();
        var calls = FindDomainCalls(
            normalized,
            DomainNamePattern().Matches(MaskLiteralsAndBracketedIdentifiers(normalized)));
        var references = new HashSet<AccessStaticDomainFieldCandidate>(
            DomainFieldReferenceComparer.Instance);
        foreach (var call in calls)
        {
            var args = SplitArguments(call.Args);
            if (args.Count < 2
                || !TryStaticArgumentIdentifier(args[1], requireQuoted: true, out var domainName))
                continue;

            if (!call.Name.Equals("DCount", StringComparison.OrdinalIgnoreCase)
                || NormalizeArgumentIdentifier(args[0]) != "*")
            {
                if (TryStaticArgumentIdentifier(args[0], requireQuoted: false, out var selectedField))
                    references.Add(new(domainName, selectedField, "selected"));
            }

            if (args.Count < 3 || !TryGetStaticStringLiteral(args[2], out var criteriaExpression)) continue;
            var externalMatches = ExternalReferencePattern().Matches(criteriaExpression);
            foreach (var fieldName in ExtractIdentifiers(MaskMatches(criteriaExpression, externalMatches)))
                references.Add(new(domainName, fieldName, "criteria"));
        }
        return references
            .OrderBy(item => item.DomainName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.FieldName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.ReferenceKind, StringComparer.Ordinal)
            .ToArray();
    }

    private static FieldResolution ResolveField(string value, IReadOnlyDictionary<string, IReadOnlyList<string>>? fields, ISet<string> output)
    {
        var name = NormalizeArgumentIdentifier(value);
        if (fields is null || !fields.TryGetValue(name, out var candidates) || candidates.Count == 0)
            return FieldResolution.Missing;
        if (candidates.Count == 1)
        {
            output.Add(candidates[0]);
            return FieldResolution.Unique;
        }
        return FieldResolution.Ambiguous;
    }

    private enum FieldResolution
    {
        Missing,
        Unique,
        Ambiguous
    }

    private static string? ResolveObject(string value, IReadOnlyDictionary<string, IReadOnlyList<(string StableKey, string Kind)>>? objects, ISet<string> output)
    {
        var name = NormalizeArgumentIdentifier(value);
        if (objects is not null && objects.TryGetValue(name, out var candidates) && candidates.Count == 1)
        {
            output.Add(candidates[0].StableKey);
            return candidates[0].StableKey;
        }
        return null;
    }

    private static IEnumerable<string> ExtractIdentifiers(string value) =>
        IdentifierPattern().Matches(MaskLiterals(value.Trim().Trim('"'))).Select(match => NormalizeIdentifier(match.Groups["name"].Value))
            .Where(name => !Functions.Contains(name) && !IsKeyword(name));

    private static IReadOnlyList<string> SplitArguments(string value)
    {
        var result = new List<string>();
        var start = 0;
        var depth = 0;
        var quote = false;
        for (var index = 0; index < value.Length; index++)
        {
            var current = value[index];
            if (current == '"') quote = !quote;
            else if (!quote && current == '(') depth++;
            else if (!quote && current == ')' && depth > 0) depth--;
            else if (!quote && depth == 0 && current == ',') { result.Add(value[start..index]); start = index + 1; }
        }
        result.Add(value[start..]);
        return result;
    }

    private static bool IsKeyword(string name) => name.Equals("and", StringComparison.OrdinalIgnoreCase)
        || name.Equals("or", StringComparison.OrdinalIgnoreCase) || name.Equals("not", StringComparison.OrdinalIgnoreCase)
        || name.Equals("true", StringComparison.OrdinalIgnoreCase) || name.Equals("false", StringComparison.OrdinalIgnoreCase);

    private static string NormalizeIdentifier(string value) => value.Trim().Trim('[', ']');

    private static string NormalizeArgumentIdentifier(string value)
    {
        var normalized = value.Trim();
        if (normalized.Length >= 2 && normalized[0] == '"' && normalized[^1] == '"')
            normalized = normalized[1..^1].Trim();
        if (normalized.Length >= 2 && normalized[0] == '[' && normalized[^1] == ']')
            normalized = normalized[1..^1].Trim();
        return normalized;
    }

    private static bool TryStaticArgumentIdentifier(
        string value,
        bool requireQuoted,
        out string identifier)
    {
        identifier = NormalizeArgumentIdentifier(value);
        if (identifier.Length == 0) return false;
        var trimmed = value.Trim();
        var quoted = trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"';
        var bracketed = trimmed.Length >= 2 && trimmed[0] == '[' && trimmed[^1] == ']';
        if (requireQuoted ? !quoted : !quoted && !bracketed) return false;
        return identifier.All(character => !char.IsControl(character))
            && identifier.IndexOfAny(['"', '\'', '[', ']', '!', '&', '(', ')', ',']) < 0;
    }

    private static bool TryGetStaticStringLiteral(string value, out string literal)
    {
        literal = string.Empty;
        var trimmed = value.Trim();
        if (trimmed.Length < 2 || trimmed[0] != '"' || trimmed[^1] != '"') return false;
        var builder = new System.Text.StringBuilder(trimmed.Length - 2);
        for (var index = 1; index < trimmed.Length - 1; index++)
        {
            if (trimmed[index] != '"')
            {
                builder.Append(trimmed[index]);
                continue;
            }
            if (index + 1 < trimmed.Length - 1 && trimmed[index + 1] == '"')
            {
                builder.Append('"');
                index++;
                continue;
            }
            return false;
        }
        literal = builder.ToString();
        return true;
    }

    private static bool HasUnbalancedBracketedIdentifier(string value)
    {
        var inString = false;
        var inBracket = false;
        for (var index = 0; index < value.Length; index++)
        {
            var current = value[index];
            if (current == '"' && !inBracket)
            {
                if (inString && index + 1 < value.Length && value[index + 1] == '"')
                {
                    index++;
                    continue;
                }
                inString = !inString;
            }
            else if (!inString && current == '[')
                inBracket = true;
            else if (!inString && current == ']' && inBracket)
                inBracket = false;
        }
        return inBracket;
    }

    private static string MaskMatches(string value, MatchCollection matches)
    {
        var chars = value.ToCharArray();
        foreach (Match match in matches)
            for (var index = match.Index; index < match.Index + match.Length && index < chars.Length; index++)
                chars[index] = ' ';
        return new string(chars);
    }

    private static void AddExternalReferences(MatchCollection matches, ISet<string> output)
    {
        foreach (Match match in matches)
        {
            var normalized = Regex.Replace(
                match.Value,
                @"\s+",
                string.Empty,
                RegexOptions.CultureInvariant).ToLowerInvariant();
            output.Add(AccessSafeValues.RoleHash("access-expression-external-reference", normalized));
        }
    }

    private static void AddIdentifierReferenceHash(string value, string role, ISet<string> output)
    {
        var normalized = NormalizeArgumentIdentifier(value);
        if (normalized.Length > 0)
            output.Add(AccessSafeValues.RoleHash(role, normalized.ToLowerInvariant()));
    }

    private static string MaskLiterals(string value)
    {
        var chars = value.ToCharArray();
        var quote = '\0';
        for (var index = 0; index < chars.Length; index++)
        {
            var current = chars[index];
            if (quote == '\0')
            {
                if (current is '\'' or '"')
                {
                    quote = current;
                    chars[index] = ' ';
                }
                continue;
            }

            chars[index] = ' ';
            if (current == quote)
            {
                if (index + 1 < chars.Length && chars[index + 1] == quote)
                    chars[++index] = ' ';
                else
                    quote = '\0';
            }
        }
        return new string(chars);
    }

    private static string MaskLiteralsAndBracketedIdentifiers(string value)
    {
        var chars = MaskLiterals(value).ToCharArray();
        MaskBracketedContents(chars);
        return new string(chars);
    }

    private static string MaskBracketedIdentifiers(string value)
    {
        var chars = value.ToCharArray();
        MaskBracketedContents(chars);
        return new string(chars);
    }

    private static void MaskBracketedContents(char[] chars)
    {
        var bracketDepth = 0;
        for (var index = 0; index < chars.Length; index++)
        {
            if (chars[index] == '[')
            {
                bracketDepth++;
                continue;
            }
            if (bracketDepth > 0)
            {
                if (chars[index] == ']') bracketDepth--;
                else chars[index] = ' ';
            }
        }
    }

    private static string MaskDomainCalls(string value, IReadOnlyList<DomainCall> matches)
    {
        if (matches.Count == 0) return MaskLiterals(value);
        var chars = MaskLiterals(value).ToCharArray();
        foreach (var match in matches)
            for (var index = match.Index; index < match.Index + match.Length && index < chars.Length; index++)
                chars[index] = ' ';
        return new string(chars);
    }

    private static IReadOnlyList<DomainCall> FindDomainCalls(string value, MatchCollection syntaxMatches)
    {
        var calls = new List<DomainCall>();
        foreach (Match match in syntaxMatches)
        {
            var open = value.IndexOf('(', match.Index);
            if (open < 0) continue;
            var depth = 1;
            var quoted = false;
            var close = -1;
            for (var index = open + 1; index < value.Length; index++)
            {
                var current = value[index];
                if (current == '"')
                {
                    if (quoted && index + 1 < value.Length && value[index + 1] == '"') { index++; continue; }
                    quoted = !quoted;
                    continue;
                }
                if (quoted) continue;
                if (current == '(') depth++;
                else if (current == ')' && --depth == 0) { close = index; break; }
            }
            if (close > open)
                calls.Add(new(match.Index, close - match.Index + 1, value[match.Index..open], value[(open + 1)..close]));
        }
        return calls;
    }

    private sealed record DomainCall(int Index, int Length, string Name, string Args);

    internal sealed record AccessStaticDomainFieldCandidate(
        string DomainName,
        string FieldName,
        string ReferenceKind);

    private sealed class DomainFieldReferenceComparer : IEqualityComparer<AccessStaticDomainFieldCandidate>
    {
        public static readonly DomainFieldReferenceComparer Instance = new();

        public bool Equals(
            AccessStaticDomainFieldCandidate? left,
            AccessStaticDomainFieldCandidate? right) =>
            left is not null
            && right is not null
            && StringComparer.OrdinalIgnoreCase.Equals(left.DomainName, right.DomainName)
            && StringComparer.OrdinalIgnoreCase.Equals(left.FieldName, right.FieldName)
            && StringComparer.Ordinal.Equals(left.ReferenceKind, right.ReferenceKind);

        public int GetHashCode(AccessStaticDomainFieldCandidate value) =>
            HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(value.DomainName),
                StringComparer.OrdinalIgnoreCase.GetHashCode(value.FieldName),
                StringComparer.Ordinal.GetHashCode(value.ReferenceKind));
    }

    [GeneratedRegex(@"\b(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\(", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FunctionPattern();
    [GeneratedRegex(@"(?<name>[A-Za-z_][A-Za-z0-9_]*|\[[^\]]+\])", RegexOptions.CultureInvariant)]
    private static partial Regex IdentifierPattern();
    [GeneratedRegex(@"(?<![A-Za-z0-9_])\[?(?:TempVars|Forms|Reports)\]?(?:\s*!\s*(?:\[[^\]\r\n]+\]|[A-Za-z_][A-Za-z0-9_]*)){1,2}", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ExternalReferencePattern();
    [GeneratedRegex(@"(?<name>\bDLookup|\bDCount|\bDSum|\bDAvg|\bDMax|\bDMin)\s*\((?<args>[^()]*(?:\([^)]*\)[^()]*)*)\)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DomainPattern();
    [GeneratedRegex(@"\bD(?:Lookup|Count|Sum|Avg|Max|Min)\s*\(", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DomainNamePattern();
    [GeneratedRegex("(?<kind>\"(?:\"\"|[^\"])*\"|[-+]?\\d+(?:\\.\\d+)?)", RegexOptions.CultureInvariant)]
    private static partial Regex LiteralPattern();
    [GeneratedRegex(@"[+\-*/&<>=]|\b(?:AND|OR|NOT)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex OperatorPattern();
}
