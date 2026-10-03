using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Diagnostics;
using Mono.Cecil;
using Mono.Cecil.Cil;
using TraceMap.Core;
using TraceMap.Combine;
using TraceMap.Reporting;
using TraceMap.Storage;

namespace TraceMap.Tests;

// Exact-root fixtures depend on bounded Git probes. Match the existing identity-
// sensitive collection instead of competing with concurrently scanning fixtures.
[Collection("Git metadata sensitive")]
public sealed class IlCommandBindingExtractorTests
{
    private const string PrivateLiteral = "private-fixture-procedure-never-retained";

    [Theory]
    [InlineData(true, 24, true, true)]
    [InlineData(true, 24, false, true)]
    [InlineData(false, 24, true, false)]
    [InlineData(true, 8, true, false)]
    public async Task Deep_projectless_handler_crosses_provider_dll_and_binds_command_through_twelve_layers(
        bool includeProvider, int depth, bool compiledOnly, bool expectedPath)
    {
        var report = await DeepReportAsync("Lookup_Click", compiledOnly, includeProvider: includeProvider, depth: depth);
        if (!expectedPath)
        {
            Assert.Empty(report.Paths);
            Assert.NotEmpty(report.Gaps);
            if (depth == 8) Assert.Contains(report.Gaps, gap => gap.GapKind == "TruncatedByLimit" && gap.Reason == "depth");
            return;
        }
        var path = Assert.Single(report.Paths);
        var ordered = string.Join("\n", path.Nodes.Select(node => node.SymbolId));
        var previous = -1;
        for (var layer = 1; layer <= 12; layer++)
        {
            var position = ordered.IndexOf($"Layer{layer:00}", StringComparison.Ordinal);
            Assert.True(position > previous, $"Missing or out-of-order Layer{layer:00}");
            previous = position;
        }
        Assert.Contains("PublicProof.DeepWebsite", ordered);
        Assert.Contains("PublicProof.Framework", ordered);
        var endpoint = Assert.Single(path.Nodes, node => node.SurfaceName == "DbDataAdapter.Fill");
        var binding = Assert.IsType<CompiledCommandConfigurationCandidate>(endpoint.CommandBinding);
        Assert.Equal("constant-on-encoded-call-path", binding.CommandTextFromPath!.State);
        Assert.Equal("method-local-constant", binding.CommandTypeFromPath!.State);
        Assert.True(binding.CommandTextFromPath.Steps.Count >= 14);
        Assert.Equal(DeepTextIdentity("public.deep_lookup"),
            binding.CommandTextFromPath.Origin.Identity);
        Assert.Equal("4", binding.CommandTypeFromPath.Origin.Identity);
        Assert.Equal(new[] { "Lookup_Click" }.Concat(Enumerable.Range(1, 12).Select(n => $"Layer{n:00}"))
            .Concat(["Run", "Run", "DbDataAdapter.Fill"]), path.Nodes.Select(DeepMethodName));
        Assert.DoesNotContain(path.Nodes, node => node.SymbolId?.Contains("DeepDecoy", StringComparison.Ordinal) == true);
        Assert.InRange(report.Summary.TraversalWorkUnits!.Value, 1, 100_000);
    }

    [Theory]
    [InlineData("Branch_Click", true, 2)]
    [InlineData("Branch_Click", false, 2)]
    [InlineData("Cycle_Click", true, 1)]
    [InlineData("Cycle_Click", false, 1)]
    [InlineData("Unknown_Click", true, 1)]
    [InlineData("Comparison_Click", true, 1)]
    public async Task Deep_projectless_branch_cycle_and_unknown_values_are_evidence_bounded(
        string handler, bool compiledOnly, int expectedPaths)
    {
        var report = await DeepReportAsync(handler, compiledOnly);
        Assert.Equal(expectedPaths, report.Paths.Count);
        Assert.All(report.Paths, path => Assert.DoesNotContain(path.Nodes,
            node => node.SymbolId?.Contains("DeepDecoy", StringComparison.Ordinal) == true));
        Assert.All(report.Paths, path => Assert.Equal(path.Nodes.Count - 1, path.Edges.Count));
        if (handler == "Cycle_Click")
        {
            Assert.Contains(report.Gaps, gap => gap.GapKind == "TruncatedByLimit" && gap.Reason == "cycle");
            Assert.DoesNotContain(report.Paths.SelectMany(path => path.Nodes), node => DeepMethodName(node) == "CycleB");
        }
        var hashes = report.Paths.Select(path => Assert.IsType<CompiledCommandConfigurationCandidate>(
            Assert.Single(path.Nodes, node => node.SurfaceName == "DbDataAdapter.Fill").CommandBinding).CommandTextFromPath!).ToArray();
        if (handler is "Unknown_Click" or "Comparison_Click")
        {
            Assert.All(hashes, value => Assert.NotEqual("constant-on-encoded-call-path", value.State));
            Assert.All(hashes, value => Assert.NotEmpty(value.Gaps));
        }
        else
        {
            var expected = handler == "Branch_Click" ? new[] { "public.branch_left", "public.branch_right" } : ["public.cycle_exit"];
            Assert.Equal(expected.Select(DeepTextIdentity).Order(),
                hashes.Select(value => value.Origin.Identity).Order());
            Assert.All(hashes, value => Assert.Equal("constant-on-encoded-call-path", value.State));
        }
    }

    [Fact]
    public async Task Deep_projectless_repeated_queries_are_deterministic_and_work_exhaustion_is_explicit()
    {
        var report = await DeepReportAsync("Branch_Click", true);
        var repeated = await DeepReportAsync("Branch_Click", true);
        // Compare method sequences and values, not cross-scan IDs or timing observations.
        Assert.Equal(report.Paths.Select(path => string.Join("/", path.Nodes.Select(DeepMethodName))),
            repeated.Paths.Select(path => string.Join("/", path.Nodes.Select(DeepMethodName))));
        Assert.Equal(report.Paths.Select(path => path.Nodes.Last().CommandBinding!.CommandTextFromPath!.Origin),
            repeated.Paths.Select(path => path.Nodes.Last().CommandBinding!.CommandTextFromPath!.Origin));
        var limited = await DeepReportAsync("Lookup_Click", true, work: 1);
        Assert.Empty(limited.Paths);
        Assert.True(limited.Summary.Truncated);
        Assert.Contains(limited.Gaps, gap => gap.GapKind == "TruncatedByLimit" && gap.Reason == "work");
        Assert.InRange(limited.Summary.TraversalWorkUnits!.Value, 0, 1);
    }

    private static string DeepMethodName(CombinedPathNode node)
    {
        if (node.SurfaceName is not null) return node.SurfaceName;
        var match = Regex.Match(node.SymbolId ?? "", @"\|method:\d+:([^|]+)\|");
        Assert.True(match.Success, $"Expected exact compiled method identity: {node.NodeKind}");
        return match.Groups[1].Value;
    }

    private static string DeepTextIdentity(string text) =>
        $"str:{text.Length}:{Convert.ToHexStringLower(SHA256.HashData(Encoding.Unicode.GetBytes(text)))}";

