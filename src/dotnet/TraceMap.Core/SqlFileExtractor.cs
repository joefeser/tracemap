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

                facts.Add(FactFactory.Create(
                    manifest,
                    FactTypes.SqlTextUsed,
                    RuleIds.DatabaseSqlText,
                    EvidenceTiers.Tier3SyntaxOrTextual,
                    new EvidenceSpan(file.RelativePath, 1, CountLines(text), FactFactory.Hash(text, 32), "SqlFileExtractor", ScannerVersions.SqlTextExtractor),
                    targetSymbol: Path.GetFileName(file.RelativePath),
                    properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["path"] = file.RelativePath,
                        ["textHash"] = FactFactory.Hash(text, 32),
                        ["textLength"] = text.Length.ToString()
                    }));
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
