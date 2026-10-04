# Site Web Forms Local Demo Implementation State

- Status: implementation complete; ready PR under review
- Issue: #807
- Branch: `codex/site-webforms-local-demo`
- Base: `origin/main`
Exact base: `79ac39cf4459e913944786d755566568d02eccab`
PR #803 ancestry at exact base: `not-shipped`
- Route: `/webforms/local-demo/`
- Initial validated implementation commit: `3843558a`
- Pre-review handoff head: `289a2906b98c6ff8cd67c301ab1a284deabccc72`
- Pull request: `https://github.com/joefeser/tracemap/pull/816`
- Public claim level: `demo` for the exact-tree synthetic validation receipt;
  `concept` for the #806 illustrative path projection; `hidden` for retained raw
  artifacts and unavailable evidence

## Scope and route ownership

This site-only branch implements issue #807 from freshly fetched `origin/main`.
It adds the evaluator-facing reproduction and artifact-orientation route. The
existing `/webforms/` route continues to own guided configuration and consent,
while `/webforms/source-plus-compiled-proof/` owns the allowlisted illustrative
path projection. The later #808 route will own the review-workbench walkthrough.

No scanner, reducer, Web Forms workflow, fixture, or generated `site/dist` /
`site/output` content is changed. The page introduces no new machine-readable
public derivative; it reuses #806's checked-in projection.

## Verified implementation-time evidence

- Exact starting `origin/main`:
  `79ac39cf4459e913944786d755566568d02eccab`.
- PR #803 merge `af05c289a799c896862983fd9f4f73a28d882d0c` is not an
  ancestor of that base, so its diagnostic and scheduling repairs remain
  excluded from shipped wording.
- PR #801 head `afe6152ccb58ad11b61ef4782ba82afdfe7651b7`
  and merged main commit `d76358f663ce40532fe0954ca9888612d51a4121`
  share tree `1163de1ea0bc4f57cd762ebc3a85ee014bc6fff2`.
- Durable run `https://github.com/joefeser/tracemap/actions/runs/37125942534`
  completed successfully on the tested PR head. Its retained local-only receipt
  records 148 passed, zero failed/skipped, two authentic Windows publication
  cases, two profile dynamic-lookup cases, five operator layouts, and four
  diagnostic layouts on Windows Server 2025.
- The source validation receipt records rule
  `validation.deep-projectless-corpus.v1`, Tier2Structural, generator digest
  `529f399c5492f89d12785beb091eda70b7ea6191be60c8fd53a81e818027ec39`,
  and bounded-input digest
  `90120a5650f5da8472f7ffc734b05a74e9b4ef2075d123fafc35f07addc96a12`.
  The bounded input includes the admitted source roster, execution/fixture
  assemblies, and test receipt; it is not the public projection's input hash.
- The reused #806 projection remains concept-level and separately records
  generator digest
  `a9aae27d806b21bb1d8c48f861d0b82533e0862f1c8b12e1683ad58027f4946c`
  and privacy-projected-input digest
  `23000bf21810695f70f3b5e9f96f460037c1611534b8a5ab7d63a8d63effcd54`.

## Public implementation

- Added the exact-tree replay choice, prerequisites, fresh-clone recipe,
  artifact-role map, five layout rows, four query views, selected projected
  examples, exact receipt table, fail-closed outcome matrix, gaps, non-claims,
  and owner next steps.
- Added page/discovery/sitemap metadata and focused secondary links from guided
  setup, source proof, validation, examples, outputs, limitations,
  capabilities, roadmap, and the legacy .NET/modernization matrices.
- Added a focused validator and mutation tests for route structure, active
  links, provenance, distinct hash domains, Windows boundaries, layouts/views,
  failure outcomes, branch ancestry, discovery/sitemap metadata, and
  private/raw/overclaim safety.

## Claim boundary

The `demo` claim is limited to the named checked-in public synthetic corpus and
its exact-tree durable result. The 148-pass count is historical with respect to
later main revisions and is not current-head proof. The #806 path details remain
`concept` because independent extractor output is not checked in for their
illustrative hop IDs, tiers, and spans.

The page does not establish runtime page or database execution, branch
feasibility, runtime dispatch, provider selection, parameter values, customer
compatibility, private application coverage, migration parity, complete
cross-service tracing, physical drive-I/O measurement, release approval, or
safety. Raw SQLite, facts, logs, TRX, binaries, source, SQL, command bodies,
configuration, credentials, connection material, local paths, identities,
customer artifacts, and unpublished validation output remain hidden.

## Validation

- Focused route regressions: 10 passed, 0 failed.
- Full site tests: 1,220 passed, 0 failed, skipped, cancelled, or todo.
- `npm run build`: passed.
- `npm run validate`: passed across 119 HTML files, 3,959 internal
  references, and 118 sitemap URLs.
- `./scripts/check-private-paths.sh`: passed.
- `git diff --check`: passed.
- Exact-tree disposable worktree guard suite: passed and remained explicitly
  orchestration-only, not native acceptance.
- Exact-tree default local replay: 146 passed, 0 failed, 1 explicit
  Windows-only skip. This run does not replace the durable Windows result and
  does not establish Windows publication on this host.
- Desktop at 1440 by 1000 and mobile at 390 by 844: one H1, no horizontal
  overflow, responsive artifact-map treatment present, linked guided-setup and
  source-proof routes resolved, and no browser-console errors or warnings.
- Ready PR #816 targets `main`. The first ACK pass authorized six current-head
  review-thread repairs covering copy accuracy, visible-only evidence checks,
  broader local-path rejection, graceful missing-file handling, and shallow
  history. A current-head follow-up required adjacent receipt rule, tier,
  coverage, and provenance metadata for the `reversed` and `repeat` examples;
  that focused correction and its regression are awaiting the next exact-head
  ACK pass.

## Remaining work

#808 and #809 remain separate site stories. This file will be updated with the
final head, validation counts, browser results, PR URL, and ACK state before the
implementation handoff.
