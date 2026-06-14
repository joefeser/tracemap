namespace TraceMap.Core;

public static class SqlTextDetector
{
    public static bool IsSqlLike(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return SqlShapeExtractor.IsSqlLike(text);
    }
}
