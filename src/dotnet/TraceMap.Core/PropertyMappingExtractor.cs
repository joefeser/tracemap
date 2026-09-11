using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TraceMap.Core;

/// <summary>
/// Emits compiler-resolved direct property-to-property mapping evidence for
/// the bounded v0 shapes: simple assignment and object-initializer member
/// assignment (including LINQ-style projections through the same
/// object-initializer rule). Both endpoints must resolve through Roslyn to
/// non-indexer properties with canonical symbol identities. All other shapes
/// fail closed as categorical gaps or stay silent; no expression text is
/// retained anywhere.
/// </summary>
internal static class PropertyMappingExtractor
{
    private const string CoverageLabel = "bounded-static-property-mapping";
    private const string ReducedCoverageLabel = "reduced-static-property-mapping";
    private const string Limitations =
        "Static compiler evidence only; does not prove mapping execution, object creation, runtime values, persistence, serializer behavior, mapper execution, business meaning, correctness, completeness, or impact.";
    private const int MaxFactsPerMethod = 25;
    private const int MaxFactsPerDocument = 250;
    private const int MaxGapsPerDocument = 100;

    public static void Extract(
        string? projectPath,
        string filePath,
        SyntaxNode root,
        SemanticModel model,
        List<SemanticFactCandidate> facts,
        List<SemanticFactCandidate> gaps)
    {
        var documentFacts = 0;
        var suppressedFacts = 0;
        var gapCount = 0;
        var suppressedGaps = 0;
        var methodFacts = new Dictionary<SyntaxNode, int>();

        foreach (var assignment in CollectAssignments(root))
        {
            if (assignment.Left is ImplicitElementAccessSyntax)
            {
                continue;
            }

            var containerMethod = FindContainerMethod(assignment, model);
            if (containerMethod?.Symbol is null)
            {
                continue;
            }

            var left = ResolveSide(assignment.Left, model);
            var right = ResolveSide(assignment.Right, model);
            var propertySideCount = CountPropertySides(left, right);

            if (propertySideCount == 0)
            {
                continue;
            }

            if (!assignment.IsKind(SyntaxKind.SimpleAssignmentExpression))
            {
                // Compound accumulation into a local, parameter, field, or literal is
                // ordinary value arithmetic, not a member-mapping attempt; only
                // compound assignments that touch at least one property on a
                // transforming counterpart stay fail-closed gaps.
                var compoundFailing = left.State == SideResolutionState.Property ? right : left;
                if (compoundFailing.State is SideResolutionState.NonPropertySymbol or SideResolutionState.ValueLiteral)
                {
                    continue;
                }
                AddGapOrCount(
                    projectPath,
                    filePath,
                    assignment,
                    "PropertyMappingShapeUnsupported",
                    "compound-assignment",
                    containerMethod.Symbol,
                    left,
                    right,
                    ref gapCount,
                    ref suppressedGaps,
                    gaps);
                continue;
            }

            if (left.State == SideResolutionState.Property && right.State == SideResolutionState.Property)
            {
                var semanticGapState = GetAssignmentSemanticGapState(
                    assignment,
                    model,
                    containerMethod.Symbol,
                    left.Property!,
                    right.Property!);
                if (semanticGapState is not null)
                {
                    AddGapOrCount(
                        projectPath,
                        filePath,
                        assignment,
                        "PropertyMappingSemanticUnavailable",
                        semanticGapState,
                        containerMethod.Symbol,
                        left,
                        right,
                        ref gapCount,
                        ref suppressedGaps,
                    gaps);
                    continue;
                }

                if (SymbolEqualityComparer.Default.Equals(left.Property, right.Property))
                {
                    continue;
                }
                var sourceIdentity = CSharpSymbolIdentityProvider.TryCreate(right.Property);
                var targetIdentity = CSharpSymbolIdentityProvider.TryCreate(left.Property);
                if (sourceIdentity is not null
                    && targetIdentity is not null
                    && string.Equals(sourceIdentity.SymbolId, targetIdentity.SymbolId, StringComparison.Ordinal))
                {
                    // Distinct resolved symbols whose canonical IDs collapse (for
                    // example differently constructed generic instantiations) must
                    // fail closed; emitting them would seed false exact-ID self
                    // joins in the exact-identity composition slice.
                    AddGapOrCount(
                        projectPath,
                        filePath,
                        assignment,
                        "PropertyMappingTargetAmbiguous",
                        "canonical-identity-collision",
                        containerMethod.Symbol,
                        left,
                        right,
                        ref gapCount,
                        ref suppressedGaps,
                        gaps);
                    continue;
                }
                if (!SymbolEqualityComparer.Default.Equals(left.Property!.Type, right.Property!.Type))
                {
                    AddGapOrCount(
                        projectPath,
                        filePath,
                        assignment,
                        "PropertyMappingShapeUnsupported",
                        "type-conversion-required",
                        containerMethod.Symbol,
                        left,
                        right,
                        ref gapCount,
                        ref suppressedGaps,
                        gaps);
                    continue;
                }

                methodFacts.TryGetValue(containerMethod.Declaration, out var emittedForMethod);
                if (emittedForMethod >= MaxFactsPerMethod || documentFacts >= MaxFactsPerDocument)
                {
                    suppressedFacts++;
                    continue;
                }

                facts.Add(CreateMappingFact(
                    projectPath,
                    filePath,
                    assignment,
                    containerMethod.Symbol,
                    sourceProperty: right.Property!,
                    targetProperty: left.Property!));
                methodFacts[containerMethod.Declaration] = emittedForMethod + 1;
                documentFacts++;
                continue;
            }

            // A fully resolved non-property counterpart (local, parameter, field,
            // or method group) or a plain value literal is ordinary copy-in or
            // initialization rather than a member-mapping attempt; emitting gaps
            // there would be global noise.
            var failing = left.State == SideResolutionState.Property ? right : left;
            if (failing.State is SideResolutionState.NonPropertySymbol or SideResolutionState.ValueLiteral)
            {
                continue;
            }

            ClassifyFailure(
                projectPath,
                filePath,
                assignment,
                containerMethod.Symbol,
                left,
                right,
                ref gapCount,
                ref suppressedGaps,
                gaps);
        }

        if (suppressedFacts > 0 || suppressedGaps > 0)
        {
            // The truncation summary counts against the documented gap bound.
            // Reserve its slot deterministically if ordinary gaps filled it.
            if (gapCount >= MaxGapsPerDocument)
            {
                gaps.RemoveAt(gaps.Count - 1);
                suppressedGaps++;
            }
            gaps.Add(CreateTruncationGap(projectPath, filePath, suppressedFacts, suppressedGaps));
        }
    }

