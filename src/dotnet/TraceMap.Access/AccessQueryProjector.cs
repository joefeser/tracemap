using System.Text;
using System.Text.RegularExpressions;

namespace TraceMap.Access;

public static partial class AccessQueryProjector
{
    private static readonly string[] SqlFunctionNames = [
        "abs", "avg", "count", "date", "dateadd", "datediff", "dlookup", "dsum", "first", "format",
        "iif", "instr", "isnull", "len", "max", "min", "nz", "sum", "val"];
    private static readonly string[] SqlKeywordCallNames = ["in", "exists"];
    public static AccessQueryActionProjection ProjectActionLineage(
        string sql,
        string operationKind,
        IReadOnlyDictionary<string, IReadOnlyList<(string StableKey, string Kind)>> knownObjects,
        IReadOnlyDictionary<string, Dictionary<string, List<AccessFieldProjection>>> fieldLookups,
        IReadOnlyList<int>? parameterOrdinals = null)
    {
        var masked = MaskLiteralsAndComments(sql);
        var targetName = operationKind switch
        {
            "append" or "make-table" => MatchValue(masked, AppendTargetPattern()),
            "update" => MatchValue(masked, UpdateTargetPattern()),
            "delete" => MatchValue(masked, DeleteTargetPattern()),
            _ => null
        };
        var target = ResolveUnique(targetName, knownObjects);
        var targetFields = operationKind switch
        {
            "append" or "make-table" => ParseParenthesizedNames(masked, AppendTargetFieldsPattern().Match(masked)),
            "update" => ParseUpdateTargets(masked),
            _ => []
        };
        var targetFieldKeys = ResolveFieldsAligned(target?.StableKey, targetFields, fieldLookups);
        var mappings = new List<AccessQueryFieldMappingProjection>();
        var coverage = targetName is null ? "partial" : target is null ? "partial" : "complete";
        if (operationKind is "append" or "make-table")
        {
            var select = SelectList(masked);
            var items = select is null ? [] : SplitSelectItems(select);
            if (items.Count == 0) coverage = "partial";
            for (var index = 0; index < Math.Max(items.Count, targetFields.Count); index++)
            {
                var expression = index < items.Count ? items[index].Trim() : null;
                var targetField = index < targetFieldKeys.Count && targetFieldKeys[index].Length > 0 ? targetFieldKeys[index] : null;
                var sources = expression is null ? [] : ResolveExpressionFields(expression, knownObjects, fieldLookups);
                var mapped = expression is not null && targetField is not null && sources.Count > 0 ? "complete" : "partial";
                if (mapped == "partial") coverage = "partial";
                mappings.Add(new(index, expression is null ? null : AccessSafeValues.RoleHash("access-query-expression", expression), sources, targetField, mapped));
            }
        }
        else if (operationKind == "update")
        {
            var set = SetClause(masked);
            var assignments = set is null ? [] : SplitSelectItems(set);
            if (assignments.Count == 0) coverage = "partial";
            for (var index = 0; index < assignments.Count; index++)
            {
                var assignment = assignments[index];
                var equals = assignment.IndexOf('=');
                var targetField = equals < 0 ? null : ResolveFieldsAligned(target?.StableKey, [assignment[..equals].Trim()], fieldLookups).FirstOrDefault();
                if (targetField is { Length: 0 }) targetField = null;
                var expression = equals < 0 ? null : assignment[(equals + 1)..].Trim();
                var sources = expression is null ? [] : ResolveExpressionFields(expression, knownObjects, fieldLookups);
                var mapped = targetField is not null && expression is not null && sources.Count > 0 ? "complete" : "partial";
                if (mapped == "partial") coverage = "partial";
                mappings.Add(new(index, expression is null ? null : AccessSafeValues.RoleHash("access-query-expression", expression), sources, targetField, mapped));
            }
        }
        var predicate = PredicateClause(masked);
        var parameters = parameterOrdinals?.Distinct().OrderBy(value => value).ToArray() ?? [];
        return new(operationKind, target?.StableKey, targetFieldKeys, mappings,
            predicate is null ? null : AccessSafeValues.RoleHash("access-query-predicate", predicate), parameters, coverage);
    }

