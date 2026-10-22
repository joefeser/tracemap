using System.Text.Json;
using TraceMap.Cli;
using TraceMap.Core;

namespace TraceMap.Tests;

// The scan-level cases create Git repositories, so they join the nonparallel collection.
[Collection("Git metadata sensitive")]
public sealed class CiWorkflowProducerEvidenceTests
{
    private const string PureCiProject = """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
          </PropertyGroup>
        </Project>
        """;

    private const string PureCiWorkflow = """
        name: pack
        on: push
        jobs:
          release:
            runs-on: ubuntu-latest
            steps:
              - uses: actions/checkout@v4
              - name: Pack
                run: |
                  dotnet pack src/Core.csproj -p:PackageId=Contoso.Ci -p:PackageVersion=2.0.0
        """;

    [Fact]
    public void Pure_ci_producer_takes_identity_and_version_from_flags()
    {
        using var temp = new TempDirectory();
        WriteWorkflow(temp.Path, "pack.yml", PureCiWorkflow);
        WriteProject(temp.Path, "src/Core.csproj", PureCiProject);

        var result = Read(temp.Path);

        var row = Assert.Single(result.Rows);
        Assert.Equal("Contoso.Ci", row.Package);
        Assert.Equal("2.0.0", row.Version);
        Assert.Equal(".github/workflows/pack.yml", row.WorkflowPath);
        Assert.Equal("src/Core.csproj", row.ProjectPath);
        Assert.Equal(10, row.StartLine); // the dotnet pack line inside the run block
        Assert.Equal(10, row.EndLine);
        Assert.Empty(result.Gaps);
        Assert.Equal(64, result.GeneratorSha256.Length);
        Assert.Equal(result.BoundedInputSha256, Read(temp.Path).BoundedInputSha256);
    }

