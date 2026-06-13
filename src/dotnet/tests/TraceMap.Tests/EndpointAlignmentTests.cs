using TraceMap.Core;
using TraceMap.EndpointAlignment;
using TraceMap.Storage;

namespace TraceMap.Tests;

public sealed class EndpointAlignmentTests
{
    [Fact]
    public void Route_normalizer_handles_malformed_percent_encoding_without_throwing()
    {
        var route = EndpointRouteNormalizer.Normalize("/api/admin/%zz/{id}");

        Assert.Equal("/api/admin/%zz/{}", route.PathKey);
        Assert.Equal("/api/admin/%zz/{id}", route.PathTemplate);
    }

    [Fact]
    public void Optional_route_expansion_is_capped_for_large_optional_templates()
    {
        var route = EndpointRouteNormalizer.Normalize("/api/{a?}/{b?}/{c?}/{d?}/{e?}/{f?}");

        var keys = EndpointRouteNormalizer.ExpandOptionalPathKeys(route);

        Assert.Equal(["/api/{}/{}/{}/{}/{}/{}"], keys);
    }

    [Fact]
    public void Scan_extracts_aspnet_routes_with_syntax_fallback()
    {
        using var temp = new TempDirectory();
        Directory.CreateDirectory(Path.Combine(temp.Path, "src", "Api"));
        File.WriteAllText(Path.Combine(temp.Path, "src", "Api", "RunnerController.cs"), """
            namespace Sample.Api;

            [ApiController]
            [Route("api/admin/[controller]")]
            public sealed class RunnerController
            {
                [HttpGet("get-by-id/{runnerId:guid}")]
                public object GetById(string runnerId)
                {
                    return new { runnerId };
                }

                [HttpPost("check-in/{clubId?}")]
                public object CheckIn(CheckInRequest request)
                {
                    return request;
                }
            }

            public sealed class CheckInRequest
            {
                public string Name { get; set; } = "";
            }
            """);

        var result = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, ".tracemap")));

        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.HttpRouteBinding
            && fact.RuleId == RuleIds.CSharpSyntaxAspNetRoute
            && fact.Properties["methodName"] == "GET"
            && fact.Properties["normalizedPathKey"] == "/api/admin/runner/get-by-id/{}"
            && fact.Properties["routeConstraints"] == "runnerId:guid");
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.HttpRouteBinding
            && fact.Properties["normalizedPathTemplate"] == "/api/admin/Runner/check-in/{clubId?}"
            && fact.Properties["optionalParameterNames"] == "clubId"
            && fact.Properties["bodyParameterNames"] == "request");
    }

    [Fact]
    public async Task Endpoint_alignment_reports_matches_mismatches_dynamic_and_unmatched_endpoints()
    {
        using var temp = new TempDirectory();
        var clientIndex = Path.Combine(temp.Path, "client.sqlite");
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var outDir = Path.Combine(temp.Path, "endpoints");
        var clientManifest = Manifest("client", "Level1SemanticAnalysisReduced", "FailedOrPartial", "client/root");
        var serverManifest = Manifest("server", "Level1SemanticAnalysis", "Succeeded", "server/root");

        SqliteIndexWriter.Write(clientIndex, clientManifest, [
            HttpClientFact(clientManifest, "fact-client-get", "GET", "/api/admin/runner/get-by-id/{runnerId}", "/api/admin/runner/get-by-id/{}", "src/runner.service.ts", 8),
            HttpClientFact(clientManifest, "fact-client-optional", "POST", "/api/admin/runner/check-in?source=app", "/api/admin/runner/check-in", "src/runner.service.ts", 12, query: "source"),
            HttpClientFact(clientManifest, "fact-client-method", "DELETE", "/api/admin/runner/archive", "/api/admin/runner/archive", "src/runner.service.ts", 16),
            DynamicClientFact(clientManifest, "src/runner.service.ts", 20)
        ]);
        SqliteIndexWriter.Write(serverIndex, serverManifest, [
            RouteFact(serverManifest, "GET", "/api/admin/Runner/get-by-id/{id}", "/api/admin/runner/get-by-id/{}", "Controllers/RunnerController.cs", 10),
            RouteFact(serverManifest, "POST", "/api/admin/Runner/check-in/{clubId?}", "/api/admin/runner/check-in/{?}", "Controllers/RunnerController.cs", 16, optional: "clubId"),
            RouteFact(serverManifest, "POST", "/api/admin/Runner/archive", "/api/admin/runner/archive", "Controllers/RunnerController.cs", 22),
            RouteFact(serverManifest, "GET", "/api/admin/Runner/server-only", "/api/admin/runner/server-only", "Controllers/RunnerController.cs", 28)
        ]);

        var result = await EndpointAlignmentEngine.AlignAsync(new EndpointAlignmentOptions(clientIndex, serverIndex, outDir, ClientLabel: "client", ServerLabel: "server"));

        Assert.Contains(result.Report.Findings, finding => finding.Classification == EndpointClassifications.MatchedEndpoint);
        Assert.Contains(result.Report.Findings, finding => finding.Classification == EndpointClassifications.OptionalSegmentMatch);
        Assert.Contains(result.Report.Findings, finding => finding.Classification == EndpointClassifications.MethodMismatch);
        Assert.Contains(result.Report.Findings, finding => finding.Classification == EndpointClassifications.DynamicClientUrlNeedsReview);
        Assert.Contains(result.Report.Findings, finding => finding.Classification == EndpointClassifications.ServerEndpointNoClientMatch);
        Assert.Contains(result.Report.CoverageWarnings, warning => warning.Contains("client index reports", StringComparison.Ordinal));
        Assert.True(File.Exists(Path.Combine(outDir, "endpoint-report.md")));
        Assert.True(File.Exists(Path.Combine(outDir, "endpoint-report.json")));
        var markdown = await File.ReadAllTextAsync(Path.Combine(outDir, "endpoint-report.md"));
        Assert.Contains("not proof of dead code", markdown);
    }

    private static ScanManifest Manifest(string repo, string analysisLevel, string buildStatus, string scanRoot)
    {
        return new ScanManifest(
            $"scan-{repo}",
            repo,
            null,
            "main",
            "abc123",
            "test",
            DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
            analysisLevel,
            buildStatus,
            [],
            [],
            [],
            [],
            scanRoot,
            FactFactory.Hash(scanRoot, 32),
            FactFactory.Hash("git-root", 32));
    }

    private static CodeFact HttpClientFact(ScanManifest manifest, string id, string method, string template, string key, string file, int line, string query = "")
    {
        return FactFactory.Create(
            manifest,
            FactTypes.HttpCallDetected,
            RuleIds.HttpClientInvocation,
            EvidenceTiers.Tier2Structural,
            new EvidenceSpan(file, line, line, null, "test", "test/1.0"),
            targetSymbol: id,
            contractElement: method,
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["httpMethod"] = method,
                ["methodName"] = method,
                ["normalizedPathTemplate"] = template,
                ["normalizedPathKey"] = key,
                ["queryParameterNames"] = query,
                ["hasQueryParameters"] = (!string.IsNullOrWhiteSpace(query)).ToString().ToLowerInvariant(),
                ["urlKind"] = "template"
            });
    }

    private static CodeFact DynamicClientFact(ScanManifest manifest, string file, int line)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.HttpCallDetected,
            RuleIds.HttpClientInvocation,
            EvidenceTiers.Tier3SyntaxOrTextual,
            new EvidenceSpan(file, line, line, null, "test", "test/1.0"),
            targetSymbol: "dynamic",
            contractElement: "GET",
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["httpMethod"] = "GET",
                ["methodName"] = "GET",
                ["urlKind"] = "dynamic",
                ["dynamicReason"] = "HelperFunctionCall"
            });
    }

    private static CodeFact RouteFact(ScanManifest manifest, string method, string template, string key, string file, int line, string optional = "")
    {
        return FactFactory.Create(
            manifest,
            FactTypes.HttpRouteBinding,
            RuleIds.CSharpSyntaxAspNetRoute,
            EvidenceTiers.Tier3SyntaxOrTextual,
            new EvidenceSpan(file, line, line, null, "test", "test/1.0"),
            targetSymbol: $"{method} {template}",
            contractElement: template,
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["httpMethods"] = method,
                ["methodName"] = method,
                ["normalizedPathTemplate"] = template,
                ["normalizedPathKey"] = key,
                ["optionalParameterNames"] = optional
            });
    }
}
