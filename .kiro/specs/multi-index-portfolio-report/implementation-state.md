# Multi-Index Portfolio Report Implementation State

## Current Branch

`codex/multi-index-portfolio-report`

## Intended Implementation Boundary

Implement a useful v1 portfolio command in this branch:

- `tracemap portfolio --index <path> --label <label> --out <path>`
- `tracemap portfolio --manifest <portfolio.json> --out <path>`
- read-only support for single-language and combined SQLite indexes
- source identity and coverage inventory
- dependency surface inventory
- dependency edge inventory
- endpoint alignment using the existing combined-report matcher
- shared-surface grouping using safe static identities
- deterministic Markdown and JSON
- rule catalog entries
- focused tests and docs

## Deliberate V1 Deferrals

- persisted portfolio database
- mixed direct inputs plus manifest
- `--exit-code`
- full path/reverse traversal composition
- full portfolio impact composition
- release-review import
- package vulnerability/license/compatibility overlays
- runtime topology, ownership, traffic, deployment, or service catalog inference

## Implementation Notes

- Combined indexes are expanded through `index_sources` and `combined_facts`.
- Single-language indexes are projected from `scan_manifest`, `facts`, and available edge tables.
- Output must not include raw local paths, raw URLs, raw SQL, snippets, config values, connection strings, or secret-looking values.
- Task checkboxes in `tasks.md` should be updated before finishing the branch.

## Implemented In This Branch

- Added `tracemap portfolio`.
- Added direct `--index/--label` and manifest input modes.
- Added paired `--before-manifest/--after-manifest` source-level comparison.
- Added deterministic Markdown/JSON output.
- Added portfolio source coverage, endpoint alignment, dependency surface inventory, dependency edge inventory, shared surface grouping, gaps, limitations, and optional-section statuses.
- Added rule catalog entries for `portfolio.*.v1`.
- Added README quickstart and `samples/portfolio.example.json`.
- Added focused tests in `PortfolioReportTests`.

## Remaining Follow-Ups

- Full surface/edge before/after diff semantics.
- Real impact composition over compatible before/after combined snapshots.
- Real path/reverse composition through existing bounded graph APIs.
- Stable symbol identity grouping.
- Ignored selector gaps for selectors that apply only to disabled/unavailable sections.
- Deeper redaction tests for raw SQL, raw URLs, snippets, and secret-looking values.

## Validation

- `dotnet build src/dotnet/TraceMap.sln` passed.
- `dotnet test src/dotnet/TraceMap.sln --no-build` passed: 153 tests.
- `./scripts/check-private-paths.sh` passed.
- `git diff --check` passed.
- `./scripts/smoke-combined-paths.sh` passed.
