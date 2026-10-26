using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using static TraceMap.Core.SqlServerDataSetConsumers;
using static TraceMap.Core.SqlServerDataTableConsumers;
using static TraceMap.Core.SqlServerReaderColumns;

namespace TraceMap.Core;

/// <summary>One call site of a bounded helper and the consumer of its returned DataTable/DataSet local.</summary>
/// <param name="Caller">Display string of the calling member (the ordinary member containing any lambda).</param>
/// <param name="CommandTextHash">SHA-256 of this call's constant procedure argument; the text itself is not retained.</param>
/// <param name="Selectors">DataSet helpers only: sorted subset of table-local, tables-index and tables-name.</param>
/// <param name="Guards">Sorted subset of foreach, rows-count and (DataSet) tables-count, bound to this caller's local only.</param>
/// <param name="Accesses">The Phase 2 DataRow access records read through rows of the returned table (DataSet: first table).</param>
public sealed record SqlServerHelperCaller(int Line, int EndLine, string? Caller, string Coverage, string? Gap, string? CommandTextHash,
    int? ResultLine, IReadOnlyList<string> Selectors, IReadOnlyList<string> Guards, IReadOnlyList<SqlServerDataTableAccess> Accesses);
/// <param name="ResultKind">datatable or dataset.</param>
/// <param name="CommandTextSource">constructor-parameter or command-text-parameter: the command text is exactly the procedure parameter.</param>
/// <param name="FillOverload">fill-datatable, fill-dataset or fill-dataset-source-table.</param>
/// <param name="TableName">DataSet helpers only: the first-result table name a fresh adapter with default mappings creates.</param>
/// <param name="Callers">Every invocation of the helper in its document, in source order; one entry per call site.</param>
public sealed record SqlServerHelperResultState(string Version, string Coverage, string? Gap, string? ResultKind, string? CommandType,
    string? CommandTextSource, string? AdapterAssociation, string? FillOverload, string? TableName, int? HelperLine, int? AdapterLine,
    int? ResultLine, IReadOnlyList<SqlServerHelperCaller> Callers);

/// <summary>Shared helper analysis before projection into the Phase 4 or internal-helper state; Path is null in Phase 4 mode.</summary>
internal sealed record SqlServerHelperAnalysis(string? Gap, string? ResultKind, string? CommandType, string? CommandTextSource, string? AdapterAssociation,
    string? FillOverload, string? TableName, int? HelperLine, int? AdapterLine, int? ResultLine, IReadOnlyList<(string? Path, SqlServerHelperCaller Caller)> Callers);

/// <summary>
/// Independent fail-closed helper-boundary state for one Fill invocation: exactly one level, a non-virtual, non-generic
/// <c>private</c> static helper in a single-declaration type whose body is one Phase 2 (<c>Fill(DataTable)</c>) or Phase 3
/// (<c>Fill(DataSet[, constant])</c>) shape with the command text taken from one string parameter, returning the filled local.
/// Every call site in the helper's document becomes one caller entry with its constant procedure-argument hash and the Phase
/// 2/3 consumer rules applied to the fresh local it initializes. Sibling of the DataTable, DataSet, reader and parameter states,
/// which are unchanged. It never claims that the helper or Fill executes, that the enumerated call sites are all runtime
/// callers (reflection is never claimed), that tables or rows exist, or anything about later results, types or values.
/// </summary>
internal static class SqlServerHelperResults
{
    internal const string Version = "sqlserver-helper-result/2";
    internal const int MaxCallers = 64;
    private const string DataTableType = "System.Data.DataTable";
    private const string DataSetType = "System.Data.DataSet";
    private static readonly string[] ConnectionTypes = ["System.Data.SqlClient.SqlConnection", "Microsoft.Data.SqlClient.SqlConnection"];

    internal static void Add(SortedDictionary<string, string> properties, IInvocationOperation call)
        => properties["sqlServerHelperResultState"] = SqlServerSchemaEvidence.Json(Extract(call));

    private static SqlServerHelperAnalysis Unknown(string gap) => new(gap, null, null, null, null, null, null, null, null, null, []);

