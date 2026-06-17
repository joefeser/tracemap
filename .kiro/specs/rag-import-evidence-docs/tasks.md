# RAG-Import Evidence Docs Tasks

## Spec-Only PR Scope

- [x] Add Kiro spec files under `.kiro/specs/rag-import-evidence-docs/`.
- [x] Keep this PR limited to specification files and implementation-state
  notes.
- [x] Do not implement product code, site files, generated docs output, or rule
  catalog entries in this PR.

## Implementation Tasks

- [ ] 1. Confirm command boundary, schemas, and rule namespace. Requirements: 1, 2, 3, 8, 9.
  - [ ] Confirm final command shape as `tracemap docs-export --index <index-or-combined.sqlite> --out <path>`.
  - [ ] Define `tracemap-evidence-docs.v1` manifest and JSONL chunk schemas.
  - [ ] Define gap object schema, including `UnknownAnalysisGap` records and closed reason codes.
  - [ ] Define generated Markdown frontmatter sentinel and content hash behavior.
  - [ ] Define canonical JSON, JSONL, frontmatter, Markdown, array, and file ordering rules.
  - [ ] Define `--format` as one comma-separated value containing one or both closed tokens, `markdown` and `jsonl`, with token order normalized.
  - [ ] Define `--families` as one comma-separated value from the closed vocabulary and implement unsupported-family gap behavior.
  - [ ] Treat repeated `--format`, duplicate/out-of-set/empty `--format` tokens, empty `--families`, whitespace-only `--families`, and out-of-vocabulary family tokens as sanitized CLI argument errors.
  - [ ] Define stable ID hash contexts, input fields, truncation length, collision handling, and duplicate-identity gaps for chunks, citations, redactions, gaps, limitations, file names, links, and manifest entries.
  - [ ] Add `docs-export.*.v1` rule catalog entries before emitting docs-export chunks, gaps, limitations, or validation findings.
  - [ ] Document rule limitations for static evidence, schema compatibility, claim levels, redaction, generated-file safety, and external ingestion boundaries.

- [ ] 2. Implement input readers and compatibility gates. Requirements: 1, 4, 5.
  - [ ] Read single-source TraceMap indexes read-only and project supported fact, source, gap, and limitation evidence.
  - [ ] Read combined indexes read-only and preserve combined source labels, source index IDs, commit SHAs, scan IDs, fact IDs, edge IDs, rule IDs, evidence tiers, coverage labels, and extractor metadata.
  - [ ] Read combined dependency report JSON through documented schema fields.
  - [ ] Read route-flow report JSON through documented schema fields.
  - [ ] Read paths and reverse report JSON through documented schema fields.
  - [ ] Read release-review report JSON through documented schema fields.
  - [ ] Read vault `graph.json` only when schema-compatible and use it as supplemental link/claim metadata, not as a second analysis source.
  - [ ] Read evidence-pack JSON only when schema-compatible and use it for reviewed claim-level proof and safe evidence sections.
  - [ ] Read source-claim catalog JSON and match entries by stable source identity.
  - [ ] Audit compatible report JSON schemas for stable row/finding/section IDs before implementing report-derived chunks.
  - [ ] Emit missing-provenance or identity gaps when a compatible report lacks stable IDs required for chunk identity.
  - [ ] Emit schema-incompatible, unsupported-family, missing-provenance, and reduced-coverage gaps for unknown or incompatible inputs.
  - [ ] Fail with sanitized diagnostics when no compatible input can produce any chunks.

