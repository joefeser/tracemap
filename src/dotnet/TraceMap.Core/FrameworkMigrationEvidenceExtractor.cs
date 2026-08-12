using System.Globalization;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace TraceMap.Core;

internal static class FrameworkMigrationEvidenceExtractor
{
    private const string EfRelationalAssembly = "Microsoft.EntityFrameworkCore.Relational";
    private const string EfPublicKeyToken = "adb9793829ddae60";
    private const string MigrationMetadataName = "Microsoft.EntityFrameworkCore.Migrations.Migration";
    private const string BuilderMetadataName = "Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder";
    private const string DeclarationLimitations = "Static framework migration declaration only; execution, ordering, provider selection, generated SQL, database state, rollback, and safety are not proven.";
    private const string OperationLimitations = "Static framework migration operation candidate only; execution, ordering, provider selection, generated SQL, database state, rollback, reversibility, and safety are not proven.";
    private const string GapLimitations = "Static framework migration coverage is reduced; omitted protected content and runtime behavior were not analyzed.";

    private static readonly SymbolDisplayFormat DisplayFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        memberOptions: SymbolDisplayMemberOptions.IncludeContainingType
            | SymbolDisplayMemberOptions.IncludeParameters
            | SymbolDisplayMemberOptions.IncludeExplicitInterface,
        parameterOptions: SymbolDisplayParameterOptions.IncludeType
            | SymbolDisplayParameterOptions.IncludeName
            | SymbolDisplayParameterOptions.IncludeParamsRefOut
            | SymbolDisplayParameterOptions.IncludeDefaultValue,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers
            | SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    private sealed record OperationContract(
        string Kind,
        string ObjectKind,
        IReadOnlyDictionary<string, string> Required,
        IReadOnlyDictionary<string, string> Optional,
        string? ArrayParameter = null,
        string? ArrayProperty = null,
        string? SecondaryArrayParameter = null,
        string? SecondaryArrayProperty = null);

    private sealed record GapKey(
        string MigrationScope,
        string GapKind,
        string SourceScope,
        string OperationKind,
        string Direction);

    private sealed class GapAccumulator(
        TypeDeclarationSyntax declaration,
        INamedTypeSymbol? migrationType,
        IMethodSymbol? sourceMethod,
        string? operationKind,
        string? direction)
    {
        public TypeDeclarationSyntax Declaration { get; } = declaration;
        public INamedTypeSymbol? MigrationType { get; } = migrationType;
        public IMethodSymbol? SourceMethod { get; } = sourceMethod;
        public string? OperationKind { get; } = operationKind;
        public string? Direction { get; } = direction;
        public int Count { get; set; }
    }

    private static readonly IReadOnlyDictionary<string, OperationContract> Operations =
        new Dictionary<string, OperationContract>(StringComparer.Ordinal)
        {
            ["CreateTable"] = new("create-table", "table", Required("name", "tableName"), Optional("schema", "schemaName")),
            ["AddColumn"] = new("add-column", "column", Required("name", "columnName", "table", "tableName"), Optional("schema", "schemaName")),
            ["AlterColumn"] = new("alter-column", "column", Required("name", "columnName", "table", "tableName"), Optional("schema", "schemaName")),
            ["DropTable"] = new("drop-table", "table", Required("name", "tableName"), Optional("schema", "schemaName")),
            ["DropColumn"] = new("drop-column", "column", Required("name", "columnName", "table", "tableName"), Optional("schema", "schemaName")),
            ["RenameTable"] = new("rename-table", "table", Required("name", "tableName", "newName", "newTableName"), Optional("schema", "schemaName", "newSchema", "newSchemaName")),
            ["RenameColumn"] = new("rename-column", "column", Required("name", "columnName", "newName", "newColumnName", "table", "tableName"), Optional("schema", "schemaName")),
            ["CreateIndex"] = new("create-index", "index", Required("name", "indexName", "table", "tableName"), Optional("schema", "schemaName"), "columns", "columnNames"),
            ["DropIndex"] = new("drop-index", "index", Required("name", "indexName"), Optional("table", "tableName", "schema", "schemaName")),
            ["AddForeignKey"] = new("add-foreign-key", "foreign-key", Required("name", "constraintName", "table", "tableName", "principalTable", "principalTableName"), Optional("schema", "schemaName", "principalSchema", "principalSchemaName"), "columns", "columnNames", "principalColumns", "principalColumnNames"),
            ["DropForeignKey"] = new("drop-foreign-key", "foreign-key", Required("name", "constraintName", "table", "tableName"), Optional("schema", "schemaName"))
        };

