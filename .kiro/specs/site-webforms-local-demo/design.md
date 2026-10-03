# Site Web Forms Local Demo Design

## Placement and relationship to adjacent stories

- Canonical route: `/webforms/local-demo/`
- Guided entry point: `/webforms/` from issue #805
- Reused proof projection: `/webforms/source-plus-compiled-proof/` and its
  public-safe asset from issue #806
- Downstream review path: `/webforms/review-workbench/` from issue #808
- Later status reconciliation: issue #809

The local-demo page owns reproduction, environment boundaries, artifact
orientation, and failure interpretation. The guided setup page owns choosing
and resuming configuration. The source-plus-compiled proof page owns the
evidence chain and projected proof asset. The workbench page owns reviewer
inspection. Existing `/blog/modernizing-web-forms-without-running-it/`,
`/legacy-dotnet/evidence/`, `/legacy-modernization/evidence-map/`, and
`/legacy-modernization/review-handoff/` remain the concept overview, evidence
status, planning map, and handoff guidance respectively.

## Page sequence

1. **What this demo answers** — a short evaluator question, `demo` label, and
   static-evidence boundary.
2. **Choose the correct replay** — default cross-platform synthetic replay
   versus Windows-only `-RequireWindowsPublish`, with prerequisites and explicit
   skip semantics.
3. **Fresh-clone recipe** — checked-out commit/tree identity, clean dependency
   setup, fresh output directory, command, durable result location, and the rule
   that failures stay inspectable. The public page may show the repository
   command but never a user-specific path.
4. **Artifact map** — responsive table/cards mapping each retained artifact
   class to its question, public visibility, and limitation.
5. **Five layouts** — attached, separate, separate-DLL-only, reversed, and
   missing-provider. Each row names the admitted inputs and whether a route or
   explicit gap is expected.
6. **Four query views** — all terminals, Fill-only, capped, and repeat. Explain
   that a filtered zero-unresolved subset does not resolve another command and
   that cap/repeat assertions do not prove completeness.
7. **Selected evidence** — render allowlisted summaries from issue #806's
   public-safe projection. Do not client-fetch or expose the local-only
   validation receipt, raw path reports, raw indexes, facts, logs, or source.
8. **Validation receipt** — exact tested head, tree-equivalent merge when
   applicable, durable run, platform, command mode, counts, hash provenance,
   and Windows publication status.
9. **How failure stays evidence** — refusal/error matrix and owner next steps.
10. **What remains unknown** — unresolved values, candidate bridges, partial
    coverage, runtime/customer/migration non-claims, and the #803 main boundary.
11. **Continue the review** — guided setup, proof, workbench, validation,
    limitations, and claim-ledger links.

## Artifact-map contract

The public map describes artifact *roles*, not artifact contents:

| Artifact class | Review question | Public treatment |
| --- | --- | --- |
| Validation receipt | Which bounded generator/input and test result were admitted? | Project only allowlisted hashes, counts, platform, mode, rule, tier, limitations, and durable result URL. |
| Test receipt | Did the named synthetic suite pass, fail, or skip? | Summarize exact counts; do not publish raw TRX. |
| Scan/report family | What source or compiled scope was indexed? | Link to the #806 projection; do not publish raw facts, SQLite, logs, or report bodies. |
| Combined artifact | Which independent scopes were available to the reporter? | Describe labels and source/compiled roles only; do not publish SQLite. |
| Path view | Which ordered static route or explicit gap was retained? | Render bounded allowlisted hops, rule/tier/coverage, unresolved state, and gaps from the #806 projection. |
| Cap comparison | What disappeared because of the configured result bound? | Show admitted counts/status and a truncation gap; no local HTML copy. |
| Unresolved-value view | Which value stayed unknown? | Show category and owner question; never raw SQL or scheduled/connection material. |

The page should use semantic tables on wide screens and the site's existing
overflow/card treatment on mobile. Progressive enhancement may support a
layout filter, but the complete map and limitations must remain usable without
JavaScript.

## Provenance model

The first implementation may cite the PR #801 public Windows run only after it
re-verifies these bindings at implementation time:

