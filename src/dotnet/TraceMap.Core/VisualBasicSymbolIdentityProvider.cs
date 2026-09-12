using Microsoft.CodeAnalysis;

namespace TraceMap.Core;

// Visual Basic sibling of CSharpSymbolIdentityProvider: the same canonical
// .NET symbol normalization, tagged with the Visual Basic adapter language so
// VB-scan symbol rows are distinguishable from C# rows while their identity
// shape stays directly comparable. All symbols resolved inside a Visual Basic
// scan are tagged "visualbasic", mirroring how the C# extractor tags every
// symbol it resolves "csharp".
public static class VisualBasicSymbolIdentityProvider
{
    private static readonly SymbolDisplayFormat DisplayFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        memberOptions:
            SymbolDisplayMemberOptions.IncludeContainingType
            | SymbolDisplayMemberOptions.IncludeParameters
            | SymbolDisplayMemberOptions.IncludeType
            | SymbolDisplayMemberOptions.IncludeExplicitInterface,
        parameterOptions:
            SymbolDisplayParameterOptions.IncludeType
            | SymbolDisplayParameterOptions.IncludeName
            | SymbolDisplayParameterOptions.IncludeParamsRefOut,
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
            "visualbasic",
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
            IAssemblySymbol assembly => $"visualbasic assembly {Escape(AssemblyKey(assembly))}",
            INamespaceSymbol namespaceSymbol => $"visualbasic namespace {Escape(GetNamespaceName(namespaceSymbol))}",
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
        return $"visualbasic type {Escape(AssemblyKey(definition.ContainingAssembly))} {Escape(definition.ToDisplayString(DisplayFormat))}";
    }

    private static string CreateMethodId(IMethodSymbol method)
    {
        var definition = (method.ReducedFrom ?? method).OriginalDefinition;
        var containingTypeId = definition.ContainingType is null ? string.Empty : CreateNamedTypeId(definition.ContainingType);
        var parameterTypes = string.Join(",", definition.Parameters.Select(parameter => TypeKey(parameter.Type)));
        var returnType = definition.ReturnsVoid ? "void" : TypeKey(definition.ReturnType);
        return $"visualbasic method {Escape(containingTypeId)} {Escape(definition.MetadataName)}({Escape(parameterTypes)})->{Escape(returnType)}";
    }

    private static string CreatePropertyId(IPropertySymbol property)
    {
        var definition = property.OriginalDefinition;
        var containingTypeId = definition.ContainingType is null ? string.Empty : CreateNamedTypeId(definition.ContainingType);
        var parameterTypes = string.Join(",", definition.Parameters.Select(parameter => TypeKey(parameter.Type)));
        return $"visualbasic property {Escape(containingTypeId)} {Escape(definition.MetadataName)}({Escape(parameterTypes)}):{Escape(TypeKey(definition.Type))}";
    }

    private static string CreateFieldId(IFieldSymbol field)
    {
        var containingTypeId = field.ContainingType is null ? string.Empty : CreateNamedTypeId(field.ContainingType);
        return $"visualbasic field {Escape(containingTypeId)} {Escape(field.MetadataName)}:{Escape(TypeKey(field.Type))}";
    }

    private static string CreateEventId(IEventSymbol eventSymbol)
    {
        var containingTypeId = eventSymbol.ContainingType is null ? string.Empty : CreateNamedTypeId(eventSymbol.ContainingType);
        return $"visualbasic event {Escape(containingTypeId)} {Escape(eventSymbol.MetadataName)}:{Escape(TypeKey(eventSymbol.Type))}";
    }

    private static string CreateParameterId(IParameterSymbol parameter)
    {
        var containing = parameter.ContainingSymbol is null ? string.Empty : CreateSymbolId(parameter.ContainingSymbol);
        return $"visualbasic parameter {Escape(containing)} {parameter.Ordinal}:{Escape(parameter.Name)}:{Escape(TypeKey(parameter.Type))}";
    }

    private static string CreateLocalId(ILocalSymbol local)
    {
        var containing = local.ContainingSymbol is null ? string.Empty : CreateSymbolId(local.ContainingSymbol);
        var location = local.Locations.FirstOrDefault(location => location.IsInSource);
        var sourceKey = location is null
            ? "metadata"
            : $"source-{FactFactory.Hash(Path.GetFileName(location.SourceTree?.FilePath ?? string.Empty), 20)}:{location.GetLineSpan().StartLinePosition.Line + 1}:{location.GetLineSpan().StartLinePosition.Character + 1}";
        return $"visualbasic local {Escape(containing)} {Escape(local.Name)}:{Escape(TypeKey(local.Type))}@{Escape(sourceKey)}";
    }

    private static string CreateTypeParameterId(ITypeParameterSymbol typeParameter)
    {
        var containing = typeParameter.ContainingSymbol is null ? string.Empty : CreateSymbolId(typeParameter.ContainingSymbol);
        return $"visualbasic typeParameter {Escape(containing)} {typeParameter.Ordinal}:{Escape(typeParameter.Name)}";
    }

    private static string CreateFallbackId(ISymbol symbol)
    {
        return $"visualbasic {Escape(symbol.Kind.ToString())} {Escape(AssemblyKey(symbol.ContainingAssembly))} {Escape(symbol.ToDisplayString(DisplayFormat))}";
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
