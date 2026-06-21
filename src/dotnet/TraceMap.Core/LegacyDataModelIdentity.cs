using System.Text;

namespace TraceMap.Core;

internal readonly record struct LegacyDataModelIdentityDescriptor(
    string MetadataFormat,
    string ModelKind,
    string DescriptorRole,
    string RelativePath,
    string Scope,
    string? DisplayName,
    string? ContainerName,
    IReadOnlyDictionary<string, string>? IdentityParts = null,
    string? SourceMetadataFactId = null,
    string CoverageLabel = "full");

internal static class LegacyDataModelIdentity
{
    public static void Apply(SortedDictionary<string, string> properties, LegacyDataModelIdentityDescriptor descriptor)
    {
        var metadataFormat = NormalizeToken(descriptor.MetadataFormat, "unknown");
        var modelKind = NormalizeToken(descriptor.ModelKind, "unknown");
        var descriptorRole = NormalizeToken(descriptor.DescriptorRole, "unknown");
        var relativePath = NormalizePath(descriptor.RelativePath);
        var scope = string.IsNullOrWhiteSpace(descriptor.Scope) ? "document" : descriptor.Scope.Trim();

        properties["metadataFormat"] = metadataFormat;
        properties["modelKind"] = modelKind;
        properties["descriptorRole"] = descriptorRole;
        properties["coverageLabel"] = NormalizeCoverageLabel(descriptor.CoverageLabel);
        properties["modelIdentityRuleId"] = RuleIds.LegacyDataModelIdentity;
        properties["modelIdentityEvidenceTier"] = EvidenceTiers.Tier2Structural;

        LegacyDataSafeValues.AddSafeOrHash(properties, "displayName", "displayNameHash", descriptor.DisplayName, "hashed-unsafe-identifier");
        LegacyDataSafeValues.AddSafeOrHash(properties, "containerName", "containerHash", descriptor.ContainerName, "hashed-unsafe-identifier");

        if (!string.IsNullOrWhiteSpace(descriptor.SourceMetadataFactId))
        {
            properties["sourceMetadataFactId"] = descriptor.SourceMetadataFactId.Trim();
        }

        properties["stableModelKey"] = "ldm:" + FactFactory.Hash(StableKeySeed(metadataFormat, modelKind, descriptorRole, relativePath, scope, descriptor), 32);
    }

    public static string MetadataFormat(string metadataKind)
    {
        return metadataKind switch
        {
            "Dbml" => "dbml",
            "Edmx" => "edmx",
            "TypedDataSet" => "typed-dataset",
            "TableAdapter" => "tableadapter",
            "GeneratedDesigner" => "generated-code",
            "Config" => "config",
            "NHibernateHbm" => "nhibernate-hbm",
            _ => NormalizeToken(metadataKind, "unknown")
        };
    }

    private static string StableKeySeed(
        string metadataFormat,
        string modelKind,
        string descriptorRole,
        string relativePath,
        string scope,
        LegacyDataModelIdentityDescriptor descriptor)
    {
        var builder = new StringBuilder()
            .Append(metadataFormat).Append('|')
            .Append(modelKind).Append('|')
            .Append(descriptorRole).Append('|')
            .Append(relativePath).Append('|')
            .Append(scope).Append('|')
            .Append(LegacyDataSafeValues.Identity("display", descriptor.DisplayName)).Append('|')
            .Append(LegacyDataSafeValues.Identity("container", descriptor.ContainerName));

        foreach (var (key, value) in (descriptor.IdentityParts ?? new Dictionary<string, string>())
            .OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            builder.Append('|')
                .Append(NormalizeToken(key, "part"))
                .Append('=')
                .Append(LegacyDataSafeValues.Identity(key, value));
        }

        return builder.ToString();
    }

    private static string NormalizePath(string relativePath)
    {
        return relativePath.Replace('\\', '/').Trim();
    }

    private static string NormalizeCoverageLabel(string coverageLabel)
    {
        return string.Equals(coverageLabel, "reduced", StringComparison.OrdinalIgnoreCase) ? "reduced" : "full";
    }

    private static string NormalizeToken(string value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var trimmed = value.Trim();
        var builder = new StringBuilder(trimmed.Length);
        foreach (var ch in trimmed)
        {
            builder.Append(char.IsLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '-');
        }

        var normalized = builder.ToString().Trim('-');
        while (normalized.Contains("--", StringComparison.Ordinal))
        {
            normalized = normalized.Replace("--", "-", StringComparison.Ordinal);
        }

        return normalized.Length == 0 ? fallback : normalized;
    }
}
