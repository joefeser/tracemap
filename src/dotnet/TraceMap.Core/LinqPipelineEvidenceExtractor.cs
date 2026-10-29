using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using System.Globalization;

namespace TraceMap.Core;

/// <summary>Compiler-bound expression dependencies. Never executes callbacks or changes SQL admission.</summary>
internal static class LinqPipelineEvidenceExtractor
{
    internal const string Version = ScannerVersions.LinqPipelineEvidenceExtractor;
    internal const string Rule = RuleIds.DotNetSemanticLinqPipeline;
    internal const string Limitation = "Bounded compiler-resolved expression dependencies only; no callback execution, purity, enumeration, cardinality, equality/comparer behavior, row existence, conversion success, SQL Fill association, runtime values, serialization or impact proof. Source semantic evidence does not authorize compiled lifecycle delegates. Dynamic DataTable.Select predicates remain unsupported.";
    private const int MaxNodes = 20000, MaxSites = 128, MaxOperations = 512, MaxMappings = 16;
    internal sealed record Mapping(string TargetMemberSha256, string Origin, string? ColumnSha256, IReadOnlyList<string> Transforms);
    internal sealed record Summary(string Kind, string SourceKind, string SourceSymbolSha256, int? TableOrdinal,
        IReadOnlyList<string> Operators, IReadOnlyList<Mapping> Mappings, IReadOnlyList<string> Captures,
        IReadOnlyList<string> FrameworkIdentities, string Coverage = "bounded-expression-dependencies;execution-unproven");
    private sealed class Rejection(string reason) : Exception(reason);

