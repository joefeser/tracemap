using System.Text;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace TraceMap.Core;

public sealed record SqlServerDdlResultUnsupported(SqlServerObjectIdentity Identity, string Kind, string RuleId, string Tier,
    string FilePath, int StartLine, int EndLine);
public sealed record SqlServerDdlResultExport(string Version, string GeneratorSha256, string BoundedInputSha256,
    string ParserVersion, string ParserSha256, string ExtractorVersion, string RuleId, string Tier, SqlServerDdlInput Input,
    SqlServerResultDeclarations Declarations, IReadOnlyList<SqlServerDdlResultUnsupported> Unsupported);
public sealed record SqlServerDdlResultComparisonReport(string Version, string GeneratorSha256, string BoundedInputSha256,
    SqlServerDdlResultExport DdlEvidence, SqlServerResultComparisonReport Comparison);

/// <summary>
/// Deliberately narrow DDL first-result projection: one unconditional SELECT of aliased constants or
/// CAST(constant AS builtin), no FROM, at most one BEGIN/END wrapper. Every other body is an explicit
/// unknown shape. Bodies are inspected structurally only; nothing is executed or evaluated.
/// </summary>
public static class SqlServerDdlResultEvidence
{
    public const string InputVersion = "sqlserver-ddl-result-input/1";
    public const string ExtractorVersion = "sqlserver-ddl-result-shapes/1";
    public const string RuleId = "database.sqlserver.ddl-result-shape.v1";
    private static void Require([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool ok, string reason) { if (!ok) throw new ArgumentException("ddl-result-" + reason); }

    /// <summary>Reads the opt-in result manifest; a parameter-only manifest version is refused.</summary>
    public static SqlServerDdlInput ReadInput(string inputJson) => SqlServerDdlEvidence.ReadInput(inputJson, InputVersion);

    public static SqlServerDdlResultExport Produce(string inputJson, IReadOnlyDictionary<string, string> sources)
    {
        var shapes = new List<SqlServerResultShape>();
        var unsupported = new List<SqlServerDdlResultUnsupported>();
        // Reuse the complete parameter admission core: malformed SQL, SQLCMD, digests, bounds, identity
        // ambiguity and the parameter vocabulary still reject the whole input.
        SqlServerDdlEvidence.ProduceCore(inputJson, sources, InputVersion, (identity, path, procedure) =>
        {
            var (shape, gaps) = Analyse(identity, path, procedure);
            shapes.Add(shape);
            unsupported.AddRange(gaps);
        });
        var input = ReadInput(inputJson);
        var declarations = new SqlServerResultDeclarations("sqlserver-ddl-result-declarations/1", input.Repo, input.Revision,
            "public-synthetic-tsql-ddl-result", "ordinal-exact-no-aliases", shapes);
        SqlServerResultEvidence.Validate(declarations, ddl: true);
        var export = new SqlServerDdlResultExport("sqlserver-ddl-result-schema/1", SqlServerSchemaEvidence.GeneratorSha256,
            SqlServerSchemaEvidence.Digest(input), SqlServerDdlEvidence.ParserVersion, SqlServerDdlEvidence.ParserDigest.Value,
            ExtractorVersion, RuleId, EvidenceTiers.Tier2Structural, input, declarations, unsupported);
        Require(Encoding.UTF8.GetByteCount(SqlServerSchemaEvidence.Json(export)) <= SqlServerSchemaEvidence.MaxBytes, "export-size-limit");
        return export;
    }

    private static (SqlServerResultShape, IReadOnlyList<SqlServerDdlResultUnsupported>) Analyse(SqlServerObjectIdentity identity, string path, ProcedureStatementBody procedure)
    {
        var start = procedure.StartLine;
        var end = SqlServerDdlEvidence.EndLine(procedure);
        (SqlServerResultShape, IReadOnlyList<SqlServerDdlResultUnsupported>) Unknown(string kind, TSqlFragment at)
            => (new(identity, path, start, end, "first", "unknown", kind, []),
                [new(identity, kind, RuleId, EvidenceTiers.Tier4Unknown, path, at.StartLine, SqlServerDdlEvidence.EndLine(at))]);
        IList<TSqlStatement> statements = procedure.StatementList.Statements;
        // At most one simple BEGIN/END wrapper; atomic or nested blocks are not unwrapped.
        if (statements.Count == 1 && statements[0].GetType() == typeof(BeginEndBlockStatement))
            statements = ((BeginEndBlockStatement)statements[0]).StatementList?.Statements ?? [];
        if (statements.Count != 1) return Unknown("body-statement-count-unsupported", statements.Count > 1 ? statements[1] : procedure);
        var statement = statements[0];
        if (statement.GetType() != typeof(SelectStatement)) return Unknown(statement switch
        {
            IfStatement => "conditional-body-unsupported",
            ExecuteStatement => "exec-or-dynamic-sql-unsupported",
            BeginEndBlockStatement => "nested-block-unsupported",
            WhileStatement => "loop-body-unsupported",
            TryCatchStatement => "try-catch-body-unsupported",
            ReturnStatement => "return-channel-not-result-set",
            _ => "body-statement-unsupported"
        }, statement);
        var select = (SelectStatement)statement;
        if (select.Into is not null || select.On is not null || select.ComputeClauses.Count > 0 || select.WithCtesAndXmlNamespaces is not null
            || select.OptimizerHints.Count > 0) return Unknown("select-clause-unsupported", select);
        if (select.QueryExpression is BinaryQueryExpression) return Unknown("set-operation-unsupported", select.QueryExpression);
        if (select.QueryExpression is not QuerySpecification query || query.GetType() != typeof(QuerySpecification))
            return Unknown("query-expression-unsupported", select.QueryExpression);
        // Star and assignment elements are named before table dependencies so their reason stays specific.
        if (query.SelectElements.FirstOrDefault(e => e is SelectStarExpression) is { } star) return Unknown("select-star-unsupported", star);
        if (query.SelectElements.FirstOrDefault(e => e is SelectSetVariable) is { } assignment) return Unknown("select-assignment-unsupported", assignment);
        if (query.FromClause is not null) return Unknown("from-dependency-unsupported", query.FromClause);
        if (query.WhereClause is not null || query.GroupByClause is not null || query.HavingClause is not null || query.OrderByClause is not null
            || query.TopRowFilter is not null || query.OffsetClause is not null || query.ForClause is not null || query.WindowClause is not null
            || query.UniqueRowFilter != UniqueRowFilter.NotSpecified) return Unknown("select-clause-unsupported", query);
        var columns = new List<SqlServerResultColumn>();
        foreach (var element in query.SelectElements)
        {
            if (element is SelectStarExpression) return Unknown("select-star-unsupported", element);
            if (element is SelectSetVariable) return Unknown("select-assignment-unsupported", element);
            if (element is not SelectScalarExpression scalar) return Unknown("select-element-unsupported", element);
            var alias = scalar.ColumnName?.Identifier?.Value;
            if (alias is null) return Unknown("explicit-alias-required", element);
            if (!SqlServerSchemaEvidence.Identifier(alias)) return Unknown("alias-outside-identifier-vocabulary", element);
            string? declaredType = null;
            if (scalar.Expression is CastCall cast)
            {
                if (!Constant(cast.Parameter) || cast.Collation is not null || cast.DataType is not SqlDataTypeReference type
                    || type.Name is null || type.Name.Identifiers.Count != 1 || type.Parameters.Count > 2
                    || type.Parameters.Any(p => p is not (IntegerLiteral or MaxLiteral))) return Unknown("non-constant-projection-unsupported", element);
                declaredType = type.Name.BaseIdentifier.Value + (type.Parameters.Count == 0 ? "" : "(" + string.Join(',', type.Parameters.Select(p => p.Value)) + ")");
            }
            else if (!Constant(scalar.Expression)) return Unknown("non-constant-projection-unsupported", element);
            columns.Add(new(alias, columns.Count, element.StartLine, declaredType));
        }
        if (columns.Count == 0 || columns.Count > SqlServerResultEvidence.MaxColumns) return Unknown("column-limit", select);
        if (columns.Select(c => c.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != columns.Count)
            return Unknown("duplicate-or-case-only-alias-unsupported", select);
        // Literal and CAST types, nullability and values stay visible as an explicit uncompared facet.
        SqlServerDdlResultUnsupported[] facets = [new(identity, "column-type-facets-not-compared", RuleId, EvidenceTiers.Tier4Unknown,
            path, select.StartLine, SqlServerDdlEvidence.EndLine(select))];
        return (new(identity, path, select.StartLine, SqlServerDdlEvidence.EndLine(select), "first", "complete", null, columns), facets);
    }

    // Constants only; no expression, subquery, variable or function value is evaluated or retained.
    private static bool Constant(ScalarExpression? expression)
        => expression is IntegerLiteral or NumericLiteral or RealLiteral or MoneyLiteral or StringLiteral or BinaryLiteral or NullLiteral;

    public static SqlServerDdlResultExport Admit(string exportJson, string inputJson, IReadOnlyDictionary<string, string> sources, string repo, string revision)
    {
        var expected = Produce(inputJson, sources);
        var actual = SqlServerSchemaEvidence.Read<SqlServerDdlResultExport>(exportJson);
        Require(SqlServerSchemaEvidence.Json(actual) == SqlServerSchemaEvidence.Json(expected), "integrity-origin-version-or-generator-mismatch");
        Require(actual.Input.Repo == repo && actual.Input.Revision == revision, "unexpected-input-identity");
        return actual;
    }

    public static SqlServerDdlResultComparisonReport Compare(string exportJson, string inputJson, IReadOnlyDictionary<string, string> sources,
        SqlServerResultBindings bindings, IReadOnlyList<CodeFact> facts, string repo, string revision)
    {
        var ddl = Admit(exportJson, inputJson, sources, repo, revision);
        var comparison = SqlServerResultEvidence.CompareAdmitted(ddl.Version, ddl.GeneratorSha256, ddl.BoundedInputSha256, ddl.ExtractorVersion,
            ddl.RuleId, ddl.Tier, ddl.Declarations, ddl, bindings, facts, repo, revision);
        comparison = comparison with
        {
            Version = "sqlserver-ddl-result-column-comparison/1",
            BoundedInputSha256 = SqlServerSchemaEvidence.Digest(new { ddl, comparisonInputSha256 = comparison.BoundedInputSha256 }),
            Limitations = comparison.Limitations.Concat(new[] {
                "Origin: public-synthetic-tsql-ddl-result. Only one unconditional SELECT of aliased constants or CAST constants without FROM (optionally in one BEGIN/END) establishes a complete shape.",
                "IF, dynamic SQL/EXEC, multiple statements, SELECT *, FROM/table/view, UNION, SELECT assignment and nested blocks are explicit unknown shapes, never partial columns.",
                "CAST target types are visible facets only; literal values and body text are never retained or compared.",
                "Only the explicit bounded file list is covered; no migration ordering, catalog or deployed correspondence is claimed."
            }).ToArray()
        };
        var bounded = SqlServerSchemaEvidence.Digest(new { ddl, comparison });
        return new("sqlserver-ddl-result-comparison/1", SqlServerSchemaEvidence.GeneratorSha256, bounded, ddl, comparison);
    }
}