- [ ] 3. Implement the chunk projection model. Requirements: 3, 4, 5.
  - [ ] Add internal models for chunks, citations, source refs, supporting IDs, redactions, links, gaps, limitations, and manifest records.
  - [ ] Implement source-overview chunks.
  - [ ] Implement endpoint chunks for route bindings, HTTP client calls, endpoint alignment, dynamic URL gaps, and safe route keys.
  - [ ] Implement route-flow chunks from route-flow reports or compatible path evidence.
  - [ ] Implement property-flow/value-origin chunks from argument, parameter, alias, constructor/member, callback, async, boundary, and dependency-surface evidence.
  - [ ] Implement dependency-surface and data-surface chunks for HTTP, SQL/query, persistence, packages, config, WCF, remoting, ASMX, WebForms, WinForms, legacy data, storage, and event/message evidence where available.
  - [ ] Implement package/config/query/SQL-shape chunks using safe metadata only.
  - [ ] Implement legacy chunks from compatible legacy evidence families and evidence-pack sections.
  - [ ] Implement release-review and impact-summary chunks without release approval language.
  - [ ] Implement gap and limitation chunks and link claim-bearing chunks to them.
  - [ ] Preserve original rule IDs/evidence tiers and add docs-export packaging rule IDs where needed.
  - [ ] Preserve supporting fact IDs, edge IDs, route-flow IDs, value-flow IDs, path IDs, reverse IDs, report section IDs, vault node/edge IDs, evidence-pack IDs, and release-review IDs.
  - [ ] Emit `UnknownAnalysisGap` for absence-like chunks whenever full credible coverage is not proven.

- [ ] 4. Implement claim-level filtering and promotion. Requirements: 6.
  - [ ] Default raw indexes and report inputs to `hidden`.
  - [ ] Implement `--minimum-claim-level hidden|demo-safe|public-safe`.
  - [ ] Promote only through stable source identity proof from source-claim catalog, compatible vault metadata, or compatible evidence-pack metadata.
  - [ ] Reject display-name-only promotion and emit unmatched claim catalog gaps.
  - [ ] Split mixed-claim chunks where practical; otherwise cap at the lowest safe claim level.
  - [ ] Recompute manifest claim level, chunk counts, omitted counts, gaps, and limitations after filtering.
  - [ ] Emit hidden-evidence omission gaps when filtering changes interpretation.
  - [ ] Map evidence-pack `local-only` to docs-export `hidden`; allow only `demo-safe` and `public-safe` pack metadata to promote chunks.
  - [ ] Fail demo/public output when filtering leaves no visible claim-bearing non-gap chunks.
  - [ ] Ensure filtering does not mutate input artifacts.

- [ ] 5. Implement Markdown, JSONL, and manifest writers. Requirements: 2, 3, 4.
  - [ ] Write `manifest.json` with deterministic content hash.
  - [ ] Write `chunks.jsonl` with one canonical chunk object per line.
  - [ ] Write `README.md`, `index.md`, and per-chunk Markdown files with generated frontmatter sentinels.
  - [ ] Generate deterministic relative Markdown links and stable file names.
  - [ ] Escape Markdown headings, tables, link labels, inline code, and frontmatter fields.
  - [ ] Include citations near every claim-bearing Markdown chunk.
  - [ ] Include gaps and limitations near affected claim text.
  - [ ] Support `--dry-run` without writing files.
  - [ ] Support directory output only for v1 unless a later design defines single-file output.
  - [ ] Ensure JSONL and Markdown are generated from the same chunk model.

- [ ] 6. Implement generated-file collision handling. Requirements: 2.
  - [ ] Detect valid generated Markdown files by schema, generator, chunk ID, and self-consistent content hash.
  - [ ] Detect valid generated manifest and JSONL files by schema, generator, and content hash where applicable.
  - [ ] Replace valid generated files only after the new output passes all safety validation.
  - [ ] Reject stale, hand-edited, malformed, or hash-invalid generated files unless `--force` is supplied.
  - [ ] Reject non-generated user files and directories that would collide with generated output.
  - [ ] Ensure `--force` cannot bypass claim-level, redaction, private-path, schema, unsafe-value, or non-generated collision gates.
  - [ ] Emit sanitized generated-file diagnostics with docs-export validation rule IDs.

