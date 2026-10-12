using System.Text.Json;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class DatabaseCommandStateTests
{
    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Named_public_controls_preserve_hash_only_state_and_mutation_boundaries(string language)
    {
        var repo = new DirectoryInfo(AppContext.BaseDirectory);
        while (repo is not null && !File.Exists(Path.Combine(repo.FullName,"AGENTS.md"))) repo = repo.Parent;
        using var temp = new TempDirectory();
        var scan = ScanEngine.Scan(new ScanOptions(Path.Combine(repo!.FullName,"samples/sql-database-matrix/net10.0",language),Path.Combine(temp.Path,"scan")));
        CodeFact Call(string name) => Assert.Single(scan.Facts,f => f.FactType == FactTypes.DatabaseOperationCandidate && f.EvidenceTier == EvidenceTiers.Tier1Semantic && f.SourceSymbol?.Contains("."+name+"(",StringComparison.Ordinal) == true);
        Assert.Equal("hash-only-constant",Call("ControlLiteral").Properties["commandTextAvailability"]);
        Assert.Equal("hash-only-constant",Call("ControlConstant").Properties["commandTextAvailability"]);
        Assert.Equal("dynamic",Call("ControlDynamic").Properties["commandTextAvailability"]);
        Assert.Equal("StoredProcedure",Call("ControlStoredProcedure").Properties["commandTypeEvidence"]);
        Assert.Equal("Text",Call("ControlText").Properties["commandTypeEvidence"]);
        Assert.Equal("InputOutput",Call("ControlParameters").Properties["parameterDirectionKinds"]);
        Assert.Equal("enum-argument-witness",Call("ControlParameters").Properties["parameterTypeKinds"]);
        Assert.Equal("local-assignment-witness",Call("ControlConnectionAssignment").Properties["connectionEvidence"]);
        Assert.Equal("constructor-argument-witness",Call("ControlConnectionConstructor").Properties["connectionEvidence"]);
        Assert.Equal("SqlDbType.Int",Call("ControlParameters").Properties["parameterTypeNames"]);
        Assert.Equal("local-assignment-witness",Call("ControlTransaction").Properties["transactionEvidence"]);
        foreach (var boundary in new[] { "ControlBranch", "ControlUnknownCall", "ControlNestedAssignment", "ControlAliasEscape" })
            Assert.True(Call(boundary).Properties["commandTextAvailability"] == "unavailable", boundary + ": " + Call(boundary).Properties["commandTextAvailability"]);
        Assert.Equal("unavailable",Call("ControlReassignment").Properties["commandTextAvailability"]);
        var reuse=scan.Facts.Where(f => f.FactType == FactTypes.DatabaseOperationCandidate && f.EvidenceTier == EvidenceTiers.Tier1Semantic && f.SourceSymbol?.Contains(".ControlReuse(",StringComparison.Ordinal) == true).OrderBy(f => f.Evidence.StartLine).ThenBy(f => f.Properties["methodName"]).ToArray();
        Assert.Equal(2,reuse.Length); Assert.NotEqual(reuse[0].Properties["commandTextHash"],reuse[1].Properties["commandTextHash"]);
        Assert.Equal("interface",Call("ControlInterface").Properties["dispatchBoundary"]);
        Assert.Equal("unavailable",Call("ControlFactory").Properties["connectionEvidence"]);
        Assert.DoesNotContain("PublicProcedure",JsonSerializer.Serialize(scan.Facts.Where(f => f.FactType == FactTypes.DatabaseOperationCandidate)));
    }

    [Theory]
    [InlineData("Imports System.Data.SqlClient", "SqlCommand", true)]
    [InlineData("Imports C = System.Data.SqlClient.SqlCommand", "C", true)]
    [InlineData("Imports N = System.Data.SqlClient", "N.SqlCommand", true)]
    [InlineData("Imports System.Data.SqlClient", "Fake.SqlCommand", false)]
    [InlineData("", "Object", false)]
    public void Projectless_alias_and_unrelated_receivers_remain_candidates(string imports,string type,bool supported)
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path,"Input.vb"),$"{imports}\nPublic Class Example\nPublic Sub Run(command As {type})\ncommand.ExecuteNonQuery()\nEnd Sub\nEnd Class");
        WebFormsWizardNativeTests.InitGit(temp.Path);
        var scan=ScanEngine.Scan(new ScanOptions(temp.Path,Path.Combine(temp.Path,"out")));
        Assert.Equal(supported ? 1 : 0,scan.Facts.Count(f => f.FactType == FactTypes.DatabaseOperationCandidate));
    }
    [Fact]
    public void Projectless_same_named_shadow_is_not_a_provider()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path,"Input.vb"),"Imports System.Data.SqlClient\nPublic Class SqlCommand\nEnd Class\nPublic Class Example\nPublic Sub Run(command As SqlCommand)\ncommand.ExecuteNonQuery()\nEnd Sub\nEnd Class");
        WebFormsWizardNativeTests.InitGit(temp.Path);
        var scan=ScanEngine.Scan(new ScanOptions(temp.Path,Path.Combine(temp.Path,"out")));
        Assert.DoesNotContain(scan.Facts,f => f.FactType == FactTypes.DatabaseOperationCandidate);
    }
    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Failed_project_load_retains_explicit_database_candidates(string language)
    {
        using var temp = new TempDirectory();
        var source = language == "vb" ? "Public Class Example\nPublic Sub Run(command As Global.System.Data.Common.DbCommand)\ncommand.ExecuteScalar()\nEnd Sub\nEnd Class"
            : "public class Example { public void Run(global::System.Data.Common.DbCommand command) { command.ExecuteScalar(); } }";
        File.WriteAllText(Path.Combine(temp.Path,language == "vb" ? "Input.vb" : "Input.cs"),source);
        File.WriteAllText(Path.Combine(temp.Path,language == "vb" ? "Broken.vbproj" : "Broken.csproj"),"<Project><malformed>");
        WebFormsWizardNativeTests.InitGit(temp.Path);
        var scan=ScanEngine.Scan(new ScanOptions(temp.Path,Path.Combine(temp.Path,"out")));
        Assert.NotEqual("Succeeded",scan.Manifest.BuildStatus);
        Assert.Contains(scan.Facts,f => f.FactType == FactTypes.AnalysisGap);
        var candidate=Assert.Single(scan.Facts,f => f.FactType == FactTypes.DatabaseOperationCandidate);
        Assert.Equal(EvidenceTiers.Tier3SyntaxOrTextual,candidate.EvidenceTier);
        Assert.NotEqual("unknown",candidate.CommitSha);
    }

    [Fact]
    public void Database_projection_excludes_arbitrary_readable_contract_fields()
    {
        var properties = new Dictionary<string,string>
        {
            ["methodName"] = "ExecuteScalar", ["operationKind"] = "scalar-candidate",
            ["originalSignature"] = "System.Data.SqlClient.SqlCommand.ExecuteScalar(SELECT private_data)",
            ["providerAssemblyIdentity"] = "password=private", ["commandTextAvailability"] = "SELECT private_data",
            ["parameterEvidence"] = "secret-value", ["transactionEvidence"] = "secret-value", ["commandTextHash"] = "private_sql"
        };
        var evidence = DatabaseCallEvidence.FromProperties(properties);
        Assert.Null(evidence.OriginalSignature); Assert.Null(evidence.ProviderAssemblyIdentity); Assert.Null(evidence.CommandTextHash);
        Assert.DoesNotContain("private",JsonSerializer.Serialize(evidence)); Assert.DoesNotContain("secret",JsonSerializer.Serialize(evidence));
    }

}
