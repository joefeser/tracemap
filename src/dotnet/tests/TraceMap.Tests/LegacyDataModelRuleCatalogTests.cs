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
            Assert.Contains("static", block, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("prove", block, StringComparison.OrdinalIgnoreCase);
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
        var start = catalog.IndexOf($"  - id: {ruleId}", StringComparison.Ordinal);
        Assert.True(start >= 0, $"Missing rule catalog entry for {ruleId}.");
        var next = catalog.IndexOf("\n  - id: ", start + 1, StringComparison.Ordinal);
        return next < 0 ? catalog[start..] : catalog[start..next];
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
