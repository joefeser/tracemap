# Implementation State

Status: site implementation validated; commit, PR, and ACK pending

Branch: `codex/site-webforms-source-compiled-proof`

Implementation revision: `94f4873c60f1ad4dc937b172ae21bb43ed00b0f4`

Base: `origin/main` at
`5ffd4a54176c002e4c6d41ce0133eab5963ad79b`

Issue: #806

## Scope decision

This branch implements only the public-site source-plus-compiled Web Forms proof
story. It changes site source, a site-owned projection generator/input, focused
site validators, inbound links, and this spec state. It changes no scanner,
reporter, fixture, rule, or generated `site/dist` / `site/output` source.

The route is `/webforms/source-plus-compiled-proof/`. Its claim level is `demo`
because implementation produced a checked-in, allowlisted, deterministic
privacy projection bound to the exact public fixture revision. The generator
hash identifies the exact generator bytes; the bounded-input hash covers the
canonical public privacy projection, never a private scan, binary, or tree.

## Exact-main evidence inspected

- PR #801 is represented by the selected `main` merge commit and includes the
  VB.NET, compiled evidence, and Web Forms workflow baseline.
- `samples/fixture-build/lazy-constructor/` provides the external net48 website
  and provider build harness plus a public operator-workflow description.
- `samples/messy-dotnet-workspace/vb-lazy-constructor/` and the separate public
  `samples/messy-dotnet-workspace/vb-lazy-logging-provider/` fixture model the
  page handler, lazy property/constructor work, provider source used by the
  separate DLL layouts, a dynamic lookup, a literal audit path, and an
  independent `Fill` terminal.
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
  limitations. `combined.paths.compiled-il-bridge.v1` spans emitted Tier 1
  source/metadata identity edges, Tier 2 uniquely resolved bound nonvirtual IL
  calls, and Tier 3 virtual, unbound, or database-terminal candidates; the rule
  ID alone does not determine a hop's tier.

## Branch boundary

PR #803 merged into `dev` as `af05c289a799c896862983fd9f4f73a28d882d0c`
and remains outside the selected `main` baseline. Among other hardening,
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

## Public claim

Allowed: on the exact selected public fixture revision, TraceMap's deterministic
static evidence retains the displayed ordered source/compiled candidates and
the three displayed database API terminal outcomes, with explicit gaps.

Not allowed: runtime reachability or execution, selected branches, warm/cold
property state, SQL text or parameter recovery, database success, returned
values, provider selection, source/build authenticity, deployed identity,
customer compatibility, migration parity, complete tracing, release approval,
or safety.

## Implementation evidence

- Added the ordered three-outcome route and public JSON projection.
- Added generator/input digest verification, recursive protected-field checks,
  forbidden public-material checks, per-hop tier/span/rule validation,
  discovery/sitemap checks, and inbound/outbound link checks.
- Added a conditional landing-page regression: when `/webforms/` lands under
  #805, it must link to this proof. The route is not created in this #806 PR,
  preserving one story per PR.
- Promoted only the exact synthetic proof row to `demo` in the legacy .NET and
  modernization maps; broader Web Forms event/route/navigation rows remain
  hidden.

## Validation

- `dotnet test src/dotnet/tests/TraceMap.Tests/TraceMap.Tests.csproj --filter FullyQualifiedName~LazyConstructorLoggingTests --no-restore` — passed.
- `cd site && node --test scripts/webforms-source-compiled-proof.test.mjs` — 4 passed.
- `cd site && npm run build` — passed; generated output remained ignored.
- `cd site && npm run validate` — passed; 117 HTML files, 3,883 internal
  references, and 116 sitemap URLs validated.
- `cd site && npm test` — passed; 1,194 tests passed with no failures, skips,
  cancellations, or todos.
- `./scripts/check-private-paths.sh` — passed.
- `git diff --check` — passed.
- Desktop browser QA at 1,440 by 1,000 on the proof route — one H1, no
  horizontal overflow, and no console errors.
- Mobile browser QA at 390 by 844 on the proof route — one H1, all three
  evidence chains visible, no horizontal overflow, and no console errors.
- Mobile browser QA on the current inbound Web Forms article — the proof link
  is present, no horizontal overflow, and no console errors.
- `/webforms/` is intentionally absent until #805 implements that separately;
  this branch's focused validator requires it to link here whenever that route
  exists. This is a dependency boundary, not proof that the future landing
  layout has been checked.
- Commit, ready PR, and ACK remain pending at this checkpoint.

## Earlier spec validation

- `git diff --check` — passed.
- `./scripts/check-private-paths.sh` — passed.
- `node scripts/kiro-review.mjs --phase site-webforms-source-compiled-proof
  --kind spec --model auto --dry-run` — prompt generation passed; no external
  review was run.
- Focused scope inspection — only the four files under this spec directory are
  intended to change.

Site build/test/browser validation is deliberately deferred to the later
implementation PR because this spec-only branch changes no site source.

## Handoff

Implementation begins only after owner approval and should start from a freshly
fetched target branch. Keep the implementation site-only, update this file and
task checkboxes as work completes, and do not edit generated `site/dist` or
`site/output` content.

Specification review PR: https://github.com/joefeser/tracemap/pull/810

The specification PR plans #806 and deliberately does not close it. ACK review
and owner merge remain separate gates; the planning PR grants no implementation
or merge authority.

## ACK review corrections

The current-head ACK batch authorized two independent P2 specification fixes:

- the public claim is now conditionally `demo` only when a compliant public
  projection exists, with `concept` as the fail-closed fallback; and
- the bounded public input allowlist now includes the separate
  `vb-lazy-logging-provider` fixture used by the provider project and workflow
  tests.

Both corrections were validated together and do not expand this branch beyond
the four spec files.

A later exact-head Codex review identified one additional P2 tier-preservation
issue. The evidence model now treats `combined.paths.compiled-il-bridge.v1` as
a multi-tier rule family and requires every projection and validator to retain
the tier emitted for each individual edge.
