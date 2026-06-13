# Combined Dependency Reporting Tasks

## Implementation Tasks

- [x] Add combined dependency report models.
  - [x] Define source, summary, endpoint finding, dependency surface, dependency edge, needs-review, known-gap, and limitation rows.
  - [x] Define stable JSON shape version `1.0`.
  - [x] Pin endpoint finding JSON fields, including client/server source labels, fact IDs, file spans, `sameSource`, and nullable one-sided values.

- [x] Add project wiring.
  - [x] Add `Microsoft.Data.Sqlite` access to `TraceMap.Reporting`.
  - [x] Decide whether to duplicate minimal source records or reference `TraceMap.Combine` without creating awkward ownership.
  - [x] Keep scanner projects untouched unless a small combine compatibility fix is required.

- [x] Fix combined source language inference.
  - [x] Update `CombinedIndexBuilder.InferLanguage` so JVM sources render as `jvm`.
  - [x] Update `CombinedIndexBuilder.InferLanguage` so Python sources render as `python`.
  - [x] Add regression coverage for JVM/Python language values.

- [x] Add combined index reader.
  - [x] Detect combined indexes by requiring `index_sources` with at least one row, `combined_facts`, and `combined_dependency_edges`.
  - [x] Reject single-language indexes with a clear message.
  - [x] Treat missing `endpoint_matches` as a warning for read-only MVP reporting.
  - [x] Read `index_sources` with manifest JSON.
  - [x] Read endpoint, SQL/query, package/config, and dependency-edge rows.
  - [x] Parse `properties_json` defensively.

- [x] Add coverage summarization.
  - [x] Classify report coverage.
  - [x] Distinguish report-level `UnknownAnalysisGap` from finding-level `UnknownAnalysisGap`.
  - [x] Correct stale source-language display from scanner version and emit a warning.
  - [x] Group known gaps by source label and category.
  - [x] Ensure local absolute paths are not rendered.

- [x] Add endpoint candidate extraction.
  - [x] Identify client and server endpoint candidates from combined facts.
  - [x] Preserve source/fact provenance before matching.
  - [x] Sanitize dynamic URL reasons so raw URL fragments are not rendered.

- [x] Add combined endpoint classification.
  - [x] Match by HTTP method and normalized path key.
  - [x] Compute two-sided comparison findings per `(client source, server source)` pair.
  - [x] Compute client-only, server-only, and dynamic findings as global one-sided inventory rows with absent-side JSON fields set to `null`.
  - [x] Emit fan-out matches across different server sources as separate matches, not global ambiguity.
  - [x] Include same-source client/route pairs and flag `sameSource`.
  - [x] Classify matched, optional, method mismatch, ambiguous, dynamic, client-only, server-only, and unknown-gap cases.
  - [x] Preserve full source/fact provenance in each finding.

- [x] Keep MVP endpoint reporting read-only.
  - [x] Prove report generation does not mutate `endpoint_matches`.
  - [x] Defer `--write-derived` and derived DB writes unless explicitly pulled into a follow-up.
  - [x] If `--write-derived` is implemented later, add boolean flag parsing support and delete old rows by `derivedBy` before inserting new rows.

- [x] Add dependency surface extraction.
  - [x] Render HTTP client and route surfaces.
  - [x] Render SQL-shape and query-builder surfaces.
  - [x] Render `SqlTextUsed`, `DapperCallDetected`, and `SqlCommandDetected` as hash/length or operation/source metadata evidence only.
  - [x] Render `n/a` for table/column fields when only hash/length SQL evidence exists.
  - [x] Render package/config surfaces only from the explicit fact/property keys listed in the design.
  - [x] Derive deterministic surface display names using the design fallback order.

- [x] Add dependency edge extraction.
  - [x] Read `combined_dependency_edges`.
  - [x] Include calls, creates, symbol relationships, and parameter-forwarding edges.
  - [x] Preserve source label, edge IDs, rule IDs, evidence tiers, and file spans.

- [x] Add Markdown writer.
  - [x] Sections: Summary, Sources, Endpoint Alignment, Dependency Surfaces, Dependency Edges, Needs Review, Known Gaps, Limitations.
  - [x] Deterministic sort order.
  - [x] Row caps with truncation notices.
  - [x] Inline coverage-relative caveats for unmatched endpoints.

- [x] Add JSON writer.
  - [x] Emit stable top-level shape.
  - [x] Include all rows without Markdown caps.
  - [x] Use `null` and empty arrays consistently for missing values.

- [x] Wire CLI.
  - [x] Replace `report` skeleton with combined dependency report implementation.
  - [x] Update root help and report help.
  - [x] Treat missing-extension output paths as directories.
  - [x] Print useful completion summary.

- [x] Add tests.
  - [x] Single-language index rejection.
  - [x] Markdown and JSON output.
  - [x] Source coverage warnings.
  - [x] Endpoint classifications, fan-out matching, and same-source matching.
  - [x] No mutation of `endpoint_matches`.
  - [x] Dependency edge rows.
  - [x] SQL/query rows without raw SQL, including `SqlTextUsed`-only rows.
  - [x] Dynamic URL findings without raw URL fragments.
  - [x] JVM/Python language inference.
  - [x] Markdown 200-row truncation notice with full JSON rows.
  - [x] Deterministic output ordering.

- [x] Update docs.
  - [x] README quickstart for `combine -> report`.
  - [x] `docs/ACCEPTANCE.md` combined report acceptance.
  - [x] `docs/VALIDATION.md` smoke command.
  - [x] Rule catalog only if a derived rule ID is introduced.

- [x] Validate.
  - [x] `dotnet build src/dotnet/TraceMap.sln`
  - [x] `dotnet test src/dotnet/TraceMap.sln`
  - [x] `./scripts/check-private-paths.sh`
  - [x] `git diff --check`

## Deferred Follow-Ups

- Snapshot/diff between two combined indexes.
- Opt-in `--write-derived` endpoint persistence after deciding schema behavior for one-sided findings.
- HTML report output.
- Cross-source path search over combined dependency edges.
- More complete package/dependency taxonomy.
- Parser-backed SQL normalization.
- Rule-backed derived facts if TraceMap formalizes derived fact rules.
