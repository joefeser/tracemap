# Compare the exact retained body with the existing graph

Three-minute total limit. Read-only. NO build, scan, preflight, requery, DLL
search, SDK changes, application execution, or changes to evidence/checkpoints.
Fast-forward the clean tool checkout on codex/evidence-admission-hardening;
preserve unrelated changes and stop if they conflict.

Correction: the public regression proves that a System.Data 2.0 ExecuteScalar
member-reference can produce a database-api terminal through the bounded reader
without admitting System.Data.dll. Do not treat its absence as the explanation.
The unresolved difference is the earlier exact 26-call body versus the current
graph's 19 compiled calls across all reachable bodies.

Use the existing fresh combined.sqlite and algorithm 1.7 method-graph.local.json
from the latest successful requery. Recover the exact body-linked fact ID for
the previously inspected ExecuteScalar call from local records. Resolve it in
this fresh index by complete member identity AND artifact hash, never by method
name alone; distinguish overloads. If it cannot be identified uniquely, report
that and stop. Do not reuse a fact ID from a different scan without resolving it.

Run with the installed Python (substitute verified local paths/ID):

    python scripts/inspect-retained-il-body.py --index <combined.sqlite> --fact-id <exact-combined-body-or-call-fact-id> --graph <method-graph.local.json> --local-details

The helper verifies the graph's index SHA and compares exact combined body/call
IDs. Private details stay local. Return only sanitized counts and findings:

- Body call count, exact-body calls inside/outside graph, owner-match count,
  owner artifact-hash match, and owner declaration-node count.
- Whether this exact body still contains the SqlCommand.ExecuteScalar call;
  its opcode, referenceKind, trusted assembly token match, and offset.
- If body calls are absent from the graph, inspect only its explicit
  compiledFactId owner join and the exact member reached by the source bridge.
  Are they the same complete identity and same artifact bytes? Report the first
  demonstrated mismatch, not an inferred reason.
- If the exact call is present in graph.calls but has no API terminal, report
  its referenceKind and whether its body retains ilGeneratorSha256,
  ilBoundedInputSha256, and rawFileSha256. Do not broaden this investigation.

An owner declaration-node count of zero alone is not proof of unreachability:
nodes can represent other facts for the same symbol. No runtime, SQL identity,
stored-procedure, or clean-build claims. Stop at the time limit and preserve all
existing artifacts. No further changes or investigation in this run.
