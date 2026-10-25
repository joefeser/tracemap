using System.Globalization;
using System.Text;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace TraceMap.Core;

public sealed record SqlServerDdlFile(string Path, string Sha256);
public sealed record SqlServerDdlInput(string Version, string Repo, string Revision, string Server, string Database,
    bool PublicSynthetic, IReadOnlyList<SqlServerDdlFile> Files, SqlServerInputProvenance? Provenance = null);
public sealed record SqlServerDdlParameterSpan(SqlServerObjectIdentity Identity, string Parameter, string FilePath, int StartLine, int EndLine);
public sealed record SqlServerDdlUnsupported(SqlServerObjectIdentity Identity, string Parameter, string Kind,
    string RuleId, string Tier, string FilePath, int StartLine, int EndLine);
public sealed record SqlServerDdlExport(string Version, string GeneratorSha256, string BoundedInputSha256,
    string ParserVersion, string ParserSha256, string ExtractorVersion, string RuleId, string Tier,
    SqlServerDdlInput Input, SqlServerDeclarations Declarations, IReadOnlyList<SqlServerDdlParameterSpan> ParameterSpans, IReadOnlyList<SqlServerDdlUnsupported> Unsupported);
public sealed record SqlServerDdlComparisonReport(string Version, string GeneratorSha256, string BoundedInputSha256,
    SqlServerDdlExport DdlEvidence, SqlServerComparisonReport Comparison);

/// <summary>Public synthetic declarations only; syntax is parsed, never executed or interpreted.</summary>
public static class SqlServerDdlEvidence
{
    public const string ExtractorVersion = "sqlserver-ddl-parameters/1";
    public const string RuleId = "database.sqlserver.ddl-parameters.v1";
    public const string ParserVersion = "Microsoft.SqlServer.TransactSql.ScriptDom/170.191.0;TSql160;quoted-identifiers-on";
    public const int MaxFileBytes = 256 * 1024;
    public const int MaxTotalBytes = 1024 * 1024;
    public const int MaxFiles = 32;
    public const int MaxTokens = 100000;
    public const int ParserStackBytes = 64 * 1024 * 1024;
    private static readonly Lazy<string> ParserDigest = new(() => SqlServerSchemaEvidence.Hash(File.ReadAllBytes(typeof(TSql160Parser).Assembly.Location)));
    private static void Require([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool ok, string reason) { if (!ok) throw new ArgumentException("ddl-" + reason); }

    public static SqlServerDdlInput ReadInput(string json)
    {
        var input = SqlServerSchemaEvidence.Read<SqlServerDdlInput>(json);
        Require(input.Provenance is null || SqlServerSchemaEvidence.Sha(input.Provenance.GeneratorSha256) && SqlServerSchemaEvidence.Sha(input.Provenance.BoundedInputSha256), "invalid-input-provenance");
        Require(input.Version == "sqlserver-ddl-input/1" && input.PublicSynthetic, "unsupported-input-contract");
        Require(SqlServerSchemaEvidence.PathSafe(input.Repo) && !input.Repo.Contains('/') && SqlServerSchemaEvidence.Sha(input.Revision, 40)
            && SqlServerSchemaEvidence.Identifier(input.Server) && SqlServerSchemaEvidence.Identifier(input.Database), "invalid-logical-identity");
        Require(input.Files is { Count: > 0 and <= MaxFiles }, "file-limit");
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in input.Files!)
            Require(file is not null && SqlServerSchemaEvidence.PathSafe(file.Path) && file.Path.EndsWith(".sql", StringComparison.Ordinal)
                && SqlServerSchemaEvidence.Sha(file.Sha256) && names.Add(file.Path), "invalid-or-duplicate-file");
        return input;
    }

