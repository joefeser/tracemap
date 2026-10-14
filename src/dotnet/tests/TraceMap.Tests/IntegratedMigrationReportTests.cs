using TraceMap.Core;
using TraceMap.Reporting;

namespace TraceMap.Tests;

public sealed class IntegratedMigrationReportTests
{
    [Theory]
    [InlineData("System.Web.UI.UserControl", "explicit-usercontrol-base-syntax-candidate")]
    [InlineData("UnavailableBase", "explicit-usercontrol-base-not-established")]
    [InlineData("System.Windows.Forms.Form", "explicit-usercontrol-base-not-established")]
    public void Handles_helper_http_chain_preserves_syntax_tier_and_refuses_ambiguous_anchors(string baseType, string frameworkReason)
    {
        using var source = new TempDirectory();
        using var output = new TempDirectory();
        File.WriteAllText(Path.Combine(source.Path, "PublicControl.vb"), """
            Public Class PublicControl
                Inherits System.Web.UI.UserControl
                Private Sub TriggerHandler(sender As Object, e As System.EventArgs) Handles actionButton.Click
                    ProcessAction()
                End Sub
                Protected Sub ProcessAction()
                    Dim client As New System.Net.WebClient()
                    client.UploadString("https://example.invalid/action", "POST", "synthetic")
                End Sub
            End Class
            """.Replace("System.Web.UI.UserControl", baseType, StringComparison.Ordinal));
        var scan = ScanEngine.Scan(new ScanOptions(source.Path, output.Path));
        var binding = Assert.Single(scan.Facts, f => f.FactType == FactTypes.VisualBasicEventBindingDeclared);
        Assert.DoesNotContain(scan.Facts, f => f.FactType == FactTypes.WebFormsHandlerResolved);
        var operation = Assert.Single(scan.Facts, f => f.FactType == FactTypes.HttpCallDetected);
        var handler = Assert.Single(scan.Facts, f => f.FactType == FactTypes.MethodDeclared
            && f.Properties.GetValueOrDefault("name") == "TriggerHandler");
        string Report(IEnumerable<CodeFact> facts) => MarkdownReportWriter.Build(scan with { Facts = facts.ToArray() });
        var report = Report(scan.Facts);
        Assert.Contains("Displayed groups with candidate entry paths: 1/1", report);
        Assert.Contains("1/1 VB Handles observed-event candidate anchors", report);
        Assert.Contains($"framework assessment `{frameworkReason}`", report);
        Assert.Contains("not necessarily WebForms entries", report);
        Assert.Contains("0/0 handler-resolution facts", report);
        Assert.Contains("`candidate-anchor-established`: 1", report);
        Assert.Contains("hosting/route unresolved", report);
        Assert.Contains(binding.FactId, report);
        Assert.Contains(operation.FactId, report);
        Assert.Equal(EvidenceTiers.Tier3SyntaxOrTextual, binding.EvidenceTier);
        Assert.Equal(report, Report(scan.Facts.Reverse()));
        Assert.Contains("Displayed groups with candidate entry paths: 0/1",
            Report(scan.Facts.Where(f => f.FactType != FactTypes.CallEdge)));
        Assert.Contains("Displayed groups with candidate entry paths: 0/1",
            Report(scan.Facts.Where(f => f.FactType != FactTypes.TypeDeclared)));
        Assert.Contains("Displayed groups with candidate entry paths: 0/1",
            Report(scan.Facts.Append(handler with { FactId = "ambiguous-handler" })));
        Assert.Contains("Displayed groups with candidate entry paths: 0/1",
            Report(scan.Facts.Select(f => f == binding
                ? f with { Evidence = f.Evidence with { FilePath = "Unrelated.vb" } } : f)));
        Assert.Contains("Displayed groups with candidate entry paths: 0/1",
            Report(scan.Facts.Select(f => f == binding
                ? f with { Properties = new SortedDictionary<string, string>(f.Properties.ToDictionary(p => p.Key, p => p.Value), StringComparer.Ordinal)
                    { ["handlerName"] = "OtherHandler" } } : f)));
        var type = Assert.Single(scan.Facts, f => f.FactType == FactTypes.TypeDeclared);
        var helperCall = Assert.Single(scan.Facts, f => f.FactType == FactTypes.CallEdge
            && f.Properties.GetValueOrDefault("calleeName") == "ProcessAction");
        var pathLine = Assert.Single(report.Split('\n'), line => line.StartsWith("- VB Handles observed-event", StringComparison.Ordinal));
        Assert.True(pathLine.IndexOf(binding.FactId, StringComparison.Ordinal) < pathLine.IndexOf(helperCall.FactId, StringComparison.Ordinal));
        Assert.True(pathLine.IndexOf(helperCall.FactId, StringComparison.Ordinal) < pathLine.IndexOf(operation.FactId, StringComparison.Ordinal));
        var selfCall = helperCall with { FactId = "synthetic-recursive-helper", Evidence = operation.Evidence };
        var cyclic = Report(scan.Facts.Append(selfCall));
        Assert.Contains("Displayed groups with candidate entry paths: 1/1", cyclic);
        Assert.Contains("search limited: False", cyclic);
        // The old forward-per-anchor walk hit this unrelated depth cap first and
        // stopped before reaching the real entry. Operation-directed search must
        // neither inspect this component nor mislabel the real path as truncated.
        var noise = new List<CodeFact>();
        noise.Add(type with { FactId = "!noise-type", Evidence = type.Evidence with { FilePath = "Noise.vb" },
            Properties = new Dictionary<string, string> { ["qualifiedName"] = "Noise", ["baseTypes"] = "UnavailableBase" } });
        noise.Add(binding with { FactId = "!noise-anchor", Evidence = binding.Evidence with { FilePath = "Noise.vb", StartLine = 2, EndLine = 2 },
            Properties = new Dictionary<string, string>(binding.Properties.ToDictionary(p => p.Key, p => p.Value)) { ["handlerName"] = "Noise0" } });
        for (var i = 0; i < 20; i++)
        {
            noise.Add(handler with { FactId = "noise-method-" + i,
                Evidence = handler.Evidence with { FilePath = "Noise.vb", StartLine = i * 10 + 1, EndLine = i * 10 + 1 },
                Properties = new Dictionary<string, string> { ["qualifiedContainingType"] = "Noise", ["name"] = "Noise" + i,
                    ["parameterCount"] = "0", ["bodyStartLine"] = (i * 10 + 1).ToString(), ["bodyEndLine"] = (i * 10 + 9).ToString() } });
            if (i < 19) noise.Add(helperCall with { FactId = "noise-call-" + i,
                Evidence = helperCall.Evidence with { FilePath = "Noise.vb", StartLine = i * 10 + 3, EndLine = i * 10 + 3 },
                Properties = new Dictionary<string, string> { ["receiverName"] = "", ["calleeName"] = "Noise" + (i + 1), ["argumentCount"] = "0" } });
        }
        var withNoise = Report(scan.Facts.Concat(noise));
        Assert.Contains("Displayed groups with candidate entry paths: 1/1", withNoise);
        Assert.Contains("search limited: False", withNoise);
        Assert.Contains("`candidate-path-retained`", withNoise);
        Assert.Equal(withNoise, Report(scan.Facts.Concat(noise).Reverse()));
        var deepCaller = helperCall with { FactId = "deep-source-caller",
            Evidence = helperCall.Evidence with { FilePath = "Noise.vb", StartLine = 193, EndLine = 193 },
            Properties = new Dictionary<string, string> { ["receiverName"] = "other", ["receiverType"] = handler.Properties["qualifiedContainingType"],
                ["calleeName"] = "ProcessAction", ["argumentCount"] = "0" } };
        var bounded = Report(scan.Facts.Concat(noise).Append(deepCaller));
        Assert.Contains("Displayed groups with candidate entry paths: 1/1", bounded);
        Assert.Contains("search limited: True", bounded);
        var isolated = Report(scan.Facts.Where(f => f != helperCall));
        Assert.Contains("`no-supported-source-callers-or-local-anchor`", isolated);
        void CheckProperty(CodeFact fact, string key, string value, string reason)
        {
            var changed = fact with { Properties = fact.Properties.ToDictionary(p => p.Key, p => p.Value) };
            ((Dictionary<string, string>)changed.Properties)[key] = value;
            var actual = Report(scan.Facts.Select(f => f == fact ? changed : f));
            Assert.Contains("Displayed groups with candidate entry paths: 0/1", actual);
            Assert.Contains($"`{reason}`: 1", actual);
            Assert.Contains($"anchor assessment `{reason}`", actual);
        }
        CheckProperty(binding, "handlerName", "Other", "handler-name-mismatch");
        CheckProperty(binding, "receiverName", "", "receiver-name-missing");
        CheckProperty(binding, "eventName", "", "event-name-missing");
        CheckProperty(handler, "qualifiedContainingType", "", "containing-type-identity-missing");
        CheckProperty(type, "qualifiedName", "Other", "same-file-type-declaration-missing");
        foreach (var spelling in new[] { "", "UserControl", "UI.UserControl", "CustomControl", "Forms.Control" })
        {
            var properties = type.Properties.ToDictionary(p => p.Key, p => p.Value);
            properties["baseTypes"] = spelling;
            var candidate = Report(scan.Facts.Select(f => f == type ? f with { Properties = properties } : f));
            Assert.Contains("Displayed groups with candidate entry paths: 1/1", candidate);
            Assert.Contains("0/0 handler-resolution facts", candidate);
            Assert.Contains(spelling.Length == 0 ? "framework assessment `same-file-base-evidence-missing`"
                : "framework assessment `explicit-usercontrol-base-not-established`", candidate);
        }
        Assert.Contains("`method-owner-ambiguous`: 1", Report(scan.Facts.Append(handler with { FactId = "duplicate-owner" })));
        Assert.Contains("`method-owner-missing`: 1", Report(scan.Facts.Where(f => f != handler)));
        Assert.Contains("`same-file-type-declaration-ambiguous`: 1", Report(scan.Facts.Append(type with { FactId = "duplicate-type" })));
        // A split partial's base evidence must not silently become same-file identity proof.
        Assert.Contains("`same-file-type-declaration-missing`: 1", Report(scan.Facts.Select(f => f == type
            ? f with { Evidence = f.Evidence with { FilePath = "PublicControl.Designer.vb" } } : f)));
        Assert.Contains("`matching-budget-exceeded-unassessed`: 1", Report(scan.Facts.Concat(
            Enumerable.Range(0, 2001).Select(i => type with { FactId = "type-limit-" + i }))));
    }

