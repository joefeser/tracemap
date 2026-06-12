using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.MSBuild;

namespace TraceMap.Core;

public sealed record SemanticFactCandidate(
    string FactType,
    string RuleId,
    string EvidenceTier,
    EvidenceSpan Evidence,
    string? ProjectPath = null,
    string? SourceSymbol = null,
    string? TargetSymbol = null,
    string? ContractElement = null,
    IReadOnlyDictionary<string, string>? Properties = null);

public sealed record SemanticExtractionResult(
    IReadOnlyList<SemanticFactCandidate> Facts,
    IReadOnlyList<SemanticFactCandidate> GapFacts,
    bool Attempted,
    bool ReducedCoverage);

public static class CSharpSemanticExtractor
{
    private static readonly object MSBuildRegistrationLock = new();

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
            | SymbolDisplayParameterOptions.IncludeParamsRefOut
            | SymbolDisplayParameterOptions.IncludeDefaultValue,
        miscellaneousOptions:
            SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers
            | SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    public static SemanticExtractionResult Extract(string repoPath, IReadOnlyList<FileInventoryItem> inventory)
    {
        var facts = new List<SemanticFactCandidate>();
        var gaps = new List<SemanticFactCandidate>();
        var projects = inventory.Where(item => item.Kind == "Project").OrderBy(item => item.RelativePath, StringComparer.Ordinal).ToArray();
        var solutions = inventory.Where(item => item.Kind == "Solution").OrderBy(item => item.RelativePath, StringComparer.Ordinal).ToArray();
        var csharpFiles = inventory.Where(item => item.Kind == "CSharp").ToArray();

        if (projects.Length == 0 && csharpFiles.Length > 0)
        {
            gaps.Add(CreateGap(
                ".",
                "No C# project or solution was found; semantic analysis is unavailable for inventoried C# files.",
                "NoProjectOrSolution"));
            return new SemanticExtractionResult(facts, gaps, Attempted: false, ReducedCoverage: true);
        }

        if (projects.Length == 0)
        {
            return new SemanticExtractionResult(facts, gaps, Attempted: false, ReducedCoverage: false);
        }

        if (!TryRegisterMsBuild(gaps))
        {
            return new SemanticExtractionResult(facts, gaps, Attempted: true, ReducedCoverage: true);
        }

        using var workspace = MSBuildWorkspace.Create();
        workspace.RegisterWorkspaceFailedHandler(args =>
        {
            gaps.Add(CreateGap(
                ".",
                $"MSBuildWorkspace {args.Diagnostic.Kind}: {args.Diagnostic.Message}",
                "WorkspaceDiagnostic"));
        });

        var loadedProjectPaths = new HashSet<string>(StringComparer.Ordinal);
        var attempted = false;

        if (solutions.Length > 0)
        {
            foreach (var solutionItem in solutions)
            {
                attempted = true;
                var solutionPath = Path.Combine(repoPath, solutionItem.RelativePath);
                try
                {
                    var solution = workspace.OpenSolutionAsync(solutionPath).GetAwaiter().GetResult();
                    ExtractSolution(repoPath, solution, facts, gaps, loadedProjectPaths);
                }
                catch (Exception ex) when (IsWorkspaceException(ex))
                {
                    gaps.Add(CreateGap(
                        solutionItem.RelativePath,
                        $"Unable to load solution with MSBuildWorkspace: {ex.Message}",
                        "SolutionLoadFailed"));
                }
            }
        }

        foreach (var projectItem in projects.Where(project => !loadedProjectPaths.Contains(project.RelativePath)))
        {
            attempted = true;
            var projectPath = Path.Combine(repoPath, projectItem.RelativePath);
            try
            {
                var project = workspace.OpenProjectAsync(projectPath).GetAwaiter().GetResult();
                ExtractProject(repoPath, project, facts, gaps);
                loadedProjectPaths.Add(projectItem.RelativePath);
            }
            catch (Exception ex) when (IsWorkspaceException(ex))
            {
                gaps.Add(CreateGap(
                    projectItem.RelativePath,
                    $"Unable to load project with MSBuildWorkspace: {ex.Message}",
                    "ProjectLoadFailed"));
            }
        }

        return new SemanticExtractionResult(
            facts,
            gaps,
            Attempted: attempted,
            ReducedCoverage: gaps.Count > 0);
    }

    public static IReadOnlyList<CodeFact> MaterializeFacts(ScanManifest manifest, IEnumerable<SemanticFactCandidate> candidates)
    {
        return candidates
            .Select(candidate => FactFactory.Create(
                manifest,
                candidate.FactType,
                candidate.RuleId,
                candidate.EvidenceTier,
                candidate.Evidence,
                projectPath: candidate.ProjectPath,
                sourceSymbol: candidate.SourceSymbol,
                targetSymbol: candidate.TargetSymbol,
                contractElement: candidate.ContractElement,
                properties: candidate.Properties))
            .ToArray();
    }

