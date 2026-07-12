using System.Text.Json;
using System.Text.Json.Serialization;
using TraceMap.Core;

namespace TraceMap.Reporting;

public sealed record SqlRunbookPacket(
    string SchemaVersion,
    string Purpose,
    SqlRunbookSource Source,
    SqlRunbookCoverage Coverage,
    IReadOnlyList<SqlRunbookStepGroup> StepGroups,
    IReadOnlyList<SqlRunbookMilestone> Milestones,
    IReadOnlyList<SqlRunbookPrerequisite> Prerequisites,
    IReadOnlyList<SqlRunbookProtectedStep> ProtectedSteps,
    IReadOnlyList<SqlRunbookValidation> ValidationExpectations,
    IReadOnlyList<SqlRunbookCleanup> CleanupEvidence,
    IReadOnlyList<SqlRunbookStopCondition> StopConditions,
    IReadOnlyList<SqlRunbookGap> Gaps,
    IReadOnlyList<SqlRunbookOwnerQuestion> OwnerQuestions,
    IReadOnlyList<string> Limitations);

public sealed record SqlRunbookSource(string Repository, string CommitSha, string ScanId);
public sealed record SqlRunbookCoverage(string Status, string BuildStatus, IReadOnlyList<string> ReducedComponents);
public sealed record SqlRunbookLineSpan(int StartLine, int EndLine);
public sealed record SqlRunbookEvidence(string RuleId, string EvidenceTier, string CommitSha, string FilePath, SqlRunbookLineSpan LineSpan, string ExtractorId, string ExtractorVersion, string Coverage, IReadOnlyList<string> SupportingFactIds, IReadOnlyList<string> Limitations);
public sealed record SqlRunbookStepGroup(string GroupId, string Engine, string ServerRole, string DatabaseRole, string SchemaRole, string ExecutionMode, bool ContextTransition, string Checkpoint, IReadOnlyList<SqlRunbookStep> Steps);
public sealed record SqlRunbookStep(int StatementOrdinal, string StepKind, string ContextClassification, IReadOnlyList<string> StopConditions, SqlRunbookEvidence Evidence);
public sealed record SqlRunbookMilestone(string Kind, string State, string ValidationState, SqlRunbookEvidence Evidence);
public sealed record SqlRunbookPrerequisite(string OperationKind, string Capability, string Status, string ContextRole, SqlRunbookEvidence Evidence);
public sealed record SqlRunbookProtectedStep(int StatementOrdinal, string Classification, IReadOnlyList<string> Categories, string OwnerHandling, SqlRunbookEvidence Evidence);
public sealed record SqlRunbookValidation(int StatementOrdinal, string State, string ObservationState, SqlRunbookEvidence Evidence);
public sealed record SqlRunbookCleanup(int StatementOrdinal, string State, SqlRunbookEvidence Evidence);
public sealed record SqlRunbookStopCondition(string Code, SqlRunbookEvidence Evidence);
public sealed record SqlRunbookGap(string Code, string Category, SqlRunbookEvidence Evidence);
public sealed record SqlRunbookOwnerQuestion(string Question, SqlRunbookEvidence Evidence);

public static class SqlRunbookPacketBuilder
{
    public const string SchemaVersion = "sql-operator-runbook-packet/v2";

