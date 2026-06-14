using System.Text.RegularExpressions;

namespace TraceMap.Core;

public sealed record SqlQueryShape(
    string OperationName,
    IReadOnlyList<string> TableNames,
    IReadOnlyList<string> ColumnNames,
    string QueryShapeHash)
{
    public string? PrimaryTable => TableNames.Count == 0 ? null : TableNames[0];
}

public static class SqlShapeExtractor
{
    private static readonly HashSet<string> SqlVerbs = new(StringComparer.Ordinal)
    {
        "SELECT",
        "INSERT",
        "UPDATE",
        "DELETE",
        "MERGE",
        "CREATE",
        "ALTER",
        "DROP",
        "TRUNCATE",
        "CALL",
        "EXEC",
        "EXECUTE"
    };

    private static readonly HashSet<string> SqlStopWords = new(StringComparer.Ordinal)
    {
        "AND",
        "AS",
        "ASC",
        "BETWEEN",
        "BY",
        "CASE",
        "DESC",
        "DISTINCT",
        "ELSE",
        "END",
        "FALSE",
        "FROM",
        "GROUP",
        "HAVING",
        "IN",
        "IS",
        "JOIN",
        "LEFT",
        "LIKE",
        "LIMIT",
        "NOT",
        "NULL",
        "ON",
        "OR",
        "ORDER",
        "RIGHT",
        "SELECT",
        "SET",
        "THEN",
        "TRUE",
        "VALUES",
        "WHEN",
        "WHERE"
    };

    public static bool IsSqlLike(string value)
    {
        var first = FirstToken(value);
        return SqlVerbs.Contains(first) || first == "WITH";
    }

    public static string OperationName(string value)
    {
        var first = FirstToken(value);
        return SqlVerbs.Contains(first) ? first : string.Empty;
    }

    public static SqlQueryShape QueryShape(string value)
    {
        var normalized = NormalizeSql(value);
        var operation = ShapeOperation(normalized);
        var tables = TableNames(normalized, operation);
        var columns = ColumnNames(normalized, operation);
        return new SqlQueryShape(operation, tables, columns, FactFactory.Hash(normalized, 32));
    }

    public static SortedDictionary<string, string> QueryShapeProperties(string value, string sourceKind)
    {
        var shape = QueryShape(value);
        var properties = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["textHash"] = FactFactory.Hash(value, 32),
            ["queryShapeHash"] = shape.QueryShapeHash,
            ["sqlSourceKind"] = sourceKind
        };
        if (!string.IsNullOrWhiteSpace(shape.OperationName))
        {
            properties["operationName"] = shape.OperationName;
        }

        if (shape.PrimaryTable is { Length: > 0 } primary)
        {
            properties["tableName"] = primary;
        }

        if (shape.TableNames.Count > 0)
        {
            properties["tableNames"] = string.Join(';', shape.TableNames);
        }

        if (shape.ColumnNames.Count > 0)
        {
            var columns = string.Join(';', shape.ColumnNames);
            properties["columnNames"] = columns;
            properties["fieldNames"] = columns;
        }

