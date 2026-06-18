# TraceMap Kiro Review Packet

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

Review the `site-tracemap-tools-claim-ledger` spec for spec-review findings
first. This is a spec-only site phase; it should not implement site code.

## Review Orientation

Branch: codex/spec-site-claim-ledger
Last verified: 2026-06-18
Prior review cycle: multiple Opus/Sonnet passes; Medium findings patched. See
`implementation-state.md` Review Findings for patch history.

Local review artifacts are not committed and were saved under
`.tmp/kiro-reviews/site-tracemap-tools-claim-ledger/`.

Remaining open questions: none.

## Scope

The future page would extend `/roadmap/` or define a public `/claims/` or
`/claim-ledger/` claim ledger that lists major public site claims, public claim
level, proof path, evidence status, limitations, and explicit non-claims. The
ledger is presentation and claim governance only. SQLite indexes, fact streams,
reports, analyzer logs, rule catalog entries, commit metadata, coverage labels,
and documented limitations remain the source of truth.

Please inspect:

- `.kiro/specs/site-tracemap-tools-claim-ledger/requirements.md`
- `.kiro/specs/site-tracemap-tools-claim-ledger/tasks.md`
- `.kiro/specs/site-tracemap-tools-claim-ledger/implementation-state.md`
- `.kiro/specs/site-tracemap-tools-claim-ledger/review-packet.md`

Review focus:

- Does the spec define a bounded future route/page decision that evaluates
  extending the existing `/roadmap/` claim-ledger surface before choosing
  `/claims/` or `/claim-ledger/` without implementing site code?
- Are `Status: not-started`, `Readiness: ready-for-implementation`, and
  `Public claim level: concept` present and consistent?
- Are future implementation tasks unchecked?
- Does the claim-level table requirement help managers, reviewers, bots, and
  future agents distinguish shipped, demo, concept, and hidden wording?
- Does the spec prevent `hidden` claim rows and `hidden/internal`
  evidence-status rows from disclosing unreleased capability names, internal
  routes, private sample identities, hidden-export specifics, counts, cadence,
  sequencing, or in-flight status?
- Does the spec define how the ledger relates to existing roadmap, proof path
  index, and capability matrix surfaces without duplicating them?
- Does the spec map any new claim-level or evidence-status vocabulary to
  existing labels used by roadmap, capability, proof-path, and discovery
  surfaces?
- Does every claim-level and evidence-status label resolve through one mapping
  table before automated cross-page review depends on it?
- Are proof paths tied to public-safe summaries, routes, documentation, rule
  catalog pages, reports, or demo artifacts while preserving SQLite/facts/
  reports/rule catalog as source of truth?
- Does the spec forbid claims that TraceMap proves runtime behavior,
  production traffic, endpoint performance, outage cause, release safety,
  operational safety, AI impact analysis, LLM analysis, or complete product
  coverage?
- Does the spec forbid raw source snippets, raw SQL, config values, secrets,
  local absolute paths, raw remotes, generated scan directories, private sample
  names, raw facts, raw SQLite indexes, and raw analyzer logs?
- Are acceptance criteria present for route/page placement, claim-level table,
  proof-path links, discovery metadata, forbidden overclaims/private text,
  validation, and implementation-state updates?

Return findings first, severity ordered. Include suggested spec edits for any
Medium or higher findings.
