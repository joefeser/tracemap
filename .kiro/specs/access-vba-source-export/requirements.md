# Access VBA Source Export Requirements

## Goal

Produce a private, owner-controlled VBA source bundle from a disposable Access
copy so the existing deterministic VBA projector can emit bounded static facts.

## Requirements

1. The Windows-only exporter uses `Application.SaveAsText` for saved module
   serialization and does not access VBE, ActiveVBProject, VBComponents, or
   source-line APIs.
2. It requires a hash-identical disposable copy, unchanged original, compatible
   protected form/report metadata bundle, provenance hashes, and separate
   generation/extraction canaries.
3. Before opening the copy it forces automation security and invisibility. It
   never opens/renders a form/report, executes a query/macro/VBA procedure,
   inspects rows, or invokes an event.
4. Module count before/after extraction is a canary. A changed count, visible
   Access, fired canary, source mutation, timeout, or cleanup failure fails
   closed and removes partial output.
5. Raw module files live separately from normalized protected evidence input.
   Standard artifacts contain only hashes, safe identities, spans, rule IDs,
   provenance, coverage, gaps, and limitations—not raw source or expressions.
6. The normalized input reuses `vba-module` and `AccessVbaProjector`. Exact
   zero-argument event expressions and conventional event procedures may map
   only to same-module procedure candidates.

## Non-claims

This lane does not prove a form loaded, an event fired, a procedure executed,
a query ran, records changed, navigation, business intent, correctness,
completeness, production use, or release approval.
