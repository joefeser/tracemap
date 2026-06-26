# Site TraceMap Tools Legacy .NET Evidence Lane Implementation State

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

## Branch

Spec branch: `codex/spec-site-legacy-dotnet-evidence-lane-20260625190237`

Base: `origin/dev`

Target PR base: `dev`

Scope: spec-only packet for a future public-site legacy .NET evidence lane.
Site source, generated output, scanner code, reducer code, validators, and
unrelated specs are out of scope for this phase.

Worktree: dedicated isolated spec worktree; local absolute path intentionally
omitted from checked-in spec notes.

## Current State

The packet completed Kiro spec review and is ready for future implementation.
All future site implementation tasks remain unchecked because this phase does
not implement site code.

Current readiness is `ready-for-implementation`. Opus, Sonnet, and one bounded
post-patch re-review completed with full coverage. Medium or higher findings
were patched, and the final packet still satisfies the spec-only scope.

## Claim-Level Decision

Selected public claim level: `concept`.

Rationale: this packet defines a future evidence-status lane and reviewer
matrix. It does not publish new scanner behavior, reducer findings, public demo
proof, runtime telemetry, production evidence, migration readiness, release
approval, or operational safety.

## Placement Decision

No final site placement has been selected in this spec-only phase.

Candidate placements for future implementation:

- `/legacy-dotnet/evidence/`
- `/legacy-modernization/dotnet-evidence/`
- A section on an existing legacy modernization page.
- A section on an existing limitations or validation page.
- A recorded equivalent if site information architecture changes before
  implementation.

Future implementation must record the selected placement, rejected
alternatives, and why the placement does not imply shipped legacy .NET coverage
before changing site source.

## Evidence-Status Boundary

The future lane must use row-level status labels:

- `shipped`: exact wording true on `main` with public-safe proof.
- `demo`: exact wording backed by checked-in public-safe demo proof.
- `dev` or `dev-only`: exact wording true on target branch and explicitly not
  a main claim.
- `future`: reviewer-question or evidence-shape framing without a support
  claim.
- `hidden`: public results or proof details are not public-safe.

When status is uncertain, the row remains `future`, `hidden`, or omitted.

## Current Ledger Snapshot

The packet was drafted against the current public legacy evidence story posture
on the `dev` base. The existing public ledger pins WCF/service-reference
mapping, WCF metadata normalization, .NET Remoting detection, WebForms event
flow, legacy data metadata, build diagnostics, and flow composition reporting
as hidden pending validation.

ASMX/SOAP and WinForms legacy .NET lane rows are required by this packet but
were not observed as promoted public ledger rows in this review pass. Future
implementation must treat absent or unreconciled rows as `hidden` or `future`
until a public-safe ledger update and proof path exist.

This snapshot is not a promotion decision. Future implementation must recheck
the ledger and branch state before assigning row statuses.

## Review Commands

Planned initial review commands:

```bash
node scripts/kiro-review.mjs --phase site-tracemap-tools-legacy-dotnet-evidence-lane --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase site-tracemap-tools-legacy-dotnet-evidence-lane --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
```

If a requested model or harness is unavailable, record the exact blocker here.
Local review artifacts should remain under `.tmp/kiro-reviews/` and must not be
committed.

## Review Outcomes

### claude-opus-4.8

Status: complete.

Initial command:

```bash
node scripts/kiro-review.mjs --phase site-tracemap-tools-legacy-dotnet-evidence-lane --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
```

Clean artifact:
`.tmp/kiro-reviews/site-tracemap-tools-legacy-dotnet-evidence-lane/2026-06-26T000720-041Z-spec-claude-opus-4.8.clean.md`

Coverage: full.

Findings: 0 high, 2 medium, 3 low.

Patched: hidden sub-surface row collapse/omission rules, hidden capability
inventory guardrails, structural separation between general evidence-model rows
and legacy-surface support rows, `dev` or `dev-only` status consistency,
requirements-level raw artifact wording parity, and adjacent route
re-verification task coverage.

Patch map:

- `requirements.md` Requirement 3 adds family-level collapse, hidden inventory
  guardrails, and omission recording rules.
- `requirements.md` Requirement 5 aligns raw/private artifact wording with the
  design and tasks.
- `design.md` evidence-status matrix updates `dev` or `dev-only` wording and
  adds structural separation requirements for general evidence-model rows
  versus legacy-surface support rows.
- `tasks.md` adds adjacent-route re-verification coverage.

Dispositioned without patch: none.

### claude-sonnet-4.6

Status: complete.

Initial command:

```bash
node scripts/kiro-review.mjs --phase site-tracemap-tools-legacy-dotnet-evidence-lane --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
```

Clean artifact:
`.tmp/kiro-reviews/site-tracemap-tools-legacy-dotnet-evidence-lane/2026-06-26T001057-101Z-spec-claude-sonnet-4.6.clean.md`

Coverage: full.

Findings: 0 high, 2 medium, 3 low.

Patched: Sonnet review outcome recording, Opus finding-to-patch section map,
review-packet header lifecycle note, explicit task to record Sonnet review
outcomes, and `/proof-paths/` candidate-route verification wording.

Dispositioned without patch: none.

### Re-review (post-patch)

Status: complete.

Command:

```bash
node scripts/kiro-review.mjs --phase site-tracemap-tools-legacy-dotnet-evidence-lane --kind re-review --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
```

Clean artifact:
`.tmp/kiro-reviews/site-tracemap-tools-legacy-dotnet-evidence-lane/2026-06-26T001241-369Z-re-review-claude-sonnet-4.6.clean.md`

Coverage: full.

Findings: 0 high, 2 medium, 3 low.

Patched: re-review task wording now records that the pass was required because
prior review findings were patched, requires local artifact recording, and
records model, findings, patched items, dispositions, and clean artifact path
in this file. This re-review outcome block was added, and readiness promotion
wording now explicitly includes the re-review pass.

Dispositioned without patch: Low notes about the already accepted
`claude-opus-4.8` model flag and requirements-level `/proof-paths/` note did
not require additional changes because the command already succeeded and
Requirement 6 plus the tasks already require generated-route verification.

## Validation

Planned spec-only validation:

```bash
git diff --check
./scripts/check-private-paths.sh
```

Also confirm the committed diff is limited to:

```text
.kiro/specs/site-tracemap-tools-legacy-dotnet-evidence-lane/
```

No site build, site test, site validation, or browser sanity check is required
for this spec-only phase because no site source, layout, route, metadata, or
generated output is changed.

Completed spec-only validation:

```bash
git diff --check
./scripts/check-private-paths.sh
git status --short
```

Results:

- `git diff --check`: passed.
- `./scripts/check-private-paths.sh`: passed with `Private path guard passed.`
- Diff scope: only
  `.kiro/specs/site-tracemap-tools-legacy-dotnet-evidence-lane/` is changed.
- Site build, site tests, site validation, and browser checks were not run
  because this phase is spec-only and does not touch site source or layout.

## Follow-Up Items

- Future implementation must choose the route or section placement.
- Future implementation must recheck the legacy evidence story claim ledger and
  target branch status before assigning matrix statuses.
- Future implementation must add focused validation for matrix rows, proof
  paths, limitations, forbidden claims, metadata, links, and private/raw
  material.
- Future implementation must run site tests, site validation, build, and
  browser sanity checks if layout or interaction changes are made.
