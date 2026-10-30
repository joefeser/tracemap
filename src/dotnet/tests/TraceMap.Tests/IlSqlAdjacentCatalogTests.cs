using TraceMap.Core;
namespace TraceMap.Tests;

/// <summary>
/// Adjacent whole-protocol families: command/adapter configuration, typed-array
/// parameter flow with exact-count loops, VB scalar helpers, string concatenation,
/// reflection-identity reads, the configuration opaque chain and event-log tracing.
/// </summary>
public sealed class IlSqlAdjacentCatalogTests
{
    private static IlSqlLifecycleExtractor.Result Extract(
        IReadOnlyList<IlSqlObjectPathExtractor.Body> bodies, string name,
        bool opaque = false, bool trace = false, bool scalar = false)
        => IlSqlLifecycleExtractor.Extract(
            bodies.Single(body => body.Observation.ObjectBody?.Name == name), bodies, false,
            new(400_000), opaqueRootInitializers: opaque, externalTraceBoundary: trace,
            compiledScalarBoundary: scalar, infoMessageBoundary: true);

    private static IlSqlLifecycleExtractor.Result Completed(
        IReadOnlyList<IlSqlObjectPathExtractor.Body> bodies, string name,
        bool opaque = false, bool trace = false, bool scalar = false)
    {
        var result = Extract(bodies, name, opaque, trace, scalar);
        Assert.True(result.Gaps.Count == 0, "GAPS->> " + string.Join(" <<OR>> ", result.Gaps) + " <<-");
        // A state-free root completes without alternatives on the first run.
        if (result.StateAlternatives is null) { Assert.NotNull(result.Entry); return result; }
        return Assert.Single(result.StateAlternatives, alternative => alternative.Completion?.Entry is not null)
            .Completion!;
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Command_configuration_and_collection_flow_support_the_whole_protocol(string language)
    {
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var result = Completed(bodies, "AdjacentProduce");
        var command = Assert.Single(result.Entry!.Commands);
        Assert.Equal(2, command.Parameters.Count); // 4-arg source-column ctor + rebuilt collection parameter
        Assert.Single(result.Executions);
        Assert.Empty(result.Loops ?? []); // collection enumeration is not a loop receipt
        Assert.DoesNotContain("computed-string-values-unbound", result.Claim, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Typed_array_snapshot_and_exact_count_loop_preserve_parameter_identity(string language)
    {
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var result = Completed(bodies, "AdjacentTypedArray");
        var command = Assert.Single(result.Entry!.Commands);
        Assert.Equal(2, command.Parameters.Count); // ToArray snapshot, both attached once
        Assert.All(command.Parameters, parameter => Assert.NotEqual(0, parameter.Allocation.Length));
        Assert.Contains("count-exact", Assert.Single(result.Loops ?? []).Meaning, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("vb")]
    public void Vb_scalar_helpers_select_concretely_and_stay_bounded(string language)
    {
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var result = Completed(bodies, "AdjacentScalarHelpers");
        var command = Assert.Single(result.Entry!.Commands);
        Assert.Equal(2, command.Parameters.Count);
        // IIf chose the concrete 4 and ToInteger passed the boxed 4 through.
        Assert.Equal(4, command.Parameters[0].Size);
        Assert.Equal(4, command.Parameters[1].Size);
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Configuration_chain_becomes_opaque_field_origin(string language)
    {
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var result = Completed(bodies, "AdjacentConfigProduce", opaque: true);
        var initializer = Assert.Single(result.OpaqueInitializers!);
        Assert.Equal(3, initializer.Calls.Count); // get_ConnectionStrings -> get_Item -> get_ConnectionString
        // The three-call configuration chain is recorded as one opaque initializer whose
        // field origin stays opaque; the entry completes from it without catalog entries.
        Assert.Single(result.Entry!.Commands);
        Assert.Contains("opaque-initialization-normal-return-assumed", result.Claim, StringComparison.Ordinal);
        var withoutOpaque = Extract(bodies, "AdjacentConfigProduce");
        Assert.Contains("lifecycle-unknown-call", Assert.Single(withoutOpaque.Gaps), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Event_log_terminal_records_trace_receipts_in_root_bodies_only(string language)
    {
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var result = Extract(bodies, "AdjacentEventLog", trace: true);
        Assert.Empty(result.Gaps);
        Assert.Equal(2, result.StateAlternatives!.Count); // throw guard forks; completing path carries the entry
        var completing = Assert.Single(result.StateAlternatives, alternative => alternative.Completion?.Entry is not null).Completion!;
        Assert.Equal(2, completing.ExternalTraces!.Count);
        Assert.All(completing.ExternalTraces, receipt => Assert.Contains("external-effects-unmodeled", receipt.Boundary, StringComparison.Ordinal));
        // Without the trace boundary the same terminal refuses.
        var refused = Extract(bodies, "AdjacentEventLog");
        Assert.Contains("lifecycle-event-log-boundary", Assert.Single(refused.Gaps), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Adjacent_entries_are_exact_signed_and_legacy_profiles_resolve(string language)
    {
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language, legacyFramework: true);
        var result = Completed(bodies, "AdjacentTypedArray");
        Assert.Equal(2, Assert.Single(result.Entry!.Commands).Parameters.Count);
        var modern = IlSqlLifecycleExtractorTests.Bodies(language);
        var identity = modern.SelectMany(body => body.Observation.Calls)
            .Single(call => call.TargetIdentity.Contains("set_CommandTimeout", StringComparison.Ordinal)).TargetIdentity;
        var changed = modern.Select(body => body with { Observation = body.Observation with
        { Calls = body.Observation.Calls.Select(call => call.TargetIdentity == identity
            ? call with { TargetIdentity = identity.Replace("version:7:4.0.0.0", "version:7:3.0.0.0", StringComparison.Ordinal) } : call).ToArray() } }).ToArray();
        var refused = IlSqlLifecycleExtractor.Extract(
            changed.Single(body => body.Observation.ObjectBody?.Name == "AdjacentProduce"), changed, false,
            new(400_000), infoMessageBoundary: true);
        Assert.NotEmpty(refused.Gaps);
    }
}