    public static SqlServerDdlExport Produce(string inputJson, IReadOnlyDictionary<string, string> sources)
    {
        var input = ReadInput(inputJson);
        Require(sources.Count == input.Files.Count && sources.Keys.Order(StringComparer.Ordinal).SequenceEqual(
            input.Files.Select(f => f.Path).Order(StringComparer.Ordinal), StringComparer.Ordinal), "source-set-mismatch");
        var procedures = new List<SqlServerProcedure>();
        var spans = new List<SqlServerDdlParameterSpan>();
        var unsupported = new List<SqlServerDdlUnsupported>();
        var identities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var total = 0;
        foreach (var file in input.Files.OrderBy(f => f.Path, StringComparer.Ordinal))
        {
            Require(sources.TryGetValue(file.Path, out var text) && text is not null, "source-missing");
            // Input is explicitly public/synthetic. Hash only the admitted UTF-8 file bytes.
            var bytes = new UTF8Encoding(false, true).GetBytes(text!);
            total += bytes.Length;
            Require(bytes.Length <= MaxFileBytes && total <= MaxTotalBytes, "input-size-limit");
            Require(SqlServerSchemaEvidence.Hash(bytes) == file.Sha256, "source-digest-mismatch");
            // Refuse unresolved SQLCMD anywhere, including strings/comments; never substitute.
            Require(!text!.Contains("$(", StringComparison.Ordinal)
                && !text.Split('\n').Any(l => l.TrimStart().StartsWith(':') || l.TrimStart().StartsWith("!!", StringComparison.Ordinal)), "sqlcmd-unresolved");
            var parser = new TSql160Parser(initialQuotedIdentifiers: true);
            // The digest above covers the exact bytes, including a leading UTF-8 BOM (common in
            // SSMS-saved scripts). ScriptDom treats U+FEFF as a syntax error, so lex without that
            // one leading mark; it precedes line 1, so recorded line spans are unchanged.
            var lexText = text.StartsWith('\uFEFF') ? text[1..] : text;
            var (tokens, tokenErrors) = OnParserStack(() =>
            {
                using var tokenReader = new StringReader(lexText);
                var stream = parser.GetTokenStream(tokenReader, out var lexErrors);
                return (stream, lexErrors);
            });
            Require(tokenErrors.Count == 0, "malformed-sql");
            Require(tokens.Count <= MaxTokens, "token-limit");
            var nestingBudget = 0;
            foreach (var token in tokens)
            {
                // Token kinds separate strings/comments from grammar. Conservatively bound
                // total open constructs, including BEGIN TRANSACTION, rather than infer flow.
                if (token.Text is "(" || token.TokenType.ToString() is "Begin" or "Case" or "If" or "While") nestingBudget++;
                Require(nestingBudget <= 128, "syntax-complexity-limit");
            }
            // Repetition of GO would imply execution ordering/repetition; reject it.
            for (var i = 0; i < tokens.Count; i++)
                if (tokens[i].TokenType == TSqlTokenType.Go)
                {
                    var next = i + 1;
                    while (next < tokens.Count && tokens[next].TokenType == TSqlTokenType.WhiteSpace) next++;
                    Require(next == tokens.Count || tokens[next].TokenType != TSqlTokenType.Integer, "batch-repeat-unsupported");
                }
            var (fragment, errors) = OnParserStack(() => (parser.Parse(tokens, out var parseErrors), parseErrors));
            var script = fragment as TSqlScript;
            Require(errors.Count == 0 && script is not null, "malformed-sql");
            foreach (var statement in script!.Batches.SelectMany(b => b.Statements))
            {
                Require(statement is CreateProcedureStatement or AlterProcedureStatement or CreateOrAlterProcedureStatement, "top-level-statement-unsupported");
                var proc = (ProcedureStatementBody)statement;
                var name = proc.ProcedureReference?.Name;
                Require(name is not null && name.Identifiers.Count == 2 && name.SchemaIdentifier is not null
                    && SqlServerSchemaEvidence.Identifier(name.SchemaIdentifier.Value) && SqlServerSchemaEvidence.Identifier(name.BaseIdentifier.Value)
                    && proc.ProcedureReference!.Number is null, "explicit-bounded-schema-required");
                var identity = new SqlServerObjectIdentity(input.Server, input.Database, name!.SchemaIdentifier.Value, name.BaseIdentifier.Value);
                // Case-only duplicate definitions are ambiguous under unresolved collation.
                Require(identities.Add(SqlServerSchemaEvidence.Json(identity)), "ambiguous-object");
                Require(procedures.Count < 256 && proc.Parameters.Count <= 128, "declaration-limit");
                Require(proc.Options.Count == 0 && !proc.IsForReplication && proc.MethodSpecifier is null
                    && proc.StatementList is { Statements.Count: > 0 }, "options-or-unavailable-body");
                var parameters = new List<SqlServerDeclarationParameter>();
                foreach (var p in proc.Parameters)
                {
                    spans.Add(new(identity, p.VariableName.Value, file.Path, p.StartLine, EndLine(p)));
                    Require(!p.IsVarying && p.Nullable is null && p.Modifier is ParameterModifier.None or ParameterModifier.Output,
                        "parameter-modifier-unsupported");
                    Require(p.DataType is SqlDataTypeReference, "alias-clr-or-table-type-unsupported");
                    var type = (SqlDataTypeReference)p.DataType;
                    var sqlType = type.SqlDataTypeOption switch
                    {
                        SqlDataTypeOption.Int => "Int", SqlDataTypeOption.BigInt => "BigInt", SqlDataTypeOption.Bit => "Bit",
                        SqlDataTypeOption.NVarChar => "NVarChar", SqlDataTypeOption.VarChar => "VarChar", SqlDataTypeOption.Decimal => "Decimal",
                        _ => null
                    };
                    Require(sqlType is not null, "built-in-type-unsupported");
                    // Reject qualified aliases to builtin names; do not silently normalize them.
                    Require(type.Name is null || type.Name.Identifiers.Count <= 1, "qualified-type-unsupported");
                    // ScriptDom folds ANSI synonyms (integer, dec, char varying, national character
                    // varying, nchar varying) into the same option. Admit only the documented canonical
                    // spelling; bracket/double-quote delimiters (SSMS-scripted DDL) do not change it.
                    Require(type.Name is not null && string.Equals(type.Name.BaseIdentifier.Value, sqlType, StringComparison.OrdinalIgnoreCase),
                        "type-spelling-unsupported");
                    int? size = null, precision = null, scale = null;
                    if (sqlType is "NVarChar" or "VarChar")
                    {
                        Require(type.Parameters.Count == 1, "explicit-size-required");
                        size = type.Parameters[0] is MaxLiteral ? -1 : Integer(type.Parameters[0]);
                    }
                    else if (sqlType == "Decimal")
                    {
                        Require(type.Parameters.Count <= 2, "unsupported-facets");
                        if (type.Parameters.Count > 0) precision = Integer(type.Parameters[0]);
                        if (type.Parameters.Count > 1) scale = Integer(type.Parameters[1]);
                        // Decimal remains unknown even with omitted facets; retain explicit evidence.
                        unsupported.Add(Gap(identity, p, "precision-scale-semantics-unsupported", file.Path, type));
                    }
                    else Require(type.Parameters.Count == 0, "unsupported-facets");
                    if (p.Value is not null) unsupported.Add(Gap(identity, p, "default-semantics-unsupported", file.Path, p.Value));
                    parameters.Add(new(p.VariableName.Value, sqlType!, size,
                        sqlType == "NVarChar" ? "utf16-code-units" : sqlType == "VarChar" ? "bytes" : "not-applicable",
                        p.Modifier == ParameterModifier.Output ? "InputOutput" : "Input", p.Value is not null,
                        precision, scale, null, p.StartLine));
                }
                procedures.Add(new(identity, file.Path, proc.StartLine, EndLine(proc), parameters));
            }
        }
        var declarations = new SqlServerDeclarations("sqlserver-ddl-declarations/1", input.Repo, input.Revision,
            "public-synthetic-tsql-ddl", "ordinal-exact-no-aliases", procedures);
        SqlServerSchemaEvidence.Validate(declarations, ddl: true);
        // No raw definition/default/body text enters the shareable export or its input projection.
        var export = new SqlServerDdlExport("sqlserver-ddl-schema/1", SqlServerSchemaEvidence.GeneratorSha256, SqlServerSchemaEvidence.Digest(input),
            ParserVersion, ParserDigest.Value, ExtractorVersion, RuleId, EvidenceTiers.Tier2Structural, input, declarations, spans, unsupported);
        Require(Encoding.UTF8.GetByteCount(SqlServerSchemaEvidence.Json(export)) <= SqlServerSchemaEvidence.MaxBytes, "export-size-limit");
        return export;
    }

