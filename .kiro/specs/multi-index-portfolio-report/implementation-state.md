# Multi-Index Portfolio Report Implementation State

## Current State

Implemented v1 portfolio report in `dev`; follow-up surface/edge comparison slice is implemented on `codex/portfolio-report-followups`.

## Intended Implementation Boundary

Implemented a useful v1 portfolio command:

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
- Task checkboxes in `tasks.md` distinguish the implemented v1 from remaining follow-up slices.

## Implemented In This Branch

- Added `tracemap portfolio`.
- Added direct `--index/--label` and manifest input modes.
- Added paired `--before-manifest/--after-manifest` source-level comparison.
- Added paired `--before-manifest/--after-manifest` projected surface and edge comparison using stable safe identities.
- Added deterministic Markdown/JSON output.
- Added portfolio source coverage, endpoint alignment, dependency surface inventory, dependency edge inventory, shared surface grouping, gaps, limitations, and optional-section statuses.
- Added rule catalog entries for `portfolio.*.v1`.
- Added README quickstart and `samples/portfolio.example.json`.
- Added focused tests in `PortfolioReportTests`.
- Added comparison redaction tests for raw SQL, snippets, raw URLs, secret-looking values, connection strings, local absolute paths, and manifest display field injection.

## Remaining Follow-Ups

- Combined diff engine reuse for compatible combined before/after snapshots where safe; current portfolio comparison projects sources, surfaces, and edges directly from already-read evidence.
- Real impact composition over compatible before/after combined snapshots.
- Real path/reverse composition through existing bounded graph APIs.
- Stable symbol identity grouping.
- Ignored selector gaps for selectors that apply only to disabled/unavailable sections.
- Additional integration coverage for object creation and parameter-forwarding rendering, mixed single/combined input, duplicate-label rejection, unknown commit coverage, duplicate source identity, optional path/reverse unavailable states, and truncation caps.

## Validation

- `dotnet build src/dotnet/TraceMap.sln` passed.
- `dotnet test src/dotnet/TraceMap.sln` passed: 201 tests.
- `./scripts/check-private-paths.sh` passed.
- `git diff --check` passed.
- Relevant pinned smoke checks are deferred for this branch because the change is limited to portfolio report comparison projection and does not change language adapters, combined indexes, path/reverse traversal, combined report, combined diff, impact, or release-review behavior.
