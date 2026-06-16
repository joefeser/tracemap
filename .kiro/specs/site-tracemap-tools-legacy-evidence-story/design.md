# site-tracemap-tools-legacy-evidence-story design

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

## Overview

The future implementation should add a small public-facing legacy evidence story
to `tracemap.tools`. The story is an orientation surface over deterministic
static evidence themes, not a scanner feature and not a promise that every
legacy capability is shipped.

The implementation must re-check `main` and `dev` before writing final copy.
Anything that is not promoted to `main` with checked-in public-safe proof remains
`concept`, `dev-only`, `hidden`, or omitted.

## Content Model

Use existing long-form site patterns rather than introducing new components or
runtime services.

The site already has `/legacy-validation/`, which covers adjacent themes such as
failed builds, UI event evidence, redacted summaries, public-safe shape, and
non-claims. The future implementation must decide whether this legacy evidence
story extends that page, becomes a sibling page, or remains a smaller linked
section/card set.

Recommended sections:

- Evidence principle: "No public conclusion without evidence."
- Evidence model: rule IDs, evidence tiers, coverage labels, limitations,
  public-safe proof paths, and promotion gates.
- Claim ledger: theme, current public label, proof requirement, allowed wording,
  and out-of-bounds wording. Because every starting theme is hidden, the ledger
  is more important than support descriptions.
- Artifact safety: what may be summarized publicly and what remains local-only.
- Promotion gate: what must be true before concept wording can become demo
  wording.

## Claim Labels

Use these labels consistently:

- `demo`: only for capabilities promoted to `main` with checked-in public-safe
  artifacts or generated summaries.
- `dev-only`: for capabilities present on `dev` but not promoted to `main`.
- `concept`: for planned or explanatory copy without public proof.
- `hidden`: for core capabilities whose specs require redacted validation before
  public support claims.

## Current Theme Ledger

The normative starting ledger lives in `requirements.md`. Treat it as the
single source for theme labels, proof requirements, allowed wording, and
out-of-bounds wording. Recheck it before future implementation.

The surrounding page can be a `concept` story about the evidence model, current
claim ledger, promotion gates, and safety boundaries. Individual hidden themes
must not be upgraded by implication.

## Theme Boundaries

WCF/service-reference copy may explain static evidence for config endpoints,
service contracts, operations, generated clients, `.svc` or ASMX hosts, and
metadata normalization. It must not claim service reachability, deployment, or
binding compatibility.

Remoting copy may explain static evidence for Remoting API usage,
`MarshalByRefObject`, channels, registration APIs, and config declarations. It
must not claim hosted services, exploitability, runtime connection paths, or
production use.

WebForms copy may explain markup events, code-behind handlers, designer fields,
and possible static event-to-backend paths. It must not claim a user can reach a
handler at runtime, that ViewState/postback behavior is simulated, or that a UI
path executed.

Legacy data metadata copy may explain DBML, EDMX, typed DataSet, TableAdapter,
provider, and connection-name metadata. It must not claim database existence,
runtime query execution, permissions, schema compatibility, or production data
usage.

Build diagnostics copy may explain target frameworks, project styles, toolsets,
SDK/runtime clues, restore blockers, generated-file gaps, and reduced coverage.
It must not claim a failed build is clean or prescribe unverified tool installs.

Flow composition copy may explain bounded static paths over existing facts and
edges. It must preserve reduced-coverage and analysis-gap labels and must not
upgrade syntax or structural evidence because later facts connect.

## Artifact Safety

Safe public material:

- Generated public-safe summaries.
- Label-only counts and safe descriptors.
- Rule IDs, evidence tiers, coverage labels, supporting IDs, limitations, and
  public proof paths.
- Hashes where the source fact already permits them.

Local-only material:

- Content forbidden by the canonical content-safety rules in
  `requirements.md`.

## Content-Safety Guard

The future implementation must add a deterministic rendered-content check for
the canonical content-safety rules in `requirements.md` and wire it into
`npm test` or `npm run validate`. Current `npm run validate` performs
structural site validation only; it does not scan rendered content for
forbidden tokens.

The guard runs after the site build against the rendered legacy story page or
containing page only. Existing rendered pages are out of scope unless the future
implementation modifies them for this story. It excludes `.kiro/**`, spec
source, fixture definition files, and other non-rendered source files so the
canonical rules and test fixtures do not fail their own scan.

Before writing the guard, the implementation chooses and records the concrete
target: a rendered page file/glob for a standalone page or a section
anchor/extraction strategy for a section on an existing page. If the story lands
as a new standalone page, it must satisfy the existing top-navigation validation;
adding a new top-nav entry is a broader site change because it mutates all
rendered pages.

The guard must fail if zero rendered HTML files are found and must assert that
the rendered legacy story page or section is included in the scanned set. If it
is wired into `npm test` instead of `npm run validate`, the test must build into
an isolated temp directory or otherwise guarantee it is scanning fresh output
without mutating or racing shared `site/dist`.

The preferred wiring is `npm run validate` after `buildSite()` or a temp-output
test that builds and scans isolated fresh output. The guard ships in the same
PR as the legacy story page or section so the target assertion has a rendered
page to inspect.

The check should include a failing fixture for at least one hard leak token, a
mixed-case or normalized hard-leak fixture, path-leak, connection-string,
credential-assignment, private/local URL, raw-remote, affirmative-overclaim,
conditional negation false-positive if the heuristic is used, sanctioned
negated-disclaimer, empty-output, fresh-build, legitimate artifact-name
documentation, hidden-theme-without-label failure, clean concept copy, and
boundary fixtures defined in `requirements.md`.

## Validation

The future implementation should run:

- `npm test` from `site/`.
- `npm run validate` from `site/` for structural site validation.
- The new rendered-content safety check for the canonical content-safety rules.
- `git diff --check`.
- Desktop and mobile browser sanity checks for any page, layout, or interaction
  changes.

The spec-prep PR should run Kiro spec review and `git diff --check`.
