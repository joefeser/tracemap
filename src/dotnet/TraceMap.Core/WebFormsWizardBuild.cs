using System.Security.Cryptography;
using System.Text;

namespace TraceMap.Core;

public sealed record WebFormsWizardBuildPlan(string Tool, string ToolSha256, string WorkingDirectory,
    string InputPath, string InputSha256, string[] VersionArguments, string[] BuildArguments,
    string? SelectedProjectPath = null, string? SelectedProjectSha256 = null);
public sealed record WebFormsWizardProcessResult(int ExitCode, string StandardOutput, string StandardError,
    string? StandardOutputSha256 = null, string? StandardErrorSha256 = null);
public sealed record WebFormsWizardBuildEvidence(string RuleId, string Tool, string ToolSha256,
    string InputPath, string InputSha256, string VersionSha256, string OutputSha256, int ExitCode,
    string? SelectedProjectPath = null, string? SelectedProjectSha256 = null);
public delegate Task<WebFormsWizardProcessResult> WebFormsWizardProcessRunner(string tool, string directory,
    IReadOnlyList<string> arguments, CancellationToken cancellationToken);

/// <summary>Explicit build consent boundary. A successful command is not source-to-binary provenance.</summary>
public static class WebFormsWizardBuild
{
    public const string RuleId = "workflow.webforms.wizard-build.v1";

    public static WebFormsWizardBuildPlan Plan(WebFormsWizardProject project, string executable)
    {
        var target = WebFormsWizardTarget.Inspect(project.InputPath, project.WebRoot);
        if (target.ProjectMode == "projectless") throw Fail("MANUAL_PUBLICATION_REQUIRED");
        if (target.BuildTool == "unknown") throw Fail("TOOLCHAIN_UNRESOLVED");
        if (target.RequiresWindows && !OperatingSystem.IsWindows()) throw Fail("WINDOWS_BUILD_REQUIRED");
        var tool = WebFormsWizardStore.Physical(executable);
        var name = Path.GetFileNameWithoutExtension(tool);
        if (!name.Equals(target.BuildTool == "dotnet" ? "dotnet" : "MSBuild", StringComparison.OrdinalIgnoreCase)) throw Fail("TOOLCHAIN_MISMATCH");
        var toolHash = HashFile(tool);
        var input = target.InputPath;
        var selected = target.SelectedProject ?? throw Fail("BUILD_PROJECT_REQUIRED");
        return new(tool, toolHash, Path.GetDirectoryName(selected)!, input, HashFile(input),
            target.BuildTool == "dotnet" ? ["--version"] : ["-version", "-nologo"],
            target.BuildTool == "dotnet" ? ["build", selected, "--nologo", "-v:minimal"] : [selected, "/nologo", "/t:Build", "/verbosity:minimal"],
            selected, HashFile(selected));
    }

    public static async Task<bool> ExecuteAsync(WebFormsWizardStore store, string id, WebFormsWizardBuildPlan preview,
        bool confirmed, WebFormsWizardProcessRunner runner, CancellationToken cancellationToken = default)
    {
        if (!confirmed) return false;
        var project = store.ReadProject(id);
        if (project.Step != "build") throw Fail("BUILD_STEP_REQUIRED");
        store.VerifyUnchanged();
        var plan = Plan(project, preview.Tool);
        if (plan.ToolSha256 != preview.ToolSha256 || plan.InputSha256 != preview.InputSha256 ||
            plan.InputPath != preview.InputPath || plan.WorkingDirectory != preview.WorkingDirectory ||
            plan.SelectedProjectPath != preview.SelectedProjectPath || plan.SelectedProjectSha256 != preview.SelectedProjectSha256 ||
            !plan.VersionArguments.SequenceEqual(preview.VersionArguments) || !plan.BuildArguments.SequenceEqual(preview.BuildArguments)) throw Fail("BUILD_PLAN_CHANGED");
        cancellationToken.ThrowIfCancellationRequested();
        var version = await runner(plan.Tool, plan.WorkingDirectory, plan.VersionArguments, cancellationToken);
        CheckResult(version);
        if (version.ExitCode != 0 || !System.Text.RegularExpressions.Regex.IsMatch(version.StandardOutput,
                @"(?m)^\d+\.\d+(?:\.\d+)*(?:[-+][A-Za-z0-9.-]+)?\s*$",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1))) throw Fail("TOOLCHAIN_PROBE_FAILED");
        Recheck(plan);
        var result = await runner(plan.Tool, plan.WorkingDirectory, plan.BuildArguments, cancellationToken);
        CheckResult(result);
        if (result.ExitCode != 0) throw Fail("BUILD_FAILED");
        Recheck(plan);
        store.SaveProject(project with { Step = "publication", Build = new(RuleId, plan.Tool, plan.ToolSha256,
            plan.InputPath, plan.InputSha256, ProcessHash(version), ProcessHash(result),
            result.ExitCode, plan.SelectedProjectPath, plan.SelectedProjectSha256) });
        return true;
    }

    public static void ValidateRetained(WebFormsWizardProject project)
    {
        if (project.Build is not { } build) return;
        if (build.RuleId != RuleId || build.ExitCode != 0 || !Path.IsPathFullyQualified(build.InputPath) ||
            !Path.IsPathFullyQualified(build.Tool) || build.InputSha256 != HashFile(build.InputPath) ||
            build.ToolSha256 != HashFile(build.Tool)) throw Fail("BUILD_EVIDENCE_CHANGED");
        if (build.SelectedProjectPath is { } selected && (!Path.IsPathFullyQualified(selected) ||
            build.SelectedProjectSha256 != HashFile(selected))) throw Fail("BUILD_EVIDENCE_CHANGED");
    }

    private static void Recheck(WebFormsWizardBuildPlan plan)
    {
        if (HashFile(plan.Tool) != plan.ToolSha256 || HashFile(plan.InputPath) != plan.InputSha256) throw Fail("BUILD_INPUT_CHANGED");
        if (plan.SelectedProjectPath is { } selected && HashFile(selected) != plan.SelectedProjectSha256) throw Fail("BUILD_INPUT_CHANGED");
    }
    private static void CheckResult(WebFormsWizardProcessResult result)
    {
        if (result.StandardOutput.Length > 65_536 || result.StandardError.Length > 65_536) throw Fail("BUILD_OUTPUT_LIMIT");
    }
    private static string TextHash(string output, string error) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(output + "\0" + error)));
    private static string ProcessHash(WebFormsWizardProcessResult result) => TextHash(
        result.StandardOutputSha256 ?? Convert.ToHexStringLower(SHA256.HashData(Encoding.Unicode.GetBytes(result.StandardOutput))),
        result.StandardErrorSha256 ?? Convert.ToHexStringLower(SHA256.HashData(Encoding.Unicode.GetBytes(result.StandardError))));
    private static string HashFile(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length is <= 0 or > 268_435_456) throw Fail("BUILD_INPUT_LIMIT");
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }
    private static InvalidOperationException Fail(string code) => new("WEBFORMS_WIZARD_" + code);
}