    /// <summary>The Phase 4 private same-document state; unchanged by the internal-helper widening.</summary>
    internal static SqlServerHelperResultState Extract(IInvocationOperation call)
    {
        var analysis = Analyze(call, internalMode: false);
        return analysis.Gap is { } gap ? new(Version, "unknown", gap, null, null, null, null, null, null, null, null, null, [])
            : new(Version, "bounded", null, analysis.ResultKind, analysis.CommandType, analysis.CommandTextSource, analysis.AdapterAssociation,
                analysis.FillOverload, analysis.TableName, analysis.HelperLine, analysis.AdapterLine, analysis.ResultLine,
                analysis.Callers.Select(c => c.Caller).ToArray());
    }

    /// <summary>
    /// Shared helper analysis. <paramref name="internalMode"/> false is Phase 4 (private helper, call sites in its document);
    /// true admits exactly an internal / Friend helper whose call sites are enumerated across the registered compilation scope,
    /// with every other helper-body, signature and caller rule unchanged.
    /// </summary>
    internal static SqlServerHelperAnalysis Analyze(IInvocationOperation call, bool internalMode)
    {
        var method = call.TargetMethod;
        var declaring = method.ContainingType.ToDisplayString();
        string? overload = null;
        if (method.Name == "Fill" && DatabaseOperationApi.Trusted(method.ContainingAssembly) && method.Parameters.Length is 1 or 2)
        {
            if (method.Parameters.Length == 1 && IsType(method.Parameters[0].Type, DataTableType) && declaring == "System.Data.Common.DbDataAdapter") overload = "fill-datatable";
            else if (IsType(method.Parameters[0].Type, DataSetType))
                overload = method.Parameters.Length == 1 && declaring is "System.Data.Common.DataAdapter" or "System.Data.Common.DbDataAdapter" ? "fill-dataset"
                    : method.Parameters.Length == 2 && method.Parameters[1].Type.SpecialType == SpecialType.System_String && declaring == "System.Data.Common.DbDataAdapter"
                        ? "fill-dataset-source-table" : null;
        }
        if (overload is null) return Unknown("fill-overload-unsupported");
        var table = overload == "fill-datatable";
        var resultType = table ? DataTableType : DataSetType;
        string? tableName = table ? null : "Table";
        if (overload == "fill-dataset-source-table")
        {
            if (Argument(call, 1)?.ConstantValue is not { HasValue: true } source) return Unknown("dynamic-source-table-unsupported");
            if (source.Value is not string { Length: > 0 } name) return Unknown("invalid-source-table");
            if (!SqlServerSchemaEvidence.Identifier(name)) return Unknown("source-table-outside-identifier-vocabulary");
            tableName = name;
        }
        if (call.SemanticModel is not { } model || model.GetEnclosingSymbol(call.Syntax.SpanStart) is not IMethodSymbol { MethodKind: MethodKind.Ordinary } helper)
            return Unknown("helper-method-unsupported");
        if (!FillStatement(call)) return Unknown("fill-result-use-unsupported");
        if (FillAdapter(call) is not { } adapter) return Unknown("fresh-sqldataadapter-local-required");
        if (Local(Argument(call, 0)) is not { } result || !IsType(result.Type, resultType))
            return Unknown(table ? "fresh-datatable-local-required" : "fresh-dataset-local-required");
        if (MethodOperations(call) is not { } all) return Unknown("operation-limit");
        // Only explicit returns count; VB adds an implicit return of the function value at End Function.
        var returns = all.OfType<IReturnOperation>().Where(r => !r.IsImplicit).ToArray();
        if (!returns.Any(r => Same(Local(r.ReturnedValue), result))) return Unknown("helper-return-required");

        // Symbol: one non-virtual, non-generic private static method with a unique name in single-declaration types.
        var types = ContainingTypes(helper).ToArray();
        if (helper.IsVirtual || helper.IsAbstract || helper.IsOverride || helper.ContainingType.TypeKind == TypeKind.Interface
            || helper.ExplicitInterfaceImplementations.Length > 0) return Unknown("helper-virtual-or-interface-unsupported");
        if (!helper.IsStatic) return Unknown("helper-static-required");
        if (helper.IsGenericMethod || types.Any(t => t.IsGenericType)) return Unknown("helper-generic-unsupported");
        if (helper.IsAsync || helper.IsIterator) return Unknown("helper-async-unsupported");
        if (helper.IsExtensionMethod) return Unknown("helper-extension-unsupported");
        if (!internalMode)
        {
            // Only private helpers have every call site inside their single type declaration (one document, one compilation).
            if (helper.DeclaredAccessibility != Accessibility.Private) return Unknown("helper-accessibility-unsupported");
        }
        else
        {
            // Private helpers stay Phase 4 evidence; only internal / Friend is enumerated across the compilation.
            if (helper.DeclaredAccessibility == Accessibility.Private) return Unknown("helper-private-phase4-scope");
            if (helper.DeclaredAccessibility != Accessibility.Internal) return Unknown("helper-accessibility-unsupported");
            // Other compilations may call internal members only through InternalsVisibleTo (or reflection, never claimed).
            if (helper.ContainingAssembly.GetAttributes().Any(a => a.AttributeClass?.ToDisplayString() == "System.Runtime.CompilerServices.InternalsVisibleToAttribute"))
                return Unknown("helper-internals-visible-to-unsupported");
        }
        if (helper.PartialDefinitionPart is not null || helper.PartialImplementationPart is not null || helper.DeclaringSyntaxReferences.Length != 1
            || types.Any(t => t.DeclaringSyntaxReferences.Length != 1)) return Unknown("helper-partial-type-unsupported");
        if (helper.ContainingType.GetMembers(helper.Name).Length != 1) return Unknown("helper-overload-ambiguous");
        IParameterSymbol? procedure = null, connection = null;
        foreach (var parameter in helper.Parameters)
        {
            if (parameter.RefKind != RefKind.None || parameter.IsOptional || parameter.IsParams || parameter.HasExplicitDefaultValue || helper.Parameters.Length > 2)
                return Unknown("helper-parameter-unsupported");
            if (parameter.Type.SpecialType == SpecialType.System_String && procedure is null) procedure = parameter;
            else if (connection is null && ConnectionTypes.Contains(parameter.Type.ToDisplayString(), StringComparer.Ordinal)
                && DatabaseOperationApi.Trusted(parameter.Type.ContainingAssembly)) connection = parameter;
            else return Unknown("helper-parameter-unsupported");
        }
        if (procedure is null) return Unknown("helper-parameter-unsupported");
        if (!IsType(helper.ReturnType, resultType)) return Unknown("helper-return-type-unsupported");
        SqlServerCompilationScope? scope = null;
        string? referenceGap;
        List<IInvocationOperation> callers;
        if (internalMode)
        {
            scope = SqlServerCompilationScopes.Find(model.Compilation);
            if (ScopeGap(scope, model) is { } scopeGap) return Unknown(scopeGap);
            (referenceGap, callers) = CompilationReferences(model, helper, scope!);
        }
        else (referenceGap, callers) = References(model, helper);
        if (referenceGap is not null) return Unknown(referenceGap);

        // Body: the Phase 2/3 rules with no guard, read or other call; the filled local's only other use is the one return.
        if (returns.Length != 1) return Unknown("helper-multiple-returns");
        if (Flow(all, _ => false, null, [], [], new SortedSet<string>(StringComparer.Ordinal)) is { } flowGap) return Unknown(flowGap);
        if (AdapterCreation(all, adapter, out var adapterDeclarator, out var adapterCreation) is { } adapterGap) return Unknown(adapterGap);
        var resultDeclarator = all.OfType<IVariableDeclaratorOperation>().SingleOrDefault(d => Same(d.Symbol, result));
        var creation = Unwrap(resultDeclarator is null ? null : Initializer(resultDeclarator)) as IObjectCreationOperation;
        if (creation?.Type is null || !IsType(creation.Type, resultType)) return Unknown(table ? "fresh-datatable-required" : "fresh-dataset-required");
        if (creation.Initializer is not null) return Unknown(table ? "datatable-initializer-unsupported" : "dataset-initializer-unsupported");
        if (creation.Arguments.Length != 0) return Unknown(table ? "datatable-constructor-unsupported" : "dataset-constructor-unsupported");
        // A returned result disposed at scope exit is outside the subset.
        if (Disposed(resultDeclarator!)) return Unknown("helper-result-disposed");
        if (ImplicitReceiver(all)) return Unknown("implicit-receiver-unsupported");
        if (AdapterAndCommand(all, call, adapter, result, adapterCreation, [result], out var association, out var commandType, out _) is { } commandGap)
            return Unknown(commandGap);
        var parameterReferences = all.OfType<IParameterReferenceOperation>().ToArray();
        if (parameterReferences.Any(Written)) return Unknown("helper-parameter-mutation");
        if (CommandTextSource(all, adapter, adapterCreation, parameterReferences.Where(r => Same(r.Parameter, procedure)).ToArray()) is not { } textSource)
            return Unknown("helper-command-text-parameter-required");
        if (connection is not null && !ConnectionUse(parameterReferences.Where(r => Same(r.Parameter, connection)).ToArray())) return Unknown("helper-body-unsupported");
        // Assembly trust establishes identity, not permission for unrelated executions or allocations.
        var commandLocal = Local(adapterCreation.Arguments.FirstOrDefault()?.Value)
            ?? all.OfType<ISimpleAssignmentOperation>()
                .Where(w => w.Target is IPropertyReferenceOperation p && p.Property.Name == "SelectCommand" && Same(Local(p.Instance), adapter))
                .Select(w => Local(w.Value)).SingleOrDefault();
        var commandCreation = all.OfType<IVariableDeclaratorOperation>().Where(d => Same(d.Symbol, commandLocal))
            .Select(d => Unwrap(Initializer(d))).OfType<IObjectCreationOperation>().SingleOrDefault();
        var unsupportedBody = all.Any(o => o is IInvocationOperation i && i != call
                && !(i.TargetMethod.Name == "Dispose" && i.Arguments.Length == 0
                    && (Same(Local(i.Instance), adapter) || Same(Local(i.Instance), commandLocal)))
            || o is IObjectCreationOperation c && c != adapterCreation && c != creation && c != commandCreation);
        var rows = new List<IPropertyReferenceOperation>();
        var consumed = false;
        var fillEnd = call.Syntax.Span.End;
        foreach (var reference in all.OfType<ILocalReferenceOperation>().Where(r => Same(r.Local, result)))
        {
            if (reference.Parent is IArgumentOperation argument && argument.Parent == call) continue;
            if (reference.Parent == returns[0])
            {
                if (reference.Syntax.SpanStart < fillEnd) return Unknown(table ? "table-use-before-fill" : "dataset-use-before-fill");
                continue;
            }
            if ((table ? TableUse(reference, rows, allowDispose: false) : DataSetUse(reference, rows, [], [])) is { } useGap) return Unknown(useGap);
            consumed = true;
        }
        // Any remaining use (a row read, a selected table, Dispose) is a consumer inside the helper, outside this boundary;
        // row-level mutations keep their Phase 2 gap.
        if (rows.Count > 0) return Unknown(RowsAndAccesses(all, rows, [], [], []) ?? "helper-body-unsupported");
        if (consumed) return Unknown("helper-body-unsupported");
        if (unsupportedBody) return Unknown("helper-body-unsupported");

        // Phase 4: source order in the one document. Internal: (repo-relative caller path, source order) across documents.
        var ordered = scope is null ? callers.OrderBy(c => c.Syntax.SpanStart).Select(c => (Path: (string?)null, Call: c)).ToArray()
            : callers.Select(c => (Path: scope.Document(c.Syntax.SyntaxTree).Path, Call: c))
                .OrderBy(c => c.Path, StringComparer.Ordinal).ThenBy(c => c.Call.Syntax.SpanStart).ToArray();
        var entries = ordered.Select(c => (c.Path, Caller(c.Call, procedure, connection, resultType, tableName))).ToArray();
        return new(null, table ? "datatable" : "dataset", commandType, textSource, association, overload, tableName,
            Line(helper.DeclaringSyntaxReferences[0].GetSyntax()), Line(adapterDeclarator.Syntax), Line(resultDeclarator!.Syntax), entries);
    }