    public static AccessQueryCrosstabProjection ProjectCrosstabLineage(
        string sql,
        IReadOnlyDictionary<string, IReadOnlyList<(string StableKey, string Kind)>> knownObjects,
        IReadOnlyDictionary<string, Dictionary<string, List<AccessFieldProjection>>> fieldLookups)
    {
        var masked = MaskLiteralsAndComments(sql);
        var select = SelectListAfterKeyword(masked, "select");
        var rowExpressions = select is null ? [] : SplitSelectItems(select);
        var row = rowExpressions.SelectMany(item => ResolveExpressionFields(item, knownObjects, fieldLookups)).Distinct(StringComparer.Ordinal).ToArray();
        var aggregate = MatchValue(masked, TransformPattern());
        var pivot = MatchValue(masked, PivotPattern());
        var value = aggregate is null ? null : ExtractAggregateValue(aggregate);
        var staticColumns = ParsePivotColumns(sql);
        var rowsResolve = rowExpressions.Count > 0
            && rowExpressions.All(expression => ResolvesExpressionCompletely(expression, knownObjects, fieldLookups));
        var valueResolves = value is not null
            && ResolvesExpressionCompletely(value, knownObjects, fieldLookups);
        var pivotResolves = pivot is not null
            && ResolvesExpressionCompletely(pivot, knownObjects, fieldLookups);
        var coverage = rowsResolve && valueResolves && pivotResolves && staticColumns.Count > 0
            ? "complete"
            : "partial";
        return new(row,
            aggregate is null ? null : AccessSafeValues.RoleHash("access-query-aggregate", aggregate),
            value is null ? null : AccessSafeValues.RoleHash("access-query-value", value),
            pivot is null ? null : AccessSafeValues.RoleHash("access-query-pivot", pivot),
            staticColumns, coverage);
    }

    public static AccessQueryStaticProjection ProjectStaticSelect(
        string sql,
        IReadOnlyDictionary<string, IReadOnlyList<(string StableKey, string Kind)>> knownObjects,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>> fieldsByTable)
    {
        var masked = MaskLiteralsAndComments(sql);
        var dependencyProjection = ProjectDependencies(sql, knownObjects);
        var dependencyKeys = dependencyProjection.Dependencies.Select(item => item.TargetStableKey).ToHashSet(StringComparer.Ordinal);
        var scopedKnownObjects = knownObjects.ToDictionary(
            item => item.Key,
            item => (IReadOnlyList<(string StableKey, string Kind)>)item.Value.Where(candidate => dependencyKeys.Contains(candidate.StableKey)).ToArray(),
            StringComparer.OrdinalIgnoreCase);
        var select = SelectListAfterKeyword(masked, "select");
        var expressions = select is null ? [] : SplitSelectItems(select);
        var outputs = expressions.Select((expression, ordinal) =>
        {
            var trimmed = expression.Trim();
            var sourceFields = ResolveExpressionFieldKeys(trimmed, scopedKnownObjects, fieldsByTable);
            var nameHash = AccessSafeValues.RoleHash("access-query-output-name", trimmed);
            return new AccessQueryStaticOutputProjection(
                ordinal,
                nameHash,
                sourceFields,
                sourceFields.Count == 1 && IsStaticDirectProjection(trimmed) ? "complete" : "partial");
        }).ToArray();
        var predicate = Clause(masked, "where", ["group", "order", ";", "$"]);
        var order = Clause(masked, "order\\s+by", [";", "$"]);
        var functions = predicate is null
            ? []
            : NamedFunctionPattern().Matches(predicate)
                .Select(match => match.Groups["name"].Value)
                .Where(name => !SqlFunctionNames.Contains(name, StringComparer.OrdinalIgnoreCase)
                    && !SqlKeywordCallNames.Contains(name, StringComparer.OrdinalIgnoreCase))
                .Select(name => AccessSafeValues.RoleHash("access-query-function-name", name))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        var predicateComplete = predicate is null || ResolvesExpressionFieldKeys(predicate, scopedKnownObjects, fieldsByTable);
        var orderComplete = order is null || ResolvesExpressionFieldKeys(order, scopedKnownObjects, fieldsByTable);
        var coverage = expressions.Count > 0
            && outputs.All(output => output.Coverage == "complete")
            && dependencyProjection.Coverage == "complete"
            && predicateComplete
            && orderComplete
            && functions.Length == 0
            ? "complete"
            : "partial";
        return new(
            AccessSafeValues.RoleHash("access-query-sql", sql),
            sql.Length,
            dependencyProjection.Dependencies,
            predicate is null ? null : AccessSafeValues.RoleHash("access-query-predicate", predicate),
            order is null ? null : AccessSafeValues.RoleHash("access-query-order-by", order),
            functions,
            outputs,
            coverage);
    }

