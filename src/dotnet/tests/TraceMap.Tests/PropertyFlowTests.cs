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
        Assert.Equal(3, binding.Evidence.StartLine);
        Assert.Equal(3, binding.Evidence.EndLine);

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
            Assert.False(string.IsNullOrWhiteSpace(gap.ExtractorId));
            Assert.False(string.IsNullOrWhiteSpace(gap.ExtractorVersion));
            Assert.True(gap.LineSpan is not null || gap.SupportingFactIds.Count > 0 || gap.SupportingSourceIds.Count > 0 || gap.CommitShas.Count > 0);
        });
        Assert.All(result.Report.Inventory.EvidenceEdges, edge =>
        {
            Assert.False(string.IsNullOrWhiteSpace(edge.RuleId));
            Assert.False(string.IsNullOrWhiteSpace(edge.EvidenceTier));
            Assert.False(string.IsNullOrWhiteSpace(edge.ExtractorId));
            Assert.False(string.IsNullOrWhiteSpace(edge.ExtractorVersion));
            if (edge.StartLine is not null || edge.EndLine is not null)
            {
                Assert.NotNull(edge.LineSpan);
            }
        });
        Assert.All(result.Report.CoverageWarnings, warning =>
        {
            Assert.Equal("property-flow.coverage.v1", warning.RuleId);
            Assert.Equal(EvidenceTiers.Tier4Unknown, warning.EvidenceTier);
            Assert.NotEmpty(warning.SupportingSourceIds);
            Assert.NotEmpty(warning.ExtractorId);
            Assert.NotEmpty(warning.ExtractorVersion);
        });

        var markdown = await File.ReadAllTextAsync(Path.Combine(outDir, "property-flow-report.md"));
        var json = await File.ReadAllTextAsync(Path.Combine(outDir, "property-flow-report.json"));
        Assert.Contains("TraceMap Property Flow Report", markdown);
        Assert.Contains("Selected Roots", markdown);
        Assert.Contains("Coverage Warnings", markdown);
        Assert.Contains("Optional Observed Evidence", markdown);
        Assert.Contains("\"reportType\": \"property-flow\"", json);
        Assert.Contains("\"extractorId\": \"property-flow\"", json);
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
    public async Task Property_flow_accepts_safe_observed_demo_metadata_without_upgrading_static_classification()
    {
        using var temp = new TempDirectory();
        var (combinedPath, _) = await CreatePropertyFlowCombinedIndexAsync(temp);
        var observedPath = Path.Combine(temp.Path, "observed.json");
        await File.WriteAllTextAsync(observedPath, """
            {
              "observedEvidence": [
                {
                  "label": "local-demo-field-check",
                  "safeMetadata": {
                    "artifactHash": "abc123",
                    "captureMode": "local-demo",
                    "field": "email",
                    "selector": "binding:user.email",
                    "tool": "browser-observation"
                  }
                }
              ]
            }
            """);

        var withoutObserved = await PropertyFlowReporter.BuildReportAsync(new PropertyFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "without-observed"),
            "binding:user.email",
            Source: "client",
            Framework: "angular"));
        var withObserved = await PropertyFlowReporter.WriteAsync(new PropertyFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "with-observed"),
            "binding:user.email",
            Source: "client",
            Framework: "angular",
            ObservedEvidencePath: observedPath));

        Assert.Equal(withoutObserved.Summary.Classification, withObserved.Report.Summary.Classification);
        Assert.Equal(withoutObserved.LineagePaths.Select(path => path.Classification), withObserved.Report.LineagePaths.Select(path => path.Classification));
        var observed = Assert.Single(withObserved.Report.ObservedEvidence);
        Assert.Equal(PropertyFlowClassifications.ObservedDemoContext, observed.Classification);
        Assert.Equal("property-flow.observed-evidence.v1", observed.RuleId);
        Assert.Equal(EvidenceTiers.Tier4Unknown, observed.EvidenceTier);
        Assert.Equal("email", observed.SafeMetadata["field"]);
        Assert.Equal("abc123", observed.SafeMetadata["artifactHash"]);

        var markdown = await File.ReadAllTextAsync(withObserved.MarkdownPath!);
        var json = await File.ReadAllTextAsync(withObserved.JsonPath!);
        Assert.Contains("Observed evidence is demo/validation metadata only", markdown);
        Assert.Contains("local-demo-field-check", markdown);
        Assert.Contains("\"classification\": \"ObservedDemoContext\"", json);
        Assert.DoesNotContain(temp.Path, markdown);
        Assert.DoesNotContain(temp.Path, json);
    }

    [Fact]
    public async Task Property_flow_rejects_unsafe_observed_demo_metadata()
    {
        using var temp = new TempDirectory();
        var (combinedPath, _) = await CreatePropertyFlowCombinedIndexAsync(temp);
        var observedPath = Path.Combine(temp.Path, "observed-unsafe.json");
        await File.WriteAllTextAsync(observedPath, """
            {
              "observedEvidence": [
                {
                  "label": "local-demo-field-check",
                  "safeMetadata": {
                    "secretToken": "nope"
                  }
                }
              ]
            }
            """);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => PropertyFlowReporter.BuildReportAsync(new PropertyFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "unsafe-out"),
            "binding:user.email",
            Source: "client",
            Framework: "angular",
            ObservedEvidencePath: observedPath)));
        Assert.Contains("unsafe observed evidence metadata", exception.Message);
        Assert.DoesNotContain("secretToken", exception.Message);
    }

    [Theory]
    [InlineData("""{"observedEvidence":[{"label":"local-demo-field-check"}]}""", "requires non-empty safeMetadata")]
    [InlineData("""{"observedEvidence":[{"label":"local-demo-field-check","safeMetadata":{}}]}""", "requires non-empty safeMetadata")]
    [InlineData("""{"observedEvidence":[{"label":"local-demo-field-check","safeMetadata":{"artifactHash":"src/private/page.html"}}]}""", "unsafe observed evidence metadata")]
    [InlineData("""{"observedEvidence":[{"label":"local-demo-field-check","safeMetadata":{"captureMode":"production-login"}}]}""", "unsafe observed evidence metadata")]
    [InlineData("""{"observedEvidence":[{"label":"local-demo-field-check","safeMetadata":{"noteCode":"live-http"}}]}""", "unsafe observed evidence metadata")]
    [InlineData("""{"observedEvidence":[{"label":"local-demo-field-check","safeMetadata":{"noteCode":"apiKey=abc123"}}]}""", "unsafe observed evidence metadata")]
    [InlineData("""{"observedEvidence":[{"label":"C:/workspace/private-field","safeMetadata":{"artifactHash":"abc123"}}]}""", "unsafe observed evidence metadata")]
    [InlineData("""{"observedEvidence":[{"label":"\\\\server\\share\\private-field","safeMetadata":{"artifactHash":"abc123"}}]}""", "unsafe observed evidence metadata")]
    public async Task Property_flow_rejects_observed_demo_metadata_without_safe_evidence_or_with_runtime_markers(string observedJson, string expectedMessage)
    {
        using var temp = new TempDirectory();
        var (combinedPath, _) = await CreatePropertyFlowCombinedIndexAsync(temp);
        var observedPath = Path.Combine(temp.Path, "observed-unsafe.json");
        await File.WriteAllTextAsync(observedPath, observedJson);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => PropertyFlowReporter.BuildReportAsync(new PropertyFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "unsafe-out"),
            "binding:user.email",
            Source: "client",
            Framework: "angular",
            ObservedEvidencePath: observedPath)));

        Assert.Contains(expectedMessage, exception.Message);
        Assert.DoesNotContain(temp.Path, exception.Message);
        Assert.DoesNotContain("apiKey=abc123", exception.Message);
    }

    [Fact]
    public async Task Property_flow_rejects_unbounded_observed_demo_metadata_inputs()
    {
        using var temp = new TempDirectory();
        var (combinedPath, _) = await CreatePropertyFlowCombinedIndexAsync(temp);

        var oversizedPath = Path.Combine(temp.Path, "observed-oversized.json");
        await File.WriteAllTextAsync(oversizedPath, "{\"observedEvidence\":[{\"label\":\"local-demo-field-check\",\"safeMetadata\":{\"artifactHash\":\"" + new string('a', 300_000) + "\"}}]}");

        var oversizedException = await Assert.ThrowsAsync<ArgumentException>(() => PropertyFlowReporter.BuildReportAsync(new PropertyFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "oversized-out"),
            "binding:user.email",
            Source: "client",
            Framework: "angular",
            ObservedEvidencePath: oversizedPath)));

        Assert.Contains("size limit", oversizedException.Message);
        Assert.DoesNotContain(temp.Path, oversizedException.Message);

        var manyRowsPath = Path.Combine(temp.Path, "observed-many-rows.json");
        var rows = Enumerable.Range(1, 51).Select(index => "{\"label\":\"demo-field-" + index + "\",\"safeMetadata\":{\"artifactHash\":\"hash" + index + "\"}}");
        await File.WriteAllTextAsync(manyRowsPath, """{"observedEvidence":[""" + string.Join(",", rows) + """]}""");

        var manyRowsException = await Assert.ThrowsAsync<ArgumentException>(() => PropertyFlowReporter.BuildReportAsync(new PropertyFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "many-rows-out"),
            "binding:user.email",
            Source: "client",
            Framework: "angular",
            ObservedEvidencePath: manyRowsPath)));

        Assert.Contains("row limit", manyRowsException.Message);
        Assert.DoesNotContain(temp.Path, manyRowsException.Message);
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

    [Fact]
    public async Task Property_flow_connects_razor_binding_and_form_target_to_model_binding_property()
    {
        using var temp = new TempDirectory();
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var server = Manifest("server", "tracemap-milestone16");
        var binding = RazorBindingFact(server, "ProfileInput", "Email", "Views/Profile/Edit.cshtml", 4);
        var form = RazorFormTargetFact(server, "Profile", "Save", "POST", "Views/Profile/Edit.cshtml", 2);
        var modelBinding = ModelBindingFact(server, "ProfileInput", "Email", "view-model", "action-parameter", "form", "Profile", "Save", null, "POST", "Controllers/ProfileController.cs", 11);
        SqliteIndexWriter.Write(serverIndex, server, [binding, form, modelBinding]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([serverIndex], combinedPath, ["server"]));

        var report = await PropertyFlowReporter.BuildReportAsync(new PropertyFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "out"),
            "binding:Email",
            Framework: "razor"));

        Assert.Contains(report.LineagePaths, path => path.Edges.Any(edge => edge.EdgeKind == "razor-binding-binds-property"));
        Assert.Contains(report.LineagePaths, path => path.Edges.Any(edge => edge.EdgeKind == "form-target-binds-model"));
        Assert.Contains(report.Inventory.EvidenceNodes.Concat(report.LineagePaths.SelectMany(path => path.Nodes)), node => node.NodeKind == "ModelBindingTarget");

        var serverOnly = await PropertyFlowReporter.BuildReportAsync(new PropertyFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "out-server-only"),
            "field:Email",
            Framework: "razor",
            Source: "server"));
        Assert.Single(serverOnly.SelectedRoots);

        var modelOnlyIndex = Path.Combine(temp.Path, "model-only.sqlite");
        var modelOnlyCombined = Path.Combine(temp.Path, "model-only-combined.sqlite");
        SqliteIndexWriter.Write(modelOnlyIndex, server, [modelBinding]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([modelOnlyIndex], modelOnlyCombined, ["server"]));
        var noUiRoot = await PropertyFlowReporter.BuildReportAsync(new PropertyFlowOptions(
            modelOnlyCombined,
            Path.Combine(temp.Path, "model-only-out"),
            "field:Email",
            Framework: "razor"));
        Assert.Empty(noUiRoot.SelectedRoots);
        Assert.Contains(noUiRoot.Gaps, gap => gap.GapKind == "SelectorNoMatch");

        var formOnlyIndex = Path.Combine(temp.Path, "form-only.sqlite");
        var formOnlyCombined = Path.Combine(temp.Path, "form-only-combined.sqlite");
        SqliteIndexWriter.Write(formOnlyIndex, server, [form]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([formOnlyIndex], formOnlyCombined, ["server"]));
        var noHandler = await PropertyFlowReporter.BuildReportAsync(new PropertyFlowOptions(
            formOnlyCombined,
            Path.Combine(temp.Path, "form-only-out"),
            "binding:POST",
            Framework: "razor"));
        Assert.Contains(noHandler.Gaps, gap => gap.GapKind == "EndpointAlignmentUnavailable");

        var pageFormIndex = Path.Combine(temp.Path, "page-form.sqlite");
        var pageFormCombined = Path.Combine(temp.Path, "page-form-combined.sqlite");
        var pageForm = RazorPageFormTargetFact(server, "Save", "POST", "Pages/Profile/Edit.cshtml", 2);
        var pageHandlerBinding = ModelBindingFact(server, "ProfileInput", "Email", "view-model", "handler-parameter", "form", null, null, "OnPostSaveAsync", "POST", "Pages/Profile/Edit.cshtml.cs", 11);
        var sameHandlerOtherPageBinding = ModelBindingFact(server, "ProfileInput", "Email", "view-model", "handler-parameter", "form", null, null, "OnPostSaveAsync", "POST", "Pages/Admin/Edit.cshtml.cs", 12);
        SqliteIndexWriter.Write(pageFormIndex, server, [pageForm, pageHandlerBinding, sameHandlerOtherPageBinding]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([pageFormIndex], pageFormCombined, ["server"]));
        var pageHandlerReport = await PropertyFlowReporter.BuildReportAsync(new PropertyFlowOptions(
            pageFormCombined,
            Path.Combine(temp.Path, "page-form-out"),
            "binding:POST",
            Framework: "razor"));
        Assert.Contains(pageHandlerReport.LineagePaths, path => path.Edges.Any(edge => edge.EdgeKind == "form-target-binds-model"));
        Assert.DoesNotContain(pageHandlerReport.LineagePaths.SelectMany(path => path.Nodes), node => node.FilePath == "Pages/Admin/Edit.cshtml.cs");

        var written = await PropertyFlowReporter.WriteAsync(new PropertyFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "stable-out"),
            "binding:Email",
            Framework: "razor"));
        var markdown = await File.ReadAllTextAsync(written.MarkdownPath!);
        var json = await File.ReadAllTextAsync(written.JsonPath!);
        await PropertyFlowReporter.WriteAsync(new PropertyFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "stable-out"),
            "binding:Email",
            Framework: "razor"));
        Assert.Equal(markdown, await File.ReadAllTextAsync(written.MarkdownPath!));
        Assert.Equal(json, await File.ReadAllTextAsync(written.JsonPath!));
        Assert.DoesNotContain(temp.Path, markdown);
        Assert.DoesNotContain(temp.Path, json);

        var clientIndex = Path.Combine(temp.Path, "client-control.sqlite");
        var mixedCombined = Path.Combine(temp.Path, "mixed-control-combined.sqlite");
        var client = Manifest("client", "tracemap-typescript/0.1.0");
        SqliteIndexWriter.Write(clientIndex, client, [AngularControlFact(client, "Email", "src/profile.component.html", 3)]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([clientIndex, serverIndex], mixedCombined, ["client", "server"]));
        var razorControl = await PropertyFlowReporter.BuildReportAsync(new PropertyFlowOptions(
            mixedCombined,
            Path.Combine(temp.Path, "razor-control-out"),
            "control:Email",
            Framework: "razor"));
        Assert.Equal("server", Assert.Single(razorControl.SelectedRoots).SourceLabel);
        var angularControl = await PropertyFlowReporter.BuildReportAsync(new PropertyFlowOptions(
            mixedCombined,
            Path.Combine(temp.Path, "angular-control-out"),
            "control:Email",
            Framework: "angular"));
        Assert.Equal("client", Assert.Single(angularControl.SelectedRoots).SourceLabel);
    }

    [Fact]
    public async Task Property_flow_connects_angular_event_payload_http_endpoint_to_dto_property()
    {
        using var temp = new TempDirectory();
        var clientIndex = Path.Combine(temp.Path, "client.sqlite");
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var client = Manifest("client", "tracemap-typescript/0.1.0");
        var server = Manifest("server", "tracemap-milestone16");
        SqliteIndexWriter.Write(clientIndex, client, [
            AngularEventFact(client, "save", "email", "src/profile.component.html", 6),
            ObjectShapeFact(client, "save", "email", "src/profile.component.ts", 15),
            HttpClientFact(client, "POST", "/api/profile", "/api/profile", "src/profile.service.ts", 18, "save")
        ]);
        SqliteIndexWriter.Write(serverIndex, server, [
            RouteFact(server, "POST", "/api/profile", "/api/profile", "Server.ProfileController.Save(ProfileDto)", "Controllers/ProfileController.cs", 8),
            ModelBindingFact(server, "ProfileDto", "email", "dto", "action-parameter", "body", "Profile", "Save", null, "POST", "Controllers/ProfileController.cs", 8)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([clientIndex, serverIndex], combinedPath, ["client", "server"]));

        var report = await PropertyFlowReporter.BuildReportAsync(new PropertyFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "out"),
            "binding:save",
            Framework: "angular",
            Source: "client"));

        var path = Assert.Single(report.LineagePaths, path => path.Edges.Any(edge => edge.EdgeKind == "endpoint-binds-model"));
        Assert.Contains(path.Nodes, node => node.NodeKind == "PayloadField");
        Assert.Contains(path.Nodes, node => node.NodeKind == "DtoProperty");
        Assert.Equal(PropertyFlowClassifications.NeedsReviewLineage, path.Classification);

        var noRouteIndex = Path.Combine(temp.Path, "client-only.sqlite");
        var noRouteCombined = Path.Combine(temp.Path, "client-only-combined.sqlite");
        SqliteIndexWriter.Write(noRouteIndex, client, [
            AngularEventFact(client, "save", "email", "src/profile.component.html", 6),
            ObjectShapeFact(client, "save", "email", "src/profile.component.ts", 15),
            HttpClientFact(client, "POST", "/api/profile", "/api/profile", "src/profile.service.ts", 18, "save")
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([noRouteIndex], noRouteCombined, ["client"]));
        var noRoute = await PropertyFlowReporter.BuildReportAsync(new PropertyFlowOptions(
            noRouteCombined,
            Path.Combine(temp.Path, "no-route-out"),
            "binding:save",
            Framework: "angular"));
        Assert.Contains(noRoute.Gaps, gap => gap.GapKind == "EndpointAlignmentUnavailable");
    }

    [Fact]
    public async Task Property_flow_connects_angular_event_payload_http_endpoint_to_razor_page_handler_model_property()
    {
        using var temp = new TempDirectory();
        var clientIndex = Path.Combine(temp.Path, "client.sqlite");
        var serverIndex = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var client = Manifest("client", "tracemap-typescript/0.1.0");
        var server = Manifest("server", "tracemap-milestone16");
        SqliteIndexWriter.Write(clientIndex, client, [
            AngularEventFact(client, "save", "email", "src/profile.component.html", 6),
            ObjectShapeFact(client, "save", "email", "src/profile.component.ts", 15),
            HttpClientFact(client, "POST", "/Profile/Edit", "/Profile/Edit", "src/profile.service.ts", 18, "save")
        ]);
        SqliteIndexWriter.Write(serverIndex, server, [
            RazorPageRouteFact(server, "POST", "/Profile/Edit", "/Profile/Edit", "OnPostSave", "Pages/Profile/Edit.cshtml.cs", 8),
            ModelBindingFact(server, "ProfileInput", "email", "view-model", "handler-parameter", "form", null, null, "OnPostSaveAsync", "POST", "Pages/Profile/Edit.cshtml.cs", 8)
        ]);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([clientIndex, serverIndex], combinedPath, ["client", "server"]));

        var report = await PropertyFlowReporter.BuildReportAsync(new PropertyFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "out"),
            "binding:save",
            Framework: "angular",
            Source: "client"));

        var path = Assert.Single(report.LineagePaths, path => path.Edges.Any(edge => edge.EdgeKind == "endpoint-binds-model"));
        Assert.Contains(path.Nodes, node => node.NodeKind == "PayloadField");
        Assert.Contains(path.Nodes, node => node.NodeKind == "ModelBindingTarget");
        Assert.DoesNotContain(report.Gaps, gap => gap.GapKind == "PropertyIdentityUnavailable");
    }

    [Fact]
    public async Task Property_flow_handles_family_exclusion_overlap_and_generic_fanout_threshold()
    {
        using var temp = new TempDirectory();
        var index = Path.Combine(temp.Path, "server.sqlite");
        var combinedPath = Path.Combine(temp.Path, "combined.sqlite");
        var server = Manifest("server", "tracemap-milestone16");
        var facts = new List<CodeFact>
        {
            TypeFact(server, "ProfileEmpty", "Models/ProfileEmpty.cs", 3),
            PropertyFact(server, "ProfileModel", "Email", "model", "Models/ProfileModel.cs", 4),
            PropertyFact(server, "ProfileShared", "Email", "model;dto", "Models/ProfileShared.cs", 5),
            PropertyFact(server, "StatusReview", "status", "model", "Models/StatusReview.cs", 6)
        };
        for (var i = 0; i < 10; i++)
        {
            facts.Add(RazorBindingFact(server, $"StatusModel{i}", "status", $"Views/Profile/Edit{i}.cshtml", 3));
        }

        SqliteIndexWriter.Write(index, server, facts);
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([index], combinedPath, ["server"]));

        var dtoExclusion = await PropertyFlowReporter.BuildReportAsync(new PropertyFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "dto-out"),
            "dto:ProfileModel.Email"));
        Assert.Empty(dtoExclusion.SelectedRoots);
        Assert.Contains(dtoExclusion.Gaps, gap => gap.GapKind == "SelectorNoMatch");

        var missingProperty = await PropertyFlowReporter.BuildReportAsync(new PropertyFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "missing-property-out"),
            "model:ProfileEmpty.Email"));
        Assert.Empty(missingProperty.SelectedRoots);
        Assert.Contains(missingProperty.Gaps, gap => gap.GapKind == "SelectorNoMatch");

        var overlap = await PropertyFlowReporter.BuildReportAsync(new PropertyFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "overlap-out"),
            "model:ProfileShared.Email"));
        Assert.Equal("model;dto", Assert.Single(overlap.SelectedRoots).SafeDisplay["modelKind"]);

        var sameName = await PropertyFlowReporter.BuildReportAsync(new PropertyFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "same-name-out"),
            "binding:status",
            Framework: "razor",
            MaxRoots: 1));
        Assert.Contains(sameName.Gaps, gap => gap.GapKind == "SameNameOnlyPropertyMatch");

        await using (var connection = new SqliteConnection($"Data Source={combinedPath}"))
        {
            await connection.OpenAsync();
            var statusFactId = await ScalarAsync(connection, "select combined_fact_id from combined_facts where fact_type = 'RazorBinding' and contract_element = 'status' order by combined_fact_id limit 1;");
            var factReport = await PropertyFlowReporter.BuildReportAsync(new PropertyFlowOptions(
                combinedPath,
                Path.Combine(temp.Path, "fact-status-out"),
                $"fact:{statusFactId}"));
            Assert.Single(factReport.SelectedRoots);
            Assert.DoesNotContain(factReport.Gaps, gap => gap.GapKind is "AmbiguousSelector" or "GenericPropertyFanOut");
        }

        var fanoutNineIndex = Path.Combine(temp.Path, "fanout-nine.sqlite");
        var fanoutNineCombined = Path.Combine(temp.Path, "fanout-nine-combined.sqlite");
        SqliteIndexWriter.Write(fanoutNineIndex, server, facts.Where(fact => fact.FactType != FactTypes.RazorBinding || !fact.Evidence.FilePath.EndsWith("Edit9.cshtml", StringComparison.Ordinal)).ToArray());
        await CombinedIndexBuilder.CombineAsync(new CombineOptions([fanoutNineIndex], fanoutNineCombined, ["server"]));
        var nine = await PropertyFlowReporter.BuildReportAsync(new PropertyFlowOptions(
            fanoutNineCombined,
            Path.Combine(temp.Path, "nine-out"),
            "field:status",
            Framework: "razor",
            MaxRoots: 25));
        Assert.DoesNotContain(nine.Gaps, gap => gap.GapKind == "GenericPropertyFanOut");

        var ten = await PropertyFlowReporter.BuildReportAsync(new PropertyFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "ten-out"),
            "field:status",
            Framework: "razor",
            MaxRoots: 25));
        Assert.Contains(ten.Gaps, gap => gap.GapKind == "GenericPropertyFanOut");

        await using (var connection = new SqliteConnection($"Data Source={combinedPath}"))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "create table combined_route_flow_edges(edge_id text);";
            await command.ExecuteNonQueryAsync();
        }
        var emptyRouteFlow = await PropertyFlowReporter.BuildReportAsync(new PropertyFlowOptions(
            combinedPath,
            Path.Combine(temp.Path, "empty-route-flow-out"),
            "field:status",
            Framework: "razor",
            MaxRoots: 25));
        Assert.Equal("empty", emptyRouteFlow.Snapshot.Schema.RouteFlowSignal);
        Assert.Contains(emptyRouteFlow.Gaps, gap => gap.GapKind == "RouteFlowUnavailable");
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
        var methodName = methodSymbol.Split('.').Last().Split('(')[0];
        var controllerName = methodSymbol.Split('.').Reverse().Skip(1).FirstOrDefault()?.Replace("Controller", string.Empty, StringComparison.Ordinal) ?? string.Empty;
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
                ["actionName"] = methodName,
                ["controllerName"] = controllerName,
                ["httpMethods"] = method,
                ["methodName"] = method,
                ["normalizedPathTemplate"] = template,
                ["normalizedPathKey"] = key,
                ["routeTemplates"] = template
            });
    }

    private static CodeFact RazorPageRouteFact(ScanManifest manifest, string method, string template, string key, string handlerName, string file, int line)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.HttpRouteBinding,
            RuleIds.CSharpSyntaxAspNetRoute,
            EvidenceTiers.Tier3SyntaxOrTextual,
            new EvidenceSpan(file, line, line, null, "test", "test/1.0"),
            sourceSymbol: handlerName,
            targetSymbol: handlerName,
            contractElement: template,
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["handlerName"] = handlerName,
                ["httpMethods"] = method,
                ["methodName"] = method,
                ["normalizedPathTemplate"] = template,
                ["normalizedPathKey"] = key,
                ["routeTemplates"] = template
            });
    }

    private static CodeFact RazorBindingFact(ScanManifest manifest, string modelType, string propertyName, string file, int line)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.RazorBinding,
            RuleIds.RazorBinding,
            EvidenceTiers.Tier2Structural,
            new EvidenceSpan(file, line, line, null, "RazorBindingExtractor", ScannerVersions.RazorBindingExtractor),
            targetSymbol: propertyName,
            contractElement: propertyName,
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["uiFramework"] = "razor",
                ["bindingKind"] = "asp-for",
                ["controlKind"] = "input",
                ["modelType"] = modelType,
                ["propertyName"] = propertyName,
                ["propertyPath"] = propertyName,
                ["valueStored"] = "safe-metadata-only"
            });
    }

    private static CodeFact RazorFormTargetFact(ScanManifest manifest, string controllerName, string actionName, string httpMethod, string file, int line)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.RazorFormTarget,
            RuleIds.RazorFormTarget,
            EvidenceTiers.Tier2Structural,
            new EvidenceSpan(file, line, line, null, "RazorBindingExtractor", ScannerVersions.RazorBindingExtractor),
            targetSymbol: actionName,
            contractElement: httpMethod,
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["actionName"] = actionName,
                ["bindingKind"] = "form-action",
                ["controllerName"] = controllerName,
                ["controlKind"] = "form",
                ["httpMethod"] = httpMethod,
                ["uiFramework"] = "razor",
                ["valueStored"] = "safe-metadata-only"
            });
    }

    private static CodeFact RazorPageFormTargetFact(ScanManifest manifest, string handlerName, string httpMethod, string file, int line)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.RazorFormTarget,
            RuleIds.RazorFormTarget,
            EvidenceTiers.Tier2Structural,
            new EvidenceSpan(file, line, line, null, "RazorBindingExtractor", ScannerVersions.RazorBindingExtractor),
            targetSymbol: handlerName,
            contractElement: httpMethod,
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["bindingKind"] = "form-action",
                ["controlKind"] = "form",
                ["handlerName"] = handlerName,
                ["httpMethod"] = httpMethod,
                ["uiFramework"] = "razor",
                ["valueStored"] = "safe-metadata-only"
            });
    }

    private static CodeFact ModelBindingFact(
        ScanManifest manifest,
        string modelType,
        string propertyName,
        string modelKind,
        string bindingKind,
        string parameterSource,
        string? controllerName,
        string? actionName,
        string? handlerName,
        string httpMethod,
        string file,
        int line)
    {
        var properties = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["bindingKind"] = bindingKind,
            ["httpMethod"] = httpMethod,
            ["modelKind"] = modelKind,
            ["modelType"] = modelType,
            ["parameterName"] = "input",
            ["parameterSource"] = parameterSource,
            ["propertyName"] = propertyName,
            ["propertyPath"] = propertyName,
            ["propertyType"] = "string",
            ["uiFramework"] = "razor",
            ["valueStored"] = "safe-metadata-only"
        };
        if (controllerName is not null)
        {
            properties["controllerName"] = controllerName;
        }
        if (actionName is not null)
        {
            properties["actionName"] = actionName;
        }
        if (handlerName is not null)
        {
            properties["handlerName"] = handlerName;
        }

        return FactFactory.Create(
            manifest,
            FactTypes.RazorModelBindingTarget,
            RuleIds.RazorModelBinding,
            EvidenceTiers.Tier3SyntaxOrTextual,
            new EvidenceSpan(file, line, line, null, "CSharpSyntaxExtractor", ScannerVersions.CSharpSyntaxExtractor),
            sourceSymbol: actionName ?? handlerName ?? modelType,
            targetSymbol: $"{modelType}.{propertyName}",
            contractElement: propertyName,
            properties: properties);
    }

    private static CodeFact AngularEventFact(ScanManifest manifest, string handlerName, string propertyName, string file, int line)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.UiEventBinding,
            "typescript.angular.event-binding.v1",
            EvidenceTiers.Tier2Structural,
            new EvidenceSpan(file, line, line, null, "typescript-angular-template", "typescript-angular-template/0.1.0"),
            sourceSymbol: "ProfileComponent",
            targetSymbol: handlerName,
            contractElement: handlerName,
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["bindingKind"] = "event",
                ["eventName"] = "submit",
                ["handlerName"] = handlerName,
                ["propertyName"] = propertyName,
                ["uiFramework"] = "angular"
            });
    }

    private static CodeFact AngularControlFact(ScanManifest manifest, string controlName, string file, int line)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.UiFormControlBinding,
            "typescript.angular.form-binding.v1",
            EvidenceTiers.Tier2Structural,
            new EvidenceSpan(file, line, line, null, "typescript-angular-template", "typescript-angular-template/0.1.0"),
            sourceSymbol: "ProfileComponent",
            targetSymbol: controlName,
            contractElement: controlName,
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["bindingKind"] = "form-control",
                ["controlName"] = controlName,
                ["formControlName"] = controlName,
                ["propertyName"] = controlName,
                ["uiFramework"] = "angular"
            });
    }

    private static CodeFact ObjectShapeFact(ScanManifest manifest, string sourceMethod, string fieldName, string file, int line)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.ObjectShapeInferred,
            "typescript.syntax.objectshape.v1",
            EvidenceTiers.Tier3SyntaxOrTextual,
            new EvidenceSpan(file, line, line, null, "typescript-syntax", "typescript-syntax/0.1.0"),
            sourceSymbol: sourceMethod,
            targetSymbol: "object-literal",
            contractElement: "object-literal",
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["fieldCount"] = "1",
                ["fieldNames"] = fieldName,
                ["objectKind"] = "object-literal",
                ["shapeHash"] = FactFactory.Hash(fieldName, 32),
                ["sourceMethod"] = sourceMethod
            });
    }

    private static CodeFact PropertyFact(ScanManifest manifest, string containingType, string propertyName, string modelKind, string file, int line)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.PropertyDeclared,
            RuleIds.CSharpSyntaxDeclarations,
            EvidenceTiers.Tier3SyntaxOrTextual,
            new EvidenceSpan(file, line, line, null, "CSharpSyntaxExtractor", ScannerVersions.CSharpSyntaxExtractor),
            sourceSymbol: containingType,
            targetSymbol: $"{containingType}.{propertyName}",
            contractElement: propertyName,
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["containingType"] = containingType,
                ["modelKind"] = modelKind,
                ["propertyName"] = propertyName
            });
    }

    private static CodeFact TypeFact(ScanManifest manifest, string typeName, string file, int line)
    {
        return FactFactory.Create(
            manifest,
            FactTypes.TypeDeclared,
            RuleIds.CSharpSyntaxDeclarations,
            EvidenceTiers.Tier3SyntaxOrTextual,
            new EvidenceSpan(file, line, line, null, "CSharpSyntaxExtractor", ScannerVersions.CSharpSyntaxExtractor),
            targetSymbol: typeName,
            contractElement: typeName,
            properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                ["kind"] = "class",
                ["name"] = typeName
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
