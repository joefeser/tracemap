using System.Xml;
using System.Xml.Linq;

namespace TraceMap.Core;

public static class SqlProjectRefactorExtractor
{
    private const long MaxInputBytes = 1024 * 1024;
    private const int MaxOperations = 1000;
    private const string Limitation =
        "Checked-in SQL project refactor intent only; project build, DACPAC packaging, deployment planning, SQL execution, target __RefactorLog state, applied operations, compatibility, rollback, production state, release approval, and safe execution are not proven.";

    public static IReadOnlyList<CodeFact> Extract(
        string repoPath,
        ScanManifest manifest,
        IEnumerable<FileInventoryItem> inventory)
    {
        var items = inventory.OrderBy(item => item.RelativePath, StringComparer.Ordinal).ToArray();
        var projects = items.Where(item => item.Kind == "SqlProject").ToArray();
        var logs = items.Where(item => item.Kind == "SqlProjectRefactorLog").ToArray();
        if (projects.Length == 0 && logs.Length == 0)
            return [];

        var facts = new List<CodeFact>();
        var referencedLogs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var processedLinks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var logsByPath = logs
            .GroupBy(item => item.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);

        foreach (var project in projects)
        {
            if (!TryLoad(repoPath, project, out var document, out var failure))
            {
                facts.Add(Gap(manifest, project.RelativePath, null, 1, ProjectFailure(failure)));
                continue;
            }

            var includes = document!
                .Descendants()
                .Where(element => element.Name.LocalName.Equals("RefactorLog", StringComparison.Ordinal))
                .OrderBy(Line, Comparer<int>.Default)
                .ThenBy(element => (string?)element.Attribute("Include"), StringComparer.Ordinal)
                .ToArray();

            foreach (var element in includes)
            {
                var include = (string?)element.Attribute("Include");
                if (!TryResolveReference(repoPath, project.RelativePath, include, out var relativePath, out var classification))
                {
                    facts.Add(Gap(manifest, project.RelativePath, project.RelativePath, Line(element), classification));
                    continue;
                }

                if (!logsByPath.TryGetValue(relativePath!, out var matches))
                {
                    facts.Add(Gap(manifest, project.RelativePath, project.RelativePath, Line(element), "RefactorLogReferenceMissing"));
                    continue;
                }
                if (matches.Length != 1)
                {
                    facts.Add(Gap(manifest, project.RelativePath, project.RelativePath, Line(element), "RefactorLogReferenceAmbiguous"));
                    continue;
                }

                var log = matches[0];
                referencedLogs.Add(log.RelativePath);
                if (!processedLinks.Add($"{project.RelativePath}\0{log.RelativePath}"))
                    continue;

                facts.Add(FactFactory.Create(
                    manifest,
                    FactTypes.SqlProjectRefactorLogDeclared,
                    RuleIds.DatabaseSqlProjectRefactorIntent,
                    EvidenceTiers.Tier2Structural,
                    Span(project.RelativePath, Line(element)),
                    projectPath: project.RelativePath,
                    targetSymbol: log.RelativePath,
                    properties: Properties(
                        ("objectKind", "refactor-log"),
                        ("linkStatus", "literal-project-reference"),
                        ("coverageLabel", "bounded-static-evidence"),
                        ("limitations", Limitation))));

                ExtractLog(repoPath, manifest, project.RelativePath, log, facts);
            }
        }

        foreach (var log in logs.Where(log => !referencedLogs.Contains(log.RelativePath)))
            facts.Add(Gap(manifest, log.RelativePath, null, 1, "RefactorLogProjectReferenceUnavailable"));

        return facts
            .OrderBy(fact => fact.Evidence.FilePath, StringComparer.Ordinal)
            .ThenBy(fact => fact.Evidence.StartLine)
            .ThenBy(fact => fact.FactType, StringComparer.Ordinal)
            .ThenBy(fact => fact.FactId, StringComparer.Ordinal)
            .ToArray();
    }

