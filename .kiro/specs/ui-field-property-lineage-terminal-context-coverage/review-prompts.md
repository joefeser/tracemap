# Review Prompts

## Primary Review Prompt

Review the `ui-field-property-lineage-terminal-context-coverage` Kiro spec for
merge readiness. This is a spec-only branch. Do not edit files.

Focus on:

- whether the spec correctly follows PR #400 and stays focused on
  property-flow terminal-context coverage/vocabulary hardening;
- whether every current `CombinedTerminalSurfaceKinds` value must receive a
  mapped, suppressed, or deferred property-flow decision;
- whether exact selected-property bridge requirements are concrete enough for
  implementation tests;
- whether method, endpoint/route, class, file, same-name, generic-name, and
  broad dependency proximity are explicit negative cases;
- whether rule-catalog and report-version decisions are required before new
  emitted artifacts or broader terminal-context families;
- whether public claim level is hidden and non-claims exclude runtime
  behavior, database execution, dependency execution, impact proof, complete
  coverage, and AI/LLM analysis;
- whether PR slices are small enough for implementation without scanner,
  Swift, site, or reducer work.

Report findings first, severity ordered. Call out Medium+ actionable issues
that must be patched before PR.

## Re-Review Prompt

Re-review the same spec after patches. Focus on whether previous Medium+
findings are resolved, whether any new blocker was introduced, and whether
`implementation-state.md` can honestly be marked `ready-for-implementation`.
