using TraceMap.Core;

namespace TraceMap.Cli;

/// <summary>Terminal adapter over shared wizard state. No customer execution in setup.</summary>
public static class WebFormsWizardCommand
{
    public const string Help = "tracemap webforms-review wizard [--root <configuration-folder>] [--continue] [--add-project]\n" +
        "Continue reuses saved answers; add-project explicitly registers another website. Exit 2 means setup paused, not a completed review.";

    public static async Task<int> RunAsync(string[] args, TextReader input, TextWriter output, TextWriter error,
        CancellationToken cancellationToken = default, WebFormsWizardProcessRunner? buildRunner = null,
        WebFormsWizardNativeRunner? nativeRunner = null)
    {
        try
        {
            if (args.FirstOrDefault() != "wizard") throw Invalid("ARGUMENT_INVALID");
            string? root = null;
            var resume = false;
            var add = false;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 1; i < args.Length; i++)
            {
                if (!seen.Add(args[i])) throw Invalid("ARGUMENT_INVALID");
                switch (args[i])
                {
                    case "--root" when i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal): root = args[++i]; break;
                    case "--continue": resume = true; break;
                    case "--add-project": add = true; break;
                    default: throw Invalid("ARGUMENT_INVALID");
                }
            }
            if (add && !resume) throw Invalid("ADD_REQUIRES_CONTINUE");
            root ??= await Ask("Where would you like the root of your TraceMap configuration to be?");
            if (!resume && Directory.Exists(root))
            {
                var choice = await Ask("This configuration folder exists. Continue where you left off, or choose a new folder? [continue/new]");
                if (choice == "continue") resume = true;
                else if (choice == "new") root = await Ask("New configuration folder (must not exist):");
                else throw Invalid("CHOICE_INVALID");
            }
            using var store = WebFormsWizardStore.Open(root, resume);
            string id;
            if (!resume || add || store.State.Projects.Length == 0)
            {
                id = await Ask("Project ID (lowercase letters, numbers and hyphens; start with a letter):");
                if (store.State.Projects.Any(project => project.Id == id)) throw Invalid("PROJECT_ALREADY_REGISTERED");
                var path = await Ask("Point me to your website folder, .sln, .csproj or .vbproj:");
                var webRoot = Path.GetExtension(path).Equals(".sln", StringComparison.OrdinalIgnoreCase)
                    ? await Ask("Which local website root in this solution should be configured?") : null;
                var target = WebFormsWizardTarget.Inspect(path, webRoot);
                await output.WriteLineAsync($"Detected {target.ProjectMode}; build tool candidate: {target.BuildTool}. This is not build validation.");
                foreach (var limitation in target.Limitations) await output.WriteLineAsync(limitation);
                var mode = await Ask("Use all forms or select a subset? [all/selected]");
                if (mode is not ("all" or "selected")) throw Invalid("CHOICE_INVALID");
                store.SaveProject(new(id, target.InputPath, target.WebRoot, target.ProjectMode, mode,
                    [], null, [], [], "forms"));
            }
            else
            {
                var projects = store.State.Projects;
                if (projects.Length == 1) id = projects[0].Id;
                else
                {
                    await output.WriteLineAsync("Configured projects: " + string.Join(", ", projects.Select(project => project.Id)));
                    id = await Ask("Which project would you like to continue?");
                }
            }
            cancellationToken.ThrowIfCancellationRequested();
            store.VerifyUnchanged();
            var selection = WebFormsWizardSelection.Advance(store, id);
            await output.WriteLineAsync($"Configuration root: {store.DirectoryPath}");
            if (selection.Paused)
            {
                await output.WriteLineAsync($"Edit {selection.EditFile}; keep only the forms you want. Then run the same wizard with --continue.");
                return 2;
            }
            var current = store.ReadProject(id);
            WebFormsWizardBuild.ValidateRetained(current);
            WebFormsWizardPublication.ValidateRetained(current);
            await output.WriteLineAsync($"Project {id}: saved step '{selection.Step}'. Setup is incomplete.");
            if (current.Step == "build")
            {
                var target = WebFormsWizardTarget.Inspect(current.InputPath, current.WebRoot);
                if (target.ProjectMode == "projectless")
                {
                    await output.WriteLineAsync("Compile this website externally with the appropriate Windows ASP.NET compiler. TraceMap will not launch it. Have you prepared a separate compiled publication? [ready/later]");
                    var answer = await input.ReadLineAsync(cancellationToken);
                    if (answer is null || answer.Trim() == "later") return 2;
                    if (answer.Trim() != "ready") throw Invalid("CHOICE_INVALID");
                    store.SaveProject(current with { Step = "publication" });
                    await output.WriteLineAsync("Manual publication declared; its contents and source binding still require validation. This is not build proof.");
                }
                else
                {
                    await output.WriteLineAsync("Run a build after reviewing the exact tool and command, or stop here? [run/later]");
                    var answer = await input.ReadLineAsync(cancellationToken);
                    if (answer is null || answer.Trim() == "later") return 2;
                    if (answer.Trim() != "run") throw Invalid("CHOICE_INVALID");
                    var tool = await Ask("Absolute path to the trusted dotnet or MSBuild executable:");
                    if (!Path.IsPathFullyQualified(tool)) throw Invalid("TOOL_PATH_ABSOLUTE_REQUIRED");
                    var plan = WebFormsWizardBuild.Plan(current, tool);
                    await output.WriteLineAsync("Executable: " + plan.Tool);
                    await output.WriteLineAsync("Working directory: " + plan.WorkingDirectory);
                    await output.WriteLineAsync("Version arguments: " + System.Text.Json.JsonSerializer.Serialize(plan.VersionArguments));
                    await output.WriteLineAsync("Build arguments: " + System.Text.Json.JsonSerializer.Serialize(plan.BuildArguments));
                    await output.WriteLineAsync("Building can execute project-defined tasks, restore dependencies, and modify source/bin/obj files. No website is launched. Type 'build' to authorize these commands; any other answer pauses:");
                    var consent = await input.ReadLineAsync(cancellationToken);
                    if (!await WebFormsWizardBuild.ExecuteAsync(store, id, plan, consent?.Trim() == "build",
                            RunBuildProcess, cancellationToken)) return 2;
                    await output.WriteLineAsync("Build command succeeded. This does not establish source-to-binary provenance; publication validation is next.");
                }
            }
            current = store.ReadProject(id);
            if (current.Step == "publication")
            {
                await output.WriteLineAsync("Where is the compiled/published website root (the folder containing bin, not bin itself)? Enter later to pause:");
                var folder = await input.ReadLineAsync(cancellationToken);
                if (folder is null || folder.Trim() == "later") return 2;
                var publication = WebFormsWizardPublication.Inspect(folder.Trim(), current.ProjectMode == "projectless");
                await output.WriteLineAsync("Managed DLL candidates (metadata inspected; not loaded):");
                for (var index = 0; index < publication.ManagedAssemblies.Length; index++)
                    await output.WriteLineAsync($"{index + 1}: {Path.GetRelativePath(publication.Root, publication.ManagedAssemblies[index])}");
                if (publication.NativeAssemblies.Length > 0) await output.WriteLineAsync($"{publication.NativeAssemblies.Length} native DLLs are excluded from managed scanning.");
                var answer = await Ask("Select primary website assemblies by comma-separated numbers, or type all:");
                var primary = answer == "all" ? publication.ManagedAssemblies : answer.Split(',').Select(value =>
                {
                    if (!int.TryParse(value.Trim(), out var number) || number < 1 || number > publication.ManagedAssemblies.Length) throw Invalid("ASSEMBLY_SELECTION_INVALID");
                    return publication.ManagedAssemblies[number - 1];
                }).ToArray();
                WebFormsWizardPublication.Configure(store, id, publication.Root, primary, []);
            }
            current = store.ReadProject(id);
            if (current.Step == "dependencies")
            {
                await output.WriteLineAsync("Additional managed dependency DLL paths, separated by semicolons (relative to publication root or absolute); type none or later:");
                var answer = await input.ReadLineAsync(cancellationToken);
                if (answer is null || answer.Trim() == "later") return 2;
                var dependencies = answer.Trim() == "none" ? [] : answer.Split(';', StringSplitOptions.TrimEntries);
                if (dependencies.Any(string.IsNullOrWhiteSpace)) throw Invalid("ASSEMBLY_SELECTION_INVALID");
                WebFormsWizardPublication.Configure(store, id, current.PublishedRoot!, current.PrimaryAssemblies, dependencies);
                store.SaveProject(store.ReadProject(id) with { Step = "configuration" });
            }
            current = store.ReadProject(id);
            if (current.Step == "configuration")
            {
                await output.WriteLineAsync("Create native configuration and verified copies of selected publication inputs under this configuration root? Source and publication originals will not be modified. [prepare/later]");
                var answer = await input.ReadLineAsync(cancellationToken);
                if (answer is null || answer.Trim() == "later")
                {
                    await output.WriteLineAsync($"Project {id}: saved step 'configuration'.");
                    return 2;
                }
                if (answer.Trim() != "prepare") throw Invalid("CHOICE_INVALID");
                await output.WriteLineAsync("Native configuration: " + await WebFormsWizardNative.ConfigureAsync(store, id, cancellationToken));
            }
            if (store.ReadProject(id).Native is not null) _ = await WebFormsWizardNative.ValidateAsync(store, id, cancellationToken);
            current = store.ReadProject(id);
            if (current.Step == "ready")
            {
                await output.WriteLineAsync("To attest that the selected compiled publication corresponds to this exact source commit and run TraceMap scan/reports, type the full commit below. This is your declaration, not build authenticity proof. Type later to pause:");
                await output.WriteLineAsync(current.Native!.SourceCommitSha);
                var answer = await input.ReadLineAsync(cancellationToken);
                if (answer is null || answer.Trim() == "later") return 2;
                return await WebFormsWizardExecution.RunAsync(store, id, answer.Trim(), output, error, cancellationToken, nativeRunner);
            }
            if (current.Step is "running" or "failed")
            {
                await output.WriteLineAsync("A prior attempt is retained; this does not mean a process is still running. Resume its native checkpoints? [resume/later]");
                var answer = await input.ReadLineAsync(cancellationToken);
                if (answer is null || answer.Trim() == "later") return 2;
                if (answer.Trim() != "resume") throw Invalid("CHOICE_INVALID");
                return await WebFormsWizardExecution.RunAsync(store, id, null, output, error, cancellationToken, nativeRunner);
            }
            if (current.Step == "completed") return await WebFormsWizardExecution.RunAsync(store, id, null, output, error, cancellationToken, nativeRunner);
            await output.WriteLineAsync($"Project {id}: saved step '{store.ReadProject(id).Step}'. No scan or customer website was executed.");
            return 2;

