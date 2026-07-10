using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TraceMap.Core;

public static partial class SqlExecutionContextExtractor
{
    public const string ContractVersion = "sql-execution-context/v1";
    public const string SidecarSuffix = ".tracemap-sql-context.json";

    private const string StaticLimitation = "Static intended-context evidence only; active connection, database state, authorization, execution, and safety are not proven.";
    private const string GapLimitation = "SQL execution context could not be established from supported checked-in evidence; the active connection and runtime state remain unknown.";

    private static readonly HashSet<string> EngineFamilies = new(StringComparer.Ordinal)
    {
        "postgresql", "unknown"
    };

    private static readonly HashSet<string> ServerRoles = new(StringComparer.Ordinal)
    {
        "source", "archive-target", "admin", "unknown"
    };

    private static readonly HashSet<string> DatabaseRoles = new(StringComparer.Ordinal)
    {
        "source-data", "archive-data", "admin", "validation-only", "unknown"
    };

    private static readonly HashSet<string> SchemaRoles = new(StringComparer.Ordinal)
    {
        "application", "archive", "extension", "unspecified", "unknown"
    };

    private static readonly HashSet<string> ExecutionModes = new(StringComparer.Ordinal)
    {
        "manual", "scheduled", "validation-only", "unknown"
    };

    private static readonly HashSet<string> StepKinds = new(StringComparer.Ordinal)
    {
        "extension-setup",
        "fdw-server-setup",
        "user-mapping",
        "schema-import",
        "foreign-table-setup",
        "grant-permission",
        "publication-setup",
        "subscription-setup",
        "scheduled-job",
        "validation-query",
        "destructive-operation",
        "unknown-sql-step"
    };

    private static readonly HashSet<string> CapabilityCodes = new(StringComparer.Ordinal)
    {
        "create-extension",
        "create-server",
        "create-user-mapping",
        "import-schema",
        "create-foreign-table",
        "grant-permission",
        "create-publication",
        "create-subscription",
        "schedule-job",
        "validate-state",
        "destructive-operation-review"
    };

    private static readonly HashSet<string> StopConditionCodes = new(StringComparer.Ordinal)
    {
        "verify-active-connection",
        "verify-database-context",
        "verify-server-role",
        "owner-review",
        "secret-owner-review"
    };

    private static readonly HashSet<string> DirectiveKeys = new(StringComparer.Ordinal)
    {
        "engine", "server", "database", "schema", "mode", "step", "capabilities", "stops"
    };

    private static readonly HashSet<string> SidecarRootKeys = new(StringComparer.Ordinal)
    {
        "schemaVersion", "steps"
    };

    private static readonly HashSet<string> SidecarStepKeys = new(StringComparer.Ordinal)
    {
        "statementOrdinal", "engineFamily", "serverRole", "databaseRole", "schemaRole",
        "executionMode", "stepKind", "requiredCapabilities", "stopConditions"
    };

