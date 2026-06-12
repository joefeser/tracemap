using TraceMap.Cli;

namespace TraceMap.Tests;

public sealed class CliTests
{
    [Fact]
    public async Task Help_for_scan_returns_usage()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await TraceMapCommand.RunAsync(["scan", "--help"], output, error);

        Assert.Equal(0, exitCode);
        Assert.Contains("tracemap scan --repo <path> --out <path>", output.ToString());
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public async Task Scan_against_temporary_directory_writes_required_outputs()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        var outputPath = Path.Combine(temp.Path, "out");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "TraceMap.Sample.sln"), "");
        Directory.CreateDirectory(Path.Combine(repo, "src"));
        File.WriteAllText(Path.Combine(repo, "src", "Sample.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Example.Package" Version="1.2.3" />
              </ItemGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(repo, "appsettings.json"), "{}");
        File.WriteAllText(Path.Combine(repo, "schema.sql"), "select 1;");

        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = await TraceMapCommand.RunAsync(["scan", "--repo", repo, "--out", outputPath], output, error);

        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(Path.Combine(outputPath, "scan-manifest.json")));
        Assert.True(File.Exists(Path.Combine(outputPath, "facts.ndjson")));
        Assert.True(File.Exists(Path.Combine(outputPath, "index.sqlite")));
        Assert.True(File.Exists(Path.Combine(outputPath, "report.md")));
        Assert.True(File.Exists(Path.Combine(outputPath, "logs", "analyzer.log")));
        var facts = await File.ReadAllTextAsync(Path.Combine(outputPath, "facts.ndjson"));
        Assert.Contains("\"factType\":\"PackageReferenced\"", facts);
        Assert.Contains("\"factType\":\"SqlFileDeclared\"", facts);
        Assert.Contains("\"analysisLevel\": \"Level1SemanticAnalysisReduced\"", await File.ReadAllTextAsync(Path.Combine(outputPath, "scan-manifest.json")));
        Assert.Equal(string.Empty, error.ToString());
    }

    [Fact]
    public async Task Scan_project_scope_limits_inventory_to_selected_project_tree()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        var outputPath = Path.Combine(temp.Path, "out");
        var keepProject = Path.Combine(repo, "src", "Keep");
        var skipProject = Path.Combine(repo, "src", "Skip");
        Directory.CreateDirectory(keepProject);
        Directory.CreateDirectory(skipProject);
        File.WriteAllText(Path.Combine(keepProject, "Keep.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(keepProject, "Keep.cs"), "namespace Keep; public sealed class Kept { }");
        File.WriteAllText(Path.Combine(skipProject, "Skip.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(skipProject, "Skip.cs"), "namespace Skip; public sealed class Skipped { }");

        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await TraceMapCommand.RunAsync(
            ["scan", "--repo", repo, "--out", outputPath, "--project", "src/Keep/Keep.csproj"],
            output,
            error);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, error.ToString());
        var facts = await File.ReadAllTextAsync(Path.Combine(outputPath, "facts.ndjson"));
        Assert.Contains("src/Keep/Keep.cs", facts);
        Assert.DoesNotContain("src/Skip/Skip.cs", facts);
        var manifest = await File.ReadAllTextAsync(Path.Combine(outputPath, "scan-manifest.json"));
        Assert.Contains("src/Keep/Keep.csproj", manifest);
        Assert.DoesNotContain("src/Skip/Skip.csproj", manifest);
    }

    [Fact]
    public async Task Scan_solution_scope_does_not_load_standalone_projects_for_semantic_facts()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        var outputPath = Path.Combine(temp.Path, "out");
        var keepProject = Path.Combine(repo, "src", "Keep");
        var skipProject = Path.Combine(repo, "src", "Skip");
        Directory.CreateDirectory(keepProject);
        Directory.CreateDirectory(skipProject);
        File.WriteAllText(Path.Combine(repo, "Scoped.sln"), """
            Microsoft Visual Studio Solution File, Format Version 12.00
            # Visual Studio Version 17
            VisualStudioVersion = 17.0.31903.59
            MinimumVisualStudioVersion = 10.0.40219.1
            Project("{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}") = "Keep", "src\Keep\Keep.csproj", "{11111111-1111-1111-1111-111111111111}"
            EndProject
            Global
                GlobalSection(SolutionConfigurationPlatforms) = preSolution
                    Debug|Any CPU = Debug|Any CPU
                EndGlobalSection
                GlobalSection(ProjectConfigurationPlatforms) = postSolution
                    {11111111-1111-1111-1111-111111111111}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
                    {11111111-1111-1111-1111-111111111111}.Debug|Any CPU.Build.0 = Debug|Any CPU
                EndGlobalSection
            EndGlobal
            """);
        File.WriteAllText(Path.Combine(keepProject, "Keep.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(keepProject, "Keep.cs"), "namespace Keep; public sealed class Kept { }");
        File.WriteAllText(Path.Combine(skipProject, "Skip.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(skipProject, "Skip.cs"), "namespace Skip; public sealed class Skipped { }");

        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await TraceMapCommand.RunAsync(
            ["scan", "--repo", repo, "--out", outputPath, "--solution", "Scoped.sln"],
            output,
            error);

        Assert.Equal(0, exitCode);
        Assert.Equal(string.Empty, error.ToString());
        var facts = await File.ReadAllTextAsync(Path.Combine(outputPath, "facts.ndjson"));
        Assert.Contains("global::Keep.Kept", facts);
        Assert.DoesNotContain("global::Skip.Skipped", facts);
    }

    [Fact]
    public async Task Flow_traces_parameter_forwarding_paths()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        var projectPath = Path.Combine(repo, "src", "FlowSample");
        var outputPath = Path.Combine(temp.Path, "out");
        var flowReportPath = Path.Combine(temp.Path, "flow.md");
        Directory.CreateDirectory(projectPath);
        File.WriteAllText(Path.Combine(projectPath, "FlowSample.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(projectPath, "Flow.cs"), """
            namespace FlowSample;

            public sealed class RequestDto { }

            public sealed class Controller
            {
                private readonly Service service = new();
                private RequestDto? cached;

                public void Post(RequestDto request)
                {
                    var outbound = request;
                    cached = outbound;
                    service.Save(cached);
                }
            }

            public sealed class Service
            {
                private readonly Gateway gateway = new();
                private RequestDto? relay;

                public void Save(RequestDto input)
                {
                    var next = input;
                    relay = next;
                    gateway.Send(relay);
                }
            }

            public sealed class Gateway
            {
                public void Send(RequestDto payload)
                {
                }
            }
            """);

        using var scanOutput = new StringWriter();
        using var scanError = new StringWriter();
        var scanExitCode = await TraceMapCommand.RunAsync(["scan", "--repo", repo, "--out", outputPath], scanOutput, scanError);
        Assert.Equal(0, scanExitCode);
        Assert.Equal(string.Empty, scanError.ToString());

        using var flowOutput = new StringWriter();
        using var flowError = new StringWriter();
        var flowExitCode = await TraceMapCommand.RunAsync(
            [
                "flow",
                "--index",
                Path.Combine(outputPath, "index.sqlite"),
                "--symbol",
                "request",
                "--out",
                flowReportPath,
                "--max-depth",
                "4"
            ],
            flowOutput,
            flowError);

        Assert.Equal(0, flowExitCode);
        Assert.Equal(string.Empty, flowError.ToString());
        Assert.Contains("Paths written: 1", flowOutput.ToString());

        var report = await File.ReadAllTextAsync(flowReportPath);
        Assert.Contains("TraceMap Flow Report", report);
        Assert.Contains("FlowSample.Controller.Post", report);
        Assert.Contains("FlowSample.Service.Save", report);
        Assert.Contains("FlowSample.Gateway.Send", report);
        Assert.Contains("csharp.semantic.parameterforwarding.v1", report);
        Assert.DoesNotContain("RequestDto { }", report);
    }

    [Fact]
    public async Task Flow_traces_unique_constructor_field_forwarding_path()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        var projectPath = Path.Combine(repo, "src", "FlowSample");
        var outputPath = Path.Combine(temp.Path, "out");
        var flowReportPath = Path.Combine(temp.Path, "flow.md");
        Directory.CreateDirectory(projectPath);
        File.WriteAllText(Path.Combine(projectPath, "FlowSample.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(projectPath, "Flow.cs"), """
            namespace FlowSample;

            public sealed class RequestDto { }

            public sealed class SnapshotController
            {
                private readonly RequestDto snapshot;

                public SnapshotController(RequestDto seed)
                {
                    snapshot = seed;
                }

                public void Replay(Gateway gateway)
                {
                    gateway.Send(snapshot);
                }
            }

            public sealed class Gateway
            {
                public void Send(RequestDto payload)
                {
                }
            }
            """);

        using var scanOutput = new StringWriter();
        using var scanError = new StringWriter();
        var scanExitCode = await TraceMapCommand.RunAsync(["scan", "--repo", repo, "--out", outputPath], scanOutput, scanError);
        Assert.Equal(0, scanExitCode);
        Assert.Equal(string.Empty, scanError.ToString());

        using var flowOutput = new StringWriter();
        using var flowError = new StringWriter();
        var flowExitCode = await TraceMapCommand.RunAsync(
            [
                "flow",
                "--index",
                Path.Combine(outputPath, "index.sqlite"),
                "--symbol",
                "seed",
                "--out",
                flowReportPath,
                "--max-depth",
                "4"
            ],
            flowOutput,
            flowError);

        Assert.Equal(0, flowExitCode);
        Assert.Equal(string.Empty, flowError.ToString());
        Assert.Contains("Paths written: 1", flowOutput.ToString());

        var report = await File.ReadAllTextAsync(flowReportPath);
        Assert.Contains("SnapshotController.SnapshotController", report);
        Assert.Contains("Gateway.Send", report);
        Assert.Contains("RequestDto seed", report);
        Assert.Contains("src/FlowSample/Flow.cs:16-16", report);
    }

    [Fact]
    public async Task Relate_traces_symbol_relationship_paths()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        var projectPath = Path.Combine(repo, "src", "RelateSample");
        var outputPath = Path.Combine(temp.Path, "out");
        var relationshipReportPath = Path.Combine(temp.Path, "relationships.md");
        Directory.CreateDirectory(projectPath);
        File.WriteAllText(Path.Combine(projectPath, "RelateSample.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(projectPath, "Relationships.cs"), """
            namespace RelateSample;

            public interface IRequestHandler
            {
                void Handle();
            }

            public abstract class HandlerBase
            {
                public virtual void Handle()
                {
                }
            }

            public sealed class ConcreteHandler : HandlerBase, IRequestHandler
            {
                public override void Handle()
                {
                }
            }
            """);

        using var scanOutput = new StringWriter();
        using var scanError = new StringWriter();
        var scanExitCode = await TraceMapCommand.RunAsync(["scan", "--repo", repo, "--out", outputPath], scanOutput, scanError);
        Assert.Equal(0, scanExitCode);
        Assert.Equal(string.Empty, scanError.ToString());

        using var relateOutput = new StringWriter();
        using var relateError = new StringWriter();
        var relateExitCode = await TraceMapCommand.RunAsync(
            [
                "relate",
                "--index",
                Path.Combine(outputPath, "index.sqlite"),
                "--symbol",
                "ConcreteHandler",
                "--out",
                relationshipReportPath,
                "--direction",
                "outgoing",
                "--max-depth",
                "3"
            ],
            relateOutput,
            relateError);

        Assert.Equal(0, relateExitCode);
        Assert.Equal(string.Empty, relateError.ToString());
        Assert.Contains("Symbol relationships indexed:", relateOutput.ToString());
        var report = await File.ReadAllTextAsync(relationshipReportPath);
        Assert.Contains("TraceMap Relationship Report", report);
        Assert.Contains("ConcreteHandler", report);
        Assert.Contains("InheritsFrom", report);
        Assert.Contains("ImplementsInterface", report);
        Assert.Contains("csharp.semantic.symbolrelationship.v1", report);
    }

    [Fact]
    public async Task Relate_both_direction_advances_through_incoming_edges()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        var projectPath = Path.Combine(repo, "src", "BothRelateSample");
        var outputPath = Path.Combine(temp.Path, "out");
        var relationshipReportPath = Path.Combine(temp.Path, "relationships.md");
        Directory.CreateDirectory(projectPath);
        File.WriteAllText(Path.Combine(projectPath, "BothRelateSample.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(projectPath, "Relationships.cs"), """
            namespace BothRelateSample;

            public interface IBase
            {
            }

            public interface IDerived : IBase
            {
            }

            public sealed class Impl : IDerived
            {
            }
            """);

        using var scanOutput = new StringWriter();
        using var scanError = new StringWriter();
        var scanExitCode = await TraceMapCommand.RunAsync(["scan", "--repo", repo, "--out", outputPath], scanOutput, scanError);
        Assert.Equal(0, scanExitCode);
        Assert.Equal(string.Empty, scanError.ToString());

        using var relateOutput = new StringWriter();
        using var relateError = new StringWriter();
        var relateExitCode = await TraceMapCommand.RunAsync(
            [
                "relate",
                "--index",
                Path.Combine(outputPath, "index.sqlite"),
                "--symbol",
                "IBase",
                "--out",
                relationshipReportPath,
                "--direction",
                "both",
                "--max-depth",
                "3"
            ],
            relateOutput,
            relateError);

        Assert.Equal(0, relateExitCode);
        Assert.Equal(string.Empty, relateError.ToString());
        var report = await File.ReadAllTextAsync(relationshipReportPath);
        Assert.Contains("BothRelateSample.IDerived", report);
        Assert.Contains("BothRelateSample.Impl", report);
    }
}