- [ ] 7. Implement redaction and safety validation. Requirements: 6, 7, 8.
  - [ ] Scan every Markdown file, JSONL string leaf, manifest string leaf, frontmatter value, file name, link, tag, diagnostic, and log string for unsafe content.
  - [ ] Scan JSONL `bodyMarkdown` with the same Markdown escaping, prohibited-claim wording, and unsafe-value gates as generated `.md` files.
  - [ ] Reject local absolute paths, home fragments, raw remotes, raw SQL, raw config values, connection strings, raw URLs, endpoint addresses, credentials, tokens, secret-like values, source snippets, analyzer diagnostics, stack traces, private identifiers, production data, and unsafe Markdown.
  - [ ] Return sanitized category and location diagnostics without echoing unsafe values.
  - [ ] Use context-separated deterministic hashes only for explicitly allowed safe input categories.
  - [ ] Omit or category-label secret-like, credential-like, low-entropy private, enumerable private, snippet-derived, and production-data values.
  - [ ] Add a prohibited-claim wording check that rejects runtime execution, production traffic, release approval, vulnerability, ownership, deployment, service reachability, and business-impact claims unless a separate rule-backed input explicitly supports bounded wording.
  - [ ] Prove the implementation does not introduce LLM, embedding, vector database, retrieval API, prompt-classification, or natural-language Q&A code paths in TraceMap core.

- [ ] 8. Add tests and fixtures. Requirements: 1, 2, 3, 4, 5, 6, 7, 8, 10.
  - [ ] Add synthetic/public-safe fixtures for single index, combined index, route-flow report, value-origin/property-flow evidence, release-review report, vault metadata, evidence pack metadata, and source-claim catalog promotion.
  - [ ] Test deterministic manifest, JSONL, and Markdown byte stability.
  - [ ] Test hidden outputs without `--date` use the `local-only` date sentinel and remain byte-stable.
  - [ ] Test schema shape and required fields for `tracemap-evidence-docs.v1`.
  - [ ] Test stable chunk IDs across reruns, output directories, and input row permutations.
  - [ ] Test stable ID input records use the documented length-prefixed field delimiter format.
  - [ ] Test duplicate chunk identity emits gaps rather than arbitrary merges.
  - [ ] Test every claim-bearing chunk includes rule IDs, evidence tiers, source label, commit identity where available, coverage label, file path/line span where available, supporting IDs, extractor/version where available, gaps, and limitations.
  - [ ] Test lower-tier, reduced-coverage, fallback, high-fan-out, hidden, syntax-only, and textual evidence is not promoted.
  - [ ] Test all v1 chunk families and unsupported-family gaps.
  - [ ] Test `--families ''`, `--families ' '`, out-of-vocabulary family tokens, and mixed valid/invalid family tokens produce sanitized parse errors rather than selecting all or zero families.
  - [ ] Test claim-level filtering, source-claim promotion, unmatched catalog gaps, mixed-claim chunk handling, hidden omission gaps, and public/demo no-visible-evidence failure.
  - [ ] Test evidence-pack `local-only` maps to docs-export `hidden` and cannot promote demo/public chunks.
  - [ ] Test redaction rejects planted unsafe values in Markdown, JSONL, manifest, frontmatter, file names, links, diagnostics, and logs.
  - [ ] Test JSONL `bodyMarkdown` is scanned with the same Markdown and unsafe-value gates as generated `.md` files.
  - [ ] Test validator diagnostics do not echo unsafe planted values.
  - [ ] Test prohibited-claim wording validation rejects unsupported runtime, production, release approval, vulnerability, ownership, deployment, reachability, and business-impact claims.
  - [ ] Test generated-file collision handling for valid generated files, stale generated files, malformed sentinels, non-generated user files, and `--force` boundaries.
  - [ ] Test stale, hand-edited, and non-generated collision handling for summary Markdown files such as `README.md` and `index.md`.
  - [ ] Test `--force` does not bypass non-generated user file collisions.
  - [ ] Test `--dry-run` writes no files.
  - [ ] Test `--format markdown`, `--format jsonl`, and `--format markdown,jsonl`.
  - [ ] Test `--format jsonl,markdown` normalizes to `markdown,jsonl`.
  - [ ] Test `--format markdown` omits `chunks.jsonl` and `--format jsonl` omits generated Markdown files.
  - [ ] Test `manifest.json` is written for every successful `--format` value and remains the generated-file integrity anchor.
  - [ ] Test repeated `--format` and out-of-set `--format` values fail with sanitized parse errors.
  - [ ] Test explicit `--date` is required for demo/public byte-stable outputs.
  - [ ] Test demo/public output without `--date` fails.
  - [ ] Test missing report-derived stable IDs emit missing-provenance or identity gaps.
  - [ ] Test manifest `contentHash` blanks its own hash field and is stable across input row permutations.
  - [ ] Test every emitted `docs-export.*.v1` rule ID exists in `rules/rule-catalog.yml` before implementation emits it.
  - [ ] Test every closed gap reason maps to a defined docs-export or underlying rule ID.
  - [ ] Test a valid non-TraceMap SQLite file supplied to `--index` fails with a sanitized diagnostic and does not echo the file path.
  - [ ] Test a combined index with valid schema but zero usable source/fact/schema evidence fails with a sanitized diagnostic.
  - [ ] Test generated-marker recognition for `manifest.json` and `chunks.jsonl`, including a `chunks.jsonl` without a valid generated manifest.
  - [ ] Test canonical frontmatter key ordering keeps fixed keys first even when optional keys would sort earlier.
  - [ ] Test JSONL `bodyMarkdown` and corresponding generated `.md` body content come from the same chunk model.
  - [ ] Test a displayed 24-hex stable ID collision from different full ID input records emits duplicate-stable-identity rather than merging chunks.
  - [ ] Test no network, LLM, embedding, vector database, prompt-classification, or RAG integration behavior is invoked.

