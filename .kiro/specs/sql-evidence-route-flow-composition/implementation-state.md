# SQL Evidence Route-Flow Composition Implementation State

Status: implementation-complete-awaiting-pr-review
Implementation branch: `codex/implement-sql-evidence-release-review-composition`
Target base: `dev`
Public claim level: static evidence packet

## Scope State

Spec skeleton only — no code implemented in this pass. This slice **composes**
the already-shipped PostgreSQL SQL runway evidence into the combined route-flow
report and/or the release-review packet. It does not add SQL extraction.

Confirmed shipped on `main` at spec creation (audited files present at HEAD):

- `SqlExecutionContextExtractor` is wired into the scan pipeline at
  `src/dotnet/TraceMap.Core/ScanEngine.cs` (execution-context runs on every scan).
- `SqlSecretSafetyExtractor`, `PostgresPermissionEvidenceExtractor`,
  `PostgresArchiveLinkExtractor` (`TraceMap.Core`) and `SqlRunbookPacket`
  (`TraceMap.Reporting`) are present with tests and rule-catalog entries.
- Evidence currently surfaces only in single-scan `report.md` and the standalone
  `sql-runbook.md` / `sql-runbook.json` artifacts.

Confirmed composition gap (the reason this spec exists):

- `CombinedRouteFlowReport` and `ReleaseReviewReport` do **not** read the SQL
  execution-context / permission / archive-link / runbook evidence.
  `ReleaseReviewReport` has a `SqlSchemaImpact` section, but that is the older
  SQL-*shape* path, not the operator/context evidence.

## Implementation Choices To Record When Started

- This PR delivers Target B (release-review) only. Target A (route-flow) is
  deferred to a separate PR so this slice stays focused.
- A tiny read-side persistence hook is required: `facts` and `combined_facts`
  preserve the already-emitted `EvidenceSpan.ExtractorId` and
  `EvidenceSpan.ExtractorVersion` in additive columns, and release-review reads
  those columns. Existing indexes without the columns remain readable but
  cannot be treated as compatible SQL runway inputs because full upstream
  provenance is unavailable. No extraction, fact shape, rule ID, tier,
  coverage, or extractor-version behavior changes.
- No new sample fixture is planned; Target B tests reuse the existing SQL
  extractors and synthetic release-review indexes.

### Read Gate Inventory

Release-review can read the following already-shipped SQL runway facts from
single or combined indexes: `SqlExecutionContextDeclared`,
`SqlExecutionContextCandidate`, `DatabaseLinkSurfaceDeclared`,
`DatabaseLinkEdgeCandidate`, `DatabasePrerequisiteCandidate`,
`DatabasePermissionDeclared`, `DatabasePrerequisiteEvidence`,
`SecretBearingSqlStep`, and SQL/PostgreSQL `AnalysisGap` rows. Reachable rule
families are `database.sql.context.*.v1`,
`database.sql.secret-bearing-step.v1`,
`database.sql.secret-text-candidate.v1`,
`database.sql.secret-safety-gap.v1`,
`database.postgres.permission.*.v1`, and
`database.postgres.archive-link*.v1`.

## Boundaries (do not cross in this spec)

- No new extraction, no engine beyond PostgreSQL, no rule/tier/coverage/
  extractor-version changes. A read-side hook is the maximum extraction-adjacent
  change; anything larger becomes a follow-up spec.
- No runtime, reachability, production-state, permission-effectiveness,
  validation-result, rollback, or release-approval claims. Preserve every
  existing SQL non-claim.
- No raw SQL, connection strings, credentials, scheduled command bodies, private
  identities, or local absolute paths in output.

## Adjacent / Follow-Up (tracked in tasks.md, not blocking the composition PR)

- Phase 5.1: `docs/ACCEPTANCE.md` SQL acceptance rows remain open. Phase 5.2 is
  implemented below.
- Phase 6: audit status-drift cleanup (`NEXT_EXECUTION_REPORT.md` refresh,
  headerless implementation-state files, stale in-flight statuses, README SQL
  mention).

