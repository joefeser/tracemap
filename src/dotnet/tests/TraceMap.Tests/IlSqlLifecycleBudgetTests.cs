using System.Text.Json;
using TraceMap.Cli;
using TraceMap.Core;
using Xunit.Abstractions;

namespace TraceMap.Tests;

public sealed class IlSqlLifecycleBudgetTests(ITestOutputHelper output)
{
    [Fact]
    public void Work_stop_records_root_accounting_and_the_forks_already_explored()
    {
        var bodies = IlSqlLifecycleExtractorTests.Bodies("csharp");
        var root = bodies.Single(body => body.Observation.ObjectBody?.Name == "ClosurePrivateShapeProduce");
        var budget = new IlBodyEvidenceExtractor.IlWorkBudget(new IlBodyLimits().MaxTotalWorkUnits);
        Assert.True(budget.TryConsume(budget.Maximum - 100_000));
        var before = budget.Consumed;
        var result = IlSqlLifecycleExtractor.Extract(root, bodies, false, budget,
            opaqueRootInitializers: true, externalTraceBoundary: true, compiledScalarBoundary: true, infoMessageBoundary: true);
        Assert.Equal("lifecycle-work-limit", Assert.Single(result.Gaps));
        using var json = JsonDocument.Parse(SqlServerSchemaEvidence.Json(result));
        Assert.True(json.RootElement.TryGetProperty("workLimitFailure", out var failure));
        Assert.NotEmpty(failure.GetProperty("forkCensus").EnumerateArray());
        Assert.Equal("shared-request-refused-during-extraction", failure.GetProperty("budgetObservation").GetString());
        var work = json.RootElement.GetProperty("workAccounting");
        Assert.Equal(before, work.GetProperty("consumedBefore").GetInt64());
        Assert.Equal(budget.Consumed, work.GetProperty("consumedAfter").GetInt64());
        Assert.Equal(budget.Consumed - before, work.GetProperty("rootConsumed").GetInt64());
        Assert.True(work.GetProperty("refusedAfter").GetInt64() > work.GetProperty("refusedBefore").GetInt64());
        Assert.InRange(budget.Consumed, before, budget.Maximum);
        Assert.Null(result.Entry); Assert.Null(result.StateAlternatives);
    }

