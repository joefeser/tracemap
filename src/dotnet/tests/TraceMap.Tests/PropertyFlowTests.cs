using System.Text.Json;
using Microsoft.Data.Sqlite;
using TraceMap.Cli;
using TraceMap.Combine;
using TraceMap.Core;
using TraceMap.Reporting;
using TraceMap.Storage;

namespace TraceMap.Tests;

public sealed class PropertyFlowTests
{
    [Fact]
    public void Razor_extractor_emits_binding_form_target_and_gap_facts()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(Path.Combine(repo, "Views", "Profile"));
        File.WriteAllText(Path.Combine(repo, "Views", "Profile", "Edit.cshtml"), """
            @model ProfileViewModel
            <form asp-controller="Profile" asp-action="Save" method="post">
              <input asp-for="@Model.Email" />
              @Html.TextBoxFor(m => m.DisplayName)
              @ViewBag.DynamicValue
            </form>
            """);
        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));

        var binding = result.Facts.Single(fact => fact.FactType == FactTypes.RazorBinding && fact.ContractElement == "Email");
        Assert.Equal(RuleIds.RazorBinding, binding.RuleId);
        Assert.Equal(EvidenceTiers.Tier2Structural, binding.EvidenceTier);
        Assert.Equal("razor", binding.Properties["uiFramework"]);
        Assert.Equal("asp-for", binding.Properties["bindingKind"]);
        Assert.Equal("Email", binding.Properties["propertyPath"]);
        Assert.Equal("ProfileViewModel", binding.Properties["modelType"]);

        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.RazorFormTarget
            && fact.RuleId == RuleIds.RazorFormTarget
            && fact.Properties["actionName"] == "Save"
            && fact.Properties["controllerName"] == "Profile"
            && fact.Properties["httpMethod"] == "POST");
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.RazorBindingGap
            && fact.RuleId == RuleIds.RazorBindingGap
            && fact.EvidenceTier == EvidenceTiers.Tier4Unknown
            && fact.Properties["gapKind"] == "dynamic-razor-model");
        Assert.DoesNotContain(temp.Path, JsonSerializer.Serialize(result.Facts));
    }

    [Fact]
    public async Task Property_flow_writes_markdown_and_json_from_ui_roots_without_mutating_combined_index()
    {
        using var temp = new TempDirectory();
        var (combinedPath, rootFactId) = await CreatePropertyFlowCombinedIndexAsync(temp);
        var outDir = Path.Combine(temp.Path, "property-flow");
        var before = await FingerprintAsync(combinedPath);

        var result = await PropertyFlowReporter.WriteAsync(new PropertyFlowOptions(
            combinedPath,
            outDir,
            "binding:user.email",
            Source: "client",
            Framework: "angular",
            MaxPaths: 10));

        Assert.Equal(before, await FingerprintAsync(combinedPath));
        Assert.True(File.Exists(Path.Combine(outDir, "property-flow-report.md")));
        Assert.True(File.Exists(Path.Combine(outDir, "property-flow-report.json")));
        Assert.Equal("property-flow", result.Report.ReportType);
        Assert.Equal("1.0", result.Report.Version);
        var root = Assert.Single(result.Report.SelectedRoots);
        Assert.Equal(rootFactId, root.CombinedFactId);
        Assert.Equal("TemplateBinding", root.RootKind);
        Assert.Equal("client", root.SourceLabel);
        Assert.Equal("typescript.angular.template-binding.v1", root.RuleId);
        Assert.Contains(result.Report.Gaps, gap => gap.GapKind == "RouteFlowUnavailable");
        Assert.All(result.Report.Gaps, gap =>
        {
            Assert.False(string.IsNullOrWhiteSpace(gap.RuleId));
            Assert.False(string.IsNullOrWhiteSpace(gap.EvidenceTier));
        });

        var markdown = await File.ReadAllTextAsync(Path.Combine(outDir, "property-flow-report.md"));
        var json = await File.ReadAllTextAsync(Path.Combine(outDir, "property-flow-report.json"));
        Assert.Contains("TraceMap Property Flow Report", markdown);
        Assert.Contains("Selected Roots", markdown);
        Assert.Contains("Optional Observed Evidence", markdown);
        Assert.Contains("\"reportType\": \"property-flow\"", json);
        Assert.DoesNotContain(temp.Path, markdown);
        Assert.DoesNotContain(temp.Path, json);
        Assert.DoesNotContain("user.email.toString", markdown);

        await PropertyFlowReporter.WriteAsync(new PropertyFlowOptions(
            combinedPath,
            outDir,
            "binding:user.email",
            Source: "client",
            Framework: "angular",
            MaxPaths: 10));
        Assert.Equal(markdown, await File.ReadAllTextAsync(Path.Combine(outDir, "property-flow-report.md")));
        Assert.Equal(json, await File.ReadAllTextAsync(Path.Combine(outDir, "property-flow-report.json")));
    }

    [Fact]
    public async Task Property_flow_reports_generic_ambiguous_and_selector_no_match_states()
    {
        using var temp = new TempDirectory();
        var clientIndex = Path.Combine(temp.Path, "client.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var client = Manifest("client", "tracemap-typescript/0.1.0");
        SqliteIndexWriter.Write(clientIndex, client, [
            AngularBindingFact(client, "user.status", "src/profile.html", 3),
            AngularBindingFact(client, "order.status", "src/order.html", 4)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([clientIndex], combinedPath, ["client"]));

        var ambiguous = await PropertyFlowReporter.BuildReportAsync(new PropertyFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "out"),
            "field:status",
            MaxRoots: 1));

        Assert.Equal(2, ambiguous.Summary.TotalCandidateCount);
        Assert.Single(ambiguous.SelectedRoots);
        Assert.Equal(PropertyFlowClassifications.NeedsReviewLineage, ambiguous.SelectedRoots[0].Classification);
        Assert.Contains(ambiguous.Gaps, gap => gap.GapKind == "AmbiguousSelector");
        Assert.Contains(ambiguous.Gaps, gap => gap.GapKind == "TruncatedByLimit");

        var noMatch = await PropertyFlowReporter.BuildReportAsync(new PropertyFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "out"),
            "binding:notPresent"));
        Assert.Empty(noMatch.SelectedRoots);
        Assert.Equal(PropertyFlowClassifications.SelectorNoMatch, noMatch.Summary.Classification);
        Assert.Contains(noMatch.Gaps, gap => gap.GapKind == "SelectorNoMatch");
    }

    [Fact]
    public async Task Property_flow_rejects_single_language_index_and_unsafe_selectors()
    {
        using var temp = new TempDirectory();
        var indexPath = Path.Combine(temp.Path, "single.sqlite");
        var manifest = Manifest("client", "tracemap-typescript/0.1.0");
        SqliteIndexWriter.Write(indexPath, manifest, [
            AngularBindingFact(manifest, "user.email", "src/profile.html", 3)
        ]);

        await Assert.ThrowsAsync<InvalidDataException>(() => PropertyFlowReporter.BuildReportAsync(new PropertyFlowOptions(
            indexPath,
            Path.Combine(temp.Path, "out"),
            "binding:user.email")));
        await Assert.ThrowsAsync<ArgumentException>(() => PropertyFlowReporter.BuildReportAsync(new PropertyFlowOptions(
            indexPath,
            Path.Combine(temp.Path, "out"),
            "binding:https://example.test/secret")));
    }

    [Fact]
    public async Task Property_flow_cli_reports_counts_and_help()
    {
        using var temp = new TempDirectory();
        var (combinedPath, _) = await CreatePropertyFlowCombinedIndexAsync(temp);
        var output = new StringWriter();
        var error = new StringWriter();

        var exitCode = await TraceMapCommand.RunAsync([
            "property-flow",
            "--index", combinedPath,
            "--property", "binding:user.email",
            "--out", Path.Combine(temp.Path, "property-flow")
        ], output, error);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, error.ToString());
        Assert.Contains("TraceMap property-flow completed:", output.ToString());
        Assert.Contains("Selected roots:", output.ToString());

        output = new StringWriter();
        exitCode = await TraceMapCommand.RunAsync(["property-flow", "--help"], output, error);
        Assert.Equal(0, exitCode);
        Assert.Contains("field:", output.ToString());
    }

    [Fact]
    public async Task Property_flow_supports_fact_selector_razor_filter_and_single_file_outputs()
    {
        using var temp = new TempDirectory();
        var clientIndex = Path.Combine(temp.Path, "client.sqlite");
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var client = Manifest("client", "tracemap-typescript/0.1.0");
        var server = Manifest("server", "tracemap-milestone16");
        var angular = AngularBindingFact(client, "user.email", "src/profile.html", 3);
        var razor = FactFactory.Create(
            server,
            FactTypes.RazorBinding,
            RuleIds.RazorBinding,
            EvidenceTiers.Tier2Structural,
            new EvidenceSpan("Views/Profile/Edit.cshtml", 4, 4, null, "RazorBindingExtractor", ScannerVersions.RazorBindingExtractor),
            targetSymbol: "Email",
            contractElement: "Email",
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["uiFramework"] = "razor",
                ["bindingKind"] = "asp-for",
                ["controlKind"] = "input",
                ["modelType"] = "ProfileViewModel",
                ["propertyPath"] = "Email",
                ["propertyName"] = "Email"
            });
        SqliteIndexWriter.Write(clientIndex, client, [angular]);
        SqliteIndexWriter.Write(serverIndex, server, [razor]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([clientIndex, serverIndex], combinedPath, ["client", "server"]));

        await using var connection = new SqliteConnection($"Data Source={combinedPath}");
        await connection.OpenAsync();
        var combinedAngularId = await ScalarAsync(connection, "select combined_fact_id from combined_facts where original_fact_id = $id;", ("$id", angular.FactId));

        var factReport = await PropertyFlowReporter.WriteAsync(new PropertyFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "property-flow.json"),
            $"fact:{combinedAngularId}",
            Format: "json"));
        Assert.Null(factReport.MarkdownPath);
        Assert.Equal(Path.Combine(temp.Path, "property-flow.json"), factReport.JsonPath);
        Assert.True(File.Exists(factReport.JsonPath));
        Assert.False(File.Exists(Path.Combine(temp.Path, "property-flow.md")));
        Assert.Equal(combinedAngularId, Assert.Single(factReport.Report.SelectedRoots).CombinedFactId);

        var razorReport = await PropertyFlowReporter.WriteAsync(new PropertyFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "property-flow.md"),
            "binding:Email",
            Format: "markdown",
            Framework: "razor"));
        Assert.Equal(Path.Combine(temp.Path, "property-flow.md"), razorReport.MarkdownPath);
        Assert.Null(razorReport.JsonPath);
        var root = Assert.Single(razorReport.Report.SelectedRoots);
        Assert.Equal("ViewModelProperty", root.RootKind);
        Assert.Equal("server", root.SourceLabel);
        Assert.Equal(RuleIds.RazorBinding, root.RuleId);
        Assert.Contains("ViewModelProperty", await File.ReadAllTextAsync(razorReport.MarkdownPath!));

        var modelReport = await PropertyFlowReporter.BuildReportAsync(new PropertyFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "model-out"),
            "model:ProfileViewModel.Email",
            Framework: "razor"));
        Assert.Equal("server", Assert.Single(modelReport.SelectedRoots).SourceLabel);
    }

    private static async Task<(string CombinedPath, string RootFactId)> CreatePropertyFlowCombinedIndexAsync(TempDirectory temp)
    {
        var clientIndex = Path.Combine(temp.Path, "client.sqlite");
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var client = Manifest("client", "tracemap-typescript/0.1.0");
        var server = Manifest("server", "tracemap-milestone16");

        var uiFact = AngularBindingFact(client, "user.email", "src/profile.component.html", 4);
        SqliteIndexWriter.Write(clientIndex, client, [
            uiFact,
            HttpClientFact(client, "POST", "/api/profile", "/api/profile", "src/profile.service.ts", 12, "ProfileService.save")
        ]);
        SqliteIndexWriter.Write(serverIndex, server, [
            RouteFact(server, "POST", "/api/profile", "/api/profile", "Server.ProfileController.Save(ProfileDto)", "Controllers/ProfileController.cs", 8)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([clientIndex, serverIndex], combinedPath, ["client", "server"]));

        await using var connection = new SqliteConnection($"Data Source={combinedPath}");
        await connection.OpenAsync();
        var combinedRootId = await ScalarAsync(connection, "select combined_fact_id from combined_facts where original_fact_id = $id;", ("$id", uiFact.FactId));
        return (combinedPath, combinedRootId);
    }

    private static ScanManifest Manifest(string repo, string scannerVersion)
    {
        return new ScanManifest(
            $"scan-{repo}",
            repo,
            null,
            "main",
            "abc123",
            scannerVersion,
            DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
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

    private static CodeFact AngularBindingFact(ScanManifest manifest, string propertyPath, string file, int line)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.UiTemplateBinding,
            "typescript.angular.template-binding.v1",
            EvidenceTiers.Tier2Structural,
            new EvidenceSpan(file, line, line, null, "typescript-angular-template", "typescript-angular-template/0.1.0"),
            sourceSymbol: "ProfileComponent",
            targetSymbol: propertyPath,
            contractElement: "email",
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["uiFramework"] = "angular",
                ["bindingKind"] = "interpolation",
                ["componentClass"] = "ProfileComponent",
                ["propertyPath"] = propertyPath,
                ["propertyName"] = "email",
                ["templateOrigin"] = "templateUrl",
                ["valueStored"] = "safe-metadata-only"
            });
    }

    private static CodeFact HttpClientFact(ScanManifest manifest, string method, string template, string key, string file, int line, string sourceSymbol)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.HttpCallDetected,
            RuleIds.HttpClientInvocation,
            EvidenceTiers.Tier2Structural,
            new EvidenceSpan(file, line, line, null, "test", "test/1.0"),
            sourceSymbol: sourceSymbol,
            targetSymbol: $"{method} {template}",
            contractElement: method,
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["httpMethod"] = method,
                ["methodName"] = method,
                ["normalizedPathTemplate"] = template,
                ["normalizedPathKey"] = key,
                ["clientFramework"] = "angular",
                ["urlKind"] = "template"
            });
    }

    private static CodeFact RouteFact(ScanManifest manifest, string method, string template, string key, string methodSymbol, string file, int line)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.HttpRouteBinding,
            RuleIds.CSharpSyntaxAspNetRoute,
            EvidenceTiers.Tier3SyntaxOrTextual,
            new EvidenceSpan(file, line, line, null, "test", "test/1.0"),
            sourceSymbol: methodSymbol,
            targetSymbol: methodSymbol,
            contractElement: template,
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["httpMethods"] = method,
                ["methodName"] = method,
                ["normalizedPathTemplate"] = template,
                ["normalizedPathKey"] = key,
                ["routeTemplates"] = template
            });
    }

    private static async Task<string> FingerprintAsync(string path)
    {
        await using var connection = new SqliteConnection($"Data Source={path};Mode=ReadOnly");
        await connection.OpenAsync();
        return await ScalarAsync(connection, "select count(*) || ':' || coalesce(group_concat(name, ','), '') from sqlite_master where name not like 'sqlite_%';");
    }

    private static async Task<string> ScalarAsync(SqliteConnection connection, string sql, params (string Name, string Value)[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }
        return Convert.ToString(await command.ExecuteScalarAsync()) ?? string.Empty;
    }
}
