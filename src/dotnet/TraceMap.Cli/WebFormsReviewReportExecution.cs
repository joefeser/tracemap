using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TraceMap.Combine;
using TraceMap.Core;
using TraceMap.Reporting;

namespace TraceMap.Cli;

internal delegate Task<WebFormsReviewReportResult> WebFormsReviewReportRunner(
    WebFormsReviewPreflightManifest plan, string scanPath, string reportPath, CancellationToken cancellationToken);
internal sealed record WebFormsReviewReportResult(string Coverage, int Surfaces, int CompiledPaths, IReadOnlyList<string> Gaps,
    string BoundedInputSha256, IReadOnlyList<WebFormsReviewArtifact> GeneratedArtifacts);
public sealed record NativeWebFormsReviewHandoff(
    string SchemaVersion, string RuleId, string EvidenceTier, string Visibility, string ClaimLevel,
    string GeneratorSha256, string ReportingGeneratorSha256, string BoundedInputSha256,
    string RunId, string Coverage, string IndexSha256, string PacketSha256,
    WebFormsReviewConfig Configuration, IReadOnlyList<WebFormsReviewInput> InputInventory,
    IReadOnlyList<ScanManifest> ScanManifests, WebFormsModernizationPacket Packet,
    string CompiledHandoffRelativePath, string CompiledHandoffSha256,
    int RequestedCompiledRoots, int OmittedCompiledRoots, IReadOnlyList<string> Gaps,
    IReadOnlyList<string> Limitations);

internal static class WebFormsReviewReportExecution
{
    internal const string RuleId = "workflow.webforms.native-report-execution.v1";
    internal const string Schema = "webforms-native-review-handoff.v1";
    internal const string Completed = "reports-completed-review-only";
    internal const string HandoffName = "handoff.local.json";
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    internal static object Policy(WebFormsReviewPreflightManifest plan, string scanPath, string reportPath) => new
    {
        schemaVersion = Schema, ruleId = RuleId, scanPath, reportPath,
        operation = plan.Configuration.Operation, scope = plan.Configuration.PageMode,
        pages = plan.Configuration.PageRelativePaths,
        budgets = plan.Configuration.Budgets.Reports ?? new(),
        plan.Configuration.Budgets.GraphMaxDepth, plan.Configuration.Budgets.GraphMaxPaths,
        plan.Configuration.Budgets.GraphMaxWork,
        selection = "retained-packet-handler-full-symbol-and-exact-source-index-scan-commit-v1",
        pageVerdicts = "preserve-packet-verdicts-compiled-paths-supplement-only",
        rawSource = false, cleanup = false
    };

