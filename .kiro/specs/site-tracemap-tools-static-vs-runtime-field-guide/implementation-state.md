# Site TraceMap Tools Static Vs Runtime Field Guide Implementation State

Status: implemented
Readiness: implemented
Public claim level: concept

## Branch

Implementation branch: `codex/impl-site-static-vs-runtime-field-guide-20260625225002`
Base: `origin/dev`
Target PR base: `dev`

Dedicated worktree: yes. The local absolute path is intentionally omitted from
checked-in notes.

## Scope

This implementation extends the existing public-safe `/static-vs-runtime/`
concept route into the requested field guide. It changes site source, focused
site validation, validation fixtures, and this spec packet's bookkeeping only.

It does not change scanner code, reducer behavior, runtime telemetry
ingestion, observability vendor integrations, client-side tracking, generated
site output, or generated scan artifacts.

## Placement Decision

Selected placement: extend the existing standalone `/static-vs-runtime/` route.

Reasoning:

- `/static-vs-runtime/` already owns the public static-versus-runtime concept
  boundary from the `site-tracemap-tools-static-vs-runtime-telemetry` spec.
- The field-guide content fits as a deeper practical expansion without
  weakening the route's concept-level metadata or non-claims.
- Reusing the existing route avoids a second concept-level discovery entry for
  nearly the same reader question.
- Cross-linking remains bounded through the existing related links on the page
  and adjacent public-safe routes.

Rejected alternatives:

- `/static-vs-runtime-field-guide/`: rejected because a second route would
  duplicate the existing concept page and add discovery overlap without a new
  proof level.
- Guide or article-family route: rejected because the current site has no
  separate field-guide family that would improve discovery over the existing
  static-versus-runtime route.
- Section on `/limitations/`: rejected because it would bury the practical
  comparison and handoff workflow under broader limitation copy.
- Section on `/incident-call/` or `/use-cases/incident-review/`: rejected
  because those routes are incident-adjacent, while this guide covers the
  broader static evidence versus runtime telemetry boundary.
- Primary navigation: rejected because this remains a concept-level guide, not
  a top-level shipped capability.

## Metadata And Discovery

The existing `/static-vs-runtime/` route metadata remains concept-level:

- `publicClaimLevel`: `concept`
- `sourceType`: `site-page`
- `hintCategory`: `use-case`
- `preferredProofPath`: `/proof-paths/`

The existing page title, description, canonical URL, Open Graph URL, sitemap
entry, and discovery entry already target `/static-vs-runtime/` and remain
compatible with the field-guide expansion. No duplicate
`/static-vs-runtime-field-guide/` route, sitemap entry, discovery entry, or
primary-navigation item was added.

Verified related routes used by the page: `/docs/`, `/validation/`,
`/limitations/`, `/outputs/`, `/proof-paths/`, `/capabilities/`, `/demo/`,
`/demo/result/`, `/static-triage/`, `/incident-call/`, and
`/use-cases/incident-review/`.

Unavailable candidate routes: none for the links selected on this page.
Candidate routes not linked in the final related-links set remain available
elsewhere in the site but were not needed for this expansion.

## Claim-Boundary Decisions

- Visible label retained: `Public claim level: concept`.
- Visible principle retained: `No public conclusion without evidence`.
- Visible core message added: `TraceMap shows static dependency evidence and
  limitations; runtime tools show observed behavior. Neither replaces the
  other.`
- Static examples are limited to public-safe evidence shapes: repository
  snapshot, commit SHA, rule IDs, evidence tiers, file paths, line spans,
  extractor versions, coverage labels, limitations, dependency references,
  route or endpoint references, contract/package/config/project/SQL-facing
  references, and analysis gaps.
- Runtime examples remain generic and are owned by runtime systems, tests,
  incident-response roles, release-process roles, and service owners.
- No specific observability vendor is named as a shipped integration.
- No forbidden raw/private material, production identifier, runtime payload,
  command output, or dashboard screenshot is published.
- The forbidden-wording wrapper for future teaching examples is
  `data-forbidden-wording-example`. The current page does not publish a
  forbidden-wording example, but validation now strips that marked wrapper for
  claim-wording checks and still scans the full page for private/raw material.

## Implemented Page Shape

Required anchors implemented on `/static-vs-runtime/`:

- `#different-questions`
- `#how-to-use-both`
- `#reading-static-evidence`
- `#runtime-authority`
- `#non-claims`
- `#proof-paths-and-limitations`
- `#related-links`

The comparison table uses scoped column headers for static question, TraceMap
evidence shape, runtime question, runtime owner or system, limitation, and
handoff. On mobile, the page keeps document width stable and lets the table
scroll inside its wrapper.

## Validation

Site validation completed for this implementation:

- `npm test` from `site/`: passed.
- `npm run build` from `site/`: passed.
- `npm run validate` from `site/`: passed.
- Desktop browser sanity: passed for required anchors, table headers, no
  document overflow, and no browser console errors.
- Mobile browser sanity: passed for required anchors, table headers, no
  document overflow, table-wrapper horizontal scroll, and no browser console
  errors.

Pending final local validation before commit:

- `git diff --check`
- `./scripts/check-private-paths.sh`
- `git diff --name-only origin/dev...HEAD` scope confirmation

## Review Loop Notes

The PR-loop ACK is a post-PR step and cannot be completed until the
implementation is pushed and the PR exists. The final handoff should report the
terminal ACK decision for the exact pushed head. This checked-in state avoids
recording a local worktree path or post-review command output that would make
the reviewed head stale.

## Oddities

- This phase intentionally builds on the prior static-versus-runtime telemetry
  route instead of creating a second route. The result is a fuller guide on the
  existing concept page.
- The focused validator now allows future marked forbidden-wording teaching
  examples while continuing to reject the same phrases in normal page copy.

## Follow-Ups

- If a future information-architecture review creates a guide/article family,
  reassess whether `/static-vs-runtime/` should remain the canonical route or
  link to a more detailed child route.
- Keep future copy bounded to deterministic static evidence, visible
  limitations, and generic runtime-owner handoff language.
