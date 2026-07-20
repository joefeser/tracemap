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

        Assert.Contains("Ambiguous, duplicate, unsupported, reduced-coverage, malformed, parser-rejected, or too-large metadata", RuleBlock(catalog, RuleIds.LegacyDataModelIdentity), StringComparison.Ordinal);
        Assert.Contains("unidirectional, ambiguous, duplicate, inherited, split, conditional, many-to-many, provider-specific, or unsupported shapes", RuleBlock(catalog, RuleIds.LegacyDataModelRelationship), StringComparison.Ordinal);
        Assert.Contains("Unsupported descriptors produce gaps, not invented entity, table, column, relationship, or generated-code facts", RuleBlock(catalog, RuleIds.LegacyDataOrmUnsupported), StringComparison.Ordinal);
        Assert.Contains("AmbiguousLegacyDataModelSelector gaps", RuleBlock(catalog, RuleIds.LegacyDataModelSurface), StringComparison.Ordinal);
        Assert.Contains("DuplicateIdentity gaps with reason duplicate-surface", RuleBlock(catalog, RuleIds.LegacyDataModelSurface), StringComparison.Ordinal);
        Assert.Contains("Unknown vocabulary values for descriptor role, metadata format, or source artifact type", RuleBlock(catalog, RuleIds.LegacyDataModelSurface), StringComparison.Ordinal);
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

    [Fact]
    public void Relationship_rule_catalogs_closed_classifier_vocabulary_and_existing_gap_ownership()
    {
        var catalog = File.ReadAllText(Path.Combine(FindRepoRoot(), "rules", "rule-catalog.yml"));
        var relationship = RuleBlock(catalog, RuleIds.LegacyDataModelRelationship);

        AssertCatalogList(relationship, "safeReasonCodes",
        [
            "deterministic-relationship",
            "missing-endpoint",
            "duplicate-relationship-identity",
            "ambiguous-endpoint-candidates",
            "unsupported-relationship-shape",
            "reduced-parser-coverage",
            "unsafe-redacted-endpoint-identity",
            "not-in-scope"
        ]);
        AssertCatalogList(relationship, "gapClassifications",
        [
            "AmbiguousLegacyDataModelIdentity",
            "IncompleteLegacyDataModelRelationship",
            "ReducedLegacyDataModelRelationshipCoverage",
            "UnsupportedLegacyOrmDescriptor",
            "UnsupportedLegacyOrmMappingShape"
        ]);
        AssertCatalogList(relationship, "limitationCodes",
        [
            "ambiguous-constraint-name",
            "constraint-endpoint-needs-review",
            "duplicate-relationship-name",
            "inherited-endpoint-needs-review",
            "missing-endpoint",
            "missing-endpoint-type",
            "missing-relationship-endpoint",
            "missing-source-endpoint",
            "missing-target-endpoint",
            "duplicate-relationship-identity",
            "ambiguous-endpoint-candidates",
            "unsupported-relationship-shape",
            "reduced-parser-coverage",
            "unsafe-redacted-endpoint-identity"
        ]);
        Assert.Contains("      - descriptorOrdinal", relationship, StringComparison.Ordinal);
        Assert.Contains("AmbiguousLegacyDataModelIdentity", RuleBlock(catalog, RuleIds.LegacyDataDbml), StringComparison.Ordinal);
        Assert.Contains("UnsupportedLegacyOrmMappingShape", RuleBlock(catalog, RuleIds.LegacyDataDbml), StringComparison.Ordinal);
        Assert.Contains("AmbiguousLegacyDataModelIdentity", RuleBlock(catalog, RuleIds.LegacyDataEdmx), StringComparison.Ordinal);
        Assert.Contains("AmbiguousLegacyDataModelIdentity", RuleBlock(catalog, RuleIds.LegacyDataTypedDataSet), StringComparison.Ordinal);
        Assert.Contains("AmbiguousLegacyDataModelIdentity", RuleBlock(catalog, RuleIds.LegacyDataOrmNHibernate), StringComparison.Ordinal);
        Assert.Contains("UnsupportedLegacyOrmMappingShape", RuleBlock(catalog, RuleIds.LegacyDataOrmNHibernate), StringComparison.Ordinal);
        Assert.Contains("UnsupportedLegacyOrmDescriptor", RuleBlock(catalog, RuleIds.LegacyDataOrmUnsupported), StringComparison.Ordinal);
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

    private static void AssertCatalogList(string ruleBlock, string key, IReadOnlyList<string> expected)
    {
        var match = Regex.Match(
            ruleBlock,
            $@"(?ms)^\s*{Regex.Escape(key)}:\s*\n(?<items>(?:\s+-\s+[^\r\n]+\r?\n?)+)");
        Assert.True(match.Success, $"Rule catalog entry is missing {key}.");
        var actual = Regex.Matches(match.Groups["items"].Value, @"(?m)^\s+-\s+(\S+)\s*$")
            .Select(item => item.Groups[1].Value)
            .ToArray();
        Assert.Equal(expected, actual);
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
