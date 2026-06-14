# Contract Delta Impact V2 Review Prompts

Branch:

```text
codex/spec-contract-delta-impact-v2
```

Issue:

```text
https://github.com/joefeser/tracemap/issues/24
```

Spec files:

- `.kiro/specs/contract-delta-impact-v2/requirements.md`
- `.kiro/specs/contract-delta-impact-v2/design.md`
- `.kiro/specs/contract-delta-impact-v2/tasks.md`

## Opus Review Prompt

Review the TraceMap `contract-delta-impact-v2` spec on branch `codex/spec-contract-delta-impact-v2` for merge readiness.

This spec extends the deterministic contract reducer. It is not a runtime impact engine and must not add LLM calls, embeddings, vector DBs, or prompt-based classification.

Please inspect:

- `.kiro/specs/contract-delta-impact-v2/requirements.md`
- `.kiro/specs/contract-delta-impact-v2/design.md`
- `.kiro/specs/contract-delta-impact-v2/tasks.md`
- existing `ContractDeltaReducer` code and tests
- existing combined report/path/reverse/diff/impact specs and code
- `docs/LANGUAGE_ADAPTER_CONTRACT.md`
- `docs/ACCEPTANCE.md`
- `rules/rule-catalog.yml`

TraceMap principles:

- No conclusion without evidence.
- No evidence without a rule ID.
- No rule without documented limitations.
- No scan without repo and commit SHA.
- Partial analysis is useful, but must be labeled partial.
- Do not add LLM calls, embeddings, vector DBs, or prompt-based classification.
- Do not display raw SQL, snippets, literal values, connection strings, raw URLs, or local absolute paths in public reports.

Review questions:

1. Is the v2 contract-delta input model expressive enough without becoming unbounded?
2. Does the spec preserve v1 reducer compatibility clearly?
3. Are single-index and combined-index behaviors separated enough?
4. Are matching semantics evidence-backed for type/property/method/endpoint/package/SQL/surface changes?
5. Are classifications and confidence mappings conservative enough, and are the single-index and combined-index classification vocabularies closed and unambiguous?
6. Are coverage, commit SHA, extractor version, rule ID, evidence tier, and file-span requirements complete?
7. Does optional path/reverse context avoid overclaiming runtime reachability, especially by refusing to seed traversal from name-only, syntax-only, ambiguous, generic, or high-fan-out matches?
8. Does the legacy v1 adapter avoid pretending flat legacy input is more structured than it is?
9. Are endpoint, package, SQL, and dependency-surface keys mapped to fields that actually exist in current indexes and combined reports?
10. Are output safety and deterministic JSON/Markdown requirements strong enough?
11. Are rule catalog expectations complete?
12. Are implementation slices reviewable?
13. What tests are missing?

Return:

- Blocking issues with file/section references.
- Important non-blocking issues.
- Suggested spec edits.
- Scope cuts or PR slicing changes.
- Missing tests.
- Whether the spec is ready to implement after fixes.

## Sonnet Review Prompt

Review the `contract-delta-impact-v2` spec on branch `codex/spec-contract-delta-impact-v2` as an implementation planner.

Focus on:

- Existing reducer code seams.
- Existing fact keys and indexes that can support each change kind.
- How to reuse combined report/path/reverse/diff/impact code.
- Minimal first PR boundary.
- Risky assumptions around package, SQL, endpoint, and dependency-surface matching.
- The legacy v1 adapter boundary and whether it preserves existing reducer behavior.
- The split between `ContractDeltaImpactSingleV2` and `ContractDeltaImpactCombinedV2` classification vocabularies.
- Whether `--include-paths` and `--include-reverse` are safely limited to combined indexes and stable selector evidence.
- JSON/Markdown determinism and redaction risks.
- Tests most likely to catch overclaiming.
- Validation commands likely to fail.

Return a concrete implementation plan, risky assumptions, and recommended first PR boundary.

## Qodo/Gemini Review Prompt

Review the `contract-delta-impact-v2` spec for correctness, safety, and maintainability.

Look for:

- Runtime impact overclaims.
- Evidence without rule IDs or documented limitations.
- Input model ambiguity.
- Generic-name false positives.
- Coverage gaps hidden as no-impact results.
- Unsafe raw SQL/snippet/config/path/URL rendering.
- Combined-index source identity mistakes.
- Path/reverse context false positives.
- Test gaps that could allow overclaiming.

Return actionable findings with exact section references and suggested fixes.
