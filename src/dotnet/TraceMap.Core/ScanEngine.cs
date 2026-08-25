using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis.Text;

namespace TraceMap.Core;

public static class ScanEngine
{
    public static ScanResult Scan(ScanOptions options) => Scan(options, CancellationToken.None);

    public static ScanResult Scan(ScanOptions options, CancellationToken cancellationToken) =>
        Scan(options, receiptRecorder: null, cancellationToken);

    public static ScanResult Scan(
        ScanOptions options,
        ScanReceiptRecorder? receiptRecorder,
        CancellationToken cancellationToken = default,
        ScanProgressReporter? progress = null)
    {
        using var scanOperation = TraceMapDiagnostics.StartScan(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var repoPath = Path.GetFullPath(options.RepoPath);
        var outputPath = Path.GetFullPath(options.OutputPath);
        if (!Directory.Exists(repoPath))
        {
            throw new DirectoryNotFoundException($"Repository path does not exist: {repoPath}");
        }

        GitMetadata git;
        IReadOnlyList<FileInventoryItem> fullInventory;
        IReadOnlyList<FileInventoryItem> inventory;
        using var discoveryReceipt = receiptRecorder?.StartStage("discovery", "inventory-and-identity");
        try
        {
            using (var discoveryOperation = TraceMapDiagnostics.StartPhase("scan", TraceMapDiagnosticPhases.Discovery, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.StartStage(ScanProgressReporter.ScanOperation, ScanProgressStages.RepositoryIdentity);
                git = GitMetadataProvider.Detect(repoPath);
                cancellationToken.ThrowIfCancellationRequested();
                receiptRecorder?.Bind(git);
                progress?.FinishStage(
                    ScanProgressReporter.ScanOperation,
                    ScanProgressStages.RepositoryIdentity,
                    "completed");
                MsBuildBinlogExtractor.ValidateCommitBinding(git.CommitSha, options.BinlogPaths, options.BinlogCommitSha);
                progress?.StartStage(ScanProgressReporter.ScanOperation, ScanProgressStages.Inventory);
                var sourcePathComparer = CSharpSemanticExtractor.CreateSourcePathComparer(repoPath);
                fullInventory = FileInventory.Collect(
                    repoPath,
                    outputPath,
                    options.ExcludeGlobs,
                    sourcePathComparer,
                    options.IncludeGlobs);
                inventory = ApplyScope(fullInventory, repoPath, options);
                cancellationToken.ThrowIfCancellationRequested();
                progress?.FinishStage(
                    ScanProgressReporter.ScanOperation,
                    ScanProgressStages.Inventory,
                    "completed",
                    counts: new Dictionary<string, long> { ["files"] = inventory.Count });
                discoveryOperation.RecordItems(inventory.Count);
                discoveryOperation.Complete(TraceMapDiagnosticOutcome.Succeeded);
                discoveryReceipt?.Complete("succeeded", "unchanged", "inventory-and-commit-observed");
            }
        }
        catch (Exception ex)
        {
            if (ex is not OperationCanceledException)
            {
                progress?.FailActiveStage(ScanProgressReporter.ScanOperation, "SCAN_DISCOVERY_FAILED");
            }

            discoveryReceipt?.Fail(ex, "stage-started");
            throw;
        }

        IReadOnlyDictionary<string, string> semanticInputSnapshot;
        SemanticExtractionResult semanticResult;
        bool semanticToolchainReducedCoverage;
        using var semanticReceipt = receiptRecorder?.StartStage("semantic-analysis", "compiler-and-syntax-analysis");
        try
        {
            progress?.StartStage(ScanProgressReporter.ScanOperation, ScanProgressStages.SourceSnapshotCapture);
            try
            {
                semanticInputSnapshot = CaptureSemanticInputSnapshot(repoPath, fullInventory, cancellationToken);
            }
            catch (SourceInventoryException ex)
            {
                throw new SourceSnapshotException(ex);
            }

            progress?.FinishStage(
                ScanProgressReporter.ScanOperation,
                ScanProgressStages.SourceSnapshotCapture,
                "completed");
            progress?.FinishStage(
                ScanProgressReporter.ScanOperation,
                ScanProgressStages.ProjectSelection,
                "completed",
                counts: ProjectSelectionCounts(inventory));
            using (var semanticOperation = TraceMapDiagnostics.StartPhase("scan", TraceMapDiagnosticPhases.SemanticAnalysis, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                semanticResult = CSharpSemanticExtractor.Extract(
                    repoPath,
                    inventory,
                    options,
                    fullInventory,
                    cancellationToken,
                    progress);
                semanticToolchainReducedCoverage = HasToolchainSemanticReduction(semanticResult);
                var semanticStageCoverage = semanticResult.Attempted
                    ? semanticToolchainReducedCoverage ? "semantic-reduced" : "semantic"
                    : semanticToolchainReducedCoverage ? "syntax-reduced" : "syntax";
                cancellationToken.ThrowIfCancellationRequested();
                VerifySemanticInputSnapshot(repoPath, fullInventory, semanticResult, semanticInputSnapshot, cancellationToken);
                semanticOperation.Complete(semanticToolchainReducedCoverage
                    ? TraceMapDiagnosticOutcome.Partial
                    : TraceMapDiagnosticOutcome.Succeeded);
                semanticReceipt?.Complete(
                    semanticToolchainReducedCoverage ? "partial" : "succeeded",
                    semanticStageCoverage,
                    "semantic-input-snapshot-verified",
                    retryability: semanticToolchainReducedCoverage ? "retry-after-dependency-restoration" : "not-required",
                    nextAction: semanticToolchainReducedCoverage ? "review-analysis-gaps" : "continue");
            }
        }
        catch (Exception ex)
        {
            if (ex is not OperationCanceledException)
            {
                progress?.FailActiveStage(ScanProgressReporter.ScanOperation, "SEMANTIC_STAGE_FAILED");
            }

            semanticReceipt?.Fail(ex, "stage-started");
            throw;
        }

        inventory = IncludeSemanticallyAnalyzedFiles(inventory, fullInventory, semanticResult);
        var discoveredSnapshotInventory = IncludeSemanticInputs(inventory, fullInventory, semanticResult);
        IReadOnlyList<FileInventoryItem> authoritativeSnapshotInventory;
        string sourceSnapshotDigest;
        using var preVerificationReceipt = receiptRecorder?.StartStage("source-verification", "pre-extraction-snapshot-verification");
        try
        {
            progress?.StartStage(ScanProgressReporter.ScanOperation, ScanProgressStages.SourceVerification);
            var sourcePathComparer = CSharpSemanticExtractor.CreateSourcePathComparer(repoPath);
            var refreshedFullInventory = FileInventory.Collect(
                repoPath,
                outputPath,
                options.ExcludeGlobs,
                sourcePathComparer,
                options.IncludeGlobs);
            var refreshedInventory = ApplyScope(refreshedFullInventory, repoPath, options);
            refreshedInventory = IncludeSemanticallyAnalyzedFiles(
                refreshedInventory,
                refreshedFullInventory,
                semanticResult);
            var refreshedSnapshotInventory = IncludeSemanticInputs(
                refreshedInventory,
                refreshedFullInventory,
                semanticResult);
            VerifySourceSnapshotInventoryMembership(discoveredSnapshotInventory, refreshedSnapshotInventory);
            VerifySemanticInputSnapshot(
                repoPath,
                refreshedFullInventory,
                semanticResult,
                semanticInputSnapshot,
                cancellationToken);
            fullInventory = refreshedFullInventory;
            inventory = refreshedInventory;
            authoritativeSnapshotInventory = refreshedSnapshotInventory;
            sourceSnapshotDigest = CreateSourceSnapshotDigest(repoPath, authoritativeSnapshotInventory, cancellationToken);
            progress?.FinishStage(ScanProgressReporter.ScanOperation, ScanProgressStages.SourceVerification, "completed");
            preVerificationReceipt?.Complete("succeeded", "unchanged", "source-snapshot-verified");
        }
        catch (SourceInventoryException ex)
        {
            var transformed = new SourceSnapshotException(ex);
            progress?.FailActiveStage(ScanProgressReporter.ScanOperation, "SOURCE_VERIFICATION_FAILED");
            preVerificationReceipt?.Fail(transformed, "stage-started");
            throw transformed;
        }
        catch (Exception ex)
        {
            if (ex is not OperationCanceledException)
            {
                progress?.FailActiveStage(ScanProgressReporter.ScanOperation, "SOURCE_VERIFICATION_FAILED");
            }

            preVerificationReceipt?.Fail(ex, "stage-started");
            throw;
        }

        var solutions = inventory
            .Where(item => item.Kind == "Solution")
            .Select(item => item.RelativePath)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        var projects = inventory
            .Where(item => item.Kind is "Project" or "SqlProject")
            .Select(item => item.RelativePath)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        var targetFrameworkInfos = ProjectFileReader.ReadTargetFrameworks(repoPath, inventory);
        var targetFrameworks = targetFrameworkInfos
            .Select(item => item.TargetFramework)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var semanticallyAnalyzedFiles = GetSemanticallyAnalyzedFiles(semanticResult);
        var migrationSyntaxFallback = FrameworkMigrationEvidenceExtractor.ExtractSyntaxFallback(
            repoPath,
            inventory,
            semanticallyAnalyzedFiles);
        var migrationFallbackGaps = migrationSyntaxFallback.Gaps
            .Select(GetGapMessage)
            .ToArray();
        var semanticKnownGaps = git.KnownGaps
            .Concat(semanticResult.GapFacts.Select(GetGapMessage))
            .Concat(migrationFallbackGaps)
            .OrderBy(gap => gap, StringComparer.Ordinal)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var nugetLockfiles = ProjectFileReader.ReadNuGetLockfiles(repoPath, inventory);
        var nugetLockfileGaps = nugetLockfiles.Gaps
            .Select(gap => $"NuGet lockfile analysis reported `{gap.Category}`.")
            .OrderBy(gap => gap, StringComparer.Ordinal)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var nugetLockfileReducedCoverage = nugetLockfileGaps.Length > 0;
        var migrationFallbackReducedCoverage = migrationFallbackGaps.Length > 0;
        var semanticBuildReducedCoverage = semanticResult.GapFacts.Any(gap =>
            gap.RuleId != RuleIds.DatabaseFrameworkMigrationGap
            && gap.RuleId != RuleIds.CSharpRazorSemanticModelBindingGap);
        var semanticBuildStatus = semanticResult.Attempted
            ? semanticBuildReducedCoverage ? "FailedOrPartial" : "Succeeded"
            : "NotRun";
        var semanticAnalysisLevel = semanticResult.Attempted
            ? semanticToolchainReducedCoverage || migrationFallbackReducedCoverage || nugetLockfileReducedCoverage ? "Level1SemanticAnalysisReduced" : "Level1SemanticAnalysis"
            : migrationFallbackReducedCoverage || nugetLockfileReducedCoverage ? "Level3SyntaxAnalysisReduced" : "Level3SyntaxAnalysis";
        var provisionalBuildStatus = nugetLockfileReducedCoverage ? "FailedOrPartial" : semanticBuildStatus;
        var provisionalKnownGaps = semanticKnownGaps
            .Concat(nugetLockfileGaps)
            .OrderBy(gap => gap, StringComparer.Ordinal)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var provisionalManifest = new ScanManifest(
            CreateScanId(git, inventory, sourceSnapshotDigest, options),
            git.RepoName,
            git.RemoteUrl,
            git.Branch,
            git.CommitSha,
            ScannerVersions.TraceMap,
            DateTimeOffset.UtcNow,
            semanticAnalysisLevel,
            provisionalBuildStatus,
            solutions,
            projects,
            targetFrameworks,
            provisionalKnownGaps,
            GetScanRootRelativePath(repoPath, git),
            FactFactory.Hash(repoPath, 32),
            string.IsNullOrWhiteSpace(git.GitRootPath) ? null : FactFactory.Hash(Path.GetFullPath(git.GitRootPath), 32),
            sourceSnapshotDigest);

        var binlogFacts = MsBuildBinlogExtractor.Extract(repoPath, provisionalManifest, options.BinlogPaths);
        var binlogGaps = binlogFacts
            .Where(fact => fact.FactType == FactTypes.AnalysisGap)
            .Select(fact => $"MSBuild binlog analysis reported `{fact.Properties.GetValueOrDefault("gapKind") ?? "binlog-gap"}`.")
            .ToArray();
        var binlogRecordedFailure = binlogFacts.Any(fact =>
            fact.FactType == FactTypes.MsBuildBinlogObserved
            && fact.Properties.GetValueOrDefault("recordedBuildResult") == "failed");
        var binlogReducedCoverage = binlogRecordedFailure || binlogGaps.Length > 0;
        var knownGaps = provisionalKnownGaps
            .Concat(binlogGaps)
            .Concat(binlogRecordedFailure ? ["An explicitly supplied MSBuild binlog recorded a failed build."] : [])
            .OrderBy(gap => gap, StringComparer.Ordinal)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var manifest = provisionalManifest with
        {
            AnalysisLevel = binlogReducedCoverage && !semanticToolchainReducedCoverage
                ? semanticResult.Attempted ? "Level1SemanticAnalysisReduced" : "Level3SyntaxAnalysisReduced"
                : semanticAnalysisLevel,
            BuildStatus = binlogReducedCoverage || nugetLockfileReducedCoverage ? "FailedOrPartial" : semanticBuildStatus,
            KnownGaps = knownGaps
        };

        IReadOnlyList<CodeFact> facts;
        receiptRecorder?.Bind(manifest);
        using var extractionReceipt = receiptRecorder?.StartStage("static-extraction", "deterministic-fact-extraction", manifest.AnalysisLevel);
        try
        {
            using (var extractionOperation = TraceMapDiagnostics.StartPhase("scan", TraceMapDiagnosticPhases.StaticExtraction, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                facts = CreateFacts(
                    manifest,
                    inventory,
                    targetFrameworkInfos,
                    ProjectFileReader.ReadPackageReferences(repoPath, inventory),
                    nugetLockfiles,
                    knownGaps,
                    repoPath,
                    semanticResult,
                    options,
                    binlogFacts,
                    migrationSyntaxFallback,
                    progress,
                    cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                extractionOperation.RecordItems(facts.Count);
                extractionOperation.Complete(manifest.BuildStatus == "FailedOrPartial"
                    ? TraceMapDiagnosticOutcome.Partial
                    : TraceMapDiagnosticOutcome.Succeeded);
                extractionReceipt?.Complete(
                    manifest.BuildStatus == "FailedOrPartial" ? "partial" : "succeeded",
                    manifest.AnalysisLevel,
                    "facts-created",
                    retryability: manifest.BuildStatus == "FailedOrPartial" ? "retry-after-dependency-restoration" : "not-required",
                    nextAction: manifest.BuildStatus == "FailedOrPartial" ? "review-analysis-gaps" : "continue",
                    supportingFactIds: facts.Select(fact => fact.FactId),
                    supportingGapIds: facts.Where(fact => fact.FactType == FactTypes.AnalysisGap).Select(fact => fact.FactId));
            }
        }
        catch (Exception ex)
        {
            extractionReceipt?.Fail(ex, "stage-started");
            throw;
        }

        string verificationDigest;
        using var verificationReceipt = receiptRecorder?.StartStage("source-verification", "post-extraction-snapshot-verification", manifest.AnalysisLevel);
        try
        {
            progress?.StartStage(ScanProgressReporter.ScanOperation, ScanProgressStages.SourceVerification);
            var sourcePathComparer = CSharpSemanticExtractor.CreateSourcePathComparer(repoPath);
            var verificationFullInventory = FileInventory.Collect(
                repoPath,
                outputPath,
                options.ExcludeGlobs,
                sourcePathComparer,
                options.IncludeGlobs);
            var verificationInventory = ApplyScope(verificationFullInventory, repoPath, options);
            verificationInventory = IncludeSemanticallyAnalyzedFiles(
                verificationInventory,
                verificationFullInventory,
                semanticResult);
            var verificationSnapshotInventory = IncludeSemanticInputs(
                verificationInventory,
                verificationFullInventory,
                semanticResult);
            VerifySourceSnapshotInventory(authoritativeSnapshotInventory, verificationSnapshotInventory);
            verificationDigest = CreateSourceSnapshotDigest(repoPath, verificationSnapshotInventory);
            cancellationToken.ThrowIfCancellationRequested();
            if (!string.Equals(sourceSnapshotDigest, verificationDigest, StringComparison.Ordinal))
                throw new SourceSnapshotException();
            progress?.FinishStage(ScanProgressReporter.ScanOperation, ScanProgressStages.SourceVerification, "completed");
            verificationReceipt?.Complete("succeeded", manifest.AnalysisLevel, "source-snapshot-verified");
        }
        catch (SourceInventoryException ex)
        {
            var transformed = new SourceSnapshotException(ex);
            progress?.FailActiveStage(ScanProgressReporter.ScanOperation, "SOURCE_VERIFICATION_FAILED");
            verificationReceipt?.Fail(transformed, "stage-started");
            throw transformed;
        }
        catch (Exception ex)
        {
            if (ex is not OperationCanceledException)
            {
                progress?.FailActiveStage(ScanProgressReporter.ScanOperation, "SOURCE_VERIFICATION_FAILED");
            }

            verificationReceipt?.Fail(ex, "stage-started");
            throw;
        }
        cancellationToken.ThrowIfCancellationRequested();

        scanOperation.RecordItems(facts.Count);
        scanOperation.Complete(
            manifest.BuildStatus == "FailedOrPartial"
                ? TraceMapDiagnosticOutcome.Partial
                : TraceMapDiagnosticOutcome.Succeeded,
            manifest.AnalysisLevel,
            manifest.BuildStatus);
        var result = new ScanResult(manifest, facts, inventory);
        receiptRecorder?.Bind(result);
        return result;
    }

    private static string CreateScanId(
        GitMetadata git,
        IReadOnlyList<FileInventoryItem> inventory,
        string sourceSnapshotDigest,
        ScanOptions options)
    {
        var signature = string.Join('\n', inventory.Select(item => $"{item.RelativePath}|{item.Kind}|{item.SizeBytes}"));
        var binlogSignature = MsBuildBinlogExtractor.CreateInputSignature(options.BinlogPaths, repoPath: options.RepoPath);
        var optionSignature = string.Join('|',
            FrameValues(NormalizeOptionPaths(options.RepoPath, options.SolutionPaths)),
            FrameValues(NormalizeOptionPaths(options.RepoPath, options.ProjectPaths)),
            FrameValues((options.IncludeGlobs ?? []).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => NormalizePathForFileSystemComparison(value.Trim()))),
            FrameValues((options.ExcludeGlobs ?? []).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => NormalizePathForFileSystemComparison(value.Trim()))),
            FrameValues(string.IsNullOrWhiteSpace(options.TargetFramework) ? [] : [options.TargetFramework.Trim()]),
            $"restore={options.Restore.ToString().ToLowerInvariant()}",
            FrameValues(string.IsNullOrWhiteSpace(options.BinlogCommitSha) ? [] : [options.BinlogCommitSha.Trim()]));
        var repoIdentity = string.IsNullOrWhiteSpace(git.RemoteUrl) ? git.RepoName : git.RemoteUrl;
        return "scan-" + FactFactory.Hash($"{repoIdentity}|{git.CommitSha}|{sourceSnapshotDigest}|{signature}|{optionSignature}|{binlogSignature}", 20);
    }

    private static string FrameValues(IEnumerable<string> values)
    {
        var sorted = values.Order(StringComparer.Ordinal).ToArray();
        return $"{sorted.Length}:" + string.Concat(sorted.Select(value => $"{Encoding.UTF8.GetByteCount(value)}:{value}"));
    }

    private static IReadOnlyDictionary<string, long> ProjectSelectionCounts(IReadOnlyList<FileInventoryItem> inventory) =>
        new Dictionary<string, long>
        {
            ["solutions"] = inventory.Count(item => item.Kind == "Solution"),
            ["projects"] = inventory.Count(item => item.Kind is "Project" or "SqlProject")
        };

    internal static string CreateSourceSnapshotDigest(
        string repoPath,
        IReadOnlyList<FileInventoryItem> inventory,
        CancellationToken cancellationToken = default)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        Span<byte> lengthBuffer = stackalloc byte[sizeof(long)];
        var buffer = new byte[64 * 1024];

        try
        {
            foreach (var item in inventory.OrderBy(candidate => candidate.RelativePath, StringComparer.Ordinal))
            {
                cancellationToken.ThrowIfCancellationRequested();
                AppendString(hash, item.RelativePath, lengthBuffer);
                AppendString(hash, item.Kind, lengthBuffer);
                var path = Path.Combine(repoPath, item.RelativePath.Replace('/', Path.DirectorySeparatorChar));
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                if (stream.Length != item.SizeBytes)
                    throw new SourceSnapshotException();

                BinaryPrimitives.WriteInt64BigEndian(lengthBuffer, item.SizeBytes);
                hash.AppendData(lengthBuffer);

                long bytesRead = 0;
                int read;
                while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    hash.AppendData(buffer.AsSpan(0, read));
                    bytesRead += read;
                }

                if (bytesRead != item.SizeBytes || stream.Length != item.SizeBytes)
                    throw new SourceSnapshotException();
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new SourceInventoryException(ex);
        }

        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }

    internal static void VerifySourceSnapshotInventory(
        IReadOnlyList<FileInventoryItem> expected,
        IReadOnlyList<FileInventoryItem> observed)
    {
        var expectedItems = expected.OrderBy(item => item.RelativePath, StringComparer.Ordinal).ToArray();
        var observedItems = observed.OrderBy(item => item.RelativePath, StringComparer.Ordinal).ToArray();
        if (!expectedItems.SequenceEqual(observedItems))
            throw new SourceSnapshotException();
    }

    private static void VerifySourceSnapshotInventoryMembership(
        IReadOnlyList<FileInventoryItem> expected,
        IReadOnlyList<FileInventoryItem> observed)
    {
        var expectedItems = expected
            .Select(item => (item.RelativePath, item.Kind))
            .OrderBy(item => item.RelativePath, StringComparer.Ordinal)
            .ToArray();
        var observedItems = observed
            .Select(item => (item.RelativePath, item.Kind))
            .OrderBy(item => item.RelativePath, StringComparer.Ordinal)
            .ToArray();
        if (!expectedItems.SequenceEqual(observedItems))
            throw new SourceSnapshotException();
    }

    internal static IReadOnlyDictionary<string, string> CaptureSemanticInputSnapshot(
        string repoPath,
        IReadOnlyList<FileInventoryItem> inventory,
        CancellationToken cancellationToken = default)
    {
        return inventory
            .Where(item => FileInventory.IsCSharpKind(item.Kind) || IsSemanticMetadataKind(item.Kind))
            .ToDictionary(
                item => item.RelativePath,
                item => CreateSourceSnapshotDigest(repoPath, [item], cancellationToken),
                StringComparer.Ordinal);
    }

    internal static void VerifySemanticInputSnapshot(
        string repoPath,
        IReadOnlyList<FileInventoryItem> inventory,
        SemanticExtractionResult semanticResult,
        IReadOnlyDictionary<string, string> baseline,
        CancellationToken cancellationToken = default)
    {
        var protectedPaths = GetSemanticallyAnalyzedFiles(semanticResult)
            .Concat(semanticResult.CompilationInputFiles?.AsEnumerable() ?? Enumerable.Empty<string>())
            .Concat(inventory.Where(item => IsSemanticMetadataKind(item.Kind)).Select(item => item.RelativePath))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var itemsByPath = inventory.ToDictionary(item => item.RelativePath, StringComparer.Ordinal);
        try
        {
            foreach (var path in protectedPaths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (path.StartsWith("__external__/", StringComparison.Ordinal))
                    continue;

                if (!baseline.TryGetValue(path, out var expected)
                    || !itemsByPath.TryGetValue(path, out var item)
                    || !string.Equals(expected, CreateSourceSnapshotDigest(repoPath, [item], cancellationToken), StringComparison.Ordinal))
                {
                    throw new SourceSnapshotException();
                }
            }
        }
        catch (SourceInventoryException ex)
        {
            throw new SourceSnapshotException(ex);
        }
    }

    private static bool IsSemanticMetadataKind(string kind) =>
        kind is "Solution" or "Project" or "MSBuildProps" or "MSBuildTargets";

    private static IReadOnlyList<FileInventoryItem> IncludeSemanticInputs(
        IReadOnlyList<FileInventoryItem> inventory,
        IReadOnlyList<FileInventoryItem> fullInventory,
        SemanticExtractionResult semanticResult)
    {
        var compilationInputFiles = semanticResult.CompilationInputFiles ?? new HashSet<string>(StringComparer.Ordinal);
        return inventory
            .Concat(fullInventory.Where(item =>
                IsSemanticMetadataKind(item.Kind)
                || compilationInputFiles.Contains(item.RelativePath)))
            .GroupBy(item => item.RelativePath, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(item => item.RelativePath, StringComparer.Ordinal)
            .ToArray();
    }

    private static void AppendString(IncrementalHash hash, string value, Span<byte> lengthBuffer)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        BinaryPrimitives.WriteInt64BigEndian(lengthBuffer, bytes.Length);
        hash.AppendData(lengthBuffer);
        hash.AppendData(bytes);
    }

    private static string GetScanRootRelativePath(string repoPath, GitMetadata git)
    {
        if (string.IsNullOrWhiteSpace(git.GitRootPath))
        {
            return ".";
        }

        if (git.ScanRootRelativePath is not null)
        {
            var gitRelative = FileInventory.NormalizeRelativePath(git.ScanRootRelativePath);
            return gitRelative is "." or "" ? "." : gitRelative;
        }

        var relative = Path.GetRelativePath(git.GitRootPath, repoPath);
        var normalized = FileInventory.NormalizeRelativePath(relative);
        return normalized is "." or "" || normalized == ".." || normalized.StartsWith("../", StringComparison.Ordinal)
            ? "."
            : normalized;
    }

    private static IReadOnlyList<CodeFact> CreateFacts(
        ScanManifest manifest,
        IReadOnlyList<FileInventoryItem> inventory,
        IReadOnlyList<TargetFrameworkInfo> targetFrameworks,
        IReadOnlyList<PackageReferenceInfo> packageReferences,
        NuGetLockfileReadResult nugetLockfiles,
        IReadOnlyList<string> knownGaps,
        string repoPath,
        SemanticExtractionResult semanticResult,
        ScanOptions options,
        IReadOnlyList<CodeFact> binlogFacts,
        FrameworkMigrationEvidenceExtractor.SyntaxProtectionResult migrationSyntaxFallback,
        ScanProgressReporter? progress = null,
        CancellationToken cancellationToken = default)
    {
        var facts = new List<CodeFact>
        {
            FactFactory.Create(
                manifest,
                FactTypes.RepoScanned,
                RuleIds.RepoManifest,
                EvidenceTiers.Tier2Structural,
                new EvidenceSpan(".", 1, 1, null, "RepoManifestExtractor", ScannerVersions.RepoManifestExtractor),
                properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["fileCount"] = inventory.Count.ToString(),
                    ["projectCount"] = manifest.Projects.Count.ToString(),
                    ["solutionCount"] = manifest.Solutions.Count.ToString(),
                    ["scanScopeSolutions"] = string.Join(",", options.SolutionPaths ?? []),
                    ["scanScopeProjects"] = string.Join(",", options.ProjectPaths ?? []),
                    ["scanScopeIncludes"] = string.Join(",", options.IncludeGlobs ?? []),
                    ["scanScopeExcludes"] = string.Join(",", options.ExcludeGlobs ?? []),
                    ["targetFramework"] = options.TargetFramework ?? string.Empty,
                    ["restoreRequested"] = options.Restore.ToString()
                }),
            FactFactory.Create(
                manifest,
                FactTypes.BuildStatus,
                RuleIds.RepoManifest,
                EvidenceTiers.Tier4Unknown,
                new EvidenceSpan(".", 1, 1, null, "RepoManifestExtractor", ScannerVersions.RepoManifestExtractor),
                properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["status"] = manifest.BuildStatus,
                    ["reason"] = GetBuildStatusReason(manifest, semanticResult, binlogFacts)
                })
        };

        foreach (var gap in knownGaps)
        {
            facts.Add(FactFactory.Create(
                manifest,
                FactTypes.AnalysisGap,
                RuleIds.RepoManifest,
                EvidenceTiers.Tier4Unknown,
                new EvidenceSpan(".", 1, 1, null, "RepoManifestExtractor", ScannerVersions.RepoManifestExtractor),
                properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["message"] = gap
                }));
        }

