using TraceMap.Core;

namespace TraceMap.Reporting;

public static class MarkdownReportWriter
{
    public static async Task WriteAsync(string path, ScanResult result, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        await File.WriteAllTextAsync(path, Build(result), cancellationToken);
    }

    public static string Build(ScanResult result)
    {
        var manifest = result.Manifest;
        var factsByType = result.Facts
            .GroupBy(fact => fact.FactType)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => (FactType: group.Key, Count: group.Count()))
            .ToArray();

        var lines = new List<string>
        {
            "# TraceMap Scan Report",
            "",
            "## Repository",
            "",
            $"- Repo: `{manifest.RepoName}`",
            $"- Commit SHA: `{manifest.CommitSha}`",
            $"- Branch: `{manifest.Branch ?? "unknown"}`",
            $"- Remote: `{manifest.RemoteUrl ?? "unknown"}`",
            $"- Scan ID: `{manifest.ScanId}`",
            $"- Scanner version: `{manifest.ScannerVersion}`",
            "",
            "## Analysis Coverage",
            "",
            $"- Analysis level: `{manifest.AnalysisLevel}`",
            $"- Build status: `{manifest.BuildStatus}`",
            "",
            "This report is an evidence inventory and syntax map only. It does not classify contract impact.",
            "",
            "## Inventory",
            "",
            $"- Solutions: `{manifest.Solutions.Count}`",
            $"- Projects: `{manifest.Projects.Count}`",
            $"- Target frameworks: `{manifest.TargetFrameworks.Count}`",
            $"- Inventoried files: `{result.Inventory.Count}`",
            "",
            "## Known Gaps",
            ""
        };

        if (manifest.KnownGaps.Count == 0)
        {
            lines.Add("- None recorded.");
        }
        else
        {
            lines.AddRange(manifest.KnownGaps.Select(gap => $"- {gap}"));
        }

        AddBuildEnvironmentDiagnostics(lines, result);

        lines.Add("");
        lines.Add("## Facts By Type");
        lines.Add("");

        foreach (var item in factsByType)
        {
            lines.Add($"- `{item.FactType}`: `{item.Count}`");
        }

        lines.Add("");
        lines.Add("## Solutions");
        lines.Add("");
        lines.AddRange(manifest.Solutions.Count == 0 ? ["- None found."] : manifest.Solutions.Select(path => $"- `{path}`"));

        lines.Add("");
        lines.Add("## Projects");
        lines.Add("");
        lines.AddRange(manifest.Projects.Count == 0 ? ["- None found."] : manifest.Projects.Select(path => $"- `{path}`"));

        AddFactSection(
            lines,
            "HTTP Calls",
            result.Facts.Where(fact => fact.FactType is FactTypes.HttpCallDetected or FactTypes.HttpClientCreated),
            fact => $"- `{fact.FactType}` `{DisplayFactName(fact)}` ({fact.EvidenceTier}) at `{fact.Evidence.FilePath}:{fact.Evidence.StartLine}`");

        AddFactSection(
            lines,
            "Database Calls",
            result.Facts.Where(fact => fact.FactType is FactTypes.DbContextDeclared
                or FactTypes.DbSetDeclared
                or FactTypes.DbChangeSaved
                or FactTypes.DapperCallDetected
                or FactTypes.SqlCommandDetected
                or FactTypes.SqlTextUsed),
            fact => $"- `{fact.FactType}` `{DisplayFactName(fact)}` ({fact.EvidenceTier}) at `{fact.Evidence.FilePath}:{fact.Evidence.StartLine}`");

        AddFactSection(
            lines,
            "Config Keys",
            result.Facts.Where(fact => fact.FactType is FactTypes.ConfigKeyDeclared or FactTypes.ConnectionStringDeclared),
            fact => $"- `{fact.FactType}` `{DisplayFactName(fact)}` ({fact.EvidenceTier}) at `{fact.Evidence.FilePath}:{fact.Evidence.StartLine}`");

        AddFactSection(
            lines,
            "Legacy Data Metadata",
            result.Facts.Where(fact => fact.FactType is FactTypes.LegacyDataMetadataDeclared
                or FactTypes.LegacyDataEntityDeclared
                or FactTypes.LegacyDataStorageObjectDeclared
                or FactTypes.LegacyDataColumnDeclared
                or FactTypes.LegacyDataMappingDeclared
                or FactTypes.LegacyDataProviderConfigDeclared
                or FactTypes.LegacyDataGeneratedCodeLinked),
            FormatLegacyDataMetadataFact);

