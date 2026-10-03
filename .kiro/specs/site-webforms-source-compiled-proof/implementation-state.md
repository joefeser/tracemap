# Implementation State

Status: current-head P1 repairs in progress; concept downgrade selected because independent extractor output is not checked in

Branch: `codex/site-webforms-source-compiled-proof`

Initial implementation revision: `94f4873c60f1ad4dc937b172ae21bb43ed00b0f4`

Base: `origin/main` at
`5ffd4a54176c002e4c6d41ce0133eab5963ad79b`

Issue: #806

## Scope decision

This branch implements only the public-site source-plus-compiled Web Forms proof
story. It changes site source, a site-owned projection generator/input, focused
site validators, inbound links, and this spec state. It changes no scanner,
reporter, fixture, rule, or generated `site/dist` / `site/output` source.

The route is `/webforms/source-plus-compiled-proof/`. Its claim level is
`concept`: the checked-in allowlisted projection is deterministic and bound to
the exact public fixture revision, but it is not independently derived from
checked-in extractor output. The generator hash identifies the exact generator
bytes; the bounded-input hash covers the canonical public privacy projection,
never a private scan, binary, or tree. Those hashes prove projection integrity,
not extractor truth.

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

Allowed: the exact selected public fixture revision, rule catalog, and focused
regression contract support a concept showing how ordered source/compiled
candidates and three database API terminal outcomes would be reviewed with
explicit gaps.

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

## Current-head implementation review repairs

ACK authorized one consolidated repair batch on PR #814 head
`ea8533309d0a3a111cf8fee380b020f6a45fe485`. The batch:

- adds a generated supporting-evidence registry and requires every hop and
  top-level gap reference to resolve to matching rule, tier, and public span;
- machine-labels the bounded result as `partial`;
- keeps the generic bridge-tier examples while downgrading every selected
  DLL-only reproduction bridge to `Tier3SyntaxOrTextual` because this public
  projection has no source/assembly binding receipt;
- aligns the literal-audit state with the checked-in
  `method-local-constant` regression;
- rejects multiline raw SQL inside parsed allowlisted values;
- compares the checked-in asset with a fresh canonical in-memory projection,
  checks rendered hop/version/digest values against the asset, aggregates
  missing or malformed provenance errors, and invokes validation based on the
  public route/asset rather than generator presence.

Post-repair validation passed: 8 focused proof tests, full `npm test`, site
build, full site validation (117 HTML files, 3,883 internal references, 116
sitemap URLs), the focused .NET regression, private-path guard, and
`git diff --check`. Desktop 1,440 by 1,000 and mobile 390 by 844 browser checks
each found one H1, all three outcomes, the partial-status disclosure, no
horizontal overflow, and no console errors.

## Exact-head P1 fail-closed correction

Codex reviewed `83e3d2cd6c7c241263cf1628ff8ece29478ddb91` and identified two
P1 trust-boundary failures:

- the SQL detector's 500-character window allowed a long multiline statement
  to pass; and
- the supporting-evidence registry was synthesized from the same hop assertions
  it claimed to support.

The repair removes the SQL length cutoff in both generation and validation and
adds a regression with more than 4,000 characters between the verb and source
clause. Because this site-only branch has no independently projected extractor
output, it takes the specification's fail-closed path: public claim level is
`concept`, self-derived evidence records and aliases are omitted from the
generated asset, the page/discovery/claim-ledger rows disclose the missing
independent evidence, and validators reject any reintroduced evidence registry
or supporting IDs. Moving back to `demo` now requires a separate bounded
extractor-owned public projection rather than another site-authored mapping.

Post-P1 validation passed: 10 focused proof tests, all 1,200 site tests, site
build, full site validation (117 HTML files, 3,883 internal references, 116
sitemap URLs), the focused `LazyConstructorLoggingTests` lane, private-path
guard, and `git diff --check`. Desktop 1,440 by 1,000 and mobile 390 by 844
browser checks each found one H1, all three concept outcomes, the missing
independent-evidence disclosure, no horizontal overflow, and no console errors.
