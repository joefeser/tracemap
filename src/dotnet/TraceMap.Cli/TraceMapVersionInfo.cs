using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using TraceMap.Core;

namespace TraceMap.Cli;

public sealed record TraceMapVersionResult(
    string SchemaVersion,
    string ToolVersion,
    string ScannerVersion,
    string DistributionKind,
    string TargetFramework,
    TraceMapVersionHost Host,
    TraceMapVersionReadiness Readiness,
    IReadOnlyList<string> Limitations);

public sealed record TraceMapVersionHost(
    string OperatingSystem,
    string Architecture,
    string RuntimeVersion);

public sealed record TraceMapVersionReadiness(
    string Outcome,
    string NextAction,
    TraceMapCapability Git,
    TraceMapCapability MsBuild);

public sealed record TraceMapCapability(string Status);

public static class TraceMapVersionInfo
{
    public const string SchemaVersion = "tracemap-version.v1";

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static TraceMapVersionResult Create(Func<string, bool>? capabilityProbe = null)
    {
        capabilityProbe ??= ProbeCapability;
        var assembly = typeof(TraceMapVersionInfo).Assembly;
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;
        var toolVersion = string.IsNullOrWhiteSpace(informationalVersion)
            ? assembly.GetName().Version?.ToString() ?? "unknown"
            : informationalVersion;
        var distributionKind = assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => string.Equals(attribute.Key, "TraceMapDistributionKind", StringComparison.Ordinal))?
            .Value;
        var targetFramework = assembly
            .GetCustomAttribute<System.Runtime.Versioning.TargetFrameworkAttribute>()?
            .FrameworkName;

        var gitAvailable = capabilityProbe("git");
        var msBuildAvailable = capabilityProbe("dotnet-msbuild");
        var outcome = !gitAvailable ? "unavailable" : msBuildAvailable ? "ready" : "reduced";
        var nextAction = !gitAvailable
            ? "install-git"
            : msBuildAvailable
                ? "none"
                : "install-dotnet-sdk-or-use-reduced-analysis";

        return new TraceMapVersionResult(
            SchemaVersion,
            toolVersion,
            TraceMapDiagnostics.ToolVersion,
            string.IsNullOrWhiteSpace(distributionKind) ? "unknown" : distributionKind,
            string.IsNullOrWhiteSpace(targetFramework) ? "unknown" : targetFramework,
            new TraceMapVersionHost(
                OperatingSystemName(),
                RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant(),
                Environment.Version.ToString()),
            new TraceMapVersionReadiness(
                outcome,
                nextAction,
                new TraceMapCapability(gitAvailable ? "available" : "unavailable"),
                new TraceMapCapability(msBuildAvailable ? "available" : "unavailable")),
            [
                "Readiness observes local capability availability only; it does not inspect a repository or prove scan coverage.",
                "Version and package hashes do not establish publisher identity or authority."
            ]);
    }

    private static string OperatingSystemName()
    {
        if (OperatingSystem.IsWindows()) return "windows";
        if (OperatingSystem.IsMacOS()) return "macos";
        if (OperatingSystem.IsLinux()) return "linux";
        return "unknown";
    }

    private static bool ProbeCapability(string capability)
    {
        var (fileName, arguments) = capability switch
        {
            "git" => ("git", "--version"),
            "dotnet-msbuild" => ("dotnet", "msbuild -version -nologo"),
            _ => throw new ArgumentOutOfRangeException(nameof(capability), capability, "Unknown capability probe.")
        };

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            if (string.Equals(fileName, "dotnet", StringComparison.Ordinal))
            {
                startInfo.Environment["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1";
                startInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
            }

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return false;
            }

            if (!process.WaitForExit(5_000))
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit();
                return false;
            }

            return process.ExitCode == 0;
        }
        catch (Exception exception) when (exception is System.ComponentModel.Win32Exception
            or InvalidOperationException
            or NotSupportedException)
        {
            return false;
        }
    }
}
