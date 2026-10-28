using System.Globalization;
using System.Text;

namespace TraceMap.Core;

public sealed record SqlServerLifecycleExecutionBinding(string ExecutionId, string CommandText, SqlServerObjectIdentity Identity);
public sealed record SqlServerLifecycleBinding(string FactId, string EvidenceSha256,
    string ConsumerTable, string ColumnName, string ReadBody, string ReadBinary, long ReadOffset, int ReadInvocation,
    IReadOnlyList<SqlServerLifecycleExecutionBinding> Executions);
public sealed record SqlServerLifecycleBindings(string Version, string Repo, string Revision,
    IReadOnlyList<SqlServerLifecycleBinding> Candidates);
public sealed record SqlServerLifecycleComparison(string FactId, string Guard, string Table, string ColumnHash,
    string Status, IReadOnlyList<string> Reasons, IReadOnlyList<string> SchemaExecutions,
    string ValueOrigin, string? ValueExecution, string ValueTable, string ValueColumnHash, bool ApplicationAssignment,
    string SourceRule, string SourceTier);
public sealed record SqlServerLifecycleSite(string Body, string Binary, long Offset, int Invocation);
public sealed record SqlServerLifecycleTableState(string Allocation, bool RowsCleared, string? Execution,
    IReadOnlyList<string> SchemaExecutions, IReadOnlyList<string> ApplicationWrittenColumns);
public sealed record SqlServerLifecycleObjectState(string Allocation, string Kind, IReadOnlyList<string> Slots,
    IReadOnlyDictionary<string, string> Fields);
public sealed record SqlServerLifecycleFailure(string FactId, string Guard, string Operation,
    SqlServerLifecycleSite Site, SqlServerLifecycleSite CatchHandler,
    IReadOnlyList<SqlServerLifecycleTableState> Before, IReadOnlyList<SqlServerLifecycleTableState> NormalCompletion,
    bool MayPartiallyMutate, bool MayHaveCompletedEarlierRows,
    IReadOnlyList<SqlServerLifecycleObjectState> BeforeObjects,
    IReadOnlyList<SqlServerLifecycleObjectState> NormalCompletionObjects, string Limitation);
public sealed record SqlServerLifecycleReport(string Version, string GeneratorSha256, string BoundedInputSha256,
    string SchemaGeneratorSha256, string SchemaInputSha256, string Repo, string Revision,
    string Coverage, IReadOnlyList<SqlServerLifecycleComparison> Comparisons,
    IReadOnlyList<SqlServerLifecycleFailure> PotentialFailures, IReadOnlyList<string> Limitations)
{
    public string RuleId => SqlServerCompiledLifecycleEvidence.RuleId;
    public string EvidenceTier => EvidenceTiers.Tier3SyntaxOrTextual;
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? SchemaOrigin { get; init; }
}

