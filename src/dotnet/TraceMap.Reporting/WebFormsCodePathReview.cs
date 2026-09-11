using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TraceMap.Reporting;

/// <summary>Builds bounded private and anonymous working-tree review artifacts from a batch inspection.</summary>
public static class WebFormsCodePathReview
{
    private const int MaximumAnnotatedSourceLinesPerFile = 100_000;
    private const int MaximumAnnotatedSourceLinesPerReport = 150_000;
    private const long MaximumAnnotatedSourceOutputBytesPerReport = 64L * 1024 * 1024;

    private sealed record ReviewLocation(string Role, string FilePath, int StartLine, int EndLine, string? Caller, string? Callee, bool PreferFullSpan = false);
    private sealed record GroupedEdge(string Caller, string Callee, IReadOnlyList<ReviewLocation> Locations, IReadOnlyList<string> RuleIds, IReadOnlyList<string> Tiers);
    private sealed record AnonymousNode(string Id, string Classification);

    public static IReadOnlyList<string> Run(string inspectionPath, string sourceRoot, string caseId, string outputPath,
        int contextLines = 4, int maxExcerpts = 64, int maxSourceBytes = 16 * 1024 * 1024, int triggerContextLines = 12,
        string? returnHref = null, bool includeRawSource = false)
    {
        if (contextLines is < 0 or > 12 || triggerContextLines is < 0 or > 100 || maxExcerpts is < 1 or > 128 || maxSourceBytes is < 1 or > 32 * 1024 * 1024)
            throw new InvalidDataException("CodePathReviewInvalidLimit");
        if (!System.Text.RegularExpressions.Regex.IsMatch(caseId, "^case-[0-9]{3}$"))
            throw new InvalidDataException("CodePathReviewCaseInvalid");
        if (returnHref is not null && !System.Text.RegularExpressions.Regex.IsMatch(returnHref, "^[a-zA-Z0-9][a-zA-Z0-9._-]{0,127}\\.html$"))
            throw new InvalidDataException("CodePathReviewReturnLinkInvalid");
        var inspection = new FileInfo(inspectionPath);
        if (!inspection.Exists || inspection.Length > 32 * 1024 * 1024) throw new InvalidDataException("CodePathReviewInspectionUnavailable");
        var rootPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(sourceRoot));
        if (!Directory.Exists(rootPath)) throw new InvalidDataException("CodePathReviewSourceRootUnavailable");
        var physicalRootPath = ResolvePhysicalPath(rootPath);

        var privatePath = Path.GetFullPath(outputPath);
        var outputStem = Path.GetFileNameWithoutExtension(privatePath);
        if (outputStem.EndsWith(".private", StringComparison.Ordinal)) outputStem = outputStem[..^8];
        var outputDirectory = Path.GetDirectoryName(privatePath)!;
        var shareableHtmlPath = Path.Combine(outputDirectory, outputStem + ".shareable.html");
        var shareableJsonPath = Path.Combine(outputDirectory, outputStem + ".shareable.json");
        if (File.Exists(privatePath) || File.Exists(shareableHtmlPath) || File.Exists(shareableJsonPath))
            throw new IOException("CodePathReviewOutputExists");

