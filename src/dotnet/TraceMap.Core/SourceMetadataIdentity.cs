using System.Collections.Immutable;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;

namespace TraceMap.Core;

public sealed record SourceMetadataIdentityCandidate(
    string SourceIdentity,
    string? MetadataIdentity,
    string MemberKind,
    string Language,
    EvidenceSpan Evidence,
    string? ProjectPath,
    string RelationshipProof,
    IReadOnlyList<int> OptionalParameterOrdinals,
    string SourceDeclarationIdentity,
    string? IncompleteReason = null);

internal static class SourceMetadataIdentityCollector
{
    public static void Collect(
        SyntaxNode root,
        SemanticModel model,
        string? projectPath,
        string filePath,
        string language,
        List<SourceMetadataIdentityCandidate> candidates)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in root.DescendantNodesAndSelf().Where(IsDeclarationNode))
        {
            var symbol = model.GetDeclaredSymbol(node);
            if (symbol is null)
            {
                if (node is Microsoft.CodeAnalysis.CSharp.Syntax.VariableDeclaratorSyntax
                    or BaseFieldDeclarationSyntax
                    or Microsoft.CodeAnalysis.VisualBasic.Syntax.VariableDeclaratorSyntax
                    or ModifiedIdentifierSyntax)
                    continue;
                AddIncomplete(node, "SourceDeclaredSymbolUnavailable");
                continue;
            }
            if (!IsSupportedDeclaration(symbol) || symbol.IsImplicitlyDeclared)
                continue;

            Add(symbol, node, "source-declaration");
            if (symbol is IPropertySymbol property)
            {
                if (property.GetMethod is not null)
                    Add(property.GetMethod, node, "roslyn-associated-property-accessor");
                if (property.SetMethod is not null)
                    Add(property.SetMethod, node, "roslyn-associated-property-accessor");
            }
            else if (symbol is IEventSymbol eventSymbol)
            {
                if (eventSymbol.AddMethod is not null)
                    Add(eventSymbol.AddMethod, node, "roslyn-associated-event-accessor");
                if (eventSymbol.RemoveMethod is not null)
                    Add(eventSymbol.RemoveMethod, node, "roslyn-associated-event-accessor");
                if (eventSymbol.RaiseMethod is not null)
                    Add(eventSymbol.RaiseMethod, node, "roslyn-associated-event-accessor");
            }
        }

        void Add(ISymbol symbol, SyntaxNode node, string relationshipProof)
        {
            var metadataIdentity = SourceMetadataIdentityProvider.TryCreate(symbol, out var memberKind, out var metadataIncompleteReason);
            var sourceIdentity = language == LanguageNames.VisualBasic
                ? VisualBasicSymbolIdentityProvider.TryCreate(symbol)
                : CSharpSymbolIdentityProvider.TryCreate(symbol);
            if (sourceIdentity is null)
            {
                var syntaxIdentity = SyntaxIdentity(node);
                AddCandidate(syntaxIdentity, metadataIdentity, memberKind, node, relationshipProof, [], syntaxIdentity, "SourceDeclarationIdentityUnavailable");
                return;
            }

            var optionalParameters = symbol switch
            {
                IMethodSymbol method => method.Parameters.Where(parameter => parameter.IsOptional).Select(parameter => parameter.Ordinal).ToArray(),
                IPropertySymbol property => property.Parameters.Where(parameter => parameter.IsOptional).Select(parameter => parameter.Ordinal).ToArray(),
                _ => []
            };
            var reconciliationIdentity = metadataIdentity is null
                ? sourceIdentity.SymbolId
                : $"source:{language}|{metadataIdentity}";
            AddCandidate(reconciliationIdentity, metadataIdentity, memberKind, node, relationshipProof, optionalParameters, sourceIdentity.SymbolId, metadataIncompleteReason);
        }

        void AddIncomplete(SyntaxNode node, string reason)
        {
            var syntaxIdentity = SyntaxIdentity(node);
            AddCandidate(syntaxIdentity, null, "declaration", node, "syntax-located-unresolved-declaration", [], syntaxIdentity, reason);
        }

        string SyntaxIdentity(SyntaxNode node)
        {
            var line = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            return $"{language}:unresolved:{ManagedMetadataExtractor.EncodeIdentityComponent(filePath)}:{line.ToString(CultureInfo.InvariantCulture)}:{node.RawKind.ToString(CultureInfo.InvariantCulture)}";
        }

        void AddCandidate(
            string sourceIdentity,
            string? metadataIdentity,
            string memberKind,
            SyntaxNode node,
            string relationshipProof,
            IReadOnlyList<int> optionalParameters,
            string sourceDeclarationIdentity,
            string? incompleteReason)
        {
            var lineSpan = node.GetLocation().GetLineSpan();
            var evidence = new EvidenceSpan(
                filePath,
                lineSpan.StartLinePosition.Line + 1,
                Math.Max(lineSpan.StartLinePosition.Line + 1, lineSpan.EndLinePosition.Line + 1),
                null,
                language == LanguageNames.VisualBasic ? nameof(VisualBasicSemanticExtractor) : nameof(CSharpSemanticExtractor),
                language == LanguageNames.VisualBasic ? ScannerVersions.VisualBasicSemanticExtractor : ScannerVersions.CSharpSemanticExtractor);
            var key = string.Join('|', sourceIdentity, metadataIdentity ?? incompleteReason ?? string.Empty, evidence.FilePath,
                evidence.StartLine.ToString(CultureInfo.InvariantCulture), relationshipProof);
            if (!seen.Add(key))
                return;
            candidates.Add(new SourceMetadataIdentityCandidate(
                sourceIdentity,
                metadataIdentity,
                memberKind,
                language,
                evidence,
                projectPath,
                relationshipProof,
                optionalParameters,
                sourceDeclarationIdentity,
                incompleteReason));
        }
    }

    private static bool IsDeclarationNode(SyntaxNode node) =>
        node is not AccessorDeclarationSyntax
        && node is not Microsoft.CodeAnalysis.VisualBasic.Syntax.AccessorBlockSyntax
        && node is (MemberDeclarationSyntax
        or BaseMethodDeclarationSyntax
        or Microsoft.CodeAnalysis.CSharp.Syntax.VariableDeclaratorSyntax
        or Microsoft.CodeAnalysis.VisualBasic.Syntax.VariableDeclaratorSyntax
        or Microsoft.CodeAnalysis.VisualBasic.Syntax.MethodBlockBaseSyntax
        or DeclarationStatementSyntax
        or ModifiedIdentifierSyntax);

    private static bool IsSupportedDeclaration(ISymbol? symbol) => symbol is
        INamedTypeSymbol
        or IMethodSymbol
        or IPropertySymbol
        or IFieldSymbol
        or IEventSymbol;
}

