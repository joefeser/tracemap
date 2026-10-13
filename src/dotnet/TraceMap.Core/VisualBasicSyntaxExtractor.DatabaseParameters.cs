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
        var arguments = creation.ArgumentList?.Arguments;
        var properties = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["parameterType"] = names[0],
            ["parameterNameEvidence"] = "unavailable",
            ["parameterTypeEvidence"] = "unavailable",
            ["parameterDirectionEvidence"] = "unknown",
            ["commandAssociation"] = "unresolved",
            ["coverageLabel"] = "syntax-constructor-only",
            ["ruleLimitations"] = "Syntax constructor candidate only; no compiler/provider binding, overload resolution, command association, database declaration, direction default or runtime value is inferred. Names and argument expressions are hashed; source review is required."
        };
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
                    && ResolveHttpTypeNames(root, member.Expression.ToString()).Distinct(StringComparer.OrdinalIgnoreCase).SequenceEqual(["System.Data.SqlDbType"], StringComparer.OrdinalIgnoreCase)
                    && Enum.TryParse<System.Data.SqlDbType>(member.Name.Identifier.ValueText, true, out var kind)
                    && Enum.IsDefined(kind))
                {
                    properties["parameterTypeEvidence"] = "syntax-enum-candidate";
                    properties["parameterTypeName"] = "SqlDbType." + kind;
                }
            }
        }
        return TryAddSyntaxFact(manifest, facts, FactTypes.DatabaseParameterCandidate,
            RuleIds.VisualBasicSyntaxDatabaseParameter, filePath, creation, names[0], properties,
            budget, sourceSymbol: GetContainingMemberName(creation));
    }
}
