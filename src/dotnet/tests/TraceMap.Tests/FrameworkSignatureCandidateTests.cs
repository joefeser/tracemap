using Mono.Cecil;
using Mono.Cecil.Cil;
using TraceMap.Core;
using TraceMap.Combine;
using TraceMap.Reporting;
using TraceMap.Storage;

namespace TraceMap.Tests;

[Collection("Git metadata sensitive")]
public sealed class FrameworkSignatureCandidateTests
{
    private static string C(string value) => $"{value.Length}:{value}";
    private static string Assembly(string name, string version, string token = "b77a5c561934e089", string culture = "neutral")
        => $"assembly:name:{C(name)}|version:{C(version)}|culture:{C(culture)}|publicKeyToken:{C(token)}";
    private static string Type(string version, string name = "ArrayList", string assembly = "mscorlib", string token = "b77a5c561934e089", string culture = "neutral")
        => $"scope({Assembly(assembly, version, token, culture)})type(namespace:18:System.Collections|names:{C(name)})";
    private static string Key(string type, string outerVersion = "1.0.0.0", string suffix = "")
        => $"memberref|type:scope({Assembly("PublicProvider", outerVersion, "null")})type(namespace:0:|names:8:Provider)|member:5:Query|arity:0|call:default|hasThis:false|explicitThis:false|({type})->{type}{suffix}";
    private static string? Normalize(string value) => CombinedDependencyPathReporter.FrameworkSignatureCandidateKey(value);

    [Theory]
    [InlineData("plain")]
    [InlineData("array")]
    [InlineData("byref")]
    [InlineData("pointer")]
    [InlineData("generic")]
    [InlineData("nested")]
    [InlineData("modreq")]
    [InlineData("modopt")]
    [InlineData("System")]
    [InlineData("System.Data")]
    public void Embedded_framework_scopes_compare_without_erasing_other_structure(string shape)
    {
        string Shape(string v) => shape switch
        {
            "array" => Type(v) + "[]", "byref" => Type(v) + "&", "pointer" => Type(v) + "*",
            "generic" => Type(v, "List`1") + "<" + Type(v) + ">",
            "nested" => Type(v).Replace("names:9:ArrayList", "names:5:Outer5:Inner", StringComparison.Ordinal),
            "modreq" => $"modreq({Type(v)}) {Type(v)}", "modopt" => $"modopt({Type(v)}) {Type(v)}",
            "System" or "System.Data" => Type(v, assembly: shape), _ => Type(v)
        };
        var old = Key(Shape("2.0.0.0"));
        var current = Key(Shape("4.0.0.0"));
        Assert.NotEqual(old, current);
        Assert.NotNull(Normalize(old));
        Assert.Equal(Normalize(current), Normalize(old));
    }

    [Theory]
    [InlineData("outer-version")]
    [InlineData("outer-name")]
    [InlineData("custom-assembly")]
    [InlineData("token")]
    [InlineData("culture")]
    [InlineData("type-name")]
    [InlineData("namespace")]
    [InlineData("parameter-count")]
    [InlineData("return-type")]
    [InlineData("method-name")]
    [InlineData("arity")]
    [InlineData("static-instance")]
    [InlineData("unknown-version")]
    [InlineData("injected-name")]
    public void Other_identity_differences_do_not_merge(string difference)
    {
        var baseline = Key(Type("4.0.0.0"));
        var other = difference switch
        {
            "outer-version" => Key(Type("2.0.0.0"), "2.0.0.0"),
            "outer-name" => baseline.Replace("14:PublicProvider", "13:OtherProvider", StringComparison.Ordinal),
            "custom-assembly" => Key(Type("2.0.0.0", assembly: "OtherLib")),
            "token" => Key(Type("2.0.0.0", token: "0000000000000000")),
            "culture" => Key(Type("2.0.0.0", culture: "en-US")),
            "type-name" => Key(Type("2.0.0.0", name: "OtherType")),
            "namespace" => baseline.Replace("18:System.Collections", "18:Custom.Collections", StringComparison.Ordinal),
            "parameter-count" => Key(Type("2.0.0.0") + "," + Type("2.0.0.0")),
            "return-type" => baseline.Replace("->" + Type("4.0.0.0"), "->" + Type("4.0.0.0", "OtherType"), StringComparison.Ordinal),
            "method-name" => baseline.Replace("5:Query", "5:Other", StringComparison.Ordinal),
            "arity" => baseline.Replace("arity:0", "arity:1", StringComparison.Ordinal),
            "static-instance" => baseline.Replace("hasThis:false", "hasThis:true", StringComparison.Ordinal),
            "unknown-version" => Key(Type("9.0.0.0")),
            _ => Key(Type("2.0.0.0", name: "X|version:7:4.0.0.0"))
        };
        Assert.NotEqual(Normalize(baseline), Normalize(other));
    }

