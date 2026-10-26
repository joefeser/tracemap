using System.Text;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace TraceMap.Core;

public sealed record SqlServerTableIdentity(string Server, string Database, string Schema, string Table);
/// <param name="DeclaredType">Visible declared type spelling; never compared.</param>
public sealed record SqlServerDdlTableColumn(string Name, int Ordinal, int Line, string? DeclaredType);
/// <param name="Completeness">complete or unknown. An unknown definition retains no columns and never establishes a shape.</param>
public sealed record SqlServerDdlTable(SqlServerTableIdentity Identity, string FilePath, int StartLine, int EndLine, string Completeness,
    string? UnknownReason, IReadOnlyList<SqlServerDdlTableColumn> Columns);
public sealed record SqlServerDdlTableUnsupported(SqlServerTableIdentity Identity, string Kind, string RuleId, string Tier,
    string FilePath, int StartLine, int EndLine);
public sealed record SqlServerDdlProjectionColumn(int Ordinal, string OutputName, string SourceColumn, int Line, int SourceColumnLine);
/// <summary>Which admitted table column backs each output of one complete shape.</summary>
public sealed record SqlServerDdlTableProjection(SqlServerObjectIdentity Procedure, SqlServerTableIdentity Table, IReadOnlyList<SqlServerDdlProjectionColumn> Columns);
public sealed record SqlServerDdlTableResultExport(string Version, string GeneratorSha256, string BoundedInputSha256,
    string ParserVersion, string ParserSha256, string ExtractorVersion, string RuleId, string Tier, SqlServerDdlInput Input,
    IReadOnlyList<SqlServerDdlTable> Tables, SqlServerResultDeclarations Declarations, IReadOnlyList<SqlServerDdlTableProjection> Projections,
    IReadOnlyList<SqlServerDdlTableUnsupported> TableUnsupported, IReadOnlyList<SqlServerDdlResultUnsupported> Unsupported);
public sealed record SqlServerDdlTableResultComparisonReport(string Version, string GeneratorSha256, string BoundedInputSha256,
    SqlServerDdlTableResultExport DdlEvidence, SqlServerResultComparisonReport Comparison);

