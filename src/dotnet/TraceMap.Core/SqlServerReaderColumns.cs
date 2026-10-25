using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace TraceMap.Core;

/// <summary>One bounded column access on the exact reader instance returned by one execution.</summary>
/// <param name="Kind">name-indexer, ordinal-indexer, ordinal-getter or get-ordinal.</param>
/// <param name="OrdinalSource">constant, get-ordinal (direct argument) or get-ordinal-local; null for name accesses.</param>
/// <param name="Gap">Access-level unknown reason; null when the name or ordinal is a compile-time constant.</param>
public sealed record SqlServerReaderAccess(string Kind, string Member, string? Name, int? Ordinal, string? OrdinalSource,
    int Line, int EndLine, string? Gap);
public sealed record SqlServerReaderState(string Version, string Coverage, string? Gap, string? CommandType, string? CommandTextHash,
    int? ReaderLine, int? ReaderEndLine, string ReadGuard, IReadOnlyList<SqlServerReaderAccess> Accesses);

/// <summary>
/// Independent fail-closed reader consumer state for one ExecuteReader execution. Separate from
/// sqlServerParameterState: it tolerates only a using lifetime and one Read()-conditioned if/while.
/// Every reference to the command, reader and ordinal locals in the whole method is classified;
/// an unclassified reference is an alias/escape gap. It never claims types, nullability or rows.
/// </summary>
internal static class SqlServerReaderColumns
{
    internal const string Version = "sqlserver-reader/1";
    internal const int MaxOperations = 20000;
    internal const int MaxAccesses = 256;
    private static readonly string[] CommandTypes = ["System.Data.SqlClient.SqlCommand", "Microsoft.Data.SqlClient.SqlCommand"];
    private static readonly string[] ReaderTypes = ["System.Data.SqlClient.SqlDataReader", "Microsoft.Data.SqlClient.SqlDataReader"];
    private static readonly string[] CollectionTypes = ["System.Data.SqlClient.SqlParameterCollection", "Microsoft.Data.SqlClient.SqlParameterCollection"];

    internal static void Add(SortedDictionary<string, string> properties, IInvocationOperation call)
        => properties["sqlServerReaderState"] = SqlServerSchemaEvidence.Json(Extract(call));

    private static SqlServerReaderState Unknown(string gap) => new(Version, "unknown", gap, null, null, null, null, "none", []);

