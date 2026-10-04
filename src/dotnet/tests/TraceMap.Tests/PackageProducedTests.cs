using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class PackageProducedTests
{
    [Fact]
    public void ReadProducedPackages_extracts_packable_projects_with_versions()
    {
        const string project = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <PackageId>Contoso.Core</PackageId>
                <Version>1.0.0</Version>
              </PropertyGroup>
            </Project>
            """;
        using var temp = new TempDirectory();
        Directory.CreateDirectory(Path.Combine(temp.Path, "src", "Lib"));
        File.WriteAllText(Path.Combine(temp.Path, "src", "Lib", "Lib.csproj"), project);

        var result = ProjectFileReader.ReadProducedPackages(temp.Path, [new FileInventoryItem("src/Lib/Lib.csproj", "Project", project.Length)]);

        var produced = Assert.Single(result);
        Assert.Equal("Contoso.Core", produced.PackageId);
        Assert.Equal("1.0.0", produced.Version);
        Assert.True(produced.ExplicitPackageId);
        Assert.Equal("src/Lib/Lib.csproj", produced.ProjectPath);
    }

    [Fact]
    public void ReadProducedPackages_respects_packable_false_and_version_fallbacks()
    {
        const string packable = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <PackageId>Should.Not.Appear</PackageId>
                <IsPackable>false</IsPackable>
              </PropertyGroup>
            </Project>
            """;
        const string pinned = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <PackageId>Contoso.Pinned</PackageId>
                <PackageVersion>2.5.0</PackageVersion>
              </PropertyGroup>
            </Project>
            """;
        const string unsafeVersion = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <PackageId>Contoso.Unsafe</PackageId>
                <Version>$(VersionSuffix)-rc1</Version>
              </PropertyGroup>
            </Project>
            """;
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "a.csproj"), packable);
        File.WriteAllText(Path.Combine(temp.Path, "b.csproj"), pinned);
        File.WriteAllText(Path.Combine(temp.Path, "c.csproj"), unsafeVersion);

        var result = ProjectFileReader.ReadProducedPackages(temp.Path,
        [
            new FileInventoryItem("a.csproj", "Project", packable.Length),
            new FileInventoryItem("b.csproj", "Project", pinned.Length),
            new FileInventoryItem("c.csproj", "Project", unsafeVersion.Length),
        ]);

        Assert.DoesNotContain(result, p => p.PackageId == "Should.Not.Appear"); // IsPackable=false
        var pinnedProduced = Assert.Single(result, p => p.PackageId == "Contoso.Pinned");
        Assert.Equal("2.5.0", pinnedProduced.Version); // PackageVersion fallback
        var unsafeProduced = Assert.Single(result, p => p.PackageId == "Contoso.Unsafe");
        Assert.Null(unsafeProduced.Version); // unsafe version = unevidenced, never projected
    }

    [Fact]
    public void ReadProducedPackages_assemblyname_fallback_is_marked()
    {
        const string project = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <AssemblyName>Contoso.Fallback</AssemblyName>
              </PropertyGroup>
            </Project>
            """;
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "lib.csproj"), project);

        var produced = Assert.Single(ProjectFileReader.ReadProducedPackages(temp.Path, [new FileInventoryItem("lib.csproj", "Project", project.Length)]));

        Assert.Equal("Contoso.Fallback", produced.PackageId);
        Assert.False(produced.ExplicitPackageId); // weaker claim, visible to consumers
        Assert.Null(produced.Version);
    }

    [Fact]
    public void ReadProducedPackages_skips_unsafe_ids_entirely()
    {
        const string project = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <PackageId>../escapes/../id</PackageId>
              </PropertyGroup>
            </Project>
            """;
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "lib.csproj"), project);

        Assert.Empty(ProjectFileReader.ReadProducedPackages(temp.Path, [new FileInventoryItem("lib.csproj", "Project", project.Length)]));
    }
}
