using System.Text;
using System.Text.Json.Serialization;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace TraceMap.Core;

public sealed record SqlLocalInput(string Version, string Visibility, string ContextRepository, string ContextCommitSha,
    string DefinitionSet, string Server, string Database, IReadOnlyList<SqlServerDdlFile> Files,
    SqlServerInputProvenance? Provenance = null);
public sealed record SqlLocalSpan(string FilePath, int StartLine, int EndLine);
public sealed record SqlLocalGap(string Kind, string RuleId, string Tier, SqlLocalSpan Span);
public sealed record SqlLocalParameter(string Name, string? DeclaredType, string Direction, bool HasDefault, SqlLocalSpan Span);
public sealed record SqlLocalObject(string Id, string Kind, string Schema, string Name, string RuleId, string Tier,
    SqlLocalSpan Span, IReadOnlyList<SqlLocalParameter> Parameters, SqlServerDdlTable? Table, SqlServerResultShape? ResultShape);
public sealed record SqlLocalDependency(string SourceId, string Kind, string? Schema, string Name, string Resolution,
    string? TargetId, string RuleId, string Tier, SqlLocalSpan Span);
public sealed record SqlLocalExport(string Version, string Visibility, string ClaimLevel, string Coverage, string Verdict, string GeneratorSha256,
    string BoundedInputSha256, string ParserVersion, string ParserSha256, string ExtractorVersion, string RuleId,
    SqlLocalInput Input, IReadOnlyList<SqlLocalObject> Objects, IReadOnlyList<SqlLocalDependency> Dependencies,
    IReadOnlyList<SqlLocalGap> Gaps, IReadOnlyList<string> Limitations,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SqlDefinitionSelectionEvidence? Selection = null);

/// <summary>Local-only AST definition inventory. No database access, execution, source-call binding or public projection.</summary>
public static class SqlServerLocalDefinitions
{
    public const string InputVersion = "sqlserver-local-definitions-input/1";
    public const string ExportVersion = "sqlserver-local-definitions/1";
    public const string RuleId = "database.sqlserver.local-definitions.v1";
    public const string DependencyRule = "database.sqlserver.local-definition-reference.v1";
    public const string ExtractorVersion = "sqlserver-local-definitions/1";
    public const int MaxObjects = 512;
    public const int MaxDependencies = 4096;

    private static void Require(bool condition, string reason)
    { if (!condition) throw new ArgumentException("sql-local-" + reason); }
    private static bool Name(string? name) => name is { Length: > 0 and <= 128 } && !name.Any(char.IsControl);

