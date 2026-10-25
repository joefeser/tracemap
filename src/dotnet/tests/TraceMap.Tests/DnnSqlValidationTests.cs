using System.Text.Json;
using TraceMap.Core;
using TraceMap.Reporting;
using TraceMap.Storage;

namespace TraceMap.Tests;

public sealed class DnnSqlValidationTests
{
    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public async Task Synthetic_contract_controls_retain_witnesses_without_claiming_schema_comparison(string language)
    {
        var repository = new DirectoryInfo(AppContext.BaseDirectory);
        while (repository is not null && !File.Exists(Path.Combine(repository.FullName, "AGENTS.md"))) repository = repository.Parent;
        var sample = Path.Combine(repository!.FullName, "samples/dnn-sql-validation");
        using var temp = new TempDirectory();
        var scan = ScanEngine.Scan(new ScanOptions(sample, Path.Combine(temp.Path, "scan"),
            ProjectPaths: [Path.Combine(sample, language, language == "vb" ? "Contracts.vbproj" : "Contracts.csproj")]));
        Assert.Equal("Succeeded", scan.Manifest.BuildStatus);
        var operations = scan.Facts.Where(f => f.FactType == FactTypes.DatabaseOperationCandidate
            && f.EvidenceTier == EvidenceTiers.Tier1Semantic).ToArray();
        Assert.Equal(12, operations.Length);
        CodeFact Call(string name) => Assert.Single(operations, f => f.SourceSymbol?.Contains("." + name + "(", StringComparison.Ordinal) == true);
        foreach (var name in new[] { "ReadGood", "SizeMismatch", "TypeMismatch", "DirectionMismatch", "MissingColumn", "RenamedColumn", "ConditionalShape", "DynamicShape", "UnknownSchema", "Channels" })
        {
            var call = Call(name);
            Assert.Equal("StoredProcedure", call.Properties["commandTypeEvidence"]);
            Assert.Equal("hash-only-constant", call.Properties["commandTextAvailability"]);
            Assert.NotEmpty(call.RuleId);
            Assert.NotEqual("unknown", call.CommitSha);
            Assert.True(call.Evidence.StartLine > 0);
            Assert.True(call.Evidence.EndLine >= call.Evidence.StartLine);
            Assert.NotEmpty(call.Evidence.ExtractorId);
            Assert.NotEmpty(call.Evidence.ExtractorVersion);
            // Current witness summaries have no admitted per-parameter size/result schema.
            Assert.DoesNotContain("parameterSize", call.Properties.Keys);
            Assert.DoesNotContain("schemaComparison", call.Properties.Keys);
        }
        Assert.Equal("Text", Call("TextControl").Properties["commandTypeEvidence"]);
        Assert.Equal("SqlDbType.Int", Call("TypeMismatch").Properties["parameterTypeNames"]);
        Assert.Equal("Output", Call("DirectionMismatch").Properties["parameterDirectionKinds"]);
        Assert.Equal("Input", Call("ReadGood").Properties["parameterDirectionKinds"]);
        Assert.Equal(Call("ReadGood").Properties["commandTextHash"], Call("SizeMismatch").Properties["commandTextHash"]);
        Assert.NotEqual(Call("ReadGood").Properties["commandTextHash"], Call("UnknownSchema").Properties["commandTextHash"]);
        Assert.Equal("Output;ReturnValue", Call("Channels").Properties["parameterDirectionKinds"]);
        Assert.Equal("dynamic", Call("Execute").Properties["commandTextAvailability"]);
        Assert.DoesNotContain(operations, f => f.SourceSymbol?.Contains(".UnknownWrapper(", StringComparison.Ordinal) == true);
        Assert.DoesNotContain("dbo.ReadItem", JsonSerializer.Serialize(operations));
        var ddl = ScanEngine.Scan(new ScanOptions(Path.Combine(sample, "sql"), Path.Combine(temp.Path, "ddl")));
        Assert.Contains(ddl.Facts, f => f.FactType == FactTypes.SqlTextUsed && f.Evidence.FilePath.EndsWith("schema.sql", StringComparison.Ordinal));
        var report = MarkdownReportWriter.Build(scan);
        Assert.Contains("not a complete database inventory", report);
        Assert.Contains("ExecuteReader", report);
        var index = Path.Combine(temp.Path, "index.sqlite");
        SqliteIndexWriter.Write(index, scan.Manifest, scan.Facts);
        Assert.True(new FileInfo(index).Length > 0);
        var paths = await CombinedDependencyPathReporter.BuildReportAsync(new(index, temp.Path,
            FromSymbol: "Contracts.Wrapper", ToSurface: "sql-query"));
        Assert.NotEmpty(paths.Paths);
        var shallow = await CombinedDependencyPathReporter.BuildReportAsync(new(index, temp.Path,
            FromSymbol: "Contracts.Wrapper", ToSurface: "sql-query", MaxDepth: 1));
        Assert.Contains(shallow.Gaps, g => g.GapKind == "TruncatedByLimit");
        Assert.Contains(paths.Paths, p => p.Nodes.Any(n => n.DatabaseCallEvidence?.MethodName == "ExecuteScalar"));
        var handoff = GroupedCompiledPathHandoffBuilder.Create(paths,
            Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(index))));
        Assert.Contains(handoff.BackendExtraction!.Operations, o => o.Family == "database");
        var written = await GroupedCompiledPathReportWriter.WriteAsync(handoff, Path.Combine(temp.Path, "paths"));
        Assert.Contains("Backend extraction inventory", await File.ReadAllTextAsync(written.HtmlPath));
        var repeated = ScanEngine.Scan(new ScanOptions(sample, Path.Combine(temp.Path, "repeat"),
            ProjectPaths: [Path.Combine(sample, language, language == "vb" ? "Contracts.vbproj" : "Contracts.csproj")]));
        Assert.Equal(operations.Select(f => f.FactId), repeated.Facts.Where(f => f.FactType == FactTypes.DatabaseOperationCandidate && f.EvidenceTier == EvidenceTiers.Tier1Semantic).Select(f => f.FactId));
    }

    [Theory]
    [InlineData("cs", "public class Page { public object Read(dynamic provider) => provider.GetRoleGroups(1); }")]
    [InlineData("vb", "Public Class Page\nPublic Function Read(provider As Object) As Object\nReturn provider.GetRoleGroups(1)\nEnd Function\nEnd Class")]
    public void Missing_wrapper_identity_cannot_become_a_database_terminal(string extension, string source)
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "Page." + extension), source);
        WebFormsWizardNativeTests.InitGit(temp.Path);
        var scan = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, "out")));
        Assert.DoesNotContain(scan.Facts, f => f.FactType == FactTypes.DatabaseOperationCandidate);
        Assert.Contains("partial or unsupported coverage does not establish absence", MarkdownReportWriter.Build(scan));
    }
}
