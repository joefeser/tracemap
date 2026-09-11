using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;

namespace TraceMap.Core;

// Visual Basic sibling front end for the foundation slice: deterministic
// .vbproj selection, MSBuildWorkspace loading, Visual Basic compilation
// orchestration, sanitized workspace/compilation gaps, and honest reduced
// coverage when loading or compilation fails. Compiler-resolved declarations,
// references, calls, and relationships are emitted by the later extraction
// slice (spec task 5); this slice claims only load/compilation observations.
public static class VisualBasicSemanticExtractor
{
    public static SemanticExtractionResult Extract(
        string repoPath,
        IReadOnlyList<FileInventoryItem> inventory,
        ScanOptions? options = null,
        IReadOnlyList<FileInventoryItem>? fullInventory = null,
        CancellationToken cancellationToken = default,
        ScanProgressReporter? progress = null)
    {
        options ??= new ScanOptions(repoPath, ".");
        cancellationToken.ThrowIfCancellationRequested();
        var facts = new List<SemanticFactCandidate>();
        var gaps = new List<SemanticFactCandidate>();
        var analyzedFiles = new HashSet<string>(StringComparer.Ordinal);
        var compilationInputFiles = new HashSet<string>(StringComparer.Ordinal);
        var projects = inventory
            .Where(item => item.Kind == "VisualBasicProject")
            .OrderBy(item => item.RelativePath, StringComparer.Ordinal)
            .ToArray();
        var solutions = inventory
            .Where(item => item.Kind == "Solution")
            .OrderBy(item => item.RelativePath, StringComparer.Ordinal)
            .ToArray();
        var useFullSourceInventory = options.ProjectPaths is { Count: > 0 }
            && options.IncludeGlobs is not { Count: > 0 };
        var vbFiles = (useFullSourceInventory ? fullInventory ?? inventory : inventory)
            .Where(item => FileInventory.IsVisualBasicKind(item.Kind))
            .ToArray();
        var sourcePathComparer = CSharpSemanticExtractor.CreateSourcePathComparer(repoPath);
        var compilationInputPaths = (fullInventory ?? inventory)
            .Where(item => FileInventory.IsVisualBasicKind(item.Kind))
            .Select(item => item.RelativePath)
            .OrderBy(path => path, StringComparer.Ordinal)
            .GroupBy(path => path, sourcePathComparer)
            .ToDictionary(group => group.Key, group => group.First(), sourcePathComparer);
        var selectedProjectPaths = projects
            .Select(item => item.RelativePath)
            .ToHashSet(sourcePathComparer);
        var excludeGlobs = (options.ExcludeGlobs ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToArray();

        if (projects.Length == 0)
        {
            if (vbFiles.Length > 0)
            {
                gaps.Add(CreateGap(
                    ".",
                    "No Visual Basic project or solution was found; semantic analysis is unavailable for inventoried Visual Basic files.",
                    "NoVisualBasicProjectOrSolution"));
                return new SemanticExtractionResult(
                    facts,
                    gaps,
                    Attempted: false,
                    ReducedCoverage: true,
                    AnalyzedFiles: analyzedFiles,
                    CompilationInputFiles: compilationInputFiles,
                    ProtectedSourceSpans: []);
            }

            return new SemanticExtractionResult(
                facts,
                gaps,
                Attempted: false,
                ReducedCoverage: false,
                AnalyzedFiles: analyzedFiles,
                CompilationInputFiles: compilationInputFiles,
                ProtectedSourceSpans: []);
        }

        if (!MsBuildRuntimeRegistration.TryRegister(out var registrationError))
        {
            gaps.Add(CreateGap(
                ".",
                $"Unable to register MSBuild for Roslyn Visual Basic semantic analysis: {registrationError}",
                "MSBuildRegistrationFailed"));
            return new SemanticExtractionResult(
                facts,
                gaps,
                Attempted: true,
                ReducedCoverage: true,
                AnalyzedFiles: analyzedFiles,
                CompilationInputFiles: compilationInputFiles,
                ProtectedSourceSpans: []);
        }

        RunRestoreIfRequested(repoPath, projects, solutions, options, gaps, cancellationToken);

        var workspaceProperties = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(options.TargetFramework))
        {
            workspaceProperties["TargetFramework"] = options.TargetFramework;
        }

        using var workspace = workspaceProperties.Count == 0
            ? MSBuildWorkspace.Create()
            : MSBuildWorkspace.Create(workspaceProperties);
        workspace.RegisterWorkspaceFailedHandler(args =>
        {
            gaps.Add(CreateGap(
                ".",
                $"MSBuildWorkspace {args.Diagnostic.Kind}: {args.Diagnostic.Message}",
                "WorkspaceDiagnostic"));
        });

        var loadedProjectPaths = new HashSet<string>(sourcePathComparer);
        var explicitlyExcludedSourcePaths = new HashSet<string>(sourcePathComparer);

        if (solutions.Length > 0)
        {
            foreach (var solutionItem in solutions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var solutionPath = Path.Combine(repoPath, solutionItem.RelativePath);
                try
                {
                    var solution = WaitOnWorkspace(
                        workspace.OpenSolutionAsync(solutionPath, cancellationToken: cancellationToken),
                        cancellationToken);
                    ExtractSolution(
                        repoPath,
                        solution,
                        compilationInputPaths,
                        excludeGlobs,
                        explicitlyExcludedSourcePaths,
                        facts,
                        gaps,
                        loadedProjectPaths,
                        compilationInputFiles,
                        options.ProjectPaths is { Count: > 0 } ? selectedProjectPaths : null,
                        cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex) when (IsWorkspaceFailure(ex))
                {
                    gaps.Add(CreateGap(
                        solutionItem.RelativePath,
                        $"Unable to load solution with MSBuildWorkspace: {ex.Message}",
                        "SolutionLoadFailed"));
                }
            }
        }

        var shouldLoadStandaloneProjects = options.SolutionPaths is not { Count: > 0 };
        var standaloneProjects = shouldLoadStandaloneProjects
            ? projects.Where(project => !loadedProjectPaths.Contains(project.RelativePath)).ToArray()
            : [];
        foreach (var projectItem in standaloneProjects)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var projectPath = Path.Combine(repoPath, projectItem.RelativePath);
            try
            {
                var project = WaitOnWorkspace(
                    workspace.OpenProjectAsync(projectPath, cancellationToken: cancellationToken),
                    cancellationToken);
                var filteredSolution = CSharpSemanticExtractor.RemoveExplicitlyExcludedSourceDocuments(
                    repoPath,
                    project.Solution,
                    excludeGlobs,
                    out var excludedPaths);
                explicitlyExcludedSourcePaths.UnionWith(excludedPaths);
                project = filteredSolution.GetProject(project.Id)
                    ?? throw new InvalidOperationException($"Filtered solution no longer contains project '{projectItem.RelativePath}'.");
                ExtractProject(
                    repoPath,
                    project,
                    compilationInputPaths,
                    facts,
                    gaps,
                    compilationInputFiles,
                    cancellationToken);
                loadedProjectPaths.Add(projectItem.RelativePath);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (IsWorkspaceFailure(ex))
            {
                gaps.Add(CreateGap(
                    projectItem.RelativePath,
                    $"Unable to load Visual Basic project with MSBuildWorkspace: {ex.Message}",
                    "ProjectLoadFailed",
                    projectItem.RelativePath));
            }
        }

        if (explicitlyExcludedSourcePaths.Count > 0)
        {
            var message = $"Configured scan scope omitted {explicitlyExcludedSourcePaths.Count} repository-local Visual Basic source document(s) from semantic compilation.";
            gaps.Add(new SemanticFactCandidate(
                FactTypes.AnalysisGap,
                RuleIds.VisualBasicSemanticWorkspace,
                EvidenceTiers.Tier4Unknown,
                new EvidenceSpan(
                    ".",
                    1,
                    1,
                    null,
                    "VisualBasicSemanticExtractor",
                    ScannerVersions.VisualBasicSemanticExtractor),
                Properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["coverageEffect"] = "reduces-semantic-coverage",
                    ["diagnosticCode"] = "ScanScopeExcludedSources",
                    ["diagnosticKind"] = "scan-scope",
                    ["excludedDocumentCount"] = explicitlyExcludedSourcePaths.Count.ToString(),
                    ["gapKind"] = "ScanScopeExcludedSources",
                    ["guidanceCode"] = "ReviewScanScope",
                    ["message"] = message,
                    ["messageHash"] = FactFactory.Hash(message, 32),
                    ["sanitization"] = "categorical-count"
                }));
        }

        return new SemanticExtractionResult(
            facts,
            gaps,
            Attempted: true,
            ReducedCoverage: gaps.Count > 0,
            AnalyzedFiles: analyzedFiles,
            ScopeReduced: explicitlyExcludedSourcePaths.Count > 0,
            CompilationInputFiles: compilationInputFiles,
            ProtectedSourceSpans: []);
    }

