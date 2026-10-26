using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using static TraceMap.Core.SqlServerReaderColumns;

namespace TraceMap.Core;

/// <summary>One bounded constant column read through a DataRow of the exact table filled by one Fill invocation.</summary>
/// <param name="Kind">name-indexer, ordinal-indexer, isnull-name or isnull-ordinal.</param>
/// <param name="RowSource">rows-index (direct <c>table.Rows[k]</c>), row-local or foreach-row.</param>
/// <param name="RowIndex">The constant row index for rows-index and row-local; null for foreach-row. Never a row-existence claim.</param>
/// <param name="Gap">Access-level unknown reason; null when the name or ordinal is a compile-time constant.</param>
public sealed record SqlServerDataTableAccess(string Kind, string Member, string RowSource, int? RowIndex, string? Name, int? Ordinal,
    int Line, int EndLine, string? Gap);
/// <param name="AdapterAssociation">constructor-command or select-command-property.</param>
/// <param name="Guard">none, rows-count, foreach or rows-count-and-foreach; guards are bound to the selected table only.</param>
public sealed record SqlServerDataTableState(string Version, string Coverage, string? Gap, string? CommandType, string? CommandTextHash,
    string? AdapterAssociation, int? AdapterLine, int? TableLine, string Guard, IReadOnlyList<SqlServerDataTableAccess> Accesses);

/// <summary>
/// Independent fail-closed DataTable consumer state for one <c>DbDataAdapter.Fill(DataTable)</c> invocation. Separate from
/// sqlServerReaderState and sqlServerParameterState. The command keeps the #912 rules with the adapter association as the
/// cut-off; one fresh SqlDataAdapter, one fresh DataTable filled exactly once and directly associated DataRows are
/// classified at every reference in the whole method; anything unclassified is unknown. It never claims that Fill
/// executes, that rows exist, or anything about types, nullability or values.
/// </summary>
internal static class SqlServerDataTableConsumers
{
    internal const string Version = "sqlserver-datatable/1";
    internal const int MaxOperations = 20000;
    internal const int MaxAccesses = 256;
    private static readonly string[] AdapterTypes = ["System.Data.SqlClient.SqlDataAdapter", "Microsoft.Data.SqlClient.SqlDataAdapter"];
    private static readonly HashSet<string> TableMutations = new(StringComparer.Ordinal)
    {
        "Load", "Merge", "ImportRow", "LoadDataRow", "NewRow", "Clear", "Reset", "AcceptChanges", "RejectChanges",
        "BeginLoadData", "EndLoadData", "BeginInit", "EndInit"
    };
    private static readonly HashSet<string> RowOwnershipMembers = new(StringComparer.Ordinal)
    {
        "Select", "Copy", "Clone", "GetChanges", "GetErrors", "CreateDataReader", "DefaultView"
    };
    private static readonly HashSet<string> SchemaMembers = new(StringComparer.Ordinal)
    {
        "Columns", "Constraints", "PrimaryKey", "ChildRelations", "ParentRelations"
    };

    internal static void Add(SortedDictionary<string, string> properties, IInvocationOperation call)
        => properties["sqlServerDataTableState"] = SqlServerSchemaEvidence.Json(Extract(call));

    private static SqlServerDataTableState Unknown(string gap) => new(Version, "unknown", gap, null, null, null, null, null, "none", []);

