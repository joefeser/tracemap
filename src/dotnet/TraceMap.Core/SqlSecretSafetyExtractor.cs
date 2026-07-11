using System.Text.RegularExpressions;

namespace TraceMap.Core;

/// <summary>
/// Identifies SQL positions that require protected-material handling. Raw input is
/// used only while classifying and is never copied into a fact or diagnostic.
/// </summary>
public static partial class SqlSecretSafetyExtractor
{
    private const string Limitation = "Static category evidence only; values are intentionally omitted, absence of a finding does not prove absence of secrets, and runtime safety is not established.";
    private const string GapLimitation = "Protected-material handling failed closed because the SQL boundary was dynamic, malformed, or unsupported; owner review is required.";

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
            string text;
            try
            {
                text = File.ReadAllText(Path.Combine(repoPath, file.RelativePath));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                facts.Add(CreateFact(
                    manifest, file.RelativePath, 1, 1, 0,
                    new SqlSecretAssessment("not-established", ["dynamic-secret-boundary"], "failed", RuleIds.DatabaseSqlSecretSafetyGap)));
                continue;
            }

            foreach (var statement in SqlExecutionContextExtractor.SplitStatements(text))
            {
                var assessment = Analyze(statement.Slice(text), statement.StructuralText, statement.LexicallyComplete);
                if (assessment is null)
                {
                    continue;
                }

                facts.Add(CreateFact(
                    manifest,
                    file.RelativePath,
                    statement.StartLine,
                    statement.EndLine,
                    statement.Ordinal,
                    assessment));
            }
        }

        return facts
            .OrderBy(fact => fact.Evidence.FilePath, StringComparer.Ordinal)
            .ThenBy(fact => fact.Evidence.StartLine)
            .ThenBy(fact => fact.FactId, StringComparer.Ordinal)
            .ToArray();
    }

    internal static bool HasProtectedMaterial(string sql)
    {
        return SqlExecutionContextExtractor.SplitStatements(sql)
            .Any(statement => Analyze(statement.Slice(sql), statement.StructuralText, statement.LexicallyComplete) is not null);
    }

    internal static IReadOnlyList<CodeFact> CreateEmbeddedFacts(
        ScanManifest manifest,
        string relativePath,
        int startLine,
        int endLine,
        string sql)
    {
        var facts = new List<CodeFact>();
        foreach (var statement in SqlExecutionContextExtractor.SplitStatements(sql))
        {
            var assessment = Analyze(statement.Slice(sql), statement.StructuralText, statement.LexicallyComplete);
            if (assessment is not null)
            {
                var statementStart = Math.Clamp(startLine + statement.StartLine - 1, startLine, Math.Max(startLine, endLine));
                var statementEnd = Math.Clamp(startLine + statement.EndLine - 1, statementStart, Math.Max(statementStart, endLine));
                facts.Add(CreateFact(manifest, relativePath, statementStart, statementEnd, statement.Ordinal, assessment));
            }
        }

        return facts;
    }

    internal static CodeFact CreateStatementFact(
        ScanManifest manifest,
        string relativePath,
        SqlExecutionContextExtractor.SqlStatement statement,
        SqlSecretAssessment assessment) =>
        CreateFact(manifest, relativePath, statement.StartLine, statement.EndLine, statement.Ordinal, assessment);

    internal static SqlSecretAssessment? Analyze(string raw, string structural, bool lexicallyComplete)
    {
        var categories = new SortedSet<string>(StringComparer.Ordinal);
        var userMapping = UserMappingPattern().IsMatch(structural);
        var subscription = SubscriptionPattern().IsMatch(structural);
        var dblink = DblinkPattern().IsMatch(structural);
        var fdwOptions = ServerOptionsPattern().IsMatch(structural) && CredentialKeyPattern().IsMatch(raw);
        var scheduled = CronPattern().IsMatch(structural);
        var credentialText = CredentialAssignmentPattern().IsMatch(raw);
        var externalReference = ReferencePattern().IsMatch(raw);
        var activeCommentCredential = SqlExecutionContextExtractor.EnumerateActiveLineComments(raw)
            .Any(item => CredentialAssignmentPattern().IsMatch(item.Comment));

        if (userMapping)
        {
            categories.Add("user-mapping");
            categories.Add("credential-option");
        }
        if (subscription)
        {
            categories.Add("subscription-connection");
            categories.Add("connection-material");
        }
        if (dblink)
        {
            categories.Add("remote-query-input");
            categories.Add("connection-material");
        }
        if (fdwOptions)
        {
            categories.Add("credential-option");
            categories.Add("connection-material");
        }
        if (scheduled && (credentialText || externalReference))
        {
            categories.Add("scheduled-command");
        }
        if (activeCommentCredential)
        {
            categories.Add("credential-option");
        }

        var highRiskSurface = userMapping || subscription || dblink || fdwOptions || (scheduled && (credentialText || externalReference));
        if (categories.Count == 0 && !activeCommentCredential)
        {
            return null;
        }

        if (!lexicallyComplete || (highRiskSurface && DynamicPattern().IsMatch(raw)))
        {
            categories.Add("dynamic-secret-boundary");
            return new SqlSecretAssessment("not-established", categories.ToArray(), "reduced", RuleIds.DatabaseSqlSecretSafetyGap);
        }

        if (highRiskSurface && externalReference)
        {
            categories.Add("external-secret-provider");
            return new SqlSecretAssessment("secret-reference", categories.ToArray(), "complete", RuleIds.DatabaseSqlSecretBearingStep);
        }

        if (highRiskSurface && (QuotedValuePattern().IsMatch(raw) || credentialText))
        {
            return new SqlSecretAssessment("secret-bearing", categories.ToArray(), "complete", RuleIds.DatabaseSqlSecretBearingStep);
        }

        return new SqlSecretAssessment("possible-secret", categories.ToArray(), "reduced", RuleIds.DatabaseSqlSecretTextCandidate);
    }

    private static CodeFact CreateFact(
        ScanManifest manifest,
        string relativePath,
        int startLine,
        int endLine,
        int ordinal,
        SqlSecretAssessment assessment)
    {
        var isGap = assessment.RuleId == RuleIds.DatabaseSqlSecretSafetyGap;
        return FactFactory.Create(
            manifest,
            isGap ? FactTypes.AnalysisGap : FactTypes.SecretBearingSqlStep,
            assessment.RuleId,
            isGap ? EvidenceTiers.Tier4Unknown
                : assessment.RuleId == RuleIds.DatabaseSqlSecretTextCandidate
                    ? EvidenceTiers.Tier3SyntaxOrTextual
                    : EvidenceTiers.Tier2Structural,
            new EvidenceSpan(
                relativePath,
                Math.Max(1, startLine),
                Math.Max(Math.Max(1, startLine), endLine),
                null,
                nameof(SqlSecretSafetyExtractor),
                ScannerVersions.SqlSecretSafetyExtractor),
            targetSymbol: ordinal > 0 ? $"sql-step-{ordinal:D4}" : null,
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["categoryCodes"] = string.Join(",", assessment.Categories),
                ["classification"] = assessment.Classification,
                ["coverage"] = assessment.Coverage,
                ["identityPrecision"] = "span-only",
                ["limitation"] = isGap ? GapLimitation : Limitation,
                ["statementOrdinal"] = ordinal.ToString(),
                ["stopCondition"] = "secret-owner-review"
            });
    }

    [GeneratedRegex(@"\b(?:CREATE|ALTER)\s+USER\s+MAPPING\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex UserMappingPattern();

    [GeneratedRegex(@"\b(?:CREATE|ALTER)\s+SUBSCRIPTION\b[\s\S]*?\bCONNECTION\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SubscriptionPattern();

    [GeneratedRegex(@"\b(?:dblink|dblink_exec|dblink_connect|dblink_connect_u)\s*\(", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DblinkPattern();

    [GeneratedRegex(@"\b(?:CREATE|ALTER)\s+SERVER\b[\s\S]*?\bOPTIONS\s*\(", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ServerOptionsPattern();

    [GeneratedRegex(@"\b(?:password|passwd|user|username|host|hostaddr|dbname|database|token|secret|sslkey|sslcert)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CredentialKeyPattern();

    [GeneratedRegex(@"\b(?:password|passwd|token|secret|api[_-]?key|connection(?:string)?)\b\s*(?:=>|:=|=|:|\s)\s*[^\s,;)]+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CredentialAssignmentPattern();

    [GeneratedRegex(@"\bcron\s*\.\s*schedule\s*\(", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CronPattern();

    [GeneratedRegex(@"(?:\$\{|\{\{|\$\(|\b(?:current_setting|vault|secret_manager|getenv|environment)\s*\()", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ReferencePattern();

    [GeneratedRegex(@"(?:\|\||\bformat\s*\(|\bconcat\s*\()", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex DynamicPattern();

    [GeneratedRegex(@"(?:'(?:[^']|'')*'|\$\$[\s\S]*?\$\$|\$([A-Za-z_][A-Za-z0-9_]*)\$[\s\S]*?\$\1\$)", RegexOptions.CultureInvariant)]
    private static partial Regex QuotedValuePattern();
}

internal sealed record SqlSecretAssessment(
    string Classification,
    IReadOnlyList<string> Categories,
    string Coverage,
    string RuleId);
