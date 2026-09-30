using System.Text.Json;
using Mono.Cecil;
using Mono.Cecil.Cil;
using TraceMap.Core;
using TraceMap.Combine;
using TraceMap.Reporting;
using TraceMap.Storage;

namespace TraceMap.Tests;

public sealed class IlCommandBindingExtractorTests
{
    private const string PrivateLiteral = "private-fixture-procedure-never-retained";

    [Fact]
    public void Independent_readers_bind_command_configuration_to_adapter_fill()
    {
        var (body, _) = Fixture();
        var binding = Assert.Single(IlCommandBindingExtractor.Extract(body).Bindings);
        Assert.Equal("constant-string-hash", binding.CommandText.Kind);
        Assert.StartsWith($"str:{PrivateLiteral.Length}:", binding.CommandText.Identity);
        Assert.Equal(new IlValueOrigin("constant-int32", ((int)System.Data.CommandType.StoredProcedure).ToString()), binding.CommandType);
        Assert.Equal("allocation-site", binding.CommandReceiver.Kind);
        Assert.Equal("allocation-site", binding.EndpointReceiver.Kind);
        Assert.NotEqual(binding.CommandReceiver, binding.EndpointReceiver);
        Assert.Equal(3, binding.ConfigurationOffsets.Count);
        Assert.NotNull(binding.AdapterBindingOffset);
        Assert.DoesNotContain(PrivateLiteral, JsonSerializer.Serialize(binding), StringComparison.Ordinal);
    }

    [Fact]
    public void Wrapper_argument_is_retained_as_a_slot_not_a_procedure_value()
    {
        var (body, _) = Fixture(argumentText: true);
        var binding = Assert.Single(IlCommandBindingExtractor.Extract(body).Bindings);
        Assert.Equal(new IlValueOrigin("argument-slot", "0"), binding.CommandText);
    }

    [Theory]
    [InlineData(true, false, "IlCommandAdapterBindingUnavailable")]
    [InlineData(false, true, "IlCommandTextBindingUnavailable")]
    public void Branch_target_or_unknown_command_mutation_withholds_binding(bool branch, bool mutation, string gap)
    {
        var (body, _) = Fixture(branch: branch, mutation: mutation);
        var result = IlCommandBindingExtractor.Extract(body);
        Assert.Empty(result.Bindings);
        Assert.Contains(gap, result.Gaps);
    }

    [Fact]
    public void Framework_name_without_expected_token_does_not_create_a_binding()
    {
        var (body, _) = Fixture(lookalike: true);
        Assert.Empty(IlCommandBindingExtractor.Extract(body).Bindings);
    }

    [Fact]
    public void CommandText_member_name_with_wrong_signature_does_not_authorize_configuration()
    {
        var (body, _) = Fixture(wrongTextSetter: true);
        var result = IlCommandBindingExtractor.Extract(body);
        Assert.Empty(result.Bindings);
        Assert.Contains("IlCommandTextBindingUnavailable", result.Gaps);
    }

    [Fact]
    public void Aggregate_work_limit_fails_closed_without_partial_body_bindings()
    {
        var (body, _) = Fixture();
        var result = IlCommandBindingExtractor.Extract(body, new IlBodyEvidenceExtractor.IlWorkBudget(1));
        Assert.Empty(result.Bindings);
        Assert.Contains("IlCommandBindingWorkLimit", result.Gaps);
    }

