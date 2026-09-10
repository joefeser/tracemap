using System.Text.Json;
using Microsoft.Data.Sqlite;
using TraceMap.Reporting;

namespace TraceMap.Tests;

public sealed class WebFormsRawEvidenceAuditTests
{
    [Fact]
    public void BatchInspectionDistinguishesUiEndpointsWithoutOtherUnresolvedLeaves()
    {
        WithFixture((db, report) =>
        {
            using (var connection = new SqliteConnection($"Data Source={db};Pooling=False"))
            {
                connection.Open();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = """
                    delete from facts where fact_id in ('call','invoke-one','invoke-two');
                    insert into facts(fact_id,fact_type,source_symbol,target_symbol)
                    values('ui-only','MethodInvoked','Private.Handler()','global::System.Web.UI.WebControls.ListItemCollection.Clear()');
                    """;
                cmd.ExecuteNonQuery();
            }
            var path = Path.Combine(Path.GetDirectoryName(report)!, "ui-only-batch.json");
            var lines = WebFormsRawEvidenceAudit.Run(db, report, inspectionPath: path, inspectAllHandlers: true);
            using var json = JsonDocument.Parse(File.ReadAllText(path));
            var item = json.RootElement.GetProperty("cases")[0];
            Assert.Equal("ui-control-operations-observed-no-other-unresolved-leaves", item.GetProperty("evidenceConclusion").GetString());
            Assert.Single(item.GetProperty("uiControlEndpoints").EnumerateArray());
            Assert.Empty(item.GetProperty("unresolvedOtherLeaves").EnumerateArray());
            Assert.Contains(lines, line => line.Contains("uiControlEndpoints=1|unresolvedOtherLeaves=0|evidence=ui-control-operations-observed-no-other-unresolved-leaves", StringComparison.Ordinal));
        });
    }

    [Fact]
    public void BatchInspectionIncludesEveryHandlerSiblingAndStoppingLocationWithoutConsoleDisclosure()
    {
        WithFixture((db, report) =>
        {
            using (var connection = new SqliteConnection($"Data Source={db};Pooling=False"))
            {
                connection.Open();
                using var cmd = connection.CreateCommand();
                cmd.CommandText = """
                    insert into facts(fact_id,fact_type,target_symbol) values('handler-two','WebFormsHandlerResolved','Private.SecondHandler()');
                    insert into facts(fact_id,fact_type,source_symbol,target_symbol,start_line) values
                        ('sibling','CallEdge','Private.Handler()','Private.UiReset()',42),
                        ('ui-control','MethodInvoked','Private.Handler()','global::System.Web.UI.WebControls.ListControl.ClearSelection()',43),
                        ('second-call','MethodInvoked','Private.SecondHandler()','Private.OtherStop()',70);
                    """;
                cmd.ExecuteNonQuery();
            }
            File.WriteAllText(report, JsonSerializer.Serialize(new
            {
                schemaVersion = "webforms-modernization-packet.v1",
                sources = new[] { new { scanId = "scan-one", commitSha = "commit-one" } },
                eventChains = new[] { "handler", "handler-two" }.Select(id => new
                {
                    handlerFactId = id,
                    bindingFactId = id,
                    surfaceId = "private-surface",
                    terminalKind = "",
                    traversalObservation = new { stopState = "observed-downstream-without-supported-terminal" }
                })
            }));
            var before = File.ReadAllBytes(db);
            var path = Path.Combine(Path.GetDirectoryName(report)!, "batch.json");
            var lines = WebFormsRawEvidenceAudit.Run(db, report, inspectionPath: path, inspectAllHandlers: true);
            Assert.Contains("batchInspection=created|chains=2|handlers=2|boundedHandlers=0", lines);
            Assert.DoesNotContain("Private", string.Join('\n', lines));
            using var json = JsonDocument.Parse(File.ReadAllText(path));
            var cases = json.RootElement.GetProperty("cases").EnumerateArray().ToArray();
            Assert.Equal(2, cases.Length);
            var first = Assert.Single(cases, c => c.GetProperty("handler").GetString() == "Private.Handler()");
            Assert.Contains(first.GetProperty("stoppingSymbols").EnumerateArray(), s => s.GetString() == "Private.UiReset()");
            Assert.Contains(first.GetProperty("stoppingSymbols").EnumerateArray(), s => s.GetString() == "Private.External()");
            Assert.Equal("ui-control-operations-observed-with-unresolved-leaves", first.GetProperty("evidenceConclusion").GetString());
            Assert.Single(first.GetProperty("uiControlEndpoints").EnumerateArray());
            Assert.Equal(2, first.GetProperty("unresolvedOtherLeaves").GetArrayLength());
            Assert.Equal("no-supported-backend-terminal-observed", first.GetProperty("backendTerminalConclusion").GetString());
            var second = Assert.Single(cases, c => c.GetProperty("handler").GetString() == "Private.SecondHandler()");
            Assert.Single(second.GetProperty("stoppingSymbols").EnumerateArray());
            Assert.Equal("no-supported-backend-terminal-observed", second.GetProperty("evidenceConclusion").GetString());
            var markdown = File.ReadAllText(Path.ChangeExtension(path, ".md"));
            Assert.Contains("Private.cs:42", markdown);
            Assert.Contains("Private.cs:70", markdown);
            Assert.Contains("case-001", markdown);
            Assert.Contains("case-002", markdown);
            Assert.Contains("Evidence conclusion: **ui-control-operations-observed-with-unresolved-leaves**", markdown);
            Assert.Contains("Observed UI/control endpoints: 1; other unresolved leaves: 2; supported backend terminal: not observed.", markdown);
            Assert.Contains("Manual result: **unreviewed**", markdown);
            Assert.Contains(lines, line => line.Contains("uiControlEndpoints=1|unresolvedOtherLeaves=2|evidence=ui-control-operations-observed-with-unresolved-leaves", StringComparison.Ordinal));
            Assert.Equal(before, File.ReadAllBytes(db));
            Assert.Throws<IOException>(() => WebFormsRawEvidenceAudit.Run(db, report, inspectionPath: path, inspectAllHandlers: true));
            Assert.Equal(markdown, File.ReadAllText(Path.ChangeExtension(path, ".md")));
        });
    }