    public static SqlLocalInput ReadInput(string json)
    {
        var input = SqlServerSchemaEvidence.Read<SqlLocalInput>(json);
        Require(input.Version == InputVersion && input.Visibility == "local-only", "input-contract-invalid");
        Require(SqlServerSchemaEvidence.PathSafe(input.ContextRepository) && !input.ContextRepository.Contains('/')
            && (SqlServerSchemaEvidence.Sha(input.ContextCommitSha, 40) || SqlServerSchemaEvidence.Sha(input.ContextCommitSha))
            && Name(input.DefinitionSet) && Name(input.Server) && Name(input.Database), "context-invalid");
        Require(input.Files is { Count: > 0 and <= SqlServerDdlEvidence.MaxFiles }, "file-limit");
        Require(input.Provenance is null || SqlServerSchemaEvidence.Sha(input.Provenance.GeneratorSha256)
            && SqlServerSchemaEvidence.Sha(input.Provenance.BoundedInputSha256), "input-provenance-invalid");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in input.Files!)
            Require(file is not null && SqlServerSchemaEvidence.PathSafe(file.Path) && file.Path.EndsWith(".sql", StringComparison.Ordinal)
                && SqlServerSchemaEvidence.Sha(file.Sha256) && names.Add(file.Path), "file-invalid-or-duplicate");
        return input;
    }

    public static SqlLocalExport Produce(string inputJson, IReadOnlyDictionary<string, string> sources, CancellationToken token = default)
    {
        var input = ReadInput(inputJson);
        Require(sources.Count == input.Files.Count && sources.Keys.Order(StringComparer.Ordinal).SequenceEqual(
            input.Files.Select(file => file.Path).Order(StringComparer.Ordinal), StringComparer.Ordinal), "source-set-mismatch");
        var definitions = new List<(string Kind, string Schema, string Name, string Path, TSqlStatement Statement)>();
        var gaps = new List<SqlLocalGap>();
        var identities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var total = 0;
        var shapeMutation = false;
        foreach (var file in input.Files.OrderBy(file => file.Path, StringComparer.Ordinal))
        {
            token.ThrowIfCancellationRequested();
            Require(sources.TryGetValue(file.Path, out var text) && text is not null, "source-missing");
            var bytes = new UTF8Encoding(false, true).GetBytes(text!);
            total += bytes.Length;
            Require(bytes.Length <= SqlServerDdlEvidence.MaxFileBytes && total <= SqlServerDdlEvidence.MaxTotalBytes, "input-size-limit");
            Require(SqlServerSchemaEvidence.Hash(bytes) == file.Sha256, "source-digest-mismatch");
            // Shared bounded lexer/parser only; no publicSynthetic manifest is manufactured.
            var script = SqlServerDdlEvidence.ParseBoundedSql(text!);
            foreach (var statement in script.Batches.SelectMany(batch => batch.Statements))
            {
                token.ThrowIfCancellationRequested();
                string? kind = null;
                SchemaObjectName? name = null;
                switch (statement)
                {
                    case ProcedureStatementBody procedure:
                        Require(procedure.ProcedureReference?.Number is null, "numbered-procedure-unsupported");
                        kind = "procedure"; name = procedure.ProcedureReference?.Name; break;
                    case CreateTableStatement table: kind = "table"; name = table.SchemaObjectName; break;
                    case ViewStatementBody view: kind = "view"; name = view.SchemaObjectName; break;
                    case FunctionStatementBody function: kind = "function"; name = function.Name; break;
                    case UseStatement use:
                        Require(use.DatabaseName?.Value == input.Database, "database-context-mismatch");
                        continue;
                    case PredicateSetStatement setting when setting.Options is SetOptions.AnsiNulls or SetOptions.QuotedIdentifier:
                        // OFF changes interpretation; ON agrees with the pinned parser settings.
                        shapeMutation |= !setting.IsOn;
                        gaps.Add(Gap("session-setting-not-runtime-observation", file.Path, statement)); continue;
                    default:
                        if (statement is AlterTableStatement || statement.GetType().Name.StartsWith("Drop", StringComparison.Ordinal))
                        {
                            shapeMutation = true;
                            gaps.Add(Gap("schema-mutation-not-applied", file.Path, statement)); continue;
                        }
                        // Row-bearing or executable top-level scripts are not schema-definition input.
                        throw new ArgumentException("sql-local-top-level-statement-not-admitted");
                }
                Require(name is not null && name.Identifiers.Count == 2 && Name(name.SchemaIdentifier?.Value) && Name(name.BaseIdentifier?.Value),
                    "explicit-schema-object-required");
                var schema = name!.SchemaIdentifier!.Value; var objectName = name.BaseIdentifier!.Value;
                Require(identities.Add(SqlServerSchemaEvidence.Json(new[] { schema, objectName })), "ambiguous-definition-set");
                Require(definitions.Count < MaxObjects, "object-limit");
                definitions.Add((kind!, schema, objectName, file.Path, statement));
            }
        }
        Require(definitions.Count > 0, "no-definitions");
        var tables = new List<SqlServerDdlTable>();
        foreach (var definition in definitions.Where(item => item.Statement is CreateTableStatement))
        {
            var (table, unsupported) = SqlServerDdlTableResultEvidence.AnalyseTable(Identity(definition.Schema, definition.Name),
                definition.Path, (CreateTableStatement)definition.Statement);
            if (shapeMutation) table = table with { Completeness = "unknown", UnknownReason = "definition-set-context-unmodeled", Columns = [] };
            tables.Add(table);
            if (unsupported is not null) gaps.Add(Gap(unsupported.Kind, definition.Path, definition.Statement));
        }
        var objects = new List<SqlLocalObject>();
        var references = new List<SqlLocalDependency>();
        foreach (var definition in definitions)
        {
            token.ThrowIfCancellationRequested();
            var id = "sql-object-" + SqlServerSchemaEvidence.Digest(new[] { input.ContextRepository, input.ContextCommitSha,
                input.DefinitionSet, input.Server, input.Database, definition.Kind, definition.Schema, definition.Name });
            var parameters = new List<SqlLocalParameter>();
            SqlServerResultShape? shape = null;
            if (definition.Statement is ProcedureStatementBody procedure)
            {
                Require(procedure.Parameters.Count <= 128, "parameter-limit");
                var parameterNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var parameter in procedure.Parameters)
                {
                    Require(Name(parameter.VariableName?.Value) && parameterNames.Add(parameter.VariableName!.Value), "parameter-name-ambiguous");
                    var type = TypeSpelling(parameter.DataType);
                    parameters.Add(new(parameter.VariableName!.Value, type,
                        parameter.Modifier == ParameterModifier.Output ? "output-capable" : "input", parameter.Value is not null,
                        Span(definition.Path, parameter)));
                    if (type is null || parameter.Nullable is not null || parameter.IsVarying || parameter.Modifier is not (ParameterModifier.None or ParameterModifier.Output))
                        gaps.Add(Gap("parameter-facets-not-modeled", definition.Path, parameter));
                    if (parameter.Value is not null) gaps.Add(Gap("parameter-default-not-evaluated", definition.Path, parameter));
                }
                var body = NormalizeNoCount(procedure);
                if (procedure.StatementList is null || procedure.StatementList.Statements.Count == 0 || procedure.Options.Count != 0
                    || procedure.MethodSpecifier is not null || procedure.IsForReplication || shapeMutation)
                    shape = new(Identity(definition.Schema, definition.Name), definition.Path, procedure.StartLine,
                        SqlServerDdlEvidence.EndLine(procedure), "first", "unknown", "procedure-or-context-not-modeled", []);
                else
                {
                    var analysis = SqlServerDdlResultEvidence.Analyse(Identity(definition.Schema, definition.Name), definition.Path, body, RuleId,
                        (select, query, unknown) => SqlServerDdlTableResultEvidence.AnalyseTableProjection(
                            Identity(definition.Schema, definition.Name), definition.Path, select, query, unknown, tables).Item1);
                    shape = analysis.Shape;
                    foreach (var gap in analysis.Gaps) gaps.Add(new(gap.Kind, RuleId, EvidenceTiers.Tier4Unknown,
                        new(gap.FilePath, gap.StartLine, gap.EndLine)));
                }
                if (shape.Completeness != "complete") gaps.Add(Gap(shape.UnknownReason ?? "result-shape-unknown", definition.Path, procedure));
            }
            else if (definition.Kind is "function" or "view")
                gaps.Add(Gap("view-or-function-shape-not-modeled", definition.Path, definition.Statement));
            var tableDefinition = tables.SingleOrDefault(table => table.Identity.Schema == definition.Schema && table.Identity.Table == definition.Name);
            objects.Add(new(id, definition.Kind, definition.Schema, definition.Name, RuleId, EvidenceTiers.Tier2Structural,
                Span(definition.Path, definition.Statement), parameters, tableDefinition, shape));
            definition.Statement.Accept(new ReferenceVisitor(id, definition.Path, references, gaps));
            Require(references.Count <= MaxDependencies && gaps.Count <= 8192, "evidence-limit");
        }
        var linked = references.Select(reference =>
        {
            var targets = objects.Where(item => reference.Schema == item.Schema && reference.Name == item.Name
                && (reference.Kind == "relation" ? item.Kind is "table" or "view" : item.Kind == reference.Kind)).ToArray();
            return reference with
            {
                Resolution = reference.Schema is null ? "unqualified-or-external-unknown" : targets.Length == 1 ? "declared-definition-only" : "definition-not-admitted-unknown",
                TargetId = targets.Length == 1 ? targets[0].Id : null
            };
        }).ToArray();
        var export = new SqlLocalExport(ExportVersion, "local-only", "offline-definitions-not-deployment-or-call-binding", "partial", "no-aggregate-clean-verdict",
            SqlServerSchemaEvidence.GeneratorSha256, SqlServerSchemaEvidence.Digest(input), SqlServerDdlEvidence.ParserVersion,
            SqlServerDdlEvidence.ParserDigest.Value, ExtractorVersion, RuleId, input, objects, linked, gaps,
            ["Input fingerprints and object identities are local-only; no public privacy projection is provided.",
             "Context repository/commit identifies application context only, not the database export revision or deployed database.",
             "Definitions are an unordered set. Duplicate/case-only identities refuse admission; no migration ordering is inferred.",
             "Parameter types/directions are declarations, not full comparison contracts; defaults and literal/body text are never emitted.",
             "Complete result shapes use the bounded single-SELECT subset only; joins, filters, branches, dynamic SQL, views/functions and schema mutations stay unknown.",
             "Dependency references are syntax observations, not execution, reachability or deployed resolution. No source call is automatically bound.",
             "Partial local evidence only; no aggregate clean verdict, SQL execution, database connection or runtime observation."]);
        Require(Encoding.UTF8.GetByteCount(SqlServerSchemaEvidence.Json(export)) <= SqlServerSchemaEvidence.MaxBytes, "export-size-limit");
        return export;

        SqlServerObjectIdentity Identity(string schema, string name) => new(input.Server, input.Database, schema, name);
    }

    public static SqlLocalExport Admit(string exportJson, string inputJson, IReadOnlyDictionary<string, string> sources,
        CancellationToken token = default)
    {
        var expected = Produce(inputJson, sources, token);
        var actual = SqlServerSchemaEvidence.Read<SqlLocalExport>(exportJson);
        Require(SqlServerSchemaEvidence.Json(expected) == SqlServerSchemaEvidence.Json(actual), "export-integrity-mismatch");
        return actual;
    }

    private static string? TypeSpelling(DataTypeReference? type)
    {
        if (type is not ParameterizedDataTypeReference named || named.Name is null || named.Name.Identifiers.Any(item => !Name(item.Value))) return null;
        if (named.Parameters.Count > 2 || named.Parameters.Any(item => item is not (IntegerLiteral or MaxLiteral) || item.Value.Length > 10)) return null;
        return string.Join('.', named.Name.Identifiers.Select(item => item.Value))
            + (named.Parameters.Count == 0 ? "" : "(" + string.Join(',', named.Parameters.Select(item => item.Value)) + ")");
    }

    private static ProcedureStatementBody NormalizeNoCount(ProcedureStatementBody original)
    {
        IList<TSqlStatement> statements = original.StatementList?.Statements ?? [];
        if (statements.Count == 1 && statements[0].GetType() == typeof(BeginEndBlockStatement))
            statements = ((BeginEndBlockStatement)statements[0]).StatementList?.Statements ?? [];
        var filtered = statements.SkipWhile(statement => statement is PredicateSetStatement setting && setting.Options == SetOptions.NoCount).ToArray();
        var list = new StatementList();
        foreach (var statement in filtered) list.Statements.Add(statement);
        return new CreateProcedureStatement { StatementList = list, ScriptTokenStream = original.ScriptTokenStream,
            FirstTokenIndex = original.FirstTokenIndex, LastTokenIndex = original.LastTokenIndex };
    }

    private static SqlLocalSpan Span(string path, TSqlFragment at) => new(path, at.StartLine, SqlServerDdlEvidence.EndLine(at));
    private static SqlLocalGap Gap(string kind, string path, TSqlFragment at) => new(kind, RuleId, EvidenceTiers.Tier4Unknown, Span(path, at));

    private sealed class ReferenceVisitor(string source, string path, List<SqlLocalDependency> references, List<SqlLocalGap> gaps) : TSqlFragmentVisitor
    {
        public override void ExplicitVisit(NamedTableReference node)
        { Add("relation", node.SchemaObject, node); base.ExplicitVisit(node); }
        public override void ExplicitVisit(ForeignKeyConstraintDefinition node)
        { Add("relation", node.ReferenceTableName, node); base.ExplicitVisit(node); }
        public override void ExplicitVisit(ExecutableProcedureReference node)
        {
            if (node.ProcedureReference?.ProcedureReference?.Name is { } name) Add("procedure", name, node);
            else gaps.Add(Gap("dynamic-procedure-reference-unknown", path, node));
            base.ExplicitVisit(node);
        }
        public override void ExplicitVisit(ExecuteStatement node)
        {
            if (node.ExecuteSpecification?.ExecutableEntity is not ExecutableProcedureReference)
                gaps.Add(Gap("dynamic-execution-reference-unknown", path, node));
            base.ExplicitVisit(node);
        }
        public override void ExplicitVisit(FunctionCall node)
        {
            var parts = (node.CallTarget as MultiPartIdentifierCallTarget)?.MultiPartIdentifier?.Identifiers;
            AddRow("function", parts is { Count: 1 } ? parts[0].Value : null, node.FunctionName.Value, node);
            base.ExplicitVisit(node);
        }
        private void Add(string kind, SchemaObjectName name, TSqlFragment at) =>
            AddRow(kind, name.Identifiers.Count == 2 ? name.SchemaIdentifier?.Value : null, name.BaseIdentifier.Value, at);
        private void AddRow(string kind, string? schema, string name, TSqlFragment at)
        {
            Require(references.Count < MaxDependencies && gaps.Count < 8192, "evidence-limit");
            Require(Name(name) && (schema is null || Name(schema)), "reference-name-invalid");
            references.Add(new(source, kind, schema, name, "unresolved", null, DependencyRule, EvidenceTiers.Tier2Structural, Span(path, at)));
        }
    }
}