    [Fact]
    public void Version_override_keeps_project_declaration_and_adds_ci_fact()
    {
        const string project = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <PackageId>Contoso.Core</PackageId>
                <Version>1.0.0</Version>
              </PropertyGroup>
            </Project>
            """;
        const string workflow = """
            jobs:
              release:
                steps:
                  - run: dotnet pack src/Core.csproj -p:PackageId=Contoso.Core -p:PackageVersion=9.9.9
            """;
        using var temp = new TempDirectory();
        WriteWorkflow(temp.Path, "pack.yml", workflow);
        WriteProject(temp.Path, "src/Core.csproj", project);
        InitGit(temp.Path);

        var scan = ScanEngine.Scan(new ScanOptions(temp.Path, temp.Path + "-out") { IndexCiProducers = true });
        var produced = scan.Facts.Where(fact => fact.FactType == FactTypes.PackageProduced).ToArray();
        Assert.Equal(2, produced.Length);
        var declared = Assert.Single(produced, fact => fact.Properties["sourceKind"] == "build-file");
        Assert.Equal("1.0.0", declared.Properties["version"]);
        Assert.Equal("csproj", declared.Properties["manifestKind"]);
        var ci = Assert.Single(produced, fact => fact.Properties["sourceKind"] == "ci-workflow");
        Assert.Equal("9.9.9", ci.Properties["version"]);
        Assert.Equal("github-workflow", ci.Properties["manifestKind"]);
        Assert.Equal(".github/workflows/pack.yml", ci.Properties["workflowPath"]);
        Assert.Equal(RuleIds.ProjectFile, ci.RuleId);
        Assert.Equal(EvidenceTiers.Tier2Structural, ci.EvidenceTier);
        Assert.Equal(ScannerVersions.CiWorkflowExtractor, ci.Evidence.ExtractorVersion);
        Assert.Equal("Contoso.Core", ci.TargetSymbol);
        Assert.Empty(resultGaps(scan));
    }

    [Fact]
    public void Templated_version_resolves_from_literal_env_and_names_unresolvable_templates()
    {
        const string workflow = """
            env:
              PKG_VERSION: 3.2.1
            jobs:
              release:
                env:
                  PKG_VERSION: 4.0.0
                steps:
                  - run: dotnet pack src/Core.csproj -p:PackageId=Contoso.Env -p:PackageVersion=${{ env.PKG_VERSION }}
                  - run: dotnet pack src/Core.csproj -p:PackageId=Contoso.Matrix -p:PackageVersion=${{ matrix.version }}
                  - run: dotnet pack src/Core.csproj -p:PackageId=Contoso.Shell -p:PackageVersion=$PKG_VERSION
            """;
        using var temp = new TempDirectory();
        WriteWorkflow(temp.Path, "pack.yml", workflow);
        WriteProject(temp.Path, "src/Core.csproj", PureCiProject);

        var result = Read(temp.Path);

        // Job env outranks workflow env: the effective literal is 4.0.0.
        var env = Assert.Single(result.Rows, row => row.Package == "Contoso.Env");
        Assert.Equal("4.0.0", env.Version);
        // Unresolvable templates and shell variables omit the version entirely, never guess.
        var matrix = Assert.Single(result.Rows, row => row.Package == "Contoso.Matrix");
        Assert.Null(matrix.Version);
        var shell = Assert.Single(result.Rows, row => row.Package == "Contoso.Shell");
        Assert.Null(shell.Version);
        Assert.Contains(result.Gaps, gap => gap.Kind == "ci-producer-version-template"
            && gap.Detail!.Contains("${{ matrix.version }}", StringComparison.Ordinal));
        Assert.Contains(result.Gaps, gap => gap.Kind == "ci-producer-version-template"
            && gap.Detail!.Contains("$PKG_VERSION", StringComparison.Ordinal));
    }

    [Fact]
    public void Flag_off_reads_no_workflows_and_changes_no_scan_output()
    {
        using var temp = new TempDirectory();
        WriteWorkflow(temp.Path, "pack.yml", PureCiWorkflow);
        WriteProject(temp.Path, "src/Core.csproj", PureCiProject);
        InitGit(temp.Path);

        var first = ScanEngine.Scan(new ScanOptions(temp.Path, temp.Path + "-out"));
        var second = ScanEngine.Scan(new ScanOptions(temp.Path, temp.Path + "-out"));
        Assert.Equal(Normalize(first.Facts), Normalize(second.Facts));
        Assert.Equal(first.Manifest.ScanId, second.Manifest.ScanId);
        // The flag-off lane never reads workflows: no ci facts, no ci gaps, no ci coverage marks.
        Assert.DoesNotContain(first.Facts, fact => fact.Properties.GetValueOrDefault("sourceKind") == "ci-workflow"
            || fact.Properties.GetValueOrDefault("manifestKind") == "github-workflow"
            || fact.Evidence.ExtractorId == "CiWorkflowExtractor");
        Assert.DoesNotContain(first.Manifest.KnownGaps, gap => gap.Contains("CI workflow producer", StringComparison.Ordinal));

        var enabled = ScanEngine.Scan(
            new ScanOptions(temp.Path, temp.Path + "-on") { IndexCiProducers = true });
        Assert.Contains(enabled.Facts, fact => fact.Properties.GetValueOrDefault("sourceKind") == "ci-workflow");
        Assert.Equal(first.Manifest.SourceSnapshotDigest, enabled.Manifest.SourceSnapshotDigest);
        Assert.NotEqual(first.Manifest.ScanId, enabled.Manifest.ScanId);
    }

    [Fact]
    public void Opt_in_properties_preserve_the_existing_positional_constructor()
    {
        var constructor = Assert.Single(typeof(ScanOptions).GetConstructors(),
            ctor => ctor.GetParameters().LastOrDefault()?.Name == "WebFormsPublishSourceRelativeBase");
        Assert.DoesNotContain(constructor.GetParameters(), parameter => parameter.Name == "IndexCiProducers");
        Assert.False(new ScanOptions("repo", "out").IndexCiProducers);
    }

    [Fact]
    public void Same_package_same_version_collapses_and_two_versions_conflict()
    {
        const string first = """
            jobs:
              a:
                steps:
                  - run: dotnet pack src/Core.csproj -p:PackageId=Contoso.Dup -p:PackageVersion=2.0.0
            """;
        const string second = """
            jobs:
              b:
                steps:
                  - run: dotnet pack src/Core.csproj -p:PackageId=Contoso.Dup -p:PackageVersion=2.0.0
            """;
        const string third = """
            jobs:
              c:
                steps:
                  - run: dotnet pack src/Core.csproj -p:PackageId=Contoso.Dup -p:PackageVersion=3.0.0
            """;
        using var temp = new TempDirectory();
        WriteProject(temp.Path, "src/Core.csproj", PureCiProject);
        WriteWorkflow(temp.Path, "a-first.yml", first);
        WriteWorkflow(temp.Path, "b-second.yml", second);

        var collapsed = Read(temp.Path);
        var row = Assert.Single(collapsed.Rows);
        Assert.Equal("2.0.0", row.Version);
        Assert.Equal(".github/workflows/a-first.yml", row.WorkflowPath); // first deterministic occurrence
        Assert.Empty(collapsed.Gaps);

        WriteWorkflow(temp.Path, "c-third.yml", third);
        var conflicted = Read(temp.Path);
        Assert.DoesNotContain(conflicted.Rows, r => r.Package == "Contoso.Dup");
        var gap = Assert.Single(conflicted.Gaps, g => g.Kind == "ci-producer-version-conflict");
        Assert.Contains("Contoso.Dup", gap.Detail!, StringComparison.Ordinal);
        Assert.Contains("2.0.0", gap.Detail!, StringComparison.Ordinal);
        Assert.Contains("3.0.0", gap.Detail!, StringComparison.Ordinal);
    }

    [Fact]
    public void Versioned_and_versionless_claims_collapse_to_the_evidenced_version()
    {
        const string versioned = """
            jobs:
              a:
                steps:
                  - run: dotnet pack src/Core.csproj -p:PackageId=Contoso.Mixed -p:PackageVersion=5.0.0
            """;
        const string versionless = """
            jobs:
              b:
                steps:
                  - run: dotnet pack src/Core.csproj -p:PackageId=Contoso.Mixed
            """;
        using var temp = new TempDirectory();
        WriteProject(temp.Path, "src/Core.csproj", PureCiProject);
        WriteWorkflow(temp.Path, "a.yml", versioned);
        WriteWorkflow(temp.Path, "b.yml", versionless);

        var result = Read(temp.Path);
        var row = Assert.Single(result.Rows);
        Assert.Equal("5.0.0", row.Version); // the unknown claim does not contradict the evidenced one
        Assert.Empty(result.Gaps);
    }

    [Fact]
    public void Unparseable_workflow_is_a_typed_gap_and_deep_layouts_are_not_scanned()
    {
        using var temp = new TempDirectory();
        WriteProject(temp.Path, "src/Core.csproj", PureCiProject);
        WriteWorkflow(temp.Path, "tabbed.yml", "jobs:\n\tbuild:\n\t\tsteps: []\n");
        WriteWorkflow(temp.Path, "duplicate.yml", "on: push\non: pull_request\n");
        WriteWorkflow(temp.Path, "anchored.yml", """
            jobs:
              build:
                steps:
                  - run: &pack
                      dotnet pack src/Core.csproj
            """);

        var result = Read(temp.Path);
        Assert.Empty(result.Rows);
        Assert.Contains(result.Gaps, gap => gap.Kind == "ci-workflow-invalid" && gap.Path.EndsWith("tabbed.yml", StringComparison.Ordinal));
        Assert.Contains(result.Gaps, gap => gap.Kind == "ci-workflow-invalid" && gap.Path.EndsWith("duplicate.yml", StringComparison.Ordinal));
        Assert.Contains(result.Gaps, gap => gap.Kind == "ci-workflow-unsupported" && gap.Path.EndsWith("anchored.yml", StringComparison.Ordinal));

        // Exactly .github/workflows/*.yml depth: nested workflow directories are not hunted.
        WriteWorkflow(temp.Path, "nested/deep.yml", PureCiWorkflow);
        var deep = Read(temp.Path);
        Assert.DoesNotContain(deep.Gaps, gap => gap.Path.Contains("deep.yml", StringComparison.Ordinal));
        WriteWorkflow(temp.Path, "pack.yml", PureCiWorkflow);
        var onlyDepth = Read(temp.Path);
        var row = Assert.Single(onlyDepth.Rows);
        Assert.Equal(".github/workflows/pack.yml", row.WorkflowPath);
    }

    [Fact]
    public void Missing_workflows_directory_is_an_explicit_not_found_gap()
    {
        using var temp = new TempDirectory();
        Directory.CreateDirectory(Path.Combine(temp.Path, ".github", "nested"));
        var result = Read(temp.Path);
        var gap = Assert.Single(result.Gaps);
        Assert.Equal("ci-workflow-not-found", gap.Kind);
        Assert.Empty(result.Rows);
    }

    [Fact]
    public void Package_id_falls_back_to_explicit_project_declaration_or_gaps()
    {
        const string declaredProject = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <PackageId>Contoso.Declared</PackageId>
              </PropertyGroup>
            </Project>
            """;
        const string assemblyNamedProject = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <AssemblyName>Contoso.Fallback</AssemblyName>
              </PropertyGroup>
            </Project>
            """;
        const string workflow = """
            jobs:
              build:
                steps:
                  - run: dotnet pack src/Declared.csproj
                  - run: dotnet pack src/Fallback.csproj
                  - run: dotnet pack Contoso.sln
                  - run: dotnet pack
            """;
        using var temp = new TempDirectory();
        WriteWorkflow(temp.Path, "pack.yml", workflow);
        WriteProject(temp.Path, "src/Declared.csproj", declaredProject);
        WriteProject(temp.Path, "src/Fallback.csproj", assemblyNamedProject);

        var result = Read(temp.Path);

        // Effective id after CI overrides: the flag wins; without a flag the packed project's
        // explicit declaration is the evidence. A project declaring AssemblyName does NOT derive
        // from its filename: MSBuild's PackageId default is $(AssemblyName), so `dotnet pack` would
        // produce Contoso.Fallback, never "Fallback". The filename default applies only when the
        // bounded view proves nothing overrides the chain (see the zero-signal derived test).
        Assert.Equal(2, result.Rows.Count); // FULL CHAIN (0.4.0): the AssemblyName project ships its AssemblyName
        var declared = Assert.Single(result.Rows, row => row.IdProvenance == "declared");
        Assert.Equal("Contoso.Declared", declared.Package);
        Assert.Null(declared.Version); // no CI version override: the version stays unevidenced
        var viaChain = Assert.Single(result.Rows, row => row.Package == "Contoso.Fallback");
        Assert.True(viaChain.IdProvenance is "declared-id" or "derived-default");
        Assert.DoesNotContain(result.Rows, row => row.Package == "Fallback"); // the bare filename never ships
        var unevidenced = result.Gaps.Where(gap => gap.Kind == "ci-producer-id-unevidenced").ToArray();
        Assert.Equal(2, unevidenced.Length); // sln + bare-target only: the AssemblyName case graduated to a row
        Assert.DoesNotContain(unevidenced, gap => gap.Detail!.Contains("AssemblyName", StringComparison.Ordinal));
        Assert.Contains(unevidenced, gap => gap.Detail!.Contains("solution packs", StringComparison.Ordinal));
        Assert.Contains(unevidenced, gap => gap.Detail!.Contains("no explicit project target", StringComparison.Ordinal));
    }



    [Fact] // 2026-10-22 live: 27 *.Tests ghost producers on the real estate — hint-swept test projects must not ship
    public void Hint_lane_skips_test_projects_unless_literal_ispackable()
    {
        const string testProject = "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup>\n    <TargetFramework>net10.0</TargetFramework>\n  </PropertyGroup>\n  <ItemGroup>\n    <PackageReference Include=\"Microsoft.NET.Test.Sdk\" Version=\"17.0.0\" />\n  </ItemGroup>\n</Project>";
        const string packedTestProject = "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup>\n    <TargetFramework>net10.0</TargetFramework>\n    <IsPackable>true</IsPackable>\n  </PropertyGroup>\n  <ItemGroup>\n    <PackageReference Include=\"Microsoft.NET.Test.Sdk\" Version=\"17.0.0\" />\n  </ItemGroup>\n</Project>";
        const string libProject = "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup>\n    <TargetFramework>net10.0</TargetFramework>\n  </PropertyGroup>\n</Project>";
        const string workflow = "on:\n  workflow_dispatch:\njobs:\n  release:\n    uses: org/shared-ci/.github/workflows/dotnet-release-library.yml@main";
        using var temp = new TempDirectory();
        WriteWorkflow(temp.Path, "release.yml", workflow);
        WriteProject(temp.Path, "src/Lib.csproj", libProject);
        WriteProject(temp.Path, "src/Lib.Tests.csproj", testProject);
        WriteProject(temp.Path, "src/Lib.PackedTests.csproj", packedTestProject);

        var options = new ScanOptions(temp.Path, Path.Combine(temp.Path, ".tracemap")) { IndexCiProducers = true, CiHintWorkflows = new[] { "org/shared-ci/.github/workflows/dotnet-release-library.yml" } };
        var result = CiWorkflowProducerExtractor.Read(options, [], InventProjects(temp.Path), CancellationToken.None);

        Assert.Equal(2, result.Rows.Count);
        Assert.Contains(result.Rows, row => row.Package == "Lib");
        Assert.Contains(result.Rows, row => row.Package == "Lib.PackedTests"); // literal IsPackable=true ships even as a test project
        Assert.DoesNotContain(result.Rows, row => row.Package == "Lib.Tests"); // plain test project: never ships
    }

    [Fact] // review: a project-level Import can set identity/packability after the body — no derive
    public void Custom_import_project_never_derives_a_filename_id()
    {
        const string importingProject = "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup>\n    <TargetFramework>net10.0</TargetFramework>\n  </PropertyGroup>\n  <Import Project=\"..\\build\\identity.props\" />\n</Project>";
        const string workflow = "jobs:\n  build:\n    steps:\n      - run: dotnet pack src/Importing.csproj";
        using var temp = new TempDirectory();
        WriteWorkflow(temp.Path, "pack.yml", workflow);
        WriteProject(temp.Path, "src/Importing.csproj", importingProject);

        var result = Read(temp.Path);

        var gap = Assert.Single(result.Gaps, g => g.Kind == "ci-producer-id-unevidenced");
        Assert.Contains("Import", gap.Detail, StringComparison.Ordinal); // 0.4.1: ANY Project import refuses, not just .props/.targets suffixes
        Assert.Empty(result.Rows);
    }

    [Fact] // 2026-10-22 live shape: shared dotnet-release-library.yml@main called with nuget_publication_path —
    // the hint says this repo publishes; ZERO-SIGNAL projects become ci producers with derived ids
    public void Hinted_shared_workflow_call_makes_unclaimed_projects_ci_producers()
    {
        const string bareProject = "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup>\n    <TargetFramework>net10.0</TargetFramework>\n  </PropertyGroup>\n</Project>";
        const string workflow = "on:\n  workflow_dispatch:\njobs:\n  release:\n    uses: org/shared-ci/.github/workflows/dotnet-release-library.yml@main\n    with:\n      nuget_publication_path: 'Some/Path/'";
        using var temp = new TempDirectory();
        WriteWorkflow(temp.Path, "release.yml", workflow);
        WriteProject(temp.Path, "src/Bare.csproj", bareProject);

        var options = new ScanOptions(temp.Path, Path.Combine(temp.Path, ".tracemap")) { IndexCiProducers = true, CiHintWorkflows = new[] { "org/shared-ci/.github/workflows/dotnet-release-library.yml" } };
        var result = CiWorkflowProducerExtractor.Read(options, [], InventProjects(temp.Path), CancellationToken.None);

        var row = Assert.Single(result.Rows);
        Assert.Equal("Bare", row.Package);
        Assert.Equal("derived-default", row.IdProvenance);
        Assert.Equal("src/Bare.csproj", row.ProjectPath);
        Assert.DoesNotContain(result.Gaps, gap => gap.Kind == "ci-hint-no-projects");
    }

    [Fact] // 2026-10-22: the zero-signal producer - NOTHING in the csproj, only the CI pack step
    public void Zero_signal_project_packs_by_ci_step_alone_as_derived_default()
    {
        const string bareProject = "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup>\n    <TargetFramework>net10.0</TargetFramework>\n  </PropertyGroup>\n</Project>";
        const string workflow = "jobs:\n  build:\n    steps:\n      - run: dotnet pack src/Bare.csproj -c Release -o out";
        using var temp = new TempDirectory();
        WriteWorkflow(temp.Path, "pack.yml", workflow);
        WriteProject(temp.Path, "src/Bare.csproj", bareProject);

        var result = Read(temp.Path);

        var row = Assert.Single(result.Rows);
        Assert.Equal("Bare", row.Package);
        Assert.Equal("derived-default", row.IdProvenance);
        Assert.Equal("src/Bare.csproj", row.ProjectPath);
        Assert.DoesNotContain(result.Gaps, gap => gap.Kind == "ci-producer-id-unevidenced");
    }

    private const string BareSdkProject = "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup>\n    <TargetFramework>net10.0</TargetFramework>\n  </PropertyGroup>\n</Project>";
    private const string HintedReleaseWorkflow = "on:\n  workflow_dispatch:\njobs:\n  release:\n    uses: org/shared-ci/.github/workflows/dotnet-release-library.yml@main\n    with:\n      nuget_publication_path: 'Some/Path/'";

    private static CiWorkflowResult ReadHinted(string root, IEnumerable<FileInventoryItem>? inventory = null, params string[] hints)
    {
        var options = new ScanOptions(root, Path.Combine(root, ".tracemap")) { IndexCiProducers = true, CiHintWorkflows = hints };
        var scoped = (inventory ?? InventProjects(root)).ToArray();
        return CiWorkflowProducerExtractor.Read(options, ProjectFileReader.ReadProducedPackages(root, scoped), scoped, CancellationToken.None);
    }

    [Fact]
    public void Hint_suffix_match_respects_path_boundaries_and_cites_the_uses_line()
    {
        using var temp = new TempDirectory();
        WriteWorkflow(temp.Path, "release.yml", HintedReleaseWorkflow);
        WriteProject(temp.Path, "src/Bare.csproj", BareSdkProject);

        // a bare filename hint is a path-boundary suffix of the full uses value
        var row = Assert.Single(ReadHinted(temp.Path, null, "dotnet-release-library.yml").Rows);
        Assert.Equal(".github/workflows/release.yml", row.WorkflowPath);
        Assert.Equal(5, row.StartLine); // the job-level `uses:` line, not line 1
        Assert.Equal("ci-hint-workflow", row.SignalsKind);
        // a suffix that is not on a '/' boundary never matches
        Assert.Empty(ReadHinted(temp.Path, null, "release-library.yml").Rows);
    }

    [Fact]
    public void Hint_call_in_a_rejected_workflow_contributes_nothing()
    {
        using var temp = new TempDirectory();
        // duplicate job key: the validated reader drops the whole file, so the call is not evidence either
        WriteWorkflow(temp.Path, "release.yml",
            "jobs:\n  release:\n    uses: org/shared-ci/.github/workflows/dotnet-release-library.yml@main\n    uses: org/other/.github/workflows/x.yml@main");
        WriteProject(temp.Path, "src/Bare.csproj", BareSdkProject);

        var result = ReadHinted(temp.Path, null, "org/shared-ci/.github/workflows/dotnet-release-library.yml");

        Assert.Empty(result.Rows);
        Assert.Contains(result.Gaps, gap => gap.Kind == "ci-workflow-invalid");
        Assert.DoesNotContain(result.Gaps, gap => gap.Kind == "ci-hint-no-projects");
    }

    [Fact]
    public void Uses_text_outside_a_job_level_key_is_not_a_hinted_call()
    {
        using var temp = new TempDirectory();
        WriteWorkflow(temp.Path, "build.yml", """
            jobs:
              build:
                runs-on: ubuntu-latest
                env:
                  NOTE: |
                    uses: org/shared-ci/.github/workflows/dotnet-release-library.yml@main
                steps:
                  - name: checkout
                    uses: org/shared-ci/.github/workflows/dotnet-release-library.yml@main
            """);
        WriteProject(temp.Path, "src/Bare.csproj", BareSdkProject);

        var result = ReadHinted(temp.Path, null, "org/shared-ci/.github/workflows/dotnet-release-library.yml");

        Assert.Empty(result.Rows);
        Assert.DoesNotContain(result.Gaps, gap => gap.Kind.StartsWith("ci-hint", StringComparison.Ordinal));
    }

    [Fact]
    public void Hint_sweep_uses_the_scanned_inventory_and_never_relabels_direct_pack_rows()
    {
        using var temp = new TempDirectory();
        WriteWorkflow(temp.Path, "release.yml", HintedReleaseWorkflow);
        WriteWorkflow(temp.Path, "pack.yml", "jobs:\n  build:\n    steps:\n      - run: dotnet pack src/Packed.csproj");
        WriteProject(temp.Path, "src/Lib.csproj", BareSdkProject);
        WriteProject(temp.Path, "src/Packed.csproj", BareSdkProject);
        WriteProject(temp.Path, "src/OutOfScope.csproj", BareSdkProject);
        var scoped = InventProjects(temp.Path).Where(item => !item.RelativePath.EndsWith("OutOfScope.csproj", StringComparison.Ordinal));

        var result = ReadHinted(temp.Path, scoped, "dotnet-release-library.yml");

        Assert.Equal(2, result.Rows.Count);
        Assert.Equal("ci-hint-workflow", Assert.Single(result.Rows, row => row.Package == "Lib").SignalsKind);
        var packed = Assert.Single(result.Rows, row => row.Package == "Packed");
        Assert.Equal("ci-pack-step", packed.SignalsKind); // direct evidence outranks the operator assertion
        Assert.Equal(".github/workflows/pack.yml", packed.WorkflowPath);
        Assert.DoesNotContain(result.Rows, row => row.Package == "OutOfScope"); // excluded from the scan scope
    }

    [Fact]
    public void Hint_sweep_gaps_unprovable_identities_and_skips_non_producers()
    {
        using var temp = new TempDirectory();
        WriteWorkflow(temp.Path, "release.yml", HintedReleaseWorkflow);
        WriteProject(temp.Path, "src/Lib.csproj", BareSdkProject);
        // interpolated AssemblyName without pack signals: the project lane does not promote it,
        // and the filename default is NOT its id — a typed gap, never a filename guess
        WriteProject(temp.Path, "src/Interp.csproj",
            "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup>\n    <AssemblyName>Contoso.$(MSBuildProjectName)</AssemblyName>\n  </PropertyGroup>\n</Project>");
        // the nearest Directory.Build.props overrides the identity chain for everything under lib/
        WriteProject(temp.Path, "lib/Directory.Build.props",
            "<Project>\n  <PropertyGroup>\n    <PackageId>Contoso.$(MSBuildProjectName)</PackageId>\n  </PropertyGroup>\n</Project>");
        WriteProject(temp.Path, "lib/Inherited.csproj", BareSdkProject);
        // inherited IsPackable=false and legacy non-SDK projects are not producers at all
        WriteProject(temp.Path, "tools/Directory.Build.props",
            "<Project>\n  <PropertyGroup>\n    <IsPackable>false</IsPackable>\n  </PropertyGroup>\n</Project>");
        WriteProject(temp.Path, "tools/Tool.csproj", BareSdkProject);
        WriteProject(temp.Path, "legacy/Legacy.csproj",
            "<Project ToolsVersion=\"15.0\" xmlns=\"http://schemas.microsoft.com/developer/msbuild/2003\">\n  <PropertyGroup>\n    <OutputType>Library</OutputType>\n  </PropertyGroup>\n</Project>");

        var result = ReadHinted(temp.Path, null, "dotnet-release-library.yml");

        // FULL CHAIN (0.4.0): Interp declares AssemblyName=Contoso.Interp -> a declared-id ROW
        // (the live estate's 88-producer recovery), not a gap; only Inherited (Directory.Build
        // identity outside the provable chain) stays unevidenced
        Assert.Equal(2, result.Rows.Count);
        Assert.Contains(result.Rows, row => row.Package == "Lib" && row.IdProvenance == "derived-default");
        var interp = Assert.Single(result.Rows, row => row.Package == "Contoso.Interp");
        Assert.Equal("declared-id", interp.IdProvenance);
        Assert.Equal("AssemblyName", interp.IdSource);
        var unevidenced = result.Gaps.Where(gap => gap.Kind == "ci-hint-id-unevidenced").Select(gap => gap.Path).Order(StringComparer.Ordinal).ToArray();
        // gaps anchor to the uses: CALL SITE (the workflow + line carrying the assertion); the
        // affected project rides the DETAIL — a gap at <project>:1-1 was a fabricated location
        Assert.All(unevidenced, gapPath => Assert.Equal(".github/workflows/release.yml", gapPath));
        var details = result.Gaps.Where(gap => gap.Kind == "ci-hint-id-unevidenced").Select(gap => gap.Detail).ToList();
        Assert.Contains(details, detail => detail!.StartsWith("lib/Inherited.csproj:"));
        Assert.DoesNotContain(result.Gaps, gap => gap.Path is "tools/Tool.csproj" or "legacy/Legacy.csproj");
    }

    [Fact]
    public void Direct_pack_derivation_honors_directory_build_identity_overrides()
    {
        using var temp = new TempDirectory();
        WriteWorkflow(temp.Path, "pack.yml", "jobs:\n  build:\n    steps:\n      - run: dotnet pack src/Bare.csproj");
        WriteProject(temp.Path, "Directory.Build.props",
            "<Project>\n  <PropertyGroup>\n    <AssemblyName>Contoso.$(MSBuildProjectName)</AssemblyName>\n  </PropertyGroup>\n</Project>");
        WriteProject(temp.Path, "src/Bare.csproj", BareSdkProject);

        var result = Read(temp.Path);

        Assert.Empty(result.Rows);
        var gap = Assert.Single(result.Gaps, gap => gap.Kind == "ci-producer-id-unevidenced");
        Assert.Contains("Directory.Build", gap.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void Derived_identity_decisions_move_the_bounded_digest()
    {
        using var temp = new TempDirectory();
        WriteWorkflow(temp.Path, "pack.yml", "jobs:\n  build:\n    steps:\n      - run: dotnet pack src/Bare.csproj");
        WriteProject(temp.Path, "src/Bare.csproj", BareSdkProject);
        var before = Read(temp.Path);
        WriteProject(temp.Path, "Directory.Build.props",
            "<Project>\n  <PropertyGroup>\n    <PackageId>Contoso.$(MSBuildProjectName)</PackageId>\n  </PropertyGroup>\n</Project>");
        var after = Read(temp.Path);

        Assert.Single(before.Rows);
        Assert.Empty(after.Rows);
        Assert.NotEqual(before.BoundedInputSha256, after.BoundedInputSha256);
    }

    [Theory]
    [InlineData("{\"packWorkflow\": [{\"uses\": \"x.yml\"}]}")] // misspelled key
    [InlineData("{\"packWorkflows\": [{\"uses\": 3}]}")]
    [InlineData("{\"packWorkflows\": [")]
    public async Task Cli_rejects_malformed_ci_hints_instead_of_silently_disabling_them(string hints)
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        WriteProject(repo, "src/Core.csproj", PureCiProject);
        WriteWorkflow(repo, "pack.yml", PureCiWorkflow);
        InitGit(repo);
        var hintsPath = Path.Combine(temp.Path, "ci-hints.v1.json");
        File.WriteAllText(hintsPath, hints);
        using var output = new StringWriter();
        using var error = new StringWriter();

        Assert.NotEqual(0, await TraceMapCommand.RunAsync(
            ["scan", "--repo", repo, "--out", Path.Combine(temp.Path, "out"), "--index-ci-producers", "--ci-hints", hintsPath],
            output, error));
        Assert.Contains("--ci-hints", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Cli_rejects_ci_hints_without_the_ci_lane()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        WriteProject(repo, "src/Core.csproj", PureCiProject);
        InitGit(repo);
        var hintsPath = Path.Combine(temp.Path, "ci-hints.v1.json");
        File.WriteAllText(hintsPath, "{\"packWorkflows\": [{\"uses\": \"dotnet-release-library.yml\"}]}");
        using var output = new StringWriter();
        using var error = new StringWriter();

        Assert.NotEqual(0, await TraceMapCommand.RunAsync(
            ["scan", "--repo", repo, "--out", Path.Combine(temp.Path, "out"), "--ci-hints", hintsPath], output, error));
        Assert.Contains("--index-ci-producers", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Templated_and_unsafe_package_ids_are_skipped_never_guessed()
    {
        const string workflow = """
            jobs:
              build:
                steps:
                  - run: dotnet pack src/Core.csproj -p:PackageId=${{ matrix.package }}
                  - run: dotnet pack src/Core.csproj -p:PackageId=../escapes/../id
            """;
        using var temp = new TempDirectory();
        WriteWorkflow(temp.Path, "pack.yml", workflow);
        WriteProject(temp.Path, "src/Core.csproj", PureCiProject);

        var result = Read(temp.Path);
        Assert.Empty(result.Rows);
        Assert.Equal(2, result.Gaps.Count(gap => gap.Kind == "ci-producer-id-unevidenced"));
    }

    [Fact]
    public void Property_forms_quoting_and_nuget_version_precedence()
    {
        const string workflow = """
        jobs:
          long-form:
            steps:
              - run: dotnet pack src/Core.csproj --property:PackageId=Contoso.Long -p:PackageVersion=6.1.0
          both-versions:
            steps:
              - run: dotnet pack src/Core.csproj -p:PackageId=Contoso.Rank -p:Version=1.0.0 -p:PackageVersion=2.0.0
          quoted:
            steps:
              - run: dotnet pack src/Core.csproj -p:PackageId="Contoso.Quoted" -p:PackageVersion=1.2.3
          folded:
            steps:
              - run: >
                  dotnet pack src/Core.csproj
                  -p:PackageId=Contoso.Folded
                  -p:PackageVersion=7.0.0
          continuation:
            steps:
              - run: |
                  dotnet pack src/Core.csproj \
                    -p:PackageId=Contoso.Continued \
                    -p:PackageVersion=8.0.0
          mixed-commands:
            steps:
              - run: |
                  dotnet build src/Core.csproj
                  dotnet pack src/Core.csproj -o dist -p:PackageId=Contoso.Flagged -p:PackageVersion=9.0.0
                  echo done
        """;
        using var temp = new TempDirectory();
        WriteWorkflow(temp.Path, "pack.yml", workflow);
        WriteProject(temp.Path, "src/Core.csproj", PureCiProject);

        var result = Read(temp.Path);
        Assert.Empty(result.Gaps);
        Assert.Equal(6, result.Rows.Count);
        Assert.Contains(result.Rows, row => row.Package == "Contoso.Long" && row.Version == "6.1.0");
        // NuGet pack semantics: PackageVersion outranks Version regardless of flag order.
        Assert.Contains(result.Rows, row => row.Package == "Contoso.Rank" && row.Version == "2.0.0");
        Assert.Contains(result.Rows, row => row.Package == "Contoso.Quoted" && row.Version == "1.2.3");
        var folded = Assert.Single(result.Rows, row => row.Package == "Contoso.Folded");
        Assert.Equal("7.0.0", folded.Version);
        // Line continuations join into one command whose span covers every continued line.
        var continued = Assert.Single(result.Rows, row => row.Package == "Contoso.Continued");
        Assert.True(continued.EndLine > continued.StartLine, "continuation span must cover joined lines");
        // Value-taking flags (-o dist) never displace the project target.
        Assert.Contains(result.Rows, row => row.Package == "Contoso.Flagged" && row.ProjectPath == "src/Core.csproj");
    }

    [Fact]
    public void Quoted_flag_values_never_leak_truncated_facts()
    {
        const string workflow = """
            jobs:
              build:
                steps:
                  - run: dotnet pack src/Core.csproj -p:PackageId=Contoso.Pipe -p:PackageVersion="1.0.0+meta|2"
                  - run: dotnet pack src/Core.csproj -p:PackageId=Contoso.Hash -p:PackageVersion="1.0.0 #2"
            """;
        using var temp = new TempDirectory();
        WriteWorkflow(temp.Path, "pack.yml", workflow);
        WriteProject(temp.Path, "src/Core.csproj", PureCiProject);

        var result = Read(temp.Path);
        // Pipes and hashes inside quoted values must not split commands or comments in a
        // way that leaves a truncated-but-valid version behind.
        Assert.Equal(2, result.Rows.Count);
        Assert.All(result.Rows, row => Assert.Null(row.Version));
        Assert.Equal(2, result.Gaps.Count(gap => gap.Kind == "ci-producer-version-unsafe"));
        Assert.DoesNotContain(result.Rows, row => row.Version == "1.0.0+meta");
    }

    [Fact]
    public void Env_declared_after_jobs_or_steps_still_resolves()
    {
        const string workflow = """
            jobs:
              release:
                steps:
                  - run: dotnet pack src/Core.csproj -p:PackageId=Contoso.Later -p:PackageVersion=${{ env.PKG_VERSION }}
                env:
                  PKG_VERSION: 5.5.5
            env:
              JOB_FALLBACK: 1.0.0
            """;
        using var temp = new TempDirectory();
        WriteWorkflow(temp.Path, "pack.yml", workflow);
        WriteProject(temp.Path, "src/Core.csproj", PureCiProject);

        var result = Read(temp.Path);
        var row = Assert.Single(result.Rows);
        Assert.Equal("5.5.5", row.Version); // job env declared after steps, resolved post-parse
        Assert.Empty(result.Gaps);
    }

    [Fact]
    public void Working_directory_resolves_relative_pack_targets()
    {
        const string declaredProject = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <PackageId>Contoso.InSrc</PackageId>
              </PropertyGroup>
            </Project>
            """;
        // GitHub resolves every working-directory against the workspace root; step values
        // replace (never chain onto) the defaults value.
        const string workflow = """
            defaults:
              run:
                working-directory: src
            jobs:
              steplevel:
                steps:
                  - run: dotnet pack InSrc.csproj -p:PackageId=Contoso.StepWd -p:PackageVersion=1.0.0
                    working-directory: nested
              jobdefault:
                steps:
                  - run: dotnet pack InSrc.csproj
              templated:
                steps:
                  - run: dotnet pack InSrc.csproj -p:PackageId=Contoso.UnresolvedWd
                    working-directory: ${{ matrix.dir }}
            """;
        using var temp = new TempDirectory();
        WriteWorkflow(temp.Path, "pack.yml", workflow);
        WriteProject(temp.Path, "nested/InSrc.csproj", declaredProject);
        WriteProject(temp.Path, "src/InSrc.csproj", declaredProject);

        var result = Read(temp.Path);
        Assert.Empty(result.Gaps);
        // Step working-directory (workspace-relative) resolves the relative target; the
        // literal id keeps the fact and the attribution follows the effective directory.
        var step = Assert.Single(result.Rows, row => row.Package == "Contoso.StepWd");
        Assert.Equal("nested/InSrc.csproj", step.ProjectPath);
        // The job default (inherited from workflow defaults) resolves a project-declared id.
        var declared = Assert.Single(result.Rows, row => row.Package == "Contoso.InSrc");
        Assert.Equal("src/InSrc.csproj", declared.ProjectPath);
        // A templated working-directory cannot attribute a relative target: the literal id
        // fact survives without projectPath, never a guessed path.
        var templated = Assert.Single(result.Rows, row => row.Package == "Contoso.UnresolvedWd");
        Assert.Null(templated.ProjectPath);
    }

