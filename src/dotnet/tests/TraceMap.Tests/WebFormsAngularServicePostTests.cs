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
            Assert.Equal(5, request.Evidence.StartLine);
            var member = facts.Single(f => f.FactId == request.Properties["supportingFactIds"]);
            Assert.Equal("records", member.Properties["serviceToken"]);
            var support = member.Properties["supportingFactIds"].Split(',');
            var call = Assert.Single(facts, f => support.Contains(f.FactId) && f.FactType == FactTypes.WebFormsAngularServiceCallCandidate);
            Assert.Equal(6, call.Evidence.StartLine);
            Assert.Equal("1", call.Properties["injectionSlot"]);
            var handler = facts.Single(f => f.FactId == call.Properties["clickHandlerFactId"]);
            Assert.Equal(FactTypes.WebFormsAngularPartialClickHandlerCandidate, handler.FactType);
            Assert.Equal(handler.Properties["clickFactId"], link.Properties["clickFactId"]);
            var registration = Assert.Single(facts, f => support.Contains(f.FactId) && f.FactType == FactTypes.WebFormsAngularServiceRegistrationCandidate);
            Assert.Equal((3, 8), (registration.Evidence.StartLine, registration.Evidence.EndLine));
            using var continuations = System.Text.Json.JsonDocument.Parse(registration.Properties["moduleContinuations"]);
            var suffix = continuations.RootElement.EnumerateArray().ToArray();
            Assert.Equal(new[] { "filter", "directive", "config", "run" }, suffix.Select(x => x.GetProperty("member").GetString()));
            Assert.Equal(new[] { 9, 10, 11, 12 }, suffix.Select(x => x.GetProperty("startLine").GetInt32()));
            var factory = Assert.Single(facts, f => f.FactType == FactTypes.WebFormsAngularFactoryRegistrationCandidate
                && registration.Properties["supportingFactIds"].Split(',').Contains(f.FactId));
            Assert.Equal("formatting", factory.Properties["factoryToken"]);
            Assert.Equal(2, factory.Evidence.StartLine);
            Assert.Equal(factory.TargetSymbol, registration.SourceSymbol);
            Assert.Contains(facts, f => f.TargetSymbol == factory.SourceSymbol && f.Properties.GetValueOrDefault("ownerKind") == "module"
                && factory.Properties["supportingFactIds"].Split(',').Contains(f.FactId));
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

    [Theory]
    [InlineData(".factory(\"formatting\"", ".unknown(\"formatting\"", "AngularServiceModuleUnavailable")]
    [InlineData(".factory(\"formatting\"", ".factory(dynamicName", "UnsupportedAngularFactoryRegistration")]
    [InlineData(".factory(\"formatting\"", ".factory(\"records\"", "UnsupportedAngularServiceRegistration")]
    [InlineData("function () { return {}; }", "makeFactory()", "UnsupportedAngularFactoryRegistration")]
    [InlineData("exampleApp\n.factory", "exampleApp.factory = custom; exampleApp\n.factory", "AngularFactoryReceiverMutation")]
    [InlineData("exampleApp\n.factory", "if (enabled) exampleApp\n.factory", "UnsupportedAngularServiceRegistration")]
    public void Unsupported_factory_chains_keep_the_gap(string from, string to, string gap)
    {
        var facts = Extract("Scripts/services.js", from, to);
        Assert.DoesNotContain(facts, f => f.FactType == FactTypes.WebFormsAngularPostHandlerCandidate);
        Assert.Contains(facts, f => f.Properties.GetValueOrDefault("gapKind") == gap);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(27)]
    [InlineData(28)]
    [InlineData(32)]
    public void Registration_bound_counts_prefix_service_and_suffix(int count)
    {
        var chain = string.Concat(Enumerable.Range(0, count).Select(i => $".factory('helper{i}', ['input', function (input) {{}}])"));
        var facts = Extract("Scripts/services.js", ".factory(\"formatting\", function () { return {}; })", chain);
        Assert.Equal(count <= 27 ? 2 : 0, facts.Count(f => f.FactType == FactTypes.WebFormsAngularPostHandlerCandidate));
        Assert.Equal(count <= 27 ? count : 0, facts.Count(f => f.FactType == FactTypes.WebFormsAngularFactoryRegistrationCandidate));
        if (count > 27) Assert.Contains(facts, f => f.Properties.GetValueOrDefault("gapKind") == "AngularServiceProviderChainLimit");
    }

    [Fact]
    public void Terminal_service_still_resolves_without_continuation_evidence()
    {
        var facts = Extract("Scripts/services.js", "}])\n.filter", "}]);\nexampleApp.filter");
        Assert.Equal(2, facts.Count(f => f.FactType == FactTypes.WebFormsAngularPostHandlerCandidate));
        Assert.DoesNotContain(facts, f => f.Properties.ContainsKey("moduleContinuations"));
    }

    [Theory]
    [InlineData(".filter(\"display\"", ".unknown(\"display\"")]
    [InlineData(".directive(\"summary\"", ".directive(dynamicName")]
    [InlineData(".config([function () {}])", ".config(configureLater)")]
    [InlineData(".run([\"records\"", "?.run([\"records\"")]
    [InlineData("exampleApp\n.factory", "var configured = exampleApp\n.factory")]
    [InlineData("exampleApp\n.factory", "if (enabled) exampleApp\n.factory")]
    public void Unproven_continuation_or_enclosure_refuses_the_join(string from, string to)
    {
        var facts = Extract("Scripts/services.js", from, to);
        Assert.DoesNotContain(facts, f => f.FactType == FactTypes.WebFormsAngularPostHandlerCandidate);
        Assert.Contains(facts, f => f.Properties.GetValueOrDefault("gapKind") == "UnsupportedAngularServiceRegistration");
    }

    [Theory]
    [InlineData("adapter.js")]
    [InlineData("z-adapter.js")]
    public void Unbound_library_call_is_a_location_backed_gap_in_either_source_order(string adapter)
    {
        var facts = Extract(adapterName: adapter);
        var edges = facts.Where(f => f.FactType == FactTypes.WebFormsAngularPostHandlerCandidate).ToArray();
        Assert.Equal(2, edges.Length);
        foreach (var edge in edges)
        {
            var request = Assert.Single(facts, f => f.FactId == edge.Properties["requestFactId"]);
            var ids = request.Properties["unboundProviderGapIds"];
            Assert.Equal(ids, edge.Properties["unboundProviderGapIds"]);
            var gaps = ids.Split(',').Select(id => Assert.Single(facts, f => f.FactId == id)).OrderBy(f => f.Evidence.StartLine).ToArray();
            Assert.Equal(new[] { "absent", "call-expression", "identifier" }, gaps.Select(f => f.Properties["tokenKind"]));
            Assert.Equal(new[] { "this", "this", "member-expression" }, gaps.Select(f => f.Properties["receiverKind"]));
            Assert.Equal(new[] { 2, 3, 4 }, gaps.Select(f => f.Evidence.StartLine));
            var member = Assert.Single(facts, f => f.FactId == request.Properties["supportingFactIds"]);
            Assert.All(gaps, gap =>
            {
                Assert.Contains(gap.FactId, member.Properties["supportingFactIds"].Split(','));
                Assert.Equal("AngularUnboundProviderIdentity", gap.Properties["gapKind"]);
                Assert.Equal("Scripts/" + adapter, gap.Evidence.FilePath);
                Assert.Equal(EvidenceTiers.Tier4Unknown, gap.EvidenceTier);
                Assert.Contains(facts, f => f.FactType == FactTypes.WebFormsScriptBundleMemberCandidate
                    && gap.Properties["supportingFactIds"].Split(',').Contains(f.FactId));
            });
        }
    }

    [Theory]
    [InlineData("this.service", "service")]
    [InlineData("this['service']", "service")]
    [InlineData("this.factory", "factory")]
    [InlineData("this['provider']", "provider")]
    [InlineData("this.value", "value")]
    [InlineData("this.constant", "constant")]
    [InlineData("this.decorator", "decorator")]
    [InlineData("this?.service", "service")]
    public void Unbound_shape_preserves_the_actual_provider_member(string callee, string name)
    {
        var facts = Extract("Scripts/adapter.js", "this['service'](", callee + "(");
        var edges = facts.Where(f => f.FactType == FactTypes.WebFormsAngularPostHandlerCandidate).ToArray();
        Assert.Equal(2, edges.Length);
        foreach (var edge in edges)
        {
            var ids = edge.Properties["unboundProviderGapIds"].Split(',');
            var gap = Assert.Single(facts, f => ids.Contains(f.FactId) && f.Properties.GetValueOrDefault("tokenKind") == "call-expression");
            Assert.Equal("AngularUnboundProviderIdentity", gap.Properties["gapKind"]);
            Assert.Equal("unsupported-receiver", gap.Properties["ownerFailure"]);
            Assert.Equal(name, gap.Properties["providerMember"]);
            Assert.Equal("0", gap.Properties["factoryCount"]);
            Assert.Equal("Scripts/adapter.js", gap.Evidence.FilePath);
            Assert.Equal(3, gap.Evidence.StartLine);
        }
    }

    [Theory]
    [InlineData("this['service'](selectFeature()", "this['service']('records'", "AngularServiceModuleUnavailable")]
    [InlineData("this['service'](selectFeature()", "unknownOwner.service(selectFeature()", "AngularServiceModuleUnavailable")]
    [InlineData("this['service'](selectFeature()", "exampleApp.service(selectFeature()", "UnsupportedAngularServiceRegistration")]
    public void Other_potential_competitors_still_block_the_endpoint(string from, string to, string reason)
    {
        var facts = Extract("Scripts/adapter.js", from, to);
        Assert.DoesNotContain(facts, f => f.FactType == FactTypes.WebFormsAngularPostHandlerCandidate);
        Assert.Contains(facts, f => f.Properties.GetValueOrDefault("gapKind") == reason);
    }

    [Theory]
    [InlineData("unknownOwner", "missing-alias-declaration", "Identifier")]
    [InlineData("obtainOwner()", "unsupported-receiver", "CallExpression")]
    [InlineData("this", "unsupported-receiver", "ThisExpression")]
    [InlineData("registry.hooks", "unsupported-receiver", "MemberExpression")]
    public void Failed_owner_check_retains_the_specific_provider_site(string receiver, string failure, string kind)
    {
        var facts = Extract("Scripts/adapter.js", "this['service'](selectFeature()", receiver + ".service('records'");
        var gaps = facts.Where(f => f.Properties.GetValueOrDefault("gapKind") == "AngularServiceModuleUnavailable").ToArray();
        Assert.Equal(2, gaps.Length);
        Assert.All(gaps, gap =>
        {
            Assert.Equal(failure, gap.Properties["ownerFailure"]);
            Assert.Equal(kind, gap.Properties["receiverKind"]);
            Assert.Equal("string-literal", gap.Properties["tokenKind"]);
            Assert.Equal("0", gap.Properties["factoryCount"]);
            Assert.Equal("Scripts/adapter.js", gap.Evidence.FilePath);
            Assert.Equal(3, gap.Evidence.StartLine);
            Assert.Contains(facts, f => f.FactType == FactTypes.WebFormsAngularServiceCallCandidate
                && gap.Properties["supportingFactIds"].Split(',').Contains(f.FactId));
        });
    }

    [Fact]
    public void Bundle_wide_angular_write_stops_the_controller_before_a_service_call_can_be_retained()
    {
        var facts = Extract("Scripts/adapter.js", "function configureAdapter()", "(function () { angular = {}; }()); function configureAdapter()");
        Assert.DoesNotContain(facts, f => f.FactType is FactTypes.WebFormsAngularPartialClickHandlerCandidate
            or FactTypes.WebFormsAngularServiceCallCandidate or FactTypes.WebFormsAngularPostHandlerCandidate);
    }

    [Fact]
    public void Unbound_call_alone_never_supplies_a_service_registration()
    {
        var facts = Extract("Scripts/services.js", ".service(\"records\"", ".service(\"notRecords\"");
        Assert.DoesNotContain(facts, f => f.FactType is FactTypes.WebFormsAngularServiceRegistrationCandidate or FactTypes.WebFormsAngularPostHandlerCandidate);
        Assert.Contains(facts, f => f.Properties.GetValueOrDefault("gapKind") == "AngularServiceOnlyUnboundProviders");
    }

    [Theory]
    [InlineData("unknownOwner.value()", "AngularServiceModuleUnavailable")]
    [InlineData("exampleApp.value()", "UnsupportedAngularServiceRegistration")]
    public void Zero_arguments_do_not_bypass_other_owner_checks(string call, string reason)
    {
        var facts = Extract("Scripts/adapter.js", "this.value()", call);
        Assert.DoesNotContain(facts, f => f.FactType == FactTypes.WebFormsAngularPostHandlerCandidate);
        Assert.Contains(facts, f => f.Properties.GetValueOrDefault("gapKind") == reason);
    }

    [Theory]
    [InlineData(14, 2)]
    [InlineData(15, 0)]
    public void Absent_and_computed_tokens_share_the_observation_limit(int absentCount, int endpoints)
    {
        var facts = Extract("Scripts/adapter.js", "this.value();", string.Concat(Enumerable.Repeat("this.value(); ", absentCount)));
        Assert.Equal(endpoints, facts.Count(f => f.FactType == FactTypes.WebFormsAngularPostHandlerCandidate));
        Assert.Equal(endpoints == 0, facts.Any(f => f.Properties.GetValueOrDefault("gapKind") == "AngularUnboundProviderLimit"));
    }

    [Fact]
    public void Excess_unbound_calls_leave_a_limit_gap_without_a_partial_selection()
    {
        const string call = "this['service'](selectFeature(), function () {});";
        var facts = Extract("Scripts/adapter.js", call, string.Concat(Enumerable.Repeat(call, 17)));
        Assert.DoesNotContain(facts, f => f.FactType == FactTypes.WebFormsAngularPostHandlerCandidate);
        Assert.Contains(facts, f => f.Properties.GetValueOrDefault("gapKind") == "AngularUnboundProviderLimit");
    }

    [Fact]
    public void More_than_sixteen_injected_calls_retains_the_truncation_gap()
    {
        var facts = Extract("Scripts/panel.js", "client.submit(rows).then(function () {});",
            string.Concat(Enumerable.Repeat("client.submit(rows);\n", 17)));
        Assert.Contains(facts, f => f.Properties.GetValueOrDefault("gapKind") == "AngularServiceCallLimit");
    }

    private static CodeFact[] Extract(string? file = null, string? from = null, string? to = null, string adapterName = "adapter.js")
    {
        using var temp = new TempDirectory();
        var source = Path.Combine(SqlDatabaseMatrixCells.Repo(), "samples/webforms-service-post");
        foreach (var path in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, path);
            var output = Path.Combine(temp.Path, relative.Replace("adapter.js", adapterName, StringComparison.Ordinal));
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            var text = File.ReadAllText(path).ReplaceLineEndings("\n").Replace("adapter.js", adapterName, StringComparison.Ordinal);
            File.WriteAllText(output, relative.Replace('\\', '/') == file ? text.Replace(from!, to) : text);
        }
        var inventory = FileInventory.Collect(temp.Path);
        var frontend = LegacyWebFormsExtractor.Extract(temp.Path, Manifest, inventory, [], WebFormsIisMount.Parse("/Portal/Desk=."));
        return frontend.Concat(LegacyAspNetExtractor.Extract(temp.Path, Manifest, inventory, frontend)).ToArray();
    }
}
