using System.Text.Json;
using Microsoft.Data.Sqlite;
using TraceMap.Reporting;

namespace TraceMap.Tests;

public sealed class WebFormsDatabaseEvidenceAuditTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void CensusIsScopedAndPrivateAndRejectsWrongSnapshot(bool mismatch, bool missingFill)
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var db = Path.Combine(directory, "index.sqlite");
            using (var c = new SqliteConnection($"Data Source={db};Pooling=False"))
            {
                c.Open();
                using var q = c.CreateCommand();
                q.CommandText = """
                    create table scan_manifest(scan_id text,commit_sha text);
                    insert into scan_manifest values('s','c');
                    create table facts(scan_id text default 's',commit_sha text default 'c',source_symbol text default 'Private.Method()',fact_type text,target_symbol text,evidence_tier text default 'Tier1Semantic',properties_json text default '{}');
                    insert into facts(fact_type,target_symbol,properties_json) values
                    ('ObjectCreated','System.Data.SqlClient.SqlCommand','{"assignedTo":"privateCommand","sql":"PRIVATE_SQL"}'),
                    ('ObjectCreated','System.Data.SqlClient.SqlDataAdapter','{"assignedTo":"privateAdapter"}'),
                    ('MethodInvoked','System.Data.Common.DbDataAdapter.Fill(System.Data.DataSet)','{}'),
                    ('PropertyAccessed','System.Data.Common.DbCommand.CommandType','{}'),
                    ('ObjectCreated','System.Data.SqlClient.SqlCommandBuilder','{}');
                    insert into facts(source_symbol,fact_type,target_symbol) values('Other.Method()','SqlCommandDetected','System.Data.SqlClient.SqlCommand');
                    """;
                q.ExecuteNonQuery();
                if (missingFill)
                {
                    q.CommandText = "delete from facts where fact_type='MethodInvoked'";
                    q.ExecuteNonQuery();
                }
            }
            var inspection = Path.Combine(directory, "inspection.json");
            File.WriteAllText(inspection, JsonSerializer.Serialize(new { schemaVersion = "webforms-local-inspection.v1", scanId = "s", commitSha = mismatch ? "wrong" : "c", hops = new[] { new { caller = "Private.Method()", callee = "System.Data.Common.DbDataAdapter.Fill(System.Data.DataSet)" } } }));
            if (mismatch || missingFill)
            {
                Assert.Throws<InvalidDataException>(() => WebFormsDatabaseEvidenceAudit.Run(db, inspection));
                return;
            }
            var before = File.ReadAllBytes(db);
            var output = WebFormsDatabaseEvidenceAudit.Run(db, inspection);
            Assert.Contains("retainedFacts=5", output);
            Assert.Contains("semanticSignal=commandtype-property|count=1", output);
            Assert.Contains("semanticSignal=command-construction|count=1", output);
            Assert.Contains("semanticSignal=fill-invocation|count=1", output);
            Assert.Contains("semanticSignal=command-assigned-variable-retained|count=1", output);
            Assert.Contains("factType=SqlCommandDetected|count=0", output);
            Assert.DoesNotContain("PRIVATE_SQL", string.Join('\n', output));
            Assert.DoesNotContain("Private.Method", string.Join('\n', output));
            Assert.DoesNotContain("privateCommand", string.Join('\n', output));
            Assert.Equal(before, File.ReadAllBytes(db));
        }
        finally { Directory.Delete(directory, true); }
    }
}
