using System.Text.RegularExpressions;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class LegacyDataModelRuleCatalogTests
{
    private static readonly string[] ModelRuleIds =
    [
        RuleIds.LegacyDataModelIdentity,
        RuleIds.LegacyDataModelRelationship,
        RuleIds.LegacyDataOrmNHibernate,
        RuleIds.LegacyDataOrmUnsupported,
        RuleIds.LegacyDataModelGeneratedLink,
        RuleIds.LegacyDataModelSurface
    ];

    private static readonly string[] EvidenceTiers =
    [
        "Tier1Semantic",
        "Tier2Structural",
        "Tier3SyntaxOrTextual",
        "Tier4Unknown"
    ];

    [Fact]
    public void Legacy_data_model_rule_constants_match_spec_ids()
    {
        Assert.Equal("legacy.data.model.identity.v1", RuleIds.LegacyDataModelIdentity);
        Assert.Equal("legacy.data.model.relationship.v1", RuleIds.LegacyDataModelRelationship);
        Assert.Equal("legacy.data.orm.nhibernate.v1", RuleIds.LegacyDataOrmNHibernate);
        Assert.Equal("legacy.data.orm.unsupported.v1", RuleIds.LegacyDataOrmUnsupported);
        Assert.Equal("legacy.data.model.generated-link.v1", RuleIds.LegacyDataModelGeneratedLink);
        Assert.Equal("legacy.data.model.surface.v1", RuleIds.LegacyDataModelSurface);
    }

    [Fact]
    public void Rule_catalog_documents_legacy_data_model_rule_contracts()
    {
        var catalog = File.ReadAllText(Path.Combine(FindRepoRoot(), "rules", "rule-catalog.yml"));

        foreach (var ruleId in ModelRuleIds)
        {
            var block = RuleBlock(catalog, ruleId);
            Assert.Contains("limitations:", block, StringComparison.Ordinal);
            Assert.Contains("AnalysisGap", block, StringComparison.Ordinal);
            Assert.Contains(EvidenceTier(block), EvidenceTiers);
        }

        Assert.Contains(FactTypes.LegacyDataMetadataDeclared, RuleBlock(catalog, RuleIds.LegacyDataOrmNHibernate), StringComparison.Ordinal);
        Assert.Contains(FactTypes.LegacyDataMappingDeclared, RuleBlock(catalog, RuleIds.LegacyDataOrmNHibernate), StringComparison.Ordinal);
        Assert.Contains(FactTypes.LegacyDataGeneratedCodeLinked, RuleBlock(catalog, RuleIds.LegacyDataModelGeneratedLink), StringComparison.Ordinal);
    }

    [Fact]
    public void Surface_rule_documents_projection_without_scan_time_surface_fact()
    {
        var catalog = File.ReadAllText(Path.Combine(FindRepoRoot(), "rules", "rule-catalog.yml"));
        var block = RuleBlock(catalog, RuleIds.LegacyDataModelSurface);

        Assert.Contains("surfaceSubtype data-model", block, StringComparison.Ordinal);
        Assert.Contains("legacy-data surface kind", block, StringComparison.Ordinal);
        Assert.Contains("AnalysisGap facts under legacy.data.* rules are excluded", block, StringComparison.Ordinal);
        Assert.DoesNotContain("      - LegacyDataModelSurfaceProjected", block, StringComparison.Ordinal);
    }

    private static string RuleBlock(string catalog, string ruleId)
    {
        var startMatch = Regex.Match(catalog, $@"(?m)^\s*-\s*id:\s*{Regex.Escape(ruleId)}\s*$");
        Assert.True(startMatch.Success, $"Missing rule catalog entry for {ruleId}.");

        var afterStart = startMatch.Index + startMatch.Length;
        var nextMatch = Regex.Match(catalog[afterStart..], @"(?m)^\s*-\s*id:\s*\S+\s*$");
        return nextMatch.Success
            ? catalog[startMatch.Index..(afterStart + nextMatch.Index)]
            : catalog[startMatch.Index..];
    }

    private static string EvidenceTier(string ruleBlock)
    {
        var match = Regex.Match(ruleBlock, @"(?m)^\s*evidenceTier:\s*(\S+)\s*$");
        Assert.True(match.Success, "Rule catalog entry is missing a single evidenceTier value.");
        return match.Groups[1].Value;
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "rules", "rule-catalog.yml")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
