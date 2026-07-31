using System.Diagnostics;

namespace TraceMap.Access;

internal static class AccessGitIsolation
{
    internal const string EmptyGlobalConfigFileName = ".tracemap-empty-global-config";

    internal static void Configure(ProcessStartInfo start, string repository)
    {
        foreach (var key in start.Environment.Keys.ToArray())
        {
            // Git honors many process-level routing variables before it considers
            // the working directory. Start from no inherited Git state, then add
            // only the settings controlled by this snapshot operation.
            if (key.StartsWith("GIT_", StringComparison.OrdinalIgnoreCase))
                start.Environment.Remove(key);
        }

        start.Environment["GIT_CONFIG_NOSYSTEM"] = "1";
        start.Environment["GIT_CONFIG_GLOBAL"] = Path.Combine(repository, EmptyGlobalConfigFileName);
        start.Environment["GIT_ATTR_NOSYSTEM"] = "1";
        start.Environment["GIT_TERMINAL_PROMPT"] = "0";
        start.Environment["GIT_OPTIONAL_LOCKS"] = "0";
    }
}
