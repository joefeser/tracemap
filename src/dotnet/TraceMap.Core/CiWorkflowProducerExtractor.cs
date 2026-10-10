using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Win32.SafeHandles;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TraceMap.Core;

internal sealed record CiWorkflowProducerRow(
    string WorkflowPath, string Package, string? Version, string? ProjectPath, int StartLine, int EndLine);
internal sealed record CiWorkflowGap(string Path, string Kind, string? Detail = null, int Line = 1);
internal sealed record CiWorkflowResult(
    IReadOnlyList<CiWorkflowProducerRow> Rows,
    IReadOnlyList<CiWorkflowGap> Gaps,
    string GeneratorSha256,
    string BoundedInputSha256);

/// Reads GitHub workflow pack definitions (dotnet pack with CI property overrides) as
/// ci-defined producer evidence. A bounded structural YAML subset only: any construct the
/// subset cannot prove becomes a typed gap for the whole file, never a guessed fact.
internal static class CiWorkflowProducerExtractor
{
    // Absolute resource caps, deliberately not options-tunable.
    internal const int MaxFiles = 64;
    internal const int MaxFileBytes = 1_048_576;
    internal const long MaxTotalBytes = 8_388_608;

    private static readonly Regex KeyPattern = new("^[A-Za-z_][A-Za-z0-9_-]*\\z",
        RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    private static readonly Regex EnvExpression = new("^\\$\\{\\{\\s*env\\.(?<name>[A-Za-z_][A-Za-z0-9_]*)\\s*\\}\\}$",
        RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    internal static CiWorkflowResult Read(
        ScanOptions options,
        IReadOnlyList<ProducedPackageInfo> producedPackages,
        CancellationToken cancellationToken)
    {
        var root = Path.GetFullPath(options.RepoPath);
        var output = Path.GetFullPath(options.OutputPath).TrimEnd(Path.DirectorySeparatorChar);
        var comparer = CSharpSemanticExtractor.CreateSourcePathComparer(root);
        var workflows = Path.Combine(root, ".github", "workflows");
        var rows = new List<CiWorkflowProducerRow>();
        var gaps = new List<CiWorkflowGap>();
        var observed = new List<string>();
        var files = 0;
        long bytesRead = 0;
        var producedByPath = producedPackages
            .GroupBy(package => package.ProjectPath, comparer)
            .ToDictionary(group => group.Key, group => group.First(), comparer);
        try
        {
            if (Directory.Exists(workflows))
            {
                if (File.GetAttributes(Path.Combine(root, ".github")).HasFlag(FileAttributes.ReparsePoint)
                    || File.GetAttributes(workflows).HasFlag(FileAttributes.ReparsePoint))
                    gaps.Add(new(".github/workflows", "ci-workflow-linked-path"));
                else if (comparer.Equals(workflows.TrimEnd(Path.DirectorySeparatorChar), output)
                    || workflows.StartsWith(output + Path.DirectorySeparatorChar,
                        comparer == StringComparer.OrdinalIgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                    gaps.Add(new(".github/workflows", "ci-workflow-output-boundary"));
                else
                    ReadWorkflows(root, workflows, options, comparer, producedByPath, rows, gaps, observed,
                        ref files, ref bytesRead, cancellationToken);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            gaps.Add(new(".github/workflows", "ci-workflow-discovery-failed"));
        }
        if (files == 0 && gaps.Count == 0) gaps.Add(new(".github/workflows", "ci-workflow-not-found"));
        using var generator = File.OpenRead(typeof(CiWorkflowProducerExtractor).Assembly.Location);
        // The signature includes bounded reads and categorical omissions. It is not a source snapshot.
        var signature = JsonSerializer.Serialize(new
        {
            observed = observed.Order(StringComparer.Ordinal),
            gaps = OrderGaps(gaps),
            limits = new { maxFiles = MaxFiles, maxFileBytes = MaxFileBytes, maxTotalBytes = MaxTotalBytes }
        });
        return new(ResolveConflicts(rows, gaps), OrderGaps(gaps).ToArray(),
            Convert.ToHexString(SHA256.HashData(generator)).ToLowerInvariant(), FactFactory.Hash(signature, 64));
    }

    private static void ReadWorkflows(
        string root,
        string workflows,
        ScanOptions options,
        StringComparer comparer,
        IReadOnlyDictionary<string, ProducedPackageInfo> producedByPath,
        List<CiWorkflowProducerRow> rows,
        List<CiWorkflowGap> gaps,
        List<string> observed,
        ref int files,
        ref long bytesRead,
        CancellationToken cancellationToken)
    {
        List<string> candidates;
        try
        {
            candidates = Directory.EnumerateFiles(workflows)
                .Where(path => path.EndsWith(".yml", StringComparison.OrdinalIgnoreCase)
                    || path.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase))
                .Order(StringComparer.Ordinal)
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            gaps.Add(new(".github/workflows", "ci-workflow-discovery-failed"));
            return;
        }
        foreach (var path in candidates)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            if ((options.ExcludeGlobs ?? []).Any(glob => ScanEngine.GlobMatches(relative, glob, comparer)))
                continue;
            if (++files > MaxFiles)
            {
                gaps.Add(new(relative, "ci-workflow-file-limit"));
                return;
            }
            try
            {
                if (File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint))
                {
                    gaps.Add(new(relative, "ci-workflow-linked-path"));
                    continue;
                }
                using var stream = OpenNoFollow(root, path, out var linked);
                if (linked || stream is null)
                {
                    gaps.Add(new(relative, "ci-workflow-linked-path"));
                    continue;
                }
                if (stream.Length > MaxFileBytes || stream.Length > MaxTotalBytes - bytesRead)
                {
                    gaps.Add(new(relative, "ci-workflow-byte-limit"));
                    continue;
                }
                // Bounded read even if another writer grows the file on a platform without mandatory locks.
                var bytes = new byte[checked((int)stream.Length)];
                bytesRead += bytes.Length;
                stream.ReadExactly(bytes);
                if (stream.ReadByte() != -1) throw new IOException("Changed workflow file");
                var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
                observed.Add(relative + "\0" + hash);
                ParseWorkflow(relative, bytes, root, producedByPath, rows, gaps, observed);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                gaps.Add(new(relative, "ci-workflow-read-failed"));
            }
        }
    }

    private const int OReadOnly = 0;
    private const int NoFollowMacOs = 0x0100;
    private const int NoFollowLinux = 0x20000;
    private const int ErrnoLoopLinux = 40;
    private const int ErrnoLoopMacOs = 62; // ELOOP: O_NOFOLLOW hit a final symlink

    [DllImport("libc", SetLastError = true, EntryPoint = "open")]
    private static extern int open(string path, int flags);

    [DllImport("libc", SetLastError = true, EntryPoint = "openat")]
    private static extern int openat(SafeFileHandle directory, string path, int flags);

    /// Opens the workflow without following a final symlink. Unix platforms use a true
    /// O_NOFOLLOW descriptor (the read bytes belong to the opened inode even if the path is
    /// swapped afterwards); Windows — where creating symlinks already needs elevated
    /// privileges — keeps the documented path-check residual.
    private static FileStream? OpenNoFollow(string root, string path, out bool linked)
    {
        linked = false;
        if (OperatingSystem.IsMacOS() || OperatingSystem.IsLinux())
        {
            var noFollow = OperatingSystem.IsMacOS() ? NoFollowMacOs : NoFollowLinux;
            var directoryFlags = noFollow | (OperatingSystem.IsMacOS() ? 0x100000 : 0x10000); // O_DIRECTORY
            using var rootHandle = CheckedHandle(open(root, OReadOnly | directoryFlags));
            using var github = CheckedHandle(openat(rootHandle, ".github", directoryFlags));
            using var workflows = CheckedHandle(openat(github, "workflows", directoryFlags));
            // Resolve each component from its held parent descriptor. O_NOFOLLOW on the
            // leaf alone would still follow a swapped .github/workflows directory.
            var nonBlocking = OperatingSystem.IsMacOS() ? 0x4 : 0x800; // O_NONBLOCK: never wait for a FIFO writer
            var descriptor = openat(workflows, Path.GetFileName(path), OReadOnly | noFollow | nonBlocking);
            if (descriptor < 0
                && Marshal.GetLastWin32Error() == (OperatingSystem.IsMacOS() ? ErrnoLoopMacOs : ErrnoLoopLinux))
            {
                linked = true;
                return null;
            }
            var handle = CheckedHandle(descriptor);
            try
            {
                var opened = new FileStream(handle, FileAccess.Read);
                if (opened.CanSeek) return opened;
                opened.Dispose();
                throw new IOException("Workflow must be a seekable regular file.");
            }
            catch
            {
                handle.Dispose();
                throw;
            }
        }
        if (IsLink(path))
        {
            linked = true;
            return null;
        }
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (IsLink(path))
        {
            stream.Dispose();
            linked = true;
            return null;
        }
        return stream;
    }

    private static SafeFileHandle CheckedHandle(int descriptor)
    {
        if (descriptor < 0) throw new IOException($"Workflow open failed (errno {Marshal.GetLastWin32Error()}).");
        return new SafeFileHandle((IntPtr)descriptor, ownsHandle: true);
    }

    private static bool IsLink(string path)
    {
        try
        {
            return File.ResolveLinkTarget(path, returnFinalTarget: false) is not null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return true; // a path whose link state cannot be proven is not workflow evidence
        }
    }

    /// Same package claimed by CI at one effective version collapses to the first deterministic
    /// occurrence; two different versions are a self-contradiction gap with no fact (a human resolves).
    private static IReadOnlyList<CiWorkflowProducerRow> ResolveConflicts(
        List<CiWorkflowProducerRow> rows, List<CiWorkflowGap> gaps)
    {
        var resolved = new List<CiWorkflowProducerRow>();
        foreach (var group in rows.GroupBy(row => row.Package, StringComparer.OrdinalIgnoreCase))
        {
            var versions = group.Select(row => row.Version).Where(version => version is not null)
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            if (versions.Length > 1)
            {
                gaps.Add(new(".", "ci-producer-version-conflict",
                    $"{group.Key}: {string.Join(" | ", versions)}"));
                continue;
            }
            var ordered = group.OrderBy(row => row.WorkflowPath, StringComparer.Ordinal)
                .ThenBy(row => row.StartLine)
                .ThenBy(row => row.EndLine)
                .ToList();
            // Keep the entire evidence tuple from one occurrence. A version/project from
            // another command must never be attributed to the first command's line span.
            resolved.Add(versions.Length == 0 ? ordered[0]
                : ordered.First(row => row.Version == versions[0]));
        }
        return resolved.OrderBy(row => row.WorkflowPath, StringComparer.Ordinal)
            .ThenBy(row => row.Package, StringComparer.Ordinal)
            .ToArray();
    }

    private static IEnumerable<CiWorkflowGap> OrderGaps(IEnumerable<CiWorkflowGap> gaps) =>
        gaps.OrderBy(gap => gap.Path, StringComparer.Ordinal)
            .ThenBy(gap => gap.Kind, StringComparer.Ordinal)
            .ThenBy(gap => gap.Detail, StringComparer.Ordinal);

    internal static IEnumerable<CodeFact> Materialize(ScanManifest manifest, CiWorkflowResult result)
    {
        foreach (var row in result.Rows)
        {
            var properties = new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["generatorSha256"] = result.GeneratorSha256,
                ["boundedInputSha256"] = result.BoundedInputSha256,
                ["dependencyGroup"] = "PackageProduced",
                ["ecosystem"] = "nuget",
                ["manifestKind"] = "github-workflow",
                ["package"] = row.Package,
                ["packageManager"] = "nuget",
                ["packageName"] = row.Package,
                ["sourceKind"] = "ci-workflow",
                ["surfaceKind"] = "package-config",
                ["workflowPath"] = row.WorkflowPath
            };
            if (row.ProjectPath is not null) properties["projectPath"] = row.ProjectPath;
            // Unresolvable versions are omitted entirely (never empty, never guessed) — the
            // consuming registry renders them as unevidenced.
            if (row.Version is not null) properties["version"] = row.Version;
            yield return FactFactory.Create(manifest, FactTypes.PackageProduced, RuleIds.ProjectFile,
                EvidenceTiers.Tier2Structural,
                new EvidenceSpan(row.WorkflowPath, row.StartLine, row.EndLine, null, "CiWorkflowExtractor",
                    ScannerVersions.CiWorkflowExtractor),
                projectPath: row.ProjectPath,
                targetSymbol: row.Package,
                properties: properties);
        }
        foreach (var gap in result.Gaps.Distinct())
        {
            var properties = new SortedDictionary<string, string>( StringComparer.Ordinal)
            {
                ["gapKind"] = gap.Kind,
                ["message"] = "CI workflow producer evidence is partial or unavailable."
                    + (string.IsNullOrWhiteSpace(gap.Detail) ? string.Empty : " " + gap.Detail + "."),
                ["generatorSha256"] = result.GeneratorSha256,
                ["boundedInputSha256"] = result.BoundedInputSha256
            };
            if (gap.Path.EndsWith(".yml", StringComparison.OrdinalIgnoreCase)
                || gap.Path.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase))
                properties["workflowPath"] = gap.Path;
            yield return FactFactory.Create(manifest, FactTypes.AnalysisGap, RuleIds.ProjectFile,
                EvidenceTiers.Tier4Unknown,
                new EvidenceSpan(gap.Path, gap.Line, gap.Line, null, "CiWorkflowExtractor",
                    ScannerVersions.CiWorkflowExtractor),
                properties: properties);
        }
    }

