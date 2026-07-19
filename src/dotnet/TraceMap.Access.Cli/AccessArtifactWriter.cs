using System.Text;
using Microsoft.Data.Sqlite;
using TraceMap.Access;
using TraceMap.Core;
using TraceMap.Storage;

namespace TraceMap.Access.Cli;

public static class AccessArtifactWriter
{
    public static async Task WriteAsync(string outputPath, ScanResult result, AccessLimits limits, CancellationToken cancellationToken = default)
    {
        var target = Path.GetFullPath(outputPath);
        var parent = Path.GetDirectoryName(target) ?? throw new AccessScanException("AccessUnsafeOutputPath");
        Directory.CreateDirectory(parent);
        var staging = Path.Combine(parent, $".tracemap-access-staging-{Guid.NewGuid():N}");
        Directory.CreateDirectory(staging);
        try
        {
            var logs = Path.Combine(staging, "logs");
            Directory.CreateDirectory(logs);
            try { await ManifestWriter.WriteAsync(Path.Combine(staging, "scan-manifest.json"), result.Manifest, cancellationToken); }
            catch { throw new AccessScanException("AccessManifestWriteFailed"); }
            try { await JsonlFactWriter.WriteAsync(Path.Combine(staging, "facts.ndjson"), result.Facts, cancellationToken); }
            catch { throw new AccessScanException("AccessFactsWriteFailed"); }
            try
            {
                SqliteIndexWriter.Write(Path.Combine(staging, "index.sqlite"), result.Manifest, result.Facts);
                // The shared writer uses SQLite connection pooling. Release the pooled
                // handle before atomically renaming the staging directory on Windows.
                SqliteConnection.ClearAllPools();
            }
            catch (SqliteException exception)
            {
                // SQLite error codes are bounded numeric diagnostics and cannot expose
                // repository or database material. Keep enough detail to distinguish a
                // schema/constraint failure from an environmental I/O failure.
                throw new AccessScanException($"AccessIndexWriteFailed-Sqlite{exception.SqliteErrorCode}-Extended{exception.SqliteExtendedErrorCode}");
            }
            catch { throw new AccessScanException("AccessIndexWriteFailed"); }
            try { await File.WriteAllTextAsync(Path.Combine(staging, "report.md"), Report(result), new UTF8Encoding(false), cancellationToken); }
            catch { throw new AccessScanException("AccessReportWriteFailed"); }
            try { await File.WriteAllTextAsync(Path.Combine(logs, "analyzer.log"), AnalyzerLog(result), new UTF8Encoding(false), cancellationToken); }
            catch { throw new AccessScanException("AccessAnalyzerLogWriteFailed"); }
            var totalBytes = Directory.EnumerateFiles(staging, "*", SearchOption.AllDirectories).Sum(path => new FileInfo(path).Length);
            if (totalBytes > limits.MaxArtifactBytes) throw new AccessScanException("AccessArtifactLimitReached");

            if (Directory.Exists(target)) Directory.Delete(target, recursive: true);
            else if (File.Exists(target)) throw new AccessScanException("AccessOutputIsFile");
            try { Directory.Move(staging, target); }
            catch { throw new AccessScanException("AccessArtifactPublishFailed"); }
        }
        catch
        {
            try { if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true); } catch { }
            throw;
        }
    }

    public static string Report(ScanResult result)
    {
        var lines = new List<string>
        {
            "# TraceMap Microsoft Access Design Evidence",
            "",
            $"- Repository: `{Inline(result.Manifest.RepoName)}`",
            $"- Commit SHA: `{Inline(result.Manifest.CommitSha)}`",
            $"- Analysis: `{result.Manifest.AnalysisLevel}` / `{result.Manifest.BuildStatus}`",
            "- Coverage: `reduced-static-design`",
            "",
            "## Evidence Counts",
            ""
        };
        foreach (var group in result.Facts.GroupBy(fact => fact.FactType).OrderBy(group => group.Key, StringComparer.Ordinal))
            lines.Add($"- `{Inline(group.Key)}`: {group.Count()}");
        lines.Add("");
        lines.Add("## Rules and Evidence Tiers");
        lines.Add("");
        lines.Add("| Rule ID | Tier | Count |");
        lines.Add("| --- | --- | ---: |");
        foreach (var group in result.Facts.GroupBy(fact => (fact.RuleId, fact.EvidenceTier)).OrderBy(group => group.Key.RuleId, StringComparer.Ordinal).ThenBy(group => group.Key.EvidenceTier, StringComparer.Ordinal))
            lines.Add($"| `{Inline(group.Key.RuleId)}` | `{Inline(group.Key.EvidenceTier)}` | {group.Count()} |");
        lines.Add("");
        lines.Add("## Coverage Gaps");
        lines.Add("");
        var gaps = result.Facts.Where(fact => fact.FactType == FactTypes.AnalysisGap).ToArray();
        if (gaps.Length == 0) lines.Add("- No extractor gaps were recorded for the selected v0 collections; coverage remains reduced by design.");
        else foreach (var group in gaps.GroupBy(fact => fact.Properties.GetValueOrDefault("classification") ?? "AccessUnknownGap").OrderBy(group => group.Key, StringComparer.Ordinal))
            lines.Add($"- `{Inline(group.Key)}`: {group.Count()}");
        lines.Add("");
        lines.Add("## Static Evidence Boundaries");
        lines.Add("");
        lines.Add("- No table rows, row counts, attachment/OLE contents, or query resultsets were read.");
        lines.Add("- No saved query, action query, pass-through query, linked source, macro, form, report, or VBA procedure was executed.");
        lines.Add("- Raw SQL, connection strings, external source values, credentials, private hosts, local absolute paths, VBA, macro bodies, and form/report expressions are omitted or role-hashed.");
        lines.Add("- Evidence does not prove runtime reachability, linked-source availability, permissions, production state, release approval, or that a change is safe.");
        lines.Add("");
        return string.Join('\n', lines);
    }

    public static string AnalyzerLog(ScanResult result)
    {
        var lines = new List<string>
        {
            $"scanId={result.Manifest.ScanId}",
            $"repo={result.Manifest.RepoName}",
            $"commitSha={result.Manifest.CommitSha}",
            $"analysisLevel={result.Manifest.AnalysisLevel}",
            $"buildStatus={result.Manifest.BuildStatus}",
            $"facts={result.Facts.Count}",
            $"gaps={result.Facts.Count(fact => fact.FactType == FactTypes.AnalysisGap)}",
            "rowDataRead=false",
            "executionPerformed=false"
        };
        lines.AddRange(result.Manifest.KnownGaps.Select(gap => $"knownGap={gap}"));
        return string.Join('\n', lines) + "\n";
    }

    private static string Inline(string value) => value.Replace("`", "'", StringComparison.Ordinal).Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
}
