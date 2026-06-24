# Route Flow Service/Data Composition Final Review Prompts

## Kiro Spec Review Prompt

Review the `route-flow-service-data-composition-final` Kiro spec for
merge-readiness. This is a spec-only branch; it must not implement product code.

Inspect:

- `.kiro/specs/route-flow-service-data-composition-final/requirements.md`
- `.kiro/specs/route-flow-service-data-composition-final/design.md`
- `.kiro/specs/route-flow-service-data-composition-final/tasks.md`
- `.kiro/specs/route-flow-service-data-composition-final/implementation-state.md`
- `.kiro/specs/route-flow-service-data-composition-final/review-prompts.md`

Context:

- This spec should build on, not duplicate:
  - `.kiro/specs/route-flow-service-data-composition-next/`
  - `.kiro/specs/route-centered-endpoint-trace-completeness/`
  - `.kiro/specs/route-centered-static-flow-report/`
  - `.kiro/specs/route-flow-endpoint-stitching/`
  - GitHub issues #159, #179, and #201.
- The next implementation target is still `tracemap route-flow`.
- The spec must stay static-only and must not claim runtime proof, production
  execution, runtime DI resolution, branch feasibility, database execution, or
  AI impact analysis.

Review questions:

- Does the spec identify a clear next implementation PR scope rather than a
  giant route-flow rewrite?
- Does it avoid duplicating already-completed specs while naming the remaining
  bridge/gap/downgrade work?
- Are every row/gap/output requirement tied to rule IDs, evidence tiers, file
  spans, supporting fact/edge IDs, coverage labels, classifications, and
  limitations?
- Is the CLI/report contract exact enough for deterministic JSON and Markdown
  implementation?
- Are safety requirements strong enough to prevent raw SQL, config, secrets,
  local paths, raw remotes, private labels, private routes, and source snippets
  from appearing in outputs, logs, fixtures, or review notes?
- Are validation commands and public-safe sample guidance specific enough?
- Are task checkboxes implementation-ready and reasonably sized?
- What findings, if any, should the author resolve and record in
  `implementation-state.md` before marking the spec
  `ready-for-implementation`?

Return findings first, severity ordered. Mark Medium+ findings that should be
patched before PR. Include suggested spec edits for actionable findings.
