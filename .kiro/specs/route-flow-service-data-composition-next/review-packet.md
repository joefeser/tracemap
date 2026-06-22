# Route Flow Service/Data Composition Next Review Packet

Review this TraceMap spec for merge readiness.

Spec folder:

- `.kiro/specs/route-flow-service-data-composition-next/requirements.md`
- `.kiro/specs/route-flow-service-data-composition-next/design.md`
- `.kiro/specs/route-flow-service-data-composition-next/tasks.md`
- `.kiro/specs/route-flow-service-data-composition-next/implementation-state.md`

Related context:

- GitHub issue #179
- `.kiro/specs/route-flow-service-data-composition/`
- `.kiro/specs/route-centered-static-flow-report/`
- `.kiro/specs/route-centered-endpoint-trace-completeness/`
- `src/dotnet/TraceMap.Reporting/CombinedRouteFlowReport.cs`
- `src/dotnet/tests/TraceMap.Tests/CombinedRouteFlowTests.cs`
- `rules/rule-catalog.yml`

Review questions:

1. Is the scope clearly a continuation over shipped route-flow service/data
   composition work rather than a duplicate rewrite?
2. Does the spec define an implementable next slice for service/data grouping,
   data/query/dependency context, value-origin context, downgrade behavior, and
   deterministic safe output?
3. Does it avoid runtime proof claims, DI resolution claims, branch feasibility
   claims, SQL execution claims, production traffic claims, release approval
   claims, and AI/LLM/vector/prompt classification?
4. Are rule ID, evidence tier, supporting ID, file/span, source label, coverage,
   and limitation requirements explicit enough?
5. Are redaction requirements strong enough for raw SQL, config values,
   endpoint URLs, private route strings, private sample names, local absolute
   paths, remotes, source snippets, generated local artifacts, connection
   strings, and secrets?
6. Are gap and downgrade requirements strong enough for reduced coverage,
   missing optional tables, old schemas, ambiguous matches, high fan-out,
   unknown commit SHA, stale generated code, unjoinable projection rows, and
   truncation?
7. Are the tasks sliced in a reviewable order?
8. Are validation requirements enough for a future product implementation?

Return:

- Blocking issues with exact file references.
- Important non-blocking issues.
- Suggested fixes.
- Whether the spec is ready for implementation after fixes.
