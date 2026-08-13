using System.Text.Json;
using Microsoft.Data.Sqlite;
using TraceMap.Core;
using TraceMap.Storage;

namespace TraceMap.Tests;

public sealed class LegacyWebFormsEventIdentityTests
{
    [Fact]
    public async Task Same_named_surfaces_keep_event_sources_and_structural_handlers_distinct_through_persistence()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        foreach (var area in new[] { "AreaA", "AreaB" })
        {
            Directory.CreateDirectory(Path.Combine(repo, area));
            File.WriteAllText(Path.Combine(repo, area, "Default.aspx"), """
                <%@ Page Language="C#" CodeBehind="Default.aspx.cs" Inherits="Sample.Default" %>
                <asp:Button runat="server" ID="SaveButton" OnClick="Save_Click" />
                """);
            File.WriteAllText(Path.Combine(repo, area, "Default.aspx.cs"), """
                using System;
                namespace Sample;
                public partial class Default
                {
                    protected void Save_Click(object sender, EventArgs e) { }
                }
                """);
        }

        var output = Path.Combine(temp.Path, "out");
        var result = ScanEngine.Scan(new ScanOptions(repo, output));
        var bindings = result.Facts
            .Where(fact => fact.FactType == FactTypes.WebFormsEventBindingDeclared)
            .OrderBy(fact => fact.Evidence.FilePath, StringComparer.Ordinal)
            .ToArray();
        var handlers = result.Facts
            .Where(fact => fact.FactType == FactTypes.WebFormsHandlerResolved)
            .OrderBy(fact => fact.Properties["markupFile"], StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(2, bindings.Length);
        Assert.Equal(2, handlers.Length);
        Assert.All(bindings, binding =>
        {
            Assert.StartsWith("webforms-surface:", binding.Properties["surfaceIdentity"], StringComparison.Ordinal);
            Assert.StartsWith("webforms-control:", binding.Properties["controlIdentity"], StringComparison.Ordinal);
            Assert.Equal(binding.SourceSymbol, binding.Properties["eventSourceIdentity"]);
            Assert.StartsWith("webforms-handler:", binding.TargetSymbol, StringComparison.Ordinal);
        });
        Assert.NotEqual(bindings[0].SourceSymbol, bindings[1].SourceSymbol);
        Assert.NotEqual(bindings[0].TargetSymbol, bindings[1].TargetSymbol);
        Assert.All(handlers, handler =>
        {
            var binding = Assert.Single(bindings, candidate => candidate.FactId == handler.Properties["bindingFactId"]);
            Assert.Equal(binding.SourceSymbol, handler.SourceSymbol);
            Assert.Equal(binding.TargetSymbol, handler.TargetSymbol);
            Assert.Equal(handler.TargetSymbol, handler.Properties["handlerSymbolId"]);
            Assert.Equal(handler.TargetSymbol, handler.Properties["sourceSymbolId"]);
            Assert.Equal(binding.Properties["surfaceIdentity"], handler.Properties["surfaceIdentity"]);
            Assert.Equal(binding.FactId, handler.Properties["supportingFactIds"]);
            Assert.Equal(RuleIds.LegacyWebFormsHandlerResolution, handler.RuleId);
            Assert.Equal(ScannerVersions.LegacyWebFormsExtractor, handler.Evidence.ExtractorVersion);
            Assert.Equal(result.Manifest.CommitSha, handler.CommitSha);
        });
        Assert.NotEqual(handlers[0].TargetSymbol, handlers[1].TargetSymbol);

        await JsonlFactWriter.WriteAsync(Path.Combine(output, "facts.ndjson"), result.Facts);
        SqliteIndexWriter.Write(Path.Combine(output, "index.sqlite"), result.Manifest, result.Facts);
        var directionalFacts = bindings.Concat(handlers).OrderBy(fact => fact.FactId, StringComparer.Ordinal).ToArray();
        var persisted = File.ReadLines(Path.Combine(output, "facts.ndjson"))
            .Select(line => JsonSerializer.Deserialize<CodeFact>(line, new JsonSerializerOptions(JsonSerializerDefaults.Web))!)
            .Where(fact => directionalFacts.Any(expected => expected.FactId == fact.FactId))
            .OrderBy(fact => fact.FactId, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(directionalFacts.Select(fact => fact.SourceSymbol), persisted.Select(fact => fact.SourceSymbol));
        Assert.Equal(directionalFacts.Select(fact => fact.TargetSymbol), persisted.Select(fact => fact.TargetSymbol));
        Assert.Equal(directionalFacts.Select(fact => fact.Properties["surfaceIdentity"]), persisted.Select(fact => fact.Properties["surfaceIdentity"]));

        using var connection = new SqliteConnection($"Data Source={Path.Combine(output, "index.sqlite")}");
        connection.Open();
        foreach (var directionalFact in directionalFacts)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "select source_symbol, target_symbol, rule_id, evidence_tier, file_path, start_line, end_line, extractor_version, properties_json from facts where fact_id = $id";
            command.Parameters.AddWithValue("$id", directionalFact.FactId);
            using var reader = command.ExecuteReader();
            Assert.True(reader.Read());
            Assert.Equal(directionalFact.SourceSymbol, reader.GetString(0));
            Assert.Equal(directionalFact.TargetSymbol, reader.GetString(1));
            Assert.Equal(directionalFact.RuleId, reader.GetString(2));
            Assert.Equal(directionalFact.EvidenceTier, reader.GetString(3));
            Assert.Equal(directionalFact.Evidence.FilePath, reader.GetString(4));
            Assert.Equal(directionalFact.Evidence.StartLine, reader.GetInt32(5));
            Assert.Equal(directionalFact.Evidence.EndLine, reader.GetInt32(6));
            Assert.Equal(directionalFact.Evidence.ExtractorVersion, reader.GetString(7));
            var properties = JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(8));
            Assert.Equal(directionalFact.Properties["surfaceIdentity"], properties!["surfaceIdentity"]);
            Assert.Equal(directionalFact.Properties["handlerSymbolId"], properties["handlerSymbolId"]);
            if (directionalFact.FactType == FactTypes.WebFormsHandlerResolved)
            {
                Assert.Equal(directionalFact.Properties["supportingFactIds"], properties["supportingFactIds"]);
            }
        }
    }

    [Fact]
    public void Explicit_control_subscriptions_are_bounded_and_dynamic_or_unknown_shapes_fail_closed()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Default.aspx"), """
            <%@ Page Language="C#" CodeBehind="Default.aspx.cs" Inherits="Sample.Default" AutoEventWireup="false" %>
            <asp:Button runat="server" ID="SaveButton" />
            """);
        File.WriteAllText(Path.Combine(repo, "Default.aspx.cs"), """
            using System;
            namespace Sample;
            public partial class Default
            {
                public Default()
                {
                    SaveButton.Click += Save_Click;
                    SaveButton.Click += (sender, args) => Save_Click(sender, args);
                    UnknownButton.Click += Save_Click;
                    Load += Page_Load;
                }

                protected object SaveButton { get; } = new object();
                protected void Save_Click(object sender, EventArgs e) { }
                protected void Page_Load(object sender, EventArgs e) { }
            }
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));
        var binding = Assert.Single(result.Facts, fact =>
            fact.FactType == FactTypes.WebFormsEventBindingDeclared
            && fact.Properties.GetValueOrDefault("bindingKind") == "ExplicitControlSubscription");
        var handler = Assert.Single(result.Facts, fact =>
            fact.FactType == FactTypes.WebFormsHandlerResolved
            && fact.Properties.GetValueOrDefault("bindingFactId") == binding.FactId);

        Assert.Equal("SaveButton", binding.Properties["controlId"]);
        Assert.Equal("OnClick", binding.Properties["eventName"]);
        Assert.Equal("Default.aspx.cs", binding.Evidence.FilePath);
        Assert.Equal(binding.SourceSymbol, handler.SourceSymbol);
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyWebFormsEventBinding
            && fact.Properties.GetValueOrDefault("gapKind") == "DynamicWebFormsEventSubscription");
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.LegacyWebFormsEventBinding
            && fact.Properties.GetValueOrDefault("gapKind") == "UnknownWebFormsEventSubscriptionReceiver");
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.WebFormsHandlerResolved
            && fact.ContractElement == "Page_Load"
            && fact.Properties.GetValueOrDefault("explicitEventSubscription") == "True");
    }

    [Fact]
    public void Cross_file_partial_handler_requires_exact_semantic_method_evidence()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Sample.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(repo, "Default.aspx"), """
            <%@ Page Language="C#" CodeBehind="Default.aspx.cs" Inherits="Sample.Default" %>
            <asp:Button runat="server" ID="SaveButton" OnClick="Save_Click" />
            """);
        File.WriteAllText(Path.Combine(repo, "Default.aspx.cs"), """
            namespace Sample;
            public partial class Default { }
            """);
        File.WriteAllText(Path.Combine(repo, "Handlers.aspx.cs"), """
            using System;
            namespace Sample;
            public partial class Default
            {
                protected void Save_Click(object sender, EventArgs e)
                {
                    Helper.Touch();
                }
            }

            public static class Helper
            {
                public static void Touch() { }
            }
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));
        var handler = Assert.Single(result.Facts, fact =>
            fact.FactType == FactTypes.WebFormsHandlerResolved
            && fact.ContractElement == "Save_Click");
        var binding = Assert.Single(result.Facts, fact =>
            fact.FactType == FactTypes.WebFormsEventBindingDeclared
            && fact.ContractElement == "Save_Click");

        Assert.Equal(EvidenceTiers.Tier1Semantic, handler.EvidenceTier);
        Assert.Equal("SemanticSourceSymbol", handler.Properties["resolutionKind"]);
        Assert.Equal("Handlers.aspx.cs", handler.Evidence.FilePath);
        Assert.StartsWith("csharp method ", handler.TargetSymbol, StringComparison.Ordinal);
        Assert.Equal(binding.TargetSymbol, handler.TargetSymbol);
        Assert.DoesNotContain(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.Properties.GetValueOrDefault("gapKind") == "UnprovenCrossFileWebFormsHandler");
    }

    [Fact]
    public void Missing_and_overloaded_named_lifecycle_subscriptions_remain_loud()
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
                    Load += Missing_Load;
                    Init += Duplicate_Init;
                }

                protected void Duplicate_Init(object sender, EventArgs e) { }
                protected void Duplicate_Init(object sender, EventArgs e, string extra) { }
            }
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));

        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.WebFormsEventBindingDeclared
            && fact.Properties.GetValueOrDefault("bindingKind") == "ExplicitLifecycleSubscription"
            && fact.ContractElement == "Missing_Load");
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.Evidence.FilePath == "Default.aspx.cs"
            && fact.Properties.GetValueOrDefault("gapKind") == "MissingWebFormsHandler");
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.WebFormsEventBindingDeclared
            && fact.ContractElement == "Duplicate_Init");
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.Properties.GetValueOrDefault("gapKind") == "AmbiguousWebFormsHandler");
    }

    [Fact]
    public void Explicit_subscriptions_are_page_scoped_and_support_page_and_base_receivers()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Default.aspx"), """
            <%@ Page Language="C#" CodeBehind="Default.aspx.cs" Inherits="Sample.Default" AutoEventWireup="false" %>
            <asp:Button runat="server" ID="SaveButton" />
            """);
        File.WriteAllText(Path.Combine(repo, "Default.aspx.cs"), """
            using System;
            namespace Sample;
            public partial class Default
            {
                public Default()
                {
                    base.SaveButton.Click += Save_Click;
                    Page.Load += Page_Load;
                }

                protected void Save_Click(object sender, EventArgs e) { }
                protected void Page_Load(object sender, EventArgs e) { }
            }

            public sealed class Helper
            {
                public void Wire()
                {
                    SaveButton.Click += Helper_Click;
                }

                private object SaveButton { get; } = new object();
                private void Helper_Click(object sender, EventArgs e) { }
            }
            """);

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));

        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.WebFormsEventBindingDeclared
            && fact.ContractElement == "Save_Click"
            && fact.Properties.GetValueOrDefault("bindingKind") == "ExplicitControlSubscription");
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.WebFormsEventBindingDeclared
            && fact.ContractElement == "Page_Load"
            && fact.Properties.GetValueOrDefault("bindingKind") == "ExplicitLifecycleSubscription");
        Assert.DoesNotContain(result.Facts, fact =>
            fact.FactType == FactTypes.WebFormsEventBindingDeclared
            && fact.ContractElement == "Helper_Click");
        Assert.DoesNotContain(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.Properties.GetValueOrDefault("gapKind") == "AutoEventWireupUnavailable"
            && fact.Properties.GetValueOrDefault("message")?.Contains("Page_Load", StringComparison.Ordinal) == true);
    }
}