    private sealed class WorkflowFormatException(string kind, string detail) : Exception(detail)
    {
        public string Kind { get; } = kind;
    }

    [DoesNotReturn]
    private static void Fail(string detail) => throw new WorkflowFormatException("ci-workflow-invalid", detail);
    [DoesNotReturn]
    private static void Unsupported(string detail) => throw new WorkflowFormatException("ci-workflow-unsupported", detail);

    private static void ParseWorkflow(
        string path,
        byte[] bytes,
        string root,
        IReadOnlyDictionary<string, ProducedPackageInfo> producedByPath,
        List<CiWorkflowProducerRow> rows,
        List<CiWorkflowGap> gaps,
        List<string> observed)
    {
        try
        {
            foreach (var script in new WorkflowParser(bytes).Parse())
            {
                if (!script.CommandShell)
                {
                    // The text is handed to a non-command interpreter (or an unprovable one):
                    // a pack-looking line is unevidence, never a producer fact.
                    if (ContainsPackCommand(script))
                        gaps.Add(new(path, "ci-workflow-unsupported",
                            "pack text under a non-command shell", script.StartLine));
                    continue;
                }
                if (!IsLineOrientedScript(script))
                {
                    gaps.Add(new(path, "ci-workflow-unsupported",
                        "multiline shell data or unsupported shell syntax", script.StartLine));
                    continue;
                }
                foreach (var occurrence in ExtractPackOccurrences(path, root, script))
                {
                    if (occurrence.RawPackageId is null && occurrence.ProjectTarget is { } target)
                    {
                        producedByPath.TryGetValue(target, out var project);
                        observed.Add("project-identity\0" + JsonSerializer.Serialize(new
                        {
                            target, project?.PackageId, project?.ExplicitPackageId
                        }));
                    }
                    ResolveOccurrence(occurrence, producedByPath, rows, gaps);
                }
            }
        }
        catch (WorkflowFormatException ex)
        {
            gaps.Add(new(path, ex.Kind, ex.Message));
        }
    }

