using Microsoft.Build.Locator;

namespace TraceMap.Core;

internal static class MsBuildRuntimeRegistration
{
    private static readonly object RegistrationLock = new();

    public static bool TryRegister(out string? error)
    {
        lock (RegistrationLock)
        {
            if (MSBuildLocator.IsRegistered)
            {
                error = null;
                return true;
            }

            try
            {
                MSBuildLocator.RegisterDefaults();
                error = null;
                return true;
            }
            catch (Exception exception) when (exception is InvalidOperationException or FileNotFoundException)
            {
                if (MSBuildLocator.IsRegistered)
                {
                    error = null;
                    return true;
                }

                error = exception.Message;
                return false;
            }
        }
    }
}
