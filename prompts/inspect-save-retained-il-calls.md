# Correct the compiled-call inspection

Five-minute TOTAL limit. No build, tests, rescan, combine, handler requery,
decompiler installation, DLL search, source changes, or application execution.

The previous report searched for compiled CallEdge facts. That is the wrong
fact type: TraceMap emits ManagedIlCallObserved linked to ManagedIlBodyDeclared
by properties.ilBodyFactId. Those calls are emitted separately from value and
command-binding gaps. A gap does not establish that calls are absent.

Fast-forward the clean TraceMap branch codex/evidence-admission-hardening only
to obtain this helper. Preserve unrelated changes; stop on a conflicting dirty
checkout. Do not rebuild the tool or modify the private application checkout.

Use the SAME completed run's combined.sqlite and the exact F1 IL analysis-gap
fact ID recorded in the previous local report. Prefer combined_fact_id; the
helper also accepts an original fact ID only when unique across the index.
Run with the existing Python installation:

```text
python scripts/inspect-retained-il-body.py --index "<existing combined.sqlite>" --fact-id "<F1 body-linked gap fact ID>" --local-details
```

Keep all detailed target identities local. The helper opens SQLite read-only,
joins only within one source index, verifies call/body artifact hashes, bounds
rows and VM work, and refuses ambiguous selectors. Failure is unavailable
evidence, NOT zero calls. Do not substitute a different run or broaden its bounds.

Read F1's returned IL call targets in offset order. Identify whether any exact
target is a standard database API or a direct helper. Preserve opcode and exact
signature evidence locally; do not classify a target solely by a familiar name.
Do not follow additional method bodies during this run.

Using the already retained diagnostic gaps, locate any gap linked to the exact
M4 source call fact and/or F1 body/call facts. Report the literal existing gap
kind and reason, if present. Distinguish bound/unbound artifact provenance from
the existence of a retained IL body. Do not promote provenance or invent a
reason when the graph does not record one.

Return only: elapsed time; helper success/failure; compiled-call count; value
observation, command-candidate and gap counts; whether an exact standard API
target exists in F1; and the recorded graph exclusion reason or unknown.
Sanitize targets to F1 -> API or F1 -> helper labels. No private paths, hashes,
SQL or symbols. Correct the earlier zero-CallEdge interpretation explicitly.

This task does not prove runtime database use or repair a graph. It establishes
what the existing IL evidence actually contains using the correct schema.
