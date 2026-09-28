using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using TraceMap.Cli;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class WebFormsReviewPreparationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };

    [Fact]
    public async Task Preparation_reuses_core_policy_binds_only_attested_primary_and_runs_from_external_receipts()
    {
        using var fixture = new Fixture();
        var sourceBefore = fixture.Hashes(fixture.Source);
        var publishedBefore = fixture.Hashes(fixture.Published);
        Assert.Equal(0, await fixture.Prepare());
        Assert.Equal(string.Empty, fixture.Error.ToString());
        Assert.Equal(new[] { "compiled-binding.local.json", "preparation-manifest.local.json", "publish-receipt.local.json", "review-config.local.json" },
            Directory.GetFiles(fixture.Evidence).Select(Path.GetFileName).Order(StringComparer.Ordinal));
        foreach (var pair in sourceBefore) Assert.Equal(pair.Value, Hash(pair.Key));
        foreach (var pair in publishedBefore) Assert.Equal(pair.Value, Hash(pair.Key));
        Assert.Equal(sourceBefore.Count, fixture.Hashes(fixture.Source).Count);
        Assert.Equal(publishedBefore.Count, fixture.Hashes(fixture.Published).Count);
        var manifest = fixture.Manifest();
        Assert.Equal(WebFormsReviewPreparationCommand.RuleId, manifest.RuleId);
        Assert.Equal(EvidenceTiers.Tier2Structural, manifest.EvidenceTier);
        Assert.Equal("local-only", manifest.Visibility);
        Assert.Equal("operator-attested-review-only-not-build-proof", manifest.ClaimLevel);
        Assert.Equal(Hash(typeof(WebFormsReviewPreparationCommand).Assembly.Location), manifest.GeneratorSha256);
        Assert.Equal("bound", manifest.PublishInspection.Status);
        Assert.Equal(2, manifest.PublishInspection.SourceFileCount);
        Assert.Single(manifest.CompiledInspection.Provenance!.Outcomes, item => item.Role == "primary" && item.ProvenanceState == "bound");
        Assert.Single(manifest.CompiledInspection.Provenance.Outcomes, item => item.Role == "dependency" && item.ProvenanceState == "unbound");
        Assert.Contains("CompilerProvenanceUnavailable", manifest.Gaps);
        Assert.Contains("BuildAuthenticityNotEstablished", manifest.Gaps);
        Assert.Contains("PdbInputsNotDeclared", manifest.Gaps);
        Assert.Contains("SourceSnapshotValidationDeferred", manifest.Gaps);
        Assert.Contains("UnboundManagedInput", manifest.Gaps);
        foreach (var item in manifest.Artifacts) Assert.Equal(item.Sha256, Hash(Path.Combine(fixture.Evidence, item.RelativePath)));
        using var binding = JsonDocument.Parse(File.ReadAllText(Path.Combine(fixture.Evidence, "compiled-binding.local.json")));
        Assert.Equal(manifest.BoundedInputSha256, binding.RootElement.GetProperty("boundedInputSha256").GetString());
        Assert.Single(binding.RootElement.GetProperty("bindings").EnumerateArray());
        using var publish = JsonDocument.Parse(File.ReadAllText(Path.Combine(fixture.Evidence, "publish-receipt.local.json")));
        Assert.Equal("unavailable-existing-output", publish.RootElement.GetProperty("compilerProvenance").GetString());
        Assert.Equal(manifest.BoundedInputSha256, publish.RootElement.GetProperty("receiptInputSha256").GetString());
        var preparedConfig = JsonSerializer.Deserialize<WebFormsReviewConfig>(File.ReadAllText(fixture.PreparedConfig), JsonOptions)!;
        Assert.Equal(fixture.Evidence, preparedConfig.ReceiptRoot);
        Assert.Null(preparedConfig.PublishSourceRelativePaths);
        Assert.Equal(manifest.GeneratorSha256, preparedConfig.PreparationProvenance!.GeneratorSha256);
        Assert.Equal(manifest.BoundedInputSha256, preparedConfig.PreparationProvenance.BoundedInputSha256);
        var run = Path.Combine(fixture.Root, "run");
        Assert.Equal(0, await TraceMapCommand.RunAsync(["webforms-review", "preflight", "--config", fixture.PreparedConfig, "--out", run], fixture.Output, fixture.Error));
        Assert.Equal(0, await TraceMapCommand.RunAsync(["webforms-review", "run", "--run", run], fixture.Output, fixture.Error));
        var checkpoint = JsonSerializer.Deserialize<WebFormsReviewCheckpoint>(File.ReadAllText(Path.Combine(run, "checkpoints", "0002.json")), JsonOptions)!;
        Assert.Equal("scan-completed-reports-pending", checkpoint.State);
        Assert.DoesNotContain("PublishMapExecutionPending", checkpoint.Gaps);
        var evidenceBefore = fixture.Hashes(fixture.Evidence);
        Assert.Equal(1, await fixture.Prepare()); // Never overwrite an existing prepared evidence root.
        foreach (var pair in evidenceBefore) Assert.Equal(pair.Value, Hash(pair.Key));
    }

    [Theory]
    [InlineData("missing-attestation", "EXPLICIT_ATTESTATION_AND_ARGUMENTS_REQUIRED")]
    [InlineData("wrong-attestation", "ATTESTATION_COMMIT_MISMATCH")]
    [InlineData("no-sources", "EXPLICIT_SOURCE_MEMBERSHIP_REQUIRED")]
    [InlineData("dirty", "SOURCE_COMMIT_BYTES_MISMATCH")]
    [InlineData("assume-unchanged", "SOURCE_COMMIT_BYTES_MISMATCH")]
    [InlineData("untracked", "SOURCE_DIRTY")]
    [InlineData("ignored-source", "SOURCE_MEMBERSHIP_NOT_COMMITTED")]
    [InlineData("no-origin", "SOURCE_REPOSITORY_OR_COMMIT_UNAVAILABLE")]
    [InlineData("duplicate-map", "PAGE_MAP_NOT_UNIQUE")]
    [InlineData("map-dtd", "INPUT_OR_OUTPUT_INVALID")]
    [InlineData("map-unsafe", "MAP_INVALID")]
    [InlineData("mapped-assembly", "MAPPED_ASSEMBLY_UNAVAILABLE")]
    [InlineData("duplicate-bytes", "METADATA_INPUT_NOT_UNIQUELY_ADMITTED")]
    [InlineData("metadata-work", "METADATA_INPUT_NOT_UNIQUELY_ADMITTED")]
    [InlineData("has-receipts", "CONFIG_ALREADY_HAS_RECEIPTS")]
    public async Task Failed_preparation_does_not_publish_or_leak_private_paths(string mutation, string suffix)
    {
        using var fixture = new Fixture();
        var attestation = fixture.Config.SourceCommitSha;
        switch (mutation)
        {
            case "wrong-attestation": attestation = new string('a', 40); break;
            case "no-sources": fixture.Config = fixture.Config with { PublishSourceRelativePaths = null }; break;
            case "dirty": File.AppendAllText(Path.Combine(fixture.Source, "Pages", "Lookup.aspx.vb"), "' changed\n"); break;
            case "assume-unchanged":
                fixture.Git("update-index", "--assume-unchanged", "Pages/Lookup.aspx.vb");
                File.AppendAllText(Path.Combine(fixture.Source, "Pages", "Lookup.aspx.vb"), "' hidden change\n"); break;
            case "untracked": File.WriteAllText(Path.Combine(fixture.Source, "extra.txt"), "public untracked"); break;
            case "ignored-source":
                File.WriteAllText(Path.Combine(fixture.Source, ".gitignore"), "*.tmp\n");
                fixture.Git("add", "."); fixture.Git("commit", "-qm", "public ignore rule");
                File.WriteAllText(Path.Combine(fixture.Source, "ignored.tmp"), "public ignored");
                fixture.Config = fixture.Config with { SourceCommitSha = GitMetadataProvider.Detect(fixture.Source).CommitSha,
                    PublishSourceRelativePaths = ["Pages/Lookup.aspx.vb", "ignored.tmp"] };
                attestation = fixture.Config.SourceCommitSha;
                break;
            case "no-origin": fixture.Git("remote", "remove", "origin"); break;
            case "duplicate-map":
                File.Copy(fixture.Map, Path.Combine(fixture.Published, "duplicate.compiled"));
                fixture.Config = fixture.Config with { PageMaps = [.. fixture.Config.PageMaps, "duplicate.compiled"] }; break;
            case "map-dtd": File.WriteAllText(fixture.Map, "<!DOCTYPE preserve [<!ENTITY x SYSTEM 'file:///unavailable'>]><preserve virtualPath='/Pages/Lookup.aspx'>&x;</preserve>"); break;
            case "map-unsafe": File.WriteAllText(fixture.Map, "<preserve virtualPath='/../Pages/Lookup.aspx'/>"); break;
            case "mapped-assembly": File.WriteAllText(fixture.Map, "<preserve virtualPath='/Pages/Lookup.aspx' assembly='Missing' type='Public.Page'/>"); break;
            case "duplicate-bytes": File.Copy(fixture.Primary, fixture.Dependency, overwrite: true); break;
            case "metadata-work": fixture.Config = fixture.Config with { Budgets = fixture.Config.Budgets with { MetadataMaxWork = 1 } }; break;
            case "has-receipts": fixture.Config = fixture.Config with { PreparationProvenance = new("public", new string('a', 64), new string('b', 64)) }; break;
        }
        var sources = fixture.Hashes(fixture.Source); var published = fixture.Hashes(fixture.Published);
        Assert.Equal(1, await fixture.Prepare(mutation == "missing-attestation" ? null : attestation));
        Assert.Contains("WEBFORMS_PREPARATION_" + suffix, fixture.Error.ToString(), StringComparison.Ordinal);
        Assert.False(Directory.Exists(fixture.Evidence));
        Assert.DoesNotContain(fixture.Source, fixture.Error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.Published, fixture.Error.ToString(), StringComparison.Ordinal);
        if (mutation == "metadata-work")
        {
            Assert.Contains("webFormsPreparation=gap;phase=metadata;admitted=0", fixture.Output.ToString(), StringComparison.Ordinal);
            Assert.Contains("maxWork=1", fixture.Output.ToString(), StringComparison.Ordinal);
            Assert.Contains("webFormsPreparationGap=ManagedInputTotalWorkLimitExceeded", fixture.Output.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain(fixture.Published, fixture.Output.ToString(), StringComparison.Ordinal);
        }
        foreach (var pair in sources) Assert.Equal(pair.Value, Hash(pair.Key));
        foreach (var pair in published) Assert.Equal(pair.Value, Hash(pair.Key));
    }

    [Fact]
    public async Task All_mode_uses_only_declared_source_pages_and_keeps_completeness_gap()
    {
        using var fixture = new Fixture();
        fixture.Config = fixture.Config with { PageMode = "all", PageRelativePaths = [], PublishSourceRelativePaths = ["Pages/Lookup.aspx", "Pages/Lookup.aspx.vb"] };
        Assert.Equal(0, await fixture.Prepare());
        Assert.Equal(1, fixture.Manifest().PublishInspection.PageCount);
        Assert.Contains("AllPagesPublicationCompletenessNotEstablished", fixture.Manifest().Gaps);
    }

    [Fact]
    public async Task Nested_site_root_uses_commit_membership_relative_to_that_site()
    {
        using var fixture = new Fixture();
        fixture.Config = fixture.Config with { SourceRoot = Path.Combine(fixture.Source, "Pages"), SourceFolders = ["."],
            PageRelativePaths = ["Lookup.aspx"], PublishSourceRelativePaths = ["Lookup.aspx.vb"] };
        Assert.Equal(0, await fixture.Prepare());
        Assert.Equal("bound", fixture.Manifest().PublishInspection.Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Built_in_git_line_endings_are_verified_without_executing_a_clean_filter(bool crossesBufferBoundary)
    {
        using var fixture = new Fixture();
        var path = Path.Combine(fixture.Source, "Pages", "Lookup.aspx.vb");
        if (crossesBufferBoundary) File.WriteAllText(path, new string(' ', 65_535) + "\n" + File.ReadAllText(path));
        File.WriteAllText(Path.Combine(fixture.Source, ".gitattributes"), "*.vb text eol=crlf\n");
        fixture.Git("add", "."); fixture.Git("commit", "-qm", "public line-ending policy");
        File.WriteAllText(path, File.ReadAllText(path).Replace("\n", "\r\n", StringComparison.Ordinal));
        fixture.Git("add", "--renormalize", "Pages/Lookup.aspx.vb");
        fixture.Config = fixture.Config with { SourceCommitSha = GitMetadataProvider.Detect(fixture.Source).CommitSha };
        var status = fixture.Git("status", "--porcelain=v1");
        Assert.True(status.Length == 0, status);
        Assert.True(await fixture.Prepare() == 0, fixture.Error.ToString());
        var membership = Assert.Single(fixture.Manifest().SourceMembership, item => item.ComparisonKind == "git-built-in-eol-normalized");
        Assert.Equal(40, membership.GitBlobObjectId.Length);
        Assert.Equal(64, membership.NormalizationPolicySha256!.Length);
        Assert.Contains("GitBuiltInLineEndingNormalizationUsed", fixture.Manifest().Gaps);
    }

    [Fact]
    public async Task Unsupported_source_transform_does_not_manufacture_a_commit_match()
    {
        using var fixture = new Fixture();
        File.WriteAllText(Path.Combine(fixture.Source, ".gitattributes"), "*.vb filter=public-no-driver\n");
        fixture.Git("add", "."); fixture.Git("commit", "-qm", "public unsupported transform");
        var path = Path.Combine(fixture.Source, "Pages", "Lookup.aspx.vb");
        fixture.Git("update-index", "--assume-unchanged", "Pages/Lookup.aspx.vb");
        File.AppendAllText(path, "' changed source\n");
        fixture.Config = fixture.Config with { SourceCommitSha = GitMetadataProvider.Detect(fixture.Source).CommitSha };
        Assert.Equal(1, await fixture.Prepare());
        Assert.Contains("WEBFORMS_PREPARATION_SOURCE_TRANSFORM_UNSUPPORTED", fixture.Error.ToString(), StringComparison.Ordinal);
        Assert.False(Directory.Exists(fixture.Evidence));
    }

    [Theory]
    [InlineData("pages", "MAPLESS_WEB_ASSEMBLY_UNAVAILABLE")]
    [InlineData("published", "METADATA_INPUT_NOT_UNIQUELY_ADMITTED")]
    public async Task Larger_inventory_does_not_bypass_mapless_or_metadata_admission(string kind, string suffix)
    {
        using var fixture = new Fixture();
        if (kind == "pages")
        {
            var names = new List<string> { "Pages/Lookup.aspx", "Pages/Lookup.aspx.vb" };
            for (var number = 0; number < 32; number++)
            {
                var path = $"Pages/Public{number:D2}.aspx";
                File.WriteAllText(Path.Combine(fixture.Source, path), "<%@ Page Language=\"VB\" %>");
                names.Add(path);
            }
            fixture.Git("add", "."); fixture.Git("commit", "-qm", "public page inventory");
            fixture.Config = fixture.Config with { PageMode = "all", PageRelativePaths = [], PublishSourceRelativePaths = names.ToArray(),
                SourceCommitSha = GitMetadataProvider.Detect(fixture.Source).CommitSha };
        }
        else
        {
            var names = fixture.Config.DependencyAssemblies.ToList();
            for (var number = 0; number < 63; number++)
            {
                var path = $"bin/PublicContext{number:D2}.dll";
                File.Copy(fixture.Dependency, Path.Combine(fixture.Published, path));
                names.Add(path);
            }
            fixture.Config = fixture.Config with { DependencyAssemblies = names.ToArray() };
        }
        Assert.Equal(1, await fixture.Prepare());
        Assert.Contains("WEBFORMS_PREPARATION_" + suffix, fixture.Error.ToString(), StringComparison.Ordinal);
        Assert.False(Directory.Exists(fixture.Evidence));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public async Task Larger_declared_inventory_is_partitioned_pinned_and_run_without_copying_inputs(bool allPages, bool attach)
    {
        using var fixture = new Fixture();
        fixture.AddLargePublicInventory(allPages);
        Dictionary<string, string>? parentBefore = null;
        if (attach)
        {
            var parent = Path.Combine(fixture.Root, "source-parent");
            Assert.Equal(0, await TraceMapCommand.RunAsync(["scan", "--repo", fixture.Source, "--out", parent,
                "--syntax-only", "true", "--retain-source-snapshot"], fixture.Output, fixture.Error));
            fixture.Config = fixture.Config with { Operation = "attach", ParentScanRoot = parent };
            parentBefore = fixture.Hashes(parent);
        }
        var sourceBefore = fixture.Hashes(fixture.Source); var publishedBefore = fixture.Hashes(fixture.Published);
        Assert.Equal(0, await fixture.Prepare());
        Assert.Equal(string.Empty, fixture.Error.ToString());
        var manifest = fixture.Manifest();
        Assert.Equal("bound", manifest.PublishInspection.Status);
        Assert.Equal(allPages ? 67 : 1, manifest.PublishInspection.PageCount);
        Assert.Equal(368, manifest.PublishInspection.SourceFileCount);
        Assert.Equal(69, manifest.PublishInspection.PublishedFileCount);
        foreach (var pair in sourceBefore) Assert.Equal(pair.Value, Hash(pair.Key));
        foreach (var pair in publishedBefore) Assert.Equal(pair.Value, Hash(pair.Key));
        Assert.Equal(sourceBefore.Count, fixture.Hashes(fixture.Source).Count);
        Assert.Equal(publishedBefore.Count, fixture.Hashes(fixture.Published).Count);
        var partitions = manifest.Artifacts.Where(item => item.RelativePath.StartsWith("publish-partitions/", StringComparison.Ordinal)).ToArray();
        Assert.Equal(allPages ? 3 : 2, partitions.Length);
        var inventoryOnly = 0; var pages = 0;
        foreach (var partition in partitions)
        {
            Assert.Equal(partition.Sha256, Hash(Path.Combine(fixture.Evidence, partition.RelativePath)));
            using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(fixture.Evidence, partition.RelativePath)));
            var root = document.RootElement;
            Assert.Equal(manifest.GeneratorSha256, root.GetProperty("receiptGeneratorSha256").GetString());
            Assert.Equal(manifest.BoundedInputSha256, root.GetProperty("receiptInputSha256").GetString());
            Assert.InRange(root.GetProperty("sourceFiles").GetArrayLength(), 1, 256);
            Assert.InRange(root.GetProperty("publishedFiles").GetArrayLength(), 1, 64);
            var count = root.GetProperty("pages").GetArrayLength(); Assert.InRange(count, 0, 32); pages += count;
            if (root.GetProperty("schemaVersion").GetString() == WebFormsReviewPreflightCommand.PublishInventorySchema)
            { inventoryOnly++; Assert.Equal(0, count); }
        }
        Assert.Equal(allPages ? 67 : 1, pages); Assert.Equal(allPages ? 0 : 1, inventoryOnly);
        var run = Path.Combine(fixture.Root, "partitioned-run");
        Assert.Equal(0, await TraceMapCommand.RunAsync(["webforms-review", "preflight", "--config", fixture.PreparedConfig, "--out", run], fixture.Output, fixture.Error));
        var plan = JsonSerializer.Deserialize<WebFormsReviewPreflightManifest>(File.ReadAllText(Path.Combine(run, "run-manifest.json")), JsonOptions)!;
        Assert.Equal(partitions.Length, plan.Inputs.Count(input => input.Role == "publish-receipt-partition"));
        foreach (var partition in partitions)
            Assert.Contains(plan.Inputs, input => input.Role == "publish-receipt-partition" && input.Sha256 == partition.Sha256);
        Assert.Equal(0, await TraceMapCommand.RunAsync(["webforms-review", "run", "--run", run], fixture.Output, fixture.Error));
        var completed = JsonSerializer.Deserialize<WebFormsReviewCheckpoint>(File.ReadAllText(Path.Combine(run, "checkpoints", "0004.json")), JsonOptions)!;
        Assert.Equal("reports-completed-review-only", completed.State);
        Assert.Equal(allPages ? 67 : 1, completed.Reports!.Surfaces);
        var handoff = JsonSerializer.Deserialize<NativeWebFormsReviewHandoff>(File.ReadAllText(Path.Combine(run,
            completed.Reports.ReportAttempt, WebFormsReviewReportExecution.HandoffName)), JsonOptions)!;
        Assert.Equal(allPages ? 67 : 1, handoff.Packet.Surfaces.Count);
        Assert.DoesNotContain("AllPagesCompiledReceiptPartitioningPending", handoff.Gaps);
        if (allPages) Assert.Contains("AllPagesPublicationCompletenessNotEstablished", handoff.Gaps);
        Assert.Equal(partitions.Length, handoff.InputInventory.Count(input => input.Role == "publish-receipt-partition"));
        Assert.Equal(0, await TraceMapCommand.RunAsync(["webforms-review", "resume", "--run", run], fixture.Output, fixture.Error));
        if (parentBefore is not null)
        {
            foreach (var pair in parentBefore) Assert.Equal(pair.Value, Hash(pair.Key));
            Assert.Equal(parentBefore.Count, fixture.Hashes(fixture.Config.ParentScanRoot!).Count);
        }
        File.AppendAllText(Path.Combine(fixture.Evidence, partitions[0].RelativePath), " ");
        Assert.Equal(1, await TraceMapCommand.RunAsync(["webforms-review", "resume", "--run", run], fixture.Output, fixture.Error));
        Assert.Equal(4, Directory.GetFiles(Path.Combine(run, "checkpoints"), "*.json").Length);
        Assert.DoesNotContain(fixture.Source, fixture.Error.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(20_481)]
    public async Task Publish_inventory_budget_requires_a_valid_explicit_value(int maximum)
    {
        using var fixture = new Fixture();
        fixture.Config = fixture.Config with { Budgets = fixture.Config.Budgets with { MaxPublishInputFiles = maximum } };
        Assert.Equal(1, await fixture.Prepare());
        Assert.Contains("WEBFORMS_PREFLIGHT_BUDGET_INVALID", fixture.Error.ToString(), StringComparison.Ordinal);
        Assert.False(Directory.Exists(fixture.Evidence));
    }

    [Fact]
    public async Task Larger_publication_budget_does_not_raise_compiled_or_follow_on_input_budget()
    {
        using var fixture = new Fixture();
        fixture.Config = fixture.Config with { Budgets = fixture.Config.Budgets with { MaxInputFiles = 3, MaxPublishInputFiles = 2048 } };
        Assert.Equal(1, await fixture.Prepare());
        Assert.Contains("WEBFORMS_PREPARATION_FOLLOW_ON_INPUT_COUNT_LIMIT", fixture.Error.ToString(), StringComparison.Ordinal);
        Assert.False(Directory.Exists(fixture.Evidence));
    }

    [Fact]
    public async Task Partition_receipts_must_fit_the_follow_on_hash_byte_budget_before_publication()
    {
        using var fixture = new Fixture(); fixture.AddLargePublicInventory(allPages: true); fixture.Save();
        var plan = await WebFormsReviewPreflightCommand.BuildAsync(fixture.ConfigPath, fixture.Evidence);
        fixture.Config = fixture.Config with { Budgets = fixture.Config.Budgets with { MaxTotalHashBytes = plan.Inputs.Sum(input => input.Bytes) + 1024 } };
        Assert.Equal(1, await fixture.Prepare());
        Assert.Contains("WEBFORMS_PREPARATION_FOLLOW_ON_HASH_BYTES_LIMIT", fixture.Error.ToString(), StringComparison.Ordinal);
        Assert.False(Directory.Exists(fixture.Evidence));
    }

    [Fact]
    public async Task Existing_configs_do_not_implicitly_opt_in_to_larger_declared_inventory()
    {
        using var fixture = new Fixture(); fixture.AddLargePublicInventory(allPages: true);
        fixture.Config = fixture.Config with { Budgets = fixture.Config.Budgets with { MaxPublishInputFiles = null } };
        Assert.Equal(1, await fixture.Prepare());
        Assert.Contains("WEBFORMS_PREFLIGHT_CONFIG_INVALID", fixture.Error.ToString(), StringComparison.Ordinal);
        Assert.False(Directory.Exists(fixture.Evidence));
    }

    [Theory]
    [InlineData("tamper", "PUBLISH_PARTITION_MISMATCH")]
    [InlineData("escape", "RELATIVE_PATH_INVALID")]
    [InlineData("nested", "PUBLISH_RECEIPT_INVALID")]
    [InlineData("duplicate", "PUBLISH_RECEIPT_INVALID")]
    [InlineData("page-limit", "PUBLISH_RECEIPT_LIMIT_OR_SHAPE_INVALID")]
    public async Task Invalid_partition_inventory_never_creates_a_run_or_exposes_input_paths(string mutation, string suffix)
    {
        using var fixture = new Fixture(); fixture.AddLargePublicInventory(allPages: true);
        Assert.Equal(0, await fixture.Prepare());
        var rootPath = Path.Combine(fixture.Evidence, "publish-receipt.local.json");
        var root = JsonNode.Parse(File.ReadAllText(rootPath))!.AsObject();
        var partitions = root["partitions"]!.AsArray();
        var first = partitions[0]!.AsObject();
        var partPath = Path.Combine(fixture.Evidence, first["path"]!.GetValue<string>());
        switch (mutation)
        {
            case "tamper": File.AppendAllText(partPath, " "); break;
            case "escape": first["path"] = "../outside.json"; break;
            case "nested": File.WriteAllText(partPath, root.ToJsonString()); first["sha256"] = Hash(partPath); break;
            case "duplicate": partitions[1] = first.DeepClone(); break;
            case "page-limit":
                var part = JsonNode.Parse(File.ReadAllText(partPath))!.AsObject();
                var pages = part["pages"]!.AsArray(); pages.Add(pages[0]!.DeepClone());
                File.WriteAllText(partPath, part.ToJsonString()); first["sha256"] = Hash(partPath); break;
        }
        File.WriteAllText(rootPath, root.ToJsonString());
        var run = Path.Combine(fixture.Root, "invalid-partition-run");
        fixture.Output.GetStringBuilder().Clear(); fixture.Error.GetStringBuilder().Clear();
        Assert.Equal(1, await TraceMapCommand.RunAsync(["webforms-review", "preflight", "--config", fixture.PreparedConfig, "--out", run], fixture.Output, fixture.Error));
        Assert.Contains("WEBFORMS_PREFLIGHT_" + suffix, fixture.Error.ToString(), StringComparison.Ordinal);
        Assert.False(Directory.Exists(run));
        Assert.DoesNotContain(fixture.Source, fixture.Error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.Published, fixture.Error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Mapless_declared_inventory_is_candidate_evidence_not_a_physical_publish_completeness_claim()
    {
        using var fixture = new Fixture();
        File.Move(fixture.Primary, Path.Combine(fixture.Published, "bin", "App_Web_public.dll"));
        fixture.Config = fixture.Config with { PrimaryAssemblies = ["bin/App_Web_public.dll"], PageMaps = [] };
        Assert.Equal(0, await fixture.Prepare());
        var manifest = fixture.Manifest();
        Assert.Equal("bound", manifest.PublishInspection.Status);
        Assert.Contains("DeclaredPublishInventoryNotHistoricalBuildClosure", manifest.Gaps);
        using var publish = JsonDocument.Parse(File.ReadAllText(Path.Combine(fixture.Evidence, "publish-receipt.local.json")));
        Assert.Equal("mapless-source-type-candidate", publish.RootElement.GetProperty("pages")[0].GetProperty("bindingKind").GetString());
        Assert.Equal(0, publish.RootElement.GetProperty("publishedMapCount").GetInt32());
        Assert.True(File.Exists(fixture.Map)); // Unselected physical maps are not discovered or deleted.
    }

    [Fact]
    public async Task Cancellation_and_output_overlap_do_not_create_a_prepared_root()
    {
        using var fixture = new Fixture();
        fixture.Save();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => WebFormsReviewPreparationCommand.RunAsync(fixture.Arguments(), fixture.Output, fixture.Error, new CancellationToken(true)));
        Assert.False(Directory.Exists(fixture.Evidence));
        var args = fixture.Arguments(); args[4] = Path.Combine(fixture.Published, "new-evidence");
        Assert.Equal(1, await WebFormsReviewPreparationCommand.RunAsync(args, fixture.Output, fixture.Error));
        Assert.False(Directory.Exists(args[4]));
    }

    [Fact]
    public async Task Publish_inspection_facade_preserves_disabled_and_cancellation_behavior()
    {
        using var fixture = new Fixture();
        var options = new ScanOptions(fixture.Source, "unused");
        Assert.Null(WebFormsPublishInputInspector.Inspect(options, fixture.Config.SourceCommitSha));
        Assert.ThrowsAny<OperationCanceledException>(() => WebFormsPublishInputInspector.Inspect(options, fixture.Config.SourceCommitSha, new CancellationToken(true)));
        Assert.False(Directory.Exists(fixture.Evidence));
    }

    private static string Hash(string path) { using var stream = File.OpenRead(path); return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant(); }

    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = WebFormsReviewPreflightCommand.PhysicalPath(Path.Combine(Path.GetTempPath(), "tracemap native preparation #&%-" + Guid.NewGuid().ToString("N")));
        public string Source => Path.Combine(Root, "source");
        public string Published => Path.Combine(Root, "published");
        public string Evidence => Path.Combine(Root, "evidence");
        public string Primary => Path.Combine(Published, "bin", "CompiledEvidence.CSharp.dll");
        public string Dependency => Path.Combine(Published, "bin", "CompiledEvidence.VisualBasic.dll");
        public string Map => Path.Combine(Published, "Lookup.aspx.compiled");
        public string ConfigPath => Path.Combine(Root, "config.json");
        public string PreparedConfig => Path.Combine(Evidence, "review-config.local.json");
        public WebFormsReviewConfig Config { get; set; }
        public StringWriter Output { get; } = new(); public StringWriter Error { get; } = new();
        public Fixture()
        {
            Directory.CreateDirectory(Path.Combine(Source, "Pages")); Directory.CreateDirectory(Path.Combine(Published, "bin"));
            File.WriteAllText(Path.Combine(Source, "Pages", "Lookup.aspx"), "<%@ Page Language=\"VB\" CodeFile=\"Lookup.aspx.vb\" Inherits=\"Public.Page\" %>");
            File.WriteAllText(Path.Combine(Source, "Pages", "Lookup.aspx.vb"), "Public Class Page\n Public Sub Load()\n End Sub\nEnd Class\n");
            File.WriteAllText(Map, "<preserve virtualPath=\"/prefix/Pages/Lookup.aspx\" assembly=\"CompiledEvidence.CSharp\" type=\"Public.Page\"/>");
            var repo = FindRepo();
            File.Copy(Path.Combine(repo, "samples", "compiled-dotnet-evidence", "csharp", "bin", "Debug", "net10.0", "CompiledEvidence.CSharp.dll"), Primary);
            File.Copy(Path.Combine(repo, "samples", "compiled-dotnet-evidence", "vb", "bin", "Debug", "net10.0", "CompiledEvidence.VisualBasic.dll"), Dependency);
            Git("init", "-q"); Git("config", "user.name", "Public test"); Git("config", "user.email", "public@example.invalid");
            Git("config", "core.autocrlf", "false");
            Git("remote", "add", "origin", "https://example.invalid/public-fixture.git"); Git("add", "."); Git("commit", "-qm", "public source");
            Config = new(WebFormsReviewPreflightCommand.ConfigSchema, "fresh", Source, GitMetadataProvider.Detect(Source).CommitSha,
                "projectless", null, [], ["Pages"], "selected", ["Pages/Lookup.aspx"], Published,
                ["bin/CompiledEvidence.CSharp.dll"], ["bin/CompiledEvidence.VisualBasic.dll"], [], [], ["Lookup.aspx.compiled"], null, new(),
                PublishSourceRelativePaths: ["Pages/Lookup.aspx.vb"]);
        }
        public string[] Arguments(string? attestation = "default") => attestation is null ? ["prepare", "--config", ConfigPath, "--out", Evidence]
            : ["prepare", "--config", ConfigPath, "--out", Evidence, "--attest-exact-source-commit", attestation == "default" ? Config.SourceCommitSha : attestation];
        public void Save() => File.WriteAllText(ConfigPath, JsonSerializer.Serialize(Config, JsonOptions));
        public void AddLargePublicInventory(bool allPages)
        {
            var sources = new List<string> { "Pages/Lookup.aspx", "Pages/Lookup.aspx.vb" };
            var maps = new List<string> { "Lookup.aspx.compiled" };
            for (var number = 1; number < 67; number++)
            {
                var page = $"Pages/Public{number:D3}.aspx"; var map = $"Public{number:D3}.aspx.compiled";
                File.WriteAllText(Path.Combine(Source, page), "<%@ Page Language=\"VB\" Inherits=\"Public.Page\" %>");
                File.WriteAllText(Path.Combine(Published, map), $"<preserve virtualPath=\"/prefix/{page}\" assembly=\"CompiledEvidence.CSharp\" type=\"Public.Page\" />");
                sources.Add(page); maps.Add(map);
            }
            Directory.CreateDirectory(Path.Combine(Source, "App_Code"));
            for (var number = 0; number < 300; number++)
            {
                var path = $"App_Code/Extra{number:D3}.vb";
                File.WriteAllText(Path.Combine(Source, path), $"Public Class Extra{number:D3}\nEnd Class\n"); sources.Add(path);
            }
            Git("add", "."); Git("commit", "-qm", "public partitioned source inventory");
            Config = Config with { SourceCommitSha = GitMetadataProvider.Detect(Source).CommitSha,
                PageMode = allPages ? "all" : "selected", PageRelativePaths = allPages ? [] : ["Pages/Lookup.aspx"],
                SourceFolders = ["Pages", "App_Code"], PublishSourceRelativePaths = sources.ToArray(), PageMaps = maps.ToArray(),
                Budgets = Config.Budgets with { MaxPublishInputFiles = 2048 } };
        }
        public async Task<int> Prepare(string? attestation = "default") { Save(); Output.GetStringBuilder().Clear(); Error.GetStringBuilder().Clear();
            return await TraceMapCommand.RunAsync(["webforms-review", .. Arguments(attestation)], Output, Error); }
        public WebFormsReviewPreparationManifest Manifest() => JsonSerializer.Deserialize<WebFormsReviewPreparationManifest>(File.ReadAllText(Path.Combine(Evidence, "preparation-manifest.local.json")), JsonOptions)!;
        public Dictionary<string, string> Hashes(string root) => Directory.GetFiles(root, "*", SearchOption.AllDirectories).ToDictionary(path => path, Hash);
        public string Git(params string[] args)
        {
            using var process = new Process { StartInfo = new("git") { WorkingDirectory = Source, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false } };
            process.StartInfo.Environment["GIT_OPTIONAL_LOCKS"] = "0";
            foreach (var arg in args) process.StartInfo.ArgumentList.Add(arg);
            process.Start(); var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
            Assert.True(process.WaitForExit(10_000)); Task.WaitAll(stdout, stderr); Assert.Equal(0, process.ExitCode);
            return stdout.Result;
        }
        private static string FindRepo()
        {
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
                if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")) && Directory.Exists(Path.Combine(directory.FullName, "samples"))) return directory.FullName;
            throw new InvalidOperationException("Public fixtures unavailable");
        }
        public void Dispose() { Output.Dispose(); Error.Dispose(); if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true); }
    }
}