        return properties;
    }

    internal static string NormalizeSql(string value)
    {
        value = Regex.Replace(value, "--[^\n\r]*", " ", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        value = Regex.Replace(value, @"/\*.*?\*/", " ", RegexOptions.CultureInvariant | RegexOptions.Singleline, TimeSpan.FromMilliseconds(100));
        value = Regex.Replace(value, @"'(?:''|\\['""]|[^'])*'", "' '", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        value = Regex.Replace(value, @"""(?:""""|\\[""']|[^""])*""", "\" \"", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        value = Regex.Replace(value, @"\s+", " ", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        return value.Trim().TrimEnd(';');
    }

    private static string ShapeOperation(string value)
    {
        var first = FirstToken(value);
        return SqlVerbs.Contains(first) ? first : string.Empty;
    }

    private static string FirstToken(string value)
    {
        var trimmed = value.TrimStart();
        if (trimmed.Length == 0)
        {
            return string.Empty;
        }

        var match = Regex.Match(trimmed, @"\S+", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        return match.Success ? match.Value.ToUpperInvariant() : string.Empty;
    }

    private static IReadOnlyList<string> TableNames(string sql, string operation)
    {
        var candidates = new List<string>();
        switch (operation)
        {
            case "SELECT":
                candidates.AddRange(TopLevelMatches(sql, @"\bFROM\s+([A-Za-z_][A-Za-z0-9_.$\[\]""`]*)"));
                candidates.AddRange(TopLevelMatches(sql, @"\bJOIN\s+([A-Za-z_][A-Za-z0-9_.$\[\]""`]*)"));
                break;
            case "INSERT":
                candidates.AddRange(Matches(sql, @"\bINSERT\s+INTO\s+([A-Za-z_][A-Za-z0-9_.$\[\]""`]*)"));
                break;
            case "UPDATE":
                candidates.AddRange(Matches(sql, @"\bUPDATE\s+([A-Za-z_][A-Za-z0-9_.$\[\]""`]*)"));
                break;
            case "DELETE":
                candidates.AddRange(Matches(sql, @"\bDELETE\s+FROM\s+([A-Za-z_][A-Za-z0-9_.$\[\]""`]*)"));
                break;
            case "CREATE":
                candidates.AddRange(Matches(sql, @"\bCREATE\s+(?:TEMP(?:ORARY)?\s+)?TABLE\s+(?:IF\s+NOT\s+EXISTS\s+)?([A-Za-z_][A-Za-z0-9_.$\[\]""`]*)"));
                break;
            case "DROP":
            case "TRUNCATE":
            case "ALTER":
                candidates.AddRange(Matches(sql, $@"\b{operation}\s+(?:TABLE\s+)?([A-Za-z_][A-Za-z0-9_.$\[\]""`]*)"));
                break;
        }

        // CALL/EXEC routine names are intentionally not table candidates in v1.
        return Unique(candidates.Select(CleanIdentifier));
    }

    private static IReadOnlyList<string> ColumnNames(string sql, string operation)
    {
        return operation switch
        {
            "SELECT" => SelectColumns(Between(sql, "SELECT", "FROM")),
            "INSERT" => SplitIdentifierList(MatchGroup(sql, @"\bINSERT\s+INTO\s+[A-Za-z_][A-Za-z0-9_.$\[\]""`]*\s*\(([^)]*)\)")),
            "UPDATE" => Unique(SplitCsv(Between(sql, "SET", "WHERE")).Select(part => CleanIdentifier(part.Split('=', 2)[0]))),
            "CREATE" => CreateTableColumns(MatchGroup(sql, @"\bCREATE\s+(?:TEMP(?:ORARY)?\s+)?TABLE\s+(?:IF\s+NOT\s+EXISTS\s+)?[A-Za-z_][A-Za-z0-9_.$\[\]""`]*\s*\((.*)\)", RegexOptions.IgnoreCase | RegexOptions.Singleline)),
            _ => []
        };
    }

    private static IReadOnlyList<string> Matches(string sql, string pattern)
    {
        return Regex.Matches(sql, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100))
            .Select(match => match.Groups[1].Value)
            .ToArray();
    }

    private static IReadOnlyList<string> TopLevelMatches(string sql, string pattern)
    {
        return Regex.Matches(sql, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100))
            .Where(match => ParenthesisDepthBefore(sql, match.Index) == 0)
            .Select(match => match.Groups[1].Value)
            .ToArray();
    }

    private static int ParenthesisDepthBefore(string sql, int index)
    {
        var depth = 0;
        for (var offset = 0; offset < index && offset < sql.Length; offset++)
        {
            if (sql[offset] == '(')
            {
                depth++;
            }
            else if (sql[offset] == ')' && depth > 0)
            {
                depth--;
            }
        }

        return depth;
    }

    private static string MatchGroup(string sql, string pattern, RegexOptions options = RegexOptions.IgnoreCase)
    {
        var match = Regex.Match(sql, pattern, options | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    private static string Between(string sql, string start, string end)
    {
        var match = Regex.Match(sql, $@"\b{start}\b(.*?)(?:\b{end}\b|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline, TimeSpan.FromMilliseconds(100));
        return match.Success ? match.Groups[1].Value : string.Empty;
    }

    private static IReadOnlyList<string> SelectColumns(string text)
    {
        var columns = new List<string>();
        foreach (var part in SplitCsv(text))
        {
            var cleaned = Regex.Replace(part.Trim(), @"\bAS\b\s+[A-Za-z_][A-Za-z0-9_]*$", string.Empty, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
            var token = cleaned.Contains('.') ? cleaned[(cleaned.LastIndexOf('.') + 1)..].Trim() : cleaned.Trim();
            if (Regex.IsMatch(token, @"\s", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)))
            {
                token = Regex.Split(token, @"\s+").Last();
            }

            var name = CleanIdentifier(token);
            if (!string.IsNullOrWhiteSpace(name)
                && name != "*"
                && !SqlStopWords.Contains(name.ToUpperInvariant())
                && Regex.IsMatch(name, @"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)))
            {
                columns.Add(name);
            }
        }

        return Unique(columns);
    }

    private static IReadOnlyList<string> CreateTableColumns(string text)
    {
        var columns = new List<string>();
        foreach (var part in SplitCsv(text))
        {
            var trimmed = part.Trim();
            var first = trimmed.Length == 0 ? string.Empty : trimmed.Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
            var name = CleanIdentifier(first);
            if (!string.IsNullOrWhiteSpace(name)
                && !new[] { "CONSTRAINT", "PRIMARY", "FOREIGN", "UNIQUE", "CHECK", "KEY" }.Contains(name.ToUpperInvariant(), StringComparer.Ordinal))
            {
                columns.Add(name);
            }
        }

        return Unique(columns);
    }

    private static IReadOnlyList<string> SplitIdentifierList(string text)
    {
        return Unique(SplitCsv(text).Select(CleanIdentifier));
    }

    private static IReadOnlyList<string> SplitCsv(string text)
    {
        var parts = new List<string>();
        var depth = 0;
        var start = 0;
        for (var index = 0; index < text.Length; index++)
        {
            var current = text[index];
            if (current == '(')
            {
                depth++;
            }
            else if (current == ')' && depth > 0)
            {
                depth--;
            }
            else if (current == ',' && depth == 0)
            {
                parts.Add(text[start..index].Trim());
                start = index + 1;
            }
        }

        var tail = text[start..].Trim();
        if (tail.Length > 0)
        {
            parts.Add(tail);
        }

        return parts;
    }

    private static string CleanIdentifier(string value)
    {
        value = value.Trim().Trim(',', ';').Trim('"', '`', '[', ']');
        if (value.Contains('.', StringComparison.Ordinal))
        {
            value = value[(value.LastIndexOf('.') + 1)..].Trim('"', '`', '[', ']');
        }

        if (value.Length == 0 || !Regex.IsMatch(value, @"^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)))
        {
            return string.Empty;
        }

        return value;
    }

    private static IReadOnlyList<string> Unique(IEnumerable<string> values)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<string>();
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value) && seen.Add(value))
            {
                result.Add(value);
            }
        }

        return result;
    }
}
