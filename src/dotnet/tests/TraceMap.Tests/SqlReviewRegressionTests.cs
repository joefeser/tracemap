using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class SqlReviewRegressionTests
{
    [Theory]
    [InlineData("")]
    [InlineData("invented")]
    [InlineData("Input;Input")]
    [InlineData("Output;Input")]
    [InlineData("Input;")]
    [InlineData("unavailable;Input")]
    public void Restored_parameter_lists_must_be_canonical(string value)
    {
        var evidence = DatabaseCallEvidence.FromProperties(new Dictionary<string, string>());
        Assert.True(evidence.IsWellFormed());
        Assert.False((evidence with { ParameterDirectionKinds = value }).IsWellFormed());
        Assert.False((evidence with { ParameterTypeKinds = value }).IsWellFormed());
        Assert.True((evidence with { ParameterDirectionKinds = null, ParameterTypeKinds = null }).IsWellFormed());
        Assert.True((evidence with { ParameterDirectionKinds = "Input;Output",
            ParameterTypeKinds = "enum-argument-witness;enum-assignment-witness" }).IsWellFormed());
    }

    [Theory]
    [InlineData("parameterDirectionKinds", "Input")]
    [InlineData("parameterTypeKinds", "enum-argument-witness")]
    [InlineData("parameterTypeNames", "DbType.String")]
    public void Parameter_metadata_is_bounded_before_tokenization(string key, string token)
    {
        foreach (var raw in new[] { new string('x', 8193), string.Join(';', Enumerable.Repeat(token, 129)), token + ";" })
        {
            var evidence = DatabaseCallEvidence.FromProperties(new Dictionary<string, string> { [key] = raw });
            Assert.Equal("unavailable", evidence.ParameterDirectionKinds);
            Assert.Equal("unavailable", evidence.ParameterTypeKinds);
            Assert.Null(evidence.ParameterTypeNames);
            Assert.True(evidence.IsWellFormed());
        }
        var accepted = DatabaseCallEvidence.FromProperties(new Dictionary<string, string>
            { [key] = string.Join(';', Enumerable.Repeat(token, 128)) });
        Assert.Contains(token, new[] { accepted.ParameterDirectionKinds, accepted.ParameterTypeKinds, accepted.ParameterTypeNames });
    }

    [Fact]
    public void Multiple_parameter_witnesses_are_unioned_not_overwritten()
    {
        var tree = CSharpSyntaxTree.ParseText("""
            using Microsoft.Data.Sqlite;
            using System.Data;
            class Example {
                void Run(SqliteCommand command) {
                    var first = command.Parameters.Add("@first", SqliteType.Integer);
                    first.Direction = ParameterDirection.Input;
                    first.DbType = DbType.Int32;
                    var second = command.Parameters.Add("@second", SqliteType.Text);
                    second.Direction = ParameterDirection.Output;
                    second.DbType = DbType.String;
                    command.ExecuteNonQuery();
                }
            }
            """);
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Append(typeof(Microsoft.Data.Sqlite.SqliteCommand).Assembly.Location).Distinct()
            .Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create("PublicWitnesses", [tree], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        Assert.DoesNotContain(compilation.GetDiagnostics(), d => d.Severity == DiagnosticSeverity.Error);
        var syntax = tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().Last();
        var call = Assert.IsAssignableFrom<IInvocationOperation>(compilation.GetSemanticModel(tree).GetOperation(syntax));
        var properties = new SortedDictionary<string, string>(StringComparer.Ordinal);
        DatabaseCommandState.Add(properties, call);
        Assert.Equal("Input;Output", properties["parameterDirectionKinds"]);
        Assert.Equal("DbType.Int32;DbType.String", properties["parameterTypeNames"]);
    }

    [Theory]
    [InlineData("xml-reader-candidate")]
    [InlineData("data-adapter-schema")]
    [InlineData("data-adapter-update")]
    [InlineData("bulk-write-boundary")]
    public void Legacy_v1_bucket_remains_discoverable(string kind)
    {
        var fact = new CombinedSurfaceFactInput("combined", "index", "public", "fact", "scan", "commit",
            FactTypes.DatabaseOperationCandidate, "database.operation.call-pattern.v1", EvidenceTiers.Tier3SyntaxOrTextual,
            "Example.vb", 1, 1, new Dictionary<string, string> { ["operationKind"] = kind });
        Assert.Equal("sql-persistence", Assert.Single(CombinedSurfaceProjection.BuildSurfaces([fact])).SurfaceKind);
    }

    [Fact]
    public void Optional_evidence_does_not_change_positional_constructor_abi()
    {
        foreach (var type in new[] { typeof(CombinedSurfaceProjectionRow), typeof(TraceMap.Reporting.CombinedDependencySurfaceRow), typeof(TraceMap.Reporting.CombinedPathNode) })
        {
            var constructor = Assert.Single(type.GetConstructors());
            Assert.Equal("HttpCallEvidence", constructor.GetParameters()[^1].Name);
            Assert.DoesNotContain(constructor.GetParameters(), p => p.Name == "DatabaseCallEvidence" || p.Name == "CompiledDatabaseApiEvidence");
        }
    }

    [Theory]
    [InlineData("insert-candidate")]
    [InlineData("update-candidate")]
    [InlineData("delete-candidate")]
    [InlineData("upsert-candidate")]
    [InlineData("stored-routine-call-candidate")]
    public void Refined_operation_kinds_survive_projection(string kind)
    {
        var evidence = DatabaseCallEvidence.FromProperties(new Dictionary<string, string>
            { ["methodName"] = "ExecuteNonQuery", ["operationKind"] = kind });
        Assert.Equal(kind, evidence.OperationKind);
        Assert.True(evidence.IsWellFormed());
        Assert.False((evidence with { MethodName = null! }).IsWellFormed());
        Assert.False((evidence with { OperationKind = "invented" }).IsWellFormed());
        Assert.False((evidence with { CommandTextHash = "not-a-hash" }).IsWellFormed());
        Assert.False((evidence with { OriginalSignature = "System.Data.SqlClient.SqlCommand.ExecuteNonQuery(" }).IsWellFormed());
    }

    [Fact]
    public void Sqlite_identity_and_distinct_parameter_witnesses_survive_projection()
    {
        const string identity = "Microsoft.Data.Sqlite, Version=10.0.0.0, Culture=neutral, PublicKeyToken=adb9793829ddae60";
        var evidence = DatabaseCallEvidence.FromProperties(new Dictionary<string, string>
        {
            ["methodName"] = "ExecuteScalar", ["providerAssemblyIdentity"] = identity,
            ["parameterDirectionKinds"] = "Output;Input;Output",
            ["parameterTypeKinds"] = "enum-assignment-witness;enum-argument-witness",
            ["parameterTypeNames"] = "SqlDbType.Int;DbType.String;SqlDbType.Int"
        });
        Assert.Equal(identity, evidence.ProviderAssemblyIdentity);
        Assert.Equal("Input;Output", evidence.ParameterDirectionKinds);
        Assert.Equal("enum-argument-witness;enum-assignment-witness", evidence.ParameterTypeKinds);
        Assert.Equal("DbType.String;SqlDbType.Int", evidence.ParameterTypeNames);
        Assert.True(evidence.IsWellFormed());
    }

    [Theory]
    [InlineData("namespace N { using System.Data; class C { void M(SqlClient.SqlCommand cmd) { cmd.ExecuteScalar(); } } }", "System.Data.SqlClient.SqlCommand")]
    [InlineData("namespace N { using System.Data.SqlClient; class C { void M(SqlCommand cmd) { cmd.ExecuteScalar(); } } }", "System.Data.SqlClient.SqlCommand")]
    [InlineData("namespace N; using System.Data.SqlClient; class C { void M(SqlCommand cmd) { cmd.ExecuteScalar(); } }", "System.Data.SqlClient.SqlCommand")]
    [InlineData("using System.Data.SqlClient; class C { SqlCommand cmd; void M() { System.Action<object> f = cmd => cmd.ExecuteScalar(); } }", null)]
    [InlineData("using System.Data.SqlClient; class C { SqlCommand cmd; void M() { System.Action<object> f = (object cmd) => cmd.ExecuteScalar(); } }", null)]
    [InlineData("using System.Data.SqlClient; class C { SqlCommand cmd; void M() { void F(object cmd) { cmd.ExecuteScalar(); } } }", null)]
    [InlineData("using System.Data.SqlClient; class C { SqlCommand cmd; void M(object cmd) { this.cmd.ExecuteScalar(); } }", "System.Data.SqlClient.SqlCommand")]
    public void Syntax_receiver_resolution_obeys_imports_and_lexical_shadowing(string source, string? expected)
    {
        var root = CSharpSyntaxTree.ParseText(source).GetCompilationUnitRoot();
        var call = Assert.Single(root.DescendantNodes().OfType<InvocationExpressionSyntax>());
        Assert.Equal(expected, CSharpDatabaseSyntax.ReceiverType(root, call));
    }

    [Theory]
    [InlineData("ExecuteReader", "data-reader")]
    [InlineData("ExecuteScalar", "scalar")]
    [InlineData("ExecuteNonQuery", "row-count-or-none")]
    [InlineData("ExecuteXmlReader", "xml-reader")]
    public void Projectless_vb_keeps_result_shape(string method, string result)
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "Input.vb"), $"Public Class Example\nPublic Sub Run(command As Global.System.Data.SqlClient.SqlCommand)\ncommand.{method}()\nEnd Sub\nEnd Class");
        WebFormsWizardNativeTests.InitGit(temp.Path);
        var scan = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, "out")));
        var fact = Assert.Single(scan.Facts, f => f.FactType == FactTypes.DatabaseOperationCandidate);
        Assert.Equal(result, fact.Properties["resultKind"]);
        Assert.Equal(EvidenceTiers.Tier3SyntaxOrTextual, fact.EvidenceTier);
    }
}
