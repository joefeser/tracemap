using System.Text.RegularExpressions;

namespace TraceMap.Core;

public sealed record NormalizedEndpointRoute(
    string PathTemplate,
    string PathKey,
    IReadOnlyList<string> ParameterNames,
    IReadOnlyList<string> OptionalParameterNames,
    IReadOnlyList<string> RouteConstraints,
    IReadOnlyList<string> QueryParameterNames,
    bool HasQueryParameters,
    bool HasCatchAll,
    string StaticMatchQuality);

public static partial class EndpointRouteNormalizer
{
    public static NormalizedEndpointRoute Normalize(
        string routeTemplate,
        string? basePathPrefix = null,
        IReadOnlyDictionary<string, string>? tokens = null)
    {
        var value = (routeTemplate ?? string.Empty).Trim();
        value = StripFragment(value, out _);
        value = SplitQuery(value, out var queryNames);
        value = StripSchemeHost(value);
        value = ReplaceTokens(value, tokens);
        if (!string.IsNullOrWhiteSpace(basePathPrefix) && !StartsWithPathPrefix(value, basePathPrefix))
        {
            value = CombinePath(basePathPrefix, value);
        }

        value = NormalizeSlashes(value);
        var parameters = new SortedSet<string>(StringComparer.Ordinal);
        var optional = new SortedSet<string>(StringComparer.Ordinal);
        var constraints = new SortedSet<string>(StringComparer.Ordinal);
        var hasCatchAll = false;
        var templateSegments = new List<string>();
        var keySegments = new List<string>();

        foreach (var rawSegment in value.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            var segment = SafeUnescapeDataString(rawSegment);
            if (TryParseAspNetParameter(segment, out var parameter))
            {
                parameters.Add(parameter.Name);
                if (parameter.Optional)
                {
                    optional.Add(parameter.Name);
                }
                if (!string.IsNullOrWhiteSpace(parameter.Constraint))
                {
                    constraints.Add($"{parameter.Name}:{parameter.Constraint}");
                }
                hasCatchAll |= parameter.CatchAll;
                templateSegments.Add("{" + parameter.Name + (parameter.Optional ? "?" : string.Empty) + "}");
                keySegments.Add(parameter.Optional ? "{?}" : "{}");
            }
            else
            {
                var clientSegment = ClientParameterRegex().Replace(segment, match =>
                {
                    var name = match.Groups["name"].Value;
                    parameters.Add(name);
                    return "{" + name + "}";
                });
                templateSegments.Add(clientSegment);
                keySegments.Add(ParameterNameRegex().Replace(clientSegment, "{}").ToLowerInvariant());
            }
        }

        var pathTemplate = "/" + string.Join("/", templateSegments);
        var pathKey = "/" + string.Join("/", keySegments);
        if (pathTemplate == "/")
        {
            pathKey = "/";
        }

        return new NormalizedEndpointRoute(
            pathTemplate,
            pathKey,
            parameters.ToArray(),
            optional.ToArray(),
            constraints.ToArray(),
            queryNames,
            queryNames.Count > 0,
            hasCatchAll,
            optional.Count > 0 ? "OptionalSegments" : "Exact");
    }

    public static IReadOnlyList<string> ExpandOptionalPathKeys(NormalizedEndpointRoute route)
    {
        const int MaxOptionalSegmentExpansion = 5;
        if (route.OptionalParameterNames.Count == 0)
        {
            return [route.PathKey];
        }

        var segments = route.PathKey.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var optionalIndexes = segments
            .Select((segment, index) => (segment, index))
            .Where(item => item.segment == "{?}")
            .Select(item => item.index)
            .ToArray();
        if (optionalIndexes.Length > MaxOptionalSegmentExpansion)
        {
            return [route.PathKey.Replace("{?}", "{}")];
        }

        var keys = new SortedSet<string>(StringComparer.Ordinal) { route.PathKey.Replace("{?}", "{}") };
        var combinations = 1 << optionalIndexes.Length;
        for (var mask = 0; mask < combinations; mask++)
        {
            var included = segments
                .Where((_, index) => !optionalIndexes.Contains(index) || (mask & (1 << Array.IndexOf(optionalIndexes, index))) != 0)
                .Select(segment => segment == "{?}" ? "{}" : segment);
            keys.Add("/" + string.Join("/", included));
        }

        return keys.ToArray();
    }

    private static string SafeUnescapeDataString(string value)
    {
        try
        {
            return Uri.UnescapeDataString(value);
        }
        catch (UriFormatException)
        {
            return value;
        }
    }

