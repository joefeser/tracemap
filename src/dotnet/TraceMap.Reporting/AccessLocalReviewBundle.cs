using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TraceMap.Reporting;

public sealed record AccessLocalReviewBundleOptions(
    string ScanOutputPath,
    string OutputPath,
    bool Force = false);

public sealed record AccessLocalReviewBundleResult(
    AccessLocalReviewBundleManifest Manifest,
    IReadOnlyList<string> WrittenFiles);

public sealed record AccessLocalReviewBundleManifest(
    string SchemaVersion,
    bool TracemapGenerated,
    AccessLocalReviewBundleGenerator Generator,
    string ClaimLevel,
    string? CommitSha,
    string SourceCoverage,
    string AccessEvidenceStatus,
    AccessLocalReviewBundleCounts Counts,
    IReadOnlyList<AccessLocalReviewBundleFile> Files,
    IReadOnlyList<string> Limitations);

public sealed record AccessLocalReviewBundleGenerator(
    string Name,
    string Version);

public sealed record AccessLocalReviewBundleCounts(
    int AccessFindingCount,
    int AccessGapCount,
    int ExplorerEvidenceRowCount,
    int ExplorerGapCount);

public sealed record AccessLocalReviewBundleFile(
    string Path,
    string Sha256,
    long SizeBytes);

public static class AccessLocalReviewBundle
{
    public const string SchemaVersion = "tracemap-access-local-review-bundle.v1";
    public const string GeneratorName = "tracemap-access-local-review-bundle";
    public const string GeneratorVersion = "1.0.0";
    public const string ManifestFileName = "access-review-manifest.json";

    private static readonly string[] RequiredArtifacts =
    [
        "scan-manifest.json",
        "facts.ndjson",
        "index.sqlite",
        "report.md",
        "logs/analyzer.log"
    ];

    private static readonly IReadOnlyList<string> Limitations =
    [
        "This local bundle contains bounded static Microsoft Access design evidence only.",
        "It does not prove row contents, execution, runtime reachability, effective permissions, production state, correctness, compatibility, operational safety, release approval, or DBA approval.",
        "Form/report, VBA-module, and macro evidence remains count-only; identities, source, bodies, expressions, and runtime behavior are unavailable.",
        "Raw SQL, connections, credentials, private object names, captions, expressions, VBA, macro bodies, infrastructure identities, and local paths are omitted."
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static async Task<AccessLocalReviewBundleResult> CreateAsync(
        AccessLocalReviewBundleOptions options,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.ScanOutputPath))
        {
            throw new ArgumentException("access-review create requires --scan-output <access-scan-directory>.");
        }

        if (string.IsNullOrWhiteSpace(options.OutputPath))
        {
            throw new ArgumentException("access-review create requires --out <bundle-directory>.");
        }

        var inputDirectory = NormalizeDirectory(options.ScanOutputPath);
        var outputDirectory = NormalizeDirectory(options.OutputPath);
        if (!Directory.Exists(inputDirectory))
        {
            throw new DirectoryNotFoundException("AccessReviewInputUnavailable: scan output directory was not found.");
        }

        ValidateNoOverlap(inputDirectory, outputDirectory);
        ValidateRequiredArtifacts(inputDirectory);
        ValidateExistingOutput(outputDirectory, options.Force);

        var outputParent = Path.GetDirectoryName(outputDirectory)
            ?? throw new InvalidOperationException("AccessReviewOutputInvalid: output directory requires a parent.");
        Directory.CreateDirectory(outputParent);
        var stagingDirectory = Path.Combine(
            outputParent,
            $".{Path.GetFileName(outputDirectory)}.access-review-{Guid.NewGuid():N}");
        var backupDirectory = Path.Combine(
            outputParent,
            $".{Path.GetFileName(outputDirectory)}.access-review-backup-{Guid.NewGuid():N}");

