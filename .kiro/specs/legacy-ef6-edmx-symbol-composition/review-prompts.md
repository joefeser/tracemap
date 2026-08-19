# Legacy EF6 EDMX Symbol Composition Review Prompts

```text
Branch: codex/spec-ef6-clr-edmx-composition
Base: origin/dev @ 1b79f4e62f9d544e120197d95b2d099e0300be1d
Spec: .kiro/specs/legacy-ef6-edmx-symbol-composition/
Issue: #680 (Part of; do not close)
```

Spec files:

- `requirements.md`
- `design.md`
- `tasks.md`
- `implementation-state.md`

Related specs:

- `legacy-data-model-metadata-extraction`
- `legacy-data-model-orm-mapping-completion`
- `legacy-data-model-relationship-composition`
- `legacy-data-model-relationship-completion`
- `ef-core-mapping-v0`

TraceMap principles:

- No conclusion without evidence. No evidence without a rule ID. No rule
  without documented limitations. No scan without repo and commit SHA.
- Failed build is not a clean repo. Partial analysis is labeled partial.
- No LLM calls, embeddings, vector databases, or prompt-based matching.
- A loud incomplete mapping is safer than a plausible wrong entity-to-table
  path.

## Sonnet Review Prompt

Review the specification packet in
`.kiro/specs/legacy-ef6-edmx-symbol-composition/` for implementation planning
readiness. Check:

1. Are the reconciliation rules in `design.md` D4/D4.1 — the namespace
   evidence ladder (explicit generated-type metadata, proven deterministic
   generation/project metadata, scoped qualified-name equality convention,
   typed gap fallback), single-assembly uniqueness, and
   entity-set/type/fragment/store-set/scalar resolution — exact and
   mechanically implementable with no room for interpretation?
   In particular, does every emitted edge name a `namespaceBridgeFactId` that
   appears in `supportingFactIds` and controls weakest-link tier and coverage?
2. Is the D4.1 separation between currently available evidence, evidence
   requiring a bounded extractor addition, unsupported shapes, and future
   possibilities airtight, with no claimed metadata read the scanner cannot
   perform today?
3. Does every fail-closed row in the D9 table map to a requirement, a gap
   classification, and a fixture case, including association mappings and
   same-name/version assemblies in distinct compilation scopes?
4. Is the evidence contract in D7 complete for every composed kind (rule ID,
   tier, span, extractor version, supporting fact IDs, coverage, limitations)?
5. Are the four relationship kinds, their directions, and their target
   descriptor scopes unambiguous for persistence and reverse traversal?
6. Is the fixture matrix F1–F15 sufficient to prove the acceptance criteria of
   issue #680, including SSDL same-column-name decoys and staged generated-file
   candidate intersection, and are the assertions identity-level rather than
   count-level?
7. Are any implementation steps missing, mis-ordered, or undersized in
   `tasks.md`?

Return: blockers, important findings, suggested edits, and missing tests.

## Opus Review Prompt

Review the specification for merge readiness against repository evidence.
Verify against the actual code on the base commit:

1. Rule decision: is a new `legacy.data.edmx.symbol-composition.v1` justified
   over extending `legacy.data.generated-link.v1`, per the catalog entries in
   `rules/rule-catalog.yml` and the contract docs?
2. Tier ceilings: do the Tier2-capped conceptual and storage edges respect the
   principle that composition never upgrades EDMX descriptor facts beyond
   Tier2Structural? Confirm conceptual edges use the weakest tier of the CLR
   declaration, selected namespace bridge, and CSDL descriptor, and that no
   mechanism claims a Tier1 composed edge.
3. Namespace ladder: verify D4/D4.1 against the repository — confirm the
   design separates currently available evidence from bounded extractor
   additions, claims no metadata read the scanner cannot perform today
   (attribute or generation/project), and fails closed with
   `UnresolvedGeneratedNamespace` for custom namespaces without a
   deterministic bridge. Confirm the selected bridge is an explicit supporting
   fact and that Tier3 generated-link fallback cannot authorize composition.
4. Static-only claims: does any requirement, decision, or fixture imply
   runtime model loading, database access, generated-code execution, schema
   existence, or EDMX deployment/currency? Any overclaim must be flagged.
5. Consumer behavior: is reusing `SymbolRelationship` persistence
   (`symbol_relationships`, `combined_symbol_relationships`,
   `combined_dependency_edges`) sound, and is the resolved opt-in `mapping`
   reverse-impact filter consistent with `tracemap.reverse-impact.v1`'s closed
   contract, with defaults unchanged and the existing `database` filter
   untouched (its edges being deterministic static compiler evidence of
   database operation call patterns, not runtime proof)?
6. Fail-closed coverage: enumerate any ambiguous or unsupported join shape
   missing from the D9 table, including shapes a reviewer could plausibly
   encounter in checked-in EF6 EDMX files (including generated/custom
   namespace shapes, canonical-ID collisions across same-name/version
   assemblies, SSDL column names repeated across storage types, association
   mappings, and missing semantic property evidence).
7. Privacy: could any composed property leak snippets, connection strings,
   provider secrets, local paths, or private identifiers? Is the safe
   display-name policy applied on both endpoints?

Return: blockers, important findings, suggested edits, missing tests, and a
ready/not-ready verdict for implementation planning.

## Qodo/Gemini Review Prompt

Review `.kiro/specs/legacy-ef6-edmx-symbol-composition/` for privacy,
determinism, and false-positive risks in the proposed EF6 CLR-to-EDMX
composition. Focus on:

1. Global short-name matching: confirm no requirement, decision, or fixture
   permits simple-name, case-insensitive, or fuzzy joins between CLR symbols
   and EDMX descriptors.
2. Namespace bridging: confirm divergent generated/custom CLR namespaces
   compose only through explicit ladder evidence (supported generated-type
   metadata, proven deterministic generation/project metadata, or the scoped
   equality convention) and otherwise produce `UnresolvedGeneratedNamespace`
   gaps, never edges.
3. Determinism: confirm fact identity, ordering, and gap deduplication rules
   guarantee identical output across repeated scans of the same commit.
4. False positives: identify any scenario where a composed entity-to-table or
   property-to-column edge could be wrong rather than merely incomplete (for
   example, duplicate qualified names, multi-container models, generated code
   that diverges from the EDMX, same-name/version assembly collisions, SSDL
   same-column-name decoys, or decoy type names), and confirm the spec fails
   closed there.
5. False negatives loudness: confirm every no-edge outcome has a rule-backed
   gap so silence is never mistaken for proof of no mapping.
6. Secret leakage: confirm the composed property set contains only safe
   identifiers, hashes, keys, IDs, and closed-vocabulary codes.

Return actionable findings with exact file and section references.