    internal static SqlServerDataTableState Extract(IInvocationOperation call)
    {
        var method = call.TargetMethod;
        if (method.Name != "Fill" || method.Parameters.Length != 1 || !IsType(method.Parameters[0].Type, "System.Data.DataTable")
            || method.ContainingType.ToDisplayString() != "System.Data.Common.DbDataAdapter" || !DatabaseOperationApi.Trusted(method.ContainingAssembly))
            return Unknown("fill-overload-unsupported");
        if (!FillStatement(call)) return Unknown("fill-result-use-unsupported");
        if (FillAdapter(call) is not { } adapter) return Unknown("fresh-sqldataadapter-local-required");
        if (Local(call.Arguments[0].Value) is not { } table || !IsType(table.Type, "System.Data.DataTable")) return Unknown("fresh-datatable-local-required");
        if (MethodOperations(call) is not { } all) return Unknown("operation-limit");
        var fillEnd = call.Syntax.Span.End;

        // Method-wide flow: only guards bound to the selected table are admitted.
        var guardRows = new HashSet<IOperation>();
        var foreachRows = new List<ILocalSymbol>();
        var guards = new SortedSet<string>(StringComparer.Ordinal);
        if (Flow(all, o => Same(Local(o), table), null, guardRows, foreachRows, guards) is { } flowGap) return Unknown(flowGap);

        // Construction: one fresh SqlDataAdapter and one fresh DataTable() without initializers.
        if (AdapterCreation(all, adapter, out var adapterDeclarator, out var adapterCreation) is { } adapterGap) return Unknown(adapterGap);
        var tableDeclarator = all.OfType<IVariableDeclaratorOperation>().SingleOrDefault(d => Same(d.Symbol, table));
        var tableCreation = Unwrap(tableDeclarator is null ? null : Initializer(tableDeclarator)) as IObjectCreationOperation;
        if (tableCreation?.Type is null || !IsType(tableCreation.Type, "System.Data.DataTable")) return Unknown("fresh-datatable-required");
        if (tableCreation.Initializer is not null) return Unknown("datatable-initializer-unsupported");
        // TableName participates in the adapter's table-mapping lookup; named tables are a later slice.
        if (tableCreation.Arguments.Length != 0) return Unknown("datatable-constructor-unsupported");
        // Any other object initializer or VB With block in the method is an implicit receiver outside the subset.
        if (ImplicitReceiver(all)) return Unknown("implicit-receiver-unsupported");

        // Adapter tied to exactly one command and used only for this Fill; the command keeps the #912 rules.
        if (AdapterAndCommand(all, call, adapter, table, adapterCreation, [table], out var associationKind, out var commandType, out var textHash) is { } commandGap)
            return Unknown(commandGap);

        // Table: the only uses are this Fill, row reads after it and the bounded guards.
        var rows = new List<IPropertyReferenceOperation>();
        foreach (var reference in all.OfType<ILocalReferenceOperation>().Where(r => Same(r.Local, table)))
        {
            if (reference.Parent is IArgumentOperation selected && selected.Parent == call) continue;
            if (TableUse(reference, rows, allowDispose: true) is { } gap) return Unknown(gap);
            // Rows read before the Fill would observe an empty, unfilled table.
            if (reference.Syntax.SpanStart < fillEnd) return Unknown("table-use-before-fill");
        }

        var accesses = new List<(int Position, SqlServerDataTableAccess Access)>();
        if (RowsAndAccesses(all, rows, guardRows, foreachRows, accesses) is { } rowGap) return Unknown(rowGap);
        if (accesses.Count > MaxAccesses) return Unknown("access-limit");
        var adapterLine = adapterDeclarator.Syntax.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
        var tableLine = tableDeclarator!.Syntax.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
        bool countGuard = guards.Contains("rows-count"), foreachGuard = guards.Contains("foreach");
        var guard = countGuard && foreachGuard ? "rows-count-and-foreach" : countGuard ? "rows-count" : foreachGuard ? "foreach" : "none";
        return new(Version, "bounded", null, commandType, textHash, associationKind, adapterLine, tableLine, guard,
            accesses.OrderBy(a => a.Position).Select(a => a.Access).ToArray());
    }

    // ---------------------------------------------------------------- Shared adapter-consumer rules (DataTable and DataSet)

    /// <summary>The int Fill result is either a standalone statement or explicitly discarded; any other use is outside the subset.</summary>
    internal static bool FillStatement(IInvocationOperation call)
        => call.Parent is IExpressionStatementOperation
            || call.Parent is ISimpleAssignmentOperation { Target: IDiscardOperation, Parent: IExpressionStatementOperation } discard && discard.Value == call;

    /// <summary>The receiver when it is a local of a signed SqlDataAdapter type.</summary>
    internal static ILocalSymbol? FillAdapter(IInvocationOperation call)
        => Local(call.Instance) is { } adapter && AdapterTypes.Contains(adapter.Type.ToDisplayString(), StringComparer.Ordinal)
            && DatabaseOperationApi.Trusted(adapter.Type.ContainingAssembly) ? adapter : null;

    /// <summary>Every operation of the containing method, or null above the operation bound.</summary>
    internal static IOperation[]? MethodOperations(IInvocationOperation call)
    {
        IOperation root = call;
        while (root.Parent is not null) root = root.Parent;
        var all = Walk(root).Take(MaxOperations + 1).ToArray();
        return all.Length > MaxOperations ? null : all;
    }

