using TraceMap.Core;
using TraceMap.Cli;
namespace TraceMap.Tests;

public sealed class IlSqlInfoMessageTests
{
    [Theory]
    [InlineData("csharp", false)]
    [InlineData("vb", false)]
    [InlineData("csharp", true)]
    [InlineData("vb", true)]
    public void Xml_ordinal_attribute_and_inner_xml_are_exact_bounded_external_calls(string language, bool legacy)
    {
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language, legacy);
        var callback = Assert.Single(bodies, body => body.Observation.ObjectBody?.Name == "OnMessage");
        var xml = "scope(assembly:name:10:System.Xml|version:7:4.0.0.0|culture:7:neutral|publicKeyToken:16:b77a5c561934e089)";
        string X(string name) => xml + IlSqlInfoMessageApis.T("System.Xml", name);
        var identities = new[] {
            IlSqlInfoMessageApis.M(X("XmlAttributeCollection"), "get_ItemOf", X("XmlAttribute"), true, IlSqlInfoMessageApis.P("Int32")),
            IlSqlInfoMessageApis.M(X("XmlNode"), "get_InnerXml", IlSqlInfoMessageApis.P("String"), true) };
        var root = bodies.Single(body => body.Observation.ObjectBody?.Name == "EventNamingProduce");
        foreach (var identity in identities.Select(identity => legacy ? IlSqlInfoMessageApis.Legacy(identity) : identity))
        {
            Assert.Contains(callback.Observation.Calls, call => call.TargetIdentity == identity);
            Assert.Equal("external", IlSqlInfoMessageApis.Rule(identity));
            foreach (var invalid in new[] { identity.Replace("b77a5c561934e089", "0000000000000000", StringComparison.Ordinal),
                identity.Replace(legacy ? "version:7:2.0.0.0" : "version:7:4.0.0.0", "version:7:3.0.0.0", StringComparison.Ordinal),
                identity.Replace("|hasThis:true|", "|hasThis:false|", StringComparison.Ordinal) })
            {
                Assert.Null(IlSqlInfoMessageApis.Rule(invalid));
                var changed = bodies.Select(body => body with { Observation = body.Observation with
                    { Calls = body.Observation.Calls.Select(call => call.TargetIdentity == identity
                        ? call with { TargetIdentity = invalid } : call).ToArray() } }).ToArray();
                var rejected = IlSqlLifecycleExtractor.Extract(changed.Single(body => body.Observation.BodyIdentity == root.Observation.BodyIdentity),
                    changed, false, new(400_000), infoMessageBoundary: true);
                Assert.NotEmpty(rejected.Gaps); Assert.Null(rejected.Entry);
            }
        }
        var result = IlSqlLifecycleExtractor.Extract(root, bodies, false, new(400_000), infoMessageBoundary: true);
        Assert.Empty(result.Gaps); Assert.Equal(result.Entry!.ReturnedDataSet, Assert.Single(result.ResultNaming!).DataSet);
        Assert.Contains("unbound", result.Claim, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Exact_framework_two_profile_keeps_lifecycle_identity_and_refusals(string language)
    {
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language, legacyFramework: true);
        var root = Assert.Single(bodies, b => b.Observation.ObjectBody?.Name == "EventNamingProduce");
        Assert.Contains(bodies.SelectMany(b => b.Observation.Calls), call =>
            call.TargetIdentity == IlSqlInfoMessageApis.Legacy(IlSqlInfoMessageApis.DelegateCtor));
        var result = IlSqlLifecycleExtractor.Extract(root, bodies, false, new(400_000), infoMessageBoundary: true);
        Assert.True(result.Gaps.Count == 0, string.Join("\n", result.Gaps));
        Assert.Equal(result.Entry!.ReturnedDataSet, Assert.Single(result.ResultNaming!).DataSet);
        Assert.Single(Assert.Single(result.Entry.Commands).Parameters);
        Assert.Equal(new[] { "subscribe", "unsubscribe" }, result.InfoMessages!.Select(receipt => receipt.Action));
        Assert.Null(IlSqlLifecycleExtractor.Extract(root, bodies, false, new(400_000)).Entry);
        foreach (var (name, gap) in new[] { ("EventNameListEscape", "lifecycle-info-parameter-reference-escape"),
            ("EventSenderRefusal", "lifecycle-info-sender-access"), ("EventNamingReused", "lifecycle-info-list-reused") })
        {
            var negative = bodies.Single(b => b.Observation.ObjectBody?.Name == name);
            var refused = IlSqlLifecycleExtractor.Extract(negative, bodies, false, new(400_000), infoMessageBoundary: true);
            Assert.Equal(gap, Assert.Single(refused.Gaps)); Assert.Null(refused.Entry);
        }
        var mixed = bodies.Select(body => body with { Observation = body.Observation with
            { Calls = body.Observation.Calls.Select(call => call.TargetIdentity == IlSqlInfoMessageApis.Legacy(IlSqlInfoMessageApis.Add)
                ? call with { TargetIdentity = IlSqlInfoMessageApis.Add } : call).ToArray() } }).ToArray();
        var mixedResult = IlSqlLifecycleExtractor.Extract(mixed.Single(body => body.Observation.BodyIdentity == root.Observation.BodyIdentity),
            mixed, false, new(400_000), infoMessageBoundary: true);
        Assert.Equal("lifecycle-info-framework-profile-mismatch", Assert.Single(mixedResult.Gaps)); Assert.Null(mixedResult.Entry);
        var expected = IlSqlInfoMessageApis.Legacy(IlSqlInfoMessageApis.DelegateCtor);
        foreach (var invalid in new[] { expected.Replace("version:7:2.0.0.0", "version:7:3.0.0.0", StringComparison.Ordinal),
            expected.Replace("b77a5c561934e089", "0000000000000000", StringComparison.Ordinal),
            expected.Replace("names:6:IntPtr", "names:5:Int32", StringComparison.Ordinal) })
        {
            Assert.False(IlSqlInfoMessageApis.Matches(invalid, IlSqlInfoMessageApis.DelegateCtor));
            var altered = bodies.Select(body => body with { Observation = body.Observation with
                { Calls = body.Observation.Calls.Select(call => call.TargetIdentity == expected
                    ? call with { TargetIdentity = invalid } : call).ToArray() } }).ToArray();
            var refused = IlSqlLifecycleExtractor.Extract(altered.Single(body => body.Observation.BodyIdentity == root.Observation.BodyIdentity),
                altered, false, new(400_000), infoMessageBoundary: true);
            Assert.Equal("lifecycle-info-function-escape", Assert.Single(refused.Gaps)); Assert.Null(refused.Entry);
        }
    }

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
    [InlineData("csharp", false, false)]
    [InlineData("vb", false, false)]
    [InlineData("csharp", true, false)]
    [InlineData("vb", true, false)]
    [InlineData("csharp", true, true)]
    [InlineData("vb", true, true)]
    public async Task Native_entry_has_separate_partial_naming_schema_and_bound_provenance(string language, bool compiledOnly, bool legacyFramework)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))) directory = directory.Parent;
        Assert.NotNull(directory);
        var config = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var sample = Path.Combine(directory.FullName, "samples", "sql-compiled-wrapper-evidence", language);
        var root = Assert.Single(IlSqlLifecycleExtractorTests.Bodies(language, legacyFramework), b => b.Observation.ObjectBody?.Name == "EventNamingProduce");
        var output = IlBodyEvidenceExtractorShared.TempOutput();
        var inputDirectory = IlBodyEvidenceExtractorShared.TempOutput(); Directory.CreateDirectory(inputDirectory);
        var inputs = new[] { "end-to-end", "wrapper" }.Select(role =>
        {
            var path = Path.Combine(sample, role, "bin", config, "net48", $"PublicSql.{language}.{role}.dll");
            if (!legacyFramework) return path;
            var copy = Path.Combine(inputDirectory, Path.GetFileName(path));
            File.WriteAllBytes(copy, IlSqlLifecycleExtractorTests.LegacyFrameworkBytes(File.ReadAllBytes(path)));
            return copy;
        }).ToArray();
        var rootSha = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(inputs[0])));
        var args = new List<string> { "scan", "--repo", Path.Combine(sample, "end-to-end"), "--out", output,
            "--il-body-evidence", "--il-info-message-boundary", "--il-lifecycle-root", rootSha + ":" + root.Observation.MetadataToken };
        if (compiledOnly) args.Add("--compiled-only");
        foreach (var input in inputs) args.AddRange(["--compiled-input", input]);
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