    private static void ExtractLog(
        string repoPath,
        ScanManifest manifest,
        string projectPath,
        FileInventoryItem log,
        ICollection<CodeFact> facts)
    {
        if (!TryLoad(repoPath, log, out var document, out var failure))
        {
            facts.Add(Gap(manifest, log.RelativePath, projectPath, 1, LogFailure(failure)));
            return;
        }

        var operations = document!.Descendants()
            .Where(element => element.Name.LocalName.Equals("Operation", StringComparison.Ordinal))
            .OrderBy(Line, Comparer<int>.Default)
            .ToArray();
        foreach (var operation in operations.Take(MaxOperations))
        {
            var name = ((string?)operation.Attribute("Name"))?.Trim();
            if (!TryOperation(operation, name, out var properties, out var source, out var target, out var classification))
            {
                facts.Add(Gap(manifest, log.RelativePath, projectPath, Line(operation), classification));
                continue;
            }

            var key = ChildValue(operation, "Key");
            if (!string.IsNullOrWhiteSpace(key))
                properties!["operationKeyHash"] = FactFactory.Hash(key, 32);
            properties!["coverageLabel"] = "bounded-static-evidence";
            properties["limitations"] = Limitation;
            facts.Add(FactFactory.Create(
                manifest,
                FactTypes.SqlProjectRefactorOperation,
                RuleIds.DatabaseSqlProjectRefactorIntent,
                EvidenceTiers.Tier2Structural,
                Span(log.RelativePath, Line(operation)),
                projectPath: projectPath,
                sourceSymbol: source,
                targetSymbol: target,
                contractElement: properties["operationKind"],
                properties: properties));
        }

        if (operations.Length > MaxOperations)
            facts.Add(Gap(manifest, log.RelativePath, projectPath, Line(operations[MaxOperations]), "RefactorOperationLimitExceeded",
                ("omittedOperationCount", (operations.Length - MaxOperations).ToString(System.Globalization.CultureInfo.InvariantCulture))));
    }

    private static bool TryOperation(
        XElement operation,
        string? name,
        out SortedDictionary<string, string>? properties,
        out string? source,
        out string? target,
        out string classification)
    {
        properties = null;
        source = null;
        target = null;
        classification = "RefactorOperationUnsupported";
        var elementType = ChildValue(operation, "ElementType");
        var objectKind = ObjectKind(elementType);
        var elementName = ChildValue(operation, "ElementName");
        if (objectKind is null)
        {
            classification = "RefactorElementTypeUnsupported";
            return false;
        }
        if (!TrySourceIdentity(objectKind, elementName, ChildValue(operation, "ParentElementName"), out var sourceParts))
        {
            classification = "RefactorSourceIdentityUnsupported";
            return false;
        }

        if (string.Equals(name, "Rename Refactor", StringComparison.Ordinal))
        {
            var newName = ChildValue(operation, "NewName");
            if (!TryMultipartIdentifier(newName, out var targetParts))
            {
                classification = "RefactorTargetIdentityUnsupported";
                return false;
            }
            if (!TryRename(objectKind, sourceParts, targetParts, out properties, out source, out target))
            {
                classification = "RefactorIdentityShapeUnsupported";
                return false;
            }
            return true;
        }

        if (string.Equals(name, "Move Schema", StringComparison.Ordinal) && objectKind == "table")
        {
            var newSchema = ChildValue(operation, "NewSchema");
            if (!TryMultipartIdentifier(newSchema, out var schemaParts) || schemaParts.Length != 1 || sourceParts.Length != 2)
            {
                classification = "RefactorIdentityShapeUnsupported";
                return false;
            }
            properties = Properties(
                ("objectKind", "table"),
                ("operationKind", "move-schema"),
                ("schemaName", sourceParts[0]),
                ("tableName", sourceParts[1]),
                ("newSchemaName", schemaParts[0]));
            source = $"{sourceParts[0]}.{sourceParts[1]}";
            target = $"{schemaParts[0]}.{sourceParts[1]}";
            return true;
        }

        classification = "RefactorOperationUnsupported";
        return false;
    }

