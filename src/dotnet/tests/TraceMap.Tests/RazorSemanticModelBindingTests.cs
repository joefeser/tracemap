using Microsoft.Data.Sqlite;
using TraceMap.Core;
using TraceMap.Storage;

namespace TraceMap.Tests;

public sealed class RazorSemanticModelBindingTests
{
    private static readonly HashSet<string> TargetPropertySchema = new(StringComparer.Ordinal)
    {
        "actionName", "bindingAttributeAssemblyName", "bindingAttributePublicKeyToken", "bindingAttributeType",
        "bindingKind", "controllerName", "coverageLabel", "frameworkOwnerAssemblyName", "frameworkOwnerPublicKeyToken",
        "frameworkOwnerType", "handlerName", "httpMethods", "limitations", "modelKind", "modelType", "modelTypeAssemblyName",
        "modelTypeAssemblyVersion", "modelTypeContainingSymbolId", "modelTypeDisplay", "modelTypeSymbolId", "modelTypeSymbolKind",
        "ownerAssemblyName", "ownerAssemblyVersion", "ownerContainingSymbolId", "ownerFamily", "ownerSymbolId", "ownerSymbolKind",
        "pageModelName", "parameterAssemblyName", "parameterAssemblyVersion", "parameterContainingSymbolId", "parameterName",
        "parameterOrdinal", "parameterSource", "parameterSymbolId", "parameterSymbolKind", "propertyName", "propertyPath",
        "propertyType", "reconciliationKeyVersion", "supportsGet", "targetAssemblyName", "targetAssemblyVersion",
        "targetContainingSymbolId", "targetSymbolId", "targetSymbolKind", "uiFramework", "valueStored"
    };

    private static readonly HashSet<string> GapPropertySchema = new(StringComparer.Ordinal)
    {
        "bindingKind", "coverageEffect", "coverageLabel", "frameworkState", "gapKind", "limitations", "occurrenceCount", "ownerState", "sanitization",
        "scopeAssemblyName", "scopeAssemblyVersion", "scopeContainingSymbolId", "scopeSymbolId", "scopeSymbolKind",
        "targetTypeAssemblyName", "targetTypeAssemblyVersion", "targetTypeContainingSymbolId", "targetTypeSymbolId", "targetTypeSymbolKind", "typeState"
    };