    private static IEnumerable<AssignmentExpressionSyntax> CollectAssignments(SyntaxNode root) =>
        root.DescendantNodes()
            .OfType<AssignmentExpressionSyntax>()
            .Where(assignment => assignment.Left is not TupleExpressionSyntax)
            .OrderBy(assignment => assignment.SpanStart);

    private static ContainerMethod? FindContainerMethod(AssignmentExpressionSyntax assignment, SemanticModel model)
    {
        foreach (var ancestor in assignment.Ancestors())
        {
            if (!IsMethodContainer(ancestor))
            {
                continue;
            }
            if (model.GetDeclaredSymbol(ancestor) is IMethodSymbol method
                && !method.IsImplicitlyDeclared)
            {
                return new ContainerMethod(ancestor, method);
            }
        }
        return null;
    }

    private static bool IsMethodContainer(SyntaxNode node) =>
        node is MethodDeclarationSyntax
            or LocalFunctionStatementSyntax
            or AccessorDeclarationSyntax
            or OperatorDeclarationSyntax
            or ConversionOperatorDeclarationSyntax;

    private sealed record ContainerMethod(SyntaxNode Declaration, IMethodSymbol Symbol);

    private enum SideResolutionState
    {
        Property,
        NonPropertySymbol,
        ValueLiteral,
        AmbiguousCandidates,
        UnresolvedBinding,
        DynamicValue,
        TransformExpression
    }

    private sealed record Side(
        SideResolutionState State,
        IPropertySymbol? Property = null,
        ISymbol? Symbol = null,
        string? Detail = null);

    private static Side ResolveSide(ExpressionSyntax expression, SemanticModel model)
    {
        var current = expression;
        while (true)
        {
            switch (current)
            {
                case ParenthesizedExpressionSyntax parenthesized:
                    current = parenthesized.Expression;
                    continue;
                case PostfixUnaryExpressionSyntax nullForgiving
                    when nullForgiving.IsKind(SyntaxKind.SuppressNullableWarningExpression):
                    current = nullForgiving.Operand;
                    continue;
                case CastExpressionSyntax cast:
                    return ResolveCast(cast, model);
                case ElementAccessExpressionSyntax:
                    return new Side(SideResolutionState.TransformExpression, Detail: "indexer-element");
                case ConditionalAccessExpressionSyntax:
                    return new Side(SideResolutionState.TransformExpression, Detail: "expression-transform");
                default:
                    return ResolveTerminal(current, model);
            }
        }
    }