    public static SqlRunbookPacket Build(ScanResult result)
    {
        var commitSha = result.Manifest.CommitSha ?? "unknown";
        var sqlFacts = result.Facts.Where(IsSqlFact).ToArray();
        var contexts = sqlFacts
            .Where(f => f.FactType is FactTypes.SqlExecutionContextDeclared or FactTypes.SqlExecutionContextCandidate)
            .Where(f => f.Evidence is not null)
            .GroupBy(f => (SourcePath: ContextSourcePath(f), Ordinal: Value(f, "statementOrdinal", "0")))
            .Select(g => g.OrderBy(f => f.FactType == FactTypes.SqlExecutionContextDeclared ? 0 : 1).ThenBy(f => f.FactId, StringComparer.Ordinal).First())
            .OrderBy(ContextSourcePath, StringComparer.Ordinal)
            .ThenBy(f => Ordinal(f))
            .ThenBy(f => f.FactId, StringComparer.Ordinal)
            .ToArray();

        var groups = new List<SqlRunbookStepGroup>();
        var contextRuns = new List<List<CodeFact>>();
        foreach (var context in contexts)
        {
            if (contextRuns.Count == 0
                || ContextSourcePath(contextRuns[^1][^1]) != ContextSourcePath(context)
                || ContextKey(contextRuns[^1][^1]) != ContextKey(context))
                contextRuns.Add([]);
            contextRuns[^1].Add(context);
        }
        foreach (var run in contextRuns)
        {
            var first = run[0];
            var transition = groups.Count > 0;
            var steps = run.Select(context => new SqlRunbookStep(
                Ordinal(context), Value(context, "stepKind", "unknown-sql-step"), Value(context, "contextClassification", "unknown"),
                Codes(Value(context, "stopConditions", "verify-active-connection")), Evidence(context, commitSha))).ToArray();
            groups.Add(new SqlRunbookStepGroup(
                $"group-{groups.Count + 1:D3}", Value(first, "engineFamily", "unknown"), Value(first, "serverRole", "unknown"),
                Value(first, "databaseRole", "unknown"), Value(first, "schemaRole", "unspecified"), Value(first, "executionMode", "unknown"),
                transition, "independently-verify-active-client-tab-connection-and-database", steps));
        }

        var surfaces = sqlFacts.Where(f => f.FactType == FactTypes.DatabaseLinkSurfaceDeclared && f.Evidence is not null)
            .OrderBy(FactOrder).ToArray();
        var validationFacts = contexts.Where(f => Value(f, "stepKind", "") == "validation-query").ToArray();
        var milestones = surfaces.Select(f => new SqlRunbookMilestone(
                MilestoneKind(Value(f, "surfaceKind", "unknown")), "intended-by-script", "validation-evidence-not-provided", Evidence(f, commitSha)))
            .Concat(sqlFacts.Where(f => f.FactType == FactTypes.DatabasePermissionDeclared && f.Evidence is not null)
                .Select(f => new SqlRunbookMilestone("permission", "intended-by-script", "validation-evidence-not-provided", Evidence(f, commitSha))))
            .Concat(validationFacts.Select(f => new SqlRunbookMilestone("validation", "validation-step-present", "validation-evidence-not-provided", Evidence(f, commitSha))))
            .Concat(contexts.Where(f => Value(f, "stepKind", "") == "destructive-operation")
                .Select(f => new SqlRunbookMilestone("cleanup-or-rollback-candidate", "intended-by-script", "validation-evidence-not-provided", Evidence(f, commitSha))))
            .OrderBy(m => m.Evidence.FilePath, StringComparer.Ordinal).ThenBy(m => m.Evidence.LineSpan.StartLine).ThenBy(m => m.Kind, StringComparer.Ordinal).ToArray();

        var prerequisites = sqlFacts.Where(f => f.FactType == FactTypes.DatabasePrerequisiteEvidence && f.Evidence is not null)
            .OrderBy(FactOrder).Select(f => new SqlRunbookPrerequisite(
                Value(f, "operationKind", "unknown-sql-step"), Value(f, "candidateCapability", "unknown"),
                Value(f, "evidenceStatus", "unknown"), Value(f, "contextRole", "unknown"), Evidence(f, commitSha))).ToArray();
        var protectedSteps = sqlFacts.Where(f => f.FactType == FactTypes.SecretBearingSqlStep && f.Evidence is not null)
            .OrderBy(FactOrder).Select(f => new SqlRunbookProtectedStep(
                Ordinal(f), Value(f, "classification", "not-established"), Codes(Value(f, "categoryCodes", "dynamic-secret-boundary")),
                "route-protected-material-through-owner-approved-process", Evidence(f, commitSha))).ToArray();
        var validations = validationFacts.Select(f => new SqlRunbookValidation(
            Ordinal(f), "validation-step-present", "validation-evidence-not-provided", Evidence(f, commitSha))).ToArray();
        var cleanup = contexts.Where(f => Value(f, "stepKind", "") == "destructive-operation")
            .Select(f => new SqlRunbookCleanup(Ordinal(f), "intended-by-script", Evidence(f, commitSha))).ToArray();
        var gaps = sqlFacts.Where(f => f.FactType == FactTypes.AnalysisGap && f.Evidence is not null)
            .OrderBy(FactOrder).Select(f => new SqlRunbookGap(
                Value(f, "gapKind", "unknown-static-gap"), GapCategory(f.RuleId), Evidence(f, commitSha))).ToArray();

        var multipleSqlFiles = contexts.Select(ContextSourcePath).Where(path => path is not null).Distinct(StringComparer.Ordinal).Skip(1).Any();
        var commitKnown = IsKnownCommit(result.Manifest.CommitSha);
        var derivedSource = contexts.Concat(surfaces).Concat(sqlFacts.Where(f => f.Evidence is not null)).OrderBy(FactOrder).FirstOrDefault();
        var stops = contexts.SelectMany(f => Codes(Value(f, "stopConditions", "verify-active-connection"))
                .Select(code => new SqlRunbookStopCondition(code, Evidence(f, commitSha))))
            .Concat(protectedSteps.Select(step => new SqlRunbookStopCondition("secret-owner-review", step.Evidence)))
            .Concat(gaps.Select(gap => new SqlRunbookStopCondition($"resolve-{gap.Category}-gap", gap.Evidence)))
            .Concat(validationFacts.Length == 0 && surfaces.Length > 0 ? [new SqlRunbookStopCondition("validation-step-not-established", DerivedEvidence(surfaces[0], commitSha))] : [])
            .Concat(multipleSqlFiles && contexts.Length > 0 ? [new SqlRunbookStopCondition("cross-file-order-not-established", DerivedEvidence(contexts[0], commitSha))] : [])
            .Concat(contexts.FirstOrDefault(f => Value(f, "coverage", "reduced") != "complete") is { } reducedContext
                ? [new SqlRunbookStopCondition("partial-context-coverage-review", DerivedEvidence(reducedContext, commitSha))] : [])
            .Concat(!commitKnown && derivedSource is not null ? [new SqlRunbookStopCondition("commit-identity-not-established", DerivedEvidence(derivedSource, commitSha))] : [])
            .GroupBy(stop => stop.Code, StringComparer.Ordinal)
            .Select(group => group.OrderBy(stop => stop.Evidence.FilePath, StringComparer.Ordinal).ThenBy(stop => stop.Evidence.LineSpan.StartLine).First())
            .OrderBy(stop => stop.Code, StringComparer.Ordinal).ToArray();
        var questions = gaps.Select(g => new SqlRunbookOwnerQuestion(Question(g.Category), g.Evidence))
            .Concat(prerequisites.Where(p => p.Status != "present-in-scripts")
                .Select(p => new SqlRunbookOwnerQuestion("Who owns validation of unresolved permission prerequisite candidates?", p.Evidence)))
            .Concat(protectedSteps.Select(p => new SqlRunbookOwnerQuestion("Who owns the approved handling process for protected material?", p.Evidence)))
            .Concat(validationFacts.Length == 0 && surfaces.Length > 0
                ? [new SqlRunbookOwnerQuestion("Who owns independent validation of the intended database changes?", DerivedEvidence(surfaces[0], commitSha))]
                : [])
            .Concat(multipleSqlFiles && contexts.Length > 0
                ? [new SqlRunbookOwnerQuestion("Who owns confirmation of execution order across SQL files?", DerivedEvidence(contexts[0], commitSha))]
                : [])
            .GroupBy(question => question.Question, StringComparer.Ordinal)
            .Select(group => group.OrderBy(question => question.Evidence.FilePath, StringComparer.Ordinal)
                .ThenBy(question => question.Evidence.LineSpan.StartLine).First())
            .OrderBy(question => question.Question, StringComparer.Ordinal).ToArray();
        var reduced = new List<string>();
        if (!string.Equals(result.Manifest.BuildStatus, "Succeeded", StringComparison.OrdinalIgnoreCase)) reduced.Add("build");
        if (!commitKnown) reduced.Add("commit-identity");
        if (gaps.Length > 0) reduced.Add("sql-static-analysis");
        if (contexts.Any(f => Value(f, "coverage", "reduced") != "complete")) reduced.Add("execution-context");
        if (validationFacts.Length == 0 && surfaces.Length > 0) reduced.Add("validation-step-evidence");
        if (multipleSqlFiles) reduced.Add("cross-file-order");

        return new SqlRunbookPacket(
            SchemaVersion, "Static SQL operator handoff evidence; not an execution plan or safety approval.",
            new SqlRunbookSource(result.Manifest.RepoName, commitSha, result.Manifest.ScanId),
            new SqlRunbookCoverage(reduced.Count == 0 ? "complete-static-evidence" : "reduced", result.Manifest.BuildStatus ?? "unknown", reduced),
            groups, milestones, prerequisites, protectedSteps, validations, cleanup, stops, gaps, questions,
            [
                "TraceMap does not execute SQL or connect to a live database.",
                "TraceMap does not observe the active client tab, connection, database, schema, role, or runtime state.",
                "Static permission evidence does not establish effective authorization.",
                "Validation steps do not establish observed validation results.",
                "Cleanup candidates do not establish that rollback is complete or reversible.",
                "This packet does not certify safety, approve changes, or replace DBA/operator judgment."
            ]);
    }