    // ---- bounded structural YAML subset -------------------------------------------------------

    private sealed class Frame(int column, bool sequence)
    {
        public int Column { get; } = column;
        public bool Sequence { get; } = sequence;
        public string Role { get; init; } = "other"; // root, jobs, job, steps, step, env-*, other
        public string? JobId { get; init; }
        public HashSet<string> SeenKeys { get; } = new(StringComparer.Ordinal);
    }

    /// A run block captured from jobs.<id>.steps[*].run. Env resolution is deferred until the
    /// whole document is parsed (env may be declared after jobs/steps); WorkingDir carries the
    /// effective literal working directory (step over job defaults over workflow defaults).
    private sealed record WorkingDirectory(bool Known, string? Path);

    private sealed record RunScript(
        IReadOnlyList<(string Text, int LineNo)> Lines, bool Folded, int StartLine, int EndLine,
        IReadOnlyDictionary<string, string> Env, WorkingDirectory WorkingDir, bool CommandShell = true);

    /// Shells GitHub runs as command interpreters for run blocks; anything else (python,
    /// custom `shell: tool {0}` forms, templated shells) is not command-line evidence.
    private static readonly string[] CommandShells =
        ["bash", "sh", "pwsh", "powershell", "cmd"];

    private sealed class WorkflowParser(byte[] bytes)
    {
    private readonly record struct ScopeValue(byte State, string? Value)
    {
        public static readonly ScopeValue Absent = new(0, null);
        public static ScopeValue Known(string value) => new(1, value);
        public static readonly ScopeValue Unknown = new(2, null);
        public bool IsAbsent => State == 0;
    }

    private readonly string[] _lines = SplitLines(bytes);
    private readonly Dictionary<string, string> _workflowEnv = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<string, string>> _jobEnvs = new(StringComparer.Ordinal);
    private readonly List<(string JobId, RunScript Script, Dictionary<string, string> StepEnv,
        ScopeValue StepWd, ScopeValue StepShell)> _pendingRuns = [];
    private readonly Stack<Frame> _frames = [];
    private Dictionary<string, string>? _stepEnv;
    private RunScript? _stepRun;
    private ScopeValue _rootDefaultWd = ScopeValue.Absent;
    private readonly Dictionary<string, ScopeValue> _jobDefaultWds = new(StringComparer.Ordinal);
    private ScopeValue _rootDefaultShell = ScopeValue.Absent;
    private readonly Dictionary<string, ScopeValue> _jobDefaultShells = new(StringComparer.Ordinal);
    private ScopeValue _stepWd = ScopeValue.Absent;
    private ScopeValue _stepShell = ScopeValue.Absent;
    private (int Column, Frame Parent, string Key)? _pendingBlock;
    private (int Column, string? JobId, bool Step)? _pendingItem;
    private int _index;
    private bool _sawContent;

    public List<RunScript> Parse()
    {
        _frames.Push(new Frame(0, sequence: false) { Role = "root" });
        while (_index < _lines.Length)
        {
            var raw = _lines[_index];
            var lineNo = _index + 1;
            _index++;
            if (raw.Trim().Length == 0) continue;
            var indent = 0;
            while (indent < raw.Length && raw[indent] == ' ') indent++;
            if (indent < raw.Length && raw[indent] == '\t') Fail("tab used for indentation");
            var content = StripComment(raw[indent..]).TrimEnd();
            if (content.Length == 0) continue;
            if (content == "---")
            {
                if (_sawContent) Unsupported("multiple YAML documents");
                continue;
            }
            if (content == "...")
            {
                _sawContent = true;
                continue;
            }
            if (content[0] == '%') Unsupported("YAML directives");
            _sawContent = true;
            var isDash = content == "-" || content.StartsWith("- ", StringComparison.Ordinal);
            ProcessStructuralLine(content, indent, isDash, lineNo);
        }
        while (_frames.Count > 1) Pop();
        // Env maps are complete only now: resolve every run with its full scope chain
        // (step over job over workflow), regardless of YAML key order.
        return _pendingRuns.Select(pending =>
        {
            var effective = new Dictionary<string, string>(_workflowEnv, StringComparer.Ordinal);
            if (_jobEnvs.TryGetValue(pending.JobId, out var jobEnv))
                foreach (var (name, literal) in jobEnv) effective[name] = literal;
            foreach (var (name, literal) in pending.StepEnv) effective[name] = literal;
            var wd = ResolveScope(pending.StepWd,
                _jobDefaultWds.TryGetValue(pending.JobId, out var jobWd) ? jobWd : ScopeValue.Absent, _rootDefaultWd);
            var shell = ResolveScope(pending.StepShell,
                _jobDefaultShells.TryGetValue(pending.JobId, out var jobShell) ? jobShell : ScopeValue.Absent, _rootDefaultShell);
            return pending.Script with
            {
                Env = effective,
                WorkingDir = wd.State == 1 ? new WorkingDirectory(true, wd.Value)
                    : wd.State == 2 ? new WorkingDirectory(false, null)
                    : new WorkingDirectory(true, null),
                CommandShell = shell.IsAbsent || (shell.State == 1 && CommandShells.Contains(shell.Value, StringComparer.OrdinalIgnoreCase))
            };
        }).ToList();
    }

