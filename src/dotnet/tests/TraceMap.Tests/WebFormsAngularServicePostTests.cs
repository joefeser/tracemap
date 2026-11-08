using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class WebFormsAngularServicePostTests
{
    private static readonly ScanManifest Manifest = new("service", "synthetic", null, "test", "abc123", "test/1",
        DateTimeOffset.UnixEpoch, "Level3SyntaxAnalysis", "NotRun", [], [], [], []);

    [Fact]
    public void Two_partial_clicks_use_the_positional_service_and_http_bindings_and_exact_declaration()
    {
        var facts = Extract();
        var links = facts.Where(f => f.FactType == FactTypes.WebFormsAngularPostHandlerCandidate).ToArray();
        Assert.Equal(2, links.Length);
        Assert.Equal(2, links.Select(f => f.Properties["clickFactId"]).Distinct().Count());
        foreach (var link in links)
        {
            Assert.Equal("Api/Save.ashx", link.Evidence.FilePath);
            var endpoint = facts.Single(f => f.FactId == link.Properties["declarationFactId"]);
            Assert.Equal(FactTypes.AspNetHandlerDeclared, endpoint.FactType);
            var request = facts.Single(f => f.FactId == link.Properties["requestFactId"]);
            Assert.Equal(FactTypes.WebFormsClientHttpRequestCandidate, request.FactType);
            Assert.Equal("POST", request.Properties["httpMethod"]);
            Assert.Equal("1", request.Properties["httpInjectionSlot"]);
            Assert.Equal("Scripts/services.js", request.Evidence.FilePath);
            Assert.Equal(3, request.Evidence.StartLine);
            var member = facts.Single(f => f.FactId == request.Properties["supportingFactIds"]);
            Assert.Equal("records", member.Properties["serviceToken"]);
            var support = member.Properties["supportingFactIds"].Split(',');
            var call = Assert.Single(facts, f => support.Contains(f.FactId) && f.FactType == FactTypes.WebFormsAngularServiceCallCandidate);
            Assert.Equal(6, call.Evidence.StartLine);
            Assert.Equal("1", call.Properties["injectionSlot"]);
            var handler = facts.Single(f => f.FactId == call.Properties["clickHandlerFactId"]);
            Assert.Equal(FactTypes.WebFormsAngularPartialClickHandlerCandidate, handler.FactType);
            Assert.Equal(handler.Properties["clickFactId"], link.Properties["clickFactId"]);
            Assert.Contains(facts, f => support.Contains(f.FactId) && f.FactType == FactTypes.WebFormsAngularServiceRegistrationCandidate);
        }
    }

    [Theory]
    [InlineData("Scripts/services.js", "\"unrelated\"", "\"records\"", "AmbiguousAngularService")]
    [InlineData("Scripts/panel.js", "client.submit(rows)", "(function(client){ client.submit(rows); })(other)", "UnsupportedAngularServiceCall")]
    [InlineData("Scripts/panel.js", "client.submit(rows)", "client = other; client.submit(rows)", "AngularServiceReceiverBindingUnavailable")]
    [InlineData("Scripts/services.js", "this.submit =", "this.submit = function() {}; this.submit =", "AmbiguousAngularServiceMember")]
    [InlineData("Scripts/services.js", "return send({", "return send({ ...options,", "AngularHttpOptionsUnavailable")]
    [InlineData("Scripts/services.js", "method: \"post\"", "method: \"post\", method: \"get\"", "AngularHttpOptionsUnavailable")]
    [InlineData("Scripts/services.js", "/Portal/Desk/API/save.ashx", "/Other/API/save.ashx", "AngularIisMountPrefixMismatch")]
    [InlineData("Scripts/services.js", "/Portal/Desk/API/save.ashx", "/Portal/Desk/missing/save.ashx", "AngularHttpEndpointOutsideInventory")]
    [InlineData("Scripts/services.js", "\"$http\"", "\"different\"", "AngularHttpInjectionUnavailable")]
    [InlineData("Api/Save.ashx", "WebHandler", "Unknown", "AngularHttpHandlerDeclarationUnavailable")]
    public void Unproven_bindings_and_requests_emit_a_gap_without_an_endpoint(string file, string from, string to, string gap)
    {
        var facts = Extract(file, from, to);
        Assert.DoesNotContain(facts, f => f.FactType == FactTypes.WebFormsAngularPostHandlerCandidate);
        Assert.Contains(facts, f => f.RuleId == RuleIds.LegacyWebFormsAngularServiceHttp && f.Properties.GetValueOrDefault("gapKind") == gap);
    }

    [Fact]
    public void More_than_sixteen_injected_calls_retains_the_truncation_gap()
    {
        var facts = Extract("Scripts/panel.js", "client.submit(rows).then(function () {});",
            string.Concat(Enumerable.Repeat("client.submit(rows);\n", 17)));
        Assert.Contains(facts, f => f.Properties.GetValueOrDefault("gapKind") == "AngularServiceCallLimit");
    }

    private static CodeFact[] Extract(string? file = null, string? from = null, string? to = null)
    {
        using var temp = new TempDirectory();
        var source = Path.Combine(SqlDatabaseMatrixCells.Repo(), "samples/webforms-service-post");
        foreach (var path in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, path);
            var output = Path.Combine(temp.Path, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            var text = File.ReadAllText(path);
            File.WriteAllText(output, relative.Replace('\\', '/') == file ? text.Replace(from!, to) : text);
        }
        var inventory = FileInventory.Collect(temp.Path);
        var frontend = LegacyWebFormsExtractor.Extract(temp.Path, Manifest, inventory, [], WebFormsIisMount.Parse("/Portal/Desk=."));
        return frontend.Concat(LegacyAspNetExtractor.Extract(temp.Path, Manifest, inventory, frontend)).ToArray();
    }
}