        AddFactSection(
            lines,
            "Call Flow",
            result.Facts.Where(fact => fact.FactType == FactTypes.CallEdge),
            fact => $"- `{DisplaySource(fact)}` -> `{DisplayFactName(fact)}` ({fact.EvidenceTier}) at `{fact.Evidence.FilePath}:{fact.Evidence.StartLine}`");

        AddFactSection(
            lines,
            "Object Creations",
            result.Facts.Where(fact => fact.FactType == FactTypes.ObjectCreated),
            fact => $"- `{DisplaySource(fact)}` creates `{DisplayFactName(fact)}` ({fact.EvidenceTier}) at `{fact.Evidence.FilePath}:{fact.Evidence.StartLine}`");

        AddFactSection(
            lines,
            "Argument Flow",
            result.Facts.Where(fact => fact.FactType == FactTypes.ArgumentPassed),
            fact => $"- `{DisplaySource(fact)}` passes argument `{fact.Properties.GetValueOrDefault("argumentOrdinal") ?? "?"}` to `{fact.Properties.GetValueOrDefault("parameterName") ?? "unknown"}` on `{DisplayFactName(fact)}` ({fact.EvidenceTier}) at `{fact.Evidence.FilePath}:{fact.Evidence.StartLine}`");

        AddFactSection(
            lines,
            "Local Aliases",
            result.Facts.Where(fact => fact.FactType == FactTypes.LocalAlias),
            fact => $"- `{fact.Properties.GetValueOrDefault("aliasSymbol") ?? DisplayFactName(fact)}` aliases `{fact.Properties.GetValueOrDefault("originSymbol") ?? "unknown"}` ({fact.EvidenceTier}) at `{fact.Evidence.FilePath}:{fact.Evidence.StartLine}`");

        AddFactSection(
            lines,
            "Field Aliases",
            result.Facts.Where(fact => fact.FactType == FactTypes.FieldAlias),
            fact => $"- `{fact.Properties.GetValueOrDefault("fieldSymbol") ?? DisplayFactName(fact)}` aliases `{fact.Properties.GetValueOrDefault("originSymbol") ?? "unknown"}` ({fact.EvidenceTier}) at `{fact.Evidence.FilePath}:{fact.Evidence.StartLine}`");

        AddFactSection(
            lines,
            "Logic Hotspots",
            result.Facts.Where(fact => fact.FactType is FactTypes.CalculationExpression
                or FactTypes.BranchingLogic
                or FactTypes.RetryPolicyLogic
                or FactTypes.SerializationLogic),
            fact => $"- `{fact.FactType}` `{DisplayFactName(fact)}` ({fact.EvidenceTier}) at `{fact.Evidence.FilePath}:{fact.Evidence.StartLine}`");

        AddFactSection(
            lines,
            "Query Patterns",
            result.Facts.Where(fact => fact.FactType == FactTypes.QueryPatternDetected),
            FormatQueryPattern);

        AddFactSection(
            lines,
            "Object Shapes",
            result.Facts.Where(fact => fact.FactType == FactTypes.ObjectShapeInferred),
            fact => $"- `{fact.Properties.GetValueOrDefault("objectKind") ?? DisplayFactName(fact)}` fields `{fact.Properties.GetValueOrDefault("fieldNames") ?? "unknown"}` ({fact.EvidenceTier}) at `{fact.Evidence.FilePath}:{fact.Evidence.StartLine}`");

        AddFactSection(
            lines,
            "Flow Boundaries",
            result.Facts.Where(fact => fact.FactType is FactTypes.DependencyResolved
                or FactTypes.DeserializedObject
                or FactTypes.ReflectionUsage
                or FactTypes.DynamicInvocation
                or FactTypes.CollectionMutation
                or FactTypes.ObjectMutation
                or FactTypes.BranchCondition
                or FactTypes.CallbackBoundary
                or FactTypes.AsyncBoundary),
            fact => $"- `{fact.FactType}` `{DisplayFactName(fact)}` ({fact.EvidenceTier}) at `{fact.Evidence.FilePath}:{fact.Evidence.StartLine}`");

