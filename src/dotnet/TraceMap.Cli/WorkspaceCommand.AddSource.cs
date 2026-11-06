using System.Text;
using System.Text.Json;
using TraceMap.Core;

namespace TraceMap.Cli;

public static partial class WorkspaceCommand
{
    internal const string AddSourceRuleId = "workflow.workspace.add-source.v1";
    internal static WorkspaceConfiguration ReadConfiguration(string root, out byte[] bytes)
    {
        bytes = ReadBounded(Path.Combine(root, "workspace.local.json"), 1_048_576);
        using (var document = JsonDocument.Parse(bytes)) CheckProperties(document.RootElement);
        var config = JsonSerializer.Deserialize<WorkspaceConfiguration>(bytes, Json)
            ?? throw new ArgumentException("WORKSPACE_CONFIG_INVALID");
        Validate(config);
        return config;
    }

    private static async Task<int> AddSourceAsync(string root, Dictionary<string, string> options,
        TextWriter output, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        foreach (var required in new[] { "--id", "--repo", "--target" })
            if (!options.ContainsKey(required)) throw new ArgumentException("WORKSPACE_ADD_SOURCE_OPTION_REQUIRED: " + required);
        if (GitMetadataProvider.Detect(root).GitRootPath is { } gitRoot && Within(root, Full(gitRoot)))
            throw new ArgumentException("WORKSPACE_SETUP_INSIDE_GIT_REPOSITORY");
        var configPath = Path.Combine(root, "workspace.local.json");
        Unlinked(configPath);
        var lockPath = Path.Combine(root, ".workspace-update.lock");
        Unlinked(lockPath);
        // Persistent empty lock file, OS handle lock only: process exit releases it.
        // No stale CreateNew lock remains after interruption.
        await using var updateLock = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var config = ReadConfiguration(root, out var original);
        var id = options["--id"];
        if (config.Repositories.Any(item => item.Id == id)) throw new ArgumentException("WORKSPACE_SOURCE_ID_EXISTS: " + id);
        var source = Full(options["--repo"]);
        Unlinked(source);
        if (!Directory.Exists(source)) throw new ArgumentException("WORKSPACE_REPOSITORY_MISSING: " + id);
        if (Within(root, source) || Within(source, root)) throw new ArgumentException("WORKSPACE_OUTPUT_OVERLAPS_REPOSITORY");
        foreach (var existing in config.Repositories)
        {
            var existingRoot = Full(existing.Path, root);
            if (Within(source, existingRoot) || Within(existingRoot, source))
                throw new ArgumentException("WORKSPACE_SOURCE_ALREADY_COVERED_OR_OVERLAPPING: " + existing.Id);
        }
        var repositoryRoot = Full(await Git(source, token, "rev-parse", "--show-toplevel"));
        if (!source.Equals(repositoryRoot, PathComparison))
            throw new ArgumentException("WORKSPACE_SOURCE_REPOSITORY_ROOT_REQUIRED: use " + repositoryRoot
                + " with a repository-relative project/solution target; bin/publication subfolders do not select the framework source repository.");
        var target = ValidateNewSelectionPath(options["--target"]);
        var added = target == "discovery" ? new WorkspaceRepository(id, source, [])
            : IsProject(target) ? new WorkspaceRepository(id, source, [target])
            : IsSolution(target) ? new WorkspaceRepository(id, source, [], Solution: target)
            : throw new ArgumentException("WORKSPACE_SELECTION_FILE_KIND: choose .csproj/.vbproj/.fsproj/.sln/.slnx or discovery.");
        string? selectionPath = null, selectionHash = null;
        if (target != "discovery")
        {
            selectionPath = Full(target, source); Unlinked(selectionPath);
            if (!Within(selectionPath, source) || !File.Exists(selectionPath))
                throw new ArgumentException("WORKSPACE_PROJECT_MISSING_OR_OUTSIDE_REPOSITORY: " + id);
            selectionHash = Hash(ReadBounded(selectionPath, 4_194_304));
        }
        var head = await Git(source, token, "rev-parse", "--verify", "HEAD");
        if (!System.Text.RegularExpressions.Regex.IsMatch(head, "\\A[0-9a-f]{40,64}\\z", System.Text.RegularExpressions.RegexOptions.CultureInvariant))
            throw new ArgumentException("WORKSPACE_HEAD_INVALID: " + id);
        var inputs = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["operation"] = Hash(Encoding.UTF8.GetBytes(AddSourceRuleId)),
            ["previous-configuration"] = Hash(original),
            ["source"] = Hash(Encoding.UTF8.GetBytes(source + "\n" + head)),
            ["selection"] = selectionHash ?? Hash(Encoding.UTF8.GetBytes("explicit-discovery"))
        };
        string? oldScopePath = null, oldScopeHash = null, newScopePath = null;
        byte[]? newScopeBytes = null;
        var candidate = config with { Repositories = [.. config.Repositories, added], TemplateProvenance = null };
        if (config.ScopeFile is { } scope)
        {
            oldScopePath = Full(scope, root);
            var selected = Select(config.Repositories, oldScopePath, inputs);
            oldScopeHash = inputs["scope"];
            var scopeInput = Hash(JsonSerializer.SerializeToUtf8Bytes(new { previousScope = oldScopeHash, id, inputs }, Json));
            newScopeBytes = Encoding.UTF8.GetBytes("# TraceMap generated local scope copy\n# ruleId=" + AddSourceRuleId + "\n# generatorSha256=" + GeneratorHash()
                + "\n# boundedInputSha256=" + scopeInput + "\n" + string.Join('\n', selected.Select(item => item.Id).Append(id)) + "\n");
            newScopePath = Path.Combine(root, "workspace-scope-" + Guid.NewGuid().ToString("N") + ".local.txt");
            candidate = candidate with { ScopeFile = Path.GetFileName(newScopePath) };
            inputs["updated-scope"] = Hash(newScopeBytes);
        }
        Validate(candidate);
        inputs["updated-configuration"] = Hash(JsonSerializer.SerializeToUtf8Bytes(candidate, Json));
        candidate = candidate with { TemplateProvenance = new(GeneratorHash(), Hash(JsonSerializer.SerializeToUtf8Bytes(inputs, Json))) };
        var bytes = JsonSerializer.SerializeToUtf8Bytes(candidate, Json);
        if (bytes.Length > 1_048_576) throw new ArgumentException("WORKSPACE_CONFIG_LIMIT");
        await output.WriteLineAsync("Source addition: " + id + "; folder=" + JsonSerializer.Serialize(source)
            + "; target=" + JsonSerializer.Serialize(target) + "; Git HEAD=" + head);
        await output.WriteLineAsync("Existing website, compiled/SQL selections and completed runs will be retained. This configures a future source scan; no source scan or MSBuild/SQL admission runs now.");
        token.ThrowIfCancellationRequested();
        if (Hash(ReadBounded(configPath, 1_048_576)) != inputs["previous-configuration"]
            || selectionPath is not null && Hash(ReadBounded(selectionPath, 4_194_304)) != selectionHash
            || oldScopePath is not null && Hash(ReadBounded(oldScopePath, 65_536)) != oldScopeHash
            || await Git(source, token, "rev-parse", "--verify", "HEAD") != head)
            throw new ArgumentException("WORKSPACE_ADD_SOURCE_INPUT_CHANGED");
        var temporary = Path.Combine(root, ".workspace-add-source-" + Guid.NewGuid().ToString("N"));
        var backup = Path.Combine(root, "workspace-before-add-" + Guid.NewGuid().ToString("N") + ".local.json");
        var tempOwned = false;
        try
        {
            if (newScopePath is not null)
            {
                await using var scopeStream = new FileStream(newScopePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                await scopeStream.WriteAsync(newScopeBytes!, token); scopeStream.Flush(true);
            }
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { tempOwned = true; await stream.WriteAsync(bytes, token); stream.Flush(true); }
            token.ThrowIfCancellationRequested();
            if (Hash(ReadBounded(configPath, 1_048_576)) != inputs["previous-configuration"])
                throw new ArgumentException("WORKSPACE_ADD_SOURCE_INPUT_CHANGED");
            File.Replace(temporary, configPath, backup);
            // A non-cooperating external editor can race the final check. Keep both
            // complete versions and explicitly refuse a success claim if it did.
            if (Hash(ReadBounded(backup, 1_048_576)) != inputs["previous-configuration"]
                || Hash(ReadBounded(configPath, 1_048_576)) != Hash(bytes))
                throw new ArgumentException("WORKSPACE_ADD_SOURCE_CONCURRENT_CHANGE: both versions retained; inspect " + backup);
        }
        finally
        {
            if (tempOwned && File.Exists(temporary)) File.Delete(temporary);
            // A generated scope copy is retained even on failure: a platform I/O
            // failure after replacement must never leave an active config dangling.
        }
        await output.WriteLineAsync("Configured source repositories: " + candidate.Repositories.Length + ". Added source: " + id + ". Previous config retained: " + backup);
        if (newScopePath is not null) await output.WriteLineAsync("Saved scope extended in a new copy; original scope preserved: " + newScopePath);
        await output.WriteLineAsync("Existing runs still describe their original scope. Choose collect to produce a NEW run with the added source; no DLL/source correspondence or semantic completeness is assumed.");
        return 0;
    }
}
