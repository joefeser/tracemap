# Review Prompts

## Primary Review Prompt

Review the `ui-field-property-lineage-terminal-context` Kiro spec for merge
readiness. This is a spec-only branch. Do not edit files.

Focus on:

- whether the spec is correctly scoped to backend terminal context after PR
  #376 rather than reopening root extraction or route-flow schema gaps;
- whether validation/read/write/mapping/service/query/data/dependency terminal
  context can attach only when existing facts expose a selected-property trail;
- whether broad endpoint reachability, route reachability, same method, same
  class, same file, and same property name are explicitly negative cases;
- whether new emitted gap or rule IDs are rule-catalog-first with documented
  limitations;
- whether public claim level is hidden and non-claims exclude runtime behavior,
  production execution, browser visibility, authorization, database execution,
  impact proof, complete coverage, and AI/LLM analysis;
- whether PR 1 is small enough for a focused .NET implementation slice;
- whether validation follows AGENTS.md and `docs/VALIDATION.md`.

Report findings first, severity ordered. Call out Medium+ actionable issues
that must be patched before PR.

## Re-Review Prompt

Re-review the same spec after patches. Focus on whether previous Medium+
findings are resolved, whether any new blocker was introduced, and whether
`implementation-state.md` can honestly be marked `ready-for-implementation`.