    [WindowsDeepCorpusTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Deep_projectless_Windows_ASPNET_publish_preserves_exact_chain_and_command_binding(bool updatable)
    {
        var repo = FindRepoRoot();
        var retainedRoot = Environment.GetEnvironmentVariable("TRACEMAP_DEEP_CORPUS_ROOT");
        var published = retainedRoot is null
            ? Path.Combine(Path.GetTempPath(), "tracemap-deep-windows-" + Guid.NewGuid().ToString("N"))
            : Path.Combine(retainedRoot, updatable ? "publish-mapless" : "publish-mapped");
        var start = new ProcessStartInfo("pwsh")
        { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
        var script = Path.Combine(repo, "scripts", "validation", "Test-PublicWebFormsPublish.ps1");
        foreach (var argument in new[] { "-NoProfile", "-File", script, "-TraceMapRoot", repo, "-OutputRoot", published, "-DeepChain" })
            start.ArgumentList.Add(argument);
        // Run both mapped and mapless authentic ASP.NET compiler outputs.
        if (updatable) start.ArgumentList.Add("-Updatable");
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) { process.Kill(entireProcessTree: true); throw; }
        Assert.True(process.ExitCode == 0, $"Publish failed; preserved output={published}\n{await stdout}\n{await stderr}");
        using var receipt = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(published, "publish-receipt.local.json")));
        var proof = receipt.RootElement;
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(script))),
            proof.GetProperty("receiptGeneratorSha256").GetString());
        Assert.Matches("^[0-9a-f]{64}$", proof.GetProperty("frameworkBoundedInputSha256").GetString()!);
        Assert.Matches("^[0-9a-f]{64}$", proof.GetProperty("boundedInputSha256").GetString()!);
        Assert.Equal("Lookup.aspx", proof.GetProperty("pages")[0].GetProperty("sourcePath").GetString());
        foreach (var artifact in proof.GetProperty("publishedFiles").EnumerateArray())
        {
            var file = Path.Combine(published, artifact.GetProperty("path").GetString()!);
            Assert.Equal(artifact.GetProperty("sha256").GetString(), Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(file))));
        }
        Assert.Empty(Directory.GetFiles(published, "*.pdb", SearchOption.AllDirectories));
        var report = await DeepReportAsync("Lookup_Click", true, publishedRoot: published);
        var path = Assert.Single(report.Paths);
        Assert.Equal(new[] { "Lookup_Click" }.Concat(Enumerable.Range(1, 12).Select(n => $"Layer{n:00}"))
            .Concat(["Run", "Run", "DbDataAdapter.Fill"]), path.Nodes.Select(DeepMethodName));
        var binding = Assert.IsType<CompiledCommandConfigurationCandidate>(path.Nodes.Last().CommandBinding);
        Assert.Equal(DeepTextIdentity("public.deep_lookup"), binding.CommandTextFromPath!.Origin.Identity);
        Assert.Equal("constant-on-encoded-call-path", binding.CommandTextFromPath.State);
        // Keep the authentic small corpus for transfer back to macOS. No private inputs.
        Assert.True(Directory.Exists(published));
        Console.WriteLine($"deepCorpus.windowsPublish={published}");
    }

    private static async Task<CombinedDependencyPathReport> DeepReportAsync(string handler, bool compiledOnly,
        int work = 100_000, string? publishedRoot = null, bool includeProvider = true, int depth = 24)
    {
        var repo = FindRepoRoot();
        var sourcePath = Path.Combine(repo, "samples", "messy-dotnet-workspace", "vb-deep-projectless");
        Assert.Empty(Directory.GetFiles(sourcePath, "*.*proj", SearchOption.AllDirectories));
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var bin = publishedRoot is null ? Path.Combine(repo, "samples", "fixture-build", "deep-projectless", "bin", configuration, "net48")
            : Path.Combine(publishedRoot, "bin");
        var dlls = Directory.GetFiles(bin, "*.dll").Where(path => includeProvider || Path.GetFileName(path) != "PublicProof.Framework.dll").ToArray();
        var root = Directory.CreateTempSubdirectory("tracemap-deep-regression-").FullName;
        var clock = Stopwatch.StartNew();
        try
        {
            var scan = ScanEngine.Scan(new ScanOptions(sourcePath, Path.Combine(root, "scan"),
                CompiledInputPaths: dlls, IlBodyEvidence: true,
                WebFormsPublishReceiptPath: publishedRoot is null ? null : Path.Combine(publishedRoot, "publish-receipt.local.json")));
            Assert.InRange(scan.Facts.Count, 1, 10_000);
            var entry = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.ManagedMethodDeclared
                && fact.TargetSymbol!.Contains("DeepLookup|", StringComparison.Ordinal)
                && fact.TargetSymbol.Contains($"|method:{handler.Length}:{handler}|", StringComparison.Ordinal));
            var index = Path.Combine(root, "index.sqlite");
            var combined = Path.Combine(root, "combined.sqlite");
            SqliteIndexWriter.Write(index, scan.Manifest, scan.Facts);
            var combine = await CombinedIndexBuilder.CombineAsync(new CombineOptions([index], combined, ["public-deep-regression"]));
            var source = Assert.Single(combine.Sources);
            Assert.True(Regex.IsMatch(source.CommitSha, "^[0-9a-f]{40}$", RegexOptions.CultureInvariant),
                $"Deep corpus Git identity unavailable before exact-root admission: handler={handler}; compiledOnly={compiledOnly}; " +
                $"scanCommit={scan.Manifest.CommitSha}; combinedCommit={source.CommitSha}; scanId={source.ScanId}");
            Assert.Equal(scan.Manifest.CommitSha, source.CommitSha);
            CombinedPathGraphObservation? observed = null;
            var report = await CombinedDependencyPathReporter.BuildSelectedSymbolsAsync(
                new CombinedDependencyPathOptions(combined, root, ToSurface: "database-api", SurfaceName: "DbDataAdapter.Fill", MaxDepth: depth)
                { CompiledOnly = compiledOnly, ExactFromSymbol = true, MaxTraversalWork = work },
                [new(source.SourceIndexId, source.ScanId, source.CommitSha, entry.TargetSymbol!)], combinedIndex: true,
                limits: new(MaxFacts: 10_000, MaxEdges: 10_000, MaxTextBytes: 32 * 1024 * 1024)
                { MaxGraphStorageBytes = 32 * 1024 * 1024, GraphObservationObserver = value => observed = value });
            Assert.NotNull(observed);
            Console.WriteLine($"deepGraph.reads={JsonSerializer.Serialize(observed)}");
            Assert.InRange(observed.FactPayloadRowsByStage!["vb-receiver-bridges"], 0, 200);
            Assert.InRange(observed.FactPayloadRowsByStage["vb-implicit-receiver-bridges"], 0, 200);
            Assert.InRange(observed.FactPayloadBytesRead, 0, 32 * 1024 * 1024);
            Assert.InRange(observed.FactPayloadRowsRead, 0, 100_000);
            Assert.InRange(report.Summary.TraversalWorkUnits!.Value, 0, work);
            Assert.InRange(Directory.GetFiles(root, "*", SearchOption.AllDirectories).Sum(file => new FileInfo(file).Length), 1, 32 * 1024 * 1024);
            Assert.True(clock.Elapsed < TimeSpan.FromSeconds(60), "Tiny corpus exceeded the generous 60-second end-to-end guard.");
            return report;
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public async Task Real_legacy_VB_ref_arrays_debug_fields_and_mapping_loops_retain_command_candidate()
    {
        var path = Path.Combine(FindRepoRoot(), "samples", "messy-dotnet-workspace", "vb-publish-crossdll-framework", "bin",
            new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name, "net48", "PublicProof.Framework.dll");
        var bytes = File.ReadAllBytes(path); var limits = new IlBodyLimits();
        var left = IlBodyEvidenceExtractor.ReadCecilBodies(bytes, limits, new(200_000), CancellationToken.None, true);
        var right = IlBodyEvidenceExtractor.ReadSystemReflectionMetadataBodies(bytes, limits, new(200_000), CancellationToken.None, true);
        Assert.Empty(IlBodyEvidenceExtractor.CompareBodies(left, right));
        var bodies = IlBodyEvidenceExtractor.AgreeValueFlows(left.Bodies, right.Bodies)
            .Where(body => body.MethodIdentity.Contains("PublicLegacyCommandFlow|", StringComparison.Ordinal)).ToArray();
        var commandBody = Assert.Single(bodies, body => IlCommandBindingExtractor.Extract(body).Bindings.Count > 0);
        var binding = Assert.Single(IlCommandBindingExtractor.Extract(commandBody).Bindings);
        Assert.Equal(new IlValueOrigin("argument-slot", "1"), binding.CommandText);
        Assert.Equal(new IlValueOrigin("constant-int32", "4"), binding.CommandType);
        Assert.DoesNotContain("IlValueInstructionUnavailable", commandBody.ValueFlow!.Gaps);
        Assert.DoesNotContain("IlValueStackUnavailable", commandBody.ValueFlow.Gaps);
        Assert.DoesNotContain("IlValueStackMergeUnavailable", commandBody.ValueFlow.Gaps);
        var wrapper = Assert.Single(bodies, body => body.Calls.Any(call => call.StackShape?.ByReferenceParameters == "011"));
        var forward = Assert.Single(wrapper.ValueFlow!.Calls, value => wrapper.Calls.Single(call => call.Offset == value.Offset).StackShape?.ByReferenceParameters == "011");
        Assert.Equal("control-flow-candidate", forward.State);
        Assert.Equal(new IlValueOrigin("argument-slot", "1"), forward.Arguments[0]);
        Assert.All(forward.Arguments.Skip(1), value => Assert.Equal("local-address", value.Kind));
        var root = Directory.CreateTempSubdirectory("tracemap-legacy-command-").FullName;
        var sourcePath = Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(path))))!;
        var scan = ScanEngine.Scan(new ScanOptions(sourcePath, Path.Combine(root, "out"), CompiledInputPaths: [path], IlBodyEvidence: true));
        var entry = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.ManagedMethodDeclared
            && fact.TargetSymbol!.Contains("PublicLegacyCommandEntry", StringComparison.Ordinal)
            && fact.TargetSymbol.Contains("|method:3:Run|", StringComparison.Ordinal));
        var index = Path.Combine(root, "index.sqlite"); var combined = Path.Combine(root, "combined.sqlite");
        SqliteIndexWriter.Write(index, scan.Manifest, scan.Facts);
        var combine = await CombinedIndexBuilder.CombineAsync(new CombineOptions([index], combined, ["public-legacy-fixture"]));
        var source = Assert.Single(combine.Sources);
        var selector = new CombinedPathSymbolRoot(source.SourceIndexId, source.ScanId, source.CommitSha, entry.TargetSymbol!);
        var report = await CombinedDependencyPathReporter.BuildSelectedSymbolsAsync(
            new CombinedDependencyPathOptions(combined, root, ToSurface: "database-api", SurfaceName: "DbDataAdapter.Fill", MaxDepth: 10)
            { CompiledOnly = true, ExactFromSymbol = true, MaxTraversalWork = 10_000 }, [selector], combinedIndex: true);
        var endpoint = Assert.Single(Assert.Single(report.Paths).Nodes, node => node.SurfaceName == "DbDataAdapter.Fill");
        var projected = Assert.IsType<CompiledCommandConfigurationCandidate>(endpoint.CommandBinding);
        Assert.Equal("constant-on-encoded-call-path", projected.CommandTextFromPath!.State);
        Assert.Equal("method-local-constant", projected.CommandTypeFromPath!.State);
        Assert.Equal(2, projected.CommandTextFromPath.Steps.Count);
        Assert.All(scan.Facts.Where(fact => fact.FactType == FactTypes.ManagedIlCallValuesObserved), fact =>
            Assert.DoesNotContain("local-address", fact.Properties["argumentOrigins"], StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("timeout", true)]
    [InlineData("wrong-timeout", false)]
    [InlineData("transaction", true)]
    [InlineData("wrong-transaction", false)]
    [InlineData("mapping", true)]
    [InlineData("wrong-mapping", false)]
    [InlineData("mapping-escape", false)]
    [InlineData("field-debug", true)]
    [InlineData("unrelated-unknown", true)]
    [InlineData("field-escape", false)]
    [InlineData("array-escape", false)]
    [InlineData("byref-escape", false)]
    [InlineData("indirect-escape", false)]
    [InlineData("boxed-escape", false)]
    [InlineData("address-before-assignment", false)]
    [InlineData("field-indirect-debug", true)]
    [InlineData("field-indirect-escape", false)]
    public void Bookkeeping_preserves_text_but_wrong_contracts_and_object_exposure_do_not(string effect, bool expected)
    {
        var (body, _) = Fixture(extraEffect: effect);
        var bindings = IlCommandBindingExtractor.Extract(body).Bindings;
        if (expected)
        {
            var binding = Assert.Single(bindings);
            Assert.Equal("constant-string-hash", binding.CommandText.Kind);
            Assert.Equal(new IlValueOrigin("constant-int32", "4"), binding.CommandType);
        }
        else Assert.Empty(bindings);
    }

    [Fact]
    public async Task Real_VB_net48_wrapper_retains_both_normal_command_endpoints_with_provenance()
    {
        var sourcePath = Path.Combine(FindRepoRoot(), "samples", "messy-dotnet-workspace", "vb-publish-crossdll-framework");
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var assemblyPath = Path.Combine(sourcePath, "bin", configuration, "net48", "PublicProof.Framework.dll");
        Assert.True(File.Exists(assemblyPath), "The project-reference fixture must be built before this test.");
        var bytes = File.ReadAllBytes(assemblyPath); var limits = new IlBodyLimits();
        var cecil = IlBodyEvidenceExtractor.ReadCecilBodies(bytes, limits, new IlBodyEvidenceExtractor.IlWorkBudget(100_000), CancellationToken.None);
        var srm = IlBodyEvidenceExtractor.ReadSystemReflectionMetadataBodies(bytes, limits, new IlBodyEvidenceExtractor.IlWorkBudget(100_000), CancellationToken.None);
        Assert.Empty(IlBodyEvidenceExtractor.CompareBodies(cecil, srm));
        var body = Assert.Single(IlBodyEvidenceExtractor.AgreeValueFlows(cecil.Bodies, srm.Bodies),
            candidate => candidate.MethodIdentity.Contains("PublicSqlDataAccess|", StringComparison.Ordinal)
                && IlCommandBindingExtractor.Extract(candidate).Bindings.Count > 0);
        var bindings = IlCommandBindingExtractor.Extract(body).Bindings;
        Assert.Equal(2, bindings.Count);
        Assert.All(bindings, binding => {
            Assert.Equal(new IlValueOrigin("argument-slot", "1"), binding.CommandText);
            Assert.Equal(new IlValueOrigin("constant-int32", "4"), binding.CommandType);
        });
        Assert.Contains("IlValueExceptionFlowUnavailable", body.ValueFlow!.Gaps);
        var root = Directory.CreateTempSubdirectory("tracemap-real-vb-command-").FullName;
        var scan = ScanEngine.Scan(new ScanOptions(sourcePath, Path.Combine(root, "out"), CompiledInputPaths: [assemblyPath], IlBodyEvidence: true));
        var candidates = scan.Facts.Where(fact => fact.FactType == FactTypes.ManagedIlDatabaseCommandCandidate
            && scan.Facts.Any(bodyFact => bodyFact.FactId == fact.Properties["ilBodyFactId"]
                && scan.Facts.Any(method => method.FactId == bodyFact.Properties["compiledFactId"]
                    && method.TargetSymbol!.Contains("PublicSqlDataAccess", StringComparison.Ordinal)))).ToArray();
        Assert.Equal(2, candidates.Length);
        var bodyFact = scan.Facts.Single(fact => fact.FactId == candidates[0].Properties["ilBodyFactId"]);
        var method = scan.Facts.Single(fact => fact.FactId == bodyFact.Properties["compiledFactId"]);
        var index = Path.Combine(root, "index.sqlite"); var combined = Path.Combine(root, "combined.sqlite");
        SqliteIndexWriter.Write(index, scan.Manifest, scan.Facts);
        var combine = await CombinedIndexBuilder.CombineAsync(new CombineOptions([index], combined, ["public-fixture"]));
        var source = Assert.Single(combine.Sources);
        var selector = new CombinedPathSymbolRoot(source.SourceIndexId, source.ScanId, source.CommitSha, method.TargetSymbol!);
        var report = await CombinedDependencyPathReporter.BuildSelectedSymbolsAsync(
            new CombinedDependencyPathOptions(combined, root, ToSurface: "database-api", SurfaceName: "DbDataAdapter.Fill", MaxDepth: 10)
            { CompiledOnly = true, ExactFromSymbol = true, MaxTraversalWork = 10_000 }, [selector], combinedIndex: true);
        var endpoint = Assert.Single(Assert.Single(report.Paths).Nodes, node => node.SurfaceName == "DbDataAdapter.Fill");
        var binding = Assert.IsType<CompiledCommandConfigurationCandidate>(endpoint.CommandBinding);
        Assert.Equal("4", binding.CommandTypeOrigin.Identity);
        Assert.Equal("unresolved-root-argument", binding.CommandTextFromPath!.State);
        Assert.Equal(bodyFact.Properties["ilGeneratorSha256"], binding.GeneratorSha256);
        Assert.Equal(bodyFact.Properties["ilBoundedInputSha256"], binding.BoundedInputSha256);
    }

    [Theory]
    [InlineData(false, true, "none", true)]
    [InlineData(true, true, "none", true)]
    [InlineData(true, false, "none", true)]
    [InlineData(true, true, "wrong-signature", false)]
    public void Using_regions_and_typed_AddRange_preserve_only_encoded_normal_configuration(
        bool usingRegions, bool addRange, string effect, bool expected)
    {
        var (body, _) = Fixture(usingRegions: usingRegions, addRange: addRange,
            parameterLoop: !addRange, loopEffect: effect);
        var result = IlCommandBindingExtractor.Extract(body);
        if (expected)
        {
            var binding = Assert.Single(result.Bindings);
            Assert.Equal("constant-string-hash", binding.CommandText.Kind);
            Assert.Equal("4", binding.CommandType.Identity);
        }
        else Assert.Empty(result.Bindings);
        if (usingRegions) Assert.Contains("IlValueExceptionFlowUnavailable", body.ValueFlow!.Gaps);
    }

    [Theory]
    [InlineData("none", true, true)]
    [InlineData("none", true, false)]
    [InlineData("collection-escape", false, true)]
    [InlineData("collection-escape", false, false)]
    [InlineData("command-escape", false, true)]
    [InlineData("conditional-text", false, true)]
    [InlineData("wrong-signature", false, true)]
    [InlineData("wrong-signature", false, false)]
    public void Parameter_loop_preserves_only_unexposed_agreed_configuration(string effect, bool expected, bool loop)
    {
        var (body, _) = Fixture(parameterLoop: loop, parameterOnly: !loop, loopEffect: effect);
        if (loop) Assert.NotNull(body.ValueFlow!.ControlFlow);
        else Assert.Null(body.ValueFlow!.ControlFlow);
        var result = IlCommandBindingExtractor.Extract(body);
        if (expected)
        {
            var binding = Assert.Single(result.Bindings);
            Assert.Equal("constant-string-hash", binding.CommandText.Kind);
            Assert.Equal(new IlValueOrigin("constant-int32", "4"), binding.CommandType);
            Assert.Contains("IlCommandParameterFlowUnavailable", result.Gaps);
        }
        else Assert.Empty(result.Bindings);
    }

    [Fact]
    public void Independent_readers_bind_command_configuration_to_adapter_fill()
    {
        var (body, _) = Fixture();
        var binding = Assert.Single(IlCommandBindingExtractor.Extract(body).Bindings);
        Assert.Equal("constant-string-hash", binding.CommandText.Kind);
        Assert.StartsWith($"str:{PrivateLiteral.Length}:", binding.CommandText.Identity);
        Assert.Equal(new IlValueOrigin("constant-int32", ((int)System.Data.CommandType.StoredProcedure).ToString()), binding.CommandType);
        Assert.Equal("allocation-site", binding.CommandReceiver.Kind);
        Assert.Equal("allocation-site", binding.EndpointReceiver.Kind);
        Assert.NotEqual(binding.CommandReceiver, binding.EndpointReceiver);
        Assert.Equal(3, binding.ConfigurationOffsets.Count);
        Assert.NotNull(binding.AdapterBindingOffset);
        Assert.DoesNotContain(PrivateLiteral, JsonSerializer.Serialize(binding), StringComparison.Ordinal);
    }

    [Fact]
    public void Wrapper_argument_is_retained_as_a_slot_not_a_procedure_value()
    {
        var (body, _) = Fixture(argumentText: true);
        var binding = Assert.Single(IlCommandBindingExtractor.Extract(body).Bindings);
        Assert.Equal(new IlValueOrigin("argument-slot", "0"), binding.CommandText);
    }

    [Theory]
    [InlineData(true, false, "IlCommandAdapterBindingUnavailable")]
    [InlineData(false, true, "IlCommandTextBindingUnavailable")]
    public void Branch_target_or_unknown_command_mutation_withholds_binding(bool branch, bool mutation, string gap)
    {
        var (body, _) = Fixture(branch: branch, mutation: mutation);
        var result = IlCommandBindingExtractor.Extract(body);
        Assert.Empty(result.Bindings);
        Assert.Contains(gap, result.Gaps);
    }

    [Fact]
    public void Framework_name_without_expected_token_does_not_create_a_binding()
    {
        var (body, _) = Fixture(lookalike: true);
        Assert.Empty(IlCommandBindingExtractor.Extract(body).Bindings);
    }

    [Fact]
    public void CommandText_member_name_with_wrong_signature_does_not_authorize_configuration()
    {
        var (body, _) = Fixture(wrongTextSetter: true);
        var result = IlCommandBindingExtractor.Extract(body);
        Assert.Empty(result.Bindings);
        Assert.Contains("IlCommandTextBindingUnavailable", result.Gaps);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Aggregate_work_limit_fails_closed_without_partial_body_bindings(bool parameterLoop)
    {
        var (body, _) = Fixture(parameterLoop: parameterLoop);
        var result = IlCommandBindingExtractor.Extract(body, new IlBodyEvidenceExtractor.IlWorkBudget(1));
        Assert.Empty(result.Bindings);
        Assert.Contains("IlCommandBindingWorkLimit", result.Gaps);
    }

    [Fact]
    public void Independent_control_flow_disagreement_withholds_all_command_candidates()
    {
        var (body, _) = Fixture(parameterLoop: true);
        var nodes = body.ValueFlow!.ControlFlow!.ToArray();
        nodes[0] = nodes[0] with { InvalidatesConfiguration = !nodes[0].InvalidatesConfiguration };
        var disagreed = Assert.Single(IlBodyEvidenceExtractor.AgreeValueFlows([body],
            [body with { ValueFlow = body.ValueFlow with { ControlFlow = nodes } }]));
        Assert.Contains("IlValueReaderDisagreementOrLimit", disagreed.ValueFlow!.Gaps);
        Assert.Empty(IlCommandBindingExtractor.Extract(disagreed).Bindings);
    }

    [Fact]
    public void Exhausted_operand_budget_preserves_independently_decoded_body_and_call_evidence()
    {
        var (_, bytes) = Fixture(parameterLoop: true);
        var limits = new IlBodyLimits();
        var cecil = IlBodyEvidenceExtractor.ReadCecilBodies(bytes, limits, new IlBodyEvidenceExtractor.IlWorkBudget(100_000),
            CancellationToken.None, valueBudget: new IlBodyEvidenceExtractor.IlWorkBudget(1));
        var srm = IlBodyEvidenceExtractor.ReadSystemReflectionMetadataBodies(bytes, limits, new IlBodyEvidenceExtractor.IlWorkBudget(100_000),
            CancellationToken.None, valueBudget: new IlBodyEvidenceExtractor.IlWorkBudget(1));
        Assert.Empty(IlBodyEvidenceExtractor.CompareBodies(cecil, srm));
        var body = Assert.Single(IlBodyEvidenceExtractor.AgreeValueFlows(cecil.Bodies, srm.Bodies));
        Assert.NotEmpty(body.Calls);
        Assert.Contains("IlValueWorkLimitExceeded", body.ValueFlow!.Gaps);
        Assert.Empty(IlCommandBindingExtractor.Extract(body).Bindings);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Scan_materializes_joined_command_candidates_with_exact_provenance_and_no_literal(bool parameterLoop)
    {
        var (_, bytes) = Fixture(parameterLoop: parameterLoop);
        var root = Directory.CreateTempSubdirectory("tracemap-il-command-test-").FullName;
        var assemblyPath = Path.Combine(root, "CommandFixture.dll");
        File.WriteAllBytes(assemblyPath, bytes);
        var source = Path.Combine(FindRepoRoot(), "samples", "vb-modern-sample");
        var scan = ScanEngine.Scan(new ScanOptions(source, Path.Combine(root, "out"),
            CompiledInputPaths: [assemblyPath], IlBodyEvidence: true));
        var candidate = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.ManagedIlDatabaseCommandCandidate);
        Assert.Equal(RuleIds.DotNetIlCommandBinding, candidate.RuleId);
        Assert.Equal(EvidenceTiers.Tier3SyntaxOrTextual, candidate.EvidenceTier);
        Assert.Equal(scan.Manifest.IlBodyProvenance!.GeneratorSha256, candidate.Properties["ilGeneratorSha256"]);
        Assert.Equal(scan.Manifest.IlBodyProvenance.BoundedInputSha256, candidate.Properties["ilBoundedInputSha256"]);
        var byId = scan.Facts.ToDictionary(fact => fact.FactId);
        Assert.Equal(FactTypes.ManagedIlBodyDeclared, byId[candidate.Properties["ilBodyFactId"]].FactType);
        Assert.Equal(FactTypes.ManagedIlCallObserved, byId[candidate.Properties["ilCallFactId"]].FactType);
        foreach (var id in JsonSerializer.Deserialize<string[]>(candidate.Properties["configurationCallFactIds"])!)
        {
            Assert.Equal(FactTypes.ManagedIlCallObserved, byId[id].FactType);
            Assert.Equal(candidate.Properties["ilBodyFactId"], byId[id].Properties["ilBodyFactId"]);
        }
        Assert.DoesNotContain(PrivateLiteral, JsonSerializer.Serialize(scan), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    [InlineData(false, false, true)]
    [InlineData(true, false, true)]
    public async Task Compiled_handler_path_retains_command_binding_only_with_exact_fact_provenance(bool tamper, bool parameterLoop, bool usingRegions)
    {
        var (_, bytes) = Fixture(parameterLoop: parameterLoop, usingRegions: usingRegions, addRange: usingRegions);
        var root = Directory.CreateTempSubdirectory("tracemap-il-command-path-").FullName;
        var assemblyPath = Path.Combine(root, "CommandFixture.dll");
        File.WriteAllBytes(assemblyPath, bytes);
        var sourcePath = Path.Combine(FindRepoRoot(), "samples", "vb-modern-sample");
        var scan = ScanEngine.Scan(new ScanOptions(sourcePath, Path.Combine(root, "out"),
            CompiledInputPaths: [assemblyPath], IlBodyEvidence: true));
        var candidate = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.ManagedIlDatabaseCommandCandidate);
        var body = scan.Facts.Single(fact => fact.FactId == candidate.Properties["ilBodyFactId"]);
        var method = scan.Facts.Single(fact => fact.FactId == body.Properties["compiledFactId"]);
        var facts = scan.Facts.ToArray();
        if (tamper)
        {
            var properties = new Dictionary<string, string>(candidate.Properties) { ["ilBoundedInputSha256"] = new string('f', 64) };
            facts[Array.IndexOf(facts, candidate)] = candidate with { Properties = properties };
        }
        var index = Path.Combine(root, "index.sqlite"); var combined = Path.Combine(root, "combined.sqlite");
        SqliteIndexWriter.Write(index, scan.Manifest, facts);
        var result = await CombinedIndexBuilder.CombineAsync(new CombineOptions([index], combined, ["fixture"]));
        var source = Assert.Single(result.Sources);
        var selector = new CombinedPathSymbolRoot(source.SourceIndexId, source.ScanId, source.CommitSha, method.TargetSymbol!);
        var options = new CombinedDependencyPathOptions(combined, root, ToSurface: "database-api", SurfaceName: "DbDataAdapter.Fill", MaxDepth: 10)
        { CompiledOnly = true, ExactFromSymbol = true, MaxTraversalWork = 10_000 };
        var report = await CombinedDependencyPathReporter.BuildSelectedSymbolsAsync(options, [selector], combinedIndex: true);
        var path = Assert.Single(report.Paths);
        var endpoint = Assert.Single(path.Nodes, node => node.SurfaceName == "DbDataAdapter.Fill");
        if (tamper)
        {
            Assert.Null(endpoint.CommandBinding);
            Assert.Contains(report.Gaps, gap => gap.GapKind == "CompiledIlCommandBindingUnavailable");
        }
        else
        {
            var binding = Assert.IsType<CompiledCommandConfigurationCandidate>(endpoint.CommandBinding);
            Assert.Equal("constant-string-hash", binding.CommandTextOrigin.Kind);
            Assert.Equal("4", binding.CommandTypeOrigin.Identity);
            Assert.Equal(candidate.Properties["ilGeneratorSha256"], binding.GeneratorSha256);
            Assert.Equal(candidate.Properties["ilBoundedInputSha256"], binding.BoundedInputSha256);
            Assert.Contains(path.Edges.SelectMany(edge => edge.SupportingFactIds), id => id == binding.CombinedFactId);
        }
        Assert.DoesNotContain(PrivateLiteral, JsonSerializer.Serialize(report), StringComparison.Ordinal);
        Assert.All(path.Edges, edge => Assert.True(CombinedDependencyPathReporter.CompiledBaselineAllowsEdge(edge.EdgeKind, true)));
    }

    [Theory]
    [InlineData(1, false, "none")]
    [InlineData(2, false, "none")]
    [InlineData(1, true, "none")]
    [InlineData(2, false, "missing")]
    [InlineData(2, false, "changed")]
    [InlineData(2, false, "ambiguous")]
    [InlineData(2, false, "source-bridge")]
    [InlineData(2, false, "protected")]
    public async Task Compiled_caller_argument_substitution_is_path_specific_and_provenance_bound(int hops, bool instance, string tamper)
    {
        var (_, bytes) = Fixture(argumentText: true, wrapperDepth: hops, instanceRun: instance,
            usingRegions: tamper == "protected", parameterLoop: tamper == "protected");
        var root = Directory.CreateTempSubdirectory("tracemap-il-command-caller-").FullName;
        var assemblyPath = Path.Combine(root, "CommandFixture.dll"); File.WriteAllBytes(assemblyPath, bytes);
        var scan = ScanEngine.Scan(new ScanOptions(Path.Combine(FindRepoRoot(), "samples", "vb-modern-sample"),
            Path.Combine(root, "out"), CompiledInputPaths: [assemblyPath], IlBodyEvidence: true));
        var entry = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.ManagedMethodDeclared
            && fact.Properties.GetValueOrDefault("metadataName") == "Entry");
        var entryBody = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.ManagedIlBodyDeclared
            && fact.Properties.GetValueOrDefault("compiledFactId") == entry.FactId);
        var entryCall = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.ManagedIlCallObserved
            && fact.Properties.GetValueOrDefault("ilBodyFactId") == entryBody.FactId
            && fact.Properties.GetValueOrDefault("opcode") == "call");
        var operand = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.ManagedIlCallValuesObserved
            && fact.Properties.GetValueOrDefault("ilCallFactId") == entryCall.FactId);
        var facts = scan.Facts.ToList();
        if (tamper == "missing") facts.Remove(operand);
        if (tamper == "changed")
            facts[facts.IndexOf(operand)] = operand with
            { Properties = new Dictionary<string, string>(operand.Properties) { ["ilBoundedInputSha256"] = new string('f', 64) } };
        if (tamper == "ambiguous")
            facts.Add(FactFactory.Create(scan.Manifest, operand.FactType, operand.RuleId, operand.EvidenceTier,
                operand.Evidence, targetSymbol: operand.TargetSymbol, contractElement: operand.ContractElement,
                properties: new Dictionary<string, string>(operand.Properties) { ["syntheticCompetitor"] = "true" }));
        if (tamper == "source-bridge")
        {
            facts.Remove(entryCall);
            facts.Add(FactFactory.Create(scan.Manifest, FactTypes.CallEdge, RuleIds.CSharpSemanticCallGraph,
                EvidenceTiers.Tier1Semantic, new EvidenceSpan("synthetic.cs", 1, 1, null, "test", "test/1"),
                sourceSymbol: entry.TargetSymbol, targetSymbol: entryCall.Properties["targetIdentity"],
                properties: new Dictionary<string, string> { ["callKind"] = "method", ["targetSymbolId"] = entryCall.Properties["targetIdentity"] }));
        }
        var index = Path.Combine(root, "index.sqlite"); var combined = Path.Combine(root, "combined.sqlite");
        SqliteIndexWriter.Write(index, scan.Manifest, facts);
        var combine = await CombinedIndexBuilder.CombineAsync(new CombineOptions([index], combined, ["fixture"]));
        var source = Assert.Single(combine.Sources);
        var selector = new CombinedPathSymbolRoot(source.SourceIndexId, source.ScanId, source.CommitSha, entry.TargetSymbol!);
        var options = new CombinedDependencyPathOptions(combined, root, ToSurface: "database-api", SurfaceName: "DbDataAdapter.Fill", MaxDepth: 10)
        { CompiledOnly = tamper != "source-bridge", ExactFromSymbol = true, MaxTraversalWork = 10_000 };
        var report = await CombinedDependencyPathReporter.BuildSelectedSymbolsAsync(options, [selector], combinedIndex: true);
        var endpoint = Assert.Single(Assert.Single(report.Paths).Nodes, node => node.SurfaceName == "DbDataAdapter.Fill");
        var binding = Assert.IsType<CompiledCommandConfigurationCandidate>(endpoint.CommandBinding);
        Assert.Equal("argument-slot", binding.CommandTextOrigin.Kind);
        Assert.Equal(instance ? "1" : "0", binding.CommandTextOrigin.Identity);
        var mapped = Assert.IsType<CompiledCommandPathValueBinding>(binding.CommandTextFromPath);
        if (tamper is "none" or "protected")
        {
            Assert.Equal("constant-on-encoded-call-path", mapped.State);
            Assert.Equal("constant-string-hash", mapped.Origin.Kind);
            Assert.Equal(hops, mapped.Steps.Count);
            Assert.Empty(mapped.Gaps);
            Assert.Equal(Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(
                File.ReadAllBytes(typeof(CombinedDependencyPathReporter).Assembly.Location))), mapped.GeneratorSha256);
            Assert.Matches("^[0-9a-f]{64}$", mapped.BoundedInputSha256);
        }
        else if (tamper == "source-bridge")
        {
            Assert.Equal("unresolved-non-il-bridge", mapped.State);
            Assert.Contains("IlCommandNonIlCallerBridge", mapped.Gaps);
        }
        else
        {
            Assert.Equal("unresolved-call-evidence", mapped.State);
            Assert.Equal("argument-slot", mapped.Origin.Kind);
            Assert.Contains(mapped.Gaps, gap => gap is "IlCommandCallerOperandMissingOrAmbiguous" or "IlCommandCallerOperandProvenanceUnavailable");
        }
        Assert.DoesNotContain(PrivateLiteral, JsonSerializer.Serialize(report), StringComparison.Ordinal);
        var repeated = await CombinedDependencyPathReporter.BuildSelectedSymbolsAsync(options, [selector], combinedIndex: true);
        var repeatedBinding = Assert.Single(Assert.Single(repeated.Paths).Nodes,
            node => node.SurfaceName == "DbDataAdapter.Fill").CommandBinding;
        Assert.Equal(JsonSerializer.Serialize(binding), JsonSerializer.Serialize(repeatedBinding));
    }

    private static (IlBodyObservation Body, byte[] Bytes) Fixture(bool argumentText = false,
        bool branch = false, bool mutation = false, bool lookalike = false, bool wrongTextSetter = false,
        int wrapperDepth = 0, bool instanceRun = false, bool parameterLoop = false, string loopEffect = "none", bool parameterOnly = false,
        bool usingRegions = false, bool addRange = false, string extraEffect = "none")
    {
        using var assembly = AssemblyDefinition.CreateAssembly(new AssemblyNameDefinition("CommandFixture", new Version(1, 0)), "CommandFixture", ModuleKind.Dll);
        var module = assembly.MainModule;
        var data = new AssemblyNameReference("System.Data", new Version(4, 0, 0, 0))
        { PublicKeyToken = lookalike ? [] : Convert.FromHexString("b77a5c561934e089") };
        module.AssemblyReferences.Add(data);
        TypeReference Type(string ns, string name, bool valueType = false) => new(ns, name, module, data, valueType);
        var command = Type("System.Data.SqlClient", "SqlCommand");
        var adapter = Type("System.Data.SqlClient", "SqlDataAdapter");
        var commandBase = Type("System.Data.Common", "DbCommand");
        var adapterBase = Type("System.Data.Common", "DbDataAdapter");
        var commandType = Type("System.Data", "CommandType", true);
        var dataSet = Type("System.Data", "DataSet");
        MethodReference Method(TypeReference owner, string name, TypeReference result, params TypeReference[] parameters)
        {
            var reference = new MethodReference(name, result, owner) { HasThis = true };
            foreach (var parameter in parameters) reference.Parameters.Add(new ParameterDefinition(parameter));
            return reference;
        }
        var type = new TypeDefinition("Fixture", "Handler", TypeAttributes.Public, module.TypeSystem.Object);
        module.Types.Add(type);
        var method = new MethodDefinition("Run", MethodAttributes.Public | (instanceRun ? 0 : MethodAttributes.Static), module.TypeSystem.Void);
        if (argumentText) method.Parameters.Add(new ParameterDefinition(module.TypeSystem.String));
        if (addRange) method.Parameters.Add(new ParameterDefinition(new ArrayType(Type("System.Data.SqlClient", "SqlParameter"))));
        type.Methods.Add(method);
        method.Body.InitLocals = true;
        method.Body.Variables.Add(new VariableDefinition(command));
        method.Body.Variables.Add(new VariableDefinition(adapter));
        if (parameterLoop || parameterOnly || addRange) method.Body.Variables.Add(new VariableDefinition(module.TypeSystem.Int32));
        var il = method.Body.GetILProcessor();
        var endpointLoad = Instruction.Create(OpCodes.Ldloc_1);
        if (branch) il.Emit(OpCodes.Br, endpointLoad);
        il.Emit(OpCodes.Newobj, Method(command, ".ctor", module.TypeSystem.Void));
        il.Emit(OpCodes.Stloc_0);
        var outerStart = Instruction.Create(OpCodes.Ldloc_0); il.Append(outerStart);
        if (argumentText) il.Emit(instanceRun ? OpCodes.Ldarg_1 : OpCodes.Ldarg_0); else il.Emit(OpCodes.Ldstr, PrivateLiteral);
        il.Emit(OpCodes.Callvirt, Method(commandBase, "set_CommandText", module.TypeSystem.Void,
            wrongTextSetter ? module.TypeSystem.Object : module.TypeSystem.String));
        il.Emit(OpCodes.Ldloc_0);
        il.Emit(OpCodes.Ldc_I4, (int)System.Data.CommandType.StoredProcedure);
        il.Emit(OpCodes.Callvirt, Method(commandBase, "set_CommandType", module.TypeSystem.Void, commandType));
        il.Emit(OpCodes.Ldloc_0);
        il.Emit(OpCodes.Newobj, Method(adapter, ".ctor", module.TypeSystem.Void, command));
        il.Emit(OpCodes.Stloc_1);
        var innerStart = Instruction.Create(OpCodes.Nop); il.Append(innerStart);
        if (parameterLoop || parameterOnly || addRange)
        {
            var collection = Type("System.Data.SqlClient", "SqlParameterCollection");
            var parameter = Type("System.Data.SqlClient", "SqlParameter");
            il.Emit(OpCodes.Ldc_I4_0); il.Emit(OpCodes.Stloc_2);
            var loop = Instruction.Create(OpCodes.Ldloc_0); il.Append(loop);
            il.Emit(OpCodes.Callvirt, Method(command, "get_Parameters", collection));
            if (addRange)
            {
                il.Emit(argumentText ? OpCodes.Ldarg_1 : OpCodes.Ldarg_0);
                il.Emit(OpCodes.Callvirt, Method(collection, "AddRange", module.TypeSystem.Void,
                    new ArrayType(loopEffect == "wrong-signature" ? command : parameter)));
            }
            else if (loopEffect == "collection-escape")
            {
                var mutateCollection = new MethodReference("MutateCollection", module.TypeSystem.Void, type);
                mutateCollection.Parameters.Add(new ParameterDefinition(collection)); il.Emit(OpCodes.Call, mutateCollection);
            }
            else
            {
                il.Emit(OpCodes.Newobj, Method(parameter, ".ctor", module.TypeSystem.Void));
                il.Emit(OpCodes.Callvirt, Method(collection, "Add", module.TypeSystem.Int32,
                    loopEffect == "wrong-signature" ? command : module.TypeSystem.Object)); il.Emit(OpCodes.Pop);
            }
            if (loopEffect == "command-escape")
            {
                var mutateCommand = new MethodReference("MutateCommand", module.TypeSystem.Void, type);
                mutateCommand.Parameters.Add(new ParameterDefinition(command));
                il.Emit(OpCodes.Ldloc_0); il.Emit(OpCodes.Call, mutateCommand);
            }
            if (loopEffect == "conditional-text")
            {
                var skip = Instruction.Create(OpCodes.Nop);
                il.Emit(OpCodes.Ldloc_2); il.Emit(OpCodes.Brfalse_S, skip);
                il.Emit(OpCodes.Ldloc_0); il.Emit(OpCodes.Ldstr, "different-private-text");
                il.Emit(OpCodes.Callvirt, Method(commandBase, "set_CommandText", module.TypeSystem.Void, module.TypeSystem.String)); il.Append(skip);
            }
            if (parameterLoop)
            {
                il.Emit(OpCodes.Ldloc_2); il.Emit(OpCodes.Ldc_I4_1); il.Emit(OpCodes.Add); il.Emit(OpCodes.Stloc_2);
                il.Emit(OpCodes.Ldloc_2); il.Emit(OpCodes.Ldc_I4_3); il.Emit(OpCodes.Blt_S, loop);
            }
        }
        if (mutation)
        {
            var mutate = new MethodReference("Mutate", module.TypeSystem.Void, type);
            mutate.Parameters.Add(new ParameterDefinition(command));
            il.Emit(OpCodes.Ldloc_0); il.Emit(OpCodes.Call, mutate);
        }
        if (extraEffect is "timeout" or "wrong-timeout")
        {
            il.Emit(OpCodes.Ldloc_0); il.Emit(OpCodes.Ldc_I4_1);
            il.Emit(OpCodes.Callvirt, Method(command, "set_CommandTimeout", module.TypeSystem.Void,
                extraEffect == "timeout" ? module.TypeSystem.Int32 : module.TypeSystem.Object));
        }
        if (extraEffect is "transaction" or "wrong-transaction")
        {
            il.Emit(OpCodes.Ldloc_0); il.Emit(OpCodes.Ldnull);
            il.Emit(OpCodes.Callvirt, Method(command, "set_Transaction", module.TypeSystem.Void,
                extraEffect == "transaction" ? Type("System.Data.SqlClient", "SqlTransaction") : module.TypeSystem.Object));
        }
        if (extraEffect is "mapping" or "wrong-mapping" or "mapping-escape")
        {
            var mapping = Type("System.Data.Common", "DataTableMappingCollection");
            il.Emit(OpCodes.Ldloc_1); il.Emit(OpCodes.Callvirt, Method(adapterBase, "get_TableMappings", mapping));
            if (extraEffect == "mapping-escape")
            { var mutate = new MethodReference("MutateMappings", module.TypeSystem.Void, type); mutate.Parameters.Add(new(mapping)); il.Emit(OpCodes.Call, mutate); }
            else
            {
                il.Emit(OpCodes.Ldstr, "Table"); il.Emit(OpCodes.Ldstr, "Result");
                il.Emit(OpCodes.Callvirt, Method(mapping, "Add", Type("System.Data.Common", "DataTableMapping"),
                    module.TypeSystem.String, extraEffect == "mapping" ? module.TypeSystem.String : module.TypeSystem.Object)); il.Emit(OpCodes.Pop);
            }
        }
        if (extraEffect is "field-debug" or "field-escape")
        {
            var field = new FieldDefinition("Stored", FieldAttributes.Public | FieldAttributes.Static,
                extraEffect == "field-debug" ? module.TypeSystem.String : command); type.Fields.Add(field);
            if (extraEffect == "field-debug") il.Emit(OpCodes.Ldstr, "debug"); else il.Emit(OpCodes.Ldloc_0);
            il.Emit(OpCodes.Stsfld, field);
        }
        if (extraEffect == "array-escape")
        {
            il.Emit(OpCodes.Ldc_I4_1); il.Emit(OpCodes.Newarr, module.TypeSystem.Object);
            il.Emit(OpCodes.Ldc_I4_0); il.Emit(OpCodes.Ldloc_0); il.Emit(OpCodes.Stelem_Ref);
        }
        if (extraEffect is "field-indirect-debug" or "field-indirect-escape")
        {
            var field = new FieldDefinition("Indirect", FieldAttributes.Public | FieldAttributes.Static,
                extraEffect == "field-indirect-debug" ? module.TypeSystem.String : command); type.Fields.Add(field);
            il.Emit(OpCodes.Ldsflda, field);
            if (extraEffect == "field-indirect-debug") il.Emit(OpCodes.Ldstr, "debug"); else il.Emit(OpCodes.Ldloc_0);
            il.Emit(OpCodes.Stind_Ref);
        }
        if (extraEffect is "byref-escape" or "indirect-escape" or "address-before-assignment")
        {
            var indirect = extraEffect == "indirect-escape";
            var mutate = new MethodReference("MutateRef", module.TypeSystem.Void, type); mutate.Parameters.Add(new(indirect ? command : new ByReferenceType(command)));
            if (extraEffect == "address-before-assignment")
            {
                // Taking an address before copying a configured command into
                // its slot must expose the current object, not the old null.
                var alias = new VariableDefinition(command); var address = new VariableDefinition(new ByReferenceType(command));
                method.Body.Variables.Add(alias); method.Body.Variables.Add(address);
                il.Emit(OpCodes.Ldnull); il.Emit(OpCodes.Stloc, alias);
                il.Emit(OpCodes.Ldloca, alias); il.Emit(OpCodes.Stloc, address);
                il.Emit(OpCodes.Ldloc_0); il.Emit(OpCodes.Stloc, alias); il.Emit(OpCodes.Ldloc, address);
            }
            else il.Emit(OpCodes.Ldloca_S, method.Body.Variables[0]);
            if (indirect) il.Emit(OpCodes.Ldind_Ref);
            il.Emit(OpCodes.Call, mutate);
        }
        if (extraEffect == "boxed-escape")
        {
            var mutate = new MethodReference("MutateBox", module.TypeSystem.Void, type); mutate.Parameters.Add(new(module.TypeSystem.Object));
            il.Emit(OpCodes.Ldloc_0); il.Emit(OpCodes.Box, command); il.Emit(OpCodes.Call, mutate);
        }
        if (extraEffect is "unrelated-unknown" or "field-debug")
        {
            var unrelated = new MethodReference("Unrelated", module.TypeSystem.Void, type); unrelated.Parameters.Add(new(module.TypeSystem.Object));
            // A field read is unknown, but cannot alias an unexposed allocation.
            var field = new FieldDefinition("External", FieldAttributes.Public | FieldAttributes.Static, module.TypeSystem.Object); type.Fields.Add(field);
            il.Emit(OpCodes.Ldsfld, field); il.Emit(OpCodes.Call, unrelated);
        }
        il.Append(endpointLoad);
        il.Emit(OpCodes.Newobj, Method(dataSet, ".ctor", module.TypeSystem.Void));
        il.Emit(OpCodes.Callvirt, Method(adapterBase, "Fill", module.TypeSystem.Int32, dataSet));
        il.Emit(OpCodes.Pop);
        if (usingRegions)
        {
            var finalReturn = Instruction.Create(OpCodes.Ret);
            il.Emit(OpCodes.Leave, finalReturn);
            var disposable = module.ImportReference(typeof(IDisposable));
            var innerFinally = Instruction.Create(OpCodes.Ldloc_1); il.Append(innerFinally);
            var innerEnd = Instruction.Create(OpCodes.Endfinally);
            il.Emit(OpCodes.Brfalse_S, innerEnd); il.Emit(OpCodes.Ldloc_1);
            il.Emit(OpCodes.Callvirt, Method(disposable, "Dispose", module.TypeSystem.Void)); il.Append(innerEnd);
            var outerFinally = Instruction.Create(OpCodes.Ldloc_0); il.Append(outerFinally);
            var outerEnd = Instruction.Create(OpCodes.Endfinally);
            il.Emit(OpCodes.Brfalse_S, outerEnd); il.Emit(OpCodes.Ldloc_0);
            il.Emit(OpCodes.Callvirt, Method(disposable, "Dispose", module.TypeSystem.Void)); il.Append(outerEnd); il.Append(finalReturn);
            method.Body.ExceptionHandlers.Add(new ExceptionHandler(ExceptionHandlerType.Finally)
            { TryStart = innerStart, TryEnd = innerFinally, HandlerStart = innerFinally, HandlerEnd = outerFinally });
            method.Body.ExceptionHandlers.Add(new ExceptionHandler(ExceptionHandlerType.Finally)
            { TryStart = outerStart, TryEnd = outerFinally, HandlerStart = outerFinally, HandlerEnd = finalReturn });
        }
        else il.Emit(OpCodes.Ret);
        MethodDefinition? instanceConstructor = null;
        if (instanceRun)
        {
            instanceConstructor = new MethodDefinition(".ctor", MethodAttributes.Public | MethodAttributes.SpecialName
                | MethodAttributes.RTSpecialName, module.TypeSystem.Void);
            type.Methods.Add(instanceConstructor);
            var ctorIl = instanceConstructor.Body.GetILProcessor(); ctorIl.Emit(OpCodes.Ldarg_0);
            ctorIl.Emit(OpCodes.Call, Method(module.TypeSystem.Object, ".ctor", module.TypeSystem.Void)); ctorIl.Emit(OpCodes.Ret);
        }
        var target = method;
        for (var level = 1; level <= wrapperDepth; level++)
        {
            var entry = level == wrapperDepth;
            var forward = new MethodDefinition(entry ? "Entry" : $"Forward{level}",
                MethodAttributes.Public | MethodAttributes.Static, module.TypeSystem.Void);
            if (!entry) forward.Parameters.Add(new ParameterDefinition(module.TypeSystem.String));
            type.Methods.Add(forward);
            var forwardIl = forward.Body.GetILProcessor();
            if (target.HasThis) forwardIl.Emit(OpCodes.Newobj, instanceConstructor!);
            if (entry) forwardIl.Emit(OpCodes.Ldstr, PrivateLiteral); else forwardIl.Emit(OpCodes.Ldarg_0);
            forwardIl.Emit(OpCodes.Call, target); forwardIl.Emit(OpCodes.Ret); target = forward;
        }
        using var stream = new MemoryStream();
        assembly.Write(stream);
        var bytes = stream.ToArray();
        var limits = new IlBodyLimits();
        var cecil = IlBodyEvidenceExtractor.ReadCecilBodies(bytes, limits, new IlBodyEvidenceExtractor.IlWorkBudget(100_000), CancellationToken.None);
        var srm = IlBodyEvidenceExtractor.ReadSystemReflectionMetadataBodies(bytes, limits, new IlBodyEvidenceExtractor.IlWorkBudget(100_000), CancellationToken.None);
        Assert.Empty(IlBodyEvidenceExtractor.CompareBodies(cecil, srm));
        var body = Assert.Single(IlBodyEvidenceExtractor.AgreeValueFlows(cecil.Bodies, srm.Bodies),
            item => item.MethodIdentity.Contains("|method:3:Run|", StringComparison.Ordinal));
        Assert.DoesNotContain("IlValueReaderDisagreementOrLimit", body.ValueFlow!.Gaps);
        return (body, bytes);
    }

    private static string FindRepoRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))) return directory.FullName;
        throw new InvalidOperationException("TraceMap repository root not found.");
    }
}
