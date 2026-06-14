using System.Text.Json;
using System.Reflection;
using Microsoft.Data.Sqlite;
using TraceMap.Cli;
using TraceMap.Storage;

namespace TraceMap.Tests;

public sealed class SqliteIndexWriterTests
{
    [Fact]
    public async Task Scan_writes_manifest_and_all_jsonl_facts_to_sqlite()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        var outputPath = Path.Combine(temp.Path, "out");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Sample.cs"), """
            public sealed class CustomerProfile
            {
                public string PrimaryEmail { get; set; } = "";

                public void Load(HttpClient client)
                {
                    var profile = new CustomerProfile();
                    _ = profile.PrimaryEmail;
                    client.GetAsync("/profiles");
                }
            }
            """);

        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await TraceMapCommand.RunAsync(["scan", "--repo", repo, "--out", outputPath], output, error);
        Assert.Equal(0, exitCode);

        var sqlitePath = Path.Combine(outputPath, "index.sqlite");
        var jsonlPath = Path.Combine(outputPath, "facts.ndjson");
        Assert.True(File.Exists(sqlitePath));

        var jsonlCount = File.ReadLines(jsonlPath).Count();
        await using var connection = new SqliteConnection($"Data Source={sqlitePath}");
        await connection.OpenAsync();

        Assert.Equal(1L, await ExecuteScalarAsync<long>(connection, "select count(*) from scan_manifest;"));
        Assert.Equal(jsonlCount, await ExecuteScalarAsync<long>(connection, "select count(*) from facts;"));
        Assert.Equal("Level3SyntaxAnalysis", await ExecuteScalarAsync<string>(connection, "select analysis_level from scan_manifest;"));
        Assert.Equal("NotRun", await ExecuteScalarAsync<string>(connection, "select build_status from scan_manifest;"));

        var memberAccessCount = await ExecuteScalarAsync<long>(
            connection,
            "select count(*) from facts where fact_type = 'MemberAccessName' and target_symbol = 'PrimaryEmail' and evidence_tier = 'Tier3SyntaxOrTextual';");
        Assert.Equal(1L, memberAccessCount);

        var invocationCount = await ExecuteScalarAsync<long>(
            connection,
            "select count(*) from facts where fact_type = 'InvocationName' and target_symbol = 'GetAsync' and file_path = 'Sample.cs';");
        Assert.Equal(1L, invocationCount);

        var callEdgeCount = await ExecuteScalarAsync<long>(
            connection,
            "select count(*) from call_edges where caller_symbol = 'Load' and callee_symbol = 'GetAsync' and file_path = 'Sample.cs';");
        Assert.Equal(1L, callEdgeCount);

        var createdObjectCount = await ExecuteScalarAsync<long>(
            connection,
            "select count(*) from object_creations where caller_symbol = 'Load' and created_type = 'CustomerProfile' and assigned_to = 'profile' and file_path = 'Sample.cs';");
        Assert.Equal(1L, createdObjectCount);

        var propertiesJson = await ExecuteScalarAsync<string>(
            connection,
            "select properties_json from facts where fact_type = 'PropertyDeclared' and target_symbol = 'PrimaryEmail';");
        using var properties = JsonDocument.Parse(propertiesJson);
        Assert.Equal("PrimaryEmail", properties.RootElement.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Scan_writes_semantic_symbol_tables_to_sqlite()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        var projectPath = Path.Combine(repo, "src", "SymbolSample");
        var outputPath = Path.Combine(temp.Path, "out");
        Directory.CreateDirectory(projectPath);
        File.WriteAllText(Path.Combine(projectPath, "SymbolSample.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(projectPath, "SymbolDemo.cs"), """
            namespace SymbolSample;

            public sealed class Dto
            {
                public string Name { get; set; } = "";
            }

            public interface ISymbolDemo
            {
                void Use(Dto dto);
            }

            public abstract class SymbolBase
            {
                public virtual void Track(Dto dto)
                {
                }
            }