        foreach (var item in inventory)
        {
            facts.Add(FactFactory.Create(
                manifest,
                FactTypes.FileInventoried,
                RuleIds.FileInventory,
                EvidenceTiers.Tier2Structural,
                new EvidenceSpan(item.RelativePath, 1, 1, null, "FileInventoryExtractor", ScannerVersions.FileInventoryExtractor),
                properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["kind"] = item.Kind,
                    ["sizeBytes"] = item.SizeBytes.ToString()
                }));

            if (item.Kind == "Solution")
            {
                facts.Add(FactFactory.Create(
                    manifest,
                    FactTypes.SolutionDeclared,
                    RuleIds.ProjectFile,
                    EvidenceTiers.Tier2Structural,
                    new EvidenceSpan(item.RelativePath, 1, 1, null, "ProjectFileExtractor", ScannerVersions.ProjectFileExtractor),
                    properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["path"] = item.RelativePath
                    }));
            }
            else if (item.Kind == "Project")
            {
                facts.Add(FactFactory.Create(
                    manifest,
                    FactTypes.ProjectDeclared,
                    RuleIds.ProjectFile,
                    EvidenceTiers.Tier2Structural,
                    new EvidenceSpan(item.RelativePath, 1, 1, null, "ProjectFileExtractor", ScannerVersions.ProjectFileExtractor),
                    projectPath: item.RelativePath,
                    properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["path"] = item.RelativePath
                    }));
            }
            else if (item.Kind == "Config" || item.Kind == "Json")
            {
                facts.Add(FactFactory.Create(
                    manifest,
                    FactTypes.ConfigFileDeclared,
                    RuleIds.FileInventory,
                    EvidenceTiers.Tier2Structural,
                    new EvidenceSpan(item.RelativePath, 1, 1, null, "FileInventoryExtractor", ScannerVersions.FileInventoryExtractor),
                    properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["path"] = item.RelativePath,
                        ["kind"] = item.Kind
                    }));
            }
            else if (item.Kind == "Sql")
            {
                facts.Add(FactFactory.Create(
                    manifest,
                    FactTypes.SqlFileDeclared,
                    RuleIds.FileInventory,
                    EvidenceTiers.Tier3SyntaxOrTextual,
                    new EvidenceSpan(item.RelativePath, 1, 1, null, "FileInventoryExtractor", ScannerVersions.FileInventoryExtractor),
                    properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["path"] = item.RelativePath
                    }));
            }
        }

        foreach (var item in targetFrameworks)
        {
            facts.Add(FactFactory.Create(
                manifest,
                FactTypes.TargetFrameworkDeclared,
                RuleIds.ProjectFile,
                EvidenceTiers.Tier2Structural,
                new EvidenceSpan(item.ProjectPath, item.Line, item.Line, null, "ProjectFileExtractor", ScannerVersions.ProjectFileExtractor),
                projectPath: item.ProjectPath,
                targetSymbol: item.TargetFramework,
                properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["targetFramework"] = item.TargetFramework
                }));
        }

        foreach (var item in packageReferences)
        {
            var packageProperties = new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["dependencyGroup"] = item.DependencyGroup,
                ["dependencyScope"] = item.DependencyScope,
                ["ecosystem"] = "nuget",
                ["manifestKind"] = item.ManifestKind,
                ["package"] = item.PackageName,
                ["packageManager"] = "nuget",
                ["packageName"] = item.PackageName,
                ["sourceKind"] = item.ManifestKind == "packages.config" ? "manifest" : "build-file",
                ["surfaceKind"] = "package-config",
                ["targetFramework"] = item.TargetFramework ?? string.Empty
            };
            AddSafeVersionProperties(packageProperties, item.Version);
            facts.Add(FactFactory.Create(
                manifest,
                FactTypes.PackageReferenced,
                RuleIds.ProjectFile,
                EvidenceTiers.Tier2Structural,
                new EvidenceSpan(item.ProjectPath, item.Line, item.Line, null, "ProjectFileExtractor", ScannerVersions.ProjectFileExtractor),
                projectPath: item.ProjectPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ? item.ProjectPath : null,
                targetSymbol: item.PackageName,
                properties: packageProperties));
        }

        foreach (var entry in nugetLockfiles.Entries)
        {
            // Lockfile rows carry resolved versions only; packages.lock.json has no registry artifact
            // digest (its contentHash is package-content metadata and is never emitted here).
            var lockfileProperties = new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["dependencyGroup"] = "lockfile",
                ["dependencyRelation"] = entry.DependencyRelation,
                ["ecosystem"] = "nuget",
                ["lockfileHash"] = entry.LockfileHash,
                ["lockfilePath"] = entry.LockfilePath,
                ["manifestKind"] = "packages.lock.json",
                ["package"] = entry.PackageName,
                ["packageManager"] = "nuget",
                ["packageName"] = entry.PackageName,
                ["sourceKind"] = "lockfile",
                ["surfaceKind"] = "package-config",
                ["targetFramework"] = entry.TargetFramework
            };
            if (entry.DependencyCount > 0)
            {
                lockfileProperties["dependencyCount"] = entry.DependencyCount.ToString();
                if (entry.DependencyNames is not null)
                {
                    lockfileProperties["dependencyNames"] = entry.DependencyNames;
                }
            }

            if (entry.ResolvedVersion is not null)
            {
                lockfileProperties["resolvedVersion"] = entry.ResolvedVersion;
                lockfileProperties["version"] = entry.ResolvedVersion;
            }
            else
            {
                lockfileProperties["redactionReason"] = "unsafe-package-version";
                if (!string.IsNullOrWhiteSpace(entry.ResolvedVersionHash))
                {
                    lockfileProperties["versionHash"] = $"version-hash:{entry.ResolvedVersionHash}";
                }
            }

            facts.Add(FactFactory.Create(
                manifest,
                FactTypes.PackageReferenced,
                RuleIds.ProjectFile,
                EvidenceTiers.Tier2Structural,
                new EvidenceSpan(entry.LockfilePath, entry.Line, entry.Line, null, "NuGetLockfileExtractor", ScannerVersions.NuGetLockfileExtractor),
                targetSymbol: entry.PackageName,
                properties: lockfileProperties));
        }

        foreach (var gap in nugetLockfiles.Gaps)
        {
            facts.Add(FactFactory.Create(
                manifest,
                FactTypes.AnalysisGap,
                RuleIds.ProjectFile,
                EvidenceTiers.Tier4Unknown,
                new EvidenceSpan(gap.LockfilePath, gap.Line, gap.Line, null, "NuGetLockfileExtractor", ScannerVersions.NuGetLockfileExtractor),
                properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["gapKind"] = gap.Category,
                    ["message"] = gap.Message
                }));
        }

        facts.AddRange(BuildEnvironmentDiagnosticExtractor.Extract(repoPath, manifest, inventory, semanticResult));
        var semanticallyAnalyzedFiles = GetSemanticallyAnalyzedFiles(semanticResult);
        var protectedSourceSpans = (semanticResult.ProtectedSourceSpans ?? [])
            .Concat(migrationSyntaxFallback.ProtectedSpans)
            .Distinct()
            .ToArray();
        var protectedLineRanges = BuildProtectedLineRanges(repoPath, protectedSourceSpans);
        facts.AddRange(CSharpSemanticExtractor.MaterializeFacts(manifest, migrationSyntaxFallback.Gaps));
        progress?.StartStage(ScanProgressReporter.ScanOperation, ScanProgressStages.SyntaxFallback);
        facts.AddRange(CSharpSyntaxExtractor.Extract(repoPath, manifest, inventory, protectedSourceSpans));
        progress?.FinishStage(ScanProgressReporter.ScanOperation, ScanProgressStages.SyntaxFallback, "completed");
        cancellationToken.ThrowIfCancellationRequested();
        progress?.StartStage(ScanProgressReporter.ScanOperation, ScanProgressStages.SpecializedExtraction);
        var extractorOrdinal = 0;
        IReadOnlyList<CodeFact> Observe(string extractor, Func<IReadOnlyList<CodeFact>> extract)
        {
            extractorOrdinal++;
            return progress?.ObserveExtractor(extractor, extractorOrdinal, inventory.Count, extract) ?? extract();
        }

        facts.AddRange(Observe(
            ScanPerformanceExtractors.CSharpIntegrationSyntax,
            () => CSharpIntegrationSyntaxExtractor.Extract(
                repoPath,
                manifest,
                inventory,
                semanticallyAnalyzedFiles,
                protectedSourceSpans)));
        facts.AddRange(Observe(
            ScanPerformanceExtractors.RazorBinding,
            () => FilterProtectedEvidence(RazorBindingExtractor.Extract(repoPath, manifest, inventory), protectedLineRanges).ToArray()));
        facts.AddRange(Observe(
            ScanPerformanceExtractors.LegacyWcf,
            () => FilterProtectedEvidence(LegacyWcfExtractor.Extract(repoPath, manifest, inventory), protectedLineRanges).ToArray()));
        facts.AddRange(Observe(
            ScanPerformanceExtractors.LegacyAsmx,
            () => FilterProtectedEvidence(LegacyAsmxExtractor.Extract(repoPath, manifest, inventory), protectedLineRanges).ToArray()));
        facts.AddRange(Observe(
            ScanPerformanceExtractors.LegacyRemoting,
            () => FilterProtectedEvidence(
                LegacyRemotingExtractor.Extract(repoPath, manifest, inventory, semanticResult.Facts, semanticResult.Attempted),
                protectedLineRanges).ToArray()));
        facts.AddRange(Observe(
            ScanPerformanceExtractors.SqlFile,
            () => SqlFileExtractor.Extract(repoPath, manifest, inventory)));
        facts.AddRange(Observe(
            ScanPerformanceExtractors.SqlExecutionContext,
            () => SqlExecutionContextExtractor.Extract(repoPath, manifest, inventory)));
        facts.AddRange(Observe(
            ScanPerformanceExtractors.PostgresSchemaMigration,
            () => PostgresSchemaMigrationExtractor.Extract(repoPath, manifest, inventory)));
        facts.AddRange(Observe(
            ScanPerformanceExtractors.SqlProjectRefactor,
            () => SqlProjectRefactorExtractor.Extract(
                repoPath,
                manifest,
                inventory,
                includeUnreferencedLogGaps: options.ProjectPaths is null || options.ProjectPaths.Count == 0)));
        facts.AddRange(binlogFacts);
        facts.AddRange(Observe(
            ScanPerformanceExtractors.Config,
            () => ConfigExtractor.Extract(repoPath, manifest, inventory)));
        facts.AddRange(CSharpSemanticExtractor.MaterializeFacts(manifest, semanticResult.GapFacts));
        facts.AddRange(CSharpSemanticExtractor.MaterializeFacts(manifest, semanticResult.Facts));
        facts.AddRange(Observe(
            ScanPerformanceExtractors.LegacyData,
            () => FilterProtectedEvidence(LegacyDataMetadataExtractor.Extract(repoPath, manifest, inventory, facts), protectedLineRanges).ToArray()));
        facts.AddRange(Observe(
            ScanPerformanceExtractors.LegacyDataSymbolComposition,
            () => FilterProtectedEvidence(
                LegacyDataEdmxSymbolComposition.Extract(manifest, inventory, facts, semanticallyAnalyzedFiles),
                protectedLineRanges).ToArray()));
        facts.AddRange(Observe(
            ScanPerformanceExtractors.LegacyWebForms,
            () => FilterProtectedEvidence(LegacyWebFormsExtractor.Extract(repoPath, manifest, inventory, facts), protectedLineRanges).ToArray()));
        facts.AddRange(Observe(
            ScanPerformanceExtractors.LegacyWinForms,
            () => FilterProtectedEvidence(LegacyWinFormsExtractor.Extract(repoPath, manifest, inventory, facts), protectedLineRanges).ToArray()));
        facts.AddRange(Observe(
            ScanPerformanceExtractors.LegacyAspNet,
            () => FilterProtectedEvidence(LegacyAspNetExtractor.Extract(repoPath, manifest, inventory, facts), protectedLineRanges).ToArray()));
        facts.AddRange(Observe(
            ScanPerformanceExtractors.LegacyBatchDataMovement,
            () => FilterProtectedEvidence(
                LegacyBatchDataMovementExtractor.Extract(repoPath, manifest, inventory, facts),
                protectedLineRanges).ToArray()));
        var diagnosticSemanticResult = semanticResult with
        {
            GapFacts = semanticResult.GapFacts.Where(gap => !IsProducerLocalSemanticGap(gap)).ToArray(),
            ReducedCoverage = HasToolchainSemanticReduction(semanticResult)
        };
        facts.AddRange(Observe(
            ScanPerformanceExtractors.AnalyzerCapability,
            () => AnalyzerCapabilityDiagnosticExtractor.Extract(manifest, inventory, diagnosticSemanticResult, facts, options)));
        progress?.FinishStage(
            ScanProgressReporter.ScanOperation,
            ScanProgressStages.SpecializedExtraction,
            "completed",
            counts: new Dictionary<string, long> { ["facts"] = facts.Count });
        cancellationToken.ThrowIfCancellationRequested();

        return facts
            .GroupBy(fact => fact.FactId, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(fact => fact.FactType, StringComparer.Ordinal)
            .ThenBy(fact => fact.Evidence.FilePath, StringComparer.Ordinal)
            .ThenBy(fact => fact.Evidence.StartLine)
            .ThenBy(fact => fact.TargetSymbol, StringComparer.Ordinal)
            .ThenBy(fact => fact.FactId, StringComparer.Ordinal)
            .ToArray();
    }

    internal static IReadOnlySet<string> GetSemanticallyAnalyzedFiles(SemanticExtractionResult semanticResult) =>
        semanticResult.AnalyzedFiles is not null
            ? new HashSet<string>(semanticResult.AnalyzedFiles, StringComparer.Ordinal)
            : semanticResult.Facts
                .Select(candidate => candidate.Evidence.FilePath)
                .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                .ToHashSet(StringComparer.Ordinal);

    internal static IReadOnlyDictionary<string, IReadOnlyList<(int StartLine, int EndLine)>> BuildProtectedLineRanges(
        string repoPath,
        IReadOnlyList<ProtectedSourceSpan> protectedSourceSpans)
    {
        var result = new Dictionary<string, IReadOnlyList<(int StartLine, int EndLine)>>(StringComparer.Ordinal);
        foreach (var group in protectedSourceSpans.GroupBy(span => span.FilePath, StringComparer.Ordinal))
        {
            var fullPath = Path.Combine(repoPath, group.Key);
            if (!File.Exists(fullPath))
            {
                result[group.Key] = [(1, int.MaxValue)];
                continue;
            }

            try
            {
                var source = SourceText.From(File.ReadAllText(fullPath));
                result[group.Key] = group.Select(span =>
                {
                    var start = Math.Clamp(span.Start, 0, source.Length);
                    var length = Math.Clamp(span.Length, 0, source.Length - start);
                    var lineSpan = source.Lines.GetLinePositionSpan(new TextSpan(start, length));
                    return (lineSpan.Start.Line + 1, lineSpan.End.Line + 1);
                }).ToArray();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                result[group.Key] = [(1, int.MaxValue)];
            }
        }
        return result;
    }

    internal static IEnumerable<CodeFact> FilterProtectedEvidence(
        IEnumerable<CodeFact> facts,
        IReadOnlyDictionary<string, IReadOnlyList<(int StartLine, int EndLine)>> protectedLineRanges) =>
        facts.Where(fact => !protectedLineRanges.TryGetValue(fact.Evidence.FilePath, out var ranges)
            || !ranges.Any(range => fact.Evidence.StartLine <= range.EndLine && range.StartLine <= fact.Evidence.EndLine));

    private static string GetGapMessage(SemanticFactCandidate gap)
    {
        if (gap.Properties is not null && gap.Properties.TryGetValue("message", out var message))
        {
            return message;
        }
        if (gap.RuleId == RuleIds.DatabaseFrameworkMigrationGap)
        {
            var gapKind = gap.Properties?.GetValueOrDefault("gapKind") ?? gap.ContractElement ?? "UnknownMigrationGap";
            return $"Framework migration coverage reduced: {gapKind}.";
        }
        if (gap.RuleId == RuleIds.CSharpRazorSemanticModelBindingGap)
        {
            var gapKind = gap.Properties?.GetValueOrDefault("gapKind") ?? gap.ContractElement ?? "UnknownRazorModelBindingGap";
            return $"Semantic Razor model-binding coverage reduced: {gapKind}.";
        }
        return "Roslyn semantic analysis reported a gap.";
    }

    private static bool HasToolchainSemanticReduction(SemanticExtractionResult result) =>
        result.GapFacts.Any(gap => !IsProducerLocalSemanticGap(gap));

    private static bool IsProducerLocalSemanticGap(SemanticFactCandidate gap) =>
        gap.RuleId == RuleIds.CSharpRazorSemanticModelBindingGap;

    private static string GetBuildStatusReason(
        ScanManifest manifest,
        SemanticExtractionResult semanticResult,
        IReadOnlyList<CodeFact> binlogFacts)
    {
        if (manifest.BuildStatus == "Succeeded")
        {
            return "MSBuildWorkspace loaded projects and Roslyn compilation reported no errors.";
        }

        if (manifest.BuildStatus != "FailedOrPartial")
        {
            return "No C# project was available for MSBuildWorkspace semantic analysis.";
        }

        var hasBinlogGap = binlogFacts.Any(fact =>
            fact.FactType == FactTypes.AnalysisGap
            || fact.FactType == FactTypes.MsBuildBinlogObserved
                && fact.Properties.GetValueOrDefault("recordedBuildResult") == "failed");
        var scopeOnlyReduction = semanticResult.ScopeReduced
            && semanticResult.GapFacts.All(gap => gap.Properties?.GetValueOrDefault("diagnosticKind")
                == BuildEnvironmentDiagnosticExtractor.DiagnosticKindScanScope);
        if (scopeOnlyReduction && !hasBinlogGap)
        {
            return "Configured scan scope omitted C# source evidence; semantic coverage is partial without claiming an MSBuildWorkspace load failure.";
        }

        return "MSBuildWorkspace project load or Roslyn compilation reported gaps; syntax fallback still ran.";
    }

    private static void AddSafeVersionProperties(SortedDictionary<string, string> properties, string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            properties["version"] = string.Empty;
            return;
        }

        var trimmed = version.Trim();
        if (IsUnsafePackageVersion(trimmed))
        {
            properties["versionHash"] = FactFactory.Hash(trimmed, 32);
            properties["redactionReason"] = "unsafe-package-version";
            return;
        }

        properties["version"] = trimmed;
    }

    private static bool IsUnsafePackageVersion(string value)
    {
        return value.Contains("://", StringComparison.Ordinal)
            || value.Contains("\\", StringComparison.Ordinal)
            || value.StartsWith("/", StringComparison.Ordinal)
            || value.StartsWith("./", StringComparison.Ordinal)
            || value.StartsWith("../", StringComparison.Ordinal)
            || value.StartsWith("file:", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("git+", StringComparison.OrdinalIgnoreCase)
            || value.Contains("${", StringComparison.Ordinal)
            || value.Contains("$(", StringComparison.Ordinal)
            || value.Contains("%", StringComparison.Ordinal);
    }

    private static IReadOnlyList<FileInventoryItem> ApplyScope(
        IReadOnlyList<FileInventoryItem> inventory,
        string repoPath,
        ScanOptions options)
    {
        var solutionPaths = NormalizeOptionPaths(repoPath, options.SolutionPaths);
        var projectPaths = NormalizeOptionPaths(repoPath, options.ProjectPaths);
        var includeGlobs = (options.IncludeGlobs ?? []).Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
        var excludeGlobs = (options.ExcludeGlobs ?? []).Where(value => !string.IsNullOrWhiteSpace(value)).ToArray();
        var sourcePathComparer = CSharpSemanticExtractor.CreateSourcePathComparer(repoPath);
        var projectDirectories = projectPaths
            .Select(path => FileInventory.NormalizeRelativePath(Path.GetDirectoryName(path) ?? "."))
            .ToArray();

        return inventory
            .Where(item => includeGlobs.Length == 0 || includeGlobs.Any(glob => GlobMatches(item.RelativePath, glob, sourcePathComparer)))
            .Where(item => excludeGlobs.Length == 0 || !excludeGlobs.Any(glob => GlobMatches(item.RelativePath, glob, sourcePathComparer)))
            .Where(item => solutionPaths.Count == 0 || item.Kind != "Solution" || solutionPaths.Contains(item.RelativePath))
            .Where(item => projectPaths.Count == 0 || item.Kind is not ("Project" or "SqlProject") || projectPaths.Contains(item.RelativePath))
            .Where(item => projectDirectories.Length == 0
                || item.Kind is "Solution"
                || projectPaths.Contains(item.RelativePath)
                || projectDirectories.Any(directory => IsUnderScopedDirectory(item.RelativePath, directory)))
            .OrderBy(item => item.RelativePath, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<FileInventoryItem> IncludeSemanticallyAnalyzedFiles(
        IReadOnlyList<FileInventoryItem> scopedInventory,
        IReadOnlyList<FileInventoryItem> fullInventory,
        SemanticExtractionResult semanticResult)
    {
        if (semanticResult.AnalyzedFiles is null || semanticResult.AnalyzedFiles.Count == 0)
        {
            return scopedInventory;
        }

        var includedPaths = scopedInventory
            .Select(item => item.RelativePath)
            .ToHashSet(StringComparer.Ordinal);
        return scopedInventory
            .Concat(fullInventory.Where(item =>
                semanticResult.AnalyzedFiles.Contains(item.RelativePath)
                && includedPaths.Add(item.RelativePath)))
            .OrderBy(item => item.RelativePath, StringComparer.Ordinal)
            .ToArray();
    }

    private static HashSet<string> NormalizeOptionPaths(string repoPath, IReadOnlyList<string>? paths)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var path in paths ?? [])
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                continue;
            }

            var normalized = Path.IsPathRooted(path)
                ? Path.GetRelativePath(repoPath, Path.GetFullPath(path))
                : path;
            result.Add(FileInventory.NormalizeRelativePath(normalized));
        }

        return result;
    }

    private static bool IsUnderScopedDirectory(string relativePath, string directory)
    {
        if (directory is "." or "")
        {
            return true;
        }

        var normalizedDirectory = directory.TrimEnd('/') + "/";
        return relativePath.StartsWith(normalizedDirectory, StringComparison.Ordinal);
    }

    internal static bool GlobMatches(string relativePath, string glob, StringComparer? pathComparer = null)
    {
        pathComparer ??= StringComparer.Ordinal;
        var normalizedPath = NormalizePathForFileSystemComparison(relativePath);
        var normalizedGlob = NormalizePathForFileSystemComparison(glob.Trim());
        if (string.IsNullOrWhiteSpace(normalizedGlob))
        {
            return false;
        }

        if (!normalizedGlob.Contains('*', StringComparison.Ordinal))
        {
            var directoryPrefix = normalizedGlob.TrimEnd('/') + "/";
            var comparison = pathComparer.Equals("a", "A")
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            return normalizedPath.Equals(normalizedGlob, comparison)
                || normalizedPath.StartsWith(directoryPrefix, comparison);
        }

        var regex = "^" + System.Text.RegularExpressions.Regex.Escape(normalizedGlob)
            .Replace("\\*\\*/", "(?:.*/)?", StringComparison.Ordinal)
            .Replace("\\*\\*", ".*", StringComparison.Ordinal)
            .Replace("\\*", "[^/]*", StringComparison.Ordinal) + "$";
        var regexOptions = System.Text.RegularExpressions.RegexOptions.CultureInvariant;
        if (pathComparer.Equals("a", "A"))
            regexOptions |= System.Text.RegularExpressions.RegexOptions.IgnoreCase;
        return System.Text.RegularExpressions.Regex.IsMatch(normalizedPath, regex, regexOptions);
    }

    private static string NormalizePathForFileSystemComparison(string path)
    {
        var normalized = FileInventory.NormalizeRelativePath(path);
        return OperatingSystem.IsMacOS()
            ? normalized.Normalize(NormalizationForm.FormC)
            : normalized;
    }
}
