# API and DTO Contract Diff Review Prompts

Branch:

```text
codex/spec-api-dto-contract-diff
```

Spec files:

- `.kiro/specs/api-dto-contract-diff/requirements.md`
- `.kiro/specs/api-dto-contract-diff/design.md`
- `.kiro/specs/api-dto-contract-diff/tasks.md`

## Opus Review Prompt

Review the TraceMap `api-dto-contract-diff` spec on branch `codex/spec-api-dto-contract-diff` for merge readiness.

This spec is about deterministic static comparison of indexed API and DTO contract evidence between two TraceMap indexes. It is not OpenAPI generation, source-code diffing, binary compatibility analysis, runtime traffic analysis, deployment reachability analysis, or AI classification.

Please inspect:

- `.kiro/specs/api-dto-contract-diff/requirements.md`
- `.kiro/specs/api-dto-contract-diff/design.md`
- `.kiro/specs/api-dto-contract-diff/tasks.md`
- Existing endpoint/DTO extraction facts in .NET, TypeScript, JVM, and Python where relevant
- Existing combined diff/report/path/impact specs and code where relevant
- `rules/rule-catalog.yml`
- `docs/LANGUAGE_ADAPTER_CONTRACT.md`

TraceMap principles:

- No conclusion without evidence.
- No evidence without a rule ID.
- No rule without documented limitations.
- No scan without repo and commit SHA.
- Partial analysis is useful, but must be labeled partial.
- Do not add LLM calls, embeddings, vector DBs, or prompt-based classification.
- Do not display raw snippets, raw SQL, config values, connection strings, raw URLs, or local absolute paths in public reports.

Review questions:

1. Is the spec distinct enough from combined dependency diff and contract delta impact v2?
2. Does it define endpoint, DTO type, DTO property, route shape, method, and request/response attachment identities safely?
3. Does it avoid OpenAPI completeness, runtime route, traffic, deployment, auth, serializer, or binary compatibility overclaims?
4. Are the single-index and combined-index modes clear and implementable?
5. Are source identity and coverage downgrade rules strong enough?
6. Are classifications closed, deterministic, and mapped to confidence without hidden scoring?
7. Are CLI selectors and output behavior consistent with existing TraceMap commands?
8. Are rule IDs and limitations complete enough for implementation?
9. Are safety/redaction constraints sufficient?
10. Are tasks sliced into reviewable PRs?
11. What tests are missing?

Return:

- Blocking issues with file/section references.
- Important non-blocking issues.
- Suggested spec edits.
- Scope cuts or PR slicing changes.
- Missing tests.
- Whether the spec is ready to implement after fixes.

## Sonnet Review Prompt

Review the `api-dto-contract-diff` spec on branch `codex/spec-api-dto-contract-diff` as an implementation planner.

Focus on:

- Existing endpoint/DTO fact seams in each adapter.
- Existing combined diff/report reader reuse.
- Minimal first implementation PR.
- Stable identity risks for endpoints, DTO types, DTO properties, route shapes, and request/response attachments.
- Where request/response attachments are currently missing and should be represented as gaps.
- Tests likely to fail or require fixture creation.
- How to avoid duplicating combined dependency diff logic.

Return:

- Concrete implementation plan.
- Risky assumptions.
- Recommended first PR boundary.
- Code modules likely affected.
- Test fixture plan.

## Qodo/Gemini Review Prompt

Review the `api-dto-contract-diff` spec for correctness, safety, and maintainability.

Look for:

- Runtime/OpenAPI/binary compatibility overclaims.
- Missing rule IDs or undocumented limitations.
- Stable identity dependence on volatile row IDs, display names, local paths, or unsafe values.
- Source identity and reduced-coverage gaps.
- Selector ambiguity.
- Raw snippet, URL, config, SQL, connection string, or local path leakage risks.
- Missing tests for generic properties, syntax-only evidence, duplicate identities, byte-stability, and unsafe rendering.

Return actionable findings with exact section references and suggested fixes.
