# Site TraceMap Tools Evidence Packet Examples Implementation State

Status: implemented
Readiness: ready-for-implementation
Public claim level: concept

## Current Branch

- Branch: `codex/impl-site-evidence-packet-examples`
- Worktree: `<local-worktree>` (absolute local path intentionally omitted to
  satisfy the `./scripts/check-private-paths.sh` private-path guard)
- Base: `origin/dev`
- PR target: `dev`

## Scope

Implementation of the public-safe evidence packet examples surface under
`site/src/packets/examples/`, route metadata, discovery metadata, adjacent
packet-page links, focused site validation, and spec bookkeeping.

Generated site output, scanner code, reducer code, raw scan artifacts, raw
SQLite, raw facts, analyzer logs, source snippets, SQL values, config values,
secrets, local paths, raw remotes, private sample names, and generated scan
directories remain out of scope.

## Claim-Level Decision

The public claim level is `concept` because the examples are teaching shapes.
All four examples are labeled `synthetic public-safe example`, including the
demo-backed shape. The implementation did not use checked-in demo artifacts as
real evidence for a stronger example-level claim; demo-backed remains a
coverage-shape label, not a public result claim.

## Placement State

Final placement: `/packets/examples/`.

Route collision check: no existing `site/src/packets/examples/` route existed
before implementation. The new standalone route is registered in sitemap
metadata and discovery metadata.

Rejected alternatives:

- `/examples/evidence-packets/`: rejected because it reads like a broad
  examples hub and could compete with `/examples/scan-packet/`.
- Section on `/packets/`: rejected because the four complete examples would
  lengthen the packet model page and blur model explanation with example
  details.
- Section on `/packets/assembly/`: rejected because assembly should remain the
  human workflow checklist rather than a packet gallery.

Navigation decision: no top-navigation change. Discovery uses adjacent links
from `/packets/` and `/packets/assembly/`, plus links from the new page to
`/packets/`, `/packets/assembly/`, `/examples/scan-packet/`,
`/demo/result/`, `/proof-source-catalog/`, and
`/review-claim-checklist/`.

Discovery metadata uses `hintCategory: use-case` because the current discovery
vocabulary does not support `example`. This is the spec-approved fallback; the
preferred proof path is `/packets/`.

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

Implementation validation passed.

Required implementation checks:

```bash
git diff --check
./scripts/check-private-paths.sh
cd site && npm test
cd site && npm run validate
cd site && npm run build
```

Results:

- `git diff --check`: passed with no output.
- `./scripts/check-private-paths.sh`: passed with
  `Private path guard passed.`
- `cd site && npm test`: passed, 359 tests.
- `cd site && npm run validate`: passed; generated output validation reported
  56 HTML files, 1873 internal references, 55 sitemap URLs, 1 legacy story
  safety target, and 13 legacy modernization evidence-map rows.
- `cd site && npm run build`: passed with generated output under `site/dist/`.
- Browser sanity at `http://localhost:4174/packets/examples/`: desktop check
  confirmed no body overflow, all four categories, all 12 schema rows, concept
  label, evidence principle, and no console errors. Mobile check at
  390x844 confirmed no body overflow, table contained in a horizontal scroll
  wrapper, stop marker present, boundary copy present, and no console errors.

Focused route validation added:

- `site/scripts/evidence-packet-examples.mjs`
- `site/scripts/evidence-packet-examples.test.mjs`
- aggregate registration in `site/scripts/validate.mjs`

The validator checks schema fields, four categories, synthetic labels, route
metadata, sitemap coverage, adjacent links, inbound links, word count
450-1300, allowed evidence tiers, stop blocked marker, forbidden claims, hard
private material, raw/private material outside sanctioned boundaries, and blame
language.

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

- Running `npm run validate` and `npm run build` concurrently raced over
  `site/dist/` and caused one transient missing-file validation failure. The
  commands were rerun separately; `npm run validate` passed after the race was
  removed, and `npm run build` also passed.
- The requested spec has both a spec-review starting state and a requirement
  to move readiness only after review findings are patched or dispositioned.
  The packet started at `Readiness: spec-review` and all spec headers were
  moved together after review and validation completed.
- Kiro review coverage is reduced because the review sandbox denied shell/tool
  execution. This is recorded as residual review coverage risk, but both
  requested models returned content reviews and no final Medium or higher
  findings remain.
- The required "demo-backed packet" example remains concept-level and is
  labeled as a synthetic public-safe example. A future checked-in demo-backed
  artifact may justify stronger wording only through a future spec and
  validation contract.

## Follow-Up Items

- PR loop pending at implementation-state update time. Record final PR-loop
  decision after the PR is opened and `agent-control pr-loop` completes.
- No known content or validation follow-ups remain before PR review.
