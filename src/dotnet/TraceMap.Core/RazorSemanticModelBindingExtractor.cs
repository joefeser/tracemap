using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TraceMap.Core;

internal static class RazorSemanticModelBindingExtractor
{
    private const string MicrosoftPublicKeyToken = "adb9793829ddae60";
    private const string CoverageLabel = "bounded-static-semantic-model-binding";
    private const string Limitations = "Static compiler evidence only; does not prove runtime binding, route selection, validation, handler execution, serializer behavior, authorization, or submitted values.";
    private const int MaxFactsPerDocument = 500;
    private const int MaxGapsPerDocument = 100;

    private static readonly IReadOnlyDictionary<string, string> TrustedTypes =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Microsoft.AspNetCore.Mvc.ControllerBase"] = "Microsoft.AspNetCore.Mvc.Core",
            ["Microsoft.AspNetCore.Mvc.NonControllerAttribute"] = "Microsoft.AspNetCore.Mvc.Core",
            ["Microsoft.AspNetCore.Mvc.NonActionAttribute"] = "Microsoft.AspNetCore.Mvc.Core",
            ["Microsoft.AspNetCore.Mvc.FromBodyAttribute"] = "Microsoft.AspNetCore.Mvc.Core",
            ["Microsoft.AspNetCore.Mvc.FromFormAttribute"] = "Microsoft.AspNetCore.Mvc.Core",
            ["Microsoft.AspNetCore.Mvc.FromServicesAttribute"] = "Microsoft.AspNetCore.Mvc.Core",
            ["Microsoft.AspNetCore.Mvc.BindPropertyAttribute"] = "Microsoft.AspNetCore.Mvc.Core",
            ["Microsoft.AspNetCore.Mvc.ModelBinding.BindNeverAttribute"] = "Microsoft.AspNetCore.Mvc.Core",
            ["Microsoft.AspNetCore.Mvc.HttpGetAttribute"] = "Microsoft.AspNetCore.Mvc.Core",
            ["Microsoft.AspNetCore.Mvc.HttpPostAttribute"] = "Microsoft.AspNetCore.Mvc.Core",
            ["Microsoft.AspNetCore.Mvc.HttpPutAttribute"] = "Microsoft.AspNetCore.Mvc.Core",
            ["Microsoft.AspNetCore.Mvc.HttpDeleteAttribute"] = "Microsoft.AspNetCore.Mvc.Core",
            ["Microsoft.AspNetCore.Mvc.HttpPatchAttribute"] = "Microsoft.AspNetCore.Mvc.Core",
            ["Microsoft.AspNetCore.Mvc.HttpHeadAttribute"] = "Microsoft.AspNetCore.Mvc.Core",
            ["Microsoft.AspNetCore.Mvc.HttpOptionsAttribute"] = "Microsoft.AspNetCore.Mvc.Core",
            ["Microsoft.AspNetCore.Mvc.RazorPages.PageModel"] = "Microsoft.AspNetCore.Mvc.RazorPages",
            ["Microsoft.AspNetCore.Mvc.RazorPages.NonHandlerAttribute"] = "Microsoft.AspNetCore.Mvc.RazorPages"
        };

    private static readonly IReadOnlyDictionary<string, string> HttpAttributeMethods =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Microsoft.AspNetCore.Mvc.HttpGetAttribute"] = "GET",
            ["Microsoft.AspNetCore.Mvc.HttpPostAttribute"] = "POST",
            ["Microsoft.AspNetCore.Mvc.HttpPutAttribute"] = "PUT",
            ["Microsoft.AspNetCore.Mvc.HttpDeleteAttribute"] = "DELETE",
            ["Microsoft.AspNetCore.Mvc.HttpPatchAttribute"] = "PATCH",
            ["Microsoft.AspNetCore.Mvc.HttpHeadAttribute"] = "HEAD",
            ["Microsoft.AspNetCore.Mvc.HttpOptionsAttribute"] = "OPTIONS"
        };

    public static void Extract(
        string? projectPath,
        string filePath,
        SyntaxNode root,
        SemanticModel model,
        List<SemanticFactCandidate> facts,
        List<SemanticFactCandidate> gaps)
    {
        var factCount = 0;
        var gapCount = 0;
        var truncatedFacts = 0;
        var truncatedGaps = 0;

        foreach (var methodSyntax in root.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .OrderBy(method => method.SpanStart))
        {
            if (model.GetDeclaredSymbol(methodSyntax) is not IMethodSymbol method
                || method.ContainingType is null)
            {
                continue;
            }
            if (method.PartialImplementationPart is not null)
            {
                continue;
            }

            if (InheritsTrusted(method.ContainingType, "Microsoft.AspNetCore.Mvc.RazorPages.PageModel")
                && !IsDiscoverablePageModel(method.ContainingType))
            {
                AddGapOrCount(
                    projectPath,
                    filePath,
                    methodSyntax,
                    "razor-page-handler-owner",
                    "RazorEndpointOwnerUnavailable",
                    method,
                    method.ContainingType,
                    ref gapCount,
                    ref truncatedGaps,
                    gaps,
                    stateKey: "ownerState",
                    stateValue: "page-model-type-not-discoverable");
                continue;
            }

            var controllerOwners = InheritsTrusted(method.ContainingType, "Microsoft.AspNetCore.Mvc.ControllerBase")
                ? EffectiveControllerOwners(method, model.Compilation)
                : [];
            if (InheritsTrusted(method.ContainingType, "Microsoft.AspNetCore.Mvc.ControllerBase")
                && controllerOwners.Length == 0)
            {
                AddGapOrCount(
                    projectPath,
                    filePath,
                    methodSyntax,
                    "mvc-controller-owner",
                    "RazorEndpointOwnerUnavailable",
                    method,
                    method.ContainingType,
                    ref gapCount,
                    ref truncatedGaps,
                    gaps,
                    stateKey: "ownerState",
                    stateValue: "controller-type-not-discoverable");
                continue;
            }

            var genericOwnerKind = PotentialOwnerKind(method);
            if (method.IsGenericMethod && genericOwnerKind is not null)
            {
                AddGapOrCount(
                    projectPath,
                    filePath,
                    methodSyntax,
                    genericOwnerKind,
                    "RazorEndpointOwnerUnavailable",
                    method,
                    method.ContainingType,
                    ref gapCount,
                    ref truncatedGaps,
                    gaps,
                    stateKey: "ownerState",
                    stateValue: "generic-method-not-discoverable");
                foreach (var parameterSyntax in methodSyntax.ParameterList.Parameters.OrderBy(parameter => parameter.SpanStart))
                {
                    if (model.GetDeclaredSymbol(parameterSyntax) is IParameterSymbol { Type: ITypeParameterSymbol } parameter)
                    {
                        AddGapOrCount(
                            projectPath,
                            filePath,
                            parameterSyntax,
                            genericOwnerKind,
                            "RazorBindingTypeUnavailable",
                            parameter,
                            parameter.Type,
                            ref gapCount,
                            ref truncatedGaps,
                            gaps);
                    }
                }
                continue;
            }

            var ownerKind = OwnerKind(method);
            if (ownerKind is null)
            {
                if (method.Parameters.Length > 0 && HasUntrustedFrameworkOwnerShape(method.ContainingType))
                {
                    AddGapOrCount(
                        projectPath,
                        filePath,
                        methodSyntax,
                        "untrusted-framework-owner",
                        "RazorFrameworkIdentityUnavailable",
                        method,
                        method.ContainingType,
                        ref gapCount,
                        ref truncatedGaps,
                        gaps);
                }
                continue;
            }

            var httpMethods = ownerKind == "mvc-action-parameter"
                ? ActionHttpMethods(method)
                : HandlerHttpMethods(method.Name);
            foreach (var parameterSyntax in methodSyntax.ParameterList.Parameters.OrderBy(parameter => parameter.SpanStart))
            {
                if (model.GetDeclaredSymbol(parameterSyntax) is not IParameterSymbol parameter)
                {
                    continue;
                }

                var source = ParameterSource(parameter);
                if (source == "services")
                {
                    continue;
                }
                if (source is null)
                {
                    AddGapOrCount(
                        projectPath,
                        filePath,
                        parameterSyntax,
                        ownerKind,
                        "AmbiguousRazorBindingTarget",
                        parameter,
                        parameter.Type,
                        ref gapCount,
                        ref truncatedGaps,
                        gaps);
                    continue;
                }
                if (parameter.Type is not INamedTypeSymbol modelType
                    || modelType.TypeKind == TypeKind.Error
                    || modelType.IsUnboundGenericType)
                {
                    AddGapOrCount(
                        projectPath,
                        filePath,
                        parameterSyntax,
                        ownerKind,
                        "RazorBindingTypeUnavailable",
                        parameter,
                        parameter.Type,
                        ref gapCount,
                        ref truncatedGaps,
                        gaps,
                        stateKey: "typeState",
                        stateValue: parameter.Type is IArrayTypeSymbol ? "unsupported-collection" : TypeState(parameter.Type));
                    continue;
                }
                if (IsTerminalParameterType(modelType))
                {
                    continue;
                }
                if (modelType.IsRecord || IsCollectionType(modelType))
                {
                    AddGapOrCount(
                        projectPath,
                        filePath,
                        parameterSyntax,
                        ownerKind,
                        "RazorBindingTypeUnavailable",
                        parameter,
                        modelType,
                        ref gapCount,
                        ref truncatedGaps,
                        gaps,
                        stateKey: "typeState",
                        stateValue: modelType.IsRecord ? "unsupported-record" : "unsupported-collection");
                    continue;
                }
                if (modelType.Locations.All(location => !location.IsInSource))
                {
                    AddGapOrCount(projectPath, filePath, parameterSyntax, ownerKind, "RazorBindingTypeUnavailable", parameter, modelType, ref gapCount, ref truncatedGaps, gaps);
                    continue;
                }
                if (source != "body" && !HasPublicParameterlessConstruction(modelType))
                {
                    AddGapOrCount(
                        projectPath,
                        filePath,
                        parameterSyntax,
                        ownerKind,
                        "RazorBindingTypeUnavailable",
                        parameter,
                        modelType,
                        ref gapCount,
                        ref truncatedGaps,
                        gaps,
                        stateKey: "typeState",
                        stateValue: "constructor-unavailable");
                    continue;
                }

                var owners = ownerKind == "mvc-action-parameter" ? controllerOwners : [method];
                foreach (var owner in owners)
                {
                    ExpandModelProperties(
                        projectPath,
                        filePath,
                        parameterSyntax,
                        ownerKind,
                        source,
                        owner,
                        method,
                        parameter,
                        modelType,
                        httpMethods,
                        supportsGet: null,
                        ref factCount,
                        ref gapCount,
                        ref truncatedFacts,
                        ref truncatedGaps,
                        facts,
                        gaps);
                }
            }
        }

        foreach (var propertySyntax in root.DescendantNodes().OfType<PropertyDeclarationSyntax>()
            .OrderBy(property => property.SpanStart))
        {
            if (model.GetDeclaredSymbol(propertySyntax) is not IPropertySymbol property
                || property.ContainingType is null)
            {
                continue;
            }
            var bindPropertyShape = AttributeByMetadataName(property, "Microsoft.AspNetCore.Mvc.BindPropertyAttribute");
            if (bindPropertyShape is null)
            {
                continue;
            }
            var trustedPageModel = InheritsTrusted(property.ContainingType, "Microsoft.AspNetCore.Mvc.RazorPages.PageModel");
            var trustedBindProperty = IsTrustedMetadataType(bindPropertyShape.AttributeClass, "Microsoft.AspNetCore.Mvc.BindPropertyAttribute");
            if (!trustedPageModel || !trustedBindProperty)
            {
                if (HasUntrustedFrameworkOwnerShape(property.ContainingType)
                    || (trustedPageModel && !trustedBindProperty))
                {
                    AddGapOrCount(
                        projectPath,
                        filePath,
                        propertySyntax,
                        "untrusted-framework-owner",
                        "RazorFrameworkIdentityUnavailable",
                        property,
                        property.ContainingType,
                        ref gapCount,
                        ref truncatedGaps,
                        gaps);
                }
                continue;
            }
            if (!IsDiscoverablePageModel(property.ContainingType))
            {
                AddGapOrCount(
                    projectPath,
                    filePath,
                    propertySyntax,
                    "razor-page-property",
                    "RazorEndpointOwnerUnavailable",
                    property,
                    property.ContainingType,
                    ref gapCount,
                    ref truncatedGaps,
                    gaps,
                    stateKey: "ownerState",
                    stateValue: "page-model-type-not-discoverable");
                continue;
            }
            var bindProperty = bindPropertyShape;

            if (!IsEligibleProperty(property))
            {
                AddGapOrCount(projectPath, filePath, propertySyntax, "razor-page-property", "RazorBindingPropertyUnavailable", property, property.Type, ref gapCount, ref truncatedGaps, gaps);
                continue;
            }

            if (factCount >= MaxFactsPerDocument)
            {
                truncatedFacts++;
                continue;
            }

            var supportsGet = bindProperty.NamedArguments.FirstOrDefault(argument => argument.Key == "SupportsGet").Value.Value as bool? ?? false;
            facts.Add(CreateTarget(
                projectPath,
                filePath,
                propertySyntax,
                "razor-page-property",
                "bind-property",
                property,
                parameter: null,
                endpointMethod: null,
                property.ContainingType,
                property,
                httpMethods: [],
                supportsGet));
            factCount++;
        }

        if (truncatedFacts > 0)
        {
            gaps.Add(CreateBoundGap(projectPath, filePath, "RazorBindingTargetTruncated", truncatedFacts));
        }
        if (truncatedGaps > 0)
        {
            gaps.Add(CreateBoundGap(projectPath, filePath, "RazorBindingGapTruncated", truncatedGaps));
        }
    }

    private static void ExpandModelProperties(
        string? projectPath,
        string filePath,
        SyntaxNode evidenceNode,
        string bindingKind,
        string parameterSource,
        ISymbol owner,
        IMethodSymbol endpointMethod,
        IParameterSymbol parameter,
        INamedTypeSymbol modelType,
        IReadOnlyList<string> httpMethods,
        bool? supportsGet,
        ref int factCount,
        ref int gapCount,
        ref int truncatedFacts,
        ref int truncatedGaps,
        List<SemanticFactCandidate> facts,
        List<SemanticFactCandidate> gaps)
    {
        var properties = EffectiveModelProperties(modelType);
        if (FirstMetadataBase(modelType) is INamedTypeSymbol metadataBase)
        {
            AddGapOrCount(
                projectPath,
                filePath,
                evidenceNode,
                bindingKind,
                "RazorBindingExternalBaseUnavailable",
                owner,
                metadataBase,
                ref gapCount,
                ref truncatedGaps,
                gaps,
                stateKey: "typeState",
                stateValue: "external-base-properties-unavailable",
                owner: owner,
                endpointMethod: endpointMethod,
                parameter: parameter);
        }
        if (properties.Length == 0)
        {
            AddGapOrCount(
                projectPath, filePath, evidenceNode, bindingKind, "RazorBindingPropertyUnavailable", owner, modelType,
                ref gapCount, ref truncatedGaps, gaps, owner: owner, endpointMethod: endpointMethod, parameter: parameter);
            return;
        }

        foreach (var property in properties)
        {
            if (!IsEligibleProperty(property))
            {
                AddGapOrCount(
                    projectPath, filePath, evidenceNode, bindingKind, "RazorBindingPropertyUnavailable", property, property.Type,
                    ref gapCount, ref truncatedGaps, gaps, owner: owner, endpointMethod: endpointMethod, parameter: parameter);
                continue;
            }
            if (factCount >= MaxFactsPerDocument)
            {
                truncatedFacts++;
                continue;
            }

            facts.Add(CreateTarget(projectPath, filePath, evidenceNode, bindingKind, parameterSource, owner, parameter, endpointMethod, modelType, property, httpMethods, supportsGet));
            factCount++;
        }
    }

    private static SemanticFactCandidate CreateTarget(
        string? projectPath,
        string filePath,
        SyntaxNode node,
        string bindingKind,
        string parameterSource,
        ISymbol owner,
        IParameterSymbol? parameter,
        IMethodSymbol? endpointMethod,
        INamedTypeSymbol modelType,
        IPropertySymbol property,
        IReadOnlyList<string> httpMethods,
        bool? supportsGet)
    {
        var properties = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["bindingKind"] = bindingKind,
            ["coverageLabel"] = CoverageLabel,
            ["httpMethods"] = string.Join(";", httpMethods.OrderBy(value => value, StringComparer.Ordinal)),
            ["limitations"] = Limitations,
            ["modelKind"] = parameterSource == "body" ? "dto" : "view-model",
            ["modelType"] = modelType.Name,
            ["modelTypeDisplay"] = modelType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            ["ownerFamily"] = bindingKind,
            ["parameterName"] = parameter?.Name ?? string.Empty,
            ["parameterOrdinal"] = parameter?.Ordinal.ToString() ?? string.Empty,
            ["parameterSource"] = parameterSource,
            ["propertyName"] = property.Name,
            ["propertyPath"] = property.Name,
            ["propertyType"] = property.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            ["reconciliationProfileVersion"] = "razor-model-binding/1.0",
            ["supportsGet"] = supportsGet?.ToString().ToLowerInvariant() ?? string.Empty,
            ["tier3ReconciliationState"] = "contextual-support-only-missing-canonical-identity",
            ["uiFramework"] = "razor",
            ["valueStored"] = "safe-metadata-only"
        };
        AddFrameworkAdmission(properties, bindingKind, parameterSource, owner, parameter, property);
        AddIdentity(properties, "owner", owner);
        AddIdentity(properties, "parameter", parameter);
        AddIdentity(properties, "modelType", modelType);
        AddIdentity(properties, "target", property);
        if (endpointMethod is not null)
        {
            if (bindingKind == "mvc-action-parameter")
            {
                properties["actionName"] = endpointMethod.Name;
                properties["controllerName"] = ControllerName((owner as INamedTypeSymbol ?? endpointMethod.ContainingType).Name);
            }
            else
            {
                properties["handlerName"] = endpointMethod.Name;
                properties["pageModelName"] = endpointMethod.ContainingType.Name;
            }
        }
        else
        {
            properties["pageModelName"] = property.ContainingType.Name;
        }

        return new SemanticFactCandidate(
            FactTypes.RazorModelBindingTarget,
            RuleIds.CSharpRazorSemanticModelBinding,
            EvidenceTiers.Tier1Semantic,
            Span(filePath, node),
            projectPath,
            owner.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            property.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            property.Name,
            properties,
            node.SpanStart,
            node.Span.Length);
    }

    private static string? OwnerKind(IMethodSymbol method) =>
        method.IsGenericMethod ? null : PotentialOwnerKind(method);

    private static string? PotentialOwnerKind(IMethodSymbol method)
    {
        if (method.MethodKind != MethodKind.Ordinary
            || method.IsStatic
            || method.IsAbstract
            || method.DeclaredAccessibility != Accessibility.Public
            || method.IsImplicitlyDeclared)
        {
            return null;
        }
        if (InheritsTrusted(method.ContainingType, "Microsoft.AspNetCore.Mvc.ControllerBase"))
        {
            return !HasTrustedAttributeInTypeHierarchy(method.ContainingType, "Microsoft.AspNetCore.Mvc.NonControllerAttribute")
                && !HasTrustedAttributeInOverrideChain(method, "Microsoft.AspNetCore.Mvc.NonActionAttribute")
                ? "mvc-action-parameter"
                : null;
        }
        return InheritsTrusted(method.ContainingType, "Microsoft.AspNetCore.Mvc.RazorPages.PageModel")
            && !HasTrustedAttributeInOverrideChain(method, "Microsoft.AspNetCore.Mvc.RazorPages.NonHandlerAttribute")
            && HandlerHttpMethods(method.Name).Length > 0
            ? "razor-page-handler-parameter"
            : null;
    }

    private static IPropertySymbol[] EffectiveModelProperties(INamedTypeSymbol modelType)
    {
        var properties = new List<IPropertySymbol>();
        var seenNames = new HashSet<string>(StringComparer.Ordinal);
        for (var current = modelType;
            current is not null && current.Locations.Any(location => location.IsInSource);
            current = current.BaseType)
        {
            foreach (var property in current.GetMembers().OfType<IPropertySymbol>()
                .OrderBy(candidate => candidate.MetadataName, StringComparer.Ordinal)
                .ThenBy(candidate => candidate.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), StringComparer.Ordinal))
            {
                if (seenNames.Add(property.MetadataName))
                {
                    properties.Add(property);
                }
            }
        }
        return properties
            .OrderBy(property => property.MetadataName, StringComparer.Ordinal)
            .ThenBy(property => property.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), StringComparer.Ordinal)
            .ToArray();
    }

    private static INamedTypeSymbol? FirstMetadataBase(INamedTypeSymbol modelType)
    {
        for (var current = modelType.BaseType; current is not null; current = current.BaseType)
        {
            if (current.Locations.Any(location => location.IsInSource))
            {
                continue;
            }
            return current.SpecialType == SpecialType.System_Object ? null : current;
        }
        return null;
    }

    private static bool IsDiscoverableController(INamedTypeSymbol type) =>
        type.TypeKind == TypeKind.Class
        && !type.IsAbstract
        && type.DeclaredAccessibility == Accessibility.Public
        && type.ContainingType is null
        && !type.TypeArguments.Any(argument => argument.TypeKind == TypeKind.TypeParameter);

    private static bool IsDiscoverablePageModel(INamedTypeSymbol type) =>
        type.TypeKind == TypeKind.Class
        && !type.IsAbstract
        && type.DeclaredAccessibility == Accessibility.Public
        && type.ContainingType is null
        && !type.TypeArguments.Any(argument => argument.TypeKind == TypeKind.TypeParameter);

    private static bool HasPublicParameterlessConstruction(INamedTypeSymbol type) =>
        type.TypeKind == TypeKind.Struct
        || type.InstanceConstructors.Any(constructor =>
            constructor.DeclaredAccessibility == Accessibility.Public
            && constructor.Parameters.Length == 0);

    private static bool IsTerminalParameterType(INamedTypeSymbol type)
    {
        if (type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
            && type.TypeArguments.FirstOrDefault() is INamedTypeSymbol nullableValue)
        {
            return IsTerminalParameterType(nullableValue);
        }
        if (type.SpecialType != SpecialType.None || type.TypeKind == TypeKind.Enum)
        {
            return true;
        }
        return MetadataName(type) is
            "System.Guid" or
            "System.DateTime" or
            "System.DateTimeOffset" or
            "System.DateOnly" or
            "System.TimeOnly" or
            "System.TimeSpan" or
            "System.Threading.CancellationToken" or
            "System.Uri" or
            "Microsoft.AspNetCore.Http.IFormFile";
    }

    private static bool IsCollectionType(INamedTypeSymbol type) =>
        MetadataName(type.OriginalDefinition) is
            "System.Collections.Generic.IEnumerable<T>" or
            "System.Collections.Generic.ICollection<T>" or
            "System.Collections.Generic.IList<T>" or
            "System.Collections.Generic.IReadOnlyCollection<T>" or
            "System.Collections.Generic.IReadOnlyList<T>" or
            "System.Collections.Generic.List<T>"
        || type.AllInterfaces.Any(candidate =>
            MetadataName(candidate.OriginalDefinition) == "System.Collections.Generic.IEnumerable<T>");

    private static ISymbol[] EffectiveControllerOwners(IMethodSymbol method, Compilation compilation)
    {
        if (IsDiscoverableController(method.ContainingType))
        {
            return [method];
        }
        if (!method.ContainingType.IsAbstract
            || method.IsAbstract
            || method.IsGenericMethod
            || HasTrustedAttributeInTypeHierarchy(method.ContainingType, "Microsoft.AspNetCore.Mvc.NonControllerAttribute")
            || HasTrustedAttributeInOverrideChain(method, "Microsoft.AspNetCore.Mvc.NonActionAttribute"))
        {
            return [];
        }

        return AllSourceTypes(compilation.Assembly.GlobalNamespace)
            .Where(IsDiscoverableController)
            .Where(candidate => !HasTrustedAttributeInTypeHierarchy(candidate, "Microsoft.AspNetCore.Mvc.NonControllerAttribute"))
            .Where(candidate => InheritsFrom(candidate, method.ContainingType))
            .Where(candidate => !HasInterveningOverride(candidate, method))
            .OrderBy(candidate => candidate.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), StringComparer.Ordinal)
            .Cast<ISymbol>()
            .ToArray();
    }

    private static bool HasInterveningOverride(INamedTypeSymbol candidate, IMethodSymbol method)
    {
        for (var current = candidate; current is not null && !SymbolEqualityComparer.Default.Equals(current, method.ContainingType); current = current.BaseType)
        {
            if (current.GetMembers(method.Name).OfType<IMethodSymbol>().Any(member => HasEffectiveSignature(member, method)))
            {
                return true;
            }
        }
        return false;
    }

    private static bool HasEffectiveSignature(IMethodSymbol candidate, IMethodSymbol expectedBase) =>
        candidate.Arity == expectedBase.Arity
        && candidate.Parameters.Length == expectedBase.Parameters.Length
        && candidate.Parameters.Zip(expectedBase.Parameters).All(pair =>
            pair.First.RefKind == pair.Second.RefKind
            && SymbolEqualityComparer.Default.Equals(pair.First.Type, pair.Second.Type));

    private static IEnumerable<INamedTypeSymbol> AllSourceTypes(INamespaceSymbol root)
    {
        foreach (var member in root.GetMembers().OrderBy(member => member.Name, StringComparer.Ordinal))
        {
            if (member is INamespaceSymbol childNamespace)
            {
                foreach (var type in AllSourceTypes(childNamespace))
                {
                    yield return type;
                }
            }
            else if (member is INamedTypeSymbol type)
            {
                foreach (var nested in SelfAndNestedTypes(type))
                {
                    yield return nested;
                }
            }
        }
    }

    private static IEnumerable<INamedTypeSymbol> SelfAndNestedTypes(INamedTypeSymbol type)
    {
        yield return type;
        foreach (var nested in type.GetTypeMembers().OrderBy(candidate => candidate.Name, StringComparer.Ordinal))
        {
            foreach (var descendant in SelfAndNestedTypes(nested))
            {
                yield return descendant;
            }
        }
    }

    private static bool InheritsFrom(INamedTypeSymbol type, INamedTypeSymbol expectedBase)
    {
        for (var current = type.BaseType; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, expectedBase))
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsEligibleProperty(IPropertySymbol property) =>
        !property.IsStatic
        && !property.IsIndexer
        && property.Parameters.Length == 0
        && !property.ReturnsByRef
        && !property.ReturnsByRefReadonly
        && property.DeclaredAccessibility == Accessibility.Public
        && property.SetMethod is { DeclaredAccessibility: Accessibility.Public, IsInitOnly: false }
        && property.ExplicitInterfaceImplementations.Length == 0
        && TrustedAttribute(property, "Microsoft.AspNetCore.Mvc.ModelBinding.BindNeverAttribute") is null
        && property.Locations.Any(location => location.IsInSource);

    private static string? ParameterSource(IParameterSymbol parameter)
    {
        if (TrustedAttribute(parameter, "Microsoft.AspNetCore.Mvc.FromServicesAttribute") is not null)
        {
            return "services";
        }
        var body = TrustedAttribute(parameter, "Microsoft.AspNetCore.Mvc.FromBodyAttribute") is not null;
        var form = TrustedAttribute(parameter, "Microsoft.AspNetCore.Mvc.FromFormAttribute") is not null;
        if (body && form)
        {
            return null;
        }
        if (body)
        {
            return "body";
        }
        if (form)
        {
            return "form";
        }
        return "convention";
    }

    private static string[] ActionHttpMethods(IMethodSymbol method) =>
        OverrideChain(method)
            .SelectMany(candidate => candidate.GetAttributes())
            .Where(attribute => HttpAttributeMethods.Keys.Any(name => IsTrustedMetadataType(attribute.AttributeClass, name)))
            .Select(attribute => HttpAttributeMethods[MetadataName(attribute.AttributeClass)])
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

    private static IEnumerable<IMethodSymbol> OverrideChain(IMethodSymbol method)
    {
        for (var current = method; current is not null; current = current.OverriddenMethod)
        {
            yield return current;
        }
    }

    private static string[] HandlerHttpMethods(string methodName)
    {
        if (!methodName.StartsWith("On", StringComparison.Ordinal) || methodName.Length <= 2)
        {
            return [];
        }
        var conventionName = methodName.EndsWith("Async", StringComparison.Ordinal)
            ? methodName[..^"Async".Length]
            : methodName;
        if (conventionName.Length <= 2)
        {
            return [];
        }

        var handlerNameStart = conventionName.Length;
        for (var index = 3; index < conventionName.Length; index++)
        {
            if (char.IsUpper(conventionName[index]))
            {
                handlerNameStart = index;
                break;
            }
        }
        return [conventionName[2..handlerNameStart].ToUpperInvariant()];
    }

    private static bool InheritsTrusted(INamedTypeSymbol type, string metadataName)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (IsTrustedMetadataType(current, metadataName))
            {
                return true;
            }
        }
        return false;
    }

    private static bool HasUntrustedFrameworkOwnerShape(INamedTypeSymbol type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            var name = MetadataName(current);
            if ((name == "Microsoft.AspNetCore.Mvc.ControllerBase"
                    || name == "Microsoft.AspNetCore.Mvc.RazorPages.PageModel")
                && !IsTrustedMetadataType(current, name))
            {
                return true;
            }
        }
        return false;
    }

    private static AttributeData? TrustedAttribute(ISymbol symbol, string metadataName) =>
        symbol.GetAttributes().FirstOrDefault(attribute => IsTrustedMetadataType(attribute.AttributeClass, metadataName));

    private static bool HasTrustedAttributeInTypeHierarchy(INamedTypeSymbol type, string metadataName)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (TrustedAttribute(current, metadataName) is not null)
            {
                return true;
            }
        }
        return false;
    }

    private static bool HasTrustedAttributeInOverrideChain(IMethodSymbol method, string metadataName)
    {
        for (var current = method; current is not null; current = current.OverriddenMethod)
        {
            if (TrustedAttribute(current, metadataName) is not null)
            {
                return true;
            }
        }
        return false;
    }

    private static AttributeData? AttributeByMetadataName(ISymbol symbol, string metadataName) =>
        symbol.GetAttributes().FirstOrDefault(attribute =>
            string.Equals(MetadataName(attribute.AttributeClass), metadataName, StringComparison.Ordinal));

    private static bool IsTrustedMetadataType(INamedTypeSymbol? type, string metadataName)
    {
        if (type is null
            || !TrustedTypes.TryGetValue(metadataName, out var assemblyName)
            || type.Locations.Any(location => location.IsInSource)
            || !string.Equals(MetadataName(type), metadataName, StringComparison.Ordinal)
            || !string.Equals(type.ContainingAssembly?.Identity.Name, assemblyName, StringComparison.Ordinal))
        {
            return false;
        }
        return string.Equals(PublicKeyToken(type.ContainingAssembly), MicrosoftPublicKeyToken, StringComparison.Ordinal);
    }

    private static string MetadataName(INamedTypeSymbol? type) =>
        type?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Replace("global::", string.Empty, StringComparison.Ordinal) ?? string.Empty;

    private static string PublicKeyToken(IAssemblySymbol? assembly) =>
        assembly is null ? string.Empty : Convert.ToHexString(assembly.Identity.PublicKeyToken.ToArray()).ToLowerInvariant();

    private static string ControllerName(string typeName) =>
        typeName.EndsWith("Controller", StringComparison.Ordinal) ? typeName[..^"Controller".Length] : typeName;

    private static void AddFrameworkAdmission(
        SortedDictionary<string, string> properties,
        string bindingKind,
        string parameterSource,
        ISymbol owner,
        IParameterSymbol? parameter,
        IPropertySymbol property)
    {
        var ownerType = owner as INamedTypeSymbol ?? owner.ContainingType ?? property.ContainingType;
        var frameworkTypeName = bindingKind == "mvc-action-parameter"
            ? "Microsoft.AspNetCore.Mvc.ControllerBase"
            : "Microsoft.AspNetCore.Mvc.RazorPages.PageModel";
        var frameworkType = FindTrustedBase(ownerType, frameworkTypeName);
        AddTrustedMetadataIdentity(properties, "frameworkOwner", frameworkType);

        AttributeData? bindingAttribute = parameterSource switch
        {
            "body" when parameter is not null => TrustedAttribute(parameter, "Microsoft.AspNetCore.Mvc.FromBodyAttribute"),
            "form" when parameter is not null => TrustedAttribute(parameter, "Microsoft.AspNetCore.Mvc.FromFormAttribute"),
            "bind-property" => TrustedAttribute(property, "Microsoft.AspNetCore.Mvc.BindPropertyAttribute"),
            _ => null
        };
        AddTrustedMetadataIdentity(properties, "bindingAttribute", bindingAttribute?.AttributeClass);
    }

    private static INamedTypeSymbol? FindTrustedBase(INamedTypeSymbol? type, string metadataName)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (IsTrustedMetadataType(current, metadataName))
            {
                return current;
            }
        }
        return null;
    }

    private static void AddTrustedMetadataIdentity(
        SortedDictionary<string, string> properties,
        string prefix,
        INamedTypeSymbol? type)
    {
        if (type is null)
        {
            return;
        }
        properties[$"{prefix}Type"] = MetadataName(type);
        properties[$"{prefix}AssemblyName"] = type.ContainingAssembly.Identity.Name;
        properties[$"{prefix}PublicKeyToken"] = PublicKeyToken(type.ContainingAssembly);
    }

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

    private static void AddGapOrCount(
        string? projectPath,
        string filePath,
        SyntaxNode node,
        string bindingKind,
        string gapKind,
        ISymbol scope,
        ITypeSymbol? targetType,
        ref int gapCount,
        ref int truncatedGaps,
        List<SemanticFactCandidate> gaps,
        string? stateKey = null,
        string? stateValue = null,
        ISymbol? owner = null,
        IMethodSymbol? endpointMethod = null,
        IParameterSymbol? parameter = null)
    {
        if (gapCount >= MaxGapsPerDocument)
        {
            truncatedGaps++;
            return;
        }
        var properties = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["bindingKind"] = bindingKind,
            ["coverageEffect"] = "reduces-semantic-model-binding-coverage",
            ["coverageLabel"] = "reduced-static-semantic-model-binding",
            ["gapKind"] = gapKind,
            ["limitations"] = Limitations,
            ["occurrenceCount"] = "1",
            ["sanitization"] = "categorical-symbol-identity"
        };
        if (gapKind == "RazorBindingTypeUnavailable")
        {
            properties["typeState"] = TypeState(targetType);
        }
        if (gapKind == "RazorFrameworkIdentityUnavailable")
        {
            properties["frameworkState"] = "source-or-unsigned-lookalike";
        }
        if (stateKey is not null && stateValue is not null)
        {
            properties[stateKey] = stateValue;
        }
        AddIdentity(properties, "scope", scope);
        AddIdentity(properties, "targetType", targetType);
        AddIdentity(properties, "owner", owner);
        AddIdentity(properties, "endpointMethod", endpointMethod);
        AddIdentity(properties, "parameter", parameter);
        if (parameter is not null)
        {
            properties["parameterOrdinal"] = parameter.Ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
        gaps.Add(new SemanticFactCandidate(
            FactTypes.AnalysisGap,
            RuleIds.CSharpRazorSemanticModelBindingGap,
            EvidenceTiers.Tier4Unknown,
            Span(filePath, node),
            projectPath,
            ContractElement: gapKind,
            Properties: properties,
            SourceStart: node.SpanStart,
            SourceLength: node.Span.Length));
        gapCount++;
    }

    private static string TypeState(ITypeSymbol? type) => type switch
    {
        null => "unresolved",
        IDynamicTypeSymbol => "dynamic",
        ITypeParameterSymbol => "type-parameter",
        { TypeKind: TypeKind.Error } => "error-or-ambiguous",
        INamedTypeSymbol named when named.Locations.All(location => !location.IsInSource) => "external-unavailable",
        INamedTypeSymbol { IsUnboundGenericType: true } => "unbound-generic",
        _ => "unsupported"
    };

    private static SemanticFactCandidate CreateBoundGap(string? projectPath, string filePath, string gapKind, int count) =>
        new(
            FactTypes.AnalysisGap,
            RuleIds.CSharpRazorSemanticModelBindingGap,
            EvidenceTiers.Tier4Unknown,
            new EvidenceSpan(filePath, 1, 1, null, "CSharpSemanticExtractor", ScannerVersions.CSharpSemanticExtractor),
            projectPath,
            ContractElement: gapKind,
            Properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["coverageEffect"] = "reduces-semantic-model-binding-coverage",
                ["coverageLabel"] = "reduced-static-semantic-model-binding",
                ["gapKind"] = gapKind,
                ["limitations"] = Limitations,
                ["occurrenceCount"] = count.ToString(),
                ["sanitization"] = "categorical-count"
            });

    private static EvidenceSpan Span(string filePath, SyntaxNode node)
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
}
