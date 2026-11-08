using System.Globalization;

namespace TraceMap.Core;

public static partial class LegacyWebFormsExtractor
{
    private const string ServiceLimitations = "Literal AngularJS service and POST syntax in the click's included sources. Parent partial coverage applies. Dispatch, runtime injection and handler execution are unobserved.";

    private sealed partial class AngularClickScan
    {
        private readonly ILookup<string, string> endpoints = inventory.Where(i => i.RelativePath.EndsWith(".ashx", StringComparison.OrdinalIgnoreCase))
            .Select(i => i.RelativePath).ToLookup(p => p, StringComparer.OrdinalIgnoreCase);

        private int serviceNodeBudget = 4_000_000;

        private void ChargeServiceWork(int nodes)
        {
            if (nodes > serviceNodeBudget) throw new AngularClickGap("AngularServiceWorkLimit");
            serviceNodeBudget -= nodes;
        }

        private void AddServiceRequests(WebFormsPage page, AngularJsScript.Handler handler, CodeFact clickHandler,
            AngularJsScript[] sources, SortedDictionary<string, HashSet<string>> assets, string module)
        {
            try
            {
                if (!handler.Dependencies.Any(token => token is not null && !token.StartsWith('$'))) return;
                var controller = parsed[handler.Method.File];
                ChargeServiceWork(controller.NodeCount);
                foreach (var call in controller.ServiceCalls(handler))
                {
                    var callFact = ServiceSite(FactTypes.WebFormsAngularServiceCallCandidate, call.Site, clickHandler.TargetSymbol!,
                        [clickHandler.FactId], new() { ["serviceToken"] = call.Token, ["member"] = call.Member,
                            ["injectionSlot"] = call.Slot.ToString(CultureInfo.InvariantCulture), ["clickHandlerFactId"] = clickHandler.FactId });
                    var last = callFact;
                    try
                    {
                        ChargeServiceWork(sources.Sum(source => source.NodeCount));
                        var method = AngularJsScript.ResolveService(sources, module, call);
                        if (!assets.TryGetValue(method.Member.File, out var memberIds) || !assets.TryGetValue(method.Module.File, out var moduleIds))
                            throw new AngularClickGap("AngularServiceOutsideIncludedFiles");
                        var moduleFact = ScriptOwner(method.Module, "module", module, moduleIds);
                        var receiver = moduleFact;
                        foreach (var factory in method.Factories)
                            receiver = ServiceSite(FactTypes.WebFormsAngularFactoryRegistrationCandidate, factory.Site, receiver.TargetSymbol!,
                                memberIds.Append(receiver.FactId), new() { ["module"] = module, ["factoryToken"] = factory.Token });
                        var registration = ServiceSite(FactTypes.WebFormsAngularServiceRegistrationCandidate, method.Registration, receiver.TargetSymbol!,
                            memberIds.Append(receiver.FactId), new() { ["module"] = module, ["serviceToken"] = call.Token });
                        last = ServiceSite(FactTypes.WebFormsAngularServiceMemberCandidate, method.Member, callFact.TargetSymbol!,
                            [callFact.FactId, registration.FactId], new() { ["module"] = module, ["serviceToken"] = call.Token, ["member"] = call.Member });
                        var post = AngularJsScript.ResolvePost(method);
                        var props = new SortedDictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["httpMethod"] = "POST", ["behaviorKind"] = "client-http-request", ["eventName"] = "HTTP POST",
                            ["coverageLabel"] = "bounded-angularjs-service-syntax",
                            ["httpInjectionSlot"] = post.HttpSlot.ToString(CultureInfo.InvariantCulture),
                            ["clickHandlerFactId"] = clickHandler.FactId, ["clickFactId"] = clickHandler.Properties["clickFactId"],
                            ["surfaceIdentity"] = SurfaceIdentity(page.FilePath), ["targetResolution"] = "unresolved"
                        };
                        string? gap = null;
                        try
                        {
                            if (iisMount is null) throw new AngularClickGap("AngularHttpMountUnavailable");
                            props["endpointDeclarationFile"] = ResolveMountedFile(page.FilePath, method.Member.File, post.Url, iisMount, ".ashx", endpoints);
                            props["endpointPathHash"] = FactFactory.Hash(post.Url, 64);
                            props["targetResolution"] = "operator-mounted-handler-file";
                            props["mountPrefix"] = iisMount.UrlPrefix;
                            props["repositoryWebRoot"] = iisMount.WebRoot;
                        }
                        catch (AngularClickGap ex)
                        {
                            gap = ex.Kind switch
                            {
                                "AngularScriptOutsideInventory" => "AngularHttpEndpointOutsideInventory",
                                "AmbiguousAngularMountedScript" => "AmbiguousAngularHttpEndpoint",
                                "UnsupportedAngularScriptPath" => "UnsupportedAngularHttpEndpoint",
                                _ => ex.Kind
                            };
                        }
                        last = ServiceSite(FactTypes.WebFormsClientHttpRequestCandidate, post.Site, last.TargetSymbol!, [last.FactId], props);
                        if (gap is not null) ServiceGap(last, gap);
                    }
                    catch (AngularClickGap ex) { ServiceGap(last, ex.Kind); }
                }
            }
            catch (AngularClickGap ex) { ServiceGap(clickHandler, ex.Kind); }
        }

