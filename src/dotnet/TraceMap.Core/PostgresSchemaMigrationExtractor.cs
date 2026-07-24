using System.Text.RegularExpressions;

namespace TraceMap.Core;

public static partial class PostgresSchemaMigrationExtractor
{
    private const string Limitation = "Bounded static PostgreSQL DDL evidence only; dialect validity, execution order, applied migrations, live schema, data, permissions, compatibility, rollback, and production state are not proven.";

    public static IReadOnlyList<CodeFact> Extract(string repoPath, ScanManifest manifest, IEnumerable<FileInventoryItem> inventory)
    {
        var facts = new List<CodeFact>();
        foreach (var file in inventory.Where(item => item.Kind == "Sql").OrderBy(item => item.RelativePath, StringComparer.Ordinal))
        {
            string text;
            try { text = File.ReadAllText(Path.Combine(repoPath, file.RelativePath)); }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                facts.Add(Gap(manifest, file.RelativePath, 1, 1, 0, "SqlFileUnavailable"));
                continue;
            }
            if (!MightContainSupportedFamily(text)) continue;

            var fileFacts = new List<CodeFact>();
            var recognizedStatementHashes = new List<string>();
            foreach (var statement in SqlExecutionContextExtractor.SplitStatements(text))
            {
                var structural = statement.StructuralText;
                if (!StartsSupportedFamily(structural)) continue;
                var statementHash = FactFactory.Hash(structural, 32);
                recognizedStatementHashes.Add(statementHash);
                if (!statement.LexicallyComplete || !HasBalancedParentheses(structural))
                {
                    fileFacts.Add(Gap(manifest, file.RelativePath, statement.StartLine, statement.EndLine, statement.Ordinal, "IncompleteDdlStatement", statementHash));
                    continue;
                }

                if (TryCreateTable(structural, out var schema, out var table, out var columns, out var constraints, out var hasUnsupportedClauses))
                {
                    fileFacts.Add(Surface(manifest, FactTypes.PostgresMigrationOperation, file.RelativePath, statement, schema, table, null, "create-table", "migration-operation"));
                    fileFacts.Add(Surface(manifest, FactTypes.PostgresSchemaTableDeclared, file.RelativePath, statement, schema, table, null, "create-table", "table"));
                    foreach (var declaredColumn in columns)
                        fileFacts.Add(Surface(manifest, FactTypes.PostgresSchemaColumnDeclared, file.RelativePath, statement, schema, table, declaredColumn, "create-table", "column"));
                    foreach (var tableConstraint in constraints)
                        fileFacts.Add(ConstraintSurface(manifest, FactTypes.PostgresSchemaConstraintDeclared, file.RelativePath, statement, schema, table, tableConstraint, "create-table-constraint", "constraint"));
                    if (hasUnsupportedClauses)
                        fileFacts.Add(Gap(manifest, file.RelativePath, statement.StartLine, statement.EndLine, statement.Ordinal, "CreateTableClauseUnsupported", statementHash));
                    else if (columns.Count == 0)
                        fileFacts.Add(Gap(manifest, file.RelativePath, statement.StartLine, statement.EndLine, statement.Ordinal, "CreateTableColumnsUnavailable", statementHash));
                    continue;
                }

                if (AlterTablePrefix().IsMatch(structural) && HasTopLevelComma(structural))
                {
                    fileFacts.Add(Gap(manifest, file.RelativePath, statement.StartLine, statement.EndLine, statement.Ordinal, "AlterTableMultipleSubcommandsUnsupported", statementHash));
                    continue;
                }

                if (TryAlterAddColumn(structural, out schema, out table, out var column))
                {
                    fileFacts.Add(Surface(manifest, FactTypes.PostgresMigrationOperation, file.RelativePath, statement, schema, table, column, "add-column", "migration-operation"));
                    fileFacts.Add(Surface(manifest, FactTypes.PostgresSchemaColumnDeclared, file.RelativePath, statement, schema, table, column, "add-column", "column"));
                    continue;
                }

                if (TryAlterAddConstraint(structural, out schema, out table, out var alterConstraint))
                {
                    fileFacts.Add(ConstraintSurface(manifest, FactTypes.PostgresMigrationOperation, file.RelativePath, statement, schema, table, alterConstraint, "add-constraint", "migration-operation"));
                    fileFacts.Add(ConstraintSurface(manifest, FactTypes.PostgresSchemaConstraintDeclared, file.RelativePath, statement, schema, table, alterConstraint, "add-constraint", "constraint"));
                    continue;
                }

                if (TryCreateIndex(structural, out schema, out table, out var index))
                {
                    fileFacts.Add(IndexSurface(manifest, FactTypes.PostgresMigrationOperation, file.RelativePath, statement, schema, table, index, "migration-operation"));
                    fileFacts.Add(IndexSurface(manifest, FactTypes.PostgresSchemaIndexDeclared, file.RelativePath, statement, schema, table, index, "index"));
                    continue;
                }

                fileFacts.Add(Gap(manifest, file.RelativePath, statement.StartLine, statement.EndLine, statement.Ordinal, "UnsupportedSchemaDdlShape", statementHash));
            }

            if (fileFacts.Count == 0) continue;
            facts.Add(FactFactory.Create(manifest, FactTypes.PostgresMigrationFileDeclared, RuleIds.DatabasePostgresSchemaMigration,
                EvidenceTiers.Tier2Structural,
                new EvidenceSpan(file.RelativePath, 1, CountLines(text), FactFactory.Hash(string.Join(";", recognizedStatementHashes), 32), nameof(PostgresSchemaMigrationExtractor), ScannerVersions.PostgresSchemaMigrationExtractor),
                targetSymbol: Path.GetFileName(file.RelativePath),
                properties: Properties(("objectKind", "migration-file"), ("coverageLabel", "bounded-static-evidence"), ("limitations", Limitation))));
            facts.AddRange(fileFacts);
        }
        return facts;
    }

    private static bool StartsSupportedFamily(string sql) =>
        CreateTablePrefix().IsMatch(sql) || AlterTablePrefix().IsMatch(sql) || CreateIndexPrefix().IsMatch(sql);

    private static bool MightContainSupportedFamily(string sql) =>
        (sql.Contains("TABLE", StringComparison.OrdinalIgnoreCase)
            && (sql.Contains("CREATE", StringComparison.OrdinalIgnoreCase) || sql.Contains("ALTER", StringComparison.OrdinalIgnoreCase)))
        || (sql.Contains("CREATE", StringComparison.OrdinalIgnoreCase)
            && sql.Contains("INDEX", StringComparison.OrdinalIgnoreCase));

    private static bool TryCreateTable(
        string sql,
        out string schema,
        out string table,
        out IReadOnlyList<string> columns,
        out IReadOnlyList<ConstraintProjection> constraints,
        out bool hasUnsupportedClauses)
    {
        schema = table = string.Empty;
        columns = [];
        constraints = [];
        hasUnsupportedClauses = false;
        var match = CreateTable().Match(sql);
        if (!match.Success) return false;
        schema = match.Groups["schema"].Success ? match.Groups["schema"].Value : string.Empty;
        table = match.Groups["table"].Value;
        var declaredColumns = new List<string>();
        var declaredConstraints = new List<ConstraintProjection>();
        var seenColumns = new HashSet<string>(StringComparer.Ordinal);
        var seenConstraints = new HashSet<string>(StringComparer.Ordinal);
        foreach (var part in SplitTopLevel(match.Groups["body"].Value))
        {
            var trimmed = part.Trim();
            var candidate = trimmed.Split((char[]?)null, 2, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
            if (SafeIdentifier().IsMatch(candidate) && !ConstraintPrefixes.Contains(candidate, StringComparer.OrdinalIgnoreCase))
            {
                if (seenColumns.Add(candidate)) declaredColumns.Add(candidate);
                if (InlineConstraintSignal().IsMatch(trimmed[candidate.Length..]))
                    hasUnsupportedClauses = true;
            }
            else if (TryNamedConstraint(trimmed, out var constraint))
            {
                if (seenConstraints.Add(constraint.Name))
                    declaredConstraints.Add(constraint);
                else
                    hasUnsupportedClauses = true;
            }
            else
            {
                hasUnsupportedClauses = true;
            }
        }
        columns = declaredColumns;
        constraints = declaredConstraints;
        return true;
    }

    private static bool TryAlterAddColumn(string sql, out string schema, out string table, out string column)
    {
        schema = table = column = string.Empty;
        var match = AlterAddColumn().Match(sql);
        if (!match.Success) return false;
        schema = match.Groups["schema"].Success ? match.Groups["schema"].Value : string.Empty;
        table = match.Groups["table"].Value;
        column = match.Groups["column"].Value;
        return true;
    }

    private static bool TryAlterAddConstraint(string sql, out string schema, out string table, out ConstraintProjection constraint)
    {
        schema = table = string.Empty;
        constraint = ConstraintProjection.Empty;
        var match = AlterAddConstraint().Match(sql);
        if (!match.Success || !TryNamedConstraint(match.Groups["constraint"].Value, out constraint)) return false;
        schema = match.Groups["schema"].Success ? match.Groups["schema"].Value : string.Empty;
        table = match.Groups["table"].Value;
        return true;
    }

    private static bool TryCreateIndex(string sql, out string schema, out string table, out IndexProjection index)
    {
        schema = table = string.Empty;
        index = IndexProjection.Empty;
        var match = CreateIndex().Match(sql);
        if (!match.Success || !TryIdentifierList(match.Groups["columns"].Value, allowOrdering: true, out var columns)) return false;
        schema = match.Groups["schema"].Success ? match.Groups["schema"].Value : string.Empty;
        table = match.Groups["table"].Value;
        index = new IndexProjection(
            match.Groups["index"].Value,
            match.Groups["unique"].Success,
            match.Groups["method"].Success ? match.Groups["method"].Value : "btree",
            columns);
        return true;
    }

    private static bool TryNamedConstraint(string value, out ConstraintProjection constraint)
    {
        constraint = ConstraintProjection.Empty;
        var match = NamedConstraint().Match(value);
        if (!match.Success || !TryIdentifierList(match.Groups["columns"].Value, allowOrdering: false, out var columns)) return false;

        if (match.Groups["primary"].Success)
        {
            constraint = new ConstraintProjection(match.Groups["name"].Value, "primary-key", columns, string.Empty, string.Empty, []);
            return true;
        }
        if (match.Groups["unique"].Success)
        {
            constraint = new ConstraintProjection(match.Groups["name"].Value, "unique", columns, string.Empty, string.Empty, []);
            return true;
        }
        if (!match.Groups["foreign"].Success
            || !TryIdentifierList(match.Groups["referencedColumns"].Value, allowOrdering: false, out var referencedColumns))
            return false;

        constraint = new ConstraintProjection(
            match.Groups["name"].Value,
            "foreign-key",
            columns,
            match.Groups["referencedSchema"].Success ? match.Groups["referencedSchema"].Value : string.Empty,
            match.Groups["referencedTable"].Value,
            referencedColumns);
        return true;
    }

    private static bool TryIdentifierList(string value, bool allowOrdering, out IReadOnlyList<string> identifiers)
    {
        var result = new List<string>();
        foreach (var part in SplitTopLevel(value))
        {
            var match = (allowOrdering ? OrderedIdentifier() : PlainIdentifier()).Match(part.Trim());
            if (!match.Success)
            {
                identifiers = [];
                return false;
            }
            result.Add(match.Groups["identifier"].Value);
        }
        identifiers = result;
        return result.Count > 0;
    }

    private static readonly string[] ConstraintPrefixes = ["CONSTRAINT", "PRIMARY", "FOREIGN", "UNIQUE", "CHECK", "EXCLUDE", "LIKE"];

    private static bool HasBalancedParentheses(string value)
    {
        var depth = 0;
        foreach (var character in value)
        {
            if (character == '(') depth++;
            else if (character == ')' && --depth < 0) return false;
        }
        return depth == 0;
    }

    private static bool HasTopLevelComma(string value)
    {
        var depth = 0;
        foreach (var character in value)
        {
            if (character == '(') depth++;
            else if (character == ')' && depth > 0) depth--;
            else if (character == ',' && depth == 0) return true;
        }
        return false;
    }

    private static IReadOnlyList<string> SplitTopLevel(string value)
    {
        var parts = new List<string>();
        var depth = 0;
        var start = 0;
        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] == '(') depth++;
            else if (value[index] == ')' && depth > 0) depth--;
            else if (value[index] == ',' && depth == 0) { parts.Add(value[start..index]); start = index + 1; }
        }
        if (start < value.Length) parts.Add(value[start..]);
        return parts;
    }

    private static CodeFact Surface(ScanManifest manifest, string factType, string path, SqlExecutionContextExtractor.SqlStatement statement,
        string schema, string table, string? column, string operation, string objectKind)
    {
        var target = string.IsNullOrEmpty(schema) ? table : $"{schema}.{table}";
        if (!string.IsNullOrEmpty(column)) target += $".{column}";
        var properties = Properties(
            ("objectKind", objectKind), ("operationKind", operation), ("tableName", table),
            ("statementOrdinal", statement.Ordinal.ToString()), ("coverageLabel", "bounded-static-evidence"), ("limitations", Limitation));
        if (!string.IsNullOrEmpty(schema)) properties["schemaName"] = schema;
        if (!string.IsNullOrEmpty(column)) properties["columnName"] = column;
        return FactFactory.Create(manifest, factType, RuleIds.DatabasePostgresSchemaMigration, EvidenceTiers.Tier2Structural,
            new EvidenceSpan(path, statement.StartLine, statement.EndLine, FactFactory.Hash(statement.StructuralText, 32), nameof(PostgresSchemaMigrationExtractor), ScannerVersions.PostgresSchemaMigrationExtractor),
            targetSymbol: target, contractElement: target, properties: properties);
    }

    private static CodeFact ConstraintSurface(
        ScanManifest manifest,
        string factType,
        string path,
        SqlExecutionContextExtractor.SqlStatement statement,
        string schema,
        string table,
        ConstraintProjection constraint,
        string operation,
        string objectKind)
    {
        var target = string.IsNullOrEmpty(schema) ? table : $"{schema}.{table}";
        target += $".{constraint.Name}";
        var properties = Properties(
            ("objectKind", objectKind),
            ("operationKind", operation),
            ("tableName", table),
            ("constraintName", constraint.Name),
            ("constraintKind", constraint.Kind),
            ("columnNames", string.Join(',', constraint.Columns)),
            ("statementOrdinal", statement.Ordinal.ToString()),
            ("coverageLabel", "bounded-static-evidence"),
            ("limitations", Limitation));
        if (!string.IsNullOrEmpty(schema)) properties["schemaName"] = schema;
        if (!string.IsNullOrEmpty(constraint.ReferencedTable))
        {
            properties["referencedTableName"] = constraint.ReferencedTable;
            properties["referencedColumnNames"] = string.Join(',', constraint.ReferencedColumns);
        }
        if (!string.IsNullOrEmpty(constraint.ReferencedSchema))
            properties["referencedSchemaName"] = constraint.ReferencedSchema;
        return FactFactory.Create(manifest, factType, RuleIds.DatabasePostgresSchemaMigration, EvidenceTiers.Tier2Structural,
            new EvidenceSpan(path, statement.StartLine, statement.EndLine, FactFactory.Hash(statement.StructuralText, 32), nameof(PostgresSchemaMigrationExtractor), ScannerVersions.PostgresSchemaMigrationExtractor),
            targetSymbol: target, contractElement: target, properties: properties);
    }

    private static CodeFact IndexSurface(
        ScanManifest manifest,
        string factType,
        string path,
        SqlExecutionContextExtractor.SqlStatement statement,
        string schema,
        string table,
        IndexProjection index,
        string objectKind)
    {
        var target = string.IsNullOrEmpty(schema) ? table : $"{schema}.{table}";
        target += $".{index.Name}";
        var properties = Properties(
            ("objectKind", objectKind),
            ("operationKind", "create-index"),
            ("tableName", table),
            ("indexName", index.Name),
            ("indexKind", index.Unique ? "unique" : "non-unique"),
            ("accessMethod", index.AccessMethod),
            ("columnNames", string.Join(',', index.Columns)),
            ("statementOrdinal", statement.Ordinal.ToString()),
            ("coverageLabel", "bounded-static-evidence"),
            ("limitations", Limitation));
        if (!string.IsNullOrEmpty(schema)) properties["schemaName"] = schema;
        return FactFactory.Create(manifest, factType, RuleIds.DatabasePostgresSchemaMigration, EvidenceTiers.Tier2Structural,
            new EvidenceSpan(path, statement.StartLine, statement.EndLine, FactFactory.Hash(statement.StructuralText, 32), nameof(PostgresSchemaMigrationExtractor), ScannerVersions.PostgresSchemaMigrationExtractor),
            targetSymbol: target, contractElement: target, properties: properties);
    }

    private static CodeFact Gap(ScanManifest manifest, string path, int start, int end, int ordinal, string classification, string? snippetHash = null) =>
        FactFactory.Create(manifest, FactTypes.AnalysisGap, RuleIds.DatabasePostgresSchemaMigrationGap, EvidenceTiers.Tier4Unknown,
            new EvidenceSpan(path, start, end, snippetHash, nameof(PostgresSchemaMigrationExtractor), ScannerVersions.PostgresSchemaMigrationExtractor),
            properties: Properties(("classification", classification), ("statementOrdinal", ordinal.ToString()), ("coverageLabel", "reduced-static-evidence"), ("limitations", Limitation)));

    private static SortedDictionary<string, string> Properties(params (string Key, string Value)[] values) =>
        new(values.ToDictionary(value => value.Key, value => value.Value, StringComparer.Ordinal), StringComparer.Ordinal);

    private static int CountLines(string text) => text.Length == 0 ? 1 : 1 + text.Count(character => character == '\n');

    private sealed record ConstraintProjection(
        string Name,
        string Kind,
        IReadOnlyList<string> Columns,
        string ReferencedSchema,
        string ReferencedTable,
        IReadOnlyList<string> ReferencedColumns)
    {
        public static ConstraintProjection Empty { get; } = new(string.Empty, string.Empty, [], string.Empty, string.Empty, []);
    }

    private sealed record IndexProjection(string Name, bool Unique, string AccessMethod, IReadOnlyList<string> Columns)
    {
        public static IndexProjection Empty { get; } = new(string.Empty, false, string.Empty, []);
    }

    [GeneratedRegex(@"^CREATE\s+(?:UNLOGGED\s+|TEMP(?:ORARY)?\s+)?TABLE\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)] private static partial Regex CreateTablePrefix();
    [GeneratedRegex(@"^ALTER\s+TABLE\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)] private static partial Regex AlterTablePrefix();
    [GeneratedRegex(@"^CREATE\s+(?:UNIQUE\s+)?INDEX\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)] private static partial Regex CreateIndexPrefix();
    [GeneratedRegex(@"^CREATE\s+(?:UNLOGGED\s+|TEMP(?:ORARY)?\s+)?TABLE\s+(?:IF\s+NOT\s+EXISTS\s+)?(?:(?<schema>[A-Za-z_][A-Za-z0-9_$]*)\.)?(?<table>[A-Za-z_][A-Za-z0-9_$]*)\s*\((?<body>.*)\)\s*;?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline)] private static partial Regex CreateTable();
    [GeneratedRegex(@"^ALTER\s+TABLE\s+(?:IF\s+EXISTS\s+)?(?:(?<schema>[A-Za-z_][A-Za-z0-9_$]*)\.)?(?<table>[A-Za-z_][A-Za-z0-9_$]*)\s+ADD\s+(?:COLUMN\s+)?(?:IF\s+NOT\s+EXISTS\s+)?(?!(?:CONSTRAINT|PRIMARY|FOREIGN|UNIQUE|CHECK|EXCLUDE)\b)(?<column>[A-Za-z_][A-Za-z0-9_$]*)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)] private static partial Regex AlterAddColumn();
    [GeneratedRegex(@"^ALTER\s+TABLE\s+(?:IF\s+EXISTS\s+)?(?:(?<schema>[A-Za-z_][A-Za-z0-9_$]*)\.)?(?<table>[A-Za-z_][A-Za-z0-9_$]*)\s+ADD\s+(?<constraint>CONSTRAINT\s+.*)\s*;?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline)] private static partial Regex AlterAddConstraint();
    [GeneratedRegex(@"^CONSTRAINT\s+(?<name>[A-Za-z_][A-Za-z0-9_$]*)\s+(?:(?<primary>PRIMARY\s+KEY)\s*\((?<columns>[^()]*)\)|(?<unique>UNIQUE)\s*\((?<columns>[^()]*)\)|(?<foreign>FOREIGN\s+KEY)\s*\((?<columns>[^()]*)\)\s+REFERENCES\s+(?:(?<referencedSchema>[A-Za-z_][A-Za-z0-9_$]*)\.)?(?<referencedTable>[A-Za-z_][A-Za-z0-9_$]*)\s*\((?<referencedColumns>[^()]*)\))\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline)] private static partial Regex NamedConstraint();
    [GeneratedRegex(@"^CREATE\s+(?<unique>UNIQUE\s+)?INDEX\s+(?:CONCURRENTLY\s+)?(?:IF\s+NOT\s+EXISTS\s+)?(?<index>[A-Za-z_][A-Za-z0-9_$]*)\s+ON\s+(?:ONLY\s+)?(?:(?<schema>[A-Za-z_][A-Za-z0-9_$]*)\.)?(?<table>[A-Za-z_][A-Za-z0-9_$]*)(?:\s+USING\s+(?<method>[A-Za-z_][A-Za-z0-9_$]*))?\s*\((?<columns>[^()]*)\)\s*;?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline)] private static partial Regex CreateIndex();
    [GeneratedRegex(@"\b(?:PRIMARY\s+KEY|UNIQUE|REFERENCES|CHECK|CONSTRAINT)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)] private static partial Regex InlineConstraintSignal();
    [GeneratedRegex(@"^(?<identifier>[A-Za-z_][A-Za-z0-9_$]*)$", RegexOptions.CultureInvariant)] private static partial Regex PlainIdentifier();
    [GeneratedRegex(@"^(?<identifier>[A-Za-z_][A-Za-z0-9_$]*)(?:\s+(?:ASC|DESC))?(?:\s+NULLS\s+(?:FIRST|LAST))?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)] private static partial Regex OrderedIdentifier();
    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_$]*$", RegexOptions.CultureInvariant)] private static partial Regex SafeIdentifier();
}
