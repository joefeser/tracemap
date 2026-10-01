using System.Security.Cryptography;
using System.Text.Json;
using TraceMap.Core;

namespace TraceMap.Cli;

/// <summary>Generate private native configuration; never attest, build, scan or execute the website.</summary>
public static class WebFormsWizardNative
{
    public const string RuleId = "workflow.webforms.wizard-native-config.v1";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static async Task<string> ConfigureAsync(WebFormsWizardStore store, string id, CancellationToken cancellationToken = default)
    {
        var project = store.ReadProject(id);
        if (project.Step != "configuration" || project.Native is not null) throw Fail("CONFIGURATION_STEP_REQUIRED");
        WebFormsWizardBuild.ValidateRetained(project);
        WebFormsWizardPublication.ValidateRetained(project);
        var retainedInputs = project.Inputs ?? throw Fail("INPUT_SNAPSHOT_MISSING");
        store.VerifyUnchanged();
        var target = WebFormsWizardTarget.Inspect(project.InputPath, project.WebRoot);
        var git = GitMetadataProvider.Detect(project.WebRoot);
        if (git.CommitSha.Length != 40 || !git.CommitSha.All(Uri.IsHexDigit) || string.IsNullOrWhiteSpace(git.RemoteUrl))
            throw Fail("COMMITTED_SOURCE_WITH_REMOTE_REQUIRED");
        var projectFolder = Path.GetDirectoryName(store.ProjectPath(id))!;
        var configPath = Path.Combine(projectFolder, "native.config.json");
        if (File.Exists(configPath) || Directory.Exists(configPath)) configPath = Path.Combine(projectFolder, "native-" + Guid.NewGuid().ToString("N") + ".config.json");
        var stagedRoot = Path.Combine(projectFolder, "publication-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stagedRoot);
        var stagedInputs = new List<WebFormsWizardInputSnapshot>();
        var primary = new List<string>();
        var dependencies = new List<string>();
        var maps = new List<string>();
        foreach (var source in retainedInputs.Where(item => item.Role is "primary-assembly" or "dependency-assembly" or "publication-metadata"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relative = source.Role == "dependency-assembly" ? "dependencies/" + source.Sha256 + "/" + Path.GetFileName(source.Path)
                : Relative(project.PublishedRoot!, source.Path);
            var destination = WebFormsReviewPreflightCommand.Child(stagedRoot, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            await CopyVerified(source, destination, cancellationToken);
            stagedInputs.Add(source with { Path = destination });
            if (source.Role == "primary-assembly") primary.Add(relative);
            else if (source.Role == "dependency-assembly") dependencies.Add(relative);
            else if (Path.GetExtension(relative).Equals(".compiled", StringComparison.OrdinalIgnoreCase)) maps.Add(relative);
        }
        var sourcePaths = retainedInputs.Where(item => item.Role == "source-input" && Within(project.WebRoot, item.Path))
            .Select(item => Relative(project.WebRoot, item.Path)).Order(StringComparer.Ordinal).ToArray();
        var config = new WebFormsReviewConfig(WebFormsReviewPreflightCommand.ConfigSchema, "fresh", project.WebRoot,
            git.CommitSha, target.SelectedProject is null ? "projectless" : "projects", null,
            target.SelectedProject is null ? [] : [Relative(project.WebRoot, target.SelectedProject)], ["."], project.FormsMode,
            project.FormsMode == "selected" ? project.Forms : [], stagedRoot, primary.ToArray(), dependencies.ToArray(),
            [], [], maps.ToArray(), null, new WebFormsReviewBudgets { MaxPublishInputFiles = 20_480 },
            PublishSourceRelativePaths: sourcePaths);
        WebFormsReviewPreflightCommand.ValidateConfig(config);
        var generator = Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(typeof(WebFormsWizardNative).Assembly.Location, cancellationToken)));
        var bounded = Hash(JsonSerializer.SerializeToUtf8Bytes(new { project, stagedInputs, configuration = config }, Json));
        config = config with { WizardProvenance = new(RuleId, generator, bounded) };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(config, Json);
        if (bytes.Length > 1_048_576) throw Fail("NATIVE_CONFIG_LIMIT");
        WebFormsWizardPublication.ValidateRetained(project);
        store.VerifyUnchanged();
        // A crash leaves this exact attempt for explicit repair; never overwrite a previous config.
        var pending = configPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        await using (var stream = new FileStream(pending, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        { await stream.WriteAsync(bytes, cancellationToken); stream.Flush(true); }
        File.Move(pending, configPath, overwrite: false);
        _ = await WebFormsReviewPreflightCommand.BuildAsync(configPath,
            Path.Combine(store.DirectoryPath, "runs", id + "-validation-" + Guid.NewGuid().ToString("N")), cancellationToken);
        var reference = new WebFormsWizardNativeReference(Path.GetFileName(configPath), Hash(bytes), git.CommitSha, stagedInputs.ToArray());
        store.SaveProject(project with { Native = reference, Step = "ready" });
        return configPath;
    }

    public static async Task<string> ValidateAsync(WebFormsWizardStore store, string id, CancellationToken cancellationToken = default)
    {
        var project = store.ReadProject(id);
        var reference = project.Native ?? throw Fail("NATIVE_CONFIG_MISSING");
        if (!System.Text.RegularExpressions.Regex.IsMatch(reference.RelativePath, @"\Anative(?:-[0-9a-f]{32})?\.config\.json\z")) throw Fail("NATIVE_CONFIG_PATH_INVALID");
        WebFormsWizardPublication.ValidateRetained(project);
        WebFormsWizardBuild.ValidateRetained(project);
        var git = GitMetadataProvider.Detect(project.WebRoot);
        if (git.CommitSha != reference.SourceCommitSha) throw Fail("SOURCE_COMMIT_CHANGED");
        var projectFolder = Path.GetDirectoryName(store.ProjectPath(id))!;
        var configPath = WebFormsReviewPreflightCommand.Child(projectFolder, reference.RelativePath);
        var current = await WebFormsReviewPreflightCommand.HashAsync("wizard-native-config", configPath, 1_048_576, cancellationToken);
        if (current.Sha256 != reference.Sha256) throw Fail("NATIVE_CONFIG_CHANGED");
        foreach (var retained in reference.Inputs)
        {
            if (!Within(projectFolder, retained.Path)) throw Fail("STAGED_INPUT_OUTSIDE_PROJECT");
            var path = WebFormsReviewPreflightCommand.Child(projectFolder, Relative(projectFolder, retained.Path));
            var actual = await WebFormsReviewPreflightCommand.HashAsync(retained.Role, path, 67_108_864, cancellationToken);
            if (actual.Bytes != retained.Bytes || actual.Sha256 != retained.Sha256) throw Fail("STAGED_INPUT_CHANGED");
        }
        return configPath;
    }

    private static async Task CopyVerified(WebFormsWizardInputSnapshot source, string destination, CancellationToken token)
    {
        await using var input = new FileStream(source.Path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (input.Length != source.Bytes) throw Fail("INPUT_CHANGED");
        await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[65_536];
        long copied = 0;
        int count;
        while ((count = await input.ReadAsync(buffer, token)) != 0)
        {
            copied += count;
            if (copied > source.Bytes) throw Fail("INPUT_CHANGED");
            digest.AppendData(buffer, 0, count);
            await output.WriteAsync(buffer.AsMemory(0, count), token);
        }
        if (copied != source.Bytes || Convert.ToHexStringLower(digest.GetHashAndReset()) != source.Sha256) throw Fail("INPUT_CHANGED");
        output.Flush(true);
    }
    private static string Relative(string root, string path)
    {
        if (!Within(root, path)) throw Fail("INPUT_OUTSIDE_ROOT");
        return Path.GetRelativePath(root, path).Replace('\\', '/');
    }
    private static bool Within(string root, string path)
    {
        var relative = Path.GetRelativePath(root, path);
        return !Path.IsPathRooted(relative) && relative != ".." && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static InvalidOperationException Fail(string code) => new("WEBFORMS_WIZARD_" + code);
}
