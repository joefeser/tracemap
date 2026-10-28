using Mono.Cecil;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class IlSqlLifecycleLegacyBaseTests
{
    private const string Legacy = "memberref|type:scope(assembly:name:8:mscorlib|version:7:2.0.0.0|culture:7:neutral|publicKeyToken:16:b77a5c561934e089)type(namespace:6:System|names:6:Object)|member:5:.ctor|arity:0|call:default|hasThis:true|explicitThis:false|()->type(namespace:6:System|names:4:Void)";

    [Fact]
    public void Legacy_base_call_requires_the_entire_exact_framework_signature()
    {
        Assert.Equal("object-base", IlSqlLifecycleApis.Resolve(Legacy, []));
        foreach (var forged in new[] {
            Legacy.Replace("b77a5c561934e089", "0000000000000000", StringComparison.Ordinal),
            Legacy.Replace("2.0.0.0", "3.0.0.0", StringComparison.Ordinal),
            Legacy.Replace("mscorlib", "fakecore", StringComparison.Ordinal),
            Legacy.Replace("neutral", "en-USxx", StringComparison.Ordinal),
            Legacy.Replace("System", "Custom", StringComparison.Ordinal),
            Legacy.Replace("Object", "Widget", StringComparison.Ordinal),
            Legacy.Replace("hasThis:true", "hasThis:false", StringComparison.Ordinal),
            Legacy.Replace("arity:0", "arity:1", StringComparison.Ordinal),
            Legacy.Replace("|()->", "|(type(namespace:6:System|names:5:Int32))->", StringComparison.Ordinal) })
            Assert.Null(IlSqlLifecycleApis.Resolve(forged, []));
        // The additional identity belongs only to the lifecycle protocol.
        Assert.Null(IlSqlObjectPathExtractor.KnownFrameworkApi(Legacy));
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Compiled_legacy_base_call_continues_into_parameter_fields_and_execution_specific_attachment(string language)
    {
        var baseline = IlSqlLifecycleExtractorTests.Bodies(language);
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))) directory = directory.Parent;
        Assert.NotNull(directory);
        var config = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var path = Path.Combine(directory.FullName, "samples", "sql-compiled-wrapper-evidence", language,
            "wrapper", "bin", config, "net48", $"PublicSql.{language}.wrapper.dll");
        byte[] bytes;
        // Rewrite only an independently authored public fixture's encoded base call,
        // in memory. No application/provider execution or private input is involved.
        using (var module = ModuleDefinition.ReadModule(new MemoryStream(File.ReadAllBytes(path)), new ReaderParameters { InMemory = true }))
        {
            var type = Assert.Single(ManagedMetadataExtractor.FlattenTypes(module.Types), type => type.Name == "InputParameter");
            var ctor = Assert.Single(type.Methods, method => method.IsConstructor);
            var instruction = Assert.Single(ctor.Body.Instructions, instruction => instruction.Operand is MethodReference method
                && method.Name == ".ctor" && method.DeclaringType.FullName == "System.Object");
            var legacy = new AssemblyNameReference("mscorlib", new Version(2, 0, 0, 0))
                { PublicKeyToken = Convert.FromHexString("b77a5c561934e089") };
            module.AssemblyReferences.Add(legacy);
            instruction.Operand = new MethodReference(".ctor", module.TypeSystem.Void,
                new TypeReference("System", "Object", module, legacy)) { HasThis = true };
            using var output = new MemoryStream(); module.Write(output); bytes = output.ToArray();
        }
        var left = IlBodyEvidenceExtractor.ReadCecilBodies(bytes, new(), new(400_000), CancellationToken.None);
        var right = IlBodyEvidenceExtractor.ReadSystemReflectionMetadataBodies(bytes, new(), new(400_000), CancellationToken.None);
        Assert.Empty(IlBodyEvidenceExtractor.CompareBodies(left, right));
        var agreed = IlBodyEvidenceExtractor.AgreeValueFlows(left.Bodies, right.Bodies);
        Assert.Contains(agreed.SelectMany(body => body.Calls), call => call.TargetIdentity == Legacy);
        var input = Assert.Single(baseline.Select(body => body.Input).Distinct(), input => input.Role == "wrapper");
        var hash = SqlServerSchemaEvidence.Hash(bytes);
        var changedInput = input with { RawFileSha256 = hash, PrivacyProjectedInputSha256 = hash };
        var bodies = baseline.Where(body => body.Input.Role != "wrapper")
            .Concat(agreed.Select(body => new IlSqlObjectPathExtractor.Body(body, changedInput))).ToArray();
        var root = Assert.Single(bodies, body => body.Observation.ObjectBody?.Name == "ProduceInputs");
        var result = IlSqlLifecycleExtractor.Extract(root, bodies, false, new(400_000));
        Assert.Empty(result.Gaps);
        var entry = Assert.IsType<IlSqlLifecycleExtractor.EntryOutput>(result.Entry);
        var command = Assert.Single(entry.Commands);
        Assert.Equal(4, command.Parameters.Count);
        Assert.Equal(4, command.Parameters.Select(parameter => parameter.Allocation).Distinct().Count());
        Assert.Equal(new[] { "argument:0", "argument:1", "argument:2", "argument:3" }, command.Parameters.Select(parameter => parameter.ValueOrigin));
        Assert.Equal(Assert.Single(result.Executions).Id, command.Execution);
    }
}
