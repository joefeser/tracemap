using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;

namespace TraceMap.Core;

public static partial class VisualBasicSyntaxExtractor
{
    private static bool AddDatabaseParameterCandidate(ScanManifest manifest, List<CodeFact> facts,
        string filePath, CompilationUnitSyntax root, ObjectCreationExpressionSyntax creation, FactBudget budget)
    {
        var names = ResolveHttpTypeNames(root, creation.Type.ToString()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (names.Length != 1 || !new[] { "System.Data.SqlClient.SqlParameter", "Microsoft.Data.SqlClient.SqlParameter" }
            .Contains(names[0], StringComparer.OrdinalIgnoreCase)) return true;
        return AddParameterEvidence(manifest, facts, filePath, root, creation, creation.ArgumentList, names[0], "syntax-constructor-only", budget);
    }

    private static bool AddDatabaseParameterAddCandidate(ScanManifest manifest, List<CodeFact> facts,
        string filePath, CompilationUnitSyntax root, InvocationExpressionSyntax invocation, FactBudget budget)
    {
        if (invocation.Expression is not MemberAccessExpressionSyntax add
            || !add.Name.Identifier.ValueText.Equals("Add", StringComparison.OrdinalIgnoreCase)
            || add.Expression is not MemberAccessExpressionSyntax collection
            || !collection.Name.Identifier.ValueText.Equals("Parameters", StringComparison.OrdinalIgnoreCase)
            || collection.Expression is not IdentifierNameSyntax receiver
            || !TryResolveExplicitReceiverType(root, invocation, receiver.Identifier.ValueText, _ => true, out var type)) return true;
        var resolved = ResolveHttpTypeNames(root, type);
        var dot = type.IndexOf('.');
        if (dot > 0 && !type.StartsWith("Global.", StringComparison.OrdinalIgnoreCase))
            resolved = resolved.Concat(ResolveHttpTypeNames(root, type[..dot]).Select(prefix => prefix + type[dot..]));
        var names = resolved
            .Where(n => new[] { "System.Data.SqlClient.SqlCommand", "Microsoft.Data.SqlClient.SqlCommand" }.Contains(n, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (names.Length != 1) return true;
        return AddParameterEvidence(manifest, facts, filePath, root, invocation, invocation.ArgumentList,
            names[0], "syntax-parameter-collection-add", budget);
    }

    private static bool AddParameterEvidence(ScanManifest manifest, List<CodeFact> facts, string filePath,
        CompilationUnitSyntax root, SyntaxNode site, ArgumentListSyntax? argumentList, string type,
        string coverage, FactBudget budget)
    {
        var arguments = argumentList?.Arguments;
        var properties = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["syntaxOwnerType"] = type,
            ["parameterNameEvidence"] = "unavailable",
            ["parameterTypeEvidence"] = "unavailable",
            ["parameterDirectionEvidence"] = "unknown",
            ["commandAssociation"] = "unresolved",
            ["coverageLabel"] = coverage,
            ["ruleLimitations"] = "Syntax parameter candidate only; no compiler/provider binding, overload resolution, verified command instance identity, procedure association, database declaration, direction default or runtime value is inferred. Optional straight-line local association is syntax evidence only. Names and argument expressions are hashed; source review is required."
        };
        if (coverage == "syntax-constructor-only") properties["parameterType"] = type;
        if (coverage == "syntax-parameter-collection-add" && site is InvocationExpressionSyntax addCall)
            AddLocalCommandAssociation(root, addCall, properties);
        if (arguments is { Count: > 0 } args && args.Count <= 16)
        {
            properties["argumentCount"] = args.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
            for (var index = 0; index < args.Count; index++)
                properties[$"argument{index}Hash"] = FactFactory.Hash(args[index].ToString(), 64);
            // Do not resolve named/reordered overload arguments without semantic evidence.
            if (args.All(a => a is SimpleArgumentSyntax { NameColonEquals: null }))
            {
                var first = ((SimpleArgumentSyntax)args[0]).Expression;
                if (first is LiteralExpressionSyntax literal && literal.Token.Value is string name)
                {
                    properties["parameterNameEvidence"] = "hash-only-literal";
                    properties["parameterNameHash"] = FactFactory.Hash(name, 64);
                }
                if (args.Count > 1 && ((SimpleArgumentSyntax)args[1]).Expression is MemberAccessExpressionSyntax member
                    && member.Expression is not null
                    && ResolveHttpTypeNames(root, member.Expression.ToString()).Any(n => n.Equals("System.Data.SqlDbType", StringComparison.OrdinalIgnoreCase))
                    && Enum.TryParse<System.Data.SqlDbType>(member.Name.Identifier.ValueText, true, out var kind)
                    && Enum.IsDefined(kind))
                {
                    properties["parameterTypeEvidence"] = "syntax-enum-candidate";
                    properties["parameterTypeName"] = "SqlDbType." + kind;
                }
            }
        }
        return TryAddSyntaxFact(manifest, facts, FactTypes.DatabaseParameterCandidate,
            RuleIds.VisualBasicSyntaxDatabaseParameter, filePath, site, type, properties,
            budget, sourceSymbol: GetContainingMemberName(site));
    }

    private static void AddLocalCommandAssociation(CompilationUnitSyntax root, InvocationExpressionSyntax add,
        SortedDictionary<string, string> properties)
    {
        // Deliberately narrow: one method body, fresh local creation and an uninterrupted
        // sequence of literal/enum parameter additions, then execution.
        if (add.Expression is not MemberAccessExpressionSyntax { Expression: MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax receiver } }
            || add.Parent is not ExpressionStatementSyntax statement
            || statement.Parent is not MethodBlockSyntax method) return;
        var statements = method.Statements;
        if (statements.Count > 256) { properties["commandAssociationReason"] = "local-statement-limit"; return; }
        var at = statements.IndexOf(statement);
        var name = receiver.Identifier.ValueText;
        bool Same(ExpressionSyntax? expression) => expression is IdentifierNameSyntax id && id.Identifier.ValueText.Equals(name, StringComparison.OrdinalIgnoreCase);
        bool SafeArguments(InvocationExpressionSyntax call) => call.ArgumentList is { } list && list.Arguments.Count <= 16
            && list.Arguments.All(a => a is SimpleArgumentSyntax { NameColonEquals: null } simple
                && (simple.Expression is LiteralExpressionSyntax || simple.Expression is MemberAccessExpressionSyntax member
                    && member.Expression is not null && ResolveHttpTypeNames(root, member.Expression.ToString())
                        .Any(n => n.Equals("System.Data.SqlDbType", StringComparison.OrdinalIgnoreCase))));
        bool ParameterAdd(StatementSyntax candidate) => candidate is ExpressionStatementSyntax { Expression: InvocationExpressionSyntax call }
            && call.Expression is MemberAccessExpressionSyntax member && member.Name.Identifier.ValueText.Equals("Add", StringComparison.OrdinalIgnoreCase)
            && member.Expression is MemberAccessExpressionSyntax collection && collection.Name.Identifier.ValueText.Equals("Parameters", StringComparison.OrdinalIgnoreCase)
            && Same(collection.Expression) && SafeArguments(call);
        if (!ParameterAdd(statement)) return;
        LocalDeclarationStatementSyntax? declaration = null;
        var work = 0;
        for (var i = at - 1; i >= 0; i--)
        {
            if (++work > 64) { properties["commandAssociationReason"] = "local-window-limit"; return; }
            if (ParameterAdd(statements[i])) continue;
            if (statements[i] is LocalDeclarationStatementSyntax local && local.Declarators.Count == 1)
            {
                var variable = local.Declarators[0];
                var creation = variable.AsClause is AsNewClauseSyntax asNew ? asNew.NewExpression as ObjectCreationExpressionSyntax
                    : variable.Initializer?.Value as ObjectCreationExpressionSyntax;
                if (variable.Names.Count == 1 && variable.Names[0].Identifier.ValueText.Equals(name, StringComparison.OrdinalIgnoreCase)
                    && creation is not null && creation.Initializer is null
                    && (creation.ArgumentList is null || creation.ArgumentList.Arguments.Count == 0)
                    && ResolveHttpTypeNames(root, creation.Type.ToString())
                        .Where(n => n.Equals("System.Data.SqlClient.SqlCommand", StringComparison.OrdinalIgnoreCase)
                            || n.Equals("Microsoft.Data.SqlClient.SqlCommand", StringComparison.OrdinalIgnoreCase))
                        .Distinct(StringComparer.OrdinalIgnoreCase).SequenceEqual(new[] { properties["syntaxOwnerType"] }, StringComparer.OrdinalIgnoreCase)) declaration = local;
            }
            break;
        }
        if (declaration is null) { properties["commandAssociationReason"] = "fresh-local-not-established"; return; }
        for (var i = at + 1; i < statements.Count; i++)
        {
            if (++work > 64) { properties["commandAssociationReason"] = "local-window-limit"; return; }
            if (ParameterAdd(statements[i])) continue;
            if (statements[i] is ExpressionStatementSyntax { Expression: InvocationExpressionSyntax execute }
                && execute.Expression is MemberAccessExpressionSyntax member && Same(member.Expression)
                && new[] { "ExecuteScalar", "ExecuteNonQuery", "ExecuteReader", "ExecuteXmlReader" }.Contains(member.Name.Identifier.ValueText, StringComparer.OrdinalIgnoreCase)
                && (execute.ArgumentList is null || execute.ArgumentList.Arguments.Count == 0))
            {
                properties["commandAssociation"] = "syntax-local-straight-line-candidate";
                properties["commandDeclarationStartLine"] = (declaration.GetLocation().GetLineSpan().StartLinePosition.Line + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
                properties["commandExecutionStartLine"] = (execute.GetLocation().GetLineSpan().StartLinePosition.Line + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
                properties["commandExecutionSpanStart"] = execute.SpanStart.ToString(System.Globalization.CultureInfo.InvariantCulture);
                properties["commandExecutionMethod"] = member.Name.Identifier.ValueText;
                properties["commandAssociationReason"] = "fresh-local-consecutive-adds-and-execution";
            }
            else properties["commandAssociationReason"] = "intervening-statement-or-unsupported-execution";
            return;
        }
        properties["commandAssociationReason"] = "no-following-execution";
    }
}
