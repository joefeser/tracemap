# Query Pattern Reporting Review Prompts

## Opus Review Prompt

```text
Review the revised Kiro spec in `.kiro/specs/query-pattern-reporting/`.

This is a spec review, not implementation review.

Context:
- TraceMap now emits SQL-shape `QueryPatternDetected` facts from Python with `sqlSourceKind`, table/column metadata, and `queryShapeHash`.
- Python is currently the only adapter that emits SQL-shape `QueryPatternDetected` facts.
- The revised spec intentionally scopes the first implementation PR to Python report rendering and defers .NET/TypeScript/JVM renderer work.
- The goal is to make Python `report.md` show useful SQL-shape evidence without changing fact schemas, reducer behavior, or runtime claims.

Focus areas:
- Does the revised spec correctly fix the prior over-scoping around .NET, TypeScript, and JVM?
- Is the SQL-shape discriminator clear: non-empty `sqlSourceKind`?
- Does the Python report plan avoid overclaiming runtime SQL execution, schema existence, dialect validity, generated SQL equivalence, or branch feasibility?
- Are the proposed report fields useful for open-source demo/readability without leaking raw SQL or literal values?
- Are ordering, row-limit, and placeholder behaviors implementable without scope creep?
- Are tests and validation concrete enough, especially `orders`, `id;status;total`, source kind, `queryShapeHash`, no raw SQL, and limitation text?
- Are docs/rule-catalog tasks sufficient?

Please identify merge-blocking spec issues separately from nice-to-have polish.
```

## Secondary Sonnet Review Prompt

```text
Review `.kiro/specs/query-pattern-reporting/` for implementability.

Focus on whether a coding agent can implement the Python-only first slice without expanding scope into .NET, TypeScript, JVM, row limits, SQL parsing, reducer behavior, or schema changes.
```
