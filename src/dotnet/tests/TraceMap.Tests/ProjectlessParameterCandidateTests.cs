using System.Text.Json;
using TraceMap.Core;
using TraceMap.Reporting;

namespace TraceMap.Tests;

public sealed class ProjectlessParameterCandidateTests
{
    [Theory]
    [InlineData("Imports System.Data", "SqlClient.SqlCommand", true)]
    [InlineData("Imports C = System.Data.SqlClient.SqlCommand", "C", true)]
    [InlineData("Imports N = Microsoft.Data.SqlClient", "N.SqlCommand", true)]
    [InlineData("", "Global.System.Data.SqlClient.SqlCommand", true)]
    [InlineData("Imports System.Data", "Fake.SqlCommand", false)]
    [InlineData("Imports System.Data.SqlClient\nImports Microsoft.Data.SqlClient", "SqlCommand", false)]
    [InlineData("Imports System.Data\nPublic Class SqlClient\nEnd Class", "SqlClient.SqlCommand", false)]
    public void Nested_relative_command_uses_same_resolver_for_add_and_declaration(string imports, string type, bool expected)
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "Input.vb"), $"{imports}\nPublic Class Example\nSub Run(connection As Object, context As Object)\nTry\nDim command As {type} = New {type}(\"PublicProcedure\", connection)\ncommand.Parameters.Add(\"@PublicId\", Global.System.Data.SqlDbType.Int)\ncommand.Parameters(\"@PublicId\").Value = CInt(context.Current.Value.ToString().Trim())\ncommand.ExecuteNonQuery()\nCatch\nEnd Try\nEnd Sub\nEnd Class");
        WebFormsWizardNativeTests.InitGit(temp.Path);
        var scan = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, "out")));
        var candidates = scan.Facts.Where(f => f.FactType == FactTypes.DatabaseParameterCandidate).ToArray();
        Assert.Equal(expected ? 1 : 0, candidates.Length);
        if (!expected) return;
        var candidate = Assert.Single(candidates);
        Assert.Equal("syntax-local-declaration-candidate", candidate.Properties["commandReceiverAssociation"]);
        Assert.Equal("unresolved", candidate.Properties["commandExecutionEvidence"]);
        Assert.Equal(EvidenceTiers.Tier3SyntaxOrTextual, candidate.EvidenceTier);
        Assert.Contains("syntax-local-declaration-candidate", MarkdownReportWriter.Build(scan));
    }

    [Theory]
    [InlineData("context.Current.Value")]
    [InlineData("CInt(context.Current.Value)")]
    [InlineData("context.Current.Value.ToString().Trim()")]
    [InlineData("Mutate(command)")]
    public void Complex_values_do_not_erase_receiver_or_prove_execution(string value)
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "Input.vb"), $"Imports System.Data\nImports System.Data.SqlClient\nPublic Class Example\nSub Run(context As Object, connection As SqlConnection)\nTry\nDim command As New SqlCommand(\"PublicProcedure\", connection)\ncommand.Parameters.Add(\"@PublicA\", SqlDbType.Int)\ncommand.Parameters.Add(\"@PublicB\", SqlDbType.Int)\ncommand.Parameters(\"@PublicA\").Value = {value}\ncommand.ExecuteNonQuery()\nCatch\nEnd Try\nEnd Sub\nEnd Class");
        WebFormsWizardNativeTests.InitGit(temp.Path);
        var scan = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, "out")));
        var candidates = scan.Facts.Where(f => f.FactType == FactTypes.DatabaseParameterCandidate).ToArray();
        Assert.Equal(2, candidates.Length);
        foreach (var candidate in candidates)
        {
            Assert.Equal("syntax-local-declaration-candidate", candidate.Properties["commandReceiverAssociation"]);
            Assert.Equal("unresolved", candidate.Properties["commandExecutionEvidence"]);
            Assert.Equal("unresolved", candidate.Properties["commandAssociation"]);
            Assert.False(candidate.Properties.ContainsKey("commandExecutionStartLine"));
            Assert.Equal(EvidenceTiers.Tier3SyntaxOrTextual, candidate.EvidenceTier);
        }
        Assert.Contains("syntax-local-declaration-candidate", MarkdownReportWriter.Build(scan));
    }

    [Theory]
    [InlineData("If True Then", "End If", "unsupported-containing-block")]
    [InlineData("Try", "Catch\nEnd Try", "fresh-local-not-established")]
    public void Association_does_not_cross_block_boundary_and_reports_reason(string open, string close, string reason)
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "Input.vb"), $"Imports System.Data\nImports System.Data.SqlClient\nPublic Class Example\nSub Run()\nDim command As New SqlCommand()\n{open}\ncommand.Parameters.Add(\"@PublicId\", SqlDbType.Int)\ncommand.ExecuteNonQuery()\n{close}\nEnd Sub\nEnd Class");
        WebFormsWizardNativeTests.InitGit(temp.Path);
        var scan = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, "out")));
        var candidate = Assert.Single(scan.Facts, f => f.FactType == FactTypes.DatabaseParameterCandidate);
        Assert.Equal("unresolved", candidate.Properties["commandAssociation"]);
        Assert.Equal(reason, candidate.Properties["commandAssociationReason"]);
        Assert.Contains(reason, MarkdownReportWriter.Build(scan));
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("command = New SqlCommand()", false)]
    [InlineData("Dim escaped = command", false)]
    [InlineData("UnknownCall(command)", false)]
    [InlineData("If True Then\ncommand = New SqlCommand()\nEnd If", false)]
    [InlineData("command.Parameters(\"@PublicId\").Value = UnknownCall(command)", false)]
    public void Nested_configuration_retains_only_same_block_command_candidate(string intervening, bool expected)
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "Input.vb"), $"Imports System.Data\nImports System.Data.SqlClient\nPublic Class Example\nSub Run(connection As SqlConnection, transaction As SqlTransaction, value As Integer)\nTry\nDim command As New SqlCommand(\"PublicProcedure\", connection)\ncommand.Parameters.Add(\"@PublicId\", SqlDbType.Int)\ncommand.Parameters(\"@PublicId\").Value = value\ncommand.Transaction = transaction\n{intervening}\ncommand.ExecuteNonQuery()\nCatch\nEnd Try\nEnd Sub\nEnd Class");
        WebFormsWizardNativeTests.InitGit(temp.Path);
        var scan = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, "out")));
        var candidate = Assert.Single(scan.Facts, f => f.FactType == FactTypes.DatabaseParameterCandidate);
        Assert.Equal(expected ? "syntax-local-straight-line-candidate" : "unresolved", candidate.Properties["commandAssociation"]);
        Assert.False(string.IsNullOrWhiteSpace(candidate.Properties["commandAssociationReason"]));
        Assert.Equal(EvidenceTiers.Tier3SyntaxOrTextual, candidate.EvidenceTier);
        Assert.DoesNotContain("PublicProcedure", JsonSerializer.Serialize(candidate));
    }

    [Theory]
    [InlineData("", "", true)]
    [InlineData("Dim aliasCommand = command", "", false)]
    [InlineData("command = New SqlCommand()", "", false)]
    [InlineData("", "command = New SqlCommand()", false)]
    [InlineData("", "UnknownCall(command)", false)]
    [InlineData("", "If True Then\ncommand = New SqlCommand()\nEnd If", false)]
    public void Local_command_association_is_bounded_and_stops_at_uncertainty(string before, string after, bool associated)
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "Input.vb"), $"Imports System.Data\nImports System.Data.SqlClient\nPublic Class Example\nSub Run()\nDim command As New SqlCommand()\n{before}\ncommand.Parameters.Add(\"@PrivateSentinel\", SqlDbType.Int)\ncommand.Parameters.Add(\"@Second\", SqlDbType.VarChar)\n{after}\ncommand.ExecuteNonQuery()\nEnd Sub\nEnd Class");
        WebFormsWizardNativeTests.InitGit(temp.Path);
        var scan = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, "out")));
        var candidates = scan.Facts.Where(f => f.FactType == FactTypes.DatabaseParameterCandidate).ToArray();
        Assert.Equal(2, candidates.Length);
        foreach (var candidate in candidates)
        {
            Assert.Equal(associated ? "syntax-local-straight-line-candidate" : "unresolved", candidate.Properties["commandAssociation"]);
            Assert.Equal(string.IsNullOrEmpty(before) ? "syntax-local-declaration-candidate" : "unresolved", candidate.Properties["commandReceiverAssociation"]);
            Assert.Equal(EvidenceTiers.Tier3SyntaxOrTextual, candidate.EvidenceTier);
            Assert.Equal("unknown", candidate.Properties["parameterDirectionEvidence"]);
            if (associated)
            {
                Assert.Equal("ExecuteNonQuery", candidate.Properties["commandExecutionMethod"]);
                Assert.True(int.Parse(candidate.Properties["commandExecutionStartLine"]) > candidate.Evidence.StartLine);
            }
        }
        Assert.DoesNotContain("PrivateSentinel", JsonSerializer.Serialize(candidates));
        Assert.Contains(associated ? "syntax-local-straight-line-candidate" : "unresolved", MarkdownReportWriter.Build(scan));
    }
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