            public sealed class SymbolDemo : SymbolBase, ISymbolDemo
            {
                public void Use(Dto dto)
                {
                    Overloaded(dto);
                    Overloaded(dto.Name);
                }

                public override void Track(Dto dto)
                {
                }

                private void Overloaded(Dto dto)
                {
                }

                private void Overloaded(string name)
                {
                }
            }
            """);

        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await TraceMapCommand.RunAsync(["scan", "--repo", repo, "--out", outputPath], output, error);
        Assert.Equal(0, exitCode);

        await using var connection = new SqliteConnection($"Data Source={Path.Combine(outputPath, "index.sqlite")}");
        await connection.OpenAsync();

        Assert.Equal("Level1SemanticAnalysis", await ExecuteScalarAsync<string>(connection, "select analysis_level from scan_manifest;"));
        Assert.True(await ExecuteScalarAsync<long>(connection, "select count(*) from symbols where language = 'csharp';") > 0);
        Assert.True(await ExecuteScalarAsync<long>(connection, "select count(*) from symbol_occurrences where occurrence_kind = 'Definition';") > 0);
        Assert.True(await ExecuteScalarAsync<long>(connection, "select count(*) from fact_symbols where role = 'target';") > 0);

        var overloadedMethodCount = await ExecuteScalarAsync<long>(
            connection,
            "select count(*) from symbols where symbol_kind = 'Method' and display_name like '%Overloaded%';");
        Assert.Equal(2L, overloadedMethodCount);

        var targetCallSymbolCount = await ExecuteScalarAsync<long>(
            connection,
            """
            select count(*)
            from fact_symbols fs
            join facts f on f.fact_id = fs.fact_id
            where f.fact_type = 'CallEdge' and fs.role = 'target';
            """);
        Assert.True(targetCallSymbolCount >= 2L);

        Assert.Equal(1L, await ExecuteScalarAsync<long>(connection, "select count(*) from symbol_relationships where relationship_kind = 'InheritsFrom';"));
        Assert.Equal(1L, await ExecuteScalarAsync<long>(connection, "select count(*) from symbol_relationships where relationship_kind = 'ImplementsInterface';"));
        Assert.Equal(1L, await ExecuteScalarAsync<long>(connection, "select count(*) from symbol_relationships where relationship_kind = 'Overrides';"));
        Assert.Equal(1L, await ExecuteScalarAsync<long>(connection, "select count(*) from symbol_relationships where relationship_kind = 'ImplementsInterfaceMember';"));
        Assert.Equal(4L, await ExecuteScalarAsync<long>(connection, "select count(*) from facts where fact_type = 'SymbolRelationship';"));
    }

    [Fact]
    public async Task Scan_writes_required_sqlite_indexes()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "repo");
        var outputPath = Path.Combine(temp.Path, "out");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Only.cs"), "public sealed class Only { }");

        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await TraceMapCommand.RunAsync(["scan", "--repo", repo, "--out", outputPath], output, error);
        Assert.Equal(0, exitCode);

        await using var connection = new SqliteConnection($"Data Source={Path.Combine(outputPath, "index.sqlite")}");
        await connection.OpenAsync();

        var indexNames = new HashSet<string>(StringComparer.Ordinal);
        await using var command = connection.CreateCommand();
        command.CommandText = "select name from sqlite_master where type = 'index' and tbl_name = 'facts';";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            indexNames.Add(reader.GetString(0));
        }

        Assert.Contains("ix_facts_type", indexNames);
        Assert.Contains("ix_facts_rule", indexNames);
        Assert.Contains("ix_facts_target_symbol", indexNames);
        Assert.Contains("ix_facts_contract_element", indexNames);
        Assert.Contains("ix_facts_file", indexNames);

        var symbolIndexNames = new HashSet<string>(StringComparer.Ordinal);
        await using var symbolCommand = connection.CreateCommand();
        symbolCommand.CommandText = "select name from sqlite_master where type = 'index' and tbl_name = 'symbols';";
        await using var symbolReader = await symbolCommand.ExecuteReaderAsync();
        while (await symbolReader.ReadAsync())
        {
            symbolIndexNames.Add(symbolReader.GetString(0));
        }

        Assert.Contains("ix_symbols_display", symbolIndexNames);
        Assert.Contains("ix_symbols_kind", symbolIndexNames);
        Assert.Contains("ix_symbols_assembly", symbolIndexNames);

        var symbolOccurrenceIndexNames = new HashSet<string>(StringComparer.Ordinal);
        await using var symbolOccurrenceCommand = connection.CreateCommand();
        symbolOccurrenceCommand.CommandText = "select name from sqlite_master where type = 'index' and tbl_name = 'symbol_occurrences';";
        await using var symbolOccurrenceReader = await symbolOccurrenceCommand.ExecuteReaderAsync();
        while (await symbolOccurrenceReader.ReadAsync())
        {
            symbolOccurrenceIndexNames.Add(symbolOccurrenceReader.GetString(0));
        }

        Assert.Contains("ix_symbol_occurrences_symbol", symbolOccurrenceIndexNames);
        Assert.Contains("ix_symbol_occurrences_file", symbolOccurrenceIndexNames);

        var factSymbolIndexNames = new HashSet<string>(StringComparer.Ordinal);
        await using var factSymbolCommand = connection.CreateCommand();
        factSymbolCommand.CommandText = "select name from sqlite_master where type = 'index' and tbl_name = 'fact_symbols';";
        await using var factSymbolReader = await factSymbolCommand.ExecuteReaderAsync();
        while (await factSymbolReader.ReadAsync())
        {
            factSymbolIndexNames.Add(factSymbolReader.GetString(0));
        }

        Assert.Contains("ix_fact_symbols_symbol", factSymbolIndexNames);

        var symbolRelationshipIndexNames = new HashSet<string>(StringComparer.Ordinal);
        await using var symbolRelationshipCommand = connection.CreateCommand();
        symbolRelationshipCommand.CommandText = "select name from sqlite_master where type = 'index' and tbl_name = 'symbol_relationships';";
        await using var symbolRelationshipReader = await symbolRelationshipCommand.ExecuteReaderAsync();
        while (await symbolRelationshipReader.ReadAsync())
        {
            symbolRelationshipIndexNames.Add(symbolRelationshipReader.GetString(0));
        }

        Assert.Contains("ix_symbol_relationships_source", symbolRelationshipIndexNames);
        Assert.Contains("ix_symbol_relationships_target", symbolRelationshipIndexNames);
        Assert.Contains("ix_symbol_relationships_kind", symbolRelationshipIndexNames);

        var callEdgeIndexNames = new HashSet<string>(StringComparer.Ordinal);
        await using var callEdgeCommand = connection.CreateCommand();
        callEdgeCommand.CommandText = "select name from sqlite_master where type = 'index' and tbl_name = 'call_edges';";
        await using var callEdgeReader = await callEdgeCommand.ExecuteReaderAsync();
        while (await callEdgeReader.ReadAsync())
        {
            callEdgeIndexNames.Add(callEdgeReader.GetString(0));
        }

        Assert.Contains("ix_call_edges_caller", callEdgeIndexNames);
        Assert.Contains("ix_call_edges_callee", callEdgeIndexNames);
        Assert.Contains("ix_call_edges_callee_assembly", callEdgeIndexNames);
        Assert.Contains("ix_call_edges_file", callEdgeIndexNames);

        var objectCreationIndexNames = new HashSet<string>(StringComparer.Ordinal);
        await using var objectCreationCommand = connection.CreateCommand();
        objectCreationCommand.CommandText = "select name from sqlite_master where type = 'index' and tbl_name = 'object_creations';";
        await using var objectCreationReader = await objectCreationCommand.ExecuteReaderAsync();
        while (await objectCreationReader.ReadAsync())
        {
            objectCreationIndexNames.Add(objectCreationReader.GetString(0));
        }

        Assert.Contains("ix_object_creations_type", objectCreationIndexNames);
        Assert.Contains("ix_object_creations_assembly", objectCreationIndexNames);
        Assert.Contains("ix_object_creations_caller", objectCreationIndexNames);

        var argumentFlowIndexNames = new HashSet<string>(StringComparer.Ordinal);
        await using var argumentFlowCommand = connection.CreateCommand();
        argumentFlowCommand.CommandText = "select name from sqlite_master where type = 'index' and tbl_name = 'argument_flows';";
        await using var argumentFlowReader = await argumentFlowCommand.ExecuteReaderAsync();
        while (await argumentFlowReader.ReadAsync())
        {
            argumentFlowIndexNames.Add(argumentFlowReader.GetString(0));
        }

        Assert.Contains("ix_argument_flows_callee", argumentFlowIndexNames);
        Assert.Contains("ix_argument_flows_parameter", argumentFlowIndexNames);
        Assert.Contains("ix_argument_flows_argument_symbol", argumentFlowIndexNames);
        Assert.Contains("ix_argument_flows_argument_source", argumentFlowIndexNames);

        var localAliasIndexNames = new HashSet<string>(StringComparer.Ordinal);
        await using var localAliasCommand = connection.CreateCommand();
        localAliasCommand.CommandText = "select name from sqlite_master where type = 'index' and tbl_name = 'local_aliases';";
        await using var localAliasReader = await localAliasCommand.ExecuteReaderAsync();
        while (await localAliasReader.ReadAsync())
        {
            localAliasIndexNames.Add(localAliasReader.GetString(0));
        }

        Assert.Contains("ix_local_aliases_alias", localAliasIndexNames);
        Assert.Contains("ix_local_aliases_origin", localAliasIndexNames);

        var fieldAliasIndexNames = new HashSet<string>(StringComparer.Ordinal);
        await using var fieldAliasCommand = connection.CreateCommand();
        fieldAliasCommand.CommandText = "select name from sqlite_master where type = 'index' and tbl_name = 'field_aliases';";
        await using var fieldAliasReader = await fieldAliasCommand.ExecuteReaderAsync();
        while (await fieldAliasReader.ReadAsync())
        {
            fieldAliasIndexNames.Add(fieldAliasReader.GetString(0));
        }

        Assert.Contains("ix_field_aliases_field", fieldAliasIndexNames);
        Assert.Contains("ix_field_aliases_origin", fieldAliasIndexNames);

        var parameterForwardIndexNames = new HashSet<string>(StringComparer.Ordinal);
        await using var parameterForwardCommand = connection.CreateCommand();
        parameterForwardCommand.CommandText = "select name from sqlite_master where type = 'index' and tbl_name = 'parameter_forward_edges';";
        await using var parameterForwardReader = await parameterForwardCommand.ExecuteReaderAsync();
        while (await parameterForwardReader.ReadAsync())
        {
            parameterForwardIndexNames.Add(parameterForwardReader.GetString(0));
        }

        Assert.Contains("ix_parameter_forward_edges_source", parameterForwardIndexNames);
        Assert.Contains("ix_parameter_forward_edges_target", parameterForwardIndexNames);
        Assert.Contains("ix_parameter_forward_edges_source_method", parameterForwardIndexNames);
        Assert.Contains("ix_parameter_forward_edges_target_method", parameterForwardIndexNames);
    }

    [Fact]
    public void Constructor_symbol_detection_accepts_metadata_ctor_names()
    {
        var method = typeof(SqliteIndexWriter).GetMethod("IsConstructorSymbol", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.NotNull(method);

        Assert.True((bool)method.Invoke(null, ["global::Demo.Service..ctor(global::Demo.Dependency dependency)"])!);
        Assert.True((bool)method.Invoke(null, ["global::Demo.Service.#ctor(global::Demo.Dependency dependency)"])!);
        Assert.True((bool)method.Invoke(null, ["global::Demo.Service.Service(global::Demo.Dependency dependency)"])!);
        Assert.False((bool)method.Invoke(null, ["global::Demo.Service.Run()"])!);
    }

    [Fact]
    public async Task Parameter_forwarding_derives_direct_and_bounded_local_alias_edges()
    {
        using var temp = new TempDirectory();
        var sqlitePath = await ScanSemanticProjectAsync(temp, "AliasFlowSample", """
            namespace AliasFlowSample;

