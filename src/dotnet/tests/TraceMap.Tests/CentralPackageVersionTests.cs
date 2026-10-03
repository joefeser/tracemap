using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class CentralPackageVersionTests
{
    private const string CentralProps = """
        <Project>
          <PropertyGroup>
            <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
          </PropertyGroup>
          <ItemGroup>
            <PackageVersion Include="Contoso.Core" Version="1.0.0" />
            <PackageVersion Include="Serilog">
              <Version>3.1.1</Version>
            </PackageVersion>
            <PackageVersion Update="Newtonsoft.Json" Version="13.0.3" />
            <PackageVersion Include="No.Version.Pin" />
            <PackageVersion Include="../escapes/../id" Version="1.0.0" />
          </ItemGroup>
        </Project>
        """;

    [Fact]
    public void ReadCentralPackageVersions_extracts_pins_from_props_files_in_attribute_and_element_form()
    {
        using var temp = new TempDirectory();
        var repo = temp.Path;
        Directory.CreateDirectory(Path.Combine(repo, "src"));
        File.WriteAllText(Path.Combine(repo, "src", "Directory.Packages.props"), CentralProps);

        var result = ProjectFileReader.ReadCentralPackageVersions(repo, [new FileInventoryItem("src/Directory.Packages.props", "MSBuildProps", CentralProps.Length)]);

        Assert.Equal(3, result.Count);
        // sorted by (path, package): Contoso.Core, Newtonsoft.Json (Update attr counts), Serilog (child element)
        Assert.Equal("Contoso.Core", result[0].PackageName);
        Assert.Equal("1.0.0", result[0].Version);
        Assert.Equal("src/Directory.Packages.props", result[0].PropsPath);
        Assert.True(result[0].Line > 0);
        Assert.Equal("Newtonsoft.Json", result[1].PackageName);
        Assert.Equal("13.0.3", result[1].Version);
        Assert.Equal("Serilog", result[2].PackageName);
        Assert.Equal("3.1.1", result[2].Version);
    }

    [Fact]
    public void ReadCentralPackageVersions_skips_unsafe_ids_and_versionless_pins()
    {
        using var temp = new TempDirectory();
        var repo = temp.Path;
        File.WriteAllText(Path.Combine(repo, "Directory.Packages.props"), CentralProps);

        var result = ProjectFileReader.ReadCentralPackageVersions(repo, [new FileInventoryItem("Directory.Packages.props", "MSBuildProps", CentralProps.Length)]);

        Assert.DoesNotContain(result, pin => pin.PackageName == "No.Version.Pin");
        Assert.DoesNotContain(result, pin => pin.PackageName.Contains("..", StringComparison.Ordinal)); // unsafe identity skipped, never projected
    }

    [Fact]
    public void ReadCentralPackageVersions_ignores_non_props_inventory_kinds()
    {
        using var temp = new TempDirectory();
        var repo = temp.Path;
        File.WriteAllText(Path.Combine(repo, "Directory.Packages.props"), CentralProps);

        var result = ProjectFileReader.ReadCentralPackageVersions(repo, [new FileInventoryItem("Directory.Packages.props", "MSBuildTargets", CentralProps.Length)]);

        Assert.Empty(result);
    }

    [Fact]
    public void ReadPackageReferences_captures_version_override_in_attribute_and_element_form()
    {
        const string project = """
            <Project Sdk="Microsoft.NET.Sdk">
              <ItemGroup>
                <PackageReference Include="Contoso.Core" VersionOverride="0.9.0" />
                <PackageReference Include="Serilog" />
                <PackageReference Include="Other.Lib">
                  <VersionOverride>2.0.0</VersionOverride>
                </PackageReference>
              </ItemGroup>
            </Project>
            """;
        using var temp = new TempDirectory();
        var repo = temp.Path;
        Directory.CreateDirectory(Path.Combine(repo, "src", "Worker"));
        File.WriteAllText(Path.Combine(repo, "src", "Worker", "Worker.csproj"), project);

        var result = ProjectFileReader.ReadPackageReferences(repo, [new FileInventoryItem("src/Worker/Worker.csproj", "Project", project.Length)]);

        Assert.Equal(3, result.Count);
        Assert.Null(result.First(r => r.PackageName == "Contoso.Core").Version); // CPM: no in-file version
        Assert.Equal("0.9.0", result.First(r => r.PackageName == "Contoso.Core").VersionOverride);
        Assert.Null(result.First(r => r.PackageName == "Serilog").VersionOverride);
        Assert.Equal("2.0.0", result.First(r => r.PackageName == "Other.Lib").VersionOverride); // child-element form
    }
}