    private void ProcessStructuralLine(string content, int indent, bool isDash, int lineNo)
    {
        if (_pendingBlock is { } open && indent > open.Column)
            OpenBlock(open, indent, isDash);
        else if (_pendingItem is { } item && indent > item.Column)
            OpenItemBody(item, indent, isDash);
        else if (_pendingBlock is { } closed && closed.Parent.Role == "jobs")
            Fail($"job '{Truncate(closed.Key)}' must be a mapping block");
        else
        {
            _pendingBlock = null;
            _pendingItem = null; // the pending key or item had a null body
        }

        while (_frames.Count > 1)
        {
            var top = _frames.Peek();
            if (!top.Sequence && indent < top.Column) { Pop(); continue; }
            if (top.Sequence && indent <= top.Column && !(indent == top.Column && isDash)) { Pop(); continue; }
            break;
        }

        var current = _frames.Peek();
        if (isDash)
        {
            if (!current.Sequence || indent != current.Column)
                Fail("sequence item outside a sequence block");
            ProcessSequenceItem(content, indent);
            return;
        }
        if (current.Sequence || indent != current.Column)
            Unsupported("unexpected indentation (plain scalar continuation)");
        ProcessKeyLine(current, content, indent, lineNo);
    }

    /// Opens the nested block of a pending key; the current line is its first member.
    private void OpenBlock((int Column, Frame Parent, string Key) open, int indent, bool isDash)
    {
        _pendingBlock = null;
        var role = (open.Parent.Role, open.Key) switch
        {
            ("root", "jobs") => "jobs",
            ("jobs", _) => "job",
            ("job", "steps") => "steps",
            ("root", "env") => "env-workflow",
            ("job", "env") => "env-job",
            ("step", "env") => "env-step",
            ("root", "defaults") => "defaults-root",
            ("job", "defaults") => "defaults-job",
            ("defaults-root", "run") => "defaults-run-root",
            ("defaults-job", "run") => "defaults-run-job",
            _ => "other"
        };
        if (role is not "steps" and not "other" && isDash)
            Fail($"{RoleName(role)} must be a mapping block");
        if (role == "steps" && !isDash) Fail("steps must be a sequence block");
        _frames.Push(new Frame(indent, isDash)
        {
            Role = role,
            JobId = role == "job" ? open.Key : open.Parent.JobId
        });
    }

    private static string RoleName(string role) => role switch
    {
        "jobs" => "jobs",
        "job" => "job",
        "env-workflow" or "env-job" or "env-step" => "env",
        "defaults-root" or "defaults-job" => "defaults",
        "defaults-run-root" or "defaults-run-job" => "defaults run",
        _ => role
    };

    /// Opens the body of a bare "- " sequence item; the current line is its first member.
    private void OpenItemBody((int Column, string? JobId, bool Step) item, int indent, bool isDash)
    {
        _pendingItem = null;
        if (isDash) Unsupported("sequence items inside a sequence item body");
        var frame = new Frame(indent, sequence: false)
        {
            Role = item.Step ? "step" : "other",
            JobId = item.JobId
        };
        _frames.Push(frame);
        if (item.Step) BeginStep();
    }

    private void BeginStep()
    {
        _stepEnv = [];
        _stepWd = ScopeValue.Absent;
        _stepShell = ScopeValue.Absent;
    }

    private void Pop()
    {
        var frame = _frames.Pop();
        if (frame.Role != "step" || frame.JobId is null) return;
        if (_stepRun is { } script)
            _pendingRuns.Add((frame.JobId, script,
                new Dictionary<string, string>(_stepEnv ?? [], StringComparer.Ordinal), _stepWd, _stepShell));
        _stepRun = null;
        _stepEnv = null;
        _stepWd = ScopeValue.Absent;
        _stepShell = ScopeValue.Absent;
    }

    private static ScopeValue ResolveScope(ScopeValue step, ScopeValue job, ScopeValue root) =>
        !step.IsAbsent ? step : !job.IsAbsent ? job : !root.IsAbsent ? root : ScopeValue.Absent;

    private void ProcessSequenceItem(string content, int dashColumn)
    {
        var sequence = _frames.Peek();
        var rest = content == "-" ? string.Empty : content[1..];
        var spaces = 0;
        while (spaces < rest.Length && rest[spaces] == ' ') spaces++;
        var itemColumn = dashColumn + 1 + spaces;
        var itemContent = spaces >= rest.Length ? string.Empty : rest[spaces..];
        if (itemContent.Length == 0)
        {
            _pendingItem = (dashColumn, sequence.JobId, sequence.Role == "steps");
            return;
        }
        var split = SplitKey(itemContent);
        if (split is null)
        {
            if (itemContent == "-" || itemContent.StartsWith("- ", StringComparison.Ordinal))
                Fail("nested sequence items are not modeled");
            ClassifyValue(itemContent); // plain or opaque single-line flow scalar item
            return;
        }
        var frame = new Frame(itemColumn, sequence: false)
        {
            Role = sequence.Role == "steps" ? "step" : "other",
            JobId = sequence.JobId
        };
        _frames.Push(frame);
        if (sequence.Role == "steps") BeginStep();
        var (key, value) = split.Value;
        ApplyKey(frame, key, value, itemColumn, lineNo: _index);
    }

    private void ProcessKeyLine(Frame mapping, string content, int column, int lineNo)
    {
        var split = SplitKey(content);
        if (split is null) Unsupported("plain scalar where a mapping key was expected");
        var (key, value) = split.Value;
        ApplyKey(mapping, key, value, column, lineNo);
    }

