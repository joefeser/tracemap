using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TraceMap.Cli;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class WebFormsReviewPreflightTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };

    [Fact]
    public async Task Fresh_plan_is_deterministic_inventory_not_execution_or_binding()
    {
        using var fixture = new Fixture();
        var first = await fixture.Build();
        var second = await fixture.Build();
        Assert.Equal(first.BoundedInputSha256, second.BoundedInputSha256);
        Assert.NotEqual(first.RunId, second.RunId);
        Assert.Equal("local-only", first.Visibility);
        Assert.Equal("preflight-only-not-executed", first.ClaimLevel);
        Assert.Equal(3, first.Inputs.Count);
        Assert.Contains(first.Inputs, input => input.Role == "primary-assembly" && input.MetadataName!.StartsWith("tracemap, Version=", StringComparison.Ordinal));
        Assert.Contains("BindingValidationDeferred", first.Gaps);
        Assert.Contains("MissingBindingReceiptHashCandidate", first.Gaps);
        Assert.Contains("PdbInputsNotDeclared", first.Gaps);
        Assert.All(first.Phases.Where(phase => phase.Phase != "preflight"), phase => Assert.Equal("pending", phase.State));
        Assert.False(Directory.Exists(fixture.Output));
        Assert.Equal(Hash(typeof(WebFormsReviewPreflightCommand).Assembly.Location), first.GeneratorSha256);
        var expected = HashText(JsonSerializer.Serialize(new { configurationSha256 = first.Inputs.Single(input => input.Role == "configuration").Sha256, inputs = first.Inputs }, JsonOptions));
        Assert.Equal(expected, first.BoundedInputSha256);
        Assert.Equal(first.Inputs.Sum(input => input.Bytes), first.HashedBytes);
    }

    [Theory]
    [InlineData(256)]
    [InlineData(257)]
    [InlineData(4096)]
    public async Task Explicit_graph_path_budget_is_retained_without_raising_other_defaults(int paths)
    {
        using var fixture = new Fixture();
        var defaults = new WebFormsReviewBudgets();
        Assert.Equal(256, defaults.GraphMaxPaths);
        fixture.Config = fixture.Config with { Budgets = defaults with { GraphMaxPaths = paths } };
        var result = await fixture.Build();
        Assert.Equal(defaults with { GraphMaxPaths = paths }, result.Configuration.Budgets);
        Assert.False(Directory.Exists(fixture.Output));
    }

    [Fact]
    public async Task Attachment_inventories_all_parent_artifacts_without_mutating_them()
    {
        using var fixture = new Fixture();
        fixture.AddParent();
        fixture.Config = fixture.Config with { Budgets = fixture.Config.Budgets with { MaxRetainedArtifactBytes = 4_294_967_296 } };
        var original = Directory.GetFiles(fixture.Parent, "*", SearchOption.AllDirectories).ToDictionary(path => path, Hash);
        var result = await fixture.Build();
        Assert.Equal("public-parent", result.ParentScanId);
        Assert.Equal(5, result.Inputs.Count(input => input.Role.StartsWith("parent-", StringComparison.Ordinal)));
        Assert.Equal("retained-not-validated", result.Phases.Single(phase => phase.Phase == "source").State);
        Assert.Contains("ParentRepositoryAndIndexValidationDeferred", result.Gaps);
        foreach (var pair in original) Assert.Equal(pair.Value, Hash(pair.Key));
    }

    [Fact]
    public async Task Receipt_hash_candidate_never_becomes_source_binding()
    {
        using var fixture = new Fixture();
        var hash = Hash(fixture.AssemblyPath);
        File.WriteAllText(Path.Combine(fixture.Published, "binding.json"), JsonSerializer.Serialize(new
        {
            schemaVersion = "compiled-input-binding-set.v1", bindings = new[] { new { artifactSha256 = hash } }
        }));
        fixture.Config = fixture.Config with { BindingReceipts = ["binding.json"] };
        var result = await fixture.Build();
        Assert.DoesNotContain("MissingBindingReceiptHashCandidate", result.Gaps);
        Assert.Contains("BindingValidationDeferred", result.Gaps);
        Assert.DoesNotContain(result.Phases, phase => phase.Phase == "binding" && phase.State == "completed");
    }

    [Fact]
    public async Task Duplicate_receipt_candidates_are_explicit_gaps()
    {
        using var fixture = new Fixture();
        var binding = new { artifactSha256 = Hash(fixture.AssemblyPath) };
        File.WriteAllText(Path.Combine(fixture.Published, "binding.json"), JsonSerializer.Serialize(new
        { schemaVersion = "compiled-input-binding-set.v1", bindings = new[] { binding, binding } }));
        fixture.Config = fixture.Config with { BindingReceipts = ["binding.json"] };
        Assert.Contains("AmbiguousBindingReceiptHashCandidate", (await fixture.Build()).Gaps);
    }

    [Fact]
    public async Task Separate_receipt_root_pins_receipt_bytes_without_writing_to_published_files()
    {
        using var fixture = new Fixture();
        var evidence = WebFormsReviewPreflightCommand.PhysicalPath(Path.Combine(fixture.Root, "evidence"));
        Directory.CreateDirectory(evidence);
        var receipt = Path.Combine(evidence, "binding.json");
        File.WriteAllText(receipt, JsonSerializer.Serialize(new
        { schemaVersion = "compiled-input-binding-set.v1", bindings = new[] { new { artifactSha256 = Hash(fixture.AssemblyPath) } } }));
        fixture.Config = fixture.Config with { ReceiptRoot = evidence, BindingReceipts = ["binding.json"] };
        var publishedBefore = Directory.GetFiles(fixture.Published, "*", SearchOption.AllDirectories).ToDictionary(path => path, Hash);
        var result = await fixture.Build();
        Assert.Equal(evidence, result.Configuration.ReceiptRoot);
        Assert.Contains(result.Inputs, input => input.Role == "binding-receipt" && input.Path == receipt && input.Sha256 == Hash(receipt));
        Assert.DoesNotContain("MissingBindingReceiptHashCandidate", result.Gaps);
        foreach (var pair in publishedBefore) Assert.Equal(pair.Value, Hash(pair.Key));
        Assert.Equal(publishedBefore.Count, Directory.GetFiles(fixture.Published, "*", SearchOption.AllDirectories).Length);
        var exception = await Assert.ThrowsAnyAsync<Exception>(() => fixture.Build(Path.Combine(evidence, "new-run")));
        Assert.Equal("WEBFORMS_PREFLIGHT_OUTPUT_OVERLAPS_INPUT", exception.Message);
    }

    [Theory]
    [InlineData("relative", "ROOT_PATH_INVALID")]
    [InlineData("missing", "ROOT_UNAVAILABLE")]
    [InlineData("escape", "INPUT_ESCAPES_ROOT")]
    public async Task Separate_receipt_root_rejects_invalid_or_escaped_inputs(string mutation, string suffix)
    {
        if (mutation == "escape" && OperatingSystem.IsWindows()) return; // Native junction lane remains required.
        using var fixture = new Fixture();
        var evidence = Path.Combine(fixture.Root, "evidence");
        if (mutation != "missing") Directory.CreateDirectory(evidence);
        if (mutation == "escape") Directory.CreateSymbolicLink(Path.Combine(evidence, "escape"), fixture.Published);
        fixture.Config = fixture.Config with { ReceiptRoot = mutation == "relative" ? "relative" : evidence,
            BindingReceipts = mutation == "escape" ? ["escape/binding.json"] : [] };
        var exception = await Assert.ThrowsAnyAsync<Exception>(() => fixture.Build());
        Assert.Equal("WEBFORMS_PREFLIGHT_" + suffix, exception.Message);
        Assert.False(Directory.Exists(fixture.Output));
    }

    [Fact]
    public async Task Explicit_root_scope_and_all_page_mode_are_planning_not_all_page_acceptance()
    {
        using var fixture = new Fixture();
        fixture.Config = fixture.Config with { SourceFolders = ["."], PageMode = "all", PageRelativePaths = [] };
        var result = await fixture.Build();
        Assert.Equal("all", result.Configuration.PageMode);
        Assert.Equal(2, result.Inputs.Count);
        Assert.Equal("pending", result.Phases.Single(phase => phase.Phase == "compiled-scan").State);
    }

    [Fact]
    public async Task Declared_PDB_and_map_are_inventory_only_even_when_bytes_are_not_valid_formats()
    {
        using var fixture = new Fixture();
        File.WriteAllText(Path.Combine(fixture.Published, "public.pdb"), "public unvalidated PDB input");
        File.WriteAllText(Path.Combine(fixture.Published, "public.compiled"), "public unvalidated map input");
        fixture.Config = fixture.Config with { PdbInputs = ["public.pdb"], PageMaps = ["public.compiled"] };
        var result = await fixture.Build();
        Assert.DoesNotContain("PdbInputsNotDeclared", result.Gaps);
        Assert.DoesNotContain("PageMapsNotDeclared", result.Gaps);
        Assert.Contains("PdbIdentityValidationDeferred", result.Gaps);
        Assert.Contains("PageMapValidationDeferred", result.Gaps);
    }

    [Fact]
    public async Task Nonmanaged_assembly_and_invalid_receipt_never_produce_a_run()
    {
        using var fixture = new Fixture();
        File.WriteAllText(fixture.AssemblyPath, "not a managed PE");
        await Assert.ThrowsAnyAsync<Exception>(() => fixture.Build());
        Assert.False(Directory.Exists(fixture.Output));
        File.Copy(typeof(WebFormsReviewPreflightCommand).Assembly.Location, fixture.AssemblyPath, overwrite: true);
        File.WriteAllText(Path.Combine(fixture.Published, "binding.json"), "{\"schemaVersion\":\"other\",\"bindings\":[]}");
        fixture.Config = fixture.Config with { BindingReceipts = ["binding.json"] };
        var invalid = await Assert.ThrowsAnyAsync<Exception>(() => fixture.Build());
        Assert.Equal("WEBFORMS_PREFLIGHT_BINDING_RECEIPT_INVALID", invalid.Message);
        Assert.False(Directory.Exists(fixture.Output));
    }

    [Theory]
    [InlineData("schema", "CONFIG_INVALID")]
    [InlineData("mode", "PROJECT_SELECTION_INVALID")]
    [InlineData("all-pages", "PAGE_SELECTION_INVALID")]
    [InlineData("parent", "PARENT_SELECTION_INVALID")]
    [InlineData("duplicate", "CONFIG_INVALID")]
    [InlineData("traversal", "RELATIVE_PATH_INVALID")]
    [InlineData("missing", "unreadable")]
    [InlineData("bytes", "FILE_BYTES_LIMIT")]
    [InlineData("count", "INPUT_COUNT_LIMIT")]
    [InlineData("total", "HASH_BYTES_LIMIT")]
    [InlineData("work", "BUDGET_INVALID")]
    [InlineData("source", "SOURCE_COMMIT_MISMATCH")]
    [InlineData("relative-root", "ROOT_PATH_INVALID")]
    [InlineData("metadata-work", "BUDGET_INVALID")]
    [InlineData("metadata-text", "BUDGET_INVALID")]
    [InlineData("il-text", "BUDGET_INVALID")]
    [InlineData("parent-facts", "BUDGET_INVALID")]
    [InlineData("fact-line", "BUDGET_INVALID")]
    [InlineData("graph-path-low", "BUDGET_INVALID")]
    [InlineData("graph-path-high", "BUDGET_INVALID")]
    public async Task Invalid_configuration_is_rejected_before_output(string mutation, string expected)
    {
        using var fixture = new Fixture();
        fixture.Config = mutation switch
        {
            "schema" => fixture.Config with { SchemaVersion = "other" },
            "mode" => fixture.Config with { ProjectMode = "solution" },
            "all-pages" => fixture.Config with { PageMode = "all" },
            "parent" => fixture.Config with { Operation = "attach" },
            "duplicate" => fixture.Config with { PrimaryAssemblies = ["bin/Public.dll", "bin/Public.dll"] },
            "traversal" => fixture.Config with { PrimaryAssemblies = ["../Public.dll"] },
            "missing" => fixture.Config with { PrimaryAssemblies = ["missing.dll"] },
            "bytes" => fixture.Config with { Budgets = fixture.Config.Budgets with { MaxAssemblyBytes = 1 } },
            "count" => fixture.Config with { Budgets = fixture.Config.Budgets with { MaxInputFiles = 1 } },
            "total" => fixture.Config with { Budgets = fixture.Config.Budgets with { MaxTotalHashBytes = 1 } },
            "work" => fixture.Config with { Budgets = fixture.Config.Budgets with { IlMaxWork = 0 } },
            "source" => fixture.Config with { SourceCommitSha = new string('b', 40) },
            "relative-root" => fixture.Config with { SourceRoot = "relative" },
            "metadata-work" => fixture.Config with { Budgets = fixture.Config.Budgets with { MetadataMaxWork = 0 } },
            "metadata-text" => fixture.Config with { Budgets = fixture.Config.Budgets with { MetadataMaxText = 70 } },
            "il-text" => fixture.Config with { Budgets = fixture.Config.Budgets with { IlMaxText = 65_537 } },
            "parent-facts" => fixture.Config with { Budgets = fixture.Config.Budgets with { MaxParentFacts = 0 } },
            "fact-line" => fixture.Config with { Budgets = fixture.Config.Budgets with { MaxFactLineChars = 127 } },
            "graph-path-low" => fixture.Config with { Budgets = fixture.Config.Budgets with { GraphMaxPaths = 0 } },
            "graph-path-high" => fixture.Config with { Budgets = fixture.Config.Budgets with { GraphMaxPaths = 4097 } },
            _ => throw new InvalidOperationException()
        };
        var exception = await Assert.ThrowsAnyAsync<Exception>(() => fixture.Build());
        if (expected != "unreadable") Assert.Equal("WEBFORMS_PREFLIGHT_" + expected, exception.Message);
        Assert.False(Directory.Exists(fixture.Output));
    }

    [Fact]
    public async Task Parent_commit_mismatch_fails_closed()
    {
        using var fixture = new Fixture();
        fixture.AddParent();
        File.WriteAllText(Path.Combine(fixture.Parent, "scan-manifest.json"), "{\"scanId\":\"other\",\"commitSha\":\"" + new string('b', 40) + "\"}");
        var exception = await Assert.ThrowsAnyAsync<Exception>(() => fixture.Build());
        Assert.Equal("WEBFORMS_PREFLIGHT_PARENT_PROVENANCE_MISMATCH", exception.Message);
    }

    [Fact]
    public async Task Output_must_be_fresh_and_outside_every_input_root()
    {
        using var fixture = new Fixture();
        foreach (var output in new[] { Path.Combine(fixture.Source, "new-run"), Path.Combine(fixture.Published, "new-run") })
        {
            var exception = await Assert.ThrowsAnyAsync<Exception>(() => fixture.Build(output));
            Assert.Equal("WEBFORMS_PREFLIGHT_OUTPUT_OVERLAPS_INPUT", exception.Message);
        }
        Directory.CreateDirectory(fixture.Output);
        var existing = await Assert.ThrowsAnyAsync<Exception>(() => fixture.Build());
        Assert.Equal("WEBFORMS_PREFLIGHT_OUTPUT_EXISTS", existing.Message);
    }

    [Fact]
    public async Task Linked_input_escape_is_rejected()
    {
        if (OperatingSystem.IsWindows()) return; // Windows junction acceptance needs the native lane.
        using var fixture = new Fixture();
        Directory.CreateSymbolicLink(Path.Combine(fixture.Published, "escape"), fixture.Source);
        fixture.Config = fixture.Config with { PrimaryAssemblies = ["escape/Pages/Lookup.aspx"] };
        var exception = await Assert.ThrowsAnyAsync<Exception>(() => fixture.Build());
        Assert.Equal("WEBFORMS_PREFLIGHT_INPUT_ESCAPES_ROOT", exception.Message);
    }

    [Fact]
    public async Task Duplicate_or_unknown_JSON_fields_cannot_change_configuration_silently()
    {
        using var fixture = new Fixture();
        fixture.Save();
        var json = File.ReadAllText(fixture.ConfigPath);
        File.WriteAllText(fixture.ConfigPath, json.Replace("\"operation\": \"fresh\"", "\"operation\": \"fresh\", \"operation\": \"attach\"", StringComparison.Ordinal));
        var duplicate = await Assert.ThrowsAnyAsync<Exception>(() => fixture.Build(save: false));
        Assert.Equal("WEBFORMS_PREFLIGHT_JSON_DUPLICATE_PROPERTY", duplicate.Message);
        File.WriteAllText(fixture.ConfigPath, json.Replace("\"operation\"", "\"typoOperation\"", StringComparison.Ordinal));
        await Assert.ThrowsAsync<JsonException>(() => fixture.Build(save: false));
    }

    [Fact]
    public async Task Cancellation_does_not_create_output()
    {
        using var fixture = new Fixture();
        fixture.Save();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => WebFormsReviewPreflightCommand.BuildAsync(fixture.ConfigPath, fixture.Output, new CancellationToken(true)));
        Assert.False(Directory.Exists(fixture.Output));
    }

    [Fact]
    public async Task CLI_creates_only_private_preflight_artifacts_and_never_overwrites_run()
    {
        using var fixture = new Fixture();
        fixture.InitializeGit();
        fixture.Save();
        var sourceBefore = Directory.GetFiles(fixture.Source, "*", SearchOption.AllDirectories).ToDictionary(path => path, Hash);
        var assemblyBefore = Hash(fixture.AssemblyPath);
        using var output = new StringWriter();
        using var error = new StringWriter();
        var args = new[] { "webforms-review", "preflight", "--config", fixture.ConfigPath, "--out", fixture.Output };
        Assert.Equal(0, await TraceMapCommand.RunAsync(args, output, error));
        Assert.Equal(string.Empty, error.ToString());
        Assert.Equal(new[] { "README.md", "run-manifest.json" }, Directory.GetFiles(fixture.Output).Select(Path.GetFileName).Order(StringComparer.Ordinal));
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(fixture.Output, "run-manifest.json")));
        Assert.Equal(WebFormsReviewPreflightCommand.ManifestSchema, manifest.RootElement.GetProperty("schemaVersion").GetString());
        Assert.Equal("preflight-only-not-executed", manifest.RootElement.GetProperty("claimLevel").GetString());
        foreach (var pair in sourceBefore) Assert.Equal(pair.Value, Hash(pair.Key));
        Assert.Equal(assemblyBefore, Hash(fixture.AssemblyPath));
        var manifestHash = Hash(Path.Combine(fixture.Output, "run-manifest.json"));
        Assert.Equal(1, await TraceMapCommand.RunAsync(args, output, error));
        Assert.Contains("WEBFORMS_PREFLIGHT_OUTPUT_EXISTS", error.ToString(), StringComparison.Ordinal);
        Assert.Equal(manifestHash, Hash(Path.Combine(fixture.Output, "run-manifest.json")));
    }

    [Fact]
    public async Task CLI_errors_are_categorical_and_do_not_disclose_input_paths()
    {
        using var fixture = new Fixture();
        File.WriteAllText(fixture.ConfigPath, "{ invalid private input }");
        using var output = new StringWriter();
        using var error = new StringWriter();
        Assert.Equal(1, await TraceMapCommand.RunAsync(["webforms-review", "preflight", "--config", fixture.ConfigPath, "--out", fixture.Output], output, error));
        Assert.DoesNotContain(fixture.Root, error.ToString(), StringComparison.Ordinal);
        Assert.False(Directory.Exists(fixture.Output));
        Assert.Equal(0, await TraceMapCommand.RunAsync(["webforms-review", "--help"], output, error));
        Assert.Contains("preflight only", output.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private static string Hash(string path) { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant(); }
    private static string HashText(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "tracemap-native-preflight-" + Guid.NewGuid().ToString("N"));
        public string Source => Path.Combine(Root, "source");
        public string Published => Path.Combine(Root, "published");
        public string Parent => Path.Combine(Root, "parent-scan");
        public string ConfigPath => Path.Combine(Root, "private-config.json");
        public string AssemblyPath => Path.Combine(Published, "bin", "Public.dll");
        public string Output => Path.Combine(Root, "run");
        public WebFormsReviewConfig Config { get; set; }
        public Fixture()
        {
            Directory.CreateDirectory(Path.Combine(Source, "Pages"));
            Directory.CreateDirectory(Path.Combine(Published, "bin"));
            File.WriteAllText(Path.Combine(Source, "Pages", "Lookup.aspx"), "<%@ Page Language=\"VB\" %>");
            File.Copy(typeof(WebFormsReviewPreflightCommand).Assembly.Location, AssemblyPath);
            Config = new(WebFormsReviewPreflightCommand.ConfigSchema, "fresh", Source, new string('a', 40),
                "projectless", null, [], ["Pages"], "selected", ["Pages/Lookup.aspx"], Published,
                ["bin/Public.dll"], [], [], [], [], null, new());
        }
        public void Save() => File.WriteAllText(ConfigPath, JsonSerializer.Serialize(Config, JsonOptions));
        public Task<WebFormsReviewPreflightManifest> Build(string? output = null, bool save = true)
        {
            if (save) Save();
            return WebFormsReviewPreflightCommand.BuildAsync(ConfigPath, output ?? Output,
                detectGit: _ => new GitMetadata("public", null, "dev", new string('a', 40), []));
        }
        public void AddParent()
        {
            Directory.CreateDirectory(Path.Combine(Parent, "logs"));
            foreach (var name in new[] { "facts.ndjson", "index.sqlite", "report.md", "logs/analyzer.log" }) File.WriteAllText(Path.Combine(Parent, name), "public retained artifact");
            File.WriteAllText(Path.Combine(Parent, "scan-manifest.json"), JsonSerializer.Serialize(new { scanId = "public-parent", commitSha = Config.SourceCommitSha }));
            Config = Config with { Operation = "attach", ParentScanRoot = Parent };
        }
        public void InitializeGit()
        {
            foreach (var args in new[] { new[] { "init" }, new[] { "config", "user.name", "Public Test" }, new[] { "config", "user.email", "public@example.test" }, new[] { "add", "." }, new[] { "commit", "-m", "public fixture" } })
            {
                using var process = new Process { StartInfo = new ProcessStartInfo("git") { WorkingDirectory = Source, RedirectStandardOutput = true, RedirectStandardError = true } };
                foreach (var arg in args) process.StartInfo.ArgumentList.Add(arg);
                process.Start();
                process.WaitForExit();
                Assert.Equal(0, process.ExitCode);
            }
            Config = Config with { SourceCommitSha = GitMetadataProvider.Detect(Source).CommitSha };
        }
        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
