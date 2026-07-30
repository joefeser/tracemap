# Site SQL Project Refactor Intent Story Implementation State

- Status: implementation and validation complete; PR #544 open; third
  ACK-authorized validator hardening pass complete
- Branch: `codex/site-sql-project-refactor-intent`
- Base: `origin/dev`
- Base SHA: `26189d1dd8d97d5741005ae7cbd033840099f216`
- Public claim level: `demo`

## Source grounding

A local read-only scan of `samples/sql-project-refactor` at the base commit
produced scan ID `scan-bc301542ac1a5995396c`, one literal project/log link,
one table rename, and one table schema move under
`database.sql-project.refactor-intent.v1`. The extractor family/version is
`SqlProjectRefactorExtractor` / `sql-project-refactor/0.1.0`.

The supported column-rename category is illustrative and explicitly labeled as
not emitted by this fixture. Gap examples are illustrative shapes from
`database.sql-project.refactor-intent.gap.v1`; they are not presented as gaps
from the clean fixture scan.

## Boundary

Site-only. No SQL project build, DACPAC work, deployment-plan inspection,
SqlPackage invocation, database connection, SQL execution, target refactor-log
table inspection, operational command, private material, applied-state claim,
compatibility claim, approval claim, or safety claim.

## Validation

- `cd site && npm run build`: passed.
- `cd site && npm run validate`: passed for 98 HTML files, 3,369 internal
  references, and 97 sitemap URLs.
- `cd site && npm test`: 745 passed, 0 failed.
- Focused story tests: 28 passed, 0 failed, including ACK-authorized
  attribute/obfuscation leak, full-boundary/JSON overclaim, and exact fixture
  fact, scan, gap-shape, downstream-handoff, and supported-category pinning
  regressions, plus direct compatibility/applied-state and bounded executable
  SQL-form rejection.
- Aggregate validator tests: 11 passed, 0 failed.
- `./scripts/check-private-paths.sh`: passed.
- `git diff --check`: passed.
- Desktop checks at 1,440 × 1,000 and mobile checks at 390 × 844 passed for
  `/sql/project-refactor-intent/` and
  `/blog/sql-project-refactor-intent-evidence/`; no horizontal overflow or
  browser console warnings/errors were observed. The article-to-proof link was
  exercised successfully.

## PR review

ACK-authorized findings were fixed in `7553c4b5`, all associated review threads
were resolved, and evidence-backed dispositions were recorded for important
prior-head Codex and Qodo findings. After all checks passed, ACK returned
`not_merge_ready` / `REQUIRED_REVIEW_QUORUM_NOT_MET`: Qodo's one-shot evidence
was terminal but stale, and no current-head required-review substitute was
admitted. The required five-minute unchanged reviewer-lag wait and single rerun
produced the same owner-decision result. This state does not claim merge
readiness. A subsequently requested current-head Codex review identified three
additional exact-value validator gaps; the scan ID, representative gap
classifications, and downstream review classifications/states are now pinned
with focused regressions. A later exact-head review also required exact
supported-category objects, direct compatibility/applied-state claim guards,
and bounded `TRUNCATE`, `EXEC`, and permission-statement detection; those
regressions are included in the counts above.
