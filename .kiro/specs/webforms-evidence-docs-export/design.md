# Design: Web Forms evidence docs export

The existing `docs-export` command remains the single public evidence-document pipeline. A repeatable `--webforms-packet` input augments the index-derived corpus with a closed `webforms-modernization` chunk family.

The adapter deserializes only `webforms-modernization-packet.v1`, matches packet sources to index sources by stable scan and commit identity, and projects bounded packet records into the existing `EvidenceDocChunk` schema. Packet overview, surfaces, chains, downstream boundaries, identity/state records, batch/data-movement records, and structural slices become independent retrieval units. Packet gaps become first-class gap chunks; owner questions and limitations remain scoped metadata, not findings.

All collections are sorted before projection. Chunk identity uses the existing context-separated stable-ID algorithm and supporting evidence IDs. Existing Markdown/JSONL rendering, manifest hashing, collision protection, claim filtering, and unsafe-output validation remain authoritative. No prompt or BRD format is emitted.

