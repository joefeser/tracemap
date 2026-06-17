# Route-Centered Static Flow Report Implementation State

Status: not-started

## Branch

- Suggested implementation/spec branch:
  `codex/spec-route-centered-static-flow-report`.

## Scope

This is spec-only work for a future route-centered static call flow report. It
does not implement product code, edit scanner/reducer behavior, update site
files, or add generated outputs.

The proposed feature is a deterministic reporting/query layer over a combined
TraceMap index. It should compose existing evidence families where possible:

- endpoint alignment;
- HTTP route bindings;
- TypeScript HTTP client calls;
- call edges, object creations, argument flows, and parameter-forward edges;
- `combined_symbol_relationships` and fact-symbol attachments;
- query patterns and SQL-shape evidence;
- data/dependency surfaces;
- object/projection/schema/business-boundary facts;
- combined path and reverse graph inventory;
- evidence graph or vault export safe-rendering helpers.

## Source Material

- GitHub issue #159: `https://github.com/joefeser/tracemap/issues/159`.
- Issue title: `Add route-centered static call flow report`.
- Issue labels observed during spec creation: `enhancement`, `type:spec`,
  `area:flow`, `area:surfaces`, `priority:next`.

The issue describes a public-safe problem statement from private validation:
TraceMap can index route, call, repository, DbSet-like data access,
projection/query-shape, and client-call evidence in pieces, but it does not yet
produce one route-centered report that starts from an HTTP route or client call
and lists touched files, spans, symbols, intermediate calls, transformations,
and dependency/data logic. Private sample names and paths are intentionally not
recorded here.

## Scope Decisions

- The spec proposes a focused `tracemap route-flow` command over a combined
  SQLite index.
- The implementation may still reuse or internally extend `tracemap paths`; the
  new route-flow view should not create a second traversal engine.
- Interface-to-implementation bridging is conservative candidate evidence from
  `combined_symbol_relationships` or successor rule-backed implementation
  evidence. It is not runtime DI target proof.
- Business/data logic rows are static review context, such as projection/object
  shape, branching, validation, query/filter/sort/selection, async boundaries,
  and data/dependency surfaces. They are not runtime execution or business
  impact claims.
- Default outputs are Markdown and JSON, with no source snippets, raw SQL, raw
  URLs, connection strings, raw remotes, local absolute paths, private sample
  labels, or secrets.
- Public-safe validation should use checked-in synthetic/sample fixtures only.

## Current Validation

Spec-only validation should run:

```bash
git diff --check
./scripts/check-private-paths.sh
```

Kiro spec review should be attempted with Opus and Sonnet through
`scripts/kiro-review.mjs` when `kiro-cli` and auth are available locally. If
Kiro review is unavailable, record the exact blocker in this file or the PR
summary and perform self-review.

## Spec Delivery Notes

- Created the spec files only under
  `.kiro/specs/route-centered-static-flow-report/`.
- Did not edit product code, site files, generated outputs, docs outside this
  spec, or rule catalog entries.
- Ran Kiro Opus spec review:
  `node scripts/kiro-review.mjs --phase route-centered-static-flow-report --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text`.
  The review completed with reduced coverage because Kiro reported denied tool
  access.
- Ran Kiro Sonnet spec review:
  `node scripts/kiro-review.mjs --phase route-centered-static-flow-report --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`.
  The review completed with full coverage.
- Patched actionable Medium+ findings from the initial reviews, including
  combined schema table names, explicit fact-symbol and argument-flow readers,
  selector semantics, route-flow summary classification, exit-code behavior,
  interface-bridge classification caps, high-fan-out threshold documentation,
  and public/private safety tests.
- Ran two re-review cycles with Opus and Sonnet, each using `--kind re-review`
  and a 10 minute timeout. The final re-reviews completed but both had reduced
  coverage because Kiro reported denied tool access.
- Final local validation passed:
  - `git diff --check`
  - `./scripts/check-private-paths.sh`
  - `node scripts/kiro-review.mjs --self-test`

## Follow-Ups For Implementation

- Add route-flow rule catalog entries before emitting report rows.
- Add public-safe fixture coverage for aligned client/server route flow.
- Update `docs/VALIDATION.md` when implementation changes CLI behavior or
  shared path/report validation requirements.
- Run pinned smoke checks from `docs/VALIDATION.md` if implementation changes
  combined path traversal, endpoint alignment, language adapters, or shared
  reporting helpers.
