using TraceMap.Access;

namespace TraceMap.Tests;

public sealed class AccessExpressionProjectorTests
{
    [Fact]
    public void Projects_nested_calculation_without_persisting_expression_text()
    {
        var fields = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["StartTimeChallengeYes"] = ["field-start-yes"],
            ["StartTimeChallengeNo"] = ["field-start-no"],
            ["ActivityChallengeYes"] = ["field-activity-yes"],
            ["ActivityChallengeNo"] = ["field-activity-no"]
        };

        var result = AccessExpressionProjector.Project(
            "=Abs([StartTimeChallengeYes] + [StartTimeChallengeNo] + [ActivityChallengeYes] + [ActivityChallengeNo])",
            null,
            fields);

        Assert.Equal("calculated-expression", result.Classification);
        Assert.Equal("complete", result.Coverage);
        Assert.Equal(4, result.FieldStableKeys.Count);
        Assert.NotEmpty(result.FunctionNameHashes);
        Assert.NotEmpty(result.OperatorNameHashes);
        Assert.DoesNotContain("StartTimeChallengeYes", result.StructureHash, StringComparison.Ordinal);
    }

    [Fact]
    public void Projects_domain_lookup_query_field_and_criteria_control()
    {
        var queryKey = "query-weekly-plan";
        var fields = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["WeeklyPlanID"] = ["field-weekly-plan"],
            ["StartDate"] = ["field-start-date"]
        };
        var objects = new Dictionary<string, IReadOnlyList<(string StableKey, string Kind)>>(StringComparer.OrdinalIgnoreCase)
        {
            ["qWeeklyPlans"] = [(queryKey, "query")]
        };

        var result = AccessExpressionProjector.Project(
            "=DLookUp([WeeklyPlanID], \"qWeeklyPlans\", \"[StartDate] = [txtPriorDate]\")",
            objects,
            fields,
            new HashSet<string>(["txtPriorDate"], StringComparer.OrdinalIgnoreCase));

        Assert.Equal("domain-lookup", result.Classification);
        Assert.Equal("complete", result.Coverage);
        Assert.Contains(queryKey, result.QueryStableKeys);
        Assert.Contains("field-weekly-plan", result.SelectedFieldStableKeys);
        Assert.Contains("field-start-date", result.CriteriaFieldStableKeys);
        Assert.Single(result.ControlReferenceHashes);
    }

    [Fact]
    public void Projects_date_offset_literals_and_preserves_dynamic_gap()
    {
        var fields = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["StartDate"] = ["field-start-date"]
        };
        var result = AccessExpressionProjector.Project("=DateAdd(\"d\", -14, [StartDate])", null, fields);
        Assert.Equal("calculated-expression", result.Classification);
        Assert.Equal("complete", result.Coverage);
        Assert.Contains("field-start-date", result.FieldStableKeys);
        Assert.Contains("string", result.LiteralKinds);
        Assert.Contains("number", result.LiteralKinds);

        var dynamic = AccessExpressionProjector.Project("=Eval([ExpressionText])", null, fields);
        Assert.Equal("partial", dynamic.Coverage);
        Assert.Equal("AccessBindingExpressionDynamic", dynamic.GapClassification);
    }
}