    private static Side ResolveCast(CastExpressionSyntax cast, SemanticModel model)
    {
        var sourceType = model.GetTypeInfo(cast.Expression).Type;
        var targetType = model.GetTypeInfo(cast.Type).Type;
        if (sourceType is not null
            && targetType is not null
            && SymbolEqualityComparer.Default.Equals(sourceType, targetType))
        {
            return ResolveSide(cast.Expression, model);
        }
        return new Side(SideResolutionState.TransformExpression, Detail: "conversion");
    }

    private static Side ResolveTerminal(ExpressionSyntax expression, SemanticModel model)
    {
        ExpressionSyntax boundTarget;
        switch (expression)
        {
            case MemberAccessExpressionSyntax memberAccess when memberAccess.Name is IdentifierNameSyntax:
                boundTarget = memberAccess;
                break;
            case IdentifierNameSyntax identifier:
                boundTarget = identifier;
                break;
            case LiteralExpressionSyntax:
                return new Side(SideResolutionState.ValueLiteral, Detail: "value-literal");
            case DefaultExpressionSyntax:
                return new Side(SideResolutionState.ValueLiteral, Detail: "value-literal");
            case InvocationExpressionSyntax:
                return new Side(SideResolutionState.TransformExpression, Detail: "invocation");
            case BinaryExpressionSyntax binary when !binary.IsKind(SyntaxKind.SimpleAssignmentExpression):
                return new Side(SideResolutionState.TransformExpression, Detail: "binary-expression");
            case PrefixUnaryExpressionSyntax:
                return new Side(SideResolutionState.TransformExpression, Detail: "expression-transform");
            case PostfixUnaryExpressionSyntax:
                return new Side(SideResolutionState.TransformExpression, Detail: "expression-transform");
            case IsPatternExpressionSyntax:
                return new Side(SideResolutionState.TransformExpression, Detail: "pattern-expression");
            case InterpolatedStringExpressionSyntax:
                return new Side(SideResolutionState.TransformExpression, Detail: "interpolation");
            case ConditionalExpressionSyntax:
                return new Side(SideResolutionState.TransformExpression, Detail: "conditional-expression");
            case SwitchExpressionSyntax:
                return new Side(SideResolutionState.TransformExpression, Detail: "switch-expression");
            case ImplicitElementAccessSyntax:
                return new Side(SideResolutionState.TransformExpression, Detail: "indexer-element");
            case AwaitExpressionSyntax:
                return new Side(SideResolutionState.TransformExpression, Detail: "expression-transform");
            default:
                return new Side(SideResolutionState.TransformExpression, Detail: "expression-transform");
        }

        var typeInfo = model.GetTypeInfo(expression);
        if (typeInfo.Type is IDynamicTypeSymbol || typeInfo.ConvertedType is IDynamicTypeSymbol)
        {
            return new Side(SideResolutionState.DynamicValue, Detail: "dynamic");
        }

        var symbolInfo = model.GetSymbolInfo(boundTarget);
        if (symbolInfo.CandidateSymbols.Length >= 2)
        {
            return new Side(SideResolutionState.AmbiguousCandidates, Detail: "ambiguous-candidates");
        }
        if (symbolInfo.Symbol is not IPropertySymbol)
        {
            if (symbolInfo.Symbol is not null)
            {
                return new Side(SideResolutionState.NonPropertySymbol, Symbol: symbolInfo.Symbol, Detail: "non-property-symbol");
            }
            if (symbolInfo.CandidateSymbols.Length == 1)
            {
                return new Side(SideResolutionState.UnresolvedBinding, Detail: "incomplete-binding");
            }
            return new Side(SideResolutionState.UnresolvedBinding, Detail: "unresolved-binding");
        }

        var property = (IPropertySymbol)symbolInfo.Symbol!;
        if (property.IsIndexer || property.Parameters.Length > 0)
        {
            return new Side(SideResolutionState.TransformExpression, Detail: "indexer-element");
        }
        return new Side(SideResolutionState.Property, Property: property, Symbol: property);
    }

    private static int CountPropertySides(Side left, Side right) =>
        (left.State == SideResolutionState.Property ? 1 : 0)
        + (right.State == SideResolutionState.Property ? 1 : 0);

