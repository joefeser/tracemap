# Site Web Forms Guided Setup Implementation State

Status: specification complete; implementation not started
Readiness: ready for site implementation after spec acceptance
Public claim level: planned `shipped` workflow with `demo`-bounded synthetic proof

Last updated: 2026-10-03
Specification branch: `codex/site-webforms-guided-setup-spec`
Target base: `main`
Issue: #805

## Scope decision

The future implementation will add `/webforms/` as the public landing and
guided-setup route for the native Web Forms terminal workflow. This spec-only
branch intentionally does not change `site/src/`, validators, generated output,
or public claim-ledger rows.

The landing page will explain website folder, solution, explicit C#/VB project,
projectless Web Site, all/subset forms, `forms.txt`, pause/continue, saved
configuration, build consent, external Windows compilation, explicit project
addition, isolated repair, stop conditions, and owner handoff.

## Verified evidence state

- Specification base: `d76358f663ce40532fe0954ca9888612d51a4121`,
  the main merge commit of promotion PR #801.
- Main-backed delivery chain: PRs #797, #798, #799, #800 and #801.
- Primary public repository evidence:
  `docs/WEBFORMS_NATIVE_WORKFLOW.md`,
  `.kiro/specs/webforms-resumable-wizard/acceptance-audit.md`, wizard source and
  tests, rule catalog entries, and checked-in synthetic fixtures.
- Dev-only repair: PR #803 merge
  `af05c289a799c896862983fd9f4f73a28d882d0c` is an ancestor of `origin/dev`
  and is not an ancestor of the verified `origin/main`. The implementation must
  recheck this fact and must not claim those repairs as shipped until promoted.

## Placement and overlap decisions

- Chosen route: `/webforms/`.
- Existing `/blog/modernizing-web-forms-without-running-it/` remains the
  concept overview and should cross-link rather than be replaced.
- Existing `/legacy-dotnet/evidence/` and
  `/legacy-modernization/evidence-map/` remain conservative status matrices;
  only the bounded terminal workflow gains a main-backed row.
- `/roadmap/#claim-ledger` is the canonical claim-status surface. It should add
  a Web Forms workflow row without upgrading the broad legacy-validation row.
- #806 supplies the first-wave source-plus-compiled proof story. #807 and #808
  consume these orientation/proof routes later; #809 performs the broader
  capability and roadmap refresh after all four routes exist.

## Claim decision

The workflow itself may be labeled `shipped` because its implementation and
documentation are on main. Synthetic examples and proof projections remain
`demo`. Neither label supports claims about runtime execution, publication
success, customer compatibility, complete coverage, migration parity,
cross-service tracing, release approval, or safety.

The delivered user interface is a terminal wizard over shared persisted state.
No GUI wizard, automatic source acquisition, arbitrary whole-solution
onboarding, automatic projectless publication, or operator replacement is in
scope.

## Public-data boundary

The future implementation may use checked-in public synthetic evidence only.
It must not publish private source or markup, raw SQL, configuration values,
credentials, connection material, copied DLL contents, local paths, customer or
infrastructure identities, raw SQLite/facts/logs, screenshots, or private
validation details. A new derived machine-readable artifact would require the
exact generator SHA-256 and a SHA-256 of its bounded privacy-projected input.

## Validation state

For this specification-only branch:

- `git diff --check`: passed.
- `./scripts/check-private-paths.sh`: passed (`Private path guard passed.`).
- Site build/tests/browser checks: intentionally deferred because this branch
  makes no site source, route, layout, interaction, metadata, or validator
  changes.

The implementation PR must run focused and full site tests, validation, build,
private-path and diff guards, plus desktop and mobile browser checks, and record
the exact results here.

## Remaining work

All tasks under **Future implementation work** in `tasks.md` remain open. No
public site story has been implemented by this specification PR.
