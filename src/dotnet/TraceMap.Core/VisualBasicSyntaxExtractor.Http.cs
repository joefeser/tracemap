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
        => VisualBasicSyntaxTypeLookup.For(root).Resolve(text);

    private static string StripGlobal(string name) => name.StartsWith("Global.", StringComparison.OrdinalIgnoreCase)
        ? name[7..] : name;
}
