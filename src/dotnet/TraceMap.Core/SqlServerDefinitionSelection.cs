using System.Text;
using System.Text.RegularExpressions;
using System.Diagnostics.CodeAnalysis;
using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace TraceMap.Core;

public sealed record SqlDefinitionTemplateSettings(string DatabaseOwner, string ObjectQualifier, string ValuesOrigin);
public sealed record SqlDefinitionRange(string SourcePath, int StartLine, int EndLine, string Kind, string Name);
public sealed record SqlDefinitionSelectionInput(string Version, string Visibility, string ContextRepository, string ContextCommitSha,
    string DefinitionSet, string Server, string Database, IReadOnlyList<SqlServerDdlFile> Files,
    IReadOnlyList<SqlDefinitionRange> Selections, SqlDefinitionTemplateSettings Template,
    SqlServerInputProvenance? Provenance = null);
public sealed record SqlDefinitionProjection(string ProjectedPath, string ProjectedSha256, SqlLocalSpan OriginalSpan,
    string Kind, string Schema, string Name, int OwnerReplacements, int QualifierReplacements);
public sealed record SqlDefinitionSelectionEvidence(string Version, string GeneratorSha256, string BoundedInputSha256,
    string ParserVersion, string ParserSha256, string RuleId, SqlDefinitionSelectionInput Input,
    IReadOnlyList<SqlDefinitionProjection> Projections);

/// <summary>Explicit whole-batch definition selection, not an install-script interpreter or deployment witness.</summary>
public static partial class SqlServerDefinitionSelection
{
    public const string InputVersion = "sqlserver-local-definition-selection-input/1";
    public const string ExportVersion = "sqlserver-local-selected-definitions/1";
    public const string RuleId = "database.sqlserver.selected-definitions.v1";
    public const int MaxFiles = 8;
    public const int MaxFileBytes = 4_194_304;
    public const int MaxTotalBytes = 16_777_216;
    private const string OwnerToken = "{databaseOwner}";
    private const string QualifierToken = "{objectQualifier}";

