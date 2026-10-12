using System.Text.Json;
using System.Security.Cryptography;
using TraceMap.Cli;
using TraceMap.Reporting;

namespace TraceMap.Tests;

/// <summary>Uses public CLI entry points, not an in-memory graph assembled by the test.</summary>
public sealed class WebFormsOperatorWorkflowTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData("attached")]
    [InlineData("separate")]
    [InlineData("separate-dll-only")]
    [InlineData("reversed")]
    [InlineData("missing")]
    public async Task WebForms_operator_source_compiled_and_separate_dll_reports(string layout)
    {
        using var temp = new TempDirectory();
        var retainedRoot = Environment.GetEnvironmentVariable("TRACEMAP_OPERATOR_ROOT");
        var outputRoot = retainedRoot is null ? temp.Path : Path.Combine(retainedRoot, layout);
        if (retainedRoot is not null)
        {
            Assert.False(Directory.Exists(outputRoot), "Refusing to overwrite a previous operator run.");
            Directory.CreateDirectory(outputRoot);
        }
        var splitProvider = layout is "separate" or "separate-dll-only" or "reversed";
        var root = FindRepo();
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var binaries = Path.Combine(root, "samples/fixture-build/lazy-constructor/bin", configuration, "net48");
        var website = Path.Combine(root, "samples/messy-dotnet-workspace/vb-lazy-constructor");
        var provider = Path.Combine(root, "samples/messy-dotnet-workspace/vb-lazy-logging-provider");
        var siteScan = Path.Combine(outputRoot, "site");
        var dllScan = Path.Combine(outputRoot, "dll");
        var combined = Path.Combine(outputRoot, "combined.sqlite");
        var scan = new List<string> { "scan", "--repo", website, "--out", siteScan,
            "--compiled-input", Path.Combine(binaries, "PublicLazy.Website.dll"), "--il-body-evidence" };
        if (layout == "attached") scan.AddRange(["--compiled-input", Path.Combine(binaries, "PublicLazy.Framework.dll")]);
        await Run(scan.ToArray());
        var combine = new List<string> { "combine", "--index", Path.Combine(siteScan, "index.sqlite"),
            "--label", "website", "--out", combined };
        if (splitProvider)
        {
            var providerScan = new List<string> { "scan", "--repo", provider, "--out", dllScan, "--compiled-input",
                Path.Combine(binaries, "PublicLazy.Framework.dll"), "--il-body-evidence" };
            if (layout == "separate-dll-only") providerScan.AddRange(["--exclude", "**/*.vb"]);
            await Run(providerScan.ToArray());
            combine.AddRange(["--index", Path.Combine(dllScan, "index.sqlite"), "--label", "provider"]);
            if (layout == "reversed") combine = ["combine", "--index", Path.Combine(dllScan, "index.sqlite"),
                "--label", "provider", "--index", Path.Combine(siteScan, "index.sqlite"), "--label", "website", "--out", combined];
        }
        await Run(combine.ToArray());
        var originalIndex = SHA256.HashData(await File.ReadAllBytesAsync(combined));
        await Run("report", "--index", combined, "--out", Path.Combine(outputRoot, "report"));
        Assert.True(File.Exists(Path.Combine(outputRoot, "report/dependency-report.md")));
        foreach (var file in new[] { "scan-manifest.json", "facts.ndjson", "index.sqlite", "report.md", "logs/analyzer.log" })
            Assert.True(File.Exists(Path.Combine(siteScan, file)), file);

        var symbols = File.ReadLines(Path.Combine(siteScan, "facts.ndjson")).Select(line => JsonDocument.Parse(line)).ToArray();
        string handler;
        try
        {
            handler = Assert.Single(symbols, doc => doc.RootElement.GetProperty("factType").GetString() == "ManagedMethodDeclared"
                && doc.RootElement.GetProperty("properties").GetProperty("metadataName").GetString() == "Profile_Click")
                .RootElement.GetProperty("targetSymbol").GetString()!;
        }
        finally { foreach (var doc in symbols) doc.Dispose(); }

        async Task<CombinedDependencyPathReport> Paths(string name, int maxPaths = 256, params string[] extra)
        {
            var output = Path.Combine(outputRoot, name);
            await Run(["paths", "--index", combined, "--out", output, "--format", "json",
                "--from-symbol", handler, "--exact-from-symbol", "--from-source", "website",
                "--to-surface", "database-api", "--max-depth", "20", "--max-paths", maxPaths.ToString(System.Globalization.CultureInfo.InvariantCulture), .. extra]);
            return JsonSerializer.Deserialize<CombinedDependencyPathReport>(
                await File.ReadAllTextAsync(Path.Combine(output, "paths-report.json")), Json)!;
        }
        var all = await Paths("all");
        Assert.Equal(splitProvider ? 2 : 1, all.Summary.SourceCount);
        if (layout == "missing")
        {
            Assert.Empty(all.Paths);
            Assert.NotEmpty(all.Gaps);
            Assert.Equal(originalIndex, SHA256.HashData(await File.ReadAllBytesAsync(combined)));
            return;
        }
        Assert.Equal(3, all.Paths.Count);
        bool Method(CombinedPathNode node, string name) => node.SymbolId?.Contains($"|method:{name.Length}:{name}|", StringComparison.Ordinal) == true;
        var dynamic = Assert.Single(all.Paths, path => path.Nodes.Any(node => Method(node, "GetEmail")));
        var ordered = dynamic.Nodes.ToList();
        var getter = ordered.FindIndex(node => Method(node, "get_EmployeeInfo"));
        var constructor = ordered.FindIndex(node => node.SymbolId?.Contains("names:15:ProfileEmployee|", StringComparison.Ordinal) == true
            && node.SymbolId.Contains("|constructor:5:.ctor|", StringComparison.Ordinal));
        var profile = ordered.FindIndex(node => Method(node, "GetProfile"));
        var email = ordered.FindIndex(node => Method(node, "GetEmail"));
        var execute = ordered.FindIndex(node => Method(node, "ExecuteSql"));
        Assert.True(getter >= 0 && constructor > getter && profile > constructor && email > profile && execute > email);
        Assert.Equal("SqlCommand.ExecuteScalar", dynamic.Nodes.Last().SurfaceName);
        Assert.Equal("symbolic-string-composition", dynamic.Nodes.Last().CommandBinding!.CommandTextFromPath!.State);
        Assert.Equal("1", dynamic.Nodes.Last().CommandBinding!.CommandTypeFromPath!.Origin.Identity);
        var audit = Assert.Single(all.Paths, path => path.Nodes.Any(node => Method(node, "WriteAudit")));
        Assert.Equal("4", audit.Nodes.Last().CommandBinding!.CommandTypeFromPath!.Origin.Identity);
        Assert.Equal("method-local-constant", audit.Nodes.Last().CommandBinding!.CommandTextFromPath!.State);
        var fill = await Paths("fill", 256, "--surface-name", "DbDataAdapter.Fill");
        Assert.Single(fill.Paths);
        Assert.Equal("DbDataAdapter.Fill", fill.Paths.Single().Nodes.Last().SurfaceName);
        Assert.DoesNotContain(fill.Paths, path => path.Nodes.Any(node => Method(node, "GetEmail")));
        Assert.DoesNotContain(all.Gaps, gap => gap.GapKind == "TruncatedByLimit");
        var capped = await Paths("capped", 1);
        Assert.Single(capped.Paths);
        Assert.Contains(capped.Gaps, gap => gap.GapKind == "TruncatedByLimit" && gap.Reason == "path");
        Assert.Contains(all.Paths, path => JsonSerializer.Serialize(path.Nodes, Json) == JsonSerializer.Serialize(capped.Paths.Single().Nodes, Json));
        var repeat = await Paths("repeat");
        Assert.Equal(JsonSerializer.Serialize(all.Paths, Json), JsonSerializer.Serialize(repeat.Paths, Json));
        Assert.Equal(JsonSerializer.Serialize(all.Gaps, Json), JsonSerializer.Serialize(repeat.Gaps, Json));
        Assert.Equal(originalIndex, SHA256.HashData(await File.ReadAllBytesAsync(combined)));
    }

    private static async Task Run(params string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        Assert.True(await TraceMapCommand.RunAsync(args, output, error) == 0,
            $"{args[0]} failed: {error}\n{output}");
    }

    private static string FindRepo()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Fixture repository not found.");
    }
}