    [Fact]
    public void Unprovable_working_directory_gaps_project_fallback_ids()
    {
        const string workflow = """
            jobs:
              build:
                steps:
                  - run: dotnet pack Core.csproj
                    working-directory: ${{ env.SUBDIR }}
            """;
        using var temp = new TempDirectory();
        WriteWorkflow(temp.Path, "pack.yml", workflow);
        WriteProject(temp.Path, "src/Core.csproj", PureCiProject);

        var result = Read(temp.Path);
        Assert.Empty(result.Rows);
        var gap = Assert.Single(result.Gaps, gap => gap.Kind == "ci-producer-id-unevidenced");
        Assert.Contains("unprovable working directory", gap.Detail!, StringComparison.Ordinal);
    }

    [Fact]
    public void Malformed_utf8_is_an_invalid_workflow_gap()
    {
        using var temp = new TempDirectory();
        var file = Path.Combine(temp.Path, ".github", "workflows", "pack.yml");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        var prefix = "jobs:\n  build:\n    steps:\n      - run: dotnet pack src/Core.csproj -p:PackageId=Contoso.Bad"
            .Select(c => (byte)c).ToList();
        prefix.AddRange([0xFF, 0xFE, 0x00]); // invalid UTF-8 payload
        File.WriteAllBytes(file, prefix.ToArray());

        var result = Read(temp.Path);
        Assert.Empty(result.Rows);
        Assert.Contains(result.Gaps, gap => gap.Kind == "ci-workflow-invalid"
            && gap.Path.EndsWith("pack.yml", StringComparison.Ordinal));
    }

