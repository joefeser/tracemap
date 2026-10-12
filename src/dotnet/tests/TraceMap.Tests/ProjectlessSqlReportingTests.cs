using TraceMap.Core;
using TraceMap.Reporting;
using TraceMap.Storage;
using System.Security.Cryptography;
using System.Text.Json;

namespace TraceMap.Tests;

public sealed class ProjectlessSqlReportingTests
{
    [Fact]
    public async Task Separate_wrapper_source_and_compiled_modes_keep_their_evidence_distinct()
    {
        var repository = new DirectoryInfo(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "AGENTS.md"))) repository = repository.Parent;
        var samples = Path.Combine(repository!.FullName, "samples/sql-database-matrix");
        using var temp = new TempDirectory();
        var appSource = await File.ReadAllTextAsync(Path.Combine(samples, "wrapper-website/App_Code/CatalogPage.vb"));
        var applicationOnly = Scan(Path.Combine(temp.Path, "application"), appSource);
        Assert.DoesNotContain(applicationOnly.Facts, f => f.FactType == FactTypes.DatabaseOperationCandidate);
        Assert.Contains(applicationOnly.Facts, f => f.FactType == FactTypes.CallEdge);
        Assert.Contains("Wrapper calls without a retained target-to-database path remain unresolved", MarkdownReportWriter.Build(applicationOnly));

        var root = Path.Combine(temp.Path, "together");
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "CatalogPage.vb"), appSource);
        File.Copy(Path.Combine(samples, "wrapper-library/SyntheticStore.vb"), Path.Combine(root, "SyntheticStore.vb"));
        WebFormsWizardNativeTests.InitGit(root);
        var together = ScanEngine.Scan(new ScanOptions(root, Path.Combine(temp.Path, "source-scan")));
        var candidates = together.Facts.Where(f => f.FactType == FactTypes.DatabaseOperationCandidate).ToArray();
        Assert.Equal(2, candidates.Length);
        Assert.All(candidates, f =>
        {
            Assert.Equal(EvidenceTiers.Tier3SyntaxOrTextual, f.EvidenceTier);
            Assert.Equal("unavailable", f.Properties["commandTextAvailability"]);
        });
        Assert.DoesNotContain("PublicCatalog_Save", JsonSerializer.Serialize(candidates));
        Assert.Contains("Showing 2 of 2 candidates", MarkdownReportWriter.Build(together));
        var index = Path.Combine(temp.Path, "source.sqlite");
        SqliteIndexWriter.Write(index, together.Manifest, together.Facts);
        var paths = await CombinedDependencyPathReporter.BuildReportAsync(new(index, temp.Path, FromSymbol:"CatalogPage.Search", ToSurface:"sql-query"));
        var handoff = GroupedCompiledPathHandoffBuilder.Create(paths, Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(index))));
        Assert.Contains(handoff.BackendExtraction!.Operations, o => o.Family == "database");

        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var binary = Path.Combine(samples, "wrapper-library/bin", configuration, "net472/SyntheticStore.dll");
        var admitted = ScanEngine.Scan(new ScanOptions(Path.Combine(temp.Path, "application/source"),
            Path.Combine(temp.Path, "compiled-scan"), CompiledInputPaths:[binary], IlBodyEvidence:true));
        Assert.Contains(admitted.Facts, f => f.FactType == FactTypes.ManagedIlCallObserved
            && f.Properties.GetValueOrDefault("targetIdentity", "").Contains("ExecuteNonQuery", StringComparison.Ordinal));
        Assert.DoesNotContain(admitted.Facts, f => f.FactType == FactTypes.DatabaseOperationCandidate);
        // The wrapper's IL is separately admitted, not a compiler-resolved application call.
    }

    private static ScanResult Scan(string parent, string source)
    {
        var root = Path.Combine(parent, "source");
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "Example.vb"), source);
        WebFormsWizardNativeTests.InitGit(root);
        return ScanEngine.Scan(new ScanOptions(root, Path.Combine(parent, "scan")));
    }

    [Theory]
    [InlineData("Imports System.Data", "SqlClient.SqlCommand", true)]
    [InlineData("Imports System.Data", "Other.SqlCommand", false)]
    [InlineData("Imports SqlClient = Other", "SqlClient.SqlCommand", false)]
    [InlineData("Imports Cmd = Other.SqlCommand", "Cmd", false)]
    [InlineData("Imports SqlClient = System.Data.SqlClient", "SqlClient.SqlCommand", true)]
    [InlineData("", "Global.System.Data.SqlClient.SqlCommand", true)]
    [InlineData("Imports System.Data", "Global.SqlClient.SqlCommand", false)]
    public void Qualified_receiver_preserves_namespace_and_alias_boundaries(string imports, string type, bool expected)
    {
        using var temp = new TempDirectory();
        var result = Scan(temp.Path, $"""
            {imports}
            Public Class Example
                Public Sub Run(command As {type})
                    command.ExecuteNonQuery()
                End Sub
            End Class
            """);
        var facts = result.Facts.Where(f => f.FactType == FactTypes.DatabaseOperationCandidate).ToArray();
        Assert.Equal(expected ? 1 : 0, facts.Length);
        Assert.All(facts, f => Assert.Equal(EvidenceTiers.Tier3SyntaxOrTextual, f.EvidenceTier));
        var report = MarkdownReportWriter.Build(result);
        Assert.Contains("not a complete database inventory", report);
        if (expected) Assert.Contains("Showing 1 of 1 candidates", report);
        else Assert.Contains("partial or unsupported coverage does not establish absence", report);
    }

    [Fact]
    public void Lifecycle_and_same_named_local_type_are_not_database_candidates()
    {
        using var temp = new TempDirectory();
        var result = Scan(temp.Path, """
            Imports System.Data
            Public Class SqlClient
                Public Class SqlCommand
                    Public Sub ExecuteNonQuery()
                    End Sub
                End Class
            End Class
            Public Class Example
                Public Sub Run(command As SqlClient.SqlCommand, connection As System.Data.SqlClient.SqlConnection)
                    command.ExecuteNonQuery()
                    connection.Open()
                    connection.Close()
                End Sub
            End Class
            """);
        Assert.DoesNotContain(result.Facts, f => f.FactType == FactTypes.DatabaseOperationCandidate);
    }

    [Fact]
    public void Candidate_display_reports_total_and_omission_count_deterministically()
    {
        using var temp = new TempDirectory();
        var result = Scan(temp.Path, """
            Public Class Example
                Public Sub Run(command As System.Data.Common.DbCommand)
                    command.ExecuteScalar()
                End Sub
            End Class
            """);
        var candidate = Assert.Single(result.Facts, f => f.FactType == FactTypes.DatabaseOperationCandidate);
        var facts = Enumerable.Range(0, 53).Select(i => candidate with { FactId = $"candidate-{i:D3}" }).ToArray();
        var report = MarkdownReportWriter.Build(result with { Facts = facts });
        Assert.Contains("Showing 50 of 53 candidates; 3 omitted", report);
        Assert.Equal(50, report.Split("- `ExecuteScalar` on `", StringSplitOptions.None).Length - 1);
        Assert.Equal(report, MarkdownReportWriter.Build(result with { Facts = facts.Reverse().ToArray() }));
    }
}
