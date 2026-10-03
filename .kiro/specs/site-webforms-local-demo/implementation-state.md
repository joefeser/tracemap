# Site Web Forms Local Demo Implementation State

- Status: specification complete; site implementation not started
- Issue: #807
- Branch: `codex/site-webforms-local-demo-spec`
- Base: `origin/main`
- Base SHA: `d76358f663ce40532fe0954ca9888612d51a4121`
- Planned route: `/webforms/local-demo/`
- Planned route claim level: `demo` for exact public synthetic proof;
  `concept` for unproven workflow explanation; `hidden` for unsafe/unavailable
  material

## Scope decision

This branch contains only the site-prefixed planning specification. It does not
change `site/src`, generated output, scanner/reducer behavior, workflow scripts,
fixtures, validators, claim-ledger rows, navigation, or public claims. The
future implementation remains a separate site-only issue branch and PR.

The page will own reproducible local replay and artifact orientation. It will
reuse issue #806's source-plus-compiled privacy projection, receive an inbound
link from issue #805's guided setup, and provide proof context to issue #808's
review workbench. Issue #809 should reconcile capability and roadmap wording
only after these four routes exist.

## Verified exact-main evidence

- `scripts/wlocal.ps1` delegates to the bounded deep-corpus wrapper, requires a
  fresh output root, and never reads private operator configuration or an old
  run.
- The documented five operator layouts are `attached`, `separate`,
  `separate-dll-only`, `reversed`, and `missing`; positive layouts also retain
  `all`, `fill`, `capped`, and `repeat` query views.
- The wrapper retains failed output and writes `validation.local.json` only
  after its required corpus, layout, helper, input, generator, and optional
  Windows publication checks pass.
- PR #801's head `afe6152ccb58ad11b61ef4782ba82afdfe7651b7`
  and the `main` merge `d76358f663ce40532fe0954ca9888612d51a4121`
  have the same tree, `1163de1ea0bc4f57cd762ebc3a85ee014bc6fff2`.
- Durable Actions run 37125942534 passed on the PR #801 head. Its local-only
  receipt records 148 passed, zero skipped, two authentic Windows publication
  cases, five operator layouts, four diagnostic layouts,
  `validation.deep-projectless-corpus.v1`, `Tier2Structural`, and the source
  receipt's generator/bounded-input hashes. The receipt input includes the
  admitted source roster plus execution/fixture assembly and raw test-receipt
  hashes; it is not the future public projection's bounded input. The issue
  #806 projection must carry its own generator and privacy-projected-input
  hashes. This is synthetic exact-tree validation, not permission to publish
  the run's raw artifacts or proof of a private site.

These values must be freshly reverified during implementation. A later count
or changed tree supersedes them; an older successful count must not be relabeled
as current-head proof.

## Dev-only boundary

PR #803 merged to `dev` as
`af05c289a799c896862983fd9f4f73a28d882d0c`. At specification time it is an
ancestor of `origin/dev` and is not an ancestor of `origin/main`. Its diagnostic
and scheduling repairs therefore remain dev-only and cannot be described as
shipped by the future main-based public page unless an implementation-time
ancestry check proves promotion.

## Public boundary

The future page may explain a deterministic synthetic static workflow and show
an allowlisted privacy projection. It must not publish raw SQLite, facts, logs,
TRX, source, raw SQL or command values, credentials, connection material,
customer data, local paths, private identities, or private validation details.

The page does not establish runtime execution, database execution, branch
feasibility, runtime dispatch, customer compatibility, private application
coverage, migration parity, complete cross-service tracing, physical drive-I/O,
release approval, or safety to run. A Windows-required skip is not a pass, and
repeat success alone is not proof that an earlier intermittent failure was
fixed.

## Specification validation

- `git diff --check`: passed.
- `./scripts/check-private-paths.sh`: passed.
- Focused site build/tests/browser QA: intentionally deferred because this PR
  adds no site route, site metadata, validator, layout, or interaction.

## Implementation handoff

1. Start from a freshly fetched `origin/main` after the #805 and #806 proof
   routes are available.
2. Recheck #801 result/tree bindings and #803 ancestry before choosing wording.
3. Reuse #806's allowlisted projection; do not copy the downloaded Actions
   artifact or local-only receipt into the site.
4. Implement the page and focused integrations described in `design.md`.
5. Complete every unchecked implementation task and replace this handoff with
   exact branch/head, validation, browser, PR, and ACK evidence.
