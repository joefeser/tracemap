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
        Assert.Equal(PackageIdProvenances.Declared, produced.PackageIdProvenance); // tier 1: a literal declaration
        Assert.Equal(PackageIdSources.PackageId, produced.PackageIdSource);
        Assert.Null(produced.PackSignals); // signals only justify the derived-default tier
        Assert.Empty(produced.PackDependencies);
        Assert.Equal(0, produced.UnresolvedPackDependencyCount);
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
        Assert.Equal(PackageIdSources.AssemblyName, produced.PackageIdSource); // the chain link that supplied the id
        Assert.Equal(PackageIdProvenances.Declared, produced.PackageIdProvenance); // ...whose value was a literal
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

    [Fact]
    public void ReadProducedPackages_last_ispackable_wins_and_properties_only()
    {
        const string project = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <IsPackable>false</IsPackable>
                <IsPackable>true</IsPackable>
                <PackageId>Contoso.Order</PackageId>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="X" Version="9.9.9" />
                <Version>0.0.0-should-not-count</Version>
              </ItemGroup>
            </Project>
            """;
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "lib.csproj"), project);

        var produced = Assert.Single(ProjectFileReader.ReadProducedPackages(temp.Path, [new FileInventoryItem("lib.csproj", "Project", project.Length)]));

        Assert.Equal("Contoso.Order", produced.PackageId); // last IsPackable wins -> emitted
        Assert.Null(produced.Version); // ItemGroup <Version> is not a property — never a producer version
    }

    [Fact]
    public void ReadProducedPackages_packageversion_outranks_version_nuget_semantics()
    {
        const string project = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <PackageVersion>2.5.0</PackageVersion>
                <PackageId>Contoso.Pin</PackageId>
                <Version>3.0.0</Version>
              </PropertyGroup>
            </Project>
            """;
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "lib.csproj"), project);

        var produced = Assert.Single(ProjectFileReader.ReadProducedPackages(temp.Path, [new FileInventoryItem("lib.csproj", "Project", project.Length)]));

        Assert.Equal("2.5.0", produced.Version); // PackageVersion outranks Version (NuGet pack semantics), regardless of XML order
        Assert.True(produced.Line >= produced.SpanStart); // span covers identity..version
    }

    [Fact]
    public void ReadProducedPackages_legacy_projects_never_fallback_to_assemblyname()
    {
        const string legacy = """
            <Project ToolsVersion="4.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
              <PropertyGroup>
                <AssemblyName>fluentjdf</AssemblyName>
              </PropertyGroup>
            </Project>
            """;
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "legacy.csproj"), legacy);

        Assert.Empty(ProjectFileReader.ReadProducedPackages(temp.Path, [new FileInventoryItem("legacy.csproj", "Project", legacy.Length)]));
    }

    [Fact]
    public void ReadProducedPackages_packageid_overrides_earlier_assemblyname()
    {
        const string project = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <AssemblyName>some.build.name</AssemblyName>
                <PackageId>Contoso.Real</PackageId>
              </PropertyGroup>
            </Project>
            """;
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "lib.csproj"), project);

        var produced = Assert.Single(ProjectFileReader.ReadProducedPackages(temp.Path, [new FileInventoryItem("lib.csproj", "Project", project.Length)]));

        Assert.Equal("Contoso.Real", produced.PackageId);
        Assert.True(produced.ExplicitPackageId); // AssemblyName seen FIRST must not win
    }

    [Fact]
    public void ReadProducedPackages_last_assignment_wins_for_all_properties()
    {
        const string project = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <PackageId>Contoso.Old</PackageId>
                <PackageId>Contoso.New</PackageId>
              </PropertyGroup>
            </Project>
            """;
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "lib.csproj"), project);

        var produced = Assert.Single(ProjectFileReader.ReadProducedPackages(temp.Path, [new FileInventoryItem("lib.csproj", "Project", project.Length)]));

        Assert.Equal("Contoso.New", produced.PackageId); // MSBuild: later redefinition replaces
    }

    // ---- SDK-default identity tiers (project-file/0.4.1) ------------------------------------------

    [Fact]
    public void ReadProducedPackages_interpolated_assemblyname_resolves_msbuildprojectname_from_the_file_itself()
    {
        const string project = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <AssemblyName>Prefix.$(MSBuildProjectName)</AssemblyName>
                <Authors>Fixture Authors</Authors>
              </PropertyGroup>
            </Project>
            """;
        using var temp = new TempDirectory();
        Write(temp.Path, "src/Fixture.Alpha/Fixture.Alpha.csproj", project);

        var produced = Assert.Single(ProjectFileReader.ReadProducedPackages(temp.Path, [Project("src/Fixture.Alpha/Fixture.Alpha.csproj", project)]));

        Assert.Equal("Prefix.Fixture.Alpha", produced.PackageId); // tier 2: the one token the file defines, evaluated
        Assert.False(produced.ExplicitPackageId); // never promoted to a literal declaration (the CI lane keeps its gap)
        Assert.Equal(PackageIdSources.AssemblyName, produced.PackageIdSource);
        Assert.Equal(PackageIdProvenances.Interpolated, produced.PackageIdProvenance);
        Assert.Equal("Authors", produced.PackSignals); // the pack-intent gate names what carried it (naming alone is not packing)
        Assert.Null(produced.Version); // unevidenced: versions here are computed at build time
        Assert.Equal(3, produced.SpanStart);
        Assert.Equal(4, produced.Line); // identity and required Authors evidence
    }

    [Fact] // live 2026-10-21: AssemblyName=Prefix.$(MSBuildProjectName) on projects consumed in-solution
    public void ReadProducedPackages_interpolated_assemblyname_without_pack_intent_is_refused()
    {
        const string project = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <AssemblyName>Alt.$(MSBuildProjectName)</AssemblyName>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """;
        using var temp = new TempDirectory();
        Write(temp.Path, "src/Svc/Svc.Domain.csproj", project);

        Assert.Empty(ProjectFileReader.ReadProducedPackages(temp.Path, [Project("src/Svc/Svc.Domain.csproj", project)]));
    }

    [Fact]
    public void ReadProducedPackages_interpolated_assemblyname_with_literal_ispackable_produces()
    {
        const string project = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <AssemblyName>Alt.$(MSBuildProjectName)</AssemblyName>
                <IsPackable>true</IsPackable>
              </PropertyGroup>
            </Project>
            """;
        using var temp = new TempDirectory();
        Write(temp.Path, "src/Svc/Svc.Domain.csproj", project);

        var produced = Assert.Single(ProjectFileReader.ReadProducedPackages(temp.Path, [Project("src/Svc/Svc.Domain.csproj", project)]));
        Assert.Equal("Alt.Svc.Domain", produced.PackageId);
        Assert.Equal(PackageIdProvenances.Interpolated, produced.PackageIdProvenance);
        Assert.Equal("IsPackable", produced.PackSignals);
    }

    [Fact]
    public void ReadProducedPackages_interpolated_packageid_is_resolved_but_not_explicit()
    {
        const string project = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <PackageId>Fixture.$(msbuildprojectname)</PackageId>
              </PropertyGroup>
            </Project>
            """;
        using var temp = new TempDirectory();
        Write(temp.Path, "src/Beta/Beta.csproj", project);

        var produced = Assert.Single(ProjectFileReader.ReadProducedPackages(temp.Path, [Project("src/Beta/Beta.csproj", project)]));

        Assert.Equal("Fixture.Beta", produced.PackageId); // MSBuild property names are case-insensitive
        Assert.False(produced.ExplicitPackageId); // the value was computed by us, not read literally
        Assert.Equal(PackageIdSources.PackageId, produced.PackageIdSource);
        Assert.Equal(PackageIdProvenances.Interpolated, produced.PackageIdProvenance);
    }

    [Theory]
    [InlineData("<AssemblyName>$(OtherProperty)</AssemblyName>")]
    [InlineData("<AssemblyName>Prefix.$(MSBuildProjectName).$(Suffix)</AssemblyName>")]
    [InlineData("<AssemblyName>$(MSBuildProjectName.Replace('.', '-'))</AssemblyName>")]
    [InlineData("<AssemblyName>$([System.String]::Copy('x'))</AssemblyName>")]
    [InlineData("<AssemblyName>@(Compile)</AssemblyName>")]
    [InlineData("<AssemblyName>%(Filename)</AssemblyName>")]
    [InlineData("<PackageId>$(OtherProperty)</PackageId>")]
    [InlineData("<PackageId>Fixture.$(MSBuildProjectName.ToLower())</PackageId>")]
    public void ReadProducedPackages_any_other_token_is_unevidenced_never_guessed(string identity)
    {
        // Pack metadata is present on purpose: an unresolvable DECLARED identity must never fall
        // through to the derived-default tier — the real id is whatever the token evaluates to.
        var project = $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                {identity}
                <Authors>Fixture Authors</Authors>
                <Description>Fixture description</Description>
                <PackageTags>fixture</PackageTags>
              </PropertyGroup>
            </Project>
            """;
        using var temp = new TempDirectory();
        Write(temp.Path, "src/Gamma/Gamma.csproj", project);

        Assert.Empty(ProjectFileReader.ReadProducedPackages(temp.Path, [Project("src/Gamma/Gamma.csproj", project)]));
    }

    [Fact]
    public void ReadProducedPackages_sdk_pack_metadata_without_identity_derives_the_file_name_as_a_distinct_tier()
    {
        const string project = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <Authors>Fixture Authors</Authors>
                <Company>Fixture Company</Company>
                <Product>Fixture Product</Product>
                <Description>Ships as a package with no identity properties at all.</Description>
                <PackageTags>fixture;derived</PackageTags>
                <Copyright>Fixture</Copyright>
                <RootNamespace>Fixture.$(MSBuildProjectName.Replace('.Delta', ''))</RootNamespace>
              </PropertyGroup>
            </Project>
            """;
        using var temp = new TempDirectory();
        Write(temp.Path, "lib/Fixture.Delta/Fixture.Delta.csproj", project);

        var produced = Assert.Single(ProjectFileReader.ReadProducedPackages(temp.Path, [Project("lib/Fixture.Delta/Fixture.Delta.csproj", project)]));

        Assert.Equal("Fixture.Delta", produced.PackageId); // PackageId <- AssemblyName <- MSBuildProjectName: the file name
        Assert.False(produced.ExplicitPackageId);
        Assert.Equal(PackageIdSources.MSBuildProjectName, produced.PackageIdSource);
        Assert.Equal(PackageIdProvenances.DerivedDefault, produced.PackageIdProvenance); // visibly distinct from declared
        Assert.Equal("Authors,Description,PackageTags,Product", produced.PackSignals); // Company/Copyright/RootNamespace are not pack signals
        Assert.Null(produced.Version); // unevidenced (null), not an error
        Assert.Equal(4, produced.SpanStart); // no identity line: the span covers the signals (Authors..PackageTags)
        Assert.Equal(8, produced.Line);
    }

    [Fact]
    public void ReadProducedPackages_multi_package_solution_records_pack_dependencies_and_never_package_references()
    {
        // Serilog-style sink topology: one solution, a core library and two sinks that ProjectReference it,
        // every project shipping its own package on SDK defaults alone.
        const string core = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>netstandard2.0</TargetFramework>
                <Authors>Fixture Authors</Authors>
                <Description>Core abstractions.</Description>
                <PackageTags>fixture;core</PackageTags>
              </PropertyGroup>
            </Project>
            """;
        const string sinkAlpha = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>netstandard2.0</TargetFramework>
                <Authors>Fixture Authors</Authors>
                <Description>Alpha sink.</Description>
              </PropertyGroup>
              <ItemGroup>
                <ProjectReference Include="..\Fixture.Core\Fixture.Core.csproj" />
              </ItemGroup>
            </Project>
            """;
        const string sinkBeta = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>netstandard2.0</TargetFramework>
                <Product>Fixture Beta Sink</Product>
              </PropertyGroup>
              <ItemGroup>
                <ProjectReference Include="../Fixture.Core/Fixture.Core.csproj" />
                <ProjectReference Include="../Fixture.Core/Fixture.Core.csproj" />
              </ItemGroup>
            </Project>
            """;
        using var temp = new TempDirectory();
        Write(temp.Path, "Fixture.sln", Solution);
        Write(temp.Path, "src/Fixture.Core/Fixture.Core.csproj", core);
        Write(temp.Path, "src/Fixture.Sink.Alpha/Fixture.Sink.Alpha.csproj", sinkAlpha);
        Write(temp.Path, "src/Fixture.Sink.Beta/Fixture.Sink.Beta.csproj", sinkBeta);
        FileInventoryItem[] inventory =
        [
            new("Fixture.sln", "Solution", Solution.Length),
            Project("src/Fixture.Core/Fixture.Core.csproj", core),
            Project("src/Fixture.Sink.Alpha/Fixture.Sink.Alpha.csproj", sinkAlpha),
            Project("src/Fixture.Sink.Beta/Fixture.Sink.Beta.csproj", sinkBeta),
        ];

        var produced = ProjectFileReader.ReadProducedPackages(temp.Path, inventory);

        Assert.Equal(["Fixture.Core", "Fixture.Sink.Alpha", "Fixture.Sink.Beta"], produced.Select(p => p.PackageId).ToArray());
        Assert.All(produced, p => Assert.Equal(PackageIdProvenances.DerivedDefault, p.PackageIdProvenance));
        Assert.All(produced, p => Assert.Null(p.Version));
        var coreProduced = produced.Single(p => p.PackageId == "Fixture.Core");
        Assert.Empty(coreProduced.PackDependencies);
        foreach (var sink in produced.Where(p => p.PackageId != "Fixture.Core"))
        {
            Assert.Equal(["Fixture.Core"], sink.PackDependencies); // NuGet pack turns the ProjectReference into a dependency on core's id
            Assert.Equal(0, sink.UnresolvedPackDependencyCount); // a repeated reference collapses, never counts as unresolved
        }

        var repeated = produced.Single(p => p.PackageId == "Fixture.Sink.Beta").DependencyEvidence;
        Assert.Equal(new[] { 7, 8 }, repeated.Select(edge => edge.Line));
        Assert.All(repeated, edge => Assert.Equal("src/Fixture.Core/Fixture.Core.csproj", edge.TargetProjectPath));

        // In-repo ProjectReferences are consumption of a sibling project, never NuGet package consumption.
        Assert.Empty(ProjectFileReader.ReadPackageReferences(temp.Path, inventory));
    }

    [Theory]
    [InlineData("""
        <Project ToolsVersion="4.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
          <PropertyGroup>
            <Authors>Fixture Authors</Authors>
            <Description>Legacy project with pack metadata but no identity.</Description>
            <PackageTags>fixture</PackageTags>
          </PropertyGroup>
        </Project>
        """)]
    [InlineData("""
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
            <OutputType>Exe</OutputType>
          </PropertyGroup>
        </Project>
        """)]
    [InlineData("""
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <Company>Fixture Company</Company>
            <Copyright>Fixture</Copyright>
            <RootNamespace>Fixture.Root</RootNamespace>
          </PropertyGroup>
        </Project>
        """)]
    [InlineData("""
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <Description>Explicitly not packable.</Description>
            <IsPackable>false</IsPackable>
          </PropertyGroup>
        </Project>
        """)]
    [InlineData("""
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <Description>A test project: the test SDK defaults IsPackable to false.</Description>
          </PropertyGroup>
          <ItemGroup>
            <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.0.0" />
          </ItemGroup>
        </Project>
        """)]
    public void ReadProducedPackages_refuses_legacy_zero_signal_and_unpackable_projects(string project)
    {
        using var temp = new TempDirectory();
        Write(temp.Path, "src/Fixture.Epsilon/Fixture.Epsilon.csproj", project);

        Assert.Empty(ProjectFileReader.ReadProducedPackages(temp.Path, [Project("src/Fixture.Epsilon/Fixture.Epsilon.csproj", project)]));
    }

    [Fact]
    public void ReadProducedPackages_literal_ispackable_true_is_a_pack_signal_that_overrides_the_test_sdk_default()
    {
        const string packableOnly = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <IsPackable>true</IsPackable>
              </PropertyGroup>
            </Project>
            """;
        const string packableTestProject = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <IsPackable>True</IsPackable>
                <Description>A test helper package.</Description>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.0.0" />
              </ItemGroup>
            </Project>
            """;
        const string templatedPackable = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <IsPackable>$(ShipIt)</IsPackable>
              </PropertyGroup>
            </Project>
            """;
        using var temp = new TempDirectory();
        Write(temp.Path, "a/Fixture.A.csproj", packableOnly);
        Write(temp.Path, "b/Fixture.B.csproj", packableTestProject);
        Write(temp.Path, "c/Fixture.C.csproj", templatedPackable);

        var produced = ProjectFileReader.ReadProducedPackages(temp.Path,
        [
            Project("a/Fixture.A.csproj", packableOnly),
            Project("b/Fixture.B.csproj", packableTestProject),
            Project("c/Fixture.C.csproj", templatedPackable),
        ]);

        var a = Assert.Single(produced, p => p.PackageId == "Fixture.A");
        Assert.Equal("IsPackable", a.PackSignals); // the most explicit intent-to-pack statement is itself a signal
        var b = Assert.Single(produced, p => p.PackageId == "Fixture.B");
        Assert.Equal("Description,IsPackable", b.PackSignals);
        Assert.DoesNotContain(produced, p => p.PackageId == "Fixture.C"); // a templated IsPackable proves nothing
    }

    [Fact]
    public void ReadProducedPackages_pack_dependencies_honor_private_assets_and_count_what_cannot_be_resolved()
    {
        const string core = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <PackageId>Fixture.Declared.Core</PackageId>
              </PropertyGroup>
            </Project>
            """;
        const string tooling = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <AssemblyName>Fixture.Tooling</AssemblyName>
              </PropertyGroup>
            </Project>
            """;
        const string unpackable = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <IsPackable>false</IsPackable>
              </PropertyGroup>
            </Project>
            """;
        const string consumer = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <Description>Depends on a mix of resolvable and unresolvable references.</Description>
              </PropertyGroup>
              <ItemGroup>
                <ProjectReference Include="..\Core\Core.csproj" />
                <ProjectReference Include="..\Tooling\Tooling.csproj" PrivateAssets="all" />
                <ProjectReference Include="..\Unpackable\Unpackable.csproj" />
                <ProjectReference Include="..\Missing\Missing.csproj" />
                <ProjectReference Include="$(RepoRoot)\Shared\Shared.csproj" />
                <ProjectReference Include="..\..\..\Outside\Outside.csproj" />
                <ProjectReference Include="..\Core\Core.csproj">
                  <PrivateAssets>compile;All</PrivateAssets>
                </ProjectReference>
              </ItemGroup>
            </Project>
            """;
        using var temp = new TempDirectory();
        Write(temp.Path, "src/Core/Core.csproj", core);
        Write(temp.Path, "src/Tooling/Tooling.csproj", tooling);
        Write(temp.Path, "src/Unpackable/Unpackable.csproj", unpackable);
        Write(temp.Path, "src/Consumer/Consumer.csproj", consumer);

        var produced = ProjectFileReader.ReadProducedPackages(temp.Path,
        [
            Project("src/Core/Core.csproj", core),
            Project("src/Tooling/Tooling.csproj", tooling),
            Project("src/Unpackable/Unpackable.csproj", unpackable),
            Project("src/Consumer/Consumer.csproj", consumer),
        ]);

        var consumerProduced = Assert.Single(produced, p => p.PackageId == "Consumer");
        Assert.Equal(["Fixture.Declared.Core"], consumerProduced.PackDependencies); // the referenced project's OWN tier supplies the id
        // Unpackable (no produced package), Missing (not in the repo), $(RepoRoot) (token), ..\..\..\Outside (escapes).
        // PrivateAssets=all references are not pack dependencies at all, so they are neither listed nor counted.
        Assert.Equal(4, consumerProduced.UnresolvedPackDependencyCount);
    }

    [Fact]
    public void Scan_materializes_one_provenance_tier_per_fact_with_additive_properties()
    {
        const string declared = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <PackageId>Fixture.Declared</PackageId>
                <Version>1.2.3</Version>
              </PropertyGroup>
            </Project>
            """;
        const string interpolated = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <AssemblyName>Prefix.$(MSBuildProjectName)</AssemblyName>
                <Authors>Fixture Authors</Authors>
              </PropertyGroup>
            </Project>
            """;
        const string derived = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <Authors>Fixture Authors</Authors>
                <Description>Derived default identity.</Description>
              </PropertyGroup>
              <ItemGroup>
                <ProjectReference Include="..\Declared\Declared.csproj" />
                <ProjectReference Include="..\Interpolated\Interpolated.csproj" />
              </ItemGroup>
            </Project>
            """;
        using var temp = new TempDirectory();
        Write(temp.Path, "Fixture.sln", Solution);
        Write(temp.Path, "src/Declared/Declared.csproj", declared);
        Write(temp.Path, "src/Interpolated/Interpolated.csproj", interpolated);
        Write(temp.Path, "src/Fixture.Derived/Fixture.Derived.csproj", derived);

        var scan = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, ".tracemap"), SolutionPaths: ["Fixture.sln"]));

        var facts = scan.Facts
            .Where(fact => fact.FactType == FactTypes.PackageProduced)
            .ToDictionary(fact => fact.TargetSymbol!, StringComparer.Ordinal);
        Assert.Equal(["Fixture.Declared", "Fixture.Derived", "Prefix.Interpolated"], facts.Keys.Order(StringComparer.Ordinal).ToArray());
        Assert.All(facts.Values, fact =>
        {
            Assert.Equal(ScannerVersions.ProjectFileExtractor, fact.Evidence.ExtractorVersion);
            Assert.Equal("project-file/0.4.1", fact.Evidence.ExtractorVersion); // policy change: no cache may silently resume across it
            Assert.Equal("build-file", fact.Properties["sourceKind"]);
            Assert.Equal(EvidenceTiers.Tier2Structural, fact.EvidenceTier);
        });

        var declaredFact = facts["Fixture.Declared"];
        Assert.Equal("declared", declaredFact.Properties["packageIdProvenance"]);
        Assert.False(declaredFact.Properties.ContainsKey("packageIdSource")); // existing contract: absent means a literal PackageId
        Assert.False(declaredFact.Properties.ContainsKey("packSignals"));
        Assert.Equal("1.2.3", declaredFact.Properties["version"]);

        var interpolatedFact = facts["Prefix.Interpolated"];
        Assert.Equal("interpolated", interpolatedFact.Properties["packageIdProvenance"]);
        Assert.Equal("AssemblyName", interpolatedFact.Properties["packageIdSource"]);
        Assert.Equal(string.Empty, interpolatedFact.Properties["version"]); // unevidenced, present, never guessed

        var derivedFact = facts["Fixture.Derived"];
        Assert.Equal("derived-default", derivedFact.Properties["packageIdProvenance"]);
        Assert.Equal("MSBuildProjectName", derivedFact.Properties["packageIdSource"]);
        Assert.Equal("Authors,Description", derivedFact.Properties["packSignals"]);
        Assert.Equal("Fixture.Declared,Prefix.Interpolated", derivedFact.Properties["packDependencies"]);
        Assert.Equal("2", derivedFact.Properties["packDependencyCount"]);
        Assert.False(derivedFact.Properties.ContainsKey("packDependencyUnresolvedCount"));
        Assert.Equal(string.Empty, derivedFact.Properties["version"]);
        Assert.Equal("src/Fixture.Derived/Fixture.Derived.csproj", derivedFact.Evidence.FilePath);
        Assert.Equal(4, derivedFact.Evidence.StartLine); // Authors
        Assert.Equal(5, derivedFact.Evidence.EndLine); // Description

        // ProjectReferences never surface as package consumption.
        Assert.DoesNotContain(scan.Facts, fact => fact.FactType == FactTypes.PackageReferenced);
    }

    [Theory]
    [InlineData("Authors")]
    [InlineData("Description")]
    [InlineData("Product")]
    [InlineData("PackageTags")]
    [InlineData("IsPackable")]
    public void Signal_matrix_cites_both_sides_of_identity_and_clears_last_assignment(string signal)
    {
        using var temp = new TempDirectory();
        foreach (var before in new[] { true, false })
        foreach (var interpolated in new[] { true, false })
        foreach (var cleared in new[] { true, false })
        {
            var assignment = $"<{signal}>true</{signal}>";
            var identity = interpolated ? "<aSsemblyName>Prefix.$(MSBuildProjectName)</aSsemblyName>" : "";
            var project = "<Project Sdk=\"Microsoft.NET.Sdk\">\n<PropertyGroup>\n"
                + (before ? assignment : identity) + "\n"
                + (before ? identity : assignment) + "\n"
                + (cleared ? $"<{signal.ToLowerInvariant()} />" : "") + "\n</PropertyGroup>\n</Project>";
            Write(temp.Path, "Fixture.csproj", project);
            var rows = ProjectFileReader.ReadProducedPackages(temp.Path, [Project("Fixture.csproj", project)]);
            if (cleared) Assert.Empty(rows);
            else
            {
                var row = Assert.Single(rows);
                Assert.Equal(signal, row.PackSignals);
                Assert.Equal(interpolated ? 3 : before ? 3 : 4, row.SpanStart);
                Assert.Equal(interpolated ? 4 : before ? 3 : 4, row.Line);
                Assert.Equal(interpolated ? "interpolated" : "derived-default", row.PackageIdProvenance);
            }
        }
    }

    [Theory]
    [InlineData("<Sdk Name=\"Microsoft.NET.Sdk\" />", true)]
    [InlineData("<Import Project=\"Sdk.props\" Sdk=\"Microsoft.NET.Sdk\" />", true)]
    [InlineData("<ImportGroup><Import Project=\"Sdk.props\" Sdk=\"Microsoft.NET.Sdk\" /></ImportGroup>", true)]
    [InlineData("<Import Project=\"Sdk.props\" />", false)]
    [InlineData("<Sdk Name=\"\" />", false)]
    [InlineData("<ItemGroup><Sdk Name=\"Microsoft.NET.Sdk\" /></ItemGroup>", false)]
    public void Sdk_declaration_matrix_preserves_intent_test_sdk_and_false_guards(string sdk, bool supported)
    {
        using var temp = new TempDirectory();
        foreach (var identity in new[] { "", "<assemblyname>Prefix.$(MSBuildProjectName)</assemblyname>" })
        foreach (var signal in new[] { "", "<authors>fixture</authors>", "<ispackable>true</ispackable>", "<Authors>fixture</Authors><ISPACKABLE>false</ISPACKABLE>" })
        foreach (var testSdk in new[] { false, true })
        {
            var project = $"<Project>{sdk}<PropertyGroup>{identity}{signal}</PropertyGroup>"
                + (testSdk ? "<ItemGroup><PackageReference Include=\"Microsoft.NET.Test.Sdk\" /></ItemGroup>" : "") + "</Project>";
            Write(temp.Path, "Fixture.csproj", project);
            var rows = ProjectFileReader.ReadProducedPackages(temp.Path, [Project("Fixture.csproj", project)]);
            if (supported && signal.Length > 0 && !signal.Contains("false", StringComparison.Ordinal)
                && (!testSdk || signal.Contains("true", StringComparison.Ordinal))) Assert.Single(rows);
            else Assert.Empty(rows);
        }
    }

    [Theory]
    [InlineData("<packageid>Declared.Id</packageid>", "Declared.Id", true)]
    [InlineData("<aSsemblyName>Assembly.Id</aSsemblyName>", "Assembly.Id", false)]
    [InlineData("<packageid>$(Unknown)</packageid>", null, false)]
    [InlineData("<assemblyname>$(Unknown)</assemblyname>", null, false)]
    [InlineData("<PackageId>Old</PackageId><packageid />", null, false)]
    [InlineData("<AssemblyName>Old</AssemblyName><assemblyname />", null, false)]
    public void Mixed_case_and_cleared_identity_never_invent_filename(string identity, string? expected, bool explicitId)
    {
        using var temp = new TempDirectory();
        var project = $"<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup>{identity}<Description>fixture</Description></PropertyGroup></Project>";
        Write(temp.Path, "WrongFilename.csproj", project);
        var rows = ProjectFileReader.ReadProducedPackages(temp.Path, [Project("WrongFilename.csproj", project)]);
        if (expected is null) Assert.Empty(rows);
        else
        {
            var row = Assert.Single(rows);
            Assert.Equal(expected, row.PackageId);
            Assert.Equal(explicitId, row.ExplicitPackageId);
            Assert.Equal("declared", row.PackageIdProvenance);
        }
    }

    [Theory]
    [InlineData("<vErSiOn>1.2.3</vErSiOn>", "1.2.3", 3)]
    [InlineData("<Version>1.2.3</Version><VERSION />", null, 4)]
    [InlineData("<Version>1.2.3</Version><packageversion>2.0.0</packageversion>", "2.0.0", 3)]
    [InlineData("<Version>1.2.3</Version><PackageVersion>2.0.0</PackageVersion><PACKAGEVERSION />", null, 4)]
    public void Version_matrix_preserves_empty_shadowing_and_pre_identity_evidence(string version, string? expected, int start)
    {
        using var temp = new TempDirectory();
        var project = $"<Project Sdk=\"Microsoft.NET.Sdk\">\n<PropertyGroup>\n{version}\n<packageid>Fixture.Id</packageid>\n</PropertyGroup></Project>";
        Write(temp.Path, "Fixture.csproj", project);
        var row = Assert.Single(ProjectFileReader.ReadProducedPackages(temp.Path, [Project("Fixture.csproj", project)]));
        Assert.Equal(expected, row.Version);
        Assert.Equal(start, row.SpanStart);
        Assert.Equal(4, row.Line);
    }

    [Fact]
    public void Scan_retains_same_id_target_provenance_and_attributable_unresolved_gaps()
    {
        using var temp = new TempDirectory();
        Write(temp.Path, "Fixture.sln", Solution); // empty trusted solution: never evaluate the project imports
        Write(temp.Path, "declared/Target.csproj", "<Project><PropertyGroup><PackageId>Same</PackageId></PropertyGroup></Project>");
        Write(temp.Path, "derived/Same.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><Authors>fixture</Authors></PropertyGroup></Project>");
        Write(temp.Path, "interpolated/Same.csproj", "<Project><PropertyGroup><PackageId>$(MSBuildProjectName)</PackageId></PropertyGroup></Project>");
        var includes = new[] { "declared/Target.csproj", "derived/Same.csproj", "interpolated/Same.csproj", "Missing.csproj", "$(SecretRoot)/Target.csproj", "*.csproj", "../../private/Target.csproj", "/private/secret/Target.csproj" };
        var project = "<Project><PropertyGroup><PackageId>Consumer</PackageId></PropertyGroup>\n<ItemGroup>\n"
            + string.Join('\n', includes.Select(path => $"<ProjectReference Include=\"{path}\" />")) + "\n</ItemGroup></Project>";
        Write(temp.Path, "Consumer.csproj", project);
        var scan = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, ".tracemap"), SolutionPaths: ["Fixture.sln"]));
        var fact = Assert.Single(scan.Facts, f => f.FactType == FactTypes.PackageProduced && f.TargetSymbol == "Consumer");
        Assert.Equal("Same", fact.Properties["packDependencies"]);
        Assert.Equal("1", fact.Properties["packDependencyCount"]);
        Assert.Equal("8", fact.Properties["packDependencyEvidenceCount"]);
        Assert.Equal("5", fact.Properties["packDependencyUnresolvedCount"]);
        Assert.Equal("partial", fact.Properties["packDependencyCoverage"]);
        var edges = System.Text.Json.JsonSerializer.Deserialize<PackDependencyEvidence[]>(fact.Properties["packDependencyEvidence"])!;
        Assert.Equal(new[] { "declared", "derived-default", "interpolated" }, edges.Take(3).Select(e => e.PackageIdProvenance));
        Assert.Equal(includes.Take(3), edges.Take(3).Select(e => e.TargetProjectPath));
        Assert.All(edges.Take(3), edge => { Assert.Equal(1, edge.TargetStartLine); Assert.Equal(1, edge.TargetEndLine); });
        var gaps = scan.Facts.Where(f => f.Properties.GetValueOrDefault("gapKind") == "package-producer-dependency-unresolved").OrderBy(f => f.Evidence.StartLine).ToArray();
        Assert.Equal(5, gaps.Length);
        for (var i = 0; i < gaps.Length; i++)
        {
            Assert.Equal(i + 6, gaps[i].Evidence.StartLine);
            Assert.Equal(FactFactory.Hash(includes[i + 3], 64), gaps[i].Properties["includeHash"]);
            Assert.Equal(EvidenceTiers.Tier4Unknown, gaps[i].EvidenceTier);
            Assert.Equal(RuleIds.ProjectFile, gaps[i].RuleId);
        }
        Assert.DoesNotContain("SecretRoot", System.Text.Json.JsonSerializer.Serialize(scan.Facts), StringComparison.Ordinal);
        Assert.DoesNotContain("/private/secret", System.Text.Json.JsonSerializer.Serialize(scan.Facts), StringComparison.Ordinal);
        Assert.Contains("Reduced", scan.Manifest.AnalysisLevel, StringComparison.Ordinal);
        Assert.Contains(scan.Manifest.KnownGaps, gap => gap.Contains("Package producer dependency", StringComparison.Ordinal));
        Assert.DoesNotContain(scan.Facts, f => f.FactType == FactTypes.PackageReferenced);
        var repeat = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, ".tracemap"), SolutionPaths: ["Fixture.sln"]));
        Assert.Equal(scan.Facts.Select(f => f.FactId), repeat.Facts.Select(f => f.FactId));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Scan_marks_oversized_id_and_evidence_projections_without_losing_counts(bool sameId)
    {
        using var temp = new TempDirectory();
        Write(temp.Path, "Fixture.sln", Solution);
        var references = new List<string>();
        for (var i = 0; i < 40; i++)
        {
            var id = $"Fixture.Dependency{i:D2}";
            Write(temp.Path, $"{id}.csproj", $"<Project><PropertyGroup><PackageId>{(sameId ? "Shared" : id)}</PackageId></PropertyGroup></Project>");
            references.Add($"<ProjectReference Include=\"{id}.csproj\" />");
        }
        Write(temp.Path, "Consumer.csproj", "<Project><PropertyGroup><PackageId>Consumer</PackageId></PropertyGroup>\n<ItemGroup>\n" + string.Join('\n', references) + "\n</ItemGroup></Project>");
        var scan = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, ".tracemap"), SolutionPaths: ["Fixture.sln"]));
        var fact = Assert.Single(scan.Facts, f => f.FactType == FactTypes.PackageProduced && f.TargetSymbol == "Consumer");
        Assert.Equal(sameId ? "1" : "40", fact.Properties["packDependencyCount"]);
        if (sameId)
        {
            Assert.Equal("Shared", fact.Properties["packDependencies"]);
            Assert.False(fact.Properties.ContainsKey("packDependenciesTruncated"));
        }
        else
        {
            Assert.Equal("40", fact.Properties["packDependenciesOmittedCount"]);
            Assert.Equal("true", fact.Properties["packDependenciesTruncated"]);
            Assert.False(fact.Properties.ContainsKey("packDependencies"));
        }
        var json = fact.Properties["packDependencyEvidence"];
        Assert.True(json.Length <= 4096);
        var retained = System.Text.Json.JsonSerializer.Deserialize<PackDependencyEvidence[]>(json)!;
        Assert.NotEmpty(retained);
        Assert.Equal(40 - retained.Length, int.Parse(fact.Properties["packDependencyEvidenceOmittedCount"], System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal("partial", fact.Properties["packDependencyCoverage"]);
        Assert.Single(scan.Facts, f => f.Properties.GetValueOrDefault("gapKind") == "package-producer-dependency-projection-limit");
        Assert.Contains("Reduced", scan.Manifest.AnalysisLevel, StringComparison.Ordinal);
    }

    private const string Solution = """
        Microsoft Visual Studio Solution File, Format Version 12.00
        # Visual Studio Version 17
        Global
        EndGlobal
        """;

    private static FileInventoryItem Project(string relativePath, string content) => new(relativePath, "Project", content.Length);

    private static void Write(string root, string relativePath, string content)
    {
        var file = Path.Combine(root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, content);
    }
}
