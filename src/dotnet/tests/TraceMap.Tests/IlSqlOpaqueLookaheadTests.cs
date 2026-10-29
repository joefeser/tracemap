using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class IlSqlOpaqueLookaheadTests
{
    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Absent_configuration_bodies_are_already_eligible_for_the_explicit_opaque_boundary(string language)
    {
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        Assert.DoesNotContain(bodies, body => body.Input.AssemblyIdentity!.Contains("System.Configuration", StringComparison.Ordinal));
        var root = Assert.Single(bodies, body => body.Observation.ObjectBody?.Name == "ExternalConfigurationProduce");
        var result = IlSqlLifecycleExtractor.Extract(root, bodies, false, new(400_000), opaqueRootInitializers: true);
        Assert.Empty(result.Gaps);
        Assert.NotNull(result.Entry);
        Assert.Single(result.OpaqueInitializers!);
        Assert.Null(result.OpaqueInitializerRejection);
        var compiled = Assert.Single(bodies, body => body.Observation.ObjectBody?.Name == "CompiledOnlyConfigurationProduce");
        var known = IlSqlLifecycleExtractor.Extract(compiled, bodies, false, new(400_000), opaqueRootInitializers: true);
        Assert.Empty(known.Gaps);
        Assert.NotNull(known.Entry);
        Assert.Null(known.OpaqueInitializers);
        Assert.Null(known.OpaqueInitializerRejection); // Fully inspected helpers still work normally.
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Later_inspectable_helper_rejection_names_its_actual_site_not_the_earlier_configuration_getter(string language)
    {
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var root = Assert.Single(bodies, body => body.Observation.ObjectBody?.Name == "CompiledConfigurationProduce");
        var helper = Assert.Single(bodies, body => body.Observation.ObjectBody?.Name == "PublicScalarEcho");
        var result = IlSqlLifecycleExtractor.Extract(root, bodies, false, new(400_000), opaqueRootInitializers: true);
        Assert.Null(result.Entry);
        Assert.Empty(result.Executions);
        var rejection = Assert.IsType<IlSqlLifecycleExtractor.OpaqueChainRejection>(result.OpaqueInitializerRejection);
        Assert.Equal("compiled-callee-requires-inspection", rejection.Reason);
        Assert.Equal("String", rejection.ReturnKind);
        Assert.False(rejection.HasThis);
        Assert.Equal(1, rejection.ParameterCount);
        var match = Assert.Single(rejection.Matches);
        Assert.Equal(helper.Observation.BodyIdentity, match.Body);
        Assert.Equal("retained", match.MetadataState);
        Assert.Equal(helper.Observation.InstructionCount, match.InstructionCount);
        Assert.True(rejection.RejectedCall.Site.Offset > rejection.TriggerCall!.Site.Offset);
        Assert.NotEqual(rejection.RejectedCall.SignatureSha256, rejection.TriggerCall.SignatureSha256);
        Assert.Contains(rejection.RejectedCall.SignatureSha256, Assert.Single(result.Gaps), StringComparison.Ordinal);
        Assert.DoesNotContain("get_ConnectionStrings", Assert.Single(result.Gaps), StringComparison.Ordinal);
        Assert.Equal("opaque-initializer-lookahead", result.MetadataFailure!.Stage);
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Metadata_unavailable_and_ambiguous_matches_remain_distinct_refusals(string language)
    {
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var root = Assert.Single(bodies, body => body.Observation.ObjectBody?.Name == "CompiledConfigurationProduce");
        var helper = Assert.Single(bodies, body => body.Observation.ObjectBody?.Name == "PublicScalarEcho");
        var missing = helper with { Observation = helper.Observation with { ObjectBody = null },
            MemberReferenceAlias = helper.Observation.ObjectBody!.MemberReference };
        var unavailable = IlSqlLifecycleExtractor.Extract(root, bodies.Select(body => body == helper ? missing : body).ToArray(),
            false, new(400_000), opaqueRootInitializers: true);
        Assert.Equal("compiled-callee-metadata-unavailable", unavailable.OpaqueInitializerRejection!.Reason);
        Assert.Equal("unavailable", Assert.Single(unavailable.OpaqueInitializerRejection.Matches).MetadataState);
        Assert.Null(Assert.Single(unavailable.OpaqueInitializerRejection.Matches).HasThrow);
        var duplicate = helper with { Observation = helper.Observation with { MethodIdentity = helper.Observation.MethodIdentity + "|synthetic-duplicate" } };
        var ambiguous = IlSqlLifecycleExtractor.Extract(root, bodies.Append(duplicate).ToArray(), false, new(400_000), opaqueRootInitializers: true);
        Assert.Equal("compiled-callee-ambiguous", ambiguous.OpaqueInitializerRejection!.Reason);
        Assert.Equal(2, ambiguous.OpaqueInitializerRejection.Matches.Count);
        Assert.Null(ambiguous.Entry);
        Assert.Empty(ambiguous.Executions);
    }
}
