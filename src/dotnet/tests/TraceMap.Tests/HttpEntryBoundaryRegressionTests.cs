using System.Security.Cryptography;
using System.Text.Json;
using TraceMap.Combine;
using TraceMap.Core;
using TraceMap.Reporting;
using TraceMap.Storage;

namespace TraceMap.Tests;

/// <summary>
/// Compiler-backed synthetic source-call boundaries, not an ASP.NET runtime emulator.
/// Harness types intentionally do not impersonate signed framework assemblies.
/// </summary>
public sealed class HttpEntryBoundaryRegressionTests
{
    [Fact]
    public async Task Real_webforms_framework_keeps_override_body_and_host_entry_separate()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "AGENTS.md"))) root = root.Parent;
        Assert.NotNull(root);
        using var temp = new TempDirectory();
        var scan = ScanEngine.Scan(new ScanOptions(Path.Combine(root.FullName, "samples/http-entry-boundaries"), Path.Combine(temp.Path, "scan")));
        Assert.True(scan.Manifest.BuildStatus == "Succeeded", "Public fixture compilation gaps: " + string.Join("; ",
            scan.Facts.Where(f => f.FactType == FactTypes.AnalysisGap).Select(f =>
                f.RuleId + ":" + f.Properties.GetValueOrDefault("gapKind") + ":" + f.Properties.GetValueOrDefault("diagnosticId"))));
        Assert.Contains(scan.Facts, f => f.SourceSymbol?.Contains("LifecycleWidget.CreateChildControls", StringComparison.Ordinal) == true
            && f.TargetSymbol?.Contains("System.Web.UI.Control.CreateChildControls", StringComparison.Ordinal) == true
            && f.ContractElement == "Overrides" && f.EvidenceTier == EvidenceTiers.Tier1Semantic);
        var index = Path.Combine(temp.Path, "index.sqlite");
        SqliteIndexWriter.Write(index, scan.Manifest, scan.Facts);
        var combined = Path.Combine(temp.Path, "combined.sqlite");
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([index], combined, ["webforms"]));
        foreach (var (type, method, expected) in new[]
        {
            ("ProfileWidget", "Apply", true), ("HostPage", "ExplicitRead", true),
            ("LifecycleWidget", "CreateChildControls", true), ("HostPage", "Page_Load", false),
            ("InertWidget", "CreateChildControls", false), ("SameNameWidget", "CreateChildControls", false),
            ("DispatchWidget", "DirectCurrentClass", false), ("DispatchWidget", "VirtualCandidate", true)
        })
        {
            var declaration = Assert.Single(scan.Facts, f => f.FactType == FactTypes.MethodDeclared && f.EvidenceTier == EvidenceTiers.Tier1Semantic
                && f.Properties.GetValueOrDefault("containingType") == "Global.HttpEntrySamples." + type && f.Properties.GetValueOrDefault("name") == method);
            var options = new CombinedDependencyPathOptions(combined, Path.Combine(temp.Path, type + method),
                FromSymbol: declaration.TargetSymbol, ToSurface: "http-client", MaxDepth: 16) { ExactFromSymbol = true };
            var result = await CombinedDependencyPathReporter.BuildReportAsync(options);
            Assert.True(expected == (result.Paths.Count > 0), type + "." + method + " " + JsonSerializer.Serialize(result.Paths.Select(p => new { Nodes = p.Nodes.Select(n => n.DisplayName), Edges = p.Edges.Select(e => new { e.EdgeKind, e.EvidenceTier }) })));
            var bounded = await CombinedDependencyPathReporter.BuildBoundedCombinedIndexReportWithTraversalAsync(options,
                new ReportInputBudget(250_000, 250_000, 128 * 1024 * 1024));
            Assert.Equal(expected, bounded.Report.Paths.Count > 0);
            var inventory = await CombinedDependencyPathReporter.BuildReportAsync(options with { InventoryDistinctTerminals = true });
            Assert.Equal(expected, inventory.Paths.Count > 0);
            if (method == "VirtualCandidate")
                Assert.All(result.Paths, path => Assert.Contains(path.Edges, edge => edge.EdgeKind == "override-candidate"));
        }
    }

    [Theory]
    [InlineData("event-direct", true)]
    [InlineData("event-constructor", true)]
    [InlineData("explicit-control", true)]
    [InlineData("markup-only", false)]
    [InlineData("explicit-property", true)]
    [InlineData("markup-property", false)]
    [InlineData("lifecycle-body", true)]
    [InlineData("lifecycle-host", false)]
    [InlineData("inert-override", false)]
    [InlineData("same-name", false)]
    [InlineData("nameof-property", false)]
    [InlineData("deferred-property", false)]
    [InlineData("virtual-property", false)]
    [InlineData("writable-property", false)]
    [InlineData("byref-property", false)]
    [InlineData("invalid-write", false)]
    public async Task Retained_http_routes_do_not_invent_entry_calls(string mode, bool expected)
    {
        using var temp = new TempDirectory(OperatingSystem.IsMacOS() ? "/private/tmp" : null);
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(Path.Combine(repo, "App_Code"));
        var handlerBody = mode == "event-direct" ? "FetchService.GetProfile()" : "Dim value As New ProfileRecord()";
        File.WriteAllText(Path.Combine(repo, "App_Code", "Widgets.vb"), $$"""
            Imports System
            Public Class ButtonHarness
                Public Event Click As EventHandler
            End Class
            Public Class ControlHarness
                Protected Overridable Sub CreateChildControls()
                End Sub
            End Class
            Public Class ToolWidget
                Private WithEvents button As New ButtonHarness()
                Public Sub Apply(sender As Object, e As EventArgs) Handles button.Click
                    {{handlerBody}}
                End Sub
                Public Sub Fetch()
                    Dim value As New ProfileRecord()
                End Sub
                Public ReadOnly Property Profile As ProfileRecord
                    Get
                        Return New ProfileRecord()
                    End Get
                End Property
            End Class
            Public Class ProfileRecord
                Public Sub New()
                    FetchService.GetProfile()
                End Sub
            End Class
            Public Class FetchService
                Public Shared Sub GetProfile()
                    GetToken()
                    Dim client As New System.Net.WebClient()
                    client.UploadString("https://example.invalid/profile", "POST", "synthetic")
                End Sub
                Private Shared Sub GetToken()
                    Dim request = DirectCast(System.Net.WebRequest.Create("https://example.invalid/token"), System.Net.HttpWebRequest)
                    Using response = request.GetResponse()
                    End Using
                End Sub
            End Class
            Public Class HistoryWidget
                Inherits ControlHarness
                Protected Overrides Sub CreateChildControls()
                    Prepare()
                End Sub
                Private Sub Prepare()
                    FetchService.GetProfile()
                End Sub
            End Class
            Public Class InertWidget
                Inherits ControlHarness
                Protected Overrides Sub CreateChildControls()
                    Dim local As Integer = 1
                End Sub
            End Class
            Public Class SameNameWidget
                Public Sub CreateChildControls()
                End Sub
            End Class
            """);
        var widgetPath = Path.Combine(repo, "App_Code", "Widgets.vb");
        if (mode == "virtual-property")
            File.WriteAllText(widgetPath, File.ReadAllText(widgetPath).Replace("Public ReadOnly Property Profile", "Public Overridable ReadOnly Property Profile", StringComparison.Ordinal));
        if (mode == "writable-property")
            File.WriteAllText(widgetPath, File.ReadAllText(widgetPath)
                .Replace("Public ReadOnly Property Profile", "Public Property Profile", StringComparison.Ordinal)
                .Replace("End Get", "End Get\n        Set(value As ProfileRecord)\n        End Set", StringComparison.Ordinal));
        var pageBody = mode switch
        {
            "explicit-control" => "widget.Fetch()",
            "explicit-property" => "Dim value = widget.Profile",
            "nameof-property" => "Dim value = NameOf(widget.Profile)",
            "deferred-property" => "Dim value As System.Func(Of ProfileRecord) = Function() widget.Profile",
            "virtual-property" or "writable-property" => "Dim value = widget.Profile",
            "byref-property" => "Observe(widget.Profile)",
            "invalid-write" => "widget.Profile = Nothing",
            _ => "' No explicit source call. Containment is not invocation."
        };
        File.WriteAllText(Path.Combine(repo, "Host.aspx.vb"), $$"""
            Public Class Host
                Private widget As New ToolWidget()
                Public Sub Load()
                    {{pageBody}}
                End Sub
                Private Sub Observe(ByRef value As ProfileRecord)
                End Sub
            End Class
            """);
        File.WriteAllText(Path.Combine(repo, "Host.aspx"), """
            <%@ Page Language="VB" CodeFile="Host.aspx.vb" Inherits="Host" %>
            <%@ Register TagPrefix="demo" Namespace="" %>
            <demo:ToolWidget ID="widget" runat="server" />
            <%# widget.Profile %>
            """);
        WebFormsWizardNativeTests.InitGit(repo);
        static string Hash(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
        var context = Path.Combine(temp.Path, "context.json");
        // Use the installed runtime reference closure, including WebClient's
        // ComponentModel/EventBasedAsync dependencies. Each input is hash-pinned.
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
            .Split(Path.PathSeparator).Where(path => Path.GetDirectoryName(path) == Path.GetDirectoryName(typeof(object).Assembly.Location))
            .Distinct().Order(StringComparer.Ordinal).ToArray();
        File.WriteAllText(context, JsonSerializer.Serialize(new
        {
            schemaVersion = "vb-compilation-context.v1", commitSha = GitMetadataProvider.Detect(repo).CommitSha,
            rootNamespace = "", optionStrict = true, optionInfer = true, optionExplicit = true, imports = Array.Empty<string>(),
            sources = new[] { "App_Code/Widgets.vb", "Host.aspx.vb" }
                .Select(path => new { path, sha256 = Hash(Path.Combine(repo, path)) }).ToArray(),
            references = references.Select(path => new { path, sha256 = Hash(path) }).ToArray()
        }));
        var scan = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "scan")) { VisualBasicCompilationContextPath = context });
        var diagnostics = scan.Facts.Where(f => f.Properties.GetValueOrDefault("gapKind") == "CompilationDiagnostic").ToArray();
        if (mode == "invalid-write") Assert.NotEmpty(diagnostics);
        else Assert.True(diagnostics.Length == 0, JsonSerializer.Serialize(diagnostics.Select(f => new { f.Evidence.StartLine, f.Properties })));
        var http = scan.Facts.Where(f => f.FactType == FactTypes.HttpCallDetected && f.EvidenceTier == EvidenceTiers.Tier1Semantic).ToArray();
        Assert.Equal(2, http.Length);
        Assert.All(http, f => Assert.Equal(RuleIds.HttpClientInvocation, f.RuleId));
        var binding = Assert.Single(scan.Facts, f => f.FactType == FactTypes.VisualBasicEventBindingDeclared
            && f.EvidenceTier == EvidenceTiers.Tier1Semantic && f.Properties.GetValueOrDefault("wiringKind") == "Handles");
        Assert.Equal("Click", binding.Properties["eventName"]);
        var getterCalls = scan.Facts.Where(f => f.RuleId == RuleIds.VisualBasicSemanticReadOnlyGetterCall).ToArray();
        if (mode == "explicit-property")
        {
            var getterCall = Assert.Single(getterCalls);
            Assert.Equal(EvidenceTiers.Tier1Semantic, getterCall.EvidenceTier);
            Assert.Contains(scan.Facts, f => f.FactType == FactTypes.CallEdge
                && f.SourceSymbol == getterCall.TargetSymbol && f.Properties["sourceSymbolId"] == getterCall.Properties["targetSymbolId"]);
        }
        else Assert.Empty(getterCalls);
        var rootType = mode switch
        {
            "event-direct" or "event-constructor" => "ToolWidget",
            "lifecycle-body" => "HistoryWidget",
            "inert-override" => "InertWidget",
            "same-name" => "SameNameWidget",
            _ => "Host"
        };
        var rootName = rootType == "ToolWidget" ? "Apply" : rootType == "Host" ? "Load" : "CreateChildControls";
        var root = Assert.Single(scan.Facts, f => f.FactType == FactTypes.MethodDeclared
            && f.EvidenceTier == EvidenceTiers.Tier1Semantic && f.Properties.GetValueOrDefault("name") == rootName
            && f.Properties.GetValueOrDefault("containingType") == "Global." + rootType);
        if (rootType == "ToolWidget")
        {
            Assert.Equal(root.TargetSymbol, binding.SourceSymbol);
            Assert.Equal(root.Properties["targetSymbolId"], binding.Properties["sourceSymbolId"]);
        }
        // No page-to-event or page-to-override invocation may appear merely from markup.
        Assert.DoesNotContain(scan.Facts, f => f.FactType == FactTypes.CallEdge
            && f.SourceSymbol?.Contains("Host.Load", StringComparison.Ordinal) == true
            && (f.TargetSymbol?.Contains("CreateChildControls", StringComparison.Ordinal) == true
                || f.TargetSymbol?.Contains("ToolWidget.Apply", StringComparison.Ordinal) == true));
        var index = Path.Combine(temp.Path, "index.sqlite");
        SqliteIndexWriter.Write(index, scan.Manifest, scan.Facts);
        var combined = Path.Combine(temp.Path, "combined.sqlite");
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([index], combined, ["synthetic"]));
        var options = new CombinedDependencyPathOptions(combined, Path.Combine(temp.Path, "paths"),
            FromSymbol: root.TargetSymbol, ToSurface: "http-client", MaxDepth: 16) { ExactFromSymbol = true };
        var ordinary = await CombinedDependencyPathReporter.BuildReportAsync(options);
        var bounded = await CombinedDependencyPathReporter.BuildBoundedCombinedIndexReportWithTraversalAsync(options,
            new ReportInputBudget(250_000, 250_000, 128 * 1024 * 1024));
        Assert.Equal(expected, ordinary.Paths.Count > 0);
        Assert.Equal(expected, bounded.Report.Paths.Count > 0);
        if (expected)
        {
            var semanticPaths = ordinary.Paths.Where(p => p.Edges.All(e => e.EvidenceTier == EvidenceTiers.Tier1Semantic)).ToArray();
            // The legacy route-binding surface for WebRequest.Create is separate
            // from the two outbound operation facts; don't count it as a send.
            Assert.Equal(2, semanticPaths.Where(p => p.Nodes.Last().HttpCallEvidence is not null)
                .Select(p => p.Nodes.Last().NodeId).Distinct().Count());
        }
        Assert.Equal(ordinary.Paths.Select(p => string.Join("|", p.Nodes.Select(n => n.NodeId))).Order(),
            bounded.Report.Paths.Select(p => string.Join("|", p.Nodes.Select(n => n.NodeId))).Order());
        var repeated = await CombinedDependencyPathReporter.BuildReportAsync(options);
        Assert.Equal(JsonSerializer.Serialize(ordinary.Paths), JsonSerializer.Serialize(repeated.Paths));
    }
}
