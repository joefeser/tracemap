# Site TraceMap Tools Static Vs Runtime Telemetry Implementation State

Status: completed
Readiness: ready-for-implementation
Public claim level: concept

## Branch

Implementation branch: `codex/impl-site-static-vs-runtime-telemetry`
Base: `origin/dev`

## Scope

This implementation adds a public-safe concept route explaining how TraceMap's
deterministic static evidence complements, but does not replace, runtime
observability tools such as logs, traces, APM, telemetry, incident dashboards,
and production metrics.

It changes site source, route metadata, discovery metadata, focused site
validation, tests, and spec bookkeeping only. It does not change scanner code,
reducer behavior, runtime telemetry ingestion, vendor integrations, demo
artifacts, or generated site output.

## Public Claim Level

Selected level: `concept`.

Rationale: the future page is explanatory positioning. Existing public surfaces
can support TraceMap's deterministic static evidence model in general, but this
specific static-versus-runtime explanation has not shipped as a dedicated
public route or a checked-in public demo proof path. The page must therefore
stay concept-level until future public-safe proof exists for any stronger demo
claim.

## Scope Decisions

- Selected placement: standalone `/static-vs-runtime/` concept route.
- The route is added to sitemap and discovery metadata with
  `publicClaimLevel: concept`, `sourceType: site-page`, `hintCategory:
  use-case`, and `preferredProofPath: /proof-paths/`.
- The route is intentionally not added to primary navigation. Discovery comes
  through sitemap, LLM/bot-oriented discovery metadata, `/use-cases/`, and
  adjacent concept pages.
- Rejected alternate: adding a section to `/limitations/`. That would keep the
  boundary near existing limitations, but it would bury the comparison and make
  stable section-level discovery weaker than a standalone concept route.
- Rejected alternate: adding a section to `/incident-call/` or
  `/static-triage/`. Those pages are incident-adjacent and narrower; this spec
  needs a broader static-versus-runtime explanation for review and handoff.
- Rejected alternate: primary navigation placement. The page is concept-level
  orientation, not a top-level shipped capability.
- The spec recommends a future standalone route such as `/static-vs-runtime/`
  or a bounded section on an existing use-case or limitations page, with the
  final placement decided during implementation.
- The page must explain complementary responsibilities: TraceMap orients static
  repository evidence; runtime observability tools answer operational
  questions.
- The spec intentionally uses generic runtime observability language and does
  not name vendor integrations as shipped.
- The spec forbids runtime proof, production traffic, endpoint performance,
  outage-cause, release-safety, operational-safety, AI/LLM analysis, and
  complete-product-coverage claims.
- The spec forbids publishing raw static artifacts, raw runtime payloads,
  private sample names, service names, customer data, production identifiers,
  local absolute paths, raw remotes, secrets, and analyzer logs.
- No `design.md` is included. Design-level guidance is intentionally folded
  into Requirement 5, which defines page structure, and Requirement 6, which
  defines metadata and discovery behavior.
- Checked route targets at implementation time: `/docs/`, `/validation/`,
  `/limitations/`, `/outputs/`, `/proof-paths/`, `/capabilities/`, `/demo/`,
  `/demo/result/`, `/static-triage/`, `/incident-call/`, and
  `/use-cases/incident-review/` exist in the site source and sitemap metadata.
- Unavailable or moved route targets: none found during implementation.

## Spec Review Commands And Results

Planned commands:

- `node scripts/kiro-review.mjs --phase site-tracemap-tools-static-vs-runtime-telemetry --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text`
- `node scripts/kiro-review.mjs --phase site-tracemap-tools-static-vs-runtime-telemetry --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`

Results:

- `claude-sonnet-4.6` initial spec review:
  `node scripts/kiro-review.mjs --phase site-tracemap-tools-static-vs-runtime-telemetry --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`
  exited 0 with reduced coverage because Kiro reported denied shell tool
  access. Saved artifacts under
  `.tmp/kiro-reviews/site-tracemap-tools-static-vs-runtime-telemetry/2026-06-18T062042-858Z-spec-claude-sonnet-4.6.*`.
  Findings: 3 Medium, 2 Low. Patched all Medium findings and both Low
  suggestions: route-resolution fallback, task validation fallback, discovery
  non-claims parity, review-result slots, and unsupported "impacted" wording
  validation.