        private CodeFact ServiceSite(string type, AngularJsScript.Site site, string source, IEnumerable<string> ids,
            SortedDictionary<string, string> properties)
        {
            var target = $"angular-service-site:{FactFactory.Hash($"{type}|{site.File}|{site.Start}", 32)}";
            var evidence = new EvidenceSpan(site.File, site.Line, site.EndLine, parsed[site.File].Hash,
                "LegacyWebFormsExtractor", ScannerVersions.LegacyWebFormsExtractor);
            var fact = AngularFact(manifest, RuleIds.LegacyWebFormsAngularServiceHttp, type, evidence, source, target, ids, properties);
            facts.Add(fact);
            return fact;
        }

        private void ServiceGap(CodeFact last, string kind) => facts.Add(AngularFact(manifest, RuleIds.LegacyWebFormsAngularServiceHttp,
            FactTypes.AnalysisGap, last.Evidence, last.TargetSymbol!, "unresolved", [last.FactId], new() { ["gapKind"] = kind }));
    }

    // Handler declarations are available only after the ASP.NET extraction stage.
    internal static void LinkAngularPostHandlers(ScanManifest manifest, IReadOnlyList<CodeFact> prior, List<CodeFact> facts)
    {
        var declarations = facts.Where(f => f.FactType == FactTypes.AspNetHandlerDeclared
                && f.Properties.GetValueOrDefault("handlerKind") == "ashx-directive" && !string.IsNullOrEmpty(f.TargetSymbol))
            .ToLookup(f => f.Evidence.FilePath, StringComparer.Ordinal);
        foreach (var request in prior.Where(f => f.RuleId == RuleIds.LegacyWebFormsAngularServiceHttp
                     && f.FactType == FactTypes.WebFormsClientHttpRequestCandidate && f.Properties.ContainsKey("endpointDeclarationFile")))
        {
            var matches = declarations[request.Properties["endpointDeclarationFile"]].Take(2).ToArray();
            if (matches.Length != 1)
            {
                facts.Add(AngularFact(manifest, RuleIds.LegacyWebFormsAngularServiceHttp, FactTypes.AnalysisGap, request.Evidence,
                    request.TargetSymbol!, "unresolved", [request.FactId], new() { ["gapKind"] = "AngularHttpHandlerDeclarationUnavailable" }));
                continue;
            }
            var handler = matches[0];
            facts.Add(AngularFact(manifest, RuleIds.LegacyWebFormsAngularServiceHttp, FactTypes.WebFormsAngularPostHandlerCandidate,
                handler.Evidence with { ExtractorId = "LegacyWebFormsExtractor", ExtractorVersion = ScannerVersions.LegacyWebFormsExtractor },
                request.TargetSymbol!, handler.TargetSymbol!, [request.FactId, handler.FactId], new()
                {
                    ["clickFactId"] = request.Properties["clickFactId"], ["clickHandlerFactId"] = request.Properties["clickHandlerFactId"],
                    ["requestFactId"] = request.FactId, ["declarationFactId"] = handler.FactId
                }));
        }
    }
}
