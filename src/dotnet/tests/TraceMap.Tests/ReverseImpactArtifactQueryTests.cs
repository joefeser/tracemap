using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using TraceMap.Cli;
using TraceMap.Core;
using TraceMap.Storage;

namespace TraceMap.Tests;

public sealed class ReverseImpactArtifactQueryTests
{
    private const string Commit = "0123456789012345678901234567890123456789";
    private const string Seed = "symbol:csharp:Target.Service.Call(System.String,System.Int32)";
    private const string Caller = "symbol:csharp:Caller.Controller.Get";

    [Fact]
    public async Task Standard_index_loads_exact_snapshot_and_cli_emits_deterministic_read_only_json()
    {
        using var temp = new TempDirectory();
        var index = Path.Combine(temp.Path, "index.sqlite");
        var firstOutput = Path.Combine(temp.Path, "first.json");
        var secondOutput = Path.Combine(temp.Path, "second.json");
        var facts = new[]
        {
            Relationship("fact-call", Caller, "Caller.Controller.Get()", Seed, "Target.Service.Call()", 17),
            Gap("fact-gap", Seed)
        };
        SqliteIndexWriter.Write(index, Manifest(), facts);
        var indexHash = Hash(index);

        var artifact = await ReverseImpactArtifactReader.ReadAsync(index);

        Assert.Equal("scan-reverse-impact", artifact.Manifest.ScanId);
        Assert.Equal(Commit, artifact.Manifest.CommitSha);
        Assert.Equal(["fact-call", "fact-gap"], artifact.Facts.Select(fact => fact.FactId));

        using var firstStdout = new StringWriter();
        using var firstStderr = new StringWriter();
        var firstExit = await TraceMapCommand.RunAsync(
            ["reverse-impact", "--index", index, "--selector", Seed, "--depth", "1", "--out", firstOutput],
            firstStdout,
            firstStderr);
        using var secondStdout = new StringWriter();
        using var secondStderr = new StringWriter();
        var secondExit = await TraceMapCommand.RunAsync(
            ["reverse-impact", "--index", index, "--selector", Seed, "--depth", "1", "--out", secondOutput],
            secondStdout,
            secondStderr);

        Assert.Equal(0, firstExit);
        Assert.Equal(0, secondExit);
        Assert.Equal(string.Empty, firstStderr.ToString());
        Assert.Equal(string.Empty, secondStderr.ToString());
        Assert.Equal(await File.ReadAllBytesAsync(firstOutput), await File.ReadAllBytesAsync(secondOutput));
        Assert.Equal(indexHash, Hash(index));
        var result = JsonSerializer.Deserialize<ReverseImpactResult>(
            await File.ReadAllTextAsync(firstOutput),
            JsonOptions.Stable)!;
        Assert.Equal(ReverseImpactContract.SchemaVersion, result.SchemaVersion);
        Assert.Equal(ReverseImpactResolutions.Resolved, result.Resolution);
        Assert.Equal("scan-reverse-impact", result.Snapshot!.ScanId);
        Assert.Equal(Commit, result.Snapshot.CommitSha);
        var impact = Assert.Single(result.Impacts);
        Assert.Equal(Caller, impact.Symbol.SymbolId);
        var hop = Assert.Single(impact.Path);
        Assert.Equal(Caller, hop.SourceSymbolId);
        Assert.Equal(Seed, hop.TargetSymbolId);
        Assert.Equal("SourceToTarget", hop.OriginalDirection);
        Assert.Equal("TargetToSource", hop.TraversalDirection);
        Assert.Equal("Source.cs", hop.Evidence.FilePath);
        Assert.Equal(17, hop.Evidence.StartLine);
        Assert.Equal(RuleIds.CSharpSemanticCallGraph, hop.RuleId);
        Assert.Equal(EvidenceTiers.Tier1Semantic, hop.EvidenceTier);
        Assert.Contains(result.Gaps, gap =>
            gap.GapKind == ReverseImpactGapKinds.AnalysisGap
            && gap.RelatedSymbolIds.Contains(Seed, StringComparer.Ordinal));
    }

