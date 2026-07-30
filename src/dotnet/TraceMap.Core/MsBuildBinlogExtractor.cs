using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;

namespace TraceMap.Core;

internal sealed record MsBuildBinlogLimits(
    long MaxArtifactBytes = 64L * 1024 * 1024,
    long MaxExpandedBytes = 256L * 1024 * 1024,
    int MaxRecords = 250_000,
    int MaxProjects = 5_000,
    int MaxEdges = 10_000,
    int MaxDiagnostics = 5_000,
    int MaxSafeStringLength = 512);

public static partial class MsBuildBinlogExtractor
{
    private const string Limitations =
        "Observed bounded binlog evidence does not authenticate the artifact or prove the build ran at the declared commit, tests passed, the repository was clean, deployment is safe, release approval exists, diagnostics are complete, graph edges are runtime-reachable, or absence of a diagnostic means absence of a defect.";

    private static readonly MsBuildBinlogLimits DefaultLimits = new();

    public static void ValidateCommitBinding(
        string detectedCommitSha,
        IReadOnlyList<string>? binlogPaths,
        string? declaredCommitSha)
    {
        var hasInputs = binlogPaths is { Count: > 0 };
        if (!hasInputs)
        {
            if (!string.IsNullOrWhiteSpace(declaredCommitSha))
                throw new ArgumentException("--binlog-commit-sha requires at least one --binlog.");
            return;
        }
        if (binlogPaths!.Count > 8)
            throw new ArgumentException("--binlog accepts at most 8 explicit artifacts per scan.");

        if (string.IsNullOrWhiteSpace(declaredCommitSha))
            throw new ArgumentException("--binlog requires --binlog-commit-sha <sha>.");

        var normalized = declaredCommitSha.Trim();
        if (!CommitShaRegex().IsMatch(normalized))
            throw new ArgumentException("--binlog-commit-sha must be a full 40- or 64-character hexadecimal commit SHA.");
        if (string.IsNullOrWhiteSpace(detectedCommitSha)
            || detectedCommitSha.Equals("unknown", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("--binlog requires a repository with a detected commit SHA.");
        if (!detectedCommitSha.Equals(normalized, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("--binlog-commit-sha does not match the repository commit detected by TraceMap.");
    }

    internal static string CreateInputSignature(IReadOnlyList<string>? paths, string repoPath)
    {
        if (paths is not { Count: > 0 })
            return "no-binlog";

        var signatures = paths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => TryResolveInputPath(repoPath, path, out var fullPath)
                ? InputSignature(fullPath)
                : $"invalid-path:{FactFactory.Hash(path, 16)}")
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        return string.Join("|", signatures);
    }

    public static IReadOnlyList<CodeFact> Extract(
        string repoPath,
        ScanManifest manifest,
        IReadOnlyList<string>? binlogPaths) =>
        Extract(repoPath, manifest, binlogPaths, DefaultLimits);

    internal static IReadOnlyList<CodeFact> Extract(
        string repoPath,
        ScanManifest manifest,
        IReadOnlyList<string>? binlogPaths,
        MsBuildBinlogLimits limits,
        bool? runtimeAvailableOverride = null)
    {
        if (binlogPaths is not { Count: > 0 })
            return [];

        var root = Path.GetFullPath(repoPath);
        var runtimeAvailable = runtimeAvailableOverride
            ?? MsBuildRuntimeRegistration.TryRegister(out _);
        var facts = new List<CodeFact>();
        var inputPaths = new HashSet<string>(PathComparer());
        var invalidPathCount = 0;
        foreach (var path in binlogPaths.Where(path => !string.IsNullOrWhiteSpace(path)))
        {
            if (TryResolveInputPath(root, path, out var fullPath))
                inputPaths.Add(fullPath);
            else
                invalidPathCount++;
        }
        if (invalidPathCount > 0)
            facts.Add(Gap(manifest, "unavailable", "binlog-path-invalid", invalidPathCount));

        foreach (var inputPath in inputPaths.OrderBy(path => path, PathComparer()))
        {
            facts.AddRange(ExtractOne(root, manifest, inputPath, limits, runtimeAvailable));
        }

        return facts
            .GroupBy(fact => fact.FactId, StringComparer.Ordinal)
            .Select(group => AggregateEquivalentFacts(manifest, group))
            .OrderBy(fact => fact.FactType, StringComparer.Ordinal)
            .ThenBy(fact => fact.Evidence.FilePath, StringComparer.Ordinal)
            .ThenBy(fact => fact.Evidence.StartLine)
            .ThenBy(fact => fact.SourceSymbol, StringComparer.Ordinal)
            .ThenBy(fact => fact.TargetSymbol, StringComparer.Ordinal)
            .ThenBy(fact => fact.FactId, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<CodeFact> ExtractOne(
        string repoPath,
        ScanManifest manifest,
        string inputPath,
        MsBuildBinlogLimits limits,
        bool runtimeAvailable)
    {
        if (!Path.GetExtension(inputPath).Equals(".binlog", StringComparison.OrdinalIgnoreCase))
            return [Gap(manifest, "unavailable", "binlog-extension-unsupported", 1)];
        if (!File.Exists(inputPath))
            return [Gap(manifest, "unavailable", "binlog-unavailable", 1)];
        if (HasLinkOrReparsePointInPath(inputPath))
            return [Gap(manifest, "unavailable", "binlog-link-input-rejected", 1)];

        byte[] artifactBytes;
        try
        {
            using var input = new FileStream(inputPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (input.Length > limits.MaxArtifactBytes)
                return [Gap(manifest, "unavailable", "binlog-size-cap-exceeded", 1)];
            artifactBytes = new byte[checked((int)input.Length)];
            input.ReadExactly(artifactBytes);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or OverflowException)
        {
            return [Gap(manifest, "unavailable", "binlog-unavailable", 1)];
        }

        var artifactSha256 = Convert.ToHexString(SHA256.HashData(artifactBytes)).ToLowerInvariant();
        if (!runtimeAvailable)
            return [Gap(manifest, artifactSha256, "binlog-parser-runtime-unavailable", 1)];

        if (!TryMeasureExpandedSize(artifactBytes, limits.MaxExpandedBytes, out var expandedBytes, out var expandedLimitReached))
            return [Gap(manifest, artifactSha256, "binlog-malformed-or-unsupported", 1)];
        if (expandedLimitReached)
            return [Gap(manifest, artifactSha256, "binlog-expanded-size-cap-exceeded", 1)];

        var span = ArtifactSpan(artifactSha256);
        var facts = new List<CodeFact>();
        var projects = new SortedSet<string>(StringComparer.Ordinal);
        var projectByInstance = new Dictionary<int, string>();
        var rawEdges = new HashSet<(int Parent, int Child)>();
        var edges = new SortedSet<(string Parent, string Child)>(ProjectEdgeComparer.Instance);
        var diagnostics = new SortedSet<SafeDiagnostic>(SafeDiagnosticComparer.Instance);
        var gapCounts = new SortedDictionary<string, int>(StringComparer.Ordinal);
        var recordCount = 0;
        var recordedOutcome = "unknown";

        try
        {
            using var recordCapCancellation = new CancellationTokenSource();
            var replay = new BinaryLogReplayEventSource
            {
                AllowForwardCompatibility = false
            };
            replay.AnyEventRaised += (_, args) =>
            {
                if (++recordCount > limits.MaxRecords)
                {
                    Increment(gapCounts, "binlog-record-cap-reached");
                    recordCapCancellation.Cancel();
                    return;
                }

                switch (args)
                {
                    case ProjectStartedEventArgs project:
                        ObserveProject(repoPath, project, projects, projectByInstance, rawEdges, gapCounts, limits);
                        break;
                    case BuildErrorEventArgs error:
                        ObserveDiagnostic(repoPath, error.Code, error.File, error.ProjectFile, error.LineNumber, error.ColumnNumber, "error", diagnostics, gapCounts, limits);
                        break;
                    case BuildWarningEventArgs warning:
                        ObserveDiagnostic(repoPath, warning.Code, warning.File, warning.ProjectFile, warning.LineNumber, warning.ColumnNumber, "warning", diagnostics, gapCounts, limits);
                        break;
                    case BuildFinishedEventArgs finished:
                        recordedOutcome = finished.Succeeded ? "succeeded" : "failed";
                        break;
                }
            };
            using var stream = new MemoryStream(artifactBytes, writable: false);
            replay.Replay(stream, recordCapCancellation.Token);
        }
        catch (OperationCanceledException) when (recordCount > limits.MaxRecords)
        {
            // The record cap is already represented by a deterministic gap.
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            Increment(gapCounts, "binlog-malformed-or-unsupported");
        }

        foreach (var (parentId, childId) in rawEdges.OrderBy(edge => edge.Parent).ThenBy(edge => edge.Child))
        {
            if (edges.Count >= limits.MaxEdges)
            {
                Increment(gapCounts, "binlog-edge-cap-reached");
                break;
            }
            if (!projectByInstance.TryGetValue(parentId, out var parent)
                || !projectByInstance.TryGetValue(childId, out var child))
            {
                Increment(gapCounts, "binlog-edge-identity-unavailable");
                continue;
            }
            if (!parent.Equals(child, StringComparison.Ordinal))
                edges.Add((parent, child));
        }

        var coverage = gapCounts.Count == 0 ? "observed-bounded" : "observed-partial";
        foreach (var project in projects)
        {
            facts.Add(FactFactory.Create(
                manifest,
                FactTypes.MsBuildProjectObserved,
                RuleIds.BuildMsBuildBinlogObservation,
                EvidenceTiers.Tier2Structural,
                span,
                projectPath: project,
                targetSymbol: project,
                properties: ObservationProperties(artifactSha256, coverage, new()
                {
                    ["projectPath"] = project
                })));
        }

        foreach (var edge in edges)
        {
            facts.Add(FactFactory.Create(
                manifest,
                FactTypes.MsBuildProjectReferenceObserved,
                RuleIds.BuildMsBuildBinlogObservation,
                EvidenceTiers.Tier2Structural,
                span,
                sourceSymbol: edge.Parent,
                targetSymbol: edge.Child,
                properties: ObservationProperties(artifactSha256, coverage, new()
                {
                    ["relationshipKind"] = "recorded-project-build-edge"
                })));
        }

        foreach (var diagnostic in diagnostics)
        {
            var diagnosticSpan = new EvidenceSpan(
                diagnostic.FilePath,
                diagnostic.Line,
                diagnostic.Line,
                null,
                nameof(MsBuildBinlogExtractor),
                ScannerVersions.MsBuildBinlogExtractor);
            facts.Add(FactFactory.Create(
                manifest,
                FactTypes.MsBuildDiagnosticObserved,
                RuleIds.BuildMsBuildBinlogObservation,
                EvidenceTiers.Tier2Structural,
                diagnosticSpan,
                projectPath: diagnostic.ProjectPath,
                contractElement: diagnostic.Code,
                properties: ObservationProperties(artifactSha256, coverage, new()
                {
                    ["severity"] = diagnostic.Severity,
                    ["code"] = diagnostic.Code,
                    ["filePath"] = diagnostic.FilePath,
                    ["line"] = diagnostic.Line.ToString(),
                    ["column"] = diagnostic.Column.ToString()
                })));
        }

        facts.Add(FactFactory.Create(
            manifest,
            FactTypes.MsBuildBinlogObserved,
            RuleIds.BuildMsBuildBinlogObservation,
            EvidenceTiers.Tier2Structural,
            span,
            properties: ObservationProperties(artifactSha256, coverage, new()
            {
                ["artifactBytes"] = artifactBytes.Length.ToString(),
                ["expandedBytes"] = expandedBytes.ToString(),
                ["recordedBuildResult"] = recordedOutcome,
                ["processedRecordCount"] = Math.Min(recordCount, limits.MaxRecords).ToString(),
                ["projectCount"] = projects.Count.ToString(),
                ["projectEdgeCount"] = edges.Count.ToString(),
                ["diagnosticCount"] = diagnostics.Count.ToString(),
                ["partial"] = (gapCounts.Count > 0).ToString().ToLowerInvariant()
            })));

        facts.AddRange(gapCounts.Select(pair => Gap(manifest, artifactSha256, pair.Key, pair.Value)));
        return facts;
    }

    private static void ObserveProject(
        string repoPath,
        ProjectStartedEventArgs project,
        ISet<string> projects,
        IDictionary<int, string> projectByInstance,
        ISet<(int Parent, int Child)> rawEdges,
        IDictionary<string, int> gaps,
        MsBuildBinlogLimits limits)
    {
        if (!TryNormalizeRepoPath(repoPath, project.ProjectFile, repoPath, limits.MaxSafeStringLength, out var relativePath))
        {
            Increment(gaps, "binlog-project-path-omitted");
            return;
        }

        var instanceId = project.BuildEventContext?.ProjectInstanceId ?? -1;
        if (projects.Count >= limits.MaxProjects && !projects.Contains(relativePath))
        {
            Increment(gaps, "binlog-project-cap-reached");
            return;
        }
        projects.Add(relativePath);
        if (instanceId >= 0)
            projectByInstance[instanceId] = relativePath;

        var parentId = project.ParentProjectBuildEventContext?.ProjectInstanceId ?? -1;
        if (instanceId >= 0 && parentId >= 0 && parentId != instanceId)
            rawEdges.Add((parentId, instanceId));
    }

    private static void ObserveDiagnostic(
        string repoPath,
        string? code,
        string? file,
        string? projectFile,
        int line,
        int column,
        string severity,
        ISet<SafeDiagnostic> diagnostics,
        IDictionary<string, int> gaps,
        MsBuildBinlogLimits limits)
    {
        if (diagnostics.Count >= limits.MaxDiagnostics)
        {
            Increment(gaps, "binlog-diagnostic-cap-reached");
            return;
        }

        var normalizedCode = code?.Trim() ?? string.Empty;
        if (!DiagnosticCodeRegex().IsMatch(normalizedCode))
        {
            Increment(gaps, "binlog-diagnostic-code-omitted");
            return;
        }

        string? safeProject = null;
        var baseDirectory = repoPath;
        if (!string.IsNullOrWhiteSpace(projectFile))
        {
            if (!TryNormalizeRepoPath(repoPath, projectFile, repoPath, limits.MaxSafeStringLength, out var normalizedProject))
            {
                Increment(gaps, "binlog-diagnostic-path-omitted");
                return;
            }
            safeProject = normalizedProject;
            baseDirectory = Path.GetDirectoryName(Path.Combine(repoPath, normalizedProject.Replace('/', Path.DirectorySeparatorChar))) ?? repoPath;
        }

        if (!TryNormalizeRepoPath(repoPath, file, baseDirectory, limits.MaxSafeStringLength, out var normalizedFile))
        {
            Increment(gaps, "binlog-diagnostic-path-omitted");
            return;
        }

        diagnostics.Add(new SafeDiagnostic(
            severity,
            normalizedCode,
            normalizedFile,
            safeProject,
            Math.Max(1, line),
            Math.Max(1, column)));
    }

    private static bool TryNormalizeRepoPath(
        string repoPath,
        string? value,
        string baseDirectory,
        int maxLength,
        out string relativePath)
    {
        relativePath = string.Empty;
        if (string.IsNullOrWhiteSpace(value) || value.Length > 4096 || value.IndexOf('\0') >= 0)
            return false;
        if (!OperatingSystem.IsWindows() && WindowsRootedPathRegex().IsMatch(value))
            return false;

        try
        {
            var platformPath = value.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
            if (Path.DirectorySeparatorChar != '\\')
                platformPath = platformPath.Replace('\\', Path.DirectorySeparatorChar);
            var fullPath = Path.GetFullPath(Path.IsPathRooted(platformPath)
                ? platformPath
                : Path.Combine(baseDirectory, platformPath));
            var normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(repoPath));
            var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            if (!fullPath.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, comparison)
                && !fullPath.Equals(normalizedRoot, comparison))
                return false;

            var relative = FileInventory.NormalizeRelativePath(Path.GetRelativePath(normalizedRoot, fullPath));
            if (relative is "." or "" || relative.Length > maxLength || relative.StartsWith("../", StringComparison.Ordinal))
                return false;
            relativePath = relative;
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or NotSupportedException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static SortedDictionary<string, string> ObservationProperties(
        string artifactSha256,
        string coverage,
        SortedDictionary<string, string> specific)
    {
        specific["artifactSha256"] = artifactSha256;
        specific["coverageLabel"] = coverage;
        specific["limitations"] = Limitations;
        specific["observationKind"] = "recorded-build-artifact";
        return specific;
    }

    private static CodeFact Gap(ScanManifest manifest, string artifactSha256, string kind, int count) =>
        FactFactory.Create(
            manifest,
            FactTypes.AnalysisGap,
            RuleIds.BuildMsBuildBinlogGap,
            EvidenceTiers.Tier4Unknown,
            ArtifactSpan(artifactSha256),
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["artifactSha256"] = artifactSha256,
                ["coverageLabel"] = "observed-partial",
                ["gapKind"] = kind,
                ["omittedCount"] = count.ToString(),
                ["limitations"] = Limitations
            });

    private static CodeFact AggregateEquivalentFacts(ScanManifest manifest, IGrouping<string, CodeFact> group)
    {
        var first = group.First();
        if (first.FactType != FactTypes.AnalysisGap)
            return first;

        var omittedCount = group.Sum(fact =>
            int.TryParse(fact.Properties.GetValueOrDefault("omittedCount"), out var count) ? count : 1);
        return Gap(
            manifest,
            first.Properties.GetValueOrDefault("artifactSha256") ?? "unavailable",
            first.Properties.GetValueOrDefault("gapKind") ?? "binlog-gap",
            omittedCount);
    }

    private static EvidenceSpan ArtifactSpan(string artifactSha256) =>
        new(
            $"@artifact/msbuild-binlog/{(artifactSha256.Length >= 16 ? artifactSha256[..16] : artifactSha256)}",
            1,
            1,
            null,
            nameof(MsBuildBinlogExtractor),
            ScannerVersions.MsBuildBinlogExtractor);

    private static void Increment(IDictionary<string, int> counts, string key)
    {
        counts.TryGetValue(key, out var current);
        counts[key] = current + 1;
    }

    private static bool TryResolveInputPath(string repoPath, string path, out string fullPath)
    {
        try
        {
            fullPath = Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(repoPath, path));
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            fullPath = string.Empty;
            return false;
        }
    }

    private static string InputSignature(string path)
    {
        try
        {
            if (!File.Exists(path))
                return "unavailable";
            if (HasLinkOrReparsePointInPath(path))
                return "rejected:link";
            var info = new FileInfo(path);
            if (info.Length > DefaultLimits.MaxArtifactBytes)
                return $"rejected:{info.Length}";
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException or ArgumentException)
        {
            return "unavailable";
        }
    }

    private static bool TryMeasureExpandedSize(
        byte[] artifactBytes,
        long maxExpandedBytes,
        out long expandedBytes,
        out bool limitReached)
    {
        expandedBytes = 0;
        limitReached = false;
        try
        {
            using var input = new MemoryStream(artifactBytes, writable: false);
            using var gzip = new GZipStream(input, CompressionMode.Decompress, leaveOpen: false);
            var buffer = new byte[64 * 1024];
            while (true)
            {
                var read = gzip.Read(buffer, 0, buffer.Length);
                if (read == 0)
                    return true;
                expandedBytes += read;
                if (expandedBytes > maxExpandedBytes)
                {
                    limitReached = true;
                    return true;
                }
            }
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static bool HasLinkOrReparsePointInPath(string path)
    {
        try
        {
            var fullPath = Path.GetFullPath(path);
            var root = Path.GetPathRoot(fullPath);
            if (string.IsNullOrEmpty(root))
                return true;

            var current = root;
            var firstSegment = true;
            foreach (var segment in Path.GetRelativePath(root, fullPath).Split(
                         [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                         StringSplitOptions.RemoveEmptyEntries))
            {
                current = Path.Combine(current, segment);
                FileSystemInfo info = Directory.Exists(current)
                    ? new DirectoryInfo(current)
                    : new FileInfo(current);
                if (!string.IsNullOrEmpty(info.LinkTarget)
                    || (info.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    if (!firstSegment)
                        return true;

                    var resolved = info.ResolveLinkTarget(returnFinalTarget: true);
                    if (resolved is null)
                        return true;
                    current = resolved.FullName;
                }
                firstSegment = false;
            }

            return false;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return true;
        }
    }

    private static StringComparer PathComparer() =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    private sealed record SafeDiagnostic(
        string Severity,
        string Code,
        string FilePath,
        string? ProjectPath,
        int Line,
        int Column);

    private sealed class ProjectEdgeComparer : IComparer<(string Parent, string Child)>
    {
        public static ProjectEdgeComparer Instance { get; } = new();

        public int Compare((string Parent, string Child) x, (string Parent, string Child) y)
        {
            var parent = StringComparer.Ordinal.Compare(x.Parent, y.Parent);
            return parent != 0 ? parent : StringComparer.Ordinal.Compare(x.Child, y.Child);
        }
    }

    private sealed class SafeDiagnosticComparer : IComparer<SafeDiagnostic>
    {
        public static SafeDiagnosticComparer Instance { get; } = new();

        public int Compare(SafeDiagnostic? x, SafeDiagnostic? y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x is null) return -1;
            if (y is null) return 1;
            var values = new[]
            {
                StringComparer.Ordinal.Compare(x.FilePath, y.FilePath),
                x.Line.CompareTo(y.Line),
                x.Column.CompareTo(y.Column),
                StringComparer.Ordinal.Compare(x.Severity, y.Severity),
                StringComparer.Ordinal.Compare(x.Code, y.Code),
                StringComparer.Ordinal.Compare(x.ProjectPath ?? string.Empty, y.ProjectPath ?? string.Empty)
            };
            return values.FirstOrDefault(value => value != 0);
        }
    }

    [GeneratedRegex("^[0-9a-fA-F]{40}([0-9a-fA-F]{24})?$", RegexOptions.CultureInvariant)]
    private static partial Regex CommitShaRegex();

    [GeneratedRegex("^(?:MSB|CS|BC|FS|NU|NETSDK|CA|IDE|IL|RZ|SYSLIB|ASP)[0-9]{3,7}$", RegexOptions.CultureInvariant)]
    private static partial Regex DiagnosticCodeRegex();

    [GeneratedRegex("^[A-Za-z]:[\\\\/]", RegexOptions.CultureInvariant)]
    private static partial Regex WindowsRootedPathRegex();
}
