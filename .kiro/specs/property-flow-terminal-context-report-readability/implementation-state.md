# Property Flow Terminal Context Report Readability Implementation State

Status: ready-for-implementation
Readiness: validated-spec-only
Spec branch: `codex/spec-property-flow-terminal-context-report-readability`
Target base: `dev`
Public claim level: hidden

## Current Context

Fetched `origin/dev` and created an isolated worktree/branch from the latest
target base before drafting.

Baseline:

```text
c37eff84
```

That commit is the `dev` merge for PR #405,
`[codex] Add Swift inventory project discovery spec`.

## Scope Decisions

- This is a spec-only PR.
- The future implementation slice is optional property-flow report
  readability and documentation closure after terminal-context coverage.
- Public claim level remains hidden.
- Hidden static-only semantics must be preserved.
- No scanner facts, reducer conclusions, impact claims, schema migrations,
  docs-export chunks, vault graph/navigation artifacts, public site copy, or
  public/demo claim promotion are in scope.
- Active docs-export implementation is intentionally excluded; that work
  remains governed by
  `.kiro/specs/ui-field-property-lineage-terminal-context-consumers/`.
- Active vault-local-navigation implementation is intentionally excluded; this
  spec must not add vault graph nodes, edges, backlinks, tags, or local
  navigation.
- Future product edits must first record `render`, `document-only`, or `defer`
  as the selected implementation decision in this file.
- Prefer no new rule IDs; reuse existing property-flow path/node/source rules
  unless a new emitted reporting artifact, gap, limitation, or validation
  finding requires catalog-first documentation.

## Source Material Reviewed

- `AGENTS.md`
- `.kiro/specs/ui-field-property-lineage-terminal-context-consumers/requirements.md`
- `.kiro/specs/ui-field-property-lineage-terminal-context-consumers/design.md`
- `.kiro/specs/ui-field-property-lineage-terminal-context-consumers/tasks.md`
- `.kiro/specs/ui-field-property-lineage-terminal-context-consumers/review-prompts.md`
- `.kiro/specs/ui-field-property-lineage-terminal-context-coverage/implementation-state.md`
- `.kiro/specs/evidence-export-usability-polish/tasks.md`
- `docs/VALIDATION.md`
- Repository search for `terminalContextKind`, docs-export, vault, reporting,
  and property-flow references.

## Review Log

Planned commands:

```bash
node scripts/kiro-review.mjs --phase property-flow-terminal-context-report-readability --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase property-flow-terminal-context-report-readability --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
```

Review results will be recorded here before commit. Medium+ actionable
findings must be patched before this spec is marked ready for implementation.

Initial Opus review:

- Command:
  `node scripts/kiro-review.mjs --phase property-flow-terminal-context-report-readability --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text`
- Result: completed with reduced coverage because Kiro reported denied tool
  access.
- Artifacts:
  `.tmp/kiro-reviews/property-flow-terminal-context-report-readability/2026-06-27T190045-336Z-spec-claude-opus-4.8.clean.md`
  and matching `.meta.json`.
- Findings patched: replaced incorrect `MarkdownReportWriter` guidance with
  `PropertyFlowReport.RenderMarkdown` and `PropertyFlowTests`; documented that
  `StaticTerminalContext:` notes already render as Markdown path-note bullets;
  clarified display-vs-classification handling for producer-generated notes;
  marked contradictory note-prose tests as synthetic/defensive; clarified that
  evidence identity preservation may be asserted in JSON/report data rather
  than duplicated in every Markdown cue; added note-ordering, full-output, and
  absent-key/gate test guidance.

Initial Sonnet review:

- Command:
  `node scripts/kiro-review.mjs --phase property-flow-terminal-context-report-readability --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`
- Result: completed with reduced coverage because Kiro reported denied tool
  access.
- Artifacts:
  `.tmp/kiro-reviews/property-flow-terminal-context-report-readability/2026-06-27T190045-366Z-spec-claude-sonnet-4.6.clean.md`
  and matching `.meta.json`.
- Findings patched: documented that `terminalContextKind` is present only when
  the selected-property bridge gate allows it; clarified absent key semantics
  for bridge-absent and HTTP-suppressed paths; documented the current Markdown
  baseline and the exact `render` versus `document-only` decision boundary;
  replaced misdirected Markdown writer validation with property-flow-focused
  validation; added docs-export coupling deferral guidance and private-path
  validation scope.

## Validation Log

Completed:

```bash
git diff --check
./scripts/check-private-paths.sh
```

Results:

- `git diff --check`: passed.
- `./scripts/check-private-paths.sh`: passed (`Private path guard passed.`).
- Diff scope: limited to
  `.kiro/specs/property-flow-terminal-context-report-readability/`.

Spec-only validation does not run product tests because this PR only adds a
Kiro spec folder and no product code. Future implementation PRs must run the
focused and full validation listed in `tasks.md` unless they record a narrower
reason.

## Follow-Up Items

- Implementation PR must choose and record `render`, `document-only`, or
  `defer` before product edits.
- Implementation PR must keep docs-export and vault-local-navigation work out
  of scope unless a separate spec explicitly authorizes those changes.
- Implementation PR must preserve report compatibility or version schema
  changes explicitly.