    [Fact]
    public void Literal_flag_identity_is_complete_evidence_without_a_target()
    {
        // The pinned upgrade-authority contract: -p:PackageId + version flags are complete
        // ci-defined identity evidence on their own. The project target is attribution
        // metadata — omitted when the command names none, never a reason to drop the fact
        // to a gap.
        const string workflow = """
            jobs:
              build:
                steps:
                  - run: dotnet pack -p:PackageId=Contoso.Targetless -p:PackageVersion=3.1.4
                  - run: dotnet pack Contoso.sln -p:PackageId=Contoso.SlnScoped -p:PackageVersion=2.2.2
            """;
        using var temp = new TempDirectory();
        WriteWorkflow(temp.Path, "pack.yml", workflow);
        WriteProject(temp.Path, "src/Core.csproj", PureCiProject);

        var result = Read(temp.Path);
        Assert.Equal(2, result.Rows.Count);
        Assert.All(result.Rows, row =>
        {
            Assert.Null(row.ProjectPath);
            Assert.False(string.IsNullOrEmpty(row.Package));
        });
        Assert.Contains(result.Rows, row => row.Package == "Contoso.Targetless" && row.Version == "3.1.4");
        Assert.Contains(result.Rows, row => row.Package == "Contoso.SlnScoped" && row.Version == "2.2.2");
        Assert.Empty(result.Gaps);
    }