    public static IReadOnlyList<CodeFact> Extract(
        string repoPath,
        ScanManifest manifest,
        IEnumerable<FileInventoryItem> inventory)
    {
        var facts = new List<CodeFact>();
        foreach (var file in inventory
            .Where(item => item.Kind == "Sql")
            .OrderBy(item => item.RelativePath, StringComparer.Ordinal))
        {
            var fullPath = Path.Combine(repoPath, file.RelativePath);
            string text;
            try
            {
                text = File.ReadAllText(fullPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                facts.Add(CreateGap(
                    manifest,
                    file.RelativePath,
                    1,
                    1,
                    0,
                    "sql-file-read-failed",
                    "failed"));
                continue;
            }

            var statements = SplitStatements(text);
            if (statements.Count == 0)
            {
                continue;
            }

            var (sidecarDeclarations, sidecarGaps) = LoadSidecar(repoPath, file.RelativePath, manifest);
            facts.AddRange(sidecarGaps);
            var directives = ParseDirectives(text, file.RelativePath, manifest, facts);

            var previousEndLine = 0;
            foreach (var statement in statements)
            {
                var inferred = Classify(statement.StructuralText);
                var matchingDirectives = directives
                    .Where(item => item.Line > previousEndLine && item.Line <= statement.StartLine)
                    .OrderBy(item => item.Line)
                    .ToArray();
                previousEndLine = statement.EndLine;

                ContextDeclaration? directive = matchingDirectives.LastOrDefault()?.Declaration;
                if (matchingDirectives.Length > 1)
                {
                    facts.Add(CreateGap(
                        manifest,
                        file.RelativePath,
                        matchingDirectives[0].Line,
                        matchingDirectives[^1].Line,
                        statement.Ordinal,
                        "multiple-context-directives",
                        "reduced"));
                    directive = null;
                }

                sidecarDeclarations.TryGetValue(statement.Ordinal, out var sidecar);
                var declaration = sidecar ?? directive;
                if (sidecar is not null && directive is not null && Conflicts(sidecar, directive))
                {
                    facts.Add(CreateGap(
                        manifest,
                        file.RelativePath,
                        statement.StartLine,
                        statement.EndLine,
                        statement.Ordinal,
                        "conflicting-declarations",
                        "reduced"));
                }

                facts.Add(CreateContextFact(
                    manifest,
                    FactTypes.SqlExecutionContextCandidate,
                    RuleIds.DatabaseSqlContextSyntax,
                    inferred.IsRecognized ? EvidenceTiers.Tier2Structural : EvidenceTiers.Tier3SyntaxOrTextual,
                    file.RelativePath,
                    statement,
                    inferred,
                    "syntax",
                    inferred.IsRecognized ? "inferred" : "unknown"));

                if (declaration is not null)
                {
                    facts.Add(CreateContextFact(
                        manifest,
                        FactTypes.SqlExecutionContextDeclared,
                        RuleIds.DatabaseSqlContextDeclaration,
                        EvidenceTiers.Tier2Structural,
                        file.RelativePath,
                        statement,
                        declaration,
                        sidecar is not null ? "sidecar" : "directive",
                        Conflicts(declaration, inferred) ? "conflicting" : "declared"));

                    if (Conflicts(declaration, inferred))
                    {
                        facts.Add(CreateGap(
                            manifest,
                            file.RelativePath,
                            statement.StartLine,
                            statement.EndLine,
                            statement.Ordinal,
                            "declared-syntax-context-conflict",
                            "reduced"));
                    }
                }

                var effective = declaration ?? inferred;
                if (!inferred.IsRecognized)
                {
                    facts.Add(CreateGap(
                        manifest,
                        file.RelativePath,
                        statement.StartLine,
                        statement.EndLine,
                        statement.Ordinal,
                        "unknown-sql-step",
                        "reduced"));
                }

                if (effective.EngineFamily == "unknown"
                    || (RequiresConcreteContext(effective.StepKind)
                        && (effective.ServerRole == "unknown" || effective.DatabaseRole == "unknown")))
                {
                    facts.Add(CreateGap(
                        manifest,
                        file.RelativePath,
                        statement.StartLine,
                        statement.EndLine,
                        statement.Ordinal,
                        "missing-context-evidence",
                        "reduced"));
                }
            }
        }

        return facts
            .OrderBy(fact => fact.Evidence.FilePath, StringComparer.Ordinal)
            .ThenBy(fact => fact.Evidence.StartLine)
            .ThenBy(fact => fact.FactType, StringComparer.Ordinal)
            .ThenBy(fact => fact.FactId, StringComparer.Ordinal)
            .ToArray();
    }

    private static CodeFact CreateContextFact(
        ScanManifest manifest,
        string factType,
        string ruleId,
        string tier,
        string relativePath,
        SqlStatement statement,
        ContextDeclaration context,
        string declarationSource,
        string classification)
    {
        var properties = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["contextClassification"] = classification,
            ["coverage"] = classification is "conflicting" or "unknown"
                || context.EngineFamily == "unknown"
                || (RequiresConcreteContext(context.StepKind)
                    && (context.ServerRole == "unknown" || context.DatabaseRole == "unknown"))
                ? "reduced"
                : "complete",
            ["databaseRole"] = context.DatabaseRole,
            ["declarationSource"] = declarationSource,
            ["engineFamily"] = context.EngineFamily,
            ["executionMode"] = context.ExecutionMode,
            ["limitation"] = StaticLimitation,
            ["schemaRole"] = context.SchemaRole,
            ["serverRole"] = context.ServerRole,
            ["statementOrdinal"] = statement.Ordinal.ToString(),
            ["statementShapeHash"] = FactFactory.Hash(statement.StructuralText, 32),
            ["stepKind"] = context.StepKind
        };
        AddJoined(properties, "requiredCapabilities", context.RequiredCapabilities);
        AddJoined(properties, "stopConditions", context.StopConditions);

        var stepId = $"sql-step-{statement.Ordinal:D4}";
        return FactFactory.Create(
            manifest,
            factType,
            ruleId,
            tier,
            new EvidenceSpan(
                relativePath,
                statement.StartLine,
                statement.EndLine,
                FactFactory.Hash(statement.StructuralText, 32),
                nameof(SqlExecutionContextExtractor),
                ScannerVersions.SqlExecutionContextExtractor),
            targetSymbol: stepId,
            contractElement: context.StepKind,
            properties: properties);
    }

