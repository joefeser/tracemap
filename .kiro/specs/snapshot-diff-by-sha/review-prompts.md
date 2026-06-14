# Snapshot Diff By Commit SHA Review Prompts

Branch:

```text
codex/spec-snapshot-diff-by-sha
```

Spec files:

- `.kiro/specs/snapshot-diff-by-sha/requirements.md`
- `.kiro/specs/snapshot-diff-by-sha/design.md`
- `.kiro/specs/snapshot-diff-by-sha/tasks.md`

## Opus Review Prompt

Review the TraceMap `snapshot-diff-by-sha` spec on branch `codex/spec-snapshot-diff-by-sha` for merge readiness.

This spec defines a deterministic command over existing TraceMap index artifacts. It must not introduce Git checkout orchestration, source scanning, runtime behavior, source-code semantic diffing, LLM calls, embeddings, vector databases, or prompt-based classification.

Please inspect:

- `.kiro/specs/snapshot-diff-by-sha/requirements.md`
- `.kiro/specs/snapshot-diff-by-sha/design.md`
- `.kiro/specs/snapshot-diff-by-sha/tasks.md`
- existing combined diff and impact specs/code
- current scan manifest/source identity model
- current combined index schema and source metadata
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
- Do not display raw SQL, snippets, literal values, connection strings, raw URLs, local absolute paths, or private repo names in public reports.

Review questions:

1. Is `snapshot-diff` distinct enough from existing `tracemap diff`, or should it be an option/extension instead of a command?
2. Are source identity and commit SHA validation rules strict enough?
3. Does single-index support make sense, and is the mixed single/combined rejection correct for v1?
4. Are coverage downgrade rules sufficient to prevent false added/removed claims?
5. Are stable key rules safe and deterministic across endpoint, DTO, surface, graph, and gap evidence?
6. Does the spec preserve existing combined diff semantics instead of duplicating them?
7. Are JSON/Markdown output contracts deterministic and safe?
8. Are rule IDs and limitations complete enough?
9. Are selectors and caps implementable with current CLI/reporting patterns?
10. Are the implementation slices reviewable?
11. What tests are missing?

Return:

- Blocking issues with file/section references.
- Important non-blocking issues.
- Suggested spec edits.
- Scope cuts or PR slicing changes.
- Missing tests.
- Whether the spec is ready to implement after fixes.

## Sonnet Review Prompt

Review the `snapshot-diff-by-sha` spec on branch `codex/spec-snapshot-diff-by-sha` as an implementation planner.

Focus on:

- current code seams in `TraceMap.Cli` and `TraceMap.Reporting`;
- how much can reuse `CombinedDependencyDiffer`;
- what a single-index projector would need;
- source identity and commit SHA metadata availability;
- schema validation risks;
- stable key implementation risks;
- redaction/safety risks;
- smallest valuable first implementation PR;
- tests likely to fail or require fixture builders.

Return:

- concrete implementation plan;
- risky assumptions;
- recommended first PR boundary;
- missing tests;
- validation commands.

## Qodo/Gemini Review Prompt

Review the `snapshot-diff-by-sha` spec for correctness, safety, and maintainability.

Look for:

- overclaiming around commit SHAs, source history, runtime behavior, or source-code semantic meaning;
- false added/removed results under reduced coverage;
- unsafe identity pairing or `--allow-identity-mismatch` semantics;
- stable keys depending on volatile row IDs, local paths, raw snippets, raw SQL, raw URLs, or display names;
- JSON nondeterminism;
- missing rule IDs or limitations;
- missing tests for redaction, identity conflicts, same SHA anomalies, row ID churn, malformed metadata, byte stability, and read-only inputs.

Return actionable findings with exact section references and suggested fixes.
