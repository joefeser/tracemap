# Verify retained Web Forms projection ownership

Stay on the same retained index and selected page/event from the last one-page
trace. This is read-only verification, not a BRD task. Do not rescan, rebuild,
install dependencies, execute the application, call services/databases, modify
the index, or choose another page. Keep source, paths, symbols, URLs and raw
diagnostics on this computer. Report aliases, public rule IDs, tiers, counts,
and categorical results only.

## Provenance and selection

Confirm the index is the same run as the matching summaries: scanner commit
`963392f4` (or locally verified descendant containing the ownership fix),
extractor `legacy-webforms/0.7.1` (or verified successor), and zero workspace
diagnostics. Do not confuse the application commit with the scanner commit.
Use the previous event binding and resolved handler, not a newly selected event.
If that selection is unavailable, ask the operator once rather than guessing.

Open SQLite read-only and enable `PRAGMA query_only=ON`. Inspect the schema
before querying. Resolve the handler through its exact `bindingFactId`; require
one unambiguous result. Retain its fact ID, `handlerSymbolId`, file and line span
locally. Avoid name-only joins.

## Find the projection through its actual relationship

`WebFormsEventFlowProjected` does not expose a direct `bindingFactId` join.
Find candidates using its `handlerSymbolId`, then require exact token membership
of the resolved handler fact ID in its comma-separated `supportingFactIds`.
Do not use substring matching for IDs. Resolve those support IDs back to facts
and verify they refer to the same selected handler and binding. Restrict every
query to the selected scan when applicable. Inspect at most ten candidates;
report ambiguity or truncation instead of selecting arbitrarily.

For the verified projection, split `supportingEdgeIds` into exact tokens and
resolve each edge. Inspect at most 100 support edges, detecting an extra row
before claiming completeness. Verify each edge's file and contained line span
against the handler. Semantic edges must have the same canonical source symbol
ID, including assembly identity. Syntax edges remain lower-tier file/span and
member-name candidates, not compiler-resolved assembly ownership. Report
missing support, mismatches, or unsupported comparisons explicitly.

Compare with the prior trace using identities rather than assuming fact IDs
survived the extractor-version change. Determine whether the two previously
unrelated same-name edges are absent and the legitimate direct handler edge
remains. If the old run is unavailable, report current ownership validation but
label the before/after comparison unverified.

## Result

Return a short sanitized report with:

- Provenance pass/fail and same-event confirmation.
- Projection candidate/match counts and support-edge count.
- Semantic identity matches, syntax-only candidates, mismatches and missing IDs.
- Prior unrelated edges absent/present/unverified; legitimate edge retained or missing.
- Any bounds hit or unresolved comparisons.
- `ownership-verified`, `ownership-mismatch`, or `ownership-verification-incomplete`.

If no projection matches, provide the parameterized SQL query (placeholders,
not private values) and count-only results for this scan's resolved handlers,
projections, handler-ID matches and exact support-ID matches. Do not infer that
the projection was intentionally removed. The extractor emits a projection for
each resolved handler; absence needs investigation of lookup or retained data.

Keep the HTTP boundary conclusion limited to the traversed path. Omitted
branches remain unverified. No runtime execution, successful binding, or absence
of database effects is established by this check.
