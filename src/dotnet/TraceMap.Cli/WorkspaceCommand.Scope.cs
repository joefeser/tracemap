using System.Text.Json;

namespace TraceMap.Cli;

public static partial class WorkspaceCommand
{
    // An operational preview, not a source inventory, project evaluation or new evidence artifact.
    private static async Task WriteScopePreviewAsync(WorkspaceConfiguration configuration,
        IReadOnlyList<(WorkspaceRepository Config, string Root, string Head)> repositories,
        WorkspaceCompiledPlan? compiled, TextWriter output, CancellationToken token)
    {
        var selected = repositories.Select(item => item.Config.Id).ToHashSet(StringComparer.Ordinal);
        await output.WriteLineAsync("Scope preview: selection is not a coverage proof; paths and fingerprints remain local.");
        await output.WriteLineAsync("Excluded repositories: " + Show(configuration.Repositories
            .Where(item => !selected.Contains(item.Id)).Select(item => item.Id).Order(StringComparer.Ordinal).ToArray()));
        foreach (var (repository, root, _) in repositories)
        {
            token.ThrowIfCancellationRequested();
            var native = compiled is not null && repository.Id == configuration.CompiledReview?.RepositoryId
                ? compiled.NativePlan.Configuration : null;
            var mode = native is not null ? "native-" + native.ProjectMode
                : repository.Solution is not null ? "solution"
                : repository.Projects.Length != 0 ? "projects" : "discovery";
            await output.WriteLineAsync($"Scope {repository.Id}: mode={mode}; root={Show(root)}");
            await output.WriteLineAsync($"  projects={Show(native?.ProjectRelativePaths ?? repository.Projects)}; solution={Show(native?.SolutionRelativePath ?? repository.Solution)}");
            await output.WriteLineAsync($"  include={Show(repository.Include ?? [])}; exclude={Show(repository.Exclude ?? [])}; targetFramework={Show(repository.TargetFramework)}");
            // Read at most 257 immediate names, never recurse or follow links. If the
            // cap is exceeded, do not present an enumeration-order-dependent subset.
            var names = Directory.EnumerateFileSystemEntries(root).Take(257).Select(Path.GetFileName).ToArray();
            if (names.Length > 256)
                await output.WriteLineAsync($"  warning WORKSPACE_ROOT_PREVIEW_LIMIT: {repository.Id}; root-only preview omitted above 256 entries; nested projects not inventoried.");
            else
            {
                var projects = names.Where(name => name is not null && IsProject(name)).Order(StringComparer.Ordinal).ToArray();
                var solutions = names.Where(name => name is not null && IsSolution(name)).Order(StringComparer.Ordinal).ToArray();
                await output.WriteLineAsync($"  root-only project names={Show(projects)}; solution names={Show(solutions)}; nested projects not inventoried.");
                if (native?.ProjectMode == "projectless" && projects.Length != 0)
                    await output.WriteLineAsync($"  warning WORKSPACE_PROJECTLESS_WITH_ROOT_PROJECT: {repository.Id}; explicit projectless mode coexists with project names; confirm intended application type.");
                if (native is null && mode == "discovery" && projects.Length == 0
                    && names.Any(name => string.Equals(name, "web.config", StringComparison.OrdinalIgnoreCase)))
                    await output.WriteLineAsync($"  warning WORKSPACE_WEBSITE_WITHOUT_ROOT_PROJECT: {repository.Id}; possible projectless website or nested projects; discovery does not establish semantic compilation.");
                if (mode == "discovery" && projects.Length + solutions.Length > 1)
                    await output.WriteLineAsync($"  warning WORKSPACE_DISCOVERY_SELECTION: {repository.Id}; multiple root project/solution names; select intended projects or solution explicitly.");
            }
            if (native is not null)
            {
                await output.WriteLineAsync($"  sourceFolders={Show(native.SourceFolders)}; pageMode={native.PageMode}; pages={Show(native.PageRelativePaths)}");
                await output.WriteLineAsync($"  publishedRoot={Show(native.PublishedRoot)}; primaryAssemblies={Show(native.PrimaryAssemblies)}; dependencyAssemblies={Show(native.DependencyAssemblies)}; pdbInputs={Show(native.PdbInputs)}; pageMaps={Show(native.PageMaps)}");
                await output.WriteLineAsync($"  warning WORKSPACE_PAGE_SCOPE_IS_NOT_REPOSITORY_SCOPE: {repository.Id}; all/selected pages cover declared native scope, not every project or historical compiler input.");
            }
            else if (mode == "solution")
                await output.WriteLineAsync($"  warning WORKSPACE_SOLUTION_SCOPE: {repository.Id}; solution selection does not establish inclusion of projects outside that solution.");
        }
        await output.WriteLineAsync($"SQL context: {Show(configuration.SqlEvidence?.RepositoryId)}; compiled context: {Show(configuration.CompiledReview?.RepositoryId)}; no automatic source/compiled/SQL call binding.");
    }

    private static string Show<T>(T value) => JsonSerializer.Serialize(value);
    private static bool IsProject(string path) => Path.GetExtension(path).ToLowerInvariant() is ".csproj" or ".vbproj" or ".fsproj";
    private static bool IsSolution(string path) => Path.GetExtension(path).ToLowerInvariant() is ".sln" or ".slnx";
}
