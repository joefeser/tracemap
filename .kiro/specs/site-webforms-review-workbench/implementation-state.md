# Implementation State

Status: specification complete; site implementation not started

Branch: `codex/site-webforms-review-workbench-spec`

Base: `origin/main` at
`d76358f663ce40532fe0954ca9888612d51a4121`

Issue: #808

Specification PR: https://github.com/joefeser/tracemap/pull/813

## Scope decision

This branch defines only the public-site contract for a Web Forms
review-workbench walkthrough. It intentionally changes no `site/src` content,
scanner/reporter/workbench implementation, public fixture, rule catalog, test,
or generated site output.

The planned route is `/webforms/review-workbench/`. Its preferred `demo` level
is conditional on the #806 public-safe proof route and projection being present
and validated on the implementation base. The spec does not pre-approve a demo
claim. Without that dependency, the route remains `concept` or is deferred.

## Exact-main evidence inspected

- PR #796 added readable compiled Web Forms proof into the workbench.
- PR #797 added the native source-plus-compiled/attachment review workflow,
  grouped compiled reports, bounded evidence query, immutable checkpoints, and
  private workbench/handoff surfaces.
- PR #801 promoted that baseline to the selected exact `main` commit.
- `docs/WEBFORMS_NATIVE_WORKFLOW.md` describes the private workbench as static,
  potentially partial, and review-only; its grouped compiled paths supplement
  page chains and do not upgrade page verdicts.
- `docs/WEBFORMS_AGENT_HANDOFF.md` keeps coverage, packet truncation, page-path
  truncation, and bounded query omissions separate. Its evidence queries are
  read-only and source-free, with explicit output/depth/child bounds.
- Exact-main packet code and tests retain client and server behavior inventories
  independently at 10,000 rows, mark overflow with their separate gap kinds,
  and set packet truncation. This evidence does not establish any broader
  10,000-row workbench limit or complete inventory.
- The lazy-constructor public fixture and focused tests provide the synthetic
  ordered route, unresolved command candidate, literal/Fill contrast, and gaps
  expected to be projected by #806.

## Broader issue boundary

Issue #744 remains open. It requests a broader private application workbench for
every selected surface, per-surface handoffs, deterministic output, private
source opt-in, and other full-application behaviors. Historical spec notes show
substantial delivered work, but the open issue is not proof that every requested
feature is complete. This site story demonstrates how to review one public
synthetic proof and does not close or supersede #744.

## Dev-only repair boundary

PR #803 merged to `dev` as
`af05c289a799c896862983fd9f4f73a28d882d0c` and is not an ancestor of the
selected `main` baseline. Its safety, diagnostics, scan-identity assertions,
and documentation/regression hardening remain dev-only until promoted. The
future page must not cite those repairs as shipped on main.

The independent 10,000-row behavior inventory caps are present in the selected
main implementation itself. They may be described from exact-main code/tests,
while #803-only explanatory repairs remain outside the claim.

## Dependencies and overlap

- #806 owns `/webforms/source-plus-compiled-proof/` and the public-safe proof
  projection used by this walkthrough.
- #805 owns `/webforms/` and workflow/setup entry.
- #807 may own the reproducible local-demo artifact map; this page links rather
  than duplicating those instructions.
- `/review-room/demo-path/` stays the generic concept-level review sequence.
- `/legacy-modernization/review-handoff/` stays the broader owner handoff.
- #809 may refresh capabilities/roadmap only after the route's actual claim
  level and proof dependency are verified.

## Planned public claim

Allowed after the #806 proof gate: this checked-in synthetic projection
demonstrates how a reviewer can follow the displayed static source/compiled
evidence, inspect provenance and an unresolved value/gap, and decide the next
bounded question.

Not allowed: a live or public product workbench, runtime execution, complete
application coverage, every #744 feature, current external-input validation,
source/build authenticity, customer compatibility, migration parity, migration
effort, release approval, or operational safety.

## Spec validation

Passed on 2026-10-03:

- `git diff --check`
- `./scripts/check-private-paths.sh`
- `node scripts/kiro-review.mjs --phase site-webforms-review-workbench --kind spec --model auto --dry-run`
- focused inspection confirmed that only the four files under this spec
  directory changed

Site build/test/browser validation is deferred to the later implementation PR
because this branch changes no site source.

## Handoff

Implementation begins only after owner approval and should start from a freshly
fetched target branch containing the approved proof dependency. Keep it
site-only, update task checkboxes and this note as work completes, and never edit
generated `site/dist` or `site/output` content.
