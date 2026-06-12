using System.Text.RegularExpressions;

namespace TraceMap.Core;

public static class SqlTextDetector
{
    private static readonly Regex SqlPattern = new(
        @"\b(select\s+.+\s+from|insert\s+into|update\s+.+\s+set|delete\s+from|merge\s+into|create\s+(table|view|procedure|proc|function)|alter\s+(table|view|procedure|proc|function))\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100));

    public static bool IsSqlLike(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        try
        {
            return SqlPattern.IsMatch(text);
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }
}
