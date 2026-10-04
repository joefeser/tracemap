# Implementation State

Status: ACK-authorized review repair validated; current-head ACK rerun pending

Branch: `codex/site-webforms-review-workbench`

Exact implementation base: `5fd50ebec3bef40c7c0b3660a754ab08cab45982`

Initial validated implementation commit: `4b225971bf18299103283ad1db022121d9951939`

PR #803 ancestry at exact base: `not-shipped`

Issue: #808

Specification PR: https://github.com/joefeser/tracemap/pull/813

Implementation PR: https://github.com/joefeser/tracemap/pull/817

## Implementation decision

The #806 route and `tracemap.webforms-source-compiled-proof.v1` asset are on the
exact implementation base and pass their focused validator. The walkthrough is
therefore published at `demo` for its authored seven-step reading sequence. The
underlying #806 projection remains `concept`: no independent extractor output
is checked in for its illustrative hop IDs, tiers, spans, or supporting aliases.
The page does not turn those fields into verified facts.

The implementation reuses the existing JSON projection and creates no new
machine-readable public artifact. Its generator and bounded privacy-projected
input SHA-256 values remain owned and validated by #806.

## Implemented surface

- `/webforms/review-workbench/` presents the fixed scope, handler, route,
  evidence-detail, unresolved-command, coverage-gap, and ask-or-stop sequence.
- The dynamic-email route preserves source syntax, compiled metadata,
  Tier3 review candidates, and a Tier4 gap as separate observations.
- The evidence detail carries rule, tier, public relative span, exact selected
  commit, extractor version, coverage, projection namespace, limitation, and a
  visible missing supporting-reference gap.
- The reviewer decision table keeps partial, truncated, query-omitted,
  path/work-limited, candidate-only, missing-input, and unresolved-command
  states distinct.
- The packet note records separate deterministic 10,000-row client and server
  inventories, packet truncation, and the conditional generic gap-budget
  fallback without applying those limits to other surfaces.
- Pages/discovery, sitemap input, roadmap claim ledger, focused validation, and
  inbound links from the Web Forms, proof, manager, docs/output, generic review,
  and modernization-handoff surfaces are updated.

## Scope boundaries retained

Issue #744 remains open and is not treated as proof that the broader private
full-application workbench is complete. PR #803 is still not an ancestor of the
exact main implementation base and is not cited as shipped evidence.

No live workbench, scanner, reporter, query, source, database, runtime, release,
or approval behavior changed. No customer material, raw retained artifact,
source value, command body, private path, credential, connection material, or
private validation output is published.

## Validation

Validated on 2026-10-03 CDT from the implementation worktree:

- `cd site && npm run build`: passed.
- `cd site && npm test`: 1,234 passed; 0 failed, skipped, cancelled, or todo
  after the ACK-authorized review repair.
- `cd site && npm run validate`: passed across 120 HTML files, 4,004 internal
  references, and 119 sitemap URLs.
- `./scripts/check-private-paths.sh`: passed.
- `git diff --check`: passed.

Desktop browser QA at 1440 x 1000 covered the new route. Mobile QA at 390 x
844 covered `/webforms/`, `/webforms/source-plus-compiled-proof/`, and
`/webforms/review-workbench/`. The pages had no document-level horizontal
overflow, broken images, or browser-console errors/warnings. The two wide
evidence tables stay within independent horizontally scrollable containers at
mobile width.

## Review repair

ACK authorized a consolidated repair from reviewed head
`aa24469c31f68cd440d2b1a77f225ae657474f67`. The repair makes the route
validator fail closed for malformed proof arrays and unresolvable implementation
bases; binds rendered commit, command states, outcome coverage, gaps, bounds,
discovery limitations, and evidence metadata to the reused #806 projection;
and scans both the page and proof asset across browser-decoded and tag-collapsed
safety surfaces. Fourteen focused route regressions pass. The repaired head
still requires the normal exact-head ACK rerun and is not merge-approved here.

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
  independently at 10,000 rows and set packet truncation on overflow. Their
  separate gap kinds are retained only while the configured packet gap budget
  has room; after saturation, `WebFormsModernizationGapLimitReached` can replace
  a later inventory-specific gap. This evidence does not establish any broader
  10,000-row workbench limit or complete inventory.
- The lazy-constructor public fixture and #806 projection provide the synthetic
  ordered route, unresolved command candidate, and explicit gaps used here.

## Dependencies and overlap

- #806 owns `/webforms/source-plus-compiled-proof/` and the reused public-safe
  JSON projection. This implementation does not regenerate or strengthen it.
- #805 owns `/webforms/` and workflow/setup entry.
- #807 owns the reproducible local-demo artifact map; this page links to it
  rather than duplicating operator instructions or retained artifacts.
- `/review-room/demo-path/` remains the generic concept-level review sequence.
- `/legacy-modernization/review-handoff/` remains the broader owner handoff.
- #809 may consume only this recorded `demo` sequence / `concept` projection
  split when refreshing capabilities and roadmap language.

## Known limitations

- The selected projection is historical exact-commit evidence, not current
  runtime, build, deployment, database, or customer evidence.
- Its illustrative hops do not carry independently projected supporting fact
  IDs or source/index namespaces; the page exposes those missing details rather
  than constructing them.
- The walkthrough covers one selected synthetic route, not every page, handler,
  candidate, gap, or requested #744 workbench feature.
- Client and server behavior inventory limits belong to modernization packet
  construction and are not observed overflow events in the selected projection.

## Handoff

The current-head ACK result remains to be recorded. Do not merge without Joe's
approval and do not edit generated `site/dist` or `site/output` content.
