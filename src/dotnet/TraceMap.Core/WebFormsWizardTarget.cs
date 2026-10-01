using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace TraceMap.Core;

public sealed record WebFormsWizardTargetInfo(string InputPath, string WebRoot, string ProjectMode,
    string? SelectedProject, string BuildTool, string[] DeclaredFrameworks, bool RequiresWindows,
    string[] Limitations);

/// <summary>
/// Bounded static target classification, never MSBuild evaluation. Imports, conditions and
/// project references are not executed. Tool recommendations are not installation/build proof.
/// </summary>
public static class WebFormsWizardTarget
{
    public const string RuleId = "workflow.webforms.wizard-target.v1";
    private const int MaxBytes = 1_048_576;
    private const string WebsiteGuid = "E24C65DC-7377-472B-9ABA-BC803B73C61A";
    private static readonly Regex SolutionEntry = new(
        "^Project\\(\"\\{(?<type>[A-Fa-f0-9-]+)\\}\"\\)\\s*=\\s*\"[^\"]*\",\\s*\"(?<path>[^\"]+)\",\\s*\"\\{[A-Fa-f0-9-]+\\}\"\\s*$",
        RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    public static WebFormsWizardTargetInfo Inspect(string input, string? selectedWebRoot = null)
    {
        var path = WebFormsWizardStore.Physical(input);
        if (Directory.Exists(path))
        {
            if (selectedWebRoot is not null && !PathEquals(WebFormsWizardStore.Physical(selectedWebRoot), path))
                throw Fail("WEB_ROOT_MISMATCH");
            var files = Directory.EnumerateFiles(path).Take(WebFormsWizardForms.MaxInventoryEntries + 1).ToArray();
            if (files.Length > WebFormsWizardForms.MaxInventoryEntries) throw Fail("INPUT_LIMIT");
            if (files.Any(IsProject))
                throw Fail("SELECT_PROJECT_FILE");
            ValidateWebRoot(path);
            return new(path, path, "projectless", null, "manual-aspnet-compiler", [], true,
                ["Static website structure only; supply separately compiled publication. No compilation or runtime execution performed."]);
        }
        if (!File.Exists(path)) throw Fail("INPUT_MISSING");
        if (IsProject(path)) return Project(path, path, selectedWebRoot, "project");
        if (!Path.GetExtension(path).Equals(".sln", StringComparison.OrdinalIgnoreCase)) throw Fail("INPUT_UNSUPPORTED");
        if (selectedWebRoot is null) throw Fail("SOLUTION_WEB_ROOT_REQUIRED");
        var root = WebFormsWizardStore.Physical(selectedWebRoot);
        var solutionRoot = Path.GetDirectoryName(path)!;
        if (!Within(solutionRoot, root)) throw Fail("WEB_ROOT_OUTSIDE_SOLUTION");
        var text = Read(path);
        if (!text.TrimStart('\uFEFF', '\r', '\n').StartsWith("Microsoft Visual Studio Solution File, Format Version ", StringComparison.Ordinal))
            throw Fail("SOLUTION_INVALID");
        var matches = new List<(string Path, bool Website)>();
        foreach (var line in text.Replace("\r", "", StringComparison.Ordinal).Split('\n'))
        {
            var match = SolutionEntry.Match(line.Trim());
            if (!match.Success) continue;
            var website = match.Groups["type"].Value.Equals(WebsiteGuid, StringComparison.OrdinalIgnoreCase);
            var relative = match.Groups["path"].Value.Replace('\\', '/');
            if (!website && !IsProject(relative)) continue;
            // URL-based sites and foreign drive paths require a local solution/publication.
            if (relative.Contains(':') || Path.IsPathRooted(relative)) continue;
            var candidate = WebFormsWizardStore.Physical(Path.Combine(solutionRoot, relative));
            if (!Within(solutionRoot, candidate)) continue;
            var candidateRoot = website ? candidate : Path.GetDirectoryName(candidate)!;
            if (PathEquals(root, candidateRoot)) matches.Add((candidate, website));
        }
        if (matches.Count != 1) throw Fail(matches.Count == 0 ? "WEB_ROOT_NOT_IN_SOLUTION" : "WEB_ROOT_AMBIGUOUS");
        var selected = matches[0];
        if (!selected.Website) return Project(path, selected.Path, root, "solution");
        ValidateWebRoot(root);
        return new(path, root, "projectless", null, "manual-aspnet-compiler", [], true,
            ["Selected local website entry only; other solution projects are not registered. Supply separately compiled publication."]);
    }

    private static WebFormsWizardTargetInfo Project(string input, string project, string? requestedRoot, string mode)
    {
        if (!File.Exists(project)) throw Fail("PROJECT_MISSING");
        var root = Path.GetDirectoryName(project)!;
        if (requestedRoot is not null && !PathEquals(WebFormsWizardStore.Physical(requestedRoot), root)) throw Fail("WEB_ROOT_MISMATCH");
        using var reader = XmlReader.Create(new StringReader(Read(project)), new XmlReaderSettings
        { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaxBytes });
        var document = XDocument.Load(reader);
        if (document.Root?.Name.LocalName != "Project") throw Fail("PROJECT_INVALID");
        var frameworks = document.Descendants().Where(element => element.Name.LocalName is "TargetFramework" or "TargetFrameworks" or "TargetFrameworkVersion")
            .SelectMany(element => element.Value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var sdk = document.Root.Attribute("Sdk") is not null || document.Root.Elements().Any(element => element.Name.LocalName == "Sdk");
        var framework = frameworks.Any(value => Regex.IsMatch(value, "\\A(?:v[1-4]\\.|net[1-4][0-9]{1,2}(?:\\z|-))", RegexOptions.CultureInvariant));
        var unresolved = frameworks.Length == 0 || frameworks.Any(value => value.Contains('$') || value.Contains('@'));
        ValidateWebRoot(root);
        return new(input, root, mode, project, !sdk || framework ? "windows-msbuild" : unresolved ? "unknown" : "dotnet",
            frameworks, !sdk || framework,
            ["Framework declarations are unevaluated: imports and conditional properties may change the target. Verify the toolchain and actual build result.",
             "ProjectReference build dependencies are not independently registered. No customer code or MSBuild targets were executed."]);
    }

    private static void ValidateWebRoot(string root)
    {
        if (!Directory.Exists(root)) throw Fail("WEB_ROOT_MISSING");
        if (!File.Exists(Path.Combine(root, "web.config")) && !File.Exists(Path.Combine(root, "Web.config"))) throw Fail("WEB_CONFIG_MISSING");
        _ = WebFormsWizardForms.Discover(root);
    }

    private static bool IsProject(string path) => Path.GetExtension(path).ToLowerInvariant() is ".csproj" or ".vbproj";
    private static bool PathEquals(string left, string right) => string.Equals(left, right, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    private static bool Within(string root, string path)
    {
        var relative = Path.GetRelativePath(root, path);
        return !Path.IsPathRooted(relative) && relative != ".." && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }
    private static string Read(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length is <= 0 or > MaxBytes) throw Fail("INPUT_LIMIT");
        var bytes = new byte[(int)stream.Length];
        stream.ReadExactly(bytes);
        if (stream.ReadByte() != -1) throw Fail("INPUT_CHANGED");
        using var reader = new StreamReader(new MemoryStream(bytes));
        var text = reader.ReadToEnd();
        if (text.Length > MaxBytes) throw Fail("INPUT_LIMIT");
        return text;
    }
    private static InvalidOperationException Fail(string code) => new("WEBFORMS_WIZARD_" + code);
}