    [Fact]
    public void RawAuditReadsWitnessesWithoutCompactionAndDoesNotEchoSymbols()
    {
        WithFixture((db, report) =>
        {
            var before = File.ReadAllBytes(db);
            var lines = WebFormsRawEvidenceAudit.Run(db, report);
            Assert.Contains(lines, l => l.Contains("semanticInvocationSources=2") && l.Contains("invocationWithoutCallFact=1"));
            Assert.Contains(lines, l => l.Contains("symbols=3|bounded=false"));
            Assert.Equal(before, File.ReadAllBytes(db));
            Assert.DoesNotContain("Private", string.Join('\n', lines));
            Assert.Contains("scope=independent-exact-semantic-call-closure-not-report-leaves", lines);
        });
    }

    [Fact]
    public void LocalInspectionKeepsPrivateCallSitesOutOfConsoleAndDoesNotOverwrite()
    {
        WithFixture((db, report) =>
        {
            var path = Path.Combine(Path.GetDirectoryName(report)!, "private.json");
            var lines = WebFormsRawEvidenceAudit.Run(db, report, inspectionPath: path);
            Assert.DoesNotContain("Private", string.Join('\n', lines));
            using var inspection = JsonDocument.Parse(File.ReadAllText(path));
            var root = inspection.RootElement;
            Assert.Equal("Private.Handler()", root.GetProperty("handler").GetString());
            Assert.Equal("Private.External()", root.GetProperty("stoppingSymbol").GetString());
            Assert.Equal(2, root.GetProperty("hops").GetArrayLength());
            Assert.Equal("call-site-not-callee-definition", root.GetProperty("hops")[1].GetProperty("locationKind").GetString());
            Assert.Equal("Private.cs", root.GetProperty("hops")[1].GetProperty("location").GetProperty("filePath").GetString());
            var before = File.ReadAllBytes(path);
            Assert.Throws<IOException>(() => WebFormsRawEvidenceAudit.Run(db, report, inspectionPath: path));
            Assert.Equal(before, File.ReadAllBytes(path));
        });
    }