    private static string? GetAssignmentSemanticGapState(
        AssignmentExpressionSyntax assignment,
        SemanticModel model,
        IMethodSymbol containerMethod,
        IPropertySymbol targetProperty,
        IPropertySymbol sourceProperty)
    {
        // Record `with` initializers and future initializer forms are not the
        // admitted `new Target { ... }` object-initializer shape.
        if (assignment.Parent is InitializerExpressionSyntax initializer
            && !initializer.IsKind(SyntaxKind.ObjectInitializerExpression))
        {
            return "expression-transform";
        }

        // Roslyn can bind properties whose declared value type is unresolved.
        // Equal IErrorTypeSymbol instances are not compiler-resolved evidence
        // and therefore cannot support a Tier1 mapping.
        if (targetProperty.Type.TypeKind == TypeKind.Error
            || sourceProperty.Type.TypeKind == TypeKind.Error)
        {
            return "incomplete-binding";
        }

        var setter = targetProperty.SetMethod;
        if (setter is null)
        {
            return "incomplete-binding";
        }

        var within = (ISymbol?)containerMethod.ContainingType ?? containerMethod;
        if (!model.Compilation.IsSymbolAccessibleWithin(setter, within))
        {
            return "incomplete-binding";
        }

        var getter = sourceProperty.GetMethod;
        if (getter is null || !model.Compilation.IsSymbolAccessibleWithin(getter, within))
        {
            return "incomplete-binding";
        }

        // A property symbol can still resolve inside an invalid assignment
        // (for example a receiver/accessibility error in partial compilation).
        // Tier1 mapping evidence requires an error-free assignment span.
        return model.GetDiagnostics(assignment.Span)
            .Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            ? "incomplete-binding"
            : null;
    }

    private static void ClassifyFailure(
        string? projectPath,
        string filePath,
        AssignmentExpressionSyntax assignment,
        IMethodSymbol containerMethod,
        Side left,
        Side right,
        ref int gapCount,
        ref int truncatedGaps,
        List<SemanticFactCandidate> gaps)
    {
        var failingSide = left.State == SideResolutionState.Property ? right : left;
        var detail = failingSide.Detail ?? "unsupported";
        var (gapKind, stateValue) = failingSide.State switch
        {
            SideResolutionState.ValueLiteral => ("PropertyMappingShapeUnsupported", detail),
            SideResolutionState.AmbiguousCandidates => ("PropertyMappingTargetAmbiguous", detail),
            SideResolutionState.DynamicValue => ("PropertyMappingShapeUnsupported", detail),
            SideResolutionState.TransformExpression => ("PropertyMappingShapeUnsupported", detail),
            SideResolutionState.UnresolvedBinding => ("PropertyMappingSemanticUnavailable", detail),
            _ => ("PropertyMappingSemanticUnavailable", detail)
        };
        AddGapOrCount(
            projectPath,
            filePath,
            assignment,
            gapKind,
            stateValue,
            containerMethod,
            left,
            right,
            ref gapCount,
            ref truncatedGaps,
            gaps);
    }