internal static class SourceMetadataIdentityProvider
{
    public static string? TryCreate(ISymbol symbol, out string memberKind, out string? incompleteReason)
    {
        memberKind = MemberKind(symbol);
        incompleteReason = null;
        try
        {
            if (symbol.ContainingAssembly is null || symbol.ContainingModule is null)
                return Incomplete("SourceAssemblyIdentityUnavailable", out incompleteReason);
            var targetFramework = TargetFramework(symbol.ContainingAssembly);
            if (targetFramework is null)
                return Incomplete("SourceTargetFrameworkIdentityUnavailable", out incompleteReason);
            var assemblyIdentity = AssemblyArtifactIdentity(symbol.ContainingAssembly, symbol.ContainingModule.Name, targetFramework);
            return symbol switch
            {
                INamedTypeSymbol type => TypeIdentity(assemblyIdentity, type),
                IMethodSymbol method => MethodIdentity(assemblyIdentity, method, out incompleteReason),
                IPropertySymbol property => PropertyIdentity(assemblyIdentity, property, out incompleteReason),
                IFieldSymbol field => FieldIdentity(assemblyIdentity, field, out incompleteReason),
                IEventSymbol eventSymbol => EventIdentity(assemblyIdentity, eventSymbol, out incompleteReason),
                _ => Incomplete("SourceDeclarationKindUnsupported", out incompleteReason)
            };
        }
        catch (NotSupportedException exception)
        {
            incompleteReason = exception.Message;
            return null;
        }
    }

