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
    public void Custom_exception_construction_throws_terminated_alternatives(string language)
    {
        // The v31 boundary became an admission: the base-constructor call is
        // constructor completion and the throw terminates its alternative.
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var result = Extract(bodies, "ClosureExceptionProduce");
        Assert.True(result.Gaps.Count == 0, "GAPS->> " + string.Join(" <<OR>> ", result.Gaps) + " <<-");
        Assert.Contains(result.StateAlternatives!, alternative => alternative.Outcome == "exceptional-termination");
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
    public void Closure_completes_on_the_legacy_framework_profile(string language)
    {
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language, legacyFramework: true);
        var completing = Completing(bodies, "ClosureProduce");
        Assert.Contains(completing, complete => complete.Loops?.Count is 1);
        var traced = Completing(bodies, "ClosureTraceProduce");
        Assert.All(traced, complete => Assert.Single(complete.ExternalTraces!));
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void ByRef_argument_passing_aliases_the_caller_locals(string language)
    {
        // v14-001 outcome B shape: locals passed by reference (ArrayList& and
        // String[]&) into a tracked execute helper; ldloca.s addresses alias the
        // caller's slots and every mutation flows through the aliases.
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var result = Extract(bodies, "ClosureByRefProduce");
        Assert.True(result.Gaps.Count == 0, "GAPS->> " + string.Join(" <<OR>> ", result.Gaps) + " <<-");
        Assert.NotNull(result.Entry);
        var command = Assert.Single(result.Entry.Commands);
        Assert.Equal(2, command.Parameters.Count); // both added through the byref alias
        Assert.Single(result.Executions);
        Assert.Empty(result.Loops ?? []); // ArrayList enumeration is not a loop receipt
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void ByRef_shape_completes_on_the_legacy_framework_profile(string language)
    {
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language, legacyFramework: true);
        var result = Extract(bodies, "ClosureByRefProduce");
        Assert.True(result.Gaps.Count == 0, "GAPS->> " + string.Join(" <<OR>> ", result.Gaps) + " <<-");
        Assert.Equal(2, Assert.Single(result.Entry!.Commands).Parameters.Count);
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Framework_byref_apis_never_receive_local_addresses(string language)
    {
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var result = Extract(bodies, "ClosureByRefFrameworkProduce");
        Assert.NotEmpty(result.Gaps);
        Assert.Null(result.Entry);
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Raw_parameter_items_complete_with_identity_preserved(string language)
    {
        // v15-001 outcome B2: constructor-proven SqlParameter allocations join the
        // lifecycle collection, freeze on Add like custom objects, and attach with
        // their exact allocation identity through the enumeration.
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var result = Extract(bodies, "ClosureRawParameterProduce");
        Assert.True(result.Gaps.Count == 0, "GAPS->> " + string.Join(" <<OR>> ", result.Gaps) + " <<-");
        Assert.NotNull(result.Entry);
        var command = Assert.Single(result.Entry.Commands);
        Assert.Equal(2, command.Parameters.Count);
        Assert.Single(result.Executions);
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Raw_parameter_items_complete_on_the_legacy_framework_profile(string language)
    {
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language, legacyFramework: true);
        var result = Extract(bodies, "ClosureRawParameterProduce");
        Assert.True(result.Gaps.Count == 0, "GAPS->> " + string.Join(" <<OR>> ", result.Gaps) + " <<-");
        Assert.Equal(2, Assert.Single(result.Entry!.Commands).Parameters.Count);
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void String_items_join_the_collection_with_exact_text(string language)
    {
        // Naming lists built in live bodies: constant strings enter the lifecycle
        // collection unfrozen and flow to parameter names with exact provenance.
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var result = Extract(bodies, "ClosureNameListProduce");
        Assert.True(result.Gaps.Count == 0, "GAPS->> " + string.Join(" <<OR>> ", result.Gaps) + " <<-");
        Assert.Single(Assert.Single(result.Entry!.Commands).Parameters);
        Assert.Single(result.Executions);
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Cross_assembly_custom_parameters_resolve_through_memberrefs(string language)
    {
        // The custom parameter type lives in the wrapper role assembly; every ctor
        // and getter call from this root is a cross-assembly memberref (the v15
        // follow-up shape). Expected: resolve, inline, complete.
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var result = Extract(bodies, "ClosureCrossParameterProduce");
        Assert.True(result.Gaps.Count == 0, "GAPS->> " + string.Join(" <<OR>> ", result.Gaps) + " <<-");
        Assert.NotNull(result.Entry);
        var command = Assert.Single(result.Entry.Commands);
        Assert.Equal(2, command.Parameters.Count);
        Assert.Single(result.Executions);
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Null_items_refuse_with_a_self_describing_diagnostic(string language)
    {
        // Declared boundary: null items stay refused; the refusal message stays
        // byte-stable while the found kind and the refusing call identity ride
        // on the result so no report interpretation is needed.
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var result = Extract(bodies, "ClosureNullItemProduce");
        Assert.Equal("lifecycle-object-origin:custom/parameter", Assert.Single(result.Gaps));
        var diagnostic = result.ObjectOriginFailure!;
        Assert.Equal("null", diagnostic.Found);
        Assert.Equal("custom/parameter", diagnostic.Expected);
        Assert.Contains("ArrayList", diagnostic.CallIdentity!, StringComparison.Ordinal);
        Assert.Contains("member:3:Add", diagnostic.CallIdentity!, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Struct_parameter_construction_completes_into_the_collection(string language)
    {
        // v17-001 root cause: value-type construction lowers to ldloca + CALL (not
        // newobj) on the destination local. The constructed value is a tracked
        // custom stored back into the slot and flows into the collection.
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var result = Extract(bodies, "ClosureStructProduce");
        Assert.True(result.Gaps.Count == 0, "GAPS->> " + string.Join(" <<OR>> ", result.Gaps) + " <<-");
        Assert.NotNull(result.Entry);
        var command = Assert.Single(result.Entry.Commands);
        Assert.Equal(2, command.Parameters.Count);
        Assert.Single(result.Executions);
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Struct_parameter_construction_completes_on_the_legacy_profile(string language)
    {
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language, legacyFramework: true);
        var result = Extract(bodies, "ClosureStructProduce");
        Assert.True(result.Gaps.Count == 0, "GAPS->> " + string.Join(" <<OR>> ", result.Gaps) + " <<-");
        Assert.Equal(2, Assert.Single(result.Entry!.Commands).Parameters.Count);
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Struct_wrapped_SqlParameter_property_attaches_directly(string language)
    {
        // The confirmed v17 private consumption shape: the struct itself rides the
        // collection and the loop attaches item.Param (the wrapped SqlParameter).
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var result = Extract(bodies, "ClosureStructPropertyProduce");
        Assert.True(result.Gaps.Count == 0, "GAPS->> " + string.Join(" <<OR>> ", result.Gaps) + " <<-");
        Assert.NotNull(result.Entry);
        var command = Assert.Single(result.Entry.Commands);
        Assert.Equal(2, command.Parameters.Count);
        Assert.Single(result.Executions);
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Struct_wrapped_SqlParameter_property_completes_on_legacy(string language)
    {
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language, legacyFramework: true);
        var result = Extract(bodies, "ClosureStructPropertyProduce");
        Assert.True(result.Gaps.Count == 0, "GAPS->> " + string.Join(" <<OR>> ", result.Gaps) + " <<-");
        Assert.Equal(2, Assert.Single(result.Entry!.Commands).Parameters.Count);
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void ExecProc_helper_enumerates_the_byref_list_and_attaches_wrapped_parameters(string language)
    {
        // The confirmed private topology end to end: structs built in the root, the
        // ArrayList handed BY REFERENCE to the execute helper, the helper enumerating
        // it and attaching each struct's wrapped SqlParameter before Fill.
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var result = Extract(bodies, "ClosureStructExecProduce");
        Assert.True(result.Gaps.Count == 0, "GAPS->> " + string.Join(" <<OR>> ", result.Gaps) + " <<-");
        Assert.NotNull(result.Entry);
        var command = Assert.Single(result.Entry.Commands);
        Assert.Equal(2, command.Parameters.Count);
        Assert.Single(result.Executions);
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void ExecProc_byref_topology_completes_on_the_legacy_profile(string language)
    {
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language, legacyFramework: true);
        var result = Extract(bodies, "ClosureStructExecProduce");
        Assert.True(result.Gaps.Count == 0, "GAPS->> " + string.Join(" <<OR>> ", result.Gaps) + " <<-");
        Assert.Equal(2, Assert.Single(result.Entry!.Commands).Parameters.Count);
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Input_null_guards_fork_bounded_alternatives_with_dbnull(string language)
    {
        // v18-001 stop: a struct ctor null-guarding an INPUT-supplied value. The
        // input's nullness is one runtime fact: one fork, two alternatives — the
        // null-assumed path substitutes DBNull.Value, the other keeps the input.
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var result = Extract(bodies, "ClosureStructNullGuardProduce");
        Assert.True(result.Gaps.Count == 0, "GAPS->> " + string.Join(" <<OR>> ", result.Gaps) + " <<-");
        var expected = language == "vb" ? 12 : 2; // VB forks null x type-name x nested <> x emptiness (shared keys; the digest fix activated the skipped emptiness dimension)
        Assert.Equal(expected, result.StateAlternatives!.Count);
        var entries = result.StateAlternatives.Where(a => a.Completion?.Entry is not null).ToArray();
        Assert.Equal(expected, entries.Length); // every alternative carries entry evidence
        Assert.All(entries, e => Assert.Equal(2, Assert.Single(e.Completion!.Entry!.Commands).Parameters.Count));
        Assert.Contains(result.StateAlternatives.SelectMany(a => a.Decisions),
            d => d.Connection == "input:1" && d.Constant == "null");
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Input_null_guards_fork_on_the_legacy_profile(string language)
    {
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language, legacyFramework: true);
        var result = Extract(bodies, "ClosureStructNullGuardProduce");
        Assert.True(result.Gaps.Count == 0, "GAPS->> " + string.Join(" <<OR>> ", result.Gaps) + " <<-");
        Assert.Equal(language == "vb" ? 12 : 2, result.StateAlternatives!.Count); // the digest fix doubles VB (the activated emptiness dimension)
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Compounded_state_and_input_forks_stay_within_the_raised_budget(string language)
    {
        // v19-001: two guarded inputs + the open/close state guards compound past
        // the old 8-path bound. The raised capacity keeps every path individually
        // proven; no path is selected or merged.
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var result = Extract(bodies, "ClosurePathBudgetProduce");
        Assert.True(result.Gaps.Count == 0, "GAPS->> " + string.Join(" <<OR>> ", result.Gaps) + " <<-");
        var expectedAlts = language == "vb" ? 25 : 9; // VB adds the shared-key CompareString and <> forks; the digest fix doubles both (the emptiness dimension the mixed-form ordinal shortcut mis-folded away)
        Assert.Equal(expectedAlts, result.StateAlternatives!.Count); // completing + the open-guard throw termination
        Assert.Equal(expectedAlts - 1, result.StateAlternatives.Count(alternative => alternative.Completion is not null));
        Assert.All(result.StateAlternatives.Where(a => a.Completion is not null),
            alternative => Assert.Equal(2, Assert.Single(alternative.Completion!.Entry!.Commands).Parameters.Count));
        var inputs = result.StateAlternatives.SelectMany(a => a.Decisions).Select(d => d.Connection).Distinct().Count(d => d.StartsWith("input:", StringComparison.Ordinal));
        Assert.Equal(1, inputs); // the second parameter is constant-built
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Path_budget_fixture_completes_on_the_legacy_profile(string language)
    {
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language, legacyFramework: true);
        var result = Extract(bodies, "ClosurePathBudgetProduce");
        Assert.True(result.Gaps.Count == 0, "GAPS->> " + string.Join(" <<OR>> ", result.Gaps) + " <<-");
        Assert.NotEmpty(result.StateAlternatives!);
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Object_GetType_executes_with_an_untracked_result(string language)
    {
        // v20-001: the last never-executed batch-3 catalog entry — the real memberref
        // returns a SCOPED System.Type, not the unscoped encoding first written.
        // GetType now executes in the collection loop with an untracked result.
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var result = Extract(bodies, "ClosureStructNullGuardProduce");
        Assert.True(result.Gaps.Count == 0, "GAPS->> " + string.Join(" <<OR>> ", result.Gaps) + " <<-");
        Assert.Equal(language == "vb" ? 12 : 2, result.StateAlternatives!.Count); // the digest fix doubles VB (the activated emptiness dimension)
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Object_GetType_resolves_on_the_legacy_profile(string language)
    {
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language, legacyFramework: true);
        var result = Extract(bodies, "ClosureStructNullGuardProduce");
        Assert.True(result.Gaps.Count == 0, "GAPS->> " + string.Join(" <<OR>> ", result.Gaps) + " <<-");
        Assert.Equal(language == "vb" ? 12 : 2, result.StateAlternatives!.Count); // the digest fix doubles VB (the activated emptiness dimension)
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Inverted_comparisons_branch_through_bne_un(string language)
    {
        // ECMA-335 has no ceq.un: "not equal" lowers to bne.un (or the ceq
        // inversion). Both forms resolve for proven operand kinds.
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var result = Extract(bodies, "ClosureStructNullGuardProduce");
        Assert.True(result.Gaps.Count == 0, "GAPS->> " + string.Join(" <<OR>> ", result.Gaps) + " <<-");
        // VB: null x type-eq x the nested <> fork on the assumed-string side; C#: bne.un on ints is concrete.
        Assert.Equal(language == "vb" ? 12 : 2, result.StateAlternatives!.Count); // the digest fix doubles VB (the activated emptiness dimension)
    }

    [Theory]
    [InlineData("vb")]
    public void Full_root_mirror_completes_with_the_confirmed_shape(string language)
    {
        // The sanitized full-root mirror: trace preamble with the four-frame
        // stack chain, boxed CInt(Integer), unbound CInt(String) size, VarChar(-1),
        // two-argument ExecProc, close, return.
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var result = IlSqlLifecycleExtractor.Extract(
            bodies.Single(body => body.Observation.ObjectBody?.Name == "ClosureFullRootProduce"), bodies, false,
            new(4_000_000), opaqueRootInitializers: true, externalTraceBoundary: true,
            compiledScalarBoundary: true, infoMessageBoundary: true);
        // Four nullable inputs x nullness x emptiness x open/close state. The
        // digest-correct literal comparison (review round 2) activated the
        // per-string-input emptiness fork the old mixed-form ordinal shortcut
        // silently mis-folded away: the mirror's four-input shape now exceeds
        // the 128-path capacity and refuses with the census naming the genuine
        // runtime families. The two-string production root shape still fits;
        // a census-driven capacity decision is the owner's, as at v30.
        Assert.NotEmpty(result.Gaps);
        Assert.Contains(result.Gaps, gap => gap.Contains("lifecycle-state-path-limit", StringComparison.Ordinal));
        Assert.Null(result.StateAlternatives);
        Assert.NotNull(result.PathLimitFailure);
        Assert.Contains(result.PathLimitFailure!.ForkCensus, fact => fact.Contains("/null", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("csharp")]
    public void Round3_unsigned_branches_plain_equality_and_checked_arithmetic(string language)
    {
        // Review round 3: bgt.un.s evaluates unsigned (the MaxValue bit pattern
        // takes the branch — a signed misread skips the entry); two derived
        // plain strings with identical text are equal (the same-form comparison
        // compares texts, never an unconditional false); checked subtraction
        // that cannot overflow keeps walking to the entry.
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var result = Extract(bodies, "ClosureRound3Produce");
        Assert.True(result.Gaps.Count == 0, "GAPS->> " + string.Join(" <<OR>> ", result.Gaps) + " <<-");
        Assert.Contains(result.StateAlternatives!, a => a.Completion?.Entry is not null);
    }

    [Theory]
    [InlineData("csharp")]
    public void Concrete_checked_overflow_refuses_by_name(string language)
    {
        // Review round 3: a concrete checked overflow throws at runtime — the
        // wrapped continuation is an impossible normal-completion path and
        // refuses by name instead of emitting a successful lifecycle.
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var result = Extract(bodies, "ClosureOverflowProduce");
        Assert.NotEmpty(result.Gaps);
        Assert.Contains(result.Gaps, gap => gap.Contains("lifecycle-arithmetic-overflow", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Guard_exceptions_throw_terminated_alternatives(string language)
    {
        // v31-001 / the confirmed shape: the custom exception ctor takes the
        // current-method name (unbound), two command texts and an inner that may
        // be a framework exception constructed as an argument. The base call is
        // constructor completion; the constructed inner is an untracked external
        // exception; the throw terminates its alternative; the completing side
        // carries the entry.
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var result = Extract(bodies, "ClosureGuardProduce");
        Assert.True(result.Gaps.Count == 0, "GAPS->> " + string.Join(" <<OR>> ", result.Gaps) + " <<-");
        Assert.NotNull(result.StateAlternatives);
        Assert.Contains(result.StateAlternatives, alternative => alternative.Outcome == "exceptional-termination");
        var completing = result.StateAlternatives.Where(a => a.Completion?.Entry is not null).ToArray();
        Assert.NotEmpty(completing);
        Assert.All(completing, complete => Assert.Single(Assert.Single(complete.Completion!.Entry!.Commands).Parameters));
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Guard_exceptions_on_the_legacy_profile(string language)
    {
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language, legacyFramework: true);
        var result = Extract(bodies, "ClosureGuardProduce");
        Assert.True(result.Gaps.Count == 0, "GAPS->> " + string.Join(" <<OR>> ", result.Gaps) + " <<-");
        Assert.Contains(result.StateAlternatives!, alternative => alternative.Outcome == "exceptional-termination");
    }

    [Theory]
    [InlineData("vb")]
    public void Byref_forwarding_across_overload_frames_completes(string language)
    {
        // v37-001: addresses crossing frames — the root's names local forwarded
        // root -> Forward -> Run, with stores through both array addresses.
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var result = Extract(bodies, "ClosureOverloadProduce");
        Assert.True(result.Gaps.Count == 0, "GAPS->> " + string.Join(" <<OR>> ", result.Gaps) + " <<-");
        Assert.NotNull(result.StateAlternatives);
        Assert.NotEmpty(result.StateAlternatives!);
        foreach (var alternative in result.StateAlternatives!.Where(a => a.Completion?.Entry is not null))
            Assert.Single(Assert.Single(alternative.Completion!.Entry!.Commands).Parameters);
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Null_origin_forward_passes_the_callee_null_guard(string language)
    {
        // v40-001: the forwarded local stays null-origin on the never-assigned
        // fork path; the callee's null guard compares null against null and folds
        // deterministically (a constant, not a fork), then the copy-back store
        // writes through the forwarded address. The assigned path and the null
        // path both complete and attach different parameter counts.
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var result = Extract(bodies, "ClosureNullFoldProduce");
        Assert.True(result.Gaps.Count == 0, "GAPS->> " + string.Join(" <<OR>> ", result.Gaps) + " <<-");
        Assert.NotNull(result.StateAlternatives);
        Assert.NotEmpty(result.StateAlternatives!);
        var parameterCounts = result.StateAlternatives!.Where(a => a.Completion?.Entry is not null)
            .Select(a => Assert.Single(a.Completion!.Entry!.Commands).Parameters.Count).ToList();
        Assert.Contains(1, parameterCounts);
        Assert.Contains(0, parameterCounts);
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void External_detach_unsubscribe_records_without_claiming_the_subscription(string language)
    {
        // v41-001: the walk's terminal stop — a SECOND RemoveHandler after the
        // matched one. The handler stays tracked-shape (the info-delegate and
        // field checks pass) but is no longer in the connection's subscription
        // set: the detach records as unsubscribe-external, the external
        // subscription itself stays unproven, and the paths complete.
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var result = Extract(bodies, "ClosureExternalDetachProduce");
        Assert.True(result.Gaps.Count == 0, "GAPS->> " + string.Join(" <<OR>> ", result.Gaps) + " <<-");
        Assert.NotNull(result.StateAlternatives);
        Assert.NotEmpty(result.StateAlternatives!);
        foreach (var alternative in result.StateAlternatives!.Where(a => a.Completion?.Entry is not null))
        {
            Assert.Single(Assert.Single(alternative.Completion!.Entry!.Commands).Parameters);
            Assert.Equal(new[] { "subscribe", "unsubscribe", "unsubscribe-external" },
                alternative.Completion!.InfoMessages!.Select(receipt => receipt.Action).ToArray());
        }
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Strict_comparisons_evaluate_each_opcode_independently(string language)
    {
        // Codex review fix: the value form of cgt computed !(x<y), so EQUAL
        // count-derived ints folded to true and the walk took the empty early
        // return (no entry). Per-opcode strict evaluation folds equal to false
        // and the entry with its parameter completes.
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var result = Extract(bodies, "ClosureStrictCompareProduce");
        Assert.True(result.Gaps.Count == 0, "GAPS->> " + string.Join(" <<OR>> ", result.Gaps) + " <<-");
        Assert.NotEmpty(result.StateAlternatives!);
        // Pre-fix the equal-int cgt folded TRUE and the walk took the empty
        // early return, leaving zero entry completions.
        var completing = result.StateAlternatives!.Where(a => a.Completion?.Entry is not null).ToArray();
        Assert.NotEmpty(completing);
        foreach (var alternative in completing)
            Assert.Single(Assert.Single(alternative.Completion!.Entry!.Commands).Parameters);
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Fill_returns_an_unbound_count_that_forks(string language)
    {
        // Codex review fix: Fill fabricated Int(0). The count is an unbound
        // integer: a stored > 0 flag forks the rows/no-rows hypothetical, and
        // every completing alternative carries the execution.
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var result = Extract(bodies, "ClosureFillCountProduce");
        Assert.True(result.Gaps.Count == 0, "GAPS->> " + string.Join(" <<OR>> ", result.Gaps) + " <<-");
        Assert.NotNull(result.StateAlternatives);
        Assert.Contains(result.StateAlternatives!, a => a.Completion?.Entry is not null
            && a.Decisions.Any(d => d.Connection.Contains("computed") || d.Connection.Contains("fill")));
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Equality_on_the_fill_count_refuses_by_name(string language)
    {
        // Negative control: no inferred row count. An equality test on the
        // unbound Fill count refuses with the operand kinds named.
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var result = Extract(bodies, "ClosureFillZeroProduce");
        Assert.NotEmpty(result.Gaps);
        Assert.Contains(result.Gaps, gap => gap.Contains("lifecycle-equality-unresolved:computed-int:int", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("csharp")]
    public void Boxed_booleans_render_their_runtime_type(string language)
    {
        // Codex review fix: boxing a Boolean left the int representation, so
        // GetType().ToString() rendered System.Int32 and the .NET 1 type-name
        // dispatch took the wrong branch concretely. The boxing instruction's
        // type is retained: the dispatch renders System.Boolean and the entry
        // completes (pre-fix the walk returned the empty dataset with no entry).
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var result = Extract(bodies, "ClosureBoxedTypeProduce");
        Assert.True(result.Gaps.Count == 0, "GAPS->> " + string.Join(" <<OR>> ", result.Gaps) + " <<-");
        Assert.Contains(result.StateAlternatives!, a => a.Completion?.Entry is not null);
    }

    [Theory]
    [InlineData("csharp")]
    public void Executed_output_parameters_read_back_unknown(string language)
    {
        // Codex review fix: an Output parameter's post-execution value is a
        // database result. The pre-execution origin is stale, so the read is
        // unknown and the dependent string comparison refuses — never a
        // concrete comparison against the initial value.
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var result = Extract(bodies, "ClosureOutputValueProduce");
        // The sentinel comparison must never resolve concretely: C# refuses on
        // the unknown's ToString demand; VB's Conversions.ToString keeps it an
        // unbound string and the comparison forks instead.
        Assert.True(result.Gaps.Count > 0
            || result.StateAlternatives!.Any(a => a.Decisions.Any(d => d.Connection.Contains("string-compare", StringComparison.Ordinal))),
            "the pre-execution sentinel must not compare concretely");
    }

    [Theory]
    [InlineData("vb")]
    public void Field_addresses_deref_store_and_never_escape(string language)
    {
        // v36-001: ldflda on a tracked holder passed ByRef — the address
        // dereferences, stores through the callee, and completes.
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var result = Extract(bodies, "ClosureFieldAddressProduce");
        Assert.True(result.Gaps.Count == 0, "GAPS->> " + string.Join(" <<OR>> ", result.Gaps) + " <<-");
        Assert.NotEmpty(result.StateAlternatives!);
    }

    [Theory]
    [InlineData("vb")]
    public void Indexed_for_loops_complete_with_exact_counts(string language)
    {
        // The Opus battery family: sub/sub.ovf, length-1 bounds, the .s branch forms,
        // ArrayList get_Count/get_Item, and Information.IsNothing on tracked values.
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var result = Extract(bodies, "ClosureIndexedProduce");
        Assert.True(result.Gaps.Count == 0, "GAPS->> " + string.Join(" <<OR>> ", result.Gaps) + " <<-");
        Assert.NotNull(result.StateAlternatives);
        Assert.NotEmpty(result.StateAlternatives!);
        foreach (var alternative in result.StateAlternatives!.Where(a => a.Completion?.Entry is not null))
        {
            var parameters = Assert.Single(alternative.Completion!.Entry!.Commands).Parameters;
            Assert.Equal(2, parameters.Count);
        }
    }

    [Theory]
    [InlineData("vb")]
    public void Typed_element_loads_from_byref_outputs_stay_unknown(string language)
    {
        // v34-001: ldelem from the String()& output parameter — untracked array
        // elements are unknown with no claim; the run completes.
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var result = Extract(bodies, "ClosureOutputNamesProduce");
        Assert.True(result.Gaps.Count == 0, "GAPS->> " + string.Join(" <<OR>> ", result.Gaps) + " <<-");
        Assert.NotNull(result.StateAlternatives);
        Assert.All(result.StateAlternatives!, a => Assert.NotNull(a.Completion));
    }

    [Theory]
    [InlineData("vb")]
    public void COBOL_period_slicing_feeds_unbound_int_parameters(string language)
    {
        // The 1970s-source pattern: Substring/Mid slice a YYYYMM string into
        // parts; CInt parses them into unbound integer parameter values.
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var result = Extract(bodies, "ClosurePeriodProduce");
        Assert.True(result.Gaps.Count == 0, "GAPS->> " + string.Join(" <<OR>> ", result.Gaps) + " <<-");
        Assert.NotNull(result.StateAlternatives);
        Assert.NotEmpty(result.StateAlternatives!);
        // The range check on the unbound month forks: null x empty x > 0 = up to 8 paths.
        Assert.Equal(8, result.StateAlternatives.Count);
        foreach (var alternative in result.StateAlternatives.Where(a => a.Completion?.Entry is not null))
        {
            var parameters = Assert.Single(alternative.Completion!.Entry!.Commands).Parameters;
            Assert.Equal(2, parameters.Count);
        }
        Assert.Contains(result.StateAlternatives.SelectMany(a => a.Decisions),
            decision => decision.Connection.StartsWith("int-compare:", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("vb")]
    public void CInt_over_runtime_strings_stays_unproven(string language)
    {
        // v23-v25: CInt(string) lowers to Conversions.ToInteger(String) — the
        // culture-dependent parse stays unproven. Fed as the SqlParameter SIZE it
        // becomes unbound size evidence (null), never a number.
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var result = Extract(bodies, "ClosureStructNullGuardProduce");
        Assert.True(result.Gaps.Count == 0, "GAPS->> " + string.Join(" <<OR>> ", result.Gaps) + " <<-");
        Assert.Equal(12, result.StateAlternatives!.Count);
        foreach (var alternative in result.StateAlternatives.Where(a => a.Completion?.Entry is not null))
        {
            var parameters = Assert.Single(alternative.Completion!.Entry!.Commands).Parameters;
            Assert.Single(parameters, parameter => parameter.Size is null); // the CInt-parsed size
            Assert.Single(parameters, parameter => parameter.Size == 64); // the constant sibling
        }
    }

    [Theory]
    [InlineData("vb")]
    public void CInt_over_runtime_strings_on_the_legacy_profile(string language)
    {
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language, legacyFramework: true);
        var result = Extract(bodies, "ClosureStructNullGuardProduce");
        Assert.True(result.Gaps.Count == 0, "GAPS->> " + string.Join(" <<OR>> ", result.Gaps) + " <<-");
        Assert.Equal(12, result.StateAlternatives!.Count);
    }

    [Theory]
    [InlineData("vb")]
    public void VB_string_comparisons_fork_bounded_alternatives(string language)
    {
        // v22-001: VB lowers typeName = "..." to Operators.CompareString. With an
        // unbound type name the equality outcome is a runtime hypothetical — keyed
        // by the operand's provenance so both ctor calls share one fact, compounding
        // with the input-null fork (2 x 2 = 4 alternatives, all completing).
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var result = Extract(bodies, "ClosureStructNullGuardProduce");
        Assert.True(result.Gaps.Count == 0, "GAPS->> " + string.Join(" <<OR>> ", result.Gaps) + " <<-");
        Assert.Equal(12, result.StateAlternatives!.Count); // null x type-eq x nested <> x emptiness (the digest fix activated the skipped dimension)
        Assert.All(result.StateAlternatives, alternative => Assert.NotNull(alternative.Completion));
        Assert.Contains(result.StateAlternatives.SelectMany(a => a.Decisions),
            decision => decision.Connection.Contains("string-compare", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("vb")]
    public void VB_string_comparison_forks_on_the_legacy_profile(string language)
    {
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language, legacyFramework: true);
        var result = Extract(bodies, "ClosureStructNullGuardProduce");
        Assert.True(result.Gaps.Count == 0, "GAPS->> " + string.Join(" <<OR>> ", result.Gaps) + " <<-");
        Assert.Equal(12, result.StateAlternatives!.Count);
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Path_limit_refusals_carry_their_fork_census(string language)
    {
        // The many-guards fixture exceeds the budget: the refusal now names the
        // forks that consumed it (runs/alts/queue/sites + per-fact census).
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var root = bodies.Single(body => body.Observation.ObjectBody?.Name == "WholeProtocolManyGuards");
        var result = IlSqlLifecycleExtractor.Extract(root, bodies, false, new(400_000), infoMessageBoundary: true);
        Assert.Single(result.Gaps, gap => gap == "lifecycle-state-path-limit");
        var census = result.PathLimitFailure!;
        Assert.True(census.Runs > 0);
        Assert.NotEmpty(census.ForkCensus);
        Assert.Contains(census.ForkCensus, entry => entry.Contains("/1 x", StringComparison.Ordinal)); // the state guards
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
