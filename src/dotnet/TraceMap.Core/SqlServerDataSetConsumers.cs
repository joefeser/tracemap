using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using static TraceMap.Core.SqlServerDataTableConsumers;
using static TraceMap.Core.SqlServerReaderColumns;

namespace TraceMap.Core;

/// <param name="FillOverload">fill-dataset (<c>Fill(DataSet)</c>) or fill-dataset-source-table (<c>Fill(DataSet, constant)</c>).</param>
/// <param name="TableName">The DataSet table name a fresh adapter with default mappings creates for the first result.</param>
/// <param name="Selectors">Sorted subset of table-local, tables-index and tables-name that select the first table.</param>
/// <param name="Guards">Sorted subset of foreach, rows-count and tables-count; guards are bound to the selected DataSet/table only.</param>
/// <param name="Accesses">The Phase 2 DataRow access records, read through rows of the first table only.</param>
public sealed record SqlServerDataSetState(string Version, string Coverage, string? Gap, string? CommandType, string? CommandTextHash,
    string? AdapterAssociation, string? FillOverload, string? TableName, int? AdapterLine, int? DataSetLine, IReadOnlyList<string> Selectors,
    IReadOnlyList<string> Guards, IReadOnlyList<SqlServerDataTableAccess> Accesses);

/// <summary>
/// Independent fail-closed DataSet consumer state for one <c>Fill(DataSet)</c> or <c>Fill(DataSet, constant)</c> invocation.
/// Sibling of sqlServerDataTableState (which keeps refusing DataSet overloads) and separate from reader and parameter state.
/// The command and adapter keep the Phase 2 rules; one fresh untyped DataSet is filled exactly once and only the first
/// table, selected by <c>Tables[0]</c> or the exact default name a fresh adapter creates, is read through the Phase 2
/// table, rows and row rules. It never claims that Fill executes, that any table or row exists, or anything about later
/// result sets, types, nullability or values.
/// </summary>
internal static class SqlServerDataSetConsumers
{
    internal const string Version = "sqlserver-dataset/1";
    private const string DataSetType = "System.Data.DataSet";
    private static readonly HashSet<string> DataSetMutations = new(StringComparer.Ordinal)
    {
        "Merge", "ReadXml", "ReadXmlSchema", "InferXmlSchema", "Load", "Clear", "Reset", "AcceptChanges", "RejectChanges", "BeginInit", "EndInit"
    };
    private static readonly HashSet<string> DataSetRowOwnership = new(StringComparer.Ordinal) { "Copy", "Clone", "GetChanges", "CreateDataReader" };
    private static readonly HashSet<string> TablesMutations = new(StringComparer.Ordinal) { "Add", "AddRange", "Remove", "RemoveAt", "Clear" };

    internal static void Add(SortedDictionary<string, string> properties, IInvocationOperation call)
        => properties["sqlServerDataSetState"] = SqlServerSchemaEvidence.Json(Extract(call));

    private static SqlServerDataSetState Unknown(string gap) => new(Version, "unknown", gap, null, null, null, null, null, null, null, [], [], []);

