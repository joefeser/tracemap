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
        AddTypeSymbolRelationshipFacts(projectPath, filePath, root, model, facts);
        AddMemberSymbolRelationshipFacts(projectPath, filePath, root, model, facts);
        AddPropertyAccessFacts(projectPath, filePath, root, model, facts);
        AddMethodInvocationFacts(repoPath, projectPath, filePath, root, model, facts, gaps);
        AddObjectCreationFacts(repoPath, projectPath, filePath, root, model, facts, gaps);
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
                || model.GetDeclaredSymbol(identifier) is not IFieldSymbol field
                || field.ContainingType.TypeKind == TypeKind.Error)
            {
                continue;
            }

            var isWithEvents = fieldDeclaration.Modifiers.Any(SyntaxKind.WithEventsKeyword);
            var properties = AddAssemblyProperties(
                AddSymbolProperties(
                    new SortedDictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["fieldName"] = field.Name,
                        ["fieldType"] = field.Type.ToDisplayString(SymbolFormat),
                        ["containingType"] = field.ContainingType?.ToDisplayString(SymbolFormat) ?? string.Empty,
                        ["declaredAccessibility"] = field.DeclaredAccessibility.ToString(),
                        ["isShared"] = field.IsShared().ToString(),
                        ["isWithEvents"] = isWithEvents ? "True" : "False"
                    },
                    "target",
                    field),
                field.ContainingAssembly,
                field.Type.ContainingAssembly);
            facts.Add(CreateSemanticFact(
                FactTypes.FieldDeclared,
                RuleIds.VisualBasicSemanticDeclarations,
                projectPath,
                filePath,
                identifier,
                sourceSymbol: field.ContainingType?.ToDisplayString(SymbolFormat),
                targetSymbol: field.ToDisplayString(SymbolFormat),
                contractElement: field.Name,
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
        IReadOnlyDictionary<string, string>? properties = null)
    {
        return new SemanticFactCandidate(
            factType,
            ruleId,
            EvidenceTiers.Tier1Semantic,
            ToEvidenceSpan(filePath, node),
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

    private static EvidenceSpan ToEvidenceSpan(string filePath, SyntaxNode node)
    {
        var span = node.SyntaxTree.GetLineSpan(node.Span);
        return new EvidenceSpan(
            FileInventory.NormalizeRelativePath(filePath),
            span.StartLinePosition.Line + 1,
            Math.Max(span.StartLinePosition.Line + 1, span.EndLinePosition.Line + 1),
            null,
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
