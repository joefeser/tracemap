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
            LiteralExpressionSyntax literal when literal.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.NullLiteralExpression) => "NullLiteral",
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
