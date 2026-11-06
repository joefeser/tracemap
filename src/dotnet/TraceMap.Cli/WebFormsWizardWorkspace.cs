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
            while (true)
            {
                var configured = WorkspaceCommand.ReadConfiguration(root, out _);
                await output.WriteLineAsync("Configured SOURCE repositories: " + configured.Repositories.Length + " "
                    + JsonSerializer.Serialize(configured.Repositories.Select(item => item.Id).ToArray())
                    + ". Compiled dependency DLLs are separate from source repositories.");
                foreach (var source in configured.Repositories)
                    await output.WriteLineAsync("  SOURCE " + source.Id + ": folder=" + JsonSerializer.Serialize(source.Path)
                        + "; selection=" + JsonSerializer.Serialize(source.Solution is { } solution ? new[] { solution } : source.Projects)
                        + (source.Solution is null && source.Projects.Length == 0 ? " (folder/discovery or saved website scope; not a full-build claim)" : " (file selection relative to the source folder)"));
                await output.WriteLineAsync("Collect verifies these settings first; you do not need to choose verify before collect. Each collect creates a fresh run and retains previous runs.");
                await output.WriteLineAsync("Selected-root lifecycle analysis is a separate workflow; existing lifecycle results are not imported into this workspace run.");
                await output.WriteLineAsync("Workspace configuration already exists in this root. Verify, collect a fresh run, add framework SOURCE, or pause? [verify/collect/add-framework/later]");
                var choice = await input.ReadLineAsync(token);
                if (choice is null || choice.Trim() == "later") return 2;
                if (choice.Trim() == "collect") { store.Dispose(); return await Collect(); }
                if (choice.Trim() == "add-framework")
                {
                    if (configured.Repositories.Any(item => item.Id == "framework"))
                    {
                        await output.WriteLineAsync("A framework source entry is already configured; it is not replaced. Choose verify or collect to inspect/use that scope.");
                        continue;
                    }
                    var folder = await Existing("Framework SOURCE Git repository folder (not the DLL/bin folder), or later:", false);
                    if (folder is null) return 2;
                    var sourceGitRoot = GitMetadataProvider.Detect(folder).GitRootPath;
                    if (sourceGitRoot is null)
                    { await output.WriteLineAsync("No Git source repository was found at that folder. Existing setup is preserved; choose the source checkout on continuation."); return 2; }
                    sourceGitRoot = WebFormsWizardStore.Physical(sourceGitRoot);
                    if (!folder.Equals(sourceGitRoot, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
                    {
                        await output.WriteLineAsync("That folder is inside the source checkout " + JsonSerializer.Serialize(sourceGitRoot)
                            + ". Use the detected repository root and select its project next? [use-root/later]");
                        if ((await input.ReadLineAsync(token))?.Trim() != "use-root") return 2;
                        folder = sourceGitRoot;
                    }
                    var names = Directory.EnumerateFiles(folder).Take(257).ToArray();
                    if (names.Length > 256) { await output.WriteLineAsync("Root listing exceeds 256 entries; enter the exact project/solution path below."); names = []; }
                    var projects = names.Where(item => Path.GetExtension(item).ToLowerInvariant() is ".csproj" or ".vbproj" or ".fsproj")
                        .Select(item => Path.GetFileName(item)!).Order(StringComparer.Ordinal).ToArray();
                    var selections = names.Where(Supported).Select(item => Path.GetFileName(item)!).Order(StringComparer.Ordinal).ToArray();
                    await output.WriteLineAsync("Root project/solution files: " + JsonSerializer.Serialize(selections) + ". Nested projects are not inventoried.");
                    var detected = projects.Length == 1 ? projects[0] : null;
                    await output.WriteLineAsync("Choose the project FILE under that source folder, not another folder or DLL. For a nested project, enter its path such as Library/Library.vbproj. A solution can select more projects and is never chosen automatically.");
                    string target;
                    while (true)
                    {
                        await output.WriteLineAsync("Framework project/solution path relative to that folder, discovery, or later"
                            + (detected is null ? ":" : "; Enter uses detected project " + JsonSerializer.Serialize(detected) + ":"));
                        var selectedTarget = await input.ReadLineAsync(token);
                        if (selectedTarget is null || selectedTarget.Trim() == "later") return 2;
                        target = Unquote(selectedTarget);
                        if (target.Length == 0 && detected is not null) target = detected;
                        if (target != "discovery")
                        {
                            var full = WebFormsWizardStore.Physical(Path.GetFullPath(target.Replace('\\', '/'), folder));
                            var relative = Path.GetRelativePath(folder, full);
                            if (!File.Exists(full) || !Supported(full) || relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                            { await output.WriteLineAsync("Select an existing supported project/solution inside the framework source folder. Folder and earlier choices are retained."); continue; }
                            target = relative.Replace('\\', '/');
                        }
                        break;
                    }
                    await output.WriteLineAsync("Add framework SOURCE " + JsonSerializer.Serialize(folder) + "; target=" + JsonSerializer.Serialize(target)
                        + "? Existing website/DLL/SQL choices and runs stay intact; no scan starts. [add/later]");
                    if ((await input.ReadLineAsync(token))?.Trim() != "add") return 2;
                    store.VerifyUnchanged();
                    var added = await WorkspaceCommand.RunAsync(["add-source", "--root", root, "--id", "framework", "--repo", folder, "--target", target], output, error, token);
                    if (added != 0) return added;
                    await output.WriteLineAsync("Framework SOURCE configured. Choose collect for a fresh two-source run. Existing setup answers are historical; the active workspace configuration now controls source scope.");
                    continue;
                }
                if (choice.Trim() != "verify") throw Fail("CHOICE_INVALID");
                store.Dispose();
                return await WorkspaceCommand.RunAsync(["verify", "--root", root], output, error, token);
            }
        }
        var native = store.ReadProject(id).Native ?? throw Fail("NATIVE_CONFIG_MISSING");
        var path = WebFormsReviewPreflightCommand.Child(Path.GetDirectoryName(store.ProjectPath(id))!, "workspace-answers.local.json");
        string? previousHash = null;
        var answers = new WorkspaceWizardAnswers(id, native.Sha256, null, null, null, null);
        if (File.Exists(path))
        {
            try
            {
                var bytes = Read(path);
                previousHash = Hash(bytes);
                JsonDocument document;
                try { document = JsonDocument.Parse(bytes); }
                catch (JsonException exception) { throw Fail("ANSWERS_JSON_INVALID: " + path + "; " + exception.Message); }
                using var ownedDocument = document;
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
            catch (Exception exception) when (previousHash is not null && (exception is JsonException
                || exception is InvalidOperationException && exception.Message.StartsWith("WEBFORMS_WORKSPACE_WIZARD_ANSWERS_", StringComparison.Ordinal)))
            {
                await output.WriteLineAsync("Saved workspace answers cannot be reused: " + path);
                await output.WriteLineAsync("Reason: " + exception.Message);
                await output.WriteLineAsync("Archive only these workspace answers and answer workspace setup again, or pause? Website setup, SQL files and native runs stay unchanged. [restart/later]");
                var correction = await input.ReadLineAsync(token);
                if (correction is null || correction.Trim() == "later") return 2;
                if (correction.Trim() != "restart") throw Fail("CHOICE_INVALID");
                store.VerifyUnchanged();
                if (Hash(Read(path)) != previousHash) throw Fail("ANSWERS_CHANGED");
                var retained = path + ".retained-" + Guid.NewGuid().ToString("N");
                File.Move(path, retained, overwrite: false);
                await output.WriteLineAsync("Previous workspace answers retained at: " + retained);
                answers = new(id, native.Sha256, null, null, null, null);
                previousHash = null;
            }
        }
        await output.WriteLineAsync("Continue workspace setup in your existing root: " + root + ". No scan will start.");
        string? sizeBlockedManifest = null;
        string? lastSqlFailureManifest = null;
        string? lastSqlFailureReason = null;
        while (true)
        {
            if (answers.FrameworkRoot is null)
            {
                await output.WriteLineAsync("Your website source and compiled dependency DLLs are already selected. A DLL does not add its source checkout. Give the additional source repository folder if you want its implementation files scanned; use skip only to leave that source out for now.");
                var value = await Existing("Framework SOURCE repository folder if you have its checkout; type skip if you only have the DLL or do not want additional source, or later:", false, allowSkip: true);
                if (value is null) return 2;
                answers = answers with { FrameworkRoot = value, FrameworkTarget = value == "" ? "" : null }; await Save();
            }
            if (answers.FrameworkTarget is null)
            {
                var names = Directory.EnumerateFiles(answers.FrameworkRoot).Take(257).ToArray();
                var candidates = names.Length > 256 ? [] : names.Where(Supported).Order(StringComparer.Ordinal).ToArray();
                if (names.Length > 256) await output.WriteLineAsync("Root file preview limit reached; no partial candidate list shown. Nested projects are not inventoried.");
                var projects = candidates.Where(item => Path.GetExtension(item).ToLowerInvariant() is ".csproj" or ".vbproj" or ".fsproj").ToArray();
                var detected = projects.Length == 1 ? Path.GetFileName(projects[0]) : null;
                await output.WriteLineAsync("Source repository folder: " + JsonSerializer.Serialize(answers.FrameworkRoot)
                    + ". Select the PROJECT FILE inside it, not the folder or dependency DLL. For Library/Library.vbproj below this folder, enter Library/Library.vbproj. Solution selection is explicit and can include more projects.");
                for (var index = 0; index < candidates.Length; index++) await output.WriteLineAsync($"{index + 1}: {Path.GetFileName(candidates[index])}");
                while (true)
                {
                    await output.WriteLineAsync("Framework project/solution: choose a listed number, paste a file path relative to the source folder (absolute paths inside it also work), or type discovery (explicit fallback) or later"
                        + (detected is null ? ":" : "; Enter uses detected project " + JsonSerializer.Serialize(detected) + ":"));
                    var value = await input.ReadLineAsync(token);
                    if (value is null || value.Trim() == "later") return 2;
                    value = Unquote(value);
                    if (value.Length == 0 && detected is not null) value = detected;
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
                if (value == sizeBlockedManifest)
                {
                    await output.WriteLineAsync("That is the same size-blocked manifest. Re-entering its path cannot change the input limit. Setup paused; no scan or input was reset.");
                    return 2;
                }
                answers = answers with { SqlManifest = value }; await Save();
            }
            // Prefer the manifest's own directory. Recheck an older saved base first;
            // repair a missing-file base only when the manifest-relative bytes verify.
            var manifestBase = Path.GetDirectoryName(answers.SqlManifest!)!;
            SqlLocalCollectionInput? sqlPlan;
            var sqlBase = answers.SqlRoot ?? manifestBase;
            try
            {
                sqlPlan = TrySql(sqlBase);
                if (sqlPlan is null && answers.SqlRoot is not null) { sqlBase = manifestBase; sqlPlan = TrySql(sqlBase); }
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or JsonException or IOException or UnauthorizedAccessException)
            {
                await output.WriteLineAsync("Workspace SQL setup could not verify the selected manifest: " + answers.SqlManifest);
                await output.WriteLineAsync("Reason: " + exception.Message);
                var syntaxLimit = exception.Data["sqlSyntaxComplexityLimit"] as SqlSyntaxComplexityLimit;
                if (syntaxLimit is not null)
                    await output.WriteLineAsync("SQL parser guard: " + syntaxLimit.Metric + "; observed=" + syntaxLimit.Observed
                        + "; limit=" + syntaxLimit.Limit + "; file " + syntaxLimit.FilePath + ":" + syntaxLimit.Line);
                if (answers.SqlManifest == lastSqlFailureManifest && exception.Message == lastSqlFailureReason)
                {
                    await output.WriteLineAsync("The same SQL validation failure remains after that action. No input is edited automatically. Setup paused instead of repeating the question; website setup and runs are preserved.");
                    return 2;
                }
                lastSqlFailureManifest = answers.SqlManifest;
                lastSqlFailureReason = exception.Message;
                if (exception.Data["sqlLocalDatabaseContext"] is SqlLocalDatabaseContext databaseContext)
                {
                    await output.WriteLineAsync("SQL database context: manifest " + JsonSerializer.Serialize(databaseContext.ManifestDatabase)
                        + "; declaration " + (databaseContext.SqlDatabase is null ? "multiple distinct databases" : JsonSerializer.Serialize(databaseContext.SqlDatabase))
                        + "; file " + databaseContext.Span.FilePath + ":" + databaseContext.Span.StartLine);
                    if (!databaseContext.WholeInputInspected)
                    {
                        await output.WriteLineAsync("Inspect the whole SQL input in streamed mode before offering a database repair? [inspect/later]");
                        if ((await input.ReadLineAsync(token))?.Trim() != "inspect") return 2;
                        var inspected = SqlServerLocalDefinitionsCommand.CreateStreamedManifest(answers.SqlManifest!, sqlBase);
                        answers = answers with { SqlManifest = inspected, SqlRoot = sqlBase }; await Save();
                        continue;
                    }
                    if (databaseContext.DistinctDatabaseCount != 1 || databaseContext.SqlDatabase is null)
                    {
                        foreach (var declaration in databaseContext.Declarations ?? [])
                            await output.WriteLineAsync("SQL USE " + JsonSerializer.Serialize(declaration.Database) + "; file "
                                + declaration.Span.FilePath + ":" + declaration.Span.StartLine);
                        await output.WriteLineAsync("The complete input declares multiple databases. One manifest cannot relabel them as one database. Setup paused; source files and existing runs are preserved.");
                        return 2;
                    }
                    await output.WriteLineAsync("Use the single SQL-declared database in a NEW manifest copy? Original SQL, hashes and manifest stay unchanged. [use-sql/later]");
                    if ((await input.ReadLineAsync(token))?.Trim() != "use-sql") return 2;
                    var repaired = SqlServerLocalDefinitionsCommand.CreateStreamedManifest(answers.SqlManifest!, sqlBase, databaseContext.SqlDatabase);
                    answers = answers with { SqlManifest = repaired, SqlRoot = sqlBase }; await Save();
                    await output.WriteLineAsync("Database context corrected in manifest copy: " + repaired);
                    continue;
                }
                var encodingCanStream = exception is System.Text.DecoderFallbackException
                    && SqlServerLocalDefinitionsCommand.IsOrdinaryManifest(answers.SqlManifest!);
                var syntaxCanStream = syntaxLimit?.Metric == "legacy-total-open-constructs"
                    && SqlServerLocalDefinitionsCommand.IsOrdinaryManifest(answers.SqlManifest!);
                if (exception is SqlLocalSizeLimitException || encodingCanStream || syntaxCanStream)
                {
                    sizeBlockedManifest = answers.SqlManifest;
                    await output.WriteLineAsync("SQL setup is blocked by an admission limit, parser profile or source encoding. Retry cannot change that; no workspace was created.");
                    var canStream = encodingCanStream || syntaxCanStream || exception is SqlLocalSizeLimitException size
                        && (size.Scope == "SQL-file" && size.LimitBytes == SqlServerDdlEvidence.MaxFileBytes
                        || size.Scope == "SQL-total" && size.LimitBytes == SqlServerDdlEvidence.MaxTotalBytes);
                    if (canStream)
                    {
                        await output.WriteLineAsync("Streamed definitions can read up to 8 GiB per file/total, using bounded GO batches and strict UTF-8 or BOM-marked UTF-16 decoding. All listed files are retained; no SQL is executed or truncated. Parser/evidence limits still apply.");
                        await output.WriteLineAsync("Use streamed mode, select a different prepared manifest, or pause? [stream/select/later]");
                    }
                    else await output.WriteLineAsync("Select a DIFFERENT prepared SQL manifest or pause? [select/later]");
                    var selection = await input.ReadLineAsync(token);
                    if (selection is null || selection.Trim() is "later" or "retry") return 2;
                    if (selection.Trim() == "stream" && canStream)
                    {
                        var changed = SqlServerLocalDefinitionsCommand.CreateStreamedManifest(answers.SqlManifest!, sqlBase);
                        await output.WriteLineAsync("Streamed manifest created; original preserved: " + changed);
                        answers = answers with { SqlManifest = changed, SqlRoot = sqlBase };
                        await Save();
                        continue;
                    }
                    if (selection.Trim() is not ("select" or "change")) throw Fail("CHOICE_INVALID");
                    answers = answers with { SqlManifest = null, SqlRoot = null };
                    await Save();
                    continue;
                }
                await output.WriteLineAsync("Website setup and completed native scans are preserved. Change the SQL manifest selection, retry after editing that file locally, or pause? [change/retry/later]");
                var correction = await input.ReadLineAsync(token);
                if (correction is null || correction.Trim() == "later") return 2;
                if (correction.Trim() == "retry") continue;
                if (correction.Trim() != "change") throw Fail("CHOICE_INVALID");
                answers = answers with { SqlManifest = null, SqlRoot = null };
                await Save();
                continue;
            }
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
            await output.WriteLineAsync(answers.FrameworkRoot == ""
                ? "SOURCE repositories to configure: 1 (website). Additional framework SOURCE is SKIPPED; its DLL does not scan its source. You can add it later with workspace -> add-framework."
                : "SOURCE repositories to configure: 2 (website and framework). Framework source: " + answers.FrameworkRoot + "; project/solution file: " + answers.FrameworkTarget);
            await output.WriteLineAsync("Collection scope: selected sources, the saved compiled publication and the selected offline SQL files. Existing selected-root lifecycle scans remain separate; they are not rerun or imported.");
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
                if (File.Exists(Path.Combine(run, "sql", "sql-evidence.sqlite")))
                {
                    await output.WriteLineAsync("SQL was collected separately on purpose: " + Path.Combine(run, "sql", "report.md"));
                    await output.WriteLineAsync("SQL SQLite: " + Path.Combine(run, "sql", "sql-evidence.sqlite") + ". Read it alongside combined.sqlite; separate does not mean missing. SQL was not executed and application-call binding is not automatic.");
                }
                else await output.WriteLineAsync("SQL evidence was not collected in this run; see START-HERE.md for its configured channels and coverage.");
                await output.WriteLineAsync("Earlier selected-root lifecycle evidence stays in its original run. This collection does not replace or import it. START-HERE.md explains which output answers which question.");
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
