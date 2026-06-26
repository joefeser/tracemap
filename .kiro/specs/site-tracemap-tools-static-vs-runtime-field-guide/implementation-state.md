# Site TraceMap Tools Static Vs Runtime Field Guide Implementation State

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

## Branch

Spec branch: `codex/spec-site-static-vs-runtime-field-guide-20260625190224`
Base: `origin/dev`
Target PR base: `dev`

Worktree: dedicated temporary worktree; local absolute path intentionally
omitted from checked-in spec notes.

## Scope

This packet specifies a future public `tracemap.tools` field guide explaining
how TraceMap's deterministic static evidence complements runtime telemetry,
APM, logs, traces, metrics, dashboards, tests, incident response, and
service-owner interpretation.

This is spec-only. It changes only the spec packet under
`.kiro/specs/site-tracemap-tools-static-vs-runtime-field-guide/`. It does not
change site source, generated site output, scanner code, reducer behavior,
runtime telemetry ingestion, observability integrations, validation scripts,
or generated artifacts.

## Current State

Spec packet reviewed, patched, validated, and ready for future implementation.

Spec files:

- `requirements.md`
- `design.md`
- `tasks.md`
- `implementation-state.md`
- `review-packet.md`

## Claim-Level Decision

Public claim level: `concept`.

Rationale: the future surface is an explanatory field guide. It can describe
how to read deterministic static evidence beside runtime observability, but it
does not publish a new TraceMap finding, scanner capability, reducer result,
runtime observation, production monitoring integration, incident conclusion,
endpoint performance proof, release decision, operational safety claim, or
demo-backed runtime proof path.

## Scope Decisions

- The future page must include the visible core message: `TraceMap shows static
  dependency evidence and limitations; runtime tools show observed behavior.
  Neither replaces the other.`
- The future page may describe generic runtime observability categories such
  as logs, traces, metrics, APM, dashboards, alerts, telemetry, incident
  timelines, request behavior, runtime errors, and service-owner
  interpretation only as complementary systems.
- The future page must stay clear that runtime systems, tests, incident
  response, and service owners remain authoritative for observed behavior,
  production traffic, endpoint performance, outage cause, operational safety,
  incident conclusions, and release decisions.
- The spec intentionally does not name specific observability vendors as
  shipped integrations.
- The spec forbids runtime proof, production traffic claims, endpoint
  performance proof, incident truth claims, outage-cause claims, release-safety
  claims, operational-safety claims, complete-coverage claims, AI/LLM impact
  analysis claims, and replacement-of-human-judgment claims.
- The spec forbids publishing raw source snippets, raw SQL, config values,
  secrets, local absolute paths, raw repository remotes, generated scan
  directories, private sample names, raw facts, raw SQLite indexes, analyzer
  logs, raw telemetry payloads, incident timelines, customer data, service
  names, production identifiers, dashboard screenshots, command output,
  hidden validation details, and credential-like values.
- Future implementation should choose placement only after checking current
  site information architecture and route availability.
- `/static-vs-runtime/` is already a shipped concept page produced by the
  `site-tracemap-tools-static-vs-runtime-telemetry` spec. Before selecting
  placement, future implementation must decide whether this field guide extends
  the existing `/static-vs-runtime/` page or justifies a distinct
  `/static-vs-runtime-field-guide/` route.
- Extending the existing `/static-vs-runtime/` page is preferred if the
  field-guide content fits as a deeper practical section without weakening the
  existing page's claim boundaries.
- If a distinct route is selected, record why a second concept-level
  static-versus-runtime surface is warranted, how the two pages cross-link, and
  how duplicate discovery entries are avoided.
- Future implementation should keep the field guide out of primary navigation
  unless an information-architecture decision records otherwise.
- `hintCategory` for the discovery metadata entry has not been selected yet.
  Future implementation must choose an existing allowed value from the current
  site discovery schema and record the value and rationale here before writing
  the discovery metadata entry. At spec time, allowed values are `start`,
  `evidence`, `limitations`, `demo`, `repo-doc`, `roadmap`, and `use-case`;
  `use-case` or `evidence` are likely fits.
- `preferredProofPath` for the discovery metadata entry has not been selected
  yet. Future implementation must verify an existing public-safe route at
  implementation time. Candidate routes include `/docs/`, `/validation/`,
  `/limitations/`, `/outputs/`, and `/proof-paths/`. Record the selected route
  and verification result here before writing the discovery metadata entry. Do
  not use a route that does not resolve in generated output.
- The forbidden-wording wrapper mechanism has not been selected yet. Future
  implementation must document the chosen HTML, CSS, or data-attribute pattern
  here before writing validation tests that depend on it. Acceptable patterns
  include a `data-forbidden-wording-example` attribute, a dedicated CSS class
  such as `tracemap-forbidden-example`, or an equivalent site pattern that
  validation scripts can query without relying on surrounding prose position.
- Stable anchors required at implementation time are `#different-questions`,
  `#how-to-use-both`, `#reading-static-evidence`, `#runtime-authority`,
  `#non-claims`, `#proof-paths-and-limitations`, and `#related-links`. If the
  guide is folded into a host page and any anchor collides, prefix with
  `static-runtime-field-guide-` and record the mapping here.

## Review Commands

Planned initial reviews:

```bash
node scripts/kiro-review.mjs --phase site-tracemap-tools-static-vs-runtime-field-guide --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase site-tracemap-tools-static-vs-runtime-field-guide --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
```

If a requested model, wrapper, or tool is unavailable, record the exact
blocker here. If Medium or higher actionable findings are patched, run one
bounded re-review and record the result.

