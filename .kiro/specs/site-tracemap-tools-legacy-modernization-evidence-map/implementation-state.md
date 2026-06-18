# Site TraceMap Tools Legacy Modernization Evidence Map Implementation State

Status: not-started
Readiness: ready-for-implementation
Last verified: 2026-06-18
Public claim level: concept

## Branch

Spec branch: `codex/spec-site-legacy-modernization-evidence-map`
Base: `origin/dev`
Worktree: dedicated spec worktree; local path intentionally omitted to satisfy
the private absolute-path guardrail.

## Scope

This branch creates the spec packet for a future public-safe site page or
section explaining how TraceMap can organize deterministic static evidence
during legacy modernization and migration planning.

This is spec creation only. It does not implement site code, scanner code,
reducer behavior, runtime integrations, AI/LLM workflows, demo artifacts,
generated evidence, or static site output.

## Public Claim Level

Selected level: `concept`.

Rationale: the future page is explanatory planning content. Existing
public-safe surfaces can support TraceMap's deterministic static evidence model
in general, but this specific legacy modernization evidence map has not shipped
as a dedicated route and does not have a complete checked-in public demo proof
path. Specific demo rows require public-safe proof before they can be shown as
demo-backed.

## Scope Decisions

- Recommended future route: `/legacy-modernization/evidence-map/`, with final
  placement decided during implementation.
- The page must be public-safe planning context for managers, architects,
  engineers, and reviewers.
- The page must connect old framework/toolchain clues, project load failures,
  syntax fallback, WCF, ASMX, remoting, WinForms, WebForms, config/project
  metadata, and legacy data metadata to reviewer questions only where
  public-safe evidence exists or as clearly labeled concept/dev-only/hidden
  material.
- The future page must stay distinct from `/legacy-evidence/` and
  `/legacy-validation/`: it is a reviewer-question evidence map for
  modernization planning, not the legacy evidence story and not the legacy
  validation plan page.
- The future page must also stay distinct from `/capabilities/`,
  `/limitations/`, `/validation/`, `/manager-packet/`, and claim-governance or
  claim-ledger pages: it links to those surfaces instead of restating
  capability maturity, limitations, validation status, the manager packet, or
  the canonical claim ledger.
- Per-theme labels must be reconciled against the
  `site-tracemap-tools-legacy-evidence-story` theme claim ledger as the
  authoritative label source, and cross-checked against
  `legacy-story-reconciliation` as an internal coexistence reference before
  publish.
- Future implementation must re-check the sibling ledger state before assigning
  row labels. In this snapshot, every sibling-ledger theme is pinned `hidden`,
  so legacy-surface detection rows default to named `hidden` rows until the
  ledger is updated with public-safe proof.
- General static-evidence-model rows such as old frameworks/toolchains,
  project-load/build-as-reduced-coverage, syntax fallback, and config/project
  metadata coverage may use `concept` when they describe only the public
  evidence model and reviewer question. They must not imply hidden detection
  support such as build environment diagnostics detection or specific
  WCF/WebForms/WinForms/ASMX support.
- WinForms navigation/event surfaces and ASMX/SOAP services are currently
  required row themes but are not entries in either sibling reconciliation
  source; future implementation must default them to named `hidden` rows until
  a future ledger update adds public-safe proof.
- WebForms route/navigation surfaces beyond the sibling ledger's narrower
  WebForms event-flow theme are also unledgered for that route/navigation
  aspect and default to named `hidden` rows until the sibling ledger is updated
  with public-safe proof.
- `omitted` is reserved for cases where naming the surface family itself would
  leak hidden capability detail or private validation information. Publicly
  documented framework-family names such as WCF, ASMX/SOAP, remoting,
  WinForms, and WebForms do not by themselves require omission.
- `legacy-story-reconciliation` is consulted as an internal hidden-level
  reconciliation source; its private contents must not be surfaced in public
  copy.
- Capabilities that exist only on `dev` must be labeled `dev-only` or omitted.
- Hidden or unsanitized validation stays abstract and does not disclose private
  sample names, raw artifacts, counts, cadence, or unreleased sequencing.