    /// <summary>
    /// Method-wide flow: admits foreach over the selected table's Rows, <c>selected.Rows.Count &gt; 0</c> guards and, when
    /// <paramref name="extraGuard"/> is supplied, its own guard condition (no else); every other flow is unsupported.
    /// Admitted guard operations are added to <paramref name="guarded"/> and their kind to <paramref name="kinds"/>.
    /// </summary>
    internal static string? Flow(IOperation[] all, Func<IOperation?, bool> selected, Func<IOperation?, IOperation?>? extraGuard,
        HashSet<IOperation> guarded, List<ILocalSymbol> foreachRows, SortedSet<string> kinds, string extraKind = "")
    {
        foreach (var op in all)
        {
            if (op is IForEachLoopOperation loop && RowsOf(loop.Collection, selected) is { } loopRows)
            {
                if (loop.LoopControlVariable is not IVariableDeclaratorOperation { Symbol: { RefKind: RefKind.None } row } || !IsType(row.Type, "System.Data.DataRow"))
                    return "foreach-row-type-unsupported";
                guarded.Add(loopRows);
                foreachRows.Add(row);
                kinds.Add("foreach");
                continue;
            }
            if (op is IConditionalOperation { Type: null, WhenFalse: null } branch)
            {
                if (CountGuard(branch.Condition, selected) is { } rows)
                {
                    guarded.Add(rows);
                    kinds.Add("rows-count");
                    continue;
                }
                if (extraGuard?.Invoke(branch.Condition) is { } extra)
                {
                    guarded.Add(extra);
                    kinds.Add(extraKind);
                    continue;
                }
            }
            if (UnsupportedFlow(op)) return "unsupported-control-flow";
        }
        return null;
    }

    /// <summary>One fresh signed <c>new SqlDataAdapter(...)</c> local without an initializer.</summary>
    internal static string? AdapterCreation(IOperation[] all, ILocalSymbol adapter, out IVariableDeclaratorOperation declarator, out IObjectCreationOperation creation)
    {
        declarator = all.OfType<IVariableDeclaratorOperation>().SingleOrDefault(d => Same(d.Symbol, adapter))!;
        creation = (Unwrap(declarator is null ? null : Initializer(declarator)) as IObjectCreationOperation)!;
        if (creation?.Type is null || !AdapterTypes.Contains(creation.Type.ToDisplayString(), StringComparer.Ordinal)
            || !DatabaseOperationApi.Trusted(creation.Type.ContainingAssembly)) return "fresh-sqldataadapter-required";
        return creation.Initializer is not null ? "adapter-initializer-unsupported" : null;
    }

    internal static bool ImplicitReceiver(IOperation[] all) => all.Any(op => op is IInstanceReferenceOperation { ReferenceKind: InstanceReferenceKind.ImplicitReceiver });