    private void ApplyKey(Frame mapping, string key, string value, int column, int lineNo)
    {
        if (!KeyPattern.IsMatch(key)) Unsupported($"unsupported key syntax: {Truncate(key)}");
        if (key == "<<") Unsupported("merge keys");
        if (!mapping.SeenKeys.Add(key)) Fail($"duplicate key: {Truncate(key)}");
        if (mapping.Role is "env-workflow" or "env-job" or "env-step")
        {
            // Even an unknown/empty binding shadows the same name in an outer scope.
            var literal = string.Empty;
            if (value.Length > 0)
            {
                if (IsBlockHeader(value)) { ParseBlockHeader(value); ConsumeBlock(column); }
                else
                {
                    var classified = ClassifyValue(value);
                    if (!value.Contains("${{", StringComparison.Ordinal)) literal = classified;
                }
            }
            switch (mapping.Role)
            {
                case "env-workflow": _workflowEnv[key] = literal; break;
                case "env-job": JobEnv(mapping.JobId)[key] = literal; break;
                default: _stepEnv![key] = literal; break;
            }
            return;
        }
        if (value.Length == 0)
        {
            _pendingBlock = (column, mapping, key);
            return;
        }
        if (mapping.Role == "jobs")
            Unsupported($"job '{Truncate(key)}' must be a mapping block");
        if (mapping.Role == "root" && key == "jobs")
            Unsupported("jobs must be a mapping block");
        if (mapping.Role == "job" && key == "steps")
            Unsupported("steps must be a sequence block");
        if (mapping.Role == "step" && key == "run")
        {
            _stepRun = ParseRunValue(value, column, lineNo);
            return;
        }
        if (mapping.Role == "step" && key is "working-directory" or "shell")
        {
            var scope = ClassifyLiteralScope(value, column);
            if (key == "working-directory") _stepWd = scope;
            else _stepShell = scope;
            return;
        }
        if (mapping.Role is "defaults-run-root" or "defaults-run-job")
        {
            if (key is not ("working-directory" or "shell")) { ConsumeOpaqueValue(value, column); return; }
            var scope = ClassifyLiteralScope(value, column);
            if (key == "working-directory")
            {
                if (mapping.Role == "defaults-run-root") _rootDefaultWd = scope;
                else _jobDefaultWds[mapping.JobId ?? "."] = scope;
            }
            else
            {
                if (mapping.Role == "defaults-run-root") _rootDefaultShell = scope;
                else _jobDefaultShells[mapping.JobId ?? "."] = scope;
            }
            return;
        }
        if ((mapping.Role is "root" or "job" or "step" && key == "env")
            || (mapping.Role is "root" or "job" && key == "defaults")
            || (mapping.Role is "defaults-root" or "defaults-job" && key == "run"))
            Unsupported("effective env/defaults scope must be a mapping block");
        ConsumeOpaqueValue(value, column);
    }

    /// A working-directory or shell is evidence only as a literal; templated, flow, or
    /// block values leave the scope unprovable.
    private ScopeValue ClassifyLiteralScope(string value, int column)
    {
        if (IsBlockHeader(value)) { ParseBlockHeader(value); ConsumeBlock(column); return ScopeValue.Unknown; }
        if (!value.Contains("${{", StringComparison.Ordinal) && ClassifyValue(value) is { Length: > 0 } literal)
            return ScopeValue.Known(literal);
        return ScopeValue.Unknown;
    }

    private Dictionary<string, string> JobEnv(string? jobId)
    {
        if (jobId is null) return [];
        if (!_jobEnvs.TryGetValue(jobId, out var env))
            _jobEnvs[jobId] = env = new Dictionary<string, string>(StringComparer.Ordinal);
        return env;
    }

    private RunScript? ParseRunValue(string value, int keyColumn, int lineNo)
    {
        if (IsBlockHeader(value))
        {
            ParseBlockHeader(value);
            var (contentLines, startLine, endLine) = ConsumeBlock(keyColumn);
            if (contentLines.Count == 0) return null;
            // YAML retains newlines around empty/more-indented folded lines; joining
            // those with spaces would turn separate commands into property overrides.
            if (value[0] == '>' && (contentLines.Any(line => line.Text.Length > 0 && char.IsWhiteSpace(line.Text[0]))
                || contentLines.Zip(contentLines.Skip(1)).Any(pair => pair.Second.LineNo != pair.First.LineNo + 1)))
                Unsupported("folded scalar with blank or more-indented lines");
            return value[0] == '>'
                ? new RunScript([(string.Join(" ", contentLines.Select(line => line.Text)), startLine)],
                    Folded: true, startLine, endLine, EmptyEnv, RootWorkingDirectory)
                : new RunScript(contentLines, Folded: false, startLine, endLine, EmptyEnv, RootWorkingDirectory);
        }
        var scalar = ClassifyValue(value);
        if (scalar.Length == 0) Unsupported("run must be a scalar or block scalar");
        return new RunScript([(scalar, lineNo)], Folded: false, lineNo, lineNo, EmptyEnv, RootWorkingDirectory);
    }

    private static readonly IReadOnlyDictionary<string, string> EmptyEnv =
        new Dictionary<string, string>(StringComparer.Ordinal);
    private static readonly WorkingDirectory RootWorkingDirectory = new(true, null);

    private static bool IsBlockHeader(string value) => value.Length > 0 && (value[0] == '|' || value[0] == '>');

    private (List<(string Text, int LineNo)> Content, int StartLine, int EndLine) ConsumeBlock(int keyColumn)
    {
        var content = new List<(string, int)>();
        var blockIndent = -1;
        int startLine = 0, endLine = 0;
        int line;
        for (line = _index; line < _lines.Length; line++)
        {
            var candidate = _lines[line];
            if (candidate.Trim().Length == 0) continue;
            var ind = 0;
            while (ind < candidate.Length && candidate[ind] == ' ') ind++;
            if (blockIndent < 0)
            {
                if (ind <= keyColumn) break;
                blockIndent = ind;
            }
            if (ind < blockIndent) break;
            if (startLine == 0) startLine = line + 1;
            endLine = line + 1;
            content.Add((candidate[blockIndent..], line + 1));
        }
        _index = line;
        return (content, startLine, endLine);
    }

    /// Consumes a value we do not model: block scalars advance the index, flow collections
    /// stay opaque, everything else is a plain scalar.
    private void ConsumeOpaqueValue(string value, int column)
    {
        if (IsBlockHeader(value)) { ParseBlockHeader(value); ConsumeBlock(column); }
        else ClassifyValue(value);
    }

    /// Validates a block scalar header (chomping indicators only); explicit indentation
    /// indicators are unsupported.
    private static void ParseBlockHeader(string value)
    {
        var chompSeen = false;
        for (var i = 1; i < value.Length; i++)
        {
            if (value[i] is '-' or '+')
            {
                if (chompSeen) Unsupported("block scalar header");
                chompSeen = true;
            }
            else if (char.IsDigit(value[i])) Unsupported("explicit block indentation indicators");
            else Unsupported("block scalar header");
        }
    }

