using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class ComReferenceWorkspaceFallbackTests
{
    [Fact]
    public void Prepare_creates_bounded_targets_override_for_declared_com_reference()
    {
        using var temp = new TempDirectory();
        var projectPath = "src/Legacy/Legacy.csproj";
        Directory.CreateDirectory(Path.Combine(temp.Path, "src", "Legacy"));
        File.WriteAllText(Path.Combine(temp.Path, projectPath), ProjectWithComReference());
        var projects = new[] { new FileInventoryItem(projectPath, "Project", 1) };

        string targetsPath;
        using (var fallback = ComReferenceWorkspaceFallback.Prepare(temp.Path, projects))
        {
            Assert.True(fallback.IsActive);
            Assert.Equal(projectPath, Assert.Single(fallback.ProjectPaths));
            targetsPath = Assert.IsType<string>(fallback.TargetsPath);
            var targets = File.ReadAllText(targetsPath);
            Assert.Contains("Name=\"ResolveComReferences\"", targets, StringComparison.Ordinal);
            Assert.Contains("Name=\"ResolveComReferencesDesignTime\"", targets, StringComparison.Ordinal);
            Assert.DoesNotContain(temp.Path, targets, StringComparison.Ordinal);
        }

        Assert.False(File.Exists(targetsPath));
    }

    [Fact]
    public void Prepare_does_not_replace_project_defined_custom_after_targets()
    {
        using var temp = new TempDirectory();
        var projectPath = "src/Legacy/Legacy.csproj";
        Directory.CreateDirectory(Path.Combine(temp.Path, "src", "Legacy"));
        File.WriteAllText(Path.Combine(temp.Path, projectPath), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <CustomAfterMicrosoftCommonTargets>private.targets</CustomAfterMicrosoftCommonTargets>
              </PropertyGroup>
              <ItemGroup>
                <COMReference Include="Synthetic.Legacy.Component">
                  <Guid>{00000000-0000-0000-0000-000000000001}</Guid>
                </COMReference>
              </ItemGroup>
            </Project>
            """);

        using var fallback = ComReferenceWorkspaceFallback.Prepare(
            temp.Path,
            [new FileInventoryItem(projectPath, "Project", 1)]);

        Assert.False(fallback.IsActive);
        Assert.Equal("project-custom-after-targets", fallback.UnavailableReason);
        Assert.Equal(projectPath, Assert.Single(fallback.ProjectPaths));
    }

    [Fact]
    public void Scan_preserves_independent_semantic_evidence_when_com_resolution_is_omitted()
    {
        using var temp = new TempDirectory();
        var projectPath = "Legacy.csproj";
        File.WriteAllText(Path.Combine(temp.Path, projectPath), ProjectWithComReference());
        File.WriteAllText(Path.Combine(temp.Path, "Independent.cs"), """
            namespace Legacy;

            public sealed class Independent
            {
                public string Value { get; set; } = string.Empty;
            }
            """);

        var result = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, ".tracemap")));

        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.CSharpSemanticWorkspace
            && fact.ProjectPath == projectPath
            && fact.Properties.GetValueOrDefault("gapKind") == "ComReferenceResolutionSkipped"
            && fact.Properties.GetValueOrDefault("guidanceCode") == "ReviewComReferenceCoverage");
        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.TypeDeclared
            && fact.EvidenceTier == EvidenceTiers.Tier1Semantic
            && fact.TargetSymbol == "global::Legacy.Independent");
        Assert.DoesNotContain(result.Facts, fact =>
            fact.Properties.GetValueOrDefault("diagnosticCode") == "MSBuildTaskHostIncompatible");
        Assert.Equal("Level1SemanticAnalysisReduced", result.Manifest.AnalysisLevel);
        Assert.Equal("FailedOrPartial", result.Manifest.BuildStatus);
    }

    private static string ProjectWithComReference() => """
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
          </PropertyGroup>
          <ItemGroup>
            <COMReference Include="Synthetic.Legacy.Component">
              <Guid>{00000000-0000-0000-0000-000000000001}</Guid>
              <VersionMajor>1</VersionMajor>
              <VersionMinor>0</VersionMinor>
              <Lcid>0</Lcid>
              <WrapperTool>tlbimp</WrapperTool>
            </COMReference>
          </ItemGroup>
        </Project>
        """;
}
