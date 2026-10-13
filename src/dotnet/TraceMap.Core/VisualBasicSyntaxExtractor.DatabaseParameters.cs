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
            ["ruleLimitations"] = "Syntax parameter candidate only; no compiler/provider binding, overload resolution, command instance identity, procedure association, database declaration, direction default or runtime value is inferred. Names and argument expressions are hashed; source review is required."
        };
        if (coverage == "syntax-constructor-only") properties["parameterType"] = type;
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
}
