# Design: Web Forms agent evidence handoff

`WebFormsCodePathReview` writes a private `case-NNN.handoff.json` beside each
private case report. The private report links to that file and the set-level
`agent-evidence-handoff.json`; shareable artifacts do neither.

The case handoff contains provenance, a bounded subject summary, ordered local
report sections, retained evidence references, corpus selectors, closed
retrieval hints, external evidence questions, and explicit limitations. Query
hints reuse `EvidenceDocsQueryRecipes.Build()` as the source of truth for
recipe IDs, parameters, result fields, rules, and tiers.

After all cases succeed, `WebFormsAgentEvidenceHandoff.WriteSet` validates the
case handoffs and writes the root manifest. An optional TraceMap index is opened
in read-only mode and its `scan_manifest` row must match the inspection scan and
commit. An optional docs-export root is validated through `manifest.json`,
`query-recipes.json`, and bounded streaming reads of `chunks.jsonl`; chunks are
selected only by retained supporting IDs or exact retrieval-hint parameters.

The review-set PowerShell entry point remains the operator surface. It resolves
the configured index when available and accepts explicit `-IndexPath` and
`-EvidenceDocsRoot` overrides. Missing optional artifacts remain visible as an
availability state rather than being inferred as absent evidence.

All handoff artifacts are private. They may contain retained paths, symbols,
fact IDs, and local relative locators. Anonymous reports remain alias-only and
are checked for private-token leakage before publication.
