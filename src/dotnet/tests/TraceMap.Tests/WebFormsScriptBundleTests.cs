using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class WebFormsScriptBundleTests
{
    private static readonly ScanManifest Manifest = new("bundles", "synthetic", null, "test", "abc123", "test/1.0",
        DateTimeOffset.UnixEpoch, "Level1SemanticAnalysisReduced", "FailedOrPartial", [], [], [], []);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Explicit_members_and_master_reference_retain_separate_source_evidence(bool vb)
    {
        using var temp = new TempDirectory();
        Write(temp.Path, "BundleConfig." + (vb ? "vb" : "cs"), Config(vb, "\"~/Scripts/global.js\", \"~/Scripts/main.js\""));
        Write(temp.Path, "Site.master", "<%@ Master Language=\"VB\" %>\n<%: Scripts.Render(\"~/bundles/app\") %>");
        Write(temp.Path, "Default.aspx", "<%@ Page Language=\"VB\" MasterPageFile=\"~/Site.master\" %>\n<button onclick=\"saveRecord()\">Save</button>");
        Write(temp.Path, "Scripts/main.js", "function saveRecord() { fetch('/api/Save.ashx'); }");
        var inventory = FileInventory.Collect(temp.Path);
        var facts = Extract(temp.Path, inventory);
        var declaration = Assert.Single(facts, fact => fact.FactType == FactTypes.WebFormsScriptBundleDeclaredCandidate);
        var members = facts.Where(fact => fact.FactType == FactTypes.WebFormsScriptBundleMemberCandidate).OrderBy(fact => fact.Properties["declaredOrdinal"]).ToArray();
        Assert.Equal(new[] { "~/Scripts/global.js", "~/Scripts/main.js" }, members.Select(fact => fact.TargetSymbol));
        Assert.All(members, fact => Assert.Equal(declaration.FactId, fact.Properties["bundleDeclarationFactId"]));
        var reference = Assert.Single(facts, fact => fact.FactType == FactTypes.WebFormsScriptBundleReferenceCandidate);
        Assert.Equal("Site.master", reference.Evidence.FilePath);
        Assert.Equal(2, reference.Evidence.StartLine);
        Assert.Equal("~/bundles/app", reference.TargetSymbol);
        Assert.NotEqual(reference.Properties["targetSymbolId"], declaration.Properties["targetSymbolId"]);
        using var binary = File.OpenRead(typeof(LegacyWebFormsExtractor).Assembly.Location);
        var generator = Convert.ToHexStringLower(SHA256.HashData(binary));
        Assert.All(facts, fact =>
        {
            Assert.Equal(Manifest.CommitSha, fact.CommitSha);
            Assert.Equal(ScannerVersions.LegacyWebFormsExtractor, fact.Evidence.ExtractorVersion);
            Assert.Equal(EvidenceTiers.Tier3SyntaxOrTextual, fact.EvidenceTier);
            Assert.Equal(generator, fact.Properties["generatorSha256"]);
            var source = File.ReadAllText(Path.Combine(temp.Path, fact.Evidence.FilePath));
            var expectedHash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(source)));
            Assert.Equal(expectedHash, fact.Properties["boundedInputSha256"]);
            Assert.Equal(expectedHash, fact.Evidence.SnippetHash);
        });
        Assert.Contains(inventory, item => item.RelativePath == "Scripts/main.js" && item.Kind == "JavaScript");
        Assert.Equal(JsonSerializer.Serialize(facts), JsonSerializer.Serialize(Extract(temp.Path, inventory.Reverse().ToArray())));
    }

    [Theory]
    [InlineData("\"~/Scripts/*.js\"", "UnsupportedBundleMember")]
    [InlineData("\"~/Scripts/jquery-{version}.js\"", "UnsupportedBundleMember")]
    [InlineData("\"~/../outside.js\"", "UnsupportedBundleMember")]
    [InlineData("\"https://example.invalid/main.js\"", "UnsupportedBundleMember")]
    [InlineData("ResolvePath()", "UnsupportedBundleMember")]
    [InlineData("\"~/Scripts/main.js\", ResolvePath()", "UnsupportedBundleMember")]
    public void Unsupported_members_refuse_entire_declaration(string members, string reason)
    {
        using var temp = new TempDirectory();
        Write(temp.Path, "BundleConfig.cs", Config(false, members));
        var facts = Extract(temp.Path);
        Assert.Equal(reason, Assert.Single(facts).Properties["gapKind"]);
        Assert.Equal(EvidenceTiers.Tier4Unknown, facts[0].EvidenceTier);
    }

    [Fact]
    public void Member_limit_is_explicit_and_does_not_publish_partial_members()
    {
        using var temp = new TempDirectory();
        Write(temp.Path, "BundleConfig.cs", Config(false, string.Join(",", Enumerable.Repeat("\"~/Scripts/main.js\"", 129))));
        Assert.Equal("BundleMemberLimit", Assert.Single(Extract(temp.Path)).Properties["gapKind"]);
    }

    [Fact]
    public void Same_virtual_path_declarations_are_not_collapsed_or_resolved_by_name()
    {
        using var temp = new TempDirectory();
        Write(temp.Path, "First/BundleConfig.cs", Config(false, "\"~/Scripts/first.js\""));
        Write(temp.Path, "Second/BundleConfig.cs", Config(false, "\"~/Scripts/second.js\""));
        Write(temp.Path, "Site.master", "<%@ Master %><%: Scripts.Render(\"~/bundles/app\") %>");
        var facts = Extract(temp.Path);
        var declarations = facts.Where(fact => fact.FactType == FactTypes.WebFormsScriptBundleDeclaredCandidate).ToArray();
        Assert.Equal(2, declarations.Length);
        Assert.Equal(2, declarations.Select(fact => fact.SourceSymbol).Distinct().Count());
        var reference = Assert.Single(facts, fact => fact.FactType == FactTypes.WebFormsScriptBundleReferenceCandidate);
        Assert.False(reference.Properties.ContainsKey("bundleDeclarationFactId"));
        Assert.All(facts, fact => Assert.Equal("declared-syntax-only", fact.Properties["resolution"]));
    }

    [Fact]
    public void Comments_strings_and_non_framework_type_names_are_not_bundle_declarations()
    {
        using var temp = new TempDirectory();
        Write(temp.Path, "Config.cs", """
            class Config { string text = "new ScriptBundle(\"~/bundles/app\")";
            // new ScriptBundle("~/bundles/app").Include("~/Scripts/main.js");
            void Register() { var x = new Other.ScriptBundle("~/bundles/app").Include("~/Scripts/main.js"); }
            }
            """);
        Write(temp.Path, "Site.master", "<%@ Master %><%-- <%: Scripts.Render(\"~/bundles/app\") %> --%><script>const x = 'Scripts.Render(\"~/bundles/app\")';</script>");
        Assert.Empty(Extract(temp.Path));
    }

    [Fact]
    public void Malformed_source_and_directory_include_remain_gaps()
    {
        using var temp = new TempDirectory();
        Write(temp.Path, "Broken.cs", Config(false, "\"~/Scripts/main.js\"") + "{{");
        Write(temp.Path, "Directory.cs", "class C { void M() { var b = new ScriptBundle(\"~/bundles/app\").IncludeDirectory(\"~/Scripts\", \"*.js\"); } }");
        var facts = Extract(temp.Path);
        Assert.Equal(2, facts.Length);
        Assert.Contains(facts, fact => fact.Properties.GetValueOrDefault("gapKind") == "MalformedBundleSource");
        Assert.Contains(facts, fact => fact.Properties.GetValueOrDefault("gapKind") == "UnsupportedBundleInclude");
    }

    [Fact]
    public void Dynamic_render_and_source_limit_are_explicit()
    {
        using var temp = new TempDirectory();
        Write(temp.Path, "Site.master", "<%@ Master %><%: Scripts.Render(GetBundle()) %>");
        Write(temp.Path, "Large.cs", new string(' ', 1_048_577));
        var facts = Extract(temp.Path);
        Assert.Contains(facts, fact => fact.Properties.GetValueOrDefault("gapKind") == "UnsupportedBundleRender");
        var limit = Assert.Single(facts, fact => fact.Properties.GetValueOrDefault("gapKind") == "BundleSourceLimit");
        var unsupported = Assert.Single(facts, fact => fact.Properties.GetValueOrDefault("gapKind") == "UnsupportedBundleRender");
        Assert.NotNull(unsupported.Evidence.SnippetHash);
        Assert.Equal(unsupported.Evidence.SnippetHash, unsupported.Properties["boundedInputSha256"]);
        Assert.Equal(unsupported.Properties["generatorSha256"], limit.Properties["generatorSha256"]);
        Assert.Null(limit.Evidence.SnippetHash);
        Assert.False(limit.Properties.ContainsKey("boundedInputSha256"));
        Assert.DoesNotContain(facts, fact => fact.FactType != FactTypes.AnalysisGap);
    }

    [Fact]
    public void Repeated_render_sites_on_one_line_keep_unique_fact_ids()
    {
        using var temp = new TempDirectory();
        Write(temp.Path, "Site.master", "<%@ Master %><%: Scripts.Render(\"~/bundles/app\") %><%: Scripts.Render(\"~/bundles/app\") %>");
        var facts = Extract(temp.Path);
        Assert.Equal(2, facts.Length);
        Assert.Equal(2, facts.Select(fact => fact.FactId).Distinct().Count());
    }

    [Fact]
    public void Declaration_and_render_block_limits_are_retained()
    {
        using var temp = new TempDirectory();
        Write(temp.Path, "Config.cs", "class C { void M() {" + string.Concat(Enumerable.Repeat("new ScriptBundle(\"~/bundles/app\").Include(\"~/Scripts/main.js\");", 257)) + "} }");
        Write(temp.Path, "Site.master", "<%@ Master %>" + string.Concat(Enumerable.Repeat("<%: Scripts.Render(\"~/bundles/app\") %>", 257)));
        var facts = Extract(temp.Path);
        Assert.Equal(256, facts.Count(fact => fact.FactType == FactTypes.WebFormsScriptBundleDeclaredCandidate));
        Assert.Equal(256, facts.Count(fact => fact.FactType == FactTypes.WebFormsScriptBundleReferenceCandidate));
        Assert.Contains(facts, fact => fact.Properties.GetValueOrDefault("gapKind") == "BundleDeclarationLimit");
        Assert.Contains(facts, fact => fact.Properties.GetValueOrDefault("gapKind") == "BundleRenderBlockLimit");
    }

    [Fact]
    public void Visual_basic_names_are_case_insensitive_but_paths_retain_case()
    {
        using var temp = new TempDirectory();
        Write(temp.Path, "Config.vb", Config(true, "\"~/Scripts/Main.js\"").Replace("ScriptBundle", "scriptbundle").Replace("Include(", "include("));
        Write(temp.Path, "Site.master", "<%@ Master Language=\"VB\" %><%: scripts.render(\"~/bundles/app\") %>");
        var facts = Extract(temp.Path);
        Assert.Equal(3, facts.Length);
        Assert.Contains(facts, fact => fact.TargetSymbol == "~/Scripts/Main.js");
    }

    private const string ApplicationStart = """
        <%@ Application Language="VB" %>
        <%@ Import Namespace="System.Web.Optimization" %>
        <script runat="server">
        Sub Application_Start(sender As Object, e As EventArgs)
            Dim first As New ScriptBundle("~/bundles/first")
            Dim second = New ScriptBundle("~/bundles/second")
            first.Include("~/Scripts/a.js")
            second.Include("~/Scripts/b.js")
            FIRST.Include("~/Scripts/c.js")
            BundleTable.Bundles.Add(first)
            BundleTable.Bundles.Add(second)
        End Sub
        </script>
        """;

    [Fact]
    public void Global_asax_interleaved_locals_keep_declaration_ids_ordinals_and_physical_lines()
    {
        using var temp = new TempDirectory();
        Write(temp.Path, "Global.asax", ApplicationStart);
        var facts = Extract(temp.Path);
        Assert.Equal(5, facts.Length);
        var first = Assert.Single(facts, f => f.TargetSymbol == "~/bundles/first");
        var second = Assert.Single(facts, f => f.TargetSymbol == "~/bundles/second");
        Assert.Equal(5, first.Evidence.StartLine);
        foreach (var (target, owner, ordinal, line) in new[] {
            ("~/Scripts/a.js", first, "0", 7), ("~/Scripts/b.js", second, "0", 8), ("~/Scripts/c.js", first, "1", 9) })
        {
            var member = Assert.Single(facts, f => f.TargetSymbol == target);
            Assert.Equal(owner.FactId, member.Properties["bundleDeclarationFactId"]);
            Assert.Equal(ordinal, member.Properties["declaredOrdinal"]);
            Assert.Equal("Global.asax", member.Evidence.FilePath);
            Assert.Equal(line, member.Evidence.StartLine);
        }
    }

    [Theory]
    [InlineData("first = second")]
    [InlineData("Dim aliasValue = first")]
    [InlineData("Dim FIRST As Object")]
    [InlineData("If enabled Then first.Include(\"~/Scripts/conditional.js\")")]
    public void Unsafe_local_uses_refuse_member_attribution(string extra)
    {
        using var temp = new TempDirectory();
        Write(temp.Path, "Global.asax", ApplicationStart.Replace("End Sub", extra + "\nEnd Sub"));
        var facts = Extract(temp.Path);
        Assert.Contains(facts, f => f.FactType == FactTypes.AnalysisGap);
        Assert.DoesNotContain(facts, f => f.TargetSymbol is "~/Scripts/a.js" or "~/Scripts/c.js");
    }

    [Theory]
    [InlineData("runat=\"server\"", "type=\"text/javascript\"", "UnsupportedApplicationBundleSource")]
    [InlineData("</script>", "", "UnsupportedApplicationBundleSource")]
    [InlineData("<script ", "<script-custom ", "UnsupportedApplicationBundleSource")]
    [InlineData("Application Language", "Application-custom Language", "UnsupportedApplicationBundleSource")]
    [InlineData("runat=\"server\"", "runat=\"client\" runat=\"server\"", "UnsupportedApplicationBundleSource")]
    [InlineData("runat=\"server\"", "invalid runat=\"server\"", "UnsupportedApplicationBundleSource")]
    [InlineData("Language=\"VB\"", "Language=\"C#\"", "UnsupportedApplicationBundleSource")]
    [InlineData("End Sub", "End If", "MalformedBundleSource")]
    public void Unsupported_application_markup_or_malformed_body_emits_gap(string from, string to, string kind)
    {
        using var temp = new TempDirectory();
        Write(temp.Path, "Global.asax", ApplicationStart.Replace(from, to));
        Assert.Equal(kind, Assert.Single(Extract(temp.Path)).Properties["gapKind"]);
    }

    [Fact]
    public void Commented_out_application_script_contributes_no_bundle_candidates()
    {
        using var temp = new TempDirectory();
        Write(temp.Path, "Global.asax", "<%--" + ApplicationStart + "--%>");
        Assert.Empty(Extract(temp.Path));
    }

    [Fact]
    public void Sequential_member_limit_refuses_only_the_oversized_local()
    {
        using var temp = new TempDirectory();
        var includes = string.Join("\n", Enumerable.Repeat("first.Include(\"~/Scripts/file.js\")", 129));
        Write(temp.Path, "Global.asax", ApplicationStart.Replace("FIRST.Include(\"~/Scripts/c.js\")", includes));
        var facts = Extract(temp.Path);
        Assert.Contains(facts, f => f.Properties.GetValueOrDefault("gapKind") == "BundleMemberLimit");
        Assert.Equal("~/Scripts/b.js", Assert.Single(facts, f => f.FactType == FactTypes.WebFormsScriptBundleMemberCandidate).TargetSymbol);
    }

    private static string Config(bool vb, string members) => vb
        ? $"Imports System.Web.Optimization\nModule BundleConfig\nSub RegisterBundles(bundles As BundleCollection)\nbundles.Add(New ScriptBundle(\"~/bundles/app\").Include({members}))\nEnd Sub\nEnd Module"
        : $"using System.Web.Optimization;\nclass BundleConfig {{ void RegisterBundles(BundleCollection bundles) {{ bundles.Add(new ScriptBundle(\"~/bundles/app\").Include({members})); }} }}";

    private static CodeFact[] Extract(string repo, IReadOnlyList<FileInventoryItem>? inventory = null) =>
        LegacyWebFormsExtractor.Extract(repo, Manifest, inventory ?? FileInventory.Collect(repo), [])
            .Where(fact => fact.RuleId == RuleIds.LegacyWebFormsScriptBundles).OrderBy(fact => fact.FactId, StringComparer.Ordinal).ToArray();

    private static void Write(string root, string path, string text)
    {
        var fullPath = Path.Combine(root, path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, text);
    }
}
