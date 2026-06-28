# Review Prompts

## Primary Review Prompt

Review the `ui-field-property-lineage-terminal-context-consumers` Kiro spec for
merge readiness. This is a spec-only branch. Do not edit files.

Focus on:

- whether the spec correctly follows PR #400 and consumes only the newly added
  property-flow terminal-context path notes and path-scoped
  `terminalContextKind` safe metadata;
- whether docs export, vault export, report rendering, rule catalog, and docs
  work are scoped as deterministic consumers of SQLite/facts/reports/rules,
  not new scanner or reducer behavior;
- whether terminal context remains path-scoped static evidence and cannot be
  promoted to endpoint/source/impact/runtime/database-execution claims;
- whether public claim level remains hidden unless a separate demo/concept
  justification is required and documented;
- whether rule IDs, evidence tiers, commit SHAs, extractor versions,
  supporting IDs, line spans, coverage labels, and limitations are preserved by
  consumer output;
- whether new emitted chunk families, graph node/edge kinds, gap codes,
  limitations, or redaction categories are rule-catalog-first;
- whether the PR slices are implementation-ready and small enough for docs
  export first, vault hidden/local second, and optional reporting polish third;
- whether validation follows AGENTS.md and `docs/VALIDATION.md`.

Report findings first, severity ordered. Call out Medium+ actionable issues
that must be patched before PR.

## Re-Review Prompt

Re-review the same spec after patches. Focus on whether previous Medium+
findings are resolved, whether any new blocker was introduced, and whether
`implementation-state.md` can honestly be marked `ready-for-implementation`.
