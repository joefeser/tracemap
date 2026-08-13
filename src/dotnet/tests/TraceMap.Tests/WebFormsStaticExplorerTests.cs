using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using TraceMap.Cli;
using TraceMap.Reporting;

namespace TraceMap.Tests;

public sealed class WebFormsStaticExplorerTests
{
    [Fact]
    public async Task Explorer_renders_compatible_webforms_packet_deterministically_with_provenance()
    {
        using var temp = new TempDirectory();
        var repo = CreateWebFormsRepository(temp.Path);
        var scan = Path.Combine(temp.Path, "scan");
        var packet = Path.Combine(temp.Path, "packet");
        var first = Path.Combine(temp.Path, "explorer-one");
        var second = Path.Combine(temp.Path, "explorer-two");
        var publicOutput = Path.Combine(temp.Path, "explorer-public");

        Assert.Equal(0, await TraceMapCommand.RunAsync(
            ["scan", "--repo", repo, "--out", scan],
            TextWriter.Null,
            TextWriter.Null));
        Assert.Equal(0, await TraceMapCommand.RunAsync(
            ["webforms-modernization", "--index", Path.Combine(scan, "index.sqlite"), "--out", packet],
            TextWriter.Null,
            TextWriter.Null));

        var firstResult = await StaticHtmlEvidenceExplorer.GenerateAsync(new(packet, first, "hidden-local"));
        var secondResult = await StaticHtmlEvidenceExplorer.GenerateAsync(new(packet, second, "hidden-local"));
        var publicResult = await StaticHtmlEvidenceExplorer.GenerateAsync(new(packet, publicOutput, "public-demo"));

        var artifact = Assert.Single(firstResult.Data.Artifacts, row => row.ArtifactKind == "webforms-modernization-packet");
        Assert.Equal(WebFormsModernizationPacketReporter.SchemaVersion, artifact.SchemaVersion);
        Assert.Contains(artifact.Compatibility, new[] { "supported", "supported-partial" });
        Assert.NotNull(firstResult.Data.WebForms);
        Assert.NotEmpty(firstResult.Data.WebForms!.Surfaces);
        Assert.NotEmpty(firstResult.Data.WebForms.EventChains);
        Assert.NotEmpty(firstResult.Data.WebForms.OwnerQuestions);
        Assert.Contains(firstResult.Data.EvidenceRows, row =>
            row.ArtifactId == "artifact:webforms-modernization"
            && !string.IsNullOrWhiteSpace(row.RuleId)
            && !string.IsNullOrWhiteSpace(row.EvidenceTier)
            && !string.IsNullOrWhiteSpace(row.ExtractorId)
            && !string.IsNullOrWhiteSpace(row.ExtractorVersion)
            && row.StartLine > 0
            && row.EndLine >= row.StartLine
            && row.SupportingFactIds is not null
            && row.SupportingEdgeIds is not null
            && row.SupportId.Length > 0);
        Assert.Contains(firstResult.Data.SectionStatuses, row =>
            row.SectionId == "webforms" && row.Status is "available" or "partial");
        Assert.Equal("public-demo", publicResult.Manifest.SafetyProfile);
        Assert.NotNull(publicResult.Data.WebForms);

        var html = await File.ReadAllTextAsync(Path.Combine(first, "index.html"));
        Assert.Contains("<h2 id=\"webforms-heading\">Web Forms Modernization</h2>", html, StringComparison.Ordinal);
        Assert.Contains("Application coverage", html, StringComparison.Ordinal);
        Assert.Contains("Application surfaces and composition", html, StringComparison.Ordinal);
        Assert.Contains("Event and handler chains", html, StringComparison.Ordinal);
        Assert.Contains("Structural slice candidates", html, StringComparison.Ordinal);
        Assert.Contains("Owner questions", html, StringComparison.Ordinal);
        Assert.DoesNotContain(repo, html, StringComparison.Ordinal);
        Assert.DoesNotContain(
            repo,
            string.Join('\n', Directory.EnumerateFiles(publicOutput, "*", SearchOption.AllDirectories).Select(File.ReadAllText)),
            StringComparison.Ordinal);

        Assert.Equal(
            Directory.EnumerateFiles(first, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(first, path))
                .OrderBy(value => value, StringComparer.Ordinal),
            Directory.EnumerateFiles(second, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(second, path))
                .OrderBy(value => value, StringComparer.Ordinal));
        foreach (var relative in Directory.EnumerateFiles(first, "*", SearchOption.AllDirectories)
                     .Select(path => Path.GetRelativePath(first, path)))
        {
            Assert.Equal(
                await File.ReadAllBytesAsync(Path.Combine(first, relative)),
                await File.ReadAllBytesAsync(Path.Combine(second, relative)));
        }
    }