    public static bool IsDirectOutputField(string sql, string outputName)
    {
        if (string.IsNullOrWhiteSpace(sql) || string.IsNullOrWhiteSpace(outputName))
            return false;
        var masked = MaskLiteralsAndComments(sql);
        var match = SelectListPattern().Match(masked);
        if (!match.Success) return false;
        var fields = new List<string>();
        foreach (var item in SplitSelectItems(match.Groups["list"].Value))
        {
            if (item.Contains('*')) return false;
            var direct = DirectSelectFieldPattern().Match(item.Trim());
            if (!direct.Success) continue;
            fields.Add((direct.Groups["bracketed"].Success
                ? direct.Groups["bracketed"].Value
                : direct.Groups["plain"].Value).Trim());
        }
        return fields.Count(field => string.Equals(field, outputName.Trim(), StringComparison.OrdinalIgnoreCase)) == 1;
    }

    private static IReadOnlyList<string> SplitSelectItems(string value)
    {
        var result = new List<string>();
        var start = 0;
        var parentheses = 0;
        var bracket = false;
        for (var index = 0; index < value.Length; index++)
        {
            var current = value[index];
            if (current == '[') bracket = true;
            else if (current == ']') bracket = false;
            else if (!bracket && current == '(') parentheses++;
            else if (!bracket && current == ')' && parentheses > 0) parentheses--;
            else if (!bracket && parentheses == 0 && current == ',')
            {
                result.Add(value[start..index]);
                start = index + 1;
            }
        }
        result.Add(value[start..]);
        return result;
    }

    private static string? SelectList(string masked) => SelectListAfterKeyword(masked, "select");

    private static string? SelectListAfterKeyword(string masked, string keyword)
    {
        var prefix = Regex.Match(masked, $@"(?is)\b{keyword}\b\s+(?:(?:distinct|distinctrow|top\s+\d+(?:\s+percent)?)\s+)*");
        if (!prefix.Success) return null;
        var start = prefix.Index + prefix.Length;
        var depth = 0;
        var bracket = false;
        for (var index = start; index < masked.Length; index++)
        {
            var current = masked[index];
            if (current == '[') bracket = true;
            else if (current == ']') bracket = false;
            else if (!bracket && current == '(') depth++;
            else if (!bracket && current == ')' && depth > 0) depth--;
            else if (!bracket && depth == 0 && IsKeywordAt(masked, index, "from"))
                return masked[start..index].Trim();
        }
        return null;
    }

    private static bool IsKeywordAt(string value, int index, string keyword) =>
        index + keyword.Length <= value.Length
        && string.Equals(value.Substring(index, keyword.Length), keyword, StringComparison.OrdinalIgnoreCase)
        && (index == 0 || !char.IsLetterOrDigit(value[index - 1]) && value[index - 1] != '_')
        && (index + keyword.Length == value.Length || !char.IsLetterOrDigit(value[index + keyword.Length]) && value[index + keyword.Length] != '_');

    private static string? MatchValue(string masked, Regex regex)
    {
        var match = regex.Match(masked);
        return match.Success ? match.Groups["value"].Value.Trim() : null;
    }

