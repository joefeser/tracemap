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

    [Theory]
    [InlineData("none", true, true)]
    [InlineData("none", true, false)]
    [InlineData("collection-escape", false, true)]
    [InlineData("collection-escape", false, false)]
    [InlineData("command-escape", false, true)]
    [InlineData("conditional-text", false, true)]
    [InlineData("wrong-signature", false, true)]
    [InlineData("wrong-signature", false, false)]
    public void Parameter_loop_preserves_only_unexposed_agreed_configuration(string effect, bool expected, bool loop)
    {
        var (body, _) = Fixture(parameterLoop: loop, parameterOnly: !loop, loopEffect: effect);
        if (loop) Assert.NotNull(body.ValueFlow!.ControlFlow);
        else Assert.Null(body.ValueFlow!.ControlFlow);
        var result = IlCommandBindingExtractor.Extract(body);
        if (expected)
        {
            var binding = Assert.Single(result.Bindings);
            Assert.Equal("constant-string-hash", binding.CommandText.Kind);
            Assert.Equal(new IlValueOrigin("constant-int32", "4"), binding.CommandType);
            Assert.Contains("IlCommandParameterFlowUnavailable", result.Gaps);
        }
        else Assert.Empty(result.Bindings);
    }

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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Aggregate_work_limit_fails_closed_without_partial_body_bindings(bool parameterLoop)
    {
        var (body, _) = Fixture(parameterLoop: parameterLoop);
        var result = IlCommandBindingExtractor.Extract(body, new IlBodyEvidenceExtractor.IlWorkBudget(1));
        Assert.Empty(result.Bindings);
        Assert.Contains("IlCommandBindingWorkLimit", result.Gaps);
    }

    [Fact]
    public void Independent_control_flow_disagreement_withholds_all_command_candidates()
    {
        var (body, _) = Fixture(parameterLoop: true);
        var nodes = body.ValueFlow!.ControlFlow!.ToArray();
        nodes[0] = nodes[0] with { InvalidatesConfiguration = !nodes[0].InvalidatesConfiguration };
        var disagreed = Assert.Single(IlBodyEvidenceExtractor.AgreeValueFlows([body],
            [body with { ValueFlow = body.ValueFlow with { ControlFlow = nodes } }]));
        Assert.Contains("IlValueReaderDisagreementOrLimit", disagreed.ValueFlow!.Gaps);
        Assert.Empty(IlCommandBindingExtractor.Extract(disagreed).Bindings);
    }

    [Fact]
    public void Exhausted_operand_budget_preserves_independently_decoded_body_and_call_evidence()
    {
        var (_, bytes) = Fixture(parameterLoop: true);
        var limits = new IlBodyLimits();
        var cecil = IlBodyEvidenceExtractor.ReadCecilBodies(bytes, limits, new IlBodyEvidenceExtractor.IlWorkBudget(100_000),
            CancellationToken.None, valueBudget: new IlBodyEvidenceExtractor.IlWorkBudget(1));
        var srm = IlBodyEvidenceExtractor.ReadSystemReflectionMetadataBodies(bytes, limits, new IlBodyEvidenceExtractor.IlWorkBudget(100_000),
            CancellationToken.None, valueBudget: new IlBodyEvidenceExtractor.IlWorkBudget(1));
        Assert.Empty(IlBodyEvidenceExtractor.CompareBodies(cecil, srm));
        var body = Assert.Single(IlBodyEvidenceExtractor.AgreeValueFlows(cecil.Bodies, srm.Bodies));
        Assert.NotEmpty(body.Calls);
        Assert.Contains("IlValueWorkLimitExceeded", body.ValueFlow!.Gaps);
        Assert.Empty(IlCommandBindingExtractor.Extract(body).Bindings);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Scan_materializes_joined_command_candidates_with_exact_provenance_and_no_literal(bool parameterLoop)
    {
        var (_, bytes) = Fixture(parameterLoop: parameterLoop);
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
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Compiled_handler_path_retains_command_binding_only_with_exact_fact_provenance(bool tamper, bool parameterLoop)
    {
        var (_, bytes) = Fixture(parameterLoop: parameterLoop);
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

    [Theory]
    [InlineData(1, false, "none")]
    [InlineData(2, false, "none")]
    [InlineData(1, true, "none")]
    [InlineData(2, false, "missing")]
    [InlineData(2, false, "changed")]
    [InlineData(2, false, "ambiguous")]
    [InlineData(2, false, "source-bridge")]
    public async Task Compiled_caller_argument_substitution_is_path_specific_and_provenance_bound(int hops, bool instance, string tamper)
    {
        var (_, bytes) = Fixture(argumentText: true, wrapperDepth: hops, instanceRun: instance);
        var root = Directory.CreateTempSubdirectory("tracemap-il-command-caller-").FullName;
        var assemblyPath = Path.Combine(root, "CommandFixture.dll"); File.WriteAllBytes(assemblyPath, bytes);
        var scan = ScanEngine.Scan(new ScanOptions(Path.Combine(FindRepoRoot(), "samples", "vb-modern-sample"),
            Path.Combine(root, "out"), CompiledInputPaths: [assemblyPath], IlBodyEvidence: true));
        var entry = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.ManagedMethodDeclared
            && fact.Properties.GetValueOrDefault("metadataName") == "Entry");
        var entryBody = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.ManagedIlBodyDeclared
            && fact.Properties.GetValueOrDefault("compiledFactId") == entry.FactId);
        var entryCall = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.ManagedIlCallObserved
            && fact.Properties.GetValueOrDefault("ilBodyFactId") == entryBody.FactId
            && fact.Properties.GetValueOrDefault("opcode") == "call");
        var operand = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.ManagedIlCallValuesObserved
            && fact.Properties.GetValueOrDefault("ilCallFactId") == entryCall.FactId);
        var facts = scan.Facts.ToList();
        if (tamper == "missing") facts.Remove(operand);
        if (tamper == "changed")
            facts[facts.IndexOf(operand)] = operand with
            { Properties = new Dictionary<string, string>(operand.Properties) { ["ilBoundedInputSha256"] = new string('f', 64) } };
        if (tamper == "ambiguous")
            facts.Add(FactFactory.Create(scan.Manifest, operand.FactType, operand.RuleId, operand.EvidenceTier,
                operand.Evidence, targetSymbol: operand.TargetSymbol, contractElement: operand.ContractElement,
                properties: new Dictionary<string, string>(operand.Properties) { ["syntheticCompetitor"] = "true" }));
        if (tamper == "source-bridge")
        {
            facts.Remove(entryCall);
            facts.Add(FactFactory.Create(scan.Manifest, FactTypes.CallEdge, RuleIds.CSharpSemanticCallGraph,
                EvidenceTiers.Tier1Semantic, new EvidenceSpan("synthetic.cs", 1, 1, null, "test", "test/1"),
                sourceSymbol: entry.TargetSymbol, targetSymbol: entryCall.Properties["targetIdentity"],
                properties: new Dictionary<string, string> { ["callKind"] = "method", ["targetSymbolId"] = entryCall.Properties["targetIdentity"] }));
        }
        var index = Path.Combine(root, "index.sqlite"); var combined = Path.Combine(root, "combined.sqlite");
        SqliteIndexWriter.Write(index, scan.Manifest, facts);
        var combine = await CombinedIndexBuilder.CombineAsync(new CombineOptions([index], combined, ["fixture"]));
        var source = Assert.Single(combine.Sources);
        var selector = new CombinedPathSymbolRoot(source.SourceIndexId, source.ScanId, source.CommitSha, entry.TargetSymbol!);
        var options = new CombinedDependencyPathOptions(combined, root, ToSurface: "database-api", SurfaceName: "DbDataAdapter.Fill", MaxDepth: 10)
        { CompiledOnly = tamper != "source-bridge", ExactFromSymbol = true, MaxTraversalWork = 10_000 };
        var report = await CombinedDependencyPathReporter.BuildSelectedSymbolsAsync(options, [selector], combinedIndex: true);
        var endpoint = Assert.Single(Assert.Single(report.Paths).Nodes, node => node.SurfaceName == "DbDataAdapter.Fill");
        var binding = Assert.IsType<CompiledCommandConfigurationCandidate>(endpoint.CommandBinding);
        Assert.Equal("argument-slot", binding.CommandTextOrigin.Kind);
        Assert.Equal(instance ? "1" : "0", binding.CommandTextOrigin.Identity);
        var mapped = Assert.IsType<CompiledCommandPathValueBinding>(binding.CommandTextFromPath);
        if (tamper == "none")
        {
            Assert.Equal("constant-on-encoded-call-path", mapped.State);
            Assert.Equal("constant-string-hash", mapped.Origin.Kind);
            Assert.Equal(hops, mapped.Steps.Count);
            Assert.Empty(mapped.Gaps);
            Assert.Equal(Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(
                File.ReadAllBytes(typeof(CombinedDependencyPathReporter).Assembly.Location))), mapped.GeneratorSha256);
            Assert.Matches("^[0-9a-f]{64}$", mapped.BoundedInputSha256);
        }
        else if (tamper == "source-bridge")
        {
            Assert.Equal("unresolved-non-il-bridge", mapped.State);
            Assert.Contains("IlCommandNonIlCallerBridge", mapped.Gaps);
        }
        else
        {
            Assert.Equal("unresolved-call-evidence", mapped.State);
            Assert.Equal("argument-slot", mapped.Origin.Kind);
            Assert.Contains(mapped.Gaps, gap => gap is "IlCommandCallerOperandMissingOrAmbiguous" or "IlCommandCallerOperandProvenanceUnavailable");
        }
        Assert.DoesNotContain(PrivateLiteral, JsonSerializer.Serialize(report), StringComparison.Ordinal);
        var repeated = await CombinedDependencyPathReporter.BuildSelectedSymbolsAsync(options, [selector], combinedIndex: true);
        var repeatedBinding = Assert.Single(Assert.Single(repeated.Paths).Nodes,
            node => node.SurfaceName == "DbDataAdapter.Fill").CommandBinding;
        Assert.Equal(JsonSerializer.Serialize(binding), JsonSerializer.Serialize(repeatedBinding));
    }

    private static (IlBodyObservation Body, byte[] Bytes) Fixture(bool argumentText = false,
        bool branch = false, bool mutation = false, bool lookalike = false, bool wrongTextSetter = false,
        int wrapperDepth = 0, bool instanceRun = false, bool parameterLoop = false, string loopEffect = "none", bool parameterOnly = false)
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
        var method = new MethodDefinition("Run", MethodAttributes.Public | (instanceRun ? 0 : MethodAttributes.Static), module.TypeSystem.Void);
        if (argumentText) method.Parameters.Add(new ParameterDefinition(module.TypeSystem.String));
        type.Methods.Add(method);
        method.Body.InitLocals = true;
        method.Body.Variables.Add(new VariableDefinition(command));
        method.Body.Variables.Add(new VariableDefinition(adapter));
        if (parameterLoop || parameterOnly) method.Body.Variables.Add(new VariableDefinition(module.TypeSystem.Int32));
        var il = method.Body.GetILProcessor();
        var endpointLoad = Instruction.Create(OpCodes.Ldloc_1);
        if (branch) il.Emit(OpCodes.Br, endpointLoad);
        il.Emit(OpCodes.Newobj, Method(command, ".ctor", module.TypeSystem.Void));
        il.Emit(OpCodes.Stloc_0);
        il.Emit(OpCodes.Ldloc_0);
        if (argumentText) il.Emit(instanceRun ? OpCodes.Ldarg_1 : OpCodes.Ldarg_0); else il.Emit(OpCodes.Ldstr, PrivateLiteral);
        il.Emit(OpCodes.Callvirt, Method(commandBase, "set_CommandText", module.TypeSystem.Void,
            wrongTextSetter ? module.TypeSystem.Object : module.TypeSystem.String));
        il.Emit(OpCodes.Ldloc_0);
        il.Emit(OpCodes.Ldc_I4, (int)System.Data.CommandType.StoredProcedure);
        il.Emit(OpCodes.Callvirt, Method(commandBase, "set_CommandType", module.TypeSystem.Void, commandType));
        il.Emit(OpCodes.Ldloc_0);
        il.Emit(OpCodes.Newobj, Method(adapter, ".ctor", module.TypeSystem.Void, command));
        il.Emit(OpCodes.Stloc_1);
        if (parameterLoop || parameterOnly)
        {
            var collection = Type("System.Data.SqlClient", "SqlParameterCollection");
            var parameter = Type("System.Data.SqlClient", "SqlParameter");
            il.Emit(OpCodes.Ldc_I4_0); il.Emit(OpCodes.Stloc_2);
            var loop = Instruction.Create(OpCodes.Ldloc_0); il.Append(loop);
            il.Emit(OpCodes.Callvirt, Method(command, "get_Parameters", collection));
            if (loopEffect == "collection-escape")
            {
                var mutateCollection = new MethodReference("MutateCollection", module.TypeSystem.Void, type);
                mutateCollection.Parameters.Add(new ParameterDefinition(collection)); il.Emit(OpCodes.Call, mutateCollection);
            }
            else
            {
                il.Emit(OpCodes.Newobj, Method(parameter, ".ctor", module.TypeSystem.Void));
                il.Emit(OpCodes.Callvirt, Method(collection, "Add", module.TypeSystem.Int32,
                    loopEffect == "wrong-signature" ? command : module.TypeSystem.Object)); il.Emit(OpCodes.Pop);
            }
            if (loopEffect == "command-escape")
            {
                var mutateCommand = new MethodReference("MutateCommand", module.TypeSystem.Void, type);
                mutateCommand.Parameters.Add(new ParameterDefinition(command));
                il.Emit(OpCodes.Ldloc_0); il.Emit(OpCodes.Call, mutateCommand);
            }
            if (loopEffect == "conditional-text")
            {
                var skip = Instruction.Create(OpCodes.Nop);
                il.Emit(OpCodes.Ldloc_2); il.Emit(OpCodes.Brfalse_S, skip);
                il.Emit(OpCodes.Ldloc_0); il.Emit(OpCodes.Ldstr, "different-private-text");
                il.Emit(OpCodes.Callvirt, Method(commandBase, "set_CommandText", module.TypeSystem.Void, module.TypeSystem.String)); il.Append(skip);
            }
            if (parameterLoop)
            {
                il.Emit(OpCodes.Ldloc_2); il.Emit(OpCodes.Ldc_I4_1); il.Emit(OpCodes.Add); il.Emit(OpCodes.Stloc_2);
                il.Emit(OpCodes.Ldloc_2); il.Emit(OpCodes.Ldc_I4_3); il.Emit(OpCodes.Blt_S, loop);
            }
        }
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
        MethodDefinition? instanceConstructor = null;
        if (instanceRun)
        {
            instanceConstructor = new MethodDefinition(".ctor", MethodAttributes.Public | MethodAttributes.SpecialName
                | MethodAttributes.RTSpecialName, module.TypeSystem.Void);
            type.Methods.Add(instanceConstructor);
            var ctorIl = instanceConstructor.Body.GetILProcessor(); ctorIl.Emit(OpCodes.Ldarg_0);
            ctorIl.Emit(OpCodes.Call, Method(module.TypeSystem.Object, ".ctor", module.TypeSystem.Void)); ctorIl.Emit(OpCodes.Ret);
        }
        var target = method;
        for (var level = 1; level <= wrapperDepth; level++)
        {
            var entry = level == wrapperDepth;
            var forward = new MethodDefinition(entry ? "Entry" : $"Forward{level}",
                MethodAttributes.Public | MethodAttributes.Static, module.TypeSystem.Void);
            if (!entry) forward.Parameters.Add(new ParameterDefinition(module.TypeSystem.String));
            type.Methods.Add(forward);
            var forwardIl = forward.Body.GetILProcessor();
            if (target.HasThis) forwardIl.Emit(OpCodes.Newobj, instanceConstructor!);
            if (entry) forwardIl.Emit(OpCodes.Ldstr, PrivateLiteral); else forwardIl.Emit(OpCodes.Ldarg_0);
            forwardIl.Emit(OpCodes.Call, target); forwardIl.Emit(OpCodes.Ret); target = forward;
        }
        using var stream = new MemoryStream();
        assembly.Write(stream);
        var bytes = stream.ToArray();
        var limits = new IlBodyLimits();
        var cecil = IlBodyEvidenceExtractor.ReadCecilBodies(bytes, limits, new IlBodyEvidenceExtractor.IlWorkBudget(100_000), CancellationToken.None);
        var srm = IlBodyEvidenceExtractor.ReadSystemReflectionMetadataBodies(bytes, limits, new IlBodyEvidenceExtractor.IlWorkBudget(100_000), CancellationToken.None);
        Assert.Empty(IlBodyEvidenceExtractor.CompareBodies(cecil, srm));
        var body = Assert.Single(IlBodyEvidenceExtractor.AgreeValueFlows(cecil.Bodies, srm.Bodies),
            item => item.MethodIdentity.Contains("|method:3:Run|", StringComparison.Ordinal));
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