    internal static SqlServerDataSetState Extract(IInvocationOperation call)
    {
        var method = call.TargetMethod;
        var declaring = method.ContainingType.ToDisplayString();
        // SqlDataAdapter is sealed and declares no Fill: DataAdapter.Fill(DataSet) and its DbDataAdapter override dispatch alike.
        var overload = method.Name != "Fill" || !DatabaseOperationApi.Trusted(method.ContainingAssembly) || method.Parameters.Length is 0 or > 2
            || !IsType(method.Parameters[0].Type, DataSetType) ? null
            : method.Parameters.Length == 1 && declaring is "System.Data.Common.DataAdapter" or "System.Data.Common.DbDataAdapter" ? "fill-dataset"
            : method.Parameters.Length == 2 && method.Parameters[1].Type.SpecialType == SpecialType.System_String
                && declaring == "System.Data.Common.DbDataAdapter" ? "fill-dataset-source-table" : null;
        if (overload is null) return Unknown("fill-overload-unsupported");
        if (!FillStatement(call)) return Unknown("fill-result-use-unsupported");
        if (FillAdapter(call) is not { } adapter) return Unknown("fresh-sqldataadapter-local-required");
        // Typed DataSets carry pre-declared tables and mappings; only the exact untyped DataSet is in the subset.
        if (Local(Argument(call, 0)) is not { } set || !IsType(set.Type, DataSetType)) return Unknown("fresh-dataset-local-required");
        var tableName = "Table"; // DbDataAdapter.DefaultSourceTableName, passed through unchanged by empty default mappings.
        if (overload == "fill-dataset-source-table")
        {
            if (Argument(call, 1)?.ConstantValue is not { HasValue: true } source) return Unknown("dynamic-source-table-unsupported");
            if (source.Value is not string { Length: > 0 } name) return Unknown("invalid-source-table");
            // Only the bounded identifier vocabulary is retained; arbitrary constant text is not copied into facts.
            if (!SqlServerSchemaEvidence.Identifier(name)) return Unknown("source-table-outside-identifier-vocabulary");
            tableName = name;
        }
        if (MethodOperations(call) is not { } all) return Unknown("operation-limit");
        var fillEnd = call.Syntax.Span.End;

        // Table selectors: Tables[0] or the exact-case first-result name only; later tables and other names stay unknown.
        var selectors = new HashSet<IOperation>();
        var selectorKinds = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var indexer in all.OfType<IPropertyReferenceOperation>().Where(p => p.Property.IsIndexer && TablesOf(p.Instance, set) is not null))
        {
            if (SelectorGap(indexer, tableName, out var kind) is { } selectorGap) return Unknown(selectorGap);
            selectors.Add(indexer);
            selectorKinds.Add(kind);
        }
        var tableLocals = new List<ILocalSymbol>();
        foreach (var selector in selectors)
            if (selector.Parent is IVariableInitializerOperation initializer && Declared(initializer) is { RefKind: RefKind.None } local && IsType(local.Type, "System.Data.DataTable"))
            {
                tableLocals.Add(local);
                selectorKinds.Add("table-local");
            }
        bool Selected(IOperation? operation) => Unwrap(operation) is { } unwrapped
            && (selectors.Contains(unwrapped) || unwrapped is ILocalReferenceOperation reference && tableLocals.Any(t => Same(t, reference.Local)));

        // Method-wide flow: only guards bound to the selected DataSet and its first table are admitted.
        var guarded = new HashSet<IOperation>();
        var foreachRows = new List<ILocalSymbol>();
        var guards = new SortedSet<string>(StringComparer.Ordinal);
        if (Flow(all, Selected, condition => TablesCountGuard(condition, set), guarded, foreachRows, guards, "tables-count") is { } flowGap)
            return Unknown(flowGap);

        // Construction: the Phase 2 adapter and one fresh untyped new DataSet() without arguments or initializer.
        if (AdapterCreation(all, adapter, out var adapterDeclarator, out var adapterCreation) is { } adapterGap) return Unknown(adapterGap);
        var setDeclarator = all.OfType<IVariableDeclaratorOperation>().SingleOrDefault(d => Same(d.Symbol, set));
        var setCreation = Unwrap(setDeclarator is null ? null : Initializer(setDeclarator)) as IObjectCreationOperation;
        if (setCreation?.Type is null || !IsType(setCreation.Type, DataSetType)) return Unknown("fresh-dataset-required");
        if (setCreation.Initializer is not null) return Unknown("dataset-initializer-unsupported");
        // DataSetName does not take part in table mapping, but named DataSets stay a later, separately decided slice.
        if (setCreation.Arguments.Length != 0) return Unknown("dataset-constructor-unsupported");
        if (ImplicitReceiver(all)) return Unknown("implicit-receiver-unsupported");

