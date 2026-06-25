# Site TraceMap Tools Public Demo Troubleshooting Implementation State

Status: implemented
Readiness: ready-for-implementation
Public claim level: concept

## Branch

Spec branch: `codex/spec-site-public-demo-troubleshooting`

Implementation branch: `codex/impl-site-public-demo-troubleshooting`

Base: `origin/dev`

Target PR base: `dev`

Worktree: dedicated isolated spec worktree; local absolute path intentionally
omitted from checked-in spec notes.

Scope: implement the public-demo troubleshooting concept surface as a bounded
public static-site route, with route-specific validation and spec-state
bookkeeping.

Out of scope: generated output edits, scanner code, reducer code, runtime
diagnostics, support workflow, primary navigation changes, production
availability claims, and stronger demo/proof claims.

## Current State

The implementation is in progress on a dedicated `codex/` branch. The route
was added as a standalone concept-level public page at
`/demo/troubleshooting/`.

Site source has been changed only under public site source, site metadata,
site validation scripts/tests, one inbound link from the demo runbook, and this
spec-local implementation state/tasks file.

`./scripts/check-private-paths.sh` exists on this branch and remains part of
the required implementation validation before completion.

## Claim-Level Decision

Selected public claim level: `concept`.

Rationale: the future surface explains how visitors should interpret confusing
public demo routes, stale demo summaries, proof expectation gaps, reduced
coverage labels, private-only evidence boundaries, unsupported claim wording,
validation mismatches, and owner handoffs. It does not generate evidence,
prove a public demo result, diagnose runtime behavior, approve release safety,
or guarantee complete coverage.

Do not upgrade the future surface to `demo` unless a future spec amendment
records checked-in public-safe demo evidence for the exact claims, rows, proof
links, and validation checks.

## Placement Decision

Final placement: standalone route `/demo/troubleshooting/`.

Placement rationale: the troubleshooting matrix is large enough to deserve a
durable URL and would crowd `/demo/runbook/` or `/demo/start-here/`. The
content answers "what should I check when public demo guidance does not line
up?" rather than the runbook's reading sequence, start-here onboarding, result
summary, proof-upgrades ledger, validation method, limitations catalog, or
stakeholder objection guide.

Rejected alternatives:

- `/demo/help/`: rejected because the spec vocabulary and matrix are about
  troubleshooting public demo proof/wording mismatches, not general help.
- Section on `/demo/runbook/`: rejected because the runbook remains the demo
  reading sequence and operator checklist; the matrix would dominate it.
- Section on `/demo/start-here/`: rejected because start-here remains
  first-visitor orientation and should not carry a dense troubleshooting
  table.

Metadata consequences: standalone route title, description, canonical URL,
Open Graph metadata, sitemap entry, and discovery entry were added with
`publicClaimLevel: concept`. Primary navigation remains unchanged. A single
bounded inbound link, `Demo troubleshooting`, was added from the demo runbook's
bridge routes.

Section-placement reconciliation: not applicable. There is no host-page
claim-level conflict, no section anchor set, and no host crowding measurement
because the selected placement is a standalone route.

Implementation sequencing note: placement and marker choices were made during
the implementation read-through before site edits; this checked-in state file
was updated after the first source patch with the same decisions.

## Adjacent Route Inventory

All required adjacent public routes exist in this checkout and are linked with
bounded anchor text:

- `/demo/runbook/`: present; linked as `demo runbook` or `Demo runbook`.
- `/demo/start-here/`: present; linked as `demo start-here` or
  `Demo start-here`.
- `/demo/result/`: present; linked as `demo result` or `Demo result`.
- `/demo/proof-upgrades/`: present; linked as `proof upgrades` or
  `Proof upgrades`.
- `/validation/`: present; linked as `validation expectations` or
  `Validation expectations`.
- `/limitations/`: present; linked as `limitations` or
  `Limitations and non-claims`.
- `/questions/objections/`: present; linked as `questions and objections` or
  `Questions and objections`.

No adjacent routes were absent, moved, substituted, deferred, or omitted.

## Programmatic Markers

Rejected-pattern marker: `data-rejected-pattern-region` on the rejected
wording section. The route-specific validator requires exactly one marked
rejected-pattern region and scans forbidden affirmative claims outside this
marker.

