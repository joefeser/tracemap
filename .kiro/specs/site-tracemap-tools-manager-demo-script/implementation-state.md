# Site TraceMap Tools Manager Demo Script Implementation State

Status: implemented
Readiness: implemented
Public claim level: concept

## Current Scope

Implemented a concept-level public site route for a bounded
manager/teammate demo script.

Changed source files:

- `site/src/demo/manager-script/index.html`
- `site/src/demo/index.html`
- `site/src/_site/pages.json`
- `site/src/_site/discovery.json`
- `site/scripts/manager-demo-script.mjs`
- `site/scripts/manager-demo-script.test.mjs`
- `site/scripts/validate.mjs`
- `site/scripts/validate.test.mjs`
- `.kiro/specs/site-tracemap-tools-manager-demo-script/tasks.md`
- `.kiro/specs/site-tracemap-tools-manager-demo-script/implementation-state.md`

No scanner, reducer, generated output, runtime service, analytics dependency,
client-side data fetch, form, or new generated evidence artifact was added.

## Branch And Base

- Branch: `codex/impl-site-manager-demo-script`.
- Base: `origin/dev`.
- PR target: `dev`.
- Implementation status: implemented and published in PR 258.
- Spec sync note: `origin/dev` did not contain this spec directory at
  implementation start, so the spec packet was restored from `origin/main`
  before editing. This records the temporary main/dev sync gap without adding
  local paths.

## Placement State

Final placement: `/demo/manager-script/`.

Rationale:

- The route is demonstrably a demo aid and stays under the public demo route
  family.
- It keeps the human presenter script separate from the operator checklist at
  `/demo/runbook/`.
- It avoids making the script look like the canonical manager summary,
  manager FAQ, manager packet, or product capability proof.

Rejected alternatives:

- `/demo/briefing/`: too broad and less explicit about a script with stop
  conditions.
- Section on `/demo/runbook/`: would bury the presenter words inside an
  operator checklist.
- Section on `/manager-brief/`: too close to manager positioning and easier to
  misread as a stronger product claim.
- Replacing or merging with `/manager-packet/`: the packet explains value; the
  script choreographs a bounded live route.

Navigation decision:

- Added one inbound discovery path from `/demo/` through a hero action and a
  short callout.
- Did not add the route to primary navigation, to avoid bloating the top nav
  with a presenter-specific script.

## Route Verification State

Generated-output verification before linking confirmed these route entries and
claim levels:

| Route | Claim level | Title |
| --- | --- | --- |
| `/` | `demo` | TraceMap Home |
| `/capabilities/` | `demo` | Capabilities |
| `/proof-paths/` | `demo` | Proof Path Index |
| `/proof-source-catalog/` | `demo` | Proof Source Catalog |
| `/demo/result/` | `demo` | Public Demo Result |
| `/demo/runbook/` | `demo` | Public Demo Runbook |
| `/questions/` | `concept` | Stakeholder Question Index |
| `/limitations/` | `demo` | Limitations |
| `/validation/` | `demo` | Validation |
| `/static-vs-runtime/` | `concept` | Static Evidence Vs Runtime Telemetry |

No substitutions, removals, or route blocks were needed. Evidence-field
wording uses `where present` or `where visibly present` when referencing rule
IDs, rule families, evidence tiers, coverage labels, proof paths, source
mapping, limitations, and validation state on linked pages.

## Implemented Surface

The new page includes:

- Visible `Public claim level: concept`.
- Visible `No public conclusion without evidence`.
- Opening context.
- 2-minute tour.
- 5-minute proof walkthrough.
- Manager questions and safe answer shapes for value, trust, completeness,
  release decision, production behavior, incident use, team handoff, and what
  to do next.
- Engineer questions and proof routes for rule IDs, evidence tiers, coverage
  labels, source mapping, demo result status, gaps, static-versus-runtime
  boundaries, validation, and raw artifact boundaries.
- Stop conditions.
- Follow-up handoff.
- Non-claims.

The copy keeps the route at concept level and does not claim production
incident diagnosis, runtime proof, release approval, complete dependency
understanding, endpoint performance insight, operational safety, or AI/LLM
impact analysis. Raw facts, raw SQLite content, analyzer logs, raw source
snippets, raw SQL, config values, secrets, local paths, raw remotes, generated
scan directories, private sample names, and hidden validation details are
named only as material that stays out of public copy.

## Validation Log

Completed:

- `git diff --check`: passed.
- `./scripts/check-private-paths.sh`: passed.
- `cd site && npm test`: passed, 270 tests.
- `cd site && npm run validate`: passed; built static site and validated 50
  HTML files, 1641 internal references, 49 sitemap URLs, 1 legacy story safety
  target, and 13 legacy modernization evidence-map rows.
- `cd site && npm run build`: passed.
- Desktop browser sanity: passed at 1440x1100 on
  `/demo/manager-script/`; title was `Manager Demo Script | TraceMap`, 9 main
  sections, 42 main links, and no horizontal overflow.
- Mobile browser sanity: passed at 390x844 on `/demo/manager-script/`; title
  was `Manager Demo Script | TraceMap`, 9 main sections, 42 main links, and no
  horizontal overflow.

## PR Loop State

Initial PR-loop command after the ready PR was opened against `dev`:

```bash
agent-control pr-loop --repo joefeser/tracemap --pr 258 --base dev --require-codex-review --quiet --json
```

Initial result for head `671ab0f6789dda98795a8b466bd1049b3338f733`:

- Decision: `merge_ready`.
- Stop reason: `NONE`.
- Next action: `merge_ready`.
- Human next action: `merge_current_head`.
- Merge state: `CLEAN`.
- Pending checks: none.
- Failed checks: none.
- Unresolved review threads: 0.
- Actionable bot findings: 0.
- Qodo state: `review_completed`.
- Gemini state: `review_completed`.
- Codex state: `not_requested`; treated as residual risk, not a blocker, by
  configured `trustedCodeReview` quorum.
- Sourcery state: `not_requested`; optional residual risk, not a blocker.
- Review quorum: enabled; group `trustedCodeReview`; returned bots `qodo`;
  missing bots `codex`; minimum returned `1`; quorum met; residual risk
  `medium`; required Codex review satisfied by quorum on `dev`.
- Lane config: loaded from `.agent-control/lanes/pr-review-loop.yaml`.
- Push batching: no local push batching action active.
- Recommended human action: Joe can merge the current head if he accepts the
  configured policy evidence.

This file was updated after the initial PR-loop result to record the outcome,
so a follow-up state-only commit must be pushed and the PR loop rerun for the
new head before final merge-readiness reporting.

## Oddities

- The implementation worktree started from `origin/dev`, but the spec packet
  was present only on `origin/main`; it was restored from `origin/main` before
  editing.
- The manager demo script validator normalizes `public-safe` before scanning
  unsupported conclusion wording so the required phrase `safe answer shapes`
  and existing public-safe terminology do not become false positives.
- The aggregate site validation fixture needed a compact generated-page
  representation for `/demo/manager-script/` and target claim-level metadata
  for `/capabilities/`.

## Follow-Up Items

- Rerun PR loop after the state-only follow-up commit that records the initial
  PR-loop result.