    [Fact]
    public void Shell_comments_cannot_overwrite_flag_values()
    {
        const string workflow = """
            jobs:
              build:
                steps:
                  - run: |
                      dotnet pack src/Core.csproj -p:PackageId=Contoso.Good # -p:PackageId=Contoso.Bad
                  - run: >
                      dotnet pack src/Core.csproj -p:PackageId=Contoso.FoldedGood
                      # -p:PackageId=Contoso.FoldedBad
                  - run: dotnet pack src/Core.csproj -p:PackageId=Contoso.Keep # see docs: run full builds
            """;
        using var temp = new TempDirectory();
        WriteWorkflow(temp.Path, "pack.yml", workflow);
        WriteProject(temp.Path, "src/Core.csproj", PureCiProject);

        var result = Read(temp.Path);
        Assert.Empty(result.Gaps);
        Assert.Equal(3, result.Rows.Count);
        Assert.DoesNotContain(result.Rows, row => row.Package.Contains("Bad", StringComparison.Ordinal));
        Assert.Contains(result.Rows, row => row.Package == "Contoso.Good");
        Assert.Contains(result.Rows, row => row.Package == "Contoso.FoldedGood");
        Assert.Contains(result.Rows, row => row.Package == "Contoso.Keep"); // mid-word # is not a comment
    }

    [Fact]
    public void Non_command_shells_do_not_become_producer_evidence()
    {
        const string workflow = """
            jobs:
              scripted:
                steps:
                  - run: |
                      dotnet pack src/Core.csproj -p:PackageId=Contoso.Python -p:PackageVersion=1.0.0
                    shell: python
              powershell:
                steps:
                  - run: dotnet pack src/Core.csproj -p:PackageId=Contoso.Pwsh -p:PackageVersion=1.0.0
                    shell: pwsh
              unprovable:
                steps:
                  - run: dotnet pack src/Core.csproj -p:PackageId=Contoso.UnknownShell
                    shell: ${{ matrix.shell }}
            """;
        using var temp = new TempDirectory();
        WriteWorkflow(temp.Path, "pack.yml", workflow);
        WriteProject(temp.Path, "src/Core.csproj", PureCiProject);

        var result = Read(temp.Path);
        // A command shell keeps its evidence; python and templated shells never produce facts.
        var row = Assert.Single(result.Rows);
        Assert.Equal("Contoso.Pwsh", row.Package);
        Assert.Equal(2, result.Gaps.Count(gap => gap.Kind == "ci-workflow-unsupported"
            && gap.Detail!.Contains("non-command shell", StringComparison.Ordinal)));
    }

    [Fact]
    public void Defaults_declared_after_jobs_still_resolve_working_directories()
    {
        const string declaredProject = """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <PackageId>Contoso.LateDefault</PackageId>
              </PropertyGroup>
            </Project>
            """;
        const string workflow = """
            jobs:
              build:
                steps:
                  - run: dotnet pack LateDefault.csproj
            defaults:
              run:
                working-directory: late
            """;
        using var temp = new TempDirectory();
        WriteWorkflow(temp.Path, "pack.yml", workflow);
        WriteProject(temp.Path, "late/LateDefault.csproj", declaredProject);

        var result = Read(temp.Path);
        var row = Assert.Single(result.Rows);
        Assert.Equal("Contoso.LateDefault", row.Package);
        Assert.Equal("late/LateDefault.csproj", row.ProjectPath);
        Assert.Empty(result.Gaps);
    }

    [Fact]
    public void Package_identities_group_case_insensitively()
    {
        const string upper = """
            jobs:
              a:
                steps:
                  - run: dotnet pack src/Core.csproj -p:PackageId=Contoso.Case -p:PackageVersion=1.0.0
            """;
        const string lower = """
            jobs:
              b:
                steps:
                  - run: dotnet pack src/Core.csproj -p:PackageId=contoso.case -p:PackageVersion=1.0.0
            """;
        const string lowerBumped = """
            jobs:
              c:
                steps:
                  - run: dotnet pack src/Core.csproj -p:PackageId=contoso.case -p:PackageVersion=2.0.0
            """;
        using var temp = new TempDirectory();
        WriteProject(temp.Path, "src/Core.csproj", PureCiProject);
        WriteWorkflow(temp.Path, "a-upper.yml", upper);
        WriteWorkflow(temp.Path, "b-lower.yml", lower);

        var collapsed = Read(temp.Path);
        var row = Assert.Single(collapsed.Rows); // NuGet ids are case-insensitive: one key
        Assert.Equal("Contoso.Case", row.Package); // deterministic display casing from the first occurrence
        Assert.Empty(collapsed.Gaps);

        WriteWorkflow(temp.Path, "c-bumped.yml", lowerBumped);
        var conflicted = Read(temp.Path);
        Assert.Empty(conflicted.Rows);
        var gap = Assert.Single(conflicted.Gaps, gap => gap.Kind == "ci-producer-version-conflict");
        Assert.Contains("1.0.0", gap.Detail!, StringComparison.Ordinal);
        Assert.Contains("2.0.0", gap.Detail!, StringComparison.Ordinal);
    }

    [Fact]
    public void Shell_pipes_subshells_and_templates_do_not_invent_pack_commands()
    {
        const string workflow = """
            jobs:
              build:
                steps:
                  - run: echo "dotnet pack fake" | tee log.txt
                  - run: dotnet --version
                  - run: dotnet msbuild /t:Pack src/Core.csproj
                  - run: echo ${{ contains(github.ref, 'pack') && 'yes' || 'no' }}
            """;
        using var temp = new TempDirectory();
        WriteWorkflow(temp.Path, "pack.yml", workflow);
        WriteProject(temp.Path, "src/Core.csproj", PureCiProject);

        var result = Read(temp.Path);
        Assert.Empty(result.Rows);
        Assert.Empty(result.Gaps);
    }

    [Fact]
    public void Common_workflow_yaml_shapes_stay_parseable()
    {
        const string workflow = """
        name: CI # inline comment
        on:
          push:
            branches: [ main, 'release/*' ]
          pull_request:
        env:
          WORKFLOW_LEVEL: only-workflow
        jobs:
          build:
            strategy:
              matrix:
                os: [ubuntu-latest, windows-latest]
            runs-on: ${{ matrix.os }}
            if: ${{ github.event_name == 'push' }}
            steps:
              - uses: actions/checkout@v4
                with:
                  fetch-depth: 0
              -
                name: pack
                env:
                  STEP_LEVEL: step
                run: |
                  # a shell comment, not a YAML comment
                  dotnet pack src/Core.csproj -p:PackageId=Contoso.Shapes -p:PackageVersion=1.0.1
                working-directory: src
              - run: echo 'single quoted with: colon'
              - run: echo "double quoted with # hash"
              - plain-scalar-step-is-ignored
        """;
        using var temp = new TempDirectory();
        WriteWorkflow(temp.Path, "pack.yml", workflow.Replace("\r\n", "\n"));
        WriteProject(temp.Path, "src/Core.csproj", PureCiProject);

        var result = Read(temp.Path);
        Assert.Empty(result.Gaps);
        var row = Assert.Single(result.Rows);
        Assert.Equal("Contoso.Shapes", row.Package);
        Assert.Equal("1.0.1", row.Version);
    }

    [Fact]
    public void Unsupported_yaml_constructs_reduce_coverage_without_breaking_other_workflows()
    {
        const string anchored = """
            defaults: &anchor
              run:
                shell: pwsh
            jobs:
              build:
                steps:
                  - run: dotnet pack src/Core.csproj -p:PackageId=Contoso.Never
            """;
        using var temp = new TempDirectory();
        WriteProject(temp.Path, "src/Core.csproj", PureCiProject);
        WriteWorkflow(temp.Path, "anchored.yml", anchored);
        WriteWorkflow(temp.Path, "valid.yml", PureCiWorkflow);

        var result = Read(temp.Path);
        var row = Assert.Single(result.Rows);
        Assert.Equal("Contoso.Ci", row.Package); // the valid workflow still contributes
        var gap = Assert.Single(result.Gaps, g => g.Path.EndsWith("anchored.yml", StringComparison.Ordinal));
        Assert.Equal("ci-workflow-unsupported", gap.Kind);
    }