    /// <summary>
    /// ScriptDom's recursive-descent parser recurses on some constructs that the lexical
    /// nesting budget does not cap (prefix unary chains, JOIN/APPLY chains). A stack overflow
    /// aborts the process and cannot fail closed, so lexing and parsing always run on a
    /// dedicated thread whose stack does not depend on the caller (1–8 MiB on common hosts).
    /// Measured worst uncapped cost is about 200 bytes of stack per token; MaxTokens therefore
    /// needs about 20 MiB, and this fixed size leaves a margin of more than 3x.
    /// </summary>
    private static T OnParserStack<T>(Func<T> work)
    {
        T result = default!;
        System.Runtime.ExceptionServices.ExceptionDispatchInfo? failure = null;
        var thread = new Thread(() =>
        {
            try { result = work(); }
            catch (Exception ex) { failure = System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex); }
        }, ParserStackBytes) { IsBackground = true, Name = "tracemap-tsql-parser" };
        thread.Start();
        thread.Join();
        failure?.Throw();
        return result;
    }

    private static int Integer(Literal literal)
    {
        Require(literal is IntegerLiteral && int.TryParse(literal.Value, NumberStyles.None, CultureInfo.InvariantCulture, out _), "invalid-numeric-facet");
        return int.Parse(literal.Value, CultureInfo.InvariantCulture);
    }
    private static int EndLine(TSqlFragment fragment) => fragment.ScriptTokenStream[fragment.LastTokenIndex].Line
        + fragment.ScriptTokenStream[fragment.LastTokenIndex].Text.Count(c => c == '\n');
    private static SqlServerDdlUnsupported Gap(SqlServerObjectIdentity identity, ProcedureParameter parameter, string kind, string path, TSqlFragment fragment)
        => new(identity, parameter.VariableName.Value, kind, RuleId, EvidenceTiers.Tier4Unknown, path, fragment.StartLine, EndLine(fragment));

    public static SqlServerDdlExport Admit(string exportJson, string inputJson, IReadOnlyDictionary<string, string> sources, string repo, string revision)
    {
        var expected = Produce(inputJson, sources);
        var actual = SqlServerSchemaEvidence.Read<SqlServerDdlExport>(exportJson);
        Require(SqlServerSchemaEvidence.Json(actual) == SqlServerSchemaEvidence.Json(expected), "integrity-origin-version-or-generator-mismatch");
        Require(actual.Input.Repo == repo && actual.Input.Revision == revision, "unexpected-input-identity");
        return actual;
    }

    public static SqlServerDdlComparisonReport Compare(string exportJson, string inputJson, IReadOnlyDictionary<string, string> sources,
        SqlServerBindings bindings, IReadOnlyList<CodeFact> facts, string repo, string revision)
    {
        var ddl = Admit(exportJson, inputJson, sources, repo, revision);
        var schema = new SqlServerSchemaExport(ddl.Version, ddl.GeneratorSha256, ddl.BoundedInputSha256,
            ddl.ExtractorVersion, ddl.Declarations, ddl.RuleId, ddl.Tier);
        var comparison = SqlServerSchemaEvidence.CompareAdmitted(schema, bindings, facts, repo, revision);
        var rows = comparison.Comparisons.Select(row =>
        {
            var gaps = ddl.Unsupported.Where(g => g.Identity == row.Declaration?.Identity).Select(g => "ddl-" + g.Kind + ":" + g.Parameter).ToArray();
            return gaps.Length == 0 ? row : row with { Status = "unknown", Tier = EvidenceTiers.Tier4Unknown,
                Reasons = row.Reasons.Concat(gaps).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray() };
        }).ToArray();
        comparison = comparison with { Version = "sqlserver-ddl-parameter-comparison/1", Comparisons = rows,
            BoundedInputSha256 = SqlServerSchemaEvidence.Digest(new { ddl, comparisonInputSha256 = comparison.BoundedInputSha256 }),
            Limitations = comparison.Limitations.Concat(new[] {
                "Origin: public-synthetic-tsql-ddl. Declarations are source DDL, not independently authored JSON declarations or deployed catalog state.",
                "Bodies are syntax-checked only; dynamic SQL, OUTPUT assignments, RETURN and result shapes are never interpreted.",
                "Defaults and precision/scale are unsupported evidence; collation syntax and unsupported declarations reject the entire input.",
                "Only the explicit bounded file list is covered; no migration ordering or whole-database coverage is claimed."
            }).ToArray() };
        // Bind parser/unsupported provenance as well as the shared public call/schema projection.
        var bounded = SqlServerSchemaEvidence.Digest(new { ddl, comparison });
        return new("sqlserver-ddl-comparison/1", SqlServerSchemaEvidence.GeneratorSha256, bounded, ddl, comparison);
    }
}
