using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class AppCodeRegistrationMatrixTests
{
    public static IEnumerable<object[]> InteractionCases()
    {
        foreach (var receiver in new[] { "Widget", "New Widget()", "GetWidget()" })
        foreach (var condition in new[] { "Not Page.IsPostBack", "IsPostBack = False", "shadowed" })
            yield return new object[] { receiver, condition };
    }

    [Theory]
    [MemberData(nameof(InteractionCases))]
    public void Checked_in_sample_combines_registration_events_lifecycle_and_postback(string receiver, string condition)
    {
        using var temp = new TempDirectory();
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !Directory.Exists(Path.Combine(root.FullName, "samples", "webforms-pattern-matrix"))) root = root.Parent;
        Assert.NotNull(root);
        var sample = Path.Combine(root.FullName, "samples", "webforms-pattern-matrix");
        foreach (var file in Directory.EnumerateFiles(sample, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(temp.Path, Path.GetRelativePath(sample, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }
        var code = Path.Combine(temp.Path, "Page.aspx.vb");
        var source = File.ReadAllText(code).Replace("With Widget", "With " + receiver, StringComparison.Ordinal);
        source = condition == "shadowed"
            ? source.Replace("Sub Page_Load()", "Sub Page_Load()\nDim Page As Object", StringComparison.Ordinal)
            : source.Replace("Not Page.IsPostBack", condition, StringComparison.Ordinal);
        File.WriteAllText(code, source);
        var manifest = new ScanManifest("interaction", "synthetic", null, "main", "abc123", "test/1.0", DateTimeOffset.UnixEpoch, "Level3SyntaxAnalysis", "NotRun", [], [], [], []);
        var inventory = FileInventory.Collect(temp.Path);
        var syntax = VisualBasicSyntaxExtractor.Extract(temp.Path, manifest, inventory, null);
        var facts = LegacyWebFormsExtractor.Extract(temp.Path, manifest, inventory, syntax);
        Assert.Single(facts, f => f.ContractElement == "UsesRegisteredAppCodeControlCandidate");
        Assert.Equal(receiver == "GetWidget()" ? 0 : 1, syntax.Count(f => f.FactType == FactTypes.VisualBasicEventBindingDeclared && f.Properties.GetValueOrDefault("wiringKind") == "AddHandler"));
        Assert.Equal(condition == "shadowed" ? 0 : 1, facts.Count(f => f.FactType == FactTypes.WebFormsLifecycleBranchCandidate));
        Assert.Single(facts, f => f.FactType == FactTypes.WebFormsPostBackTargetCandidate);
        Assert.Single(facts, f => f.Properties.GetValueOrDefault("gapKind") == "DynamicWebFormsPostBackTarget");
    }

    [Theory]
    [InlineData("clear")]
    [InlineData("remove")]
    [InlineData("other-location")]
    [InlineData("invalid-assembly")]
    [InlineData("invalid-src")]
    [InlineData("missing-facts")]
    [InlineData("uninventoried")]
    [InlineData("interface")]
    public void Registration_guards_do_not_invent_targets(string scenario)
    {
        using var temp = new TempDirectory();
        Directory.CreateDirectory(Path.Combine(temp.Path, "App_Code"));
        Directory.CreateDirectory(Path.Combine(temp.Path, "Child"));
        var extra = scenario == "invalid-assembly" ? " assembly=\"${dynamic}\"" : scenario == "invalid-src" ? " src=\"${dynamic}\"" : "";
        var section = $"<system.web><pages><controls><add tagPrefix=\"local\" namespace=\"PublicControls\"{extra} /></controls></pages></system.web>";
        File.WriteAllText(Path.Combine(temp.Path, "web.config"), "<configuration>"
            + (scenario == "other-location" ? "<location path=\"Elsewhere\">" + section + "</location>" : section) + "</configuration>");
        if (scenario is "clear" or "remove") File.WriteAllText(Path.Combine(temp.Path, "Child", "web.config"),
            "<configuration><system.web><pages><controls>" + (scenario == "clear" ? "<clear />" : "<remove tagPrefix=\"local\" namespace=\"PublicControls\" />")
            + "</controls></pages></system.web></configuration>");
        File.WriteAllText(Path.Combine(temp.Path, "Child", "Page.aspx"), "<%@ Page Language=\"VB\" Inherits=\"Page\" %>\n<local:Widget ID=\"One\" runat=\"server\" />");
        var kind = scenario == "interface" ? "Interface" : "Class";
        File.WriteAllText(Path.Combine(temp.Path, "App_Code", "Widget.vb"), $"Namespace PublicControls\nPublic {kind} Widget\nEnd {kind}\nEnd Namespace");
        var manifest = new ScanManifest("guards", "synthetic", null, "main", "abc123", "test/1.0", DateTimeOffset.UnixEpoch, "Level3SyntaxAnalysis", "NotRun", [], [], [], []);
        var inventory = FileInventory.Collect(temp.Path);
        var types = VisualBasicSyntaxExtractor.Extract(temp.Path, manifest, inventory, null);
        var facts = LegacyWebFormsExtractor.Extract(temp.Path, manifest,
            scenario == "uninventoried" ? inventory.Where(f => !f.RelativePath.EndsWith("Widget.vb", StringComparison.Ordinal)).ToArray() : inventory,
            scenario == "missing-facts" ? [] : types);
        Assert.DoesNotContain(facts, f => f.ContractElement == "UsesRegisteredAppCodeControlCandidate");
        Assert.Contains(facts, f => f.FactType == FactTypes.AnalysisGap && f.RuleId == RuleIds.LegacyWebFormsComposition);
    }

    public static IEnumerable<object[]> Cases()
    {
        foreach (var language in new[] { "vb", "cs" })
        foreach (var registration in new[] { "config", "directive" })
        foreach (var scope in new[] { "root", "child", "foreign-root" })
        foreach (var target in new[] { "unique", "missing", "duplicate", "wrong-namespace", "outside-app-code", "case-decoy" })
            yield return new object[] { language, registration, scope, target };
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void Namespace_only_registration_matrix(string language, string registration, string scope, string target)
    {
        using var temp = new TempDirectory();
        void Write(string path, string content)
        {
            var full = Path.Combine(temp.Path, path);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, content);
        }
        var page = scope == "child" ? "Web/Child/Page.aspx" : "Web/Page.aspx";
        Write("Web/web.config", registration == "config" ? """
            <configuration><system.web><pages><controls>
            <add tagPrefix="local" namespace="PublicControls" />
            </controls></pages></system.web></configuration>
            """ : "<configuration />");
        Write(page, "<%@ Page Language=\"VB\" Inherits=\"SamplePage\" %>\n"
            + (registration == "directive" ? "<%@ Register TagPrefix=\"local\" Namespace=\"PublicControls\" %>\n" : "")
            + "<local:Widget runat=\"server\" ID=\"One\" />\n<local:Widget runat=\"server\" ID=\"Two\" />");
        var ns = target == "wrong-namespace" ? "OtherControls" : target == "case-decoy" ? "publiccontrols" : "PublicControls";
        var declaration = language == "vb"
            ? $"Namespace {ns}\nPublic Class Widget\nEnd Class\nEnd Namespace"
            : $"namespace {ns} {{ public class Widget {{ }} }}";
        var sourceRoot = scope == "foreign-root" ? "Other/App_Code" : target == "outside-app-code" ? "Web/Controls" : "Web/App_Code";
        if (target != "missing") Write($"{sourceRoot}/Widget.{language}", declaration);
        if (target == "duplicate") Write($"{sourceRoot}/Duplicate.{language}", declaration);
        var manifest = new ScanManifest("matrix", "synthetic", null, "main", "abc123", "test/1.0",
            DateTimeOffset.UnixEpoch, "Level3SyntaxAnalysis", "NotRun", [], [], [], []);
        var inventory = FileInventory.Collect(temp.Path);
        var types = VisualBasicSyntaxExtractor.Extract(temp.Path, manifest, inventory, null)
            .Concat(CSharpSyntaxExtractor.Extract(temp.Path, manifest, inventory)).ToArray();
        var facts = LegacyWebFormsExtractor.Extract(temp.Path, manifest, inventory, types);
        var expected = target == "unique" && scope != "foreign-root";
        var joins = facts.Where(f => f.ContractElement == "UsesRegisteredAppCodeControlCandidate").ToArray();
        Assert.Equal(expected ? 2 : 0, joins.Length);
        Assert.All(joins, fact =>
        {
            Assert.Equal(EvidenceTiers.Tier3SyntaxOrTextual, fact.EvidenceTier);
            Assert.Equal("PublicControls.Widget", fact.TargetSymbol);
            Assert.Contains(types, type => type.FactType == FactTypes.TypeDeclared
                && fact.Properties["supportingFactIds"].Split(',').Contains(type.FactId));
        });
        Assert.DoesNotContain(facts, f => f.ContractElement == "UsesRegisteredAssemblyControl");
        Assert.Equal(expected ? 0 : 2, facts.Count(f => f.FactType == FactTypes.AnalysisGap
            && f.RuleId == RuleIds.LegacyWebFormsComposition));
        Assert.Equal(facts.Select(f => f.FactId), LegacyWebFormsExtractor.Extract(temp.Path, manifest,
            inventory.Reverse().ToArray(), types.Reverse().ToArray()).Select(f => f.FactId));
    }
}
