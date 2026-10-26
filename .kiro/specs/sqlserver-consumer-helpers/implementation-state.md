# Implementation state — Phase 4
Branch `codex/sqlserver-consumer-helpers`, worktree
`~/.codex/worktrees/sqlserver-consumer-helpers/tracemap`, created from freshly fetched
`origin/dev` = `834d74fe86e1a1e769c9d1be583317b09295e92a` (#916 merge; #915 `b55b2d107` is an
ancestor). Upstream tracking was removed so nothing can be pushed to `dev` by accident. The user
checkout (`codex/sqlserver-result-column-prompt`) and every other worktree were not touched.
## Scope decisions
- Evidence is a sibling property `sqlServerHelperResultState` on every Fill fact; one helper
  execution, one caller entry per call site, one binding per `(factId, callerIndex)`.
- Only `private` static helpers in single-declaration types are admitted, so every call site is
  in the Fill's document; other accessibilities stay `helper-accessibility-unsupported`.
- One optional exact `SqlConnection` parameter is admitted besides the `string` procedure
  parameter so the #916 runtime-linked fixtures can execute the same helper methods.
- The #916 Microsoft.Data.SqlClient fixture pair and disposable harness are reused and extended;
  no new provider foundation.
## Oddities noticed
(none yet)