    [Fact]
    public async Task Explorer_rejects_nested_webforms_evidence_with_conflicting_provenance()
    {
        using var temp = new TempDirectory();
        var repo = CreateWebFormsRepository(temp.Path);
        var scan = Path.Combine(temp.Path, "scan");
        var packet = Path.Combine(temp.Path, "packet");
        Assert.Equal(0, await TraceMapCommand.RunAsync(["scan", "--repo", repo, "--out", scan], TextWriter.Null, TextWriter.Null));
        Assert.Equal(0, await TraceMapCommand.RunAsync(
            ["webforms-modernization", "--index", Path.Combine(scan, "index.sqlite"), "--out", packet],
            TextWriter.Null,
            TextWriter.Null));

        var packetPath = Path.Combine(packet, "webforms-modernization.json");
        var root = JsonNode.Parse(await File.ReadAllTextAsync(packetPath))!.AsObject();
        root["surfaces"]![0]!["evidence"]!["commitSha"] = new string('f', 40);
        await File.WriteAllTextAsync(packetPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        var result = await StaticHtmlEvidenceExplorer.GenerateAsync(
            new(packet, Path.Combine(temp.Path, "explorer"), "hidden-local"));

        Assert.Null(result.Data.WebForms);
        Assert.Contains(result.Data.Artifacts, artifact =>
            artifact.ArtifactKind == "webforms-modernization-packet"
            && artifact.Compatibility == "unsupported");
        Assert.Contains(result.Gaps, gap =>
            gap.Scope == "artifact:webforms-modernization"
            && gap.GapKind == "unsupported-schema");
    }

    [Fact]
    public async Task Explorer_fails_closed_for_unsupported_or_conflicting_webforms_packet()
    {
        using var temp = new TempDirectory();
        var repo = CreateWebFormsRepository(temp.Path);
        var scan = Path.Combine(temp.Path, "scan");
        var packet = Path.Combine(temp.Path, "packet");
        Assert.Equal(0, await TraceMapCommand.RunAsync(["scan", "--repo", repo, "--out", scan], TextWriter.Null, TextWriter.Null));
        Assert.Equal(0, await TraceMapCommand.RunAsync(
            ["webforms-modernization", "--index", Path.Combine(scan, "index.sqlite"), "--out", packet],
            TextWriter.Null,
            TextWriter.Null));

        var packetPath = Path.Combine(packet, "webforms-modernization.json");
        var root = JsonNode.Parse(await File.ReadAllTextAsync(packetPath))!.AsObject();
        root["schemaVersion"] = "webforms-modernization-packet.v999";
        await File.WriteAllTextAsync(packetPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        var unsupported = await StaticHtmlEvidenceExplorer.GenerateAsync(new(packet, Path.Combine(temp.Path, "unsupported"), "hidden-local"));
        Assert.Null(unsupported.Data.WebForms);
        Assert.Contains(unsupported.Gaps, gap =>
            gap.Scope == "artifact:webforms-modernization"
            && gap.GapKind == "unsupported-schema");
        Assert.Contains(unsupported.Data.Artifacts, artifact =>
            artifact.ArtifactKind == "webforms-modernization-packet"
            && artifact.Compatibility == "unsupported");

        var conflictInput = Path.Combine(temp.Path, "conflict-input");
        Directory.CreateDirectory(conflictInput);
        File.Copy(Path.Combine(scan, "scan-manifest.json"), Path.Combine(conflictInput, "scan-manifest.json"));
        root["schemaVersion"] = WebFormsModernizationPacketReporter.SchemaVersion;
        root["sources"]![0]!["commitSha"] = new string('f', 40);
        await File.WriteAllTextAsync(
            Path.Combine(conflictInput, "webforms-modernization.json"),
            root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        var conflict = await StaticHtmlEvidenceExplorer.GenerateAsync(new(conflictInput, Path.Combine(temp.Path, "conflict"), "hidden-local"));
        Assert.Null(conflict.Data.WebForms);
        Assert.Contains(conflict.Gaps, gap => gap.Scope == "artifact:webforms-modernization");
    }

    [Fact]
    public async Task Explorer_redacts_unsafe_owner_question_instead_of_rendering_it()
    {
        using var temp = new TempDirectory();
        var repo = CreateWebFormsRepository(temp.Path);
        var scan = Path.Combine(temp.Path, "scan");
        var packet = Path.Combine(temp.Path, "packet");
        Assert.Equal(0, await TraceMapCommand.RunAsync(["scan", "--repo", repo, "--out", scan], TextWriter.Null, TextWriter.Null));
        Assert.Equal(0, await TraceMapCommand.RunAsync(
            ["webforms-modernization", "--index", Path.Combine(scan, "index.sqlite"), "--out", packet],
            TextWriter.Null,
            TextWriter.Null));
        var packetPath = Path.Combine(packet, "webforms-modernization.json");
        var root = JsonNode.Parse(await File.ReadAllTextAsync(packetPath))!.AsObject();
        root["ownerQuestions"]![0] = "Inspect C:\\Users\\private\\secret.txt";
        await File.WriteAllTextAsync(packetPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        var result = await StaticHtmlEvidenceExplorer.GenerateAsync(new(packet, Path.Combine(temp.Path, "safe"), "hidden-local"));
        Assert.NotNull(result.Data.WebForms);
        var all = string.Join('\n', Directory.EnumerateFiles(Path.Combine(temp.Path, "safe"), "*", SearchOption.AllDirectories).Select(File.ReadAllText));
        Assert.DoesNotContain("C:\\Users\\private", all, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("webforms-owner-question-hash:", all, StringComparison.Ordinal);
        Assert.Contains(result.Data.Redactions, redaction => redaction.Location == "webforms-owner-question");
    }

    private static string CreateWebFormsRepository(string root)
    {
        var repo = Path.Combine(root, "repo");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Sample.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(repo, "Orders.aspx"),
            "<%@ Page Language=\"C#\" CodeBehind=\"Orders.aspx.cs\" Inherits=\"Sample.Orders\" %><asp:Button ID=\"Save\" runat=\"server\" OnClick=\"Save_Click\" />");
        File.WriteAllText(Path.Combine(repo, "Orders.aspx.cs"), """
            namespace Sample;
            public class Orders
            {
                protected void Save_Click(object sender, System.EventArgs e) { }
            }
            """);
        RunGit(repo, "init");
        RunGit(repo, "add", ".");
        RunGit(repo, "-c", "user.name=TraceMap", "-c", "user.email=fixture@example.invalid", "commit", "-m", "baseline");
        return repo;
    }

    private static void RunGit(string repo, params string[] arguments)
    {
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = repo,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start)!;
        process.WaitForExit();
        Assert.Equal(0, process.ExitCode);
    }
}