    private static CodeFact CreateGap(
        ScanManifest manifest,
        string relativePath,
        int startLine,
        int endLine,
        int statementOrdinal,
        string gapKind,
        string coverage)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.AnalysisGap,
            RuleIds.DatabaseSqlContextGap,
            EvidenceTiers.Tier4Unknown,
            new EvidenceSpan(
                relativePath,
                Math.Max(1, startLine),
                Math.Max(Math.Max(1, startLine), endLine),
                null,
                nameof(SqlExecutionContextExtractor),
                ScannerVersions.SqlExecutionContextExtractor),
            targetSymbol: statementOrdinal > 0 ? $"sql-step-{statementOrdinal:D4}" : null,
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["contextClassification"] = gapKind.Contains("conflict", StringComparison.Ordinal) ? "conflicting" : "missing-evidence",
                ["coverage"] = coverage,
                ["gapKind"] = gapKind,
                ["limitation"] = GapLimitation,
                ["statementOrdinal"] = statementOrdinal.ToString()
            });
    }

    private static (IReadOnlyDictionary<int, ContextDeclaration> Declarations, IReadOnlyList<CodeFact> Gaps) LoadSidecar(
        string repoPath,
        string sqlRelativePath,
        ScanManifest manifest)
    {
        var relativePath = sqlRelativePath + SidecarSuffix;
        var fullPath = Path.Combine(repoPath, relativePath);
        if (!File.Exists(fullPath))
        {
            return (new Dictionary<int, ContextDeclaration>(), []);
        }

        var declarations = new Dictionary<int, ContextDeclaration>();
        var gaps = new List<CodeFact>();
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(fullPath));
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || HasUnknownProperties(root, SidecarRootKeys)
                || !root.TryGetProperty("schemaVersion", out var version)
                || version.ValueKind != JsonValueKind.String
                || version.GetString() != ContractVersion
                || !root.TryGetProperty("steps", out var steps)
                || steps.ValueKind != JsonValueKind.Array)
            {
                gaps.Add(CreateGap(manifest, relativePath, 1, CountLines(File.ReadAllText(fullPath)), 0, "invalid-context-sidecar", "reduced"));
                return (declarations, gaps);
            }

            foreach (var step in steps.EnumerateArray())
            {
                if (!TryParseSidecarDeclaration(step, out var ordinal, out var declaration)
                    || !declarations.TryAdd(ordinal, declaration!))
                {
                    gaps.Add(CreateGap(manifest, relativePath, 1, 1, Math.Max(0, ordinal), "invalid-context-sidecar-step", "reduced"));
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            gaps.Add(CreateGap(manifest, relativePath, 1, 1, 0, "context-sidecar-read-or-parse-failed", "failed"));
        }

        return (declarations, gaps);
    }

    private static bool TryParseSidecarDeclaration(JsonElement step, out int ordinal, out ContextDeclaration? declaration)
    {
        ordinal = 0;
        declaration = null;
        if (step.ValueKind != JsonValueKind.Object
            || HasUnknownProperties(step, SidecarStepKeys)
            || !step.TryGetProperty("statementOrdinal", out var ordinalElement)
            || !ordinalElement.TryGetInt32(out ordinal)
            || ordinal <= 0)
        {
            return false;
        }

        var engine = GetString(step, "engineFamily", "unknown");
        var server = GetString(step, "serverRole", "unknown");
        var database = GetString(step, "databaseRole", "unknown");
        var schema = GetString(step, "schemaRole", "unspecified");
        var mode = GetString(step, "executionMode", "unknown");
        var stepKind = GetString(step, "stepKind", "unknown-sql-step");
        var capabilities = GetStringArray(step, "requiredCapabilities");
        var stops = GetStringArray(step, "stopConditions");
        if (engine is null || server is null || database is null || schema is null || mode is null || stepKind is null
            || capabilities is null || stops is null
            || !IsValidContext(engine, server, database, schema, mode, stepKind, capabilities, stops))
        {
            return false;
        }

        declaration = new ContextDeclaration(engine, server, database, schema, mode, stepKind, capabilities, stops, true);
        return true;
    }

    private static IReadOnlyList<ContextDirective> ParseDirectives(
        string text,
        string relativePath,
        ScanManifest manifest,
        ICollection<CodeFact> facts)
    {
        var result = new List<ContextDirective>();
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        for (var index = 0; index < lines.Length; index++)
        {
            var match = DirectiveRegex().Match(lines[index]);
            if (!match.Success)
            {
                continue;
            }

            if (TryParseDirective(match.Groups["body"].Value, out var declaration))
            {
                result.Add(new ContextDirective(index + 1, declaration!));
            }
            else
            {
                facts.Add(CreateGap(manifest, relativePath, index + 1, index + 1, 0, "invalid-context-directive", "reduced"));
            }
        }

        return result;
    }

    private static bool TryParseDirective(string body, out ContextDeclaration? declaration)
    {
        declaration = null;
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var token in body.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = token.IndexOf('=');
            if (separator <= 0 || separator == token.Length - 1)
            {
                return false;
            }

            var key = token[..separator];
            var value = token[(separator + 1)..];
            if (!DirectiveKeys.Contains(key) || !values.TryAdd(key, value))
            {
                return false;
            }
        }

        var engine = values.GetValueOrDefault("engine") ?? "unknown";
        var server = values.GetValueOrDefault("server") ?? "unknown";
        var database = values.GetValueOrDefault("database") ?? "unknown";
        var schema = values.GetValueOrDefault("schema") ?? "unspecified";
        var mode = values.GetValueOrDefault("mode") ?? "unknown";
        var stepKind = values.GetValueOrDefault("step") ?? "unknown-sql-step";
        var capabilities = SplitCodes(values.GetValueOrDefault("capabilities"));
        var stops = SplitCodes(values.GetValueOrDefault("stops"));
        if (!IsValidContext(engine, server, database, schema, mode, stepKind, capabilities, stops))
        {
            return false;
        }

        declaration = new ContextDeclaration(engine, server, database, schema, mode, stepKind, capabilities, stops, true);
        return true;
    }

    private static ContextDeclaration Classify(string structuralText)
    {
        var normalized = CollapseWhitespace(structuralText).ToUpperInvariant();
        if (normalized.StartsWith("CREATE EXTENSION ", StringComparison.Ordinal))
        {
            return Context("admin", "admin", "extension", "manual", "extension-setup", ["create-extension"]);
        }

        if (normalized.StartsWith("CREATE SERVER ", StringComparison.Ordinal))
        {
            return Context("unknown", "unknown", "unspecified", "manual", "fdw-server-setup", ["create-server"]);
        }

        if (normalized.StartsWith("CREATE USER MAPPING ", StringComparison.Ordinal))
        {
            return Context("unknown", "unknown", "unspecified", "manual", "user-mapping", ["create-user-mapping"], ["secret-owner-review"]);
        }

        if (normalized.StartsWith("IMPORT FOREIGN SCHEMA ", StringComparison.Ordinal))
        {
            return Context("unknown", "unknown", "archive", "manual", "schema-import", ["import-schema"]);
        }

        if (normalized.StartsWith("CREATE FOREIGN TABLE ", StringComparison.Ordinal))
        {
            return Context("unknown", "unknown", "archive", "manual", "foreign-table-setup", ["create-foreign-table"]);
        }

        if (normalized.StartsWith("GRANT ", StringComparison.Ordinal)
            || normalized.StartsWith("REVOKE ", StringComparison.Ordinal)
            || normalized.StartsWith("ALTER DEFAULT PRIVILEGES ", StringComparison.Ordinal)
            || normalized.StartsWith("ALTER ROLE ", StringComparison.Ordinal))
        {
            return Context("unknown", "unknown", "unspecified", "manual", "grant-permission", ["grant-permission"]);
        }

        if (normalized.StartsWith("CREATE PUBLICATION ", StringComparison.Ordinal))
        {
            return Context("source", "source-data", "application", "manual", "publication-setup", ["create-publication"]);
        }

        if (normalized.StartsWith("CREATE SUBSCRIPTION ", StringComparison.Ordinal))
        {
            return Context("archive-target", "archive-data", "archive", "manual", "subscription-setup", ["create-subscription"], ["secret-owner-review"]);
        }

        if (normalized.StartsWith("SELECT CRON.SCHEDULE", StringComparison.Ordinal)
            || normalized.StartsWith("SELECT CRON.SCHEDULE_IN_DATABASE", StringComparison.Ordinal)
            || normalized.StartsWith("SELECT CRON.UNSCHEDULE", StringComparison.Ordinal))
        {
            return Context("unknown", "unknown", "unspecified", "scheduled", "scheduled-job", ["schedule-job"]);
        }

        if (normalized.StartsWith("SELECT ", StringComparison.Ordinal)
            || normalized.StartsWith("SHOW ", StringComparison.Ordinal)
            || normalized.StartsWith("EXPLAIN ", StringComparison.Ordinal))
        {
            return Context("unknown", "unknown", "unspecified", "validation-only", "validation-query", ["validate-state"]);
        }

        if (normalized.StartsWith("DROP ", StringComparison.Ordinal)
            || normalized.StartsWith("TRUNCATE ", StringComparison.Ordinal)
            || normalized.StartsWith("DELETE ", StringComparison.Ordinal))
        {
            return Context("unknown", "unknown", "unspecified", "manual", "destructive-operation", ["destructive-operation-review"], ["owner-review"]);
        }

        return new ContextDeclaration("unknown", "unknown", "unknown", "unspecified", "unknown", "unknown-sql-step", [], ["owner-review"], false);
    }

    private static ContextDeclaration Context(
        string server,
        string database,
        string schema,
        string mode,
        string stepKind,
        IReadOnlyList<string> capabilities,
        IReadOnlyList<string>? stops = null)
    {
        var stopConditions = new HashSet<string>(stops ?? [], StringComparer.Ordinal)
        {
            "verify-active-connection"
        };
        if (server == "unknown")
        {
            stopConditions.Add("verify-server-role");
        }
        if (database == "unknown")
        {
            stopConditions.Add("verify-database-context");
        }

        return new ContextDeclaration(
            "postgresql",
            server,
            database,
            schema,
            mode,
            stepKind,
            capabilities,
            stopConditions.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            true);
    }

    private static IReadOnlyList<SqlStatement> SplitStatements(string text)
    {
        var result = new List<SqlStatement>();
        var startIndex = 0;
        var startLine = 1;
        var line = 1;
        var state = LexState.Normal;
        string? dollarTag = null;

        for (var index = 0; index < text.Length; index++)
        {
            var current = text[index];
            var next = index + 1 < text.Length ? text[index + 1] : '\0';
            if (current == '\n')
            {
                line++;
            }

            switch (state)
            {
                case LexState.LineComment:
                    if (current == '\n') state = LexState.Normal;
                    continue;
                case LexState.BlockComment:
                    if (current == '*' && next == '/')
                    {
                        state = LexState.Normal;
                        index++;
                    }
                    continue;
                case LexState.SingleQuote:
                    if (current == '\'' && next == '\'')
                    {
                        index++;
                    }
                    else if (current == '\'')
                    {
                        state = LexState.Normal;
                    }
                    continue;
                case LexState.DoubleQuote:
                    if (current == '"' && next == '"')
                    {
                        index++;
                    }
                    else if (current == '"')
                    {
                        state = LexState.Normal;
                    }
                    continue;
                case LexState.DollarQuote:
                    if (dollarTag is not null && text.AsSpan(index).StartsWith(dollarTag, StringComparison.Ordinal))
                    {
                        index += dollarTag.Length - 1;
                        state = LexState.Normal;
                        dollarTag = null;
                    }
                    continue;
            }

            if (current == '-' && next == '-')
            {
                state = LexState.LineComment;
                index++;
                continue;
            }
            if (current == '/' && next == '*')
            {
                state = LexState.BlockComment;
                index++;
                continue;
            }
            if (current == '\'')
            {
                state = LexState.SingleQuote;
                continue;
            }
            if (current == '"')
            {
                state = LexState.DoubleQuote;
                continue;
            }
            if (current == '$' && TryReadDollarTag(text, index, out var tag))
            {
                state = LexState.DollarQuote;
                dollarTag = tag;
                index += tag.Length - 1;
                continue;
            }
            if (current != ';')
            {
                continue;
            }

            AddStatement(result, text[startIndex..(index + 1)], startLine, line);
            startIndex = index + 1;
            startLine = line;
        }

        if (startIndex < text.Length)
        {
            AddStatement(result, text[startIndex..], startLine, line);
        }

        return result;
    }

    private static void AddStatement(ICollection<SqlStatement> statements, string raw, int segmentStartLine, int segmentEndLine)
    {
        var structural = StructuralText(raw);
        if (string.IsNullOrWhiteSpace(structural))
        {
            return;
        }

        var leadingLines = raw.TakeWhile(char.IsWhiteSpace).Count(ch => ch == '\n');
        statements.Add(new SqlStatement(
            statements.Count + 1,
            Math.Max(1, segmentStartLine + leadingLines),
            Math.Max(segmentStartLine + leadingLines, segmentEndLine),
            CollapseWhitespace(structural)));
    }

    private static string StructuralText(string text)
    {
        var builder = new StringBuilder(text.Length);
        var state = LexState.Normal;
        string? dollarTag = null;
        for (var index = 0; index < text.Length; index++)
        {
            var current = text[index];
            var next = index + 1 < text.Length ? text[index + 1] : '\0';
            if (state == LexState.LineComment)
            {
                if (current == '\n')
                {
                    state = LexState.Normal;
                    builder.Append('\n');
                }
                continue;
            }
            if (state == LexState.BlockComment)
            {
                if (current == '*' && next == '/')
                {
                    state = LexState.Normal;
                    index++;
                }
                continue;
            }
            if (state == LexState.SingleQuote)
            {
                if (current == '\'' && next == '\'') index++;
                else if (current == '\'') state = LexState.Normal;
                continue;
            }
            if (state == LexState.DoubleQuote)
            {
                if (current == '"' && next == '"') index++;
                else if (current == '"') state = LexState.Normal;
                continue;
            }
            if (state == LexState.DollarQuote)
            {
                if (dollarTag is not null && text.AsSpan(index).StartsWith(dollarTag, StringComparison.Ordinal))
                {
                    index += dollarTag.Length - 1;
                    state = LexState.Normal;
                    dollarTag = null;
                }
                continue;
            }

            if (current == '-' && next == '-')
            {
                state = LexState.LineComment;
                index++;
            }
            else if (current == '/' && next == '*')
            {
                state = LexState.BlockComment;
                index++;
            }
            else if (current == '\'')
            {
                state = LexState.SingleQuote;
                builder.Append(" ? ");
            }
            else if (current == '"')
            {
                state = LexState.DoubleQuote;
                builder.Append(" ? ");
            }
            else if (current == '$' && TryReadDollarTag(text, index, out var tag))
            {
                state = LexState.DollarQuote;
                dollarTag = tag;
                builder.Append(" ? ");
                index += tag.Length - 1;
            }
            else
            {
                builder.Append(current);
            }
        }

        return builder.ToString();
    }

    private static bool TryReadDollarTag(string text, int index, out string tag)
    {
        tag = string.Empty;
        var end = text.IndexOf('$', index + 1);
        if (end < 0 || end - index > 64)
        {
            return false;
        }

        var inner = text[(index + 1)..end];
        if (inner.Length > 0 && (!char.IsLetter(inner[0]) && inner[0] != '_'))
        {
            return false;
        }
        if (inner.Any(ch => !char.IsLetterOrDigit(ch) && ch != '_'))
        {
            return false;
        }

        tag = text[index..(end + 1)];
        return true;
    }

    private static bool Conflicts(ContextDeclaration left, ContextDeclaration right)
    {
        return Conflicts(left.EngineFamily, right.EngineFamily)
            || Conflicts(left.ServerRole, right.ServerRole)
            || Conflicts(left.DatabaseRole, right.DatabaseRole)
            || Conflicts(left.SchemaRole, right.SchemaRole)
            || Conflicts(left.ExecutionMode, right.ExecutionMode)
            || (left.StepKind != "unknown-sql-step" && right.StepKind != "unknown-sql-step" && left.StepKind != right.StepKind);
    }

    private static bool Conflicts(string left, string right)
    {
        return left is not "unknown" and not "unspecified"
            && right is not "unknown" and not "unspecified"
            && !string.Equals(left, right, StringComparison.Ordinal);
    }

    private static bool RequiresConcreteContext(string stepKind)
    {
        return stepKind is not "validation-query" and not "unknown-sql-step";
    }

    private static bool IsValidContext(
        string engine,
        string server,
        string database,
        string schema,
        string mode,
        string stepKind,
        IReadOnlyList<string> capabilities,
        IReadOnlyList<string> stops)
    {
        return EngineFamilies.Contains(engine)
            && ServerRoles.Contains(server)
            && DatabaseRoles.Contains(database)
            && SchemaRoles.Contains(schema)
            && ExecutionModes.Contains(mode)
            && StepKinds.Contains(stepKind)
            && capabilities.All(CapabilityCodes.Contains)
            && stops.All(StopConditionCodes.Contains);
    }

    private static bool HasUnknownProperties(JsonElement element, ISet<string> allowed)
    {
        return element.EnumerateObject().Any(property => !allowed.Contains(property.Name));
    }

    private static string? GetString(JsonElement element, string propertyName, string defaultValue)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return defaultValue;
        }

        return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    private static IReadOnlyList<string>? GetStringArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
        {
            return [];
        }
        if (value.ValueKind != JsonValueKind.Array || value.EnumerateArray().Any(item => item.ValueKind != JsonValueKind.String))
        {
            return null;
        }

        return value.EnumerateArray()
            .Select(item => item.GetString()!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> SplitCodes(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToArray();
    }

    private static void AddJoined(IDictionary<string, string> properties, string key, IReadOnlyList<string> values)
    {
        if (values.Count > 0)
        {
            properties[key] = string.Join(';', values.OrderBy(value => value, StringComparer.Ordinal));
        }
    }

    private static string CollapseWhitespace(string value)
    {
        return WhitespaceRegex().Replace(value, " ").Trim().TrimEnd(';').Trim();
    }

    private static int CountLines(string text)
    {
        return text.Length == 0 ? 1 : text.Count(ch => ch == '\n') + 1;
    }

    [GeneratedRegex(@"^\s*--\s*tracemap-sql-context:\s*(?<body>.*)$", RegexOptions.CultureInvariant)]
    private static partial Regex DirectiveRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();

    private sealed record SqlStatement(int Ordinal, int StartLine, int EndLine, string StructuralText);
    private sealed record ContextDirective(int Line, ContextDeclaration Declaration);
    private sealed record ContextDeclaration(
        string EngineFamily,
        string ServerRole,
        string DatabaseRole,
        string SchemaRole,
        string ExecutionMode,
        string StepKind,
        IReadOnlyList<string> RequiredCapabilities,
        IReadOnlyList<string> StopConditions,
        bool IsRecognized);

    private enum LexState
    {
        Normal,
        SingleQuote,
        DoubleQuote,
        DollarQuote,
        LineComment,
        BlockComment
    }
}
