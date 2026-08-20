using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using TraceMap.Core;
using TraceMap.Reporting;
using TraceMap.Storage;

namespace TraceMap.Tests;

public sealed class PackageDecisionLockfileEvidenceTests
{
    private const string ValidLockfile = """
        {
          "version": 2,
          "dependencies": {
            "net8.0": {
              "Example.Direct": {
                "type": "Direct",
                "requested": "[1.0.0, )",
                "resolved": "1.0.0",
                "contentHash": "not-an-artifact-digest==",
                "dependencies": {
                  "Example.Transitive": "2.0.0"
                }
              },
              "Example.Transitive": {
                "type": "Transitive",
                "resolved": "2.0.0",
                "contentHash": "also-not-an-artifact-digest=="
              }
            },
            "net9.0": {
              "Example.Direct": {
                "type": "Direct",
                "requested": "[1.0.0, )",
                "resolved": "1.0.1",
                "contentHash": "still-not-an-artifact-digest=="
              }
            }
          }
        }
        """;

    [Fact]
    public void ReadNuGetLockfiles_extracts_direct_and_transitive_resolved_versions_per_target_framework()
    {
        using var temp = new TempDirectory();
        var repo = temp.Path;
        Directory.CreateDirectory(Path.Combine(repo, "src"));
        File.WriteAllText(Path.Combine(repo, "src", "packages.lock.json"), ValidLockfile);

        var result = ProjectFileReader.ReadNuGetLockfiles(repo, [new FileInventoryItem("src/packages.lock.json", "PackagesLock", ValidLockfile.Length)]);

        Assert.Empty(result.Gaps);
        var entries = result.Entries;
        Assert.Equal(3, entries.Count);
        var direct = Assert.Single(entries, entry => entry.PackageName == "Example.Direct" && entry.TargetFramework == "net8.0");
        Assert.Equal("1.0.0", direct.ResolvedVersion);
        Assert.Equal("direct", direct.DependencyRelation);
        Assert.Equal(1, direct.DependencyCount);
        Assert.Equal("Example.Transitive", direct.DependencyNames);
        Assert.Null(direct.ResolvedVersionHash);
        var transitive = Assert.Single(entries, entry => entry.PackageName == "Example.Transitive");
        Assert.Equal("2.0.0", transitive.ResolvedVersion);
        Assert.Equal("transitive", transitive.DependencyRelation);
        Assert.Equal(0, transitive.DependencyCount);
        Assert.Null(transitive.DependencyNames);
        var netNine = Assert.Single(entries, entry => entry.TargetFramework == "net9.0");
        Assert.Equal("1.0.1", netNine.ResolvedVersion);
        Assert.Equal(
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(repo, "src", "packages.lock.json")))).ToLowerInvariant()[..32],
            direct.LockfileHash);
        Assert.Equal(direct.LockfileHash, transitive.LockfileHash);
        Assert.Equal(5, direct.Line);
        Assert.Equal(14, transitive.Line);
        Assert.Equal(21, netNine.Line);
    }

    [Fact]
    public void ReadNuGetLockfiles_classifies_unsafe_and_unsupported_lockfile_shapes_as_gaps()
    {
        using var temp = new TempDirectory();
        var repo = temp.Path;
        Directory.CreateDirectory(Path.Combine(repo, "a"));
        Directory.CreateDirectory(Path.Combine(repo, "b"));
        Directory.CreateDirectory(Path.Combine(repo, "c"));
        Directory.CreateDirectory(Path.Combine(repo, "d"));
        File.WriteAllText(Path.Combine(repo, "a", "packages.lock.json"), "{ \"version\": 2, \"dependencies\": ");
        File.WriteAllText(Path.Combine(repo, "b", "packages.lock.json"), "{\"version\": 3, \"dependencies\": {\"net8.0\": {\"X\": {\"type\": \"Direct\", \"resolved\": \"1.0.0\"}}}}");
        File.WriteAllText(Path.Combine(repo, "c", "packages.lock.json"), """
            {
              "version": 1,
              "dependencies": {
                "net8.0": {
                  "../unsafe/name": { "type": "Direct", "resolved": "1.0.0" },
                  "Safe.Missing": { "type": "Direct" },
                  "Safe.UnsafeVersion": { "type": "Project", "resolved": "git+ssh://user:pass@example.invalid/x" }
                }
              }
            }
            """);
        File.WriteAllText(Path.Combine(repo, "d", "packages.lock.json"), "{\"dependencies\": {}}");

        var result = ProjectFileReader.ReadNuGetLockfiles(repo, new[]
        {
            new FileInventoryItem("a/packages.lock.json", "PackagesLock", 1),
            new FileInventoryItem("b/packages.lock.json", "PackagesLock", 1),
            new FileInventoryItem("c/packages.lock.json", "PackagesLock", 1),
            new FileInventoryItem("d/packages.lock.json", "PackagesLock", 1)
        });

        Assert.DoesNotContain(result.Entries, entry => entry.PackageName != "Safe.UnsafeVersion");
        var hashed = Assert.Single(result.Entries);
        Assert.Equal("unknown", Assert.Single(result.Entries, entry => entry.PackageName == "Safe.UnsafeVersion").DependencyRelation);
        Assert.Null(hashed.ResolvedVersion);
        Assert.Equal(32, hashed.ResolvedVersionHash!.Length);
        var categories = result.Gaps.Select(gap => gap.Category).OrderBy(category => category, StringComparer.Ordinal).ToArray();
        Assert.Contains("packages-lock-parse", categories);
        Assert.Contains("packages-lock-unsupported", categories);
        Assert.Contains("packages-lock-entry-unsafe", categories);
        Assert.Contains("packages-lock-entry-resolved-missing", categories);
        var serialized = string.Join('\n', result.Gaps.Select(gap => gap.Message)
            .Concat(result.Entries.SelectMany(entry => (IEnumerable<string>)[entry.PackageName, entry.ResolvedVersion ?? string.Empty, entry.ResolvedVersionHash ?? string.Empty])));
        Assert.DoesNotContain("git+ssh://user:pass@example.invalid", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("../unsafe/name", serialized, StringComparison.Ordinal);
    }

    [Fact]
    public void Scan_emits_lockfile_package_facts_and_preserves_presence_diagnostic()
    {
        using var temp = new TempDirectory();
        var repo = CreateScannedRepo(temp, ValidLockfile);

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));

        var lockfileRows = result.Facts
            .Where(fact => fact.FactType == FactTypes.PackageReferenced
                && fact.Evidence.FilePath == "src/packages.lock.json"
                && fact.Properties.GetValueOrDefault("sourceKind") == "lockfile")
            .ToArray();
        Assert.Equal(3, lockfileRows.Length);
        Assert.All(lockfileRows, fact =>
        {
            Assert.Equal(RuleIds.ProjectFile, fact.RuleId);
            Assert.Equal(EvidenceTiers.Tier2Structural, fact.EvidenceTier);
            Assert.Equal("nuget-lockfile/0.1.0", fact.Evidence.ExtractorVersion);
            Assert.Equal("packages.lock.json", fact.Properties["manifestKind"]);
            Assert.Equal("nuget", fact.Properties["ecosystem"]);
            Assert.True(fact.Properties.ContainsKey("lockfilePath"));
            Assert.Equal(32, fact.Properties["lockfileHash"].Length);
            Assert.True(fact.Properties.ContainsKey("dependencyRelation"));
            Assert.DoesNotContain(fact.Properties, pair => pair.Value.Contains("not-an-artifact-digest", StringComparison.Ordinal));
        });
        var directRow = Assert.Single(lockfileRows, fact => fact.Properties.GetValueOrDefault("dependencyRelation") == "direct" && fact.Properties.GetValueOrDefault("targetFramework") == "net8.0");
        Assert.Equal("1.0.0", directRow.Properties["resolvedVersion"]);
        Assert.Equal("1.0.0", directRow.Properties["version"]);
        Assert.Equal("src/packages.lock.json", directRow.Properties["lockfilePath"]);
        Assert.Equal(5, directRow.Evidence.StartLine);
        Assert.Equal("Example.Direct", directRow.TargetSymbol);
        Assert.Single(lockfileRows, fact => fact.Properties.GetValueOrDefault("dependencyRelation") == "transitive");
        Assert.DoesNotContain(result.Facts, fact => fact.FactType == FactTypes.AnalysisGap && fact.Evidence.FilePath == "src/packages.lock.json");
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.BuildEnvironmentDiagnostic
            && fact.Properties.GetValueOrDefault("diagnosticCode") == "PackagesLockPresent"
            && fact.Properties.GetValueOrDefault("safeObservedValue") == "packages.lock.json"
            && fact.Evidence.FilePath == "src/packages.lock.json");
    }

    [Fact]
    public async Task PackageDecision_correlates_nuget_lockfile_resolved_versions_as_possible_only()
    {
        using var temp = new TempDirectory();
        var repo = CreateScannedRepo(temp, ValidLockfile);
        var scan = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "scan")));
        var indexPath = Path.Combine(temp.Path, "index.sqlite");
        SqliteIndexWriter.Write(indexPath, scan.Manifest, scan.Facts);
        var decisionPath = Path.Combine(temp.Path, "decision.json");
        await File.WriteAllTextAsync(decisionPath, """
            {
              "version": "package-decision.v1",
              "records": [
                {
                  "decisionId": "dec-nuget-lockfile",
                  "decisionKind": "reject",
                  "ecosystem": "nuget",
                  "packageName": "example.direct",
                  "artifactVersion": "1.0.0",
                  "artifactDigestAlgorithm": "sha256",
                  "artifactDigest": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                  "producer": { "id": "sample-producer", "policyVersion": "2026-08" },
                  "decisionTimeUtc": "2026-08-18T00:00:00Z"
                },
                {
                  "decisionId": "dec-nuget-tfm",
                  "decisionKind": "admit",
                  "ecosystem": "nuget",
                  "packageName": "Example.Direct",
                  "artifactVersion": "1.0.1",
                  "producer": { "id": "sample-producer", "policyVersion": "2026-08" },
                  "decisionTimeUtc": "2026-08-18T00:00:00Z"
                }
              ]
            }
            """);

        var result = await PackageDecisionCorrelationReporter.WriteAsync(new PackageDecisionOptions(decisionPath, indexPath, Path.Combine(temp.Path, "report"), ExitCode: true));

        Assert.Empty(result.Report.ExactMatches);
        Assert.Empty(result.Report.DigestMismatches);
        var resolvedRows = result.Report.PossibleMatches.Where(row => row.MatchBasis == "resolved-version").ToArray();
        Assert.Equal(2, resolvedRows.Length);
        Assert.Equal(2, resolvedRows.Count(row => row.DecisionId is "dec-nuget-lockfile" or "dec-nuget-tfm"));
        var netEightRow = Assert.Single(resolvedRows, row => row.DecisionId == "dec-nuget-lockfile");
        var netNineRow = Assert.Single(resolvedRows, row => row.DecisionId == "dec-nuget-tfm");
        Assert.Equal("1.0.0", netEightRow.Evidence.ResolvedVersion);
        Assert.Equal("1.0.1", netNineRow.Evidence.ResolvedVersion);
        Assert.All(resolvedRows, row =>
        {
            Assert.Equal("project.file.v1", row.Evidence.RuleId);
            Assert.Equal(EvidenceTiers.Tier2Structural, row.Evidence.EvidenceTier);
            Assert.Equal("NuGetLockfileExtractor", row.Evidence.ExtractorId);
            Assert.Equal("nuget-lockfile/0.1.0", row.Evidence.ExtractorVersion);
            Assert.Equal("src/packages.lock.json", row.Evidence.LockfilePath);
            Assert.Equal(32, row.Evidence.LockfileHash!.Length);
            Assert.Null(row.Evidence.ArtifactDigest);
            Assert.Equal("direct", row.DependencyRelation);
            Assert.Equal(scan.Manifest.CommitSha, row.CommitSha);
            Assert.Contains(row.Evidence.FactId, scan.Facts.Select(fact => fact.FactId));
        });
        Assert.Contains(result.Report.PossibleMatches, row => row.MatchBasis == "declared-exact" && row.DependencyRelation == "unknown");
        Assert.Contains(result.Report.Gaps, gap => gap.Classification == "LockfileDigestUnavailable" && gap.DecisionId == "dec-nuget-lockfile");
        Assert.False(result.ExitCodeTriggered);
        var json = await File.ReadAllTextAsync(Path.Combine(temp.Path, "report", "package-decision-report.json"));
        Assert.DoesNotContain("not-an-artifact-digest", json, StringComparison.Ordinal);
        Assert.DoesNotContain(temp.Path, json, StringComparison.Ordinal);
        var repeated = await PackageDecisionCorrelationReporter.WriteAsync(new PackageDecisionOptions(decisionPath, indexPath, Path.Combine(temp.Path, "report-repeat"), ExitCode: true));
        Assert.Equal(json, await File.ReadAllTextAsync(Path.Combine(temp.Path, "report-repeat", "package-decision-report.json")));
    }

    private static string CreateScannedRepo(TempDirectory temp, string lockfileContent)
    {
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(Path.Combine(repo, "src"));
        File.WriteAllText(Path.Combine(repo, "src", "App.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net8.0</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Example.Direct" Version="1.0.0" />
              </ItemGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(repo, "src", "packages.lock.json"), lockfileContent);
        RunGit(repo, "init");
        RunGit(repo, "add", ".");
        RunGit(repo, "-c", "user.email=tracemap@example.invalid", "-c", "user.name=TraceMap Test", "commit", "-m", "fixture");
        return repo;
    }

    private static CodeFact SwiftLockfileFact(
        ScanManifest manifest,
        string factId,
        string filePath,
        int line,
        string ruleId,
        string tier,
        IReadOnlyDictionary<string, string> properties)
    {
        return new CodeFact(
            factId,
            manifest.ScanId,
            manifest.RepoName,
            manifest.CommitSha,
            null,
            "SwiftDependencyLockfileEntryDeclared",
            ruleId,
            tier,
            null,
            properties.GetValueOrDefault("normalizedDependencyIdentity"),
            "PackageLockfile",
            new EvidenceSpan(filePath, line, line, null, "SwiftDependencyExtractor", "swift-dependency/0.1.0"),
            new SortedDictionary<string, string>(properties.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal), StringComparer.Ordinal));
    }

    private static void RunGit(string repo, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "git",
            WorkingDirectory = repo,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("git unavailable");
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, process.StandardError.ReadToEnd());
    }
}
