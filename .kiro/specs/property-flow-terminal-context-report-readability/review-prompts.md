# Review Prompts

## Primary Review Prompt

Review the `property-flow-terminal-context-report-readability` Kiro spec for
merge readiness. This is a spec-only branch. Do not edit files.

Focus on:

- whether the spec is implementation-ready for optional property-flow report
  readability and documentation closure;
- whether it clearly avoids active docs-export implementation and
  vault-local-navigation implementation scope;
- whether it preserves hidden public claim level and static-only,
  path-scoped terminal-context semantics;
- whether it prevents runtime, database execution, dependency execution,
  impact, complete coverage, release-safety, public/demo, LLM, embedding,
  vector, or answer-generation claims;
- whether it requires structured `terminalContextKind` as the primary source
  and prevents inference from note prose, proximity, or downstream consumer
  metadata;
- whether rule IDs, evidence tiers, commit SHAs, extractor versions,
  supporting IDs, line spans, coverage labels, limitations, and partial labels
  remain preserved;
- whether the implementation tasks are small, deterministic, testable, and
  non-overlapping with terminal-context-consumers PR 1 docs-export work or
  vault-local-navigation work;
- whether validation follows AGENTS.md and `docs/VALIDATION.md`.

Report findings first, severity ordered. Call out Medium+ actionable issues
that must be patched before PR.

## Re-Review Prompt

Re-review the same spec after patches. Focus on whether previous Medium+
findings are resolved, whether any new blocker was introduced, and whether
`implementation-state.md` can honestly be marked `ready-for-implementation`.
