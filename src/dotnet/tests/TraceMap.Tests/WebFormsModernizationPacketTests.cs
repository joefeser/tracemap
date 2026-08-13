using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using TraceMap.Cli;
using TraceMap.Core;
using TraceMap.Reporting;
using TraceMap.Storage;

namespace TraceMap.Tests;

public sealed class WebFormsModernizationPacketTests
{
    [Fact]
    public async Task Synthetic_scan_index_packet_is_deterministic_typed_private_and_preserves_partial_evidence()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(Path.Combine(repo, "AreaA"));
        Directory.CreateDirectory(Path.Combine(repo, "AreaB"));
        File.WriteAllText(Path.Combine(repo, "Site.Master"), "<%@ Master Language=\"C#\" %><asp:ContentPlaceHolder runat=\"server\" ID=\"Body\" />");
        File.WriteAllText(Path.Combine(repo, "Widget.ascx"), "<%@ Control Language=\"C#\" %><asp:Label runat=\"server\" ID=\"Status\" />");
        File.WriteAllText(Path.Combine(repo, "AreaA", "Default.aspx"), """
            <%@ Page Language="C#" CodeBehind="Default.aspx.cs" Inherits="Sample.AreaA.Default" MasterPageFile="~/Site.Master" Title="private-marker-value" %>
            <%@ Register TagPrefix="uc" TagName="Widget" Src="~/Widget.ascx" %>
            <asp:Content runat="server" ContentPlaceHolderID="Body"><asp:Button runat="server" ID="Save" OnClick="Save_Click" /><uc:Widget runat="server" ID="Details" /></asp:Content>
            """);
        File.WriteAllText(Path.Combine(repo, "AreaA", "Default.aspx.cs"), """
            using System;
            namespace Sample.AreaA;
            public partial class Default
            {
                public Default() { Save.Click += (sender, args) => Save_Click(sender, args); }
                protected ButtonStub Save { get; } = new();
                protected void Save_Click(object sender, EventArgs e) { }
            }
            public sealed class ButtonStub { public event EventHandler? Click; }
            """);
        File.WriteAllText(Path.Combine(repo, "AreaB", "Default.aspx"), """
            <%@ Page Language="C#" CodeBehind="Default.aspx.cs" Inherits="Sample.AreaB.Default" %>
            <asp:Button runat="server" ID="Save" OnClick="Missing_Click" />
            """);
        File.WriteAllText(Path.Combine(repo, "AreaB", "Default.aspx.cs"), "namespace Sample.AreaB; public partial class Default { } ");

