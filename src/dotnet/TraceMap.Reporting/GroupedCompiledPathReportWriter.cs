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
            W("<p class=\"notice\">LOCAL ONLY — review-only static candidates, not runtime execution or proof that SQL ran. Grouping is a display/index operation; no evidence variant was removed.</p>");
            W($"<p>{handoff.Chains.Count} exact chains · {handoff.Variants.Count} retained variants · coverage {H(report.ReportCoverage)} · truncated {H(report.Summary.Truncated.ToString())}. <a href=\"{HandoffName}\">Lossless indexed handoff JSON</a></p>");
            W("<nav aria-label=\"Method chains\"><ul>");
            for (var index = 0; index < handoff.Chains.Count; index++)
            {
                var group = handoff.Chains[index];
                var path = report.Paths[group.VariantIndexes[0]];
                W($"<li><a href=\"#chain-{index + 1}\">Chain {index + 1}: {H(path.Nodes.FirstOrDefault() is { } first ? Compact(first) : path.StartNodeId)}</a> — {group.VariantIndexes.Count} variants</li>");
            }
            W("</ul></nav>");
            for (var index = 0; index < handoff.Chains.Count; index++)
            {
                var group = handoff.Chains[index];
                var path = report.Paths[group.VariantIndexes[0]];
                W($"<section id=\"chain-{index + 1}\"><h2>Chain {index + 1} <small>({group.VariantIndexes.Count} variants)</small></h2>");
                W($"<p>{H(string.Join(" → ", path.Nodes.Select(Compact)))}</p>");
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
            foreach (var gap in report.Gaps.Where(gap => gap.CutoffWitness is not null).Take(6))
            {
                var witness = gap.CutoffWitness!;
                W($"<details><summary>{H(gap.Reason)}: {H(Location(gap.FilePath, gap.StartLine, gap.EndLine))}</summary><p>Prefix truncated: {witness.PrefixTruncated}; final candidate edge not traversed: {witness.LastEdgeNotTraversed}</p><ol>");
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
