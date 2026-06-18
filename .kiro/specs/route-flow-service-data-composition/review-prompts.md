# Route Flow Service/Data Composition Review Prompts

Use these prompts to review the spec before implementation.

Spec files:

- `.kiro/specs/route-flow-service-data-composition/requirements.md`
- `.kiro/specs/route-flow-service-data-composition/design.md`
- `.kiro/specs/route-flow-service-data-composition/tasks.md`
- `.kiro/specs/route-flow-service-data-composition/implementation-state.md`

## Opus Review Prompt

Review this TraceMap spec for route-flow service/data composition.

Focus on:

- Whether the spec is narrowly scoped to issue 179 and avoids implementation.
- Whether `combined_argument_flows` and `combined_fact_symbols` projection rules
  are concrete enough for deterministic implementation.
- Whether controller-to-service/repository/data composition is conservative and
  evidence-backed.
- Whether interface-call-to-implementation candidate handling avoids runtime
  dependency injection or dynamic dispatch claims.
- Whether route entry to downstream method, object-shape, query-shape, and
  data-surface evidence has clear static bridge rules.
- Whether reduced-coverage and unknown-gap labels are applied wherever evidence
  is incomplete.
- Whether every conclusion has rule IDs, evidence tiers, supporting IDs,
  limitations, and deterministic ordering.
- Whether safe rendering rules are sufficient to prevent raw snippets, raw SQL,
  secrets, local absolute paths, private sample names, private route strings, or
  raw remotes from public artifacts.
- Whether the acceptance smoke direction is useful while remaining generic and
  private-local only.

Please return:

1. Blocking issues.
2. Medium or high-value changes before implementation.
3. Missing acceptance criteria.
4. Missing tests or validation.
5. Wording that overclaims runtime behavior or leaks private context.
6. Whether the spec is ready to merge after fixes.

## Sonnet Review Prompt

Review this spec as an implementation planner.

Focus on:

- Likely code ownership in the current .NET solution.
- Whether this should extend existing path reporting or introduce a
  route-flow-specific schema.
- Table/column assumptions around `combined_argument_flows`,
  `combined_fact_symbols`, symbol relationships, route facts, and data-surface
  facts.
- Risks in source-local symbol joins and fact-symbol projection.
- Risks in interface implementation candidate expansion and classification
  caps.
- Edge cases for reduced coverage, missing extractors, missing schema, high
  fan-out, and ambiguous candidate rows.
- Deterministic traversal and stable output ID risks.
- Privacy guard coverage for Markdown, JSON, logs, and display labels.
- A recommended first implementation PR boundary.

Please return concrete findings with file/section references, suggested fixes,
and any assumptions that should be documented before implementation.

## Qodo/Gemini Review Prompt

Review this spec for correctness, maintainability, and safety risks.

Look for:

- Overbroad scope or hidden scanner changes.
- False-positive route-to-data composition risks.
- Runtime DI, dynamic dispatch, route reachability, or SQL execution overclaims.
- Missing rule IDs, evidence tiers, limitations, or supporting IDs.
- Missing reduced-coverage or unknown-gap cases.
- Unsafe rendering of raw SQL, snippets, secrets, URLs, local paths, private
  routes, private repo names, or raw remotes.
- Non-deterministic ordering or stable ID risks.
- JSON schema instability.
- Unclear acceptance smoke or validation requirements.

Please provide actionable findings with exact spec sections and suggested
patches.
