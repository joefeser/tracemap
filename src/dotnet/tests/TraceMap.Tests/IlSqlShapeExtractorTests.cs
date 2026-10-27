using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class IlSqlShapeExtractorTests
{
    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Parameter_bound_counts_attached_parameters_not_only_live_constructor_states(string language)
    {
        var original = Bodies(language).Single(b => b.MethodIdentity.Contains("|method:6:Direct|", StringComparison.Ordinal));
        var binding = Assert.Single(IlCommandBindingExtractor.Extract(original).Bindings);
        var flow = original.ValueFlow!;
        var ctor = original.Calls.Single(call => call.Opcode == "newobj" && call.TargetIdentity.Contains("|names:12:SqlParameter)", StringComparison.Ordinal));
        var add = original.Calls.Single(call => call.TargetIdentity.Contains("|member:3:Add|", StringComparison.Ordinal));
        var dataSet = original.Calls.Single(call => call.Opcode == "newobj" && call.TargetIdentity.Contains("|names:7:DataSet)", StringComparison.Ordinal));
        var fill = original.Calls.Single(call => call.Offset == binding.EndpointOffset);
        var ctorValue = flow.Calls.Single(call => call.Offset == ctor.Offset);
        var addValue = flow.Calls.Single(call => call.Offset == add.Offset);
        foreach (var count in new[] { IlSqlShapeExtractor.MaxParameters, IlSqlShapeExtractor.MaxParameters + 1 })
        {
            var removed = new[] { ctor.Offset, add.Offset, dataSet.Offset, fill.Offset }.ToHashSet();
            var calls = original.Calls.Where(call => !removed.Contains(call.Offset)).ToList();
            var values = flow.Calls.Where(call => !removed.Contains(call.Offset)).ToList();
            for (var i = 0; i < count; i++)
            {
                var offset = 100L + i * 10;
                var origin = new IlValueOrigin("allocation-site", offset.ToString(System.Globalization.CultureInfo.InvariantCulture));
                calls.Add(ctor with { Offset = offset });
                calls.Add(add with { Offset = offset + 1 });
                values.Add(ctorValue with { Offset = offset, Result = origin });
                values.Add(addValue with { Offset = offset + 1, Arguments = [origin] });
            }
            var resultOrigin = new IlValueOrigin("allocation-site", "10000");
            calls.Add(dataSet with { Offset = 10000 });
            calls.Add(fill with { Offset = 10010 });
            values.Add(flow.Calls.Single(call => call.Offset == dataSet.Offset) with { Offset = 10000, Result = resultOrigin });
            values.Add(flow.Calls.Single(call => call.Offset == fill.Offset) with { Offset = 10010, Arguments = [resultOrigin] });
            var changed = original with { Calls = calls, ValueFlow = flow with
            {
                Calls = values, ControlFlow = [new(0, [], false)],
                Returns = [new(10020, "return-operand-candidate", resultOrigin)]
            } };
            var result = IlSqlShapeExtractor.Extract(changed, new([binding with { EndpointOffset = 10010 }], []), new(100_000));
            if (count == IlSqlShapeExtractor.MaxParameters)
                Assert.Equal(count, Assert.Single(result.Observations).Parameters.Count);
            else
            {
                Assert.Empty(result.Observations);
                Assert.Contains("IlSqlShapeParameterLimit", result.Gaps);
            }
        }
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Native_shape_links_exact_calls_and_provenance_without_upgrading_legacy_gaps(string language)
    {
        var assembly = AssemblyPath(language);
        var result = ScanEngine.Scan(new ScanOptions(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(assembly))))!,
            IlBodyEvidenceExtractorShared.TempOutput(), CompiledInputPaths: [assembly], IlBodyEvidence: true));
        var shape = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.ManagedIlSqlShapeCandidate);
        Assert.Equal(EvidenceTiers.Tier3SyntaxOrTextual, shape.EvidenceTier);
        Assert.Equal(RuleIds.DotNetIlSqlShape, shape.RuleId);
        var provenance = result.Manifest.IlBodyProvenance!;
        Assert.Equal(provenance.GeneratorSha256, shape.Properties["ilGeneratorSha256"]);
        Assert.Equal(provenance.BoundedInputSha256, shape.Properties["ilBoundedInputSha256"]);
        Assert.Equal(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(assembly))).ToLowerInvariant(), shape.Properties["rawFileSha256"]);
        var byId = result.Facts.ToDictionary(fact => fact.FactId);
        Assert.Equal(FactTypes.ManagedIlBodyDeclared, byId[shape.Properties["ilBodyFactId"]].FactType);
        Assert.Equal(FactTypes.ManagedIlCallObserved, byId[shape.Properties["ilCallFactId"]].FactType);
        using var parameters = System.Text.Json.JsonDocument.Parse(shape.Properties["parameterShapes"]);
        var parameter = Assert.Single(parameters.RootElement.EnumerateArray());
        foreach (var property in new[] { "constructorCallFactId", "attachmentCallFactId" })
        {
            var call = byId[parameter.GetProperty(property).GetString()!];
            Assert.Equal(FactTypes.ManagedIlCallObserved, call.FactType);
            Assert.Equal(shape.Properties["ilBodyFactId"], call.Properties["ilBodyFactId"]);
            Assert.Equal(shape.Properties["rawFileSha256"], call.Properties["rawFileSha256"]);
        }
        Assert.Contains(result.Facts, fact => fact.RuleId == RuleIds.DotNetIlCommandBinding
            && fact.ContractElement == "IlCommandParameterFlowUnavailable");
        Assert.DoesNotContain("@Name", shape.Properties["parameterShapes"], StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Missing_or_unsafe_flow_never_produces_shape(string language)
    {
        var body = Bodies(language).Single(b => b.MethodIdentity.Contains("|method:6:Direct|", StringComparison.Ordinal));
        var flow = body.ValueFlow!;
        var binding = IlCommandBindingExtractor.Extract(body);
        var node = flow.ControlFlow![0];
        var variants = new[]
        {
            body with { ValueFlow = null },
            body with { ExceptionRegionCount = 1 },
            body with { ValueFlow = flow with { Gaps = ["IlValueStackUnavailable"] } },
            body with { ValueFlow = flow with { ControlFlow = null } },
            body with { ValueFlow = flow with { ControlFlow = [] } },
            body with { ValueFlow = flow with { ControlFlow = [node with { Successors = [node.Offset] }] } },
            body with { ValueFlow = flow with { ControlFlow = [node with { Successors = [1, 2] }] } },
            body with { ValueFlow = flow with { ControlFlow = [node with { InvalidatesConfiguration = true }] } },
            body with { ValueFlow = flow with { ControlFlow = [node with { ExposedOrigins = [new("allocation-site", "0")] }] } },
            body with { ValueFlow = flow with { Returns = [] } },
            body with { ValueFlow = flow with { Returns = [flow.Returns![0], flow.Returns[0]] } },
            body with { ValueFlow = flow with { Returns = [flow.Returns![0] with { Origin = new("argument-slot", "0") }] } }
        };
        foreach (var changed in variants)
        {
            var result = IlSqlShapeExtractor.Extract(changed, binding, new(100_000));
            Assert.Empty(result.Observations);
            Assert.NotEmpty(result.Gaps);
        }
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Encoded_parameter_scope_and_derivation_bounds_are_checked(string language)
    {
        var body = Bodies(language).Single(b => b.MethodIdentity.Contains("|method:6:Direct|", StringComparison.Ordinal));
        var binding = IlCommandBindingExtractor.Extract(body);
        var changed = body with { Calls = body.Calls.Select(call => call.TargetIdentity.Contains("|names:12:SqlParameter)", StringComparison.Ordinal)
            ? call with { TargetIdentity = call.TargetIdentity.Replace("b77a5c561934e089", "0000000000000000", StringComparison.Ordinal) } : call).ToArray() };
        Assert.Empty(IlSqlShapeExtractor.Extract(changed, binding, new(100_000)).Observations);
        var units = 1L + body.Calls.Count + body.ValueFlow!.ControlFlow!.Count;
        Assert.Single(IlSqlShapeExtractor.Extract(body, binding, new(units)).Observations);
        Assert.Contains("IlSqlShapeWorkLimit", IlSqlShapeExtractor.Extract(body, binding, new(units - 1)).Gaps);
        changed = body with { Calls = Enumerable.Repeat(body.Calls[0], IlSqlShapeExtractor.MaxCalls + 1).ToArray() };
        Assert.Contains("IlSqlShapeWorkLimit", IlSqlShapeExtractor.Extract(changed, binding, new(100_000)).Gaps);
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Parameter_origins_mutation_and_duplicate_attachment_are_not_guessed(string language)
    {
        var body = Bodies(language).Single(b => b.MethodIdentity.Contains("|method:6:Direct|", StringComparison.Ordinal));
        var flow = body.ValueFlow!;
        var binding = IlCommandBindingExtractor.Extract(body);
        var constructor = body.Calls.Single(call => call.Opcode == "newobj" && call.TargetIdentity.Contains("|names:12:SqlParameter)", StringComparison.Ordinal));
        var value = flow.Calls.Single(call => call.Offset == constructor.Offset);
        foreach (var index in Enumerable.Range(0, 3))
        {
            var arguments = value.Arguments.ToArray();
            arguments[index] = new("unknown", "");
            var changed = body with { ValueFlow = flow with { Calls = flow.Calls.Select(call => call.Offset == value.Offset ? value with { Arguments = arguments } : call).ToArray() } };
            Assert.Contains("IlSqlShapeParameterOriginUnresolved", IlSqlShapeExtractor.Extract(changed, binding, new(100_000)).Gaps);
        }
        var attachment = body.Calls.Single(call => call.TargetIdentity.Contains("|member:3:Add|", StringComparison.Ordinal));
        var attached = flow.Calls.Single(call => call.Offset == attachment.Offset);
        var duplicateOffset = binding.Bindings[0].EndpointOffset - 1;
        var duplicate = body with
        {
            Calls = body.Calls.Append(attachment with { Offset = duplicateOffset }).ToArray(),
            ValueFlow = flow with { Calls = flow.Calls.Append(attached with { Offset = duplicateOffset }).ToArray() }
        };
        Assert.Contains("IlSqlShapeParameterCallerOrAliasUnresolved", IlSqlShapeExtractor.Extract(duplicate, binding, new(100_000)).Gaps);
        var mutation = duplicate with { Calls = duplicate.Calls.Select(call => call.Offset == duplicateOffset
            ? call with { TargetIdentity = call.TargetIdentity.Replace("|member:3:Add|", "|member:8:set_Size|", StringComparison.Ordinal) } : call).ToArray() };
        Assert.Contains("IlSqlShapeCallOrMutationUnsupported", IlSqlShapeExtractor.Extract(mutation, binding, new(100_000)).Gaps);
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Real_compiled_direct_parameter_and_return_shape_is_bounded(string language)
    {
        var body = Bodies(language).Single(b => b.MethodIdentity.Contains("|method:6:Direct|", StringComparison.Ordinal));
        var binding = IlCommandBindingExtractor.Extract(body);
        Assert.Single(binding.Bindings);
        var result = IlSqlShapeExtractor.Extract(body, binding, new(100_000));
        Assert.True(result.Gaps.Count == 0, string.Join(",", result.Gaps) + "\n" + System.Text.Json.JsonSerializer.Serialize(body.ValueFlow));
        var shape = Assert.Single(result.Observations);
        var parameter = Assert.Single(shape.Parameters);
        Assert.Equal("constant-string-hash", parameter.Name.Kind);
        Assert.Equal(new IlValueOrigin("constant-int32", "12"), parameter.SqlDbType);
        Assert.Equal(new IlValueOrigin("constant-int32", "40"), parameter.Size);
        Assert.Equal("allocation-site", shape.DataSetOrigin.Kind);
        Assert.True(shape.ReturnOffset > shape.EndpointOffset);
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Forwarded_parameter_is_not_a_locally_proven_shape(string language)
    {
        var body = Bodies(language).Single(b => b.MethodIdentity.Contains("|method:9:Forwarded|", StringComparison.Ordinal));
        var result = IlSqlShapeExtractor.Extract(body, IlCommandBindingExtractor.Extract(body), new(100_000));
        Assert.Empty(result.Observations);
        Assert.Contains("IlSqlShapeParameterCallerOrAliasUnresolved", result.Gaps);
    }

    private static IReadOnlyList<IlBodyObservation> Bodies(string language)
    {
        var bytes = File.ReadAllBytes(AssemblyPath(language));
        var limits = new IlBodyLimits();
        var left = IlBodyEvidenceExtractor.ReadCecilBodies(bytes, limits, new(200_000), CancellationToken.None);
        var right = IlBodyEvidenceExtractor.ReadSystemReflectionMetadataBodies(bytes, limits, new(200_000), CancellationToken.None);
        Assert.Empty(IlBodyEvidenceExtractor.CompareBodies(left, right));
        return IlBodyEvidenceExtractor.AgreeValueFlows(left.Bodies, right.Bodies);
    }

    private static string AssemblyPath(string language)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "AGENTS.md"))) root = root.Parent;
        Assert.NotNull(root);
        var config = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        return Path.Combine(root.FullName, "samples", "sql-compiled-wrapper-evidence", language,
            "wrapper", "bin", config, "net48", $"PublicSql.{language}.wrapper.dll");
    }
}