    private static AccessSafeIdentity? ResolveUnique(string? name, IReadOnlyDictionary<string, IReadOnlyList<(string StableKey, string Kind)>> known)
    {
        if (string.IsNullOrWhiteSpace(name) || !known.TryGetValue(name, out var candidates) || candidates.Count != 1) return null;
        var candidate = candidates[0];
        return new(null, AccessSafeValues.RoleHash("access-resolved-name", name), candidate.StableKey);
    }

    private static IReadOnlyList<string> ParseParenthesizedNames(string masked, Match match)
    {
        if (!match.Success || !match.Groups["fields"].Success) return [];
        return match.Groups["fields"].Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(UnquoteIdentifier).ToArray();
    }

    private static IReadOnlyList<string> ParseUpdateTargets(string masked)
    {
        var set = SetClause(masked);
        return set is null ? [] : SplitSelectItems(set).Select(item => item[..Math.Max(0, item.IndexOf('='))].Trim()).Where(value => value.Length > 0).Select(UnquoteIdentifier).ToArray();
    }

    private static string UnquoteIdentifier(string value) => value.Trim().Trim('[', ']').Split('.').Last().Trim('[', ']');

    private static IReadOnlyList<string> ResolveFieldsAligned(string? tableKey, IReadOnlyList<string> names, IReadOnlyDictionary<string, Dictionary<string, List<AccessFieldProjection>>> fields)
    {
        if (tableKey is null || !fields.TryGetValue(tableKey, out var lookup)) return names.Select(_ => string.Empty).ToArray();
        return names.Select(name => lookup.TryGetValue(UnquoteIdentifier(name), out var candidates) && candidates.Count == 1
            ? candidates[0].Identity.StableKey : string.Empty).ToArray();
    }

    private static string? ExtractAggregateValue(string aggregate)
    {
        var open = aggregate.IndexOf('(');
        if (open < 0 || !aggregate.EndsWith(')')) return null;
        var depth = 0;
        for (var index = open; index < aggregate.Length; index++)
        {
            if (aggregate[index] == '(') depth++;
            else if (aggregate[index] == ')' && --depth == 0)
                return index == aggregate.Length - 1 && index > open + 1
                    ? aggregate[(open + 1)..index].Trim()
                    : null;
        }
        return null;
    }

