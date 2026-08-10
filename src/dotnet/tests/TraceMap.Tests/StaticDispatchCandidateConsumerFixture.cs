using TraceMap.Combine;
using TraceMap.Core;
using TraceMap.Storage;

namespace TraceMap.Tests;

internal static class StaticDispatchCandidateConsumerFixture
{
    public static async Task<string> CreateCombinedIndexAsync(string root, string suffix = "dispatch")
    {
        var indexPath = Path.Combine(root, $"{suffix}-source.sqlite");
        var combinedPath = Path.Combine(root, $"{suffix}-combined.sqlite");
        var manifest = Manifest(suffix);
        const string controller = "Sample.OrdersController.Get(System.Int32)";
        const string abstraction = "Sample.IOrderService.Get(System.Int32)";
        const string implementation = "Sample.OrderService.Get(System.Int32)";
        const string repository = "Sample.OrderRepository.Query(System.Int32)";

        SqliteIndexWriter.Write(indexPath, manifest, [
            FactFactory.Create(
                manifest,
                FactTypes.CallEdge,
                RuleIds.CSharpSemanticCallGraph,
                EvidenceTiers.Tier1Semantic,
                new EvidenceSpan("Controllers/OrdersController.cs", 14, 14, null, "test", "test/1.0"),
                sourceSymbol: controller,
                targetSymbol: abstraction,
                properties: new SortedDictionary<string, string>(StringComparer.Ordinal) { ["callKind"] = "method" }),
            FactFactory.Create(
                manifest,
                FactTypes.SymbolRelationship,
                RuleIds.CSharpSemanticSymbolRelationship,
                EvidenceTiers.Tier1Semantic,
                new EvidenceSpan("Services/OrderService.cs", 18, 18, null, "test", "test/1.0"),
                sourceSymbol: implementation,
                targetSymbol: abstraction,
                contractElement: "ImplementsInterfaceMember",
                properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["relationshipKind"] = "ImplementsInterfaceMember",
                    ["sourceContainingSymbolId"] = "type:Sample.OrderService",
                    ["sourceSymbolDisplayName"] = implementation,
                    ["sourceSymbolId"] = implementation,
                    ["targetContainingSymbolId"] = "type:Sample.IOrderService",
                    ["targetSymbolDisplayName"] = abstraction,
                    ["targetSymbolId"] = abstraction
                }),
            FactFactory.Create(
                manifest,
                FactTypes.CallEdge,
                RuleIds.CSharpSemanticCallGraph,
                EvidenceTiers.Tier1Semantic,
                new EvidenceSpan("Services/OrderService.cs", 21, 21, null, "test", "test/1.0"),
                sourceSymbol: implementation,
                targetSymbol: repository,
                properties: new SortedDictionary<string, string>(StringComparer.Ordinal) { ["callKind"] = "method" })
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([indexPath], combinedPath, [suffix]));
        return combinedPath;
    }

    private static ScanManifest Manifest(string repo) => new(
        $"scan-{repo}",
        repo,
        null,
        "main",
        "1111111111111111111111111111111111111111",
        ScannerVersions.TraceMap,
        DateTimeOffset.Parse("2026-08-09T00:00:00Z"),
        "Level1SemanticAnalysis",
        "Succeeded",
        [],
        [],
        [],
        [],
        ".",
        FactFactory.Hash(repo, 32),
        FactFactory.Hash("git-root", 32));
}