    private static bool TryRename(
        string objectKind,
        string[] sourceParts,
        string[] targetParts,
        out SortedDictionary<string, string>? properties,
        out string? source,
        out string? target)
    {
        properties = null;
        source = null;
        target = null;
        if (objectKind == "table" && sourceParts.Length == 2 && targetParts.Length is 1 or 2)
        {
            var targetSchema = targetParts.Length == 2 ? targetParts[0] : sourceParts[0];
            var targetTable = targetParts[^1];
            properties = Properties(
                ("objectKind", "table"),
                ("operationKind", "rename-table"),
                ("schemaName", sourceParts[0]),
                ("tableName", sourceParts[1]),
                ("newSchemaName", targetSchema),
                ("newTableName", targetTable));
            source = $"{sourceParts[0]}.{sourceParts[1]}";
            target = $"{targetSchema}.{targetTable}";
            return true;
        }
        if (objectKind == "column" && sourceParts.Length == 3 && targetParts.Length is 1 or 3)
        {
            var targetSchema = targetParts.Length == 3 ? targetParts[0] : sourceParts[0];
            var targetTable = targetParts.Length == 3 ? targetParts[1] : sourceParts[1];
            var targetColumn = targetParts[^1];
            properties = Properties(
                ("objectKind", "column"),
                ("operationKind", "rename-column"),
                ("schemaName", sourceParts[0]),
                ("tableName", sourceParts[1]),
                ("columnName", sourceParts[2]),
                ("newSchemaName", targetSchema),
                ("newTableName", targetTable),
                ("newColumnName", targetColumn));
            source = $"{sourceParts[0]}.{sourceParts[1]}.{sourceParts[2]}";
            target = $"{targetSchema}.{targetTable}.{targetColumn}";
            return true;
        }
        return false;
    }

    private static string? ObjectKind(string? elementType)
    {
        if (string.IsNullOrWhiteSpace(elementType))
            return null;
        if (elementType.EndsWith("Table", StringComparison.OrdinalIgnoreCase))
            return "table";
        if (elementType.EndsWith("Column", StringComparison.OrdinalIgnoreCase))
            return "column";
        return null;
    }

    private static bool TrySourceIdentity(
        string objectKind,
        string? elementName,
        string? parentElementName,
        out string[] parts)
    {
        if (!TryMultipartIdentifier(elementName, out parts))
            return false;
        var expected = objectKind == "column" ? 3 : 2;
        if (parts.Length == expected)
            return true;
        if (parts.Length != 1
            || !TryMultipartIdentifier(parentElementName, out var parent)
            || parent.Length != expected - 1)
            return false;
        parts = [.. parent, parts[0]];
        return true;
    }

    private static bool TryMultipartIdentifier(string? value, out string[] parts)
    {
        parts = [];
        if (string.IsNullOrWhiteSpace(value) || value.Length > 512)
            return false;
        var candidates = value.Trim().Split('.', StringSplitOptions.TrimEntries);
        if (candidates.Length is < 1 or > 3)
            return false;
        var normalized = new List<string>(candidates.Length);
        foreach (var candidate in candidates)
        {
            var item = candidate;
            if (item.Length >= 2 && item[0] == '[' && item[^1] == ']')
                item = item[1..^1];
            if (item.Length is < 1 or > 128
                || !(char.IsLetter(item[0]) || item[0] is '_' or '#' or '@')
                || item.Any(character => !(char.IsLetterOrDigit(character) || character is '_' or '$' or '#' or '@')))
                return false;
            normalized.Add(item);
        }
        parts = normalized.ToArray();
        return true;
    }

