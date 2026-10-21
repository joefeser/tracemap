using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TraceMap.Reporting;

public sealed record GroupedCompiledPathReportResult(string HtmlPath, string HandoffPath,
    string HtmlSha256, string HandoffSha256, long OutputBytes);

/// <summary>Private projection only. Native orchestration must admit returned bytes in its own checkpoint.</summary>
public static class GroupedCompiledPathReportWriter
{
    public const string HtmlName = "compiled-paths.local.html";
    public const string HandoffName = "compiled-paths.handoff.local.json";

    /// <summary>Writes only new fixed-name files; failure leaves unadmitted partial output for the owning workflow.</summary>
    public static async Task<GroupedCompiledPathReportResult> WriteAsync(GroupedCompiledPathHandoff handoff,
        string outputDirectory, GroupedCompiledPathLimits? admissionLimits = null,
        CancellationToken cancellationToken = default)
    {
        // Validate before allocating output; this restores every variant, not just representatives.
        var report = GroupedCompiledPathHandoffBuilder.Restore(handoff, admissionLimits, cancellationToken);
        using (var generator = File.OpenRead(typeof(GroupedCompiledPathReportWriter).Assembly.Location))
            if (handoff.GeneratorSha256 != Convert.ToHexStringLower(SHA256.HashData(generator)))
                throw new InvalidDataException("WEBFORMS_GROUPED_REPORT_GENERATOR_MISMATCH");
        outputDirectory = Path.GetFullPath(outputDirectory);
        var htmlPath = Path.Combine(outputDirectory, HtmlName);
        var jsonPath = Path.Combine(outputDirectory, HandoffName);
        if (File.Exists(htmlPath) || File.Exists(jsonPath) || Directory.Exists(htmlPath) || Directory.Exists(jsonPath))
            throw new InvalidDataException("WEBFORMS_GROUPED_REPORT_OUTPUT_EXISTS");
        // Input paths are not accepted by this API. The caller owns isolation and input admission.
        Directory.CreateDirectory(outputDirectory);
        var budget = new OutputBudget(handoff.Limits.MaxOutputBytes, cancellationToken);
        await using var jsonFile = new FileStream(jsonPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await using var htmlFile = new FileStream(htmlPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await using var json = new OutputStream(jsonFile, budget);
        await using var html = new OutputStream(htmlFile, budget);
        await JsonSerializer.SerializeAsync(json, handoff, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }, cancellationToken);
        await json.FlushAsync(cancellationToken);
        using (var writer = new StreamWriter(html, new UTF8Encoding(false), 4096, leaveOpen: true))
        {
            void W(string value) { cancellationToken.ThrowIfCancellationRequested(); writer.WriteLine(value); }
            W("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><link rel=\"icon\" href=\"data:,\"><title>Grouped compiled method paths</title>");
            W("<style>body{font:16px/1.5 system-ui,sans-serif;max-width:1100px;margin:2rem auto;padding:0 1rem;color:#17212b;background:#f7f9fb}a{color:#12599b}section{background:white;border:1px solid #ced8e1;border-radius:6px;padding:1rem;margin:1rem 0}.notice{border-left:4px solid #a34024;padding:1rem;background:#fff4e9}table{border-collapse:collapse;width:100%;table-layout:fixed}td,th{border:1px solid #ced8e1;padding:.6rem;vertical-align:top;text-align:left;overflow-wrap:anywhere}th{background:#edf2f7}code,pre{white-space:pre-wrap;overflow-wrap:anywhere}details{margin:.5rem 0}summary{cursor:pointer}small{color:#42546a}.identities{font-size:.85rem}@media(max-width:650px){body{margin:1rem auto}thead{display:none}table,tbody,tr,td{display:block;width:auto}tr{margin:.6rem 0}td{padding:.5rem;font-size:.9rem}td+td{border-top:0}details[open]>table td:before{content:attr(data-label);display:block;font-weight:600;color:#42546a}section{padding:.6rem}}</style></head><body>");
            W("<h1>Grouped compiled method paths</h1>");
            if (report.Gaps.Any(gap => gap.GapKind == "ProjectlessPublishImportsNotBuildBound"))
                W("<p class=\"notice\">Effective VB imports describe the current environment, not the published build. Source/configuration/provider hashes were checked during extraction, but these settings are not proven to have produced the DLLs. Source-to-compiled bridges remain Tier3 candidates, not authenticated build or runtime binding evidence.</p>");
            W("<p class=\"notice\">LOCAL ONLY — review-only static candidates, not runtime execution or proof that SQL ran. Grouping is a display/index operation; no evidence variant was removed.</p>");
            W($"<p>{handoff.Chains.Count} exact chains · {handoff.Variants.Count} retained variants · coverage {H(report.ReportCoverage)} · truncated {H(report.Summary.Truncated.ToString())}. <a href=\"{HandoffName}\">Lossless indexed handoff JSON</a></p>");
            if (report.Paths.Any(path => path.Edges.Any(edge => edge.EdgeKind == "compiled-il-framework-version-candidate")))
                W("<p class=\"notice\">Framework-version candidate routes are present (Tier3). Declaring assembly identity remains exact, but embedded desktop framework type versions differ. Runtime binding and compatibility are unproven; argument and return-value propagation stop at this boundary. ‘Exact chains’ describes identity-based grouping, not exact runtime binding.</p>");
            if (handoff.Chains.Count == 0)
                W("<p class=\"notice\">No terminal routes were retained by this path query. This is not evidence that the handler has no database dependencies. Inspect selector, traversal and coverage gaps below; a method graph is a separate diagnostic view, not a path report.</p>");
            else
                W("<p class=\"notice\">Static terminal routes were retained by this query. Each route is supported only at its recorded node and edge evidence tiers; the terminal's tier does not upgrade the whole route. This is not runtime execution, original-build correspondence, or proof of a specific SQL statement or stored procedure.</p>");
            if (report.Summary.Truncated)
            {
                W("<p class=\"notice\">Enumeration incomplete: retained routes remain evidence, but counts are not exhaustive and unlisted routes are not proven absent. Exact-chain grouping happens after bounded variant enumeration; many variants can belong to the same chain. Grouping does not remove variants or clear truncation. Inspect the retained traversal gaps for the actual stopping reason.</p>");
                W($"<p>Query limits: retained path variants {report.Query.MaxPaths}; depth {report.Query.MaxDepth}; frontier {report.Query.MaxFrontier}. No limits were increased by this report.</p>");
            }
            W("<nav aria-label=\"Method chains\"><ul>");
            for (var index = 0; index < handoff.Chains.Count; index++)
            {
                var group = handoff.Chains[index];
                var path = report.Paths[group.VariantIndexes[0]];
                W($"<li><a href=\"#chain-{index + 1}\">Chain {index + 1}: {H(path.Nodes.FirstOrDefault() is { } first ? Compact(first) : path.StartNodeId)}</a> — {group.VariantIndexes.Count} variants</li>");
            }
            W("</ul></nav>");
            W("<section><h2>WebForms entry relationships (separate from call paths)</h2>");
            W("<p>Method-to-terminal paths do not establish a page or framework entry. Containment is not execution, declarative references are not getter calls, and lifecycle candidates do not prove framework dispatch. Entry candidates never upgrade path tiers and are not traversed call edges.</p>");
            if (report.WebFormsEntries is { } entries)
            {
                W($"<p>{H(entries.Coverage)}</p><p>Inventory generator SHA-256: {H(entries.GeneratorSha256)}; bounded input SHA-256: {H(entries.BoundedInputSha256)}.</p>");
                if (entries.Relationships.Count == 0)
                    W("<p>No entry relationships were retained in this report inventory. This is not proof that the application has no framework entry.</p>");
                foreach (var entry in entries.Relationships)
                {
                    W($"<details><summary>{H(entry.EdgeKind)} — {H(entry.EvidenceTier)} — {H(entry.Target)}</summary>");
                    W($"<p>Source: {H(entry.Source)}; target identity: {H(entry.TargetSymbolId)}; rule: {H(entry.RuleId)}.</p>");
                    W($"<p>Evidence: {H(entry.SourceIndexId)} / {H(entry.FilePath)}:{entry.StartLine}-{entry.EndLine}; commit: {H(entry.CommitSha)}; extractor version: {H(entry.ExtractorVersion)}; combined fact: {H(entry.CombinedFactId)}.</p>");
                    W($"<p>Supporting fact IDs (scoped to source index): {H(string.Join(", ", entry.SupportingFactIds))}.</p>");
                    W($"<p>First unresolved boundary: {H(entry.FirstUnresolvedBoundary)}</p></details>");
                }
            }
            else
                W("<p>Entry relationship inventory unavailable in this retained report. Do not infer entry absence or framework dispatch from method names. Older retained inputs may lack this evidence; this rendering does not rescan or synthesize it.</p>");
            W("</section>");
            // Restore already admitted the current or exact legacy projection.
            // Render current coverage wording without rewriting the retained packet.
            var backend = BackendExtractionInventoryBuilder.Create(handoff);
            W("<section><h2>Backend extraction inventory</h2><p>Retained query evidence — not a whole-site inventory. Human retain/retire decisions are undecided.</p>");
            W($"<p>Query surface: {H(backend.QuerySurface)}. This filter bounds inventory coverage.</p>");
            W($"<p>Rule {H(backend.RuleId)}; coverage {H(backend.Coverage)}; truncated {backend.Truncated}; retained gaps {backend.RetainedGapCount}. {backend.Operations.Count} evidence records (not distinct business operations).</p>");
            W($"<p>{H(handoff.HttpCoverageLimitations ?? GroupedCompiledPathHandoffBuilder.HttpCoverageLimitations)}</p>");
            if (handoff.DatabaseCoverageLimitations is { } sqlLimits) W($"<p>{H(sqlLimits)}</p>");
            foreach (var limitation in backend.Limitations) W($"<p>{H(limitation)}</p>");
            foreach (var operation in backend.Operations)
            {
                var node = handoff.Nodes[operation.NodeReference];
                W($"<details><summary>{H(operation.Family)}: {H(Compact(node))} — {H(operation.ConnectionState)}</summary><p>Evidence {H(node.RuleId)} / {H(node.EvidenceTier)}; {H(Location(node.FilePath, node.StartLine, node.EndLine))}</p><p>Node reference: {H(operation.NodeReference)}; human decision: {H(operation.HumanDecision)}</p>");
                if (node.DatabaseCallEvidence is { } database)
                    W($"<p>Database operation: {H(database.MethodName)}; {H(database.OperationKind)}; command: {H(database.CommandTextAvailability)}; command type: {H(database.CommandTypeEvidence)}; state coverage: {H(database.CommandStateCoverage)}; parameters: {H(database.ParameterEvidence)} / {H(database.ParameterDirectionKinds)} / {H(database.ParameterTypeKinds)} / {H(database.ParameterTypeNames)}; transaction: {H(database.TransactionEvidence)}; connection: {H(database.ConnectionEvidence)}; signature: {H(database.OriginalSignature)}; provider reference: {H(database.ProviderAssemblyIdentity)}; {H(database.Limitations)}</p>");
                if (node.CompiledDatabaseApiEvidence is { } il)
                    W($"<p>Compiled-only API candidate; provenance {H(il.ProvenanceState)}; signature truncated {il.SignatureTruncated}; IL generator {H(il.IlGeneratorSha256)}; bounded input {H(il.IlBoundedInputSha256)}; binary {H(il.RawFileSha256)}. Encoded API operand:</p><pre>{H(il.OriginalTargetIdentity)}</pre>");
                if (node.HttpCallEvidence is { } http)
                    W($"<p>API family {H(http.MethodFamily)}; method {H(http.MethodName)}; verb {H(node.HttpMethod ?? "UNKNOWN")}; destination {H(http.DestinationAvailability)}; protocol {H(http.ProtocolKind)}; coverage {H(http.CoverageLabel)}. {H(http.Limitations)}</p>");
                W($"<p>Query roots: {H(string.Join(", ", operation.QueryRootReferences))}. Ordered variant indexes (zero-based): {H(string.Join(", ", operation.VariantIndexes))}. Candidate variant indexes: {H(string.Join(", ", operation.CandidateVariantIndexes))}.</p><p>Review obligations: {H(string.Join(", ", operation.ContractReviewObligations))}</p>");
                W("<h3>Human verification and implementation handoff</h3><p>All checks pending. Evidence above is an input to review, not approval. Record verification in the client work tracker; this report does not persist checkbox decisions.</p><ul>");
                foreach (var task in BackendExtractionInventoryBuilder.ReviewTasks(operation))
                    W($"<li>Pending: {H(task)}</li>");
                W("</ul><p>Reviewer: unassigned; verification evidence: not supplied; implementation acceptance: pending. Truncated queries do not establish complete route coverage.</p></details>");
            }
            W("</section>");
            for (var index = 0; index < handoff.Chains.Count; index++)
            {
                var group = handoff.Chains[index];
                var path = report.Paths[group.VariantIndexes[0]];
                W($"<section id=\"chain-{index + 1}\"><h2>Chain {index + 1} <small>({group.VariantIndexes.Count} variants)</small></h2>");
                W($"<p>{H(string.Join(" → ", path.Nodes.Select(Compact)))}</p>");
                var commandEndpoints = group.VariantIndexes.Select(variant => handoff.Variants[variant].NodeReferences.LastOrDefault())
                    .OfType<string>().Distinct(StringComparer.Ordinal)
                    .Where(reference => handoff.Nodes[reference].SurfaceKind == "database-api" || handoff.Nodes[reference].CommandBinding is not null).ToArray();
                if (commandEndpoints.Length > 0)
                    W("<h3>Database operations and command inputs</h3><p>Retained static routes, not proof of execution. Constants remain hash-only; SQL text and table/procedure names are not reconstructed.</p>");
                foreach (var reference in commandEndpoints)
                {
                    var endpoint = handoff.Nodes[reference];
                    W($"<h4>{H(endpoint.SurfaceName ?? Compact(endpoint))}</h4><p>Endpoint evidence: {H(endpoint.RuleId)} / {H(endpoint.EvidenceTier)}; {H(Location(endpoint.FilePath, endpoint.StartLine, endpoint.EndLine))}. Node reference: <code>{H(reference)}</code></p>");
                    foreach (var limitation in endpoint.Limitations ?? [])
                        W($"<p>Endpoint limitation: {H(limitation)}</p>");
                    var variants = group.VariantIndexes.Where(variant => handoff.Variants[variant].NodeReferences.LastOrDefault() == reference);
                    W($"<p>Retained variants: {H(string.Join(", ", variants.Select(variant => handoff.Variants[variant].Path.PathId)))}</p>");
                    foreach (var (label, value) in new[] { ("Command text expression", endpoint.CommandBinding?.CommandTextFromPath),
                        ("Command type evidence", endpoint.CommandBinding?.CommandTypeFromPath) })
                    {
                        W($"<h5>{label}</h5>");
                        if (value is null) { W("<p>Value evidence unavailable; the endpoint route is still retained.</p>"); continue; }
                        var remaining = 32;
                        WriteCommandValue(value, W, 0, ref remaining, label == "Command type evidence");
                    }
                }
                W("<details><summary>Exact method and source identities</summary><div class=\"identities\">");
                foreach (var reference in group.VariantIndexes.SelectMany(variant => handoff.Variants[variant].NodeReferences).Distinct(StringComparer.Ordinal))
                {
                    var node = handoff.Nodes[reference];
                    W($"<p><code>{H(node.DisplayName)}</code><br>source {H(node.SourceIndexId)}; scan {H(node.ScanId)}; commit {H(node.CommitSha)}; node {H(node.NodeId)}<br>symbol <code>{H(node.SymbolId)}</code>; rule {H(node.RuleId)}; tier {H(node.EvidenceTier)}; location {H(Location(node.FilePath, node.StartLine, node.EndLine))}<br>reference <code>{H(reference)}</code></p>");
                }
                W("</div></details>");
                var evidence = group.VariantIndexes.SelectMany(variant => handoff.Variants[variant].EdgeReferences)
                    .Distinct(StringComparer.Ordinal).ToArray();
                W("<details><summary>Retained transition evidence</summary><table><thead><tr><th>Transition / evidence</th><th>Rule / tier</th><th>Evidence location</th></tr></thead><tbody>");
                foreach (var reference in evidence)
                {
                    var edge = handoff.Edges[reference];
                    foreach (var limitation in edge.Limitations ?? CompiledEdgeLimitations.ForRule(edge.RuleId) ?? [])
                        W($"<tr><td colspan=\"3\">Transition {H(reference)} limitation: {H(limitation)}</td></tr>");
                    W($"<tr><td data-label=\"Transition / evidence\" id=\"e-{index + 1}-{reference[5..]}\">{H(edge.EdgeKind)}<br><small>{H(edge.FromNodeId)} → {H(edge.ToNodeId)}</small><details><summary>Exact evidence references</summary><code>{H(reference)}</code><br>edge {H(edge.EdgeId)}<br>facts {H(string.Join(", ", edge.SupportingFactIds))}<br>combined edges {H(string.Join(", ", edge.SupportingCombinedEdgeIds))}<br>attachment link {H(edge.CompiledAttachmentLinkSha256)}</details></td><td data-label=\"Rule / tier\">{H(edge.RuleId)}<br>{H(edge.EvidenceTier)}<br>{H(edge.Classification)}</td><td data-label=\"Evidence location\">{H(Location(edge.FilePath, edge.StartLine, edge.EndLine))}</td></tr>");
                }
                W("</tbody></table></details>");
                W("<details><summary>Every retained path variant</summary><ul>");
                foreach (var variantIndex in group.VariantIndexes)
                {
                    var variant = handoff.Variants[variantIndex];
                    W($"<li><details><summary>{H(variant.Path.PathId)} — {H(variant.Path.Classification)} / {H(variant.Path.Confidence)}</summary><p>Ordered transition references: " +
                        string.Join(" → ", variant.EdgeReferences.Select((reference, ordinal) => $"<a href=\"#e-{index + 1}-{reference[5..]}\">{ordinal + 1}</a>")) + "</p>");
                    W($"<p>Supporting facts: <code>{H(string.Join(", ", variant.Path.SupportingFactIds))}</code><br>Supporting edges: <code>{H(string.Join(", ", variant.Path.SupportingEdgeIds))}</code></p>");
                    foreach (var note in variant.Path.Notes) W($"<p>{H(note.Code)}: {H(note.Message)}</p>");
                    W("</details></li>");
                }
                W("</ul></details></section>");
            }
            W($"<section><h2>Coverage and retained gaps</h2><p>{report.Gaps.Count} gap records remain in the indexed handoff. Gap absence is not proof of complete coverage.</p><ul>");
            W("</ul><h3>Sampled root-to-cutoff prefixes</h3><p>At most three witnesses per depth/cycle reason and 64 edges per witness. These are traversal diagnostics, not database paths or runtime proof. Other gaps may have no retained witness.</p>");
            var cutoffGaps = report.Gaps.Where(gap => gap.GapKind == "TruncatedByLimit").ToArray();
            var identityReturns = cutoffGaps.Count(gap => gap.CutoffCause == "candidate-identity-roundtrip");
            var terminalUncertainty = cutoffGaps.Count(gap => gap.CutoffCause == "terminal-route-not-found-within-depth-bound");
            W($"<p>Identity round trips (not application recursion): {identityReturns}. Bounded terminal-route uncertainty: {terminalUncertainty}. Other or legacy cutoffs: {cutoffGaps.Length - identityReturns - terminalUncertainty}. These are retained gap counts, not missing paths. Original depth/cycle accounting and partial coverage remain unchanged.</p>");
            foreach (var gap in report.Gaps.Where(gap => gap.CutoffWitness is not null).Take(6))
            {
                var witness = gap.CutoffWitness!;
                if (gap.CutoffTraversalDepth is { } evaluatedDepth && gap.CutoffMaxDepth is { } depthLimit)
                    W($"<p>Cutoff evaluation: {evaluatedDepth} edges traversed on this prefix; configured depth {depthLimit}. This is not the node's shortest or first-discovery depth.</p>");
                W($"<details><summary>{H(gap.Reason)} / {H(gap.CutoffCause)}: {H(Location(gap.FilePath, gap.StartLine, gap.EndLine))}</summary><p>{H(gap.Message)}</p><p>Prefix truncated: {witness.PrefixTruncated}; final candidate edge not traversed: {witness.LastEdgeNotTraversed}</p><ol>");
                for (var i = 0; i < witness.Edges.Count; i++)
                {
                    var edge = witness.Edges[i];
                    W($"<li>{H(edge.FromNodeId)} → {H(edge.ToNodeId)}<br>{H(edge.EdgeKind)}; rule {H(edge.RuleId)}; tier {H(edge.EvidenceTier)}; {H(Location(edge.FilePath, edge.StartLine, edge.EndLine))}</li>");
                }
                W("</ol><details><summary>Retained node identities</summary>");
                foreach (var node in witness.Nodes) W($"<p>{H(node.NodeId)}: {H(Compact(node))}; {H(Location(node.FilePath, node.StartLine, node.EndLine))}</p>");
                W("</details></details>");
            }
            W("<ul>");
            foreach (var warning in report.CoverageWarnings) W($"<li>{H(warning)}</li>");
            foreach (var limitation in report.Limitations) W($"<li>{H(limitation)}</li>");
            W("</ul><details><summary>Projection provenance and limits</summary>");
            W($"<p>{H(handoff.Limitation)}</p><p>Rule {H(handoff.RuleId)}; tier {H(handoff.EvidenceTier)}<br>Generator SHA-256 <code>{H(handoff.GeneratorSha256)}</code><br>Bounded input SHA-256 <code>{H(handoff.BoundedInputSha256)}</code><br>Admitted index SHA-256 <code>{H(handoff.InputIndexSha256)}</code><br>Canonical report SHA-256 <code>{H(handoff.InputReportSha256)}</code></p>");
            W($"<pre>{H(JsonSerializer.Serialize(handoff.Limits))}</pre></details></section></body></html>");
            await writer.FlushAsync(cancellationToken);
        }
        await html.FlushAsync(cancellationToken);
        return new(htmlPath, jsonPath, html.Digest(), json.Digest(), budget.Bytes);
    }

    private static void WriteCommandValue(CompiledCommandPathValueBinding value, Action<string> write, int depth, ref int remaining, bool commandType = false)
    {
        if (depth > 4 || remaining-- <= 0)
        { write("<p>Expression display limit reached; consult the lossless handoff.</p>"); return; }
        var label = value.SymbolicInput is { } input ? "Value returned by " + Compact(input.MethodIdentity)
            : value.Composition is { } composition ? composition.Operation
            : value.Alternatives is not null ? "Possible input alternatives (branch feasibility not proven)"
            : value.Origin.Kind == "constant-string-hash" ? "String constant (hash retained; plaintext unavailable)"
            : value.Origin.Kind == "constant-int32" ? commandType ? value.Origin.Identity switch
                { "1" => "CommandType.Text", "4" => "CommandType.StoredProcedure", "512" => "CommandType.TableDirect", _ => "Unrecognized command type constant" }
                : "Integer constant (retained evidence, not execution)"
            : value.Origin.Kind == "argument-slot" ? "Unresolved method argument"
            : "Unresolved value";
        write($"<div><p><strong>{H(label)}</strong><br>State: {H(value.State)}; rule: {H(value.RuleId)}; tier: {H(value.EvidenceTier)}</p>");
        write($"<p>Connection / evidence gaps: {H(value.Gaps.Count == 0 ? "none retained for this operand (not a completeness claim)" : string.Join(", ", value.Gaps))}</p>");
        if (value.SymbolicInput is { } symbolic)
            write($"<p>Value uncertainty: {H(string.Join(", ", symbolic.ValueGaps))}. Method return not evaluated; unknown runtime values do not imply a missing connection.</p><details><summary>Symbolic input evidence</summary><p>Method {H(symbolic.MethodFactId)}; producer {H(symbolic.ProducerCallFactId)}; body {H(symbolic.BodyFactId)}; return {H(symbolic.ReturnFactId)}</p></details>");
        foreach (var limitation in value.Limitations ?? []) write($"<p>Limitation: {H(limitation)}</p>");
        if (value.Alternatives is not null || value.Composition is not null)
        {
            write("<ol>");
            foreach (var child in value.Alternatives ?? value.Composition!.OperandBindings)
            {
                if (remaining <= 0) { write("<li>Expression display limit reached; consult the lossless handoff.</li>"); break; }
                write("<li>"); WriteCommandValue(child, write, depth + 1, ref remaining, commandType); write("</li>");
            }
            write("</ol>");
        }
        write($"<details><summary>Value projection provenance</summary><p>Origin body {H(value.OriginBodyFactId)}; argument hops {value.Steps.Count}; return hops {value.ReturnSteps?.Count ?? 0}<br>Generator SHA-256 {H(value.GeneratorSha256)}<br>Bounded input SHA-256 {H(value.BoundedInputSha256)}</p></details></div>");
    }

    private static string H(string? value) => WebUtility.HtmlEncode(value ?? "unavailable");
    private static string Location(string? file, int? start, int? end) => file is null ? "unavailable" :
        start is > 0 ? $"{file}:{start}-{end}" : $"{file} (no source-line claim)";
    private static string Compact(CombinedPathNode node)
    {
        // This writer admits local-only handoffs and already exposes exact symbols
        // in the identity detail. Canonical compiled symbols contain publicKeyToken,
        // so the general safe-display filter can hash their entire display name.
        // Recover only a method label here, never alter global privacy projection,
        // graph matching, grouping, identities or the lossless JSON records.
        if (node.DisplayName.StartsWith("redacted-hash:", StringComparison.Ordinal)
            && node.SymbolId is { Length: > 0 } symbol
            && Regex.IsMatch(symbol, @"\|(?:method|constructor):[0-9]+:[^|]+", RegexOptions.NonBacktracking))
            return Compact(symbol);
        return Compact(node.DisplayName);
    }
    private static string Compact(string identity)
    {
        // Display only. Never use this lossy label for grouping or matching overloads.
        var method = Regex.Match(identity, @"\|(?:method|constructor):[0-9]+:([^|]+)", RegexOptions.NonBacktracking);
        if (method.Success)
        {
            var prefix = identity[..method.Index];
            var types = Regex.Matches(prefix, @"\|names:[0-9]+:([^|]+)", RegexOptions.NonBacktracking);
            var namespaces = Regex.Matches(prefix, @"namespace:[0-9]+:([^|]*)", RegexOptions.NonBacktracking);
            var type = types.Count > 0 ? types[^1].Groups[1].Value : "unknown type";
            var space = namespaces.Count > 0 ? namespaces[^1].Groups[1].Value : "";
            return (space.Length == 0 ? type : space + "." + type) + "." +
                (method.Groups[1].Value == ".ctor" ? "New" : method.Groups[1].Value) + "()";
        }
        return identity.Length > 160 ? identity[..157] + "…" : identity;
    }

    private sealed class OutputBudget(long maximumBytes, CancellationToken cancellationToken)
    {
        public long Bytes { get; private set; }
        public void Consume(int count)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (count > maximumBytes - Bytes) throw new InvalidDataException("WEBFORMS_GROUPED_REPORT_OUTPUT_LIMIT");
            Bytes += count;
        }
    }

    private sealed class OutputStream(Stream inner, OutputBudget budget) : Stream
    {
        private readonly IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        public string Digest() => Convert.ToHexStringLower(hash.GetHashAndReset());
        public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));
        public override void Write(ReadOnlySpan<byte> buffer)
        {
            budget.Consume(buffer.Length); inner.Write(buffer); hash.AppendData(buffer);
        }
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested(); budget.Consume(buffer.Length);
            await inner.WriteAsync(buffer, cancellationToken); hash.AppendData(buffer.Span);
        }
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            => WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        protected override void Dispose(bool disposing) { if (disposing) hash.Dispose(); base.Dispose(disposing); }
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => inner.Length;
        public override long Position { get => inner.Position; set => throw new NotSupportedException(); }
        public override void Flush() => inner.Flush();
        public override Task FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
