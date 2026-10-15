# Build the first work-app migration handoff from accepted evidence

Stop the scanner diagnostic loop. Use the accepted Save-handler report generated
at tool commit 1dc37d3e from vb-semantic/0.10.3 evidence. Its readback confirmed
two exact chains, 256 retained variants, truncated=true, and a retained route
to a compiled database API terminal. These are a bounded baseline, not whole-app
coverage or runtime proof. Do not assume a newer/different report is equivalent.

Five-minute total budget. NO build, scan, requery, new context, DLL discovery,
limit changes, database/HTTP/application execution, repository changes or commits.
Use only the accepted grouped HTML, its grouped handoff JSON, associated local
receipt, and retained evidence already available for that run. Locate them from
the previous successful invocation, not broad filesystem discovery. If unavailable
or inconsistent, stop with the missing artifact rather than regenerate it.

The grouped report already has a Backend extraction inventory, ordered variant
references, candidate classifications, provenance, and human review obligations.
Consume these; do not build another scanner or fabricate an inventory from names.

Produce a short human-readable PRIVATE migration handoff beside the accepted
report in a NEW, non-overwriting Markdown file. Do not emit a new JSON/SQLite
artifact. Record the source receipt/report filenames and their existing generator
and bounded-input identities locally so the handoff is traceable. Never copy this
private document, symbols, paths, hashes or SQL into the public tool repository.

Scope: the ORIGINAL Save handler only. Do not claim other handlers, the whole
page or the whole application are inventoried. Deliver:

1. Acceptance baseline: original root identity, two retained chains, variant
   count, truncation, coverage and provenance limitations. State that static
   routes exist while enumeration is incomplete and runtime use remains unproven.
2. One row per retained chain: exact chain reference; ordered source/compiled
   method hops; bridge kind and tier at each boundary; reached database API;
   supporting variant references. Do not turn 256 variants into 256 business
   operations, collapse distinct overloads, or splice separate paths together.
3. Contract checklist for each reached operation: command/procedure identity,
   parameter names/types/directions/defaults, result shape, connection choice,
   transaction behavior, authorization/validation and error behavior. Fill only
   what retained evidence supports, with references. Mark the rest UNRESOLVED,
   with the specific evidence needed; a wrapper name is not a procedure name.
4. First implementation slice: a proposed Save-entry-to-data-access boundary,
   preserving observed inputs/outputs and side-effect boundaries. Separate
   observed behavior from proposed design. No code, deployment, retain/replace
   decision or approval is implied. Human decisions remain undecided.
5. Acceptance-test checklist: successful save, invalid input, authorization,
   null/default parameters, database failure and transaction behavior. Label
   unverified expectations as pending contract evidence, not established behavior.
6. A prioritized missing-input list. Identify the first concrete evidence needed
   to implement the slice safely; do not use generic "more analysis needed."

Correction from the subsequent retained-chain handoff: the two retained chains
were SelectCartItems and SelectCartCount through ExecProc_DataSet to
DbDataAdapter.Fill. The earlier claim that CreateBidGroup/ExecProc_Scalar was on
these retained paths was incorrect. Treat these as read/refresh routes, not proof
of a Save write. Verify exact identities against the retained artifacts; never
substitute an earlier narrative for the chain records. Follow-up contract work is
specified in prompts/extract-save-read-and-write-contracts.md.

Return only a sanitized summary: document created yes/no, chain and operation
record counts (not business-operation counts), contract categories supported versus
unresolved, the proposed slice's generic boundary, and the first missing input.
Keep the local document location in the local receipt. Do not start another
diagnostic run or claim the application migration is complete.
