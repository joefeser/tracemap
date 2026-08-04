using TraceMap.Access;

namespace TraceMap.Tests;

public sealed class AccessExpressionProjectorTests
{
    [Fact]
    public void Public_project_contract_retains_nine_parameter_binary_signature()
    {
        var overload = Assert.Single(
            typeof(AccessExpressionProjector).GetMethods(),
            method => method.Name == nameof(AccessExpressionProjector.Project));

        Assert.Equal(9, overload.GetParameters().Length);
    }

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
    public void Treats_access_in_predicate_as_an_operator_not_a_custom_function()
    {
        var fields = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["DOWText"] = ["field-dow-text"]
        };

        var result = AccessExpressionProjector.Project(
            "[DOWText] in('Friday','Saturday')",
            null,
            fields);

        Assert.Equal("calculated-expression", result.Classification);
        Assert.Equal("complete", result.Coverage);
        Assert.Equal("complete", result.RuntimeValueCoverage);
        Assert.Equal(["field-dow-text"], result.FieldStableKeys);
        Assert.Empty(result.FunctionNameHashes);
        Assert.Null(result.GapClassification);
    }

    [Fact]
    public void Keeps_bracketed_predicate_keyword_fields_and_malformed_predicates_partial()
    {
        var fields = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["In"] = ["field-in"],
            ["Exists"] = ["field-exists"],
            ["Status"] = ["field-status"]
        };

        var bracketed = AccessExpressionProjector.Project("=[In] + [Exists]", null, fields);
        var incompleteIn = AccessExpressionProjector.Project("=[Status] IN (", null, fields);
        var emptyExists = AccessExpressionProjector.Project("=EXISTS ()", null, fields);

        Assert.Equal("complete", bracketed.Coverage);
        Assert.Equal(["field-exists", "field-in"], bracketed.FieldStableKeys);
        Assert.Equal("partial", incompleteIn.Coverage);
        Assert.Equal("AccessBindingExpressionPartial", incompleteIn.GapClassification);
        Assert.Equal("partial", emptyExists.Coverage);
        Assert.Equal("AccessBindingExpressionPartial", emptyExists.GapClassification);
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
            new HashSet<string>(["txtPriorDate"], StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>>(StringComparer.Ordinal)
            {
                [queryKey] = fields
            });

        Assert.Equal("domain-lookup", result.Classification);
        Assert.Equal("complete", result.Coverage);
        Assert.Equal("partial", result.RuntimeValueCoverage);
        Assert.Contains(queryKey, result.QueryStableKeys);
        Assert.Contains("field-weekly-plan", result.SelectedFieldStableKeys);
        Assert.Contains("field-start-date", result.CriteriaFieldStableKeys);
        Assert.Single(result.ControlReferenceHashes);
    }

    [Fact]
    public void Finds_only_static_domain_selected_and_criteria_field_references()
    {
        var references = AccessExpressionProjector.FindStaticDomainFieldCandidates(
            "=DLookUp(\"[Percent]\", \"qWeekly\", \"[WeeklyPlanID]=[txtPlan] AND [StartDate]=[Forms]![Picker]![StartDate]\")");

        Assert.Equal(
            [
                new AccessExpressionProjector.AccessStaticDomainFieldCandidate("qWeekly", "Percent", "selected"),
                new AccessExpressionProjector.AccessStaticDomainFieldCandidate("qWeekly", "StartDate", "criteria"),
                new AccessExpressionProjector.AccessStaticDomainFieldCandidate("qWeekly", "txtPlan", "criteria"),
                new AccessExpressionProjector.AccessStaticDomainFieldCandidate("qWeekly", "WeeklyPlanID", "criteria")
            ],
            references);

        var dynamic = AccessExpressionProjector.FindStaticDomainFieldCandidates(
            "=DLookUp([Percent], [DomainControl], \"[WeeklyPlanID]=1\")");
        Assert.Empty(dynamic);

        var dynamicCriteria = AccessExpressionProjector.FindStaticDomainFieldCandidates(
            "=DLookUp(\"[Percent]\", \"qWeekly\", \"[WeeklyPlanID]=\" & [txtPlan])");
        Assert.Equal(
            [new AccessExpressionProjector.AccessStaticDomainFieldCandidate("qWeekly", "Percent", "selected")],
            dynamicCriteria);
    }

    [Fact]
    public void Malformed_bracketed_identifier_keeps_projection_partial()
    {
        const string expression = "=[unterminated + DLookUp(\"[Percent]\", \"qWeekly\")";

        Assert.Empty(AccessExpressionProjector.FindStaticDomainFieldCandidates(expression));
        var projected = AccessExpressionProjector.Project(expression, null, null);

        Assert.Equal("partial", projected.Coverage);
        Assert.Equal("AccessBindingExpressionPartial", projected.GapClassification);
    }

    [Fact]
    public void Resolves_domain_criteria_from_dependency_scope_without_exposing_it_as_a_return_field()
    {
        const string queryKey = "query-weekly";
        var objects = new Dictionary<string, IReadOnlyList<(string StableKey, string Kind)>>(StringComparer.OrdinalIgnoreCase)
        {
            ["qWeekly"] = [(queryKey, "query")]
        };
        var outputs = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Percent"] = ["output-percent"]
        };
        var criteria = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Percent"] = ["output-percent"],
            ["WeeklyPlanID"] = ["dependency-weekly-plan"]
        };

        var resolved = AccessExpressionProjector.ProjectWithDomainCriteriaFields(
            "=DLookUp([Percent], \"qWeekly\", \"[WeeklyPlanID]=[txtPlan]\")",
            objects,
            null,
            new HashSet<string>(["txtPlan"], StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>>(StringComparer.Ordinal)
            {
                [queryKey] = outputs
            },
            domainCriteriaFieldSetsByObject:
                new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>>(StringComparer.Ordinal)
                {
                    [queryKey] = criteria
                });

        Assert.Equal("complete", resolved.Coverage);
        Assert.Equal(["output-percent"], resolved.SelectedFieldStableKeys);
        Assert.Equal(["dependency-weekly-plan"], resolved.CriteriaFieldStableKeys);

        var returnFieldStillMissing = AccessExpressionProjector.ProjectWithDomainCriteriaFields(
            "=DLookUp([WeeklyPlanID], \"qWeekly\")",
            objects,
            null,
            fieldSetsByObject:
                new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>>(StringComparer.Ordinal)
                {
                    [queryKey] = outputs
                },
            domainCriteriaFieldSetsByObject:
                new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>>(StringComparer.Ordinal)
                {
                    [queryKey] = criteria
                });
        Assert.Equal("partial", returnFieldStillMissing.Coverage);
        Assert.Equal("AccessBindingDomainSelectedFieldDependencyOnly", returnFieldStillMissing.GapClassification);
        Assert.Empty(returnFieldStillMissing.SelectedFieldStableKeys);

        criteria["WeeklyPlanID"] = ["candidate-one", "candidate-two"];
        var ambiguousCriteria = AccessExpressionProjector.ProjectWithDomainCriteriaFields(
            "=DLookUp([Percent], \"qWeekly\", \"[WeeklyPlanID]=1\")",
            objects,
            null,
            fieldSetsByObject:
                new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>>(StringComparer.Ordinal)
                {
                    [queryKey] = outputs
                },
            domainCriteriaFieldSetsByObject:
                new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>>(StringComparer.Ordinal)
                {
                    [queryKey] = criteria
                });
        Assert.Equal("AccessBindingDomainCriteriaFieldAmbiguous", ambiguousCriteria.GapClassification);
    }

    [Fact]
    public void Reports_unmatched_domain_return_before_ambiguous_criteria()
    {
        const string queryKey = "query-weekly";
        var projected = AccessExpressionProjector.ProjectWithDomainCriteriaFields(
            "=DLookUp([MissingReturn], \"qWeekly\", \"[WeeklyPlanID]=1\")",
            new Dictionary<string, IReadOnlyList<(string StableKey, string Kind)>>(StringComparer.OrdinalIgnoreCase)
            {
                ["qWeekly"] = [(queryKey, "query")]
            },
            null,
            fieldSetsByObject:
                new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>>(StringComparer.Ordinal)
                {
                    [queryKey] = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
                },
            domainCriteriaFieldSetsByObject:
                new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>>(StringComparer.Ordinal)
                {
                    [queryKey] = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["WeeklyPlanID"] = ["candidate-one", "candidate-two"]
                    }
                });

        Assert.Equal("partial", projected.Coverage);
        Assert.Equal("AccessBindingDomainSelectedFieldUnmatched", projected.GapClassification);
        Assert.NotEmpty(projected.SelectedFieldReferenceHashes);
    }

    [Fact]
    public void Distinguishes_crosstab_pivot_and_dependency_only_domain_return_candidates()
    {
        const string queryKey = "query-weekly";
        var objects = new Dictionary<string, IReadOnlyList<(string StableKey, string Kind)>>(StringComparer.OrdinalIgnoreCase)
        {
            ["qWeekly"] = [(queryKey, "query")]
        };
        var pivotCandidate = AccessSafeValues.CrosstabPivotColumnCandidate("database-seed", queryKey, "4").StableKey;
        var pivot = AccessExpressionProjector.ProjectWithDomainCriteriaFields(
            "=DLookUp(\"[4]\", \"qWeekly\")",
            objects,
            null,
            fieldSetsByObject: new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>>(StringComparer.Ordinal)
            {
                [queryKey] = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["4"] = [pivotCandidate]
                }
            });

        Assert.Equal("partial", pivot.Coverage);
        Assert.Equal("AccessBindingDomainCrosstabPivotCandidate", pivot.GapClassification);
        Assert.Equal([pivotCandidate], pivot.SelectedFieldStableKeys);

        var mismatchCandidate = AccessSafeValues.CrosstabPivotPrefixMismatchCandidate(
            "database-seed", queryKey, "4").StableKey;
        var prefixMismatch = AccessExpressionProjector.ProjectWithDomainCriteriaFields(
            "=DLookUp(\"[4]\", \"qWeekly\")",
            objects,
            null,
            fieldSetsByObject: new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>>(StringComparer.Ordinal)
            {
                [queryKey] = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["4"] = [mismatchCandidate]
                }
            });
        Assert.Equal("partial", prefixMismatch.Coverage);
        Assert.Equal("AccessBindingDomainCrosstabPivotPrefixMismatch", prefixMismatch.GapClassification);
        Assert.Equal([mismatchCandidate], prefixMismatch.SelectedFieldStableKeys);

        var dependencyOnly = AccessExpressionProjector.ProjectWithDomainCriteriaFields(
            "=DLookUp(\"[WeeklyPlanID]\", \"qWeekly\")",
            objects,
            null,
            fieldSetsByObject: new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>>(StringComparer.Ordinal)
            {
                [queryKey] = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            },
            domainCriteriaFieldSetsByObject:
                new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>>(StringComparer.Ordinal)
                {
                    [queryKey] = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["WeeklyPlanID"] = ["dependency-weekly-plan"]
                    }
                });
        Assert.Equal("AccessBindingDomainSelectedFieldDependencyOnly", dependencyOnly.GapClassification);
        Assert.Empty(dependencyOnly.SelectedFieldStableKeys);

        var ambiguous = AccessExpressionProjector.ProjectWithDomainCriteriaFields(
            "=DLookUp(\"[WeeklyPlanID]\", \"qWeekly\")",
            objects,
            null,
            fieldSetsByObject: new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>>(StringComparer.Ordinal)
            {
                [queryKey] = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            },
            domainCriteriaFieldSetsByObject:
                new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>>(StringComparer.Ordinal)
                {
                    [queryKey] = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["WeeklyPlanID"] = ["dependency-one", "dependency-two"]
                    }
                });
        Assert.Equal("AccessBindingDomainSelectedFieldDependencyAmbiguous", ambiguous.GapClassification);
        Assert.Empty(ambiguous.SelectedFieldStableKeys);
    }

    [Fact]
    public void Mixed_domain_lookups_keep_per_lookup_outputs_and_prefer_unmatched_return_gaps()
    {
        const string pivotQuery = "query-pivot";
        const string otherQuery = "query-other";
        var pivotCandidate = AccessSafeValues.CrosstabPivotColumnCandidate(
            "database-seed", pivotQuery, "4").StableKey;
        var projected = AccessExpressionProjector.ProjectWithDomainCriteriaFields(
            "=DLookUp(\"[4]\", \"qPivot\") + DLookUp(\"[Missing]\", \"qOther\")",
            new Dictionary<string, IReadOnlyList<(string StableKey, string Kind)>>(StringComparer.OrdinalIgnoreCase)
            {
                ["qPivot"] = [(pivotQuery, "query")],
                ["qOther"] = [(otherQuery, "query")]
            },
            null,
            fieldSetsByObject: new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>>(StringComparer.Ordinal)
            {
                [pivotQuery] = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["4"] = [pivotCandidate]
                },
                [otherQuery] = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            });

        Assert.Equal("partial", projected.Coverage);
        Assert.Equal("AccessBindingDomainSelectedFieldUnmatched", projected.GapClassification);
        Assert.Equal([pivotCandidate], projected.SelectedFieldStableKeys);
        Assert.NotEmpty(projected.SelectedFieldReferenceHashes);
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

        var customFunction = AccessExpressionProjector.Project("=CustomLookup([StartDate])", null, fields);
        Assert.Equal("partial", customFunction.Coverage);
        Assert.Equal("AccessBindingExpressionFunctionUnresolved", customFunction.GapClassification);

        var zeroArgumentFunction = AccessExpressionProjector.Project("=Date", null, null);
        Assert.Equal("complete", zeroArgumentFunction.Coverage);
        Assert.Null(zeroArgumentFunction.GapClassification);

        var runtimeDate = AccessExpressionProjector.Project("=Date()", null, null);
        Assert.Equal("complete", runtimeDate.Coverage);
        Assert.Equal("partial", runtimeDate.RuntimeValueCoverage);

        var collidingFunction = AccessExpressionProjector.Project(
            "=CustomLookup([StartDate])",
            null,
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["CustomLookup"] = ["field-collision"],
                ["StartDate"] = ["field-start-date"]
            });
        Assert.Equal("AccessBindingExpressionFunctionUnresolved", collidingFunction.GapClassification);
        Assert.DoesNotContain("field-collision", collidingFunction.FieldStableKeys);
        Assert.Contains("field-start-date", collidingFunction.FieldStableKeys);
    }

    [Fact]
    public void Projects_declared_report_context_identifiers_without_treating_them_as_fields()
    {
        var report = AccessExpressionProjector.Project(
            "=\"Page \" & [Page] & \" of \" & [Pages]",
            null,
            null,
            contextIdentifierNames: new HashSet<string>(["Page", "Pages"], StringComparer.OrdinalIgnoreCase));

        Assert.Equal("complete", report.Coverage);
        Assert.Equal("partial", report.RuntimeValueCoverage);
        Assert.Null(report.GapClassification);
        Assert.Equal(2, report.ExternalReferenceHashes.Count);
        Assert.Empty(report.FieldStableKeys);

        var collidingReport = AccessExpressionProjector.Project(
            "=[Page]",
            null,
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Page"] = ["field-page"]
            },
            contextIdentifierNames: new HashSet<string>(["Page", "Pages"], StringComparer.OrdinalIgnoreCase));
        Assert.Equal("complete", collidingReport.Coverage);
        Assert.Single(collidingReport.ExternalReferenceHashes);
        Assert.Empty(collidingReport.FieldStableKeys);

        var unscoped = AccessExpressionProjector.Project(
            "=\"Page \" & [Page] & \" of \" & [Pages]",
            null,
            null);
        Assert.Equal("partial", unscoped.Coverage);
        Assert.Equal("AccessBindingExpressionPartial", unscoped.GapClassification);
    }

    [Fact]
    public void Resolves_declared_vba_function_as_static_input_without_claiming_runtime_value()
    {
        var result = AccessExpressionProjector.Project(
            "=[UserId] = glngUserID()",
            null,
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["UserId"] = ["field-user-id"]
            },
            vbaProcedureStableKeys: new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["glngUserID"] = ["vba-procedure-user-id"]
            });

        Assert.Equal("complete", result.Coverage);
        Assert.Equal("partial", result.RuntimeValueCoverage);
        Assert.Null(result.GapClassification);
        Assert.Equal(["vba-procedure-user-id"], result.VbaProcedureStableKeys);
        Assert.Equal(["field-user-id"], result.FieldStableKeys);
    }

    [Fact]
    public void Projects_multiple_domain_calls_and_operator_only_expressions()
    {
        var domainKey = "query-domain";
        var domainField = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["DisplayName"] = ["domain-display"],
            ["StatusId"] = ["domain-status"]
        };
        var objects = new Dictionary<string, IReadOnlyList<(string StableKey, string Kind)>>(StringComparer.OrdinalIgnoreCase)
        {
            ["qDomain"] = [(domainKey, "query")]
        };
        var fieldsByObject = new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>>(StringComparer.Ordinal)
        {
            [domainKey] = domainField
        };

        var domains = AccessExpressionProjector.Project(
            "=IIf([UsePrimary], DLookup([DisplayName], \"qDomain\", \"[StatusId] = [txtStatus]\"), DCount([StatusId], \"qDomain\"))",
            objects,
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["UsePrimary"] = ["surface-use-primary"]
            },
            new HashSet<string>(["txtStatus"], StringComparer.OrdinalIgnoreCase),
            fieldsByObject);

        Assert.Equal("domain-lookup", domains.Classification);
        Assert.Equal("complete", domains.Coverage);
        Assert.Equal("partial", domains.RuntimeValueCoverage);
        Assert.Single(domains.QueryStableKeys);
        Assert.Contains("domain-display", domains.SelectedFieldStableKeys);
        Assert.Contains("domain-status", domains.CriteriaFieldStableKeys);

        var calculated = AccessExpressionProjector.Project(
            "=[Price] * [Quantity]",
            null,
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Price"] = ["field-price"],
                ["Quantity"] = ["field-quantity"]
            });
        Assert.Equal("calculated-expression", calculated.Classification);
        Assert.Equal("complete", calculated.Coverage);
    }

    [Fact]
    public void Resolves_unique_same_surface_control_references_to_stable_keys()
    {
        var controls = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["MonActAcntPts"] = ["control-mon-accountability"],
            ["MonActAchPts"] = ["control-mon-achievement"]
        };

        var result = AccessExpressionProjector.Project(
            "=[MonActAcntPts]+[MonActAchPts]",
            null,
            null,
            new HashSet<string>(controls.Keys, StringComparer.OrdinalIgnoreCase),
            null,
            controls);

        Assert.Equal("complete", result.Coverage);
        Assert.Equal(
            ["control-mon-accountability", "control-mon-achievement"],
            result.ControlStableKeys);
        Assert.Empty(result.ControlReferenceHashes);
    }

    [Fact]
    public void Preserves_field_control_ambiguity_as_an_explicit_gap()
    {
        var result = AccessExpressionProjector.Project(
            "=[StartDate]",
            null,
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["StartDate"] = ["field-start-date"]
            },
            new HashSet<string>(["StartDate"], StringComparer.OrdinalIgnoreCase),
            null,
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["StartDate"] = ["control-start-date"]
            });

        Assert.Equal("partial", result.Coverage);
        Assert.Equal("AccessBindingExpressionTargetAmbiguous", result.GapClassification);
        Assert.Contains("field-start-date", result.FieldStableKeys);
        Assert.Contains("control-start-date", result.ControlStableKeys);
    }

    [Fact]
    public void Domain_lookup_retains_proven_fields_when_external_context_is_unresolved()
    {
        const string queryKey = "query-metrics";
        var objects = new Dictionary<string, IReadOnlyList<(string StableKey, string Kind)>>(StringComparer.OrdinalIgnoreCase)
        {
            ["qryMetrics"] = [(queryKey, "query")]
        };
        var queryFields = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["WeeklyPlanID"] = ["field-weekly-plan"],
            ["MetricValue"] = ["field-metric-value"]
        };

        var result = AccessExpressionProjector.Project(
            "=DLookUp([MetricValue], \"qryMetrics\", \"[WeeklyPlanID]=[TempVars]![PlanId]\")",
            objects,
            null,
            null,
            new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>>(StringComparer.Ordinal)
            {
                [queryKey] = queryFields
            });

        Assert.Equal("complete", result.Coverage);
        Assert.Null(result.GapClassification);
        Assert.Contains(queryKey, result.QueryStableKeys);
        Assert.Contains("field-metric-value", result.SelectedFieldStableKeys);
        Assert.Contains("field-weekly-plan", result.CriteriaFieldStableKeys);
        Assert.Single(result.ExternalReferenceHashes);
    }

    [Fact]
    public void Domain_fields_do_not_fall_back_to_the_owning_surface_scope()
    {
        const string queryKey = "query-domain-without-fields";
        var result = AccessExpressionProjector.Project(
            "=DLookUp([SharedName], \"qDomain\", \"[SharedName]=1\")",
            new Dictionary<string, IReadOnlyList<(string StableKey, string Kind)>>(StringComparer.OrdinalIgnoreCase)
            {
                ["qDomain"] = [(queryKey, "query")]
            },
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["SharedName"] = ["surface-field"]
            });

        Assert.Equal("partial", result.Coverage);
        Assert.Empty(result.SelectedFieldStableKeys);
        Assert.Empty(result.CriteriaFieldStableKeys);
        Assert.Single(result.SelectedFieldReferenceHashes);
        Assert.Single(result.CriteriaFieldReferenceHashes);
        Assert.Equal("AccessBindingDomainFieldCatalogIncomplete", result.GapClassification);
    }

    [Fact]
    public void Unresolved_domain_preserves_hash_only_field_roles_without_surface_fallback()
    {
        var result = AccessExpressionProjector.Project(
            "=DLookUp([Status], \"MissingQuery\", \"[Status]=[txtStatus]\")",
            new Dictionary<string, IReadOnlyList<(string StableKey, string Kind)>>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Status"] = ["surface-status"]
            },
            new HashSet<string>(["txtStatus"], StringComparer.OrdinalIgnoreCase),
            null,
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["txtStatus"] = ["control-status"]
            });

        Assert.Equal("partial", result.Coverage);
        Assert.Empty(result.SelectedFieldStableKeys);
        Assert.Empty(result.CriteriaFieldStableKeys);
        Assert.Single(result.SelectedFieldReferenceHashes);
        Assert.Contains("control-status", result.ControlStableKeys);
    }

    [Fact]
    public void Domain_criteria_field_control_collision_is_explicitly_ambiguous()
    {
        const string queryKey = "query-status";
        var result = AccessExpressionProjector.Project(
            "=DLookUp([Value], \"qStatus\", \"[Status]=1\")",
            new Dictionary<string, IReadOnlyList<(string StableKey, string Kind)>>(StringComparer.OrdinalIgnoreCase)
            {
                ["qStatus"] = [(queryKey, "query")]
            },
            null,
            new HashSet<string>(["Status"], StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>>(StringComparer.Ordinal)
            {
                [queryKey] = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Value"] = ["field-value"],
                    ["Status"] = ["field-status"]
                }
            },
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Status"] = ["control-status"]
            });

        Assert.Equal("partial", result.Coverage);
        Assert.Equal("AccessBindingExpressionTargetAmbiguous", result.GapClassification);
        Assert.Contains("field-status", result.CriteriaFieldStableKeys);
        Assert.Contains("control-status", result.ControlStableKeys);
    }

    [Fact]
    public void Domain_multi_candidate_fields_are_ambiguous_instead_of_missing()
    {
        const string queryKey = "query-duplicate-fields";
        var result = AccessExpressionProjector.Project(
            "=DLookUp([Value], \"qDuplicate\", \"[Status]=1\")",
            new Dictionary<string, IReadOnlyList<(string StableKey, string Kind)>>(StringComparer.OrdinalIgnoreCase)
            {
                ["qDuplicate"] = [(queryKey, "query")]
            },
            null,
            null,
            new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>>(StringComparer.Ordinal)
            {
                [queryKey] = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Value"] = ["field-value-1", "field-value-2"],
                    ["Status"] = ["field-status-1", "field-status-2"]
                }
            });

        Assert.Equal("partial", result.Coverage);
        Assert.Equal("AccessBindingDomainCriteriaFieldAmbiguous", result.GapClassification);
        Assert.Empty(result.SelectedFieldReferenceHashes);
        Assert.Empty(result.CriteriaFieldReferenceHashes);
    }

    [Fact]
    public void Projects_multiline_export_style_metrics_domain_lookup()
    {
        const string queryKey = "query-metrics-day";
        var result = AccessExpressionProjector.Project(
            "=DLookUp(\"[EngagementPoints] \",\"[qrptMetricsByDay]\",\"[WeeklyPlanID]=[TempVars]![TempPlanID]  AND [Dow]=1\")",
            new Dictionary<string, IReadOnlyList<(string StableKey, string Kind)>>(StringComparer.OrdinalIgnoreCase)
            {
                ["qrptMetricsByDay"] = [(queryKey, "query")]
            },
            null,
            null,
            new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>>(StringComparer.Ordinal)
            {
                [queryKey] = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["EngagementPoints"] = ["field-engagement"],
                    ["WeeklyPlanID"] = ["field-weekly-plan"],
                    ["Dow"] = ["field-day-of-week"]
                }
            });

        Assert.Equal("domain-lookup", result.Classification);
        Assert.Equal("complete", result.Coverage);
        Assert.Contains("field-engagement", result.SelectedFieldStableKeys);
        Assert.Equal(2, result.CriteriaFieldStableKeys.Count);
        Assert.Single(result.ExternalReferenceHashes);
    }

    [Fact]
    public void Projects_domain_count_wildcard_and_general_tempvars_reference()
    {
        const string queryKey = "query-actions";
        var objects = new Dictionary<string, IReadOnlyList<(string StableKey, string Kind)>>(StringComparer.OrdinalIgnoreCase)
        {
            ["qryActions"] = [(queryKey, "query")]
        };
        var fieldsByObject = new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<string>>>(StringComparer.Ordinal)
        {
            [queryKey] = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["WeeklyPlanID"] = ["field-weekly-plan"]
            }
        };

        var count = AccessExpressionProjector.Project(
            "=DCount(\"*\",\"qryActions\",\"[WeeklyPlanID]=[TempVars]![PlanId]\")",
            objects,
            null,
            null,
            fieldsByObject);
        Assert.Equal("complete", count.Coverage);
        Assert.Contains("wildcard", count.LiteralKinds);
        Assert.Contains("field-weekly-plan", count.CriteriaFieldStableKeys);

        var filter = AccessExpressionProjector.Project(
            "=[WeeklyPlanID]=[TempVars]![PlanId]",
            null,
            new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["WeeklyPlanID"] = ["field-weekly-plan"]
            });
        Assert.Equal("complete", filter.Coverage);
        Assert.Contains("field-weekly-plan", filter.FieldStableKeys);
        Assert.Single(filter.ExternalReferenceHashes);
    }
}