    internal static void Extract(string? projectPath, string filePath, SyntaxNode root, SemanticModel model,
        List<SemanticFactCandidate> facts, List<SemanticFactCandidate> gaps)
    {
        var nodes = root.DescendantNodes().Take(MaxNodes + 1).ToArray();
        if (nodes.Length > MaxNodes) { Emit(root, null, "document-node-limit"); return; }
        var sites = 0;
        foreach (var node in nodes.Where(node => node is Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax
            or Microsoft.CodeAnalysis.VisualBasic.Syntax.InvocationExpressionSyntax))
        {
            if (model.GetOperation(node) is not IInvocationOperation call || !Candidate(call) || HasTerminalParent(call)) continue;
            if (++sites > MaxSites) { Emit(node, null, "document-pipeline-limit"); break; }
            try { Emit(node, Analyze(call), null); }
            catch (Rejection error) { Emit(node, null, error.Message); }
        }
        void Emit(SyntaxNode site, Summary? summary, string? gap)
        {
            var span = site.GetLocation().GetLineSpan();
            var properties = new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["schema"] = "linq-pipeline/1", ["coverage"] = summary?.Coverage ?? "unknown",
                ["limitation"] = Limitation, ["language"] = site.Language,
                ["extractorVersion"] = Version
            };
            if (summary is not null) properties["pipeline"] = SqlServerSchemaEvidence.Json(summary);
            if (gap is not null) properties["gapKind"] = "linq-pipeline-" + gap;
            (gap is null ? facts : gaps).Add(new(gap is null ? FactTypes.LinqPipelineCandidate : FactTypes.AnalysisGap,
                Rule, gap is null ? EvidenceTiers.Tier1Semantic : EvidenceTiers.Tier4Unknown,
                new EvidenceSpan(filePath, span.StartLinePosition.Line + 1, span.EndLinePosition.Line + 1, null,
                    nameof(LinqPipelineEvidenceExtractor), Version), projectPath,
                ContractElement: summary?.Kind ?? "linq-pipeline-gap", Properties: properties,
                SourceStart: site.SpanStart, SourceLength: site.Span.Length));
        }
    }

    private static bool Candidate(IInvocationOperation call)
    {
        var method = Definition(call);
        if (!EnumerableType(method.ContainingType)) return false;
        if (method.Name == "ToList" && Source(call) is IInvocationOperation { } select && Definition(select).Name == "Select") return true;
        var cursor = call;
        for (var depth = 0; depth < 4; depth++)
        {
            if (Definition(cursor).Name == "GroupBy") return true;
            if (Source(cursor) is not IInvocationOperation next) break;
            cursor = next;
        }
        return false;
    }
    private static bool HasTerminalParent(IInvocationOperation call)
    {
        IOperation? parent = call.Parent;
        while (parent is IConversionOperation or IParenthesizedOperation) parent = parent.Parent;
        if (parent is IArgumentOperation argument) parent = argument.Parent;
        return parent is IInvocationOperation outer && EnumerableType(Definition(outer).ContainingType)
            && Definition(outer).Name is "Select" or "Where" or "ToList" or "Count"
            && ReferenceEquals(Strip(Source(outer)), call);
    }
    private static IMethodSymbol Definition(IInvocationOperation call) => (call.TargetMethod.ReducedFrom ?? call.TargetMethod).OriginalDefinition;
    private static IOperation? Strip(IOperation? value)
    {
        while (value is IConversionOperation { IsImplicit: true } conversion || value is IParenthesizedOperation)
            value = value is IConversionOperation c ? c.Operand : ((IParenthesizedOperation)value).Operand;
        return value;
    }
    private static IOperation? Source(IInvocationOperation call) => Strip(call.Instance ?? call.Arguments.FirstOrDefault()?.Value);
    private static bool EnumerableType(INamedTypeSymbol type) => type.ToDisplayString() == "System.Linq.Enumerable" && Signed(type.ContainingAssembly, "System.Core", "System.Linq");
    private static bool Signed(IAssemblySymbol assembly, params string[] names)
    {
        if (!names.Contains(assembly.Identity.Name, StringComparer.Ordinal) || !string.IsNullOrEmpty(assembly.Identity.CultureName)) return false;
        var token = Convert.ToHexStringLower(assembly.Identity.PublicKeyToken.ToArray());
        var version = assembly.Identity.Version;
        var legacy = version == new System.Version(2, 0, 0, 0) || version == new System.Version(4, 0, 0, 0);
        var extensions = version == new System.Version(3, 5, 0, 0) || version == new System.Version(4, 0, 0, 0);
        var modern = version.Major is >= 4 and <= 10 && version.Minor == 0 && version.Build == 0 && version.Revision == 0;
        return assembly.Identity.Name switch
        {
            "System.Data" or "mscorlib" => token == "b77a5c561934e089" && legacy,
            "System.Core" or "System.Data.DataSetExtensions" => token == "b77a5c561934e089" && extensions,
            "System.Private.CoreLib" => token == "7cec85d7bea7798e" && modern,
            "System.Linq" or "System.Data.Common" or "System.Runtime" => token == "b03f5f7f11d50a3a" && modern,
            _ => false
        };
    }

    private static bool Data(ITypeSymbol? type, string name) => type?.ToDisplayString() == "System.Data." + name
        && Signed(type.ContainingAssembly, "System.Data", "System.Data.Common", "System.Data.DataSetExtensions");
    private static bool DataGeneric(INamedTypeSymbol type, string metadataName)
        => type.OriginalDefinition.MetadataName == metadataName && type.ContainingNamespace.ToDisplayString() == "System.Data"
            && Signed(type.ContainingAssembly, "System.Data.DataSetExtensions", "System.Data.Common");
    private static bool Core(ITypeSymbol type) => Signed(type.ContainingAssembly, "mscorlib", "System.Private.CoreLib", "System.Runtime");
    private static string HashSymbol(ISymbol symbol) => FactFactory.Hash(symbol.Kind + "|" + symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
        + "|" + string.Join(',', symbol.DeclaringSyntaxReferences.Select(reference => reference.Span.Start.ToString(CultureInfo.InvariantCulture))), 64);
    private static string Identity(IMethodSymbol method) => method.ContainingAssembly.Identity + "|" + method.OriginalDefinition.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    private static ISymbol? Referenced(IOperation? value) => Strip(value) switch
    {
        ILocalReferenceOperation local => local.Local, IParameterReferenceOperation parameter => parameter.Parameter,
        IFieldReferenceOperation field => field.Field, IPropertyReferenceOperation property => property.Property, _ => null
    };
    private static IEnumerable<IOperation> Walk(IOperation root)
    {
        var pending = new Stack<IOperation>(); pending.Push(root);
        while (pending.TryPop(out var item)) { yield return item; foreach (var child in item.ChildOperations.Reverse()) pending.Push(child); }
    }
    private static IInvocationOperation Require(IOperation? op, string name, int arity, bool callback, List<string> identities, bool rowSelect = false)
    {
        if (Strip(op) is not IInvocationOperation call) throw new Rejection("chain-shape");
        var method = Definition(call);
        var rowCollectionSelect = rowSelect && name == "Select" && method.ContainingType.ToDisplayString() == "System.Data.EnumerableRowCollectionExtensions"
            && Signed(method.ContainingAssembly, "System.Data.DataSetExtensions", "System.Data.Common");
        if ((!EnumerableType(method.ContainingType) && !rowCollectionSelect) || method.Name != name || method.Arity != arity || !method.IsExtensionMethod
            || method.Parameters.Length != (callback ? 2 : 1) || method.Parameters.Any(parameter => parameter.RefKind != RefKind.None)
            || method.Parameters[0].Type is not INamedTypeSymbol sequence)
            throw new Rejection("framework-overload-or-identity");
        if (rowCollectionSelect ? !DataGeneric(sequence, "EnumerableRowCollection`1")
            : sequence.OriginalDefinition.MetadataName != "IEnumerable`1" || sequence.ContainingNamespace.ToDisplayString() != "System.Collections.Generic" || !Core(sequence))
            throw new Rejection("framework-overload-or-identity");
        if (callback && (method.Parameters[1].Type is not INamedTypeSymbol function || function.Name != "Func" || function.Arity != 2
            || function.ContainingNamespace.ToDisplayString() != "System" || !Core(function))) throw new Rejection("indexed-or-nonfunc-callback");
        if (callback)
        {
            var callbackSignature = (INamedTypeSymbol)method.Parameters[1].Type;
            if (!SymbolEqualityComparer.Default.Equals(callbackSignature.TypeArguments[0], method.TypeParameters[0])
                || (name == "Where" ? callbackSignature.TypeArguments[1].SpecialType != SpecialType.System_Boolean
                    : !SymbolEqualityComparer.Default.Equals(callbackSignature.TypeArguments[1], method.TypeParameters[1])))
                throw new Rejection("callback-type-parameters");
        }
        identities.Add(Identity(method)); return call;
    }
    private static (IParameterSymbol Parameter, IOperation Value) Lambda(IInvocationOperation call)
    {
        var candidates = call.Arguments.Select(argument => Strip(argument.Value)).Select(value => value is IDelegateCreationOperation creation ? creation.Target : value).OfType<IAnonymousFunctionOperation>().ToArray();
        if (candidates.Length != 1 || candidates[0].Symbol.Parameters.Length != 1) throw new Rejection("inspected-single-parameter-lambda-required");
        var lambda = candidates[0]; var nodes = Walk(lambda.Body).Take(129).ToArray();
        var returns = nodes.OfType<IReturnOperation>().ToArray();
        if (nodes.Length > 128 || returns.Length != 1 || returns[0].ReturnedValue is not { } value
            || nodes.Any(node => node is ILoopOperation or IConditionalOperation or IAwaitOperation or IThrowOperation or ITryOperation
                or IIncrementOrDecrementOperation or ICompoundAssignmentOperation)) throw new Rejection("callback-body-shape");
        return (lambda.Symbol.Parameters[0], Strip(value)!);
    }
    private static bool Parameter(IOperation? value, IParameterSymbol parameter)
        => Strip(value) is IParameterReferenceOperation reference && SymbolEqualityComparer.Default.Equals(reference.Parameter, parameter);

    internal static Summary Analyze(IInvocationOperation terminal)
    {
        if (Walk(terminal).Take(MaxOperations + 1).Count() > MaxOperations) throw new Rejection("operation-limit");
        var identities = new List<string>();
        var operators = new List<string>();
        IOperation? current = terminal;
        if (Definition(terminal).Name == "ToList") { current = Source(Require(terminal, "ToList", 1, false, identities)); operators.Add("ToList"); }
        if (Strip(current) is IInvocationOperation where && Definition(where).Name == "Where")
            return Grouping(where, operators, identities);
        var select = Require(current, "Select", 2, true, identities, rowSelect: true);
        if (operators.Count != 1) throw new Rejection("projection-tolist-required");
        var source = Source(select) as IInvocationOperation ?? throw new Rejection("asenumerable-source-required");
        var asEnumerable = Definition(source);
        if (asEnumerable.Name != "AsEnumerable" || asEnumerable.Arity != 0 || asEnumerable.Parameters.Length != 1
            || asEnumerable.ContainingType.ToDisplayString() != "System.Data.DataTableExtensions"
            || !Signed(asEnumerable.ContainingAssembly, "System.Data.DataSetExtensions", "System.Data.Common")
            || !Data(asEnumerable.Parameters[0].Type, "DataTable")) throw new Rejection("datatable-asenumerable-identity");
        identities.Add(Identity(asEnumerable));
        var (row, projection) = Lambda(select);
        if (!Data(row.Type, "DataRow") || projection is not IObjectCreationOperation { Initializer: { } initializer } creation
            || creation.Arguments.Length != 0) throw new Rejection("row-object-initializer-required");
        var assignments = initializer.Initializers.OfType<ISimpleAssignmentOperation>().ToArray();
        if (assignments.Length == 0 || assignments.Length != initializer.Initializers.Length || assignments.Length > MaxMappings) throw new Rejection("mapping-shape-or-limit");
        var mappings = new List<Mapping>();
        foreach (var assignment in assignments)
        {
            ISymbol target = assignment.Target switch { IPropertyReferenceOperation property => property.Property, IFieldReferenceOperation field => field.Field, _ => throw new Rejection("projection-target") };
            var transforms = new List<string>();
            var column = Column(assignment.Value, row, transforms, identities);
            mappings.Add(new(HashSymbol(target), "datarow-name-indexer", FactFactory.Hash(column, 64), transforms));
        }
        var table = TableSource(Source(source));
        return new("datatable-row-projection", table.Kind, table.Hash, table.Ordinal, ["AsEnumerable", "Select", "ToList"], mappings, [], identities.Distinct(StringComparer.Ordinal).ToArray());
    }

    private static string Column(IOperation value, IParameterSymbol row, List<string> transforms, List<string> identities)
    {
        if (value is IConversionOperation conversion)
        {
            if (conversion.OperatorMethod is not null || conversion.Type is null || conversion.Type.SpecialType is not (SpecialType.System_Object or SpecialType.System_String or SpecialType.System_Int32)) throw new Rejection("projection-conversion");
            var column = Column(conversion.Operand, row, transforms, identities);
            if (!conversion.IsImplicit) transforms.Add("convert:" + conversion.Type.SpecialType); return column;
        }
        value = Strip(value)!;
        if (value is IInvocationOperation parse && parse.TargetMethod.Name == "Parse" && parse.TargetMethod.IsStatic
            && parse.TargetMethod.ContainingType.SpecialType == SpecialType.System_Int32 && Core(parse.TargetMethod.ContainingType)
            && parse.Arguments.Length == 1 && parse.TargetMethod.Parameters.Length == 1
            && parse.TargetMethod.Parameters[0].Type.SpecialType == SpecialType.System_String)
        {
            var column = Column(parse.Arguments[0].Value, row, transforms, identities);
            transforms.Add("parse:Int32"); identities.Add(Identity(parse.TargetMethod)); return column;
        }
        if (value is IInvocationOperation call && call.Arguments.Length == 0 && call.Instance is not null
            && Core(call.TargetMethod.ContainingType)
            && (call.TargetMethod.Name == "ToString" && call.TargetMethod.ContainingType.SpecialType is SpecialType.System_Object or SpecialType.System_String
                || call.TargetMethod.Name == "Trim" && call.TargetMethod.ContainingType.SpecialType == SpecialType.System_String))
        {
            var column = Column(call.Instance, row, transforms, identities); transforms.Add(call.TargetMethod.Name);
            identities.Add(Identity(call.TargetMethod)); return column;
        }
        if (value is not IPropertyReferenceOperation indexer || !Data(indexer.Property.ContainingType, "DataRow")
            || indexer.Property.Name != "Item" || indexer.Arguments.Length != 1 || !Parameter(indexer.Instance, row)
            || indexer.Property.Parameters.Length != 1 || indexer.Property.Parameters[0].Type.SpecialType != SpecialType.System_String
            || indexer.Arguments[0].Value.ConstantValue is not { HasValue: true, Value: string name } || name.Length > 256)
            throw new Rejection("constant-row-column-required");
        return name;
    }

    private static (string Kind, string Hash, int? Ordinal) TableSource(IOperation? value)
    {
        value = Strip(value);
        if (value?.Type is { } type && Data(type, "DataTable") && Referenced(value) is { } symbol
            && value is ILocalReferenceOperation or IParameterReferenceOperation or IFieldReferenceOperation)
            return ("datatable-symbol-reference;fill-unproven", HashSymbol(symbol), null);
        if (value is IPropertyReferenceOperation index && Data(index.Property.ContainingType, "DataTableCollection")
            && index.Property.Name == "Item" && index.Property.Parameters.Length == 1
            && index.Property.Parameters[0].Type.SpecialType == SpecialType.System_Int32
            && index.Arguments.Length == 1 && index.Arguments[0].Value.ConstantValue is { HasValue: true, Value: int ordinal }
            && ordinal is >= 0 and <= 15 && Strip(index.Instance) is IPropertyReferenceOperation tables
            && Data(tables.Property.ContainingType, "DataSet") && tables.Property.Name == "Tables"
            && Strip(tables.Instance) is IOperation receiver && Referenced(receiver) is { } dataset)
            return ("dataset-table-ordinal-reference;fill-unproven", HashSymbol(dataset), ordinal);
        throw new Rejection("table-source-unresolved");
    }

    private static Summary Grouping(IInvocationOperation where, List<string> terminal, List<string> identities)
    {
        where = Require(where, "Where", 1, true, identities);
        var select = Require(Source(where), "Select", 2, true, identities);
        var group = Require(Source(select), "GroupBy", 2, true, identities);
        var (element, key) = Lambda(group);
        if (!Parameter(key, element)) throw new Rejection("identity-group-key-required");
        var source = Source(group);
        if (source is not (ILocalReferenceOperation or IParameterReferenceOperation or IFieldReferenceOperation) || Referenced(source) is not { } input)
            throw new Rejection("object-collection-symbol-required");
        if (element.Type.SpecialType == SpecialType.None && element.Type.ToDisplayString() == "System.Data.DataRow") throw new Rejection("object-grouping-is-not-row-projection");
        var (grouping, projected) = Lambda(select);
        if (projected is not IAnonymousObjectCreationOperation anonymous || anonymous.Initializers.Length != 2) throw new Rejection("anonymous-key-count-projection-required");
        var mappings = new List<Mapping>(); IPropertySymbol? countProperty = null;
        foreach (var assignment in anonymous.Initializers.OfType<ISimpleAssignmentOperation>())
        {
            if (assignment.Target is not IPropertyReferenceOperation target) throw new Rejection("anonymous-target");
            var value = Strip(assignment.Value);
            if (value is IPropertyReferenceOperation keyRead && keyRead.Property.Name == "Key" && Parameter(keyRead.Instance, grouping)
                && keyRead.Property.ContainingType.OriginalDefinition.MetadataName == "IGrouping`2"
                && keyRead.Property.ContainingNamespace.ToDisplayString() == "System.Linq"
                && Signed(keyRead.Property.ContainingAssembly, "System.Core", "System.Linq"))
                mappings.Add(new(HashSymbol(target.Property), "group-key", null, []));
            else if (value is IInvocationOperation count)
            {
                count = Require(count, "Count", 1, false, identities);
                if (!Parameter(Source(count), grouping)) throw new Rejection("group-count-source");
                countProperty = target.Property; mappings.Add(new(HashSymbol(target.Property), "group-count", null, []));
            }
            else throw new Rejection("anonymous-key-count-value");
        }
        if (mappings.Count != 2 || mappings.Count(value => value.Origin == "group-key") != 1 || countProperty is null) throw new Rejection("anonymous-key-count-members");
        var (item, predicate) = Lambda(where);
        if (predicate is not IBinaryOperation { OperatorKind: BinaryOperatorKind.Equals, OperatorMethod: null } equality) throw new Rejection("count-equality-predicate-required");
        IOperation? capture = null;
        foreach (var pair in new[] { (equality.LeftOperand, equality.RightOperand), (equality.RightOperand, equality.LeftOperand) })
            if (Strip(pair.Item1) is IPropertyReferenceOperation count && Parameter(count.Instance, item)
                && SymbolEqualityComparer.Default.Equals(count.Property, countProperty)) capture = Strip(pair.Item2);
        if (capture?.Type?.SpecialType != SpecialType.System_Int32 || capture is not (ILocalReferenceOperation or IParameterReferenceOperation or IFieldReferenceOperation)
            || Referenced(capture) is not { } captured) throw new Rejection("external-scalar-count-reference-required");
        return new("object-group-count-filter", "object-collection-symbol-reference;enumeration-unproven", HashSymbol(input), null,
            new[] { "GroupBy", "Select", "Where" }.Concat(terminal.AsEnumerable().Reverse()).ToArray(), mappings,
            [HashSymbol(captured)], identities.Distinct(StringComparer.Ordinal).ToArray());
    }
}
