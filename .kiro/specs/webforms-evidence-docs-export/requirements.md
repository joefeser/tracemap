# Requirements: Web Forms evidence docs export

1. `tracemap docs-export` shall accept one or more existing `webforms-modernization-packet.v1` JSON artifacts as supplemental read-only inputs.
2. The exporter shall project the packet's deterministic inventory, event chains, downstream boundaries, identity/state evidence, batch/data-movement evidence, structural slices, gaps, questions, and limitations into citation-bearing documentation chunks.
3. Every projected claim shall preserve the packet rule IDs, evidence tiers, coverage labels, commit identity, file spans, and supporting fact or edge IDs that support it.
4. The output shall remain suitable for ordinary documentation search, knowledge-base indexing, or retrieval ingestion without TraceMap calling a model, generating embeddings, writing a vector database, shipping prompts, or generating a BRD.
5. Raw source, raw SQL, configuration values, credentials, local absolute paths, and inferred business intent shall not be added to the corpus.
6. Packet schema mismatch, unreadable input, missing provenance, truncation, and reduced coverage shall remain explicit rule-backed gaps or failures rather than clean absence claims.
7. Reordering equivalent packet collections shall not change chunk IDs or output bytes.

