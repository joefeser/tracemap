# Implementation state — Phase 5
Branch `codex/sqlserver-internal-helpers`, worktree
`~/.codex/worktrees/sqlserver-internal-helpers/tracemap`, created from freshly fetched
`origin/dev` = `7169ee6e46252669bbe515d091a33b0c6a1f7429` (the #917 merge commit itself, so the
Phase 4 merge is an ancestor). Upstream tracking was removed so nothing can be pushed to `dev`
by accident. The user checkout and every other worktree were not touched.
## Scope decisions
- Distinct sibling state `sqlserver-internal-helper-result/1`; Phase 4 `sqlserver-helper-result/2`
  is unchanged and keeps internal helpers `helper-accessibility-unsupported`.
- Whole-compilation enumeration uses a per-compilation document scope registered by both
  adapters; without it the state is `helper-compilation-scope-unavailable`.
- The linked runtime matrix is not extended; it runs unchanged as a regression gate.
