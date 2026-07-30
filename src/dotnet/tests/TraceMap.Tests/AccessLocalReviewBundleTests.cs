using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using TraceMap.Access;
using TraceMap.Access.Cli;
using TraceMap.Cli;
using TraceMap.Core;
using TraceMap.Reporting;
using TraceMap.Storage;

namespace TraceMap.Tests;

public sealed class AccessLocalReviewBundleTests
{
    private const string ProtectedTable = "CustomerLedgerPrivate_8172";
    private const string ProtectedField = "CredentialColumnPrivate_8172";
    private const string ProtectedQuery = "SELECT * FROM CustomerLedgerPrivate_8172";
    private const string ProtectedServer = "private-db-server-8172";

    [Fact]
    public async Task Cli_help_describes_read_side_local_bundle_boundary()
    {
        var output = new StringWriter();
        var error = new StringWriter();

        var exit = await TraceMapCommand.RunAsync(["access-review", "--help"], output, error);

        Assert.Equal(0, exit);
        Assert.Empty(error.ToString());
        Assert.Contains(
            "tracemap access-review create --scan-output <access-scan-directory> --out <bundle-directory>",
            output.ToString(),
            StringComparison.Ordinal);
        Assert.Contains("does not invoke Microsoft Access", output.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Bundle_composes_access_review_and_explorer_with_deterministic_safe_integrity_manifest()
    {
        using var temp = new TempDirectory();
        var scanOutput = await WriteCountOnlyAccessScanAsync(temp.Path);
        var firstOutput = Path.Combine(temp.Path, "bundle-one");
        var secondOutput = Path.Combine(temp.Path, "bundle-two");

        var first = await AccessLocalReviewBundle.CreateAsync(new(scanOutput, firstOutput));
        var second = await AccessLocalReviewBundle.CreateAsync(new(scanOutput, secondOutput));

        Assert.Equal(AccessLocalReviewBundle.SchemaVersion, first.Manifest.SchemaVersion);
        Assert.True(first.Manifest.TracemapGenerated);
        Assert.Equal("hidden", first.Manifest.ClaimLevel);
        Assert.Equal(new string('e', 40), first.Manifest.CommitSha);
        Assert.Equal(ReleaseReviewStatuses.Available, first.Manifest.AccessEvidenceStatus);
        Assert.True(first.Manifest.Counts.AccessFindingCount >= 5);
        Assert.True(first.Manifest.Counts.AccessGapCount >= 3);
        Assert.True(first.Manifest.Counts.ExplorerEvidenceRowCount > 0);
        Assert.Equal(
            JsonSerializer.Serialize(first.Manifest),
            JsonSerializer.Serialize(second.Manifest));

        string[] expectedFiles =
        [
            "README.md",
            AccessLocalReviewBundle.ManifestFileName,
            "explorer/README.md",
            "explorer/assets/explorer.css",
            "explorer/assets/explorer.js",
            "explorer/data/explorer-data.json",
            "explorer/data/explorer-manifest.json",
            "explorer/index.html",
            "release-review/release-review.json",
            "release-review/release-review.md"
        ];
        Assert.Equal(expectedFiles, first.WrittenFiles);
        Assert.Equal(expectedFiles, second.WrittenFiles);
        AssertDirectoriesEqual(firstOutput, secondOutput);

        var readme = await File.ReadAllTextAsync(Path.Combine(firstOutput, "README.md"));
        Assert.Contains("(explorer/index.html)", readme, StringComparison.Ordinal);
        Assert.Contains("(release-review/release-review.md)", readme, StringComparison.Ordinal);
        Assert.Contains("count-only", readme, StringComparison.Ordinal);
        Assert.Contains("does not prove row contents", readme, StringComparison.Ordinal);

        var releaseJson = await File.ReadAllTextAsync(
            Path.Combine(firstOutput, "release-review", "release-review.json"));
        using (var release = JsonDocument.Parse(releaseJson))
        {
            Assert.Equal(
                ["access-evidence"],
                release.RootElement.GetProperty("query").GetProperty("scopes")
                    .EnumerateArray().Select(item => item.GetString()!).ToArray());
            Assert.Equal(
                ReleaseReviewStatuses.Available,
                release.RootElement.GetProperty("accessEvidence").GetProperty("status").GetString());
        }

        var explorerManifest = await File.ReadAllTextAsync(
            Path.Combine(firstOutput, "explorer", "data", "explorer-manifest.json"));
        Assert.Contains("\"safetyProfile\": \"hidden-local\"", explorerManifest, StringComparison.Ordinal);
        Assert.Contains("\"claimLevel\": \"hidden-local\"", explorerManifest, StringComparison.Ordinal);

        foreach (var file in first.Manifest.Files)
        {
            var fullPath = Path.Combine(firstOutput, file.Path.Replace('/', Path.DirectorySeparatorChar));
            Assert.Equal(file.SizeBytes, new FileInfo(fullPath).Length);
            using var stream = File.OpenRead(fullPath);
            Assert.Equal(file.Sha256, Convert.ToHexStringLower(SHA256.HashData(stream)));
        }

        AssertBundleDoesNotContain(
            firstOutput,
            temp.Path,
            ProtectedTable,
            ProtectedField,
            ProtectedQuery,
            ProtectedServer);
    }

    [Fact]
    public async Task Cli_rejects_non_access_missing_and_overlapping_inputs_without_publishing()
    {
        using var temp = new TempDirectory();
        var incomplete = Path.Combine(temp.Path, "incomplete");
        Directory.CreateDirectory(incomplete);
        var output = new StringWriter();
        var error = new StringWriter();

        var incompleteExit = await TraceMapCommand.RunAsync(
            ["access-review", "create", "--scan-output", incomplete, "--out", Path.Combine(temp.Path, "missing-bundle")],
            output,
            error);
        Assert.Equal(1, incompleteExit);
        Assert.Contains("AccessReviewInputIncomplete", error.ToString(), StringComparison.Ordinal);

        var scanOutput = await WriteCountOnlyAccessScanAsync(temp.Path);
        error.GetStringBuilder().Clear();
        var overlapExit = await TraceMapCommand.RunAsync(
            ["access-review", "create", "--scan-output", scanOutput, "--out", Path.Combine(scanOutput, "bundle")],
            output,
            error);
        Assert.Equal(1, overlapExit);
        Assert.Contains("AccessReviewPathOverlap", error.ToString(), StringComparison.Ordinal);

        var nonAccess = await WriteNonAccessScanAsync(temp.Path);
        error.GetStringBuilder().Clear();
        var nonAccessExit = await TraceMapCommand.RunAsync(
            ["access-review", "create", "--scan-output", nonAccess, "--out", Path.Combine(temp.Path, "non-access-bundle")],
            output,
            error);
        Assert.Equal(1, nonAccessExit);
        Assert.Contains("AccessEvidenceUnavailable", error.ToString(), StringComparison.Ordinal);
        Assert.False(Directory.Exists(Path.Combine(temp.Path, "non-access-bundle")));
    }

    [Fact]
    public async Task Force_replaces_only_an_intact_generated_bundle_and_preserves_caller_owned_directories()
    {
        using var temp = new TempDirectory();
        var scanOutput = await WriteCountOnlyAccessScanAsync(temp.Path);
        var bundle = Path.Combine(temp.Path, "bundle");
        await AccessLocalReviewBundle.CreateAsync(new(scanOutput, bundle));

        var rerun = await AccessLocalReviewBundle.CreateAsync(new(scanOutput, bundle, Force: true));
        Assert.Equal(ReleaseReviewStatuses.Available, rerun.Manifest.AccessEvidenceStatus);

        await File.WriteAllTextAsync(Path.Combine(bundle, "caller-owned.txt"), "do not delete");
        var modifiedError = await Assert.ThrowsAsync<AccessLocalReviewException>(
            () => AccessLocalReviewBundle.CreateAsync(new(scanOutput, bundle, Force: true)));
        Assert.Contains("AccessReviewOutputCollision", modifiedError.Message, StringComparison.Ordinal);
        Assert.Equal("do not delete", await File.ReadAllTextAsync(Path.Combine(bundle, "caller-owned.txt")));

        var callerOwned = Path.Combine(temp.Path, "caller-owned");
        Directory.CreateDirectory(callerOwned);
        await File.WriteAllTextAsync(Path.Combine(callerOwned, "sentinel.txt"), "preserve");
        var foreignError = await Assert.ThrowsAsync<AccessLocalReviewException>(
            () => AccessLocalReviewBundle.CreateAsync(new(scanOutput, callerOwned, Force: true)));
        Assert.Contains("AccessReviewOutputCollision", foreignError.Message, StringComparison.Ordinal);
        Assert.Equal("preserve", await File.ReadAllTextAsync(Path.Combine(callerOwned, "sentinel.txt")));

        var poisonedBundle = Path.Combine(temp.Path, "poisoned-bundle");
        await AccessLocalReviewBundle.CreateAsync(new(scanOutput, poisonedBundle));
        var manifestPath = Path.Combine(poisonedBundle, AccessLocalReviewBundle.ManifestFileName);
        var manifestText = await File.ReadAllTextAsync(manifestPath);
        await File.WriteAllTextAsync(
            manifestPath,
            manifestText.Replace(
                "\"path\": \"README.md\"",
                "\"path\": \"../outside.txt\"",
                StringComparison.Ordinal));
        var outside = Path.Combine(temp.Path, "outside.txt");
        await File.WriteAllTextAsync(outside, "preserve");
        var traversalError = await Assert.ThrowsAsync<AccessLocalReviewException>(
            () => AccessLocalReviewBundle.CreateAsync(new(scanOutput, poisonedBundle, Force: true)));
        Assert.Contains("AccessReviewOutputCollision", traversalError.Message, StringComparison.Ordinal);
        Assert.Equal("preserve", await File.ReadAllTextAsync(outside));
    }

    [Fact]
    public void Publication_failure_restores_or_retains_the_previous_bundle()
    {
        using var temp = new TempDirectory();
        var output = Path.Combine(temp.Path, "bundle");
        var staging = Path.Combine(temp.Path, "staging");
        var backup = Path.Combine(temp.Path, "backup");
        Directory.CreateDirectory(output);
        Directory.CreateDirectory(staging);
        File.WriteAllText(Path.Combine(output, "old.txt"), "old");
        File.WriteAllText(Path.Combine(staging, "new.txt"), "new");

        Assert.ThrowsAny<IOException>(
            () => AccessLocalReviewBundle.Publish(
                staging,
                output,
                backup,
                () =>
                {
                    Directory.CreateDirectory(output);
                    File.WriteAllText(Path.Combine(output, "collision.txt"), "collision");
                }));

        Assert.Equal("old", File.ReadAllText(Path.Combine(backup, "old.txt")));
        Assert.Equal("collision", File.ReadAllText(Path.Combine(output, "collision.txt")));
        Assert.True(File.Exists(Path.Combine(staging, "new.txt")));

        var restoredOutput = Path.Combine(temp.Path, "restored-bundle");
        var missingStaging = Path.Combine(temp.Path, "missing-staging");
        var restoredBackup = Path.Combine(temp.Path, "restored-backup");
        Directory.CreateDirectory(restoredOutput);
        File.WriteAllText(Path.Combine(restoredOutput, "old.txt"), "old");

        Assert.ThrowsAny<IOException>(
            () => AccessLocalReviewBundle.Publish(missingStaging, restoredOutput, restoredBackup));

        Assert.Equal("old", File.ReadAllText(Path.Combine(restoredOutput, "old.txt")));
        Assert.False(Directory.Exists(restoredBackup));
    }

    [Fact]
    public async Task Mixed_scan_artifacts_fail_closed_and_unexpected_io_does_not_leak_paths()
    {
        using var temp = new TempDirectory();
        var first = await WriteCountOnlyAccessScanAsync(temp.Path, "first-scan");
        var second = await WriteCountOnlyAccessScanAsync(
            temp.Path,
            "second-scan",
            new string('f', 40));
        File.Copy(Path.Combine(second, "index.sqlite"), Path.Combine(first, "index.sqlite"), overwrite: true);

        var output = new StringWriter();
        var error = new StringWriter();
        var mismatchExit = await TraceMapCommand.RunAsync(
            ["access-review", "create", "--scan-output", first, "--out", Path.Combine(temp.Path, "mixed-bundle")],
            output,
            error);

        Assert.Equal(1, mismatchExit);
        Assert.Contains("AccessReviewInputMismatch", error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(temp.Path, error.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.False(Directory.Exists(Path.Combine(temp.Path, "mixed-bundle")));

        var corrupt = await WriteCountOnlyAccessScanAsync(temp.Path, "corrupt-scan");
        await File.WriteAllTextAsync(Path.Combine(corrupt, "index.sqlite"), "not a SQLite index");
        error.GetStringBuilder().Clear();
        var corruptExit = await TraceMapCommand.RunAsync(
            ["access-review", "create", "--scan-output", corrupt, "--out", Path.Combine(temp.Path, "corrupt-bundle")],
            output,
            error);

        Assert.Equal(1, corruptExit);
        Assert.Contains("AccessReviewFailed", error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(temp.Path, error.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.False(Directory.Exists(Path.Combine(temp.Path, "corrupt-bundle")));
    }

    [Fact]
    public async Task Modified_ndjson_evidence_with_the_same_fact_id_fails_closed()
    {
        using var temp = new TempDirectory();
        var scanOutput = await WriteCountOnlyAccessScanAsync(temp.Path, "modified-fact-scan");
        var factsPath = Path.Combine(scanOutput, "facts.ndjson");
        var lines = await File.ReadAllLinesAsync(factsPath);
        using var document = JsonDocument.Parse(lines[0]);
        var ruleId = document.RootElement.GetProperty("ruleId").GetString()!;
        lines[0] = lines[0].Replace(
            $"\"ruleId\":\"{ruleId}\"",
            "\"ruleId\":\"modified.rule.v1\"",
            StringComparison.Ordinal);
        await File.WriteAllLinesAsync(factsPath, lines);

        var exception = await Assert.ThrowsAsync<AccessLocalReviewException>(
            () => AccessLocalReviewBundle.CreateAsync(new(
                scanOutput,
                Path.Combine(temp.Path, "modified-fact-bundle"))));

        Assert.Equal("AccessReviewInputMismatch", exception.Code);
        Assert.DoesNotContain(temp.Path, exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Symlinked_output_ancestor_cannot_bypass_scan_overlap()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var temp = new TempDirectory();
        var scanOutput = await WriteCountOnlyAccessScanAsync(temp.Path);
        var link = Path.Combine(temp.Path, "scan-link");
        Directory.CreateSymbolicLink(link, scanOutput);
        var redirectedOutput = Path.Combine(link, "bundle");

        var exception = await Assert.ThrowsAsync<AccessLocalReviewException>(
            () => AccessLocalReviewBundle.CreateAsync(new(scanOutput, redirectedOutput)));

        Assert.Equal("AccessReviewPathOverlap", exception.Code);
        Assert.False(Directory.Exists(Path.Combine(scanOutput, "bundle")));
    }

    [Theory]
    [InlineData("databases/private/design.accdb")]
    [InlineData("src/home/schema.accdb")]
    public async Task Repository_relative_path_segments_do_not_trigger_absolute_path_denial(
        string databaseRelativePath)
    {
        using var temp = new TempDirectory();
        var scanOutput = await WriteCountOnlyAccessScanAsync(
            temp.Path,
            "relative-path-scan",
            databaseRelativePath: databaseRelativePath);
        var bundle = Path.Combine(temp.Path, "relative-path-bundle");

        await AccessLocalReviewBundle.CreateAsync(new(scanOutput, bundle));

        var releaseReview = await File.ReadAllTextAsync(
            Path.Combine(bundle, "release-review", "release-review.json"));
        Assert.Contains(databaseRelativePath, releaseReview, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Windows_harnesses_validate_and_optionally_retain_the_local_review_bundle_without_new_extraction()
    {
        var repoRoot = FindRepoRoot();
        var synthetic = await File.ReadAllTextAsync(
            Path.Combine(repoRoot, "scripts", "access-validation", "Invoke-AccessSmoke.ps1"));
        var representative = await File.ReadAllTextAsync(
            Path.Combine(repoRoot, "scripts", "access-validation", "Invoke-AccessRepresentativeSmoke.ps1"));

        foreach (var script in new[] { synthetic, representative })
        {
            Assert.Contains("[string]$ReviewBundlePath", script, StringComparison.Ordinal);
            Assert.Contains(
                "access-review create --scan-output $outA --out $accessReviewOutput",
                script,
                StringComparison.Ordinal);
            Assert.Contains("tracemap-access-local-review-bundle.v1", script, StringComparison.Ordinal);
            Assert.Contains("localReviewBundleContractCorrect", script, StringComparison.Ordinal);
            Assert.Contains(
                "@(\"available\", \"truncated\") -notcontains $accessReviewManifest.accessEvidenceStatus",
                script,
                StringComparison.Ordinal);
            Assert.Contains("$accessReviewOutput", script, StringComparison.Ordinal);
            Assert.DoesNotContain("RunMacro", script, StringComparison.Ordinal);
            Assert.DoesNotContain("OpenRecordset", script, StringComparison.Ordinal);
            Assert.DoesNotContain("OpenQuery", script, StringComparison.Ordinal);
            Assert.DoesNotContain("SaveAsText", script, StringComparison.Ordinal);
        }

        Assert.Contains(
            "Review bundle must be a new path outside the disposable smoke root",
            synthetic,
            StringComparison.Ordinal);
        Assert.Contains(
            "representative review bundle must be a new path outside scratch",
            representative,
            StringComparison.Ordinal);
    }

    private static async Task<string> WriteCountOnlyAccessScanAsync(
        string root,
        string outputName = "access-scan",
        string? commitSha = null,
        string databaseRelativePath = "fixture.accdb")
    {
        var databasePath = Path.Combine(root, $"{outputName}.accdb");
        await File.WriteAllBytesAsync(databasePath, [1, 2, 3, 4]);
        var databaseHash = AccessInputValidator.HashFile(databasePath);
        var output = Path.Combine(root, outputName);
        var input = new AccessValidatedInput(
            root,
            "private-repository-label",
            AccessSafeValues.RoleHash("access-repository-identity", "private-repository-label"),
            null,
            "test",
            commitSha ?? new string('e', 40),
            databasePath,
            databaseRelativePath,
            databaseHash,
            ".accdb",
            output,
            false);
        var seed = AccessSafeValues.DatabaseIdentitySeed(
            input.RepositoryIdentityHash,
            input.CommitSha,
            input.DatabaseRelativePath,
            input.DatabaseHash);
        var table = AccessSafeValues.Identity(seed, "table", ProtectedTable);
        var field = AccessSafeValues.Identity(seed, $"field-{table.StableKey}", ProtectedField);
        var index = AccessSafeValues.Identity(seed, $"index-{table.StableKey}", "PrivateIndex_8172");
        var relationship = AccessSafeValues.Identity(seed, "relationship", "PrivateRelationship_8172");
        var query = AccessSafeValues.Identity(seed, "query", "PrivateQuery_8172");
        var external = AccessSafeValues.Identity(seed, "table", ProtectedServer);
        var projection = new AccessDatabaseProjection(
            "tracemap.access-projection.v1",
            databaseHash,
            ".accdb",
            "16.0",
            1234,
            false,
            false,
            0,
            [new(table, [new(field, 0, "long", 4, true)], [new(index, true, true, [field.StableKey])])],
            [new(relationship, table.StableKey, table.StableKey, 0, [new(field.StableKey, field.StableKey, 0)])],
            [new(
                query,
                "select",
                AccessSafeValues.RoleHash("access-query-sql", ProtectedQuery),
                ProtectedQuery.Length,
                "complete",
                [],
                [new(table.StableKey, "table", "direct-static-reference")],
                false,
                null,
                null)],
            [new(
                external,
                "odbc",
                AccessSafeValues.RoleHash("access-linked-source", ProtectedServer),
                "linked-table")],
            [
                new("AccessFormReportCoverageUnavailable", "ui-catalog", null, RuleIds.LegacyAccessUiSurface),
                new("AccessVbaProjectUnavailable", "vba-project", null, RuleIds.LegacyAccessVba),
                new("AccessMacroIdentityUnavailable", "macro-catalog", null, RuleIds.LegacyAccessMacroGap)
            ],
            [
                new("formsReports", "counts-observed-identities-unavailable"),
                new("vba", "count-observed-source-unavailable"),
                new("macros", "named-count-observed-identities-bodies-unavailable"),
                new("rowDataRead", "false"),
                new("executionPerformed", "false"),
                new("startupSuppression", "force-disable-requested")
            ],
            UiInventory: new(2, 1, "counts-observed-identities-unavailable"),
            VbaInventory: new(3, true, "count-observed-source-unavailable"),
            MacroInventory: new(4, null, "named-count-observed-identities-bodies-unavailable"));
        var scan = AccessFactBuilder.Build(input, projection, new(root, databaseRelativePath, output));
        await AccessArtifactWriter.WriteAsync(output, scan, AccessLimits.Default);
        return output;
    }

    private static async Task<string> WriteNonAccessScanAsync(string root)
    {
        var output = Path.Combine(root, "non-access-scan");
        Directory.CreateDirectory(Path.Combine(output, "logs"));
        var manifest = new ScanManifest(
            "scan-non-access",
            "repo",
            null,
            "dev",
            new string('a', 40),
            "test",
            DateTimeOffset.UnixEpoch,
            "Level1Semantic",
            "Succeeded",
            [],
            [],
            [],
            []);
        var evidence = new EvidenceSpan("Program.cs", 1, 1, null, "test", "1.0.0");
        var fact = new CodeFact(
            "fact-repo",
            manifest.ScanId,
            manifest.RepoName,
            manifest.CommitSha,
            null,
            FactTypes.RepoScanned,
            "scan.repository.v1",
            EvidenceTiers.Tier2Structural,
            null,
            null,
            null,
            evidence,
            new Dictionary<string, string>());
        var scan = new ScanResult(manifest, [fact], []);
        await ManifestWriter.WriteAsync(Path.Combine(output, "scan-manifest.json"), manifest);
        await JsonlFactWriter.WriteAsync(Path.Combine(output, "facts.ndjson"), scan.Facts);
        SqliteIndexWriter.Write(Path.Combine(output, "index.sqlite"), manifest, scan.Facts);
        SqliteConnection.ClearAllPools();
        await File.WriteAllTextAsync(Path.Combine(output, "report.md"), "# Non-Access scan\n");
        await File.WriteAllTextAsync(Path.Combine(output, "logs", "analyzer.log"), "scan complete\n");
        return output;
    }

    private static void AssertDirectoriesEqual(string first, string second)
    {
        var firstFiles = Directory.EnumerateFiles(first, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(first, path).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        var secondFiles = Directory.EnumerateFiles(second, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(second, path).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(firstFiles, secondFiles);
        foreach (var relativePath in firstFiles)
        {
            Assert.Equal(
                File.ReadAllBytes(Path.Combine(first, relativePath.Replace('/', Path.DirectorySeparatorChar))),
                File.ReadAllBytes(Path.Combine(second, relativePath.Replace('/', Path.DirectorySeparatorChar))));
        }
    }

    private static void AssertBundleDoesNotContain(string root, params string[] markers)
    {
        foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var content = Encoding.UTF8.GetString(File.ReadAllBytes(path));
            foreach (var marker in markers)
            {
                Assert.DoesNotContain(marker, content, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "src", "dotnet", "TraceMap.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("TraceMap repository root was not found.");
    }
}
