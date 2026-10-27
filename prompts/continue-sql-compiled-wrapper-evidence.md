# Continue the bounded compiled-wrapper whole-path package locally

Work on the public corpus in `samples/sql-compiled-wrapper-evidence`. Read AGENTS,
the spec under `.kiro/specs/sql-compiled-wrapper-evidence`, its implementation state,
`docs/validation/SQL_COMPILED_WRAPPER_EVIDENCE.md`, current IL command/value rules,
and source SQL parameter/DataSet/helper contracts before editing.

This branch is a **partial foundation**, not SQL-A acceptance. Its finite matrix has
20 entry cases, two local Tier3 SQL-shape candidates and 20 whole-path unknowns.
Do not turn expected unknowns passing a smoke into a clean application verdict.
Do not request private inputs or a work-machine retry to fix missing capabilities.

First verify the exact branch head and existing validation record. Reproduce the
focused tests and compile-only native matrix. Preserve every existing source and
IL comparison contract, refusal, provenance field and budget. Never invoke fixture
methods, SQL providers, containers, a database or deployed application.

Before implementation, propose the smallest explicit compiled heap/object model
needed for this already-authored finite path. Obtain scope confirmation if it
requires a new interprocedural engine rather than a bounded extension. Priorities:

1. Actual encoded custom constructor stores and corresponding field/getter loads,
   exact unique caller/callee/type identities, positional argument substitution,
   assignment spans as IL locations, and binary/provenance binding. No name-based
   wrapper exceptions or source-derived assumptions about a compiled body.
2. Forward a fresh parameter object and returned DataSet through the exact call
   instance; refuse alias/escape/mutation, recursion, virtual ambiguity, unsupported
   control flow and competing assembly/body candidates. Keep IL at Tier3.
3. Model only demonstrably safe Merge/field consumer relations. Incoming data is
   not destination data; preserve unknown existing destination schema, schema
   union, mutation and aliases. Never claim row/value/type/null/runtime behavior.
4. Link a supported consumer to an independently admitted public SQL contract only
   when all required legs refer to the same operation/execution evidence. Keep
   server/database/procedure and source/binary/deployed identity separate.

Use both existing C# and VB cases and their mutation/escape/branch/Merge controls.
Add independent negatives for wrong assembly/key, stale hashes, missing/ambiguous
bodies, changed constructor/store/load, limits and unsupported destinations before
claiming any newly supported case. Do not remove unsupported cases or change their
expectations merely to make the matrix green. Extend rule limitations and retain
exact generator and bounded-input hashes on every new derived artifact.

Run focused matrix/version/scope/bounds first, then one frozen full Debug suite with
`-warnaserror`, relevant retained offline and pinned checks sequentially. No duplicate
test infrastructure, full DNN runtime, general SQL parser, join inference or new ORM
support. Push only the bounded validated branch when authorized; no PR, merge, ACK
policy change or review spend. Final return: exact head, supported legs, still-missing
legs, unchanged denominators, gate failures/reruns, and the smallest next action.

Do not ask for work-machine acceptance until locally implemented legs actually
cover the required compiled-wrapper and merged-field route. Owner schema/identity
inputs remain separate from implementation completeness.
