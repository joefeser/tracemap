# Access VBA Source Export Requirements

## Goal

Produce a private, owner-controlled VBA source bundle from a disposable Access
copy so the existing deterministic VBA projector can emit bounded static facts.

## Requirements

1. The Windows-only exporter uses `Application.SaveAsText` for saved module
   serialization and does not access VBE, ActiveVBProject, VBComponents, or
   source-line APIs.
2. It requires a hash-identical disposable copy before export, an unchanged
   original, compatible protected form/report metadata bundle, provenance
   hashes, and separate generation/extraction canaries. The output records
   pre/post supplied-copy and inner-working-copy hashes with typed outcomes.
3. Before opening the copy it forces automation security and invisibility. It
   never opens/renders a form/report, executes a query/macro/VBA procedure,
   inspects rows, or invokes an event.
4. Module count before/after extraction is a canary. A changed count, visible
   Access, fired canary, original-source mutation, timeout, or cleanup failure
   fails closed and removes partial output. A disposable-copy mutation is
   explicitly recorded as `AccessVbaWorkingCopyChanged`; it is never presented
   as original-source mutation or evidence that source content was unchanged.
   The supplied copy remains an integrity gate; only the per-run inner scratch
   copy may receive Access bookkeeping after its StartupForm property is
   removed through DAO.
5. Raw module files and full form/report definitions live separately from
   normalized protected evidence input. Layout parsing is deferred.
   Standard artifacts contain only hashes, safe identities, spans, rule IDs,
   provenance, coverage, gaps, and limitations—not raw source or expressions.
6. The normalized input reuses `vba-module` and `AccessVbaProjector`. Exact
   zero-argument event expressions and conventional event procedures may map
   only to same-module procedure candidates.
7. Form/report/control lifecycle properties preserve owner kind, event role,
   binding kind, procedure candidate, source span, and hash-only event
   expression where applicable. Embedded macros and dynamic handlers are gaps.
8. Bounded active handler effects may record `Me` state assignments,
   `Me.Requery`, literal `Forms(...)` references, and already supported open
   calls. Conditions are hash-only syntax context; comments are inactive.

## Non-claims

This lane does not prove a form loaded, an event fired, a procedure executed,
a query ran, records changed, navigation, lifecycle order, business intent, correctness,
completeness, production use, or release approval.