    private static bool TryResolveReference(
        string repoPath,
        string projectPath,
        string? include,
        out string? relativePath,
        out string classification)
    {
        relativePath = null;
        classification = "RefactorLogReferenceUnsupported";
        if (string.IsNullOrWhiteSpace(include)
            || include.Contains("$(", StringComparison.Ordinal)
            || include.IndexOfAny(['*', '?']) >= 0
            || Path.IsPathRooted(include))
            return false;
        try
        {
            var root = Path.GetFullPath(repoPath);
            var projectDirectory = Path.GetDirectoryName(Path.Combine(root, projectPath)) ?? root;
            var resolved = Path.GetFullPath(Path.Combine(projectDirectory, include.Replace('\\', Path.DirectorySeparatorChar)));
            var rootPrefix = Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar;
            var pathComparison = OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            if (!resolved.StartsWith(rootPrefix, pathComparison))
            {
                classification = "RefactorLogReferenceEscapesScanRoot";
                return false;
            }
            relativePath = FileInventory.NormalizeRelativePath(Path.GetRelativePath(root, resolved));
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    private static bool TryLoad(
        string repoPath,
        FileInventoryItem item,
        out XDocument? document,
        out SafeXmlFailureKind? failure)
    {
        document = null;
        failure = null;
        var fullPath = Path.Combine(repoPath, item.RelativePath);
        try
        {
            if (item.SizeBytes > MaxInputBytes || new FileInfo(fullPath).Length > MaxInputBytes)
            {
                failure = SafeXmlFailureKind.TooLarge;
                return false;
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
        try
        {
            document = SafeXml.LoadDocument(fullPath);
            return true;
        }
        catch (SafeXmlException exception)
        {
            failure = exception.FailureKind;
            return false;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    private static string ProjectFailure(SafeXmlFailureKind? failure) => failure switch
    {
        SafeXmlFailureKind.TooLarge => "SqlProjectInputTooLarge",
        SafeXmlFailureKind.SecurityRejected => "SqlProjectXmlSecurityRejected",
        SafeXmlFailureKind.Malformed => "SqlProjectXmlMalformed",
        _ => "SqlProjectUnavailable"
    };

    private static string LogFailure(SafeXmlFailureKind? failure) => failure switch
    {
        SafeXmlFailureKind.TooLarge => "RefactorLogInputTooLarge",
        SafeXmlFailureKind.SecurityRejected => "RefactorLogXmlSecurityRejected",
        SafeXmlFailureKind.Malformed => "RefactorLogXmlMalformed",
        _ => "RefactorLogUnavailable"
    };

    private static CodeFact Gap(
        ScanManifest manifest,
        string filePath,
        string? projectPath,
        int line,
        string classification,
        params (string Key, string Value)[] extra) =>
        FactFactory.Create(
            manifest,
            FactTypes.AnalysisGap,
            RuleIds.DatabaseSqlProjectRefactorIntentGap,
            EvidenceTiers.Tier4Unknown,
            Span(filePath, line),
            projectPath: projectPath,
            properties: Properties(
                [("classification", classification), ("coverageLabel", "reduced"), ("limitations", Limitation), .. extra]));

    private static EvidenceSpan Span(string filePath, int line) =>
        new(filePath, Math.Max(1, line), Math.Max(1, line), null,
            nameof(SqlProjectRefactorExtractor), ScannerVersions.SqlProjectRefactorExtractor);

    private static string? ChildValue(XElement operation, string localName) =>
        operation.Elements().FirstOrDefault(element => element.Name.LocalName.Equals(localName, StringComparison.Ordinal))?.Value.Trim();

    private static int Line(XObject node) =>
        node is IXmlLineInfo info && info.HasLineInfo() ? Math.Max(1, info.LineNumber) : 1;

    private static SortedDictionary<string, string> Properties(params (string Key, string Value)[] values) =>
        new(values.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal), StringComparer.Ordinal);
}
