using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class IlObjectBodyRetentionTests
{
    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Generic_value_refusal_does_not_erase_independently_agreed_object_metadata(string language)
    {
        var body = ScaleBody(language);
        var changed = body with { ValueFlow = new([], ["synthetic-value-refusal"]) };
        var agreed = Assert.Single(IlBodyEvidenceExtractor.AgreeValueFlows([body], [changed]));
        Assert.NotNull(agreed.ObjectBody);
        var receipt = Assert.IsType<IlObjectBodyAgreement>(agreed.ObjectBodyAgreement);
        Assert.True(receipt.RawBody);
        Assert.True(receipt.CallShapes);
        Assert.True(receipt.ObjectBody);
        Assert.False(receipt.ValueFlow);
        Assert.Contains("IlValueReaderDisagreementOrLimit", agreed.ValueFlow!.Gaps);
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Raw_call_shape_and_object_metadata_disagreements_still_withhold_objects(string language)
    {
        var body = ScaleBody(language);
        Assert.NotEmpty(body.Calls);
        foreach (var changed in new[] {
            body with { InstructionsSha256 = new string('0', 64) },
            body with { Calls = body.Calls.Select((call, index) => index == 0 ? call with { StackShape = null } : call).ToArray() },
            body with { ObjectBody = body.ObjectBody! with { Name = "synthetic-disagreement" } } })
        {
            var agreed = Assert.Single(IlBodyEvidenceExtractor.AgreeValueFlows([body], [changed]));
            Assert.Null(agreed.ObjectBody);
            Assert.Equal("unavailable", agreed.ObjectBodyRetention!.State);
            Assert.Contains(agreed.ObjectBodyRetention.Reason, new[] {
                "object-body-raw-disagreement", "object-body-call-shape-disagreement", "object-body-reader-disagreement" });
        }
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Scale_fixture_keeps_target_after_many_unrelated_field_owning_methods(string language)
    {
        // This fixture pins the PRODUCTION 1 MiB object-body budget: the noise bodies
        // consume it, the target stays retained, and the harness must not inflate it.
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language, objectBudgetChars: 1_048_576);
        Assert.True(bodies.Count(body => body.Observation.ObjectBody?.Name.StartsWith("Noise", StringComparison.Ordinal) == true) >= 160);
        var target = Assert.Single(bodies, body => body.Observation.ObjectBody?.Name == "RetainedScaleProducer");
        Assert.Equal("retained", target.Observation.ObjectBodyRetention!.State);
        Assert.True(target.Observation.ObjectBodyRetention.BudgetMaximum <= 1_048_576);
        var result = IlSqlLifecycleExtractor.Extract(target, bodies, false, new(400_000));
        Assert.Empty(result.Gaps);
        Assert.NotNull(result.Entry);
        Assert.Equal("dataset-return", result.Entry.Kind);
        Assert.Equal(4, Assert.Single(result.Entry.Commands).Parameters.Count);
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Exhaustion_preserves_raw_bodies_and_reports_the_exact_failed_metadata_stage(string language)
    {
        var template = IlSqlLifecycleExtractorTests.Bodies(language);
        var input = Assert.Single(template.Select(body => body.Input).Distinct(), input => input.Role == "end-to-end");
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "AGENTS.md"))) root = root.Parent;
        Assert.NotNull(root);
        var config = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var path = Path.Combine(root.FullName, "samples", "sql-compiled-wrapper-evidence", language, "end-to-end",
            "bin", config, "net48", $"PublicSql.{language}.end-to-end.dll");
        var bytes = File.ReadAllBytes(path);
        var left = IlBodyEvidenceExtractor.ReadCecilBodies(bytes, new(), new(400_000), CancellationToken.None,
            objectBudget: new(1));
        var right = IlBodyEvidenceExtractor.ReadSystemReflectionMetadataBodies(bytes, new(), new(400_000), CancellationToken.None,
            objectBudget: new(1));
        Assert.Empty(IlBodyEvidenceExtractor.CompareBodies(left, right));
        var target = Assert.Single(IlBodyEvidenceExtractor.AgreeValueFlows(left.Bodies, right.Bodies),
            body => body.MethodIdentity.Contains("RetainedScaleProducer", StringComparison.Ordinal));
        Assert.True(target.InstructionCount > 0);
        Assert.Null(target.ObjectBody);
        Assert.Equal("object-body-retention-budget", target.ObjectBodyRetention!.Reason);
        Assert.Equal(1, target.ObjectBodyAgreement!.Cecil!.BudgetMaximum);
        var body = new IlSqlObjectPathExtractor.Body(target, input);
        var result = IlSqlLifecycleExtractor.Extract(body, [body], false, new(400_000));
        Assert.Equal("lifecycle-body-unavailable", Assert.Single(result.Gaps));
        Assert.Equal("root-metadata", result.MetadataFailure!.Stage);
        Assert.Equal(target.BodyIdentity, result.MetadataFailure.Body);
        Assert.Equal("object-body-retention-budget", result.MetadataFailure.Retention!.Reason);
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void A_resolved_callee_without_object_metadata_reports_the_callee_instead_of_throwing(string language)
    {
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var root = Assert.Single(bodies, body => body.Observation.ObjectBody?.Name == "RetainedScaleProducer");
        var callee = Assert.Single(bodies, body => body.Observation.ObjectBody?.Name == "ProduceInputs");
        var removed = bodies.Select(body => body == callee ? body with
        {
            Observation = body.Observation with
            {
                ObjectBody = null,
                ObjectBodyRetention = new("unavailable", "object-body-retention-budget", 1, 2000, 1000, 1000),
                ObjectBodyAgreement = null
            }
        } : body).ToArray();
        var result = IlSqlLifecycleExtractor.Extract(root, removed, false, new(400_000));
        Assert.Equal("lifecycle-body-unavailable", Assert.Single(result.Gaps));
        Assert.Equal("callee-metadata", result.MetadataFailure!.Stage);
        Assert.Equal(callee.Observation.BodyIdentity, result.MetadataFailure.Body);
        Assert.Empty(result.Executions);
    }

    [Fact]
    public void Shape_caps_are_refused_before_materializing_metadata()
    {
        foreach (var (instructions, fields, reason) in new[] {
            (257, 0, "object-body-instruction-limit"), (1, 65, "object-body-field-limit") })
        {
            var budget = new IlBodyEvidenceExtractor.IlWorkBudget(1_048_576);
            var result = IlBodyEvidenceExtractor.RetainObjectBody(instructions, fields,
                () => throw new InvalidOperationException("must-not-materialize"), new(), budget, out var retention);
            Assert.Null(result);
            Assert.Equal(reason, retention.Reason);
            Assert.Equal(0, budget.Consumed);
        }
    }

    private static IlBodyObservation ScaleBody(string language)
        => Assert.Single(IlSqlLifecycleExtractorTests.Bodies(language), body => body.Observation.ObjectBody?.Name == "RetainedScaleProducer").Observation;
}