            public sealed class Request
            {
            }

            public sealed class Sink
            {
                public void Send(Request input)
                {
                }
            }

            public sealed class Handler
            {
                public void Direct(Request request)
                {
                    new Sink().Send(request);
                }

                public void ThreeHop(Request request)
                {
                    var first = request;
                    var second = first;
                    var third = second;
                    new Sink().Send(third);
                }

                public void FourHop(Request request)
                {
                    var first = request;
                    var second = first;
                    var third = second;
                    var fourth = third;
                    new Sink().Send(fourth);
                }
            }
            """);

        await using var connection = new SqliteConnection($"Data Source={sqlitePath}");
        await connection.OpenAsync();

        Assert.Equal(1L, await CountParameterForwardEdgesAsync(connection, "Direct", "Send"));
        Assert.Equal(1L, await CountParameterForwardEdgesAsync(connection, "ThreeHop", "Send"));
        Assert.Equal(0L, await CountParameterForwardEdgesAsync(connection, "FourHop", "Send"));
    }

    [Fact]
    public async Task Parameter_forwarding_uses_unique_constructor_field_origin()
    {
        using var temp = new TempDirectory();
        var sqlitePath = await ScanSemanticProjectAsync(temp, "ConstructorFlowSample", """
            namespace ConstructorFlowSample;

