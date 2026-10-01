using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using TraceMap.Cli;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class WebFormsProofImportTests
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };

    [Fact]
    public async Task Imports_exact_legacy_receipts_and_proves_bound_inputs_without_scanning_or_overwriting()
    {
        using var f = new Fixture();
        var originals = f.InputHashes();
        Assert.Equal(0, await f.Run());
        Assert.Empty(f.Error.ToString());
        Assert.DoesNotContain(f.Root, f.Output.ToString(), StringComparison.Ordinal);
        var config = JsonSerializer.Deserialize<WebFormsReviewConfig>(File.ReadAllBytes(f.ImportedConfig), Options)!;
        Assert.Equal(f.Commit, config.SourceCommitSha);
        Assert.Equal(f.Published, config.PublishedRoot);
        Assert.Equal(f.Proof, config.ReceiptRoot);
        Assert.Equal(new[] { "bin/CompiledEvidence.CSharp.dll" }, config.PrimaryAssemblies);
        Assert.Equal(new[] { "bin/CompiledEvidence.VisualBasic.dll" }, config.DependencyAssemblies);
        Assert.Equal("selected", config.PageMode);
        Assert.Equal(new[] { "Lookup.aspx" }, config.PageRelativePaths);
        Assert.Equal(f.Draft.SourceFolders, config.SourceFolders);
        Assert.Equal(f.Draft.Budgets, config.Budgets);
        Assert.Null(config.PublishSourceRelativePaths);
        using var receipt = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(f.Out, "proof-import.local.json")));
        var audit = receipt.RootElement;
        Assert.Equal("verified-inputs-not-scanned", audit.GetProperty("state").GetString());
        Assert.Equal(Hash(File.ReadAllBytes(typeof(WebFormsProofImportCommand).Assembly.Location)), audit.GetProperty("generatorSha256").GetString());
        Assert.Equal(config.PreparationProvenance!.BoundedInputSha256, audit.GetProperty("boundedInputSha256").GetString());
        Assert.Equal(Hash(File.ReadAllBytes(f.ImportedConfig)), audit.GetProperty("outputConfigSha256").GetString());
        Assert.Equal(Hash(File.ReadAllBytes(f.PublishReceipt)), audit.GetProperty("retainedPublishReceiptSha256").GetString());
        var imported = Directory.GetFiles(f.Out).ToDictionary(path => path, path => Hash(File.ReadAllBytes(path)));
        Assert.Equal(1, await f.Run()); Assert.Contains("OUTPUT_EXISTS", f.Error.ToString(), StringComparison.Ordinal);
        foreach (var pair in originals) Assert.Equal(pair.Value, Hash(File.ReadAllBytes(pair.Key)));
        foreach (var pair in imported) Assert.Equal(pair.Value, Hash(File.ReadAllBytes(pair.Key)));
        Assert.Equal(new[] { "proof-import.local.json", "review-config.local.json" }, Directory.GetFiles(f.Out).Select(Path.GetFileName).Order(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("source-bytes", "SOURCE_BYTES_MISMATCH")]
    [InlineData("published-bytes", "PUBLISHED_BYTES_MISMATCH")]
    [InlineData("commit", "SOURCE_COMMIT_OR_REPOSITORY_MISMATCH")]
    [InlineData("repository", "RECEIPT_REPOSITORY_DIGEST_MISMATCH")]
    [InlineData("duplicate", "DUPLICATE_PATH")]
    [InlineData("traversal", "RELATIVE_PATH_INVALID")]
    [InlineData("digest", "RECEIPT_DIGEST_MISMATCH")]
    [InlineData("binding", "BINDING_SOURCE_OR_DUPLICATE_MISMATCH")]
    [InlineData("binding-digest", "BINDING_DIGEST_MISMATCH")]
    [InlineData("dirty-extra", "SOURCE_DIRTY")]
    [InlineData("json-duplicate", "JSON_DUPLICATE_PROPERTY")]
    [InlineData("oversize", "FILE_BYTES_LIMIT")]
    public async Task Refuses_changed_or_ambiguous_inputs_without_publishing_settings(string scenario, string code)
    {
        using var f = new Fixture();
        switch (scenario)
        {
            case "source-bytes": File.AppendAllText(Path.Combine(f.Source, "Lookup.aspx"), "changed"); break;
            case "published-bytes": File.AppendAllText(f.Primary, "changed"); break;
            case "commit": f.Git("commit", "--allow-empty", "-qm", "new source commit"); break;
            case "repository": f.Git("remote", "set-url", "origin", "https://example.invalid/different.git"); break;
            case "dirty-extra": File.WriteAllText(Path.Combine(f.Source, "extra.txt"), "untracked"); break;
            case "duplicate": f.ChangePublish(root => root["assemblyInventory"]!.AsArray().Add(root["assemblyInventory"]![0]!.DeepClone())); break;
            case "traversal": f.ChangePublish(root => root["sourceFiles"]![0]!["path"] = "../escape.aspx"); break;
            case "digest": f.ChangePublish(root => root["boundedInputSha256"] = new string('a', 64)); break;
            case "binding": f.ChangeBinding(root => root["bindings"]![0]!["binarySourceCommitSha"] = new string('a', 40)); break;
            case "binding-digest": f.ChangeBinding(root => root["boundedInputSha256"] = new string('a', 64)); break;
            case "json-duplicate": File.WriteAllText(f.PublishReceipt, "{\"schemaVersion\":\"a\",\"schemaVersion\":\"b\"}"); break;
            case "oversize": File.WriteAllText(f.PublishReceipt, new string(' ', 1_048_577)); break;
        }
        Assert.Equal(1, await f.Run());
        Assert.Contains(code, f.Error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(f.Root, f.Error.ToString(), StringComparison.Ordinal);
        Assert.False(Directory.Exists(f.Out));
    }

    [Fact]
    public async Task A_backend_draft_cannot_silently_inherit_the_websites_source_identity()
    {
        using var f = new Fixture();
        var backend = Path.Combine(f.Root, "backend"); Directory.CreateDirectory(backend);
        File.WriteAllText(f.ConfigPath, JsonSerializer.Serialize(f.Draft with { SourceRoot = backend }, Options));
        Assert.Equal(1, await f.Run()); Assert.False(Directory.Exists(f.Out));
        Assert.Contains("SOURCE_COMMIT_OR_REPOSITORY_MISMATCH", f.Error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Nested_website_requires_explicit_base_and_preserves_repo_scope_and_receipt_bytes()
    {
        using var f = new Fixture(nested: true);
        var originals = f.InputHashes();
        Assert.Equal(1, await f.Run());
        Assert.Contains("stage=source-roster", f.Error.ToString(), StringComparison.Ordinal);
        Assert.Equal(0, await f.Run("UBid"));
        var config = JsonSerializer.Deserialize<WebFormsReviewConfig>(File.ReadAllBytes(f.ImportedConfig), Options)!;
        Assert.Equal(f.Source, config.SourceRoot);
        Assert.Equal(f.Draft.SourceFolders, config.SourceFolders);
        Assert.Equal("UBid", config.PublishSourceRelativeBase);
        Assert.Equal(new[] { "UBid/Lookup.aspx" }, config.PageRelativePaths);
        var plan = await WebFormsReviewPreflightCommand.BuildAsync(f.ImportedConfig, Path.Combine(f.Root, "attachment-option-check"));
        Assert.Equal("UBid", WebFormsReviewAttachmentExecution.Options(plan, "unused").WebFormsPublishSourceRelativeBase);
        foreach (var pair in originals) Assert.Equal(pair.Value, Hash(File.ReadAllBytes(pair.Key)));
        using var receipt = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(f.Out, "proof-import.local.json")));
        Assert.Equal("UBid", receipt.RootElement.GetProperty("sourceRelativeBase").GetString());
        Assert.Equal(Hash(File.ReadAllBytes(f.PublishReceipt)), receipt.RootElement.GetProperty("retainedPublishReceiptSha256").GetString());
    }

    [Fact]
    public async Task Exact_legacy_proof_copy_locator_is_projected_without_reissuing_owner_attestation()
    {
        using var f = new Fixture(nested: true);
        var copied = Path.Combine(f.Proof, "bin", f.PrimaryName);
        Directory.CreateDirectory(Path.GetDirectoryName(copied)!);
        File.Copy(f.Primary, copied);
        var original = JsonNode.Parse(File.ReadAllText(f.BindingReceipt))!.AsObject();
        var originalBinding = original["bindings"]![0]!.DeepClone();
        var oldLocator = FileInventory.NormalizeRelativePath(Path.GetRelativePath(Path.Combine(f.Source, "UBid"), copied));
        f.ChangeBinding(root =>
        {
            root["bindings"]![0]!["safeLocator"] = oldLocator;
            using var publish = JsonDocument.Parse(File.ReadAllBytes(f.PublishReceipt));
            var receipt = publish.RootElement;
            var item = root["bindings"]![0]!;
            root["boundedInputSha256"] = HashText($"{item["safeLocator"]}:{item["artifactSha256"]}:{item["assemblyIdentity"]}:{item["binarySourceCommitSha"]}\n"
                + $"source:{receipt.GetProperty("boundedInputSha256").GetString()}\n"
                + $"source-repository:{HashText("https://example.invalid/public-proof.git")}\n"
                + $"assembly-inventory:{receipt.GetProperty("assemblyInventorySha256").GetString()}\n"
                + $"map-inventory:{receipt.GetProperty("mapInventorySha256").GetString()}\n");
        });
        var originals = f.InputHashes();
        Assert.Equal(0, await f.Run("UBid"));
        var config = JsonSerializer.Deserialize<WebFormsReviewConfig>(File.ReadAllBytes(f.ImportedConfig), Options)!;
        Assert.Equal(f.Out, config.ReceiptRoot);
        var projectedPath = Path.Combine(f.Out, "compiled-binding.local.json");
        using var projected = JsonDocument.Parse(File.ReadAllBytes(projectedPath));
        var newBinding = projected.RootElement.GetProperty("bindings")[0];
        Assert.StartsWith("__external__/primary/", newBinding.GetProperty("safeLocator").GetString(), StringComparison.Ordinal);
        foreach (var field in new[] { "artifactSha256", "assemblyIdentity", "binarySourceRepository", "binarySourceCommitSha", "binaryBuildIdentity" })
            Assert.Equal(originalBinding[field]!.GetValue<string>(), newBinding.GetProperty(field).GetString());
        Assert.Equal(Hash(File.ReadAllBytes(f.BindingReceipt)), projected.RootElement.GetProperty("locatorProjection").GetProperty("retainedBindingReceiptSha256").GetString());
        Assert.Equal(Hash(File.ReadAllBytes(f.PublishReceipt)), Hash(File.ReadAllBytes(Path.Combine(f.Out, "publish-receipt.local.json"))));
        Assert.Equal(1, projected.RootElement.GetProperty("locatorProjection").GetProperty("projections").GetArrayLength());
        using var audit = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(f.Out, "proof-import.local.json")));
        Assert.Equal(1, audit.RootElement.GetProperty("locatorProjectionCount").GetInt32());
        Assert.Equal(Hash(File.ReadAllBytes(projectedPath)), audit.RootElement.GetProperty("projectedBindingReceiptSha256").GetString());
        var finalPlan = await WebFormsReviewPreflightCommand.BuildAsync(f.ImportedConfig, Path.Combine(f.Root, "next-review"));
        var finalValidation = await WebFormsReviewInputValidation.ValidateAsync(finalPlan);
        Assert.All(finalValidation.CompiledProvenance!.Outcomes.Where(item => item.Role == "primary"),
            item => Assert.Equal("bound", item.ProvenanceState));
        foreach (var pair in originals) Assert.Equal(pair.Value, Hash(File.ReadAllBytes(pair.Key)));
        Assert.Contains("newAttestation=false", f.Output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(f.Root, f.Output.ToString(), StringComparison.Ordinal);
        File.AppendAllText(copied, "changed-proof-copy");
        f.Out = Path.Combine(f.Root, "tampered-output");
        Assert.Equal(1, await f.Run("UBid"));
        Assert.Contains("PUBLISHED_BYTES_MISMATCH", f.Error.ToString(), StringComparison.Ordinal);
        Assert.False(Directory.Exists(f.Out));
    }

    [Fact]
    public void External_locator_classification_is_portable_for_windows_parent_segments()
    {
        Assert.False(ManagedMetadataExtractor.IsRepositoryRelativeLocator("..\\private\\App_Code.dll"));
        Assert.False(ManagedMetadataExtractor.IsRepositoryRelativeLocator("../private/App_Code.dll"));
        Assert.False(ManagedMetadataExtractor.IsRepositoryRelativeLocator(".."));
        Assert.True(ManagedMetadataExtractor.IsRepositoryRelativeLocator("UBid\\bin\\App_Code.dll"));
    }

    [Theory]
    [InlineData("../UBid", "RELATIVE_PATH_INVALID")]
    [InlineData("/UBid", "RELATIVE_PATH_INVALID")]
    [InlineData("UBid/../UBid", "RELATIVE_PATH_INVALID")]
    [InlineData("missing", "SOURCE_BASE_INVALID")]
    [InlineData("UBid\\child", "SOURCE_BASE_INVALID")]
    public async Task Invalid_source_bases_are_not_discovered_or_silently_replaced(string sourceBase, string code)
    {
        using var f = new Fixture(nested: true);
        Assert.Equal(1, await f.Run(sourceBase));
        Assert.Contains(code, f.Error.ToString(), StringComparison.Ordinal);
        Assert.False(Directory.Exists(f.Out));
    }

    [Fact]
    public async Task Explicit_source_base_does_not_bypass_changed_source_or_receipt_traversal()
    {
        using var f = new Fixture(nested: true);
        File.AppendAllText(f.PagePath, "changed");
        Assert.Equal(1, await f.Run("UBid"));
        Assert.Contains("SOURCE_BYTES_MISMATCH", f.Error.ToString(), StringComparison.Ordinal);
        f.ChangePublish(root => root["sourceFiles"]![0]!["path"] = "../Lookup.aspx");
        Assert.Equal(1, await f.Run("UBid"));
        Assert.Contains("RELATIVE_PATH_INVALID", f.Error.ToString(), StringComparison.Ordinal);
        Assert.False(Directory.Exists(f.Out));
    }

    [Fact]
    public async Task Website_base_cannot_select_an_independent_nested_repository()
    {
        using var f = new Fixture(nested: true);
        using var git = new Process { StartInfo = new("git") { WorkingDirectory = Path.GetDirectoryName(f.PagePath)!, UseShellExecute = false } };
        git.StartInfo.ArgumentList.Add("init"); git.StartInfo.ArgumentList.Add("-q");
        git.Start(); await git.WaitForExitAsync(); Assert.Equal(0, git.ExitCode);
        Assert.Equal(1, await f.Run("UBid"));
        Assert.Contains("SOURCE_BASE_REPOSITORY_MISMATCH", f.Error.ToString(), StringComparison.Ordinal);
        Assert.False(Directory.Exists(f.Out));
    }

    [Theory]
    [InlineData("draft", "FILE_OR_DIRECTORY_UNAVAILABLE", "migration-draft")]
    [InlineData("publish-receipt", "FILE_OR_DIRECTORY_UNAVAILABLE", "publish-receipt")]
    [InlineData("binding-receipt", "FILE_OR_DIRECTORY_UNAVAILABLE", "binding-receipt")]
    [InlineData("source", "FILE_OR_DIRECTORY_UNAVAILABLE", "source-roster")]
    [InlineData("dll", "FILE_OR_DIRECTORY_UNAVAILABLE", "assembly-inventory")]
    [InlineData("field", "RECEIPT_FIELD_UNAVAILABLE;field=receiptGeneratorSha256", "receipt-schema")]
    [InlineData("roster", "RECEIPT_FIELD_UNAVAILABLE;field=assemblyInventory", "receipt-rosters")]
    [InlineData("json", "JSON_INVALID", "publish-receipt")]
    [InlineData("dirty", "SOURCE_DIRTY", "source-membership")]
    public async Task Direct_import_reports_safe_stage_and_reason_without_private_paths(string scenario, string code, string stage)
    {
        using var f = new Fixture();
        switch (scenario)
        {
            case "draft": File.Delete(f.ConfigPath); break;
            case "publish-receipt": File.Delete(f.PublishReceipt); break;
            case "binding-receipt": File.Delete(f.BindingReceipt); break;
            case "source": f.ChangePublish(root => root["sourceFiles"]![0]!["path"] = "private-missing-source.aspx"); break;
            case "dll": File.Delete(f.Primary); break;
            case "field": f.ChangePublish(root => root.Remove("receiptGeneratorSha256")); break;
            case "roster": f.ChangePublish(root => root.Remove("assemblyInventory")); break;
            case "json": File.WriteAllText(f.PublishReceipt, "{\"private-secret\": invalid}"); break;
            case "dirty": File.WriteAllText(Path.Combine(f.Source, "private-untracked.txt"), "private-secret"); break;
        }
        Assert.Equal(1, await WebFormsProofImportCommand.RunAsync(
            ["import-proof", "--config", f.ConfigPath, "--proof-root", f.Proof, "--published-root", f.Published, "--out", f.Out], f.Output, f.Error));
        var diagnostic = f.Error.ToString();
        Assert.Contains(code, diagnostic, StringComparison.Ordinal);
        Assert.Contains($";stage={stage};no-scan-started", diagnostic, StringComparison.Ordinal);
        Assert.DoesNotContain(f.Root, diagnostic, StringComparison.Ordinal);
        Assert.DoesNotContain("private-", diagnostic, StringComparison.Ordinal);
        Assert.False(Directory.Exists(f.Out));
        Assert.Empty(Directory.GetFiles(f.Root, "proof-import.local.json", SearchOption.AllDirectories));
    }

    [Theory]
    [InlineData("safeLocator", false)]
    [InlineData("assemblyIdentity", false)]
    [InlineData("safeLocator", true)]
    public async Task Matching_hashes_and_valid_receipt_digests_do_not_bypass_binding_policy(string field, bool traversal)
    {
        using var f = new Fixture();
        f.ChangeBinding(root =>
        {
            root["bindings"]![0]![field] = traversal ? "../private-copy/artifact.dll" : "not-the-recorded-identity";
            using var publish = JsonDocument.Parse(File.ReadAllBytes(f.PublishReceipt));
            var receipt = publish.RootElement;
            var item = root["bindings"]![0]!;
            var lines = $"{item["safeLocator"]}:{item["artifactSha256"]}:{item["assemblyIdentity"]}:{item["binarySourceCommitSha"]}\n" +
                $"source:{receipt.GetProperty("boundedInputSha256").GetString()}\n" +
                $"source-repository:{HashText("https://example.invalid/public-proof.git")}\n" +
                $"assembly-inventory:{receipt.GetProperty("assemblyInventorySha256").GetString()}\n" +
                $"map-inventory:{receipt.GetProperty("mapInventorySha256").GetString()}\n";
            root["boundedInputSha256"] = HashText(lines);
        });
        Assert.Equal(1, await f.Run());
        Assert.Contains("RETAINED_BINDING_NOT_ADMITTED", f.Error.ToString(), StringComparison.Ordinal);
        Assert.False(Directory.Exists(f.Out));
        Assert.Empty(Directory.GetFiles(f.Root, "proof-import.local.json", SearchOption.AllDirectories));
        Directory.CreateDirectory(f.Out);
        File.WriteAllText(Path.Combine(f.Out, "preserve.txt"), "private-preserved-marker");
        var before = Directory.GetFiles(f.Root, "*", SearchOption.AllDirectories).ToDictionary(path => path, path => Hash(File.ReadAllBytes(path)));
        Assert.Equal(1, await f.Run(diagnoseOnly: true));
        Assert.Contains(field == "safeLocator" ? "locatorMatches=0" : "locatorMatches=1", f.Output.ToString(), StringComparison.Ordinal);
        Assert.Contains(field == "safeLocator" ? "bindingDiagnostic.gap=UnboundManagedInput" : "bindingDiagnostic.gap=ManagedInputProvenanceMismatch", f.Output.ToString(), StringComparison.Ordinal);
        Assert.Contains("no-configuration-written;no-scan-started", f.Output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(f.Root, f.Output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("not-the-recorded-identity", f.Output.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("private-copy", f.Output.ToString(), StringComparison.Ordinal);
        Assert.Contains(traversal ? "retainedTraversalLocators=1" : "retainedTraversalLocators=0", f.Output.ToString(), StringComparison.Ordinal);
        var after = Directory.GetFiles(f.Root, "*", SearchOption.AllDirectories).ToDictionary(path => path, path => Hash(File.ReadAllBytes(path)));
        Assert.Equal(before.OrderBy(pair => pair.Key), after.OrderBy(pair => pair.Key));
    }

    [Theory]
    [InlineData("relative-source")]
    [InlineData("selected-inventory")]
    public async Task Existing_owner_selections_are_not_silently_overwritten(string scenario)
    {
        using var f = new Fixture();
        var draft = scenario == "relative-source" ? f.Draft with { SourceRoot = "." }
            : f.Draft with { PrimaryAssemblies = ["bin/OwnerChosen.dll"] };
        File.WriteAllText(f.ConfigPath, JsonSerializer.Serialize(draft, Options));
        Assert.Equal(1, await f.Run()); Assert.Contains("FRESH_DRAFT_REQUIRED", f.Error.ToString(), StringComparison.Ordinal);
        Assert.False(Directory.Exists(f.Out));
    }

    [Fact]
    public async Task Output_inside_any_input_is_refused_before_staging()
    {
        using var f = new Fixture();
        foreach (var root in new[] { f.Source, f.Published, f.Proof, Path.GetDirectoryName(f.ConfigPath)! })
        {
            f.Out = Path.Combine(root, "never-write");
            Assert.Equal(1, await f.Run()); Assert.Contains("OUTPUT_OVERLAPS_INPUT", f.Error.ToString(), StringComparison.Ordinal);
            Assert.False(Directory.Exists(f.Out));
        }
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, true)]
    public async Task Short_helper_uses_real_native_import_and_scans_only_when_requested(bool run, bool nested, bool diagnose)
    {
        using var f = new Fixture(nested);
        var pwsh = OperatingSystem.IsWindows() ? "pwsh" : "/opt/homebrew/bin/pwsh";
        if (!File.Exists(pwsh) && !OperatingSystem.IsWindows()) return; // Platform-specific helper smoke; direct native cases always run.
        var reviewRoot = Path.Combine(f.Root, "review-root");
        Directory.CreateDirectory(Path.Combine(reviewRoot, "native-config"));
        File.Copy(f.ConfigPath, Path.Combine(reviewRoot, "native-config/review.draft.json"));
        var originals = f.InputHashes();
        if (diagnose)
        {
            Directory.CreateDirectory(f.Out);
            File.WriteAllText(Path.Combine(f.Out, "preserve.txt"), "private-preserved-marker");
        }
        var repository = Fixture.FindRepo();
        using var process = new Process { StartInfo = new(pwsh) { WorkingDirectory = repository, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false } };
        foreach (var argument in new[] { "-NoProfile", "-File", Path.Combine(repository, "scripts/wverify.ps1"), "-ReviewRoot", reviewRoot,
                     "-ProofRoot", f.Proof, "-PublishedRoot", f.Published, "-SourceBase", nested ? "UBid" : ".", "-OutputRoot", f.Out, "-NoBuild" }) process.StartInfo.ArgumentList.Add(argument);
        if (run) process.StartInfo.ArgumentList.Add("-Run");
        if (diagnose) process.StartInfo.ArgumentList.Add("-Diagnose");
        process.Start(); var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch (OperationCanceledException) { process.Kill(entireProcessTree: true); throw; }
        var standardOutput = await stdout; var standardError = await stderr;
        Assert.True(process.ExitCode == 0, standardOutput + standardError);
        if (diagnose)
        {
            Assert.Contains("bindingDiagnostic=counts-only", standardOutput, StringComparison.Ordinal);
            Assert.Contains("locatorMatches=1", standardOutput, StringComparison.Ordinal);
            Assert.Contains("no-configuration-written;no-scan-started", standardOutput, StringComparison.Ordinal);
            Assert.DoesNotContain(f.Root, standardOutput, StringComparison.Ordinal);
            Assert.DoesNotContain("private-preserved-marker", standardOutput, StringComparison.Ordinal);
            Assert.Equal(new[] { "preserve.txt" }, Directory.GetFiles(f.Out, "*", SearchOption.AllDirectories).Select(Path.GetFileName));
            foreach (var pair in originals) Assert.Equal(pair.Value, Hash(File.ReadAllBytes(pair.Key)));
            return;
        }
        Assert.Contains(run ? "completion is not a parity verdict" : "Verified inputs only; no scan or reports ran", standardOutput, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(f.Out, "configuration/review-config.local.json")));
        var config = JsonSerializer.Deserialize<WebFormsReviewConfig>(File.ReadAllBytes(Path.Combine(f.Out, "configuration/review-config.local.json")), Options)!;
        Assert.Equal(f.Source, config.SourceRoot);
        Assert.Equal(new[] { "." }, config.SourceFolders);
        Assert.Equal(nested ? "UBid" : null, config.PublishSourceRelativeBase);
        Assert.Equal(new[] { nested ? "UBid/Lookup.aspx" : "Lookup.aspx" }, config.PageRelativePaths);
        Assert.Equal(run, Directory.Exists(Path.Combine(f.Out, "review")));
        if (run)
        {
            Assert.True(File.Exists(Path.Combine(f.Out, "review/run/run-manifest.json")));
            if (nested)
            {
                var manifestPath = Assert.Single(Directory.GetFiles(Path.Combine(f.Out, "review"), "scan-manifest.json", SearchOption.AllDirectories));
                using var manifest = JsonDocument.Parse(File.ReadAllBytes(manifestPath));
                Assert.Equal("bound", manifest.RootElement.GetProperty("webFormsPublishProvenance").GetProperty("status").GetString());
                Assert.Equal("UBid", manifest.RootElement.GetProperty("webFormsPublishProvenance").GetProperty("sourceRelativeBase").GetString());
                var facts = File.ReadAllText(Path.Combine(Path.GetDirectoryName(manifestPath)!, "facts.ndjson"));
                Assert.Contains("UBid/Lookup.aspx", facts, StringComparison.Ordinal);
            }
        }
        foreach (var pair in originals) Assert.Equal(pair.Value, Hash(File.ReadAllBytes(pair.Key)));
    }

    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = WebFormsReviewPreflightCommand.PhysicalPath(Path.Combine(Path.GetTempPath(), "tracemap proof import public-" + Guid.NewGuid().ToString("N")));
        public string Source => Path.Combine(Root, "source");
        public string Published => Path.Combine(Root, "published");
        public string Proof => Path.Combine(Root, "proof");
        public string ConfigPath => Path.Combine(Root, "draft", "review.draft.json");
        public string PublishReceipt => Path.Combine(Proof, "publish-receipt.local.json");
        public string BindingReceipt => Path.Combine(Proof, "compiled-binding.local.json");
        public string PrimaryName { get; }
        public string Primary => Path.Combine(Published, "bin", PrimaryName);
        public string PagePath { get; }
        public string ImportedConfig => Path.Combine(Out, "review-config.local.json");
        public string Out { get; set; }
        public string Commit { get; }
        public WebFormsReviewConfig Draft { get; }
        public StringWriter Output { get; } = new(); public StringWriter Error { get; } = new();
        public Fixture(bool nested = false)
        {
            PrimaryName = nested ? "App_Web_Public.dll" : "CompiledEvidence.CSharp.dll";
            PagePath = Path.Combine(Source, nested ? "UBid/Lookup.aspx" : "Lookup.aspx");
            Out = Path.Combine(Root, "imported");
            Directory.CreateDirectory(Source); Directory.CreateDirectory(Path.Combine(Published, "bin"));
            Directory.CreateDirectory(Proof); Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
            Directory.CreateDirectory(Path.GetDirectoryName(PagePath)!);
            File.WriteAllText(PagePath, "<%@ Page Language=\"VB\" Inherits=\"Public.Page\" %>");
            var repository = FindRepo();
            File.Copy(Path.Combine(repository, "samples/compiled-dotnet-evidence/csharp/bin/Debug/net10.0/CompiledEvidence.CSharp.dll"), Primary);
            var dependency = Path.Combine(Published, "bin/CompiledEvidence.VisualBasic.dll");
            File.Copy(Path.Combine(repository, "samples/compiled-dotnet-evidence/vb/bin/Debug/net10.0/CompiledEvidence.VisualBasic.dll"), dependency);
            Git("init", "-q"); Git("config", "user.name", "Public fixture"); Git("config", "user.email", "public@example.invalid");
            Git("config", "core.autocrlf", "false"); Git("remote", "add", "origin", "https://example.invalid/public-proof.git");
            Git("add", "."); Git("commit", "-qm", "public source"); Commit = GitMetadataProvider.Detect(Source).CommitSha;
            Draft = new(WebFormsReviewPreflightCommand.ConfigSchema, "fresh", Source, "", "projectless", null, [], ["."], "all", [], "", [], [], [], [], [], null, new());
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(Draft, Options));
            var sourceHash = Hash(File.ReadAllBytes(PagePath));
            var primaryHash = Hash(File.ReadAllBytes(Primary)); var dependencyHash = Hash(File.ReadAllBytes(dependency));
            var sourceDigest = HashText($"Lookup.aspx:{sourceHash}\n");
            var inventoryDigest = HashText($"bin/{PrimaryName}:{primaryHash}:selected\nbin/CompiledEvidence.VisualBasic.dll:{dependencyHash}:artifact-context-no-source-commit\n");
            var mapDigest = HashText("\n"); var repositoryDigest = HashText("https://example.invalid/public-proof.git");
            File.WriteAllText(PublishReceipt, JsonSerializer.Serialize(new {
                schemaVersion = "webforms-publish-binding.v1", visibility = "local-only", receiptGeneratorSha256 = new string('b', 64),
                compilerSha256 = new string('c', 64), compilerProvenance = "unavailable-existing-output", sourceCommitSha = Commit,
                boundedInputSha256 = sourceDigest, assemblyInventorySha256 = inventoryDigest, mapInventorySha256 = mapDigest,
                receiptInputSha256 = HashText($"source:{repositoryDigest}\ncommit:{Commit}\nsource:{sourceDigest}\nassemblies:{inventoryDigest}\nmaps:{mapDigest}\n"),
                sourceFiles = new[] { new { path = "Lookup.aspx", sha256 = sourceHash } },
                assemblyInventory = new[] { new { path = "bin/" + PrimaryName, sha256 = primaryHash, disposition = "selected" },
                    new { path = "bin/CompiledEvidence.VisualBasic.dll", sha256 = dependencyHash, disposition = "artifact-context-no-source-commit" } },
                publishedFiles = new[] { new { path = "bin/" + PrimaryName, sha256 = primaryHash, kind = "assembly" },
                    new { path = "bin/CompiledEvidence.VisualBasic.dll", sha256 = dependencyHash, kind = "assembly" } },
                publishedMapCount = 0,
                pages = new[] { new { sourcePath = "Lookup.aspx", virtualPath = "/Lookup.aspx", bindingKind = "mapless-source-type-candidate" } }
            }, Options));
            var inspected = ManagedMetadataExtractor.InspectInputs(new ScanOptions(Source, "unused", CompiledInputPaths: [Primary]), Commit);
            var item = inspected.Provenance!.Outcomes.Single();
            File.WriteAllText(BindingReceipt, JsonSerializer.Serialize(new { schemaVersion = "compiled-input-binding-set.v1", generatorSha256 = new string('b', 64),
                boundedInputSha256 = HashText($"{item.SafeLocator}:{primaryHash}:{item.AssemblyIdentity}:{Commit}\nsource:{sourceDigest}\nsource-repository:{repositoryDigest}\nassembly-inventory:{inventoryDigest}\nmap-inventory:{mapDigest}\n"),
                bindings = new[] { new { schemaVersion = "compiled-input-binding.v1", safeLocator = item.SafeLocator, artifactSha256 = primaryHash,
                    assemblyIdentity = item.AssemblyIdentity, binarySourceRepository = "https://example.invalid/public-proof.git", binarySourceCommitSha = Commit,
                    binaryBuildIdentity = "operator-attested-existing-publish:" + sourceDigest } } }, Options));
        }
        public async Task<int> Run(string? sourceBase = null, bool diagnoseOnly = false) { Output.GetStringBuilder().Clear(); Error.GetStringBuilder().Clear();
            var args = new List<string> { "webforms-review", "import-proof", "--config", ConfigPath, "--proof-root", Proof, "--published-root", Published, "--out", Out };
            if (sourceBase is not null) args.AddRange(["--source-base", sourceBase]);
            if (diagnoseOnly) args.Add("--diagnose");
            return await TraceMapCommand.RunAsync(args.ToArray(), Output, Error); }
        public Dictionary<string, string> InputHashes() => new[] { ConfigPath, PublishReceipt, BindingReceipt, Primary, PagePath }.ToDictionary(path => path, path => Hash(File.ReadAllBytes(path)));
        public void ChangePublish(Action<JsonObject> edit) => Change(PublishReceipt, edit);
        public void ChangeBinding(Action<JsonObject> edit) => Change(BindingReceipt, edit);
        private static void Change(string path, Action<JsonObject> edit) { var root = JsonNode.Parse(File.ReadAllText(path))!.AsObject(); edit(root); File.WriteAllText(path, root.ToJsonString(Options)); }
        public void Git(params string[] arguments)
        {
            using var process = new Process { StartInfo = new("git") { WorkingDirectory = Source, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false } };
            process.StartInfo.Environment["GIT_OPTIONAL_LOCKS"] = "0";
            foreach (var arg in arguments) process.StartInfo.ArgumentList.Add(arg);
            process.Start(); var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
            Assert.True(process.WaitForExit(10_000)); Task.WaitAll(stdout, stderr); Assert.Equal(0, process.ExitCode);
        }
        public static string FindRepo() { for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))) return directory.FullName; throw new InvalidOperationException("Public fixture unavailable"); }
        public void Dispose() { Output.Dispose(); Error.Dispose(); if (Directory.Exists(Root)) Directory.Delete(Root, true); }
    }
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static string HashText(string text) => Hash(Encoding.UTF8.GetBytes(text));
}
