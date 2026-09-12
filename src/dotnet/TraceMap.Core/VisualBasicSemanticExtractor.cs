using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;

namespace TraceMap.Core;

// Visual Basic front end: deterministic .vbproj selection, MSBuildWorkspace
// loading, Visual Basic compilation orchestration, compiler-resolved Tier1
// declaration/reference/call/construction/argument/relationship facts,
// sanitized workspace/compilation gaps, and honest reduced coverage when
// loading or compilation fails. Syntax-only fallback lives in
// VisualBasicSyntaxExtractor; the two are never merged by rule or tier.
public static class VisualBasicSemanticExtractor
{
    private const int MaxCallSiteSyntaxFallbackFactsPerDocument = 2000;
    // Language-aware display format mirroring the C# extractor's shape so VB
    // display strings stay directly comparable to C# ones.
    private static readonly SymbolDisplayFormat SymbolFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        memberOptions:
            SymbolDisplayMemberOptions.IncludeContainingType
            | SymbolDisplayMemberOptions.IncludeParameters
            | SymbolDisplayMemberOptions.IncludeExplicitInterface,
        parameterOptions:
            SymbolDisplayParameterOptions.IncludeType
            | SymbolDisplayParameterOptions.IncludeName
            | SymbolDisplayParameterOptions.IncludeParamsRefOut,
        miscellaneousOptions:
            SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers
            | SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

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
        var inventoriedVbPaths = vbFiles
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
                        inventoriedVbPaths,
                        compilationInputPaths,
                        excludeGlobs,
                        explicitlyExcludedSourcePaths,
                        facts,
                        gaps,
                        loadedProjectPaths,
                        analyzedFiles,
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
                    inventoriedVbPaths,
                    compilationInputPaths,
                    facts,
                    gaps,
                    analyzedFiles,
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
        IReadOnlyDictionary<string, string> inventoriedVbPaths,
        IReadOnlyDictionary<string, string> compilationInputPaths,
        IReadOnlyList<string> excludeGlobs,
        HashSet<string> explicitlyExcludedSourcePaths,
        List<SemanticFactCandidate> facts,
        List<SemanticFactCandidate> gaps,
        HashSet<string> loadedProjectPaths,
        HashSet<string> analyzedFiles,
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
                inventoriedVbPaths,
                compilationInputPaths,
                facts,
                gaps,
                analyzedFiles,
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
        IReadOnlyDictionary<string, string> inventoriedVbPaths,
        IReadOnlyDictionary<string, string> compilationInputPaths,
        List<SemanticFactCandidate> facts,
        List<SemanticFactCandidate> gaps,
        HashSet<string> analyzedFiles,
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
            if (projection.IsExternal)
            {
                continue;
            }

            var compilerGenerated = IsCompilerGeneratedDocument(document.FilePath);
            if (compilationInputPaths.TryGetValue(projection.Path, out var canonicalCompilationInputPath))
            {
                compilationInputFiles.Add(canonicalCompilationInputPath);
            }
            else if (!compilerGenerated
                && File.Exists(Path.Combine(repoPath, projection.Path.Replace('/', Path.DirectorySeparatorChar))))
            {
                // A repository-local Visual Basic compilation input that appeared
                // after initial inventory remains protected so source mutation
                // fails loudly.
                compilationInputFiles.Add(projection.Path);
            }
            else if (!compilerGenerated)
            {
                unavailableCompilationInputObserved = true;
            }

            if (compilerGenerated)
            {
                continue;
            }

            string? canonicalEvidencePath = null;
            if (!inventoriedVbPaths.TryGetValue(projection.Path, out canonicalEvidencePath))
            {
                continue;
            }

            ExtractDocument(
                repoPath,
                projectPath,
                document,
                compilation,
                facts,
                gaps,
                analyzedFiles,
                canonicalEvidencePath,
                cancellationToken);
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

