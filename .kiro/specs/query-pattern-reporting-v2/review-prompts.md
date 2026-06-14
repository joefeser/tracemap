# Query Pattern Reporting V2 Review Prompts

Branch:

```text
codex/query-pattern-reporting-v2-spec
```

Spec files:

- `.kiro/specs/query-pattern-reporting-v2/requirements.md`
- `.kiro/specs/query-pattern-reporting-v2/design.md`
- `.kiro/specs/query-pattern-reporting-v2/tasks.md`

## Opus Review Prompt

Review the TraceMap `query-pattern-reporting-v2` spec on branch `codex/query-pattern-reporting-v2-spec` for merge readiness.

This is a report-rendering spec, not an extractor spec. Focus on whether it safely finishes issue `#12` by improving scan `report.md` query-pattern sections for .NET, TypeScript, and JVM while preserving Python behavior.

Please inspect:

- `.kiro/specs/query-pattern-reporting-v2/requirements.md`
- `.kiro/specs/query-pattern-reporting-v2/design.md`
- `.kiro/specs/query-pattern-reporting-v2/tasks.md`
- Existing completed spec: `.kiro/specs/query-pattern-reporting/`
- `src/dotnet/TraceMap.Reporting/MarkdownReportWriter.cs`
- `src/typescript/src/reporting/MarkdownReportWriter.ts`
- `src/jvm/src/main/java/com/tracemap/jvm/reporting/MarkdownReportWriter.java`
- `src/python/tracemap_py/report.py`
- `rules/rule-catalog.yml`
- `docs/VALIDATION.md`

TraceMap principles:

- No conclusion without evidence.
- No evidence without a rule ID.
- No rule without documented limitations.
- Partial analysis is useful, but must be labeled partial.
- Do not add LLM calls, embeddings, vector DBs, or prompt-based classification.
- Do not display raw SQL, source snippets, literal values, connection strings, raw URLs, or local absolute paths in public reports.

Review questions:

1. Is the SQL-shape vs query-builder flavor split clear and implementable?
2. Is `sqlSourceKind` the right discriminator for SQL-shape rendering?
3. Does the spec preserve existing .NET/TypeScript query-builder behavior?
4. Does the spec avoid accidentally requiring new extraction behavior?
5. Does the JVM scope make sense given the current report writer?
6. Are Python expectations safe and minimal?
7. Are report limitations strong enough to avoid runtime SQL/schema overclaims?
8. Is the safe identifier policy strong enough to prevent raw SQL fragments or unsafe schema text from leaking through table/column fields?
9. Are rule catalog updates scoped correctly, or should new rule IDs be avoided as specified?
10. Are tests sufficient for the risk level?
11. Is the validation command list realistic for this repo?

Return:

- Blocking issues with file/section references.
- Important non-blocking issues.
- Suggested spec edits.
- Missing tests.
- Whether the spec is ready to implement after fixes.

## Sonnet Review Prompt

Review the `query-pattern-reporting-v2` spec on branch `codex/query-pattern-reporting-v2-spec` as an implementation planner.

Focus on:

- Smallest safe code changes in each report writer.
- Test placement for .NET, TypeScript, and JVM.
- Whether any existing report-writer helper can be reused or should stay local per adapter.
- Risks around property naming differences between adapters.
- Risks around Markdown escaping and safe path rendering.
- Whether limitation wording should be shared or adapter-local.
- Whether safe identifier rendering should be shared or adapter-local.
- Validation commands and likely failure points.

Return a concrete implementation plan, risky assumptions, and suggested PR boundaries.

## Qodo/Gemini Review Prompt

Review the `query-pattern-reporting-v2` spec for correctness, safety, and maintainability.

Look for:

- Raw SQL or private-data leakage risks.
- Unsafe table/column identifier rendering risks.
- Runtime SQL execution or schema-validity overclaims.
- Any requirement that silently changes extraction behavior.
- Any report row without evidence tier, span, or rule-backed source evidence.
- Any missing rule catalog limitation.
- Any nondeterministic output risk.
- Any regression risk for existing C#/TypeScript query-builder reports.
- Any JVM behavior that is too vague to test.
- Missing tests for raw SQL suppression and hash rendering.

Return actionable findings with exact section references and suggested fixes.
