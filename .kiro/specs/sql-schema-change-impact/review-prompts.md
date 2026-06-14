# SQL Schema Change Impact Review Prompts

Branch:

```text
codex/spec-sql-schema-change-impact
```

Spec files:

- `.kiro/specs/sql-schema-change-impact/requirements.md`
- `.kiro/specs/sql-schema-change-impact/design.md`
- `.kiro/specs/sql-schema-change-impact/tasks.md`

## Opus Review Prompt

Review the TraceMap `sql-schema-change-impact` spec on branch `codex/spec-sql-schema-change-impact` for merge readiness.

This spec is about deterministic static SQL/schema change impact. It is not a database connector, migration executor, SQL parser, runtime telemetry feature, or AI analysis feature.

Please inspect:

- `.kiro/specs/sql-schema-change-impact/requirements.md`
- `.kiro/specs/sql-schema-change-impact/design.md`
- `.kiro/specs/sql-schema-change-impact/tasks.md`
- `.kiro/specs/sql-dependency-surfaces/*`
- `.kiro/specs/contract-delta-impact-v2/*`
- `.kiro/specs/combined-change-impact/*`
- `.kiro/specs/reverse-impact-query/*`
- `docs/LANGUAGE_ADAPTER_CONTRACT.md`
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

1. Is the spec clear that SQL/schema impact is a SQL-specific delta adapter for contract-delta impact rather than a competing engine?
2. Does it correctly distinguish SQL query-shape evidence, hash-only SQL text evidence, SQL resources, ORM/schema mapping evidence, static reachability evidence, and runtime execution claims?
3. Are schema-only, table-only, column-only, text-hash-only, mappedName-only, and mapping-only classifications conservative enough?
4. Are `sql-query` and `sql-persistence` used correctly, including default inclusion of persistence surfaces without implying query execution?
5. Are combined-index identity, reduced coverage, unlinked surface, hash-only, volatile identity, and schema-gap caveats strong enough?
6. Does optional path/reverse context reuse existing machinery safely?
7. Are `--contract-delta` and `--sql-schema-delta` mutual exclusion and output naming rules clear enough?
8. Are rule ID expectations complete while correctly defaulting to reused contract-delta rules and requiring missing catalog entries during implementation?
9. Are JSON/Markdown safety and determinism requirements complete?
10. Are the implementation slices reviewable, and what tests are missing?

Return:

- Blocking issues with file/section references.
- Important non-blocking issues.
- Suggested spec edits.
- Scope cuts or merge suggestions.
- Missing tests.
- Whether the spec is ready to implement after fixes.

## Sonnet Review Prompt

Review the `sql-schema-change-impact` spec on branch `codex/spec-sql-schema-change-impact` as an implementation planner.

Focus on:

- Existing code seams for SQL facts, combined surfaces, reverse, paths, diff, impact, and contract-delta v2.
- Whether the pinned CLI `tracemap reduce --index <index.sqlite> --sql-schema-delta <delta.json> --out <path>` fits the existing reducer shape.
- Whether the mutual exclusion behavior with `--contract-delta` is sufficient.
- How to implement this as a SQL-specific mode of existing reducer/impact commands.
- Minimal first implementation PR with high value and low blast radius.
- How to avoid duplicating SQL surface projection or graph traversal.
- Safe selector derivation for schema, table, column, query-shape, text-hash, and mapping deltas.
- Rule catalog strategy that reuses contract-delta rules unless dedicated SQL rules are necessary.
- Tests likely to catch overclaiming or unsafe output.
- Validation commands likely to fail.

Return:

- Concrete implementation plan.
- Risky assumptions.
- Recommended first PR boundary.
- Code/files likely affected.
- Missing tests.
- Whether the spec is implementable as written.

## Qodo/Gemini Review Prompt

Review the `sql-schema-change-impact` spec for correctness, safety, and maintainability.

Look for:

- Raw SQL or literal-value leakage risks.
- Runtime execution/schema/dialect/migration overclaims.
- Conflation between SQL query evidence and schema/mapping evidence.
- Missing reduced-coverage, identity, hash-only, unlinked-surface, schema-only, mappedName-only, or volatile identity caveats.
- Rule IDs or limitations that are missing.
- Stable identity dependence on row order, display names, local paths, or volatile fact IDs.
- Path/reverse false positives where unlinked SQL evidence is treated as reachable.
- Test gaps that could hide overclaiming.

Return actionable findings with exact section references and suggested fixes.