            public sealed class Request
            {
            }

            public sealed class Sink
            {
                public void Send(Request input)
                {
                }
            }

            public sealed class Handler
            {
                private readonly Request cached;

                public Handler(Request request)
                {
                    cached = request;
                }

                public void UseCached()
                {
                    new Sink().Send(cached);
                }
            }
            """);

        await using var connection = new SqliteConnection($"Data Source={sqlitePath}");
        await connection.OpenAsync();

        var constructorForwardCount = await ExecuteScalarAsync<long>(
            connection,
            """
            select count(*)
            from parameter_forward_edges
            where source_method_symbol like '%Handler.%'
              and source_parameter_symbol like '%Request request%'
              and target_method_symbol like '%Sink.Send%'
              and target_parameter_name = 'input';
            """);
        Assert.Equal(1L, constructorForwardCount);
    }

    [Fact]
    public async Task Parameter_forwarding_omits_ambiguous_constructor_field_origin()
    {
        using var temp = new TempDirectory();
        var sqlitePath = await ScanSemanticProjectAsync(temp, "AmbiguousConstructorFlowSample", """
            namespace AmbiguousConstructorFlowSample;

            public sealed class Request
            {
            }

            public sealed class Sink
            {
                public void Send(Request input)
                {
                }
            }

            public sealed class Handler
            {
                private readonly Request cached;

