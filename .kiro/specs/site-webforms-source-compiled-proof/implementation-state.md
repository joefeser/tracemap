# Implementation State

Status: specification complete; site implementation not started

Branch: `codex/site-webforms-source-compiled-proof-spec`

Base: `origin/main` at
`d76358f663ce40532fe0954ca9888612d51a4121`

Issue: #806

## Scope decision

This branch defines only the public-site contract for the source-plus-compiled
Web Forms proof story. It intentionally changes no `site/src` files, scanner or
reporter code, fixtures, rules, tests, generated site output, or public claims.

The planned route is `/webforms/source-plus-compiled-proof/`. Its preferred
claim level is `demo`, contingent on implementation producing a checked-in,
allowlisted, privacy-projected proof artifact that is bound to the exact public
fixture revision. If that condition cannot be met, implementation must keep the
route at `concept`; the spec does not pre-approve a demo claim.

## Exact-main evidence inspected

- PR #801 is represented by the selected `main` merge commit and includes the
  VB.NET, compiled evidence, and Web Forms workflow baseline.
- `samples/fixture-build/lazy-constructor/` provides the external net48 website
  and provider build harness plus a public operator-workflow description.
- `samples/messy-dotnet-workspace/vb-lazy-constructor/` and the separate public
  provider fixture model the page handler, lazy property/constructor work, a
  dynamic lookup, a literal audit path, and an independent `Fill` terminal.
- `LazyConstructorLoggingTests` pins mixed and compiled-only route ordering,
  separates the dynamic and literal outcomes, retains unresolved operand and
  virtual-dispatch gaps, excludes same-name decoys, and checks bounded
  generator/input hashes. These are static synthetic regressions, not public
  runtime output.
- The relevant rule catalog includes `dotnet.compiled.member.v1`,
  `dotnet.compiled.il-body.v1`, `dotnet.compiled.il-call.v1`,
  `dotnet.compiled.il-values.v1`, `dotnet.compiled.il-command-binding.v1`,
  `combined.paths.compiled-command-value.v1`,
  `combined.paths.compiled-il-bridge.v1`, and
  `combined.paths.projectless-publish-candidate.v1`, each with explicit
  limitations.

## Branch boundary

PR #803 merged into `dev` as `af05c289a799c896862983fd9f4f73a28d882d0c`
but is not an ancestor of the selected `main` baseline. Among other hardening,
it adds explicit lazy-corpus scan-identity assertions and related Web Forms
safety/diagnostic repairs. The eventual story may describe those changes only
as `dev`-only until a later promotion is verified. It must not use #803 to
strengthen an exact-main proof claim.

## Dependencies and overlap

- #805 is expected to own `/webforms/` and link to this proof route.
- #807 should reuse this story's public proof asset and provenance instead of
  generating a conflicting copy.
- #808 should use this route as the evidence walkthrough into the review
  workbench.
- #809 should update capability and roadmap wording only after the preceding
  routes exist and their claim levels are verified.
- Existing concept and handoff pages remain authoritative for their broader
  topics; this story promotes only the exact synthetic proof it can show.

## Planned public claim

Allowed: on the exact selected public fixture revision, TraceMap's deterministic
static evidence retains the displayed ordered source/compiled candidates and
the three displayed database API terminal outcomes, with explicit gaps.

Not allowed: runtime reachability or execution, selected branches, warm/cold
property state, SQL text or parameter recovery, database success, returned
values, provider selection, source/build authenticity, deployed identity,
customer compatibility, migration parity, complete tracing, release approval,
or safety.

## Spec validation

- `git diff --check` — passed.
- `./scripts/check-private-paths.sh` — passed.
- Focused scope inspection — only the four files under this spec directory are
  intended to change.

Site build/test/browser validation is deliberately deferred to the later
implementation PR because this spec-only branch changes no site source.

## Handoff

Implementation begins only after owner approval and should start from a freshly
fetched target branch. Keep the implementation site-only, update this file and
task checkboxes as work completes, and do not edit generated `site/dist` or
`site/output` content.