    private static bool TryRegisterMsBuild(List<SemanticFactCandidate> gaps)
    {
        lock (MSBuildRegistrationLock)
        {
            if (MSBuildLocator.IsRegistered)
            {
                return true;
            }

            try
            {
                MSBuildLocator.RegisterDefaults();
                return true;
            }
            catch (Exception ex) when (ex is InvalidOperationException or FileNotFoundException)
            {
                gaps.Add(CreateGap(
                    ".",
                    $"Unable to register MSBuild for Roslyn semantic analysis: {ex.Message}",
                    "MSBuildRegistrationFailed"));
                return false;
            }
        }
    }

    private static void ExtractSolution(
        string repoPath,
        Solution solution,
        List<SemanticFactCandidate> facts,
        List<SemanticFactCandidate> gaps,
        HashSet<string> loadedProjectPaths)
    {
        foreach (var project in solution.Projects.OrderBy(project => ToRelativePath(repoPath, project.FilePath), StringComparer.Ordinal))
        {
            ExtractProject(repoPath, project, facts, gaps);
            if (!string.IsNullOrWhiteSpace(project.FilePath))
            {
                loadedProjectPaths.Add(ToRelativePath(repoPath, project.FilePath));
            }
        }
    }

    private static void ExtractProject(
        string repoPath,
        Project project,
        List<SemanticFactCandidate> facts,
        List<SemanticFactCandidate> gaps)
    {
        var projectPath = ToRelativePath(repoPath, project.FilePath);
        Compilation? compilation;
        try
        {
            compilation = project.GetCompilationAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex) when (IsWorkspaceException(ex))
        {
            gaps.Add(CreateGap(
                projectPath,
                $"Unable to create Roslyn compilation for project: {ex.Message}",
                "CompilationCreateFailed",
                projectPath));
            return;
        }

        if (compilation is null)
        {
            gaps.Add(CreateGap(projectPath, "Roslyn returned no compilation for project.", "CompilationMissing", projectPath));
            return;
        }

        AddCompilationDiagnostics(repoPath, projectPath, compilation, gaps);

        foreach (var document in project.Documents.OrderBy(document => ToRelativePath(repoPath, document.FilePath), StringComparer.Ordinal))
        {
            ExtractDocument(repoPath, projectPath, document, compilation, facts, gaps);
        }
    }

    private static void ExtractDocument(
        string repoPath,
        string? projectPath,
        Document document,
        Compilation compilation,
        List<SemanticFactCandidate> facts,
        List<SemanticFactCandidate> gaps)
    {
        if (!document.SupportsSyntaxTree || IsGeneratedSource(document.FilePath))
        {
            return;
        }

        var filePath = ToRelativePath(repoPath, document.FilePath);
        SyntaxTree? tree;
        SyntaxNode? root;
        try
        {
            tree = document.GetSyntaxTreeAsync().GetAwaiter().GetResult();
            root = tree is null ? null : tree.GetRoot();
        }
        catch (Exception ex) when (IsWorkspaceException(ex))
        {
            gaps.Add(CreateGap(filePath, $"Unable to read C# syntax tree for semantic analysis: {ex.Message}", "SyntaxTreeReadFailed", projectPath));
            return;
        }

        if (tree is null || root is null)
        {
            gaps.Add(CreateGap(filePath, "Roslyn returned no syntax tree for document.", "SyntaxTreeMissing", projectPath));
            return;
        }

        var model = compilation.GetSemanticModel(tree, ignoreAccessibility: true);
        AddTypeDeclarationFacts(projectPath, filePath, root, model, facts);
        AddPropertyAccessFacts(projectPath, filePath, root, model, facts);
        AddMethodInvocationFacts(projectPath, filePath, root, model, facts);
    }

    private static void AddTypeDeclarationFacts(
        string? projectPath,
        string filePath,
        SyntaxNode root,
        SemanticModel model,
        List<SemanticFactCandidate> facts)
    {
        foreach (var declaration in root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>())
        {
            if (model.GetDeclaredSymbol(declaration) is not INamedTypeSymbol symbol)
            {
                continue;
            }

            facts.Add(CreateSemanticFact(
                FactTypes.TypeDeclared,
                RuleIds.CSharpSemanticDeclarations,
                projectPath,
                filePath,
                declaration,
                targetSymbol: symbol.ToDisplayString(SymbolFormat),
                properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["name"] = symbol.Name,
                    ["namespace"] = symbol.ContainingNamespace?.IsGlobalNamespace == false ? symbol.ContainingNamespace.ToDisplayString() : string.Empty,
                    ["typeKind"] = symbol.TypeKind.ToString()
                }));
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
            if (model.GetSymbolInfo(memberAccess).Symbol is IPropertySymbol property)
            {
                facts.Add(CreatePropertyAccessFact(projectPath, filePath, memberAccess, model, property));
            }
        }

