# Site TraceMap Tools Demo Proof Upgrades Requirements

Public claim level: demo

## Objective

Create a queued site phase for a public `/demo/proof-upgrades/` page that explains how the rows formerly deferred on the public demo became reproducible demo evidence on `main`. The page should act as a proof ledger: each row names the checked-in proof path, generated public-safe artifacts, fresh demo counts, coverage caveats, and non-claims.

This spec is content planning only until implementation starts.

## Current Evidence State

Coordinator evidence report date: after `main` was merged into `dev`.

The six public demo rows have reproducible demo evidence from checked-in samples on `main`. A fresh run of `./scripts/demo-public.sh <ignored-out>` passed and generated available sections for all six rows. The generated reports are static, rule-backed, and coverage-labeled. Several reports carry `PartialAnalysis` coverage, so the site may describe them as demo evidence, not full proof, runtime proof, release approval, or production usage evidence.

## Requirements

### Requirement 1: Publish a demo proof-upgrades page

The site shall publish a page at `/demo/proof-upgrades/` that explains the proof behind the demo rows that were previously deferred on `/demo/result/`.

Acceptance criteria:

- The page says `Public claim level: demo`.
- The page states that these rows have reproducible demo evidence from checked-in samples.
- The page states that the reports are static, rule-backed, coverage-labeled, and may include `PartialAnalysis`.
- The page links to `/demo/result/`, `/roadmap/`, `/packets/`, and `/capabilities/`.
- The page uses existing static site layout patterns and does not introduce a new runtime service.

### Requirement 2: Show the demo evidence ledger

The page shall include one row or section for each public demo area that has now moved from deferred status to demo evidence.

Acceptance criteria:

- The page covers `combine-and-dependency-report`.
- The page covers `paths-and-reverse`.
- The page covers `portfolio`.
- The page covers `diff`.
- The page covers `impact`.
- The page covers `release-review`.
- Each row includes current public status, proof path, public-safe artifacts, fresh demo counts, limitations, and what the row must not claim.

### Requirement 3: Preserve promotion gates

The page shall explain why these rows can be described as demo evidence and what would still be required before stronger claims are safe.

Acceptance criteria:

- A row is `demo` only because it is reproducible from checked-in samples and generated public-safe summaries.
- Public copy includes or points to rule IDs, evidence tiers, coverage labels, counts or supporting IDs where applicable, limitations, and a source path back to the checked-in workflow or generated summary.
- Rows with before/after fixture pairs state that the fixtures are checked in under `samples/public-demo/before/` and `samples/public-demo/after/`.
- Rows that rely on generated output explain that raw scan directories, fact streams, SQLite databases, combined SQLite files, and analyzer logs remain local-only.
- Stronger claims such as release approval, runtime proof, production dependency understanding, endpoint performance, or production usage remain out of bounds.

### Requirement 4: Keep public claim boundaries explicit

The page shall keep the static evidence boundary visible.

Acceptance criteria:

- The page does not claim runtime behavior, production traffic, deployment state, endpoint performance, release safety, or AI impact analysis.
- The page does not say demo evidence proves full impact, runtime reachability, or release approval.
- The page does not publish raw scan artifacts, SQLite databases, fact streams, analyzer logs, source snippets, SQL text, config values, secrets, local absolute paths, raw repository remotes, or private sample identities.
- The page says generated public-safe summaries are a presentation layer over evidence, not a replacement for facts, reports, rule IDs, coverage labels, and limitations.

### Requirement 5: Make the page discoverable

The page shall be reachable from existing public demo and roadmap surfaces.

Acceptance criteria:

- `/demo/result/` links to `/demo/proof-upgrades/`.
- `/roadmap/` links to `/demo/proof-upgrades/`.
- `/demo/` links to `/demo/proof-upgrades/` near the current demo-result and roadmap callouts.
- `/demo/proof-upgrades/` is included in sitemap metadata.
- The implementation-state note records scope, branch, validation, and follow-up items when implementation begins.

## Implementation Notes

Suggested row framing:

