using TraceMap.Core;
namespace TraceMap.Tests;

public sealed class IlSqlTraceStackNameTests
{
    [Theory]
    [InlineData("csharp", "TraceStackNamesProduce", 2)]
    [InlineData("vb", "TraceStackNamesProduce", 2)]
    [InlineData("csharp", "TraceLongStackNamesProduce", 8)]
    [InlineData("vb", "TraceLongStackNamesProduce", 8)]
    public void Stack_names_are_unknown_logging_values_and_preserve_parameter_identity(string language, string name, int captures)
    {
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var root = Assert.Single(bodies, body => body.Observation.ObjectBody?.Name == name);
        if (name == "TraceStackNamesProduce")
            Assert.Contains(root.Observation.ObjectBody!.Instructions, line => line.Split(':', 4)[2].StartsWith("stloc", StringComparison.Ordinal));
        var result = IlSqlLifecycleExtractor.Extract(root, bodies, false, new(400_000), externalTraceBoundary: true);
        Assert.Empty(result.Gaps);
        Assert.NotNull(result.Entry);
        Assert.Null(result.ExternalTraceFailure);
        var receipt = Assert.Single(result.ExternalTraces!);
        Assert.Equal("explicit-external-web-trace/3", receipt.Policy);
        Assert.True(receipt.ArgumentsPartial);
        Assert.Equal(2, receipt.ArgumentOrigins.Count);
        Assert.Equal(3 + captures * 4, receipt.Calls.Count); // Current, Trace, closed four-call chains, Write.
        Assert.Equal(captures, result.TraceStringComputations!.Count(value => value.ArgumentOrigins.Contains("external-stack-method-name")));
        Assert.All(result.TraceStringComputations!, value => Assert.Equal("unbound-string", value.Result));
        Assert.Equal(4, Assert.Single(result.Entry.Commands).Parameters.Select(value => value.Allocation).Distinct().Count());
        var strict = IlSqlLifecycleExtractor.Extract(root, bodies, false, new(400_000));
        Assert.NotEmpty(strict.Gaps);
        Assert.Null(strict.Entry);
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Dynamic_frames_and_general_reflection_still_refuse_at_an_exact_site(string language)
    {
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        foreach (var name in new[] { "TraceDynamicFrameRefusal", "TraceGeneralReflectionRefusal" })
        {
            var root = Assert.Single(bodies, body => body.Observation.ObjectBody?.Name == name);
            var result = IlSqlLifecycleExtractor.Extract(root, bodies, false, new(400_000), externalTraceBoundary: true);
            Assert.NotEmpty(result.Gaps);
            Assert.Null(result.Entry);
            Assert.Empty(result.Executions);
            var rejection = Assert.IsType<IlSqlLifecycleExtractor.ExternalTraceRejection>(result.ExternalTraceFailure);
            Assert.Equal(root.Observation.MetadataToken, rejection.MetadataToken);
            Assert.Equal(root.Observation.BodyIdentity, rejection.Site.Body);
            Assert.InRange(rejection.Site.Offset, 0, long.MaxValue);
        }
    }

    [Fact]
    public void Stack_reference_local_escape_refuses()
    {
        var bodies = IlSqlLifecycleExtractorTests.Bodies("csharp");
        var root = Assert.Single(bodies, body => body.Observation.ObjectBody?.Name == "TraceStackEscapeRefusal");
        var result = IlSqlLifecycleExtractor.Extract(root, bodies, false, new(400_000), externalTraceBoundary: true);
        Assert.Contains("lifecycle-external-trace-local-store-tracked-or-unproved-value", result.Gaps);
        Assert.Null(result.Entry);
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Stack_catalog_rejects_wrong_signature_or_framework_identity(string language)
    {
        var identities = IlSqlLifecycleExtractorTests.Bodies(language).SelectMany(body => body.Observation.Calls)
            .Select(call => call.TargetIdentity).Distinct().Where(identity => IlSqlTraceStackNameApis.Resolve(identity) is not null).ToArray();
        Assert.Equal(4, identities.Length);
        foreach (var identity in identities)
        {
            Assert.Null(IlSqlTraceStackNameApis.Resolve(identity.Replace("arity:0", "arity:1", StringComparison.Ordinal)));
            Assert.Null(IlSqlTraceStackNameApis.Resolve(identity.Replace("b77a5c561934e089", "0000000000000000", StringComparison.Ordinal)));
            Assert.Null(IlSqlTraceStackNameApis.Resolve(identity.Replace("version:7:4.0.0.0", "version:7:3.0.0.0", StringComparison.Ordinal)
                .Replace("version:7:2.0.0.0", "version:7:3.0.0.0", StringComparison.Ordinal)));
        }
    }
}
