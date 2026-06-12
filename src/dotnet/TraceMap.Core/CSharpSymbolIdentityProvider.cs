using Microsoft.CodeAnalysis;

namespace TraceMap.Core;

public static class CSharpSymbolIdentityProvider
{
    private static readonly SymbolDisplayFormat DisplayFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        memberOptions:
            SymbolDisplayMemberOptions.IncludeContainingType
            | SymbolDisplayMemberOptions.IncludeParameters
            | SymbolDisplayMemberOptions.IncludeType
            | SymbolDisplayMemberOptions.IncludeRef
            | SymbolDisplayMemberOptions.IncludeExplicitInterface,
        parameterOptions:
            SymbolDisplayParameterOptions.IncludeType
            | SymbolDisplayParameterOptions.IncludeName
            | SymbolDisplayParameterOptions.IncludeParamsRefOut
            | SymbolDisplayParameterOptions.IncludeDefaultValue,
        miscellaneousOptions:
            SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers
            | SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    public static SymbolIdentity? TryCreate(ISymbol? symbol)
    {
        if (symbol is null)
        {
            return null;
        }

        var symbolId = CreateSymbolId(symbol);
        if (string.IsNullOrWhiteSpace(symbolId))
        {
            return null;
        }

        var containingSymbolId = TryCreateContainingSymbolId(symbol);
        return new SymbolIdentity(
            symbolId,
            "csharp",
            symbol.Kind.ToString(),
            symbol.ToDisplayString(DisplayFormat),
            symbol.ContainingAssembly?.Identity.Name,
            symbol.ContainingAssembly?.Identity.Version?.ToString(),
            containingSymbolId);
    }

    private static string CreateSymbolId(ISymbol symbol)
    {
        return symbol switch
        {
            IAssemblySymbol assembly => $"csharp assembly {Escape(AssemblyKey(assembly))}",
            INamespaceSymbol namespaceSymbol => $"csharp namespace {Escape(GetNamespaceName(namespaceSymbol))}",
            INamedTypeSymbol type => CreateNamedTypeId(type),
            IMethodSymbol method => CreateMethodId(method.ReducedFrom ?? method),
            IPropertySymbol property => CreatePropertyId(property),
            IFieldSymbol field => CreateFieldId(field),
            IEventSymbol eventSymbol => CreateEventId(eventSymbol),
            IParameterSymbol parameter => CreateParameterId(parameter),
            ILocalSymbol local => CreateLocalId(local),
            ITypeParameterSymbol typeParameter => CreateTypeParameterId(typeParameter),
            _ => CreateFallbackId(symbol)
        };
    }

    private static string CreateNamedTypeId(INamedTypeSymbol type)
    {
        var definition = type.OriginalDefinition;
        return $"csharp type {Escape(AssemblyKey(definition.ContainingAssembly))} {Escape(definition.ToDisplayString(DisplayFormat))}";
    }

    private static string CreateMethodId(IMethodSymbol method)
    {
        var containingTypeId = method.ContainingType is null ? string.Empty : CreateNamedTypeId(method.ContainingType);
        var parameterTypes = string.Join(",", method.Parameters.Select(parameter => TypeKey(parameter.Type)));
        var returnType = method.ReturnsVoid ? "void" : TypeKey(method.ReturnType);
        return $"csharp method {Escape(containingTypeId)} {Escape(method.MetadataName)}({Escape(parameterTypes)})->{Escape(returnType)}";
    }

    private static string CreatePropertyId(IPropertySymbol property)
    {
        var containingTypeId = property.ContainingType is null ? string.Empty : CreateNamedTypeId(property.ContainingType);
        var parameterTypes = string.Join(",", property.Parameters.Select(parameter => TypeKey(parameter.Type)));
        return $"csharp property {Escape(containingTypeId)} {Escape(property.MetadataName)}({Escape(parameterTypes)}):{Escape(TypeKey(property.Type))}";
    }

    private static string CreateFieldId(IFieldSymbol field)
    {
        var containingTypeId = field.ContainingType is null ? string.Empty : CreateNamedTypeId(field.ContainingType);
        return $"csharp field {Escape(containingTypeId)} {Escape(field.MetadataName)}:{Escape(TypeKey(field.Type))}";
    }