## Review Outcomes

Initial Kiro review commands:

```bash
node scripts/kiro-review.mjs --phase site-tracemap-tools-static-vs-runtime-field-guide --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase site-tracemap-tools-static-vs-runtime-field-guide --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
```

Both commands completed with reduced coverage because Kiro reported denied
tool access after reading the spec and producing review text. Saved artifacts:

- `.tmp/kiro-reviews/site-tracemap-tools-static-vs-runtime-field-guide/2026-06-26T000605-775Z-spec-claude-opus-4.8.clean.md`
- `.tmp/kiro-reviews/site-tracemap-tools-static-vs-runtime-field-guide/2026-06-26T000605-775Z-spec-claude-opus-4.8.meta.json`
- `.tmp/kiro-reviews/site-tracemap-tools-static-vs-runtime-field-guide/2026-06-26T000931-212Z-spec-claude-sonnet-4.6.clean.md`
- `.tmp/kiro-reviews/site-tracemap-tools-static-vs-runtime-field-guide/2026-06-26T000931-212Z-spec-claude-sonnet-4.6.meta.json`

Findings and dispositions:

- Medium (`design.md`, `requirements.md`, `implementation-state.md`): overlap
  with the shipped `/static-vs-runtime/` route and sibling
  `site-tracemap-tools-static-vs-runtime-telemetry` spec was not reconciled.
  Disposition: patched by requiring future implementation to extend the
  existing route when possible or justify a distinct route, cross-linking, and
  duplicate-discovery avoidance.
- Medium (`implementation-state.md`): `hintCategory` was not recorded as a
  pending discovery decision. Disposition: patched by adding a pending decision
  note with the current allowed values and likely candidates.
- Medium (`implementation-state.md`): `preferredProofPath` was not recorded as
  a pending discovery decision. Disposition: patched by adding a pending
  decision note requiring route verification before metadata is written.
- Medium (`design.md`, `tasks.md`, `implementation-state.md`):
  forbidden-wording example wrapper mechanism was not specific enough for
  future validation. Disposition: patched by adding wrapper options and a task
  to document the chosen implementation mechanism.
- Low (`requirements.md`, `design.md`): `hintCategory` guidance used
  orientation terms that are not valid schema values. Disposition: patched by
  naming the current allowed set.
- Low (`requirements.md`): forbidden-material lists omitted command output,
  hidden validation details, and credential-like values in two normative
  locations. Disposition: patched.
- Low (`tasks.md`, `implementation-state.md`): stable anchors, including
  `#runtime-authority`, were not explicitly enumerated in implementation
  validation tasks. Disposition: patched.

Bounded Kiro re-review:

```bash
node scripts/kiro-review.mjs --phase site-tracemap-tools-static-vs-runtime-field-guide --kind re-review --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
```

The command completed with reduced coverage because Kiro reported denied tool
access after reading the spec and producing review text. Saved artifacts:

- `.tmp/kiro-reviews/site-tracemap-tools-static-vs-runtime-field-guide/2026-06-26T001319-166Z-re-review-claude-opus-4.8.clean.md`
- `.tmp/kiro-reviews/site-tracemap-tools-static-vs-runtime-field-guide/2026-06-26T001319-166Z-re-review-claude-opus-4.8.meta.json`

Re-review findings and dispositions:

- Medium (`requirements.md`): Requirement 5 omitted the
  `Where runtime tools remain authoritative` section and `#runtime-authority`
  anchor required by `design.md`, `tasks.md`, and validation expectations.
  Disposition: patched by adding the required section and stable anchor to
  Requirement 5.
- Low (`requirements.md`): top-level Claim Boundaries omitted dashboard
  screenshots from the forbidden-material list. Disposition: patched.
- Low (`requirements.md`): Requirement 6 `sourceType` wording referenced
  non-schema source types without naming the current closed set. Disposition:
  patched by stating the current schema allows only `site-page` and `repo-doc`
  and defaults to `site-page` unless extended.
- Low (`requirements.md`): Requirement 2 runtime-tools necessity wording was
  narrower than its own runtime examples. Disposition: patched by adding
  incident timelines, request behavior, and runtime errors.

No further re-review is planned; the requested bounded re-review has been run
and the remaining findings were patched.

## Validation

Planned spec-packet validation before commit:

- `git diff --check`
- `./scripts/check-private-paths.sh`
- Confirm diff is limited to
  `.kiro/specs/site-tracemap-tools-static-vs-runtime-field-guide/`

Future implementation validation expected by this spec:

- `npm test` from `site/`
- `npm run validate` from `site/`
- `npm run build` from `site/`
- `git diff --check`
- `./scripts/check-private-paths.sh`
- Desktop and mobile browser sanity checks when route, layout, or interaction
  changes are made.

Validation results:

- `git diff --check`: passed.
- `./scripts/check-private-paths.sh`: passed.
- Diff scope confirmed limited to
  `.kiro/specs/site-tracemap-tools-static-vs-runtime-field-guide/`.

Site implementation validations such as `npm test`, `npm run validate`,
`npm run build`, and browser sanity checks are deferred because this packet is
spec-only and does not change site source, generated output, layout, route, or
interaction behavior.

## Oddities

- This packet intentionally overlaps with the shipped `/static-vs-runtime/`
  concept route, but it is framed as a practical field guide or article with
  stronger handoff, metadata, and forbidden-wording validation expectations.
- Browser sanity checks are not required for this spec-only packet because no
  site layout, route, or interaction changes are implemented.

## Follow-Ups

- Future implementation should use this packet to choose placement, update
  site source, add focused validation, and run the full site validation
  workflow.
