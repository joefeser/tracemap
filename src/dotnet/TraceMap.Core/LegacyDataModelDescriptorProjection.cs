using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace TraceMap.Core;

public sealed record LegacyDataModelDescriptorProjectionOptions(
    bool AllowClearDisplayLabels = false,
    string? ClaimLevelContextId = null);

public sealed record LegacyDataModelDescriptorProjectionRow(
    string DescriptorId,
    string SurfaceKind,
    string DisplayName,
    string SourceIndexId,
    string SourceLabel,
    string ScanId,
    string CommitSha,
    string CombinedFactId,
    string OriginalFactId,
    string FactType,
    string SourceRuleId,
    string EvidenceTier,
    string? ExtractorVersion,
    string FilePath,
    int StartLine,
    int EndLine,
    string MetadataFormat,
    string SourceArtifactType,
    string ModelKind,
    string DescriptorRole,
    string? StableModelKey,
    string? DisplayNameHash,
    string? ContainerName,
    string? ContainerHash,
    string? StorageKind,
    string? MappingKind,
    string? ModelRelationshipKind,
    string? SourceMetadataFactId,
    IReadOnlyList<string> SupportingFactIds,
    IReadOnlyList<string> SupportingEdgeIds,
    string CoverageLabel,
    IReadOnlyList<string> Limitations,
    IReadOnlyList<string> Redactions,
    bool DisplayClearance,
    string? ClaimLevelContextId);

public static partial class LegacyDataModelDescriptorProjection
{
    private const string ProjectionSeedVersion = "legacy-data-model-reporting/v1";