    private static string? MethodIdentity(string assemblyIdentity, IMethodSymbol method, out string? incompleteReason)
    {
        incompleteReason = null;
        if (HasCustomModifiers(method.ReturnTypeCustomModifiers, method.RefCustomModifiers)
            || method.Parameters.Any(parameter => HasCustomModifiers(parameter.CustomModifiers, parameter.RefCustomModifiers)))
            return Incomplete("SourceCustomModifierIdentityUnsupported", out incompleteReason);
        var containing = TypeIdentity(assemblyIdentity, method.ContainingType);
        var returnType = method.ReturnsVoid ? FormatSpecialType(SpecialType.System_Void) : FormatType(method.ReturnType);
        if (method.RefKind != RefKind.None)
            returnType += "&";
        var parameters = method.Parameters.Select(parameter => FormatType(parameter.Type) + (parameter.RefKind == RefKind.None ? string.Empty : "&"));
        var signature = MethodSignature(returnType, parameters, method.Arity, method.IsVararg ? "vararg" : "default", !method.IsStatic, false);
        var kind = method.MethodKind is MethodKind.Constructor or MethodKind.StaticConstructor ? "constructor" : "method";
        return $"{containing}|{kind}:{ManagedMetadataExtractor.EncodeIdentityComponent(method.MetadataName)}|{signature}";
    }

    private static string? PropertyIdentity(string assemblyIdentity, IPropertySymbol property, out string? incompleteReason)
    {
        incompleteReason = null;
        if (HasCustomModifiers(property.TypeCustomModifiers, property.RefCustomModifiers)
            || property.Parameters.Any(parameter => HasCustomModifiers(parameter.CustomModifiers, parameter.RefCustomModifiers)))
            return Incomplete("SourceCustomModifierIdentityUnsupported", out incompleteReason);
        var containing = TypeIdentity(assemblyIdentity, property.ContainingType);
        var propertyType = FormatType(property.Type) + (property.RefKind == RefKind.None ? string.Empty : "&");
        var parameters = property.Parameters.Select(parameter => FormatType(parameter.Type) + (parameter.RefKind == RefKind.None ? string.Empty : "&"));
        var signature = PropertySignature(propertyType, parameters, "default", !property.IsStatic);
        return $"{containing}|property:{ManagedMetadataExtractor.EncodeIdentityComponent(property.MetadataName)}|{signature}";
    }

    private static string? FieldIdentity(string assemblyIdentity, IFieldSymbol field, out string? incompleteReason)
    {
        incompleteReason = null;
        if (!field.CustomModifiers.IsDefaultOrEmpty)
            return Incomplete("SourceCustomModifierIdentityUnsupported", out incompleteReason);
        return $"{TypeIdentity(assemblyIdentity, field.ContainingType)}|field:{ManagedMetadataExtractor.EncodeIdentityComponent(field.MetadataName)}|type:{FormatType(field.Type)}";
    }

    private static string? EventIdentity(string assemblyIdentity, IEventSymbol eventSymbol, out string? incompleteReason)
    {
        incompleteReason = null;
        return $"{TypeIdentity(assemblyIdentity, eventSymbol.ContainingType)}|event:{ManagedMetadataExtractor.EncodeIdentityComponent(eventSymbol.MetadataName)}|type:{FormatType(eventSymbol.Type)}";
    }

    private static string TypeIdentity(string assemblyIdentity, INamedTypeSymbol type)
    {
        var definition = type.OriginalDefinition;
        var names = new Stack<string>();
        for (INamedTypeSymbol? current = definition; current is not null; current = current.ContainingType)
            names.Push(current.MetadataName);
        var ns = definition.ContainingNamespace is { IsGlobalNamespace: false } containingNamespace
            ? containingNamespace.ToDisplayString()
            : string.Empty;
        var metadataArity = ContainingTypeArity(definition);
        return $"{assemblyIdentity}|type:namespace:{ManagedMetadataExtractor.EncodeIdentityComponent(ns)}|names:{string.Concat(names.Select(ManagedMetadataExtractor.EncodeIdentityComponent))}|arity:{metadataArity.ToString(CultureInfo.InvariantCulture)}";
    }