    private static void ExtractDocument(
        string repoPath,
        string? projectPath,
        Document document,
        Compilation compilation,
        List<SemanticFactCandidate> facts,
        List<SemanticFactCandidate> gaps,
        HashSet<string> analyzedFiles,
        string? canonicalEvidencePath,
        CancellationToken cancellationToken)
    {
        if (!document.SupportsSyntaxTree || IsCompilerGeneratedDocument(document.FilePath))
        {
            return;
        }

        var pathProjection = CSharpSemanticExtractor.ToRelativePathProjection(repoPath, document.FilePath);
        var filePath = canonicalEvidencePath ?? pathProjection.Path;
        if (pathProjection.IsExternal)
        {
            gaps.Add(CreateGap(
                filePath,
                "Roslyn loaded a Visual Basic source document outside the repository. TraceMap retained semantic evidence under a deterministic synthetic identity; the original host-local path was omitted.",
                "ExternalSourcePathProjected",
                projectPath));
        }

        SyntaxTree? tree;
        SyntaxNode? root;
        try
        {
            tree = WaitOnWorkspace(document.GetSyntaxTreeAsync(cancellationToken), cancellationToken);
            root = tree is null ? null : tree.GetRoot(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (IsWorkspaceFailure(ex))
        {
            gaps.Add(CreateGap(filePath, $"Unable to read Visual Basic syntax tree for semantic analysis: {ex.Message}", "SyntaxTreeReadFailed", projectPath));
            return;
        }

        if (tree is null || root is null)
        {
            gaps.Add(CreateGap(filePath, "Roslyn returned no syntax tree for the Visual Basic document.", "SyntaxTreeMissing", projectPath));
            return;
        }

        var model = compilation.GetSemanticModel(tree, ignoreAccessibility: true);
        analyzedFiles.Add(filePath);
        AddTypeDeclarationFacts(projectPath, filePath, root, model, facts);
        AddMethodDeclarationFacts(projectPath, filePath, root, model, facts);
        AddPropertyDeclarationFacts(projectPath, filePath, root, model, facts);
        AddFieldDeclarationFacts(projectPath, filePath, root, model, facts);
        AddParameterDeclarationFacts(projectPath, filePath, root, model, facts);
        AddEventDeclarationFacts(projectPath, filePath, root, model, facts);
        AddEventCompositionFacts(projectPath, filePath, root, model, facts, gaps);
        AddTypeSymbolRelationshipFacts(projectPath, filePath, root, model, facts);
        AddMemberSymbolRelationshipFacts(projectPath, filePath, root, model, facts);
        AddPropertyAccessFacts(projectPath, filePath, root, model, facts);
        AddMethodInvocationFacts(repoPath, projectPath, filePath, root, model, facts, gaps);
        AddObjectCreationFacts(repoPath, projectPath, filePath, root, model, facts, gaps);
        AddAdoNetBoundaryFacts(projectPath, filePath, root, model, facts, gaps);
        AddExternalBoundaryFacts(projectPath, filePath, root, model, facts, gaps);
    }

    private static void AddTypeDeclarationFacts(
        string? projectPath,
        string filePath,
        SyntaxNode root,
        SemanticModel model,
        List<SemanticFactCandidate> facts)
    {
        foreach (var statement in root.DescendantNodes().OfType<TypeStatementSyntax>())
        {
            if (model.GetDeclaredSymbol(statement) is not INamedTypeSymbol symbol
                || symbol.TypeKind == TypeKind.Error)
            {
                continue;
            }

            var properties = AddSymbolProperties(
                new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["name"] = symbol.Name,
                    ["namespace"] = symbol.ContainingNamespace?.IsGlobalNamespace == false ? symbol.ContainingNamespace.ToDisplayString() : string.Empty,
                    ["typeKind"] = symbol.TypeKind.ToString()
                },
                "target",
                symbol);
            facts.Add(CreateSemanticFact(
                FactTypes.TypeDeclared,
                RuleIds.VisualBasicSemanticDeclarations,
                projectPath,
                filePath,
                statement,
                targetSymbol: symbol.ToDisplayString(SymbolFormat),
                properties: properties));
        }

        foreach (var statement in root.DescendantNodes().OfType<EnumStatementSyntax>())
        {
            if (model.GetDeclaredSymbol(statement) is not INamedTypeSymbol symbol
                || symbol.TypeKind == TypeKind.Error)
            {
                continue;
            }

            var properties = AddSymbolProperties(
                new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["name"] = symbol.Name,
                    ["namespace"] = symbol.ContainingNamespace?.IsGlobalNamespace == false ? symbol.ContainingNamespace.ToDisplayString() : string.Empty,
                    ["typeKind"] = symbol.TypeKind.ToString()
                },
                "target",
                symbol);
            facts.Add(CreateSemanticFact(
                FactTypes.TypeDeclared,
                RuleIds.VisualBasicSemanticDeclarations,
                projectPath,
                filePath,
                statement,
                targetSymbol: symbol.ToDisplayString(SymbolFormat),
                properties: properties));
        }

        foreach (var statement in root.DescendantNodes().OfType<DelegateStatementSyntax>())
        {
            if (model.GetDeclaredSymbol(statement) is not INamedTypeSymbol symbol
                || symbol.TypeKind != TypeKind.Delegate)
            {
                continue;
            }

            var properties = AddSymbolProperties(
                new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["name"] = symbol.Name,
                    ["namespace"] = symbol.ContainingNamespace?.IsGlobalNamespace == false ? symbol.ContainingNamespace.ToDisplayString() : string.Empty,
                    ["typeKind"] = symbol.TypeKind.ToString(),
                    ["invokeSignature"] = symbol.DelegateInvokeMethod?.ToDisplayString(SymbolFormat) ?? string.Empty
                },
                "target",
                symbol);
            facts.Add(CreateSemanticFact(
                FactTypes.TypeDeclared,
                RuleIds.VisualBasicSemanticDeclarations,
                projectPath,
                filePath,
                statement,
                targetSymbol: symbol.ToDisplayString(SymbolFormat),
                properties: properties));
        }
    }

    private static void AddMethodDeclarationFacts(
        string? projectPath,
        string filePath,
        SyntaxNode root,
        SemanticModel model,
        List<SemanticFactCandidate> facts)
    {
        foreach (var statement in root.DescendantNodes().OfType<MethodBaseSyntax>())
        {
            if (statement is DeclareStatementSyntax or AccessorStatementSyntax or OperatorStatementSyntax)
            {
                continue;
            }

            if (model.GetDeclaredSymbol(statement) is not IMethodSymbol method
                || method.MethodKind is MethodKind.PropertyGet or MethodKind.PropertySet or MethodKind.EventAdd or MethodKind.EventRemove or MethodKind.EventRaise)
            {
                continue;
            }

            var properties = AddAssemblyProperties(
                AddSymbolProperties(
                    new SortedDictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["containingType"] = method.ContainingType?.ToDisplayString(SymbolFormat) ?? string.Empty,
                        ["methodName"] = method.Name,
                        ["methodKind"] = method.MethodKind.ToString(),
                        ["isShared"] = method.IsShared().ToString(),
                        ["returnsVoid"] = method.ReturnsVoid ? "True" : "False"
                    },
                    "target",
                    method),
                method.ContainingAssembly,
                method.ReturnType.ContainingAssembly);
            facts.Add(CreateSemanticFact(
                FactTypes.MethodDeclared,
                RuleIds.VisualBasicSemanticDeclarations,
                projectPath,
                filePath,
                statement,
                sourceSymbol: method.ContainingType?.ToDisplayString(SymbolFormat),
                targetSymbol: method.ToDisplayString(SymbolFormat),
                contractElement: method.Name,
                properties: properties));
        }
    }

    private static void AddPropertyDeclarationFacts(
        string? projectPath,
        string filePath,
        SyntaxNode root,
        SemanticModel model,
        List<SemanticFactCandidate> facts)
    {
        foreach (var statement in root.DescendantNodes().OfType<PropertyStatementSyntax>())
        {
            if (model.GetDeclaredSymbol(statement) is not IPropertySymbol property
                || property.ContainingType.TypeKind == TypeKind.Error)
            {
                continue;
            }

            var properties = AddAssemblyProperties(
                AddSymbolProperties(
                    new SortedDictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["containingType"] = property.ContainingType?.ToDisplayString(SymbolFormat) ?? string.Empty,
                        ["propertyName"] = property.Name,
                        ["propertyType"] = property.Type.ToDisplayString(SymbolFormat),
                        ["isDefault"] = property.IsDefault() ? "True" : "False",
                        ["isReadOnly"] = property.IsReadOnly ? "True" : "False",
                        ["isWriteOnly"] = property.IsWriteOnly ? "True" : "False"
                    },
                    "target",
                    property),
                property.ContainingAssembly,
                property.Type.ContainingAssembly);
            facts.Add(CreateSemanticFact(
                FactTypes.PropertyDeclared,
                RuleIds.VisualBasicSemanticDeclarations,
                projectPath,
                filePath,
                statement,
                sourceSymbol: property.ContainingType?.ToDisplayString(SymbolFormat),
                targetSymbol: property.ToDisplayString(SymbolFormat),
                contractElement: property.Name,
                properties: properties));
        }
    }

    private static void AddFieldDeclarationFacts(
        string? projectPath,
        string filePath,
        SyntaxNode root,
        SemanticModel model,
        List<SemanticFactCandidate> facts)
    {
        foreach (var identifier in root.DescendantNodes().OfType<ModifiedIdentifierSyntax>())
        {
            if (identifier.Parent is not VariableDeclaratorSyntax
                || identifier.Ancestors().OfType<FieldDeclarationSyntax>().FirstOrDefault() is not { } fieldDeclaration
                || model.GetDeclaredSymbol(identifier) is not { } declared)
            {
                continue;
            }

            var field = declared as IFieldSymbol;
            var withEventsProperty = declared as IPropertySymbol;
            var containingType = declared.ContainingType;
            var fieldType = field?.Type ?? withEventsProperty?.Type;
            if (containingType is null || containingType.TypeKind == TypeKind.Error || fieldType is null)
            {
                continue;
            }

            var isWithEvents = fieldDeclaration.Modifiers.Any(SyntaxKind.WithEventsKeyword);
            var properties = AddAssemblyProperties(
                AddSymbolProperties(
                    new SortedDictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["fieldName"] = declared.Name,
                        ["fieldType"] = fieldType.ToDisplayString(SymbolFormat),
                        ["containingType"] = containingType.ToDisplayString(SymbolFormat),
                        ["declaredAccessibility"] = declared.DeclaredAccessibility.ToString(),
                        ["isShared"] = declared.IsStatic.ToString(),
                        ["semanticKind"] = declared.Kind.ToString(),
                        ["isWithEvents"] = isWithEvents ? "True" : "False"
                    },
                    "target",
                    declared),
                declared.ContainingAssembly,
                fieldType.ContainingAssembly);
            facts.Add(CreateSemanticFact(
                FactTypes.FieldDeclared,
                RuleIds.VisualBasicSemanticDeclarations,
                projectPath,
                filePath,
                identifier,
                sourceSymbol: containingType.ToDisplayString(SymbolFormat),
                targetSymbol: declared.ToDisplayString(SymbolFormat),
                contractElement: declared.Name,
                properties: properties));
        }
    }

    private static void AddParameterDeclarationFacts(
        string? projectPath,
        string filePath,
        SyntaxNode root,
        SemanticModel model,
        List<SemanticFactCandidate> facts)
    {
        foreach (var parameterSyntax in root.DescendantNodes().OfType<ParameterSyntax>())
        {
            if (model.GetDeclaredSymbol(parameterSyntax) is not IParameterSymbol parameter
                || parameter.ContainingSymbol is not IMethodSymbol
                || parameter.Type.TypeKind == TypeKind.Error)
            {
                continue;
            }

            var containingSymbol = parameter.ContainingSymbol;
            var properties = AddAssemblyProperties(
                AddSymbolProperties(
                    AddSymbolProperties(
                        new SortedDictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["parameterName"] = parameter.Name,
                            ["parameterType"] = parameter.Type.ToDisplayString(SymbolFormat),
                            ["parameterOrdinal"] = parameter.Ordinal.ToString(),
                            ["containingSymbol"] = containingSymbol.ToDisplayString(SymbolFormat),
                            ["isOptional"] = parameter.IsOptional ? "True" : "False",
                            ["isByRef"] = parameter.RefKind != RefKind.None ? "True" : "False"
                        },
                        "source",
                        containingSymbol),
                    "target",
                    parameter),
                containingSymbol.ContainingAssembly,
                parameter.Type.ContainingAssembly);
            facts.Add(CreateSemanticFact(
                FactTypes.ParameterDeclared,
                RuleIds.VisualBasicSemanticDeclarations,
                projectPath,
                filePath,
                parameterSyntax,
                sourceSymbol: containingSymbol.ToDisplayString(SymbolFormat),
                targetSymbol: parameter.ToDisplayString(SymbolFormat),
                contractElement: parameter.Name,
                properties: properties));
        }
    }

    private static void AddEventDeclarationFacts(
        string? projectPath,
        string filePath,
        SyntaxNode root,
        SemanticModel model,
        List<SemanticFactCandidate> facts)
    {
        foreach (var statement in root.DescendantNodes().OfType<EventStatementSyntax>())
        {
            if (model.GetDeclaredSymbol(statement) is not IEventSymbol eventSymbol
                || eventSymbol.ContainingType.TypeKind == TypeKind.Error)
            {
                continue;
            }

            var properties = AddAssemblyProperties(
                AddSymbolProperties(
                    new SortedDictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["containingType"] = eventSymbol.ContainingType.ToDisplayString(SymbolFormat),
                        ["eventName"] = eventSymbol.Name,
                        ["eventType"] = eventSymbol.Type.ToDisplayString(SymbolFormat),
                        ["isShared"] = eventSymbol.IsShared().ToString()
                    },
                    "target",
                    eventSymbol),
                eventSymbol.ContainingAssembly,
                eventSymbol.Type.ContainingAssembly);
            facts.Add(CreateSemanticFact(
                FactTypes.EventDeclared,
                RuleIds.VisualBasicSemanticDeclarations,
                projectPath,
                filePath,
                statement,
                sourceSymbol: eventSymbol.ContainingType.ToDisplayString(SymbolFormat),
                targetSymbol: eventSymbol.ToDisplayString(SymbolFormat),
                contractElement: eventSymbol.Name,
                properties: properties));
        }
    }

    private static void AddEventCompositionFacts(
        string? projectPath,
        string filePath,
        SyntaxNode root,
        SemanticModel model,
        List<SemanticFactCandidate> facts,
        List<SemanticFactCandidate> gaps)
    {
        var retainedSites = 0;
        bool ReserveEventSite(SyntaxNode node)
        {
            if (retainedSites++ < MaxCallSiteSyntaxFallbackFactsPerDocument)
            {
                return true;
            }
            if (retainedSites == MaxCallSiteSyntaxFallbackFactsPerDocument + 1)
            {
                AddEventGap(gaps, projectPath, filePath, node, "VisualBasicEventCompositionBudgetExhausted");
            }
            return false;
        }

        foreach (var methodStatement in root.DescendantNodes().OfType<MethodStatementSyntax>())
        {
            if (methodStatement.HandlesClause is null)
            {
                continue;
            }

            if (model.GetDeclaredSymbol(methodStatement) is not IMethodSymbol handler)
            {
                foreach (var item in methodStatement.HandlesClause.Events)
                {
                    if (!ReserveEventSite(item)) return;
                    AddEventGap(gaps, projectPath, filePath, item, "UnresolvedVisualBasicHandlesHandler");
                }
                continue;
            }

            foreach (var item in methodStatement.HandlesClause.Events)
            {
                if (!ReserveEventSite(item)) return;
                var eventSymbol = model.GetSymbolInfo(item.EventMember).Symbol as IEventSymbol;
                var receiver = model.GetSymbolInfo(item.EventContainer).Symbol;
                var hasValidHandlesClause = !model.GetDiagnostics(item.Span)
                    .Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error
                        && diagnostic.Id is "BC30506" or "BC31029");
                if (!hasValidHandlesClause)
                {
                    AddEventGap(gaps, projectPath, filePath, item, "InvalidVisualBasicHandlesBinding");
                    continue;
                }
                if (eventSymbol is null || eventSymbol.Type.TypeKind == TypeKind.Error)
                {
                    AddEventGap(gaps, projectPath, filePath, item, "UnresolvedVisualBasicHandlesEvent");
                    continue;
                }

                facts.Add(CreateEventBindingFact(projectPath, filePath, item, handler, eventSymbol, receiver, "Handles", isAttach: true));
            }
        }

        foreach (var statement in root.DescendantNodes().OfType<AddRemoveHandlerStatementSyntax>())
        {
            if (!ReserveEventSite(statement)) return;
            var containingMethod = GetContainingMethod(statement, model);
            var operation = model.GetOperation(statement) as IEventAssignmentOperation;
            var hasValidDelegateConversion = !model.GetDiagnostics(statement.Span)
                .Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error);
            var eventSymbol = (operation?.EventReference as IEventReferenceOperation)?.Event
                ?? model.GetSymbolInfo(statement.EventExpression).Symbol as IEventSymbol
                ?? ResolveUniqueEventMember(statement.EventExpression, model);
            var handlerSymbol = !hasValidDelegateConversion || statement.DelegateExpression is LambdaExpressionSyntax
                ? null
                : ResolveEventHandler(operation?.HandlerValue)
                    ?? ResolveAddressOfTarget(statement.DelegateExpression, model)
                    ?? ResolveUniqueContainingTypeHandler(statement.DelegateExpression, containingMethod);
            var isAttach = operation?.Adds ?? statement.IsKind(SyntaxKind.AddHandlerStatement);
            if (containingMethod is null || eventSymbol is null || handlerSymbol is null)
            {
                AddEventGap(
                    gaps,
                    projectPath,
                    filePath,
                    statement,
                    eventSymbol is null ? "LateBoundOrUnresolvedVisualBasicEvent" : "UnsupportedVisualBasicEventHandlerDelegate");
                continue;
            }

            var receiver = statement.EventExpression is MemberAccessExpressionSyntax memberAccess
                ? model.GetSymbolInfo(memberAccess.Expression).Symbol
                : null;
            facts.Add(CreateEventBindingFact(
                projectPath,
                filePath,
                statement,
                handlerSymbol,
                eventSymbol,
                receiver,
                isAttach ? "AddHandler" : "RemoveHandler",
                isAttach,
                containingMethod));
        }

        foreach (var statement in root.DescendantNodes().OfType<RaiseEventStatementSyntax>())
        {
            if (!ReserveEventSite(statement)) return;
            var sourceMethod = GetContainingMethod(statement, model);
            var eventSymbol = model.GetSymbolInfo(statement.Name).Symbol as IEventSymbol;
            if (sourceMethod is null || eventSymbol is null)
            {
                AddEventGap(gaps, projectPath, filePath, statement, "UnresolvedVisualBasicRaiseEvent");
                continue;
            }

            var properties = AddSymbolProperties(
                AddSymbolProperties(
                    new SortedDictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["argumentCount"] = (statement.ArgumentList?.Arguments.Count ?? 0).ToString(),
                        ["eventName"] = eventSymbol.Name,
                        ["wiringKind"] = "RaiseEvent"
                    },
                    "source",
                    sourceMethod),
                "target",
                eventSymbol);
            facts.Add(CreateSemanticFact(
                FactTypes.VisualBasicEventRaised,
                RuleIds.VisualBasicSemanticEventWiring,
                projectPath,
                filePath,
                statement,
                sourceMethod.ToDisplayString(SymbolFormat),
                eventSymbol.ToDisplayString(SymbolFormat),
                eventSymbol.Name,
                properties,
                includeSnippetHash: true));
        }
    }

    private static SemanticFactCandidate CreateEventBindingFact(
        string? projectPath,
        string filePath,
        SyntaxNode node,
        IMethodSymbol handler,
        IEventSymbol eventSymbol,
        ISymbol? receiver,
        string wiringKind,
        bool isAttach,
        IMethodSymbol? wiringOwner = null)
    {
        var properties = AddSymbolProperties(
            AddSymbolProperties(
                new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["eventName"] = eventSymbol.Name,
                    ["handlerName"] = handler.Name,
                    ["isAttach"] = isAttach ? "True" : "False",
                    ["receiverKind"] = receiver?.Kind.ToString() ?? "Implicit",
                    ["receiverName"] = receiver?.Name ?? string.Empty,
                    ["wiringKind"] = wiringKind,
                    ["wiringOwner"] = wiringOwner?.ToDisplayString(SymbolFormat) ?? handler.ToDisplayString(SymbolFormat)
                },
                "source",
                handler),
            "target",
            eventSymbol);
        if (receiver is not null)
        {
            AddSymbolProperties(properties, "receiver", receiver);
            if (GetSymbolType(receiver) is { } receiverType)
            {
                properties["receiverTypeName"] = receiverType.Name;
                properties["receiverTypeDisplayName"] = receiverType.ToDisplayString(SymbolFormat);
                AddSymbolProperties(properties, "receiverType", receiverType);
            }
        }

        return CreateSemanticFact(
            FactTypes.VisualBasicEventBindingDeclared,
            RuleIds.VisualBasicSemanticEventWiring,
            projectPath,
            filePath,
            node,
            handler.ToDisplayString(SymbolFormat),
            eventSymbol.ToDisplayString(SymbolFormat),
            eventSymbol.Name,
            properties,
            includeSnippetHash: true);
    }

    private static ITypeSymbol? GetSymbolType(ISymbol symbol) => symbol switch
    {
        IFieldSymbol field => field.Type,
        IPropertySymbol property => property.Type,
        IParameterSymbol parameter => parameter.Type,
        ILocalSymbol local => local.Type,
        INamedTypeSymbol type => type,
        _ => null
    };

    private static IMethodSymbol? GetContainingMethod(SyntaxNode node, SemanticModel model)
    {
        var statement = node.Ancestors().OfType<MethodBlockBaseSyntax>().FirstOrDefault()?.BlockStatement;
#pragma warning disable RS1039 // VB Roslyn returns the declared method for concrete MethodBaseSyntax nodes.
        return statement is null ? null : model.GetDeclaredSymbol(statement) as IMethodSymbol;
#pragma warning restore RS1039
    }

    private static IEventSymbol? ResolveUniqueEventMember(ExpressionSyntax expression, SemanticModel model)
    {
        if (expression is not MemberAccessExpressionSyntax member)
        {
            return null;
        }

        var receiverType = model.GetTypeInfo(member.Expression).Type;
        return receiverType?.GetMembers(member.Name.Identifier.ValueText).OfType<IEventSymbol>().SingleOrDefault();
    }

    private static IMethodSymbol? ResolveAddressOfTarget(ExpressionSyntax expression, SemanticModel model)
    {
        var directInfo = model.GetSymbolInfo(expression);
        if (directInfo.Symbol is IMethodSymbol direct)
        {
            return direct;
        }
        if (directInfo.CandidateSymbols.OfType<IMethodSymbol>().ToArray() is [var directCandidate])
        {
            return directCandidate;
        }

        if (expression is UnaryExpressionSyntax addressOf
            && addressOf.IsKind(SyntaxKind.AddressOfExpression))
        {
            var operandInfo = model.GetSymbolInfo(addressOf.Operand);
            if (operandInfo.Symbol is IMethodSymbol operand)
            {
                return operand;
            }
            return operandInfo.CandidateSymbols.OfType<IMethodSymbol>().SingleOrDefault();
        }

        return null;
    }

    private static IMethodSymbol? ResolveEventHandler(IOperation? operation)
    {
        return operation switch
        {
            IAnonymousFunctionOperation => null,
            IDelegateCreationOperation { Target: IMethodReferenceOperation methodReference } => methodReference.Method,
            IDelegateCreationOperation { Target: IAnonymousFunctionOperation } => null,
            IMethodReferenceOperation methodReference => methodReference.Method,
            IConversionOperation conversion => ResolveEventHandler(conversion.Operand),
            IParenthesizedOperation parenthesized => ResolveEventHandler(parenthesized.Operand),
            _ => null
        };
    }

    private static IMethodSymbol? ResolveUniqueContainingTypeHandler(ExpressionSyntax expression, IMethodSymbol? containingMethod)
    {
        if (containingMethod?.ContainingType is null)
        {
            return null;
        }

        if (expression is not UnaryExpressionSyntax unary || !unary.IsKind(SyntaxKind.AddressOfExpression))
        {
            return null;
        }
        expression = unary.Operand;

        var name = expression switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
            _ => string.Empty
        };
        return name.Length == 0
            ? null
            : containingMethod.ContainingType.GetMembers(name).OfType<IMethodSymbol>().SingleOrDefault();
    }

    private static void AddEventGap(
        List<SemanticFactCandidate> gaps,
        string? projectPath,
        string filePath,
        SyntaxNode node,
        string gapKind)
    {
        var span = node.SyntaxTree.GetLineSpan(node.Span);
        gaps.Add(CreateGap(
            filePath,
            "A Visual Basic event composition site could not be resolved without guessing; only bounded source-location evidence was retained.",
            gapKind,
            projectPath,
            span.StartLinePosition.Line + 1,
            Math.Max(span.StartLinePosition.Line + 1, span.EndLinePosition.Line + 1),
            siteHash: FactFactory.Hash(node.ToString(), 32),
            ruleId: RuleIds.VisualBasicSemanticEventWiring));
    }

    private static void AddTypeSymbolRelationshipFacts(
        string? projectPath,
        string filePath,
        SyntaxNode root,
        SemanticModel model,
        List<SemanticFactCandidate> facts)
    {
        // In Visual Basic, Inherits and Implements are standalone statements
        // inside the type block, so the relationships are collected from those
        // statements and attributed to the containing type symbol.
        foreach (var inherits in root.DescendantNodes().OfType<InheritsStatementSyntax>())
        {
            var sourceType = GetContainingDeclaredType(inherits, model);
            if (sourceType is null)
            {
                continue;
            }

            foreach (var baseTypeSyntax in inherits.Types)
            {
                if (model.GetTypeInfo(baseTypeSyntax).Type is not INamedTypeSymbol targetType
                    || targetType.TypeKind == TypeKind.Error)
                {
                    continue;
                }

                var relationshipKind = sourceType.TypeKind == TypeKind.Interface && targetType.TypeKind == TypeKind.Interface
                    ? "ExtendsInterface"
                    : "InheritsFrom";
                facts.Add(CreateSymbolRelationshipFact(
                    projectPath,
                    filePath,
                    baseTypeSyntax,
                    model,
                    sourceType,
                    targetType,
                    relationshipKind,
                    "InheritsStatement"));
            }
        }

        foreach (var implements in root.DescendantNodes().OfType<ImplementsStatementSyntax>())
        {
            var sourceType = GetContainingDeclaredType(implements, model);
            if (sourceType is null)
            {
                continue;
            }

            foreach (var interfaceTypeSyntax in implements.Types)
            {
                if (model.GetTypeInfo(interfaceTypeSyntax).Type is not INamedTypeSymbol interfaceType
                    || interfaceType.TypeKind != TypeKind.Interface)
                {
                    continue;
                }

                facts.Add(CreateSymbolRelationshipFact(
                    projectPath,
                    filePath,
                    interfaceTypeSyntax,
                    model,
                    sourceType,
                    interfaceType,
                    "ImplementsInterface",
                    "ImplementsStatement"));
            }
        }
    }

    private static INamedTypeSymbol? GetContainingDeclaredType(SyntaxNode statement, SemanticModel model)
    {
        var typeBlock = statement.Ancestors().OfType<TypeBlockSyntax>().FirstOrDefault();
        if (typeBlock is null
            || model.GetDeclaredSymbol(typeBlock.BlockStatement) is not INamedTypeSymbol sourceType
            || sourceType.TypeKind == TypeKind.Error)
        {
            return null;
        }

        return sourceType;
    }

    private static void AddMemberSymbolRelationshipFacts(
        string? projectPath,
        string filePath,
        SyntaxNode root,
        SemanticModel model,
        List<SemanticFactCandidate> facts)
    {
        foreach (var statement in root.DescendantNodes().OfType<MethodStatementSyntax>())
        {
            if (model.GetDeclaredSymbol(statement) is not IMethodSymbol method)
            {
                continue;
            }

            AddOverrideRelationshipFact(projectPath, filePath, statement, model, facts, method, method.OverriddenMethod);
            AddInterfaceMemberRelationshipFacts(projectPath, filePath, statement, model, facts, method);
        }

        foreach (var statement in root.DescendantNodes().OfType<PropertyStatementSyntax>())
        {
            if (model.GetDeclaredSymbol(statement) is not IPropertySymbol property)
            {
                continue;
            }

            AddOverrideRelationshipFact(projectPath, filePath, statement, model, facts, property, property.OverriddenProperty);
            AddInterfaceMemberRelationshipFacts(projectPath, filePath, statement, model, facts, property);
        }
    }

    private static void AddOverrideRelationshipFact(
        string? projectPath,
        string filePath,
        SyntaxNode evidenceNode,
        SemanticModel model,
        List<SemanticFactCandidate> facts,
        ISymbol sourceMember,
        ISymbol? targetMember)
    {
        if (targetMember is null)
        {
            return;
        }

        facts.Add(CreateSymbolRelationshipFact(
            projectPath,
            filePath,
            evidenceNode,
            model,
            sourceMember,
            targetMember,
            "Overrides",
            "Override"));
    }

    private static void AddInterfaceMemberRelationshipFacts(
        string? projectPath,
        string filePath,
        SyntaxNode evidenceNode,
        SemanticModel model,
        List<SemanticFactCandidate> facts,
        ISymbol sourceMember)
    {
        var containingType = sourceMember.ContainingType;
        if (containingType is null)
        {
            return;
        }

        var seenTargets = new HashSet<string>(StringComparer.Ordinal);
        foreach (var interfaceType in containingType.AllInterfaces)
        {
            foreach (var interfaceMember in interfaceType.GetMembers())
            {
                ISymbol? implementation;
                try
                {
                    implementation = containingType.FindImplementationForInterfaceMember(interfaceMember);
                }
                catch (InvalidOperationException)
                {
                    // Ambiguous generic interface mapping is a compiler-level
                    // ambiguity; no relationship is claimed.
                    continue;
                }

                if (!SymbolEqualityComparer.Default.Equals(implementation, sourceMember))
                {
                    continue;
                }

                var targetIdentity = VisualBasicSymbolIdentityProvider.TryCreate(interfaceMember);
                if (targetIdentity is null || !seenTargets.Add(targetIdentity.SymbolId))
                {
                    continue;
                }

                facts.Add(CreateSymbolRelationshipFact(
                    projectPath,
                    filePath,
                    evidenceNode,
                    model,
                    sourceMember,
                    interfaceMember,
                    "ImplementsInterfaceMember",
                    "InterfaceImplementation"));
            }
        }
    }

    private static void AddPropertyAccessFacts(
        string? projectPath,
        string filePath,
        SyntaxNode root,
        SemanticModel model,
        List<SemanticFactCandidate> facts)
    {
        foreach (var memberAccess in root.DescendantNodes().OfType<MemberAccessExpressionSyntax>())
        {
            if (model.GetSymbolInfo(memberAccess).Symbol is IPropertySymbol property
                && property.ContainingType.TypeKind != TypeKind.Error)
            {
                facts.Add(CreatePropertyAccessFact(projectPath, filePath, memberAccess, model, property));
            }
        }

        // A compiler-resolved default-member index such as catalog(0) binds the
        // invocation to the default property; it stays Tier1 only when Roslyn
        // resolves exactly one member.
        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (model.GetSymbolInfo(invocation).Symbol is IPropertySymbol defaultProperty
                && defaultProperty.IsDefault()
                && defaultProperty.ContainingType.TypeKind != TypeKind.Error)
            {
                facts.Add(CreatePropertyAccessFact(projectPath, filePath, invocation, model, defaultProperty));
            }
        }
    }

    private static SemanticFactCandidate CreatePropertyAccessFact(
        string? projectPath,
        string filePath,
        SyntaxNode node,
        SemanticModel model,
        IPropertySymbol property)
    {
        var enclosing = model.GetEnclosingSymbol(node.SpanStart);
        var containingType = property.ContainingType?.ToDisplayString(SymbolFormat) ?? string.Empty;
        var properties = AddAssemblyProperties(
            AddSymbolProperties(
                AddSymbolProperties(
                    new SortedDictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["containingType"] = containingType,
                        ["propertyName"] = property.Name,
                        ["propertyType"] = property.Type.ToDisplayString(SymbolFormat),
                        ["isDefault"] = property.IsDefault() ? "True" : "False"
                    },
                    "source",
                    enclosing),
                "target",
                property),
            enclosing?.ContainingAssembly,
            property.ContainingAssembly);

        if (node is MemberAccessExpressionSyntax access)
        {
            var receiver = model.GetSymbolInfo(access.Expression).Symbol;
            properties["receiverSymbol"] = receiver?.ToDisplayString(SymbolFormat) ?? string.Empty;
            AddSymbolProperties(properties, "receiver", receiver);
        }

        if (node.Parent is AssignmentStatementSyntax assignment
            && assignment.Left == node
            && assignment.IsKind(SyntaxKind.SimpleAssignmentStatement))
        {
            var valueSymbol = model.GetSymbolInfo(assignment.Right).Symbol;
            properties["accessKind"] = "SimpleAssignmentTarget";
            properties["assignedValueSymbol"] = valueSymbol?.ToDisplayString(SymbolFormat) ?? string.Empty;
            AddSymbolProperties(properties, "assignedValue", valueSymbol);
        }

        return CreateSemanticFact(
            FactTypes.PropertyAccessed,
            RuleIds.VisualBasicSemanticPropertyAccess,
            projectPath,
            filePath,
            node,
            sourceSymbol: enclosing?.ToDisplayString(SymbolFormat),
            targetSymbol: property.ToDisplayString(SymbolFormat),
            contractElement: property.Name,
            properties: properties);
    }

    private static void AddMethodInvocationFacts(
        string repoPath,
        string? projectPath,
        string filePath,
        SyntaxNode root,
        SemanticModel model,
        List<SemanticFactCandidate> facts,
        List<SemanticFactCandidate> gaps)
    {
        var fallbackFactCount = 0;
        var fallbackTruncationReported = false;
        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            var symbolInfo = model.GetSymbolInfo(invocation);
            var operation = model.GetOperation(invocation);
            if ((symbolInfo.Symbol is not null && symbolInfo.Symbol is not IMethodSymbol)
                || operation is IPropertyReferenceOperation or IArrayElementReferenceOperation)
            {
                // Compiler-resolved non-method invocation shapes (including
                // default-property indexing and array access) are not calls.
                continue;
            }

            if (symbolInfo.Symbol is not IMethodSymbol method
                || method.ContainingType.TypeKind == TypeKind.Error)
            {
                if (fallbackFactCount <= MaxCallSiteSyntaxFallbackFactsPerDocument - 2)
                {
                    AddUnresolvedInvocationFallback(projectPath, filePath, invocation, model, facts);
                    var lineSpan = invocation.SyntaxTree.GetLineSpan(invocation.Span);
                    gaps.Add(CreateGap(
                        filePath,
                        "Visual Basic invocation target was unavailable; bounded syntax call-site evidence was retained.",
                        "CallSiteSemanticResolutionUnavailable",
                        projectPath,
                        lineSpan.StartLinePosition.Line + 1,
                        lineSpan.EndLinePosition.Line + 1,
                        siteHash: FactFactory.Hash(invocation.Expression.ToString(), 32)));
                    fallbackFactCount += 2;
                }
                else if (!fallbackTruncationReported)
                {
                    gaps.Add(CreateGap(filePath,
                        "Visual Basic unresolved invocation fallback reached its deterministic per-document fact budget; remaining call sites were not emitted.",
                        "CallSiteSyntaxFallbackBudgetExhausted",
                        projectPath));
                    fallbackTruncationReported = true;
                }
                continue;
            }

            var enclosing = model.GetEnclosingSymbol(invocation.SpanStart);
            var enclosingSymbol = enclosing?.ToDisplayString(SymbolFormat);
            var methodProperties = AddAssemblyProperties(
                AddSymbolProperties(
                    AddSymbolProperties(
                        new SortedDictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["containingType"] = method.ContainingType?.ToDisplayString(SymbolFormat) ?? string.Empty,
                            ["methodName"] = method.Name,
                            ["methodKind"] = method.MethodKind.ToString()
                        },
                        "source",
                        enclosing),
                    "target",
                    method),
                enclosing?.ContainingAssembly,
                method.ContainingAssembly);
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                var receiver = model.GetSymbolInfo(memberAccess.Expression).Symbol;
                methodProperties["receiverSymbol"] = receiver?.ToDisplayString(SymbolFormat) ?? string.Empty;
                methodProperties["receiverType"] = model.GetTypeInfo(memberAccess.Expression).Type?.ToDisplayString(SymbolFormat) ?? string.Empty;
                AddSymbolProperties(methodProperties, "receiver", receiver);
            }

            facts.Add(CreateSemanticFact(
                FactTypes.MethodInvoked,
                RuleIds.VisualBasicSemanticMethodInvocation,
                projectPath,
                filePath,
                invocation,
                sourceSymbol: enclosingSymbol,
                targetSymbol: method.ToDisplayString(SymbolFormat),
                contractElement: method.Name,
                properties: methodProperties));

            var callProperties = AddAssemblyProperties(
                AddSymbolProperties(
                    AddSymbolProperties(
                        new SortedDictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["callerSymbol"] = enclosingSymbol ?? string.Empty,
                            ["calleeSymbol"] = method.ToDisplayString(SymbolFormat),
                            ["calleeName"] = method.Name,
                            ["calleeContainingType"] = method.ContainingType?.ToDisplayString(SymbolFormat) ?? string.Empty,
                            ["callKind"] = "SemanticMethodInvocation"
                        },
                        "source",
                        enclosing),
                    "target",
                    method),
                enclosing?.ContainingAssembly,
                method.ContainingAssembly);

            facts.Add(CreateSemanticFact(
                FactTypes.CallEdge,
                RuleIds.VisualBasicSemanticCallGraph,
                projectPath,
                filePath,
                invocation,
                sourceSymbol: enclosingSymbol,
                targetSymbol: method.ToDisplayString(SymbolFormat),
                contractElement: method.Name,
                properties: callProperties));

            AddArgumentPassedFacts(
                repoPath,
                projectPath,
                filePath,
                model,
                facts,
                invocation.ArgumentList.Arguments,
                method,
                invocation,
                enclosing,
                enclosingSymbol,
                method.ToDisplayString(SymbolFormat),
                "SemanticMethodInvocation");
        }
    }

    private static void AddUnresolvedInvocationFallback(
        string? projectPath,
        string filePath,
        InvocationExpressionSyntax invocation,
        SemanticModel model,
        List<SemanticFactCandidate> facts)
    {
        var invocationName = GetSafeInvocationName(invocation.Expression);
        var enclosing = model.GetEnclosingSymbol(invocation.SpanStart);
        var callerName = enclosing?.ToDisplayString(SymbolFormat);
        var properties = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["expressionHash"] = FactFactory.Hash(invocation.Expression.ToString(), 32),
            ["expressionKind"] = invocation.Expression.Kind().ToString(),
            ["invocationName"] = invocationName,
            ["resolution"] = "unresolved"
        };
        facts.Add(CreateSyntaxFallbackFact(
            FactTypes.InvocationName,
            RuleIds.VisualBasicSyntaxInvocation,
            projectPath,
            filePath,
            invocation,
            sourceSymbol: callerName,
            targetSymbol: invocationName,
            properties: properties));
        facts.Add(CreateSyntaxFallbackFact(
            FactTypes.CallEdge,
            RuleIds.VisualBasicSyntaxCallGraph,
            projectPath,
            filePath,
            invocation,
            sourceSymbol: callerName,
            targetSymbol: invocationName,
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["callKind"] = "SyntaxInvocation",
                ["calleeName"] = invocationName,
                ["callerName"] = callerName ?? string.Empty,
                ["resolution"] = "unresolved"
            }));
    }

    private static void AddObjectCreationFacts(
        string repoPath,
        string? projectPath,
        string filePath,
        SyntaxNode root,
        SemanticModel model,
        List<SemanticFactCandidate> facts,
        List<SemanticFactCandidate> gaps)
    {
        var fallbackFactCount = 0;
        var fallbackTruncationReported = false;
        foreach (var creation in root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
        {
            if (model.GetTypeInfo(creation).Type is not INamedTypeSymbol type
                || type.TypeKind == TypeKind.Error)
            {
                continue;
            }

            var constructor = model.GetSymbolInfo(creation).Symbol as IMethodSymbol;
            if (constructor is null || constructor.ContainingType.TypeKind == TypeKind.Error)
            {
                if (fallbackFactCount <= MaxCallSiteSyntaxFallbackFactsPerDocument - 2)
                {
                    AddUnresolvedObjectCreationFallback(projectPath, filePath, creation, model, type, facts);
                    var lineSpan = creation.SyntaxTree.GetLineSpan(creation.Span);
                    gaps.Add(CreateGap(
                        filePath,
                        "Visual Basic constructor target was unavailable; bounded syntax call-site evidence was retained.",
                        "CallSiteSemanticResolutionUnavailable",
                        projectPath,
                        lineSpan.StartLinePosition.Line + 1,
                        lineSpan.EndLinePosition.Line + 1,
                        siteHash: FactFactory.Hash(creation.Type.ToString(), 32)));
                    fallbackFactCount += 2;
                }
                else if (!fallbackTruncationReported)
                {
                    gaps.Add(CreateGap(filePath,
                        "Visual Basic unresolved constructor fallback reached its deterministic per-document fact budget; remaining creation sites were not emitted.",
                        "CallSiteSyntaxFallbackBudgetExhausted",
                        projectPath));
                    fallbackTruncationReported = true;
                }
                continue;
            }

            var enclosing = model.GetEnclosingSymbol(creation.SpanStart);
            var enclosingSymbol = enclosing?.ToDisplayString(SymbolFormat);
            var createdType = type.ToDisplayString(SymbolFormat);
            var constructorSymbol = constructor.ToDisplayString(SymbolFormat);
            var assignedTo = GetAssignedVariableName(creation);

            var objectProperties = AddAssemblyProperties(
                AddSymbolProperties(
                    AddSymbolProperties(
                        AddSymbolProperties(
                            new SortedDictionary<string, string>(StringComparer.Ordinal)
                            {
                                ["callerSymbol"] = enclosingSymbol ?? string.Empty,
                                ["createdType"] = createdType,
                                ["createdTypeName"] = type.Name,
                                ["constructorSymbol"] = constructorSymbol,
                                ["assignedTo"] = assignedTo ?? string.Empty,
                                ["argumentCount"] = (creation.ArgumentList?.Arguments.Count ?? 0).ToString(),
                                ["creationKind"] = "SemanticObjectCreation"
                            },
                            "source",
                            enclosing),
                        "target",
                        type),
                    "constructor",
                    constructor),
                enclosing?.ContainingAssembly,
                type.ContainingAssembly);

            facts.Add(CreateSemanticFact(
                FactTypes.ObjectCreated,
                RuleIds.VisualBasicSemanticObjectCreation,
                projectPath,
                filePath,
                creation,
                sourceSymbol: enclosingSymbol,
                targetSymbol: createdType,
                contractElement: type.Name,
                properties: objectProperties));

            var callProperties = AddAssemblyProperties(
                AddSymbolProperties(
                    AddSymbolProperties(
                        new SortedDictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["callerSymbol"] = enclosingSymbol ?? string.Empty,
                            ["calleeSymbol"] = constructorSymbol,
                            ["calleeName"] = type.Name,
                            ["calleeContainingType"] = createdType,
                            ["callKind"] = "SemanticObjectCreation",
                            ["assignedTo"] = assignedTo ?? string.Empty
                        },
                        "source",
                        enclosing),
                    "target",
                    constructor),
                enclosing?.ContainingAssembly,
                type.ContainingAssembly);

            facts.Add(CreateSemanticFact(
                FactTypes.CallEdge,
                RuleIds.VisualBasicSemanticCallGraph,
                projectPath,
                filePath,
                creation,
                sourceSymbol: enclosingSymbol,
                targetSymbol: constructorSymbol,
                contractElement: type.Name,
                properties: callProperties));

            if (creation.ArgumentList is not null)
            {
                AddArgumentPassedFacts(
                    repoPath,
                    projectPath,
                    filePath,
                    model,
                    facts,
                    creation.ArgumentList.Arguments,
                    constructor,
                    creation,
                    enclosing,
                    enclosingSymbol,
                    constructorSymbol,
                    "SemanticObjectCreation");
            }
        }
    }

    private static void AddUnresolvedObjectCreationFallback(
        string? projectPath,
        string filePath,
        ObjectCreationExpressionSyntax creation,
        SemanticModel model,
        INamedTypeSymbol type,
        List<SemanticFactCandidate> facts)
    {
        var enclosing = model.GetEnclosingSymbol(creation.SpanStart);
        var callerName = enclosing?.ToDisplayString(SymbolFormat);
        var typeName = type.Name;
        var assignedTo = GetAssignedVariableName(creation) ?? string.Empty;
        facts.Add(CreateSyntaxFallbackFact(
            FactTypes.ObjectCreated,
            RuleIds.VisualBasicSyntaxObjectCreation,
            projectPath,
            filePath,
            creation,
            sourceSymbol: callerName,
            targetSymbol: typeName,
            contractElement: typeName,
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["argumentCount"] = (creation.ArgumentList?.Arguments.Count ?? 0).ToString(),
                ["assignedTo"] = assignedTo,
                ["callerName"] = callerName ?? string.Empty,
                ["createdType"] = typeName,
                ["creationKind"] = "SyntaxObjectCreation",
                ["resolution"] = "unresolved-constructor"
            }));
        facts.Add(CreateSyntaxFallbackFact(
            FactTypes.CallEdge,
            RuleIds.VisualBasicSyntaxCallGraph,
            projectPath,
            filePath,
            creation,
            sourceSymbol: callerName,
            targetSymbol: typeName,
            contractElement: typeName,
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["assignedTo"] = assignedTo,
                ["callKind"] = "SyntaxObjectCreation",
                ["calleeContainingType"] = typeName,
                ["calleeName"] = typeName,
                ["callerName"] = callerName ?? string.Empty,
                ["resolution"] = "unresolved-constructor"
            }));
    }

    private static void AddAdoNetBoundaryFacts(
        string? projectPath,
        string filePath,
        SyntaxNode root,
        SemanticModel model,
        List<SemanticFactCandidate> facts,
        List<SemanticFactCandidate> gaps)
    {
        foreach (var creation in root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
        {
            if (model.GetTypeInfo(creation).Type is not INamedTypeSymbol type
                || !IsAdoNetType(type, "System.Data.Common.DbCommand"))
            {
                continue;
            }

            var enclosing = model.GetEnclosingSymbol(creation.SpanStart);
            var commandReceiver = TryGetCreationAssignedSymbol(creation, model);
            var properties = AddAssemblyProperties(
                AddSymbolProperties(AddSymbolProperties(
                    AddSymbolProperties(
                        new SortedDictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["commandTextClassification"] = "unavailable",
                            ["frameworkFamily"] = GetAdoNetFrameworkFamily(type),
                            ["limitations"] = "Compiler-resolved command construction only; runtime execution, database identity, command effects, and success are not proven.",
                            ["typeName"] = type.ToDisplayString(SymbolFormat)
                        },
                        "source",
                        enclosing),
                        "target",
                        type),
                    "commandReceiver",
                    commandReceiver),
                enclosing?.ContainingAssembly,
                type.ContainingAssembly);

            if (TryGetCommandTextArgument(creation, model) is { } commandTextArgument)
            {
                var constant = model.GetConstantValue(commandTextArgument.Expression);
                if (constant.HasValue && constant.Value is string text)
                {
                    properties["commandTextClassification"] = "compile-time-constant-hashed";
                    properties["commandTextHash"] = FactFactory.Hash(text, 64);
                    properties["commandTextLength"] = text.Length.ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
                else
                {
                    properties["commandTextClassification"] = "dynamic-or-nonconstant";
                }
            }

            facts.Add(CreateSemanticFact(
                FactTypes.SqlCommandDetected,
                RuleIds.DatabaseSqlText,
                projectPath,
                filePath,
                creation,
                sourceSymbol: enclosing?.ToDisplayString(SymbolFormat),
                targetSymbol: type.ToDisplayString(SymbolFormat),
                contractElement: type.Name,
                properties: properties));
        }

        foreach (var assignment in root.DescendantNodes().OfType<AssignmentStatementSyntax>())
        {
            if (!assignment.IsKind(SyntaxKind.SimpleAssignmentStatement)
                || assignment.Left is not MemberAccessExpressionSyntax memberAccess
                || model.GetSymbolInfo(memberAccess).Symbol is not IPropertySymbol property
                || !property.Name.Equals("CommandType", StringComparison.OrdinalIgnoreCase)
                || !IsAdoNetType(property.ContainingType, "System.Data.Common.DbCommand"))
            {
                continue;
            }

            var receiver = model.GetSymbolInfo(memberAccess.Expression).Symbol;
            var assigned = model.GetSymbolInfo(assignment.Right).Symbol;
            var commandType = assigned is IFieldSymbol { ContainingType: { } enumType } field
                && GetMetadataName(enumType) == "System.Data.CommandType"
                ? field.Name
                : "unknown";
            var enclosing = model.GetEnclosingSymbol(assignment.SpanStart);
            var properties = AddSymbolProperties(
                AddSymbolProperties(
                    AddSymbolProperties(
                        new SortedDictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["commandTypeClassification"] = commandType,
                            ["configurationKind"] = "command-type-assignment",
                            ["frameworkFamily"] = GetAdoNetFrameworkFamily(property.ContainingType),
                            ["limitations"] = "Static assignment evidence only; branch feasibility, later mutation, runtime command type, and execution are not proven.",
                            ["storedProcedureCandidate"] = commandType == "StoredProcedure" ? "true" : "false"
                        },
                        "source",
                        enclosing),
                    "target",
                    property),
                "commandReceiver",
                receiver);
            facts.Add(CreateSemanticFact(
                FactTypes.SqlCommandDetected,
                RuleIds.DatabaseSqlText,
                projectPath,
                filePath,
                assignment,
                sourceSymbol: enclosing?.ToDisplayString(SymbolFormat),
                targetSymbol: property.ContainingType.ToDisplayString(SymbolFormat),
                contractElement: "CommandType",
                properties: properties));
        }

        var boundaryGapCount = 0;
        var boundaryGapBudgetReported = false;
        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            var name = GetSafeInvocationName(invocation.Expression);
            var method = model.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
            if (method is not null && TryClassifyAdoNetOperation(method, out var operationKind, out var resultKind))
            {
                var enclosing = model.GetEnclosingSymbol(invocation.SpanStart);
                var receiver = invocation.Expression is MemberAccessExpressionSyntax access
                    ? model.GetSymbolInfo(access.Expression).Symbol
                    : null;
                var properties = AddAssemblyProperties(
                    AddSymbolProperties(
                        AddSymbolProperties(
                            AddSymbolProperties(
                                new SortedDictionary<string, string>(StringComparer.Ordinal)
                                {
                                    ["coverageLabel"] = "bounded-static-call",
                                    ["frameworkFamily"] = GetAdoNetFrameworkFamily(method.ContainingType),
                                    ["limitations"] = "Compiler-resolved static call candidate only; runtime reachability, database identity, SQL, affected rows, returned data, and success are not proven.",
                                    ["methodName"] = method.Name,
                                    ["operationKind"] = operationKind,
                                    ["resultKind"] = resultKind,
                                    ["targetIdentityStatus"] = "boundary-only"
                                },
                                "source",
                                enclosing),
                            "target",
                            method),
                        "receiver",
                        receiver),
                    enclosing?.ContainingAssembly,
                    method.ContainingAssembly);
                facts.Add(CreateSemanticFact(
                    FactTypes.DatabaseOperationCandidate,
                    RuleIds.DatabaseOperationCallPattern,
                    projectPath,
                    filePath,
                    invocation,
                    sourceSymbol: enclosing?.ToDisplayString(SymbolFormat),
                    targetSymbol: method.ToDisplayString(SymbolFormat),
                    contractElement: operationKind,
                    properties: properties));
                continue;
            }

            if (method is not null && IsParameterCollectionMutation(method))
            {
                var enclosing = model.GetEnclosingSymbol(invocation.SpanStart);
                var commandReceiver = TryGetParameterCommandReceiver(invocation, model);
                var properties = AddSymbolProperties(
                    AddSymbolProperties(
                        AddSymbolProperties(
                            new SortedDictionary<string, string>(StringComparer.Ordinal)
                            {
                                ["configurationKind"] = "parameter-collection-mutation",
                                ["frameworkFamily"] = "ado-net",
                                ["limitations"] = "Compiler-resolved parameter collection mutation only; parameter names and values are not retained, and runtime command association or execution is not proven.",
                                ["parameterMutationMethod"] = method.Name
                            },
                            "source",
                            enclosing),
                        "target",
                        method),
                    "commandReceiver",
                    commandReceiver);
                facts.Add(CreateSemanticFact(
                    FactTypes.SqlCommandDetected,
                    RuleIds.DatabaseSqlText,
                    projectPath,
                    filePath,
                    invocation,
                    sourceSymbol: enclosing?.ToDisplayString(SymbolFormat),
                    targetSymbol: method.ContainingType.ToDisplayString(SymbolFormat),
                    contractElement: "Parameters",
                    properties: properties));
                continue;
            }

            if (method is null && IsPotentialAdoNetOperationName(name) && boundaryGapCount < 50)
            {
                var lineSpan = invocation.SyntaxTree.GetLineSpan(invocation.Span);
                gaps.Add(CreateGap(
                    filePath,
                    "A potential Visual Basic ADO.NET operation had no compiler-resolved target; no database boundary was claimed.",
                    "VisualBasicAdoNetTargetUnavailable",
                    projectPath,
                    lineSpan.StartLinePosition.Line + 1,
                    lineSpan.EndLinePosition.Line + 1,
                    siteHash: FactFactory.Hash(invocation.Expression.ToString(), 32),
                    ruleId: RuleIds.DatabaseOperationCallPattern));
                boundaryGapCount++;
            }
            else if (method is null && IsPotentialAdoNetOperationName(name) && !boundaryGapBudgetReported)
            {
                gaps.Add(CreateGap(
                    filePath,
                    "Visual Basic ADO.NET unresolved-target gap evidence reached its deterministic per-document budget; remaining candidate sites were not classified by the database rule.",
                    "VisualBasicAdoNetGapBudgetExhausted",
                    projectPath,
                    ruleId: RuleIds.DatabaseOperationCallPattern));
                boundaryGapBudgetReported = true;
            }
        }
    }

    private static void AddExternalBoundaryFacts(
        string? projectPath,
        string filePath,
        SyntaxNode root,
        SemanticModel model,
        List<SemanticFactCandidate> facts,
        List<SemanticFactCandidate> gaps)
    {
        var gapCount = 0;
        var budgetReported = false;
        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            var method = model.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
            if (method is not null && IsWebRequestCreate(method))
            {
                var enclosing = model.GetEnclosingSymbol(invocation.SpanStart);
                var properties = AddSymbolProperties(
                    AddSymbolProperties(
                        new SortedDictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["clientKind"] = "WebRequest",
                            ["coverageLabel"] = "bounded-static-http-client-construction",
                            ["limitations"] = "Compiler-resolved request construction only; a later response call, runtime destination, request contents, response, and success are not proven.",
                            ["surfaceKind"] = "http-client",
                            ["urlKind"] = "unavailable"
                        }, "source", enclosing), "target", method);
                if (TryGetHttpUriArgument(invocation, model, out var requestUri))
                {
                    var normalized = EndpointRouteNormalizer.Normalize(requestUri);
                    properties["normalizedPathKey"] = normalized.PathKey;
                    properties["normalizedPathTemplate"] = normalized.PathTemplate;
                    properties["urlHash"] = FactFactory.Hash(requestUri, 32);
                    properties["urlKind"] = "compile-time-constant-path";
                }
                else
                {
                    AddExternalGap(invocation, "VisualBasicHttpConstructionDestinationUnavailable", "A compiler-resolved WebRequest construction had no compile-time constant destination; later response calls cannot be correlated to a route identity.");
                }
                facts.Add(CreateSemanticFact(FactTypes.HttpClientCreated, RuleIds.HttpClientInvocation,
                    projectPath, filePath, invocation,
                    sourceSymbol: enclosing?.ToDisplayString(SymbolFormat),
                    targetSymbol: method.ToDisplayString(SymbolFormat),
                    contractElement: method.Name,
                    properties: properties));
                continue;
            }

            if (method is not null && TryClassifyHttpOperation(method, out var family, out var httpMethod))
            {
                var enclosing = model.GetEnclosingSymbol(invocation.SpanStart);
                var properties = AddAssemblyProperties(
                    AddSymbolProperties(
                        AddSymbolProperties(
                            new SortedDictionary<string, string>(StringComparer.Ordinal)
                            {
                                ["coverageLabel"] = "bounded-static-http-call",
                                ["httpMethod"] = httpMethod,
                                ["limitations"] = "Compiler-resolved outbound HTTP API call only; runtime reachability, destination host, request contents, response, and success are not proven.",
                                ["methodFamily"] = family,
                                ["methodName"] = method.Name,
                                ["surfaceKind"] = "http-client",
                                ["urlKind"] = "unavailable"
                            },
                            "source",
                            enclosing),
                        "target",
                        method),
                    enclosing?.ContainingAssembly,
                    method.ContainingAssembly);
                if (TryGetHttpUriArgument(invocation, model, out var uriText))
                {
                    var normalized = EndpointRouteNormalizer.Normalize(uriText);
                    properties["normalizedPathKey"] = normalized.PathKey;
                    properties["normalizedPathTemplate"] = normalized.PathTemplate;
                    properties["urlKind"] = "compile-time-constant-path";
                    properties["urlHash"] = FactFactory.Hash(uriText, 32);
                }
                else
                {
                    AddExternalGap(invocation, "VisualBasicHttpDestinationUnavailable", "A compiler-resolved Visual Basic HTTP call had no compile-time constant destination; no host or route identity was claimed.");
                }

                facts.Add(CreateSemanticFact(
                    FactTypes.HttpCallDetected,
                    RuleIds.HttpClientInvocation,
                    projectPath,
                    filePath,
                    invocation,
                    sourceSymbol: enclosing?.ToDisplayString(SymbolFormat),
                    targetSymbol: method.ToDisplayString(SymbolFormat),
                    contractElement: method.Name,
                    properties: properties));
                continue;
            }

            if (method is not null && IsConfigurationManagerGetSection(method))
            {
                if (TryGetConstantStringArgument(invocation, model, out var key))
                {
                    AddConfigBinding(invocation, model, "configuration-manager-section", key, method, facts, projectPath, filePath);
                }
                else
                {
                    AddExternalGap(invocation, "VisualBasicConfigurationKeyUnavailable", "A compiler-resolved ConfigurationManager section access had no compile-time constant key; no config binding was claimed.");
                }
                continue;
            }

            if (method is not null && TryClassifyLegacyServiceClient(method, out var serviceFamily))
            {
                AddExternalGap(
                    invocation,
                    serviceFamily == "wcf"
                        ? "VisualBasicWcfServiceMappingUnavailable"
                        : "VisualBasicAsmxServiceMappingUnavailable",
                    $"A compiler-resolved Visual Basic {serviceFamily.ToUpperInvariant()} client invocation was retained, but the existing service mapping extractor does not establish Visual Basic proxy-to-contract mappings; no service operation boundary was claimed.");
                continue;
            }

            if (model.GetOperation(invocation) is IPropertyReferenceOperation propertyReference
                && TryClassifyConfigurationManagerIndexer(propertyReference, out var configSourceKind))
            {
                if (TryGetConstantStringArgument(invocation, model, out var key))
                {
                    AddConfigBinding(invocation, model, configSourceKind, key, propertyReference.Property, facts, projectPath, filePath);
                }
                else
                {
                    AddExternalGap(invocation, "VisualBasicConfigurationKeyUnavailable", "A compiler-resolved ConfigurationManager indexer access had no compile-time constant key; no config binding was claimed.");
                }
                continue;
            }

            if (method is null && IsPotentialExternalOperationName(GetSafeInvocationName(invocation.Expression)))
            {
                AddExternalGap(invocation, "VisualBasicExternalBoundaryTargetUnavailable", "A potential Visual Basic external-boundary call had no compiler-resolved target; no HTTP or service boundary was claimed.");
            }
        }

        foreach (var access in root.DescendantNodes().OfType<MemberAccessExpressionSyntax>())
        {
            if (access.Expression is MemberAccessExpressionSyntax settings
                && settings.Name.Identifier.ValueText.Equals("Settings", StringComparison.OrdinalIgnoreCase)
                && settings.Expression is IdentifierNameSyntax myIdentifier
                && myIdentifier.Identifier.ValueText.Equals("My", StringComparison.OrdinalIgnoreCase)
                && model.GetSymbolInfo(access).Symbol is IPropertySymbol property)
            {
                AddConfigBinding(access, model, "my-settings-member", property.Name, property, facts, projectPath, filePath);
            }
        }

        void AddExternalGap(SyntaxNode node, string kind, string message)
        {
            if (gapCount < 50)
            {
                var span = node.SyntaxTree.GetLineSpan(node.Span);
                gaps.Add(CreateGap(filePath, message, kind, projectPath,
                    span.StartLinePosition.Line + 1, span.EndLinePosition.Line + 1,
                    siteHash: FactFactory.Hash(node.ToString(), 32), ruleId: RuleIds.VisualBasicSemanticExternalBoundary));
                gapCount++;
            }
            else if (!budgetReported)
            {
                gaps.Add(CreateGap(filePath,
                    "Visual Basic external-boundary gaps reached the deterministic per-document budget; remaining candidate sites were not classified.",
                    "VisualBasicExternalBoundaryGapBudgetExhausted", projectPath,
                    ruleId: RuleIds.VisualBasicSemanticExternalBoundary));
                budgetReported = true;
            }
        }
    }

    private static void AddConfigBinding(
        SyntaxNode node,
        SemanticModel model,
        string sourceKind,
        string key,
        ISymbol target,
        List<SemanticFactCandidate> facts,
        string? projectPath,
        string filePath)
    {
        var enclosing = model.GetEnclosingSymbol(node.SpanStart);
        var keyHash = FactFactory.Hash(key, 32);
        var properties = AddSymbolProperties(
            new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["configKeyHash"] = keyHash,
                ["configSourceKind"] = sourceKind,
                ["coverageLabel"] = "bounded-static-config-binding",
                ["limitations"] = "Compiler-resolved configuration access only; raw keys and values, provider precedence, reload behavior, availability, and runtime use are not proven.",
                ["surfaceKind"] = "package-config",
                ["targetAssemblyName"] = target.ContainingAssembly?.Identity.Name ?? string.Empty
            },
            "source",
            enclosing);
        facts.Add(CreateSemanticFact(
            FactTypes.ConfigBinding,
            RuleIds.VisualBasicSemanticConfigBinding,
            projectPath,
            filePath,
            node,
            sourceSymbol: enclosing?.ToDisplayString(SymbolFormat),
            targetSymbol: $"config-key:{keyHash}",
            contractElement: keyHash,
            properties: properties));
    }

    private static bool TryClassifyHttpOperation(IMethodSymbol method, out string family, out string httpMethod)
    {
        family = string.Empty;
        httpMethod = "UNKNOWN";
        var type = GetMetadataName(method.ContainingType.OriginalDefinition);
        if (type == "System.Net.Http.HttpClient" && method.ContainingAssembly.Identity.Name == "System.Net.Http")
        {
            family = "HttpClient";
            httpMethod = method.Name.StartsWith("Get", StringComparison.Ordinal) ? "GET"
                : method.Name.StartsWith("Post", StringComparison.Ordinal) ? "POST"
                : method.Name.StartsWith("Put", StringComparison.Ordinal) ? "PUT"
                : method.Name.StartsWith("Delete", StringComparison.Ordinal) ? "DELETE"
                : method.Name.StartsWith("Patch", StringComparison.Ordinal) ? "PATCH" : "UNKNOWN";
            return method.Name is "GetAsync" or "GetStringAsync" or "GetByteArrayAsync" or "GetStreamAsync"
                or "PostAsync" or "PutAsync" or "DeleteAsync" or "PatchAsync" or "SendAsync" or "Send";
        }

        if (IsAdoNetType(method.ContainingType, "System.Net.WebRequest")
            && method.Name is "GetResponse" or "GetResponseAsync")
        {
            family = "WebRequest";
            return true;
        }

        if (type == "System.Net.WebClient"
            && method.ContainingAssembly.Identity.Name == "System.Net.WebClient"
            && (method.Name.StartsWith("Download", StringComparison.Ordinal)
                || method.Name.StartsWith("Upload", StringComparison.Ordinal)
                || method.Name.StartsWith("OpenRead", StringComparison.Ordinal)
                || method.Name.StartsWith("OpenWrite", StringComparison.Ordinal)))
        {
            family = "WebClient";
            httpMethod = method.Name.StartsWith("Download", StringComparison.Ordinal) || method.Name.StartsWith("OpenRead", StringComparison.Ordinal)
                ? "GET" : "UNKNOWN";
            return true;
        }

        return false;
    }

    private static bool TryGetHttpUriArgument(InvocationExpressionSyntax invocation, SemanticModel model, out string value)
    {
        foreach (var argument in invocation.ArgumentList.Arguments.OfType<SimpleArgumentSyntax>())
        {
            var parameter = (model.GetOperation(argument) as IArgumentOperation)?.Parameter;
            if (parameter?.Name is not ("requestUri" or "requestUriString" or "address"))
            {
                continue;
            }
            var constant = model.GetConstantValue(argument.Expression);
            if (constant.HasValue && constant.Value is string text)
            {
                value = text;
                return true;
            }
            if (argument.Expression is ObjectCreationExpressionSyntax uriCreation
                && model.GetTypeInfo(uriCreation).Type is INamedTypeSymbol uriType
                && GetMetadataName(uriType.OriginalDefinition) == "System.Uri"
                && uriCreation.ArgumentList?.Arguments.FirstOrDefault() is SimpleArgumentSyntax uriArgument)
            {
                var uriConstant = model.GetConstantValue(uriArgument.Expression);
                if (uriConstant.HasValue && uriConstant.Value is string uriText)
                {
                    value = uriText;
                    return true;
                }
            }
        }
        value = string.Empty;
        return false;
    }

    private static bool TryGetConstantStringArgument(InvocationExpressionSyntax invocation, SemanticModel model, out string value)
    {
        foreach (var argument in invocation.ArgumentList.Arguments.OfType<SimpleArgumentSyntax>())
        {
            var constant = model.GetConstantValue(argument.Expression);
            if (constant.HasValue && constant.Value is string text)
            {
                value = text;
                return true;
            }
        }
        value = string.Empty;
        return false;
    }

    private static bool IsConfigurationManagerGetSection(IMethodSymbol method) =>
        GetMetadataName(method.ContainingType.OriginalDefinition) == "System.Configuration.ConfigurationManager"
        && method.ContainingAssembly.Identity.Name == "System.Configuration.ConfigurationManager"
        && method.Name == "GetSection";

    private static bool IsWebRequestCreate(IMethodSymbol method) =>
        GetMetadataName(method.ContainingType.OriginalDefinition) == "System.Net.WebRequest"
        && method.ContainingAssembly.Identity.Name == "System.Net.Requests"
        && method.Name is "Create" or "CreateHttp";

    private static bool TryClassifyConfigurationManagerIndexer(IPropertyReferenceOperation operation, out string sourceKind)
    {
        sourceKind = string.Empty;
        foreach (var child in DescendantsAndSelf(operation))
        {
            if (child is not IPropertyReferenceOperation property
                || GetMetadataName(property.Property.ContainingType.OriginalDefinition) != "System.Configuration.ConfigurationManager"
                || property.Property.ContainingAssembly.Identity.Name != "System.Configuration.ConfigurationManager")
            {
                continue;
            }
            if (property.Property.Name == "AppSettings")
            {
                sourceKind = "configuration-manager-app-settings";
                return true;
            }
            if (property.Property.Name == "ConnectionStrings")
            {
                sourceKind = "configuration-manager-connection-strings";
                return true;
            }
        }
        return false;
    }

    private static bool TryClassifyLegacyServiceClient(IMethodSymbol method, out string serviceFamily)
    {
        serviceFamily = string.Empty;
        for (var type = method.ContainingType; type is not null; type = type.BaseType)
        {
            var baseName = GetMetadataName(type.OriginalDefinition);
            var assemblyName = type.ContainingAssembly.Identity.Name;
            if (baseName == "System.ServiceModel.ClientBase`1" && assemblyName == "System.ServiceModel.Primitives")
            {
                serviceFamily = "wcf";
                return true;
            }
            if (baseName == "System.Web.Services.Protocols.SoapHttpClientProtocol" && assemblyName == "System.Web.Services")
            {
                serviceFamily = "asmx";
                return true;
            }
        }
        return false;
    }

    private static IEnumerable<IOperation> DescendantsAndSelf(IOperation operation)
    {
        yield return operation;
        foreach (var child in operation.ChildOperations)
        {
            foreach (var descendant in DescendantsAndSelf(child))
            {
                yield return descendant;
            }
        }
    }

    private static bool IsPotentialExternalOperationName(string name) =>
        new[] { "GetAsync", "PostAsync", "PutAsync", "DeleteAsync", "SendAsync", "GetResponse", "GetResponseAsync", "DownloadString", "UploadString" }
            .Contains(name, StringComparer.OrdinalIgnoreCase);

    private static bool TryClassifyAdoNetOperation(IMethodSymbol method, out string operationKind, out string resultKind)
    {
        operationKind = string.Empty;
        resultKind = "none";
        if (IsAdoNetType(method.ContainingType, "System.Data.Common.DbCommand"))
        {
            operationKind = method.Name switch
            {
                "ExecuteReader" or "ExecuteReaderAsync" => "select-candidate",
                "ExecuteScalar" or "ExecuteScalarAsync" => "scalar-candidate",
                "ExecuteNonQuery" or "ExecuteNonQueryAsync" => "execute-candidate",
                _ => string.Empty
            };
            resultKind = method.Name.StartsWith("ExecuteReader", StringComparison.Ordinal)
                ? "data-reader"
                : method.Name.StartsWith("ExecuteScalar", StringComparison.Ordinal)
                    ? "scalar"
                    : "row-count-or-none";
        }
        else if (IsAdoNetType(method.ContainingType, "System.Data.Common.DbDataAdapter")
            && method.Name == "Fill")
        {
            operationKind = "data-adapter-fill";
            resultKind = method.Parameters.Any(parameter => IsAdoNetType(parameter.Type, "System.Data.DataTable")
                    || parameter.Type is IArrayTypeSymbol array && IsAdoNetType(array.ElementType, "System.Data.DataTable"))
                ? "data-table"
                : method.Parameters.Any(parameter => IsAdoNetType(parameter.Type, "System.Data.DataSet"))
                    ? "data-set"
                    : "data-container-unknown";
        }

        return operationKind.Length > 0;
    }

    private static bool IsParameterCollectionMutation(IMethodSymbol method) =>
        IsAdoNetType(method.ContainingType, "System.Data.Common.DbParameterCollection")
        && method.Name is "Add" or "AddWithValue" or "AddRange";

    private static ISymbol? TryGetParameterCommandReceiver(InvocationExpressionSyntax invocation, SemanticModel model)
    {
        if (invocation.Expression is MemberAccessExpressionSyntax
            {
                Expression: MemberAccessExpressionSyntax parametersAccess
            }
            && model.GetSymbolInfo(parametersAccess).Symbol is IPropertySymbol property
            && property.Name.Equals("Parameters", StringComparison.OrdinalIgnoreCase)
            && IsAdoNetType(property.ContainingType, "System.Data.Common.DbCommand"))
        {
            return model.GetSymbolInfo(parametersAccess.Expression).Symbol;
        }

        return null;
    }

    private static ISymbol? TryGetCreationAssignedSymbol(ObjectCreationExpressionSyntax creation, SemanticModel model)
    {
        VariableDeclaratorSyntax? declarator = creation.Parent switch
        {
            AsNewClauseSyntax { Parent: VariableDeclaratorSyntax value } => value,
            EqualsValueSyntax { Parent: VariableDeclaratorSyntax value } => value,
            _ => null
        };
        if (declarator is not null)
        {
            var name = declarator.Names.FirstOrDefault();
            return name is null ? null : model.GetDeclaredSymbol(name);
        }

        return creation.Parent is AssignmentStatementSyntax assignment
            ? model.GetSymbolInfo(assignment.Left).Symbol
            : null;
    }

    private static SimpleArgumentSyntax? TryGetCommandTextArgument(ObjectCreationExpressionSyntax creation, SemanticModel model)
    {
        if (creation.ArgumentList is null)
        {
            return null;
        }

        foreach (var argument in creation.ArgumentList.Arguments.OfType<SimpleArgumentSyntax>())
        {
            var parameter = (model.GetOperation(argument) as IArgumentOperation)?.Parameter;
            if (parameter?.Name.Equals("commandText", StringComparison.OrdinalIgnoreCase) == true)
            {
                return argument;
            }
        }

        return null;
    }

    private static bool IsPotentialAdoNetOperationName(string name) =>
        new[] { "Fill", "ExecuteReader", "ExecuteReaderAsync", "ExecuteScalar", "ExecuteScalarAsync", "ExecuteNonQuery", "ExecuteNonQueryAsync" }
            .Contains(name, StringComparer.OrdinalIgnoreCase);

    private static bool IsAdoNetType(ITypeSymbol? type, string metadataName)
    {
        for (var current = type as INamedTypeSymbol; current is not null; current = current.BaseType)
        {
            if (GetMetadataName(current.OriginalDefinition) == metadataName)
            {
                return true;
            }
        }

        return false;
    }

    private static string GetAdoNetFrameworkFamily(ITypeSymbol type) =>
        type is INamedTypeSymbol named
            && GetMetadataName(named).StartsWith("Npgsql.", StringComparison.Ordinal)
                ? "npgsql"
                : "ado-net";

    private static string GetMetadataName(INamedTypeSymbol type)
    {
        var names = new Stack<string>();
        for (INamedTypeSymbol? current = type; current is not null; current = current.ContainingType)
        {
            names.Push(current.MetadataName);
        }
        var typeName = string.Join("+", names);
        var ns = type.ContainingNamespace?.ToDisplayString() ?? string.Empty;
        return ns.Length == 0 ? typeName : $"{ns}.{typeName}";
    }

    private static void AddArgumentPassedFacts(
        string repoPath,
        string? projectPath,
        string filePath,
        SemanticModel model,
        List<SemanticFactCandidate> facts,
        SeparatedSyntaxList<ArgumentSyntax> arguments,
        IMethodSymbol callee,
        SyntaxNode callSite,
        ISymbol? caller,
        string? callerSymbol,
        string calleeSymbol,
        string callKind)
    {
        for (var index = 0; index < arguments.Count; index++)
        {
            if (arguments[index] is not SimpleArgumentSyntax argument)
            {
                continue;
            }

            var parameter = ResolveParameter(model, argument);
            if (parameter is null)
            {
                continue;
            }

            var argumentSymbol = model.GetSymbolInfo(argument.Expression).Symbol;
            var argumentType = model.GetTypeInfo(argument.Expression).Type;
            var sourceLocation = GetSourceLocation(repoPath, argumentSymbol);
            var properties = AddAssemblyProperties(
                AddSymbolProperties(
                    AddSymbolProperties(
                        AddSymbolProperties(
                            AddSymbolProperties(
                                new SortedDictionary<string, string>(StringComparer.Ordinal)
                                {
                                    ["callerSymbol"] = callerSymbol ?? string.Empty,
                                    ["calleeSymbol"] = calleeSymbol,
                                    ["callKind"] = callKind,
                                    ["parameterOrdinal"] = parameter.Ordinal.ToString(),
                                    ["parameterName"] = parameter.Name,
                                    ["parameterType"] = parameter.Type.ToDisplayString(SymbolFormat),
                                    ["argumentOrdinal"] = index.ToString(),
                                    ["argumentExpressionKind"] = GetExpressionKind(argument.Expression),
                                    ["argumentExpressionHash"] = FactFactory.Hash(argument.Expression.ToString(), 32),
                                    ["argumentSymbol"] = argumentSymbol?.ToDisplayString(SymbolFormat) ?? string.Empty,
                                    ["argumentSymbolKind"] = argumentSymbol?.Kind.ToString() ?? string.Empty,
                                    ["argumentType"] = argumentType?.ToDisplayString(SymbolFormat) ?? string.Empty,
                                    ["argumentSourceFile"] = sourceLocation.FilePath ?? string.Empty,
                                    ["argumentSourceStartLine"] = sourceLocation.StartLine?.ToString() ?? string.Empty,
                                    ["argumentSourceEndLine"] = sourceLocation.EndLine?.ToString() ?? string.Empty
                                },
                                "source",
                                caller),
                            "target",
                            callee),
                        "parameter",
                        parameter),
                    "argument",
                    argumentSymbol),
                caller?.ContainingAssembly,
                callee.ContainingAssembly);
            properties["argumentAssemblyName"] = argumentSymbol?.ContainingAssembly?.Identity.Name
                ?? argumentType?.ContainingAssembly?.Identity.Name
                ?? string.Empty;
            properties["argumentAssemblyVersion"] = argumentSymbol?.ContainingAssembly?.Identity.Version?.ToString()
                ?? argumentType?.ContainingAssembly?.Identity.Version?.ToString()
                ?? string.Empty;

            facts.Add(CreateSemanticFact(
                FactTypes.ArgumentPassed,
                RuleIds.VisualBasicSemanticValueFlow,
                projectPath,
                filePath,
                argument,
                sourceSymbol: callerSymbol,
                targetSymbol: calleeSymbol,
                contractElement: parameter.Name,
                properties: properties));
        }
    }

    private static IParameterSymbol? ResolveParameter(SemanticModel model, SimpleArgumentSyntax argument)
    {
        for (var operation = model.GetOperation(argument) ?? model.GetOperation(argument.Expression);
             operation is not null;
             operation = operation.Parent)
        {
            if (operation is IArgumentOperation { Parameter: not null } boundArgument)
            {
                return boundArgument.Parameter;
            }
        }

        return null;
    }

    private static SemanticFactCandidate CreateSymbolRelationshipFact(
        string? projectPath,
        string filePath,
        SyntaxNode evidenceNode,
        SemanticModel model,
        ISymbol sourceSymbol,
        ISymbol targetSymbol,
        string relationshipKind,
        string relationshipSource)
    {
        var properties = AddAssemblyProperties(
            AddSymbolProperties(
                AddSymbolProperties(
                    new SortedDictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["relationshipKind"] = relationshipKind,
                        ["relationshipSource"] = relationshipSource,
                        ["sourceSymbol"] = sourceSymbol.ToDisplayString(SymbolFormat),
                        ["sourceSymbolKind"] = sourceSymbol.Kind.ToString(),
                        ["targetSymbol"] = targetSymbol.ToDisplayString(SymbolFormat),
                        ["targetSymbolKind"] = targetSymbol.Kind.ToString()
                    },
                    "source",
                    sourceSymbol),
                "target",
                targetSymbol),
            model.GetEnclosingSymbol(evidenceNode.SpanStart)?.ContainingAssembly ?? sourceSymbol.ContainingAssembly,
            targetSymbol.ContainingAssembly);

        return CreateSemanticFact(
            FactTypes.SymbolRelationship,
            RuleIds.VisualBasicSemanticSymbolRelationship,
            projectPath,
            filePath,
            evidenceNode,
            sourceSymbol: sourceSymbol.ToDisplayString(SymbolFormat),
            targetSymbol: targetSymbol.ToDisplayString(SymbolFormat),
            contractElement: relationshipKind,
            properties: properties);
    }

    private static SortedDictionary<string, string> AddSymbolProperties(
        SortedDictionary<string, string> properties,
        string prefix,
        ISymbol? symbol)
    {
        var identity = VisualBasicSymbolIdentityProvider.TryCreate(symbol);
        if (identity is null)
        {
            return properties;
        }

        properties[$"{prefix}SymbolId"] = identity.SymbolId;
        properties[$"{prefix}SymbolLanguage"] = identity.Language;
        properties[$"{prefix}SymbolKind"] = identity.SymbolKind;
        properties[$"{prefix}SymbolDisplayName"] = identity.DisplayName;
        properties[$"{prefix}SymbolAssemblyName"] = identity.AssemblyName ?? string.Empty;
        properties[$"{prefix}SymbolAssemblyVersion"] = identity.AssemblyVersion ?? string.Empty;
        properties[$"{prefix}ContainingSymbolId"] = identity.ContainingSymbolId ?? string.Empty;
        return properties;
    }

    private static SortedDictionary<string, string> AddAssemblyProperties(
        SortedDictionary<string, string> properties,
        IAssemblySymbol? callerAssembly,
        IAssemblySymbol? calleeAssembly)
    {
        properties["callerAssemblyName"] = callerAssembly?.Identity.Name ?? string.Empty;
        properties["callerAssemblyVersion"] = callerAssembly?.Identity.Version?.ToString() ?? string.Empty;
        properties["calleeAssemblyName"] = calleeAssembly?.Identity.Name ?? string.Empty;
        properties["calleeAssemblyVersion"] = calleeAssembly?.Identity.Version?.ToString() ?? string.Empty;
        return properties;
    }

    private static string GetExpressionKind(ExpressionSyntax expression)
    {
        return expression switch
        {
            LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.NothingLiteralExpression) => "NullLiteral",
            LiteralExpressionSyntax => "Literal",
            IdentifierNameSyntax => "Identifier",
            MemberAccessExpressionSyntax => "MemberAccess",
            InvocationExpressionSyntax => "Invocation",
            ObjectCreationExpressionSyntax => "ObjectCreation",
            MeExpressionSyntax => "Me",
            MyBaseExpressionSyntax => "MyBase",
            SingleLineLambdaExpressionSyntax or MultiLineLambdaExpressionSyntax => "Lambda",
            _ => expression.Kind().ToString()
        };
    }

    private static string? GetAssignedVariableName(ObjectCreationExpressionSyntax creation)
    {
        if (creation.Parent is AsNewClauseSyntax { Parent: VariableDeclaratorSyntax declarator })
        {
            return declarator.Names.FirstOrDefault()?.Identifier.ValueText;
        }

        if (creation.Parent is EqualsValueSyntax { Parent: VariableDeclaratorSyntax equalsDeclarator })
        {
            return equalsDeclarator.Names.FirstOrDefault()?.Identifier.ValueText;
        }

        if (creation.Parent is AssignmentStatementSyntax assignment)
        {
            return GetSafeExpressionName(assignment.Left);
        }

        return null;
    }

    private static string? GetSafeExpressionName(ExpressionSyntax expression)
    {
        return expression switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            MemberAccessExpressionSyntax memberAccess
                when GetSafeExpressionName(memberAccess.Expression) is { Length: > 0 } receiver =>
                $"{receiver}.{memberAccess.Name.Identifier.ValueText}",
            MeExpressionSyntax => "Me",
            MyBaseExpressionSyntax => "MyBase",
            _ => null
        };
    }

    private static (string? FilePath, int? StartLine, int? EndLine) GetSourceLocation(string repoPath, ISymbol? symbol)
    {
        var location = symbol?.Locations.FirstOrDefault(location => location.IsInSource);
        if (location is null)
        {
            return (null, null, null);
        }

        var span = location.GetLineSpan();
        return (
            CSharpSemanticExtractor.ToRelativePath(repoPath, span.Path),
            span.StartLinePosition.Line + 1,
            Math.Max(span.StartLinePosition.Line + 1, span.EndLinePosition.Line + 1));
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

    private static SemanticFactCandidate CreateSemanticFact(
        string factType,
        string ruleId,
        string? projectPath,
        string filePath,
        SyntaxNode node,
        string? sourceSymbol = null,
        string? targetSymbol = null,
        string? contractElement = null,
        IReadOnlyDictionary<string, string>? properties = null,
        bool includeSnippetHash = false)
    {
        return new SemanticFactCandidate(
            factType,
            ruleId,
            EvidenceTiers.Tier1Semantic,
            ToEvidenceSpan(filePath, node, includeSnippetHash),
            projectPath,
            sourceSymbol,
            targetSymbol,
            contractElement,
            properties,
            node.SpanStart,
            node.Span.Length);
    }

    private static SemanticFactCandidate CreateSyntaxFallbackFact(
        string factType,
        string ruleId,
        string? projectPath,
        string filePath,
        SyntaxNode node,
        string? sourceSymbol = null,
        string? targetSymbol = null,
        string? contractElement = null,
        IReadOnlyDictionary<string, string>? properties = null)
    {
        var span = node.SyntaxTree.GetLineSpan(node.Span);
        return new SemanticFactCandidate(
            factType,
            ruleId,
            EvidenceTiers.Tier3SyntaxOrTextual,
            new EvidenceSpan(
                FileInventory.NormalizeRelativePath(filePath),
                span.StartLinePosition.Line + 1,
                Math.Max(span.StartLinePosition.Line + 1, span.EndLinePosition.Line + 1),
                null,
                "VisualBasicSyntaxExtractor",
                ScannerVersions.VisualBasicSyntaxExtractor),
            projectPath,
            sourceSymbol,
            targetSymbol,
            contractElement,
            properties,
            node.SpanStart,
            node.Span.Length);
    }

    private static string GetSafeInvocationName(ExpressionSyntax expression) => expression switch
    {
        MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
        _ => $"unsupported-{expression.Kind()}-{FactFactory.Hash(expression.ToString(), 16)}"
    };

    private static EvidenceSpan ToEvidenceSpan(string filePath, SyntaxNode node, bool includeSnippetHash = false)
    {
        var span = node.SyntaxTree.GetLineSpan(node.Span);
        return new EvidenceSpan(
            FileInventory.NormalizeRelativePath(filePath),
            span.StartLinePosition.Line + 1,
            Math.Max(span.StartLinePosition.Line + 1, span.EndLinePosition.Line + 1),
            includeSnippetHash ? FactFactory.Hash(node.ToString(), 32) : null,
            "VisualBasicSemanticExtractor",
            ScannerVersions.VisualBasicSemanticExtractor);
    }

    private static SemanticFactCandidate CreateGap(
        string filePath,
        string message,
        string gapKind,
        string? projectPath = null,
        int startLine = 1,
        int endLine = 1,
        string? diagnosticId = null,
        string? siteHash = null,
        string? ruleId = null)
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
        if (!string.IsNullOrWhiteSpace(siteHash))
        {
            properties["siteHash"] = siteHash;
        }

        return new SemanticFactCandidate(
            FactTypes.AnalysisGap,
            ruleId ?? RuleIds.VisualBasicSemanticWorkspace,
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
    // remain protected through the inventory-level semantic-input snapshot and
    // are not analyzed for compiler-resolved facts.
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
            || fileName.Equals("AssemblyInfo.vb", StringComparison.OrdinalIgnoreCase)
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
