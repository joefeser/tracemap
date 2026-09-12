using System.Text.Json;
using TraceMap.Core;
using TraceMap.Reporting;
using TraceMap.Storage;

namespace TraceMap.Tests;

public sealed class VisualBasicExternalBoundaryTests
{
    [Fact]
    public void Compiler_resolved_vb_http_config_file_and_service_shapes_are_safe_and_bounded()
    {
        using var temp = new TempDirectory();
        var repo = CreateRepository(temp.Path);

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));

        Assert.Equal("Succeeded", result.Manifest.BuildStatus);
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.WebFormsHandlerResolved
            && fact.ContractElement == "Fetch_Click");

        var http = result.Facts.Where(fact => fact.FactType == FactTypes.HttpCallDetected).ToArray();
        Assert.Contains(http, fact => fact.Properties.GetValueOrDefault("methodFamily") == "HttpClient"
            && fact.Properties.GetValueOrDefault("urlKind") == "compile-time-constant-path");
        Assert.Contains(http, fact => fact.Properties.GetValueOrDefault("methodFamily") == "WebClient");
        Assert.Contains(http, fact => fact.Properties.GetValueOrDefault("methodFamily") == "WebRequest"
            && fact.Properties.GetValueOrDefault("urlKind") == "unavailable");
        var requestConstruction = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.HttpClientCreated
            && fact.Properties.GetValueOrDefault("clientKind") == "WebRequest");
        Assert.Equal("compile-time-constant-path", requestConstruction.Properties["urlKind"]);
        Assert.NotEmpty(requestConstruction.Properties["urlHash"]);
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.AnalysisGap
            && fact.Properties.GetValueOrDefault("gapKind") == "VisualBasicHttpDestinationUnavailable");

        var config = result.Facts.Where(fact => fact.FactType == FactTypes.ConfigBinding
            && fact.RuleId == RuleIds.VisualBasicSemanticConfigBinding).ToArray();
        Assert.Contains(config, fact => fact.Properties.GetValueOrDefault("configSourceKind") == "configuration-manager-section");
        Assert.Contains(config, fact => fact.Properties.GetValueOrDefault("configSourceKind") == "configuration-manager-app-settings");
        Assert.Contains(config, fact => fact.Properties.GetValueOrDefault("configSourceKind") == "configuration-manager-connection-strings");
        Assert.All(config, fact => Assert.Matches("^[0-9a-f]{32}$", fact.Properties["configKeyHash"]));

        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.LegacyBatchDataMovementDeclared
            && fact.Properties.GetValueOrDefault("mechanism") == "compiler-resolved-system-io-call"
            && fact.Properties.GetValueOrDefault("surfaceKind") == "file-data-movement");
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.AnalysisGap
            && fact.Properties.GetValueOrDefault("gapKind") == "VisualBasicWcfServiceMappingUnavailable");
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.AnalysisGap
            && fact.Properties.GetValueOrDefault("gapKind") == "VisualBasicAsmxServiceMappingUnavailable");
        Assert.DoesNotContain(result.Facts, fact => fact.FactType is FactTypes.WcfServiceReferenceMapping or FactTypes.AsmxServiceReferenceMapping
            && fact.Evidence.FilePath.EndsWith("Default.aspx.vb", StringComparison.Ordinal));

        var serialized = JsonSerializer.Serialize(result.Facts);
        Assert.DoesNotContain("example.invalid", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private-api-key", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private-connection", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private-section", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private-file", serialized, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Unresolved_vb_external_calls_emit_gaps_without_boundary_claims()
    {
        using var temp = new TempDirectory();
        var repo = CreateRepository(temp.Path, includeLateBound: true);

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));

        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.VisualBasicSemanticExternalBoundary
            && fact.Properties.GetValueOrDefault("gapKind") == "VisualBasicExternalBoundaryTargetUnavailable");
        Assert.DoesNotContain(result.Facts, fact => fact.FactType == FactTypes.HttpCallDetected
            && fact.SourceSymbol?.Contains("LateBound", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void Repeated_vb_external_boundary_scans_are_fact_deterministic()
    {
        using var temp = new TempDirectory();
        var repo = CreateRepository(temp.Path);

        var first = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out-a")));
        var second = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out-b")));

        Assert.Equal(first.Facts.Select(fact => JsonSerializer.Serialize(fact)), second.Facts.Select(fact => JsonSerializer.Serialize(fact)));
    }

    [Fact]
    public async Task Vb_external_boundaries_flow_through_packet_docs_recipes_and_handoff_corpus()
    {
        using var temp = new TempDirectory();
        var repo = CreateRepository(temp.Path);
        var scan = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "scan")));
        var index = Path.Combine(temp.Path, "index.sqlite");
        SqliteIndexWriter.Write(index, scan.Manifest, scan.Facts);

        var packet = await WebFormsModernizationPacketReporter.WriteAsync(new(index, Path.Combine(temp.Path, "packet")));
        var httpFactIds = scan.Facts.Where(fact => fact.FactType == FactTypes.HttpCallDetected)
            .Select(fact => fact.FactId).ToHashSet(StringComparer.Ordinal);
        var handlerChains = packet.Packet.EventChains.Where(chain =>
            chain.HandlerSymbol?.Contains("Fetch_Click", StringComparison.OrdinalIgnoreCase) == true).ToArray();
        Assert.NotEmpty(handlerChains);
        var handlerChain = Assert.Single(handlerChains.Where(chain => chain.TerminalKind == "http-client"
            && chain.PathEvidence.Any(item => item.RuleId == RuleIds.HttpClientInvocation)
            && chain.SupportingFactIds.Any(id => httpFactIds.Any(factId => id.EndsWith(factId, StringComparison.Ordinal)))).Take(1));
        Assert.Contains(handlerChain.SupportingFactIds, id => httpFactIds.Any(factId => id.EndsWith(factId, StringComparison.Ordinal)));
        Assert.Contains(handlerChain.PathEvidence, item => item.RuleId == RuleIds.HttpClientInvocation);

        var docsRoot = Path.Combine(temp.Path, "docs");
        var docs = await EvidenceDocsExporter.ExportAsync(new EvidenceDocsExportOptions(
            index,
            docsRoot,
            Families: "webforms-modernization,gap,limitation",
            WebFormsPacketPaths: [packet.JsonPath]));
        Assert.Contains(docs.Chunks, chunk => chunk.Title == "Web Forms event-chain evidence"
            && chunk.SupportingIds.Contains(handlerChain.ChainId, StringComparer.Ordinal)
            && chunk.RetrievalHints.Any(hint => hint.RecipeId == "calls-from-handler"));
        Assert.True(File.Exists(Path.Combine(docsRoot, "query-recipes.json")));
        WebFormsAgentEvidenceHandoff.ValidateEvidenceCorpus(
            docsRoot,
            packet.Packet.Sources[0].ScanId,
            packet.Packet.Sources[0].CommitSha,
            packet.Packet.PacketId);
    }

    private static string CreateRepository(string root, bool includeLateBound = false)
    {
        var repo = Path.Combine(root, "repo");
        Directory.CreateDirectory(repo);
        WriteSupportProject(repo, "ConfigurationSupport", "System.Configuration.ConfigurationManager", """
            using System.Collections.Specialized;
            namespace System.Configuration;
            public static class ConfigurationManager
            {
                public static NameValueCollection AppSettings { get; } = new();
                public static ConnectionStringSettingsCollection ConnectionStrings { get; } = new();
                public static object GetSection(string name) => new object();
            }
            public sealed class ConnectionStringSettingsCollection
            {
                public ConnectionStringSettings this[string name] => new();
            }
            public sealed class ConnectionStringSettings { }
            """);
        WriteSupportProject(repo, "WcfSupport", "System.ServiceModel.Primitives", """
            namespace System.ServiceModel;
            public abstract class ClientBase<T> { }
            """);
        WriteSupportProject(repo, "AsmxSupport", "System.Web.Services", """
            namespace System.Web.Services.Protocols;
            public abstract class SoapHttpClientProtocol { }
            """);
        File.WriteAllText(Path.Combine(repo, "Legacy.vbproj"), $$"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <RootNamespace>Legacy</RootNamespace>
                <OptionStrict>{{(includeLateBound ? "Off" : "On")}}</OptionStrict>
              </PropertyGroup>
              <ItemGroup>
                <ProjectReference Include="ConfigurationSupport/ConfigurationSupport.csproj" />
                <ProjectReference Include="WcfSupport/WcfSupport.csproj" />
                <ProjectReference Include="AsmxSupport/AsmxSupport.csproj" />
              </ItemGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(repo, "Default.aspx"), "<%@ Page Language=\"VB\" CodeBehind=\"Default.aspx.vb\" Inherits=\"Legacy.SamplePage\" %><asp:Button ID=\"Fetch\" runat=\"server\" OnClick=\"Fetch_Click\" />");
        var lateBound = includeLateBound ? "value.GetAsync(\"https://private-late.invalid/value\")" : string.Empty;
        File.WriteAllText(Path.Combine(repo, "Default.aspx.vb"), $$"""
            Imports System.Configuration
            Imports System.IO
            Imports System.Net
            Imports System.Net.Http

            Public Interface IRatingService
            End Interface

            Public Class RatingClient
                Inherits System.ServiceModel.ClientBase(Of IRatingService)
                Public Sub Fetch()
                End Sub
            End Class

            Public Class LegacySoapClient
                Inherits System.Web.Services.Protocols.SoapHttpClientProtocol
                Public Sub Fetch()
                End Sub
            End Class

            Public Partial Class SamplePage
                Protected Sub Fetch_Click(sender As Object, e As EventArgs)
                    Dim client = New HttpClient()
                    Dim body = client.GetStringAsync(New Uri("https://example.invalid/api/items/42"))
                    Using legacyClient = New WebClient()
                        Dim older = legacyClient.DownloadString("https://example.invalid/api/older/42")
                    End Using
                    Dim request = WebRequest.Create(New Uri("https://example.invalid/api/request/42"))
                    Using response = request.GetResponse()
                    End Using
                    Dim section = ConfigurationManager.GetSection("private-section")
                    Dim setting = ConfigurationManager.AppSettings("private-api-key")
                    Dim connection = ConfigurationManager.ConnectionStrings("private-connection")
                    Dim content = File.ReadAllText("private-file")
                    File.WriteAllText("private-file", content)
                    Dim wcf = New RatingClient()
                    wcf.Fetch()
                    Dim soap = New LegacySoapClient()
                    soap.Fetch()
                End Sub

                Public Sub LateBound(value As Object)
                    {{lateBound}}
                End Sub
            End Class
            """);
        Commit(repo);
        return repo;
    }

    private static void WriteSupportProject(string repo, string directory, string assemblyName, string source)
    {
        var path = Path.Combine(repo, directory);
        Directory.CreateDirectory(path);
        File.WriteAllText(Path.Combine(path, $"{directory}.csproj"), $$"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <AssemblyName>{{assemblyName}}</AssemblyName>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(path, "Support.cs"), source);
    }

    private static void Commit(string repo)
    {
        RunGit(repo, "init");
        RunGit(repo, "add", "-A");
        RunGit(repo, "-c", "user.name=TraceMap", "-c", "user.email=tests@example.invalid", "commit", "-m", "fixture");
    }

    private static void RunGit(string repo, params string[] arguments)
    {
        using var process = new System.Diagnostics.Process
        {
            StartInfo = new()
            {
                FileName = "git",
                WorkingDirectory = repo,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };
        foreach (var argument in arguments) process.StartInfo.ArgumentList.Add(argument);
        process.Start();
        process.WaitForExit(30_000);
        Assert.Equal(0, process.ExitCode);
    }
}