    /// Classifies a scalar value: quoted values are unescaped, single-line flow
    /// collections return empty (opaque), and constructs outside the subset fail the file.
    private static string ClassifyValue(string value)
    {
        switch (value[0])
        {
            case '&' or '*' or '!' or '`' or '@' or '%':
                Unsupported($"YAML '{value[0]}' construct");
                return string.Empty;
            case '"' or '\'':
                return Unquote(value);
            case '[' or '{':
                RequireBalancedFlow(value);
                return string.Empty;
            default:
                return value;
        }
    }

    private static void RequireBalancedFlow(string value)
    {
        var square = 0;
        var curly = 0;
        Walk(value, (_, c, _) =>
        {
            if (c == '[') square++;
            else if (c == ']') square--;
            else if (c == '{') curly++;
            else if (c == '}') curly--;
        });
        if (square != 0 || curly != 0) Unsupported("multi-line flow collections");
    }

    private static string Unquote(string value)
    {
        var quote = value[0];
        var builder = new StringBuilder();
        for (var i = 1; i < value.Length; i++)
        {
            var c = value[i];
            if (quote == '\'' && c == '\'')
            {
                if (i + 1 < value.Length && value[i + 1] == '\'') { builder.Append('\''); i++; continue; }
                if (i + 1 != value.Length) Unsupported("text after closing quote");
                return builder.ToString();
            }
            if (quote == '"' && c == '\\' && i + 1 < value.Length && value[i + 1] is '"' or '\\')
            {
                builder.Append(value[i + 1]);
                i++;
                continue;
            }
            if (quote == '"' && c == '\\') Unsupported("unmodeled double-quoted YAML escape");
            if (quote == '"' && c == '"')
            {
                if (i + 1 != value.Length) Unsupported("text after closing quote");
                return builder.ToString();
            }
            builder.Append(c);
        }
        Unsupported("unterminated quoted scalar");
        return string.Empty;
    }

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private static string[] SplitLines(byte[] bytes)
    {
        // Malformed bytes are invalid evidence, never silently replacement-decoded text.
        string text;
        try
        {
            text = StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            Fail("malformed UTF-8");
            return [];
        }
        if (text.StartsWith('\ufeff')) text = text[1..];
        return text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
    }
}

    // ---- shared quote/expression scanner -------------------------------------------------------