    /// <summary>
    /// The adapter is tied to exactly one command (constructor argument or one SelectCommand write before Fill) and used only
    /// for this Fill; another Fill naming <paramref name="target"/> is a repeated Fill. The command then keeps the #912 rules
    /// with the association as the cut-off. Returns null when both are supported.
    /// </summary>
    internal static string? AdapterAndCommand(IOperation[] all, IInvocationOperation call, ILocalSymbol adapter, ILocalSymbol target,
        IObjectCreationOperation adapterCreation, ILocalSymbol[] excluded, out string? associationKind, out string? commandType, out string? textHash)
    {
        associationKind = commandType = textHash = null;
        ILocalReferenceOperation? association = null;
        if (adapterCreation.Arguments.Length == 1 && adapterCreation.Arguments[0].Parameter?.Type is { } parameterType
            && CommandTypes.Contains(parameterType.ToDisplayString(), StringComparer.Ordinal))
        {
            if (Unwrap(adapterCreation.Arguments[0].Value) is not ILocalReferenceOperation argument) return "select-command-local-required";
            association = argument;
            associationKind = "constructor-command";
        }
        else if (adapterCreation.Arguments.Length != 0) return "adapter-constructor-unsupported";
        foreach (var reference in all.OfType<ILocalReferenceOperation>().Where(r => Same(r.Local, adapter)))
        {
            var parent = reference.Parent;
            if (parent is IInvocationOperation invocation && invocation.Instance == reference)
            {
                if (invocation == call) continue;
                if (invocation.TargetMethod.Name == "Dispose" && invocation.TargetMethod.Parameters.Length == 0 && invocation.Parent is IExpressionStatementOperation) continue;
                if (invocation.TargetMethod.Name == "Fill" && invocation.Arguments.Any(a => References(a.Value, target))) return "repeated-fill";
                if (invocation.TargetMethod.Name == "FillSchema") return "fill-schema-unsupported";
                return DatabaseOperationApi.TryClassify(invocation.TargetMethod, out _, out _) ? "adapter-reused" : "unsupported-adapter-member";
            }
            if (parent is IPropertyReferenceOperation property && property.Instance == reference)
            {
                if (property.Property.Name == "SelectCommand" && property.Parent is ISimpleAssignmentOperation write && write.Target == property)
                {
                    if (association is not null) return "multiple-select-command-writes";
                    if (write.Parent is not IExpressionStatementOperation || Walk(write.Value).Any(o => o is IAssignmentOperation)
                        || Ancestors(write).Any(a => a is IConditionalOperation or ILoopOperation)) return "select-command-write-unsupported";
                    if (write.Syntax.SpanStart > call.Syntax.SpanStart) return "select-command-after-fill";
                    if (Unwrap(write.Value) is not ILocalReferenceOperation value) return "select-command-local-required";
                    association = value;
                    associationKind = "select-command-property";
                    continue;
                }
                return property.Property.Name switch
                {
                    "TableMappings" => "adapter-table-mappings-unsupported",
                    "MissingSchemaAction" => "adapter-missing-schema-action-unsupported",
                    "MissingMappingAction" => "adapter-missing-mapping-action-unsupported",
                    "AcceptChangesDuringFill" => "adapter-accept-changes-during-fill-unsupported",
                    "ContinueUpdateOnError" => "adapter-continue-update-on-error-unsupported",
                    "FillLoadOption" => "adapter-fill-load-option-unsupported",
                    "ReturnProviderSpecificTypes" => "adapter-provider-specific-types-unsupported",
                    _ => "unsupported-adapter-member"
                };
            }
            if (parent is IEventReferenceOperation) return "unsupported-adapter-member";
            if (parent is ISimpleAssignmentOperation reassigned && reassigned.Target == reference) return "adapter-reassignment";
            return "adapter-alias-or-escape";
        }
        if (association is null) return "select-command-unbound";

        // Command: the #912 rules, cut off at the adapter association; any later mutation or other execution is unknown.
        var command = association.Local;
        var cutoff = association.Syntax.SpanStart;
        return ClassifyCommand(all, command, [command, adapter, .. excluded], cutoff, reference => reference == association,
            _ => "command-executed-outside-fill", afterCutoffGap: "command-mutation-after-adapter-association",
            out commandType, out textHash);
    }

    /// <summary>
    /// One use of a DataTable-valued expression (the filled table local, or a table selected from a filled DataSet): only
    /// <c>Rows</c> reads are collected into <paramref name="rows"/>; every other use returns its gap.
    /// </summary>
    internal static string? TableUse(IOperation reference, List<IPropertyReferenceOperation> rows, bool allowDispose)
    {
        var parent = reference.Parent;
        if (parent is IPropertyReferenceOperation property && property.Instance == reference)
        {
            if (property.Parent is ISimpleAssignmentOperation write && write.Target == property) return "datatable-mutation";
            if (property.Property.Name == "Rows" && Trusted(property.Property))
            {
                rows.Add(property);
                return null;
            }
            return SchemaMembers.Contains(property.Property.Name) ? "datatable-schema-access-unsupported"
                : RowOwnershipMembers.Contains(property.Property.Name) ? "ambiguous-row-ownership" : "unsupported-datatable-member";
        }
        if (parent is IInvocationOperation invocation && invocation.Instance == reference)
        {
            var name = invocation.TargetMethod.Name;
            if (allowDispose && name == "Dispose" && invocation.TargetMethod.Parameters.Length == 0 && invocation.Parent is IExpressionStatementOperation) return null;
            // VB represents reduced extension calls (AsEnumerable, CopyToDataTable sources) with the receiver as Instance.
            return Extension(invocation.TargetMethod, "System.Data.DataTableExtensions") || RowOwnershipMembers.Contains(name) ? "ambiguous-row-ownership"
                : TableMutations.Contains(name) ? "datatable-mutation" : "unsupported-datatable-member";
        }
        if (parent is IArgumentOperation { Parent: IInvocationOperation other })
        {
            var target = other.TargetMethod;
            return target.Name == "Fill" && DatabaseOperationApi.TryClassify(target, out _, out _) ? "repeated-fill"
                : target.Name == "FillSchema" ? "fill-schema-unsupported"
                : Extension(target, "System.Data.DataTableExtensions") ? "ambiguous-row-ownership"
                : "datatable-alias-or-escape";
        }
        if (parent is ISimpleAssignmentOperation reassigned && reassigned.Target == reference) return "datatable-reassignment";
        return "datatable-alias-or-escape";
    }

