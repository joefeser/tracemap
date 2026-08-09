using System.Text;
using System.Text.RegularExpressions;

namespace TraceMap.Access;

public static partial class AccessQueryProjector
{
    internal sealed record StaticOutputCatalogEntry(int Ordinal, string Name);
    internal sealed record CrosstabOutputCatalogEntry(
        int Ordinal,
        string Name,
        IReadOnlyList<string> SourceFieldStableKeys,
        string Coverage,
        string OutputKind,
        string AliasKind = "unknown",
        string? SourceExpressionHash = null,
        IReadOnlyList<string>? PivotSourceFieldStableKeys = null);
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
        var dependencyProjection = ProjectDependencies(sql, knownObjects);
        var sourceKeys = dependencyProjection.Dependencies
            .Select(item => item.TargetStableKey)
            .Where(stableKey => operationKind is not ("append" or "make-table")
                || !string.Equals(stableKey, target?.StableKey, StringComparison.Ordinal))
            .ToHashSet(StringComparer.Ordinal);
        var sourceObjects = knownObjects.ToDictionary(
            item => item.Key,
            item => (IReadOnlyList<(string StableKey, string Kind)>)item.Value
                .Where(candidate => sourceKeys.Contains(candidate.StableKey))
                .ToArray(),
            StringComparer.OrdinalIgnoreCase);
        var targetFields = operationKind switch
        {
            "append" or "make-table" => ParseParenthesizedNames(masked, AppendTargetFieldsPattern().Match(masked)),
            "update" => ParseUpdateTargets(masked),
            _ => []
        };
        var targetFieldKeys = ResolveFieldsAligned(target?.StableKey, targetFields, fieldLookups);
        var mappings = new List<AccessQueryFieldMappingProjection>();
        var coverage = targetName is not null
            && target is not null
            && dependencyProjection.Coverage == "complete"
            ? "complete"
            : "partial";
        if (operationKind is "append" or "make-table")
        {
            var select = SelectList(masked);
            var items = select is null ? [] : SplitSelectItems(select);
            if (items.Count == 0) coverage = "partial";
            for (var index = 0; index < Math.Max(items.Count, targetFields.Count); index++)
            {
                var expression = index < items.Count ? items[index].Trim() : null;
                var targetField = index < targetFieldKeys.Count && targetFieldKeys[index].Length > 0 ? targetFieldKeys[index] : null;
                var sources = expression is null ? [] : ResolveExpressionFields(expression, sourceObjects, fieldLookups);
                var mapped = expression is not null
                    && targetField is not null
                    && sources.Count > 0
                    && ResolvesExpressionCompletely(expression, sourceObjects, fieldLookups)
                    && !HasUnsupportedNamedFunction(expression)
                    ? "complete"
                    : "partial";
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
                var sources = expression is null ? [] : ResolveExpressionFields(expression, sourceObjects, fieldLookups);
                var mapped = targetField is not null
                    && expression is not null
                    && sources.Count > 0
                    && ResolvesExpressionCompletely(expression, sourceObjects, fieldLookups)
                    && !HasUnsupportedNamedFunction(expression)
                    ? "complete"
                    : "partial";
                if (mapped == "partial") coverage = "partial";
                mappings.Add(new(index, expression is null ? null : AccessSafeValues.RoleHash("access-query-expression", expression), sources, targetField, mapped));
            }
        }
        var predicate = PredicateClause(masked);
        if (predicate is not null
            && (!ResolvesExpressionCompletely(predicate, sourceObjects, fieldLookups)
                || HasUnsupportedNamedFunction(predicate)))
            coverage = "partial";
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
        var pivot = PivotExpression(masked);
        var value = aggregate is null ? null : ExtractAggregateValue(aggregate);
        var aggregateSources = value is null
            ? []
            : ResolveExpressionFields(value, knownObjects, fieldLookups);
        var pivotSources = pivot is null
            ? []
            : ResolveExpressionFields(pivot, knownObjects, fieldLookups);
        var pivotColumnsComplete = TryParsePivotColumnNames(sql, out var pivotColumnNames);
        var staticColumns = pivotColumnNames
            .Select(value => AccessSafeValues.RoleHash("access-query-pivot-column", value))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var rowsResolve = rowExpressions.Count > 0
            && rowExpressions.All(expression => ResolvesExpressionCompletely(expression, knownObjects, fieldLookups));
        var valueResolves = value is not null
            && ResolvesExpressionCompletely(value, knownObjects, fieldLookups);
        var pivotResolves = pivot is not null
            && ResolvesExpressionCompletely(pivot, knownObjects, fieldLookups);
        var expressionsUseOnlySupportedFunctions = rowExpressions.All(expression => !HasUnsupportedNamedFunction(expression))
            && (aggregate is null || !HasUnsupportedNamedFunction(aggregate))
            && (pivot is null || !HasUnsupportedNamedFunction(pivot));
        var coverage = HasCompleteStaticSelectShape(masked)
            && rowsResolve && valueResolves && pivotResolves && staticColumns.Length > 0
            && pivotColumnsComplete
            && expressionsUseOnlySupportedFunctions
            ? "complete"
            : "partial";
        return new(row,
            aggregate is null ? null : AccessSafeValues.RoleHash("access-query-aggregate", aggregate),
            value is null ? null : AccessSafeValues.RoleHash("access-query-value", value),
            pivot is null ? null : AccessSafeValues.RoleHash("access-query-pivot", pivot),
            staticColumns, coverage, aggregateSources, pivotSources);
    }

    internal static IReadOnlyList<CrosstabOutputCatalogEntry> ProjectCrosstabOutputCatalog(
        string sql,
        IReadOnlyDictionary<string, IReadOnlyList<(string StableKey, string Kind)>> knownObjects,
        IReadOnlyDictionary<string, Dictionary<string, List<AccessFieldProjection>>> fieldLookups)
    {
        if (string.IsNullOrWhiteSpace(sql)) return [];
        var masked = MaskLiteralsAndComments(sql);
        var outputs = new List<CrosstabOutputCatalogEntry>();
        var select = SelectListAfterKeyword(masked, "select");
        IReadOnlyList<string> selectItems = select is null ? [] : SplitSelectItems(select);
        var selectComplete = select is not null
            && HasCompleteStaticSelectShape(masked)
            && ProjectionStructureComplete(select)
            && selectItems.All(item => !string.IsNullOrWhiteSpace(item) && !IsWildcardProjectionItem(item));
        for (var ordinal = 0; ordinal < selectItems.Count; ordinal++)
        {
            var expression = selectItems[ordinal];
            var staticName = StaticOutputName(expression);
            var name = staticName ?? $"unresolved-output-{ordinal}";
            var analysisExpression = RemoveOutputAlias(expression);
            var sources = ResolveExpressionFields(analysisExpression, knownObjects, fieldLookups);
            var coverage = staticName is not null
                && selectComplete
                && sources.Count > 0
                && ResolvesExpressionCompletely(analysisExpression, knownObjects, fieldLookups)
                && !HasUnsupportedNamedFunction(analysisExpression)
                ? "complete"
                : "partial";
            outputs.Add(new(
                ordinal,
                name,
                sources,
                coverage,
                staticName is null ? "row-heading-unresolved-name" : "row-heading",
                OutputAliasKind(expression),
                AccessSafeValues.RoleHash("access-query-output-expression", analysisExpression)));
        }

        var nextOrdinal = selectItems.Count;
        var aggregate = MatchValue(masked, TransformPattern());
        var aggregateValue = aggregate is null ? null : ExtractAggregateValue(RemoveOutputAlias(aggregate));
        var aggregateSources = aggregateValue is null
            ? []
            : ResolveExpressionFields(aggregateValue, knownObjects, fieldLookups);
        var pivot = PivotExpression(masked);
        var pivotSources = pivot is null
            ? []
            : ResolveExpressionFields(pivot, knownObjects, fieldLookups);
        var pivotColumnsComplete = TryParsePivotColumnNames(sql, out var pivotColumnNames);
        var aggregateSourceCoverage = selectComplete
            && pivotColumnsComplete
            && aggregateValue is not null
            && aggregateSources.Count > 0
            && ResolvesExpressionCompletely(aggregateValue, knownObjects, fieldLookups)
            && !HasUnsupportedNamedFunction(aggregateValue)
            && pivot is not null
            && pivotSources.Count > 0
            && ResolvesExpressionCompletely(pivot, knownObjects, fieldLookups)
            && !HasUnsupportedNamedFunction(pivot)
            ? "complete"
            : "partial";
        foreach (var name in pivotColumnNames)
            outputs.Add(new(
                nextOrdinal++,
                name,
                aggregateSources,
                aggregateSourceCoverage,
                "static-pivot",
                AccessQueryOutputAliasKinds.PivotLiteral,
                pivot is null ? null : AccessSafeValues.RoleHash("access-query-pivot", pivot),
                pivotSources));

        var duplicateNames = outputs
            .GroupBy(output => output.Name, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return outputs
            .Select(output => duplicateNames.Contains(output.Name)
                ? output with { Coverage = "partial", OutputKind = $"{output.OutputKind}-duplicate-name" }
                : output)
            .ToArray();
    }

    public static AccessQueryStaticProjection ProjectStaticSelect(
        string sql,
        IReadOnlyDictionary<string, IReadOnlyList<(string StableKey, string Kind)>> knownObjects,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>> fieldsByTable) =>
        ProjectStaticSelect(sql, knownObjects, fieldsByTable, null);

    internal static AccessQueryStaticProjection ProjectStaticSelect(
        string sql,
        IReadOnlyDictionary<string, IReadOnlyList<(string StableKey, string Kind)>> knownObjects,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>> fieldsByTable,
        (IReadOnlyList<AccessQueryDependencyProjection> Dependencies, string Coverage, bool UnsupportedShape)? precomputedDependencies)
    {
        var masked = MaskLiteralsAndComments(sql);
        var balanced = HasBalancedSqlDelimiters(masked);
        var completeShape = HasCompleteStaticSelectShape(masked);
        var dependencyProjection = precomputedDependencies ?? ProjectDependencies(sql, knownObjects);
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
            var analysisExpression = RemoveOutputAlias(trimmed);
            var sourceFields = ResolveExpressionFieldKeys(analysisExpression, scopedKnownObjects, fieldsByTable);
            var outputName = StaticOutputName(trimmed);
            var nameHash = outputName is null
                ? null
                : AccessSafeValues.RoleHash("access-query-output-name", outputName);
            return new AccessQueryStaticOutputProjection(
                ordinal,
                nameHash,
                sourceFields,
                sourceFields.Count == 1 && IsStaticDirectProjection(trimmed) ? "complete" : "partial",
                OutputAliasKind(trimmed),
                outputName is null ? null : AccessSafeValues.RoleHash("access-query-output-expression", analysisExpression));
        }).ToArray();
        var predicate = Clause(masked, "where", ["group", "order", ";", "$"]);
        var order = Clause(masked, "order\\s+by", [";", "$"]);
        var runtimeExpressions = expressions
            .Concat(new[] { predicate, order }.Where(expression => expression is not null).Cast<string>())
            .ToArray();
        var runtimeFunctionsPresent = runtimeExpressions
            .SelectMany(expression => NamedFunctionPattern().Matches(expression))
            .Select(match => match.Groups["name"].Value)
            .Any(name => !SqlKeywordCallNames.Contains(name, StringComparer.OrdinalIgnoreCase));
        var functions = runtimeExpressions
            .Where(expression => expression is not null)
            .SelectMany(expression => NamedFunctionPattern().Matches(expression))
                .Select(match => match.Groups["name"].Value)
                .Where(name => !SqlFunctionNames.Contains(name, StringComparer.OrdinalIgnoreCase)
                    && !SqlKeywordCallNames.Contains(name, StringComparer.OrdinalIgnoreCase))
                .Select(name => AccessSafeValues.RoleHash("access-query-function-name", name))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        var predicateComplete = predicate is null || ResolvesExpressionFieldKeys(predicate, scopedKnownObjects, fieldsByTable);
        var orderComplete = order is null || ResolvesExpressionFieldKeys(order, scopedKnownObjects, fieldsByTable);
        orderComplete = orderComplete && (order is null || !HasUnsupportedNamedFunction(order));
        var coverage = expressions.Count > 0
            && balanced
            && completeShape
            && outputs.All(output => output.Coverage == "complete")
            && dependencyProjection.Coverage == "complete"
            && predicateComplete
            && orderComplete
            && functions.Length == 0
            ? "complete"
            : "partial";
        var outputCoverage = expressions.Count > 0
            && balanced
            && completeShape
            && outputs.All(output => output.NameHash is not null && output.Coverage == "complete")
            ? "complete"
            : "partial";
        var runtimeValueCoverage = predicateComplete
            && balanced
            && completeShape
            && orderComplete
            && !runtimeFunctionsPresent
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
            coverage,
            dependencyProjection.Coverage,
            outputCoverage,
            runtimeValueCoverage);
    }

    internal static IReadOnlyList<StaticOutputCatalogEntry> ProjectStaticOutputCatalog(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql)) return [];
        var select = SelectListAfterKeyword(MaskLiteralsAndComments(sql), "select");
        if (select is null) return [];
        var parsed = SplitSelectItems(select)
            .Select((expression, ordinal) => (Name: StaticOutputName(expression), Ordinal: ordinal))
            .Where(item => !string.IsNullOrWhiteSpace(item.Name))
            .ToArray();
        var duplicateNames = parsed.GroupBy(item => item.Name!, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return parsed
            .Where(item => !duplicateNames.Contains(item.Name!))
            .Select(item => new StaticOutputCatalogEntry(item.Ordinal, item.Name!))
            .ToArray();
    }

    public static bool IsDirectOutputField(string sql, string outputName)
    {
        if (string.IsNullOrWhiteSpace(sql) || string.IsNullOrWhiteSpace(outputName))
            return false;
        var select = SelectListAfterKeyword(MaskLiteralsAndComments(sql), "select");
        if (select is null) return false;
        return SplitSelectItems(select).Count(item =>
            IsStaticDirectProjection(item)
            && string.Equals(
                StaticOutputName(item),
                outputName.Trim(),
                StringComparison.OrdinalIgnoreCase)) == 1;
    }

    internal static bool CanReconcileStaticOutputByOrdinal(string sql, int ordinal, string outputName)
    {
        if (!CanReconcileStaticOutputProvenanceByOrdinal(sql, ordinal, outputName))
            return false;
        var select = SelectListAfterKeyword(MaskLiteralsAndComments(sql), "select")!;
        return IsStaticDirectProjection(SplitSelectItems(select)[ordinal]);
    }

    internal static bool CanReconcileStaticOutputProvenanceByOrdinal(string sql, int ordinal, string outputName)
    {
        if (string.IsNullOrWhiteSpace(sql) || ordinal < 0 || string.IsNullOrWhiteSpace(outputName))
            return false;
        var masked = MaskLiteralsAndComments(sql);
        if (!HasCompleteStaticSelectShape(masked)) return false;
        var select = SelectListAfterKeyword(masked, "select");
        if (select is null) return false;
        var expressions = SplitSelectItems(select);
        return ordinal < expressions.Count
            && ProjectionStructureComplete(select)
            && expressions.All(expression => !string.IsNullOrWhiteSpace(expression))
            && !expressions.Take(ordinal + 1).Any(IsWildcardProjectionItem)
            && string.Equals(
                StaticOutputName(expressions[ordinal]),
                outputName.Trim(),
                StringComparison.OrdinalIgnoreCase);
    }

    public static bool HasStaticOutputName(string sql, string outputName)
    {
        if (string.IsNullOrWhiteSpace(sql) || string.IsNullOrWhiteSpace(outputName))
            return false;
        var select = SelectListAfterKeyword(MaskLiteralsAndComments(sql), "select");
        if (select is null) return false;
        var names = SplitSelectItems(select)
            .Select(StaticOutputName)
            .Where(name => name is not null)
            .Cast<string>()
            .ToArray();
        return names.Count(name => string.Equals(name, outputName.Trim(), StringComparison.OrdinalIgnoreCase)) == 1;
    }

    internal static bool HasWildcardProjection(string sql)
    {
        return TryGetSelectItems(sql, out var items) && items.Any(IsWildcardProjectionItem);
    }

    internal static bool HasOnlyWildcardProjection(string sql)
    {
        if (!TryGetSelectItems(sql, out var items) || items.Count != 1 || !items.All(IsWildcardProjectionItem))
            return false;
        var masked = MaskLiteralsAndComments(sql);
        return HasCompleteSingleSourceSelectShape(masked);
    }

    internal static bool HasBalancedStaticSelectSyntax(string sql) =>
        !string.IsNullOrWhiteSpace(sql)
        && HasBalancedSqlDelimiters(MaskLiteralsAndComments(sql));

    private static bool TryGetSelectItems(string sql, out IReadOnlyList<string> items)
    {
        items = [];
        if (string.IsNullOrWhiteSpace(sql)) return false;
        var select = SelectListAfterKeyword(MaskLiteralsAndComments(sql), "select");
        if (select is null) return false;
        items = SplitSelectItems(select);
        return items.Count > 0;
    }

    private static bool IsWildcardProjectionItem(string item)
    {
        var normalized = item.Trim();
        const string identifier = @"(?:\[[^\]]+\]|[A-Za-z_][A-Za-z0-9_$]*)";
        return Regex.IsMatch(
            normalized,
            $@"(?is)^(?:{identifier}\s*\.\s*)*\*$",
            RegexOptions.CultureInvariant);
    }

    private static bool HasBalancedSqlDelimiters(string value)
    {
        var squareBrackets = 0;
        var parentheses = 0;
        foreach (var current in value)
        {
            if (current == '[') squareBrackets++;
            else if (current == ']')
            {
                if (squareBrackets == 0) return false;
                squareBrackets--;
            }
            else if (squareBrackets == 0 && current == '(') parentheses++;
            else if (squareBrackets == 0 && current == ')')
            {
                if (parentheses == 0) return false;
                parentheses--;
            }
        }
        return squareBrackets == 0 && parentheses == 0;
    }

    private static bool HasCompleteSingleSourceSelectShape(string masked)
    {
        if (!HasBalancedSqlDelimiters(masked)) return false;
        var topLevelFromIndexes = TopLevelKeywordIndexes(masked, "from", 0);
        if (topLevelFromIndexes.Count != 1) return false;

        var tail = masked[(topLevelFromIndexes[0] + "from".Length)..].Trim();
        if (tail.EndsWith(';')) tail = tail[..^1].TrimEnd();
        if (tail.Length == 0
            || Regex.IsMatch(
                tail,
                @"(?is)(?:,|=|<>|<=|>=|<|>|\+|-|\*|/|\b(?:where|having|group\s+by|order\s+by|join|on|and|or)\b)\s*$"))
            return false;

        var squareBrackets = 0;
        var parentheses = 0;
        var clauseStart = tail.Length;
        for (var index = 0; index < tail.Length; index++)
        {
            var current = tail[index];
            if (current == '[') squareBrackets++;
            else if (current == ']') squareBrackets--;
            else if (squareBrackets == 0 && current == '(') parentheses++;
            else if (squareBrackets == 0 && current == ')') parentheses--;
            else if (squareBrackets == 0 && parentheses == 0)
            {
                if (current == ',') return false;
                if (IsKeywordAt(tail, index, "where")
                    || IsKeywordAt(tail, index, "group")
                    || IsKeywordAt(tail, index, "having")
                    || IsKeywordAt(tail, index, "order")
                    || IsKeywordAt(tail, index, "join")
                    || IsKeywordAt(tail, index, "inner")
                    || IsKeywordAt(tail, index, "left")
                    || IsKeywordAt(tail, index, "right")
                    || IsKeywordAt(tail, index, "full")
                    || IsKeywordAt(tail, index, "cross")
                    || IsKeywordAt(tail, index, "union"))
                {
                    clauseStart = index;
                    break;
                }
            }
        }
        var identifier = @"(?:\[[^\]]+\]|[A-Za-z_][A-Za-z0-9_$]*)";
        var source = tail[..clauseStart].Trim();
        return Regex.IsMatch(
            source,
            $@"(?is)^{identifier}(?:\s*\.\s*{identifier})*(?:\s+as\s+{identifier})?$",
            RegexOptions.CultureInvariant);
    }

    private static bool HasCompleteStaticSelectShape(string masked)
    {
        if (!HasBalancedSqlDelimiters(masked)) return false;
        var topLevelFromIndexes = TopLevelKeywordIndexes(masked, "from", 0);
        if (topLevelFromIndexes.Count != 1) return false;
        var tail = masked[(topLevelFromIndexes[0] + "from".Length)..].Trim();
        if (tail.EndsWith(';')) tail = tail[..^1].TrimEnd();
        return tail.Length > 0
            && !Regex.IsMatch(
                tail,
                @"(?is)(?:,|=|<>|<=|>=|<|>|\+|-|\*|/|\b(?:where|having|group\s+by|order\s+by|join|on|and|or)\b)\s*$",
                RegexOptions.CultureInvariant);
    }

    private static IReadOnlyList<int> TopLevelKeywordIndexes(string value, string keyword, int start)
    {
        var result = new List<int>();
        var depth = 0;
        var bracket = false;
        for (var index = start; index < value.Length; index++)
        {
            var current = value[index];
            if (current == '[') bracket = true;
            else if (current == ']') bracket = false;
            else if (!bracket && current == '(') depth++;
            else if (!bracket && current == ')' && depth > 0) depth--;
            else if (!bracket && depth == 0 && IsKeywordAt(value, index, keyword)) result.Add(index);
        }
        return result;
    }

    private static string MatchedOutputName(Match match) =>
        (match.Groups["bracketed"].Success ? match.Groups["bracketed"].Value : match.Groups["plain"].Value).Trim();

    private static string? StaticOutputName(string expression)
    {
        var trimmed = expression.Trim();
        var alias = OutputAliasPattern().Match(trimmed);
        if (alias.Success) return MatchedOutputName(alias);
        var accessAlias = AccessOutputAliasPattern().Match(trimmed);
        if (accessAlias.Success) return MatchedOutputName(accessAlias);
        var direct = DirectSelectFieldPattern().Match(trimmed);
        return direct.Success
            ? (direct.Groups["bracketed"].Success ? direct.Groups["bracketed"].Value : direct.Groups["plain"].Value).Trim()
            : null;
    }

    private static string OutputAliasKind(string expression)
    {
        var trimmed = expression.Trim();
        if (OutputAliasPattern().IsMatch(trimmed)) return AccessQueryOutputAliasKinds.ExplicitAs;
        if (AccessOutputAliasPattern().IsMatch(trimmed)) return AccessQueryOutputAliasKinds.AccessColon;
        if (DirectSelectFieldPattern().IsMatch(trimmed)) return AccessQueryOutputAliasKinds.DirectField;
        return AccessQueryOutputAliasKinds.Unknown;
    }

    private static string RemoveOutputAlias(string expression)
    {
        var alias = OutputAliasPattern().Match(expression);
        if (alias.Success) return expression[..alias.Index].Trim();
        var accessAlias = AccessOutputAliasPattern().Match(expression);
        return accessAlias.Success ? expression[(accessAlias.Index + accessAlias.Length)..].Trim() : expression.Trim();
    }

    private static bool ProjectionStructureComplete(string value)
    {
        var parentheses = 0;
        var bracket = false;
        foreach (var current in value)
        {
            if (current == '[')
            {
                if (bracket) return false;
                bracket = true;
            }
            else if (current == ']')
            {
                if (!bracket) return false;
                bracket = false;
            }
            else if (!bracket && current == '(')
            {
                parentheses++;
            }
            else if (!bracket && current == ')')
            {
                if (parentheses == 0) return false;
                parentheses--;
            }
        }
        return !bracket && parentheses == 0;
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
        var fromIndexes = TopLevelKeywordIndexes(masked, "from", start);
        return fromIndexes.Count == 0 ? null : masked[start..fromIndexes[0]].Trim();
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
        => ResolveExpressionFieldStableKeys(expression, known, (stableKey, fieldName) =>
            UniqueFieldCandidates(fields, stableKey, fieldName)
                .Select(candidate => candidate.Identity.StableKey)
                .ToArray());

    private static IReadOnlyList<string> ResolveExpressionFieldKeys(
        string expression,
        IReadOnlyDictionary<string, IReadOnlyList<(string StableKey, string Kind)>> known,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>> fields)
        => ResolveExpressionFieldStableKeys(expression, known, (stableKey, fieldName) =>
            UniqueFieldCandidates(fields, stableKey, fieldName));

    private static IReadOnlyList<T> UniqueFieldCandidates<T>(
        IReadOnlyDictionary<string, Dictionary<string, List<T>>> fields,
        string stableKey,
        string fieldName) =>
        fields.TryGetValue(stableKey, out var lookup)
        && lookup.TryGetValue(fieldName, out var candidates)
        && candidates.Count == 1
            ? candidates
            : [];

    private static IReadOnlyList<T> UniqueFieldCandidates<T>(
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyList<T>>> fields,
        string stableKey,
        string fieldName) =>
        fields.TryGetValue(stableKey, out var lookup)
        && lookup.TryGetValue(fieldName, out var candidates)
        && candidates.Count == 1
            ? candidates
            : [];

    private static IReadOnlyList<string> ResolveExpressionFieldStableKeys(
        string expression,
        IReadOnlyDictionary<string, IReadOnlyList<(string StableKey, string Kind)>> known,
        Func<string, string, IReadOnlyList<string>> resolveFields)
    {
        var result = new List<string>();
        foreach (var reference in ExpressionFieldReferences(expression))
            result.AddRange(ResolveFieldReferenceStableKeys(reference, known, resolveFields));
        return result.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    private static IReadOnlyList<string> ResolveFieldReferenceStableKeys(
        Match reference,
        IReadOnlyDictionary<string, IReadOnlyList<(string StableKey, string Kind)>> known,
        Func<string, string, IReadOnlyList<string>> resolveFields)
    {
        var objectName = reference.Groups["table"].Success ? reference.Groups["table"].Value : null;
        var fieldName = reference.Groups["field"].Value;
        IEnumerable<(string StableKey, string Kind)> objects;
        if (objectName is null)
            objects = known.Values.SelectMany(value => value);
        else if (known.TryGetValue(objectName, out var found))
            objects = found;
        else
            objects = [];
        var matches = objects
            .Where(value => value.Kind is "table" or "query")
            .Select(value => (value.StableKey, Fields: resolveFields(value.StableKey, fieldName)))
            .Where(value => value.Fields.Count == 1)
            .ToArray();
        if (objectName is null
            && matches.Select(value => value.StableKey).Distinct(StringComparer.Ordinal).Count() != 1)
            return [];
        return matches.SelectMany(value => value.Fields).ToArray();
    }

    private static bool ResolvesExpressionFieldKeys(
        string expression,
        IReadOnlyDictionary<string, IReadOnlyList<(string StableKey, string Kind)>> known,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>> fields)
    {
        var matches = ExpressionFieldReferences(expression);
        return matches.All(match => ResolveFieldReferenceStableKeys(match, known, (stableKey, fieldName) =>
            UniqueFieldCandidates(fields, stableKey, fieldName)).Count == 1);
    }

    private static bool IsStaticDirectProjection(string expression) =>
        DirectSelectFieldPattern().IsMatch(RemoveOutputAlias(expression));

    private static bool ResolvesExpressionCompletely(
        string expression,
        IReadOnlyDictionary<string, IReadOnlyList<(string StableKey, string Kind)>> known,
        IReadOnlyDictionary<string, Dictionary<string, List<AccessFieldProjection>>> fields)
    {
        var references = ExpressionFieldReferences(expression);
        return references.All(reference => ResolveFieldReferenceStableKeys(reference, known, (stableKey, fieldName) =>
            UniqueFieldCandidates(fields, stableKey, fieldName)
                .Select(candidate => candidate.Identity.StableKey)
                .ToArray()).Count == 1);
    }

    private static Match[] ExpressionFieldReferences(string expression) =>
        FieldReferencePattern().Matches(expression)
            .Where(match =>
            {
                var end = match.Index + match.Length;
                while (end < expression.Length && char.IsWhiteSpace(expression[end])) end++;
                return end >= expression.Length || expression[end] != '(';
            })
            .ToArray();

    private static bool HasUnsupportedNamedFunction(string expression) =>
        NamedFunctionPattern().Matches(expression)
            .Select(match => match.Groups["name"].Value)
            .Any(name => !SqlFunctionNames.Contains(name, StringComparer.OrdinalIgnoreCase)
                && !SqlKeywordCallNames.Contains(name, StringComparer.OrdinalIgnoreCase));

    private static string? SetClause(string masked) => Clause(masked, "set", ["where", "order", ";", "$"]);
    private static string? PredicateClause(string masked) => Clause(masked, "where", ["group", "order", ";", "$"]);
    private static string? Clause(string masked, string keyword, string[] boundaries)
    {
        var start = Regex.Match(masked, $@"(?is)\b{keyword}\b");
        if (!start.Success) return null;
        var tail = masked[(start.Index + start.Length)..];
        var end = Regex.Match(tail, $@"(?is)\s+(?:{string.Join('|', boundaries.Where(x => x != "$"))})\b|;");
        return tail[..(end.Success ? end.Index : tail.Length)].Trim();
    }

    private static IReadOnlyList<string> ParsePivotColumns(string sql)
        => (TryParsePivotColumnNames(sql, out var values) ? values : [])
            .Select(value => AccessSafeValues.RoleHash("access-query-pivot-column", value))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

    private static IReadOnlyList<string> ParsePivotColumnNames(string sql)
        => TryParsePivotColumnNames(sql, out var values) ? values : [];

    private static bool TryParsePivotColumnNames(string sql, out IReadOnlyList<string> values)
    {
        values = [];
        var masked = MaskLiteralsAndComments(sql);
        if (!TryLocatePivotClause(masked, out _, out _, out var inIndex) || inIndex is null) return false;
        var open = inIndex.Value + "in".Length;
        while (open < masked.Length && char.IsWhiteSpace(masked[open])) open++;
        if (open >= masked.Length || masked[open] != '(') return false;

        var depth = 0;
        var bracket = false;
        var close = -1;
        for (var index = open; index < masked.Length; index++)
        {
            var current = masked[index];
            if (current == '[') bracket = true;
            else if (current == ']') bracket = false;
            else if (!bracket && current == '(') depth++;
            else if (!bracket && current == ')' && --depth == 0)
            {
                close = index;
                break;
            }
        }
        if (close < 0 || masked[(close + 1)..].Trim().TrimEnd(';').Trim().Length > 0) return false;

        var items = SplitSelectItems(sql[(open + 1)..close]);
        var parsed = items
            .Select(value => UnquotePivotLiteral(value.Trim()))
            .ToArray();
        if (items.Count == 0 || parsed.Any(value => value is null)) return false;
        values = parsed.Select(value => value!).ToArray();
        return values.Count > 0;
    }

    private static string? PivotExpression(string masked)
    {
        if (!TryLocatePivotClause(masked, out var start, out var end, out _)) return null;
        var expression = masked[start..end].Trim();
        return expression.Length == 0 ? null : expression;
    }

    private static bool TryLocatePivotClause(
        string masked,
        out int expressionStart,
        out int expressionEnd,
        out int? inIndex)
    {
        expressionStart = 0;
        expressionEnd = 0;
        inIndex = null;
        var pivotIndexes = TopLevelKeywordIndexes(masked, "pivot", 0);
        var pivotIndex = pivotIndexes.Count == 1 ? pivotIndexes[0] : -1;
        if (pivotIndex < 0 && !HasBalancedSqlDelimiters(masked))
        {
            var fallbackIndexes = Regex.Matches(masked, @"(?i)\bpivot\b", RegexOptions.CultureInvariant)
                .Cast<Match>()
                .Select(match => match.Index)
                .ToArray();
            if (fallbackIndexes.Length == 1) pivotIndex = fallbackIndexes[0];
        }
        if (pivotIndex < 0) return false;

        expressionStart = pivotIndex + "pivot".Length;
        var semicolon = masked.IndexOf(';', expressionStart);
        var clauseEnd = semicolon < 0 ? masked.Length : semicolon;
        inIndex = TopLevelKeywordIndexes(masked, "in", expressionStart)
            .Cast<int?>()
            .FirstOrDefault(index => index < clauseEnd);
        expressionEnd = inIndex ?? clauseEnd;
        return expressionStart < expressionEnd;
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

        var bracketMask = MaskBracketedIdentifiers(masked);
        var unsupported = !bracketMask.Complete || UnsupportedPattern().IsMatch(bracketMask.Sql);
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

    private static (string Sql, bool Complete) MaskBracketedIdentifiers(string sql)
    {
        var builder = new StringBuilder(sql.Length);
        var bracketed = false;
        for (var index = 0; index < sql.Length; index++)
        {
            var current = sql[index];
            if (!bracketed)
            {
                if (current == '[')
                {
                    bracketed = true;
                    builder.Append(' ');
                }
                else builder.Append(current);
                continue;
            }

            builder.Append(char.IsWhiteSpace(current) ? current : ' ');
            if (current != ']') continue;
            if (index + 1 < sql.Length && sql[index + 1] == ']')
            {
                builder.Append(' ');
                index++;
                continue;
            }

            bracketed = false;
        }

        return (builder.ToString(), !bracketed);
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

    [GeneratedRegex(@"(?ix)\s+as\s+(?:\[(?<bracketed>[^\]]+)\]|(?<plain>[A-Za-z_][A-Za-z0-9_ ]*))\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex OutputAliasPattern();

    [GeneratedRegex(@"(?ix)^\s*(?:\[(?<bracketed>[^\]]+)\]|(?<plain>[A-Za-z_][A-Za-z0-9_ ]*))\s*:\s*", RegexOptions.CultureInvariant)]
    private static partial Regex AccessOutputAliasPattern();

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

    [GeneratedRegex(@"(?ix)(?:(?:\[(?<table>[^\]]+)\]|(?<table>[A-Za-z_][A-Za-z0-9_]*))\s*\.\s*)?(?:\[(?<field>[^\]]+)\]|(?<field>[A-Za-z_][A-Za-z0-9_]*))", RegexOptions.CultureInvariant)]
    private static partial Regex FieldReferencePattern();

    [GeneratedRegex(@"(?ix)\b(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*\(", RegexOptions.CultureInvariant)]
    private static partial Regex NamedFunctionPattern();
}
