using System.Text.Json;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class CompiledAdmissionWorkUsageTests
{
    [Theory]
    [InlineData("metadata", CompiledAdmissionWorkUsage.MetadataScope)]
    [InlineData("il-body", CompiledAdmissionWorkUsage.IlScope)]
    public void Usage_pins_generator_input_scope_and_exact_logical_accounting(string phase, string scope)
    {
        var usage = CompiledAdmissionWorkUsage.Create(phase, new string('a', 64), new string('b', 64), 12, 2, 20);
        Assert.Equal(CompiledAdmissionWorkUsage.Rule, usage.RuleId);
        Assert.Equal("Tier2Structural", usage.EvidenceTier); Assert.Equal("local-only", usage.Visibility);
        Assert.Equal(scope, usage.WorkUnitsScope); Assert.Equal(12, usage.ConsumedWorkUnits);
        Assert.Equal(2, usage.RefusedAggregateRequests); Assert.Equal(20, usage.MaxWorkUnits);
        Assert.Equal(64, usage.BoundedInputSha256.Length);
        Assert.Contains(usage.Limitations, item => item.Contains("not CPU", StringComparison.Ordinal));
        Assert.Contains(usage.Limitations, item => item.Contains("Per-input caps", StringComparison.Ordinal));
        Assert.Contains(usage.Limitations, item => item.Contains("before an input later fails", StringComparison.Ordinal));
        Assert.Equal(JsonSerializer.Serialize(usage), JsonSerializer.Serialize(
            CompiledAdmissionWorkUsage.Create(phase, new string('a', 64), new string('b', 64), 12, 2, 20)));
        Assert.NotEqual(usage.BoundedInputSha256, CompiledAdmissionWorkUsage.Create(phase, new string('a', 64), new string('b', 64), 13, 2, 20).BoundedInputSha256);
    }

    [Theory]
    [InlineData("schema")]
    [InlineData("rule")]
    [InlineData("tier")]
    [InlineData("visibility")]
    [InlineData("generator")]
    [InlineData("input")]
    [InlineData("phase")]
    [InlineData("scope")]
    [InlineData("negative")]
    [InlineData("over-budget")]
    [InlineData("refusals")]
    [InlineData("maximum")]
    [InlineData("hash")]
    [InlineData("limitations")]
    public void Malformed_usage_is_not_projected_as_operational_evidence(string mutation)
    {
        var generator = new string('a', 64); var input = new string('b', 64);
        var usage = CompiledAdmissionWorkUsage.Create("metadata", generator, input, 12, 2, 20);
        usage = mutation switch
        {
            "schema" => usage with { SchemaVersion = "unknown" },
            "rule" => usage with { RuleId = "unknown" },
            "tier" => usage with { EvidenceTier = "Tier1Semantic" },
            "visibility" => usage with { Visibility = "shareable" },
            "generator" => usage with { GeneratorSha256 = new string('c', 64) },
            "input" => usage with { SourceBoundedInputSha256 = new string('c', 64) },
            "phase" => usage with { Phase = "il-body" },
            "scope" => usage with { WorkUnitsScope = "cpu-instructions" },
            "negative" => usage with { ConsumedWorkUnits = -1 },
            "over-budget" => usage with { ConsumedWorkUnits = 21 },
            "refusals" => usage with { RefusedAggregateRequests = -1 },
            "maximum" => usage with { MaxWorkUnits = 21 },
            "hash" => usage with { BoundedInputSha256 = new string('0', 64) },
            _ => usage with { Limitations = [] }
        };
        Assert.Equal("COMPILED_ADMISSION_USAGE_INVALID", Assert.Throws<InvalidDataException>(() =>
            CompiledAdmissionWorkUsage.Validate(usage, "metadata", generator, input, 20)).Message);
    }

    [Fact]
    public void Logical_budget_rejections_never_create_credits_or_overflow()
    {
        var budget = new IlBodyEvidenceExtractor.IlWorkBudget(10);
        Assert.True(budget.TryConsume(4)); Assert.True(budget.TryConsume(6));
        Assert.False(budget.TryConsume(1)); Assert.False(budget.TryConsume(-1));
        Assert.True(budget.TryConsume(0)); Assert.Equal(10, budget.Consumed); Assert.Equal(2, budget.RefusedRequests);
        var large = new IlBodyEvidenceExtractor.IlWorkBudget(long.MaxValue);
        Assert.True(large.TryConsume(long.MaxValue)); Assert.False(large.TryConsume(long.MaxValue));
        Assert.Equal(long.MaxValue, large.Consumed); Assert.Equal(1, large.RefusedRequests);
    }

    [Fact]
    public void Historical_absence_is_unknown_and_not_a_zero_measurement()
    {
        CompiledAdmissionWorkUsage.Validate(null, "metadata", new string('a', 64), new string('b', 64), 20);
        Assert.Equal(0, CompiledAdmissionWorkUsage.Create("metadata", new string('a', 64), new string('b', 64), 0, 0, 20).ConsumedWorkUnits);
        Assert.Throws<InvalidDataException>(() => CompiledAdmissionWorkUsage.Create("metadata", "invalid", new string('b', 64), 0, 0, 20));
    }
}
