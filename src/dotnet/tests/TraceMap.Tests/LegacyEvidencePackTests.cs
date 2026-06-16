using System.Text.Json;
using TraceMap.Reporting;

namespace TraceMap.Tests;

public class LegacyEvidencePackTests
{
    [Fact]
    public async Task CreateAsync_WritesDeterministicPublicSafePackFromSyntheticSummary()
    {
        var outDir = TempEvidenceOutDir("synthetic");
        var result = await LegacyEvidencePacks.CreateAsync(new LegacyEvidencePackCreateOptions(
            "samples/synthetic-legacy-evidence-pack",
            "legacy-validation-summary",
            "synthetic-legacy-alpha",
            "legacy-validation-proof",
            LegacyEvidencePackClaimLevels.PublicSafe,
            outDir,
            "2026-06"));

        Assert.True(result.Validation.IsValid);
        Assert.NotNull(result.JsonPath);
        Assert.NotNull(result.MarkdownPath);
        Assert.NotNull(result.ValidationPath);
        Assert.Equal("public-safe", result.Pack.ClaimLevel);
        Assert.Equal(8, result.Pack.Summary.FactCount);
        Assert.Contains(result.Pack.EvidenceSections, section => section.SectionId == "legacy-ui");
        Assert.Contains(result.Pack.EvidenceSections.SelectMany(section => section.Rows), row => row.RuleId == "legacy.webforms.event-binding.v1");

        var firstJson = await File.ReadAllTextAsync(result.JsonPath!);
        var firstMarkdown = await File.ReadAllTextAsync(result.MarkdownPath!);
        Assert.DoesNotContain("/home/", firstJson, StringComparison.Ordinal);
        Assert.DoesNotContain("http://", firstJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SELECT ", firstJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("runtime proof", firstMarkdown, StringComparison.OrdinalIgnoreCase);

        var secondDir = TempEvidenceOutDir("synthetic-second");
        var second = await LegacyEvidencePacks.CreateAsync(new LegacyEvidencePackCreateOptions(
            "samples/synthetic-legacy-evidence-pack",
            "legacy-validation-summary",
            "synthetic-legacy-alpha",
            "legacy-validation-proof",
            LegacyEvidencePackClaimLevels.PublicSafe,
            secondDir,
            "2026-06"));

        Assert.Equal(firstJson, await File.ReadAllTextAsync(second.JsonPath!));
        Assert.Equal(firstMarkdown, await File.ReadAllTextAsync(second.MarkdownPath!));
    }

    [Fact]
    public async Task CreateAsync_RequiresDateForPublicSafePacks()
    {
        var outDir = TempEvidenceOutDir("missing-date");

        var error = await Assert.ThrowsAsync<ArgumentException>(() => LegacyEvidencePacks.CreateAsync(new LegacyEvidencePackCreateOptions(
            "samples/synthetic-legacy-evidence-pack",
            "legacy-validation-summary",
            "synthetic-legacy-alpha",
            "legacy-validation-proof",
            LegacyEvidencePackClaimLevels.PublicSafe,
            outDir)));

        Assert.Contains("require --date", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidateAsync_RejectsUnsafePackContentWithoutEchoingValue()
    {
        using var temp = new TempDirectory();
        var packPath = Path.Combine(temp.Path, "evidence-pack.json");
        var markdownPath = Path.Combine(temp.Path, "evidence-pack.md");
        await File.WriteAllTextAsync(packPath, """
            {
              "schemaVersion": "legacy-evidence-pack.v1",
              "packId": "unsafe-pack",
              "generatedFor": "legacy-validation-proof",
              "claimLevel": "public-safe",
              "date": "2026-06",
              "commandProvenance": {
                "packGeneratorVersion": "legacy-evidence-pack.v1",
                "normalizedCommand": "tracemap evidence-pack create",
                "inputKind": "legacy-validation-summary",
                "inputFingerprint": "abc",
                "validationCommands": ["tracemap evidence-pack validate"]
              },
              "sources": [],
              "summary": {
                "ruleId": "legacy.evidence-pack.summary.v1",
                "evidenceTier": "Tier4Unknown",
                "sourceLabel": "synthetic-legacy-alpha",
                "coverageLabel": "unknown",
                "factCount": 0,
                "gapCount": 1,
                "byRuleId": {},
                "byEvidenceTier": {},
                "byFactCategory": {},
                "limitations": []
              },
              "evidenceSections": [],
              "gaps": [],
              "limitations": ["/home/example/private"],
              "safety": {
                "validatorVersion": "legacy-evidence-pack-safety.v1",
                "classification": "public-safe",
                "rejectedCategories": [],
                "limitations": []
              }
            }
            """);
        await File.WriteAllTextAsync(markdownPath, "# Evidence\n");

        var result = await LegacyEvidencePacks.ValidateAsync(new LegacyEvidencePackValidateOptions(packPath, LegacyEvidencePackClaimLevels.PublicSafe));

        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Category == "absolute-path");
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Message.Contains("/home/", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ValidateAsync_RequiresEvidenceRowsToCarryMetadata()
    {
        var create = await LegacyEvidencePacks.CreateAsync(new LegacyEvidencePackCreateOptions(
            "samples/synthetic-legacy-evidence-pack",
            "legacy-validation-summary",
            "synthetic-legacy-alpha",
            "legacy-validation-proof",
            LegacyEvidencePackClaimLevels.PublicSafe,
            TempEvidenceOutDir("valid"),
            "2026-06"));

        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(create.JsonPath!));
        foreach (var row in document.RootElement.GetProperty("evidenceSections").EnumerateArray().SelectMany(section => section.GetProperty("rows").EnumerateArray()))
        {
            Assert.False(string.IsNullOrWhiteSpace(row.GetProperty("ruleId").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(row.GetProperty("evidenceTier").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(row.GetProperty("sourceLabel").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(row.GetProperty("coverageLabel").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(row.GetProperty("safeProvenance").GetString()));
            Assert.NotEmpty(row.GetProperty("limitations").EnumerateArray());
        }
    }

    [Fact]
    public async Task PromoteAsync_CopiesOnlyValidatedPublicSafePackUnderApprovedRoot()
    {
        var outDir = TempEvidenceOutDir("promote");
        var create = await LegacyEvidencePacks.CreateAsync(new LegacyEvidencePackCreateOptions(
            "samples/synthetic-legacy-evidence-pack",
            "legacy-validation-summary",
            "synthetic-legacy-alpha",
            "legacy-validation-proof",
            LegacyEvidencePackClaimLevels.PublicSafe,
            outDir,
            "2026-06"));
        var repoRoot = FindRepoRoot(Directory.GetCurrentDirectory());
        var promoteDir = Path.Combine(repoRoot, "docs", "evidence-packs", "legacy", "test-pack-" + Guid.NewGuid().ToString("N"));

        try
        {
            var promote = await LegacyEvidencePacks.PromoteAsync(new LegacyEvidencePackPromoteOptions(
                create.JsonPath!,
                create.MarkdownPath!,
                promoteDir));

            Assert.True(promote.Validation.IsValid);
            Assert.True(File.Exists(Path.Combine(promoteDir, LegacyEvidencePacks.PackJsonFileName)));
            Assert.True(File.Exists(Path.Combine(promoteDir, LegacyEvidencePacks.PackMarkdownFileName)));
        }
        finally
        {
            if (Directory.Exists(promoteDir))
            {
                Directory.Delete(promoteDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task PromoteAsync_RejectsDestinationsOutsideApprovedRoot()
    {
        using var temp = new TempDirectory();
        var outDir = TempEvidenceOutDir("promote-outside");
        var create = await LegacyEvidencePacks.CreateAsync(new LegacyEvidencePackCreateOptions(
            "samples/synthetic-legacy-evidence-pack",
            "legacy-validation-summary",
            "synthetic-legacy-alpha",
            "legacy-validation-proof",
            LegacyEvidencePackClaimLevels.PublicSafe,
            outDir,
            "2026-06"));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => LegacyEvidencePacks.PromoteAsync(new LegacyEvidencePackPromoteOptions(
            create.JsonPath!,
            create.MarkdownPath!,
            Path.Combine(temp.Path, "outside"))));

        Assert.Contains("docs/evidence-packs/legacy", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateAsync_RejectsUnapprovedOutputWithoutWritingFiles()
    {
        var repoRoot = FindRepoRoot(Directory.GetCurrentDirectory());
        var outDir = Path.Combine(repoRoot, "docs", "evidence-packs", "legacy", "create-bypass-" + Guid.NewGuid().ToString("N"));

        var result = await LegacyEvidencePacks.CreateAsync(new LegacyEvidencePackCreateOptions(
            "samples/synthetic-legacy-evidence-pack",
            "legacy-validation-summary",
            "synthetic-legacy-alpha",
            "legacy-validation-proof",
            LegacyEvidencePackClaimLevels.PublicSafe,
            outDir,
            "2026-06"));

        Assert.False(result.Validation.IsValid);
        Assert.Contains(result.Validation.Diagnostics, diagnostic => diagnostic.Category == "output-storage");
        Assert.False(Directory.Exists(outDir));
    }

    [Fact]
    public async Task ValidateAsync_ReturnsDiagnosticForInvalidClaimLevel()
    {
        using var temp = new TempDirectory();
        var packPath = Path.Combine(temp.Path, "evidence-pack.json");
        await File.WriteAllTextAsync(packPath, """
            {
              "schemaVersion": "legacy-evidence-pack.v1",
              "packId": "bad-claim",
              "generatedFor": "legacy-validation-proof",
              "claimLevel": "maybe",
              "date": "2026-06",
              "commandProvenance": {
                "packGeneratorVersion": "legacy-evidence-pack.v1",
                "normalizedCommand": "tracemap evidence-pack create",
                "inputKind": "legacy-validation-summary",
                "inputFingerprint": "abc",
                "validationCommands": []
              },
              "sources": [],
              "summary": {
                "ruleId": "legacy.evidence-pack.summary.v1",
                "evidenceTier": "Tier4Unknown",
                "sourceLabel": "synthetic-legacy-alpha",
                "coverageLabel": "unknown",
                "factCount": 0,
                "gapCount": 0,
                "byRuleId": {},
                "byEvidenceTier": {},
                "byFactCategory": {},
                "limitations": []
              },
              "evidenceSections": [],
              "gaps": [],
              "limitations": [],
              "safety": {
                "validatorVersion": "legacy-evidence-pack-safety.v1",
                "classification": "maybe",
                "rejectedCategories": [],
                "limitations": []
              }
            }
            """);

        var result = await LegacyEvidencePacks.ValidateAsync(new LegacyEvidencePackValidateOptions(packPath));

        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Category == "claim-level-invalid");
    }

    [Fact]
    public async Task ValidateAsync_RejectsMarkdownThatDoesNotMatchJson()
    {
        var outDir = TempEvidenceOutDir("markdown-mismatch");
        var create = await LegacyEvidencePacks.CreateAsync(new LegacyEvidencePackCreateOptions(
            "samples/synthetic-legacy-evidence-pack",
            "legacy-validation-summary",
            "synthetic-legacy-alpha",
            "legacy-validation-proof",
            LegacyEvidencePackClaimLevels.PublicSafe,
            outDir,
            "2026-06"));
        await File.WriteAllTextAsync(create.MarkdownPath!, "# Different safe markdown\n");

        var result = await LegacyEvidencePacks.ValidateAsync(new LegacyEvidencePackValidateOptions(create.JsonPath!, LegacyEvidencePackClaimLevels.PublicSafe));

        Assert.False(result.IsValid);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Category == "markdown-mismatch");
    }

    [Fact]
    public async Task PromoteAsync_RejectsApprovedRootItselfEvenWithForce()
    {
        var outDir = TempEvidenceOutDir("root-promote");
        var create = await LegacyEvidencePacks.CreateAsync(new LegacyEvidencePackCreateOptions(
            "samples/synthetic-legacy-evidence-pack",
            "legacy-validation-summary",
            "synthetic-legacy-alpha",
            "legacy-validation-proof",
            LegacyEvidencePackClaimLevels.PublicSafe,
            outDir,
            "2026-06"));
        var repoRoot = FindRepoRoot(Directory.GetCurrentDirectory());

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => LegacyEvidencePacks.PromoteAsync(new LegacyEvidencePackPromoteOptions(
            create.JsonPath!,
            create.MarkdownPath!,
            Path.Combine(repoRoot, "docs", "evidence-packs", "legacy"),
            Force: true)));

        Assert.Contains("child directory", error.Message, StringComparison.Ordinal);
    }

    private static string FindRepoRoot(string start)
    {
        var directory = new DirectoryInfo(Path.GetFullPath(start));
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".git")) || File.Exists(Path.Combine(directory.FullName, ".git")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return Path.GetFullPath(start);
    }

    private static string TempEvidenceOutDir(string suffix)
    {
        var repoRoot = FindRepoRoot(Directory.GetCurrentDirectory());
        var path = Path.Combine(repoRoot, ".tmp", "legacy-evidence-packs", suffix + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
