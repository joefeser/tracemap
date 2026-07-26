# Database Design-Review Single-Index Design

## Decision

Extend the existing read-side reporter with a small input adapter. Detect the
TraceMap index shape from its tables, then normalize either input to the
reporter's existing source, fact, safe-surface, and extractor-provenance
models.

For combined input, retain `CombinedDependencyReporter.ReadAsync`,
`BuildSurfaces`, and both bounded path searches unchanged.

For single input, reuse `ReleaseReviewReporter.ReadSqlEvidenceInputsAsync`,
construct one bounded source descriptor from the stored scan manifest, and
adapt only those already-filtered facts to the existing safe SQL surface
projection. Do not invoke the combined path reporter. Emit one
`SingleIndexRoutePathUnavailable` packet gap and zero route references.

## Coverage

A valid single index with compatible database-design evidence is `partial`
because cross-fact route traversal is outside this input contract. Missing
PostgreSQL design evidence remains `unavailable`. Source manifest known gaps
and reduced build/analysis identity remain explicit packet gaps.

## Safety

The output schema and allowlists do not change. The adapter never renders
arbitrary fact properties, raw SQL, source text, hashes, connection material,
local paths, or private infrastructure identifiers.