/// <summary>
/// Opt-in table-backed first-result projection. One hashed manifest admits bounded CREATE TABLE definitions and
/// procedures; a procedure is a complete shape only when its whole body is one unconditional SELECT of direct column
/// references (optional identifier output aliases) from exactly one schema-qualified admitted table, optionally through
/// one exactly bound table alias. Everything else is an explicit unknown. Nothing is executed, ordered or deployed.
/// </summary>
public static class SqlServerDdlTableResultEvidence
{
    public const string InputVersion = "sqlserver-ddl-table-result-input/1";
    public const string ExportVersion = "sqlserver-ddl-table-result-schema/1";
    public const string DeclarationsVersion = "sqlserver-ddl-table-result-declarations/1";
    public const string Origin = "public-synthetic-tsql-ddl-table-result";
    public const string ExtractorVersion = "sqlserver-ddl-table-result-shapes/1";
    public const string RuleId = "database.sqlserver.ddl-table-result-shape.v1";
    public const int MaxTableColumns = 1024;
    private static void Require([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool ok, string reason) { if (!ok) throw new ArgumentException("ddl-table-result-" + reason); }

    /// <summary>Reads the opt-in table manifest; parameter-only and constant result manifest versions are refused.</summary>
    public static SqlServerDdlInput ReadInput(string inputJson) => SqlServerDdlEvidence.ReadInput(inputJson, InputVersion);

    public static SqlServerDdlTableResultExport Produce(string inputJson, IReadOnlyDictionary<string, string> sources)
    {
        var procedures = new List<(SqlServerObjectIdentity Identity, string Path, ProcedureStatementBody Body)>();
        var tables = new List<SqlServerDdlTable>();
        var tableGaps = new List<SqlServerDdlTableUnsupported>();
        // The complete DDL admission core runs first: malformed SQL, SQLCMD, digests, bounds, identity ambiguity across
        // tables and procedures, other top-level statements and the parameter vocabulary still reject the whole input.
        SqlServerDdlEvidence.ProduceCore(inputJson, sources, InputVersion, (identity, path, procedure) => procedures.Add((identity, path, procedure)),
            (identity, path, table) =>
            {
                var (definition, gap) = AnalyseTable(identity, path, table);
                tables.Add(definition);
                if (gap is not null) tableGaps.Add(gap);
            });
        // The admitted files are one unordered definition set: analysis starts only after every file is read.
        var shapes = new List<SqlServerResultShape>();
        var unsupported = new List<SqlServerDdlResultUnsupported>();
        var projections = new List<SqlServerDdlTableProjection>();
        foreach (var (identity, path, body) in procedures)
        {
            SqlServerDdlTableProjection? projection = null;
            var (shape, gaps) = SqlServerDdlResultEvidence.Analyse(identity, path, body, RuleId,
                (select, query, unknown) =>
                {
                    var (result, mapped) = AnalyseTableProjection(identity, path, select, query, unknown, tables);
                    projection = mapped;
                    return result;
                });
            shapes.Add(shape);
            unsupported.AddRange(gaps);
            if (projection is not null) projections.Add(projection);
        }
        var input = ReadInput(inputJson);
        var declarations = new SqlServerResultDeclarations(DeclarationsVersion, input.Repo, input.Revision, Origin, "ordinal-exact-no-aliases", shapes);
        SqlServerResultEvidence.Validate(declarations, DeclarationsVersion, Origin);
        var export = new SqlServerDdlTableResultExport(ExportVersion, SqlServerSchemaEvidence.GeneratorSha256, SqlServerSchemaEvidence.Digest(input),
            SqlServerDdlEvidence.ParserVersion, SqlServerDdlEvidence.ParserDigest.Value, ExtractorVersion, RuleId, EvidenceTiers.Tier2Structural,
            input, tables, declarations, projections, tableGaps, unsupported);
        Require(Encoding.UTF8.GetByteCount(SqlServerSchemaEvidence.Json(export)) <= SqlServerSchemaEvidence.MaxBytes, "export-size-limit");
        return export;
    }

    private static (SqlServerDdlTable, SqlServerDdlTableUnsupported?) AnalyseTable(SqlServerObjectIdentity objectIdentity, string path, CreateTableStatement table)
    {
        var identity = new SqlServerTableIdentity(objectIdentity.Server, objectIdentity.Database, objectIdentity.Schema, objectIdentity.Procedure);
        var start = table.StartLine;
        var end = SqlServerDdlEvidence.EndLine(table);
        (SqlServerDdlTable, SqlServerDdlTableUnsupported?) Unknown(string kind, TSqlFragment at)
            => (new(identity, path, start, end, "unknown", kind, []),
                new(identity, kind, RuleId, EvidenceTiers.Tier4Unknown, path, at.StartLine, SqlServerDdlEvidence.EndLine(at)));
        var definition = table.Definition;
        if (definition is null || table.AsNode || table.AsEdge || table.AsFileTable || table.OnFileGroupOrPartitionScheme is not null
            || table.TextImageOn is not null || table.FileStreamOn is not null || table.Options.Count > 0 || table.FederationScheme is not null
            || table.SelectStatement is not null || table.CtasColumns.Count > 0 || definition.Indexes.Count > 0 || definition.SystemTimePeriod is not null)
            return Unknown("table-option-unsupported", table);
        foreach (var constraint in definition.TableConstraints)
            if (!InertKey(constraint)) return Unknown("table-constraint-unsupported", constraint);
        var columns = new List<SqlServerDdlTableColumn>();
        foreach (var column in definition.ColumnDefinitions)
        {
            if (column.ComputedColumnExpression is not null) return Unknown("computed-column-unsupported", column);
            if (column.DefaultConstraint is not null || column.Constraints.Any(c => c is DefaultConstraintDefinition)) return Unknown("column-default-unsupported", column);
            if (column.Collation is not null) return Unknown("column-collation-unsupported", column);
            if (column.IsRowGuidCol || column.StorageOptions is not null || column.Index is not null || column.GeneratedAlways is not null
                || column.IsHidden || column.Encryption is not null || column.IsMasked || column.MaskingFunction is not null || column.IsPersisted)
                return Unknown("column-option-unsupported", column);
            if (column.Constraints.Any(c => c is not NullableConstraintDefinition && !InertKey(c))) return Unknown("column-constraint-unsupported", column);
            if (!SqlServerSchemaEvidence.Identifier(column.ColumnIdentifier?.Value)) return Unknown("column-name-outside-identifier-vocabulary", column);
            if (column.DataType is not SqlDataTypeReference type || type.Name is null || type.Name.Identifiers.Count != 1 || type.Parameters.Count > 2
                || type.Parameters.Any(p => p is not (IntegerLiteral or MaxLiteral))) return Unknown("column-type-unsupported", column);
            var spelling = type.Name.BaseIdentifier.Value + (type.Parameters.Count == 0 ? "" : "(" + string.Join(',', type.Parameters.Select(p => p.Value)) + ")");
            if (!SqlServerResultEvidence.TypeFacet.IsMatch(spelling)) return Unknown("column-type-unsupported", column);
            columns.Add(new(column.ColumnIdentifier!.Value, columns.Count, column.StartLine, spelling));
        }
        if (columns.Count == 0 || columns.Count > MaxTableColumns) return Unknown("column-limit", table);
        // Case-only siblings are legal only under case-sensitive collations; the collation is never inferred.
        if (columns.Select(c => c.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != columns.Count)
            return Unknown("duplicate-or-case-only-column-unsupported", table);
        return (new(identity, path, start, end, "complete", null, columns), null);
    }

    // PRIMARY KEY / UNIQUE without index options or storage placement do not change the column list.
    private static bool InertKey(ConstraintDefinition constraint) => constraint is UniqueConstraintDefinition key
        && key.IndexOptions.Count == 0 && key.OnFileGroupOrPartitionScheme is null && key.FileStreamOn is null
        && (key.IndexType is null || key.IndexType.IndexTypeKind is IndexTypeKind.Clustered or IndexTypeKind.NonClustered);

    private static ((SqlServerResultShape, IReadOnlyList<SqlServerDdlResultUnsupported>), SqlServerDdlTableProjection?) AnalyseTableProjection(
        SqlServerObjectIdentity identity, string path, SelectStatement select, QuerySpecification query,
        SqlServerDdlResultEvidence.UnknownShape unknown, IReadOnlyList<SqlServerDdlTable> tables)
    {
        ((SqlServerResultShape, IReadOnlyList<SqlServerDdlResultUnsupported>), SqlServerDdlTableProjection?) Unknown(string kind, TSqlFragment at)
            => (unknown(kind, at), null);
        if (query.WhereClause is not null || query.GroupByClause is not null || query.HavingClause is not null || query.OrderByClause is not null
            || query.TopRowFilter is not null || query.OffsetClause is not null || query.ForClause is not null || query.WindowClause is not null
            || query.UniqueRowFilter != UniqueRowFilter.NotSpecified) return Unknown("select-clause-unsupported", query);
        var from = query.FromClause;
        if (from.TableReferences.Count != 1) return Unknown("multiple-table-sources-unsupported", from);
        if (from.PredictTableReference.Count > 0) return Unknown("table-reference-unsupported", from);
        var reference = from.TableReferences[0];
        switch (reference)
        {
            case QualifiedJoin or UnqualifiedJoin or JoinParenthesisTableReference: return Unknown("join-unsupported", reference);
            case QueryDerivedTable: return Unknown("subquery-unsupported", reference);
            case VariableTableReference: return Unknown("table-variable-unsupported", reference);
            case SchemaObjectFunctionTableReference or BuiltInFunctionTableReference or GlobalFunctionTableReference:
                return Unknown("table-valued-function-unsupported", reference);
        }
        if (reference.GetType() != typeof(NamedTableReference)) return Unknown("table-reference-unsupported", reference);
        var named = (NamedTableReference)reference;
        if (named.TableHints.Count > 0) return Unknown("table-hint-unsupported", named);
        if (named.TableSampleClause is not null || named.TemporalClause is not null || named.ForPath) return Unknown("table-reference-unsupported", named);
        var name = named.SchemaObject;
        if (name.BaseIdentifier.Value.StartsWith('#')) return Unknown("temporary-table-unsupported", named);
        if (name.ServerIdentifier is not null || name.DatabaseIdentifier is not null) return Unknown("cross-database-or-server-reference-unsupported", named);
        if (name.SchemaIdentifier is null) return Unknown("table-reference-schema-required", named);
        var alias = named.Alias?.Value;
        if (!SqlServerSchemaEvidence.Identifier(name.SchemaIdentifier.Value) || !SqlServerSchemaEvidence.Identifier(name.BaseIdentifier.Value)
            || (alias is not null && !SqlServerSchemaEvidence.Identifier(alias))) return Unknown("table-reference-outside-identifier-vocabulary", named);
        // Exact ordinal identity only; a case-only match depends on the unresolved database collation.
        var table = tables.SingleOrDefault(t => t.Identity.Schema == name.SchemaIdentifier.Value && t.Identity.Table == name.BaseIdentifier.Value);
        if (table is null)
            return Unknown(tables.Any(t => string.Equals(t.Identity.Schema, name.SchemaIdentifier.Value, StringComparison.OrdinalIgnoreCase)
                && string.Equals(t.Identity.Table, name.BaseIdentifier.Value, StringComparison.OrdinalIgnoreCase))
                ? "table-reference-case-policy-unknown" : "table-dependency-missing", named);
        if (table.Completeness != "complete") return Unknown("table-definition-unsupported", named);
        var columns = new List<SqlServerResultColumn>();
        var mapped = new List<SqlServerDdlProjectionColumn>();
        foreach (var element in query.SelectElements)
        {
            if (element is not SelectScalarExpression scalar) return Unknown("select-element-unsupported", element);
            if (scalar.Expression is ScalarSubquery) return Unknown("subquery-unsupported", element);
            if (scalar.Expression is not ColumnReferenceExpression column || column.GetType() != typeof(ColumnReferenceExpression)
                || column.ColumnType != ColumnType.Regular || column.MultiPartIdentifier is null || column.Collation is not null)
                return Unknown("non-column-projection-unsupported", element);
            if (scalar.ColumnName is not null && scalar.ColumnName.Identifier is null) return Unknown("string-alias-unsupported", element);
            var outputAlias = scalar.ColumnName?.Identifier?.Value;
            if (outputAlias is not null && !SqlServerSchemaEvidence.Identifier(outputAlias)) return Unknown("alias-outside-identifier-vocabulary", element);
            var parts = column.MultiPartIdentifier.Identifiers;
            if (parts.Count > 2) return Unknown("column-qualifier-unsupported", element);
            if (parts.Count == 2)
            {
                var qualifier = parts[0].Value;
                if (alias is null) return Unknown("column-qualifier-without-alias-unsupported", element);
                if (qualifier != alias)
                    return Unknown(string.Equals(qualifier, alias, StringComparison.OrdinalIgnoreCase)
                        ? "column-qualifier-case-policy-unknown" : "column-qualifier-unbound", element);
            }
            var columnName = parts[^1].Value;
            if (!SqlServerSchemaEvidence.Identifier(columnName)) return Unknown("column-reference-outside-identifier-vocabulary", element);
            var source = table.Columns.SingleOrDefault(c => c.Name == columnName);
            if (source is null)
                return Unknown(table.Columns.Any(c => string.Equals(c.Name, columnName, StringComparison.OrdinalIgnoreCase))
                    ? "column-case-policy-unknown" : "column-dependency-missing", element);
            var output = outputAlias ?? source.Name;
            mapped.Add(new(columns.Count, output, source.Name, element.StartLine, source.Line));
            columns.Add(new(output, columns.Count, element.StartLine, source.DeclaredType));
        }
        if (columns.Count == 0 || columns.Count > SqlServerResultEvidence.MaxColumns) return Unknown("column-limit", select);
        if (columns.Select(c => c.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != columns.Count)
            return Unknown("duplicate-or-case-only-output-name-unsupported", select);
        // Table column types, nullability and values stay visible as an explicit uncompared facet.
        SqlServerDdlResultUnsupported[] facets = [new(identity, "column-type-facets-not-compared", RuleId, EvidenceTiers.Tier4Unknown,
            path, select.StartLine, SqlServerDdlEvidence.EndLine(select))];
        return ((new(identity, path, select.StartLine, SqlServerDdlEvidence.EndLine(select), "first", "complete", null, columns), facets),
            new(identity, table.Identity, mapped));
    }

    public static SqlServerDdlTableResultExport Admit(string exportJson, string inputJson, IReadOnlyDictionary<string, string> sources, string repo, string revision)
    {
        var expected = Produce(inputJson, sources);
        var actual = SqlServerSchemaEvidence.Read<SqlServerDdlTableResultExport>(exportJson);
        Require(SqlServerSchemaEvidence.Json(actual) == SqlServerSchemaEvidence.Json(expected), "integrity-origin-version-or-generator-mismatch");
        Require(actual.Input.Repo == repo && actual.Input.Revision == revision, "unexpected-input-identity");
        return actual;
    }

    public static SqlServerDdlTableResultComparisonReport Compare(string exportJson, string inputJson, IReadOnlyDictionary<string, string> sources,
        SqlServerResultBindings bindings, IReadOnlyList<CodeFact> facts, string repo, string revision)
    {
        var ddl = Admit(exportJson, inputJson, sources, repo, revision);
        var comparison = SqlServerResultEvidence.CompareAdmitted(ddl.Version, ddl.GeneratorSha256, ddl.BoundedInputSha256, ddl.ExtractorVersion,
            ddl.RuleId, ddl.Tier, ddl.Declarations, ddl, bindings, facts, repo, revision);
        comparison = comparison with
        {
            Version = "sqlserver-ddl-table-result-column-comparison/1",
            BoundedInputSha256 = SqlServerSchemaEvidence.Digest(new { ddl, comparisonInputSha256 = comparison.BoundedInputSha256 }),
            Limitations = comparison.Limitations.Concat(new[] {
                "Origin: public-synthetic-tsql-ddl-table-result. A complete shape needs one unconditional SELECT of direct column references (optional identifier output aliases, optional exactly bound table alias) from exactly one schema-qualified admitted table whose definition is fully supported.",
                "SELECT *, joins, views, CTEs, subqueries, UNION, temporary tables and table variables, functions, hints, WHERE and other clauses, expressions, dynamic SQL/EXEC, conditional or multiple statements are explicit unknown shapes, never partial columns.",
                "Missing tables or columns, unqualified, cross-database, case-only or collation-dependent references and unsupported table definitions are unknown; definitions are one unordered admitted set with no migration ordering.",
                "Table column types are visible facets only; nullability, defaults, keys, rows, values and body text are never compared or retained. No catalog or deployed correspondence is claimed."
            }).ToArray()
        };
        var bounded = SqlServerSchemaEvidence.Digest(new { ddl, comparison });
        return new("sqlserver-ddl-table-result-comparison/1", SqlServerSchemaEvidence.GeneratorSha256, bounded, ddl, comparison);
    }
}
