using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace TraceMap.Core;

public sealed record CombinedSurfaceFactInput(
    string CombinedFactId,
    string SourceIndexId,
    string SourceLabel,
    string OriginalFactId,
    string ScanId,
    string CommitSha,
    string FactType,
    string RuleId,
    string EvidenceTier,
    string FilePath,
    int StartLine,
    int EndLine,
    IReadOnlyDictionary<string, string> Properties);

public sealed record CombinedSurfaceProjectionRow(
    string SurfaceKind,
    string DisplayName,
    string SourceIndexId,
    string SourceLabel,
    string ScanId,
    string CommitSha,
    string CombinedFactId,
    string OriginalFactId,
    string FactType,
    string RuleId,
    string EvidenceTier,
    string FilePath,
    int StartLine,
    int EndLine,
    string? HttpMethod,
    string? NormalizedPathKey,
    string? OperationName,
    string? TableName,
    string? ColumnNames,
    string? SourceKind,
    string? ShapeHash,
    string? TextHash,
    string? TextLength,
    string? PackageName,
    string? Version,
    string? ConfigKey,
    string? Ecosystem = null,
    string? ManifestKind = null,
    string? DependencyScope = null,
    string? DependencyGroup = null,
    string? PackageManager = null,
    string? VersionHash = null,
    string? RedactionReason = null);

