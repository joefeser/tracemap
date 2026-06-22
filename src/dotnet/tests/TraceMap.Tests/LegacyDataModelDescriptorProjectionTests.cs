using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class LegacyDataModelDescriptorProjectionTests
{
    [Fact]
    public void Legacy_data_analysis_gaps_are_not_terminal_descriptor_surfaces()
    {
        var dbmlGap = SurfaceInput(
            FactTypes.AnalysisGap,
            RuleIds.LegacyDataDbml,
            EvidenceTiers.Tier4Unknown,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["classification"] = "MalformedLegacyDataMetadata",
                ["metadataFormat"] = "dbml"
            });
        var nhibernateGap = SurfaceInput(
            FactTypes.AnalysisGap,
            RuleIds.LegacyDataOrmNHibernate,
            EvidenceTiers.Tier4Unknown,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["classification"] = "UnsupportedLegacyOrmMappingShape",
                ["metadataFormat"] = "nhibernate-hbm"
            });
        var validDescriptor = SurfaceInput(
            FactTypes.LegacyDataEntityDeclared,
            RuleIds.LegacyDataOrmNHibernate,
            EvidenceTiers.Tier2Structural,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["descriptorRole"] = "conceptual",
                ["displayName"] = "Customer",
                ["metadataFormat"] = "nhibernate-hbm",
                ["modelKind"] = "entity",
                ["stableModelKey"] = "ldm:nhibernate-hbm:customer"
            });

        Assert.False(LegacyDataModelDescriptorProjection.IsTerminalLegacyDataDescriptor(dbmlGap));
        Assert.False(LegacyDataModelDescriptorProjection.IsTerminalLegacyDataDescriptor(nhibernateGap));
        Assert.Empty(LegacyDataModelDescriptorProjection.BuildDescriptors([dbmlGap, nhibernateGap]));
        Assert.True(LegacyDataModelDescriptorProjection.IsTerminalLegacyDataDescriptor(validDescriptor));
        Assert.Single(LegacyDataModelDescriptorProjection.BuildDescriptors([validDescriptor]));
    }

    private static CombinedSurfaceFactInput SurfaceInput(
        string factType,
        string ruleId,
        string evidenceTier,
        IReadOnlyDictionary<string, string> properties)
    {
        return new CombinedSurfaceFactInput(
            "combined-1",
            "source-1",
            "api",
            "fact-1",
            "scan-1",
            "0123456789abcdef0123456789abcdef01234567",
            factType,
            ruleId,
            evidenceTier,
            "Mappings/Customer.hbm.xml",
            10,
            10,
            properties);
    }
}
