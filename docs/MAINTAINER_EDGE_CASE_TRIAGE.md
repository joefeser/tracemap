# Private-to-maintainer edge-case triage

This workflow exchanges bounded diagnostic metadata without publishing a
private repository, index, report, log, path, source label, value, or snapshot
identity. It is a maintainer aid, not a standard scan artifact or public proof.

The initial observation established two separate behaviors:

- A reverse-report-only hidden JSON vault export completed with 1,106 nodes,
  no edges, 1,000 gaps, and one file.
- Two tested variants containing a combined index did not complete before
  manual termination.
- Docs export originally rejected `credential-or-config` at `input.properties`
  under `docs-export.validation.unsafe-value-rejected.v1`. The bounded follow-up
  changes structured fact-property handling to omit the value while retaining
  its safe property key, supporting fact ID, category-only marker, and a
  `docs-export.redaction.unsafe-property.v1` gap. Other unsafe output locations
  remain fail closed.

This proves correlation with the combined-index path, not deadlock or root
cause. The original docs-export result was a deliberate fail-closed decision.
Synthetic coverage now proves that structured property values can be omitted
without weakening final output safety validation or echoing private content.

Safe follow-up data is limited to closed stage tokens, elapsed-time buckets,
aggregate count buckets, completion state, TraceMap's public commit, and
sanitized failure rule/category/location. Private source labels and commit SHAs
must be replaced with opaque references or an explicit unavailable state.

Useful future vault stages are `input-open`, `schema-validation`,
`source-read`, `fact-read`, `fact-projection`, `relationship-read`,
`relationship-projection`, `gap-deduplication`, `graph-finalization`,
`output-safety-validation`, `output-write`, and `completed`.

Instrumentation must remain opt-in and bounded. A timeout must return a typed
incomplete result with the last completed stage and must not leave output that
resembles a completed export.

See `prompts/collect-maintainer-edge-case-followup.md` for the private-side
collection instructions.