- [ ] 9. Update docs and validation guidance. Requirements: 8, 9, 10.
  - [ ] Add docs for command usage with placeholders only.
  - [ ] Document input support and schema compatibility behavior.
  - [ ] Document manifest, JSONL, Markdown frontmatter, stable IDs, and deterministic ordering.
  - [ ] Document claim levels, source-claim promotion, filtering, and hidden omission gaps.
  - [ ] Document redaction categories and unsafe value behavior.
  - [ ] Document generated-file collision behavior and `--force` limits.
  - [ ] Document that TraceMap emits deterministic evidence docs for external systems and does not implement RAG, embeddings, vector writes, prompt classification, or Q&A.
  - [ ] Update rule catalog documentation for `docs-export.*.v1` rules.
  - [ ] Update `docs/VALIDATION.md` when implementation changes shared report/export behavior.

- [ ] 10. Validate implementation. Requirements: 10.
  - [ ] Update this spec's `implementation-state.md` with branch, scope decisions, validation, and follow-ups.
  - [ ] Run focused docs-export tests.
  - [ ] Run generated-file collision and redaction tests.
  - [ ] Run `dotnet build src/dotnet/TraceMap.sln`.
  - [ ] Run `dotnet test src/dotnet/TraceMap.sln`.
  - [ ] Run `./scripts/check-private-paths.sh`.
  - [ ] Run `git diff --check`.
  - [ ] Run or explicitly defer relevant pinned smoke checks from `docs/VALIDATION.md`.

## Recommended PR Slices

- PR 1: Schema, rule catalog, manifest/JSONL/Markdown model, stable IDs, and
  command shell with no analyzer changes.
- PR 2: Combined and single-index source/fact projection plus core source,
  endpoint, dependency-surface, gap, and limitation chunk families.
- PR 3: Route-flow, property-flow/value-origin, release-review, vault, and
  evidence-pack readers.
- PR 4: Claim-level promotion/filtering, redaction hardening, generated-file
  collision handling, and public/demo fixtures.
- PR 5: Documentation, validation guidance, and broader smoke coverage.

## Deferred Follow-Ups

- Evidence-pack-only docs export without an index root.
- HTML browsing over generated chunks.
- Optional raw snippet mode behind a separate safety spec.
- Site consumption or hosted public docs.
- External connectors for specific knowledge-base or RAG products outside
  TraceMap core.
- Chunk tuning based on external importer feedback.
