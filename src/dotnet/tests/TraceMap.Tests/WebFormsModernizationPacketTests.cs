using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using TraceMap.Cli;
using TraceMap.Core;
using TraceMap.Reporting;
using TraceMap.Storage;

namespace TraceMap.Tests;

public sealed class WebFormsModernizationPacketTests
{
    [Fact]
    public async Task Synthetic_scan_index_packet_is_deterministic_typed_private_and_preserves_partial_evidence()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(Path.Combine(repo, "AreaA"));
        Directory.CreateDirectory(Path.Combine(repo, "AreaB"));
        File.WriteAllText(Path.Combine(repo, "Site.Master"), "<%@ Master Language=\"C#\" %><asp:ContentPlaceHolder runat=\"server\" ID=\"Body\" />");
        File.WriteAllText(Path.Combine(repo, "Widget.ascx"), "<%@ Control Language=\"C#\" %><asp:Label runat=\"server\" ID=\"Status\" />");
        File.WriteAllText(Path.Combine(repo, "AreaA", "Default.aspx"), """
            <%@ Page Language="C#" CodeBehind="Default.aspx.cs" Inherits="Sample.AreaA.Default" MasterPageFile="~/Site.Master" Title="private-marker-value" %>
            <%@ Register TagPrefix="uc" TagName="Widget" Src="~/Widget.ascx" %>
            <asp:Content runat="server" ContentPlaceHolderID="Body"><asp:Button runat="server" ID="Save" OnClick="Save_Click" /><uc:Widget runat="server" ID="Details" /></asp:Content>
            """);
        File.WriteAllText(Path.Combine(repo, "AreaA", "Default.aspx.cs"), """
            using System;
            namespace Sample.AreaA;
            public partial class Default
            {
                public Default() { Save.Click += (sender, args) => Save_Click(sender, args); }
                protected ButtonStub Save { get; } = new();
                protected void Save_Click(object sender, EventArgs e) { }
            }
            public sealed class ButtonStub { public event EventHandler? Click; }
            """);
        File.WriteAllText(Path.Combine(repo, "AreaB", "Default.aspx"), """
            <%@ Page Language="C#" CodeBehind="Default.aspx.cs" Inherits="Sample.AreaB.Default" %>
            <asp:Button runat="server" ID="Save" OnClick="Missing_Click" />
            """);
        File.WriteAllText(Path.Combine(repo, "AreaB", "Default.aspx.cs"), "namespace Sample.AreaB; public partial class Default { } ");