    internal static SqlServerReaderState Extract(IInvocationOperation call)
    {
        var method = call.TargetMethod;
        if (method.Name != "ExecuteReader") return Unknown("async-or-apm-execution-unsupported");
        if (!CommandTypes.Contains(method.ContainingType.ToDisplayString(), StringComparer.Ordinal) || !DatabaseOperationApi.Trusted(method.ContainingAssembly))
            return Unknown("sqlcommand-required");
        if (call.Arguments.Length != 0) return Unknown("command-behavior-unsupported");
        if (Local(call.Instance) is not { } command) return Unknown("fresh-command-local-required");
        // The reader must be the direct initializer of one fresh SqlDataReader local.
        ILocalSymbol? reader = null;
        if (call.Parent is IVariableInitializerOperation initializer)
        {
            if (initializer.Parent is IVariableDeclaratorOperation declarator) reader = declarator.Symbol;
            else if (initializer.Parent is IVariableDeclarationOperation { Declarators.Length: 1 } declaration) reader = declaration.Declarators[0].Symbol;
        }
        if (reader is null) return Unknown("direct-reader-local-required");
        if (!ReaderTypes.Contains(reader.Type.ToDisplayString(), StringComparer.Ordinal) || !DatabaseOperationApi.Trusted(reader.Type.ContainingAssembly))
            return Unknown("sqldatareader-local-required");
        IOperation root = call;
        while (root.Parent is not null) root = root.Parent;
        var all = Walk(root).Take(MaxOperations + 1).ToArray();
        if (all.Length > MaxOperations) return Unknown("operation-limit");
        foreach (var op in all)
        {
            // Only a using lifetime and if/top-tested while conditioned exactly on some reader's Read() are tolerated.
            if (op is IWhileLoopOperation { ConditionIsTop: true, ConditionIsUntil: false } loop && IsReadCondition(loop.Condition)) continue;
            if (op is IConditionalOperation { Type: null } branch && IsReadCondition(branch.Condition)) continue;
            if (op is IConditionalOperation or ILoopOperation or IBranchOperation or ITryOperation or IAnonymousFunctionOperation
                or ILocalFunctionOperation or IInvalidOperation or ICompoundAssignmentOperation or IIncrementOrDecrementOperation
                or IDeconstructionAssignmentOperation or IDynamicInvocationOperation or IDynamicObjectCreationOperation
                or IDynamicMemberReferenceOperation or IDynamicIndexerAccessOperation or IConditionalAccessOperation or ICoalesceOperation
                or IWithOperation or IBinaryOperation { OperatorKind: BinaryOperatorKind.ConditionalAnd or BinaryOperatorKind.ConditionalOr })
                return Unknown("unsupported-control-flow");
            if (op is IInstanceReferenceOperation { ReferenceKind: InstanceReferenceKind.ImplicitReceiver }) return Unknown("implicit-receiver-unsupported");
        }

        // Command: declared from a signed SqlCommand constructor, no initializer, inert constructor arguments.
        var commandDeclarator = all.OfType<IVariableDeclaratorOperation>().SingleOrDefault(d => Same(d.Symbol, command));
        var creation = Unwrap(commandDeclarator is null ? null : Initializer(commandDeclarator)) as IObjectCreationOperation;
        if (creation?.Type is null || !CommandTypes.Contains(creation.Type.ToDisplayString(), StringComparer.Ordinal)
            || !DatabaseOperationApi.Trusted(creation.Type.ContainingAssembly)) return Unknown("fresh-sqlcommand-required");
        if (creation.Initializer is not null) return Unknown("implicit-receiver-unsupported");
        if (creation.Arguments.Any(a => !Inert(a.Value, command, reader))) return Unknown("unsupported-constructor-argument");
        var text = creation.Arguments.FirstOrDefault(a => a.Parameter?.Name is "cmdText" or "commandText");
        var textHash = text?.Value.ConstantValue is { HasValue: true, Value: string s } ? FactFactory.Hash(s, 64) : null;
        string? commandType = null;
        var start = call.Syntax.SpanStart;
        foreach (var reference in all.OfType<ILocalReferenceOperation>().Where(r => Same(r.Local, command)))
        {
            var parent = reference.Parent;
            if (parent is IInvocationOperation invocation && invocation.Instance == reference)
            {
                if (invocation == call) continue;
                if (DatabaseOperationApi.TryClassify(invocation.TargetMethod, out _, out _))
                {
                    if (invocation.Syntax.SpanStart < start) return Unknown("prior-execution");
                    continue;
                }
                return Unknown("unsupported-command-member");
            }
            if (parent is IPropertyReferenceOperation property && property.Instance == reference && DatabaseOperationApi.Trusted(property.Property.ContainingAssembly))
            {
                if (property.Property.Name == "Parameters" && property.Parent is IInvocationOperation add && add.Instance == property
                    && add.TargetMethod.Name is "Add" or "AddWithValue" && CollectionTypes.Contains(add.TargetMethod.ContainingType.ToDisplayString(), StringComparer.Ordinal)
                    && DatabaseOperationApi.Trusted(add.TargetMethod.ContainingAssembly) && add.Arguments.All(a => Inert(a.Value, command, reader)))
                {
                    if (add.Parent is not (IExpressionStatementOperation or IVariableInitializerOperation)) return Unknown("command-alias-or-escape");
                    continue;
                }
                if (property.Parent is ISimpleAssignmentOperation assignment && assignment.Target == property)
                {
                    if (assignment.Syntax.SpanStart > start) continue; // Later mutation cannot change this execution.
                    if (property.Property.Name == "CommandType") commandType = assignment.Value.ConstantValue.Value switch { 4 => "StoredProcedure", 1 => "Text", _ => null };
                    else if (property.Property.Name == "CommandText") textHash = assignment.Value.ConstantValue is { HasValue: true, Value: string t } ? FactFactory.Hash(t, 64) : null;
                    else if (property.Property.Name == "Connection" && Unwrap(assignment.Value) is ILocalReferenceOperation or IParameterReferenceOperation && Inert(assignment.Value, command, reader)) { }
                    else return Unknown("unsupported-command-assignment");
                    continue;
                }
                return Unknown("unsupported-command-member");
            }
            if (parent is ISimpleAssignmentOperation reassigned && reassigned.Target == reference) return Unknown("command-reassignment");
            return Unknown("command-alias-or-escape");
        }

        // Ordinal locals declared directly from GetOrdinal(constant) on some reader.
        var ordinalLocals = new Dictionary<ILocalSymbol, (ILocalSymbol Reader, IInvocationOperation GetOrdinal)>(SymbolEqualityComparer.Default);
        foreach (var declarator in all.OfType<IVariableDeclaratorOperation>())
            if (Unwrap(Initializer(declarator)) is IInvocationOperation { TargetMethod.Name: "GetOrdinal" } getOrdinal && Local(getOrdinal.Instance) is { } owner
                && ReaderTypes.Contains(owner.Type.ToDisplayString(), StringComparer.Ordinal))
                ordinalLocals[declarator.Symbol] = (owner, getOrdinal);
        var rewritten = new HashSet<ILocalSymbol>(SymbolEqualityComparer.Default);
        foreach (var reference in all.OfType<ILocalReferenceOperation>().Where(r => ordinalLocals.ContainsKey(r.Local)))
            if (reference.Parent is ISimpleAssignmentOperation write && write.Target == reference
                || reference.Parent is IArgumentOperation { Parameter.RefKind: not RefKind.None })
                rewritten.Add(reference.Local);

        var accesses = new List<(int Position, SqlServerReaderAccess Access)>();
        var reads = 0;
        var guard = "none";
        foreach (var reference in all.OfType<ILocalReferenceOperation>().Where(r => Same(r.Local, reader)))
        {
            var parent = reference.Parent;
            if (parent is IPropertyReferenceOperation indexer && indexer.Instance == reference)
            {
                if (indexer.Parent is ISimpleAssignmentOperation target && target.Target == indexer) return Unknown("reader-alias-or-escape");
                if (!Trusted(indexer.Property.ContainingType, indexer.Property.ContainingAssembly) || indexer.Property.Parameters.Length != 1 || indexer.Arguments.Length != 1)
                    return Unknown("unsupported-reader-member");
                var argument = Unwrap(indexer.Arguments[0].Value)!;
                var parameterType = indexer.Property.Parameters[0].Type.SpecialType;
                if (parameterType == SpecialType.System_String) accesses.Add((indexer.Syntax.SpanStart, NameAccess("name-indexer", "Item", argument, indexer)));
                else if (parameterType == SpecialType.System_Int32) accesses.Add((indexer.Syntax.SpanStart, OrdinalAccess("ordinal-indexer", "Item", argument, indexer, reader, ordinalLocals, rewritten)));
                else return Unknown("unsupported-reader-member");
                continue;
            }
            if (parent is IInvocationOperation member && member.Instance == reference)
            {
                var target = member.TargetMethod;
                if (!Trusted(target.ContainingType, target.ContainingAssembly)) return Unknown("unsupported-reader-member");
                if (target.Name == "Read" && target.Parameters.Length == 0)
                {
                    if (++reads > 1) return Unknown("multiple-read-calls");
                    if (member.Parent is IWhileLoopOperation { ConditionIsTop: true, ConditionIsUntil: false } loop && loop.Condition == member) guard = "while";
                    else if (member.Parent is IConditionalOperation { Type: null } branch && branch.Condition == member) guard = "if";
                    else return Unknown("read-outside-guard");
                    continue;
                }
                if (target.Name is "Close" or "Dispose" && target.Parameters.Length == 0 && member.Parent is IExpressionStatementOperation) continue;
                if (target.Name == "GetOrdinal" && target.Parameters.Length == 1 && target.Parameters[0].Type.SpecialType == SpecialType.System_String)
                {
                    accesses.Add((member.Syntax.SpanStart, NameAccess("get-ordinal", "GetOrdinal", Unwrap(member.Arguments[0].Value)!, member)));
                    continue;
                }
                if ((target.Name.StartsWith("Get", StringComparison.Ordinal) || target.Name == "IsDBNull") && !target.Name.EndsWith("Async", StringComparison.Ordinal)
                    && target.Name is not ("GetOrdinal" or "GetEnumerator" or "GetSchemaTable" or "GetValues" or "GetData")
                    && target.Parameters.Length == 1 && target.Parameters[0].Type.SpecialType == SpecialType.System_Int32 && target.Parameters[0].RefKind == RefKind.None)
                {
                    accesses.Add((member.Syntax.SpanStart, OrdinalAccess("ordinal-getter", target.Name, Unwrap(member.Arguments[0].Value)!, member, reader, ordinalLocals, rewritten)));
                    continue;
                }
                // NextResult, RecordsAffected/FieldCount (via property), async members and others are outside the first-result subset.
                return Unknown(target.Name == "NextResult" ? "next-result-unsupported" : "unsupported-reader-member");
            }
            if (parent is IPropertyReferenceOperation other && other.Instance == reference) return Unknown("unsupported-reader-member");
            if (parent is ISimpleAssignmentOperation reassigned && reassigned.Target == reference) return Unknown("reader-reassignment");
            return Unknown("reader-alias-or-escape");
        }
        // Escapes of the reader through property syntax that is not an instance reference (for example `Item` on another receiver) are caught above.
        if (accesses.Count > MaxAccesses) return Unknown("access-limit");
        var span = reader.DeclaringSyntaxReferences.FirstOrDefault()?.GetSyntax().GetLocation().GetLineSpan();
        return new(Version, "bounded", null, commandType, textHash,
            span is { } s1 ? s1.StartLinePosition.Line + 1 : null, span is { } s2 ? s2.EndLinePosition.Line + 1 : null, guard,
            accesses.OrderBy(a => a.Position).Select(a => a.Access).ToArray());
    }

