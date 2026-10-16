using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace TraceMap.Core;

/// <summary>Bounded local straight-line witnesses, never cross-method/branch command state.</summary>
internal static class DatabaseCommandState
{
    internal static void Add(SortedDictionary<string,string> p, IInvocationOperation call)
    {
        var receiver = Symbol(call.Instance);
        if (receiver is null) return;
        var block = call.Parent;
        while (block is not null && block is not IBlockOperation) block = block.Parent;
        if (block is not IBlockOperation body) return;
        if (body.Operations.Length > 256) { p["commandStateCoverage"] = "local-work-limit"; return; }
        p["commandStateCoverage"] = "bounded-local-straight-line";
        var witnesses = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
        void Witness(string key, string value)
        {
            if (value == "unavailable") return;
            p[key] = string.Join(";", p.GetValueOrDefault(key, "").Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Append(value).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));
        }
        void Reset()
        {
            p["commandTextAvailability"] = "unavailable"; p.Remove("commandTextHash");
            p["commandTypeEvidence"] = "unavailable"; p["transactionEvidence"] = "separate-witness-required";
            p["connectionEvidence"] = "unavailable"; p["parameterEvidence"] = "separate-witness-required";
            p.Remove("parameterDirectionKinds"); p.Remove("parameterTypeKinds"); p.Remove("parameterTypeNames"); witnesses.Clear();
        }
        var escaped = false;
        var work = 0;
        foreach (var statement in body.Operations)
        {
            if (statement.Syntax.Span.Contains(call.Syntax.Span)) break;
            if (statement is not (IExpressionStatementOperation or IVariableDeclarationGroupOperation)) { Reset(); continue; }
            var operations = Walk(statement).Take(1025).ToArray();
            work += operations.Length;
            if (operations.Length > 1024 || work > 4096) { Reset(); p["commandStateCoverage"] = "local-work-limit"; return; }
            if (operations.Any(o => o is IVariableDeclaratorOperation variable && !SymbolEqualityComparer.Default.Equals(variable.Symbol,receiver)
                    && SymbolEqualityComparer.Default.Equals(Symbol(variable.Initializer?.Value),receiver)
                    || o is IVariableDeclarationOperation declaration && SymbolEqualityComparer.Default.Equals(Symbol(declaration.Initializer?.Value),receiver)
                    && declaration.Declarators.Any(d => !SymbolEqualityComparer.Default.Equals(d.Symbol,receiver))
                    || o is ISimpleAssignmentOperation assignment && !SymbolEqualityComparer.Default.Equals(Symbol(assignment.Target),receiver)
                    && SymbolEqualityComparer.Default.Equals(Symbol(assignment.Value),receiver))) escaped = true;
            if (escaped) { Reset(); continue; }
            bool ParameterMutation(IInvocationOperation invocation) => invocation.TargetMethod.Name is "Add" or "AddWithValue" or "AddRange"
                && DatabaseOperationApi.Trusted(invocation.TargetMethod.ContainingAssembly)
                && invocation.Instance is IPropertyReferenceOperation { Property.Name:"Parameters" } parameters
                && SymbolEqualityComparer.Default.Equals(Symbol(parameters.Instance),receiver);
            if (operations.Any(o => o is IConditionalOperation or IConditionalAccessOperation or IAnonymousFunctionOperation or ILocalFunctionOperation
                    || o is IBinaryOperation { OperatorKind:BinaryOperatorKind.ConditionalAnd or BinaryOperatorKind.ConditionalOr }
                    || o is IInvocationOperation invocation && !ParameterMutation(invocation) && !DatabaseOperationApi.TryClassify(invocation.TargetMethod,out _,out _)))
            { Reset(); continue; }
            foreach (var operation in operations)
            {
                if (operation is ISimpleAssignmentOperation assignment)
                {
                    if (SymbolEqualityComparer.Default.Equals(Symbol(assignment.Target),receiver))
                    {
                        Reset();
                        if (assignment.Value is IObjectCreationOperation creation) Creation(creation);
                    }
                    else if (assignment.Target is IPropertyReferenceOperation property && SymbolEqualityComparer.Default.Equals(Symbol(property.Instance),receiver)
                        && DatabaseOperationApi.Trusted(property.Property.ContainingAssembly))
                    {
                        switch (property.Property.Name)
                        {
                            case "CommandText": Text(assignment.Value); break;
                            case "CommandType": p["commandTypeEvidence"] = (assignment.Value.ConstantValue.HasValue ? assignment.Value.ConstantValue.Value : null) switch { 1 => "Text", 4 => "StoredProcedure", 512 => "TableDirect", _ => "unavailable" }; break;
                            case "Transaction": p["transactionEvidence"] = "local-assignment-witness"; break;
                            case "Connection": p["connectionEvidence"] = "local-assignment-witness"; break;
                        }
                    }
                    else if (assignment.Target is IPropertyReferenceOperation parameter && Symbol(parameter.Instance) is { } ps && witnesses.Contains(ps))
                    {
                        if (parameter.Property.Name == "Direction")
                            Witness("parameterDirectionKinds", (assignment.Value.ConstantValue.HasValue ? assignment.Value.ConstantValue.Value : null) switch { 1 => "Input", 2 => "Output", 3 => "InputOutput", 6 => "ReturnValue", _ => "unavailable" });
                        if (parameter.Property.Name is "DbType" or "SqlDbType")
                        {
                            Witness("parameterTypeKinds", "enum-assignment-witness");
                            var value = assignment.Value;
                            while (value is IConversionOperation conversion) value = conversion.Operand;
                            if (value is IFieldReferenceOperation field && DatabaseOperationApi.Trusted(field.Field.ContainingAssembly))
                                Witness("parameterTypeNames", field.Field.ContainingType.Name + "." + field.Field.Name);
                        }
                    }
                }
                if (operation is IVariableDeclaratorOperation variable && SymbolEqualityComparer.Default.Equals(variable.Symbol,receiver))
                {
                    Reset(); if (variable.Initializer?.Value is IObjectCreationOperation creation) Creation(creation);
                }
                if (operation is IInvocationOperation invocation)
                {
                    if (ParameterMutation(invocation))
                    {
                        p["parameterEvidence"] = "local-collection-mutation-witness";
                        for (var parent = invocation.Parent; parent is not null && parent != statement; parent = parent.Parent)
                            if (parent is IVariableDeclaratorOperation v) witnesses.Add(v.Symbol);
                        foreach (var declaration in operations.OfType<IVariableDeclarationOperation>().Where(d => d.Initializer?.Syntax.Span.Contains(invocation.Syntax.Span) == true))
                            foreach (var parameterVariable in declaration.Declarators) witnesses.Add(parameterVariable.Symbol);
                        foreach (var argument in invocation.Arguments.Where(a => a.Parameter?.Type.Name is "SqlDbType" or "DbType"))
                        {
                            Witness("parameterTypeKinds", "enum-argument-witness");
                            var value = argument.Value;
                            while (value is IConversionOperation conversion) value = conversion.Operand;
                            if (value is IFieldReferenceOperation field && DatabaseOperationApi.Trusted(field.Field.ContainingAssembly))
                                Witness("parameterTypeNames", field.Field.ContainingType.Name + "." + field.Field.Name);
                        }
                    }
                    else if (!DatabaseOperationApi.TryClassify(invocation.TargetMethod,out _,out _)) Reset();
                }
            }
        }
        void Text(IOperation value)
        {
            if (value.ConstantValue is { HasValue:true, Value:string text })
            { p["commandTextAvailability"] = "hash-only-constant"; p["commandTextHash"] = FactFactory.Hash(text,64); }
            else { p["commandTextAvailability"] = "dynamic"; p.Remove("commandTextHash"); }
        }
        void Creation(IObjectCreationOperation creation)
        {
            // Constructor type must itself be a recognized public command API owner.
            if (creation.Type is not INamedTypeSymbol type || !DatabaseOperationApi.Trusted(type.ContainingAssembly) || !DatabaseOperationApi.IsSyntaxType(type.ToDisplayString(),"ExecuteScalar")) return;
            var text = creation.Arguments.FirstOrDefault(a => a.Parameter?.Name is "commandText" or "cmdText");
            if (text is not null) Text(text.Value);
            // Constructor default is not inferred; require an explicit type witness.
            if (creation.Arguments.Any(a => a.Parameter?.Name == "connection")) p["connectionEvidence"] = "constructor-argument-witness";
        }
    }
    private static ISymbol? Symbol(IOperation? op) => op switch
    { ILocalReferenceOperation l => l.Local, IParameterReferenceOperation p => p.Parameter, IConversionOperation c => Symbol(c.Operand), _ => null };
    private static IEnumerable<IOperation> Walk(IOperation operation)
    {
        yield return operation;
        foreach (var child in operation.ChildOperations) foreach (var nested in Walk(child)) yield return nested;
    }
}
