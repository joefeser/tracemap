using System.Text.Json;
using TraceMap.Core;
using TraceMap.Reporting;

namespace TraceMap.Tests;

public sealed class ProjectlessParameterCandidateTests
{
    [Theory]
    [InlineData("Imports System.Data\nImports System.Data.SqlClient", "SqlCommand", true)]
    [InlineData("Imports System.Data", "SqlClient.SqlCommand", true)]
    [InlineData("Imports C = System.Data.SqlClient.SqlCommand\nImports System.Data", "C", true)]
    [InlineData("Imports N = Microsoft.Data.SqlClient\nImports System.Data", "N.SqlCommand", true)]
    [InlineData("Imports System.Data", "Fake.SqlCommand", false)]
    [InlineData("Imports System.Data", "Object", false)]
    [InlineData("Imports System.Data\nImports System.Data.SqlClient\nImports Microsoft.Data.SqlClient", "SqlCommand", false)]
    [InlineData("Imports System.Data\nImports System.Data.SqlClient\nPublic Class SqlCommand\nEnd Class", "SqlCommand", false)]
    public void Collection_add_retains_only_explicit_command_candidates(string imports, string type, bool expected)
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "Input.vb"), $"{imports}\nPublic Class Example\nSub Run(command As {type})\ncommand.Parameters.Add(\"@PrivateSentinel\", SqlDbType.VarChar)\nEnd Sub\nEnd Class");
        WebFormsWizardNativeTests.InitGit(temp.Path);
        var scan = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, "out")));
        var candidates = scan.Facts.Where(f => f.FactType == FactTypes.DatabaseParameterCandidate).ToArray();
        Assert.Equal(expected ? 1 : 0, candidates.Length);
        if (!expected) return;
        var candidate = Assert.Single(candidates);
        Assert.Equal("syntax-parameter-collection-add", candidate.Properties["coverageLabel"]);
        Assert.Equal("SqlDbType.VarChar", candidate.Properties["parameterTypeName"]);
        Assert.Equal("unknown", candidate.Properties["parameterDirectionEvidence"]);
        Assert.Equal("unresolved", candidate.Properties["commandAssociation"]);
        Assert.DoesNotContain("PrivateSentinel", JsonSerializer.Serialize(candidates));
        Assert.Contains(scan.Facts, f => f.FactType == FactTypes.CallEdge);
    }
    [Fact]
    public void Ordinary_creations_and_alias_parameter_call_edges_are_preserved()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "Input.vb"), "Imports P = System.Data.SqlClient.SqlParameter\nPublic Class Example\nSub Run()\nDim x = New Object()\nDim p = New P(\"@Public\", Global.System.Data.SqlDbType.Int)\nEnd Sub\nEnd Class");
        WebFormsWizardNativeTests.InitGit(temp.Path);
        var scan = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, "out")));
        Assert.Single(scan.Facts, f => f.FactType == FactTypes.DatabaseParameterCandidate);
        foreach (var type in new[] { "Object", "P" })
        {
            Assert.Contains(scan.Facts, f => f.FactType == FactTypes.ObjectCreated && f.TargetSymbol == type);
            Assert.Contains(scan.Facts, f => f.FactType == FactTypes.CallEdge && f.TargetSymbol == type);
        }
    }
    [Theory]
    [InlineData("", "Global.System.Data.SqlClient.SqlParameter", true)]
    [InlineData("", "global.system.data.sqlclient.sqlparameter", true)]
    [InlineData("Imports System.Data.SqlClient", "SqlParameter", true)]
    [InlineData("Imports P = System.Data.SqlClient.SqlParameter", "P", true)]
    [InlineData("Imports P = Fake.SqlParameter", "P", false)]
    [InlineData("", "Fake.SqlParameter", false)]
    [InlineData("Imports System.Data.SqlClient\nPublic Class SqlParameter\nEnd Class", "SqlParameter", false)]
    public void Constructor_candidates_are_bounded_hashed_and_not_command_bindings(string imports, string type, bool expected)
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "Input.vb"), $"{imports}\nPublic Class Example\nPublic Sub Run()\nDim p = New {type}(\"@PrivateSentinel\", Global.System.Data.SqlDbType.Int)\nEnd Sub\nEnd Class");
        WebFormsWizardNativeTests.InitGit(temp.Path);
        var scan = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, "out")));
        var facts = scan.Facts.Where(f => f.FactType == FactTypes.DatabaseParameterCandidate).ToArray();
        Assert.Equal(expected ? 1 : 0, facts.Length);
        if (!expected) return;
        var fact = Assert.Single(facts);
        Assert.Equal(EvidenceTiers.Tier3SyntaxOrTextual, fact.EvidenceTier);
        Assert.Equal("SqlDbType.Int", fact.Properties["parameterTypeName"]);
        Assert.Equal("hash-only-literal", fact.Properties["parameterNameEvidence"]);
        Assert.Equal("unknown", fact.Properties["parameterDirectionEvidence"]);
        Assert.Equal("unresolved", fact.Properties["commandAssociation"]);
        Assert.DoesNotContain("PrivateSentinel", JsonSerializer.Serialize(facts));
        Assert.Contains("Database Parameter Candidates", MarkdownReportWriter.Build(scan));
        Assert.Contains(fact.FactId, MarkdownReportWriter.Build(scan));
    }

    [Theory]
    [InlineData("name, value")]
    [InlineData("parameterName:=\"@PrivateSentinel\", dbType:=Global.System.Data.SqlDbType.Int")]
    public void Dynamic_or_named_arguments_do_not_infer_type_or_direction(string arguments)
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "Input.vb"), $"Public Class Example\nPublic Sub Run(name As String, value As Object)\nDim p = New Global.System.Data.SqlClient.SqlParameter({arguments})\nEnd Sub\nEnd Class");
        WebFormsWizardNativeTests.InitGit(temp.Path);
        var scan = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, "out")));
        var fact = Assert.Single(scan.Facts, f => f.FactType == FactTypes.DatabaseParameterCandidate);
        Assert.Equal("unavailable", fact.Properties["parameterTypeEvidence"]);
        Assert.Equal("unknown", fact.Properties["parameterDirectionEvidence"]);
        Assert.DoesNotContain("PrivateSentinel", JsonSerializer.Serialize(fact));
    }
}