    [Theory]
    [InlineData("truncated")]
    [InlineData("length")]
    [InlineData("trailing")]
    [InlineData("oversized")]
    [InlineData("deep")]
    [InlineData("fnptr")]
    [InlineData("shaped-array")]
    public void Unsupported_or_malformed_identity_has_no_fallback(string shape)
    {
        var input = Key(Type("2.0.0.0"));
        input = shape switch
        {
            "truncated" => input[..^1], "length" => input.Replace("8:Provider", "999:Provider", StringComparison.Ordinal),
            "trailing" => input + "junk", "oversized" => new string('x', 65_537),
            "deep" => Key(string.Concat(Enumerable.Repeat(Type("2.0.0.0") + "<", 34)) + Type("2.0.0.0") + new string('>', 34)),
            "fnptr" => Key("fnptr:unsupported"), _ => Key(Type("2.0.0.0") + "[rank:2;sizes:-;lowerBounds:-]")
        };
        Assert.Null(Normalize(input));
    }

    [Theory]
    [InlineData(2, 2, "valid")]
    [InlineData(4, 4, "valid")]
    [InlineData(4, 2, "valid")]
    [InlineData(2, 4, "valid")]
    [InlineData(4, 2, "exact-wins")]
    [InlineData(4, 2, "ambiguous")]
    [InlineData(4, 4, "exact-ambiguous")]
    [InlineData(4, 2, "virtual")]
    [InlineData(4, 2, "nonvirtual")]
    [InlineData(4, 2, "missing-flags")]
    [InlineData(4, 2, "missing-receipt")]
    [InlineData(4, 2, "wrong-receipt")]
    [InlineData(4, 2, "depth")]
    [InlineData(4, 2, "provider-version")]
    [InlineData(4, 2, "provider-name")]
    [InlineData(4, 2, "framework-token")]
    [InlineData(4, 2, "framework-culture")]
    [InlineData(4, 2, "parameter-type")]
    [InlineData(4, 2, "return-type")]
    [InlineData(4, 2, "ineligible-provenance")]
    [InlineData(4, 2, "bound-provenance")]
    [InlineData(4, 2, "return-flow")]
    [InlineData(4, 2, "work")]
    [InlineData(4, 2, "fill")]
    [InlineData(4, 2, "appcode")]
    [InlineData(2, 2, "base-fill")]
    [InlineData(4, 4, "base-fill")]
    [InlineData(4, 2, "base-fill")]
    [InlineData(2, 4, "base-fill")]
    [InlineData(2, 2, "preserved-base-fill")]
    [InlineData(4, 4, "preserved-base-fill")]
    [InlineData(2, 4, "preserved-base-fill")]
    [InlineData(4, 2, "preserved-base-fill")]
    [InlineData(4, 4, "fake-base-fill-namespace")]
    [InlineData(4, 4, "fake-base-fill-assembly")]
    [InlineData(4, 4, "fake-base-fill-token")]
    [InlineData(4, 4, "fake-base-fill-culture")]
    [InlineData(2, 2, "unbridged-helper")]
    [InlineData(4, 4, "unbridged-helper")]
    [InlineData(2, 2, "byref-helper")]
    [InlineData(4, 4, "byref-helper")]
    [InlineData(4, 4, "wrong-mode-helper")]
    [InlineData(4, 4, "invalid-mode-helper")]
    [InlineData(4, 4, "mode-count-helper")]
    [InlineData(4, 4, "missing-mode-helper")]
    [InlineData(2, 2, "nested-type")]
    [InlineData(4, 4, "nested-type")]
    [InlineData(4, 4, "nested-namespace-decoy")]
    [InlineData(4, 4, "nested-missing-namespace")]
    [InlineData(4, 4, "nested-ambiguous")]
    [InlineData(2, 2, "generic-parameter")]
    [InlineData(4, 4, "generic-parameter")]
    [InlineData(4, 4, "generic-wrong-argument")]
    [InlineData(4, 4, "generic-missing-import")]
    [InlineData(2, 2, "generic-method")]
    [InlineData(4, 4, "generic-method")]
    [InlineData(4, 4, "generic-method-wrong-ordinal")]
    [InlineData(4, 4, "generic-method-missing-evidence")]
    [InlineData(4, 4, "generic-method-duplicate-name")]
    [InlineData(4, 4, "generic-method-source-collision")]
    [InlineData(2, 2, "generic-configured")]
    [InlineData(4, 4, "generic-configured")]
    [InlineData(4, 4, "generic-configured-missing-proof")]
    [InlineData(4, 4, "generic-configured-wrong-kind")]
    [InlineData(4, 4, "generic-configured-ambiguous")]
    [InlineData(2, 2, "generic-configured-events")]
    [InlineData(4, 4, "generic-configured-events")]
    public async Task Mixed_framework_source_to_database_path_preserves_candidate_boundary(int callerVersion, int providerVersion, string scenario)
    {
        using var temp = new TempDirectory();
        var callerPath = Path.Combine(temp.Path, "PublicWebsite.dll");
        var providerPath = Path.Combine(temp.Path, "PublicProvider.dll");
        var instanceCall = scenario is "virtual" or "nonvirtual" or "missing-flags";
        Build(callerPath, callerVersion, true, instanceCall, scenario.EndsWith("-helper", StringComparison.Ordinal) ? "unbridged-helper" : scenario.StartsWith("nested-", StringComparison.Ordinal) || scenario.StartsWith("generic-", StringComparison.Ordinal) || scenario is "return-flow" or "appcode" or "preserved-base-fill" ? scenario : "valid", providerVersion);
        Build(providerPath, providerVersion, false, instanceCall, scenario);
        var paths = new List<string> { callerPath, providerPath };
        if (scenario == "appcode")
        {
            var forwarder = Path.Combine(temp.Path, "PublicAppCode.dll");
            BuildForwarder(forwarder, callerVersion);
            paths.Add(forwarder);
        }
        if (scenario is "exact-wins" or "ambiguous" or "exact-ambiguous")
        {
            var extra = Path.Combine(temp.Path, "extra", "PublicProvider.dll");
            Directory.CreateDirectory(Path.GetDirectoryName(extra)!);
            Build(extra, scenario == "exact-wins" ? callerVersion : providerVersion, false, false);
            paths.Add(extra);
            if (scenario == "exact-ambiguous")
            {
                var alternate = Path.Combine(temp.Path, "alternate", "PublicProvider.dll");
                Directory.CreateDirectory(Path.GetDirectoryName(alternate)!);
                Build(alternate, 2, false, false);
                paths.Add(alternate);
            }
        }
        var repo = FindRepo();
        var scan = ScanEngine.Scan(new ScanOptions(Path.Combine(repo, "samples", "messy-dotnet-workspace", scenario.StartsWith("generic-configured", StringComparison.Ordinal) ? "vb-configured-imports" : scenario.StartsWith("generic-method", StringComparison.Ordinal) ? "vb-generic-method-publish" : scenario.StartsWith("generic-", StringComparison.Ordinal) ? "vb-generic-publish" : scenario.StartsWith("nested-", StringComparison.Ordinal) ? "vb-nested-publish" : scenario.EndsWith("-helper", StringComparison.Ordinal) ? "vb-unbridged-helper" : "framework-signature-boundary"),
            Path.Combine(temp.Path, "scan"), CompiledInputPaths: paths, IlBodyEvidence: true));
        var rootName = scenario == "generic-configured-events" ? "Handle" : "Start";
        var declaration = Assert.Single(scan.Facts, f => f.FactType == FactTypes.MethodDeclared && f.TargetSymbol == rootName);
        var compiledStarts = scan.Facts.Where(f => f.FactType == FactTypes.ManagedMethodDeclared && f.Properties.GetValueOrDefault("metadataName") == rootName).ToArray();
        Assert.Equal(scenario == "generic-configured-ambiguous" ? 2 : 1, compiledStarts.Length);
        var compiled = compiledStarts[0];
        var facts = scan.Facts.ToList();
        if (scenario.StartsWith("generic-configured", StringComparison.Ordinal))
        {
            Assert.Equal("", declaration.Properties["importedNamespaces"]);
            Assert.Contains("System.Collections.Generic", declaration.Properties["configuredImportedNamespaces"]);
            if (scenario is "generic-configured-missing-proof" or "generic-configured-wrong-kind")
                facts = facts.Select(f => f.FactId == declaration.FactId ? f with
                { Properties = new Dictionary<string, string>(f.Properties)
                    { [scenario == "generic-configured-missing-proof" ? "configuredImportsSha256" : "configuredImportsKind"] = "" } } : f).ToList();
        }
        if (scenario.StartsWith("generic-method", StringComparison.Ordinal))
        {
            Assert.Equal("TLeft;TRight", declaration.Properties["methodTypeParameters"]);
            if (scenario == "generic-method-source-collision") facts.Add(declaration with { FactId = declaration.FactId + "-duplicate-source" });
            if (scenario == "generic-method-missing-evidence")
                facts = facts.Select(f => f.FactId == declaration.FactId ? f with
                    { Properties = f.Properties.Where(p => p.Key != "methodTypeParameters").ToDictionary() } : f).ToList();
            if (scenario == "generic-method-duplicate-name")
                facts = facts.Select(f => f.FactId == declaration.FactId ? f with
                    { Properties = new Dictionary<string, string>(f.Properties) { ["methodTypeParameters"] = "TLeft;tleft" } } : f).ToList();
        }
        if (scenario == "generic-missing-import")
            facts = facts.Select(f => f.FactId == declaration.FactId ? f with
                { Properties = new Dictionary<string, string>(f.Properties) { ["importedNamespaces"] = "" } } : f).ToList();
        if (scenario == "nested-missing-namespace")
            facts = facts.Select(f => f.FactId == declaration.FactId ? f with
                { Properties = f.Properties.Where(p => p.Key != "lexicalNamespace").ToDictionary() } : f).ToList();
        if (scenario == "nested-ambiguous") facts.Add(compiled with { FactId = compiled.FactId + "-duplicate" });
        if (scenario.EndsWith("-helper", StringComparison.Ordinal))
        {
            var helper = Assert.Single(facts, f => f.FactType == FactTypes.MethodDeclared && f.TargetSymbol == "Helper");
            Assert.Equal("byref", helper.Properties["parameterPassingModes"]);
            if (scenario != "byref-helper")
                facts = facts.Select(f => f.FactId == helper.FactId ? f with
                {
                    Properties = scenario is "unbridged-helper" or "missing-mode-helper"
                        ? f.Properties.Where(p => p.Key != "parameterPassingModes").ToDictionary()
                        : new Dictionary<string, string>(f.Properties) { ["parameterPassingModes"] = scenario switch
                        { "wrong-mode-helper" => "byval", "mode-count-helper" => "byref;byval", _ => "unknown" } }
                } : f).ToList();
            // Query the helper directly to prove the new bridge, not merely the
            // existing compiled Start -> Helper route used by the historical case.
            if (scenario != "unbridged-helper") declaration = helper;
        }
        if (scenario == "missing-flags")
            facts = facts.Select(f => f.FactType == FactTypes.ManagedMethodDeclared && f.Properties.GetValueOrDefault("metadataName") == "Query"
                ? f with { Properties = f.Properties.Where(p => p.Key != "methodDispatchFlags").ToDictionary() } : f).ToList();
        if (scenario is "ineligible-provenance" or "bound-provenance")
            facts = facts.Select(f => f.FactType == FactTypes.ManagedMethodDeclared && f.Properties.GetValueOrDefault("metadataName") == "Query"
                ? f with { Properties = new Dictionary<string, string>(f.Properties) { ["provenanceState"] = scenario == "bound-provenance" ? "bound" : "unavailable" } } : f).ToList();
        const string receiptHash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        if (scenario != "missing-receipt")
        {
            facts.Add(declaration with { FactId = declaration.FactId + "-receipt", FactType = FactTypes.WebFormsPublishSourceBound,
                RuleId = RuleIds.LegacyWebFormsPublishMap, EvidenceTier = EvidenceTiers.Tier2Structural,
                Properties = new Dictionary<string, string> { ["sourcePath"] = declaration.Evidence.FilePath,
                    ["boundedInputSha256"] = receiptHash, ["generatorSha256"] = receiptHash } });
            facts.Add(compiled with { FactId = compiled.FactId + "-receipt", FactType = FactTypes.WebFormsPublishAssemblyBound,
                RuleId = RuleIds.LegacyWebFormsPublishMap, EvidenceTier = EvidenceTiers.Tier2Structural,
                Properties = new Dictionary<string, string> { ["assemblyRawSha256"] = scenario == "wrong-receipt" ? new string('f', 64) : compiled.Properties["rawFileSha256"],
                    ["boundedInputSha256"] = receiptHash, ["generatorSha256"] = receiptHash } });
        }
        var index = Path.Combine(temp.Path, "index.sqlite");
        var combined = Path.Combine(temp.Path, "combined.sqlite");
        SqliteIndexWriter.Write(index, scan.Manifest, facts);
        var composition = await CombinedIndexBuilder.CombineAsync(new([index], combined, ["synthetic-framework-boundary"]));
        var source = Assert.Single(composition.Sources);
        var options = new CombinedDependencyPathOptions(combined, temp.Path, ToSurface: "database-api", IncludeLegacyRoots: true,
            MaxDepth: scenario == "depth" ? 1 : 12) { ExactFromSymbol = true, MaxTraversalWork = scenario == "work" ? 1 : 10000 };
        var roots = new[] { new CombinedPathSymbolRoot(source.SourceIndexId, source.ScanId, source.CommitSha, declaration.Properties["memberIdentity"]) };
        var report = await CombinedDependencyPathReporter.BuildSelectedSymbolsAsync(options, roots, true);
        if (scenario is "missing-receipt" or "wrong-receipt" or "ambiguous" or "exact-ambiguous" or "work" or "fake-base-fill-namespace" or "fake-base-fill-assembly" or "fake-base-fill-token" or "fake-base-fill-culture"
            or "provider-version" or "provider-name" or "framework-token" or "framework-culture" or "parameter-type" or "return-type" or "ineligible-provenance"
            or "wrong-mode-helper" or "invalid-mode-helper" or "mode-count-helper" or "missing-mode-helper"
            or "nested-namespace-decoy" or "nested-missing-namespace" or "nested-ambiguous"
            or "generic-wrong-argument" or "generic-missing-import" or "generic-method-wrong-ordinal"
            or "generic-method-missing-evidence" or "generic-method-duplicate-name" or "generic-method-source-collision"
            or "generic-configured-missing-proof" or "generic-configured-wrong-kind" or "generic-configured-ambiguous")
        {
            Assert.Empty(report.Paths);
            if (scenario == "generic-configured-ambiguous")
                Assert.Contains(report.Gaps, gap => gap.Reason == "multiple-qualified-compatible-members");
            if (scenario == "generic-method-source-collision")
                Assert.Contains(report.Gaps, gap => gap.Reason == "source-member-identity-ambiguous");
            if (scenario.StartsWith("nested-", StringComparison.Ordinal))
                Assert.Contains(report.Gaps, gap => gap.GapKind == "ProjectlessPublishMemberAmbiguous" &&
                    gap.Reason == (scenario == "nested-ambiguous" ? "multiple-qualified-compatible-members" : "qualified-containing-type-unmatched"));
            if (scenario.EndsWith("-helper", StringComparison.Ordinal))
                Assert.Contains(report.Gaps, gap => gap.GapKind == "ProjectlessPublishMemberAmbiguous" && gap.Reason == "parameter-shape-unmatched");
            if (scenario == "work") Assert.True(report.Summary.Truncated);
            if (scenario == "ambiguous") Assert.Contains(report.Gaps, g => g.GapKind == "CompiledIlFrameworkVersionAmbiguous");
            if (scenario == "exact-ambiguous") Assert.Contains(report.Gaps, g => g.GapKind == "CompiledIlTargetAmbiguous");
            return;
        }
        Assert.True(report.Paths.Count > 0, string.Join("\n", report.Gaps.Select(g => g.GapKind + ":" + g.Reason)) + "\n" +
            string.Join("\n", facts.Where(f => f.FactType == FactTypes.ManagedMethodDeclared && f.Properties.GetValueOrDefault("metadataName") == "Helper").Select(f => f.Properties.GetValueOrDefault("signature"))));
        var path = Assert.Single(report.Paths);
        if (scenario == "return-flow")
        {
            Assert.Contains("IlCommandReturnFrameworkVersionBindingUnproven", path.Nodes.Last().CommandBinding!.CommandTextFromPath!.Gaps);
            Assert.Contains(report.Gaps, g => g.GapKind == "CompiledIlFrameworkVersionCandidate");
            return;
        }
        if (scenario == "depth")
        {
            Assert.Contains(path.Notes, n => n.Code == "TerminalReachabilityPrewalk");
            Assert.True(report.Summary.Truncated);
        }
        var endpoint = scenario is "fill" or "appcode" or "base-fill" or "preserved-base-fill" ? "DbDataAdapter.Fill" : "SqlCommand.ExecuteScalar";
        Assert.Equal(endpoint, path.Nodes.Last().SurfaceName);
        if (scenario == "appcode")
        {
            Assert.Contains(path.Nodes, n => n.SymbolId?.Contains("PublicAppCode", StringComparison.Ordinal) == true);
            Assert.Contains(path.Nodes, n => n.SymbolId?.Contains("PublicProvider", StringComparison.Ordinal) == true);
        }
        var candidate = callerVersion != providerVersion && scenario is not ("exact-wins" or "preserved-base-fill");
        Assert.Equal(candidate, path.Edges.Any(e => e.EdgeKind == "compiled-il-framework-version-candidate"));
        if (candidate)
        {
            var edge = Assert.Single(path.Edges, e => e.EdgeKind == "compiled-il-framework-version-candidate");
            Assert.Equal(EvidenceTiers.Tier3SyntaxOrTextual, edge.EvidenceTier);
            Assert.Equal("NeedsReviewStaticPath", path.Classification);
            Assert.Contains(path.Notes, n => n.Code == "FrameworkSignatureCandidate");
            Assert.Contains(report.Gaps, g => g.Reason == "embedded-framework-version-difference-runtime-binding-unproven");
            Assert.Contains("IlCommandFrameworkVersionBindingUnproven", path.Nodes.Last().CommandBinding!.CommandTextFromPath!.Gaps);
            if (scenario is "virtual" or "missing-flags") Assert.Contains(report.Gaps, g => g.GapKind == "CompiledIlVirtualDispatchUnproven");
            if (scenario == "nonvirtual") Assert.DoesNotContain(report.Gaps, g => g.GapKind == "CompiledIlVirtualDispatchUnproven");
        }
        RetainedMethodGraph? graph = null;
        await CombinedDependencyPathReporter.BuildSelectedSymbolsAsync(options with { ToSurface = null, MethodGraphObserver = value => graph = value }, roots, true);
        Assert.NotNull(graph);
        if (scenario is "unbridged-helper" or "byref-helper")
        {
            Assert.Contains(path.Nodes, node => node.SymbolId?.Contains("|method:6:Helper|", StringComparison.Ordinal) == true);
            var helper = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.MethodDeclared && fact.TargetSymbol == "Helper");
            var helperNodes = graph.Nodes.Where(node => node.SymbolId == helper.Properties["memberIdentity"]).Select(node => node.NodeId).ToHashSet();
            if (scenario == "byref-helper")
                Assert.Contains(graph.Edges, edge => edge.EdgeKind == "projectless-publish-member-candidate" && helperNodes.Contains(edge.FromNodeId));
            else
            {
                Assert.DoesNotContain(graph.Edges, edge => edge.EdgeKind == "projectless-publish-member-candidate" && helperNodes.Contains(edge.FromNodeId));
                Assert.Contains(report.Gaps, gap => gap.GapKind == "ProjectlessPublishMemberAmbiguous" && gap.Reason == "parameter-shape-unmatched");
            }
        }
        if (scenario is "base-fill" or "preserved-base-fill")
        {
            Assert.Contains(graph.Calls, c => c.EncodedTarget?.Contains("names:11:DataAdapter)|member:4:Fill|", StringComparison.Ordinal) == true);
            Assert.Equal(candidate, report.Gaps.Any(g => g.GapKind == "CompiledIlFrameworkVersionCandidate"));
            var fillOnly = await CombinedDependencyPathReporter.BuildSelectedSymbolsAsync(options with { SurfaceName = "DbDataAdapter.Fill" }, roots, true);
            Assert.Single(fillOnly.Paths);
        }
        if (scenario == "depth") Assert.Empty(graph.CommandTraces);
        else Assert.NotEmpty(graph.CommandTraces);
        if (scenario != "depth")
        {
            var compiledReport = await CombinedDependencyPathReporter.BuildSelectedSymbolsAsync(options with { CompiledOnly = true }, roots, true);
            var compiledPath = Assert.Single(compiledReport.Paths);
            Assert.Equal(candidate, compiledPath.Edges.Any(e => e.EdgeKind == "compiled-il-framework-version-candidate"));
            Assert.Equal(endpoint, compiledPath.Nodes.Last().SurfaceName);
        }
        var handoff = GroupedCompiledPathHandoffBuilder.Create(report, new string('a', 64));
        var written = await GroupedCompiledPathReportWriter.WriteAsync(handoff, Path.Combine(temp.Path, "report"));
        var html = await File.ReadAllTextAsync(written.HtmlPath);
        Assert.Contains(endpoint, html);
        if (scenario is "base-fill" or "preserved-base-fill")
            Assert.Contains("historical DbDataAdapter.Fill surface groups", html);
        if (candidate) Assert.Contains("Framework-version candidate routes are present (Tier3)", html);
    }

    private static void Build(string path, int version, bool caller, bool virtualCall, string scenario = "valid", int importedVersion = 2)
    {
        using var assembly = AssemblyDefinition.CreateAssembly(new(caller ? "PublicWebsite" : "PublicProvider", new Version(1, 0, 0, 0)),
            caller ? "PublicWebsite.dll" : "PublicProvider.dll", ModuleKind.Dll);
        var module = assembly.MainModule;
        if (scenario == "provider-version") assembly.Name.Version = new Version(2, 0, 0, 0);
        if (scenario == "provider-name") assembly.Name.Name = "OtherProvider";
        module.RuntimeVersion = version == 2 ? "v2.0.50727" : "v4.0.30319";
        var core = (AssemblyNameReference)module.TypeSystem.Object.Scope;
        core.Version = new Version(version, 0, 0, 0);
        if (scenario == "framework-token") core.PublicKeyToken = new byte[8];
        if (scenario == "framework-culture") core.Culture = "en-US";
        var data = new AssemblyNameReference("System.Data", new Version(version, 0, 0, 0))
            { PublicKeyToken = Convert.FromHexString("b77a5c561934e089") };
        module.AssemblyReferences.Add(data);
        var list = new TypeReference("System.Collections", "ArrayList", module, core);
        var dataset = new TypeReference("System.Data", "DataSet", module, data);
        if (caller && scenario == "preserved-base-fill")
        {
            // Preserve the provider's embedded signature independently of the
            // caller framework. Only 4-to-2 was observed in the native fixture;
            // the other pairs are synthetic metadata controls, not runtime proof.
            var importedCore = new AssemblyNameReference("mscorlib", new Version(importedVersion, 0, 0, 0)) { PublicKeyToken = Convert.FromHexString("b77a5c561934e089") };
            var importedData = new AssemblyNameReference("System.Data", new Version(importedVersion, 0, 0, 0)) { PublicKeyToken = Convert.FromHexString("b77a5c561934e089") };
            module.AssemblyReferences.Add(importedCore); module.AssemblyReferences.Add(importedData);
            list.Scope = importedCore; dataset.Scope = importedData;
        }
        if (scenario == "parameter-type") list.Name = "OtherList";
        if (scenario == "return-type") dataset.Name = "OtherData";
        var type = new TypeDefinition("", caller ? "Entry" : "Provider", TypeAttributes.Public | TypeAttributes.Class, module.TypeSystem.Object);
        if (caller && scenario.StartsWith("nested-", StringComparison.Ordinal) && scenario != "nested-namespace-decoy")
        {
            var outer = new TypeDefinition("PublicDomain", "Container", TypeAttributes.Public | TypeAttributes.Class, module.TypeSystem.Object);
            module.Types.Add(outer);
            type.Attributes = TypeAttributes.NestedPublic | TypeAttributes.Class;
            outer.NestedTypes.Add(type);
        }
        else
        {
            if (caller && scenario == "nested-namespace-decoy") type.Namespace = "PublicDomain.Container";
            module.Types.Add(type);
        }
        if (caller)
        {
            var reference = new AssemblyNameReference(scenario == "appcode" ? "PublicAppCode" : "PublicProvider", new Version(1, 0, 0, 0));
            module.AssemblyReferences.Add(reference);
            var query = new MethodReference("Query", scenario == "return-flow" ? module.TypeSystem.String : dataset, new TypeReference("", "Provider", module, reference)) { HasThis = virtualCall };
            query.Parameters.Add(new(module.TypeSystem.String)); query.Parameters.Add(new(list));
            var start = new MethodDefinition("Start", MethodAttributes.Public | MethodAttributes.Static, module.TypeSystem.Void);
            if (scenario == "generic-configured-events")
            {
                type.Name = "Events";
                start.Name = "Handle";
                start.Parameters.Add(new(module.TypeSystem.Object));
                start.Parameters.Add(new(new TypeReference("System", "EventArgs", module, core)));
                var web = new AssemblyNameReference("System.Web", new Version(version, 0, 0, 0)) { PublicKeyToken = Convert.FromHexString("b03f5f7f11d50a3a") };
                module.AssemblyReferences.Add(web);
                start.Parameters.Add(new(new TypeReference("System.Web.UI", "Control", module, web)));
            }
            else if (scenario.StartsWith("generic-", StringComparison.Ordinal))
            {
                var dictionary = new GenericInstanceType(new TypeReference("System.Collections.Generic", "Dictionary`2", module, core));
                if (scenario.StartsWith("generic-method", StringComparison.Ordinal))
                {
                    var left = new GenericParameter("TLeft", start);
                    var right = new GenericParameter("TRight", start);
                    start.GenericParameters.Add(left); start.GenericParameters.Add(right);
                    dictionary.GenericArguments.Add(scenario == "generic-method-wrong-ordinal" ? left : right);
                    dictionary.GenericArguments.Add(scenario == "generic-method-wrong-ordinal" ? right : left);
                }
                else
                {
                    dictionary.GenericArguments.Add(module.TypeSystem.String);
                    dictionary.GenericArguments.Add(scenario == "generic-wrong-argument" ? module.TypeSystem.String : module.TypeSystem.Int32);
                }
                start.Parameters.Add(new(dictionary));
                if (scenario == "generic-configured-ambiguous")
                {
                    var other = new GenericInstanceType(new TypeReference("Other", "Dictionary`2", module, core));
                    other.GenericArguments.Add(module.TypeSystem.String);
                    other.GenericArguments.Add(module.TypeSystem.Int32);
                    var overload = new MethodDefinition("Start", MethodAttributes.Public | MethodAttributes.Static, module.TypeSystem.Void);
                    overload.Parameters.Add(new(other));
                    overload.Body.GetILProcessor().Emit(OpCodes.Ret);
                    type.Methods.Add(overload);
                }
            }
            type.Methods.Add(start);
            var il = start.Body.GetILProcessor();
            if (scenario == "unbridged-helper")
            {
                var helper = new MethodDefinition("Helper", MethodAttributes.Private | MethodAttributes.Static, dataset);
                helper.Parameters.Add(new(new ByReferenceType(module.TypeSystem.Int32)));
                type.Methods.Add(helper);
                var helperIl = helper.Body.GetILProcessor();
                helperIl.Emit(OpCodes.Ldstr, "synthetic command"); helperIl.Emit(OpCodes.Ldnull);
                helperIl.Emit(OpCodes.Call, query); helperIl.Emit(OpCodes.Ret);
                start.Body.InitLocals = true;
                var id = new VariableDefinition(module.TypeSystem.Int32);
                start.Body.Variables.Add(id);
                il.Emit(OpCodes.Ldloca, id); il.Emit(OpCodes.Call, helper);
                il.Emit(OpCodes.Pop); il.Emit(OpCodes.Ret);
                assembly.Write(path);
                return;
            }
            if (virtualCall) il.Emit(OpCodes.Ldnull);
            il.Emit(OpCodes.Ldstr, "synthetic command"); il.Emit(OpCodes.Ldnull);
            il.Emit(virtualCall ? OpCodes.Callvirt : OpCodes.Call, query);
            if (scenario == "return-flow")
            {
                var command = new TypeReference("System.Data.SqlClient", "SqlCommand", module, data);
                var ctor = new MethodReference(".ctor", module.TypeSystem.Void, command) { HasThis = true };
                ctor.Parameters.Add(new(module.TypeSystem.String));
                il.Emit(OpCodes.Newobj, ctor);
                il.Emit(OpCodes.Callvirt, new MethodReference("ExecuteScalar", module.TypeSystem.Object, command) { HasThis = true });
            }
            il.Emit(OpCodes.Pop); il.Emit(OpCodes.Ret);
        }
        else
        {
            var dispatch = !virtualCall ? MethodAttributes.Static : scenario is "nonvirtual" or "missing-flags"
                ? (MethodAttributes)0 : MethodAttributes.Virtual | MethodAttributes.NewSlot;
            var query = new MethodDefinition("Query", MethodAttributes.Public | dispatch, scenario == "return-flow" ? module.TypeSystem.String : dataset);
            query.Parameters.Add(new(module.TypeSystem.String)); query.Parameters.Add(new(list)); type.Methods.Add(query);
            var command = new TypeReference("System.Data.SqlClient", "SqlCommand", module, data);
            var ctor = new MethodReference(".ctor", module.TypeSystem.Void, command) { HasThis = true };
            ctor.Parameters.Add(new(module.TypeSystem.String));
            var execute = new MethodReference("ExecuteScalar", module.TypeSystem.Object, command) { HasThis = true };
            var il = query.Body.GetILProcessor();
            if (scenario == "return-flow") il.Emit(OpCodes.Ldstr, "synthetic returned command");
            else
            {
                il.Emit(virtualCall ? OpCodes.Ldarg_1 : OpCodes.Ldarg_0); il.Emit(OpCodes.Newobj, ctor);
                if (scenario is "fill" or "appcode" or "base-fill" or "preserved-base-fill" or "fake-base-fill-namespace" or "fake-base-fill-assembly" or "fake-base-fill-token" or "fake-base-fill-culture")
                {
                    var adapter = new TypeReference("System.Data.SqlClient", "SqlDataAdapter", module, data);
                    var adapterCtor = new MethodReference(".ctor", module.TypeSystem.Void, adapter) { HasThis = true };
                    adapterCtor.Parameters.Add(new(command));
                    var owner = new TypeReference(scenario == "fake-base-fill-namespace" ? "Other.Common" : "System.Data.Common",
                        scenario is "fill" or "appcode" ? "DbDataAdapter" : "DataAdapter", module, data);
                    if (scenario is "fake-base-fill-assembly" or "fake-base-fill-token" or "fake-base-fill-culture")
                    {
                        var fake = new AssemblyNameReference(scenario == "fake-base-fill-assembly" ? "PublicData" : "System.Data", new Version(4, 0, 0, 0))
                        {
                            PublicKeyToken = scenario == "fake-base-fill-token" ? new byte[8] : Convert.FromHexString("b77a5c561934e089"),
                            Culture = scenario == "fake-base-fill-culture" ? "en-US" : ""
                        };
                        module.AssemblyReferences.Add(fake); owner.Scope = fake;
                    }
                    var fill = new MethodReference("Fill", module.TypeSystem.Int32, owner) { HasThis = true };
                    fill.Parameters.Add(new(dataset));
                    il.Emit(OpCodes.Newobj, adapterCtor); il.Emit(OpCodes.Ldnull); il.Emit(OpCodes.Callvirt, fill);
                }
                else il.Emit(OpCodes.Callvirt, execute);
                il.Emit(OpCodes.Pop); il.Emit(OpCodes.Ldnull);
            }
            il.Emit(OpCodes.Ret);
        }
        assembly.Write(path);
    }

    private static void BuildForwarder(string path, int version)
    {
        using var assembly = AssemblyDefinition.CreateAssembly(new("PublicAppCode", new Version(1, 0, 0, 0)), "PublicAppCode.dll", ModuleKind.Dll);
        var module = assembly.MainModule;
        module.RuntimeVersion = "v4.0.30319";
        var core = (AssemblyNameReference)module.TypeSystem.Object.Scope;
        core.Version = new Version(version, 0, 0, 0);
        var data = new AssemblyNameReference("System.Data", new Version(version, 0, 0, 0)) { PublicKeyToken = Convert.FromHexString("b77a5c561934e089") };
        var provider = new AssemblyNameReference("PublicProvider", new Version(1, 0, 0, 0));
        module.AssemblyReferences.Add(data); module.AssemblyReferences.Add(provider);
        var list = new TypeReference("System.Collections", "ArrayList", module, core);
        var dataset = new TypeReference("System.Data", "DataSet", module, data);
        var type = new TypeDefinition("", "Provider", TypeAttributes.Public | TypeAttributes.Class, module.TypeSystem.Object);
        module.Types.Add(type);
        var method = new MethodDefinition("Query", MethodAttributes.Public | MethodAttributes.Static, dataset);
        method.Parameters.Add(new(module.TypeSystem.String)); method.Parameters.Add(new(list)); type.Methods.Add(method);
        var target = new MethodReference("Query", dataset, new TypeReference("", "Provider", module, provider)) { HasThis = false };
        target.Parameters.Add(new(module.TypeSystem.String)); target.Parameters.Add(new(list));
        var il = method.Body.GetILProcessor();
        il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldarg_1); il.Emit(OpCodes.Call, target); il.Emit(OpCodes.Ret);
        assembly.Write(path);
    }
    private static string FindRepo()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "AGENTS.md"))) return dir.FullName;
        throw new InvalidOperationException("Repository unavailable");
    }
}
