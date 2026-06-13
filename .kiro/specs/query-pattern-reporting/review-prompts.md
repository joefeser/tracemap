# Query Pattern Reporting Review Prompts

## Opus Review Prompt

```text
Review the Kiro spec in `.kiro/specs/query-pattern-reporting/`.

This is a spec review, not implementation review.

Context:
- TraceMap now emits SQL-shape `QueryPatternDetected` facts from Python with `sqlSourceKind`, table/column metadata, and `queryShapeHash`.
- Existing reports are oriented around C#/TypeScript query-builder fields such as `filterFields`, `sortFields`, `selectFields`, `includeFields`, and `mutationFields`.
- The goal is to make human reports show useful SQL-shape evidence without changing fact schemas, reducer behavior, or runtime claims.

Focus areas:
- Is the split between SQL-shape and query-builder `QueryPatternDetected` facts clear?
- Does the spec avoid overclaiming runtime SQL execution, schema existence, dialect validity, or branch feasibility?
- Are .NET, TypeScript, and Python report-renderer responsibilities scoped correctly?
- Are the proposed report fields useful for open-source demo/readability without leaking raw SQL or literal values?
- Are tests and validation sufficient to prevent regressions in existing query-builder reporting?
- Are there any missing docs, rule-catalog, language-adapter-contract, or validation updates?
- Should implementation include all three report renderers in one PR, or should it be split?

Please identify merge-blocking spec issues separately from nice-to-have polish.
```

## Secondary Sonnet Review Prompt

```text
Review `.kiro/specs/query-pattern-reporting/` for implementability.

Focus on task ordering, test coverage, places where wording is ambiguous, and whether a coding agent could implement the spec without expanding scope.
```
