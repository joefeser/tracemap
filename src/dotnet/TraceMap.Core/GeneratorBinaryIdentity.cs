using System.Reflection;

namespace TraceMap.Core;

/// <summary>Exact generator assembly bytes, or the containing single-file application bundle.</summary>
public static class GeneratorBinaryIdentity
{
    public static byte[] ReadBytes(Assembly assembly) => ReadBytes(assembly.Location,
        Assembly.GetEntryAssembly()?.Location, Environment.ProcessPath);

    internal static byte[] ReadBytes(string assemblyLocation, string? entryAssemblyLocation, string? processPath)
    {
        if (!string.IsNullOrEmpty(assemblyLocation)) return File.ReadAllBytes(assemblyLocation);
        // A normal dotnet host must never stand in for an unavailable generator.
        // Bundled applications have no entry-assembly location and execute the bundle.
        if (entryAssemblyLocation == "" && !string.IsNullOrEmpty(processPath)
            && !Path.GetFileNameWithoutExtension(processPath).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            return File.ReadAllBytes(processPath);
        throw new InvalidOperationException("Exact generator assembly or application bundle bytes are unavailable.");
    }
}
