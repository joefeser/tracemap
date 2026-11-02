using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
namespace TraceMap.Core;

internal static partial class LinqPipelineEvidenceExtractor
{
    internal sealed record JsonFilter(string KeySha256, string ComparedLiteralSha256);
    private static bool JsonAssembly(IAssemblySymbol assembly) => assembly.Identity.Name == "Newtonsoft.Json"
        && assembly.Identity.Version == new System.Version(13, 0, 0, 0) && string.IsNullOrEmpty(assembly.Identity.CultureName)
        && Convert.ToHexStringLower(assembly.Identity.PublicKeyToken.ToArray()) == "30ad4fe6b2a6aeed";
    private static bool JsonType(ITypeSymbol? type, string name) => type is INamedTypeSymbol named
        && named.MetadataName == name && named.ContainingNamespace.ToDisplayString() == "Newtonsoft.Json.Linq" && JsonAssembly(named.ContainingAssembly);
    private static bool JsonCandidate(IInvocationOperation call)
    {
        if (call.TargetMethod.Name != "Join" || call.TargetMethod.ContainingType.SpecialType != SpecialType.System_String
            || !Core(call.TargetMethod.ContainingType) || call.Arguments.Length != 2) return false;
        var source = ResolveJsonLocal(call.Arguments[1].Value, call, validate: false);
        return source is IInvocationOperation selector && EnumerableType(Definition(selector).ContainingType)
            && Definition(selector).Name == "Select" && selector.Arguments.Any(argument => argument.Value.Type is INamedTypeSymbol function
                && function.MetadataName == "Func`2" && Core(function) && JsonType(function.TypeArguments[0], "JToken"));
    }
    private static IOperation? ResolveJsonLocal(IOperation? value, IInvocationOperation terminal, bool validate)
    {
        value = Strip(value);
        if (value is not ILocalReferenceOperation reference) return value;
        IOperation method = terminal;
        while (method.Parent is not null) method = method.Parent;
        var operations = Walk(method).Take(4097).ToArray();
        if (operations.Length > 4096) { if (validate) throw new Rejection("json-alias-operation-limit"); return null; }
        var declarations = operations.OfType<IVariableDeclaratorOperation>().Where(declaration => SymbolEqualityComparer.Default.Equals(declaration.Symbol, reference.Local)).ToArray();
        if (declarations.Length != 1) return null;
        var declarator = declarations[0];
        var initial = declarator.Initializer?.Value ?? (declarator.Parent as IVariableDeclarationOperation)?.Initializer?.Value;
        if (validate && (declarator.Syntax.SpanStart >= terminal.Syntax.SpanStart
            || operations.OfType<ISimpleAssignmentOperation>().Any(assignment => Strip(assignment.Target) is ILocalReferenceOperation target && SymbolEqualityComparer.Default.Equals(target.Local, reference.Local))
            || operations.OfType<IArgumentOperation>().Any(argument => argument.Parameter?.RefKind is not (null or RefKind.None)
                && Referenced(argument.Value) is { } symbol && SymbolEqualityComparer.Default.Equals(symbol, reference.Local))))
            throw new Rejection("json-pipeline-alias-mutated-or-ref-escape");
        return Strip(initial);
    }
    private static Summary JsonPipeline(IInvocationOperation terminal)
    {
        var method = Definition(terminal);
        if (!method.IsStatic || method.Arity != 0 || method.Parameters.Length != 2
            || method.Parameters[0].Type.SpecialType != SpecialType.System_String
            || method.Parameters[1].Type is not INamedTypeSymbol sequence || sequence.MetadataName != "IEnumerable`1"
            || sequence.ContainingNamespace.ToDisplayString() != "System.Collections.Generic" || !Core(sequence)
            || sequence.TypeArguments[0].SpecialType != SpecialType.System_String
            || Strip(terminal.Arguments[0].Value)?.ConstantValue is not { HasValue: true, Value: string separator } || separator.Length > 32)
            throw new Rejection("json-string-join-overload-or-separator");
        var identities = new List<string> { Identity(method) };
        var selected = ResolveJsonLocal(terminal.Arguments[1].Value, terminal, validate: true);
        var select = Require(selected, "Select", 2, true, identities);
        var where = Require(Source(select), "Where", 1, true, identities);
        var (input, predicate) = Lambda(where);
        if (!JsonType(input.Type, "JToken") || predicate is not IBinaryOperation { OperatorKind: BinaryOperatorKind.Equals } equality
            || equality.OperatorMethod is { } operatorMethod && !(Core(operatorMethod.ContainingType)
                && operatorMethod.ContainingType.SpecialType == SpecialType.System_String && operatorMethod.Name == "op_Equality"))
            throw new Rejection("json-string-equality-predicate");
        var left = Strip(equality.LeftOperand)!; var right = Strip(equality.RightOperand)!;
        if (left.ConstantValue is { HasValue: true, Value: string }) (left, right) = (right, left);
        if (right.ConstantValue is not { HasValue: true, Value: string literal } || literal.Length > 256)
            throw new Rejection("json-constant-filter-value-required");
        var filter = JsonString(left, input, identities);
        var (element, projection) = Lambda(select);
        if (!JsonType(element.Type, "JToken")) throw new Rejection("json-selector-parameter");
        var column = JsonString(projection, element, identities);
        var source = Source(where);
        if (source is not (ILocalReferenceOperation or IParameterReferenceOperation or IFieldReferenceOperation) || Referenced(source) is not { } sourceSymbol)
            throw new Rejection("json-sequence-symbol-required");
        return new Summary("jtoken-filter-project-join", "jtoken-sequence-symbol-reference;json-origin-unmodeled", HashSymbol(sourceSymbol), null,
            ["Where", "Select", "String.Join"],
            [new Mapping(FactFactory.Hash("joined-string-element", 64), "jtoken-name-indexer", FactFactory.Hash(column, 64), ["string-value", "join"])], [],
            identities.Distinct(StringComparer.Ordinal).ToArray())
            { JsonFilter = new(FactFactory.Hash(filter, 64), FactFactory.Hash(literal, 64)) };
    }
    private static string JsonString(IOperation value, IParameterSymbol parameter, List<string> identities)
    {
        IOperation? token;
        if (value is IConversionOperation { Type.SpecialType: SpecialType.System_String, OperatorMethod: { } conversion } cast
            && JsonType(conversion.ContainingType, "JToken") && conversion.MetadataName == "op_Explicit"
            && conversion.Parameters.Length == 1 && JsonType(conversion.Parameters[0].Type, "JToken")
            && conversion.ReturnType.SpecialType == SpecialType.System_String)
        { identities.Add(Identity(conversion)); token = Strip(cast.Operand); }
        else if (Strip(value) is IInvocationOperation call)
        {
            var method = Definition(call);
            if (method.Name != "Value" || method.ContainingType.ToDisplayString() != "Newtonsoft.Json.Linq.Extensions"
                || !JsonAssembly(method.ContainingAssembly) || !method.IsExtensionMethod || method.Arity != 1
                || call.TargetMethod.TypeArguments.Length != 1 || call.TargetMethod.TypeArguments[0].SpecialType != SpecialType.System_String
                || method.Parameters.Length != 1 || method.Parameters[0].Type is not INamedTypeSymbol sequence
                || sequence.MetadataName != "IEnumerable`1" || !Core(sequence) || !JsonType(sequence.TypeArguments[0], "JToken"))
                throw new Rejection("json-value-string-identity");
            identities.Add(Identity(method)); token = Source(call);
        }
        else throw new Rejection("json-string-value-required");
        if (token is not IPropertyReferenceOperation indexer || !JsonType(indexer.Property.ContainingType, "JToken")
            || indexer.Property.MetadataName != "Item" || indexer.Property.Parameters.Length != 1
            || indexer.Property.Parameters[0].Type.SpecialType != SpecialType.System_Object || indexer.Arguments.Length != 1
            || !Parameter(indexer.Instance, parameter)
            || Strip(indexer.Arguments[0].Value)?.ConstantValue is not { HasValue: true, Value: string key } || key.Length > 256)
            throw new Rejection("json-constant-token-key-required");
        return key;
    }
}