        AddFactSection(
            lines,
            "Runtime Evidence",
            result.Facts.Where(fact => fact.FactType is FactTypes.DependencyRegistered
                or FactTypes.SerializerContractMember
                or FactTypes.ReflectionTarget
                or FactTypes.DynamicDispatchCandidate
                or FactTypes.CollectionElementFlow
                or FactTypes.MutationSemantics
                or FactTypes.BranchFeasibility),
            fact => $"- `{fact.FactType}` `{DisplayFactName(fact)}` ({fact.EvidenceTier}) at `{fact.Evidence.FilePath}:{fact.Evidence.StartLine}`");

        AddFactSection(
            lines,
            "Contract Mappings",
            result.Facts.Where(fact => fact.FactType is FactTypes.HttpRouteBinding
                or FactTypes.DatabaseColumnMapping
                or FactTypes.ConfigBinding
                or FactTypes.SerializerContractMember),
            fact => $"- `{fact.FactType}` `{DisplayFactName(fact)}` ({fact.EvidenceTier}) at `{fact.Evidence.FilePath}:{fact.Evidence.StartLine}`");

        AddFactSection(
            lines,
            "Symbol Relationships",
            result.Facts.Where(fact => fact.FactType == FactTypes.SymbolRelationship),
            fact => $"- `{DisplaySource(fact)}` `{fact.Properties.GetValueOrDefault("relationshipKind") ?? DisplayFactName(fact)}` `{DisplayFactName(fact)}` ({fact.EvidenceTier}) at `{fact.Evidence.FilePath}:{fact.Evidence.StartLine}`");

        AddFactSection(
            lines,
            "WebForms Events",
            result.Facts.Where(fact => fact.FactType is FactTypes.WebFormsPageDeclared
                or FactTypes.WebFormsControlDeclared
                or FactTypes.WebFormsEventBindingDeclared
                or FactTypes.WebFormsDesignerControlDeclared
                or FactTypes.WebFormsHandlerResolved),
            FormatWebFormsEventFact);

        AddFactSection(
            lines,
            "WebForms Event Flow",
            result.Facts.Where(fact => fact.FactType == FactTypes.WebFormsEventFlowProjected),
            fact => $"- `{fact.Properties.GetValueOrDefault("flowClassification") ?? "UnknownAnalysisGap"}` `{fact.Properties.GetValueOrDefault("handlerName") ?? DisplayFactName(fact)}` -> `{fact.Properties.GetValueOrDefault("terminalSurfaceKind") ?? "none"}` ({fact.EvidenceTier}, coverage `{fact.Properties.GetValueOrDefault("coverage") ?? "unknown"}`) at `{fact.Evidence.FilePath}:{fact.Evidence.StartLine}`");

        AddFactSection(
            lines,
            "WebForms Static Logic Signals",
            result.Facts.Where(fact => fact.FactType == FactTypes.WebFormsLogicSignalDetected),
            fact => $"- `{fact.Properties.GetValueOrDefault("signalKind") ?? "unknown"}` for `{fact.Properties.GetValueOrDefault("handlerName") ?? DisplayFactName(fact)}` ({fact.EvidenceTier}) at `{fact.Evidence.FilePath}:{fact.Evidence.StartLine}`");

        AddFactSection(
            lines,
            "Boilerplate Signals",
            result.Facts.Where(fact => fact.FactType == FactTypes.InfrastructureBoilerplate),
            fact => $"- `{fact.Properties.GetValueOrDefault("category") ?? "unknown"}` at `{fact.Evidence.FilePath}:{fact.Evidence.StartLine}`");

        if (result.Facts.Any(fact => fact.FactType == FactTypes.QueryPatternDetected))
        {
            lines.Add("");
            lines.Add("## Query Pattern Limitations");
            lines.Add("");
            lines.Add("- Query-pattern rows are static shape evidence. They do not prove runtime execution, database schema existence, SQL dialect validity, generated SQL equivalence, or branch feasibility.");
        }

        if (result.Facts.Any(fact => fact.FactType.StartsWith("LegacyData", StringComparison.Ordinal)))
        {
            lines.Add("");
            lines.Add("## Legacy Data Metadata Limitations");
            lines.Add("");
            lines.Add("- Legacy data metadata rows are static design-time metadata evidence from checked-in DBML, EDMX, typed DataSet, TableAdapter, config, or generated-code descriptors.");
            lines.Add("- They do not prove runtime data access, SQL execution, database existence, provider compatibility, config transform selection, generated-code freshness, deployment, or production usage.");
            lines.Add("- Raw SQL, connection strings, config values, URLs, local paths, remotes, source snippets, and secret-looking values are hashed or omitted.");
        }