    /// <summary>Rows: constant row index, guard Count or guard foreach collection only; then every DataRow use records an access.</summary>
    internal static string? RowsAndAccesses(IOperation[] all, List<IPropertyReferenceOperation> rows, HashSet<IOperation> guardRows,
        List<ILocalSymbol> foreachRows, List<(int Position, SqlServerDataTableAccess Access)> accesses)
    {
        var rowLocals = new Dictionary<ILocalSymbol, (string Source, int? Index)>(SymbolEqualityComparer.Default);
        foreach (var row in foreachRows) rowLocals[row] = ("foreach-row", null);
        foreach (var property in rows)
        {
            if (guardRows.Contains(property)) continue;
            var parent = property.Parent;
            if (parent is IPropertyReferenceOperation { Property.IsIndexer: true } indexer && indexer.Instance == property && indexer.Arguments.Length == 1)
            {
                if (indexer.Arguments[0].Value.ConstantValue is not { HasValue: true, Value: int index }) return "dynamic-row-index-unsupported";
                if (index < 0) return "invalid-row-index";
                // A row local is only ever its direct initializer; a later write changes which row it names.
                if (indexer.Parent is ISimpleAssignmentOperation { Target: ILocalReferenceOperation } rowWrite && rowWrite.Value == indexer) return "row-reassignment";
                if (indexer.Parent is IVariableInitializerOperation initializer)
                {
                    var declared = initializer.Parent as IVariableDeclaratorOperation
                        ?? (initializer.Parent is IVariableDeclarationOperation { Declarators.Length: 1 } declaration ? declaration.Declarators[0] : null);
                    if (declared?.Symbol is not { RefKind: RefKind.None } local || !IsType(local.Type, "System.Data.DataRow")) return "row-alias-or-escape";
                    rowLocals[local] = ("row-local", index);
                    continue;
                }
                if (RowUse(indexer, "rows-index", index, accesses) is { } direct) return direct;
                continue;
            }
            if (parent is IInvocationOperation invocation && invocation.Instance == property)
                return invocation.TargetMethod.Name is "Add" or "Remove" or "RemoveAt" or "InsertAt" or "Clear" ? "datatable-mutation" : "unsupported-rows-member";
            if (parent is IPropertyReferenceOperation other && other.Instance == property) return "unsupported-rows-member";
            return "rows-alias-or-escape";
        }
        foreach (var reference in all.OfType<ILocalReferenceOperation>().Where(r => rowLocals.ContainsKey(r.Local)))
        {
            if (reference.Parent is IForEachLoopOperation loop && loop.NextVariables.Contains(reference)) continue;
            var (source, index) = rowLocals[reference.Local];
            if (RowUse(reference, source, index, accesses) is { } gap) return gap;
        }
        return null;
    }