        foreach (var memberBinding in root.DescendantNodes().OfType<MemberBindingExpressionSyntax>())
        {
            if (model.GetSymbolInfo(memberBinding).Symbol is IPropertySymbol property)
            {
                facts.Add(CreatePropertyAccessFact(projectPath, filePath, memberBinding, model, property));
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
        var containingType = property.ContainingType?.ToDisplayString(SymbolFormat) ?? string.Empty;
        return CreateSemanticFact(
            FactTypes.PropertyAccessed,
            RuleIds.CSharpSemanticPropertyAccess,
            projectPath,
            filePath,
            node,
            sourceSymbol: GetEnclosingSymbol(model, node),
            targetSymbol: property.ToDisplayString(SymbolFormat),
            contractElement: property.Name,
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["containingType"] = containingType,
                ["propertyName"] = property.Name,
                ["propertyType"] = property.Type.ToDisplayString(SymbolFormat)
            });
    }

    private static void AddMethodInvocationFacts(
        string? projectPath,
        string filePath,
        SyntaxNode root,
        SemanticModel model,
        List<SemanticFactCandidate> facts)
    {
        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (model.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method)
            {
                continue;
            }

            facts.Add(CreateSemanticFact(
                FactTypes.MethodInvoked,
                RuleIds.CSharpSemanticMethodInvocation,
                projectPath,
                filePath,
                invocation,
                sourceSymbol: GetEnclosingSymbol(model, invocation),
                targetSymbol: method.ToDisplayString(SymbolFormat),
                contractElement: method.Name,
                properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["containingType"] = method.ContainingType?.ToDisplayString(SymbolFormat) ?? string.Empty,
                    ["methodName"] = method.Name,
                    ["methodKind"] = method.MethodKind.ToString()
                }));
        }
    }

    private static string? GetEnclosingSymbol(SemanticModel model, SyntaxNode node)
    {
        return model.GetEnclosingSymbol(node.SpanStart)?.ToDisplayString(SymbolFormat);
    }

    private static void AddCompilationDiagnostics(
        string repoPath,
        string? projectPath,
        Compilation compilation,
        List<SemanticFactCandidate> gaps)
    {
        foreach (var diagnostic in compilation.GetDiagnostics()
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .OrderBy(diagnostic => ToDiagnosticPath(repoPath, diagnostic), StringComparer.Ordinal)
            .ThenBy(diagnostic => diagnostic.Location.GetLineSpan().StartLinePosition.Line)
            .ThenBy(diagnostic => diagnostic.Id, StringComparer.Ordinal))
        {
            var lineSpan = diagnostic.Location.GetLineSpan();
            var filePath = diagnostic.Location.IsInSource
                ? ToRelativePath(repoPath, lineSpan.Path)
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
            properties);
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
        var properties = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["gapKind"] = gapKind,
            ["message"] = message
        };
        if (!string.IsNullOrWhiteSpace(diagnosticId))
        {
            properties["diagnosticId"] = diagnosticId;
        }

        return new SemanticFactCandidate(
            FactTypes.AnalysisGap,
            RuleIds.CSharpSemanticWorkspace,
            EvidenceTiers.Tier4Unknown,
            new EvidenceSpan(
                FileInventory.NormalizeRelativePath(filePath),
                Math.Max(1, startLine),
                Math.Max(Math.Max(1, startLine), endLine),
                null,
                "CSharpSemanticExtractor",
                ScannerVersions.CSharpSemanticExtractor),
            ProjectPath: projectPath,
            Properties: properties);
    }

    private static EvidenceSpan ToEvidenceSpan(string filePath, SyntaxNode node)
    {
        var span = node.SyntaxTree.GetLineSpan(node.Span);
        return new EvidenceSpan(
            FileInventory.NormalizeRelativePath(filePath),
            span.StartLinePosition.Line + 1,
            Math.Max(span.StartLinePosition.Line + 1, span.EndLinePosition.Line + 1),
            null,
            "CSharpSemanticExtractor",
            ScannerVersions.CSharpSemanticExtractor);
    }

    private static string ToDiagnosticPath(string repoPath, Diagnostic diagnostic)
    {
        return diagnostic.Location.IsInSource
            ? ToRelativePath(repoPath, diagnostic.Location.GetLineSpan().Path)
            : ".";
    }

    private static string ToRelativePath(string repoPath, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return ".";
        }

        var fullPath = Path.GetFullPath(path);
        var root = Path.GetFullPath(repoPath);
        var relativePath = Path.GetRelativePath(root, fullPath);
        return relativePath.StartsWith("..", StringComparison.Ordinal)
            ? FileInventory.NormalizeRelativePath(path)
            : FileInventory.NormalizeRelativePath(relativePath);
    }

    private static bool IsGeneratedSource(string? fullPath)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
        {
            return false;
        }

        var fileName = Path.GetFileName(fullPath);
        return fileName.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".g.i.cs", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".generated.cs", StringComparison.OrdinalIgnoreCase)
            || fileName.EndsWith(".designer.cs", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsWorkspaceException(Exception ex)
    {
        return ex is not OperationCanceledException
            and not OutOfMemoryException
            and not StackOverflowException;
    }
}