        if (result.Facts.Any(fact => fact.FactType.StartsWith("WebForms", StringComparison.Ordinal)))
        {
            lines.Add("");
            lines.Add("## WebForms Limitations");
            lines.Add("");
            lines.Add("- WebForms event evidence is static markup, code-behind, designer, and direct backend evidence. It does not prove runtime event firing, page lifecycle execution, event bubbling, deployment, service reachability, SQL execution, branch feasibility, or production usage.");
            lines.Add("- Static logic signals and UI-boilerplate signals are deterministic heuristics, not proof of business logic or code quality.");
        }

        lines.Add("");
        return string.Join(Environment.NewLine, lines);
    }

    private static void AddFactSection(List<string> lines, string title, IEnumerable<CodeFact> facts, Func<CodeFact, string> format)
    {
        lines.Add("");
        lines.Add($"## {title}");
        lines.Add("");

        var selectedFacts = facts
            .OrderBy(fact => fact.Evidence.FilePath, StringComparer.Ordinal)
            .ThenBy(fact => fact.Evidence.StartLine)
            .ThenBy(fact => DisplayFactName(fact), StringComparer.Ordinal)
            .Take(50)
            .ToArray();

        lines.AddRange(selectedFacts.Length == 0 ? ["- None found."] : selectedFacts.Select(format));
    }

    private static void AddBuildEnvironmentDiagnostics(List<string> lines, ScanResult result)
    {
        var diagnostics = result.Facts
            .Where(fact => fact.FactType == FactTypes.BuildEnvironmentDiagnostic)
            .OrderBy(fact => fact.Properties.GetValueOrDefault("diagnosticKind"), StringComparer.Ordinal)
            .ThenBy(fact => fact.Properties.GetValueOrDefault("diagnosticCode"), StringComparer.Ordinal)
            .ThenBy(fact => fact.Evidence.FilePath, StringComparer.Ordinal)
            .ThenBy(fact => fact.Evidence.StartLine)
            .ThenBy(fact => fact.FactId, StringComparer.Ordinal)
            .ToArray();
        if (diagnostics.Length == 0)
        {
            return;
        }

        lines.Add("");
        lines.Add("## Build Environment Diagnostics");
        lines.Add("");
        if (result.Manifest.BuildStatus == "FailedOrPartial")
        {
            lines.Add("Build or project load coverage is reduced; syntax/config fallback analysis continued where possible.");
            lines.Add("");
        }

        lines.Add("| Code | Tier | Rule | Evidence | Guidance | Limitation |");
        lines.Add("| --- | --- | --- | --- | --- | --- |");
        foreach (var fact in diagnostics.Take(100))
        {
            var code = DisplayCodeValue(fact.Properties.GetValueOrDefault("diagnosticCode") ?? fact.ContractElement ?? "unknown");
            var evidence = CombinedReportHelpers.SafePath(fact.Evidence.FilePath) + $":{fact.Evidence.StartLine}";
            var guidance = DisplayTableValue(fact.Properties.GetValueOrDefault("guidance") ?? fact.Properties.GetValueOrDefault("guidanceCode") ?? "Review the diagnostic evidence.");
            var limitation = DisplayTableValue(fact.Properties.GetValueOrDefault("limitation") ?? "Static diagnostic evidence only.");
            lines.Add($"| `{code}` | `{fact.EvidenceTier}` | `{fact.RuleId}` | `{evidence}` | {guidance} | {limitation} |");
        }
    }

    private static string DisplayFactName(CodeFact fact)
    {
        return fact.ContractElement ?? fact.TargetSymbol ?? fact.Properties.GetValueOrDefault("keyPath") ?? "unknown";
    }

    private static string DisplaySource(CodeFact fact)
    {
        return string.IsNullOrWhiteSpace(fact.SourceSymbol) ? "unknown" : fact.SourceSymbol;
    }

    private static string DisplayFields(CodeFact fact)
    {
        var fields = new[]
            {
                fact.Properties.GetValueOrDefault("filterFields"),
                fact.Properties.GetValueOrDefault("sortFields"),
                fact.Properties.GetValueOrDefault("selectFields"),
                fact.Properties.GetValueOrDefault("includeFields"),
                fact.Properties.GetValueOrDefault("mutationFields")
            }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal);

        var joined = string.Join(";", fields);
        return string.IsNullOrWhiteSpace(joined) ? "none" : joined;
    }

    private static string FormatQueryPattern(CodeFact fact)
    {
        return IsSqlShapeQueryPattern(fact)
            ? FormatSqlShapeQueryPattern(fact)
            : FormatQueryBuilderPattern(fact);
    }

    private static string FormatWebFormsEventFact(CodeFact fact)
    {
        return fact.FactType switch
        {
            FactTypes.WebFormsEventBindingDeclared => $"- event `{fact.Properties.GetValueOrDefault("eventName") ?? "unknown"}` on `{fact.Properties.GetValueOrDefault("controlId") ?? "unknown"}` -> `{fact.Properties.GetValueOrDefault("handlerName") ?? DisplayFactName(fact)}` ({fact.EvidenceTier}) at `{fact.Evidence.FilePath}:{fact.Evidence.StartLine}`",
            FactTypes.WebFormsHandlerResolved => $"- handler `{fact.Properties.GetValueOrDefault("handlerName") ?? DisplayFactName(fact)}` resolved as `{fact.Properties.GetValueOrDefault("resolutionKind") ?? "unknown"}` ({fact.EvidenceTier}) at `{fact.Evidence.FilePath}:{fact.Evidence.StartLine}`",
            FactTypes.WebFormsDesignerControlDeclared => $"- designer field `{fact.Properties.GetValueOrDefault("fieldName") ?? DisplayFactName(fact)}` type `{fact.Properties.GetValueOrDefault("controlType") ?? "unknown"}` ({fact.EvidenceTier}) at `{fact.Evidence.FilePath}:{fact.Evidence.StartLine}`",
            FactTypes.WebFormsControlDeclared => $"- control `{fact.Properties.GetValueOrDefault("controlId") ?? DisplayFactName(fact)}` type `{fact.Properties.GetValueOrDefault("controlType") ?? "unknown"}` ({fact.EvidenceTier}) at `{fact.Evidence.FilePath}:{fact.Evidence.StartLine}`",
            _ => $"- `{fact.FactType}` `{DisplayFactName(fact)}` ({fact.EvidenceTier}) at `{fact.Evidence.FilePath}:{fact.Evidence.StartLine}`"
        };
    }

    private static string FormatLegacyDataMetadataFact(CodeFact fact)
    {
        var metadataKind = DisplayCodeValue(fact.Properties.GetValueOrDefault("metadataKind") ?? "unknown");
        var label = FirstPresentValue(
            fact.Properties.GetValueOrDefault("entityName"),
            fact.Properties.GetValueOrDefault("storageObjectName"),
            fact.Properties.GetValueOrDefault("columnName"),
            fact.Properties.GetValueOrDefault("connectionName"),
            fact.Properties.GetValueOrDefault("typeName"),
            fact.Properties.GetValueOrDefault("targetName"),
            fact.ContractElement,
            fact.TargetSymbol,
            "hash-only");
        var role = FirstPresentValue(
            fact.Properties.GetValueOrDefault("entityKind"),
            fact.Properties.GetValueOrDefault("storageObjectKind"),
            fact.Properties.GetValueOrDefault("columnKind"),
            fact.Properties.GetValueOrDefault("mappingKind"),
            fact.Properties.GetValueOrDefault("configKind"),
            fact.Properties.GetValueOrDefault("linkKind"),
            fact.Properties.GetValueOrDefault("inventoryKind"),
            fact.FactType);
        var path = CombinedReportHelpers.SafePath(fact.Evidence.FilePath);
        return $"- `{fact.FactType}` `{DisplayCodeValue(metadataKind)}` `{DisplayIdentifierValue(label, IdentifierKind.Column, "hash-only")}` role `{DisplayCodeValue(role)}` rule `{fact.RuleId}` ({fact.EvidenceTier}) at `{path}:{fact.Evidence.StartLine}`";
    }

    private static bool IsSqlShapeQueryPattern(CodeFact fact)
    {
        return fact.Properties.TryGetValue("sqlSourceKind", out var value) && !string.IsNullOrWhiteSpace(value);
    }

    private static string FirstPresentValue(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }

    private static string FormatSqlShapeQueryPattern(CodeFact fact)
    {
        var operation = DisplayCodeValue(fact.Properties.GetValueOrDefault("operationName") ?? "unknown");
        var table = DisplayIdentifierValue(
            FirstPresent(fact.Properties.GetValueOrDefault("tableName"), fact.Properties.GetValueOrDefault("tableNames")),
            IdentifierKind.Table,
            "unknown");
        var columns = DisplayIdentifierValue(
            FirstPresent(fact.Properties.GetValueOrDefault("columnNames"), fact.Properties.GetValueOrDefault("fieldNames")),
            IdentifierKind.Column,
            "none");
        var sourceKind = DisplayCodeValue(fact.Properties.GetValueOrDefault("sqlSourceKind") ?? "unknown");
        var shapeHash = DisplayCodeValue(fact.Properties.GetValueOrDefault("queryShapeHash") ?? "n/a");
        var path = CombinedReportHelpers.SafePath(fact.Evidence.FilePath);

        return $"- SQL shape `{operation}` table `{table}` columns `{columns}` source `{sourceKind}` shape `{shapeHash}` rule `{fact.RuleId}` ({fact.EvidenceTier}) at `{path}:{fact.Evidence.StartLine}`";
    }

    private static string FormatQueryBuilderPattern(CodeFact fact)
    {
        var operation = DisplayCodeValue(fact.Properties.GetValueOrDefault("operationName") ?? DisplayFactName(fact));
        var patternHash = fact.Properties.GetValueOrDefault("patternHash");
        var hashPart = string.IsNullOrWhiteSpace(patternHash) ? string.Empty : $" pattern `{DisplayCodeValue(patternHash)}`";
        var path = CombinedReportHelpers.SafePath(fact.Evidence.FilePath);
        return $"- Query builder `{operation}` fields `{DisplayFields(fact)}`{hashPart} rule `{fact.RuleId}` ({fact.EvidenceTier}) at `{path}:{fact.Evidence.StartLine}`";
    }

    private static string DisplayIdentifierValue(string? rawValue, IdentifierKind kind, string missingValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return missingValue;
        }

        var allParts = rawValue
            .Split([',', ';', '|'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => DisplayIdentifier(value, kind))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (allParts.Length == 0)
        {
            return missingValue;
        }

        if (allParts.Length <= 20)
        {
            return string.Join(";", allParts);
        }

        return string.Join(";", allParts.Take(20)) + $";... and {allParts.Length - 20} more";
    }

    private static string DisplayIdentifier(string value, IdentifierKind kind)
    {
        var trimmed = value.Trim();
        if (IsSafeIdentifier(trimmed, kind))
        {
            return trimmed;
        }

        return $"unsafe-identifier-hash:{CombinedReportHelpers.Hash(trimmed, 32)}";
    }

    private static bool IsSafeIdentifier(string value, IdentifierKind kind)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > MaxIdentifierLength(kind))
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

        foreach (var ch in value)
        {
            var allowed = char.IsLetterOrDigit(ch)
                || ch is '_' or '.' or '-'
                || (kind == IdentifierKind.Table && ch == ' ');

            if (!allowed)
            {
                return false;
            }
        }

        var tokens = value
            .Split([' ', '.', '-'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return !tokens.Any(token => SqlKeywords.Contains(token));
    }

    private static int MaxIdentifierLength(IdentifierKind kind)
    {
        return kind == IdentifierKind.Table ? 100 : 80;
    }

    private static string DisplayCodeValue(string value)
    {
        return value.Replace('`', '\'').ReplaceLineEndings(" ");
    }

    private static string DisplayTableValue(string value)
    {
        return DisplayCodeValue(value).Replace("|", "/");
    }

    private static string? FirstPresent(string? first, string? second)
    {
        return string.IsNullOrWhiteSpace(first) ? second : first;
    }

    private enum IdentifierKind
    {
        Table,
        Column
    }

    private static readonly HashSet<string> SqlKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "select",
        "from",
        "insert",
        "into",
        "values",
        "update",
        "set",
        "delete",
        "where",
        "join",
        "having",
        "group",
        "order",
        "by",
        "union",
        "create",
        "alter",
        "drop",
        "truncate",
        "merge",
        "exec"
    };
}
