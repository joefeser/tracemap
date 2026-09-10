using System.Text.Json;
using Microsoft.Data.Sqlite;
using TraceMap.Reporting;

namespace TraceMap.Tests;

public sealed class WebFormsRawEvidenceAuditTests
{
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
