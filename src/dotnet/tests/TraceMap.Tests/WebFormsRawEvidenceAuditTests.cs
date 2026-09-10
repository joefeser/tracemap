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
                    create table facts(fact_id text, scan_id text default 'scan-one', commit_sha text default 'commit-one', fact_type text, source_symbol text, target_symbol text, evidence_tier text default 'Tier1Semantic', properties_json text default '{}');
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
                eventChains = new[] { new { handlerFactId = "handler", terminalKind = "", traversalObservation = new { stopState = "observed-downstream-without-supported-terminal" } } }
            }));
            test(db, report);
        }
        finally { Directory.Delete(dir, true); }
    }
}