### Implemented Phase 5.2

The HACP Dispatch dogfood branch `codex/sql-site-guardrail-dogfood`, based on
`dev@a40ad6a23d3629e1e5bc7870857a2a278e2e2a43`, widened the shared hard
private/credential scan to every generated `dist/**/index.html` file. The
enumerator is deterministic, does not follow symlinks, and reports only
dist-relative evidence paths. Page-specific overclaim and bounded-example
semantics remain with each page validator; applying the guardrails-page claim
grammar globally produced false positives in valid teaching examples and was
therefore rejected during the broad test pass.

Focused regression coverage proves nested `index.html` files are scanned,
non-index HTML files are outside this exact task, and diagnostics do not expose
the temporary fixture root.

## Implemented Target B

- Added a separate `SqlEvidence` release-review section for single and combined
  indexes, including JSON/Markdown rendering, summary counts, release-review
  statuses, existing attention classifications, and packet-level gap flow.
- Reused `SqlRunbookPacketBuilder` for context steps, milestones,
  prerequisites, protected-material outcomes, SQL gaps, and owner questions.
- Preserved rule ID, tier, coverage, safe relative span, commit SHA, extractor
  ID/version, supporting fact IDs, and upstream limitations. Protected material
  remains span-only; raw SQL, snippets, hashes, connection material, private
  identities, and scheduled command bodies are not projected.
- Added additive `extractor_id` / `extractor_version` persistence columns to
  single and combined indexes. Combining older indexes remains supported via
  null projection; release-review labels their SQL evidence unavailable rather
  than silently inventing missing provenance.

## Validation

Phase 5.2 validation:

- `cd site && node --test scripts/site-claim-guardrails.test.mjs`: 13 passed.
- `cd site && npm test`: 683 passed.
- `cd site && npm run build`: passed; static site built to `dist/`.
- `cd site && npm run validate`: passed; 92 HTML files, 3,188 internal
  references, and 91 sitemap URLs validated.
- `./scripts/check-private-paths.sh`: passed.
- `git diff --check`: passed.

- Focused release-review/runbook/combine tests: 37 passed after promotion-review fixes.
- `dotnet build src/dotnet/TraceMap.sln`: passed, 0 warnings.
- `dotnet test src/dotnet/TraceMap.sln`: 750 passed, 0 failed.
- Checked-in `samples/sql-operator-runbook` scan plus release-review smoke:
  `SqlEvidence=available`, rollup `ReviewRecommended`, no truncation, 32 SQL
  findings, 18 structured gaps, and no planted sentinel or forbidden phrase.
- `./scripts/check-private-paths.sh`: passed.
- `git diff --check`: passed.

Review fixes preserve source-selector isolation, include SQL evidence in review
priority scoring, tolerate malformed fact-property JSON, expose extractor
provenance as first-class finding fields, preserve both shipped limitation keys,
and disambiguate finding/gap IDs with end spans and supporting fact IDs. SQLite
schema inspection now uses parameterized table-valued pragmas; the remaining
combined import interpolation is restricted to validated internal identifiers
because SQLite does not parameterize attached-schema identifiers.
The SQL runway reader uses an exact shipped-rule allowlist so ordinary SQL text
and shape usage facts cannot falsely mark the runway section available.
Promotion review also aligned single-index SQL findings to the canonical
`single` source label, kept informational SQL rows out of review rollups and
review counts, and made missing generic finding provenance explicit with
non-null `not-recorded` values.

## Deferred From This PR

- Target A route-flow SQL context groups (Phase 3).
- Acceptance cleanup (Phase 5.1).
- Status-drift and README cleanup (Phase 6).

## Related Specs

- [[sql-execution-context-contracts]]
- [[sql-operator-runbook-packet]]
- [[sql-permission-prerequisite-evidence]]
- [[postgres-archive-link-evidence]]
- [[sql-secret-bearing-step-safety]]
- [[route-flow-service-data-composition]]
- [[route-centered-endpoint-trace-completeness]]
