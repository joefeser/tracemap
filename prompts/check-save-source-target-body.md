# Compare the source call, bridge destination, and inspected body

Two-minute total inspection budget after fast-forwarding the clean
codex/evidence-admission-hardening checkout. Preserve unrelated changes.
NO build, scan, preflight, requery, DLL search, SDK changes, or evidence edits.

Reuse the successful helper command from the previous run: same combined index,
same method graph, same exact body-linked fact ID. Add --source-call-id with the
exact combined_fact_id of the already-investigated M4 -> F1 source CallEdge
(the external wrapper invocation at line 827 in the prior local evidence).
Use the existing local receipt to recover this ID. If necessary, resolve that
specific retained CallEdge in the same index using the previously established
caller and span, verifying uniqueness and the explicit-reference witness.
Do not substitute MethodInvoked, a call at another span, a name-only match, or
an ID from an older scan. If the exact call cannot be recovered, stop.

Run scripts/inspect-retained-il-body.py once with the existing --index,
--fact-id, --graph and the added --source-call-id. Omit --local-details.
Return its count lines verbatim, exit status and elapsed time. Private IDs,
paths, symbols, signatures and hashes must remain local.

The new source_* counts directly compare the source witness to the inspected
owner, find its unique metadata target by full identity AND artifact hash,
join that target to its own IL body, and correlate the source call with actual
graph bridge destinations. A capped-at-two count of 2 means non-unique, not an
exhaustive total. Missing conditional counts are unavailable, not zero.

No narrative root-cause speculation or further investigation. In particular,
do not assert that provenance or System.Data admission blocks the path. This
update fixes missing diagnostic correlation; graph admission is unchanged and
the private application's database path remains unproven.
