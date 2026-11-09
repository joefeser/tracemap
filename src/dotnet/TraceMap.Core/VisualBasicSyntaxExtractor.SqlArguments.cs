using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;

namespace TraceMap.Core;

public static partial class VisualBasicSyntaxExtractor
{
    private static readonly Lazy<string> SqlArgumentGenerator = new(() => Convert.ToHexStringLower(
        SHA256.HashData(GeneratorBinaryIdentity.ReadBytes(typeof(VisualBasicSyntaxExtractor).Assembly))));

    private sealed record SqlTextOrigin(string Verb, int Start, int End, bool HasVariable, TextSpan Construction);

    // Only adjacent, explicit construction steps contribute to the argument.
    // A control-flow block, call, or other intervening statement kills tracked values.
    private static void AddSqlArgumentFacts(ScanManifest manifest, List<CodeFact> facts, string file,
        CompilationUnitSyntax root, IReadOnlyList<ProtectedSourceSpan> protectedSpans)
    {
        if (root.ContainsDirectives && root.DescendantTrivia().Any(t => t.IsKind(SyntaxKind.IfDirectiveTrivia))) return;
        const int maxHits = 256;
        var budget = new FactBudget(maxHits);
        foreach (var method in root.DescendantNodes().OfType<MethodBlockSyntax>())
        {
            if (method.ContainsDiagnostics || method.ContainsDirectives || method.DescendantNodes().Any(n =>
                n is LabelStatementSyntax or GoToStatementSyntax || n.GetType().Name is
                    "OnErrorGoToStatementSyntax" or "OnErrorResumeNextStatementSyntax" or "ResumeStatementSyntax")) continue;

            string? local = null;
            SqlTextOrigin? origin = null;
            foreach (var stmt in method.Statements)
            {
                if (OverlapsProtected(stmt, protectedSpans))
                {
                    local = null;
                    origin = null;
                    continue;
                }
                var start = stmt.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
                var end = stmt.GetLocation().GetLineSpan().EndLinePosition.Line + 1;
                if (stmt is LocalDeclarationStatementSyntax decl && decl.Modifiers.Count == 1
                    && decl.Modifiers[0].IsKind(SyntaxKind.DimKeyword) && decl.Declarators.Count == 1
                    && decl.Declarators[0] is { Names.Count: 1, Initializer.Value: { } init } variable
                    && SqlTextExpression(init, start, end, stmt.Span) is { } seed)
                {
                    local = variable.Names[0].Identifier.ValueText;
                    origin = seed;
                    continue;
                }
                if (origin is not null && stmt is AssignmentStatementSyntax assignment
                    && SameLocal(assignment.Left, local) && AppendedLiteral(assignment, local))
                {
                    origin = origin with { End = end, Construction = TextSpan.FromBounds(origin.Construction.Start, stmt.Span.End) };
                    continue;
                }

                if (local is not null && stmt is AssignmentStatementSyntax replacement
                    && replacement.IsKind(SyntaxKind.SimpleAssignmentStatement) && SameLocal(replacement.Left, local)
                    && SqlTextExpression(replacement.Right, start, end, stmt.Span) is { } reset)
                {
                    origin = reset;
                    continue;
                }

                var call = stmt switch
                {
                    ExpressionStatementSyntax e => e.Expression as InvocationExpressionSyntax,
                    CallStatementSyntax c => c.Invocation as InvocationExpressionSyntax,
                    ReturnStatementSyntax r => r.Expression as InvocationExpressionSyntax,
                    AssignmentStatementSyntax { Left: IdentifierNameSyntax } a => a.Right as InvocationExpressionSyntax,
                    LocalDeclarationStatementSyntax { Declarators.Count: 1 } d => d.Declarators[0].Initializer?.Value as InvocationExpressionSyntax,
                    _ => null
                };
                if (call?.Expression is MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax owner, Name: IdentifierNameSyntax name }
                    && IsSqlWrapperName(name.Identifier.ValueText) && call.ArgumentList is { Arguments.Count: > 0 } args
                    && args.Arguments[0] is SimpleArgumentSyntax { NameColonEquals: null } arg)
                {
                    var value = SameLocal(arg.Expression, local) ? origin : SqlTextExpression(arg.Expression,
                        arg.GetLocation().GetLineSpan().StartLinePosition.Line + 1, arg.GetLocation().GetLineSpan().EndLinePosition.Line + 1, arg.Expression.Span);
                    if (value is not null)
                    {
                        var target = owner.Identifier.ValueText + "." + name.Identifier.ValueText;
                        var props = new SortedDictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["sqlOperation"] = value.Verb, ["commandInstanceBinding"] = "unverified",
                            ["constructionStartLine"] = value.Start.ToString(CultureInfo.InvariantCulture),
                            ["constructionEndLine"] = value.End.ToString(CultureInfo.InvariantCulture),
                            ["constructionKind"] = SameLocal(arg.Expression, local) ? "local" : "inline",
                            ["containsVariableOperand"] = value.HasVariable ? "true" : "false",
                            ["argumentOrdinal"] = "1", ["callOffset"] = call.SpanStart.ToString(CultureInfo.InvariantCulture),
                            ["generatorSha256"] = SqlArgumentGenerator.Value
                        };
                        var constructionHash = FactFactory.Hash(root.SyntaxTree.GetText().ToString(value.Construction), 64);
                        var callHash = FactFactory.Hash(call.ToString(), 64);
                        props["boundedInputSha256"] = FactFactory.Hash(JsonSerializer.Serialize(new { manifest.CommitSha,
                            file, target, constructionHash, callHash, properties = props.ToArray() }), 64);
                        if (budget.Remaining == 0)
                        {
                            facts.Add(FactFactory.Create(manifest, FactTypes.AnalysisGap, RuleIds.VisualBasicSqlArgument,
                                EvidenceTiers.Tier4Unknown, new EvidenceSpan(file, start, end, null, "VisualBasicSyntaxExtractor",
                                    ScannerVersions.VisualBasicSyntaxExtractor), properties: new Dictionary<string, string>
                                    { ["gapKind"] = "SqlTextArgumentLimit", ["limit"] = "256" }));
                            return;
                        }
                        if (!TryAddSyntaxFact(manifest, facts, FactTypes.SqlTextArgumentCandidate, RuleIds.VisualBasicSqlArgument,
                            file, call, target, props, budget, GetContainingMemberName(call), snippetHash: callHash)) return;
                    }
                }
                local = null;
                origin = null;
            }
        }
    }

    private static bool IsSqlWrapperName(string name) =>
        name.Equals("ExecSql", StringComparison.OrdinalIgnoreCase) || name.StartsWith("ExecSql_", StringComparison.OrdinalIgnoreCase)
        || name.Equals("ExecuteSql", StringComparison.OrdinalIgnoreCase) || name.StartsWith("ExecuteSql_", StringComparison.OrdinalIgnoreCase);

    private static bool SameLocal(ExpressionSyntax expr, string? name) => expr is IdentifierNameSyntax id
        && id.Identifier.ValueText.Equals(name, StringComparison.OrdinalIgnoreCase);

    private static bool AppendedLiteral(AssignmentStatementSyntax stmt, string? local) =>
        stmt.IsKind(SyntaxKind.SimpleAssignmentStatement) && stmt.Right is BinaryExpressionSyntax binary
        && IsSqlConcat(binary) && SameLocal(binary.Left, local) && binary.Right.IsKind(SyntaxKind.StringLiteralExpression)
        || (stmt.IsKind(SyntaxKind.ConcatenateAssignmentStatement) || stmt.IsKind(SyntaxKind.AddAssignmentStatement))
            && stmt.Right.IsKind(SyntaxKind.StringLiteralExpression);

    private static bool IsSqlConcat(BinaryExpressionSyntax expr) =>
        expr.IsKind(SyntaxKind.ConcatenateExpression) || expr.IsKind(SyntaxKind.AddExpression);

    private static SqlTextOrigin? SqlTextExpression(ExpressionSyntax expr, int start, int end, TextSpan construction)
    {
        bool variable = false;
        while (expr is BinaryExpressionSyntax binary && IsSqlConcat(binary))
        {
            if (binary.Right is IdentifierNameSyntax) variable = true;
            else if (!binary.Right.IsKind(SyntaxKind.StringLiteralExpression)) return null;
            expr = binary.Left;
        }
        if (expr is not LiteralExpressionSyntax literal || !literal.IsKind(SyntaxKind.StringLiteralExpression)) return null;
        var words = literal.Token.ValueText.AsSpan().TrimStart();
        foreach (var verb in new[] { "SELECT", "UPDATE" })
            if (words.Length > verb.Length && char.IsWhiteSpace(words[verb.Length])
                && words.StartsWith(verb, StringComparison.OrdinalIgnoreCase))
                return new(verb, start, end, variable, construction);
        return null;
    }
}