    /// <summary>The compilation must have a registered, complete, error-free, bounded scope, and the helper an ordinary document.</summary>
    private static string? ScopeGap(SqlServerCompilationScope? scope, SemanticModel model)
    {
        if (scope is null) return "helper-compilation-scope-unavailable";
        if (scope.Reduced) return "helper-compilation-reduced";
        var trees = model.Compilation.SyntaxTrees.ToArray();
        if (trees.Length > SqlServerCompilationScope.MaxTrees || trees.Sum(t => (long)t.Length) > SqlServerCompilationScope.MaxCharacters)
            return "helper-compilation-limit";
        if (scope.Document(model.SyntaxTree).Gap is { } documentGap) return documentGap;
        return scope.HasErrors ? "helper-compilation-errors" : null;
    }

    /// <summary>
    /// Every reference to an internal helper in every document of the compilation, in path order. A reference in a generated,
    /// linked or path-unsafe document makes the whole helper unknown with that document's gap; recursion and escapes win.
    /// </summary>
    private static (string? Gap, List<IInvocationOperation> Callers) CompilationReferences(SemanticModel model, IMethodSymbol helper, SqlServerCompilationScope scope)
    {
        var callers = new List<IInvocationOperation>();
        bool recursion = false, escape = false;
        string? documentGap = null;
        foreach (var tree in model.Compilation.SyntaxTrees.OrderBy(t => t.FilePath, StringComparer.Ordinal))
        {
            // Decode identifiers before filtering: C# Unicode escapes can spell a reference without the raw helper name.
            // VB names are case-insensitive and the semantic symbol check stays exact.
            if (!tree.GetRoot().DescendantTokens().Any(t => string.Equals(t.ValueText, helper.Name, StringComparison.OrdinalIgnoreCase))) continue;
            var treeModel = tree == model.SyntaxTree ? model : model.Compilation.GetSemanticModel(tree, ignoreAccessibility: true);
            if (ScanReferences(treeModel, helper, callers, ref recursion, ref escape, out var found)) return ("helper-caller-limit", []);
            if (found && documentGap is null) documentGap = scope.Document(tree).Gap;
        }
        return (recursion ? "helper-recursion" : escape ? "helper-reference-escape" : documentGap, callers);
    }

