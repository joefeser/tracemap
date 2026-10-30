using TraceMap.Core;
namespace TraceMap.Tests;

/// <summary>
/// Synthetic closure mirroring the public v12-001 body/edge table: two inner roots
/// plus connection/adapter-build/parameter/exception/crypto/table-mapping/debug
/// helper bodies, run under all five boundary flags. Every refusal here is an
/// enumerated boundary (catalog admission or by-design), never a decoding gap.
/// </summary>
public sealed class IlSqlClosureShapeTests
{
    private static IlSqlLifecycleExtractor.Result Extract(
        IReadOnlyList<IlSqlObjectPathExtractor.Body> bodies, string name,
        bool opaque = true, bool trace = true, bool scalar = true)
        => IlSqlLifecycleExtractor.Extract(
            bodies.Single(body => body.Observation.ObjectBody?.Name == name), bodies, false,
            new(400_000), opaqueRootInitializers: opaque, externalTraceBoundary: trace,
            compiledScalarBoundary: scalar, infoMessageBoundary: true);

    private static IlSqlLifecycleExtractor.Result[] Completing(
        IReadOnlyList<IlSqlObjectPathExtractor.Body> bodies, string name,
        bool opaque = true, bool trace = true, bool scalar = true)
    {
        var result = Extract(bodies, name, opaque, trace, scalar);
        Assert.True(result.Gaps.Count == 0, "GAPS->> " + string.Join(" <<OR>> ", result.Gaps) + " <<-");
        var completions = result.StateAlternatives is not null
            ? result.StateAlternatives.Where(alternative => alternative.Completion?.Entry is not null)
                .Select(alternative => alternative.Completion!).ToArray()
            : [result];
        Assert.NotEmpty(completions);
        return completions;
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Whole_closure_producer_completes_with_all_boundaries(string language)
    {
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var completing = Completing(bodies, "ClosureProduce");
        // The open guard terminates its alternative; the close guard completes on both
        // sides, so every completing alternative carries its own entry identities.
        Assert.Contains(completing, complete => complete.Loops?.Count is 1);
        foreach (var complete in completing)
        {
            var command = Assert.Single(complete.Entry!.Commands);
            Assert.Equal(3, command.Parameters.Count); // sourced 4-arg + two rebuilt from the typed array
            Assert.Single(complete.Executions);
            Assert.Contains("count-exact", Assert.Single(complete.Loops!).Meaning, StringComparison.Ordinal);
            // The crypto-shaped ctor call is qualified by the compiled-scalar boundary.
            var initializer = Assert.Single(complete.OpaqueInitializers!);
            Assert.Single(initializer.CompiledCallees!);
            Assert.Contains("compiled-scalar-normal-return-assumed", complete.Claim, StringComparison.Ordinal);
            Assert.DoesNotContain("computed-string-values-unbound", complete.Claim, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Whole_closure_trace_producer_completes_with_delegate_and_event_log(string language)
    {
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var completing = Completing(bodies, "ClosureTraceProduce");
        foreach (var complete in completing)
        {
            Assert.Single(complete.Entry!.Commands);
            Assert.Single(complete.Executions);
            Assert.NotEmpty(complete.InfoMessages!); // subscribe/unsubscribe + callback inspection
            var receipt = Assert.Single(complete.ExternalTraces!);
            Assert.Contains("external-effects-unmodeled", receipt.Boundary, StringComparison.Ordinal);
            Assert.True(receipt.ArgumentsPartial); // debug-name message is an unbound computed string
            Assert.Contains("computed-string-values-unbound", complete.Claim, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Table_mapping_index_reads_refuse_beyond_the_catalog(string language)
    {
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var result = Extract(bodies, "ClosureNamesProduce");
        var gap = Assert.Single(result.Gaps);
        Assert.Contains("lifecycle-unknown-call", gap, StringComparison.Ordinal);
        Assert.Contains("get_Item", gap, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Stack_chain_reads_resolve_under_the_trace_boundary(string language)
    {
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var completing = Completing(bodies, "ClosureFrameProduce");
        foreach (var complete in completing)
        {
            Assert.True(complete.UnboundStringCount > 0); // frame method name stays unbound
            var receipt = Assert.Single(complete.ExternalTraces!);
            Assert.True(receipt.ArgumentsPartial);
        }
        // Without the external-trace boundary the same chain refuses explicitly.
        var refused = Extract(bodies, "ClosureFrameProduce", trace: false);
        Assert.Contains("lifecycle-stack-chain-boundary", Assert.Single(refused.Gaps), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Input_driven_parameter_sizes_stay_unresolved(string language)
    {
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var result = Extract(bodies, "ClosureInputProduce");
        Assert.Contains("lifecycle-integer-unresolved", Assert.Single(result.Gaps), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Custom_exception_construction_refuses_at_the_unmodeled_base_call(string language)
    {
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var result = Extract(bodies, "ClosureExceptionProduce");
        var gap = Assert.Single(result.Gaps);
        Assert.Contains("lifecycle-unknown-call", gap, StringComparison.Ordinal);
        Assert.Contains("names:9:Exception", gap, StringComparison.Ordinal);
        Assert.Contains("member:5:.ctor", gap, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Nested_framework_exception_construction_refuses_outside_direct_throw(string language)
    {
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var result = Extract(bodies, "ClosureExceptionNestedProduce");
        var gap = Assert.Single(result.Gaps);
        Assert.Contains("lifecycle-unknown-call", gap, StringComparison.Ordinal);
        Assert.Contains("InvalidOperationException", gap, StringComparison.Ordinal);
        Assert.Contains("member:5:.ctor", gap, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Exception_debug_reads_flow_as_unbound_strings(string language)
    {
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var completing = Completing(bodies, "ClosureDescribeProduce");
        foreach (var complete in completing)
        {
            Assert.True(complete.UnboundStringCount > 0);
            var receipt = Assert.Single(complete.ExternalTraces!);
            Assert.True(receipt.ArgumentsPartial); // message and inner message are unbound reads
            Assert.Contains("computed-string-values-unbound", complete.Claim, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Non_primitive_root_arguments_leave_typed_admissions_refusing(string language)
    {
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var result = Extract(bodies, "ClosureTypedInputProduce");
        Assert.Contains("lifecycle-info-connection-shape", Assert.Single(result.Gaps), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Zero_constant_state_guards_lower_to_a_nonzero_test_and_fork(string language)
    {
        // Close() compares State != ConnectionState.Closed (Closed == 0), which both
        // compilers lower to ldc.i4.0 + cgt.un; the fork must still be a state test.
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var result = Extract(bodies, "ClosureProduce");
        Assert.True(result.Gaps.Count == 0, "GAPS->> " + string.Join(" <<OR>> ", result.Gaps) + " <<-");
        var decisions = result.StateAlternatives!.SelectMany(alternative => alternative.Decisions).ToArray();
        Assert.NotEmpty(decisions);
        Assert.Contains(decisions, decision => decision.Comparison == "not-equal-constant" && decision.Constant == "0");
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Crypto_shaped_ctor_requires_the_compiled_scalar_boundary(string language)
    {
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var result = Extract(bodies, "ClosureProduce", scalar: false);
        Assert.NotEmpty(result.Gaps); // without the opt-in the transform body executes and refuses
        Assert.Contains(result.Gaps, gap => gap.Contains("lifecycle-", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Closure_extractions_replay_deterministically(string language)
    {
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var first = Extract(bodies, "ClosureProduce");
        var second = Extract(bodies, "ClosureProduce");
        Assert.Equal(first.Claim, second.Claim);
        Assert.Equal(
            first.StateAlternatives?.Select(alternative => alternative.Completion?.Executions.Count),
            second.StateAlternatives?.Select(alternative => alternative.Completion?.Executions.Count));
    }
}
