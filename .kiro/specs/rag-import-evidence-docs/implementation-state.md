# RAG-Import Evidence Docs Implementation State

## Status

implemented

## Current Branch

`codex/issue-172-rag-evidence-docs`

## Scope

This branch implements TraceMap issue #172 as `tracemap docs-export`.

Implemented product areas:

- `TraceMap.Reporting.EvidenceDocsExporter`
- `tracemap docs-export` CLI command and help
- `tracemap-evidence-docs.v1` manifest, JSONL chunk, and Markdown output
- generated frontmatter sentinels and content hashes
- stable length-prefixed ID inputs with 24-hex displayed IDs
- read-only single-index and combined-index projection
- bounded report-input projection for compatible JSON report metadata
- explicit schema, unsupported-family, missing-provenance, unknown-analysis,
  hidden-filter, duplicate-identity, and claim-catalog gaps
- hidden/demo/public claim filtering with source-claim catalog promotion
- hidden `local-only` date sentinel and demo/public `--date YYYY-MM` gate
- generated-file collision handling with `--force` limits
- redaction and prohibited wording validation
- docs-export rule catalog entries and user documentation
- focused docs-export tests

## Scope Decisions

- The command shape is `tracemap docs-export --index <index-or-combined.sqlite>
  --out <path>`.
- `--index` is required for v1 so every export is anchored to scan or combined
  source identity.
- Raw indexes and report JSON default to `hidden`.
- Demo/public promotion requires stable source identity from
  `source-claim-catalog.v1`; display names alone do not promote evidence.
- Hidden outputs without `--date` use `local-only`; demo/public outputs require
  `--date YYYY-MM`.
- Single-source indexes emit available source/fact chunks and schema gaps for
  combined-only views.
- Report JSON inputs are consumed as bounded report metadata chunks when they
  expose documented schema/type markers; row-level report chunking remains
  constrained to fields with stable IDs.
- Vault graph claim promotion is not assumed beyond compatible schema evidence;
  incompatible supplied vault graph inputs emit schema gaps.
- Raw snippets remain out of scope.
- No LLM calls, embeddings, vector database writes, prompt-based
  classification, retrieval API calls, natural-language Q&A, or RAG-stack
  integration were added.

## Validation

Completed during implementation:

- `dotnet restore src/dotnet/TraceMap.sln`
- `dotnet build src/dotnet/TraceMap.sln --no-restore`
- `dotnet test src/dotnet/TraceMap.sln --filter EvidenceDocs`
- `dotnet build src/dotnet/TraceMap.sln`
- `dotnet test src/dotnet/TraceMap.sln`
- `./scripts/check-private-paths.sh`
- `git diff --check`

Relevant pinned smoke checks from `docs/VALIDATION.md` are explicitly deferred
unless review requests a local combined-index smoke: this branch adds a new
downstream export and focused synthetic SQLite fixtures, but does not change
scanner, reducer, combined-report, route-flow, release-review, evidence-pack,
vault, or language-adapter analysis behavior.

## Oddities

- The shared CLI parser splits comma-separated values. `docs-export` therefore
  adds pre-parse checks for repeated `--format` and empty `--format` or
  `--families` raw values before using the shared parser.
- Hidden source labels are category-safe hashed labels in generated output. A
  public/demo claim catalog can promote the source claim level, but labels are
  still safety-normalized.
- Unsafe file paths become missing-provenance gaps; raw SQL/config/credential
  values in metadata are rejected with sanitized diagnostics.

## Follow-Ups

- Expand compatible report readers to row-level chunks for every report section
  after each report schema exposes stable row/finding IDs for the requested
  family.
- Share more generated-file and redaction helpers with vault export once both
  exporters settle on a common utility shape.
- Add public/demo fixture packs that exercise source-claim promotion across
  combined report, vault, and evidence-pack inputs together.