    [Fact]
    public void Scan_materializes_joined_command_candidates_with_exact_provenance_and_no_literal()
    {
        var (_, bytes) = Fixture();
        var root = Directory.CreateTempSubdirectory("tracemap-il-command-test-").FullName;
        var assemblyPath = Path.Combine(root, "CommandFixture.dll");
        File.WriteAllBytes(assemblyPath, bytes);
        var source = Path.Combine(FindRepoRoot(), "samples", "vb-modern-sample");
        var scan = ScanEngine.Scan(new ScanOptions(source, Path.Combine(root, "out"),
            CompiledInputPaths: [assemblyPath], IlBodyEvidence: true));
        var candidate = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.ManagedIlDatabaseCommandCandidate);
        Assert.Equal(RuleIds.DotNetIlCommandBinding, candidate.RuleId);
        Assert.Equal(EvidenceTiers.Tier3SyntaxOrTextual, candidate.EvidenceTier);
        Assert.Equal(scan.Manifest.IlBodyProvenance!.GeneratorSha256, candidate.Properties["ilGeneratorSha256"]);
        Assert.Equal(scan.Manifest.IlBodyProvenance.BoundedInputSha256, candidate.Properties["ilBoundedInputSha256"]);
        var byId = scan.Facts.ToDictionary(fact => fact.FactId);
        Assert.Equal(FactTypes.ManagedIlBodyDeclared, byId[candidate.Properties["ilBodyFactId"]].FactType);
        Assert.Equal(FactTypes.ManagedIlCallObserved, byId[candidate.Properties["ilCallFactId"]].FactType);
        foreach (var id in JsonSerializer.Deserialize<string[]>(candidate.Properties["configurationCallFactIds"])!)
        {
            Assert.Equal(FactTypes.ManagedIlCallObserved, byId[id].FactType);
            Assert.Equal(candidate.Properties["ilBodyFactId"], byId[id].Properties["ilBodyFactId"]);
        }
        Assert.DoesNotContain(PrivateLiteral, JsonSerializer.Serialize(scan), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Compiled_handler_path_retains_command_binding_only_with_exact_fact_provenance(bool tamper)
    {
        var (_, bytes) = Fixture();
        var root = Directory.CreateTempSubdirectory("tracemap-il-command-path-").FullName;
        var assemblyPath = Path.Combine(root, "CommandFixture.dll");
        File.WriteAllBytes(assemblyPath, bytes);
        var sourcePath = Path.Combine(FindRepoRoot(), "samples", "vb-modern-sample");
        var scan = ScanEngine.Scan(new ScanOptions(sourcePath, Path.Combine(root, "out"),
            CompiledInputPaths: [assemblyPath], IlBodyEvidence: true));
        var candidate = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.ManagedIlDatabaseCommandCandidate);
        var body = scan.Facts.Single(fact => fact.FactId == candidate.Properties["ilBodyFactId"]);
        var method = scan.Facts.Single(fact => fact.FactId == body.Properties["compiledFactId"]);
        var facts = scan.Facts.ToArray();
        if (tamper)
        {
            var properties = new Dictionary<string, string>(candidate.Properties) { ["ilBoundedInputSha256"] = new string('f', 64) };
            facts[Array.IndexOf(facts, candidate)] = candidate with { Properties = properties };
        }
        var index = Path.Combine(root, "index.sqlite"); var combined = Path.Combine(root, "combined.sqlite");
        SqliteIndexWriter.Write(index, scan.Manifest, facts);
        var result = await CombinedIndexBuilder.CombineAsync(new CombineOptions([index], combined, ["fixture"]));
        var source = Assert.Single(result.Sources);
        var selector = new CombinedPathSymbolRoot(source.SourceIndexId, source.ScanId, source.CommitSha, method.TargetSymbol!);
        var options = new CombinedDependencyPathOptions(combined, root, ToSurface: "database-api", SurfaceName: "DbDataAdapter.Fill", MaxDepth: 10)
        { CompiledOnly = true, ExactFromSymbol = true, MaxTraversalWork = 10_000 };
        var report = await CombinedDependencyPathReporter.BuildSelectedSymbolsAsync(options, [selector], combinedIndex: true);
        var path = Assert.Single(report.Paths);
        var endpoint = Assert.Single(path.Nodes, node => node.SurfaceName == "DbDataAdapter.Fill");
        if (tamper)
        {
            Assert.Null(endpoint.CommandBinding);
            Assert.Contains(report.Gaps, gap => gap.GapKind == "CompiledIlCommandBindingUnavailable");
        }
        else
        {
            var binding = Assert.IsType<CompiledCommandConfigurationCandidate>(endpoint.CommandBinding);
            Assert.Equal("constant-string-hash", binding.CommandTextOrigin.Kind);
            Assert.Equal("4", binding.CommandTypeOrigin.Identity);
            Assert.Equal(candidate.Properties["ilGeneratorSha256"], binding.GeneratorSha256);
            Assert.Equal(candidate.Properties["ilBoundedInputSha256"], binding.BoundedInputSha256);
            Assert.Contains(path.Edges.SelectMany(edge => edge.SupportingFactIds), id => id == binding.CombinedFactId);
        }
        Assert.DoesNotContain(PrivateLiteral, JsonSerializer.Serialize(report), StringComparison.Ordinal);
        Assert.All(path.Edges, edge => Assert.True(CombinedDependencyPathReporter.CompiledBaselineAllowsEdge(edge.EdgeKind, true)));
    }