- The spec forbids runtime proof, production traffic, endpoint performance,
  outage-cause, release-safety, operational-safety, database-existence,
  package-compatibility, exploitability, migration-safety, AI/LLM analysis, and
  complete-product-coverage claims.
- Future implementation tasks remain unchecked because this branch is
  spec-only.

## Spec Review Commands And Results

Planned commands:

- `node scripts/kiro-review.mjs --phase site-tracemap-tools-legacy-modernization-evidence-map --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text`
- `node scripts/kiro-review.mjs --phase site-tracemap-tools-legacy-modernization-evidence-map --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`

Results:

- `claude-opus-4.8` initial spec review:
  `node scripts/kiro-review.mjs --phase site-tracemap-tools-legacy-modernization-evidence-map --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text`
  completed with full coverage and saved artifacts under
  `.tmp/kiro-reviews/site-tracemap-tools-legacy-modernization-evidence-map/2026-06-18T190342-577Z-spec-claude-opus-4.8.*`.
  Findings: 2 Medium and 2 Low. Patched the Medium findings by distinguishing
  this page from `/legacy-evidence/` and `/legacy-validation/`, and by
  requiring row-label reconciliation with the sibling legacy theme claim
  ledger. Also patched the Low cross-link and shipped-row proof-link hardening.
- `claude-sonnet-4.6` initial spec review:
  `node scripts/kiro-review.mjs --phase site-tracemap-tools-legacy-modernization-evidence-map --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`
  completed with full coverage and saved artifacts under
  `.tmp/kiro-reviews/site-tracemap-tools-legacy-modernization-evidence-map/2026-06-18T190642-706Z-spec-claude-sonnet-4.6.*`.
  Findings: 4 Low and no Medium or higher findings. Patched the Low
  bookkeeping and polish items: validation readiness gate wording, explicit
  re-review follow-up wording, `git diff --cached --check` in future tasks,
  and cross-link sequencing guidance.
- `claude-sonnet-4.6` re-review:
  `node scripts/kiro-review.mjs --phase site-tracemap-tools-legacy-modernization-evidence-map --kind re-review --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`
  completed with full coverage and saved artifacts under
  `.tmp/kiro-reviews/site-tracemap-tools-legacy-modernization-evidence-map/2026-06-18T190805-796Z-re-review-claude-sonnet-4.6.*`.
  Findings: 3 Low and no Medium or higher findings. Patched readiness-gate task
  coverage and the design definition for `omitted`.
- `claude-opus-4.8` re-review:
  `node scripts/kiro-review.mjs --phase site-tracemap-tools-legacy-modernization-evidence-map --kind re-review --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text`
  completed with full coverage and saved artifacts under
  `.tmp/kiro-reviews/site-tracemap-tools-legacy-modernization-evidence-map/2026-06-18T190853-992Z-re-review-claude-opus-4.8.*`.
  Findings: 1 Medium and 2 Low. Patched the Medium finding by defaulting
  unledgered WinForms and ASMX/SOAP themes to `hidden` or `omitted` until a
  sibling ledger update adds public-safe proof. Also patched the label
  vocabulary drift and hidden-source clarification.
- `claude-opus-4.8` second re-review:
  `node scripts/kiro-review.mjs --phase site-tracemap-tools-legacy-modernization-evidence-map --kind re-review --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text`
  completed with full coverage and saved artifacts under
  `.tmp/kiro-reviews/site-tracemap-tools-legacy-modernization-evidence-map/2026-06-18T191424-857Z-re-review-claude-opus-4.8.*`.
  Findings: 2 Medium and 2 Low. Patched by clarifying that all current
  sibling-ledger themes default to `hidden` or `omitted`, constraining
  `concept` row labels to general evidence-model framing only, adding
  enforceable distinctness from non-legacy site surfaces, and clarifying that
  `legacy-story-reconciliation` is an internal coexistence reference rather
  than the authoritative label ledger.