/// <summary>Offline encoded-lifecycle comparison with separately admitted synthetic or owner-declared first-result shapes.</summary>
public static class SqlServerCompiledLifecycleEvidence
{
    public const string RuleId = "database.sqlserver.compiled-lifecycle-comparison.v1";
    private sealed record BodyLink(string Body, string Binary, int Invocation, string BodyFactId);
    private static void Require([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool condition, string reason)
    { if (!condition) throw new ArgumentException("compiled-lifecycle-" + reason); }
    private static string Property(CodeFact fact, string name)
        => fact.Properties.TryGetValue(name, out var value) ? value : throw new ArgumentException("compiled-lifecycle-missing-" + name);

    private static object Projection(CodeFact fact) => new
    {
        fact.FactId, fact.Repo, fact.CommitSha, fact.RuleId, fact.EvidenceTier, fact.TargetSymbol,
        fact.Evidence.ExtractorId, fact.Evidence.ExtractorVersion,
        schema = Property(fact, "sqlLifecycleSchema"), guard = Property(fact, "guard"),
        root = Property(fact, "rootBodyFactId"), lifecycle = Property(fact, "lifecycle"),
        links = Property(fact, "bodyLinks"), generator = Property(fact, "ilGeneratorSha256"),
        input = Property(fact, "ilBoundedInputSha256")
    };
    public static string EvidenceDigest(CodeFact fact) => SqlServerSchemaEvidence.Digest(Projection(fact));
    private static string Literal(string value)
        => "str:" + value.Length.ToString(CultureInfo.InvariantCulture) + ":" + SqlServerSchemaEvidence.Hash(Encoding.Unicode.GetBytes(value));

    public static SqlServerLifecycleReport Compare(SqlServerResultSchemaExport schema, SqlServerLifecycleBindings bindings,
        IReadOnlyList<CodeFact> facts, string repo, string revision)
    {
        schema = SqlServerResultEvidence.Admit(SqlServerSchemaEvidence.Json(schema),
            SqlServerSchemaEvidence.Json(schema.Declarations), repo, revision);
        return CompareAdmitted(schema, bindings, facts, repo, revision);
    }

    public static SqlServerLifecycleReport CompareOwnerDeclared(SqlServerResultSchemaExport schema,
        SqlServerLifecycleBindings bindings, IReadOnlyList<CodeFact> facts, string repo, string revision)
    {
        schema = SqlServerOwnerResultEvidence.Admit(SqlServerSchemaEvidence.Json(schema),
            SqlServerSchemaEvidence.Json(schema.Declarations), repo, revision);
        var report = CompareAdmitted(schema, bindings, facts, repo, revision);
        return report with
        {
            Version = "sqlserver-owner-compiled-lifecycle-comparison/1",
            SchemaOrigin = SqlServerOwnerResultEvidence.Origin,
            Limitations = [.. report.Limitations, SqlServerOwnerResultEvidence.Limitation,
                "Column matches and mismatches are conditional on the supplied declaration. No exhaustive coverage claim."]
        };
    }

    private static SqlServerLifecycleReport CompareAdmitted(SqlServerResultSchemaExport schema,
        SqlServerLifecycleBindings bindings, IReadOnlyList<CodeFact> facts, string repo, string revision)
    {
        Require(bindings.Version == "sqlserver-compiled-lifecycle-bindings/1" && bindings.Repo == repo
            && bindings.Revision == revision && bindings.Candidates is { Count: > 0 and <= 256 }, "binding-envelope");
        Require(facts.Count <= 1_000_000 && facts.All(f => f is not null), "fact-limit");
        Require(facts.Select(f => f.FactId).Distinct(StringComparer.Ordinal).Count() == facts.Count, "duplicate-fact");
        var byId = facts.ToDictionary(f => f.FactId, StringComparer.Ordinal);
        var selected = new List<CodeFact>();
        var rows = new List<SqlServerLifecycleComparison>();
        var failures = new List<SqlServerLifecycleFailure>();
        var unique = new HashSet<string>(StringComparer.Ordinal);
        foreach (var binding in bindings.Candidates)
        {
            Require(binding is not null && unique.Add(binding.FactId) && byId.ContainsKey(binding.FactId), "missing-or-duplicate-candidate");
            var fact = byId[binding.FactId];
            Require(fact.Repo == repo && fact.CommitSha == revision && fact.FactType == FactTypes.ManagedIlSqlLifecycleCandidate
                && fact.RuleId == RuleIds.DotNetIlSqlLifecycle && fact.EvidenceTier == EvidenceTiers.Tier3SyntaxOrTextual
                && fact.Evidence.ExtractorVersion == ScannerVersions.IlBodyEvidenceExtractor
                && Property(fact, "sqlLifecycleSchema") == IlSqlLifecycleExtractor.Schema
                && Property(fact, "ilGeneratorSha256") == SqlServerSchemaEvidence.GeneratorSha256
                && SqlServerSchemaEvidence.Sha(Property(fact, "ilBoundedInputSha256")), "unsupported-or-stale-candidate");
            Require(EvidenceDigest(fact) == binding.EvidenceSha256, "stale-or-substituted-binding");
            var state = SqlServerSchemaEvidence.Read<IlSqlLifecycleExtractor.Result>(Property(fact, "lifecycle"));
            Require(state.Gaps is { Count: 0 } && state.Returned is not null && state.Executions is { Count: > 0 and <= 8 }
                && state.Bodies is { Count: > 0 and <= 8192 } && state.Reads is { Count: > 0 and <= 8192 }, "unavailable-state");
            Require(state.Reads.Any(r => SqlServerSchemaEvidence.Json(r) == SqlServerSchemaEvidence.Json(state.Returned)), "returned-read-not-retained");
            var links = SqlServerSchemaEvidence.Read<BodyLink[]>(Property(fact, "bodyLinks"));
            Require(links.Length == state.Bodies.Count && links.Select(l => l.Invocation).Distinct().Count() == links.Length, "body-link-count");
            foreach (var site in state.Bodies)
            {
                var link = links.SingleOrDefault(l => l.Invocation == site.Invocation);
                Require(link is not null && link.Body == site.Body && link.Binary == site.Binary && byId.ContainsKey(link.BodyFactId), "body-link-missing");
                var body = byId[link.BodyFactId];
                Require(body.FactType == FactTypes.ManagedIlBodyDeclared && body.Repo == repo && body.CommitSha == revision
                    && body.TargetSymbol == site.Body && Property(body, "rawFileSha256") == site.Binary
                    && Property(body, "ilGeneratorSha256") == Property(fact, "ilGeneratorSha256")
                    && Property(body, "ilBoundedInputSha256") == Property(fact, "ilBoundedInputSha256"), "body-link-conflict");
            }
            Require(byId.TryGetValue(Property(fact, "rootBodyFactId"), out var root) && root.TargetSymbol == fact.TargetSymbol
                && root.FactType == FactTypes.ManagedIlBodyDeclared && root.Repo == repo && root.CommitSha == revision, "root-link-conflict");
            Require(binding.Executions is not null && binding.Executions.Count == state.Executions.Count
                && binding.Executions.Select(e => e.ExecutionId).Distinct().Count() == binding.Executions.Count
                && state.Executions.Select(e => e.Id).Distinct().Count() == state.Executions.Count, "execution-count");
            var shapes = new Dictionary<string, SqlServerResultShape>(StringComparer.Ordinal);
            foreach (var execution in state.Executions)
            {
                var b = binding.Executions.SingleOrDefault(e => e.ExecutionId == execution.Id);
                Require(b is not null && b.Identity is not null && b.CommandText == b.Identity.Schema + "." + b.Identity.Procedure
                    && Literal(b.CommandText) == execution.CommandHash, "execution-binding-conflict");
                var shape = schema.Declarations.Shapes.SingleOrDefault(s => s.Identity == b.Identity);
                Require(shape is not null, "execution-shape-missing");
                Require(state.Bodies.Any(s => s.Body == execution.Site.Body && s.Binary == execution.Site.Binary
                    && s.Invocation == execution.Site.Invocation), "execution-site-conflict");
                shapes.Add(execution.Id, shape);
            }
            var read = state.Returned;
            Require(SqlServerSchemaEvidence.Identifier(binding.ColumnName) && Literal(binding.ColumnName) == read.ColumnHash
                && binding.ConsumerTable == read.Table && binding.ReadBody == read.Site.Body && binding.ReadBinary == read.Site.Binary
                && binding.ReadOffset == read.Site.Offset && binding.ReadInvocation == read.Site.Invocation, "consumer-binding-conflict");
            Require(read.SchemaExecutions.Count > 0 && read.SchemaExecutions.All(shapes.ContainsKey), "schema-execution-conflict");
            Require(read.Value.Kind is "sql" or "application" && (read.Value.Kind == "sql"
                ? read.Value.Execution is not null && shapes.ContainsKey(read.Value.Execution) : read.Value.Execution is null), "value-origin-conflict");
            var statuses = read.SchemaExecutions.Select(id => Match(shapes[id], binding.ColumnName)).ToArray();
            var status = statuses.Contains("match", StringComparer.Ordinal) ? "match"
                : statuses.All(s => s == "mismatch") ? "mismatch" : "unknown";
            // Retained schema ancestry does not establish the current execution's value.
            // In particular Clear + Merge may retain a column absent from the new result.
            var valueProven = read.Value.Kind == "application" || shapes[read.Value.Execution!].Columns
                .Count(c => Literal(c.Name) == read.Value.ColumnHash) == 1;
            var reasons = new List<string> { status == "match" ? "column-present-in-schema-ancestry"
                : status == "mismatch" ? "column-absent-from-complete-schema-ancestry" : "schema-identity-incomplete" };
            if (!valueProven) reasons.Add("current-execution-column-origin-unproven");
            rows.Add(new(fact.FactId, Property(fact, "guard"), read.Table, read.ColumnHash, status,
                reasons,
                read.SchemaExecutions, valueProven ? read.Value.Kind : "unknown", valueProven ? read.Value.Execution : null,
                read.Value.Table, read.Value.ColumnHash,
                read.Value.Assignment is not null, fact.RuleId, fact.EvidenceTier));
            Require(state.Failures is { Count: <= 256 }, "failure-limit");
            static SqlServerLifecycleSite Site(IlSqlLifecycleExtractor.Site site) => new(site.Body, site.Binary, site.Offset, site.Invocation);
            static SqlServerLifecycleTableState Table(IlSqlLifecycleExtractor.TableState table) => new(table.Allocation,
                table.RowsCleared, table.Execution, table.SchemaExecutions, table.Writes.Keys.Order(StringComparer.Ordinal).ToArray());
            static SqlServerLifecycleObjectState ObjectState(IlSqlLifecycleExtractor.ObjectSnapshot value)
                => new(value.Allocation, value.Kind, value.Slots, value.Fields);
            foreach (var failure in state.Failures)
            {
                Require(state.Bodies.Any(s => s.Body == failure.Site.Body && s.Binary == failure.Site.Binary && s.Invocation == failure.Site.Invocation)
                    && state.Bodies.Any(s => s.Body == failure.CatchHandler.Body && s.Binary == failure.CatchHandler.Binary
                        && s.Invocation == failure.CatchHandler.Invocation), "failure-site-conflict");
                failures.Add(new(fact.FactId, Property(fact, "guard"), failure.Operation, Site(failure.Site), Site(failure.CatchHandler),
                    failure.Before.Select(Table).ToArray(), failure.NormalCompletion.Select(Table).ToArray(),
                    failure.MayPartiallyMutate, failure.MayHaveCompletedEarlierRows,
                    failure.BeforeObjects.Select(ObjectState).ToArray(), failure.NormalCompletionObjects.Select(ObjectState).ToArray(),
                    failure.Limitation));
            }
            selected.Add(fact);
        }
        return new("sqlserver-compiled-lifecycle-comparison/1", SqlServerSchemaEvidence.GeneratorSha256,
            SqlServerSchemaEvidence.Digest(new { schema, bindings, candidates = selected.Select(Projection).ToArray() }),
            schema.GeneratorSha256, schema.BoundedInputSha256, repo, revision, "partial", rows, failures,
            [IlSqlLifecycleExtractor.Limitation, "Synthetic first-result schema identity only; no type, value, row, runtime or private-application proof. Bound selections are not exhaustive coverage."]);
    }

    private static string Match(SqlServerResultShape shape, string column)
    {
        var count = shape.Columns.Count(c => c.Name == column);
        if (count > 1) return "unknown";
        if (count == 1) return "match";
        if (shape.Columns.Any(c => string.Equals(c.Name, column, StringComparison.OrdinalIgnoreCase))) return "unknown";
        return shape.Completeness == "complete" ? "mismatch" : "unknown";
    }
}