    private static string CreateEventId(IEventSymbol eventSymbol)
    {
        var containingTypeId = eventSymbol.ContainingType is null ? string.Empty : CreateNamedTypeId(eventSymbol.ContainingType);
        return $"csharp event {Escape(containingTypeId)} {Escape(eventSymbol.MetadataName)}:{Escape(TypeKey(eventSymbol.Type))}";
    }

    private static string CreateParameterId(IParameterSymbol parameter)
    {
        var containing = parameter.ContainingSymbol is null ? string.Empty : CreateSymbolId(parameter.ContainingSymbol);
        return $"csharp parameter {Escape(containing)} {parameter.Ordinal}:{Escape(parameter.Name)}:{Escape(TypeKey(parameter.Type))}";
    }

    private static string CreateLocalId(ILocalSymbol local)
    {
        var containing = local.ContainingSymbol is null ? string.Empty : CreateSymbolId(local.ContainingSymbol);
        var location = local.Locations.FirstOrDefault(location => location.IsInSource);
        var sourceKey = location is null
            ? "metadata"
            : $"{Path.GetFileName(location.SourceTree?.FilePath ?? string.Empty)}:{location.GetLineSpan().StartLinePosition.Line + 1}:{location.GetLineSpan().StartLinePosition.Character + 1}";
        return $"csharp local {Escape(containing)} {Escape(local.Name)}:{Escape(TypeKey(local.Type))}@{Escape(sourceKey)}";
    }

    private static string CreateTypeParameterId(ITypeParameterSymbol typeParameter)
    {
        var containing = typeParameter.ContainingSymbol is null ? string.Empty : CreateSymbolId(typeParameter.ContainingSymbol);
        return $"csharp typeParameter {Escape(containing)} {typeParameter.Ordinal}:{Escape(typeParameter.Name)}";
    }

    private static string CreateFallbackId(ISymbol symbol)
    {
        return $"csharp {Escape(symbol.Kind.ToString())} {Escape(AssemblyKey(symbol.ContainingAssembly))} {Escape(symbol.ToDisplayString(DisplayFormat))}";
    }

    private static string? TryCreateContainingSymbolId(ISymbol symbol)
    {
        var containing = symbol switch
        {
            IParameterSymbol parameter => parameter.ContainingSymbol,
            ILocalSymbol local => local.ContainingSymbol,
            INamedTypeSymbol { ContainingType: not null } type => type.ContainingType,
            INamedTypeSymbol type => type.ContainingNamespace,
            ISymbol { ContainingType: not null } other => other.ContainingType,
            ISymbol { ContainingNamespace.IsGlobalNamespace: false } other => other.ContainingNamespace,
            _ => null
        };

        return containing is null ? null : CreateSymbolId(containing);
    }

    private static string TypeKey(ITypeSymbol type)
    {
        return type switch
        {
            IArrayTypeSymbol array => $"{TypeKey(array.ElementType)}[{new string(',', Math.Max(0, array.Rank - 1))}]",
            IPointerTypeSymbol pointer => $"{TypeKey(pointer.PointedAtType)}*",
            ITypeParameterSymbol typeParameter => $"typeParameter:{typeParameter.Ordinal}:{typeParameter.Name}",
            INamedTypeSymbol named when named.IsGenericType && !named.IsUnboundGenericType =>
                $"{AssemblyKey(named.OriginalDefinition.ContainingAssembly)}:{named.OriginalDefinition.ToDisplayString(DisplayFormat)}<{string.Join(",", named.TypeArguments.Select(TypeKey))}>",
            INamedTypeSymbol named => $"{AssemblyKey(named.OriginalDefinition.ContainingAssembly)}:{named.OriginalDefinition.ToDisplayString(DisplayFormat)}",
            _ => type.ToDisplayString(DisplayFormat)
        };
    }

    private static string AssemblyKey(IAssemblySymbol? assembly)
    {
        return assembly is null
            ? "unknown"
            : $"{assembly.Identity.Name}@{assembly.Identity.Version}";
    }

    private static string GetNamespaceName(INamespaceSymbol namespaceSymbol)
    {
        return namespaceSymbol.IsGlobalNamespace ? "<global>" : namespaceSymbol.ToDisplayString();
    }

    private static string Escape(string value)
    {
        return Uri.EscapeDataString(value);
    }
}