    /// <summary>
    /// Every reference to the helper in its document: invocations become callers; a reference inside the helper is recursion and
    /// any other reference (method group, delegate, AddressOf, nameof) escapes the bounded call-site set.
    /// </summary>
    private static (string? Gap, List<IInvocationOperation> Callers) References(SemanticModel model, IMethodSymbol helper)
    {
        var callers = new List<IInvocationOperation>();
        bool recursion = false, escape = false;
        if (ScanReferences(model, helper, callers, ref recursion, ref escape, out _)) return ("helper-caller-limit", []);
        return (recursion ? "helper-recursion" : escape ? "helper-reference-escape" : callers.Count > MaxCallers ? "helper-caller-limit" : null, callers);
    }

    /// <summary>Scans one tree for helper references; returns true when a unique invocation beyond <see cref="MaxCallers"/> is found.</summary>
    private static bool ScanReferences(SemanticModel model, IMethodSymbol helper, List<IInvocationOperation> callers, ref bool recursion, ref bool escape, out bool found)
    {
        found = false;
        var seen = new HashSet<Microsoft.CodeAnalysis.Text.TextSpan>();
        foreach (var token in model.SyntaxTree.GetRoot().DescendantTokens())
        {
            // VB names are case-insensitive; the symbol check below is exact either way.
            if (!string.Equals(token.ValueText, helper.Name, StringComparison.OrdinalIgnoreCase) || token.Parent is not { } node) continue;
            if (Same(model.GetDeclaredSymbol(node), helper)) continue;
            // nameof and other method-group uses bind only as candidates; any binding to the helper is a reference.
            var info = model.GetSymbolInfo(node);
            if (!(info.Symbol is { } symbol ? [symbol] : info.CandidateSymbols).Any(s => Same(s.OriginalDefinition, helper))) continue;
            found = true;
            for (var enclosing = model.GetEnclosingSymbol(node.SpanStart); enclosing is not null; enclosing = enclosing.ContainingSymbol)
                if (Same(enclosing, helper)) recursion = true;
            IInvocationOperation? invocation = null;
            SyntaxNode? ancestor = node;
            for (var depth = 0; ancestor is not null && depth < 6 && invocation is null; depth++, ancestor = ancestor.Parent)
                if (model.GetOperation(ancestor) is IInvocationOperation candidate && Same(candidate.TargetMethod.OriginalDefinition, helper)
                    && !candidate.Arguments.Any(a => a.Syntax.Span.Contains(node.Span)))
                    invocation = candidate;
            if (invocation is null) escape = true;
            else if (seen.Add(invocation.Syntax.Span))
            {
                if (callers.Count == MaxCallers) return true;
                callers.Add(invocation);
            }
        }
        return false;
    }