                public Handler(Request request)
                {
                    cached = request;
                }

                public Handler(Request first, Request second)
                {
                    cached = second;
                }

                public void UseCached()
                {
                    new Sink().Send(cached);
                }
            }
            """);

        await using var connection = new SqliteConnection($"Data Source={sqlitePath}");
        await connection.OpenAsync();

        var constructorForwardCount = await ExecuteScalarAsync<long>(
            connection,
            """
            select count(*)
            from parameter_forward_edges
            where source_parameter_symbol like '%Request%'
              and target_method_symbol like '%Sink.Send%'
              and target_parameter_name = 'input';
            """);
        Assert.Equal(0L, constructorForwardCount);
    }

    [Fact]
    public async Task Parameter_forwarding_omits_reassigned_constructor_field_origin_beyond_bound()
    {
        using var temp = new TempDirectory();
        var sqlitePath = await ScanSemanticProjectAsync(temp, "ReassignedConstructorFlowSample", """
            namespace ReassignedConstructorFlowSample;

            public sealed class Request
            {
            }

            public sealed class Sink
            {
                public void Send(Request input)
                {
                }
            }

            public sealed class Handler
            {
                private Request cached;

                public Handler(Request request)
                {
                    cached = request;
                }

                public void UseCached(Request other)
                {
                    var first = other;
                    var second = first;
                    var third = second;
                    var fourth = third;
                    cached = fourth;
                    new Sink().Send(cached);
                }
            }
            """);

        await using var connection = new SqliteConnection($"Data Source={sqlitePath}");
        await connection.OpenAsync();

        var constructorForwardCount = await ExecuteScalarAsync<long>(
            connection,
            """
            select count(*)
            from parameter_forward_edges
            where source_parameter_symbol like '%Request%'
              and target_method_symbol like '%Sink.Send%'
              and target_parameter_name = 'input';
            """);
        Assert.Equal(0L, constructorForwardCount);
    }

    [Fact]
    public async Task Parameter_forwarding_omits_repeated_constructor_field_assignment()
    {
        using var temp = new TempDirectory();
        var sqlitePath = await ScanSemanticProjectAsync(temp, "RepeatedConstructorAssignmentFlowSample", """
            namespace RepeatedConstructorAssignmentFlowSample;

