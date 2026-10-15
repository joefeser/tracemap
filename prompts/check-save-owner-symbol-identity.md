# Correct the exact-owner readback

One-minute inspection budget after pulling the clean tool branch
codex/evidence-admission-hardening. Preserve unrelated changes.
NO build, scan, requery, preflight, DLL search, SDK changes, or code changes.

The previous helper's exact_body_owner_nodes counted representative declaration
fact IDs. GetOrAddSymbolNode creates valid graph nodes without those IDs, so zero
did NOT prove that F1 had no node. Also exact_body_owner_matches counted unique
owners in the index, not an identity comparison with the graph. The last report
overstated both measurements. Do not carry those conclusions forward.

Recover the EXACT successful helper invocation from the previous local run,
using the same fresh index, exact body-linked fact ID, and algorithm 1.7 graph.
Run it once using the updated scripts/inspect-retained-il-body.py, omitting
--local-details. Do not reconstruct or guess paths or fact IDs. If the invocation
is unavailable, stop and say so.

Return verbatim count lines only, plus helper exit status and elapsed seconds:
calls, exact_body_calls_in_graph, exact_body_calls_outside_graph,
exact_body_owner_matches, owner_artifact_hash_matches, exact_body_owner_nodes,
exact_body_owner_symbol_nodes, owner_symbol_nodes_in_other_indexes,
explicit_bridges_to_exact_owner.

No narrative root-cause inference, extra SQL, or follow-on investigation. The
new symbol-node count compares source index AND complete member identity; the
bridge count checks its exact destination node. Keep private details local.

Public regression: syntax-only page -> semantic source wrapper -> overloaded
external method with a ByRef parameter -> compiled ExecuteScalar terminal passes
both ordinary and bounded readers, while provenance remains unbound. This does
not establish the private application's path. No graph admission rules changed.
