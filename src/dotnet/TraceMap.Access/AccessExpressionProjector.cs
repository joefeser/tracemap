using System.Text.RegularExpressions;

namespace TraceMap.Access;

/// <summary>Projects a deliberately bounded Access expression shape without evaluating it.</summary>
public static partial class AccessExpressionProjector
{
    private static readonly HashSet<string> Functions = new(StringComparer.OrdinalIgnoreCase)
    {
        "abs", "avg", "count", "date", "dateadd", "datediff", "dlookup", "dsum", "first", "format",
        "iif", "instr", "isnull", "len", "max", "min", "nz", "sum", "val"
    };

    public static AccessExpressionProjection Project(
        string expression,
        IReadOnlyDictionary<string, IReadOnlyList<(string StableKey, string Kind)>>? objects,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? fields,
        IReadOnlySet<string>? controlNames = null,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>>? fieldSetsByObject = null)
    {
        var normalized = expression.Trim();
        var functions = FunctionPattern().Matches(MaskLiterals(normalized))
            .Select(match => match.Groups["name"].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(name => AccessSafeValues.RoleHash("access-expression-function", name.ToLowerInvariant()))
            .OrderBy(value => value, StringComparer.Ordinal).ToArray();
        var operators = OperatorPattern().Matches(MaskLiterals(normalized))
            .Select(match => match.Value)
            .Distinct(StringComparer.Ordinal)
            .Select(value => AccessSafeValues.RoleHash("access-expression-operator", value))
            .OrderBy(value => value, StringComparer.Ordinal).ToArray();
        var fieldKeys = new SortedSet<string>(StringComparer.Ordinal);
        var queryKeys = new SortedSet<string>(StringComparer.Ordinal);
        var selectedFields = new SortedSet<string>(StringComparer.Ordinal);
        var criteriaFields = new SortedSet<string>(StringComparer.Ordinal);
        var controlRefs = new SortedSet<string>(StringComparer.Ordinal);
        var literals = new SortedSet<string>(StringComparer.Ordinal);
        var unresolved = false;

        foreach (Match literal in LiteralPattern().Matches(normalized))
            literals.Add(literal.Groups["kind"].Value.StartsWith('"') ? "string" : "number");

        foreach (Match match in IdentifierPattern().Matches(MaskLiterals(normalized)))
        {
            var name = NormalizeIdentifier(match.Groups["name"].Value);
            if (Functions.Contains(name) || IsKeyword(name)) continue;
            if (fields is not null && fields.TryGetValue(name, out var candidates))
            {
                if (candidates.Count == 1) fieldKeys.Add(candidates[0]);
                else unresolved = true;
                continue;
            }
            if (objects is not null && objects.TryGetValue(name, out var objectCandidates))
            {
                if (objectCandidates.Count == 1 && objectCandidates[0].Kind is "query" or "table") queryKeys.Add(objectCandidates[0].StableKey);
                else unresolved = true;
                continue;
            }
            if (controlNames is not null && controlNames.Contains(name))
                controlRefs.Add(AccessSafeValues.RoleHash("access-expression-control", name));
            else
                unresolved = true;
        }

        var dlookup = DomainPattern().Match(normalized);
        if (dlookup.Success)
        {
            var args = SplitArguments(dlookup.Groups["args"].Value);
            if (args.Count >= 2)
            {
                var queryCandidate = ResolveObject(args[1], objects, queryKeys);
                var selected = queryCandidate is not null && fieldSetsByObject?.TryGetValue(queryCandidate, out var queryFields) == true
                    ? ResolveField(args[0], queryFields, selectedFields)
                    : ResolveField(args[0], fields, selectedFields);
                if (selected is null) unresolved = true;
                if (queryCandidate is null) unresolved = true;
                if (args.Count >= 3)
                {
                    foreach (var candidate in ExtractIdentifiers(args[2]))
                    {
                        var criteria = ResolveField(candidate, fields, criteriaFields);
                        if (criteria is null && controlNames?.Contains(candidate) == true)
                            controlRefs.Add(AccessSafeValues.RoleHash("access-expression-control", candidate));
                        else if (criteria is null)
                            unresolved = true;
                    }
                }
            }
            else unresolved = true;
        }

        var dynamic = Regex.IsMatch(normalized, @"\b(?:Eval|Run|Call)\s*\(", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
            || normalized.Contains("&", StringComparison.Ordinal) && normalized.Contains("[", StringComparison.Ordinal);
        var classification = dlookup.Success ? "domain-lookup"
            : functions.Length > 0 ? "calculated-expression"
            : "expression";
        var coverage = dynamic ? "partial" : unresolved ? "partial" : "complete";
        var gap = dynamic ? "AccessBindingExpressionDynamic" : unresolved ? "AccessBindingExpressionPartial" : null;
        return new(
            classification,
            AccessSafeValues.RoleHash("access-expression-structure", normalized),
            functions,
            operators,
            fieldKeys.ToArray(),
            controlRefs.ToArray(),
            queryKeys.ToArray(),
            selectedFields.ToArray(),
            criteriaFields.ToArray(),
            literals.ToArray(),
            coverage,
            gap);
    }

    private static string? ResolveField(string value, IReadOnlyDictionary<string, IReadOnlyList<string>>? fields, ISet<string> output)
    {
        var name = value.Trim().Trim('"').Trim('[', ']');
        if (fields is not null && fields.TryGetValue(name, out var candidates) && candidates.Count == 1)
        {
            output.Add(candidates[0]);
            return candidates[0];
        }
        return null;
    }

    private static string? ResolveObject(string value, IReadOnlyDictionary<string, IReadOnlyList<(string StableKey, string Kind)>>? objects, ISet<string> output)
    {
        var name = value.Trim().Trim('"').Trim('[', ']');
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

    private static string MaskLiterals(string value) => Regex.Replace(value, "\"(?:\"\"|[^\"])*\"", " ");

    [GeneratedRegex(@"\b(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\(", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FunctionPattern();
    [GeneratedRegex(@"(?<name>[A-Za-z_][A-Za-z0-9_]*|\[[^\]]+\])", RegexOptions.CultureInvariant)]
    private static partial Regex IdentifierPattern();
    [GeneratedRegex(@"(?<name>\bDLookup|\bDCount|\bDSum|\bDAvg|\bDMax|\bDMin)\s*\((?<args>[^()]*(?:\([^)]*\)[^()]*)*)\)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DomainPattern();
    [GeneratedRegex("(?<kind>\"(?:\"\"|[^\"])*\"|[-+]?\\d+(?:\\.\\d+)?)", RegexOptions.CultureInvariant)]
    private static partial Regex LiteralPattern();
    [GeneratedRegex(@"[+\-*/&<>=]|\b(?:AND|OR|NOT)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex OperatorPattern();
}
