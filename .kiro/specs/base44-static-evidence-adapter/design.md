# Design

The TypeScript scan invokes an additive syntax extractor before semantic project loading, so Base44 evidence remains available when a packet lacks a working `tsconfig`. Existing generic facts and storage stay unchanged.

`base44-evidence` runs the ordinary scanner and writes a self-contained packet whose facts are the `Base44*` subset. The packet binds caller-supplied accepted source/tree digests, Git commit, coverage, rules, tiers, extractor versions, and hashes of the ordinary artifacts. It also writes redacted Markdown and HTML projections.

`base44-diff` compares canonical fact content rather than volatile packet paths. It marks added/removed facts and makes coverage reduction explicit. It never turns missing facts into a runtime or readiness conclusion.

The host consumer validates the published JSON Schema and compares packet facts with its existing canonical outputs. No host verdict is implemented in TraceMap.