Non-claim marker: `data-non-claim-region` on the non-claim boundary section,
the stop/non-claim section, and each matrix `what not to conclude` and
`non-claim` cell. This marker is distinct from the rejected-pattern marker.
The route-specific validator excludes marked non-claim regions from
affirmative-claim scanning while still applying hard private/raw-material
scanning everywhere.

## Scope Decisions

- The page or section must be a public site/demo guidance surface.
- The page or section must not become a support contract, runtime diagnostic
  tool, production proof page, release safety or approval page, endpoint
  performance checker, complete coverage claim, AI/LLM analysis surface, or
  replacement for validation and human review.
- The page or section must include visible `Public claim level: concept`.
- The page or section must include visible `No public conclusion without
  evidence`.
- Required rows are missing route, outdated demo summary, broken proof
  expectation, reduced coverage label, private-only evidence, unsupported claim
  wording, validation mismatch, and where to ask next.
- Every required row must include symptom, likely public-safe cause, what to
  check, what not to conclude, next owner/route, stop condition, and non-claim.
- Public copy must not include raw facts, SQLite, analyzer logs, source
  snippets, SQL, config values, secrets, local paths, raw remotes, generated
  scan directories, private sample names, command output, hidden validation
  details, or credential-like values.
- Local-only artifact families are described only generically as private/raw
  material in public copy; exact raw artifact names are intentionally avoided
  on this route.

## Review Commands

Planned initial review commands:

```bash
node scripts/kiro-review.mjs --phase site-tracemap-tools-public-demo-troubleshooting --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase site-tracemap-tools-public-demo-troubleshooting --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
```

If either requested model is unavailable, record the exact unavailable-tool or
model error here, run a best-available substitute when the harness offers one,
and keep readiness at `spec-review` until Medium or higher findings are
patched or dispositioned.

## Model Identifier Verification

Both requested identifiers were confirmed available against
`kiro-cli chat --list-models --format json` on 2026-06-24 before review:

- `claude-opus-4.8`: verified available; no substitution.
- `claude-sonnet-4.6`: verified available; no substitution.

## Review Outcomes

Initial review date: 2026-06-24.

| Cycle | Model | Clean artifact | Coverage | Findings summary | Disposition |
| --- | --- | --- | --- | --- | --- |
| Initial spec review | `claude-opus-4.8` | `.tmp/kiro-reviews/site-tracemap-tools-public-demo-troubleshooting/2026-06-24T031215-997Z-spec-claude-opus-4.8.clean.md` | Full | Medium findings on public matrix references to internal spec artifacts, normative rejected-pattern markers, and distinct validation scopes for claims versus raw/private material. Low notes on validation-source completeness and owner-handoff SLA drift. | Patched in requirements, design, tasks, and implementation-state. |
| Initial spec review | `claude-sonnet-4.6` | `.tmp/kiro-reviews/site-tracemap-tools-public-demo-troubleshooting/2026-06-24T031216-062Z-spec-claude-sonnet-4.6.clean.md` | Full | Low findings on check-private-paths state note, rejected-pattern marker convention, and word-count matrix-text definition. | Patched. |
| Re-review | `claude-opus-4.8` | `.tmp/kiro-reviews/site-tracemap-tools-public-demo-troubleshooting/2026-06-24T031539-750Z-re-review-claude-opus-4.8.clean.md` | Reduced | High finding on forbidden-claim validation needing a separate non-claim/matrix marker; Medium findings on review-state consistency and section placement crowding. Reduced coverage because Kiro reported denied tool access for a shell verification attempt. | Patched in requirements, design, tasks, review-packet, and implementation-state. |
| Re-review | `claude-sonnet-4.6` | `.tmp/kiro-reviews/site-tracemap-tools-public-demo-troubleshooting/2026-06-24T031539-831Z-re-review-claude-sonnet-4.6.clean.md` | Full | Low findings on stale task and follow-up bookkeeping plus design word-count detail. | Patched. |
| Final re-review | `claude-opus-4.8` | `.tmp/kiro-reviews/site-tracemap-tools-public-demo-troubleshooting/2026-06-24T031951-728Z-re-review-claude-opus-4.8.clean.md` | Full | Medium findings on section-placement claim-level scoping, measurable section crowding, and mobile access to the likely public-safe cause field. Low notes on readiness gate wording and adjacent-route inventory. | Patched in requirements, design, tasks, review-packet, and implementation-state. |
| Final re-review | `claude-sonnet-4.6` | `.tmp/kiro-reviews/site-tracemap-tools-public-demo-troubleshooting/2026-06-24T031952-537Z-re-review-claude-sonnet-4.6.clean.md` | Full | Medium findings on model identifier verification and spec-phase gate status clarity. Low notes on check-private-paths result placeholder. | Patched. |
| Final verification re-review | `claude-opus-4.8` | `.tmp/kiro-reviews/site-tracemap-tools-public-demo-troubleshooting/2026-06-24T032407-661Z-re-review-claude-opus-4.8.clean.md` | Full | Medium finding on matrix-inclusive section crowding measurement; Low notes on readiness gate wording, section size, scanner authoring constraints, and relative review artifact paths. | Patched in requirements, design, tasks, and implementation-state. |
| Final verification re-review | `claude-sonnet-4.6` | `.tmp/kiro-reviews/site-tracemap-tools-public-demo-troubleshooting/2026-06-24T032408-113Z-re-review-claude-sonnet-4.6.clean.md` | Full | Low findings only on readiness gate wording, check-private-paths placeholder clarity, and review-packet mobile-disclosure focus. | Patched or accepted as non-blocking review-checklist polish. |

