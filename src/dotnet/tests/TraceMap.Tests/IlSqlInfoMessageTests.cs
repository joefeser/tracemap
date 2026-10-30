using TraceMap.Core;
using TraceMap.Cli;
namespace TraceMap.Tests;

public sealed class IlSqlInfoMessageTests
{
    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Event_names_remain_unbound_on_the_filled_returned_allocation(string language)
    {
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var root = Assert.Single(bodies, b => b.Observation.ObjectBody?.Name == "EventNamingProduce");
        var result = IlSqlLifecycleExtractor.Extract(root, bodies, false, new(400_000), infoMessageBoundary: true);
        Assert.True(result.Gaps.Count == 0, string.Join("\n", result.Gaps));
        var entry = Assert.IsType<IlSqlLifecycleExtractor.EntryOutput>(result.Entry);
        var naming = Assert.Single(result.ResultNaming!);
        Assert.Equal(entry.ReturnedDataSet, naming.DataSet);
        Assert.Equal(Assert.Single(entry.Commands).Execution, naming.Execution);
        Assert.True(naming.DataSetName); Assert.True(naming.TableNames);
        Assert.Contains("unbound", naming.Limitation, StringComparison.Ordinal);
        Assert.Equal(new[] { "subscribe", "unsubscribe" }, result.InfoMessages!.Select(receipt => receipt.Action));
        Assert.Equal(result.InfoMessages![0].Connection, result.InfoMessages[1].Connection);
        Assert.Equal(result.InfoMessages[0].Delegate, result.InfoMessages[1].Delegate);
        Assert.DoesNotContain(result.InfoMessages[0].CallbackBody, result.Bodies!.Select(site => site.Body));
        Assert.Contains("callback-values-unbound", result.Claim, StringComparison.Ordinal);
        Assert.Single(Assert.Single(entry.Commands).Parameters);
        var strict = IlSqlLifecycleExtractor.Extract(root, bodies, false, new(400_000));
        Assert.NotEmpty(strict.Gaps); Assert.Null(strict.Entry);
        var replay = IlSqlLifecycleExtractor.Extract(root, bodies, false, new(400_000), infoMessageBoundary: true);
        Assert.Equal(SqlServerSchemaEvidence.Json(result), SqlServerSchemaEvidence.Json(replay));
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Separate_executions_keep_event_list_and_result_identities_separate(string language)
    {
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var root = Assert.Single(bodies, b => b.Observation.ObjectBody?.Name == "EventNamingSeparate");
        var result = IlSqlLifecycleExtractor.Extract(root, bodies, false, new(400_000), infoMessageBoundary: true);
        Assert.True(result.Gaps.Count == 0, string.Join("\n", result.Gaps));
        Assert.Equal(2, result.Executions.Count);
        Assert.Equal(2, result.ResultNaming!.Select(receipt => receipt.DataSet).Distinct().Count());
        Assert.Equal(2, result.InfoMessages!.Select(receipt => receipt.Connection).Distinct().Count());
        Assert.Equal(result.Entry!.ReturnedDataSet, result.ResultNaming![1].DataSet);
        Assert.NotEqual(result.Entry.Commands[0].Parameters[0].Allocation, result.Entry.Commands[1].Parameters[0].Allocation);
    }

    [Theory]
    [InlineData("csharp", "EventNameListMutation", "lifecycle-info-name-list-field-mutated")]
    [InlineData("vb", "EventNameListMutation", "lifecycle-info-name-list-field-mutated")]
    [InlineData("csharp", "EventNameListEscape", "lifecycle-info-parameter-reference-escape")]
    [InlineData("vb", "EventNameListEscape", "lifecycle-info-parameter-reference-escape")]
    [InlineData("csharp", "EventReceiverCatchRefusal", "lifecycle-info-reference-escape")]
    [InlineData("vb", "EventReceiverCatchRefusal", "lifecycle-info-reference-escape")]
    [InlineData("csharp", "EventUnsafeProduce", "lifecycle-info-callback-tracked-field")]
    [InlineData("vb", "EventUnsafeProduce", "lifecycle-info-callback-tracked-field")]
    [InlineData("csharp", "EventSenderRefusal", "lifecycle-info-sender-access")]
    [InlineData("vb", "EventSenderRefusal", "lifecycle-info-sender-access")]
    public void Callback_access_to_other_fields_or_sender_refuses(string language, string name, string gap)
    {
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var root = Assert.Single(bodies, b => b.Observation.ObjectBody?.Name == name);
        var result = IlSqlLifecycleExtractor.Extract(root, bodies, false, new(400_000), infoMessageBoundary: true);
        Assert.Equal(gap, Assert.Single(result.Gaps)); Assert.Null(result.Entry); Assert.Empty(result.Executions);
    }

    [Fact]
    public void Boundary_requires_selected_roots_and_exact_signed_identities()
    {
        Assert.Throws<ArgumentException>(() => IlBodyEvidenceExtractor.Evaluate(new ScanOptions(".", ".", IlBodyEvidence: true)
            { IlInfoMessageBoundary = true }, CompiledInputEvaluation.Disabled));
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Wrong_framework_identity_missing_callback_metadata_and_reused_list_refuse(string language)
    {
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var root = Assert.Single(bodies, b => b.Observation.ObjectBody?.Name == "EventNamingProduce");
        foreach (var identity in new[] { IlSqlInfoMessageApis.DelegateCtor, IlSqlInfoMessageApis.Add, IlSqlInfoMessageApis.Remove })
        foreach (var replacement in new[] { "wrong-key", "wrong-version", "no-shape" })
        {
            var changed = bodies.Select(b => b with { Observation = b.Observation with { Calls = b.Observation.Calls.Select(c =>
                c.TargetIdentity != identity ? c : replacement == "no-shape" ? c with { StackShape = null } : c with
                { TargetIdentity = replacement == "wrong-key" ? c.TargetIdentity.Replace("b77a5c561934e089", "0000000000000000", StringComparison.Ordinal)
                    : c.TargetIdentity.Replace("version:7:4.0.0.0", "version:7:3.0.0.0", StringComparison.Ordinal) }).ToArray() } }).ToArray();
            var selected = changed.Single(b => b.Observation.BodyIdentity == root.Observation.BodyIdentity);
            var refused = IlSqlLifecycleExtractor.Extract(selected, changed, false, new(400_000), infoMessageBoundary: true);
            Assert.NotEmpty(refused.Gaps); Assert.Null(refused.Entry);
        }
        var independent = bodies.Select(b => b.Observation.ObjectBody?.Name == "OnMessage" ? b with
            { Observation = b.Observation with { ObjectBodyAgreement = b.Observation.ObjectBodyAgreement! with { ValueFlow = false }, ValueFlow = null } } : b).ToArray();
        var inspected = IlSqlLifecycleExtractor.Extract(root, independent, false, new(400_000), infoMessageBoundary: true);
        Assert.Empty(inspected.Gaps); Assert.NotNull(inspected.Entry);
        var missing = bodies.Select(b => b.Observation.ObjectBody?.Name == "OnMessage" ? b with
            { Observation = b.Observation with { ObjectBodyAgreement = null } } : b).ToArray();
        var absent = IlSqlLifecycleExtractor.Extract(root, missing, false, new(400_000), infoMessageBoundary: true);
        Assert.Equal("lifecycle-info-body-metadata-or-limit", Assert.Single(absent.Gaps)); Assert.Null(absent.Entry);
        var mismatched = bodies.Single(b => b.Observation.ObjectBody?.Name == "EventNamingMismatch");
        var mismatch = IlSqlLifecycleExtractor.Extract(mismatched, bodies, false, new(400_000), infoMessageBoundary: true);
        Assert.Equal("lifecycle-info-naming-execution-mismatch", Assert.Single(mismatch.Gaps)); Assert.Null(mismatch.Entry);
        var oversized = bodies.Select(b => b.Observation.ObjectBody?.Name == "OnMessage" ? b with
            { Observation = b.Observation with { ObjectBody = b.Observation.ObjectBody with { Instructions = Enumerable.Repeat("i:0000:nop:", 257).ToArray() } } } : b).ToArray();
        var capped = IlSqlLifecycleExtractor.Extract(root, oversized, false, new(400_000), infoMessageBoundary: true);
        Assert.Equal("lifecycle-info-body-metadata-or-limit", Assert.Single(capped.Gaps)); Assert.Null(capped.Entry);
        var reused = bodies.Single(b => b.Observation.ObjectBody?.Name == "EventNamingReused");
        var reuse = IlSqlLifecycleExtractor.Extract(reused, bodies, false, new(400_000), infoMessageBoundary: true);
        Assert.Equal("lifecycle-info-list-reused", Assert.Single(reuse.Gaps)); Assert.Null(reuse.Entry);
    }

    [Theory]
    [InlineData("csharp", false)]
    [InlineData("vb", false)]
    [InlineData("csharp", true)]
    [InlineData("vb", true)]
    public async Task Native_entry_has_separate_partial_naming_schema_and_bound_provenance(string language, bool compiledOnly)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))) directory = directory.Parent;
        Assert.NotNull(directory);
        var config = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var sample = Path.Combine(directory.FullName, "samples", "sql-compiled-wrapper-evidence", language);
        var root = Assert.Single(IlSqlLifecycleExtractorTests.Bodies(language), b => b.Observation.ObjectBody?.Name == "EventNamingProduce");
        var output = IlBodyEvidenceExtractorShared.TempOutput();
        var args = new List<string> { "scan", "--repo", Path.Combine(sample, "end-to-end"), "--out", output,
            "--il-body-evidence", "--il-info-message-boundary", "--il-lifecycle-root", root.Input.RawFileSha256 + ":" + root.Observation.MetadataToken };
        if (compiledOnly) args.Add("--compiled-only");
        foreach (var role in new[] { "end-to-end", "wrapper" })
            args.AddRange(["--compiled-input", Path.Combine(sample, role, "bin", config, "net48", $"PublicSql.{language}.{role}.dll")]);
        using var stdout = new StringWriter(); using var stderr = new StringWriter();
        Assert.True(await TraceMapCommand.RunAsync(args.ToArray(), stdout, stderr) == 0, stderr.ToString());
        var facts = File.ReadLines(Path.Combine(output, "facts.ndjson")).Select(SqlServerSchemaEvidence.Read<CodeFact>).ToArray();
        if (compiledOnly)
        {
            Assert.DoesNotContain(facts, f => f.FactType == FactTypes.FileInventoried);
            Assert.Contains(facts, f => f.FactType == FactTypes.AnalysisGap
                && f.Properties.Values.Any(v => v.Contains("SourceAnalysisExcludedByCompiledOnly", StringComparison.Ordinal)));
        }
        var candidate = Assert.Single(facts, f => f.TargetSymbol == root.Observation.BodyIdentity && f.FactType == FactTypes.ManagedIlSqlLifecycleEntryCandidate);
        Assert.Equal(IlSqlLifecycleExtractor.InfoMessageSchema, candidate.Properties["sqlLifecycleSchema"]);
        Assert.Equal(IlSqlLifecycleExtractor.InfoMessagePolicy, candidate.Properties["infoMessagePolicy"]);
        Assert.Equal(64, candidate.Properties["ilGeneratorSha256"].Length);
        Assert.Equal(64, candidate.Properties["ilBoundedInputSha256"].Length);
        var result = SqlServerSchemaEvidence.Read<IlSqlLifecycleExtractor.Result>(candidate.Properties["lifecycle"]);
        Assert.Single(result.ResultNaming!); Assert.Equal(2, result.InfoMessages!.Count);
        foreach (var required in new[] { "scan-manifest.json", "facts.ndjson", "index.sqlite", "report.md", "logs/analyzer.log" })
            Assert.True(File.Exists(Path.Combine(output, required)));
    }
}
