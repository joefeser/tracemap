using TraceMap.Core;
namespace TraceMap.Tests;

/// <summary>
/// Bounded unbound-state control: runtime State reads never become enum values; both
/// outcomes of every state comparison survive as separate path alternatives with their
/// own allocation/execution identities, and unsupported shapes still refuse.
/// </summary>
public sealed class IlSqlStateControlTests
{
    private static IlSqlLifecycleExtractor.Result Extract(
        IReadOnlyList<IlSqlObjectPathExtractor.Body> bodies, string name, bool legacy = false)
        => IlSqlLifecycleExtractor.Extract(
            bodies.Single(body => body.Observation.ObjectBody?.Name == name), bodies, false,
            new(400_000), infoMessageBoundary: true);

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void State_guard_throw_terminates_only_its_own_alternative(string language)
    {
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var result = Extract(bodies, "WholeProtocolProduce");
        Assert.True(result.Gaps.Count == 0, string.Join("\n", result.Gaps));
        Assert.Null(result.Entry); Assert.Empty(result.Executions);
        var alternatives = result.StateAlternatives!;
        Assert.Equal(2, alternatives.Count);
        var terminated = Assert.Single(alternatives, alternative => alternative.Outcome == "exceptional-termination");
        Assert.Null(terminated.Completion);
        Assert.NotNull(terminated.Termination);
        Assert.Contains("uncaught-exception", terminated.TerminationLimitation, StringComparison.Ordinal);
        Assert.Contains("catch-continuation-not-executed", terminated.TerminationLimitation, StringComparison.Ordinal);
        Assert.False(terminated.Decisions[0].EqualAssumed);
        var completed = Assert.Single(alternatives, alternative => alternative.Outcome == "normal-completion");
        Assert.True(completed.Decisions[0].EqualAssumed);
        var entry = completed.Completion!.Entry!;
        Assert.Single(completed.Completion.Executions);
        Assert.Single(Assert.Single(entry.Commands).Parameters);
        Assert.Single(completed.Completion.ConnectionStateReads!);
        Assert.Contains("state-conditional-alternatives", result.Claim, StringComparison.Ordinal);
        Assert.Contains("per-alternative-allocation-and-execution-identity", result.Claim, StringComparison.Ordinal);
        Assert.Contains("runtime-state-control-unproven", result.Claim, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Natural_guard_shape_keeps_both_completions_separate(string language)
    {
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var result = Extract(bodies, "WholeProtocolGuardedFill");
        Assert.True(result.Gaps.Count == 0, string.Join("\n", result.Gaps));
        var alternatives = result.StateAlternatives!;
        Assert.Equal(2, alternatives.Count);
        var filled = Assert.Single(alternatives, alternative => alternative.Completion?.Entry is not null);
        Assert.NotNull(filled.Completion!.Entry); Assert.Single(filled.Completion.Entry.Commands);
        Assert.Equal("normal-completion", filled.Outcome);
        var skipped = Assert.Single(alternatives, alternative => alternative.Completion?.Entry is null);
        Assert.Equal("normal-completion-without-entry-evidence", skipped.Outcome);
        Assert.Empty(skipped.Completion!.Executions);
        Assert.NotEqual(filled.Decisions[0].EqualAssumed, skipped.Decisions[0].EqualAssumed);
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Incompatible_attachments_never_merge_across_alternatives(string language)
    {
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var result = Extract(bodies, "WholeProtocolChoiceAttachment");
        Assert.True(result.Gaps.Count == 0, string.Join("\n", result.Gaps));
        var alternatives = result.StateAlternatives!;
        Assert.Equal(2, alternatives.Count);
        Assert.All(alternatives, alternative => Assert.NotNull(alternative.Completion!.Entry));
        var types = alternatives.Select(alternative => alternative.Completion!.Entry!.Commands[0].Parameters[0].SqlDbType).ToHashSet();
        Assert.Equal(2, types.Count); // SqlDbType.Int vs SqlDbType.VarChar stay per-path
        var allocations = alternatives.Select(alternative => alternative.Completion!.Entry!.Commands[0].Parameters[0].Allocation);
        Assert.Single(allocations.Distinct()); // per-alternative numbering: same id, different content
        // The shared prefix (command construction before the fork) keeps one identity.
        var commands = alternatives.Select(alternative => alternative.Completion!.Entry!.Commands[0].Allocation).Distinct();
        Assert.Single(commands);
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Separate_reads_are_separate_decisions(string language)
    {
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var result = Extract(bodies, "WholeProtocolRecheck");
        Assert.True(result.Gaps.Count == 0, string.Join("\n", result.Gaps));
        var alternatives = result.StateAlternatives!;
        // The closing branch of the FIRST guard returns before the second read runs,
        // so three alternatives exist, not four: every decision outcome is its own path.
        Assert.Equal(3, alternatives.Count);
        var entries = alternatives.Where(alternative => alternative.Completion?.Entry is not null).ToArray();
        Assert.Single(entries);
        Assert.Equal(2, entries[0].Decisions.Count);
        Assert.All(entries[0].Decisions, decision => Assert.True(decision.EqualAssumed));
        Assert.Equal(1, alternatives.Count(alternative => alternative.Decisions.Count == 2 && alternative.Completion?.Entry is null));
        Assert.Single(alternatives, alternative => alternative.Decisions.Count == 1);
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void State_read_inside_the_row_loop_composes_with_the_symbolic_pass(string language)
    {
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var result = Extract(bodies, "WholeProtocolGuardInLoop");
        Assert.True(result.Gaps.Count == 0, string.Join("\n", result.Gaps));
        var alternatives = result.StateAlternatives!;
        Assert.Equal(2, alternatives.Count);
        Assert.Single(alternatives, alternative => alternative.Completion?.Entry is not null);
        // The early return abandons the symbolic enumeration on the closing path.
        var abandoned = Assert.Single(alternatives, alternative => alternative.Completion?.Entry is null);
        Assert.Equal("normal-completion-without-entry-evidence", abandoned.Outcome);
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Caught_throw_keeps_catch_continuation_unexecuted_and_refuses(string language)
    {
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var result = Extract(bodies, "WholeProtocolStoredException");
        Assert.Equal("lifecycle-throw-catch-continuation-unmodeled", Assert.Single(result.Gaps));
        Assert.Null(result.StateAlternatives); Assert.Null(result.Entry);
        Assert.NotNull(result.OpcodeFailure);
        Assert.True(result.OpcodeFailure!.Opcode is "throw");
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Two_unbound_reads_compared_to_each_other_refuse(string language)
    {
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var result = Extract(bodies, "WholeProtocolStateComparedTwice");
        Assert.StartsWith("lifecycle-equality-unresolved:connection-state-unbound:connection-state-unbound",
            Assert.Single(result.Gaps), StringComparison.Ordinal);
        Assert.Null(result.StateAlternatives);
        Assert.Equal("ceq", result.OpcodeFailure!.Opcode);
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Guard_count_beyond_the_path_budget_refuses_instead_of_guessing(string language)
    {
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var result = Extract(bodies, "WholeProtocolManyGuards");
        Assert.Equal("lifecycle-state-path-limit", Assert.Single(result.Gaps));
        Assert.Null(result.StateAlternatives);
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void State_control_requires_the_info_message_boundary_and_stays_deterministic(string language)
    {
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var root = bodies.Single(body => body.Observation.ObjectBody?.Name == "WholeProtocolProduce");
        var strict = IlSqlLifecycleExtractor.Extract(root, bodies, false, new(400_000));
        Assert.NotEmpty(strict.Gaps); Assert.Null(strict.StateAlternatives);
        var first = Extract(bodies, "WholeProtocolChoiceAttachment");
        var second = Extract(bodies, "WholeProtocolChoiceAttachment");
        Assert.Equal(SqlServerSchemaEvidence.Json(first), SqlServerSchemaEvidence.Json(second));
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Legacy_framework_profiles_fork_the_same_way(string language)
    {
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language, legacyFramework: true);
        var result = Extract(bodies, "WholeProtocolProduce");
        Assert.True(result.Gaps.Count == 0, string.Join("\n", result.Gaps));
        Assert.Equal(2, result.StateAlternatives!.Count);
        Assert.Contains("exceptional-termination", result.StateAlternatives.Select(alternative => alternative.Outcome));
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Alternatives_keep_decision_site_and_constant_evidence(string language)
    {
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var result = Extract(bodies, "WholeProtocolProduce");
        var decisions = result.StateAlternatives!.SelectMany(alternative => alternative.Decisions).ToArray();
        Assert.All(decisions, decision =>
        {
            Assert.Equal("1", decision.Constant); // ConnectionState.Open, never assumed observed
            Assert.Equal("not-equal-constant", decision.Comparison); // source guard is a not-equal shape
            Assert.Equal(0, decision.Occurrence);
            Assert.NotEmpty(decision.Connection);
            Assert.NotEqual(0, decision.Site.Offset);
        });
    }
}
