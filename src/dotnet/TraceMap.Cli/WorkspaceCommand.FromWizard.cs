using System.Text.Json;
using System.Text.RegularExpressions;
using TraceMap.Core;

namespace TraceMap.Cli;

public static partial class WorkspaceCommand
{
    private static readonly string[] FromWizardOptions = ["--wizard-root", "--website-id", "--framework-repo",
        "--framework-target", "--sql-manifest", "--sql-source-root"];

    private static async Task<int> InitializeFromWizardAsync(string root, Dictionary<string, string> options,
        TextWriter output, CancellationToken token)
    {
        foreach (var name in FromWizardOptions.Where(name => name is "--wizard-root" or "--sql-manifest" or "--sql-source-root"))
            if (!options.ContainsKey(name)) throw new ArgumentException("WORKSPACE_SETUP_OPTION_REQUIRED: " + name);
        var destination = Path.Combine(root, "workspace.local.json");
        if (File.Exists(destination) || Directory.Exists(destination))
            throw new ArgumentException("WORKSPACE_CONFIG_EXISTS: existing configuration preserved; choose another workspace root.");
        var ancestor = root;
        while (!Directory.Exists(ancestor)) ancestor = Path.GetDirectoryName(ancestor)
            ?? throw new ArgumentException("WORKSPACE_ROOT_INVALID");
        if (GitMetadataProvider.Detect(ancestor).GitRootPath is { } gitRoot && Within(root, Full(gitRoot)))
            throw new ArgumentException("WORKSPACE_SETUP_INSIDE_GIT_REPOSITORY: choose a private workspace outside Git repositories.");
        using var store = WebFormsWizardStore.Open(Full(options["--wizard-root"]), resume: true);
        var id = options.GetValueOrDefault("--website-id");
        if (id is null)
        {
            if (store.State.Projects.Length != 1)
                throw new ArgumentException("WORKSPACE_WIZARD_WEBSITE_ID_REQUIRED: select exactly one saved website with --website-id; configured IDs: "
                    + JsonSerializer.Serialize(store.State.Projects.Select(item => item.Id).ToArray()));
            id = store.State.Projects[0].Id;
        }
        var project = store.ReadProject(id);
        var originalPath = await WebFormsWizardNative.ValidateAsync(store, id, token);
        var reference = project.Run;
        if (reference is null || !Regex.IsMatch(reference.RelativeRoot,
            "\\Aruns/" + Regex.Escape(id) + "-[0-9a-f]{32}\\z", RegexOptions.CultureInvariant)
            || reference.NativeConfigSha256 != project.Native!.Sha256
            || reference.AttestedCommitSha != project.Native.SourceCommitSha)
            throw new ArgumentException("WORKSPACE_WIZARD_PREPARED_RUN_REQUIRED: reuse a wizard attempt with explicit source attestation and prepared receipts; no attestation or preparation is performed here.");
        // This is the selected saved run, never the newest directory or an inferred filename.
        var attempt = WebFormsReviewPreflightCommand.Child(store.DirectoryPath, reference.RelativeRoot);
        var preparedPath = WebFormsReviewPreflightCommand.Child(attempt, "evidence/review-config.local.json");
        if (!File.Exists(preparedPath)) throw new ArgumentException("WORKSPACE_WIZARD_PREPARED_CONFIG_MISSING: the selected saved attempt has no prepared receipt config.");
        var prospective = Path.Combine(root, ".setup-check");
        var original = await WebFormsReviewPreflightCommand.BuildAsync(originalPath, prospective, token);
        var prepared = await WebFormsReviewPreflightCommand.BuildAsync(preparedPath, prospective, token);
        var restored = prepared.Configuration with
        {
            ReceiptRoot = original.Configuration.ReceiptRoot,
            BindingReceipts = original.Configuration.BindingReceipts,
            PublishReceiptRelativePath = original.Configuration.PublishReceiptRelativePath,
            PublishSourceRelativePaths = original.Configuration.PublishSourceRelativePaths,
            PreparationProvenance = original.Configuration.PreparationProvenance
        };
        if (!JsonSerializer.SerializeToUtf8Bytes(restored, Json).AsSpan()
            .SequenceEqual(JsonSerializer.SerializeToUtf8Bytes(original.Configuration, Json)))
            throw new ArgumentException("WORKSPACE_WIZARD_PREPARED_SCOPE_MISMATCH: saved wizard selection differs from the prepared config.");
        var sqlManifest = Full(options["--sql-manifest"]);
        var sqlRoot = Full(options["--sql-source-root"]);
        Unlinked(sqlManifest); Unlinked(sqlRoot);
        var sql = SqlServerLocalDefinitionsCommand.Inspect(sqlManifest, sqlRoot, token);
        var webId = sql.Export.Input.ContextRepository;
        if (webId == "framework") throw new ArgumentException("WORKSPACE_SETUP_REPOSITORY_ID_CONFLICT: SQL context repository cannot use the reserved framework ID.");
        WorkspaceRepository? framework = null;
        if (options.ContainsKey("--framework-repo") != options.ContainsKey("--framework-target"))
            throw new ArgumentException("WORKSPACE_SETUP_FRAMEWORK_PAIR_REQUIRED: provide both framework repo and target, or omit both to retain compiled dependency evidence only.");
        if (options.TryGetValue("--framework-repo", out var frameworkPath))
        {
            var frameworkRoot = Full(frameworkPath);
            Unlinked(frameworkRoot);
            if (!Directory.Exists(frameworkRoot)) throw new ArgumentException("WORKSPACE_REPOSITORY_MISSING: framework");
            var target = ValidateNewSelectionPath(options["--framework-target"]);
            framework = target == "discovery" ? new WorkspaceRepository("framework", frameworkRoot, [])
                : IsProject(target) ? new WorkspaceRepository("framework", frameworkRoot, [target])
                : IsSolution(target) ? new WorkspaceRepository("framework", frameworkRoot, [], Solution: target)
                : throw new ArgumentException("WORKSPACE_SELECTION_FILE_KIND: --framework-target requires a project, solution or the explicit word discovery.");
        }
        else await output.WriteLineAsync("No additional dependency source selected. Existing compiled DLL selection is retained; dependency-source coverage is not added.");
        WorkspaceRepository[] configuredRepositories = framework is null
            ? [new(webId, original.Configuration.SourceRoot, [])]
            : [new(webId, original.Configuration.SourceRoot, []), framework];
        var config = new WorkspaceConfiguration(SqlSchemaVersion,
            configuredRepositories, WebFormsReport: true,
            CompiledReview: new(webId, preparedPath), SqlEvidence: new(webId, sqlManifest, sqlRoot));
        Validate(config);
        var repositories = new List<(WorkspaceRepository Config, string Root, string Head)>();
        var inputs = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var selectedFiles = new List<(string Path, string Hash)>();
        foreach (var repository in config.Repositories)
        {
            Unlinked(repository.Path);
            if (Within(root, repository.Path)) throw new ArgumentException("WORKSPACE_SETUP_INSIDE_SOURCE");
            foreach (var selection in repository.Projects.Concat(repository.Solution is null ? [] : new[] { repository.Solution }))
            {
                var path = Full(selection, repository.Path);
                Unlinked(path);
                if (!Within(path, repository.Path) || !File.Exists(path))
                    throw new ArgumentException("WORKSPACE_PROJECT_MISSING_OR_OUTSIDE_REPOSITORY: " + repository.Id);
                selectedFiles.Add((path, Hash(ReadBounded(path, 4_194_304))));
            }
            var head = await Git(repository.Path, token, "rev-parse", "--verify", "HEAD");
            repositories.Add((repository, repository.Path, head));
            inputs.Add("repository:" + repository.Id, Hash(System.Text.Encoding.UTF8.GetBytes(repository.Path + "\n" + head)));
        }
        var compiled = await VerifyCompiledAsync(config, root, prospective, repositories, inputs, token)
            ?? throw new ArgumentException("WORKSPACE_COMPILED_PREPARED_CONFIG_REQUIRED");
        _ = VerifySql(config, root, prospective, repositories, inputs, token);
        inputs.Add("wizard-original", original.Inputs.Single(item => item.Role == "configuration").Sha256);
        inputs.Add("wizard-root", Hash(ReadBounded(Path.Combine(store.DirectoryPath, "root.config.json"), 1_048_576)));
        inputs.Add("wizard-project", Hash(ReadBounded(store.ProjectPath(id), 1_048_576)));
        foreach (var (path, hash) in selectedFiles) inputs.Add("selection:" + path, hash);
        inputs.Add("setup-configuration", Hash(JsonSerializer.SerializeToUtf8Bytes(config, Json)));
        config = config with { TemplateProvenance = new(GeneratorHash(), Hash(JsonSerializer.SerializeToUtf8Bytes(inputs, Json))) };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(config, Json);
        if (bytes.Length > 1_048_576) throw new ArgumentException("WORKSPACE_CONFIG_LIMIT");
        await WriteScopePreviewAsync(config, repositories, compiled, output, token);
        _ = await WebFormsWizardNative.ValidateAsync(store, id, token);
        await WebFormsReviewInputValidation.RecheckAsync(compiled.NativePlan, token);
        var sqlRecheck = new SortedDictionary<string, string>(StringComparer.Ordinal);
        _ = VerifySql(config, root, prospective, repositories, sqlRecheck, token);
        if (sqlRecheck.Any(item => inputs.GetValueOrDefault(item.Key) != item.Value))
            throw new ArgumentException("WORKSPACE_SETUP_INPUT_CHANGED");
        foreach (var (repository, path, head) in repositories)
            if (await Git(path, token, "rev-parse", "--verify", "HEAD") != head)
                throw new ArgumentException("WORKSPACE_HEAD_CHANGED: " + repository.Id);
        foreach (var (path, hash) in selectedFiles)
            if (Hash(ReadBounded(path, 4_194_304)) != hash) throw new ArgumentException("WORKSPACE_SETUP_INPUT_CHANGED");
        store.VerifyUnchanged();
        token.ThrowIfCancellationRequested();
        Directory.CreateDirectory(root);
        // Publish a complete config atomically; never replace even an empty init template.
        var temporary = Path.Combine(root, ".workspace-setup-" + Guid.NewGuid().ToString("N"));
        var ownsTemporary = false;
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                ownsTemporary = true;
                await stream.WriteAsync(bytes, token);
            }
            File.Move(temporary, destination, overwrite: false);
        }
        finally { if (ownsTemporary && File.Exists(temporary)) File.Delete(temporary); }
        await output.WriteLineAsync("Created verified V3 workspace configuration: " + destination);
        await output.WriteLineAsync("Reused existing wizard configuration and receipts; no scans, builds, SQL, attestation or completed-result import performed. Run workspace collect separately only when intended.");
        return 0;
    }
}
