# site-tracemap-tools-legacy-evidence-story implementation state

Status: ready-for-implementation / not started
Public claim level: concept

## Branch

Spec preparation branch: `codex/site-legacy-evidence-story`

Future implementation branch: not started

## Scope

This spec queues a bounded public legacy evidence story for `tracemap.tools`.
The story should explain how TraceMap can talk about legacy-adjacent static
evidence without implying runtime proof or shipped support that is not verified
on `main`.

This PR creates spec files only. It does not edit site source, add routes,
change scanner or reducer behavior, add public artifacts, or implement the
future page.

## Current Claim Position

Keep the public claim level at `concept`.

The referenced legacy themes include capabilities with mixed maturity: some
implemented or ready on `dev`, some hidden pending redacted validation, and some
not yet verified through checked-in public-safe artifacts on `main`. Until
promotion and proof are rechecked during implementation, public copy must label
these as `concept`, `dev-only`, or `hidden`, or omit them.

Current theme labels for the implementation starting point:

| Theme | Current public label |
| --- | --- |
| WCF/service-reference mapping | hidden |
| WCF metadata normalization | hidden |
| .NET Remoting detection | hidden |
| WebForms event flow | hidden |
| Legacy data metadata | hidden |
| Build environment diagnostics | hidden |
| Flow composition reporting | hidden |

`Concept` applies to the page/story shape. It does not upgrade hidden core
capability into public support wording.

The future implementation must update this note with the per-theme promotion
check outcome, including negative results such as "confirmed not on `main`" or
"public proof not available."

## Main/Dev Boundary

- `main` wording may describe only behavior promoted to `main` and backed by
  checked-in public-safe demos, generated summaries, or documentation.
- `dev` wording must be explicit: use `dev-only` or `concept` labels and avoid
  implying shipped behavior.
- Hidden core capability should remain hidden in public copy until redacted
  validation summaries or checked-in public fixtures exist.
- Any upgrade to `demo` requires a future implementation-state update with the
  exact artifacts and validation commands used.

## Claim Boundaries

- Shared site principle: No public conclusion without evidence.
- Safe public vocabulary: rule IDs, evidence tiers, coverage labels,
  limitations, safe descriptors, hashes, generated public summaries, supporting
  IDs, commit/source provenance, and extractor versions.
- Do not claim runtime behavior, UI reachability, production traffic,
  deployment state, endpoint performance, exploitability, database existence,
  package compatibility, incident cause, release approval, or release safety.
- Do not publish content forbidden by the canonical content-safety rules in
  `requirements.md`.
- Do not describe TraceMap as AI, LLM, embedding, vector-database, or
  prompt-based impact analysis.

## Content-Safety Guard

The future implementation must add a new deterministic rendered-content safety
check for the canonical content-safety rules in `requirements.md` and wire it
into `npm test` or `npm run validate`. Current `npm run validate` is structural
site validation and does not perform this content-safety scan.

The guard must run after build against the rendered legacy story page or
containing page only, and exclude `.kiro/**`, spec source, fixture definitions,
and other non-rendered source files. Existing rendered pages are out of scope
unless the future implementation modifies them for this story.

Before writing the guard, the future implementation must record the concrete
target path or section-anchor extraction strategy. If the story lands as a new
standalone page, it must satisfy the existing top-navigation validation; adding
a top-nav entry should be treated as a broader all-pages site change.

The guard must fail on zero rendered HTML files, assert that the rendered legacy
story page or section is included in the scanned set, and scan freshly built
output. Prefer `npm run validate` after `buildSite()` or an isolated temp-output
test that builds before scanning. If wired into `npm test`, the test must not
scan stale or shared `site/dist`.

## Source Themes

- WCF/service references and WCF metadata normalization.
- .NET Remoting detection.
- WebForms event flow.
- Legacy data metadata.
- Legacy build environment diagnostics.
- Legacy flow composition and reporting.

Each theme must remain bounded to static evidence unless public-safe demo proof
exists on `main`.

## Existing Surfaces

The site already has `/legacy-validation/`, which covers adjacent themes such as
failed builds, UI event evidence, redacted summaries, public-safe shape, and
non-claims. The future implementation must decide whether this legacy evidence
story extends that page, becomes a sibling page, or remains a smaller linked
section/card set.

## Validation Plan

Spec-prep validation:

- Run Kiro spec review with Opus and Sonnet models if configured.
- Patch Medium+ findings.
- Run `git diff --check`.

Future implementation validation:

- Recheck `main` versus `dev` promotion state.
- Run `npm test` from `site/`.
- Run `npm run validate` from `site/` for structural site validation.
- Add and run the new rendered-content safety check for the canonical
  content-safety rules from `requirements.md`.
- Run `git diff --check`.
- Run desktop and mobile browser sanity checks for any layout or interaction
  changes.

## Follow-Ups

- During implementation, decide whether this lands as a new concept page or a
  smaller section on an existing legacy/concept surface.
- Upgrade individual themes to `demo` only after checked-in public-safe proof is
  available and documented.
- Link future public-safe sample summaries if a legacy evidence pack lands.