- PR head `afe6152ccb58ad11b61ef4782ba82afdfe7651b7`;
- `main` merge `d76358f663ce40532fe0954ca9888612d51a4121`;
- identical Git tree `1163de1ea0bc4f57cd762ebc3a85ee014bc6fff2`;
- durable run `https://github.com/joefeser/tracemap/actions/runs/37125942534`;
- 148 passed, 0 failed, 0 skipped;
- two Windows publication cases, two profile dynamic-lookup cases, five
  operator layouts, and four diagnostic layouts admitted by the receipt;
- `validation.deep-projectless-corpus.v1`, `Tier2Structural`;
- generator SHA-256
  `529f399c5492f89d12785beb091eda70b7ea6191be60c8fd53a81e818027ec39`;
- bounded-input SHA-256
  `90120a5650f5da8472f7ffc734b05a74e9b4ef2075d123fafc35f07addc96a12`;
  and
- Windows Server 2025 with required mapped/mapless publication.

These values are specification evidence, not permission to copy the run's raw
artifacts into `site/src`. If any binding changes or cannot be reverified, the
implementation must omit the count or label it historical rather than silently
claim current-head proof.

## Failure and clean-clone contract

The implementation plan must verify the documented recipe in a disposable
fresh clone or equivalent exact-tree worktree. It should exercise inexpensive
guard cases directly and rely on checked-in tests for bounded destructive or
platform-specific cases. The visible matrix includes:

- existing output root -> `DEEP_CORPUS_OUTPUT_NOT_FRESH`;
- non-Windows required publication -> `DEEP_CORPUS_WINDOWS_REQUIRED`;
- failed test process -> outputs preserved, no admitted receipt;
- missing required layout or wizard case -> validation refusal;
- input/generator mutation -> `DEEP_CORPUS_INPUT_CHANGED` and no admission;
- requested Windows publication without both cases ->
  `DEEP_CORPUS_WINDOWS_ACCEPTANCE_MISSING`;
- comparison, unresolved-ledger, cap, or repeat mismatch -> failed corpus, not
  a partial success; and
- a skipped Windows theory -> explicit not-run, never pass.

No browser demo executes PowerShell, builds projects, scans source, or reads a
user's filesystem. The site remains static-first.

## Claim and privacy design

The primary route can be `demo` because it is tied to checked-in synthetic
fixtures and a durable exact-tree result. Individual rows that only explain a
workflow stay `concept`. Unsafe or unavailable material stays `hidden`.

Required non-claims include: no fixture/database method execution, runtime
reachability or branch feasibility, chosen runtime implementation, customer
compatibility, private application coverage, parameter values, migration
parity, complete cross-service tracing, physical drive-I/O measurement,
release approval, or safety to run.

The site must never ingest or copy the downloaded Actions artifact wholesale.
A future public JSON projection is permitted only when its schema allowlists
every field and records the exact generator and bounded privacy-projected input
hashes. Machine-local paths and hashes of private/raw source inputs are not
public provenance.

## Site integration

The implementation updates:

- `site/src/_site/pages.json` for sitemap generation;
- `site/src/_site/discovery.json` with `publicClaimLevel`, limitations, and
  non-claims;
- the Web Forms entries in `/roadmap/`, `/capabilities/`,
  `/legacy-dotnet/evidence/`, and `/legacy-modernization/evidence-map/` only to
  the level supported by the new proof;
- focused links from `/webforms/`, `/webforms/source-plus-compiled-proof/`,
  `/examples/`, `/outputs/`, `/validation/`, `/limitations/`, and the later
  `/webforms/review-workbench/`; and
- the existing secondary navigation/card system, not the primary header.

## Validation design

A focused validator and mutation tests should require:

- route HTML, canonical/social metadata, a single H1, and mobile-safe artifact
  map structure;
- discovery and sitemap registration;
- inbound links from guided setup and source-plus-compiled proof;
- the required docs and adjacent-site links;
- all five layouts, all four query views, missing-provider and cap gaps;
- exact-result URL/head/tree/platform/count/hash fields when a validation row
  is present;
- separate local and Windows publication wording;
- public claim levels and explicit non-claims;
- absence of raw SQL/command text, source, raw SQLite/facts/log/TRX content,
  credentials, connection material, private identities, local paths, and
  customer data; and
- no accidental promotion of #803's dev-only repairs.

Run the focused tests, `cd site && npm test`, `npm run build`,
`npm run validate`, the private-path guard, and `git diff --check`. Browser QA
must cover desktop and mobile versions of the local demo plus its guided-setup
and source-plus-compiled inbound links, including keyboard access, overflow,
link behavior, and console errors.