    public static IReadOnlyList<LegacyDataModelDescriptorProjectionRow> BuildDescriptors(
        IReadOnlyList<CombinedSurfaceFactInput> facts,
        LegacyDataModelDescriptorProjectionOptions? options = null)
    {
        var rows = facts
            .Select(fact => TryProject(fact, options))
            .OfType<LegacyDataModelDescriptorProjectionRow>()
            .ToArray();
        var duplicateIds = rows
            .GroupBy(DuplicateIdentityKey, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .SelectMany(group => group.Select(row => row.DescriptorId))
            .ToHashSet(StringComparer.Ordinal);

        return rows
            .Select(row => duplicateIds.Contains(row.DescriptorId)
                ? row with { Limitations = Append(row.Limitations, "duplicate-stable-identity") }
                : row)
            .OrderBy(row => row.DescriptorRole, StringComparer.Ordinal)
            .ThenBy(row => row.MetadataFormat, StringComparer.Ordinal)
            .ThenBy(row => row.DisplayName, StringComparer.Ordinal)
            .ThenBy(row => row.DescriptorId, StringComparer.Ordinal)
            .ToArray();
    }

    public static LegacyDataModelDescriptorProjectionRow? TryProject(
        CombinedSurfaceFactInput fact,
        LegacyDataModelDescriptorProjectionOptions? options = null)
    {
        options ??= new LegacyDataModelDescriptorProjectionOptions();
        if (!IsTerminalLegacyDataDescriptor(fact))
        {
            return null;
        }

        var metadataFormat = ClosedToken(FirstValue(fact.Properties, "metadataFormat") ?? MetadataFormatFromKind(FirstValue(fact.Properties, "metadataKind")), "unknown");
        var sourceArtifactType = SourceArtifactType(
            metadataFormat,
            FirstValue(fact.Properties, "descriptorKind"),
            FirstValue(fact.Properties, "sourceSection"));
        var modelKind = ClosedToken(FirstValue(fact.Properties, "modelKind", "descriptorModelKind") ?? ModelKindFromFactType(fact.FactType), "unknown");
        var descriptorRole = ClosedToken(FirstValue(fact.Properties, "descriptorRole", "descriptorKind") ?? modelKind, "unknown");
        var stableModelKey = FirstValue(fact.Properties, "stableModelKey");
        var displayNameHash = FirstValue(fact.Properties, "displayNameHash")
            ?? FirstHashValue(fact.Properties)
            ?? Hash(FirstValue(fact.Properties, "displayName", "entityName", "typeName", "storageObjectName", "tableName", "columnName", "connectionName", "providerInvariantName", "metadataHash") ?? fact.CombinedFactId, 32);
        var label = SelectDisplayLabel(fact, options, displayNameHash, out var displayClearance, out var redactions);
        var supportingFactIds = Split(FirstValue(fact.Properties, "supportingFactIds"))
            .Concat(Split(FirstValue(fact.Properties, "sourceMetadataFactId")))
            .Append(fact.CombinedFactId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var supportingEdgeIds = Split(FirstValue(fact.Properties, "supportingEdgeIds"))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var coverageLabel = CoverageLabel(FirstValue(fact.Properties, "coverageLabel"));
        var limitations = Split(FirstValue(fact.Properties, "limitations"))
            .Append(displayClearance ? null : "descriptor-display-hash-only-without-claim-context")
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var descriptorId = DescriptorId(
            fact,
            metadataFormat,
            sourceArtifactType,
            modelKind,
            descriptorRole,
            stableModelKey,
            displayNameHash,
            supportingFactIds);

        return new LegacyDataModelDescriptorProjectionRow(
            descriptorId,
            "legacy-data",
            label,
            fact.SourceIndexId,
            fact.SourceLabel,
            fact.ScanId,
            fact.CommitSha,
            fact.CombinedFactId,
            fact.OriginalFactId,
            fact.FactType,
            fact.RuleId,
            fact.EvidenceTier,
            fact.ExtractorVersion,
            SafePath(fact.FilePath),
            fact.StartLine,
            fact.EndLine,
            metadataFormat,
            sourceArtifactType,
            modelKind,
            descriptorRole,
            stableModelKey,
            displayNameHash,
            options.AllowClearDisplayLabels ? FirstSafeValue(fact.Properties, "containerName") : null,
            FirstValue(fact.Properties, "containerHash"),
            FirstValue(fact.Properties, "storageKind"),
            FirstValue(fact.Properties, "mappingKind"),
            FirstValue(fact.Properties, "modelRelationshipKind"),
            FirstValue(fact.Properties, "sourceMetadataFactId"),
            supportingFactIds,
            supportingEdgeIds,
            coverageLabel,
            limitations,
            redactions,
            displayClearance,
            options.ClaimLevelContextId);
    }

    public static bool IsTerminalLegacyDataDescriptor(CombinedSurfaceFactInput fact)
    {
        return IsLegacyDataEvidence(fact)
            && fact.FactType.StartsWith("LegacyData", StringComparison.Ordinal)
            && fact.FactType != FactTypes.AnalysisGap
            && fact.FactType != FactTypes.LegacyDataGeneratedCodeLinked
            && !string.Equals(ClosedToken(FirstValue(fact.Properties, "descriptorRole", "descriptorKind") ?? string.Empty, string.Empty), "analysis-gap", StringComparison.Ordinal)
            && !string.Equals(FirstValue(fact.Properties, "classification"), "AnalysisGap", StringComparison.Ordinal);
    }

    public static bool IsLegacyDataEvidence(CombinedSurfaceFactInput fact)
    {
        return fact.FactType.StartsWith("LegacyData", StringComparison.Ordinal)
            || fact.RuleId.StartsWith("legacy.data.", StringComparison.Ordinal);
    }

    public static string? FirstValue(IReadOnlyDictionary<string, string> properties, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (properties.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    private static string SelectDisplayLabel(
        CombinedSurfaceFactInput fact,
        LegacyDataModelDescriptorProjectionOptions options,
        string displayNameHash,
        out bool displayClearance,
        out IReadOnlyList<string> redactions)
    {
        var safeLabel = options.AllowClearDisplayLabels ? FirstSafeValue(
            fact.Properties,
            "displayName",
            "entityName",
            "typeName",
            "storageObjectName",
            "tableName",
            "columnName",
            "connectionName",
            "providerInvariantName") : null;
        displayClearance = safeLabel is not null;
        var values = new SortedSet<string>(StringComparer.Ordinal);
        if (!displayClearance)
        {
            values.Add("display-name-hidden-without-claim-context");
        }

        foreach (var key in fact.Properties.Keys.Where(key => key.EndsWith("Redaction", StringComparison.Ordinal)).OrderBy(key => key, StringComparer.Ordinal))
        {
            values.Add($"{key}:{fact.Properties[key]}");
        }

        redactions = values.ToArray();
        if (safeLabel is not null)
        {
            return $"{DescriptorPrefix(fact)}:{safeLabel}";
        }

        if (!string.IsNullOrWhiteSpace(displayNameHash))
        {
            return $"{DescriptorPrefix(fact)}:hash:{displayNameHash[..Math.Min(16, displayNameHash.Length)]}";
        }

        var stableModelKey = FirstValue(fact.Properties, "stableModelKey");
        if (!string.IsNullOrWhiteSpace(stableModelKey))
        {
            return $"{DescriptorPrefix(fact)}:key:{Hash(stableModelKey, 16)}";
        }

        return $"{DescriptorPrefix(fact)}:unknown:{Hash(fact.CombinedFactId, 16)}";
    }

    private static string DescriptorPrefix(CombinedSurfaceFactInput fact)
    {
        return ClosedToken(FirstValue(fact.Properties, "modelKind", "descriptorKind") ?? ModelKindFromFactType(fact.FactType), "descriptor");
    }

    private static string? FirstSafeValue(IReadOnlyDictionary<string, string> properties, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (properties.TryGetValue(key, out var value) && IsSafeIdentifier(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    private static string? FirstHashValue(IReadOnlyDictionary<string, string> properties)
    {
        return FirstValue(
            properties,
            "entityHash",
            "typeHash",
            "storageObjectHash",
            "tableHash",
            "columnHash",
            "propertyHash",
            "relationHash",
            "associationHash",
            "connectionNameHash",
            "providerInvariantHash",
            "metadataHash");
    }

    private static string DescriptorId(
        CombinedSurfaceFactInput fact,
        string metadataFormat,
        string sourceArtifactType,
        string modelKind,
        string descriptorRole,
        string? stableModelKey,
        string displayNameHash,
        IReadOnlyList<string> supportingFactIds)
    {
        var commitCategory = string.IsNullOrWhiteSpace(fact.CommitSha) ? "sha-missing" : $"sha:{fact.CommitSha.Trim()}";
        var seed = string.Join(
            "|",
            ProjectionSeedVersion,
            fact.SourceIndexId,
            commitCategory,
            fact.RuleId,
            metadataFormat,
            modelKind,
            descriptorRole,
            stableModelKey ?? $"display-hash:{displayNameHash}",
            sourceArtifactType,
            SafePath(fact.FilePath),
            fact.StartLine.ToString(System.Globalization.CultureInfo.InvariantCulture),
            fact.EndLine.ToString(System.Globalization.CultureInfo.InvariantCulture),
            string.Join(";", supportingFactIds));
        return "ldm-surface:" + Hash(seed, 32);
    }

    private static string DuplicateIdentityKey(LegacyDataModelDescriptorProjectionRow row)
    {
        return string.Join(
            "|",
            row.SourceIndexId,
            row.SourceRuleId,
            row.MetadataFormat,
            row.ModelKind,
            row.DescriptorRole,
            row.StableModelKey ?? $"display-hash:{row.DisplayNameHash}",
            row.SourceArtifactType,
            row.FilePath,
            row.StartLine.ToString(System.Globalization.CultureInfo.InvariantCulture),
            row.EndLine.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    private static string SourceArtifactType(string metadataFormat, string? descriptorKind, string? sourceSection)
    {
        descriptorKind ??= string.Empty;
        sourceSection = ClosedToken(sourceSection, string.Empty);
        return metadataFormat switch
        {
            "dbml" => "dbml",
            "edmx" when sourceSection == "csdl" => "edmx-csdl",
            "edmx" when sourceSection == "ssdl" => "edmx-ssdl",
            "edmx" when sourceSection == "msl" => "edmx-msl",
            "edmx" when descriptorKind.StartsWith("csdl", StringComparison.Ordinal) => "edmx-csdl",
            "edmx" when descriptorKind.StartsWith("ssdl", StringComparison.Ordinal) => "edmx-ssdl",
            "edmx" when descriptorKind.StartsWith("msl", StringComparison.Ordinal) || descriptorKind.Contains("mapping", StringComparison.Ordinal) => "edmx-msl",
            "edmx" => "edmx-csdl",
            "typed-dataset" when descriptorKind.Contains("adapter", StringComparison.Ordinal) => "tableadapter-command",
            "tableadapter" => "tableadapter-command",
            "typed-dataset" => "typed-dataset-xsd",
            "generated-code" => "generated-data-code",
            "config" => "provider-config",
            "nhibernate" => "nhibernate-hbm",
            "nhibernate-hbm" => "nhibernate-hbm",
            _ => "unknown"
        };
    }

    private static string? MetadataFormatFromKind(string? metadataKind)
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
            _ => null
        };
    }

    private static string ModelKindFromFactType(string factType)
    {
        return factType switch
        {
            FactTypes.LegacyDataMetadataDeclared => "model",
            FactTypes.LegacyDataEntityDeclared => "entity",
            FactTypes.LegacyDataStorageObjectDeclared => "storage-object",
            FactTypes.LegacyDataColumnDeclared => "column",
            FactTypes.LegacyDataMappingDeclared => "mapping",
            FactTypes.LegacyDataProviderConfigDeclared => "provider-config",
            FactTypes.LegacyDataGeneratedCodeLinked => "generated-code-link",
            _ => "unknown"
        };
    }

    private static string CoverageLabel(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "full" => "full",
            "reduced" => "reduced",
            "unknown" => "unknown",
            _ => "unknown"
        };
    }

    private static IReadOnlyList<string> Split(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split([';', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static IReadOnlyList<string> Append(IReadOnlyList<string> values, string value)
    {
        return values
            .Append(value)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();
    }

    private static string ClosedToken(string? value, string fallback)
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

    private static bool IsSafeIdentifier(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= 128
            && SafeIdentifierPattern().IsMatch(trimmed)
            && !trimmed.Contains("://", StringComparison.Ordinal)
            && !trimmed.Contains('\\', StringComparison.Ordinal)
            && !trimmed.StartsWith("/", StringComparison.Ordinal)
            && !trimmed.StartsWith("../", StringComparison.Ordinal)
            && !trimmed.Contains("password", StringComparison.OrdinalIgnoreCase)
            && !trimmed.Contains("secret", StringComparison.OrdinalIgnoreCase)
            && !trimmed.Contains("token", StringComparison.OrdinalIgnoreCase)
            && !trimmed.Contains("connectionstring", StringComparison.OrdinalIgnoreCase);
    }

    private static string SafePath(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return "n/a";
        }

        return Path.IsPathFullyQualified(filePath)
            || filePath.StartsWith("/", StringComparison.Ordinal)
            || filePath.StartsWith("\\", StringComparison.Ordinal)
            || filePath.Contains("://", StringComparison.Ordinal)
            || filePath.Contains(":/", StringComparison.Ordinal)
            || filePath.Contains(":\\", StringComparison.Ordinal)
            ? $"absolute-path-hash:{Hash(filePath, 16)}"
            : filePath.Replace('\\', '/');
    }

    private static string Hash(string value, int length = 64)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        var text = Convert.ToHexString(bytes).ToLowerInvariant();
        return text[..Math.Min(length, text.Length)];
    }

    [GeneratedRegex(@"^[A-Za-z_][A-Za-z0-9_.$]*$", RegexOptions.CultureInvariant)]
    private static partial Regex SafeIdentifierPattern();
}