    private static readonly IReadOnlyDictionary<string, string> ProtectedOperations =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Sql"] = "RawSqlMigrationOperationUnavailable",
            ["InsertData"] = "DataMigrationOperationUnavailable",
            ["UpdateData"] = "DataMigrationOperationUnavailable",
            ["DeleteData"] = "DataMigrationOperationUnavailable",
            ["Operation"] = "UnsupportedMigrationOperation"
        };

    private static readonly HashSet<string> AllowedGapKinds = new(StringComparer.Ordinal)
    {
        "FrameworkAssemblyIdentityUnavailable",
        "SemanticBindingUnavailable",
        "MigrationDirectionUnavailable",
        "DynamicIdentityUnavailable",
        "MissingRequiredIdentity",
        "NestedTableShapeUnavailable",
        "IndexColumnShapeUnavailable",
        "ForeignKeyColumnShapeUnavailable",
        "RawSqlMigrationOperationUnavailable",
        "DataMigrationOperationUnavailable",
        "AnnotationMigrationOperationUnavailable",
        "DefaultOrComputedExpressionUnavailable",
        "UnsupportedMigrationOperation"
    };

    internal static void Extract(
        string? projectPath,
        string filePath,
        SyntaxNode root,
        SemanticModel model,
        List<SemanticFactCandidate> facts,
        List<SemanticFactCandidate> gaps,
        List<ProtectedSourceSpan>? protectedSourceSpans = null)
    {
        var admitted = new Dictionary<INamedTypeSymbol, TypeDeclarationSyntax>(SymbolEqualityComparer.Default);
        var gapCounts = new Dictionary<GapKey, GapAccumulator>();

        foreach (var declaration in root.DescendantNodes().OfType<TypeDeclarationSyntax>())
        {
            var symbol = model.GetDeclaredSymbol(declaration) as INamedTypeSymbol;
            var candidateBase = FindMigrationBase(symbol?.BaseType);
            var syntaxCandidate = declaration.BaseList?.Types.Any(type =>
                type.Type.ToString().EndsWith("Migration", StringComparison.Ordinal)) == true;
            if (candidateBase is null)
            {
                if (syntaxCandidate)
                {
                    AddGap(gapCounts, declaration, null, null, "SemanticBindingUnavailable", null, null);
                }
                continue;
            }

            if (!IsTrustedEfSymbol(candidateBase))
            {
                AddGap(gapCounts, declaration, null, null, "FrameworkAssemblyIdentityUnavailable", null, null);
                continue;
            }

            admitted[symbol!] = declaration;
            facts.Add(CreateDeclaration(projectPath, filePath, declaration, symbol!));
        }

        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>().OrderBy(node => node.SpanStart))
        {
            var sourceMethod = model.GetEnclosingSymbol(invocation.SpanStart) as IMethodSymbol;
            var migrationType = sourceMethod?.ContainingType;
            if (migrationType is null || !TryGetAdmittedDeclaration(admitted, migrationType, out var declaration))
            {
                continue;
            }

            var admittedSourceMethod = sourceMethod!;
            var direction = GetDirection(admittedSourceMethod);
            var methodName = InvocationName(invocation);
            var targetMethod = model.GetSymbolInfo(invocation).Symbol as IMethodSymbol;
            if (targetMethod is null)
            {
                if (Operations.TryGetValue(methodName, out var unresolvedContract))
                {
                    AddGap(gapCounts, declaration, migrationType, sourceMethod, "SemanticBindingUnavailable", unresolvedContract.Kind, direction);
                }
                else if (ProtectedOperations.TryGetValue(methodName, out var protectedGap))
                {
                    AddGap(gapCounts, declaration, migrationType, sourceMethod, protectedGap, null, direction);
                }
                continue;
            }

            if (methodName == "Annotation"
                && IsTrustedOperationBuilder(targetMethod)
                && IsAdmittedAnnotationReceiver(invocation, model))
            {
                protectedSourceSpans?.Add(new ProtectedSourceSpan(filePath, invocation.SpanStart, invocation.Span.Length));
                AddGap(gapCounts, declaration, migrationType, sourceMethod, "AnnotationMigrationOperationUnavailable", null, direction);
                continue;
            }

            if (!IsMigrationBuilderMethod(targetMethod))
            {
                continue;
            }

            if (direction == "unknown")
            {
                AddGap(gapCounts, declaration, migrationType, sourceMethod, "MigrationDirectionUnavailable", Operations.GetValueOrDefault(methodName)?.Kind, direction);
            }

            if (ProtectedOperations.TryGetValue(methodName, out var gapKind))
            {
                protectedSourceSpans?.Add(new ProtectedSourceSpan(filePath, invocation.SpanStart, invocation.Span.Length));
                AddGap(gapCounts, declaration, migrationType, sourceMethod, gapKind, null, direction);
                continue;
            }

            if (!Operations.TryGetValue(methodName, out var contract))
            {
                AddGap(gapCounts, declaration, migrationType, sourceMethod, "UnsupportedMigrationOperation", null, direction);
                continue;
            }

            var properties = CommonOperationProperties(migrationType, admittedSourceMethod, targetMethod, contract, direction, InvocationOrdinal(root, model, admittedSourceMethod, invocation));
            var missingRequired = false;
            foreach (var pair in contract.Required)
            {
                if (TryGetStringArgument(invocation, targetMethod, pair.Key, model, out var value))
                {
                    properties[pair.Value] = value;
                }
                else
                {
                    missingRequired = true;
                    AddGap(gapCounts, declaration, migrationType, sourceMethod,
                        HasArgument(invocation, targetMethod, pair.Key) ? "DynamicIdentityUnavailable" : "MissingRequiredIdentity",
                        contract.Kind, direction);
                }
            }

            foreach (var pair in contract.Optional)
            {
                if (TryGetStringArgument(invocation, targetMethod, pair.Key, model, out var value))
                {
                    properties[pair.Value] = value;
                }
                else if (HasArgument(invocation, targetMethod, pair.Key))
                {
                    AddGap(gapCounts, declaration, migrationType, sourceMethod, "DynamicIdentityUnavailable", contract.Kind, direction);
                }
            }

            if (contract.ArrayParameter is not null)
            {
                AddArrayIdentity(invocation, targetMethod, contract.ArrayParameter, contract.ArrayProperty!, model, properties,
                    contract.ObjectKind == "index" ? "IndexColumnShapeUnavailable" : "ForeignKeyColumnShapeUnavailable",
                    gapCounts, declaration, migrationType, admittedSourceMethod, contract.Kind, direction);
            }
            if (contract.SecondaryArrayParameter is not null)
            {
                AddArrayIdentity(invocation, targetMethod, contract.SecondaryArrayParameter, contract.SecondaryArrayProperty!, model, properties,
                    "ForeignKeyColumnShapeUnavailable", gapCounts, declaration, migrationType, admittedSourceMethod, contract.Kind, direction);
            }

            if (methodName == "CreateTable")
            {
                protectedSourceSpans?.Add(new ProtectedSourceSpan(filePath, invocation.SpanStart, invocation.Span.Length));
                AddGap(gapCounts, declaration, migrationType, sourceMethod, "NestedTableShapeUnavailable", contract.Kind, direction);
            }
            if (methodName is "AddColumn" or "AlterColumn" && HasProtectedDefaultArgument(invocation, targetMethod))
            {
                protectedSourceSpans?.Add(new ProtectedSourceSpan(filePath, invocation.SpanStart, invocation.Span.Length));
                AddGap(gapCounts, declaration, migrationType, sourceMethod, "DefaultOrComputedExpressionUnavailable", contract.Kind, direction);
            }

            if (!missingRequired)
            {
                facts.Add(CreateOperation(projectPath, filePath, invocation, admittedSourceMethod, targetMethod, contract.Kind, properties));
            }
        }

        foreach (var pair in gapCounts.OrderBy(item => item.Key.MigrationScope, StringComparer.Ordinal)
            .ThenBy(item => item.Key.GapKind, StringComparer.Ordinal)
            .ThenBy(item => item.Key.SourceScope, StringComparer.Ordinal)
            .ThenBy(item => item.Key.OperationKind, StringComparer.Ordinal)
            .ThenBy(item => item.Key.Direction, StringComparer.Ordinal))
        {
            gaps.Add(CreateGapFact(projectPath, filePath, pair.Key.GapKind, pair.Value));
        }
    }

    private static SemanticFactCandidate CreateDeclaration(string? projectPath, string filePath, TypeDeclarationSyntax declaration, INamedTypeSymbol type)
    {
        var identity = CSharpSymbolIdentityProvider.TryCreate(type)!;
        return new SemanticFactCandidate(
            FactTypes.FrameworkMigrationDeclared,
            RuleIds.DatabaseFrameworkMigrationDeclaration,
            EvidenceTiers.Tier1Semantic,
            Evidence(filePath, declaration),
            projectPath,
            TargetSymbol: type.ToDisplayString(DisplayFormat),
            ContractElement: "framework-migration",
            Properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["coverageLabel"] = "bounded-static-migration",
                ["declarationKind"] = "framework-migration",
                ["frameworkFamily"] = "ef-core",
                ["limitations"] = DeclarationLimitations,
                ["migrationTypeName"] = type.ToDisplayString(DisplayFormat),
                ["providerScope"] = "unknown",
                ["targetAssemblyIdentity"] = AssemblyIdentity(type.ContainingAssembly),
                ["targetSymbolId"] = identity.SymbolId,
                ["targetSymbolKind"] = "NamedType"
            });
    }

    private static SemanticFactCandidate CreateOperation(
        string? projectPath,
        string filePath,
        InvocationExpressionSyntax invocation,
        IMethodSymbol source,
        IMethodSymbol target,
        string operationKind,
        IReadOnlyDictionary<string, string> properties) =>
        new(
            FactTypes.FrameworkMigrationOperationCandidate,
            RuleIds.DatabaseFrameworkMigrationOperation,
            EvidenceTiers.Tier1Semantic,
            Evidence(filePath, invocation),
            projectPath,
            source.ToDisplayString(DisplayFormat),
            target.ToDisplayString(DisplayFormat),
            operationKind,
            properties);

    private static SemanticFactCandidate CreateGapFact(string? projectPath, string filePath, string gapKind, GapAccumulator gap)
    {
        var properties = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["coverageLabel"] = "reduced-static-migration",
            ["frameworkFamily"] = "ef-core",
            ["gapKind"] = gapKind,
            ["limitations"] = GapLimitations,
            ["occurrenceCount"] = gap.Count.ToString(CultureInfo.InvariantCulture),
            ["providerScope"] = "unknown"
        };
        if (gap.MigrationType is not null)
        {
            properties["migrationTypeSymbolId"] = CSharpSymbolIdentityProvider.TryCreate(gap.MigrationType)!.SymbolId;
        }
        if (gap.SourceMethod is not null)
        {
            properties["sourceAssemblyIdentity"] = AssemblyIdentity(gap.SourceMethod.ContainingAssembly);
            properties["sourceSymbolId"] = CSharpSymbolIdentityProvider.TryCreate(gap.SourceMethod)!.SymbolId;
            properties["sourceSymbolKind"] = "Method";
        }
        if (gap.OperationKind is not null)
        {
            properties["operationKind"] = gap.OperationKind;
        }
        if (gap.Direction is not null)
        {
            properties["direction"] = gap.Direction;
        }

        return new SemanticFactCandidate(
            FactTypes.AnalysisGap,
            RuleIds.DatabaseFrameworkMigrationGap,
            EvidenceTiers.Tier4Unknown,
            Evidence(filePath, gap.Declaration, snippetHash: null),
            projectPath,
            SourceSymbol: gap.SourceMethod?.ToDisplayString(DisplayFormat),
            ContractElement: gapKind,
            Properties: properties);
    }

    private static SortedDictionary<string, string> CommonOperationProperties(
        INamedTypeSymbol migrationType,
        IMethodSymbol source,
        IMethodSymbol target,
        OperationContract contract,
        string direction,
        int ordinal)
    {
        var sourceIdentity = CSharpSymbolIdentityProvider.TryCreate(source)!;
        var targetIdentity = CSharpSymbolIdentityProvider.TryCreate(target)!;
        return new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["coverageLabel"] = "bounded-static-migration",
            ["direction"] = direction,
            ["frameworkFamily"] = "ef-core",
            ["invocationOrdinal"] = ordinal.ToString(CultureInfo.InvariantCulture),
            ["limitations"] = OperationLimitations,
            ["migrationTypeSymbolId"] = CSharpSymbolIdentityProvider.TryCreate(migrationType)!.SymbolId,
            ["objectKind"] = contract.ObjectKind,
            ["operationKind"] = contract.Kind,
            ["providerScope"] = "unknown",
            ["sourceAssemblyIdentity"] = AssemblyIdentity(source.ContainingAssembly),
            ["sourceSymbolId"] = sourceIdentity.SymbolId,
            ["sourceSymbolKind"] = "Method",
            ["targetAssemblyIdentity"] = AssemblyIdentity(target.ContainingAssembly),
            ["targetSymbolId"] = targetIdentity.SymbolId,
            ["targetSymbolKind"] = "Method"
        };
    }

    private static void AddArrayIdentity(
        InvocationExpressionSyntax invocation,
        IMethodSymbol method,
        string parameter,
        string property,
        SemanticModel model,
        SortedDictionary<string, string> properties,
        string gapKind,
        Dictionary<GapKey, GapAccumulator> gaps,
        TypeDeclarationSyntax declaration,
        INamedTypeSymbol migrationType,
        IMethodSymbol source,
        string operationKind,
        string direction)
    {
        var argument = FindArgument(invocation, method, parameter)
            ?? (parameter.EndsWith('s') ? FindArgument(invocation, method, parameter[..^1]) : null);
        if (argument is null)
        {
            return;
        }
        if (TryGetStringArray(argument.Expression, model, out var values))
        {
            properties[property] = JsonSerializer.Serialize(values);
            return;
        }
        AddGap(gaps, declaration, migrationType, source, gapKind, operationKind, direction);
    }

    private static bool TryGetStringArray(ExpressionSyntax expression, SemanticModel model, out string[] values)
    {
        if (model.GetConstantValue(expression) is { HasValue: true, Value: string scalar })
        {
            values = [scalar];
            return true;
        }

        var initializers = expression switch
        {
            ArrayCreationExpressionSyntax array => array.Initializer?.Expressions,
            ImplicitArrayCreationExpressionSyntax array => array.Initializer.Expressions,
            CollectionExpressionSyntax collection => new SeparatedSyntaxList<ExpressionSyntax>().AddRange(
                collection.Elements.OfType<ExpressionElementSyntax>().Select(item => item.Expression)),
            _ => null
        };
        if (initializers is null)
        {
            values = [];
            return false;
        }

        var result = new List<string>();
        foreach (var item in initializers.Value)
        {
            if (model.GetConstantValue(item) is not { HasValue: true, Value: string value })
            {
                values = [];
                return false;
            }
            result.Add(value);
        }
        values = result.ToArray();
        return values.Length > 0;
    }

    private static bool TryGetStringArgument(InvocationExpressionSyntax invocation, IMethodSymbol method, string parameter, SemanticModel model, out string value)
    {
        var argument = FindArgument(invocation, method, parameter);
        if (argument is not null && model.GetConstantValue(argument.Expression) is { HasValue: true, Value: string constant })
        {
            value = constant;
            return true;
        }
        value = string.Empty;
        return false;
    }

    private static bool HasArgument(InvocationExpressionSyntax invocation, IMethodSymbol method, string parameter) =>
        FindArgument(invocation, method, parameter) is not null;

    private static ArgumentSyntax? FindArgument(InvocationExpressionSyntax invocation, IMethodSymbol method, string parameter)
    {
        var named = invocation.ArgumentList.Arguments.FirstOrDefault(argument =>
            argument.NameColon?.Name.Identifier.ValueText.Equals(parameter, StringComparison.Ordinal) == true);
        if (named is not null)
        {
            return named;
        }
        var parameterIndex = method.Parameters.IndexOf(method.Parameters.FirstOrDefault(item => item.Name.Equals(parameter, StringComparison.Ordinal))!);
        return parameterIndex >= 0
            && parameterIndex < invocation.ArgumentList.Arguments.Count
            && invocation.ArgumentList.Arguments[parameterIndex].NameColon is null
            ? invocation.ArgumentList.Arguments[parameterIndex]
            : null;
    }

    private static bool HasProtectedDefaultArgument(InvocationExpressionSyntax invocation, IMethodSymbol method) =>
        new[] { "defaultValue", "defaultValueSql", "computedColumnSql" }.Any(name => HasArgument(invocation, method, name));

    private static int InvocationOrdinal(SyntaxNode root, SemanticModel model, IMethodSymbol source, InvocationExpressionSyntax current) =>
        root.DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Where(invocation => SymbolEqualityComparer.Default.Equals(model.GetEnclosingSymbol(invocation.SpanStart), source))
            .OrderBy(invocation => invocation.SpanStart)
            .TakeWhile(invocation => invocation != current)
            .Count() + 1;

    private static string GetDirection(IMethodSymbol method)
    {
        if (method.MethodKind != MethodKind.Ordinary || !method.IsOverride || method.OverriddenMethod is not { } overridden
            || !IsTrustedEfSymbol(FindMigrationBase(overridden.ContainingType)))
        {
            return "unknown";
        }
        return overridden.Name switch { "Up" => "up", "Down" => "down", _ => "unknown" };
    }

    private static INamedTypeSymbol? FindMigrationBase(INamedTypeSymbol? type)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (MetadataName(current).Equals(MigrationMetadataName, StringComparison.Ordinal))
            {
                return current;
            }
        }
        return null;
    }

    private static bool IsMigrationBuilderMethod(IMethodSymbol method) =>
        MetadataName(method.ContainingType).Equals(BuilderMetadataName, StringComparison.Ordinal)
        && IsTrustedEfSymbol(method.ContainingType);

    private static bool IsTrustedOperationBuilder(IMethodSymbol method)
    {
        for (var current = method.ContainingType; current is not null; current = current.BaseType)
        {
            if (current.Name.Equals("OperationBuilder", StringComparison.Ordinal)
                && GetNamespace(current).Equals("Microsoft.EntityFrameworkCore.Migrations.Operations.Builders", StringComparison.Ordinal)
                && IsTrustedEfSymbol(current))
            {
                return true;
            }
        }
        return false;
    }

    private static bool IsAdmittedAnnotationReceiver(InvocationExpressionSyntax annotation, SemanticModel model)
    {
        ExpressionSyntax? receiver = (annotation.Expression as MemberAccessExpressionSyntax)?.Expression;
        while (receiver is InvocationExpressionSyntax invocation)
        {
            if (model.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method)
            {
                return false;
            }
            if (IsMigrationBuilderMethod(method) && Operations.ContainsKey(method.Name))
            {
                return true;
            }
            if (method.Name != "Annotation" || !IsTrustedOperationBuilder(method))
            {
                return false;
            }
            receiver = (invocation.Expression as MemberAccessExpressionSyntax)?.Expression;
        }
        return false;
    }

    private static bool IsTrustedEfSymbol(INamedTypeSymbol? type)
    {
        var assembly = type?.ContainingAssembly;
        return assembly is not null
            && assembly.Identity.Name.Equals(EfRelationalAssembly, StringComparison.Ordinal)
            && PublicKeyToken(assembly).Equals(EfPublicKeyToken, StringComparison.Ordinal)
            && assembly.Locations.All(location => !location.IsInSource);
    }

    private static string PublicKeyToken(IAssemblySymbol assembly) =>
        string.Concat(assembly.Identity.PublicKeyToken.Select(value => value.ToString("x2", CultureInfo.InvariantCulture)));

    private static string AssemblyIdentity(IAssemblySymbol? assembly) => assembly?.Identity.GetDisplayName() ?? "unknown";

    private static string MetadataName(INamedTypeSymbol? type) =>
        type is null ? string.Empty : string.IsNullOrEmpty(GetNamespace(type)) ? type.MetadataName : $"{GetNamespace(type)}.{type.MetadataName}";

    private static string GetNamespace(INamedTypeSymbol type) =>
        type.ContainingNamespace?.IsGlobalNamespace == false ? type.ContainingNamespace.ToDisplayString() : string.Empty;

    private static bool TryGetAdmittedDeclaration(
        Dictionary<INamedTypeSymbol, TypeDeclarationSyntax> admitted,
        INamedTypeSymbol type,
        out TypeDeclarationSyntax declaration)
    {
        foreach (var pair in admitted)
        {
            if (SymbolEqualityComparer.Default.Equals(pair.Key, type))
            {
                declaration = pair.Value;
                return true;
            }
        }
        declaration = null!;
        return false;
    }

    private static void AddGap(
        Dictionary<GapKey, GapAccumulator> gaps,
        TypeDeclarationSyntax declaration,
        INamedTypeSymbol? migrationType,
        IMethodSymbol? source,
        string gapKind,
        string? operationKind,
        string? direction)
    {
        if (!AllowedGapKinds.Contains(gapKind))
        {
            throw new InvalidOperationException($"Unsupported framework migration gap kind: {gapKind}");
        }

        var migrationScope = migrationType is null
            ? $"{declaration.SyntaxTree.FilePath}:{declaration.SpanStart}:{declaration.Span.Length}"
            : CSharpSymbolIdentityProvider.TryCreate(migrationType)!.SymbolId;
        var sourceScope = source is null ? "<none>" : CSharpSymbolIdentityProvider.TryCreate(source)!.SymbolId;
        var key = new GapKey(migrationScope, gapKind, sourceScope, operationKind ?? "<none>", direction ?? "<none>");
        if (!gaps.TryGetValue(key, out var accumulator))
        {
            accumulator = new GapAccumulator(declaration, migrationType, source, operationKind, direction);
            gaps[key] = accumulator;
        }
        accumulator.Count++;
    }

    private static EvidenceSpan Evidence(string filePath, SyntaxNode node, string? snippetHash = null)
    {
        var span = node.GetLocation().GetLineSpan();
        return new EvidenceSpan(
            FileInventory.NormalizeRelativePath(filePath),
            span.StartLinePosition.Line + 1,
            Math.Max(span.StartLinePosition.Line + 1, span.EndLinePosition.Line + 1),
            snippetHash is null ? null : FactFactory.Hash(node.ToString(), 32),
            "CSharpSemanticExtractor",
            ScannerVersions.CSharpSemanticExtractor);
    }

    private static string InvocationName(InvocationExpressionSyntax invocation) => invocation.Expression switch
    {
        MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
        IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
        GenericNameSyntax generic => generic.Identifier.ValueText,
        _ => string.Empty
    };

    private static IReadOnlyDictionary<string, string> Required(params string[] pairs) => Pairs(pairs);
    private static IReadOnlyDictionary<string, string> Optional(params string[] pairs) => Pairs(pairs);
    private static IReadOnlyDictionary<string, string> Pairs(string[] pairs)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < pairs.Length; index += 2)
        {
            result[pairs[index]] = pairs[index + 1];
        }
        return result;
    }
}
