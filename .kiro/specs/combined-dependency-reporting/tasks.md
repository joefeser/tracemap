# Combined Dependency Reporting Tasks

## Implementation Tasks

- [ ] Add combined dependency report models.
  - [ ] Define source, summary, endpoint finding, dependency surface, dependency edge, needs-review, known-gap, and limitation rows.
  - [ ] Define stable JSON shape version `1.0`.

- [ ] Add combined index reader.
  - [ ] Detect combined vs single-language index.
  - [ ] Validate required tables/views.
  - [ ] Read `index_sources` with manifest JSON.
  - [ ] Read endpoint, SQL/query, package/config, and dependency-edge rows.
  - [ ] Parse `properties_json` defensively.

- [ ] Add coverage summarization.
  - [ ] Classify report coverage.
  - [ ] Group known gaps by source label and category.
  - [ ] Ensure local absolute paths are not rendered.

- [ ] Add combined endpoint matcher.
  - [ ] Identify client and server endpoint candidates from combined facts.
  - [ ] Match by HTTP method and normalized path key.
  - [ ] Classify matched, optional, method mismatch, ambiguous, dynamic, client-only, server-only, and unknown-gap cases.
  - [ ] Preserve full source/fact provenance in each finding.

- [ ] Add optional `endpoint_matches` persistence.
  - [ ] Generate deterministic endpoint match IDs.
  - [ ] Insert or replace derived rows idempotently.
  - [ ] Add `--no-write-derived` support.
  - [ ] Keep raw snippets, raw URLs, raw SQL, and absolute paths out of `evidence_json`.

- [ ] Add dependency surface extraction.
  - [ ] Render HTTP client and route surfaces.
  - [ ] Render SQL-shape and query-builder surfaces.
  - [ ] Render `SqlTextUsed` as hash/length evidence only.
  - [ ] Render package/dependency surfaces where existing facts expose stable names.

- [ ] Add dependency edge extraction.
  - [ ] Read `combined_dependency_edges`.
  - [ ] Include calls, creates, symbol relationships, and parameter-forwarding edges.
  - [ ] Preserve source label, edge IDs, rule IDs, evidence tiers, and file spans.

- [ ] Add Markdown writer.
  - [ ] Sections: Summary, Sources, Endpoint Alignment, Dependency Surfaces, Dependency Edges, Needs Review, Known Gaps, Limitations.
  - [ ] Deterministic sort order.
  - [ ] Row caps with truncation notices.
  - [ ] Inline coverage-relative caveats for unmatched endpoints.

- [ ] Add JSON writer.
  - [ ] Emit stable top-level shape.
  - [ ] Include all rows without Markdown caps.
  - [ ] Use `null` and empty arrays consistently for missing values.

- [ ] Wire CLI.
  - [ ] Replace `report` skeleton with combined dependency report implementation.
  - [ ] Update root help and report help.
  - [ ] Print useful completion summary.

- [ ] Add tests.
  - [ ] Single-language index rejection.
  - [ ] Markdown and JSON output.
  - [ ] Source coverage warnings.
  - [ ] Endpoint classifications and optional DB persistence.
  - [ ] `--no-write-derived`.
  - [ ] Dependency edge rows.
  - [ ] SQL/query rows without raw SQL.
  - [ ] Deterministic output ordering.

- [ ] Update docs.
  - [ ] README quickstart for `combine -> report`.
  - [ ] `docs/ACCEPTANCE.md` combined report acceptance.
  - [ ] `docs/VALIDATION.md` smoke command.
  - [ ] Rule catalog only if a derived rule ID is introduced.

- [ ] Validate.
  - [ ] `dotnet build src/dotnet/TraceMap.sln`
  - [ ] `dotnet test src/dotnet/TraceMap.sln`
  - [ ] `./scripts/check-private-paths.sh`
  - [ ] `git diff --check`

## Deferred Follow-Ups

- Snapshot/diff between two combined indexes.
- HTML report output.
- Cross-source path search over combined dependency edges.
- More complete package/dependency taxonomy.
- Parser-backed SQL normalization.
- Rule-backed derived facts if TraceMap formalizes derived fact rules.