    [Fact]
    public void MethodHintFollowsThreeLayersWithoutInferringAnEventBinding()
    {
        WithFixture((db, report) =>
        {
            using (var c = new SqliteConnection($"Data Source={db};Pooling=False"))
            {
                c.Open();
                using var cmd = c.CreateCommand();
                cmd.CommandText = """
                    insert into facts(fact_id,fact_type,source_symbol,target_symbol) values
                    ('fill','CallEdge','Private.External()','System.Data.SqlDataAdapter.Fill(System.Data.DataTable)'),
                    ('other','CallEdge','Private.Handler()','Private.AFirstLeaf()');
                    """;
                cmd.ExecuteNonQuery();
            }
            var path = Path.Combine(Path.GetDirectoryName(report)!, "private.json");
            var output = WebFormsRawEvidenceAudit.Run(db, report, inspectionPath: path, startingMethodName: "Handler");
            Assert.DoesNotContain("Private", string.Join('\n', output));
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            Assert.Equal(3, doc.RootElement.GetProperty("hops").GetArrayLength());
            Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("surfaceId").ValueKind);
            Assert.StartsWith("System.Data.SqlDataAdapter.Fill", doc.RootElement.GetProperty("stoppingSymbol").GetString());
        });
    }

    [Theory]
    [InlineData("Missing", "RawAuditMethodNotFound")]
    [InlineData("Handler", "RawAuditMethodAmbiguous")]
    public void MethodHintsFailClosedWhenNotUnique(string hint, string code)
    {
        WithFixture((db, report) =>
        {
            using (var c = new SqliteConnection($"Data Source={db};Pooling=False"))
            {
                c.Open();
                using var cmd = c.CreateCommand();
                cmd.CommandText = "insert into facts(fact_id,fact_type,source_symbol,target_symbol) values('overload','CallEdge','Other.Handler()','Other.End()')";
                cmd.ExecuteNonQuery();
            }
            var ex = Assert.Throws<InvalidDataException>(() => WebFormsRawEvidenceAudit.Run(db, report, startingMethodName: hint));
            Assert.Equal(code, ex.Message);
        });
    }

    [Fact]
    public void LocalInspectionShowsSiblingCallsAndRepeatedSitesSeparately()
    {
        WithFixture((db, report) =>
        {
            using (var c = new SqliteConnection($"Data Source={db};Pooling=False"))
            {
                c.Open();
                using var cmd = c.CreateCommand();
                cmd.CommandText = """
                    insert into facts(fact_id,fact_type,source_symbol,target_symbol,start_line,end_line) values
                    ('ui','CallEdge','Private.Handler()','Private.UiOnly()',3,3),
                    ('later','CallEdge','Private.Handler()','Private.Later()',20,20),
                    ('repeat','CallEdge','Private.Handler()','Private.Leaf()',25,25);
                    """;
                cmd.ExecuteNonQuery();
            }
            var path = Path.Combine(Path.GetDirectoryName(report)!, "private.json");
            var output = WebFormsRawEvidenceAudit.Run(db, report, inspectionPath: path);
            Assert.Contains("localInspectionDirectCallSites=4", output);
            Assert.DoesNotContain("Private", string.Join('\n', output));
            using var inspection = JsonDocument.Parse(File.ReadAllText(path));
            var calls = inspection.RootElement.GetProperty("directCalls");
            Assert.Equal(4, calls.GetArrayLength());
            Assert.Equal(2, calls[0].GetProperty("witnesses").GetArrayLength());
            Assert.Equal("Private.UiOnly()", calls[1].GetProperty("callee").GetString());
            Assert.Equal("Private.UiOnly()", calls[1].GetProperty("branch").GetProperty("stoppingSymbols")[0].GetString());
            Assert.Equal("Private.Later()", calls[2].GetProperty("callee").GetString());
            Assert.Equal(25, calls[3].GetProperty("startLine").GetInt32());
        });
    }

    [Fact]
    public void LocalInspectionLabelsMissingSourceLocations()
    {
        WithFixture((db, report) =>
        {
            using (var c = new SqliteConnection($"Data Source={db};Pooling=False"))
            {
                c.Open();
                using var cmd = c.CreateCommand();
                cmd.CommandText = "update facts set file_path=null";
                cmd.ExecuteNonQuery();
            }
            var path = Path.Combine(Path.GetDirectoryName(report)!, "private.json");
            WebFormsRawEvidenceAudit.Run(db, report, inspectionPath: path);
            using var inspection = JsonDocument.Parse(File.ReadAllText(path));
            Assert.Equal("source-location-unavailable", inspection.RootElement.GetProperty("hops")[0].GetProperty("location").GetProperty("availability").GetString());
        });
    }

    [Fact]
    public void RawAuditDoesNotChargeUnrelatedSymbolsToInputBudgets()
    {
        WithFixture((db, report) =>
        {
            using (var c = new SqliteConnection($"Data Source={db};Pooling=False"))
            {
                c.Open();
                using var cmd = c.CreateCommand();
                cmd.CommandText = """
                    insert into facts(fact_id,fact_type,source_symbol,target_symbol)
                    values('unrelated','MethodInvoked',$large,'Private.Handler()');
                    insert into facts(fact_id,fact_type,source_symbol,target_symbol)
                    values('unrelated-declaration','MethodDeclared','Private.Handler()',$large);
                    """;
                cmd.Parameters.AddWithValue("$large", new string('x', 10_000));
                cmd.ExecuteNonQuery();
            }
            var lines = WebFormsRawEvidenceAudit.Run(db, report, maxRows: 4, maxTextBytes: 1024);
            Assert.Contains("rawFactRows=4", lines);
            Assert.Contains(lines, l => l.Contains("semanticInvocationSources=2"));
        });
    }

    [Fact]
    public void RawAuditStillRejectsOversizedSelectedEvidence()
    {
        WithFixture((db, report) => Assert.Throws<InvalidDataException>(() =>
            WebFormsRawEvidenceAudit.Run(db, report, maxTextBytes: 1)));
    }

    [Fact]
    public void RawAuditRejectsIncompleteInputInsteadOfPublishingAbsence()
    {
        WithFixture((db, report) => Assert.Throws<InvalidDataException>(() => WebFormsRawEvidenceAudit.Run(db, report, 1)));
    }

    [Fact]
    public void RawAuditLabelsTraversalLimits()
    {
        WithFixture((db, report) =>
        {
            using (var c = new SqliteConnection($"Data Source={db};Pooling=False"))
            {
                c.Open();
                using var cmd = c.CreateCommand();
                cmd.CommandText = """
                    with recursive numbers(n) as (select 1 union all select n+1 from numbers where n<510)
                    insert into facts(fact_id,fact_type,source_symbol,target_symbol)
                    select 'fan-'||n,'CallEdge','Private.Handler()','Private.Target-'||n from numbers;
                    """;
                cmd.ExecuteNonQuery();
            }
            Assert.Contains(WebFormsRawEvidenceAudit.Run(db, report), l => l.Contains("symbols=500|bounded=true"));
            var path = Path.Combine(Path.GetDirectoryName(report)!, "bounded-batch.json");
            Assert.Contains(WebFormsRawEvidenceAudit.Run(db, report, inspectionPath: path, inspectAllHandlers: true),
                l => l == "batchInspection=created|chains=1|handlers=1|boundedHandlers=1");
            using var batch = JsonDocument.Parse(File.ReadAllText(path));
            Assert.True(batch.RootElement.GetProperty("cases")[0].GetProperty("bounded").GetBoolean());
        });
    }

    [Fact]
    public void RawAuditRejectsWrongSnapshot()
    {
        WithFixture((db, report) =>
        {
            File.WriteAllText(report, File.ReadAllText(report).Replace("commit-one", "commit-two", StringComparison.Ordinal));
            Assert.Throws<InvalidDataException>(() => WebFormsRawEvidenceAudit.Run(db, report));
        });
    }

    private static void WithFixture(Action<string, string> test)
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var db = Path.Combine(dir, "index.sqlite");
            using (var c = new SqliteConnection($"Data Source={db};Pooling=False"))
            {
                c.Open();
                using var cmd = c.CreateCommand();
                cmd.CommandText = """
                    create table scan_manifest(scan_id text, commit_sha text);
                    insert into scan_manifest values('scan-one','commit-one');
                    create table facts(fact_id text, scan_id text default 'scan-one', commit_sha text default 'commit-one', fact_type text, source_symbol text, target_symbol text, evidence_tier text default 'Tier1Semantic', properties_json text default '{}', file_path text default 'Private.cs', start_line integer default 1, end_line integer default 1, rule_id text default 'test.evidence.v1');
                    insert into facts(fact_id,fact_type,target_symbol) values('handler','WebFormsHandlerResolved','Private.Handler()');
                    insert into facts(fact_id,fact_type,source_symbol,target_symbol) values('call','CallEdge','Private.Handler()','Private.Leaf()');
                    insert into facts(fact_id,fact_type,source_symbol,target_symbol) values('invoke-one','MethodInvoked','Private.Handler()','Private.Leaf()');
                    insert into facts(fact_id,fact_type,source_symbol,target_symbol) values('invoke-two','MethodInvoked','Private.Leaf()','Private.External()');
                    insert into facts(fact_id,fact_type,source_symbol,target_symbol,evidence_tier) values('syntax','CallEdge','Private.External()','Private.Unrelated()','Tier3SyntaxOrTextual');
                    """;
                cmd.ExecuteNonQuery();
            }
            var report = Path.Combine(dir, "report.json");
            File.WriteAllText(report, JsonSerializer.Serialize(new
            {
                schemaVersion = "webforms-modernization-packet.v1",
                sources = new[] { new { scanId = "scan-one", commitSha = "commit-one" } },
                eventChains = new[] { new { handlerFactId = "handler", bindingFactId = "handler", surfaceId = "private-surface", terminalKind = "", traversalObservation = new { stopState = "observed-downstream-without-supported-terminal" } } }
            }));
            test(db, report);
        }
        finally { Directory.Delete(dir, true); }
    }
}