public static class CombinedSurfaceProjection
{
    public static IReadOnlyList<CombinedSurfaceProjectionRow> BuildSurfaces(IReadOnlyList<CombinedSurfaceFactInput> facts)
    {
        return facts
            .Select(ToSurface)
            .OfType<CombinedSurfaceProjectionRow>()
            .OrderBy(surface => surface.SurfaceKind, StringComparer.Ordinal)
            .ThenBy(surface => surface.SourceLabel, StringComparer.Ordinal)
            .ThenBy(surface => surface.DisplayName, StringComparer.Ordinal)
            .ThenBy(surface => surface.FilePath, StringComparer.Ordinal)
            .ThenBy(surface => surface.StartLine)
            .ToArray();
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

    private static CombinedSurfaceProjectionRow? ToSurface(CombinedSurfaceFactInput fact)
    {
        var surfaceKind = SurfaceKind(fact);
        if (surfaceKind is null)
        {
            return null;
        }

        var httpMethod = FirstValue(fact.Properties, "httpMethod", "httpMethods", "methodName");
        var normalizedPathKey = FirstValue(fact.Properties, "normalizedPathKey");
        var operationName = FirstValue(fact.Properties, "operationName");
        var mappingKind = FirstValue(fact.Properties, "mappingKind");
        var mappedName = FirstValue(fact.Properties, "mappedName");
        var tableName = SafeSqlIdentifierList(
            FirstValue(fact.Properties, "tableName", "tableNames") ?? (IsTableMappingKind(mappingKind) ? mappedName : null),
            100,
            allowSpaces: true);
        var columns = SafeSqlIdentifierList(
            FirstValue(fact.Properties, "columnNames", "fieldNames", "columnName") ?? (IsColumnMappingKind(mappingKind) ? mappedName : null),
            80,
            allowSpaces: false);
        var sourceKind = FirstValue(fact.Properties, "sqlSourceKind", "sourceKind");
        var shapeHash = FirstValue(fact.Properties, "queryShapeHash", "patternHash");
        var textHash = FirstValue(fact.Properties, "textHash");
        var textLength = FirstValue(fact.Properties, "textLength");
        var packageName = FirstValue(fact.Properties, "packageName", "package", "dependencyName", "moduleName", "name");
        var rawVersion = FirstValue(fact.Properties, "version", "packageVersion");
        var version = SafePackageVersion(rawVersion);
        var unsafeVersion = !string.IsNullOrWhiteSpace(rawVersion) && version is null;
        var versionHash = FirstValue(fact.Properties, "versionHash")
            ?? (unsafeVersion ? Hash(rawVersion!, 32) : null);
        var redactionReason = FirstValue(fact.Properties, "redactionReason")
            ?? (unsafeVersion ? "unsafe-package-version" : null);
        var configKey = FirstValue(fact.Properties, "keyPath", "configKey", "connectionStringName", "environmentVariableName");
        var ecosystem = FirstValue(fact.Properties, "ecosystem", "packageEcosystem", "packageManager");
        var manifestKind = FirstValue(fact.Properties, "manifestKind", "metadataSource", "sourceFormat", "type");
        var dependencyScope = FirstValue(fact.Properties, "dependencyScope", "scope");
        var dependencyGroup = FirstValue(fact.Properties, "dependencyGroup", "dependencySection", "buildTool");
        var packageManager = FirstValue(fact.Properties, "packageManager", "buildTool");
        var displayName = surfaceKind switch
        {
            "http-client" or "http-route" => normalizedPathKey ?? FirstValue(fact.Properties, "normalizedPathTemplate") ?? $"{httpMethod ?? "ANY"} unknown",
            "sql-query" => SqlSurfaceDisplayName(fact, operationName, tableName, columns, sourceKind, shapeHash, textHash),
            "sql-persistence" => SqlPersistenceDisplayName(fact, tableName, columns, mappedName),
            "package-config" => packageName ?? configKey ?? $"unknown-package-config:{fact.CombinedFactId}",
            _ => $"unknown-surface:{fact.CombinedFactId}"
        };

        return new CombinedSurfaceProjectionRow(
            surfaceKind,
            displayName,
            fact.SourceIndexId,
            fact.SourceLabel,
            fact.ScanId,
            fact.CommitSha,
            fact.CombinedFactId,
            fact.OriginalFactId,
            fact.FactType,
            fact.RuleId,
            fact.EvidenceTier,
            SafePath(fact.FilePath),
            fact.StartLine,
            fact.EndLine,
            httpMethod,
            normalizedPathKey,
            operationName,
            tableName,
            columns ?? (surfaceKind == "sql-query" ? "n/a" : null),
            sourceKind,
            shapeHash,
            textHash,
            textLength,
            packageName,
            version,
            configKey,
            ecosystem,
            manifestKind,
            dependencyScope,
            dependencyGroup,
            packageManager,
            versionHash,
            redactionReason);
    }

    private static string? SurfaceKind(CombinedSurfaceFactInput fact)
    {
        if (fact.FactType == FactTypes.HttpCallDetected)
        {
            return "http-client";
        }

        if (fact.FactType == FactTypes.HttpRouteBinding)
        {
            return "http-route";
        }

        if (fact.FactType == FactTypes.DatabaseColumnMapping)
        {
            return "sql-persistence";
        }

        if (fact.FactType is FactTypes.QueryPatternDetected or FactTypes.SqlTextUsed or FactTypes.DapperCallDetected or FactTypes.SqlCommandDetected)
        {
            return "sql-query";
        }

        if (fact.Properties.TryGetValue("surfaceKind", out var declaredSurfaceKind)
            && !string.IsNullOrWhiteSpace(declaredSurfaceKind))
        {
            var trimmed = declaredSurfaceKind.Trim();
            return string.Equals(trimmed, "package", StringComparison.OrdinalIgnoreCase)
                ? "package-config"
                : trimmed;
        }

        if (fact.FactType == FactTypes.PackageReferenced)
        {
            return "package-config";
        }

        if (fact.FactType is FactTypes.ConfigBinding
            or FactTypes.ConfigKeyDeclared
            or FactTypes.ConnectionStringDeclared)
        {
            return "package-config";
        }

        return null;
    }

    private static string SqlSurfaceDisplayName(
        CombinedSurfaceFactInput fact,
        string? operationName,
        string? tableName,
        string? columns,
        string? sourceKind,
        string? shapeHash,
        string? textHash)
    {
        if (!string.IsNullOrWhiteSpace(shapeHash))
        {
            return $"shape:{shapeHash}";
        }

        if (!string.IsNullOrWhiteSpace(operationName)
            || !string.IsNullOrWhiteSpace(tableName)
            || !string.IsNullOrWhiteSpace(columns))
        {
            return string.Join(
                " ",
                new[]
                {
                    operationName,
                    tableName is null ? null : $"table:{tableName}",
                    columns is null ? null : $"columns:{columns}",
                    sourceKind is null ? null : $"source:{sourceKind}"
                }.Where(value => !string.IsNullOrWhiteSpace(value)));
        }

        if (!string.IsNullOrWhiteSpace(textHash))
        {
            return $"text:{textHash}";
        }

        return $"unknown-sql:{Hash(fact.OriginalFactId ?? fact.CombinedFactId, 16)}";
    }

    private static string SqlPersistenceDisplayName(CombinedSurfaceFactInput fact, string? tableName, string? columns, string? mappedName)
    {
        if (!string.IsNullOrWhiteSpace(tableName))
        {
            return $"table:{tableName}";
        }

        if (!string.IsNullOrWhiteSpace(columns))
        {
            return $"columns:{columns}";
        }

        var safeMappedName = SafeSqlIdentifierList(mappedName, 80, allowSpaces: true);
        if (!string.IsNullOrWhiteSpace(safeMappedName))
        {
            return $"mapping:{safeMappedName}";
        }

        return $"unknown-persistence:{Hash(fact.OriginalFactId ?? fact.CombinedFactId, 16)}";
    }

    private static string? SafePackageVersion(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return IsUnsafePackageVersion(trimmed) ? null : trimmed;
    }

    private static bool IsUnsafePackageVersion(string value)
    {
        return value.Contains("://", StringComparison.Ordinal)
            || value.Contains("\\", StringComparison.Ordinal)
            || value.StartsWith("/", StringComparison.Ordinal)
            || value.StartsWith("./", StringComparison.Ordinal)
            || value.StartsWith("../", StringComparison.Ordinal)
            || value.StartsWith("file:", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("git+", StringComparison.OrdinalIgnoreCase)
            || value.Contains("${", StringComparison.Ordinal)
            || value.Contains("$(", StringComparison.Ordinal)
            || value.Contains("%", StringComparison.Ordinal);
    }

    private static bool IsTableMappingKind(string? mappingKind)
    {
        return mappingKind is not null && mappingKind.Contains("Table", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsColumnMappingKind(string? mappingKind)
    {
        return mappingKind is not null && mappingKind.Contains("Column", StringComparison.OrdinalIgnoreCase);
    }

    private static string? SafeSqlIdentifierList(string? value, int maxIdentifierLength, bool allowSpaces)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var identifiers = value
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(identifier => IsSafeSqlIdentifier(identifier, maxIdentifierLength, allowSpaces))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(identifier => identifier, StringComparer.Ordinal)
            .ToArray();
        return identifiers.Length == 0 ? null : string.Join(';', identifiers);
    }

    private static bool IsSafeSqlIdentifier(string value, int maxIdentifierLength, bool allowSpaces)
    {
        if (value.Length == 0 || value.Length > maxIdentifierLength)
        {
            return false;
        }

        if (value.Contains("://", StringComparison.Ordinal)
            || value.Contains("--", StringComparison.Ordinal)
            || value.Contains("/*", StringComparison.Ordinal)
            || value.Contains("*/", StringComparison.Ordinal))
        {
            return false;
        }

        var pattern = allowSpaces
            ? "^[A-Za-z0-9_. -]+$"
            : "^[A-Za-z0-9_.-]+$";
        if (!Regex.IsMatch(value, pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)))
        {
            return false;
        }

        var token = value.Trim().ToUpperInvariant();
        return token is not ("SELECT" or "INSERT" or "UPDATE" or "DELETE" or "CREATE" or "ALTER" or "DROP" or "TRUNCATE" or "MERGE" or "CALL" or "EXEC" or "EXECUTE" or "WHERE" or "FROM" or "JOIN");
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
}
