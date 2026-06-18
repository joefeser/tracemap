using System.Text.Json;
using TraceMap.Core;
using TraceMap.Reporting;

namespace TraceMap.Tests;

public sealed class LegacyWinFormsExtractorTests
{
    [Fact]
    public void Scan_extracts_designer_binding_handler_navigation_callback_resource_and_report_sections()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        WriteProgram(repo);
        WriteMainForm(repo, """
            var details = new DetailsForm();
            details.ShowDialog(this);
            var sql = "select Id from Orders";
            """);
        WriteDesigner(repo, """
            this.saveButton = new System.Windows.Forms.Button();
            this.timer1 = new System.Windows.Forms.Timer(this.components);
            this.backgroundWorker1 = new System.ComponentModel.BackgroundWorker();
            this.saveButton.Click += new System.EventHandler(this.SaveButton_Click);
            this.timer1.Tick += this.Timer1_Tick;
            this.backgroundWorker1.DoWork += new System.ComponentModel.DoWorkEventHandler(this.BackgroundWorker1_DoWork);
            """);
        File.WriteAllText(Path.Combine(repo, "DetailsForm.cs"), """
            using System.Windows.Forms;
            namespace Sample;
            public partial class DetailsForm : Form { }
            """);
        File.WriteAllText(Path.Combine(repo, "MainForm.resx"), """
            <?xml version="1.0" encoding="utf-8"?>
            <root>
              <data name="WelcomeText" xml:space="preserve"><value>Do not store this private label</value></data>
              <data name="ConnectionString" xml:space="preserve"><value>Server=secret;Password=keepout</value></data>
            </root>
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));

        Assert.Contains(result.Inventory, item => item is { RelativePath: "MainForm.Designer.cs", Kind: "WinFormsDesigner" });
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.WinFormsSurfaceDeclared && fact.TargetSymbol == "Sample.MainForm");
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.WinFormsControlDeclared && fact.Properties.GetValueOrDefault("controlId") == "saveButton");
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.WinFormsEventBindingDeclared && fact.Properties.GetValueOrDefault("handlerName") == "SaveButton_Click" && fact.EvidenceTier == EvidenceTiers.Tier2Structural);
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.WinFormsHandlerResolved && fact.ContractElement == "SaveButton_Click");
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.WinFormsNavigationEdgeDeclared && fact.Properties.GetValueOrDefault("targetFormTypeName") == "DetailsForm");
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.WinFormsCallbackBoundaryDeclared && fact.Properties.GetValueOrDefault("boundaryClassification") == "TimerCallbackBoundary");
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.WinFormsCallbackBoundaryDeclared && fact.Properties.GetValueOrDefault("boundaryClassification") == "BackgroundWorkerBoundary");
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.WinFormsResourceMetadataDeclared && !string.IsNullOrWhiteSpace(fact.Properties.GetValueOrDefault("resourceKeyHashes")));
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.WinFormsHandlerFlowProjected && fact.Properties.GetValueOrDefault("flowClassification") != "NoBackendEvidence");

        var serialized = SerializeFacts(result.Facts.Where(fact => fact.FactType.StartsWith("WinForms", StringComparison.Ordinal)));
        Assert.DoesNotContain("Do not store this private label", serialized);
        Assert.DoesNotContain("Password=keepout", serialized);
        Assert.DoesNotContain("select Id from Orders", serialized);

        var report = MarkdownReportWriter.Build(result);
        Assert.Contains("## WinForms Static Evidence", report);
        Assert.Contains("## WinForms Events", report);
        Assert.Contains("## WinForms Navigation And Callbacks", report);
        Assert.Contains("## WinForms Handler Flow", report);
        Assert.Contains("## WinForms Limitations", report);
    }

    [Fact]
    public void Scan_extracts_code_subscription_as_syntax_tier_binding()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "MainForm.cs"), """
            using System;
            using System.Windows.Forms;
            namespace Sample;
            public partial class MainForm : Form
            {
                private readonly Button saveButton = new Button();
                public MainForm()
                {
                    saveButton.Click += SaveButton_Click;
                }

                private void SaveButton_Click(object sender, EventArgs e) { }
            }
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));

        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.WinFormsEventBindingDeclared
            && fact.EvidenceTier == EvidenceTiers.Tier3SyntaxOrTextual
            && fact.Properties.GetValueOrDefault("bindingKind") == "MethodGroup");
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.WinFormsHandlerResolved
            && fact.Properties.GetValueOrDefault("resolutionKind") == "SyntaxScopedMethod");
    }

    [Fact]
    public void Scan_emits_gap_for_lambda_event_subscription_without_guessing_handler()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "MainForm.cs"), """
            using System.Windows.Forms;
            namespace Sample;
            public partial class MainForm : Form
            {
                private readonly Button saveButton = new Button();
                public MainForm()
                {
                    saveButton.Click += (sender, args) => Show();
                }
            }
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));

        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.WinFormsEventBindingDeclared
            && fact.EvidenceTier == EvidenceTiers.Tier4Unknown
            && fact.Properties.GetValueOrDefault("needsReview") == "True");
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyWinFormsEventBinding
            && fact.Properties.GetValueOrDefault("classification") == "UnsupportedWinFormsEventSubscription");
        Assert.DoesNotContain(result.Facts, fact => fact.FactType == FactTypes.WinFormsHandlerResolved);
    }

    [Fact]
    public void Scan_rejects_unsafe_resx_without_rendering_raw_resource_values()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        WriteMainForm(repo, "");
        File.WriteAllText(Path.Combine(repo, "MainForm.resx"), """
            <!DOCTYPE root [
              <!ENTITY privateValue SYSTEM "file:///private/value.txt">
            ]>
            <root>
              <data name="Unsafe"><value>&privateValue;</value></data>
            </root>
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));

        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyWinFormsResourceMetadata
            && fact.Properties.GetValueOrDefault("classification") == "WinFormsResourceParserSecurityRejected");
        var serialized = SerializeFacts(result.Facts.Where(fact => fact.RuleId.StartsWith("legacy.winforms", StringComparison.Ordinal)));
        Assert.DoesNotContain("privateValue", serialized);
        Assert.DoesNotContain("file:///private/value.txt", serialized);
    }

    [Fact]
    public void Scan_produces_stable_winforms_fact_ids_for_same_commit_and_inputs()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        WriteMainForm(repo, "");
        WriteDesigner(repo, "this.saveButton.Click += this.SaveButton_Click;");

        var first = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out-a")));
        var second = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out-b")));

        var firstIds = first.Facts
            .Where(fact => fact.FactType.StartsWith("WinForms", StringComparison.Ordinal))
            .Select(fact => fact.FactId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var secondIds = second.Facts
            .Where(fact => fact.FactType.StartsWith("WinForms", StringComparison.Ordinal))
            .Select(fact => fact.FactId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(firstIds);
        Assert.Equal(firstIds, secondIds);
    }

    [Fact]
    public void Scan_emits_missing_and_ambiguous_handler_gaps_without_guessing()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "MainForm.cs"), """
            using System;
            using System.Windows.Forms;
            namespace Sample;
            public partial class MainForm : Form
            {
                private Button missingButton = new Button();
                private Button ambiguousButton = new Button();
                public MainForm()
                {
                    missingButton.Click += Missing_Click;
                    ambiguousButton.Click += SaveButton_Click;
                }

                private void SaveButton_Click(object sender, EventArgs e) { }
                private void SaveButton_Click(object sender, EventArgs e, string extra = "") { }
            }
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));

        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyWinFormsHandlerResolution
            && fact.Properties.GetValueOrDefault("classification") == "MissingWinFormsPartialClass");
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyWinFormsHandlerResolution
            && fact.Properties.GetValueOrDefault("classification") == "AmbiguousWinFormsHandler");
        Assert.DoesNotContain(result.Facts, fact =>
            fact.FactType == FactTypes.WinFormsHandlerResolved
            && fact.ContractElement == "SaveButton_Click");
    }

    [Fact]
    public void Scan_classifies_non_control_begininvoke_as_async_delegate_boundary()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "MainForm.cs"), """
            using System;
            using System.Windows.Forms;
            namespace Sample;
            public partial class MainForm : Form
            {
                private readonly Button saveButton = new Button();
                private readonly Action worker = () => { };
                public void Schedule()
                {
                    this.BeginInvoke(worker);
                    saveButton.BeginInvoke(worker);
                    worker.BeginInvoke(null, null);
                }
            }
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));

        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.WinFormsCallbackBoundaryDeclared
            && fact.Properties.GetValueOrDefault("receiverName") == "worker"
            && fact.Properties.GetValueOrDefault("boundaryClassification") == "AsyncDelegateBoundary");
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.WinFormsCallbackBoundaryDeclared
            && fact.Properties.GetValueOrDefault("receiverName") == "saveButton"
            && fact.Properties.GetValueOrDefault("boundaryClassification") == "UiMarshalBoundary");
    }

    [Fact]
    public void Scan_scopes_same_named_handler_backend_evidence_to_resolved_file()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "MainForm.cs"), """
            using System;
            using System.Windows.Forms;
            namespace Sample;
            public partial class MainForm : Form
            {
                private readonly Button saveButton = new Button();
                public MainForm() { saveButton.Click += SaveButton_Click; }
                private void SaveButton_Click(object sender, EventArgs e) { }
            }
            """);
        File.WriteAllText(Path.Combine(repo, "OtherForm.cs"), """
            using System;
            using System.Windows.Forms;
            namespace Sample;
            public partial class OtherForm : Form
            {
                private void SaveButton_Click(object sender, EventArgs e)
                {
                    var sql = "select Id from OtherOrders";
                }
            }
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));

        var flow = Assert.Single(result.Facts, fact =>
            fact.FactType == FactTypes.WinFormsHandlerFlowProjected
            && fact.Evidence.FilePath == "MainForm.cs");
        Assert.DoesNotContain("OtherForm.cs", flow.Properties.GetValueOrDefault("supportingFactIds") ?? string.Empty);
        Assert.NotEqual("sql-query", flow.Properties.GetValueOrDefault("terminalSurfaceKind"));
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyWinFormsHandlerFlow
            && fact.Properties.GetValueOrDefault("classification") == "WinFormsBackendPathUnavailable");
    }

    [Fact]
    public void Scan_does_not_emit_dynamic_control_or_reflection_gaps_for_ordinary_create_and_gettype_calls()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "MainForm.cs"), """
            using System;
            using System.Windows.Forms;
            namespace Sample;
            public partial class MainForm : Form
            {
                public void Work()
                {
                    CreateOrder();
                    GetType();
                }

                private object CreateOrder() => new object();
            }
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));

        Assert.DoesNotContain(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.Properties.GetValueOrDefault("classification") is "DynamicWinFormsControlCreation" or "WinFormsReflectionBoundary");
    }

    [Fact]
    public void Scan_hashes_winforms_terminal_surface_names_with_fact_factory_digest()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        WriteMainForm(repo, """
            var values = new[] { 1, 2, 3 };
            var filtered = values.Where(value => value > 1);
            """);
        WriteDesigner(repo, "this.saveButton.Click += this.SaveButton_Click;");

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));

        var flow = Assert.Single(result.Facts, fact =>
            fact.FactType == FactTypes.WinFormsHandlerFlowProjected
            && fact.Properties.GetValueOrDefault("terminalSurfaceKind") == "sql-query");
        Assert.Equal(FactFactory.Hash("Where", 32), flow.Properties.GetValueOrDefault("terminalSurfaceNameHash"));
    }

    [Fact]
    public void Scan_keeps_initializecomponent_event_binding_in_non_designer_file_at_syntax_tier()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "MainForm.cs"), """
            using System;
            using System.Windows.Forms;
            namespace Sample;
            public partial class MainForm : Form
            {
                private Button saveButton = new Button();
                private void InitializeComponent()
                {
                    saveButton.Click += SaveButton_Click;
                }

                private void SaveButton_Click(object sender, EventArgs e) { }
            }
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));

        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.WinFormsEventBindingDeclared
            && fact.Properties.GetValueOrDefault("handlerName") == "SaveButton_Click"
            && fact.EvidenceTier == EvidenceTiers.Tier3SyntaxOrTextual);
    }

    private static void WriteProgram(string repo)
    {
        File.WriteAllText(Path.Combine(repo, "Program.cs"), """
            using System;
            using System.Windows.Forms;
            namespace Sample;
            internal static class Program
            {
                [STAThread]
                private static void Main()
                {
                    Application.Run(new MainForm());
                }
            }
            """);
    }

    private static void WriteMainForm(string repo, string handlerBody)
    {
        File.WriteAllText(Path.Combine(repo, "MainForm.cs"), $$"""
            using System;
            using System.ComponentModel;
            using System.Windows.Forms;
            namespace Sample;
            public partial class MainForm : Form
            {
                private void SaveButton_Click(object sender, EventArgs e)
                {
                    {{handlerBody}}
                }

                private void Timer1_Tick(object sender, EventArgs e) { }
                private void BackgroundWorker1_DoWork(object sender, DoWorkEventArgs e) { }
            }
            """);
    }

    private static void WriteDesigner(string repo, string initializeBody)
    {
        File.WriteAllText(Path.Combine(repo, "MainForm.Designer.cs"), $$"""
            namespace Sample;
            public partial class MainForm
            {
                private System.ComponentModel.IContainer components = null;
                private System.Windows.Forms.Button saveButton;
                private System.Windows.Forms.Timer timer1;
                private System.ComponentModel.BackgroundWorker backgroundWorker1;

                private void InitializeComponent()
                {
                    {{initializeBody}}
                }
            }
            """);
    }

    private static string SerializeFacts(IEnumerable<CodeFact> facts)
    {
        return string.Join("\n", facts.Select(fact => JsonSerializer.Serialize(fact)));
    }
}