    /// <summary>One caller entry: constant positional procedure argument, a fresh exact local, then the Phase 2/3 consumer rules on it.</summary>
    private static SqlServerHelperCaller Caller(IInvocationOperation call, IParameterSymbol procedure, IParameterSymbol? connection, string resultType, string? tableName)
    {
        var (line, end) = Lines(call);
        ISymbol? member = call.SemanticModel?.GetEnclosingSymbol(call.Syntax.SpanStart);
        while (member is IMethodSymbol { MethodKind: MethodKind.AnonymousFunction or MethodKind.LocalFunction }) member = member.ContainingSymbol;
        var callerName = member?.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
        SqlServerHelperCaller Unknown(string gap) => new(line, end, callerName, "unknown", gap, null, null, [], [], []);

        if (call.Arguments.Any(Named)) return Unknown("helper-named-argument-unsupported");
        var procedureArgument = call.Arguments.Single(a => Same(a.Parameter?.OriginalDefinition, procedure));
        if (procedureArgument.Value.ConstantValue is not { HasValue: true } constant) return Unknown("helper-procedure-argument-nonconstant");
        if (constant.Value is not string { Length: > 0 } text) return Unknown("helper-procedure-argument-invalid");
        if (connection is not null && Unwrap(call.Arguments.Single(a => Same(a.Parameter?.OriginalDefinition, connection)).Value)
            is not (IParameterReferenceOperation or ILocalReferenceOperation)) return Unknown("helper-connection-argument-unsupported");
        if (call.Parent is not IVariableInitializerOperation initializer || Declared(initializer) is not { RefKind: RefKind.None } local || !IsType(local.Type, resultType))
            return Unknown("helper-result-local-required");
        if (MethodOperations(call) is not { } all) return Unknown("operation-limit");
        var hash = FactFactory.Hash(text, 64);
        var declarator = all.OfType<IVariableDeclaratorOperation>().First(d => Same(d.Symbol, local));

        // Phase 3 selectors first (DataSet), then method-wide flow bound to this caller's local only.
        var selectors = new HashSet<IOperation>();
        var selectorKinds = new SortedSet<string>(StringComparer.Ordinal);
        var tableLocals = new List<ILocalSymbol>();
        if (tableName is not null)
        {
            foreach (var indexer in all.OfType<IPropertyReferenceOperation>().Where(p => p.Property.IsIndexer && TablesOf(p.Instance, local) is not null))
            {
                if (SelectorGap(indexer, tableName, out var kind) is { } selectorGap) return Unknown(selectorGap);
                selectors.Add(indexer);
                selectorKinds.Add(kind);
            }
            foreach (var selector in selectors)
                if (selector.Parent is IVariableInitializerOperation selected && Declared(selected) is { RefKind: RefKind.None } tableLocal && IsType(tableLocal.Type, DataTableType))
                {
                    tableLocals.Add(tableLocal);
                    selectorKinds.Add("table-local");
                }
        }
        bool Selected(IOperation? operation) => tableName is null ? Same(Local(operation), local)
            : Unwrap(operation) is { } unwrapped && (selectors.Contains(unwrapped) || unwrapped is ILocalReferenceOperation reference && tableLocals.Any(t => Same(t, reference.Local)));
        var guarded = new HashSet<IOperation>();
        var foreachRows = new List<ILocalSymbol>();
        var guards = new SortedSet<string>(StringComparer.Ordinal);
        if (Flow(all, Selected, tableName is null ? null : condition => TablesCountGuard(condition, local), guarded, foreachRows, guards, "tables-count") is { } flowGap)
            return Unknown(flowGap);
        if (ImplicitReceiver(all)) return Unknown("implicit-receiver-unsupported");
        var rows = new List<IPropertyReferenceOperation>();
        foreach (var reference in all.OfType<ILocalReferenceOperation>().Where(r => Same(r.Local, local)))
            if ((tableName is null ? TableUse(reference, rows, allowDispose: true) : DataSetUse(reference, rows, guarded, tableLocals)) is { } useGap)
                return Unknown(useGap);
        foreach (var reference in all.OfType<ILocalReferenceOperation>().Where(r => tableLocals.Any(t => Same(t, r.Local))))
            if (TableUse(reference, rows, allowDispose: false) is { } tableGap) return Unknown(tableGap);
        var accesses = new List<(int Position, SqlServerDataTableAccess Access)>();
        if (RowsAndAccesses(all, rows, guarded, foreachRows, accesses) is { } rowGap) return Unknown(rowGap);
        if (accesses.Count > SqlServerDataTableConsumers.MaxAccesses) return Unknown("access-limit");
        return new(line, end, callerName, "bounded", null, hash, Line(declarator.Syntax), selectorKinds.ToArray(), guards.ToArray(),
            accesses.OrderBy(a => a.Position).Select(a => a.Access).ToArray());
    }

