using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class IlSqlConstructorOwnerTests
{
    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Reference_field_construction_proves_base_slots_without_opaque_reference_values(string language)
    {
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var root = Assert.Single(bodies, body => body.Observation.ObjectBody?.Name == "ReferenceCtorTraceProduce");
        var result = IlSqlLifecycleExtractor.Extract(root, bodies, false, new(400_000), opaqueRootInitializers: true, externalTraceBoundary: true);
        Assert.Empty(result.Gaps);
        Assert.NotNull(result.Entry);
        var view = Assert.Single(result.ConstructorOwnerViews!);
        Assert.NotEqual(view.DerivedOwnerSha256, view.BaseOwnerSha256);
        Assert.Contains(result.Bodies!, site => site.Body == view.TargetBody && site.Binary == view.TargetBinary);
        Assert.Single(result.OpaqueInitializers!);
        Assert.Single(result.ExternalTraces!);
        Assert.Equal(4, Assert.Single(result.Entry.Commands).Parameters.Select(parameter => parameter.Allocation).Distinct().Count());
        var rootWithoutTrace = Assert.Single(bodies, body => body.Observation.ObjectBody?.Name == "ReferenceCtorProduce");
        var strict = IlSqlLifecycleExtractor.Extract(rootWithoutTrace, bodies, false, new(400_000));
        Assert.NotEmpty(strict.Gaps);
        Assert.Null(strict.Entry); // No broad permission to ignore configuration-like calls.
        var paired = Assert.Single(bodies, body => body.Observation.ObjectBody?.Name == "ReferenceOwnerExecutions");
        var isolated = IlSqlLifecycleExtractor.Extract(paired, bodies, false, new(400_000), opaqueRootInitializers: true);
        Assert.Empty(isolated.Gaps);
        Assert.Equal(2, isolated.Executions.Count);
        Assert.Equal(2, isolated.Executions.Select(execution => execution.CommandHash).Distinct().Count());
        Assert.Equal(2, isolated.Entry!.Commands.Select(command => command.Execution).Distinct().Count());
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Missing_wrong_or_unagreed_direct_base_metadata_cannot_authorize_another_field_owner(string language)
    {
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var root = Assert.Single(bodies, body => body.Observation.ObjectBody?.Name == "ReferenceCtorProduce");
        var derived = Assert.Single(bodies, body => body.Observation.ObjectBody is { Name: ".ctor" } meta
            && meta.OwnerType.Contains("ReferenceOwnerDerived", StringComparison.Ordinal));
        Assert.NotNull(derived.Observation.ObjectBody!.BaseType);
        Assert.True(derived.Observation.ObjectBodyAgreement!.ObjectBody);
        foreach (var changed in new[] {
            derived with { Observation = derived.Observation with { ObjectBody = derived.Observation.ObjectBody! with { BaseType = null } } },
            derived with { Observation = derived.Observation with { ObjectBody = derived.Observation.ObjectBody! with { BaseType = "unrelated-owner" } } },
            derived with { Observation = derived.Observation with { ObjectBodyAgreement = null } } })
        {
            var variant = bodies.Select(body => body == derived ? changed : body).ToArray();
            var result = IlSqlLifecycleExtractor.Extract(root, variant, false, new(400_000), opaqueRootInitializers: true);
            Assert.Equal("lifecycle-constructor-owner-unproven", Assert.Single(result.Gaps));
            Assert.Null(result.Entry);
            Assert.Empty(result.Executions);
        }
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Unrelated_field_slots_still_refuse_with_allocation_and_executing_owner_receipt(string language)
    {
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var root = Assert.Single(bodies, body => body.Observation.ObjectBody?.Name == "ReferenceCtorProduce");
        var getter = Assert.Single(bodies, body => body.Observation.ObjectBody?.Name == "get_BaseMarker");
        var meta = getter.Observation.ObjectBody!;
        var instructions = meta.Instructions.Select(line =>
        {
            var parts = line.Split(':', 4);
            return parts[2] == "ldfld" ? line.Replace("type:" + meta.OwnerType + "|member:", "type:unrelated-owner|member:", StringComparison.Ordinal) : line;
        }).ToArray();
        var changed = getter with { Observation = getter.Observation with { ObjectBody = meta with { Instructions = instructions } } };
        var result = IlSqlLifecycleExtractor.Extract(root, bodies.Select(body => body == getter ? changed : body).ToArray(),
            false, new(400_000), opaqueRootInitializers: true);
        Assert.Equal("lifecycle-field-owner", Assert.Single(result.Gaps));
        Assert.NotNull(result.FieldOwnerFailure);
        Assert.NotNull(result.FieldAccessFailure);
        Assert.Equal("ldfld", result.FieldAccessFailure.Opcode);
        Assert.Equal("custom", result.FieldAccessFailure.ReceiverHeapKind);
        Assert.False(result.FieldAccessFailure.FieldOwnerAdmitted);
        Assert.NotNull(result.FieldAccessFailure.FieldDeclaredType);
        Assert.InRange(result.FieldAccessFailure.PrecedingOpcodes.Count, 0, 7);
        Assert.True(result.FieldOwnerFailure.ReceiverIsThis);
        Assert.Equal(2, result.FieldOwnerFailure.AdmittedOwnerSha256s.Count);
        Assert.Equal("field-owner", result.MetadataFailure!.Stage);
        Assert.Null(result.Entry);
    }
    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Non_object_field_receiver_keeps_refusal_and_records_exact_site(string language)
    {
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var root = Assert.Single(bodies, body => body.Observation.ObjectBody?.Name == "ReferenceCtorProduce");
        var getter = Assert.Single(bodies, body => body.Observation.ObjectBody?.Name == "get_BaseMarker");
        var meta = getter.Observation.ObjectBody!;
        var changed = getter with { Observation = getter.Observation with { ObjectBody = meta with {
            Instructions = meta.Instructions.Select(line => {
                var parts = line.Split(':', 4);
                return parts[2] == "ldarg.0" ? string.Join(':', parts[0], parts[1], "ldnull", "") : line;
            }).ToArray() } } };
        var result = IlSqlLifecycleExtractor.Extract(root, bodies.Select(body => body == getter ? changed : body).ToArray(),
            false, new(400_000), opaqueRootInitializers: true);
        Assert.Equal("lifecycle-object-origin:custom", Assert.Single(result.Gaps));
        var diagnostic = Assert.IsType<IlSqlLifecycleExtractor.FieldAccessRejection>(result.FieldAccessFailure);
        Assert.Equal("null", diagnostic.ReceiverValueKind);
        Assert.Null(diagnostic.ReceiverAllocation);
        Assert.Null(diagnostic.ReceiverHeapKind);
        Assert.Null(diagnostic.FieldOwnerAdmitted);
        Assert.Equal(getter.Observation.MetadataToken, diagnostic.MetadataToken);
        Assert.Equal(getter.Observation.BodyIdentity, diagnostic.Site.Body);
        Assert.Equal("field-receiver", result.MetadataFailure!.Stage);
        Assert.Null(result.Entry);
        Assert.Empty(result.Executions);
    }

}