            async Task<WebFormsWizardProcessResult> RunBuildProcess(string tool, string directory,
                IReadOnlyList<string> arguments, CancellationToken token)
            {
                var result = await (buildRunner ?? WebFormsWizardProcess.RunAsync)(tool, directory, arguments, token);
                await output.WriteLineAsync("Private build output (not a shareable report):");
                await output.WriteLineAsync(result.StandardOutput);
                if (!string.IsNullOrEmpty(result.StandardError)) await output.WriteLineAsync(result.StandardError);
                await output.WriteLineAsync($"Build/tool process exit code: {result.ExitCode}");
                return result;
            }

            async Task<string> Ask(string prompt)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await output.WriteLineAsync(prompt);
                var answer = await input.ReadLineAsync(cancellationToken);
                if (answer is null) throw Invalid("INPUT_ENDED_CONTINUE_TO_RESUME");
                answer = answer.Trim();
                if (answer.Length == 0 || answer.Length > 4096 || answer.Any(char.IsControl)) throw Invalid("ANSWER_INVALID");
                return answer;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (WebFormsReviewPreflightCommand.PreflightException exception)
        { await error.WriteLineAsync("error: " + exception.Code + ". Generated attempts are retained for explicit repair."); return 1; }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or UnauthorizedAccessException
            or ArgumentException or System.Xml.XmlException or System.Text.Json.JsonException or System.ComponentModel.Win32Exception or BadImageFormatException)
        {
            var code = exception is InvalidOperationException && exception.Message.StartsWith("WEBFORMS_WIZARD_", StringComparison.Ordinal)
                ? exception.Message : "WEBFORMS_WIZARD_INPUT_OR_CONFIGURATION_INVALID";
            await error.WriteLineAsync("error: " + code + ". Existing saved projects and runs were not reset.");
            return 1;
        }
    }

    private static InvalidOperationException Invalid(string code) => new("WEBFORMS_WIZARD_" + code);
}