    /// <summary>Classifies one use of a DataRow-valued expression; returns a whole-state gap or null after recording an access.</summary>
    private static string? RowUse(IOperation row, string source, int? index, List<(int, SqlServerDataTableAccess)> accesses)
    {
        var parent = row.Parent;
        if (parent is IPropertyReferenceOperation { Property.IsIndexer: true } indexer && indexer.Instance == row)
        {
            if (indexer.Parent is ISimpleAssignmentOperation write && write.Target == indexer) return "datatable-mutation";
            if (!Trusted(indexer.Property)) return "unsupported-row-member";
            var parameters = indexer.Property.Parameters;
            if (parameters.Any(p => IsType(p.Type, "System.Data.DataColumn"))) return "datacolumn-access-unsupported";
            if (parameters.Length != 1) return "row-version-access-unsupported";
            return Record(parameters[0].Type.SpecialType, "Item", indexer.Arguments[0].Value, indexer, "name-indexer", "ordinal-indexer");
        }
        if (parent is IInvocationOperation invocation && invocation.Instance == row)
        {
            var target = invocation.TargetMethod;
            // VB represents reduced extension calls with the receiver as Instance.
            if (Extension(target, "System.Data.DataRowExtensions")) return target.Name == "SetField" ? "datatable-mutation" : "datarow-field-extension-unsupported";
            if (target.Name != "IsNull" || !DatabaseOperationApi.Trusted(target.ContainingAssembly)) return "unsupported-row-member";
            if (target.Parameters.Any(p => IsType(p.Type, "System.Data.DataColumn"))) return "datacolumn-access-unsupported";
            if (target.Parameters.Length != 1) return "row-version-access-unsupported";
            return Record(target.Parameters[0].Type.SpecialType, "IsNull", invocation.Arguments[0].Value, invocation, "isnull-name", "isnull-ordinal");
        }
        if (parent is IArgumentOperation { Parent: IInvocationOperation { TargetMethod: var extension } } && Extension(extension, "System.Data.DataRowExtensions"))
            return extension.Name == "SetField" ? "datatable-mutation" : "datarow-field-extension-unsupported";
        if (parent is IPropertyReferenceOperation other && other.Instance == row) return "unsupported-row-member";
        if (row is ILocalReferenceOperation && parent is ISimpleAssignmentOperation reassigned && reassigned.Target == row) return "row-reassignment";
        return "row-alias-or-escape";

        string? Record(SpecialType type, string member, IOperation argument, IOperation site, string nameKind, string ordinalKind)
        {
            var (line, end) = Lines(site);
            if (type == SpecialType.System_String)
            {
                var access = argument.ConstantValue is not { HasValue: true, Value: string name } ? new SqlServerDataTableAccess(nameKind, member, source, index, null, null, line, end, "dynamic-column-name")
                    // Only the bounded identifier vocabulary is retained; arbitrary constant text is not copied into facts.
                    : SqlServerSchemaEvidence.Identifier(name) ? new(nameKind, member, source, index, name, null, line, end, null)
                    : new(nameKind, member, source, index, null, null, line, end, "column-name-outside-identifier-vocabulary");
                accesses.Add((site.Syntax.SpanStart, access));
                return null;
            }
            if (type != SpecialType.System_Int32) return "unsupported-row-member";
            accesses.Add((site.Syntax.SpanStart, argument.ConstantValue is { HasValue: true, Value: int ordinal }
                ? new(ordinalKind, member, source, index, null, ordinal, line, end, null)
                : new(ordinalKind, member, source, index, null, null, line, end, "dynamic-ordinal")));
            return null;
        }
    }

    /// <summary>The selected table's Rows property when <paramref name="operation"/> is exactly that collection.</summary>
    private static IPropertyReferenceOperation? RowsOf(IOperation? operation, Func<IOperation?, bool> selected)
        => Unwrap(operation) is IPropertyReferenceOperation { Property.Name: "Rows" } rows && selected(rows.Instance) && Trusted(rows.Property) ? rows : null;

    /// <summary><c>selected.Rows.Count &gt; 0</c> on the selected table only; returns its Rows reference.</summary>
    private static IPropertyReferenceOperation? CountGuard(IOperation? condition, Func<IOperation?, bool> selected)
        => Unwrap(condition) is IBinaryOperation { OperatorKind: BinaryOperatorKind.GreaterThan, OperatorMethod: null, IsLifted: false } comparison
            && comparison.RightOperand.ConstantValue is { HasValue: true, Value: 0 }
            && Unwrap(comparison.LeftOperand) is IPropertyReferenceOperation { Property.Name: "Count" } count && Trusted(count.Property)
            && RowsOf(count.Instance, selected) is { } rows && count.Instance == rows ? rows : null;

    internal static bool References(IOperation value, ILocalSymbol local) => Walk(value).Any(o => o is ILocalReferenceOperation l && Same(l.Local, local));
    internal static IEnumerable<IOperation> Ancestors(IOperation operation)
    {
        for (var ancestor = operation.Parent; ancestor is not null; ancestor = ancestor.Parent) yield return ancestor;
    }
    // Classified by declaring type only, never trusted as evidence: DataSetExtensions is outside the trusted assembly set.
    internal static bool Extension(IMethodSymbol method, string type)
        => (method.IsExtensionMethod || method.ReducedFrom is not null) && (method.ReducedFrom ?? method).ContainingType.ToDisplayString() == type;
    internal static bool Trusted(IPropertySymbol property) => DatabaseOperationApi.Trusted(property.ContainingAssembly);
    internal static bool IsType(ITypeSymbol? type, string name) => type is not null && type.ToDisplayString() == name && DatabaseOperationApi.Trusted(type.ContainingAssembly);
}