        try
        {
            Directory.CreateDirectory(stagingDirectory);
            var indexPath = Path.Combine(inputDirectory, "index.sqlite");
            var releaseReview = await ReleaseReviewReporter.WriteAsync(
                new ReleaseReviewOptions(
                    indexPath,
                    indexPath,
                    Path.Combine(stagingDirectory, "release-review"),
                    Scope: "access-evidence"),
                cancellationToken);
            if (releaseReview.Report.AccessEvidence.Status is not ReleaseReviewStatuses.Available
                and not ReleaseReviewStatuses.Truncated)
            {
                throw new InvalidOperationException(
                    "AccessEvidenceUnavailable: compatible Microsoft Access evidence was not found in the scan index.");
            }

            var explorer = await StaticHtmlEvidenceExplorer.GenerateAsync(
                new StaticHtmlEvidenceExplorerOptions(
                    inputDirectory,
                    Path.Combine(stagingDirectory, "explorer"),
                    "hidden-local"),
                cancellationToken);

            var readme = RenderReadme(releaseReview.Report, explorer.Manifest);
            await File.WriteAllTextAsync(
                Path.Combine(stagingDirectory, "README.md"),
                readme,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                cancellationToken);

            EnsureGeneratedTextIsSafe(stagingDirectory, inputDirectory, outputDirectory);
            var files = await HashFilesAsync(stagingDirectory, cancellationToken);
            var manifest = BuildManifest(releaseReview.Report, explorer, files);
            await File.WriteAllTextAsync(
                Path.Combine(stagingDirectory, ManifestFileName),
                JsonSerializer.Serialize(manifest, JsonOptions) + "\n",
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                cancellationToken);
            EnsureGeneratedTextIsSafe(stagingDirectory, inputDirectory, outputDirectory);

            Publish(stagingDirectory, outputDirectory, backupDirectory);
            var writtenFiles = Directory.EnumerateFiles(outputDirectory, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(outputDirectory, path).Replace('\\', '/'))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            return new AccessLocalReviewBundleResult(manifest, writtenFiles);
        }
        finally
        {
            DeleteDirectoryIfPresent(stagingDirectory);
            TryDeleteDirectoryIfPresent(backupDirectory);
        }
    }

    private static AccessLocalReviewBundleManifest BuildManifest(
        ReleaseReviewDocument releaseReview,
        StaticHtmlEvidenceExplorerResult explorer,
        IReadOnlyList<AccessLocalReviewBundleFile> files)
    {
        var commitShas = releaseReview.AfterSnapshot.Sources
            .Select(source => source.CommitSha)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return new AccessLocalReviewBundleManifest(
            SchemaVersion,
            true,
            new AccessLocalReviewBundleGenerator(GeneratorName, GeneratorVersion),
            "hidden",
            commitShas.Length == 1 ? commitShas[0] : null,
            releaseReview.AfterSnapshot.ReportCoverage,
            releaseReview.AccessEvidence.Status,
            new AccessLocalReviewBundleCounts(
                releaseReview.AccessEvidence.Findings.Count,
                releaseReview.AccessEvidence.Gaps.Count,
                explorer.Manifest.Counts.EvidenceRowCount,
                explorer.Manifest.Counts.GapCount),
            files,
            Limitations);
    }

    private static string RenderReadme(
        ReleaseReviewDocument releaseReview,
        ExplorerManifest explorer)
    {
        var access = releaseReview.AccessEvidence;
        var builder = new StringBuilder();
        builder.AppendLine("# TraceMap Local Microsoft Access Review");
        builder.AppendLine();
        builder.AppendLine("Open [the offline evidence explorer](explorer/index.html) for local navigation.");
        builder.AppendLine();
        builder.AppendLine("Read [the Access design evidence packet](release-review/release-review.md) for the bounded reviewer view.");
        builder.AppendLine();
        builder.AppendLine("## Coverage");
        builder.AppendLine();
        builder.AppendLine($"- Access evidence status: `{access.Status}`");
        builder.AppendLine($"- Access evidence findings: {access.Findings.Count}");
        builder.AppendLine($"- Access evidence gaps: {access.Gaps.Count}");
        builder.AppendLine($"- Explorer coverage: `{explorer.CoverageStatus}`");
        builder.AppendLine($"- Explorer safety profile: `{explorer.SafetyProfile}`");
        builder.AppendLine();
        builder.AppendLine("## Review boundary");
        builder.AppendLine();
        foreach (var limitation in Limitations)
        {
            builder.Append("- ").AppendLine(limitation);
        }

        builder.AppendLine();
        builder.AppendLine("Keep this bundle and its source scan local unless a separate evidence-promotion review explicitly authorizes publication.");
        return builder.ToString().ReplaceLineEndings("\n");
    }

    private static void ValidateRequiredArtifacts(string inputDirectory)
    {
        foreach (var relativePath in RequiredArtifacts)
        {
            var path = Path.Combine(inputDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path))
            {
                throw new InvalidOperationException(
                    $"AccessReviewInputIncomplete: required generated artifact is missing: {relativePath}.");
            }

            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            {
                throw new InvalidOperationException(
                    $"AccessReviewInputInvalid: generated artifact cannot be a reparse point: {relativePath}.");
            }
        }
    }

    private static void ValidateExistingOutput(string outputDirectory, bool force)
    {
        if (File.Exists(outputDirectory))
        {
            throw new InvalidOperationException("AccessReviewOutputCollision: output path is an existing file.");
        }

        if (!Directory.Exists(outputDirectory))
        {
            return;
        }

        if ((File.GetAttributes(outputDirectory) & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidOperationException("AccessReviewOutputCollision: output directory cannot be a reparse point.");
        }

        if (!force)
        {
            throw new InvalidOperationException(
                "AccessReviewOutputExists: use --force only for an existing TraceMap-generated Access review bundle.");
        }

        if (!IsRecognizedGeneratedBundle(outputDirectory))
        {
            throw new InvalidOperationException(
                "AccessReviewOutputCollision: refusing to replace an unrecognized caller-owned directory.");
        }
    }

    private static void ValidateNoOverlap(string inputDirectory, string outputDirectory)
    {
        if (IsEqualOrDescendant(inputDirectory, outputDirectory)
            || IsEqualOrDescendant(outputDirectory, inputDirectory))
        {
            throw new InvalidOperationException(
                "AccessReviewPathOverlap: scan input and bundle output must not overlap.");
        }
    }

    private static bool IsEqualOrDescendant(string parent, string candidate)
    {
        if (string.Equals(parent, candidate, PathComparison))
        {
            return true;
        }

        var relative = Path.GetRelativePath(parent, candidate);
        return relative != ".."
            && !relative.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            && !Path.IsPathRooted(relative);
    }

    private static async Task<IReadOnlyList<AccessLocalReviewBundleFile>> HashFilesAsync(
        string root,
        CancellationToken cancellationToken)
    {
        var files = new List<AccessLocalReviewBundleFile>();
        foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                     .Where(path => !string.Equals(Path.GetFileName(path), ManifestFileName, StringComparison.Ordinal))
                     .OrderBy(path => Path.GetRelativePath(root, path).Replace('\\', '/'), StringComparer.Ordinal))
        {
            await using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                64 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            var hash = await SHA256.HashDataAsync(stream, cancellationToken);
            files.Add(new AccessLocalReviewBundleFile(
                Path.GetRelativePath(root, path).Replace('\\', '/'),
                Convert.ToHexStringLower(hash),
                stream.Length));
        }

        return files;
    }

    private static void EnsureGeneratedTextIsSafe(
        string root,
        string inputDirectory,
        string outputDirectory)
    {
        var prohibited = new[]
        {
            inputDirectory,
            outputDirectory,
            "/Users/",
            "/home/",
            "/private/",
            "\\Users\\"
        };
        foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var content = File.ReadAllText(path);
            if (prohibited.Any(value => content.Contains(value, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException(
                    "AccessReviewUnsafeOutput: generated bundle contains a prohibited local path.");
            }
        }
    }

    private static bool IsRecognizedGeneratedBundle(string outputDirectory)
    {
        try
        {
            var manifestPath = Path.Combine(outputDirectory, ManifestFileName);
            var manifest = JsonSerializer.Deserialize<AccessLocalReviewBundleManifest>(
                File.ReadAllText(manifestPath),
                JsonOptions);
            if (manifest is null
                || manifest.SchemaVersion != SchemaVersion
                || !manifest.TracemapGenerated
                || manifest.Generator.Name != GeneratorName)
            {
                return false;
            }

            var expectedPaths = manifest.Files
                .Select(file => file.Path)
                .Append(ManifestFileName)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            if (manifest.Files.Any(file => !IsSafeRelativePath(file.Path)))
            {
                return false;
            }

            var actualPaths = Directory.EnumerateFiles(outputDirectory, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(outputDirectory, path).Replace('\\', '/'))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            if (!expectedPaths.SequenceEqual(actualPaths, StringComparer.Ordinal))
            {
                return false;
            }

            foreach (var file in manifest.Files)
            {
                var fullPath = Path.Combine(outputDirectory, file.Path.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(fullPath)
                    || (File.GetAttributes(fullPath) & FileAttributes.ReparsePoint) != 0
                    || new FileInfo(fullPath).Length != file.SizeBytes)
                {
                    return false;
                }

                using var stream = File.OpenRead(fullPath);
                if (!string.Equals(
                        Convert.ToHexStringLower(SHA256.HashData(stream)),
                        file.Sha256,
                        StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsSafeRelativePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)
            || Path.IsPathRooted(path)
            || path.Contains('\\', StringComparison.Ordinal))
        {
            return false;
        }

        var segments = path.Split('/');
        return segments.All(segment => segment.Length > 0 && segment is not "." and not "..");
    }

    private static void Publish(
        string stagingDirectory,
        string outputDirectory,
        string backupDirectory)
    {
        var existing = Directory.Exists(outputDirectory);
        if (existing)
        {
            Directory.Move(outputDirectory, backupDirectory);
        }

        try
        {
            Directory.Move(stagingDirectory, outputDirectory);
        }
        catch
        {
            if (existing && !Directory.Exists(outputDirectory) && Directory.Exists(backupDirectory))
            {
                Directory.Move(backupDirectory, outputDirectory);
            }

            throw;
        }
    }

    private static void DeleteDirectoryIfPresent(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static void TryDeleteDirectoryIfPresent(string path)
    {
        try
        {
            DeleteDirectoryIfPresent(path);
        }
        catch (IOException)
        {
            // The published bundle is complete. A stale backup is safer than
            // reporting an ambiguous failure after the atomic directory move.
        }
        catch (UnauthorizedAccessException)
        {
            // Preserve the completed output when cleanup permissions changed.
        }
    }

    private static string NormalizeDirectory(string path)
    {
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
    }

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
}
