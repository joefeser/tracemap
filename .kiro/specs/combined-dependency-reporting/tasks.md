# Combined Dependency Reporting Tasks

## Implementation Tasks

- [ ] Add combined dependency report models.
  - [ ] Define source, summary, endpoint finding, dependency surface, dependency edge, needs-review, known-gap, and limitation rows.
  - [ ] Define stable JSON shape version `1.0`.
  - [ ] Pin endpoint finding JSON fields, including client/server source labels, fact IDs, file spans, `sameSource`, and nullable one-sided values.

- [ ] Add project wiring.
  - [ ] Add `Microsoft.Data.Sqlite` access to `TraceMap.Reporting`.
  - [ ] Decide whether to duplicate minimal source records or reference `TraceMap.Combine` without creating awkward ownership.
  - [ ] Keep scanner projects untouched unless a small combine compatibility fix is required.

- [ ] Fix combined source language inference.
  - [ ] Update `CombinedIndexBuilder.InferLanguage` so JVM sources render as `jvm`.
  - [ ] Update `CombinedIndexBuilder.InferLanguage` so Python sources render as `python`.
  - [ ] Add regression coverage for JVM/Python language values.

- [ ] Add combined index reader.
  - [ ] Detect combined indexes by requiring `index_sources` with at least one row, `combined_facts`, and `combined_dependency_edges`.
  - [ ] Reject single-language indexes with a clear message.
  - [ ] Treat missing `endpoint_matches` as a warning for read-only MVP reporting.
  - [ ] Read `index_sources` with manifest JSON.
  - [ ] Read endpoint, SQL/query, package/config, and dependency-edge rows.
  - [ ] Parse `properties_json` defensively.

- [ ] Add coverage summarization.
  - [ ] Classify report coverage.
  - [ ] Distinguish report-level `UnknownAnalysisGap` from finding-level `UnknownAnalysisGap`.
  - [ ] Correct stale source-language display from scanner version and emit a warning.
  - [ ] Group known gaps by source label and category.
  - [ ] Ensure local absolute paths are not rendered.

- [ ] Add endpoint candidate extraction.
  - [ ] Identify client and server endpoint candidates from combined facts.
  - [ ] Preserve source/fact provenance before matching.
  - [ ] Sanitize dynamic URL reasons so raw URL fragments are not rendered.

- [ ] Add combined endpoint classification.
  - [ ] Match by HTTP method and normalized path key.
  - [ ] Compute matches per `(client source, server source)` pair.
  - [ ] Emit fan-out matches across different server sources as separate matches, not global ambiguity.
  - [ ] Include same-source client/route pairs and flag `sameSource`.
  - [ ] Classify matched, optional, method mismatch, ambiguous, dynamic, client-only, server-only, and unknown-gap cases.
  - [ ] Preserve full source/fact provenance in each finding.

- [ ] Keep MVP endpoint reporting read-only.
  - [ ] Prove report generation does not mutate `endpoint_matches`.
  - [ ] Defer `--write-derived` and derived DB writes unless explicitly pulled into a follow-up.
  - [ ] If `--write-derived` is implemented later, add boolean flag parsing support and delete old rows by `derivedBy` before inserting new rows.

- [ ] Add dependency surface extraction.
  - [ ] Render HTTP client and route surfaces.
  - [ ] Render SQL-shape and query-builder surfaces.
  - [ ] Render `SqlTextUsed` as hash/length evidence only.
  - [ ] Render `n/a` for table/column fields when only hash/length SQL evidence exists.
  - [ ] Render package/dependency surfaces only from the explicit fact/property keys listed in the design.

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
  - [ ] Treat missing-extension output paths as directories.
  - [ ] Print useful completion summary.

- [ ] Add tests.
  - [ ] Single-language index rejection.
  - [ ] Markdown and JSON output.
  - [ ] Source coverage warnings.
  - [ ] Endpoint classifications, fan-out matching, and same-source matching.
  - [ ] No mutation of `endpoint_matches`.
  - [ ] Dependency edge rows.
  - [ ] SQL/query rows without raw SQL, including `SqlTextUsed`-only rows.
  - [ ] Dynamic URL findings without raw URL fragments.
  - [ ] JVM/Python language inference.
  - [ ] Markdown 200-row truncation notice with full JSON rows.
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
- Opt-in `--write-derived` endpoint persistence after deciding schema behavior for one-sided findings.
- HTML report output.
- Cross-source path search over combined dependency edges.
- More complete package/dependency taxonomy.
- Parser-backed SQL normalization.
- Rule-backed derived facts if TraceMap formalizes derived fact rules.