    [Fact]
    public async Task Cli_requires_explicit_depth_and_never_replaces_the_input_index()
    {
        using var temp = new TempDirectory();
        var index = Path.Combine(temp.Path, "index.sqlite");
        SqliteIndexWriter.Write(index, Manifest(), [Relationship("fact-call", Caller, "Caller", Seed, "Target", 1)]);
        var originalHash = Hash(index);

        using var missingDepthOutput = new StringWriter();
        using var missingDepthError = new StringWriter();
        var missingDepth = await TraceMapCommand.RunAsync(
            ["reverse-impact", "--index", index, "--selector", Seed, "--out", Path.Combine(temp.Path, "result.json")],
            missingDepthOutput,
            missingDepthError);
        using var replaceOutput = new StringWriter();
        using var replaceError = new StringWriter();
        var replace = await TraceMapCommand.RunAsync(
            ["reverse-impact", "--index", index, "--selector", Seed, "--depth", "1", "--out", index],
            replaceOutput,
            replaceError);

        Assert.Equal(1, missingDepth);
        Assert.Contains("requires --depth <1-20>", missingDepthError.ToString(), StringComparison.Ordinal);
        Assert.Equal(1, replace);
        Assert.Contains("must not replace the input index", replaceError.ToString(), StringComparison.Ordinal);
        Assert.Equal(originalHash, Hash(index));
    }

    [Fact]
    public async Task Cli_rejects_an_existing_symbolic_link_alias_of_the_input_index()
    {
        using var temp = new TempDirectory();
        var index = Path.Combine(temp.Path, "index.sqlite");
        var alias = Path.Combine(temp.Path, "output.json");
        SqliteIndexWriter.Write(index, Manifest(), [Relationship("fact-call", Caller, "Caller", Seed, "Target", 1)]);
        File.CreateSymbolicLink(alias, index);
        var originalHash = Hash(index);
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = await TraceMapCommand.RunAsync(
            ["reverse-impact", "--index", index, "--selector", Seed, "--depth", "1", "--out", alias],
            stdout,
            stderr);

        Assert.Equal(1, exitCode);
        Assert.Contains("output must be a new file", stderr.ToString(), StringComparison.Ordinal);
        Assert.Equal(originalHash, Hash(index));
    }

    [Fact]
    public async Task Reader_fails_closed_for_combined_or_mixed_snapshot_artifacts()
    {
        using var temp = new TempDirectory();
        var unrelated = Path.Combine(temp.Path, "combined.sqlite");
        await using (var connection = new SqliteConnection($"Data Source={unrelated}"))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "create table index_sources (source_index_id text);";
            await command.ExecuteNonQueryAsync();
        }

        var unsupported = await Assert.ThrowsAsync<ReverseImpactArtifactException>(
            () => ReverseImpactArtifactReader.ReadAsync(unrelated));
        Assert.Equal("ReverseImpactArtifactSchemaUnsupported", unsupported.ErrorCode);

        var mixed = Path.Combine(temp.Path, "mixed.sqlite");
        var wrongSnapshot = Relationship("fact-call", Caller, "Caller", Seed, "Target", 1) with
        {
            CommitSha = "ffffffffffffffffffffffffffffffffffffffff"
        };
        SqliteIndexWriter.Write(mixed, Manifest(), [wrongSnapshot]);