        var scan = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "scan")));
        const string fixtureCommit = "0123456789abcdef0123456789abcdef01234567";
        var committedManifest = scan.Manifest with { CommitSha = fixtureCommit };
        var committedFacts = scan.Facts.Select(fact => fact with { CommitSha = fixtureCommit }).ToArray();
        var index = Path.Combine(temp.Path, "index.sqlite");
        SqliteIndexWriter.Write(index, committedManifest, committedFacts);
        var indexHash = Hash(index);
        var firstOut = Path.Combine(temp.Path, "packet-a");
        var secondOut = Path.Combine(temp.Path, "packet-b");
        var first = await WebFormsModernizationPacketReporter.WriteAsync(new(index, firstOut));
        var second = await WebFormsModernizationPacketReporter.WriteAsync(new(index, secondOut));

        Assert.Equal(indexHash, Hash(index));
        Assert.Equal(await File.ReadAllBytesAsync(first.JsonPath), await File.ReadAllBytesAsync(second.JsonPath));
        Assert.Equal(await File.ReadAllBytesAsync(first.MarkdownPath), await File.ReadAllBytesAsync(second.MarkdownPath));
        Assert.Equal(WebFormsModernizationPacketReporter.SchemaVersion, first.Packet.SchemaVersion);
        Assert.Equal("local-only", first.Packet.ClaimLevel);
        Assert.False(string.IsNullOrWhiteSpace(first.Packet.Sources.Single().RepositoryId));
        Assert.DoesNotContain("synthetic-repo", first.Packet.Sources.Single().RepositoryId, StringComparison.Ordinal);
        Assert.Equal(4, first.Packet.Surfaces.Count);
        Assert.Equal(2, first.Packet.Surfaces.Count(surface => surface.SurfaceKind == "Page"));
        Assert.Equal(2, first.Packet.Surfaces.Where(surface => surface.SurfaceKind == "Page").Select(surface => surface.SurfaceId).Distinct().Count());
        Assert.Contains(first.Packet.EventChains, chain => chain.HandlerFactId is not null && chain.TerminalKind is null);
        Assert.Contains(first.Packet.Gaps, gap => gap.Classification == "NoBackendEvidence");
        Assert.Contains(first.Packet.Gaps, gap => gap.Classification == "MissingWebFormsHandler");
        Assert.Contains(first.Packet.Gaps, gap => gap.Classification == "DynamicWebFormsEventSubscription");
        Assert.Contains(first.Packet.StructuralSliceCandidates, candidate => candidate.SurfaceIds.Count > 1);
        var areaPage = first.Packet.Surfaces.Single(surface => surface.Evidence.FilePath == "AreaA/Default.aspx");
        var widget = first.Packet.Surfaces.Single(surface => surface.Evidence.FilePath == "Widget.ascx");
        Assert.Contains(widget.SurfaceId, areaPage.CompositionTargetIds);
        Assert.Contains(first.Packet.StructuralSliceCandidates, candidate => candidate.SurfaceIds.Contains(areaPage.SurfaceId)
            && candidate.SurfaceIds.Contains(widget.SurfaceId));
        Assert.All(first.Packet.Surfaces, surface => Assert.NotEqual("unknown", surface.Evidence.CoverageLabel));
        Assert.All(first.Packet.EventChains.SelectMany(chain => chain.Evidence), evidence => Assert.NotEqual("unknown", evidence.CoverageLabel));
        var typed = JsonSerializer.Deserialize<WebFormsModernizationPacket>(await File.ReadAllTextAsync(first.JsonPath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(typed);
        Assert.Equal(first.Packet.PacketId, typed.PacketId);
        var markdown = await File.ReadAllTextAsync(first.MarkdownPath);
        Assert.Contains(first.Packet.PacketId, markdown, StringComparison.Ordinal);
        Assert.Contains($"Surfaces: `{first.Packet.Summary.SurfaceCount}`", markdown, StringComparison.Ordinal);
        Assert.All(first.Packet.Surfaces, surface => Assert.Contains(surface.SurfaceId, markdown, StringComparison.Ordinal));
        Assert.All(first.Packet.EventChains, chain => Assert.Contains(chain.ChainId, markdown, StringComparison.Ordinal));
        Assert.Equal(first.Packet.Summary.GapCount, first.Packet.Gaps.Count);
        Assert.DoesNotContain(temp.Path, await File.ReadAllTextAsync(first.JsonPath), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(temp.Path, markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private-marker-value", await File.ReadAllTextAsync(first.JsonPath), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Existing_static_terminal_path_is_composed_without_runtime_claims()
    {
        using var temp = new TempDirectory();
        var manifest = Manifest("Succeeded");
        var surface = "webforms-surface:orders";
        var binding = Fact(manifest, FactTypes.WebFormsEventBindingDeclared, RuleIds.LegacyWebFormsEventBinding, "Pages/Orders.aspx", 12,
            source: "webforms-control:submit", target: "method:submit", contract: "Submit_Click",
            ("surfaceIdentity", surface), ("eventSourceIdentity", "webforms-control:submit"), ("eventName", "OnClick"), ("controlId", "Submit"), ("handlerName", "Submit_Click"), ("markupFile", "Pages/Orders.aspx"), ("coverageLabel", "bounded-static-webforms-event"));
        var handler = Fact(manifest, FactTypes.WebFormsHandlerResolved, RuleIds.LegacyWebFormsHandlerResolution, "Pages/Orders.aspx.cs", 24,
            source: "webforms-control:submit", target: "method:submit", contract: "Submit_Click",
            ("surfaceIdentity", surface), ("bindingFactId", binding.FactId), ("handlerSymbolId", "method:submit"), ("handlerSymbol", "method:submit"), ("handlerName", "Submit_Click"), ("controlId", "Submit"), ("eventName", "OnClick"), ("markupFile", "Pages/Orders.aspx"), ("pageTypeName", "Sample.Orders"), ("coverageLabel", "bounded-static-webforms-handler"));
        var page = Fact(manifest, FactTypes.WebFormsPageDeclared, RuleIds.LegacyWebFormsInventory, "Pages/Orders.aspx", 1,
            source: surface, target: "Sample.Orders", contract: "Orders.aspx",
            ("surfaceIdentity", surface), ("directiveKind", "Page"), ("coverageLabel", "bounded-static-webforms-inventory"));
        var call = Fact(manifest, FactTypes.CallEdge, "csharp.semantic.call.v1", "Pages/Orders.aspx.cs", 27,
            source: "method:submit", target: "method:save", contract: "Save", ("coverageLabel", "bounded-static-call"));
        var terminal = Fact(manifest, FactTypes.QueryPatternDetected, RuleIds.CSharpSyntaxQueryPattern, Path.Combine(temp.Path, "private", "Orders.cs"), 40,
            source: "method:save", target: "query-shape", contract: "query", ("operationName", "SELECT"), ("tableName", "orders"), ("columnNames", "id;state"), ("sqlSourceKind", "literal-string"), ("queryShapeHash", "shape-hash"), ("coverageLabel", "bounded-static-query"));
        var secondTerminal = Fact(manifest, FactTypes.PackageReferenced, RuleIds.ProjectFile, "App.csproj", 10,
            source: "method:save", target: "package-hash", contract: "package", ("dependencyGroup", "PackageReference"), ("dependencyScope", "runtime"), ("ecosystem", "nuget"), ("manifestKind", "csproj"), ("packageName", "package-hash"), ("packageManager", "nuget"), ("surfaceKind", "package-config"), ("version", "1.0.0"), ("coverageLabel", "bounded-static-package"));
        var unrelatedGap = Fact(manifest, FactTypes.AnalysisGap, RuleIds.LegacyWinFormsEventBinding, "Forms/Main.cs", 2,
            source: null, target: "winforms:main", contract: "gap", ("gapKind", "UnrelatedWinFormsGap"), ("coverageLabel", "reduced-static-winforms"));
        var laterPage = Fact(manifest, FactTypes.WebFormsPageDeclared, RuleIds.LegacyWebFormsInventory, "Pages/ZLater.aspx", 1,
            source: "webforms-surface:later", target: "Sample.Later", contract: "Later.aspx",
            ("surfaceIdentity", "webforms-surface:later"), ("directiveKind", "Page"), ("coverageLabel", "bounded-static-webforms-inventory"));
        var laterBinding = Fact(manifest, FactTypes.WebFormsEventBindingDeclared, RuleIds.LegacyWebFormsEventBinding, "Pages/ZLater.aspx", 12,
            source: "webforms-control:later", target: "method:later", contract: "Later_Click",
            ("surfaceIdentity", "webforms-surface:later"), ("eventSourceIdentity", "webforms-control:later"), ("eventName", "OnClick"), ("controlId", "Later"), ("handlerName", "Later_Click"), ("markupFile", "Pages/ZLater.aspx"), ("coverageLabel", "bounded-static-webforms-event"));
        var laterHandler = Fact(manifest, FactTypes.WebFormsHandlerResolved, RuleIds.LegacyWebFormsHandlerResolution, "Pages/ZLater.aspx.cs", 24,
            source: "webforms-control:later", target: "method:later", contract: "Later_Click",
            ("surfaceIdentity", "webforms-surface:later"), ("bindingFactId", laterBinding.FactId), ("handlerSymbolId", "method:later"), ("handlerSymbol", "method:later"), ("handlerName", "Later_Click"), ("controlId", "Later"), ("eventName", "OnClick"), ("markupFile", "Pages/ZLater.aspx"), ("pageTypeName", "Sample.Later"), ("coverageLabel", "bounded-static-webforms-handler"));
        var index = Path.Combine(temp.Path, "index.sqlite");
        SqliteIndexWriter.Write(index, manifest, [page, binding, handler, call, terminal, secondTerminal, unrelatedGap, laterPage, laterBinding, laterHandler]);

        var packet = await WebFormsModernizationPacketReporter.BuildAsync(new(index, Path.Combine(temp.Path, "unused"), MaxEventChains: 2));
        Assert.Equal(2, packet.EventChains.Count);
        Assert.All(packet.EventChains, chain =>
        {
            Assert.NotNull(chain.HandlerFactId);
            Assert.NotNull(chain.LegacyPathId);
            Assert.NotNull(chain.TerminalKind);
        });
        Assert.DoesNotContain(packet.Gaps, gap => gap.Classification == "UnrelatedWinFormsGap");
        Assert.True(packet.Summary.Truncated);
        Assert.Contains(packet.Gaps, gap => gap.Classification == "WebFormsModernizationEventChainLimitReached"
            && gap.SupportingFactIds.Contains(laterBinding.FactId));
        Assert.All(packet.EventChains.SelectMany(chain => chain.PathEvidence), evidence =>
        {
            Assert.False(string.IsNullOrWhiteSpace(evidence.RuleId));
            Assert.False(string.IsNullOrWhiteSpace(evidence.EvidenceTier));
            Assert.False(string.IsNullOrWhiteSpace(evidence.ExtractorVersion));
            Assert.Equal("unknown", evidence.CoverageLabel);
            Assert.True(evidence.FilePath is null || !Path.IsPathRooted(evidence.FilePath));
        });
        Assert.Contains(packet.Gaps, gap => gap.Classification is "LegacyPathEvidenceCoverageUnavailable" or "LegacyPathEvidenceProvenanceUnavailable");
        Assert.Equal("reduced-static-webforms-modernization", packet.Coverage);
        Assert.DoesNotContain(temp.Path, JsonSerializer.Serialize(packet), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("does not prove runtime", string.Join(' ', packet.Limitations), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Missing_coverage_and_limits_fail_closed_deterministically()
    {
        using var temp = new TempDirectory();
        var manifest = Manifest("FailedOrPartial");
        var first = Fact(manifest, FactTypes.WebFormsPageDeclared, RuleIds.LegacyWebFormsInventory, "A/Default.aspx", 1,
            source: "surface:a", target: "A.Default", contract: "Default.aspx", ("surfaceIdentity", "surface:a"), ("directiveKind", "Page"));
        var second = Fact(manifest, FactTypes.WebFormsPageDeclared, RuleIds.LegacyWebFormsInventory, "B/Default.aspx", 1,
            source: "surface:b", target: "B.Default", contract: "Default.aspx", ("surfaceIdentity", "surface:b"), ("directiveKind", "Page"));
        var third = Fact(manifest, FactTypes.WebFormsPageDeclared, RuleIds.LegacyWebFormsInventory, "C/Default.aspx", 1,
            source: "surface:c", target: "C.Default", contract: "Default.aspx", ("surfaceIdentity", "surface:c"), ("directiveKind", "Page"));
        var index = Path.Combine(temp.Path, "index.sqlite");
        SqliteIndexWriter.Write(index, manifest, [first, second, third]);
        await using (var connection = new SqliteConnection($"Data Source={index}"))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "update facts set extractor_version = '', start_line = 0 where fact_id = $id;";
            command.Parameters.AddWithValue("$id", first.FactId);
            await command.ExecuteNonQueryAsync();
        }

        var packet = await WebFormsModernizationPacketReporter.BuildAsync(new(index, Path.Combine(temp.Path, "unused"), MaxSurfaces: 1));
        Assert.True(packet.Summary.Truncated);
        Assert.Equal("reduced-static-webforms-modernization", packet.Coverage);
        Assert.Contains(packet.Gaps, gap => gap.Classification == "WebFormsModernizationSurfaceLimitReached");
        Assert.Contains(packet.Gaps, gap => gap.Classification == "EvidenceProvenanceUnavailable" && gap.SupportingFactIds.Contains(first.FactId));
        var retained = Assert.Single(packet.Surfaces);
        Assert.NotEqual("surface:a", retained.SurfaceId);
        Assert.Equal("unknown", retained.Evidence.CoverageLabel);
        Assert.Contains(packet.Gaps, gap => gap.Classification == "EvidenceCoverageLabelUnavailable" && gap.SupportingFactIds.Contains(retained.Evidence.FactId));
        Assert.Equal(packet.Summary.GapCount, packet.Gaps.Count);

        var gapBounded = await WebFormsModernizationPacketReporter.BuildAsync(new(
            index,
            Path.Combine(temp.Path, "gap-bounded"),
            MaxSurfaces: 10,
            MaxGaps: 1));
        Assert.True(gapBounded.Summary.Truncated);
        Assert.Single(gapBounded.Gaps);
        Assert.Equal("WebFormsModernizationGapLimitReached", gapBounded.Gaps[0].Classification);
    }

    [Fact]
    public async Task Cli_writes_both_packet_formats()
    {
        using var temp = new TempDirectory();
        var manifest = Manifest("Succeeded");
        var index = Path.Combine(temp.Path, "index.sqlite");
        SqliteIndexWriter.Write(index, manifest, []);
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var output = Path.Combine(temp.Path, "packet");
        var exit = await TraceMapCommand.RunAsync(["webforms-modernization", "--index", index, "--out", output], stdout, stderr);
        Assert.Equal(0, exit);
        Assert.Equal(string.Empty, stderr.ToString());
        Assert.True(File.Exists(Path.Combine(output, "webforms-modernization.json")));
        Assert.True(File.Exists(Path.Combine(output, "webforms-modernization.md")));

        stderr.GetStringBuilder().Clear();
        exit = await TraceMapCommand.RunAsync(
            ["webforms-modernization", "--index", index, "--out", output, "--max-surafces", "1"],
            stdout,
            stderr);
        Assert.Equal(1, exit);
        Assert.Contains("unsupported webforms-modernization option", stderr.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Combined_and_multi_manifest_indexes_fail_closed_with_stable_errors()
    {
        using var temp = new TempDirectory();
        var manifest = Manifest("Succeeded");
        var index = Path.Combine(temp.Path, "index.sqlite");
        SqliteIndexWriter.Write(index, manifest, []);
        await using (var connection = new SqliteConnection($"Data Source={index}"))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "create table index_sources(source_id text primary key);";
            await command.ExecuteNonQueryAsync();
        }

        var combined = await Assert.ThrowsAsync<InvalidDataException>(() =>
            WebFormsModernizationPacketReporter.BuildAsync(new(index, Path.Combine(temp.Path, "combined"))));
        Assert.Equal("WebFormsModernizationIndexUnsupported", combined.Message);

        await using (var connection = new SqliteConnection($"Data Source={index}"))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                drop table index_sources;
                insert into scan_manifest(scan_id, repo, commit_sha, scanner_version, scanned_at, analysis_level, build_status, manifest_json)
                select 'second-scan', repo, commit_sha, scanner_version, scanned_at, analysis_level, build_status, manifest_json
                from scan_manifest limit 1;
                """;
            await command.ExecuteNonQueryAsync();
        }

        var multiple = await Assert.ThrowsAsync<InvalidDataException>(() =>
            WebFormsModernizationPacketReporter.BuildAsync(new(index, Path.Combine(temp.Path, "multiple"))));
        Assert.Equal("WebFormsModernizationSnapshotInvalid", multiple.Message);
    }

    [Fact]
    public async Task Reduced_analysis_is_explicit_and_unknown_commit_identity_is_rejected()
    {
        using var temp = new TempDirectory();
        var reducedIndex = Path.Combine(temp.Path, "reduced.sqlite");
        SqliteIndexWriter.Write(reducedIndex, Manifest("Succeeded"), []);

        var reduced = await WebFormsModernizationPacketReporter.BuildAsync(new(
            reducedIndex,
            Path.Combine(temp.Path, "reduced-output")));
        Assert.Equal("reduced-static-webforms-modernization", reduced.Coverage);
        Assert.Contains(reduced.Gaps, gap => gap.Classification == "SourceAnalysisCoverageReduced"
            && gap.ScopeId == reduced.Sources.Single().ScanId);

        var unknownCommitIndex = Path.Combine(temp.Path, "unknown-commit.sqlite");
        SqliteIndexWriter.Write(unknownCommitIndex, Manifest("Succeeded") with { CommitSha = "unknown" }, []);
        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            WebFormsModernizationPacketReporter.BuildAsync(new(
                unknownCommitIndex,
                Path.Combine(temp.Path, "unknown-output"))));
        Assert.Equal("WebFormsModernizationCommitIdentityUnavailable", exception.Message);
    }

    private static ScanManifest Manifest(string buildStatus) => new(
        "scan-webforms-packet", "synthetic-repo", null, "dev", "0123456789abcdef0123456789abcdef01234567",
        "scanner-test", DateTimeOffset.Parse("2026-08-13T00:00:00Z"), "Level1SemanticAnalysisReduced", buildStatus,
        [], [], [], ["Synthetic fixture only."], ".", "scan-root-hash", "git-root-hash");

    private static CodeFact Fact(
        ScanManifest manifest,
        string factType,
        string ruleId,
        string path,
        int line,
        string? source,
        string? target,
        string? contract,
        params (string Key, string Value)[] properties) => FactFactory.Create(
            manifest, factType, ruleId, EvidenceTiers.Tier2Structural,
            new(path, line, line, null, "SyntheticFixture", "1.0"), sourceSymbol: source, targetSymbol: target, contractElement: contract,
            properties: new SortedDictionary<string, string>(properties.ToDictionary(item => item.Key, item => item.Value), StringComparer.Ordinal));

    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
}
