using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using TraceMap.Core;

namespace TraceMap.Cli;

internal sealed record WorkspaceWizardAnswers(string WebsiteId, string NativeConfigurationSha256,
    string? FrameworkRoot, string? FrameworkTarget, string? SqlManifest, string? SqlRoot);
internal sealed record WorkspaceWizardAnswerFile(string SchemaVersion, string RuleId, string Visibility,
    string GeneratorSha256, string BoundedInputSha256, WorkspaceWizardAnswers Answers);

/// <summary>Interactive adapter over the existing verified initializer; never scans or attests.</summary>
internal static class WebFormsWizardWorkspace
{
    internal const string RuleId = "workflow.workspace.wizard-setup.v1";
    private const string Schema = "tracemap-workspace-wizard-answers.v1";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true, MaxDepth = 16, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true
    };

    internal static async Task<int> RunAsync(WebFormsWizardStore store, string id, TextReader input,
        TextWriter output, TextWriter error, CancellationToken token)
    {
        var root = store.DirectoryPath;
        if (File.Exists(Path.Combine(root, "workspace.local.json")))
        {
            await output.WriteLineAsync("Workspace configuration already exists in this root. Verify the saved configuration, collect evidence now, or pause? [verify/collect/later]");
            var choice = await input.ReadLineAsync(token);
            if (choice is null || choice.Trim() == "later") return 2;
            if (choice.Trim() == "collect") { store.Dispose(); return await Collect(); }
            if (choice.Trim() != "verify") throw Fail("CHOICE_INVALID");
            store.Dispose();
            return await WorkspaceCommand.RunAsync(["verify", "--root", root], output, error, token);
        }
        var native = store.ReadProject(id).Native ?? throw Fail("NATIVE_CONFIG_MISSING");
        var path = WebFormsReviewPreflightCommand.Child(Path.GetDirectoryName(store.ProjectPath(id))!, "workspace-answers.local.json");
        string? previousHash = null;
        var answers = new WorkspaceWizardAnswers(id, native.Sha256, null, null, null, null);
        if (File.Exists(path))
        {
            var bytes = Read(path);
            previousHash = Hash(bytes);
            using var document = JsonDocument.Parse(bytes);
            Unique(document.RootElement);
            if (document.RootElement.TryGetProperty("answers", out var payload)) Unique(payload);
            var saved = JsonSerializer.Deserialize<WorkspaceWizardAnswerFile>(bytes, Json) ?? throw Fail("ANSWERS_INVALID");
            if (saved.Answers is null || saved.SchemaVersion != Schema || saved.RuleId != RuleId || saved.Visibility != "local-only"
                || saved.GeneratorSha256 is null || !Regex.IsMatch(saved.GeneratorSha256, "\\A[0-9a-f]{64}\\z", RegexOptions.CultureInvariant)
                || saved.BoundedInputSha256 != Hash(JsonSerializer.SerializeToUtf8Bytes(saved.Answers, Json))) throw Fail("ANSWERS_INVALID");
            answers = saved.Answers;
            if (answers.WebsiteId != id || answers.NativeConfigurationSha256 != native.Sha256) throw Fail("ANSWERS_STALE_NATIVE_SELECTION");
            await output.WriteLineAsync("Reusing saved workspace answers. Existing website, DLL selection and SQL manifest are not recreated.");
        }
        await output.WriteLineAsync("Continue workspace setup in your existing root: " + root + ". No scan will start.");
        while (true)
        {
            if (answers.FrameworkRoot is null)
            {
                await output.WriteLineAsync("Your website and compiled dependency DLLs are already selected. An additional source-code repository is optional; it is not the DLL or bin folder.");
                var value = await Existing("Framework SOURCE repository folder if you have its checkout; type skip if you only have the DLL or do not want additional source, or later:", false, allowSkip: true);
                if (value is null) return 2;
                answers = answers with { FrameworkRoot = value, FrameworkTarget = value == "" ? "" : null }; await Save();
            }
            if (answers.FrameworkTarget is null)
            {
                var names = Directory.EnumerateFiles(answers.FrameworkRoot).Take(257).ToArray();
                var candidates = names.Length > 256 ? [] : names.Where(Supported).Order(StringComparer.Ordinal).ToArray();
                if (names.Length > 256) await output.WriteLineAsync("Root file preview limit reached; no partial candidate list shown. Nested projects are not inventoried.");
                for (var index = 0; index < candidates.Length; index++) await output.WriteLineAsync($"{index + 1}: {Path.GetFileName(candidates[index])}");
                while (true)
                {
                    await output.WriteLineAsync("Framework project/solution: choose a listed number, paste a file path, or type discovery (explicit fallback) or later:");
                    var value = await input.ReadLineAsync(token);
                    if (value is null || value.Trim() == "later") return 2;
                    value = Unquote(value);
                    if (value == "discovery") { answers = answers with { FrameworkTarget = value }; break; }
                    if (int.TryParse(value, out var number))
                    {
                        if (number < 1 || number > candidates.Length) { await output.WriteLineAsync("Choose a listed number or a project/solution path."); continue; }
                        value = candidates[number - 1];
                    }
                    var full = WebFormsWizardStore.Physical(Path.GetFullPath(value.Replace('\\', '/'), answers.FrameworkRoot));
                    var relative = Path.GetRelativePath(answers.FrameworkRoot, full);
                    if (!File.Exists(full) || !Supported(full) || Path.IsPathRooted(relative)
                        || relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                    { await output.WriteLineAsync("Choose an existing project/solution inside that framework folder; no selection was saved."); continue; }
                    answers = answers with { FrameworkTarget = relative.StartsWith("--", StringComparison.Ordinal) ? "./" + relative : relative };
                    break;
                }
                await Save();
            }
            if (answers.SqlManifest is null)
            {
                var value = await Existing("Existing SQL manifest FILE from step 4 (not the SQL file itself), or later:", true);
                if (value is null) return 2;
                answers = answers with { SqlManifest = value }; await Save();
            }
            // Prefer the manifest's own directory. Recheck an older saved base first;
            // repair a missing-file base only when the manifest-relative bytes verify.
            var manifestBase = Path.GetDirectoryName(answers.SqlManifest!)!;
            var sqlPlan = TrySql(answers.SqlRoot ?? manifestBase);
            if (sqlPlan is null && answers.SqlRoot is not null) sqlPlan = TrySql(manifestBase);
            if (sqlPlan is not null)
            {
                if (answers.SqlRoot != sqlPlan.SourceRoot)
                {
                    if (answers.SqlRoot is not null) await output.WriteLineAsync("The saved SQL base did not resolve the listed files; using verified paths relative to the manifest instead.");
                    answers = answers with { SqlRoot = sqlPlan.SourceRoot }; await Save();
                }
                await output.WriteLineAsync("SQL files verified. Base directory: " + sqlPlan.SourceRoot);
                foreach (var file in sqlPlan.SourceFiles) await output.WriteLineAsync("  " + file.Path + " -> " + Path.GetFullPath(file.Path, sqlPlan.SourceRoot));
            }
            if (answers.SqlRoot is null)
            {
                var value = await Existing("Files were not found relative to the manifest. Enter an alternate BASE folder for its listed paths (for sql/file.sql choose the parent of sql, not sql itself), or later:", false);
                if (value is null) return 2;
                answers = answers with { SqlRoot = value }; await Save();
            }
            await output.WriteLineAsync("Saved choices (same destination and wizard root): " + root);
            await output.WriteLineAsync(answers.FrameworkRoot == "" ? "Additional dependency source: skipped; existing compiled DLL evidence retained." : "Framework source: " + answers.FrameworkRoot + "; target: " + answers.FrameworkTarget);
            await output.WriteLineAsync("SQL manifest: " + answers.SqlManifest + "; SQL folder: " + answers.SqlRoot);
            await output.WriteLineAsync("Create and verify the workspace configuration without scanning, change one answer, or pause? [create/change/later]");
            var action = await input.ReadLineAsync(token);
            if (action is null || action.Trim() == "later") return 2;
            if (action.Trim() == "change")
            {
                await output.WriteLineAsync("Change which saved choice? 1=framework folder, 2=framework project/solution, 3=SQL manifest, 4=SQL folder; later pauses:");
                var field = await input.ReadLineAsync(token);
                if (field is null || field.Trim() == "later") return 2;
                answers = field.Trim() switch
                {
                    "1" => answers with { FrameworkRoot = null, FrameworkTarget = null },
                    "2" => answers with { FrameworkRoot = answers.FrameworkRoot == "" ? null : answers.FrameworkRoot, FrameworkTarget = null },
                    "3" => answers with { SqlManifest = null },
                    "4" => answers with { SqlRoot = null },
                    _ => throw Fail("CHOICE_INVALID")
                };
                await Save();
                continue;
            }
            if (action.Trim() != "create") throw Fail("CHOICE_INVALID");
            store.VerifyUnchanged();
            store.Dispose(); // The initializer independently opens and verifies the saved wizard.
            var created = await WorkspaceCommand.RunAsync(["init-from-wizard", "--root", root, "--wizard-root", root,
                "--website-id", id, .. (answers.FrameworkRoot == "" ? Array.Empty<string>() : new[] { "--framework-repo", answers.FrameworkRoot!, "--framework-target", answers.FrameworkTarget! }),
                "--sql-manifest", answers.SqlManifest!, "--sql-source-root", answers.SqlRoot!], output, error, token);
            if (created != 0) return created;
            await output.WriteLineAsync("Workspace configuration is saved at: " + Path.Combine(root, "workspace.local.json"));
            await output.WriteLineAsync("SQLite evidence is created by collection, not configuration. Collect source, compiled and SQL evidence now, or finish setup for later? [collect/later]");
            var next = await input.ReadLineAsync(token);
            if (next is null || next.Trim() == "later") return 0;
            if (next.Trim() != "collect") throw Fail("CHOICE_INVALID");
            return await Collect();
        }

        SqlLocalCollectionInput? TrySql(string baseFolder)
        {
            try { return SqlServerLocalDefinitionsCommand.Inspect(answers.SqlManifest!, baseFolder, token); }
            catch (FileNotFoundException) { return null; }
            catch (DirectoryNotFoundException) { return null; }
            catch (ArgumentException exception) when (exception.Message is "sql-local-file-invalid-or-duplicate" or "sql-selection-original-file-invalid-or-duplicate")
            {
                throw new InvalidOperationException("WEBFORMS_WORKSPACE_SQL_MANIFEST_ENTRY_INVALID: the manifest file list is invalid before folder resolution. Each path must be unique, relative, slash-separated, end in lowercase .sql, and use supported path characters; each SHA-256 must be 64 lowercase hexadecimal characters. Changing the base folder cannot repair an invalid manifest entry.", exception);
            }
        }

        async Task<int> Collect()
        {
            var run = Path.Combine(root, "runs", "workspace-" + Guid.NewGuid().ToString("N"));
            await output.WriteLineAsync("Collecting into a fresh output folder: " + run);
            var result = await WorkspaceCommand.RunAsync(["collect", "--root", root, "--out", run], output, error, token);
            if (result == 0)
            {
                await output.WriteLineAsync("Open the workspace results: " + Path.Combine(run, "START-HERE.md"));
                await output.WriteLineAsync("Combined SQLite evidence: " + Path.Combine(run, "combined.sqlite"));
                await output.WriteLineAsync("SQL evidence remains a separate sidecar; see START-HERE.md for coverage and channel paths.");
            }
            return result;
        }

        async Task<string?> Existing(string prompt, bool file, bool allowSkip = false)
        {
            while (true)
            {
                await output.WriteLineAsync(prompt);
                var value = await input.ReadLineAsync(token);
                if (value is null || value.Trim() == "later") return null;
                value = Unquote(value);
                if (allowSkip && value == "skip") return "";
                if (string.IsNullOrWhiteSpace(value)) { await output.WriteLineAsync("Enter a path or later."); continue; }
                var full = WebFormsWizardStore.Physical(value);
                if (file ? File.Exists(full) : Directory.Exists(full)) return full;
                await output.WriteLineAsync("That path does not exist; try again or type later. Earlier choices are retained.");
            }
        }
        async Task Save()
        {
            store.VerifyUnchanged();
            if (File.Exists(path) ? previousHash is null || Hash(Read(path)) != previousHash : previousHash is not null)
                throw Fail("ANSWERS_CHANGED");
            var record = new WorkspaceWizardAnswerFile(Schema, RuleId, "local-only",
                Hash(File.ReadAllBytes(typeof(WebFormsWizardWorkspace).Assembly.Location)),
                Hash(JsonSerializer.SerializeToUtf8Bytes(answers, Json)), answers);
            var bytes = JsonSerializer.SerializeToUtf8Bytes(record, Json);
            if (bytes.Length > 1_048_576) throw Fail("ANSWERS_LIMIT");
            var temporary = path + ".pending-" + Guid.NewGuid().ToString("N");
            var owned = false;
            try
            {
                await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                { owned = true; await stream.WriteAsync(bytes, token); }
                File.Move(temporary, path, overwrite: true);
                previousHash = Hash(bytes);
            }
            finally { if (owned && File.Exists(temporary)) File.Delete(temporary); }
        }
    }

    private static string Unquote(string value)
    {
        value = value.Trim();
        return value.Length >= 2 && value[0] == '"' && value[^1] == '"' ? value[1..^1] : value;
    }
    private static bool Supported(string path) => Path.GetExtension(path).ToLowerInvariant() is ".csproj" or ".vbproj" or ".fsproj" or ".sln" or ".slnx";
    private static byte[] Read(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw Fail("ANSWERS_LINKED");
        using var stream = File.OpenRead(path);
        if (stream.Length > 1_048_576) throw Fail("ANSWERS_LIMIT");
        var bytes = new byte[checked((int)stream.Length)]; stream.ReadExactly(bytes); return bytes;
    }
    private static void Unique(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object) throw Fail("ANSWERS_INVALID");
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject()) if (!names.Add(property.Name)) throw Fail("ANSWERS_INVALID");
    }
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static InvalidOperationException Fail(string reason) => new("WEBFORMS_WORKSPACE_WIZARD_" + reason);
}