        var mixedError = await Assert.ThrowsAsync<ReverseImpactArtifactException>(
            () => ReverseImpactArtifactReader.ReadAsync(mixed));
        Assert.Equal("ReverseImpactArtifactMixedSnapshot", mixedError.ErrorCode);
    }

    [Fact]
    public async Task Reader_rejects_partial_standard_schema_and_incomplete_manifest()
    {
        using var temp = new TempDirectory();
        var partial = Path.Combine(temp.Path, "partial.sqlite");
        await using (var connection = new SqliteConnection($"Data Source={partial}"))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                create table scan_manifest (
                  scan_id text primary key,
                  repo text not null,
                  commit_sha text not null,
                  manifest_json text not null
                );
                create table facts (fact_id text primary key);
                """;
            await command.ExecuteNonQueryAsync();
        }

        var schemaError = await Assert.ThrowsAsync<ReverseImpactArtifactException>(
            () => ReverseImpactArtifactReader.ReadAsync(partial));
        Assert.Equal("ReverseImpactArtifactSchemaUnsupported", schemaError.ErrorCode);

        var invalidManifest = Path.Combine(temp.Path, "invalid-manifest.sqlite");
        SqliteIndexWriter.Write(
            invalidManifest,
            Manifest(),
            [Relationship("fact-call", Caller, "Caller", Seed, "Target", 1)]);
        await using (var connection = new SqliteConnection($"Data Source={invalidManifest}"))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "update scan_manifest set manifest_json = $manifest;";
            command.Parameters.AddWithValue(
                "$manifest",
                JsonSerializer.Serialize(new
                {
                    ScanId = "scan-reverse-impact",
                    RepoName = "synthetic",
                    CommitSha = Commit
                }));
            await command.ExecuteNonQueryAsync();
        }

        var manifestError = await Assert.ThrowsAsync<ReverseImpactArtifactException>(
            () => ReverseImpactArtifactReader.ReadAsync(invalidManifest));
        Assert.Equal("ReverseImpactArtifactSnapshotInvalid", manifestError.ErrorCode);
    }

    [Fact]
    public async Task Cli_reports_only_a_stable_error_code_for_malformed_persisted_facts()
    {
        using var temp = new TempDirectory();
        var index = Path.Combine(temp.Path, "index.sqlite");
        var output = Path.Combine(temp.Path, "result.json");
        const string sensitiveFactId = "private-fact-identifier";
        SqliteIndexWriter.Write(
            index,
            Manifest(),
            [Relationship(sensitiveFactId, Caller, "Caller", Seed, "Target", 1)]);
        await using (var connection = new SqliteConnection($"Data Source={index}"))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "update facts set fact_type = '';";
            await command.ExecuteNonQueryAsync();
        }

        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var exitCode = await TraceMapCommand.RunAsync(
            ["reverse-impact", "--index", index, "--selector", Seed, "--depth", "1", "--out", output],
            stdout,
            stderr);

        Assert.Equal(1, exitCode);
        Assert.Equal("error: MissingRequiredFactField.\n", stderr.ToString().ReplaceLineEndings("\n"));
        Assert.DoesNotContain(sensitiveFactId, stderr.ToString(), StringComparison.Ordinal);
        Assert.False(File.Exists(output));
    }

    [Fact]
    public async Task Reader_fails_closed_instead_of_partially_loading_an_oversized_snapshot()
    {
        using var temp = new TempDirectory();
        var index = Path.Combine(temp.Path, "index.sqlite");
        SqliteIndexWriter.Write(
            index,
            Manifest(),
            [
                Relationship("fact-one", Caller, "Caller", Seed, "Target", 1),
                Relationship("fact-two", "symbol:csharp:Other", "Other", Seed, "Target", 2)
            ]);

        var error = await Assert.ThrowsAsync<ReverseImpactArtifactException>(
            () => ReverseImpactArtifactReader.ReadAsync(index, maxFacts: 1));

        Assert.Equal("ReverseImpactArtifactFactLimitExceeded", error.ErrorCode);
    }

    [Fact]
    public async Task Human_selector_ambiguity_is_preserved_in_machine_output_without_traversal()
    {
        using var temp = new TempDirectory();
        var index = Path.Combine(temp.Path, "index.sqlite");
        var outputPath = Path.Combine(temp.Path, "ambiguous.json");
        var facts = new[]
        {
            Relationship("fact-one", "symbol:csharp:Caller.One", "Same()", Seed, "Target", 1),
            Relationship("fact-two", "symbol:csharp:Caller.Two", "Same()", Seed, "Target", 2)
        };
        SqliteIndexWriter.Write(index, Manifest(), facts);
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exitCode = await TraceMapCommand.RunAsync(
            ["reverse-impact", "--index", index, "--selector", "Same()", "--depth", "1", "--out", outputPath],
            stdout,
            stderr);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, stderr.ToString());
        var result = JsonSerializer.Deserialize<ReverseImpactResult>(
            await File.ReadAllTextAsync(outputPath),
            JsonOptions.Stable)!;
        Assert.Equal(ReverseImpactResolutions.Ambiguous, result.Resolution);
        Assert.Null(result.Seed);
        Assert.Equal(["symbol:csharp:Caller.One", "symbol:csharp:Caller.Two"], result.Candidates.Select(item => item.SymbolId));
        Assert.Empty(result.Impacts);
        Assert.Contains(result.Gaps, gap => gap.GapKind == ReverseImpactGapKinds.AmbiguousSelector);
    }

    [Fact]
    public async Task Semantic_http_boundary_survives_scan_persistence_and_explicit_reverse_impact()
    {
        using var temp = new TempDirectory();
        var project = Path.Combine(temp.Path, "src", "HttpSample");
        Directory.CreateDirectory(project);
        File.WriteAllText(Path.Combine(project, "HttpSample.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(project, "Client.cs"), """
            using System.Net.Http;
            using System.Threading.Tasks;

            namespace HttpSample;

            public sealed class Client
            {
                public Task FetchAsync(HttpClient client) => client.GetAsync("/health");
            }
            """);

        var scan = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, ".tracemap")));
        var http = Assert.Single(scan.Facts, fact =>
            fact.FactType == FactTypes.HttpCallDetected
            && fact.EvidenceTier == EvidenceTiers.Tier1Semantic);
        var sourceId = http.Properties.GetValueOrDefault("sourceSymbolId");
        var targetId = http.Properties.GetValueOrDefault("targetSymbolId");

        Assert.False(string.IsNullOrWhiteSpace(sourceId));
        Assert.False(string.IsNullOrWhiteSpace(targetId));
        var index = Path.Combine(temp.Path, "index.sqlite");
        SqliteIndexWriter.Write(index, scan.Manifest, scan.Facts);
        var artifact = await ReverseImpactArtifactReader.ReadAsync(index);
        var result = ReverseImpactTraversal.Analyze(
            artifact.Facts,
            new ReverseImpactOptions(targetId!, 1, ["http"]));
        var impact = Assert.Single(result.Impacts, item => item.Symbol.SymbolId == sourceId);
        Assert.Equal(http.FactId, Assert.Single(impact.Path).FactId);
    }

    private static ScanManifest Manifest() => new(
        "scan-reverse-impact",
        "synthetic",
        null,
        "dev",
        Commit,
        "tracemap/0.1.0",
        DateTimeOffset.Parse("2026-08-08T00:00:00Z"),
        "Level1SemanticAnalysis",
        "Succeeded",
        [],
        [],
        ["net10.0"],
        []);

    private static CodeFact Relationship(
        string factId,
        string sourceId,
        string sourceDisplay,
        string targetId,
        string targetDisplay,
        int line) => new(
            factId,
            "scan-reverse-impact",
            "synthetic",
            Commit,
            "Synthetic.csproj",
            FactTypes.CallEdge,
            RuleIds.CSharpSemanticCallGraph,
            EvidenceTiers.Tier1Semantic,
            sourceDisplay,
            targetDisplay,
            null,
            new EvidenceSpan("Source.cs", line, line, $"hash-{factId}", "csharp-semantic", "1.2.3"),
            new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["sourceSymbolId"] = sourceId,
                ["sourceSymbolDisplayName"] = sourceDisplay,
                ["sourceSymbolKind"] = "Method",
                ["sourceSymbolLanguage"] = "csharp",
                ["targetSymbolId"] = targetId,
                ["targetSymbolDisplayName"] = targetDisplay,
                ["targetSymbolKind"] = "Method",
                ["targetSymbolLanguage"] = "csharp"
            });

    private static CodeFact Gap(string factId, string symbolId) => new(
        factId,
        "scan-reverse-impact",
        "synthetic",
        Commit,
        "Synthetic.csproj",
        FactTypes.AnalysisGap,
        RuleIds.AnalyzerCapabilitySemantic,
        EvidenceTiers.Tier4Unknown,
        null,
        symbolId,
        "Synthetic reduced-coverage fixture.",
        new EvidenceSpan("Source.cs", 23, 23, null, "csharp-semantic", "1.2.3"),
        new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["message"] = "Synthetic reduced-coverage fixture.",
            ["targetSymbolId"] = symbolId,
            ["targetSymbolDisplayName"] = "Target.Service.Call()",
            ["targetSymbolKind"] = "Method",
            ["targetSymbolLanguage"] = "csharp"
        });

    private static string Hash(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
}
