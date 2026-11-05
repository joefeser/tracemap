using System.Diagnostics.CodeAnalysis;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TraceMap.Core;

public sealed record SqlSourceMethodSelector(string Role, string Path, string Sha256, string TypeName, string MethodName,
    IReadOnlyList<string> ParameterTypes);
public sealed record SqlSourceBindingInput(string Version, string Visibility, string Repository, string CommitSha,
    string Profile, string DispatchValuesOrigin, string SqlInputSha256, SqlDefinitionTemplateSettings Template,
    IReadOnlyList<SqlSourceMethodSelector> Methods, SqlServerInputProvenance? Provenance = null);
public sealed record SqlSourceObservation(string Role, string Status, string RuleId, string Tier, string TypeName,
    string MethodName, string SourceFileSha256, string? MethodSyntaxSha256, SqlLocalSpan? Span);
public sealed record SqlSourceParameterCandidate(string Status, string SourceName, int SourceOrdinal, string SourceType,
    string SqlObjectId, string SqlName, int SqlOrdinal, string? DeclaredSqlType, string RuleId, string Tier, SqlLocalSpan SourceSpan, SqlLocalSpan SqlSpan);
public sealed record SqlSourceBindingGap(string Kind, string RuleId, string Tier, string? Role, SqlLocalSpan? Span);
public sealed record SqlSourceBindingReport(string Version, string Visibility, string GeneratorSha256, string BoundedInputSha256,
    string ExtractorVersion, string RuleId, string Tier, string SourceParserVersion, string SourceParserSha256,
    string SqlEvidenceSha256, string SqlGeneratorSha256, string SqlParserSha256, SqlLocalExport SqlEvidence, SqlSourceBindingInput Input,
    string Coverage, string Verdict, string Status, string? ProcedureName, string? SqlObjectId,
    IReadOnlyList<SqlSourceObservation> Observations, IReadOnlyList<SqlSourceParameterCandidate> Parameters,
    IReadOnlyList<SqlSourceBindingGap> Gaps, IReadOnlyList<string> Limitations);

/// <summary>Syntax-only conditional wrapper candidate. Never a terminal operation or compiler/runtime call binding.</summary>
public static partial class SqlServerSourceBinding
{
    public const string InputVersion = "sqlserver-source-binding-input/1";
    public const string ReportVersion = "sqlserver-source-binding/1";
    public const string RuleId = "database.sqlserver.source-procedure-candidate.v1";
    public const int MaxFileBytes = 1_048_576;
    public const int MaxTotalBytes = 4_194_304;
    private static readonly string[] Roles = ["entry", "provider", "connection", "helper", "formatter"];
    private static void Require([DoesNotReturnIf(false)] bool condition, string reason)
    { if (!condition) throw new ArgumentException("sql-source-binding-" + reason); }

    public static SqlSourceBindingInput ReadInput(string json)
    {
        var input = SqlServerSchemaEvidence.Read<SqlSourceBindingInput>(json);
        Require(input.Version == InputVersion && input.Visibility == "local-only" && input.Profile == "dnn-reader-wrapper/1", "input-contract");
        Require(input.DispatchValuesOrigin is "review-scenario" or "owner-supplied" && SqlServerSchemaEvidence.Sha(input.SqlInputSha256), "explicit-context");
        Require(input.Template is not null && input.Methods is { Count: 5 }, "selectors-required");
        Require(input.Methods.Select(method => method?.Role).Order(StringComparer.Ordinal).SequenceEqual(Roles.Order(StringComparer.Ordinal)), "roles-invalid-or-duplicate");
        foreach (var method in input.Methods)
            Require(method is not null && SqlServerSchemaEvidence.PathSafe(method.Path) && method.Path.EndsWith(".cs", StringComparison.Ordinal)
                && SqlServerSchemaEvidence.Sha(method.Sha256) && method.TypeName is { Length: > 0 and <= 256 }
                && method.TypeName.Split('.').All(SqlServerSchemaEvidence.Identifier) && SqlServerSchemaEvidence.Identifier(method.MethodName)
                && method.ParameterTypes is { Count: > 0 and <= 8 }
                && method.ParameterTypes.All(type => type is { Length: > 0 and <= 80 } && type.All(c => char.IsAsciiLetterOrDigit(c) || "._[]".Contains(c))), "method-selector-invalid");
        Require(input.Methods.GroupBy(method => method.Path, StringComparer.OrdinalIgnoreCase).All(group =>
            group.Select(method => (method.Path, method.Sha256)).Distinct().Count() == 1), "source-path-or-hash-ambiguous");
        SqlServerLocalDefinitions.ReadInput(SqlServerSchemaEvidence.Json(new SqlLocalInput(SqlServerLocalDefinitions.InputVersion,
            "local-only", input.Repository, input.CommitSha, "binding-context", "context", "context", [new("context.sql", new('0', 64))], input.Provenance)));
        return input;
    }

