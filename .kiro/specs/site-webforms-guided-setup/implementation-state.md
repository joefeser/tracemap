# Site Web Forms Guided Setup Implementation State

Status: current main integrated; review corrections validated locally
Readiness: correction commit, push, and exact-head ACK pending
Public claim level: `shipped` workflow with `demo`-bounded synthetic proof

Last updated: 2026-10-03
Implementation branch: `codex/site-webforms-guided-setup`
Target base: `main`
Exact base: `684acb3457d1942fb7fde43db77c5cb27e5d1648`
PR #803 ancestry at exact base: `not-shipped`
Implementation revision: `bf5b96808631d8196cfef3c7ba051185481d47a1`
Issue: #805

## Scope and delivery

This branch adds `/webforms/` as the public landing and guided-setup route for
the native Web Forms terminal workflow. It changes only site source, focused
validators, and this site-prefixed spec record. Generated `site/dist` and
`site/output` content remains untracked and was not edited by hand.

The page explains website folder, solution, explicit C#/VB project,
projectless Web Site, all/subset forms, `forms.txt`, pause/continue, private
saved configuration, build consent, external Windows compilation, explicit
project addition, isolated repair, stop conditions, and owner handoff. Classic,
non-SDK, and .NET Framework builds that resolve to Windows MSBuild remain
Windows-only and stop on non-Windows hosts.

## Evidence boundary

- PRs #797-#801, `docs/WEBFORMS_NATIVE_WORKFLOW.md`, the wizard acceptance
  audit, persisted wizard/state implementation, tests, rule catalog, and
  checked-in synthetic fixtures support the shipped workflow claim.
- PR #803 merge `af05c289a799c896862983fd9f4f73a28d882d0c` remains not an
  ancestor of the exact implementation base (`git merge-base --is-ancestor`
  exited 1), so its repairs are not presented as shipped.
- The interface is a terminal wizard over shared persisted state, not a GUI,
  automatic source acquisition, or automatic whole-solution onboarding.
- Saved configuration remains private operator state. This implementation uses
  no private source, raw output, customer screenshot, or new derived proof asset.

## Integration

- Added page registry, discovery, sitemap input, and a focused shipped workflow
  row to the roadmap claim ledger.
- Added narrow links/copy to capabilities, docs, limitations, proof paths, the
  concept article, and the conservative legacy .NET/modernization matrices.
- Preserved broad Web Forms event/route/navigation rows as hidden; only the
  bounded terminal setup workflow is labeled shipped.
- The separate #806 route is present on this exact base through merged PR #814.
  This page links to it while preserving its corrected `concept` claim level;
  it does not treat the illustrative chain as extractor-observed proof.

## Review correction — 2026-10-03

After PR #814 merged, this branch merged exact `origin/main`
`684acb3457d1942fb7fde43db77c5cb27e5d1648` with a normal merge commit. The
only textual conflict was the central validator import; both focused validators
were retained.

The review repair now describes projectless solution entries, blank/missing
selection template pauses, and post-consent version probing exactly as the
shipped terminal implementation does. Active anchors are checked after removing
HTML comments, decoded published attribute values and page metadata are scanned
for private material, and the page's main boundary is compared with this
independent implementation record and verified through Git ancestry. The merged
source-plus-compiled route is linked only as a `concept`.

The site-validation workflow now fetches full Git history on pull requests and
main pushes so both ancestry relationships can be verified directly. Other
checkouts fail closed when the recorded base object is missing, and every
checkout rejects an affirmative #803 shipped claim when the repair commit is
unavailable. Missing Git objects are never described as verified.

## Validation

- `cd site && node --test scripts/webforms-guided-setup.test.mjs`: 10 passed.
- `cd site && npm test`: 1,210 passed; no failures, skips, cancellations, or todos.
- `cd site && npm run build`: passed.
- `cd site && npm run validate`: passed; 118 HTML files, 3,918 internal
  references, and 117 sitemap URLs validated.
- `./scripts/check-private-paths.sh`: passed.
- `git diff --check`: passed.
- Desktop browser at 1,440 by 1,000: one H1, no horizontal overflow, required
  stop block and concept link present, and zero console errors.
- Mobile browser at 390 by 844: one H1, no horizontal overflow, concept link
  present, and zero console errors.
- Desktop and mobile screenshots were inspected locally for wrapping and layout;
  they are validation artifacts, not committed public evidence.

## Claim boundary and remaining work

The shipped label applies only to the bounded main-backed terminal/state
workflow. Synthetic examples remain demo. Neither label proves runtime page
execution, event firing, selected branches, SQL execution, publication success,
customer compatibility, migration parity, cross-service tracing, complete
coverage, release approval, or safety. macOS site validation does not replace
Windows legacy-project build or projectless publication validation.

#807-#809 remain separate stories. The implementation revision, PR URL, and ACK
state will be appended after commit and orchestration without changing the
public claim.
