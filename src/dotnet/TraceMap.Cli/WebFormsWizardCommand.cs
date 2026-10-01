using TraceMap.Core;

namespace TraceMap.Cli;

/// <summary>Terminal adapter over shared wizard state. No customer execution in setup.</summary>
public static class WebFormsWizardCommand
{
    public const string Help = "tracemap webforms-review wizard [--root <configuration-folder>] [--continue] [--add-project]\n" +
        "Continue reuses saved answers; add-project explicitly registers another website. Exit 2 means setup paused, not a completed review.";

    public static async Task<int> RunAsync(string[] args, TextReader input, TextWriter output, TextWriter error,
        CancellationToken cancellationToken = default)
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
            await output.WriteLineAsync($"Project {id}: saved step '{selection.Step}'. Setup is incomplete; no build, scan or customer application was executed.");
            return 2;

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
        catch (Exception exception) when (exception is InvalidOperationException or IOException or UnauthorizedAccessException
            or ArgumentException or System.Xml.XmlException or System.Text.Json.JsonException)
        {
            var code = exception is InvalidOperationException && exception.Message.StartsWith("WEBFORMS_WIZARD_", StringComparison.Ordinal)
                ? exception.Message : "WEBFORMS_WIZARD_INPUT_OR_CONFIGURATION_INVALID";
            await error.WriteLineAsync("error: " + code + ". Existing saved projects and runs were not reset.");
            return 1;
        }
    }

    private static InvalidOperationException Invalid(string code) => new("WEBFORMS_WIZARD_" + code);
}
