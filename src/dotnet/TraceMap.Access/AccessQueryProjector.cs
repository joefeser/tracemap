using System.Text;
using System.Text.RegularExpressions;

namespace TraceMap.Access;

public static partial class AccessQueryProjector
{
    public static (IReadOnlyList<AccessQueryDependencyProjection> Dependencies, string Coverage, bool UnsupportedShape) ProjectDependencies(
        string sql,
        IReadOnlyDictionary<string, IReadOnlyList<(string StableKey, string Kind)>> knownObjects)
    {
        var masked = MaskLiteralsAndComments(sql);
        var dependencies = new SortedDictionary<string, AccessQueryDependencyProjection>(StringComparer.Ordinal);
        var ambiguous = false;

        foreach (Match match in ReferencePattern().Matches(masked))
        {
            var name = match.Groups[1].Success ? match.Groups[1].Value.Trim() : match.Groups[2].Value.Trim();
            if (!knownObjects.TryGetValue(name, out var candidates) || candidates.Count == 0) continue;
            if (candidates.Count != 1)
            {
                ambiguous = true;
                continue;
            }

            var candidate = candidates[0];
            dependencies[candidate.StableKey] = new AccessQueryDependencyProjection(candidate.StableKey, candidate.Kind, "direct-static-reference");
        }

        var unsupported = UnsupportedPattern().IsMatch(masked);
        var coverage = ambiguous || unsupported ? "partial" : "complete";
        return (dependencies.Values.ToArray(), coverage, unsupported || ambiguous);
    }

    public static string MaskLiteralsAndComments(string sql)
    {
        var builder = new StringBuilder(sql.Length);
        var quote = '\0';
        for (var index = 0; index < sql.Length; index++)
        {
            var current = sql[index];
            if (quote != '\0')
            {
                builder.Append(char.IsWhiteSpace(current) ? current : ' ');
                if (current == quote)
                {
                    if (index + 1 < sql.Length && sql[index + 1] == quote)
                    {
                        builder.Append(' ');
                        index++;
                    }
                    else quote = '\0';
                }
                continue;
            }

            if (current is '\'' or '"')
            {
                quote = current;
                // Preserve only the fact that a literal occupied this position. This lets
                // unsupported external IN clauses be recognized without retaining content.
                builder.Append('#');
                continue;
            }

            if (current == '-' && index + 1 < sql.Length && sql[index + 1] == '-')
            {
                while (index < sql.Length && sql[index] is not ('\r' or '\n'))
                {
                    builder.Append(' ');
                    index++;
                }
                if (index < sql.Length) builder.Append(sql[index]);
                continue;
            }

            builder.Append(current);
        }
        return builder.ToString();
    }

    [GeneratedRegex(@"(?ix)\b(?:from|join|update|into|table)\s+(?:\[([^\]]+)\]|([A-Za-z_][A-Za-z0-9_.$]*))", RegexOptions.CultureInvariant)]
    private static partial Regex ReferencePattern();

    [GeneratedRegex(@"(?ix)\b(?:transform\b|union\b|in\s+\#|parameters\s+[^;]+\s+(?:text|long|short|datetime)\b)", RegexOptions.CultureInvariant)]
    private static partial Regex UnsupportedPattern();
}