    /// Visits characters that are outside quotes and outside ${{ }} expressions. A quote
    /// starts only at a token boundary (start, after whitespace or structural punctuation).
    private static void Walk(string text, Action<int, char, char?> visit)
    {
        var inSingle = false;
        var inDouble = false;
        var expression = 0;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (expression > 0)
            {
                if (c == '}' && i + 1 < text.Length && text[i + 1] == '}') expression--;
                continue;
            }
            if (inSingle)
            {
                if (c == '\'') inSingle = false;
                continue;
            }
            if (inDouble)
            {
                if (c == '\\' && i + 1 < text.Length) i++;
                else if (c == '"') inDouble = false;
                continue;
            }
            if (c is '\'' or '"')
            {
                char? previous = i == 0 ? null : text[i - 1];
                if (previous is null or ' ' or '\t' or ':' or '-' or ',' or '[' or '{' or '=')
                {
                    if (c == '\'') inSingle = true;
                    else inDouble = true;
                    continue;
                }
            }
            if (c == '$' && i + 2 < text.Length && text[i + 1] == '{' && text[i + 2] == '{')
            {
                expression++;
                i += 2;
                continue;
            }
            visit(i, c, i == 0 ? null : text[i - 1]);
        }
    }

    /// Strips a trailing comment: '#' at the start or after whitespace, outside quotes and
    /// expressions.
    private static string StripComment(string content)
    {
        var cut = -1;
        Walk(content, (index, c, previous) =>
        {
            if (cut < 0 && c == '#' && previous is null or ' ' or '\t') cut = index;
        });
        return cut < 0 ? content : content[..cut];
    }

    /// Splits "key: value" at the first colon outside quotes and expressions; the colon must
    /// end the line or be followed by a space. A trailing colon yields an empty value (a
    /// nested block follows).
    private static (string Key, string Value)? SplitKey(string content)
    {
        var colon = -1;
        Walk(content, (index, c, _) =>
        {
            if (colon >= 0 || c != ':') return;
            if (index == content.Length - 1 || content[index + 1] == ' ') colon = index;
        });
        if (colon < 0) return null;
        if (colon == content.Length - 1) return (content[..colon].TrimEnd(), string.Empty);
        return (content[..colon].TrimEnd(), content[(colon + 1)..].TrimStart());
    }

    private static string Truncate(string value) => value.Length <= 48 ? value : value[..48] + "…";

    // ---- pack command analysis ----------------------------------------------------------------

    private static bool ContainsPackCommand(RunScript script) => SplitCommands(script).Any(command =>
    {
        var tokens = Tokenize(command.Text);
        return tokens.Count >= 2
            && tokens[0].Equals("dotnet", StringComparison.OrdinalIgnoreCase)
            && tokens[1].Equals("pack", StringComparison.OrdinalIgnoreCase);
    });

    private static bool IsLineOrientedScript(RunScript script)
    {
        foreach (var (raw, _) in script.Lines)
        {
            var text = StripShellComment(raw);
            // Here-documents, PowerShell here-strings/block comments and substitutions
            // can contain pack-looking data. They need a real shell parser; gap the block.
            if (text.Contains("<<", StringComparison.Ordinal)
                || text.Contains("@\"", StringComparison.Ordinal) || text.Contains("@'", StringComparison.Ordinal)
                || text.Contains("<#", StringComparison.Ordinal) || text.Contains('`')
                || text.Contains("$(", StringComparison.Ordinal)) return false;
            char quote = '\0';
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (c == '\\' && quote != '\'' && i + 1 < text.Length) { i++; continue; }
                if (quote != '\0') { if (c == quote) quote = '\0'; continue; }
                if (c is '\'' or '"') quote = c;
            }
            if (quote != '\0') return false;
        }
        return true;
    }

    private sealed record PackOccurrence(
        string WorkflowPath, string? RawPackageId, string? RawVersion,
        string? ProjectTarget, bool TargetUnresolvable, int StartLine, int EndLine,
        IReadOnlyDictionary<string, string> Env);

    private static readonly string[] ValueTakingFlags =
    [
        "-o", "--output", "-c", "--configuration", "-f", "--framework", "-r", "--runtime",
        "--version-suffix", "-v", "--verbosity", "--artifacts-path", "-bl", "-m", "--maxcpucount",
        "-nr", "--nodereuse"
    ];

    private static IEnumerable<PackOccurrence> ExtractPackOccurrences(
        string workflowPath, string root, RunScript script)
    {
        foreach (var (text, startLine, endLine) in SplitCommands(script))
        {
            var tokens = Tokenize(text);
            if (tokens.Count < 2
                || !tokens[0].Equals("dotnet", StringComparison.OrdinalIgnoreCase)
                || !tokens[1].Equals("pack", StringComparison.OrdinalIgnoreCase))
                continue;
            string? packageId = null;
            string? packageVersion = null;
            string? plainVersion = null;
            string? target = null;
            var unresolvableTarget = false;
            for (var i = 2; i < tokens.Count; i++)
            {
                var token = tokens[i];
                if (TryReadPropertyAssignment(token, out var name, out var assignedValue))
                {
                    if (assignedValue.Contains(';') || assignedValue.Contains(','))
                    {
                        packageId = "${{ unsupported-property-list }}";
                        break;
                    }
                    if (name.Equals("PackageId", StringComparison.OrdinalIgnoreCase)) packageId = assignedValue;
                    else if (name.Equals("PackageVersion", StringComparison.OrdinalIgnoreCase)) packageVersion = assignedValue;
                    else if (name.Equals("Version", StringComparison.OrdinalIgnoreCase)) plainVersion = assignedValue;
                    continue;
                }
                if (token.StartsWith('-'))
                {
                    // Only target detection depends on knowing value-taking flags; a missed
                    // flag can never invent a wrong target (targets must end in a project or
                    // solution extension).
                    var flag = token.Split(':', '=')[0];
                    if (token == flag && ValueTakingFlags.Contains(flag, StringComparer.OrdinalIgnoreCase)) i++;
                    continue;
                }
                if (target is null && unresolvableTarget == false && IsProjectOrSolutionPath(token))
                    target = ResolveTarget(root, script.WorkingDir, token, ref unresolvableTarget);
            }
            yield return new PackOccurrence(workflowPath, packageId, packageVersion ?? plainVersion,
                target, unresolvableTarget, startLine, endLine, script.Env);
        }
    }

    private static bool TryReadPropertyAssignment(string token, out string name, out string value)
    {
        name = string.Empty;
        value = string.Empty;
        string? assignment = null;
        if (token.Length > 3 && (token.StartsWith("-p:", StringComparison.Ordinal) || token.StartsWith("-P:", StringComparison.Ordinal)))
            assignment = token[3..];
        else if (token.Length > 3 && token.StartsWith("/p:", StringComparison.OrdinalIgnoreCase))
            assignment = token[3..];
        else if (token.Length > 10 && (token.StartsWith("-property:", StringComparison.OrdinalIgnoreCase)
            || token.StartsWith("/property:", StringComparison.OrdinalIgnoreCase)))
            assignment = token[10..];
        else if (token.Length > 11 && token.StartsWith("--property:", StringComparison.OrdinalIgnoreCase))
            assignment = token[11..];
        if (assignment is null) return false;
        var split = assignment.IndexOf('=');
        if (split <= 0) return false;
        name = assignment[..split];
        value = assignment[(split + 1)..];
        return true;
    }

    private static bool IsProjectOrSolutionPath(string token) =>
        token.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
        || token.EndsWith(".vbproj", StringComparison.OrdinalIgnoreCase)
        || token.EndsWith(".sln", StringComparison.OrdinalIgnoreCase);

    /// Resolves a pack target against the run's effective working directory. A relative
    /// target under an unprovable directory is unresolvable (attribution omitted, never
    /// guessed); absolute targets resolve on their own.
    private static string? ResolveTarget(string root, WorkingDirectory workingDirectory, string token, ref bool unresolvable)
    {
        try
        {
            var baseDirectory = workingDirectory.Known
                ? Path.Combine(root, workingDirectory.Path ?? string.Empty)
                : null;
            if (!Path.IsPathRooted(token) && baseDirectory is null)
            {
                unresolvable = true;
                return null;
            }
            var relative = Path.GetRelativePath(root, Path.GetFullPath(token, baseDirectory ?? root))
                .Replace('\\', '/');
            return relative.StartsWith("../", StringComparison.Ordinal) || relative == ".." ? null : relative;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    /// Logical shell commands with their source line spans: shell comments stripped,
    /// line continuations joined, then split on shell separators outside quotes and
    /// ${{ }} expressions.
    private static IEnumerable<(string Text, int Start, int End)> SplitCommands(RunScript script)
    {
        if (script.Folded)
        {
            foreach (var piece in SplitOnOperators(string.Join(" ", script.Lines.Select(line => StripShellComment(line.Text)))))
                yield return (piece, script.StartLine, script.EndLine);
            yield break;
        }
        var lineIndex = 0;
        while (lineIndex < script.Lines.Count)
        {
            var start = script.Lines[lineIndex].LineNo;
            var end = start;
            var text = new StringBuilder(StripShellComment(script.Lines[lineIndex].Text));
            while (EndsWithLineContinuation(text))
            {
                text.Length -= 1;
                lineIndex++;
                if (lineIndex >= script.Lines.Count) break;
                end = script.Lines[lineIndex].LineNo;
                text.Append(' ').Append(StripShellComment(script.Lines[lineIndex].Text).TrimStart());
            }
            foreach (var piece in SplitOnOperators(text.ToString()))
                yield return (piece, start, end);
            lineIndex++;
        }
    }

    /// An unquoted '#' at a word boundary starts a shell comment that runs to end of line.
    private static string StripShellComment(string text)
    {
        var cut = -1;
        Walk(text, (index, c, previous) =>
        {
            if (cut < 0 && c == '#' && previous is null or ' ' or '\t') cut = index;
        });
        return cut < 0 ? text : text[..cut];
    }

    private static bool EndsWithLineContinuation(StringBuilder text)
    {
        var backslashes = 0;
        for (var i = text.Length - 1; i >= 0 && text[i] == '\\'; i--) backslashes++;
        return backslashes % 2 == 1;
    }

    private static IEnumerable<string> SplitOnOperators(string text)
    {
        var pieces = new List<string>();
        var pieceStart = 0;
        var inSingle = false;
        var inDouble = false;
        var expression = 0;
        void Cut(int index)
        {
            pieces.Add(text[pieceStart..index]);
            pieceStart = index + 1;
        }
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (expression > 0)
            {
                if (c == '}' && i + 1 < text.Length && text[i + 1] == '}') expression--;
                continue;
            }
            if (inSingle)
            {
                if (c == '\'') inSingle = false;
                continue;
            }
            if (inDouble)
            {
                if (c == '\\' && i + 1 < text.Length) i++;
                else if (c == '"') inDouble = false;
                continue;
            }
            if (c is '\'' or '"')
            {
                char? previous = i == 0 ? null : text[i - 1];
                if (previous is null or ' ' or '\t' or ':' or '-' or ',' or '[' or '{' or '=')
                {
                    if (c == '\'') inSingle = true;
                    else inDouble = true;
                    continue;
                }
            }
            if (c == '$' && i + 2 < text.Length && text[i + 1] == '{' && text[i + 2] == '{') { expression++; i += 2; continue; }
            if (c == '&' && i + 1 < text.Length && text[i + 1] == '&') { Cut(i); i++; pieceStart = i + 1; }
            else if (c == '|' && i + 1 < text.Length && text[i + 1] == '|') { Cut(i); i++; pieceStart = i + 1; }
            else if (c is '&' or '|' or ';') Cut(i);
        }
        pieces.Add(text[pieceStart..]);
        return pieces.Where(piece => piece.Trim().Length > 0).Select(piece => piece.Trim());
    }

    /// Whitespace tokenization with shell quoting: quoted regions stay part of one token
    /// and lose their quote characters. ${{ }} expressions never break a token — GitHub
    /// substitutes them before the shell parses the command line.
    private static List<string> Tokenize(string command)
    {
        var tokens = new List<string>();
        var current = new StringBuilder();
        var hasToken = false;
        var inSingle = false;
        var inDouble = false;
        var expression = 0;
        void Flush()
        {
            if (!hasToken) return;
            tokens.Add(current.ToString());
            current.Clear();
            hasToken = false;
        }
        for (var i = 0; i < command.Length; i++)
        {
            var c = command[i];
            if (expression > 0)
            {
                current.Append(c);
                if (c == '}' && i + 1 < command.Length && command[i + 1] == '}') { current.Append(command[++i]); expression--; }
                continue;
            }
            if (inSingle)
            {
                if (c == '\'') inSingle = false;
                else current.Append(c);
                continue;
            }
            if (inDouble)
            {
                if (c == '\\'
                    && i + 1 < command.Length
                    && (command[i + 1] == '"' || command[i + 1] == '\\' || char.IsWhiteSpace(command[i + 1])))
                {
                    current.Append(command[i + 1]);
                    i++;
                    continue;
                }
                if (c == '"') inDouble = false;
                else current.Append(c);
                continue;
            }
            if (c == '\'' && hasToken) { inSingle = true; continue; }
            if (c == '"' && hasToken) { inDouble = true; continue; }
            if (c == '\'' || c == '"') { inSingle = c == '\''; inDouble = c == '"'; hasToken = true; continue; }
            if (c == '$' && i + 2 < command.Length && command[i + 1] == '{' && command[i + 2] == '{')
            {
                current.Append("${{");
                i += 2;
                expression++;
                hasToken = true;
                continue;
            }
            if (c == '\\' && i + 1 < command.Length) { current.Append(command[i + 1]); i++; hasToken = true; continue; }
            if (char.IsWhiteSpace(c)) { Flush(); continue; }
            current.Append(c);
            hasToken = true;
        }
        Flush();
        return tokens;
    }

    private static void ResolveOccurrence(
        PackOccurrence occurrence,
        IReadOnlyDictionary<string, ProducedPackageInfo> producedByPath,
        List<CiWorkflowProducerRow> rows,
        List<CiWorkflowGap> gaps)
    {
        string? packageId = null;
        if (occurrence.RawPackageId is { } rawId)
        {
            // Effective id AFTER CI overrides: the flag wins; anything unevidenced is skipped.
            if (IsTemplated(rawId))
                gaps.Add(new(occurrence.WorkflowPath, "ci-producer-id-unevidenced",
                    $"templated PackageId {Truncate(rawId)}", occurrence.StartLine));
            else if (!ProjectFileReader.IsSafeNuGetPackageId(rawId))
                gaps.Add(new(occurrence.WorkflowPath, "ci-producer-id-unevidenced",
                    "unsafe PackageId", occurrence.StartLine));
            else packageId = rawId;
        }
        else if (occurrence.ProjectTarget is { } target
            && !target.EndsWith(".sln", StringComparison.OrdinalIgnoreCase)
            && producedByPath.TryGetValue(target, out var produced)
            && produced.ExplicitPackageId)
        {
            packageId = produced.PackageId; // CI packs the project: its explicit declaration is the id evidence
        }
        else if (occurrence.TargetUnresolvable)
        {
            gaps.Add(new(occurrence.WorkflowPath, "ci-producer-id-unevidenced",
                "relative target under an unprovable working directory", occurrence.StartLine));
        }
        else if (occurrence.ProjectTarget is { } solution && solution.EndsWith(".sln", StringComparison.OrdinalIgnoreCase))
        {
            gaps.Add(new(occurrence.WorkflowPath, "ci-producer-id-unevidenced",
                "solution packs cannot be attributed to one package id", occurrence.StartLine));
        }
        else if (occurrence.ProjectTarget is null)
        {
            gaps.Add(new(occurrence.WorkflowPath, "ci-producer-id-unevidenced",
                "no explicit project target", occurrence.StartLine));
        }
        else
        {
            gaps.Add(new(occurrence.WorkflowPath, "ci-producer-id-unevidenced",
                "packed project declares no explicit PackageId", occurrence.StartLine));
        }
        if (packageId is null) return;

        string? version = null;
        if (occurrence.RawVersion is { } rawVersion)
        {
            var resolved = ResolveTemplate(rawVersion, occurrence.Env);
            if (resolved is null)
                gaps.Add(new(occurrence.WorkflowPath, "ci-producer-version-template",
                    $"unresolved version template {Truncate(rawVersion)}", occurrence.StartLine));
            else if (!ProjectFileReader.IsSafeNuGetResolvedVersion(resolved))
                gaps.Add(new(occurrence.WorkflowPath, "ci-producer-version-unsafe",
                    "unsafe literal version", occurrence.StartLine));
            else version = resolved;
        }
        // projectPath attributes the packed PROJECT; solution targets never ride the fact.
        rows.Add(new CiWorkflowProducerRow(occurrence.WorkflowPath, packageId, version,
            occurrence.ProjectTarget is { } attributed
                && !attributed.EndsWith(".sln", StringComparison.OrdinalIgnoreCase)
                ? attributed
                : null,
            occurrence.StartLine, occurrence.EndLine));
    }

    /// Resolves a flag value that is exactly one ${{ env.NAME }} expression against the
    /// workflow's own literal env definitions (step over job over workflow scope). Every
    /// other template or shell-variable form is unevidence.
    private static string? ResolveTemplate(string value, IReadOnlyDictionary<string, string> env)
    {
        if (!value.Contains("${{", StringComparison.Ordinal))
            return value.StartsWith('$') ? null : value; // shell variable form: unevidence
        var match = EnvExpression.Match(value.Trim());
        return match.Success && env.TryGetValue(match.Groups["name"].Value, out var literal)
            && !string.IsNullOrEmpty(literal)
            ? literal
            : null;
    }

    private static bool IsTemplated(string value) =>
        value.Contains("${{", StringComparison.Ordinal) || value.StartsWith('$');
}