    internal static async Task<WebFormsReviewReportResult> WriteAsync(WebFormsReviewPreflightManifest plan,
        string scanPath, string reportPath, CancellationToken cancellationToken)
    {
        var config = plan.Configuration;
        var budget = config.Budgets.Reports ?? new();
        WebFormsReviewPreflightCommand.ValidateReportBudgets(budget);
        var attach = config.Operation == "attach";
        var parentPath = attach ? config.ParentScanRoot! : scanPath;
        var parentIndex = Path.Combine(parentPath, "index.sqlite");
        var indexPath = Path.Combine(reportPath, "combined.sqlite");
        var options = new CombineOptions(attach ? [parentIndex, Path.Combine(scanPath, "index.sqlite")] : [parentIndex],
            indexPath, attach ? ["retained", "compiled"] : ["retained"])
        {
            CompiledAttachments = attach ? [new(parentIndex, Path.Combine(parentPath, "scan-manifest.json"),
                Path.Combine(scanPath, "index.sqlite"), Path.Combine(scanPath, "scan-manifest.json"))] : [],
            MaxAttachmentIndexBytes = config.Budgets.MaxRetainedArtifactBytes,
            MaxAttachmentHashBytes = config.Budgets.MaxTotalHashBytes
        };
        if (Directory.Exists(reportPath) || File.Exists(reportPath)) throw Invalid("OUTPUT_EXISTS");
        Directory.CreateDirectory(reportPath);
        var combined = await CombinedIndexBuilder.CombineAsync(options, cancellationToken);
        string? selectionPath = null;
        long selectionBytes = 0;
        if (config.PageMode == "selected")
        {
            if (config.PageRelativePaths.Any(page => page.Any(character => character < ' '))) throw Invalid("PAGE_SCOPE_UNREPRESENTABLE");
            selectionPath = Path.Combine(reportPath, "selected-pages.local.txt");
            var selection = Encoding.UTF8.GetBytes(string.Join('\n', config.PageRelativePaths) + "\n");
            selectionBytes = selection.Length;
            if (selectionBytes >= budget.MaxOutputBytes) throw Invalid("OUTPUT_LIMIT");
            await using var stream = new FileStream(selectionPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await stream.WriteAsync(selection, cancellationToken);
        }
        var packet = await WebFormsModernizationPacketReporter.BuildAsync(new(indexPath, reportPath,
            MaxSurfaces: budget.MaxSurfaces, MaxEventChains: budget.MaxEventChains, MaxGaps: budget.MaxGaps,
            MaxDepth: config.Budgets.GraphMaxDepth, MaxPaths: config.Budgets.GraphMaxPaths,
            MaxInputFacts: budget.MaxInputFacts, MaxInputEdges: budget.MaxInputEdges,
            MaxInputTextBytes: budget.MaxInputTextBytes, SurfaceListPath: selectionPath,
            MaxTraversalWork: checked((int)config.Budgets.GraphMaxWork), MaxFrontier: budget.MaxFrontier), cancellationToken);
        var source = combined.Sources.Single(item => item.Label == "retained");
        var allRoots = packet.EventChains.Where(chain => !string.IsNullOrWhiteSpace(chain.HandlerSymbol))
            .Select(chain => new CombinedPathSymbolRoot(source.SourceIndexId, source.ScanId, source.CommitSha, chain.HandlerSymbol!))
            .Distinct().OrderBy(root => root.SymbolId, StringComparer.Ordinal).ToArray();
        var roots = allRoots.Take(budget.MaxCompiledRoots).ToArray();
        var paths = await CombinedDependencyPathReporter.BuildSelectedSymbolsAsync(new(indexPath, reportPath,
            ToSurface: "database-api", IncludeLegacyRoots: true, MaxDepth: config.Budgets.GraphMaxDepth,
            MaxPaths: config.Budgets.GraphMaxPaths, MaxFrontier: budget.MaxFrontier)
            { MaxTraversalWork = checked((int)config.Budgets.GraphMaxWork) }, roots, combinedIndex: true,
            new(budget.MaxInputFacts, budget.MaxInputEdges, budget.MaxInputTextBytes), cancellationToken);
        var index = await WebFormsReviewPreflightCommand.HashAsync("combined-index", indexPath, config.Budgets.MaxRetainedArtifactBytes, cancellationToken);
        var projectionLimits = new GroupedCompiledPathLimits(budget.MaxProjectionInputBytes, budget.MaxOutputBytes - selectionBytes,
            Math.Max(1, config.Budgets.GraphMaxPaths), budget.MaxProjectionRecords, budget.MaxProjectionReferences);
        var grouped = GroupedCompiledPathHandoffBuilder.Create(paths, index.Sha256, projectionLimits, cancellationToken);
        var compiled = await GroupedCompiledPathReportWriter.WriteAsync(grouped, Path.Combine(reportPath, "compiled"),
            projectionLimits, cancellationToken);
        var gaps = new List<string>();
        if (packet.Summary.Truncated) gaps.Add("PagePacketTruncated");
        if (paths.Summary.Truncated) gaps.Add("CompiledPathsTruncated");
        if (allRoots.Length > roots.Length) gaps.Add("CompiledRootLimitReached");
        if (paths.Summary.SelectorCandidateCount != roots.Length) gaps.Add("CompiledRootResolutionIncomplete");
        if (roots.Length == 0) gaps.Add("RetainedHandlerSymbolsUnavailable");
        if (packet.Gaps.Count > 0) gaps.Add("PagePacketCoverageGaps");
        if (paths.Gaps.Count > 0) gaps.Add("CompiledPathCoverageGaps");
        if (config.PageMode == "all") gaps.Add("AllPagesCompiledReceiptPartitioningPending");
        var manifests = new List<ScanManifest> { await ManifestAsync(parentPath, cancellationToken) };
        if (attach) manifests.Add(await ManifestAsync(scanPath, cancellationToken));
        var generator = await WebFormsReviewPreflightCommand.HashAsync("generator", typeof(WebFormsReviewReportExecution).Assembly.Location,
            67_108_864, cancellationToken);
        var packetHash = CanonicalHash(packet, budget.MaxProjectionInputBytes, cancellationToken);
        var inputHash = CanonicalHash(new
        {
            policy = Policy(plan, scanPath, reportPath), preflightInputSha256 = plan.BoundedInputSha256,
            indexSha256 = index.Sha256, packetSha256 = packetHash,
            scanManifestsSha256 = CanonicalHash(manifests, 8_388_608, cancellationToken),
            compiledHandoffSha256 = compiled.HandoffSha256, generatorSha256 = generator.Sha256,
            reportingGeneratorSha256 = grouped.GeneratorSha256
        }, budget.MaxProjectionInputBytes, cancellationToken);
        var handoff = new NativeWebFormsReviewHandoff(Schema, RuleId, "Tier2Structural", "local-only",
            "review-only-static-not-runtime", generator.Sha256, grouped.GeneratorSha256, inputHash,
            plan.RunId, gaps.Count > 0 ? "partial-static-review" : "retained-static-review", index.Sha256, packetHash,
            config, plan.Inputs, manifests, packet, "compiled/" + GroupedCompiledPathReportWriter.HandoffName,
            compiled.HandoffSha256, allRoots.Length, allRoots.Length - roots.Length,
            gaps.Order(StringComparer.Ordinal).ToArray(),
            ["Page-chain verdicts are retained from the existing packet; compiled paths are a separate review-only supplement.",
             "No source excerpts, source scan, build, runtime SQL execution, cleanup or external publication occurred in the report phase.",
             "Exact index/input/DLL/manifest hashes retain local byte provenance, not build authenticity or source-line identity.",
             "Graph admission remains bounded and may fail closed before classifying paths. Representative eight-times scale remains unverified.",
             "All identities, locations, commits and input fingerprints remain private; this is not a shareable artifact."]);
        var remaining = budget.MaxOutputBytes - compiled.OutputBytes - selectionBytes;
        var outputBudget = new ByteBudget(remaining, cancellationToken);
        string handoffHash;
        await using (var file = new FileStream(Path.Combine(reportPath, HandoffName), FileMode.CreateNew, FileAccess.Write, FileShare.None))
        await using (var bounded = new BoundedStream(file, outputBudget))
        {
            await JsonSerializer.SerializeAsync(bounded, handoff, Json, cancellationToken);
            await bounded.FlushAsync(cancellationToken);
            handoffHash = bounded.Digest();
        }
        var handoffBytes = outputBudget.Bytes;
        string htmlHash;
        await using (var file = new FileStream(Path.Combine(reportPath, "index.html"), FileMode.CreateNew, FileAccess.Write, FileShare.None))
        await using (var bounded = new BoundedStream(file, outputBudget))
        using (var writer = new StreamWriter(bounded, new UTF8Encoding(false), 4096, leaveOpen: true))
        {
            Render(writer, handoff, paths.Paths.Count, grouped.Chains.Count, cancellationToken);
            await writer.FlushAsync(cancellationToken);
            htmlHash = bounded.Digest();
        }
        var after = await WebFormsReviewPreflightCommand.HashAsync("combined-index", indexPath, index.Bytes, cancellationToken);
        if (after != index) throw Invalid("INDEX_CHANGED");
        var generated = new List<WebFormsReviewArtifact>
        {
            new("combined.sqlite", index.Bytes, index.Sha256),
            new(HandoffName, handoffBytes, handoffHash),
            new("index.html", outputBudget.Bytes - handoffBytes, htmlHash),
            new("compiled/" + GroupedCompiledPathReportWriter.HandoffName, new FileInfo(compiled.HandoffPath).Length, compiled.HandoffSha256),
            new("compiled/" + GroupedCompiledPathReportWriter.HtmlName, new FileInfo(compiled.HtmlPath).Length, compiled.HtmlSha256)
        };
        if (selectionPath is not null) generated.Add(new("selected-pages.local.txt", selectionBytes,
            Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', config.PageRelativePaths) + "\n")))));
        return new(handoff.Coverage, packet.Surfaces.Count, paths.Paths.Count, handoff.Gaps, inputHash,
            generated.OrderBy(item => item.RelativePath, StringComparer.Ordinal).ToArray());
    }

    private static void Render(TextWriter writer, NativeWebFormsReviewHandoff handoff, int paths, int chains, CancellationToken token)
    {
        void W(string value) { token.ThrowIfCancellationRequested(); writer.WriteLine(value); }
        W("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><link rel=\"icon\" href=\"data:,\"><title>Native Web Forms review workbench</title><style>body{font:16px/1.5 system-ui,sans-serif;margin:2rem auto;padding:0 1rem;max-width:1100px;color:#17212b;overflow-wrap:anywhere}section{margin:1rem 0;border:1px solid #ccd8e3;padding:1rem}.notice{border-left:4px solid #a34024;background:#fff4e9;padding:1rem}td,th{border:1px solid #ccd8e3;padding:.5rem;text-align:left;vertical-align:top;overflow-wrap:anywhere}table{border-collapse:collapse;width:100%;table-layout:fixed}code,pre{white-space:pre-wrap;overflow-wrap:anywhere}a{color:#12599b}summary{cursor:pointer}@media(max-width:650px){thead{display:none}table,tbody,tr,td{display:block;width:auto}tr{margin:.5rem 0}td:before{content:attr(data-label);display:block;font-weight:bold}td+td{border-top:0}}</style></head><body>");
        W("<h1>Native Web Forms review workbench</h1><p class=\"notice\">PRIVATE — retained static evidence only. Compiled paths do not override page-chain verdicts and do not prove SQL executed.</p>");
        W($"<p>Run {H(handoff.RunId)} · coverage {H(handoff.Coverage)} · page mode {H(handoff.Configuration.PageMode)} · {handoff.Packet.Surfaces.Count} retained surfaces. <a href=\"{HandoffName}\">Native handoff JSON</a></p>");
        W($"<p><a href=\"compiled/{GroupedCompiledPathReportWriter.HtmlName}\">Compiled method paths</a>: {chains} exact chains, {paths} evidence variants. Requested roots {handoff.RequestedCompiledRoots}; omitted roots {handoff.OmittedCompiledRoots}. <a href=\"compiled/{GroupedCompiledPathReportWriter.HandoffName}\">Lossless compiled handoff</a></p>");
        W("<p>Call accounting: P = retained chain-associated call projections; F = distinct retained fact/scan/commit identities; S = retained normalized site identities. Missing site IDs are reported separately, not guessed.</p><table><thead><tr><th>Page / controls</th><th>Calls P / F / S</th><th>Retained page-chain verdicts</th></tr></thead><tbody>");
        foreach (var surface in handoff.Packet.Surfaces)
        {
            var events = handoff.Packet.EventChains.Where(chain => chain.SurfaceId == surface.SurfaceId).ToArray();
            var calls = events.SelectMany(chain => chain.CallEvidence).ToArray();
            var facts = calls.Select(call => (call.Evidence.FactId, call.Evidence.ScanId, call.Evidence.CommitSha)).Distinct().Count();
            var sites = calls.Where(call => !string.IsNullOrEmpty(call.CallSiteId)).Select(call => call.CallSiteId).Distinct(StringComparer.Ordinal).Count();
            W($"<tr><td data-label=\"Page / controls\">{H(surface.Evidence.FilePath)}<br>{surface.Controls.Count} controls; {events.Length} chains<details><summary>Retained control identities</summary><ul>");
            foreach (var control in surface.Controls) W($"<li>{H(control.DeclaredId)} ({H(control.ControlType)})</li>");
            W($"</ul></details></td><td data-label=\"Calls P / F / S\">{calls.Length} / {facts} / {sites}<br>{calls.Count(call => string.IsNullOrEmpty(call.CallSiteId))} missing site IDs</td><td data-label=\"Retained page-chain verdicts\"><ul>");
            foreach (var chain in events) W($"<li>{H(chain.HandlerSymbol ?? chain.HandlerResolution)}: {H(chain.Classification)}<br>{H(string.Join(", ", chain.RuleIds))}; {H(string.Join(", ", chain.EvidenceTiers))}</li>");
            W("</ul></td></tr>");
        }
        W("</tbody></table><section><h2>Coverage, gaps and exact provenance</h2><ul>");
        foreach (var gap in handoff.Gaps) W($"<li>{H(gap)}</li>");
        foreach (var limitation in handoff.Limitations) W($"<li>{H(limitation)}</li>");
        W($"</ul><p>Packet gaps {handoff.Packet.Gaps.Count}; packet truncated {H(handoff.Packet.Summary.Truncated.ToString())}. Complete retained records are in the native handoff.</p><details><summary>Declared source, published and input locations</summary><p>Source root: {H(handoff.Configuration.SourceRoot)}<br>Published root: {H(handoff.Configuration.PublishedRoot)}<br>Receipt root: {H(handoff.Configuration.ReceiptRoot)}</p><ul>");
        foreach (var input in handoff.InputInventory) W($"<li>{H(input.Role)} — {H(input.Path)}<br>SHA-256 <code>{H(input.Sha256)}</code>; bytes {input.Bytes}</li>");
        W($"</ul></details><p>Rule {H(handoff.RuleId)}; tier {H(handoff.EvidenceTier)}<br>Generator <code>{H(handoff.GeneratorSha256)}</code><br>Bounded input <code>{H(handoff.BoundedInputSha256)}</code></p></section></body></html>");
    }

    private static string H(string? value) => WebUtility.HtmlEncode(value ?? "unavailable");
    private static async Task<ScanManifest> ManifestAsync(string path, CancellationToken token)
    {
        var bytes = await WebFormsReviewPreflightCommand.ReadSmallAsync(Path.Combine(path, "scan-manifest.json"), 4_194_304, token);
        WebFormsReviewPreflightCommand.RejectDuplicateProperties(bytes);
        return JsonSerializer.Deserialize<ScanManifest>(bytes, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw Invalid("MANIFEST_INVALID");
    }
    internal static string CanonicalHash<T>(T value, long maximumBytes, CancellationToken token)
    {
        using var stream = new BoundedStream(null, new(maximumBytes, token));
        JsonSerializer.Serialize(stream, value, Json);
        return stream.Digest();
    }
    private static InvalidDataException Invalid(string category) => new("WEBFORMS_NATIVE_REPORT_" + category);
    private sealed class ByteBudget(long maximum, CancellationToken token)
    {
        public long Bytes { get; private set; }
        public void Consume(int count)
        {
            token.ThrowIfCancellationRequested();
            if (maximum <= 0 || count > maximum - Bytes) throw Invalid("OUTPUT_LIMIT");
            Bytes += count;
        }
    }
    private sealed class BoundedStream(Stream? inner, ByteBudget budget) : Stream
    {
        private readonly IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        public string Digest() => Convert.ToHexStringLower(hash.GetHashAndReset());
        public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));
        public override void Write(ReadOnlySpan<byte> buffer) { budget.Consume(buffer.Length); inner?.Write(buffer); hash.AppendData(buffer); }
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested(); budget.Consume(buffer.Length);
            if (inner is not null) await inner.WriteAsync(buffer, cancellationToken);
            hash.AppendData(buffer.Span);
        }
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        protected override void Dispose(bool disposing) { if (disposing) hash.Dispose(); base.Dispose(disposing); }
        public override bool CanRead => false; public override bool CanSeek => false; public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => inner?.Flush();
        public override Task FlushAsync(CancellationToken cancellationToken) => inner?.FlushAsync(cancellationToken) ?? Task.CompletedTask;
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
