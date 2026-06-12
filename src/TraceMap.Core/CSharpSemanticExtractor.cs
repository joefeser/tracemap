using Microsoft.Build.Locator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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

    private static readonly HashSet<string> HttpClientMethods = new(StringComparer.Ordinal)
    {
        "GetAsync",
        "PostAsync",
        "PutAsync",
        "DeleteAsync",
        "SendAsync"
    };

    private static readonly HashSet<string> JsonHttpMethods = new(StringComparer.Ordinal)
    {
        "GetFromJsonAsync",
        "ReadFromJsonAsync",
        "PostAsJsonAsync",
        "PutAsJsonAsync"
    };

    private static readonly HashSet<string> DbSaveMethods = new(StringComparer.Ordinal)
    {
        "SaveChanges",
        "SaveChangesAsync"
    };

    private static readonly HashSet<string> DapperMethods = new(StringComparer.Ordinal)
    {
        "Query",
        "QueryAsync",
        "Execute",
        "ExecuteAsync"
    };

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
        AddFieldDeclarationFacts(projectPath, filePath, root, model, facts);
        AddParameterDeclarationFacts(projectPath, filePath, root, model, facts);
        AddLocalAliasFacts(repoPath, projectPath, filePath, root, model, facts);
        AddFieldAliasFacts(repoPath, projectPath, filePath, root, model, facts);
        AddPropertyAccessFacts(projectPath, filePath, root, model, facts);
        AddMethodInvocationFacts(repoPath, projectPath, filePath, root, model, facts);
        AddObjectCreationFacts(repoPath, projectPath, filePath, root, model, facts);
        AddFlowBoundaryFacts(projectPath, filePath, root, model, facts);
        AddRuntimeEvidenceFacts(projectPath, filePath, root, model, facts);
        AddIntegrationFacts(projectPath, filePath, root, model, facts);
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

    private static void AddFieldDeclarationFacts(
        string? projectPath,
        string filePath,
        SyntaxNode root,
        SemanticModel model,
        List<SemanticFactCandidate> facts)
    {
        foreach (var variable in root.DescendantNodes().OfType<VariableDeclaratorSyntax>())
        {
            if (variable.Parent?.Parent is not FieldDeclarationSyntax || model.GetDeclaredSymbol(variable) is not IFieldSymbol field)
            {
                continue;
            }

            facts.Add(CreateSemanticFact(
                FactTypes.FieldDeclared,
                RuleIds.CSharpSemanticDeclarations,
                projectPath,
                filePath,
                variable,
                sourceSymbol: field.ContainingType?.ToDisplayString(SymbolFormat),
                targetSymbol: field.ToDisplayString(SymbolFormat),
                contractElement: field.Name,
                properties: AddAssemblyProperties(
                    new SortedDictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["fieldName"] = field.Name,
                        ["fieldType"] = field.Type.ToDisplayString(SymbolFormat),
                        ["containingType"] = field.ContainingType?.ToDisplayString(SymbolFormat) ?? string.Empty,
                        ["declaredAccessibility"] = field.DeclaredAccessibility.ToString(),
                        ["isStatic"] = field.IsStatic.ToString()
                    },
                    field.ContainingAssembly,
                    field.Type.ContainingAssembly)));
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
            if (model.GetDeclaredSymbol(parameterSyntax) is not IParameterSymbol parameter)
            {
                continue;
            }

            var containingSymbol = parameter.ContainingSymbol;
            facts.Add(CreateSemanticFact(
                FactTypes.ParameterDeclared,
                RuleIds.CSharpSemanticDeclarations,
                projectPath,
                filePath,
                parameterSyntax,
                sourceSymbol: containingSymbol?.ToDisplayString(SymbolFormat),
                targetSymbol: parameter.ToDisplayString(SymbolFormat),
                contractElement: parameter.Name,
                properties: AddAssemblyProperties(
                    new SortedDictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["parameterName"] = parameter.Name,
                        ["parameterType"] = parameter.Type.ToDisplayString(SymbolFormat),
                        ["parameterOrdinal"] = parameter.Ordinal.ToString(),
                        ["containingSymbol"] = containingSymbol?.ToDisplayString(SymbolFormat) ?? string.Empty,
                        ["isOptional"] = parameter.IsOptional.ToString()
                    },
                    containingSymbol?.ContainingAssembly,
                    parameter.Type.ContainingAssembly)));
        }
    }

    private static void AddLocalAliasFacts(
        string repoPath,
        string? projectPath,
        string filePath,
        SyntaxNode root,
        SemanticModel model,
        List<SemanticFactCandidate> facts)
    {
        foreach (var variable in root.DescendantNodes().OfType<VariableDeclaratorSyntax>())
        {
            if (variable.Parent?.Parent is FieldDeclarationSyntax
                || variable.Initializer is null
                || model.GetDeclaredSymbol(variable) is not ILocalSymbol local)
            {
                continue;
            }

            AddLocalAliasFact(
                repoPath,
                projectPath,
                filePath,
                variable,
                variable.Initializer.Value,
                model,
                facts,
                local);
        }

        foreach (var assignment in root.DescendantNodes().OfType<AssignmentExpressionSyntax>())
        {
            if (!assignment.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.SimpleAssignmentExpression)
                || model.GetSymbolInfo(assignment.Left).Symbol is not ILocalSymbol local)
            {
                continue;
            }

            AddLocalAliasFact(
                repoPath,
                projectPath,
                filePath,
                assignment,
                assignment.Right,
                model,
                facts,
                local);
        }
    }

    private static void AddLocalAliasFact(
        string repoPath,
        string? projectPath,
        string filePath,
        SyntaxNode evidenceNode,
        ExpressionSyntax originExpression,
        SemanticModel model,
        List<SemanticFactCandidate> facts,
        ILocalSymbol local)
    {
        var originSymbol = model.GetSymbolInfo(originExpression).Symbol;
        if (originSymbol is null || SymbolEqualityComparer.Default.Equals(originSymbol, local))
        {
            return;
        }

        var containingSymbol = local.ContainingSymbol?.ToDisplayString(SymbolFormat);
        var originType = model.GetTypeInfo(originExpression).Type;
        var aliasSourceLocation = GetSourceLocation(repoPath, local);
        var originSourceLocation = GetSourceLocation(repoPath, originSymbol);
        var properties = AddAssemblyProperties(
            new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["aliasSymbol"] = local.ToDisplayString(SymbolFormat),
                ["aliasSymbolKind"] = local.Kind.ToString(),
                ["aliasType"] = local.Type.ToDisplayString(SymbolFormat),
                ["aliasSourceFile"] = aliasSourceLocation.FilePath ?? string.Empty,
                ["aliasSourceStartLine"] = aliasSourceLocation.StartLine?.ToString() ?? string.Empty,
                ["aliasSourceEndLine"] = aliasSourceLocation.EndLine?.ToString() ?? string.Empty,
                ["containingSymbol"] = containingSymbol ?? string.Empty,
                ["originExpressionKind"] = GetExpressionKind(originExpression),
                ["originExpressionHash"] = FactFactory.Hash(originExpression.ToString(), 32),
                ["originSymbol"] = originSymbol.ToDisplayString(SymbolFormat),
                ["originSymbolKind"] = originSymbol.Kind.ToString(),
                ["originType"] = originType?.ToDisplayString(SymbolFormat) ?? string.Empty,
                ["originSourceFile"] = originSourceLocation.FilePath ?? string.Empty,
                ["originSourceStartLine"] = originSourceLocation.StartLine?.ToString() ?? string.Empty,
                ["originSourceEndLine"] = originSourceLocation.EndLine?.ToString() ?? string.Empty
            },
            local.ContainingAssembly,
            originSymbol.ContainingAssembly ?? originType?.ContainingAssembly);

        facts.Add(CreateSemanticFact(
            FactTypes.LocalAlias,
            RuleIds.CSharpSemanticLocalAlias,
            projectPath,
            filePath,
            evidenceNode,
            sourceSymbol: containingSymbol,
            targetSymbol: local.ToDisplayString(SymbolFormat),
            contractElement: local.Name,
            properties: properties));
    }

    private static void AddFieldAliasFacts(
        string repoPath,
        string? projectPath,
        string filePath,
        SyntaxNode root,
        SemanticModel model,
        List<SemanticFactCandidate> facts)
    {
        foreach (var assignment in root.DescendantNodes().OfType<AssignmentExpressionSyntax>())
        {
            if (!assignment.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.SimpleAssignmentExpression)
                || model.GetSymbolInfo(assignment.Left).Symbol is not IFieldSymbol field)
            {
                continue;
            }

            AddFieldAliasFact(
                repoPath,
                projectPath,
                filePath,
                assignment,
                assignment.Right,
                model,
                facts,
                field);
        }
    }

    private static void AddFieldAliasFact(
        string repoPath,
        string? projectPath,
        string filePath,
        SyntaxNode evidenceNode,
        ExpressionSyntax originExpression,
        SemanticModel model,
        List<SemanticFactCandidate> facts,
        IFieldSymbol field)
    {
        var originSymbol = model.GetSymbolInfo(originExpression).Symbol;
        if (originSymbol is null || SymbolEqualityComparer.Default.Equals(originSymbol, field))
        {
            return;
        }

        var containingSymbol = model.GetEnclosingSymbol(evidenceNode.SpanStart)?.ToDisplayString(SymbolFormat);
        var originType = model.GetTypeInfo(originExpression).Type;
        var fieldSourceLocation = GetSourceLocation(repoPath, field);
        var originSourceLocation = GetSourceLocation(repoPath, originSymbol);
        var properties = AddAssemblyProperties(
            new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["containingSymbol"] = containingSymbol ?? string.Empty,
                ["fieldSymbol"] = field.ToDisplayString(SymbolFormat),
                ["fieldSymbolKind"] = field.Kind.ToString(),
                ["fieldType"] = field.Type.ToDisplayString(SymbolFormat),
                ["fieldSourceFile"] = fieldSourceLocation.FilePath ?? string.Empty,
                ["fieldSourceStartLine"] = fieldSourceLocation.StartLine?.ToString() ?? string.Empty,
                ["fieldSourceEndLine"] = fieldSourceLocation.EndLine?.ToString() ?? string.Empty,
                ["originExpressionKind"] = GetExpressionKind(originExpression),
                ["originExpressionHash"] = FactFactory.Hash(originExpression.ToString(), 32),
                ["originSymbol"] = originSymbol.ToDisplayString(SymbolFormat),
                ["originSymbolKind"] = originSymbol.Kind.ToString(),
                ["originType"] = originType?.ToDisplayString(SymbolFormat) ?? string.Empty,
                ["originSourceFile"] = originSourceLocation.FilePath ?? string.Empty,
                ["originSourceStartLine"] = originSourceLocation.StartLine?.ToString() ?? string.Empty,
                ["originSourceEndLine"] = originSourceLocation.EndLine?.ToString() ?? string.Empty
            },
            field.ContainingAssembly,
            originSymbol.ContainingAssembly ?? originType?.ContainingAssembly);

        facts.Add(CreateSemanticFact(
            FactTypes.FieldAlias,
            RuleIds.CSharpSemanticFieldAlias,
            projectPath,
            filePath,
            evidenceNode,
            sourceSymbol: containingSymbol,
            targetSymbol: field.ToDisplayString(SymbolFormat),
            contractElement: field.Name,
            properties: properties));
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
        string repoPath,
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

            var enclosing = model.GetEnclosingSymbol(invocation.SpanStart);
            var enclosingSymbol = enclosing?.ToDisplayString(SymbolFormat);
            facts.Add(CreateSemanticFact(
                FactTypes.MethodInvoked,
                RuleIds.CSharpSemanticMethodInvocation,
                projectPath,
                filePath,
                invocation,
                sourceSymbol: enclosingSymbol,
                targetSymbol: method.ToDisplayString(SymbolFormat),
                contractElement: method.Name,
                properties: AddAssemblyProperties(
                    new SortedDictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["containingType"] = method.ContainingType?.ToDisplayString(SymbolFormat) ?? string.Empty,
                        ["methodName"] = method.Name,
                        ["methodKind"] = method.MethodKind.ToString()
                    },
                    enclosing?.ContainingAssembly,
                    method.ContainingAssembly)));

            facts.Add(CreateSemanticFact(
                FactTypes.CallEdge,
                RuleIds.CSharpSemanticCallGraph,
                projectPath,
                filePath,
                invocation,
                sourceSymbol: enclosingSymbol,
                targetSymbol: method.ToDisplayString(SymbolFormat),
                contractElement: method.Name,
                properties: AddAssemblyProperties(
                    new SortedDictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["callerSymbol"] = enclosingSymbol ?? string.Empty,
                        ["calleeSymbol"] = method.ToDisplayString(SymbolFormat),
                        ["calleeName"] = method.Name,
                        ["calleeContainingType"] = method.ContainingType?.ToDisplayString(SymbolFormat) ?? string.Empty,
                        ["callKind"] = "SemanticMethodInvocation"
                    },
                    enclosing?.ContainingAssembly,
                    method.ContainingAssembly)));

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

    private static void AddObjectCreationFacts(
        string repoPath,
        string? projectPath,
        string filePath,
        SyntaxNode root,
        SemanticModel model,
        List<SemanticFactCandidate> facts)
    {
        foreach (var creation in root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
        {
            if (model.GetTypeInfo(creation).Type is not INamedTypeSymbol type)
            {
                continue;
            }

            var constructor = model.GetSymbolInfo(creation).Symbol as IMethodSymbol;
            var enclosing = model.GetEnclosingSymbol(creation.SpanStart);
            var enclosingSymbol = enclosing?.ToDisplayString(SymbolFormat);
            var createdType = type.ToDisplayString(SymbolFormat);
            var constructorSymbol = constructor?.ToDisplayString(SymbolFormat) ?? createdType;
            var assignedTo = GetAssignedVariableName(creation);

            facts.Add(CreateSemanticFact(
                FactTypes.ObjectCreated,
                RuleIds.CSharpSemanticObjectCreation,
                projectPath,
                filePath,
                creation,
                sourceSymbol: enclosingSymbol,
                targetSymbol: createdType,
                contractElement: type.Name,
                properties: AddAssemblyProperties(
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
                    enclosing?.ContainingAssembly,
                    type.ContainingAssembly)));

            facts.Add(CreateSemanticFact(
                FactTypes.CallEdge,
                RuleIds.CSharpSemanticCallGraph,
                projectPath,
                filePath,
                creation,
                sourceSymbol: enclosingSymbol,
                targetSymbol: constructorSymbol,
                contractElement: type.Name,
                properties: AddAssemblyProperties(
                    new SortedDictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["callerSymbol"] = enclosingSymbol ?? string.Empty,
                        ["calleeSymbol"] = constructorSymbol,
                        ["calleeName"] = type.Name,
                        ["calleeContainingType"] = createdType,
                        ["callKind"] = "SemanticObjectCreation",
                        ["assignedTo"] = assignedTo ?? string.Empty
                    },
                    enclosing?.ContainingAssembly,
                    type.ContainingAssembly)));

            if (constructor is not null && creation.ArgumentList is not null)
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
            var argument = arguments[index];
            var parameter = ResolveParameter(callee, argument, index);
            if (parameter is null)
            {
                continue;
            }

            var argumentSymbol = model.GetSymbolInfo(argument.Expression).Symbol;
            var argumentType = model.GetTypeInfo(argument.Expression).Type;
            var sourceLocation = GetSourceLocation(repoPath, argumentSymbol);
            var properties = AddAssemblyProperties(
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
                caller?.ContainingAssembly,
                callee.ContainingAssembly);
            AddArgumentAssemblyProperties(properties, argumentSymbol, argumentType);

            facts.Add(CreateSemanticFact(
                FactTypes.ArgumentPassed,
                RuleIds.CSharpSemanticValueFlow,
                projectPath,
                filePath,
                argument,
                sourceSymbol: callerSymbol,
                targetSymbol: calleeSymbol,
                contractElement: parameter.Name,
                properties: properties));
        }
    }

    private static void AddIntegrationFacts(
        string? projectPath,
        string filePath,
        SyntaxNode root,
        SemanticModel model,
        List<SemanticFactCandidate> facts)
    {
        AddDbContextFacts(projectPath, filePath, root, model, facts);
        AddIntegrationInvocationFacts(projectPath, filePath, root, model, facts);
        AddSqlCommandFacts(projectPath, filePath, root, model, facts);
    }

    private static void AddFlowBoundaryFacts(
        string? projectPath,
        string filePath,
        SyntaxNode root,
        SemanticModel model,
        List<SemanticFactCandidate> facts)
    {
        AddBranchConditionFacts(projectPath, filePath, root, model, facts);
        AddMutationBoundaryFacts(projectPath, filePath, root, model, facts);
        AddInvocationBoundaryFacts(projectPath, filePath, root, model, facts);
    }

    private static void AddBranchConditionFacts(
        string? projectPath,
        string filePath,
        SyntaxNode root,
        SemanticModel model,
        List<SemanticFactCandidate> facts)
    {
        foreach (var statement in root.DescendantNodes().OfType<IfStatementSyntax>())
        {
            facts.Add(CreateBranchConditionFact(projectPath, filePath, statement, statement.Condition, model, "If"));
        }

        foreach (var expression in root.DescendantNodes().OfType<ConditionalExpressionSyntax>())
        {
            facts.Add(CreateBranchConditionFact(projectPath, filePath, expression, expression.Condition, model, "ConditionalExpression"));
        }

        foreach (var statement in root.DescendantNodes().OfType<SwitchStatementSyntax>())
        {
            facts.Add(CreateBranchConditionFact(projectPath, filePath, statement, statement.Expression, model, "Switch"));
        }

        foreach (var expression in root.DescendantNodes().OfType<SwitchExpressionSyntax>())
        {
            facts.Add(CreateBranchConditionFact(projectPath, filePath, expression, expression.GoverningExpression, model, "SwitchExpression"));
        }
    }

    private static SemanticFactCandidate CreateBranchConditionFact(
        string? projectPath,
        string filePath,
        SyntaxNode evidenceNode,
        ExpressionSyntax condition,
        SemanticModel model,
        string branchKind)
    {
        var conditionType = model.GetTypeInfo(condition).Type;
        return CreateSemanticFact(
            FactTypes.BranchCondition,
            RuleIds.CSharpSemanticFlowBoundary,
            projectPath,
            filePath,
            evidenceNode,
            sourceSymbol: GetEnclosingSymbol(model, evidenceNode),
            targetSymbol: branchKind,
            contractElement: branchKind,
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["boundaryKind"] = "BranchCondition",
                ["branchKind"] = branchKind,
                ["conditionExpressionKind"] = GetExpressionKind(condition),
                ["conditionExpressionHash"] = FactFactory.Hash(condition.ToString(), 32),
                ["conditionType"] = conditionType?.ToDisplayString(SymbolFormat) ?? string.Empty
            });
    }

    private static void AddMutationBoundaryFacts(
        string? projectPath,
        string filePath,
        SyntaxNode root,
        SemanticModel model,
        List<SemanticFactCandidate> facts)
    {
        foreach (var assignment in root.DescendantNodes().OfType<AssignmentExpressionSyntax>())
        {
            var targetSymbol = model.GetSymbolInfo(assignment.Left).Symbol;
            if (targetSymbol is not (IFieldSymbol or IPropertySymbol))
            {
                continue;
            }

            var assignedType = model.GetTypeInfo(assignment.Right).Type;
            facts.Add(CreateSemanticFact(
                FactTypes.ObjectMutation,
                RuleIds.CSharpSemanticFlowBoundary,
                projectPath,
                filePath,
                assignment,
                sourceSymbol: GetEnclosingSymbol(model, assignment),
                targetSymbol: targetSymbol.ToDisplayString(SymbolFormat),
                contractElement: targetSymbol.Name,
                properties: AddAssemblyProperties(
                    new SortedDictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["boundaryKind"] = "ObjectMutation",
                        ["mutationKind"] = assignment.Kind().ToString(),
                        ["targetSymbol"] = targetSymbol.ToDisplayString(SymbolFormat),
                        ["targetSymbolKind"] = targetSymbol.Kind.ToString(),
                        ["assignedExpressionKind"] = GetExpressionKind(assignment.Right),
                        ["assignedExpressionHash"] = FactFactory.Hash(assignment.Right.ToString(), 32),
                        ["assignedType"] = assignedType?.ToDisplayString(SymbolFormat) ?? string.Empty
                    },
                    model.GetEnclosingSymbol(assignment.SpanStart)?.ContainingAssembly,
                    targetSymbol.ContainingAssembly)));
        }
    }

    private static void AddInvocationBoundaryFacts(
        string? projectPath,
        string filePath,
        SyntaxNode root,
        SemanticModel model,
        List<SemanticFactCandidate> facts)
    {
        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            var symbolInfo = model.GetSymbolInfo(invocation);
            var method = symbolInfo.Symbol as IMethodSymbol;
            var expression = invocation.Expression;
            var receiverExpression = GetInvocationReceiver(expression);
            var receiverType = receiverExpression is null ? null : model.GetTypeInfo(receiverExpression).Type;

            if (method is not null)
            {
                AddResolvedInvocationBoundaryFacts(projectPath, filePath, invocation, model, facts, method, receiverExpression, receiverType);
            }
            else if (receiverType?.TypeKind == TypeKind.Dynamic || model.GetTypeInfo(expression).Type?.TypeKind == TypeKind.Dynamic)
            {
                facts.Add(CreateDynamicInvocationFact(projectPath, filePath, invocation, model, expression.ToString(), receiverType));
            }
        }
    }

    private static void AddResolvedInvocationBoundaryFacts(
        string? projectPath,
        string filePath,
        InvocationExpressionSyntax invocation,
        SemanticModel model,
        List<SemanticFactCandidate> facts,
        IMethodSymbol method,
        ExpressionSyntax? receiverExpression,
        ITypeSymbol? receiverType)
    {
        if (IsDependencyResolutionMethod(method, invocation, out var dependencyType))
        {
            facts.Add(CreateInvocationBoundaryFact(
                projectPath,
                filePath,
                invocation,
                model,
                method,
                FactTypes.DependencyResolved,
                "DependencyResolved",
                dependencyType,
                receiverExpression,
                receiverType));
        }

        if (IsDeserializerMethod(method, invocation, out var deserializedType))
        {
            facts.Add(CreateInvocationBoundaryFact(
                projectPath,
                filePath,
                invocation,
                model,
                method,
                FactTypes.DeserializedObject,
                "DeserializedObject",
                deserializedType,
                receiverExpression,
                receiverType));
        }

        if (IsReflectionMethod(method))
        {
            facts.Add(CreateInvocationBoundaryFact(
                projectPath,
                filePath,
                invocation,
                model,
                method,
                FactTypes.ReflectionUsage,
                "ReflectionUsage",
                method.ReturnType.ToDisplayString(SymbolFormat),
                receiverExpression,
                receiverType));
        }

        if (IsCollectionMutationMethod(method))
        {
            facts.Add(CreateInvocationBoundaryFact(
                projectPath,
                filePath,
                invocation,
                model,
                method,
                FactTypes.CollectionMutation,
                "CollectionMutation",
                receiverType?.ToDisplayString(SymbolFormat) ?? method.ContainingType?.ToDisplayString(SymbolFormat) ?? string.Empty,
                receiverExpression,
                receiverType));
        }
    }

    private static SemanticFactCandidate CreateInvocationBoundaryFact(
        string? projectPath,
        string filePath,
        InvocationExpressionSyntax invocation,
        SemanticModel model,
        IMethodSymbol method,
        string factType,
        string boundaryKind,
        string target,
        ExpressionSyntax? receiverExpression,
        ITypeSymbol? receiverType)
    {
        var receiverSymbol = receiverExpression is null ? null : model.GetSymbolInfo(receiverExpression).Symbol;
        return CreateSemanticFact(
            factType,
            RuleIds.CSharpSemanticFlowBoundary,
            projectPath,
            filePath,
            invocation,
            sourceSymbol: GetEnclosingSymbol(model, invocation),
            targetSymbol: string.IsNullOrWhiteSpace(target) ? method.ToDisplayString(SymbolFormat) : target,
            contractElement: method.Name,
            properties: AddAssemblyProperties(
                new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["boundaryKind"] = boundaryKind,
                    ["methodSymbol"] = method.ToDisplayString(SymbolFormat),
                    ["methodName"] = method.Name,
                    ["containingType"] = method.ContainingType?.ToDisplayString(SymbolFormat) ?? string.Empty,
                    ["receiverSymbol"] = receiverSymbol?.ToDisplayString(SymbolFormat) ?? string.Empty,
                    ["receiverType"] = receiverType?.ToDisplayString(SymbolFormat) ?? string.Empty,
                    ["targetType"] = target,
                    ["argumentCount"] = invocation.ArgumentList.Arguments.Count.ToString()
                },
                model.GetEnclosingSymbol(invocation.SpanStart)?.ContainingAssembly,
                method.ContainingAssembly));
    }

    private static SemanticFactCandidate CreateDynamicInvocationFact(
        string? projectPath,
        string filePath,
        InvocationExpressionSyntax invocation,
        SemanticModel model,
        string expression,
        ITypeSymbol? receiverType)
    {
        return CreateSemanticFact(
            FactTypes.DynamicInvocation,
            RuleIds.CSharpSemanticFlowBoundary,
            projectPath,
            filePath,
            invocation,
            sourceSymbol: GetEnclosingSymbol(model, invocation),
            targetSymbol: "dynamic",
            contractElement: "dynamic",
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["boundaryKind"] = "DynamicInvocation",
                ["expressionHash"] = FactFactory.Hash(expression, 32),
                ["receiverType"] = receiverType?.ToDisplayString(SymbolFormat) ?? string.Empty
            });
    }

    private static void AddRuntimeEvidenceFacts(
        string? projectPath,
        string filePath,
        SyntaxNode root,
        SemanticModel model,
        List<SemanticFactCandidate> facts)
    {
        AddDependencyRegistrationFacts(projectPath, filePath, root, model, facts);
        AddSerializerContractMemberFacts(projectPath, filePath, root, model, facts);
        AddReflectionTargetFacts(projectPath, filePath, root, model, facts);
        AddDynamicDispatchCandidateFacts(projectPath, filePath, root, model, facts);
        AddCollectionElementFlowFacts(projectPath, filePath, root, model, facts);
        AddMutationSemanticsFacts(projectPath, filePath, root, model, facts);
        AddBranchFeasibilityFacts(projectPath, filePath, root, model, facts);
    }

    private static void AddDependencyRegistrationFacts(
        string? projectPath,
        string filePath,
        SyntaxNode root,
        SemanticModel model,
        List<SemanticFactCandidate> facts)
    {
        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (model.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method
                || !TryGetDependencyRegistration(method, invocation, model, out var registrationKind, out var serviceType, out var implementationType))
            {
                continue;
            }

            facts.Add(CreateSemanticFact(
                FactTypes.DependencyRegistered,
                RuleIds.CSharpSemanticRuntimeEvidence,
                projectPath,
                filePath,
                invocation,
                sourceSymbol: GetEnclosingSymbol(model, invocation),
                targetSymbol: serviceType,
                contractElement: method.Name,
                properties: AddAssemblyProperties(
                    new SortedDictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["evidenceKind"] = "DependencyRegistered",
                        ["registrationKind"] = registrationKind,
                        ["serviceType"] = serviceType,
                        ["implementationType"] = implementationType,
                        ["methodSymbol"] = method.ToDisplayString(SymbolFormat),
                        ["argumentCount"] = invocation.ArgumentList.Arguments.Count.ToString()
                    },
                    model.GetEnclosingSymbol(invocation.SpanStart)?.ContainingAssembly,
                    method.ContainingAssembly)));
        }
    }

    private static void AddSerializerContractMemberFacts(
        string? projectPath,
        string filePath,
        SyntaxNode root,
        SemanticModel model,
        List<SemanticFactCandidate> facts)
    {
        foreach (var member in root.DescendantNodes().OfType<MemberDeclarationSyntax>())
        {
            if (member is not PropertyDeclarationSyntax and not FieldDeclarationSyntax)
            {
                continue;
            }

            var symbol = model.GetDeclaredSymbol(member);
            if (symbol is not (IPropertySymbol or IFieldSymbol))
            {
                continue;
            }

            foreach (var attributeList in member.AttributeLists)
            {
                foreach (var attribute in attributeList.Attributes)
                {
                    if (!TryGetSerializerContractAttribute(attribute, model, out var attributeName, out var contractName))
                    {
                        continue;
                    }

                    var memberType = symbol switch
                    {
                        IPropertySymbol property => property.Type,
                        IFieldSymbol field => field.Type,
                        _ => null
                    };
                    var containingType = symbol.ContainingType?.ToDisplayString(SymbolFormat) ?? string.Empty;
                    facts.Add(CreateSemanticFact(
                        FactTypes.SerializerContractMember,
                        RuleIds.CSharpSemanticRuntimeEvidence,
                        projectPath,
                        filePath,
                        attribute,
                        sourceSymbol: containingType,
                        targetSymbol: symbol.ToDisplayString(SymbolFormat),
                        contractElement: contractName,
                        properties: AddAssemblyProperties(
                            new SortedDictionary<string, string>(StringComparer.Ordinal)
                            {
                                ["evidenceKind"] = "SerializerContractMember",
                                ["attributeName"] = attributeName,
                                ["contractName"] = contractName,
                                ["memberName"] = symbol.Name,
                                ["memberSymbol"] = symbol.ToDisplayString(SymbolFormat),
                                ["memberType"] = memberType?.ToDisplayString(SymbolFormat) ?? string.Empty,
                                ["containingType"] = containingType
                            },
                            model.GetEnclosingSymbol(attribute.SpanStart)?.ContainingAssembly,
                            symbol.ContainingAssembly)));
                }
            }
        }
    }

    private static void AddReflectionTargetFacts(
        string? projectPath,
        string filePath,
        SyntaxNode root,
        SemanticModel model,
        List<SemanticFactCandidate> facts)
    {
        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (model.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method
                || !IsReflectionMethod(method)
                || !TryGetReflectionTarget(invocation, method, model, out var reflectionKind, out var declaringType, out var memberName))
            {
                continue;
            }

            var targetSymbol = string.IsNullOrWhiteSpace(memberName) ? declaringType : $"{declaringType}.{memberName}";
            facts.Add(CreateSemanticFact(
                FactTypes.ReflectionTarget,
                RuleIds.CSharpSemanticRuntimeEvidence,
                projectPath,
                filePath,
                invocation,
                sourceSymbol: GetEnclosingSymbol(model, invocation),
                targetSymbol: targetSymbol,
                contractElement: memberName ?? reflectionKind,
                properties: AddAssemblyProperties(
                    new SortedDictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["evidenceKind"] = "ReflectionTarget",
                        ["reflectionKind"] = reflectionKind,
                        ["declaringType"] = declaringType,
                        ["memberName"] = memberName ?? string.Empty,
                        ["methodSymbol"] = method.ToDisplayString(SymbolFormat)
                    },
                    model.GetEnclosingSymbol(invocation.SpanStart)?.ContainingAssembly,
                    method.ContainingAssembly)));
        }
    }

    private static void AddDynamicDispatchCandidateFacts(
        string? projectPath,
        string filePath,
        SyntaxNode root,
        SemanticModel model,
        List<SemanticFactCandidate> facts)
    {
        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            var symbolInfo = model.GetSymbolInfo(invocation);
            if (symbolInfo.Symbol is not null)
            {
                continue;
            }

            var receiverExpression = GetInvocationReceiver(invocation.Expression);
            var receiverType = receiverExpression is null ? null : model.GetTypeInfo(receiverExpression).Type;
            if (receiverType?.TypeKind != TypeKind.Dynamic && model.GetTypeInfo(invocation.Expression).Type?.TypeKind != TypeKind.Dynamic)
            {
                continue;
            }

            var memberName = GetInvocationMemberName(invocation.Expression) ?? "dynamic";
            facts.Add(CreateSemanticFact(
                FactTypes.DynamicDispatchCandidate,
                RuleIds.CSharpSemanticRuntimeEvidence,
                projectPath,
                filePath,
                invocation,
                sourceSymbol: GetEnclosingSymbol(model, invocation),
                targetSymbol: memberName,
                contractElement: memberName,
                properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["evidenceKind"] = "DynamicDispatchCandidate",
                    ["memberName"] = memberName,
                    ["receiverType"] = receiverType?.ToDisplayString(SymbolFormat) ?? string.Empty,
                    ["typeArguments"] = GetInvocationTypeArguments(invocation.Expression),
                    ["argumentCount"] = invocation.ArgumentList.Arguments.Count.ToString(),
                    ["expressionHash"] = FactFactory.Hash(invocation.Expression.ToString(), 32)
                }));
        }
    }

    private static void AddCollectionElementFlowFacts(
        string? projectPath,
        string filePath,
        SyntaxNode root,
        SemanticModel model,
        List<SemanticFactCandidate> facts)
    {
        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (model.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method
                || !IsCollectionMutationMethod(method)
                || !TryGetCollectionElementArgument(method, invocation, out var elementArgument, out var flowKind, out var elementOrdinal))
            {
                continue;
            }

            var receiverExpression = GetInvocationReceiver(invocation.Expression);
            var receiverSymbol = receiverExpression is null ? null : model.GetSymbolInfo(receiverExpression).Symbol;
            var receiverType = receiverExpression is null ? method.ContainingType : model.GetTypeInfo(receiverExpression).Type;
            var elementSymbol = model.GetSymbolInfo(elementArgument.Expression).Symbol;
            var elementType = model.GetTypeInfo(elementArgument.Expression).Type;
            var target = receiverSymbol?.ToDisplayString(SymbolFormat)
                ?? receiverType?.ToDisplayString(SymbolFormat)
                ?? method.ContainingType?.ToDisplayString(SymbolFormat)
                ?? method.Name;

            facts.Add(CreateSemanticFact(
                FactTypes.CollectionElementFlow,
                RuleIds.CSharpSemanticRuntimeEvidence,
                projectPath,
                filePath,
                invocation,
                sourceSymbol: GetEnclosingSymbol(model, invocation),
                targetSymbol: target,
                contractElement: method.Name,
                properties: AddAssemblyProperties(
                    new SortedDictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["evidenceKind"] = "CollectionElementFlow",
                        ["flowKind"] = flowKind,
                        ["mutationMethod"] = method.Name,
                        ["collectionSymbol"] = receiverSymbol?.ToDisplayString(SymbolFormat) ?? string.Empty,
                        ["collectionType"] = receiverType?.ToDisplayString(SymbolFormat) ?? string.Empty,
                        ["elementArgumentOrdinal"] = elementOrdinal.ToString(),
                        ["elementExpressionKind"] = GetExpressionKind(elementArgument.Expression),
                        ["elementExpressionHash"] = FactFactory.Hash(elementArgument.Expression.ToString(), 32),
                        ["elementSymbol"] = elementSymbol?.ToDisplayString(SymbolFormat) ?? string.Empty,
                        ["elementType"] = elementType?.ToDisplayString(SymbolFormat) ?? string.Empty,
                        ["methodSymbol"] = method.ToDisplayString(SymbolFormat)
                    },
                    model.GetEnclosingSymbol(invocation.SpanStart)?.ContainingAssembly,
                    method.ContainingAssembly)));
        }
    }

    private static void AddMutationSemanticsFacts(
        string? projectPath,
        string filePath,
        SyntaxNode root,
        SemanticModel model,
        List<SemanticFactCandidate> facts)
    {
        foreach (var assignment in root.DescendantNodes().OfType<AssignmentExpressionSyntax>())
        {
            var targetSymbol = model.GetSymbolInfo(assignment.Left).Symbol;
            if (targetSymbol is not (IFieldSymbol or IPropertySymbol))
            {
                continue;
            }

            facts.Add(CreateMutationSemanticsFact(
                projectPath,
                filePath,
                assignment,
                assignment.Left,
                assignment.Right,
                model,
                targetSymbol,
                GetAssignmentSemantics(assignment)));
        }

        foreach (var unary in root.DescendantNodes().OfType<PrefixUnaryExpressionSyntax>())
        {
            AddUnaryMutationSemanticsFact(projectPath, filePath, unary, unary.Operand, model, facts);
        }

        foreach (var unary in root.DescendantNodes().OfType<PostfixUnaryExpressionSyntax>())
        {
            AddUnaryMutationSemanticsFact(projectPath, filePath, unary, unary.Operand, model, facts);
        }
    }

    private static void AddBranchFeasibilityFacts(
        string? projectPath,
        string filePath,
        SyntaxNode root,
        SemanticModel model,
        List<SemanticFactCandidate> facts)
    {
        foreach (var statement in root.DescendantNodes().OfType<IfStatementSyntax>())
        {
            AddBranchFeasibilityFact(projectPath, filePath, statement, statement.Condition, model, "If", facts);
        }

        foreach (var expression in root.DescendantNodes().OfType<ConditionalExpressionSyntax>())
        {
            AddBranchFeasibilityFact(projectPath, filePath, expression, expression.Condition, model, "ConditionalExpression", facts);
        }

        foreach (var statement in root.DescendantNodes().OfType<SwitchStatementSyntax>())
        {
            AddBranchFeasibilityFact(projectPath, filePath, statement, statement.Expression, model, "Switch", facts);
        }

        foreach (var expression in root.DescendantNodes().OfType<SwitchExpressionSyntax>())
        {
            AddBranchFeasibilityFact(projectPath, filePath, expression, expression.GoverningExpression, model, "SwitchExpression", facts);
        }
    }

    private static bool TryGetDependencyRegistration(
        IMethodSymbol method,
        InvocationExpressionSyntax invocation,
        SemanticModel model,
        out string registrationKind,
        out string serviceType,
        out string implementationType)
    {
        registrationKind = string.Empty;
        serviceType = string.Empty;
        implementationType = string.Empty;

        var methodName = method.Name;
        var isKnownRegistrationName = methodName is "AddSingleton" or "AddScoped" or "AddTransient"
            or "AddKeyedSingleton" or "AddKeyedScoped" or "AddKeyedTransient"
            or "Register" or "RegisterType" or "RegisterInstance";
        if (!isKnownRegistrationName)
        {
            return false;
        }

        var typeArguments = method.TypeArguments
            .Select(argument => argument.ToDisplayString(SymbolFormat))
            .Where(argument => !string.IsNullOrWhiteSpace(argument))
            .ToArray();
        var typeOfArguments = invocation.ArgumentList.Arguments
            .Select(argument => GetTypeOfExpressionDisplay(argument.Expression, model))
            .Where(type => !string.IsNullOrWhiteSpace(type))
            .ToArray();

        if (typeArguments.Length > 0)
        {
            serviceType = typeArguments[0];
            implementationType = typeArguments.Length > 1 ? typeArguments[1] : serviceType;
        }
        else if (typeOfArguments.Length > 0)
        {
            serviceType = typeOfArguments[0]!;
            implementationType = typeOfArguments.Length > 1 ? typeOfArguments[1]! : serviceType;
        }
        else
        {
            return false;
        }

        registrationKind = methodName;
        return true;
    }

    private static bool TryGetSerializerContractAttribute(
        AttributeSyntax attribute,
        SemanticModel model,
        out string attributeName,
        out string contractName)
    {
        attributeName = ((model.GetSymbolInfo(attribute).Symbol as IMethodSymbol)?.ContainingType?.ToDisplayString(SymbolFormat)
            ?? attribute.Name.ToString()).Trim();
        contractName = string.Empty;

        var shortName = attributeName.Split('.').Last();
        if (shortName.EndsWith("Attribute", StringComparison.Ordinal))
        {
            shortName = shortName[..^"Attribute".Length];
        }

        if (shortName is not ("JsonPropertyName" or "JsonProperty" or "DataMember"))
        {
            return false;
        }

        contractName = GetAttributeStringArgument(attribute, "Name")
            ?? GetAttributeStringArgument(attribute, "PropertyName")
            ?? GetAttributeStringArgument(attribute, null)
            ?? string.Empty;
        if (string.IsNullOrWhiteSpace(contractName))
        {
            return false;
        }

        return true;
    }

    private static bool TryGetReflectionTarget(
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        SemanticModel model,
        out string reflectionKind,
        out string declaringType,
        out string? memberName)
    {
        reflectionKind = method.Name;
        declaringType = string.Empty;
        memberName = null;

        if (method.Name is "CreateInstance" or "CreateInstanceFrom")
        {
            declaringType = method.TypeArguments.Length > 0
                ? method.TypeArguments[0].ToDisplayString(SymbolFormat)
                : invocation.ArgumentList.Arguments
                    .Select(argument => GetTypeOfExpressionDisplay(argument.Expression, model))
                    .FirstOrDefault(type => !string.IsNullOrWhiteSpace(type)) ?? string.Empty;
            return !string.IsNullOrWhiteSpace(declaringType);
        }

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return false;
        }

        declaringType = GetTypeOfExpressionDisplay(memberAccess.Expression, model) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(declaringType))
        {
            return false;
        }

        if (invocation.ArgumentList.Arguments.Count > 0)
        {
            memberName = GetNameExpressionValue(invocation.ArgumentList.Arguments[0].Expression);
        }

        return true;
    }

    private static bool TryGetCollectionElementArgument(
        IMethodSymbol method,
        InvocationExpressionSyntax invocation,
        out ArgumentSyntax elementArgument,
        out string flowKind,
        out int elementOrdinal)
    {
        elementArgument = default!;
        flowKind = string.Empty;
        elementOrdinal = 0;

        if (method.Name is not ("Add" or "AddRange" or "Insert" or "InsertRange" or "Enqueue" or "Push" or "TryAdd"))
        {
            return false;
        }

        if (invocation.ArgumentList.Arguments.Count == 0)
        {
            return false;
        }

        elementOrdinal = method.Name is "Insert" or "InsertRange"
            ? Math.Min(1, invocation.ArgumentList.Arguments.Count - 1)
            : method.Name == "TryAdd" && invocation.ArgumentList.Arguments.Count > 1
                ? 1
                : 0;
        elementArgument = invocation.ArgumentList.Arguments[elementOrdinal];
        flowKind = method.Name is "AddRange" or "InsertRange" ? "CollectionRangeInput" : "CollectionElementInput";
        return true;
    }

    private static void AddUnaryMutationSemanticsFact(
        string? projectPath,
        string filePath,
        SyntaxNode evidenceNode,
        ExpressionSyntax operand,
        SemanticModel model,
        List<SemanticFactCandidate> facts)
    {
        var targetSymbol = model.GetSymbolInfo(operand).Symbol;
        if (targetSymbol is not (IFieldSymbol or IPropertySymbol))
        {
            return;
        }

        facts.Add(CreateMutationSemanticsFact(
            projectPath,
            filePath,
            evidenceNode,
            operand,
            null,
            model,
            targetSymbol,
            evidenceNode.Kind() is SyntaxKind.PreIncrementExpression or SyntaxKind.PostIncrementExpression ? "Increment" : "Decrement"));
    }

    private static SemanticFactCandidate CreateMutationSemanticsFact(
        string? projectPath,
        string filePath,
        SyntaxNode evidenceNode,
        ExpressionSyntax targetExpression,
        ExpressionSyntax? valueExpression,
        SemanticModel model,
        ISymbol targetSymbol,
        string mutationSemantics)
    {
        var valueType = valueExpression is null ? null : model.GetTypeInfo(valueExpression).Type;
        var targetType = targetSymbol switch
        {
            IPropertySymbol property => property.Type,
            IFieldSymbol field => field.Type,
            _ => null
        };
        return CreateSemanticFact(
            FactTypes.MutationSemantics,
            RuleIds.CSharpSemanticRuntimeEvidence,
            projectPath,
            filePath,
            evidenceNode,
            sourceSymbol: GetEnclosingSymbol(model, evidenceNode),
            targetSymbol: targetSymbol.ToDisplayString(SymbolFormat),
            contractElement: targetSymbol.Name,
            properties: AddAssemblyProperties(
                new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["evidenceKind"] = "MutationSemantics",
                    ["mutationSemantics"] = mutationSemantics,
                    ["targetSymbol"] = targetSymbol.ToDisplayString(SymbolFormat),
                    ["targetSymbolKind"] = targetSymbol.Kind.ToString(),
                    ["targetType"] = targetType?.ToDisplayString(SymbolFormat) ?? string.Empty,
                    ["targetExpressionKind"] = GetExpressionKind(targetExpression),
                    ["valueExpressionKind"] = valueExpression is null ? string.Empty : GetExpressionKind(valueExpression),
                    ["valueExpressionHash"] = valueExpression is null ? string.Empty : FactFactory.Hash(valueExpression.ToString(), 32),
                    ["valueType"] = valueType?.ToDisplayString(SymbolFormat) ?? string.Empty
                },
                model.GetEnclosingSymbol(evidenceNode.SpanStart)?.ContainingAssembly,
                targetSymbol.ContainingAssembly));
    }

    private static string GetAssignmentSemantics(AssignmentExpressionSyntax assignment)
    {
        return assignment.Kind() switch
        {
            SyntaxKind.SimpleAssignmentExpression => "Overwrite",
            SyntaxKind.AddAssignmentExpression => "AdditiveUpdate",
            SyntaxKind.SubtractAssignmentExpression => "SubtractiveUpdate",
            SyntaxKind.MultiplyAssignmentExpression => "MultiplicativeUpdate",
            SyntaxKind.DivideAssignmentExpression => "DivisiveUpdate",
            SyntaxKind.ModuloAssignmentExpression => "ModuloUpdate",
            SyntaxKind.AndAssignmentExpression or SyntaxKind.OrAssignmentExpression or SyntaxKind.ExclusiveOrAssignmentExpression => "BitwiseUpdate",
            SyntaxKind.LeftShiftAssignmentExpression or SyntaxKind.RightShiftAssignmentExpression => "ShiftUpdate",
            SyntaxKind.CoalesceAssignmentExpression => "NullCoalescingUpdate",
            _ => "CompoundUpdate"
        };
    }

    private static void AddBranchFeasibilityFact(
        string? projectPath,
        string filePath,
        SyntaxNode evidenceNode,
        ExpressionSyntax condition,
        SemanticModel model,
        string branchKind,
        List<SemanticFactCandidate> facts)
    {
        if (!TryClassifyBranchFeasibility(condition, model, out var feasibilityKind, out var checkedSymbol, out var comparisonOperator, out var constantValue))
        {
            return;
        }

        facts.Add(CreateSemanticFact(
            FactTypes.BranchFeasibility,
            RuleIds.CSharpSemanticRuntimeEvidence,
            projectPath,
            filePath,
            evidenceNode,
            sourceSymbol: GetEnclosingSymbol(model, evidenceNode),
            targetSymbol: checkedSymbol ?? branchKind,
            contractElement: branchKind,
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["evidenceKind"] = "BranchFeasibility",
                ["branchKind"] = branchKind,
                ["feasibilityKind"] = feasibilityKind,
                ["checkedSymbol"] = checkedSymbol ?? string.Empty,
                ["comparisonOperator"] = comparisonOperator ?? string.Empty,
                ["constantValue"] = constantValue ?? string.Empty,
                ["conditionExpressionKind"] = GetExpressionKind(condition),
                ["conditionExpressionHash"] = FactFactory.Hash(condition.ToString(), 32)
            }));
    }

    private static bool TryClassifyBranchFeasibility(
        ExpressionSyntax condition,
        SemanticModel model,
        out string feasibilityKind,
        out string? checkedSymbol,
        out string? comparisonOperator,
        out string? constantValue)
    {
        feasibilityKind = string.Empty;
        checkedSymbol = null;
        comparisonOperator = null;
        constantValue = null;

        var constant = model.GetConstantValue(condition);
        if (constant.HasValue && constant.Value is bool boolValue)
        {
            feasibilityKind = boolValue ? "ConstantTrue" : "ConstantFalse";
            constantValue = boolValue.ToString();
            return true;
        }

        if (condition is BinaryExpressionSyntax binary
            && binary.Kind() is SyntaxKind.EqualsExpression or SyntaxKind.NotEqualsExpression
            && TryGetNullCheckSymbol(binary.Left, binary.Right, model, out checkedSymbol))
        {
            feasibilityKind = binary.IsKind(SyntaxKind.EqualsExpression) ? "NullCheckEquals" : "NullCheckNotEquals";
            comparisonOperator = binary.OperatorToken.ValueText;
            return true;
        }

        if (condition is IsPatternExpressionSyntax patternExpression
            && TryGetNullPatternKind(patternExpression.Pattern, out feasibilityKind))
        {
            checkedSymbol = model.GetSymbolInfo(patternExpression.Expression).Symbol?.ToDisplayString(SymbolFormat)
                ?? patternExpression.Expression.ToString();
            comparisonOperator = "is";
            return true;
        }

        return false;
    }

    private static bool TryGetNullCheckSymbol(
        ExpressionSyntax left,
        ExpressionSyntax right,
        SemanticModel model,
        out string? checkedSymbol)
    {
        checkedSymbol = null;
        var checkedExpression = IsNullLiteral(left) ? right : IsNullLiteral(right) ? left : null;
        if (checkedExpression is null)
        {
            return false;
        }

        checkedSymbol = model.GetSymbolInfo(checkedExpression).Symbol?.ToDisplayString(SymbolFormat)
            ?? checkedExpression.ToString();
        return true;
    }

    private static bool TryGetNullPatternKind(PatternSyntax pattern, out string feasibilityKind)
    {
        feasibilityKind = string.Empty;
        if (pattern is ConstantPatternSyntax { Expression: LiteralExpressionSyntax literal }
            && literal.IsKind(SyntaxKind.NullLiteralExpression))
        {
            feasibilityKind = "NullPattern";
            return true;
        }

        if (pattern is UnaryPatternSyntax { Pattern: ConstantPatternSyntax { Expression: LiteralExpressionSyntax unaryLiteral } }
            && unaryLiteral.IsKind(SyntaxKind.NullLiteralExpression))
        {
            feasibilityKind = "NotNullPattern";
            return true;
        }

        return false;
    }

    private static void AddDbContextFacts(
        string? projectPath,
        string filePath,
        SyntaxNode root,
        SemanticModel model,
        List<SemanticFactCandidate> facts)
    {
        foreach (var declaration in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
        {
            if (model.GetDeclaredSymbol(declaration) is not INamedTypeSymbol symbol || !DerivesFromDbContext(symbol))
            {
                continue;
            }

            facts.Add(CreateSemanticFact(
                FactTypes.DbContextDeclared,
                RuleIds.DatabaseEntityFramework,
                projectPath,
                filePath,
                declaration,
                targetSymbol: symbol.ToDisplayString(SymbolFormat),
                contractElement: symbol.Name,
                properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["className"] = symbol.Name,
                    ["namespace"] = symbol.ContainingNamespace?.IsGlobalNamespace == false ? symbol.ContainingNamespace.ToDisplayString() : string.Empty
                }));
        }

        foreach (var propertyDeclaration in root.DescendantNodes().OfType<PropertyDeclarationSyntax>())
        {
            if (model.GetDeclaredSymbol(propertyDeclaration) is not IPropertySymbol property || !IsDbSetType(property.Type))
            {
                continue;
            }

            facts.Add(CreateSemanticFact(
                FactTypes.DbSetDeclared,
                RuleIds.DatabaseEntityFramework,
                projectPath,
                filePath,
                propertyDeclaration,
                sourceSymbol: property.ContainingType?.ToDisplayString(SymbolFormat),
                targetSymbol: property.ToDisplayString(SymbolFormat),
                contractElement: property.Name,
                properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["propertyName"] = property.Name,
                    ["propertyType"] = property.Type.ToDisplayString(SymbolFormat)
                }));
        }
    }

    private static void AddIntegrationInvocationFacts(
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

            var methodName = method.Name;
            var containingType = method.ContainingType?.ToDisplayString(SymbolFormat) ?? string.Empty;
            if (IsHttpClientCall(method))
            {
                facts.Add(CreateSemanticFact(
                    FactTypes.HttpCallDetected,
                    RuleIds.HttpClientInvocation,
                    projectPath,
                    filePath,
                    invocation,
                    sourceSymbol: GetEnclosingSymbol(model, invocation),
                    targetSymbol: method.ToDisplayString(SymbolFormat),
                    contractElement: methodName,
                    properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["containingType"] = containingType,
                        ["methodFamily"] = "HttpClient",
                        ["methodName"] = methodName
                    }));
            }
            else if (IsJsonHttpCall(method))
            {
                facts.Add(CreateSemanticFact(
                    FactTypes.HttpCallDetected,
                    RuleIds.HttpClientInvocation,
                    projectPath,
                    filePath,
                    invocation,
                    sourceSymbol: GetEnclosingSymbol(model, invocation),
                    targetSymbol: method.ToDisplayString(SymbolFormat),
                    contractElement: methodName,
                    properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["containingType"] = containingType,
                        ["methodFamily"] = "JsonHttpExtension",
                        ["methodName"] = methodName
                    }));
            }

            if (IsHttpClientFactoryCreateClient(method) && TryGetLiteralArgument(invocation, 0, out var clientName))
            {
                facts.Add(CreateSemanticFact(
                    FactTypes.HttpClientCreated,
                    RuleIds.HttpClientInvocation,
                    projectPath,
                    filePath,
                    invocation,
                    sourceSymbol: GetEnclosingSymbol(model, invocation),
                    targetSymbol: clientName,
                    contractElement: clientName,
                    properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["clientName"] = clientName,
                        ["containingType"] = containingType,
                        ["methodName"] = methodName
                    }));
            }

            if (IsDbSaveCall(method))
            {
                facts.Add(CreateSemanticFact(
                    FactTypes.DbChangeSaved,
                    RuleIds.DatabaseEntityFramework,
                    projectPath,
                    filePath,
                    invocation,
                    sourceSymbol: GetEnclosingSymbol(model, invocation),
                    targetSymbol: method.ToDisplayString(SymbolFormat),
                    contractElement: methodName,
                    properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["containingType"] = containingType,
                        ["methodName"] = methodName
                    }));
            }

            if (IsDapperCall(method))
            {
                facts.Add(CreateSemanticFact(
                    FactTypes.DapperCallDetected,
                    RuleIds.DatabaseDapperInvocation,
                    projectPath,
                    filePath,
                    invocation,
                    sourceSymbol: GetEnclosingSymbol(model, invocation),
                    targetSymbol: method.ToDisplayString(SymbolFormat),
                    contractElement: methodName,
                    properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["containingType"] = containingType,
                        ["methodName"] = methodName
                    }));
            }
        }
    }

    private static void AddSqlCommandFacts(
        string? projectPath,
        string filePath,
        SyntaxNode root,
        SemanticModel model,
        List<SemanticFactCandidate> facts)
    {
        foreach (var creation in root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
        {
            if (model.GetTypeInfo(creation).Type is not INamedTypeSymbol type || !IsSqlCommandType(type))
            {
                continue;
            }

            facts.Add(CreateSemanticFact(
                FactTypes.SqlCommandDetected,
                RuleIds.DatabaseSqlText,
                projectPath,
                filePath,
                creation,
                sourceSymbol: GetEnclosingSymbol(model, creation),
                targetSymbol: type.ToDisplayString(SymbolFormat),
                contractElement: type.Name,
                properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["typeName"] = type.ToDisplayString(SymbolFormat)
                }));
        }
    }

    private static string? GetEnclosingSymbol(SemanticModel model, SyntaxNode node)
    {
        return model.GetEnclosingSymbol(node.SpanStart)?.ToDisplayString(SymbolFormat);
    }

    private static SortedDictionary<string, string> AddAssemblyProperties(
        SortedDictionary<string, string> properties,
        IAssemblySymbol? callerAssembly,
        IAssemblySymbol? calleeAssembly)
    {
        AddAssemblyProperties(properties, "caller", callerAssembly);
        AddAssemblyProperties(properties, "callee", calleeAssembly);
        return properties;
    }

    private static void AddAssemblyProperties(SortedDictionary<string, string> properties, string prefix, IAssemblySymbol? assembly)
    {
        properties[$"{prefix}AssemblyName"] = assembly?.Identity.Name ?? string.Empty;
        properties[$"{prefix}AssemblyVersion"] = assembly?.Identity.Version?.ToString() ?? string.Empty;
    }

    private static void AddArgumentAssemblyProperties(
        SortedDictionary<string, string> properties,
        ISymbol? argumentSymbol,
        ITypeSymbol? argumentType)
    {
        properties["argumentAssemblyName"] = argumentSymbol?.ContainingAssembly?.Identity.Name
            ?? argumentType?.ContainingAssembly?.Identity.Name
            ?? string.Empty;
        properties["argumentAssemblyVersion"] = argumentSymbol?.ContainingAssembly?.Identity.Version?.ToString()
            ?? argumentType?.ContainingAssembly?.Identity.Version?.ToString()
            ?? string.Empty;
    }

    private static IParameterSymbol? ResolveParameter(IMethodSymbol method, ArgumentSyntax argument, int ordinal)
    {
        if (argument.NameColon is not null)
        {
            var name = argument.NameColon.Name.Identifier.ValueText;
            return method.Parameters.FirstOrDefault(parameter => parameter.Name.Equals(name, StringComparison.Ordinal));
        }

        if (ordinal < method.Parameters.Length)
        {
            return method.Parameters[ordinal];
        }

        return method.Parameters.LastOrDefault(parameter => parameter.IsParams);
    }

    private static string GetExpressionKind(ExpressionSyntax expression)
    {
        return expression switch
        {
            LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.NullLiteralExpression) => "NullLiteral",
            LiteralExpressionSyntax => "Literal",
            IdentifierNameSyntax => "Identifier",
            MemberAccessExpressionSyntax => "MemberAccess",
            InvocationExpressionSyntax => "Invocation",
            ObjectCreationExpressionSyntax => "ObjectCreation",
            LambdaExpressionSyntax => "Lambda",
            AnonymousFunctionExpressionSyntax => "AnonymousFunction",
            _ => expression.Kind().ToString()
        };
    }

    private static string? GetTypeOfExpressionDisplay(ExpressionSyntax expression, SemanticModel model)
    {
        return expression is TypeOfExpressionSyntax typeOfExpression
            ? model.GetTypeInfo(typeOfExpression.Type).Type?.ToDisplayString(SymbolFormat) ?? typeOfExpression.Type.ToString()
            : null;
    }

    private static string? GetAttributeStringArgument(AttributeSyntax attribute, string? name)
    {
        var arguments = attribute.ArgumentList?.Arguments;
        if (arguments is null)
        {
            return null;
        }

        foreach (var argument in arguments)
        {
            if (name is not null
                && (argument.NameEquals?.Name.Identifier.ValueText.Equals(name, StringComparison.Ordinal) != true))
            {
                continue;
            }

            if (name is null && argument.NameEquals is not null)
            {
                continue;
            }

            if (argument.Expression is LiteralExpressionSyntax literal
                && literal.IsKind(SyntaxKind.StringLiteralExpression)
                && literal.Token.Value is string literalValue)
            {
                return literalValue;
            }
        }

        return null;
    }

    private static string? GetNameExpressionValue(ExpressionSyntax expression)
    {
        if (expression is LiteralExpressionSyntax literal
            && literal.IsKind(SyntaxKind.StringLiteralExpression)
            && literal.Token.Value is string literalValue)
        {
            return literalValue;
        }

        if (expression is InvocationExpressionSyntax invocation
            && invocation.Expression is IdentifierNameSyntax identifier
            && identifier.Identifier.ValueText == "nameof"
            && invocation.ArgumentList.Arguments.Count > 0)
        {
            return invocation.ArgumentList.Arguments[0].Expression switch
            {
                IdentifierNameSyntax name => name.Identifier.ValueText,
                MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
                _ => invocation.ArgumentList.Arguments[0].Expression.ToString()
            };
        }

        return null;
    }

    private static string? GetInvocationMemberName(ExpressionSyntax expression)
    {
        return expression switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            GenericNameSyntax generic => generic.Identifier.ValueText,
            MemberAccessExpressionSyntax { Name: IdentifierNameSyntax identifier } => identifier.Identifier.ValueText,
            MemberAccessExpressionSyntax { Name: GenericNameSyntax generic } => generic.Identifier.ValueText,
            _ => null
        };
    }

    private static string GetInvocationTypeArguments(ExpressionSyntax expression)
    {
        var typeArguments = expression switch
        {
            GenericNameSyntax generic => generic.TypeArgumentList.Arguments.Select(argument => argument.ToString()),
            MemberAccessExpressionSyntax { Name: GenericNameSyntax generic } => generic.TypeArgumentList.Arguments.Select(argument => argument.ToString()),
            _ => []
        };
        return string.Join(",", typeArguments);
    }

    private static bool IsNullLiteral(ExpressionSyntax expression)
    {
        return expression is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.NullLiteralExpression);
    }

    private static ExpressionSyntax? GetInvocationReceiver(ExpressionSyntax expression)
    {
        return expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Expression,
            MemberBindingExpressionSyntax => null,
            _ => null
        };
    }

    private static bool IsDependencyResolutionMethod(
        IMethodSymbol method,
        InvocationExpressionSyntax invocation,
        out string dependencyType)
    {
        dependencyType = GetGenericTypeArgument(method)
            ?? GetTypeOfArgument(invocation)
            ?? method.ReturnType.ToDisplayString(SymbolFormat);

        var containingType = method.ContainingType?.ToDisplayString(SymbolFormat) ?? string.Empty;
        return method.Name is "GetService" or "GetRequiredService" or "GetServices" or "GetRequiredKeyedService" or "GetKeyedService"
            || (method.Name is "Resolve" or "ResolveOptional" or "ResolveNamed" or "ResolveKeyed"
                && (containingType.Contains("Container", StringComparison.Ordinal)
                    || containingType.Contains("Autofac", StringComparison.Ordinal)
                    || containingType.Contains("Windsor", StringComparison.Ordinal)
                    || containingType.Contains("Ninject", StringComparison.Ordinal)
                    || containingType.Contains("Unity", StringComparison.Ordinal)
                    || containingType.Contains("StructureMap", StringComparison.Ordinal)
                    || containingType.Contains("IServiceProvider", StringComparison.Ordinal)));
    }

    private static bool IsDeserializerMethod(
        IMethodSymbol method,
        InvocationExpressionSyntax invocation,
        out string deserializedType)
    {
        deserializedType = GetGenericTypeArgument(method)
            ?? method.ReturnType.ToDisplayString(SymbolFormat);
        var containingType = method.ContainingType?.ToDisplayString(SymbolFormat) ?? string.Empty;
        var methodName = method.Name;
        return methodName is "Deserialize" or "DeserializeAsync" or "DeserializeObject" or "ReadFromJsonAsync" or "GetFromJsonAsync"
            || (methodName.Contains("Deserialize", StringComparison.Ordinal)
                && (containingType.Contains("Json", StringComparison.Ordinal)
                    || containingType.Contains("Xml", StringComparison.Ordinal)
                    || containingType.Contains("Serializer", StringComparison.Ordinal)));
    }

    private static bool IsReflectionMethod(IMethodSymbol method)
    {
        var containingType = method.ContainingType?.ToDisplayString(SymbolFormat) ?? string.Empty;
        return containingType.Contains("System.Reflection.", StringComparison.Ordinal)
            || containingType.EndsWith("System.Type", StringComparison.Ordinal)
            || containingType.EndsWith("System.Activator", StringComparison.Ordinal)
            || containingType.EndsWith("System.Delegate", StringComparison.Ordinal);
    }

    private static bool IsCollectionMutationMethod(IMethodSymbol method)
    {
        if (method.Name is not ("Add" or "AddRange" or "Insert" or "InsertRange" or "Remove" or "RemoveAt" or "RemoveRange"
            or "Clear" or "Enqueue" or "Dequeue" or "Push" or "Pop" or "TryAdd" or "TryRemove" or "TryTake"))
        {
            return false;
        }

        var type = method.ContainingType;
        if (type is null || type.SpecialType == SpecialType.System_String)
        {
            return false;
        }

        return type.AllInterfaces.Any(IsCollectionLikeType)
            || IsCollectionLikeType(type)
            || type.Name.Contains("Collection", StringComparison.Ordinal)
            || type.Name.Contains("Dictionary", StringComparison.Ordinal)
            || type.Name.Contains("Queue", StringComparison.Ordinal)
            || type.Name.Contains("Stack", StringComparison.Ordinal)
            || type.Name.Contains("Set", StringComparison.Ordinal)
            || type.Name.Contains("List", StringComparison.Ordinal);
    }

    private static bool IsCollectionLikeType(INamedTypeSymbol type)
    {
        var metadataName = type.ConstructedFrom.ToDisplayString(SymbolFormat);
        return metadataName.StartsWith("global::System.Collections.", StringComparison.Ordinal)
            || metadataName.StartsWith("System.Collections.", StringComparison.Ordinal)
            || metadataName.Contains("ICollection", StringComparison.Ordinal)
            || metadataName.Contains("IList", StringComparison.Ordinal)
            || metadataName.Contains("IDictionary", StringComparison.Ordinal)
            || metadataName.Contains("ISet", StringComparison.Ordinal)
            || metadataName.Contains("IProducerConsumerCollection", StringComparison.Ordinal);
    }

    private static string? GetGenericTypeArgument(IMethodSymbol method)
    {
        return method.TypeArguments.Length == 0
            ? null
            : method.TypeArguments[0].ToDisplayString(SymbolFormat);
    }

    private static string? GetTypeOfArgument(InvocationExpressionSyntax invocation)
    {
        foreach (var argument in invocation.ArgumentList.Arguments)
        {
            if (argument.Expression is TypeOfExpressionSyntax typeOfExpression)
            {
                return typeOfExpression.Type.ToString();
            }
        }

        return null;
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
            ToRelativePath(repoPath, span.Path),
            span.StartLinePosition.Line + 1,
            Math.Max(span.StartLinePosition.Line + 1, span.EndLinePosition.Line + 1));
    }

    private static string? GetAssignedVariableName(ObjectCreationExpressionSyntax creation)
    {
        if (creation.Parent is EqualsValueClauseSyntax { Parent: VariableDeclaratorSyntax variable })
        {
            return variable.Identifier.ValueText;
        }

        if (creation.Parent is AssignmentExpressionSyntax assignment)
        {
            return assignment.Left.ToString();
        }

        return null;
    }

    private static bool IsHttpClientCall(IMethodSymbol method)
    {
        return HttpClientMethods.Contains(method.Name)
            && method.ContainingType is not null
            && IsKnownType(method.ContainingType, "System.Net.Http.HttpClient");
    }

    private static bool IsJsonHttpCall(IMethodSymbol method)
    {
        return JsonHttpMethods.Contains(method.Name)
            && method.ContainingType is not null
            && GetNamespaceName(method.ContainingType).Equals("System.Net.Http.Json", StringComparison.Ordinal);
    }

    private static bool IsHttpClientFactoryCreateClient(IMethodSymbol method)
    {
        return method.Name == "CreateClient"
            && method.ContainingType is not null
            && method.ContainingType.Name == "IHttpClientFactory"
            && (GetNamespaceName(method.ContainingType).Equals("System.Net.Http", StringComparison.Ordinal)
                || GetNamespaceName(method.ContainingType).Equals("Microsoft.Extensions.Http", StringComparison.Ordinal));
    }

    private static bool IsDbSaveCall(IMethodSymbol method)
    {
        return DbSaveMethods.Contains(method.Name)
            && method.ContainingType is not null
            && DerivesFromDbContext(method.ContainingType);
    }

    private static bool IsDapperCall(IMethodSymbol method)
    {
        return DapperMethods.Contains(method.Name)
            && method.ContainingType is not null
            && (IsKnownType(method.ContainingType, "Dapper.SqlMapper")
                || GetNamespaceName(method.ContainingType).Equals("Dapper", StringComparison.Ordinal));
    }

    private static bool DerivesFromDbContext(INamedTypeSymbol symbol)
    {
        for (INamedTypeSymbol? current = symbol; current is not null; current = current.BaseType)
        {
            if (IsKnownType(current, "Microsoft.EntityFrameworkCore.DbContext")
                || IsKnownType(current, "System.Data.Entity.DbContext"))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsDbSetType(ITypeSymbol type)
    {
        return type is INamedTypeSymbol namedType
            && (IsKnownType(namedType.OriginalDefinition, "Microsoft.EntityFrameworkCore.DbSet<T>")
                || IsKnownType(namedType.OriginalDefinition, "System.Data.Entity.DbSet<T>")
                || IsKnownType(namedType.OriginalDefinition, "System.Data.Entity.IDbSet<T>"));
    }

    private static bool IsSqlCommandType(INamedTypeSymbol type)
    {
        return IsKnownType(type, "System.Data.SqlClient.SqlCommand")
            || IsKnownType(type, "Microsoft.Data.SqlClient.SqlCommand");
    }

    private static bool IsKnownType(INamedTypeSymbol symbol, string metadataName)
    {
        var displayName = symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
        if (displayName.StartsWith("global::", StringComparison.Ordinal))
        {
            displayName = displayName["global::".Length..];
        }

        return displayName.Equals(metadataName, StringComparison.Ordinal);
    }

    private static string GetNamespaceName(INamedTypeSymbol symbol)
    {
        return symbol.ContainingNamespace?.IsGlobalNamespace == false
            ? symbol.ContainingNamespace.ToDisplayString()
            : string.Empty;
    }

    private static bool TryGetLiteralArgument(InvocationExpressionSyntax invocation, int index, out string value)
    {
        value = string.Empty;
        var arguments = invocation.ArgumentList.Arguments;
        if (arguments.Count <= index)
        {
            return false;
        }

        if (arguments[index].Expression is LiteralExpressionSyntax literal
            && literal.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.StringLiteralExpression)
            && literal.Token.Value is string literalValue)
        {
            value = literalValue;
            return true;
        }

        return false;
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