    [Fact]
    public void Projectless_webforms_to_backend_sql_and_http_is_one_bounded_report()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "AGENTS.md"))) root = root.Parent;
        Assert.NotNull(root);
        using var temp = new TempDirectory();
        var scan = ScanEngine.Scan(new ScanOptions(Path.Combine(root.FullName, "samples", "migration-vertical-slice"), temp.Path));
        var report = MarkdownReportWriter.Build(scan);
        Assert.Contains(scan.Facts, f => f.FactType == FactTypes.WebFormsHandlerResolved);
        Assert.Contains(scan.Facts, f => f.FactType == FactTypes.HttpCallDetected);
        Assert.Contains(scan.Facts, f => f.FactType == FactTypes.DatabaseOperationCandidate);
        Assert.Contains("candidate ordered source path", report);
        Assert.Contains("unresolved—not proof of unused code", report);
        Assert.Contains("value observations `1`", report);
        Assert.Contains("bounded-local-constant-syntax-candidate", report);
        Assert.Contains("outbound-http:", report);
        Assert.Contains("database:", report);
        Assert.Contains("Review pending:", report);
        Assert.Contains("Displayed groups with candidate entry paths: 2/3", report);
        Assert.Contains("Parameter facts not displayed in a group: 0", report);
        Assert.Contains("Parameter witnesses: 5", report);
        Assert.Contains("PublicWidget.ascx", report);
        Assert.Equal(report, MarkdownReportWriter.Build(scan with { Facts = scan.Facts.Reverse().ToArray() }));
        var disconnected = MarkdownReportWriter.Build(scan with { Facts = scan.Facts.Where(f => f.FactType != FactTypes.CallEdge).ToArray() });
        Assert.Contains("Displayed groups with candidate entry paths: 0/3", disconnected);
        Assert.Contains("3 callsite evidence groups", disconnected);
        Assert.Contains("Entry path unresolved:", disconnected);
        var unbound = MarkdownReportWriter.Build(scan with { Facts = scan.Facts.Where(f => f.FactType != FactTypes.WebFormsHandlerResolved).ToArray() });
        Assert.Contains("Bindings without retained handler resolution: 2", unbound);
        Assert.Contains("Displayed groups with candidate entry paths: 0/3", unbound);
        Assert.DoesNotContain("DELETE FROM PublicRows", report);
        var save = Assert.Single(scan.Facts, f => f.FactType == FactTypes.MethodDeclared && f.Properties.GetValueOrDefault("name") == "Save");
        var ambiguous = save with { FactId = "synthetic-duplicate", Evidence = save.Evidence with { FilePath = "Other.vb" } };
        var ambiguousReport = MarkdownReportWriter.Build(scan with { Facts = [.. scan.Facts, ambiguous] });
        Assert.Contains("Displayed groups with candidate entry paths: 0/3", ambiguousReport);
        var tooMany = Enumerable.Range(0, 2001).Select(i => save with { FactId = "synthetic-limit-" + i }).ToArray();
        var limited = MarkdownReportWriter.Build(scan with { Facts = [.. scan.Facts, .. tooMany] });
        Assert.Contains("limited: method/call budget exceeded", limited);
        Assert.Contains("3 callsite evidence groups", limited);
    }
}
