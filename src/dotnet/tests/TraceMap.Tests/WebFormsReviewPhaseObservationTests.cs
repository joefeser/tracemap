using System.Text.Json;
using TraceMap.Cli;

namespace TraceMap.Tests;

public sealed class WebFormsReviewPhaseObservationTests
{
    [Fact]
    public void Observation_retains_constant_space_aggregate_and_idempotent_finish_without_claiming_exact_peak()
    {
        long reads = 0;
        using var observation = new WebFormsReviewPhaseObservation("scan", () => Interlocked.Increment(ref reads) * 10);
        observation.Sample(); observation.Sample();
        var usage = observation.Finish();
        var stopped = reads;
        observation.Sample();
        Assert.Same(usage, observation.Finish());
        Assert.Equal(stopped, reads);
        Assert.Equal(stopped, usage.SuccessfulMemorySamples);
        Assert.Equal(stopped * 10, usage.MaximumObservedWorkingSetBytes);
        Assert.True(usage.ElapsedMilliseconds >= 0);
        Assert.Equal("Tier4Unknown", usage.EvidenceTier); Assert.Equal("local-only", usage.Visibility);
        Assert.Equal(WebFormsReviewPhaseObservation.Rule, usage.RuleId);
        Assert.Contains(usage.Limitations, item => item.Contains("lower bound", StringComparison.Ordinal));
        Assert.Contains(usage.Limitations, item => item.Contains("child/adaptor processes are excluded", StringComparison.Ordinal));
        Assert.True(JsonSerializer.SerializeToUtf8Bytes(usage).Length < 2048);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0L)]
    [InlineData(-1L)]
    public void Unavailable_or_invalid_memory_samples_remain_unknown_not_zero(long? reading)
    {
        using var observation = new WebFormsReviewPhaseObservation("reports", () => reading);
        observation.Sample();
        var usage = observation.Finish();
        Assert.Equal(0, usage.SuccessfulMemorySamples);
        Assert.Null(usage.MaximumObservedWorkingSetBytes);
        Assert.True(usage.ElapsedMilliseconds >= 0);
    }

    [Fact]
    public void Unknown_phase_is_refused_before_sampling()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new WebFormsReviewPhaseObservation("preflight",
            () => throw new InvalidOperationException("must not sample")));
    }
}