        var scan = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "scan")));
        const string fixtureCommit = "0123456789abcdef0123456789abcdef01234567";
        var committedManifest = scan.Manifest with { CommitSha = fixtureCommit };
        var committedFacts = scan.Facts.Select(fact => fact with { CommitSha = fixtureCommit }).ToArray();
        var index = Path.Combine(temp.Path, "index.sqlite");
        SqliteIndexWriter.Write(index, committedManifest, committedFacts);
        var indexHash = Hash(index);
        var firstOut = Path.Combine(temp.Path, "packet-a");
        var secondOut = Path.Combine(temp.Path, "packet-b");
        var first = await WebFormsModernizationPacketReporter.WriteAsync(new(index, firstOut));
        var second = await WebFormsModernizationPacketReporter.WriteAsync(new(index, secondOut));

        Assert.Equal(indexHash, Hash(index));
        Assert.Equal(await File.ReadAllBytesAsync(first.JsonPath), await File.ReadAllBytesAsync(second.JsonPath));
        Assert.Equal(await File.ReadAllBytesAsync(first.MarkdownPath), await File.ReadAllBytesAsync(second.MarkdownPath));
        Assert.Equal(WebFormsModernizationPacketReporter.SchemaVersion, first.Packet.SchemaVersion);
        Assert.Equal("local-only", first.Packet.ClaimLevel);
        Assert.False(string.IsNullOrWhiteSpace(first.Packet.Sources.Single().RepositoryId));
        Assert.DoesNotContain("synthetic-repo", first.Packet.Sources.Single().RepositoryId, StringComparison.Ordinal);
        Assert.Equal(4, first.Packet.Surfaces.Count);
        Assert.Equal(2, first.Packet.Surfaces.Count(surface => surface.SurfaceKind == "Page"));
        Assert.Equal(2, first.Packet.Surfaces.Where(surface => surface.SurfaceKind == "Page").Select(surface => surface.SurfaceId).Distinct().Count());
        Assert.Contains(first.Packet.EventChains, chain => chain.HandlerFactId is not null && chain.TerminalKind is null);
        Assert.Contains(first.Packet.Gaps, gap => gap.Classification == "NoBackendEvidence");
        Assert.Contains(first.Packet.Gaps, gap => gap.Classification == "MissingWebFormsHandler");
        Assert.Contains(first.Packet.Gaps, gap => gap.Classification == "DynamicWebFormsEventSubscription");
        Assert.Contains(first.Packet.StructuralSliceCandidates, candidate => candidate.SurfaceIds.Count > 1);
        var areaPage = first.Packet.Surfaces.Single(surface => surface.Evidence.FilePath == "AreaA/Default.aspx");
        var widget = first.Packet.Surfaces.Single(surface => surface.Evidence.FilePath == "Widget.ascx");
        Assert.Contains(widget.SurfaceId, areaPage.CompositionTargetIds);
        Assert.Contains(first.Packet.StructuralSliceCandidates, candidate => candidate.SurfaceIds.Contains(areaPage.SurfaceId)
            && candidate.SurfaceIds.Contains(widget.SurfaceId));
        Assert.All(first.Packet.Surfaces, surface => Assert.NotEqual("unknown", surface.Evidence.CoverageLabel));
        Assert.All(first.Packet.EventChains.SelectMany(chain => chain.Evidence), evidence => Assert.NotEqual("unknown", evidence.CoverageLabel));
        var typed = JsonSerializer.Deserialize<WebFormsModernizationPacket>(await File.ReadAllTextAsync(first.JsonPath), new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(typed);
        Assert.Equal(first.Packet.PacketId, typed.PacketId);
        var markdown = await File.ReadAllTextAsync(first.MarkdownPath);
        Assert.Contains(first.Packet.PacketId, markdown, StringComparison.Ordinal);
        Assert.Contains($"Surfaces: `{first.Packet.Summary.SurfaceCount}`", markdown, StringComparison.Ordinal);
        Assert.All(first.Packet.Surfaces, surface => Assert.Contains(surface.SurfaceId, markdown, StringComparison.Ordinal));
        Assert.All(first.Packet.EventChains, chain => Assert.Contains(chain.ChainId, markdown, StringComparison.Ordinal));
        Assert.Equal(first.Packet.Summary.GapCount, first.Packet.Gaps.Count);
        Assert.DoesNotContain(temp.Path, await File.ReadAllTextAsync(first.JsonPath), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(temp.Path, markdown, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private-marker-value", await File.ReadAllTextAsync(first.JsonPath), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Existing_static_terminal_path_is_composed_without_runtime_claims()
    {
        using var temp = new TempDirectory();
        var manifest = Manifest("Succeeded");
        var surface = "webforms-surface:orders";
        var binding = Fact(manifest, FactTypes.WebFormsEventBindingDeclared, RuleIds.LegacyWebFormsEventBinding, "Pages/Orders.aspx", 12,
            source: "webforms-control:submit", target: "method:submit", contract: "Submit_Click",
            ("surfaceIdentity", surface), ("eventSourceIdentity", "webforms-control:submit"), ("eventName", "OnClick"), ("controlId", "Submit"), ("handlerName", "Submit_Click"), ("markupFile", "Pages/Orders.aspx"), ("coverageLabel", "bounded-static-webforms-event"));
        var handler = Fact(manifest, FactTypes.WebFormsHandlerResolved, RuleIds.LegacyWebFormsHandlerResolution, "Pages/Orders.aspx.cs", 24,
            source: "webforms-control:submit", target: "method:submit", contract: "Submit_Click",
            ("surfaceIdentity", surface), ("bindingFactId", binding.FactId), ("handlerSymbolId", "method:submit"), ("handlerSymbol", "method:submit"), ("handlerName", "Submit_Click"), ("controlId", "Submit"), ("eventName", "OnClick"), ("markupFile", "Pages/Orders.aspx"), ("pageTypeName", "Sample.Orders"), ("coverageLabel", "bounded-static-webforms-handler"));
        var page = Fact(manifest, FactTypes.WebFormsPageDeclared, RuleIds.LegacyWebFormsInventory, "Pages/Orders.aspx", 1,
            source: surface, target: "Sample.Orders", contract: "Orders.aspx",
            ("surfaceIdentity", surface), ("directiveKind", "Page"), ("coverageLabel", "bounded-static-webforms-inventory"));
        var call = Fact(manifest, FactTypes.CallEdge, "csharp.semantic.call.v1", "Pages/Orders.aspx.cs", 27,
            source: "method:submit", target: "method:save", contract: "Save", ("coverageLabel", "bounded-static-call"));
        var terminal = Fact(manifest, FactTypes.QueryPatternDetected, RuleIds.CSharpSyntaxQueryPattern, Path.Combine(temp.Path, "private", "Orders.cs"), 40,
            source: "method:save", target: "query-shape", contract: "query", ("operationName", "SELECT"), ("tableName", "orders"), ("columnNames", "id;state"), ("sqlSourceKind", "literal-string"), ("queryShapeHash", "shape-hash"), ("coverageLabel", "bounded-static-query"));
        var secondTerminal = Fact(manifest, FactTypes.PackageReferenced, RuleIds.ProjectFile, "App.csproj", 10,
            source: "method:save", target: "package-hash", contract: "package", ("dependencyGroup", "PackageReference"), ("dependencyScope", "runtime"), ("ecosystem", "nuget"), ("manifestKind", "csproj"), ("packageName", "package-hash"), ("packageManager", "nuget"), ("surfaceKind", "package-config"), ("version", "1.0.0"), ("coverageLabel", "bounded-static-package"));
        var unrelatedGap = Fact(manifest, FactTypes.AnalysisGap, RuleIds.LegacyWinFormsEventBinding, "Forms/Main.cs", 2,
            source: null, target: "winforms:main", contract: "gap", ("gapKind", "UnrelatedWinFormsGap"), ("coverageLabel", "reduced-static-winforms"));
        var laterPage = Fact(manifest, FactTypes.WebFormsPageDeclared, RuleIds.LegacyWebFormsInventory, "Pages/ZLater.aspx", 1,
            source: "webforms-surface:later", target: "Sample.Later", contract: "Later.aspx",
            ("surfaceIdentity", "webforms-surface:later"), ("directiveKind", "Page"), ("coverageLabel", "bounded-static-webforms-inventory"));
        var laterBinding = Fact(manifest, FactTypes.WebFormsEventBindingDeclared, RuleIds.LegacyWebFormsEventBinding, "Pages/ZLater.aspx", 12,
            source: "webforms-control:later", target: "method:later", contract: "Later_Click",
            ("surfaceIdentity", "webforms-surface:later"), ("eventSourceIdentity", "webforms-control:later"), ("eventName", "OnClick"), ("controlId", "Later"), ("handlerName", "Later_Click"), ("markupFile", "Pages/ZLater.aspx"), ("coverageLabel", "bounded-static-webforms-event"));
        var laterHandler = Fact(manifest, FactTypes.WebFormsHandlerResolved, RuleIds.LegacyWebFormsHandlerResolution, "Pages/ZLater.aspx.cs", 24,
            source: "webforms-control:later", target: "method:later", contract: "Later_Click",
            ("surfaceIdentity", "webforms-surface:later"), ("bindingFactId", laterBinding.FactId), ("handlerSymbolId", "method:later"), ("handlerSymbol", "method:later"), ("handlerName", "Later_Click"), ("controlId", "Later"), ("eventName", "OnClick"), ("markupFile", "Pages/ZLater.aspx"), ("pageTypeName", "Sample.Later"), ("coverageLabel", "bounded-static-webforms-handler"));
        var index = Path.Combine(temp.Path, "index.sqlite");
        SqliteIndexWriter.Write(index, manifest, [page, binding, handler, call, terminal, secondTerminal, unrelatedGap, laterPage, laterBinding, laterHandler]);

        var packet = await WebFormsModernizationPacketReporter.BuildAsync(new(index, Path.Combine(temp.Path, "unused"), MaxEventChains: 2));
        Assert.Equal(2, packet.EventChains.Count);
        Assert.All(packet.EventChains, chain =>
        {
            Assert.NotNull(chain.HandlerFactId);
            Assert.NotNull(chain.LegacyPathId);
            Assert.NotNull(chain.TerminalKind);
        });
        Assert.Equal(packet.DownstreamBoundaries.Count, packet.Summary.DownstreamBoundaryCount);
        Assert.Contains(packet.DownstreamBoundaries, boundary => boundary.BoundaryCategory == "database" && boundary.BoundaryKind == "sql-query");
        Assert.Contains(packet.DownstreamBoundaries, boundary => boundary.BoundaryCategory == "dependency" && boundary.BoundaryKind == "package-config");
        Assert.All(packet.DownstreamBoundaries, boundary =>
        {
            Assert.False(string.IsNullOrWhiteSpace(boundary.BoundaryTargetId));
            Assert.False(string.IsNullOrWhiteSpace(boundary.TerminalEvidenceId));
            Assert.NotEmpty(boundary.RuleIds);
            Assert.NotEmpty(boundary.EvidenceTiers);
            Assert.NotEmpty(boundary.SupportingFactIds);
        });
        Assert.DoesNotContain(packet.Gaps, gap => gap.Classification == "UnrelatedWinFormsGap");
        Assert.True(packet.Summary.Truncated);
        Assert.Contains(packet.Gaps, gap => gap.Classification == "WebFormsModernizationEventChainLimitReached"
            && gap.SupportingFactIds.Contains(laterBinding.FactId));
        Assert.All(packet.EventChains.SelectMany(chain => chain.PathEvidence), evidence =>
        {
            Assert.False(string.IsNullOrWhiteSpace(evidence.RuleId));
            Assert.False(string.IsNullOrWhiteSpace(evidence.EvidenceTier));
            Assert.False(string.IsNullOrWhiteSpace(evidence.ExtractorVersion));
            Assert.Equal("unknown", evidence.CoverageLabel);
            Assert.True(evidence.FilePath is null || !Path.IsPathRooted(evidence.FilePath));
        });
        Assert.Contains(packet.Gaps, gap => gap.Classification is "LegacyPathEvidenceCoverageUnavailable" or "LegacyPathEvidenceProvenanceUnavailable");
        Assert.Equal("reduced-static-webforms-modernization", packet.Coverage);
        Assert.DoesNotContain(temp.Path, JsonSerializer.Serialize(packet), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("does not prove runtime", string.Join(' ', packet.Limitations), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Boundary_inventory_preserves_direct_and_indirect_database_service_message_and_config_evidence()
    {
        using var temp = new TempDirectory();
        var manifest = Manifest("Succeeded") with { AnalysisLevel = "Level1SemanticAnalysis" };
        var surface = "webforms-surface:orders";
        var page = Fact(manifest, FactTypes.WebFormsPageDeclared, RuleIds.LegacyWebFormsInventory, "Pages/Orders.aspx", 1,
            source: surface, target: "Sample.Orders", contract: "Orders.aspx",
            ("surfaceIdentity", surface), ("directiveKind", "Page"), ("coverageLabel", "bounded-static-webforms-inventory"));
        var bindings = new[]
        {
            (Control: "save", Handler: "method:save", Name: "Save_Click", Line: 10),
            (Control: "copy", Handler: "method:copy", Name: "Copy_Click", Line: 11),
            (Control: "send", Handler: "method:send", Name: "Send_Click", Line: 12),
            (Control: "config", Handler: "method:config", Name: "Config_Click", Line: 13)
        }.Select(item => Fact(manifest, FactTypes.WebFormsEventBindingDeclared, RuleIds.LegacyWebFormsEventBinding, "Pages/Orders.aspx", item.Line,
            source: $"webforms-control:{item.Control}", target: item.Handler, contract: item.Name,
            ("surfaceIdentity", surface), ("eventSourceIdentity", $"webforms-control:{item.Control}"), ("eventName", "OnClick"),
            ("controlId", item.Control), ("handlerName", item.Name), ("markupFile", "Pages/Orders.aspx"), ("coverageLabel", "bounded-static-webforms-event"))).ToArray();
        var handlers = bindings.Select((binding, index) => Fact(manifest, FactTypes.WebFormsHandlerResolved, RuleIds.LegacyWebFormsHandlerResolution, "Pages/Orders.aspx.cs", 20 + index,
            source: binding.SourceSymbol, target: binding.TargetSymbol, contract: binding.ContractElement,
            ("surfaceIdentity", surface), ("bindingFactId", binding.FactId), ("handlerSymbolId", binding.TargetSymbol!),
            ("handlerSymbol", binding.TargetSymbol!), ("handlerName", binding.ContractElement!), ("controlId", binding.Properties["controlId"]),
            ("eventName", "OnClick"), ("markupFile", "Pages/Orders.aspx"), ("pageTypeName", "Sample.Orders"),
            ("coverageLabel", "bounded-static-webforms-handler"))).ToArray();
        var firstQuery = Fact(manifest, FactTypes.QueryPatternDetected, RuleIds.CSharpSyntaxQueryPattern, "Services/Orders.cs", 40,
            source: "method:save", target: "query-label", contract: "SELECT",
            ("operationName", "SELECT"), ("tableName", "orders"), ("columnNames", "id;state"), ("sqlSourceKind", "literal-string"),
            ("queryShapeHash", "shape-a"), ("coverageLabel", "bounded-static-query"));
        var secondQuery = Fact(manifest, FactTypes.QueryPatternDetected, RuleIds.CSharpSyntaxQueryPattern, "Services/Orders.cs", 41,
            source: "method:copy", target: "query-label", contract: "SELECT",
            ("operationName", "SELECT"), ("tableName", "orders"), ("columnNames", "id;state"), ("sqlSourceKind", "literal-string"),
            ("queryShapeHash", "shape-b"), ("coverageLabel", "bounded-static-query"));
        var persistence = Fact(manifest, FactTypes.DatabaseOperationCandidate, RuleIds.DatabaseOperationCallPattern, "Services/Orders.cs", 42,
            source: "method:copy", target: "orders", contract: "save",
            ("frameworkFamily", "ef-core"), ("operationKind", "save-boundary"), ("targetIdentityStatus", "entity-static"),
            ("tableName", "orders"), ("coverageLabel", "bounded-static-call"));
        var serviceCall = Fact(manifest, FactTypes.CallEdge, "csharp.semantic.call.v1", "Pages/Orders.aspx.cs", 30,
            source: "method:send", target: "method:dispatch", contract: "Dispatch", ("coverageLabel", "bounded-static-call"));
        var http = Fact(manifest, FactTypes.HttpCallDetected, RuleIds.HttpClientInvocation, "Services/Dispatch.cs", 50,
            source: "method:dispatch", target: "GET /orders", contract: "GET",
            ("httpMethod", "GET"), ("methodName", "GET"), ("normalizedPathTemplate", "/orders"),
            ("normalizedPathKey", "/orders"), ("urlKind", "template"), ("coverageLabel", "bounded-static-http"));
        var message = Fact(manifest, FactTypes.MessagePublisherSurface, RuleIds.MessageSurfacePublish, "Services/Dispatch.cs", 51,
            source: "method:dispatch", target: "publish:orders", contract: "publish",
            ("destinationIdentityStatus", "static"), ("frameworkFamily", "fixture"), ("frameworkFeature", "send"),
            ("normalizedDestinationKey", "orders"), ("operationDirection", "publish"), ("operationKind", "send"),
            ("stableMessageSurfaceKey", "message:orders:publish"), ("surfaceKind", "message-queue"),
            ("coverageLabel", "bounded-static-message"));
        var config = Fact(manifest, FactTypes.ConfigBinding, RuleIds.ConfigKey, "Pages/Orders.aspx.cs", 60,
            source: "method:config", target: "status", contract: "status",
            ("configKey", "status"), ("surfaceKind", "package-config"), ("coverageLabel", "bounded-static-config"));
        var index = Path.Combine(temp.Path, "index.sqlite");
        SqliteIndexWriter.Write(index, manifest, [page, .. bindings, .. handlers, firstQuery, secondQuery, persistence, serviceCall, http, message, config]);

        var packet = await WebFormsModernizationPacketReporter.BuildAsync(new(
            index,
            Path.Combine(temp.Path, "output"),
            MaxBoundaries: 5));

        Assert.Equal(5, packet.DownstreamBoundaries.Count);
        Assert.Equal(5, packet.Summary.DownstreamBoundaryCount);
        Assert.Equal(3, packet.DownstreamBoundaries.Count(boundary => boundary.BoundaryCategory == "database"));
        Assert.Equal(3, packet.DownstreamBoundaries.Where(boundary => boundary.BoundaryCategory == "database").Select(boundary => boundary.BoundaryTargetId).Distinct().Count());
        Assert.Contains(packet.DownstreamBoundaries, boundary => boundary.BoundaryKind == "sql-persistence");
        Assert.Contains(packet.DownstreamBoundaries, boundary => boundary.BoundaryCategory == "service" && boundary.BoundaryKind == "http-client");
        Assert.Contains(packet.DownstreamBoundaries, boundary => boundary.BoundaryCategory == "messaging" && boundary.BoundaryKind == "message-queue");
        Assert.Contains(packet.Gaps, gap => gap.Classification == "WebFormsModernizationBoundaryLimitReached");
        Assert.Contains(packet.Gaps, gap => gap.Classification == "IndirectFileOperationBoundaryCoverageUnavailable");
        Assert.True(packet.Summary.Truncated);
        Assert.All(packet.DownstreamBoundaries, boundary =>
        {
            Assert.StartsWith("boundary-", boundary.BoundaryId, StringComparison.Ordinal);
            Assert.StartsWith("boundary-target-", boundary.BoundaryTargetId, StringComparison.Ordinal);
            Assert.NotEmpty(boundary.PathEvidence);
            Assert.All(boundary.PathEvidence, evidence =>
            {
                Assert.False(string.IsNullOrWhiteSpace(evidence.RuleId));
                Assert.False(string.IsNullOrWhiteSpace(evidence.EvidenceTier));
                Assert.Equal(manifest.CommitSha, evidence.CommitSha);
                Assert.True(evidence.FilePath is null || !Path.IsPathRooted(evidence.FilePath));
            });
        });

        var unbounded = await WebFormsModernizationPacketReporter.BuildAsync(new(
            index,
            Path.Combine(temp.Path, "unbounded"),
            MaxBoundaries: 20));
        Assert.Contains(unbounded.DownstreamBoundaries, boundary => boundary.BoundaryCategory == "configuration");
        Assert.Contains(unbounded.Gaps, gap => gap.Classification == "ConfigurationBoundaryNeedsReview");
        var bytes = JsonSerializer.SerializeToUtf8Bytes(unbounded);
        var roundTrip = JsonSerializer.Deserialize<WebFormsModernizationPacket>(bytes, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(roundTrip);
        Assert.Equal(unbounded.DownstreamBoundaries.Count, roundTrip.DownstreamBoundaries.Count);
        Assert.DoesNotContain("/orders", System.Text.Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Identity_inventory_is_bounded_joinable_private_and_preserves_identity_gaps()
    {
        using var temp = new TempDirectory();
        var manifest = Manifest("FailedOrPartial");
        const string surface = "webforms-surface:login";
        const string controlId = "webforms-control:login";
        var page = Fact(manifest, FactTypes.WebFormsPageDeclared, RuleIds.LegacyWebFormsInventory, "Pages/Login.aspx", 1,
            source: surface, target: "Sample.Login", contract: "Login.aspx",
            ("surfaceIdentity", surface), ("directiveKind", "Page"), ("coverageLabel", "reduced-static-webforms-inventory"));
        var control = Fact(manifest, FactTypes.WebFormsControlDeclared, RuleIds.LegacyWebFormsInventory, "Pages/Login.aspx", 4,
            source: surface, target: controlId, contract: "Login",
            ("surfaceIdentity", surface), ("controlIdentity", controlId), ("controlType", "Login"),
            ("coverageLabel", "reduced-static-webforms-inventory"));
        var login = Fact(manifest, FactTypes.AspNetIdentityStateDeclared, RuleIds.LegacyAspNetIdentityState, "Pages/Login.aspx", 4,
            source: surface, target: controlId, contract: "login-control",
            ("identityKind", "login-control"), ("declarationStatus", "declared-in-markup"),
            ("controlType", "Login"), ("supportingFactIds", control.FactId), ("unsafeRawValue", "private-login-secret"),
            ("ruleLimitations", "Login-control rows are static markup declarations and do not prove runtime use."),
            ("coverageLabel", "reduced-static-identity-state"));
        var authentication = Fact(manifest, FactTypes.AspNetIdentityStateDeclared, RuleIds.LegacyAspNetIdentityState, "web.config", 3,
            source: null, target: null, contract: "authentication",
            ("identityKind", "authentication"), ("declarationStatus", "private-classification-secret"),
            ("authenticationMode", "Forms"), ("cookielessSetting", "UseUri"), ("decryptionAlgorithm", "3DES"),
            ("cookieName", "private-cookie-secret"), ("sameSite", "private-metadata-secret"),
            ("coverageLabel", "reduced-static-identity-state"));
        var gap = Fact(manifest, FactTypes.AnalysisGap, RuleIds.LegacyAspNetIdentityState, "Identity.cs", 7,
            source: null, target: null, contract: "IdentitySemanticDependencyUnavailable",
            ("gapKind", "IdentitySemanticDependencyUnavailable"), ("supportingFactIds", login.FactId),
            ("coverageLabel", "Reduced"));
        var index = Path.Combine(temp.Path, "index.sqlite");
        SqliteIndexWriter.Write(index, manifest, [page, control, login, authentication, gap]);

        var bounded = await WebFormsModernizationPacketReporter.BuildAsync(new(
            index,
            Path.Combine(temp.Path, "bounded"),
            MaxIdentityState: 1));
        Assert.Single(bounded.IdentityStateInventory);
        Assert.Equal(1, bounded.Summary.IdentityStateCount);
        Assert.True(bounded.Summary.Truncated);
        Assert.Contains(bounded.Gaps, item => item.Classification == "WebFormsModernizationIdentityStateLimitReached"
            && item.SupportingFactIds.Contains(authentication.FactId));
        Assert.Contains(bounded.Gaps, item => item.Classification == "IdentitySemanticDependencyUnavailable"
            && item.SupportingFactIds.Contains(login.FactId));

        var packet = await WebFormsModernizationPacketReporter.BuildAsync(new(
            index,
            Path.Combine(temp.Path, "unbounded"),
            MaxIdentityState: 10));
        Assert.Equal(2, packet.IdentityStateInventory.Count);
        Assert.Equal(2, packet.Summary.IdentityStateCount);
        var loginRow = Assert.Single(packet.IdentityStateInventory, item => item.IdentityKind == "login-control");
        Assert.Equal(surface, loginRow.SurfaceId);
        Assert.Contains(control.FactId, loginRow.SupportingFactIds);
        Assert.Equal("Login", loginRow.SafeMetadata["controlType"]);
        Assert.Equal(RuleIds.LegacyAspNetIdentityState, loginRow.Evidence.RuleId);
        Assert.Equal(manifest.CommitSha, loginRow.Evidence.CommitSha);
        var authenticationRow = Assert.Single(packet.IdentityStateInventory, item => item.IdentityKind == "authentication");
        Assert.Equal("Forms", authenticationRow.SafeMetadata["authenticationMode"]);
        Assert.Equal("UseUri", authenticationRow.SafeMetadata["cookielessSetting"]);
        Assert.Equal("3DES", authenticationRow.SafeMetadata["decryptionAlgorithm"]);
        Assert.Equal("unknown", authenticationRow.Classification);
        Assert.Contains(packet.Gaps, item => item.Classification == "UnsupportedIdentityStatePropertyShape"
            && item.SupportingFactIds.Contains(authentication.FactId));
        var serialized = JsonSerializer.Serialize(packet);
        Assert.DoesNotContain("private-login-secret", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("private-cookie-secret", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("private-classification-secret", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("private-metadata-secret", serialized, StringComparison.Ordinal);
        Assert.Contains("Login-control rows are static markup declarations", string.Join(' ', loginRow.Limitations), StringComparison.Ordinal);
        var written = await WebFormsModernizationPacketReporter.WriteAsync(new(
            index,
            Path.Combine(temp.Path, "written"),
            MaxIdentityState: 10));
        var markdown = await File.ReadAllTextAsync(written.MarkdownPath);
        Assert.Contains("## Identity and state declarations", markdown, StringComparison.Ordinal);
        Assert.Contains(loginRow.IdentityStateId, markdown, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Batch_data_movement_inventory_is_bounded_private_and_preserves_gaps()
    {
        using var temp = new TempDirectory();
        var manifest = Manifest("FailedOrPartial");
        var batch = Fact(manifest, FactTypes.LegacyBatchDataMovementDeclared, RuleIds.LegacyWebFormsBatchDataMovement, "Jobs/Archive.cs", 12,
            source: "Run", target: "scheduled-task", contract: "timer-trigger-attribute",
            ("surfaceKind", "scheduled-task"), ("mechanism", "compiler-resolved-hangfire-recurring-job"), ("operationKind", "trigger"),
            ("ownerStatus", "member-declared"), ("projectResolution", "resolved"), ("ownerMember", "Run"),
            ("scheduleSource", "config-reference-matched"), ("scheduleReferenceHash", new string('a', 32)),
            ("retryDeclaration", "named-call"), ("checkpointDeclaration", "named-call"),
            ("supportingFactIds", "fact-safe-support"), ("limitations", "Static batch candidate only; runtime execution is not proven."));
        var malformed = Fact(manifest, FactTypes.LegacyBatchDataMovementDeclared, RuleIds.LegacyWebFormsBatchDataMovement, "Jobs/Archive.cs", 30,
            source: "Copy", target: "bulk-copy", contract: "compiler-resolved-sql-bulk-copy",
            ("surfaceKind", "private-unsupported-kind"), ("mechanism", "compiler-resolved-sql-bulk-copy"), ("operationKind", "write"),
            ("ownerStatus", "member-declared"), ("projectResolution", "resolved"), ("unsafeProperty", "private-batch-value"),
            ("limitations", "Static batch candidate only."));
        var binding = Fact(manifest, FactTypes.LegacyBatchDataMovementDeclared, RuleIds.LegacyWebFormsBatchDataMovement, "Jobs/Archive.cs", 24,
            source: "Bind", target: "message-data-movement", contract: "existing-message-surface",
            ("surfaceKind", "message-data-movement"), ("mechanism", "existing-message-surface"), ("operationKind", "bind"),
            ("ownerStatus", "member-declared"), ("projectResolution", "resolved"), ("messageSurfaceKind", "message-topic"),
            ("limitations", "Static batch candidate only."));
        var gap = Fact(manifest, FactTypes.AnalysisGap, RuleIds.LegacyWebFormsBatchDataMovement, "Jobs/Archive.cs", 31,
            source: "Copy", target: "bulk-copy", contract: "BatchOwnerProjectUnavailable",
            ("gapKind", "BatchOwnerProjectUnavailable"), ("supportingFactIds", malformed.FactId), ("coverage", "reduced"));
        var index = Path.Combine(temp.Path, "index.sqlite");
        SqliteIndexWriter.Write(index, manifest, [batch, binding, malformed, gap]);

        var bounded = await WebFormsModernizationPacketReporter.BuildAsync(new(
            index,
            Path.Combine(temp.Path, "bounded"),
            MaxBatchDataMovement: 1));
        Assert.Single(bounded.BatchDataMovementInventory);
        Assert.Equal(1, bounded.Summary.BatchDataMovementCount);
        Assert.True(bounded.Summary.Truncated);
        Assert.Contains(bounded.Gaps, item => item.Classification == "WebFormsModernizationBatchDataMovementLimitReached"
            && item.SupportingFactIds.Contains(malformed.FactId));
        Assert.Contains(bounded.Gaps, item => item.Classification == "BatchOwnerProjectUnavailable"
            && item.SupportingFactIds.Contains(malformed.FactId));

        var written = await WebFormsModernizationPacketReporter.WriteAsync(new(
            index,
            Path.Combine(temp.Path, "written"),
            MaxBatchDataMovement: 10));
        Assert.Equal(3, written.Packet.BatchDataMovementInventory.Count);
        var row = Assert.Single(written.Packet.BatchDataMovementInventory, item => item.Mechanism == "compiler-resolved-hangfire-recurring-job");
        Assert.Equal("scheduled-task", row.SurfaceKind);
        Assert.Equal("trigger", row.OperationKind);
        Assert.Equal("config-reference-matched", row.SafeMetadata["scheduleSource"]);
        Assert.Equal("named-call", row.SafeMetadata["retryDeclaration"]);
        Assert.Contains("fact-safe-support", row.SupportingFactIds);
        Assert.Equal(RuleIds.LegacyWebFormsBatchDataMovement, row.Evidence.RuleId);
        var bindingRow = Assert.Single(written.Packet.BatchDataMovementInventory, item => item.OperationKind == "bind");
        Assert.Equal("message-topic", bindingRow.SafeMetadata["messageSurfaceKind"]);
        Assert.Contains(written.Packet.Gaps, item => item.Classification == "UnsupportedBatchDataMovementPropertyShape"
            && item.SupportingFactIds.Contains(malformed.FactId));
        var json = await File.ReadAllTextAsync(written.JsonPath);
        var markdown = await File.ReadAllTextAsync(written.MarkdownPath);
        Assert.Contains("## Batch and data-movement inventory", markdown, StringComparison.Ordinal);
        Assert.Contains(row.BatchDataMovementId, markdown, StringComparison.Ordinal);
        Assert.DoesNotContain("private-batch-value", json, StringComparison.Ordinal);
        Assert.DoesNotContain("private-unsupported-kind", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Flow_fallback_groups_the_same_terminal_without_conflating_evidence_records()
    {
        var manifest = Manifest("Succeeded") with { AnalysisLevel = "Level1SemanticAnalysis" };
        var firstBinding = Fact(manifest, FactTypes.WebFormsEventBindingDeclared, RuleIds.LegacyWebFormsEventBinding, "Pages/First.aspx", 10,
            source: "control:first", target: "method:first", contract: "First_Click",
            ("surfaceIdentity", "surface:first"), ("eventSourceIdentity", "control:first"), ("coverageLabel", "bounded-static-webforms-event"));
        var secondBinding = Fact(manifest, FactTypes.WebFormsEventBindingDeclared, RuleIds.LegacyWebFormsEventBinding, "Pages/Second.aspx", 10,
            source: "control:second", target: "method:second", contract: "Second_Click",
            ("surfaceIdentity", "surface:second"), ("eventSourceIdentity", "control:second"), ("coverageLabel", "bounded-static-webforms-event"));
        var firstHandler = Fact(manifest, FactTypes.WebFormsHandlerResolved, RuleIds.LegacyWebFormsHandlerResolution, "Pages/First.aspx.cs", 20,
            source: "control:first", target: "method:first", contract: "First_Click",
            ("bindingFactId", firstBinding.FactId), ("handlerSymbolId", "method:first"), ("coverageLabel", "bounded-static-webforms-handler"));
        var secondHandler = Fact(manifest, FactTypes.WebFormsHandlerResolved, RuleIds.LegacyWebFormsHandlerResolution, "Pages/Second.aspx.cs", 20,
            source: "control:second", target: "method:second", contract: "Second_Click",
            ("bindingFactId", secondBinding.FactId), ("handlerSymbolId", "method:second"), ("coverageLabel", "bounded-static-webforms-handler"));
        var firstFlow = Fact(manifest, FactTypes.WebFormsEventFlowProjected, RuleIds.LegacyWebFormsEventFlow, "Pages/First.aspx.cs", 21,
            source: "method:first", target: "private-target-a", contract: "flow",
            ("supportingFactIds", firstHandler.FactId), ("terminalSurfaceKind", "http-client"),
            ("terminalSurfaceNameHash", "shared-terminal-hash"), ("flowClassification", "ProbableStaticPath"),
            ("coverageLabel", "bounded-static-webforms-flow"));
        var secondFlow = Fact(manifest, FactTypes.WebFormsEventFlowProjected, RuleIds.LegacyWebFormsEventFlow, "Pages/Second.aspx.cs", 21,
            source: "method:second", target: "private-target-b", contract: "flow",
            ("supportingFactIds", secondHandler.FactId), ("terminalSurfaceKind", "http-client"),
            ("terminalSurfaceNameHash", "shared-terminal-hash"), ("flowClassification", "ProbableStaticPath"),
            ("coverageLabel", "bounded-static-webforms-flow"));
        var snapshot = new WebFormsModernizationPacketReporter.Snapshot(
            manifest.RepoName, manifest.ScanId, manifest.CommitSha, manifest.AnalysisLevel, manifest.BuildStatus,
            [firstBinding, secondBinding, firstHandler, secondHandler, firstFlow, secondFlow]);

        var packet = WebFormsModernizationPacketReporter.Build(
            snapshot,
            LegacyFlow(),
            new("unused", "unused"));

        Assert.Equal(2, packet.DownstreamBoundaries.Count);
        Assert.Single(packet.DownstreamBoundaries.Select(boundary => boundary.BoundaryTargetId).Distinct(StringComparer.Ordinal));
        Assert.Equal(2, packet.DownstreamBoundaries.Select(boundary => boundary.TerminalEvidenceId).Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(packet.DownstreamBoundaries, boundary => boundary.TerminalEvidenceId == firstFlow.FactId);
        Assert.Contains(packet.DownstreamBoundaries, boundary => boundary.TerminalEvidenceId == secondFlow.FactId);

        var firstRoot = PathNode("root:first", "symbol", "method:first", manifest, symbolId: "method:first");
        var secondRoot = PathNode("root:second", "symbol", "method:second", manifest, symbolId: "method:second");
        var firstProjection = PathNode(
            "projection:first", "surface", "http-client:hash:shared-terminal-hash", manifest,
            combinedFactId: firstFlow.FactId, ruleId: RuleIds.LegacyWebFormsEventFlow,
            evidenceTier: EvidenceTiers.Tier2Structural, filePath: "Pages/First.aspx.cs", startLine: 21, endLine: 21,
            surfaceKind: "http-client", sourceKind: "projection", shapeHash: "shared-terminal-hash");
        var secondProjection = PathNode(
            "projection:second", "surface", "http-client:hash:shared-terminal-hash", manifest,
            combinedFactId: secondFlow.FactId, ruleId: RuleIds.LegacyWebFormsEventFlow,
            evidenceTier: EvidenceTiers.Tier2Structural, filePath: "Pages/Second.aspx.cs", startLine: 21, endLine: 21,
            surfaceKind: "http-client", sourceKind: "projection", shapeHash: "shared-terminal-hash");
        var firstPath = LegacyPath(
            "path:first", firstRoot, firstProjection, bindingId: firstBinding.FactId, handlerId: firstHandler.FactId);
        var secondPath = LegacyPath(
            "path:second", secondRoot, secondProjection, bindingId: secondBinding.FactId, handlerId: secondHandler.FactId);

        var projectionPacket = WebFormsModernizationPacketReporter.Build(
            snapshot,
            LegacyFlow(firstPath, secondPath),
            new("unused", "unused"));

        Assert.Equal(2, projectionPacket.DownstreamBoundaries.Count);
        Assert.Single(projectionPacket.DownstreamBoundaries.Select(boundary => boundary.BoundaryTargetId).Distinct(StringComparer.Ordinal));
        Assert.Equal(2, projectionPacket.DownstreamBoundaries.Select(boundary => boundary.TerminalEvidenceId).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Legacy_path_node_identity_remains_joinable_and_its_provenance_is_aggregated()
    {
        var manifest = Manifest("Succeeded") with { AnalysisLevel = "Level1SemanticAnalysis" };
        var binding = Fact(manifest, FactTypes.WebFormsEventBindingDeclared, RuleIds.LegacyWebFormsEventBinding, "Pages/Orders.aspx", 10,
            source: "control:orders", target: "method:orders", contract: "Orders_Click",
            ("surfaceIdentity", "surface:orders"), ("eventSourceIdentity", "control:orders"), ("coverageLabel", "bounded-static-webforms-event"));
        var handler = Fact(manifest, FactTypes.WebFormsHandlerResolved, RuleIds.LegacyWebFormsHandlerResolution, "Pages/Orders.aspx.cs", 20,
            source: "control:orders", target: "method:orders", contract: "Orders_Click",
            ("bindingFactId", binding.FactId), ("handlerSymbolId", "method:orders"), ("coverageLabel", "bounded-static-webforms-handler"));
        var rootNode = PathNode("handler-node", "symbol", "method:orders", manifest, symbolId: "method:orders");
        var terminalNode = PathNode(
            "surface-node-safe", "surface", "service-boundary", manifest,
            ruleId: RuleIds.HttpClientInvocation,
            evidenceTier: EvidenceTiers.Tier2Structural,
            filePath: "Services/OrdersClient.cs",
            startLine: 30,
            endLine: 31,
            surfaceKind: "http-client");
        var edge = new CombinedPathEdge(
            "path-edge-safe", "call", rootNode.NodeId, terminalNode.NodeId,
            CombinedDependencyPathClassifications.ProbableStaticPath,
            RuleIds.CSharpSemanticCallGraph,
            EvidenceTiers.Tier1Semantic,
            [handler.FactId], [], "Pages/Orders.aspx.cs", 20, 20);
        var path = new CombinedPath(
            "path:orders", CombinedDependencyPathClassifications.ProbableStaticPath, "Medium", 1,
            rootNode.NodeId, terminalNode.NodeId, [rootNode, terminalNode], [edge],
            [binding.FactId, handler.FactId], [edge.EdgeId], []);
        var snapshot = new WebFormsModernizationPacketReporter.Snapshot(
            manifest.RepoName, manifest.ScanId, manifest.CommitSha, manifest.AnalysisLevel, manifest.BuildStatus, [binding, handler]);

        var packet = WebFormsModernizationPacketReporter.Build(snapshot, LegacyFlow(path), new("unused", "unused"));

        var boundary = Assert.Single(packet.DownstreamBoundaries);
        Assert.Equal(terminalNode.NodeId, boundary.TerminalEvidenceId);
        Assert.Contains(boundary.PathEvidence, evidence => evidence.EvidenceId == boundary.TerminalEvidenceId);
        Assert.Contains(RuleIds.HttpClientInvocation, boundary.RuleIds);
        Assert.Contains(EvidenceTiers.Tier2Structural, boundary.EvidenceTiers);
        Assert.Contains("unknown", boundary.CoverageLabels);
    }

    [Fact]
    public void Generic_projection_is_suppressed_when_its_supporting_concrete_config_terminal_is_available()
    {
        var manifest = Manifest("Succeeded") with { AnalysisLevel = "Level1SemanticAnalysis" };
        var binding = Fact(manifest, FactTypes.WebFormsEventBindingDeclared, RuleIds.LegacyWebFormsEventBinding, "Pages/Settings.aspx", 10,
            source: "control:settings", target: "method:settings", contract: "Settings_Click",
            ("surfaceIdentity", "surface:settings"), ("eventSourceIdentity", "control:settings"), ("coverageLabel", "bounded-static-webforms-event"));
        var handler = Fact(manifest, FactTypes.WebFormsHandlerResolved, RuleIds.LegacyWebFormsHandlerResolution, "Pages/Settings.aspx.cs", 20,
            source: "control:settings", target: "method:settings", contract: "Settings_Click",
            ("bindingFactId", binding.FactId), ("handlerSymbolId", "method:settings"), ("coverageLabel", "bounded-static-webforms-handler"));
        var config = Fact(manifest, FactTypes.ConfigBinding, RuleIds.ConfigKey, "Pages/Settings.aspx.cs", 22,
            source: "method:settings", target: "config-key-hash", contract: "config-key-hash",
            ("configKey", "config-key-hash"), ("surfaceKind", "package-config"), ("coverageLabel", "bounded-static-config"));
        var otherConfig = Fact(manifest, FactTypes.ConfigBinding, RuleIds.ConfigKey, "Pages/Settings.aspx.cs", 23,
            source: "method:settings", target: "other-key-hash", contract: "other-key-hash",
            ("configKey", "other-key-hash"), ("surfaceKind", "package-config"), ("coverageLabel", "bounded-static-config"));
        var flow = Fact(manifest, FactTypes.WebFormsEventFlowProjected, RuleIds.LegacyWebFormsEventFlow, "Pages/Settings.aspx.cs", 20,
            source: "method:settings", target: "config-key-hash", contract: "Settings_Click",
            ("supportingFactIds", $"{handler.FactId},{config.FactId},{otherConfig.FactId}"), ("terminalSurfaceKind", "dependency-surface"),
            ("terminalSurfaceNameHash", FactFactory.Hash("config-key-hash", 32)), ("flowClassification", "ProbableStaticPath"),
            ("coverageLabel", "bounded-static-webforms-flow"));
        var root = PathNode("root:settings", "symbol", "method:settings", manifest, symbolId: "method:settings");
        var concrete = PathNode(
            "config:concrete", "surface", "config-key-hash", manifest,
            combinedFactId: config.FactId, ruleId: RuleIds.ConfigKey, evidenceTier: EvidenceTiers.Tier2Structural,
            filePath: "Pages/Settings.aspx.cs", startLine: 22, endLine: 22, surfaceKind: "package-config", sourceKind: "config",
            shapeHash: "config-shape", configKey: "config-key-hash");
        var projection = PathNode(
            "projection:settings", "surface", "dependency-surface:hash:config-terminal", manifest,
            combinedFactId: flow.FactId, ruleId: RuleIds.LegacyWebFormsEventFlow, evidenceTier: EvidenceTiers.Tier2Structural,
            filePath: "Pages/Settings.aspx.cs", startLine: 20, endLine: 20, surfaceKind: "dependency-surface", sourceKind: "projection",
            shapeHash: FactFactory.Hash("config-key-hash", 32));
        var concretePath = LegacyPath("path:config", root, concrete, binding.FactId, handler.FactId, [config.FactId]);
        var projectionPath = LegacyPath("path:projection", root, projection, binding.FactId, handler.FactId, [config.FactId, flow.FactId]);
        var snapshot = new WebFormsModernizationPacketReporter.Snapshot(
            manifest.RepoName, manifest.ScanId, manifest.CommitSha, manifest.AnalysisLevel, manifest.BuildStatus,
            [binding, handler, config, otherConfig, flow]);

        var packet = WebFormsModernizationPacketReporter.Build(
            snapshot,
            LegacyFlow(concretePath, projectionPath),
            new("unused", "unused"));

        var boundary = Assert.Single(packet.DownstreamBoundaries);
        Assert.Equal("configuration", boundary.BoundaryCategory);
        Assert.Equal("package-config", boundary.BoundaryKind);
        Assert.Equal(config.FactId, boundary.TerminalEvidenceId);

        var otherConcrete = PathNode(
            "config:other", "surface", "other-key-hash", manifest,
            combinedFactId: otherConfig.FactId, ruleId: RuleIds.ConfigKey, evidenceTier: EvidenceTiers.Tier2Structural,
            filePath: "Pages/Settings.aspx.cs", startLine: 23, endLine: 23, surfaceKind: "package-config", sourceKind: "config",
            shapeHash: "other-config-shape", configKey: "other-key-hash");
        var otherConcretePath = LegacyPath("path:other-config", root, otherConcrete, binding.FactId, handler.FactId, [otherConfig.FactId]);
        var partialPacket = WebFormsModernizationPacketReporter.Build(
            snapshot,
            LegacyFlow(otherConcretePath, projectionPath),
            new("unused", "unused"));

        Assert.Equal(2, partialPacket.DownstreamBoundaries.Count);
        Assert.Contains(partialPacket.DownstreamBoundaries, item => item.TerminalEvidenceId == otherConfig.FactId && item.BoundaryCategory == "configuration");
        Assert.Contains(partialPacket.DownstreamBoundaries, item => item.TerminalEvidenceId == flow.FactId && item.BoundaryKind == "dependency-surface");
    }

    [Fact]
    public async Task Missing_coverage_and_limits_fail_closed_deterministically()
    {
        using var temp = new TempDirectory();
        var manifest = Manifest("FailedOrPartial");
        var first = Fact(manifest, FactTypes.WebFormsPageDeclared, RuleIds.LegacyWebFormsInventory, "A/Default.aspx", 1,
            source: "surface:a", target: "A.Default", contract: "Default.aspx", ("surfaceIdentity", "surface:a"), ("directiveKind", "Page"));
        var second = Fact(manifest, FactTypes.WebFormsPageDeclared, RuleIds.LegacyWebFormsInventory, "B/Default.aspx", 1,
            source: "surface:b", target: "B.Default", contract: "Default.aspx", ("surfaceIdentity", "surface:b"), ("directiveKind", "Page"));
        var third = Fact(manifest, FactTypes.WebFormsPageDeclared, RuleIds.LegacyWebFormsInventory, "C/Default.aspx", 1,
            source: "surface:c", target: "C.Default", contract: "Default.aspx", ("surfaceIdentity", "surface:c"), ("directiveKind", "Page"));
        var index = Path.Combine(temp.Path, "index.sqlite");
        SqliteIndexWriter.Write(index, manifest, [first, second, third]);
        await using (var connection = new SqliteConnection($"Data Source={index}"))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "update facts set extractor_version = '', start_line = 0 where fact_id = $id;";
            command.Parameters.AddWithValue("$id", first.FactId);
            await command.ExecuteNonQueryAsync();
        }

        var packet = await WebFormsModernizationPacketReporter.BuildAsync(new(index, Path.Combine(temp.Path, "unused"), MaxSurfaces: 1));
        Assert.True(packet.Summary.Truncated);
        Assert.Equal("reduced-static-webforms-modernization", packet.Coverage);
        Assert.Contains(packet.Gaps, gap => gap.Classification == "WebFormsModernizationSurfaceLimitReached");
        Assert.Contains(packet.Gaps, gap => gap.Classification == "EvidenceProvenanceUnavailable" && gap.SupportingFactIds.Contains(first.FactId));
        var retained = Assert.Single(packet.Surfaces);
        Assert.NotEqual("surface:a", retained.SurfaceId);
        Assert.Equal("unknown", retained.Evidence.CoverageLabel);
        Assert.Contains(packet.Gaps, gap => gap.Classification == "EvidenceCoverageLabelUnavailable" && gap.SupportingFactIds.Contains(retained.Evidence.FactId));
        Assert.Equal(packet.Summary.GapCount, packet.Gaps.Count);

        var gapBounded = await WebFormsModernizationPacketReporter.BuildAsync(new(
            index,
            Path.Combine(temp.Path, "gap-bounded"),
            MaxSurfaces: 10,
            MaxGaps: 1));
        Assert.True(gapBounded.Summary.Truncated);
        Assert.Single(gapBounded.Gaps);
        Assert.Equal("WebFormsModernizationGapLimitReached", gapBounded.Gaps[0].Classification);
    }

    [Fact]
    public async Task Cli_writes_both_packet_formats()
    {
        using var temp = new TempDirectory();
        var manifest = Manifest("Succeeded");
        var index = Path.Combine(temp.Path, "index.sqlite");
        SqliteIndexWriter.Write(index, manifest, []);
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var output = Path.Combine(temp.Path, "packet");
        var exit = await TraceMapCommand.RunAsync([
            "webforms-modernization", "--index", index, "--out", output, "--max-boundaries", "1", "--max-identity-state", "1", "--max-batch-data-movement", "1"
        ], stdout, stderr);
        Assert.Equal(0, exit);
        Assert.Equal(string.Empty, stderr.ToString());
        Assert.Contains("Downstream boundaries: 0", stdout.ToString(), StringComparison.Ordinal);
        Assert.Contains("Identity/state declarations: 0", stdout.ToString(), StringComparison.Ordinal);
        Assert.Contains("Batch/data-movement declarations: 0", stdout.ToString(), StringComparison.Ordinal);
        Assert.Contains("Repository: repository-", stdout.ToString(), StringComparison.Ordinal);
        Assert.Contains($"Commit SHA: {manifest.CommitSha}", stdout.ToString(), StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(output, "webforms-modernization.json")));
        Assert.True(File.Exists(Path.Combine(output, "webforms-modernization.md")));

        stderr.GetStringBuilder().Clear();
        exit = await TraceMapCommand.RunAsync(
            ["webforms-modernization", "--index", index, "--out", output, "--max-surafces", "1"],
            stdout,
            stderr);
        Assert.Equal(1, exit);
        Assert.Contains("unsupported webforms-modernization option", stderr.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Combined_and_multi_manifest_indexes_fail_closed_with_stable_errors()
    {
        using var temp = new TempDirectory();
        var manifest = Manifest("Succeeded");
        var index = Path.Combine(temp.Path, "index.sqlite");
        SqliteIndexWriter.Write(index, manifest, []);
        await using (var connection = new SqliteConnection($"Data Source={index}"))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "create table index_sources(source_id text primary key);";
            await command.ExecuteNonQueryAsync();
        }

        var combined = await Assert.ThrowsAsync<InvalidDataException>(() =>
            WebFormsModernizationPacketReporter.BuildAsync(new(index, Path.Combine(temp.Path, "combined"))));
        Assert.Equal("WebFormsModernizationIndexUnsupported", combined.Message);

        await using (var connection = new SqliteConnection($"Data Source={index}"))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                drop table index_sources;
                insert into scan_manifest(scan_id, repo, commit_sha, scanner_version, scanned_at, analysis_level, build_status, manifest_json)
                select 'second-scan', repo, commit_sha, scanner_version, scanned_at, analysis_level, build_status, manifest_json
                from scan_manifest limit 1;
                """;
            await command.ExecuteNonQueryAsync();
        }

        var multiple = await Assert.ThrowsAsync<InvalidDataException>(() =>
            WebFormsModernizationPacketReporter.BuildAsync(new(index, Path.Combine(temp.Path, "multiple"))));
        Assert.Equal("WebFormsModernizationSnapshotInvalid", multiple.Message);
    }

    [Fact]
    public async Task Reduced_analysis_is_explicit_and_unknown_commit_identity_is_rejected()
    {
        using var temp = new TempDirectory();
        var reducedIndex = Path.Combine(temp.Path, "reduced.sqlite");
        SqliteIndexWriter.Write(reducedIndex, Manifest("Succeeded"), []);

        var reduced = await WebFormsModernizationPacketReporter.BuildAsync(new(
            reducedIndex,
            Path.Combine(temp.Path, "reduced-output")));
        Assert.Equal("reduced-static-webforms-modernization", reduced.Coverage);
        Assert.Contains(reduced.Gaps, gap => gap.Classification == "SourceAnalysisCoverageReduced"
            && gap.ScopeId == reduced.Sources.Single().ScanId);

        var syntaxIndex = Path.Combine(temp.Path, "syntax.sqlite");
        SqliteIndexWriter.Write(syntaxIndex, Manifest("NotRun") with { AnalysisLevel = "Level3SyntaxAnalysis" }, []);
        var syntax = await WebFormsModernizationPacketReporter.BuildAsync(new(
            syntaxIndex,
            Path.Combine(temp.Path, "syntax-output")));
        Assert.Contains(syntax.Gaps, gap => gap.Classification == "SourceAnalysisCoverageReduced");

        var unknownCommitIndex = Path.Combine(temp.Path, "unknown-commit.sqlite");
        SqliteIndexWriter.Write(unknownCommitIndex, Manifest("Succeeded") with { CommitSha = "unknown" }, []);
        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            WebFormsModernizationPacketReporter.BuildAsync(new(
                unknownCommitIndex,
                Path.Combine(temp.Path, "unknown-output"))));
        Assert.Equal("WebFormsModernizationCommitIdentityUnavailable", exception.Message);
    }

    [Fact]
    public async Task Foreign_path_and_non_webforms_snapshot_identity_fail_closed()
    {
        using var temp = new TempDirectory();
        var manifest = Manifest("Succeeded") with { AnalysisLevel = "Level1SemanticAnalysis" };
        var page = Fact(manifest, FactTypes.WebFormsPageDeclared, RuleIds.LegacyWebFormsInventory, "Pages/Orders.aspx", 1,
            source: "surface:orders", target: "Orders", contract: "Orders.aspx",
            ("surfaceIdentity", "surface:orders"), ("directiveKind", "Page"), ("coverageLabel", "bounded-static-webforms-inventory"));
        var call = Fact(manifest, FactTypes.CallEdge, "csharp.semantic.call.v1", "Pages/Orders.aspx.cs", 4,
            source: "method:orders", target: "method:save", contract: "Save", ("coverageLabel", "bounded-static-call"));
        var index = Path.Combine(temp.Path, "index.sqlite");
        SqliteIndexWriter.Write(index, manifest, [page, call]);
        await using (var connection = new SqliteConnection($"Data Source={index}"))
        {
            await connection.OpenAsync();
            await using var path = connection.CreateCommand();
            path.CommandText = "update facts set file_path = 'C:\\Users\\name\\Orders.aspx' where fact_id = $id;";
            path.Parameters.AddWithValue("$id", page.FactId);
            await path.ExecuteNonQueryAsync();
        }
        var packet = await WebFormsModernizationPacketReporter.BuildAsync(new(index, Path.Combine(temp.Path, "output")));
        Assert.DoesNotContain(packet.Surfaces, surface => surface.Evidence.FilePath.Contains("Users", StringComparison.Ordinal));
        Assert.Contains(packet.Gaps, gap => gap.Classification == "EvidenceFilePathUnavailable");

        await using (var connection = new SqliteConnection($"Data Source={index}"))
        {
            await connection.OpenAsync();
            await using var mismatch = connection.CreateCommand();
            mismatch.CommandText = "update facts set repo = 'foreign-repository' where fact_id = $id;";
            mismatch.Parameters.AddWithValue("$id", call.FactId);
            await mismatch.ExecuteNonQueryAsync();
        }
        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            WebFormsModernizationPacketReporter.BuildAsync(new(index, Path.Combine(temp.Path, "mismatch"))));
        Assert.Equal("WebFormsModernizationSourceIdentityMismatch", exception.Message);
    }

    private static ScanManifest Manifest(string buildStatus) => new(
        "scan-webforms-packet", "synthetic-repo", null, "dev", "0123456789abcdef0123456789abcdef01234567",
        "scanner-test", DateTimeOffset.Parse("2026-08-13T00:00:00Z"), "Level1SemanticAnalysisReduced", buildStatus,
        [], [], [], ["Synthetic fixture only."], ".", "scan-root-hash", "git-root-hash");

    private static CodeFact Fact(
        ScanManifest manifest,
        string factType,
        string ruleId,
        string path,
        int line,
        string? source,
        string? target,
        string? contract,
        params (string Key, string Value)[] properties) => FactFactory.Create(
            manifest, factType, ruleId, EvidenceTiers.Tier2Structural,
            new(path, line, line, null, "SyntheticFixture", "1.0"), sourceSymbol: source, targetSymbol: target, contractElement: contract,
            properties: new SortedDictionary<string, string>(properties.ToDictionary(item => item.Key, item => item.Value), StringComparer.Ordinal));

    private static CombinedDependencyPathReport LegacyFlow(params CombinedPath[] paths) => new(
        "1.0.0",
        LegacyFlowReportConstants.SchemaVersion,
        LegacyFlowReportConstants.View,
        "bounded-static-legacy-flow",
        [],
        new(null, null, null, null, null, null, null, null, true, 8, 100, 10_000, "bounded-bfs", "1.0.0", null),
        [],
        new(1, paths.SelectMany(path => path.Nodes).Select(node => node.NodeId).Distinct(StringComparer.Ordinal).Count(),
            paths.SelectMany(path => path.Edges).Select(edge => edge.EdgeId).Distinct(StringComparer.Ordinal).Count(),
            paths.Length, 0, 0, false),
        paths,
        [],
        new(
            new Dictionary<string, int>(), new Dictionary<string, int>(), new Dictionary<string, int>(),
            new Dictionary<string, int>(), new Dictionary<string, int>(), [], []),
        ["Synthetic legacy-flow fixture only."]);

    private static CombinedPathNode PathNode(
        string nodeId,
        string nodeKind,
        string displayName,
        ScanManifest manifest,
        string? symbolId = null,
        string? combinedFactId = null,
        string? ruleId = null,
        string? evidenceTier = null,
        string? filePath = null,
        int? startLine = null,
        int? endLine = null,
        string? surfaceKind = null,
        string? sourceKind = null,
        string? shapeHash = null,
        string? configKey = null) => new(
            nodeId, nodeKind, displayName, "fixture", "fixture", manifest.ScanId, manifest.CommitSha,
            symbolId, combinedFactId, ruleId, evidenceTier, filePath, startLine, endLine, surfaceKind,
            null, null, null, null, null, null, sourceKind, shapeHash, null, null, null, configKey, null);

    private static CombinedPath LegacyPath(
        string pathId,
        CombinedPathNode root,
        CombinedPathNode terminal,
        string bindingId,
        string handlerId,
        IReadOnlyList<string>? additionalSupport = null)
    {
        var edge = new CombinedPathEdge(
            $"edge:{pathId}", "webforms-event-flow-projection", root.NodeId, terminal.NodeId,
            CombinedDependencyPathClassifications.ProbableStaticPath,
            RuleIds.LegacyFlowStaticTraversal,
            EvidenceTiers.Tier2Structural,
            [bindingId, handlerId], [], root.FilePath ?? "Pages/Default.aspx.cs", root.StartLine ?? 1, root.EndLine ?? 1);
        return new(
            pathId, CombinedDependencyPathClassifications.ProbableStaticPath, "Medium", 1,
            root.NodeId, terminal.NodeId, [root, terminal], [edge],
            new[] { bindingId, handlerId }.Concat(additionalSupport ?? []).Distinct(StringComparer.Ordinal).ToArray(),
            [edge.EdgeId], []);
    }

    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
}