    public static bool HasMeaningfulContent(SqlRunbookPacket packet) => packet.StepGroups.Count > 0
        || packet.Milestones.Count > 0
        || packet.Prerequisites.Count > 0
        || packet.ProtectedSteps.Count > 0
        || packet.ValidationExpectations.Count > 0
        || packet.CleanupEvidence.Count > 0
        || packet.Gaps.Count > 0;

    private static bool IsSqlFact(CodeFact f) => f.RuleId.StartsWith("database.sql.", StringComparison.Ordinal)
        || f.RuleId.StartsWith("database.postgres.", StringComparison.Ordinal);
    private static string ContextKey(CodeFact f) => string.Join('|', Value(f, "engineFamily", "unknown"), Value(f, "serverRole", "unknown"), Value(f, "databaseRole", "unknown"), Value(f, "schemaRole", "unspecified"), Value(f, "executionMode", "unknown"));
    private static string? ContextSourcePath(CodeFact fact)
    {
        var path = fact.Evidence?.FilePath;
        return path?.EndsWith(SqlExecutionContextExtractor.SidecarSuffix, StringComparison.Ordinal) == true
            ? path[..^SqlExecutionContextExtractor.SidecarSuffix.Length]
            : path;
    }
    private static string Value(CodeFact f, string key, string fallback) => f.Properties.GetValueOrDefault(key) ?? fallback;
    private static int Ordinal(CodeFact f) => int.TryParse(Value(f, "statementOrdinal", "0"), out var value) ? value : int.MaxValue;
    private static string FactOrder(CodeFact f) => $"{f.Evidence.FilePath}\u001f{f.Evidence.StartLine:D10}\u001f{f.FactId}";
    private static IReadOnlyList<string> Codes(string value) => value.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Order(StringComparer.Ordinal).ToArray();
    private static SqlRunbookEvidence Evidence(CodeFact f, string commitSha) => new(f.RuleId, f.EvidenceTier, commitSha, CombinedReportHelpers.SafePath(f.Evidence.FilePath), new SqlRunbookLineSpan(f.Evidence.StartLine, f.Evidence.EndLine), f.Evidence.ExtractorId, f.Evidence.ExtractorVersion, Value(f, "coverage", "reduced"), [f.FactId], FactLimitations(f));
    private static SqlRunbookEvidence DerivedEvidence(CodeFact source, string commitSha) => new(RuleIds.DatabaseSqlOperatorRunbookPacket, EvidenceTiers.Tier4Unknown, commitSha, CombinedReportHelpers.SafePath(source.Evidence.FilePath), new SqlRunbookLineSpan(source.Evidence.StartLine, source.Evidence.EndLine), nameof(SqlRunbookPacketBuilder), "sql-runbook-packet/0.1.0", "reduced", [source.FactId], ["Runbook-derived stop and owner-review evidence is bounded by its supporting upstream fact."]);
    private static IReadOnlyList<string> FactLimitations(CodeFact fact) => fact.Properties.TryGetValue("ruleLimitations", out var value) && !string.IsNullOrWhiteSpace(value) ? [value] : [];
    private static bool IsKnownCommit(string? value) => !string.IsNullOrWhiteSpace(value)
        && !value.Equals("unknown", StringComparison.OrdinalIgnoreCase)
        && value.Trim('0').Length > 0;
    private static string MilestoneKind(string kind) => kind switch { "extension" => "extension", "foreign-server" => "foreign-server", "server-grant" => "permission", "user-mapping" => "user-mapping", "schema-import" or "foreign-table" => "schema-import-or-foreign-table", "publication" => "publication", "subscription" => "subscription", "scheduled-operation" => "scheduled-job", _ => "unknown" };
    private static string GapCategory(string rule) => rule switch { RuleIds.DatabaseSqlContextGap => "context", RuleIds.DatabaseSqlSecretSafetyGap => "protected-material", RuleIds.DatabasePostgresPermissionGap => "permission", RuleIds.DatabasePostgresArchiveLinkGap => "archive-link", _ => "coverage" };
    private static string Question(string category) => category switch { "context" => "Who will verify the active categorical context before manual execution?", "protected-material" => "Who owns the approved handling process for protected material?", "permission" => "Who owns validation of unresolved permission prerequisite candidates?", "archive-link" => "Who owns review of incomplete archive-link evidence?", _ => "Who owns resolution of reduced static-analysis coverage?" };
}