    private static string FormatType(ITypeSymbol type)
    {
        type = type.WithNullableAnnotation(NullableAnnotation.None);
        return type switch
        {
            IDynamicTypeSymbol => FormatSpecialType(SpecialType.System_Object),
            IArrayTypeSymbol array => FormatType(array.ElementType) + (array.IsSZArray
                ? "[]"
                : ManagedMetadataExtractor.FormatArrayShape(array.Rank, [], [])),
            IPointerTypeSymbol pointer => FormatType(pointer.PointedAtType) + "*",
            IFunctionPointerTypeSymbol => throw new NotSupportedException("SourceFunctionPointerIdentityUnsupported"),
            ITypeParameterSymbol parameter => (parameter.TypeParameterKind == TypeParameterKind.Method ? "!!" : "!") + MetadataParameterOrdinal(parameter).ToString(CultureInfo.InvariantCulture),
            IErrorTypeSymbol => throw new NotSupportedException("SourceErrorTypeIdentityUnavailable"),
            _ when type.SpecialType != SpecialType.None => FormatSpecialType(type.SpecialType),
            INamedTypeSymbol named => FormatNamedType(named.IsTupleType ? named.TupleUnderlyingType! : named),
            _ => throw new NotSupportedException("SourceTypeIdentityUnsupported")
        };
    }

    private static int ContainingTypeArity(INamedTypeSymbol type)
    {
        var arity = 0;
        for (INamedTypeSymbol? current = type.OriginalDefinition; current is not null; current = current.ContainingType)
            arity += current.Arity;
        return arity;
    }

    private static int MetadataParameterOrdinal(ITypeParameterSymbol parameter)
    {
        if (parameter.TypeParameterKind == TypeParameterKind.Method)
            return parameter.Ordinal;
        var ordinal = parameter.Ordinal;
        for (var current = parameter.DeclaringType?.ContainingType; current is not null; current = current.ContainingType)
            ordinal += current.Arity;
        return ordinal;
    }

    private static string FormatNamedType(INamedTypeSymbol named)
    {
        var definition = named.OriginalDefinition;
        var names = new Stack<string>();
        for (INamedTypeSymbol? current = definition; current is not null; current = current.ContainingType)
            names.Push(current.MetadataName);
        var ns = definition.ContainingNamespace is { IsGlobalNamespace: false } containingNamespace
            ? containingNamespace.ToDisplayString()
            : string.Empty;
        if (definition.ContainingAssembly is null)
            throw new NotSupportedException("SourceTypeAssemblyScopeUnavailable");
        var result = "scope(" + AssemblyReferenceIdentity(definition.ContainingAssembly) + ")type(namespace:" + ManagedMetadataExtractor.EncodeIdentityComponent(ns)
            + "|names:" + string.Concat(names.Select(ManagedMetadataExtractor.EncodeIdentityComponent)) + ")";
        return named.IsGenericType
            ? result + "<" + string.Join(",", named.TypeArguments.Select(FormatType)) + ">"
            : result;
    }

    private static string FormatSpecialType(SpecialType specialType)
    {
        var metadataName = specialType switch
        {
            SpecialType.System_Void => ("System", "Void"),
            SpecialType.System_Object => ("System", "Object"),
            SpecialType.System_Boolean => ("System", "Boolean"),
            SpecialType.System_Char => ("System", "Char"),
            SpecialType.System_SByte => ("System", "SByte"),
            SpecialType.System_Byte => ("System", "Byte"),
            SpecialType.System_Int16 => ("System", "Int16"),
            SpecialType.System_UInt16 => ("System", "UInt16"),
            SpecialType.System_Int32 => ("System", "Int32"),
            SpecialType.System_UInt32 => ("System", "UInt32"),
            SpecialType.System_Int64 => ("System", "Int64"),
            SpecialType.System_UInt64 => ("System", "UInt64"),
            SpecialType.System_Decimal => ("System", "Decimal"),
            SpecialType.System_Single => ("System", "Single"),
            SpecialType.System_Double => ("System", "Double"),
            SpecialType.System_String => ("System", "String"),
            SpecialType.System_IntPtr => ("System", "IntPtr"),
            SpecialType.System_UIntPtr => ("System", "UIntPtr"),
            _ => throw new NotSupportedException("SourceSpecialTypeIdentityUnsupported")
        };
        return $"type(namespace:{ManagedMetadataExtractor.EncodeIdentityComponent(metadataName.Item1)}|names:{ManagedMetadataExtractor.EncodeIdentityComponent(metadataName.Item2)})";
    }