    private static (IlBodyObservation Body, byte[] Bytes) Fixture(bool argumentText = false,
        bool branch = false, bool mutation = false, bool lookalike = false, bool wrongTextSetter = false)
    {
        using var assembly = AssemblyDefinition.CreateAssembly(new AssemblyNameDefinition("CommandFixture", new Version(1, 0)), "CommandFixture", ModuleKind.Dll);
        var module = assembly.MainModule;
        var data = new AssemblyNameReference("System.Data", new Version(4, 0, 0, 0))
        { PublicKeyToken = lookalike ? [] : Convert.FromHexString("b77a5c561934e089") };
        module.AssemblyReferences.Add(data);
        TypeReference Type(string ns, string name, bool valueType = false) => new(ns, name, module, data, valueType);
        var command = Type("System.Data.SqlClient", "SqlCommand");
        var adapter = Type("System.Data.SqlClient", "SqlDataAdapter");
        var commandBase = Type("System.Data.Common", "DbCommand");
        var adapterBase = Type("System.Data.Common", "DbDataAdapter");
        var commandType = Type("System.Data", "CommandType", true);
        var dataSet = Type("System.Data", "DataSet");
        MethodReference Method(TypeReference owner, string name, TypeReference result, params TypeReference[] parameters)
        {
            var reference = new MethodReference(name, result, owner) { HasThis = true };
            foreach (var parameter in parameters) reference.Parameters.Add(new ParameterDefinition(parameter));
            return reference;
        }
        var type = new TypeDefinition("Fixture", "Handler", TypeAttributes.Public, module.TypeSystem.Object);
        module.Types.Add(type);
        var method = new MethodDefinition("Run", MethodAttributes.Public | MethodAttributes.Static, module.TypeSystem.Void);
        if (argumentText) method.Parameters.Add(new ParameterDefinition(module.TypeSystem.String));
        type.Methods.Add(method);
        method.Body.InitLocals = true;
        method.Body.Variables.Add(new VariableDefinition(command));
        method.Body.Variables.Add(new VariableDefinition(adapter));
        var il = method.Body.GetILProcessor();
        var endpointLoad = Instruction.Create(OpCodes.Ldloc_1);
        if (branch) il.Emit(OpCodes.Br, endpointLoad);
        il.Emit(OpCodes.Newobj, Method(command, ".ctor", module.TypeSystem.Void));
        il.Emit(OpCodes.Stloc_0);
        il.Emit(OpCodes.Ldloc_0);
        if (argumentText) il.Emit(OpCodes.Ldarg_0); else il.Emit(OpCodes.Ldstr, PrivateLiteral);
        il.Emit(OpCodes.Callvirt, Method(commandBase, "set_CommandText", module.TypeSystem.Void,
            wrongTextSetter ? module.TypeSystem.Object : module.TypeSystem.String));
        il.Emit(OpCodes.Ldloc_0);
        il.Emit(OpCodes.Ldc_I4, (int)System.Data.CommandType.StoredProcedure);
        il.Emit(OpCodes.Callvirt, Method(commandBase, "set_CommandType", module.TypeSystem.Void, commandType));
        il.Emit(OpCodes.Ldloc_0);
        il.Emit(OpCodes.Newobj, Method(adapter, ".ctor", module.TypeSystem.Void, command));
        il.Emit(OpCodes.Stloc_1);
        if (mutation)
        {
            var mutate = new MethodReference("Mutate", module.TypeSystem.Void, type);
            mutate.Parameters.Add(new ParameterDefinition(command));
            il.Emit(OpCodes.Ldloc_0); il.Emit(OpCodes.Call, mutate);
        }
        il.Append(endpointLoad);
        il.Emit(OpCodes.Newobj, Method(dataSet, ".ctor", module.TypeSystem.Void));
        il.Emit(OpCodes.Callvirt, Method(adapterBase, "Fill", module.TypeSystem.Int32, dataSet));
        il.Emit(OpCodes.Pop); il.Emit(OpCodes.Ret);
        using var stream = new MemoryStream();
        assembly.Write(stream);
        var bytes = stream.ToArray();
        var limits = new IlBodyLimits();
        var cecil = IlBodyEvidenceExtractor.ReadCecilBodies(bytes, limits, new IlBodyEvidenceExtractor.IlWorkBudget(100_000), CancellationToken.None);
        var srm = IlBodyEvidenceExtractor.ReadSystemReflectionMetadataBodies(bytes, limits, new IlBodyEvidenceExtractor.IlWorkBudget(100_000), CancellationToken.None);
        Assert.Empty(IlBodyEvidenceExtractor.CompareBodies(cecil, srm));
        var body = Assert.Single(IlBodyEvidenceExtractor.AgreeValueFlows(cecil.Bodies, srm.Bodies));
        Assert.DoesNotContain("IlValueReaderDisagreementOrLimit", body.ValueFlow!.Gaps);
        return (body, bytes);
    }

    private static string FindRepoRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))) return directory.FullName;
        throw new InvalidOperationException("TraceMap repository root not found.");
    }
}