    /// <summary>The command text is exactly the procedure parameter: the associated command's cmdText argument or its one CommandText write.</summary>
    private static string? CommandTextSource(IOperation[] all, ILocalSymbol adapter, IObjectCreationOperation adapterCreation, IParameterReferenceOperation[] references)
    {
        if (references.Length != 1) return null;
        var commandValue = adapterCreation.Arguments.Length == 1 ? adapterCreation.Arguments[0].Value
            : all.OfType<ISimpleAssignmentOperation>().FirstOrDefault(a => a.Target is IPropertyReferenceOperation { Property.Name: "SelectCommand" } p && Same(Local(p.Instance), adapter))?.Value;
        if (Local(commandValue) is not { } command) return null;
        var commandCreation = Unwrap(all.OfType<IVariableDeclaratorOperation>().Where(d => Same(d.Symbol, command)).Select(Initializer).SingleOrDefault()) as IObjectCreationOperation;
        var text = commandCreation?.Arguments.FirstOrDefault(a => a.Parameter?.Name is "cmdText" or "commandText");
        var writes = all.OfType<ISimpleAssignmentOperation>().Where(a => a.Target is IPropertyReferenceOperation { Property.Name: "CommandText" } p && Same(Local(p.Instance), command)).ToArray();
        if (text is not null && writes.Length == 0 && Unwrap(text.Value) == references[0]) return "constructor-parameter";
        if (text is null && writes.Length == 1 && Unwrap(writes[0].Value) == references[0]) return "command-text-parameter";
        return null;
    }