    private static string AssemblyArtifactIdentity(IAssemblySymbol assembly, string moduleName, string targetFramework)
    {
        var identity = assembly.Identity;
        var token = identity.PublicKeyToken.IsDefaultOrEmpty ? "null" : Convert.ToHexString(identity.PublicKeyToken.ToArray()).ToLowerInvariant();
        var culture = string.IsNullOrWhiteSpace(identity.CultureName) ? "neutral" : identity.CultureName;
        return $"assembly:name:{ManagedMetadataExtractor.EncodeIdentityComponent(identity.Name)}|version:{ManagedMetadataExtractor.EncodeIdentityComponent(identity.Version.ToString())}|culture:{ManagedMetadataExtractor.EncodeIdentityComponent(culture)}|publicKeyToken:{ManagedMetadataExtractor.EncodeIdentityComponent(token)}|module:{ManagedMetadataExtractor.EncodeIdentityComponent(moduleName)}|targetFramework:{ManagedMetadataExtractor.EncodeIdentityComponent(targetFramework)}";
    }

    private static string AssemblyReferenceIdentity(IAssemblySymbol assembly)
    {
        var identity = assembly.Identity;
        var token = identity.PublicKeyToken.IsDefaultOrEmpty ? "null" : Convert.ToHexString(identity.PublicKeyToken.ToArray()).ToLowerInvariant();
        var culture = string.IsNullOrWhiteSpace(identity.CultureName) ? "neutral" : identity.CultureName;
        return $"assembly:name:{ManagedMetadataExtractor.EncodeIdentityComponent(identity.Name)}|version:{ManagedMetadataExtractor.EncodeIdentityComponent(identity.Version.ToString())}|culture:{ManagedMetadataExtractor.EncodeIdentityComponent(culture)}|publicKeyToken:{ManagedMetadataExtractor.EncodeIdentityComponent(token)}";
    }

    private static string? TargetFramework(IAssemblySymbol assembly)
    {
        var attribute = assembly.GetAttributes().FirstOrDefault(item =>
            item.AttributeClass?.ToDisplayString() == "System.Runtime.Versioning.TargetFrameworkAttribute");
        return attribute?.ConstructorArguments.Length == 1 && attribute.ConstructorArguments[0].Value is string value
            ? value
            : null;
    }

    private static bool HasCustomModifiers(ImmutableArray<CustomModifier> first, ImmutableArray<CustomModifier> second) =>
        !first.IsDefaultOrEmpty || !second.IsDefaultOrEmpty;

    private static string MethodSignature(string returnType, IEnumerable<string> parameters, int genericArity, string callingConvention, bool hasThis, bool explicitThis) =>
        $"arity:{genericArity.ToString(CultureInfo.InvariantCulture)}|call:{callingConvention}|hasThis:{hasThis.ToString().ToLowerInvariant()}|explicitThis:{explicitThis.ToString().ToLowerInvariant()}|({string.Join(",", parameters)})->{returnType}";

    private static string PropertySignature(string propertyType, IEnumerable<string> parameters, string callingConvention, bool hasThis) =>
        $"call:{callingConvention}|hasThis:{hasThis.ToString().ToLowerInvariant()}|({string.Join(",", parameters)})->{propertyType}";

    private static string MemberKind(ISymbol symbol) => symbol switch
    {
        INamedTypeSymbol => "type",
        IMethodSymbol { MethodKind: MethodKind.Constructor or MethodKind.StaticConstructor } => "constructor",
        IMethodSymbol { MethodKind: MethodKind.PropertyGet or MethodKind.PropertySet } => "property-accessor",
        IMethodSymbol { MethodKind: MethodKind.EventAdd or MethodKind.EventRemove or MethodKind.EventRaise } => "event-accessor",
        IMethodSymbol => "method",
        IPropertySymbol => "property",
        IFieldSymbol => "field",
        IEventSymbol => "event",
        _ => "unsupported"
    };

    private static string? Incomplete(string reason, out string? incompleteReason)
    {
        incompleteReason = reason;
        return null;
    }
}