    private static IReadOnlyList<string> ResolveExpressionFields(string expression, IReadOnlyDictionary<string, IReadOnlyList<(string StableKey, string Kind)>> known, IReadOnlyDictionary<string, Dictionary<string, List<AccessFieldProjection>>> fields)
    {
        var result = new List<string>();
        foreach (Match match in FieldReferencePattern().Matches(expression))
        {
            var next = match.Index + match.Length;
            while (next < expression.Length && char.IsWhiteSpace(expression[next])) next++;
            if (next < expression.Length && expression[next] == '(') continue;
            var tableName = match.Groups["table"].Success ? match.Groups["table"].Value : null;
            var fieldName = match.Groups["field"].Value;
            var tables = tableName is null ? known.Values.SelectMany(value => value).Where(value => value.Kind == "table") : (known.TryGetValue(tableName, out var found) ? found : []);
            var matches = tables.Where(value => value.Kind == "table")
                .Where(table => fields.TryGetValue(table.StableKey, out var lookup)
                    && lookup.TryGetValue(fieldName, out var candidates)
                    && candidates.Count == 1)
                .SelectMany(table => fields[table.StableKey][fieldName].Select(field => (table.StableKey, Field: field.Identity.StableKey)))
                .ToArray();
            if (tableName is null && matches.Select(value => value.StableKey).Distinct(StringComparer.Ordinal).Count() != 1)
                continue;
            result.AddRange(matches.Select(value => value.Field));
        }
        return result.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    private static IReadOnlyList<string> ResolveExpressionFieldKeys(
        string expression,
        IReadOnlyDictionary<string, IReadOnlyList<(string StableKey, string Kind)>> known,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>> fields)
    {
        var result = new List<string>();
        foreach (Match match in FieldReferencePattern().Matches(expression))
        {
            var next = match.Index + match.Length;
            while (next < expression.Length && char.IsWhiteSpace(expression[next])) next++;
            if (next < expression.Length && expression[next] == '(') continue;
            var tableName = match.Groups["table"].Success ? match.Groups["table"].Value : null;
            var fieldName = match.Groups["field"].Value;
            var tables = tableName is null
                ? known.Values.SelectMany(value => value).Where(value => value.Kind == "table")
                : known.TryGetValue(tableName, out var found) ? found : [];
            var matches = tables.Where(table => fields.TryGetValue(table.StableKey, out var lookup)
                    && lookup.TryGetValue(fieldName, out var candidates)
                    && candidates.Count == 1)
                .SelectMany(table => fields[table.StableKey][fieldName])
                .ToArray();
            if (tableName is null && matches.Length != 1) continue;
            result.AddRange(matches);
        }
        return result.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    private static bool ResolvesExpressionFieldKeys(
        string expression,
        IReadOnlyDictionary<string, IReadOnlyList<(string StableKey, string Kind)>> known,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>> fields) =>
        FieldReferencePattern().Matches(expression).Cast<Match>().Where(match =>
        {
            var next = match.Index + match.Length;
            while (next < expression.Length && char.IsWhiteSpace(expression[next])) next++;
            return next >= expression.Length || expression[next] != '(';
        }).ToArray() is { Length: > 0 } matches
        && matches.All(match => ResolveExpressionFieldKeys(match.Value, known, fields).Count == 1);

    private static bool IsStaticDirectProjection(string expression) =>
        DirectSelectFieldPattern().IsMatch(expression.Trim());

    private static bool ResolvesExpressionCompletely(
        string expression,
        IReadOnlyDictionary<string, IReadOnlyList<(string StableKey, string Kind)>> known,
        IReadOnlyDictionary<string, Dictionary<string, List<AccessFieldProjection>>> fields)
    {
        var references = FieldReferencePattern().Matches(expression)
            .Where(match =>
            {
                var end = match.Index + match.Length;
                while (end < expression.Length && char.IsWhiteSpace(expression[end])) end++;
                return end >= expression.Length || expression[end] != '(';
            })
            .ToArray();
        return references.Length > 0
            && references.All(reference => ResolveExpressionFields(reference.Value, known, fields).Count == 1);
    }

    private static string? SetClause(string masked) => Clause(masked, "set", ["where", "order", ";", "$"]);
    private static string? PredicateClause(string masked) => Clause(masked, "where", ["group", "order", ";", "$"]);
    private static string? Clause(string masked, string keyword, string[] boundaries)
    {
        var start = Regex.Match(masked, $@"(?is)\b{keyword}\b");
        if (!start.Success) return null;
        var tail = masked[(start.Index + start.Length)..];
        var end = Regex.Match(tail, $@"(?is)\s+(?:{string.Join('|', boundaries.Where(x => x != "$"))})\b|;").Index;
        return tail[..(end == 0 ? tail.Length : end)].Trim();
    }

    private static IReadOnlyList<string> ParsePivotColumns(string sql)
    {
        var match = PivotPattern().Match(MaskLiteralsAndComments(sql));
        if (!match.Success) return [];
        var masked = MaskLiteralsAndComments(sql);
        var inMatch = Regex.Match(masked[match.Index..], @"(?is)\bin\s*\((?<values>[^)]*)\)");
        if (!inMatch.Success) return [];
        var original = sql[(match.Index + inMatch.Index)..];
        var originalValues = Regex.Match(original, @"(?is)\bin\s*\((?<values>[^)]*)\)");
        if (!originalValues.Success) return [];
        var values = SplitSelectItems(originalValues.Groups["values"].Value)
            .Select(value => UnquotePivotLiteral(value.Trim()))
            .Where(value => value is not null)
            .Select(value => AccessSafeValues.RoleHash("access-query-pivot-column", value!))
            .ToArray();
        return values.Length == 0 ? [] : values.OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    private static string? UnquotePivotLiteral(string value)
    {
        if (value.Length >= 2 && value[0] == '\'' && value[^1] == '\'')
            return value[1..^1].Replace("''", "'", StringComparison.Ordinal);
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
            return value[1..^1].Replace("\"\"", "\"", StringComparison.Ordinal);
        return Regex.IsMatch(value, @"^[+-]?(?:\d+(?:\.\d*)?|\.\d+)$", RegexOptions.CultureInvariant)
            ? value
            : null;
    }

    public static (IReadOnlyList<AccessQueryDependencyProjection> Dependencies, string Coverage, bool UnsupportedShape) ProjectDependencies(
        string sql,
        IReadOnlyDictionary<string, IReadOnlyList<(string StableKey, string Kind)>> knownObjects)
    {
        var masked = MaskLiteralsAndComments(sql);
        var dependencies = new SortedDictionary<string, AccessQueryDependencyProjection>(StringComparer.Ordinal);
        var ambiguous = false;
        var unresolved = false;

        foreach (var name in ReferenceNames(masked))
        {
            if (!knownObjects.TryGetValue(name, out var candidates) || candidates.Count == 0)
            {
                unresolved = true;
                continue;
            }
            if (candidates.Count != 1)
            {
                ambiguous = true;
                continue;
            }

            var candidate = candidates[0];
            dependencies[candidate.StableKey] = new AccessQueryDependencyProjection(candidate.StableKey, candidate.Kind, "direct-static-reference");
        }

        var unsupported = UnsupportedPattern().IsMatch(masked);
        var coverage = ambiguous || unresolved || unsupported ? "partial" : "complete";
        return (dependencies.Values.ToArray(), coverage, unsupported || ambiguous || unresolved);
    }

    private static IEnumerable<string> ReferenceNames(string masked)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in ReferencePattern().Matches(masked))
            names.Add(MatchedName(match));

        // Access permits comma-separated sources. Bound this additional scan to the
        // initial FROM list; JOIN clauses are handled by ReferencePattern and may
        // contain expression commas that are not object references.
        foreach (Match clause in FromClausePattern().Matches(masked))
        {
            var body = clause.Groups["body"].Value;
            var join = JoinBoundaryPattern().Match(body);
            if (join.Success) body = body[..join.Index];
            foreach (Match match in CommaReferencePattern().Matches(body))
                names.Add(MatchedName(match));
        }

        return names;
    }

    private static string MatchedName(Match match) =>
        (match.Groups["bracketed"].Success ? match.Groups["bracketed"].Value : match.Groups["plain"].Value).Trim();

    public static string MaskLiteralsAndComments(string sql)
    {
        var builder = new StringBuilder(sql.Length);
        var quote = '\0';
        for (var index = 0; index < sql.Length; index++)
        {
            var current = sql[index];
            if (quote != '\0')
            {
                builder.Append(char.IsWhiteSpace(current) ? current : ' ');
                if (current == quote)
                {
                    if (index + 1 < sql.Length && sql[index + 1] == quote)
                    {
                        builder.Append(' ');
                        index++;
                    }
                    else quote = '\0';
                }
                continue;
            }

            if (current is '\'' or '"')
            {
                quote = current;
                // Preserve only the fact that a literal occupied this position. This lets
                // unsupported external IN clauses be recognized without retaining content.
                builder.Append('#');
                continue;
            }

            if (current == '-' && index + 1 < sql.Length && sql[index + 1] == '-')
            {
                while (index < sql.Length && sql[index] is not ('\r' or '\n'))
                {
                    builder.Append(' ');
                    index++;
                }
                if (index < sql.Length) builder.Append(sql[index]);
                continue;
            }

            builder.Append(current);
        }
        return builder.ToString();
    }

    [GeneratedRegex(@"(?ix)\b(?:from|join|update|into|table)\s+\(*\s*(?:\[(?<bracketed>[^\]]+)\]|(?<plain>[A-Za-z_][A-Za-z0-9_.$]*))", RegexOptions.CultureInvariant)]
    private static partial Regex ReferencePattern();

    [GeneratedRegex(@"(?ix)\bfrom\b(?<body>.*?)(?=\b(?:where|group\s+by|having|order\s+by|union|transform)\b|;|$)", RegexOptions.CultureInvariant)]
    private static partial Regex FromClausePattern();

    [GeneratedRegex(@"(?ix)\b(?:(?:inner|left|right|full|cross)\s+)?join\b", RegexOptions.CultureInvariant)]
    private static partial Regex JoinBoundaryPattern();

    [GeneratedRegex(@"(?ix),\s*\(*\s*(?:\[(?<bracketed>[^\]]+)\]|(?<plain>[A-Za-z_][A-Za-z0-9_.$]*))", RegexOptions.CultureInvariant)]
    private static partial Regex CommaReferencePattern();

    [GeneratedRegex(@"(?ix)\b(?:transform\b|union\b|in\s+\#|parameters\s+[^;]+\s+(?:text|long|short|datetime)\b)", RegexOptions.CultureInvariant)]
    private static partial Regex UnsupportedPattern();

    [GeneratedRegex(@"(?is)\bselect\s+(?:(?:distinct|distinctrow|top\s+\d+(?:\s+percent)?)\s+)*(?<list>.*?)\s+\bfrom\b", RegexOptions.CultureInvariant)]
    private static partial Regex SelectListPattern();

    [GeneratedRegex(@"^(?:(?:\[[^\]]+\]|[A-Za-z_][A-Za-z0-9_.$]*)\s*\.\s*)?(?:\[(?<bracketed>[^\]]+)\]|(?<plain>[A-Za-z_][A-Za-z0-9_ ]*))$", RegexOptions.CultureInvariant)]
    private static partial Regex DirectSelectFieldPattern();

    [GeneratedRegex(@"(?ix)\binto\s+(?:\[(?<value>[^\]]+)\]|(?<value>[A-Za-z_][A-Za-z0-9_.$]*))", RegexOptions.CultureInvariant)]
    private static partial Regex AppendTargetPattern();

    [GeneratedRegex(@"(?ix)\binto\s+(?:\[[^\]]+\]|[A-Za-z_][A-Za-z0-9_.$]*)\s*\((?<fields>[^)]*)\)", RegexOptions.CultureInvariant)]
    private static partial Regex AppendTargetFieldsPattern();

    [GeneratedRegex(@"(?ix)\bupdate\s+(?:\[(?<value>[^\]]+)\]|(?<value>[A-Za-z_][A-Za-z0-9_.$]*))", RegexOptions.CultureInvariant)]
    private static partial Regex UpdateTargetPattern();

    [GeneratedRegex(@"(?ix)\bdelete\s+from\s+(?:\[(?<value>[^\]]+)\]|(?<value>[A-Za-z_][A-Za-z0-9_.$]*))", RegexOptions.CultureInvariant)]
    private static partial Regex DeleteTargetPattern();

    [GeneratedRegex(@"(?is)\btransform\b(?<value>.*?)\bselect\b", RegexOptions.CultureInvariant)]
    private static partial Regex TransformPattern();

    [GeneratedRegex(@"(?is)\bpivot\b(?<value>.*?)(?:\bin\b|;|$)", RegexOptions.CultureInvariant)]
    private static partial Regex PivotPattern();

    [GeneratedRegex(@"(?ix)(?:(?:\[(?<table>[^\]]+)\]|(?<table>[A-Za-z_][A-Za-z0-9_]*))\s*\.\s*)?(?:\[(?<field>[^\]]+)\]|(?<field>[A-Za-z_][A-Za-z0-9_]*))", RegexOptions.CultureInvariant)]
    private static partial Regex FieldReferencePattern();

    [GeneratedRegex(@"(?ix)\b(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\(", RegexOptions.CultureInvariant)]
    private static partial Regex NamedFunctionPattern();
}
