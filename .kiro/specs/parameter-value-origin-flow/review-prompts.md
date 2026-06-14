# Parameter and Value-Origin Flow Review Prompts

Branch:

```text
codex/spec-parameter-value-origin-flow
```

Spec files:

- `.kiro/specs/parameter-value-origin-flow/requirements.md`
- `.kiro/specs/parameter-value-origin-flow/design.md`
- `.kiro/specs/parameter-value-origin-flow/tasks.md`

Issue:

- `https://github.com/joefeser/tracemap/issues/34`

## Opus Review Prompt

Review the TraceMap `parameter-value-origin-flow` spec on branch `codex/spec-parameter-value-origin-flow` for merge readiness.

This is a spec-only PR. It should only add files under `.kiro/specs/parameter-value-origin-flow/`.

Please inspect:

- `.kiro/specs/parameter-value-origin-flow/requirements.md`
- `.kiro/specs/parameter-value-origin-flow/design.md`
- `.kiro/specs/parameter-value-origin-flow/tasks.md`
- `docs/LANGUAGE_ADAPTER_CONTRACT.md`
- `docs/ACCEPTANCE.md`
- `rules/rule-catalog.yml`
- Existing flow-related code in .NET, TypeScript, JVM, and Python
- Existing combined report/path/reverse/diff/impact behavior

TraceMap principles:

- No conclusion without evidence.
- No evidence without a rule ID.
- No rule without documented limitations.
- No scan without repo and commit SHA.
- Partial analysis is useful, but must be labeled partial.
- Do not add LLM calls, embeddings, vector DBs, or prompt-based classification.
- Do not store source snippets by default.

Review questions:

1. Does the spec clearly avoid becoming a full taint-analysis or runtime-value inference feature?
2. Are argument-to-parameter, local alias, field alias, constructor-member, callback, async, and boundary semantics precise enough to implement?
3. Are the evidence tiers and downgrade rules safe?
4. Are mutation, aliasing, collection contents, branch feasibility, dynamic dispatch, DI, reflection, serializer, and async scheduling gaps explicit enough?
5. Does the spec fit the current shared fact/role/symbol contract?
6. Does it preserve deterministic output and public-report safety?
7. Are combined report/path/reverse/diff/impact expectations realistic?
8. Are the tasks sliceable and ordered correctly?
9. What tests are missing?
10. Is this ready to merge as a spec after fixes?

Return:

- Blocking issues with exact file/section references.
- Important non-blocking issues.
- Suggested spec edits.
- Missing tests.
- Whether the spec is ready to merge after fixes.

## Sonnet Review Prompt

Review the TraceMap `parameter-value-origin-flow` spec on branch `codex/spec-parameter-value-origin-flow` as an implementation planner.

Focus on:

- Existing code seams for .NET, TypeScript, JVM, and Python.
- Whether existing facts/rules can be reused or new `combined.flow.*.v1` rules are needed.
- The minimal first implementation PR with high value and low blast radius.
- Risky assumptions around constructor/member flow, callbacks, async, mutation, collection contents, and DI/reflection/dynamic dispatch.
- Storage/schema compatibility.
- Combined path/reverse/diff/impact integration risks.
- Tests and sample fixtures that should be built first.
- Validation commands likely to fail.

Return:

- Recommended first PR boundary.
- Concrete code seams by adapter/reporting layer.
- Risky assumptions.
- Missing tests.
- Suggested scope cuts.
- Whether implementation should proceed after spec fixes.