| Demo area | Current public status | Proof path | Public-safe artifact |
| --- | --- | --- | --- |
| combine-and-dependency-report | demo | `scripts/demo-public.sh`, `scripts/demo-public-assert.mjs`, `samples/modern-sample/`, `samples/endpoint-server-aspnet/`, `samples/typescript-modern-sample/`, `samples/endpoint-client-angular/`, `CombinedDependencyReportTests.cs` | dependency reports under `<out>/reports/dependency/...` |
| paths-and-reverse | demo | `scripts/demo-public.sh`, combined path and reverse query specs, path/reverse tests | path and reverse reports under `<out>/reports/paths/...` and `<out>/reports/reverse/...` |
| portfolio | demo | `scripts/demo-public.sh`, `samples/portfolio.example.json`, `PortfolioReportTests.cs` | `<out>/portfolio-manifest.json` and portfolio reports |
| diff | demo | `scripts/demo-public.sh`, `samples/public-demo/before/`, `samples/public-demo/after/`, `CombinedDependencyDiffTests.cs` | diff reports under `<out>/reports/diff/public-demo/` |
| impact | demo | `scripts/demo-public.sh`, before/after public fixtures, `CombinedChangeImpactTests.cs` | impact reports under `<out>/reports/impact/public-demo/` |
| release-review | demo | `scripts/demo-public.sh`, before/after public fixtures, `ReleaseReviewTests.cs` | release-review reports under `<out>/reports/release-review/public-demo/` |

## Demo Evidence Details

### combine-and-dependency-report

- Status: shipped on main; demo evidence exists.
- Public-safe artifacts:
  - `<out>/reports/dependency/endpoint-stack/dependency-report.md`
  - `<out>/reports/dependency/endpoint-stack/dependency-report.json`
  - `<out>/reports/dependency/mixed-stack/dependency-report.md`
  - `<out>/reports/dependency/mixed-stack/dependency-report.json`
- Fresh run counts: 6 combined sources, 14 endpoint findings, 62 dependency surfaces, 77 dependency edges, 2 gaps.
- Limitations: `PartialAnalysis`; static evidence only; does not prove runtime topology, deployment, auth, traffic, or production behavior.

### paths-and-reverse

- Status: shipped on main; demo evidence exists.
- Public-safe artifacts:
  - `<out>/reports/paths/endpoint-to-sql/paths-report.md`
  - `<out>/reports/paths/endpoint-to-sql/paths-report.json`
  - `<out>/reports/reverse/sql-to-endpoints/reverse-report.md`
  - `<out>/reports/reverse/sql-to-endpoints/reverse-report.json`
- Fresh run counts: 12 paths, 43 path gaps, 25 reverse paths, 6 reverse roots, 23 reverse gaps, 5 selected surfaces.
- Limitations: static path evidence, not runtime traces; reverse query shows static roots under available coverage; `PartialAnalysis` and gaps stay visible.

### portfolio

- Status: shipped on main; demo evidence exists.
- Public-safe artifacts:
  - `<out>/portfolio-manifest.json`
  - `<out>/reports/portfolio/portfolio-report.md`
  - `<out>/reports/portfolio/portfolio-report.json`
- Fresh run counts: 2 portfolio inputs, 6 portfolio sources, 9 dependency surfaces, 10 dependency edges, 4 endpoint findings, 4 gaps.
- Limitations: static portfolio inventory only; does not infer ownership, deployment topology, runtime service maps, traffic, vulnerabilities, package compatibility, or release approval.

### diff

- Status: shipped on main; demo evidence exists.
- Public-safe artifacts:
  - `<out>/reports/diff/public-demo/diff-report.md`
  - `<out>/reports/diff/public-demo/diff-report.json`
- Fresh run counts: 14 diff rows, 12 surface diffs, 0 endpoint diffs, 0 edge diffs, 0 gaps.
- Limitations: coverage-relative diff, not runtime impact; public fixture currently demonstrates surface diffs more than endpoint diffs.

### impact

- Status: shipped on main; demo evidence exists.
- Public-safe artifacts:
  - `<out>/reports/impact/public-demo/impact-report.md`
  - `<out>/reports/impact/public-demo/impact-report.json`
- Fresh run counts: 14 diff rows considered, 12 impact items, 12 surface impacts, 0 endpoint impacts, 0 edge impacts, 0 gaps.
- Limitations: static change context only; not runtime impact analysis; path context is bounded/optional and must not be implied unless the command uses it.

### release-review

- Status: shipped on main; demo evidence exists.
- Public-safe artifacts:
  - `<out>/reports/release-review/public-demo/release-review.md`
  - `<out>/reports/release-review/public-demo/release-review.json`
- Fresh run counts: 50 findings or top changed surfaces, 0 contract findings, 3 gaps, 53 checklist items.
- Limitations: static evidence packet, not release approval; default demo does not include contract delta proof; SQL/schema, package compatibility, runtime reachability, deployment verification, CI policy, and production usage remain unavailable or deferred unless compatible deterministic inputs are supplied.
