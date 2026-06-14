# SQL Dependency Surfaces Review Prompts

Branch:

```text
codex/sql-dependency-surfaces-spec
```

Spec files:

- `.kiro/specs/sql-dependency-surfaces/requirements.md`
- `.kiro/specs/sql-dependency-surfaces/design.md`
- `.kiro/specs/sql-dependency-surfaces/tasks.md`

## Opus Review Prompt

Review the TraceMap `sql-dependency-surfaces` spec on branch `codex/sql-dependency-surfaces-spec` for merge readiness.

This spec is about making SQL a consistent cross-language dependency surface. It is not a full SQL parser, database connector, migration executor, or AI analysis feature.

Please inspect:

- `.kiro/specs/sql-dependency-surfaces/requirements.md`
- `.kiro/specs/sql-dependency-surfaces/design.md`
- `.kiro/specs/sql-dependency-surfaces/tasks.md`
- `docs/LANGUAGE_ADAPTER_CONTRACT.md`
- `docs/VALIDATION.md`
- `docs/ACCEPTANCE.md`
- `rules/rule-catalog.yml`
- Current SQL/query extraction in `.NET`, TypeScript, JVM, and Python
- Current combined SQL surface projection/path/reverse/diff/impact code

TraceMap principles:

- No conclusion without evidence.
- No evidence without a rule ID.
- No rule without documented limitations.
- No scan without repo and commit SHA.
- Partial analysis is useful, but must be labeled partial.
- Do not add LLM calls, embeddings, vector DBs, or prompt-based classification.
- Do not display raw SQL, snippets, literal values, connection strings, raw URLs, or local absolute paths in public reports.

Review questions:

1. Is the scope useful and implementable, or too broad for one spec?
2. Does the spec clearly distinguish `SqlTextUsed`, SQL-shape `QueryPatternDetected`, query-builder `QueryPatternDetected`, and `DatabaseColumnMapping`?
3. Does it avoid claiming runtime SQL execution, schema existence, dialect validity, or generated SQL equivalence?
4. Is the lightweight SQL-shape extraction conservative enough?
5. Is Python's existing normalized masked SQL text `queryShapeHash` behavior acceptable as the v1 cross-adapter contract?
6. Are the adapter-specific requirements realistic for current .NET, TypeScript, JVM, and Python code?
7. Are combined `sql-query` and `sql-persistence` display/grouping labels and diff/reverse identity keys safe and deterministic, especially preserving full-metadata identity while preferring shape hash for SQL query display?
8. Are path/reverse semantics correct for reachable vs unlinked SQL surfaces?
9. Are evidence tiers and rule catalog expectations complete, including `typescript.integration.sql.v1`?
10. Are safety rules strong enough to prevent raw SQL/private-data leakage?
11. Are the recommended PR slices reviewable?
12. What tests are missing?

Return:

- Blocking issues with file/section references.
- Important non-blocking issues.
- Suggested spec edits.
- Scope cuts or PR slicing changes.
- Missing tests.
- Whether the spec is ready to implement after fixes.

## Sonnet Review Prompt

Review the `sql-dependency-surfaces` spec on branch `codex/sql-dependency-surfaces-spec` as an implementation planner.

Focus on:

- Existing code seams for SQL extraction in each adapter.
- Whether a shared SQL-shape helper should be duplicated per language or specified via common golden fixtures.
- Whether Python-compatible golden fixtures are enough to keep `queryShapeHash` behavior aligned.
- Whether shape-hash-only `WITH`/CTE behavior now cleanly follows Python without overclaiming operation/table metadata.
- Minimal first PR with high value and low blast radius.
- How to avoid breaking existing query-builder facts.
- Combined surface display/grouping and full-identity risks.
- Path/reverse query linkage risks.
- Test fixture design for simple SQL shapes and dynamic boundaries.
- Validation commands likely to fail.

Return a concrete implementation plan, risky assumptions, and recommended first PR boundary.

## Qodo/Gemini Review Prompt

Review the `sql-dependency-surfaces` spec for correctness, safety, and maintainability.

Look for:

- Raw SQL or literal-value leakage risks.
- Unsafe identifier rendering or storage.
- Runtime execution/schema/dialect overclaims.
- `queryShapeHash` drift from the Python reference behavior.
- New evidence without rule IDs or documented limitations.
- Conflation between SQL text evidence, SQL-shape evidence, ORM mapping evidence, and query-builder evidence.
- Stable key dependence on volatile fact IDs, display names, row order, or local paths.
- Accidental identity weakening where same shape hash but different source kind collapses.
- Missing reduced-coverage caveats.
- Path/reverse false positives where unlinked SQL evidence is treated as reachable.
- Test gaps that could hide overclaiming.

Return actionable findings with exact section references and suggested fixes.