    private static SqlServerReaderAccess NameAccess(string kind, string member, IOperation argument, IOperation site)
    {
        var (line, end) = Lines(site);
        if (argument.ConstantValue is not { HasValue: true, Value: string name }) return new(kind, member, null, null, null, line, end, "dynamic-column-name");
        // Only the bounded identifier vocabulary is retained; arbitrary constant text is not copied into facts.
        return SqlServerSchemaEvidence.Identifier(name) ? new(kind, member, name, null, null, line, end, null)
            : new(kind, member, null, null, null, line, end, "column-name-outside-identifier-vocabulary");
    }

    private static SqlServerReaderAccess OrdinalAccess(string kind, string member, IOperation argument, IOperation site, ILocalSymbol reader,
        Dictionary<ILocalSymbol, (ILocalSymbol Reader, IInvocationOperation GetOrdinal)> ordinalLocals, HashSet<ILocalSymbol> rewritten)
    {
        var (line, end) = Lines(site);
        if (argument.ConstantValue is { HasValue: true, Value: int ordinal }) return new(kind, member, null, ordinal, "constant", line, end, null);
        if (argument is IInvocationOperation { TargetMethod.Name: "GetOrdinal" } direct && Same(Local(direct.Instance), reader))
            return FromName(direct, "get-ordinal");
        if (argument is ILocalReferenceOperation local && ordinalLocals.TryGetValue(local.Local, out var source))
        {
            if (!Same(source.Reader, reader)) return new(kind, member, null, null, "get-ordinal-local", line, end, "ordinal-from-other-reader");
            if (rewritten.Contains(local.Local)) return new(kind, member, null, null, "get-ordinal-local", line, end, "ordinal-local-reassigned");
            return FromName(source.GetOrdinal, "get-ordinal-local");
        }
        return new(kind, member, null, null, null, line, end, "dynamic-ordinal");

        SqlServerReaderAccess FromName(IInvocationOperation getOrdinal, string origin)
        {
            var named = NameAccess(kind, member, Unwrap(getOrdinal.Arguments[0].Value)!, site);
            return named with { OrdinalSource = origin };
        }
    }