        if (AdapterAndCommand(all, call, adapter, set, adapterCreation, [set, .. tableLocals], out var associationKind, out var commandType, out var textHash) is { } commandGap)
            return Unknown(commandGap);

        // DataSet: the only uses are this Fill, first-table selectors after it, the Tables.Count guard and Dispose.
        var rows = new List<IPropertyReferenceOperation>();
        foreach (var reference in all.OfType<ILocalReferenceOperation>().Where(r => Same(r.Local, set)))
        {
            if (reference.Parent is IArgumentOperation argument && argument.Parent == call) continue;
            if (DataSetUse(reference, rows, guarded, tableLocals) is { } gap) return Unknown(gap);
            // Tables read before the Fill would observe an empty DataSet.
            if (reference.Syntax.SpanStart < fillEnd) return Unknown("dataset-use-before-fill");
        }
        foreach (var reference in all.OfType<ILocalReferenceOperation>().Where(r => tableLocals.Any(t => Same(t, r.Local))))
            if (TableUse(reference, rows, allowDispose: false) is { } gap) return Unknown(gap);

        var accesses = new List<(int Position, SqlServerDataTableAccess Access)>();
        if (RowsAndAccesses(all, rows, guarded, foreachRows, accesses) is { } rowGap) return Unknown(rowGap);
        if (accesses.Count > SqlServerDataTableConsumers.MaxAccesses) return Unknown("access-limit");
        var adapterLine = adapterDeclarator.Syntax.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
        var setLine = setDeclarator!.Syntax.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
        return new(Version, "bounded", null, commandType, textHash, associationKind, overload, tableName, adapterLine, setLine,
            selectorKinds.ToArray(), guards.ToArray(), accesses.OrderBy(a => a.Position).Select(a => a.Access).ToArray());
    }

    /// <summary>Classifies one <c>set.Tables[...]</c> selector against the first-result table name.</summary>
    private static string? SelectorGap(IPropertyReferenceOperation indexer, string tableName, out string kind)
    {
        kind = "";
        var parameters = indexer.Property.Parameters;
        if (parameters.Length != 1) return "dataset-table-namespace-unsupported";
        var value = indexer.Arguments[0].Value.ConstantValue;
        if (!value.HasValue) return "dynamic-table-selector-unsupported";
        if (parameters[0].Type.SpecialType == SpecialType.System_Int32 && value.Value is int index)
        {
            if (index < 0) return "invalid-table-index";
            // Later result sets map to later tables; only the first result is compared.
            if (index > 0) return "dataset-later-table-unsupported";
            kind = "tables-index";
            return null;
        }
        if (parameters[0].Type.SpecialType == SpecialType.System_String && value.Value is string name)
        {
            if (string.Equals(name, tableName, StringComparison.Ordinal))
            {
                kind = "tables-name";
                return null;
            }
            // DataTableCollection falls back to a case-insensitive match at runtime; that is not claimed.
            return string.Equals(name, tableName, StringComparison.OrdinalIgnoreCase) ? "dataset-table-name-case-unknown" : "dataset-table-name-unbound";
        }
        return "dataset-table-name-unbound";
    }

    /// <summary>One use of the filled DataSet local other than the Fill argument.</summary>
    private static string? DataSetUse(ILocalReferenceOperation reference, List<IPropertyReferenceOperation> rows, HashSet<IOperation> guarded,
        List<ILocalSymbol> tableLocals)
    {
        var parent = reference.Parent;
        if (parent is IPropertyReferenceOperation property && property.Instance == reference)
        {
            if (property.Parent is ISimpleAssignmentOperation write && write.Target == property) return "dataset-mutation";
            if (!Trusted(property.Property)) return "unsupported-dataset-member";
            if (property.Property.Name == "Relations") return "dataset-relations-unsupported";
            if (property.Property.Name != "Tables") return "unsupported-dataset-member";
            if (guarded.Contains(property)) return null;
            var use = property.Parent;
            // Selectors were validated method-wide before flow; only the selected first table reaches the table rules.
            if (use is IPropertyReferenceOperation { Property.IsIndexer: true } selector && selector.Instance == property)
                return selector.Parent is IVariableInitializerOperation initializer
                    ? Declared(initializer) is { } local && tableLocals.Any(t => Same(t, local)) ? null : "datatable-alias-or-escape"
                    : TableUse(selector, rows, allowDispose: false);
            if (use is IPropertyReferenceOperation { Property.Name: "Count" } count && count.Instance == property) return "dataset-tables-count-unsupported";
            if (use is IInvocationOperation invocation && invocation.Instance == property)
                return TablesMutations.Contains(invocation.TargetMethod.Name) ? "dataset-mutation" : "unsupported-dataset-tables-member";
            if (use is IPropertyReferenceOperation other && other.Instance == property || use is IEventReferenceOperation { } tableEvent && tableEvent.Instance == property)
                return "unsupported-dataset-tables-member";
            return "dataset-tables-alias-or-escape";
        }
        if (parent is IInvocationOperation call && call.Instance == reference)
        {
            var name = call.TargetMethod.Name;
            if (name == "Dispose" && call.TargetMethod.Parameters.Length == 0 && call.Parent is IExpressionStatementOperation) return null;
            return DataSetMutations.Contains(name) ? "dataset-mutation" : DataSetRowOwnership.Contains(name) ? "ambiguous-row-ownership" : "unsupported-dataset-member";
        }
        if (parent is IEventReferenceOperation setEvent && setEvent.Instance == reference) return "unsupported-dataset-member";
        if (parent is IArgumentOperation { Parent: IInvocationOperation consumer })
        {
            var target = consumer.TargetMethod;
            return target.Name == "Fill" && DatabaseOperationApi.TryClassify(target, out _, out _) ? "repeated-fill"
                : target.Name == "FillSchema" ? "fill-schema-unsupported" : "dataset-alias-or-escape";
        }
        if (parent is ISimpleAssignmentOperation reassigned && reassigned.Target == reference) return "dataset-reassignment";
        return "dataset-alias-or-escape";
    }

    /// <summary>The signed <c>set.Tables</c> property when <paramref name="operation"/> is exactly that collection.</summary>
    private static IPropertyReferenceOperation? TablesOf(IOperation? operation, ILocalSymbol set)
        => Unwrap(operation) is IPropertyReferenceOperation { Property.Name: "Tables" } tables && Same(Local(tables.Instance), set) && Trusted(tables.Property) ? tables : null;

    /// <summary><c>set.Tables.Count &gt; 0</c> on the filled DataSet only; returns its Tables reference.</summary>
    private static IPropertyReferenceOperation? TablesCountGuard(IOperation? condition, ILocalSymbol set)
        => Unwrap(condition) is IBinaryOperation { OperatorKind: BinaryOperatorKind.GreaterThan, OperatorMethod: null, IsLifted: false } comparison
            && comparison.RightOperand.ConstantValue is { HasValue: true, Value: 0 }
            && Unwrap(comparison.LeftOperand) is IPropertyReferenceOperation { Property.Name: "Count" } count && Trusted(count.Property)
            && TablesOf(count.Instance, set) is { } tables && count.Instance == tables ? tables : null;

    private static IOperation? Argument(IInvocationOperation call, int ordinal) => call.Arguments.SingleOrDefault(a => a.Parameter?.Ordinal == ordinal)?.Value;
    private static ILocalSymbol? Declared(IVariableInitializerOperation initializer)
        => (initializer.Parent as IVariableDeclaratorOperation
            ?? (initializer.Parent is IVariableDeclarationOperation { Declarators.Length: 1 } declaration ? declaration.Declarators[0] : null))?.Symbol;
}
