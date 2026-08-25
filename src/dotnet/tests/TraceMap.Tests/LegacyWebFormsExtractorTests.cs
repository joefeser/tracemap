using System.Collections;
using System.Text;
using System.Text.Json;
using TraceMap.Core;
using TraceMap.Reporting;

namespace TraceMap.Tests;

public sealed class LegacyWebFormsExtractorTests
{
    [Fact]
    public void Scan_resolves_config_namespace_assembly_registration_only_to_one_scoped_syntax_type()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(Path.Combine(repo, "Web", "Controls"));
        File.WriteAllText(Path.Combine(repo, "Web", "Widgets.csproj"), """
            <Project ToolsVersion="12.0">
              <PropertyGroup><AssemblyName>Sample.Widgets</AssemblyName></PropertyGroup>
              <ItemGroup><Compile Include="Controls\\Calendar.cs" /></ItemGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(repo, "Web", "Controls", "Calendar.cs"), """
            namespace Sample.Controls;
            public sealed class Calendar { }
            """);
        File.WriteAllText(Path.Combine(repo, "Web", "web.config"), """
            <configuration><system.web><pages><controls>
              <add tagPrefix="widgets" namespace="Sample.Controls" assembly="Sample.Widgets" />
            </controls></pages></system.web></configuration>
            """);
        File.WriteAllText(Path.Combine(repo, "Web", "Default.aspx"), """
            <%@ Page Language="C#" Inherits="Sample.Default" %>
            <widgets:Calendar runat="server" ID="Calendar" />
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));

        var typeFact = Assert.Single(result.Facts, fact =>
            fact.FactType == FactTypes.TypeDeclared
            && fact.Evidence.FilePath == "Web/Controls/Calendar.cs"
            && fact.Properties.GetValueOrDefault("qualifiedName") == "Sample.Controls.Calendar");
        var registration = Assert.Single(result.Facts, fact =>
            fact.FactType == FactTypes.WebFormsUserControlRegistered
            && fact.Properties.GetValueOrDefault("registrationShape") == "assembly-namespace");
        Assert.Equal("Sample.Controls", registration.Properties.GetValueOrDefault("namespaceName"));
        Assert.Equal("Sample.Widgets", registration.Properties.GetValueOrDefault("assemblyName"));
        var control = Assert.Single(result.Facts, fact =>
            fact.FactType == FactTypes.WebFormsControlDeclared
            && fact.Properties.GetValueOrDefault("controlId") == "Calendar");
        Assert.Equal("Sample.Controls.Calendar", control.Properties.GetValueOrDefault("registeredTargetSymbol"));
        var supportingTypeFactId = control.Properties.GetValueOrDefault("registrationTypeFactId");
        var supportingTypeFact = Assert.Single(result.Facts, fact => fact.FactId == supportingTypeFactId);
        Assert.Equal("Sample.Controls.Calendar", supportingTypeFact.Properties.GetValueOrDefault("qualifiedName")
            ?? $"{supportingTypeFact.Properties.GetValueOrDefault("namespace")}.{supportingTypeFact.Properties.GetValueOrDefault("name")}");
        var composition = Assert.Single(result.Facts, fact =>
            fact.FactType == FactTypes.WebFormsCompositionDeclared
            && fact.Properties.GetValueOrDefault("relationshipKind") == "UsesRegisteredAssemblyControl");
        Assert.Equal("Sample.Controls.Calendar", composition.TargetSymbol);
        Assert.Contains(supportingTypeFact.FactId, composition.Properties.GetValueOrDefault("supportingFactIds"), StringComparison.Ordinal);
        Assert.DoesNotContain(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.Properties.GetValueOrDefault("gapKind") is "UnresolvedWebFormsControlRegistration" or "UnresolvedWebFormsAssemblyControlRegistration");
    }

    [Fact]
    public void Scan_fails_closed_when_namespace_assembly_registration_matches_multiple_scoped_types()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(Path.Combine(repo, "Web", "Controls"));
        File.WriteAllText(Path.Combine(repo, "Web", "Widgets.csproj"), """
            <Project ToolsVersion="12.0">
              <PropertyGroup><AssemblyName>Sample.Widgets</AssemblyName></PropertyGroup>
              <ItemGroup>
                <Compile Include="Controls\\Calendar.One.cs" />
                <Compile Include="Controls\\Calendar.Two.cs" />
              </ItemGroup>
            </Project>
            """);
        foreach (var name in new[] { "Calendar.One.cs", "Calendar.Two.cs" })
        {
            File.WriteAllText(Path.Combine(repo, "Web", "Controls", name), "namespace Sample.Controls; public sealed class Calendar { }");
        }
        File.WriteAllText(Path.Combine(repo, "Web", "web.config"), """
            <configuration><system.web><pages><controls>
              <add tagPrefix="widgets" namespace="Sample.Controls" assembly="Sample.Widgets" />
            </controls></pages></system.web></configuration>
            """);
        File.WriteAllText(Path.Combine(repo, "Web", "Default.aspx"), """
            <%@ Page Language="C#" Inherits="Sample.Default" %>
            <widgets:Calendar runat="server" ID="Calendar" />
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));

        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.Properties.GetValueOrDefault("gapKind") == "AmbiguousWebFormsAssemblyControlRegistration");
        Assert.DoesNotContain(result.Facts, fact =>
            fact.FactType == FactTypes.WebFormsCompositionDeclared
            && fact.Properties.GetValueOrDefault("relationshipKind") == "UsesRegisteredAssemblyControl");
    }

    [Fact]
    public void Scan_does_not_assign_sdk_excluded_source_to_assembly_registration()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(Path.Combine(repo, "Web", "Controls"));
        File.WriteAllText(Path.Combine(repo, "Web", "Widgets.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <AssemblyName>Sample.Widgets</AssemblyName>
                <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(repo, "Web", "Controls", "Calendar.cs"), "namespace Sample.Controls; public sealed class Calendar { }");
        File.WriteAllText(Path.Combine(repo, "Web", "web.config"), """
            <configuration><system.web><pages><controls>
              <add tagPrefix="widgets" namespace="Sample.Controls" assembly="Sample.Widgets" />
            </controls></pages></system.web></configuration>
            """);
        File.WriteAllText(Path.Combine(repo, "Web", "Default.aspx"), """
            <%@ Page Language="C#" Inherits="Sample.Default" %>
            <widgets:Calendar runat="server" ID="Calendar" />
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));

        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.Properties.GetValueOrDefault("gapKind") == "WebFormsAssemblyTypeUnavailable");
        Assert.DoesNotContain(result.Facts, fact =>
            fact.FactType == FactTypes.WebFormsCompositionDeclared
            && fact.Properties.GetValueOrDefault("relationshipKind") == "UsesRegisteredAssemblyControl");
    }

    [Fact]
    public void Scan_does_not_assign_conditioned_compile_source_to_assembly_registration()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(Path.Combine(repo, "Web", "Controls"));
        File.WriteAllText(Path.Combine(repo, "Web", "Widgets.csproj"), """
            <Project ToolsVersion="12.0">
              <PropertyGroup><AssemblyName>Sample.Widgets</AssemblyName></PropertyGroup>
              <ItemGroup Condition="'$(Configuration)' == 'Release'">
                <Compile Include="Controls\Calendar.cs" />
              </ItemGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(repo, "Web", "Controls", "Calendar.cs"), "namespace Sample.Controls; public sealed class Calendar { }");
        File.WriteAllText(Path.Combine(repo, "Web", "web.config"), """
            <configuration><system.web><pages><controls>
              <add tagPrefix="widgets" namespace="Sample.Controls" assembly="Sample.Widgets" />
            </controls></pages></system.web></configuration>
            """);
        File.WriteAllText(Path.Combine(repo, "Web", "Default.aspx"), "<%@ Page Language=\"C#\" Inherits=\"Sample.Default\" %><widgets:Calendar runat=\"server\" ID=\"Calendar\" />");

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));

        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyWebFormsComposition
            && fact.Properties.GetValueOrDefault("gapKind") == "WebFormsAssemblyTypeUnavailable");
        Assert.DoesNotContain(result.Facts, fact =>
            fact.FactType == FactTypes.WebFormsCompositionDeclared
            && fact.Properties.GetValueOrDefault("relationshipKind") == "UsesRegisteredAssemblyControl");
    }

    [Fact]
    public void Scan_applies_host_path_casing_to_explicit_compile_ownership()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(Path.Combine(repo, "Web", "Controls"));
        File.WriteAllText(Path.Combine(repo, "Web", "Widgets.csproj"), """
            <Project ToolsVersion="12.0">
              <PropertyGroup><AssemblyName>Sample.Widgets</AssemblyName></PropertyGroup>
              <ItemGroup><Compile Include="controls\\Calendar.cs" /></ItemGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(repo, "Web", "Controls", "Calendar.cs"), "namespace Sample.Controls; public sealed class Calendar { }");
        File.WriteAllText(Path.Combine(repo, "Web", "web.config"), """
            <configuration><system.web><pages><controls>
              <add tagPrefix="widgets" namespace="Sample.Controls" assembly="Sample.Widgets" />
            </controls></pages></system.web></configuration>
            """);
        File.WriteAllText(Path.Combine(repo, "Web", "Default.aspx"), "<%@ Page Language=\"C#\" Inherits=\"Sample.Default\" %><widgets:Calendar runat=\"server\" ID=\"Calendar\" />");

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));
        var compositionResolved = result.Facts.Any(fact =>
            fact.FactType == FactTypes.WebFormsCompositionDeclared
            && fact.Properties.GetValueOrDefault("relationshipKind") == "UsesRegisteredAssemblyControl");
        var hostIsCaseInsensitive = CSharpSemanticExtractor.CreateSourcePathComparer(repo) == StringComparer.OrdinalIgnoreCase;

        Assert.Equal(hostIsCaseInsensitive, compositionResolved);
        Assert.Equal(!hostIsCaseInsensitive, result.Facts.Any(fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.Properties.GetValueOrDefault("gapKind") == "WebFormsAssemblyTypeUnavailable"));
    }

    [Fact]
    public void Scan_distinguishes_out_of_scope_assembly_from_missing_local_type()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(Path.Combine(repo, "Web"));
        File.WriteAllText(Path.Combine(repo, "Web", "Widgets.csproj"), """
            <Project ToolsVersion="12.0">
              <PropertyGroup><AssemblyName>Sample.Widgets</AssemblyName></PropertyGroup>
              <ItemGroup><Compile Include="Other.cs" /></ItemGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(repo, "Web", "Other.cs"), "namespace Sample.Controls; public class Other { }");
        File.WriteAllText(Path.Combine(repo, "Web", "web.config"), """
            <configuration><system.web><pages><controls>
              <add tagPrefix="local" namespace="Sample.Controls" assembly="Sample.Widgets" />
              <add tagPrefix="external" namespace="External.Controls" assembly="External.Widgets" />
            </controls></pages></system.web></configuration>
            """);
        File.WriteAllText(Path.Combine(repo, "Web", "Default.aspx"), """
            <%@ Page Language="C#" Inherits="Sample.Default" %>
            <local:Missing runat="server" ID="Local" />
            <external:Calendar runat="server" ID="External" />
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));

        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyWebFormsComposition
            && fact.Properties.GetValueOrDefault("gapKind") == "WebFormsAssemblyTypeUnavailable");
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyWebFormsComposition
            && fact.Properties.GetValueOrDefault("gapKind") == "WebFormsAssemblyProjectUnavailable");
        Assert.DoesNotContain(result.Facts, fact =>
            fact.FactType == FactTypes.WebFormsCompositionDeclared
            && fact.Properties.GetValueOrDefault("relationshipKind") == "UsesRegisteredAssemblyControl");
    }

    [Fact]
    public void Scan_inventories_master_user_control_and_bounded_control_metadata_deterministically()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(Path.Combine(repo, "Controls"));
        File.WriteAllText(Path.Combine(repo, "Site.Master"), """
            <%@ Master Language="C#" Inherits="Sample.Site" %>
            <asp:ContentPlaceHolder runat="server" ID="MainContent" />
            """);
        File.WriteAllText(Path.Combine(repo, "Controls", "Editor.ascx"), """
            <%@ Control Language="C#" Inherits="Sample.Editor" %>
            <asp:TextBox runat="server" ID="Value" />
            """);
        File.WriteAllText(Path.Combine(repo, "Edit.aspx"), """
            <%@ Page Language="C#" Inherits="Sample.Edit" MasterPageFile="~/Site.Master" Title="Edit private record $5" %>
            <%@ Register Src="~/Controls/Editor.ascx" TagPrefix="uc" TagName="Editor" %>
            <asp:Content runat="server" ID="Body" ContentPlaceHolderID="MainContent">
              <uc:Editor runat="server" ID="RecordEditor" />
              <asp:RequiredFieldValidator runat="server" ID="Required" />
              <asp:SqlDataSource runat="server" ID="Data" />
              <asp:LinkButton runat="server" ID="Edit" CommandName="Edit" />
            </asp:Content>
            """);

        var first = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out-a")));
        var second = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out-b")));

        var page = Assert.Single(first.Facts, fact =>
            fact.FactType == FactTypes.WebFormsPageDeclared
            && fact.Evidence.FilePath == "Edit.aspx");
        Assert.Equal(RuleIds.LegacyWebFormsInventory, page.RuleId);
        Assert.Equal(EvidenceTiers.Tier2Structural, page.EvidenceTier);
        Assert.Equal(first.Manifest.CommitSha, page.CommitSha);
        Assert.Equal(ScannerVersions.LegacyWebFormsExtractor, page.Evidence.ExtractorVersion);
        Assert.StartsWith("webforms-surface:", page.Properties.GetValueOrDefault("surfaceIdentity"), StringComparison.Ordinal);
        Assert.Equal("bounded-static-webforms-inventory", page.Properties.GetValueOrDefault("coverageLabel"));
        Assert.Equal("True", page.Properties.GetValueOrDefault("titlePresent"));
        Assert.Equal(32, page.Properties.GetValueOrDefault("titleHash")?.Length);

        var registration = Assert.Single(first.Facts, fact => fact.FactType == FactTypes.WebFormsUserControlRegistered);
        Assert.Equal("Controls/Editor.ascx", registration.Properties.GetValueOrDefault("sourcePath"));
        Assert.Equal(2, registration.Evidence.StartLine);
        Assert.False(string.IsNullOrWhiteSpace(registration.Evidence.SnippetHash));

        Assert.Contains(first.Facts, fact =>
            fact.FactType == FactTypes.WebFormsControlDeclared
            && fact.Properties.GetValueOrDefault("controlCategory") == "RegisteredUserControl"
            && fact.Properties.GetValueOrDefault("registeredSourcePath") == "Controls/Editor.ascx");
        Assert.Contains(first.Facts, fact =>
            fact.FactType == FactTypes.WebFormsControlDeclared
            && fact.Properties.GetValueOrDefault("controlCategory") == "MasterContent"
            && fact.Properties.GetValueOrDefault("contentPlaceHolderId") == "MainContent");
        Assert.Contains(first.Facts, fact =>
            fact.FactType == FactTypes.WebFormsControlDeclared
            && fact.Properties.GetValueOrDefault("controlCategory") == "Validator");
        Assert.Contains(first.Facts, fact =>
            fact.FactType == FactTypes.WebFormsControlDeclared
            && fact.Properties.GetValueOrDefault("controlCategory") == "DataSource");
        Assert.Contains(first.Facts, fact =>
            fact.FactType == FactTypes.WebFormsControlDeclared
            && fact.Properties.GetValueOrDefault("commandName") == "Edit");

        var compositions = first.Facts.Where(fact => fact.FactType == FactTypes.WebFormsCompositionDeclared).ToArray();
        Assert.Contains(compositions, fact => fact.Properties.GetValueOrDefault("relationshipKind") == "UsesMasterPage");
        Assert.Contains(compositions, fact => fact.Properties.GetValueOrDefault("relationshipKind") == "UsesRegisteredUserControl");
        Assert.Contains(compositions, fact => fact.Properties.GetValueOrDefault("relationshipKind") == "FillsMasterPlaceholder");
        Assert.All(compositions, fact =>
        {
            Assert.Equal(RuleIds.LegacyWebFormsComposition, fact.RuleId);
            Assert.Equal("bounded-static-webforms-composition", fact.Properties.GetValueOrDefault("coverageLabel"));
            Assert.False(string.IsNullOrWhiteSpace(fact.Properties.GetValueOrDefault("supportingFactIds")));
        });

        var firstIds = first.Facts
            .Where(fact => fact.FactType.StartsWith("WebForms", StringComparison.Ordinal))
            .Select(fact => fact.FactId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var secondIds = second.Facts
            .Where(fact => fact.FactType.StartsWith("WebForms", StringComparison.Ordinal))
            .Select(fact => fact.FactId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(firstIds, secondIds);

        var serialized = SerializeFacts(first.Facts.Where(fact => fact.FactType.StartsWith("WebForms", StringComparison.Ordinal)));
        Assert.DoesNotContain("Edit private record", serialized);

        var report = MarkdownReportWriter.Build(first);
        Assert.Contains("user-control registration", report);
        Assert.Contains("composition `UsesMasterPage`", report);
        Assert.DoesNotContain("Edit private record", report);
    }

    [Fact]
    public void Scan_scopes_designer_evidence_to_the_markup_surface_when_type_and_control_names_collide()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(Path.Combine(repo, "AreaA"));
        Directory.CreateDirectory(Path.Combine(repo, "AreaB"));
        foreach (var area in new[] { "AreaA", "AreaB" })
        {
            File.WriteAllText(Path.Combine(repo, area, "Default.aspx"), """
                <%@ Page Language="C#" Inherits="Sample.Default" %>
                <asp:Button runat="server" ID="SaveButton" />
                """);
        }
        File.WriteAllText(Path.Combine(repo, "AreaA", "Default.aspx.designer.cs"), """
            namespace Sample;
            public partial class Default
            {
                protected global::System.Web.UI.WebControls.Button SaveButton;
            }
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));

        var controls = result.Facts
            .Where(fact => fact.FactType == FactTypes.WebFormsControlDeclared && fact.Properties.GetValueOrDefault("controlId") == "SaveButton")
            .OrderBy(fact => fact.Evidence.FilePath, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(2, controls.Length);
        Assert.False(string.IsNullOrWhiteSpace(controls[0].Properties.GetValueOrDefault("designerFactId")));
        Assert.Null(controls[1].Properties.GetValueOrDefault("designerFactId"));
        Assert.NotEqual(controls[0].Properties.GetValueOrDefault("surfaceIdentity"), controls[1].Properties.GetValueOrDefault("surfaceIdentity"));
        Assert.NotEqual(controls[0].TargetSymbol, controls[1].TargetSymbol);
    }

    [Fact]
    public void Scan_emits_inventory_and_composition_gaps_for_missing_or_unsupported_static_targets()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Incomplete.aspx"), """
            <%@ Page Language="C#" CodeBehind="Missing.aspx.cs" MasterPageFile="~/Missing.Master" Title="$(PrivateTitle)" %>
            <%@ Register Namespace="Dynamic.Controls" Assembly="Dynamic" TagPrefix="dynamic" %>
            <asp:Content runat="server" ID="Body" />
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));
        var gaps = result.Facts.Where(fact => fact.FactType == FactTypes.AnalysisGap).ToArray();

        Assert.Contains(gaps, fact => fact.RuleId == RuleIds.LegacyWebFormsInventory && fact.Properties.GetValueOrDefault("gapKind") == "UnresolvedWebFormsPageType");
        Assert.Contains(gaps, fact => fact.RuleId == RuleIds.LegacyWebFormsInventory && fact.Properties.GetValueOrDefault("gapKind") == "MissingWebFormsCodeBehind");
        Assert.Contains(gaps, fact => fact.RuleId == RuleIds.LegacyWebFormsInventory && fact.Properties.GetValueOrDefault("gapKind") == "UnsupportedWebFormsTitle");
        Assert.Contains(gaps, fact => fact.RuleId == RuleIds.LegacyWebFormsComposition && fact.Properties.GetValueOrDefault("gapKind") == "MissingWebFormsMasterPage");
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.WebFormsUserControlRegistered
            && fact.Properties.GetValueOrDefault("registrationShape") == "assembly-namespace"
            && fact.Properties.GetValueOrDefault("namespaceName") == "Dynamic.Controls"
            && fact.Properties.GetValueOrDefault("assemblyName") == "Dynamic");
        Assert.Contains(gaps, fact => fact.RuleId == RuleIds.LegacyWebFormsComposition && fact.Properties.GetValueOrDefault("gapKind") == "UnresolvedWebFormsContentPlaceholder");
        Assert.All(gaps.Where(fact => fact.RuleId.StartsWith("legacy.webforms", StringComparison.Ordinal)), fact =>
        {
            Assert.Equal(EvidenceTiers.Tier4Unknown, fact.EvidenceTier);
            Assert.Equal(ScannerVersions.LegacyWebFormsExtractor, fact.Evidence.ExtractorVersion);
            Assert.Equal("reduced-static-webforms-evidence", fact.Properties.GetValueOrDefault("coverageLabel"));
            Assert.False(string.IsNullOrWhiteSpace(fact.Properties.GetValueOrDefault("ruleLimitations")));
        });
        var page = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.WebFormsPageDeclared);
        Assert.Equal("True", page.Properties.GetValueOrDefault("titlePresent"));
        Assert.Null(page.Properties.GetValueOrDefault("titleHash"));
    }

    [Fact]
    public void Scan_uses_inventory_casing_and_rejects_excluded_composition_targets()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(Path.Combine(repo, "Excluded"));
        File.WriteAllText(Path.Combine(repo, "Site.Master"), "<%@ Master Language=\"C#\" Inherits=\"Sample.Site\" %>");
        File.WriteAllText(Path.Combine(repo, "Excluded", "Widget.ascx"), "<%@ Control Language=\"C#\" Inherits=\"Sample.Widget\" %>");
        File.WriteAllText(Path.Combine(repo, "Default.aspx"), """
            <%@ Page Language="C#" Inherits="Sample.Default" MasterPageFile="~/site.master" %>
            <%@ Register Src="~/Excluded/Widget.ascx" TagPrefix="uc" TagName="Widget" %>
            <uc:Widget runat="server" ID="Widget" />
            """);

        var result = ScanEngine.Scan(new ScanOptions(
            repo,
            Path.Combine(temp.Path, "out"),
            ExcludeGlobs: ["Excluded/**"]));

        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.WebFormsPageDeclared
            && fact.Properties.GetValueOrDefault("masterPageFile") == "Site.Master");
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.WebFormsCompositionDeclared
            && fact.Properties.GetValueOrDefault("relationshipKind") == "UsesMasterPage"
            && fact.Properties.GetValueOrDefault("targetFilePath") == "Site.Master");
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.Properties.GetValueOrDefault("gapKind") == "MissingWebFormsUserControl");
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.WebFormsUserControlRegistered
            && fact.EvidenceTier == EvidenceTiers.Tier4Unknown
            && fact.Properties.GetValueOrDefault("declaredSourcePath") == "Excluded/Widget.ascx");
        Assert.DoesNotContain(result.Facts, fact =>
            fact.FactType == FactTypes.WebFormsCompositionDeclared
            && fact.Properties.GetValueOrDefault("relationshipKind") == "UsesRegisteredUserControl");
    }

    [Fact]
    public void Scan_resolves_app_relative_composition_from_the_owning_web_project_root()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(Path.Combine(repo, "Web", "Pages"));
        Directory.CreateDirectory(Path.Combine(repo, "Web", "Controls"));
        File.WriteAllText(Path.Combine(repo, "Web", "Web.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        File.WriteAllText(Path.Combine(repo, "Web", "Site.Master"), "<%@ Master Language=\"C#\" Inherits=\"Sample.Site\" %>");
        File.WriteAllText(Path.Combine(repo, "Web", "Pages", "Web.config"), "<configuration />");
        File.WriteAllText(Path.Combine(repo, "Web", "Controls", "Editor.ascx"), "<%@ Control Language=\"C#\" Inherits=\"Sample.Editor\" %>");
        File.WriteAllText(Path.Combine(repo, "Web", "Pages", "Edit.aspx"), """
            <%@ Page Language="C#" Inherits="Sample.Edit" MasterPageFile="~/Site.Master" %>
            <%@ Register Src="~/Controls/Editor.ascx" TagPrefix="uc" TagName="Editor" %>
            <uc:Editor runat="server" ID="Editor" />
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));

        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.WebFormsCompositionDeclared
            && fact.Properties.GetValueOrDefault("relationshipKind") == "UsesMasterPage"
            && fact.Properties.GetValueOrDefault("targetFilePath") == "Web/Site.Master");
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.WebFormsCompositionDeclared
            && fact.Properties.GetValueOrDefault("relationshipKind") == "UsesRegisteredUserControl"
            && fact.Properties.GetValueOrDefault("targetFilePath") == "Web/Controls/Editor.ascx");
    }

    [Fact]
    public void Scan_rejects_non_master_targets_for_master_page_composition()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Other.aspx"), "<%@ Page Language=\"C#\" Inherits=\"Sample.Other\" %>");
        File.WriteAllText(Path.Combine(repo, "Default.aspx"), "<%@ Page Language=\"C#\" Inherits=\"Sample.Default\" MasterPageFile=\"~/Other.aspx\" %>");

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));

        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyWebFormsComposition
            && fact.Properties.GetValueOrDefault("gapKind") == "UnsupportedWebFormsMasterPageTarget");
        Assert.DoesNotContain(result.Facts, fact =>
            fact.FactType == FactTypes.WebFormsCompositionDeclared
            && fact.Properties.GetValueOrDefault("relationshipKind") == "UsesMasterPage");
    }

    [Fact]
    public void Scan_does_not_resolve_a_missing_linked_handler_from_an_unrelated_codebehind_file()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(Path.Combine(repo, "Other"));
        File.WriteAllText(Path.Combine(repo, "Missing.aspx"), """
            <%@ Page Language="C#" CodeBehind="Missing.aspx.cs" Inherits="Sample.Shared" %>
            <asp:Button runat="server" ID="Save" OnClick="Save_Click" />
            """);
        File.WriteAllText(Path.Combine(repo, "Other", "Other.aspx.cs"), """
            using System;
            namespace Sample;
            public partial class Shared
            {
                protected void Save_Click(object sender, EventArgs e) { }
            }
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));

        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.AnalysisGap && fact.Properties.GetValueOrDefault("gapKind") == "MissingWebFormsCodeBehind");
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.AnalysisGap && fact.Properties.GetValueOrDefault("gapKind") == "UnprovenCrossFileWebFormsHandler");
        Assert.DoesNotContain(result.Facts, fact => fact.FactType == FactTypes.WebFormsHandlerResolved);
    }

    [Fact]
    public void Scan_canonicalizes_designer_markup_path_casing_through_inventory()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Default.aspx"), """
            <%@ Page Language="C#" Inherits="Sample.Default" %>
            <asp:Button runat="server" ID="Save" />
            """);
        File.WriteAllText(Path.Combine(repo, "default.aspx.designer.cs"), """
            namespace Sample;
            public partial class Default
            {
                protected global::System.Web.UI.WebControls.Button Save;
            }
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));
        var control = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.WebFormsControlDeclared);

        Assert.False(string.IsNullOrWhiteSpace(control.Properties.GetValueOrDefault("designerFactId")));
    }

    [Fact]
    public void Scan_ignores_server_comments_and_fails_closed_on_ambiguous_registration_tags()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(Path.Combine(repo, "Controls"));
        foreach (var name in new[] { "First", "Second", "Commented" })
        {
            File.WriteAllText(Path.Combine(repo, "Controls", $"{name}.ascx"), $"<%@ Control Language=\"C#\" Inherits=\"Sample.{name}\" %>");
        }
        File.WriteAllText(Path.Combine(repo, "Default.aspx"), """
            <%@ Page Language="C#" Inherits="Sample.Default" %>
            <%-- <%@ Register Src="~/Controls/Commented.ascx" TagPrefix="uc" TagName="Commented" %>
                 <uc:Commented runat="server" ID="Commented" /> --%>
            <%@ Register Src="~/Controls/First.ascx" TagPrefix="uc" TagName="Widget" %>
            <%@ Register Src="~/Controls/Second.ascx" TagPrefix="uc" TagName="Widget" %>
            <uc:Widget runat="server" ID="Widget" />
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));

        Assert.DoesNotContain(result.Facts, fact =>
            fact.Evidence.FilePath == "Default.aspx"
            && fact.FactType.StartsWith("WebForms", StringComparison.Ordinal)
            && (fact.Properties.Values.Any(value => value.Contains("Commented", StringComparison.Ordinal))
                || fact.ContractElement?.Contains("Commented", StringComparison.Ordinal) == true));
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyWebFormsComposition
            && fact.Properties.GetValueOrDefault("gapKind") == "AmbiguousWebFormsUserControlRegistration");
        Assert.DoesNotContain(result.Facts, fact =>
            fact.FactType == FactTypes.WebFormsCompositionDeclared
            && fact.Properties.GetValueOrDefault("relationshipKind") == "UsesRegisteredUserControl");
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.WebFormsControlDeclared
            && fact.Properties.GetValueOrDefault("controlId") == "Widget"
            && fact.Properties.GetValueOrDefault("controlCategory") == "ServerControl");
    }

    [Fact]
    public void Scan_fails_closed_when_one_competing_user_control_registration_is_missing()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(Path.Combine(repo, "Controls"));
        File.WriteAllText(Path.Combine(repo, "Controls", "Present.ascx"), "<%@ Control Language=\"C#\" Inherits=\"Sample.Present\" %>");
        File.WriteAllText(Path.Combine(repo, "Default.aspx"), """
            <%@ Page Language="C#" Inherits="Sample.Default" %>
            <%@ Register Src="~/Controls/Present.ascx" TagPrefix="uc" TagName="Widget" %>
            <%@ Register Src="~/Controls/Missing.ascx" TagPrefix="uc" TagName="Widget" %>
            <uc:Widget runat="server" ID="Widget" />
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));

        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyWebFormsComposition
            && fact.Properties.GetValueOrDefault("gapKind") == "AmbiguousWebFormsUserControlRegistration");
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.Properties.GetValueOrDefault("gapKind") == "MissingWebFormsUserControl");
        Assert.DoesNotContain(result.Facts, fact =>
            fact.FactType == FactTypes.WebFormsCompositionDeclared
            && fact.Properties.GetValueOrDefault("relationshipKind") == "UsesRegisteredUserControl");
    }

    [Fact]
    public void Webforms_inventory_and_composition_rules_are_documented()
    {
        var catalog = File.ReadAllText(Path.Combine(FindRepoRoot(), "rules", "rule-catalog.yml"));

        Assert.Contains($"- id: {RuleIds.LegacyWebFormsInventory}", catalog);
        Assert.Contains($"- id: {RuleIds.LegacyWebFormsComposition}", catalog);
        Assert.Contains(FactTypes.WebFormsUserControlRegistered, catalog);
        Assert.Contains(FactTypes.WebFormsCompositionDeclared, catalog);
        Assert.Contains("do not prove runtime loading", catalog);
    }

    [Fact]
    public void Scan_resolves_static_user_control_registration_from_ancestor_web_config()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(Path.Combine(repo, "Pages"));
        Directory.CreateDirectory(Path.Combine(repo, "Controls"));
        File.WriteAllText(Path.Combine(repo, "LegacyWeb.csproj"), "<Project ToolsVersion=\"14.0\" />");
        File.WriteAllText(Path.Combine(repo, "web.config"), """
            <configuration>
              <system.web>
                <pages>
                  <controls>
                    <add tagPrefix="uc" tagName="Widget" src="~/Controls/Widget.ascx" />
                  </controls>
                </pages>
              </system.web>
            </configuration>
            """);
        File.WriteAllText(Path.Combine(repo, "Controls", "Widget.ascx"), "<%@ Control Language=\"C#\" Inherits=\"Sample.Widget\" %>");
        File.WriteAllText(Path.Combine(repo, "Pages", "Default.aspx"), """
            <%@ Page Language="C#" Inherits="Sample.Default" %>
            <uc:Widget runat="server" ID="Widget" />
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));

        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.WebFormsUserControlRegistered
            && fact.Evidence.FilePath == "web.config"
            && fact.Properties.GetValueOrDefault("declarationKind") == "configuration"
            && fact.Properties.GetValueOrDefault("sourcePath") == "Controls/Widget.ascx");
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.WebFormsCompositionDeclared
            && fact.Evidence.FilePath == "Pages/Default.aspx"
            && fact.Properties.GetValueOrDefault("relationshipKind") == "UsesRegisteredUserControl");
        Assert.DoesNotContain(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.Properties.GetValueOrDefault("gapKind") == "UnresolvedWebFormsControlRegistration");
    }

    [Fact]
    public void Scan_applies_location_scoped_config_registration_only_to_matching_markup()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(Path.Combine(repo, "Admin"));
        Directory.CreateDirectory(Path.Combine(repo, "Public"));
        Directory.CreateDirectory(Path.Combine(repo, "Controls"));
        File.WriteAllText(Path.Combine(repo, "LegacyWeb.csproj"), "<Project ToolsVersion=\"14.0\" />");
        File.WriteAllText(Path.Combine(repo, "web.config"), """
            <configuration>
              <location path="Admin">
                <system.web><pages><controls>
                  <add tagPrefix="uc" tagName="Widget" src="~/Controls/Widget.ascx" />
                </controls></pages></system.web>
              </location>
            </configuration>
            """);
        File.WriteAllText(Path.Combine(repo, "Controls", "Widget.ascx"), "<%@ Control Language=\"C#\" Inherits=\"Sample.Widget\" %>");
        var markup = "<%@ Page Language=\"C#\" Inherits=\"Sample.Default\" %><uc:Widget runat=\"server\" ID=\"Widget\" />";
        File.WriteAllText(Path.Combine(repo, "Admin", "Default.aspx"), markup);
        File.WriteAllText(Path.Combine(repo, "Public", "Default.aspx"), markup);

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));

        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.WebFormsCompositionDeclared
            && fact.Evidence.FilePath == "Admin/Default.aspx"
            && fact.Properties.GetValueOrDefault("relationshipKind") == "UsesRegisteredUserControl");
        Assert.DoesNotContain(result.Facts, fact =>
            fact.FactType == FactTypes.WebFormsCompositionDeclared
            && fact.Evidence.FilePath == "Public/Default.aspx"
            && fact.Properties.GetValueOrDefault("relationshipKind") == "UsesRegisteredUserControl");
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.Evidence.FilePath == "Public/Default.aspx"
            && fact.Properties.GetValueOrDefault("gapKind") == "UnresolvedWebFormsControlRegistration");
    }

    [Fact]
    public void Scan_emits_bounded_static_on_event_candidates_but_not_client_side_properties()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Default.aspx"), """
            <%@ Page Language="C#" CodeBehind="Default.aspx.cs" Inherits="Sample.Default" %>
            <asp:GridView runat="server" ID="Grid" OnRowDataBound="Grid_RowDataBound" />
            <asp:Button runat="server" ID="Save" OnClientClick="return false;" />
            """);
        File.WriteAllText(Path.Combine(repo, "Default.aspx.cs"), """
            namespace Sample;
            public partial class Default
            {
                protected void Grid_RowDataBound(object sender, System.EventArgs e) { }
            }
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));

        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.WebFormsEventBindingDeclared
            && fact.EvidenceTier == EvidenceTiers.Tier3SyntaxOrTextual
            && fact.Properties.GetValueOrDefault("eventName") == "OnRowDataBound"
            && fact.Properties.GetValueOrDefault("coverageLabel") == "bounded-static-webforms-event-candidate");
        Assert.DoesNotContain(result.Facts, fact =>
            fact.FactType == FactTypes.WebFormsEventBindingDeclared
            && fact.Properties.GetValueOrDefault("eventName") == "OnClientClick");
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.Properties.GetValueOrDefault("gapKind") == "UnsupportedWebFormsEventAttribute");
        Assert.DoesNotContain(result.Facts, fact =>
            fact.FactType == FactTypes.WebFormsHandlerResolved
            && fact.ContractElement == "Grid_RowDataBound");
    }

    [Fact]
    public void Scan_fails_closed_on_conflicting_inherited_config_registrations()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(Path.Combine(repo, "Pages"));
        Directory.CreateDirectory(Path.Combine(repo, "Controls"));
        File.WriteAllText(Path.Combine(repo, "LegacyWeb.csproj"), "<Project ToolsVersion=\"14.0\" />");
        File.WriteAllText(Path.Combine(repo, "web.config"), "<configuration><system.web><pages><controls><add tagPrefix=\"uc\" tagName=\"Widget\" src=\"~/Controls/First.ascx\" /></controls></pages></system.web></configuration>");
        File.WriteAllText(Path.Combine(repo, "Pages", "web.config"), "<configuration><system.web><pages><controls><add tagPrefix=\"uc\" tagName=\"Widget\" src=\"~/Controls/Second.ascx\" /></controls></pages></system.web></configuration>");
        File.WriteAllText(Path.Combine(repo, "Controls", "First.ascx"), "<%@ Control Language=\"C#\" Inherits=\"Sample.First\" %>");
        File.WriteAllText(Path.Combine(repo, "Controls", "Second.ascx"), "<%@ Control Language=\"C#\" Inherits=\"Sample.Second\" %>");
        File.WriteAllText(Path.Combine(repo, "Pages", "Default.aspx"), "<%@ Page Language=\"C#\" Inherits=\"Sample.Default\" %><uc:Widget runat=\"server\" ID=\"Widget\" />");

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));

        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.Properties.GetValueOrDefault("gapKind") == "AmbiguousWebFormsUserControlRegistration");
        Assert.DoesNotContain(result.Facts, fact =>
            fact.FactType == FactTypes.WebFormsCompositionDeclared
            && fact.Properties.GetValueOrDefault("relationshipKind") == "UsesRegisteredUserControl");
    }

    [Theory]
    [InlineData("<clear />")]
    [InlineData("<remove tagPrefix=\"uc\" tagName=\"Widget\" />")]
    public void Scan_honors_child_config_control_registration_removal(string childControlDirective)
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(Path.Combine(repo, "Pages"));
        Directory.CreateDirectory(Path.Combine(repo, "Controls"));
        File.WriteAllText(Path.Combine(repo, "LegacyWeb.csproj"), "<Project ToolsVersion=\"14.0\" />");
        File.WriteAllText(Path.Combine(repo, "web.config"), "<configuration><system.web><pages><controls><add tagPrefix=\"uc\" tagName=\"Widget\" src=\"~/Controls/Widget.ascx\" /></controls></pages></system.web></configuration>");
        File.WriteAllText(Path.Combine(repo, "Pages", "web.config"), $"<configuration><system.web><pages><controls>{childControlDirective}</controls></pages></system.web></configuration>");
        File.WriteAllText(Path.Combine(repo, "Controls", "Widget.ascx"), "<%@ Control Language=\"C#\" Inherits=\"Sample.Widget\" %>");
        File.WriteAllText(Path.Combine(repo, "Pages", "Default.aspx"), "<%@ Page Language=\"C#\" Inherits=\"Sample.Default\" %><uc:Widget runat=\"server\" ID=\"Widget\" />");

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));

        Assert.DoesNotContain(result.Facts, fact =>
            fact.FactType == FactTypes.WebFormsCompositionDeclared
            && fact.Properties.GetValueOrDefault("relationshipKind") == "UsesRegisteredUserControl");
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.Properties.GetValueOrDefault("gapKind") == "UnresolvedWebFormsControlRegistration");
    }

    [Fact]
    public void Scan_extracts_markup_binding_handler_designer_and_report_sections()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        WriteBasicPage(repo, "Save_Click", handlerBody: "Total.Text = \"saved\";");
        File.WriteAllText(Path.Combine(repo, "Default.aspx.designer.cs"), """
            namespace Sample;
            public partial class Default
            {
                protected global::System.Web.UI.WebControls.Button SaveButton;
                protected global::System.Web.UI.WebControls.Label Total;
            }
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));
        var facts = result.Facts;

        Assert.Contains(result.Inventory, item => item is { RelativePath: "Default.aspx", Kind: "WebFormsMarkup" });
        Assert.Contains(result.Inventory, item => item is { RelativePath: "Default.aspx.cs", Kind: "WebFormsCodeBehind" });
        Assert.Contains(result.Inventory, item => item is { RelativePath: "Default.aspx.designer.cs", Kind: "WebFormsDesigner" });
        var binding = Assert.Single(facts, fact => fact.FactType == FactTypes.WebFormsEventBindingDeclared);
        Assert.Equal(RuleIds.LegacyWebFormsEventBinding, binding.RuleId);
        Assert.Equal("SaveButton", binding.Properties.GetValueOrDefault("controlId"));
        Assert.Equal("Save_Click", binding.Properties.GetValueOrDefault("handlerName"));
        Assert.False(string.IsNullOrWhiteSpace(binding.Properties.GetValueOrDefault("designerFactId")));

        Assert.Contains(facts, fact =>
            fact.FactType == FactTypes.WebFormsHandlerResolved
            && fact.RuleId == RuleIds.LegacyWebFormsHandlerResolution
            && fact.EvidenceTier == EvidenceTiers.Tier2Structural
            && fact.Properties.GetValueOrDefault("resolutionKind") == "StructuralLinkedPartialMethod");
        Assert.Contains(facts, fact =>
            fact.FactType == FactTypes.WebFormsLogicSignalDetected
            && fact.Properties.GetValueOrDefault("signalKind") == "UiBoilerplateSignal");

        var report = MarkdownReportWriter.Build(result);
        Assert.Contains("## WebForms Events", report);
        Assert.Contains("## WebForms Event Flow", report);
        Assert.Contains("## WebForms Limitations", report);
    }

    [Fact]
    public void Scan_preserves_duplicate_bindings_with_stable_distinct_fact_ids()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Default.aspx"), """
            <%@ Page Language="C#" CodeBehind="Default.aspx.cs" Inherits="Sample.Default" %>
            <asp:Button runat="server" ID="SaveButton" OnClick="Save_Click" />
            <asp:Button runat="server" ID="SaveButton" OnCommand="Save_Click" />
            """);
        WriteCodeBehind(repo, "Save_Click", "");

        var first = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out-a")));
        var second = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out-b")));

        var firstIds = first.Facts
            .Where(fact => fact.FactType == FactTypes.WebFormsEventBindingDeclared)
            .Select(fact => fact.FactId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var secondIds = second.Facts
            .Where(fact => fact.FactType == FactTypes.WebFormsEventBindingDeclared)
            .Select(fact => fact.FactId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(2, firstIds.Length);
        Assert.Equal(2, firstIds.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(firstIds, secondIds);
    }

    [Fact]
    public void Scan_resolves_windows_style_relative_codebehind_paths()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        Directory.CreateDirectory(Path.Combine(repo, "Controls"));
        File.WriteAllText(Path.Combine(repo, "Nested.aspx"), """
            <%@ Page Language="C#" CodeBehind="Controls\Nested.aspx.cs" Inherits="Sample.Nested" %>
            <asp:Button runat="server" ID="SaveButton" OnClick="Save_Click" />
            """);
        File.WriteAllText(Path.Combine(repo, "Controls", "Nested.aspx.cs"), """
            using System;
            namespace Sample;
            public partial class Nested
            {
                protected void Save_Click(object sender, EventArgs e) { }
            }
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));

        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.WebFormsHandlerResolved
            && fact.ContractElement == "Save_Click"
            && fact.Properties.GetValueOrDefault("linkedCodePath") == "Controls/Nested.aspx.cs");
    }

    [Fact]
    public void Scan_emits_ambiguity_and_auto_wireup_gaps_conservatively()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Default.aspx"), """
            <%@ Page Language="C#" CodeBehind="Default.aspx.cs" Inherits="Sample.Default" AutoEventWireup="false" %>
            <asp:Button runat="server" ID="SaveButton" OnClick="Save_Click" />
            """);
        File.WriteAllText(Path.Combine(repo, "Default.aspx.cs"), """
            using System;
            namespace Sample;
            public partial class Default
            {
                protected void Save_Click(object sender, EventArgs e) { }
                protected void Save_Click(object sender, System.EventArgs e, string extra = "") { }
                protected void Page_Load(object sender, EventArgs e) { }
            }
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));

        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyWebFormsHandlerResolution
            && fact.Properties.GetValueOrDefault("gapKind") == "AmbiguousWebFormsHandler");
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.Properties.GetValueOrDefault("gapKind") == "AutoEventWireupUnavailable");
        Assert.DoesNotContain(result.Facts, fact =>
            fact.FactType == FactTypes.WebFormsHandlerResolved
            && fact.ContractElement == "Page_Load");
    }

    [Fact]
    public void Scan_resolves_explicit_auto_wireup_when_enabled()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Default.aspx"), """
            <%@ Page Language="C#" CodeBehind="Default.aspx.cs" Inherits="Sample.Default" AutoEventWireup="true" %>
            """);
        WriteCodeBehind(repo, "Page_Load", "");

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));

        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.WebFormsHandlerResolved
            && fact.ContractElement == "Page_Load"
            && fact.Properties.GetValueOrDefault("autoEventWireup") == "True");
    }

    [Fact]
    public void Scan_resolves_lifecycle_handler_with_explicit_static_subscription()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Default.aspx"), """
            <%@ Page Language="C#" CodeBehind="Default.aspx.cs" Inherits="Sample.Default" AutoEventWireup="false" %>
            """);
        File.WriteAllText(Path.Combine(repo, "Default.aspx.cs"), """
            using System;
            namespace Sample;
            public partial class Default
            {
                public Default()
                {
                    Load += Page_Load;
                }

                protected void Page_Load(object sender, EventArgs e) { }
            }
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));

        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.WebFormsHandlerResolved
            && fact.ContractElement == "Page_Load"
            && fact.Properties.GetValueOrDefault("explicitEventSubscription") == "True");
    }

    [Fact]
    public void Scan_projects_direct_webforms_handler_flow_to_wcf_and_sql_without_raw_sql()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        Directory.CreateDirectory(Path.Combine(repo, "Service References", "Rating"));
        File.WriteAllText(Path.Combine(repo, "Default.aspx"), """
            <%@ Page Language="C#" CodeBehind="Default.aspx.cs" Inherits="Sample.Default" %>
            <asp:Button runat="server" ID="SaveButton" OnClick="Save_Click" />
            """);
        File.WriteAllText(Path.Combine(repo, "Contracts.cs"), """
            using System.ServiceModel;
            namespace Sample.Contracts;
            [ServiceContract]
            public interface IRatingService
            {
                [OperationContract]
                string Rate(string request);
            }
            """);
        File.WriteAllText(Path.Combine(repo, "RatingClient.cs"), """
            using System.ServiceModel;
            namespace Sample.Contracts;
            public partial class RatingClient : ClientBase<IRatingService>, IRatingService
            {
                public string Rate(string request) => Channel.Rate(request);
            }
            """);
        File.WriteAllText(Path.Combine(repo, "Default.aspx.cs"), """
            using System;
            using Sample.Contracts;
            namespace Sample;
            public partial class Default
            {
                protected void Save_Click(object sender, EventArgs e)
                {
                    var client = new RatingClient();
                    client.Rate("x");
                    var sql = "select Id from Orders";
                }
            }
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));

        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.WcfServiceReferenceMapping);
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.QueryPatternDetected);
        var flow = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.WebFormsEventFlowProjected);
        Assert.Contains(flow.Properties.GetValueOrDefault("flowClassification"), new[] { "ProbableStaticEventFlow", "NeedsReviewEventFlow", "StrongStaticEventFlow" });
        Assert.False(string.IsNullOrWhiteSpace(flow.Properties.GetValueOrDefault("supportingFactIds")));
        Assert.Contains(RuleIds.LegacyWebFormsEventFlow, flow.Properties.GetValueOrDefault("ruleIds"));

        var serializedWebForms = SerializeFacts(result.Facts.Where(fact => fact.FactType.StartsWith("WebForms", StringComparison.Ordinal)));
        Assert.DoesNotContain("select Id from Orders", serializedWebForms);
    }

    [Fact]
    public void Scan_does_not_project_wcf_flow_from_operation_name_collision()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        Directory.CreateDirectory(Path.Combine(repo, "Service References", "Rating"));
        File.WriteAllText(Path.Combine(repo, "Default.aspx"), """
            <%@ Page Language="C#" CodeBehind="Default.aspx.cs" Inherits="Sample.Default" %>
            <asp:Button runat="server" ID="SaveButton" OnClick="Save_Click" />
            """);
        File.WriteAllText(Path.Combine(repo, "Contracts.cs"), """
            using System.ServiceModel;
            namespace Sample.Contracts;
            [ServiceContract]
            public interface IRatingService
            {
                [OperationContract]
                string Rate(string request);
            }
            """);
        File.WriteAllText(Path.Combine(repo, "RatingClient.cs"), """
            using System.ServiceModel;
            namespace Sample.Contracts;
            public partial class RatingClient : ClientBase<IRatingService>, IRatingService
            {
                public string Rate(string request) => Channel.Rate(request);
            }
            """);
        File.WriteAllText(Path.Combine(repo, "Default.aspx.cs"), """
            using System;
            namespace Sample;
            public partial class Default
            {
                protected void Save_Click(object sender, EventArgs e)
                {
                    Rate();
                }

                private void Rate()
                {
                }
            }
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));

        var wcfFactIds = result.Facts
            .Where(fact => fact.FactType == FactTypes.WcfServiceReferenceMapping)
            .Select(fact => fact.FactId)
            .ToArray();
        Assert.NotEmpty(wcfFactIds);
        var flow = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.WebFormsEventFlowProjected);
        Assert.NotEqual("StrongStaticEventFlow", flow.Properties.GetValueOrDefault("flowClassification"));
        Assert.NotEqual("wcf-operation", flow.Properties.GetValueOrDefault("terminalSurfaceKind"));
        var supportingFactIds = flow.Properties.GetValueOrDefault("supportingFactIds") ?? string.Empty;
        Assert.DoesNotContain(wcfFactIds, id => supportingFactIds.Contains(id, StringComparison.Ordinal));
    }

    [Fact]
    public void Scan_reports_no_backend_evidence_under_full_coverage()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "App.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        WriteBasicPage(repo, "Save_Click", handlerBody: "");

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));

        Assert.Equal("Succeeded", result.Manifest.BuildStatus);
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.WebFormsEventFlowProjected
            && fact.Properties.GetValueOrDefault("flowClassification") == "NoBackendEvidence"
            && fact.Properties.GetValueOrDefault("coverage") == "Full");
    }

    [Fact]
    public void Scan_scopes_unqualified_handler_evidence_to_resolved_file()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "App.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        WritePage(repo, "Orders.aspx", "Orders.aspx.cs", "Sample.OrdersPage", "Save_Click");
        WritePage(repo, "Profile.aspx", "Profile.aspx.cs", "Sample.ProfilePage", "Save_Click");
        WritePageCodeBehind(repo, "Orders.aspx.cs", "OrdersPage", "Save_Click", "var sql = \"select Id from Orders\";");
        WritePageCodeBehind(repo, "Profile.aspx.cs", "ProfilePage", "Save_Click", "");

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));

        var profileFlow = Assert.Single(result.Facts, fact =>
            fact.FactType == FactTypes.WebFormsEventFlowProjected
            && fact.Evidence.FilePath == "Profile.aspx.cs");
        Assert.Equal("NoBackendEvidence", profileFlow.Properties.GetValueOrDefault("flowClassification"));
    }

    [Fact]
    public void Scan_links_same_file_syntax_sql_terminal_evidence()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        WriteBasicPage(repo, "Save_Click", handlerBody: "var sql = \"select Id from Orders\";");

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));

        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.QueryPatternDetected);
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.WebFormsEventFlowProjected
            && fact.Properties.GetValueOrDefault("terminalSurfaceKind") == "sql-query"
            && fact.Properties.GetValueOrDefault("flowClassification") == "ProbableStaticEventFlow");
    }

    [Fact]
    public void Scan_does_not_emit_webforms_designer_facts_without_matching_markup()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Settings.designer.cs"), """
            namespace Sample;
            public partial class Settings
            {
                internal string Theme;
            }
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));

        Assert.Contains(result.Inventory, item => item is { RelativePath: "Settings.designer.cs", Kind: "CSharp" });
        Assert.DoesNotContain(result.Inventory, item => item is { RelativePath: "Settings.designer.cs", Kind: "WebFormsDesigner" });
        Assert.DoesNotContain(result.Facts, fact => fact.FactType.StartsWith("WebForms", StringComparison.Ordinal));
    }

    [Fact]
    public void Scan_suppresses_unsafe_markup_values_and_records_malformed_gap()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Unsafe.aspx"), """
            <%@ Page Language="C#" CodeBehind="$(UnsafeCodeBehindPath)" Inherits="Sample.Unsafe" MasterPageFile="$(UnsafeMasterPagePath)" %>
            <asp:Button runat="server" ID="SaveButton" OnClientClick="PrivateHandler" />
            """);
        File.WriteAllText(Path.Combine(repo, "Broken.aspx"), """
            <asp:Button runat="server" ID="SaveButton" OnClick="Save_Click" />
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));
        var serialized = SerializeFacts(result.Facts.Where(fact => fact.FactType.StartsWith("WebForms", StringComparison.Ordinal) || fact.RuleId.StartsWith("legacy.webforms", StringComparison.Ordinal)));

        Assert.DoesNotContain("UnsafeCodeBehindPath", serialized);
        Assert.DoesNotContain("UnsafeMasterPagePath", serialized);
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyWebFormsInventory
            && fact.Properties.GetValueOrDefault("gapKind") == "MalformedWebFormsDirective");
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyWebFormsEventBinding
            && fact.Properties.GetValueOrDefault("gapKind") == "UnsupportedWebFormsEventAttribute");
    }

    [Fact]
    public void Extractor_indexes_existing_evidence_before_resolving_many_handlers()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);

        var markup = new StringBuilder("<%@ Page Language=\"C#\" CodeBehind=\"Default.aspx.cs\" Inherits=\"Sample.Default\" %>");
        var code = new StringBuilder("using System; namespace Sample; public partial class Default {");
        for (var index = 0; index < 200; index++)
        {
            markup.AppendLine($"<asp:Button runat=\"server\" ID=\"Button{index}\" OnClick=\"Handle{index}\" />");
            code.AppendLine($"protected void Handle{index}(object sender, EventArgs e) {{ }}");
        }
        code.AppendLine("}");
        File.WriteAllText(Path.Combine(repo, "Default.aspx"), markup.ToString());
        File.WriteAllText(Path.Combine(repo, "Default.aspx.cs"), code.ToString());

        var manifest = new ScanManifest(
            "scan-webforms-index",
            "synthetic",
            null,
            "main",
            "abc123",
            "test/1.0",
            DateTimeOffset.UnixEpoch,
            "Level1SemanticAnalysis",
            "Succeeded",
            [],
            ["Sample.csproj"],
            [],
            []);
        var unrelatedFacts = Enumerable.Range(0, 2_000)
            .Select(index => FactFactory.Create(
                manifest,
                FactTypes.MethodDeclared,
                RuleIds.CSharpSemanticDeclarations,
                EvidenceTiers.Tier1Semantic,
                new EvidenceSpan($"Other/File{index:D4}.cs", 1, 1, null, "fixture", "fixture/1.0"),
                projectPath: "Other.csproj",
                sourceSymbol: $"global::Other.Type{index}.Method()",
                targetSymbol: "Method",
                properties: new SortedDictionary<string, string>(StringComparer.Ordinal)
                {
                    ["sourceSymbolId"] = $"other-{index:D4}"
                }))
            .ToArray();
        var countedFacts = new CountingReadOnlyList<CodeFact>(unrelatedFacts);

        var facts = LegacyWebFormsExtractor.Extract(repo, manifest, FileInventory.Collect(repo), countedFacts);

        Assert.Equal(200, facts.Count(fact => fact.FactType == FactTypes.WebFormsHandlerResolved));
        Assert.Equal(200, facts.Count(fact => fact.FactType == FactTypes.WebFormsEventFlowProjected));
        Assert.True(
            countedFacts.EnumerationCount <= 5,
            $"Expected a bounded number of existing-evidence passes, observed {countedFacts.EnumerationCount}.");
    }

    private static void WriteBasicPage(string repo, string handlerName, string handlerBody)
    {
        WritePage(repo, "Default.aspx", "Default.aspx.cs", "Sample.Default", handlerName);
        WriteCodeBehind(repo, handlerName, handlerBody);
    }

    private static void WritePage(string repo, string markupFileName, string codeBehindFileName, string inherits, string handlerName)
    {
        File.WriteAllText(Path.Combine(repo, markupFileName), $$"""
            <%@ Page Language="C#" CodeBehind="{{codeBehindFileName}}" Inherits="{{inherits}}" %>
            <asp:Button runat="server" ID="SaveButton" OnClick="{{handlerName}}" />
            """);
    }

    private static void WriteCodeBehind(string repo, string handlerName, string handlerBody)
    {
        WritePageCodeBehind(repo, "Default.aspx.cs", "Default", handlerName, handlerBody);
    }

    private static void WritePageCodeBehind(string repo, string fileName, string className, string handlerName, string handlerBody)
    {
        File.WriteAllText(Path.Combine(repo, fileName), $$"""
            using System;
            namespace Sample;
            public partial class {{className}}
            {
                protected void {{handlerName}}(object sender, EventArgs e)
                {
                    {{handlerBody}}
                }
            }
            """);
    }

    private static string SerializeFacts(IEnumerable<CodeFact> facts)
    {
        return string.Join("\n", facts.Select(fact => JsonSerializer.Serialize(fact)));
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "rules", "rule-catalog.yml")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Unable to locate the TraceMap repository root.");
    }

    private sealed class CountingReadOnlyList<T>(IReadOnlyList<T> items) : IReadOnlyList<T>
    {
        public int EnumerationCount { get; private set; }

        public int Count => items.Count;

        public T this[int index] => items[index];

        public IEnumerator<T> GetEnumerator()
        {
            EnumerationCount++;
            return items.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
