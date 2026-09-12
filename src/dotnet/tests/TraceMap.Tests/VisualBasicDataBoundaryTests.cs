using System.Text.Json;
using Microsoft.Data.Sqlite;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class VisualBasicDataBoundaryTests
{
    [Fact]
    public void Compiler_resolved_vb_ado_net_shapes_emit_shared_safe_evidence()
    {
        using var temp = new TempDirectory();
        var repo = CreateAdoRepository(temp.Path, includeLateBoundCall: false);

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));

        var commands = result.Facts.Where(fact =>
            fact.FactType == FactTypes.SqlCommandDetected
            && fact.RuleId == RuleIds.DatabaseSqlText
            && fact.ContractElement == "SqliteCommand").ToArray();
        Assert.Equal(2, commands.Length);
        var commandType = Assert.Single(result.Facts, fact =>
            fact.FactType == FactTypes.SqlCommandDetected
            && fact.ContractElement == "CommandType"
            && fact.Properties["storedProcedureCandidate"] == "true");
        var command = Assert.Single(commands, fact =>
            fact.Properties["commandReceiverSymbolId"] == commandType.Properties["commandReceiverSymbolId"]);
        Assert.Equal(EvidenceTiers.Tier1Semantic, command.EvidenceTier);
        Assert.Equal("compile-time-constant-hashed", command.Properties["commandTextClassification"]);
        Assert.Equal("ado-net", command.Properties["frameworkFamily"]);
        Assert.NotEmpty(command.Properties["commandTextHash"]);

        Assert.Equal("StoredProcedure", commandType.Properties["commandTypeClassification"]);
        Assert.Equal("true", commandType.Properties["storedProcedureCandidate"]);
        Assert.Contains("commandReceiverSymbolId", commandType.Properties.Keys);
        Assert.Equal(command.Properties["commandReceiverSymbolId"], commandType.Properties["commandReceiverSymbolId"]);
        var textCommandType = Assert.Single(result.Facts, fact =>
            fact.FactType == FactTypes.SqlCommandDetected
            && fact.ContractElement == "CommandType"
            && fact.Properties["storedProcedureCandidate"] == "false");
        Assert.NotEqual(commandType.Properties["commandReceiverSymbolId"], textCommandType.Properties["commandReceiverSymbolId"]);

        var parameter = Assert.Single(result.Facts, fact =>
            fact.FactType == FactTypes.SqlCommandDetected
            && fact.ContractElement == "Parameters");
        Assert.Equal("AddWithValue", parameter.Properties["parameterMutationMethod"]);
        Assert.Contains("commandReceiverSymbolId", parameter.Properties.Keys);
        Assert.Equal(command.Properties["commandReceiverSymbolId"], parameter.Properties["commandReceiverSymbolId"]);

        var operations = result.Facts
            .Where(fact => fact.FactType == FactTypes.DatabaseOperationCandidate
                && fact.RuleId == RuleIds.DatabaseOperationCallPattern)
            .ToArray();
        Assert.Contains(operations, fact => fact.ContractElement == "select-candidate" && fact.Properties["resultKind"] == "data-reader");
        Assert.Contains(operations, fact => fact.ContractElement == "scalar-candidate" && fact.Properties["resultKind"] == "scalar");
        Assert.Contains(operations, fact => fact.ContractElement == "execute-candidate");
        Assert.Contains(operations, fact => fact.ContractElement == "data-adapter-fill" && fact.Properties["resultKind"] == "data-set");
        Assert.Contains(operations, fact => fact.ContractElement == "data-adapter-fill" && fact.Properties["resultKind"] == "data-table");
        Assert.All(operations.Where(fact => fact.ContractElement != "data-adapter-fill"), fact =>
            Assert.Equal(command.Properties["commandReceiverSymbolId"], fact.Properties["receiverSymbolId"]));

        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.ObjectCreated && fact.ContractElement == "DataSet");
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.ObjectCreated && fact.ContractElement == "DataTable");
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.CallEdge && fact.ContractElement == "Fill");

        var allText = JsonSerializer.Serialize(result.Facts);
        Assert.DoesNotContain("select private_secret from private_table", allText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private_parameter", allText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private_value", allText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Late_bound_fill_or_execute_retains_bounded_gap_without_claiming_database_operation()
    {
        using var temp = new TempDirectory();
        var repo = CreateAdoRepository(temp.Path, includeLateBoundCall: true);

        var result = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out")));

        Assert.Contains(result.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && fact.RuleId == RuleIds.DatabaseOperationCallPattern
            && fact.Properties.GetValueOrDefault("gapKind") == "VisualBasicAdoNetTargetUnavailable");
        Assert.DoesNotContain(result.Facts, fact =>
            fact.FactType == FactTypes.DatabaseOperationCandidate
            && fact.SourceSymbol?.Contains("LateBound", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void Repeated_vb_ado_net_scans_are_fact_deterministic()
    {
        using var temp = new TempDirectory();
        var repo = CreateAdoRepository(temp.Path, includeLateBoundCall: false);

        var first = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out-a")));
        var second = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "out-b")));

        Assert.Equal(
            first.Facts.Select(fact => JsonSerializer.Serialize(fact)),
            second.Facts.Select(fact => JsonSerializer.Serialize(fact)));
    }

    private static string CreateAdoRepository(string root, bool includeLateBoundCall)
    {
        var repo = Path.Combine(root, "repo");
        Directory.CreateDirectory(repo);
        var sqliteAssembly = typeof(SqliteCommand).Assembly.Location;
        File.WriteAllText(Path.Combine(repo, "Data.vbproj"), $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <OptionStrict>{(includeLateBoundCall ? "Off" : "On")}</OptionStrict>
              </PropertyGroup>
              <ItemGroup>
                <Reference Include="Microsoft.Data.Sqlite">
                  <HintPath>{System.Security.SecurityElement.Escape(sqliteAssembly)}</HintPath>
                </Reference>
              </ItemGroup>
            </Project>
            """);
        var lateBound = includeLateBoundCall
            ? """
                Public Sub LateBound(value As Object, data As DataSet)
                    value.fill(data)
                    value.ExecuteReader()
                End Sub
            """
            : string.Empty;
        File.WriteAllText(Path.Combine(repo, "Data.vb"), $$"""
            Imports System.Data
            Imports System.Data.Common
            Imports Microsoft.Data.Sqlite

            Public NotInheritable Class TestAdapter
                Inherits DbDataAdapter
            End Class

            Public Module DataAccess
                Public Sub Run(connection As SqliteConnection)
                    Dim command = New SqliteCommand("select private_secret from private_table", connection)
                    command.CommandType = CommandType.StoredProcedure
                    command.Parameters.AddWithValue("private_parameter", "private_value")
                    Dim other = New SqliteCommand("select other_private_secret", connection)
                    other.CommandType = CommandType.Text
                    Using reader = command.ExecuteReader()
                    End Using
                    Dim scalar = command.ExecuteScalar()
                    Dim count = command.ExecuteNonQuery()

                    Dim adapter = New TestAdapter()
                    adapter.SelectCommand = command
                    Dim data = New DataSet()
                    adapter.Fill(data)
                    Dim table = New DataTable()
                    adapter.Fill(table)
                End Sub

            {{lateBound}}
            End Module
            """);
        Commit(repo);
        return repo;
    }

    private static void Commit(string repo)
    {
        RunGit(repo, "init");
        RunGit(repo, "add", "-A");
        RunGit(repo, "-c", "user.name=TraceMap", "-c", "user.email=tests@example.invalid", "commit", "-m", "fixture");
    }

    private static void RunGit(string repo, params string[] arguments)
    {
        using var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "git",
                WorkingDirectory = repo,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            }
        };
        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }
        process.Start();
        process.WaitForExit(30_000);
        Assert.Equal(0, process.ExitCode);
    }
}
