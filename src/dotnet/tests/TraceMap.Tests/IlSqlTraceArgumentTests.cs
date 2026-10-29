using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class IlSqlTraceArgumentTests
{
    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Computed_scalar_messages_produce_partial_receipts_without_inventing_values(string language)
    {
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        foreach (var name in new[] { "TraceComputedProduce", "TraceBuiltLocalProduce", "TraceArrayProduce" })
        {
            var root = Assert.Single(bodies, body => body.Observation.ObjectBody?.Name == name);
            var result = IlSqlLifecycleExtractor.Extract(root, bodies, false, new(400_000), externalTraceBoundary: true);
            Assert.Empty(result.Gaps);
            Assert.NotNull(result.Entry);
            var trace = Assert.Single(result.ExternalTraces!);
            Assert.True(trace.ArgumentsPartial);
            Assert.Contains("partial-unbound", trace.ArgumentEffects, StringComparison.Ordinal);
            Assert.StartsWith("computed-string:", Assert.Single(trace.ArgumentOrigins), StringComparison.Ordinal);
            Assert.NotEmpty(result.TraceStringComputations!);
            Assert.All(result.TraceStringComputations!, computation =>
            {
                Assert.True(computation.Call.MayThrow);
                Assert.Equal("unbound-string", computation.Result);
            });
            Assert.Equal(4, Assert.Single(result.Entry.Commands).Parameters.Select(parameter => parameter.Allocation).Distinct().Count());
        }
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Unknown_object_formatting_arbitrary_argument_calls_and_computed_SQL_names_still_refuse(string language)
    {
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        foreach (var name in new[] { "TraceObjectConcatRefusal", "TraceUnknownBuilderRefusal", "TraceComputedCommandRefusal" })
        {
            var root = Assert.Single(bodies, body => body.Observation.ObjectBody?.Name == name);
            var result = IlSqlLifecycleExtractor.Extract(root, bodies, false, new(400_000), externalTraceBoundary: true);
            Assert.NotEmpty(result.Gaps);
            Assert.Null(result.Entry);
            Assert.Empty(result.Executions);
        }
        var positive = Assert.Single(bodies, body => body.Observation.ObjectBody?.Name == "TraceComputedProduce");
        var strict = IlSqlLifecycleExtractor.Extract(positive, bodies, false, new(400_000));
        Assert.NotEmpty(strict.Gaps);
        Assert.Null(strict.Entry);
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Scalar_string_catalog_keeps_exact_framework_identity(string language)
    {
        var calls = IlSqlLifecycleExtractorTests.Bodies(language).SelectMany(body => body.Observation.Calls)
            .Select(call => call.TargetIdentity).Distinct().Where(identity => IlSqlTraceStringApis.Resolve(identity) is not null).ToArray();
        Assert.NotEmpty(calls);
        foreach (var identity in calls)
        {
            Assert.Null(IlSqlTraceStringApis.Resolve(identity.Replace("publicKeyToken:16:", "publicKeyToken:15:", StringComparison.Ordinal)));
            Assert.Null(IlSqlTraceStringApis.Resolve(identity.Replace("arity:0", "arity:1", StringComparison.Ordinal)));
            Assert.Null(IlSqlTraceStringApis.Resolve(identity.Replace("version:7:4.0.0.0", "version:7:3.0.0.0", StringComparison.Ordinal)
                .Replace("version:7:2.0.0.0", "version:7:3.0.0.0", StringComparison.Ordinal)
                .Replace("version:7:8.0.0.0", "version:7:9.0.0.0", StringComparison.Ordinal)
                .Replace("version:8:10.0.0.0", "version:8:11.0.0.0", StringComparison.Ordinal)));
        }
    }
}
