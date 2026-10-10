using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
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
        if (Directory.Exists(workflows))
        {
            if (File.GetAttributes(workflows).HasFlag(FileAttributes.ReparsePoint))
                gaps.Add(new(".github/workflows", "ci-workflow-linked-path"));
            else if (comparer.Equals(workflows.TrimEnd(Path.DirectorySeparatorChar), output)
                || workflows.StartsWith(output + Path.DirectorySeparatorChar,
                    comparer == StringComparer.OrdinalIgnoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                gaps.Add(new(".github/workflows", "ci-workflow-output-boundary"));
            else
                ReadWorkflows(root, workflows, options, comparer, producedByPath, rows, gaps, observed,
                    ref files, ref bytesRead, cancellationToken);
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
            if (File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint))
            {
                gaps.Add(new(relative, "ci-workflow-linked-path"));
                continue;
            }
            if (++files > MaxFiles)
            {
                gaps.Add(new(relative, "ci-workflow-file-limit"));
                return;
            }
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
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
                ParseWorkflow(relative, bytes, root, producedByPath, rows, gaps);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                gaps.Add(new(relative, "ci-workflow-read-failed"));
            }
        }
    }

    /// Same package claimed by CI at one effective version collapses to the first deterministic
    /// occurrence; two different versions are a self-contradiction gap with no fact (a human resolves).
    private static IReadOnlyList<CiWorkflowProducerRow> ResolveConflicts(
        List<CiWorkflowProducerRow> rows, List<CiWorkflowGap> gaps)
    {
        var resolved = new List<CiWorkflowProducerRow>();
        foreach (var group in rows.GroupBy(row => row.Package, StringComparer.Ordinal))
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
            resolved.Add(ordered[0] with
            {
                Version = versions.FirstOrDefault(),
                ProjectPath = ordered.Select(row => row.ProjectPath).FirstOrDefault(path => path is not null)
            });
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
        List<CiWorkflowGap> gaps)
    {
        try
        {
            foreach (var script in new WorkflowParser(bytes).Parse())
                foreach (var occurrence in ExtractPackOccurrences(path, root, script))
                    ResolveOccurrence(occurrence, producedByPath, rows, gaps);
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

    /// A run block captured from jobs.<id>.steps[*].run with its effective literal env
    /// (step over job over workflow scope).
    private sealed record RunScript(
        IReadOnlyList<(string Text, int LineNo)> Lines, bool Folded, int StartLine, int EndLine,
        IReadOnlyDictionary<string, string> Env);

    private sealed class WorkflowParser(byte[] bytes)
    {
        private readonly string[] _lines = SplitLines(bytes);
        private readonly Dictionary<string, string> _workflowEnv = new(StringComparer.Ordinal);
        private readonly List<RunScript> _runs = [];
        private readonly Stack<Frame> _frames = [];
        private Dictionary<string, string>? _jobEnv;
        private Dictionary<string, string>? _stepEnv;
        private RunScript? _stepRun;
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
            return _runs;
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
                _ => "other"
            };
            if (role is "jobs" or "job" or "env-workflow" or "env-job" or "env-step" && isDash)
                Fail($"{(role == "job" ? "job" : role.StartsWith("env-", StringComparison.Ordinal) ? "env" : role)} must be a mapping block");
            if (role == "steps" && !isDash) Fail("steps must be a sequence block");
            if (role == "job") _jobEnv = new Dictionary<string, string>(StringComparer.Ordinal);
            _frames.Push(new Frame(indent, isDash)
            {
                Role = role,
                JobId = role == "job" ? open.Key : open.Parent.JobId
            });
        }

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
            if (item.Step) _stepEnv = [];
        }

        private void Pop()
        {
            var frame = _frames.Pop();
            if (frame.Role != "step") return;
            if (_stepRun is { } script)
            {
                var effective = new Dictionary<string, string>(_workflowEnv, StringComparer.Ordinal);
                if (_jobEnv is not null) foreach (var (name, literal) in _jobEnv) effective[name] = literal;
                if (_stepEnv is not null) foreach (var (name, literal) in _stepEnv) effective[name] = literal;
                _runs.Add(script with { Env = effective });
            }
            _stepRun = null;
            _stepEnv = null;
        }

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
            if (sequence.Role == "steps") _stepEnv = [];
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
            if (mapping.Role is "env-workflow" or "env-job" or "env-step")
            {
                if (IsBlockHeader(value)) { ParseBlockHeader(value); ConsumeBlock(column); return; }
                if (!value.Contains("${{", StringComparison.Ordinal) && ClassifyValue(value) is { Length: > 0 } literal)
                {
                    switch (mapping.Role)
                    {
                        case "env-workflow": _workflowEnv[key] = literal; break;
                        case "env-job": _jobEnv?.Add(key, literal); break;
                        default: _stepEnv?.Add(key, literal); break;
                    }
                }
                return; // templated, flow, or empty env values leave the variable unresolved
            }
            ConsumeOpaqueValue(value, column);
        }

        private RunScript? ParseRunValue(string value, int keyColumn, int lineNo)
        {
            if (IsBlockHeader(value))
            {
                ParseBlockHeader(value);
                var (contentLines, startLine, endLine) = ConsumeBlock(keyColumn);
                if (contentLines.Count == 0) return null;
                return value[0] == '>'
                    ? new RunScript([(string.Join(" ", contentLines.Select(line => line.Text)), startLine)],
                        Folded: true, startLine, endLine, EmptyEnv)
                    : new RunScript(contentLines, Folded: false, startLine, endLine, EmptyEnv);
            }
            var scalar = ClassifyValue(value);
            if (scalar.Length == 0) Unsupported("run must be a scalar or block scalar");
            return new RunScript([(scalar, lineNo)], Folded: false, lineNo, lineNo, EmptyEnv);
        }

        private static readonly IReadOnlyDictionary<string, string> EmptyEnv =
            new Dictionary<string, string>(StringComparer.Ordinal);

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

        private static string[] SplitLines(byte[] bytes)
        {
            var text = Encoding.UTF8.GetString(bytes);
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

    private sealed record PackOccurrence(
        string WorkflowPath, string? RawPackageId, string? RawVersion,
        string? ProjectTarget, int StartLine, int EndLine, IReadOnlyDictionary<string, string> Env);

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
            for (var i = 2; i < tokens.Count; i++)
            {
                var token = tokens[i];
                if (TryReadPropertyAssignment(token, out var name, out var assignedValue))
                {
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
                    if (ValueTakingFlags.Contains(flag, StringComparer.OrdinalIgnoreCase)) i++;
                    continue;
                }
                if (target is null && IsProjectOrSolutionPath(token)) target = NormalizeTarget(root, token);
            }
            yield return new PackOccurrence(workflowPath, packageId, packageVersion ?? plainVersion,
                target, startLine, endLine, script.Env);
        }
    }

    private static bool TryReadPropertyAssignment(string token, out string name, out string value)
    {
        name = string.Empty;
        value = string.Empty;
        string? assignment = null;
        if (token.Length > 3 && (token.StartsWith("-p:", StringComparison.Ordinal) || token.StartsWith("-P:", StringComparison.Ordinal)))
            assignment = token[3..];
        else if (token.Length > 11 && token.StartsWith("--property:", StringComparison.Ordinal))
            assignment = token[11..];
        if (assignment is null) return false;
        var split = assignment.IndexOf('=');
        if (split <= 0 || split == assignment.Length - 1) return false;
        name = assignment[..split];
        value = assignment[(split + 1)..];
        return true;
    }

    private static bool IsProjectOrSolutionPath(string token) =>
        token.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
        || token.EndsWith(".vbproj", StringComparison.OrdinalIgnoreCase)
        || token.EndsWith(".sln", StringComparison.OrdinalIgnoreCase);

    private static string? NormalizeTarget(string root, string token)
    {
        try
        {
            var relative = Path.GetRelativePath(root, Path.GetFullPath(token, root)).Replace('\\', '/');
            return relative.StartsWith("../", StringComparison.Ordinal) || relative == ".." ? null : relative;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    /// Logical shell commands with their source line spans: line continuations joined,
    /// then split on shell separators outside quotes and ${{ }} expressions.
    private static IEnumerable<(string Text, int Start, int End)> SplitCommands(RunScript script)
    {
        if (script.Folded)
        {
            foreach (var piece in SplitOnOperators(script.Lines[0].Text))
                yield return (piece, script.StartLine, script.EndLine);
            yield break;
        }
        var lineIndex = 0;
        while (lineIndex < script.Lines.Count)
        {
            var start = script.Lines[lineIndex].LineNo;
            var end = start;
            var text = new StringBuilder(script.Lines[lineIndex].Text);
            while (EndsWithLineContinuation(text))
            {
                text.Length -= 1;
                lineIndex++;
                if (lineIndex >= script.Lines.Count) break;
                end = script.Lines[lineIndex].LineNo;
                text.Append(' ').Append(script.Lines[lineIndex].Text.TrimStart());
            }
            foreach (var piece in SplitOnOperators(text.ToString()))
                yield return (piece, start, end);
            lineIndex++;
        }
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
            if (c == '&' && i + 1 < text.Length && text[i + 1] == '&') { Cut(i); i++; }
            else if (c == '|' && i + 1 < text.Length && text[i + 1] == '|') { Cut(i); i++; }
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
        rows.Add(new CiWorkflowProducerRow(occurrence.WorkflowPath, packageId, version,
            occurrence.ProjectTarget, occurrence.StartLine, occurrence.EndLine));
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
            ? literal
            : null;
    }

    private static bool IsTemplated(string value) =>
        value.Contains("${{", StringComparison.Ordinal) || value.StartsWith('$');
}