        using var document = JsonDocument.Parse(File.ReadAllText(inspectionPath));
        var root = document.RootElement;
        if (!root.TryGetProperty("schemaVersion", out var schema) || schema.GetString() != "webforms-batch-inspection.v1")
            throw new InvalidDataException("CodePathReviewSchemaMismatch");
        var matches = root.GetProperty("cases").EnumerateArray().Where(c => c.GetProperty("caseId").GetString() == caseId).ToArray();
        if (matches.Length != 1) throw new InvalidDataException("CodePathReviewCaseUnavailable");
        var selected = matches[0];
        var handler = selected.TryGetProperty("handler", out var handlerValue) ? handlerValue.GetString() : null;
        if (string.IsNullOrWhiteSpace(handler) && selected.TryGetProperty("handlerLocation", out var handlerLocationValue) &&
            handlerLocationValue.TryGetProperty("callee", out var handlerTarget))
            handler = handlerTarget.GetString();
        if (string.IsNullOrWhiteSpace(handler)) throw new InvalidDataException("CodePathReviewCaseUnavailable");
        var locations = new List<ReviewLocation>();
        var witnessMetadata = new Dictionary<(string FilePath, int StartLine, int EndLine, string? Caller, string? Callee), (string RuleId, string Tier)>();

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
            if (location is null) return;
            locations.Add(location);
            if (value.TryGetProperty("ruleId", out var rule) && value.TryGetProperty("tier", out var tier))
                witnessMetadata[(location.FilePath, location.StartLine, location.EndLine, caller, callee)] =
                    (rule.GetString() ?? "unavailable", tier.GetString() ?? "unavailable");
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
        // Repository-relative paths are evidence identities. Preserve their casing so distinct
        // files on case-sensitive source volumes can never share cached contents or artifacts.
        var sourceFiles = new Dictionary<string, string[]>(StringComparer.Ordinal);
        long bytesRead = 0;
        string ResolveSource(string relativePath)
        {
            if (Path.IsPathRooted(relativePath)) throw new InvalidDataException("CodePathReviewSourcePathInvalid");
            var candidate = Path.GetFullPath(Path.Combine(rootPath, relativePath.Replace('/', Path.DirectorySeparatorChar)));
            if (!IsWithinRoot(rootPath, candidate))
                throw new InvalidDataException("CodePathReviewSourcePathInvalid");
            var physicalCandidate = ResolvePhysicalPath(candidate);
            if (!IsWithinRoot(physicalRootPath, physicalCandidate))
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
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var candidateCount = 0;
        var unresolvedLeaves = selected.TryGetProperty("unresolvedOtherLeaves", out var unresolvedValues)
            ? unresolvedValues.EnumerateArray().Select(v => v.GetString()).ToArray()
            : selected.GetProperty("methods").EnumerateArray()
                .Where(m => m.TryGetProperty("stopReason", out var reason) && reason.GetString() != "retained-outgoing-calls")
                .Select(m => m.GetProperty("symbol").GetString()).ToArray();
        var boundedUnresolvedLeaves = unresolvedLeaves.Where(v => !string.IsNullOrWhiteSpace(v)).ToArray();
        if (boundedUnresolvedLeaves.Length > 512 || (long)boundedUnresolvedLeaves.Length * evidenceFiles.Length > 8_192)
            throw new InvalidDataException("CodePathReviewCandidateWorkLimit");
        var syntaxRoots = evidenceFiles.ToDictionary(relativePath => relativePath,
            relativePath => CSharpSyntaxTree.ParseText(string.Join(Environment.NewLine, ReadSource(relativePath)), path: relativePath).GetCompilationUnitRoot(),
            StringComparer.Ordinal);
        foreach (var unresolved in boundedUnresolvedLeaves)
        {
            var open = unresolved!.IndexOf('(');
            if (open < 1) continue;
            var dot = unresolved.LastIndexOf('.', open - 1);
            var name = unresolved[(dot + 1)..open];
            var candidates = new List<ReviewLocation>();
            foreach (var relativePath in evidenceFiles)
            {
                var syntax = syntaxRoots[relativePath];
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

        var retainedLocations = deduplicated.Where(l => l.Role == "retained-call" && l.Caller is not null && l.Callee is not null).ToArray();
        var groupedEdges = retainedLocations.GroupBy(l => new { Caller = l.Caller!, Callee = l.Callee! })
            .Select(group => new GroupedEdge(group.Key.Caller, group.Key.Callee,
                group.OrderBy(l => l.FilePath, StringComparer.Ordinal).ThenBy(l => l.StartLine).ToArray(),
                group.Select(l => witnessMetadata.GetValueOrDefault((l.FilePath, l.StartLine, l.EndLine, l.Caller, l.Callee)).RuleId)
                    .Where(v => !string.IsNullOrWhiteSpace(v)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
                group.Select(l => witnessMetadata.GetValueOrDefault((l.FilePath, l.StartLine, l.EndLine, l.Caller, l.Callee)).Tier)
                    .Where(v => !string.IsNullOrWhiteSpace(v)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray()))
            .OrderBy(e => e.Caller, StringComparer.Ordinal).ThenBy(e => e.Locations[0].FilePath, StringComparer.Ordinal)
            .ThenBy(e => e.Locations[0].StartLine).ThenBy(e => e.Callee, StringComparer.Ordinal).ToArray();
        var edgesByCaller = groupedEdges.ToLookup(e => e.Caller, StringComparer.Ordinal);

        // Assign aliases by a deterministic breadth-first walk from the handler. Raw symbols
        // never enter the shareable artifacts, including as hashes or stable cross-report IDs.
        var aliases = new Dictionary<string, string>(StringComparer.Ordinal) { [handler] = "handler-001" };
        var queue = new Queue<string>();
        queue.Enqueue(handler);
        var nextAlias = 1;
        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var edge in edgesByCaller[current].OrderBy(e => e.Locations[0].StartLine).ThenBy(e => e.Callee, StringComparer.Ordinal))
            {
                if (aliases.ContainsKey(edge.Callee)) continue;
                aliases[edge.Callee] = $"node-{nextAlias++:D3}";
                queue.Enqueue(edge.Callee);
            }
        }
        foreach (var symbol in groupedEdges.SelectMany(e => new[] { e.Caller, e.Callee }).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
            if (!aliases.ContainsKey(symbol)) aliases[symbol] = $"node-{nextAlias++:D3}";

        var uiEndpoints = selected.TryGetProperty("uiControlEndpoints", out var uiValues)
            ? uiValues.EnumerateArray().Select(v => v.GetString()).Where(v => v is not null).Cast<string>().ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
        var retainedMethods = selected.GetProperty("methods").EnumerateArray().Select(m => m.GetProperty("symbol").GetString())
            .Where(v => v is not null).Cast<string>().ToHashSet(StringComparer.Ordinal);
        string Classification(string symbol) => symbol == handler ? "event-handler"
            : uiEndpoints.Contains(symbol) ? "ui-control-operation"
            : retainedMethods.Contains(symbol) ? "retained-method"
            : "external-or-unresolved-target";
        var anonymousNodes = aliases.OrderBy(pair => pair.Value, StringComparer.Ordinal)
            .Select(pair => new AnonymousNode(pair.Value, Classification(pair.Key))).ToArray();
        var anonymousEdges = groupedEdges.Select(edge => new
        {
            from = aliases[edge.Caller],
            to = aliases[edge.Callee],
            callSiteCount = edge.Locations.Count,
            ruleIds = edge.RuleIds.Select(PublicRuleId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
            evidenceTiers = edge.Tiers.Select(PublicTier).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray()
        }).ToArray();

        var evidenceAnchors = deduplicated.Select((location, index) => (location, anchor: $"evidence-{index + 1:D3}"))
            .ToDictionary(item => item.location, item => item.anchor);
        var annotatedSourceFiles = includeRawSource
            ? deduplicated.Select(location => location.FilePath)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .Select((filePath, index) => new
                {
                    FilePath = filePath,
                    OutputPath = Path.Combine(outputDirectory, $"{outputStem}.source-{index + 1:D3}.html")
                })
                .ToDictionary(item => item.FilePath, item => item.OutputPath, StringComparer.Ordinal)
            : new Dictionary<string, string>(StringComparer.Ordinal);
        if (annotatedSourceFiles.Values.Any(File.Exists))
            throw new IOException("CodePathReviewOutputExists");
        var annotatedLineCount = 0L;
        foreach (var relativePath in annotatedSourceFiles.Keys)
        {
            var lines = ReadSource(relativePath);
            if (lines.Length > MaximumAnnotatedSourceLinesPerFile)
                throw new InvalidDataException("CodePathReviewSourceLineLimit");
            annotatedLineCount += lines.Length;
            if (annotatedLineCount > MaximumAnnotatedSourceLinesPerReport)
                throw new InvalidDataException("CodePathReviewSourceAggregateLineLimit");
            if (deduplicated.Any(location => location.FilePath.Equals(relativePath, StringComparison.Ordinal) &&
                    (location.StartLine > lines.Length || location.EndLine > lines.Length)))
                throw new InvalidDataException("CodePathReviewSourceSpanInvalid");
        }
        static string H(string? value) => WebUtility.HtmlEncode(value ?? "unavailable");
        static string BoundedSourceLine(string value) => value.Length <= 500 ? value : value[..500] + " … [line truncated]";
        static string EvidenceConclusion(JsonElement selected)
        {
            var value = selected.TryGetProperty("evidenceConclusion", out var conclusion) ? conclusion.GetString() : null;
            return value is "no-supported-backend-terminal-observed" or
                "ui-control-operations-observed-no-other-unresolved-leaves" or
                "ui-control-operations-observed-with-unresolved-leaves"
                ? value : "unavailable-from-older-inspection";
        }

        static string PublicRuleId(string value) =>
            (value.StartsWith("csharp.semantic.", StringComparison.Ordinal) ||
             value.StartsWith("legacy.webforms.", StringComparison.Ordinal) ||
             value.StartsWith("diagnostic.webforms.", StringComparison.Ordinal)) &&
            System.Text.RegularExpressions.Regex.IsMatch(value, "^[a-z0-9][a-z0-9.-]{0,127}$")
                ? value : "withheld-unsafe-rule-id";

        static string PublicTier(string value) => value is "Tier1Semantic" or "Tier2Structural" or "Tier3SyntaxOrTextual" or "Tier4Unknown"
            ? value : "withheld-unsafe-evidence-tier";

        string RenderPrivateGraph()
        {
            const int nodeWidth = 180;
            const int nodeHeight = 58;
            const int horizontalGap = 34;
            const int verticalGap = 92;
            const int margin = 28;
            var depths = new Dictionary<string, int>(StringComparer.Ordinal) { [handler] = 0 };
            var pending = new Queue<string>();
            pending.Enqueue(handler);
            while (pending.Count > 0)
            {
                var current = pending.Dequeue();
                foreach (var edge in edgesByCaller[current])
                {
                    if (depths.ContainsKey(edge.Callee)) continue;
                    depths[edge.Callee] = depths[current] + 1;
                    pending.Enqueue(edge.Callee);
                }
            }
            var unconnectedDepth = depths.Count == 0 ? 0 : depths.Values.Max() + 1;
            foreach (var symbol in aliases.Keys.Order(StringComparer.Ordinal))
                depths.TryAdd(symbol, unconnectedDepth);
            var layers = aliases.Keys.GroupBy(symbol => depths[symbol]).OrderBy(group => group.Key)
                .Select(group => group.OrderBy(symbol => aliases[symbol], StringComparer.Ordinal).ToArray()).ToArray();
            var widestLayer = layers.Max(layer => layer.Length);
            var width = Math.Max(820, (widestLayer * nodeWidth) + ((widestLayer - 1) * horizontalGap) + (2 * margin));
            var height = (layers.Length * nodeHeight) + ((layers.Length - 1) * verticalGap) + (2 * margin);
            var positions = new Dictionary<string, (int X, int Y)>(StringComparer.Ordinal);
            for (var layerIndex = 0; layerIndex < layers.Length; layerIndex++)
            {
                var layer = layers[layerIndex];
                var layerWidth = (layer.Length * nodeWidth) + ((layer.Length - 1) * horizontalGap);
                var startX = (width - layerWidth) / 2;
                for (var nodeIndex = 0; nodeIndex < layer.Length; nodeIndex++)
                    positions[layer[nodeIndex]] = (startX + (nodeIndex * (nodeWidth + horizontalGap)), margin + (layerIndex * (nodeHeight + verticalGap)));
            }

            var graph = new StringBuilder();
            graph.Append("<div class=\"inline-graph\"><svg role=\"img\" aria-label=\"Anonymous retained call graph\" viewBox=\"0 0 ")
                .Append(width).Append(' ').Append(height).Append("\" xmlns=\"http://www.w3.org/2000/svg\"><defs><marker id=\"call-arrow\" markerWidth=\"8\" markerHeight=\"8\" refX=\"7\" refY=\"4\" orient=\"auto\"><path d=\"M0,0 L8,4 L0,8 z\"/></marker></defs>");
            foreach (var edge in groupedEdges)
            {
                var from = positions[edge.Caller];
                var to = positions[edge.Callee];
                var x1 = from.X + (nodeWidth / 2);
                var y1 = from.Y + nodeHeight;
                var x2 = to.X + (nodeWidth / 2);
                var y2 = to.Y;
                graph.Append("<line class=\"graph-edge\" x1=\"").Append(x1).Append("\" y1=\"").Append(y1)
                    .Append("\" x2=\"").Append(x2).Append("\" y2=\"").Append(y2).Append("\" marker-end=\"url(#call-arrow)\"/>")
                    .Append("<text class=\"edge-label\" x=\"").Append((x1 + x2) / 2).Append("\" y=\"").Append(((y1 + y2) / 2) - 5)
                    .Append("\">calls x ").Append(edge.Locations.Count).Append("</text>");
            }
            foreach (var node in anonymousNodes)
            {
                var symbol = aliases.Single(pair => pair.Value == node.Id).Key;
                var position = positions[symbol];
                var location = deduplicated.FirstOrDefault(item => item.Callee == symbol || item.Caller == symbol || (symbol == handler && item.Role == "handler"));
                if (location is not null)
                {
                    var href = includeRawSource
                        ? Path.GetFileName(annotatedSourceFiles[location.FilePath]) + $"#L{location.StartLine}"
                        : "#" + evidenceAnchors[location];
                    graph.Append("<a href=\"").Append(H(href)).Append("\">");
                }
                graph.Append("<g class=\"graph-node\"><rect x=\"").Append(position.X).Append("\" y=\"").Append(position.Y)
                    .Append("\" width=\"").Append(nodeWidth).Append("\" height=\"").Append(nodeHeight).Append("\" rx=\"6\"/>")
                    .Append("<text x=\"").Append(position.X + (nodeWidth / 2)).Append("\" y=\"").Append(position.Y + 23).Append("\">")
                    .Append(H(node.Id)).Append("</text><text class=\"node-kind\" x=\"").Append(position.X + (nodeWidth / 2)).Append("\" y=\"")
                    .Append(position.Y + 43).Append("\">").Append(H(node.Classification)).Append("</text></g>");
                if (location is not null) graph.Append("</a>");
            }
            return graph.Append("</svg></div>").ToString();
        }

        void WriteSourceCode(StreamWriter writer, ReviewLocation location, int? surroundingLines = null, int maximumLines = 100)
        {
            if (!includeRawSource)
            {
                writer.WriteLine("<p><em>Raw source excerpts were not included. Regenerate with the explicit raw-source option for a private work-machine review.</em></p>");
                return;
            }
            var lines = ReadSource(location.FilePath);
            if (location.StartLine > lines.Length) throw new InvalidDataException("CodePathReviewSourceSpanInvalid");
            var effectiveContext = surroundingLines ?? contextLines;
            var desiredStart = location.PreferFullSpan ? location.StartLine : Math.Max(1, location.StartLine - effectiveContext);
            var desiredEnd = location.PreferFullSpan ? location.EndLine : Math.Min(lines.Length, location.EndLine + effectiveContext);
            var excerptEnd = Math.Min(lines.Length, Math.Min(desiredEnd, desiredStart + maximumLines - 1));
            writer.WriteLine("<pre><code>");
            for (var line = desiredStart; line <= excerptEnd; line++)
            {
                var marker = line >= location.StartLine && line <= location.EndLine ? "&gt;" : " ";
                writer.WriteLine($"{marker} {line,5} | {H(BoundedSourceLine(lines[line - 1]))}");
            }
            if (excerptEnd < desiredEnd) writer.WriteLine("  ... | excerpt truncated at 100 lines");
            writer.WriteLine("</code></pre>");
        }

        string AnnotatedSourceHref(ReviewLocation location) =>
            H(Path.GetFileName(annotatedSourceFiles[location.FilePath]) + $"#L{location.StartLine}");

        void WriteAnnotatedSource(StreamWriter writer, string relativePath, string privateReportFileName)
        {
            var lines = ReadSource(relativePath);
            var fileLocations = deduplicated.Where(location =>
                    location.FilePath.Equals(relativePath, StringComparison.Ordinal))
                .OrderBy(location => location.StartLine)
                .ThenBy(location => location.Role, StringComparer.Ordinal)
                .ToArray();
            var starts = fileLocations.GroupBy(location => location.StartLine)
                .ToDictionary(group => group.Key, group => group.ToArray());
            var ends = fileLocations.GroupBy(location => location.EndLine + 1)
                .ToDictionary(group => group.Key, group => group.ToArray());
            var active = new HashSet<ReviewLocation>();

            writer.WriteLine("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
            writer.WriteLine($"<title>Private annotated source {H(relativePath)}</title><style>{AnnotatedSourceCss}</style></head><body><header>");
            writer.WriteLine($"<h1>{H(relativePath)}</h1><p class=\"private\">PRIVATE: complete authorized working-tree source. Inspection-commit equality is not established.</p>");
            writer.WriteLine($"<nav><a href=\"{H(privateReportFileName)}\">← Return to case review</a>{(returnHref is null ? "" : $" <a href=\"{H(returnHref)}\">Return to review index</a>")}</nav>");
            writer.WriteLine("<p class=\"legend\"><span class=\"key handler\">handler</span><span class=\"key event-binding\">event binding</span><span class=\"key exact-declaration\">declaration</span><span class=\"key retained-call\">retained call</span><span class=\"key definition-candidate\">definition candidate—not evidence</span></p></header><main><pre class=\"source\"><code>");
            for (var lineNumber = 1; lineNumber <= lines.Length; lineNumber++)
            {
                if (ends.TryGetValue(lineNumber, out var ending))
                    foreach (var location in ending) active.Remove(location);
                if (starts.TryGetValue(lineNumber, out var starting))
                    foreach (var location in starting) active.Add(location);
                var classes = active.Select(location => location.Role switch
                    {
                        "handler" => "handler",
                        "event-binding" => "event-binding",
                        "exact-declaration" => "exact-declaration",
                        "retained-call" => "retained-call",
                        _ => "definition-candidate"
                    })
                    .Distinct(StringComparer.Ordinal)
                    .Order(StringComparer.Ordinal);
                writer.Write($"<span class=\"source-line {H(string.Join(' ', classes))}\" id=\"L{lineNumber}\"><a class=\"line-number\" href=\"#L{lineNumber}\">{lineNumber}</a><span class=\"source-text\">{H(lines[lineNumber - 1])}</span>");
                if (starting is not null)
                    foreach (var location in starting)
                        writer.Write($"<a class=\"evidence-link\" href=\"{H(privateReportFileName)}#{evidenceAnchors[location]}\">{H(location.Role)}</a>");
                writer.WriteLine("</span>");
            }
            writer.WriteLine("</code></pre></main><footer>Highlighted spans are retained static evidence, not runtime coverage or execution.</footer></body></html>");
        }

        string RenderTree(string symbol, HashSet<string> path, HashSet<string> expanded)
        {
            var builder = new StringBuilder();
            var outgoing = edgesByCaller[symbol].OrderBy(e => e.Locations[0].StartLine).ThenBy(e => e.Callee, StringComparer.Ordinal).ToArray();
            if (outgoing.Length == 0) return "";
            builder.AppendLine("<ul class=\"call-tree\">");
            foreach (var edge in outgoing)
            {
                builder.Append("<li><span class=\"callee\">").Append(H(edge.Callee)).Append("</span> <span class=\"kind\">")
                    .Append(H(Classification(edge.Callee))).Append("</span> <span class=\"sites\">")
                    .Append(edge.Locations.Count).Append(edge.Locations.Count == 1 ? " call site" : " call sites").Append("</span>");
                foreach (var location in edge.Locations)
                {
                    builder.Append(" <a href=\"#").Append(evidenceAnchors[location]).Append("\">evidence</a>");
                    if (includeRawSource)
                        builder.Append(" <a href=\"").Append(AnnotatedSourceHref(location)).Append("\">source</a>");
                }
                if (path.Contains(edge.Callee)) builder.Append(" <span class=\"reference\">cycle reference</span>");
                else if (expanded.Contains(edge.Callee)) builder.Append(" <span class=\"reference\">shared callee; shown earlier</span>");
                else
                {
                    expanded.Add(edge.Callee);
                    var nextPath = new HashSet<string>(path, StringComparer.Ordinal) { edge.Callee };
                    builder.Append(RenderTree(edge.Callee, nextPath, expanded));
                }
                builder.AppendLine("</li>");
            }
            builder.AppendLine("</ul>");
            return builder.ToString();
        }

        Directory.CreateDirectory(outputDirectory);
        var destinations = new[] { privatePath, shareableHtmlPath, shareableJsonPath }
            .Concat(annotatedSourceFiles.Values)
            .ToArray();
        var temporaryPaths = destinations
            .ToDictionary(path => path, path => path + ".tmp-" + Guid.NewGuid().ToString("N"), StringComparer.Ordinal);
        var publishedPaths = new List<string>();
        try
        {
            using (var writer = new StreamWriter(new FileStream(temporaryPaths[privatePath], FileMode.CreateNew, FileAccess.Write, FileShare.None), new UTF8Encoding(false)))
            {
                writer.WriteLine("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
                writer.WriteLine($"<title>Private Web Forms code-path review {H(caseId)}</title><style>{PrivateCss}</style></head><body id=\"top\"><main>");
                if (returnHref is not null) writer.WriteLine($"<nav class=\"review-nav\"><a href=\"{H(returnHref)}\">← Return to review index</a></nav>");
                writer.WriteLine($"<h1>Local Web Forms code-path review: {H(caseId)}</h1>");
                writer.WriteLine($"<p class=\"private\">PRIVATE: working-tree source identities{(includeRawSource ? " and explicitly requested excerpts" : "")}. Keep this artifact on the work machine.</p>");
                writer.WriteLine("<section id=\"summary\"><h2>Summary</h2><ul>");
                writer.WriteLine($"<li>Inspection commit: <code>{H(root.GetProperty("commitSha").GetString())}</code></li>");
                writer.WriteLine("<li>Source mode: <code>working-tree</code>; Git is not required and commit equality is not established.</li>");
                writer.WriteLine($"<li>Retained evidence conclusion: <code>{H(EvidenceConclusion(selected))}</code></li>");
                writer.WriteLine($"<li>Traversal limit reached: <code>{selected.GetProperty("bounded").GetBoolean().ToString().ToLowerInvariant()}</code></li>");
                writer.WriteLine($"<li>Trigger context: <code>{triggerContextLines} lines before and after the retained binding span</code></li>");
                writer.WriteLine("<li>Rule: <code>diagnostic.webforms.local-code-path-review.v3</code></li></ul>");
                writer.WriteLine($"<p><a href=\"{H(Path.GetFileName(shareableHtmlPath))}\">Open anonymous shareable graph</a></p></section>");
                writer.WriteLine("<details class=\"panel\" id=\"trigger\" open><summary><h2>Trigger</h2></summary><p>The event binding selects the handler; it is context for the rooted call path, not a sibling call.</p>");
                var bindingLocations = deduplicated.Where(l => l.Role == "event-binding").ToArray();
                if (bindingLocations.Length == 0) writer.WriteLine("<p>No event-binding source location was retained for this case.</p>");
                foreach (var location in bindingLocations)
                {
                    writer.WriteLine($"<article class=\"trigger-code\"><h3>{H(location.FilePath)}:{location.StartLine}{(includeRawSource ? $" — <a href=\"{AnnotatedSourceHref(location)}\">annotated file</a>" : "")}</h3>");
                    WriteSourceCode(writer, location, triggerContextLines, 256);
                    writer.WriteLine($"<p><a href=\"#{evidenceAnchors[location]}\">Jump to full event-binding evidence</a></p></article>");
                }
                writer.WriteLine("</details>");
                writer.WriteLine("<details class=\"panel\" id=\"graph\"><summary><h2>Call graph</h2></summary><p>This dependency-free diagram uses anonymous aliases. Click a node to jump to retained evidence; use the private legend below to connect aliases to source identities.</p>");
                writer.WriteLine(RenderPrivateGraph());
                writer.WriteLine("<h3>Private alias legend</h3><dl class=\"legend\">");
                foreach (var pair in aliases.OrderBy(pair => pair.Value, StringComparer.Ordinal))
                {
                    var location = deduplicated.FirstOrDefault(item => item.Callee == pair.Key || item.Caller == pair.Key || (pair.Key == handler && item.Role == "handler"));
                    writer.Write($"<dt>{H(pair.Value)}</dt><dd>{H(pair.Key)}");
                    if (location is not null) writer.Write($" — <a href=\"#{evidenceAnchors[location]}\">evidence</a>");
                    if (includeRawSource && location is not null) writer.Write($" — <a href=\"{AnnotatedSourceHref(location)}\">annotated source</a>");
                    writer.WriteLine("</dd>");
                }
                writer.WriteLine("</dl></details>");
                writer.WriteLine("<details class=\"panel\" id=\"call-path\" open><summary><h2>Retained call path</h2></summary><p>Static retained calls, organized from the selected handler. This is not runtime order or branch feasibility.</p>");
                var handlerLocation = deduplicated.FirstOrDefault(l => l.Role == "handler");
                writer.Write($"<div class=\"root\">Handler: <strong>{H(handler)}</strong>");
                if (handlerLocation is not null) writer.Write($" <a href=\"#{evidenceAnchors[handlerLocation]}\">evidence</a>");
                if (includeRawSource && handlerLocation is not null) writer.Write($" <a href=\"{AnnotatedSourceHref(handlerLocation)}\">annotated source</a>");
                writer.WriteLine("</div>");
                writer.WriteLine(RenderTree(handler, new HashSet<string>(StringComparer.Ordinal) { handler }, new HashSet<string>(StringComparer.Ordinal) { handler }));
                writer.WriteLine("</details><details class=\"panel\" id=\"evidence\"><summary><h2>Evidence and source excerpts</h2></summary>");
                foreach (var location in deduplicated)
                {
                    writer.WriteLine($"<article class=\"evidence\" id=\"{evidenceAnchors[location]}\"><h3>{H(location.Role)} — {H(location.FilePath)}:{location.StartLine}{(includeRawSource ? $" — <a href=\"{AnnotatedSourceHref(location)}\">annotated file</a>" : "")}</h3>");
                    if (location.Caller is not null || location.Callee is not null)
                        writer.WriteLine($"<p>Evidence edge: <code>{H(location.Caller)}</code> → <code>{H(location.Callee)}</code></p>");
                    WriteSourceCode(writer, location);
                    writer.WriteLine("<p><a href=\"#call-path\">Back to call path</a></p></article>");
                }
                writer.WriteLine("</details><details class=\"panel\" id=\"verdict\" open><summary><h2>Human verdict</h2></summary><p>Choose one and add a short reason.</p>");
                writer.WriteLine("<label><input type=\"checkbox\"> Expected UI/control-only behavior</label><label><input type=\"checkbox\"> Supported backend operation present</label><label><input type=\"checkbox\"> Backend operation expected but evidence missing</label><label><input type=\"checkbox\"> Incorrect binding or source mismatch</label><label><input type=\"checkbox\"> Needs further review</label>");
                writer.WriteLine("<p>Reason:</p><p>Reviewer:</p><p>Reviewed at:</p></details>");
                writer.WriteLine("<footer>Unique-name definition candidates are navigation aids, not evidence. Missing source or calls do not prove absence.</footer>");
                if (returnHref is not null) writer.WriteLine($"<nav class=\"review-nav bottom\"><a href=\"{H(returnHref)}\">← Return to review index</a></nav>");
                writer.WriteLine("</main>");
                writer.WriteLine("<script>function revealTarget(){const id=decodeURIComponent(location.hash.slice(1));if(!id)return;const target=document.getElementById(id);if(!target)return;let parent=target.closest('details');while(parent){parent.open=true;parent=parent.parentElement?.closest('details');}}addEventListener('hashchange',revealTarget);revealTarget();</script></body></html>");
            }

            var annotatedOutputBytes = 0L;
            foreach (var sourceFile in annotatedSourceFiles.OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                var temporarySourcePath = temporaryPaths[sourceFile.Value];
                using (var writer = new StreamWriter(new FileStream(temporarySourcePath, FileMode.CreateNew, FileAccess.Write, FileShare.None), new UTF8Encoding(false)))
                    WriteAnnotatedSource(writer, sourceFile.Key, Path.GetFileName(privatePath));
                annotatedOutputBytes += new FileInfo(temporarySourcePath).Length;
                if (annotatedOutputBytes > MaximumAnnotatedSourceOutputBytesPerReport)
                    throw new InvalidDataException("CodePathReviewSourceAggregateOutputLimit");
            }

            using (var file = new FileStream(temporaryPaths[shareableJsonPath], FileMode.CreateNew, FileAccess.Write, FileShare.None))
                JsonSerializer.Serialize(file, new
                {
                    schemaVersion = "webforms-code-path-shareable.v1",
                    privacy = "anonymous-structural-report; no source text, paths, symbols, fact IDs, SQL, URLs, configuration, or commit identity",
                    caseId,
                    root = aliases[handler],
                    evidenceConclusion = EvidenceConclusion(selected),
                    traversalLimitReached = selected.GetProperty("bounded").GetBoolean(),
                    ruleId = "diagnostic.webforms.anonymous-code-path-review.v1",
                    nodes = anonymousNodes.Select(node => new { id = node.Id, classification = node.Classification }),
                    edges = anonymousEdges,
                    reviewResult = "unreviewed",
                    limitations = new[]
                    {
                        "Static retained calls do not prove runtime execution order or branch feasibility.",
                        "Aliases are local to this report and intentionally cannot be correlated to private identities.",
                        "Missing source or calls do not prove absence."
                    }
                }, new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            using (var writer = new StreamWriter(new FileStream(temporaryPaths[shareableHtmlPath], FileMode.CreateNew, FileAccess.Write, FileShare.None), new UTF8Encoding(false)))
            {
                writer.WriteLine("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">");
                writer.WriteLine($"<title>Anonymous Web Forms call-path review {H(caseId)}</title><style>{ShareableCss}</style></head><body id=\"top\"><main>");
                writer.WriteLine($"<h1>Anonymous Web Forms call-path review: {H(caseId)}</h1><p class=\"safe\">SHAREABLE: structural aliases only; no source text, paths, symbols, fact IDs, SQL, URLs, configuration, or commit identity.</p>");
                writer.WriteLine($"<p>Evidence conclusion: <code>{H(EvidenceConclusion(selected))}</code>. Traversal limit reached: <code>{selected.GetProperty("bounded").GetBoolean().ToString().ToLowerInvariant()}</code>.</p>");
                writer.WriteLine("<section id=\"call-graph\"><h2>Call graph</h2><p>Click a node to jump to its structural details. Mermaid rendering requires browser access to the Mermaid module; the source remains visible as a fallback.</p><pre class=\"mermaid\">");
                writer.WriteLine("flowchart TD");
                foreach (var node in anonymousNodes) writer.WriteLine($"  {node.Id.Replace('-', '_')}[\"{node.Id}<br/>{node.Classification}\"]");
                // Keep the generated diagram on Mermaid's conservative flowchart grammar
                // so standalone and sandboxed rendering do not depend on permissive parsing.
                foreach (var edge in anonymousEdges) writer.WriteLine($"  {edge.from.Replace('-', '_')} -->|calls x {edge.callSiteCount}| {edge.to.Replace('-', '_')}");
                writer.WriteLine("</pre><nav class=\"graph-links\" aria-label=\"Call graph navigation\">Jump to: ");
                foreach (var node in anonymousNodes) writer.WriteLine($"<a href=\"#node-{node.Id}\">{node.Id}</a> ");
                writer.WriteLine("</nav></section><h2>Structural details</h2>");
                foreach (var node in anonymousNodes)
                {
                    writer.WriteLine($"<section id=\"node-{node.Id}\"><h3>{node.Id}</h3><p>Classification: <code>{node.Classification}</code></p><ul>");
                    foreach (var edge in anonymousEdges.Where(e => e.from == node.Id))
                        writer.WriteLine($"<li>Calls <a href=\"#node-{edge.to}\">{edge.to}</a> at {edge.callSiteCount} retained site(s); rule(s): <code>{H(string.Join(", ", edge.ruleIds))}</code>; tier(s): <code>{H(string.Join(", ", edge.evidenceTiers))}</code>.</li>");
                    writer.WriteLine("</ul><p><a href=\"#top\">Back to graph</a></p></section>");
                }
                writer.WriteLine("<h2>Human verdict</h2><p>Result: <strong>unreviewed</strong></p><p>Static retained calls do not prove runtime order, branch feasibility, or source completeness. Missing evidence does not prove absence.</p>");
                var graphNavigation = JsonSerializer.Serialize(anonymousNodes.Select(node => new
                {
                    diagramId = node.Id.Replace('-', '_'),
                    targetId = $"node-{node.Id}"
                }));
                writer.WriteLine($$"""
                    </main><script type="module">
                    import mermaid from 'https://cdn.jsdelivr.net/npm/mermaid@11.17.2/dist/mermaid.esm.min.mjs';
                    mermaid.initialize({startOnLoad:false,securityLevel:'strict'});
                    await mermaid.run();
                    const graphNavigation = {{graphNavigation}};
                    for (const item of graphNavigation) {
                      for (const element of document.querySelectorAll(`[id^="flowchart-${item.diagramId}-"]`)) {
                        element.style.cursor = 'pointer';
                        element.setAttribute('tabindex', '0');
                        const navigate = () => { location.hash = item.targetId; };
                        element.addEventListener('click', navigate);
                        element.addEventListener('keydown', event => { if (event.key === 'Enter' || event.key === ' ') navigate(); });
                      }
                    }
                    </script></body></html>
                    """);
            }

            // Fail closed before publication if a private identity entered either anonymous artifact.
            var anonymousText = File.ReadAllText(temporaryPaths[shareableHtmlPath]) + File.ReadAllText(temporaryPaths[shareableJsonPath]);
            var privateTokens = aliases.Keys.Concat(deduplicated.Select(location => location.FilePath))
                .Append(root.GetProperty("commitSha").GetString())
                .Where(value => !string.IsNullOrWhiteSpace(value) && value!.Length >= 6)
                .Distinct(StringComparer.Ordinal);
            if (privateTokens.Any(token => anonymousText.Contains(token!, StringComparison.Ordinal)))
                throw new InvalidDataException("CodePathReviewAnonymousLeak");

            foreach (var pair in temporaryPaths)
            {
                File.Move(pair.Value, pair.Key);
                publishedPaths.Add(pair.Key);
            }
        }
        catch
        {
            foreach (var temporary in temporaryPaths.Values) if (File.Exists(temporary)) File.Delete(temporary);
            foreach (var destination in publishedPaths) if (File.Exists(destination)) File.Delete(destination);
            throw;
        }
        return ["codePathReview=created", $"case={caseId}|sourceMode=working-tree|triggerContextLines={triggerContextLines}|excerpts={deduplicated.Count}|definitionCandidates={candidateCount}|anonymousNodes={anonymousNodes.Length}|anonymousEdges={groupedEdges.Length}|review=unreviewed",
            $"artifacts=private-html;shareable-html;shareable-json{(includeRawSource ? $";annotated-source-html:{annotatedSourceFiles.Count}" : "")}",
            "nonClaim=working-tree-may-differ-from-inspection-commit;static-calls-do-not-prove-runtime-execution-or-absence"];
    }

    private static bool IsWithinRoot(string root, string candidate)
    {
        var relative = Path.GetRelativePath(root, candidate);
        return !Path.IsPathRooted(relative)
            && relative != ".."
            && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            && !relative.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal);
    }

    private static string ResolvePhysicalPath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var root = Path.GetPathRoot(fullPath) ?? throw new InvalidDataException("CodePathReviewSourcePathInvalid");
        var current = root;
        foreach (var segment in fullPath[root.Length..].Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            FileSystemInfo info = Directory.Exists(current) ? new DirectoryInfo(current) : new FileInfo(current);
            if (info.Exists && info.LinkTarget is not null)
                current = info.ResolveLinkTarget(returnFinalTarget: true)?.FullName
                    ?? throw new InvalidDataException("CodePathReviewSourcePathInvalid");
        }
        return Path.GetFullPath(current);
    }

    private const string PrivateCss = """
        :root{font-family:system-ui,sans-serif;color:#172033;background:#f5f7fb}body{margin:0}main{max-width:1100px;margin:auto;padding:24px}section,article,.panel{background:white;border:1px solid #dbe2ee;border-radius:10px;padding:18px;margin:16px 0}.panel>summary{cursor:pointer;display:flex;align-items:center;gap:.6rem}.panel>summary::before{content:'▸';color:#526177}.panel[open]>summary::before{content:'▾'}.panel>summary h2{display:inline;margin:0}.private{background:#fff1f0;border-left:5px solid #c62828;padding:12px}.review-nav{margin:0 0 14px}.review-nav.bottom{margin:24px 0 0}.review-nav a{display:inline-block;background:#eaf1ff;border:1px solid #bed0ee;border-radius:6px;padding:8px 12px;text-decoration:none}.root{font-size:1.05rem;padding:10px;background:#eaf1ff;border-radius:6px}.call-tree{border-left:2px solid #bed0ee;margin:.5rem 0 .5rem 1rem;padding-left:1.4rem}.call-tree li{margin:.55rem 0}.kind,.sites,.reference{font-size:.85rem;color:#526177}.callee{font-weight:650}a{color:#1558b0}pre{overflow:auto;background:#111827;color:#e5e7eb;padding:14px;border-radius:8px;line-height:1.4}.evidence:target{outline:3px solid #ffbf47}.trigger-code{background:#f8faff}.inline-graph{overflow:auto;background:#fff;border:1px solid #dbe2ee;border-radius:8px}.inline-graph svg{display:block;min-width:820px;width:100%;height:auto}.graph-edge{stroke:#3b4658;stroke-width:1.5}.inline-graph marker path{fill:#3b4658}.edge-label,.graph-node text{text-anchor:middle;font-size:13px;fill:#172033}.edge-label{paint-order:stroke;stroke:#fff;stroke-width:5px;stroke-linejoin:round}.graph-node rect{fill:#eef0ff;stroke:#7467d8;stroke-width:1.5}.graph-node:hover rect,.graph-node:focus rect{fill:#e1e5ff;stroke-width:2.5}.node-kind{font-size:11px;fill:#526177}.legend{display:grid;grid-template-columns:max-content 1fr;gap:.45rem 1rem}.legend dt{font-weight:700}.legend dd{margin:0;overflow-wrap:anywhere}label{display:block;margin:.55rem 0}footer{color:#526177;margin:28px 0}
        """;

    private const string ShareableCss = """
        :root{font-family:system-ui,sans-serif;color:#172033;background:#f5f7fb}body{margin:0}main{max-width:1100px;margin:auto;padding:24px}section{background:white;border:1px solid #dbe2ee;border-radius:10px;padding:16px;margin:14px 0}.safe{background:#e9f7ee;border-left:5px solid #26834a;padding:12px}a{color:#1558b0}.mermaid{background:white;border:1px solid #dbe2ee;border-radius:10px;padding:16px;overflow:auto}code{background:#edf1f7;padding:.1rem .3rem;border-radius:4px}
        """;

    private const string AnnotatedSourceCss = """
        :root{font-family:system-ui,sans-serif;color:#172033;background:#f5f7fb}body{margin:0}header,footer{padding:18px 24px;background:#fff;border-bottom:1px solid #dbe2ee}h1{margin:.2rem 0;font-size:1.3rem;overflow-wrap:anywhere}.private{background:#fff1f0;border-left:5px solid #c62828;padding:10px}nav a{display:inline-block;margin-right:.6rem;color:#1558b0}.legend{display:flex;flex-wrap:wrap;gap:.45rem}.key,.evidence-link{border-radius:4px;padding:.15rem .4rem;font-size:.78rem}.handler{background:#fff0b8}.event-binding{box-shadow:inset 4px 0 #1b7f5c}.exact-declaration{box-shadow:inset 4px 0 #7467d8}.retained-call{box-shadow:inset 4px 0 #1769aa}.definition-candidate{box-shadow:inset 4px 0 #8a5a00}.source{margin:0;padding:16px 0;background:#111827;color:#e5e7eb;overflow:auto;line-height:1.45}.source-line{display:block;min-width:max-content;padding-right:18px}.source-line:target{outline:2px solid #ffbf47;outline-offset:-2px}.line-number{display:inline-block;width:5rem;padding-right:1rem;text-align:right;color:#8da2bf;text-decoration:none;user-select:none}.source-text{white-space:pre}.evidence-link{margin-left:1rem;background:#dce8fb;color:#123c70;text-decoration:none}footer{border-top:1px solid #dbe2ee;border-bottom:0;color:#526177}
        """;
}
