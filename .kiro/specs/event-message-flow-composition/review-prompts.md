# Event Message Flow Composition Review Prompts

Branch:

```text
codex/event-message-flow-composition-spec
```

Spec files:

- `.kiro/specs/event-message-flow-composition/requirements.md`
- `.kiro/specs/event-message-flow-composition/design.md`
- `.kiro/specs/event-message-flow-composition/tasks.md`
- `.kiro/specs/event-message-flow-composition/implementation-state.md`

## Opus Review Prompt

Review the TraceMap `event-message-flow-composition` spec for merge readiness.

Context:

- TraceMap is deterministic static analysis. No LLMs, embeddings, vector
  databases, prompt classification, broker execution, telemetry, or runtime
  tracing belong in the core scanner/reporter behavior.
- `event-message-surfaces` is implemented-v1-with-follow-ups. The first slice
  added .NET static message publisher, consumer, binding, gap, combined report,
  paths, reverse, and candidate-edge behavior. Remaining follow-ups include
  route-flow async boundaries, reducer context, Tier1 message extraction, and
  cross-language adapters.
- This new spec is hidden/local review context only. It must not claim runtime
  publish/subscribe delivery, production traffic, delivery guarantees, payload
  compatibility, impact proof, or complete coverage.
- PR 1 is intentionally small: shared event/message context/gap vocabulary plus
  one deterministic report/query consumer path, recommended as hidden
  `messageReviewContext` in `tracemap report`.

Please inspect:

- `.kiro/specs/event-message-flow-composition/requirements.md`
- `.kiro/specs/event-message-flow-composition/design.md`
- `.kiro/specs/event-message-flow-composition/tasks.md`
- `.kiro/specs/event-message-surfaces/implementation-state.md`
- `.kiro/specs/event-message-surfaces/design.md`
- `rules/rule-catalog.yml`
- Existing .NET message/report/query tests if needed for stale or duplicate
  scope.

Review questions:

1. Does the spec avoid runtime broker, delivery, topology, traffic, payload, or
   impact overclaims?
2. Is public claim level hidden stated strongly enough?
3. Is the PR 1 slice small and implementation-ready?
4. Are rule catalog obligations clear before new emitted rule IDs or gap
   strings?
5. Is the closed gap vocabulary sufficient and not duplicative of existing
   message surface gaps?
6. Are evidence provenance requirements complete: rule IDs, tiers, supporting
   IDs, commit SHA, extractor versions, file spans, coverage labels, and
   limitations?
7. Are safety rules strong enough to prevent raw payloads, secrets, config,
   connection strings, remotes, local paths, source snippets, raw broker URLs,
   hostnames, or unsafe destinations from rendering?
8. Does the spec accidentally require scanner/extractor work in PR 1?
9. Are deferred follow-ups explicit enough?
10. Are validation expectations realistic for a .NET/core implementation?

Return:

- Blocking issues with exact spec sections.
- Important non-blocking refinements.
- Suggested edits.
- Missing tests.
- Whether the spec is ready to implement after fixes.

## Sonnet Review Prompt

Review the `event-message-flow-composition` spec as an implementation planner.

Focus on:

- Whether hidden `messageReviewContext` in `tracemap report` is the right first
  consumer path, or whether another existing report/query path is smaller.
- How to add shared context/gap vocabulary without duplicating existing
  `message.surface.*` rules.
- Data model placement in the .NET combined report code.
- JSON/Markdown shape stability and empty-section behavior.
- Deterministic ordering, caps, and truncation gaps.
- How to preserve supporting fact IDs, edge IDs, rule IDs, evidence tiers,
  source labels, commit SHAs, extractor versions, and file spans.
- Risks around dynamic, hashed, unsafe, duplicate, ambiguous, name-only, and
  high fan-out message evidence.
- Focused tests that catch safety regressions and overclaims.
- Validation commands and likely failure points.

Return:

- Implementation plan.
- Risky assumptions.
- Suggested PR boundaries.
- Missing or oversized tasks.
- Any spec edits needed before implementation.

## Qodo/Gemini Review Prompt

Review the `event-message-flow-composition` spec for correctness, safety, and
maintainability.

Look for:

- Runtime delivery or impact overclaims.
- New gap strings or rule IDs without catalog-first limitations.
- Raw value leakage risks.
- Nondeterministic ordering or cap behavior.
- Claims that candidate edges are call edges or delivery proof.
- Missing reduced-coverage or partial-analysis behavior.
- PR 1 scope creep into scanners, reducers, route-flow, release-review, or
  cross-language adapters.
- Missing tests for one-sided evidence, binding-only evidence, candidate edges,
  dynamic/hashed destinations, truncation, empty output, and private-output
  safety.

Return actionable findings with exact section references and suggested fixes.
