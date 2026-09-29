using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Mono.Cecil;
using Mono.Cecil.Cil;
using TraceMap.Cli;
using TraceMap.Core;
using TraceMap.Reporting;
using Xunit.Abstractions;

namespace TraceMap.Tests;

[Collection("WebForms isolated allocation")]
public sealed class WebFormsNativeScaleTests(ITestOutputHelper output)
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };

    [Fact]
    public async Task Native_subprocess_scale_retains_declared_pages_and_records_observed_resource_usage()
    {
        var large = Environment.GetEnvironmentVariable("TRACEMAP_WEBFORMS_NATIVE_SCALE") == "1";
        var retained = Environment.GetEnvironmentVariable("TRACEMAP_WEBFORMS_SCALE_OUT");
        if (large && string.IsNullOrWhiteSpace(retained))
            throw new InvalidOperationException("Set TRACEMAP_WEBFORMS_SCALE_OUT to a new owned public benchmark directory.");
        var root = retained is null ? Path.Combine(Path.GetTempPath(), "tracemap-native-scale-" + Guid.NewGuid().ToString("N"))
            : Path.GetFullPath(retained);
        Assert.False(Directory.Exists(root), "Benchmark output must be a new owned directory; existing runs are never overwritten.");
        Directory.CreateDirectory(root);
        try
        {
            var cases = new List<ScaleCase>();
            foreach (var pages in large ? new[] { 32, 256 } : new[] { 1, 8 })
            {
                var fixture = new Corpus(Path.Combine(root, "pages-" + pages.ToString(CultureInfo.InvariantCulture)), pages);
                var beforeSource = RosterHash(fixture.Source);
                var beforePublished = RosterHash(fixture.Published);
                var phases = new List<PhaseUsage>();
                phases.Add(await RunCli(fixture.Root, "prepare", ["webforms-review", "prepare", "--config", fixture.ConfigPath,
                    "--out", fixture.Evidence, "--attest-exact-source-commit", fixture.Commit]));
                phases.Add(await RunCli(fixture.Root, "preflight", ["webforms-review", "preflight", "--config",
                    Path.Combine(fixture.Evidence, "review-config.local.json"), "--out", fixture.Run]));
                phases.Add(await RunCli(fixture.Root, "run", ["webforms-review", "run", "--run", fixture.Run]));
                var checkpoint = JsonSerializer.Deserialize<WebFormsReviewCheckpoint>(File.ReadAllText(
                    Path.Combine(fixture.Run, "checkpoints", "0004.json")), JsonOptions)!;
                Assert.Equal("reports-completed-review-only", checkpoint.State);
                Assert.Equal(pages, checkpoint.Reports!.Surfaces);
                Assert.True(checkpoint.Reports.CompiledPaths >= pages * 4, "Every declared page must retain all four compiled terminal branches.");
                var handoffPath = Path.Combine(fixture.Run, checkpoint.Reports.ReportAttempt, "handoff.local.json");
                var handoff = JsonSerializer.Deserialize<NativeWebFormsReviewHandoff>(File.ReadAllText(handoffPath), JsonOptions)!;
                Assert.Equal("review-only-static-not-runtime", handoff.ClaimLevel);
                Assert.Equal(pages, handoff.Packet.Surfaces.Count);
                var compiled = JsonSerializer.Deserialize<GroupedCompiledPathHandoff>(File.ReadAllText(
                    Path.Combine(fixture.Run, checkpoint.Reports.ReportAttempt, handoff.CompiledHandoffRelativePath)), JsonOptions)!;
                Assert.Equal(handoff.CompiledHandoffSha256, Hash(Path.Combine(fixture.Run,
                    checkpoint.Reports.ReportAttempt, handoff.CompiledHandoffRelativePath)));
                var restored = GroupedCompiledPathHandoffBuilder.Restore(compiled);
                Assert.Equal(checkpoint.Reports.CompiledPaths, restored.Paths.Count);
                Assert.DoesNotContain(restored.Gaps, gap => gap.GapKind == "ProjectlessPublishMemberWorkLimit");
                for (var page = 0; page < pages; page++)
                {
                    var handler = $"Synthetic.Page{page:D4}.Page_Load(Object,EventArgs)";
                    var branches = restored.Paths.Where(path => path.Nodes[0].SymbolId == handler)
                        .SelectMany(path => path.Nodes.Select(node => node.SymbolId))
                        .Where(symbol => symbol is not null && symbol.Contains($"|names:9:Store{page:D4}|", StringComparison.Ordinal))
                        .ToArray();
                    for (var branch = 0; branch < 4; branch++)
                        Assert.Contains(branches, symbol => symbol!.Contains($"|method:6:Fetch{branch}|", StringComparison.Ordinal));
                }
                Assert.Equal(beforeSource, RosterHash(fixture.Source));
                Assert.Equal(beforePublished, RosterHash(fixture.Published));
                var artifactsBefore = RosterHash(fixture.Run);
                phases.Add(await RunCli(fixture.Root, "resume", ["webforms-review", "resume", "--run", fixture.Run]));
                Assert.Equal(artifactsBefore, RosterHash(fixture.Run));
                var item = new ScaleCase(pages, fixture.SourceBytes, beforeSource, beforePublished,
                    Hash(fixture.ConfigPath), DirectoryBytes(fixture.Published), DirectoryBytes(fixture.Evidence), DirectoryBytes(fixture.Run),
                    checkpoint.FactCount, checkpoint.Reports.CompiledPaths, handoff.Coverage, handoff.Packet.Summary.Truncated,
                    restored.Summary.Truncated,
                    handoff.Gaps.OrderBy(value => value, StringComparer.Ordinal).ToArray(), phases);
                cases.Add(item);
                output.WriteLine($"nativeScale.pages={pages};sourceBytes={item.SourceBytes};publishedBytes={item.PublishedBytes};retainedDiskBytes={item.EvidenceBytes + item.RunBytes};facts={item.Facts};compiledPaths={item.CompiledPaths};coverage={item.Coverage};packetTruncated={item.PacketTruncated};compiledTruncated={item.CompiledTruncated}");
                foreach (var phase in phases)
                    output.WriteLine($"nativeScale.phase={phase.Phase};elapsedMs={phase.ElapsedMilliseconds};peakResidentBytes={phase.PeakResidentBytes?.ToString(CultureInfo.InvariantCulture) ?? "unavailable"};measurement={phase.Measurement}");
            }
            Assert.Equal(cases[0].SourceBytes * 8, cases[1].SourceBytes);
            Assert.Equal(cases[0].Pages * 8, cases[1].Pages);
            if (large)
            {
                Assert.True(cases[0].SourceBytes >= 2 * 1024 * 1024);
                Assert.True(cases[1].SourceBytes >= 16 * 1024 * 1024);
                Assert.All(cases.SelectMany(item => item.Phases), phase => Assert.True(phase.PeakResidentBytes > 0,
                    "Representative acceptance requires an OS peak metric, not samples or allocated-byte counts."));
            }
            var input = new { cases = cases.Select(item => new { item.Pages, item.SourceBytes, item.SourceRosterSha256,
                item.PublishedRosterSha256, item.ConfigSha256 }).ToArray() };
            var receipt = new { schemaVersion = "diagnostic.webforms.native-scale.v1", ruleId = "diagnostic.webforms.native-scale.v1",
                evidenceTier = EvidenceTiers.Tier4Unknown, visibility = "local-only",
                generatorSha256 = Hash(typeof(WebFormsNativeScaleTests).Assembly.Location),
                cliGeneratorSha256 = Hash(CliPath()), boundedInputSha256 = HashBytes(JsonSerializer.SerializeToUtf8Bytes(input, JsonOptions)),
                scope = large ? "public-synthetic-32-256-pages-and-8x-source-bytes" : "public-synthetic-ci-smoke-1-8-pages",
                claimLevel = "review-only-static-not-runtime", cases,
                limitations = new[] { "Generated PE/IL fixtures are not an aspnet_compiler acceptance run.",
                    "CLI OS peak excludes fixture generation and is not a simultaneous aggregate working-set measure.",
                    "Retained disk bytes exclude OS temporary sorter peak and transient private graph files.",
                    "Source bytes do not predict arbitrary graph fan-out; private Windows acceptance remains separate." } };
            File.WriteAllBytes(Path.Combine(root, "native-scale.receipt.json"), JsonSerializer.SerializeToUtf8Bytes(receipt, JsonOptions));
        }
        finally
        {
            // Only the unretained synthetic CI fixture is disposable. An explicit
            // benchmark directory is retained, including failures, for inspection.
            if (retained is null && Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private static async Task<PhaseUsage> RunCli(string root, string phase, string[] args)
    {
        var unix = OperatingSystem.IsMacOS() || OperatingSystem.IsLinux();
        if (unix) Assert.True(File.Exists("/usr/bin/time"), "OS timing utility is required for peak resident measurements.");
        var host = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? Environment.ProcessPath!;
        Assert.Contains("dotnet", Path.GetFileNameWithoutExtension(host), StringComparison.OrdinalIgnoreCase);
        var timing = Path.Combine(root, phase + ".usage.txt");
        using var process = new Process { StartInfo = new(unix ? "/usr/bin/time" : host)
            { WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false } };
        if (unix)
        {
            process.StartInfo.ArgumentList.Add(OperatingSystem.IsMacOS() ? "-l" : "-v");
            process.StartInfo.ArgumentList.Add("-o"); process.StartInfo.ArgumentList.Add(timing);
            process.StartInfo.ArgumentList.Add(host);
        }
        process.StartInfo.ArgumentList.Add(CliPath());
        foreach (var arg in args) process.StartInfo.ArgumentList.Add(arg);
        process.StartInfo.Environment["LC_ALL"] = "C";
        var clock = Stopwatch.StartNew();
        Assert.True(process.Start());
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(20));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch { if (!process.HasExited) process.Kill(entireProcessTree: true); throw; }
        clock.Stop();
        File.WriteAllText(Path.Combine(root, phase + ".stdout.local.log"), await stdout);
        File.WriteAllText(Path.Combine(root, phase + ".stderr.local.log"), await stderr);
        Assert.Equal(0, process.ExitCode);
        long? peak = null;
        if (unix)
        {
            var text = File.ReadAllText(timing);
            var match = Regex.Match(text, OperatingSystem.IsMacOS() ? @"(?m)^\s*(\d+)\s+maximum resident set size\s*$"
                : @"Maximum resident set size \(kbytes\):\s*(\d+)", RegexOptions.CultureInvariant);
            Assert.True(match.Success, "OS peak resident metric was not found; do not substitute a sampled working set.");
            peak = checked(long.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) * (OperatingSystem.IsMacOS() ? 1 : 1024));
            Assert.True(peak > 0);
        }
        return new(phase, clock.ElapsedMilliseconds, peak, OperatingSystem.IsMacOS() ? "macos-time-rusage-maxrss-bytes"
            : OperatingSystem.IsLinux() ? "linux-time-maxrss-kib-converted-to-bytes" : "os-peak-unavailable");
    }

    private sealed record PhaseUsage(string Phase, long ElapsedMilliseconds, long? PeakResidentBytes, string Measurement);
    private sealed record ScaleCase(int Pages, long SourceBytes, string SourceRosterSha256, string PublishedRosterSha256,
        string ConfigSha256, long PublishedBytes, long EvidenceBytes, long RunBytes, long? Facts, int CompiledPaths,
        string Coverage, bool PacketTruncated, bool CompiledTruncated, IReadOnlyList<string> Gaps, IReadOnlyList<PhaseUsage> Phases);

    private sealed class Corpus
    {
        public string Root { get; }
        public string Source => Path.Combine(Root, "source");
        public string Published => Path.Combine(Root, "published");
        public string Evidence => Path.Combine(Root, "evidence");
        public string Run => Path.Combine(Root, "run");
        public string ConfigPath => Path.Combine(Root, "review-config.local.json");
        public string Commit { get; }
        public long SourceBytes { get; }
        public Corpus(string root, int pages)
        {
            Root = root;
            Directory.CreateDirectory(Path.Combine(Source, "Pages")); Directory.CreateDirectory(Path.Combine(Source, "App_Code"));
            Directory.CreateDirectory(Path.Combine(Published, "bin"));
            var maps = new List<string>(); var sourcePaths = new List<string>();
            for (var number = 0; number < pages; number++)
            {
                var name = $"Page{number:D4}";
                WriteSized(Path.Combine(Source, "Pages", name + ".aspx"),
                    $"<%@ Page Language=\"VB\" CodeFile=\"{name}.aspx.vb\" Inherits=\"Synthetic.{name}\" AutoEventWireup=\"true\" %>\n", 256, markup: true);
                WriteSized(Path.Combine(Source, "Pages", name + ".aspx.vb"),
                    $"Imports System\nNamespace Synthetic\nPublic Class {name}\n Public Sub Page_Load(sender As Object, e As EventArgs)\n  Store{number:D4}.Entry(\"public_proc\")\n End Sub\nEnd Class\nEnd Namespace\n", 65536);
                WriteSized(Path.Combine(Source, "App_Code", $"Store{number:D4}.vb"),
                    $"Namespace Synthetic\nPublic Class Store{number:D4}\n Public Shared Sub Entry(name As String)\n End Sub\n Public Shared Sub Entry(number As Integer)\n End Sub\nEnd Class\nEnd Namespace\n", 4096);
                sourcePaths.AddRange(["Pages/" + name + ".aspx", "Pages/" + name + ".aspx.vb", $"App_Code/Store{number:D4}.vb"]);
                var map = name + ".aspx.compiled"; maps.Add(map);
                File.WriteAllText(Path.Combine(Published, map), $"<preserve virtualPath=\"/public/Pages/{name}.aspx\" assembly=\"Synthetic.WebSite\" type=\"Synthetic.{name}\"/>\n");
            }
            SourceBytes = DirectoryBytes(Source);
            CreateAssemblies(pages);
            Git(Source, "init", "-q"); Git(Source, "config", "user.name", "Public scale fixture");
            Git(Source, "config", "user.email", "public@example.invalid"); Git(Source, "config", "core.autocrlf", "false");
            Git(Source, "remote", "add", "origin", "https://example.invalid/public-scale.git");
            Git(Source, "add", "."); Git(Source, "commit", "-qm", "Public deterministic scale source");
            Commit = GitMetadataProvider.Detect(Source).CommitSha;
            var config = new WebFormsReviewConfig(WebFormsReviewPreflightCommand.ConfigSchema, "fresh", Source, Commit,
                "projectless", null, [], ["Pages", "App_Code"], "all", [], Published,
                ["bin/Synthetic.WebSite.dll", "bin/Synthetic.Data.dll"], [], [], [], maps.ToArray(), null,
                new WebFormsReviewBudgets(GraphMaxPaths: 4096) { MaxPublishInputFiles = 8192 }, PublishSourceRelativePaths: sourcePaths.ToArray());
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(config, JsonOptions));
            var generator = new { schemaVersion = "diagnostic.webforms.synthetic-corpus.v1", ruleId = "diagnostic.webforms.synthetic-corpus.v1",
                evidenceTier = EvidenceTiers.Tier4Unknown, visibility = "local-only", generatorSha256 = Hash(typeof(Corpus).Assembly.Location),
                boundedInputSha256 = RosterHash(Source), pages, sourceBytes = SourceBytes,
                artifacts = Directory.GetFiles(Published, "*", SearchOption.AllDirectories).OrderBy(path => path, StringComparer.Ordinal)
                    .Select(path => new { relativePath = Path.GetRelativePath(Published, path).Replace('\\', '/'), sha256 = Hash(path) }).ToArray(),
                claimLevel = "generated-pe-il-not-aspnet-compiled" };
            File.WriteAllText(Path.Combine(Root, "corpus-generator.receipt.json"), JsonSerializer.Serialize(generator, JsonOptions));
        }
        private void CreateAssemblies(int pages)
        {
            using var data = AssemblyDefinition.CreateAssembly(new("Synthetic.Data", new Version(1, 0)), "Synthetic.Data", ModuleKind.Dll);
            using var site = AssemblyDefinition.CreateAssembly(new("Synthetic.WebSite", new Version(1, 0)), "Synthetic.WebSite", ModuleKind.Dll);
            var dm = data.MainModule; var sm = site.MainModule;
            dm.Mvid = new Guid("10000000-0000-0000-0000-000000000001"); sm.Mvid = new Guid("10000000-0000-0000-0000-000000000002");
            var framework = new AssemblyNameReference("System.Data", new Version(4, 0, 0, 0)); dm.AssemblyReferences.Add(framework);
            var adapter = new TypeReference("System.Data.Common", "DbDataAdapter", dm, framework);
            var dataset = new TypeReference("System.Data", "DataSet", dm, framework);
            var fill = new MethodReference("Fill", dm.TypeSystem.Int32, adapter) { HasThis = true }; fill.Parameters.Add(new(dataset));
            for (var number = 0; number < pages; number++)
            {
                var store = new TypeDefinition("Synthetic", $"Store{number:D4}", Mono.Cecil.TypeAttributes.Public, dm.TypeSystem.Object); dm.Types.Add(store);
                var entry = Method(store, "Entry", dm.TypeSystem.Void); entry.Parameters.Add(new(dm.TypeSystem.String));
                var overload = Method(store, "Entry", dm.TypeSystem.Void); overload.Parameters.Add(new(dm.TypeSystem.Int32));
                overload.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
                for (var branch = 0; branch < 4; branch++)
                {
                    var fetch = Method(store, "Fetch" + branch, dm.TypeSystem.Void);
                    fetch.Body.Instructions.Add(Instruction.Create(OpCodes.Ldnull)); fetch.Body.Instructions.Add(Instruction.Create(OpCodes.Ldnull));
                    fetch.Body.Instructions.Add(Instruction.Create(OpCodes.Callvirt, fill)); fetch.Body.Instructions.Add(Instruction.Create(OpCodes.Pop));
                    fetch.Body.Instructions.Add(Instruction.Create(OpCodes.Ret)); entry.Body.Instructions.Add(Instruction.Create(OpCodes.Call, fetch));
                }
                entry.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
                var page = new TypeDefinition("Synthetic", $"Page{number:D4}", Mono.Cecil.TypeAttributes.Public, sm.TypeSystem.Object); sm.Types.Add(page);
                var handler = new MethodDefinition("Page_Load", Mono.Cecil.MethodAttributes.Public, sm.TypeSystem.Void); page.Methods.Add(handler);
                handler.Parameters.Add(new(sm.TypeSystem.Object));
                handler.Parameters.Add(new(new TypeReference("System", "EventArgs", sm, sm.TypeSystem.CoreLibrary)));
                handler.Body.Instructions.Add(Instruction.Create(OpCodes.Ldstr, "public_proc"));
                handler.Body.Instructions.Add(Instruction.Create(OpCodes.Call, sm.ImportReference(entry)));
                handler.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
            }
            data.Write(Path.Combine(Published, "bin", "Synthetic.Data.dll")); site.Write(Path.Combine(Published, "bin", "Synthetic.WebSite.dll"));
        }
        private static MethodDefinition Method(TypeDefinition type, string name, TypeReference result)
        { var method = new MethodDefinition(name, Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.Static, result); type.Methods.Add(method); return method; }
    }

    private static void WriteSized(string path, string text, int bytes, bool markup = false)
    {
        var suffix = markup ? "<!--" : "'"; var end = markup ? "-->\n" : "\n";
        var value = text + suffix + new string(' ', bytes - Encoding.UTF8.GetByteCount(text + suffix + end)) + end;
        File.WriteAllText(path, value, new UTF8Encoding(false));
        Assert.Equal(bytes, new FileInfo(path).Length);
    }
    private static string CliPath()
    {
        var config = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
                return Path.Combine(directory.FullName, "src", "dotnet", "TraceMap.Cli", "bin", config, "net10.0", "tracemap.dll");
        throw new InvalidOperationException("CLI build unavailable");
    }
    private static void Git(string root, params string[] args)
    {
        using var process = new Process { StartInfo = new("git") { WorkingDirectory = root, UseShellExecute = false,
            RedirectStandardOutput = true, RedirectStandardError = true } };
        foreach (var arg in args) process.StartInfo.ArgumentList.Add(arg);
        process.Start(); var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        Assert.True(process.WaitForExit(30_000)); Task.WaitAll(stdout, stderr); Assert.Equal(0, process.ExitCode);
    }
    private static long DirectoryBytes(string root) => Directory.GetFiles(root, "*", SearchOption.AllDirectories)
        .Where(path => !Path.GetRelativePath(root, path).Split(Path.DirectorySeparatorChar).Contains(".git", StringComparer.Ordinal))
        .Sum(path => new FileInfo(path).Length);
    private static string RosterHash(string root)
    {
        var entries = Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            .Where(path => !Path.GetRelativePath(root, path).Split(Path.DirectorySeparatorChar).Contains(".git", StringComparer.Ordinal))
            .Select(path => new { path = Path.GetRelativePath(root, path).Replace('\\', '/'), sha256 = Hash(path) })
            .OrderBy(item => item.path, StringComparer.Ordinal).ToArray();
        return HashBytes(JsonSerializer.SerializeToUtf8Bytes(entries, JsonOptions));
    }
    private static string Hash(string path) { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant(); }
    private static string HashBytes(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
