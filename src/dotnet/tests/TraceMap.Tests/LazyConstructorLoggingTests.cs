using System.Text.Json;
using TraceMap.Core;
using TraceMap.Combine;
using TraceMap.Reporting;
using TraceMap.Storage;

namespace TraceMap.Tests;

[Collection("Git metadata sensitive")]
public sealed class LazyConstructorLoggingTests
{
    [Fact]
    public void Structured_vb_profile_preserves_stack_shape_without_inventing_rewritten_argument_origin()
    {
        var repo = FindRepo();
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var binary = Path.Combine(repo, "samples", "fixture-build", "lazy-constructor", "bin", configuration, "net48", "PublicLazy.Website.dll");
        using (var module = Mono.Cecil.ModuleDefinition.ReadModule(binary))
        {
            var method = Assert.Single(module.Types.Single(type => type.Name == "StructuredProfileProbe").Methods,
                method => method.Name == "Lookup");
            Assert.Equal(3, method.Body.ExceptionHandlers.Count);
            Assert.Equal(2, method.Body.Instructions.Count(instruction => instruction.OpCode == Mono.Cecil.Cil.OpCodes.Ldftn));
            Assert.Contains(method.Body.Instructions, instruction => instruction.OpCode == Mono.Cecil.Cil.OpCodes.Starg_S);
        }
        using var temp = new TempDirectory();
        var scan = ScanEngine.Scan(new ScanOptions(Path.Combine(repo, "samples", "messy-dotnet-workspace", "vb-lazy-constructor"),
            Path.Combine(temp.Path, "scan"), CompiledInputPaths: [binary], IlBodyEvidence: true));
        AssertScanIdentity(scan);
        var call = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.ManagedIlCallObserved
            && fact.Properties.GetValueOrDefault("targetIdentity")?.Contains("ObserveOperand", StringComparison.Ordinal) == true);
        var operand = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.ManagedIlCallValuesObserved
            && fact.Properties.GetValueOrDefault("ilBodyFactId") == call.Properties["ilBodyFactId"]
            && fact.Properties.GetValueOrDefault("ilOffset") == call.Properties["ilOffset"]);
        Assert.Equal("control-flow-candidate", operand.Properties["valueState"]);
        var origins = System.Text.Json.JsonSerializer.Deserialize<IlValueOrigin[]>(operand.Properties["argumentOrigins"])!;
        Assert.Equal("argument-alternatives", Assert.Single(origins).Kind);
        var choices = System.Text.Json.JsonSerializer.Deserialize<IlValueOrigin[]>(origins[0].Identity)!;
        Assert.Equal(new[] { "argument-slot", "call-result" }, choices.Select(choice => choice.Kind));
    }

    [Theory]
    [InlineData("none")]
    [InlineData("nested")]
    [InlineData("oversized")]
    [InlineData("wrong-hash")]
    [InlineData("expression-cycle")]
    [InlineData("getter")]
    [InlineData("runtime")]
    public async Task Structured_profile_batch_traces_both_argument_alternatives_into_nested_SQL_composition(string tamper)
    {
        var repo = FindRepo();
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var bin = Path.Combine(repo, "samples", "fixture-build", "lazy-constructor", "bin", configuration, "net48");
        using var temp = new TempDirectory();
        var scan = ScanEngine.Scan(new ScanOptions(Path.Combine(repo, "samples", "messy-dotnet-workspace", "vb-lazy-constructor"),
            Path.Combine(temp.Path, "scan"), CompiledInputPaths: [Path.Combine(bin, "PublicLazy.Website.dll"),
                Path.Combine(bin, "PublicLazy.Framework.dll")], IlBodyEvidence: true));
        AssertScanIdentity(scan);
        var entry = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.ManagedMethodDeclared
            && fact.Properties.GetValueOrDefault("metadataName") == (tamper == "runtime" ? "BatchRuntimeProfile_Click" : tamper == "getter" ? "BatchGetterProfile_Click" : "BatchProfile_Click"));
        var index = Path.Combine(temp.Path, "index.sqlite");
        var combined = Path.Combine(temp.Path, "combined.sqlite");
        var facts = scan.Facts.ToList();
        if (tamper == "expression-cycle")
        {
            // Synthetic retained-origin cycle: the previous loop iteration's
            // value may originate at this same concatenation instruction.
            var lookupCall = Assert.Single(facts, fact => fact.FactType == FactTypes.ManagedIlCallObserved
                && fact.Properties.GetValueOrDefault("targetIdentity")?.Contains("ObserveOperand", StringComparison.Ordinal) == true);
            var producer = facts.Where(fact => fact.FactType == FactTypes.ManagedIlCallObserved
                && fact.Properties.GetValueOrDefault("ilBodyFactId") == lookupCall.Properties["ilBodyFactId"]
                && fact.Properties.GetValueOrDefault("targetIdentity")?.Contains("Concat", StringComparison.Ordinal) == true)
                .OrderBy(fact => int.Parse(fact.Properties["ilOffset"], System.Globalization.CultureInfo.InvariantCulture)).First();
            var operand = Assert.Single(facts, fact => fact.FactType == FactTypes.ManagedIlCallValuesObserved
                && fact.Properties.GetValueOrDefault("ilCallFactId") == producer.FactId);
            var properties = new Dictionary<string, string>(operand.Properties);
            var origins = JsonSerializer.Deserialize<IlValueOrigin[]>(properties["argumentOrigins"])!;
            origins[1] = new("call-result", producer.Properties["ilOffset"]);
            properties["argumentOrigins"] = JsonSerializer.Serialize(origins);
            facts[facts.IndexOf(operand)] = operand with { Properties = properties };
        }
        else if (tamper is not ("none" or "getter" or "runtime"))
        {
            var call = Assert.Single(facts, fact => fact.FactType == FactTypes.ManagedIlCallObserved
                && fact.Properties.GetValueOrDefault("targetIdentity")?.Contains("ObserveOperand", StringComparison.Ordinal) == true);
            var operand = Assert.Single(facts, fact => fact.FactType == FactTypes.ManagedIlCallValuesObserved
                && fact.Properties.GetValueOrDefault("ilCallFactId") == call.FactId);
            var properties = new Dictionary<string, string>(operand.Properties);
            if (tamper == "wrong-hash") properties["ilBoundedInputSha256"] = new string('f', 64);
            else properties["argumentOrigins"] = JsonSerializer.Serialize(new[] { new IlValueOrigin("argument-alternatives",
                tamper == "oversized" ? new string('x', 769) : JsonSerializer.Serialize(new[] {
                    new IlValueOrigin("argument-alternatives", "[]"), new IlValueOrigin("argument-slot", "1") })) });
            facts[facts.IndexOf(operand)] = operand with { Properties = properties };
        }
        SqliteIndexWriter.Write(index, scan.Manifest, facts);
        var result = await CombinedIndexBuilder.CombineAsync(new CombineOptions([index], combined, ["synthetic-batch"]));
        var source = Assert.Single(result.Sources);
        var options = new CombinedDependencyPathOptions(combined, temp.Path, ToSurface: "database-api", MaxDepth: 20, MaxPaths: 256)
            { CompiledOnly = true, ExactFromSymbol = true, MaxTraversalWork = 100_000 };
        var report = await CombinedDependencyPathReporter.BuildSelectedSymbolsAsync(options,
            [new(source.SourceIndexId, source.ScanId, source.CommitSha, entry.TargetSymbol!)], combinedIndex: true);
        var path = Assert.Single(report.Paths);
        Assert.Equal("SqlCommand.ExecuteScalar", path.Nodes.Last().SurfaceName);
        var text = path.Nodes.Last().CommandBinding!.CommandTextFromPath!;
        var middle = text.Composition!.OperandBindings[1];
        if (tamper == "expression-cycle")
        {
            var expressions = new List<CompiledCommandPathValueBinding>();
            void Walk(CompiledCommandPathValueBinding value)
            {
                expressions.Add(value);
                Assert.True(expressions.Count <= 32);
                foreach (var child in (value.Alternatives ?? []).Concat(value.Composition?.OperandBindings ?? [])) Walk(child);
            }
            Walk(text);
            Assert.Contains(expressions, value => value.Gaps.Contains("IlCommandExpressionLimit"));
            return;
        }
        if (tamper is not ("none" or "getter" or "runtime"))
        {
            Assert.Equal("unresolved-call-evidence", middle.State);
            Assert.Null(middle.Alternatives);
            Assert.NotEmpty(middle.Gaps);
            return;
        }
        Assert.Equal("symbolic-argument-alternatives", middle.State);
        Assert.Equal(2, middle.Alternatives!.Count);
        var unchanged = Assert.Single(middle.Alternatives, alternative => alternative.Composition is null);
        Assert.Equal(tamper == "runtime" ? "symbolic-method-return" : "constant-on-encoded-call-path", unchanged.State);
        var rewritten = Assert.Single(middle.Alternatives, alternative => alternative.Composition is not null);
        Assert.Equal("System.String.Concat", rewritten.Composition!.Operation);
        Assert.Equal(2, rewritten.Composition.OperandBindings.Count);
        if (tamper == "runtime")
        {
            Assert.Equal("constant-string-hash", rewritten.Composition.OperandBindings[0].Origin.Kind);
            foreach (var input in new[] { unchanged, rewritten.Composition.OperandBindings[1] })
            {
                Assert.Equal("symbolic-method-return", input.State);
                Assert.NotNull(input.SymbolicInput);
                Assert.Contains("get_ActiveIdentifier", input.SymbolicInput.MethodIdentity);
                Assert.NotEmpty(input.SymbolicInput.ValueGaps);
                Assert.Contains(input.ReturnSteps!, step => step.ProducerCallFactId == input.SymbolicInput.ProducerCallFactId
                    && step.ReturnFactId == input.SymbolicInput.ReturnFactId && step.CalleeBodyFactId == input.SymbolicInput.BodyFactId);
            }
            Assert.Equal(JsonSerializer.Serialize(unchanged.SymbolicInput), JsonSerializer.Serialize(rewritten.Composition.OperandBindings[1].SymbolicInput));
        }
        else Assert.All(rewritten.Composition.OperandBindings, operand => Assert.Equal("constant-string-hash", operand.Origin.Kind));
        Assert.All(new[] { text, middle, unchanged, rewritten }.Concat(rewritten.Composition.OperandBindings),
            operand => Assert.Empty(operand.Gaps));
        Assert.Equal(unchanged.Origin, rewritten.Composition.OperandBindings[1].Origin);
        var repeat = await CombinedDependencyPathReporter.BuildSelectedSymbolsAsync(options,
            [new(source.SourceIndexId, source.ScanId, source.CommitSha, entry.TargetSymbol!)], combinedIndex: true);
        Assert.Equal(JsonSerializer.Serialize(text), JsonSerializer.Serialize(repeat.Paths.Single().Nodes.Last().CommandBinding!.CommandTextFromPath));
        RetainedMethodGraph? graph = null;
        await CombinedDependencyPathReporter.BuildSelectedSymbolsAsync(new(combined, temp.Path, MaxDepth: 20, MaxFrontier: 10000)
            { CompiledOnly = true, ExactFromSymbol = true, MethodGraphObserver = value => graph = value },
            [new(source.SourceIndexId, source.ScanId, source.CommitSha, entry.TargetSymbol!)], combinedIndex: true);
        Assert.NotNull(graph);
        var trace = Assert.Single(graph.CommandTraces);
        Assert.Equal(tamper == "runtime" ? 3 : tamper == "getter" ? 4 : 2, trace.ProducerCallFactIds.Count);
        Assert.All(trace.ProducerCallFactIds, id => Assert.Contains(graph.Calls, call => call.FactId == id));
        await RetainedMethodGraphWriter.WriteAsync(graph, temp.Path, new string('a', 64), 16_000_000, default);
        var html = await File.ReadAllTextAsync(Path.Combine(temp.Path, RetainedMethodGraphWriter.HtmlName));
        Assert.Contains("Possible argument origins", html);
        if (tamper == "runtime") Assert.Contains("Symbolic method input", html);
        Assert.DoesNotContain("SELECT Value", html);
    }

    [Theory]
    [InlineData(true, "retained")]
    [InlineData(false, "retained")]
    [InlineData(true, "missing")]
    [InlineData(true, "malformed")]
    [InlineData(true, "virtual")]
    [InlineData(true, "operand-tampered")]
    [InlineData(true, "operand-state")]
    [InlineData(true, "operand-schema")]
    [InlineData(true, "operand-shape")]
    public async Task Property_profile_dynamic_lookup_is_distinct_from_literal_audit(bool compiledOnly, string dispatchFlags)
    {
        var repo = FindRepo();
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var bin = Path.Combine(repo, "samples", "fixture-build", "lazy-constructor", "bin", configuration, "net48");
        using var temp = new TempDirectory();
        var scan = ScanEngine.Scan(new ScanOptions(Path.Combine(repo, "samples", "messy-dotnet-workspace", "vb-lazy-constructor"),
            Path.Combine(temp.Path, "scan"), CompiledInputPaths: [Path.Combine(bin, "PublicLazy.Website.dll"),
                Path.Combine(bin, "PublicLazy.Framework.dll")], IlBodyEvidence: true));
        AssertScanIdentity(scan);
        var entry = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.ManagedMethodDeclared
            && fact.Properties.GetValueOrDefault("metadataName") == "Profile_Click");
        var index = Path.Combine(temp.Path, "index.sqlite");
        var combined = Path.Combine(temp.Path, "combined.sqlite");
        var facts = scan.Facts.ToList();
        var executor = Assert.Single(facts, fact => fact.FactType == FactTypes.ManagedMethodDeclared
            && fact.Properties.GetValueOrDefault("metadataName") == "ExecuteSql");
        Assert.True(int.TryParse(executor.Properties["methodDispatchFlags"], out var flags));
        Assert.Equal(0, flags & (int)System.Reflection.MethodAttributes.Virtual);
        if (dispatchFlags.StartsWith("operand-", StringComparison.Ordinal))
        {
            var call = Assert.Single(facts, fact => fact.FactType == FactTypes.ManagedIlCallObserved
                && fact.Properties.GetValueOrDefault("targetIdentity")?.Contains("member:8:GetEmail|", StringComparison.Ordinal) == true);
            var operand = Assert.Single(facts, fact => fact.FactType == FactTypes.ManagedIlCallValuesObserved
                && fact.Properties.GetValueOrDefault("ilBodyFactId") == call.Properties["ilBodyFactId"]
                && fact.Properties.GetValueOrDefault("ilOffset") == call.Properties["ilOffset"]);
            var properties = new Dictionary<string, string>(operand.Properties);
            if (dispatchFlags == "operand-tampered") properties["ilBoundedInputSha256"] = new string('f', 64);
            if (dispatchFlags == "operand-state") properties["valueState"] = "stack-unavailable";
            if (dispatchFlags == "operand-schema") properties["valueSchema"] = "unsupported";
            if (dispatchFlags == "operand-shape") properties["callShapeSupported"] = "false";
            facts[facts.IndexOf(operand)] = operand with { Properties = properties };
        }
        else if (dispatchFlags != "retained")
        {
            var properties = new Dictionary<string, string>(executor.Properties);
            if (dispatchFlags == "missing") properties.Remove("methodDispatchFlags");
            else properties["methodDispatchFlags"] = dispatchFlags == "virtual" ? "70" : "bad-flags";
            facts[facts.IndexOf(executor)] = executor with { Properties = properties };
        }
        SqliteIndexWriter.Write(index, scan.Manifest, facts);
        var composition = await CombinedIndexBuilder.CombineAsync(new CombineOptions([index], combined, ["synthetic-profile"]));
        var source = Assert.Single(composition.Sources);
        var report = await CombinedDependencyPathReporter.BuildSelectedSymbolsAsync(
            new CombinedDependencyPathOptions(combined, temp.Path, ToSurface: "database-api", MaxDepth: 20, MaxPaths: 256)
            { CompiledOnly = compiledOnly, ExactFromSymbol = true, MaxTraversalWork = 100_000 },
            [new(source.SourceIndexId, source.ScanId, source.CommitSha, entry.TargetSymbol!)], combinedIndex: true);
        bool Method(CombinedPathNode node, string name) => node.SymbolId?.Contains($"|method:{name.Length}:{name}|", StringComparison.Ordinal) == true;
        Assert.Equal(3, report.Paths.Count);
        var lookup = Assert.Single(report.Paths, path => path.Nodes.Any(node => Method(node, "GetEmail")));
        Assert.Equal("SqlCommand.ExecuteScalar", lookup.Nodes.Last().SurfaceName);
        var route = lookup.Nodes.ToList();
        var getter = route.FindIndex(node => Method(node, "get_EmployeeInfo"));
        var constructor = route.FindIndex(node => node.SymbolId?.Contains("names:15:ProfileEmployee|", StringComparison.Ordinal) == true
            && node.SymbolId.Contains("|constructor:5:.ctor|", StringComparison.Ordinal));
        Assert.True(getter >= 0 && constructor > getter);
        Assert.True(route.FindIndex(node => Method(node, "GetProfile")) > constructor);
        Assert.True(route.FindIndex(node => Method(node, "GetEmail")) > route.FindIndex(node => Method(node, "GetProfile")));
        var binding = lookup.Nodes.Last().CommandBinding!;
        Assert.Equal("1", binding.CommandTypeFromPath!.Origin.Identity);
        var text = binding.CommandTextFromPath!;
        Assert.Equal("callvirt", Assert.Single(text.Steps).Opcode);
        Assert.Equal(dispatchFlags is "missing" or "malformed" or "virtual", text.Gaps.Contains("IlCommandVirtualDispatchUnproven"));
        Assert.Equal("symbolic-string-composition", text.State);
        Assert.Equal("call-result", text.Origin.Kind);
        var producerBody = Assert.Single(scan.Facts, fact => $"{source.SourceIndexId}:{fact.FactId}" == text.OriginBodyFactId);
        var producer = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.ManagedIlCallObserved
            && fact.Properties.GetValueOrDefault("ilBodyFactId") == producerBody.FactId
            && fact.Properties.GetValueOrDefault("ilOffset") == text.Origin.Identity);
        Assert.Contains("Concat", producer.Properties["targetIdentity"], StringComparison.Ordinal);
        Assert.Contains("SymbolicStringValueNotMaterialized", text.Limitations!);
        Assert.DoesNotContain("IlCommandCompositionValueNotMaterialized", text.Gaps);
        Assert.DoesNotContain("IlCommandReturnTargetEdgeMissing", text.Gaps);
        Assert.Equal("System.String.Concat", text.Composition!.Operation);
        Assert.Equal(3, text.Composition.Operands.Count);
        Assert.Equal(3, text.Composition.OperandBindings.Count);
        Assert.Equal("method-local-constant", text.Composition.OperandBindings[0].State);
        Assert.NotNull(text.Composition.OperandBindings[1].OriginMethodIdentity);
        Assert.Null(text.Composition.OperandBindings[1].Composition);
        if (dispatchFlags.StartsWith("operand-", StringComparison.Ordinal))
        {
            Assert.Equal("unresolved-call-evidence", text.Composition.OperandBindings[1].State);
            Assert.Contains("IlCommandCallerOperandProvenanceUnavailable", text.Composition.OperandBindings[1].Gaps);
            var failure = Assert.Single(text.Composition.OperandBindings[1].OperandCheckFailures!);
            Assert.Equal(dispatchFlags switch { "operand-tampered" => "operand-body-input-match", "operand-shape" => "call-shape", _ => dispatchFlags }, Assert.Single(failure.FailedChecks));
            Assert.NotEmpty(failure.CallFactId);
            if (dispatchFlags == "operand-state") Assert.Equal("stack-unavailable", failure.OperandState);
            Assert.NotNull(failure.ExceptionRegionCount);
        }
        else if (compiledOnly)
        {
            Assert.Equal("constant-on-encoded-call-path", text.Composition.OperandBindings[1].State);
            Assert.Equal(3, text.Composition.OperandBindings[1].Steps.Count);
        }
        Assert.Equal($"{source.SourceIndexId}:{producer.FactId}", text.Composition.ProducerCallFactId);
        Assert.Equal(text.OriginBodyFactId, text.Composition.BodyFactId);
        var operandEvidence = Assert.Single(scan.Facts, fact => $"{source.SourceIndexId}:{fact.FactId}" == text.Composition.OperandFactId);
        Assert.Equal(operandEvidence.Properties["argumentOrigins"], JsonSerializer.Serialize(text.Composition.Operands));
        Assert.Empty(text.ReturnSteps ?? []);
        var audit = Assert.Single(report.Paths, path => path.Nodes.Any(node => Method(node, "WriteAudit")));
        Assert.Equal("4", audit.Nodes.Last().CommandBinding!.CommandTypeFromPath!.Origin.Identity);
        Assert.Equal("method-local-constant", audit.Nodes.Last().CommandBinding!.CommandTextFromPath!.State);
        Assert.DoesNotContain(lookup.Nodes, node => Method(node, "WriteAudit"));
        Assert.Single(report.Paths, path => path.Nodes.Last().SurfaceName == "DbDataAdapter.Fill");
        Assert.DoesNotContain(report.Gaps, gap => gap.GapKind == "TruncatedByLimit");
        Assert.DoesNotContain("SELECT Email", JsonSerializer.Serialize(report), StringComparison.Ordinal);
        if (!compiledOnly)
        {
            RetainedMethodGraph? graph = null;
            var graphOptions = new CombinedDependencyPathOptions(combined, temp.Path, MaxDepth: 20, MaxFrontier: 10000)
            { ExactFromSymbol = true, MethodGraphObserver = value => graph = value };
            var graphReport = await CombinedDependencyPathReporter.BuildSelectedSymbolsAsync(graphOptions,
                [new(source.SourceIndexId, source.ScanId, source.CommitSha, entry.TargetSymbol!)], true);
            Assert.NotNull(graph);
            Assert.Empty(graphReport.Paths);
            Assert.Null(graphReport.Query.ToSurface);
            Assert.Contains(graph.Calls, call => call.EncodedTarget?.Contains("Concat", StringComparison.Ordinal) == true
                && call.State == "no-admitted-method-target-edge" && call.Offset is not null);
            Assert.Contains(graph.Nodes, node => Method(node, "GetEmail"));
            Assert.Contains(graph.Nodes, node => Method(node, "WriteAudit"));
            var unresolvedTrace = Assert.Single(graph.CommandTraces, trace =>
                trace.CommandText?.Composition is not null);
            Assert.Equal("call-result", unresolvedTrace.CommandText!.Origin.Kind);
            var producerId = Assert.Single(unresolvedTrace.ProducerCallFactIds);
            Assert.Equal($"{source.SourceIndexId}:{producer.FactId}", producerId);
            Assert.Contains(graph.Calls, call => call.FactId == producerId && call.EncodedTarget!.Contains("Concat", StringComparison.Ordinal));
            Assert.Equal(graph.Nodes.Count, graph.Nodes.Select(node => node.NodeId).Distinct().Count());
            Assert.All(graph.Edges, edge =>
            {
                Assert.Contains(graph.Nodes, node => node.NodeId == edge.FromNodeId);
                Assert.Contains(graph.Nodes, node => node.NodeId == edge.ToNodeId);
            });
            var output = Path.Combine(temp.Path, "graph");
            await RetainedMethodGraphWriter.WriteAsync(graph, output, new string('a', 64), 16_000_000, default);
            var json = await File.ReadAllTextAsync(Path.Combine(output, RetainedMethodGraphWriter.JsonName));
            Assert.DoesNotContain("SELECT Email", json, StringComparison.Ordinal);
            using var document = JsonDocument.Parse(json);
            Assert.Equal(64, document.RootElement.GetProperty("generatorSha256").GetString()!.Length);
            Assert.Equal(64, document.RootElement.GetProperty("boundedInputSha256").GetString()!.Length);
            var html = await File.ReadAllTextAsync(Path.Combine(output, RetainedMethodGraphWriter.HtmlName));
            Assert.Contains("Call graph tree", html);
            Assert.Contains("Supported symbolic expression", html);
            Assert.Contains("IL slot 1", html);
            Assert.Contains("Command text routes", html);
            Assert.DoesNotContain("SELECT Email", html, StringComparison.Ordinal);
            Assert.Contains("UNRESOLVED METHOD TARGET", html);
            await RetainedMethodGraphWriter.WriteAsync(graph, output, new string('a', 64), 16_000_000, default);
            Assert.Equal(json, await File.ReadAllTextAsync(Path.Combine(output, RetainedMethodGraphWriter.JsonName)));
            await Assert.ThrowsAsync<InvalidDataException>(() => RetainedMethodGraphWriter.WriteAsync(graph,
                Path.Combine(temp.Path, "too-small"), new string('a', 64), 1, default));
            Assert.False(Directory.Exists(Path.Combine(temp.Path, "too-small")));
            RetainedMethodGraph? bounded = null;
            await CombinedDependencyPathReporter.BuildSelectedSymbolsAsync(graphOptions with
                { MaxDepth = 1, MethodGraphObserver = value => bounded = value },
                [new(source.SourceIndexId, source.ScanId, source.CommitSha, entry.TargetSymbol!)], true);
            Assert.NotNull(bounded);
            Assert.Contains(bounded.Cutoffs, cutoff => cutoff.StartsWith("depth-limit:", StringComparison.Ordinal));
            Assert.True(bounded.Nodes.Count < graph.Nodes.Count);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Deep_projectless_property_constructor_logging_retains_routes_and_return_value_gap(bool compiledOnly)
    {
        var repo = FindRepo();
        var source = Path.Combine(repo, "samples", "messy-dotnet-workspace", "vb-lazy-constructor");
        Assert.Empty(Directory.GetFiles(source, "*.*proj", SearchOption.AllDirectories));
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var bin = Path.Combine(repo, "samples", "fixture-build", "lazy-constructor", "bin", configuration, "net48");
        var assemblies = new[] { "PublicLazy.Website.dll", "PublicLazy.Framework.dll" }.Select(name => Path.Combine(bin, name)).ToArray();
        using (var provider = Mono.Cecil.AssemblyDefinition.ReadAssembly(assemblies[1]))
        {
            var logger = Assert.Single(provider.MainModule.Types, type => type.FullName == "PublicLazy.Framework.PublicLog");
            var literal = Assert.Single(logger.Methods, method => method.Name == "LiteralText");
            var direct = Assert.Single(logger.Methods, method => method.Name == "InsertLiteral");
            var returnedString = Assert.Single(literal.Body.Instructions,
                instruction => instruction.OpCode.Code == Mono.Cecil.Cil.Code.Ldstr).Operand;
            Assert.Equal(Assert.Single(direct.Body.Instructions,
                instruction => instruction.OpCode.Code == Mono.Cecil.Cil.Code.Ldstr).Operand, returnedString);
            Assert.Contains(literal.Body.Instructions, instruction => instruction.OpCode.Code == Mono.Cecil.Cil.Code.Ret);
            Assert.All(literal.Body.Instructions, instruction => Assert.Contains(instruction.OpCode.Code,
                new[] { Mono.Cecil.Cil.Code.Nop, Mono.Cecil.Cil.Code.Ldstr, Mono.Cecil.Cil.Code.Stloc_0,
                    Mono.Cecil.Cil.Code.Ldloc_0, Mono.Cecil.Cil.Code.Br_S, Mono.Cecil.Cil.Code.Br, Mono.Cecil.Cil.Code.Ret }));
        }
        using var temp = new TempDirectory();
        var scan = ScanEngine.Scan(new ScanOptions(source, Path.Combine(temp.Path, "scan"),
            CompiledInputPaths: assemblies, IlBodyEvidence: true));
        AssertScanIdentity(scan);
        var entry = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.ManagedMethodDeclared
            && fact.TargetSymbol!.Contains("LazyOverview|", StringComparison.Ordinal)
            && fact.TargetSymbol.Contains("|method:10:Load_Click|", StringComparison.Ordinal));
        var index = Path.Combine(temp.Path, "index.sqlite");
        var combined = Path.Combine(temp.Path, "combined.sqlite");
        SqliteIndexWriter.Write(index, scan.Manifest, scan.Facts);
        var composition = await CombinedIndexBuilder.CombineAsync(new CombineOptions([index], combined, ["public-lazy-corpus"]));
        var origin = Assert.Single(composition.Sources);
        var selector = new CombinedPathSymbolRoot(origin.SourceIndexId, origin.ScanId, origin.CommitSha, entry.TargetSymbol!);
        async Task<CombinedDependencyPathReport> Query(string? surface) =>
            await CombinedDependencyPathReporter.BuildSelectedSymbolsAsync(
                new CombinedDependencyPathOptions(combined, temp.Path, ToSurface: "database-api", SurfaceName: surface,
                    MaxDepth: 20, MaxPaths: 256)
                { CompiledOnly = compiledOnly, ExactFromSymbol = true, MaxTraversalWork = 100_000 },
                [selector], combinedIndex: true);

        var broad = await Query(null);
        var scalar = broad.Paths.Where(path => path.Nodes.Last().SurfaceName == "SqlCommand.ExecuteScalar").ToArray();
        Assert.Equal(6, broad.Paths.Count);
        Assert.Equal(5, scalar.Length);
        bool Type(CombinedPathNode node, string name) => node.SymbolId?.Contains($"names:{name.Length}:{name}|", StringComparison.Ordinal) == true;
        bool Method(CombinedPathNode node, string name) => node.SymbolId?.Contains($"|method:{name.Length}:{name}|", StringComparison.Ordinal) == true;
        bool Constructor(CombinedPathNode node, string name) => Type(node, name)
            && node.SymbolId!.Contains("|constructor:5:.ctor|", StringComparison.Ordinal);
        var logging = scalar.Where(path => path.Nodes.Any(node => Method(node, "InsertLog"))).ToArray();
        Assert.Equal(3, logging.Length);
        Console.WriteLine($"propertyCorpus.compiledOnly={compiledOnly};paths={broad.Paths.Count};scalar={scalar.Length};logging={logging.Length};work={broad.Summary.TraversalWorkUnits}");
        Assert.All(logging, path =>
        {
            Assert.Contains(path.Nodes, node => Constructor(node, "SyntheticChoices"));
            Assert.Contains(path.Nodes, node => Method(node, "get_EmployeeInfo"));
            Assert.Contains(path.Nodes, node => Constructor(node, "SyntheticEmployee"));
            Assert.DoesNotContain(path.Nodes, node => Type(node, "UnrelatedLog"));
            Assert.DoesNotContain(path.Nodes, node => Method(node, "BuildText"));
            var route = path.Nodes.ToList();
            Assert.True(route.FindIndex(node => Constructor(node, "SyntheticChoices")) < route.FindIndex(node => Method(node, "get_EmployeeInfo")));
            Assert.True(route.FindIndex(node => Method(node, "get_EmployeeInfo")) < route.FindIndex(node => Constructor(node, "SyntheticEmployee")));
            Assert.True(route.FindIndex(node => Constructor(node, "SyntheticEmployee")) < route.FindIndex(node => Method(node, "InsertLog")));
            var binding = Assert.IsType<CompiledCommandConfigurationCandidate>(path.Nodes.Last().CommandBinding);
            Assert.Equal("argument-slot", binding.CommandTextOrigin.Kind);
            var text = Assert.IsType<CompiledCommandPathValueBinding>(binding.CommandTextFromPath);
            Assert.Equal("symbolic-string-composition", text.State);
            Assert.Equal("call-result", text.Origin.Kind);
            var producerBody = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.ManagedIlBodyDeclared
                && $"{origin.SourceIndexId}:{fact.FactId}" == text.OriginBodyFactId);
            var producerCall = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.ManagedIlCallObserved
                && fact.Properties.GetValueOrDefault("ilBodyFactId") == producerBody.FactId
                && fact.Properties.GetValueOrDefault("ilOffset") == text.Origin.Identity);
            Assert.Contains("member:6:Concat|", producerCall.Properties["targetIdentity"], StringComparison.Ordinal);
            Assert.Single(text.Steps);
            Assert.Single(text.ReturnSteps!);
            Assert.Equal("System.String.Concat", text.Composition!.Operation);
            var dynamicOperand = text.Composition.OperandBindings[1];
            Assert.Equal("unresolved-operand", dynamicOperand.State);
            Assert.Contains("IlCommandOperandValueUnresolved", dynamicOperand.Gaps);
            Assert.Contains("IlCommandReturnTargetEdgeMissing", dynamicOperand.Gaps);
            Assert.Contains("IlCommandVirtualDispatchUnproven", text.Gaps);
            Assert.Equal("method-local-constant", binding.CommandTypeFromPath!.State);
            Assert.Equal("1", binding.CommandTypeFromPath.Origin.Identity);
            Assert.Matches("^[0-9a-f]{64}$", text.GeneratorSha256);
            Assert.Matches("^[0-9a-f]{64}$", text.BoundedInputSha256);
        });
        Assert.Contains(logging, path => path.Nodes.Any(node => Constructor(node, "SyntheticPreferences")));
        Assert.Contains(logging, path => !path.Nodes.Any(node => Constructor(node, "SyntheticPreferences")));
        Assert.Contains(scalar, path => path.Nodes.Any(node => Method(node, "InsertLiteral"))
            && path.Nodes.Last().CommandBinding?.CommandTextFromPath?.State == "constant-on-encoded-call-path");
        var literalMethod = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.ManagedMethodDeclared
            && fact.TargetSymbol!.Contains("|method:11:LiteralText|", StringComparison.Ordinal));
        var literalBody = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.ManagedIlBodyDeclared
            && fact.Properties.GetValueOrDefault("compiledFactId") == literalMethod.FactId);
        var literalReturns = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.ManagedIlReturnValuesObserved
            && fact.Properties.GetValueOrDefault("ilBodyFactId") == literalBody.FactId);
        Assert.Equal("return-operands-candidate", literalReturns.Properties["valueState"]);
        var retainedReturn = Assert.Single(JsonSerializer.Deserialize<IlReturnValueObservation[]>(literalReturns.Properties["returnOrigins"])!);
        var directText = Assert.Single(scalar, path => path.Nodes.Any(node => Method(node, "InsertLiteral")))
            .Nodes.Last().CommandBinding!.CommandTextFromPath!.Origin;
        Assert.Equal(directText.Kind, retainedReturn.Origin.Kind);
        Assert.Equal(directText.Identity, retainedReturn.Origin.Identity);
        // Unlike BuildText(message), this producer returns exactly the same
        // constant as the direct control. Its return evidence is separate
        // from call-stack edges and does not prove virtual endpoint dispatch.
        var returnedLiteral = Assert.Single(scalar, path => path.Nodes.Any(node => Method(node, "InsertReturnedLiteral")));
        var returnedText = returnedLiteral.Nodes.Last().CommandBinding!.CommandTextFromPath!;
        Assert.Equal("constant-on-encoded-call-path", returnedText.State);
        Assert.Equal(directText, returnedText.Origin);
        var returnStep = Assert.Single(returnedText.ReturnSteps!);
        Assert.Equal($"{origin.SourceIndexId}:{literalReturns.FactId}", returnStep.ReturnFactId);
        Assert.Equal($"{origin.SourceIndexId}:{literalBody.FactId}", returnedText.OriginBodyFactId);
        var returnedCall = Assert.Single(scan.Facts, fact => $"{origin.SourceIndexId}:{fact.FactId}" == returnStep.ProducerCallFactId);
        Assert.Contains("|method:11:LiteralText|", returnedCall.Properties["targetIdentity"], StringComparison.Ordinal);
        Assert.Contains("IlCommandVirtualDispatchUnproven", returnedText.Gaps);
        Assert.DoesNotContain(returnedLiteral.Nodes, node => Method(node, "LiteralText"));
        var fill = await Query("DbDataAdapter.Fill");
        Assert.Single(fill.Paths);
        Assert.All(fill.Paths, path =>
        {
            Assert.Equal("DbDataAdapter.Fill", path.Nodes.Last().SurfaceName);
            Assert.DoesNotContain(path.Nodes, node => Method(node, "InsertLog"));
            Assert.Equal("method-local-constant", path.Nodes.Last().CommandBinding!.CommandTextFromPath!.State);
        });
        Assert.DoesNotContain("SELECT LEN('", JsonSerializer.Serialize(broad), StringComparison.Ordinal);
        Assert.DoesNotContain(broad.Gaps, gap => gap.GapKind == "TruncatedByLimit" && gap.Reason is "path" or "work");
        Assert.InRange(broad.Summary.TraversalWorkUnits!.Value, 1, 100_000);
    }

    [Theory]
    [InlineData("InsertReturnedLiteral", "none", null)]
    [InlineData("InsertForwardedLiteral", "none", null)]
    [InlineData("InsertReturnedLiteralTwoHops", "none", null)]
    [InlineData("InsertComposedTextTwoHops", "none", "IlCommandCompositionValueNotMaterialized")]
    [InlineData("InsertComposedTextTwoHops", "concat-provenance", "IlCommandCompositionProvenanceUnavailable")]
    [InlineData("InsertComposedTextTwoHops", "concat-malformed", "IlCommandCompositionOperandsUnavailable")]
    [InlineData("InsertComposedTextTwoHops", "concat-byref", "IlCommandCompositionProvenanceUnavailable")]
    [InlineData("InsertComposedTextTwoHops", "concat-scope", "IlCommandReturnTargetEdgeMissing")]
    [InlineData("InsertComposedTextTwoHops", "concat-virtual", "IlCommandReturnTargetEdgeMissing")]
    [InlineData("InsertComposedTextTwoHops", "concat-signature", "IlCommandReturnTargetEdgeMissing")]
    [InlineData("InsertReturnedLiteral", "target-missing", "IlCommandReturnTargetEdgeMissing")]
    [InlineData("InsertReturnedLiteral", "target-ambiguous", "IlCommandReturnTargetEdgeMissing")]
    [InlineData("InsertRecursiveText", "none", "IlCommandReturnCycle")]
    [InlineData("InsertVirtualText", "none", "IlCommandReturnVirtualDispatchUnproven")]
    [InlineData("InsertReturnedLiteral", "missing", "IlCommandReturnEvidenceMissingOrAmbiguous")]
    [InlineData("InsertReturnedLiteral", "ambiguous", "IlCommandReturnEvidenceMissingOrAmbiguous")]
    [InlineData("InsertReturnedLiteral", "changed", "IlCommandReturnProvenanceUnavailable")]
    [InlineData("InsertReturnedLiteral", "malformed", "IlCommandReturnEvidenceMalformed")]
    [InlineData("InsertReturnedLiteral", "count", "IlCommandReturnEvidenceUnavailable")]
    [InlineData("InsertReturnedLiteral", "conflict", "IlCommandReturnValuesDisagree")]
    [InlineData("InsertReturnedLiteral", "work", "IlCommandReturnWorkLimit")]
    public async Task Deep_projectless_return_value_projection_requires_exact_bounded_evidence(string entryName, string mutation, string? gap)
    {
        var repo = FindRepo();
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var bin = Path.Combine(repo, "samples", "fixture-build", "lazy-constructor", "bin", configuration, "net48");
        using var temp = new TempDirectory();
        var scan = ScanEngine.Scan(new ScanOptions(Path.Combine(repo, "samples", "messy-dotnet-workspace", "vb-lazy-constructor"),
            Path.Combine(temp.Path, "scan"), CompiledInputPaths: [Path.Combine(bin, "PublicLazy.Framework.dll")], IlBodyEvidence: true));
        AssertScanIdentity(scan);
        var entry = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.ManagedMethodDeclared
            && fact.Properties.GetValueOrDefault("metadataName") == entryName);
        var literal = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.ManagedMethodDeclared
            && fact.Properties.GetValueOrDefault("metadataName") == "LiteralText");
        var body = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.ManagedIlBodyDeclared
            && fact.Properties.GetValueOrDefault("compiledFactId") == literal.FactId);
        var summary = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.ManagedIlReturnValuesObserved
            && fact.Properties.GetValueOrDefault("ilBodyFactId") == body.FactId);
        var facts = scan.Facts.ToList();
        if (mutation.StartsWith("concat-", StringComparison.Ordinal))
        {
            var entryBody = Assert.Single(facts, fact => fact.FactType == FactTypes.ManagedIlBodyDeclared
                && fact.Properties.GetValueOrDefault("compiledFactId") == entry.FactId);
            var concat = Assert.Single(facts, fact => fact.FactType == FactTypes.ManagedIlCallObserved
                && fact.Properties.GetValueOrDefault("ilBodyFactId") == entryBody.FactId
                && fact.Properties.GetValueOrDefault("targetIdentity")?.Contains("member:6:Concat|", StringComparison.Ordinal) == true);
            var operand = Assert.Single(facts, fact => fact.FactType == FactTypes.ManagedIlCallValuesObserved
                && fact.Properties.GetValueOrDefault("ilBodyFactId") == entryBody.FactId
                && fact.Properties.GetValueOrDefault("ilOffset") == concat.Properties["ilOffset"]);
            var target = mutation is "concat-scope" or "concat-virtual" or "concat-signature" ? concat : operand;
            var properties = new Dictionary<string, string>(target.Properties);
            if (mutation == "concat-provenance") properties["ilBoundedInputSha256"] = new string('f', 64);
            if (mutation == "concat-malformed") properties["argumentOrigins"] = "[";
            if (mutation == "concat-byref") properties["callByReferenceParameters"] = "11";
            if (mutation == "concat-scope") properties["targetIdentity"] = properties["targetIdentity"].Replace("mscorlib", "fakecore", StringComparison.Ordinal);
            if (mutation == "concat-signature") properties["targetIdentity"] = properties["targetIdentity"].Replace("names:6:String", "names:6:Object", StringComparison.Ordinal);
            if (mutation == "concat-virtual") properties["opcode"] = "callvirt";
            facts[facts.IndexOf(target)] = target with { Properties = properties };
        }
        else if (mutation == "target-missing") facts.Remove(literal);
        else if (mutation == "target-ambiguous") facts.Add(literal with { FactId = literal.FactId + "-competitor" });
        else if (mutation == "missing") facts.RemoveAll(fact => fact.FactType == FactTypes.ManagedIlReturnValuesObserved);
        else if (mutation == "ambiguous") facts.Add(summary with { FactId = summary.FactId + "-competitor" });
        else if (mutation != "none")
        {
            var properties = new Dictionary<string, string>(summary.Properties);
            if (mutation == "changed") properties["ilBoundedInputSha256"] = new string('f', 64);
            if (mutation == "malformed") properties["returnOrigins"] = "[";
            if (mutation == "count") properties["returnCount"] = "257";
            if (mutation == "conflict")
            {
                var value = Assert.Single(JsonSerializer.Deserialize<IlReturnValueObservation[]>(properties["returnOrigins"])!);
                properties["returnOrigins"] = JsonSerializer.Serialize(new[] { value, value with { Offset = value.Offset + 1,
                    Origin = new("constant-string-hash", "str:1:" + new string('a', 64)) } });
                properties["returnCount"] = "2";
            }
            if (mutation == "work")
            {
                var value = Assert.Single(JsonSerializer.Deserialize<IlReturnValueObservation[]>(properties["returnOrigins"])!);
                properties["returnOrigins"] = JsonSerializer.Serialize(Enumerable.Range(0, 64)
                    .Select(offset => value with { Offset = offset }).ToArray());
                properties["returnCount"] = "64";
            }
            facts[facts.IndexOf(summary)] = summary with { Properties = properties };
        }
        var index = Path.Combine(temp.Path, "index.sqlite");
        var combined = Path.Combine(temp.Path, "combined.sqlite");
        SqliteIndexWriter.Write(index, scan.Manifest, facts);
        var composition = await CombinedIndexBuilder.CombineAsync(new CombineOptions([index], combined, ["return-fixture"]));
        var source = Assert.Single(composition.Sources);
        var report = await CombinedDependencyPathReporter.BuildSelectedSymbolsAsync(
            new CombinedDependencyPathOptions(combined, temp.Path, ToSurface: "database-api", SurfaceName: "SqlCommand.ExecuteScalar", MaxDepth: 20)
            { CompiledOnly = true, ExactFromSymbol = true, MaxTraversalWork = 10_000 },
            [new(source.SourceIndexId, source.ScanId, source.CommitSha, entry.TargetSymbol!)], combinedIndex: true);
        var text = Assert.Single(report.Paths).Nodes.Last().CommandBinding!.CommandTextFromPath!;
        // Duplicate declarations are refused by graph admission before return
        // projection, so no candidate call edge is admitted to the resolver.
        if (mutation == "target-ambiguous") Assert.Contains(report.Gaps, item => item.GapKind == "CompiledIlTargetAmbiguous");
        if (entryName.EndsWith("TwoHops", StringComparison.Ordinal)) Assert.Equal(2, text.Steps.Count);
        var memoryReport = await CombinedDependencyPathReporter.BuildReportAsync(
            new CombinedDependencyPathOptions(combined, temp.Path, FromSymbol: entry.TargetSymbol,
                ToSurface: "database-api", SurfaceName: "SqlCommand.ExecuteScalar", MaxDepth: 20)
            { CompiledOnly = true, ExactFromSymbol = true, MaxTraversalWork = 10_000 });
        var memoryText = Assert.Single(memoryReport.Paths).Nodes.Last().CommandBinding!.CommandTextFromPath!;
        Assert.Equal(JsonSerializer.Serialize(text), JsonSerializer.Serialize(memoryText));
        if (gap is null)
        {
            Assert.Equal("constant-on-encoded-call-path", text.State);
            Assert.Equal("constant-string-hash", text.Origin.Kind);
            Assert.Equal(entryName == "InsertForwardedLiteral" ? 2 : 1, text.ReturnSteps!.Count);
        }
        else if (mutation == "conflict")
        {
            Assert.Equal("symbolic-method-return", text.State);
            Assert.Contains(gap!, text.SymbolicInput!.ValueGaps);
            Assert.DoesNotContain(gap!, text.Gaps);
            Assert.Equal("call-result", text.Origin.Kind);
        }
        else
        {
            Assert.Null(text.SymbolicInput);
            Assert.Equal(entryName == "InsertComposedTextTwoHops" && mutation == "none" ? "symbolic-string-composition" : "unresolved-operand", text.State);
            if (gap == "IlCommandCompositionValueNotMaterialized")
                Assert.Contains("SymbolicStringValueNotMaterialized", text.Limitations!);
            else Assert.Contains(gap, text.Gaps);
            if (entryName == "InsertComposedTextTwoHops" && mutation == "none")
            {
                Assert.Equal(2, text.Composition!.Operands.Count);
                var argument = text.Composition.OperandBindings[1];
                Assert.Equal("unresolved-root-argument", argument.State);
                Assert.Equal("1", argument.Origin.Identity);
                Assert.Contains("InsertComposedTextTwoHops", argument.OriginMethodIdentity!);
                Assert.Contains("IlCommandRootArgumentUnresolved", argument.Gaps);
            }
            if (mutation.StartsWith("concat-", StringComparison.Ordinal)) Assert.Null(text.Composition);
            if (mutation.StartsWith("target-", StringComparison.Ordinal))
            {
                Assert.Equal("call-result", text.Origin.Kind);
                Assert.Empty(text.ReturnSteps ?? []);
                Assert.Contains("IlCommandReturnTargetMissingOrAmbiguous", text.Gaps);
                Assert.Contains("IlCommandOperandValueUnresolved", text.Gaps);
            }
        }
        Assert.DoesNotContain("SELECT ", JsonSerializer.Serialize(report), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("InsertGetterLiteral", "constant-on-encoded-call-path")]
    [InlineData("InsertReturnFrameLimit", "limit")]
    [InlineData("InsertGetterDynamic", "symbolic-method-return")]
    [InlineData("InsertGetterSharedDynamic", "symbolic-method-return")]
    [InlineData("InsertReturnedConcat", "symbolic-string-composition")]
    [InlineData("InsertReturnedChoice", "symbolic-argument-alternatives")]
    [InlineData("InsertReturnedConcatArgument", "symbolic-string-composition")]
    public async Task Getter_return_batch_keeps_invocation_scope_and_explicit_unknown_boundaries(string entryName, string expected)
    {
        var repo = FindRepo();
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var binary = Path.Combine(repo, "samples", "fixture-build", "lazy-constructor", "bin", configuration, "net48", "PublicLazy.Framework.dll");
        using var temp = new TempDirectory();
        var scan = ScanEngine.Scan(new ScanOptions(Path.Combine(repo, "samples", "messy-dotnet-workspace", "vb-lazy-constructor"),
            Path.Combine(temp.Path, "scan"), CompiledInputPaths: [binary], IlBodyEvidence: true));
        AssertScanIdentity(scan);
        var entry = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.ManagedMethodDeclared
            && fact.Properties.GetValueOrDefault("metadataName") == entryName);
        var index = Path.Combine(temp.Path, "index.sqlite");
        var combined = Path.Combine(temp.Path, "combined.sqlite");
        SqliteIndexWriter.Write(index, scan.Manifest, scan.Facts);
        var result = await CombinedIndexBuilder.CombineAsync(new CombineOptions([index], combined, ["getter-batch"]));
        var source = Assert.Single(result.Sources);
        var options = new CombinedDependencyPathOptions(combined, temp.Path, ToSurface: "database-api", SurfaceName: "SqlCommand.ExecuteScalar", MaxDepth: 20)
            { CompiledOnly = true, ExactFromSymbol = true, MaxTraversalWork = 10_000 };
        async Task<CompiledCommandPathValueBinding> Query()
        {
            var report = await CombinedDependencyPathReporter.BuildSelectedSymbolsAsync(options,
                [new(source.SourceIndexId, source.ScanId, source.CommitSha, entry.TargetSymbol!)], combinedIndex: true);
            return Assert.Single(report.Paths).Nodes.Last().CommandBinding!.CommandTextFromPath!;
        }
        var text = await Query();
        Assert.Equal(expected, text.State);
        Assert.Equal(JsonSerializer.Serialize(text), JsonSerializer.Serialize(await Query()));
        var values = new List<CompiledCommandPathValueBinding>();
        void Walk(CompiledCommandPathValueBinding value)
        {
            values.Add(value);
            Assert.InRange(values.Count, 1, 32);
            foreach (var child in (value.Alternatives ?? []).Concat(value.Composition?.OperandBindings ?? [])) Walk(child);
        }
        Walk(text);
        if (entryName == "InsertReturnFrameLimit")
        {
            Assert.Contains("IlCommandReturnFrameLimit", text.Gaps);
            Assert.InRange(text.ReturnSteps!.Count, 1, 17);
        }
        else if (entryName.Contains("Dynamic", StringComparison.Ordinal))
        {
            Assert.Contains("IlCommandReturnOriginUnknown", text.SymbolicInput!.ValueGaps);
            Assert.DoesNotContain("IlCommandOperandValueUnresolved", text.Gaps);
            Assert.DoesNotContain("IlCommandReturnOriginUnknown", text.Gaps);
            Assert.Contains("MethodReturnValueNotEvaluated", text.Limitations!);
            Assert.Equal("call-result", text.Origin.Kind);
            Assert.Contains(entryName.Contains("Shared", StringComparison.Ordinal) ? "get_SharedDynamic" : "get_DynamicInner", text.SymbolicInput.MethodIdentity);
            Assert.Equal(entryName.Contains("Shared", StringComparison.Ordinal) ? 1 : 2, text.ReturnSteps!.Count);
        }
        else if (entryName.EndsWith("Argument", StringComparison.Ordinal))
        {
            var operand = text.Composition!.OperandBindings[1];
            Assert.Equal("unresolved-root-argument", operand.State);
            Assert.Equal("1", operand.Origin.Identity);
            Assert.Contains(entryName, operand.OriginMethodIdentity!);
            Assert.Contains("IlCommandRootArgumentUnresolved", operand.Gaps);
        }
        else
        {
            Assert.DoesNotContain(values.SelectMany(value => value.Gaps), gap => gap != "IlCommandVirtualDispatchUnproven");
            Assert.All(values.Where(value => value.Composition is null && value.Alternatives is null),
                value => Assert.Equal("constant-string-hash", value.Origin.Kind));
            if (entryName == "InsertReturnedChoice") Assert.Equal(2, text.Alternatives!.Count);
        }
        Assert.DoesNotContain("public-prefix:", JsonSerializer.Serialize(text));
        Assert.DoesNotContain("SELECT ", JsonSerializer.Serialize(text));
    }

    private static void AssertScanIdentity(ScanResult scan)
    {
        var sha = scan.Manifest.CommitSha;
        Assert.True(sha.Length == 40 && sha.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f'),
            $"LAZY_CORPUS_GIT_IDENTITY_UNAVAILABLE;commitLength={sha.Length};knownGapCount={scan.Manifest.KnownGaps.Count};" +
            "selected-root reporting requires a real Git commit; inspect the bounded Git probe/environment, not SQL route resolution.");
        Assert.False(string.IsNullOrWhiteSpace(scan.Manifest.ScanId));
    }

    private static string FindRepo()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))) return directory.FullName;
        throw new InvalidOperationException("Repository unavailable.");
    }
}
