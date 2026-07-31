using System.Diagnostics;

namespace TraceMap.Access;

internal static class AccessGitIsolation
{
    internal const string EmptyGlobalConfigFileName = ".tracemap-empty-global-config";

    internal static void Configure(ProcessStartInfo start, string repository)
    {
        foreach (var key in start.Environment.Keys.ToArray())
        {
            if (key.Equals("GIT_CONFIG_COUNT", StringComparison.OrdinalIgnoreCase)
                || key.Equals("GIT_CONFIG_PARAMETERS", StringComparison.OrdinalIgnoreCase)
                || key.StartsWith("GIT_CONFIG_KEY_", StringComparison.OrdinalIgnoreCase)
                || key.StartsWith("GIT_CONFIG_VALUE_", StringComparison.OrdinalIgnoreCase))
            {
                start.Environment.Remove(key);
            }
        }

        start.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
        start.Environment["GIT_CONFIG_GLOBAL"] = Path.Combine(repository, EmptyGlobalConfigFileName);
        start.Environment["GIT_ATTR_NOSYSTEM"] = "1";
        start.Environment["GIT_TERMINAL_PROMPT"] = "0";
        start.Environment["GIT_OPTIONAL_LOCKS"] = "0";
    }
}