            public sealed class Request
            {
            }

            public sealed class Sink
            {
                public void Send(Request input)
                {
                }
            }

            public sealed class Handler
            {
                private readonly Request cached;

                public Handler(Request request)
                {
                    cached = request;
                    cached = request;
                }

                public void UseCached()
                {
                    new Sink().Send(cached);
                }
            }
            """);

        await using var connection = new SqliteConnection($"Data Source={sqlitePath}");
        await connection.OpenAsync();

        var constructorForwardCount = await ExecuteScalarAsync<long>(
            connection,
            """
            select count(*)
            from parameter_forward_edges
            where source_parameter_symbol like '%Request request%'
              and target_method_symbol like '%Sink.Send%'
              and target_parameter_name = 'input';
            """);
        Assert.Equal(0L, constructorForwardCount);
    }

    private static async Task<string> ScanSemanticProjectAsync(TempDirectory temp, string projectName, string source)
    {
        var repo = Path.Combine(temp.Path, "repo");
        var projectPath = Path.Combine(repo, "src", projectName);
        var outputPath = Path.Combine(temp.Path, "out");
        Directory.CreateDirectory(projectPath);
        File.WriteAllText(Path.Combine(projectPath, $"{projectName}.csproj"), """
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
                <TargetFramework>net10.0</TargetFramework>
              </PropertyGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(projectPath, "Flow.cs"), source);

        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await TraceMapCommand.RunAsync(["scan", "--repo", repo, "--out", outputPath], output, error);
        Assert.True(exitCode == 0, error.ToString());

        return Path.Combine(outputPath, "index.sqlite");
    }

    private static Task<long> CountParameterForwardEdgesAsync(SqliteConnection connection, string sourceMethodName, string targetMethodName)
    {
        return ExecuteScalarAsync<long>(
            connection,
            $"""
            select count(*)
            from parameter_forward_edges
            where source_method_symbol like '%Handler.{sourceMethodName}%'
              and source_parameter_symbol like '%Request request%'
              and target_method_symbol like '%Sink.{targetMethodName}%'
              and target_parameter_name = 'input';
            """);
    }

    private static async Task<T> ExecuteScalarAsync<T>(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var value = await command.ExecuteScalarAsync();
        Assert.NotNull(value);
        return (T)Convert.ChangeType(value, typeof(T));
    }
}