    private static string ReplaceTokens(string value, IReadOnlyDictionary<string, string>? tokens)
    {
        if (tokens is null || tokens.Count == 0)
        {
            return value;
        }

        foreach (var (key, replacement) in tokens.OrderByDescending(item => item.Key.Length))
        {
            value = value.Replace($"[{key}]", replacement, StringComparison.OrdinalIgnoreCase);
        }

        return value;
    }

    private static bool TryParseAspNetParameter(string segment, out RouteParameter parameter)
    {
        parameter = new RouteParameter("", false, false, null);
        var match = AspNetParameterRegex().Match(segment);
        if (!match.Success)
        {
            return false;
        }

        var rawName = match.Groups["name"].Value;
        var catchAll = rawName.StartsWith('*');
        var name = catchAll ? rawName.TrimStart('*') : rawName;
        var constraint = match.Groups["constraint"].Success ? match.Groups["constraint"].Value.TrimEnd('?') : null;
        var optional = match.Groups["optional"].Success || segment.EndsWith("?}", StringComparison.Ordinal);
        parameter = new RouteParameter(name, optional, catchAll, constraint);
        return true;
    }

    private static bool StartsWithPathPrefix(string value, string basePathPrefix)
    {
        var path = NormalizeSlashes(value).TrimEnd('/');
        var prefix = NormalizeSlashes(basePathPrefix).TrimEnd('/');
        return path.Equals(prefix, StringComparison.OrdinalIgnoreCase)
            || path.StartsWith(prefix + "/", StringComparison.OrdinalIgnoreCase);
    }

    private static string CombinePath(string? left, string right)
    {
        if (string.IsNullOrWhiteSpace(left))
        {
            return right;
        }

        if (string.IsNullOrWhiteSpace(right))
        {
            return left;
        }

        return $"{left.TrimEnd('/')}/{right.TrimStart('/')}";
    }

    private static string NormalizeSlashes(string value)
    {
        value = value.Replace('\\', '/').Trim();
        while (value.Contains("//", StringComparison.Ordinal))
        {
            value = value.Replace("//", "/", StringComparison.Ordinal);
        }

        if (value.StartsWith("~/", StringComparison.Ordinal))
        {
            value = value[1..];
        }

        if (!value.StartsWith('/'))
        {
            value = "/" + value;
        }

        return value.Length > 1 ? value.TrimEnd('/') : value;
    }

    private static string StripSchemeHost(string value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            ? string.IsNullOrWhiteSpace(uri.AbsolutePath) ? "/" : uri.AbsolutePath + uri.Query
            : value;
    }

    private static string SplitQuery(string value, out IReadOnlyList<string> queryNames)
    {
        var queryIndex = FindQueryStart(value);
        if (queryIndex < 0)
        {
            queryNames = [];
            return value;
        }

        var query = value[(queryIndex + 1)..];
        queryNames = query.Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => part.Split('=')[0])
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(Uri.UnescapeDataString)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        return value[..queryIndex];
    }

    private static int FindQueryStart(string value)
    {
        var braceDepth = 0;
        for (var index = 0; index < value.Length; index++)
        {
            if (value[index] == '{')
            {
                braceDepth++;
            }
            else if (value[index] == '}' && braceDepth > 0)
            {
                braceDepth--;
            }
            else if (value[index] == '?' && braceDepth == 0)
            {
                return index;
            }
        }

        return -1;
    }

    private static string StripFragment(string value, out string? fragment)
    {
        var fragmentIndex = value.IndexOf('#', StringComparison.Ordinal);
        if (fragmentIndex < 0)
        {
            fragment = null;
            return value;
        }

        fragment = value[(fragmentIndex + 1)..];
        return value[..fragmentIndex];
    }

    private sealed record RouteParameter(string Name, bool Optional, bool CatchAll, string? Constraint);

    [GeneratedRegex(@"^\{(?<name>\*?[A-Za-z_][A-Za-z0-9_]*)(:(?<constraint>[^}?]+))?(?<optional>\?)?\}$")]
    private static partial Regex AspNetParameterRegex();

    [GeneratedRegex(@"\{(?<name>[A-Za-z_][A-Za-z0-9_]*)\}")]
    private static partial Regex ClientParameterRegex();

    [GeneratedRegex(@"\{[A-Za-z_][A-Za-z0-9_]*\??\}")]
    private static partial Regex ParameterNameRegex();
}