    [Fact]
    public void Resource_caps_are_absolute()
    {
        using var temp = new TempDirectory();
        WriteProject(temp.Path, "src/Core.csproj", PureCiProject);
        for (var i = 0; i < CiWorkflowProducerExtractor.MaxFiles; i++)
            WriteWorkflow(temp.Path, $"w{i:D2}.yml", $"""
                jobs:
                  build:
                    steps:
                      - run: dotnet pack src/Core.csproj -p:PackageId=Contoso.{i}
                """);
        var capped = Read(temp.Path);
        Assert.Equal(CiWorkflowProducerExtractor.MaxFiles, capped.Rows.Count);
        Assert.Empty(capped.Gaps);
        WriteWorkflow(temp.Path, "overflow.yml", PureCiWorkflow);
        var overflow = Read(temp.Path);
        Assert.Empty(overflow.Rows); // Overflow must not select a filesystem-order-dependent subset.
        Assert.Contains(overflow.Gaps, gap => gap.Kind == "ci-workflow-file-limit");

        using var large = new TempDirectory();
        WriteProject(large.Path, "src/Core.csproj", PureCiProject);
        WriteWorkflow(large.Path, "huge.yml", "jobs:\n  build:\n    steps:\n      - run: |\n" +
            new string(' ', 4) + "# " + new string('x', CiWorkflowProducerExtractor.MaxFileBytes));
        var bytes = Read(large.Path);
        Assert.Empty(bytes.Rows);
        Assert.Contains(bytes.Gaps, gap => gap.Kind == "ci-workflow-byte-limit");
    }

    [Fact]
    public void Discovery_honors_exclusions_links_and_the_output_boundary()
    {
        using var temp = new TempDirectory();
        WriteProject(temp.Path, "src/Core.csproj", PureCiProject);
        WriteWorkflow(temp.Path, "pack.yml", PureCiWorkflow);
        var excluded = Read(temp.Path, new ScanOptions(temp.Path, temp.Path + "-out",
            ExcludeGlobs: ["**/pack.yml"]));
        Assert.Empty(excluded.Rows);
        Assert.Equal("ci-workflow-not-found", Assert.Single(excluded.Gaps).Kind);

        var boundary = Read(temp.Path, new ScanOptions(temp.Path, Path.Combine(temp.Path, ".github", "workflows")));
        Assert.Empty(boundary.Rows);
        Assert.Contains(boundary.Gaps, gap => gap.Kind == "ci-workflow-output-boundary");

        if (!OperatingSystem.IsWindows())
        {
            var other = new TempDirectory();
            WriteProject(other.Path, "src/Core.csproj", PureCiProject);
            Directory.CreateDirectory(Path.Combine(other.Path, ".github", "workflows"));
            var real = Path.Combine(other.Path, ".github", "workflows", "real.yml");
            WriteWorkflow(other.Path, "real.yml", PureCiWorkflow);
            var link = Path.Combine(other.Path, ".github", "workflows", "link.yml");
            File.CreateSymbolicLink(link, real);
            var linked = Read(other.Path);
            var row = Assert.Single(linked.Rows);
            Assert.EndsWith("real.yml", row.WorkflowPath, StringComparison.Ordinal);
            Assert.Contains(linked.Gaps, gap => gap.Kind == "ci-workflow-linked-path");
        }
    }