    [Fact]
    public async Task Scan_emits_signed_semantic_mvc_page_handler_and_bind_property_targets()
    {
        using var temp = new TempDirectory();
        var project = Path.Combine(temp.Path, "src", "WebSample");
        Directory.CreateDirectory(project);
        File.WriteAllText(Path.Combine(project, "WebSample.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <RuntimeFrameworkVersion>10.0.10</RuntimeFrameworkVersion>
                <TargetLatestRuntimePatch>false</TargetLatestRuntimePatch>
                <Nullable>enable</Nullable>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(project, "InputModel.Part1.cs"), """
            namespace WebSample;

            public class BaseInputModel
            {
                public string BaseName { get; set; } = "";
                public string Hidden { get; set; } = "";
            }

            public interface IExplicitModel
            {
                string ExplicitValue { set; }
            }

            public sealed partial class InputModel : BaseInputModel, IExplicitModel
            {
                private string _refValue = "";
                public string Name { get; set; } = "";
                public static string StaticValue { get; set; } = "";
                public string ReadOnly => "";
                public string InitOnly { get; init; } = "";
                public string PrivateSetter { get; private set; } = "";
                public new string Hidden { get; private set; } = "";
                public string this[int index] { get => ""; set { } }
                public ref string RefValue => ref _refValue;
                string IExplicitModel.ExplicitValue { set { } }
            }

            public sealed record RecordInput(string ConstructorValue);

            public sealed class ExternalBaseInput : System.Exception
            {
                public string LocalValue { get; set; } = "";
            }
            """);
        File.WriteAllText(Path.Combine(project, "InputModel.Part2.cs"), """
            namespace WebSample;

            public sealed partial class InputModel
            {
                public string Email { get; set; } = "";
            }
            """);
        File.WriteAllText(Path.Combine(project, "Endpoints.cs"), """
            using Microsoft.AspNetCore.Mvc;
            using Microsoft.AspNetCore.Mvc.RazorPages;

            namespace WebSample;

            public sealed class OrdersController : ControllerBase
            {
                [HttpPost]
                public IActionResult Save([FromBody] InputModel input) => Ok();

                [HttpPut]
                [HttpPatch]
                public IActionResult Save([FromForm] InputModel input, int marker) => Ok();

                public IActionResult SaveRecord([FromForm] RecordInput input) => Ok();
                public IActionResult SaveAmbiguous([FromBody, FromForm] InputModel input) => Ok();
                public IActionResult SaveDynamic(dynamic input) => Ok();
                public IActionResult SaveGeneric<T>(T input) => Ok();
                public IActionResult SaveExternal(string input) => Ok();
                public IActionResult SaveExternalBase(ExternalBaseInput input) => Ok();

                [NonAction]
                public void Ignore([FromBody] InputModel input) { }
            }

            public sealed class OrdersPage : PageModel
            {
                [BindProperty(SupportsGet = true)]
                public string Search { get; set; } = "";

                [BindProperty]
                public string DefaultValue { get; set; } = "";

                public void OnPost(InputModel input) { }

                [NonHandler]
                public void OnPostIgnored(InputModel input) { }
            }

            [NonController]
            public sealed class HiddenController : ControllerBase
            {
                public IActionResult Save([FromBody] InputModel input) => Ok();
            }

            internal sealed class InternalController : ControllerBase
            {
                public IActionResult Save(InputModel input) => Ok();
            }

            public abstract class AbstractController : ControllerBase
            {
                public IActionResult Save(InputModel input) => Ok();
            }

            public sealed class Outer
            {
                public sealed class NestedController : ControllerBase
                {
                    public IActionResult Save(InputModel input) => Ok();
                }
            }

            public sealed class Helper
            {
                public void Run([FromBody] InputModel input) { }

                [BindProperty]
                public string HelperBound { get; set; } = "";
            }

            public sealed class GenericController<T> : ControllerBase
            {
                public IActionResult SaveType(InputModel input) => Ok();
            }
            """);

        var first = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, ".tracemap-first")));
        var second = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, ".tracemap-second")));
        var semantic = first.Facts
            .Where(fact => fact.RuleId == RuleIds.CSharpRazorSemanticModelBinding)
            .OrderBy(fact => fact.FactId, StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(semantic);
        Assert.Contains(first.Facts, fact => fact.RuleId == RuleIds.RazorModelBinding
            && fact.FactType == FactTypes.RazorModelBindingTarget);
        Assert.All(semantic, fact =>
        {
            Assert.Equal(FactTypes.RazorModelBindingTarget, fact.FactType);
            Assert.Equal(EvidenceTiers.Tier1Semantic, fact.EvidenceTier);
            Assert.Equal("bounded-static-semantic-model-binding", fact.Properties["coverageLabel"]);
            Assert.True(fact.Properties.ContainsKey("ownerSymbolId"));
            Assert.True(fact.Properties.ContainsKey("targetSymbolId"));
            Assert.Equal("adb9793829ddae60", fact.Properties["frameworkOwnerPublicKeyToken"]);
            Assert.NotEmpty(fact.Properties["limitations"]);
            Assert.Empty(fact.Properties.Keys.Except(TargetPropertySchema, StringComparer.Ordinal));
        });
        Assert.Contains(semantic, fact =>
            fact.Properties["bindingKind"] == "mvc-action-parameter"
            && fact.Properties["propertyName"] == "Name"
            && fact.Properties["parameterSource"] == "body"
            && fact.Properties["httpMethods"] == "POST"
            && fact.Properties["frameworkOwnerType"] == "Microsoft.AspNetCore.Mvc.ControllerBase"
            && fact.Properties["frameworkOwnerAssemblyName"] == "Microsoft.AspNetCore.Mvc.Core"
            && fact.Properties["bindingAttributeType"] == "Microsoft.AspNetCore.Mvc.FromBodyAttribute"
            && fact.Properties["bindingAttributeAssemblyName"] == "Microsoft.AspNetCore.Mvc.Core");
        Assert.Contains(semantic, fact =>
            fact.Properties["bindingKind"] == "mvc-action-parameter"
            && fact.Properties["propertyName"] == "Email");
        Assert.Contains(semantic, fact =>
            fact.Properties["bindingKind"] == "mvc-action-parameter"
            && fact.Properties["propertyName"] == "BaseName");
        var overloadedNames = semantic.Where(fact =>
                fact.Properties["bindingKind"] == "mvc-action-parameter"
                && fact.Properties["propertyName"] == "Name")
            .ToArray();
        Assert.Equal(2, overloadedNames.Length);
        Assert.Equal(2, overloadedNames.Select(fact => fact.Properties["ownerSymbolId"]).Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(overloadedNames, fact => fact.Properties["parameterSource"] == "form"
            && fact.Properties["httpMethods"] == "PATCH;PUT");
        Assert.Contains(semantic, fact =>
            fact.Properties["bindingKind"] == "razor-page-handler-parameter"
            && fact.Properties["handlerName"] == "OnPost"
            && fact.Properties["httpMethods"] == "POST");
        var searchProperty = Assert.Single(semantic, fact =>
            fact.Properties["bindingKind"] == "razor-page-property"
            && fact.Properties["propertyName"] == "Search");
        Assert.Equal("true", searchProperty.Properties["supportsGet"]);
        Assert.Equal(string.Empty, searchProperty.Properties["httpMethods"]);
        Assert.Equal("Microsoft.AspNetCore.Mvc.RazorPages.PageModel", searchProperty.Properties["frameworkOwnerType"]);
        Assert.Equal("Microsoft.AspNetCore.Mvc.BindPropertyAttribute", searchProperty.Properties["bindingAttributeType"]);
        Assert.NotEqual(searchProperty.Properties["ownerSymbolId"], searchProperty.Properties["targetSymbolId"]);
        Assert.Equal("NamedType", searchProperty.Properties["ownerSymbolKind"]);
        Assert.DoesNotContain("handlerName", searchProperty.Properties.Keys);
        Assert.Contains(semantic, fact =>
            fact.Properties["bindingKind"] == "razor-page-property"
            && fact.Properties["propertyName"] == "DefaultValue"
            && fact.Properties["supportsGet"] == "false");
        Assert.DoesNotContain(semantic, fact => fact.SourceSymbol?.Contains("Helper.Run", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(semantic, fact => fact.Properties["propertyName"] == "HelperBound");
        Assert.DoesNotContain(semantic, fact => fact.SourceSymbol?.Contains("OrdersController.Ignore", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(semantic, fact => fact.Properties["propertyName"] is "StaticValue" or "ReadOnly" or "InitOnly" or "PrivateSetter" or "Hidden" or "RefValue" or "ExplicitValue" or "ConstructorValue" or "Item");
        Assert.DoesNotContain(semantic, fact => fact.SourceSymbol?.Contains("HiddenController", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(semantic, fact => fact.SourceSymbol?.Contains("OnPostIgnored", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(semantic, fact => fact.Properties.GetValueOrDefault("controllerName") is "Internal" or "Abstract" or "Nested" or "Generic");
        Assert.Contains(semantic, fact => fact.Properties["propertyName"] == "LocalValue"
            && fact.SourceSymbol?.Contains("SaveExternalBase", StringComparison.Ordinal) == true);

        var gaps = first.Facts.Where(fact => fact.RuleId == RuleIds.CSharpRazorSemanticModelBindingGap).ToArray();
        Assert.Contains(gaps, gap => gap.Properties["gapKind"] == "RazorBindingPropertyUnavailable");
        Assert.Contains(gaps, gap => gap.Properties["gapKind"] == "AmbiguousRazorBindingTarget");
        Assert.Contains(gaps, gap => gap.Properties["gapKind"] == "RazorBindingTypeUnavailable"
            && gap.Properties["bindingKind"] == "mvc-action-parameter");
        Assert.Contains(gaps, gap => gap.Properties.GetValueOrDefault("typeState") == "dynamic");
        Assert.Contains(gaps, gap => gap.Properties.GetValueOrDefault("typeState") == "type-parameter");
        Assert.Contains(gaps, gap => gap.Properties.GetValueOrDefault("typeState") == "external-unavailable");
        Assert.Contains(gaps, gap => gap.Properties["gapKind"] == "RazorBindingExternalBaseUnavailable"
            && gap.Properties.GetValueOrDefault("typeState") == "external-base-properties-unavailable");
        Assert.True(gaps.Count(gap => gap.Properties["gapKind"] == "RazorEndpointOwnerUnavailable"
            && gap.Properties.GetValueOrDefault("ownerState") == "controller-type-not-discoverable") >= 4);
        Assert.DoesNotContain(semantic, fact => fact.SourceSymbol?.Contains("SaveAmbiguous", StringComparison.Ordinal) == true);
        Assert.All(gaps, gap =>
        {
            Assert.Equal(FactTypes.AnalysisGap, gap.FactType);
            Assert.Equal(EvidenceTiers.Tier4Unknown, gap.EvidenceTier);
            Assert.False(gap.Properties.ContainsKey("message"));
            Assert.Empty(gap.Properties.Keys.Except(GapPropertySchema, StringComparer.Ordinal));
        });
        Assert.DoesNotContain(gaps, gap => gap.Properties.Values.Any(value => value.Contains("HelperBound", StringComparison.Ordinal)));

        var firstProjection = semantic.Select(StableProjection).ToArray();
        var secondProjection = second.Facts
            .Where(fact => fact.RuleId == RuleIds.CSharpRazorSemanticModelBinding)
            .OrderBy(fact => fact.FactId, StringComparer.Ordinal)
            .Select(StableProjection)
            .ToArray();
        Assert.Equal(firstProjection, secondProjection);

        var artifacts = Path.Combine(temp.Path, "artifacts");
        Directory.CreateDirectory(artifacts);
        var factsPath = Path.Combine(artifacts, "facts.ndjson");
        var indexPath = Path.Combine(artifacts, "index.sqlite");
        await JsonlFactWriter.WriteAsync(factsPath, first.Facts);
        SqliteIndexWriter.Write(indexPath, first.Manifest, first.Facts);
        Assert.Contains(RuleIds.CSharpRazorSemanticModelBinding, File.ReadAllText(factsPath), StringComparison.Ordinal);
        using var connection = new SqliteConnection($"Data Source={indexPath}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "select count(*) from facts where rule_id = $rule;";
        command.Parameters.AddWithValue("$rule", RuleIds.CSharpRazorSemanticModelBinding);
        Assert.Equal(semantic.Length, Convert.ToInt32(command.ExecuteScalar()));
    }

    [Fact]
    public void Scan_rejects_source_declared_framework_lookalikes()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "Sample.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <RuntimeFrameworkVersion>10.0.10</RuntimeFrameworkVersion>
                <TargetLatestRuntimePatch>false</TargetLatestRuntimePatch>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(temp.Path, "Sample.cs"), """
            namespace Microsoft.AspNetCore.Mvc
            {
                public class ControllerBase { }
                public sealed class FromBodyAttribute : System.Attribute { }
                public sealed class BindPropertyAttribute : System.Attribute { }
            }

            namespace Microsoft.AspNetCore.Mvc.RazorPages
            {
                public class PageModel { }
            }

            namespace Sample
            {
                public sealed class InputModel { public string Name { get; set; } = ""; }
                public sealed class FakeController : Microsoft.AspNetCore.Mvc.ControllerBase
                {
                    public void Save([Microsoft.AspNetCore.Mvc.FromBody] InputModel input) { }
                }

                public sealed class FakePage : Microsoft.AspNetCore.Mvc.RazorPages.PageModel
                {
                    [Microsoft.AspNetCore.Mvc.BindProperty]
                    public string Search { get; set; } = "";
                }
            }
            """);

        var result = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, ".tracemap")));

        Assert.DoesNotContain(result.Facts, fact => fact.RuleId == RuleIds.CSharpRazorSemanticModelBinding);
        Assert.True(result.Facts.Count(fact => fact.RuleId == RuleIds.CSharpRazorSemanticModelBindingGap
            && fact.Properties["gapKind"] == "RazorFrameworkIdentityUnavailable") >= 2);
    }

    [Fact]
    public void Scan_keeps_same_named_models_from_distinct_project_assemblies_separate()
    {
        using var temp = new TempDirectory();
        var source = Path.Combine(temp.Path, "src");
        var contractsA = Path.Combine(source, "ContractsA");
        var contractsB = Path.Combine(source, "ContractsB");
        var web = Path.Combine(source, "Web");
        Directory.CreateDirectory(contractsA);
        Directory.CreateDirectory(contractsB);
        Directory.CreateDirectory(web);
        WriteContractProject(contractsB, "ContractsB");
        WriteContractProject(contractsA, "ContractsA");
        File.WriteAllText(Path.Combine(web, "Web.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
              <ItemGroup>
                <ProjectReference Include="../ContractsA/ContractsA.csproj"><Aliases>A</Aliases></ProjectReference>
                <ProjectReference Include="../ContractsB/ContractsB.csproj"><Aliases>B</Aliases></ProjectReference>
              </ItemGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(web, "Endpoints.cs"), """
            extern alias A;
            extern alias B;
            using Microsoft.AspNetCore.Mvc;

            namespace Web;

            public sealed class CollisionController : ControllerBase
            {
                [HttpPost]
                public IActionResult SaveA([FromBody] A::Shared.InputModel input) => Ok();

                [HttpPost]
                public IActionResult SaveB([FromBody] B::Shared.InputModel input) => Ok();
            }
            """);

        var result = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, ".tracemap")));
        var targets = result.Facts.Where(fact => fact.RuleId == RuleIds.CSharpRazorSemanticModelBinding
                && fact.Properties["propertyName"] == "Name")
            .OrderBy(fact => fact.Properties["modelTypeAssemblyName"], StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(2, targets.Length);
        Assert.Equal(["ContractsA", "ContractsB"], targets.Select(fact => fact.Properties["modelTypeAssemblyName"]));
        Assert.Equal(2, targets.Select(fact => fact.Properties["modelTypeSymbolId"]).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(2, targets.Select(fact => fact.Properties["targetSymbolId"]).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Semantic_razor_rules_are_versioned_and_catalogued_with_limitations()
    {
        Assert.Equal("csharp.razor.semantic-model-binding.v1", RuleIds.CSharpRazorSemanticModelBinding);
        Assert.Equal("csharp.razor.semantic-model-binding-gap.v1", RuleIds.CSharpRazorSemanticModelBindingGap);
        Assert.Equal("csharp-semantic/0.18.0", ScannerVersions.CSharpSemanticExtractor);
        var catalog = File.ReadAllText(Path.Combine(FindRepoRoot(), "rules", "rule-catalog.yml"));
        Assert.Contains($"id: {RuleIds.CSharpRazorSemanticModelBinding}", catalog, StringComparison.Ordinal);
        Assert.Contains($"id: {RuleIds.CSharpRazorSemanticModelBindingGap}", catalog, StringComparison.Ordinal);
        Assert.Contains("does not prove runtime binding", catalog, StringComparison.Ordinal);
        Assert.Contains("excluded from the legacy name-based property-flow reporter", catalog, StringComparison.Ordinal);
    }

    private static void WriteContractProject(string directory, string assemblyName)
    {
        File.WriteAllText(Path.Combine(directory, $"{assemblyName}.csproj"), $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <AssemblyName>{assemblyName}</AssemblyName>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(directory, "InputModel.cs"), """
            namespace Shared;
            public sealed class InputModel
            {
                public string Name { get; set; } = "";
            }
            """);
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
        throw new DirectoryNotFoundException("Unable to locate repository root.");
    }

    private static string StableProjection(CodeFact fact) => string.Join('|',
        fact.FactType,
        fact.RuleId,
        fact.EvidenceTier,
        fact.SourceSymbol,
        fact.TargetSymbol,
        fact.ContractElement,
        fact.Evidence.FilePath,
        fact.Evidence.StartLine,
        fact.Evidence.EndLine,
        string.Join(';', fact.Properties.Select(pair => $"{pair.Key}={pair.Value}")));
}
