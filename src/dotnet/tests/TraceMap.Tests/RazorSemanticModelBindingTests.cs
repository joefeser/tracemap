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
        "propertyType", "reconciliationProfileVersion", "supportsGet", "targetAssemblyName", "targetAssemblyVersion", "tier3ReconciliationState",
        "targetContainingSymbolId", "targetSymbolId", "targetSymbolKind", "uiFramework", "valueStored"
    };

    private static readonly HashSet<string> GapPropertySchema = new(StringComparer.Ordinal)
    {
        "bindingKind", "coverageEffect", "coverageLabel", "frameworkState", "gapKind", "limitations", "occurrenceCount", "ownerState", "sanitization",
        "endpointMethodAssemblyName", "endpointMethodAssemblyVersion", "endpointMethodContainingSymbolId", "endpointMethodSymbolId", "endpointMethodSymbolKind",
        "ownerAssemblyName", "ownerAssemblyVersion", "ownerContainingSymbolId", "ownerSymbolId", "ownerSymbolKind", "parameterAssemblyName",
        "parameterAssemblyVersion", "parameterContainingSymbolId", "parameterOrdinal", "parameterSymbolId", "parameterSymbolKind",
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

                [Microsoft.AspNetCore.Mvc.ModelBinding.BindNever]
                public string NeverBound { get; set; } = "";
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

            public sealed class ConstructorOnlyInput
            {
                public ConstructorOnlyInput(string value) => Value = value;
                public string Value { get; set; }
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

            public sealed partial class OrdersController : ControllerBase
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
                public IActionResult SaveExternal(System.Exception input) => Ok();
                public IActionResult SaveExternalBase(ExternalBaseInput input) => Ok();
                public IActionResult SaveService([FromServices] InputModel service) => Ok();
                public IActionResult SaveConstructorOnly([FromForm] ConstructorOnlyInput input) => Ok();
                public partial IActionResult PartialSave(InputModel input);
                public partial IActionResult PartialSave(InputModel input) => Ok();

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
                public void OnPoster(InputModel input) { }
                public void OnTraceAsync(InputModel input) { }

                [NonHandler]
                public void OnPostIgnored(InputModel input) { }
            }

            public abstract class AbstractOrdersPage : PageModel
            {
                [BindProperty]
                public string AbstractBound { get; set; } = "";

                public void OnPostAbstract(InputModel input) { }
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

            [NonController]
            public abstract class HiddenControllerBase : ControllerBase { }

            public sealed class InheritedHiddenController : HiddenControllerBase
            {
                public IActionResult SaveInheritedHidden(InputModel input) => Ok();
            }

            public abstract class ActionBaseController : ControllerBase
            {
                [NonAction]
                public virtual IActionResult IgnoreInherited(InputModel input) => Ok();

                public IActionResult InheritedAction(InputModel input) => Ok();
            }

            public sealed class DerivedActionController : ActionBaseController
            {
                public override IActionResult IgnoreInherited(InputModel input) => Ok();
            }

            public sealed class SecondDerivedActionController : ActionBaseController { }

            [NonController]
            public sealed class ExcludedDerivedController : ActionBaseController { }

            public abstract class VerbBaseController : ControllerBase
            {
                [HttpPost]
                public virtual IActionResult InheritedVerb(InputModel input) => Ok();
            }

            public sealed class VerbController : VerbBaseController
            {
                public override IActionResult InheritedVerb(InputModel input) => Ok();
            }

            public abstract class RootActionController : ControllerBase
            {
                public virtual IActionResult LayeredAction(InputModel input) => Ok();
            }

            public abstract class MiddleActionController : RootActionController
            {
                public override IActionResult LayeredAction(InputModel input) => Ok();
            }

            public sealed class LeafActionController : MiddleActionController { }

            public sealed class HidingActionController : RootActionController
            {
                public new IActionResult LayeredAction(InputModel input) => Ok();
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
            Assert.Equal("contextual-support-only-missing-canonical-identity", fact.Properties["tier3ReconciliationState"]);
        });
        Assert.Contains(first.Manifest.KnownGaps, gap => gap.StartsWith("Semantic Razor model-binding coverage reduced:", StringComparison.Ordinal));
        Assert.DoesNotContain("Roslyn semantic analysis reported a gap.", first.Manifest.KnownGaps);
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
                && fact.Properties.GetValueOrDefault("actionName") == "Save"
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
        Assert.Contains(semantic, fact =>
            fact.Properties["bindingKind"] == "razor-page-handler-parameter"
            && fact.Properties["handlerName"] == "OnPoster"
            && fact.Properties["httpMethods"] == "POSTER");
        Assert.Contains(semantic, fact =>
            fact.Properties["bindingKind"] == "razor-page-handler-parameter"
            && fact.Properties["handlerName"] == "OnTraceAsync"
            && fact.Properties["httpMethods"] == "TRACE");
        var searchProperty = Assert.Single(semantic, fact =>
            fact.Properties["bindingKind"] == "razor-page-property"
            && fact.Properties["propertyName"] == "Search");
        Assert.Equal("true", searchProperty.Properties["supportsGet"]);
        Assert.Equal(string.Empty, searchProperty.Properties["httpMethods"]);
        Assert.Equal("Microsoft.AspNetCore.Mvc.RazorPages.PageModel", searchProperty.Properties["frameworkOwnerType"]);
        Assert.Equal("Microsoft.AspNetCore.Mvc.BindPropertyAttribute", searchProperty.Properties["bindingAttributeType"]);
        Assert.Equal(searchProperty.Properties["ownerSymbolId"], searchProperty.Properties["targetSymbolId"]);
        Assert.Equal("Property", searchProperty.Properties["ownerSymbolKind"]);
        Assert.DoesNotContain("handlerName", searchProperty.Properties.Keys);
        Assert.Contains(semantic, fact =>
            fact.Properties["bindingKind"] == "razor-page-property"
            && fact.Properties["propertyName"] == "DefaultValue"
            && fact.Properties["supportsGet"] == "false");
        Assert.DoesNotContain(semantic, fact => fact.Properties["propertyName"] == "HelperBound");
        Assert.DoesNotContain(semantic, fact => fact.Properties.GetValueOrDefault("actionName") == "Ignore");
        Assert.DoesNotContain(semantic, fact => fact.Properties["propertyName"] is "StaticValue" or "ReadOnly" or "InitOnly" or "PrivateSetter" or "Hidden" or "RefValue" or "ExplicitValue" or "ConstructorValue" or "Item");
        Assert.DoesNotContain(semantic, fact => fact.Properties["propertyName"] == "NeverBound");
        Assert.DoesNotContain(semantic, fact => fact.Properties.GetValueOrDefault("controllerName") == "Hidden");
        Assert.DoesNotContain(semantic, fact => fact.Properties.GetValueOrDefault("actionName") is "SaveInheritedHidden" or "IgnoreInherited" or "SaveService" or "SaveConstructorOnly");
        Assert.DoesNotContain(semantic, fact => fact.Properties.GetValueOrDefault("pageModelName") == "AbstractOrdersPage");
        Assert.DoesNotContain(semantic, fact => fact.Properties.GetValueOrDefault("controllerName") == "ExcludedDerived");
        Assert.Contains(semantic, fact => fact.Properties.GetValueOrDefault("actionName") == "InheritedAction"
            && fact.Properties.GetValueOrDefault("controllerName") == "DerivedAction"
            && fact.Properties["propertyName"] == "Name"
            && fact.Properties["ownerSymbolKind"] == "NamedType");
        Assert.Contains(semantic, fact => fact.Properties.GetValueOrDefault("actionName") == "InheritedVerb"
            && fact.Properties.GetValueOrDefault("controllerName") == "Verb"
            && fact.Properties["httpMethods"] == "POST");
        Assert.Equal(3, semantic.Count(fact => fact.Properties.GetValueOrDefault("actionName") == "PartialSave"));
        Assert.Equal(3, semantic.Count(fact => fact.Properties.GetValueOrDefault("actionName") == "LayeredAction"
            && fact.Properties.GetValueOrDefault("controllerName") == "LeafAction"));
        Assert.Equal(3, semantic.Count(fact => fact.Properties.GetValueOrDefault("actionName") == "LayeredAction"
            && fact.Properties.GetValueOrDefault("controllerName") == "HidingAction"));
        Assert.DoesNotContain(semantic, fact => fact.Properties.GetValueOrDefault("actionName") == "SaveRecord");
        Assert.DoesNotContain(semantic, fact => fact.Properties.GetValueOrDefault("handlerName") == "OnPostIgnored");
        Assert.DoesNotContain(semantic, fact => fact.Properties.GetValueOrDefault("controllerName") is "Internal" or "Abstract" or "Nested" or "Generic");
        Assert.Contains(semantic, fact => fact.Properties["propertyName"] == "LocalValue"
            && fact.SourceSymbol?.Contains("SaveExternalBase", StringComparison.Ordinal) == true);

        var gaps = first.Facts.Where(fact => fact.RuleId == RuleIds.CSharpRazorSemanticModelBindingGap).ToArray();
        Assert.Contains(gaps, gap => gap.Properties["gapKind"] == "RazorBindingPropertyUnavailable");
        var inheritedReadOnlyGaps = gaps.Where(gap =>
            gap.Properties.GetValueOrDefault("gapKind") == "RazorBindingPropertyUnavailable"
            && gap.Properties.GetValueOrDefault("endpointMethodSymbolId")?.Contains("InheritedAction", StringComparison.Ordinal) == true
            && gap.Properties.GetValueOrDefault("scopeSymbolId")?.Contains("ReadOnly", StringComparison.Ordinal) == true).ToArray();
        Assert.Equal(2, inheritedReadOnlyGaps.Length);
        Assert.Equal(2, inheritedReadOnlyGaps.Select(gap => gap.Properties["ownerSymbolId"]).Distinct(StringComparer.Ordinal).Count());
        Assert.All(inheritedReadOnlyGaps, gap => Assert.Equal("0", gap.Properties["parameterOrdinal"]));
        Assert.Contains(gaps, gap => gap.Properties["gapKind"] == "AmbiguousRazorBindingTarget");
        Assert.Contains(gaps, gap => gap.Properties["gapKind"] == "RazorBindingTypeUnavailable"
            && gap.Properties["bindingKind"] == "mvc-action-parameter");
        Assert.Contains(gaps, gap => gap.Properties.GetValueOrDefault("typeState") == "dynamic");
        Assert.Contains(gaps, gap => gap.Properties.GetValueOrDefault("typeState") == "type-parameter");
        Assert.Contains(gaps, gap => gap.Properties.GetValueOrDefault("typeState") == "external-unavailable");
        Assert.Contains(gaps, gap => gap.Properties.GetValueOrDefault("typeState") == "unsupported-record"
            && gap.Properties["gapKind"] == "RazorBindingTypeUnavailable");
        Assert.Contains(gaps, gap => gap.Properties.GetValueOrDefault("typeState") == "constructor-unavailable"
            && gap.Properties["gapKind"] == "RazorBindingTypeUnavailable");
        Assert.Contains(gaps, gap => gap.Properties.GetValueOrDefault("ownerState") == "page-model-type-not-discoverable"
            && gap.Properties["gapKind"] == "RazorEndpointOwnerUnavailable");

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
    public void Scan_distinguishes_terminal_unsupported_and_external_parameter_boundaries_without_failing_build()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "Sample.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
              <ItemGroup><FrameworkReference Include="Microsoft.AspNetCore.App" /></ItemGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(temp.Path, "Endpoints.cs"), """
            using Microsoft.AspNetCore.Mvc;
            namespace Sample;

            public sealed class InputModel
            {
                public string Name { get; set; } = "";
            }

            public sealed class SampleController : ControllerBase
            {
                [HttpPost]
                public IActionResult Save([FromBody] InputModel input) => Ok();
                public IActionResult Scalars(int id, string query, System.Guid token) => Ok();
                public IActionResult Collection(System.Collections.Generic.List<InputModel> input) => Ok();
                public IActionResult Array(InputModel[] input) => Ok();
                public IActionResult External(System.Exception input) => Ok();
            }
            """);

        var result = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, "out")));
        var semantic = result.Facts.Where(fact => fact.RuleId == RuleIds.CSharpRazorSemanticModelBinding).ToArray();
        var gaps = result.Facts.Where(fact => fact.RuleId == RuleIds.CSharpRazorSemanticModelBindingGap).ToArray();

        Assert.Contains(semantic, fact => fact.SourceSymbol == "Save");
        Assert.DoesNotContain(semantic, fact => fact.SourceSymbol == "Scalars");
        Assert.DoesNotContain(gaps, gap => gap.Properties.GetValueOrDefault("scopeSymbolId")?.Contains("Scalars", StringComparison.Ordinal) == true);
        Assert.Contains(gaps, gap => gap.Properties.GetValueOrDefault("scopeSymbolId")?.Contains("Collection", StringComparison.Ordinal) == true
            && gap.Properties.GetValueOrDefault("typeState") == "unsupported-collection");
        Assert.Contains(gaps, gap => gap.Properties.GetValueOrDefault("scopeSymbolId")?.Contains("Array", StringComparison.Ordinal) == true
            && gap.Properties.GetValueOrDefault("typeState") == "unsupported-collection");
        Assert.Contains(gaps, gap => gap.Properties.GetValueOrDefault("scopeSymbolId")?.Contains("External", StringComparison.Ordinal) == true
            && gap.Properties.GetValueOrDefault("typeState") == "external-unavailable");
        Assert.Equal("Succeeded", result.Manifest.BuildStatus);
        Assert.Equal("Level1SemanticAnalysis", result.Manifest.AnalysisLevel);
        Assert.Contains("Semantic Razor model-binding coverage reduced: RazorBindingTypeUnavailable.", result.Manifest.KnownGaps);
        Assert.DoesNotContain("Roslyn semantic analysis reported a gap.", result.Manifest.KnownGaps);
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.AnalyzerCapabilityDiagnostic
            && fact.Properties.GetValueOrDefault("capabilityCode") == AnalyzerCapabilityDiagnosticExtractor.Codes.MSBuildProjectLoad
            && fact.Properties.GetValueOrDefault("capabilityState") == AnalyzerCapabilityDiagnosticExtractor.States.Available);
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.AnalyzerCapabilityDiagnostic
            && fact.Properties.GetValueOrDefault("capabilityCode") == AnalyzerCapabilityDiagnosticExtractor.Codes.CSharpSemanticCompilation
            && fact.Properties.GetValueOrDefault("capabilityState") == AnalyzerCapabilityDiagnosticExtractor.States.Available);

        var tier3Save = Assert.Single(result.Facts, fact =>
            fact.RuleId == RuleIds.RazorModelBinding
            && fact.FactType == FactTypes.RazorModelBindingTarget
            && fact.Properties.GetValueOrDefault("actionName") == "Save"
            && fact.Properties.GetValueOrDefault("propertyName") == "Name");
        var tier1Save = Assert.Single(semantic, fact =>
            fact.Properties.GetValueOrDefault("actionName") == "Save"
            && fact.Properties.GetValueOrDefault("propertyName") == "Name");
        Assert.Equal("action-parameter", tier3Save.Properties["bindingKind"]);
        Assert.Equal("mvc-action-parameter", tier1Save.Properties["bindingKind"]);
        Assert.Equal(tier3Save.Properties["controllerName"], tier1Save.Properties["controllerName"]);
        Assert.Equal(tier3Save.Properties["actionName"], tier1Save.Properties["actionName"]);
        Assert.Equal(tier3Save.Properties["parameterName"], tier1Save.Properties["parameterName"]);
        Assert.Equal(tier3Save.Properties["propertyName"], tier1Save.Properties["propertyName"]);
        Assert.False(tier3Save.Properties.ContainsKey("ownerSymbolId"));
        Assert.Equal("contextual-support-only-missing-canonical-identity", tier1Save.Properties["tier3ReconciliationState"]);
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
