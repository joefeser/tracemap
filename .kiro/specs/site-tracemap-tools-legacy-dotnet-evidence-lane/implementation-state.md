# Site TraceMap Tools Legacy .NET Evidence Lane Implementation State

Status: implemented
Readiness: ready-for-implementation
Public claim level: concept

## Branch

Spec branch: `codex/spec-site-legacy-dotnet-evidence-lane-20260625190237`

Implementation branch:
`codex/impl-site-legacy-dotnet-evidence-lane-20260625194131`

Base: `origin/dev`

Target PR base: `dev`

Scope: site implementation for a public concept-level legacy .NET evidence
lane. Scanner code, reducer code, generated site output, and unrelated specs
remain out of scope for this phase.

Worktree: dedicated isolated implementation worktree; local absolute path intentionally
omitted from checked-in spec notes.

## Current State

The packet completed Kiro spec review and this phase implements the public-site
lane. The lane is implemented as a standalone concept route with focused site
validation.

Current readiness is `ready-for-implementation`. Opus, Sonnet, and one bounded
post-patch re-review completed with full coverage. Medium or higher findings
were patched, and the final packet still satisfies the spec-only scope.

Implementation status: page source, route metadata, discovery metadata, focused
validation, sitemap metadata, and spec task notes were updated for the
concept-level site route. Generated `site/dist` and `site/output` artifacts are
not committed.

## Claim-Level Decision

Selected public claim level: `concept`.

Rationale: this packet defines a future evidence-status lane and reviewer
matrix. It does not publish new scanner behavior, reducer findings, public demo
proof, runtime telemetry, production evidence, migration readiness, release
approval, or operational safety.

## Placement Decision

Selected placement: standalone route `/legacy-dotnet/evidence/`.

Rationale: the route is specific to the legacy .NET evidence lane, keeps the
broader `/legacy-modernization/evidence-map/` page as the modernization map,
and does not imply shipped legacy .NET scanner coverage because the page title,
hero note, metadata, discovery entry, and matrix rows all keep `Public claim
level: concept`, `No public conclusion without evidence`, row-level statuses,
proof requirements, and limitations visible.

Rejected alternatives:

- `/legacy-modernization/dotnet-evidence/`: close, but less direct than the
  chosen route and easier to confuse with the broader modernization map.
- A section on `/legacy-modernization/evidence-map/`: rejected because the
  existing page already serves as the broader modernization evidence map.
- A section on `/legacy-evidence/`: rejected because that route is the
  canonical legacy evidence story and hidden claim ledger, not the .NET lane.
- A section on `/limitations/` or `/validation/`: rejected because those routes
  are cross-cutting boundaries, not the focused lane.

Primary navigation remains unchanged. The lane is discoverable through sitemap
and discovery metadata, and it links to adjacent proof-boundary routes without
joining the top nav.

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

Implementation reconciliation: the target branch was rechecked before assigning
row statuses. The public legacy evidence story and legacy modernization
evidence map still present WCF/service-reference mapping, WCF metadata
normalization, .NET Remoting, WebForms, legacy data metadata, build diagnostics,
and flow composition as hidden pending validation. ASMX/SOAP and WinForms are
treated as hidden pending ledger entry. No legacy .NET surface row was promoted
to `shipped`, `demo`, `dev`, or `dev-only`.

Row decisions:

- General evidence-model rows for status vocabulary, evidence tiers, and
  reduced coverage use `future` because they explain proof posture without
  asserting legacy .NET support.
- WCF is `hidden pending validation`; its row covers hosts, references,
  contracts, operations, binding or endpoint metadata, generated clients, and
  metadata normalization at family level only.
- ASMX/SOAP is `hidden pending ledger entry`; its row covers service
  declarations, SOAP operation metadata, generated proxy clues, and checked-in
  metadata at family level only.
- .NET Remoting is `hidden pending validation`; its row covers API references,
  channel registration clues, marshal-by-reference type clues, and remoting
  configuration at family level only.
- WebForms is `hidden pending validation and ledger entry`; its row covers
  markup event bindings, code-behind handlers, designer fields, route or
  navigation clues, and postback questions at family level only.
- WinForms is `hidden pending ledger entry`; its row covers form and control
  metadata, designer-file clues, event-handler references, launch clues,
  navigation clues, and UI-to-backend questions at family level only.
- Legacy data metadata is `hidden pending validation`; its row covers DBML,
  EDMX, typed DataSet, TableAdapter, provider metadata, connection-name
  metadata, and ORM-like mapping clues at family level only.
- Project and toolchain diagnostics are `hidden pending validation`; the row
  covers target framework, project style, SDK or non-SDK shape, toolset,
  restore clues, package metadata, generated-file uncertainty, project-load
  failure, build failure, syntax fallback, and analysis gaps.
- Modernization review use is `future` reviewer framing because it translates
  status rows into owner follow-up, proof gaps, review sequencing, and planning
  questions without claiming migration approval.

No hidden sub-surface inventory, hidden validation cadence, private sample
name, hidden count, or unreleased sequencing was published. Required
sub-surfaces were collapsed into family-level rows where publishing finer
granularity could imply hidden capability scope.

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
git diff --cached --check
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

Completed site implementation validation:

```bash
node --test scripts/legacy-dotnet-evidence-lane.test.mjs
npm test
npm run validate
npm run build
git diff --check
./scripts/check-private-paths.sh
```

Results:

- Focused legacy .NET evidence-lane tests passed.
- Full site tests passed.
- Site validation passed and reported 11 legacy .NET evidence-lane rows in
  generated output.
- Site build passed.
- `git diff --check` passed.
- `git diff --cached --check` passed after staging the implementation files.
- `./scripts/check-private-paths.sh` passed with `Private path guard passed.`
- Browser sanity check passed for desktop 1440x1200 and mobile 390x844 using
  the built static route. Screenshots were captured as generated Playwright
  artifacts and are not committed.

## Follow-Up Items

- Promote row statuses only after public-safe proof exists for the exact
  wording and branch.
- Add or update the canonical legacy evidence ledger before any hidden lane row
  moves to `demo`, `dev`, `dev-only`, or `shipped`.
- Keep the route out of primary navigation unless a later information
  architecture review records a concept-level nav decision.
- Preserve family-level hidden rows until publishing sub-surface detail is
  public-safe.

## PR Review-Loop Findings

Initial ACK on PR review returned `actionable_findings` for two unresolved
review threads.

Patched:

- Tightened `demo` row proof wording so generated summaries and validation
  output must also be checked in and public-safe.
- Expanded branch-status validation wording from `dev` rows to both `dev` and
  `dev-only` rows.
- Updated `review-packet.md` review-cycle wording from initial pending review
  state to completed Opus, Sonnet, and bounded re-review state.

Implementation ACK on PR 345 returned `actionable_findings` for five
unresolved review threads.

Patched:

- Clarified that `git diff --cached --check` passed after staging the
  implementation files.
- Updated the legacy .NET evidence-lane validator to decode numeric and
  hexadecimal HTML entities before leak checks.
- Updated the legacy .NET evidence-lane validator to scan decoded metadata and
  raw HTML text for unsupported support claims, not only tag-stripped rendered
  text.
