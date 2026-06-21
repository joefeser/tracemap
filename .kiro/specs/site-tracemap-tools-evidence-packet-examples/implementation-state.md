# Site TraceMap Tools Evidence Packet Examples Implementation State

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

## Current Branch

- Branch: `codex/spec-site-evidence-packet-examples`
- Worktree: `<local-worktree>` (absolute local path intentionally omitted to
  satisfy the `./scripts/check-private-paths.sh` private-path guard)
- Base: `origin/dev`
- PR target: `dev`

## Scope

Spec-only packet for future public-safe evidence packet examples under
`.kiro/specs/site-tracemap-tools-evidence-packet-examples/`.

No site source, generated output, scanner code, reducer code, existing specs,
or validation scripts are in scope for this phase.

## Claim-Level Decision

The public claim level is `concept` because the required examples are teaching
shapes and may be synthetic only. A future implementation may give an
individual example the `demo-backed` coverage label only when it verifies
checked-in public demo artifacts and records the proof path. The page-level
and example-level public claim level remain concept unless a later spec
defines a stronger claim level and validation contract.

## Placement State

Placement is intentionally undecided until future implementation.

Candidate placements:

- `/packets/examples/`
- `/examples/evidence-packets/`
- section on `/packets/`
- section on `/packets/assembly/`

Recommended default for future implementation: `/packets/examples/`, because
it keeps the examples in the packet family while avoiding overlap with the
packet assembly workflow. This is not final; the future implementation must
record the final placement and rejected alternatives here before editing
`site/src`.

## Review State

Kiro spec reviews ran with both requested models. Every run reported reduced
coverage because Kiro denied shell/tool execution inside the review sandbox
(`ToolDenied`, `Tier4Unknown`, `ruleId: kiro.review.wrapper.v1`), but the
review artifacts were saved and read.

Required review commands used:

```bash
node scripts/kiro-review.mjs --phase site-tracemap-tools-evidence-packet-examples --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase site-tracemap-tools-evidence-packet-examples --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
```

Initial review artifacts:

- Opus initial spec review: wrapper process exited 1 due reduced coverage.
  Clean artifact:
  `.tmp/kiro-reviews/site-tracemap-tools-evidence-packet-examples/2026-06-21T204844-923Z-spec-claude-opus-4.8.clean.md`.
  Meta artifact:
  `.tmp/kiro-reviews/site-tracemap-tools-evidence-packet-examples/2026-06-21T204844-923Z-spec-claude-opus-4.8.meta.json`.
  Finding: High private local absolute path in `implementation-state.md`.
  Patch: replaced the absolute worktree path with `<local-worktree>` and a
  private-path guard note. Low demo-backed wording and word-count feasibility
  notes were also patched.
- Sonnet initial spec review: wrapper process exited 1 due reduced coverage.
  Clean artifact:
  `.tmp/kiro-reviews/site-tracemap-tools-evidence-packet-examples/2026-06-21T205153-979Z-spec-claude-sonnet-4.6.clean.md`.
  Meta artifact:
  `.tmp/kiro-reviews/site-tracemap-tools-evidence-packet-examples/2026-06-21T205153-979Z-spec-claude-sonnet-4.6.meta.json`.
  Finding: no Medium or higher findings after the private-path patch.

Re-review artifacts:

- Opus re-review 1: wrapper process exited 1 due reduced coverage.
  Clean artifact:
  `.tmp/kiro-reviews/site-tracemap-tools-evidence-packet-examples/2026-06-21T205436-792Z-re-review-claude-opus-4.8.clean.md`.
  Findings: Medium next-owner privacy ambiguity, stop-condition field-presence
  ambiguity, and word-count escape-hatch ambiguity. Patches: constrained next
  owner to public-safe roles/review processes, defined stop-condition blocked
  markers, and allowed a justified higher word-count bound when required
  fields cannot fit safely.
- Opus re-review 2: wrapper process exited 0 with reduced coverage message.
  Clean artifact:
  `.tmp/kiro-reviews/site-tracemap-tools-evidence-packet-examples/2026-06-21T205749-365Z-re-review-claude-opus-4.8.clean.md`.
  Findings: Medium undefined stronger claim-level escape hatch and Low/Medium
  blocked-marker scope ambiguity. Patches: locked every example to
  `Public claim level: concept`, moved demo-backed status to coverage label,
  scoped blocked markers to stop-condition packets only, and added route or
  anchor collision verification as a future implementation task.
- Opus final re-review: wrapper process exited 1 due reduced coverage.
  Clean artifact:
  `.tmp/kiro-reviews/site-tracemap-tools-evidence-packet-examples/2026-06-21T210117-960Z-re-review-claude-opus-4.8.clean.md`.
  Finding: no Medium or higher findings. Low notes about overview schema
  summary, discovery `hintCategory` fallback recording, and stable section
  anchors were patched.
- Sonnet final re-review: wrapper process exited 0 with reduced coverage
  message. Clean artifact:
  `.tmp/kiro-reviews/site-tracemap-tools-evidence-packet-examples/2026-06-21T210334-784Z-re-review-claude-sonnet-4.6.clean.md`.
  Finding: no Medium or higher findings. Low notes matched the final Opus Low
  set and were patched after review. No further re-review was run because the
  post-review edits addressed Low-only clarity items and did not change the
  Medium+ disposition.

Readiness moved to `ready-for-implementation` after all Medium or higher
findings were patched or dispositioned and spec-only validation passed.

## Validation State

Spec-only validation passed on 2026-06-21.

```bash
git diff --check
./scripts/check-private-paths.sh
rg -n "Status: not-started|Readiness: spec-review|Readiness: ready-for-implementation|Public claim level: concept|No public conclusion without evidence|synthetic public-safe example|demo-backed packet|reduced-coverage packet|gap-labeled packet|stop-condition packet|claim label|proof path|rule ID|evidence tier|coverage label|next owner|validation evidence|blocked marker|/packets/examples/|/examples/evidence-packets/|/examples/scan-packet/|/demo/result/|/proof-source-catalog/|/review-claim-checklist/|hintCategory|stable anchor|desktop.*mobile|word count" .kiro/specs/site-tracemap-tools-evidence-packet-examples
```

Results:

- `git diff --check`: passed with no output.
- `./scripts/check-private-paths.sh`: passed with
  `Private path guard passed.`
- Focused required-text check: passed; required labels, example categories,
  fields, placement alternatives, neighboring routes, blocked-marker wording,
  metadata fallback, stable-anchor wording, word-count wording, and browser
  sanity wording were present.
- Focused forbidden private/credential-like text check: only matched
  forbidden-list prose and found no actual private absolute path, raw
  credential, raw token, or private remote material in this spec directory.

## Oddities

- The requested spec has both a spec-review starting state and a requirement
  to move readiness only after review findings are patched or dispositioned.
  The packet started at `Readiness: spec-review` and all spec headers were
  moved together after review and validation completed.
- Kiro review coverage is reduced because the review sandbox denied shell/tool
  execution. This is recorded as residual review coverage risk, but both
  requested models returned content reviews and no final Medium or higher
  findings remain.
- The required "demo-backed packet" example remains concept-level. A checked-in
  public demo artifact may justify a `demo-backed` coverage label, not a
  stronger public claim level.

## Follow-Up Items

- Future implementation must choose final placement and rejected alternatives
  before editing `site/src`.
- Future implementation must verify selected route or section-anchor collision
  state.
- Future implementation must record the selected discovery `hintCategory` and
  justification if a fallback value is used.
- Future implementation should keep examples compact or record a justified
  higher word-count bound before validation is added.
