using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using TraceMap.Core;

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

public sealed class AccessLocalReviewException : Exception
{
    public AccessLocalReviewException(string code, string message)
        : base($"{code}: {message}")
    {
        Code = code;
    }

    public string Code { get; }
}

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

    private static readonly Regex MachineLocalPathPattern = new(
        """(?ix)(?<![A-Za-z0-9._~-])/(?:Users|home|private|var|tmp|Volumes|workspace|root|mnt)/|(?<![A-Za-z0-9])(?:[A-Za-z]:[\\/]|\\\\[^\\/\s]+[\\/])""",
        RegexOptions.CultureInvariant);

    public static async Task<AccessLocalReviewBundleResult> CreateAsync(
        AccessLocalReviewBundleOptions options,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await CreateCoreAsync(options, cancellationToken);
        }
        catch (AccessLocalReviewException)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            throw new AccessLocalReviewException(
                "AccessReviewFailed",
                "local bundle composition failed without exposing operator-local details.");
        }
    }

    private static async Task<AccessLocalReviewBundleResult> CreateCoreAsync(
        AccessLocalReviewBundleOptions options,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.ScanOutputPath))
        {
            throw new AccessLocalReviewException(
                "AccessReviewInputRequired",
                "access-review create requires --scan-output <access-scan-directory>.");
        }

        if (string.IsNullOrWhiteSpace(options.OutputPath))
        {
            throw new AccessLocalReviewException(
                "AccessReviewOutputRequired",
                "access-review create requires --out <bundle-directory>.");
        }

        var inputDirectory = NormalizeDirectory(options.ScanOutputPath);
        var outputDirectory = NormalizeDirectory(options.OutputPath);
        if (!Directory.Exists(inputDirectory))
        {
            throw new AccessLocalReviewException(
                "AccessReviewInputUnavailable",
                "scan output directory was not found.");
        }

        RejectExistingReparsePath(inputDirectory, "AccessReviewInputInvalid");
        RejectExistingReparsePath(outputDirectory, "AccessReviewOutputInvalid");
        ValidateNoOverlap(inputDirectory, outputDirectory);
        ValidateRequiredArtifacts(inputDirectory);
        await ValidateScanConsistencyAsync(inputDirectory, cancellationToken);
        ValidateExistingOutput(outputDirectory, options.Force);

        var outputParent = Path.GetDirectoryName(outputDirectory)
            ?? throw new AccessLocalReviewException(
                "AccessReviewOutputInvalid",
                "output directory requires a parent.");
        Directory.CreateDirectory(outputParent);
        var stagingDirectory = Path.Combine(
            outputParent,
            $".{Path.GetFileName(outputDirectory)}.access-review-{Guid.NewGuid():N}");
        var backupDirectory = Path.Combine(
            outputParent,
            $".{Path.GetFileName(outputDirectory)}.access-review-backup-{Guid.NewGuid():N}");

        var publicationSucceeded = false;
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
                throw new AccessLocalReviewException(
                    "AccessEvidenceUnavailable",
                    "compatible Microsoft Access evidence was not found in the scan index.");
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

            var writtenFiles = Directory.EnumerateFiles(stagingDirectory, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(stagingDirectory, path).Replace('\\', '/'))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
            Publish(stagingDirectory, outputDirectory, backupDirectory);
            publicationSucceeded = true;
            return new AccessLocalReviewBundleResult(manifest, writtenFiles);
        }
        finally
        {
            DeleteDirectoryIfPresent(stagingDirectory);
            if (publicationSucceeded)
            {
                TryDeleteDirectoryIfPresent(backupDirectory);
            }
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

    private static async Task ValidateScanConsistencyAsync(
        string inputDirectory,
        CancellationToken cancellationToken)
    {
        var manifestPath = Path.Combine(inputDirectory, "scan-manifest.json");
        ScanManifest fileManifest;
        try
        {
            await using var manifestStream = File.OpenRead(manifestPath);
            fileManifest = await JsonSerializer.DeserializeAsync<ScanManifest>(
                    manifestStream,
                    JsonOptions,
                    cancellationToken)
                ?? throw new JsonException();
        }
        catch (JsonException)
        {
            throw new AccessLocalReviewException(
                "AccessReviewInputInvalid",
                "scan manifest is not valid TraceMap JSON.");
        }

        var factIds = new HashSet<string>(StringComparer.Ordinal);
        var factsPath = Path.Combine(inputDirectory, "facts.ndjson");
        try
        {
            await foreach (var line in File.ReadLinesAsync(factsPath, cancellationToken))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var fact = JsonSerializer.Deserialize<CodeFact>(line, JsonOptions)
                    ?? throw new JsonException();
                if (!SameScanIdentity(fileManifest, fact.ScanId, fact.Repo, fact.CommitSha)
                    || string.IsNullOrWhiteSpace(fact.FactId)
                    || !factIds.Add(fact.FactId))
                {
                    throw new AccessLocalReviewException(
                        "AccessReviewInputMismatch",
                        "facts do not belong to one unique manifest scan.");
                }
            }
        }
        catch (JsonException)
        {
            throw new AccessLocalReviewException(
                "AccessReviewInputInvalid",
                "facts artifact is not valid TraceMap NDJSON.");
        }

        var indexPath = Path.Combine(inputDirectory, "index.sqlite");
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = indexPath,
            Mode = SqliteOpenMode.ReadOnly
        }.ToString();
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using (var manifestCommand = connection.CreateCommand())
        {
            manifestCommand.CommandText = """
                select scan_id, repo, commit_sha, manifest_json
                from scan_manifest
                order by scan_id;
                """;
            await using var reader = await manifestCommand.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken)
                || !SameScanIdentity(
                    fileManifest,
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2))
                || !IndexManifestMatches(fileManifest, reader.GetString(3))
                || await reader.ReadAsync(cancellationToken))
            {
                throw new AccessLocalReviewException(
                    "AccessReviewInputMismatch",
                    "index manifest does not match the scan manifest.");
            }
        }

        var indexedFactIds = new List<string>();
        await using (var factsCommand = connection.CreateCommand())
        {
            factsCommand.CommandText = """
                select fact_id, scan_id, repo, commit_sha
                from facts
                order by fact_id;
                """;
            await using var reader = await factsCommand.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                if (!SameScanIdentity(
                        fileManifest,
                        reader.GetString(1),
                        reader.GetString(2),
                        reader.GetString(3)))
                {
                    throw new AccessLocalReviewException(
                        "AccessReviewInputMismatch",
                        "indexed facts do not match the scan manifest.");
                }

                indexedFactIds.Add(reader.GetString(0));
            }
        }

        var artifactFactIds = factIds.OrderBy(value => value, StringComparer.Ordinal).ToArray();
        if (!artifactFactIds.SequenceEqual(indexedFactIds, StringComparer.Ordinal))
        {
            throw new AccessLocalReviewException(
                "AccessReviewInputMismatch",
                "index facts do not match the NDJSON fact inventory.");
        }
    }

    private static bool SameScanIdentity(
        ScanManifest manifest,
        string scanId,
        string repository,
        string commitSha) =>
        string.Equals(manifest.ScanId, scanId, StringComparison.Ordinal)
        && string.Equals(manifest.RepoName, repository, StringComparison.Ordinal)
        && string.Equals(manifest.CommitSha, commitSha, StringComparison.Ordinal);

    private static bool IndexManifestMatches(ScanManifest expected, string manifestJson)
    {
        try
        {
            var actual = JsonSerializer.Deserialize<ScanManifest>(manifestJson, JsonOptions);
            return actual is not null
                && SameScanIdentity(expected, actual.ScanId, actual.RepoName, actual.CommitSha)
                && string.Equals(expected.ScannerVersion, actual.ScannerVersion, StringComparison.Ordinal)
                && expected.ScannedAt.Equals(actual.ScannedAt);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static void RejectExistingReparsePath(string path, string code)
    {
        if (!OperatingSystem.IsWindows())
        {
            if ((File.Exists(path) || Directory.Exists(path))
                && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            {
                throw new AccessLocalReviewException(code, "path cannot be a reparse point.");
            }

            return;
        }

        var root = Path.GetPathRoot(path)
            ?? throw new AccessLocalReviewException(code, "path root is unavailable.");
        var current = root;
        var relative = Path.GetRelativePath(root, path);
        foreach (var segment in relative.Split(
                     [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                     StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            if ((File.Exists(current) || Directory.Exists(current))
                && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                throw new AccessLocalReviewException(code, "path ancestors cannot be reparse points.");
            }
        }
    }

    private static void ValidateRequiredArtifacts(string inputDirectory)
    {
        foreach (var relativePath in RequiredArtifacts)
        {
            var path = Path.Combine(inputDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path))
            {
                throw new AccessLocalReviewException(
                    "AccessReviewInputIncomplete",
                    $"required generated artifact is missing: {relativePath}.");
            }

            RejectExistingReparsePath(path, "AccessReviewInputInvalid");
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            {
                throw new AccessLocalReviewException(
                    "AccessReviewInputInvalid",
                    $"generated artifact cannot be a reparse point: {relativePath}.");
            }
        }
    }

    private static void ValidateExistingOutput(string outputDirectory, bool force)
    {
        if (File.Exists(outputDirectory))
        {
            throw new AccessLocalReviewException(
                "AccessReviewOutputCollision",
                "output path is an existing file.");
        }

        if (!Directory.Exists(outputDirectory))
        {
            return;
        }

        if ((File.GetAttributes(outputDirectory) & FileAttributes.ReparsePoint) != 0)
        {
            throw new AccessLocalReviewException(
                "AccessReviewOutputCollision",
                "output directory cannot be a reparse point.");
        }

        if (!force)
        {
            throw new AccessLocalReviewException(
                "AccessReviewOutputExists",
                "use --force only for an existing TraceMap-generated Access review bundle.");
        }

        if (!IsRecognizedGeneratedBundle(outputDirectory))
        {
            throw new AccessLocalReviewException(
                "AccessReviewOutputCollision",
                "refusing to replace an unrecognized caller-owned directory.");
        }
    }

    private static void ValidateNoOverlap(string inputDirectory, string outputDirectory)
    {
        if (IsEqualOrDescendant(inputDirectory, outputDirectory)
            || IsEqualOrDescendant(outputDirectory, inputDirectory))
        {
            throw new AccessLocalReviewException(
                "AccessReviewPathOverlap",
                "scan input and bundle output must not overlap.");
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
        foreach (var path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            var content = File.ReadAllText(path);
            if (content.Contains(inputDirectory, StringComparison.OrdinalIgnoreCase)
                || content.Contains(outputDirectory, StringComparison.OrdinalIgnoreCase)
                || MachineLocalPathPattern.IsMatch(content))
            {
                throw new AccessLocalReviewException(
                    "AccessReviewUnsafeOutput",
                    "generated bundle contains a prohibited local path.");
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

    internal static void Publish(
        string stagingDirectory,
        string outputDirectory,
        string backupDirectory,
        Action? afterBackupMoved = null)
    {
        var existing = Directory.Exists(outputDirectory);
        if (existing)
        {
            Directory.Move(outputDirectory, backupDirectory);
        }

        try
        {
            afterBackupMoved?.Invoke();
            Directory.Move(stagingDirectory, outputDirectory);
        }
        catch
        {
            if (existing
                && !Directory.Exists(outputDirectory)
                && !File.Exists(outputDirectory)
                && Directory.Exists(backupDirectory))
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