    private static void AddGapOrCount(
        string? projectPath,
        string filePath,
        SyntaxNode node,
        string gapKind,
        string stateValue,
        IMethodSymbol containerMethod,
        Side left,
        Side right,
        ref int gapCount,
        ref int truncatedGaps,
        List<SemanticFactCandidate> gaps)
    {
        if (gapCount >= MaxGapsPerDocument)
        {
            truncatedGaps++;
            return;
        }
        var properties = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["coverageEffect"] = "reduces-direct-property-mapping-coverage",
            ["coverageLabel"] = ReducedCoverageLabel,
            ["gapKind"] = gapKind,
            ["limitations"] = Limitations,
            ["occurrenceCount"] = "1",
            ["sanitization"] = "categorical-symbol-identity",
            ["shapeState"] = stateValue
        };
        AddIdentity(properties, "scope", containerMethod);
        AddIdentity(properties, "sourceEndpoint", right.Property ?? right.Symbol);
        AddIdentity(properties, "targetEndpoint", left.Property ?? left.Symbol);
        gaps.Add(new SemanticFactCandidate(
            FactTypes.AnalysisGap,
            RuleIds.CSharpSemanticPropertyMappingGap,
            EvidenceTiers.Tier4Unknown,
            Span(filePath, node),
            projectPath,
            ContractElement: gapKind,
            Properties: properties,
            SourceStart: node.SpanStart,
            SourceLength: node.Span.Length));
        gapCount++;
    }

    private static SemanticFactCandidate CreateMappingFact(
        string? projectPath,
        string filePath,
        AssignmentExpressionSyntax assignment,
        IMethodSymbol containerMethod,
        IPropertySymbol sourceProperty,
        IPropertySymbol targetProperty)
    {
        var mappingShape = ClassifyShape(assignment);
        var properties = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["coverageLabel"] = CoverageLabel,
            ["direction"] = "source-to-target",
            ["limitations"] = Limitations,
            ["mappingShape"] = mappingShape,
            ["sanitization"] = "categorical-symbol-identity"
        };
        AddIdentity(properties, "sourceProperty", sourceProperty);
        AddIdentity(properties, "sourceType", sourceProperty.ContainingType);
        AddIdentity(properties, "targetProperty", targetProperty);
        AddIdentity(properties, "targetType", targetProperty.ContainingType);
        AddIdentity(properties, "containerMethod", containerMethod);

        return new SemanticFactCandidate(
            FactTypes.PropertyMappingDeclared,
            RuleIds.CSharpSemanticPropertyMapping,
            EvidenceTiers.Tier1Semantic,
            Span(filePath, assignment),
            projectPath,
            SourceDisplay(sourceProperty),
            TargetDisplay(targetProperty),
            mappingShape,
            properties,
            assignment.SpanStart,
            assignment.Span.Length);
    }

    internal static string ClassifyShape(AssignmentExpressionSyntax assignment)
    {
        if (assignment.Parent is InitializerExpressionSyntax initializer
            && initializer.IsKind(SyntaxKind.ObjectInitializerExpression))
        {
            return IsProjectionContext(assignment) ? "projection" : "object-initializer";
        }
        return "assignment";
    }

    private static bool IsProjectionContext(SyntaxNode node) =>
        node.Ancestors().Any(ancestor =>
            ancestor is AnonymousFunctionExpressionSyntax
                or QueryClauseSyntax
                or QueryContinuationSyntax);

    private static SemanticFactCandidate CreateTruncationGap(
        string? projectPath,
        string filePath,
        int suppressedFacts,
        int suppressedGaps)
    {
        var properties = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["coverageEffect"] = "reduces-direct-property-mapping-coverage",
            ["coverageLabel"] = ReducedCoverageLabel,
            ["gapKind"] = "PropertyMappingTruncated",
            ["limitations"] = Limitations,
            ["occurrenceCount"] = (suppressedFacts + suppressedGaps).ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["sanitization"] = "categorical-count",
            ["shapeState"] = "truncation",
            ["suppressedFactCount"] = suppressedFacts.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["suppressedGapCount"] = suppressedGaps.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };
        return new SemanticFactCandidate(
            FactTypes.AnalysisGap,
            RuleIds.CSharpSemanticPropertyMappingGap,
            EvidenceTiers.Tier4Unknown,
            new EvidenceSpan(filePath, 1, 1, null, "CSharpPropertyMappingExtractor", ScannerVersions.CSharpPropertyMappingExtractor),
            projectPath,
            ContractElement: "PropertyMappingTruncated",
            Properties: properties);
    }

    private static readonly SymbolDisplayFormat PropertyDisplayFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        memberOptions:
            SymbolDisplayMemberOptions.IncludeContainingType
            | SymbolDisplayMemberOptions.IncludeExplicitInterface,
        miscellaneousOptions:
            SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers);

    private static string SourceDisplay(IPropertySymbol property) =>
        property.ToDisplayString(PropertyDisplayFormat);

    private static string TargetDisplay(IPropertySymbol property) =>
        property.ToDisplayString(PropertyDisplayFormat);

    private static void AddIdentity(SortedDictionary<string, string> properties, string prefix, ISymbol? symbol)
    {
        var identity = CSharpSymbolIdentityProvider.TryCreate(symbol);
        if (identity is null)
        {
            return;
        }
        properties[$"{prefix}SymbolId"] = identity.SymbolId;
        properties[$"{prefix}SymbolKind"] = identity.SymbolKind;
        properties[$"{prefix}AssemblyName"] = identity.AssemblyName ?? string.Empty;
        properties[$"{prefix}AssemblyVersion"] = identity.AssemblyVersion ?? string.Empty;
        properties[$"{prefix}ContainingSymbolId"] = identity.ContainingSymbolId ?? string.Empty;
    }

    private static EvidenceSpan Span(string filePath, SyntaxNode node)
    {
        var span = node.SyntaxTree.GetLineSpan(node.Span);
        return new EvidenceSpan(
            FileInventory.NormalizeRelativePath(filePath),
            span.StartLinePosition.Line + 1,
            Math.Max(span.StartLinePosition.Line + 1, span.EndLinePosition.Line + 1),
            null,
            "CSharpPropertyMappingExtractor",
            ScannerVersions.CSharpPropertyMappingExtractor);
    }
}