- `claude-opus-4.8` third re-review:
  `node scripts/kiro-review.mjs --phase site-tracemap-tools-legacy-modernization-evidence-map --kind re-review --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text`
  completed with full coverage and saved artifacts under
  `.tmp/kiro-reviews/site-tracemap-tools-legacy-modernization-evidence-map/2026-06-18T191814-054Z-re-review-claude-opus-4.8.*`.
  Findings: 2 Medium and 3 Low. Patched by resolving the row-inclusion versus
  hidden/omitted conflict, requiring named `hidden` rows for public framework
  families, narrowing `concept` rows to non-hidden/non-unledgered framing,
  standardizing on `legacy data metadata`, requiring implementation-time ledger
  re-checks, and documenting that a sparse map is acceptable.
- `claude-opus-4.8` fourth re-review:
  `node scripts/kiro-review.mjs --phase site-tracemap-tools-legacy-modernization-evidence-map --kind re-review --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text`
  completed with full coverage and saved artifacts under
  `.tmp/kiro-reviews/site-tracemap-tools-legacy-modernization-evidence-map/2026-06-18T192406-137Z-re-review-claude-opus-4.8.*`.
  Findings: 1 Medium and 3 Low. Patched by separating general public
  static-evidence-model rows from hidden legacy-surface detection rows, keeping
  reduced-coverage and syntax-fallback principles public concept without
  implying hidden detection capability, and noting WebForms route/navigation as
  an unledgered hidden-default surface.
- `claude-opus-4.8` fifth re-review:
  `node scripts/kiro-review.mjs --phase site-tracemap-tools-legacy-modernization-evidence-map --kind re-review --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text`
  completed with full coverage and saved artifacts under
  `.tmp/kiro-reviews/site-tracemap-tools-legacy-modernization-evidence-map/2026-06-18T192905-713Z-re-review-claude-opus-4.8.*`.
  Findings: 1 Medium and 3 Low. Patched by explicitly forbidding
  config/project metadata rows from implying hidden WCF, `system.serviceModel`,
  service-reference, endpoint, or connection-string extraction and by recording
  the WebForms route/navigation unledgered default.
- `claude-sonnet-4.6` final re-review:
  `node scripts/kiro-review.mjs --phase site-tracemap-tools-legacy-modernization-evidence-map --kind re-review --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`
  completed with full coverage and saved artifacts under
  `.tmp/kiro-reviews/site-tracemap-tools-legacy-modernization-evidence-map/2026-06-18T193257-544Z-re-review-claude-sonnet-4.6.*`.
  Findings: 4 Low and no Medium or higher findings. Patched the useful Low
  cross-link task guidance and validation-gate wording before final readiness.

### Review Coverage Status

Medium or higher findings must be patched and re-reviewed where feasible before
`Readiness` moves to `ready-for-implementation`.

## Validation

Planned spec validation:

- `git diff --check`
- `git diff --cached --check`
- `./scripts/check-private-paths.sh`

Results:

- `git diff --check`: passed on 2026-06-18.
- `git diff --cached --check`: passed on 2026-06-18.
- `./scripts/check-private-paths.sh`: passed on 2026-06-18 with
  `Private path guard passed.`

## Oddities

- This spec intentionally overlaps with public capability, limitations,
  validation, manager, and claim-governance pages. The distinct role is a
  legacy modernization evidence map: it organizes reviewer questions around
  old-framework and legacy-surface evidence without claiming assessment
  completeness.
- This spec also intentionally overlaps with the already-implemented
  `site-tracemap-tools-legacy-evidence-story` and
  `site-tracemap-tools-legacy-validation-concept` specs. The distinct role is a
  reviewer-question evidence map: what old project shape is visible, what was
  found by semantic analysis versus syntax/config fallback, what gaps remain,
  and what must be validated before public or migration conclusions. It is not
  the narrative legacy evidence story and not the `/legacy-validation/` plan
  page. Future implementation must confirm the placement does not duplicate or
  supersede those pages before publishing.
- The spec includes both `requirements.md` and `design.md` because the future
  page has enough public-claim and information-architecture constraints that a
  separate design note should help implementation stay bounded.

## Follow-Ups

- Run re-review of the Opus-patched and Sonnet-polished material with at least
  one requested model, and with both requested models where feasible.
- Patch any remaining Medium or higher findings and rerun re-review where
  feasible.
- Run the required whitespace, cached-diff, and private-path checks.
- Future implementation should decide whether to add the page as a standalone
  route or as a section on an existing public-safe surface.
