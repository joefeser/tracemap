# Release Review Report Review Prompts

Branch:

```text
codex/spec-release-review-report
```

Spec files:

- `.kiro/specs/release-review-report/requirements.md`
- `.kiro/specs/release-review-report/design.md`
- `.kiro/specs/release-review-report/tasks.md`

## Opus Review Prompt

Review the TraceMap `release-review-report` spec on branch `codex/spec-release-review-report` for merge readiness.

This spec defines a deterministic release-oriented report that composes existing/future TraceMap evidence. It must not become a CI gate, approval engine, runtime risk predictor, or AI narrative generator.

Please inspect:

- `.kiro/specs/release-review-report/requirements.md`
- `.kiro/specs/release-review-report/design.md`
- `.kiro/specs/release-review-report/tasks.md`
- `.kiro/specs/combined-change-impact/*`
- `.kiro/specs/contract-delta-impact-v2/*`
- `.kiro/specs/api-dto-contract-diff/*`
- `.kiro/specs/sql-schema-change-impact/*`
- `.kiro/specs/combined-dependency-diff/*`
- `.kiro/specs/combined-dependency-paths/*`
- `.kiro/specs/reverse-impact-query/*`
- current CLI/reporting code where needed

TraceMap principles:

- No conclusion without evidence.
- No evidence without a rule ID.
- No rule without documented limitations.
- No scan without repo and commit SHA.
- Partial analysis is useful, but must be labeled partial.
- Do not add LLM calls, embeddings, vector DBs, or prompt-based classification.
- Do not display raw SQL, snippets, literal values, connection strings, raw URLs, or local absolute paths in public reports.

Review questions:

1. Is release review distinct enough from `diff`, `impact`, and `report`, while still reusing `combined-change-impact` instead of duplicating semantics?
2. Does it avoid release approval, CI gating, runtime risk, and generated conclusion overclaims?
3. Are unavailable/deferred workflow sections specified clearly enough?
4. Are source identity, commit SHA, and coverage caveats strong enough?
5. Is the rollup classification vocabulary and fixed precedence table safe and useful?
6. Are section ordering, JSON shape, and stable IDs deterministic?
7. Are path/reverse defaults and caps safe?
8. Are rule ID expectations complete?
9. Are the PR slices reviewable?
10. What tests are missing?

Return:

- Blocking issues with file/section references.
- Important non-blocking issues.
- Suggested spec edits.
- Scope cuts or PR slicing changes.
- Missing tests.
- Whether the spec is ready to implement after fixes.

## Sonnet Review Prompt

Review the `release-review-report` spec on branch `codex/spec-release-review-report` as an implementation planner.

Focus on:

- Existing CLI/reporting seams in the .NET implementation.
- Which existing readers/writers can be reused.
- How to represent unavailable/deferred workflows without creating fake evidence.
- Whether `combined-change-impact` is the right reuse boundary for release-review impact items.
- Whether package delta behavior is clearly deferred until a package-upgrade workflow exists.
- Minimal first PR with high value and low blast radius.
- Deterministic Markdown/JSON model design.
- Safe rendering helper reuse.
- Tests likely to catch overclaiming or byte churn.
- Validation commands likely to fail.

Return a concrete implementation plan, risky assumptions, recommended first PR boundary, and missing tests.

## Qodo/Gemini Review Prompt

Review the `release-review-report` spec for correctness, safety, and maintainability.

Look for:

- Runtime release approval or CI gate overclaims.
- LLM/generated narrative or hidden risk-score creep.
- Evidence without rule IDs.
- Missing unavailable/deferred section behavior.
- Missing single-index path/reverse unavailable behavior.
- Package-upgrade impact being implied before the workflow exists.
- Rollup/checklist severity mapping ambiguity.
- Raw SQL, snippets, config values, URLs, connection strings, local path leakage.
- Unstable IDs, row-order dependence, timestamps, or volatile metadata.
- Missing source identity or reduced-coverage caveats.
- Checklist items not tied to findings/gaps.
- Test gaps that could hide false confidence.

Return actionable findings with exact section references and suggested fixes.