    private static void ExtractSolution(
        string repoPath,
        Solution solution,
        IReadOnlyDictionary<string, string> compilationInputPaths,
        IReadOnlyList<string> excludeGlobs,
        HashSet<string> explicitlyExcludedSourcePaths,
        List<SemanticFactCandidate> facts,
        List<SemanticFactCandidate> gaps,
        HashSet<string> loadedProjectPaths,
        HashSet<string> compilationInputFiles,
        IReadOnlySet<string>? selectedProjectPaths,
        CancellationToken cancellationToken)
    {
        solution = CSharpSemanticExtractor.RemoveExplicitlyExcludedSourceDocuments(
            repoPath,
            solution,
            excludeGlobs,
            out var excludedPaths);
        explicitlyExcludedSourcePaths.UnionWith(excludedPaths);
        var selectedProjects = solution.Projects
            .Where(project => string.Equals(project.Language, LanguageNames.VisualBasic, StringComparison.Ordinal))
            .OrderBy(project => CSharpSemanticExtractor.ToRelativePath(repoPath, project.FilePath), StringComparer.Ordinal)
            .Where(project => selectedProjectPaths is null
                || selectedProjectPaths.Contains(CSharpSemanticExtractor.ToRelativePath(repoPath, project.FilePath)))
            .ToArray();
        foreach (var project in selectedProjects)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ExtractProject(
                repoPath,
                project,
                compilationInputPaths,
                facts,
                gaps,
                compilationInputFiles,
                cancellationToken);
            if (!string.IsNullOrWhiteSpace(project.FilePath))
            {
                loadedProjectPaths.Add(CSharpSemanticExtractor.ToRelativePath(repoPath, project.FilePath));
            }
        }
    }

    private static void ExtractProject(
        string repoPath,
        Project project,
        IReadOnlyDictionary<string, string> compilationInputPaths,
        List<SemanticFactCandidate> facts,
        List<SemanticFactCandidate> gaps,
        HashSet<string> compilationInputFiles,
        CancellationToken cancellationToken)
    {
        var projectPath = CSharpSemanticExtractor.ToRelativePath(repoPath, project.FilePath);
        Compilation? compilation;
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            compilation = WaitOnWorkspace(project.GetCompilationAsync(cancellationToken), cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (IsWorkspaceFailure(ex))
        {
            gaps.Add(CreateGap(
                projectPath,
                $"Unable to create Roslyn compilation for the Visual Basic project: {ex.Message}",
                "CompilationCreateFailed",
                projectPath));
            return;
        }

        if (compilation is null)
        {
            gaps.Add(CreateGap(
                projectPath,
                "Roslyn returned no compilation for the Visual Basic project.",
                "CompilationMissing",
                projectPath));
            return;
        }

        AddCompilationDiagnostics(repoPath, projectPath, compilation, gaps, out var sourceErrorDiagnosticCount, out var injectedTemplateErrorCount);

        var documentCount = 0;
        var unavailableCompilationInputObserved = false;
        foreach (var document in project.Documents
            .OrderBy(document => CSharpSemanticExtractor.ToRelativePath(repoPath, document.FilePath), StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            documentCount++;
            var projection = CSharpSemanticExtractor.ToRelativePathProjection(repoPath, document.FilePath);
            if (projection.IsExternal || IsCompilerGeneratedDocument(document.FilePath))
            {
                continue;
            }

            if (compilationInputPaths.TryGetValue(projection.Path, out var canonicalCompilationInputPath))
            {
                compilationInputFiles.Add(canonicalCompilationInputPath);
            }
            else if (File.Exists(Path.Combine(repoPath, projection.Path.Replace('/', Path.DirectorySeparatorChar))))
            {
                // A repository-local Visual Basic compilation input that appeared
                // after initial inventory remains protected so source mutation
                // fails loudly.
                compilationInputFiles.Add(projection.Path);
            }
            else
            {
                unavailableCompilationInputObserved = true;
            }
        }

        if (unavailableCompilationInputObserved)
        {
            gaps.Add(CreateGap(
                projectPath,
                "A project-declared Visual Basic compilation input was unavailable in the captured source inventory.",
                "CompilationInputUnavailable",
                projectPath));
        }

        var errorDiagnosticCount = compilation.GetDiagnostics(cancellationToken)
            .Count(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
        facts.Add(new SemanticFactCandidate(
            FactTypes.VisualBasicProjectObserved,
            RuleIds.VisualBasicSemanticCompilation,
            EvidenceTiers.Tier2Structural,
            new EvidenceSpan(
                FileInventory.NormalizeRelativePath(projectPath),
                1,
                1,
                null,
                "VisualBasicSemanticExtractor",
                ScannerVersions.VisualBasicSemanticExtractor),
            ProjectPath: projectPath,
            TargetSymbol: projectPath,
            Properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["documentCount"] = documentCount.ToString(),
                ["errorDiagnosticCount"] = errorDiagnosticCount.ToString(),
                ["injectedTemplateErrorCount"] = injectedTemplateErrorCount.ToString(),
                ["language"] = "Visual Basic"
            }));
    }

    private static void AddCompilationDiagnostics(
        string repoPath,
        string projectPath,
        Compilation compilation,
        List<SemanticFactCandidate> gaps,
        out int sourceErrorDiagnosticCount,
        out int injectedTemplateErrorCount)
    {
        sourceErrorDiagnosticCount = 0;
        injectedTemplateErrorCount = 0;
        foreach (var diagnostic in compilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .OrderBy(diagnostic => CSharpSemanticExtractor.ToRelativePath(repoPath, diagnostic.Location.IsInSource
                ? diagnostic.Location.GetLineSpan().Path
                : "."), StringComparer.Ordinal)
            .ThenBy(diagnostic => diagnostic.Location.GetLineSpan().StartLinePosition.Line)
            .ThenBy(diagnostic => diagnostic.Id, StringComparer.Ordinal))
        {
            // The .NET SDK computes the MSBuildWorkspace design-time compiler
            // arguments before appending _MyType="Empty", so workspace loads of
            // standard VB projects get the compiler-injected My template, which
            // cannot bind against the .NET Core Microsoft.VisualBasic surface.
            // Real builds of the same project do not run this code. Diagnostics
            // from that template point at no source location and are excluded
            // from coverage reduction; they remain visible as counts on the
            // project observation fact.
            if (!diagnostic.Location.IsInSource && IsInjectedMyTemplateDiagnostic(diagnostic))
            {
                injectedTemplateErrorCount++;
                continue;
            }

            sourceErrorDiagnosticCount++;
            var lineSpan = diagnostic.Location.GetLineSpan();
            var filePath = diagnostic.Location.IsInSource
                ? CSharpSemanticExtractor.ToRelativePath(repoPath, lineSpan.Path)
                : projectPath ?? ".";
            gaps.Add(CreateGap(
                filePath,
                $"Compilation diagnostic {diagnostic.Id}: {diagnostic.GetMessage()}",
                "CompilationDiagnostic",
                projectPath,
                lineSpan.StartLinePosition.Line + 1,
                Math.Max(lineSpan.StartLinePosition.Line + 1, lineSpan.EndLinePosition.Line + 1),
                diagnostic.Id));
        }
    }

    private static bool IsInjectedMyTemplateDiagnostic(Diagnostic diagnostic)
    {
        var message = diagnostic.GetMessage();
        return message.Contains("Microsoft.VisualBasic.MyServices", StringComparison.Ordinal)
            || message.Contains("Microsoft.VisualBasic.Devices.Computer", StringComparison.Ordinal)
            || message.Contains("Microsoft.VisualBasic.ApplicationServices", StringComparison.Ordinal);
    }

    private static void RunRestoreIfRequested(
        string repoPath,
        IReadOnlyList<FileInventoryItem> projects,
        IReadOnlyList<FileInventoryItem> solutions,
        ScanOptions options,
        List<SemanticFactCandidate> gaps,
        CancellationToken cancellationToken)
    {
        if (!options.Restore)
        {
            return;
        }

        var targets = solutions.Count > 0
            ? solutions.Select(item => item.RelativePath)
            : projects.Select(item => item.RelativePath);
        foreach (var target in targets.OrderBy(path => path, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var (exitCode, message) = CSharpSemanticExtractor.RunDotnetRestore(repoPath, target);
            if (exitCode != 0)
            {
                gaps.Add(CreateGap(
                    target,
                    $"dotnet restore failed with exit code {exitCode}: {message}",
                    "RestoreFailed",
                    target.EndsWith(".vbproj", StringComparison.OrdinalIgnoreCase) ? target : null));
            }
        }
    }

    private static SemanticFactCandidate CreateGap(
        string filePath,
        string message,
        string gapKind,
        string? projectPath = null,
        int startLine = 1,
        int endLine = 1,
        string? diagnosticId = null)
    {
        var sanitized = BuildEnvironmentDiagnosticExtractor.SanitizeWorkspaceGap(gapKind, message, diagnosticId);
        var properties = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["coverageEffect"] = sanitized.CoverageEffect,
            ["diagnosticCode"] = sanitized.DiagnosticCode,
            ["diagnosticKind"] = sanitized.DiagnosticKind,
            ["gapKind"] = gapKind,
            ["guidanceCode"] = sanitized.GuidanceCode,
            ["message"] = sanitized.Message,
            ["messageHash"] = FactFactory.Hash(sanitized.Message, 32),
            ["sanitization"] = sanitized.Sanitization
        };
        if (!string.IsNullOrWhiteSpace(sanitized.DiagnosticId))
        {
            properties["diagnosticId"] = sanitized.DiagnosticId;
        }

        return new SemanticFactCandidate(
            FactTypes.AnalysisGap,
            RuleIds.VisualBasicSemanticWorkspace,
            EvidenceTiers.Tier4Unknown,
            new EvidenceSpan(
                FileInventory.NormalizeRelativePath(filePath),
                startLine,
                Math.Max(startLine, endLine),
                null,
                "VisualBasicSemanticExtractor",
                ScannerVersions.VisualBasicSemanticExtractor),
            projectPath,
            Properties: properties);
    }

    // Mirrors the C# extractor's generated-source boundary: SDK/designer
    // generated documents (typically under obj/) are part of the compilation
    // but are not protected compilation inputs, because design-time builds
    // rewrite them during the scan itself. Checked-in designer/generated files
    // remain protected through the inventory-level semantic-input snapshot.
    private static bool IsCompilerGeneratedDocument(string? fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
        {
            return false;
        }

        var fileName = Path.GetFileName(fullPath);
        return fileName.EndsWith(".g.vb", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".g.i.vb", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".generated.vb", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".designer.vb", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".AssemblyInfo.vb", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".AssemblyAttributes.vb", StringComparison.OrdinalIgnoreCase);
    }

    private static T WaitOnWorkspace<T>(Task<T> task, CancellationToken cancellationToken) =>
        task.WaitAsync(cancellationToken).GetAwaiter().GetResult();

    private static bool IsWorkspaceFailure(Exception ex)
    {
        return ex is not OperationCanceledException
            and not OutOfMemoryException
            and not StackOverflowException;
    }
}