    [GeneratedRegex(@"^[\t ]*GO[\t ]*(?:--[^\r\n]*)?\r?$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex GoLine();
    [GeneratedRegex(@"\{[A-Za-z_][A-Za-z0-9_]*\}", RegexOptions.CultureInvariant)]
    private static partial Regex UnresolvedTemplate();
    private static void Require([DoesNotReturnIf(false)] bool condition, string reason)
    { if (!condition) throw new ArgumentException("sql-selection-" + reason); }

    public static SqlDefinitionSelectionInput ReadInput(string json)
    {
        var input = SqlServerSchemaEvidence.Read<SqlDefinitionSelectionInput>(json);
        Require(input.Version == InputVersion && input.Visibility == "local-only", "input-contract-invalid");
        Require(input.Template is not null && SqlServerSchemaEvidence.Identifier(input.Template.DatabaseOwner)
            && input.Template.ObjectQualifier is not null && input.Template.ObjectQualifier.Length <= 64
            && (input.Template.ObjectQualifier.Length == 0 || SqlServerSchemaEvidence.Identifier(input.Template.ObjectQualifier))
            && input.Template.ValuesOrigin is "review-scenario" or "owner-supplied", "explicit-template-settings-required");
        Require(input.Files is { Count: > 0 and <= MaxFiles }, "original-file-limit");
        // Reuse context validation without weakening the v1 file extension or size contract.
        var context = new SqlLocalInput(SqlServerLocalDefinitions.InputVersion, input.Visibility, input.ContextRepository,
            input.ContextCommitSha, input.DefinitionSet, input.Server, input.Database,
            [new("context.sql", new('0', 64))], input.Provenance);
        SqlServerLocalDefinitions.ReadInput(SqlServerSchemaEvidence.Json(context));
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in input.Files!)
            Require(file is not null && SqlServerSchemaEvidence.PathSafe(file.Path)
                && SqlServerSchemaEvidence.Sha(file.Sha256) && paths.Add(file.Path), "original-file-invalid-or-duplicate");
        Require(input.Selections is { Count: > 0 and <= SqlServerDdlEvidence.MaxFiles }, "selection-limit");
        foreach (var range in input.Selections!)
            Require(range is not null && input.Files.Any(file => file.Path == range.SourcePath)
                && range.StartLine > 0 && range.EndLine >= range.StartLine && range.EndLine <= 200_000
                && range.Kind is "table" or "procedure" or "view" or "function"
                && SqlServerSchemaEvidence.Identifier(range.Name)
                && input.Template!.ObjectQualifier.Length + range.Name.Length <= 128, "selection-invalid");
        Require(input.Files!.All(file => input.Selections!.Any(range => range.SourcePath == file.Path)), "unselected-original-file");
        foreach (var group in input.Selections!.GroupBy(range => range.SourcePath, StringComparer.Ordinal))
        {
            var previous = 0;
            foreach (var range in group.OrderBy(range => range.StartLine))
            { Require(range.StartLine > previous, "overlapping-selections"); previous = range.EndLine; }
        }
        return input;
    }

    public static SqlLocalExport Produce(string inputJson, IReadOnlyDictionary<string, string> sources, CancellationToken token = default)
    {
        var input = ReadInput(inputJson);
        Require(sources.Keys.Order(StringComparer.Ordinal).SequenceEqual(input.Files.Select(file => file.Path).Order(StringComparer.Ordinal)), "source-set-mismatch");
        var selected = new Dictionary<string, string>(StringComparer.Ordinal);
        var projections = new Dictionary<string, SqlDefinitionProjection>(StringComparer.Ordinal);
        var total = 0;
        foreach (var file in input.Files)
        {
            token.ThrowIfCancellationRequested();
            Require(sources.TryGetValue(file.Path, out var original) && original is not null, "source-missing");
            var originalBytes = new UTF8Encoding(false, true).GetBytes(original!);
            total += originalBytes.Length;
            Require(originalBytes.Length <= MaxFileBytes && total <= MaxTotalBytes, "original-size-limit");
            Require(SqlServerSchemaEvidence.Hash(originalBytes) == file.Sha256, "original-digest-mismatch");
            var lines = original!.Split('\n');
            Require(lines.Length <= 200_000, "original-line-limit");
            BoundCommentNesting(original, token);
            // Only lex the larger original, never parse/execute its omitted installation statements.
            var parser = new TSql160Parser(initialQuotedIdentifiers: true);
            var lexText = original.StartsWith('\uFEFF') ? original[1..] : original;
            var (tokens, errors) = SqlServerDdlEvidence.OnParserStack(() =>
            {
                using var reader = new StringReader(lexText);
                var stream = parser.GetTokenStream(reader, out var lexErrors);
                return (stream, lexErrors);
            });
            Require(errors.Count == 0 && tokens.Count <= 1_000_000, "original-lexical-limit-or-error");
            var boundaries = tokens.Where(item => item.TokenType == TSqlTokenType.Go
                && item.Line <= lines.Length && GoLine().IsMatch(lines[item.Line - 1])).Select(item => item.Line).ToHashSet();
            foreach (var range in input.Selections.Where(range => range.SourcePath == file.Path))
            {
                token.ThrowIfCancellationRequested();
                Require(range.EndLine <= lines.Length && (range.StartLine == 1 || boundaries.Contains(range.StartLine - 1))
                    && (range.EndLine == lines.Length || boundaries.Contains(range.EndLine)), "whole-batch-boundaries-required");
                // A batch delimiter inside a multiline token must never become a real boundary.
                var contentTokens = tokens.Where(item => item.TokenType is not (TSqlTokenType.WhiteSpace or TSqlTokenType.EndOfFile));
                Require(!contentTokens.Any(item => item.Line < range.StartLine && EndLine(item) >= range.StartLine)
                    && !contentTokens.Any(item => item.Line <= range.EndLine && EndLine(item) > range.EndLine), "selection-splits-token");
                foreach (var item in tokens.Where(item => item.Line >= range.StartLine && item.Line <= range.EndLine))
                {
                    var type = item.TokenType.ToString();
                    if (type.Contains("String", StringComparison.Ordinal) || type.Contains("Comment", StringComparison.Ordinal))
                        Require(!item.Text.Contains(OwnerToken, StringComparison.Ordinal) && !item.Text.Contains(QualifierToken, StringComparison.Ordinal), "template-in-literal-or-comment-unsupported");
                    if (type.Contains("Identifier", StringComparison.Ordinal))
                        Require(!item.Text.Contains(OwnerToken, StringComparison.Ordinal), "owner-token-inside-identifier-unsupported");
                }
                var text = string.Join('\n', lines[(range.StartLine - 1)..range.EndLine]);
                // Split/join retains each selected line's CR. Include the original terminating LF.
                if (range.EndLine < lines.Length) text += "\n";
                Require(Encoding.UTF8.GetByteCount(text) <= SqlServerDdlEvidence.MaxFileBytes, "selected-file-size-limit");
                var owners = Count(text, OwnerToken); var qualifiers = Count(text, QualifierToken);
                text = text.Replace(OwnerToken, "[" + input.Template.DatabaseOwner + "].", StringComparison.Ordinal)
                    .Replace(QualifierToken, input.Template.ObjectQualifier, StringComparison.Ordinal);
                Require(!UnresolvedTemplate().IsMatch(text), "unresolved-template");
                var path = "selection-" + Array.IndexOf(input.Selections.ToArray(), range).ToString("D3", System.Globalization.CultureInfo.InvariantCulture) + ".sql";
                selected.Add(path, text);
                projections.Add(path, new(path, SqlServerSchemaEvidence.Hash(Encoding.UTF8.GetBytes(text)),
                    new(file.Path, range.StartLine, range.EndLine), range.Kind, input.Template.DatabaseOwner,
                    input.Template.ObjectQualifier + range.Name, owners, qualifiers));
            }
        }
        var normalized = new SqlLocalInput(SqlServerLocalDefinitions.InputVersion, "local-only", input.ContextRepository,
            input.ContextCommitSha, input.DefinitionSet, input.Server, input.Database,
            selected.Select(pair => new SqlServerDdlFile(pair.Key, projections[pair.Key].ProjectedSha256)).ToArray(),
            new(SqlServerSchemaEvidence.GeneratorSha256, SqlServerSchemaEvidence.Digest(input)));
        // Existing selected parser bounds still apply after expansion (256 KiB/file, 1 MiB total).
        var basic = SqlServerLocalDefinitions.Produce(SqlServerSchemaEvidence.Json(normalized), selected, token);
        Require(basic.Objects.Count == input.Selections.Count, "one-definition-per-selection-required");
        foreach (var projection in projections.Values)
        {
            var objects = basic.Objects.Where(item => item.Span.FilePath == projection.ProjectedPath).ToArray();
            Require(objects.Length == 1 && objects[0].Kind == projection.Kind && objects[0].Schema == projection.Schema
                && objects[0].Name == projection.Name, "selected-identity-mismatch");
        }
        SqlLocalSpan Map(SqlLocalSpan span)
        {
            var origin = projections[span.FilePath].OriginalSpan;
            return new(origin.FilePath, origin.StartLine + span.StartLine - 1, origin.StartLine + span.EndLine - 1);
        }
        var objectsMapped = basic.Objects.Select(item =>
        {
            var offset = projections[item.Span.FilePath].OriginalSpan.StartLine - 1;
            var span = Map(item.Span);
            return item with
            {
                Span = span,
                Parameters = item.Parameters.Select(parameter => parameter with { Span = Map(parameter.Span) }).ToArray(),
                Table = item.Table is not { } table ? null : table with
                {
                    FilePath = span.FilePath, StartLine = table.StartLine + offset, EndLine = table.EndLine + offset,
                    Columns = table.Columns.Select(column => column with { Line = column.Line + offset }).ToArray()
                },
                ResultShape = item.ResultShape is not { } shape ? null : shape with
                {
                    FilePath = span.FilePath, StartLine = shape.StartLine + offset, EndLine = shape.EndLine + offset,
                    Completeness = "unknown", UnknownReason = "selected-script-context-not-modeled", Columns = []
                }
            };
        }).ToArray();
        var evidence = new SqlDefinitionSelectionEvidence("sqlserver-definition-selection/1", SqlServerSchemaEvidence.GeneratorSha256,
            SqlServerSchemaEvidence.Digest(input), SqlServerDdlEvidence.ParserVersion, SqlServerDdlEvidence.ParserDigest.Value,
            RuleId, input, projections.Values.OrderBy(item => item.ProjectedPath, StringComparer.Ordinal).ToArray());
        var export = basic with
        {
            Version = ExportVersion, ExtractorVersion = "sqlserver-local-selected-definitions/1", RuleId = RuleId,
            BoundedInputSha256 = SqlServerSchemaEvidence.Digest(input), Objects = objectsMapped,
            Dependencies = basic.Dependencies.Select(reference => reference with { Span = Map(reference.Span) }).ToArray(),
            Gaps = basic.Gaps.Select(gap => gap with { Span = Map(gap.Span) }).Concat(objectsMapped.Select(item =>
                new SqlLocalGap("selection-omits-script-and-session-context", RuleId, EvidenceTiers.Tier4Unknown, item.Span))).ToArray(),
            Selection = evidence,
            Limitations = basic.Limitations.Concat(new[] {
                "Explicit original whole-batch selection only; omitted statements, session settings, migrations and execution order are not interpreted.",
                "Owner/qualifier values are declared review-scenario or owner-supplied settings, never proof of deployment. No substitution defaults.",
                "Selected procedure result shapes remain unknown because whole-script/session state is unmodeled. Table columns describe selected declarations only.",
                "Projected SQL exists only in memory; output stores original/projected hashes, selection settings and original source line mappings, not SQL bodies."
            }).ToArray()
        };
        Require(Encoding.UTF8.GetByteCount(SqlServerSchemaEvidence.Json(export)) <= SqlServerSchemaEvidence.MaxBytes, "export-size-limit");
        return export;
    }

    public static SqlLocalExport Admit(string exportJson, string inputJson, IReadOnlyDictionary<string, string> sources,
        CancellationToken token = default)
    {
        var expected = Produce(inputJson, sources, token);
        var actual = SqlServerSchemaEvidence.Read<SqlLocalExport>(exportJson);
        Require(SqlServerSchemaEvidence.Json(expected) == SqlServerSchemaEvidence.Json(actual), "export-integrity-mismatch");
        return actual;
    }
    private static int EndLine(TSqlParserToken token) => token.Line + token.Text.Count(character => character == '\n');
    private static int Count(string text, string value) => (text.Length - text.Replace(value, "", StringComparison.Ordinal).Length) / value.Length;

    private enum LexicalState { Normal, SingleQuote, DoubleQuote, Bracket, LineComment, BlockComment }

    // ScriptDom's lexer recursively handles nested comments. Bound depth before entering
    // it on larger originals; this guard does not replace its tokenization or SQL parser.
    private static void BoundCommentNesting(string text, CancellationToken token)
    {
        var state = LexicalState.Normal;
        var depth = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if ((i & 4095) == 0) token.ThrowIfCancellationRequested();
            var c = text[i]; var next = i + 1 < text.Length ? text[i + 1] : '\0';
            Require(c != '\r' || next == '\n', "original-line-ending-unsupported");
            switch (state)
            {
                case LexicalState.Normal:
                    if (c == '\'') state = LexicalState.SingleQuote;
                    else if (c == '"') state = LexicalState.DoubleQuote;
                    else if (c == '[') state = LexicalState.Bracket;
                    else if (c == '-' && next == '-') { state = LexicalState.LineComment; i++; }
                    else if (c == '/' && next == '*') { state = LexicalState.BlockComment; depth = 1; i++; }
                    break;
                case LexicalState.SingleQuote:
                    if (c == '\'') { if (next == '\'') i++; else state = LexicalState.Normal; }
                    break;
                case LexicalState.DoubleQuote:
                    if (c == '"') { if (next == '"') i++; else state = LexicalState.Normal; }
                    break;
                case LexicalState.Bracket:
                    if (c == ']') { if (next == ']') i++; else state = LexicalState.Normal; }
                    break;
                case LexicalState.LineComment:
                    if (c is '\r' or '\n') state = LexicalState.Normal;
                    break;
                case LexicalState.BlockComment:
                    if (c == '/' && next == '*') { Require(++depth <= 128, "original-comment-depth-limit"); i++; }
                    else if (c == '*' && next == '/') { if (--depth == 0) state = LexicalState.Normal; i++; }
                    break;
            }
        }
    }
}