    [Fact]
    public void An_already_spent_shared_budget_records_zero_new_work_without_an_entry()
    {
        var bodies = IlSqlLifecycleExtractorTests.Bodies("csharp");
        var budget = new IlBodyEvidenceExtractor.IlWorkBudget(new IlBodyLimits().MaxTotalWorkUnits);
        Assert.True(budget.TryConsume(budget.Maximum));
        var root = bodies.Single(body => body.Observation.ObjectBody?.Name == "ClosureProduce");
        var result = IlSqlLifecycleExtractor.Extract(root, bodies, false, budget,
            opaqueRootInitializers: true, externalTraceBoundary: true, compiledScalarBoundary: true, infoMessageBoundary: true);
        Assert.Equal("lifecycle-index-limit", Assert.Single(result.Gaps));
        Assert.Equal(0, result.WorkAccounting!.RootConsumed);
        Assert.Equal(0, result.WorkAccounting.RemainingBefore);
        Assert.True(result.WorkAccounting.RefusedAfter > result.WorkAccounting.RefusedBefore);
        Assert.Null(result.WorkLimitFailure); Assert.Null(result.Entry);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Direct_interpreter_roots_share_the_caller_budget_in_both_orders(bool reverse)
    {
        var bodies = IlSqlLifecycleExtractorTests.Bodies("csharp");
        var names = new[] { "ClosureProduce", "ClosurePrivateShapeProduce" };
        if (reverse) Array.Reverse(names);
        Assert.Equal(2_000_000, new IlBodyLimits().MaxTotalWorkUnits);
        var shared = new IlBodyEvidenceExtractor.IlWorkBudget(new IlBodyLimits().MaxTotalWorkUnits);
        foreach (var name in names)
        {
            var root = bodies.Single(body => body.Observation.ObjectBody?.Name == name);
            var standalone = IlSqlLifecycleExtractor.Extract(root, bodies, false, new(new IlBodyLimits().MaxTotalWorkUnits),
                opaqueRootInitializers: true, externalTraceBoundary: true, compiledScalarBoundary: true, infoMessageBoundary: true);
            var before = shared.Consumed;
            var result = IlSqlLifecycleExtractor.Extract(root, bodies, false, shared,
                opaqueRootInitializers: true, externalTraceBoundary: true, compiledScalarBoundary: true, infoMessageBoundary: true);
            var work = result.WorkAccounting!;
            Assert.Equal(before, work.ConsumedBefore); Assert.Equal(shared.Consumed, work.ConsumedAfter);
            Assert.Equal(shared.Consumed - before, work.RootConsumed);
            Assert.InRange(shared.Consumed, before, shared.Maximum);
            if (shared.Maximum - before >= standalone.WorkAccounting!.RootConsumed && standalone.Gaps.Count == 0)
            {
                Assert.Empty(result.Gaps);
                Assert.Equal(standalone.StateAlternatives!.Count, result.StateAlternatives!.Count);
                Assert.Equal(standalone.StateAlternatives.Count(alternative => alternative.Completion?.Entry is not null),
                    result.StateAlternatives.Count(alternative => alternative.Completion?.Entry is not null));
            }
            else
            {
                Assert.Equal("lifecycle-work-limit", Assert.Single(result.Gaps));
                Assert.NotNull(result.WorkLimitFailure); Assert.Null(result.StateAlternatives);
            }
            output.WriteLine(name + ": " + SqlServerSchemaEvidence.Json(work));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Native_two_root_sequence_reports_the_actual_shared_production_budget(bool reverse)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))) directory = directory.Parent;
        Assert.NotNull(directory);
        var bodies = IlSqlLifecycleExtractorTests.Bodies("csharp");
        var roots = new[] { "ClosureProduce", "ClosurePrivateShapeProduce" }
            .Select(name => bodies.Single(body => body.Observation.ObjectBody?.Name == name)).ToArray();
        if (reverse) Array.Reverse(roots);
        var rootDirectory = IlBodyEvidenceExtractorShared.TempOutput(); Directory.CreateDirectory(rootDirectory);
        var repo = Path.Combine(rootDirectory, "repo"); Directory.CreateDirectory(repo);
        var scanOutput = Path.Combine(rootDirectory, "scan");
        var args = new List<string> { "scan", "--repo", repo, "--out", scanOutput, "--compiled-only",
            "--il-body-evidence", "--il-opaque-root-initializers", "--il-external-trace-boundary",
            "--il-compiled-scalar-boundary", "--il-info-message-boundary" };
        foreach (var root in roots) args.AddRange(["--il-lifecycle-root", root.Input.RawFileSha256 + ":" + root.Observation.MetadataToken]);
        var config = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        foreach (var role in new[] { "end-to-end", "wrapper", "whole-protocol" })
            args.AddRange(["--compiled-input", Path.Combine(directory.FullName, "samples", "sql-compiled-wrapper-evidence",
                "csharp", role, "bin", config, "net48", "PublicSql.csharp." + role + ".dll")]);
        using var stdout = new StringWriter(); using var stderr = new StringWriter();
        Assert.True(await TraceMapCommand.RunAsync(args.ToArray(), stdout, stderr) == 0, stderr.ToString());
        var facts = File.ReadLines(Path.Combine(scanOutput, "facts.ndjson")).Select(SqlServerSchemaEvidence.Read<CodeFact>)
            .Where(fact => fact.RuleId == RuleIds.DotNetIlSqlLifecycle && roots.Any(root => root.Observation.BodyIdentity == fact.TargetSymbol)).ToArray();
        foreach (var root in roots)
        {
            var rootFacts = facts.Where(fact => fact.TargetSymbol == root.Observation.BodyIdentity).ToArray();
            var rootFact = Assert.Single(rootFacts);
            Assert.Equal(FactTypes.ManagedIlSqlLifecycleCandidate, rootFact.FactType);
            using (var value = JsonDocument.Parse(rootFact.Properties["lifecycle"]))
            {
                var alternatives = value.RootElement.GetProperty("stateAlternatives").EnumerateArray().ToArray();
                var entries = alternatives.Count(alternative => alternative.TryGetProperty("completion", out var completion)
                    && completion.ValueKind == JsonValueKind.Object && completion.TryGetProperty("entry", out var entry) && entry.ValueKind == JsonValueKind.Object);
                var simple = root.Observation.ObjectBody!.Name == "ClosureProduce";
                Assert.Equal(simple ? 3 : 36, alternatives.Length);
                Assert.Equal(simple ? 2 : 36, entries);
            }
            foreach (var fact in rootFacts)
            {
                output.WriteLine(root.Observation.ObjectBody!.Name + ": " + fact.FactType + " " + fact.Properties.GetValueOrDefault("gapKind", ""));
                if (fact.Properties.TryGetValue("lifecycle", out var lifecycle))
                {
                    using var value = JsonDocument.Parse(lifecycle);
                    if (value.RootElement.TryGetProperty("stateAlternatives", out var alternatives))
                        output.WriteLine("alternatives=" + alternatives.GetArrayLength() + ";entries=" + alternatives.EnumerateArray().Count(alternative =>
                            alternative.TryGetProperty("completion", out var completion) && completion.ValueKind == JsonValueKind.Object
                            && completion.TryGetProperty("entry", out var entry) && entry.ValueKind == JsonValueKind.Object));
                }
            }
            Assert.All(rootFacts, fact => Assert.True(fact.Properties.ContainsKey("lifecycleWork")));
            Assert.All(rootFacts, fact => Assert.Equal("shared-across-roots-and-guards", fact.Properties["lifecycleBudgetScope"]));
        }
        var orderedFacts = facts.OrderBy(fact => int.Parse(fact.Properties["lifecycleRootOrdinal"])).ToArray();
        var canonical = roots.OrderBy(root => root.Input.RawFileSha256, StringComparer.Ordinal)
            .ThenBy(root => root.Observation.MetadataToken, StringComparer.Ordinal).ToArray();
        Assert.Equal(canonical.Select(root => root.Observation.BodyIdentity), orderedFacts.Select(fact => fact.TargetSymbol));
        var ledgers = facts.GroupBy(fact => fact.TargetSymbol).Select(group => group.First()).OrderBy(fact => int.Parse(fact.Properties["lifecycleRootOrdinal"]))
            .Select(fact => JsonDocument.Parse(fact.Properties["lifecycleWork"])).ToArray();
        try
        {
            Assert.Equal(2, ledgers.Length);
            Assert.Equal(0, ledgers[0].RootElement.GetProperty("consumedBefore").GetInt64());
            Assert.Equal(ledgers[0].RootElement.GetProperty("consumedAfter").GetInt64(), ledgers[1].RootElement.GetProperty("consumedBefore").GetInt64());
            foreach (var ledger in ledgers)
            {
                Assert.Equal(new IlBodyLimits().MaxTotalWorkUnits, ledger.RootElement.GetProperty("maximum").GetInt64());
                Assert.InRange(ledger.RootElement.GetProperty("consumedAfter").GetInt64(), 0, new IlBodyLimits().MaxTotalWorkUnits);
                output.WriteLine(ledger.RootElement.GetRawText());
            }
            foreach (var gap in facts.Where(fact => fact.FactType == FactTypes.AnalysisGap))
            {
                output.WriteLine("stop=" + gap.Properties["gapKind"]);
                if (gap.Properties["gapKind"] == "lifecycle-work-limit") Assert.True(gap.Properties.ContainsKey("workLimitFailure"));
            }
        }
        finally { foreach (var ledger in ledgers) ledger.Dispose(); }
    }
}
