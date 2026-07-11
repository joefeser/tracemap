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

            var fileFactStart = facts.Count;
            var (sidecarDeclarations, sidecarGaps) = LoadSidecar(repoPath, file.RelativePath, manifest);
            facts.AddRange(sidecarGaps);
            var directives = ParseDirectives(text, file.RelativePath, manifest, facts);

            var previousEndLine = 0;
            foreach (var statement in statements)
            {
                var rawStatement = statement.Slice(text);
                var secretAssessment = SqlSecretSafetyExtractor.Analyze(
                    rawStatement,
                    statement.StructuralText,
                    statement.LexicallyComplete);
                if (secretAssessment is not null)
                {
                    facts.Add(SqlSecretSafetyExtractor.CreateStatementFact(
                        manifest,
                        file.RelativePath,
                        statement,
                        secretAssessment));
                }
                var inferred = Classify(statement.StructuralText);
                var matchingDirectives = directives
                    .Where(item => item.Line > previousEndLine && item.Line == statement.StartLine - 1)
                    .OrderBy(item => item.Line)
                    .ToArray();
                previousEndLine = statement.EndLine;

                ContextDirective? matchingDirective = matchingDirectives.LastOrDefault();
                ContextDeclaration? directive = matchingDirective?.Declaration;
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
                    matchingDirective = null;
                    directive = null;
                }

                sidecarDeclarations.TryGetValue(statement.Ordinal, out var sidecar);
                if (sidecar is not null && directive is not null && DeclarationLayersConflict(sidecar, directive))
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
                    inferred.IsRecognized ? "inferred" : "unknown",
                    protectedMaterial: secretAssessment is not null));

                var explicitDeclaration = sidecar ?? directive;
                var declaration = explicitDeclaration is null
                    ? null
                    : MergeDeclarations(MergeDeclarations(inferred, directive), sidecar);
                var declarationConflictsSyntax = (directive is not null && ExplicitFieldsConflict(directive, inferred))
                    || (sidecar is not null && ExplicitFieldsConflict(sidecar, inferred));
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
                        declarationConflictsSyntax ? "conflicting" : "declared",
                        matchingDirective is not null && sidecar is null ? matchingDirective.Line : null,
                        sidecar?.EvidencePath,
                        sidecar?.EvidenceStartLine,
                        sidecar?.EvidenceEndLine,
                        protectedMaterial: secretAssessment is not null));

                    if (declarationConflictsSyntax)
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

            facts.AddRange(PostgresArchiveLinkExtractor.ExtractFileSurfaces(
                manifest,
                file.RelativePath,
                statements,
                facts.Skip(fileFactStart).ToArray()));
            facts.AddRange(PostgresPermissionEvidenceExtractor.ExtractFilePermissions(
                manifest,
                file.RelativePath,
                statements,
                facts.Skip(fileFactStart).ToArray()));
        }

        facts.AddRange(PostgresArchiveLinkExtractor.Reduce(manifest, facts));
        facts.AddRange(PostgresPermissionEvidenceExtractor.Reduce(manifest, facts));

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
        string classification,
        int? declarationLine = null,
        string? declarationPath = null,
        int? declarationStartLine = null,
        int? declarationEndLine = null,
        bool protectedMaterial = false)
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
            ["stepKind"] = context.StepKind
        };
        if (protectedMaterial)
        {
            properties["identityPrecision"] = "span-only";
            properties["redactionReason"] = "protected-sql-material";
        }
        else
        {
            properties["statementShapeHash"] = FactFactory.Hash(statement.StructuralText, 32);
        }
        AddJoined(properties, "requiredCapabilities", context.RequiredCapabilities);
        AddJoined(properties, "stopConditions", context.StopConditions);

        var stepId = $"sql-step-{statement.Ordinal:D4}";
        return FactFactory.Create(
            manifest,
            factType,
            ruleId,
            tier,
            new EvidenceSpan(
                declarationPath ?? relativePath,
                declarationStartLine ?? declarationLine ?? statement.StartLine,
                declarationEndLine ?? declarationLine ?? statement.EndLine,
                protectedMaterial || declarationPath is not null ? null : FactFactory.Hash(statement.StructuralText, 32),
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
            var content = File.ReadAllText(fullPath);
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || HasUnknownProperties(root, SidecarRootKeys)
                || !root.TryGetProperty("schemaVersion", out var version)
                || version.ValueKind != JsonValueKind.String
                || version.GetString() != ContractVersion
                || !root.TryGetProperty("steps", out var steps)
                || steps.ValueKind != JsonValueKind.Array)
            {
                gaps.Add(CreateGap(manifest, relativePath, 1, CountLines(content), 0, "invalid-context-sidecar", "reduced"));
                return (declarations, gaps);
            }

            var lineCount = CountLines(content);
            foreach (var step in steps.EnumerateArray())
            {
                if (!TryParseSidecarDeclaration(step, out var ordinal, out var declaration)
                    || !declarations.TryAdd(ordinal, declaration! with
                    {
                        EvidencePath = relativePath,
                        EvidenceStartLine = 1,
                        EvidenceEndLine = lineCount
                    }))
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

        var declaredFields = step.EnumerateObject()
            .Select(property => property.Name)
            .Where(name => name != "statementOrdinal")
            .ToHashSet(StringComparer.Ordinal);
        declaration = new ContextDeclaration(engine, server, database, schema, mode, stepKind, capabilities, stops, true, declaredFields);
        return true;
    }

    private static IReadOnlyList<ContextDirective> ParseDirectives(
        string text,
        string relativePath,
        ScanManifest manifest,
        ICollection<CodeFact> facts)
    {
        var result = new List<ContextDirective>();
        foreach (var (line, comment) in EnumerateActiveLineComments(text))
        {
            var match = DirectiveRegex().Match(comment);
            if (!match.Success)
            {
                continue;
            }

            if (TryParseDirective(match.Groups["body"].Value, out var declaration))
            {
                result.Add(new ContextDirective(line, declaration!));
            }
            else
            {
                facts.Add(CreateGap(manifest, relativePath, line, line, 0, "invalid-context-directive", "reduced"));
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

        var declaredFields = values.Keys
            .Select(CanonicalDirectiveField)
            .ToHashSet(StringComparer.Ordinal);
        declaration = new ContextDeclaration(engine, server, database, schema, mode, stepKind, capabilities, stops, true, declaredFields);
        return true;
    }

    private static ContextDeclaration Classify(string structuralText)
    {
        var normalized = structuralText.ToUpperInvariant();
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

        return new ContextDeclaration("unknown", "unknown", "unknown", "unspecified", "unknown", "unknown-sql-step", [], ["owner-review"], false, AllContextFields());
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
            true,
            AllContextFields());
    }

    internal static IReadOnlyList<SqlStatement> SplitStatements(string text)
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

            AddStatement(result, text, startIndex, index + 1 - startIndex, startLine, line, lexicallyComplete: true);
            startIndex = index + 1;
            startLine = line;
        }

        if (startIndex < text.Length)
        {
            AddStatement(result, text, startIndex, text.Length - startIndex, startLine, line, lexicallyComplete: state == LexState.Normal);
        }

        return result;
    }

    private static void AddStatement(
        ICollection<SqlStatement> statements,
        string text,
        int rawStart,
        int rawLength,
        int segmentStartLine,
        int segmentEndLine,
        bool lexicallyComplete)
    {
        var raw = text.Substring(rawStart, rawLength);
        var structural = StructuralText(raw);
        if (string.IsNullOrWhiteSpace(structural))
        {
            return;
        }

        var tokenStartLine = FindFirstSqlTokenLine(raw, segmentStartLine);
        statements.Add(new SqlStatement(
            statements.Count + 1,
            tokenStartLine,
            Math.Max(tokenStartLine, segmentEndLine),
            CollapseWhitespace(structural),
            rawStart,
            rawLength,
            lexicallyComplete));
    }

    private static int FindFirstSqlTokenLine(string text, int segmentStartLine)
    {
        var line = Math.Max(1, segmentStartLine);
        var index = 0;
        while (index < text.Length)
        {
            if (char.IsWhiteSpace(text[index]))
            {
                if (text[index] == '\n') line++;
                index++;
                continue;
            }

            if (index + 1 < text.Length && text[index] == '-' && text[index + 1] == '-')
            {
                index += 2;
                while (index < text.Length && text[index] != '\n') index++;
                continue;
            }

            if (index + 1 < text.Length && text[index] == '/' && text[index + 1] == '*')
            {
                index += 2;
                while (index < text.Length)
                {
                    if (text[index] == '\n') line++;
                    if (index + 1 < text.Length && text[index] == '*' && text[index + 1] == '/')
                    {
                        index += 2;
                        break;
                    }
                    index++;
                }
                continue;
            }

            return line;
        }

        return line;
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
                if (current == '\n')
                {
                    builder.Append('\n');
                }
                if (current == '*' && next == '/')
                {
                    state = LexState.Normal;
                    builder.Append(' ');
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
                builder.Append(' ');
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

        var inner = text.AsSpan(index + 1, end - index - 1);
        if (inner.Length > 0 && (!char.IsLetter(inner[0]) && inner[0] != '_'))
        {
            return false;
        }
        foreach (var ch in inner)
        {
            if (!char.IsLetterOrDigit(ch) && ch != '_')
            {
                return false;
            }
        }

        tag = text[index..(end + 1)];
        return true;
    }

    private static bool ExplicitFieldsConflict(ContextDeclaration declaration, ContextDeclaration other)
    {
        return FieldConflicts(declaration, other, "engineFamily", declaration.EngineFamily, other.EngineFamily)
            || FieldConflicts(declaration, other, "serverRole", declaration.ServerRole, other.ServerRole)
            || FieldConflicts(declaration, other, "databaseRole", declaration.DatabaseRole, other.DatabaseRole)
            || FieldConflicts(declaration, other, "schemaRole", declaration.SchemaRole, other.SchemaRole)
            || FieldConflicts(declaration, other, "executionMode", declaration.ExecutionMode, other.ExecutionMode)
            || FieldConflicts(declaration, other, "stepKind", declaration.StepKind, other.StepKind);
    }

    private static bool DeclarationLayersConflict(ContextDeclaration declaration, ContextDeclaration other)
    {
        return ExplicitFieldsConflict(declaration, other)
            || CollectionFieldConflicts(declaration, other, "requiredCapabilities", declaration.RequiredCapabilities, other.RequiredCapabilities)
            || CollectionFieldConflicts(declaration, other, "stopConditions", declaration.StopConditions, other.StopConditions);
    }

    private static bool FieldConflicts(
        ContextDeclaration declaration,
        ContextDeclaration other,
        string field,
        string left,
        string right)
    {
        return declaration.DeclaredFields.Contains(field)
            && other.DeclaredFields.Contains(field)
            && left is not "unknown" and not "unspecified" and not "unknown-sql-step"
            && right is not "unknown" and not "unspecified"
            && right is not "unknown-sql-step"
            && !string.Equals(left, right, StringComparison.Ordinal);
    }

    private static bool CollectionFieldConflicts(
        ContextDeclaration declaration,
        ContextDeclaration other,
        string field,
        IReadOnlyList<string> left,
        IReadOnlyList<string> right)
    {
        return declaration.DeclaredFields.Contains(field)
            && other.DeclaredFields.Contains(field)
            && !left.OrderBy(value => value, StringComparer.Ordinal)
                .SequenceEqual(right.OrderBy(value => value, StringComparer.Ordinal), StringComparer.Ordinal);
    }

    private static ContextDeclaration MergeDeclarations(ContextDeclaration source, ContextDeclaration? overlay)
    {
        if (overlay is null)
        {
            return source;
        }

        return new ContextDeclaration(
            Overlay("engineFamily", source.EngineFamily, overlay),
            Overlay("serverRole", source.ServerRole, overlay),
            Overlay("databaseRole", source.DatabaseRole, overlay),
            Overlay("schemaRole", source.SchemaRole, overlay),
            Overlay("executionMode", source.ExecutionMode, overlay),
            Overlay("stepKind", source.StepKind, overlay),
            overlay.DeclaredFields.Contains("requiredCapabilities") ? overlay.RequiredCapabilities : source.RequiredCapabilities,
            overlay.DeclaredFields.Contains("stopConditions") ? overlay.StopConditions : source.StopConditions,
            source.IsRecognized || overlay.IsRecognized,
            source.DeclaredFields.Concat(overlay.DeclaredFields).ToHashSet(StringComparer.Ordinal),
            overlay.EvidencePath ?? source.EvidencePath,
            overlay.EvidenceStartLine ?? source.EvidenceStartLine,
            overlay.EvidenceEndLine ?? source.EvidenceEndLine);
    }

    private static string Overlay(string field, string source, ContextDeclaration overlay)
    {
        if (!overlay.DeclaredFields.Contains(field))
        {
            return source;
        }

        return field switch
        {
            "engineFamily" => overlay.EngineFamily,
            "serverRole" => overlay.ServerRole,
            "databaseRole" => overlay.DatabaseRole,
            "schemaRole" => overlay.SchemaRole,
            "executionMode" => overlay.ExecutionMode,
            "stepKind" => overlay.StepKind,
            _ => source
        };
    }

    private static IReadOnlySet<string> AllContextFields()
    {
        return new HashSet<string>(
            ["engineFamily", "serverRole", "databaseRole", "schemaRole", "executionMode", "stepKind", "requiredCapabilities", "stopConditions"],
            StringComparer.Ordinal);
    }

    private static string CanonicalDirectiveField(string field)
    {
        return field switch
        {
            "engine" => "engineFamily",
            "server" => "serverRole",
            "database" => "databaseRole",
            "schema" => "schemaRole",
            "mode" => "executionMode",
            "step" => "stepKind",
            "capabilities" => "requiredCapabilities",
            "stops" => "stopConditions",
            _ => field
        };
    }

    internal static IReadOnlyList<(int Line, string Comment)> EnumerateActiveLineComments(string text)
    {
        var comments = new List<(int Line, string Comment)>();
        var state = LexState.Normal;
        string? dollarTag = null;
        var line = 1;
        for (var index = 0; index < text.Length; index++)
        {
            var current = text[index];
            var next = index + 1 < text.Length ? text[index + 1] : '\0';
            if (current == '\n')
            {
                line++;
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

            if (current == '/' && next == '*')
            {
                state = LexState.BlockComment;
                index++;
            }
            else if (current == '\'')
            {
                state = LexState.SingleQuote;
            }
            else if (current == '"')
            {
                state = LexState.DoubleQuote;
            }
            else if (current == '$' && TryReadDollarTag(text, index, out var tag))
            {
                state = LexState.DollarQuote;
                dollarTag = tag;
                index += tag.Length - 1;
            }
            else if (current == '-' && next == '-')
            {
                var end = text.IndexOf('\n', index + 2);
                if (end < 0)
                {
                    end = text.Length;
                }
                comments.Add((line, text[index..end].TrimEnd('\r')));
                index = end - 1;
            }
        }

        return comments;
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

    internal sealed record SqlStatement(
        int Ordinal,
        int StartLine,
        int EndLine,
        string StructuralText,
        int RawStart,
        int RawLength,
        bool LexicallyComplete)
    {
        internal string Slice(string text) => text.Substring(RawStart, RawLength);
    }
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
        bool IsRecognized,
        IReadOnlySet<string> DeclaredFields,
        string? EvidencePath = null,
        int? EvidenceStartLine = null,
        int? EvidenceEndLine = null);

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
