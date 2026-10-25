using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace TraceMap.Core;

public sealed record SqlServerCallParameter(string? Name, string? SqlType, int? Size, string? Direction, int Line, string SizeUnits = "unavailable", int? EndLine = null, int? DirectionLine = null, int? DirectionEndLine = null);
public sealed record SqlServerCallState(string Version, string Coverage, string? CommandType, string? CommandTextHash,
    IReadOnlyList<SqlServerCallParameter> Parameters, string? Gap);

/// <summary>Independent fail-closed parameter state; existing aggregate witnesses remain unchanged.</summary>
internal static class SqlServerCommandParameters
{
    internal static void Add(SortedDictionary<string, string> properties, IInvocationOperation call)
    {
        var state = Extract(call);
        properties["sqlServerParameterState"] = SqlServerSchemaEvidence.Json(state);
    }

    private static SqlServerCallState Extract(IInvocationOperation call)
    {
        SqlServerCallState Unknown(string gap) => new("sqlserver-call/1", "unknown", null, null, [], gap);
        if (Local(call.Instance) is not { } receiver) return Unknown("fresh-local-required");
        // Arguments run before execution, although their source spans follow the
        // invocation start. Admit only inert reads; helpers, getters, constructors
        // and user conversions can mutate the command or its parameter objects.
        if (call.Arguments.Any(a => !InertArgument(a.Value))) return Unknown("unsupported-execution-argument");
        IOperation root = call;
        while (root.Parent is not null) root = root.Parent;
        var all = Walk(root).Take(20001).ToArray();
        if (all.Length > 20000) return Unknown("operation-limit");
        // Entire method is checked: a later alias/closure can mutate before execution via callbacks.
        if (all.Any(o => o is IConditionalOperation or ILoopOperation or IBranchOperation or ITryOperation
            or IAnonymousFunctionOperation or ILocalFunctionOperation or IInvalidOperation
            or ICompoundAssignmentOperation or IIncrementOrDecrementOperation or IDeconstructionAssignmentOperation
            or IDynamicInvocationOperation or IDynamicObjectCreationOperation or IDynamicMemberReferenceOperation
            or IConditionalAccessOperation or ICoalesceOperation
            or IBinaryOperation { OperatorKind: BinaryOperatorKind.ConditionalAnd or BinaryOperatorKind.ConditionalOr })) return Unknown("unsupported-control-flow");
        var parameters = new List<SqlServerCallParameter>();
        var locals = new Dictionary<ILocalSymbol, int>(SymbolEqualityComparer.Default);
        string? commandType = null, textHash = null;
        var fresh = false;
        foreach (var op in all.Where(o => o.Syntax.SpanStart < call.Syntax.SpanStart))
        {
            if (op is IVariableDeclaratorOperation variable)
            {
                var value = variable.Initializer?.Value;
                if (value is null && variable.Parent is IVariableDeclarationOperation declaration) value = declaration.Initializer?.Value;
                value = Unwrap(value);
                if (SymbolEqualityComparer.Default.Equals(variable.Symbol, receiver))
                {
                    if (value is not IObjectCreationOperation creation || creation.Type?.ToDisplayString() is not ("System.Data.SqlClient.SqlCommand" or "Microsoft.Data.SqlClient.SqlCommand")
                        || !DatabaseOperationApi.Trusted(creation.Type.ContainingAssembly)) return Unknown("fresh-sqlcommand-required");
                    if (creation.Initializer is not null) return Unknown("implicit-receiver-mutation");
                    fresh = true;
                    var text = creation.Arguments.FirstOrDefault(a => a.Parameter?.Name is "cmdText" or "commandText");
                    textHash = text?.Value.ConstantValue is { HasValue: true, Value: string s } ? FactFactory.Hash(s, 64) : null;
                }
                else if (value is not IInvocationOperation { TargetMethod.Name: "Add" } && value is not null
                    && Walk(value).OfType<ILocalReferenceOperation>().Any(r => SymbolEqualityComparer.Default.Equals(r.Local, receiver) || locals.ContainsKey(r.Local)))
                    return Unknown("nested-alias-or-escape");
                else if (Local(value) is { } alias && (SymbolEqualityComparer.Default.Equals(alias, receiver) || locals.ContainsKey(alias)))
                    return Unknown("alias");
            }
            if (op is IObjectCreationOperation constructed && constructed.Arguments.Any(a => Walk(a.Value).OfType<ILocalReferenceOperation>()
                .Any(r => SymbolEqualityComparer.Default.Equals(r.Local, receiver) || locals.ContainsKey(r.Local)))) return Unknown("constructor-escape");
            if (op is IInvocationOperation invocation)
            {
                if (invocation.TargetMethod.Name == "Add" && invocation.Instance is IPropertyReferenceOperation { Property.Name: "Parameters" } collection
                    && SymbolEqualityComparer.Default.Equals(Local(collection.Instance), receiver)
                    && invocation.TargetMethod.ContainingType.ToDisplayString() is "System.Data.SqlClient.SqlParameterCollection" or "Microsoft.Data.SqlClient.SqlParameterCollection"
                    && DatabaseOperationApi.Trusted(invocation.TargetMethod.ContainingAssembly))
                {
                    if (!fresh || parameters.Count >= 128) return Unknown("parameter-limit-or-no-command");
                    var name = invocation.Arguments.FirstOrDefault(a => a.Parameter?.Name == "parameterName")?.Value;
                    var type = invocation.Arguments.FirstOrDefault(a => a.Parameter?.Type.Name == "SqlDbType")?.Value;
                    var size = invocation.Arguments.FirstOrDefault(a => a.Parameter?.Name == "size")?.Value;
                    var sqlType = Unwrap(type) is IFieldReferenceOperation field && DatabaseOperationApi.Trusted(field.Field.ContainingAssembly) ? field.Field.Name : null;
                    var p = new SqlServerCallParameter(name?.ConstantValue is { HasValue: true, Value: string n } ? n : null,
                        sqlType, size?.ConstantValue is { HasValue: true, Value: int i } ? i : null, "Input", invocation.Syntax.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                        sqlType == "NVarChar" ? "utf16-code-units" : sqlType == "VarChar" ? "provider-ansi-size-policy-unknown" : "not-applicable", invocation.Syntax.GetLocation().GetLineSpan().EndLinePosition.Line + 1);
                    parameters.Add(p);
                    for (var parent = invocation.Parent; parent is not null; parent = parent.Parent)
                    {
                        if (parent is IVariableDeclaratorOperation v) { locals[v.Symbol] = parameters.Count - 1; break; }
                        if (parent is IVariableDeclarationOperation d)
                            foreach (var v2 in d.Declarators) locals[v2.Symbol] = parameters.Count - 1;
                    }
                    if (invocation.Arguments.Length is not (2 or 3) || p.Name is null || p.SqlType is null) return Unknown("unsupported-add-overload-or-dynamic-parameter");
                }
                else if (invocation.TargetMethod.Name == "Add" && invocation.Instance is IPropertyReferenceOperation { Property.Name: "Parameters" } other
                    && Local(other.Instance) is { } otherCommand && !SymbolEqualityComparer.Default.Equals(otherCommand, receiver)
                    && invocation.TargetMethod.ContainingType.ToDisplayString() is "System.Data.SqlClient.SqlParameterCollection" or "Microsoft.Data.SqlClient.SqlParameterCollection"
                    && DatabaseOperationApi.Trusted(invocation.TargetMethod.ContainingAssembly))
                {
                    if (invocation.Arguments.Any(a => Walk(a.Value).OfType<ILocalReferenceOperation>().Any(r => SymbolEqualityComparer.Default.Equals(r.Local, receiver) || locals.ContainsKey(r.Local))))
                        return Unknown("cross-command-parameter-alias");
                }
                else if (!DatabaseOperationApi.TryClassify(invocation.TargetMethod, out _, out _)) return Unknown("helper-or-unsupported-mutation");
                else if (SymbolEqualityComparer.Default.Equals(Local(invocation.Instance), receiver)) return Unknown("prior-execution");
            }
            if (op is ISimpleAssignmentOperation assignment)
            {
                if (Local(assignment.Target) is { } changed && locals.ContainsKey(changed)) return Unknown("parameter-reassignment");
                if (SymbolEqualityComparer.Default.Equals(Local(assignment.Target), receiver)) return Unknown("command-reassignment");
                if (Local(assignment.Value) is { } escaped && (SymbolEqualityComparer.Default.Equals(escaped, receiver) || locals.ContainsKey(escaped))) return Unknown("alias-mutation");
                if (assignment.Target is not IPropertyReferenceOperation property)
                {
                    if (Walk(assignment.Value).OfType<ILocalReferenceOperation>().Any(r => SymbolEqualityComparer.Default.Equals(r.Local, receiver) || locals.ContainsKey(r.Local))) return Unknown("unsupported-assignment");
                    continue;
                }
                // Object initializers and VB With blocks need receiver resolution that
                // this bounded walker does not provide. Never keep stale constructor state.
                if (Unwrap(property.Instance) is IInstanceReferenceOperation { ReferenceKind: InstanceReferenceKind.ImplicitReceiver })
                    return Unknown("implicit-receiver-mutation");
                if (SymbolEqualityComparer.Default.Equals(Local(property.Instance), receiver))
                {
                    if (!DatabaseOperationApi.Trusted(property.Property.ContainingAssembly)) return Unknown("untrusted-property");
                    if (property.Property.Name == "CommandType") commandType = assignment.Value.ConstantValue.Value switch { 4 => "StoredProcedure", 1 => "Text", _ => null };
                    else if (property.Property.Name == "CommandText") textHash = assignment.Value.ConstantValue is { HasValue: true, Value: string s } ? FactFactory.Hash(s, 64) : null;
                    else return Unknown("unsupported-command-assignment");
                }
                else if (Local(property.Instance) is { } pl && locals.TryGetValue(pl, out var index))
                {
                    if (!DatabaseOperationApi.Trusted(property.Property.ContainingAssembly)) return Unknown("untrusted-parameter-property");
                    var p = parameters[index];
                    if (property.Property.Name == "Direction") parameters[index] = p with { Direction = assignment.Value.ConstantValue.Value switch { 1 => "Input", 2 => "Output", 3 => "InputOutput", 6 => "ReturnValue", _ => null },
                        DirectionLine = assignment.Syntax.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                        DirectionEndLine = assignment.Syntax.GetLocation().GetLineSpan().EndLinePosition.Line + 1 };
                    else return Unknown("unsupported-parameter-mutation");
                }
                else if (Walk(assignment.Target).Concat(Walk(assignment.Value)).OfType<ILocalReferenceOperation>().Any(r => SymbolEqualityComparer.Default.Equals(r.Local, receiver) || locals.ContainsKey(r.Local))) return Unknown("indirect-parameter-mutation");
            }
        }
        return fresh ? new("sqlserver-call/1", "bounded", commandType, textHash, parameters, null) : Unknown("fresh-local-required");
    }
    private static IOperation? Unwrap(IOperation? o) => o is IConversionOperation c ? Unwrap(c.Operand) : o;
    private static bool InertArgument(IOperation operation) => operation switch
    {
        IConversionOperation { OperatorMethod: null } conversion => InertArgument(conversion.Operand),
        ILiteralOperation or IDefaultValueOperation or ILocalReferenceOperation or IParameterReferenceOperation => true,
        _ => false
    };
    private static ILocalSymbol? Local(IOperation? o) => Unwrap(o) is ILocalReferenceOperation l ? l.Local : null;
    private static IEnumerable<IOperation> Walk(IOperation o)
    {
        var stack = new Stack<IOperation>();
        stack.Push(o);
        while (stack.TryPop(out var current))
        {
            yield return current;
            foreach (var child in current.ChildOperations.Reverse()) stack.Push(child);
        }
    }
}
