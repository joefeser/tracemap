using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TraceMap.Reporting;

/// <summary>Builds a bounded, local-only working-tree source review from a batch inspection.</summary>
public static class WebFormsCodePathReview
{
    private sealed record ReviewLocation(string Role, string FilePath, int StartLine, int EndLine, string? Caller, string? Callee, bool PreferFullSpan = false);

    public static IReadOnlyList<string> Run(string inspectionPath, string sourceRoot, string caseId, string outputPath,
        int contextLines = 4, int maxExcerpts = 64, int maxSourceBytes = 16 * 1024 * 1024)
    {
        if (contextLines is < 0 or > 12 || maxExcerpts is < 1 or > 128 || maxSourceBytes is < 1 or > 32 * 1024 * 1024)
            throw new InvalidDataException("CodePathReviewInvalidLimit");
        if (!System.Text.RegularExpressions.Regex.IsMatch(caseId, "^case-[0-9]{3}$"))
            throw new InvalidDataException("CodePathReviewCaseInvalid");
        var inspection = new FileInfo(inspectionPath);
        if (!inspection.Exists || inspection.Length > 32 * 1024 * 1024) throw new InvalidDataException("CodePathReviewInspectionUnavailable");
        var rootPath = Path.GetFullPath(sourceRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!Directory.Exists(rootPath)) throw new InvalidDataException("CodePathReviewSourceRootUnavailable");
        if (File.Exists(outputPath)) throw new IOException("CodePathReviewOutputExists");

        using var document = JsonDocument.Parse(File.ReadAllText(inspectionPath));
        var root = document.RootElement;
        if (!root.TryGetProperty("schemaVersion", out var schema) || schema.GetString() != "webforms-batch-inspection.v1")
            throw new InvalidDataException("CodePathReviewSchemaMismatch");
        var matches = root.GetProperty("cases").EnumerateArray().Where(c => c.GetProperty("caseId").GetString() == caseId).ToArray();
        if (matches.Length != 1) throw new InvalidDataException("CodePathReviewCaseUnavailable");
        var selected = matches[0];
        var locations = new List<ReviewLocation>();

        static ReviewLocation? ReadLocation(JsonElement value, string role, string? caller = null, string? callee = null, bool full = false)
        {
            if (value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ||
                !value.TryGetProperty("filePath", out var path) || string.IsNullOrWhiteSpace(path.GetString()) ||
                !value.TryGetProperty("startLine", out var start) || start.GetInt32() < 1) return null;
            var end = value.TryGetProperty("endLine", out var endValue) ? Math.Max(start.GetInt32(), endValue.GetInt32()) : start.GetInt32();
            return new(role, path.GetString()!, start.GetInt32(), end, caller, callee, full);
        }

        void Add(JsonElement value, string role, string? caller = null, string? callee = null, bool full = false)
        {
            var location = ReadLocation(value, role, caller, callee, full);
            if (location is not null) locations.Add(location);
        }

        Add(selected.GetProperty("handlerLocation"), "handler", full: true);
        foreach (var binding in selected.GetProperty("bindings").EnumerateArray())
            Add(binding.GetProperty("bindingLocation"), "event-binding");
        foreach (var method in selected.GetProperty("methods").EnumerateArray())
        {
            var symbol = method.GetProperty("symbol").GetString();
            foreach (var declaration in method.GetProperty("exactDeclarationLocations").EnumerateArray())
                Add(declaration, "exact-declaration", callee: symbol, full: true);
            foreach (var call in method.GetProperty("outgoingCallSites").EnumerateArray())
                Add(call, "retained-call", call.GetProperty("caller").GetString(), call.GetProperty("callee").GetString());
        }

        var deduplicated = locations
            .GroupBy(l => new { l.Role, l.FilePath, l.StartLine, l.EndLine, l.Caller, l.Callee })
            .Select(g => g.First()).OrderBy(l => l.FilePath, StringComparer.Ordinal).ThenBy(l => l.StartLine).ThenBy(l => l.Role, StringComparer.Ordinal)
            .ToList();
        var sourceFiles = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        long bytesRead = 0;
        string ResolveSource(string relativePath)
        {
            if (Path.IsPathRooted(relativePath)) throw new InvalidDataException("CodePathReviewSourcePathInvalid");
            var candidate = Path.GetFullPath(Path.Combine(rootPath, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            var prefix = rootPath + Path.DirectorySeparatorChar;
            if (!candidate.StartsWith(prefix, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                throw new InvalidDataException("CodePathReviewSourcePathInvalid");
            return candidate;
        }
        string[] ReadSource(string relativePath)
        {
            if (sourceFiles.TryGetValue(relativePath, out var lines)) return lines;
            var path = ResolveSource(relativePath);
            var info = new FileInfo(path);
            if (!info.Exists || info.Length > 4 * 1024 * 1024 || (bytesRead += info.Length) > maxSourceBytes)
                throw new InvalidDataException("CodePathReviewSourceUnavailable");
            return sourceFiles[relativePath] = File.ReadAllLines(path);
        }

        // Definition candidates are navigation aids, not evidence. Restrict name lookup
        // to already witnessed C# files and publish only globally unique candidates.
        var evidenceFiles = deduplicated.Select(l => l.FilePath).Where(p => p.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal).ToArray();
        var candidateCount = 0;
        var unresolvedLeaves = selected.TryGetProperty("unresolvedOtherLeaves", out var unresolvedValues)
            ? unresolvedValues.EnumerateArray().Select(v => v.GetString()).ToArray()
            : selected.GetProperty("methods").EnumerateArray()
                .Where(m => m.TryGetProperty("stopReason", out var reason) && reason.GetString() != "retained-outgoing-calls")
                .Select(m => m.GetProperty("symbol").GetString()).ToArray();
        foreach (var unresolved in unresolvedLeaves.Where(v => !string.IsNullOrWhiteSpace(v)))
        {
            var open = unresolved!.IndexOf('(');
            if (open < 1) continue;
            var dot = unresolved.LastIndexOf('.', open - 1);
            var name = unresolved[(dot + 1)..open];
            var candidates = new List<ReviewLocation>();
            foreach (var relativePath in evidenceFiles)
            {
                var text = string.Join(Environment.NewLine, ReadSource(relativePath));
                var syntax = CSharpSyntaxTree.ParseText(text, path: relativePath).GetCompilationUnitRoot();
                foreach (var method in syntax.DescendantNodes().OfType<MethodDeclarationSyntax>().Where(m => m.Identifier.ValueText == name))
                {
                    var span = method.GetLocation().GetLineSpan();
                    candidates.Add(new("unique-name-definition-candidate-not-evidence", relativePath,
                        span.StartLinePosition.Line + 1, span.EndLinePosition.Line + 1, null, unresolved, true));
                }
            }
            if (candidates.Count == 1)
            {
                deduplicated.Add(candidates[0]);
                candidateCount++;
            }
        }
        deduplicated = deduplicated.GroupBy(l => new { l.Role, l.FilePath, l.StartLine, l.EndLine, l.Caller, l.Callee })
            .Select(g => g.First()).OrderBy(l => l.Role == "handler" ? 0 : l.Role == "event-binding" ? 1 : l.Role == "retained-call" ? 2 : 3)
            .ThenBy(l => l.FilePath, StringComparer.Ordinal).ThenBy(l => l.StartLine).Take(maxExcerpts + 1).ToList();
        if (deduplicated.Count > maxExcerpts) throw new InvalidDataException("CodePathReviewExcerptLimit");

        static string Safe(string? value) => (value ?? "unavailable").Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal).Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("|", "&#124;", StringComparison.Ordinal).Replace("`", "&#96;", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
        static string BoundedSourceLine(string value) => value.Length <= 500 ? value : value[..500] + " … [line truncated]";
        var destination = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var temporary = destination + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(file, new UTF8Encoding(false)))
            {
                writer.WriteLine($"# Local Web Forms code-path review: {caseId}\n");
                writer.WriteLine("PRIVATE: this report contains working-tree source excerpts. Keep it on the work machine.\n");
                writer.WriteLine($"- Inspection commit: `{Safe(root.GetProperty("commitSha").GetString())}`");
                writer.WriteLine("- Source mode: `working-tree` (Git is not required and commit equality is not established)");
                writer.WriteLine($"- Retained evidence conclusion: `{Safe(selected.TryGetProperty("evidenceConclusion", out var conclusion) ? conclusion.GetString() : "unavailable-from-older-inspection")}`");
                writer.WriteLine($"- Traversal limit reached: `{selected.GetProperty("bounded").GetBoolean().ToString().ToLowerInvariant()}`");
                writer.WriteLine("- Rule: `diagnostic.webforms.local-code-path-review.v1`\n");
                writer.WriteLine("The ordering below is static retained call evidence, not runtime execution order or branch feasibility. Unique-name definition candidates are navigation aids and are not evidence. Missing source or calls do not prove absence.\n");
                writer.WriteLine("## Retained call path\n");
                writer.WriteLine("| Caller | Callee | Location |\n|---|---|---|");
                foreach (var location in deduplicated.Where(l => l.Role == "retained-call"))
                    writer.WriteLine($"| {Safe(location.Caller)} | {Safe(location.Callee)} | {Safe(location.FilePath)}:{location.StartLine} |");
                writer.WriteLine("\n## Source excerpts\n");
                foreach (var location in deduplicated)
                {
                    var lines = ReadSource(location.FilePath);
                    if (location.StartLine > lines.Length) throw new InvalidDataException("CodePathReviewSourceSpanInvalid");
                    var desiredStart = location.PreferFullSpan ? location.StartLine : Math.Max(1, location.StartLine - contextLines);
                    var desiredEnd = location.PreferFullSpan ? location.EndLine : Math.Min(lines.Length, location.EndLine + contextLines);
                    var excerptEnd = Math.Min(lines.Length, Math.Min(desiredEnd, desiredStart + 99));
                    writer.WriteLine($"### {Safe(location.Role)} — {Safe(location.FilePath)}:{location.StartLine}\n");
                    if (location.Caller is not null || location.Callee is not null)
                        writer.WriteLine($"Evidence edge: `{Safe(location.Caller)}` → `{Safe(location.Callee)}`\n");
                    writer.WriteLine("    @@ working-tree excerpt @@");
                    for (var line = desiredStart; line <= excerptEnd; line++)
                    {
                        var marker = line >= location.StartLine && line <= location.EndLine ? '>' : ' ';
                        writer.WriteLine($"    {marker} {line,5} | {BoundedSourceLine(lines[line - 1])}");
                    }
                    if (excerptEnd < desiredEnd) writer.WriteLine("      ... | excerpt truncated at 100 lines");
                    writer.WriteLine();
                }
                writer.WriteLine("## Human verdict\n");
                writer.WriteLine("Choose one and add a short reason.\n");
                writer.WriteLine("- [ ] Expected UI/control-only behavior");
                writer.WriteLine("- [ ] Supported backend operation present");
                writer.WriteLine("- [ ] Backend operation expected but evidence missing");
                writer.WriteLine("- [ ] Incorrect binding or source mismatch");
                writer.WriteLine("- [ ] Needs further review\n");
                writer.WriteLine("Reason:\n");
                writer.WriteLine("Reviewer:\n");
                writer.WriteLine("Reviewed at:\n");
            }
            File.Move(temporary, destination);
        }
        catch
        {
            if (File.Exists(temporary)) File.Delete(temporary);
            throw;
        }
        return ["codePathReview=created", $"case={caseId}|sourceMode=working-tree|excerpts={deduplicated.Count}|definitionCandidates={candidateCount}|review=unreviewed",
            "nonClaim=working-tree-may-differ-from-inspection-commit;static-calls-do-not-prove-runtime-execution-or-absence"];
    }
}