Medium or higher findings remaining after patch disposition: none known.

## Validation Results

Kiro spec review cycles: completed; see Review Outcomes above. Medium or
higher findings have been patched or dispositioned.

Spec-phase static checks:

- `git diff --check`: passed on 2026-06-24.
- `./scripts/check-private-paths.sh`: passed on 2026-06-24 with
  `Private path guard passed.`

Planned spec-phase validation:

- `git diff --check`
- `./scripts/check-private-paths.sh`

Future implementation validation:

- `node --test scripts/demo-troubleshooting.test.mjs`: passed on 2026-06-25.
- Focused validation added for visible concept claim label and shared
  principle, required rows and fields, table header/field markers, adjacent
  route distinctions, bounded anchor text, internal spec artifact directions,
  standalone metadata, sitemap/discovery metadata, rejected-pattern markers,
  non-claim markers, forbidden affirmative claims outside marked regions,
  hard private/raw material everywhere, blame language, owner-handoff support
  promises, and standalone word count.
- Aggregate validation is wired through `site/scripts/validate.mjs`.
- `cd site && npm test`: passed on 2026-06-25.
- `cd site && npm run validate`: passed on 2026-06-25; validated 68 HTML
  files, 2353 internal references, and 67 sitemap URLs.
- `cd site && npm run build`: passed on 2026-06-25.
- `git diff --check`: passed on 2026-06-25.
- `./scripts/check-private-paths.sh`: passed on 2026-06-25 with
  `Private path guard passed.`
- Desktop browser sanity for `http://localhost:4173/demo/troubleshooting/`:
  passed on 2026-06-25 using Playwright at 1440x1000. Snapshot showed visible
  concept claim label, shared principle, semantic table headers, and required
  matrix rows.
- Mobile browser sanity for `http://localhost:4173/demo/troubleshooting/`:
  passed on 2026-06-25 using Playwright at 390x844. Snapshot/eval showed no
  body overflow, a horizontally reachable matrix overflow region, 8 required
  rows, 1 rejected-pattern region, and 18 non-claim regions.
- Section host metadata, duplicate section-anchor, claim-level reconciliation,
  and host crowding checks are not applicable because the final placement is
  standalone.

## Oddities

- The phrase `where to ask next` is a required row label. Future copy must keep
  it as an evidence-question handoff, not an invitation to create a support SLA
  or diagnose runtime behavior.
- Some forbidden phrases may appear inside rejected-pattern or non-claim
  regions for validation purposes. Future validation should distinguish
  rejected examples from affirmative public claims.
- Discovery metadata uses broader safe wording than the page body where the
  global discovery safety guard already restricts denied terms outside direct
  `nonClaims` entries.
- The matrix is a semantic table in an overflow wrapper. Desktop/mobile sanity
  must verify the horizontal table remains reachable on narrow viewports.

## Follow-Up Items

- Create a PR to `dev` and run the required ACK PR loop.
