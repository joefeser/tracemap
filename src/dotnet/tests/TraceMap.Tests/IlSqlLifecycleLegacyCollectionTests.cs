using Mono.Cecil;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class IlSqlLifecycleLegacyCollectionTests
{
    private const string Core = "scope(assembly:name:8:mscorlib|version:7:4.0.0.0|culture:7:neutral|publicKeyToken:16:b77a5c561934e089)";
    private const string Legacy = "scope(assembly:name:8:mscorlib|version:7:2.0.0.0|culture:7:neutral|publicKeyToken:16:b77a5c561934e089)";

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Every_legacy_collection_operation_requires_exact_signed_owner_and_signature(string language)
    {
        var calls = IlSqlLifecycleExtractorTests.Bodies(language).SelectMany(body => body.Observation.Calls)
            .Select(call => call.TargetIdentity).Distinct().ToArray();
        foreach (var label in new[] { "arraylist-new", "arraylist-add", "arraylist-enumerator", "enumerator-current", "enumerator-next" })
        {
            var original = Assert.Single(calls, call => IlSqlLifecycleApis.Resolve(call, []) == label);
            var legacy = original.Replace(Core, Legacy, StringComparison.Ordinal);
            Assert.NotEqual(original, legacy);
            Assert.Equal(label, IlSqlLifecycleApis.Resolve(legacy, []));
            foreach (var forged in new[] {
                legacy.Replace("b77a5c561934e089", "0000000000000000", StringComparison.Ordinal),
                legacy.Replace("2.0.0.0", "3.0.0.0", StringComparison.Ordinal),
                legacy.Replace("mscorlib", "fakecore", StringComparison.Ordinal),
                legacy.Replace("neutral", "en-USxx", StringComparison.Ordinal),
                legacy.Replace("System.Collections", "Custom.Collections", StringComparison.Ordinal),
                legacy.Replace("hasThis:true", "hasThis:false", StringComparison.Ordinal),
                legacy.Replace("arity:0", "arity:1", StringComparison.Ordinal),
                legacy.Replace("explicitThis:false", "explicitThis:true", StringComparison.Ordinal) })
                Assert.Null(IlSqlLifecycleApis.Resolve(forged, []));
        }
        // Legacy generic collections and System.Data are outside this core profile.
        foreach (var call in calls.Where(call => IlSqlLifecycleApis.Resolve(call, []) == "parameter-value"))
            Assert.Null(IlSqlLifecycleApis.Resolve(call.Replace("4.0.0.0", "2.0.0.0", StringComparison.Ordinal), []));
    }

    [Fact]
    public void Legacy_boxing_requires_exact_type_identity_and_the_matching_declared_input_type()
    {
        const string primitive = "type(namespace:6:System|names:5:Int32)";
        var operand = "t:" + Legacy + primitive;
        Assert.True(IlSqlLifecycleApis.IsInputBox(operand, primitive));
        Assert.False(IlSqlLifecycleApis.IsInputBox(operand, "type(namespace:6:System|names:7:Boolean)"));
        Assert.False(IlSqlLifecycleApis.IsInputBox(operand.Replace("2.0.0.0", "3.0.0.0", StringComparison.Ordinal), primitive));
        Assert.False(IlSqlLifecycleApis.IsInputBox(operand.Replace("b77a5c561934e089", "0000000000000000", StringComparison.Ordinal), primitive));
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Dual_reader_compiled_legacy_core_profile_preserves_fields_enumeration_and_parameter_identity(string language)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))) directory = directory.Parent;
        Assert.NotNull(directory);
        var config = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var bodies = new List<IlSqlObjectPathExtractor.Body>();
        foreach (var role in new[] { "end-to-end", "wrapper" })
        {
            var path = Path.Combine(directory.FullName, "samples", "sql-compiled-wrapper-evidence", language,
                role, "bin", config, "net48", $"PublicSql.{language}.{role}.dll");
            byte[] bytes;
            // Independently authored public fixture metadata, rewritten only in memory.
            // No fixture/application/provider method is executed.
            using (var module = ModuleDefinition.ReadModule(new MemoryStream(File.ReadAllBytes(path)), new ReaderParameters { InMemory = true }))
            {
                var core = Assert.Single(module.AssemblyReferences, reference => reference.Name == "mscorlib");
                Assert.Equal("b77a5c561934e089", Convert.ToHexStringLower(core.PublicKeyToken));
                core.Version = new Version(2, 0, 0, 0);
                using var output = new MemoryStream(); module.Write(output); bytes = output.ToArray();
            }
            var left = IlBodyEvidenceExtractor.ReadCecilBodies(bytes, new(), new(400_000), CancellationToken.None);
            var right = IlBodyEvidenceExtractor.ReadSystemReflectionMetadataBodies(bytes, new(), new(400_000), CancellationToken.None);
            Assert.Empty(IlBodyEvidenceExtractor.CompareBodies(left, right));
            var agreed = IlBodyEvidenceExtractor.AgreeValueFlows(left.Bodies, right.Bodies);
            Assert.All(agreed, body => Assert.NotNull(body.ObjectBody));
            var hash = SqlServerSchemaEvidence.Hash(bytes);
            var input = new IlInputOutcome(Path.GetFileName(path), role, "admitted", "local", hash, hash,
                left.AssemblyIdentity, left.ModuleName, left.ModuleMvid, null, []);
            bodies.AddRange(agreed.Select(body => new IlSqlObjectPathExtractor.Body(body, input)));
        }
        var calls = bodies.SelectMany(body => body.Observation.Calls).Select(call => call.TargetIdentity).ToArray();
        foreach (var label in new[] { "arraylist-new", "arraylist-add", "arraylist-enumerator", "enumerator-current", "enumerator-next" })
            Assert.Contains(calls, call => call.Contains(Legacy, StringComparison.Ordinal) && IlSqlLifecycleApis.Resolve(call, []) == label);
        var root = Assert.Single(bodies, body => body.Observation.ObjectBody?.Name == "ProduceInputs");
        var result = IlSqlLifecycleExtractor.Extract(root, bodies, false, new(400_000));
        Assert.Empty(result.Gaps);
        var entry = Assert.IsType<IlSqlLifecycleExtractor.EntryOutput>(result.Entry);
        var command = Assert.Single(entry.Commands);
        Assert.Equal(4, command.Parameters.Count);
        Assert.Equal(4, command.Parameters.Select(parameter => parameter.Allocation).Distinct().Count());
        Assert.Equal(new[] { "argument:0", "argument:1", "argument:2", "argument:3" }, command.Parameters.Select(parameter => parameter.ValueOrigin));
        Assert.Equal(Assert.Single(result.Executions).Id, command.Execution);
        foreach (var (name, executions) in new[] { ("ArrayParameterRead", 1), ("ArrayPropertyRead", 1), ("ArraySeparateExecutions", 2) })
        {
            var consumer = Assert.Single(bodies, body => body.Observation.ObjectBody?.Name == name);
            var read = IlSqlLifecycleExtractor.Extract(consumer, bodies, false, new(400_000));
            Assert.Empty(read.Gaps);
            Assert.NotNull(read.Returned);
            Assert.Equal(executions, read.Executions.Count);
            Assert.Equal($"execution:{executions - 1}", read.Returned.Value.Execution);
            Assert.Equal($"execution:{executions - 1}", Assert.Single(read.Returned.SchemaExecutions));
        }
        foreach (var name in new[] { "UnknownGuard", "UnknownCommand", "EmptyProducer", "DiscardInputs",
            "ArrayWrongElement", "ArrayWrongCustomType", "ArrayRepeatedEnumeration", "ArrayUnsupportedCollection",
            "ArrayUnsupportedMutation", "ArrayMutatedElement", "ArrayEscaped", "ArrayTooLarge", "ArrayInvalidAttachment", "ArrayIncompleteRead" })
        {
            var invalid = Assert.Single(bodies, body => body.Observation.ObjectBody?.Name == name);
            var refused = IlSqlLifecycleExtractor.Extract(invalid, bodies, false, new(400_000));
            Assert.NotEmpty(refused.Gaps);
            Assert.Null(refused.Entry);
            Assert.Empty(refused.Executions);
        }
    }
}
