using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace TraceMap.Cli;

public sealed record WorkspaceProvenance(string GeneratorSha256, string BoundedInputSha256);
public sealed record WorkspaceRepository(string Id, string Path, string[] Projects,
    string? Solution = null, string[]? Include = null, string[]? Exclude = null, string? TargetFramework = null);
public sealed record WorkspaceConfiguration(string SchemaVersion, WorkspaceRepository[] Repositories,
    string? ScopeFile = null, bool WebFormsReport = false, WorkspaceProvenance? TemplateProvenance = null);

/// <summary>Source collection orchestration; compiled and SQL admission remain separate contracts.</summary>
public static partial class WorkspaceCommand
{
    public const string SchemaVersion = "tracemap-workspace.v1";
    public const string RuleId = "workflow.workspace.source-collection.v1";
    public const string Help = """
        tracemap workspace init --root <workspace-folder>
        tracemap workspace verify --root <workspace-folder> [--scope <repository-ids.txt>]
        tracemap workspace collect --root <workspace-folder> --out <new-run-folder> [--scope <repository-ids.txt>]

        Edit workspace.local.json after init. Paths in the config resolve from its folder.
        Scope is a nonempty list of configured repository IDs, one per line (# comments allowed).
        --scope resolves from the current shell and overrides the configured scopeFile.
        Verify is read-only. Collect runs source scans, combine and a report; optional Web Forms packet.
        No implicit restore, repository build, fetch, compiled-site or database ingestion.
        All outputs are local-only. Existing runs are preserved; choose a new --out for another run.
        """;

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true
    };
    private static readonly StringComparison PathComparison = OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    [GeneratedRegex("^[a-z][a-z0-9-]{0,47}$", RegexOptions.CultureInvariant)]
    private static partial Regex IdPattern();

    public static Task<int> RunAsync(string[] args, TextWriter output, TextWriter error,
        CancellationToken token = default) => RunAsync(args, output, error, TraceMapCommand.RunAsync, token);

    internal static async Task<int> RunAsync(string[] args, TextWriter output, TextWriter error,
        LocalReviewScanRunner execute, CancellationToken token = default)
    {
        string? run = null;
        string stage = "verification";
        var inputs = new SortedDictionary<string, string>(StringComparer.Ordinal);
        var sources = new List<object>();
        try
        {
            if (args.Length == 0 || args[0] is not ("init" or "verify" or "collect"))
                throw new ArgumentException("WORKSPACE_COMMAND_REQUIRED: use init, verify or collect; see workspace --help.");
            var operation = args[0];
            var options = Options(args.Skip(1).ToArray(), operation);
            var root = Full(options.GetValueOrDefault("--root")
                ?? throw new ArgumentException("WORKSPACE_ROOT_REQUIRED"));
            Unlinked(root);
            var configPath = System.IO.Path.Combine(root, "workspace.local.json");
            if (operation == "init")
            {
                if (File.Exists(configPath) || Directory.Exists(configPath))
                    throw new ArgumentException("WORKSPACE_CONFIG_EXISTS: existing configuration was preserved.");
                Directory.CreateDirectory(root);
                var configuration = new WorkspaceConfiguration(SchemaVersion, [], TemplateProvenance:
                    new(GeneratorHash(), Hash(Encoding.UTF8.GetBytes("tracemap-workspace-empty-template.v1"))));
                using var stream = new FileStream(configPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                await JsonSerializer.SerializeAsync(stream, configuration, Json, token);
                await output.WriteLineAsync($"Created {configPath}. Add repositories, then run workspace verify --root \"{root}\".");
                return 0;
            }

            var configBytes = ReadBounded(configPath, 1_048_576);
            using (var document = JsonDocument.Parse(configBytes)) CheckProperties(document.RootElement);
            var config = JsonSerializer.Deserialize<WorkspaceConfiguration>(configBytes, Json)
                ?? throw new ArgumentException("WORKSPACE_CONFIG_INVALID");
            Validate(config);
            inputs.Add("configuration", Hash(configBytes));
            var scope = options.TryGetValue("--scope", out var explicitScope) ? Full(explicitScope)
                : config.ScopeFile is null ? null : Full(config.ScopeFile, root);
            var selected = Select(config.Repositories, scope, inputs);
            var repositories = new List<(WorkspaceRepository Config, string Root, string Head)>();
            foreach (var repository in selected)
            {
                token.ThrowIfCancellationRequested();
                var path = Full(repository.Path, root);
                Unlinked(path);
                if (!Directory.Exists(path)) throw new ArgumentException($"WORKSPACE_REPOSITORY_MISSING: {repository.Id}");
                foreach (var project in repository.Projects.Concat(repository.Solution is null ? [] : new[] { repository.Solution }))
                {
                    var projectPath = Full(project, path);
                    Unlinked(projectPath);
                    if (!Within(projectPath, path) || !File.Exists(projectPath))
                        throw new ArgumentException($"WORKSPACE_PROJECT_MISSING_OR_OUTSIDE_REPOSITORY: {repository.Id}");
                }
                var head = await Git(path, token, "rev-parse", "--verify", "HEAD");
                if (!Regex.IsMatch(head, "^[0-9a-f]{40,64}$", RegexOptions.CultureInvariant))
                    throw new ArgumentException($"WORKSPACE_HEAD_INVALID: {repository.Id}");
                repositories.Add((repository, path, head));
                inputs.Add($"repository:{repository.Id}", Hash(Encoding.UTF8.GetBytes(path + "\n" + head)));
                await output.WriteLineAsync($"Verified {repository.Id}: {head}");
            }
            await output.WriteLineAsync($"Selected {repositories.Count} source repositories. Compiled publication and SQL are not collected by this command.");
            if (operation == "verify") return 0;

            run = Full(options.GetValueOrDefault("--out")
                ?? throw new ArgumentException("WORKSPACE_OUTPUT_REQUIRED: choose a fresh run folder."));
            Unlinked(run);
            if (File.Exists(run) || Directory.Exists(run))
                throw new ArgumentException("WORKSPACE_OUTPUT_EXISTS: choose a new run folder; existing runs are preserved.");
            foreach (var repository in repositories)
                if (Within(run, repository.Root) || Within(repository.Root, run))
                    throw new ArgumentException("WORKSPACE_OUTPUT_OVERLAPS_REPOSITORY");
            if (Within(root, run) || Within(AppContext.BaseDirectory, run) || Within(run, AppContext.BaseDirectory))
                throw new ArgumentException("WORKSPACE_OUTPUT_OVERLAPS_CONFIGURATION_OR_TOOL");
            // Assign ownership only after all preflight checks. Never write a failure record into an existing run.
            var destination = run;
            run = null;
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(destination)!);
            var reservation = destination + ".pending-" + Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(reservation);
            try { Directory.Move(reservation, destination); }
            finally { if (Directory.Exists(reservation)) Directory.Delete(reservation); }
            run = destination;
            stage = "initialization";
            await Record("running");
            var combine = new List<string> { "combine" };
            var partial = false;
            foreach (var repository in repositories)
            {
                stage = "scan:" + repository.Config.Id;
                if (await Git(repository.Root, token, "rev-parse", "--verify", "HEAD") != repository.Head)
                    throw new ArgumentException("WORKSPACE_HEAD_CHANGED: rerun verification.");
                var scan = System.IO.Path.Combine(run, "sources", repository.Config.Id);
                var scanArgs = new List<string> { "scan", "--repo", repository.Root, "--out", scan };
                foreach (var project in repository.Config.Projects) scanArgs.AddRange(["--project", Full(project, repository.Root)]);
                if (repository.Config.Solution is not null) scanArgs.AddRange(["--solution", Full(repository.Config.Solution, repository.Root)]);
                foreach (var include in repository.Config.Include ?? []) scanArgs.AddRange(["--include", include]);
                foreach (var exclude in repository.Config.Exclude ?? []) scanArgs.AddRange(["--exclude", exclude]);
                if (repository.Config.TargetFramework is not null) scanArgs.AddRange(["--target-framework", repository.Config.TargetFramework]);
                await Execute(scanArgs.ToArray());
                var manifestPath = System.IO.Path.Combine(scan, "scan-manifest.json");
                var manifestBytes = ReadBounded(manifestPath, 16_777_216);
                using var manifest = JsonDocument.Parse(manifestBytes);
                var manifestRoot = manifest.RootElement;
                var manifestHead = manifestRoot.GetProperty("commitSha").GetString();
                if (manifestHead != repository.Head) throw new ArgumentException("WORKSPACE_SCAN_HEAD_MISMATCH");
                var level = manifestRoot.GetProperty("analysisLevel").GetString();
                var build = manifestRoot.GetProperty("buildStatus").GetString();
                partial |= level != "Level1SemanticAnalysis" || build != "Succeeded"
                    || manifestRoot.GetProperty("knownGaps").GetArrayLength() != 0;
                var index = System.IO.Path.Combine(scan, "index.sqlite");
                inputs.Add(stage + ":manifest", Hash(manifestBytes));
                inputs.Add(stage + ":index", FileHash(index));
                sources.Add(new { id = repository.Config.Id, commitSha = manifestHead, analysisLevel = level, buildStatus = build });
                combine.AddRange(["--index", index, "--label", repository.Config.Id]);
                await Record("running");
            }
            stage = "combine";
            var combined = System.IO.Path.Combine(run, "combined.sqlite");
            combine.AddRange(["--out", combined]);
            await Execute(combine.ToArray());
            inputs.Add("combined-index", FileHash(combined));
            stage = "report";
            await Execute(["report", "--index", combined, "--out", System.IO.Path.Combine(run, "report.md")]);
            if (config.WebFormsReport)
            {
                stage = "webforms-report";
                await Execute(["webforms-modernization", "--index", combined, "--out", System.IO.Path.Combine(run, "webforms")]);
            }
            stage = "completed";
            await File.WriteAllTextAsync(System.IO.Path.Combine(run, "START-HERE.md"),
                $"# Source collection\n\nOutcome: {(partial ? "partial" : "completed")}. Read report.md and each sources/<id>/report.md.\n\n"
                + "Combined index: combined.sqlite. Run state: workspace-run.local.json.\n\n"
                + "Compiled publication and database definitions were not ingested. No repository build, restore or fetch was requested.\n"
                + "Reduced analysis and failed builds remain gaps; completion is not proof of complete coverage.\n", token);
            await Record(partial ? "partial" : "completed");
            await output.WriteLineAsync($"Source collection {(partial ? "partial" : "completed")}: {System.IO.Path.Combine(run, "START-HERE.md")}");
            return 0;

            async Task Execute(string[] command)
            {
                token.ThrowIfCancellationRequested();
                if (await execute(command, output, error, token) != 0)
                    throw new InvalidOperationException($"WORKSPACE_STAGE_FAILED: {stage}; retained output is preserved.");
            }

            async Task Record(string outcome)
            {
                var record = new
                {
                    schemaVersion = "tracemap-workspace-run.v1", ruleId = RuleId, visibility = "local-only",
                    generatorSha256 = GeneratorHash(), boundedInputSha256 = Hash(JsonSerializer.SerializeToUtf8Bytes(inputs, Json)),
                    inputs, outcome, stage, sources,
                    limitations = new[] { "source-only", "static-potentially-partial", "compiled-and-sql-not-ingested", "no-legacy-output-parity-claim" }
                };
                var path = System.IO.Path.Combine(run!, "workspace-run.local.json");
                var temporary = path + ".tmp";
                await File.WriteAllBytesAsync(temporary, JsonSerializer.SerializeToUtf8Bytes(record, Json), CancellationToken.None);
                File.Move(temporary, path, overwrite: true);
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            if (run is not null && stage != "verification")
            {
                var failure = new
                {
                    schemaVersion = "tracemap-workspace-run.v1", ruleId = RuleId, visibility = "local-only",
                    generatorSha256 = GeneratorHash(), boundedInputSha256 = Hash(JsonSerializer.SerializeToUtf8Bytes(inputs, Json)),
                    inputs, outcome = ex is OperationCanceledException ? "cancelled" : "failed", stage, sources,
                    limitations = new[] { "source-only", "incomplete-run-preserved" }
                };
                await File.WriteAllBytesAsync(System.IO.Path.Combine(run, "workspace-run.local.json"),
                    JsonSerializer.SerializeToUtf8Bytes(failure, Json), CancellationToken.None);
            }
            await error.WriteLineAsync($"error: {ex.Message}");
            if (ex is OperationCanceledException) throw;
            return 1;
        }
    }

    private static Dictionary<string, string> Options(string[] args, string operation)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < args.Length; i += 2)
        {
            var key = args[i];
            if (key != "--root" && !(key == "--scope" && operation != "init") && !(key == "--out" && operation == "collect"))
                throw new ArgumentException($"WORKSPACE_OPTION_UNKNOWN: {key}");
            if (i + 1 == args.Length || string.IsNullOrWhiteSpace(args[i + 1]) || args[i + 1].StartsWith("--", StringComparison.Ordinal)
                || !result.TryAdd(key, args[i + 1])) throw new ArgumentException("WORKSPACE_OPTION_INVALID_OR_DUPLICATE");
        }
        return result;
    }

    private static void Validate(WorkspaceConfiguration config)
    {
        if (config.SchemaVersion != SchemaVersion || config.Repositories is null || config.Repositories.Length is < 1 or > 64)
            throw new ArgumentException("WORKSPACE_CONFIG_INVALID: expected tracemap-workspace.v1 and 1..64 repositories.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var repository in config.Repositories)
        {
            if (repository is null || repository.Id is null || !IdPattern().IsMatch(repository.Id) || !ids.Add(repository.Id)
                || string.IsNullOrWhiteSpace(repository.Path) || repository.Projects is null || repository.Projects.Length > 64
                || repository.Projects.Any(string.IsNullOrWhiteSpace)
                || (repository.Solution is not null && (string.IsNullOrWhiteSpace(repository.Solution) || repository.Projects.Length != 0))
                || (repository.Include ?? []).Concat(repository.Exclude ?? []).Any(string.IsNullOrWhiteSpace)
                || repository.TargetFramework is not null && string.IsNullOrWhiteSpace(repository.TargetFramework))
                throw new ArgumentException("WORKSPACE_REPOSITORY_INVALID: use unique lowercase IDs, a path and projects array; select projects or one solution.");
        }
        if (config.ScopeFile is not null && string.IsNullOrWhiteSpace(config.ScopeFile)) throw new ArgumentException("WORKSPACE_SCOPE_EMPTY");
    }

    private static WorkspaceRepository[] Select(WorkspaceRepository[] repositories, string? scope,
        SortedDictionary<string, string> inputs)
    {
        if (scope is null) return repositories;
        var bytes = ReadBounded(scope, 65_536);
        inputs.Add("scope", Hash(bytes));
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF').Split('\n'))
        {
            var id = line.Split('#')[0].Trim();
            if (id.Length == 0) continue;
            if (!ids.Add(id) || !repositories.Any(repository => repository.Id == id))
                throw new ArgumentException("WORKSPACE_SCOPE_UNKNOWN_OR_DUPLICATE_ID");
        }
        if (ids.Count == 0) throw new ArgumentException("WORKSPACE_SCOPE_EMPTY: empty selection never means all.");
        return repositories.Where(repository => ids.Contains(repository.Id)).ToArray();
    }

    private static void CheckProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!seen.Add(property.Name)) throw new ArgumentException("WORKSPACE_DUPLICATE_JSON_PROPERTY");
                CheckProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray()) CheckProperties(item);
    }

    private static byte[] ReadBounded(string path, int limit)
    {
        Unlinked(path);
        using var stream = File.OpenRead(path);
        if (stream.Length > limit) throw new ArgumentException("WORKSPACE_INPUT_TOO_LARGE");
        var bytes = new byte[checked((int)stream.Length)];
        stream.ReadExactly(bytes);
        return bytes;
    }

    private static string Full(string path, string? root = null)
    {
        var absolute = root is null ? System.IO.Path.GetFullPath(path) : System.IO.Path.GetFullPath(path, root);
        // Resolve existing ancestors before overlap comparisons (including macOS /tmp and /var).
        var current = System.IO.Path.GetPathRoot(absolute)!;
        foreach (var segment in absolute[current.Length..].Split(System.IO.Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = System.IO.Path.Combine(current, segment);
            if (Directory.Exists(current) || File.Exists(current))
            {
                FileSystemInfo info = Directory.Exists(current) ? new DirectoryInfo(current) : new FileInfo(current);
                if ((info.Attributes & FileAttributes.ReparsePoint) != 0)
                    current = info.ResolveLinkTarget(returnFinalTarget: true)?.FullName
                        ?? throw new ArgumentException("WORKSPACE_LINK_TARGET_UNAVAILABLE");
            }
        }
        return current;
    }
    private static bool Within(string path, string root) => path.Equals(root, PathComparison)
        || path.StartsWith(root.TrimEnd(System.IO.Path.DirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar, PathComparison);

    private static void Unlinked(string path)
    {
        for (var current = path; current is not null; current = System.IO.Path.GetDirectoryName(current))
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new ArgumentException("WORKSPACE_LINKED_PATH_UNSUPPORTED: use a direct local path.");
    }

    private static async Task<string> Git(string root, CancellationToken token, params string[] args)
    {
        var start = new ProcessStartInfo("git") { WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (var arg in args) start.ArgumentList.Add(arg);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("WORKSPACE_GIT_UNAVAILABLE");
        var stdout = process.StandardOutput.ReadToEndAsync(token);
        var stderr = process.StandardError.ReadToEndAsync(token);
        try { await process.WaitForExitAsync(token); }
        catch (OperationCanceledException) { if (!process.HasExited) process.Kill(entireProcessTree: true); throw; }
        var value = await stdout;
        await stderr;
        if (process.ExitCode != 0) throw new ArgumentException("WORKSPACE_GIT_HEAD_UNAVAILABLE: each selected source must have a Git commit.");
        return value.Trim();
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static string FileHash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
    private static string GeneratorHash() => FileHash(typeof(WorkspaceCommand).Assembly.Location);
}