    [Fact]
    public async Task Cli_opt_in_emits_ci_producer_facts()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        WriteProject(repo, "src/Core.csproj", PureCiProject);
        WriteWorkflow(repo, "pack.yml", PureCiWorkflow);
        InitGit(repo);
        using var output = new StringWriter();
        using var error = new StringWriter();
        var destination = Path.Combine(temp.Path, "out");
        Assert.Equal(0, await TraceMapCommand.RunAsync(
            ["scan", "--repo", repo, "--out", destination, "--index-ci-producers"], output, error));
        var scan = ScanEngine.Scan(new ScanOptions(repo, destination) { IndexCiProducers = true });
        var fact = Assert.Single(scan.Facts, fact => fact.Properties.GetValueOrDefault("sourceKind") == "ci-workflow");
        Assert.Equal("Contoso.Ci", fact.Properties["package"]);
        Assert.True(File.Exists(Path.Combine(destination, "facts.ndjson")));
        Assert.Contains("ci-workflow", await File.ReadAllTextAsync(Path.Combine(destination, "facts.ndjson")));
    }

    [Fact]
    public void Partial_ci_evidence_reduces_scan_coverage()
    {
        const string workflow = """
            jobs:
              build:
                steps:
                  - run: dotnet pack src/Core.csproj -p:PackageId=Contoso.Gap -p:PackageVersion=${{ needs.meta.outputs.version }}
            """;
        using var temp = new TempDirectory();
        WriteWorkflow(temp.Path, "pack.yml", workflow);
        WriteProject(temp.Path, "src/Core.csproj", PureCiProject);
        InitGit(temp.Path);

        var scan = ScanEngine.Scan(new ScanOptions(temp.Path, temp.Path + "-out") { IndexCiProducers = true });
        Assert.EndsWith("Reduced", scan.Manifest.AnalysisLevel, StringComparison.Ordinal);
        Assert.Equal("FailedOrPartial", scan.Manifest.BuildStatus);
        Assert.Contains(scan.Facts, fact => fact.Properties.GetValueOrDefault("reason")
            == "Package/dependency evidence coverage is partial; inspect package manifest gaps and any separate semantic diagnostics.");
        var fact = Assert.Single(scan.Facts, fact => fact.Properties.GetValueOrDefault("sourceKind") == "ci-workflow");
        Assert.False(fact.Properties.ContainsKey("version"));
        var gap = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.AnalysisGap
            && fact.Properties.GetValueOrDefault("gapKind") == "ci-producer-version-template");
        Assert.Contains("needs.meta.outputs.version", gap.Properties["message"], StringComparison.Ordinal);
        Assert.Contains("CI workflow producer analysis reported", Assert.Single(scan.Manifest.KnownGaps,
            knownGap => knownGap.Contains("ci-producer-version-template", StringComparison.Ordinal)));
    }

    [Fact]
    public void Cancelled_discovery_is_explicit()
    {
        using var temp = new TempDirectory();
        WriteWorkflow(temp.Path, "pack.yml", PureCiWorkflow);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => CiWorkflowProducerExtractor.Read(
            new ScanOptions(temp.Path, temp.Path + "-out"), [], [], cancellation.Token));
    }

    [Fact]
    public void Workflow_reading_does_not_change_the_source_inventory()
    {
        using var temp = new TempDirectory();
        WriteWorkflow(temp.Path, "pack.yml", PureCiWorkflow);
        WriteProject(temp.Path, "src/Core.csproj", PureCiProject);
        InitGit(temp.Path);
        var off = ScanEngine.Scan(new ScanOptions(temp.Path, temp.Path + "-off"));
        var on = ScanEngine.Scan(new ScanOptions(temp.Path, temp.Path + "-on") { IndexCiProducers = true });
        Assert.Equal(off.Inventory, on.Inventory);
        Assert.Equal(off.SourceSnapshotInventory, on.SourceSnapshotInventory);
    }

    [Fact]
    public void Committed_fact_shape_matches_the_pinned_upgrade_authority_contract()
    {
        using var temp = new TempDirectory();
        WriteWorkflow(temp.Path, "pack.yml", PureCiWorkflow);
        WriteProject(temp.Path, "src/Core.csproj", PureCiProject);
        InitGit(temp.Path);

        var scan = ScanEngine.Scan(new ScanOptions(temp.Path, temp.Path + "-out") { IndexCiProducers = true });
        var fact = Assert.Single(scan.Facts, fact => fact.Properties.GetValueOrDefault("sourceKind") == "ci-workflow");
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(fact));
        var properties = json.RootElement.GetProperty("Properties");
        // The upgrade-authority fusion contract: provenance rides sourceKind=ci-workflow.
        foreach (var key in new[] { "dependencyGroup", "ecosystem", "manifestKind", "package", "packageManager",
                     "packageName", "projectPath", "sourceKind", "surfaceKind", "version", "workflowPath",
                     "generatorSha256", "boundedInputSha256" })
            Assert.True(properties.TryGetProperty(key, out _), $"missing contract property {key}");
        Assert.Equal("PackageProduced", json.RootElement.GetProperty("FactType").GetString());
        Assert.Equal("package-config", properties.GetProperty("surfaceKind").GetString());
    }

    [Theory]
    [InlineData("${{ matrix.version }}")]
    [InlineData("''")]
    [InlineData("")]
    [InlineData("|")]
    public void Unknown_inner_env_masks_literal_parent(string value)
    {
        using var temp = new TempDirectory();
        WriteWorkflow(temp.Path, "pack.yml", $$$"""
            env:
              VERSION: 1.0.0
            jobs:
              build:
                env:
                  VERSION: {{{value}}}
                steps:
                  - run: dotnet pack -p:PackageId=Contoso.Job -p:PackageVersion=${{ env.VERSION }}
                  - env:
                      VERSION: {{{value}}}
                    run: dotnet pack -p:PackageId=Contoso.Step -p:PackageVersion=${{ env.VERSION }}
            """);
        var result = Read(temp.Path);
        Assert.Equal(2, result.Rows.Count);
        Assert.All(result.Rows, row => Assert.Null(row.Version));
        Assert.Equal(2, result.Gaps.Count);
    }

    [Theory]
    [InlineData("&&")]
    [InlineData("||")]
    public void Pack_after_double_operator_is_indexed(string separator)
    {
        using var temp = new TempDirectory();
        WriteWorkflow(temp.Path, "pack.yml", $$$"""
            jobs:
              build:
                steps:
                  - run: echo ready {{{separator}}} dotnet pack -p:PackageId=Contoso.Chained -p:PackageVersion=2.0.0
            """);
        Assert.Equal("Contoso.Chained", Assert.Single(Read(temp.Path).Rows).Package);
    }

    [Fact]
    public void Linked_github_parent_is_not_repository_evidence()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        var outside = Path.Combine(temp.Path, "outside");
        Directory.CreateDirectory(repo);
        WriteWorkflow(outside, "pack.yml", PureCiWorkflow);
        using var link = DirectoryLinkFixture.Create(Path.Combine(repo, ".github"), Path.Combine(outside, ".github"));
        var result = Read(repo);
        Assert.Empty(result.Rows);
        Assert.Contains(result.Gaps, gap => gap.Kind == "ci-workflow-linked-path");
    }

    [Theory]
    [InlineData("cat <<'EOF'\n      dotnet pack -p:PackageId=Contoso.Data\n      EOF")]
    [InlineData("echo \"data\n      dotnet pack -p:PackageId=Contoso.Data\n      \"")]
    public void Multiline_shell_data_is_not_a_pack_definition(string script)
    {
        using var temp = new TempDirectory();
        WriteWorkflow(temp.Path, "pack.yml", "jobs:\n  build:\n    steps:\n      - run: |\n          "
            + script.Replace("\n      ", "\n          ", StringComparison.Ordinal));
        var result = Read(temp.Path);
        Assert.Empty(result.Rows);
        Assert.Contains(result.Gaps, gap => gap.Kind == "ci-workflow-unsupported");
    }

    [Theory]
    [InlineData("env: { VERSION: '${{ matrix.version }}' }")]
    [InlineData("defaults: { run: { shell: python } }")]
    public void Opaque_effective_scope_is_not_silently_ignored(string scope)
    {
        using var temp = new TempDirectory();
        WriteWorkflow(temp.Path, "pack.yml", $$$"""
            env:
              VERSION: 1.0.0
            jobs:
              build:
                {{{scope}}}
                steps:
                  - run: dotnet pack -p:PackageId=Contoso.Opaque -p:PackageVersion=${{ env.VERSION }}
            """);
        var result = Read(temp.Path);
        Assert.Empty(result.Rows);
        Assert.Contains(result.Gaps, gap => gap.Kind == "ci-workflow-unsupported");
    }

    [Fact]
    public void Input_digest_binds_referenced_project_identity()
    {
        using var temp = new TempDirectory();
        WriteWorkflow(temp.Path, "pack.yml", "jobs:\n  build:\n    steps:\n      - run: dotnet pack src/Core.csproj");
        WriteProject(temp.Path, "src/Core.csproj", "<Project><PropertyGroup><PackageId>Contoso.One</PackageId></PropertyGroup></Project>");
        var first = Read(temp.Path);
        WriteProject(temp.Path, "src/Core.csproj", "<Project><PropertyGroup><PackageId>Contoso.Two</PackageId></PropertyGroup></Project>");
        var second = Read(temp.Path);
        Assert.Equal("Contoso.One", Assert.Single(first.Rows).Package);
        Assert.Equal("Contoso.Two", Assert.Single(second.Rows).Package);
        Assert.NotEqual(first.BoundedInputSha256, second.BoundedInputSha256);
        Assert.Equal(second.BoundedInputSha256, Read(temp.Path).BoundedInputSha256);
    }

    [Fact]
    public void Collapsed_row_keeps_version_and_project_with_their_actual_evidence_span()
    {
        using var temp = new TempDirectory();
        WriteWorkflow(temp.Path, "a.yml", "jobs:\n  build:\n    steps:\n      - run: dotnet pack -p:PackageId=Contoso.Dup");
        WriteWorkflow(temp.Path, "b.yml", "jobs:\n  build:\n    steps:\n      - run: dotnet pack src/Core.csproj -p:PackageId=Contoso.Dup -p:PackageVersion=2.0.0");
        var row = Assert.Single(Read(temp.Path).Rows);
        Assert.Equal("2.0.0", row.Version);
        Assert.Equal("src/Core.csproj", row.ProjectPath);
        Assert.Equal(".github/workflows/b.yml", row.WorkflowPath);
    }

    [Fact]
    public void Empty_property_override_cannot_reuse_an_earlier_literal()
    {
        using var temp = new TempDirectory();
        WriteWorkflow(temp.Path, "pack.yml", "jobs:\n  build:\n    steps:\n      - run: dotnet pack -p:PackageId=Contoso.Old -p:PackageId=");
        var result = Read(temp.Path);
        Assert.Empty(result.Rows);
        Assert.Contains(result.Gaps, gap => gap.Kind == "ci-producer-id-unevidenced");
    }

    [Theory]
    [InlineData("jobs:\n  build:\n    steps:\n      - run: \"dotnet pack -p:PackageId=Contoso.\\u0041\"")]
    [InlineData("jobs:\n  build:\n    steps:\n      - run: >\n          dotnet pack -p:PackageId=Contoso.Good\n\n          -p:PackageId=Contoso.Bad")]
    [InlineData("jobs:\n  build:\n    steps:\n      - run: >\n          dotnet pack -p:PackageId=Contoso.Good\n            -p:PackageId=Contoso.Bad")]
    public void Unmodeled_yaml_text_transformations_are_gaps(string workflow)
    {
        using var temp = new TempDirectory();
        WriteWorkflow(temp.Path, "pack.yml", workflow);
        var result = Read(temp.Path);
        Assert.Empty(result.Rows);
        Assert.Contains(result.Gaps, gap => gap.Kind == "ci-workflow-unsupported");
    }

    [Theory]
    [InlineData("-c=Release -p:PackageId=Contoso.New")]
    [InlineData("/p:PackageId=Contoso.New")]
    [InlineData("/property:PackageId=Contoso.New")]
    [InlineData("-property:PackageId=Contoso.New")]
    public void Property_overrides_are_not_skipped_by_option_spelling(string flags)
    {
        using var temp = new TempDirectory();
        WriteProject(temp.Path, "src/Core.csproj", "<Project><PropertyGroup><PackageId>Contoso.Old</PackageId></PropertyGroup></Project>");
        WriteWorkflow(temp.Path, "pack.yml", "jobs:\n  build:\n    steps:\n      - run: dotnet pack src/Core.csproj " + flags);
        Assert.Equal("Contoso.New", Assert.Single(Read(temp.Path).Rows).Package);
    }

    [Fact]
    public void Grouped_property_override_is_not_a_literal_project_fallback()
    {
        using var temp = new TempDirectory();
        WriteProject(temp.Path, "src/Core.csproj", "<Project><PropertyGroup><PackageId>Contoso.Old</PackageId></PropertyGroup></Project>");
        WriteWorkflow(temp.Path, "pack.yml", "jobs:\n  build:\n    steps:\n      - run: dotnet pack src/Core.csproj \"-p:Version=2.0.0;PackageId=Contoso.New\"");
        var result = Read(temp.Path);
        Assert.Empty(result.Rows);
        Assert.Contains(result.Gaps, gap => gap.Kind == "ci-producer-id-unevidenced");
    }

    [Fact]
    public void Fifo_workflow_does_not_wait_for_a_writer()
    {
        if (!OperatingSystem.IsMacOS() && !OperatingSystem.IsLinux()) return;
        using var temp = new TempDirectory();
        var workflows = Path.Combine(temp.Path, ".github", "workflows");
        Directory.CreateDirectory(workflows);
        Run(temp.Path, "mkfifo", Path.Combine(workflows, "pipe.yml"));
        var result = Read(temp.Path);
        Assert.Empty(result.Rows);
        Assert.Contains(result.Gaps, gap => gap.Kind == "ci-workflow-read-failed");
    }

    [Fact]
    public void Powershell_continuation_cannot_emit_the_superseded_id()
    {
        using var temp = new TempDirectory();
        WriteWorkflow(temp.Path, "pack.yml", "jobs:\n  build:\n    steps:\n      - shell: pwsh\n        run: |\n          dotnet pack -p:PackageId=Contoso.Old `\n            -p:PackageId=Contoso.New");
        var result = Read(temp.Path);
        Assert.Empty(result.Rows);
        Assert.Contains(result.Gaps, gap => gap.Kind == "ci-workflow-unsupported");
    }

    [Fact]
    public void Folded_comment_applies_after_folding_the_whole_shell_line()
    {
        using var temp = new TempDirectory();
        WriteWorkflow(temp.Path, "pack.yml", "jobs:\n  build:\n    steps:\n      - run: >\n          dotnet pack -p:PackageId=Contoso.Good #\n          -p:PackageId=Contoso.Bad");
        Assert.Equal("Contoso.Good", Assert.Single(Read(temp.Path).Rows).Package);
    }

    [Theory]
    [InlineData("Contoso\"Pipe|Id\"")]
    [InlineData("Contoso'Pipe|Id'")]
    [InlineData("Contoso\"Semi;Id\"")]
    [InlineData("Contoso\"Hash # Id\"")]
    [InlineData("Contoso\\|Id")]
    public void Embedded_shell_quotes_and_escapes_cannot_invent_a_truncated_safe_id(string id)
    {
        using var temp = new TempDirectory();
        WriteWorkflow(temp.Path, "pack.yml", "jobs:\n  build:\n    steps:\n      - run: |\n          dotnet pack -p:PackageId=" + id);
        var result = Read(temp.Path);
        Assert.Empty(result.Rows);
        Assert.Contains(result.Gaps, gap => gap.Kind == "ci-producer-id-unevidenced");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Discovery_stops_enumerating_at_its_absolute_bound(bool workflows)
    {
        using var temp = new TempDirectory();
        var limit = workflows ? CiWorkflowProducerExtractor.MaxFiles : CiWorkflowProducerExtractor.MaxDiscoveryEntries;
        var yielded = 0;
        IEnumerable<string> Entries()
        {
            for (var i = 0; i <= limit; i++)
            {
                yielded++;
                yield return Path.Combine(temp.Path, ".github", "workflows", $"{i}." + (workflows ? "yml" : "txt"));
            }
            throw new InvalidOperationException("enumerated beyond the bounded overflow witness");
        }
        var candidates = CiWorkflowProducerExtractor.DiscoverCandidates(Entries(), temp.Path, [],
            StringComparer.Ordinal, default, out var gap);
        Assert.Empty(candidates);
        Assert.Equal(limit + 1, yielded);
        Assert.Equal("ci-workflow-file-limit", gap);
    }

    [Fact]
    public void Discovery_overflow_has_one_deterministic_gap_regardless_of_enumeration_order()
    {
        using var temp = new TempDirectory();
        var entries = Enumerable.Range(0, CiWorkflowProducerExtractor.MaxFiles + 1)
            .Select(i => Path.Combine(temp.Path, $"{i}.yml"))
            .Concat(Enumerable.Range(0, CiWorkflowProducerExtractor.MaxDiscoveryEntries + 1)
                .Select(i => Path.Combine(temp.Path, $"{i}.txt"))).ToArray();
        Assert.Empty(CiWorkflowProducerExtractor.DiscoverCandidates(entries, temp.Path, [],
            StringComparer.Ordinal, default, out var forward));
        Assert.Empty(CiWorkflowProducerExtractor.DiscoverCandidates(entries.Reverse(), temp.Path, [],
            StringComparer.Ordinal, default, out var reverse));
        Assert.Equal("ci-workflow-file-limit", forward);
        Assert.Equal(forward, reverse);
    }

    [Fact] // review P1 (Codex + Baz): <IsPackable>$(PackEnabled)</IsPackable> is neither literal — never guessed packable
    public void Non_literal_ispackable_refuses_to_derive()
    {
        const string project = "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup>\n    <TargetFramework>net10.0</TargetFramework>\n    <IsPackable>$(PackEnabled)</IsPackable>\n  </PropertyGroup>\n</Project>";
        const string workflow = "jobs:\n  build:\n    steps:\n      - run: dotnet pack src/Dyn.csproj";
        using var temp = new TempDirectory();
        WriteWorkflow(temp.Path, "pack.yml", workflow);
        WriteProject(temp.Path, "src/Dyn.csproj", project);

        var result = Read(temp.Path);

        var gap = Assert.Single(result.Gaps, g => g.Kind == "ci-producer-id-unevidenced");
        Assert.Contains("non-literal", gap.Detail, StringComparison.Ordinal);
        Assert.Empty(result.Rows);
    }

    [Fact] // review: an inherited non-literal IsPackable (nearest Directory.Build.props) is equally unprovable
    public void Inherited_non_literal_ispackable_refuses_to_derive()
    {
        const string bareProject = "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup>\n    <TargetFramework>net10.0</TargetFramework>\n  </PropertyGroup>\n</Project>";
        const string workflow = "jobs:\n  build:\n    steps:\n      - run: dotnet pack src/Bare.csproj";
        using var temp = new TempDirectory();
        WriteWorkflow(temp.Path, "pack.yml", workflow);
        WriteProject(temp.Path, "src/Bare.csproj", bareProject);
        WriteProject(temp.Path, "Directory.Build.props", "<Project>\n  <PropertyGroup>\n    <IsPackable>$(PackEnabled)</IsPackable>\n  </PropertyGroup>\n</Project>");

        var result = Read(temp.Path);

        var gap = Assert.Single(result.Gaps, g => g.Kind == "ci-producer-id-unevidenced");
        Assert.Contains("non-literal", gap.Detail, StringComparison.Ordinal);
        Assert.Empty(result.Rows);
    }

    [Fact] // review P1 (Codex): a $(...) Import path bypassed the .props/.targets suffix guard
    public void Expression_import_refuses_to_derive()
    {
        const string project = "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup>\n    <TargetFramework>net10.0</TargetFramework>\n  </PropertyGroup>\n  <Import Project=\"$(IdentityImport)\" />\n</Project>";
        const string workflow = "jobs:\n  build:\n    steps:\n      - run: dotnet pack src/DynImport.csproj";
        using var temp = new TempDirectory();
        WriteWorkflow(temp.Path, "pack.yml", workflow);
        WriteProject(temp.Path, "src/DynImport.csproj", project);

        var result = Read(temp.Path);

        var gap = Assert.Single(result.Gaps, g => g.Kind == "ci-producer-id-unevidenced");
        Assert.Contains("Import", gap.Detail, StringComparison.Ordinal);
        Assert.Empty(result.Rows);
    }

    [Fact] // review P1 (Codex): a centralized Test.Sdk reference in Directory.Build.props makes every project a test project
    public void Directory_build_test_sdk_marks_projects_test()
    {
        const string bareProject = "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup>\n    <TargetFramework>net10.0</TargetFramework>\n  </PropertyGroup>\n</Project>";
        const string workflow = "jobs:\n  build:\n    steps:\n      - run: dotnet pack src/Bare.csproj";
        using var temp = new TempDirectory();
        WriteWorkflow(temp.Path, "pack.yml", workflow);
        WriteProject(temp.Path, "src/Bare.csproj", bareProject);
        WriteProject(temp.Path, "Directory.Build.props", "<Project>\n  <ItemGroup>\n    <PackageReference Include=\"Microsoft.NET.Test.Sdk\" Version=\"17.0.0\" />\n  </ItemGroup>\n</Project>");

        var result = Read(temp.Path);

        var gap = Assert.Single(result.Gaps, g => g.Kind == "ci-producer-id-unevidenced");
        Assert.Contains("Microsoft.NET.Test.Sdk", gap.Detail, StringComparison.Ordinal);
        Assert.Empty(result.Rows);
    }

    [Fact] // review (Baz): changing an evaluated AssemblyName changes the emitted row — it must move the digest too
    public void Bounded_digest_moves_with_the_resolved_identity()
    {
        const string projectA = "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup>\n    <AssemblyName>Contoso.Alpha</AssemblyName>\n  </PropertyGroup>\n</Project>";
        const string projectB = "<Project Sdk=\"Microsoft.NET.Sdk\">\n  <PropertyGroup>\n    <AssemblyName>Contoso.Beta</AssemblyName>\n  </PropertyGroup>\n</Project>";
        const string workflow = "jobs:\n  build:\n    steps:\n      - run: dotnet pack src/Chain.csproj";
        using var temp = new TempDirectory();
        WriteWorkflow(temp.Path, "pack.yml", workflow);
        WriteProject(temp.Path, "src/Chain.csproj", projectA);
        var before = Read(temp.Path);
        Assert.Single(before.Rows, row => row.Package == "Contoso.Alpha");

        WriteProject(temp.Path, "src/Chain.csproj", projectB);
        var after = Read(temp.Path);
        Assert.Single(after.Rows, row => row.Package == "Contoso.Beta");
        Assert.NotEqual(before.BoundedInputSha256, after.BoundedInputSha256);
    }

    [Fact] // review (Baz): duplicate JSON properties must not silently select one list — typed refusal
    public void Duplicate_hints_properties_are_refused()
    {
        using var temp = new TempDirectory();
        var dupRoot = Path.Combine(temp.Path, "dup-root.json");
        File.WriteAllText(dupRoot, "{\"packWorkflows\":[{\"uses\":\"org/shared-ci/.github/workflows/release.yml\"}],\"packWorkflows\":[]}");
        var exRoot = Assert.Throws<ArgumentException>(() => TraceMap.Cli.Program.ReadCiHints(dupRoot));
        Assert.Contains("duplicate property 'packWorkflows'", exRoot.Message, StringComparison.Ordinal);

        var dupEntry = Path.Combine(temp.Path, "dup-entry.json");
        File.WriteAllText(dupEntry, "{\"packWorkflows\":[{\"uses\":\"org/shared-ci/.github/workflows/release.yml\",\"uses\":\"org/other/.github/workflows/release.yml\"}]}");
        var exEntry = Assert.Throws<ArgumentException>(() => TraceMap.Cli.Program.ReadCiHints(dupEntry));
        Assert.Contains("duplicate property 'uses'", exEntry.Message, StringComparison.Ordinal);
    }

    private static CiWorkflowResult Read(string root, ScanOptions? options = null) => CiWorkflowProducerExtractor.Read(
        options ?? new ScanOptions(root, Path.Combine(root, "out")),
        ProjectFileReader.ReadProducedPackages(root, InventProjects(root)), InventProjects(root), default);

    private static IEnumerable<FileInventoryItem> InventProjects(string root) =>
        Directory.EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories)
            .Select(path => new FileInventoryItem(
                Path.GetRelativePath(root, path).Replace('\\', '/'), "Project", new FileInfo(path).Length))
            .OrderBy(item => item.RelativePath, StringComparer.Ordinal)
            .ToArray();

    private static IEnumerable<string> resultGaps(ScanResult scan) =>
        scan.Facts.Where(fact => fact.FactType == FactTypes.AnalysisGap
                && fact.Evidence.ExtractorId == "CiWorkflowExtractor")
            .Select(fact => fact.Properties.GetValueOrDefault("gapKind") ?? "unknown");

    private static string Normalize(IReadOnlyList<CodeFact> facts) => string.Join('\n', facts
        .Select(fact => string.Join('|',
            fact.FactType,
            fact.RuleId,
            fact.Evidence.FilePath,
            fact.Evidence.StartLine,
            fact.Evidence.EndLine,
            fact.Evidence.ExtractorId,
            string.Join(';', fact.Properties.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => $"{p.Key}={p.Value}"))))
        .Order(StringComparer.Ordinal));

    private static void WriteWorkflow(string root, string relative, string content)
    {
        var file = Path.Combine(root, ".github", "workflows", relative);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, content);
    }

    private static void WriteProject(string root, string relative, string content)
    {
        var file = Path.Combine(root, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, content);
    }

    private static void InitGit(string root)
    {
        File.WriteAllText(Path.Combine(root, "README.md"), "Synthetic CI producer evidence fixture.");
        Run(root, "git", "init");
        Run(root, "git", "add", "README.md");
        Run(root, "git", "-c", "user.name=TraceMap Test", "-c", "user.email=tracemap@example.invalid", "commit", "-m", "fixture");
    }

    private static void Run(string root, params string[] args)
    {
        var info = new System.Diagnostics.ProcessStartInfo(args[0])
        {
            WorkingDirectory = root,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var arg in args[1..]) info.ArgumentList.Add(arg);
        using var process = System.Diagnostics.Process.Start(info)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(120_000)) { process.Kill(entireProcessTree: true); throw new TimeoutException(); }
        Assert.True(process.ExitCode == 0, stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult());
    }
}
