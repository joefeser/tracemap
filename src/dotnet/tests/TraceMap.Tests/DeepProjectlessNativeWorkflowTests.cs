using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using TraceMap.Cli;
using TraceMap.Core;
using TraceMap.Reporting;

namespace TraceMap.Tests;

[Collection("WebForms isolated allocation")]
public sealed class DeepProjectlessNativeWorkflowTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };

    [Fact]
    public async Task Deep_projectless_native_start_retains_the_real_compiled_chain_and_immutable_resume()
    {
        var repo = FindRepo();
        var root = Directory.CreateTempSubdirectory("tracemap-deep-native-").FullName;
        var source = Path.Combine(root, "source");
        var published = Path.Combine(root, "published");
        var build = Path.Combine(root, "build");
        try
        {
            Directory.CreateDirectory(Path.Combine(source, "Pages"));
            Directory.CreateDirectory(Path.Combine(source, "Provider"));
            Directory.CreateDirectory(Path.Combine(published, "bin"));
            Directory.CreateDirectory(Path.Combine(build, "website"));
            Directory.CreateDirectory(Path.Combine(build, "provider"));
            foreach (var file in new[] { "Lookup.aspx", "Lookup.aspx.vb", "Web.config" })
                File.Copy(Path.Combine(repo, "samples", "messy-dotnet-workspace", "vb-deep-projectless", file),
                    Path.Combine(source, "Pages", file));
            foreach (var file in new[] { "PublicLegacyCommandFlow.vb", "PublicSqlDataAccess.vb" })
                File.Copy(Path.Combine(repo, "samples", "messy-dotnet-workspace", "vb-publish-crossdll-framework", file),
                    Path.Combine(source, "Provider", file));
            foreach (var args in new[] { new[] { "init", "-q" }, ["config", "user.name", "Public synthetic corpus"],
                ["config", "user.email", "public@example.invalid"], ["config", "core.autocrlf", "false"],
                ["remote", "add", "origin", "https://example.invalid/deep-corpus.git"], ["add", "."], ["commit", "-qm", "Public deep corpus"] })
                await ProcessAsync("git", source, args, TimeSpan.FromSeconds(15));
            var commit = GitMetadataProvider.Detect(source).CommitSha;
            // Compile these exact committed fixture inputs, not unrelated prebuilt
            // binaries attributed to the temporary commit. Build projects stay outside
            // the source repository and the website remains genuinely projectless.
            File.WriteAllText(Path.Combine(build, "provider", "Provider.vbproj"), """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup><TargetFramework>net48</TargetFramework><RootNamespace></RootNamespace>
                    <AssemblyName>PublicProof.Framework</AssemblyName><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup>
                  <ItemGroup><Compile Include="../../source/Provider/*.vb"/><Reference Include="System.Data"/></ItemGroup>
                </Project>
                """);
            File.WriteAllText(Path.Combine(build, "website", "Website.vbproj"), """
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup><TargetFramework>net48</TargetFramework><RootNamespace></RootNamespace>
                    <AssemblyName>PublicProof.DeepWebsite</AssemblyName><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup>
                  <ItemGroup><Compile Include="../../source/Pages/Lookup.aspx.vb"/><Reference Include="System.Data"/>
                    <Reference Include="System.Web"/><ProjectReference Include="../provider/Provider.vbproj"/></ItemGroup>
                </Project>
                """);
            await ProcessAsync("dotnet", build, ["build", "website/Website.vbproj", "--verbosity", "quiet"], TimeSpan.FromMinutes(2));
            var bin = Path.Combine(build, "website", "bin", "Debug", "net48");
            foreach (var file in new[] { "PublicProof.DeepWebsite.dll", "PublicProof.Framework.dll" })
                File.Copy(Path.Combine(bin, file), Path.Combine(published, "bin", file));
            File.WriteAllText(Path.Combine(published, "Lookup.aspx.compiled"),
                "<preserve virtualPath=\"/public/Pages/Lookup.aspx\" assembly=\"PublicProof.DeepWebsite\" type=\"DeepLookup\"/>");
            var corpusInput = JsonSerializer.SerializeToUtf8Bytes(new { source = Roster(source),
                buildInputs = Directory.GetFiles(build, "*.vbproj", SearchOption.AllDirectories).Order()
                    .Select(file => Path.GetRelativePath(build, file) + ":" + Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(file)))) }, JsonOptions);
            File.WriteAllText(Path.Combine(root, "corpus-generator.receipt.local.json"), JsonSerializer.Serialize(new {
                schemaVersion = "validation.deep-native-corpus.v1", ruleId = "validation.deep-native-corpus.v1", visibility = "local-only",
                generatorSha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(typeof(DeepProjectlessNativeWorkflowTests).Assembly.Location))),
                boundedInputSha256 = Convert.ToHexStringLower(SHA256.HashData(corpusInput)),
                claim = "real-vb-compiler-output-with-generated-map-not-aspnet-publish", sourceCommitSha = commit,
                artifacts = Roster(published) }, JsonOptions));
            Assert.Empty(Directory.GetFiles(source, "*.*proj", SearchOption.AllDirectories));
            Assert.Empty(Directory.GetFiles(published, "*.pdb", SearchOption.AllDirectories));
            var beforeSource = Roster(source); var beforePublish = Roster(published);
            var config = new WebFormsReviewConfig(WebFormsReviewPreflightCommand.ConfigSchema, "fresh", source, commit,
                "projectless", null, [], ["Pages", "Provider"], "selected", ["Pages/Lookup.aspx"], published,
                ["bin/PublicProof.DeepWebsite.dll", "bin/PublicProof.Framework.dll"], [], [], [], ["Lookup.aspx.compiled"], null,
                new(GraphMaxDepth: 20, GraphMaxWork: 100_000)
                { Reports = new(MaxInputFacts: 10_000, MaxInputEdges: 10_000, MaxInputTextBytes: 32 * 1024 * 1024,
                    MaxOutputBytes: 32 * 1024 * 1024) { MaxGraphStorageBytes = 32 * 1024 * 1024 } },
                PublishSourceRelativePaths: ["Pages/Lookup.aspx", "Pages/Lookup.aspx.vb", "Provider/PublicLegacyCommandFlow.vb", "Provider/PublicSqlDataAccess.vb"]);
            var configPath = Path.Combine(root, "config.json");
            File.WriteAllText(configPath, JsonSerializer.Serialize(config, JsonOptions));
            var package = Path.Combine(root, "migration");
            var review = Path.Combine(package, "review");
            using var output = new StringWriter(); using var error = new StringWriter();
            Assert.True(await TraceMapCommand.RunAsync(["webforms-review", "migration-review", "--config", configPath,
                "--handler", "Lookup_Click", "--out", package,
                "--attest-exact-source-commit", commit], output, error) == 0, error.ToString());
            var migration = JsonSerializer.Deserialize<WebFormsMigrationHandoff>(File.ReadAllBytes(
                Path.Combine(package, "migration-handoff.local.json")), JsonOptions)!;
            Assert.Equal(WebFormsReviewExecutionCommand.MigrationHash(migration), migration.BoundedInputSha256);
            Assert.Equal("Lookup_Click", migration.Handler);
            Assert.Equal(6, migration.Artifacts.Count);
            foreach (var artifact in migration.Artifacts)
            {
                var bytes = File.ReadAllBytes(Path.Combine(package, artifact.RelativePath));
                Assert.Equal(artifact.Bytes, bytes.LongLength);
                Assert.Equal(artifact.Sha256, Convert.ToHexStringLower(SHA256.HashData(bytes)));
            }
            Assert.Contains("Claude review instructions", File.ReadAllText(Path.Combine(package, "START-HERE.md")));
            Assert.True(File.Exists(Path.Combine(package, "tool", "tracemap.dll")));
            using var queryOutput = new StringWriter();
            Assert.Equal(0, await TraceMapCommand.RunAsync(["webforms-review", "query-migration", "--root", package,
                "--document", "compiled", "--pointer", "/header/query/symbolRoots", "--limit", "5", "--depth", "2"], queryOutput, error));
            Assert.Contains("Lookup_Click", queryOutput.ToString());
            Assert.DoesNotContain("Branch_Click", queryOutput.ToString());
            var beforeRepeat = Roster(package);
            Assert.Equal(1, await TraceMapCommand.RunAsync(["webforms-review", "migration-review", "--config", configPath,
                "--handler", "Lookup_Click", "--out", package, "--attest-exact-source-commit", commit], output, error));
            Assert.Equal(beforeRepeat, Roster(package));
            var migrationIndex = Path.Combine(package, "review-evidence.sqlite");
            var originalIndex = File.ReadAllBytes(migrationIndex);
            try
            {
                using (var changed = new FileStream(migrationIndex, FileMode.Append)) changed.WriteByte(1);
                using var refused = new StringWriter();
                Assert.Equal(1, await TraceMapCommand.RunAsync(["webforms-review", "query-migration", "--root", package], refused, error));
                Assert.Equal("", refused.ToString());
            }
            finally { File.WriteAllBytes(migrationIndex, originalIndex); }
            var receiptPath = Path.Combine(package, "migration-handoff.local.json");
            var originalReceipt = File.ReadAllBytes(receiptPath);
            try
            {
                File.WriteAllText(receiptPath, JsonSerializer.Serialize(migration with { Handler = "Changed_Click" }, JsonOptions));
                using var refused = new StringWriter();
                Assert.Equal(1, await TraceMapCommand.RunAsync(["webforms-review", "query-migration", "--root", package], refused, error));
                Assert.Equal("", refused.ToString());
            }
            finally { File.WriteAllBytes(receiptPath, originalReceipt); }
            using (var refused = new StringWriter())
            {
                Assert.Equal(1, await TraceMapCommand.RunAsync(["webforms-review", "query-migration", "--root", package,
                    "--limit", "1000000"], refused, error));
                Assert.Equal("", refused.ToString());
            }
            // Reuse a valid native configuration without asserting a new binding.
            // Legacy proof import has a different receipt contract and separate tests.
            var readyConfig = Path.Combine(review, "evidence", "review-config.local.json");
            var importedPackage = Path.Combine(root, "imported-migration");
            using var importError = new StringWriter();
            Assert.True(await TraceMapCommand.RunAsync(["webforms-review", "migration-review", "--config", readyConfig,
                "--handler", "Lookup_Click", "--out", importedPackage], output, importError) == 0, importError.ToString());
            Assert.True(File.Exists(Path.Combine(importedPackage, "migration-handoff.local.json")));
            var legacyProof = CreateSyntheticLegacyProof(root, Path.Combine(review, "evidence"), commit);
            var draftPath = Path.Combine(Directory.CreateDirectory(Path.Combine(root, "input-config")).FullName, "draft.json");
            File.WriteAllText(draftPath, JsonSerializer.Serialize(config with { PrimaryAssemblies = [], DependencyAssemblies = [],
                PageMaps = [], PdbInputs = [], PublishSourceRelativePaths = null }, JsonOptions));
            var legacyPackage = Path.Combine(root, "legacy-import-migration");
            using var legacyError = new StringWriter();
            Assert.True(await TraceMapCommand.RunAsync(["webforms-review", "migration-review", "--config", draftPath,
                "--handler", "Lookup_Click", "--out", legacyPackage, "--proof-root", legacyProof,
                "--published-root", published, "--source-base", "."], output, legacyError) == 0, legacyError.ToString());
            Assert.True(File.Exists(Path.Combine(legacyPackage, "configuration", "proof-import.local.json")));
            Assert.True(File.Exists(Path.Combine(legacyPackage, "migration-handoff.local.json")));
            var changedProofPackage = Path.Combine(root, "changed-proof-migration");
            var legacyReceiptPath = Path.Combine(legacyProof, "publish-receipt.local.json");
            var legacyReceipt = File.ReadAllBytes(legacyReceiptPath);
            try
            {
                File.WriteAllText(legacyReceiptPath, "{}");
                Assert.Equal(1, await TraceMapCommand.RunAsync(["webforms-review", "migration-review", "--config", draftPath,
                    "--handler", "Lookup_Click", "--out", changedProofPackage, "--proof-root", legacyProof,
                    "--published-root", published, "--source-base", "."], output, legacyError));
                Assert.False(Directory.Exists(changedProofPackage));
            }
            finally { File.WriteAllBytes(legacyReceiptPath, legacyReceipt); }
            var refusedPackage = Path.Combine(root, "bad-handler-migration");
            using var failureError = new StringWriter();
            Assert.Equal(1, await TraceMapCommand.RunAsync(["webforms-review", "migration-review", "--config", readyConfig,
                "--handler", "NoSuchHandler", "--out", refusedPackage], output, failureError));
            Assert.True(File.Exists(Path.Combine(refusedPackage, "review", "run", "run-manifest.json")));
            Assert.False(File.Exists(Path.Combine(refusedPackage, "migration-handoff.local.json")));
            Assert.False(File.Exists(Path.Combine(refusedPackage, "START-HERE.md")));
            var run = Path.Combine(review, "run");
            var checkpoint = JsonSerializer.Deserialize<WebFormsReviewCheckpoint>(File.ReadAllText(
                Path.Combine(run, "checkpoints", "0004.json")), JsonOptions)!;
            Assert.Equal("reports-completed-review-only", checkpoint.State);
            var bundle = Path.Combine(run, checkpoint.Reports!.ReportAttempt);
            var handoff = Path.Combine(bundle, "compiled", "compiled-paths.handoff.local.json");
            var grouped = JsonSerializer.Deserialize<GroupedCompiledPathHandoff>(File.ReadAllBytes(handoff), JsonOptions)!;
            var original = GroupedCompiledPathHandoffBuilder.Restore(grouped);
            Assert.Equal("1.3", original.Query.AlgorithmVersion);
            Assert.Equal(6, original.Paths.Count);
            Assert.InRange(original.Summary.TraversalWorkUnits!.Value, 1, 1000);
            Assert.DoesNotContain(original.Gaps, gap => gap.GapKind == "TruncatedByLimit" && gap.Reason == "work");
            Console.WriteLine($"deepMixed.paths={original.Paths.Count};work={original.Summary.TraversalWorkUnits};truncated={original.Summary.Truncated}");
            Assert.NotEqual("compiled-il-with-root-attachment", original.Query.TraversalScope);
            Assert.Equal(5, original.Query.SymbolRoots!.Count);
            Assert.Contains(original.Paths, path => path.Nodes.Any(node => node.SymbolId?.Contains("|method:12:Lookup_Click|", StringComparison.Ordinal) == true)
                && path.Nodes.Last().CommandBinding?.CommandTextFromPath?.State == "constant-on-encoded-call-path");
            var encodedLookup = original.Paths.First(path => path.Nodes.Any(node => node.SymbolId?.Contains("|method:12:Lookup_Click|", StringComparison.Ordinal) == true)
                && path.Nodes.Last().CommandBinding?.CommandTextFromPath?.State == "constant-on-encoded-call-path");
            var encodedMethods = encodedLookup.Nodes.Select(node => System.Text.RegularExpressions.Regex.Match(node.SymbolId ?? "", @"\|method:\d+:([^|]+)\|"))
                .Where(match => match.Success).Select(match => match.Groups[1].Value);
            Assert.Equal(new[] { "Lookup_Click" }.Concat(Enumerable.Range(1, 12).Select(n => $"Layer{n:00}"))
                .Concat(["Run", "Run"]), encodedMethods);
            Assert.Equal("4", encodedLookup.Nodes.Last().CommandBinding!.CommandTypeFromPath!.Origin.Identity);
            foreach (var handler in new[] { "Lookup_Click", "Branch_Click", "Cycle_Click", "Unknown_Click", "Comparison_Click" })
            {
                var marker = $"|method:{handler.Length}:{handler}|";
                var retainedPaths = original.Paths.Where(path => path.Nodes.Any(node => node.SymbolId?.Contains(marker, StringComparison.Ordinal) == true)
                    && path.Nodes.Last().SurfaceName == "DbDataAdapter.Fill").ToArray();
                Assert.Equal(handler == "Branch_Click" ? 2 : 1, retainedPaths.Length);
                if (handler is "Unknown_Click" or "Comparison_Click")
                    Assert.All(retainedPaths, path => Assert.NotEqual("constant-on-encoded-call-path", path.Nodes.Last().CommandBinding?.CommandTextFromPath?.State));
                foreach (var text in handler switch
                {
                    "Lookup_Click" => new[] { "public.deep_lookup" },
                    "Branch_Click" => ["public.branch_left", "public.branch_right"],
                    "Cycle_Click" => ["public.cycle_exit"],
                    _ => Array.Empty<string>()
                })
                {
                    var identity = $"str:{text.Length}:{Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.Unicode.GetBytes(text)))}";
                    Assert.Contains(retainedPaths, path => path.Nodes.Last().CommandBinding?.CommandTextFromPath is
                        { State: "constant-on-encoded-call-path" } value && value.Origin.Identity == identity);
                }
                Assert.All(retainedPaths, path => Assert.DoesNotContain(path.Nodes, node => node.SymbolId?.Contains("DeepDecoy", StringComparison.Ordinal) == true));
            }
            var selected = Assert.Single(original.Query.SymbolRoots!, item => item.SymbolId.StartsWith("DeepLookup.Lookup_Click(", StringComparison.Ordinal));
            var beforeRequery = Roster(review);
            foreach (var compiledOnly in new[] { false, true })
            {
                var destination = Path.Combine(root, compiledOnly ? "completed-il" : "completed-mixed");
                var args = new List<string> { "webforms-review", "requery-handler", "--run", run,
                    "--bundle", bundle, "--handler", "Lookup_Click", "--out", destination, "--surface-name", "DbDataAdapter.Fill" };
                if (compiledOnly) args.AddRange(["--traversal-scope", "compiled-il"]);
                Assert.True(await TraceMapCommand.RunAsync(args.ToArray(), output, error) == 0, error.ToString());
                var receipt = JsonSerializer.Deserialize<WebFormsHandlerRequeryReceipt>(File.ReadAllBytes(
                    Path.Combine(destination, "handler-requery.local.json")), JsonOptions)!;
                Assert.Null(receipt.RecoveryReceiptSha256);
                Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(handoff))), receipt.CompletedReportSha256);
                Assert.Equal(WebFormsReviewExecutionCommand.HandlerRequeryHash(receipt), receipt.BoundedInputSha256);
                Assert.Equal(selected, Assert.Single(receipt.Query.SymbolRoots!));
                var result = GroupedCompiledPathHandoffBuilder.Restore(JsonSerializer.Deserialize<GroupedCompiledPathHandoff>(
                    File.ReadAllBytes(Path.Combine(destination, "compiled-paths.handoff.local.json")), JsonOptions)!);
                Assert.Contains(result.Paths, candidate => candidate.Nodes.Last().CommandBinding?.CommandTextFromPath?.State == "constant-on-encoded-call-path");
                Assert.Equal(1, result.Summary.SelectorCandidateCount);
                Assert.Equal(beforeRequery, Roster(review));
                Assert.Equal(1, await TraceMapCommand.RunAsync(args.ToArray(), output, error));
            }
            var wrongBundle = Path.Combine(root, "wrong-bundle"); Directory.CreateDirectory(wrongBundle);
            Assert.Equal(1, await TraceMapCommand.RunAsync(["webforms-review", "requery-handler", "--run", run,
                "--bundle", wrongBundle, "--handler", "Lookup_Click", "--out", Path.Combine(root, "wrong-query")], output, error));
            Assert.False(Directory.Exists(Path.Combine(root, "wrong-query")));
            var handoffBytes = File.ReadAllBytes(handoff);
            try
            {
                File.AppendAllText(handoff, " ");
                Assert.Equal(1, await TraceMapCommand.RunAsync(["webforms-review", "requery-handler", "--run", run,
                    "--bundle", bundle, "--handler", "Lookup_Click", "--out", Path.Combine(root, "changed-query")], output, error));
                Assert.False(Directory.Exists(Path.Combine(root, "changed-query")));
            }
            finally { File.WriteAllBytes(handoff, handoffBytes); }
            var retainedIndex = Path.Combine(bundle, "combined.sqlite");
            var indexLength = new FileInfo(retainedIndex).Length;
            try
            {
                using (var changedIndex = new FileStream(retainedIndex, FileMode.Append, FileAccess.Write)) changedIndex.WriteByte(0);
                Assert.Equal(1, await TraceMapCommand.RunAsync(["webforms-review", "requery-handler", "--run", run,
                    "--bundle", bundle, "--handler", "Lookup_Click", "--out", Path.Combine(root, "changed-index-query")], output, error));
                Assert.False(Directory.Exists(Path.Combine(root, "changed-index-query")));
            }
            finally { using var restoredIndex = new FileStream(retainedIndex, FileMode.Open, FileAccess.Write); restoredIndex.SetLength(indexLength); }
            Assert.Equal(beforeRequery, Roster(review));
            // Same retained index, deliberately narrower IL-only query: source
            // bridges in the multi-root workbench are not encoded IL transitions.
            var report = await CombinedDependencyPathReporter.BuildSelectedSymbolsAsync(
                new CombinedDependencyPathOptions(Path.Combine(bundle, "combined.sqlite"), Path.Combine(root, "handler-query"),
                    ToSurface: "database-api", SurfaceName: "DbDataAdapter.Fill", IncludeLegacyRoots: true, MaxDepth: 20)
                { CompiledOnly = true, ExactFromSymbol = true, MaxTraversalWork = 100_000 }, [selected], combinedIndex: true,
                limits: new(MaxFacts: 10_000, MaxEdges: 10_000, MaxTextBytes: 32 * 1024 * 1024)
                { MaxGraphStorageBytes = 32 * 1024 * 1024 });
            var paths = report.Paths.Where(path => path.Nodes.Any(node => node.SymbolId?.Contains("|method:12:Lookup_Click|", StringComparison.Ordinal) == true)
                && path.Nodes.Last().SurfaceName == "DbDataAdapter.Fill").ToArray();
            Assert.True(paths.Length > 0, JsonSerializer.Serialize(new { report.Query, report.Summary,
                Paths = report.Paths.Select(path => path.Nodes.Select(node => new { node.SymbolId, node.SurfaceName })) }, JsonOptions));
            var path = Assert.Single(paths, path => path.Nodes.Last().CommandBinding?.CommandTextFromPath?.State == "constant-on-encoded-call-path");
            var binding = Assert.IsType<CompiledCommandConfigurationCandidate>(path.Nodes.Last().CommandBinding);
            Assert.Equal("4", binding.CommandTypeFromPath!.Origin.Identity);
            var symbols = string.Join("\n", path.Nodes.Select(node => node.SymbolId));
            foreach (var layer in Enumerable.Range(1, 12)) Assert.Contains($"Layer{layer:00}", symbols);
            Assert.Contains("PublicProof.Framework", symbols);
            Assert.Contains("PublicProof.DeepWebsite", symbols);
            var retained = Roster(review);
            Assert.Equal(0, await TraceMapCommand.RunAsync(["webforms-review", "resume", "--run", run], output, error));
            Assert.Equal(retained, Roster(review));
            Assert.Equal(beforeSource, Roster(source)); Assert.Equal(beforePublish, Roster(published));
            var reportFiles = Directory.GetFiles(bundle, "*", SearchOption.AllDirectories).Select(file => new FileInfo(file)).ToArray();
            // MaxOutputBytes bounds each published document; the retained evidence
            // SQLite database is a separate index, not a rendered document.
            Assert.All(reportFiles, file => Assert.InRange(file.Length, 1,
                file.Extension == ".sqlite" ? 64 * 1024 * 1024 : 32 * 1024 * 1024));
            Assert.InRange(reportFiles.Sum(file => file.Length), 1, 128 * 1024 * 1024);
            // The native root also retains a full CLI distribution, not just reports.
            Assert.InRange(Directory.GetFiles(review, "*", SearchOption.AllDirectories).Sum(file => new FileInfo(file).Length), 1, 128 * 1024 * 1024);
        }
        finally
        {
            // Git marks object files read-only on Windows. This root is created
            // and owned solely by this synthetic test; never touch the checkout.
            foreach (var file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
            {
                var attributes = File.GetAttributes(file);
                if ((attributes & FileAttributes.ReadOnly) != 0)
                    File.SetAttributes(file, attributes & ~FileAttributes.ReadOnly);
            }
            Directory.Delete(root, recursive: true);
        }
    }

    private static string[] Roster(string root) => Directory.GetFiles(root, "*", SearchOption.AllDirectories)
        .Order(StringComparer.Ordinal).Select(file => Path.GetRelativePath(root, file) + ":" +
            Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(file)))).ToArray();

    // Generate the historical wrapper receipt format over this exact public
    // compiler-produced corpus. Not an authentic ASP.NET publication receipt.
    private static string CreateSyntheticLegacyProof(string root, string nativeProof, string commit)
    {
        static string Hash(string text) => Convert.ToHexStringLower(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)));
        static string Field(JsonNode node, string name) => node[name]!.GetValue<string>();
        var publish = JsonNode.Parse(File.ReadAllText(Path.Combine(nativeProof, "publish-receipt.local.json")))!.AsObject();
        var binding = JsonNode.Parse(File.ReadAllText(Path.Combine(nativeProof, "compiled-binding.local.json")))!.AsObject();
        var sourceRows = publish["sourceFiles"]!.AsArray().Select(item => item!).ToArray();
        var publishedRows = publish["publishedFiles"]!.AsArray().Select(item => item!).ToArray();
        var assemblies = publishedRows.Where(item => Field(item, "kind") == "assembly").ToArray();
        var maps = publishedRows.Where(item => Field(item, "kind") == "compiled-map").ToArray();
        var sourceHash = Hash(string.Join('\n', sourceRows.Select(item => $"{Field(item, "path")}:{Field(item, "sha256")}")) + "\n");
        var inventoryHash = Hash(string.Join('\n', assemblies.Select(item => $"{Field(item, "path")}:{Field(item, "sha256")}:selected")) + "\n");
        var mapHash = Hash(string.Join('\n', maps.Select(item => $"{Field(item, "path")}:{Field(item, "sha256")}").Order(StringComparer.Ordinal)) + "\n");
        var repositoryHash = Hash("https://example.invalid/deep-corpus.git");
        var generator = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(typeof(DeepProjectlessNativeWorkflowTests).Assembly.Location)));
        publish["receiptGeneratorSha256"] = generator;
        publish["compilerProvenance"] = "unavailable-existing-output";
        publish["assemblyInventory"] = new JsonArray(assemblies.Select(item => (JsonNode)new JsonObject
            { ["path"] = Field(item, "path"), ["sha256"] = Field(item, "sha256"), ["disposition"] = "selected" }).ToArray());
        publish["boundedInputSha256"] = sourceHash;
        publish["assemblyInventorySha256"] = inventoryHash;
        publish["mapInventorySha256"] = mapHash;
        publish["publishedMapCount"] = maps.Length;
        publish["receiptInputSha256"] = Hash($"source:{repositoryHash}\ncommit:{commit}\nsource:{sourceHash}\nassemblies:{inventoryHash}\nmaps:{mapHash}\n");
        var bindingRows = binding["bindings"]!.AsArray().Select(item => item!).OrderBy(item => Field(item, "safeLocator"), StringComparer.Ordinal);
        binding["generatorSha256"] = generator;
        binding["boundedInputSha256"] = Hash(string.Join('\n', bindingRows.Select(item =>
            $"{Field(item, "safeLocator")}:{Field(item, "artifactSha256")}:{Field(item, "assemblyIdentity")}:{commit}")) +
            $"\nsource:{sourceHash}\nsource-repository:{repositoryHash}\nassembly-inventory:{inventoryHash}\nmap-inventory:{mapHash}\n");
        var proof = Directory.CreateDirectory(Path.Combine(root, "synthetic-legacy-proof")).FullName;
        File.WriteAllText(Path.Combine(proof, "publish-receipt.local.json"), publish.ToJsonString(JsonOptions));
        File.WriteAllText(Path.Combine(proof, "compiled-binding.local.json"), binding.ToJsonString(JsonOptions));
        return proof;
    }

    private static async Task ProcessAsync(string executable, string cwd, string[] args, TimeSpan limit)
    {
        var start = new ProcessStartInfo(executable) { WorkingDirectory = cwd, UseShellExecute = false,
            RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in args) start.ArgumentList.Add(arg);
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(limit);
        try { await process.WaitForExitAsync(timeout.Token); }
        finally { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        Assert.True(process.ExitCode == 0, $"{executable} failed:\n{await stdout}\n{await stderr}");
    }

    private static string FindRepo()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))) return directory.FullName;
        throw new InvalidOperationException("TraceMap repository root not found.");
    }
}
