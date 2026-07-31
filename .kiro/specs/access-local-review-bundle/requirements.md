# Access Local Review Bundle Requirements

## Purpose

Prepare a locally usable Microsoft Access design-review packet from evidence
already emitted by the shipped Windows adapter. The workflow is for a private,
operator-controlled review session. It does not add Access extraction or
strengthen the adapter's count-only UI, VBA, or macro boundary.

## Requirements

### 1. One-command local composition

1. `tracemap access-review create` SHALL accept an existing Access scan output
   directory and a new output directory.
2. The command SHALL require the standard `scan-manifest.json`,
   `facts.ndjson`, `index.sqlite`, `report.md`, and `logs/analyzer.log`
   artifacts.
3. The command SHALL reject inputs without compatible Access evidence instead
   of presenting an unrelated scan as an Access review.
4. The command SHALL not invoke Access, COM, a scanner, a query, a macro, VBA,
   a form, or a report.

### 2. Bundle contents

1. The bundle SHALL contain an Access-only release-review Markdown and JSON
   packet generated with the same index as both before and after input.
2. The bundle SHALL contain a `hidden-local` static HTML evidence explorer
   generated from the existing scan artifacts.
3. The bundle SHALL contain a deterministic manifest and README using only
   relative links and safe categorical/count metadata.
4. The manifest SHALL record the source commit SHA, coverage, Access evidence
   status, finding/gap counts, explorer evidence-row/gap counts, and SHA-256
   hashes for generated files other than the manifest itself.
5. Generated output SHALL have no wall-clock timestamp.

### 3. Evidence and safety boundaries

1. The bundle SHALL preserve upstream rule IDs, evidence tiers, coverage
   labels, repository-relative spans, commit SHA, extractor versions,
   supporting fact IDs, gaps, and limitations through the existing reports.
2. The bundle SHALL not render machine-local absolute database paths, private
   object display identities, raw SQL, query hashes, connection material,
   credentials, server names, captions, expressions, VBA, macro bodies, or
   exception text. Repository-relative database evidence spans remain required
   provenance.
3. The README and manifest SHALL state that the bundle contains static design
   evidence only and does not prove row contents, execution, runtime
   reachability, effective permissions, production state, correctness,
   compatibility, safety, or approval.
4. Form/report, VBA-module, and macro counts SHALL remain count-only with
   explicit identity/source-unavailable gaps.
5. Partial analysis SHALL remain labeled partial.
6. The finding cap SHALL be configurable within a documented finite bound,
   ordered deterministically, and SHALL retain explicit truncated status,
   omitted counts, and a truncation gap whenever findings are omitted.

### 4. Output ownership and determinism

1. The input and output directories SHALL not overlap by equality or ancestry.
2. A pre-existing output SHALL be rejected unless `--force` is supplied and
   the directory contains a compatible TraceMap-generated bundle manifest.
3. Replacement SHALL use a sibling staging directory and SHALL not recursively
   delete an unrecognized caller-owned directory.
4. Two runs from byte-identical inputs SHALL produce byte-identical bundle
   files.
5. Failure messages SHALL be categorical and SHALL not print private paths or
   protected values.

### 5. Local dogfood and handoff

1. Documentation SHALL provide a repeatable Windows/Parallels workflow using
   the existing synthetic or explicitly authorized representative smoke.
2. The workflow SHALL keep databases, raw scan output, and the generated bundle
   local by default.
3. Representative validation SHALL continue to require explicit local input
   authorization and the existing cleanup/canary contract.
4. Windows validation SHALL use the shipped adapter boundary unchanged.

## Non-goals

- No richer form, report, control, binding, VBA, event, navigation, or macro
  extraction.
- No row reads, query execution, live schema introspection, linked-source
  refresh, Access rendering, or object invocation.
- No public-site publication or customer-data upload.
- No LLM calls, embeddings, vector databases, or prompt-based classification.
