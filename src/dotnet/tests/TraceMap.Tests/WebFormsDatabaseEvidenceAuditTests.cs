using System.Text.Json;
using Microsoft.Data.Sqlite;
using TraceMap.Reporting;

namespace TraceMap.Tests;

public sealed class WebFormsDatabaseEvidenceAuditTests
{
    [Fact]
    public void Real_framework_symbols_survive_scan_storage_and_database_audit()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "Sample.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>
            """);
        File.WriteAllText(Path.Combine(temp.Path, "Sample.cs"), """
            using System.Data;
            using System.Data.Common;
            public class Sample {
                public void Click(DbCommand command, DbDataAdapter adapter) => Load(command, adapter);
                void Load(DbCommand command, DbDataAdapter adapter) => Fetch(command, adapter);
                void Fetch(DbCommand command, DbDataAdapter adapter) {
                    command.CommandType = CommandType.StoredProcedure;
                    var observed = command.CommandType;
                    command.CommandType = CommandType.Text;
                    adapter.Fill(new DataSet());
                }
            }
            """);
        File.WriteAllText(Path.Combine(temp.Path, "Sample.aspx"), """
            <%@ Page Language="C#" CodeBehind="Sample.cs" Inherits="Sample" %>
            <asp:Button ID="Load" runat="server" OnClick="Click" />
            """);
        var scan = TraceMap.Core.ScanEngine.Scan(new TraceMap.Core.ScanOptions(temp.Path, Path.Combine(temp.Path, "out")));
        Assert.Equal("Succeeded", scan.Manifest.BuildStatus);
        var fill = Assert.Single(scan.Facts, f => f.FactType == "MethodInvoked" && f.TargetSymbol!.StartsWith("global::System.Data.Common.DbDataAdapter.Fill(", StringComparison.Ordinal));
        Assert.Equal("global::System.Data.Common.DbDataAdapter adapter", fill.Properties["receiverSymbol"]);
        Assert.True(fill.Properties.ContainsKey("receiverSymbolId"));
        Assert.Contains(scan.Facts, f => f.FactType == "PropertyAccessed" && f.TargetSymbol!.EndsWith(".CommandType", StringComparison.Ordinal) && !f.Properties.ContainsKey("assignedValueSymbol"));
        var index = Path.Combine(temp.Path, "index.sqlite");
        TraceMap.Storage.SqliteIndexWriter.Write(index, scan.Manifest, scan.Facts);
        var inspection = Path.Combine(temp.Path, "inspection.json");
        File.WriteAllText(inspection, JsonSerializer.Serialize(new { schemaVersion = "webforms-local-inspection.v1", scanId = scan.Manifest.ScanId, commitSha = scan.Manifest.CommitSha, hops = new[] { new { caller = fill.SourceSymbol, callee = fill.TargetSymbol } } }));
        var output = WebFormsDatabaseEvidenceAudit.Run(index, inspection);
        Assert.Contains("semanticSignal=fill-receiver-symbol-retained|count=1", output);
        Assert.Contains("storedProcedureSimpleAssignments=1", output);
        Assert.Contains("fillReceiverIdentity=retained", output);
        Assert.Contains("adapterFillLocalNameMatch=0", output);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, true)]
    public void CensusIsScopedAndPrivateAndRejectsWrongSnapshot(bool mismatch, bool missingFill, bool qualified)
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
                    ('ArgumentPassed','System.Data.SqlClient.SqlDataAdapter.SqlDataAdapter(System.Data.SqlClient.SqlCommand)', '{"argumentSymbol":"privateCommand"}'),
                    ('MethodInvoked','System.Data.Common.DbDataAdapter.Fill(System.Data.DataSet)','{"receiverSymbol":"privateAdapter"}'),
                    ('PropertyAccessed','System.Data.Common.DbCommand.CommandType','{}'),
                    ('ObjectCreated','System.Data.SqlClient.SqlCommandBuilder','{}');
                    insert into facts(source_symbol,fact_type,target_symbol) values('Other.Method()','SqlCommandDetected','System.Data.SqlClient.SqlCommand');
                    """;
                q.ExecuteNonQuery();
                if (qualified)
                {
                    q.CommandText = """
                        update facts set source_symbol='global::' || source_symbol, target_symbol='global::' || target_symbol;
                        update facts set target_symbol='global::System.Data.Common.DbDataAdapter.Fill(global::System.Data.DataSet dataSet)' where fact_type='MethodInvoked';
                        insert into facts(source_symbol,fact_type,target_symbol) values('Private.Method()','MethodInvoked','System.Data.Common.DbDataAdapter.Fill(System.Data.DataSet)');
                        """;
                    q.ExecuteNonQuery();
                }
                if (missingFill)
                {
                    q.CommandText = "delete from facts where fact_type='MethodInvoked' and source_symbol=$caller";
                    q.Parameters.AddWithValue("$caller", qualified ? "global::Private.Method()" : "Private.Method()");
                    q.ExecuteNonQuery();
                }
            }
            var inspection = Path.Combine(directory, "inspection.json");
            File.WriteAllText(inspection, JsonSerializer.Serialize(new { schemaVersion = "webforms-local-inspection.v1", scanId = "s", commitSha = mismatch ? "wrong" : "c", hops = new[] { new { caller = qualified ? "global::Private.Method()" : "Private.Method()", callee = qualified ? "global::System.Data.Common.DbDataAdapter.Fill(global::System.Data.DataSet dataSet)" : "System.Data.Common.DbDataAdapter.Fill(System.Data.DataSet)" } } }));
            if (mismatch || missingFill)
            {
                var diagnostics = new List<string>();
                var error = Assert.Throws<InvalidDataException>(() => WebFormsDatabaseEvidenceAudit.Run(db, inspection, diagnostics.Add));
                Assert.Equal(mismatch ? "RawAuditProvenanceMismatch" : "RawAuditFillIndexWitnessMissing", error.Message);
                if (missingFill) Assert.Contains("exactCallerFrameworkFillWitness=missing", diagnostics);
                return;
            }
            var before = File.ReadAllBytes(db);
            var output = WebFormsDatabaseEvidenceAudit.Run(db, inspection);
            Assert.Contains("retainedFacts=6", output);
            Assert.Contains("semanticSignal=commandtype-property|count=1", output);
            Assert.Contains("semanticSignal=command-construction|count=1", output);
            Assert.Contains("semanticSignal=fill-invocation|count=1", output);
            Assert.Contains("semanticSignal=command-assigned-variable-retained|count=1", output);
            Assert.Contains("commandAdapterLocalNameMatch=1", output);
            Assert.Contains("fillReceiverIdentity=retained", output);
            Assert.Contains("adapterFillLocalNameMatch=1", output);
            Assert.Contains("storedProcedureSimpleAssignments=0", output);
            Assert.Contains("factType=SqlCommandDetected|count=0", output);
            Assert.DoesNotContain("PRIVATE_SQL", string.Join('\n', output));
            Assert.DoesNotContain("Private.Method", string.Join('\n', output));
            Assert.DoesNotContain("privateCommand", string.Join('\n', output));
            Assert.Equal(before, File.ReadAllBytes(db));
            File.WriteAllText(inspection, File.ReadAllText(inspection).Replace("System.Data.Common.DbDataAdapter.Fill", "Private.Adapter.Fill", StringComparison.Ordinal));
            var shapeDiagnostics = new List<string>();
            var shapeError = Assert.Throws<InvalidDataException>(() => WebFormsDatabaseEvidenceAudit.Run(db, inspection, shapeDiagnostics.Add));
            Assert.Equal("RawAuditNoRecognizedFillHop", shapeError.Message);
            Assert.Contains("inspectionFillNamedHops=1", shapeDiagnostics);
            Assert.Contains("inspectionRecognizedFrameworkFillHops=0", shapeDiagnostics);
            Assert.DoesNotContain("Private", string.Join('\n', shapeDiagnostics));
        }
        finally { Directory.Delete(directory, true); }
    }
}