    private static bool IsReadCondition(IOperation? condition)
        => Unwrap(condition) is IInvocationOperation { TargetMethod.Name: "Read", TargetMethod.Parameters.Length: 0 } read
            && Local(read.Instance) is { } local && ReaderTypes.Contains(local.Type.ToDisplayString(), StringComparer.Ordinal)
            && Trusted(read.TargetMethod.ContainingType, read.TargetMethod.ContainingAssembly);
    private static bool Trusted(INamedTypeSymbol type, IAssemblySymbol assembly)
        => DatabaseOperationApi.Trusted(assembly) && (ReaderTypes.Contains(type.ToDisplayString(), StringComparer.Ordinal)
            || type.ToDisplayString() is "System.Data.Common.DbDataReader");
    private static bool Inert(IOperation value, ILocalSymbol command, ILocalSymbol reader)
        => Walk(value).All(o => o is ILiteralOperation or IDefaultValueOperation or IParameterReferenceOperation or IFieldReferenceOperation { Field.IsConst: true }
            or IConversionOperation { OperatorMethod: null } || o is ILocalReferenceOperation l && !Same(l.Local, command) && !Same(l.Local, reader));
    private static IOperation? Initializer(IVariableDeclaratorOperation declarator)
        => declarator.Initializer?.Value ?? (declarator.Parent is IVariableDeclarationOperation { Declarators.Length: 1 } declaration ? declaration.Initializer?.Value : null);
    private static (int, int) Lines(IOperation operation)
    {
        var span = operation.Syntax.GetLocation().GetLineSpan();
        return (span.StartLinePosition.Line + 1, span.EndLinePosition.Line + 1);
    }
    private static bool Same(ISymbol? left, ISymbol? right) => SymbolEqualityComparer.Default.Equals(left, right);
    private static IOperation? Unwrap(IOperation? o) => o is IConversionOperation { OperatorMethod: null } c ? Unwrap(c.Operand) : o is IParenthesizedOperation p ? Unwrap(p.Operand) : o;
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