    /// <summary>The connection parameter is used once, only as the command's connection (constructor argument or Connection write).</summary>
    private static bool ConnectionUse(IParameterReferenceOperation[] references)
    {
        if (references.Length != 1) return false;
        IOperation use = references[0];
        while (use.Parent is IConversionOperation { OperatorMethod: null } conversion) use = conversion;
        if (use.Parent is IArgumentOperation { Parameter.Name: "connection", Parent: IObjectCreationOperation { Type: { } type } })
            return CommandTypes.Contains(type.ToDisplayString(), StringComparer.Ordinal);
        return use.Parent is ISimpleAssignmentOperation { Target: IPropertyReferenceOperation { Property.Name: "Connection" } target } write && write.Value == use
            && Local(target.Instance) is { } command && CommandTypes.Contains(command.Type.ToDisplayString(), StringComparer.Ordinal);
    }

    private static bool Written(IParameterReferenceOperation reference)
        => reference.Parent is ISimpleAssignmentOperation assignment && assignment.Target == reference
            || reference.Parent is ICompoundAssignmentOperation compound && compound.Target == reference
            || reference.Parent is IIncrementOrDecrementOperation
            || reference.Parent is IArgumentOperation { Parameter.RefKind: not RefKind.None };

    /// <summary>The declarator is a resource of a using statement or declaration (C# <c>using var</c>, VB <c>Using</c>).</summary>
    private static bool Disposed(IVariableDeclaratorOperation declarator)
    {
        IOperation? group = declarator;
        while (group is not null and not IVariableDeclarationGroupOperation) group = group.Parent;
        return group?.Parent is IUsingDeclarationOperation || group?.Parent is IUsingOperation { Resources: var resources } && resources == group;
    }

    private static bool Named(IArgumentOperation argument)
        => argument.Syntax is Microsoft.CodeAnalysis.CSharp.Syntax.ArgumentSyntax { NameColon: not null }
            || argument.Syntax is Microsoft.CodeAnalysis.VisualBasic.Syntax.SimpleArgumentSyntax { IsNamed: true };

    private static IEnumerable<INamedTypeSymbol> ContainingTypes(ISymbol symbol)
    {
        for (var type = symbol.ContainingType; type is not null; type = type.ContainingType) yield return type;
    }

    private static int Line(SyntaxNode node) => node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
}