public static class SqlRunbookPacketWriter
{
    private const string Empty = "no static evidence found";
    public static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.Never };

    public static async Task WriteAsync(string outputDirectory, SqlRunbookPacket packet, CancellationToken cancellationToken = default)
    {
        await File.WriteAllTextAsync(Path.Combine(outputDirectory, "sql-runbook.json"), JsonSerializer.Serialize(packet, JsonOptions) + Environment.NewLine, cancellationToken);
        await File.WriteAllTextAsync(Path.Combine(outputDirectory, "sql-runbook.md"), RenderMarkdown(packet), cancellationToken);
    }

    public static string RenderMarkdown(SqlRunbookPacket packet)
    {
        var lines = new List<string> { "# SQL Operator Runbook Evidence Packet", "", packet.Purpose, "", $"- Schema: `{packet.SchemaVersion}`", $"- Repository: `{packet.Source.Repository}`", $"- Commit SHA: `{packet.Source.CommitSha}`", $"- Scan ID: `{packet.Source.ScanId}`", $"- Coverage: `{packet.Coverage.Status}`", $"- Build status: `{packet.Coverage.BuildStatus}`", "", "## Ordered Context Groups", "" };
        if (packet.StepGroups.Count == 0) lines.Add($"- {Empty}");
        foreach (var group in packet.StepGroups)
        {
            lines.Add($"### `{group.GroupId}` — `{group.Engine}/{group.ServerRole}/{group.DatabaseRole}/{group.SchemaRole}/{group.ExecutionMode}`");
            lines.Add("");
            if (group.ContextTransition) lines.Add("- Transition checkpoint: independently verify the active client tab, connection, and database.");
            lines.Add($"- Checkpoint: `{group.Checkpoint}`");
            foreach (var step in group.Steps) lines.Add($"- Step `{step.StatementOrdinal}`: `{step.StepKind}`; context `{step.ContextClassification}`; stops `{string.Join(',', step.StopConditions)}`; {EvidenceText(step.Evidence)}");
            lines.Add("");
        }
        Section(lines, "Milestones", packet.Milestones.Select(m => $"`{m.Kind}`: `{m.State}`; observation `{m.ValidationState}`; {EvidenceText(m.Evidence)}"));
        Section(lines, "Prerequisites", packet.Prerequisites.Select(p => $"`{p.OperationKind}` / `{p.Capability}`: `{p.Status}`; context `{p.ContextRole}`; {EvidenceText(p.Evidence)}"));
        Section(lines, "Protected Steps", packet.ProtectedSteps.Select(p => $"Step `{p.StatementOrdinal}`: `{p.Classification}` / `{string.Join(',', p.Categories)}`; `{p.OwnerHandling}`; {EvidenceText(p.Evidence)}"));
        Section(lines, "Validation Expectations", packet.ValidationExpectations.Select(v => $"Step `{v.StatementOrdinal}`: `{v.State}`; observation `{v.ObservationState}`; {EvidenceText(v.Evidence)}"));
        Section(lines, "Cleanup / Rollback Evidence", packet.CleanupEvidence.Select(c => $"Step `{c.StatementOrdinal}`: `{c.State}` candidate only; {EvidenceText(c.Evidence)}"));
        Section(lines, "Stop Conditions", packet.StopConditions.Select(stop => $"`{stop.Code}`; {EvidenceText(stop.Evidence)}"));
        Section(lines, "Gaps", packet.Gaps.Select(g => $"`{g.Category}` / `{g.Code}`; {EvidenceText(g.Evidence)}"));
        Section(lines, "Owner Questions", packet.OwnerQuestions.Select(question => $"{question.Question} {EvidenceText(question.Evidence)}"));
        Section(lines, "Limitations", packet.Limitations);
        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }

    private static void Section(List<string> lines, string title, IEnumerable<string> values)
    {
        lines.Add($"## {title}"); lines.Add("");
        var rows = values.ToArray();
        lines.AddRange(rows.Length == 0 ? [$"- {Empty}"] : rows.Select(value => $"- {value}"));
        lines.Add("");
    }
    private static string EvidenceText(SqlRunbookEvidence e) => $"rule `{e.RuleId}`, tier `{e.EvidenceTier}`, commit `{e.CommitSha}`, coverage `{e.Coverage}`, `{e.FilePath}:{e.LineSpan.StartLine}-{e.LineSpan.EndLine}`, extractor `{e.ExtractorId}@{e.ExtractorVersion}`";
}