    public static SqlSourceBindingReport Produce(string inputJson, IReadOnlyDictionary<string, string> sources,
        string sqlInputJson, IReadOnlyDictionary<string, string> sqlSources, CancellationToken token = default)
    {
        var input = ReadInput(inputJson);
        Require(SqlServerSchemaEvidence.Hash(new UTF8Encoding(false, true).GetBytes(sqlInputJson)) == input.SqlInputSha256, "sql-input-drift");
        var sql = SqlServerDefinitionSelection.Produce(sqlInputJson, sqlSources, token);
        Require(sql.Input.ContextRepository == input.Repository && sql.Input.ContextCommitSha == input.CommitSha
            && sql.Selection!.Input.Template == input.Template, "sql-context-or-template-mismatch");
        var files = input.Methods.Select(method => method.Path).Distinct(StringComparer.Ordinal).ToArray();
        Require(sources.Keys.Order(StringComparer.Ordinal).SequenceEqual(files.Order(StringComparer.Ordinal)), "source-set-mismatch");
        var roots = new Dictionary<string, CompilationUnitSyntax>(StringComparer.Ordinal);
        var total = 0;
        foreach (var path in files)
        {
            token.ThrowIfCancellationRequested();
            var bytes = new UTF8Encoding(false, true).GetBytes(sources[path]); total += bytes.Length;
            Require(bytes.Length <= MaxFileBytes && total <= MaxTotalBytes, "source-size-limit");
            Require(SqlServerSchemaEvidence.Hash(bytes) == input.Methods.First(method => method.Path == path).Sha256, "source-digest-mismatch");
            var tree = CSharpSyntaxTree.ParseText(sources[path], CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.CSharp14), cancellationToken: token);
            Require(!tree.GetDiagnostics(token).Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error), "source-syntax-error");
            var root = tree.GetCompilationUnitRoot(token);
            Require(root.DescendantNodes().Take(200_001).Count() <= 200_000, "source-node-limit");
            roots.Add(path, root);
        }
        var methods = new Dictionary<string, MethodDeclarationSyntax>(StringComparer.Ordinal);
        var observations = new List<SqlSourceObservation>(); var gaps = new List<SqlSourceBindingGap>();
        foreach (var role in Roles)
        {
            var selector = input.Methods.Single(method => method.Role == role);
            var found = roots[selector.Path].DescendantNodes().OfType<MethodDeclarationSyntax>().Where(method =>
                TypeName(method) == selector.TypeName && method.Identifier.ValueText == selector.MethodName
                && method.ParameterList.Parameters.Select(parameter => parameter.Type?.WithoutTrivia().ToString()).SequenceEqual(selector.ParameterTypes)).ToArray();
            if (found.Length != 1 || found[0].DescendantTokens().Take(8193).Count() > 8192)
            {
                observations.Add(new(role, found.Length > 1 ? "ambiguous" : "unavailable", RuleId, EvidenceTiers.Tier4Unknown,
                    selector.TypeName, selector.MethodName, selector.Sha256, null, null));
                gaps.Add(Gap("selected-method-unavailable-or-ambiguous", role, null)); continue;
            }
            var selected = found[0]; methods.Add(role, selected);
            observations.Add(new(role, "selected-syntax-only", RuleId, EvidenceTiers.Tier3SyntaxOrTextual, selector.TypeName,
                selector.MethodName, selector.Sha256, SqlServerSchemaEvidence.Digest(selected.DescendantTokens().Select(t => new { kind = t.RawKind, t.Text }).ToArray()), Span(selector.Path, selected)));
        }
        var supported = new HashSet<string>(StringComparer.Ordinal);
        string? procedure = null;
        if (methods.TryGetValue("entry", out var entry) && Entry(entry, out procedure)) supported.Add("entry");
        if (methods.TryGetValue("provider", out var provider) && Provider(provider, input)) supported.Add("provider");
        if (methods.TryGetValue("connection", out var connection) && Connection(connection, input)) supported.Add("connection");
        if (methods.TryGetValue("helper", out var helper) && Helper(helper, input)) supported.Add("helper");
        if (methods.TryGetValue("formatter", out var formatter) && Formatter(formatter)) supported.Add("formatter");
        var conventional = new HashSet<string>(new[] { "CommandType", "IDataReader", "IDataParameter", "StringBuilder", "CultureInfo", "Database" }, StringComparer.Ordinal);
        var aliases = new HashSet<string>(conventional.Concat(input.Methods.Select(method => method.TypeName.Split('.')[^1])), StringComparer.Ordinal);
        if (roots.Values.Any(root => root.DescendantNodes().OfType<UsingDirectiveSyntax>().Any(use => use.Alias is { } alias && aliases.Contains(alias.Name.Identifier.ValueText))
            || root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>().Any(type => conventional.Contains(type.Identifier.ValueText))))
        {
            supported.Clear();
            gaps.Add(Gap("visible-protocol-symbol-alias-or-shadow-outside-profile", null, null));
        }
        observations = observations.Select(observation => observation.Status != "selected-syntax-only" ? observation : observation with
            { Status = supported.Contains(observation.Role) ? "profile-shape-supported" : "profile-shape-unsupported" }).ToList();
        foreach (var observation in observations.Where(observation => observation.Status == "profile-shape-unsupported"))
            gaps.Add(Gap("wrapper-shape-outside-profile", observation.Role, observation.Span));
        var targets = procedure is null ? [] : sql.Objects.Where(item => item.Kind == "procedure"
            && item.Schema == input.Template.DatabaseOwner && item.Name == input.Template.ObjectQualifier + procedure).ToArray();
        var candidate = supported.Count == Roles.Length && targets.Length == 1;
        if (targets.Length != 1) gaps.Add(Gap("procedure-definition-not-uniquely-matched", "entry", observations.First(item => item.Role == "entry").Span));
        var parameters = new List<SqlSourceParameterCandidate>();
        if (candidate)
        {
            var target = targets[0];
            if (target.Parameters.Count == 1 && target.Parameters[0].Direction == "input")
                parameters.Add(new("conditional-positional-candidate", methods["entry"].ParameterList.Parameters[0].Identifier.ValueText,
                    0, "int", target.Id, target.Parameters[0].Name, 0, target.Parameters[0].DeclaredType, RuleId,
                    EvidenceTiers.Tier3SyntaxOrTextual, Span(input.Methods.Single(item => item.Role == "entry").Path,
                        methods["entry"].ParameterList.Parameters[0]), target.Parameters[0].Span));
            else gaps.Add(Gap("sql-parameter-count-or-direction-outside-profile", "entry", target.Span));
        }
        foreach (var kind in new[] { "compiler-symbol-and-overload-resolution-unverified", "virtual-provider-dispatch-unverified",
            "configured-connection-provider-dispatch-unverified", "source-owner-qualifier-properties-unverified", "external-petapoco-parameter-and-command-transport-unverified",
            "conditional-compilation-symbols-unverified", "runtime-invocation-not-observed", "caller-cache-event-and-hydration-path-not-bound", "result-shape-and-type-coercion-unverified" })
            gaps.Add(Gap(kind, null, null));
        var parser = typeof(CSharpSyntaxTree).Assembly;
        var report = new SqlSourceBindingReport(ReportVersion, "local-only", SqlServerSchemaEvidence.GeneratorSha256,
            SqlServerSchemaEvidence.Digest(new { input, sqlEvidence = SqlServerSchemaEvidence.Digest(sql) }), "sqlserver-source-binding/1", RuleId,
            EvidenceTiers.Tier3SyntaxOrTextual, parser.GetName().Name + "/" + parser.GetName().Version + ";CSharp14;default-preprocessor-symbols",
            SqlServerSchemaEvidence.Hash(File.ReadAllBytes(parser.Location)), SqlServerSchemaEvidence.Digest(sql), sql.GeneratorSha256,
            sql.ParserSha256, sql, input, "partial", "no-aggregate-clean-verdict", candidate ? "conditional-profile-candidate" : "unknown",
            procedure, candidate ? targets[0].Id : null, observations, parameters, gaps,
            ["Local-only syntax AST observations over exact source files and separately regenerated selected SQL; no raw bodies or unrelated literal values are emitted.",
             "The five explicit method selectors and matching wrapper ASTs support a conditional route only; conventional framework/DNN symbols, overloads and dispatch are not compiler-resolved.",
             "Declared owner/qualifier and provider selections are review-scenario or owner-supplied assumptions, not installed configuration, execution or authentication.",
             "One direct built-in int argument is conditionally associated with SQL formal ordinal zero under the recognized primitive positional formatter path; no named ADO parameter, value, type/size/coercion or runtime transport is proven.",
             "PetaPoco implementation is outside this profile. Provider StoredProcedure intent is translated to Exec text; final SqlCommand CommandType and parameter binding remain unavailable.",
             "No default scanner facts, retained source/SQL indexes or public-synthetic comparison routes are changed. This is not an admitted terminal operation or complete application path."]);
        Require(Encoding.UTF8.GetByteCount(SqlServerSchemaEvidence.Json(report)) <= SqlServerSchemaEvidence.MaxBytes, "report-size-limit");
        return report;
    }

    public static SqlSourceBindingReport Admit(string reportJson, string inputJson, IReadOnlyDictionary<string, string> sources,
        string sqlInputJson, IReadOnlyDictionary<string, string> sqlSources, CancellationToken token = default)
    {
        var expected = Produce(inputJson, sources, sqlInputJson, sqlSources, token);
        var actual = SqlServerSchemaEvidence.Read<SqlSourceBindingReport>(reportJson);
        Require(SqlServerSchemaEvidence.Json(expected) == SqlServerSchemaEvidence.Json(actual), "report-integrity-mismatch");
        return actual;
    }
    private static SqlSourceBindingGap Gap(string kind, string? role, SqlLocalSpan? span) => new(kind, RuleId, EvidenceTiers.Tier4Unknown, role, span);
    private static SqlLocalSpan Span(string path, SyntaxNode node)
    { var line = node.GetLocation().GetLineSpan(); return new(path, line.StartLinePosition.Line + 1, line.EndLinePosition.Line + 1); }
    private static string TypeName(SyntaxNode method) => string.Join('.', method.Ancestors().Reverse().Select(node => node switch
        { BaseNamespaceDeclarationSyntax ns => ns.Name.WithoutTrivia().ToString(), TypeDeclarationSyntax type => type.Identifier.ValueText, _ => null }).Where(name => name is not null));
}
