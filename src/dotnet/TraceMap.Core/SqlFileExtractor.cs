namespace TraceMap.Core;

public static class SqlFileExtractor
{
    public static IReadOnlyList<CodeFact> Extract(string repoPath, ScanManifest manifest, IEnumerable<FileInventoryItem> inventory)
    {
        var facts = new List<CodeFact>();
        foreach (var file in inventory
            .Where(item => item.Kind == "Sql")
            .OrderBy(item => item.RelativePath, StringComparer.Ordinal))
        {
            var fullPath = Path.Combine(repoPath, file.RelativePath);
            try
            {
                var text = File.ReadAllText(fullPath);
                if (!SqlTextDetector.IsSqlLike(text))
                {
                    continue;
                }

                var lineCount = CountLines(text);
                var textHash = FactFactory.Hash(text, 32);
                var operationName = SqlShapeExtractor.OperationName(text);
                var textProperties = new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["path"] = file.RelativePath,
                    ["textHash"] = textHash,
                    ["textLength"] = text.Length.ToString(),
                    ["sqlSourceKind"] = "sql-file"
                };
                if (!string.IsNullOrWhiteSpace(operationName))
                {
                    textProperties["operationName"] = operationName;
                }

                var span = new EvidenceSpan(file.RelativePath, 1, lineCount, textHash, "SqlFileExtractor", ScannerVersions.SqlTextExtractor);
                facts.Add(FactFactory.Create(
                    manifest,
                    FactTypes.SqlTextUsed,
                    RuleIds.DatabaseSqlText,
                    EvidenceTiers.Tier2Structural,
                    span,
                    targetSymbol: Path.GetFileName(file.RelativePath),
                    properties: textProperties));

                var shapeProperties = SqlShapeExtractor.QueryShapeProperties(text, "sql-file");
                var target = shapeProperties.GetValueOrDefault("tableName") ?? file.RelativePath;
                shapeProperties["targetSymbol"] = target;
                facts.Add(FactFactory.Create(
                    manifest,
                    FactTypes.QueryPatternDetected,
                    RuleIds.DatabaseSqlShape,
                    EvidenceTiers.Tier2Structural,
                    new EvidenceSpan(file.RelativePath, 1, lineCount, textHash, "SqlFileExtractor", ScannerVersions.SqlShapeExtractor),
                    targetSymbol: target,
                    contractElement: target,
                    properties: shapeProperties));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                facts.Add(FactFactory.Create(
                    manifest,
                    FactTypes.AnalysisGap,
                    RuleIds.DatabaseSqlText,
                    EvidenceTiers.Tier4Unknown,
                    new EvidenceSpan(file.RelativePath, 1, 1, null, "SqlFileExtractor", ScannerVersions.SqlTextExtractor),
                    properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["message"] = $"Unable to read SQL file: {ex.Message}"
                    }));
            }
        }

        return facts;
    }

    private static int CountLines(string text)
    {
        if (text.Length == 0)
        {
            return 1;
        }

        var count = 1;
        foreach (var c in text)
        {
            if (c == '\n')
            {
                count++;
            }
        }

        return count;
    }
}
