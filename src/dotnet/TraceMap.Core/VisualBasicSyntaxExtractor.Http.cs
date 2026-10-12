using Microsoft.CodeAnalysis.VisualBasic.Syntax;

namespace TraceMap.Core;

public static partial class VisualBasicSyntaxExtractor
{
    private static bool TryAddHttpCandidate(ScanManifest manifest, List<CodeFact> facts,
        string filePath, CompilationUnitSyntax root, InvocationExpressionSyntax invocation,
        string methodName, string? containingMember, string? receiverType, FactBudget budget)
    {
        if (!OutboundHttpApi.IsCandidate(methodName, ignoreCase: true)) return true;
        // Static extension invocation is also a bounded syntax candidate. No assembly
        // identity, overload selection, dynamic dispatch or execution is established.
        receiverType ??= (invocation.Expression as MemberAccessExpressionSyntax)?.Expression?.ToString();
        if (receiverType is null) return true;
        var names = ResolveHttpTypeNames(root, receiverType)
            .Where(name => OutboundHttpApi.IsSyntaxCandidate(name, methodName))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (names.Length != 1) return true;
        return TryAddSyntaxFact(manifest, facts, FactTypes.HttpCallDetected,
            RuleIds.VisualBasicSyntaxHttpCandidate, filePath, invocation, methodName,
            new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["methodName"] = methodName,
                ["methodFamily"] = "unresolved",
                ["classification"] = "SyntaxFallbackHttpCandidate",
                ["coverageLabel"] = "reduced-explicit-receiver-candidate",
                ["receiverType"] = names[0],
                ["receiverTypeResolution"] = "explicit-caller-syntax",
                ["httpMethod"] = "UNKNOWN",
                ["urlKind"] = "unavailable",
                ["surfaceKind"] = "http-client",
                ["limitations"] = "Explicit receiver syntax only; framework assembly identity, overload, imports from other files, runtime dispatch and execution are unproven. No URL, body or credentials are retained. Aliased values, inferred factories and late binding may be omitted; absence never means unused."
            }, budget, sourceSymbol: containingMember);
    }

    private static IEnumerable<string> ResolveHttpTypeNames(CompilationUnitSyntax root, string text)
    {
        text = text.Trim();
        if (text.StartsWith("Global.", StringComparison.OrdinalIgnoreCase))
        {
            yield return text[7..];
            yield break;
        }
        var first = text.Split('.')[0];
        // A same-file declaration shadows an imported short name. Do not guess.
        if (root.DescendantNodes().OfType<TypeBlockSyntax>().Any(type =>
                type.BlockStatement.Identifier.ValueText.Equals(first, StringComparison.OrdinalIgnoreCase)))
            yield break;
        var imports = root.Imports.SelectMany(statement => statement.ImportsClauses)
            .OfType<SimpleImportsClauseSyntax>().ToArray();
        var aliases = imports.Where(clause => clause.Alias?.Identifier.ValueText.Equals(first,
            StringComparison.OrdinalIgnoreCase) == true).ToArray();
        if (aliases.Length > 0)
        {
            foreach (var alias in aliases)
                yield return StripGlobal(alias.Name.ToString()) + text[first.Length..];
            yield break;
        }
        if (text.Contains('.')) { yield return text; yield break; }
        foreach (var import in imports.Where(clause => clause.Alias is null))
            yield return StripGlobal(import.Name.ToString()) + "." + text;
    }

    private static string StripGlobal(string name) => name.StartsWith("Global.", StringComparison.OrdinalIgnoreCase)
        ? name[7..] : name;
}
