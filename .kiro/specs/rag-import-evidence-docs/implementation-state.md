# RAG-Import Evidence Docs Implementation State

## Status

not-started

## Current Branch

`codex/spec-rag-import-evidence-docs`

## Scope

This branch is spec-only for TraceMap issue #172. It adds the Kiro spec for
RAG-import-ready evidence documentation under
`.kiro/specs/rag-import-evidence-docs/`.

No product code, site files, generated output, docs pages, rule catalog entries,
or implementation tests are intentionally changed in this PR.

## Scope Decisions

- Preferred command shape is `tracemap docs-export --index <index-or-combined.sqlite> --out <path>`.
- The CLI deliberately avoids `rag` in the command name because TraceMap emits
  deterministic evidence documents and does not implement a RAG stack.
- `--index` is required for v1 so every export remains anchored to a scan or
  combined index with source identity and commit provenance.
- Optional report inputs enrich chunks but do not replace the index root.
- Output schema is `tracemap-evidence-docs.v1`.
- Primary machine-ingestion output is `chunks.jsonl`; Markdown is the
  human-inspectable companion output.
- Hidden outputs without `--date` use the fixed generated-date sentinel
  `local-only` and remain byte-stable; demo/public date metadata requires
  `--date YYYY-MM`.
- Claim levels reuse the vault/evidence-pack vocabulary:
  `hidden`, `demo-safe`, and `public-safe`.
- Evidence-pack `local-only` maps to docs-export `hidden`; only demo/public pack
  metadata can promote chunks when matched by stable source identity.
- `--exit-code` is not planned for docs-export v1; public/demo no-visible-
  evidence failure is unconditional.
- Raw snippets remain out of v1 by default and any future snippet option needs a
  separate safety update.
- No LLM calls, embeddings, vector database writes, prompt-based
  classification, natural-language Q&A, or RAG-stack integration belong in
  TraceMap core.

## Alignment Notes

- Vault export supplied the generated-file sentinel, stable hash, collision,
  claim-level, and safety model pattern.
- Evidence packs supplied promotion and public/demo claim-level constraints.
- Route-flow supplied static path wording, route-centered provenance, and
  report composition boundaries.
- Parameter/value-origin flow supplied the property-flow/value-origin boundary
  and non-taint limitations.
- Release review supplied section-status, checklist provenance, and no-release-
  approval language.
- Combined dependency reporting supplied source, endpoint, surface, SQL/query,
  package/config, and edge provenance patterns.

## Validation

Spec-only validation to run before PR:

- `git diff --check`
- `./scripts/check-private-paths.sh`
- Kiro CLI Opus and Sonnet reviews when locally available, or documented
  self-review if unavailable.

Implementation validation is listed in `tasks.md` and should run when product
code changes.

## Pre-Implementation Gates

- Rule catalog entries must land in `rules/rule-catalog.yml` in the first commit
  of implementation PR 1 before any implementation code emits a
  `docs-export.*.v1` rule ID.
- Confirm that `source-claim-catalog.v1` field requirements are stable in the
  vault export workflow before implementing task 2 input readers. If the vault
  schema is still evolving, defer catalog-based promotion to PR 4 and emit
  claim-level-unmatched gaps in the interim.
- Confirm `evidence-graph-vault-export.v1` is locked before implementing vault
  graph claim promotion. If not locked, supplied vault graph inputs should emit
  schema gaps and claim promotion should remain deferred.
- Audit route-flow, release-review, combined dependency report, paths, and
  reverse report JSON for stable row/finding/section IDs before implementing
  report-derived chunk readers. If IDs are unavailable, emit missing-provenance
  or identity gaps rather than unstable chunks.

## Follow-Ups

- Decide whether shared vault/evidence-pack validators should move to a common
  reporting safety package before implementation.
- Confirm compatible report JSON schemas for route-flow, release-review,
  combined dependency report, vault graph, and evidence packs before writing
  readers.
