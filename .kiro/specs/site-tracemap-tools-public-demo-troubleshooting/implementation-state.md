# Site TraceMap Tools Public Demo Troubleshooting Implementation State

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

## Branch

Spec branch: `codex/spec-site-public-demo-troubleshooting`

Base: `origin/dev`

Target PR base: `dev`

Worktree: dedicated isolated spec worktree; local absolute path intentionally
omitted from checked-in spec notes.

Scope: create a spec-only Kiro packet for a future public-demo
troubleshooting page or section. Only this spec directory is in scope.

Out of scope: site source, generated output, scanner code, reducer code,
runtime diagnostics, support workflow, navigation changes, sitemap changes,
existing specs, and implementation of public copy.

## Current State

The packet is ready for future implementation. It defines future requirements,
design, tasks, and review focus for a public demo troubleshooting surface.

No site code has been changed.

`./scripts/check-private-paths.sh` exists on this branch and remains part of
the required spec-phase validation before readiness advances.

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

Final placement is intentionally not chosen in this spec-only phase.

Allowed candidate placements:

- `/demo/troubleshooting/`
- `/demo/help/`
- Section on `/demo/runbook/`
- Section on `/demo/start-here/`

The future implementation must record the selected placement, rejected
alternatives, adjacent-route status, and metadata consequences here before
changing site source.

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

- Focused validation for required rows and fields.
- Focused validation for required links and adjacent-route distinctions.
- Focused validation for metadata, sitemap metadata, and discovery metadata
  when standalone.
- Focused validation for section host metadata and anchors when section-based.
- Focused validation for forbidden claims, private/raw material, blame
  language, and word count bounds.
- `npm test` from `site/`.
- `npm run validate` from `site/`.
- `npm run build` from `site/`.
- Desktop and mobile browser sanity checks after layout changes.

## Oddities

- The phrase `where to ask next` is a required row label. Future copy must keep
  it as an evidence-question handoff, not an invitation to create a support SLA
  or diagnose runtime behavior.
- Some forbidden phrases may appear inside rejected-pattern or non-claim
  regions for validation purposes. Future validation should distinguish
  rejected examples from affirmative public claims.

## Follow-Up Items

- Future implementation must keep this spec-local state updated as placement,
  validation, and route decisions are made.
- Choose final placement and record it here before changing site source.
- Record absent, moved, or concept-only adjacent routes and their link
  handling before changing site source.
- Introduce and record the programmatic rejected-pattern marker if the site
  does not already define one.
- Introduce and record the programmatic non-claim marker if the site does not
  already define one.
