using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TraceMap.Access.Cli;
using TraceMap.Core;
using TraceMap.Reporting;
using TraceMap.Storage;

namespace TraceMap.Tests;

public sealed class AccessCopyCloneCandidateTests
{
    private const string Commit = "3333333333333333333333333333333333333333";
    private const string ProtectedMarker = "Customer_Copy_Secret_24819";
    private const string Form = "access-form-11111111111111111111111111111111";
    private const string AppendQuery = "access-query-22222222222222222222222222222222";
    private const string MakeTableQuery = "access-query-33333333333333333333333333333333";
    private const string SelectQuery = "access-query-44444444444444444444444444444444";
    private const string TableA = "access-table-55555555555555555555555555555555";
    private const string TableB = "access-table-66666666666666666666666666666666";

    [Fact]
    public void Builder_emits_only_conservative_candidates_with_flow_and_exact_provenance()
    {
        var facts = CandidateFacts();

        var first = AccessCopyCloneCandidateReporter.Build("synthetic", Commit, facts, 100, 100, 100);
        var second = AccessCopyCloneCandidateReporter.Build("synthetic", Commit, facts.Reverse().ToArray(), 100, 100, 100);

        Assert.Equal(JsonSerializer.Serialize(first), JsonSerializer.Serialize(second));
        Assert.StartsWith("repo-", first.RepositoryId, StringComparison.Ordinal);
        Assert.Equal(Commit, first.CommitSha);
        Assert.Equal(2, first.Summary.CandidateCount);
        var append = Assert.Single(first.Candidates, candidate => candidate.Shape == "bulk-append-shape");
        Assert.Equal("Candidate", append.Classification);
        Assert.Equal(RuleIds.LegacyAccessCopyCloneCandidate, append.RuleId);
        Assert.Contains(append.EvidenceTier, new[]
        {
            EvidenceTiers.Tier2Structural,
            EvidenceTiers.Tier3SyntaxOrTextual,
            EvidenceTiers.Tier4Unknown
        });
        Assert.Equal(Commit, append.CommitSha);
        Assert.Equal("fixture.accdb", append.FilePath);
        Assert.Equal(1, append.StartLine);
        Assert.Equal(1, append.EndLine);
        Assert.NotEmpty(append.ExtractorVersion);
        Assert.NotEmpty(append.FlowPathIds);
        Assert.Equal(2, append.Participants.Count);
        Assert.All(append.Participants, participant => Assert.Equal("dependency-role-unknown", participant.Role));
        Assert.Contains(append.RuleIds, rule => rule == RuleIds.LegacyAccessCopyCloneCandidate);
        Assert.Contains(append.SupportingFactIds, id => id == "fact-append");
        Assert.All(append.Evidence, evidence =>
        {
            Assert.Equal(Commit, evidence.CommitSha);
            Assert.Equal("fixture.accdb", evidence.FilePath);
            Assert.NotEmpty(evidence.RuleId);
            Assert.NotEmpty(evidence.ExtractorVersion);
        });
        Assert.DoesNotContain(first.Candidates, candidate => candidate.QueryNodeId.Contains(SelectQuery, StringComparison.Ordinal));
        Assert.DoesNotContain(first.Candidates.Select(candidate => candidate.Shape), shape => shape.Contains("clone", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(first.Gaps, gap => gap.Classification == "AccessCopyCloneRoleDirectionUnavailable");
        Assert.Contains(first.Gaps, gap => gap.Classification == "AccessCopyCloneFieldCorrespondenceUnavailable");
        Assert.Contains(first.Gaps, gap => gap.Classification == "AccessCopyCloneDependencyFanOutNeedsReview");
        Assert.Contains(first.Gaps, gap => gap.Classification == "AccessCopyCloneExternalSourcePartial");
        Assert.Contains(first.Gaps, gap => gap.Classification == "AccessCopyCloneParentChildSequenceUnavailable");
        Assert.Contains(first.Gaps, gap => gap.Classification == "AccessCopyCloneUpstreamEvidenceGap");
    }

    [Fact]
    public void Builder_does_not_infer_clone_from_name_or_ordinary_select_and_bounds_explicitly()
    {
        var nameOnly = new[]
        {
            Fact("fact-name-only", FactTypes.AccessQueryDeclared, RuleIds.LegacyAccessQuery, EvidenceTiers.Tier2Structural, null, SelectQuery,
                ("queryKind", "select"), ("displayName", "CloneEverything"), ("coverageLabel", "complete")),
            Fact("fact-dynamic", FactTypes.AnalysisGap, RuleIds.LegacyAccessVba, EvidenceTiers.Tier4Unknown, null, null,
                ("classification", "AccessVbaDynamicDispatch"), ("coverageLabel", "partial"))
        };

        var noCandidate = AccessCopyCloneCandidateReporter.Build("synthetic", Commit, nameOnly, 1, 1, 10);

        Assert.Empty(noCandidate.Candidates);
        Assert.Contains(noCandidate.Gaps, gap => gap.Classification == "AccessCopyCloneCandidateEvidenceUnavailable");
        Assert.Contains(noCandidate.Gaps, gap => gap.Classification == "AccessCopyCloneUpstreamEvidenceGap");

        var bounded = AccessCopyCloneCandidateReporter.Build("synthetic", Commit, CandidateFacts(), 1, 1, 1);
        Assert.True(bounded.Summary.Truncated);
        Assert.Single(bounded.Candidates);
        Assert.Single(bounded.Gaps);
        Assert.Equal("AccessCopyCloneGapLimitReached", bounded.Gaps[0].Classification);
    }

    [Fact]
    public void Builder_suppresses_windows_absolute_evidence_paths_on_every_host()
    {
        var facts = CandidateFacts();
        var query = facts.Single(fact => fact.FactId == "fact-append") with
        {
            Evidence = facts.Single(fact => fact.FactId == "fact-append").Evidence with
            {
                FilePath = "Z:/operator-local/private.accdb"
            }
        };
        facts = facts.Where(fact => fact.FactId != query.FactId).Append(query).ToArray();

        var report = AccessCopyCloneCandidateReporter.Build("synthetic", Commit, facts, 100, 100, 100);

        var candidate = Assert.Single(report.Candidates, item => item.Shape == "bulk-append-shape");
        Assert.Equal("unavailable", candidate.FilePath);
        Assert.DoesNotContain(report.Candidates.SelectMany(item => item.Evidence),
            evidence => evidence.FilePath.Contains("operator-local", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Cli_writes_deterministic_private_safe_artifacts_without_changing_the_index()
    {
        using var temp = new TempDirectory();
        var index = Path.Combine(temp.Path, "index.sqlite");
        var manifest = new ScanManifest(
            "scan-access-copy",
            "synthetic",
            null,
            "dev",
            Commit,
            "tracemap-access/0.1.0",
            DateTimeOffset.UnixEpoch,
            "Level1SemanticAnalysisReduced",
            "FailedOrPartial",
            [],
            [],
            [],
            []);
        SqliteIndexWriter.Write(index, manifest, CandidateFacts());
        var before = Sha256(File.ReadAllBytes(index));
        var first = Path.Combine(temp.Path, "copy-candidates");
        var second = Path.Combine(temp.Path, "copy-candidates-second");
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();

        var exit = await AccessCommand.RunAsync(
            ["copy-clone", "--index", index, "--out", first, "--max-candidates", "100"],
            stdout,
            stderr);
        var secondExit = await AccessCommand.RunAsync(
            ["copy-clone", "--index", index, "--out", second, "--max-candidates", "100"],
            TextWriter.Null,
            stderr);

        Assert.Equal(0, exit);
        Assert.Equal(0, secondExit);
        Assert.Equal(before, Sha256(File.ReadAllBytes(index)));
        Assert.Equal(
            Directory.EnumerateFiles(first).OrderBy(Path.GetFileName).Select(path => Sha256(File.ReadAllBytes(path))),
            Directory.EnumerateFiles(second).OrderBy(Path.GetFileName).Select(path => Sha256(File.ReadAllBytes(path))));
        Assert.True(File.Exists(Path.Combine(first, "access-copy-clone.md")));
        Assert.True(File.Exists(Path.Combine(first, "access-copy-clone.json")));
        Assert.Contains("Candidates:", stdout.ToString(), StringComparison.Ordinal);
        Assert.Empty(stderr.ToString());
        foreach (var file in Directory.EnumerateFiles(first))
        {
            var text = Encoding.UTF8.GetString(File.ReadAllBytes(file));
            Assert.DoesNotContain(ProtectedMarker, text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("CloneEverything", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("INSERT INTO", text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(temp.Path, text, StringComparison.Ordinal);
            Assert.Contains("does not prove", text, StringComparison.OrdinalIgnoreCase);
        }

        if (!OperatingSystem.IsWindows())
        {
            var linkedParent = Path.Combine(temp.Path, "linked-parent");
            Directory.CreateSymbolicLink(linkedParent, temp.Path);
            var unsafeExit = await AccessCommand.RunAsync(
                ["copy-clone", "--index", index, "--out", Path.Combine(linkedParent, "unsafe-output")],
                TextWriter.Null,
                stderr);
            Assert.Equal(1, unsafeExit);
            Assert.Contains("AccessCopyCloneOutputInvalid", stderr.ToString(), StringComparison.Ordinal);
        }
    }

    private static CodeFact[] CandidateFacts() =>
    [
        Fact("fact-form", FactTypes.AccessFormDeclared, RuleIds.LegacyAccessUiSurface, EvidenceTiers.Tier2Structural, null, Form,
            ("coverageLabel", "structured-design-observed")),
        Fact("fact-append", FactTypes.AccessQueryDeclared, RuleIds.LegacyAccessQuery, EvidenceTiers.Tier2Structural, null, AppendQuery,
            ("queryKind", "append"), ("coverageLabel", "complete"), ("displayName", ProtectedMarker), ("rawSql", "INSERT INTO Secret SELECT * FROM Private")),
        Fact("fact-make-table", FactTypes.AccessQueryDeclared, RuleIds.LegacyAccessQuery, EvidenceTiers.Tier2Structural, null, MakeTableQuery,
            ("queryKind", "make-table"), ("coverageLabel", "complete")),
        Fact("fact-select", FactTypes.AccessQueryDeclared, RuleIds.LegacyAccessQuery, EvidenceTiers.Tier2Structural, null, SelectQuery,
            ("queryKind", "select"), ("coverageLabel", "complete"), ("displayName", "CloneEverything")),
        Fact("fact-table-a", FactTypes.LegacyDataEntityDeclared, RuleIds.LegacyAccessSchema, EvidenceTiers.Tier2Structural, null, TableA,
            ("coverageLabel", "complete")),
        Fact("fact-table-b", FactTypes.LegacyDataEntityDeclared, RuleIds.LegacyAccessSchema, EvidenceTiers.Tier2Structural, null, TableB,
            ("coverageLabel", "complete")),
        Fact("fact-external-table-b", FactTypes.AccessExternalLinkDeclared, RuleIds.LegacyAccessExternalLink, EvidenceTiers.Tier2Structural, TableB, null,
            ("boundaryKind", "odbc"), ("coverageLabel", "hash-only-boundary")),
        Fact("fact-form-append", FactTypes.AccessBindingDeclared, RuleIds.LegacyAccessBinding, EvidenceTiers.Tier3SyntaxOrTextual, Form, AppendQuery,
            ("targetKind", "query"), ("coverageLabel", "direct-static-reference")),
        Fact("fact-append-table-a", FactTypes.AccessQueryDependencyCandidate, RuleIds.LegacyAccessQuery, EvidenceTiers.Tier3SyntaxOrTextual, AppendQuery, TableA,
            ("targetKind", "table"), ("coverageLabel", "direct-static-reference")),
        Fact("fact-append-table-b", FactTypes.AccessQueryDependencyCandidate, RuleIds.LegacyAccessQuery, EvidenceTiers.Tier3SyntaxOrTextual, AppendQuery, TableB,
            ("targetKind", "table"), ("coverageLabel", "direct-static-reference")),
        Fact("fact-make-table-source", FactTypes.AccessQueryDependencyCandidate, RuleIds.LegacyAccessQuery, EvidenceTiers.Tier3SyntaxOrTextual, MakeTableQuery, TableA,
            ("targetKind", "table"), ("coverageLabel", "partial")),
        Fact("fact-dynamic", FactTypes.AnalysisGap, RuleIds.LegacyAccessVba, EvidenceTiers.Tier4Unknown, null, null,
            ("classification", "AccessVbaDynamicDispatch"), ("coverageLabel", "partial"))
    ];

    private static CodeFact Fact(
        string id,
        string factType,
        string ruleId,
        string tier,
        string? source,
        string? target,
        params (string Key, string Value)[] values)
    {
        var properties = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["limitations"] = "static-evidence-only;no-execution"
        };
        foreach (var (key, value) in values) properties[key] = value;
        return new(
            id,
            "scan-access-copy",
            "synthetic",
            Commit,
            null,
            factType,
            ruleId,
            tier,
            source,
            target,
            null,
            new("fixture.accdb", 1, 1, null, "AccessSourceNeutralDesignEvidence", "access-design-evidence/0.1.0"),
            properties);
    }

    private static string Sha256(byte[] value) =>
        Convert.ToHexString(SHA256.HashData(value)).ToLowerInvariant();
}