- `claude-opus-4.8` initial spec review:
  `node scripts/kiro-review.mjs --phase site-tracemap-tools-static-vs-runtime-telemetry --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text`
  exited 1 with reduced coverage because Kiro reported denied write/tool
  access after producing review text. Saved artifacts under
  `.tmp/kiro-reviews/site-tracemap-tools-static-vs-runtime-telemetry/2026-06-18T062042-840Z-spec-claude-opus-4.8.*`.
  Findings: 1 Medium, 1 Low-Medium, 2 Low. Patched the Medium and
  Low-Medium findings, plus the accessibility Low: section-placement metadata
  guardrails, task validation fallback, and accessible responsive comparison
  table requirements. The remaining Low noted that some candidate cross-link
  routes may be unbuilt; existing route-resolution criteria already cover that
  risk.
- `claude-opus-4.8` re-review:
  `node scripts/kiro-review.mjs --phase site-tracemap-tools-static-vs-runtime-telemetry --kind re-review --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text`
  exited 0 with reduced coverage because Kiro reported denied shell tool
  access after producing review text. Saved artifacts under
  `.tmp/kiro-reviews/site-tracemap-tools-static-vs-runtime-telemetry/2026-06-18T062402-554Z-re-review-claude-opus-4.8.*`.
  Findings: 2 Medium bookkeeping items, no substantive claim-boundary gaps.
  Patched stale pending review state and review-packet cycle wording.
- `claude-sonnet-4.6` re-review:
  `node scripts/kiro-review.mjs --phase site-tracemap-tools-static-vs-runtime-telemetry --kind re-review --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`
  exited 0 with reduced coverage because Kiro reported denied shell tool
  access after producing review text. Saved artifacts under
  `.tmp/kiro-reviews/site-tracemap-tools-static-vs-runtime-telemetry/2026-06-18T062402-599Z-re-review-claude-sonnet-4.6.*`.
  Findings: 2 Low implementation-prompting gaps. Patched primary-navigation
  task enforcement and section-placement metadata validation.
- Final re-review with `claude-sonnet-4.6`:
  `node scripts/kiro-review.mjs --phase site-tracemap-tools-static-vs-runtime-telemetry --kind re-review --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`
  exited 1 with full coverage and saved artifacts under
  `.tmp/kiro-reviews/site-tracemap-tools-static-vs-runtime-telemetry/2026-06-18T062736-556Z-re-review-claude-sonnet-4.6.*`.
  Findings: 1 Medium bookkeeping item and 2 Low consistency items. Patched by
  recording the final review run, moving all files to
  `Readiness: ready-for-implementation`, and recording validation status.
- Final re-review with `claude-opus-4.8`:
  `node scripts/kiro-review.mjs --phase site-tracemap-tools-static-vs-runtime-telemetry --kind re-review --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text`
  exited 1 with full coverage and saved artifacts under
  `.tmp/kiro-reviews/site-tracemap-tools-static-vs-runtime-telemetry/2026-06-18T062736-492Z-re-review-claude-opus-4.8.*`.
  Findings: 2 Medium review-bookkeeping items and 3 Low polish items. Patched
  by recording final full-coverage review status, updating review-packet cycle
  wording, documenting the intentional absence of `design.md`, naming the
  current discovery metadata shape, and narrowing the `npm run validate`
  fallback.

### Review Coverage Status

The first initial reviews and first re-reviews were labeled reduced coverage
because Kiro reported denied shell/write tool access after reading the spec and
producing review text. Final re-reviews with both `claude-sonnet-4.6` and
`claude-opus-4.8` completed with full coverage in their wrapper metadata. The
remaining Medium findings from the final full-coverage passes were bookkeeping
items that are patched in this state update.

Medium or higher findings must be patched and re-reviewed where feasible before
`Readiness` moves to `ready-for-implementation`.

## Validation

- `git diff --check` passed.
- `npm test` from `site/` passed: 171 tests.
- `npm run validate` from `site/` passed: 42 HTML files, 1259 internal references, 41 sitemap URLs, and 1 legacy story safety target.
- `npm run build` from `site/` passed.
- `./scripts/check-private-paths.sh` passed: `Private path guard passed.`
- Browser sanity passed with the terminal Playwright wrapper after the in-app browser blocked local navigation. Desktop width had no document overflow, six required anchors, and the comparison table fit without horizontal document scroll. Mobile width had no document overflow and the comparison table used its own horizontal scroll wrapper. Console errors: none.

## Oddities

- This spec overlaps intentionally with incident-adjacent concept pages, but it
  is narrower: it explains the boundary between static evidence and runtime
  telemetry rather than defining a triage checklist or incident-call narrative.
- This spec does not require runtime telemetry fixtures, observability vendor
  integrations, screenshots, dashboards, metrics, logs, traces, or production
  data.

## Follow-Ups

- Placement resolved in this implementation as standalone `/static-vs-runtime/`.
- Focused validation was added and wired into the site validation workflow.
- PR-loop review found two Gemini validator-hardening comments; both were patched with safer route metadata text handling, flexible impacted-boundary stripping, and regression tests.
